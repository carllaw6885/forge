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

public sealed class KernelTestDbContext(DbContextOptions<KernelTestDbContext> options)
    : ForgeModuleDbContext(options)
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Widget>(w => w.Property(x => x.Name).HasMaxLength(64));
    }
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
