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

Modules evolve toward three layers, packaged so that nothing is assumed beyond the headless capability:

```text
Forge.Identity            capability (headless) — includes the module's application contract
Forge.Identity.Api        optional HTTP projection of that contract; added when a remote consumer exists
Forge.Identity.Ui.Blazor  optional first-party UI
```

- **Application contract (required, in the capability package).** Every module with a first-party UI defines the operations it exposes as plain interfaces inside the capability, with authorisation, tenant scope and auditing enforced inside them. This is the single front door: an in-process UI, an HTTP endpoint and a custom application all pass through the same checks. It is plain .NET, not a mediator or CQRS framework (ADR 30 spirit).
- **`.Ui.Blazor` consumes only the application contract**, never stores or contexts directly (architecture test).
- **`.Api` is a thin, optional projection** of the same contract using Minimal APIs. It exists for modules with a real remote consumer (mobile, another service, a non-Blazor frontend, interactive WebAssembly render modes) and is not required for the monolith to work — extraction enabled, never assumed. Starters do not include `.Api` packages by default. A capability whose HTTP surface is inseparable from it (Identity's OpenIddict endpoints) keeps that surface in the capability.

Published package IDs carry the `ForgeStack.` prefix (assemblies and namespaces stay `Forge.*`). Package count is a cost against "explicit and inspectable": a new package must create a genuine boundary, never a convention.

Applications may:

- use Forge capability + Forge UI;
- use Forge capability + custom UI;
- mix Forge and custom UIs module by module;
- run API/headless only.

A first-party UI is complete only when the corresponding capability remains independently usable without it. Architecture tests enforce that no capability package references its UI package.

The product is described as four composable layers: platform → headless capabilities → optional first-party module UIs → starter applications. The administration shell (ADR 37) remains the composition host for administration surfaces but is not the end-state product experience.

Engineering rules for first-party UI and the starter strategy live in `docs/POST_V0.1_ROADMAP.md`; `AGENTS.md` carries the enforceable subset.
