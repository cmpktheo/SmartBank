using System.Diagnostics;
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

        var correlationString = correlationId.ToString();
        context.Items["CorrelationId"] = correlationString;
        context.Response.Headers["X-Correlation-Id"] = correlationString;
        CorrelationContext.Current = correlationString;

        var activity = Activity.Current;
        activity?.SetTag("correlation.id", correlationString);
        activity?.SetBaggage("correlation.id", correlationString);

        // Join logs <-> traces even when the Loki sink maps TraceId separately:
        // TraceId/SpanId are pushed as ordinary properties on every log line.
        var traceId = activity?.TraceId.ToString() ?? context.TraceIdentifier;
        var spanId = activity?.SpanId.ToString();

        try
        {
            using (LogContext.PushProperty("CorrelationId", correlationString))
            using (LogContext.PushProperty("TraceId", traceId))
            using (spanId is not null ? LogContext.PushProperty("SpanId", spanId) : NullScope.Instance)
            using (LogContext.PushProperty("RequestMethod", context.Request.Method))
            using (LogContext.PushProperty("RequestPath", context.Request.Path.ToString()))
            {
                await _next(context);
            }
        }
        finally
        {
            CorrelationContext.Current = null;
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
