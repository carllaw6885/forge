# ADR 30: Data Access & Persistence Patterns

- Status: Accepted
- Baseline: v0.2

## Decision

EF Core is used directly as the reference model; generic repository and generic Unit of Work layers are not imposed. Modules own contexts. Targeted conventions may help tenancy, outbox, audit, deletion and concurrency without obscuring EF; Dapper remains a valid local choice.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
