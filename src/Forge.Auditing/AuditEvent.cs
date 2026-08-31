namespace Forge.Auditing;

/// <summary>
/// Structured compliance evidence (ADR 08): versioned, carrying tenant, actor,
/// impersonation, correlation, outcome and policy context. Distinct from
/// ILogger diagnostics and from entity history — neither substitutes for it.
/// </summary>
public sealed record AuditEvent
{
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Stable dotted action name, e.g. "catalog.item.created".</summary>
    public required string Action { get; init; }

    public required string? TenantId { get; init; }

    /// <summary>The acting identity ("system" until Phase 2.2 wires real identity).</summary>
    public required string Actor { get; init; }

    /// <summary>The real identity when the actor is being impersonated (ADR 06); null otherwise.</summary>
    public string? ImpersonatorActor { get; init; }

    public required string CorrelationId { get; init; }

    /// <summary>What the action affected, e.g. an entity id.</summary>
    public required string Subject { get; init; }

    /// <summary>"success", "denied", "failure" — the policy-relevant result.</summary>
    public required string Outcome { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Extra context; sensitive values are redacted at append time, never stored.</summary>
    public IReadOnlyDictionary<string, string> Details { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>Actions the auditing infrastructure itself emits (retention and export are audited, ADR 08).</summary>
public static class AuditActions
{
    public const string Exported = "audit.exported";
    public const string RetentionApplied = "audit.retention-applied";
}

/// <summary>
/// Decides which detail keys are sensitive. Sensitive values are excluded by
/// default and replaced with <see cref="Redacted"/> before a record is written.
/// </summary>
public interface IAuditRedactionPolicy
{
    bool IsSensitive(string detailKey);
}

/// <summary>Deny-list reference policy; substring match, case-insensitive.</summary>
public sealed class DefaultAuditRedactionPolicy : IAuditRedactionPolicy
{
    public const string Redacted = "[redacted]";

    private static readonly string[] SensitiveFragments =
        ["password", "secret", "token", "credential", "apikey", "api-key", "connectionstring"];

    public bool IsSensitive(string detailKey) =>
        SensitiveFragments.Any(f => detailKey.Contains(f, StringComparison.OrdinalIgnoreCase));
}
