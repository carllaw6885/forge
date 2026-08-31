using Forge.Core.Privacy;

namespace Forge.Storage;

/// <summary>Quarantine lifecycle (ADR 14): every upload starts quarantined; only scanned-clean blobs are readable.</summary>
public enum QuarantineState
{
    Quarantined,
    Clean,
    Rejected,
}

/// <summary>Blob metadata (ADR 14): integrity hash, classification, tenant ownership, quarantine state.</summary>
public sealed record StoredBlob(
    string Id,
    string? TenantId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    DataClassification Classification,
    QuarantineState State,
    DateTimeOffset StoredAt);

/// <summary>
/// Provider-neutral binary storage (ADR 14). Deliberately exposes no URL type:
/// there are no permanent public URLs by construction — access goes through
/// authorized, time-limited paths.
/// </summary>
public interface IBlobStore
{
    Task<StoredBlob> WriteAsync(StoredBlob metadata, Stream content, CancellationToken cancellationToken);

    Task<StoredBlob?> GetMetadataAsync(string id, CancellationToken cancellationToken);

    Task SetStateAsync(string id, QuarantineState state, CancellationToken cancellationToken);

    /// <summary>Opens a blob's content. Implementations must refuse anything not scanned clean.</summary>
    Task<Stream> OpenReadAsync(string id, CancellationToken cancellationToken);
}

/// <summary>Pluggable malware scanning seam (ADR 14). Real engines are adapters; the fake is deterministic for acceptance.</summary>
public interface IMalwareScanner
{
    Task<bool> IsCleanAsync(Stream content, CancellationToken cancellationToken);
}

/// <summary>Deterministic reference scanner: flags content containing the EICAR test signature.</summary>
public sealed class DeterministicFakeScanner : IMalwareScanner
{
    public const string EicarSignature = @"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";

    public async Task<bool> IsCleanAsync(Stream content, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return !text.Contains(EicarSignature, StringComparison.Ordinal);
    }
}

/// <summary>Upload validation limits (ADR 14).</summary>
public sealed record StorageOptions
{
    public long MaxSizeBytes { get; init; } = 10 * 1024 * 1024;
    public IReadOnlyList<string> AllowedContentTypes { get; init; } =
        ["application/pdf", "image/png", "image/jpeg", "text/plain"];
}
