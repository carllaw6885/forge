using Microsoft.EntityFrameworkCore;

namespace Forge.ReferenceCatalog.Contracts;

/// <summary>
/// The module's synchronous public contract (ADR 04 sample): DTOs only, no
/// domain entities. Other modules depend on this interface, never on the
/// module's internals. Registered in DI by CatalogModule.
/// </summary>
public interface ICatalogReader
{
    Task<CatalogItemDto?> FindAsync(string tenantId, Guid id, CancellationToken cancellationToken);
}

/// <summary>Public contract DTO — the only shape catalog data leaves the module in.</summary>
public sealed record CatalogItemDto(Guid Id, string Name, DateTimeOffset CreatedAt);

internal sealed class CatalogReader(CatalogDbContext db) : ICatalogReader
{
    public async Task<CatalogItemDto?> FindAsync(string tenantId, Guid id, CancellationToken cancellationToken)
    {
        var item = await db.Items.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);
        return item is null ? null : new CatalogItemDto(item.Id, item.Name, item.CreatedAt);
    }
}
