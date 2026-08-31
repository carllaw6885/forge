# v0.1 risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Scope creep from 39 ADRs | High | High | Use v0.1 acceptance criteria as contract; post-0.1 features may only add seams/contracts, not full products. |
| Framework abstraction creep | High | High | Architecture review for each new abstraction; prefer .NET primitives; no generic repository/UoW or proprietary logging/cache layers. |
| Tenant isolation defect | High | Critical | Central filters/resolution, negative test suite, security eventing, release blocker. |
| Audit immutability overclaim | Medium | Critical | Differentiate append-only, tamper-evident and storage-enforced WORM; require capability validation. |
| Quartz durability semantics drift | Medium | High | Contract tests for retries/idempotency/terminal failure; provider-specific integration tests. |
| UI dependency licensing | Medium | High | Permissive licence review before adoption; avoid making proprietary component libraries mandatory. |
| OpenIddict/Identity complexity | Medium | High | Ship minimal v0.1 identity surface and extensibility seams; defer full SSO/SCIM/SAML admin workflows. |
| Localisation retrofit | Low | High | Foundation requirement in every first-party module from first UI/API strings. |
| Accessibility regression | Medium | High | Design-system primitives + automated CI + manual release sign-off. |
| Package sprawl | Medium | Medium | Package boundary requires dependency/deployment/ownership justification. |
| AI-generated architecture drift | Medium | High | Agent rules + deterministic architecture/security tests; accepted ADRs cannot be silently changed. |
| SQL Server assumptions block PostgreSQL | Medium | Medium | Keep provider-specific logic isolated; portability review for first-party module persistence. |
| Build duration / test cost | Medium | Medium | Layered CI; fast PR gates, heavier release matrix; real DB tests targeted by affected modules. |
| Name/trademark collision | Medium | Medium | Use Project Forge internally until public trademark/domain/package validation. |