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

    public async Task<List<JournalTransaction>> ListByAccountAsync(Guid accountId, DateTimeOffset? from, DateTimeOffset? to, string? direction, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.Lines.Where(l => l.AccountId == accountId).AsQueryable();
        if (from.HasValue) q = q.Where(l => l.BookedAt >= from.Value);
        if (to.HasValue) q = q.Where(l => l.BookedAt <= to.Value);
        if (direction is "Debit" or "Credit")
        {
            var dir = Enum.Parse<LedgerDirection>(direction);
            q = q.Where(l => l.Direction == dir);
        }
        var lines = await q.OrderByDescending(l => l.BookedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var txIds = lines.Select(l => l.TransactionId).Distinct().ToList();
        return await _db.Journals.Where(t => txIds.Contains(t.Id)).Include(t => t.Lines).ToListAsync(ct);
    }

    public async Task<int> CountByAccountAsync(Guid accountId, DateTimeOffset? from, DateTimeOffset? to, string? direction, CancellationToken ct)
    {
        var q = _db.Lines.Where(l => l.AccountId == accountId).AsQueryable();
        if (from.HasValue) q = q.Where(l => l.BookedAt >= from.Value);
        if (to.HasValue) q = q.Where(l => l.BookedAt <= to.Value);
        if (direction is "Debit" or "Credit")
        {
            var dir = Enum.Parse<LedgerDirection>(direction);
            q = q.Where(l => l.Direction == dir);
        }
        return await q.CountAsync(ct);
    }
}
