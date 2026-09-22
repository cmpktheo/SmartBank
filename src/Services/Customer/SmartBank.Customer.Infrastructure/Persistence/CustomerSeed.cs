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
            // UK IBANs (GB, sort code 601613 / NWBK) + GBP + balances > 10,000.
            var factory = new IbanFactory("NWBK", "601613");
            await AddAccount(db, AlexEverydayAccountId, alexId, "Everyday", AccountType.Current, 25000m, factory);
            await AddAccount(db, AlexSavingsAccountId, alexId, "Rainy Day", AccountType.Savings, 18000m, factory);
            await AddAccount(db, JordanMainAccountId, jordanId, "Main", AccountType.Current, 15000m, factory);
        }
    }

    private static async Task AddAccount(CustomerDbContext db, Guid accountId, Guid customerId, string alias, AccountType type, decimal posted, IbanFactory factory)
    {
        var number = Convert.ToInt64(await db.Database.SqlQueryRaw<long>("SELECT nextval('account_number_seq') AS \"Value\"").FirstAsync());
        var acc = BankAccount.Open(accountId, customerId, factory.Create(number), alias, type, Currency.GBP, DateTimeOffset.UtcNow);
        acc.SetPostedForSeed(Money.Of(posted, Currency.GBP));
        db.BankAccounts.Add(acc);
        await db.SaveChangesAsync();
    }
}
