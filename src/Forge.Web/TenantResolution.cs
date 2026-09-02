using Forge.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Forge.Web;

/// <summary>
/// Resolves the tenant for a request from a trusted source. Resolvers run in
/// registration order; the first non-null result wins.
/// </summary>
public interface ITenantResolver
{
    ValueTask<string?> ResolveAsync(HttpContext context);
}

/// <summary>Reference resolver: reads the X-Tenant header. Phase 2.2 adds a claims-based resolver.</summary>
public sealed class HeaderTenantResolver : ITenantResolver
{
    public const string HeaderName = "X-Tenant";

    public ValueTask<string?> ResolveAsync(HttpContext context) =>
        ValueTask.FromResult(
            context.Request.Headers.TryGetValue(HeaderName, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.ToString()
                : null);
}

/// <summary>Endpoint metadata marking an endpoint as host-scoped: it runs without a tenant (e.g. health, OpenAPI).</summary>
public sealed class HostScopeMetadata
{
    public static readonly HostScopeMetadata Instance = new();
}

/// <summary>Composition surface for tenancy over HTTP (ADR 05).</summary>
public static class TenancyWebExtensions
{
    public static IServiceCollection AddForgeTenancy(this IServiceCollection services)
    {
        services.TryAddSingleton<CurrentTenant>();
        services.TryAddSingleton<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantResolver, HeaderTenantResolver>());
        return services;
    }

    /// <summary>Marks an endpoint (or group) as host-scoped: no tenant required, privileged host context.</summary>
    public static TBuilder WithHostScope<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(HostScopeMetadata.Instance);
        return builder;
    }

    /// <summary>
    /// Deny-by-default tenant resolution (ADR 05). Place after routing so
    /// endpoint metadata is visible: unresolved tenant on a tenant-scoped
    /// endpoint short-circuits with Problem Details; host-scoped endpoints run
    /// under an explicit host scope.
    /// </summary>
    public static IApplicationBuilder UseForgeTenancy(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var currentTenant = context.RequestServices.GetRequiredService<CurrentTenant>();

            if (context.GetEndpoint()?.Metadata.GetMetadata<HostScopeMetadata>() is not null)
            {
                using (currentTenant.BeginHostScope())
                {
                    await next(context);
                }

                return;
            }

            foreach (var resolver in context.RequestServices.GetServices<ITenantResolver>())
            {
                if (await resolver.ResolveAsync(context) is { } tenantId)
                {
                    // a registered directory makes tenant state authoritative:
                    // unknown or disabled tenants fail resolution (ADR 05)
                    if (context.RequestServices.GetService<ITenantDirectory>() is { } directory
                        && await directory.GetAsync(tenantId, context.RequestAborted) is not { Enabled: true })
                    {
                        await Results.Problem(
                                statusCode: StatusCodes.Status403Forbidden,
                                title: "Tenant not available",
                                detail: "The resolved tenant is unknown or disabled.")
                            .ExecuteAsync(context);
                        return;
                    }

                    currentTenant.SetTenant(tenantId);
                    await next(context);
                    return;
                }
            }

            await Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Missing tenant context",
                    detail: "No trusted tenant could be resolved for this request.")
                .ExecuteAsync(context);
        });
}
