using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Forge.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Forge.SecurityTests;

/// <summary>Permission policies on real endpoints: anonymous 401, missing permission 403, direct claim or role both 200.</summary>
public sealed class AuthTestFixture : IAsyncLifetime
{
    public WebApplication App { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    internal sealed class HeaderClaimsHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var user))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim> { new(ClaimTypes.Name, user.ToString()) };
            if (Request.Headers.TryGetValue("X-Test-Permission", out var permission))
            {
                claims.Add(new Claim(ForgeClaimTypes.Permission, permission.ToString()));
            }

            if (Request.Headers.TryGetValue("X-Test-Role", out var role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, HeaderClaimsHandler>("Test", null);
        builder.Services.AddForgePermissions();
        builder.Services.AddSingleton<IRolePermissionMap>(
            new InMemoryRolePermissionMap().Grant("editor", "Widgets.Write"));

        App = builder.Build();
        App.MapPost("/widgets", () => Results.Ok("written")).RequirePermission("Widgets.Write");
        await App.StartAsync();
        Client = App.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await App.DisposeAsync();
}

public class EndpointAuthorizationTests(AuthTestFixture fx) : IClassFixture<AuthTestFixture>
{
    private async Task<HttpStatusCode> PostAsync(params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/widgets");
        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }

        return (await fx.Client.SendAsync(request, TestContext.Current.CancellationToken)).StatusCode;
    }

    [Fact]
    public async Task Anonymous_is_challenged() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await PostAsync());

    [Fact]
    public async Task Authenticated_without_permission_is_forbidden() =>
        Assert.Equal(HttpStatusCode.Forbidden, await PostAsync(("X-Test-User", "u1")));

    [Fact]
    public async Task Direct_permission_claim_is_allowed() =>
        Assert.Equal(HttpStatusCode.OK, await PostAsync(("X-Test-User", "u1"), ("X-Test-Permission", "Widgets.Write")));

    [Fact]
    public async Task Role_aggregated_permission_is_allowed() =>
        Assert.Equal(HttpStatusCode.OK, await PostAsync(("X-Test-User", "u1"), ("X-Test-Role", "editor")));

    [Fact]
    public async Task Wrong_role_is_forbidden() =>
        Assert.Equal(HttpStatusCode.Forbidden, await PostAsync(("X-Test-User", "u1"), ("X-Test-Role", "viewer")));
}
