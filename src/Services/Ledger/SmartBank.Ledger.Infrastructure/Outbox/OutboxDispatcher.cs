using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartBank.BuildingBlocks.EventBus;
using SmartBank.BuildingBlocks.Web;
using SmartBank.Ledger.Infrastructure.Persistence;

namespace SmartBank.Ledger.Infrastructure.Outbox;

/// <summary>
/// Polls outbox_messages and publishes to RabbitMQ (exchange smartbank.ledger).
/// At-least-once: consumers dedupe on EventId/TransactionId. Survives restarts.
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<OutboxDispatcher> _log;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 50;

    public OutboxDispatcher(IServiceProvider sp, ILogger<OutboxDispatcher> log)
    {
        _sp = sp;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give DB/RabbitMQ a moment during compose startup.
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Outbox dispatch failed; retrying");
            }
            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    internal async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        var publisher = scope.ServiceProvider.GetService<IEventPublisher>();
        if (publisher is null)
        {
            _log.LogDebug("Outbox dispatcher: no IEventPublisher registered, skipping");
            return 0;
        }

        var batch = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (batch.Count == 0) return 0;

        var done = 0;
        foreach (var msg in batch)
        {
            ct.ThrowIfCancellationRequested();
            // Restore correlation so every publish/failure log joins the originating request.
            using (MessagingScope.Begin(msg.CorrelationId, msg.Id, messageType: msg.Type))
            {
                try
                {
                    await publisher.PublishAsync(msg.Type, msg.Payload, msg.CorrelationId, ct).ConfigureAwait(false);
                    msg.ProcessedAt = DateTimeOffset.UtcNow;
                    msg.LastError = null;
                    done++;
                }
                catch (Exception ex)
                {
                    msg.Attempts++;
                    msg.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                    _log.LogWarning(ex, "Outbox publish failed for {OutboxId} ({Type}) attempt {Attempts} (CorrelationId={CorrelationId})",
                        msg.Id, msg.Type, msg.Attempts, msg.CorrelationId);
                }
            }
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        if (done > 0) _log.LogInformation("Outbox dispatched {Count} message(s)", done);
        return done;
    }
}
