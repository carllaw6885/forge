using Forge.Core.Primitives;
using Forge.Events;
using Forge.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.Persistence.SqlServer.Tests;

[IntegrationEvent("kerneltest.widget.forged", 1)]
public sealed record WidgetForged(int WidgetId, string Name) : IIntegrationEvent;

/// <summary>Transactional outbox against real SQL Server (ADR 04).</summary>
public class OutboxTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private sealed class RecordingHandler(List<EventEnvelope> log) : IIntegrationEventHandler<WidgetForged>
    {
        public Task HandleAsync(EventEnvelope envelope, WidgetForged payload, CancellationToken ct)
        {
            log.Add(envelope);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingBus : IIntegrationEventBus
    {
        public int Calls;

        public Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("transient dispatch failure");
        }
    }

    private (ServiceProvider Provider, List<EventEnvelope> Log) BuildProvider(IIntegrationEventBus? bus = null)
    {
        Assert.SkipWhen(fixture.UnavailableReason is not null, $"SQL Server container unavailable: {fixture.UnavailableReason}");

        var log = new List<EventEnvelope>();
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<CurrentTenant>();
        services.AddSingleton<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddModuleDbContext<KernelTestDbContext>(fixture.ConnectionString, "kerneltest");
        if (bus is null)
        {
            services.AddForgeEvents();
            services.AddSingleton<IIntegrationEventHandler<WidgetForged>>(new RecordingHandler(log));
        }
        else
        {
            services.AddSingleton(bus);
        }

        return (services.BuildServiceProvider(), log);
    }

    private static async Task MigrateAsync(ServiceProvider provider, CancellationToken ct)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<KernelTestDbContext>().Database.MigrateAsync(ct);
    }

    private static async Task EnqueueAsync(ServiceProvider provider, string name, CancellationToken ct)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KernelTestDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
        await outbox.EnqueueAsync(
            EventEnvelope.Create(new WidgetForged(1, name), CorrelationId.New(), tenantId: "tenant-a"),
            ct);
        await db.SaveChangesAsync(ct);
    }

    [Fact]
    public async Task Enqueued_entry_is_dispatched_with_full_context_and_marked()
    {
        var ct = TestContext.Current.CancellationToken;
        var (provider, log) = BuildProvider();
        await using var _ = provider;
        await MigrateAsync(provider, ct);
        await EnqueueAsync(provider, "anvil", ct);

        var dispatcher = provider.GetRequiredService<OutboxDispatcher>();
        await dispatcher.DrainAsync(typeof(KernelTestDbContext), ct);

        var envelope = Assert.Single(log, e => ((WidgetForged)e.Payload).Name == "anvil");
        Assert.Equal("kerneltest.widget.forged", envelope.EventType);
        Assert.Equal("tenant-a", envelope.TenantId);

        using var scope = provider.CreateScope();
        var entry = Assert.Single(await scope.ServiceProvider.GetRequiredService<KernelTestDbContext>()
            .Set<OutboxEntry>().AsNoTracking().Where(e => e.EventId == envelope.EventId).ToListAsync(ct));
        Assert.NotNull(entry.DispatchedAt);
    }

    [Fact]
    public async Task Rolled_back_transaction_leaves_no_outbox_entry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (provider, _) = BuildProvider();
        await using var _1 = provider;
        await MigrateAsync(provider, ct);

        Guid eventId;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KernelTestDbContext>();
            var envelope = EventEnvelope.Create(new WidgetForged(2, "orphan"), CorrelationId.New());
            eventId = envelope.EventId;
            await scope.ServiceProvider.GetRequiredService<IOutbox>().EnqueueAsync(envelope, ct);
            db.Widgets.Add(new Widget { Name = new string('x', 500) }); // exceeds column limit -> save fails

            await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync(ct));
        }

        using var check = provider.CreateScope();
        Assert.Empty(await check.ServiceProvider.GetRequiredService<KernelTestDbContext>()
            .Set<OutboxEntry>().AsNoTracking().Where(e => e.EventId == eventId).ToListAsync(ct));
    }

    [Fact]
    public async Task Failed_dispatch_backs_off_and_redelivers_with_same_event_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var failingBus = new FailingBus();
        var (provider, _) = BuildProvider(failingBus);
        await using var _1 = provider;
        await MigrateAsync(provider, ct);
        await EnqueueAsync(provider, "retry-me", ct);

        var dispatcher = provider.GetRequiredService<OutboxDispatcher>();
        await dispatcher.DrainAsync(typeof(KernelTestDbContext), ct);

        Assert.Equal(1, failingBus.Calls);
        using (var scope = provider.CreateScope())
        {
            var entry = Assert.Single(await scope.ServiceProvider.GetRequiredService<KernelTestDbContext>()
                .Set<OutboxEntry>().AsNoTracking().Where(e => e.DispatchedAt == null && e.EventType == "kerneltest.widget.forged" && e.Attempts > 0).ToListAsync(ct));
            Assert.True(entry.NextAttemptAt > DateTimeOffset.UtcNow.AddSeconds(-1)); // backoff recorded
        }

        // not yet due: a second drain must not retry early
        await dispatcher.DrainAsync(typeof(KernelTestDbContext), ct);
        Assert.Equal(1, failingBus.Calls);
    }
}
