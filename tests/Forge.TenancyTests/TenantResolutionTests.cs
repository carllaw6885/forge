using Forge.Tenancy;
using Forge.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Forge.TenancyTests;

/// <summary>Deny-by-default resolution over HTTP (ADR 05), with and without an authoritative directory.</summary>
public class TenantResolutionTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static async Task<(WebApplication App, HttpClient Client)> BuildAsync(ITenantDirectory? directory)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddForgeTenancy();
        if (directory is not null)
        {
            builder.Services.AddSingleton(directory);
        }

        var app = builder.Build();
        app.UseRouting();
        app.UseForgeTenancy();
        app.MapGet("/tenant", (ICurrentTenant tenant) => tenant.Id!);
        await app.StartAsync(Ct);
        return (app, app.GetTestClient());
    }

    private static HttpRequestMessage Request(string? tenant)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/tenant");
        if (tenant is not null)
        {
            request.Headers.Add(HeaderTenantResolver.HeaderName, tenant);
        }

        return request;
    }

    [Fact]
    public async Task Without_a_directory_ids_are_opaque_and_missing_tenant_is_rejected()
    {
        var (app, client) = await BuildAsync(null);
        await using var _ = app;

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await client.SendAsync(Request(null), Ct)).StatusCode);
        var ok = await client.SendAsync(Request("anything"), Ct);
        Assert.Equal("anything", await ok.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task With_a_directory_unknown_and_disabled_tenants_fail_resolution()
    {
        var directory = new InMemoryTenantDirectory();
        await directory.SaveAsync(new Tenant("live", "Live", Enabled: true, DateTimeOffset.UnixEpoch), Ct);
        await directory.SaveAsync(new Tenant("off", "Off", Enabled: false, DateTimeOffset.UnixEpoch), Ct);
        var (app, client) = await BuildAsync(directory);
        await using var _ = app;

        var ok = await client.SendAsync(Request("live"), Ct);
        Assert.Equal("live", await ok.Content.ReadAsStringAsync(Ct));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await client.SendAsync(Request("unknown"), Ct)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await client.SendAsync(Request("off"), Ct)).StatusCode);
    }
}
