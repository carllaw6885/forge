using System.Text;
using Forge.Auditing;
using Xunit;

namespace Forge.AuditingTests;

public class ImmutableEvidenceAndExportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("forge-evidence").FullName;

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_dir))
        {
            File.SetAttributes(file, FileAttributes.Normal); // clear read-only so cleanup works
        }

        Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Evidence_is_write_once()
    {
        var store = new FileImmutableEvidenceStore(_dir);
        var ct = TestContext.Current.CancellationToken;

        await store.WriteAsync("export-1", Encoding.UTF8.GetBytes("original"), ct);

        await Assert.ThrowsAsync<IOException>(() =>
            store.WriteAsync("export-1", Encoding.UTF8.GetBytes("overwrite"), ct));
        Assert.Equal("original", Encoding.UTF8.GetString(await store.ReadAsync("export-1", ct)));
    }

    [Fact]
    public async Task Export_writes_verifiable_evidence_and_audits_itself()
    {
        var ct = TestContext.Current.CancellationToken;
        var audit = new InMemoryAuditStore(new DefaultAuditRedactionPolicy());
        await audit.AppendAsync(new AuditEvent
        {
            Action = "a.1",
            TenantId = "tenant-a",
            Actor = "system",
            CorrelationId = "c1",
            Subject = "s1",
            Outcome = "success",
            OccurredAt = DateTimeOffset.UnixEpoch,
        }, ct);

        var evidence = new FileImmutableEvidenceStore(_dir);
        var exporter = new AuditExporter(audit, evidence, TimeProvider.System);
        await exporter.ExportAsync("export-1", actor: "operator", correlationId: "c2", ct);

        // exported chain verifies
        var exported = AuditExportReader.Parse(Encoding.UTF8.GetString(await evidence.ReadAsync("export-1", ct)));
        Assert.Empty(AuditChainVerifier.Verify(exported));

        // the export itself became evidence in the live trail
        var records = await audit.ReadAllAsync(ct);
        var exportEvent = Assert.Single(records.Select(r => r.Event), e => e.Action == AuditActions.Exported);
        Assert.Equal("export-1", exportEvent.Subject);
        Assert.Equal("operator", exportEvent.Actor);
    }
}
