namespace Forge.Cli.Commands;

/// <summary>
/// forge ui add|remove (ADR 22/40): attaches or detaches a first-party module UI
/// package by editing ordinary source — one PackageReference and one
/// registration line in the API host. Idempotent; refuses hosts it cannot
/// recognise rather than guessing.
/// </summary>
public static class UiCommand
{
    // ponytail: identity only; a second entry here is the trigger for a table-driven catalogue
    private const string Package = "ForgeStack.Identity.Ui.Blazor";
    private const string Using = "using Forge.Identity.Ui.Blazor;";
    private const string Registration = "builder.Services.AddForgeIdentityUi();";
    private const string RegistrationLine = Registration + " // sign-in, account, users/roles pages: `forge ui remove identity` goes headless";
    private const string ShellAnchor = "builder.Services.AddForgeAdminShell();";
    private const string PackageAnchor = "<PackageReference Include=\"ForgeStack.Admin.Blazor\"";

    public static int Run(string module, string repoRoot, bool add)
    {
        if (module != "identity")
        {
            Console.Error.WriteLine($"error: unknown module ui '{module}' (available: identity)");
            return 1;
        }

        var program = Directory.EnumerateFiles(repoRoot, "Program.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault(p => File.ReadAllText(p).Contains(ShellAnchor, StringComparison.Ordinal));
        if (program is null)
        {
            Console.Error.WriteLine($"error: no Program.cs under '{repoRoot}' calls {ShellAnchor} — the identity UI needs the admin shell (forge new --admin)");
            return 1;
        }

        var csproj = Directory.EnumerateFiles(Path.GetDirectoryName(program)!, "*.csproj").Single();
        var lines = File.ReadAllLines(program).ToList();
        var projLines = File.ReadAllLines(csproj).ToList();
        var present = lines.Any(l => l.Contains(Registration, StringComparison.Ordinal));

        if (add == present)
        {
            Console.WriteLine($"ok: {Package} already {(add ? "added" : "removed")}; nothing changed");
            return 0;
        }

        if (add)
        {
            var shell = lines.FindIndex(l => l.Contains(ShellAnchor, StringComparison.Ordinal));
            lines.Insert(shell + 1, lines[shell][..lines[shell].IndexOf("builder", StringComparison.Ordinal)] + RegistrationLine);
            if (!lines.Contains(Using))
            {
                // keep the using block sorted so add ∘ remove is byte-identical to the template
                var after = lines.FindIndex(l => l.StartsWith("using ", StringComparison.Ordinal) && string.CompareOrdinal(l.TrimEnd(';'), Using.TrimEnd(';')) > 0);
                lines.Insert(after < 0 ? lines.FindLastIndex(l => l.StartsWith("using ", StringComparison.Ordinal)) + 1 : after, Using);
            }

            var anchor = projLines.FindIndex(l => l.Contains(PackageAnchor, StringComparison.Ordinal));
            if (anchor < 0)
            {
                Console.Error.WriteLine($"error: '{csproj}' has no ForgeStack.Admin.Blazor package reference to attach to");
                return 1;
            }

            projLines.Insert(anchor + 1, projLines[anchor].Replace("ForgeStack.Admin.Blazor", Package, StringComparison.Ordinal));
        }
        else
        {
            lines.RemoveAll(l => l.Contains(Registration, StringComparison.Ordinal) || l.Trim() == Using);
            projLines.RemoveAll(l => l.Contains($"Include=\"{Package}\"", StringComparison.Ordinal));
        }

        File.WriteAllText(program, string.Join('\n', lines) + '\n');
        File.WriteAllText(csproj, string.Join('\n', projLines) + '\n');
        Console.WriteLine($"{(add ? "added" : "removed")} {Package}: {Path.GetRelativePath(repoRoot, csproj)}, {Path.GetRelativePath(repoRoot, program)}");
        return 0;
    }
}
