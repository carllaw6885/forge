using System.Reflection;

namespace Forge.Admin;

/// <summary>A navigation entry a module contributes to the admin shell (ADR 37).</summary>
public sealed record AdminNavItem(string Section, string Title, string Href);

/// <summary>
/// Explicit module contribution contract (ADRs 01/37): modules add nav items
/// and optionally an assembly containing routable admin components. Registered
/// in DI by each module — never discovered. Nav visibility is not
/// authorisation; every page enforces through its application contract (ADR 40).
/// </summary>
public interface IAdminContribution
{
    IReadOnlyList<AdminNavItem> NavItems { get; }

    /// <summary>Assembly with additional routable components, or null.</summary>
    Assembly? ComponentAssembly => null;
}
