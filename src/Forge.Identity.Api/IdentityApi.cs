using Forge.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Forge.Identity.Api;

/// <summary>Body of <c>POST /api/identity/users</c>.</summary>
public sealed record CreateUserRequest(string UserName, string Password);

/// <summary>Body of <c>POST /api/identity/users/{userName}/roles</c>.</summary>
public sealed record AssignRoleRequest(string Role);

/// <summary>Body of <c>POST /api/identity/roles</c>.</summary>
public sealed record CreateRoleRequest(string Name);

/// <summary>Body of <c>POST /api/identity/roles/{role}/permissions</c>.</summary>
public sealed record GrantPermissionRequest(string Permission);

/// <summary>
/// Optional HTTP projection of the identity application contract (ADR 40) for
/// remote consumers. Bearer only; identity data is host owned, so the group is
/// host scoped. Sign-in and account operations (<see cref="IAccountOperations"/>)
/// belong to a signed-in user and are not projected — bearer clients are
/// applications with tokens from <c>/connect/token</c>, not people.
/// </summary>
public static class IdentityApi
{
    public static RouteGroupBuilder MapForgeIdentityApi(
        this IEndpointRouteBuilder app, string prefix = "/api/identity", string authenticationScheme = ForgeApi.BearerScheme)
    {
        var api = app.MapGroup(prefix).RequireBearer(authenticationScheme).WithHostScope().WithTags("Identity");

        api.MapGet("/users", async (IUserAdministration users, int? take, CancellationToken ct) =>
            (await users.ListAsync(take ?? 50, ct)).ToHttpResult());
        api.MapPost("/users", async (IUserAdministration users, CreateUserRequest body, CancellationToken ct) =>
            (await users.CreateAsync(body.UserName, body.Password, ct)).ToHttpResult());
        api.MapPost("/users/{userName}/roles", async (IUserAdministration users, string userName, AssignRoleRequest body, CancellationToken ct) =>
            (await users.AssignRoleAsync(userName, body.Role, ct)).ToHttpResult());

        api.MapGet("/roles", async (IRoleAdministration roles, CancellationToken ct) =>
            (await roles.ListAsync(ct)).ToHttpResult());
        api.MapPost("/roles", async (IRoleAdministration roles, CreateRoleRequest body, CancellationToken ct) =>
            (await roles.CreateAsync(body.Name, ct)).ToHttpResult());
        api.MapPost("/roles/{role}/permissions", async (IRoleAdministration roles, string role, GrantPermissionRequest body, CancellationToken ct) =>
            (await roles.GrantPermissionAsync(role, body.Permission, ct)).ToHttpResult());

        return api;
    }
}
