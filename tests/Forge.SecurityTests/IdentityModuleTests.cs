using System.Net.Http.Json;
using System.Security.Claims;
using Forge.Identity;
using Forge.Modularity;
using Forge.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
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

    private static string SelfSignedPfx(string subject)
    {
        Environment.SetEnvironmentVariable("FORGE_TEST_PFX_PASSWORD", "test-pfx-password");
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            $"CN={subject}", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var path = Path.Combine(Path.GetTempPath(), $"{subject}-{Guid.NewGuid():N}.pfx");
        File.WriteAllBytes(path, certificate.Export(
            System.Security.Cryptography.X509Certificates.X509ContentType.Pfx, "test-pfx-password"));
        return path;
    }

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

        // run the whole identity suite on the persisted-certificate path
        var signing = SelfSignedPfx("forge-test-signing");
        var encryption = SelfSignedPfx("forge-test-encryption");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddForge(new IdentityModule(
            _container.GetConnectionString(),
            new IdentityKeyMaterial(signing, "FORGE_TEST_PFX_PASSWORD"),
            new IdentityKeyMaterial(encryption, "FORGE_TEST_PFX_PASSWORD")));

        // cookie sign-in exactly as the admin template wires it — the cookie
        // handler needs ISecurityStampValidator, which AddIdentityCore omits
        builder.Services.AddHttpContextAccessor(); // the admin shell registers this in real apps
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();
        builder.Services.AddScoped<SignInManager<ForgeUser>>();
        builder.Services.AddScoped<ISecurityStampValidator, SecurityStampValidator<ForgeUser>>();

        App = builder.Build();
        App.Services.UseForge();
        App.MapIdentityEndpoints();
        App.MapPost("/test/login", async (SignInManager<ForgeUser> signIn, UserManager<ForgeUser> users) =>
        {
            await signIn.SignInAsync((await users.FindByNameAsync("cookie-user"))!, isPersistent: false);
            return Results.Ok();
        });
        App.MapGet("/test/me", (ClaimsPrincipal user) =>
            user.Identity?.IsAuthenticated == true ? Results.Ok(user.Identity.Name) : Results.Unauthorized());
        await App.StartAsync();

        // OpenIddict rightly refuses plain HTTP; keep transport security ON in
        // the module and present the in-memory server as HTTPS instead.
        App.GetTestServer().BaseAddress = new Uri("https://localhost");

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ForgeIdentityDbContext>();
            await db.Database.MigrateAsync();

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
    public async Task Cookie_sign_in_survives_the_next_authenticated_request()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        using (var scope = fx.App.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ForgeUser>>();
            if (await users.FindByNameAsync("cookie-user") is null)
            {
                Assert.True((await users.CreateAsync(new ForgeUser { UserName = "cookie-user" }, "Str0ng!Password!42")).Succeeded);
            }
        }

        var login = await fx.Client.PostAsync("/test/login", content: null, ct);
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync(ct));
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie")).Split(';')[0];

        // the request after login is where a missing ISecurityStampValidator explodes
        using var request = new HttpRequestMessage(HttpMethod.Get, "/test/me");
        request.Headers.Add("Cookie", cookie);
        var me = await fx.Client.SendAsync(request, ct);
        Assert.True(me.IsSuccessStatusCode, await me.Content.ReadAsStringAsync(ct));
        Assert.Contains("cookie-user", await me.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
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
