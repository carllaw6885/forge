# Phase 1 - Executable modular kernel

**Window:** Weeks 3-6

**Exit goal:** Prove explicit modular composition, persistence ownership and a working vertical slice.

## 1.1 Module kernel

- [x] Implement AddForge and explicit module registration.
- [x] Implement minimal ConfigureServices/ConfigureApplication lifecycle.
- [x] Validate declared dependencies; no assembly-wide auto-discovery.
- [x] Expose inspectable module graph to CLI and diagnostics.

## 1.2 Persistence ownership

- [x] Create module-owned DbContext pattern with SQL Server reference provider.
- [x] Create module-owned migrations and independent migration metadata.
- [x] Add no-cross-module-foreign-key architecture tests.
- [x] Add provider test harness using real SQL Server container.

## 1.3 Communication primitives

- [x] Implement synchronous public contract guidance and sample.
- [x] Implement internal domain event collector/dispatcher.
- [x] Define versioned integration event envelope including tenant, correlation, causation, event id and schema version.
- [x] Implement in-process integration event bus for v0.1.

## 1.4 First vertical slice

- [x] Create Reference Catalog module as deliberately simple tenant-owned CRUD capability.
- [x] Expose Minimal API DTOs, validation, Problem Details and OpenAPI.
- [x] Persist changes in module DbContext and emit domain/integration events.
- [x] Demonstrate localisation resources and structured audit contribution.
