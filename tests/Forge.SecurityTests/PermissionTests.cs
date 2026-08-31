using System.Security.Claims;
using Forge.Security;
using Xunit;

namespace Forge.SecurityTests;

public class PermissionTests
{
    private static ClaimsPrincipal User(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    [Fact]
    public async Task Direct_permission_claim_passes_without_any_role()
    {
        var checker = new DefaultPermissionChecker(new InMemoryRolePermissionMap());
        var user = User(new Claim(ForgeClaimTypes.Permission, "Catalog.Items.Create"));

        Assert.True(await checker.HasAsync(user, "Catalog.Items.Create", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Role_aggregates_permissions()
    {
        var map = new InMemoryRolePermissionMap().Grant("editor", "Catalog.Items.Create");
        var checker = new DefaultPermissionChecker(map);
        var user = User(new Claim(ClaimTypes.Role, "editor"));

        Assert.True(await checker.HasAsync(user, "Catalog.Items.Create", TestContext.Current.CancellationToken));
        Assert.False(await checker.HasAsync(user, "Catalog.Items.Delete", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Role_name_alone_grants_nothing()
    {
        var checker = new DefaultPermissionChecker(new InMemoryRolePermissionMap());
        var user = User(new Claim(ClaimTypes.Role, "admin"));

        Assert.False(await checker.HasAsync(user, "Catalog.Items.Create", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Catalog_rejects_duplicate_declarations()
    {
        var catalog = new PermissionCatalog().Add(new Permission("P.One", "One"));
        Assert.Throws<InvalidOperationException>(() => catalog.Add(new Permission("P.One", "Again")));
    }
}
