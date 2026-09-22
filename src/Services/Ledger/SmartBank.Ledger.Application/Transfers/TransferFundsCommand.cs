using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using MediatR;
using SmartBank.BuildingBlocks.Application;
using SmartBank.BuildingBlocks.Application.Metrics;
using SmartBank.BuildingBlocks.Domain;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.BuildingBlocks.EventBus;
using SmartBank.BuildingBlocks.Infrastructure.Idempotency;
using SmartBank.BuildingBlocks.Infrastructure.Outbox;
using SmartBank.Ledger.Application.Abstractions;
using SmartBank.Ledger.Domain;

namespace SmartBank.Ledger.Application.Transfers;

public sealed record TransferFundsCommand(
    Guid SourceAccountId,
    string DestinationIban,
    decimal Amount,
    string Currency,
    string TransferType,
    string? Narrative,
    string IdempotencyKey
) : ICommand<TransferResultDto>;

public sealed record TransferResultDto(Guid TransactionId, string Reference, DateTimeOffset BookedAt);

public sealed class TransferFundsCommandValidator : AbstractValidator<TransferFundsCommand>
{
    public TransferFundsCommandValidator()
    {
        RuleFor(x => x.SourceAccountId).NotEmpty();
        RuleFor(x => x.DestinationIban).Must(iban => Iban.TryParse(iban, out _)).WithMessage("Invalid IBAN.");
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(1_000_000m);
        RuleFor(x => x.Currency).Must(c => c is "EUR" or "USD" or "GBP").WithMessage("Unsupported currency.");
        RuleFor(x => x.TransferType).Must(t => t is "Internal" or "Domestic" or "International").WithMessage("Invalid transfer type.");
        RuleFor(x => x.Narrative).MaximumLength(140);
        RuleFor(x => x.IdempotencyKey).Must(k => Guid.TryParse(k, out _)).WithMessage("Idempotency-Key must be UUID.");
    }
}

public sealed class TransferFundsCommandHandler : IRequestHandler<TransferFundsCommand, Result<TransferResultDto>>
{
    private readonly IAccountDirectory _directory;
    private readonly IJournalRepository _journals;
    private readonly IIdempotencyStore _idempotency;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly Action<string>? _outboxWriter;

    public TransferFundsCommandHandler(
        IAccountDirectory directory, IJournalRepository journals, IIdempotencyStore idempotency,
        ICurrentUser user, IClock clock, Action<string>? outboxWriter = null)
    {
        _directory = directory;
        _journals = journals;
        _idempotency = idempotency;
        _user = user;
        _clock = clock;
        _outboxWriter = outboxWriter;
    }

    public async Task<Result<TransferResultDto>> Handle(TransferFundsCommand request, CancellationToken ct)
    {
        if (request.TransferType == "International")
        {
            SmartBankMeters.TransferFailed("LEDGER_FX_NOT_SUPPORTED");
            return Result.Failure<TransferResultDto>(Error.Validation("LEDGER_FX_NOT_SUPPORTED", "International transfers not supported."));
        }

        var fingerprint = Fingerprint(request);
        var begin = await _idempotency.TryBeginAsync($"sb:led:idemp:{request.IdempotencyKey}", fingerprint, TimeSpan.FromHours(24), ct);
        if (begin.Result == IdempotencyBeginResult.Replay)
        {
            var replayed = JsonSerializer.Deserialize<TransferResultDto>(begin.ResponseBytes!)!;
            return Result.Success(replayed);
        }
        if (begin.Result == IdempotencyBeginResult.Conflict)
            return Result.Failure<TransferResultDto>(Error.Conflict("LEDGER_IDEMPOTENCY_CONFLICT", "Idempotency key reused with different body."));
        if (begin.Result == IdempotencyBeginResult.InProgress)
            return Result.Failure<TransferResultDto>(Error.Conflict("LEDGER_IN_PROGRESS", "Transfer already in progress."));

        if (!Iban.TryParse(request.DestinationIban, out var destIban))
            return Result.Failure<TransferResultDto>(Error.Validation("VALIDATION", "Invalid IBAN."));
        var money = Money.Of(request.Amount, Currency.From(request.Currency));

        AccountStatusInfo source;
        IbanLookupInfo dest;
        try
        {
            source = await _directory.GetStatusAsync(request.SourceAccountId, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == "CUSTOMER_UNAVAILABLE")
        {
            return Result.Failure<TransferResultDto>(Error.Conflict("CUSTOMER_UNAVAILABLE", "Account directory temporarily unavailable."));
        }
        if (!source.Found) return Fail("ACCOUNT_NOT_FOUND", "Source account not found.", request, fingerprint, ct);
        if (source.CustomerId != _user.CustomerId) return Fail("ACCOUNT_NOT_OWNED", "Not your account.", request, fingerprint, ct, forbidden: true);
        if (source.Status != "Active") return Fail("ACCOUNT_NOT_ACTIVE", $"Source is {source.Status}.", request, fingerprint, ct);
        if (source.Currency != request.Currency) return Fail("ACCOUNT_CURRENCY", "Currency mismatch.", request, fingerprint, ct);

        try
        {
            dest = await _directory.LookupByIbanAsync(destIban!.Value, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == "CUSTOMER_UNAVAILABLE")
        {
            return Result.Failure<TransferResultDto>(Error.Conflict("CUSTOMER_UNAVAILABLE", "Account directory temporarily unavailable."));
        }
        if (!dest.Found)
        {
            var code = request.TransferType == "Domestic" ? "LEDGER_EXTERNAL_WIRE_NOT_SUPPORTED" : "LEDGER_DESTINATION_UNKNOWN";
            return Fail(code, "Destination unknown in v1 (no external rails).", request, fingerprint, ct);
        }
        if (dest.AccountId == request.SourceAccountId)
            return Result.Failure<TransferResultDto>(Error.Validation("LEDGER_SAME_ACCOUNT", "Source and destination must differ."));
        if (dest.Status != "Active") return Fail("ACCOUNT_NOT_ACTIVE", "Destination not active.", request, fingerprint, ct);
        if (dest.Currency != request.Currency)
            return Result.Failure<TransferResultDto>(Error.Conflict("LEDGER_FX_NOT_SUPPORTED", "Cross-currency not supported."));

        var transactionId = Guid.CreateVersion7();
        ReserveOutcome reserve;
        try
        {
            reserve = await _directory.ReserveAsync(request.SourceAccountId, transactionId, transactionId, request.Amount, request.Currency, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == "CUSTOMER_UNAVAILABLE")
        {
            return Result.Failure<TransferResultDto>(Error.Conflict("CUSTOMER_UNAVAILABLE", "Account directory temporarily unavailable."));
        }
        if (!reserve.Ok)
            return Fail(reserve.ErrorCode ?? "ACCOUNT_INSUFFICIENT_FUNDS", reserve.ErrorMessage ?? "Reserve failed.", request, fingerprint, ct);

        var reference = ReferenceGenerator.Generate(_clock.UtcNow, transactionId);
        var journal = JournalTransaction.Transfer(transactionId, request.SourceAccountId, dest.AccountId,
            money, destIban.Value, request.Narrative, request.IdempotencyKey, _clock.UtcNow, reference);
        if (journal.IsFailure) return journal.Error.Code == "LEDGER_SAME_ACCOUNT" || journal.Error.Code == "LEDGER_ZERO_AMOUNT"
            ? Result.Failure<TransferResultDto>(Error.Validation(journal.Error.Code, journal.Error.Message))
            : Result.Failure<TransferResultDto>(journal.Error);

        // Book + stage settlement intent atomically. Actual money movement
        // (CaptureHold + CreditPosted) happens async in the Customer consumer
        // via RabbitMQ, leaving room for fraud/AML background checks.
        // Do NOT call Capture/Credit synchronously here (legacy sync settle removed).
        var bookedAt = _clock.UtcNow;
        var evt = new MoneyTransferredIntegrationEvent
        {
            EventId = Guid.CreateVersion7(),
            OccurredAt = bookedAt,
            CorrelationId = transactionId,
            TransactionId = transactionId,
            Reference = reference,
            SourceAccountId = request.SourceAccountId,
            DestinationAccountId = dest.AccountId,
            Amount = request.Amount,
            Currency = request.Currency,
            SourceIban = source.Iban,
            DestinationIban = dest.Iban,
            Narrative = request.Narrative,
        };
        await _journals.AddAsync(journal.Value!, ct);
        _journals.AddOutbox(new OutboxMessage
        {
            Id = evt.EventId,
            Type = MoneyTransferredIntegrationEvent.TypeName,
            Payload = evt.ToPayload(),
            OccurredAt = bookedAt,
            CorrelationId = transactionId,
        });
        try
        {
            await _journals.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            // Journal + outbox commit failed after a successful Reserve.
            // Hold expires in 30s (Customer TTL) — do not Release here (unreliable).
            // Client retries with the SAME Idempotency-Key.
            SmartBankMeters.TransferFailed("LEDGER_POST_RESERVE_FAILURE");
            throw;
        }

        var dto = new TransferResultDto(transactionId, reference, bookedAt);
        await _idempotency.CompleteAsync($"sb:led:idemp:{request.IdempotencyKey}", fingerprint, JsonSerializer.SerializeToUtf8Bytes(dto), ct);
        SmartBankMeters.TransferSucceeded(request.Currency, request.TransferType, (double)request.Amount);
        return Result.Success(dto);
    }

    private Result<TransferResultDto> Fail(string code, string message, TransferFundsCommand request, string fingerprint, CancellationToken ct, bool forbidden = false)
    {
        SmartBankMeters.TransferFailed(code);
        var error = forbidden ? Error.Forbidden(code, message)
            : code is "ACCOUNT_NOT_FOUND" or "LEDGER_DESTINATION_UNKNOWN" ? Error.Conflict(code, message)
            : code is "LEDGER_FX_NOT_SUPPORTED" or "LEDGER_EXTERNAL_WIRE_NOT_SUPPORTED" ? Error.Validation(code, message)
            : Error.Conflict(code, message);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { code, message });
        // Store failure so replay returns same error (spec 14.3). Fire-and-forget ok.
        _ = _idempotency.CompleteWithErrorAsync($"sb:led:idemp:{request.IdempotencyKey}", fingerprint, payload, ct);
        return Result.Failure<TransferResultDto>(error);
    }

    public static string Fingerprint(TransferFundsCommand request)
    {
        var raw = $"{request.SourceAccountId}|{request.DestinationIban}|{request.Amount}|{request.Currency}|{request.TransferType}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}
