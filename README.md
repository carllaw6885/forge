# Forge

Architecture-first starter repository aligned to the 40 accepted ADRs. v0.1 (`docs/ROADMAP.md`, `docs/V0.1_ACCEPTANCE.md`) is complete and published as `ForgeStack.*` packages; the living roadmap is `docs/POST_V0.1_ROADMAP.md` (ADR 40: optional first-party module UIs and starter applications).

## Getting started

```bash
dotnet test Forge.slnx                        # build + all suites (architecture, tenancy, CLI)
dotnet run --project src/Forge.Cli -- doctor  # check the repo against Forge conventions
```

Before making significant changes, read `AGENTS.md`, `docs/FOUNDATION_PACK.md` and the active `implementation/phase-*` file. Repository conventions live in `eng/CONVENTIONS.md`; the PR quality gate is defined in `eng/QUALITY_GATES.md`.
