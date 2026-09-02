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

    [Fact]
    public async Task Anonymous_account_operations_are_denied()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;
        using var scope = ActingAs(null, []);
        var account = scope.ServiceProvider.GetRequiredService<IAccountOperations>();

        Assert.Equal(IdentityErrors.Denied, (await account.MeAsync(ct)).Error.Code);
        Assert.Equal(IdentityErrors.Denied, (await account.ChangePasswordAsync("x", "Str0ng!Password!42", ct)).Error.Code);
    }

    [Fact]
    public async Task Signed_in_user_sees_self_and_changes_password_with_audit()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;
        await SeedAdminRoleAsync();
        using (var admin = ActingAs("root", ["contract-admin"]))
        {
            var users = admin.ServiceProvider.GetRequiredService<IUserAdministration>();
            Assert.True((await users.CreateAsync("alice", "Str0ng!Password!42", ct)).IsSuccess);
            Assert.True((await users.AssignRoleAsync("alice", "contract-admin", ct)).IsSuccess);
        }

        // the principal Identity itself issues (NameIdentifier claim); no permission needed for own account
        using var scope = fx.App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ForgeUser>>();
        var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ForgeUser>>()
            .CreateAsync((await manager.FindByNameAsync("alice"))!);
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = principal, RequestServices = scope.ServiceProvider };
        var account = scope.ServiceProvider.GetRequiredService<IAccountOperations>();

        var me = (await account.MeAsync(ct)).Value;
        Assert.Equal("alice", me.UserName);
        Assert.Equal(["contract-admin"], me.Roles);

        Assert.Equal(IdentityErrors.Invalid, (await account.ChangePasswordAsync("wrong", "N3w!Password!42", ct)).Error.Code);
        Assert.True((await account.ChangePasswordAsync("Str0ng!Password!42", "N3w!Password!42", ct)).IsSuccess);
        Assert.Contains(await AuditAsync(scope, IdentityAuditActions.PasswordChanged),
            r => r.Event.Actor == "alice" && r.Event.Subject == "alice" && r.Event.Outcome == "success");
        Assert.True(await manager.CheckPasswordAsync((await manager.FindByNameAsync("alice"))!, "N3w!Password!42"));
    }

    [Fact]
    public async Task Password_sign_in_hides_user_existence_locks_out_and_sets_the_cookie()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;
        await SeedAdminRoleAsync();
        using (var admin = ActingAs("root", ["contract-admin"]))
        {
            Assert.True((await admin.ServiceProvider.GetRequiredService<IUserAdministration>()
                .CreateAsync("lockme", "Str0ng!Password!42", ct)).IsSuccess);
        }

        using var scope = ActingAs(null, []);
        var signIn = scope.ServiceProvider.GetRequiredService<ISignInOperations>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext!;

        // unknown user and wrong password: same outcome, both audited as failures
        Assert.Equal(SignInOutcome.Failed, await signIn.PasswordSignInAsync("nobody", "whatever", ct));
        Assert.Equal(SignInOutcome.Failed, await signIn.PasswordSignInAsync("lockme", "wrong", ct));
        Assert.Equal(2, (await AuditAsync(scope, IdentityAuditActions.SignInFailed)).Count);

        Assert.Equal(SignInOutcome.Succeeded, await signIn.PasswordSignInAsync("lockme", "Str0ng!Password!42", ct));
        Assert.Contains(http.Response.Headers.SetCookie, c => c!.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
        Assert.Single(await AuditAsync(scope, IdentityAuditActions.SignedIn), r => r.Event.Subject == "lockme");

        // default Identity lockout: five failures lock the account; the right password no longer helps
        for (var i = 0; i < 5; i++)
        {
            await signIn.PasswordSignInAsync("lockme", "wrong", ct);
        }

        Assert.Equal(SignInOutcome.LockedOut, await signIn.PasswordSignInAsync("lockme", "Str0ng!Password!42", ct));
    }

    [Fact]
    public async Task Sign_out_everywhere_else_rotates_the_security_stamp_with_audit()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;
        await SeedAdminRoleAsync();
        using (var admin = ActingAs("root", ["contract-admin"]))
        {
            Assert.True((await admin.ServiceProvider.GetRequiredService<IUserAdministration>()
                .CreateAsync("carol", "Str0ng!Password!42", ct)).IsSuccess);
        }

        using (var anonymous = ActingAs(null, []))
        {
            var denied = await anonymous.ServiceProvider.GetRequiredService<ISignInOperations>().SignOutEverywhereElseAsync(ct);
            Assert.Equal(IdentityErrors.Denied, denied.Error.Code);
        }

        using var scope = fx.App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ForgeUser>>();
        var carol = (await manager.FindByNameAsync("carol"))!;
        var before = await manager.GetSecurityStampAsync(carol);
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ForgeUser>>().CreateAsync(carol),
            RequestServices = scope.ServiceProvider,
        };

        var revoked = await scope.ServiceProvider.GetRequiredService<ISignInOperations>().SignOutEverywhereElseAsync(ct);
        Assert.True(revoked.IsSuccess, revoked.IsFailure ? revoked.Error.Message : "");

        Assert.NotEqual(before, await manager.GetSecurityStampAsync((await manager.FindByNameAsync("carol"))!));
        Assert.Single(await AuditAsync(scope, IdentityAuditActions.SessionsRevoked), r => r.Event.Actor == "carol");
    }
}
