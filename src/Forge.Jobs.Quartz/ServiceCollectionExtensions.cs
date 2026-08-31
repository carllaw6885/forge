using System.Reflection;
using Forge.Core.Validation;
using Forge.Jobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;

namespace Forge.Jobs.Quartz;

/// <summary>DI registration for the Quartz reference job provider (ADR 10).</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers durable Quartz-backed jobs. Pass a connection string for the
    /// persistent SQL-backed store (production); UseInMemoryStore is for tests
    /// and is rejected by production validation.
    /// </summary>
    public static IServiceCollection AddForgeQuartzJobs(this IServiceCollection services, ForgeQuartzOptions options)
    {
        if (!options.UseInMemoryStore && string.IsNullOrEmpty(options.ConnectionString))
        {
            throw new ArgumentException("a connection string is required for the persistent job store", nameof(options));
        }

        services.AddSingleton(options);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IProductionConfigurationValidator, DurableJobStoreValidator>());
        services.TryAddSingleton<ITerminalFailureSink, InMemoryTerminalFailureSink>();

        services.AddQuartz(quartz =>
        {
            quartz.SchedulerName = options.SchedulerName;
            if (!options.UseInMemoryStore)
            {
                quartz.UsePersistentStore(store =>
                {
                    store.UseSqlServer(options.ConnectionString!);
                    store.UseSystemTextJsonSerializer();
                    store.PerformSchemaValidation = false; // schema installed by EnsureQuartzSchemaAsync
                });
            }
        });

        services.AddScoped<ForgeJobWrapper>();
        services.AddSingleton<IJobScheduler, QuartzJobScheduler>();
        services.AddSingleton<QuartzHostedRunner>();
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(sp =>
            sp.GetRequiredService<QuartzHostedRunner>());
        return services;
    }

    /// <summary>
    /// Installs Quartz's official SQL Server schema (vendored verbatim) if the
    /// QRTZ_ tables are missing. Idempotent.
    /// Executed by the DbMigrator (and forge db) — never by web startup.
    /// </summary>
    public static async Task EnsureQuartzSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'QRTZ_JOB_DETAILS'";
            if ((int)(await check.ExecuteScalarAsync(cancellationToken))! > 0)
            {
                return;
            }
        }

        await using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Forge.Jobs.Quartz.quartz_tables_sqlServer.sql")!;
        using var reader = new StreamReader(stream);
        var script = await reader.ReadToEndAsync(cancellationToken);

        foreach (var batch in script.Split(["\nGO", "\rGO"], StringSplitOptions.RemoveEmptyEntries)
                     .Select(b => b.Trim())
                     .Where(b => b.Length > 0 && b != "GO"))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}

/// <summary>Starts and stops the Quartz scheduler with the host.</summary>
public sealed class QuartzHostedRunner(ISchedulerFactory schedulerFactory) : Microsoft.Extensions.Hosting.IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await scheduler.Start(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await scheduler.Shutdown(waitForJobsToComplete: false, cancellationToken);
    }
}
