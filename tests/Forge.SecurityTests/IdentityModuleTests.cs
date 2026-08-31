using System.Net.Http.Json;
using System.Security.Claims;
using Forge.Identity;
using Forge.Modularity;
using Forge.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Testcontainers.MsSql;
using Xunit;

namespace Forge.SecurityTests;

/// <summary>The identity reference module against real SQL Server: users, role-aggregated permissions, token issuance.</summary>
public sealed class IdentityFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    public WebApplication App { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
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

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddForge(new IdentityModule(_container.GetConnectionString()));

        App = builder.Build();
        App.Services.UseForge();
        App.MapIdentityEndpoints();
        await App.StartAsync();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ForgeIdentityDbContext>();
            // ponytail: CreateTables from the compiled model; hand-rolled or
            // generated migrations land with release engineering (Phase 5).
            await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();

            await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>()
                .CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "test-client",
                    ClientSecret = "test-secret-with-plenty-of-entropy",
                    DisplayName = "Test client",
                    Permissions =
                    {
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                    },
                });
        }

        Client = App.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        if (App is not null)
        {
            await App.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

public class IdentityModuleTests(IdentityFixture fx) : IClassFixture<IdentityFixture>
{
    private void RequireServer() =>
        Assert.SkipWhen(fx.UnavailableReason is not null, $"SQL Server container unavailable: {fx.UnavailableReason}");

    [Fact]
    public async Task User_role_and_permission_round_trip_through_the_store()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;
        using var scope = fx.App.Services.CreateScope();

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ForgeUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<ForgeIdentityDbContext>();

        Assert.True((await users.CreateAsync(new ForgeUser { UserName = "alice" }, "Str0ng!Password!42")).Succeeded);
        Assert.True((await roles.CreateAsync(new IdentityRole("editor"))).Succeeded);
        var alice = (await users.FindByNameAsync("alice"))!;
        Assert.True((await users.AddToRoleAsync(alice, "editor")).Succeeded);

        db.RolePermissions.Add(new RolePermission { RoleName = "editor", PermissionName = "Catalog.Items.Create" });
        await db.SaveChangesAsync(ct);

        var checker = scope.ServiceProvider.GetRequiredService<IPermissionChecker>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "alice"), new Claim(ClaimTypes.Role, "editor")], "test"));

        Assert.True(await checker.HasAsync(principal, "Catalog.Items.Create", ct));
        Assert.False(await checker.HasAsync(principal, "Catalog.Items.Delete", ct));
    }

    [Fact]
    public async Task Client_credentials_flow_issues_an_access_token()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        var response = await fx.Client.PostAsync("/connect/token", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", "test-client"),
            new KeyValuePair<string, string>("client_secret", "test-secret-with-plenty-of-entropy"),
        ]), ct);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(ct));
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(ct);
        Assert.True(payload!.ContainsKey("access_token"));
        Assert.Equal("Bearer", payload["token_type"].ToString());
    }

    [Fact]
    public async Task Wrong_client_secret_is_rejected()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        var response = await fx.Client.PostAsync("/connect/token", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", "test-client"),
            new KeyValuePair<string, string>("client_secret", "wrong"),
        ]), ct);

        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Directory_link_seam_stores_external_identity_shape()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;
        using var scope = fx.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ForgeIdentityDbContext>();

        db.DirectoryLinks.Add(new DirectoryLink
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Provider = "entra",
            ExternalId = "ext-123",
        });
        await db.SaveChangesAsync(ct);

        var link = Assert.Single(await db.DirectoryLinks.AsNoTracking().ToListAsync(ct));
        Assert.Equal("linked", link.SyncState);
    }
}
