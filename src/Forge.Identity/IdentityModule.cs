using Forge.Core.Modules;
using Forge.Modularity;
using Forge.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Forge.Identity;

/// <summary>Role-permission map backed by the identity module's own store.</summary>
internal sealed class DbRolePermissionMap(ForgeIdentityDbContext db) : IRolePermissionMap
{
    public async Task<IReadOnlySet<string>> GetPermissionsAsync(
        IEnumerable<string> roles, CancellationToken cancellationToken)
    {
        var roleList = roles.ToList();
        if (roleList.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var permissions = await db.RolePermissions.AsNoTracking()
            .Where(rp => roleList.Contains(rp.RoleName))
            .Select(rp => rp.PermissionName)
            .ToListAsync(cancellationToken);
        return permissions.ToHashSet(StringComparer.Ordinal);
    }
}

/// <summary>
/// The reference identity module (ADR 06): ASP.NET Core Identity + OpenIddict,
/// minimal v0.1 surface — client-credentials token issuance and role-aggregated
/// first-class permissions. SSO/SCIM/SAML stay data-model seams.
/// </summary>
public sealed class IdentityModule(string connectionString) : IForgeModule
{
    public ModuleManifest Manifest { get; } = new()
    {
        Id = "Forge.Identity",
        Name = "Identity",
        Version = "0.1.0",
        OwnedSchemas = [ForgeIdentityDbContext.Schema],
    };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<ForgeIdentityDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", ForgeIdentityDbContext.Schema)));

        services.AddIdentityCore<ForgeUser>(options => options.Password.RequiredLength = 12)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ForgeIdentityDbContext>();

        services.AddForgePermissions();
        services.Replace(ServiceDescriptor.Scoped<IRolePermissionMap, DbRolePermissionMap>());

        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<ForgeIdentityDbContext>())
            .AddServer(options =>
            {
                options.SetTokenEndpointUris("connect/token");
                options.AllowClientCredentialsFlow();
                options.AddEphemeralEncryptionKey()
                    .AddEphemeralSigningKey(); // ponytail: ephemeral keys; persisted certs land with release engineering (Phase 5)
                options.UseAspNetCore().EnableTokenEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });
    }
}
