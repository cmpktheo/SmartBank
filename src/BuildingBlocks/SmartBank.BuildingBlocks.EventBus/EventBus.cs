namespace SmartBank.BuildingBlocks.EventBus;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
    Guid CorrelationId { get; }
    string EventType { get; }
}

public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public Guid CorrelationId { get; init; } = Guid.CreateVersion7();
    public abstract string EventType { get; }
}

public interface IEventPublisher
{
    Task PublishAsync(string type, string payload, Guid correlationId, CancellationToken ct = default);
}
