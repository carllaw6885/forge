using Forge.Core.Primitives;
using Forge.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.EventsTests;

public class InProcessEventBusTests
{
    private sealed class RecordingHandler(List<string> log, string name) : IIntegrationEventHandler<ThingHappened>
    {
        public Task HandleAsync(EventEnvelope envelope, ThingHappened payload, CancellationToken ct)
        {
            log.Add($"{name}:{payload.What}:{envelope.EventId:N}");
            return Task.CompletedTask;
        }
    }

    private static (IIntegrationEventBus Bus, List<string> Log) BuildBus(int handlerCount)
    {
        var log = new List<string>();
        var services = new ServiceCollection().AddForgeEvents();
        for (var i = 1; i <= handlerCount; i++)
        {
            var name = $"h{i}";
            services.AddScoped<IIntegrationEventHandler<ThingHappened>>(_ => new RecordingHandler(log, name));
        }

        return (services.BuildServiceProvider().GetRequiredService<IIntegrationEventBus>(), log);
    }

    [Fact]
    public async Task Delivers_to_all_handlers_in_registration_order()
    {
        var (bus, log) = BuildBus(handlerCount: 2);
        var envelope = EventEnvelope.Create(new ThingHappened("anvil"), CorrelationId.New());

        await bus.PublishAsync(envelope, TestContext.Current.CancellationToken);

        Assert.Equal(2, log.Count);
        Assert.StartsWith("h1:anvil:", log[0], StringComparison.Ordinal);
        Assert.StartsWith("h2:anvil:", log[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Duplicate_delivery_is_tolerated_not_deduplicated()
    {
        var (bus, log) = BuildBus(handlerCount: 1);
        var envelope = EventEnvelope.Create(new ThingHappened("again"), CorrelationId.New());

        await bus.PublishAsync(envelope, TestContext.Current.CancellationToken);
        await bus.PublishAsync(envelope, TestContext.Current.CancellationToken);

        // At-least-once semantics: same EventId observed twice; idempotence is the handler's job.
        Assert.Equal(2, log.Count);
        Assert.Equal(log[0], log[1]);
    }

    [Fact]
    public async Task No_registered_handlers_is_a_no_op()
    {
        var (bus, log) = BuildBus(handlerCount: 0);

        await bus.PublishAsync(
            EventEnvelope.Create(new ThingHappened("void"), CorrelationId.New()),
            TestContext.Current.CancellationToken);

        Assert.Empty(log);
    }
}
