using Forge.Core.Primitives;
using Forge.Events;
using Xunit;

namespace Forge.EventsTests;

[IntegrationEvent("test.thing.happened", 2)]
public sealed record ThingHappened(string What) : IIntegrationEvent;

public sealed record UnattributedEvent : IIntegrationEvent;

public class EventEnvelopeTests
{
    [Fact]
    public void Create_fills_identity_type_version_and_context()
    {
        var correlation = CorrelationId.New();
        var cause = Guid.NewGuid();

        var envelope = EventEnvelope.Create(
            new ThingHappened("anvil"), correlation, tenantId: "tenant-1", causationId: cause);

        Assert.NotEqual(Guid.Empty, envelope.EventId);
        Assert.Equal("test.thing.happened", envelope.EventType);
        Assert.Equal(2, envelope.SchemaVersion);
        Assert.Equal("tenant-1", envelope.TenantId);
        Assert.Equal(correlation, envelope.CorrelationId);
        Assert.Equal(cause, envelope.CausationId);
    }

    [Fact]
    public void Create_without_attribute_fails_with_named_type()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            EventEnvelope.Create(new UnattributedEvent(), CorrelationId.New()));

        Assert.Contains(nameof(UnattributedEvent), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tenant_and_causation_default_to_none()
    {
        var envelope = EventEnvelope.Create(new ThingHappened("x"), CorrelationId.New());

        Assert.Null(envelope.TenantId);
        Assert.Null(envelope.CausationId);
    }
}
