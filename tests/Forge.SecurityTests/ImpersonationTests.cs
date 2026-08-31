using Forge.Auditing;
using Forge.Security;
using Xunit;

namespace Forge.SecurityTests;

public class ImpersonationTests
{
    private static (ImpersonationService Service, InMemoryAuditStore Audit) Build()
    {
        var audit = new InMemoryAuditStore(new DefaultAuditRedactionPolicy());
        return (new ImpersonationService(audit, TimeProvider.System), audit);
    }

    [Fact]
    public void Impersonation_requires_a_reason()
    {
        var (service, _) = Build();

        Assert.Throws<ArgumentException>(() =>
            service.Begin("admin-1", "user-9", reason: " ", tenantId: "t1", correlationId: "c1",
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Impersonation_is_visible_while_active_and_cleared_after()
    {
        var (service, _) = Build();
        var ct = TestContext.Current.CancellationToken;

        Assert.False(service.IsImpersonating);

        await using (service.Begin("admin-1", "user-9", "support ticket #42", "t1", "c1", ct))
        {
            Assert.True(service.IsImpersonating);
            Assert.Equal("admin-1", service.Impersonator);
            Assert.Equal("user-9", service.Target);
            Assert.Equal("support ticket #42", service.Reason);
        }

        Assert.False(service.IsImpersonating);
    }

    [Fact]
    public async Task Start_and_end_are_audited_with_real_actor_reason_and_context()
    {
        var (service, audit) = Build();
        var ct = TestContext.Current.CancellationToken;

        await using (service.Begin("admin-1", "user-9", "support ticket #42", "t1", "c1", ct))
        {
        }

        var events = (await audit.ReadAllAsync(ct)).Select(r => r.Event).ToList();
        var started = Assert.Single(events, e => e.Action == SecurityEvents.ImpersonationStarted);
        var ended = Assert.Single(events, e => e.Action == SecurityEvents.ImpersonationEnded);

        foreach (var e in new[] { started, ended })
        {
            Assert.Equal("admin-1", e.ImpersonatorActor);
            Assert.Equal("user-9", e.Actor);
            Assert.Equal("t1", e.TenantId);
            Assert.Equal("c1", e.CorrelationId);
            Assert.Equal("support ticket #42", e.Details["reason"]);
        }

        Assert.Empty(AuditChainVerifier.Verify(await audit.ReadAllAsync(ct)));
    }
}
