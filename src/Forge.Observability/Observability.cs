using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Forge.Observability;

/// <summary>
/// OpenTelemetry via standard .NET primitives (ADR 15): traces and metrics for
/// HTTP, EF (via its built-in ActivitySource), outbox and jobs, with tenant and
/// correlation flowing as activity context — never sensitive payloads.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>ActivitySource names the platform emits under; hosts may add their own.</summary>
    public static class Sources
    {
        public const string Outbox = "Forge.Outbox";
        public const string Jobs = "Forge.Jobs";
    }

    public static IServiceCollection AddForgeObservability(
        this IServiceCollection services,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation();
                tracing.AddHttpClientInstrumentation();
                tracing.AddSource(Sources.Outbox, Sources.Jobs);
                tracing.AddSource("Microsoft.EntityFrameworkCore"); // EF's built-in ActivitySource
                configureTracing?.Invoke(tracing);
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddMeter("Forge.Outbox");
                configureMetrics?.Invoke(metrics);
            });

        return services;
    }

    /// <summary>
    /// Liveness (/healthz/live: the process responds) and readiness
    /// (/healthz/ready: dependency checks tagged "ready" by modules) are
    /// distinct (ADR 15). Both endpoints are host-scoped.
    /// </summary>
    public static IEndpointRouteBuilder MapForgeHealth(
        this IEndpointRouteBuilder app, Action<IEndpointConventionBuilder>? configureEndpoint = null)
    {
        var live = app.MapHealthChecks("/healthz/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false, // liveness: no dependency checks, process-up only
        });
        var ready = app.MapHealthChecks("/healthz/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });
        configureEndpoint?.Invoke(live);
        configureEndpoint?.Invoke(ready);
        return app;
    }
}
