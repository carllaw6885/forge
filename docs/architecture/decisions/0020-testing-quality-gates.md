# ADR 20: Testing & Quality Gates

- Status: Accepted
- Baseline: v0.2

## Decision

Testing spans unit, module, integration, end-to-end, architecture, security, accessibility and contracts. Real infrastructure is used where provider behaviour matters; tenant isolation and security are release gates; APIs/events and database upgrades are compatibility-tested.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
