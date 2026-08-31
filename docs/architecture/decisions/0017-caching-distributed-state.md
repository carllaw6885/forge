# ADR 17: Caching & Distributed State

- Status: Accepted
- Baseline: v0.2

## Decision

Caching uses standard .NET primitives and is an optimisation. Tenant-safe keys are mandatory; Redis is the reference distributed provider; event-driven invalidation is preferred with TTL as a safety net; sensitive data requires policy and coordination/locking remains separate.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
