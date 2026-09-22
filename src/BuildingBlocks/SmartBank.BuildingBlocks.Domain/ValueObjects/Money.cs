namespace SmartBank.BuildingBlocks.Domain.ValueObjects;

public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    // EF Core materialization constructor (backing fields are used for get-only properties).
    private Money()
    {
        Currency = null!;
    }

    private Money(decimal amount, Currency currency)
    {
        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven); // bankers rounding
        Currency = currency;
    }

    public static Money Of(decimal amount, Currency currency)
    {
        if (amount < 0m)
            throw new DomainException("MONEY_NEGATIVE", "Money amount cannot be negative. Use a directed ledger entry instead.");
        return new Money(amount, currency);
    }

    public static Money Zero(Currency currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        if (other.Amount > Amount)
            throw new DomainException("MONEY_UNDERFLOW", "Subtraction would produce a negative Money.");
        return new Money(Amount - other.Amount, Currency);
    }

    public bool IsZero => Amount == 0m;

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!Currency.Equals(other.Currency))
            throw new DomainException("MONEY_CURRENCY_MISMATCH", $"Cannot operate {Currency.Code} with {other.Currency.Code}.");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount:0.00} {Currency.Code}";
}
