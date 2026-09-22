using Grpc.Core;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.Contracts.Grpc;
using SmartBank.Customer.Domain;
using SmartBank.Customer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SmartBank.Customer.Api.Grpc;

public sealed class AccountDirectoryService : AccountDirectory.AccountDirectoryBase
{
    private readonly CustomerDbContext _db;
    private readonly BuildingBlocks.Application.IClock _clock;

    public AccountDirectoryService(CustomerDbContext db, BuildingBlocks.Application.IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public override async Task<AccountStatusReply> GetAccountStatus(GetAccountStatusRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var id))
            return new AccountStatusReply { Found = false };
        var a = await _db.BankAccounts.FirstOrDefaultAsync(x => x.Id == id, context.CancellationToken);
        if (a is null) return new AccountStatusReply { Found = false };
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == a.CustomerId, context.CancellationToken);
        return new AccountStatusReply
        {
            Found = true,
            AccountId = a.Id.ToString(),
            CustomerId = a.CustomerId.ToString(),
            Iban = a.Iban.Value,
            Currency = a.Currency.Code,
            Status = a.Status.ToString(),
            AvailableBalance = a.AvailableBalance.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            PostedBalance = a.PostedBalance.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            OverdraftLimit = a.OverdraftLimit.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            CustomerEmail = customer?.Email ?? "",
            Alias = a.Alias
        };
    }

    public override async Task<LookupByIbanReply> LookupByIban(LookupByIbanRequest request, ServerCallContext context)
    {
        var compact = new string(request.Iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var a = await _db.BankAccounts.FirstOrDefaultAsync(x => x.Iban.Value == compact, context.CancellationToken);
        if (a is null) return new LookupByIbanReply { Found = false };
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == a.CustomerId, context.CancellationToken);
        return new LookupByIbanReply
        {
            Found = true,
            AccountId = a.Id.ToString(),
            CustomerId = a.CustomerId.ToString(),
            Status = a.Status.ToString(),
            Currency = a.Currency.Code,
            HolderNameMasked = MaskHolder(customer?.LegalName ?? "Account Holder"),
            CustomerEmail = customer?.Email ?? "",
            Iban = a.Iban.Value
        };
    }

    public override async Task<ReserveFundsReply> ReserveFunds(ReserveFundsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId) || !Guid.TryParse(request.HoldId, out var holdId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ids"));
        // Include(x => x.Holds) is required so Recalc() accounts for all active
        // holds. NOTE: EF Core's collection fixup then mis-tracks the newly added
        // Hold as Modified (emitting UPDATE instead of INSERT), so we force the
        // Added state explicitly below after a successful PlaceHold.
        var a = await _db.BankAccounts.Include(x => x.Holds).FirstOrDefaultAsync(x => x.Id == accountId, context.CancellationToken);
        if (a is null) return new ReserveFundsReply { Ok = false, ErrorCode = "ACCOUNT_NOT_FOUND", ErrorMessage = "Not found." };
        // Idempotent replay: hold already persisted — return success without re-placing.
        var alreadyHeld = await _db.Holds.AsNoTracking().AnyAsync(h => h.Id == holdId, context.CancellationToken);
        if (alreadyHeld)
            return new ReserveFundsReply
            {
                Ok = true,
                AvailableBalance = a.AvailableBalance.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            };
        var amount = Money.Of(decimal.Parse(request.Amount, System.Globalization.CultureInfo.InvariantCulture), Currency.From(request.Currency));
        var txId = Guid.TryParse(request.TransactionId, out var t) ? t : holdId;
        var hold = Hold.Create(holdId, txId, amount, _clock.UtcNow.AddSeconds(request.TtlSeconds <= 0 ? 30 : request.TtlSeconds));
        var result = a.PlaceHold(hold, _clock.UtcNow);
        if (result.IsFailure)
            return new ReserveFundsReply { Ok = false, ErrorCode = result.Error.Code, ErrorMessage = result.Error.Message };
        // The Include collection snapshot mis-tracks the new hold (Modified
        // instead of Added, owned Money omitted) -> UPDATE of a missing row
        // or INSERT with NULL amount. Detach whatever DetectChanges inferred,
        // then track the hold fresh so both INSERT with full values.
        try
        {
            _db.Entry(hold).State = EntityState.Detached;
            _db.Holds.Add(hold);
            await _db.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ReserveFundsReply { Ok = false, ErrorCode = "ACCOUNT_CONCURRENCY", ErrorMessage = "Concurrency conflict." };
        }
        return new ReserveFundsReply
        {
            Ok = true,
            AvailableBalance = a.AvailableBalance.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    public override async Task<ReleaseFundsReply> ReleaseFunds(ReleaseFundsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId) || !Guid.TryParse(request.HoldId, out var holdId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ids"));
        var a = await _db.BankAccounts.Include(x => x.Holds).FirstOrDefaultAsync(x => x.Id == accountId, context.CancellationToken);
        if (a is null) return new ReleaseFundsReply { Ok = true };
        a.ReleaseHold(holdId);
        await _db.SaveChangesAsync(context.CancellationToken);
        return new ReleaseFundsReply { Ok = true };
    }

    public override async Task<CommitFundsReply> CommitFunds(CommitFundsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId) || !Guid.TryParse(request.HoldId, out var holdId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ids"));
        var a = await _db.BankAccounts.Include(x => x.Holds).FirstOrDefaultAsync(x => x.Id == accountId, context.CancellationToken);
        if (a is null) return new CommitFundsReply { Ok = false, ErrorCode = "ACCOUNT_NOT_FOUND", ErrorMessage = "Not found." };
        var r = a.CaptureHold(holdId);
        if (r.IsFailure) return new CommitFundsReply { Ok = false, ErrorCode = r.Error.Code, ErrorMessage = r.Error.Message };
        await _db.SaveChangesAsync(context.CancellationToken);
        return new CommitFundsReply { Ok = true };
    }

    public override async Task<CreditFundsReply> CreditFunds(CreditFundsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ids"));
        var a = await _db.BankAccounts.Include(x => x.Holds).FirstOrDefaultAsync(x => x.Id == accountId, context.CancellationToken);
        if (a is null) return new CreditFundsReply { Ok = false, ErrorCode = "ACCOUNT_NOT_FOUND", ErrorMessage = "Not found." };
        var amount = Money.Of(decimal.Parse(request.Amount, System.Globalization.CultureInfo.InvariantCulture), Currency.From(request.Currency));
        var r = a.CreditPosted(amount);
        if (r.IsFailure) return new CreditFundsReply { Ok = false, ErrorCode = r.Error.Code, ErrorMessage = r.Error.Message };
        await _db.SaveChangesAsync(context.CancellationToken);
        return new CreditFundsReply { Ok = true };
    }

    public static string MaskHolder(string legalName)
    {
        var parts = legalName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(p => p[0] + new string('*', Math.Max(3, p.Length - 1))));
    }
}
