using Serilog;
using Serilog.Sinks.Grafana.Loki;

namespace SmartBank.BuildingBlocks.Web;

/// <summary>
/// Shared Serilog wiring: Console always, Loki when LOKI_URL is set.
/// Keeps local dev working without collector (console fallback).
/// </summary>
public static class SerilogExtensions
{
    public static LoggerConfiguration WriteToSmartBank(
        this LoggerConfiguration cfg, IConfiguration configuration, string serviceName)
    {
        var lokiUrl = Environment.GetEnvironmentVariable("LOKI_URL")
            ?? configuration["Loki:Url"];

        cfg.Enrich.FromLogContext()
            .Enrich.WithProperty("service", serviceName);

        if (!string.IsNullOrWhiteSpace(lokiUrl) && Uri.TryCreate(lokiUrl, UriKind.Absolute, out _))
        {
            cfg.WriteTo.GrafanaLoki(
                lokiUrl,
                labels: [new LokiLabel { Key = "service", Value = serviceName }],
                propertiesAsLabels: ["CorrelationId"],
                propertiesAsStructuredMetadata: ["CorrelationId", "RequestPath"],
                traceIdMode: LokiFieldDestination.StructuredMetadata,
                spanIdMode: LokiFieldDestination.StructuredMetadata);
        }

        return cfg;
    }
}
