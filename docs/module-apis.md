# ForgeStack.Identity.Api and ForgeStack.Audit.Api

Optional Minimal-API projections of the Identity and Audit application contracts (ADR 40). Each package is one `Map…` call over the contract: **bearer only** (OpenIddict validation — cookies are never accepted, anonymous is a bare 401, never a sign-in redirect), and contract failures come back as Problem Details whose `type` is the stable error code. Permission, scope and audit are enforced inside the contract, exactly as for the Blazor pages; the projection adds nothing of its own.

## Install

```bash
forge new Acme --template api          # headless host: both APIs, no admin shell
forge new Acme --template saas --with-api  # admin shell plus both APIs
forge api add identity                 # or attach to an existing saas/api host
forge api add audit
forge api remove identity              # idempotent; byte-for-byte round trip
```

What `forge api add` writes into the host:

```csharp
app.MapIdentityEndpoints().WithHostScope();
app.MapForgeIdentityApi();                 // /api/identity, host scoped by the package
app.MapForgeAuditApi().WithHostScope();    // /api/audit — the host decides the scope
```

Both accept `prefix` and `authenticationScheme` parameters (`ForgeApi.BearerScheme` by default). `ForgeApi.ToHttpResult` and `RequireBearer` in `ForgeStack.Web` are the shared shape for your own module APIs.

## Endpoints

| Route | Contract call | Permission |
|---|---|---|
| `GET /api/identity/users?take=` | `IUserAdministration.ListAsync` | `Identity.Users.Read` |
| `POST /api/identity/users` `{userName,password}` | `CreateAsync` | `Identity.Users.Manage` |
| `POST /api/identity/users/{userName}/roles` `{role}` | `AssignRoleAsync` | `Identity.Users.Manage` |
| `GET /api/identity/roles` | `IRoleAdministration.ListAsync` | `Identity.Roles.Manage` |
| `POST /api/identity/roles` `{name}` | `CreateAsync` | `Identity.Roles.Manage` |
| `POST /api/identity/roles/{role}/permissions` `{permission}` | `GrantPermissionAsync` | `Identity.Roles.Manage` |
| `GET /api/audit?actor=&action=&subject=&correlationId=&beforeSequence=&take=` | `IAuditQueries.ListAsync` | `Audit.Read` (tenant-scoped callers see their own tenant) |
| `POST /api/audit/verify` | `VerifyAsync` | `Audit.Verify`, host scope |
| `POST /api/audit/export` | `ExportAsync` | `Audit.Export`, host scope; 409 `audit.no-evidence-store` without an `IImmutableEvidenceStore` |

Sign-in and account operations are not projected: a bearer caller is an application, not a person.

Status mapping: `*.denied` → 403, `*.not-found` → 404, `*.invalid` → 400, anything else → 409. Success is `200` with the contract's value or `204` for `Result`.

## Getting a token

Client credentials against `/connect/token`. A client is authorised through roles like a user: give the OpenIddict application a `role:<Role>` permission and the token carries that role, so `Administrator`'s permissions apply.

```csharp
await applications.CreateAsync(new OpenIddictApplicationDescriptor
{
    ClientId = "reporting", ClientSecret = "…",
    Permissions =
    {
        OpenIddictConstants.Permissions.Endpoints.Token,
        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
        IdentityEndpoints.RolePermissionPrefix + "Auditor",
    },
});
```

```bash
curl -X POST https://host/connect/token -d 'grant_type=client_credentials&client_id=reporting&client_secret=…'
curl -H "Authorization: Bearer $TOKEN" https://host/api/audit?action=security.login.failed
```

`forge new --template saas|api` seeds a `dev-client` (`{Name}!Dev!Client!Secret`) in the `Administrator` role in Development only, and the Identity module accepts plain-HTTP token requests only in Development.

## Upgrading from 0.2.0-preview.1

- **`MapIdentityEndpoints()` now returns `IEndpointConventionBuilder`** so the host can mark `/connect/token` `.WithHostScope()` — under `UseForgeTenancy` it previously demanded a tenant header. Update the call to `app.MapIdentityEndpoints().WithHostScope();`.
