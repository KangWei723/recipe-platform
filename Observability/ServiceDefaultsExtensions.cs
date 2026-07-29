using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Observability;

public static class ServiceDefaultsExtensions
{
    /// <summary>
    /// Wires ASP.NET Core + HttpClient auto-instrumentation (traces and metrics) plus
    /// .NET runtime metrics, exported via OTLP to the collector configured under
    /// OpenTelemetry:OtlpEndpoint (defaults to the local Jaeger OTLP/gRPC receiver).
    /// Pass configureTracing to bolt on extra instrumentation (e.g. EF Core) per service.
    /// </summary>
    public static IHostApplicationBuilder AddObservability(
        this IHostApplicationBuilder builder,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null)
    {
        var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://127.0.0.1:4317";

        // The OTLP exporter talks gRPC over plaintext (no TLS) to the local collector.
        // .NET's HTTP/2 client refuses cleartext h2c connections unless this switch is set.
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                configureTracing?.Invoke(tracing);

                tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
            });

        return builder;
    }
}
