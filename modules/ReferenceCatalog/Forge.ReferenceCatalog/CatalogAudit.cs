using System.Collections.Concurrent;
using Forge.Events;

namespace Forge.ReferenceCatalog;

/// <summary>
/// Structured audit contribution (ADR 08 demonstration): evidence records with
/// tenant, actor-action and correlation context, distinct from ILogger.
/// </summary>
public sealed record CatalogAuditEntry(string Action, string TenantId, string CorrelationId, string Subject);

public interface ICatalogAuditTrail
{
    void Append(CatalogAuditEntry entry);
    IReadOnlyList<CatalogAuditEntry> Snapshot();
}

// ponytail: in-memory seam; Forge.Auditing's append-only tamper-evident store
// replaces this in Phase 2.
internal sealed class InMemoryCatalogAuditTrail : ICatalogAuditTrail
{
    private readonly ConcurrentQueue<CatalogAuditEntry> _entries = new();

    public void Append(CatalogAuditEntry entry) => _entries.Enqueue(entry);

    public IReadOnlyList<CatalogAuditEntry> Snapshot() => [.. _entries];
}

/// <summary>Audit is contributed off the domain event, not inline in the endpoint.</summary>
internal sealed class CatalogItemAddedAuditHandler(ICatalogAuditTrail trail) : IDomainEventHandler<CatalogItemAdded>
{
    public Task HandleAsync(CatalogItemAdded domainEvent, CancellationToken cancellationToken)
    {
        trail.Append(new CatalogAuditEntry(
            Action: "catalog.item.created",
            TenantId: domainEvent.TenantId,
            CorrelationId: domainEvent.CorrelationId.ToString(),
            Subject: domainEvent.ItemId.ToString("N")));
        return Task.CompletedTask;
    }
}
