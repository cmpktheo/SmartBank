namespace SmartBank.Identity.Application.Abstractions;

public interface IOtpDelivery
{
    Task SendAsync(string email, string code, CancellationToken ct);
}

public interface ITokenBlacklist
{
    Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct);
    Task<bool> IsRevokedAsync(string jti, CancellationToken ct);
}

public sealed record TokenPair(string AccessToken, string RefreshToken, int ExpiresIn, Guid? CustomerId);

public interface ITokenService
{
    Task<TokenPair> IssueAsync(Guid userId, string email, Guid? customerId, string[] roles, CancellationToken ct);
    Task<TokenPair> RefreshAsync(string refreshToken, CancellationToken ct);
    Task RevokeRefreshAsync(string refreshToken, CancellationToken ct);
}

public interface IMfaStore
{
    Task SetAsync(string challengeId, string payload, TimeSpan ttl, CancellationToken ct);
    Task<string?> GetAsync(string challengeId, CancellationToken ct);
    Task RemoveAsync(string challengeId, CancellationToken ct);
    Task<long> IncrementResendAsync(string challengeId, CancellationToken ct);
}
