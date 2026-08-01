# UX Handoff - Hexalith.EventStore Phase 4

Status: final
Updated: 2026-08-01

This file is the canonical top-level UX handoff expected by `prd.md`,
`architecture.md`, and `epics.md`.

The final UX source is the sharded artifact rooted at:

- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/index.md`

Canonical UX documents:

- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`

`DESIGN.md` and `EXPERIENCE.md` win on conflict with mockups, screenshots,
validation artifacts, older review findings, archived UX exports, or legacy
`Admin.UI` behavior.

Readiness note: this handoff exists to satisfy the top-level artifact path
required by the PRD, architecture, and epic plan while preserving the sharded
UX source as the detailed canonical contract.

The approved 2026-07-11 Parties parity correction expands the canonical
projection lifecycle UX to `Current`, `Stale`, `Rebuilding`, `Degraded`,
`Unavailable`, `LocalOnly`, and the fail-safe `Unknown` fallback.

The approved 2026-08-01 ownership correction makes the brownfield target
explicit: `src/Hexalith.EventStore.Admin.UI` evolves in place as the single
EventStore UI under resource/container identity `eventstore-admin-ui` and
FrontComposer module `event-store-admin`. No second UI host or duplicate page
implementation is created. Quantitative UI performance budgets remain a
documented non-blocking follow-up until measured production baselines exist.
