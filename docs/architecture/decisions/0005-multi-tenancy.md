# ADR 05: Multi-Tenancy

- Status: Accepted
- Baseline: v0.2

## Decision

Tenancy is a first-class isolation and configuration boundary, not a business entity. Ownership is explicit, filtering is centrally enforced, host and tenant scopes are distinct, shared-database and database-per-tenant models are supported, and cross-tenant access is explicit and privileged.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
