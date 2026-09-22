using FluentAssertions;
using SmartBank.BuildingBlocks.Domain;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.Ledger.Domain;

namespace SmartBank.Ledger.Domain.Tests;

public sealed class JournalTests
{
    [Fact]
    public void Transfer_TwoLines_DebitEqualsCredit()
    {
        var r = JournalTransaction.Transfer(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Money.Of(100m, Currency.EUR), "DE89370400440532013000", "Rent", null, DateTimeOffset.UtcNow, "TRN-20260920-8F3C2A");
        r.IsSuccess.Should().BeTrue();
        r.Value!.Lines.Should().HaveCount(2);
        r.Value.Lines.Sum(l => l.Direction == LedgerDirection.Debit ? l.Amount.Amount : -l.Amount.Amount).Should().Be(0);
    }

    [Fact]
    public void Transfer_SameAccount_Fails()
    {
        var id = Guid.CreateVersion7();
        var r = JournalTransaction.Transfer(Guid.CreateVersion7(), id, id, Money.Of(10m, Currency.EUR), null, null, null, DateTimeOffset.UtcNow, "R");
        r.Error.Code.Should().Be("LEDGER_SAME_ACCOUNT");
    }

    [Fact]
    public void Transfer_ZeroAmount_Fails()
    {
        var r = JournalTransaction.Transfer(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Money.Zero(Currency.EUR), null, null, null, DateTimeOffset.UtcNow, "R");
        r.Error.Code.Should().Be("LEDGER_ZERO_AMOUNT");
    }

    [Fact]
    public void Transfer_RaisesFundsTransferredDomainEvent()
    {
        var r = JournalTransaction.Transfer(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Money.Of(10m, Currency.EUR), null, null, null, DateTimeOffset.UtcNow, "R");
        r.Value!.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "FundsTransferredDomainEvent");
    }

    [Fact]
    public void EnsureBalanced_DetectsUnbalanced_Throws()
    {
        var r = JournalTransaction.Transfer(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Money.Of(10m, Currency.EUR), null, null, null, DateTimeOffset.UtcNow, "R");
        var tx = r.Value!;
        tx.AddLineForTest(LedgerLine.Create(Guid.CreateVersion7(), tx.Id, Guid.CreateVersion7(), LedgerDirection.Debit, Money.Of(5m, Currency.EUR), DateTimeOffset.UtcNow));
        var act = () => tx.EnsureBalanced();
        act.Should().Throw<DomainException>().Where(e => e.Code == "LEDGER_UNBALANCED");
    }

    [Fact]
    public void Transfer_Reference_IsNotEmpty()
    {
        ReferenceGenerator.Generate(DateTimeOffset.UtcNow, Guid.CreateVersion7()).Should().MatchRegex(@"^TRN-\d{8}-[A-F0-9]{6}$");
    }
}
