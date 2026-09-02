# ADR implementation traceability

| ADR | Capability | v0.1 location | Primary proof |
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
| 39 | Integration sync | Post-0.1 (v0.4) | No v0.1 implementation |
| 40 | First-party UI & starters | Phase 6 (Identity), Phase 7 (Audit, starters), Phase 8 (Tenancy); v0.3 Enterprise starter | Capability runs without UI; UI package installs/removes independently; SaaS starter E2E; WCAG/localisation/tenant/permission tests |
