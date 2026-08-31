using Forge.Core.Primitives;
using Forge.Events;

namespace Forge.ReferenceCatalog;

/// <summary>Tenant-owned catalog item. Domain entity — never leaves this module (ADR 04).</summary>
public sealed class CatalogItem
{
    public Guid Id { get; set; }
    public required string TenantId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Internal fact; stays inside the module.</summary>
public sealed record CatalogItemAdded(Guid ItemId, string Name, string TenantId, CorrelationId CorrelationId) : IDomainEvent;

/// <summary>Versioned cross-boundary contract: pure data.</summary>
[IntegrationEvent("catalog.item.created", 1)]
public sealed record CatalogItemCreated(Guid ItemId, string Name) : IIntegrationEvent;
