using System.CommandLine;
using Xunit;

namespace Forge.Cli.Tests;

public class CliTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("forge-cli-tests").FullName;

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private void WriteManifest(string dir, string json)
    {
        var path = Path.Combine(_root, dir);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "forge-module.json"), json);
    }

    private static (int ExitCode, string Out, string Err) Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            var exit = ForgeCli.Build().Parse(args).Invoke();
            return (exit, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void List_orders_by_id_and_is_deterministic()
    {
        WriteManifest("b", """{"id":"Beta","name":"Beta","version":"0.2.0"}""");
        WriteManifest("a", """{"id":"Alpha","name":"Alpha","version":"0.1.0"}""");

        var first = Run("modules", "list", "--root", _root);
        var second = Run("modules", "list", "--root", _root);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal("Alpha 0.1.0\nBeta 0.2.0\n".ReplaceLineEndings(), first.Out.ReplaceLineEndings());
        Assert.Equal(first, second);
    }

    [Fact]
    public void Graph_prints_dependency_edges()
    {
        WriteManifest("a", """{"id":"Alpha","name":"Alpha","version":"0.1.0"}""");
        WriteManifest("b", """{"id":"Beta","name":"Beta","version":"0.1.0","dependencies":["Alpha"]}""");

        var result = Run("modules", "graph", "--root", _root);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Alpha\nBeta -> Alpha\n".ReplaceLineEndings(), result.Out.ReplaceLineEndings());
    }

    [Fact]
    public void Validate_fails_with_stable_error_on_cycle()
    {
        WriteManifest("a", """{"id":"Alpha","name":"Alpha","version":"0.1.0","dependencies":["Beta"]}""");
        WriteManifest("b", """{"id":"Beta","name":"Beta","version":"0.1.0","dependencies":["Alpha"]}""");

        var result = Run("modules", "validate", "--root", _root);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("dependency cycle: Alpha -> Beta -> Alpha", result.Err);
    }

    [Fact]
    public void Validate_reports_ok_for_valid_graph()
    {
        WriteManifest("a", """{"id":"Alpha","name":"Alpha","version":"0.1.0"}""");

        var result = Run("modules", "validate", "--root", _root);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("ok: 1 module(s), graph valid\n".ReplaceLineEndings(), result.Out.ReplaceLineEndings());
    }

    [Fact]
    public void Missing_root_fails_with_exit_code_1()
    {
        var result = Run("modules", "list", "--root", Path.Combine(_root, "nope"));

        Assert.Equal(1, result.ExitCode);
        Assert.StartsWith("error:", result.Err, StringComparison.Ordinal);
    }

    [Fact]
    public void Doctor_passes_on_conforming_repo_and_fails_on_empty_dir()
    {
        var repoRoot = FindRepoRoot();

        Assert.Equal(0, Run("doctor", "--root", repoRoot).ExitCode);

        var empty = Run("doctor", "--root", _root);
        Assert.Equal(1, empty.ExitCode);
        Assert.Contains("FAIL", empty.Out);
    }

    [Fact]
    public async Task Audit_verify_passes_intact_export_and_fails_tampered_one()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new Forge.Auditing.InMemoryAuditStore(new Forge.Auditing.DefaultAuditRedactionPolicy());
        await store.AppendAsync(new Forge.Auditing.AuditEvent
        {
            Action = "a.1",
            TenantId = null,
            Actor = "system",
            CorrelationId = "c1",
            Subject = "s1",
            Outcome = "success",
            OccurredAt = DateTimeOffset.UnixEpoch,
        }, ct);
        var records = await store.ReadAllAsync(ct);
        var export = Path.Combine(_root, "export.jsonl");
        await File.WriteAllLinesAsync(export,
            records.Select(r => System.Text.Json.JsonSerializer.Serialize(r)), ct);

        var ok = Run("audit", "verify", export);
        Assert.Equal(0, ok.ExitCode);
        Assert.Contains("chain intact", ok.Out);

        await File.WriteAllTextAsync(export,
            (await File.ReadAllTextAsync(export, ct)).Replace("success", "denied", StringComparison.Ordinal), ct);
        var tampered = Run("audit", "verify", export);
        Assert.Equal(1, tampered.ExitCode);
        Assert.Contains("tampered", tampered.Err);
    }

    [Fact]
    public void New_generates_a_deterministic_valid_solution_and_refuses_non_empty_targets()
    {
        var genA = Path.Combine(_root, "genA");
        var genB = Path.Combine(_root, "genB");
        Directory.CreateDirectory(genA);
        Directory.CreateDirectory(genB);

        Assert.Equal(0, Run("new", "Acme", "--root", genA).ExitCode);
        Assert.Equal(0, Run("new", "Acme", "--root", genB).ExitCode);

        // deterministic: two generations are byte-identical
        var filesA = Directory.EnumerateFiles(genA, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(genA, f)).Order(StringComparer.Ordinal).ToList();
        var filesB = Directory.EnumerateFiles(genB, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(genB, f)).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(filesA, filesB);
        foreach (var relative in filesA)
        {
            Assert.Equal(File.ReadAllText(Path.Combine(genA, relative)), File.ReadAllText(Path.Combine(genB, relative)));
        }

        Assert.Equal(12, filesA.Count);

        // generated apps pin the CLI's own package version, never a stale hardcoded one
        var props = File.ReadAllText(Path.Combine(genA, "Acme", "Directory.Build.props"));
        Assert.Contains($"<ForgeVersion>{Commands.NewCommand.ForgeVersion}</ForgeVersion>", props, StringComparison.Ordinal);

        // the AppHost declares an http(s) endpoint — WithExternalHttpEndpoints alone
        // creates none and the api resource would run with no URL
        var appHost = File.ReadAllText(Path.Combine(genA, "Acme", "src", "Acme.AppHost", "Program.cs"));
        Assert.Contains(".WithHttpsEndpoint()", appHost, StringComparison.Ordinal);
        // no launch profile in the template: without this the api runs as Production
        // under Aspire and production validation refuses dev configuration
        Assert.Contains("\"ASPNETCORE_ENVIRONMENT\", \"Development\"", appHost, StringComparison.Ordinal);

        // the generated module graph validates
        var validate = Run("modules", "validate", "--root", Path.Combine(genA, "Acme"));
        Assert.Equal(0, validate.ExitCode);

        // idempotent: refuses to overwrite
        var again = Run("new", "Acme", "--root", genA);
        Assert.Equal(1, again.ExitCode);
        Assert.Contains("not empty", again.Err, StringComparison.Ordinal);

        // invalid name is rejected before touching disk
        Assert.Equal(1, Run("new", "1bad name", "--root", genA).ExitCode);
    }

    [Fact]
    public void New_with_admin_overrides_api_and_migrator_with_shell_wiring()
    {
        var gen = Path.Combine(_root, "genAdmin");
        Directory.CreateDirectory(gen);
        Assert.Equal(0, Run("new", "Acme", "--root", gen, "--admin").ExitCode);

        // same file set as the base template — admin variants override in place
        Assert.Equal(12, Directory.EnumerateFiles(gen, "*", SearchOption.AllDirectories).Count());

        var api = File.ReadAllText(Path.Combine(gen, "Acme", "src", "Acme.Api", "Program.cs"));
        Assert.Contains("AddForgeAdminShell", api, StringComparison.Ordinal);
        Assert.Contains("new IdentityModule(", api, StringComparison.Ordinal);
        Assert.Contains("MapForgeAdminShell", api, StringComparison.Ordinal);
        // the shell's system pages resolve these at render time (found by driving /admin live)
        Assert.Contains("IImpersonationContext", api, StringComparison.Ordinal);
        Assert.Contains("AddSqlServerAuditStore", api, StringComparison.Ordinal);
        Assert.Contains("ITerminalFailureSink", api, StringComparison.Ordinal);

        var apiProj = File.ReadAllText(Path.Combine(gen, "Acme", "src", "Acme.Api", "Acme.Api.csproj"));
        Assert.Contains("ForgeStack.Admin.Blazor", apiProj, StringComparison.Ordinal);

        var migrator = File.ReadAllText(Path.Combine(gen, "Acme", "src", "Acme.DbMigrator", "Program.cs"));
        Assert.Contains("ForgeIdentityDbContext", migrator, StringComparison.Ordinal);

        // module graph still validates
        Assert.Equal(0, Run("modules", "validate", "--root", Path.Combine(gen, "Acme")).ExitCode);
    }

    [Fact]
    public void Upgrade_check_is_a_deterministic_dry_run()
    {
        var gen = Path.Combine(_root, "genU");
        Directory.CreateDirectory(gen);
        Run("new", "Acme", "--root", gen);

        var first = Run("upgrade", "check", "--root", Path.Combine(gen, "Acme"));
        var second = Run("upgrade", "check", "--root", Path.Combine(gen, "Acme"));

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(first.Out, second.Out);
        Assert.Contains("dry run; nothing changed", first.Out, StringComparison.Ordinal);
        Assert.Contains($"ForgeStack.Modularity {Commands.NewCommand.ForgeVersion}", first.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Db_commands_locate_the_migrator_project()
    {
        var gen = Path.Combine(_root, "genD");
        Directory.CreateDirectory(gen);
        Run("new", "Acme", "--root", gen);

        var migrator = Forge.Cli.Commands.DbCommand.FindMigrator(Path.Combine(gen, "Acme"));
        Assert.NotNull(migrator);
        Assert.EndsWith("Acme.DbMigrator.csproj", migrator, StringComparison.Ordinal);
    }

    [Fact]
    public void Dry_run_option_is_accepted_globally()
    {
        WriteManifest("a", """{"id":"Alpha","name":"Alpha","version":"0.1.0"}""");

        Assert.Equal(0, Run("modules", "validate", "--root", _root, "--dry-run").ExitCode);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Forge.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Forge.slnx not found");
    }
}
