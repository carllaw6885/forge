using Forge.Admin.Blazor;
using Forge.Auditing;
using Forge.Identity;
using Forge.Jobs;
using Forge.Localization;
using Forge.Modularity;
using Forge.Observability;
using Forge.Persistence.SqlServer;
using Forge.Security;
using Forge.Settings;
using Forge.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using {{NAME}}.Notes;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("forge")
    ?? throw new InvalidOperationException("ConnectionStrings__forge is required");

builder.Services.AddProblemDetails();
builder.Services.AddForgeSecurityDefaults(builder.Configuration);
builder.Services.AddForgeTenancy();
builder.Services.AddForgeObservability("{{NAME_LOWER}}");
builder.Services.AddForgeAdminShell();
// the shell's system pages (audit, settings, jobs, localisation, impersonation banner)
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
builder.Services.ConfigureApplicationCookie(options => options.LoginPath = "/auth/login");
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
app.UseAntiforgery();

app.MapForgeHealth(endpoint => endpoint.WithHostScope());
app.MapIdentityEndpoints();
app.MapNotesEndpoints();
// navigation visibility is never authorisation (ADR 40): the shell requires a signed-in user
app.MapForgeAdminShell(endpoint => endpoint.WithHostScope().RequireAuthorization());

// ponytail: minimal sign-in form; replaced by the shell's first-party login page in 0.2
app.MapGet("/auth/login", (string? returnUrl, HttpContext http, IAntiforgery antiforgery) =>
{
    var token = antiforgery.GetAndStoreTokens(http).RequestToken;
    var target = WebUtility.HtmlEncode(returnUrl ?? "/admin");
    return TypedResults.Content($"""
        <!DOCTYPE html><html lang="en"><head><meta charset="utf-8"><title>Sign in</title></head>
        <body><main><h1>Sign in</h1>
        <form method="post" action="/auth/login">
        <input type="hidden" name="__RequestVerificationToken" value="{token}">
        <input type="hidden" name="returnUrl" value="{target}">
        <p><label>User name <input name="userName" autocomplete="username" required></label></p>
        <p><label>Password <input name="password" type="password" autocomplete="current-password" required></label></p>
        <button type="submit">Sign in</button>
        </form></main></body></html>
        """, "text/html; charset=utf-8");
}).WithHostScope();

app.MapPost("/auth/login", async Task<IResult> (
    [FromForm] LoginRequest request, SignInManager<ForgeUser> signIn, UserManager<ForgeUser> users) =>
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

    // only same-site targets: "/path" but not "//host" (open redirect)
    var target = request.ReturnUrl is ['/', not '/', ..] ? request.ReturnUrl : "/admin";
    await signIn.SignInAsync(user, isPersistent: false);
    return TypedResults.LocalRedirect(target);
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

internal sealed record LoginRequest(string? UserName, string? Password, string? ReturnUrl);
