using System.CommandLine;
using Forge.Core.Modules;

namespace Forge.Cli;

/// <summary>
/// Deterministic command host (ADR 23): stable ordering, invariant culture,
/// no timestamps or machine-specific paths in output. Exit code 0 = success,
/// 1 = validation failure, 2 = usage error (System.CommandLine default).
/// </summary>
public static class ForgeCli
{
    public static RootCommand Build()
    {
        var rootOption = new Option<string>("--root")
        {
            Description = "Repository root to inspect (default: current directory).",
            DefaultValueFactory = _ => ".",
            Recursive = true,
        };
        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report what a mutating command would do without doing it.",
            Recursive = true,
        };

        var root = new RootCommand("Forge developer CLI. Transparent, deterministic and idempotent.");
        root.Options.Add(rootOption);
        root.Options.Add(dryRunOption);

        var modules = new Command("modules", "Inspect and validate module manifests (forge-module.json).");

        var list = new Command("list", "List modules in id order.");
        list.SetAction(pr => WithManifests(pr, rootOption, ms =>
        {
            foreach (var m in ms.OrderBy(m => m.Id, StringComparer.Ordinal))
            {
                Console.WriteLine($"{m.Id} {m.Version}");
            }

            return 0;
        }));

        var graph = new Command("graph", "Print the module dependency graph as 'module -> dependency' lines.");
        graph.SetAction(pr => WithManifests(pr, rootOption, ms =>
        {
            foreach (var m in ms.OrderBy(m => m.Id, StringComparer.Ordinal))
            {
                if (m.Dependencies.Count == 0)
                {
                    Console.WriteLine(m.Id);
                    continue;
                }

                foreach (var dep in m.Dependencies.Order(StringComparer.Ordinal))
                {
                    Console.WriteLine($"{m.Id} -> {dep}");
                }
            }

            return 0;
        }));

        var validate = new Command("validate", "Validate the module graph: ids, dependencies, cycles, schema ownership.");
        validate.SetAction(pr => WithManifests(pr, rootOption, ms =>
        {
            var errors = ModuleGraph.Validate(ms);
            foreach (var error in errors)
            {
                Console.Error.WriteLine($"error: {error}");
            }

            if (errors.Count == 0)
            {
                Console.WriteLine($"ok: {ms.Count} module(s), graph valid");
            }

            return errors.Count == 0 ? 0 : 1;
        }));

        modules.Subcommands.Add(list);
        modules.Subcommands.Add(graph);
        modules.Subcommands.Add(validate);
        root.Subcommands.Add(modules);

        var doctor = new Command("doctor", "Check the repository against Forge conventions.");
        doctor.SetAction(pr =>
        {
            var repoRoot = pr.GetValue(rootOption)!;
            var failed = false;

            void Check(string name, bool ok)
            {
                Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}");
                failed |= !ok;
            }

            Check("solution file (*.slnx)", Directory.EnumerateFiles(repoRoot, "*.slnx").Any());
            Check("Directory.Build.props", File.Exists(Path.Combine(repoRoot, "Directory.Build.props")));
            Check("Directory.Packages.props", File.Exists(Path.Combine(repoRoot, "Directory.Packages.props")));
            Check("tool manifest (.config/dotnet-tools.json)", File.Exists(Path.Combine(repoRoot, ".config", "dotnet-tools.json")));
            Check("module manifests valid", ModuleGraph.Validate(ModuleManifest.LoadAll(repoRoot)).Count == 0);

            return failed ? 1 : 0;
        });
        root.Subcommands.Add(doctor);

        return root;
    }

    private static int WithManifests(ParseResult pr, Option<string> rootOption, Func<IReadOnlyList<ModuleManifest>, int> run)
    {
        var repoRoot = pr.GetValue(rootOption)!;
        if (!Directory.Exists(repoRoot))
        {
            Console.Error.WriteLine($"error: root '{repoRoot}' does not exist");
            return 1;
        }

        return run(ModuleManifest.LoadAll(repoRoot));
    }
}
