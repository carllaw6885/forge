# Manual assistive-technology release checklist

Automated axe/Playwright checks gate CI; this checklist gates the release
(ADR 19). A release may not ship with a known first-party WCAG 2.2 AA failure.
Record the run (date, tester, tooling, outcomes) in the release notes.

## Screen reader (one of NVDA, VoiceOver, JAWS)

- [ ] Admin journey (sign in → dashboard → create catalog item → audit trail) is completable with the screen reader alone.
- [ ] The tenant banner is announced on page load; the impersonation banner is announced as an alert when active.
- [ ] Every form field announces its label; validation and status messages (`role="status"`) are announced without focus loss.
- [ ] Tables announce captions and column headers while navigating cells.

## Keyboard only (no pointer)

- [ ] Every interactive element is reachable and operable; no traps.
- [ ] Focus is always visible, including in dark theme.
- [ ] The skip link is the first tab stop and works.

## Vision

- [ ] 200% browser zoom: no loss of content or function, no horizontal scroll at 1280px.
- [ ] High-contrast/forced-colors mode: controls remain distinguishable.
- [ ] Light, dark and system themes checked for contrast on every surface.

## RTL

- [ ] ar-SA tenant: layout mirrors correctly (navigation, tables, forms); no clipped or overlapped text.

## Motion & time

- [ ] `prefers-reduced-motion` honoured (no essential animation; none should exist).
- [ ] No time-limited interactions without an extension mechanism.
