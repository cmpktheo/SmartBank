using FluentAssertions;
using NSubstitute;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.Cards.Domain;

namespace SmartBank.Cards.Domain.Tests;

public sealed class CardTests
{
    private static IPanProtector Protector()
    {
        var p = Substitute.For<IPanProtector>();
        p.Tokenize(Arg.Any<string>()).Returns("token");
        p.EncryptCvv(Arg.Any<string>()).Returns("enc");
        return p;
    }

    private static Card NewCard() => Card.IssueVirtual(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
        "4242424242424242", "123", 9, 2029, Money.Of(2000m, Currency.EUR), Money.Of(500m, Currency.EUR),
        Protector(), DateTimeOffset.UtcNow);

    [Fact]
    public void IssueVirtual_SetsActiveAndLastFour()
    {
        var c = NewCard();
        c.Status.Should().Be(CardStatus.Active);
        c.LastFour.Should().Be("4242");
    }

    [Fact]
    public void Freeze_FromActive_SetsFrozen()
    {
        var c = NewCard();
        c.Freeze().IsSuccess.Should().BeTrue();
        c.Status.Should().Be(CardStatus.Frozen);
    }

    [Fact]
    public void Freeze_Twice_IsIdempotent()
    {
        var c = NewCard();
        c.Freeze();
        c.Freeze().IsSuccess.Should().BeTrue();
        c.Status.Should().Be(CardStatus.Frozen);
    }

    [Fact]
    public void Unfreeze_FromActive_Fails()
    {
        NewCard().Unfreeze().Error.Code.Should().Be("CARD_NOT_FROZEN");
    }

    [Fact]
    public void Block_ThenFreeze_Fails()
    {
        var c = NewCard();
        c.Block();
        c.Freeze().Error.Code.Should().Be("CARD_BLOCKED");
    }

    [Fact]
    public void SetLimits_AboveMax_Fails()
    {
        var c = NewCard();
        c.SetLimits(Money.Of(20000m, Currency.EUR), Money.Of(500m, Currency.EUR)).Error.Code.Should().Be("CARD_LIMIT");
    }

    [Fact]
    public void SetLimits_OnBlocked_Fails()
    {
        var c = NewCard();
        c.Block();
        c.SetLimits(Money.Of(100m, Currency.EUR), Money.Of(100m, Currency.EUR)).Error.Code.Should().Be("CARD_BLOCKED");
    }
}
