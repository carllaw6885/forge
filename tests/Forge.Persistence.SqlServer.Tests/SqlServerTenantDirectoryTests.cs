using Forge.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.Persistence.SqlServer.Tests;

/// <summary>SQL Server tenant directory against a real container: upsert semantics and ordering.</summary>
public class SqlServerTenantDirectoryTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task Save_upserts_and_list_orders_by_id()
    {
        Assert.SkipWhen(fixture.UnavailableReason is not null, $"SQL Server container unavailable: {fixture.UnavailableReason}");
        var ct = TestContext.Current.CancellationToken;

        var services = new ServiceCollection();
        services.AddSqlServerTenantDirectory(fixture.ConnectionString);
        await using var provider = services.BuildServiceProvider();
        await using (var db = await provider.GetRequiredService<IDbContextFactory<TenancyDbContext>>().CreateDbContextAsync(ct))
        {
            await db.Database.MigrateAsync(ct);
        }

        var directory = provider.GetRequiredService<ITenantDirectory>();
        await directory.SaveAsync(new Tenant("t2", "Two", Enabled: true, DateTimeOffset.UnixEpoch), ct);
        await directory.SaveAsync(new Tenant("t1", "One", Enabled: true, DateTimeOffset.UnixEpoch), ct);
        await directory.SaveAsync(new Tenant("t1", "One Renamed", Enabled: false, DateTimeOffset.UnixEpoch), ct);

        Assert.Null(await directory.GetAsync("missing", ct));
        var t1 = await directory.GetAsync("t1", ct);
        Assert.Equal("One Renamed", t1!.DisplayName);
        Assert.False(t1.Enabled);
        Assert.Equal(["t1", "t2"], (await directory.ListAsync(ct)).Select(t => t.Id));
    }
}
