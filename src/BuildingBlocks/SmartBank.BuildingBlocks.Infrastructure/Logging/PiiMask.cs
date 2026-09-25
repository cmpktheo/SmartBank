namespace SmartBank.BuildingBlocks.Infrastructure.Logging;

/// <summary>
/// Masks PII before it reaches logs. Fund-transfer logs need joinable ids
/// (TransactionId, Reference, CorrelationId) — never raw IBANs, emails or secrets.
/// Storage (DB rows, message payloads) keeps full values for operations/audit;
/// only log lines go through here.
/// </summary>
public static class PiiMask
{
    /// <summary>DE89 3704 0044 0532 0130 00 -&gt; "DE89************3000". Short/empty values become "****".</summary>
    public static string MaskIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return "****";
        var v = iban.Replace(" ", string.Empty);
        return v.Length <= 8 ? "****" : v[..4] + new string('*', v.Length - 8) + v[^4..];
    }

    /// <summary>jane.doe@example.com -&gt; "j***@example.com". Unparseable values become "****".</summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "****";
        var at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1) return "****";
        return $"{email[0]}***{email[at..]}";
    }
}
