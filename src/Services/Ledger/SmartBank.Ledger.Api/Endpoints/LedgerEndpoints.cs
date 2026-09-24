using System.Globalization;
using System.Text;
using MediatR;
using SmartBank.BuildingBlocks.Web;
using SmartBank.Ledger.Application.Transfers;
using SmartBank.Ledger.Domain;

namespace SmartBank.Ledger.Api.Endpoints;

public static class TransferEndpoints
{
    public static void MapTransferEndpoints(this WebApplication app)
    {
        app.MapPost("/api/transfers", async (TransferRequest req, HttpContext ctx, IMediator mediator) =>
        {
            if (!ctx.Request.Headers.TryGetValue("Idempotency-Key", out var key) || !Guid.TryParse(key.FirstOrDefault(), out _))
                return Results.Problem(title: "LEDGER_IDEMPOTENCY_REQUIRED", detail: "Idempotency-Key header required.",
                    statusCode: 400, type: "https://smartbank.local/errors/ledger-idempotency-required",
                    extensions: new Dictionary<string, object?> { ["code"] = "LEDGER_IDEMPOTENCY_REQUIRED" });
            var result = await mediator.Send(new TransferFundsCommand(req.SourceAccountId, req.DestinationIban,
                decimal.Parse(req.Amount, CultureInfo.InvariantCulture), req.Currency, req.TransferType, req.Narrative, key.ToString()), ctx.RequestAborted);
            if (result.IsFailure)
            {
                if (result.Error.Code == "CUSTOMER_UNAVAILABLE")
                    return Results.Problem(title: result.Error.Code, detail: result.Error.Message, statusCode: 503,
                        type: "https://smartbank.local/errors/customer-unavailable",
                        extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
                return result.ToHttp<TransferResultDto>(201);
            }
            var v = result.Value!;
            return Results.Json(new { transactionId = v.TransactionId, reference = v.Reference, bookedAt = v.BookedAt }, statusCode: 201);
        }).RequireAuthorization();
    }

    public sealed record TransferRequest(Guid SourceAccountId, string DestinationIban, string Amount, string Currency, string TransferType, string? Narrative);
}

public static class StatementEndpoints
{
    public static void MapStatementEndpoints(this WebApplication app)
    {
        app.MapGet("/api/ledger/accounts/{accountId:guid}/transactions", async (Guid accountId, string? from, string? to, string? type, string? kind, int? page, int? pageSize,
            Application.Abstractions.IJournalRepository repo, HttpContext ctx) =>
        {
            var p = page.GetValueOrDefault(1);
            var ps = Math.Min(pageSize.GetValueOrDefault(20), 100);
            DateTimeOffset? f = from is null ? null : DateTimeOffset.Parse(from, CultureInfo.InvariantCulture);
            DateTimeOffset? t = to is null ? null : DateTimeOffset.Parse(to, CultureInfo.InvariantCulture);
            JournalType? k = Enum.TryParse<JournalType>(kind, true, out var parsed) ? parsed : null;
            var items = await repo.ListByAccountAsync(accountId, f, t, type is "All" ? null : type, k, p, ps, ctx.RequestAborted);
            var total = await repo.CountByAccountAsync(accountId, f, t, type is "All" ? null : type, k, ctx.RequestAborted);
            var balances = await RunningBalancesAsync(repo, items, accountId, f, t, type, k, ctx.RequestAborted);
            var flat = items.SelectMany(j => j.Lines.Where(l => l.AccountId == accountId)
                .Where(l => type is null or "All" || l.Direction.ToString() == type)
                .Select(l => new
                {
                    transactionId = j.Id,
                    reference = j.Reference,
                    bookedAt = j.BookedAt,
                    direction = l.Direction.ToString(),
                    amount = l.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                    currency = l.Amount.Currency.Code,
                    counterpartyIban = j.CounterpartyIban,
                    narrative = j.Narrative,
                    kind = j.Type.ToString(),
                    balanceAfter = balances.TryGetValue(l.Id, out var b) ? b.ToString("0.00", CultureInfo.InvariantCulture) : null
                })).OrderByDescending(x => x.bookedAt).ThenBy(x => x.reference).ToList();
            return Results.Ok(new { items = flat, page = p, pageSize = ps, total });
        }).RequireAuthorization();

        app.MapGet("/api/ledger/accounts/{accountId:guid}/recent", async (Guid accountId, int? limit,
            Application.Abstractions.IJournalRepository repo, HttpContext ctx) =>
        {
            var l = Math.Min(limit.GetValueOrDefault(10), 10);
            var items = await repo.ListByAccountAsync(accountId, null, null, null, null, 1, l, ctx.RequestAborted);
            var balances = await RunningBalancesAsync(repo, items, accountId, null, null, null, null, ctx.RequestAborted);
            return Results.Ok(items.SelectMany(j => j.Lines.Where(x => x.AccountId == accountId).Select(x => new
            {
                transactionId = j.Id,
                reference = j.Reference,
                bookedAt = j.BookedAt,
                direction = x.Direction.ToString(),
                kind = j.Type.ToString(),
                amount = x.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                currency = x.Amount.Currency.Code,
                balanceAfter = balances.TryGetValue(x.Id, out var b) ? b.ToString("0.00", CultureInfo.InvariantCulture) : null
            })).OrderByDescending(x => x.bookedAt).ThenBy(x => x.reference).Take(l));
        }).RequireAuthorization();

        app.MapGet("/api/ledger/accounts/{accountId:guid}/statement.csv", async (Guid accountId, string? from, string? to,
            Application.Abstractions.IJournalRepository repo, HttpContext ctx) =>
        {
            DateTimeOffset? f = from is null ? null : DateTimeOffset.Parse(from, CultureInfo.InvariantCulture);
            DateTimeOffset? t = to is null ? null : DateTimeOffset.Parse(to, CultureInfo.InvariantCulture);
            var items = await repo.ListByAccountAsync(accountId, f, t, null, null, 1, 1000, ctx.RequestAborted);
            var balances = await RunningBalancesAsync(repo, items, accountId, f, t, null, null, ctx.RequestAborted);
            var rows = items.SelectMany(j => j.Lines.Where(x => x.AccountId == accountId).Select(line => new { Journal = j, Line = line }))
                .OrderByDescending(x => x.Journal.BookedAt).ThenBy(x => x.Journal.Reference).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("BookedAt,Reference,Kind,Direction,Amount,Currency,CounterpartyIban,Narrative,BalanceAfter");
            foreach (var r in rows)
            {
                var j = r.Journal;
                var line = r.Line;
                sb.AppendLine(string.Join(',', j.BookedAt.ToString("O"), Csv(j.Reference), j.Type, line.Direction,
                    line.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture), line.Amount.Currency.Code,
                    Csv(j.CounterpartyIban), Csv(j.Narrative),
                    balances.TryGetValue(line.Id, out var b) ? b.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty));
            }
            return Results.Text(sb.ToString(), "text/csv");
        }).RequireAuthorization();
    }

    private static string Csv(string? s) => '"' + (s ?? string.Empty).Replace("\"", "\"\"") + '"';

    /// <summary>
    /// Running balance per journal line: prefix aggregate over all matching lines older than the
    /// page in (BookedAt, Id) order, then forward accumulation across the page oldest-first.
    /// Exact regardless of page cuts, without loading full history.
    /// </summary>
    private static async Task<Dictionary<Guid, decimal>> RunningBalancesAsync(
        Application.Abstractions.IJournalRepository repo,
        List<JournalTransaction> journals,
        Guid accountId,
        DateTimeOffset? from, DateTimeOffset? to, string? direction, JournalType? kind,
        CancellationToken ct)
    {
        var pageLines = journals
            .SelectMany(j => j.Lines.Where(l => l.AccountId == accountId)
                .Where(l => direction is null or "All" || l.Direction.ToString() == direction)
                .Select(l => new { Line = l }))
            .OrderBy(x => x.Line.BookedAt).ThenBy(x => x.Line.Id)
            .ToList();
        var result = new Dictionary<Guid, decimal>();
        if (pageLines.Count == 0) return result;
        var oldest = pageLines[0].Line;
        var running = await repo.SumSignedOlderThanAsync(accountId, from, to, direction, kind, oldest.BookedAt, oldest.Id, ct);
        foreach (var pl in pageLines)
        {
            running += pl.Line.Direction == LedgerDirection.Credit ? pl.Line.Amount.Amount : -pl.Line.Amount.Amount;
            result[pl.Line.Id] = running;
        }
        return result;
    }
}
