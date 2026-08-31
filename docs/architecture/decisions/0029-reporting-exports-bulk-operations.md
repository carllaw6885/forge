# ADR 29: Reporting, Exports & Bulk Operations

- Status: Accepted
- Baseline: v0.2

## Decision

Forge supplies tenant-aware reporting and bulk infrastructure, not BI. Large work runs durably; artefacts inherit storage/privacy/audit policy; localisation and time zones apply; imports support dry-run and structured validation; immutable snapshots support evidence needs.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
