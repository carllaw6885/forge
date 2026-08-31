using Forge.Tenancy;
using Xunit;

namespace Forge.TenancyTests;

public class CurrentTenantTests
{
    [Fact]
    public void Starts_unresolved_and_deny_by_default()
    {
        var tenant = new CurrentTenant();
        Assert.Equal(TenantScope.Unresolved, tenant.Scope);
        Assert.Null(tenant.Id);
        Assert.Throws<InvalidOperationException>(() => TenantCacheKey.For(tenant, "k"));
    }

    [Fact]
    public void SetTenant_enters_tenant_scope()
    {
        var tenant = new CurrentTenant();
        tenant.SetTenant("alpha");

        Assert.Equal(TenantScope.Tenant, tenant.Scope);
        Assert.Equal("alpha", tenant.Id);
        Assert.Equal("t:alpha:items", TenantCacheKey.For(tenant, "items"));
    }

    [Fact]
    public void Host_scope_is_explicit_and_restores_previous_scope_on_dispose()
    {
        var tenant = new CurrentTenant();
        tenant.SetTenant("alpha");

        using (tenant.BeginHostScope())
        {
            Assert.Equal(TenantScope.Host, tenant.Scope);
            Assert.Null(tenant.Id);
            Assert.Equal("host:items", TenantCacheKey.For(tenant, "items"));
        }

        Assert.Equal(TenantScope.Tenant, tenant.Scope);
        Assert.Equal("alpha", tenant.Id);
    }

    [Fact]
    public async Task Scope_flows_with_async_context_not_across_it()
    {
        var tenant = new CurrentTenant();
        tenant.SetTenant("alpha");

        var observed = await Task.Run(() =>
        {
            tenant.SetTenant("beta"); // change in a child flow
            return tenant.Id;
        });

        Assert.Equal("beta", observed);
        Assert.Equal("alpha", tenant.Id); // parent flow unaffected
    }

    [Fact]
    public void Cache_keys_for_different_tenants_never_collide()
    {
        var tenant = new CurrentTenant();
        tenant.SetTenant("alpha");
        var alphaKey = TenantCacheKey.For(tenant, "items");
        tenant.SetTenant("beta");

        Assert.NotEqual(alphaKey, TenantCacheKey.For(tenant, "items"));
    }
}
