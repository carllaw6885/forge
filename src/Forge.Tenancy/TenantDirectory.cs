using System.Collections.Concurrent;
using Forge.Core.Primitives;

namespace Forge.Tenancy;

/// <summary>A tenant as a first-class record (ADR 05). Disabled tenants fail resolution.</summary>
public sealed record Tenant(string Id, string DisplayName, bool Enabled, DateTimeOffset CreatedAt);

/// <summary>
/// The tenant directory: the authoritative registry of known tenants. Optional —
/// when none is registered, resolved tenant ids are opaque and unchecked (the
/// v0.1 behaviour). When one is registered, resolution rejects unknown and
/// disabled tenants (deny-by-default extends to tenant state).
/// </summary>
public interface ITenantDirectory
{
    Task<Tenant?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>All tenants, ordered by id.</summary>
    Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Upserts by id.</summary>
    Task SaveAsync(Tenant tenant, CancellationToken cancellationToken);
}

/// <summary>In-memory reference directory; real deployments register a persistent store.</summary>
public sealed class InMemoryTenantDirectory : ITenantDirectory
{
    private readonly ConcurrentDictionary<string, Tenant> _tenants = new(StringComparer.Ordinal);

    public Task<Tenant?> GetAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(_tenants.GetValueOrDefault(id));

    public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Tenant>>(_tenants.Values.OrderBy(t => t.Id, StringComparer.Ordinal).ToList());

    public Task SaveAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant.Id);
        _tenants[tenant.Id] = tenant;
        return Task.CompletedTask;
    }
}

/// <summary>Stable failure codes returned by the tenancy application contract.</summary>
public static class TenancyErrors
{
    public const string Denied = "tenancy.denied";
    public const string NotFound = "tenancy.not-found";
    public const string Duplicate = "tenancy.duplicate";
    public const string NoDirectory = "tenancy.no-directory";
}

/// <summary>A create/rename request; the id is the immutable key.</summary>
public sealed record TenantEdit(string Id, string DisplayName);

/// <summary>
/// The tenancy module's application contract (ADR 40): the single front door
/// for first-party UI, HTTP projections and custom applications. Host scope
/// only — tenant administration is cross-tenant by nature; permission and
/// scope are enforced inside, and every mutation is audited.
/// </summary>
public interface ITenantAdministration
{
    /// <summary>All tenants, optionally filtered by an exact id or a case-insensitive name substring.</summary>
    Task<Result<IReadOnlyList<Tenant>>> ListAsync(string? search, CancellationToken cancellationToken);

    Task<Result<Tenant>> CreateAsync(TenantEdit edit, CancellationToken cancellationToken);

    Task<Result<Tenant>> RenameAsync(TenantEdit edit, CancellationToken cancellationToken);

    Task<Result<Tenant>> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken);
}
