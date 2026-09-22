using Serilog.Context;

namespace SmartBank.BuildingBlocks.Web;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        Guid correlationId;
        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var values)
            && Guid.TryParse(values.FirstOrDefault(), out var parsed))
        {
            correlationId = parsed;
        }
        else
        {
            correlationId = Guid.CreateVersion7();
        }
        context.Items["CorrelationId"] = correlationId.ToString();
        context.Response.Headers["X-Correlation-Id"] = correlationId.ToString();
        System.Diagnostics.Activity.Current?.SetTag("correlation.id", correlationId.ToString());
        System.Diagnostics.Activity.Current?.SetBaggage("correlation.id", correlationId.ToString());
        using (LogContext.PushProperty("CorrelationId", correlationId.ToString()))
        {
            await _next(context);
        }
    }
}
