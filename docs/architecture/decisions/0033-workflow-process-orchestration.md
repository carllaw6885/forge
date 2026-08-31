# ADR 33: Workflow & Process Orchestration

- Status: Accepted
- Baseline: v0.2

## Decision

Forge supplies lightweight durable process-manager/saga primitives with explicit state, tenant/correlation context, idempotency, retry, timeout, audit and observability. Compensation is explicit, state belongs to the defining module, Quartz schedules timeouts, and external workflow engines remain valid.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
