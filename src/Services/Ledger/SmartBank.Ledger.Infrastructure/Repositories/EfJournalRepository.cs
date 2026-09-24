using Microsoft.EntityFrameworkCore;
using SmartBank.Ledger.Application.Abstractions;
using SmartBank.Ledger.Domain;
using SmartBank.Ledger.Infrastructure.Persistence;

namespace SmartBank.Ledger.Infrastructure.Repositories;

public sealed class EfJournalRepository : IJournalRepository
{
    private readonly LedgerDbContext _db;
    public EfJournalRepository(LedgerDbContext db) => _db = db;

    public Task AddAsync(JournalTransaction tx, CancellationToken ct) => _db.Journals.AddAsync(tx, ct).AsTask();

    public void AddOutbox(SmartBank.BuildingBlocks.Infrastructure.Outbox.OutboxMessage message)
        => _db.OutboxMessages.Add(message);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public async Task<List<JournalTransaction>> ListByAccountAsync(Guid accountId, DateTimeOffset? from, DateTimeOffset? to, string? direction, JournalType? kind, int page, int pageSize, CancellationToken ct)
    {
        var q = FilteredLines(accountId, from, to, direction, kind);
        var lines = await q.OrderByDescending(l => l.BookedAt).ThenByDescending(l => l.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var txIds = lines.Select(l => l.TransactionId).Distinct().ToList();
        return await _db.Journals.Where(t => txIds.Contains(t.Id)).Include(t => t.Lines).ToListAsync(ct);
    }

    public async Task<int> CountByAccountAsync(Guid accountId, DateTimeOffset? from, DateTimeOffset? to, string? direction, JournalType? kind, CancellationToken ct)
    {
        return await FilteredLines(accountId, from, to, direction, kind).CountAsync(ct);
    }

    public async Task<decimal> SumSignedOlderThanAsync(Guid accountId, DateTimeOffset? from, DateTimeOffset? to, string? direction, JournalType? kind, DateTimeOffset bookedAt, Guid lineId, CancellationToken ct)
    {
        var q = FilteredLines(accountId, from, to, direction, kind)
            .Where(l => l.BookedAt < bookedAt || (l.BookedAt == bookedAt && l.Id.CompareTo(lineId) < 0));
        return await q.SumAsync(l => l.Direction == LedgerDirection.Credit ? l.Amount.Amount : -l.Amount.Amount, ct);
    }

    private IQueryable<LedgerLine> FilteredLines(Guid accountId, DateTimeOffset? from, DateTimeOffset? to, string? direction, JournalType? kind)
    {
        var q = _db.Lines.Where(l => l.AccountId == accountId).AsQueryable();
        if (from.HasValue) q = q.Where(l => l.BookedAt >= from.Value);
        if (to.HasValue) q = q.Where(l => l.BookedAt <= to.Value);
        if (direction is "Debit" or "Credit")
        {
            var dir = Enum.Parse<LedgerDirection>(direction);
            q = q.Where(l => l.Direction == dir);
        }
        if (kind.HasValue)
            q = q.Where(l => _db.Journals.Any(j => j.Id == l.TransactionId && j.Type == kind.Value));
        return q;
    }
}
