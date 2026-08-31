using Forge.Auditing;
using Forge.Events;

namespace Forge.ReferenceCatalog;

/// <summary>
/// Structured audit contribution (ADR 08): evidence is appended to the real
/// hash-chained audit store off the domain event, not inline in the endpoint.
/// Actor becomes the authenticated identity in Phase 2.2.
/// </summary>
internal sealed class CatalogItemAddedAuditHandler(IAuditStore audit, TimeProvider clock)
    : IDomainEventHandler<CatalogItemAdded>
{
    public async Task HandleAsync(CatalogItemAdded domainEvent, CancellationToken cancellationToken) =>
        await audit.AppendAsync(new AuditEvent
        {
            Action = "catalog.item.created",
            TenantId = domainEvent.TenantId,
            Actor = "system",
            CorrelationId = domainEvent.CorrelationId.ToString(),
            Subject = domainEvent.ItemId.ToString("N"),
            Outcome = "success",
            OccurredAt = clock.GetUtcNow(),
        }, cancellationToken);
}
