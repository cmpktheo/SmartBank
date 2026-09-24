using FluentAssertions;
using SmartBank.BuildingBlocks.Domain;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.Customer.Domain;

namespace SmartBank.Customer.Domain.Tests;

public sealed class BankAccountTests
{
    private static BankAccount NewAccount(decimal posted = 1000m)
    {
        var acc = BankAccount.Open(Guid.CreateVersion7(), Guid.CreateVersion7(),
            Iban.Parse("DE89370400440532013000"), "Everyday", AccountType.Current, Currency.EUR, DateTimeOffset.UtcNow);
        acc.SetPostedForSeed(Money.Of(posted, Currency.EUR));
        acc.ClearDomainEvents();
        return acc;
    }

    [Fact]
    public void Open_RaisesAccountOpenedDomainEvent()
    {
        var acc = BankAccount.Open(Guid.CreateVersion7(), Guid.CreateVersion7(),
            Iban.Parse("DE89370400440532013000"), "Everyday", AccountType.Current, Currency.EUR, DateTimeOffset.UtcNow);
        acc.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "AccountOpenedDomainEvent");
    }

    [Fact]
    public void Open_AliasTooShort_Throws()
    {
        var act = () => BankAccount.Open(Guid.CreateVersion7(), Guid.CreateVersion7(),
            Iban.Parse("DE89370400440532013000"), "A", AccountType.Current, Currency.EUR, DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>().Where(e => e.Code == "ACCOUNT_ALIAS");
    }

    [Fact]
    public void PlaceHold_WhenActiveWithFunds_DecreasesAvailable()
    {
        var acc = NewAccount(1000m);
        var hold = Hold.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Money.Of(100m, Currency.EUR), DateTimeOffset.UtcNow.AddSeconds(30));
        acc.PlaceHold(hold, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        acc.AvailableBalance.Amount.Should().Be(900m);
    }

    [Fact]
    public void PlaceHold_Insufficient_ReturnsACCOUNT_INSUFFICIENT_FUNDS()
    {
        var acc = NewAccount(50m);
        var hold = Hold.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Money.Of(100m, Currency.EUR), DateTimeOffset.UtcNow.AddSeconds(30));
        var r = acc.PlaceHold(hold, DateTimeOffset.UtcNow);
        r.Error.Code.Should().Be("ACCOUNT_INSUFFICIENT_FUNDS");
    }

    [Fact]
    public void PlaceHold_WhenFrozen_ReturnsACCOUNT_NOT_ACTIVE()
    {
        var acc = NewAccount();
        acc.Freeze();
        var hold = Hold.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Money.Of(10m, Currency.EUR), DateTimeOffset.UtcNow.AddSeconds(30));
        acc.PlaceHold(hold, DateTimeOffset.UtcNow).Error.Code.Should().Be("ACCOUNT_NOT_ACTIVE");
    }

    [Fact]
    public void PlaceHold_SameHoldId_IsIdempotent()
    {
        var acc = NewAccount();
        var id = Guid.CreateVersion7();
        var h1 = Hold.Create(id, Guid.CreateVersion7(), Money.Of(10m, Currency.EUR), DateTimeOffset.UtcNow.AddSeconds(30));
        var h2 = Hold.Create(id, Guid.CreateVersion7(), Money.Of(10m, Currency.EUR), DateTimeOffset.UtcNow.AddSeconds(30));
        acc.PlaceHold(h1, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        acc.PlaceHold(h2, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        acc.Holds.Should().HaveCount(1);
    }

    [Fact]
    public void CaptureHold_DecreasesPosted_AndClearsHold()
    {
        var acc = NewAccount(1000m);
        var holdId = Guid.CreateVersion7();
        acc.PlaceHold(Hold.Create(holdId, holdId, Money.Of(100m, Currency.EUR), DateTimeOffset.UtcNow.AddSeconds(30)), DateTimeOffset.UtcNow);
        acc.CaptureHold(holdId);
        acc.PostedBalance.Amount.Should().Be(900m);
        acc.AvailableBalance.Amount.Should().Be(900m);
    }

    [Fact]
    public void ReleaseHold_RestoresAvailable_WithoutChangingPosted()
    {
        var acc = NewAccount(1000m);
        var holdId = Guid.CreateVersion7();
        acc.PlaceHold(Hold.Create(holdId, holdId, Money.Of(100m, Currency.EUR), DateTimeOffset.UtcNow.AddSeconds(30)), DateTimeOffset.UtcNow);
        acc.ReleaseHold(holdId);
        acc.AvailableBalance.Amount.Should().Be(1000m);
        acc.PostedBalance.Amount.Should().Be(1000m);
    }

    [Fact]
    public void CreditPosted_IncreasesPostedAndAvailable()
    {
        var acc = NewAccount(1000m);
        acc.CreditPosted(Money.Of(250m, Currency.EUR));
        acc.PostedBalance.Amount.Should().Be(1250m);
    }

    [Fact]
    public void Freeze_ThenPlaceHold_Fails()
    {
        var acc = NewAccount();
        acc.Freeze();
        acc.PlaceHold(Hold.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Money.Of(1m, Currency.EUR), DateTimeOffset.UtcNow.AddSeconds(30)), DateTimeOffset.UtcNow)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Unfreeze_FromActive_Fails()
    {
        var acc = NewAccount();
        acc.Unfreeze().Error.Code.Should().Be("ACCOUNT_NOT_FROZEN");
    }
}

public sealed class CustomerTests
{
    [Fact]
    public void Register_RaisesCustomerRegisteredDomainEvent()
    {
        var c = global::SmartBank.Customer.Domain.Customer.Register(Guid.CreateVersion7(), "Alex Morgan", "alex@x.test",
            Address.Create("Main 1", null, "Munich", "80331", "DE"), DateTimeOffset.UtcNow);
        c.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "CustomerRegisteredDomainEvent");
    }

    [Fact]
    public void Register_InvalidEmail_Throws()
    {
        var act = () => global::SmartBank.Customer.Domain.Customer.Register(Guid.CreateVersion7(), "Alex Morgan", "not-an-email",
            Address.Create("Main 1", null, "Munich", "80331", "DE"), DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void IbanFactory_Create_GeneratesNumericIban()
    {
        var factory = new IbanFactory("601613");
        var iban = factory.Create(1000000001);
        iban.Value.Should().StartWith("10");
        Iban.PassesMod97(iban.Value).Should().BeTrue();
    }

    [Fact]
    public void IbanFactory_DefaultFactory_GeneratesNumericIban()
    {
        var iban = new IbanFactory().Create(1000000002);
        iban.Value.Should().StartWith("10");
        Iban.PassesMod97(iban.Value).Should().BeTrue();
    }
}
