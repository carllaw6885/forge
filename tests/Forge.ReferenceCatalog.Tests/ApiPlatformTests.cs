using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Forge.Web;
using Xunit;

namespace Forge.ReferenceCatalog.Tests;

/// <summary>API platform conventions (ADR 16): opted-in idempotency and the OpenAPI compatibility gate.</summary>
public class ApiPlatformTests(SliceFixture fx) : IClassFixture<SliceFixture>
{
    private void RequireServer() =>
        Assert.SkipWhen(fx.UnavailableReason is not null, $"SQL Server container unavailable: {fx.UnavailableReason}");

    private static HttpRequestMessage Create(string name, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/catalog/items/")
        {
            Content = JsonContent.Create(new { name }),
        };
        request.Headers.Add("X-Tenant", "tenant-idem");
        if (idempotencyKey is not null)
        {
            request.Headers.Add(IdempotencyExtensions.HeaderName, idempotencyKey);
        }

        return request;
    }

    [Fact]
    public async Task Same_idempotency_key_creates_once_and_replays_the_response()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        var first = await fx.Client.SendAsync(Create("Anvil", "key-1"), ct);
        var second = await fx.Client.SendAsync(Create("Anvil", "key-1"), ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal("true", second.Headers.GetValues(IdempotencyExtensions.ReplayedHeaderName).Single());

        var firstItem = await first.Content.ReadFromJsonAsync<CatalogItemResponse>(ct);
        var secondItem = await second.Content.ReadFromJsonAsync<CatalogItemResponse>(ct);
        Assert.Equal(firstItem!.Id, secondItem!.Id); // same response, one item
    }

    [Fact]
    public async Task Different_keys_create_distinct_items()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        var first = await fx.Client.SendAsync(Create("Hammer", "key-a"), ct);
        var second = await fx.Client.SendAsync(Create("Hammer", "key-b"), ct);

        var firstItem = await first.Content.ReadFromJsonAsync<CatalogItemResponse>(ct);
        var secondItem = await second.Content.ReadFromJsonAsync<CatalogItemResponse>(ct);
        Assert.NotEqual(firstItem!.Id, secondItem!.Id);
    }

    [Fact]
    public async Task OpenApi_document_matches_the_committed_snapshot()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        var live = Normalize(await fx.Client.GetStringAsync("/openapi/v1.json", ct));
        var snapshotPath = Path.Combine(FindRepoRoot(), "tests", "Forge.ReferenceCatalog.Tests", "openapi.v1.snapshot.json");

        if (Environment.GetEnvironmentVariable("FORGE_UPDATE_OPENAPI") == "true")
        {
            await File.WriteAllTextAsync(snapshotPath, live, ct);
            return;
        }

        Assert.True(File.Exists(snapshotPath), $"missing snapshot {snapshotPath}; run with FORGE_UPDATE_OPENAPI=true to create it");
        var snapshot = Normalize(await File.ReadAllTextAsync(snapshotPath, ct));
        Assert.True(snapshot == live,
            "OpenAPI document differs from the committed snapshot — a compatibility-relevant change. "
            + "If intentional, regenerate with FORGE_UPDATE_OPENAPI=true and commit the snapshot.");
    }

    /// <summary>Stable formatting with sorted keys so cosmetic generator changes don't false-positive.</summary>
    private static string Normalize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            WriteSorted(writer, doc.RootElement);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteSorted(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteSorted(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteSorted(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
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
