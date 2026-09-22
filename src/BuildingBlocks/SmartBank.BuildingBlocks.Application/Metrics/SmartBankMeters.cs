using System.Diagnostics.Metrics;

namespace SmartBank.BuildingBlocks.Application.Metrics;

/// <summary>
/// Business + auth meters. Exported via OTEL AddMeter("SmartBank.*") in
/// SmartBank.BuildingBlocks.Web. Uses System.Diagnostics.Metrics only (no extra deps).
/// </summary>
public static class SmartBankMeters
{
    private static readonly Meter IdentityMeter = new("SmartBank.Identity", "1.0.0");
    private static readonly Meter LedgerMeter = new("SmartBank.Ledger", "1.0.0");
    private static readonly Meter CustomerMeter = new("SmartBank.Customer", "1.0.0");
    private static readonly Meter CardsMeter = new("SmartBank.Cards", "1.0.0");
    private static readonly Meter NotificationsMeter = new("SmartBank.Notifications", "1.0.0");

    private static readonly Counter<long> AuthLogins = IdentityMeter.CreateCounter<long>(
        "smartbank.auth.logins", unit: "{login}", description: "Login attempts by result.");
    private static readonly Counter<long> AuthMfaVerifies = IdentityMeter.CreateCounter<long>(
        "smartbank.auth.mfa.verifies", unit: "{verify}", description: "MFA verify attempts by result.");
    private static readonly Counter<long> AuthMfaResends = IdentityMeter.CreateCounter<long>(
        "smartbank.auth.mfa.resends", unit: "{resend}", description: "MFA resend attempts by result.");
    private static readonly Counter<long> AuthRefreshes = IdentityMeter.CreateCounter<long>(
        "smartbank.auth.token.refreshes", unit: "{refresh}", description: "Token refresh attempts by result.");
    private static readonly Counter<long> AuthLogouts = IdentityMeter.CreateCounter<long>(
        "smartbank.auth.logouts", unit: "{logout}", description: "Logouts.");

    private static readonly Counter<long> TransfersCreated = LedgerMeter.CreateCounter<long>(
        "smartbank.ledger.transfers_created", unit: "{transfer}", description: "Successful transfers by currency/type.");
    private static readonly Counter<long> TransfersFailed = LedgerMeter.CreateCounter<long>(
        "smartbank.ledger.transfers_failed", unit: "{transfer}", description: "Failed transfers by code.");
    private static readonly Histogram<double> TransferAmount = LedgerMeter.CreateHistogram<double>(
        "smartbank.ledger.transfer.amount", unit: "{currency}", description: "Transfer amount histogram.");

    private static readonly Counter<long> AccountsOpened = CustomerMeter.CreateCounter<long>(
        "smartbank.customer.accounts_opened", unit: "{account}", description: "Accounts opened by currency/type.");
    private static readonly Counter<long> AccountsFrozen = CustomerMeter.CreateCounter<long>(
        "smartbank.customer.accounts_frozen", unit: "{account}", description: "Account freeze/unfreeze ops.");

    private static readonly Counter<long> CardOps = CardsMeter.CreateCounter<long>(
        "smartbank.cards.ops", unit: "{op}", description: "Card freeze/unfreeze/limits ops by op/result.");

    private static readonly Counter<long> NotificationsSent = NotificationsMeter.CreateCounter<long>(
        "smartbank.notifications.sent", unit: "{notification}", description: "Notifications sent by template/channel.");

    public static void Login(string result) =>
        AuthLogins.Add(1, new KeyValuePair<string, object?>("result", result));

    public static void MfaVerify(string result) =>
        AuthMfaVerifies.Add(1, new KeyValuePair<string, object?>("result", result));

    public static void MfaResend(string result) =>
        AuthMfaResends.Add(1, new KeyValuePair<string, object?>("result", result));

    public static void TokenRefresh(string result) =>
        AuthRefreshes.Add(1, new KeyValuePair<string, object?>("result", result));

    public static void Logout() => AuthLogouts.Add(1);

    public static void TransferSucceeded(string currency, string transferType, double amount)
    {
        TransfersCreated.Add(1,
            new KeyValuePair<string, object?>("currency", currency),
            new KeyValuePair<string, object?>("type", transferType));
        TransferAmount.Record(amount,
            new KeyValuePair<string, object?>("currency", currency));
    }

    public static void TransferFailed(string code) =>
        TransfersFailed.Add(1, new KeyValuePair<string, object?>("code", code));

    public static void AccountOpened(string currency, string type) =>
        AccountsOpened.Add(1,
            new KeyValuePair<string, object?>("currency", currency),
            new KeyValuePair<string, object?>("type", type));

    public static void AccountFrozen(string op, string result) =>
        AccountsFrozen.Add(1,
            new KeyValuePair<string, object?>("op", op),
            new KeyValuePair<string, object?>("result", result));

    public static void CardOp(string op, string result) =>
        CardOps.Add(1,
            new KeyValuePair<string, object?>("op", op),
            new KeyValuePair<string, object?>("result", result));

    public static void NotificationSent(string template, string channel = "Email") =>
        NotificationsSent.Add(1,
            new KeyValuePair<string, object?>("template", template),
            new KeyValuePair<string, object?>("channel", channel));
}
