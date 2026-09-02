# ForgeStack.Tenancy.Ui.Blazor

Optional first-party UI for the Tenancy capability (ADR 40): one `/admin/tenants` page that lists, searches, creates, renames, enables and disables tenants through `ITenantAdministration`. The capability (`ForgeStack.Tenancy`) is fully usable without it.

## The tenant directory

Phase 8 makes tenants first-class records. `ITenantDirectory` (in `ForgeStack.Tenancy`) is the authoritative registry; `AddSqlServerTenantDirectory(connectionString)` registers the SQL Server implementation (`tenancy` schema, migrated by your DbMigrator like every module schema).

Registering a directory changes resolution (ADR 05): `UseForgeTenancy` rejects a resolved tenant that is unknown or disabled with a 403 Problem Details response. With no directory registered, tenant ids stay opaque and unchecked — the v0.1 behaviour.

## Install

`forge new --template saas` includes it. For an existing host that maps the admin shell:

```bash
forge ui add tenancy         # adds the PackageReference and builder.Services.AddForgeTenancyUi()
```

```csharp
builder.Services.AddForgeAdminShell();
builder.Services.AddForgeTenancyUi();
builder.Services.AddSqlServerTenantDirectory(connectionString);
builder.Services.AddForgePermissions();  // registers ITenantAdministration; IdentityModule already calls this
```

## Remove

```bash
forge ui remove tenancy
```

## The page

| Feature | Needs |
|---|---|
| Tenant list with search (exact id or name substring), state, created date | `Tenancy.Read`, host scope |
| Create, rename, enable/disable — every mutation audited (`tenant.created` / `tenant.renamed` / `tenant.enabled` / `tenant.disabled`) | `Tenancy.Manage`, host scope |
| Per-tenant audit history — a link to `/admin/audit` pre-filtered to the tenant | `Audit.Read` on the audit page |

Tenant administration is host-scope only — it is cross-tenant by nature. Navigation visibility is never authorisation: the page calls `ITenantAdministration` and nothing else, and the contract checks permission and scope inside and audits every denial.

Strings live in `TenancyUiResources` (en-GB neutral, ar-SA shipped); the page inherits the shell's design tokens and is gated by the WCAG 2.2 AA suite.
