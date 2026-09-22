using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.Cards.Domain;

namespace SmartBank.Cards.Infrastructure.Persistence;

public static class CardsSeed
{
    // Must match CustomerSeed test accounts (UK / GBP / balance > 10,000).
    private static readonly Guid AlexCustomerId = Guid.Parse("11111111-1111-7111-1111-111111111111");
    private static readonly Guid JordanCustomerId = Guid.Parse("22222222-2222-7222-2222-222222222222");

    private static readonly Guid AlexEverydayAccountId = Guid.Parse("a1111111-1111-7111-1111-111111111111");
    private static readonly Guid AlexSavingsAccountId = Guid.Parse("a2222222-2222-7222-2222-222222222222");
    private static readonly Guid JordanMainAccountId = Guid.Parse("a3333333-3333-7333-3333-333333333333");

    // Visa test PANs (all start with 4, 16 digits, Luhn-valid).
    private const string VisaDebit1 = "4242424242424242";
    private const string VisaCredit1 = "4012888888881881";
    private const string VisaDebit2 = "4111111111111111";
    private const string VisaCredit2 = "4000056655665556";
    private const string VisaDebit3 = "4000000000000002";
    private const string VisaCredit3 = "4000002500003155";

    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<IPanProtector>();
        var now = DateTimeOffset.UtcNow;

        await EnsurePair(db, protector, AlexCustomerId, AlexEverydayAccountId, VisaDebit1, VisaCredit1, now);
        await EnsurePair(db, protector, JordanCustomerId, JordanMainAccountId, VisaDebit3, VisaCredit3, now);
    }

    private static async Task EnsurePair(
        CardsDbContext db, IPanProtector protector,
        Guid customerId, Guid accountId,
        string debitPan, string creditPan, DateTimeOffset now)
    {
        if (!await db.Cards.AnyAsync(c => c.AccountId == accountId && c.Type == CardType.Debit))
        {
            db.Cards.Add(Card.IssueVirtual(
                Guid.CreateVersion7(), customerId, accountId,
                debitPan, "123", 12, 2029,
                Money.Of(5000m, Currency.GBP), Money.Of(1000m, Currency.GBP),
                protector, now, CardType.Debit, CardBrand.Visa));
        }

        if (!await db.Cards.AnyAsync(c => c.AccountId == accountId && c.Type == CardType.Credit))
        {
            db.Cards.Add(Card.IssueVirtual(
                Guid.CreateVersion7(), customerId, accountId,
                creditPan, "321", 9, 2030,
                Money.Of(10000m, Currency.GBP), Money.Of(2000m, Currency.GBP),
                protector, now, CardType.Credit, CardBrand.Visa));
        }

        await db.SaveChangesAsync();
    }
}
