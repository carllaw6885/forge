using Forge.Admin.Blazor;
using Forge.Audit.Ui.Blazor;
using Forge.Auditing;
using Forge.Identity;
using Forge.Identity.Ui.Blazor;
using Forge.Jobs;
using Forge.Localization;
using Forge.Modularity;
using Forge.Observability;
using Forge.Persistence.SqlServer;
using Forge.Security;
using Forge.Settings;
using Forge.Web;
using Microsoft.AspNetCore.Identity;
using {{NAME}}.Notes;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("forge")
    ?? throw new InvalidOperationException("ConnectionStrings__forge is required");

builder.Services.AddProblemDetails();
builder.Services.AddForgeSecurityDefaults(builder.Configuration);
builder.Services.AddForgeTenancy();
builder.Services.AddForgeObservability("{{NAME_LOWER}}");
builder.Services.AddForgeAdminShell();
builder.Services.AddForgeIdentityUi(); // sign-in, account, users/roles pages: `forge ui remove identity` goes headless
builder.Services.AddForgeAuditUi(); // audit trail page: `forge ui remove audit` goes headless
// the shell's system pages (settings, jobs, localisation, impersonation banner)
builder.Services.AddForgeSettings();
builder.Services.AddForgeLocalization();
builder.Services.AddSqlServerAuditStore(connectionString);
builder.Services.AddForgeAuditing(); // after the SQL store: fills in the redaction policy, TryAdd keeps the SQL IAuditStore
builder.Services.AddSqlServerSettingStore(connectionString);
builder.Services.AddSingleton<ImpersonationService>();
builder.Services.AddSingleton<IImpersonationContext>(sp => sp.GetRequiredService<ImpersonationService>());
// ponytail: in-memory failure sink; switch to ForgeStack.Jobs.Quartz when you need durable jobs
builder.Services.AddSingleton<ITerminalFailureSink, InMemoryTerminalFailureSink>();

// persisted token keys (ADR 18): configure Identity:SigningCertificate/EncryptionCertificate
// with PfxPath (+ PasswordEnvironmentVariable); without them the module falls back to
// ephemeral keys, which production validation refuses at startup.
IdentityKeyMaterial? KeyMaterial(string section) =>
    builder.Configuration[$"Identity:{section}:PfxPath"] is { Length: > 0 } pfxPath
        ? new IdentityKeyMaterial(pfxPath, builder.Configuration[$"Identity:{section}:PasswordEnvironmentVariable"])
        : null;

builder.Services.AddForge(
    new NotesModule(connectionString),
    new IdentityModule(
        connectionString,
        KeyMaterial("SigningCertificate"),
        KeyMaterial("EncryptionCertificate")));

// cookie sign-in over the Identity module for the admin shell
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
app.UseAntiforgery();

app.MapForgeHealth(endpoint => endpoint.WithHostScope());
app.MapIdentityEndpoints();
app.MapNotesEndpoints();
// navigation visibility is never authorisation (ADR 40): the shell requires a signed-in user
app.MapForgeAdminShell(endpoint => endpoint.WithHostScope().RequireAuthorization());

// development seed so the admin shell is reachable out of the box
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ForgeUser>>();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var db = scope.ServiceProvider.GetRequiredService<ForgeIdentityDbContext>();
    if (await users.FindByNameAsync("admin") is null)
    {
        // an "Administrator" role holding every identity and audit permission; the shell's pages are permission-checked
        await roles.CreateAsync(new IdentityRole("Administrator"));
        db.RolePermissions.AddRange(IdentityPermissions.All.Concat(AuditPermissions.All).Select(p =>
            new RolePermission { RoleName = "Administrator", PermissionName = p.Name }));
        await db.SaveChangesAsync();
        await users.CreateAsync(new ForgeUser { UserName = "admin" }, "{{NAME}}!Admin!Passw0rd");
        await users.AddToRoleAsync((await users.FindByNameAsync("admin"))!, "Administrator");
    }
}

app.Run();
