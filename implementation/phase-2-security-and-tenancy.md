# Phase 2 - Security and tenancy

**Window:** Weeks 7-11

**Exit goal:** Make tenant isolation, identity, permissions and immutable audit demonstrably safe.

## 2.1 Tenancy core

- [x] Implement ICurrentTenant and explicit host/tenant scope changes.
- [x] Implement trusted tenant resolution pipeline and deny-by-default missing/invalid tenant behaviour.
- [x] Implement EF tenant query filters for opted-in entities.
- [x] Add shared-database tenant isolation negative tests across API, cache key helper, events and jobs. (Jobs scenarios land with Quartz in Phase 3.)

## 2.2 Identity and permissions

- [ ] Implement ASP.NET Core Identity + OpenIddict reference module.
- [ ] Implement first-class permission definitions and role aggregation.
- [ ] Implement permission policy provider / authorization handlers.
- [ ] Implement visible, reasoned, audited impersonation context.
- [ ] Create seams and data model for future SSO/SCIM/SAML without requiring full v0.1 delivery.

## 2.3 Audit evidence

- [ ] Implement structured audit event model distinct from ILogger and entity history.
- [ ] Implement append-only audit store abstraction with no update/delete methods.
- [ ] Implement hash-chained tamper-evident records and forge audit verify.
- [ ] Implement immutable evidence provider contract and local reference proving write-once semantics; production cloud WORM adapters may follow.
- [ ] Implement redaction/exclusion policy and audit retention/export events.

## 2.4 Security baseline

- [ ] Ship hardened ASP.NET defaults: HSTS production, secure cookies, antiforgery where relevant, safe CORS, request limits, rate-limit hooks and CSP-ready shell.
- [ ] Add unsafe-production-configuration validators.
- [ ] Add threat model template and security event taxonomy.
- [ ] Gate release on authorization and tenant-boundary regression suites.
