# Phase 6 - Identity application experience (v0.2, Stream A, slice 1)

**Window:** first v0.2 phase; starts 1 September 2026

**Exit goal:** A `forge new --admin` application signs in, manages its account and administers users/roles through first-party Identity UI, with the capability provably usable without that UI. This phase proves the ADR 40 layering once; phases 7+ repeat it.

ADRs: 05, 06, 12, 19, 30, 37, 40. Roadmap: `docs/POST_V0.1_ROADMAP.md` § Module layering, § Account / Identity surfaces.

Rule for every item: the capability never references the UI; the UI consumes only the application contract; navigation visibility is never authorisation.

## 6.1 Identity application contract (in `Forge.Identity`)

- [x] Define plain interfaces, no mediator: `IAccountOperations` (me, change password), `IUserAdministration` (list/create users, assign roles), `IRoleAdministration` (roles ↔ permissions). Sign in/out, sessions, search and disable move to 6.2 with the UI that needs them.
- [x] Enforce permission, tenant scope and audit *inside* the implementations; callers pass no authorisation state. Denied calls produce an audit event. (Identity data is host owned in v0.1: tenant scope is denied, host scope required.)
- [x] Register the contract from `IdentityModule` so it is available to any consumer (shell, `.Ui.Blazor`, custom apps) with no extra setup.
- [x] Tests: permission denied → typed failure + audit; tenant-scoped listing never crosses tenants; host scope explicit.
- [x] Remove direct `IdentityDbContext` / `UserManager` use from `Forge.Admin.Blazor` `Users.razor` / `Roles.razor` (today they query the context directly, which ADR 40 forbids for UI).

Not done in 6.1: `PermissionCatalog` population (`IdentityPermissions.All` is declared but the catalog has no module-contribution seam yet).

## 6.2 `ForgeStack.Identity.Ui.Blazor`

- [x] New RCL `src/Forge.Identity.Ui.Blazor`, PackageId `ForgeStack.Identity.Ui.Blazor`, `StaticWebAssetBasePath` pinned like the shell. Host wiring is one call: `AddForgeIdentityUi()` (contribution + cookie `LoginPath`).
- [x] Sign in / sign out pages (replace the `ponytail:` login form in the `--admin` template); lockout and failure messaging; returnUrl same-site only. Contract grew `ISignInOperations` (password sign-in with lockout, sign out, sign out everywhere else) and `IdentityModule` now registers `SignInManager` + `ISecurityStampValidator` itself (`TryAdd`; hosts keep `AddAuthentication().AddIdentityCookies()`). `[AllowAnonymous]` on the sign-in/signed-out pages carves them out of the shell's `RequireAuthorization()` — proven live by the a11y suite.
- [x] Profile, change password, sessions. **Scope decision:** Identity keeps no session table, so "sessions" ships as *sign out everywhere else* (security-stamp rotation, current session refreshed) — a session list is designed for, not shipped.
- [x] Users, roles, permissions administration pages moved here from the shell, consuming 6.1 only.
- [x] Contributes to the shell via `IAdminContribution` (first implementor of the seam); pages are plain routable components usable in any Blazor Web App host. The package references `Forge.Admin.Blazor` for the contribution contract only — moving `IAdminContribution` into a smaller abstractions package is deferred until a second module UI (phase 7) makes it a pattern. **Scope decision:** MFA / passkeys / recovery are **designed for, not shipped** in 6.x.
- [x] Localised strings (`IdentityUiResources`, en-GB neutral + ar-SA), RTL, WCAG 2.2 AA: the axe gate now signs in through the real page and scans every identity route; anonymous → sign-in redirect and the failed-sign-in alert are asserted. Design tokens are inherited from the shell stylesheet (the package ships no CSS).

## 6.3 Architecture and isolation proof

- [x] Architecture test: `Forge.Identity` has no reference to `Forge.Identity.Ui.Blazor` or `Forge.Admin.Blazor`; `Forge.Identity.Ui.Blazor` references no `DbContext`, `UserManager`, `SignInManager` or store types — only the 6.1 contract and `Forge.Admin.Blazor` contribution contracts.
- [x] Removal proof (structural, `Admin_shell_owns_no_module_ui`): the shell has no reference to any `.Ui.` package and its layout carries no identity/account routes, so deleting the package + `AddForgeIdentityUi()` removes exactly those pages. The build-and-serve E2E of the generated app lands with 6.4 (needs packed packages).
- [x] Tenant-isolation through the UI's contract path: the pages call `IUserAdministration`/`IRoleAdministration` and nothing else (architecture test), and `Tenant_scope_is_denied_for_host_owned_identity_data` covers that path.

## 6.4 Template and CLI

- [x] `--admin` template references `ForgeStack.Identity.Ui.Blazor`; `/auth/login` form and `LoginRequest` deleted from `Program.cs` (sample host too); `MapForgeAdminShell` still `RequireAuthorization`.
- [x] Dev seed stays; documented in generated README (template `README.md`).
- [x] `forge ui add identity` / `forge ui remove identity` (`Commands/UiCommand.cs`: one PackageReference + one registration line, idempotent, refuses hosts without the shell; remove ∘ add is byte-identical to the template — `CliTests`).
- [x] E2E in CI (`pr.yml`): packs every `src/*` (glob, as `release.yml`), generates `--admin`, builds with the identity UI removed and re-added, then runs the app against SQL Server: anonymous `/admin` → `/account/sign-in?ReturnUrl=`, the sign-in form renders. The browser sign-in → `/admin` → users journey is gated by the accessibility suite on the same package rather than a second Playwright harness. `IdentityContractTests` now run in the SQL job too.

## 6.5 Docs and release

- [x] `docs/POST_V0.1_ROADMAP.md`: Identity row marked shipped, status note on the contract and what moved out of the shell; `IMPLEMENTATION_PACK.md` project table gained the package.
- [x] `PONYTAIL-DEBT.md`: close the 0.1.7 login-form marker.
- [x] Package docs: `docs/identity-ui.md` (install/remove, routes and permissions, scope decisions, upgrading from 0.1.x).
- [ ] Release 0.2.0-preview.1 on explicit instruction only. Everything above is complete; waiting for the maintainer's `release 0.2.0-preview.1`.

## Not in this phase

Audit contract/UI, `.Api` projections, `--template saas`, `--with-api` (phase 7). Stream B distributed capability (phase 8). Tenancy/Settings/Jobs/Localisation UIs (later phases, same pattern).
