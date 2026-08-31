# ADR 32: Messaging Transport & Event Infrastructure

- Status: Accepted
- Baseline: v0.2

## Decision

Integration-event contracts are transport-neutral; in-process delivery serves simple monoliths; RabbitMQ is the reference external broker and Azure Service Bus first-class. Outbox, at-least-once delivery, idempotent consumers, optional inbox, retries, dead-lettering and schema compatibility are required.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
