using SmartBank.Ledger.Domain;

namespace SmartBank.Ledger.Application.Abstractions;

public sealed record AccountStatusInfo(bool Found, Guid AccountId, Guid CustomerId, string Status, string Currency, string AvailableBalance, string CustomerEmail, string Iban);
public sealed record IbanLookupInfo(bool Found, Guid AccountId, Guid CustomerId, string Status, string Currency, string CustomerEmail, string Iban);

public interface IAccountDirectory
{
    Task<AccountStatusInfo> GetStatusAsync(Guid accountId, CancellationToken ct);
    Task<IbanLookupInfo> LookupByIbanAsync(string iban, CancellationToken ct);
    Task<ReserveOutcome> ReserveAsync(Guid accountId, Guid holdId, Guid transactionId, decimal amount, string currency, CancellationToken ct);
    Task<SettleOutcome> CaptureAsync(Guid accountId, Guid holdId, CancellationToken ct);
    Task<SettleOutcome> CreditAsync(Guid accountId, Guid transactionId, decimal amount, string currency, CancellationToken ct);
}

public sealed record ReserveOutcome(bool Ok, string? ErrorCode, string? ErrorMessage);
public sealed record SettleOutcome(bool Ok, string? ErrorCode, string? ErrorMessage);

public interface IJournalRepository
{
    Task AddAsync(JournalTransaction tx, CancellationToken ct);
    /// <summary>Stage an outbox row in the same DbContext/transaction as the journal (atomic book+publish intent).</summary>
    void AddOutbox(SmartBank.BuildingBlocks.Infrastructure.Outbox.OutboxMessage message);
    Task SaveChangesAsync(CancellationToken ct);
    Task<List<JournalTransaction>> ListByAccountAsync(Guid accountId, DateTimeOffset? from, DateTimeOffset? to, string? direction, int page, int pageSize, CancellationToken ct);
    Task<int> CountByAccountAsync(Guid accountId, DateTimeOffset? from, DateTimeOffset? to, string? direction, CancellationToken ct);
}
