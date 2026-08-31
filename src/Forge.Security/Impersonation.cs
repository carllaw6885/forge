using Forge.Auditing;

namespace Forge.Security;

/// <summary>Ambient impersonation context (ADR 06): visible to audit contributions and UI banners.</summary>
public interface IImpersonationContext
{
    bool IsImpersonating { get; }

    /// <summary>The real, privileged identity doing the impersonating.</summary>
    string? Impersonator { get; }

    /// <summary>The identity being impersonated.</summary>
    string? Target { get; }

    string? Reason { get; }
}

/// <summary>
/// Begins and ends impersonation. Every impersonation is reasoned (a
/// justification is mandatory) and audited (started/ended evidence with the
/// real actor). Begin mutates AsyncLocal state synchronously so the caller's
/// own flow sees it; the started audit append runs as a task the session
/// awaits before it completes. Register as a singleton.
/// </summary>
public sealed class ImpersonationService(IAuditStore audit, TimeProvider clock) : IImpersonationContext
{
    private sealed record State(string Impersonator, string Target, string Reason, string? TenantId, string CorrelationId);

    // Mutable holder: the holder is installed synchronously (so it flows to the
    // caller's context); its contents can then be mutated from any async flow
    // sharing it — which is what lets an async DisposeAsync end the session.
    private sealed class Holder
    {
        public State? Value;
    }

    private readonly AsyncLocal<Holder?> _holder = new();

    private State? Current => _holder.Value?.Value;

    public bool IsImpersonating => Current is not null;
    public string? Impersonator => Current?.Impersonator;
    public string? Target => Current?.Target;
    public string? Reason => Current?.Reason;

    /// <summary>Starts audited impersonation; dispose to end it (also audited).</summary>
    public IAsyncDisposable Begin(
        string impersonator, string target, string reason, string? tenantId, string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(impersonator);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason); // reasoned, always

        var state = new State(impersonator, target, reason, tenantId, correlationId);
        var holder = _holder.Value;
        if (holder is null)
        {
            holder = new Holder();
            _holder.Value = holder; // synchronous: flows to the caller's async context
        }

        var previous = holder.Value;
        holder.Value = state;

        var startedAudit = AppendAsync(SecurityEvents.ImpersonationStarted, state, cancellationToken);
        return new Session(this, holder, state, previous, startedAudit, cancellationToken);
    }

    private sealed class Session(
        ImpersonationService owner, Holder holder, State state, State? previous,
        Task startedAudit, CancellationToken cancellationToken) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            holder.Value = previous; // holder mutation is visible across flows
            await startedAudit;
            await owner.AppendAsync(SecurityEvents.ImpersonationEnded, state, cancellationToken);
        }
    }

    private Task<AuditRecord> AppendAsync(string action, State state, CancellationToken ct) =>
        audit.AppendAsync(new AuditEvent
        {
            Action = action,
            TenantId = state.TenantId,
            Actor = state.Target,
            ImpersonatorActor = state.Impersonator,
            CorrelationId = state.CorrelationId,
            Subject = state.Target,
            Outcome = "success",
            OccurredAt = clock.GetUtcNow(),
            Details = new Dictionary<string, string> { ["reason"] = state.Reason },
        }, ct);
}
