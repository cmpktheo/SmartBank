using FluentAssertions;
using NSubstitute;
using SmartBank.BuildingBlocks.Application;
using SmartBank.Identity.Application.Abstractions;
using SmartBank.Identity.Application.Auth;

namespace SmartBank.Identity.Application.Tests;

public sealed class LoginHandlerTests
{
    private readonly ILoginUserLookup _users = Substitute.For<ILoginUserLookup>();
    private readonly IMfaStore _mfa = Substitute.For<IMfaStore>();
    private readonly IOtpDelivery _otp = Substitute.For<IOtpDelivery>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private LoginCommandHandler Handler => new(_users, _mfa, _otp, _tokens, _clock);

    [Fact]
    public async Task UnknownEmail_InvalidCredentials_DoesNotCallOtp()
    {
        _users.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((LoginUser?)null);
        var result = await Handler.Handle(new LoginCommand("nobody@x.test", "123456"), CancellationToken.None);
        result.Error.Code.Should().Be("IDENTITY_INVALID_CREDENTIALS");
        await _otp.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BadPassword_InvalidCredentials()
    {
        var user = new LoginUser(Guid.CreateVersion7(), "a@x.test", null, true, ["Customer"]);
        _users.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _users.IsLockedOutAsync(user, Arg.Any<CancellationToken>()).Returns(false);
        _users.CheckPasswordAsync(user, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var result = await Handler.Handle(new LoginCommand("a@x.test", "wrong"), CancellationToken.None);
        result.Error.Code.Should().Be("IDENTITY_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task GoodPassword_MfaOn_ChallengeStored_TokensNotIssued()
    {
        var user = new LoginUser(Guid.CreateVersion7(), "a@x.test", null, true, ["Customer"]);
        _users.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _users.IsLockedOutAsync(user, Arg.Any<CancellationToken>()).Returns(false);
        _users.CheckPasswordAsync(user, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var result = await Handler.Handle(new LoginCommand("a@x.test", "123456"), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value!.MfaRequired.Should().BeTrue();
        await _mfa.Received().SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await _tokens.DidNotReceive().IssueAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GoodPassword_MfaOff_TokensIssued()
    {
        var user = new LoginUser(Guid.CreateVersion7(), "a@x.test", null, false, ["Customer"]);
        _users.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _users.IsLockedOutAsync(user, Arg.Any<CancellationToken>()).Returns(false);
        _users.CheckPasswordAsync(user, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _tokens.IssueAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(new TokenPair("a", "r", 900, null));
        var result = await Handler.Handle(new LoginCommand("a@x.test", "123456"), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value!.MfaRequired.Should().BeFalse();
    }
}
