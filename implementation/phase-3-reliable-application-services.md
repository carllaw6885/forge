# Phase 3 - Reliable application services

**Window:** Weeks 12-16

**Exit goal:** Prove durable work, event reliability, APIs and observability.

## 3.1 Transactional outbox

- [x] Persist entity changes and outbox entries atomically in the owning module transaction.
- [x] Implement outbox dispatcher with duplicate-tolerant delivery.
- [x] Preserve tenant, correlation and causation context.
- [x] Add retry/backoff and operational lag metrics.

## 3.2 Quartz jobs

- [x] Implement provider-neutral durable job contracts.
- [x] Implement Quartz reference provider with persistent SQL-backed store.
- [x] Capture/restore tenant and correlation context automatically.
- [x] Implement idempotency key support, retry policy and observable terminal failure/dead-letter projection.
- [x] Reject in-memory job provider under production validation.

## 3.3 API platform

- [x] Standardise Minimal API conventions, DTO boundaries, Problem Details and OpenAPI.
- [x] Implement idempotency support for opted-in commands.
- [x] Implement tenant-safe request handling and reference rate-limit policy.
- [x] Add OpenAPI compatibility snapshot/diff gate.

## 3.4 Observability and health

- [ ] Implement OpenTelemetry traces/metrics using standard .NET primitives.
- [ ] Propagate context across HTTP, EF, outbox and jobs.
- [ ] Implement liveness/readiness distinction and dependency health registration.
- [ ] Prove seeded sensitive values do not appear in logs/traces.
