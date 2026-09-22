using Microsoft.Extensions.Logging;
using SmartBank.Identity.Application.Abstractions;
using StackExchange.Redis;

namespace SmartBank.Identity.Infrastructure.Auth;

public sealed class RedisTokenBlacklist : ITokenBlacklist
{
    private readonly IDatabase _db;
    public RedisTokenBlacklist(IConnectionMultiplexer redis) => _db = redis.GetDatabase();

    public Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct)
        => _db.StringSetAsync($"sb:id:blacklist:{jti}", "1", ttl).ContinueWith(_ => { }, ct);

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken ct)
        => await _db.KeyExistsAsync($"sb:id:blacklist:{jti}");
}

public sealed class LogOtpDelivery : IOtpDelivery
{
    private readonly ILogger<LogOtpDelivery> _logger;
    public LogOtpDelivery(ILogger<LogOtpDelivery> logger) => _logger = logger;
    public Task SendAsync(string email, string code, CancellationToken ct)
    {
        _logger.LogInformation("OTP for {Email} is {Code}", email, code);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryOtpSink : IOtpDelivery
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _codes = new();
    public string? LastCode => _codes.Values.LastOrDefault();
    public Task SendAsync(string email, string code, CancellationToken ct)
    {
        _codes[email] = code;
        return Task.CompletedTask;
    }
    public string? For(string email) => _codes.TryGetValue(email, out var c) ? c : null;
}

public sealed class RedisMfaStore : IMfaStore
{
    private readonly IDatabase _db;
    public RedisMfaStore(IConnectionMultiplexer redis) => _db = redis.GetDatabase();

    public Task SetAsync(string challengeId, string payload, TimeSpan ttl, CancellationToken ct)
        => _db.StringSetAsync($"sb:id:mfa:{challengeId}", payload, ttl).ContinueWith(_ => { }, ct);

    public async Task<string?> GetAsync(string challengeId, CancellationToken ct)
        => await _db.StringGetAsync($"sb:id:mfa:{challengeId}");

    public Task RemoveAsync(string challengeId, CancellationToken ct)
        => _db.KeyDeleteAsync($"sb:id:mfa:{challengeId}").ContinueWith(_ => { }, ct);

    public async Task<long> IncrementResendAsync(string challengeId, CancellationToken ct)
        => (long)await _db.StringIncrementAsync($"sb:id:mfa-resend:{challengeId}");
}
