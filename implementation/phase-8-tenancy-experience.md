# Phase 8 - Tenancy experience (v0.2, Stream A, slice 3)

**Window:** third v0.2 phase; starts 2 September 2026

**Exit goal:** Tenants become first-class records, not just opaque resolved ids: a tenant directory capability (store + enable/disable enforced at resolution), the ADR 40 layering repeated for Tenancy (contract → `.Ui.Blazor`), and `forge ui add tenancy`.

ADRs: 05, 06, 08, 12, 37, 40. Roadmap: `docs/POST_V0.1_ROADMAP.md` § Tenancy surfaces.

## 8.1 Tenant directory capability

- [x] `Tenant` record (id, display name, enabled, created) and `ITenantDirectory` store in `Forge.Tenancy` (get/list/save; in-memory default). SQL Server implementation + module-owned migration in `Forge.Persistence.SqlServer` (`tenancy` schema), same shape as the settings store.
- [x] Resolution enforcement (ADR 05): when a directory is registered, a resolved tenant that is unknown or disabled is rejected by `UseForgeTenancy` with Problem Details — deny-by-default extends to tenant state. No directory registered = current behaviour (opaque ids) unchanged.

## 8.2 Tenancy application contract

- [x] `ITenantAdministration` in `Forge.Tenancy` (list/search, create, rename, enable/disable); `TenancyErrors`. Implementation and `TenancyPermissions` (`Tenancy.Read|Manage`) in `Forge.Security`, registered by `AddForgePermissions()`.
- [x] Enforcement inside: host scope only — tenant administration is cross-tenant by nature; a tenant-scoped caller is denied and the denial audited. Every mutation audited (`tenant.created`, `tenant.renamed`, `tenant.enabled`, `tenant.disabled`).
- [x] Tests (`TenantAdministrationTests`): anonymous/unpermitted/tenant-scoped denied + audited; host CRUD round-trips; disable takes effect at resolution (integration through `UseForgeTenancy`); mutations audited.

## 8.3 `ForgeStack.Tenancy.Ui.Blazor`

- [x] Tenants page at `/admin/tenants` over the contract only: list with search, create form, rename, enable/disable. `TenancyUiResources` en-GB + ar-SA; `AddForgeTenancyUi()` contributes under Administration. A11y suite scans the page.
- [x] Tenant detail links to the audit page pre-filtered (`/admin/audit?subject=<tenantId>`) — no second audit view.
- [x] `forge ui add|remove tenancy`; sample and `saas` template register it; dev seed grants Administrator the tenancy permissions.

## 8.4 Docs

- [x] `docs/tenancy-ui.md`; roadmap Tenancy row shipped; `IMPLEMENTATION_PACK.md` project table.

## Not in this phase

Feature/entitlement management (ADR 07 maturity first), tenant audit history as its own view, `.Api` projection (`forge api add tenancy` when a real consumer appears) — see the roadmap Tenancy row. Stream B distributed capability moves to a later phase.
