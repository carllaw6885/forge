# Phase 4 - Foundational enterprise services

**Window:** Weeks 17-22

**Exit goal:** Add enterprise primitives required by the v0.1 contract without pulling post-v0.1 scope forward.

## 4.1 Settings/secrets/flags

- [ ] Implement typed setting definitions, scope precedence and validation.
- [ ] Implement tenant-safe caching/invalidation for settings.
- [ ] Implement secret-store abstraction only; no secrets in ordinary settings.
- [ ] Implement operational flags distinct from entitlements.

## 4.2 Localisation/globalisation

- [ ] Implement application/tenant/user culture resolution.
- [ ] Implement time-zone resolution and deterministic display conversion.
- [ ] Implement module-owned resources, fallbacks and tenant/application overrides.
- [ ] Add en-GB plus one RTL acceptance culture and CI checks for missing first-party strings.

## 4.3 Privacy/classification

- [ ] Implement data classification primitives, retention classes and legal-hold flag model.
- [ ] Implement privacy contributor contract and acceptance demonstration.
- [ ] Ensure audit/storage/template paths respect classification metadata.
- [ ] Do not build the full GDPR workbench in v0.1.

## 4.4 Storage pipeline

- [ ] Implement provider-neutral storage and local reference provider.
- [ ] Validate size/type, quarantine before trust, record SHA-256 hash and classification metadata.
- [ ] Add pluggable malware-scan contract and deterministic fake/reference scanner for acceptance.
- [ ] Implement authorized private access path; no permanent public URLs.

## 4.5 Notifications/templates

- [ ] Implement notification intents, preferences, policy override and durable delivery state.
- [ ] Implement constrained template rendering with allow-listed variables, localisation and sanitisation.
- [ ] Demonstrate one in-app/email-style provider adapter without coupling Core to vendor delivery.
- [ ] Audit security-critical delivery and template lifecycle changes.

## 4.6 Redis adapter

- [ ] Implement optional Redis distributed cache provider and tenant-safe key conventions.
- [ ] Keep cache failure degradable where authoritative source exists.
- [ ] Do not make Redis mandatory for single-instance v0.1 reference execution.
