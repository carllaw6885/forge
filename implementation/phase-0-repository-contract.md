# Phase 0 - Repository contract

**Window:** Weeks 1-2

**Exit goal:** Make the architecture executable before product code expands.

## 0.1 Repository baseline

- [ ] Create solution, Directory.Build.props/targets, package version policy, analyzers, formatting and deterministic build settings.
- [ ] Import all 39 ADRs as individual accepted ADR files with stable identifiers and front matter.
- [ ] Define module manifest schema and repository conventions.
- [ ] Create AGENTS.md rules that reference deterministic checks rather than vendor-specific prompts.

## 0.2 Architecture enforcement

- [ ] Add architecture tests preventing cross-module DbContext access, domain-entity sharing and forbidden UI/infrastructure references.
- [ ] Add module dependency graph validator and cycle detection.
- [ ] Add first tenant-isolation invariant test harness.
- [ ] Add dependency licence/security policy and SBOM generation.

## 0.3 CI baseline

- [ ] Build, format, unit tests, architecture tests, secret scan, vulnerability scan, licence scan and SBOM.
- [ ] Add PR quality gate summary and artefact retention.
- [ ] Create security disclosure and contribution templates.

## 0.4 CLI skeleton

- [ ] Create Forge.Cli command host with deterministic output.
- [ ] Implement forge modules list/graph/validate against manifests.
- [ ] Implement forge doctor skeleton and --dry-run plumbing.
