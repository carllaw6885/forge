using Forge.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.EventsTests;

public class DomainEventCollectorTests
{
    private sealed record ItemAdded(string Name) : IDomainEvent;

    private sealed record FollowUpNeeded(string Reason) : IDomainEvent;

    private sealed class ItemAddedHandler(List<string> log, DomainEventCollector collector) : IDomainEventHandler<ItemAdded>
    {
        public Task HandleAsync(ItemAdded e, CancellationToken ct)
        {
            log.Add($"added:{e.Name}");
            if (e.Name == "cascade")
            {
                collector.Raise(new FollowUpNeeded("cascade"));
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FollowUpHandler(List<string> log) : IDomainEventHandler<FollowUpNeeded>
    {
        public Task HandleAsync(FollowUpNeeded e, CancellationToken ct)
        {
            log.Add($"followup:{e.Reason}");
            return Task.CompletedTask;
        }
    }

    private static (DomainEventCollector Collector, List<string> Log) Build()
    {
        var log = new List<string>();
        var services = new ServiceCollection().AddForgeEvents();
        services.AddScoped<IDomainEventHandler<ItemAdded>>(sp =>
            new ItemAddedHandler(log, sp.GetRequiredService<DomainEventCollector>()));
        services.AddScoped<IDomainEventHandler<FollowUpNeeded>>(_ => new FollowUpHandler(log));

        var scope = services.BuildServiceProvider().CreateScope();
        return (scope.ServiceProvider.GetRequiredService<DomainEventCollector>(), log);
    }

    [Fact]
    public async Task Dispatches_in_raise_order_and_clears()
    {
        var (collector, log) = Build();
        collector.Raise(new ItemAdded("one"));
        collector.Raise(new ItemAdded("two"));

        await collector.DispatchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["added:one", "added:two"], log);
        Assert.Empty(collector.Pending);
    }

    [Fact]
    public async Task Events_raised_during_dispatch_are_also_dispatched()
    {
        var (collector, log) = Build();
        collector.Raise(new ItemAdded("cascade"));

        await collector.DispatchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["added:cascade", "followup:cascade"], log);
    }

    [Fact]
    public async Task Event_without_handler_dispatches_as_no_op()
    {
        var (collector, log) = Build();
        collector.Raise(new FollowUpNeeded("nobody-home-for-this-one"));

        await collector.DispatchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["followup:nobody-home-for-this-one"], log);
    }
}
