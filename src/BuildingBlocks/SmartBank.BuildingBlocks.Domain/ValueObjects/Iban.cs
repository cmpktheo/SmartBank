namespace SmartBank.BuildingBlocks.Domain.ValueObjects;

public sealed class Iban : ValueObject
{
    public string Value { get; } // compact, uppercase, no spaces

    private Iban(string value) => Value = value;

    public static Iban Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new DomainException("IBAN_EMPTY", "IBAN is required.");

        var compact = new string(raw.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (compact.Length is < 15 or > 34)
            throw new DomainException("IBAN_LENGTH", "IBAN length must be between 15 and 34.");

        if (!compact.Take(2).All(char.IsLetter) || !compact.Skip(2).Take(2).All(char.IsDigit))
            throw new DomainException("IBAN_FORMAT", "IBAN must start with 2 letters and 2 check digits.");

        if (!PassesMod97(compact))
            throw new DomainException("IBAN_CHECKSUM", "IBAN checksum is invalid.");

        return new Iban(compact);
    }

    public static bool TryParse(string raw, out Iban? iban)
    {
        try { iban = Parse(raw); return true; }
        catch (DomainException) { iban = null; return false; }
    }

    public static Iban GenerateDe(string bban)
    {
        // Build DE + 00 + BBAN, compute check digits.
        var compact = "DE00" + bban;
        var check = 98 - Mod97(Rearrange(compact));
        return Parse($"DE{check:00}{bban}");
    }

    public static Iban GenerateGb(string bankCode, string sortCode, string accountNumber)
    {
        // UK BBAN = 4-letter bank code + 6-digit sort code + 8-digit account number (18 chars).
        // IBAN = GB + 2 check digits + BBAN (22 chars total).
        if (string.IsNullOrWhiteSpace(bankCode) || bankCode.Length != 4 || !bankCode.All(char.IsLetter))
            throw new DomainException("IBAN_BANK_CODE", "UK bank code must be 4 letters.");
        var digitsSort = new string(sortCode.Where(char.IsDigit).ToArray());
        var digitsAcct = new string(accountNumber.Where(char.IsDigit).ToArray());
        if (digitsSort.Length != 6)
            throw new DomainException("IBAN_SORT_CODE", "UK sort code must be 6 digits.");
        if (digitsAcct.Length != 8)
            throw new DomainException("IBAN_ACCOUNT_NUMBER", "UK account number must be 8 digits.");
        var bban = bankCode.ToUpperInvariant() + digitsSort + digitsAcct;
        var compact = "GB00" + bban;
        var check = 98 - Mod97(Rearrange(compact));
        return Parse($"GB{check:00}{bban}");
    }

    public static bool PassesMod97(string compact)
    {
        return Mod97(Rearrange(compact)) == 1;
    }

    private static string Rearrange(string compact) => compact[4..] + compact[..4];

    private static int Mod97(string rearranged)
    {
        var numeric = string.Concat(rearranged.Select(c =>
            char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString()));

        long remainder = 0;
        foreach (var chunk in Chunk(numeric, 7))
        {
            remainder = long.Parse(remainder.ToString() + chunk, System.Globalization.CultureInfo.InvariantCulture) % 97;
        }
        return (int)remainder;
    }

    private static IEnumerable<string> Chunk(string s, int size)
    {
        for (var i = 0; i < s.Length; i += size)
            yield return s.Substring(i, Math.Min(size, s.Length - i));
    }

    public string Formatted
    {
        get
        {
            var chars = Value.ToCharArray();
            return string.Join(" ", Enumerable.Range(0, (chars.Length + 3) / 4)
                .Select(i => new string(chars.Skip(i * 4).Take(4).ToArray())));
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
