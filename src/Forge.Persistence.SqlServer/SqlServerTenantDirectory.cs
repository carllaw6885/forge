using Forge.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Forge.Persistence.SqlServer;

/// <summary>Row shape for tenant records (tenancy schema).</summary>
public sealed class TenantRow
{
    public required string Id { get; set; }
    public required string DisplayName { get; set; }
    public required bool Enabled { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}

/// <summary>The tenant directory's own persistence boundary (tenancy schema).</summary>
public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options)
    : ForgeModuleDbContext(options)
{
    public override string Schema => "tenancy";

    public DbSet<TenantRow> Tenants => Set<TenantRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<TenantRow>(row =>
        {
            row.HasKey(x => x.Id);
            row.Property(x => x.Id).HasMaxLength(128);
            row.Property(x => x.DisplayName).HasMaxLength(256);
        });
    }
}

/// <summary>Hand-written module-owned migration for the tenancy schema.</summary>
[DbContext(typeof(TenancyDbContext))]
[Migration("20260902000001_InitTenancy")]
public sealed class InitTenancy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("tenancy");
        migrationBuilder.CreateTable(
            name: "Tenants",
            schema: "tenancy",
            columns: table => new
            {
                Id = table.Column<string>(maxLength: 128, nullable: false),
                DisplayName = table.Column<string>(maxLength: 256, nullable: false),
                Enabled = table.Column<bool>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Tenants", x => x.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "Tenants", schema: "tenancy");
}

/// <summary>SQL Server reference implementation of the tenant directory.</summary>
internal sealed class SqlServerTenantDirectory(IDbContextFactory<TenancyDbContext> contextFactory) : ITenantDirectory
{
    public async Task<Tenant?> GetAsync(string id, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        return row is null ? null : ToTenant(row);
    }

    public async Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Tenants.AsNoTracking().OrderBy(t => t.Id).Select(t => ToTenant(t)).ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Tenants.SingleOrDefaultAsync(t => t.Id == tenant.Id, cancellationToken);
        if (row is null)
        {
            db.Tenants.Add(new TenantRow
            {
                Id = tenant.Id,
                DisplayName = tenant.DisplayName,
                Enabled = tenant.Enabled,
                CreatedAt = tenant.CreatedAt,
            });
        }
        else
        {
            row.DisplayName = tenant.DisplayName;
            row.Enabled = tenant.Enabled;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Tenant ToTenant(TenantRow row) => new(row.Id, row.DisplayName, row.Enabled, row.CreatedAt);
}

/// <summary>DI registration for the SQL Server tenant directory.</summary>
public static class TenancyPersistenceExtensions
{
    public static IServiceCollection AddSqlServerTenantDirectory(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<TenancyDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "tenancy")));
        services.AddSingleton<ITenantDirectory, SqlServerTenantDirectory>();
        return services;
    }
}
