using Forge.Core.Primitives;
using Forge.Core.Privacy;
using Forge.Events;
using Forge.Tenancy;

namespace Forge.ReferenceCatalog;

/// <summary>Tenant-owned catalog item. Domain entity — never leaves this module (ADR 04).</summary>
public sealed class CatalogItem : ITenantOwned
{
    public Guid Id { get; set; }

    /// <summary>Stamped centrally from the ambient tenant on insert (ADR 05).</summary>
    public string TenantId { get; set; } = string.Empty;

    public required string Name { get; set; }

    /// <summary>Who created the item; personal data enumerated by the module's privacy contributor (ADR 09).</summary>
    [Classified(DataClassification.Personal)]
    public string? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Internal fact; stays inside the module.</summary>
public sealed record CatalogItemAdded(Guid ItemId, string Name, string TenantId, CorrelationId CorrelationId, string? CreatedBy = null) : IDomainEvent;

/// <summary>Versioned cross-boundary contract: pure data.</summary>
[IntegrationEvent("catalog.item.created", 1)]
public sealed record CatalogItemCreated(Guid ItemId, string Name) : IIntegrationEvent;
