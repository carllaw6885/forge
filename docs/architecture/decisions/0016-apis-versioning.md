# ADR 16: APIs & Versioning

- Status: Accepted
- Baseline: v0.2

## Decision

REST/HTTP, Minimal APIs, Problem Details and generated OpenAPI are the reference public API style. Modules own endpoints and DTOs; compatibility, idempotency, rate limits, pagination, deprecation and tenant-safe handling are explicit. Controllers, gRPC and GraphQL remain optional.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
