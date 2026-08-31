# ADR 06: Identity & Authorisation

- Status: Accepted
- Baseline: v0.2

## Decision

Forge builds on ASP.NET Core Identity, OpenIddict and standard authentication/authorisation. Permissions are first-class; roles aggregate permissions. External identity, MFA, passkeys, sessions, SSO, SCIM, SAML, service identities and audited impersonation are community capabilities.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
