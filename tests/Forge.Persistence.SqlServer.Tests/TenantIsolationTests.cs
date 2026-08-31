using Forge.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forge.Persistence.SqlServer.Tests;

/// <summary>
/// Negative tenant isolation tests against real SQL Server (ADR 05): central
/// query filters, deny-by-default unresolved scope, write guards, explicit
/// host scope. Release blockers.
/// </summary>
public class TenantIsolationTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private KernelTestDbContext CreateContext(CurrentTenant tenant)
    {
        Assert.SkipWhen(fixture.UnavailableReason is not null, $"SQL Server container unavailable: {fixture.UnavailableReason}");

        return new KernelTestDbContext(new DbContextOptionsBuilder<KernelTestDbContext>()
            .UseSqlServer(fixture.ConnectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "kerneltest"))
            .Options, tenant);
    }

    private async Task<CurrentTenant> SeedAsync(CancellationToken ct)
    {
        var tenant = new CurrentTenant();
        await using (var db = CreateContext(tenant))
        {
            await db.Database.MigrateAsync(ct);
        }

        tenant.SetTenant("alpha");
        await using (var db = CreateContext(tenant))
        {
            db.Notes.Add(new TenantNote { Id = Guid.NewGuid(), Text = "alpha-note" });
            await db.SaveChangesAsync(ct);
        }

        tenant.SetTenant("beta");
        await using (var db = CreateContext(tenant))
        {
            db.Notes.Add(new TenantNote { Id = Guid.NewGuid(), Text = "beta-note" });
            await db.SaveChangesAsync(ct);
        }

        return tenant;
    }

    [Fact]
    public async Task Reads_are_filtered_to_the_current_tenant_and_inserts_are_stamped()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = await SeedAsync(ct);

        tenant.SetTenant("alpha");
        await using var db = CreateContext(tenant);
        var notes = await db.Notes.AsNoTracking().ToListAsync(ct);

        Assert.NotEmpty(notes);
        Assert.All(notes, n => Assert.Equal("alpha", n.TenantId)); // filtered to current tenant
        Assert.Contains(notes, n => n.Text == "alpha-note"); // stamped centrally, never set by the caller
        Assert.DoesNotContain(notes, n => n.Text == "beta-note");
    }

    [Fact]
    public async Task Unresolved_scope_reads_nothing_and_cannot_write()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedAsync(ct);

        var unresolved = new CurrentTenant();
        await using var db = CreateContext(unresolved);

        Assert.Empty(await db.Notes.AsNoTracking().ToListAsync(ct));

        db.Notes.Add(new TenantNote { Id = Guid.NewGuid(), Text = "orphan" });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));
        Assert.Contains("unresolved tenant scope", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Writing_another_tenants_row_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = await SeedAsync(ct);

        tenant.SetTenant("alpha");
        await using var db = CreateContext(tenant);
        db.Notes.Add(new TenantNote { Id = Guid.NewGuid(), TenantId = "beta", Text = "smuggled" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));
        Assert.Contains("tenant isolation violation", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Host_scope_is_explicit_and_sees_all_tenants()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = await SeedAsync(ct);

        // AsyncLocal changes inside SeedAsync don't flow back here; enter
        // tenant scope in this flow so the post-dispose restore is observable.
        tenant.SetTenant("alpha");

        using (tenant.BeginHostScope())
        {
            await using var db = CreateContext(tenant);
            var all = await db.Notes.AsNoTracking().ToListAsync(ct);
            Assert.Contains(all, n => n.TenantId == "alpha");
            Assert.Contains(all, n => n.TenantId == "beta");
        }

        // scope restored after dispose: back to the last tenant, not host
        Assert.Equal(TenantScope.Tenant, ((ICurrentTenant)tenant).Scope);
    }
}
