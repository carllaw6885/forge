using Microsoft.Extensions.DependencyInjection;

namespace Forge.Events;

/// <summary>Consumes one integration event type; registered explicitly in DI by its module.</summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    /// <summary>
    /// Delivery is at-least-once: the same EventId can arrive more than once
    /// and handlers must be idempotent (ADR 04).
    /// </summary>
    Task HandleAsync(EventEnvelope envelope, TEvent payload, CancellationToken cancellationToken);
}

/// <summary>Publishes an <see cref="EventEnvelope"/> to its consumers.</summary>
public interface IIntegrationEventBus
{
    Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken);
}

/// <summary>
/// Reliable publication contract (ADR 04): enqueue in the same transaction as
/// the state change. Implemented by Forge.Persistence in Phase 3; contract only
/// in v0.1 Phase 1.
/// </summary>
public interface IOutbox
{
    Task EnqueueAsync(EventEnvelope envelope, CancellationToken cancellationToken);
}

/// <summary>
/// The v0.1 in-process delivery path: dispatches an envelope synchronously to
/// every DI-registered handler for its payload type, in registration order.
/// No handlers registered is a valid state, not an error. The bus never
/// deduplicates — duplicate tolerance belongs to handlers.
/// </summary>
public sealed class InProcessEventBus(IServiceProvider services) : IIntegrationEventBus
{
    public async Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(envelope.Payload.GetType());
        foreach (var handler in services.GetServices(handlerType))
        {
            await (Task)handlerType.GetMethod("HandleAsync")!
                .Invoke(handler, [envelope, envelope.Payload, cancellationToken])!;
        }
    }
}
