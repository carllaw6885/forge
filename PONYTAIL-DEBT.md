# Ponytail debt ledger

Deliberate simplifications, each marked in source with a `ponytail:` comment
naming its ceiling and upgrade trigger. Regenerate with `/ponytail-debt`
(scan: `grep -rnE '(#|//) ?ponytail:' src modules samples tests`).
A row leaves this ledger by being fixed, never by being re-dated silently —
trigger changes are recorded here with a reason.

Snapshot: v0.1 (2026-09-01) — 5 markers, 0 with lapsed triggers.

| Location | Simplification | Ceiling | Upgrade trigger |
|---|---|---|---|
| `src/Forge.Settings/Settings.cs:78` | Settings cache invalidation bumps one global version | Any write flushes every cached resolution | Per-key versions if write churn matters |
| `src/Forge.Admin.Blazor/Components/Pages/Jobs.razor:33` | Jobs surface reads the in-memory failure sink | Empty with any custom `ITerminalFailureSink` | Queryable durable failure projection |
| `src/Forge.Web/Idempotency.cs:28` | Idempotency keys held in process memory | Single-instance only; replay protection lost on restart/scale-out | Distributed store (SQL/Redis) with multi-instance support |
| `src/Forge.Persistence.SqlServer/OutboxDispatcher.cs:31` | Outbox drains on a 500 ms polling loop | Dispatch latency ≥ poll interval; idle polling cost | Per-context change signals if lag ever matters |
| `src/Forge.Auditing/AuditStore.cs:28` | `ReadAllAsync` loads the whole audit trail | Unpaged, O(trail) memory | Sequence-windowed paging when trails grow |

## Suggested 0.2 grouping

- **Multi-instance readiness:** idempotency store + outbox change signals + per-key settings invalidation.
- **Operational scale:** audit paging + durable failure projection (unlocks the jobs admin surface for custom sinks).

## Retired

| Location (was) | Debt | Retired |
|---|---|---|
| `src/Forge.Identity/IdentityModule.cs` | Ephemeral OpenIddict token keys | v0.1 pre-tag — persisted `IdentityKeyMaterial` certificates; production validation refuses ephemeral keys |
| `Forge.Identity` schema via `CreateTables` | No schema-evolution path for identity tables | v0.1 pre-tag — generated EF `InitIdentity` migration; migrator-owned like every module schema |
| `src/Forge.Jobs.Quartz` raw schema script at startup | Quartz schema install outside the migrator | Phase 5.3 — DbMigrator owns the Quartz schema |
| `modules/ReferenceCatalog` in-memory audit seam | Audit evidence not hash-chained | Phase 2.3 — real `IAuditStore` |
| `modules/ReferenceCatalog` direct in-process publish | Event lost if publish failed after commit | Phase 3.1 — transactional outbox |
