using Forge.Auditing;
using Forge.Core.Modules;
using Forge.Modularity;
using Forge.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

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
/// Token key material (ADR 18): a PFX on disk, with its password supplied via
/// an environment variable — inject it with your secret mechanism (the
/// reference ISecretStore is environment-based, so the two compose).
/// </summary>
public sealed record IdentityKeyMaterial(string PfxPath, string? PasswordEnvironmentVariable = null)
{
    internal System.Security.Cryptography.X509Certificates.X509Certificate2 Load() =>
        System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
            PfxPath,
            PasswordEnvironmentVariable is null ? null : Environment.GetEnvironmentVariable(PasswordEnvironmentVariable));
}

/// <summary>
/// Refuses production startup on ephemeral token keys (ADR 18): a restart
/// would silently invalidate every issued token.
/// </summary>
internal sealed class PersistedKeyMaterialValidator(bool hasSigning, bool hasEncryption)
    : Forge.Core.Validation.IProductionConfigurationValidator
{
    public IEnumerable<string> Validate()
    {
        if (!hasSigning)
        {
            yield return "Identity must use a persisted signing certificate in production (IdentityKeyMaterial), not ephemeral keys";
        }

        if (!hasEncryption)
        {
            yield return "Identity must use a persisted encryption certificate in production (IdentityKeyMaterial), not ephemeral keys";
        }
    }
}

/// <summary>
/// The reference identity module (ADR 06): ASP.NET Core Identity + OpenIddict,
/// minimal v0.1 surface — client-credentials token issuance and role-aggregated
/// first-class permissions. SSO/SCIM/SAML stay data-model seams.
/// Without key material, token keys are ephemeral — a development convenience
/// that production validation refuses.
/// </summary>
public sealed class IdentityModule(
    string connectionString,
    IdentityKeyMaterial? signingCertificate = null,
    IdentityKeyMaterial? encryptionCertificate = null) : IForgeModule
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
            .AddEntityFrameworkStores<ForgeIdentityDbContext>()
            .AddSignInManager();
        // AddIdentityCore omits this; the identity cookie handler requires it on every authenticated request.
        // Hosts still choose the scheme: AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies().
        services.TryAddScoped<ISecurityStampValidator, SecurityStampValidator<ForgeUser>>();

        services.AddForgePermissions();
        services.Replace(ServiceDescriptor.Scoped<IRolePermissionMap, DbRolePermissionMap>());

        // application contract (ADR 40): the one front door for UI, HTTP and custom callers;
        // every denial and mutation is audited (TryAdd: a SQL audit store registered earlier wins)
        services.AddForgeAuditing();
        services.AddHttpContextAccessor();
        services.AddScoped<IdentityOperations>();
        services.AddScoped<IUserAdministration>(sp => sp.GetRequiredService<IdentityOperations>());
        services.AddScoped<IRoleAdministration>(sp => sp.GetRequiredService<IdentityOperations>());
        services.AddScoped<IAccountOperations>(sp => sp.GetRequiredService<IdentityOperations>());
        services.AddScoped<ISignInOperations, SignInOperations>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<Forge.Core.Validation.IProductionConfigurationValidator>(
            new PersistedKeyMaterialValidator(signingCertificate is not null, encryptionCertificate is not null)));

        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<ForgeIdentityDbContext>())
            .AddServer(options =>
            {
                options.SetTokenEndpointUris("connect/token");
                options.AllowClientCredentialsFlow();
                if (encryptionCertificate is not null)
                {
                    options.AddEncryptionCertificate(encryptionCertificate.Load());
                }
                else
                {
                    options.AddEphemeralEncryptionKey(); // development only; production validation refuses this
                }

                if (signingCertificate is not null)
                {
                    options.AddSigningCertificate(signingCertificate.Load());
                }
                else
                {
                    options.AddEphemeralSigningKey(); // development only; production validation refuses this
                }
                options.UseAspNetCore().EnableTokenEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        // plain-HTTP token requests only ever in Development (local hosts, CI smoke); production keeps HTTPS mandatory
        services.AddOptions<OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreOptions>()
            .Configure<IHostEnvironment>((options, env) => options.DisableTransportSecurityRequirement = env.IsDevelopment());
    }
}
