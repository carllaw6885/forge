# Lifecycle, upgrade and rollback policy

Per ADR 22. This page ships with each release and is updated when support windows change.

## Supported platforms

- Forge v0.1.x targets **.NET 10 (LTS)**. Production templates default to the current LTS.
- Reference providers: SQL Server 2022+, Quartz (SQL-backed store), Redis 7+ (optional).

## Support windows

- The **current Forge major** receives features, fixes and security patches.
- The **previous major** receives security and critical fixes for 12 months after the next major ships.
- Pre-1.0: each 0.x minor supersedes the previous; only the latest 0.x is supported.

## Upgrading

1. Read the release notes for the target version — breaking changes are deprecation-first (ADR 21) and called out explicitly.
2. Update the `Forge.*` package pins (or `ForgeVersion`); `forge upgrade check` reports drift.
3. Run the migrator (`forge db migrate` / your DbMigrator) **before** deploying app instances. Migrations are expand-and-contract where feasible; each release's fresh-install and supported-upgrade paths are CI-tested.
4. Deploy the app. Aspire is a local-development experience only; production remains the OCI container + migrator pair.

## Rollback

- **App rollback** is always supported within a minor: redeploy the previous image.
- **Schema rollback** is not universally promised (ADR 22): every breaking schema change documents a rollback or forward-fix strategy in its release notes. Where a `Down` migration exists it is best-effort, not release-tested.
- Audit evidence is append-only: rollbacks never truncate the audit trail.

## Release evidence

Each release publishes: signed/attested packages (GitHub provenance attestation), CycloneDX SBOM, the conformance run (all suites incl. WCAG gate, fresh install, supported upgrade), and the manually signed-off accessibility and security checklists in the release notes.
