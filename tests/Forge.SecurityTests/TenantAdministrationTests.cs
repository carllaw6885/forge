using System.Security.Claims;
using Forge.Auditing;
using Forge.Security;
using Forge.Tenancy;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Forge.SecurityTests;

public class TenantAdministrationTests
{
    private sealed record Scope(string? Id, TenantScope Scope_) : ICurrentTenant
    {
        string? ICurrentTenant.Id => Id;
        TenantScope ICurrentTenant.Scope => Scope_;
    }

    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static (TenantAdministration Admin, InMemoryTenantDirectory Directory, InMemoryAuditStore Audit) Build(
        ICurrentTenant? tenant, bool withDirectory = true, params string[] permissions)
    {
        var audit = new InMemoryAuditStore(new DefaultAuditRedactionPolicy());
        var directory = new InMemoryTenantDirectory();
        var claims = permissions.Select(p => new Claim(ForgeClaimTypes.Permission, p))
            .Prepend(new Claim(ClaimTypes.Name, "alice")).ToList();
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = permissions.Length == 0
                    ? new ClaimsPrincipal(new ClaimsIdentity())
                    : new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
            },
        };
        var admin = new TenantAdministration(new DefaultPermissionChecker(new InMemoryRolePermissionMap()),
            audit, TimeProvider.System, http, tenant, withDirectory ? directory : null);
        return (admin, directory, audit);
    }

    [Fact]
    public async Task Anonymous_unpermitted_and_tenant_scoped_callers_are_denied_and_audited()
    {
        var (anonymous, _, audit) = Build(null);
        Assert.Equal(TenancyErrors.Denied, (await anonymous.ListAsync(null, Ct)).Error.Code);
        Assert.Equal("unauthenticated", Assert.Single(await audit.ReadAllAsync(Ct)).Event.Details["reason"]);

        var (unpermitted, _, audit2) = Build(new Scope(null, TenantScope.Host), true, TenancyPermissions.Read);
        Assert.Equal(TenancyErrors.Denied, (await unpermitted.CreateAsync(new TenantEdit("t1", "One"), Ct)).Error.Code);
        Assert.Equal($"permission:{TenancyPermissions.Manage}", Assert.Single(await audit2.ReadAllAsync(Ct)).Event.Details["reason"]);

        var (tenantScoped, _, audit3) = Build(new Scope("t1", TenantScope.Tenant), true, TenancyPermissions.Read, TenancyPermissions.Manage);
        Assert.Equal(TenancyErrors.Denied, (await tenantScoped.ListAsync(null, Ct)).Error.Code);
        Assert.Equal($"scope:{TenantScope.Tenant}", Assert.Single(await audit3.ReadAllAsync(Ct)).Event.Details["reason"]);
    }

    [Fact]
    public async Task Missing_directory_fails_typed()
    {
        var (admin, _, _) = Build(new Scope(null, TenantScope.Host), withDirectory: false, TenancyPermissions.Read);
        Assert.Equal(TenancyErrors.NoDirectory, (await admin.ListAsync(null, Ct)).Error.Code);
    }

    [Fact]
    public async Task Host_crud_round_trips_and_audits_every_mutation()
    {
        var (admin, directory, audit) = Build(new Scope(null, TenantScope.Host), true,
            TenancyPermissions.Read, TenancyPermissions.Manage);

        var created = (await admin.CreateAsync(new TenantEdit("t1", "One"), Ct)).Value;
        Assert.True(created.Enabled);
        Assert.Equal(TenancyErrors.Duplicate, (await admin.CreateAsync(new TenantEdit("t1", "Again"), Ct)).Error.Code);

        var renamed = (await admin.RenameAsync(new TenantEdit("t1", "One Renamed"), Ct)).Value;
        Assert.Equal("One Renamed", renamed.DisplayName);

        var disabled = (await admin.SetEnabledAsync("t1", false, Ct)).Value;
        Assert.False(disabled.Enabled);
        Assert.Equal(TenancyErrors.NotFound, (await admin.SetEnabledAsync("missing", true, Ct)).Error.Code);

        var listed = (await admin.ListAsync(null, Ct)).Value;
        Assert.False(Assert.Single(listed).Enabled);
        Assert.Equal("t1", Assert.Single((await admin.ListAsync("renamed", Ct)).Value).Id);
        Assert.Empty((await admin.ListAsync("nope", Ct)).Value);
        Assert.False((await directory.GetAsync("t1", Ct))!.Enabled);

        Assert.Equal(
            [TenancyEvents.Created, TenancyEvents.Renamed, TenancyEvents.Disabled],
            (await audit.ReadAllAsync(Ct)).Select(r => r.Event.Action));
    }
}
