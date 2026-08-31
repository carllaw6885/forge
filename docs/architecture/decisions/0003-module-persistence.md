# ADR 03: Module Persistence

- Status: Accepted
- Baseline: v0.2

## Decision

Each module owns its persistence boundary and normally its own DbContext and migrations. Contexts may share a physical database, but modules do not access another module's context or create cross-module database foreign keys.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
