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

/// <summary>Outcome of a password sign-in. An unknown user and a wrong password are indistinguishable.</summary>
public enum SignInOutcome
{
    Succeeded,
    Failed,
    LockedOut,
}

/// <summary>
/// Interactive (cookie) sign-in for hosts that registered identity cookies;
/// first-party UI calls this, never <c>SignInManager</c> (ADR 40).
/// </summary>
public interface ISignInOperations
{
    Task<SignInOutcome> PasswordSignInAsync(string userName, string password, CancellationToken cancellationToken);
    Task SignOutAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Ends every other session of the signed-in user by rotating the security
    /// stamp; the current session is refreshed. Identity keeps no session table,
    /// so "sign out everywhere else" is the v0.2 shape of session management.
    /// </summary>
    Task<Result> SignOutEverywhereElseAsync(CancellationToken cancellationToken);
}

/// <summary>Actions the identity contract audits.</summary>
public static class IdentityAuditActions
{
    public const string UserCreated = "identity.user.created";
    public const string RoleAssigned = "identity.user.role-assigned";
    public const string RoleCreated = "identity.role.created";
    public const string PermissionGranted = "identity.role.permission-granted";
    public const string PasswordChanged = "identity.account.password-changed";
    public const string SignedIn = "identity.account.signed-in";
    public const string SignInFailed = "identity.account.sign-in-failed";
    public const string SignedOut = "identity.account.signed-out";
    public const string SessionsRevoked = "identity.account.sessions-revoked";
}

/// <summary>Cookie sign-in over <c>SignInManager</c>; lockout on failure, every outcome audited.</summary>
internal sealed class SignInOperations(
    SignInManager<ForgeUser> signIn,
    UserManager<ForgeUser> users,
    IAuditStore audit,
    TimeProvider clock,
    IHttpContextAccessor httpContext,
    ICurrentTenant? tenant = null) : ISignInOperations
{
    private ClaimsPrincipal Caller => httpContext.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());

    public async Task<SignInOutcome> PasswordSignInAsync(string userName, string password, CancellationToken ct)
    {
        var user = await users.FindByNameAsync(userName);
        var check = user is null
            ? SignInResult.Failed
            : await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        var outcome = check.Succeeded ? SignInOutcome.Succeeded
            : check.IsLockedOut ? SignInOutcome.LockedOut
            : SignInOutcome.Failed;
        if (outcome == SignInOutcome.Succeeded)
        {
            await signIn.SignInAsync(user!, isPersistent: false);
        }

        await audit.AppendAsync(IdentityAudit.Event(
            outcome == SignInOutcome.Succeeded ? IdentityAuditActions.SignedIn : IdentityAuditActions.SignInFailed,
            userName, tenant?.Id, userName, outcome == SignInOutcome.Succeeded ? "success" : "denied", clock.GetUtcNow(),
            new() { ["outcome"] = outcome.ToString() }), ct);
        return outcome;
    }

    public async Task SignOutAsync(CancellationToken ct)
    {
        var actor = Caller.Identity?.Name ?? "anonymous";
        await signIn.SignOutAsync();
        await audit.AppendAsync(IdentityAudit.Event(
            IdentityAuditActions.SignedOut, actor, tenant?.Id, actor, "success", clock.GetUtcNow(), []), ct);
    }

    public async Task<Result> SignOutEverywhereElseAsync(CancellationToken ct)
    {
        var user = Caller.Identity?.IsAuthenticated == true ? await users.GetUserAsync(Caller) : null;
        if (user is null)
        {
            return Result.Failure(new Error(IdentityErrors.Denied, "Not signed in."));
        }

        await users.UpdateSecurityStampAsync(user);
        await signIn.RefreshSignInAsync(user);
        await audit.AppendAsync(IdentityAudit.Event(
            IdentityAuditActions.SessionsRevoked, user.UserName!, tenant?.Id, user.UserName!, "success", clock.GetUtcNow(), []), ct);
        return Result.Success();
    }
}

internal static class IdentityAudit
{
    public static AuditEvent Event(
        string action, string actor, string? tenantId, string subject, string outcome,
        DateTimeOffset at, Dictionary<string, string> details) => new()
        {
            Action = action,
            TenantId = tenantId,
            Actor = actor,
            CorrelationId = Activity.Current?.TraceId.ToString() ?? CorrelationId.New().ToString(),
            Subject = subject,
            Outcome = outcome,
            OccurredAt = at,
            Details = details,
        };
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

    private AuditEvent Event(string action, string subject, string outcome, Dictionary<string, string> details) =>
        IdentityAudit.Event(action, Actor, tenant?.Id, subject, outcome, clock.GetUtcNow(), details);
}
