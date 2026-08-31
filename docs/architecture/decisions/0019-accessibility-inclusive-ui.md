# ADR 19: Accessibility & Inclusive UI

- Status: Accepted
- Baseline: v0.2

## Decision

All first-party UI and reusable components meet WCAG 2.2 AA, including keyboard operation, focus, semantics, screen readers, contrast, errors, reduced motion, responsive behaviour and RTL. Automated checks run in CI and manual assistive-technology testing gates releases.

## Consequences

Implementation must provide deterministic validation and tests proportionate to this decision. A contradiction requires a superseding ADR.
