using Forge.Core.Modules;
using Forge.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Forge.Persistence.SqlServer.Tests;

// A deliberately tiny module persistence surface used to prove the pattern:
// module-owned context, module schema, hand-written module-owned migration.

public sealed class Widget
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public sealed class TenantNote : Forge.Tenancy.ITenantOwned
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public required string Text { get; set; }
}

public sealed class KernelTestDbContext(
    DbContextOptions<KernelTestDbContext> options,
    Forge.Tenancy.ICurrentTenant? currentTenant = null)
    : ForgeModuleDbContext(options, currentTenant)
{
    public static readonly ModuleManifest Manifest = new()
    {
        Id = "Forge.KernelTest",
        Name = "Kernel test module",
        Version = "0.1.0",
        OwnedSchemas = ["kerneltest"],
    };

    public override string Schema => "kerneltest";

    public DbSet<Widget> Widgets => Set<Widget>();

    public DbSet<TenantNote> Notes => Set<TenantNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Widget>(w => w.Property(x => x.Name).HasMaxLength(64));
        modelBuilder.Entity<TenantNote>(n =>
        {
            n.Property(x => x.TenantId).HasMaxLength(64);
            n.Property(x => x.Text).HasMaxLength(256);
        });
    }
}

[DbContext(typeof(KernelTestDbContext))]
[Migration("20260831000003_AddNotes")]
public sealed class AddNotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "Notes",
            schema: "kerneltest",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<string>(maxLength: 64, nullable: false),
                Text = table.Column<string>(maxLength: 256, nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Notes", x => x.Id));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "Notes", schema: "kerneltest");
}

[DbContext(typeof(KernelTestDbContext))]
[Migration("20260831000001_Init")]
public sealed class Init : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("kerneltest");
        migrationBuilder.CreateTable(
            name: "Widgets",
            schema: "kerneltest",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(maxLength: 64, nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Widgets", x => x.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "Widgets", schema: "kerneltest");
}

/// <summary>Adds the module-owned outbox table (ADR 04).</summary>
[DbContext(typeof(KernelTestDbContext))]
[Migration("20260831000005_AddKernelOutbox")]
public sealed class AddKernelOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "__ForgeOutbox",
            schema: "kerneltest",
            columns: table => new
            {
                Sequence = table.Column<long>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                EventId = table.Column<Guid>(nullable: false),
                EventType = table.Column<string>(maxLength: 256, nullable: false),
                SchemaVersion = table.Column<int>(nullable: false),
                TenantId = table.Column<string>(maxLength: 64, nullable: true),
                CorrelationId = table.Column<string>(maxLength: 64, nullable: false),
                CausationId = table.Column<Guid>(nullable: true),
                PayloadType = table.Column<string>(maxLength: 512, nullable: false),
                PayloadJson = table.Column<string>(nullable: false),
                Attempts = table.Column<int>(nullable: false),
                NextAttemptAt = table.Column<DateTimeOffset>(nullable: false),
                DispatchedAt = table.Column<DateTimeOffset>(nullable: true),
            },
            constraints: table => table.PrimaryKey("PK___ForgeOutbox", x => x.Sequence));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "__ForgeOutbox", schema: "kerneltest");
}
