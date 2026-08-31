using System.Reflection;
using Forge.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Forge.Persistence.SqlServer;

/// <summary>
/// Base for module-owned DbContexts (ADR 03): every mapped object defaults to the
/// module's schema, and the module owns its migrations. This is EF Core used
/// directly (ADR 30) — no repository or unit-of-work layer on top.
/// Entities implementing <see cref="ITenantOwned"/> get central tenant filtering
/// and write guards (ADR 05): reads are filtered to the current tenant (host
/// scope sees all; unresolved scope sees nothing), and writes are stamped and
/// validated against the ambient tenant.
/// </summary>
public abstract class ForgeModuleDbContext(DbContextOptions options, ICurrentTenant? currentTenant = null)
    : DbContext(options)
{
    private static readonly MethodInfo ApplyFilterMethod =
        typeof(ForgeModuleDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ICurrentTenant? _currentTenant = currentTenant;

    /// <summary>The database schema this module owns; must appear in the module manifest's ownedSchemas.</summary>
    public abstract string Schema { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // Every module context carries its own outbox table (ADR 04): entries
        // commit in the module's own transaction, in the module's own schema.
        modelBuilder.Entity<OutboxEntry>(entry =>
        {
            entry.ToTable("__ForgeOutbox");
            entry.HasKey(x => x.Sequence);
            entry.Property(x => x.Sequence).ValueGeneratedOnAdd();
            entry.Property(x => x.EventType).HasMaxLength(256);
            entry.Property(x => x.TenantId).HasMaxLength(64);
            entry.Property(x => x.CorrelationId).HasMaxLength(64);
            entry.Property(x => x.TraceParent).HasMaxLength(128);
            entry.Property(x => x.PayloadType).HasMaxLength(512);
            entry.HasIndex(x => new { x.DispatchedAt, x.NextAttemptAt });
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType)))
        {
            ApplyFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }

        base.OnModelCreating(modelBuilder);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantOwned =>
        // Instance members keep the filter dynamic per query. Unresolved scope
        // has a null Id, which matches no row — deny-by-default.
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            _currentTenant!.Scope == TenantScope.Host || e.TenantId == _currentTenant.Id);

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardTenantWrites();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardTenantWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void GuardTenantWrites()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var scope = _currentTenant?.Scope ?? TenantScope.Unresolved;
            if (scope == TenantScope.Host)
            {
                continue; // explicit privileged scope may write any tenant's rows
            }

            if (scope == TenantScope.Unresolved)
            {
                throw new InvalidOperationException(
                    $"cannot write tenant-owned entity '{entry.Metadata.ClrType.Name}' with unresolved tenant scope");
            }

            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.TenantId))
            {
                entry.Entity.TenantId = _currentTenant!.Id!;
            }
            else if (entry.Entity.TenantId != _currentTenant!.Id)
            {
                throw new InvalidOperationException(
                    $"tenant isolation violation: entity '{entry.Metadata.ClrType.Name}' belongs to another tenant");
            }
        }
    }
}
