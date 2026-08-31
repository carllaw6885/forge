using Forge.Auditing;
using Forge.Notifications;
using Forge.Templates;
using Xunit;

namespace Forge.NotificationsTests;

public class NotificationServiceTests
{
    private static readonly Template ItemCreated = new(
        "catalog.item-created", 1,
        new Dictionary<string, string> { [""] = "Item {{item}} was created." },
        new HashSet<string> { "item" });

    private sealed record Harness(
        NotificationService Service, InAppChannel Channel, InMemoryNotificationPreferences Preferences,
        InMemoryDeliveryStateStore DeliveryState, InMemoryAuditStore Audit);

    private static Harness Build(INotificationChannel? channel = null)
    {
        var inApp = new InAppChannel();
        var preferences = new InMemoryNotificationPreferences();
        var deliveryState = new InMemoryDeliveryStateStore();
        var audit = new InMemoryAuditStore(new DefaultAuditRedactionPolicy());
        var service = new NotificationService(channel ?? inApp, preferences, deliveryState, audit, TimeProvider.System);
        return new Harness(service, inApp, preferences, deliveryState, audit);
    }

    private static NotificationIntent Intent(bool critical = false) => new(
        "catalog.item-created", "alice", "t1", "c1",
        new Dictionary<string, string> { ["item"] = "Anvil" }, SecurityCritical: critical);

    [Fact]
    public async Task Delivers_through_the_template_and_records_durable_state()
    {
        var h = Build();
        var ct = TestContext.Current.CancellationToken;

        var record = await h.Service.SendAsync(Intent(), ItemCreated, "en-GB", ct);

        Assert.Equal(DeliveryState.Delivered, record.State);
        Assert.Equal("Item Anvil was created.", Assert.Single(h.Channel.InboxOf("alice")));
        Assert.Single(await h.DeliveryState.ListForAsync("alice", ct), r => r.State == DeliveryState.Delivered);
    }

    [Fact]
    public async Task Opt_out_suppresses_ordinary_notifications()
    {
        var h = Build();
        var ct = TestContext.Current.CancellationToken;
        await h.Preferences.SetOptOutAsync("alice", "catalog.item-created", true, ct);

        var record = await h.Service.SendAsync(Intent(), ItemCreated, "en-GB", ct);

        Assert.Equal(DeliveryState.Suppressed, record.State);
        Assert.Empty(h.Channel.InboxOf("alice"));
    }

    [Fact]
    public async Task Security_critical_overrides_opt_out_and_is_audited()
    {
        var h = Build();
        var ct = TestContext.Current.CancellationToken;
        await h.Preferences.SetOptOutAsync("alice", "catalog.item-created", true, ct);

        var record = await h.Service.SendAsync(Intent(critical: true), ItemCreated, "en-GB", ct);

        Assert.Equal(DeliveryState.Delivered, record.State);
        Assert.Single(h.Channel.InboxOf("alice"));
        var evidence = Assert.Single((await h.Audit.ReadAllAsync(ct)).Select(r => r.Event),
            e => e.Action == "notifications.policy-override");
        Assert.Equal("alice", evidence.Subject);
    }

    private sealed class FailingChannel : INotificationChannel
    {
        public string Name => "failing";

        public Task DeliverAsync(string recipient, string body, CancellationToken ct) =>
            throw new InvalidOperationException("smtp down");
    }

    [Fact]
    public async Task Channel_failure_records_failed_state_with_the_error()
    {
        var h = Build(new FailingChannel());
        var ct = TestContext.Current.CancellationToken;

        var record = await h.Service.SendAsync(Intent(), ItemCreated, "en-GB", ct);

        Assert.Equal(DeliveryState.Failed, record.State);
        Assert.Equal("smtp down", record.Error);
    }
}
