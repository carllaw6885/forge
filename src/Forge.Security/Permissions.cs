using System.Security.Claims;

namespace Forge.Security;

/// <summary>
/// A first-class permission (ADR 06): checks are made against permissions,
/// never against role names. Roles merely aggregate permissions.
/// </summary>
public sealed record Permission(string Name, string DisplayName);

/// <summary>Claim type carrying a directly granted permission.</summary>
public static class ForgeClaimTypes
{
    public const string Permission = "forge:permission";
}

/// <summary>Explicitly populated catalogue of every permission modules declare (ADR 01: no scanning).</summary>
public sealed class PermissionCatalog
{
    private readonly Dictionary<string, Permission> _permissions = new(StringComparer.Ordinal);

    public IReadOnlyCollection<Permission> All => _permissions.Values;

    public PermissionCatalog Add(Permission permission)
    {
        if (!_permissions.TryAdd(permission.Name, permission))
        {
            throw new InvalidOperationException($"permission '{permission.Name}' is already declared");
        }

        return this;
    }
}

/// <summary>Resolves the permissions a set of roles aggregates to (backed by the identity store).</summary>
public interface IRolePermissionMap
{
    Task<IReadOnlySet<string>> GetPermissionsAsync(IEnumerable<string> roles, CancellationToken cancellationToken);
}

/// <summary>In-memory reference map for tests and hosts without the identity module.</summary>
public sealed class InMemoryRolePermissionMap : IRolePermissionMap
{
    private readonly Dictionary<string, HashSet<string>> _map = new(StringComparer.Ordinal);

    public InMemoryRolePermissionMap Grant(string role, params string[] permissions)
    {
        _map.TryAdd(role, new HashSet<string>(StringComparer.Ordinal));
        _map[role].UnionWith(permissions);
        return this;
    }

    public Task<IReadOnlySet<string>> GetPermissionsAsync(IEnumerable<string> roles, CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            if (_map.TryGetValue(role, out var permissions))
            {
                result.UnionWith(permissions);
            }
        }

        return Task.FromResult<IReadOnlySet<string>>(result);
    }
}

/// <summary>Permission decision point: direct permission claims OR role-aggregated permissions.</summary>
public interface IPermissionChecker
{
    Task<bool> HasAsync(ClaimsPrincipal user, string permission, CancellationToken cancellationToken);
}

public sealed class DefaultPermissionChecker(IRolePermissionMap rolePermissions) : IPermissionChecker
{
    public async Task<bool> HasAsync(ClaimsPrincipal user, string permission, CancellationToken cancellationToken)
    {
        if (user.HasClaim(ForgeClaimTypes.Permission, permission))
        {
            return true; // permission checks are independent of roles (ADR 06)
        }

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value);
        var aggregated = await rolePermissions.GetPermissionsAsync(roles, cancellationToken);
        return aggregated.Contains(permission);
    }
}
