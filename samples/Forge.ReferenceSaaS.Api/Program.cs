using Forge.Admin.Blazor;
using Forge.Audit.Ui.Blazor;
using Forge.Identity;
using Forge.Identity.Ui.Blazor;
using Forge.Jobs;
using Forge.Jobs.Quartz;
using Forge.Localization;
using Forge.Modularity;
using Forge.Observability;
using Forge.Persistence.SqlServer;
using Forge.ReferenceCatalog;
using Forge.ReferenceSaaS.ServiceDefaults;
using Forge.Security;
using Forge.Settings;
using Forge.Tenancy;
using Forge.Tenancy.Ui.Blazor;
using Forge.Web;
using Microsoft.AspNetCore.Identity;

// The reference SaaS host (ADRs 01/25): three modules composed explicitly —
// Catalog, Identity, and the admin shell — over the full Forge pipeline.
// Runs identically under Aspire and as a bare OCI container.

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("forge")
    ?? throw new InvalidOperationException("ConnectionStrings__forge is required");

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddServiceDefaults("forge-referencesaas");
builder.Services.AddForgeSecurityDefaults(builder.Configuration);
builder.Services.AddForgeTenancy();
builder.Services.AddForgeIdempotency();
builder.Services.AddForgeSettings();
builder.Services.AddForgeLocalization();
builder.Services.AddForgeAdminShell();
builder.Services.AddForgeIdentityUi(); // sign-in, account and users/roles pages; delete this line and the reference to go headless
builder.Services.AddForgeAuditUi(); // audit trail page; same deal
builder.Services.AddForgeTenancyUi(); // tenants page; same deal
builder.Services.AddSingleton<ImpersonationService>();
builder.Services.AddSingleton<IImpersonationContext>(sp => sp.GetRequiredService<ImpersonationService>());
builder.Services.AddSqlServerAuditStore(connectionString);
builder.Services.AddSqlServerSettingStore(connectionString);
builder.Services.AddSqlServerTenantDirectory(connectionString); // makes tenant state authoritative: unknown/disabled tenants fail resolution
builder.Services.AddForgeQuartzJobs(new ForgeQuartzOptions { ConnectionString = connectionString });
// persisted token keys (ADR 18): configure Identity:SigningCertificate/EncryptionCertificate
// with PfxPath (+ PasswordEnvironmentVariable); without them the module falls back to
// ephemeral keys, which production validation refuses at startup.
IdentityKeyMaterial? KeyMaterial(string section) =>
    builder.Configuration[$"Identity:{section}:PfxPath"] is { Length: > 0 } pfxPath
        ? new IdentityKeyMaterial(pfxPath, builder.Configuration[$"Identity:{section}:PasswordEnvironmentVariable"])
        : null;

builder.Services.AddForge(
    new CatalogModule(connectionString),
    new IdentityModule(
        connectionString,
        KeyMaterial("SigningCertificate"),
        KeyMaterial("EncryptionCertificate")));

// cookie sign-in over the Identity module for the admin acceptance journey
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

var app = builder.Build();
app.Services.UseForge();
app.UseForgeSecurityDefaults();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseForgeTenancy();
app.UseForgeRequestCulture();
app.UseForgeIdempotency();
app.UseAntiforgery();

app.MapOpenApi().WithHostScope();
app.MapForgeHealth(endpoint => endpoint.WithHostScope());
app.MapIdentityEndpoints().WithHostScope();
app.MapCatalogEndpoints();
// navigation visibility is never authorisation (ADR 40): the shell requires a signed-in user
app.MapForgeAdminShell(endpoint => endpoint.WithHostScope().RequireAuthorization());

// development seed: admin user, editor role/permission, demo tenant item
if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Forge:Seed", false))
{
    using var scope = app.Services.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ForgeUser>>();
    if (await users.FindByNameAsync("admin") is null)
    {
        await users.CreateAsync(new ForgeUser { UserName = "admin" }, "Forge!Admin!Passw0rd");
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new IdentityRole("administrator"));
        await users.AddToRoleAsync((await users.FindByNameAsync("admin"))!, "administrator");
        var db = scope.ServiceProvider.GetRequiredService<ForgeIdentityDbContext>();
        db.RolePermissions.Add(new RolePermission { RoleName = "administrator", PermissionName = "Catalog.Items.Create" });
        db.RolePermissions.AddRange(TenancyPermissions.All.Select(p =>
            new RolePermission { RoleName = "administrator", PermissionName = p.Name }));
        await db.SaveChangesAsync();
    }

    // the directory is authoritative, so the demo tenant must exist before any tenant-scoped request
    var directory = scope.ServiceProvider.GetRequiredService<ITenantDirectory>();
    if (await directory.GetAsync("smoke", CancellationToken.None) is null)
    {
        await directory.SaveAsync(new Tenant("smoke", "Smoke", Enabled: true, DateTimeOffset.UtcNow), CancellationToken.None);
    }
}

app.Run();
