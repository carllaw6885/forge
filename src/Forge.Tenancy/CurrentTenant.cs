namespace Forge.Tenancy;

/// <summary>
/// The three tenancy states (ADR 05). Unresolved is the deny-by-default state:
/// tenant-aware operations must fail until trusted resolution has run. Host
/// scope is explicit and privileged, never a fallback.
/// </summary>
public enum TenantScope
{
    Unresolved,
    Host,
    Tenant,
}

/// <summary>Ambient tenancy context for the current logical operation (ADR 05).</summary>
public interface ICurrentTenant
{
    /// <summary>The tenant id; non-null only in <see cref="TenantScope.Tenant"/> scope.</summary>
    string? Id { get; }

    TenantScope Scope { get; }
}

/// <summary>
/// AsyncLocal-backed <see cref="ICurrentTenant"/>. The resolution pipeline (or a
/// test) sets the tenant; <see cref="BeginHostScope"/> is the explicit, visible
/// way to run privileged host-scope work, restoring the previous scope on dispose.
/// Register as a singleton — state is per async flow, not per instance.
/// </summary>
public sealed class CurrentTenant : ICurrentTenant
{
    private sealed record State(string? Id, TenantScope Scope);

    private readonly AsyncLocal<State?> _state = new();

    public string? Id => _state.Value?.Id;

    public TenantScope Scope => _state.Value?.Scope ?? TenantScope.Unresolved;

    public void SetTenant(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        _state.Value = new State(tenantId, TenantScope.Tenant);
    }

    /// <summary>Explicit privileged scope change; dispose to restore the previous scope.</summary>
    public IDisposable BeginHostScope()
    {
        var previous = _state.Value;
        _state.Value = new State(null, TenantScope.Host);
        return new Restore(this, previous);
    }

    private sealed class Restore(CurrentTenant owner, State? previous) : IDisposable
    {
        public void Dispose() => owner._state.Value = previous;
    }
}

/// <summary>An entity owned by exactly one tenant; opted into central EF filtering (ADR 05).</summary>
public interface ITenantOwned
{
    string TenantId { get; set; }
}
