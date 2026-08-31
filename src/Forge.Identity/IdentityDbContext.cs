using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Forge.Identity;

/// <summary>Forge user; standard ASP.NET Core Identity user with SSO/SCIM-ready seams alongside (ADR 06).</summary>
public sealed class ForgeUser : IdentityUser;

/// <summary>Maps a role to a first-class permission (ADR 06: roles aggregate permissions).</summary>
public sealed class RolePermission
{
    public required string RoleName { get; set; }
    public required string PermissionName { get; set; }
}

/// <summary>
/// SSO/SCIM/SAML data-model seam (ADR 06): links a user to an external
/// directory identity with sync state. v0.1 stores the shape; full
/// administration experiences are post-v0.1.
/// </summary>
public sealed class DirectoryLink
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string Provider { get; set; }
    public required string ExternalId { get; set; }
    public string SyncState { get; set; } = "linked";
}

/// <summary>
/// The identity module's own persistence boundary: identity schema, ASP.NET
/// Core Identity + OpenIddict tables, role-permission map and directory seam.
/// Framework-owned entity types are this module's implementation detail.
/// </summary>
public sealed class ForgeIdentityDbContext(DbContextOptions<ForgeIdentityDbContext> options)
    : IdentityDbContext<ForgeUser>(options)
{
    public const string Schema = "identity";

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<DirectoryLink> DirectoryLinks => Set<DirectoryLink>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);
        base.OnModelCreating(builder);
        builder.UseOpenIddict();
        builder.Entity<RolePermission>(rp =>
        {
            rp.HasKey(x => new { x.RoleName, x.PermissionName });
            rp.Property(x => x.RoleName).HasMaxLength(256);
            rp.Property(x => x.PermissionName).HasMaxLength(256);
        });
        builder.Entity<DirectoryLink>(dl =>
        {
            dl.Property(x => x.UserId).HasMaxLength(450);
            dl.Property(x => x.Provider).HasMaxLength(128);
            dl.Property(x => x.ExternalId).HasMaxLength(256);
            dl.Property(x => x.SyncState).HasMaxLength(64);
            dl.HasIndex(x => new { x.Provider, x.ExternalId }).IsUnique();
        });
    }
}
