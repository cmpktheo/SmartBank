using FluentAssertions;
using NSubstitute;
using SmartBank.BuildingBlocks.Application;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.BuildingBlocks.EventBus;
using SmartBank.BuildingBlocks.Infrastructure.Idempotency;
using SmartBank.BuildingBlocks.Infrastructure.Outbox;
using SmartBank.Ledger.Application.Abstractions;
using SmartBank.Ledger.Application.Transfers;
using SmartBank.Ledger.Domain;

namespace SmartBank.Ledger.Application.Tests;

public sealed class FakeIdempotency : IIdempotencyStore
{
    private readonly Dictionary<string, (string fp, byte[]? resp, bool error, DateTime at)> _store = new();
    public Task<IdempotencyBeginOutcome> TryBeginAsync(string key, string requestHash, TimeSpan ttl, CancellationToken ct = default)
    {
        if (_store.TryGetValue(key, out var e))
        {
            if (e.fp != requestHash) return Task.FromResult(new IdempotencyBeginOutcome(IdempotencyBeginResult.Conflict, null));
            if (e.resp is not null && !e.error) return Task.FromResult(new IdempotencyBeginOutcome(IdempotencyBeginResult.Replay, e.resp));
            if (e.resp is not null && e.error) return Task.FromResult(new IdempotencyBeginOutcome(IdempotencyBeginResult.Replay, e.resp));
            if (DateTime.UtcNow - e.at < TimeSpan.FromSeconds(60)) return Task.FromResult(new IdempotencyBeginOutcome(IdempotencyBeginResult.InProgress, null));
            return Task.FromResult(new IdempotencyBeginOutcome(IdempotencyBeginResult.Started, null));
        }
        _store[key] = (requestHash, null, false, DateTime.UtcNow);
        return Task.FromResult(new IdempotencyBeginOutcome(IdempotencyBeginResult.Started, null));
    }
    public Task CompleteAsync(string key, string requestHash, byte[] responseBytes, CancellationToken ct = default)
    {
        _store[key] = (requestHash, responseBytes, false, DateTime.UtcNow);
        return Task.CompletedTask;
    }
    public Task CompleteWithErrorAsync(string key, string requestHash, byte[] errorBytes, CancellationToken ct = default)
    {
        _store[key] = (requestHash, errorBytes, true, DateTime.UtcNow);
        return Task.CompletedTask;
    }
}

public sealed class FakeDirectory : IAccountDirectory
{
    public Guid SourceId { get; set; }
    public Guid DestId { get; set; }
    public Guid CustomerId { get; set; }
    public bool Insufficient { get; set; }
    public Task<AccountStatusInfo> GetStatusAsync(Guid accountId, CancellationToken ct)
        => Task.FromResult(new AccountStatusInfo(true, SourceId, CustomerId, "Active", "EUR", "2500.00", "a@x.test", "DE89370400440532013000"));
    public Task<IbanLookupInfo> LookupByIbanAsync(string iban, CancellationToken ct)
        => Task.FromResult(new IbanLookupInfo(true, DestId, Guid.NewGuid(), "Active", "EUR", "b@x.test", iban));
    public Task<ReserveOutcome> ReserveAsync(Guid accountId, Guid holdId, Guid transactionId, decimal amount, string currency, CancellationToken ct)
        => Task.FromResult(Insufficient ? new ReserveOutcome(false, "ACCOUNT_INSUFFICIENT_FUNDS", "No funds.") : new ReserveOutcome(true, null, null));
    public int Captures { get; private set; }
    public int Credits { get; private set; }
    public Task<SettleOutcome> CaptureAsync(Guid accountId, Guid holdId, CancellationToken ct)
    { Captures++; return Task.FromResult(new SettleOutcome(true, null, null)); }
    public Task<SettleOutcome> CreditAsync(Guid accountId, Guid transactionId, decimal amount, string currency, CancellationToken ct)
    { Credits++; return Task.FromResult(new SettleOutcome(true, null, null)); }
}

public sealed class FakeJournals : IJournalRepository
{
    public List<JournalTransaction> Stored { get; } = [];
    public List<OutboxMessage> Outbox { get; } = [];
    public Task AddAsync(JournalTransaction tx, CancellationToken ct) { Stored.Add(tx); return Task.CompletedTask; }
    public void AddOutbox(OutboxMessage message) => Outbox.Add(message);
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    public Task<List<JournalTransaction>> ListByAccountAsync(Guid accountId, DateTimeOffset? from, DateTimeOffset? to, string? direction, int page, int pageSize, CancellationToken ct)
        => Task.FromResult(Stored);
    public Task<int> CountByAccountAsync(Guid accountId, DateTimeOffset? from, DateTimeOffset? to, string? direction, CancellationToken ct)
        => Task.FromResult(Stored.Count);
}

public sealed class TransferHandlerTests
{
    private readonly Guid _customer = Guid.CreateVersion7();
    private readonly Guid _source = Guid.CreateVersion7();
    private readonly Guid _dest = Guid.CreateVersion7();

    private (TransferFundsCommandHandler handler, FakeJournals journals, FakeIdempotency idemp, FakeDirectory dir) Create(bool insufficient = false)
    {
        var dir = new FakeDirectory { SourceId = _source, DestId = _dest, CustomerId = _customer, Insufficient = insufficient };
        var journals = new FakeJournals();
        var idemp = new FakeIdempotency();
        var user = Substitute.For<ICurrentUser>();
        user.CustomerId.Returns(_customer);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return (new TransferFundsCommandHandler(dir, journals, idemp, user, clock), journals, idemp, dir);
    }

    private TransferFundsCommand Cmd(string key) => new(_source, "DE89370400440532013000", 100m, "EUR", "Internal", "Rent", key);

    [Fact]
    public async Task Transfer_HappyPath_WritesTwoLinesAndOutbox()
    {
        var (h, journals, _, dir) = Create();
        var r = await h.Handle(Cmd(Guid.NewGuid().ToString()), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        journals.Stored.Should().HaveCount(1);
        journals.Stored[0].Lines.Should().HaveCount(2);
        // Async settlement: journal + outbox staged atomically, no sync gRPC settle.
        journals.Outbox.Should().HaveCount(1);
        journals.Outbox[0].Type.Should().Be(MoneyTransferredIntegrationEvent.TypeName);
        journals.Outbox[0].Payload.Should().Contain("transactionId");
        dir.Captures.Should().Be(0);
        dir.Credits.Should().Be(0);
    }

    [Fact]
    public async Task Transfer_HappyPath_OutboxPayload_MatchesSpec()
    {
        var (h, journals, _, _) = Create();
        var r = await h.Handle(Cmd(Guid.NewGuid().ToString()), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        var evt = MoneyTransferredIntegrationEvent.FromPayload(journals.Outbox[0].Payload);
        evt.Should().NotBeNull();
        evt!.TransactionId.Should().Be(r.Value!.TransactionId);
        evt.Reference.Should().Be(r.Value.Reference);
        evt.Amount.Should().Be(100m);
        evt.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task Transfer_InsufficientFunds_DoesNotWriteJournal()
    {
        var (h, journals, _, _) = Create(insufficient: true);
        var r = await h.Handle(Cmd(Guid.NewGuid().ToString()), CancellationToken.None);
        r.Error.Code.Should().Be("ACCOUNT_INSUFFICIENT_FUNDS");
        journals.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Transfer_ReplaySameIdempotencyKey_DoesNotDoubleBook()
    {
        var (h, journals, _, _) = Create();
        var key = Guid.NewGuid().ToString();
        var r1 = await h.Handle(Cmd(key), CancellationToken.None);
        // Second handler with same idempotency store but fresh journals to detect double-book:
        r1.IsSuccess.Should().BeTrue();
        var r2 = await h.Handle(Cmd(key), CancellationToken.None);
        r2.IsSuccess.Should().BeTrue();
        journals.Stored.Should().HaveCount(1);
    }

    [Fact]
    public async Task Transfer_SameKeyDifferentBody_Conflict()
    {
        var (h, _, _, _) = Create();
        var key = Guid.NewGuid().ToString();
        await h.Handle(Cmd(key), CancellationToken.None);
        var other = new TransferFundsCommand(_source, "DE89370400440532013000", 200m, "EUR", "Internal", "Rent", key);
        var r = await h.Handle(other, CancellationToken.None);
        // Replay path returns stored success OR conflict; our fake returns conflict only on fp mismatch when no resp.
        // Since first completed, fp mismatch -> Conflict.
        r.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Transfer_International_Rejected()
    {
        var (h, journals, _, _) = Create();
        var cmd = new TransferFundsCommand(_source, "DE89370400440532013000", 10m, "EUR", "International", null, Guid.NewGuid().ToString());
        var r = await h.Handle(cmd, CancellationToken.None);
        r.Error.Code.Should().Be("LEDGER_FX_NOT_SUPPORTED");
        journals.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Transfer_UnownedSource_Forbidden()
    {
        var dir = new FakeDirectory { SourceId = _source, DestId = _dest, CustomerId = Guid.CreateVersion7() };
        var journals = new FakeJournals();
        var user = Substitute.For<ICurrentUser>();
        user.CustomerId.Returns(_customer);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var h = new TransferFundsCommandHandler(dir, journals, new FakeIdempotency(), user, clock);
        var r = await h.Handle(Cmd(Guid.NewGuid().ToString()), CancellationToken.None);
        r.Error.Code.Should().Be("ACCOUNT_NOT_OWNED");
    }
}
