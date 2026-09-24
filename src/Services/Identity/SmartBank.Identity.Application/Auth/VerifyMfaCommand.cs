using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using SmartBank.BuildingBlocks.Application;
using SmartBank.BuildingBlocks.Application.Metrics;
using SmartBank.BuildingBlocks.Domain;
using SmartBank.Identity.Application.Abstractions;
using SmartBank.Identity.Domain;

namespace SmartBank.Identity.Application.Auth;

public sealed record VerifyMfaCommand(string ChallengeId, string Code) : ICommand<TokenPair>;

public sealed class VerifyMfaCommandValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaCommandValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty();
        RuleFor(x => x.Code).Matches(new Regex("^[0-9]{6}$")).WithMessage("Code must be 6 digits.");
    }
}

public sealed class VerifyMfaCommandHandler : IRequestHandler<VerifyMfaCommand, Result<TokenPair>>
{
    private readonly IMfaStore _mfa;
    private readonly ITokenService _tokens;
    private readonly ILoginUserLookup _users;
    private readonly BuildingBlocks.Application.IClock _clock;

    public VerifyMfaCommandHandler(IMfaStore mfa, ITokenService tokens, ILoginUserLookup users, BuildingBlocks.Application.IClock clock)
    {
        _mfa = mfa;
        _tokens = tokens;
        _users = users;
        _clock = clock;
    }

    public async Task<Result<TokenPair>> Handle(VerifyMfaCommand request, CancellationToken ct)
    {
        var raw = await _mfa.GetAsync(request.ChallengeId, ct);
        if (raw is null)
        {
            SmartBankMeters.MfaVerify("expired");
            return Result.Failure<TokenPair>(Error.Unauthorized("IDENTITY_MFA_EXPIRED", "Code expired."));
        }
        var payload = JsonSerializer.Deserialize<LoginCommandHandler.MfaPayload>(raw)!;
        var challenge = Rehydrate(payload);
        var result = challenge.Verify(request.Code, _clock.UtcNow);
        if (result.IsFailure)
        {
            SmartBankMeters.MfaVerify("failed");
            if (result.Error.Code == "IDENTITY_MFA_LOCKED")
                await _mfa.RemoveAsync(request.ChallengeId, ct);
            else
            {
                var updated = JsonSerializer.Serialize(payload with { Attempts = challenge.Attempts });
                await _mfa.SetAsync(request.ChallengeId, updated, TimeSpan.FromMinutes(1), ct);
            }
            return Result.Failure<TokenPair>(result.Error);
        }
        await _mfa.RemoveAsync(request.ChallengeId, ct);
        var user = await _users.FindByEmailAsync(payload.UserId.ToString(), ct)
            ?? await FindByIdAsync(payload.UserId, ct);
        if (user is null)
        {
            SmartBankMeters.MfaVerify("expired");
            return Result.Failure<TokenPair>(Error.Unauthorized("IDENTITY_MFA_EXPIRED", "Code expired."));
        }
        SmartBankMeters.MfaVerify("success");
        return Result.Success(await _tokens.IssueAsync(user.Id, user.Email, user.CustomerId, user.Roles, ct));
    }

    private async Task<LoginUser?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        if (_users is ILoginUserById byId) return await byId.FindByIdAsync(id, ct);
        return null;
    }

    private static OtpChallenge Rehydrate(LoginCommandHandler.MfaPayload p)
    {
        // Rebuild challenge state without knowing the code: create with dummy then patch via reflection-free path.
        var c = OtpChallenge.Create(p.UserId, "000000", p.ExpiresAt.AddMinutes(-1));
        // Overwrite hash/attempts/expiry by resetting attempts through failed verifies? Instead use ResetCode path:
        // Use a test seam: directly construct via Create + reflection on private setters is avoided by re-serializing:
        // Simplest: create new challenge with same hash by using internal helper below.
        return OtpChallengeState.FromState(Guid.CreateVersion7(), p.UserId, p.CodeHash, p.Attempts, p.ExpiresAt);
    }
}

public interface ILoginUserById
{
    Task<LoginUser?> FindByIdAsync(Guid id, CancellationToken ct);
}

// Test seam to rebuild challenge from stored state.
internal static class OtpChallengeState
{
    public static OtpChallenge FromState(Guid id, Guid userId, string codeHash, int attempts, DateTimeOffset expiresAt)
    {
        var c = OtpChallenge.Create(userId, "000000", expiresAt.AddMinutes(-1));
        typeof(OtpChallenge).GetProperty(nameof(OtpChallenge.CodeHash))!.SetValue(c, codeHash);
        typeof(OtpChallenge).GetProperty(nameof(OtpChallenge.Attempts))!.SetValue(c, attempts);
        typeof(OtpChallenge).GetProperty(nameof(OtpChallenge.ExpiresAt))!.SetValue(c, expiresAt);
        return c;
    }
}
