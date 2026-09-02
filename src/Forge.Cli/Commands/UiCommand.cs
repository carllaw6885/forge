namespace Forge.Cli.Commands;

/// <summary>
/// forge ui|api add|remove (ADR 22/40): attaches or detaches a first-party
/// module projection package — a Blazor UI or a Minimal-API surface — by
/// editing ordinary source: one PackageReference and one line in the host.
/// Idempotent; refuses hosts it cannot recognise rather than guessing.
/// </summary>
public static class UiCommand
{
    private sealed record Projection(string Name, string Package, string Registration, string Comment)
    {
        public string Using => $"using Forge.{Package["ForgeStack.".Length..]};";
        public string RegistrationLine(Family family) => $"{Registration} // {Comment}: `forge {family.Kind} remove {Name}` {family.RemoveVerb}";
    }

    /// <summary>One projection family: where its lines anchor in the admin host, and the modules that offer it.</summary>
    private sealed record Family(string Kind, string ProgramAnchor, string AnchorPackage, string RemoveVerb, Projection[] Catalogue)
    {
        // the whole attribute, so ForgeStack.Identity never matches ForgeStack.Identity.Ui.Blazor
        public string PackageAnchor => $"<PackageReference Include=\"{AnchorPackage}\"";
    }

    // catalogue order is the order the admin template registers them in, so add ∘ remove round-trips byte for byte
    private static readonly Family[] Families =
    [
        new("ui", "builder.Services.AddForgeAdminShell();", "ForgeStack.Admin.Blazor", "goes headless",
        [
            new("identity", "ForgeStack.Identity.Ui.Blazor", "builder.Services.AddForgeIdentityUi();", "sign-in, account, users/roles pages"),
            new("audit", "ForgeStack.Audit.Ui.Blazor", "builder.Services.AddForgeAuditUi();", "audit trail page"),
            new("tenancy", "ForgeStack.Tenancy.Ui.Blazor", "builder.Services.AddForgeTenancyUi();", "tenants page"),
        ]),
        new("api", "app.MapIdentityEndpoints().WithHostScope();", "ForgeStack.Identity", "drops it",
        [
            new("identity", "ForgeStack.Identity.Api", "app.MapForgeIdentityApi();", "bearer-only /api/identity"),
            new("audit", "ForgeStack.Audit.Api", "app.MapForgeAuditApi().WithHostScope();", "bearer-only /api/audit"),
        ]),
    ];

    public static IEnumerable<string> Modules(string kind) => Families.Single(f => f.Kind == kind).Catalogue.Select(p => p.Name);

    public static int Run(string kind, string module, string repoRoot, bool add)
    {
        var family = Families.Single(f => f.Kind == kind);
        var projection = family.Catalogue.FirstOrDefault(m => m.Name == module);
        if (projection is null)
        {
            Console.Error.WriteLine($"error: unknown module {kind} '{module}' (available: {string.Join(", ", Modules(kind))})");
            return 1;
        }

        var program = Directory.EnumerateFiles(repoRoot, "Program.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault(p => File.ReadAllText(p).Contains(family.ProgramAnchor, StringComparison.Ordinal));
        if (program is null)
        {
            Console.Error.WriteLine($"error: no Program.cs under '{repoRoot}' calls {family.ProgramAnchor} — the {module} {kind} needs the Identity host (forge new --admin)");
            return 1;
        }

        var csproj = Directory.EnumerateFiles(Path.GetDirectoryName(program)!, "*.csproj").Single();
        var lines = File.ReadAllLines(program).ToList();
        var projLines = File.ReadAllLines(csproj).ToList();
        var present = lines.Any(l => l.Contains(projection.Registration, StringComparison.Ordinal));

        if (add == present)
        {
            Console.WriteLine($"ok: {projection.Package} already {(add ? "added" : "removed")}; nothing changed");
            return 0;
        }

        if (add)
        {
            var earlier = family.Catalogue.TakeWhile(m => m != projection).ToList();
            var anchor = lines.FindIndex(l => l.Contains(family.ProgramAnchor, StringComparison.Ordinal));
            var at = anchor;
            while (earlier.Any(m => lines[at + 1].Contains(m.Registration, StringComparison.Ordinal)))
            {
                at++;
            }

            var indent = lines[anchor][..(lines[anchor].Length - lines[anchor].TrimStart().Length)];
            lines.Insert(at + 1, indent + projection.RegistrationLine(family));
            if (!lines.Contains(projection.Using))
            {
                // keep the using block sorted so add ∘ remove is byte-identical to the template
                var after = lines.FindIndex(l => l.StartsWith("using ", StringComparison.Ordinal) && string.CompareOrdinal(l.TrimEnd(';'), projection.Using.TrimEnd(';')) > 0);
                lines.Insert(after < 0 ? lines.FindLastIndex(l => l.StartsWith("using ", StringComparison.Ordinal)) + 1 : after, projection.Using);
            }

            var package = projLines.FindIndex(l => l.Contains(family.PackageAnchor, StringComparison.Ordinal));
            if (package < 0)
            {
                Console.Error.WriteLine($"error: '{csproj}' has no {family.AnchorPackage} package reference to attach to");
                return 1;
            }

            var reference = projLines[package].Replace(family.AnchorPackage + "\"", projection.Package + "\"", StringComparison.Ordinal);
            while (earlier.Any(m => projLines[package + 1].Contains($"Include=\"{m.Package}\"", StringComparison.Ordinal)))
            {
                package++;
            }

            projLines.Insert(package + 1, reference);
        }
        else
        {
            lines.RemoveAll(l => l.Contains(projection.Registration, StringComparison.Ordinal) || l.Trim() == projection.Using);
            projLines.RemoveAll(l => l.Contains($"Include=\"{projection.Package}\"", StringComparison.Ordinal));
        }

        File.WriteAllText(program, string.Join('\n', lines) + '\n');
        File.WriteAllText(csproj, string.Join('\n', projLines) + '\n');
        Console.WriteLine($"{(add ? "added" : "removed")} {projection.Package}: {Path.GetRelativePath(repoRoot, csproj)}, {Path.GetRelativePath(repoRoot, program)}");
        return 0;
    }
}
