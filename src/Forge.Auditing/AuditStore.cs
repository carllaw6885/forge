using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Forge.Auditing;

/// <summary>
/// One appended, hash-chained audit record. EventJson is the canonical stored
/// form — the hash covers it byte-for-byte, so verification never depends on
/// re-serialization stability.
/// </summary>
public sealed record AuditRecord(long Sequence, string EventJson, string PreviousHash, string Hash)
{
    public AuditEvent Event => JsonSerializer.Deserialize<AuditEvent>(EventJson)!;
}

/// <summary>
/// Append-only audit store (ADR 08). Deliberately has no update or delete
/// members — the contract itself cannot express tampering. Levels of assurance
/// are distinct and must not be conflated: this interface gives append-only
/// semantics; the hash chain gives tamper evidence; storage-enforced WORM is
/// <see cref="IImmutableEvidenceStore"/>'s job.
/// </summary>
public interface IAuditStore
{
    Task<AuditRecord> AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

    /// <summary>The whole trail in sequence order: what chain verification and export need.</summary>
    Task<IReadOnlyList<AuditRecord>> ReadAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Newest-first window: up to <paramref name="take"/> records with Sequence below
    /// <paramref name="beforeSequence"/>. Default walks <see cref="ReadAllAsync"/> so
    /// existing custom stores keep working; real stores override with a ranged query.
    /// </summary>
    async Task<IReadOnlyList<AuditRecord>> ReadLatestAsync(long beforeSequence, int take, CancellationToken cancellationToken) =>
        (await ReadAllAsync(cancellationToken))
            .Where(r => r.Sequence < beforeSequence)
            .OrderByDescending(r => r.Sequence)
            .Take(take)
            .ToList();
}

/// <summary>Hash-chain primitives shared by every store and the verifier.</summary>
public static class AuditChain
{
    /// <summary>The PreviousHash of the first record in a chain.</summary>
    public static readonly string GenesisHash = new('0', 64);

    private static readonly JsonSerializerOptions CanonicalJson = new();

    public static string Serialize(AuditEvent auditEvent, IAuditRedactionPolicy redaction)
    {
        var details = auditEvent.Details.Count == 0
            ? auditEvent.Details
            : auditEvent.Details.ToDictionary(
                kv => kv.Key,
                kv => redaction.IsSensitive(kv.Key) ? DefaultAuditRedactionPolicy.Redacted : kv.Value,
                StringComparer.Ordinal);

        return JsonSerializer.Serialize(auditEvent with { Details = details }, CanonicalJson);
    }

    public static string Hash(string previousHash, string eventJson) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(previousHash + "\n" + eventJson)));
}

/// <summary>Walks a chain and reports every break. Empty result means the trail is intact.</summary>
public static class AuditChainVerifier
{
    public static IReadOnlyList<string> Verify(IReadOnlyList<AuditRecord> records)
    {
        var errors = new List<string>();
        var expectedPrevious = AuditChain.GenesisHash;

        foreach (var record in records.OrderBy(r => r.Sequence))
        {
            if (record.PreviousHash != expectedPrevious)
            {
                errors.Add($"sequence {record.Sequence}: chain break — PreviousHash does not match the prior record's hash");
            }

            if (AuditChain.Hash(record.PreviousHash, record.EventJson) != record.Hash)
            {
                errors.Add($"sequence {record.Sequence}: content tampered — stored hash does not match the record");
            }

            expectedPrevious = record.Hash;
        }

        return errors;
    }
}

/// <summary>Reference in-memory store: hash-chained like every real store; for tests and local hosts.</summary>
public sealed class InMemoryAuditStore(IAuditRedactionPolicy redaction) : IAuditStore
{
    private readonly List<AuditRecord> _records = [];
    private readonly Lock _lock = new();

    public Task<AuditRecord> AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        var json = AuditChain.Serialize(auditEvent, redaction);
        lock (_lock)
        {
            var previous = _records.Count == 0 ? AuditChain.GenesisHash : _records[^1].Hash;
            var record = new AuditRecord(_records.Count + 1, json, previous, AuditChain.Hash(previous, json));
            _records.Add(record);
            return Task.FromResult(record);
        }
    }

    public Task<IReadOnlyList<AuditRecord>> ReadAllAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<AuditRecord>>([.. _records]);
        }
    }
}
