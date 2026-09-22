using FluentValidation;
using MediatR;
using SmartBank.BuildingBlocks.Application;
using SmartBank.BuildingBlocks.Application.Metrics;
using SmartBank.BuildingBlocks.Domain;
using SmartBank.Identity.Application.Abstractions;
using SmartBank.Identity.Domain;

namespace SmartBank.Identity.Application.Auth;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResult>;

public sealed record LoginResult(bool MfaRequired, string? ChallengeId, int? ExpiresInSeconds, TokenPair? Tokens);

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public interface ILoginUserLookup
{
    Task<LoginUser?> FindByEmailAsync(string email, CancellationToken ct);
    Task<bool> CheckPasswordAsync(LoginUser user, string password, CancellationToken ct);
    Task<bool> IsLockedOutAsync(LoginUser user, CancellationToken ct);
    Task RecordFailureAsync(LoginUser user, CancellationToken ct);
    Task RecordSuccessAsync(LoginUser user, CancellationToken ct);
}

public sealed record LoginUser(Guid Id, string Email, Guid? CustomerId, bool MfaEnabled, string[] Roles);

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResult>>
{
    private readonly ILoginUserLookup _users;
    private readonly IMfaStore _mfa;
    private readonly IOtpDelivery _otp;
    private readonly ITokenService _tokens;
    private readonly BuildingBlocks.Application.IClock _clock;

    public LoginCommandHandler(ILoginUserLookup users, IMfaStore mfa, IOtpDelivery otp, ITokenService tokens, BuildingBlocks.Application.IClock clock)
    {
        _users = users;
        _mfa = mfa;
        _otp = otp;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task<Result<LoginResult>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(request.Email, ct);
        if (user is null)
        {
            SmartBankMeters.Login("invalid_credentials");
            return Result.Failure<LoginResult>(Error.Unauthorized("IDENTITY_INVALID_CREDENTIALS", "Invalid email or password."));
        }
        if (await _users.IsLockedOutAsync(user, ct))
        {
            SmartBankMeters.Login("locked");
            return Result.Failure<LoginResult>(Error.Unauthorized("IDENTITY_LOCKED", "Account locked. Try again later."));
        }
        if (!await _users.CheckPasswordAsync(user, request.Password, ct))
        {
            await _users.RecordFailureAsync(user, ct);
            SmartBankMeters.Login("invalid_credentials");
            return Result.Failure<LoginResult>(Error.Unauthorized("IDENTITY_INVALID_CREDENTIALS", "Invalid email or password."));
        }
        await _users.RecordSuccessAsync(user, ct);

        if (!user.MfaEnabled)
        {
            var tokens = await _tokens.IssueAsync(user.Id, user.Email, user.CustomerId, user.Roles, ct);
            SmartBankMeters.Login("success_no_mfa");
            return Result.Success(new LoginResult(false, null, null, tokens));
        }

        var challengeId = Guid.CreateVersion7().ToString();
        var code = OtpChallenge.GenerateCode();
        var challenge = OtpChallenge.Create(user.Id, code, _clock.UtcNow);
        var payload = System.Text.Json.JsonSerializer.Serialize(new MfaPayload(user.Id, challenge.CodeHash, challenge.Attempts, challenge.ExpiresAt));
        await _mfa.SetAsync(challengeId, payload, TimeSpan.FromMinutes(5), ct);
        await _otp.SendAsync(user.Email, code, ct);
        SmartBankMeters.Login("mfa_challenged");
        return Result.Success(new LoginResult(true, challengeId, 300, null));
    }

    public sealed record MfaPayload(Guid UserId, string CodeHash, int Attempts, DateTimeOffset ExpiresAt);
}
