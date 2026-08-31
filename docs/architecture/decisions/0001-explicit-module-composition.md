# ADR 01: Explicit Module Composition

- Status: Accepted
- Baseline: v0.2

## Decision

Forge applications compose modules explicitly in ordinary .NET startup code. Dependencies are visible, deterministic and inspectable; convention may reduce repetition but never hides composition or silently activates capabilities.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
