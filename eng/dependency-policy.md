# Dependency licence and security policy

Applies to every new or upgraded package (ADRs 18, 21, 35). Review happens in the PR that introduces the dependency.

## Licence policy

- **Allowed:** Apache-2.0, MIT, BSD-2/3-Clause, ISC, MS-PL, Unlicense, CC0.
- **Case-by-case (maintainer approval recorded in the PR):** MPL-2.0, EPL, LGPL (dynamic linking only).
- **Forbidden:** GPL/AGPL, source-available/commercial-restricted (BUSL, SSPL, Elastic), any licence gating features behind payment. Forge itself is Apache-2.0 and must remain fully usable in proprietary software.

## Reviewed exceptions

Transitive packages whose metadata carries no SPDX licence id, reviewed and allowed by name in the CI licence gate:

| Package | Actual licence | Rationale |
|---|---|---|
| Microsoft.Identity.Client.NativeInterop | MIT (file-based metadata, no SPDX id) | Native transitive dependency of Microsoft.Data.SqlClient |
| Microsoft.Data.SqlClient.SNI.runtime | Microsoft redistributable licence file | Unavoidable native SNI component of the ADR-mandated SQL Server provider; free to use and redistribute |

Adding to this table requires the same review as the case-by-case licence tier.

## Security policy

- `dotnet list package --vulnerable --include-transitive` must be clean; CI fails on any known vulnerability. Fixes are upgrades, never suppressions.
- Secret scanning (gitleaks) and SBOM generation (CycloneDX, `.config/dotnet-tools.json`) run on every PR.
- Package versions are centrally pinned in `Directory.Packages.props` with transitive pinning enabled.

## Adoption bar

A new dependency needs all of:
1. A ladder check: not achievable with the BCL or an existing dependency in reasonable code.
2. Allowed licence (above) verified from the package metadata.
3. Active maintenance (release within the last year) or a documented exception.
4. No transitive pull of UI, EF providers, cloud SDKs or commercial components into core packages (enforced by `Forge.ArchitectureTests`).
