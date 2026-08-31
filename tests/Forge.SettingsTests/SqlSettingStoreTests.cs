using Forge.Persistence.SqlServer;
using Forge.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

namespace Forge.SettingsTests;

public sealed class SettingsSqlFixture : IAsyncLifetime
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

public class SqlSettingStoreTests(SettingsSqlFixture fixture) : IClassFixture<SettingsSqlFixture>
{
    [Fact]
    public async Task Values_round_trip_and_upsert_per_scope()
    {
        Assert.SkipWhen(fixture.UnavailableReason is not null, $"SQL Server container unavailable: {fixture.UnavailableReason}");
        var ct = TestContext.Current.CancellationToken;

        var services = new ServiceCollection();
        services.AddSqlServerSettingStore(fixture.ConnectionString);
        await using var provider = services.BuildServiceProvider();

        await using (var db = await provider.GetRequiredService<IDbContextFactory<SettingsDbContext>>()
                         .CreateDbContextAsync(ct))
        {
            await db.Database.MigrateAsync(ct);
        }

        var store = provider.GetRequiredService<ISettingStore>();

        await store.SetAsync("k", SettingScope.Application, null, "\"app\"", ct);
        await store.SetAsync("k", SettingScope.Tenant, "t1", "\"tenant\"", ct);
        await store.SetAsync("k", SettingScope.Tenant, "t1", "\"tenant2\"", ct); // upsert

        Assert.Equal("\"app\"", await store.GetAsync("k", SettingScope.Application, null, ct));
        Assert.Equal("\"tenant2\"", await store.GetAsync("k", SettingScope.Tenant, "t1", ct));
        Assert.Null(await store.GetAsync("k", SettingScope.Tenant, "t2", ct));
        Assert.Null(await store.GetAsync("k", SettingScope.User, "u1", ct));
    }
}
