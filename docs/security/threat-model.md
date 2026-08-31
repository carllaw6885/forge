# Threat model template

Copy this template into the module or feature's spec when it touches a trust boundary (ADR 18). Keep it short and current — a stale threat model is a defect.

## Scope

- **Component:**
- **Trust boundaries crossed:** (e.g. tenant ↔ tenant, tenant ↔ host, app ↔ external service)
- **Data classification handled:** (per ADR 09 primitives)

## Assets

| Asset | Why an attacker wants it |
|---|---|
| | |

## STRIDE analysis

| Threat | Applies? | Scenario | Mitigation (with test or gate reference) |
|---|---|---|---|
| Spoofing | | | |
| Tampering | | | e.g. audit hash chain (`forge audit verify`) |
| Repudiation | | | e.g. structured audit evidence (ADR 08) |
| Information disclosure | | | e.g. tenant query filters + negative isolation suite |
| Denial of service | | | e.g. request limits, rate-limit policy |
| Elevation of privilege | | | e.g. permission policies, impersonation auditing |

## Residual risk

- Accepted risks, with who accepted them and when.

## Verification

- Which deterministic tests or gates prove each mitigation. A mitigation without a test is an intention, not a control.
