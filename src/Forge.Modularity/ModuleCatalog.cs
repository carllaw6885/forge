using Forge.Core.Modules;

namespace Forge.Modularity;

/// <summary>
/// The composed application's inspectable module graph (ADR 01): the modules
/// exactly as registered, in dependency order. Registered as a singleton by
/// AddForge for diagnostics and CLI-parity inspection.
/// </summary>
public sealed class ModuleCatalog
{
    internal ModuleCatalog(IReadOnlyList<IForgeModule> modulesInDependencyOrder) =>
        Modules = modulesInDependencyOrder;

    /// <summary>Modules in dependency order (dependencies before dependents).</summary>
    public IReadOnlyList<IForgeModule> Modules { get; }

    public IReadOnlyList<ModuleManifest> Manifests => Modules.Select(m => m.Manifest).ToList();
}
