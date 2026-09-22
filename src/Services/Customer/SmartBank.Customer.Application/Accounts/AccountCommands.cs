using System.Text.Json;
using FluentValidation;
using MediatR;
using SmartBank.BuildingBlocks.Application;
using SmartBank.BuildingBlocks.Application.Metrics;
using SmartBank.BuildingBlocks.Domain;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.Customer.Application.Abstractions;
using SmartBank.Customer.Domain;

namespace SmartBank.Customer.Application.Accounts;

public sealed record OpenAccountCommand(Guid CustomerId, string Alias, string AccountType, string Currency) : ICommand<AccountDetailDto>;

public sealed record AccountSummaryDto(Guid Id, string Alias, string Iban, string IbanFormatted, string Type, string Status, string Currency, string AvailableBalance, string PostedBalance);
public sealed record AccountDetailDto(Guid Id, Guid CustomerId, string Alias, string Iban, string IbanFormatted, string Type, string Status, string Currency, string AvailableBalance, string PostedBalance);

public sealed class OpenAccountCommandValidator : AbstractValidator<OpenAccountCommand>
{
    public OpenAccountCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Alias).MinimumLength(2).MaximumLength(40);
        RuleFor(x => x.AccountType).Must(t => t is "Current" or "Savings").WithMessage("Type must be Current or Savings.");
        RuleFor(x => x.Currency).Must(c => c is "EUR" or "USD" or "GBP").WithMessage("Unsupported currency.");
    }
}

public sealed class OpenAccountCommandHandler : IRequestHandler<OpenAccountCommand, Result<AccountDetailDto>>
{
    private readonly IBankAccountRepository _accounts;
    private readonly ICustomerRepository _customers;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public OpenAccountCommandHandler(IBankAccountRepository accounts, ICustomerRepository customers, ICurrentUser user, IClock clock)
    {
        _accounts = accounts;
        _customers = customers;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<AccountDetailDto>> Handle(OpenAccountCommand request, CancellationToken ct)
    {
        if (_user.CustomerId != request.CustomerId && !_user.Roles.Contains("Operations"))
            return Result.Failure<AccountDetailDto>(Error.Forbidden("ACCOUNT_NOT_OWNED", "Not your customer."));
        var customer = await _customers.GetByIdAsync(request.CustomerId, ct);
        if (customer is null) return Result.Failure<AccountDetailDto>(Error.NotFound("CUSTOMER_NOT_FOUND", "Customer not found."));
        if (customer.Status != Domain.CustomerStatus.Active)
            return Result.Failure<AccountDetailDto>(Error.Conflict("CUSTOMER_NOT_ACTIVE", "Customer not active."));
        var currency = Currency.From(request.Currency);
        var type = Enum.Parse<AccountType>(request.AccountType);
        var number = await _accounts.NextAccountNumberAsync(ct);
        var factory = new IbanFactory();
        var iban = factory.Create(number);
        var account = BankAccount.Open(Guid.CreateVersion7(), request.CustomerId, iban, request.Alias, type, currency, _clock.UtcNow);
        await _accounts.AddAsync(account, ct);
        await _accounts.SaveChangesAsync(ct);
        SmartBankMeters.AccountOpened(request.Currency, request.AccountType);
        return Result.Success(ToDetail(account));
    }

    public static AccountDetailDto ToDetail(BankAccount a) => new(
        a.Id, a.CustomerId, a.Alias, a.Iban.Value, a.Iban.Formatted,
        a.Type.ToString(), a.Status.ToString(), a.Currency.Code,
        a.AvailableBalance.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
        a.PostedBalance.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
}

public sealed record GetMyAccountsQuery() : IQuery<List<AccountSummaryDto>>;

public sealed class GetMyAccountsQueryHandler : IRequestHandler<GetMyAccountsQuery, Result<List<AccountSummaryDto>>>
{
    private readonly IBankAccountRepository _accounts;
    private readonly ICurrentUser _user;
    public GetMyAccountsQueryHandler(IBankAccountRepository accounts, ICurrentUser user)
    {
        _accounts = accounts;
        _user = user;
    }
    public async Task<Result<List<AccountSummaryDto>>> Handle(GetMyAccountsQuery request, CancellationToken ct)
    {
        if (_user.CustomerId is null) return Result.Failure<List<AccountSummaryDto>>(Error.Forbidden("CUSTOMER_NOT_LINKED", "No customer linked."));
        var list = await _accounts.ListByCustomerAsync(_user.CustomerId.Value, ct);
        return Result.Success(list.Select(a => new AccountSummaryDto(a.Id, a.Alias, a.Iban.Value, a.Iban.Formatted,
            a.Type.ToString(), a.Status.ToString(), a.Currency.Code,
            a.AvailableBalance.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            a.PostedBalance.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))).ToList());
    }
}

public sealed record GetAccountByIdQuery(Guid AccountId) : IQuery<AccountDetailDto>;

public sealed class GetAccountByIdQueryHandler : IRequestHandler<GetAccountByIdQuery, Result<AccountDetailDto>>
{
    private readonly IBankAccountRepository _accounts;
    private readonly ICurrentUser _user;
    public GetAccountByIdQueryHandler(IBankAccountRepository accounts, ICurrentUser user)
    {
        _accounts = accounts;
        _user = user;
    }
    public async Task<Result<AccountDetailDto>> Handle(GetAccountByIdQuery request, CancellationToken ct)
    {
        var a = await _accounts.GetByIdAsync(request.AccountId, ct);
        if (a is null) return Result.Failure<AccountDetailDto>(Error.NotFound("ACCOUNT_NOT_FOUND", "Account not found."));
        if (_user.CustomerId != a.CustomerId && !_user.Roles.Contains("Operations"))
            return Result.Failure<AccountDetailDto>(Error.Forbidden("ACCOUNT_NOT_OWNED", "Not your account."));
        return Result.Success(OpenAccountCommandHandler.ToDetail(a));
    }
}

public sealed record LookupIbanQuery(string Iban) : IQuery<BeneficiaryDto>;
public sealed record BeneficiaryDto(bool Verified, string? HolderName, string? Currency);

public sealed class LookupIbanQueryHandler : IRequestHandler<LookupIbanQuery, Result<BeneficiaryDto>>
{
    private readonly IBankAccountRepository _accounts;
    public LookupIbanQueryHandler(IBankAccountRepository accounts) => _accounts = accounts;
    public async Task<Result<BeneficiaryDto>> Handle(LookupIbanQuery request, CancellationToken ct)
    {
        var compact = new string(request.Iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var a = await _accounts.GetByIbanAsync(compact, ct);
        if (a is null || a.Status != AccountStatus.Active) return Result.Success(new BeneficiaryDto(false, null, null));
        return Result.Success(new BeneficiaryDto(true, "A*** M***", a.Currency.Code));
    }
}

public sealed record FreezeAccountCommand(Guid AccountId) : ICommand;
public sealed record UnfreezeAccountCommand(Guid AccountId) : ICommand;

public sealed class FreezeAccountCommandHandler : IRequestHandler<FreezeAccountCommand, Result>
{
    private readonly IBankAccountRepository _accounts;
    public FreezeAccountCommandHandler(IBankAccountRepository accounts) => _accounts = accounts;
    public async Task<Result> Handle(FreezeAccountCommand request, CancellationToken ct)
    {
        var a = await _accounts.GetByIdAsync(request.AccountId, ct);
        if (a is null)
        {
            SmartBankMeters.AccountFrozen("freeze", "not_found");
            return Result.Failure(Error.NotFound("ACCOUNT_NOT_FOUND", "Not found."));
        }
        var r = a.Freeze();
        if (r.IsFailure)
        {
            SmartBankMeters.AccountFrozen("freeze", "failed");
            return r;
        }
        await _accounts.SaveChangesAsync(ct);
        SmartBankMeters.AccountFrozen("freeze", "success");
        return Result.Success();
    }
}

public sealed class UnfreezeAccountCommandHandler : IRequestHandler<UnfreezeAccountCommand, Result>
{
    private readonly IBankAccountRepository _accounts;
    public UnfreezeAccountCommandHandler(IBankAccountRepository accounts) => _accounts = accounts;
    public async Task<Result> Handle(UnfreezeAccountCommand request, CancellationToken ct)
    {
        var a = await _accounts.GetByIdAsync(request.AccountId, ct);
        if (a is null)
        {
            SmartBankMeters.AccountFrozen("unfreeze", "not_found");
            return Result.Failure(Error.NotFound("ACCOUNT_NOT_FOUND", "Not found."));
        }
        var r = a.Unfreeze();
        if (r.IsFailure)
        {
            SmartBankMeters.AccountFrozen("unfreeze", "failed");
            return r;
        }
        await _accounts.SaveChangesAsync(ct);
        SmartBankMeters.AccountFrozen("unfreeze", "success");
        return Result.Success();
    }
}
