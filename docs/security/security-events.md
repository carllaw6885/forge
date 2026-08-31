# Security event taxonomy

Stable action names for security-relevant evidence (ADR 18). Constants live in `Forge.Security.SecurityEvents`; audit records use these names in `AuditEvent.Action`. Names are append-only — never rename a published action; add a new one and deprecate.

| Action | Emitted when | Required context |
|---|---|---|
| `security.login.succeeded` | An identity authenticates | actor, tenant, correlation |
| `security.login.failed` | Authentication fails | attempted actor, correlation; never the credential |
| `security.authorization.denied` | A permission check fails closed | actor, permission (in details), tenant, correlation |
| `security.impersonation.started` | Privileged impersonation begins | real actor (`ImpersonatorActor`), target, mandatory reason |
| `security.impersonation.ended` | Impersonation ends | same context as started |

Rules:

- Security events are audit evidence (`IAuditStore`), not just log lines; redaction applies (credentials and secrets never appear).
- Tenant-boundary violations detected at runtime are release-blocking defects, not merely events.
- New security-relevant capabilities add their taxonomy entries in the same PR.
