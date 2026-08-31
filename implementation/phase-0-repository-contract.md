# Phase 0 - Repository contract

**Window:** Weeks 1-2

**Exit goal:** Make the architecture executable before product code expands.

## 0.1 Repository baseline

- [x] Create solution, Directory.Build.props/targets, package version policy, analyzers, formatting and deterministic build settings.
- [x] Import all 39 ADRs as individual accepted ADR files with stable identifiers and front matter.
- [x] Define module manifest schema and repository conventions.
- [x] Create AGENTS.md rules that reference deterministic checks rather than vendor-specific prompts.

## 0.2 Architecture enforcement

- [x] Add architecture tests preventing cross-module DbContext access, domain-entity sharing and forbidden UI/infrastructure references.
- [x] Add module dependency graph validator and cycle detection.
- [x] Add first tenant-isolation invariant test harness.
- [x] Add dependency licence/security policy and SBOM generation.

## 0.3 CI baseline

- [x] Build, format, unit tests, architecture tests, secret scan, vulnerability scan, licence scan and SBOM.
- [x] Add PR quality gate summary and artefact retention.
- [x] Create security disclosure and contribution templates.

## 0.4 CLI skeleton

- [x] Create Forge.Cli command host with deterministic output.
- [x] Implement forge modules list/graph/validate against manifests.
- [x] Implement forge doctor skeleton and --dry-run plumbing.
