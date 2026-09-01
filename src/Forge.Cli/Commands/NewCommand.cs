using System.Globalization;
using System.Reflection;

namespace Forge.Cli.Commands;

/// <summary>
/// forge new (ADR 23): materialises the embedded reference template as
/// ordinary, inspectable source. Deterministic and idempotent — it refuses a
/// non-empty target instead of overwriting.
/// </summary>
public static class NewCommand
{
    private const string ResourcePrefix = "Forge.Cli.templates.";

    private const string AdminPrefix = "admin.";

    /// <summary>The CLI's own package version — generated apps pin the matching ForgeStack.* packages.</summary>
    public static string ForgeVersion { get; } =
        typeof(NewCommand).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Split('+')[0];

    public static int Run(string name, string outputDirectory, bool admin = false)
    {
        if (!name.All(c => char.IsAsciiLetterOrDigit(c)) || name.Length == 0 || !char.IsAsciiLetter(name[0]))
        {
            Console.Error.WriteLine($"error: '{name}' is not a valid project name (ascii letters/digits, starting with a letter)");
            return 1;
        }

        var target = Path.Combine(outputDirectory, name);
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
        {
            Console.Error.WriteLine($"error: '{target}' already exists and is not empty");
            return 1;
        }

        var assembly = Assembly.GetExecutingAssembly();
        // base template first, then --admin variants override files at the same path
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(r => r.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                     .OrderBy(r => r[ResourcePrefix.Length..].StartsWith(AdminPrefix, StringComparison.Ordinal))
                     .ThenBy(r => r, StringComparer.Ordinal))
        {
            var isAdmin = resource[ResourcePrefix.Length..].StartsWith(AdminPrefix, StringComparison.Ordinal);
            if (isAdmin && !admin)
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            files[ResourceToPath(resource, name)] = reader.ReadToEnd()
                .Replace("{{NAME}}", name, StringComparison.Ordinal)
                .Replace("{{NAME_LOWER}}", name.ToLower(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{{FORGE_VERSION}}", ForgeVersion, StringComparison.Ordinal);
        }

        foreach (var (relativePath, content) in files)
        {
            var fullPath = Path.Combine(target, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            Console.WriteLine($"created {relativePath.Replace(Path.DirectorySeparatorChar, '/')}");
        }

        var written = files.Keys;

        Console.WriteLine($"ok: {name} generated ({written.Count} files). Next: dotnet run --project {name}/src/{name}.AppHost");
        return 0;
    }

    /// <summary>Explicit resource-to-path manifest: flattened resource names cannot be reversed reliably.</summary>
    private static readonly Dictionary<string, string> Manifest = new(StringComparer.Ordinal)
    {
        ["__NAME__.slnx"] = "{{NAME}}.slnx",
        ["Directory.Build.props"] = "Directory.Build.props",
        ["README.md"] = "README.md",
        ["src.__NAME__.Api.__NAME__.Api.csproj"] = "src/{{NAME}}.Api/{{NAME}}.Api.csproj",
        ["src.__NAME__.Api.Program.cs"] = "src/{{NAME}}.Api/Program.cs",
        ["src.__NAME__.DbMigrator.__NAME__.DbMigrator.csproj"] = "src/{{NAME}}.DbMigrator/{{NAME}}.DbMigrator.csproj",
        ["src.__NAME__.DbMigrator.Program.cs"] = "src/{{NAME}}.DbMigrator/Program.cs",
        ["src.__NAME__.AppHost.__NAME__.AppHost.csproj"] = "src/{{NAME}}.AppHost/{{NAME}}.AppHost.csproj",
        ["src.__NAME__.AppHost.Program.cs"] = "src/{{NAME}}.AppHost/Program.cs",
        ["modules.Notes.__NAME__.Notes.__NAME__.Notes.csproj"] = "modules/Notes/{{NAME}}.Notes/{{NAME}}.Notes.csproj",
        ["modules.Notes.__NAME__.Notes.forge-module.json"] = "modules/Notes/{{NAME}}.Notes/forge-module.json",
        ["modules.Notes.__NAME__.Notes.NotesModule.cs"] = "modules/Notes/{{NAME}}.Notes/NotesModule.cs",
        // --admin variants: same target paths, richer content (Identity + admin shell)
        ["admin.src.__NAME__.Api.__NAME__.Api.csproj"] = "src/{{NAME}}.Api/{{NAME}}.Api.csproj",
        ["admin.src.__NAME__.Api.Program.cs"] = "src/{{NAME}}.Api/Program.cs",
        ["admin.src.__NAME__.DbMigrator.__NAME__.DbMigrator.csproj"] = "src/{{NAME}}.DbMigrator/{{NAME}}.DbMigrator.csproj",
        ["admin.src.__NAME__.DbMigrator.Program.cs"] = "src/{{NAME}}.DbMigrator/Program.cs",
    };

    private static string ResourceToPath(string resource, string name)
    {
        var key = resource[ResourcePrefix.Length..];
        if (!Manifest.TryGetValue(key, out var template))
        {
            throw new InvalidOperationException($"template resource '{key}' has no manifest entry");
        }

        return template.Replace("{{NAME}}", name, StringComparison.Ordinal)
            .Replace('/', Path.DirectorySeparatorChar);
    }
}
