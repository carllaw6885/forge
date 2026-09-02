# ForgeStack.Identity.Ui.Blazor

Optional first-party UI for the Identity capability (ADR 40). Ships sign-in, account and user/role administration pages as a Razor class library that plugs into the admin shell. The capability (`ForgeStack.Identity`) is fully usable without it.

## Install

`forge new --template saas` includes it. To attach it to an existing host that already maps the admin shell:

```bash
forge ui add identity        # adds the PackageReference and builder.Services.AddForgeIdentityUi()
```

or by hand:

```xml
<PackageReference Include="ForgeStack.Identity.Ui.Blazor" Version="$(ForgeVersion)" />
```

```csharp
builder.Services.AddForgeAdminShell();
builder.Services.AddForgeIdentityUi();   // pages + nav contribution + cookie LoginPath
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
// ...
app.MapForgeAdminShell(endpoint => endpoint.RequireAuthorization());
```

The host keeps choosing the authentication scheme; the package only points the Identity application cookie's `LoginPath` at `/account/sign-in`.

## Remove

```bash
forge ui remove identity
```

Deleting the package reference and the `AddForgeIdentityUi()` line removes exactly these pages; nothing else changes and the shell's system pages keep working. The PR gate proves this on the packed packages.

## Pages

| Route | Page | Needs |
|---|---|---|
| `/account/sign-in` | password sign-in (lockout after 5 failures, one message for unknown user and wrong password) | anonymous |
| `/account/signed-out` | confirmation after sign-out | anonymous |
| `/account` | profile (user name, roles), sign out | signed in |
| `/account/password` | change password | signed in |
| `/account/sessions` | *sign out everywhere else* — rotates the security stamp so every other cookie is rejected; the current session is refreshed | signed in |
| `/admin/users` | list/create users, assign roles | `Identity.Users.Read` (list), `Identity.Users.Manage` (create/assign) |
| `/admin/roles` | list/create roles, grant permissions | `Identity.Roles.Manage` |

Navigation visibility is never authorisation: every page enforces through the application contract (`IAccountOperations`, `ISignInOperations`, `IUserAdministration`, `IRoleAdministration`), which checks permission and host scope inside and audits denials and mutations.

Identity keeps no session table, so "sessions" is sign-out-everywhere-else rather than a session list. MFA, passkeys and recovery are designed for, not shipped.

Strings live in `IdentityUiResources` (en-GB neutral, ar-SA shipped, RTL verified); the pages inherit the shell's design tokens and are gated by the WCAG 2.2 AA suite.

## Upgrading from 0.1.x

- **Users and Roles pages moved.** They were part of `ForgeStack.Admin.Blazor`; they now live here at the same routes. Hosts that want them add this package. Hosts that do not want them get a shell without them — nothing to remove.
- **`SignInManager` / `ISecurityStampValidator` are registered by `IdentityModule`.** Delete any host registrations of `SignInManager<ForgeUser>` or `SecurityStampValidator<ForgeUser>`; keep `AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()`.
- **The `--admin` template's `/auth/login` form is gone.** Hosts generated from 0.1.x can delete their `/auth/login` endpoints and `LoginRequest` record once this package is attached; `ConfigureApplicationCookie(o => o.LoginPath = ...)` is no longer needed.
- **Development seed** (`admin` / `<Name>!Admin!Passw0rd`, Administrator role with every identity permission) stays in the generated `Program.cs`, `Development` environment only.
