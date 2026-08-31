using Forge.Cli.Modules;
using Xunit;

namespace Forge.TenancyTests;

/// <summary>
/// Tenant-isolation invariant harness (ADR 05). Every invariant here is a release
/// blocker. Phase 0 proves isolation at the declared-ownership level; Phase 2 adds
/// runtime negative tests (API, EF query filters, cache keys, events, jobs) to this
/// suite using the same one-fact-per-invariant convention.
/// </summary>
public class TenantIsolationInvariants
{
    [Fact]
    public void No_database_schema_is_owned_by_more_than_one_module()
    {
        var root = FindRoot();
        var manifests = ModuleManifest.LoadAll(root);

        Assert.DoesNotContain(ModuleGraph.Validate(manifests),
            e => e.StartsWith("schema ", StringComparison.Ordinal));
    }

    private static string FindRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Forge.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Forge.slnx not found");
    }
}
