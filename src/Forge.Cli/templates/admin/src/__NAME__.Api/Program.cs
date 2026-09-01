using Forge.Admin.Blazor;
using Forge.Identity;
using Forge.Modularity;
using Forge.Observability;
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
app.UseAntiforgery();

app.MapForgeHealth(endpoint => endpoint.WithHostScope());
app.MapIdentityEndpoints();
app.MapNotesEndpoints();
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

// development seed so the admin shell is reachable out of the box
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ForgeUser>>();
    if (await users.FindByNameAsync("admin") is null)
    {
        await users.CreateAsync(new ForgeUser { UserName = "admin" }, "{{NAME}}!Admin!Passw0rd");
    }
}

app.Run();

internal sealed record LoginRequest(string? UserName, string? Password);
