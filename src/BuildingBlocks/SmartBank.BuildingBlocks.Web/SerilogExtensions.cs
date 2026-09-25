using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;

namespace SmartBank.BuildingBlocks.Web;

/// <summary>
/// Shared Serilog wiring: Console (Compact JSON) always, Loki when LOKI_URL is set.
/// P0 production rules:
/// - never run with zero sinks (console is the fallback);
/// - low-cardinality Loki labels only (service, environment, level) — CorrelationId/
///   TraceId go to structured metadata so the index doesn't explode;
/// - SelfLog to stderr so sink failures are visible instead of silent.
/// </summary>
public static class SerilogExtensions
{
    public static LoggerConfiguration WriteToSmartBank(
        this LoggerConfiguration cfg, IConfiguration configuration, string serviceName)
    {
        var lokiUrl = Environment.GetEnvironmentVariable("LOKI_URL")
            ?? configuration["Loki:Url"];
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? configuration["Environment"]
            ?? "Production";
        var version = Environment.GetEnvironmentVariable("SERVICE_VERSION")
            ?? configuration["Service:Version"]
            ?? "unknown";

        Serilog.Debugging.SelfLog.Enable(Console.Error);

        cfg.Enrich.FromLogContext()
            .Enrich.WithProperty("service", serviceName)
            .Enrich.WithProperty("environment", environment)
            .Enrich.WithProperty("service_version", version)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning);

        // Always-on fallback: guarantees logs exist even when Loki is unset/down.
        cfg.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {service} {Message:lj} {Properties:j}{NewLine}{Exception}");

        if (!string.IsNullOrWhiteSpace(lokiUrl) && Uri.TryCreate(lokiUrl, UriKind.Absolute, out _))
        {
            cfg.WriteTo.GrafanaLoki(
                lokiUrl,
                labels:
                [
                    new LokiLabel { Key = "service", Value = serviceName },
                    new LokiLabel { Key = "environment", Value = environment },
                ],
                propertiesAsLabels: ["level"],
                propertiesAsStructuredMetadata:
                [
                    "CorrelationId", "TraceId", "SpanId",
                    "RequestPath", "RequestMethod",
                    "ErrorCode", "EventId", "TransactionId", "MessageType", "OperationName",
                ],
                traceIdMode: LokiFieldDestination.StructuredMetadata,
                spanIdMode: LokiFieldDestination.StructuredMetadata);
        }

        return cfg;
    }
}
