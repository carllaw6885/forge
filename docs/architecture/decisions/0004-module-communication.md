# ADR 04: Module Communication

- Status: Accepted
- Baseline: v0.2

## Decision

Immediate cross-module requests use explicit synchronous contracts; internal domain events stay inside a module; versioned integration events cross module or system boundaries. Domain entities are never shared, and reliable publication uses an outbox.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
