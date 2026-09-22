using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartBank.BuildingBlocks.EventBus;

/// <summary>
/// Published by Ledger after a journal is booked. Consumers settle async:
/// Customer captures hold + credits destination, Notification logs email,
/// future fraud/AML checks hook in here without blocking the booking API.
/// Spec §6 Milestone 4.4.
/// </summary>
public sealed record MoneyTransferredIntegrationEvent : IntegrationEvent
{
    public override string EventType => TypeName;
    public const string TypeName = "MoneyTransferredIntegrationEvent";

    public Guid TransactionId { get; init; }
    public string Reference { get; init; } = string.Empty;
    public Guid SourceAccountId { get; init; }
    public Guid DestinationAccountId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string SourceIban { get; init; } = string.Empty;
    public string DestinationIban { get; init; } = string.Empty;
    public string? Narrative { get; init; }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToPayload() => JsonSerializer.Serialize(this, JsonOpts);

    public static MoneyTransferredIntegrationEvent? FromPayload(string payload)
    {
        try { return JsonSerializer.Deserialize<MoneyTransferredIntegrationEvent>(payload, JsonOpts); }
        catch { return null; }
    }
}

/// <summary>Single place for exchange / routing / queue names so Ledger, Customer and Notification agree.</summary>
public static class LedgerTopology
{
    public const string Exchange = "smartbank.ledger";
    public const string TransferredRoutingKey = "ledger.transferred";
    public const string TransferredType = MoneyTransferredIntegrationEvent.TypeName;

    public const string CustomerQueue = "customer.ledger.transferred";
    public const string NotificationQueue = "notification.ledger.transferred";

    // Fraud/AML and future background checks bind their own queue to the same routing key.
    public const string RiskQueue = "risk.ledger.transferred";
}
