using Forge.Auditing;
using Xunit;

namespace Forge.AuditingTests;

public class AuditChainTests
{
    private static AuditEvent Event(string action, Dictionary<string, string>? details = null) => new()
    {
        Action = action,
        TenantId = "tenant-a",
        Actor = "system",
        CorrelationId = "c0ffee",
        Subject = "subject-1",
        Outcome = "success",
        OccurredAt = DateTimeOffset.UnixEpoch,
        Details = details ?? [],
    };

    private static InMemoryAuditStore NewStore() => new(new DefaultAuditRedactionPolicy());

    [Fact]
    public async Task Appended_records_form_a_verifiable_chain()
    {
        var store = NewStore();
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < 5; i++)
        {
            await store.AppendAsync(Event($"a.{i}"), ct);
        }

        var records = await store.ReadAllAsync(ct);
        Assert.Equal(5, records.Count);
        Assert.Equal(AuditChain.GenesisHash, records[0].PreviousHash);
        Assert.Empty(AuditChainVerifier.Verify(records));
    }

    [Fact]
    public async Task Tampered_content_is_detected()
    {
        var store = NewStore();
        var ct = TestContext.Current.CancellationToken;
        await store.AppendAsync(Event("a.1"), ct);
        await store.AppendAsync(Event("a.2"), ct);

        var records = (await store.ReadAllAsync(ct)).ToList();
        records[0] = records[0] with { EventJson = records[0].EventJson.Replace("success", "denied", StringComparison.Ordinal) };

        Assert.Contains(AuditChainVerifier.Verify(records), e => e.Contains("content tampered", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Removed_record_breaks_the_chain()
    {
        var store = NewStore();
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < 3; i++)
        {
            await store.AppendAsync(Event($"a.{i}"), ct);
        }

        var records = (await store.ReadAllAsync(ct)).Where(r => r.Sequence != 2).ToList();

        Assert.Contains(AuditChainVerifier.Verify(records), e => e.Contains("chain break", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sensitive_details_are_redacted_before_storage()
    {
        var store = NewStore();
        var ct = TestContext.Current.CancellationToken;
        await store.AppendAsync(Event("a.1", new Dictionary<string, string>
        {
            ["userPassword"] = "hunter2",
            ["displayName"] = "Anvil",
        }), ct);

        var record = Assert.Single(await store.ReadAllAsync(ct));
        Assert.DoesNotContain("hunter2", record.EventJson, StringComparison.Ordinal);
        Assert.Equal("[redacted]", record.Event.Details["userPassword"]);
        Assert.Equal("Anvil", record.Event.Details["displayName"]);
        Assert.Empty(AuditChainVerifier.Verify(await store.ReadAllAsync(ct)));
    }

    [Fact]
    public void Store_contract_has_no_update_or_delete_members()
    {
        var members = typeof(IAuditStore).GetMethods().Select(m => m.Name).ToList();
        Assert.Equal(["AppendAsync", "ReadAllAsync"], members.Order().ToList());
    }
}
