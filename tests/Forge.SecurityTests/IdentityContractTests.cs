using System.Security.Claims;
using Forge.Auditing;
using Forge.Identity;
using Forge.Security;
using Forge.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.SecurityTests;

/// <summary>
/// The identity application contract (ADR 40) enforces authentication,
/// permission and host scope inside, and audits denials and mutations.
/// </summary>
public class IdentityContractTests(IdentityFixture fx) : IClassFixture<IdentityFixture>
{
    private void RequireServer() =>
        Assert.SkipWhen(fx.UnavailableReason is not null, $"SQL Server container unavailable: {fx.UnavailableReason}");

    /// <summary>A scope acting as <paramref name="userName"/> with the given roles, in host scope unless told otherwise.</summary>
    private IServiceScope ActingAs(string? userName, string[] roles, bool hostScope = true)
    {
        var scope = fx.App.Services.CreateScope();
        var identity = userName is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, userName), .. roles.Select(r => new Claim(ClaimTypes.Role, r))], "test");
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = new ClaimsPrincipal(identity), RequestServices = scope.ServiceProvider };
        if (hostScope)
        {
            scope.ServiceProvider.GetRequiredService<CurrentTenant>().BeginHostScope();
        }

        return scope;
    }

    private async Task SeedAdminRoleAsync()
    {
        using var scope = fx.App.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<ForgeIdentityDbContext>();
        if (await roles.FindByNameAsync("contract-admin") is null)
        {
            Assert.True((await roles.CreateAsync(new IdentityRole("contract-admin"))).Succeeded);
            db.RolePermissions.AddRange(IdentityPermissions.All.Select(p =>
                new RolePermission { RoleName = "contract-admin", PermissionName = p.Name }));
            await db.SaveChangesAsync();
        }
    }

    private static async Task<IReadOnlyList<AuditRecord>> AuditAsync(IServiceScope scope, string action) =>
        [.. (await scope.ServiceProvider.GetRequiredService<IAuditStore>().ReadAllAsync(CancellationToken.None))
            .Where(r => r.Event.Action == action)];

    [Fact]
    public async Task Anonymous_caller_is_denied_and_audited()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;
        using var scope = ActingAs(null, []);

        var result = await scope.ServiceProvider.GetRequiredService<IUserAdministration>().ListAsync(10, ct);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.Denied, result.Error.Code);
        var denial = Assert.Single(await AuditAsync(scope, SecurityEvents.AuthorizationDenied),
            r => r.Event.Actor == "anonymous" && r.Event.Details["reason"] == "unauthenticated");
        Assert.Equal("denied", denial.Event.Outcome);
    }

    [Fact]
    public async Task Missing_permission_is_denied_and_audited()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;
        using var scope = ActingAs("reader", ["nobody"]);

        var result = await scope.ServiceProvider.GetRequiredService<IUserAdministration>()
            .CreateAsync("should-not-exist", "Str0ng!Password!42", ct);

        Assert.Equal(IdentityErrors.Denied, result.Error.Code);
        Assert.Contains(await AuditAsync(scope, SecurityEvents.AuthorizationDenied),
            r => r.Event.Actor == "reader" && r.Event.Details["reason"] == "permission:" + IdentityPermissions.UsersManage);
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ForgeUser>>().FindByNameAsync("should-not-exist"));
    }

    [Fact]
    public async Task Tenant_scope_is_denied_for_host_owned_identity_data()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;
        await SeedAdminRoleAsync();
        using var scope = ActingAs("tenant-admin", ["contract-admin"], hostScope: false);
        scope.ServiceProvider.GetRequiredService<CurrentTenant>().SetTenant("t1");

        var result = await scope.ServiceProvider.GetRequiredService<IUserAdministration>().ListAsync(10, ct);

        Assert.Equal(IdentityErrors.Denied, result.Error.Code);
        Assert.Contains(await AuditAsync(scope, SecurityEvents.AuthorizationDenied),
            r => r.Event.Actor == "tenant-admin" && r.Event.TenantId == "t1" && r.Event.Details["reason"] == "scope:Tenant");
    }

    [Fact]
    public async Task Permitted_administrator_creates_users_roles_and_grants_with_audit()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;
        await SeedAdminRoleAsync();
        using var scope = ActingAs("root", ["contract-admin"]);
        var users = scope.ServiceProvider.GetRequiredService<IUserAdministration>();
        var roles = scope.ServiceProvider.GetRequiredService<IRoleAdministration>();

        Assert.True((await roles.CreateAsync("editor", ct)).IsSuccess);
        Assert.True((await roles.GrantPermissionAsync("editor", "Catalog.Items.Create", ct)).IsSuccess);
        Assert.True((await users.CreateAsync("bob", "Str0ng!Password!42", ct)).IsSuccess);
        Assert.True((await users.AssignRoleAsync("bob", "editor", ct)).IsSuccess);

        var listed = Assert.Single((await users.ListAsync(50, ct)).Value, u => u.UserName == "bob");
        Assert.Equal(["editor"], listed.Roles);
        var role = Assert.Single((await roles.ListAsync(ct)).Value, r => r.Name == "editor");
        Assert.Equal(["Catalog.Items.Create"], role.Permissions);

        foreach (var action in new[]
        {
            IdentityAuditActions.RoleCreated, IdentityAuditActions.PermissionGranted,
            IdentityAuditActions.UserCreated, IdentityAuditActions.RoleAssigned,
        })
        {
            Assert.Contains(await AuditAsync(scope, action), r => r.Event.Actor == "root" && r.Event.Outcome == "success");
        }

        // not-found and validation failures are typed, not exceptional
        Assert.Equal(IdentityErrors.NotFound, (await users.AssignRoleAsync("ghost", "editor", ct)).Error.Code);
        Assert.Equal(IdentityErrors.Invalid, (await users.CreateAsync("weak", "short", ct)).Error.Code);
    }
}
