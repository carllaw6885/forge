using Forge.Persistence.SqlServer;
using Forge.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Forge.ReferenceCatalog;

/// <summary>The module's own persistence boundary: catalog schema, module-owned migrations (ADR 03).</summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options, ICurrentTenant currentTenant)
    : ForgeModuleDbContext(options, currentTenant)
{
    public override string Schema => "catalog";

    public DbSet<CatalogItem> Items => Set<CatalogItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<CatalogItem>(item =>
        {
            item.Property(x => x.TenantId).HasMaxLength(64);
            item.Property(x => x.Name).HasMaxLength(128);
            item.HasIndex(x => new { x.TenantId, x.Id });
        });
    }
}

/// <summary>Hand-written module-owned initial migration for the catalog schema.</summary>
[DbContext(typeof(CatalogDbContext))]
[Migration("20260831000002_InitCatalog")]
public sealed class InitCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("catalog");
        migrationBuilder.CreateTable(
            name: "Items",
            schema: "catalog",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<string>(maxLength: 64, nullable: false),
                Name = table.Column<string>(maxLength: 128, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Items", x => x.Id));
        migrationBuilder.CreateIndex("IX_Items_TenantId_Id", "Items", ["TenantId", "Id"], schema: "catalog");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "Items", schema: "catalog");
}
