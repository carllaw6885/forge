# ForgeStack.Audit.Ui.Blazor

Optional first-party UI for the Audit capability (ADR 40): one `/admin/audit` page that lists, filters, verifies and exports the trail through `IAuditQueries`. The capability (`ForgeStack.Auditing`) is fully usable without it.

## Install

`forge new --admin` includes it. For an existing host that maps the admin shell:

```bash
forge ui add audit           # adds the PackageReference and builder.Services.AddForgeAuditUi()
```

```csharp
builder.Services.AddForgeAdminShell();
builder.Services.AddForgeAuditUi();
builder.Services.AddForgePermissions();  // registers IAuditQueries; IdentityModule already calls this
```

Export needs an `IImmutableEvidenceStore` registered by the host (e.g. `FileImmutableEvidenceStore`); without one the page reports it and nothing is written.

## Remove

```bash
forge ui remove audit
```

## The page

| Feature | Needs |
|---|---|
| Timeline, newest first, exact-match filters on actor / action / subject / correlation id (query string, so a filtered view is a link) | `Audit.Read` — tenant-scoped users see only their own tenant's events; host scope sees the whole trail |
| Verify hash chain — reports record count and the evidence store in use; appends `audit.verified` | `Audit.Verify`, host scope |
| Export — JSON Lines to the evidence store, returns the evidence id; appends `audit.exported` | `Audit.Export`, host scope |

Navigation visibility is never authorisation: the page calls `IAuditQueries` and nothing else, and the contract checks permission and scope inside and audits every denial.

Strings live in `AuditUiResources` (en-GB neutral, ar-SA shipped); the page inherits the shell's design tokens and is gated by the WCAG 2.2 AA suite.

## Upgrading from 0.2.0-preview.1

- **The Audit page moved out of `ForgeStack.Admin.Blazor`** to this package at the same route. `forge ui add audit` (or the two lines above) brings it back; grant `Audit.Read` / `Audit.Verify` / `Audit.Export` to the roles that should see it — the old shell page read `IAuditStore` directly with no permission check.
- **`IAdminContribution` / `AdminNavItem` moved to `ForgeStack.Admin.Abstractions`** (namespace `Forge.Admin`). The shell type-forwards them, so existing binaries keep working; update `using Forge.Admin.Blazor;` to `using Forge.Admin;` when you recompile.
- **`IAuditStore` gained `ReadLatestAsync`** with a default implementation; custom stores keep compiling and may override it with a real query.
