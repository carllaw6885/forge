# ADR 11: Notifications & Communications

- Status: Accepted
- Baseline: v0.2

## Decision

Notifications are channel-neutral intents with durable delivery, localisation, versioned templates, recipient preferences and policy overrides. In-app, email, SMS, push and webhook providers remain adapters; security-critical communications may override user preferences.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
