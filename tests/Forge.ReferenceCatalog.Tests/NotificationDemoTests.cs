using System.Net.Http.Json;
using Forge.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.ReferenceCatalog.Tests;

/// <summary>ADR 11 slice demonstration: create -> domain event -> constrained template -> in-app inbox, preferences honoured.</summary>
public class NotificationDemoTests(SliceFixture fx) : IClassFixture<SliceFixture>
{
    private void RequireServer() =>
        Assert.SkipWhen(fx.UnavailableReason is not null, $"SQL Server container unavailable: {fx.UnavailableReason}");

    private async Task CreateAsync(string name, string createdBy, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/catalog/items/")
        {
            Content = JsonContent.Create(new { name, createdBy }),
        };
        request.Headers.Add("X-Tenant", "tenant-n1");
        (await fx.Client.SendAsync(request, ct)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Creation_notifies_the_creator_through_the_constrained_template()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        await CreateAsync("Anvil", "carol", ct);

        var inbox = fx.App!.Services.GetRequiredService<InAppChannel>().InboxOf("carol");
        Assert.Contains("Your catalogue item Anvil was created.", inbox);

        var state = await fx.App.Services.GetRequiredService<IDeliveryStateStore>().ListForAsync("carol", ct);
        Assert.Contains(state, r => r.State == DeliveryState.Delivered && r.TenantId == "tenant-n1");
    }

    [Fact]
    public async Task Opted_out_creator_is_not_notified_but_suppression_is_recorded()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;
        await fx.App!.Services.GetRequiredService<INotificationPreferences>()
            .SetOptOutAsync("dave", "catalog.item-created", true, ct);

        await CreateAsync("Hammer", "dave", ct);

        Assert.Empty(fx.App.Services.GetRequiredService<InAppChannel>().InboxOf("dave"));
        var state = await fx.App.Services.GetRequiredService<IDeliveryStateStore>().ListForAsync("dave", ct);
        Assert.Contains(state, r => r.State == DeliveryState.Suppressed);
    }
}
