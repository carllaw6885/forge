using System.Security.Claims;
using Forge.Auditing;
using Forge.Security;
using Forge.Tenancy;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Forge.SecurityTests;

public class AuditQueriesTests
{
    private sealed record Tenant(string? Id, TenantScope Scope) : ICurrentTenant;

    private sealed class MemoryEvidence : IImmutableEvidenceStore
    {
        public List<string> Ids { get; } = [];

        public Task WriteAsync(string id, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
        {
            Ids.Add(id);
            return Task.CompletedTask;
        }

        public Task<byte[]> ReadAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Array.Empty<byte>());
    }

    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static (AuditQueries Queries, InMemoryAuditStore Store) Build(
        ICurrentTenant? tenant, IImmutableEvidenceStore? evidence, params string[] permissions)
    {
        var store = new InMemoryAuditStore(new DefaultAuditRedactionPolicy());
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
        var queries = new AuditQueries(store, new DefaultPermissionChecker(new InMemoryRolePermissionMap()),
            TimeProvider.System, http, tenant, evidence);
        return (queries, store);
    }

    private static Task<AuditRecord> Seed(InMemoryAuditStore store, string? tenantId, string action) =>
        store.AppendAsync(new AuditEvent
        {
            Action = action,
            TenantId = tenantId,
            Actor = "seed",
            CorrelationId = "c",
            Subject = "s",
            Outcome = "success",
            OccurredAt = DateTimeOffset.UnixEpoch,
        }, Ct);

    [Fact]
    public async Task Anonymous_and_unpermitted_callers_are_denied_and_audited()
    {
        var (anonymous, store) = Build(null, null);
        var denied = await anonymous.ListAsync(new AuditQuery(), Ct);
        Assert.Equal(AuditErrors.Denied, denied.Error.Code);

        var (reader, _) = Build(null, null, AuditPermissions.Read);
        Assert.Equal(AuditErrors.Denied, (await reader.VerifyAsync(Ct)).Error.Code);

        var records = await store.ReadAllAsync(Ct);
        var evt = Assert.Single(records).Event;
        Assert.Equal(SecurityEvents.AuthorizationDenied, evt.Action);
        Assert.Equal("unauthenticated", evt.Details["reason"]);
    }

    [Fact]
    public async Task Tenant_scoped_callers_see_only_their_own_tenant()
    {
        var (queries, store) = Build(new Tenant("t1", TenantScope.Tenant), null, AuditPermissions.Read);
        await Seed(store, "t1", "one");
        await Seed(store, "t2", "two");
        await Seed(store, null, "host");

        var result = await queries.ListAsync(new AuditQuery(), Ct);

        Assert.Equal(["one"], result.Value.Select(r => r.Event.Action));
    }

    [Fact]
    public async Task Host_scope_sees_everything_newest_first_and_filters_exactly()
    {
        var (queries, store) = Build(new Tenant(null, TenantScope.Host), null, AuditPermissions.Read);
        await Seed(store, "t1", "one");
        await Seed(store, "t2", "two");
        await Seed(store, null, "two");

        var all = await queries.ListAsync(new AuditQuery(), Ct);
        Assert.Equal(["two", "two", "one"], all.Value.Select(r => r.Event.Action));

        var filtered = await queries.ListAsync(new AuditQuery(Action: "two", Take: 1), Ct);
        Assert.Equal(3, Assert.Single(filtered.Value).Sequence);
    }

    [Fact]
    public async Task Verify_is_host_only_and_leaves_its_own_evidence()
    {
        var (tenantScoped, _) = Build(new Tenant("t1", TenantScope.Tenant), null, AuditPermissions.Verify);
        Assert.Equal(AuditErrors.Denied, (await tenantScoped.VerifyAsync(Ct)).Error.Code);

        var (host, store) = Build(new Tenant(null, TenantScope.Host), null, AuditPermissions.Verify);
        await Seed(store, null, "one");

        var status = (await host.VerifyAsync(Ct)).Value;

        Assert.True(status.IsIntact);
        Assert.Equal(1, status.RecordCount);
        Assert.Null(status.EvidenceStore);
        Assert.Equal(AuditActions.Verified, (await store.ReadAllAsync(Ct))[^1].Event.Action);
    }

    [Fact]
    public async Task Export_needs_an_evidence_store()
    {
        var (bare, _) = Build(new Tenant(null, TenantScope.Host), null, AuditPermissions.Export);
        Assert.Equal(AuditErrors.NoEvidenceStore, (await bare.ExportAsync(Ct)).Error.Code);

        var evidence = new MemoryEvidence();
        var (host, store) = Build(new Tenant(null, TenantScope.Host), evidence, AuditPermissions.Export);
        await Seed(store, null, "one");

        var id = (await host.ExportAsync(Ct)).Value;

        Assert.Equal([id], evidence.Ids);
        Assert.Equal(AuditActions.Exported, (await store.ReadAllAsync(Ct))[^1].Event.Action);
    }
}
