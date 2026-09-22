namespace SmartBank.Ledger.Domain;

public static class ReferenceGenerator
{
    // NB: use the trailing hex (random in Guid v7) — the leading hex is a
    // timestamp and collides for transactions created close together,
    // violating the unique reference index.
    public static string Generate(DateTimeOffset now, Guid id)
        => $"TRN-{now:yyyyMMdd}-{id.ToString("N")[^6..].ToUpperInvariant()}";
}
