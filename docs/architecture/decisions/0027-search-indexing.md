# ADR 27: Search & Indexing

- Status: Accepted
- Baseline: v0.2

## Decision

Search indexes are rebuildable projections, never authoritative. Modules explicitly define searchable documents/fields; SQL Server-backed search is the initial reference; external providers are optional; asynchronous indexing, tenant isolation, privacy classification, localisation and versioned rebuilds are first-class.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
