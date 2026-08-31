# ADR 14: Storage & File Management

- Status: Accepted
- Baseline: v0.2

## Decision

Metadata, binary storage, access policy and domain ownership are separate. Storage is provider-neutral and tenant-aware; uploads are validated, quarantined and scan-ready; integrity, classification, retention, legal hold, encryption, residency and immutable storage are first-class.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
