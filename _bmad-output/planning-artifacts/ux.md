# UX Handoff — Hexalith.EventStore Phase 4

Status: final
Updated: 2026-09-09

This is the canonical top-level UX handoff expected by `prd.md`,
`architecture.md`, and `epics.md`. The detailed source is the sharded artifact
rooted at:

- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/index.md`

Canonical UX documents:

- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`

`DESIGN.md` and `EXPERIENCE.md` win on conflict with mockups, screenshots,
validation artifacts, older review findings, archived UX exports, or legacy
`Admin.UI` behavior. The current update reconciles repository revision
`23a722a1ffe29099a9d87df266552be4e3addd82` plus the input-snapshot digests recorded in
`EXPERIENCE.md`.

UX document finality is not implementation readiness. The current PRD is
`blocked` / `reject`; this handoff authorizes no implementation, release,
deployment, migration, or readiness claim.

FR4/NFR8 govern projection provenance and lifecycle: `Current`, `Stale`,
`Rebuilding`, `Degraded`, `Unavailable`, `LocalOnly`, and fail-safe `Unknown`.
FR36 governs consumer parity closure and does not define lifecycle semantics.

The brownfield target is `src/Hexalith.EventStore.Admin.UI`, evolved in place
under service/resource/DAPR/container identity `eventstore-admin-ui` and
FrontComposer module `event-store-admin`. No second UI host or duplicate page
implementation is created.

Quantitative UI performance budgets remain a documented non-blocking follow-up
until measured production baselines exist. The live `/types` route and its
events, commands, and aggregates views are canonical under Streams & Events.
