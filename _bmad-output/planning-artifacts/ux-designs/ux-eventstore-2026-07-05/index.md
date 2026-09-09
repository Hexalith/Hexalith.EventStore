# Hexalith.EventStore UX Index

Status: draft update
Updated: 2026-09-09

This folder is the canonical UX source for Hexalith.EventStore. It is updated
against repository revision `e302432ca6daf3aa0436c3c0011f7baa551bb449`;
source digests and authority are in `EXPERIENCE.md`. The archived top-level UX handoff is retained only for audit
history at:

- ../../archive/ux-superseded-2026-07-05.md

The current top-level handoff is `../../ux.md`. The implementation target is
the existing `src/Hexalith.EventStore.Admin.UI`, evolved in place under
`eventstore-admin-ui`; “EventStore UI service” in older evidence never means a
second host.

## Canonical Documents

- [DESIGN.md](DESIGN.md) — visual identity, inheritance, and component visual rules.
- [EXPERIENCE.md](EXPERIENCE.md) — information architecture, behavior, states, interactions, accessibility, localization, and journeys.

`DESIGN.md` and `EXPERIENCE.md` win on conflict with mockups, screenshots,
validation artifacts, or legacy `Admin.UI` behavior. UX document finality never
authorizes implementation or readiness; the current PRD remains `blocked` /
`reject`.

## Reconciliation And Prior Validation

- [Latest-source reconciliation](reconcile-latest-sources-2026-09-09.md)
- [Prior validation report](validation-report.md)
- [Prior validation report (HTML)](validation-report.html)
- [Prior architecture-readiness review](review-architecture-readiness.md)
- [Prior accessibility and support-safety review](review-accessibility-support-safety.md)
- [Prior rubric review](review-rubric.md)

These reports describe the pre-update spines until the optional reviewer gate is
run again.

## Visual References

- [Fluent UI V5 desktop capture](imports/fluent-ui-v5-home-desktop.png)
- [Fluent UI V5 mobile capture](imports/fluent-ui-v5-home-mobile.png)
- [Dashboard overview mock](mockups/dashboard-overview.html)
- [Command investigation mock](mockups/command-investigation.html)

The screenshots and mockups are illustrative and non-copyable until regenerated
against current bindings. The canonical implementation contracts are
`DESIGN.md` and `EXPERIENCE.md`.
