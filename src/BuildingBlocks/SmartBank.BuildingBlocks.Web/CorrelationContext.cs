namespace SmartBank.BuildingBlocks.Web;

/// <summary>
/// AsyncLocal carrier for the current request's correlation id.
/// Set by <see cref="CorrelationIdMiddleware"/>, read by outgoing
/// <see cref="CorrelationIdForwardingHandler"/> and gRPC interceptors
/// so Ledger -&gt; Customer and any HttpClient call keep the same id.
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> CurrentHolder = new();

    public static string? Current
    {
        get => CurrentHolder.Value;
        set => CurrentHolder.Value = value;
    }
}
