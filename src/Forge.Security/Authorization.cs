using Forge.Auditing;
using Forge.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Forge.Security;

/// <summary>Authorization requirement for one named permission.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>Fulfils <see cref="PermissionRequirement"/> via <see cref="IPermissionChecker"/>; protected checks fail closed (ADR 07/18).</summary>
internal sealed class PermissionAuthorizationHandler(IPermissionChecker checker)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (await checker.HasAsync(context.User, requirement.Permission, CancellationToken.None))
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>Materialises "permission:X" policy names on demand — no per-permission registration boilerplate.</summary>
internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "permission:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName[Prefix.Length..]))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}

/// <summary>Composition surface for permission-based authorization.</summary>
public static class SecurityExtensions
{
    public static IServiceCollection AddForgePermissions(this IServiceCollection services)
    {
        services.AddAuthorization();
        services.TryAddSingleton<PermissionCatalog>();
        services.TryAddSingleton<IRolePermissionMap>(new InMemoryRolePermissionMap());
        services.TryAddScoped<IPermissionChecker, DefaultPermissionChecker>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, PermissionAuthorizationHandler>());
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>());
        // the audit application contract (ADR 40) rides on the permission checker registered above
        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IAuditQueries, AuditQueries>();
        services.TryAddScoped<ITenantAdministration, TenantAdministration>();
        return services;
    }

    /// <summary>Protects an endpoint (or group) with a named permission.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(PermissionPolicyProvider.Prefix + permission);
}

/// <summary>Security event taxonomy (ADR 18): stable action names for audit and security eventing.</summary>
public static class SecurityEvents
{
    public const string AuthorizationDenied = "security.authorization.denied";
    public const string LoginSucceeded = "security.login.succeeded";
    public const string LoginFailed = "security.login.failed";
    public const string ImpersonationStarted = "security.impersonation.started";
    public const string ImpersonationEnded = "security.impersonation.ended";
}
