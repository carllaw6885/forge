using Forge.Settings;
using Forge.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.SettingsTests;

public class SettingsTests
{
    private static readonly SettingDefinition<int> PageSize =
        new("Catalog:PageSize", DefaultValue: 20, Validate: v => v is > 0 and <= 200);

    private static (SettingsService Service, CurrentTenant Tenant, OperationalFlags Flags) Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<CurrentTenant>();
        services.AddSingleton<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddForgeSettings();
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return (scope.ServiceProvider.GetRequiredService<SettingsService>(),
                provider.GetRequiredService<CurrentTenant>(),
                scope.ServiceProvider.GetRequiredService<OperationalFlags>());
    }

    [Fact]
    public async Task Precedence_is_user_over_tenant_over_application_over_default()
    {
        var (settings, tenant, _) = Build();
        var ct = TestContext.Current.CancellationToken;
        tenant.SetTenant("t1");

        Assert.Equal(20, await settings.GetAsync(PageSize, "u1", ct)); // default

        using (tenant.BeginHostScope())
        {
            await settings.SetAsync(PageSize, SettingScope.Application, null, 50, ct);
        }

        Assert.Equal(50, await settings.GetAsync(PageSize, "u1", ct));

        await settings.SetAsync(PageSize, SettingScope.Tenant, null, 80, ct);
        Assert.Equal(80, await settings.GetAsync(PageSize, "u1", ct));

        await settings.SetAsync(PageSize, SettingScope.User, "u1", 10, ct);
        Assert.Equal(10, await settings.GetAsync(PageSize, "u1", ct));
        Assert.Equal(80, await settings.GetAsync(PageSize, "u2", ct)); // other user gets tenant value
    }

    [Fact]
    public async Task Validation_rejects_bad_values_at_write_time()
    {
        var (settings, tenant, _) = Build();
        tenant.SetTenant("t1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            settings.SetAsync(PageSize, SettingScope.Tenant, null, 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Secret_like_keys_are_rejected_everywhere()
    {
        var (settings, tenant, _) = Build();
        tenant.SetTenant("t1");
        var sneaky = new SettingDefinition<string>("Smtp:Password", "");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            settings.GetAsync(sneaky, null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            settings.SetAsync(sneaky, SettingScope.Tenant, null, "hunter2", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Tenant_values_are_cached_per_tenant_without_bleed()
    {
        var (settings, tenant, _) = Build();
        var ct = TestContext.Current.CancellationToken;

        tenant.SetTenant("t1");
        await settings.SetAsync(PageSize, SettingScope.Tenant, null, 33, ct);
        Assert.Equal(33, await settings.GetAsync(PageSize, null, ct)); // now cached for t1

        tenant.SetTenant("t2");
        Assert.Equal(20, await settings.GetAsync(PageSize, null, ct)); // t2 must not see t1's cached value
    }

    [Fact]
    public async Task Write_invalidates_the_cache_immediately()
    {
        var (settings, tenant, _) = Build();
        var ct = TestContext.Current.CancellationToken;
        tenant.SetTenant("t1");

        await settings.SetAsync(PageSize, SettingScope.Tenant, null, 40, ct);
        Assert.Equal(40, await settings.GetAsync(PageSize, null, ct));

        await settings.SetAsync(PageSize, SettingScope.Tenant, null, 60, ct);
        Assert.Equal(60, await settings.GetAsync(PageSize, null, ct)); // no stale 40 from cache
    }

    [Fact]
    public async Task Operational_flags_default_off_and_support_tenant_override()
    {
        var (_, tenant, flags) = Build();
        var ct = TestContext.Current.CancellationToken;
        tenant.SetTenant("t1");
        var flag = new OperationalFlag("new-dispatcher");

        Assert.False(await flags.IsEnabledAsync(flag, ct));

        await flags.SetAsync(flag, SettingScope.Tenant, true, ct);
        Assert.True(await flags.IsEnabledAsync(flag, ct));

        tenant.SetTenant("t2");
        Assert.False(await flags.IsEnabledAsync(flag, ct)); // rollout is per tenant
    }

    [Fact]
    public async Task Environment_secret_store_reads_from_environment_only()
    {
        Environment.SetEnvironmentVariable("FORGE_TEST_SECRET", "shh");
        try
        {
            var store = new EnvironmentSecretStore();
            Assert.Equal("shh", await store.GetSecretAsync("FORGE_TEST_SECRET", TestContext.Current.CancellationToken));
            Assert.Null(await store.GetSecretAsync("FORGE_TEST_SECRET_MISSING", TestContext.Current.CancellationToken));
        }
        finally
        {
            Environment.SetEnvironmentVariable("FORGE_TEST_SECRET", null);
        }
    }
}
