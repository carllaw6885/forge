# ADR 10: Background Jobs & Scheduling

- Status: Accepted
- Baseline: v0.2

## Decision

Durable and recurring jobs use provider-neutral contracts with Quartz as the reference provider. Production jobs are durable, at-least-once and idempotent, carry tenant/correlation context, and include retry, dead-letter, observability, versioning, secure payload and audited administration.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
