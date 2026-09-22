using SmartBank.BuildingBlocks.Domain;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.Cards.Domain.Events;

namespace SmartBank.Cards.Domain;

public enum CardStatus { Active = 1, Frozen = 2, Blocked = 3 }

public enum CardType { Debit = 1, Credit = 2 }

public enum CardBrand { Visa = 1, Mastercard = 2 }

public interface IPanProtector
{
    string Tokenize(string pan);
    string EncryptCvv(string cvv);
    string DecryptCvv(string cipher);
}

public sealed class Card : AggregateRoot<Guid>
{
    public Guid CustomerId { get; private set; }
    public Guid AccountId { get; private set; }
    public CardType Type { get; private set; } = CardType.Debit;
    public CardBrand Brand { get; private set; } = CardBrand.Visa;
    public string LastFour { get; private set; } = string.Empty;
    public string PanToken { get; private set; } = string.Empty;
    public int ExpiryMonth { get; private set; }
    public int ExpiryYear { get; private set; }
    public string CvvEnc { get; private set; } = string.Empty;
    public CardStatus Status { get; private set; }
    public Money DailyEcommerceLimit { get; private set; } = null!;
    public Money DailyAtmLimit { get; private set; } = null!;
    public DateTimeOffset IssuedAt { get; private set; }
    public string Currency { get; private set; } = "EUR";

    private Card() { }

    public static Card IssueVirtual(
        Guid id, Guid customerId, Guid accountId,
        string pan, string cvv, int expiryMonth, int expiryYear,
        Money ecomLimit, Money atmLimit,
        IPanProtector protector, DateTimeOffset now,
        CardType type = CardType.Debit, CardBrand brand = CardBrand.Visa)
    {
        if (pan.Length != 16 || !pan.All(char.IsDigit))
            throw new DomainException("CARD_PAN", "PAN must be 16 digits.");
        if (brand == CardBrand.Visa && !pan.StartsWith('4'))
            throw new DomainException("CARD_BRAND", "Visa PANs must start with 4.");
        if (cvv.Length != 3 || !cvv.All(char.IsDigit))
            throw new DomainException("CARD_CVV", "CVV must be 3 digits.");
        if (expiryMonth is < 1 or > 12) throw new DomainException("CARD_EXPIRY", "Invalid expiry.");

        var card = new Card
        {
            Id = id,
            CustomerId = customerId,
            AccountId = accountId,
            Type = type,
            Brand = brand,
            LastFour = pan[^4..],
            PanToken = protector.Tokenize(pan),
            CvvEnc = protector.EncryptCvv(cvv),
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Status = CardStatus.Active,
            DailyEcommerceLimit = ecomLimit,
            DailyAtmLimit = atmLimit,
            IssuedAt = now,
            Currency = ecomLimit.Currency.Code
        };
        card.Raise(new CardIssuedDomainEvent(id, accountId, customerId, card.LastFour));
        return card;
    }

    public Result Freeze()
    {
        if (Status == CardStatus.Blocked)
            return Result.Failure(Error.Conflict("CARD_BLOCKED", "Blocked cards cannot be frozen."));
        if (Status == CardStatus.Frozen) return Result.Success(); // idempotent
        Status = CardStatus.Frozen;
        Raise(new CardFrozenDomainEvent(Id));
        return Result.Success();
    }

    public Result Unfreeze()
    {
        if (Status != CardStatus.Frozen)
            return Result.Failure(Error.Conflict("CARD_NOT_FROZEN", "Only frozen cards can be unfrozen."));
        Status = CardStatus.Active;
        Raise(new CardUnfrozenDomainEvent(Id));
        return Result.Success();
    }

    public Result Block()
    {
        Status = CardStatus.Blocked;
        Raise(new CardBlockedDomainEvent(Id));
        return Result.Success();
    }

    public Result SetLimits(Money ecom, Money atm)
    {
        if (Status == CardStatus.Blocked)
            return Result.Failure(Error.Conflict("CARD_BLOCKED", "Cannot change limits on a blocked card."));
        if (ecom.Amount is < 0 or > 10_000) return Result.Failure(Error.Validation("CARD_LIMIT", "E-commerce limit 0–10000."));
        if (atm.Amount is < 0 or > 2_000) return Result.Failure(Error.Validation("CARD_LIMIT", "ATM limit 0–2000."));
        DailyEcommerceLimit = ecom;
        DailyAtmLimit = atm;
        return Result.Success();
    }
}
