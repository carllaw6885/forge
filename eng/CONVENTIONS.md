# Repository conventions

## Solution layout

- `src/<Package>/` — one project per real dependency, deployment or ownership boundary (ADR 21). Target layout: `docs/IMPLEMENTATION_PACK.md` §3/§4.
- `modules/<Module>/` — first-party reference modules.
- `tests/<Suite>/` — required suites listed in `docs/IMPLEMENTATION_PACK.md` §8.
- `docs/architecture/decisions/` — accepted ADRs, `NNNN-slug.md`, front matter `Status`/`Baseline`. Changes contradicting an accepted ADR require a superseding ADR.

## Module manifests

Every module project carries a `forge-module.json` in its project root, valid against `eng/module-manifest.schema.json`. The manifest declares identity, explicit dependencies and owned database schemas. It is metadata for inspection and validation (`forge modules list|graph|validate`) — never an auto-discovery or activation mechanism (ADR 01).

## Module communication (ADR 04)

- **Immediate cross-module requests** use explicit synchronous contracts: a public interface plus DTO records in the owning module's `Contracts` surface, registered in DI by the owning module's `ConfigureServices`. Consumers depend on the contract, never on the module's internals or entities. Sample:

  ```csharp
  // Owning module's public contract — DTOs only, no domain entities.
  public interface ICatalogReader
  {
      Task<CatalogItemDto?> FindAsync(Guid id, CancellationToken ct);
  }
  public sealed record CatalogItemDto(Guid Id, string Name);
  ```

- **Internal facts** are `IDomainEvent`s: raised into the scoped `DomainEventCollector`, dispatched explicitly by the owning module, and never visible outside it.
- **Cross-boundary facts** are `IIntegrationEvent`s: pure data records marked `[IntegrationEvent("dotted.name", schemaVersion)]`, travelling in an `EventEnvelope` (event id, tenant, correlation, causation, schema version). Delivery is at-least-once — consumers are idempotent. Reliable publication goes through the `IOutbox` contract (implementation lands in Phase 3).

## API conventions (ADR 16)

- Minimal APIs with DTO records in/out; domain entities never appear in signatures. Problem Details for every failure shape; OpenAPI metadata (`WithName`, `Produces`) on every endpoint.
- Mutating commands opt into idempotency with `.WithIdempotency()`; clients send `Idempotency-Key`, replays return the stored response with `Idempotency-Replayed: true`, concurrent duplicates get 409. Keys are tenant-scoped.
- The OpenAPI document is compatibility-gated: `tests/Forge.ReferenceCatalog.Tests/openapi.v1.snapshot.json` is the committed contract. A differing document fails CI; intentional changes regenerate it with `FORGE_UPDATE_OPENAPI=true` and commit the diff for review.
- Tenant-safe request handling is the tenancy middleware (deny-by-default); the reference rate-limit policy lives in `Forge.Web` security defaults.

## Packages and versions

- Central package management via `Directory.Packages.props`; no `Version=` attributes in project files.
- New dependencies require licence/security review against `eng/dependency-policy.md` before adoption.
- Build settings (`net10.0`, nullable, warnings-as-errors, deterministic) come from `Directory.Build.props`; do not override per-project without a recorded reason.

## Code style

- `.editorconfig` is authoritative; `dotnet format` must be clean (`--verify-no-changes` gates PRs).
- File-scoped namespaces; one public type per file for contracts.

## Determinism

CLI and tooling output is deterministic: stable ordering, invariant culture, no timestamps or machine-specific paths in output. Deterministic builds are enforced repository-wide.
