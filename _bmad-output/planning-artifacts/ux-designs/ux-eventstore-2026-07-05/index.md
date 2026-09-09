# Hexalith.EventStore UX Index

Status: final
Updated: 2026-09-09

This folder is the canonical UX source for Hexalith.EventStore. It is updated
against repository revision `23a722a1ffe29099a9d87df266552be4e3addd82`
plus current input-snapshot digests; source authority is in `EXPERIENCE.md`. The archived top-level UX handoff is retained only for audit
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

## Reconciliation And Validation

Lens reviews in this folder are regenerated in place; superseded bodies are recoverable from Git history.

- [Latest-source reconciliation](reconcile-latest-sources-2026-09-09.md) — 2026-09-09
- [Validation report](validation-report.md) — regenerated 2026-09-09; grades the 2026-08-01 `status: final` spines
- [Validation report (HTML)](validation-report.html) — regenerated 2026-09-09
- [Architecture-readiness review](review-architecture-readiness.md) — regenerated 2026-09-09; carries forward the code-verified findings of the 2026-09-08 lens
- [Accessibility and support-safety review](review-accessibility-support-safety.md) — regenerated 2026-09-09
- [Rubric review](review-rubric.md) — regenerated 2026-09-09

These reports describe the pre-update spines. The user skipped a fresh optional
multi-lens validation when finalizing this update, so they are retained as
historical review evidence rather than a verdict on the final spines.

## Visual References

- [Fluent UI V5 desktop capture](imports/fluent-ui-v5-home-desktop.png)
- [Fluent UI V5 mobile capture](imports/fluent-ui-v5-home-mobile.png)
- [Dashboard overview mock](mockups/dashboard-overview.html)
- [Dashboard overview desktop render](mockups/dashboard-overview.png)
- [Dashboard overview mobile render](mockups/dashboard-overview-mobile.png)
- [Command investigation mock](mockups/command-investigation.html)
- [Command investigation desktop render](mockups/command-investigation.png)
- [Command investigation mobile render](mockups/command-investigation-mobile.png)

The screenshots and mockups are illustrative, current composition references
and remain non-copyable. The canonical implementation contracts are
`DESIGN.md` and `EXPERIENCE.md`.
