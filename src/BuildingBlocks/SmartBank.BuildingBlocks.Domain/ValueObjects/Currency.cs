namespace SmartBank.BuildingBlocks.Domain.ValueObjects;

public sealed class Currency : ValueObject
{
    public static readonly Currency EUR = new("EUR");
    public static readonly Currency USD = new("USD");
    public static readonly Currency GBP = new("GBP");

    private static readonly HashSet<string> Allowed = ["EUR", "USD", "GBP"];

    public string Code { get; }

    // EF Core materialization constructor.
    private Currency() => Code = null!;

    private Currency(string code) => Code = code;

    public static Currency From(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || !Allowed.Contains(code))
            throw new DomainException("CURRENCY_UNSUPPORTED", $"Unsupported currency '{code}'.");
        return code switch
        {
            "EUR" => EUR,
            "USD" => USD,
            "GBP" => GBP,
            _ => throw new DomainException("CURRENCY_UNSUPPORTED", $"Unsupported currency '{code}'.")
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Code;
    }
}
