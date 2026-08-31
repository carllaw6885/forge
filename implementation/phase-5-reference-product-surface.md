# Phase 5 - Reference product surface

**Window:** Weeks 23-28

**Exit goal:** Deliver an installable, inspectable, accessible open-source reference product experience.

## 5.1 Blazor admin shell

- [x] Create Blazor Web App shell with explicit module contribution contracts.
- [x] Implement design tokens, light/dark/system modes and RTL layout support.
- [x] Make tenant and impersonation context visually obvious.
- [x] Implement admin surfaces for users/roles/permissions, audit, jobs, settings and localisation essentials.

## 5.2 Accessibility

- [x] Integrate axe/Playwright automated WCAG checks for acceptance journeys.
- [x] Add keyboard/focus/semantic regression tests.
- [x] Document manual assistive-technology release checklist.
- [x] Block release on known first-party WCAG 2.2 AA failures.

## 5.3 Aspire and packaging

- [x] Create Aspire AppHost for app, SQL Server, migrator and telemetry dependencies.
- [x] Provide ServiceDefaults and local developer diagnostics.
- [x] Create production OCI Dockerfiles/images running non-root where supported.
- [x] Ensure production runtime does not require AppHost.

## 5.4 CLI v0.1 completion

- [ ] Implement forge new against the reference template.
- [ ] Implement forge db status/migrate and forge doctor core checks.
- [ ] Implement forge upgrade check --dry-run.
- [ ] Ensure generated output is ordinary source and commands are idempotent.

## 5.5 Release engineering

- [ ] Validate fresh install and supported upgrade migration.
- [ ] Create signed NuGet packages, SBOM and provenance artefacts.
- [ ] Publish lifecycle/upgrade constraints and rollback notes.
- [ ] Run full conformance suite and manual accessibility/security sign-off.
