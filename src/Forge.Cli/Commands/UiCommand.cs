namespace Forge.Cli.Commands;

/// <summary>
/// forge ui add|remove (ADR 22/40): attaches or detaches a first-party module UI
/// package by editing ordinary source — one PackageReference and one
/// registration line in the API host. Idempotent; refuses hosts it cannot
/// recognise rather than guessing.
/// </summary>
public static class UiCommand
{
    private sealed record ModuleUi(string Name, string Package, string Registration, string Comment)
    {
        public string Using => $"using Forge.{Package["ForgeStack.".Length..]};";
        public string RegistrationLine => $"{Registration} // {Comment}: `forge ui remove {Name}` goes headless";
    }

    // table order is the order the admin template registers them in, so add ∘ remove round-trips byte for byte
    private static readonly ModuleUi[] Catalogue =
    [
        new("identity", "ForgeStack.Identity.Ui.Blazor", "builder.Services.AddForgeIdentityUi();", "sign-in, account, users/roles pages"),
        new("audit", "ForgeStack.Audit.Ui.Blazor", "builder.Services.AddForgeAuditUi();", "audit trail page"),
    ];

    private const string ShellAnchor = "builder.Services.AddForgeAdminShell();";
    private const string PackageAnchor = "<PackageReference Include=\"ForgeStack.Admin.Blazor\"";

    public static int Run(string module, string repoRoot, bool add)
    {
        var ui = Catalogue.FirstOrDefault(m => m.Name == module);
        if (ui is null)
        {
            Console.Error.WriteLine($"error: unknown module ui '{module}' (available: {string.Join(", ", Catalogue.Select(m => m.Name))})");
            return 1;
        }

        var program = Directory.EnumerateFiles(repoRoot, "Program.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault(p => File.ReadAllText(p).Contains(ShellAnchor, StringComparison.Ordinal));
        if (program is null)
        {
            Console.Error.WriteLine($"error: no Program.cs under '{repoRoot}' calls {ShellAnchor} — the {module} UI needs the admin shell (forge new --admin)");
            return 1;
        }

        var csproj = Directory.EnumerateFiles(Path.GetDirectoryName(program)!, "*.csproj").Single();
        var lines = File.ReadAllLines(program).ToList();
        var projLines = File.ReadAllLines(csproj).ToList();
        var present = lines.Any(l => l.Contains(ui.Registration, StringComparison.Ordinal));

        if (add == present)
        {
            Console.WriteLine($"ok: {ui.Package} already {(add ? "added" : "removed")}; nothing changed");
            return 0;
        }

        if (add)
        {
            var earlier = Catalogue.TakeWhile(m => m != ui).ToList();
            var shell = lines.FindIndex(l => l.Contains(ShellAnchor, StringComparison.Ordinal));
            var at = shell;
            while (earlier.Any(m => lines[at + 1].Contains(m.Registration, StringComparison.Ordinal)))
            {
                at++;
            }

            lines.Insert(at + 1, lines[shell][..lines[shell].IndexOf("builder", StringComparison.Ordinal)] + ui.RegistrationLine);
            if (!lines.Contains(ui.Using))
            {
                // keep the using block sorted so add ∘ remove is byte-identical to the template
                var after = lines.FindIndex(l => l.StartsWith("using ", StringComparison.Ordinal) && string.CompareOrdinal(l.TrimEnd(';'), ui.Using.TrimEnd(';')) > 0);
                lines.Insert(after < 0 ? lines.FindLastIndex(l => l.StartsWith("using ", StringComparison.Ordinal)) + 1 : after, ui.Using);
            }

            var anchor = projLines.FindIndex(l => l.Contains(PackageAnchor, StringComparison.Ordinal));
            if (anchor < 0)
            {
                Console.Error.WriteLine($"error: '{csproj}' has no ForgeStack.Admin.Blazor package reference to attach to");
                return 1;
            }

            var reference = projLines[anchor].Replace("ForgeStack.Admin.Blazor", ui.Package, StringComparison.Ordinal);
            while (earlier.Any(m => projLines[anchor + 1].Contains($"Include=\"{m.Package}\"", StringComparison.Ordinal)))
            {
                anchor++;
            }

            projLines.Insert(anchor + 1, reference);
        }
        else
        {
            lines.RemoveAll(l => l.Contains(ui.Registration, StringComparison.Ordinal) || l.Trim() == ui.Using);
            projLines.RemoveAll(l => l.Contains($"Include=\"{ui.Package}\"", StringComparison.Ordinal));
        }

        File.WriteAllText(program, string.Join('\n', lines) + '\n');
        File.WriteAllText(csproj, string.Join('\n', projLines) + '\n');
        Console.WriteLine($"{(add ? "added" : "removed")} {ui.Package}: {Path.GetRelativePath(repoRoot, csproj)}, {Path.GetRelativePath(repoRoot, program)}");
        return 0;
    }
}
