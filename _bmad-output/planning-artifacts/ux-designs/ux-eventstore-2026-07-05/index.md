# Hexalith.EventStore UX Index

Status: draft
Updated: 2026-10-07

This folder is the canonical UX source for Hexalith.EventStore. It is updated
against repository revision `c60503c13069fde13f329f2166ff39c90c3661c4`
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

The current update is recorded in [Current-source reconciliation](reconcile-current-sources-2026-10-07.md). It preserves the inherited visual direction and approved mock set while aligning the detailed contracts with current source authority. Upstream `/types` route-manifest repair, the NFR1 architecture conflict, OR14 acceptance, and Story 9.3 digest renewal remain open; detailed document finality does not close them.

Prior lens reviews are historical evidence; superseded bodies are recoverable from Git history.

- [Latest-source reconciliation](reconcile-latest-sources-2026-09-09.md) — historical 2026-09-09 update
- [Source-safety reconciliation](reconcile-source-safety-update-2026-09-09.md) — historical draft assumptions superseded by the current reconciliation
- [Validation report](validation-report.md) — regenerated 2026-09-09; grades the 2026-08-01 `status: final` spines
- [Validation report (HTML)](validation-report.html) — regenerated 2026-09-09
- [Architecture-readiness review](review-architecture-readiness.md) — regenerated 2026-09-09; carries forward the code-verified findings of the 2026-09-08 lens
- [Accessibility and support-safety review](review-accessibility-support-safety.md) — regenerated 2026-09-09
- [Rubric review](review-rubric.md) — regenerated 2026-09-09

These reports describe earlier spines. Fresh optional multi-lens validation has not run for the 2026-10-07 update; the reports remain historical review evidence rather than a verdict on the current pair.

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
