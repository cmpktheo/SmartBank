using SmartBank.BuildingBlocks.Domain;

namespace SmartBank.Identity.Domain.Events;

public sealed record UserLoggedInDomainEvent(Guid UserId, DateTimeOffset At) : DomainEvent;
