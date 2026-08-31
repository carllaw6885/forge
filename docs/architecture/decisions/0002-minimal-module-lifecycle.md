# ADR 02: Minimal Module Lifecycle

- Status: Accepted
- Baseline: v0.2

## Decision

Modules use a deliberately minimal lifecycle: service registration, application configuration and declarative metadata/dependencies. Additional lifecycle stages require a demonstrated need.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
