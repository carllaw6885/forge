# Phase 3 - Reliable application services

**Window:** Weeks 12-16

**Exit goal:** Prove durable work, event reliability, APIs and observability.

## 3.1 Transactional outbox

- [ ] Persist entity changes and outbox entries atomically in the owning module transaction.
- [ ] Implement outbox dispatcher with duplicate-tolerant delivery.
- [ ] Preserve tenant, correlation and causation context.
- [ ] Add retry/backoff and operational lag metrics.

## 3.2 Quartz jobs

- [ ] Implement provider-neutral durable job contracts.
- [ ] Implement Quartz reference provider with persistent SQL-backed store.
- [ ] Capture/restore tenant and correlation context automatically.
- [ ] Implement idempotency key support, retry policy and observable terminal failure/dead-letter projection.
- [ ] Reject in-memory job provider under production validation.

## 3.3 API platform

- [ ] Standardise Minimal API conventions, DTO boundaries, Problem Details and OpenAPI.
- [ ] Implement idempotency support for opted-in commands.
- [ ] Implement tenant-safe request handling and reference rate-limit policy.
- [ ] Add OpenAPI compatibility snapshot/diff gate.

## 3.4 Observability and health

- [ ] Implement OpenTelemetry traces/metrics using standard .NET primitives.
- [ ] Propagate context across HTTP, EF, outbox and jobs.
- [ ] Implement liveness/readiness distinction and dependency health registration.
- [ ] Prove seeded sensitive values do not appear in logs/traces.
