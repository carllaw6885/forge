using Xunit;

namespace Forge.ArchitectureTests;

/// <summary>
/// Declared-reference boundary rules (ADRs 01, 03, 21, 30). These run against
/// every project under src/ and modules/, so a violating project fails the
/// moment it is added.
/// </summary>
public class BoundaryRuleTests
{
    // A reference provider or UI package may only appear in the package that owns it
    // (prefix match on project name). Core packages stay free of provider/UI pulls.
    private static readonly Dictionary<string, string[]> PackageOwners = new()
    {
        ["Microsoft.EntityFrameworkCore.SqlServer"] = ["Forge.Persistence.SqlServer"],
        ["Quartz"] = ["Forge.Jobs.Quartz"],
        ["OpenIddict"] = ["Forge.Identity"],
        ["StackExchange.Redis"] = ["Forge.Caching.Redis"],
        ["Microsoft.AspNetCore.Components"] = ["Forge.Admin.Blazor"],
        ["Aspire."] = ["Forge.AppHost"],
    };

    [Fact]
    public void Provider_and_ui_packages_stay_in_their_owning_project()
    {
        var violations =
            from p in RepoModel.SourceProjects()
            from pkg in p.PackageReferences
            from rule in PackageOwners
            where pkg.StartsWith(rule.Key, StringComparison.Ordinal)
               && !rule.Value.Any(owner => p.Name.StartsWith(owner, StringComparison.Ordinal))
            select $"{p.Name} references {pkg}";

        Assert.Empty(violations);
    }

    [Fact]
    public void Modules_do_not_reference_other_modules_except_contracts()
    {
        var moduleProjects = RepoModel.SourceProjects()
            .Where(p => p.Path.Contains($"{Path.DirectorySeparatorChar}modules{Path.DirectorySeparatorChar}"))
            .ToList();

        string ModuleOf(string projectName) => projectName.Split('.')[0];

        var violations =
            from p in moduleProjects
            from r in p.ProjectReferences
            where moduleProjects.Any(other => other.Name == r)
               && ModuleOf(r) != ModuleOf(p.Name)
               && !r.EndsWith(".Contracts", StringComparison.Ordinal)
            select $"{p.Name} references {r}";

        Assert.Empty(violations);
    }

    [Fact]
    public void Cli_stays_free_of_web_ui_and_ef_dependencies()
    {
        var cli = RepoModel.SourceProjects().Single(p => p.Name == "Forge.Cli");
        string[] forbidden = ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore"];

        Assert.DoesNotContain(cli.PackageReferences, pkg =>
            forbidden.Any(f => pkg.StartsWith(f, StringComparison.Ordinal)));
    }
}
