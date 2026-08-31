using System.Diagnostics;
using System.Diagnostics.Metrics;
using Forge.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Forge.Persistence.SqlServer;

/// <summary>Explicit registry of module contexts whose outboxes the dispatcher drains (ADR 01: no scanning).</summary>
public sealed class OutboxContextRegistry
{
    private readonly List<Type> _contextTypes = [];

    public IReadOnlyList<Type> ContextTypes => _contextTypes;

    public void Register<TContext>() where TContext : ForgeModuleDbContext
    {
        if (!_contextTypes.Contains(typeof(TContext)))
        {
            _contextTypes.Add(typeof(TContext));
        }
    }
}

/// <summary>
/// Drains every registered module outbox in sequence order and publishes via
/// the integration event bus. Marks entries only after a successful publish —
/// a crash in between causes redelivery, which handlers tolerate (ADR 04).
/// Failed dispatches back off exponentially per entry.
/// ponytail: polling loop; per-context change signals if lag ever matters.
/// </summary>
public sealed class OutboxDispatcher(
    IServiceProvider services,
    OutboxContextRegistry registry,
    TimeProvider clock) : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("Forge.Outbox");
    public static readonly Meter Meter = new("Forge.Outbox");
    private static readonly Counter<long> Dispatched = Meter.CreateCounter<long>("forge.outbox.dispatched");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>("forge.outbox.dispatch_failures");

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var contextType in registry.ContextTypes)
            {
                try
                {
                    await DrainAsync(contextType, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception)
                {
                    // per-context isolation: one module's outbox failure must not
                    // starve the others; failed entries carry their own backoff
                    Failed.Add(1);
                }
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Single drain pass over one module's outbox; public for deterministic tests.</summary>
    public async Task DrainAsync(Type contextType, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = (ForgeModuleDbContext)scope.ServiceProvider.GetRequiredService(contextType);
        var bus = scope.ServiceProvider.GetRequiredService<IIntegrationEventBus>();
        var now = clock.GetUtcNow();

        var due = await db.Set<OutboxEntry>()
            .Where(e => e.DispatchedAt == null && e.NextAttemptAt <= now)
            .OrderBy(e => e.Sequence)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var entry in due)
        {
            var parent = entry.TraceParent is not null
                && ActivityContext.TryParse(entry.TraceParent, null, out var parsed) ? parsed : default;
            using var activity = ActivitySource.StartActivity("outbox.dispatch", ActivityKind.Consumer, parent);
            activity?.SetTag("forge.event_type", entry.EventType);
            activity?.SetTag("forge.tenant_id", entry.TenantId);
            activity?.SetTag("forge.correlation_id", entry.CorrelationId);

            try
            {
                await bus.PublishAsync(OutboxEnvelope.ToEnvelope(entry), cancellationToken);
                entry.DispatchedAt = clock.GetUtcNow();
                Dispatched.Add(1);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                entry.Attempts++;
                entry.NextAttemptAt = clock.GetUtcNow()
                    + TimeSpan.FromSeconds(Math.Min(Math.Pow(2, entry.Attempts), 300));
                Failed.Add(1);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
