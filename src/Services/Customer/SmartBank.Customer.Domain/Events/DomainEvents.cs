using SmartBank.BuildingBlocks.Domain;

namespace SmartBank.Customer.Domain.Events;

public sealed record AccountOpenedDomainEvent(Guid AccountId, Guid CustomerId, string Iban, string Currency, string AccountType, DateTimeOffset OpenedAt) : DomainEvent;
public sealed record AccountFrozenDomainEvent(Guid AccountId) : DomainEvent;
public sealed record CustomerRegisteredDomainEvent(Guid CustomerId, string Email) : DomainEvent;
