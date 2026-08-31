# Forge starter v0.2

Architecture-first starter repository aligned to the 39 accepted ADRs. This is a foundation scaffold: use `docs/ROADMAP.md` and `docs/V0.1_ACCEPTANCE.md` as the implementation contract.

## Getting started

```bash
dotnet test Forge.slnx                        # build + all suites (architecture, tenancy, CLI)
dotnet run --project src/Forge.Cli -- doctor  # check the repo against Forge conventions
```

Before making significant changes, read `AGENTS.md`, `docs/FOUNDATION_PACK.md` and the active `implementation/phase-*` file. Repository conventions live in `eng/CONVENTIONS.md`; the PR quality gate is defined in `eng/QUALITY_GATES.md`.
