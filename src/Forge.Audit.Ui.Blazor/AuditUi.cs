using System.Reflection;
using System.Resources;
using Forge.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

[assembly: NeutralResourcesLanguage("en-GB")]

namespace Forge.Audit.Ui.Blazor;

/// <summary>Marker for the package's localisation resources (ADR 12): en-GB neutral, no hard-coded English in pages.</summary>
public sealed class AuditUiResources;

/// <summary>The audit page's contribution to the admin shell (ADR 40). Nav visibility is never authorisation.</summary>
internal sealed class AuditAdminContribution(IStringLocalizer<AuditUiResources> text) : IAdminContribution
{
    public IReadOnlyList<AdminNavItem> NavItems => [new(text["NavOperations"], text["NavAuditTrail"], "/admin/audit")];

    public Assembly ComponentAssembly => typeof(AuditAdminContribution).Assembly;
}

/// <summary>Composition surface for <c>ForgeStack.Audit.Ui.Blazor</c>.</summary>
public static class AuditUiExtensions
{
    /// <summary>Registers the audit trail page as an admin-shell contribution. Removing this call (and the package) leaves auditing fully functional.</summary>
    public static IServiceCollection AddForgeAuditUi(this IServiceCollection services)
    {
        services.AddLocalization();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdminContribution, AuditAdminContribution>());
        return services;
    }
}
