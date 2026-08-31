using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forge.Core.Modules;

/// <summary>
/// A module's declared identity, dependencies and owned database schemas, read
/// from its forge-module.json (schema: eng/module-manifest.schema.json).
/// Inspectable metadata only — never an activation mechanism (ADR 01).
/// </summary>
public sealed record ModuleManifest
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    public IReadOnlyList<string> OwnedSchemas { get; init; } = [];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static ModuleManifest Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ModuleManifest>(stream, Options)
            ?? throw new JsonException($"Empty manifest: {path}");
    }

    /// <summary>Finds every forge-module.json under the given root, ordered by path for determinism.</summary>
    public static IReadOnlyList<ModuleManifest> LoadAll(string root) =>
        Directory.EnumerateFiles(root, "forge-module.json", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Order(StringComparer.Ordinal)
            .Select(Load)
            .ToList();
}
