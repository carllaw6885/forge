using Forge.Admin.Blazor;
using Forge.Identity;
using Forge.Jobs;
using Forge.Localization;
using Forge.Modularity;
using Forge.ReferenceCatalog;
using Forge.Security;
using Forge.Settings;
using Forge.Tenancy;
using Forge.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Testcontainers.MsSql;
using Xunit;

namespace Forge.AccessibilityTests;

/// <summary>
/// Runs the composed admin shell on real Kestrel and drives it with headless
/// Chromium — the WCAG gate needs rendered pixels, not markup assertions.
/// </summary>
public sealed class AdminShellFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private WebApplication? _app;
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;
    public string BaseUrl { get; private set; } = "";
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
            return;
        }

        var cs = _container.GetConnectionString();
        // Development: static web assets from referenced libraries (the shell's stylesheet)
        // are only mapped from the build manifest in this environment
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddProblemDetails();
        builder.Services.AddForgeTenancy();
        builder.Services.AddForgeSettings();
        builder.Services.AddForgeLocalization();
        builder.Services.AddForgeAdminShell();
        builder.Services.AddSingleton<ImpersonationService>();
        builder.Services.AddSingleton<IImpersonationContext>(sp => sp.GetRequiredService<ImpersonationService>());
        builder.Services.AddSingleton<ITerminalFailureSink, InMemoryTerminalFailureSink>();
        builder.Services.AddForge(new CatalogModule(cs), new IdentityModule(cs));

        _app = builder.Build();
        _app.Services.UseForge();
        _app.UseStaticFiles();
        _app.UseForgeTenancy();
        _app.UseForgeRequestCulture();
        _app.UseAntiforgery();
        _app.MapForgeAdminShell(); // tenant-scoped: the banner must show the tenant
        await _app.StartAsync();
        BaseUrl = _app.Urls.First();

        using (var scope = _app.Services.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<CurrentTenant>().BeginHostScope())
        {
            await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<ForgeIdentityDbContext>()
                .GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        }

        // seed the RTL tenant's culture
        using (var scope = _app.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<CurrentTenant>().SetTenant("tenant-ar");
            await scope.ServiceProvider.GetRequiredService<SettingsService>()
                .SetAsync(LocalizationSettings.Culture, SettingScope.Tenant, null, "ar-SA", CancellationToken.None);
        }

        Microsoft.Playwright.Program.Main(["install", "chromium"]);
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync();
    }

    public async Task<IPage> NewPageAsync(string tenant = "tenant-a")
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Tenant"] = tenant },
        });
        return await context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
