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
        services.AddDbContext<TContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", schema)));

        // Reliable publication (ADR 04): the module's IOutbox writes into its
        // own context, and the dispatcher drains this context's outbox table.
        services.AddScoped<Forge.Events.IOutbox>(sp =>
            new DbContextOutbox<TContext>(sp.GetRequiredService<TContext>()));

        var registry = (OutboxContextRegistry?)services
            .FirstOrDefault(d => d.ServiceType == typeof(OutboxContextRegistry))?.ImplementationInstance;
        if (registry is null)
        {
            registry = new OutboxContextRegistry();
            services.AddSingleton(registry);
            services.AddSingleton<OutboxDispatcher>();
            services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(sp =>
                sp.GetRequiredService<OutboxDispatcher>());
        }

        registry.Register<TContext>();
        return services;
    }
}
