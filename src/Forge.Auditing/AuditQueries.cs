using Forge.Core.Primitives;

namespace Forge.Auditing;

/// <summary>Stable failure codes returned by the audit application contract.</summary>
public static class AuditErrors
{
    public const string Denied = "audit.denied";
    public const string NoEvidenceStore = "audit.no-evidence-store";
}

/// <summary>
/// A timeline query. Every filter is an exact match on the event field; null
/// means "any". Paging is newest-first by sequence.
/// </summary>
public sealed record AuditQuery(
    string? Actor = null,
    string? Action = null,
    string? Subject = null,
    string? CorrelationId = null,
    long BeforeSequence = long.MaxValue,
    int Take = 50);

/// <summary>Result of walking the chain: intact when <see cref="Errors"/> is empty.</summary>
public sealed record AuditChainStatus(int RecordCount, IReadOnlyList<string> Errors, string? EvidenceStore)
{
    public bool IsIntact => Errors.Count == 0;
}

/// <summary>
/// The audit module's application contract (ADR 40): the single front door for
/// first-party UI, HTTP projections and custom applications. Permission and
/// tenant scope are enforced inside — a tenant-scoped caller sees only its own
/// tenant's evidence; host scope sees the whole trail. Denials, verification
/// and export are themselves audited.
/// </summary>
public interface IAuditQueries
{
    Task<Result<IReadOnlyList<AuditRecord>>> ListAsync(AuditQuery query, CancellationToken cancellationToken);

    /// <summary>Walks the whole chain; host scope only (the chain is cross-tenant evidence).</summary>
    Task<Result<AuditChainStatus>> VerifyAsync(CancellationToken cancellationToken);

    /// <summary>Exports the trail to the host's <see cref="IImmutableEvidenceStore"/>; returns the evidence id. Host scope only.</summary>
    Task<Result<string>> ExportAsync(CancellationToken cancellationToken);
}
