# Forge post-v0.1 roadmap

Living document. v0.1 (Phases 0–5, `implementation/phase-*`) is the completed baseline and is not reopened by this roadmap. The 40 accepted ADRs remain normative; ADR 40 (First-Party UI & Starter Applications) is the decision this roadmap implements.

## Product model

Forge is four composable layers:

```text
Forge Platform
    ↓
Headless enterprise capabilities
    ↓
Optional first-party module UIs
    ↓
Starter applications / reference products
```

The administration shell (ADR 37) remains important but is not the end-state product experience. First-party UI is **optional production-quality reference UI**, not sample/demo UI.

## v0.2 — Application Experience + Distributed Capability

Two parallel streams. Neither makes v0.2 microservices-by-default.

### Stream A — Application Experience

> A developer can create a real, usable SaaS application from Forge without first building administration UI for the platform capabilities.

Headline journey:

```bash
forge new Acme --template saas
```

The generated application runs immediately and is a cohesive, usable product, not an empty shell.

#### Module layering (ADR 40)

Each module in scope gains, in this order:

1. **Application contract** — inside the existing capability package. Plain interfaces (`IAuditQueries`, `IJobOperations`, …) with permission, tenant-scope and audit enforcement inside; no mediator/CQRS framework. Mandatory before any UI for that module ships.
2. **`ForgeStack.<Module>.Ui.Blazor`** — consumes only the application contract.
3. **`ForgeStack.<Module>.Api`** — optional Minimal-API projection of the contract, opt-in per module for remote consumers. v0.2 proves the pattern with **Identity** (account/session operations) and **Audit**; other modules add `.Api` when a real consumer appears. Not included by starters by default; `forge new --with-api` opts in at generation time (the `api` template implies it), `forge api add` opts in later.

Architecture tests: capability never references `.Ui.Blazor` or `.Api`; `.Ui.Blazor` references only the contract surface.

**Status (phase 6):** Identity has its application contract (`IAccountOperations`, `ISignInOperations`, `IUserAdministration`, `IRoleAdministration` in `ForgeStack.Identity`) and its `.Ui.Blazor`. The Users and Roles pages moved out of `ForgeStack.Admin.Blazor` into `ForgeStack.Identity.Ui.Blazor`, which contributes them (plus sign-in and account pages) through `IAdminContribution`; the shell now owns only its system pages (dashboard, jobs, settings, localisation). **Phase 7:** Audit repeats the pattern (`IAuditQueries` in `ForgeStack.Auditing`, `ForgeStack.Audit.Ui.Blazor`), and `IAdminContribution` lives in `ForgeStack.Admin.Abstractions` so module UIs never depend on the shell. `.Api` projections shipped in the same phase (`ForgeStack.Identity.Api`, `ForgeStack.Audit.Api`, [docs](module-apis.md)) — bearer-only Minimal-API groups over the same contracts, attached with `forge api add` or `forge new --admin --with-api`.

Production-quality optional Blazor UIs (see [engineering rules](#first-party-ui-engineering-rules)):

| Module | Surfaces |
|---|---|
| Account / Identity | **Shipped (phase 6, `ForgeStack.Identity.Ui.Blazor`, [docs](identity-ui.md)):** sign in/out, profile, password, sessions (sign out everywhere else), user/role/permission administration. *Designed for, not shipped:* MFA, passkeys, recovery, a session list, impersonation, security history |
| Tenancy | tenant search/list, create/edit, enable/disable, tenant settings, tenant users, feature/entitlement management where supported, isolation information where appropriate, tenant audit history |
| Audit | **Shipped (phase 7, `ForgeStack.Audit.Ui.Blazor`, [docs](audit-ui.md)):** timeline with actor/action/subject/correlation filters, integrity verification, export, evidence-store status. *Designed for, not shipped:* entity views, security-event dashboard |
| Jobs | queued/running/scheduled/recurring, retrying/failed/terminal/dead-letter, retry/requeue/cancel where supported, execution history/diagnostics; every intervention audited |
| Settings | platform/tenant/user scope where authorised, type-aware editors, validation, scope/source visibility; secrets never exposed through ordinary settings UI |
| Localisation | supported cultures, resource browser, missing translations, application/tenant overrides, import/export, RTL preview, time-zone/culture settings |
| Notifications | inbox, read/archive state, preferences, delivery history where permitted |
| Files | list/search, metadata, classification, scan/quarantine status, secure download, version/history and retention/legal-hold where supported |
| Feature entitlements (if ADR 07 is sufficiently implemented) | plan/entitlement view, tenant overrides, typed limits, effective-value explanation. Entitlements are not operational runtime flags (ADR 13) |

#### SaaS starter

Reference navigation (labels may evolve):

```text
Acme
Dashboard
My Account        Profile · Password / Security · MFA / Passkeys · Sessions · Preferences
Administration    Users · Roles · Permissions · Tenants · Features / Entitlements · Settings · Localisation
Operations        Audit · Jobs · Notifications · Files · System Health
Developer         API · Modules · Diagnostics
```

#### Starter templates

| Template | Status |
|---|---|
| `saas` | v0.2 priority — the complete experience |
| `api` | v0.2, lighter composition (today's `forge new` output plus the available `.Api` packages) |
| `modular` | v0.2, lighter composition |
| `enterprise` | v0.3 headline deliverable |

#### CLI evolution

The v0.1 CLI (`new`, `modules`, `db`, `doctor`, `upgrade check`, `audit verify`) is kept as is. Planned additions, adapted to the existing System.CommandLine command tree rather than copied verbatim:

```text
forge new <Name> --template saas|enterprise|api|modular
forge new <Name> --template saas --with-api            include the available .Api projections at generation time
forge templates list
forge ui add <module>        identity (shipped, phase 6) · audit (shipped, phase 7) · tenancy · jobs · settings · localisation · notifications · files
forge ui remove <module>
forge api add <module>       adds the optional .Api projection where one exists
```

`forge new --admin` (v0.1.x) is the seed of `--template saas` and is retained as an alias until `saas` ships, then deprecated per ADR 22.

### Stream B — Distributed Capability

> A Forge application can move selected work out of process without changing the fundamental programming model.

- RabbitMQ provider; Azure Service Bus provider (ADR 32)
- inbox/deduplication; broker retry/dead-letter administration
- SignalR realtime implementation; multi-instance realtime where appropriate (ADR 28)
- process-manager/saga foundations (ADR 33)
- distributed coordination hardening where required
- the `PONYTAIL-DEBT.md` multi-instance items (idempotency store, outbox signals, settings invalidation)

## v0.3 — Enterprise Application Experience

> Forge provides a compelling starter for enterprise line-of-business and complex B2B applications.

```bash
forge new Acme --template enterprise
```

Organisation hierarchy and delegated administration (ADR 34), organisation-scoped permissions, richer privacy/GDPR and subject-rights workflows (ADR 09), reporting/export UI and bulk import with dry-run/error review (ADR 29), enterprise search (ADR 27), operational dashboards, richer entitlement management, access reviews, tenant/application branding and controlled white-labelling (ADR 37), richer job/process operations.

Reuses the v0.2 module UIs; no second monolithic UI implementation.

## v0.4 — Enterprise Integration

> Make Forge an excellent foundation for applications that integrate with multiple enterprise systems.

Implements ADR 39 with operational UI: external identities, source provenance and authority, synchronisation state, checkpoints, conflict detection/resolution, reconciliation, mapping, webhook administration, inbound/outbound sync, connector SDK/conventions.

```text
Integrations    Systems · Connections · Sync Status · Failed Records · Conflicts · Reconciliation · Webhooks · Activity
```

No universal Forge business-domain canonical model.

## v0.5 — Ecosystem

> Enable a trustworthy Forge module ecosystem after the platform has real adoption and useful modules exist.

Implements ADR 36: module catalogue, verified publishers and criteria, compatibility certification, security/provenance checks, discovery, third-party module templates, module metadata UI, marketplace foundations.

```bash
forge modules search stripe
forge add Acme.Forge.Stripe
```

Trust information exposes publisher, verification status, supported Forge range, available UI, tenancy support, accessibility validation, licence and security/provenance status. NuGet remains the distribution foundation; no proprietary package manager.

## v1.0 — Stability Contract

> Forge is stable enough that organisations can depend on its public contracts for years.

Not a feature-completion exercise. Focus: public API, module contract, manifest, tenancy, permission model, event/job contract, persistence convention and CLI stability; semantic versioning; upgrade tooling and migration guarantees; support/lifecycle policy (`docs/lifecycle.md`); performance, security, accessibility and dependency/licence reviews; production-adopter feedback. No large new platform concepts solely to reach 1.0.

## First-party UI engineering rules

Enforceable subset is in `AGENTS.md`.

- **Capability independence** — a capability MUST NOT depend on its first-party UI package (architecture test).
- **API parity** — first-party UI consumes the module's application contract, never stores/contexts directly; an `.Api` package projects that same contract, so every consumer passes the same authorisation, tenant and audit checks.
- **Package restraint** — a new package must create a genuine boundary. `.Api` is opt-in per module; starters stay as small as the chosen template allows.
- **Replaceability** — an application can replace one Forge UI while keeping the underlying module.
- **Design system** — all first-party UI uses the Forge design system and documented extension surfaces (ADR 37).
- **Accessibility** — ADR 19 and the WCAG 2.2 AA release gate apply.
- **Localisation** — no hard-coded English-only experiences; localisation, globalisation and RTL throughout (ADR 12).
- **Security** — navigation visibility is never authorisation; privileged actions are server-authorised and audited; tenant and impersonation context stay visibly clear.
- **Quality** — first-party UI is production-quality executable documentation for how Forge applications should be built. It is not labelled "sample UI".

## Starter application philosophy

Starters are optional compositions of Forge capabilities and UIs. They must not become a second proprietary framework layer. A developer can start from a full starter, remove unwanted module UIs, replace selected UIs, consume only APIs, use a custom customer-facing frontend, or keep the Forge admin UI and build the product in another stack.

Blazor is the only first-party UI stack required by this roadmap. React does not begin until the Blazor UI contracts and reference experiences are mature; the architecture must not block it.

## Traceability

| ADR | Capability | Roadmap implementation | Primary proof |
|---:|---|---|---|
| 40 | First-Party UI & Starter Applications | v0.2 Application Experience; v0.3 Enterprise Starter | Capability runs without UI; optional UI package installs/removes independently; SaaS starter E2E; WCAG/localisation/tenant/permission tests |
