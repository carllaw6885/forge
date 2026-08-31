# Forge Foundation Pack

**Consolidated baseline:** v0.2  
**Implementation target:** v0.1  
**Decision status:** 39 accepted ADRs  
**Rebuilt:** 31 August 2026

> This pack reconstructs the accepted decision record from the referenced review conversation. The previously referenced `/mnt/data/forge_foundation_pack` source directory was not present in the execution environment; this edition is therefore a coherent replacement baseline rather than a byte-preserving edit.

## Executive position

Forge is an Apache-2.0, cloud-neutral, modular .NET application foundation. It favours explicit composition, standard .NET primitives, strong tenant/security boundaries, transparent tooling and deterministic proof. It begins as a modular monolith, preserves extraction seams, and refuses premature distributed-system complexity.

The 39 ADRs are normative. Summaries below are authoritative for v0.2; detailed implementation specifications may refine mechanics but cannot contradict them without a superseding ADR.

## Architectural north stars

- Explicit over magical: startup, module dependencies, contracts and ownership remain inspectable.
- Modular monolith first: service extraction is enabled, never assumed.
- Standard platform first: extend .NET and its ecosystem rather than duplicate them.
- Secure and tenant-safe by default: boundary failures are release blockers.
- Durable where failure matters: outbox, idempotency, retry and audit are deliberate.
- Headless capabilities, cohesive reference UX: business modules do not depend on the admin shell.
- Open source without open-core gating: ecosystem trust is evidence-based.
- AI may propose; deterministic tooling proves.

## Accepted ADR register

### ADR 01 — Explicit Module Composition

Forge applications compose modules explicitly in ordinary .NET startup code. Dependencies are visible, deterministic and inspectable; convention may reduce repetition but never hides composition or silently activates capabilities.

### ADR 02 — Minimal Module Lifecycle

Modules use a deliberately minimal lifecycle: service registration, application configuration and declarative metadata/dependencies. Additional lifecycle stages require a demonstrated need.

### ADR 03 — Module Persistence

Each module owns its persistence boundary and normally its own DbContext and migrations. Contexts may share a physical database, but modules do not access another module's context or create cross-module database foreign keys.

### ADR 04 — Module Communication

Immediate cross-module requests use explicit synchronous contracts; internal domain events stay inside a module; versioned integration events cross module or system boundaries. Domain entities are never shared, and reliable publication uses an outbox.

### ADR 05 — Multi-Tenancy

Tenancy is a first-class isolation and configuration boundary, not a business entity. Ownership is explicit, filtering is centrally enforced, host and tenant scopes are distinct, shared-database and database-per-tenant models are supported, and cross-tenant access is explicit and privileged.

### ADR 06 — Identity & Authorisation

Forge builds on ASP.NET Core Identity, OpenIddict and standard authentication/authorisation. Permissions are first-class; roles aggregate permissions. External identity, MFA, passkeys, sessions, SSO, SCIM, SAML, service identities and audited impersonation are community capabilities.

### ADR 07 — Feature Management & Entitlements

Feature definitions, tenant entitlements and operational runtime flags are separate. Entitlements support typed values and limits, billing remains outside Core, usage metering is separate, and protected checks fail closed.

### ADR 08 — Audit & Compliance Evidence

Audit is structured evidence distinct from diagnostics and entity history. Versioned events preserve tenant, actor, impersonation, correlation, outcome and policy context; sensitive values are excluded or redacted by default; retention and export are policy-driven and auditable.

### ADR 09 — Privacy & Data Rights

Forge provides reusable privacy primitives, explicit classification, subject-right workflows, retention and legal hold. Erasure may delete, anonymise or pseudonymise; lawful basis and consent remain separate; privacy operations are fully audited.

### ADR 10 — Background Jobs & Scheduling

Durable and recurring jobs use provider-neutral contracts with Quartz as the reference provider. Production jobs are durable, at-least-once and idempotent, carry tenant/correlation context, and include retry, dead-letter, observability, versioning, secure payload and audited administration.

### ADR 11 — Notifications & Communications

Notifications are channel-neutral intents with durable delivery, localisation, versioned templates, recipient preferences and policy overrides. In-app, email, SMS, push and webhook providers remain adapters; security-critical communications may override user preferences.

### ADR 12 — Localisation & Globalisation

Localisation is foundational and uses standard .NET primitives. Culture resolution, module resources, fallbacks, tenant/application overrides, time zones, currencies, locale formatting, localised domain content and LTR/RTL UI are supported; culture, time zone and currency stay distinct.

### ADR 13 — Configuration, Settings, Secrets & Operational Flags

Deployment configuration, mutable typed settings, secrets and operational flags are separate. Settings are scoped, validated and audited; secrets use pluggable stores and never ordinary settings; operational rollout flags are not SaaS entitlements.

### ADR 14 — Storage & File Management

Metadata, binary storage, access policy and domain ownership are separate. Storage is provider-neutral and tenant-aware; uploads are validated, quarantined and scan-ready; integrity, classification, retention, legal hold, encryption, residency and immutable storage are first-class.

### ADR 15 — Observability & Operational Health

Forge uses standard .NET logging and OpenTelemetry. Tenant, module, correlation and deployment context propagate across HTTP, events, outbox, jobs and downstream calls without sensitive payloads. Audit remains separate; Aspire is the reference local experience.

### ADR 16 — APIs & Versioning

REST/HTTP, Minimal APIs, Problem Details and generated OpenAPI are the reference public API style. Modules own endpoints and DTOs; compatibility, idempotency, rate limits, pagination, deprecation and tenant-safe handling are explicit. Controllers, gRPC and GraphQL remain optional.

### ADR 17 — Caching & Distributed State

Caching uses standard .NET primitives and is an optimisation. Tenant-safe keys are mandatory; Redis is the reference distributed provider; event-driven invalidation is preferred with TTL as a safety net; sensitive data requires policy and coordination/locking remains separate.

### ADR 18 — Security Architecture

Forge is secure by default on standard .NET primitives. Secrets and keys are externally managed; privileged and tenant-boundary operations are protected and audited; hardened templates, supply-chain controls, SBOMs, scanning, threat modelling and security regression tests are required.

### ADR 19 — Accessibility & Inclusive UI

All first-party UI and reusable components meet WCAG 2.2 AA, including keyboard operation, focus, semantics, screen readers, contrast, errors, reduced motion, responsive behaviour and RTL. Automated checks run in CI and manual assistive-technology testing gates releases.

### ADR 20 — Testing & Quality Gates

Testing spans unit, module, integration, end-to-end, architecture, security, accessibility and contracts. Real infrastructure is used where provider behaviour matters; tenant isolation and security are release gates; APIs/events and database upgrades are compatibility-tested.

### ADR 21 — Packaging, Versioning & Compatibility

Package boundaries require real dependency, deployment or ownership value. First-party packages use a coordinated release train and semantic versioning with deprecation-first breaking changes, package signing, provenance and SBOM metadata.

### ADR 22 — Upgrade & Lifecycle Policy

Forge follows supported .NET versions and defaults production templates to the current LTS. The current Forge major receives full support and the previous major a defined security/critical-fix window. Upgrade, migration and rollback implications are documented and tested.

### ADR 23 — Developer Tooling & CLI

The CLI is transparent, deterministic and idempotent, with dry-run where practical. It creates ordinary inspectable .NET files and supports project/module management, migrations, diagnostics, architecture/compliance validation and upgrades without becoming a proprietary build layer.

### ADR 24 — AI-Native Engineering & Repository Standards

Repositories are human- and agent-readable, vendor-neutral and keep decisions, specifications, module metadata and rules in source. Significant changes require impact assessment; accepted ADRs cannot be silently overridden; deterministic tooling proves compliance.

### ADR 25 — Deployment, Hosting & Portability

Forge is cloud-neutral and container-first. Aspire is the reference local orchestration experience, OCI containers are the production unit, Aspire is not a runtime requirement, cloud capabilities are adapters, migrations run independently and simple safe deployment is preferred.

### ADR 26 — Resilience & External Integrations

Standard .NET resilience primitives govern external calls. Timeouts, transient failures, safe retries and idempotency are explicit. Webhooks are authenticated, replay-protected, asynchronous and deduplicated; criticality, health, fallback, correlation and failure testing are defined.

### ADR 27 — Search & Indexing

Search indexes are rebuildable projections, never authoritative. Modules explicitly define searchable documents/fields; SQL Server-backed search is the initial reference; external providers are optional; asynchronous indexing, tenant isolation, privacy classification, localisation and versioned rebuilds are first-class.

### ADR 28 — Real-Time Delivery

A thin provider-neutral abstraction uses SignalR as reference. Modules publish tenant/user/topic-scoped intents, central infrastructure enforces authentication and isolation, payloads are minimal and non-authoritative, multi-instance scale-out is supported and presence is optional.

### ADR 29 — Reporting, Exports & Bulk Operations

Forge supplies tenant-aware reporting and bulk infrastructure, not BI. Large work runs durably; artefacts inherit storage/privacy/audit policy; localisation and time zones apply; imports support dry-run and structured validation; immutable snapshots support evidence needs.

### ADR 30 — Data Access & Persistence Patterns

EF Core is used directly as the reference model; generic repository and generic Unit of Work layers are not imposed. Modules own contexts. Targeted conventions may help tenancy, outbox, audit, deletion and concurrency without obscuring EF; Dapper remains a valid local choice.

### ADR 31 — Domain Modelling Conventions

DDD is supported without ceremony. Entities, aggregates, value objects, strongly typed IDs and domain events are lightweight conventions; behaviours compose explicitly; invariants stay in the domain where appropriate; infrastructure stays outside; timestamps and clocks are explicit.

### ADR 32 — Messaging Transport & Event Infrastructure

Integration-event contracts are transport-neutral; in-process delivery serves simple monoliths; RabbitMQ is the reference external broker and Azure Service Bus first-class. Outbox, at-least-once delivery, idempotent consumers, optional inbox, retries, dead-lettering and schema compatibility are required.

### ADR 33 — Workflow & Process Orchestration

Forge supplies lightweight durable process-manager/saga primitives with explicit state, tenant/correlation context, idempotency, retry, timeout, audit and observability. Compensation is explicit, state belongs to the defining module, Quartz schedules timeouts, and external workflow engines remain valid.

### ADR 34 — Organisation Structure & Delegated Administration

Forge provides a tenant-bound hierarchical organisation-unit capability without business-specific semantics. Membership may be multiple; organisational scope constrains rather than replaces permissions; delegated admins cannot exceed effective authority; all scope and membership changes are audited.

### ADR 35 — Licensing, Commercial Model & Governance

Forge is Apache 2.0 and fully usable in commercial/proprietary software without paid feature licences or open-core gating. Revenue may come from services, support, training, LTS and marketplace activity. Governance is transparent, maintainer-led and uses public ADR/RFC processes.

### ADR 36 — Community Extensions & Ecosystem Trust

Extensions are first-party, verified third-party or unverified community modules distributed through standard ecosystems. Verification requires compatibility, security, licence, provenance, testing and maintenance evidence; only official packages use the Forge namespace; status may be suspended.

### ADR 37 — Administration UX & Design System

Forge provides a cohesive accessible admin shell and design system, with Blazor Web App as reference. Modules contribute through explicit extension contracts; tokens protect consistency, accessibility and safe white-labelling; tenant, impersonation and security context stay visible; capabilities remain headless.

### ADR 38 — Templates & Content Rendering

A unified, versioned capability serves notifications, communications and documents. Tenant-editable templates run in a constrained non-code sandbox with allow-listed variables and sanitised output; lifecycle, localisation, override, rollback, preview, validation and audit are built in; PDF/DOCX are adapters.

### ADR 39 — Enterprise Integration Identity, Provenance & Synchronisation

Reusable primitives cover multiple external identities, provenance, sync state, checkpoints, conflicts and reconciliation without a universal business model. Modules declare authority, direction and conflict strategy; sync is durable, resumable and idempotent; external deletion never implies automatic local deletion.

## Consolidated interpretations and contradiction resolution

### Templates vs notifications

ADR 11 owns notification intent, preferences and delivery; ADR 38 exclusively owns reusable rendering, template lifecycle and sandboxing.

### Jobs, messaging and workflows

ADR 10 executes discrete work, ADR 32 transports integration events, and ADR 33 coordinates durable multi-step processes. Quartz schedules jobs/timeouts; it is not the workflow model or message bus.

### In-process vs brokered events

ADR 4 defines communication semantics. ADR 32 selects in-process delivery for simple deployments and RabbitMQ for the reference external transport without changing contracts.

### EF Core vs repositories

ADR 3 defines module ownership; ADR 30 defines implementation style. Specific repositories remain allowed only where they express a real domain or performance boundary.

### Feature flags vs entitlements

ADR 7 governs commercial/capability access; ADR 13 governs operational rollout. A flag cannot grant an unentitled capability.

### Localisation, accessibility and admin UI

ADRs 12 and 19 are platform constraints; ADR 37 is their reference shell implementation, not a competing UI policy.

### Storage vs reports/templates

ADRs 29 and 38 create artefacts; ADR 14 governs their binary storage, classification, access, retention and legal hold.

### Audit vs telemetry

ADR 8 is durable compliance evidence; ADR 15 is operational diagnostics. Neither substitutes for the other.

## Scope boundaries

### In the platform foundation

Module composition and validation; tenancy and identity primitives; permissions; audit/privacy/classification; settings/secrets/flags; localisation; storage abstractions; API, job, event and observability conventions; secure defaults; testing and lifecycle rules; CLI and repository contracts.

### Reference providers and experiences

EF Core and SQL Server; Quartz; RabbitMQ; Redis; SignalR; Blazor Web App; Aspire local orchestration. These are replaceable reference choices, not Core contract leakage.

### Explicitly outside Core

Billing, general-purpose BI, CMS, BPMN engines, universal canonical business entities, provider-specific cloud orchestration, mandatory external brokers, mandatory distributed cache/search, and arbitrary tenant-executable code.

## v0.1 implementation roadmap

| Phase | Window | Exit outcome |
|---|---:|---|
| 0 — Repository contract | Weeks 1-2 | ADR catalogue, module manifest, package conventions, architecture tests, CI baseline, security policy and deterministic CLI skeleton. |
| 1 — Executable modular kernel | Weeks 3-6 | Explicit composition/lifecycle, module boundaries, EF contexts/migrations, synchronous contracts, domain events and sample vertical slice. |
| 2 — Security and tenancy | Weeks 7-11 | Identity/OpenIddict, permissions, tenant resolution/filtering, host scope, impersonation context, audit evidence and isolation tests. |
| 3 — Reliable application services | Weeks 12-16 | Outbox, in-process event bus, Quartz jobs, idempotency, Problem Details/OpenAPI, OpenTelemetry and health/readiness. |
| 4 — Foundational enterprise services | Weeks 17-22 | Settings/secrets/flags, localisation, privacy/classification, storage pipeline, notifications/templates and Redis adapter. |
| 5 — Reference product surface | Weeks 23-28 | Blazor admin shell/design tokens, WCAG gates, tenant/security context, jobs/audit/settings UI, Aspire AppHost and OCI packaging. |
| Post-0.1 | After acceptance | RabbitMQ/ASB providers, workflows, search, realtime, reporting/imports, organisation scopes, ecosystem verification and sync/reconciliation. |

## v0.1 acceptance criteria

- [ ] A clean checkout builds and tests offline after dependencies are restored, with no hosted AI service required.
- [ ] The sample application composes at least three modules explicitly; the module graph is inspectable and invalid cycles fail validation.
- [ ] Each sample module owns its DbContext and migrations; architecture tests prevent cross-module context access and domain-entity references.
- [ ] A shared-database tenant path proves automatic filtering, host/tenant separation and deny-by-default cross-tenant access through negative tests.
- [ ] Identity uses ASP.NET Core Identity plus OpenIddict; permission checks are independent of roles and privileged impersonation is visible and audited.
- [ ] A state change and its integration event commit atomically through an outbox; duplicate delivery is tolerated and correlation/tenant context survives dispatch.
- [ ] Quartz executes a durable idempotent job with retry and observable terminal failure; in-memory jobs are rejected in production configuration.
- [ ] Public endpoints use DTOs, Problem Details and generated OpenAPI; idempotency and tenant-safe request handling are demonstrated.
- [ ] Structured audit evidence is queryable and distinct from logs; sensitive test values are redacted and retention/export actions are audited.
- [ ] OpenTelemetry traces connect HTTP, database, outbox and job activity without leaking seeded sensitive data; liveness and readiness are distinct.
- [ ] Localisation supports application, tenant and user resolution with at least en-GB and one RTL test culture; time-zone handling is deterministic.
- [ ] The storage sample validates and quarantines uploads, records integrity/classification metadata and uses a provider-neutral reference.
- [ ] A notification renders through the constrained template path, honours preferences unless policy overrides them, and records durable delivery state.
- [ ] The reference Blazor admin shell shows tenant, impersonation and security context and passes automated WCAG 2.2 AA checks for the acceptance journey.
- [ ] The CLI can create/inspect/validate the solution and run a dry-run upgrade check; generated outputs are ordinary source files.
- [ ] CI gates architecture, tenant isolation, security, API/event compatibility, fresh database install and supported upgrade migration.
- [ ] The Aspire local topology starts the app, database and telemetry dependencies; the production artifact is a standard OCI container and migrations run separately.
- [ ] The v0.1 release publishes signed packages plus SBOM/provenance evidence and documents supported .NET/Forge lifecycle and upgrade constraints.

## Deferred after v0.1

The following are intentionally specified by ADR but need not be complete for v0.1: external brokers beyond a minimal reference path, full workflow operations UI, enterprise search providers, realtime scale-out, advanced reports/imports, organisation-scope administration, public extension marketplace, document renderer providers and full sync/reconciliation workbench.

## Change control

A change that contradicts an accepted ADR requires a new superseding ADR, impact assessment, migration/compatibility analysis and updated acceptance proof. Documentation drift is a build defect; generated repositories retain this register under `docs/architecture/decisions`.

## Provenance

Source: the accepted architecture review conversation titled “Review ABP Open Source Platform”, consolidated through ADR 39. The original requested filesystem pack was unavailable, so this pack was reconstructed from the conversation record and packaged as a new v0.2 baseline.
