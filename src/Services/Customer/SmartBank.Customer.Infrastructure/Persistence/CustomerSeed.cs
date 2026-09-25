using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.Customer.Domain;
using SmartBank.Customer.Infrastructure.Persistence;

namespace SmartBank.Customer.Infrastructure.Persistence;

public static class CustomerSeed
{
    // Deterministic test account IDs so other services (e.g. Cards seed) can link cards.
    public static readonly Guid AlexEverydayAccountId = Guid.Parse("a1111111-1111-7111-1111-111111111111");
    public static readonly Guid AlexSavingsAccountId = Guid.Parse("a2222222-2222-7222-2222-222222222222");
    public static readonly Guid JordanMainAccountId = Guid.Parse("a3333333-3333-7333-3333-333333333333");

    public static readonly Guid AlexCustomerId = Guid.Parse("11111111-1111-7111-1111-111111111111");
    public static readonly Guid JordanCustomerId = Guid.Parse("22222222-2222-7222-2222-222222222222");

    // Fixed account numbers -> stable IBANs (IbanFactory with sort code 601613):
    //   1000000004 -> GB42 0000 0004 6016 1310 0000 0004 (Alex Everyday)
    //   1000000005 -> GB87 0000 0005 6016 1310 0000 0005 (Alex Rainy Day)
    //   1000000006 -> GB35 0000 0006 6016 1310 0000 0006 (Jordan Main)
    // Never use nextval() for seed accounts: the sequence is never reset and every
    // account opened in the app consumes a number, so nextval() would shift these
    // IBANs on every restart.
    public const long AlexEverydayAccountNumber = 1000000004;
    public const long AlexSavingsAccountNumber = 1000000005;
    public const long JordanMainAccountNumber = 1000000006;

    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE SEQUENCE IF NOT EXISTS account_number_seq AS BIGINT START WITH 1000000001");
        var alexId = AlexCustomerId;
        var jordanId = JordanCustomerId;
        if (!await db.Customers.AnyAsync(c => c.Id == alexId))
        {
            db.Customers.Add(Domain.Customer.Register(alexId, "Alex Morgan", "alex.morgan@smartbank.test",
                Address.Create("1 Bishopsgate", null, "London", "EC2M 3DQ", "GB"), DateTimeOffset.UtcNow));
            db.Customers.Add(Domain.Customer.Register(jordanId, "Jordan Lee", "jordan.lee@smartbank.test",
                Address.Create("10 Canary Wharf", null, "London", "E14 5AB", "GB"), DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        if (!await db.BankAccounts.AnyAsync())
        {
            var factory = new IbanFactory("601613");
            await AddAccount(db, AlexEverydayAccountId, alexId, "Everyday", AccountType.Current, 25000m, AlexEverydayAccountNumber, factory);
            await AddAccount(db, AlexSavingsAccountId, alexId, "Rainy Day", AccountType.Savings, 18000m, AlexSavingsAccountNumber, factory);
            await AddAccount(db, JordanMainAccountId, jordanId, "Main", AccountType.Current, 15000m, JordanMainAccountNumber, factory);
        }
        else
        {
            var factory = new IbanFactory("601613");
            var seedIds = new[] { AlexEverydayAccountId, AlexSavingsAccountId, JordanMainAccountId };
            var seedIdsNullable = seedIds.Select(id => (Guid?)id).ToArray();
            await db.Holds.Where(h => seedIdsNullable.Contains(EF.Property<Guid?>(h, "BankAccountId"))).ExecuteDeleteAsync();
            foreach (var accountId in seedIds)
            {
                var existing = await db.BankAccounts.FirstOrDefaultAsync(a => a.Id == accountId);
                if (existing != null) db.BankAccounts.Remove(existing);
            }
            await db.SaveChangesAsync();
            await AddAccount(db, AlexEverydayAccountId, alexId, "Everyday", AccountType.Current, 25000m, AlexEverydayAccountNumber, factory);
            await AddAccount(db, AlexSavingsAccountId, alexId, "Rainy Day", AccountType.Savings, 18000m, AlexSavingsAccountNumber, factory);
            await AddAccount(db, JordanMainAccountId, jordanId, "Main", AccountType.Current, 15000m, JordanMainAccountNumber, factory);
        }
        // Keep user-opened accounts collision-free: never rewind the sequence, only
        // fast-forward past the reserved seed numbers.
        // NOTE: one-time `docker compose down -v` + reseed is required if an existing
        // dev DB already used 1000000004/5/6 for user-opened accounts (unique IBAN).
        await db.Database.ExecuteSqlRawAsync(
            "SELECT setval('account_number_seq', GREATEST((SELECT last_value FROM account_number_seq), 1000000006::BIGINT), true)");
    }

    private static async Task AddAccount(CustomerDbContext db, Guid accountId, Guid customerId, string alias, AccountType type, decimal posted, long accountNumber, IbanFactory factory)
    {
        var acc = BankAccount.Open(accountId, customerId, factory.Create(accountNumber), alias, type, Currency.GBP, DateTimeOffset.UtcNow);
        acc.SetPostedForSeed(Money.Of(posted, Currency.GBP));
        db.BankAccounts.Add(acc);
        await db.SaveChangesAsync();
    }
}
