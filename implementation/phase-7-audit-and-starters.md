# Phase 7 - Audit experience, `.Api` projections, starters (v0.2, Stream A, slice 2)

**Window:** second v0.2 phase; starts 2 September 2026

**Exit goal:** The ADR 40 layering proven once for Identity repeats for Audit (contract → `.Ui.Blazor`), the optional `.Api` projection is proven for both, and `forge new` grows named templates. `IAdminContribution` moves to its own package so module UIs never depend on the shell.

ADRs: 06, 08, 12, 22, 30, 37, 40. Roadmap: `docs/POST_V0.1_ROADMAP.md` § Module layering, § Audit surfaces, § Starters.

## 7.1 Audit application contract (`IAuditQueries`)

- [x] `IAuditQueries` in `Forge.Auditing` (`ListAsync` with exact-match filters + newest-first paging, `VerifyAsync`, `ExportAsync`); `AuditErrors`. Implementation and `AuditPermissions` (`Audit.Read|Verify|Export`) in `Forge.Security` (the permission decision point), registered by `AddForgePermissions()`.
- [x] Enforcement inside: `Audit.Read` in either scope with tenant-scoped callers filtered to their own tenant; verify and export host-only. Denials audited; verification appends `audit.verified`; export writes JSON Lines to the host's `IImmutableEvidenceStore` (typed `audit.no-evidence-store` failure when none is registered).
- [x] `IAuditStore.ReadLatestAsync` (default interface method over `ReadAllAsync`; SQL Server overrides with a real query) so the page does not read the whole trail.
- [x] Tests (`AuditQueriesTests`): anonymous/unpermitted denied + audited; tenant-scoped listing sees only its tenant; host sees all, newest first, filters exact; verify host-only and self-auditing; export without an evidence store fails typed, with one exports and audits.

## 7.2 `ForgeStack.Admin.Abstractions`

- [x] `IAdminContribution` / `AdminNavItem` moved to `src/Forge.Admin.Abstractions` (namespace `Forge.Admin`); the shell type-forwards both for 0.2.0-preview.1 consumers. UI packages reference the abstractions only — asserted by `Admin_shell_owns_no_module_ui`.

## 7.3 `ForgeStack.Audit.Ui.Blazor`

- [x] Audit page moved out of the shell: GET filter form (actor/action/subject/correlation via query string), verify-chain and export forms, evidence-store status in the verify message. `AuditUiResources` en-GB + ar-SA. `AddForgeAuditUi()` contributes `/admin/audit` under Operations.
- [x] Shell layout no longer lists `/admin/audit`; the a11y suite scans the page from the package.
- [x] `forge ui add|remove audit`; `UiCommand` is table-driven (template order preserved so any add/remove order round-trips byte for byte — `CliTests`). Sample and `--admin` template register it; the dev seed grants the Administrator role the audit permissions too.

## 7.4 `.Api` projections

- [x] `ForgeStack.Identity.Api`, `ForgeStack.Audit.Api`: Minimal-API groups over the contracts, bearer-only, Problem Details from `Result` codes (`ForgeApi` in `ForgeStack.Web`); architecture tests for `.Api` (contract + web plumbing only); projection tests (`ApiProjectionTests`) and a real-token test through OpenIddict validation.
- [x] `forge api add|remove <module>`; `forge new --with-api`; CI removal proof covers UI and API; served-app check asserts the API is a bare 401 anonymously.
- [x] Bearer clients are authorised through roles: an OpenIddict application permission `role:<Role>` becomes a role claim (`IdentityEndpoints.RolePermissionPrefix`); the admin template seeds `dev-client` in Development. `MapIdentityEndpoints()` returns the convention builder so `/connect/token` runs host scoped. Docs: `docs/module-apis.md`.

## 7.5 Starters, CI, docs

- [x] `forge new --template saas|api|modular` (`--admin` stays as an alias for `saas`), `forge templates list`; CI builds every template and serves `saas` and `api`.
- [x] Docs: `docs/audit-ui.md`; roadmap Audit row shipped; `IMPLEMENTATION_PACK.md` project table.
- [ ] Release 0.2.0-preview.2 on explicit instruction only.

## Not in this phase

Stream B distributed capability (phase 8). Tenancy/Settings/Jobs/Localisation UIs (later phases, same pattern).
