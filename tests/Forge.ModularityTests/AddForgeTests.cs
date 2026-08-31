using Forge.Core.Modules;
using Forge.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.ModularityTests;

public class AddForgeTests
{
    private sealed class RecordingModule(string id, List<string> log, params string[] deps) : IForgeModule
    {
        public ModuleManifest Manifest { get; } = new()
        {
            Id = id,
            Name = id,
            Version = "0.1.0",
            Dependencies = deps,
        };

        public void ConfigureServices(IServiceCollection services) => log.Add($"services:{id}");

        public void ConfigureApplication(IServiceProvider services) => log.Add($"app:{id}");
    }

    [Fact]
    public void Configures_services_in_dependency_order()
    {
        var log = new List<string>();
        new ServiceCollection().AddForge(
            new RecordingModule("Charlie", log, "Alpha"),
            new RecordingModule("Alpha", log),
            new RecordingModule("Bravo", log, "Alpha"));

        Assert.Equal(["services:Alpha", "services:Bravo", "services:Charlie"], log);
    }

    [Fact]
    public void UseForge_configures_application_in_dependency_order()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddForge(
            new RecordingModule("Bravo", log, "Alpha"),
            new RecordingModule("Alpha", log));

        using var provider = services.BuildServiceProvider();
        log.Clear();
        provider.UseForge();

        Assert.Equal(["app:Alpha", "app:Bravo"], log);
    }

    [Fact]
    public void Cycle_fails_fast_with_named_modules()
    {
        var log = new List<string>();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddForge(
                new RecordingModule("Alpha", log, "Bravo"),
                new RecordingModule("Bravo", log, "Alpha")));

        Assert.Contains("dependency cycle: Alpha -> Bravo -> Alpha", ex.Message, StringComparison.Ordinal);
        Assert.Empty(log);
    }

    [Fact]
    public void Duplicate_module_id_fails_fast()
    {
        var log = new List<string>();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddForge(
                new RecordingModule("Alpha", log),
                new RecordingModule("Alpha", log)));

        Assert.Contains("duplicate module id 'Alpha'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_dependency_fails_fast()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddForge(new RecordingModule("Alpha", [], "Missing")));

        Assert.Contains("unknown module 'Missing'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Registers_inspectable_catalog_and_system_time_provider()
    {
        var services = new ServiceCollection();
        services.AddForge(
            new RecordingModule("Bravo", [], "Alpha"),
            new RecordingModule("Alpha", []));

        using var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredService<ModuleCatalog>();
        Assert.Equal(["Alpha", "Bravo"], catalog.Manifests.Select(m => m.Id));
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void Custom_time_provider_registered_first_is_not_overridden()
    {
        var services = new ServiceCollection();
        var custom = new FakeTimeProvider();
        services.AddSingleton<TimeProvider>(custom);
        services.AddForge(new RecordingModule("Alpha", []));

        using var provider = services.BuildServiceProvider();
        Assert.Same(custom, provider.GetRequiredService<TimeProvider>());
    }

    private sealed class FakeTimeProvider : TimeProvider;
}
