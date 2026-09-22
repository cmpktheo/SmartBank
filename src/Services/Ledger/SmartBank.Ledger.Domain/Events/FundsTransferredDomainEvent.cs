using SmartBank.BuildingBlocks.Domain;
using SmartBank.BuildingBlocks.Domain.ValueObjects;

namespace SmartBank.Ledger.Domain.Events;

public sealed record FundsTransferredDomainEvent(Guid TransactionId, Guid SourceAccountId, Guid DestinationAccountId, Money Amount, string Reference, DateTimeOffset BookedAt) : DomainEvent;
