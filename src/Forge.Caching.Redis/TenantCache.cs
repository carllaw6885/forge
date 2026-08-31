using Forge.Tenancy;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Forge.Caching.Redis;

/// <summary>
/// Tenant-safe distributed cache over Redis (ADR 17): every key is mandatorily
/// tenant-scoped via <see cref="TenantCacheKey"/>, and failure is degradable —
/// where an authoritative source exists, a broken cache means a miss, never a
/// broken request. Redis is optional; single-instance v0.1 runs without it.
/// </summary>
public sealed partial class TenantCache(IDistributedCache inner, ICurrentTenant tenant, ILogger<TenantCache> logger)
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "distributed cache {Operation} degraded")]
    private static partial void CacheDegraded(ILogger logger, string operation, Exception exception);

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await inner.GetStringAsync(TenantCacheKey.For(tenant, key), cancellationToken);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or InvalidOperationException))
        {
            // degrade to a miss: the authoritative source answers instead (ADR 17)
            CacheDegraded(logger, "read", ex);
            return null;
        }
    }

    public async Task SetAsync(string key, string value, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        try
        {
            await inner.SetStringAsync(
                TenantCacheKey.For(tenant, key), value,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeToLive },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or InvalidOperationException))
        {
            CacheDegraded(logger, "write", ex);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await inner.RemoveAsync(TenantCacheKey.For(tenant, key), cancellationToken);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or InvalidOperationException))
        {
            CacheDegraded(logger, "remove", ex);
        }
    }
}

/// <summary>DI registration for the optional Redis reference adapter.</summary>
public static class RedisCacheExtensions
{
    public static IServiceCollection AddForgeRedisCache(this IServiceCollection services, string connectionString)
    {
        services.AddStackExchangeRedisCache(options =>
            options.Configuration = connectionString + ",abortConnect=false");
        services.TryAddScoped<TenantCache>();
        return services;
    }
}
