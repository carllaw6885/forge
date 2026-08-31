using System.Text;

namespace Forge.Auditing;

/// <summary>
/// Exports the audit trail as JSON Lines into immutable evidence storage, and
/// audits the export itself (ADR 08: retention/export actions are audited).
/// The export file is what `forge audit verify` consumes.
/// </summary>
public sealed class AuditExporter(IAuditStore store, IImmutableEvidenceStore evidence, TimeProvider clock)
{
    public async Task<string> ExportAsync(string exportId, string actor, string correlationId, CancellationToken cancellationToken)
    {
        var records = await store.ReadAllAsync(cancellationToken);

        var lines = new StringBuilder();
        foreach (var record in records.OrderBy(r => r.Sequence))
        {
            lines.AppendLine(System.Text.Json.JsonSerializer.Serialize(record));
        }

        await evidence.WriteAsync(exportId, Encoding.UTF8.GetBytes(lines.ToString()), cancellationToken);

        await store.AppendAsync(new AuditEvent
        {
            Action = AuditActions.Exported,
            TenantId = null,
            Actor = actor,
            CorrelationId = correlationId,
            Subject = exportId,
            Outcome = "success",
            OccurredAt = clock.GetUtcNow(),
            Details = new Dictionary<string, string> { ["recordCount"] = records.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        }, cancellationToken);

        return exportId;
    }
}

/// <summary>Parses a JSON Lines export back into records for verification.</summary>
public static class AuditExportReader
{
    public static IReadOnlyList<AuditRecord> Parse(string jsonLines) =>
        jsonLines.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => System.Text.Json.JsonSerializer.Deserialize<AuditRecord>(line)!)
            .ToList();
}
