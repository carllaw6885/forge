# Phase 6 - Identity application experience (v0.2, Stream A, slice 1)

**Window:** first v0.2 phase; starts 1 September 2026

**Exit goal:** A `forge new --admin` application signs in, manages its account and administers users/roles through first-party Identity UI, with the capability provably usable without that UI. This phase proves the ADR 40 layering once; phases 7+ repeat it.

ADRs: 05, 06, 12, 19, 30, 37, 40. Roadmap: `docs/POST_V0.1_ROADMAP.md` § Module layering, § Account / Identity surfaces.

Rule for every item: the capability never references the UI; the UI consumes only the application contract; navigation visibility is never authorisation.

## 6.1 Identity application contract (in `Forge.Identity`)

- [ ] Define plain interfaces, no mediator: `IAccountOperations` (sign in/out, change password, profile, active sessions), `IUserAdministration` (list/search/create/disable users, assign roles), `IRoleAdministration` (roles ↔ permissions).
- [ ] Enforce permission, tenant scope and audit *inside* the implementations; callers pass no authorisation state. Denied calls produce an audit event.
- [ ] Register the contract from `IdentityModule` so it is available to any consumer (shell, `.Ui.Blazor`, custom apps) with no extra setup.
- [ ] Tests: permission denied → typed failure + audit; tenant-scoped listing never crosses tenants; host scope explicit.
- [ ] Remove direct `IdentityDbContext` / `UserManager` use from `Forge.Admin.Blazor` `Users.razor` / `Roles.razor` (today they query the context directly, which ADR 40 forbids for UI).

## 6.2 `ForgeStack.Identity.Ui.Blazor`

- [ ] New RCL `src/Forge.Identity.Ui.Blazor`, PackageId `ForgeStack.Identity.Ui.Blazor`, `StaticWebAssetBasePath` pinned like the shell.
- [ ] Sign in / sign out pages (replace the `ponytail:` login form in the `--admin` template); lockout and failure messaging; returnUrl same-site only.
- [ ] Profile, change password, active sessions (sign out others).
- [ ] Users, roles, permissions administration pages moved here from the shell, consuming 6.1 only.
- [ ] Contributes to the shell via `IAdminContribution`; works headless-shell-free (routable in any Blazor Web App host). Scope decision recorded in the slice spec: MFA / passkeys / recovery are **designed for, not shipped** in 6.x unless the spec says otherwise.
- [ ] Design system tokens, localised strings (no hard-coded English), RTL, WCAG 2.2 AA via the existing axe/Playwright gate for every page.

## 6.3 Architecture and isolation proof

- [ ] Architecture test: `Forge.Identity` has no reference to `Forge.Identity.Ui.Blazor` or `Forge.Admin.Blazor`; `Forge.Identity.Ui.Blazor` references no `DbContext`, `UserManager`, `SignInManager` or store types — only the 6.1 contract and `Forge.Admin.Blazor` contribution contracts.
- [ ] Removal test: `forge new --admin` output builds and serves `/admin` with the Identity UI package reference deleted (capability still runs; shell shows no Identity pages).
- [ ] Tenant-isolation test for user listing/administration through the UI's contract path.

## 6.4 Template and CLI

- [ ] `--admin` template references `ForgeStack.Identity.Ui.Blazor`; delete `/auth/login` form and `LoginRequest` from `Program.cs`; `MapForgeAdminShell` still `RequireAuthorization`.
- [ ] Dev seed stays; documented in generated README.
- [ ] `forge ui add identity` / `forge ui remove identity` (first `forge ui` verbs; adds/removes the package reference + contribution registration, idempotent, ordinary source output per ADR 22).
- [ ] E2E in CI: generated app, packed packages, browser sign-in → `/admin` → users page; anonymous `/admin` → sign-in page.

## 6.5 Docs and release

- [ ] `docs/POST_V0.1_ROADMAP.md`: mark Identity contract/UI rows done; note what moved out of the shell.
- [ ] `PONYTAIL-DEBT.md`: close the 0.1.7 login-form marker.
- [ ] Package docs: install/remove instructions for `ForgeStack.Identity.Ui.Blazor`; upgrade note for apps that used shell Users/Roles pages.
- [ ] Release 0.2.0-preview.1 on explicit instruction only.

## Not in this phase

Audit contract/UI, `.Api` projections, `--template saas`, `--with-api` (phase 7). Stream B distributed capability (phase 8). Tenancy/Settings/Jobs/Localisation UIs (later phases, same pattern).
