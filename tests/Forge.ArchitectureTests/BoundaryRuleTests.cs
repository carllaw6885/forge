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
        ["Aspire."] = ["Forge.ReferenceSaaS.AppHost"],
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

        static string ModuleOf(ProjectInfo p)
        {
            var parts = p.Path.Split(Path.DirectorySeparatorChar);
            return parts[Array.IndexOf(parts, "modules") + 1];
        }

        var violations =
            from p in moduleProjects
            from r in p.ProjectReferences
            let referenced = moduleProjects.FirstOrDefault(other => other.Name == r)
            where referenced is not null
               && ModuleOf(referenced) != ModuleOf(p)
               && !r.EndsWith(".Contracts", StringComparison.Ordinal)
            select $"{p.Name} references {r}";

        Assert.Empty(violations);
    }

    private static bool IsUi(string name) =>
        name == "Forge.Admin.Blazor" || name.Contains(".Ui.", StringComparison.Ordinal);

    // ADR 40: a capability never depends on its first-party UI (hosts and the CLI may)
    [Fact]
    public void Capabilities_never_reference_ui_packages()
    {
        var violations =
            from p in RepoModel.SourceProjects()
            where !IsUi(p.Name) && !p.Name.Contains("Reference", StringComparison.Ordinal)
                && !p.Name.EndsWith("AppHost", StringComparison.Ordinal) && p.Name != "Forge.Cli"
            from r in p.ProjectReferences
            where IsUi(r)
            select $"{p.Name} references {r}";

        Assert.Empty(violations);
    }

    // ADR 40: first-party UI consumes the module application contract, never stores or managers
    [Fact]
    public void Ui_packages_consume_only_application_contracts()
    {
        string[] forbidden = ["DbContext", "UserManager<", "RoleManager<", "SignInManager<"];
        var violations =
            from p in RepoModel.SourceProjects()
            where IsUi(p.Name)
            from file in Directory.EnumerateFiles(System.IO.Path.GetDirectoryName(p.Path)!, "*.*", SearchOption.AllDirectories)
            where (file.EndsWith(".razor", StringComparison.Ordinal) || file.EndsWith(".cs", StringComparison.Ordinal))
                && !file.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            let text = File.ReadAllText(file)
            from token in forbidden
            where text.Contains(token, StringComparison.Ordinal)
            select $"{System.IO.Path.GetRelativePath(RepoModel.Root, file)} uses {token}";

        Assert.Empty(violations);
    }

    // ADR 40: the shell hosts module UI packages, it never depends on one — removing
    // ForgeStack.Identity.Ui.Blazor leaves the shell (minus its identity pages) intact
    [Fact]
    public void Admin_shell_owns_no_module_ui()
    {
        var shell = RepoModel.SourceProjects().Single(p => p.Name == "Forge.Admin.Blazor");
        Assert.DoesNotContain(shell.ProjectReferences, r => r.Contains(".Ui.", StringComparison.Ordinal));

        var layout = File.ReadAllText(Path.Combine(RepoModel.Root, "src/Forge.Admin.Blazor/Components/Layout/MainLayout.razor"));
        Assert.DoesNotContain("/admin/users", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("/account", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void Core_has_no_package_references_at_all()
    {
        var core = RepoModel.SourceProjects().Single(p => p.Name == "Forge.Core");
        Assert.Empty(core.PackageReferences);
    }

    [Fact]
    public void Modularity_references_only_extensions_abstractions()
    {
        var modularity = RepoModel.SourceProjects().Single(p => p.Name == "Forge.Modularity");
        Assert.All(modularity.PackageReferences, pkg =>
            Assert.StartsWith("Microsoft.Extensions.", pkg, StringComparison.Ordinal));
        Assert.All(modularity.PackageReferences, pkg =>
            Assert.EndsWith(".Abstractions", pkg, StringComparison.Ordinal));
    }

    /// <summary>Every first-party host that maps the admin shell requires a signed-in user (ADR 40).</summary>
    [Fact]
    public void Admin_shell_hosts_require_authorization()
    {
        string[] hosts = ["samples/Forge.ReferenceSaaS.Api", "src/Forge.Cli/templates/admin/src/__NAME__.Api"];

        Assert.All(hosts.Select(dir => Path.Combine(RepoModel.Root, dir, "Program.cs")), program =>
        {
            var map = File.ReadLines(program).Single(l => l.Contains("MapForgeAdminShell(", StringComparison.Ordinal));
            Assert.Contains("RequireAuthorization()", map, StringComparison.Ordinal);
        });
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
