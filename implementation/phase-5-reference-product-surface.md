# Phase 5 - Reference product surface

**Window:** Weeks 23-28

**Exit goal:** Deliver an installable, inspectable, accessible open-source reference product experience.

## 5.1 Blazor admin shell

- [ ] Create Blazor Web App shell with explicit module contribution contracts.
- [ ] Implement design tokens, light/dark/system modes and RTL layout support.
- [ ] Make tenant and impersonation context visually obvious.
- [ ] Implement admin surfaces for users/roles/permissions, audit, jobs, settings and localisation essentials.

## 5.2 Accessibility

- [ ] Integrate axe/Playwright automated WCAG checks for acceptance journeys.
- [ ] Add keyboard/focus/semantic regression tests.
- [ ] Document manual assistive-technology release checklist.
- [ ] Block release on known first-party WCAG 2.2 AA failures.

## 5.3 Aspire and packaging

- [ ] Create Aspire AppHost for app, SQL Server, migrator and telemetry dependencies.
- [ ] Provide ServiceDefaults and local developer diagnostics.
- [ ] Create production OCI Dockerfiles/images running non-root where supported.
- [ ] Ensure production runtime does not require AppHost.

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
