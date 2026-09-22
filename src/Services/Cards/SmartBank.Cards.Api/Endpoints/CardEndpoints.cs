using MediatR;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.BuildingBlocks.Web;
using SmartBank.Cards.Application.Cards;
using SmartBank.Cards.Domain;
using StackExchange.Redis;

namespace SmartBank.Cards.Api.Endpoints;

public static class CardEndpoints
{
    public static void MapCardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/cards", async (IMediator mediator, HttpContext ctx) =>
        {
            var r = await mediator.Send(new GetCardsQuery(), ctx.RequestAborted);
            return r.ToHttp<List<CardDto>>();
        }).RequireAuthorization();

        app.MapGet("/api/cards/{id:guid}", async (Guid id, IMediator mediator, HttpContext ctx) =>
        {
            var r = await mediator.Send(new GetCardQuery(id), ctx.RequestAborted);
            return r.ToHttp<CardDto>();
        }).RequireAuthorization();

        app.MapPost("/api/cards/{id:guid}/freeze", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            if (!ctx.Request.Headers.TryGetValue("Idempotency-Key", out var key))
                return Results.BadRequest(new { code = "VALIDATION" });
            var r = await mediator.Send(new FreezeCardCommand(id, key.ToString()), ctx.RequestAborted);
            return r.ToHttp<CardDto>();
        }).RequireAuthorization();

        app.MapPost("/api/cards/{id:guid}/unfreeze", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            if (!ctx.Request.Headers.TryGetValue("Idempotency-Key", out var key))
                return Results.BadRequest(new { code = "VALIDATION" });
            var r = await mediator.Send(new UnfreezeCardCommand(id, key.ToString()), ctx.RequestAborted);
            return r.ToHttp<CardDto>();
        }).RequireAuthorization();

        app.MapPatch("/api/cards/{id:guid}/limits", async (Guid id, LimitsRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var r = await mediator.Send(new SetCardLimitsCommand(id,
                decimal.Parse(req.DailyEcommerceLimit, System.Globalization.CultureInfo.InvariantCulture),
                decimal.Parse(req.DailyAtmLimit, System.Globalization.CultureInfo.InvariantCulture)), ctx.RequestAborted);
            return r.ToHttp<CardDto>();
        }).RequireAuthorization();

        app.MapPost("/api/cards/{id:guid}/reveal-cvv", async (Guid id, HttpContext ctx, IMediator mediator,
            Infrastructure.Persistence.CardsDbContext db, IPanProtector protector, IConnectionMultiplexer redis) =>
        {
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? ctx.User.FindFirst("sub")?.Value ?? "anon";
            var rdb = redis.GetDatabase();
            var count = await rdb.StringIncrementAsync($"sb:crd:reveal:{userId}");
            if (count == 1) await rdb.KeyExpireAsync($"sb:crd:reveal:{userId}", TimeSpan.FromMinutes(10));
            if (count > 5)
                return Results.Problem(title: "CARD_REVEAL_LIMIT", detail: "Too many reveals.", statusCode: 429,
                    type: "https://smartbank.local/errors/card-reveal-limit",
                    extensions: new Dictionary<string, object?> { ["code"] = "CARD_REVEAL_LIMIT" });
            var card = await db.Cards.FindAsync([id], ctx.RequestAborted);
            if (card is null) return Results.NotFound();
            return Results.Ok(new { cvv = protector.DecryptCvv(card.CvvEnc) });
        }).RequireAuthorization();
    }

    public sealed record LimitsRequest(string DailyEcommerceLimit, string DailyAtmLimit);
}
