using SmartBank.BuildingBlocks.Domain;
using SmartBank.BuildingBlocks.Domain.ValueObjects;

namespace SmartBank.Customer.Domain;

public sealed class IbanFactory
{
    private readonly string _sortCode;

    /// <summary>
    /// Numeric IBAN factory.
    /// </summary>
    public IbanFactory(string sortCode = "601613")
    {
        var digits = new string(sortCode.Where(char.IsDigit).ToArray());
        if (digits.Length != 6)
            throw new DomainException("IBAN_SORT_CODE", "Sort code must be 6 digits.");
        _sortCode = digits;
    }

    public Iban Create(long accountNumber)
    {
        var seq = new string(accountNumber.ToString("D10").Where(char.IsDigit).ToArray());
        var acct = seq[^8..];
        if (acct.All(c => c == '0')) acct = "00000001";
        var bban = acct + _sortCode + seq;
        var check = 98 - Iban.Mod97(Iban.Rearrange("GB00" + bban));
        return Iban.Parse($"GB{check:00}{bban}");
    }
}
