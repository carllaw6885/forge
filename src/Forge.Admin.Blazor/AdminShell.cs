using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Forge.Admin.Blazor;

/// <summary>A navigation entry a module contributes to the admin shell (ADR 37).</summary>
public sealed record AdminNavItem(string Section, string Title, string Href);

/// <summary>
/// Explicit module contribution contract (ADRs 01/37): modules add nav items
/// and optionally an assembly containing routable admin components. Registered
/// in DI by each module — never discovered.
/// </summary>
public interface IAdminContribution
{
    IReadOnlyList<AdminNavItem> NavItems { get; }

    /// <summary>Assembly with additional routable components, or null.</summary>
    Assembly? ComponentAssembly => null;
}

/// <summary>Composition surface for the reference admin shell.</summary>
public static class AdminShellExtensions
{
    public static IServiceCollection AddForgeAdminShell(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddRazorComponents();
        services.AddAntiforgery();
        return services;
    }

    /// <summary>
    /// Maps the shell (explicitly, ADR 01) plus the cookie-backed theme switch
    /// endpoint. Pass a policy via <paramref name="configure"/> (e.g.
    /// RequireAuthorization) — the shell itself is auth-agnostic.
    /// </summary>
    public static IEndpointRouteBuilder MapForgeAdminShell(
        this IEndpointRouteBuilder app, Action<IEndpointConventionBuilder>? configure = null)
    {
        var contributions = app.ServiceProvider.GetServices<IAdminContribution>().ToList();
        var additionalAssemblies = contributions
            .Select(c => c.ComponentAssembly)
            .Where(a => a is not null)
            .Cast<Assembly>()
            .Distinct()
            .ToArray();

        var components = app.MapRazorComponents<Components.AdminApp>()
            .AddAdditionalAssemblies(additionalAssemblies);
        configure?.Invoke(components);

        // light/dark/system without JavaScript: a cookie and a redirect
        var theme = app.MapGet("/admin/theme/{mode}", (string mode, string? returnUrl, HttpContext http) =>
        {
            if (mode is not ("light" or "dark" or "system"))
            {
                return Results.BadRequest();
            }

            http.Response.Cookies.Append("forge-theme", mode, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromDays(365),
            });
            return Results.LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/admin" : returnUrl);
        });
        configure?.Invoke(theme);

        return app;
    }
}
