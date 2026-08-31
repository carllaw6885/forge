namespace Forge.Tenancy;

/// <summary>
/// Tenant-safe cache key helper (ADR 17): keys are always scoped, so one
/// tenant's cache entries can never be served to another. Unresolved scope
/// throws — deny-by-default applies to caches too.
/// </summary>
public static class TenantCacheKey
{
    public static string For(ICurrentTenant tenant, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return tenant.Scope switch
        {
            TenantScope.Tenant => $"t:{tenant.Id}:{key}",
            TenantScope.Host => $"host:{key}",
            _ => throw new InvalidOperationException("cannot build a cache key with unresolved tenant scope"),
        };
    }
}
