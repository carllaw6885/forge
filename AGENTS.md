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

## First-party UI rules (ADR 40)
- A capability package never references its first-party UI package; the capability must stay usable headless.
- First-party UI consumes the public capability contracts, not privileged internals; an application can replace one Forge UI and keep the module.
- All first-party UI uses the Forge design system, is localised (incl. RTL) and passes the WCAG 2.2 AA gate; it is production-quality, never labelled "sample".
- Navigation visibility is never authorisation: privileged actions are server-authorised and audited; tenant and impersonation context stay visible.

## Agent permissions
Agents may edit code, tests, docs and migrations and may propose ADRs.
Agents must not weaken gates, suppress vulnerabilities, change licensing, expose secrets, publish releases or contradict accepted ADRs.

## Required workflow
1. Read the relevant milestone in `docs/POST_V0.1_ROADMAP.md` (v0.1 `implementation/phase-*` files are completed history).
2. Identify affected ADRs using `implementation/TRACEABILITY.md`.
3. Implement with tests proving tenancy/security/architecture implications.
4. Run deterministic validation.
5. Update the checklist.

AI proposes; deterministic tooling proves.
