using System.Reflection;
using System.Xml.Linq;

namespace Forge.Cli.Commands;

/// <summary>
/// forge upgrade check --dry-run (ADR 23): compares ForgeStack package pins found in
/// the repository against this CLI's own version. Offline and deterministic —
/// no network, no mutation.
/// </summary>
public static class UpgradeCommand
{
    public static int Check(string root)
    {
        var cliVersion = Assembly.GetExecutingAssembly().GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}.{v.Build}"
            : "unknown";
        Console.WriteLine($"forge cli version: {cliVersion}");

        var pins = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var props in Directory.EnumerateFiles(root, "*.props", SearchOption.TopDirectoryOnly)
                     .Concat(Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
                     .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                              && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                     .Order(StringComparer.Ordinal))
        {
            foreach (var element in XDocument.Load(props).Descendants()
                         .Where(e => e.Name.LocalName is "PackageReference" or "PackageVersion"))
            {
                var include = element.Attribute("Include")?.Value;
                var version = element.Attribute("Version")?.Value;
                if (include is not null && version is not null
                    && include.StartsWith("ForgeStack.", StringComparison.Ordinal))
                {
                    pins.TryAdd(include, version);
                }
            }
        }

        if (pins.Count == 0)
        {
            Console.WriteLine("no Forge package pins found; nothing to upgrade");
            return 0;
        }

        var mismatches = 0;
        foreach (var (package, version) in pins)
        {
            var resolved = version == "$(ForgeVersion)" ? ReadForgeVersionProperty(root) ?? version : version;
            var state = resolved == cliVersion ? "in sync" : $"differs from cli {cliVersion}";
            if (resolved != cliVersion)
            {
                mismatches++;
            }

            Console.WriteLine($"{package} {resolved} — {state}");
        }

        Console.WriteLine(mismatches == 0
            ? "ok: all Forge pins match the cli version (dry run; nothing changed)"
            : $"note: {mismatches} pin(s) differ (dry run; nothing changed)");
        return 0;
    }

    private static string? ReadForgeVersionProperty(string root)
    {
        var props = Path.Combine(root, "Directory.Build.props");
        return File.Exists(props)
            ? XDocument.Load(props).Descendants("ForgeVersion").FirstOrDefault()?.Value
            : null;
    }
}
