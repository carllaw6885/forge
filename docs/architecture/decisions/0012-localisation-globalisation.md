# ADR 12: Localisation & Globalisation

- Status: Accepted
- Baseline: v0.2

## Decision

Localisation is foundational and uses standard .NET primitives. Culture resolution, module resources, fallbacks, tenant/application overrides, time zones, currencies, locale formatting, localised domain content and LTR/RTL UI are supported; culture, time zone and currency stay distinct.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
