using SmartBank.Customer.Domain;

namespace SmartBank.Customer.Application.Abstractions;

public interface IBankAccountRepository
{
    Task<BankAccount?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<BankAccount?> GetByIbanAsync(string iban, CancellationToken ct);
    Task<List<BankAccount>> ListByCustomerAsync(Guid customerId, CancellationToken ct);
    Task AddAsync(BankAccount account, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task<long> NextAccountNumberAsync(CancellationToken ct);
}

public interface ICustomerRepository
{
    Task<Domain.Customer?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Domain.Customer customer, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IOutboxWriterLocal
{
    void Add(string type, string payload, Guid correlationId);
}
