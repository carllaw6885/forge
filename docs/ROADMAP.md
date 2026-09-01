# v0.1 roadmap (completed)

Phases 0–5 below are the completed baseline (shipped 0.1.x, 2026-09-01). The living roadmap is `POST_V0.1_ROADMAP.md`.

## 0 — Repository contract

**Window:** Weeks 1-2 — **complete**

ADR catalogue, module manifest, package conventions, architecture tests, CI baseline, security policy and deterministic CLI skeleton.

## 1 — Executable modular kernel

**Window:** Weeks 3-6 — **complete**

Explicit composition/lifecycle, module boundaries, EF contexts/migrations, synchronous contracts, domain events and sample vertical slice.

## 2 — Security and tenancy

**Window:** Weeks 7-11 — **complete**

Identity/OpenIddict, permissions, tenant resolution/filtering, host scope, impersonation context, audit evidence and isolation tests.

## 3 — Reliable application services

**Window:** Weeks 12-16 — **complete**

Outbox, in-process event bus, Quartz jobs, idempotency, Problem Details/OpenAPI, OpenTelemetry and health/readiness.

## 4 — Foundational enterprise services

**Window:** Weeks 17-22 — **complete**

Settings/secrets/flags, localisation, privacy/classification, storage pipeline, notifications/templates and Redis adapter.

## 5 — Reference product surface

**Window:** Weeks 23-28 — **complete**

Blazor admin shell/design tokens, WCAG gates, tenant/security context, jobs/audit/settings UI, Aspire AppHost and OCI packaging.

## Post-0.1

See `POST_V0.1_ROADMAP.md`: v0.2 Application Experience (SaaS starter, first-party module UIs — ADR 40) in parallel with Distributed Capability; v0.3 Enterprise starter; v0.4 Integration; v0.5 Ecosystem; v1.0 stability contract.
