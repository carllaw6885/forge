using Forge.Caching.Redis;
using Forge.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Redis;
using Xunit;

namespace Forge.CachingTests;

public sealed class RedisFixture : IAsyncLifetime
{
    private RedisContainer? _container;
    public string ConnectionString => _container!.GetConnectionString();
    public string? UnavailableReason { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new RedisBuilder("redis:7-alpine").Build();
            await _container.StartAsync();
        }
        catch (Exception ex)
        {
            if (Environment.GetEnvironmentVariable("FORGE_REQUIRE_SQLSERVER") == "true")
            {
                throw; // CI requires all container-backed suites to run for real
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

public class RedisTenantCacheTests(RedisFixture fixture) : IClassFixture<RedisFixture>
{
    private static (TenantCache Cache, CurrentTenant Tenant, ServiceProvider Provider) Build(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<CurrentTenant>();
        services.AddSingleton<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddForgeRedisCache(connectionString);
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return (scope.ServiceProvider.GetRequiredService<TenantCache>(),
                provider.GetRequiredService<CurrentTenant>(), provider);
    }

    [Fact]
    public async Task Values_round_trip_with_tenant_scoped_keys_and_no_cross_tenant_bleed()
    {
        Assert.SkipWhen(fixture.UnavailableReason is not null, $"Redis container unavailable: {fixture.UnavailableReason}");
        var ct = TestContext.Current.CancellationToken;
        var (cache, tenant, provider) = Build(fixture.ConnectionString);
        await using var _ = provider;

        tenant.SetTenant("t1");
        await cache.SetAsync("greeting", "hello-t1", TimeSpan.FromMinutes(1), ct);
        Assert.Equal("hello-t1", await cache.GetAsync("greeting", ct));

        tenant.SetTenant("t2");
        Assert.Null(await cache.GetAsync("greeting", ct)); // same logical key, different tenant

        tenant.SetTenant("t1");
        await cache.RemoveAsync("greeting", ct);
        Assert.Null(await cache.GetAsync("greeting", ct));
    }

    [Fact]
    public async Task Unresolved_tenant_scope_cannot_touch_the_cache()
    {
        Assert.SkipWhen(fixture.UnavailableReason is not null, $"Redis container unavailable: {fixture.UnavailableReason}");
        var (cache, _, provider) = Build(fixture.ConnectionString);
        await using var _1 = provider;

        // TenantCacheKey throws on unresolved scope; deliberately NOT degraded —
        // deny-by-default applies to caches too (ADR 05/17)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.SetAsync("k", "v", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Dead_redis_degrades_to_misses_instead_of_failing_requests()
    {
        var ct = TestContext.Current.CancellationToken;
        var (cache, tenant, provider) = Build("127.0.0.1:1"); // nothing listens here
        await using var _ = provider;
        tenant.SetTenant("t1");

        await cache.SetAsync("k", "v", TimeSpan.FromMinutes(1), ct); // must not throw
        Assert.Null(await cache.GetAsync("k", ct)); // miss, not failure
        await cache.RemoveAsync("k", ct); // must not throw
    }
}
