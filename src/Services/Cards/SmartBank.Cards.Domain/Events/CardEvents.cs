using SmartBank.BuildingBlocks.Domain;

namespace SmartBank.Cards.Domain.Events;

public sealed record CardIssuedDomainEvent(Guid CardId, Guid AccountId, Guid CustomerId, string LastFour) : DomainEvent;
public sealed record CardFrozenDomainEvent(Guid CardId) : DomainEvent;
public sealed record CardUnfrozenDomainEvent(Guid CardId) : DomainEvent;
public sealed record CardBlockedDomainEvent(Guid CardId) : DomainEvent;
