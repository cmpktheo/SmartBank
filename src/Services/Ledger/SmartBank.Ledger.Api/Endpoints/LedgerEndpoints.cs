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
        app.MapGet("/api/ledger/accounts/{accountId:guid}/transactions", async (Guid accountId, string? from, string? to, string? type, int? page, int? pageSize,
            Application.Abstractions.IJournalRepository repo, HttpContext ctx) =>
        {
            var p = page.GetValueOrDefault(1);
            var ps = Math.Min(pageSize.GetValueOrDefault(20), 100);
            DateTimeOffset? f = from is null ? null : DateTimeOffset.Parse(from, CultureInfo.InvariantCulture);
            DateTimeOffset? t = to is null ? null : DateTimeOffset.Parse(to, CultureInfo.InvariantCulture);
            var items = await repo.ListByAccountAsync(accountId, f, t, type is "All" ? null : type, p, ps, ctx.RequestAborted);
            var total = await repo.CountByAccountAsync(accountId, f, t, type is "All" ? null : type, ctx.RequestAborted);
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
                    balanceAfter = (string?)null
                })).ToList();
            return Results.Ok(new { items = flat, page = p, pageSize = ps, total });
        }).RequireAuthorization();

        app.MapGet("/api/ledger/accounts/{accountId:guid}/recent", async (Guid accountId, int? limit,
            Application.Abstractions.IJournalRepository repo, HttpContext ctx) =>
        {
            var l = Math.Min(limit.GetValueOrDefault(10), 10);
            var items = await repo.ListByAccountAsync(accountId, null, null, null, 1, l, ctx.RequestAborted);
            return Results.Ok(items.SelectMany(j => j.Lines.Where(x => x.AccountId == accountId).Select(x => new
            {
                transactionId = j.Id,
                reference = j.Reference,
                bookedAt = j.BookedAt,
                direction = x.Direction.ToString(),
                amount = x.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                currency = x.Amount.Currency.Code
            })).Take(l));
        }).RequireAuthorization();

        app.MapGet("/api/ledger/accounts/{accountId:guid}/statement.csv", async (Guid accountId, string? from, string? to,
            Application.Abstractions.IJournalRepository repo, HttpContext ctx) =>
        {
            DateTimeOffset? f = from is null ? null : DateTimeOffset.Parse(from, CultureInfo.InvariantCulture);
            DateTimeOffset? t = to is null ? null : DateTimeOffset.Parse(to, CultureInfo.InvariantCulture);
            var items = await repo.ListByAccountAsync(accountId, f, t, null, 1, 1000, ctx.RequestAborted);
            var sb = new StringBuilder();
            sb.AppendLine("BookedAt,Reference,Direction,Amount,Currency,CounterpartyIban,Narrative");
            foreach (var j in items)
                foreach (var line in j.Lines.Where(x => x.AccountId == accountId))
                    sb.AppendLine(string.Join(',', j.BookedAt.ToString("O"), Csv(j.Reference), line.Direction,
                        line.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture), line.Amount.Currency.Code,
                        Csv(j.CounterpartyIban), Csv(j.Narrative)));
            return Results.Text(sb.ToString(), "text/csv");
        }).RequireAuthorization();
    }

    private static string Csv(string? s) => '"' + (s ?? string.Empty).Replace("\"", "\"\"") + '"';
}
