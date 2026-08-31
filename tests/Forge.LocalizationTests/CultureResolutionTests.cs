using System.Globalization;
using Forge.Localization;
using Forge.Settings;
using Forge.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.LocalizationTests;

public class CultureResolutionTests
{
    private static (CultureResolver Resolver, SettingsService Settings, CurrentTenant Tenant) Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<CurrentTenant>();
        services.AddSingleton<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddForgeSettings();
        services.AddForgeLocalization();
        var scope = services.BuildServiceProvider().CreateScope();
        return (scope.ServiceProvider.GetRequiredService<CultureResolver>(),
                scope.ServiceProvider.GetRequiredService<SettingsService>(),
                scope.ServiceProvider.GetRequiredService<CurrentTenant>());
    }

    [Fact]
    public async Task Application_default_is_en_GB()
    {
        var (resolver, _, tenant) = Build();
        tenant.SetTenant("t1");

        var culture = await resolver.ResolveCultureAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal("en-GB", culture.Name);
        Assert.False(culture.TextInfo.IsRightToLeft);
    }

    [Fact]
    public async Task Tenant_override_wins_over_default_and_user_over_tenant()
    {
        var (resolver, settings, tenant) = Build();
        var ct = TestContext.Current.CancellationToken;
        tenant.SetTenant("t1");

        await settings.SetAsync(LocalizationSettings.Culture, SettingScope.Tenant, null, "ar-SA", ct);
        var tenantCulture = await resolver.ResolveCultureAsync(null, ct);
        Assert.Equal("ar-SA", tenantCulture.Name);
        Assert.True(tenantCulture.TextInfo.IsRightToLeft); // the RTL acceptance path (ADR 12)

        await settings.SetAsync(LocalizationSettings.Culture, SettingScope.User, "u1", "fr-FR", ct);
        Assert.Equal("fr-FR", (await resolver.ResolveCultureAsync("u1", ct)).Name);
        Assert.Equal("ar-SA", (await resolver.ResolveCultureAsync("u2", ct)).Name);
    }

    [Fact]
    public async Task Time_zone_resolution_and_conversion_are_deterministic()
    {
        var (resolver, settings, tenant) = Build();
        var ct = TestContext.Current.CancellationToken;
        tenant.SetTenant("t1");
        await settings.SetAsync(LocalizationSettings.TimeZone, SettingScope.Tenant, null, "Asia/Riyadh", ct);

        var zone = await resolver.ResolveTimeZoneAsync(null, ct);
        var display = ForgeTime.ToDisplay(new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero), zone);

        Assert.Equal(TimeSpan.FromHours(3), display.Offset); // Riyadh is UTC+3, no DST — deterministic
        Assert.Equal(15, display.Hour);
    }

    [Fact]
    public async Task Culture_and_time_zone_are_distinct_settings()
    {
        var (resolver, settings, tenant) = Build();
        var ct = TestContext.Current.CancellationToken;
        tenant.SetTenant("t1");
        await settings.SetAsync(LocalizationSettings.Culture, SettingScope.Tenant, null, "ar-SA", ct);

        // culture changed, time zone stays at the application default
        Assert.Equal("ar-SA", (await resolver.ResolveCultureAsync(null, ct)).Name);
        Assert.Equal(TimeZoneInfo.Utc, await resolver.ResolveTimeZoneAsync(null, ct));
    }
}
