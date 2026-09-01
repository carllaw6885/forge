using System.Diagnostics;

namespace Forge.Cli.Commands;

/// <summary>
/// forge db (ADR 23): orchestrates the solution's independent DbMigrator via
/// `dotnet run` — migrations stay owned by the migrator, and the CLI stays free
/// of EF (enforced by its own boundary test).
/// </summary>
public static class DbCommand
{
    public static string? FindMigrator(string root) =>
        Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*DbMigrator*.csproj", SearchOption.AllDirectories)
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Order(StringComparer.Ordinal)
                .FirstOrDefault()
            : null;

    public static int Run(string root, string migratorCommand)
    {
        var migrator = FindMigrator(root);
        if (migrator is null)
        {
            Console.Error.WriteLine("error: no *DbMigrator*.csproj found under the root");
            return 1;
        }

        Console.WriteLine($"using migrator: {Path.GetRelativePath(root, migrator).Replace(Path.DirectorySeparatorChar, '/')}");
        using var process = Process.Start(new ProcessStartInfo("dotnet")
        {
            ArgumentList = { "run", "--project", migrator, "--", migratorCommand },
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        })!;
        process.WaitForExit();
        return process.ExitCode;
    }
}
