# UX Handoff — Hexalith.EventStore Phase 4

Status: draft
Updated: 2026-10-07

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
`c60503c13069fde13f329f2166ff39c90c3661c4` plus the input-snapshot digests recorded in
`EXPERIENCE.md`.

UX document finality is not implementation readiness. The current PRD is
`blocked` / `reject`; this handoff authorizes no implementation, release,
deployment, migration, or readiness claim.

FR4/NFR8 govern projection provenance and lifecycle: `Current`, `Stale`,
`Rebuilding`, `Degraded`, `Unavailable`, `LocalOnly`, and fail-safe `Unknown`.
FR36 governs consumer parity closure and does not define lifecycle semantics.

The [2026-10-07 reconciliation](ux-designs/ux-eventstore-2026-07-05/reconcile-current-sources-2026-10-07.md) updates the detailed pair against current product and architecture authority. It retains the ten-tab information architecture and Overview/Commands visual references; adds the gated Create Tenant flow required by Story 5.10; and assigns legacy restore/import control removal to Stories 7.4/7.14 under the existing deferred-operation policy. It also aligns command identifiers, projection-version equality, human authorization relay, notification failures, evolution outcomes, and safe release identity.

`/projections`, `/dapr`, `/services`, and `/health` retain their evidence-based behavior, including `Unknown` fail-safe handling. Reconnect/rejoin fetches authoritative typed evidence; notification delivery, absence, loss, duplicates, or ordering never establishes freshness or completion. Story 2.13 retains cross-replica authorization and group-isolation evidence. See [PRD §8.4](prd.md#84-dapr-infrastructure-boundary) and the [boundary guide](../../docs/concepts/dapr-infrastructure-boundary.md).

The `/types` manifest omission and PRD/architecture NFR1 exemption conflict remain upstream reconciliation items. Existing epics UX digests are historical bindings; this update does not repin them. OR14 acceptance and Story 9.3 renew those bindings after substantive reconciliation and approval. No release, production UI eligibility, or readiness gate is closed by UX document finality.

The brownfield target is `src/Hexalith.EventStore.Admin.UI`, evolved in place
under service/resource/DAPR/container identity `eventstore-admin-ui` and
FrontComposer module `event-store-admin`. No second UI host or duplicate page
implementation is created.

Quantitative UI performance budgets remain a documented non-blocking follow-up
until measured production baselines exist. The live `/types` route and its
events, commands, and aggregates views are canonical under Streams & Events.
