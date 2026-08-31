using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Forge.Persistence.SqlServer;

/// <summary>DI registration for module-owned SQL Server contexts.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a module-owned DbContext against the SQL Server reference
    /// provider. The migrations history table lives in the module's own schema,
    /// so each module's migrations are independent (ADR 03).
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        string schema)
        where TContext : ForgeModuleDbContext
    {
        return services.AddDbContext<TContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", schema)));
    }
}
