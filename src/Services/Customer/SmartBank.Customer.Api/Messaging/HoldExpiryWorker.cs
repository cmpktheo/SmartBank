using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartBank.BuildingBlocks.Web;
using SmartBank.Customer.Domain;
using SmartBank.Customer.Infrastructure.Persistence;

namespace SmartBank.Customer.Api.Messaging;

/// <summary>
/// Compensation for the Reserve -> Book window: if Ledger crashes after Reserve
/// but before booking (or the outbox never delivers), the 30s hold TTL expires
/// and AvailableBalance is restored. Runs every 10s.
/// </summary>
public sealed class HoldExpiryWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<HoldExpiryWorker> _log;

    public HoldExpiryWorker(IServiceProvider sp, ILogger<HoldExpiryWorker> log)
    {
        _sp = sp;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireBatchAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Hold expiry sweep failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    internal async Task<int> ExpireBatchAsync(DateTimeOffset now, CancellationToken ct)
    {
        // Each sweep gets its own CorrelationId + OperationName so the
        // "Released N expired hold(s)" line is findable in Loki/Tempo.
        using (MessagingScope.BeginOperation("HoldExpirySweep"))
        {
            await using var scope = _sp.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();

        var expiredHoldIds = await db.Holds
            .Where(h => h.State == HoldState.Active && h.ExpiresAt < now)
            .Select(h => h.Id)
            .Take(100)
            .ToListAsync(ct);
        if (expiredHoldIds.Count == 0) return 0;

        // Load parent accounts with holds (EF has no navigational back-ref, so per-account release).
        var accounts = await db.BankAccounts.Include(a => a.Holds).ToListAsync(ct);
        var released = 0;
        foreach (var account in accounts)
        {
            foreach (var holdId in expiredHoldIds)
            {
                var r = account.ReleaseHold(holdId);
                if (r.IsSuccess) released++;
            }
        }
        if (released > 0)
        {
            await db.SaveChangesAsync(ct);
            _log.LogInformation("Released {Count} expired hold(s)", released);
        }
        return released;
        }
    }
}
