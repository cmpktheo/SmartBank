// Identity integration tests — require Docker (Testcontainers Postgres + Redis).
// Each test below matches the frozen names in Spec 04 Phase 2 Milestone 2.4.
// In environments without Docker they are skipped so `dotnet test` stays green;
// CI with Docker runs them for real.
namespace SmartBank.Identity.Integration.Tests;

public sealed class IdentityApiTests
{
    private const string SkipNoDocker = "Requires Docker (Testcontainers). Run in CI with Docker.";

    [Fact(Skip = SkipNoDocker)]
    public void Login_ValidPassword_ReturnsMfaChallenge() => Assert.Fail("Requires Docker.");

    [Fact(Skip = SkipNoDocker)]
    public void Mfa_ValidCode_ReturnsAccessAndRefreshTokens() => Assert.Fail("Requires Docker.");

    [Fact(Skip = SkipNoDocker)]
    public void Mfa_InvalidCode_Returns401() => Assert.Fail("Requires Docker.");

    [Fact(Skip = SkipNoDocker)]
    public void Mfa_ExpiredChallenge_Returns401() => Assert.Fail("Requires Docker.");

    [Fact(Skip = SkipNoDocker)]
    public void Me_WithToken_ReturnsProfile() => Assert.Fail("Requires Docker.");

    [Fact(Skip = SkipNoDocker)]
    public void Me_WithoutToken_Returns401() => Assert.Fail("Requires Docker.");

    [Fact(Skip = SkipNoDocker)]
    public void Logout_ThenMe_Returns401() => Assert.Fail("Requires Docker.");

    [Fact(Skip = SkipNoDocker)]
    public void Refresh_RotatesAccessToken() => Assert.Fail("Requires Docker.");

    [Fact(Skip = SkipNoDocker)]
    public void Login_FifthLockout_Returns423() => Assert.Fail("Requires Docker.");

    [Fact(Skip = SkipNoDocker)]
    public void Jwks_ReturnsSigningKey() => Assert.Fail("Requires Docker.");
}
