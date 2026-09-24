using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.Ledger.Domain;

namespace SmartBank.Ledger.Infrastructure.Persistence;

public static class LedgerSeed
{
    // Must match CustomerSeed test accounts (posted balances below must equal each account's signed line total).
    private static readonly Guid AlexEveryday = Guid.Parse("a1111111-1111-7111-1111-111111111111");
    private static readonly Guid AlexSavings = Guid.Parse("a2222222-2222-7222-2222-222222222222");
    private static readonly Guid JordanMain = Guid.Parse("a3333333-3333-7333-3333-333333333333");

    // Suspense counterparties (not real customer accounts).
    private static readonly Guid EmployerAcme = Guid.Parse("b1111111-1111-7111-1111-111111111111");
    private static readonly Guid Landlord = Guid.Parse("b2222222-2222-7222-2222-222222222222");
    private static readonly Guid Octopus = Guid.Parse("b3333333-3333-7333-3333-333333333333");
    private static readonly Guid Tesco = Guid.Parse("b4444444-4444-7444-4444-444444444444");
    private static readonly Guid Amazon = Guid.Parse("b5555555-5555-7555-5555-555555555555");
    private static readonly Guid Currys = Guid.Parse("b6666666-6666-7666-6666-666666666666");
    private static readonly Guid Sainsburys = Guid.Parse("b7777777-7777-7777-7777-777777777777");
    private static readonly Guid BritishAirways = Guid.Parse("b8888888-8888-7888-8888-888888888888");
    private static readonly Guid Opener = Guid.Parse("b9999999-9999-7999-9999-999999999999");

    private static readonly Currency Gbp = Currency.From("GBP");

    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        var now = DateTimeOffset.UtcNow;

        // Alex Everyday target 25000 = 28000-1200+2000-450-89.99+45-180-725.01-2400
        // Alex Savings target 18000 = 20000-2000
        // Jordan Main target 15000 = 15000-900+2400-1200-300
        var journals = new[]
        {
            New("c1000001-0000-7000-8000-000000000001", Opener, AlexSavings, 20000m, null, "OPENING DEPOSIT", now.AddDays(-90)),
            New("c1000002-0000-7000-8000-000000000002", EmployerAcme, AlexEveryday, 28000m, "GB29NWBK60161331926819", "ACME LTD SALARY", now.AddDays(-80)),
            New("c1000003-0000-7000-8000-000000000003", EmployerAcme, JordanMain, 15000m, "GB29NWBK60161331926819", "ACME LTD SALARY", now.AddDays(-80)),
            New("c1000004-0000-7000-8000-000000000004", AlexEveryday, Landlord, 1200m, "GB29LOYD30962201234567", "MONTHLY RENT", now.AddDays(-75)),
            New("c1000005-0000-7000-8000-000000000005", JordanMain, Landlord, 900m, "GB29LOYD30962201234567", "MONTHLY RENT", now.AddDays(-75)),
            New("c1000006-0000-7000-8000-000000000006", AlexSavings, AlexEveryday, 2000m, null, "Rainy Day → Everyday", now.AddDays(-70)),
            New("c1000007-0000-7000-8000-000000000007", AlexEveryday, Tesco, 450m, null, "TESCO STORES · Card •••• 4242", now.AddDays(-60), JournalType.CardPayment),
            New("c1000008-0000-7000-8000-000000000008", AlexEveryday, Amazon, 89.99m, null, "AMAZON UK · Card •••• 4242", now.AddDays(-40), JournalType.CardPayment),
            New("c1000009-0000-7000-8000-000000000009", Amazon, AlexEveryday, 45m, null, "AMAZON UK REFUND · Card •••• 4242", now.AddDays(-39), JournalType.CardPayment),
            New("c1000010-0000-7000-8000-000000000010", AlexEveryday, Octopus, 180m, "GB29BARC20159634567890", "OCTOPUS ENERGY", now.AddDays(-30)),
            New("c1000011-0000-7000-8000-000000000011", JordanMain, BritishAirways, 1200m, null, "BRITISH AIRWAYS · Card •••• 0002", now.AddDays(-20), JournalType.CardPayment),
            New("c1000012-0000-7000-8000-000000000012", AlexEveryday, Currys, 725.01m, null, "CURRYS · Card •••• 4242", now.AddDays(-12), JournalType.CardPayment),
            New("c1000013-0000-7000-8000-000000000013", JordanMain, Sainsburys, 300m, null, "SAINSBURY'S · Card •••• 0002", now.AddDays(-8), JournalType.CardPayment),
            New("c1000014-0000-7000-8000-000000000014", AlexEveryday, JordanMain, 2400m, null, "Alex Morgan → Jordan Lee", now.AddDays(-5)),
        };

        // Rerun-safe: fixed journal IDs, delete-then-insert (user-created journals are untouched).
        var ids = journals.Select(j => j.Id).ToList();
        db.Lines.RemoveRange(await db.Lines.Where(l => ids.Contains(l.TransactionId)).ToListAsync());
        db.Journals.RemoveRange(await db.Journals.Where(j => ids.Contains(j.Id)).ToListAsync());
        foreach (var j in journals) db.Journals.Add(j);
        await db.SaveChangesAsync();
    }

    private static JournalTransaction New(string id, Guid source, Guid dest, decimal amount, string? counterpartyIban, string narrative, DateTimeOffset bookedAt, JournalType type = JournalType.Transfer)
    {
        var r = JournalTransaction.Transfer(Guid.Parse(id), source, dest, Money.Of(amount, Gbp), counterpartyIban, narrative, null, bookedAt, ReferenceGenerator.Generate(bookedAt, Guid.Parse(id)), type);
        if (r.IsFailure) throw new InvalidOperationException($"Ledger seed invalid: {r.Error.Code} {r.Error.Message}");
        return r.Value!;
    }
}
