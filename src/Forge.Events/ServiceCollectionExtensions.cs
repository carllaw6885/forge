using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Forge.Events;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-process integration event bus and the scoped domain
    /// event collector. Handlers are registered explicitly by each module —
    /// never discovered (ADR 01).
    /// </summary>
    public static IServiceCollection AddForgeEvents(this IServiceCollection services)
    {
        services.TryAddSingleton<IIntegrationEventBus, InProcessEventBus>();
        services.TryAddScoped<DomainEventCollector>();
        return services;
    }
}
