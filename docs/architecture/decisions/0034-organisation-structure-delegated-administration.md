# ADR 34: Organisation Structure & Delegated Administration

- Status: Accepted
- Baseline: v0.2

## Decision

Forge provides a tenant-bound hierarchical organisation-unit capability without business-specific semantics. Membership may be multiple; organisational scope constrains rather than replaces permissions; delegated admins cannot exceed effective authority; all scope and membership changes are audited.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
