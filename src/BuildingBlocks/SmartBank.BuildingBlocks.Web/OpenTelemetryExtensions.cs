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
    /// Business meters: add Meter("SmartBank.&lt;Domain&gt;") — picked up via AddMeter("SmartBank.*").
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
                .AddService(serviceName: serviceName, serviceInstanceId: Environment.MachineName))
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
                    .AddMeter("SmartBank.*");

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
