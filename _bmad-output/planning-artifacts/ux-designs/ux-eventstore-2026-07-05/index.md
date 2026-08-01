# Hexalith.EventStore UX Index

Status: final
Updated: 2026-08-01

This folder is the canonical UX source for Hexalith.EventStore. The archived
top-level UX handoff is retained only for audit history at:

- ../../archive/ux-superseded-2026-07-05.md

The current top-level handoff is `../../ux.md`. The implementation target is
the existing `src/Hexalith.EventStore.Admin.UI`, evolved in place under
`eventstore-admin-ui`; “EventStore UI service” in older evidence never means a
second host.

## Canonical Documents

- [DESIGN.md](DESIGN.md) - visual identity, tokens, component visual rules.
- [EXPERIENCE.md](EXPERIENCE.md) - information architecture, behavior, states, interactions, accessibility, localization, and journeys.

`DESIGN.md` and `EXPERIENCE.md` win on conflict with mockups, screenshots,
validation artifacts, or legacy `Admin.UI` behavior.

## Audit And Validation

- [validation-report.md](validation-report.md)
- [validation-report.html](validation-report.html)
- [review-architecture-readiness.md](review-architecture-readiness.md)
- [review-accessibility-support-safety.md](review-accessibility-support-safety.md)
- [review-rubric.md](review-rubric.md)

## Visual References

- [Fluent UI V5 desktop capture](imports/fluent-ui-v5-home-desktop.png)
- [Fluent UI V5 mobile capture](imports/fluent-ui-v5-home-mobile.png)
- [Dashboard overview mock](mockups/dashboard-overview.html)
- [Command investigation mock](mockups/command-investigation.html)

The screenshots and mockups are illustrative. The canonical implementation
contracts are `DESIGN.md` and `EXPERIENCE.md`.
