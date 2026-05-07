# Sprint Change Proposal — Carve Group A Out of admin-ui-manual-test-bug-bundle

**Date:** 2026-05-07
**Triggered by:** Dev-agent scope review of in-progress story `admin-ui-manual-test-bug-bundle`. Architectural depth of Group A (Issue #5, aggregate state replay correctness) surfaced after reading the runtime replay surface (`Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs`, `Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs`, `samples/Hexalith.EventStore.Sample/DomainServiceRequestRouter.cs`).
**Decision authority:** User (Jerome), 2026-05-07. Authorized "Option B — Group A in a separate story; do B+C+D now." in dev-agent escalation.
**Predecessor proposal:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-06-admin-ui-manual-test-bug-bundle.md` (recommended split into four stories; bundled by user direction at story creation; this proposal partially restores that recommended split).
**Mode:** Scope reduction of an in-progress story plus creation of a follow-up story. No requirements change. No AC change in substance — Group A AC moves verbatim to the new story.

---

## Why a carve-out, not a continued bundle

Three observations from the dev pre-implementation review:

1. **Open Question 1 in the source proposal is genuinely architectural.** The "Fluent Convention" aggregate-discovery surface (`DomainProcessorStateRehydrator.DiscoverApplyMethods`) lives in `Hexalith.EventStore.Client`, which is referenced by the **domain-service process** (e.g. the Counter Sample), not by the EventStore process. The EventStore process has no `CounterState` type and no `Apply` methods to invoke directly. Per ADR-1 in the source proposal the explicit preferred answer is **"Domain-owned replay service"**. Concretely that means a new domain-service contract method (e.g. `POST /replay-state`), a new `IAggregateStateReconstructor` in `Hexalith.EventStore.Server` that DAPR-invokes that method, and a new replay handler in `EventStoreAggregate<TState>` and `DomainServiceRequestRouter`. This is a real cross-cutting contract change, not a controller refactor.

2. **The bundle scope was already flagged for splitting.** The original proposal recommended four separate stories. The bundled story's own QA Conditions explicitly reserve a split seam: *"If reviewer feedback requests a split, prefer Group A as PR 1 and Groups B/C/D as PR 2."* This carve-out matches that seam.

3. **Group A reviewer surface is structurally different from B/C/D.** Group A reviews need to validate a domain-service contract change, the ServiceDefaults wiring, the Tier-2/3 fixture, and the failure-semantics matrix. Groups B/C/D reviews validate UI behavior and a metrics-source aggregator. Mixed reviews of unrelated concerns dilute attention.

The user's selection of Option B authorizes the carve-out and accepts the consequence: manual verification (ST11) cannot fully clear until Group A also lands.

---

## Carve-out summary

| Item | Stays in `admin-ui-manual-test-bug-bundle` | Moves to `admin-ui-aggregate-state-replay-correctness` |
|---|---|---|
| Group A — Aggregate State Replay Correctness (AC #1, #2, #3) | — | ✅ verbatim |
| Group B — Authorized Tenant Discovery (AC #4, #5) | ✅ | — |
| Group C — Truthful Dashboard Metrics (AC #6, #7) | ✅ | — |
| Group D — Copy Click Isolation (AC #8) | ✅ | — |
| AC #9 Negative evidence states | ✅ (B/C/D scope) + ✅ (Group A scope) | shared cross-cutting |
| AC #10 Issue #4 deferred guardrail | ✅ | — |
| AC #11 Automated regression coverage | ✅ (B/C/D scope) | ✅ (Group A scope) |
| AC #12 Manual verification evidence | partial (B/C/D) | partial (Group A) — full pass after both stories |
| Tasks ST1, ST2, ST3 | — | ✅ verbatim |
| Tasks ST4, ST5, ST6, ST7, ST8, ST9, ST10, ST11 | ✅ | — |
| Failure Semantics Matrix | — | ✅ |
| Canonical Fixture and Checkpoint Table | — | ✅ |
| Metric Contract table | ✅ | — |
| `OnRowClick` audit table | ✅ | — |

The new Group A story carries its own ST11 manual-Aspire-smoke clause for the Group A surface. The bundle story's ST11 stays as written but its acceptance is restricted to the B/C/D surface.

## Manual verification timing

The manual-test guide can fully unblock only after **both** stories ship. After the bundle (this carved-down version) ships, manual testers can verify tenant filters, dashboard metric truthfulness, and copy isolation. The state-inspection cluster (Step Through, Blame, StateDiff, Bisect, Sandbox, CausationChainView) remains visibly broken until `admin-ui-aggregate-state-replay-correctness` ships.

This is documented as **expected residual risk** for the bundle's `review` transition. Reviewers should not block the bundle on Group A evidence — that evidence belongs to the new story.

## Sprint-status changes

- `admin-ui-manual-test-bug-bundle`: stays `in-progress`. Last-updated comment notes the carve-out.
- `admin-ui-aggregate-state-replay-correctness`: added as `ready-for-dev` immediately below the bundle row in the OPEN-cleanup section. Created with full Group A context inherited from this proposal and the predecessor proposal.

## Out of scope for both stories

Unchanged from the predecessor proposal: no Aspire/DAPR infrastructure changes, no Tenant service contract changes, no auto-registration of tenants, no production-grade metrics redesign, no timeline type-name filter, no work on `/dapr*`, `/services`, `/tenants`, `/storage`, `/snapshots`, `/compaction`, `/backups`, `/consistency`, `/settings`.

## Acceptance of this proposal

This proposal is implicitly accepted by the user's selection of "B" in the dev-agent escalation on 2026-05-07. No further authorization required. Dev agent proceeds with:

1. Creating `admin-ui-aggregate-state-replay-correctness` story file with full Group A context.
2. Updating `_bmad-output/implementation-artifacts/sprint-status.yaml` with the new row and a last-updated comment.
3. Narrowing `admin-ui-manual-test-bug-bundle` to remove ST1, ST2, ST3 and the Group A AC scope (leaving an explicit pointer to the new story).
4. Implementing ST4–ST10 in `admin-ui-manual-test-bug-bundle`.
5. Marking `admin-ui-manual-test-bug-bundle` as `review` once ST4–ST10 are complete and ST11 is handed off to the operator.
