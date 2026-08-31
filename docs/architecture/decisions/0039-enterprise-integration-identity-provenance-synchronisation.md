# ADR 39: Enterprise Integration Identity, Provenance & Synchronisation

- Status: Accepted
- Baseline: v0.2

## Decision

Reusable primitives cover multiple external identities, provenance, sync state, checkpoints, conflicts and reconciliation without a universal business model. Modules declare authority, direction and conflict strategy; sync is durable, resumable and idempotent; external deletion never implies automatic local deletion.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
