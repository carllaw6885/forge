# ADR 08: Audit & Compliance Evidence

- Status: Accepted
- Baseline: v0.2

## Decision

Audit is structured evidence distinct from diagnostics and entity history. Versioned events preserve tenant, actor, impersonation, correlation, outcome and policy context; sensitive values are excluded or redacted by default; retention and export are policy-driven and auditable.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
