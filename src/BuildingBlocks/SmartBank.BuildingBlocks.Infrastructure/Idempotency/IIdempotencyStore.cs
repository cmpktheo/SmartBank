namespace SmartBank.BuildingBlocks.Infrastructure.Idempotency;

public enum IdempotencyBeginResult { Started, Replay, Conflict, InProgress }

public sealed record IdempotencyBeginOutcome(IdempotencyBeginResult Result, byte[]? ResponseBytes);

public interface IIdempotencyStore
{
    Task<IdempotencyBeginOutcome> TryBeginAsync(string key, string requestHash, TimeSpan ttl, CancellationToken ct = default);
    Task CompleteAsync(string key, string requestHash, byte[] responseBytes, CancellationToken ct = default);
    Task CompleteWithErrorAsync(string key, string requestHash, byte[] errorBytes, CancellationToken ct = default);
}
