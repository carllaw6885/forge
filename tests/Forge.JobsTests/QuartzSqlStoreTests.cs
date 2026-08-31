using System.Collections.Concurrent;
using Forge.Auditing;
using Forge.Jobs;
using Forge.Jobs.Quartz;
using Forge.Tenancy;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;
using Xunit;

namespace Forge.JobsTests;

/// <summary>Durable SQL-backed Quartz store against a real container (ADR 10/20).</summary>
public sealed class QuartzSqlFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    public string ConnectionString => _container!.GetConnectionString();
    public string? UnavailableReason { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
            await Forge.Jobs.Quartz.ServiceCollectionExtensions.EnsureQuartzSchemaAsync(ConnectionString, CancellationToken.None);
        }
        catch (Exception ex)
        {
            if (Environment.GetEnvironmentVariable("FORGE_REQUIRE_SQLSERVER") == "true")
            {
                throw;
            }

            UnavailableReason = ex.Message;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

public class QuartzSqlStoreTests(QuartzSqlFixture fixture) : IClassFixture<QuartzSqlFixture>
{
    [Fact]
    public async Task Schema_install_is_idempotent_and_jobs_persist_and_execute_durably()
    {
        Assert.SkipWhen(fixture.UnavailableReason is not null, $"SQL Server container unavailable: {fixture.UnavailableReason}");
        var ct = TestContext.Current.CancellationToken;

        // second install must be a no-op
        await Forge.Jobs.Quartz.ServiceCollectionExtensions.EnsureQuartzSchemaAsync(fixture.ConnectionString, ct);

        var contexts = new ConcurrentQueue<JobContext>();
        var services = new ServiceCollection();
        // NullLoggerFactory is static and dispose-proof: Quartz's static log
        // bridge captures the first factory it sees, so a disposable one from
        // another fixture would poison every later scheduler in the process.
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(Microsoft.Extensions.Logging.Logger<>));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<CurrentTenant>();
        services.AddSingleton<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddSingleton<IAuditRedactionPolicy, DefaultAuditRedactionPolicy>();
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();
        services.AddSingleton(contexts);
        services.AddScoped<RecordingJob>();
        services.AddForgeQuartzJobs(new ForgeQuartzOptions { ConnectionString = fixture.ConnectionString, SchedulerName = $"test-{Guid.NewGuid():N}" });

        await using var provider = services.BuildServiceProvider();
        foreach (var hosted in provider.GetServices<IHostedService>())
        {
            await hosted.StartAsync(ct);
        }

        await provider.GetRequiredService<IJobScheduler>()
            .EnqueueAsync<RecordingJob>(new JobContext("tenant-a", "corr-sql", "durable-1"), ct);

        // the job row is persisted in the QRTZ_ tables (durability), then executes
        await using (var connection = new SqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE 'QRTZ%'";
            Assert.True((int)(await command.ExecuteScalarAsync(ct))! >= 10);
        }

        for (var waited = 0; contexts.IsEmpty && waited < 20000; waited += 200)
        {
            await Task.Delay(200, ct);
        }

        var context = Assert.Single(contexts);
        Assert.Equal("corr-sql", context.CorrelationId);
        Assert.Equal("durable-1", context.IdempotencyKey);
    }
}
