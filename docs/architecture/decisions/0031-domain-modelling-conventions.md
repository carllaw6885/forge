# ADR 31: Domain Modelling Conventions

- Status: Accepted
- Baseline: v0.2

## Decision

DDD is supported without ceremony. Entities, aggregates, value objects, strongly typed IDs and domain events are lightweight conventions; behaviours compose explicitly; invariants stay in the domain where appropriate; infrastructure stays outside; timestamps and clocks are explicit.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
