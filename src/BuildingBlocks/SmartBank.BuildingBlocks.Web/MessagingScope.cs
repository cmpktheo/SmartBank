using System.Diagnostics;
using Serilog.Context;

namespace SmartBank.BuildingBlocks.Web;

/// <summary>
/// Restores log/trace correlation inside background workers (RabbitMQ consumers,
/// outbox dispatchers, hold-expiry sweeps) where there is no HttpContext.
/// Starts a short Activity so TraceId/SpanId exist, tags correlation.id, and
/// pushes CorrelationId (+ optional EventId/TransactionId/MessageType) into
/// Serilog's LogContext so every ILogger line in the scope is joinable to the
/// originating HTTP request in Loki/Tempo.
/// </summary>
public static class MessagingScope
{
    private static readonly ActivitySource Source = new("SmartBank.Messaging");

    public static IDisposable Begin(
        Guid correlationId,
        Guid? eventId = null,
        Guid? transactionId = null,
        string? messageType = null,
        string? parentTraceparent = null)
    {
        ActivityContext parent = default;
        var hasParent = false;
        if (!string.IsNullOrWhiteSpace(parentTraceparent))
        {
            try
            {
                hasParent = ActivityContext.TryParse(parentTraceparent, null, out parent);
            }
            catch
            {
                hasParent = false;
            }
        }

        var activity = hasParent
            ? Source.StartActivity("message.handle", ActivityKind.Consumer, parent)
            : Source.StartActivity("message.handle", ActivityKind.Consumer);
        activity?.SetTag("correlation.id", correlationId.ToString());
        if (eventId.HasValue) activity?.SetTag("messaging.event_id", eventId.Value.ToString());
        if (transactionId.HasValue) activity?.SetTag("messaging.transaction_id", transactionId.Value.ToString());

        var scopes = new List<IDisposable>(6)
        {
            LogContext.PushProperty("CorrelationId", correlationId.ToString()),
        };

        var traceId = activity?.TraceId.ToString() ?? Activity.Current?.TraceId.ToString();
        var spanId = activity?.SpanId.ToString() ?? Activity.Current?.SpanId.ToString();
        if (!string.IsNullOrWhiteSpace(traceId)) scopes.Add(LogContext.PushProperty("TraceId", traceId!));
        if (!string.IsNullOrWhiteSpace(spanId)) scopes.Add(LogContext.PushProperty("SpanId", spanId!));
        if (eventId.HasValue) scopes.Add(LogContext.PushProperty("EventId", eventId.Value.ToString()));
        if (transactionId.HasValue) scopes.Add(LogContext.PushProperty("TransactionId", transactionId.Value.ToString()));
        if (!string.IsNullOrWhiteSpace(messageType)) scopes.Add(LogContext.PushProperty("MessageType", messageType!));

        return new CompositeScope(scopes, activity);
    }

    /// <summary>
    /// Scope for timer-based sweeps (e.g. hold expiry) that have no incoming
    /// correlation id. Mints a fresh sweep id so all lines of one sweep are
    /// joinable via CorrelationId, and pushes OperationName for filtering.
    /// </summary>
    public static IDisposable BeginOperation(string operationName)
    {
        var sweepId = Guid.CreateVersion7();
        var activity = Source.StartActivity("operation." + operationName, ActivityKind.Internal);
        activity?.SetTag("correlation.id", sweepId.ToString());
        activity?.SetTag("operation.name", operationName);

        var scopes = new List<IDisposable>(5)
        {
            LogContext.PushProperty("CorrelationId", sweepId.ToString()),
            LogContext.PushProperty("OperationName", operationName),
        };
        var traceId = activity?.TraceId.ToString() ?? Activity.Current?.TraceId.ToString();
        var spanId = activity?.SpanId.ToString() ?? Activity.Current?.SpanId.ToString();
        if (!string.IsNullOrWhiteSpace(traceId)) scopes.Add(LogContext.PushProperty("TraceId", traceId!));
        if (!string.IsNullOrWhiteSpace(spanId)) scopes.Add(LogContext.PushProperty("SpanId", spanId!));

        return new CompositeScope(scopes, activity);
    }

    private sealed class CompositeScope(List<IDisposable> scopes, Activity? activity) : IDisposable
    {
        public void Dispose()
        {
            activity?.Stop();
            activity?.Dispose();
            for (var i = scopes.Count - 1; i >= 0; i--) scopes[i].Dispose();
        }
    }
}
