using System.Net;
using System.Net.Http.Json;
using Forge.Audit.Api;
using Forge.Auditing;
using Forge.Core.Primitives;
using Forge.Identity;
using Forge.Identity.Api;
using Forge.Security;
using Forge.Tenancy;
using Forge.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.SecurityTests;

/// <summary>
/// The .Api packages are thin projections (ADR 40): they add bearer-only
/// authentication and Problem Details on top of the application contracts, and
/// nothing else. Contract behaviour itself is proven in the contract tests.
/// </summary>
public sealed class ApiProjectionTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private InMemoryAuditStore _audit = null!;

    // fakes stand in for the SQL-backed identity contract; the projection only maps results
    private sealed class FakeUsers : IUserAdministration
    {
        public Task<Result<IReadOnlyList<UserSummary>>> ListAsync(int take, CancellationToken ct) =>
            Task.FromResult(Result.Success<IReadOnlyList<UserSummary>>([new UserSummary("alice", ["admin"])]));

        public Task<Result> CreateAsync(string userName, string password, CancellationToken ct) =>
            Task.FromResult(password.Length < 8
                ? Result.Failure(new Error(IdentityErrors.Invalid, "Password too short."))
                : Result.Success());

        public Task<Result> AssignRoleAsync(string userName, string role, CancellationToken ct) =>
            Task.FromResult(Result.Failure(new Error(IdentityErrors.NotFound, $"No user '{userName}'.")));
    }

    private sealed class Denied : IRoleAdministration
    {
        private static Task<Result<T>> Deny<T>() => Task.FromResult(Result.Failure<T>(new Error(IdentityErrors.Denied, "Not permitted.")));
        private static Task<Result> Deny() => Task.FromResult(Result.Failure(new Error(IdentityErrors.Denied, "Not permitted.")));

        public Task<Result<IReadOnlyList<RoleSummary>>> ListAsync(CancellationToken ct) => Deny<IReadOnlyList<RoleSummary>>();
        public Task<Result> CreateAsync(string role, CancellationToken ct) => Deny();
        public Task<Result> GrantPermissionAsync(string role, string permission, CancellationToken ct) => Deny();
    }

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, AuthTestFixture.HeaderClaimsHandler>("Test", null);
        builder.Services.AddForgePermissions();
        builder.Services.AddForgeTenancy();
        _audit = new InMemoryAuditStore(new DefaultAuditRedactionPolicy());
        builder.Services.AddSingleton<IAuditStore>(_audit);
        builder.Services.AddScoped<IUserAdministration, FakeUsers>();
        builder.Services.AddScoped<IRoleAdministration, Denied>();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseForgeTenancy();
        _app.MapForgeIdentityApi(authenticationScheme: "Test");
        _app.MapForgeAuditApi(authenticationScheme: "Test").WithHostScope();
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static HttpRequestMessage Request(HttpMethod method, string path, object? body = null, params (string, string)[] headers)
    {
        var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }

        return request;
    }

    [Fact]
    public async Task Anonymous_callers_get_401_not_a_sign_in_redirect()
    {
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/identity/users"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Contract_results_become_json_or_problem_details_keyed_by_error_code()
    {
        var ok = await _client.SendAsync(Request(HttpMethod.Get, "/api/identity/users", null, ("X-Test-User", "alice")), Ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("alice", (await ok.Content.ReadFromJsonAsync<List<UserSummary>>(Ct))!.Single().UserName);

        var invalid = await _client.SendAsync(
            Request(HttpMethod.Post, "/api/identity/users", new CreateUserRequest("bob", "short"), ("X-Test-User", "alice")), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var problem = await invalid.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal(IdentityErrors.Invalid, problem!.Type);
        Assert.Equal("Password too short.", problem.Title);

        var missing = await _client.SendAsync(
            Request(HttpMethod.Post, "/api/identity/users/nobody/roles", new AssignRoleRequest("admin"), ("X-Test-User", "alice")), Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var denied = await _client.SendAsync(Request(HttpMethod.Get, "/api/identity/roles", null, ("X-Test-User", "alice")), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Audit_api_enforces_permissions_inside_the_contract_and_runs_host_scoped()
    {
        var denied = await _client.SendAsync(Request(HttpMethod.Post, "/api/audit/verify", null, ("X-Test-User", "alice")), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(AuditErrors.Denied, (await denied.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Type);

        var verified = await _client.SendAsync(Request(HttpMethod.Post, "/api/audit/verify", null,
            ("X-Test-User", "alice"), ("X-Test-Permission", AuditPermissions.Verify), ("X-Tenant", "t1")), Ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.True((await verified.Content.ReadFromJsonAsync<AuditChainStatus>(Ct))!.IsIntact);

        var listed = await _client.SendAsync(Request(HttpMethod.Get, $"/api/audit?action={AuditActions.Verified}", null,
            ("X-Test-User", "alice"), ("X-Test-Permission", AuditPermissions.Read)), Ct);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Single((await listed.Content.ReadFromJsonAsync<List<AuditRecord>>(Ct))!);
    }
}
