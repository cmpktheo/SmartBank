using SmartBank.BuildingBlocks.Domain;

namespace SmartBank.Customer.Domain;

public enum AccountType { Current = 1, Savings = 2 }
public enum AccountStatus { Active = 1, Frozen = 2, Closed = 3 }
public enum HoldState { Active = 1, Captured = 2, Released = 3 }

public sealed class Hold : Entity<Guid>
{
    public BuildingBlocks.Domain.ValueObjects.Money Amount { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public HoldState State { get; private set; }
    public Guid TransactionId { get; private set; }

    private Hold() { }

    public static Hold Create(Guid id, Guid transactionId, BuildingBlocks.Domain.ValueObjects.Money amount, DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty) throw new DomainException("HOLD_ID", "Hold id required.");
        return new Hold { Id = id, TransactionId = transactionId, Amount = amount, ExpiresAt = expiresAt, State = HoldState.Active };
    }

    public void Capture() => State = HoldState.Captured;
    public void Release() => State = HoldState.Released;
}
