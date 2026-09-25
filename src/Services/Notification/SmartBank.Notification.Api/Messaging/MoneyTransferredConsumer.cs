using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SmartBank.BuildingBlocks.Application.Metrics;
using SmartBank.BuildingBlocks.EventBus;
using SmartBank.BuildingBlocks.Infrastructure.Logging;
using SmartBank.BuildingBlocks.Infrastructure.Messaging;
using SmartBank.BuildingBlocks.Web;

namespace SmartBank.Notification.Api.Messaging;

/// <summary>
/// Writes a transfer email log row per booked transfer. Idempotent on EventId (unique index).
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
            _log.LogInformation("Notification MoneyTransferredConsumer disabled: no RabbitMQ connection");
            return;
        }

        await RabbitMqSetup.DeclareTopologyAsync(connection, LedgerTopology.Exchange,
            LedgerTopology.TransferredRoutingKey, LedgerTopology.NotificationQueue, stoppingToken, _log);

        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, 20, false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, ea) => HandleAsync(ea, channel, stoppingToken);

        await channel.BasicConsumeAsync(LedgerTopology.NotificationQueue, autoAck: false, consumer, stoppingToken);
        _log.LogInformation("Notification consumer listening on {Queue}", LedgerTopology.NotificationQueue);

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
        await channel.CloseAsync(cancellationToken: CancellationToken.None);
    }

    internal async Task HandleAsync(BasicDeliverEventArgs ea, IChannel channel, CancellationToken ct)
    {
        var body = Encoding.UTF8.GetString(ea.Body.ToArray());
        var evt = MoneyTransferredIntegrationEvent.FromPayload(body);
        if (evt is null)
        {
            _log.LogWarning("Dropping unparseable MoneyTransferred notification ({Bytes} bytes)", ea.Body.Length);
            await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
            return;
        }

        using (MessagingScope.Begin(evt.CorrelationId, evt.EventId, evt.TransactionId,
                   MoneyTransferredIntegrationEvent.TypeName, PoisonMessagePolicy.ReadHeader(ea, "traceparent")))
        {
            try
            {
                await using var scope = _sp.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                if (await db.Log.AnyAsync(x => x.EventId == evt.EventId, ct))
                {
                    _log.LogInformation("Duplicate notification for {EventId} ignored (CorrelationId={CorrelationId})",
                        evt.EventId, evt.CorrelationId);
                    await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
                    return;
                }

                db.Log.Add(new NotificationEntry
                {
                    EventId = evt.EventId,
                    Channel = "Email",
                    Template = "TransferBooked",
                    Recipient = evt.DestinationIban,
                    Subject = $"Transfer {evt.Reference} booked",
                    Body = NotificationDbContext.TransferBody(evt.Reference,
                        evt.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                        evt.Currency, evt.SourceIban, evt.DestinationIban, evt.OccurredAt.ToString("O")),
                    Payload = body,
                });
                await db.SaveChangesAsync(ct);
                SmartBankMeters.NotificationSent("TransferBooked", "Email");
                // DB keeps the full payload for audit; the log line carries
                // the masked IBAN only — enough to find the row, safe for Loki.
                _log.LogInformation("Logged transfer notification {TransactionId} to {Recipient} (CorrelationId={CorrelationId})",
                    evt.TransactionId, PiiMask.MaskIban(evt.DestinationIban), evt.CorrelationId);
                await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
            {
                // Unique(EventId) race between two deliveries -> already logged.
                _log.LogInformation("Duplicate notification for {EventId} ignored (CorrelationId={CorrelationId})",
                    evt.EventId, evt.CorrelationId);
                await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Notification handling failed for {TransactionId} (CorrelationId={CorrelationId}, delivery {Delivery})",
                    evt.TransactionId, evt.CorrelationId, PoisonMessagePolicy.DeliveryCount(ea) + 1);
                await PoisonMessagePolicy.NackOrDeadLetterAsync(
                    channel, ea, _log, $"notification {evt.TransactionId}", ct);
            }
        }
    }
}
