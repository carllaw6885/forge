using System.Xml.Linq;

namespace Forge.ArchitectureTests;

/// <summary>
/// Loads the repository's project graph straight from csproj XML. Phase 0 rules
/// operate on declared references; Phase 1 adds compiled-assembly rules for
/// cross-module DbContext and domain-entity access.
/// </summary>
public sealed record ProjectInfo(
    string Name,
    string Path,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> PackageReferences);

public static class RepoModel
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(System.IO.Path.Combine(dir, "Forge.slnx")))
        {
            dir = System.IO.Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Forge.slnx not found above test directory");
    }

    private static readonly string[] SourceDirs = ["src", "modules"];

    public static IReadOnlyList<ProjectInfo> SourceProjects() =>
        SourceDirs
            .Select(d => System.IO.Path.Combine(Root, d))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.csproj", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal)
            .Select(Load)
            .ToList();

    public static ProjectInfo Load(string path)
    {
        var doc = XDocument.Load(path);
        return new ProjectInfo(
            System.IO.Path.GetFileNameWithoutExtension(path),
            path,
            Items(doc, "ProjectReference").Select(System.IO.Path.GetFileNameWithoutExtension).ToList()!,
            Items(doc, "PackageReference").ToList());
    }

    private static IEnumerable<string> Items(XDocument doc, string element) =>
        doc.Descendants(element)
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => v is not null)
            .Select(v => v!)
            .Order(StringComparer.Ordinal);
}
