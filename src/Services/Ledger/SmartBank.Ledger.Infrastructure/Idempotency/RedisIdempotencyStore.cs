using System.Text.Json;
using SmartBank.BuildingBlocks.Infrastructure.Idempotency;
using StackExchange.Redis;

namespace SmartBank.Ledger.Infrastructure.Idempotency;

public sealed class RedisIdempotencyStore : IIdempotencyStore
{
    private readonly IDatabase _db;
    public RedisIdempotencyStore(IConnectionMultiplexer redis) => _db = redis.GetDatabase();

    public async Task<IdempotencyBeginOutcome> TryBeginAsync(string key, string requestHash, TimeSpan ttl, CancellationToken ct = default)
    {
        var hash = await _db.HashGetAllAsync(key);
        if (hash.Length == 0)
        {
            await _db.HashSetAsync(key, [new HashEntry("fingerprint", requestHash), new HashEntry("status", "pending"), new HashEntry("created", DateTimeOffset.UtcNow.ToUnixTimeSeconds())]);
            await _db.KeyExpireAsync(key, ttl);
            return new IdempotencyBeginOutcome(IdempotencyBeginResult.Started, null);
        }
        var dict = hash.ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());
        if (dict.GetValueOrDefault("fingerprint") != requestHash)
            return new IdempotencyBeginOutcome(IdempotencyBeginResult.Conflict, null);
        if (dict.TryGetValue("response", out var resp) && !string.IsNullOrEmpty(resp))
            return new IdempotencyBeginOutcome(IdempotencyBeginResult.Replay, Convert.FromBase64String(resp));
        var created = long.TryParse(dict.GetValueOrDefault("created"), out var c) ? c : 0;
        var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - created;
        if (age < 60) return new IdempotencyBeginOutcome(IdempotencyBeginResult.InProgress, null);
        return new IdempotencyBeginOutcome(IdempotencyBeginResult.Started, null);
    }

    public async Task CompleteAsync(string key, string requestHash, byte[] responseBytes, CancellationToken ct = default)
    {
        await _db.HashSetAsync(key, [new HashEntry("response", Convert.ToBase64String(responseBytes)), new HashEntry("status", "completed")]);
    }

    public async Task CompleteWithErrorAsync(string key, string requestHash, byte[] errorBytes, CancellationToken ct = default)
    {
        await _db.HashSetAsync(key, [new HashEntry("response", Convert.ToBase64String(errorBytes)), new HashEntry("status", "failed")]);
    }
}
