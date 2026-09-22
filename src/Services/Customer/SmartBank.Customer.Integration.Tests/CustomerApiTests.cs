// Customer integration tests — require Docker. Frozen names from Spec 05.
namespace SmartBank.Customer.Integration.Tests;

public sealed class CustomerApiTests
{
    private const string Skip = "Requires Docker (Testcontainers). Run in CI with Docker.";

    [Fact(Skip = Skip)] public void OpenAccount_PersistsIbanWithValidChecksum() => Assert.Fail(Skip);
    [Fact(Skip = Skip)] public void GetMyAccounts_ReturnsSeededBalances() => Assert.Fail(Skip);
    [Fact(Skip = Skip)] public void ReserveFunds_Grpc_DecreasesAvailable() => Assert.Fail(Skip);
    [Fact(Skip = Skip)] public void ReserveFunds_Grpc_Insufficient_ReturnsErrorCode() => Assert.Fail(Skip);
    [Fact(Skip = Skip)] public void MoneyTransferred_Consumer_CapturesHoldAndCreditsDestination() => Assert.Fail(Skip);
    [Fact(Skip = Skip)] public void HoldExpiry_ReleasesAfterTtl() => Assert.Fail(Skip);
}
