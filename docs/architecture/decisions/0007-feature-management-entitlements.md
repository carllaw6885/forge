# ADR 07: Feature Management & Entitlements

- Status: Accepted
- Baseline: v0.2

## Decision

Feature definitions, tenant entitlements and operational runtime flags are separate. Entitlements support typed values and limits, billing remains outside Core, usage metering is separate, and protected checks fail closed.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
