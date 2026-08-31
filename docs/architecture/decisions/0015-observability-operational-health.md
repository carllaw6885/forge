# ADR 15: Observability & Operational Health

- Status: Accepted
- Baseline: v0.2

## Decision

Forge uses standard .NET logging and OpenTelemetry. Tenant, module, correlation and deployment context propagate across HTTP, events, outbox, jobs and downstream calls without sensitive payloads. Audit remains separate; Aspire is the reference local experience.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
