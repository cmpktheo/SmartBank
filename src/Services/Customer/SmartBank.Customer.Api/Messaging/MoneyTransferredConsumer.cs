using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.BuildingBlocks.EventBus;
using SmartBank.BuildingBlocks.Infrastructure.Messaging;
using SmartBank.BuildingBlocks.Infrastructure.Outbox;
using SmartBank.BuildingBlocks.Web;
using SmartBank.Customer.Infrastructure.Persistence;

namespace SmartBank.Customer.Api.Messaging;

/// <summary>
/// Async settlement for booked transfers: CaptureHold(source) + CreditPosted(dest).
/// Idempotent on EventId (inbox_messages) and TransactionId (domain is idempotent).
/// This is where fraud/AML background checks hook in — settlement no longer blocks the booking API.
/// </summary>
public sealed class MoneyTransferredConsumer : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<MoneyTransferredConsumer> _log;

    public MoneyTransferredConsumer(IServiceProvider sp, ILogger<MoneyTransferredConsumer> log)
    {
        _sp = sp;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = _sp.CreateAsyncScope();
        var connection = scope.ServiceProvider.GetService<IConnection>();
        if (connection is null)
        {
            _log.LogInformation("MoneyTransferredConsumer disabled: no RabbitMQ connection");
            return;
        }

        await RabbitMqSetup.DeclareTopologyAsync(connection, LedgerTopology.Exchange,
            LedgerTopology.TransferredRoutingKey, LedgerTopology.CustomerQueue, stoppingToken, _log);

        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, 20, false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, ea) => HandleAsync(ea, channel, stoppingToken);

        await channel.BasicConsumeAsync(LedgerTopology.CustomerQueue, autoAck: false, consumer, stoppingToken);
        _log.LogInformation("MoneyTransferredConsumer listening on {Queue}", LedgerTopology.CustomerQueue);

        // Keep alive until shutdown; actual work happens in ReceivedAsync.
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
        await channel.CloseAsync(cancellationToken: CancellationToken.None);
    }

    internal async Task HandleAsync(BasicDeliverEventArgs ea, IChannel channel, CancellationToken ct)
    {
        var body = Encoding.UTF8.GetString(ea.Body.ToArray());
        var evt = MoneyTransferredIntegrationEvent.FromPayload(body);
        if (evt is null)
        {
            _log.LogWarning("Dropping unparseable MoneyTransferred message ({Bytes} bytes)", ea.Body.Length);
            await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
            return;
        }

        var traceparent = PoisonMessagePolicy.ReadHeader(ea, "traceparent");
        using (MessagingScope.Begin(evt.CorrelationId, evt.EventId, evt.TransactionId,
                   MoneyTransferredIntegrationEvent.TypeName, traceparent))
        {
            try
            {
                var settled = await SettleAsync(evt, ct);
                _log.LogInformation("Settled transfer {TransactionId} ({Reference}): {Outcome} (CorrelationId={CorrelationId})",
                    evt.TransactionId, evt.Reference, settled, evt.CorrelationId);
                await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
            }
            catch (Exception ex)
            {
                // Requeue while under budget (hold TTL 30s keeps this safe);
                // PoisonMessagePolicy dead-letters to .dead once exhausted.
                _log.LogError(ex, "Settlement failed for {TransactionId} (CorrelationId={CorrelationId}, delivery {Delivery})",
                    evt.TransactionId, evt.CorrelationId, PoisonMessagePolicy.DeliveryCount(ea) + 1);
                await PoisonMessagePolicy.NackOrDeadLetterAsync(
                    channel, ea, _log, $"transfer {evt.TransactionId}", ct);
            }
        }
    }

    internal async Task<string> SettleAsync(MoneyTransferredIntegrationEvent evt, CancellationToken ct)
    {
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();

        // Inbox dedupe: at-least-once delivery -> second delivery is a no-op.
        if (await db.InboxMessages.AnyAsync(x => x.Id == evt.EventId, ct))
            return "duplicate-ignored";

        var source = await db.BankAccounts.Include(a => a.Holds)
            .FirstOrDefaultAsync(a => a.Id == evt.SourceAccountId, ct);
        var dest = await db.BankAccounts.Include(a => a.Holds)
            .FirstOrDefaultAsync(a => a.Id == evt.DestinationAccountId, ct);

        if (source is null || dest is null)
        {
            // Source/dest missing after booking = data race or deleted account.
            // Record inbox to avoid infinite redelivery; reconciliation flags Ledger-vs-Customer diff (Ledger wins).
            _log.LogError("Settlement accounts missing for {TransactionId}: source={SourceFound} dest={DestFound}",
                evt.TransactionId, source is not null, dest is not null);
            db.InboxMessages.Add(new InboxMessage { Id = evt.EventId });
            await db.SaveChangesAsync(ct);
            return "accounts-missing-recorded";
        }

        var amount = Money.Of(evt.Amount, Currency.From(evt.Currency));

        // Background-check hook point: fraud/AML/sanctions screening runs here,
        // before value moves. v1 settles immediately; future checks can delay/
        // reject by throwing (requeue) or routing to risk queue without touching balances.
        var sourceResult = source.CaptureHold(evt.TransactionId);
        if (sourceResult.IsFailure)
        {
            _log.LogError("CaptureHold failed for {TransactionId}: {Code} {Message}",
                evt.TransactionId, sourceResult.Error.Code, sourceResult.Error.Message);
            throw new InvalidOperationException($"CaptureHold failed: {sourceResult.Error.Code}");
        }

        var creditResult = dest.CreditPosted(amount);
        if (creditResult.IsFailure)
        {
            _log.LogError("CreditPosted failed for {TransactionId}: {Code} {Message}",
                evt.TransactionId, creditResult.Error.Code, creditResult.Error.Message);
            throw new InvalidOperationException($"CreditPosted failed: {creditResult.Error.Code}");
        }

        db.InboxMessages.Add(new InboxMessage { Id = evt.EventId });
        await db.SaveChangesAsync(ct);
        return "settled";
    }
}
