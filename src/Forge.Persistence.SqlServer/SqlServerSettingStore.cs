using Forge.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Forge.Persistence.SqlServer;

/// <summary>Row shape for scoped setting values (settings schema).</summary>
public sealed class SettingRow
{
    public required string Key { get; set; }
    public required string Scope { get; set; }
    public required string ScopeId { get; set; }
    public required string Value { get; set; }
}

/// <summary>The settings store's own persistence boundary (settings schema).</summary>
public sealed class SettingsDbContext(DbContextOptions<SettingsDbContext> options)
    : ForgeModuleDbContext(options)
{
    public override string Schema => "settings";

    public DbSet<SettingRow> Settings => Set<SettingRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SettingRow>(row =>
        {
            row.HasKey(x => new { x.Key, x.Scope, x.ScopeId });
            row.Property(x => x.Key).HasMaxLength(256);
            row.Property(x => x.Scope).HasMaxLength(16);
            row.Property(x => x.ScopeId).HasMaxLength(128);
        });
    }
}

/// <summary>Hand-written module-owned migration for the settings schema.</summary>
[DbContext(typeof(SettingsDbContext))]
[Migration("20260831000007_InitSettings")]
public sealed class InitSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("settings");
        migrationBuilder.CreateTable(
            name: "Settings",
            schema: "settings",
            columns: table => new
            {
                Key = table.Column<string>(maxLength: 256, nullable: false),
                Scope = table.Column<string>(maxLength: 16, nullable: false),
                ScopeId = table.Column<string>(maxLength: 128, nullable: false),
                Value = table.Column<string>(nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Settings", x => new { x.Key, x.Scope, x.ScopeId }));
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "Settings", schema: "settings");
}

/// <summary>SQL Server reference implementation of the scoped setting store.</summary>
public sealed class SqlServerSettingStore(IDbContextFactory<SettingsDbContext> contextFactory) : ISettingStore
{
    private const string NoScope = "-"; // key columns cannot be null

    public async Task<string?> GetAsync(string key, SettingScope scope, string? scopeId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Settings.AsNoTracking().SingleOrDefaultAsync(
            s => s.Key == key && s.Scope == scope.ToString() && s.ScopeId == (scopeId ?? NoScope), cancellationToken);
        return row?.Value;
    }

    public async Task SetAsync(string key, SettingScope scope, string? scopeId, string value, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var effectiveScopeId = scopeId ?? NoScope;
        var row = await db.Settings.SingleOrDefaultAsync(
            s => s.Key == key && s.Scope == scope.ToString() && s.ScopeId == effectiveScopeId, cancellationToken);
        if (row is null)
        {
            db.Settings.Add(new SettingRow { Key = key, Scope = scope.ToString(), ScopeId = effectiveScopeId, Value = value });
        }
        else
        {
            row.Value = value;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>DI registration for the SQL Server setting store.</summary>
public static class SettingsPersistenceExtensions
{
    public static IServiceCollection AddSqlServerSettingStore(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<SettingsDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "settings")));
        services.AddSingleton<ISettingStore, SqlServerSettingStore>();
        return services;
    }
}
