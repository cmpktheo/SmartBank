using SmartBank.BuildingBlocks.Domain;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.Customer.Domain.Events;

namespace SmartBank.Customer.Domain;

public sealed class BankAccount : AggregateRoot<Guid>
{
    public Guid CustomerId { get; private set; }
    public Iban Iban { get; private set; } = null!;
    public string Alias { get; private set; } = string.Empty;
    public AccountType Type { get; private set; }
    public AccountStatus Status { get; private set; }
    public Currency Currency { get; private set; } = null!;
    public Money AvailableBalance { get; private set; } = null!;
    public Money PostedBalance { get; private set; } = null!;
    public Money HoldTotal { get; private set; } = null!;
    public Money OverdraftLimit { get; private set; } = null!;
    public DateTimeOffset OpenedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private readonly List<Hold> _holds = [];
    public IReadOnlyCollection<Hold> Holds => _holds.AsReadOnly();

    private BankAccount() { }

    public static BankAccount Open(
        Guid id, Guid customerId, Iban iban, string alias, AccountType type,
        Currency currency, DateTimeOffset now)
    {
        if (alias is null || alias.Trim().Length is < 2 or > 40)
            throw new DomainException("ACCOUNT_ALIAS", "Alias must be 2–40 characters.");
        var a = new BankAccount
        {
            Id = id,
            CustomerId = customerId,
            Iban = iban,
            Alias = alias.Trim(),
            Type = type,
            Status = AccountStatus.Active,
            Currency = currency,
            AvailableBalance = Money.Zero(currency),
            PostedBalance = Money.Zero(currency),
            HoldTotal = Money.Zero(currency),
            OverdraftLimit = Money.Zero(currency),
            OpenedAt = now
        };
        a.Raise(new AccountOpenedDomainEvent(id, customerId, iban.Value, currency.Code, type.ToString(), now));
        return a;
    }

    public Result PlaceHold(Hold hold, DateTimeOffset now)
    {
        _ = now;
        if (Status != AccountStatus.Active)
            return Result.Failure(Error.Conflict("ACCOUNT_NOT_ACTIVE", $"Account is {Status}."));
        if (!hold.Amount.Currency.Equals(Currency))
            return Result.Failure(Error.Validation("ACCOUNT_CURRENCY", "Hold currency mismatch."));
        if (_holds.Any(h => h.Id == hold.Id))
            return Result.Success(); // idempotent replay
        var spendable = PostedBalance.Add(OverdraftLimit).Subtract(HoldTotal);
        if (hold.Amount.CompareTo(spendable) > 0)
            return Result.Failure(Error.Conflict("ACCOUNT_INSUFFICIENT_FUNDS",
                $"Available {spendable} is less than {hold.Amount}."));
        _holds.Add(hold);
        Recalc();
        return Result.Success();
    }

    public Result CaptureHold(Guid holdId)
    {
        var hold = _holds.FirstOrDefault(h => h.Id == holdId);
        if (hold is null) return Result.Success(); // already captured/expired — idempotent
        if (hold.State == HoldState.Captured) return Result.Success();
        hold.Capture();
        PostedBalance = PostedBalance.Subtract(hold.Amount);
        Recalc();
        return Result.Success();
    }

    public Result CreditPosted(Money amount)
    {
        if (Status is AccountStatus.Closed)
            return Result.Failure(Error.Conflict("ACCOUNT_CLOSED", "Account is closed."));
        PostedBalance = PostedBalance.Add(amount);
        Recalc();
        return Result.Success();
    }

    // Test seam: set posted balance (seed / tests only).
    public void SetPostedForSeed(Money posted)
    {
        PostedBalance = posted;
        Recalc();
    }

    public void AddHoldForTest(Hold hold)
    {
        _holds.Add(hold);
        Recalc();
    }

    public Result ReleaseHold(Guid holdId)
    {
        var hold = _holds.FirstOrDefault(h => h.Id == holdId);
        if (hold is null) return Result.Success();
        hold.Release();
        Recalc();
        return Result.Success();
    }

    public Result Freeze()
    {
        if (Status == AccountStatus.Closed)
            return Result.Failure(Error.Conflict("ACCOUNT_CLOSED", "Closed account cannot be frozen."));
        Status = AccountStatus.Frozen;
        Raise(new AccountFrozenDomainEvent(Id));
        return Result.Success();
    }

    public Result Unfreeze()
    {
        if (Status != AccountStatus.Frozen)
            return Result.Failure(Error.Conflict("ACCOUNT_NOT_FROZEN", "Only frozen accounts can be unfrozen."));
        Status = AccountStatus.Active;
        return Result.Success();
    }

    private void Recalc()
    {
        HoldTotal = _holds.Where(h => h.State == HoldState.Active)
            .Aggregate(Money.Zero(Currency), (acc, h) => acc.Add(h.Amount));
        var floor = Money.Zero(Currency);
        var spendable = PostedBalance.Add(OverdraftLimit).Subtract(HoldTotal);
        AvailableBalance = spendable.CompareTo(floor) < 0 ? floor : spendable;
    }
}
