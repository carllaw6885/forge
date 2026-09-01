using Microsoft.Extensions.DependencyInjection;

namespace Forge.Events;

/// <summary>
/// A fact that happened inside one module. Domain events never cross module
/// boundaries (ADR 04) — the type belongs to the module's own assembly and its
/// handlers live in the same module. Cross-module facts use
/// <see cref="IIntegrationEvent"/> instead.
/// </summary>
public interface IDomainEvent;

/// <summary>Handles a domain event inside the module that raised it; domain events never cross module boundaries (ADR 04).</summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Scoped collector: domain logic raises events during a unit of work; the
/// module dispatches them explicitly (typically after SaveChanges) via
/// <see cref="DispatchAsync"/>. No ambient magic, no auto-publication.
/// </summary>
public sealed class DomainEventCollector(IServiceProvider services)
{
    private readonly List<IDomainEvent> _pending = [];

    public IReadOnlyList<IDomainEvent> Pending => _pending;

    public void Raise(IDomainEvent domainEvent) => _pending.Add(domainEvent);

    /// <summary>Dispatches pending events in raise order to their DI-registered handlers, then clears.</summary>
    public async Task DispatchAsync(CancellationToken cancellationToken)
    {
        // Handlers may raise follow-up events while we dispatch; process until drained.
        while (_pending.Count > 0)
        {
            var domainEvent = _pending[0];
            _pending.RemoveAt(0);

            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            foreach (var handler in services.GetServices(handlerType))
            {
                await (Task)handlerType.GetMethod("HandleAsync")!
                    .Invoke(handler, [domainEvent, cancellationToken])!;
            }
        }
    }
}
