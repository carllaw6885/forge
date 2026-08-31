using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Forge.ReferenceCatalog.Tests;

/// <summary>Observability and health (ADR 15): trace continuity, liveness/readiness distinction, no sensitive leakage.</summary>
public class ObservabilityTests(SliceFixture fx) : IClassFixture<SliceFixture>
{
    private void RequireServer() =>
        Assert.SkipWhen(fx.UnavailableReason is not null, $"SQL Server container unavailable: {fx.UnavailableReason}");

    private static HttpRequestMessage Create(string name)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/catalog/items/")
        {
            Content = JsonContent.Create(new { name }),
        };
        request.Headers.Add("X-Tenant", "tenant-obs");
        return request;
    }

    [Fact]
    public async Task Outbox_dispatch_span_continues_the_http_request_trace()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        var created = await fx.Client.SendAsync(Create("Traced"), ct);
        var item = await created.Content.ReadFromJsonAsync<CatalogItemResponse>(ct);

        Activity? dispatch = null;
        for (var i = 0; i < 100 && dispatch is null; i++)
        {
            dispatch = fx.ExportedActivities.FirstOrDefault(a =>
                a.OperationName == "outbox.dispatch"
                && a.Tags.Any(t => t is { Key: "forge.event_type", Value: "catalog.item.created" })
                && fx.PublishedIntegrationEvents.Any(e =>
                    ((CatalogItemCreated)e.Payload).ItemId == item!.Id
                    && e.CorrelationId.ToString() == a.Tags.First(t => t.Key == "forge.correlation_id").Value));
            if (dispatch is null)
            {
                await Task.Delay(100, ct);
            }
        }

        Assert.NotNull(dispatch);

        var request = fx.ExportedActivities.FirstOrDefault(a =>
            a.OperationName is "Microsoft.AspNetCore.Hosting.HttpRequestIn" or "POST /api/catalog/items/"
            && a.TraceId == dispatch.TraceId);
        Assert.NotNull(request); // HTTP -> EF/outbox continuity: same trace id
        Assert.Equal("tenant-obs", dispatch.Tags.First(t => t.Key == "forge.tenant_id").Value);
    }

    [Fact]
    public async Task Liveness_and_readiness_are_distinct_and_host_scoped()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        // no tenant header on either — host-scoped endpoints
        Assert.Equal(HttpStatusCode.OK, (await fx.Client.GetAsync("/healthz/live", ct)).StatusCode);

        var ready = await fx.Client.GetAsync("/healthz/ready", ct);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode); // includes db:catalog dependency check
    }

    [Fact]
    public async Task Seeded_sensitive_value_does_not_leak_into_span_attributes()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;
        const string sensitive = "S3cr3t-Hunter2-Value";

        await fx.Client.SendAsync(Create(sensitive), ct);
        await Task.Delay(1000, ct); // let outbox/EF spans export

        foreach (var activity in fx.ExportedActivities.ToList())
        {
            foreach (var tag in activity.TagObjects)
            {
                Assert.DoesNotContain(sensitive, tag.Value?.ToString() ?? string.Empty, StringComparison.Ordinal);
            }
        }
    }
}
