using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Forge.Identity;

/// <summary>Token endpoint (mapped explicitly by the host, ADR 01). v0.1: client credentials only.</summary>
public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/connect/token", async (HttpContext context, IOpenIddictApplicationManager applications) =>
        {
            var request = context.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("no OpenIddict request in context");

            if (!request.IsClientCredentialsGrantType())
            {
                return Results.Forbid(
                    authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            // Client authentication (id + secret) already happened in the
            // OpenIddict server pipeline before passthrough reaches us.
            var application = await applications.FindByClientIdAsync(request.ClientId!)
                ?? throw new InvalidOperationException("authenticated client vanished");

            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            identity.SetClaim(OpenIddictConstants.Claims.Subject, request.ClientId);
            identity.SetClaim(OpenIddictConstants.Claims.Name,
                await applications.GetDisplayNameAsync(application));
            identity.SetDestinations(_ => [OpenIddictConstants.Destinations.AccessToken]);

            return Results.SignIn(new ClaimsPrincipal(identity),
                authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        })
        .WithName("Token");

        return app;
    }
}
