using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace SmartBank.BuildingBlocks.Web;

public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Enterprise OTEL wiring: RED + runtime metrics, distributed traces, OTLP export.
    /// Safe when OTEL_EXPORTER_OTLP_ENDPOINT is unset (tests/dev without collector).
    /// Business meters: each SmartBank.* Meter must be listed explicitly below.
    /// (Meter name matching is exact - a "SmartBank.*" wildcard entry does NOT
    /// subscribe the provider, and those instruments are silently dropped.)
    /// </summary>
    public static IHostApplicationBuilder AddSmartBankOpenTelemetry(this IHostApplicationBuilder builder, string serviceName)
    {
        var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var useOtlp = !string.IsNullOrWhiteSpace(endpoint);
        Uri? otlpUri = null;
        if (useOtlp && !Uri.TryCreate(endpoint, UriKind.Absolute, out otlpUri))
        {
            useOtlp = false;
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName: serviceName, serviceInstanceId: Environment.MachineName)
                .AddAttributes(
                [
                    new KeyValuePair<string, object>(
                        "deployment.environment",
                        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"),
                    new KeyValuePair<string, object>(
                        "service.version",
                        Environment.GetEnvironmentVariable("SERVICE_VERSION") ?? "unknown"),
                ]))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation(o =>
                    {
                        o.RecordException = true;
                        o.EnrichWithHttpRequest = (activity, request) =>
                        {
                            if (request.Headers.TryGetValue("X-Correlation-Id", out var cid))
                            {
                                activity.SetTag("correlation.id", cid.ToString());
                            }
                        };
                        o.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response.status_code", response.StatusCode);
                        };
                    })
                    .AddHttpClientInstrumentation(o => o.RecordException = true)
                    .AddEntityFrameworkCoreInstrumentation(o => o.SetDbStatementForText = true)
                    .AddGrpcClientInstrumentation();

                if (useOtlp)
                {
                    t.AddOtlpExporter(o =>
                    {
                        o.Endpoint = otlpUri!;
                        o.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
            })
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("SmartBank.Identity")
                    .AddMeter("SmartBank.Ledger")
                    .AddMeter("SmartBank.Customer")
                    .AddMeter("SmartBank.Cards")
                    .AddMeter("SmartBank.Notifications");

                if (useOtlp)
                {
                    m.AddOtlpExporter(o =>
                    {
                        o.Endpoint = otlpUri!;
                        o.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
            });

        return builder;
    }
}
