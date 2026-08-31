using Forge.Observability;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Forge.ReferenceSaaS.ServiceDefaults;

/// <summary>
/// Shared service defaults for the reference topology (ADR 25): Forge
/// observability plus OTLP export when an endpoint is configured (Aspire sets
/// OTEL_EXPORTER_OTLP_ENDPOINT automatically; a bare container simply skips it).
/// </summary>
public static class ServiceDefaultsExtensions
{
    public static IServiceCollection AddServiceDefaults(this IServiceCollection services, string serviceName)
    {
        var otlp = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        services.AddForgeObservability(
            serviceName,
            configureTracing: tracing =>
            {
                if (otlp is not null)
                {
                    tracing.AddOtlpExporter();
                }
            },
            configureMetrics: metrics =>
            {
                if (otlp is not null)
                {
                    metrics.AddOtlpExporter();
                }
            });
        return services;
    }
}
