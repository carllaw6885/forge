# Forge v0.1 Implementation Pack

**Implementation-ready engineering plan aligned to Foundation Baseline v0.2**  
**Date:** 31 August 2026

> Source of truth: Forge Foundation Pack v0.2. The 39 accepted ADRs are normative. This implementation pack refines mechanics only and does not supersede them.

## 1. Executive implementation position

Forge v0.1 is a proof of the product thesis, not an attempt to implement all 39 ADR capability areas. The release must prove that an ordinary .NET developer can create, inspect, run and extend a secure multi-tenant modular application without paid feature gates or hidden framework magic.

**v0.1 success statement:**

> A developer installs the Forge CLI, runs `forge new Acme`, starts the Aspire topology, and gets a working multi-tenant .NET application with authentication, permissions, localisation, structured/tamper-evident and immutable-capable audit, durable Quartz jobs, observability, storage/notification foundations and an accessible Blazor admin shell - all as ordinary open-source .NET code.

## 2. Scope contract

### In v0.1
- Explicit modular kernel and module validation
- SQL Server reference persistence with module-owned DbContexts/migrations
- Shared-database tenancy path and host/tenant separation
- ASP.NET Core Identity + OpenIddict, roles and first-class permissions
- Structured audit, tamper verification and immutable storage capability
- In-process integration events + transactional outbox
- Quartz reference durable jobs
- REST/Minimal API conventions, Problem Details and OpenAPI
- OpenTelemetry, health/readiness and Aspire local topology
- Typed settings, secrets abstraction, operational flags
- Localisation/globalisation including RTL acceptance path
- Privacy classification primitives
- Provider-neutral storage with quarantine/scan seams
- Notification intents + constrained templates
- Optional Redis reference adapter
- Blazor admin shell and WCAG 2.2 AA gates
- CLI and release/supply-chain evidence

### Explicitly post-v0.1
- RabbitMQ and Azure Service Bus production providers beyond minimal seams
- Durable workflow/process-manager product surface
- Enterprise search providers and search workbench
- Realtime scale-out/presence
- Reporting/import workbench
- Organisation-scope administration
- Public marketplace/verification service
- Full external sync/reconciliation workbench
- Full SSO/SCIM/SAML administration experiences

## 3. Target solution and package structure

| Path | Responsibility | Boundary rule |
|---|---|---|
| `src/Forge.Core` | Core contracts and primitives | Module metadata, module graph, clock, correlation, common result/error primitives. No ASP.NET, EF, or provider-specific dependencies. |
| `src/Forge.Modularity` | Explicit module composition | AddForge, module registration, dependency validation, module graph inspection, minimal lifecycle. |
| `src/Forge.Tenancy` | Tenancy abstractions | ICurrentTenant, tenant scopes, tenant resolution contracts, tenant-safe helpers. |
| `src/Forge.Security` | Permission/security primitives | Permission definitions, authorization integration, privileged action context, security event taxonomy. |
| `src/Forge.Auditing` | Structured audit contracts | Audit event model, append-only store abstraction, redaction policy, integrity/immutability capabilities. |
| `src/Forge.Settings` | Typed settings and flags | Settings definitions/scopes/cache invalidation, operational flags; secrets remain abstractions only. |
| `src/Forge.Localization` | Localisation/globalisation | Culture/time-zone/currency resolution conventions and localizable resource support. |
| `src/Forge.Events` | Domain/integration event abstractions | In-process integration bus, event envelope, outbox contracts and duplicate-tolerant dispatch. |
| `src/Forge.Jobs` | Job abstractions | Durable/recurring job contracts, tenant/correlation context, idempotency and job state model. |
| `src/Forge.Jobs.Quartz` | Quartz reference provider | Durable Quartz-backed jobs, retries, scheduling, dead-letter/terminal-failure projection. |
| `src/Forge.Storage` | Storage abstractions | Provider-neutral blobs, metadata, classification, quarantine state and integrity hashes. |
| `src/Forge.Notifications` | Notification intents and delivery state | Channel-neutral notification definitions, preferences/policy, durable delivery state. |
| `src/Forge.Templates` | Safe template engine | Constrained template model, allow-listed variables, localisation, versioning and validation. |
| `src/Forge.Web` | ASP.NET Core integration | Problem Details, OpenAPI conventions, tenant-safe endpoint helpers, rate-limit/idempotency hooks. |
| `src/Forge.Observability` | OpenTelemetry/service defaults | Activity/Meter conventions, correlation propagation, health/readiness. |
| `src/Forge.Persistence.SqlServer` | SQL Server reference persistence | EF Core conventions, tenant filters, migrations infrastructure, outbox SQL persistence. |
| `src/Forge.Identity` | Identity/OpenIddict module | ASP.NET Core Identity, OpenIddict, role-permission mapping, sessions, basic MFA/passkey-ready seams. |
| `src/Forge.Admin.Blazor` | Reference admin shell | Blazor Web App admin shell, nav extension points, tenant/impersonation banners, design tokens. |
| `src/Forge.Cli` | Developer CLI | new, modules list/graph/validate, db status/migrate, doctor, upgrade check --dry-run. |
| `src/Forge.DbMigrator` | Independent migration runner | Host/shared tenant database migration execution; no web-startup auto-migrate. |
| `src/Forge.AppHost` | Aspire reference topology | AppHost orchestration for SQL Server, app, migrator, telemetry; Redis optional in v0.1. |
| `samples/Forge.ReferenceSaaS` | Reference SaaS application | Three+ explicitly composed modules demonstrating tenant-safe CRUD, audit, notifications, localisation and jobs. |

### Package rules
- First-party packages share a coordinated Forge release train.
- A package exists only for a real dependency, deployment or ownership boundary.
- Core packages must not pull UI, EF provider, cloud or commercial component dependencies transitively.
- SQL Server and Quartz are reference providers, not Core assumptions.
- Domain entities never cross module boundaries; public contracts/DTOs do.

## 4. Reference repository layout

```text
forge/
  src/
    Forge.Core/
    Forge.Modularity/
    Forge.Tenancy/
    Forge.Security/
    Forge.Auditing/
    Forge.Events/
    Forge.Jobs/
    Forge.Jobs.Quartz/
    Forge.Persistence.SqlServer/
    Forge.Identity/
    Forge.Settings/
    Forge.Localization/
    Forge.Storage/
    Forge.Notifications/
    Forge.Templates/
    Forge.Web/
    Forge.Observability/
    Forge.Admin.Blazor/
    Forge.Cli/
    Forge.DbMigrator/
    Forge.AppHost/
  modules/
    ReferenceCatalog/
  samples/
    Forge.ReferenceSaaS/
  tests/
    Forge.ArchitectureTests/
    Forge.TenancyTests/
    Forge.SecurityTests/
    Forge.ConformanceTests/
    Forge.E2E/
  architecture/decisions/
  specs/
  eng/
```

## 5. Implementation phases

### Phase 0 - Repository contract
**Window:** Weeks 1-2  
**Exit goal:** Make the architecture executable before product code expands.

#### 0.1 Repository baseline
- [ ] Create solution, Directory.Build.props/targets, package version policy, analyzers, formatting and deterministic build settings.
- [ ] Import all 39 ADRs as individual accepted ADR files with stable identifiers and front matter.
- [ ] Define module manifest schema and repository conventions.
- [ ] Create AGENTS.md rules that reference deterministic checks rather than vendor-specific prompts.

#### 0.2 Architecture enforcement
- [ ] Add architecture tests preventing cross-module DbContext access, domain-entity sharing and forbidden UI/infrastructure references.
- [ ] Add module dependency graph validator and cycle detection.
- [ ] Add first tenant-isolation invariant test harness.
- [ ] Add dependency licence/security policy and SBOM generation.

#### 0.3 CI baseline
- [ ] Build, format, unit tests, architecture tests, secret scan, vulnerability scan, licence scan and SBOM.
- [ ] Add PR quality gate summary and artefact retention.
- [ ] Create security disclosure and contribution templates.

#### 0.4 CLI skeleton
- [ ] Create Forge.Cli command host with deterministic output.
- [ ] Implement forge modules list/graph/validate against manifests.
- [ ] Implement forge doctor skeleton and --dry-run plumbing.

### Phase 1 - Executable modular kernel
**Window:** Weeks 3-6  
**Exit goal:** Prove explicit modular composition, persistence ownership and a working vertical slice.

#### 1.1 Module kernel
- [ ] Implement AddForge and explicit module registration.
- [ ] Implement minimal ConfigureServices/ConfigureApplication lifecycle.
- [ ] Validate declared dependencies; no assembly-wide auto-discovery.
- [ ] Expose inspectable module graph to CLI and diagnostics.

#### 1.2 Persistence ownership
- [ ] Create module-owned DbContext pattern with SQL Server reference provider.
- [ ] Create module-owned migrations and independent migration metadata.
- [ ] Add no-cross-module-foreign-key architecture tests.
- [ ] Add provider test harness using real SQL Server container.

#### 1.3 Communication primitives
- [ ] Implement synchronous public contract guidance and sample.
- [ ] Implement internal domain event collector/dispatcher.
- [ ] Define versioned integration event envelope including tenant, correlation, causation, event id and schema version.
- [ ] Implement in-process integration event bus for v0.1.

#### 1.4 First vertical slice
- [ ] Create Reference Catalog module as deliberately simple tenant-owned CRUD capability.
- [ ] Expose Minimal API DTOs, validation, Problem Details and OpenAPI.
- [ ] Persist changes in module DbContext and emit domain/integration events.
- [ ] Demonstrate localisation resources and structured audit contribution.

### Phase 2 - Security and tenancy
**Window:** Weeks 7-11  
**Exit goal:** Make tenant isolation, identity, permissions and immutable audit demonstrably safe.

#### 2.1 Tenancy core
- [ ] Implement ICurrentTenant and explicit host/tenant scope changes.
- [ ] Implement trusted tenant resolution pipeline and deny-by-default missing/invalid tenant behaviour.
- [ ] Implement EF tenant query filters for opted-in entities.
- [ ] Add shared-database tenant isolation negative tests across API, cache key helper, events and jobs.

#### 2.2 Identity and permissions
- [ ] Implement ASP.NET Core Identity + OpenIddict reference module.
- [ ] Implement first-class permission definitions and role aggregation.
- [ ] Implement permission policy provider / authorization handlers.
- [ ] Implement visible, reasoned, audited impersonation context.
- [ ] Create seams and data model for future SSO/SCIM/SAML without requiring full v0.1 delivery.

#### 2.3 Audit evidence
- [ ] Implement structured audit event model distinct from ILogger and entity history.
- [ ] Implement append-only audit store abstraction with no update/delete methods.
- [ ] Implement hash-chained tamper-evident records and forge audit verify.
- [ ] Implement immutable evidence provider contract and local reference proving write-once semantics; production cloud WORM adapters may follow.
- [ ] Implement redaction/exclusion policy and audit retention/export events.

#### 2.4 Security baseline
- [ ] Ship hardened ASP.NET defaults: HSTS production, secure cookies, antiforgery where relevant, safe CORS, request limits, rate-limit hooks and CSP-ready shell.
- [ ] Add unsafe-production-configuration validators.
- [ ] Add threat model template and security event taxonomy.
- [ ] Gate release on authorization and tenant-boundary regression suites.

### Phase 3 - Reliable application services
**Window:** Weeks 12-16  
**Exit goal:** Prove durable work, event reliability, APIs and observability.

#### 3.1 Transactional outbox
- [ ] Persist entity changes and outbox entries atomically in the owning module transaction.
- [ ] Implement outbox dispatcher with duplicate-tolerant delivery.
- [ ] Preserve tenant, correlation and causation context.
- [ ] Add retry/backoff and operational lag metrics.

#### 3.2 Quartz jobs
- [ ] Implement provider-neutral durable job contracts.
- [ ] Implement Quartz reference provider with persistent SQL-backed store.
- [ ] Capture/restore tenant and correlation context automatically.
- [ ] Implement idempotency key support, retry policy and observable terminal failure/dead-letter projection.
- [ ] Reject in-memory job provider under production validation.

#### 3.3 API platform
- [ ] Standardise Minimal API conventions, DTO boundaries, Problem Details and OpenAPI.
- [ ] Implement idempotency support for opted-in commands.
- [ ] Implement tenant-safe request handling and reference rate-limit policy.
- [ ] Add OpenAPI compatibility snapshot/diff gate.

#### 3.4 Observability and health
- [ ] Implement OpenTelemetry traces/metrics using standard .NET primitives.
- [ ] Propagate context across HTTP, EF, outbox and jobs.
- [ ] Implement liveness/readiness distinction and dependency health registration.
- [ ] Prove seeded sensitive values do not appear in logs/traces.

### Phase 4 - Foundational enterprise services
**Window:** Weeks 17-22  
**Exit goal:** Add enterprise primitives required by the v0.1 contract without pulling post-v0.1 scope forward.

#### 4.1 Settings/secrets/flags
- [ ] Implement typed setting definitions, scope precedence and validation.
- [ ] Implement tenant-safe caching/invalidation for settings.
- [ ] Implement secret-store abstraction only; no secrets in ordinary settings.
- [ ] Implement operational flags distinct from entitlements.

#### 4.2 Localisation/globalisation
- [ ] Implement application/tenant/user culture resolution.
- [ ] Implement time-zone resolution and deterministic display conversion.
- [ ] Implement module-owned resources, fallbacks and tenant/application overrides.
- [ ] Add en-GB plus one RTL acceptance culture and CI checks for missing first-party strings.

#### 4.3 Privacy/classification
- [ ] Implement data classification primitives, retention classes and legal-hold flag model.
- [ ] Implement privacy contributor contract and acceptance demonstration.
- [ ] Ensure audit/storage/template paths respect classification metadata.
- [ ] Do not build the full GDPR workbench in v0.1.

#### 4.4 Storage pipeline
- [ ] Implement provider-neutral storage and local reference provider.
- [ ] Validate size/type, quarantine before trust, record SHA-256 hash and classification metadata.
- [ ] Add pluggable malware-scan contract and deterministic fake/reference scanner for acceptance.
- [ ] Implement authorized private access path; no permanent public URLs.

#### 4.5 Notifications/templates
- [ ] Implement notification intents, preferences, policy override and durable delivery state.
- [ ] Implement constrained template rendering with allow-listed variables, localisation and sanitisation.
- [ ] Demonstrate one in-app/email-style provider adapter without coupling Core to vendor delivery.
- [ ] Audit security-critical delivery and template lifecycle changes.

#### 4.6 Redis adapter
- [ ] Implement optional Redis distributed cache provider and tenant-safe key conventions.
- [ ] Keep cache failure degradable where authoritative source exists.
- [ ] Do not make Redis mandatory for single-instance v0.1 reference execution.

### Phase 5 - Reference product surface
**Window:** Weeks 23-28  
**Exit goal:** Deliver an installable, inspectable, accessible open-source reference product experience.

#### 5.1 Blazor admin shell
- [ ] Create Blazor Web App shell with explicit module contribution contracts.
- [ ] Implement design tokens, light/dark/system modes and RTL layout support.
- [ ] Make tenant and impersonation context visually obvious.
- [ ] Implement admin surfaces for users/roles/permissions, audit, jobs, settings and localisation essentials.

#### 5.2 Accessibility
- [ ] Integrate axe/Playwright automated WCAG checks for acceptance journeys.
- [ ] Add keyboard/focus/semantic regression tests.
- [ ] Document manual assistive-technology release checklist.
- [ ] Block release on known first-party WCAG 2.2 AA failures.

#### 5.3 Aspire and packaging
- [ ] Create Aspire AppHost for app, SQL Server, migrator and telemetry dependencies.
- [ ] Provide ServiceDefaults and local developer diagnostics.
- [ ] Create production OCI Dockerfiles/images running non-root where supported.
- [ ] Ensure production runtime does not require AppHost.

#### 5.4 CLI v0.1 completion
- [ ] Implement forge new against the reference template.
- [ ] Implement forge db status/migrate and forge doctor core checks.
- [ ] Implement forge upgrade check --dry-run.
- [ ] Ensure generated output is ordinary source and commands are idempotent.

#### 5.5 Release engineering
- [ ] Validate fresh install and supported upgrade migration.
- [ ] Create signed NuGet packages, SBOM and provenance artefacts.
- [ ] Publish lifecycle/upgrade constraints and rollback notes.
- [ ] Run full conformance suite and manual accessibility/security sign-off.

## 6. First vertical slice

Use a small `ReferenceCatalog` module to prove the platform without embedding business-specific complexity.

**Slice flow:**
1. Authenticated tenant user creates a tenant-owned Catalog Item through a Minimal API.
2. Permission policy authorises `Catalog.Items.Create`.
3. Module-owned `CatalogDbContext` persists the entity.
4. A domain event is raised internally.
5. A versioned `catalog.item.created` integration event is written to the module outbox in the same transaction.
6. Structured audit evidence is appended with tenant, actor and correlation metadata.
7. Outbox dispatcher publishes in-process and a notification job is enqueued through Quartz.
8. Admin UI shows the record, audit evidence and job outcome under the correct tenant.
9. en-GB and RTL culture rendering are exercised.
10. Negative tests prove another tenant cannot query, cache, audit-query or operate on the item.

This slice is deliberately chosen to exercise architecture rather than domain sophistication.

## 7. v0.1 acceptance gates

| Gate | Required evidence |
|---|---|
| Architecture | No cross-module DbContext/domain entity references; explicit module graph valid; no cycles. |
| Tenancy | Negative isolation tests pass; host scope explicit; tenant context preserved across events/jobs. |
| Identity/Authorization | Permissions independent of roles; privileged impersonation visible and audited. |
| Audit | Structured audit distinct from logs; sensitive values redacted; tamper verification passes; immutable mode demonstrated. |
| Reliability | Outbox atomicity proven; duplicate delivery tolerated; Quartz durable/idempotent job passes retry/failure scenario. |
| API | DTOs, Problem Details, OpenAPI, idempotency and compatibility checks pass. |
| Observability | HTTP -> EF -> outbox -> job trace continuity; no seeded sensitive data leaks. |
| Localisation | en-GB + RTL culture; deterministic timezone; no hard-coded first-party acceptance strings. |
| Storage | Validation/quarantine/hash/classification/private access demonstrated. |
| Notification/Templates | Constrained rendering, preference/policy behaviour and durable delivery state demonstrated. |
| Accessibility | Automated WCAG 2.2 AA acceptance journey + manual release checklist. |
| CLI | new/list/graph/validate/db/doctor/upgrade-check dry-run work deterministically. |
| Deployment | Aspire local topology works; OCI image works without Aspire; migrations execute separately. |
| Supply chain | Vulnerability/licence/secret scans, SBOM, signing/provenance pass. |

## 8. Test and conformance strategy

### Required suites
- `Forge.ArchitectureTests`: module boundaries, dependency direction, no cross-module DbContext/domain references, no forbidden provider/UI references.
- `Forge.TenancyTests`: negative cross-tenant API/data/cache/event/job scenarios.
- `Forge.SecurityTests`: authz, impersonation, unsafe config rejection, secure defaults and sensitive-data leakage.
- `Forge.Persistence.SqlServer.Tests`: real SQL Server migrations, query filters, concurrency and outbox atomicity.
- `Forge.Jobs.Quartz.Tests`: durable persistence, retries, duplicate/idempotent handling and terminal failure.
- `Forge.ContractTests`: OpenAPI and integration-event compatibility snapshots.
- `Forge.AccessibilityTests`: Playwright/axe plus keyboard/focus smoke journeys.
- `Forge.E2E`: create tenant, sign in, assign permission, create catalog item, change culture, inspect audit/job state.

### CI tiers
**PR fast gate:** restore, build, format, analyzers, unit/module tests, architecture, secret/vulnerability/licence scan, SBOM.

**Affected integration gate:** SQL Server, tenant isolation, Quartz, API/event compatibility, accessibility where UI touched.

**Release gate:** full conformance, fresh install, supported upgrade migration, OCI smoke, manual accessibility sign-off, security review, signed packages/provenance.

## 9. Migration and data strategy
- Each module owns migrations.
- `Forge.DbMigrator` runs independently from web processes.
- Shared tenant DB is the v0.1 acceptance topology; database-per-tenant seams are preserved and connection resolution contracts are implemented, but exhaustive operational tooling may follow.
- Fresh install and supported upgrade path are release-tested.
- No promise of universal down-migrations; every breaking schema change documents rollback/forward-fix strategy.
- Prefer expand-and-contract migrations for zero-downtime-friendly evolution.

## 10. CLI implementation contract

Required v0.1 commands:
```text
forge new <name>
forge modules list
forge modules graph
forge modules validate
forge db status
forge db migrate
forge doctor
forge upgrade check --dry-run
forge audit verify
```

CLI rules:
- deterministic and idempotent
- `--dry-run` for mutations where practical
- ordinary inspectable source files are the output
- no proprietary build layer
- no hosted AI dependency

## 11. Developer and coding-agent workflow

1. Read `AGENTS.md` and relevant accepted ADRs.
2. For significant changes, create/update a spec under `specs/changes/` with module/API/data/security/privacy/tenancy/audit/accessibility/localisation impact.
3. Implement against public contracts and module boundaries.
4. Add/modify deterministic tests before considering the change complete.
5. Run `forge modules validate`, architecture tests, tenant/security gates and affected provider tests.
6. Any conflict with an accepted ADR requires a superseding ADR; do not silently reinterpret the baseline.

### Agent operating rule
> AI proposes; deterministic tooling proves.

Agents may implement code/tests/docs/migrations and propose ADRs. They must not weaken gates, suppress vulnerabilities, change licences, publish releases, expose secrets or silently supersede accepted ADRs.

## 12. Definition of Done

A backlog item is Done only when:
- functionality and negative cases meet acceptance criteria;
- module boundaries and tenant isolation are proven;
- permissions and audit requirements are implemented;
- personal/sensitive data classification has been considered;
- localisation and accessibility impacts are addressed;
- API/event compatibility is preserved or intentionally versioned;
- real provider integration tests exist where provider behaviour matters;
- documentation is generated from authoritative sources where possible;
- no new dependency enters without licence/security review;
- CI gates pass without suppressions added merely to obtain green status.

## 13. Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Scope creep from 39 ADRs | High | High | Use v0.1 acceptance criteria as contract; post-0.1 features may only add seams/contracts, not full products. |
| Framework abstraction creep | High | High | Architecture review for each new abstraction; prefer .NET primitives; no generic repository/UoW or proprietary logging/cache layers. |
| Tenant isolation defect | High | Critical | Central filters/resolution, negative test suite, security eventing, release blocker. |
| Audit immutability overclaim | Medium | Critical | Differentiate append-only, tamper-evident and storage-enforced WORM; require capability validation. |
| Quartz durability semantics drift | Medium | High | Contract tests for retries/idempotency/terminal failure; provider-specific integration tests. |
| UI dependency licensing | Medium | High | Permissive licence review before adoption; avoid making proprietary component libraries mandatory. |
| OpenIddict/Identity complexity | Medium | High | Ship minimal v0.1 identity surface and extensibility seams; defer full SSO/SCIM/SAML admin workflows. |
| Localisation retrofit | Low | High | Foundation requirement in every first-party module from first UI/API strings. |
| Accessibility regression | Medium | High | Design-system primitives + automated CI + manual release sign-off. |
| Package sprawl | Medium | Medium | Package boundary requires dependency/deployment/ownership justification. |
| AI-generated architecture drift | Medium | High | Agent rules + deterministic architecture/security tests; accepted ADRs cannot be silently changed. |
| SQL Server assumptions block PostgreSQL | Medium | Medium | Keep provider-specific logic isolated; portability review for first-party module persistence. |
| Build duration / test cost | Medium | Medium | Layered CI; fast PR gates, heavier release matrix; real DB tests targeted by affected modules. |
| Name/trademark collision | Medium | Medium | Use Project Forge internally until public trademark/domain/package validation. |

## 14. ADR implementation traceability

| ADR | Capability | v0.1 implementation location | Primary proof |
|---:|---|---|---|
| 01 | Modularity | Phase 1 - Module kernel | Module graph and cycle tests |
| 02 | Modularity | Phase 1 - Module kernel | Lifecycle contract tests |
| 03 | Persistence | Phase 1 - Persistence ownership | Architecture + SQL integration tests |
| 04 | Events | Phases 1/3 - Communication + outbox | Event contract + outbox tests |
| 05 | Tenancy | Phase 2 - Tenancy core | Negative tenant isolation suite |
| 06 | Identity | Phase 2 - Identity/permissions | Authorization + impersonation tests |
| 07 | Entitlements | Deferred implementation; preserve contracts only | Architecture review; no v0.1 gating engine required |
| 08 | Audit | Phase 2 - Audit evidence | Redaction, tamper verification, immutable mode tests |
| 09 | Privacy | Phase 4 - Privacy primitives | Classification/retention/legal-hold tests |
| 10 | Jobs | Phase 3 - Quartz jobs | Durability, retry, idempotency tests |
| 11 | Notifications | Phase 4 - Notifications | Preference/policy/durable delivery tests |
| 12 | Localisation | Phase 4 + all UI | Culture/RTL/time-zone tests |
| 13 | Settings | Phase 4 - Settings/secrets/flags | Scope/cache/secret separation tests |
| 14 | Storage | Phase 4 - Storage pipeline | Quarantine/hash/private-access tests |
| 15 | Observability | Phase 3 - Observability | Trace continuity + sensitive-data leak tests |
| 16 | API | Phase 3 - API platform | OpenAPI/problem/idempotency/compat tests |
| 17 | Caching | Phase 4 - Redis adapter | Tenant key/invalidation/degrade tests |
| 18 | Security | Phases 0/2/all | Security regression + unsafe config tests |
| 19 | Accessibility | Phase 5 + design system | axe/Playwright/manual gate |
| 20 | Quality | Phase 0 + release | Conformance pipeline |
| 21 | Packaging | Phase 5 - Release engineering | Package validation/signing/SBOM |
| 22 | Lifecycle | Phase 5 - Release engineering | Upgrade/lifecycle docs and migration tests |
| 23 | CLI | Phases 0/5 | CLI integration/idempotency tests |
| 24 | AI/repo | Phase 0 - Repository contract | AGENTS + deterministic checks |
| 25 | Deployment | Phase 5 - Aspire/OCI | Local topology/container/migrator tests |
| 26 | Resilience | Reference conventions only in v0.1 | HTTP resilience test fixture |
| 27 | Search | Post-0.1 | No v0.1 implementation beyond extension seams |
| 28 | Realtime | Post-0.1 | No v0.1 implementation beyond notification-ready contracts |
| 29 | Reporting | Post-0.1 | No v0.1 implementation |
| 30 | Data access | Phase 1 - Persistence | Architecture and integration tests |
| 31 | Domain | Phase 1 - Vertical slice | Domain convention tests/examples |
| 32 | Messaging | Post-0.1 external broker; v0.1 in-process/outbox | In-process event/outbox tests |
| 33 | Workflow | Post-0.1 | Quartz timeout seam only, no workflow engine |
| 34 | Organisation | Post-0.1 | No v0.1 implementation |
| 35 | Licensing | Phase 0/release | Licence scan and Apache-2.0 artefacts |
| 36 | Ecosystem | Post-0.1 | Module manifest designed for future verification |
| 37 | Admin UX | Phase 5 | Blazor shell/accessibility tests |
| 38 | Templates | Phase 4 | Sandbox/allow-list/localisation tests |
| 39 | Integration sync | Post-0.1 | No v0.1 implementation |

## 15. Release milestone definition

Forge v0.1 may be tagged only when every Foundation Pack v0.1 acceptance criterion is evidenced by an automated test, build artefact, documented manual gate, or an explicit accepted deferral that does not contradict the baseline.

### Public developer journey
```bash
dotnet tool install --global Forge.Cli
forge new Acme
cd Acme
dotnet run --project src/Acme.AppHost
```

The resulting application must expose an understandable, ordinary .NET solution with explicit modules, Aspire local topology, SQL Server persistence, identity/permissions, tenant context, audit, Quartz jobs, localisation, observability and the accessible admin reference UI.

## 16. Immediate build order

Start implementation in this order:
1. Repository/CI/ADR/architecture-test contract.
2. Forge.Core + Forge.Modularity.
3. SQL Server module persistence and migrations.
4. ReferenceCatalog vertical slice.
5. Tenancy and negative isolation suite.
6. Identity/OpenIddict + permission model.
7. Audit including tamper verification and immutable-capability seam.
8. Outbox + in-process event bus.
9. Quartz durable jobs.
10. API conventions + OpenTelemetry/health.
11. Settings/localisation/privacy/storage/notifications/templates.
12. Blazor admin shell, Aspire, CLI completion and release engineering.

Do not start the post-v0.1 product areas merely because an ADR exists for them.