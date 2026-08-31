using Forge.Auditing;
using Forge.Core.Privacy;
using Xunit;

namespace Forge.AuditingTests;

public class ClassificationRedactionTests
{
    [Fact]
    public async Task Personal_and_sensitive_classified_keys_are_redacted_public_kept()
    {
        var policy = new ClassificationAwareRedactionPolicy(new Dictionary<string, DataClassification>
        {
            ["customerEmail"] = DataClassification.Personal,
            ["medicalNote"] = DataClassification.Sensitive,
            ["itemCount"] = DataClassification.Public,
        });
        var store = new InMemoryAuditStore(policy);
        var ct = TestContext.Current.CancellationToken;

        await store.AppendAsync(new AuditEvent
        {
            Action = "a.1",
            TenantId = "t1",
            Actor = "system",
            CorrelationId = "c1",
            Subject = "s1",
            Outcome = "success",
            OccurredAt = DateTimeOffset.UnixEpoch,
            Details = new Dictionary<string, string>
            {
                ["customerEmail"] = "who@example.com",
                ["medicalNote"] = "confidential",
                ["itemCount"] = "3",
                ["apiKey"] = "still-caught-by-defaults",
            },
        }, ct);

        var record = Assert.Single(await store.ReadAllAsync(ct));
        Assert.Equal("[redacted]", record.Event.Details["customerEmail"]);
        Assert.Equal("[redacted]", record.Event.Details["medicalNote"]);
        Assert.Equal("[redacted]", record.Event.Details["apiKey"]); // default deny list still applies
        Assert.Equal("3", record.Event.Details["itemCount"]);
        Assert.DoesNotContain("who@example.com", record.EventJson, StringComparison.Ordinal);
    }
}
