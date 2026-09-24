using SmartBank.BuildingBlocks.Domain;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.Ledger.Domain.Events;

namespace SmartBank.Ledger.Domain;

public enum LedgerDirection { Debit = 1, Credit = 2 }
public enum JournalType { Transfer = 1, Fee = 2, Reversal = 3, Adjustment = 4, CardPayment = 5 }

public sealed class LedgerLine : Entity<Guid>
{
    public Guid TransactionId { get; private set; }
    public Guid AccountId { get; private set; }
    public LedgerDirection Direction { get; private set; }
    public Money Amount { get; private set; } = null!;
    public DateTimeOffset BookedAt { get; private set; }

    private LedgerLine() { }

    public static LedgerLine Create(Guid id, Guid transactionId, Guid accountId, LedgerDirection direction, Money amount, DateTimeOffset bookedAt)
        => new() { Id = id, TransactionId = transactionId, AccountId = accountId, Direction = direction, Amount = amount, BookedAt = bookedAt };
}

public sealed class JournalTransaction : AggregateRoot<Guid>
{
    public string Reference { get; private set; } = string.Empty;
    public JournalType Type { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTimeOffset BookedAt { get; private set; }
    public Guid? ReversesTransactionId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public Guid SourceAccountId { get; private set; }
    public Guid DestinationAccountId { get; private set; }
    public string? CounterpartyIban { get; private set; }
    public string? Narrative { get; private set; }

    private readonly List<LedgerLine> _lines = [];
    public IReadOnlyCollection<LedgerLine> Lines => _lines.AsReadOnly();

    private JournalTransaction() { }

    public static Result<JournalTransaction> Transfer(
        Guid id,
        Guid sourceAccountId,
        Guid destinationAccountId,
        Money amount,
        string? counterpartyIban,
        string? narrative,
        string? idempotencyKey,
        DateTimeOffset now,
        string reference,
        JournalType type = JournalType.Transfer)
    {
        if (sourceAccountId == destinationAccountId)
            return Result.Failure<JournalTransaction>(Error.Validation("LEDGER_SAME_ACCOUNT", "Source and destination must differ."));
        if (amount.IsZero)
            return Result.Failure<JournalTransaction>(Error.Validation("LEDGER_ZERO_AMOUNT", "Amount must be greater than zero."));

        var tx = new JournalTransaction
        {
            Id = id,
            Reference = reference,
            Type = type,
            Currency = amount.Currency.Code,
            BookedAt = now,
            SourceAccountId = sourceAccountId,
            DestinationAccountId = destinationAccountId,
            CounterpartyIban = counterpartyIban,
            Narrative = narrative,
            IdempotencyKey = idempotencyKey
        };

        // NB: each line needs its own Money instance — EF Core cannot track
        // a single owned-entity instance under two owners (the loser's
        // columns are omitted from the INSERT, e.g. amount => NULL).
        tx._lines.Add(LedgerLine.Create(Guid.CreateVersion7(), id, sourceAccountId, LedgerDirection.Debit, Money.Of(amount.Amount, amount.Currency), now));
        tx._lines.Add(LedgerLine.Create(Guid.CreateVersion7(), id, destinationAccountId, LedgerDirection.Credit, Money.Of(amount.Amount, amount.Currency), now));
        tx.EnsureBalanced();
        tx.Raise(new FundsTransferredDomainEvent(id, sourceAccountId, destinationAccountId, amount, reference, now));
        return Result.Success(tx);
    }

    public void AddLineForTest(LedgerLine line) => _lines.Add(line);

    public void EnsureBalanced()
    {
        var debit = _lines.Where(l => l.Direction == LedgerDirection.Debit).Sum(l => l.Amount.Amount);
        var credit = _lines.Where(l => l.Direction == LedgerDirection.Credit).Sum(l => l.Amount.Amount);
        if (debit != credit)
            throw new DomainException("LEDGER_UNBALANCED", $"Debits {debit} != credits {credit}.");
        if (_lines.Count < 2)
            throw new DomainException("LEDGER_MIN_LINES", "A journal must have at least two lines.");
        if (_lines.Select(l => l.Amount.Currency.Code).Distinct().Count() != 1)
            throw new DomainException("LEDGER_MIXED_CURRENCY", "All lines must share a currency.");
    }
}
