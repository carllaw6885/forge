# ADR 13: Configuration, Settings, Secrets & Operational Flags

- Status: Accepted
- Baseline: v0.2

## Decision

Deployment configuration, mutable typed settings, secrets and operational flags are separate. Settings are scoped, validated and audited; secrets use pluggable stores and never ordinary settings; operational rollout flags are not SaaS entitlements.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
