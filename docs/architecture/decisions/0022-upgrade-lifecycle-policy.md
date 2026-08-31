# ADR 22: Upgrade & Lifecycle Policy

- Status: Accepted
- Baseline: v0.2

## Decision

Forge follows supported .NET versions and defaults production templates to the current LTS. The current Forge major receives full support and the previous major a defined security/critical-fix window. Upgrade, migration and rollback implications are documented and tested.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
