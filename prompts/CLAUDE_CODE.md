# Forge v0.1 coding-agent implementation prompt

You are implementing Forge v0.1 from the repository's accepted architecture baseline.

## Mandatory reading
1. `AGENTS.md`
2. `docs/FOUNDATION_PACK.md`
3. `docs/IMPLEMENTATION_PACK.md`
4. Relevant ADRs and current phase file under `implementation/`

## Non-negotiables
- Explicit module composition; no assembly magic.
- Minimal module lifecycle.
- Module-owned DbContext and migrations; never access another module's context.
- No cross-module domain entities or database foreign keys.
- Standard .NET primitives first; no generic repository/UoW framework.
- Tenant isolation is deny-by-default and a release gate.
- Audit is distinct from logs and must support tamper-evident/immutable modes.
- Quartz is the reference durable job provider.
- SQL Server is the v0.1 reference database.
- First-party UI must satisfy WCAG 2.2 AA and localisation/RTL requirements.
- No hosted AI dependency is required for build or validation.
- Do not implement post-v0.1 product areas unless required as a seam by the implementation pack.
- Do not change an accepted ADR implicitly. Propose a superseding ADR instead.

## Workflow
1. Select the next unchecked task in the active implementation phase.
2. State affected modules and contracts.
3. Add or update tests first for architecture/security/tenancy invariants where relevant.
4. Implement the smallest ordinary .NET solution that satisfies the task.
5. Run affected test suites and deterministic validators.
6. Update implementation checklists and generated documentation only from authoritative sources.
7. Stop if the task requires contradicting an accepted ADR.

AI proposes; deterministic tooling proves.
