using FluentAssertions;
using SmartBank.BuildingBlocks.Domain;
using SmartBank.BuildingBlocks.Domain.ValueObjects;

namespace SmartBank.BuildingBlocks.Domain.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void Money_Add_SameCurrency_ReturnsSum()
    {
        var a = Money.Of(10.00m, Currency.EUR);
        var b = Money.Of(5.25m, Currency.EUR);
        a.Add(b).Amount.Should().Be(15.25m);
    }

    [Fact]
    public void Money_Add_DifferentCurrency_ThrowsDomainException()
    {
        var a = Money.Of(10m, Currency.EUR);
        var b = Money.Of(5m, Currency.USD);
        var act = () => a.Add(b);
        act.Should().Throw<DomainException>().WithMessage("*EUR*USD*");
    }

    [Fact]
    public void Money_Subtract_GreaterThanAmount_ThrowsDomainException()
    {
        var a = Money.Of(5m, Currency.EUR);
        var b = Money.Of(10m, Currency.EUR);
        var act = () => a.Subtract(b);
        act.Should().Throw<DomainException>().Where(e => e.Code == "MONEY_UNDERFLOW");
    }

    [Fact]
    public void Money_Negative_ThrowsDomainException()
    {
        var act = () => Money.Of(-1m, Currency.EUR);
        act.Should().Throw<DomainException>().Where(e => e.Code == "MONEY_NEGATIVE");
    }

    [Theory]
    [InlineData(1.225, 1.22)]
    [InlineData(1.235, 1.24)]
    public void Money_RoundsHalfToEven(decimal input, decimal expected)
    {
        Money.Of(input, Currency.EUR).Amount.Should().Be(expected);
    }

    [Fact]
    public void Currency_From_Unsupported_Throws()
    {
        var act = () => Currency.From("CHF");
        act.Should().Throw<DomainException>().Where(e => e.Code == "CURRENCY_UNSUPPORTED");
    }

    [Fact]
    public void Iban_Parse_ValidDe_Succeeds()
    {
        var iban = Iban.Parse("DE89370400440532013000");
        iban.Value.Should().Be("DE89370400440532013000");
    }

    [Fact]
    public void Iban_Parse_BadChecksum_Throws()
    {
        var act = () => Iban.Parse("DE89370400440532013001");
        act.Should().Throw<DomainException>().Where(e => e.Code == "IBAN_CHECKSUM");
    }

    [Fact]
    public void Iban_Parse_LowercaseAndSpaces_Normalizes()
    {
        var iban = Iban.Parse("de89 3704 0044 0532 0130 00");
        iban.Value.Should().Be("DE89370400440532013000");
    }

    [Fact]
    public void Iban_GenerateDe_HasValidChecksum()
    {
        var iban = Iban.GenerateDe("700400410000000001");
        Iban.PassesMod97(iban.Value).Should().BeTrue();
        iban.Value.Should().StartWith("DE");
    }

    [Fact]
    public void Result_Success_IsSuccess()
    {
        Result.Success().IsSuccess.Should().BeTrue();
        Result.Failure(Error.Validation("X", "y")).IsFailure.Should().BeTrue();
    }

    private sealed record TestEvent(Guid AggregateId) : DomainEvent;

    private sealed class TestAggregate : AggregateRoot<Guid>
    {
        public TestAggregate(Guid id) => Id = id;
        public void DoSomething() => Raise(new TestEvent(Id));
    }

    [Fact]
    public void AggregateRoot_Raise_AddsDomainEvent()
    {
        var agg = new TestAggregate(Guid.CreateVersion7());
        agg.DoSomething();
        agg.DomainEvents.Should().HaveCount(1);
    }
}
