# ADR 23: Developer Tooling & CLI

- Status: Accepted
- Baseline: v0.2

## Decision

The CLI is transparent, deterministic and idempotent, with dry-run where practical. It creates ordinary inspectable .NET files and supports project/module management, migrations, diagnostics, architecture/compliance validation and upgrades without becoming a proprietary build layer.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
