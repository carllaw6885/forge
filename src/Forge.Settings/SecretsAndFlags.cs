using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Forge.Settings;

/// <summary>
/// Pluggable secret storage (ADR 13): secrets are abstractions only in v0.1 —
/// no secret value ever passes through the settings pipeline or its cache.
/// </summary>
public interface ISecretStore
{
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken);
}

/// <summary>Reference provider: environment variables. Vault/Key Vault providers are adapters post-v0.1.</summary>
public sealed class EnvironmentSecretStore : ISecretStore
{
    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(Environment.GetEnvironmentVariable(name));
}

/// <summary>
/// An operational rollout flag (ADR 13). Deliberately a distinct type from any
/// entitlement: a flag can never grant an unentitled capability (ADR 07) — it
/// only toggles rollout of something the caller is already entitled to.
/// </summary>
public sealed record OperationalFlag(string Name, bool DefaultEnabled = false);

/// <summary>Evaluates operational flags with the settings precedence chain (application, then tenant override).</summary>
public sealed class OperationalFlags(SettingsService settings)
{
    public Task<bool> IsEnabledAsync(OperationalFlag flag, CancellationToken cancellationToken) =>
        settings.GetAsync(Definition(flag), userId: null, cancellationToken);

    public Task SetAsync(OperationalFlag flag, SettingScope scope, bool enabled, CancellationToken cancellationToken) =>
        settings.SetAsync(Definition(flag), scope, scopeId: null, enabled, cancellationToken);

    private static SettingDefinition<bool> Definition(OperationalFlag flag) =>
        new($"flag:{flag.Name}", flag.DefaultEnabled);
}

/// <summary>DI registration for typed settings, secrets and operational flags.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddForgeSettings(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.TryAddSingleton<ISettingStore, InMemorySettingStore>();
        services.TryAddSingleton<ISecretStore, EnvironmentSecretStore>();
        services.TryAddScoped<SettingsService>();
        services.TryAddScoped<OperationalFlags>();
        return services;
    }
}
