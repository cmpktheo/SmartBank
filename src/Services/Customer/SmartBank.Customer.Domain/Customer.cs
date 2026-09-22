using SmartBank.BuildingBlocks.Domain;
using SmartBank.Customer.Domain.Events;

namespace SmartBank.Customer.Domain;

public enum CustomerStatus { Active = 1, Deactivated = 2 }

public sealed class Customer : AggregateRoot<Guid>
{
    public string LegalName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public Address Address { get; private set; } = null!;
    public CustomerStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Customer() { }

    public static Customer Register(Guid id, string legalName, string email, Address address, DateTimeOffset now)
    {
        if (id == Guid.Empty) throw new DomainException("CUSTOMER_ID", "Id must not be empty.");
        legalName = (legalName ?? "").Trim();
        email = (email ?? "").Trim().ToLowerInvariant();
        if (legalName.Length is < 3 or > 120) throw new DomainException("CUSTOMER_NAME", "LegalName must be 3–120 characters.");
        if (!email.Contains('@') || email.Length > 256) throw new DomainException("CUSTOMER_EMAIL", "Invalid email.");
        var c = new Customer { Id = id, LegalName = legalName, Email = email, Address = address, Status = CustomerStatus.Active, CreatedAt = now };
        c.Raise(new CustomerRegisteredDomainEvent(id, c.Email));
        return c;
    }

    public void Deactivate(DateTimeOffset now)
    {
        _ = now;
        Status = CustomerStatus.Deactivated;
    }
}
