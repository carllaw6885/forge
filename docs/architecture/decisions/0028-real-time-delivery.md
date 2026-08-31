# ADR 28: Real-Time Delivery

- Status: Accepted
- Baseline: v0.2

## Decision

A thin provider-neutral abstraction uses SignalR as reference. Modules publish tenant/user/topic-scoped intents, central infrastructure enforces authentication and isolation, payloads are minimal and non-authoritative, multi-instance scale-out is supported and presence is optional.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
