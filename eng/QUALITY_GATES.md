# Quality gates

## PR
- restore/build/format/analyzers
- unit and module tests
- architecture tests
- secret/vulnerability/licence scans
- SBOM generation

## Affected integration
- SQL Server integration tests
- tenant isolation tests
- Quartz durability/idempotency tests
- OpenAPI/event compatibility tests
- accessibility tests where UI changed

## Release
- authorization regression suite (Forge.SecurityTests) — blocking
- tenant-boundary suite (Forge.TenancyTests + tenant isolation tests in Forge.Persistence.SqlServer.Tests and slice tests) — blocking; a tenant isolation failure blocks release unconditionally
- full conformance suite
- fresh database install + supported upgrade
- OCI smoke test without Aspire runtime
- manual WCAG 2.2 AA release checklist
- security sign-off for sensitive changes
- signed packages + SBOM + provenance
