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
- full conformance suite
- fresh database install + supported upgrade
- OCI smoke test without Aspire runtime
- manual WCAG 2.2 AA release checklist
- security sign-off for sensitive changes
- signed packages + SBOM + provenance
