# ADR 37: Administration UX & Design System

- Status: Accepted
- Baseline: v0.2

## Decision

Forge provides a cohesive accessible admin shell and design system, with Blazor Web App as reference. Modules contribute through explicit extension contracts; tokens protect consistency, accessibility and safe white-labelling; tenant, impersonation and security context stay visible; capabilities remain headless.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
