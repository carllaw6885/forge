using System.Reflection;
using System.Resources;
using Forge.Admin.Blazor;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

[assembly: NeutralResourcesLanguage("en-GB")]

namespace Forge.Identity.Ui.Blazor;

/// <summary>Marker for the package's localisation resources (ADR 12): en-GB neutral, no hard-coded English in pages.</summary>
public sealed class IdentityUiResources;

/// <summary>
/// The identity pages' contribution to the admin shell (ADR 40): navigation
/// entries and the routable component assembly. Nav visibility is never
/// authorisation — every page enforces through the application contract.
/// </summary>
internal sealed class IdentityAdminContribution(IStringLocalizer<IdentityUiResources> text) : IAdminContribution
{
    public IReadOnlyList<AdminNavItem> NavItems =>
    [
        new(text["NavIdentity"], text["NavUsers"], "/admin/users"),
        new(text["NavIdentity"], text["NavRoles"], "/admin/roles"),
        new(text["NavMyAccount"], text["NavProfile"], "/account"),
        new(text["NavMyAccount"], text["NavPassword"], "/account/password"),
        new(text["NavMyAccount"], text["NavSessions"], "/account/sessions"),
    ];

    public Assembly ComponentAssembly => typeof(IdentityAdminContribution).Assembly;
}

/// <summary>Composition surface for <c>ForgeStack.Identity.Ui.Blazor</c>.</summary>
public static class IdentityUiExtensions
{
    /// <summary>Path of the sign-in page; unauthenticated requests to protected pages redirect here.</summary>
    public const string SignInPath = "/account/sign-in";

    /// <summary>
    /// Registers the identity pages as an admin-shell contribution and points
    /// the identity cookie's login path at the sign-in page. Removing this call
    /// (and the package) leaves the identity capability fully functional.
    /// </summary>
    public static IServiceCollection AddForgeIdentityUi(this IServiceCollection services)
    {
        services.AddLocalization();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdminContribution, IdentityAdminContribution>());
        // PostConfigure: AddIdentityCookies sets its own LoginPath whichever order the host registers in
        services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options => options.LoginPath = SignInPath);
        return services;
    }

    /// <summary>Same-site targets only ("/path", never "//host" or "/\host"): open-redirect guard.</summary>
    public static string SafeReturnUrl(string? returnUrl, string fallback = "/admin") =>
        returnUrl is ['/', not ('/' or '\\'), ..] ? returnUrl : fallback;
}
