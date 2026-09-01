using Forge.Admin.Blazor;
using Forge.Identity;
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
builder.Services.AddSingleton<ImpersonationService>();
builder.Services.AddSingleton<IImpersonationContext>(sp => sp.GetRequiredService<ImpersonationService>());
builder.Services.AddSqlServerAuditStore(connectionString);
builder.Services.AddSqlServerSettingStore(connectionString);
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
builder.Services.AddScoped<SignInManager<ForgeUser>>();
// AddIdentityCore omits this; the identity cookie handler requires it on every authenticated request
builder.Services.AddScoped<ISecurityStampValidator, SecurityStampValidator<ForgeUser>>();

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
app.MapIdentityEndpoints();
app.MapCatalogEndpoints();
app.MapForgeAdminShell(endpoint => endpoint.WithHostScope());

app.MapPost("/auth/login", async Task<IResult> (
    LoginRequest request, SignInManager<ForgeUser> signIn, UserManager<ForgeUser> users) =>
{
    var user = await users.FindByNameAsync(request.UserName ?? "");
    if (user is null)
    {
        return TypedResults.Unauthorized();
    }

    var result = await signIn.CheckPasswordSignInAsync(user, request.Password ?? "", lockoutOnFailure: true);
    if (!result.Succeeded)
    {
        return TypedResults.Unauthorized();
    }

    await signIn.SignInAsync(user, isPersistent: false);
    return TypedResults.Ok();
}).WithHostScope().WithName("Login");

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
        await db.SaveChangesAsync();
    }
}

app.Run();

internal sealed record LoginRequest(string? UserName, string? Password);
