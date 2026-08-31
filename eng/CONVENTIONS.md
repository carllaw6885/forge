# Repository conventions

## Solution layout

- `src/<Package>/` — one project per real dependency, deployment or ownership boundary (ADR 21). Target layout: `docs/IMPLEMENTATION_PACK.md` §3/§4.
- `modules/<Module>/` — first-party reference modules.
- `tests/<Suite>/` — required suites listed in `docs/IMPLEMENTATION_PACK.md` §8.
- `docs/architecture/decisions/` — accepted ADRs, `NNNN-slug.md`, front matter `Status`/`Baseline`. Changes contradicting an accepted ADR require a superseding ADR.

## Module manifests

Every module project carries a `forge-module.json` in its project root, valid against `eng/module-manifest.schema.json`. The manifest declares identity, explicit dependencies and owned database schemas. It is metadata for inspection and validation (`forge modules list|graph|validate`) — never an auto-discovery or activation mechanism (ADR 01).

## Packages and versions

- Central package management via `Directory.Packages.props`; no `Version=` attributes in project files.
- New dependencies require licence/security review against `eng/dependency-policy.md` before adoption.
- Build settings (`net10.0`, nullable, warnings-as-errors, deterministic) come from `Directory.Build.props`; do not override per-project without a recorded reason.

## Code style

- `.editorconfig` is authoritative; `dotnet format` must be clean (`--verify-no-changes` gates PRs).
- File-scoped namespaces; one public type per file for contracts.

## Determinism

CLI and tooling output is deterministic: stable ordering, invariant culture, no timestamps or machine-specific paths in output. Deterministic builds are enforced repository-wide.
