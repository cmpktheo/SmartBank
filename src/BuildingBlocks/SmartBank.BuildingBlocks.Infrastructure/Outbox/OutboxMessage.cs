namespace SmartBank.BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class InboxMessage
{
    public Guid Id { get; set; }
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}

public interface IOutboxWriter
{
    void Add(OutboxMessage message);
}
