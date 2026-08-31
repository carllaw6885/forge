using Forge.Cli.Modules;
using Xunit;

namespace Forge.ArchitectureTests;

public class ModuleGraphTests
{
    private static ModuleManifest Manifest(string id, string[]? deps = null, string[]? schemas = null) =>
        new() { Id = id, Name = id, Version = "0.1.0", Dependencies = deps ?? [], OwnedSchemas = schemas ?? [] };

    [Fact]
    public void Repository_manifests_form_a_valid_graph()
    {
        Assert.Empty(ModuleGraph.Validate(ModuleManifest.LoadAll(RepoModel.Root)));
    }

    [Fact]
    public void Cycle_is_detected()
    {
        var errors = ModuleGraph.Validate([Manifest("A", ["B"]), Manifest("B", ["A"])]);
        Assert.Contains(errors, e => e.StartsWith("dependency cycle:", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_dependency_is_detected()
    {
        var errors = ModuleGraph.Validate([Manifest("A", ["Missing"])]);
        Assert.Equal(["module 'A' depends on unknown module 'Missing'"], errors);
    }

    [Fact]
    public void Duplicate_id_is_detected()
    {
        var errors = ModuleGraph.Validate([Manifest("A"), Manifest("A")]);
        Assert.Equal(["duplicate module id 'A'"], errors);
    }

    [Fact]
    public void Shared_schema_ownership_is_detected()
    {
        var errors = ModuleGraph.Validate([Manifest("A", schemas: ["billing"]), Manifest("B", schemas: ["Billing"])]);
        Assert.Equal(["schema 'billing' owned by more than one module: 'A', 'B'"], errors);
    }

    [Fact]
    public void Topological_sort_puts_dependencies_first_and_is_deterministic()
    {
        ModuleManifest[] manifests = [Manifest("C", ["A"]), Manifest("B", ["A"]), Manifest("A")];
        var ordered = ModuleGraph.TopologicalSort(manifests).Select(m => m.Id);
        Assert.Equal(["A", "B", "C"], ordered);
    }

    [Fact]
    public void Topological_sort_rejects_invalid_graph()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ModuleGraph.TopologicalSort([Manifest("A", ["A"])]));
    }
}
