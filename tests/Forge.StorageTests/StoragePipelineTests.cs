using System.Security.Cryptography;
using System.Text;
using Forge.Core.Privacy;
using Forge.Storage;
using Forge.Tenancy;
using Xunit;

namespace Forge.StorageTests;

public class StoragePipelineTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("forge-storage").FullName;
    private readonly CurrentTenant _tenant = new();

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private StoragePipeline BuildPipeline(IMalwareScanner? scanner = null, StorageOptions? options = null) =>
        new(new FileSystemBlobStore(_dir, _tenant),
            scanner ?? new DeterministicFakeScanner(),
            options ?? new StorageOptions(),
            _tenant,
            TimeProvider.System);

    private static MemoryStream Content(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task Clean_upload_is_promoted_with_hash_classification_and_tenant()
    {
        _tenant.SetTenant("t1");
        var pipeline = BuildPipeline();
        var ct = TestContext.Current.CancellationToken;

        var blob = await pipeline.UploadAsync("doc.txt", "text/plain", DataClassification.Personal, Content("hello"), ct);

        Assert.Equal(QuarantineState.Clean, blob.State);
        Assert.Equal("t1", blob.TenantId);
        Assert.Equal(DataClassification.Personal, blob.Classification);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("hello"))), blob.Sha256);

        var store = new FileSystemBlobStore(_dir, _tenant);
        using var reader = new StreamReader(await store.OpenReadAsync(blob.Id, ct));
        Assert.Equal("hello", await reader.ReadToEndAsync(ct));
    }

    [Fact]
    public async Task Eicar_content_is_rejected_and_never_served()
    {
        _tenant.SetTenant("t1");
        var pipeline = BuildPipeline();
        var ct = TestContext.Current.CancellationToken;

        var blob = await pipeline.UploadAsync(
            "malware.txt", "text/plain", DataClassification.Internal,
            Content($"prefix {DeterministicFakeScanner.EicarSignature} suffix"), ct);

        Assert.Equal(QuarantineState.Rejected, blob.State);
        var store = new FileSystemBlobStore(_dir, _tenant);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => store.OpenReadAsync(blob.Id, ct));
        Assert.Contains("quarantined content is never served", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Blob_is_quarantined_before_the_scan_runs()
    {
        _tenant.SetTenant("t1");
        QuarantineState? observed = null;
        var store = new FileSystemBlobStore(_dir, _tenant);
        var scanner = new CallbackScanner(_dir, async id =>
        {
            observed = (await store.GetMetadataAsync(id, CancellationToken.None))?.State;
        });
        var pipeline = new StoragePipeline(store, scanner, new StorageOptions(), _tenant, TimeProvider.System);

        var blob = await pipeline.UploadAsync(
            "q.txt", "text/plain", DataClassification.Internal, Content("quarantine-check"),
            TestContext.Current.CancellationToken);

        Assert.Equal(QuarantineState.Quarantined, observed); // stored before trusted
        Assert.Equal(QuarantineState.Clean, blob.State);
    }

    private sealed class CallbackScanner(string dir, Func<string, Task> onScan) : IMalwareScanner
    {
        public async Task<bool> IsCleanAsync(Stream content, CancellationToken ct)
        {
            // the just-written blob's sidecar is the only .json in this test's directory
            var sidecar = Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).Single();
            await onScan(Path.GetFileNameWithoutExtension(sidecar));
            return true;
        }
    }

    [Fact]
    public async Task Oversize_and_disallowed_type_are_refused()
    {
        _tenant.SetTenant("t1");
        var pipeline = BuildPipeline(options: new StorageOptions { MaxSizeBytes = 8 });
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pipeline.UploadAsync("big.txt", "text/plain", DataClassification.Public, Content("way too large"), ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pipeline.UploadAsync("app.exe", "application/x-msdownload", DataClassification.Public, Content("x"), ct));
    }

    [Fact]
    public async Task Cross_tenant_reads_are_denied()
    {
        _tenant.SetTenant("t1");
        var pipeline = BuildPipeline();
        var ct = TestContext.Current.CancellationToken;
        var blob = await pipeline.UploadAsync("mine.txt", "text/plain", DataClassification.Internal, Content("private"), ct);

        _tenant.SetTenant("t2");
        var store = new FileSystemBlobStore(_dir, _tenant);
        Assert.Null(await store.GetMetadataAsync(blob.Id, ct));
        await Assert.ThrowsAsync<FileNotFoundException>(() => store.OpenReadAsync(blob.Id, ct));
    }

    [Fact]
    public void Access_tokens_are_time_limited_and_tamper_evident()
    {
        var secret = "storage-access-secret-key"u8.ToArray();
        var clock = TimeProvider.System;
        var token = StorageAccessTokens.Create("blob1", clock.GetUtcNow().AddMinutes(5), secret);

        Assert.True(StorageAccessTokens.Validate(token, "blob1", clock, secret));
        Assert.False(StorageAccessTokens.Validate(token, "blob2", clock, secret)); // different blob
        Assert.False(StorageAccessTokens.Validate(token.Replace("blob1", "blob1x", StringComparison.Ordinal), "blob1x", clock, secret)); // tampered
        var expired = StorageAccessTokens.Create("blob1", clock.GetUtcNow().AddMinutes(-1), secret);
        Assert.False(StorageAccessTokens.Validate(expired, "blob1", clock, secret)); // expired
    }
}
