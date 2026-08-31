using Microsoft.EntityFrameworkCore;

namespace Forge.Persistence.SqlServer;

/// <summary>
/// Base for module-owned DbContexts (ADR 03): every mapped object defaults to the
/// module's schema, and the module owns its migrations. This is EF Core used
/// directly (ADR 30) — no repository or unit-of-work layer on top.
/// </summary>
public abstract class ForgeModuleDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>The database schema this module owns; must appear in the module manifest's ownedSchemas.</summary>
    public abstract string Schema { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
    }
}
