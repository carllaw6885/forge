using System.Text.Json;
using Forge.Tenancy;

namespace Forge.Storage;

/// <summary>
/// Local reference provider (ADR 14): content and a metadata sidecar per blob,
/// under tenant-scoped directories. Reads deny cross-tenant access and refuse
/// anything not scanned clean.
/// </summary>
public sealed class FileSystemBlobStore(string directory, ICurrentTenant tenant) : IBlobStore
{
    private string TenantDirectory(string? tenantId) =>
        Path.Combine(directory, tenantId ?? "_host");

    private string ContentPath(string? tenantId, string id) => Path.Combine(TenantDirectory(tenantId), id + ".bin");

    private string MetadataPath(string? tenantId, string id) => Path.Combine(TenantDirectory(tenantId), id + ".json");

    public async Task<StoredBlob> WriteAsync(StoredBlob metadata, Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(TenantDirectory(metadata.TenantId));
        await using (var file = File.Create(ContentPath(metadata.TenantId, metadata.Id)))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        await File.WriteAllTextAsync(
            MetadataPath(metadata.TenantId, metadata.Id), JsonSerializer.Serialize(metadata), cancellationToken);
        return metadata;
    }

    public async Task<StoredBlob?> GetMetadataAsync(string id, CancellationToken cancellationToken)
    {
        // tenant-scoped lookup: only the current tenant's directory is consulted
        var path = MetadataPath(tenant.Id, id);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<StoredBlob>(await File.ReadAllTextAsync(path, cancellationToken));
    }

    public async Task SetStateAsync(string id, QuarantineState state, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(id, cancellationToken)
            ?? throw new FileNotFoundException($"blob '{id}' not found for current tenant");
        await File.WriteAllTextAsync(
            MetadataPath(tenant.Id, id), JsonSerializer.Serialize(metadata with { State = state }), cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(string id, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(id, cancellationToken)
            ?? throw new FileNotFoundException($"blob '{id}' not found for current tenant");
        if (metadata.State != QuarantineState.Clean)
        {
            throw new InvalidOperationException($"blob '{id}' is {metadata.State} — quarantined content is never served (ADR 14)");
        }

        return File.OpenRead(ContentPath(tenant.Id, id));
    }
}
