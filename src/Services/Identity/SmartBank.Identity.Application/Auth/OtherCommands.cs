using System.Text.Json;
using MediatR;
using SmartBank.BuildingBlocks.Application;
using SmartBank.BuildingBlocks.Application.Metrics;
using SmartBank.BuildingBlocks.Domain;
using SmartBank.Identity.Application.Abstractions;
using SmartBank.Identity.Domain;

namespace SmartBank.Identity.Application.Auth;

public sealed record ResendMfaCommand(string ChallengeId) : ICommand<ResendMfaResult>;
public sealed record ResendMfaResult(int ExpiresInSeconds);

public sealed class ResendMfaCommandHandler : IRequestHandler<ResendMfaCommand, Result<ResendMfaResult>>
{
    private readonly IMfaStore _mfa;
    private readonly IOtpDelivery _otp;
    private readonly ILoginUserById _users;
    private readonly BuildingBlocks.Application.IClock _clock;

    public ResendMfaCommandHandler(IMfaStore mfa, IOtpDelivery otp, ILoginUserById users, BuildingBlocks.Application.IClock clock)
    {
        _mfa = mfa;
        _otp = otp;
        _users = users;
        _clock = clock;
    }

    public async Task<Result<ResendMfaResult>> Handle(ResendMfaCommand request, CancellationToken ct)
    {
        var raw = await _mfa.GetAsync(request.ChallengeId, ct);
        if (raw is null)
        {
            SmartBankMeters.MfaResend("expired");
            return Result.Failure<ResendMfaResult>(Error.Unauthorized("IDENTITY_MFA_EXPIRED", "Code expired."));
        }
        var count = await _mfa.IncrementResendAsync(request.ChallengeId, ct);
        if (count > 3)
        {
            SmartBankMeters.MfaResend("rate_limited");
            return Result.Failure<ResendMfaResult>(Error.Unauthorized("IDENTITY_MFA_RESEND_LIMIT", "Resend limit exceeded."));
        }
        var payload = JsonSerializer.Deserialize<LoginCommandHandler.MfaPayload>(raw)!;
        var user = await _users.FindByIdAsync(payload.UserId, ct);
        if (user is null)
            return Result.Failure<ResendMfaResult>(Error.Unauthorized("IDENTITY_MFA_EXPIRED", "Code expired."));
        var code = OtpChallenge.GenerateCode();
        var updated = new LoginCommandHandler.MfaPayload(payload.UserId, OtpChallenge.Hash(code, payload.UserId), 0, _clock.UtcNow.AddMinutes(5));
        await _mfa.SetAsync(request.ChallengeId, JsonSerializer.Serialize(updated), TimeSpan.FromMinutes(5), ct);
        await _otp.SendAsync(user.Email, code, ct);
        SmartBankMeters.MfaResend("success");
        return Result.Success(new ResendMfaResult(300));
    }
}

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<TokenPair>;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<TokenPair>>
{
    private readonly ITokenService _tokens;
    public RefreshTokenCommandHandler(ITokenService tokens) => _tokens = tokens;
    public async Task<Result<TokenPair>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        try
        {
            var tokens = await _tokens.RefreshAsync(request.RefreshToken, ct);
            SmartBankMeters.TokenRefresh("success");
            return Result.Success(tokens);
        }
        catch (UnauthorizedAccessException ex)
        {
            SmartBankMeters.TokenRefresh("failed");
            return Result.Failure<TokenPair>(Error.Unauthorized("IDENTITY_INVALID_CREDENTIALS", ex.Message));
        }
    }
}

public sealed record LogoutCommand(string Jti, TimeSpan Ttl, string? RefreshToken) : ICommand;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly ITokenBlacklist _blacklist;
    private readonly ITokenService _tokens;
    public LogoutCommandHandler(ITokenBlacklist blacklist, ITokenService tokens)
    {
        _blacklist = blacklist;
        _tokens = tokens;
    }
    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        await _blacklist.RevokeAsync(request.Jti, request.Ttl, ct);
        if (request.RefreshToken is not null)
        {
            try { await _tokens.RevokeRefreshAsync(request.RefreshToken, ct); } catch { }
        }
        SmartBankMeters.Logout();
        return Result.Success();
    }
}

public sealed record GetMeQuery(Guid UserId) : IQuery<MeDto>;
public sealed record MeDto(Guid UserId, string Email, Guid? CustomerId, string[] Roles);

public sealed class GetMeQueryHandler : IRequestHandler<GetMeQuery, Result<MeDto>>
{
    private readonly ILoginUserById _users;
    public GetMeQueryHandler(ILoginUserById users) => _users = users;
    public async Task<Result<MeDto>> Handle(GetMeQuery request, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(request.UserId, ct);
        if (user is null) return Result.Failure<MeDto>(Error.Unauthorized("IDENTITY_INVALID_CREDENTIALS", "Unknown user."));
        return Result.Success(new MeDto(user.Id, user.Email, user.CustomerId, user.Roles));
    }
}
