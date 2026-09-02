using System.Reflection;
using System.Resources;
using Forge.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

[assembly: NeutralResourcesLanguage("en-GB")]

namespace Forge.Tenancy.Ui.Blazor;

/// <summary>Marker for the package's localisation resources (ADR 12): en-GB neutral, no hard-coded English in pages.</summary>
public sealed class TenancyUiResources;

/// <summary>The tenants page's contribution to the admin shell (ADR 40). Nav visibility is never authorisation.</summary>
internal sealed class TenancyAdminContribution(IStringLocalizer<TenancyUiResources> text) : IAdminContribution
{
    public IReadOnlyList<AdminNavItem> NavItems => [new(text["NavAdministration"], text["NavTenants"], "/admin/tenants")];

    public Assembly ComponentAssembly => typeof(TenancyAdminContribution).Assembly;
}

/// <summary>Composition surface for <c>ForgeStack.Tenancy.Ui.Blazor</c>.</summary>
public static class TenancyUiExtensions
{
    /// <summary>Registers the tenants page as an admin-shell contribution. Removing this call (and the package) leaves tenancy fully functional.</summary>
    public static IServiceCollection AddForgeTenancyUi(this IServiceCollection services)
    {
        services.AddLocalization();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdminContribution, TenancyAdminContribution>());
        return services;
    }
}
