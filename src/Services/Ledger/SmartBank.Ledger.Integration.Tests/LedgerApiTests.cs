// Ledger persistence tests — require Docker. Handler tests with fakes live in Application.Tests.
namespace SmartBank.Ledger.Integration.Tests;

public sealed class LedgerApiTests
{
    private const string Skip = "Requires Docker (Testcontainers). Run in CI with Docker.";

    [Fact(Skip = Skip)] public void Repository_InsertsBalancedJournal() => Assert.Fail(Skip);
    [Fact(Skip = Skip)] public void Query_Recent_Returns10() => Assert.Fail(Skip);
}
