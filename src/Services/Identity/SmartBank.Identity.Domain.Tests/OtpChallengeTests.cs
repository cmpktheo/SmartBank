using FluentAssertions;
using SmartBank.Identity.Domain;

namespace SmartBank.Identity.Domain.Tests;

public sealed class OtpChallengeTests
{
    [Fact]
    public void Generate_ReturnsSixDigits()
    {
        var code = OtpChallenge.GenerateCode();
        code.Should().MatchRegex("^[0-9]{6}$");
    }

    [Fact]
    public void Verify_AcceptsCorrectCode()
    {
        var userId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var challenge = OtpChallenge.Create(userId, "123456", now);
        challenge.Verify("123456", now.AddSeconds(30)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Verify_RejectsWrongCode_AndIncrementsAttempts()
    {
        var userId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var challenge = OtpChallenge.Create(userId, "123456", now);
        var result = challenge.Verify("000000", now.AddSeconds(30));
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IDENTITY_MFA_INVALID");
        challenge.Attempts.Should().Be(1);
    }

    [Fact]
    public void Verify_FifthFailure_Locks()
    {
        var userId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var challenge = OtpChallenge.Create(userId, "123456", now);
        SmartBank.BuildingBlocks.Domain.Result last = SmartBank.BuildingBlocks.Domain.Result.Success();
        for (var i = 0; i < 5; i++)
            last = challenge.Verify("000000", now.AddSeconds(30));
        last.Error.Code.Should().Be("IDENTITY_MFA_LOCKED");
    }
}
