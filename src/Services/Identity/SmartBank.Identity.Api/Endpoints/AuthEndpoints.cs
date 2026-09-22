using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using SmartBank.BuildingBlocks.Application;
using SmartBank.BuildingBlocks.Domain;
using SmartBank.BuildingBlocks.Web;
using SmartBank.Identity.Application.Abstractions;
using SmartBank.Identity.Application.Auth;

namespace SmartBank.Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest req, IMediator mediator, HttpContext ctx) =>
        {
            var result = await mediator.Send(new LoginCommand(req.Email, req.Password), ctx.RequestAborted);
            if (result.IsFailure) return result.ToHttp<LoginResult>();
            var v = result.Value!;
            if (v.MfaRequired)
                return Results.Ok(new { mfaRequired = true, challengeId = v.ChallengeId, expiresInSeconds = v.ExpiresInSeconds });
            return Results.Ok(new
            {
                mfaRequired = false,
                accessToken = v.Tokens!.AccessToken,
                refreshToken = v.Tokens.RefreshToken,
                expiresIn = v.Tokens.ExpiresIn,
                tokenType = "Bearer",
                customerId = v.Tokens.CustomerId
            });
        });

        group.MapPost("/mfa/verify", async (VerifyRequest req, IMediator mediator, HttpContext ctx) =>
        {
            var result = await mediator.Send(new VerifyMfaCommand(req.ChallengeId, req.Code), ctx.RequestAborted);
            if (result.IsFailure)
            {
                var status = result.Error.Code switch
                {
                    "IDENTITY_MFA_EXPIRED" => 401,
                    "IDENTITY_MFA_INVALID" => 401,
                    "IDENTITY_MFA_LOCKED" => 401,
                    _ => 400
                };
                if (status == 401 && result.Error.Code == "IDENTITY_MFA_LOCKED")
                    return Results.Problem(title: result.Error.Code, detail: result.Error.Message, statusCode: 401,
                        type: "https://smartbank.local/errors/identity-mfa-locked",
                        extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
                return result.ToHttp<TokenPair>();
            }
            var t = result.Value!;
            return Results.Ok(new
            {
                mfaRequired = false,
                accessToken = t.AccessToken,
                refreshToken = t.RefreshToken,
                expiresIn = t.ExpiresIn,
                tokenType = "Bearer",
                customerId = t.CustomerId
            });
        });

        group.MapPost("/mfa/resend", async (ResendRequest req, IMediator mediator, HttpContext ctx) =>
        {
            var result = await mediator.Send(new ResendMfaCommand(req.ChallengeId), ctx.RequestAborted);
            if (result.IsFailure)
            {
                if (result.Error.Code == "IDENTITY_MFA_RESEND_LIMIT")
                    return Results.Problem(title: result.Error.Code, detail: result.Error.Message, statusCode: 429,
                        type: "https://smartbank.local/errors/identity-mfa-resend-limit",
                        extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
                return result.ToHttp<ResendMfaResult>();
            }
            return Results.Ok(new { expiresInSeconds = result.Value!.ExpiresInSeconds });
        });

        group.MapPost("/refresh", async (RefreshRequest req, IMediator mediator, HttpContext ctx) =>
        {
            var result = await mediator.Send(new RefreshTokenCommand(req.RefreshToken), ctx.RequestAborted);
            if (result.IsFailure) return result.ToHttp<TokenPair>();
            var t = result.Value!;
            return Results.Ok(new
            {
                accessToken = t.AccessToken,
                refreshToken = t.RefreshToken,
                expiresIn = t.ExpiresIn,
                tokenType = "Bearer",
                customerId = t.CustomerId
            });
        });

        group.MapPost("/logout", async (HttpContext ctx, IMediator mediator, LogoutRequest? req) =>
        {
            var auth = ctx.Request.Headers.Authorization.ToString();
            if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return Results.Unauthorized();
            var token = auth["Bearer ".Length..].Trim();
            var handler = new JwtSecurityTokenHandler();
            JwtSecurityToken jwt;
            try { jwt = handler.ReadJwtToken(token); }
            catch { return Results.Unauthorized(); }
            var jti = jwt.Claims.FirstOrDefault(c => c.Type == "jti" || c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            if (jti is null) return Results.Unauthorized();
            var exp = jwt.ValidTo == DateTime.MinValue ? DateTime.UtcNow.AddMinutes(15) : jwt.ValidTo;
            var ttl = exp - DateTime.UtcNow;
            if (ttl < TimeSpan.FromSeconds(1)) ttl = TimeSpan.FromSeconds(1);
            await mediator.Send(new LogoutCommand(jti, ttl, req?.RefreshToken), ctx.RequestAborted);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapGet("/me", async (HttpContext ctx, IMediator mediator) =>
        {
            var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.User.FindFirstValue("sub");
            if (!Guid.TryParse(sub, out var id)) return Results.Unauthorized();
            var result = await mediator.Send(new GetMeQuery(id), ctx.RequestAborted);
            if (result.IsFailure) return Results.Unauthorized();
            var m = result.Value!;
            return Results.Ok(new { userId = m.UserId, email = m.Email, customerId = m.CustomerId, roles = m.Roles });
        }).RequireAuthorization();

        // E2E OTP seam: only mapped when E2E_OTP_SEAM=true
        if (Environment.GetEnvironmentVariable("E2E_OTP_SEAM") == "true")
        {
            group.MapGet("/e2e/otp", (string email, Application.Abstractions.IOtpDelivery delivery) =>
            {
                if (delivery is Infrastructure.Auth.InMemoryOtpSink sink)
                {
                    var code = sink.For(email);
                    if (code is null) return Results.NotFound();
                    return Results.Ok(new { code });
                }
                return Results.NotFound();
            });
        }
    }

    public sealed record LoginRequest(string Email, string Password);
    public sealed record VerifyRequest(string ChallengeId, string Code);
    public sealed record ResendRequest(string ChallengeId);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record LogoutRequest(string? RefreshToken);
}
