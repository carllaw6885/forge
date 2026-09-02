using Forge.Core.Primitives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Forge.Web;

/// <summary>
/// Shared shape of every <c>ForgeStack.*.Api</c> projection (ADR 40): bearer
/// authentication only, and contract failures rendered as Problem Details
/// whose <c>type</c> is the stable error code.
/// </summary>
public static class ForgeApi
{
    /// <summary>OpenIddict's validation scheme — what <c>IdentityModule</c> registers for access tokens.</summary>
    public const string BearerScheme = "OpenIddict.Validation.AspNetCore";

    /// <summary>Authenticates with <paramref name="scheme"/> only; cookies are never accepted here.</summary>
    public static TBuilder RequireBearer<TBuilder>(this TBuilder builder, string scheme = BearerScheme)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(new AuthorizationPolicyBuilder(scheme).RequireAuthenticatedUser().Build());

    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error);

    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

    // error codes are "<module>.<kind>"; the kind decides the status, the whole code travels as `type`
    private static IResult Problem(Error error) => Results.Problem(
        statusCode: error.Code[(error.Code.LastIndexOf('.') + 1)..] switch
        {
            "denied" => StatusCodes.Status403Forbidden,
            "not-found" => StatusCodes.Status404NotFound,
            "invalid" => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status409Conflict,
        },
        title: error.Message,
        type: error.Code);
}
