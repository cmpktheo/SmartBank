using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using StackExchange.Redis;

namespace SmartBank.BuildingBlocks.Web;

public sealed class JwtBlacklistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConnectionMultiplexer? _redis;

    public JwtBlacklistMiddleware(RequestDelegate next, IConnectionMultiplexer? redis = null)
    {
        _next = next;
        _redis = redis;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_redis is not null
            && context.Request.Headers.TryGetValue("Authorization", out var auth)
            && auth.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = auth.ToString()["Bearer ".Length..].Trim();
            var jti = GetJti(token);
            if (jti is not null)
            {
                var db = _redis.GetDatabase();
                if (await db.KeyExistsAsync($"sb:id:blacklist:{jti}"))
                {
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/problem+json";
                    var body = new
                    {
                        type = "https://smartbank.local/errors/token-revoked",
                        title = "TOKEN_REVOKED",
                        status = 401,
                        detail = "Token has been revoked.",
                        correlationId = context.Items["CorrelationId"]?.ToString(),
                        code = "TOKEN_REVOKED",
                        traceId = context.TraceIdentifier
                    };
                    await context.Response.WriteAsync(JsonSerializer.Serialize(body));
                    return;
                }
            }
        }
        await _next(context);
    }

    private static string? GetJti(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            return jwt.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
        }
        catch
        {
            return null;
        }
    }
}
