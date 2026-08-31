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

/// <summary>Adds the module-owned outbox table (ADR 04).</summary>
[DbContext(typeof(CatalogDbContext))]
[Migration("20260831000006_AddCatalogOutbox")]
public sealed class AddCatalogOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "__ForgeOutbox",
            schema: "catalog",
            columns: table => new
            {
                Sequence = table.Column<long>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                EventId = table.Column<Guid>(nullable: false),
                EventType = table.Column<string>(maxLength: 256, nullable: false),
                SchemaVersion = table.Column<int>(nullable: false),
                TenantId = table.Column<string>(maxLength: 64, nullable: true),
                CorrelationId = table.Column<string>(maxLength: 64, nullable: false),
                TraceParent = table.Column<string>(maxLength: 128, nullable: true),
                CausationId = table.Column<Guid>(nullable: true),
                PayloadType = table.Column<string>(maxLength: 512, nullable: false),
                PayloadJson = table.Column<string>(nullable: false),
                Attempts = table.Column<int>(nullable: false),
                NextAttemptAt = table.Column<DateTimeOffset>(nullable: false),
                DispatchedAt = table.Column<DateTimeOffset>(nullable: true),
            },
            constraints: table => table.PrimaryKey("PK___ForgeOutbox", x => x.Sequence));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "__ForgeOutbox", schema: "catalog");
}
