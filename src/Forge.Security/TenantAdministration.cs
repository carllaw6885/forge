using System.Diagnostics;
using System.Security.Claims;
using Forge.Auditing;
using Forge.Core.Primitives;
using Forge.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Forge.Security;

/// <summary>Permissions the tenancy application contract enforces (ADR 05/06); lives beside the decision point.</summary>
public static class TenancyPermissions
{
    public const string Read = "Tenancy.Read";
    public const string Manage = "Tenancy.Manage";

    public static readonly IReadOnlyList<Permission> All =
    [
        new(Read, "List and inspect tenants"),
        new(Manage, "Create, rename, enable and disable tenants"),
    ];
}

/// <summary>Tenant lifecycle action names for audit evidence.</summary>
public static class TenancyEvents
{
    public const string Created = "tenant.created";
    public const string Renamed = "tenant.renamed";
    public const string Enabled = "tenant.enabled";
    public const string Disabled = "tenant.disabled";
}

/// <summary>
/// Reference implementation of <see cref="ITenantAdministration"/>. Lives here
/// rather than in Forge.Tenancy because the permission decision point does.
/// Host scope only — tenant administration is cross-tenant by nature.
/// Registered by <see cref="SecurityExtensions.AddForgePermissions"/>.
/// </summary>
internal sealed class TenantAdministration(
    IPermissionChecker permissions,
    IAuditStore audit,
    TimeProvider clock,
    IHttpContextAccessor httpContext,
    ICurrentTenant? tenant = null, // null = tenancy not composed, no scope to enforce
    ITenantDirectory? directory = null) : ITenantAdministration
{
    private ClaimsPrincipal Caller => httpContext.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
    private string Actor => Caller.Identity?.Name ?? "anonymous";

    public async Task<Result<IReadOnlyList<Tenant>>> ListAsync(string? search, CancellationToken ct)
    {
        if (await GateAsync(TenancyPermissions.Read, "directory", ct) is { } failed)
        {
            return Result.Failure<IReadOnlyList<Tenant>>(failed);
        }

        var tenants = await directory!.ListAsync(ct);
        if (!string.IsNullOrWhiteSpace(search))
        {
            tenants = tenants.Where(t =>
                    string.Equals(t.Id, search, StringComparison.Ordinal)
                    || t.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return Result.Success(tenants);
    }

    public async Task<Result<Tenant>> CreateAsync(TenantEdit edit, CancellationToken ct)
    {
        if (await GateAsync(TenancyPermissions.Manage, edit.Id, ct) is { } failed)
        {
            return Result.Failure<Tenant>(failed);
        }

        if (string.IsNullOrWhiteSpace(edit.Id) || string.IsNullOrWhiteSpace(edit.DisplayName))
        {
            return Result.Failure<Tenant>(new Error(TenancyErrors.NotFound, "Tenant id and display name are required."));
        }

        if (await directory!.GetAsync(edit.Id, ct) is not null)
        {
            return Result.Failure<Tenant>(new Error(TenancyErrors.Duplicate, "A tenant with this id already exists."));
        }

        var created = new Tenant(edit.Id, edit.DisplayName, Enabled: true, clock.GetUtcNow());
        await directory.SaveAsync(created, ct);
        await AuditAsync(TenancyEvents.Created, created.Id, ct);
        return Result.Success(created);
    }

    public async Task<Result<Tenant>> RenameAsync(TenantEdit edit, CancellationToken ct)
    {
        if (await GateAsync(TenancyPermissions.Manage, edit.Id, ct) is { } failed)
        {
            return Result.Failure<Tenant>(failed);
        }

        if (string.IsNullOrWhiteSpace(edit.DisplayName) || await directory!.GetAsync(edit.Id, ct) is not { } existing)
        {
            return Result.Failure<Tenant>(new Error(TenancyErrors.NotFound, "No such tenant."));
        }

        var renamed = existing with { DisplayName = edit.DisplayName };
        await directory.SaveAsync(renamed, ct);
        await AuditAsync(TenancyEvents.Renamed, renamed.Id, ct);
        return Result.Success(renamed);
    }

    public async Task<Result<Tenant>> SetEnabledAsync(string id, bool enabled, CancellationToken ct)
    {
        if (await GateAsync(TenancyPermissions.Manage, id, ct) is { } failed)
        {
            return Result.Failure<Tenant>(failed);
        }

        if (await directory!.GetAsync(id, ct) is not { } existing)
        {
            return Result.Failure<Tenant>(new Error(TenancyErrors.NotFound, "No such tenant."));
        }

        var changed = existing with { Enabled = enabled };
        await directory.SaveAsync(changed, ct);
        await AuditAsync(enabled ? TenancyEvents.Enabled : TenancyEvents.Disabled, changed.Id, ct);
        return Result.Success(changed);
    }

    /// <summary>Denies (audited) or reports a missing directory; null = allowed.</summary>
    private async Task<Error?> GateAsync(string permission, string subject, CancellationToken ct)
    {
        var reason = Caller.Identity?.IsAuthenticated != true ? "unauthenticated"
            : tenant is { Scope: not TenantScope.Host } ? $"scope:{tenant.Scope}"
            : !await permissions.HasAsync(Caller, permission, ct) ? $"permission:{permission}"
            : null;
        if (reason is not null)
        {
            await audit.AppendAsync(Event(SecurityEvents.AuthorizationDenied, subject, "denied",
                new() { ["reason"] = reason }), ct);
            return new Error(TenancyErrors.Denied, "Not permitted.");
        }

        return directory is null
            ? new Error(TenancyErrors.NoDirectory, "No tenant directory is configured.")
            : null;
    }

    private Task<AuditRecord> AuditAsync(string action, string subject, CancellationToken ct) =>
        audit.AppendAsync(Event(action, subject, "success", []), ct);

    private AuditEvent Event(string action, string subject, string outcome, Dictionary<string, string> details) => new()
    {
        Action = action,
        TenantId = tenant?.Id,
        Actor = Actor,
        CorrelationId = Activity.Current?.TraceId.ToString() ?? CorrelationId.New().ToString(),
        Subject = subject,
        Outcome = outcome,
        OccurredAt = clock.GetUtcNow(),
        Details = details,
    };
}
