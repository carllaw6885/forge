using Forge.Core.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Forge.Modularity;

/// <summary>
/// The deliberately minimal module lifecycle (ADR 02): declarative metadata,
/// service registration, application configuration. Nothing more without a
/// demonstrated need. Modules are composed explicitly via
/// <see cref="ForgeServiceCollectionExtensions.AddForge"/> — never discovered.
/// </summary>
public interface IForgeModule
{
    /// <summary>Identity, dependencies and owned schemas; same model as forge-module.json.</summary>
    ModuleManifest Manifest { get; }

    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Post-build configuration in dependency order. Host-agnostic on purpose:
    /// web endpoint mapping stays in Forge.Web (Phase 1.4), not the kernel.
    /// </summary>
    void ConfigureApplication(IServiceProvider services)
    {
    }
}
