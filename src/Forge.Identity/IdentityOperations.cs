using System.Diagnostics;
using System.Security.Claims;
using Forge.Auditing;
using Forge.Core.Primitives;
using Forge.Security;
using Forge.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Forge.Identity;

/// <summary>Permissions the identity module declares (ADR 06).</summary>
public static class IdentityPermissions
{
    public const string UsersRead = "Identity.Users.Read";
    public const string UsersManage = "Identity.Users.Manage";
    public const string RolesManage = "Identity.Roles.Manage";

    public static readonly IReadOnlyList<Permission> All =
    [
        new(UsersRead, "Read users"),
        new(UsersManage, "Create users and assign roles"),
        new(RolesManage, "Create roles and grant permissions"),
    ];
}

/// <summary>Stable failure codes returned by the identity application contract.</summary>
public static class IdentityErrors
{
    public const string Denied = "identity.denied";
    public const string NotFound = "identity.not-found";
    public const string Invalid = "identity.invalid";
}

/// <summary>A user as the contract exposes it: name and role names, no store types.</summary>
public sealed record UserSummary(string UserName, IReadOnlyList<string> Roles);
/// <summary>A role and the permission names granted to it.</summary>
public sealed record RoleSummary(string Name, IReadOnlyList<string> Permissions);

/// <summary>
/// The identity module's application contract (ADR 40): the single front door
/// for in-process UI, HTTP projections and custom applications. Authorisation,
/// tenant scope and auditing are enforced inside — callers pass no
/// authorisation state. Plain interfaces, no mediator.
/// </summary>
public interface IUserAdministration
{
    Task<Result<IReadOnlyList<UserSummary>>> ListAsync(int take, CancellationToken cancellationToken);
    Task<Result> CreateAsync(string userName, string password, CancellationToken cancellationToken);
    Task<Result> AssignRoleAsync(string userName, string role, CancellationToken cancellationToken);
}

/// <summary>Role and permission administration; requires <see cref="IdentityPermissions.RolesManage"/> in host scope.</summary>
public interface IRoleAdministration
{
    Task<Result<IReadOnlyList<RoleSummary>>> ListAsync(CancellationToken cancellationToken);
    Task<Result> CreateAsync(string role, CancellationToken cancellationToken);
    Task<Result> GrantPermissionAsync(string role, string permission, CancellationToken cancellationToken);
}

/// <summary>Operations a signed-in user performs on their own account; authenticated only, no permission.</summary>
public interface IAccountOperations
{
    Task<Result<UserSummary>> MeAsync(CancellationToken cancellationToken);
    Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken);
}

/// <summary>Actions the identity contract audits.</summary>
public static class IdentityAuditActions
{
    public const string UserCreated = "identity.user.created";
    public const string RoleAssigned = "identity.user.role-assigned";
    public const string RoleCreated = "identity.role.created";
    public const string PermissionGranted = "identity.role.permission-granted";
    public const string PasswordChanged = "identity.account.password-changed";
}

/// <summary>
/// Reference implementation over ASP.NET Core Identity. Identity data is host
/// owned in v0.1 (users carry no tenant), so administration is a host-scope
/// operation: a resolved tenant scope is denied, not silently widened.
/// </summary>
internal sealed class IdentityOperations(
    UserManager<ForgeUser> users,
    RoleManager<IdentityRole> roles,
    ForgeIdentityDbContext db,
    IPermissionChecker permissions,
    IAuditStore audit,
    TimeProvider clock,
    IHttpContextAccessor httpContext,
    ICurrentTenant? tenant = null) // null = tenancy not composed in this host, no scope to enforce
    : IUserAdministration, IRoleAdministration, IAccountOperations
{
    private ClaimsPrincipal Caller => httpContext.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
    private string Actor => Caller.Identity?.Name ?? "anonymous";

    public async Task<Result<IReadOnlyList<UserSummary>>> ListAsync(int take, CancellationToken ct)
    {
        if (await DeniedAsync(IdentityPermissions.UsersRead, "users", ct) is { } denied)
        {
            return Result.Failure<IReadOnlyList<UserSummary>>(denied);
        }

        var list = await db.Users.AsNoTracking()
            .OrderBy(u => u.UserName)
            .Take(take)
            .Select(u => new UserSummary(u.UserName!,
                db.UserRoles.Where(ur => ur.UserId == u.Id)
                    .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
                    .OrderBy(n => n)
                    .ToList()))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<UserSummary>>(list);
    }

    public async Task<Result> CreateAsync(string userName, string password, CancellationToken ct)
    {
        if (await DeniedAsync(IdentityPermissions.UsersManage, userName, ct) is { } denied)
        {
            return Result.Failure(denied);
        }

        var result = await users.CreateAsync(new ForgeUser { UserName = userName }, password);
        return await AuditedAsync(IdentityAuditActions.UserCreated, userName, result, ct);
    }

    public async Task<Result> AssignRoleAsync(string userName, string role, CancellationToken ct)
    {
        if (await DeniedAsync(IdentityPermissions.UsersManage, userName, ct) is { } denied)
        {
            return Result.Failure(denied);
        }

        var user = await users.FindByNameAsync(userName);
        if (user is null)
        {
            return Result.Failure(new Error(IdentityErrors.NotFound, $"No user '{userName}'."));
        }

        var result = await users.AddToRoleAsync(user, role);
        return await AuditedAsync(IdentityAuditActions.RoleAssigned, userName, result, ct, new() { ["role"] = role });
    }

    async Task<Result<IReadOnlyList<RoleSummary>>> IRoleAdministration.ListAsync(CancellationToken ct)
    {
        if (await DeniedAsync(IdentityPermissions.RolesManage, "roles", ct) is { } denied)
        {
            return Result.Failure<IReadOnlyList<RoleSummary>>(denied);
        }

        var grants = await db.RolePermissions.AsNoTracking().ToListAsync(ct);
        var list = (await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct))
            .Select(r => new RoleSummary(r.Name!,
                [.. grants.Where(g => g.RoleName == r.Name).Select(g => g.PermissionName).Order(StringComparer.Ordinal)]))
            .ToList();
        return Result.Success<IReadOnlyList<RoleSummary>>(list);
    }

    async Task<Result> IRoleAdministration.CreateAsync(string role, CancellationToken ct)
    {
        if (await DeniedAsync(IdentityPermissions.RolesManage, role, ct) is { } denied)
        {
            return Result.Failure(denied);
        }

        var result = await roles.CreateAsync(new IdentityRole(role));
        return await AuditedAsync(IdentityAuditActions.RoleCreated, role, result, ct);
    }

    public async Task<Result> GrantPermissionAsync(string role, string permission, CancellationToken ct)
    {
        if (await DeniedAsync(IdentityPermissions.RolesManage, role, ct) is { } denied)
        {
            return Result.Failure(denied);
        }

        if (!await db.Roles.AnyAsync(r => r.Name == role, ct))
        {
            return Result.Failure(new Error(IdentityErrors.NotFound, $"No role '{role}'."));
        }

        if (!await db.RolePermissions.AnyAsync(g => g.RoleName == role && g.PermissionName == permission, ct))
        {
            db.RolePermissions.Add(new RolePermission { RoleName = role, PermissionName = permission });
            await db.SaveChangesAsync(ct);
        }

        return await AuditedAsync(IdentityAuditActions.PermissionGranted, role, IdentityResult.Success, ct,
            new() { ["permission"] = permission });
    }

    public async Task<Result<UserSummary>> MeAsync(CancellationToken ct)
    {
        var user = await CurrentUserAsync();
        return user is null
            ? Result.Failure<UserSummary>(new Error(IdentityErrors.Denied, "Not signed in."))
            : Result.Success(new UserSummary(user.UserName!, [.. await users.GetRolesAsync(user)]));
    }

    public async Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct)
    {
        var user = await CurrentUserAsync();
        if (user is null)
        {
            return Result.Failure(new Error(IdentityErrors.Denied, "Not signed in."));
        }

        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        return await AuditedAsync(IdentityAuditActions.PasswordChanged, user.UserName!, result, ct);
    }

    private async Task<ForgeUser?> CurrentUserAsync() =>
        Caller.Identity?.IsAuthenticated == true ? await users.GetUserAsync(Caller) : null;

    /// <summary>Authenticated, permitted and host scoped — otherwise an audited denial.</summary>
    private async Task<Error?> DeniedAsync(string permission, string subject, CancellationToken ct)
    {
        var reason = Caller.Identity?.IsAuthenticated != true ? "unauthenticated"
            : tenant is { Scope: not TenantScope.Host } ? $"scope:{tenant.Scope}"
            : !await permissions.HasAsync(Caller, permission, ct) ? $"permission:{permission}"
            : null;
        if (reason is null)
        {
            return null;
        }

        await audit.AppendAsync(Event(SecurityEvents.AuthorizationDenied, subject, "denied",
            new() { ["reason"] = reason }), ct);
        return new Error(IdentityErrors.Denied, "Not permitted.");
    }

    private async Task<Result> AuditedAsync(
        string action, string subject, IdentityResult result, CancellationToken ct,
        Dictionary<string, string>? details = null)
    {
        if (!result.Succeeded)
        {
            return Result.Failure(new Error(IdentityErrors.Invalid,
                string.Join("; ", result.Errors.Select(e => e.Description))));
        }

        await audit.AppendAsync(Event(action, subject, "success", details ?? []), ct);
        return Result.Success();
    }

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
