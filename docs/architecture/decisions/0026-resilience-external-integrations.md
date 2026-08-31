# ADR 26: Resilience & External Integrations

- Status: Accepted
- Baseline: v0.2

## Decision

Standard .NET resilience primitives govern external calls. Timeouts, transient failures, safe retries and idempotency are explicit. Webhooks are authenticated, replay-protected, asynchronous and deduplicated; criticality, health, fallback, correlation and failure testing are defined.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
