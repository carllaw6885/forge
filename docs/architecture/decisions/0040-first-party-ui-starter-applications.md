# ADR 40: First-Party UI & Starter Applications

- Status: Accepted
- Baseline: post-v0.1 (accepted 1 September 2026, after completion of v0.1 Phases 0–5)

## Context

Forge is intentionally headless at the capability boundary. v0.1 ships a reference administration shell (ADR 37) with selected administration surfaces, but no product-level user interface: a developer adopting Forge still builds sign-in, account, tenant and operational screens before they have a usable application.

To compete with platforms such as ABP, developers must be able to adopt Forge capabilities without building every user interface themselves. At the same time, first-party UI must remain optional and replaceable so teams can use Forge with their own Blazor, React, mobile or other experiences.

## Decision

Forge remains headless at its capability boundary. Major first-party modules additionally provide **optional, production-quality reference UI implementations**.

First-party UI packages:

- are independently consumable;
- never become dependencies of the underlying capability;
- consume the same public APIs/contracts available to custom applications;
- are localisation/globalisation aware (ADR 12);
- meet the WCAG 2.2 AA gate (ADR 19);
- respect tenant, permission, entitlement and impersonation context (ADRs 05, 06, 07);
- use the Forge design system and UI extension contracts (ADR 37);
- are production-quality reference implementations, not demo or sample screens.

Forge also provides complete **starter applications** that compose first-party module UIs into usable SaaS and enterprise application experiences.

Blazor Web App is the initial first-party UI implementation. Additional UI stacks may be introduced later without changing underlying capability contracts.

## Consequences

Modules should normally evolve toward a package shape similar to:

```text
Forge.Identity            capability (headless)
Forge.Identity.Api        HTTP surface, where a separate package is a genuine boundary
Forge.Identity.Ui.Blazor  optional first-party UI
```

Split packaging is used only where a separate package creates a genuine boundary; a capability whose endpoints are inseparable from the capability keeps them in one package. Published package IDs carry the `ForgeStack.` prefix (assemblies and namespaces stay `Forge.*`).

Applications may:

- use Forge capability + Forge UI;
- use Forge capability + custom UI;
- mix Forge and custom UIs module by module;
- run API/headless only.

A first-party UI is complete only when the corresponding capability remains independently usable without it. Architecture tests enforce that no capability package references its UI package.

The product is described as four composable layers: platform → headless capabilities → optional first-party module UIs → starter applications. The administration shell (ADR 37) remains the composition host for administration surfaces but is not the end-state product experience.

Engineering rules for first-party UI and the starter strategy live in `docs/POST_V0.1_ROADMAP.md`; `AGENTS.md` carries the enforceable subset.
