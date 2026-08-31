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
