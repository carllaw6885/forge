# Phase 2 - Security and tenancy

**Window:** Weeks 7-11

**Exit goal:** Make tenant isolation, identity, permissions and immutable audit demonstrably safe.

## 2.1 Tenancy core

- [x] Implement ICurrentTenant and explicit host/tenant scope changes.
- [x] Implement trusted tenant resolution pipeline and deny-by-default missing/invalid tenant behaviour.
- [x] Implement EF tenant query filters for opted-in entities.
- [x] Add shared-database tenant isolation negative tests across API, cache key helper, events and jobs. (Jobs scenarios land with Quartz in Phase 3.)

## 2.2 Identity and permissions

- [x] Implement ASP.NET Core Identity + OpenIddict reference module.
- [x] Implement first-class permission definitions and role aggregation.
- [x] Implement permission policy provider / authorization handlers.
- [x] Implement visible, reasoned, audited impersonation context.
- [x] Create seams and data model for future SSO/SCIM/SAML without requiring full v0.1 delivery.

## 2.3 Audit evidence

- [x] Implement structured audit event model distinct from ILogger and entity history.
- [x] Implement append-only audit store abstraction with no update/delete methods.
- [x] Implement hash-chained tamper-evident records and forge audit verify.
- [x] Implement immutable evidence provider contract and local reference proving write-once semantics; production cloud WORM adapters may follow.
- [x] Implement redaction/exclusion policy and audit retention/export events.

## 2.4 Security baseline

- [x] Ship hardened ASP.NET defaults: HSTS production, secure cookies, antiforgery where relevant, safe CORS, request limits, rate-limit hooks and CSP-ready shell.
- [x] Add unsafe-production-configuration validators.
- [x] Add threat model template and security event taxonomy.
- [x] Gate release on authorization and tenant-boundary regression suites.
