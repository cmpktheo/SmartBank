using Microsoft.EntityFrameworkCore;
using SmartBank.Customer.Application.Abstractions;
using SmartBank.Customer.Domain;
using SmartBank.Customer.Infrastructure.Persistence;

namespace SmartBank.Customer.Infrastructure.Repositories;

public sealed class EfBankAccountRepository : IBankAccountRepository
{
    private readonly CustomerDbContext _db;
    public EfBankAccountRepository(CustomerDbContext db) => _db = db;

    public async Task<BankAccount?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.BankAccounts.Include(a => a.Holds).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<BankAccount?> GetByIbanAsync(string iban, CancellationToken ct)
        => await _db.BankAccounts.Include(a => a.Holds).FirstOrDefaultAsync(a => a.Iban.Value == iban, ct);

    public async Task<List<BankAccount>> ListByCustomerAsync(Guid customerId, CancellationToken ct)
        => await _db.BankAccounts.Include(a => a.Holds).Where(a => a.CustomerId == customerId).ToListAsync(ct);

    public Task AddAsync(BankAccount account, CancellationToken ct) => _db.BankAccounts.AddAsync(account, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public async Task<long> NextAccountNumberAsync(CancellationToken ct)
    {
        var conn = _db.Database.GetDbConnection();
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT nextval('account_number_seq')";
        var result = await cmd.ExecuteScalarAsync(ct);
        await conn.CloseAsync();
        return Convert.ToInt64(result);
    }
}

public sealed class EfCustomerRepository : ICustomerRepository
{
    private readonly CustomerDbContext _db;
    public EfCustomerRepository(CustomerDbContext db) => _db = db;
    public async Task<Domain.Customer?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
    public Task AddAsync(Domain.Customer customer, CancellationToken ct) => _db.Customers.AddAsync(customer, ct).AsTask();
    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
