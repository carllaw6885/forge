using System.Security.Cryptography;
using Forge.Core.Privacy;
using Forge.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Forge.Storage;

/// <summary>
/// The upload pipeline (ADR 14): validate size and type, hash, store
/// quarantined, scan, then promote or reject. Content is never trusted before
/// the scan — reads of quarantined or rejected blobs are refused by the store.
/// </summary>
public sealed class StoragePipeline(
    IBlobStore store,
    IMalwareScanner scanner,
    StorageOptions options,
    ICurrentTenant tenant,
    TimeProvider clock)
{
    public async Task<StoredBlob> UploadAsync(
        string fileName,
        string contentType,
        DataClassification classification,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (!options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"content type '{contentType}' is not allowed");
        }

        // buffer with a hard size cap while hashing
        var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > options.MaxSizeBytes)
        {
            throw new InvalidOperationException($"upload of {buffer.Length} bytes exceeds the {options.MaxSizeBytes} byte limit");
        }

        buffer.Position = 0;
        var sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(buffer, cancellationToken));
        buffer.Position = 0;

        var metadata = new StoredBlob(
            Id: Guid.NewGuid().ToString("N"),
            TenantId: tenant.Id,
            FileName: fileName,
            ContentType: contentType,
            SizeBytes: buffer.Length,
            Sha256: sha256,
            Classification: classification,
            State: QuarantineState.Quarantined, // quarantine before trust, always
            StoredAt: clock.GetUtcNow());
        await store.WriteAsync(metadata, buffer, cancellationToken);

        buffer.Position = 0;
        var clean = await scanner.IsCleanAsync(buffer, cancellationToken);
        await store.SetStateAsync(metadata.Id, clean ? QuarantineState.Clean : QuarantineState.Rejected, cancellationToken);

        return (await store.GetMetadataAsync(metadata.Id, cancellationToken))!;
    }
}

/// <summary>DI registration for the storage pipeline with the local reference provider.</summary>
public static class StorageExtensions
{
    public static IServiceCollection AddForgeStorage(
        this IServiceCollection services, string directory, StorageOptions? options = null)
    {
        services.TryAddSingleton(options ?? new StorageOptions());
        services.TryAddSingleton<IMalwareScanner, DeterministicFakeScanner>();
        services.TryAddSingleton<IBlobStore>(sp =>
            new FileSystemBlobStore(directory, sp.GetRequiredService<ICurrentTenant>()));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<StoragePipeline>();
        return services;
    }
}
