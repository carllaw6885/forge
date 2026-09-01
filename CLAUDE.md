# Forge — Modular Monolith (.NET 10)

Apache-2.0, cloud-neutral modular .NET application foundation. Modular monolith first, extraction seams preserved, no premature distributed-system complexity. The 40 accepted ADRs are normative — read `docs/FOUNDATION_PACK.md`, `docs/IMPLEMENTATION_PACK.md` and `docs/POST_V0.1_ROADMAP.md` before significant changes, and `AGENTS.md` for the operating contract.

## Tech Stack

- **.NET 10** / C# 14, solution: `Forge.slnx`
- **ASP.NET Core** — explicit module composition in startup code (ADR 01); no automatic assembly discovery
- **EF Core + SQL Server** (v0.1 reference database) — one DbContext per module, module-scoped migrations
- **ASP.NET Core Identity + OpenIddict** — identity/authz; permissions are first-class, roles aggregate permissions (ADR 06)
- **Quartz** — reference durable job provider
- **Transactional outbox + in-process event bus** — reliable cross-module/integration events (ADR 04)
- **OpenTelemetry**, Problem Details, health/readiness (phase 3)
- **Redis adapter** (phase 4) — caching
- **Blazor admin shell + Aspire AppHost** (phase 5); WCAG 2.2 AA and localisation apply to all first-party UI
- **xUnit v3 + Testcontainers** — tests, including architecture and tenant-isolation invariant tests

## Architecture Rules (from accepted ADRs — cannot be silently overridden)

- Compose modules explicitly; minimal module lifecycle (register services, configure app, declare metadata/dependencies).
- A module owns its DbContext and migrations. Never access another module's context; no cross-module FKs; never share domain entities across modules.
- Cross-module communication: explicit synchronous contracts for immediate requests; domain events stay inside a module; versioned integration events cross boundaries via the outbox.
- Tenancy is a first-class isolation boundary: deny-by-default, centrally enforced filtering, host vs tenant scopes distinct, cross-tenant access explicit and privileged. Tenant isolation failures block release.
- Structured audit is distinct from ILogger; tamper-evident/immutable evidence, sensitive values redacted by default.
- Use standard .NET primitives before Forge abstractions. No generic repository or generic Unit of Work.
- Secrets never live in ordinary settings.
- Business modules must not depend on the admin shell (headless capabilities).

## Workflow

1. Read the active `implementation/phase-*` file (currently phase 6: Identity application experience, v0.2).
2. Identify affected ADRs via `implementation/TRACEABILITY.md`.
3. Implement with tests proving tenancy/security/architecture implications.
4. Run deterministic validation (`eng/QUALITY_GATES.md`); update the phase checklist.

AI proposes; deterministic tooling proves. Agents must not weaken gates, suppress vulnerabilities, change licensing, expose secrets, publish releases, or contradict accepted ADRs (they may propose new ADRs).

## Build & Test

```bash
dotnet build Forge.slnx
dotnet test Forge.slnx
dotnet format Forge.slnx --verify-no-changes
```

`Directory.Build.props` sets net10.0, nullable, warnings-as-errors, deterministic builds. Keep it that way.
