# ADR 18: Security Architecture

- Status: Accepted
- Baseline: v0.2

## Decision

Forge is secure by default on standard .NET primitives. Secrets and keys are externally managed; privileged and tenant-boundary operations are protected and audited; hardened templates, supply-chain controls, SBOMs, scanning, threat modelling and security regression tests are required.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
