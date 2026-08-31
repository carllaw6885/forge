using Forge.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Forge.Modularity;

public static class ForgeServiceCollectionExtensions
{
    /// <summary>
    /// Composes the given modules explicitly (ADR 01): validates the dependency
    /// graph (fails fast on cycles, duplicates, unknown dependencies, shared
    /// schema ownership), then runs ConfigureServices in dependency order.
    /// No assembly scanning, ever.
    /// </summary>
    public static IServiceCollection AddForge(this IServiceCollection services, params IForgeModule[] modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var byId = new Dictionary<string, IForgeModule>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            byId[module.Manifest.Id] = module; // duplicates surface via Validate below
        }

        var manifests = modules.Select(m => m.Manifest).ToList();
        if (ModuleGraph.Validate(manifests) is { Count: > 0 } errors)
        {
            throw new InvalidOperationException(
                "invalid module composition: " + string.Join("; ", errors));
        }

        var ordered = ModuleGraph.TopologicalSort(manifests)
            .Select(m => byId[m.Id])
            .ToList();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(new ModuleCatalog(ordered));

        foreach (var module in ordered)
        {
            module.ConfigureServices(services);
        }

        return services;
    }

    /// <summary>Runs each module's ConfigureApplication in dependency order.</summary>
    public static IServiceProvider UseForge(this IServiceProvider provider)
    {
        var catalog = provider.GetRequiredService<ModuleCatalog>();
        foreach (var module in catalog.Modules)
        {
            module.ConfigureApplication(provider);
        }

        return provider;
    }
}
