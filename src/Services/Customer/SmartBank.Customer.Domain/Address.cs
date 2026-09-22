using SmartBank.BuildingBlocks.Domain;

namespace SmartBank.Customer.Domain;

public sealed class Address : ValueObject
{
    public string Line1 { get; }
    public string? Line2 { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string CountryCode { get; }

    private static readonly HashSet<string> AllowedCountries =
        ["DE", "FR", "NL", "IE", "AT", "BE", "ES", "IT", "PT", "LU", "GB"];

    private Address(string line1, string? line2, string city, string postalCode, string countryCode)
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        PostalCode = postalCode;
        CountryCode = countryCode;
    }

    public static Address Create(string line1, string? line2, string city, string postalCode, string countryCode)
    {
        line1 = (line1 ?? "").Trim();
        city = (city ?? "").Trim();
        postalCode = (postalCode ?? "").Trim();
        countryCode = (countryCode ?? "").Trim().ToUpperInvariant();
        if (line1.Length is < 1 or > 100) throw new DomainException("ADDRESS_LINE1", "Line1 must be 1–100 characters.");
        if (line2 is not null && line2.Trim().Length > 100) throw new DomainException("ADDRESS_LINE2", "Line2 max 100 characters.");
        if (city.Length is < 1 or > 60) throw new DomainException("ADDRESS_CITY", "City must be 1–60 characters.");
        if (postalCode.Length is < 1 or > 12) throw new DomainException("ADDRESS_POSTAL", "PostalCode must be 1–12 characters.");
        if (!AllowedCountries.Contains(countryCode)) throw new DomainException("ADDRESS_COUNTRY", $"Country '{countryCode}' not supported.");
        return new Address(line1, string.IsNullOrWhiteSpace(line2) ? null : line2.Trim(), city, postalCode, countryCode);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Line1;
        yield return Line2;
        yield return City;
        yield return PostalCode;
        yield return CountryCode;
    }
}
