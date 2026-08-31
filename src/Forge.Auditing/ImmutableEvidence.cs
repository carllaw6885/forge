namespace Forge.Auditing;

/// <summary>
/// Storage-enforced write-once evidence (ADR 08): a stored item can never be
/// rewritten through this contract. Cloud WORM providers implement this
/// post-v0.1; the local reference proves the semantics.
/// </summary>
public interface IImmutableEvidenceStore
{
    /// <summary>Writes once; a second write to the same id must throw.</summary>
    Task WriteAsync(string id, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    Task<byte[]> ReadAsync(string id, CancellationToken cancellationToken);
}

/// <summary>
/// Local reference provider: FileMode.CreateNew guarantees write-once at the
/// filesystem API level, and the file is marked read-only after writing.
/// </summary>
public sealed class FileImmutableEvidenceStore(string directory) : IImmutableEvidenceStore
{
    private string PathFor(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.')))
        {
            throw new ArgumentException($"invalid evidence id '{id}'", nameof(id));
        }

        return System.IO.Path.Combine(directory, id);
    }

    public async Task WriteAsync(string id, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var path = PathFor(id);
        await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        {
            await stream.WriteAsync(content, cancellationToken);
        }

        File.SetAttributes(path, FileAttributes.ReadOnly);
    }

    public Task<byte[]> ReadAsync(string id, CancellationToken cancellationToken) =>
        File.ReadAllBytesAsync(PathFor(id), cancellationToken);
}
