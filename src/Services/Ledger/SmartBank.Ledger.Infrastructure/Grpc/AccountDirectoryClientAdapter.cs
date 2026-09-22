using Grpc.Core;
using SmartBank.Contracts.Grpc;
using SmartBank.Ledger.Application.Abstractions;

namespace SmartBank.Ledger.Infrastructure.Grpc;

public sealed class AccountDirectoryClientAdapter : IAccountDirectory
{
    private readonly AccountDirectory.AccountDirectoryClient _client;

    public AccountDirectoryClientAdapter(AccountDirectory.AccountDirectoryClient client) => _client = client;

    public async Task<AccountStatusInfo> GetStatusAsync(Guid accountId, CancellationToken ct)
    {
        try
        {
            var reply = await _client.GetAccountStatusAsync(new GetAccountStatusRequest { AccountId = accountId.ToString() },
                deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: ct);
            if (!reply.Found) return new AccountStatusInfo(false, accountId, Guid.Empty, "", "", "", "", "");
            return new AccountStatusInfo(true, Guid.Parse(reply.AccountId), Guid.Parse(reply.CustomerId),
                reply.Status, reply.Currency, reply.AvailableBalance, reply.CustomerEmail, reply.Iban);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.DeadlineExceeded or StatusCode.Unavailable)
        {
            throw new InvalidOperationException("CUSTOMER_UNAVAILABLE");
        }
    }

    public async Task<IbanLookupInfo> LookupByIbanAsync(string iban, CancellationToken ct)
    {
        try
        {
            var reply = await _client.LookupByIbanAsync(new LookupByIbanRequest { Iban = iban },
                deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: ct);
            if (!reply.Found) return new IbanLookupInfo(false, Guid.Empty, Guid.Empty, "", "", "", iban);
            return new IbanLookupInfo(true, Guid.Parse(reply.AccountId), Guid.Parse(reply.CustomerId),
                reply.Status, reply.Currency, reply.CustomerEmail, reply.Iban);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.DeadlineExceeded or StatusCode.Unavailable)
        {
            throw new InvalidOperationException("CUSTOMER_UNAVAILABLE");
        }
    }

    public async Task<ReserveOutcome> ReserveAsync(Guid accountId, Guid holdId, Guid transactionId, decimal amount, string currency, CancellationToken ct)
    {
        var reply = await _client.ReserveFundsAsync(new ReserveFundsRequest
        {
            AccountId = accountId.ToString(),
            HoldId = holdId.ToString(),
            TransactionId = transactionId.ToString(),
            Amount = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            Currency = currency,
            TtlSeconds = 30
        }, deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: ct);
        return new ReserveOutcome(reply.Ok, string.IsNullOrEmpty(reply.ErrorCode) ? null : reply.ErrorCode, reply.ErrorMessage);
    }

    public async Task<SettleOutcome> CaptureAsync(Guid accountId, Guid holdId, CancellationToken ct)
    {
        var reply = await _client.CommitFundsAsync(new CommitFundsRequest
        {
            AccountId = accountId.ToString(),
            HoldId = holdId.ToString()
        }, deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: ct);
        return new SettleOutcome(reply.Ok, string.IsNullOrEmpty(reply.ErrorCode) ? null : reply.ErrorCode, reply.ErrorMessage);
    }

    public async Task<SettleOutcome> CreditAsync(Guid accountId, Guid transactionId, decimal amount, string currency, CancellationToken ct)
    {
        var reply = await _client.CreditFundsAsync(new CreditFundsRequest
        {
            AccountId = accountId.ToString(),
            TransactionId = transactionId.ToString(),
            Amount = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            Currency = currency
        }, deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: ct);
        return new SettleOutcome(reply.Ok, string.IsNullOrEmpty(reply.ErrorCode) ? null : reply.ErrorCode, reply.ErrorMessage);
    }
}
