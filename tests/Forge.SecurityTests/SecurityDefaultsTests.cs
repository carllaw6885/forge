using System.Net;
using Forge.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Forge.SecurityTests;

public class SecurityDefaultsTests
{
    private static async Task<WebApplication> BuildAppAsync(
        string environment, Dictionary<string, string?>? settings = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings ?? []);
        builder.Services.AddForgeSecurityDefaults(builder.Configuration);

        var app = builder.Build();
        app.UseForgeSecurityDefaults();
        app.MapGet("/ping", () => Results.Ok("pong"));
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Hardening_headers_and_csp_shell_are_applied()
    {
        await using var app = await BuildAppAsync(Environments.Development);
        var response = await app.GetTestClient().GetAsync("/ping", TestContext.Current.CancellationToken);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Production_with_wildcard_cors_refuses_startup()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => BuildAppAsync(
            Environments.Production,
            new Dictionary<string, string?> { ["Security:Cors:Origins:0"] = "*" }));

        Assert.Contains("must not contain '*'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_with_https_disabled_refuses_startup()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => BuildAppAsync(
            Environments.Production,
            new Dictionary<string, string?> { ["Security:RequireHttps"] = "false" }));

        Assert.Contains("RequireHttps", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Safe_production_configuration_starts_and_sends_hsts()
    {
        await using var app = await BuildAppAsync(Environments.Production);
        // HSTS excludes localhost by design; use a routable-looking host
        app.GetTestServer().BaseAddress = new Uri("https://forge.example");
        var response = await app.GetTestClient().GetAsync("/ping", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("max-age", response.Headers.GetValues("Strict-Transport-Security").Single());
    }

    [Fact]
    public async Task Development_tolerates_unsafe_settings_without_refusing_startup()
    {
        await using var app = await BuildAppAsync(
            Environments.Development,
            new Dictionary<string, string?> { ["Security:Cors:Origins:0"] = "*" });

        Assert.True((await app.GetTestClient().GetAsync("/ping", TestContext.Current.CancellationToken)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Rate_limit_hook_rejects_beyond_the_configured_permit()
    {
        await using var app = await BuildAppAsync(
            Environments.Development,
            new Dictionary<string, string?>
            {
                ["Security:RateLimit:PermitLimit"] = "2",
                ["Security:RateLimit:WindowSeconds"] = "60",
            });
        var client = app.GetTestClient();
        var ct = TestContext.Current.CancellationToken;

        Assert.True((await client.GetAsync("/ping", ct)).IsSuccessStatusCode);
        Assert.True((await client.GetAsync("/ping", ct)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/ping", ct)).StatusCode);
    }
}
