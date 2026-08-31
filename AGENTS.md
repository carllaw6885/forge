# Forge repository operating contract

Read `docs/FOUNDATION_PACK.md` and `docs/IMPLEMENTATION_PACK.md` before significant changes.

## Architectural rules
- Compose modules explicitly; no automatic assembly discovery.
- Keep module lifecycle minimal.
- A module owns its DbContext and migrations. Never access another module's context.
- Never share domain entities across modules or create cross-module database foreign keys.
- Use standard .NET primitives before introducing Forge abstractions.
- No generic repository or generic Unit of Work framework.
- Tenant-aware operations are deny-by-default; tenant isolation failures block release.
- Structured audit is distinct from ILogger and must preserve tamper-evident/immutable evidence capability.
- Quartz is the reference durable job provider.
- SQL Server is the v0.1 reference database.
- Localisation/globalisation and WCAG 2.2 AA apply to all first-party UI.
- Secrets never live in ordinary settings.
- Accepted ADRs cannot be silently overridden.

## Agent permissions
Agents may edit code, tests, docs and migrations and may propose ADRs.
Agents must not weaken gates, suppress vulnerabilities, change licensing, expose secrets, publish releases or contradict accepted ADRs.

## Required workflow
1. Read the active `implementation/phase-*` file.
2. Identify affected ADRs using `implementation/TRACEABILITY.md`.
3. Implement with tests proving tenancy/security/architecture implications.
4. Run deterministic validation.
5. Update the checklist.

AI proposes; deterministic tooling proves.
