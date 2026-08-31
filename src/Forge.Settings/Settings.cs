using System.Text.Json;
using Forge.Tenancy;
using Microsoft.Extensions.Caching.Memory;

namespace Forge.Settings;

/// <summary>Setting scopes in precedence order: user overrides tenant overrides application (ADR 13).</summary>
public enum SettingScope
{
    Application,
    Tenant,
    User,
}

/// <summary>
/// A typed, validated setting (ADR 13). Definitions are declared explicitly by
/// modules; a value that fails validation is rejected at write time.
/// </summary>
public sealed record SettingDefinition<T>(string Key, T DefaultValue, Func<T, bool>? Validate = null)
{
    public SettingDefinition<T> EnsureValidKey()
    {
        if (SecretKeyGuard.LooksLikeSecret(Key))
        {
            throw new InvalidOperationException(
                $"setting key '{Key}' looks like a secret — secrets never live in ordinary settings (ADR 13); use ISecretStore");
        }

        return this;
    }
}

/// <summary>Secrets never live in ordinary settings (ADR 13); this guard enforces it at the seam.</summary>
public static class SecretKeyGuard
{
    private static readonly string[] SensitiveFragments =
        ["password", "secret", "token", "credential", "apikey", "api-key", "connectionstring"];

    public static bool LooksLikeSecret(string key) =>
        SensitiveFragments.Any(f => key.Contains(f, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Raw scoped storage; values are JSON strings. Implementations: in-memory reference, SQL Server.</summary>
public interface ISettingStore
{
    Task<string?> GetAsync(string key, SettingScope scope, string? scopeId, CancellationToken cancellationToken);

    Task SetAsync(string key, SettingScope scope, string? scopeId, string value, CancellationToken cancellationToken);
}

/// <summary>In-memory reference store.</summary>
public sealed class InMemorySettingStore : ISettingStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string, SettingScope, string?), string> _values = new();

    public Task<string?> GetAsync(string key, SettingScope scope, string? scopeId, CancellationToken cancellationToken) =>
        Task.FromResult(_values.TryGetValue((key, scope, scopeId), out var v) ? v : null);

    public Task SetAsync(string key, SettingScope scope, string? scopeId, string value, CancellationToken cancellationToken)
    {
        _values[(key, scope, scopeId)] = value;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Typed settings with scope precedence and tenant-safe caching (ADRs 13/17):
/// cache keys are tenant-scoped, entries invalidate on write, and a short TTL
/// is the safety net.
/// </summary>
public sealed class SettingsService(ISettingStore store, ICurrentTenant tenant, IMemoryCache cache)
{
    public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    // Any write bumps the version, invalidating every cached resolution: an
    // application-scope change must reach all tenants and users, and a scoped
    // key removal cannot express that.
    // ponytail: whole-service invalidation; per-key versions if write churn matters.
    private static long _version;

    /// <summary>Resolves with precedence user → tenant → application → default.</summary>
    public async Task<T> GetAsync<T>(SettingDefinition<T> definition, string? userId, CancellationToken ct)
    {
        definition.EnsureValidKey();
        var cacheKey = CacheKey(definition.Key, userId);
        if (cache.TryGetValue<T>(cacheKey, out var cached))
        {
            return cached!;
        }

        var value = await ResolveAsync(definition, userId, ct);
        cache.Set(cacheKey, value, CacheTtl);
        return value;
    }

    public async Task SetAsync<T>(
        SettingDefinition<T> definition, SettingScope scope, string? scopeId, T value, CancellationToken ct)
    {
        definition.EnsureValidKey();
        if (definition.Validate is not null && !definition.Validate(value))
        {
            throw new ArgumentException($"value for setting '{definition.Key}' failed validation", nameof(value));
        }

        if (scope == SettingScope.Tenant)
        {
            scopeId ??= tenant.Id ?? throw new InvalidOperationException("tenant scope requires a resolved tenant");
        }

        await store.SetAsync(definition.Key, scope, scopeId, JsonSerializer.Serialize(value), ct);

        // event-driven invalidation; the TTL is only the safety net (ADR 17)
        Interlocked.Increment(ref _version);
    }

    private async Task<T> ResolveAsync<T>(SettingDefinition<T> definition, string? userId, CancellationToken ct)
    {
        if (userId is not null
            && await store.GetAsync(definition.Key, SettingScope.User, userId, ct) is { } userValue)
        {
            return JsonSerializer.Deserialize<T>(userValue)!;
        }

        if (tenant.Scope == TenantScope.Tenant
            && await store.GetAsync(definition.Key, SettingScope.Tenant, tenant.Id, ct) is { } tenantValue)
        {
            return JsonSerializer.Deserialize<T>(tenantValue)!;
        }

        if (await store.GetAsync(definition.Key, SettingScope.Application, null, ct) is { } appValue)
        {
            return JsonSerializer.Deserialize<T>(appValue)!;
        }

        return definition.DefaultValue;
    }

    private string CacheKey(string key, string? userId) =>
        TenantCacheKey.For(tenant, $"setting:v{Volatile.Read(ref _version)}:{key}:u:{userId ?? "-"}");
}
