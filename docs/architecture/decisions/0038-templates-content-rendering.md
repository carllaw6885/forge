# ADR 38: Templates & Content Rendering

- Status: Accepted
- Baseline: v0.2

## Decision

A unified, versioned capability serves notifications, communications and documents. Tenant-editable templates run in a constrained non-code sandbox with allow-listed variables and sanitised output; lifecycle, localisation, override, rollback, preview, validation and audit are built in; PDF/DOCX are adapters.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
