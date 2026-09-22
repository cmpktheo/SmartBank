using MediatR;
using SmartBank.BuildingBlocks.Web;
using SmartBank.Customer.Application.Accounts;

namespace SmartBank.Customer.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/api/accounts", async (IMediator mediator, HttpContext ctx) =>
        {
            var result = await mediator.Send(new GetMyAccountsQuery(), ctx.RequestAborted);
            return result.ToHttp<List<AccountSummaryDto>>();
        }).RequireAuthorization();

        app.MapGet("/api/accounts/{id:guid}", async (Guid id, IMediator mediator, HttpContext ctx) =>
        {
            var result = await mediator.Send(new GetAccountByIdQuery(id), ctx.RequestAborted);
            return result.ToHttp<AccountDetailDto>();
        }).RequireAuthorization();

        app.MapPost("/api/accounts", async (OpenAccountRequest req, IMediator mediator, HttpContext ctx) =>
        {
            var customerId = ctx.User.FindFirst("customer_id")?.Value is string cid && Guid.TryParse(cid, out var g)
                ? g : Guid.Empty;
            var result = await mediator.Send(new OpenAccountCommand(customerId, req.Alias, req.Type, req.Currency), ctx.RequestAborted);
            return result.ToHttp<AccountDetailDto>(201);
        }).RequireAuthorization();

        app.MapGet("/api/accounts/iban/{iban}", async (string iban, IMediator mediator, HttpContext ctx) =>
        {
            var result = await mediator.Send(new LookupIbanQuery(iban), ctx.RequestAborted);
            if (result.IsFailure) return result.ToHttp<BeneficiaryDto>();
            var v = result.Value!;
            return Results.Ok(new { verified = v.Verified, accountHolderName = v.HolderName });
        }).RequireAuthorization();

        app.MapGet("/api/customers/me", async (IMediator mediator, HttpContext ctx) =>
        {
            var cid = ctx.User.FindFirst("customer_id")?.Value;
            if (!Guid.TryParse(cid, out var id)) return Results.Unauthorized();
            return Results.Ok(new { customerId = id });
        }).RequireAuthorization();
    }

    public sealed record OpenAccountRequest(string Alias, string Type, string Currency);
}
