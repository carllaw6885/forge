using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace Forge.Persistence.SqlServer.Tests;

/// <summary>
/// Real SQL Server provider harness (ADR 20: real infrastructure where provider
/// behaviour matters). Skips dynamically when no container runtime is available
/// (e.g. developer machine without Docker running); always runs in CI.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public string? UnavailableReason { get; private set; }

    public string ConnectionString => _container!.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
        }
        catch (Exception ex) // any startup failure means "no container runtime here"
        {
            // In CI a missing container runtime must fail, never silently skip.
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

public class SqlServerContainerTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private KernelTestDbContext CreateContext()
    {
        Assert.SkipWhen(fixture.UnavailableReason is not null, $"SQL Server container unavailable: {fixture.UnavailableReason}");

        return new KernelTestDbContext(new DbContextOptionsBuilder<KernelTestDbContext>()
            .UseSqlServer(fixture.ConnectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "kerneltest"))
            .Options);
    }

    [Fact]
    public async Task Module_migration_applies_into_module_schema_and_is_idempotent()
    {
        await using var context = CreateContext();

        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken); // second run must be a no-op

        var applied = await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["20260831000001_Init", "20260831000003_AddNotes", "20260831000005_AddKernelOutbox"], applied);

        // The migrations history table itself must live in the module's schema,
        // keeping each module's migration metadata independent (ADR 03).
        var historySchemas = context.Database
            .SqlQueryRaw<string>("SELECT TABLE_SCHEMA AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory'")
            .ToList();
        Assert.Equal(["kerneltest"], historySchemas);
    }

    [Fact]
    public async Task Widgets_round_trip_in_module_schema()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        context.Widgets.Add(new Widget { Name = "anvil" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var names = await context.Widgets.AsNoTracking()
            .Select(w => w.Name)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains("anvil", names);
    }
}
