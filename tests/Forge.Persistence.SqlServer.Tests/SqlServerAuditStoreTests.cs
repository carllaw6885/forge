using Forge.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.Persistence.SqlServer.Tests;

/// <summary>SQL Server audit store against a real container: chain integrity, retries, append-only schema.</summary>
public class SqlServerAuditStoreTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private ServiceProvider BuildProvider()
    {
        Assert.SkipWhen(fixture.UnavailableReason is not null, $"SQL Server container unavailable: {fixture.UnavailableReason}");

        var services = new ServiceCollection();
        services.AddSingleton<IAuditRedactionPolicy, DefaultAuditRedactionPolicy>();
        services.AddSqlServerAuditStore(fixture.ConnectionString);
        return services.BuildServiceProvider();
    }

    private static AuditEvent Event(string action) => new()
    {
        Action = action,
        TenantId = "tenant-a",
        Actor = "system",
        CorrelationId = "c1",
        Subject = "s1",
        Outcome = "success",
        OccurredAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public async Task Appends_form_a_verifiable_chain_across_concurrent_writers()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider();

        await using (var db = await provider.GetRequiredService<IDbContextFactory<AuditDbContext>>().CreateDbContextAsync(ct))
        {
            await db.Database.MigrateAsync(ct);
        }

        var store = provider.GetRequiredService<IAuditStore>();

        // concurrent appends race for the chain head; unique PreviousHash + retry keeps it linear
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i => store.AppendAsync(Event($"sql.{i}"), ct)));

        var records = await store.ReadAllAsync(ct);
        Assert.Equal(8, records.Count);
        Assert.Empty(AuditChainVerifier.Verify(records));
    }
}
