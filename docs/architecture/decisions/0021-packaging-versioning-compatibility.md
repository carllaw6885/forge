# ADR 21: Packaging, Versioning & Compatibility

- Status: Accepted
- Baseline: v0.2

## Decision

Package boundaries require real dependency, deployment or ownership value. First-party packages use a coordinated release train and semantic versioning with deprecation-first breaking changes, package signing, provenance and SBOM metadata.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
