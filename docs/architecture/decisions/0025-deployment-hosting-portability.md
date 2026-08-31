# ADR 25: Deployment, Hosting & Portability

- Status: Accepted
- Baseline: v0.2

## Decision

Forge is cloud-neutral and container-first. Aspire is the reference local orchestration experience, OCI containers are the production unit, Aspire is not a runtime requirement, cloud capabilities are adapters, migrations run independently and simple safe deployment is preferred.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
