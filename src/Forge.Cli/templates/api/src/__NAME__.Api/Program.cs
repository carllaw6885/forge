using Forge.Auditing;
using Forge.Identity;
using Forge.Modularity;
using Forge.Observability;
using Forge.Persistence.SqlServer;
using Forge.Security;
using Forge.Web;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using {{NAME}}.Notes;

// headless host: bearer tokens from /connect/token, module APIs, no admin shell (`forge new --template saas` for that)
var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("forge")
    ?? throw new InvalidOperationException("ConnectionStrings__forge is required");

builder.Services.AddProblemDetails();
builder.Services.AddForgeSecurityDefaults(builder.Configuration);
builder.Services.AddForgeTenancy();
builder.Services.AddForgeObservability("{{NAME_LOWER}}");
builder.Services.AddSqlServerAuditStore(connectionString);
builder.Services.AddForgeAuditing(); // after the SQL store: fills in the redaction policy, TryAdd keeps the SQL IAuditStore

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

var app = builder.Build();
app.Services.UseForge();
app.UseForgeSecurityDefaults();
app.UseAuthentication();
app.UseAuthorization();
app.UseForgeTenancy();

app.MapForgeHealth(endpoint => endpoint.WithHostScope());
app.MapIdentityEndpoints().WithHostScope();
app.MapNotesEndpoints();

// development seed so the APIs are reachable out of the box: client_credentials at /connect/token
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    if (await applications.FindByClientIdAsync("dev-client") is null)
    {
        // an "Administrator" role holding every identity and audit permission; the APIs are permission-checked
        var db = scope.ServiceProvider.GetRequiredService<ForgeIdentityDbContext>();
        await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>().CreateAsync(new IdentityRole("Administrator"));
        db.RolePermissions.AddRange(IdentityPermissions.All.Concat(AuditPermissions.All).Select(p =>
            new RolePermission { RoleName = "Administrator", PermissionName = p.Name }));
        await db.SaveChangesAsync();
        await applications.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "dev-client",
            ClientSecret = "{{NAME}}!Dev!Client!Secret",
            DisplayName = "Development client",
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                IdentityEndpoints.RolePermissionPrefix + "Administrator",
            },
        });
    }
}

app.Run();
