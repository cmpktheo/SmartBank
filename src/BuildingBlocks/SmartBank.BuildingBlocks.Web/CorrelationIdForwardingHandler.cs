namespace SmartBank.BuildingBlocks.Web;

/// <summary>
/// Copies X-Correlation-Id onto every outgoing HttpClient call
/// (REST + Grpc.Net.ClientFactory transport, which sits on HttpClient).
/// Without this, Ledger -&gt; Customer gRPC loses the id and traces/logs split.
/// Registered once via <c>ConfigureHttpClientDefaults</c>.
/// </summary>
public sealed class CorrelationIdForwardingHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationContext.Current;
        if (!string.IsNullOrWhiteSpace(correlationId) && !request.Headers.Contains("X-Correlation-Id"))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
