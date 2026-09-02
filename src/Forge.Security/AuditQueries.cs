using System.Diagnostics;
using System.Security.Claims;
using Forge.Auditing;
using Forge.Core.Primitives;
using Forge.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Forge.Security;

/// <summary>Permissions the audit application contract enforces (ADR 06/08); lives beside the decision point, like the contract's implementation.</summary>
public static class AuditPermissions
{
    public const string Read = "Audit.Read";
    public const string Verify = "Audit.Verify";
    public const string Export = "Audit.Export";

    public static readonly IReadOnlyList<Permission> All =
    [
        new(Read, "Read the audit trail"),
        new(Verify, "Verify the audit hash chain"),
        new(Export, "Export the audit trail to immutable evidence"),
    ];
}

/// <summary>
/// Reference implementation of <see cref="IAuditQueries"/>. Lives here rather
/// than in Forge.Auditing because the permission decision point does — the
/// contract itself stays in the capability package. Registered by
/// <see cref="SecurityExtensions.AddForgePermissions"/>.
/// </summary>
internal sealed class AuditQueries(
    IAuditStore store,
    IPermissionChecker permissions,
    TimeProvider clock,
    IHttpContextAccessor httpContext,
    ICurrentTenant? tenant = null, // null = tenancy not composed, no scope to enforce
    IImmutableEvidenceStore? evidence = null) : IAuditQueries
{
    // ponytail: filters run in memory over the newest window; push them into JSON_VALUE indexes when trails outgrow this
    private const int ScanWindow = 1000;

    private ClaimsPrincipal Caller => httpContext.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
    private string Actor => Caller.Identity?.Name ?? "anonymous";

    public async Task<Result<IReadOnlyList<AuditRecord>>> ListAsync(AuditQuery query, CancellationToken ct)
    {
        if (await DeniedAsync(AuditPermissions.Read, "trail", hostOnly: false, ct) is { } denied)
        {
            return Result.Failure<IReadOnlyList<AuditRecord>>(denied);
        }

        // a tenant-scoped caller only ever sees its own tenant's evidence
        var tenantId = tenant is { Scope: TenantScope.Tenant } ? tenant.Id : null;
        var window = await store.ReadLatestAsync(query.BeforeSequence, ScanWindow, ct);
        var matches = window.Where(r =>
        {
            var e = r.Event;
            return (tenantId is null || e.TenantId == tenantId)
                && Matches(query.Actor, e.Actor)
                && Matches(query.Action, e.Action)
                && Matches(query.Subject, e.Subject)
                && Matches(query.CorrelationId, e.CorrelationId);
        }).Take(query.Take).ToList();
        return Result.Success<IReadOnlyList<AuditRecord>>(matches);
    }

    public async Task<Result<AuditChainStatus>> VerifyAsync(CancellationToken ct)
    {
        if (await DeniedAsync(AuditPermissions.Verify, "chain", hostOnly: true, ct) is { } denied)
        {
            return Result.Failure<AuditChainStatus>(denied);
        }

        var records = await store.ReadAllAsync(ct);
        var errors = AuditChainVerifier.Verify(records);
        await store.AppendAsync(Event(AuditActions.Verified, "chain", errors.Count == 0 ? "success" : "failure",
            new() { ["recordCount"] = records.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) }), ct);
        return Result.Success(new AuditChainStatus(records.Count, errors, evidence?.GetType().Name));
    }

    public async Task<Result<string>> ExportAsync(CancellationToken ct)
    {
        if (await DeniedAsync(AuditPermissions.Export, "export", hostOnly: true, ct) is { } denied)
        {
            return Result.Failure<string>(denied);
        }

        if (evidence is null)
        {
            return Result.Failure<string>(new Error(AuditErrors.NoEvidenceStore, "No immutable evidence store is configured."));
        }

        var id = $"audit-{clock.GetUtcNow():yyyyMMddTHHmmssfff}Z.jsonl";
        await new AuditExporter(store, evidence, clock).ExportAsync(id, Actor, Correlation(), ct);
        return Result.Success(id);
    }

    private static bool Matches(string? filter, string value) =>
        string.IsNullOrEmpty(filter) || string.Equals(filter, value, StringComparison.Ordinal);

    private async Task<Error?> DeniedAsync(string permission, string subject, bool hostOnly, CancellationToken ct)
    {
        var reason = Caller.Identity?.IsAuthenticated != true ? "unauthenticated"
            : hostOnly && tenant is { Scope: not TenantScope.Host } ? $"scope:{tenant.Scope}"
            : !await permissions.HasAsync(Caller, permission, ct) ? $"permission:{permission}"
            : null;
        if (reason is null)
        {
            return null;
        }

        await store.AppendAsync(Event(SecurityEvents.AuthorizationDenied, subject, "denied", new() { ["reason"] = reason }), ct);
        return new Error(AuditErrors.Denied, "Not permitted.");
    }

    private static string Correlation() =>
        Activity.Current?.TraceId.ToString() ?? CorrelationId.New().ToString();

    private AuditEvent Event(string action, string subject, string outcome, Dictionary<string, string> details) => new()
    {
        Action = action,
        TenantId = tenant?.Id,
        Actor = Actor,
        CorrelationId = Correlation(),
        Subject = subject,
        Outcome = outcome,
        OccurredAt = clock.GetUtcNow(),
        Details = details,
    };
}
