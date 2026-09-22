using SmartBank.BuildingBlocks.Domain;
using SmartBank.BuildingBlocks.Domain.ValueObjects;

namespace SmartBank.Customer.Domain;

public sealed class IbanFactory
{
    private readonly string _bankCode;
    private readonly string _sortCode;

    /// <summary>
    /// UK IBAN factory. Defaults to NWBK / 601613 (well-known UK test bank/sort code).
    /// </summary>
    public IbanFactory(string bankCode = "NWBK", string sortCode = "601613")
    {
        if (string.IsNullOrWhiteSpace(bankCode) || bankCode.Length != 4 || !bankCode.All(char.IsLetter))
            throw new DomainException("IBAN_BANK_CODE", "UK bank code must be 4 letters.");
        var digits = new string(sortCode.Where(char.IsDigit).ToArray());
        if (digits.Length != 6)
            throw new DomainException("IBAN_SORT_CODE", "UK sort code must be 6 digits.");
        _bankCode = bankCode.ToUpperInvariant();
        _sortCode = digits;
    }

    public Iban Create(long accountNumber)
    {
        // Sequence is 10 digits (e.g. 1000000001); UK account number is 8 digits — use last 8.
        var seq = new string(accountNumber.ToString("D10").Where(char.IsDigit).ToArray());
        var acct = seq[^8..];
        // Avoid all-zero account numbers.
        if (acct.All(c => c == '0')) acct = "00000001";
        return Iban.GenerateGb(_bankCode, _sortCode, acct);
    }
}
