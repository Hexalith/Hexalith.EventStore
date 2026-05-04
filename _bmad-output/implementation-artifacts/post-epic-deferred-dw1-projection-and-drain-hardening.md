# Post-Epic Deferred DW1: Projection and Drain Hardening

Status: ready-for-dev

<!-- Source: sprint-change-proposal-2026-05-04-deferred-work-triage.md - Proposal B / DW1 -->
<!-- Source: deferred-work.md - projection, polling, checkpoint, and drain deferrals through 2026-05-04 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an EventStore reliability maintainer,
I want projection delivery, projection tracking, and drain recovery hardening to be closed from the deferred-work backlog,
so that projection freshness and event-drain recovery fail visibly and recover predictably instead of depending on scattered review notes.

## Story Context

`deferred-work.md` accumulated recurring operational risks around projection checkpoints, projection poller enumeration, `/project` invocation diagnostics, tracker scaling, corrupt drain records, and drain failure signals. The deferred-work triage proposal groups those risks into DW1 because they affect correctness and recovery confidence across Epics 4, 9, 10, and 11.

This story is not a rewrite of the projection builder or event distribution model. The current implementation already has per-aggregate projection serialization via `ProjectionUpdateOrchestrator.ProjectionLocks`, Dapr-backed checkpoint persistence in `ProjectionCheckpointTracker`, immediate and polling delivery modes, and integrity checks for incomplete drain ranges. DW1 should close the remaining bounded hardening gaps without changing product scope, DAPR topology, public query contracts, SignalR semantics, or admin tooling.

Current HEAD at story creation: `2846d419`.

## Acceptance Criteria

1. **Projection checkpoint drift is handled explicitly.** Given a persisted projection checkpoint whose `LastDeliveredSequence` is greater than the aggregate actor's current highest event sequence, when `ProjectionUpdateOrchestrator.DeliverProjectionAsync` reads events for that aggregate, then the behavior is deterministic and documented in code or tests. The handler must not silently report projection success for an impossible checkpoint/stream pairing, must emit a stable operator signal with tenant/domain/aggregate and the checkpoint/event-sequence values, and must leave future delivery able to recover once state is repaired or the chosen policy is applied.

2. **`/project` invocation failures are classified with useful diagnostics.** Given a domain service `/project` response returns 4xx, 5xx, unsupported content type, invalid charset, malformed JSON, null/empty `ProjectionType`, or null/undefined/string-empty state, when projection delivery runs, then the failure path logs a stable stage/reason code and enough non-payload metadata to identify tenant, domain, aggregate, app-id, HTTP status, content type, and exception type where available. Do not log event payload data.

3. **Cancellation and timeout semantics are separated from ordinary failures.** Given the projection delivery call is canceled by the host token, when cancellation is observed, then cancellation propagates or is recorded as host shutdown according to the current caller contract. Given an inner timeout or service-invocation timeout happens while the host is still running, then it is treated as a transient projection delivery failure with a stable reason code, not as a process shutdown signal.

4. **Per-aggregate projection serialization remains non-regressive.** Given immediate triggers, polling triggers, or repeated same-aggregate deliveries overlap, when they target the same `AggregateIdentity.ActorId`, then projection state writes and checkpoint saves remain serialized per aggregate. The implementation must not introduce a global lock, must not remove `KeyedSemaphore` eviction, and must include regression coverage for duplicate or overlapping same-aggregate delivery attempts.

5. **Tracker enumeration corruption is bounded and observable.** Given persisted projection identity scope/page/index state is missing, null, overfull, stale, or internally inconsistent, when the poller enumerates tracked identities, then one corrupt page or scope must not produce an unbounded tight retry loop or hide the fault indefinitely. The implementation must preserve existing page-size and ETag retry behavior, emit a stable operator signal for unrecoverable tracker corruption, and include tests for at least one scope-index and one identity-index corruption path.

6. **Tracker scaling limits are documented and not worsened.** Given `ProjectionCheckpointTracker.EnumerateTrackedIdentitiesAsync` re-reads scope indexes, scope pages, identity indexes, and identity pages on each polling tick, when DW1 closes, then the story implementation records the current scaling model and any accepted limitation. Do not add a broad caching layer unless the code change is narrow, tested, and cannot regress consistency. If scaling remains deferred, add a precise deferred entry with trigger thresholds such as identity count, page count, and polling interval.

7. **Drain poison or terminal disposition is decided before code changes.** Given a corrupt `UnpublishedEventsRecord` whose `EventCount` does not match its sequence range or whose persisted event keys cannot be recovered, when a drain reminder fires repeatedly, then the story must either implement a terminal disposition/backoff policy or record an explicit accepted-debt decision with rationale. If implemented, the policy must preserve at-least-once delivery guarantees, must not remove a drain record before the complete range is published or deliberately marked terminal, and must not decrement `pending_command_count` on integrity failure.

8. **Drain activity failure reasons use stable codes.** Given drain failure activity tags are emitted, when failure details include tenant/domain/aggregate IDs or exception messages, then `eventstore.failure_reason` or its replacement must use a bounded stable reason code. Full exception text may remain in structured logs when safe, but high-cardinality actor IDs and payload-adjacent values must not be stored as activity tag values.

9. **Drain reminder re-entrancy remains safe by evidence.** Given Dapr actor reminders respect actor turn-based concurrency, when DW1 closes, then the implementation must either prove the existing reminder path cannot concurrently publish/remove the same drain record under current Dapr actor semantics or add a narrow guard. The proof must reference the Dapr actor reminder behavior and current `DrainUnpublishedEventsAsync` side effects.

10. **EventId and operational signal collisions are not made worse.** Given existing projection logs use the 11xx range and previous review noted an EventId collision, when adding new logs or reason codes, then new EventIds must be unique in the touched files and reason-code names must be stable enough for dashboards. If a project-wide EventId allocation table is out of scope, record that as deferred instead of mixing unrelated renumbering into DW1.

11. **Tests cover production behavior, not only helper internals.** Add or update focused tests in the smallest relevant projects. Expected test areas include `ProjectionUpdateOrchestratorTests`, `ProjectionUpdateOrchestratorRefreshIntervalTests`, `ProjectionCheckpointTrackerTests`, `ProjectionPollerServiceTests`, and `EventDrainRecoveryTests`. Integration tests under `tests/Hexalith.EventStore.IntegrationTests` are required only if the selected change affects runtime Dapr/Aspire behavior that cannot be proven with Tier 2 tests.

12. **Scope boundaries stay intact.** DW1 must not add admin endpoints, change public projection/query contracts, change SignalR group semantics, create a new global drain registry, change Dapr component YAML, initialize nested submodules, or replace Dapr resiliency with custom service-invocation retry loops. Any pressure to do those things must be recorded as deferred product or architecture work.

13. **Bookkeeping is closed.** At dev handoff, update this story's Dev Agent Record, File List, Change Log, Verification Status, and any new deferred-work dispositions. Move this story and its sprint-status row to `review` only after targeted tests and documentation updates are recorded. Move both to `done` only after code review signoff.

## Scope Boundaries

- Do not re-implement the projection builder architecture from Epic 11.
- Do not change query response contracts, ETag format, query actor routing, or SignalR notification semantics.
- Do not add broad performance infrastructure, load tests, or benchmark projects.
- Do not introduce application-level retry loops around Dapr service invocation or pub/sub publishing; use Dapr resiliency and existing actor/poller retry mechanisms.
- Do not add admin APIs, manual drain controls, or projection reset controls in this story.
- Do not weaken existing R4-A6 drain integrity checks, R11-A1 checkpoint non-regression rules, or R11-A2 polling fairness rules.
- Do not log event payload data or customer identifiers in activity tags.
- Do not edit generated preflight JSON audit files.

## Implementation Inventory

| Area | File / artifact | Expected use |
|---|---|---|
| Planning source | `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md` | DW1 scope, priority, and acceptance direction |
| Deferred source | `_bmad-output/implementation-artifacts/deferred-work.md` | Raw review deferrals to close or explicitly carry forward |
| Projection delivery | `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` | `/project` invocation, response validation, projection actor write, checkpoint save, per-aggregate lock |
| Checkpoint tracker | `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs` | Dapr checkpoint read/save, identity tracking, page/index enumeration |
| Polling delivery | `src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs` | polling tick scheduling, active identity guard, enumeration failure behavior |
| Locking helper | `src/Hexalith.EventStore.Server/Projections/KeyedSemaphore.cs` | per-key serialization and eviction contract |
| Drain recovery | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` | `DrainUnpublishedEventsAsync`, `pending_command_count`, reminder handling, activity tags |
| Drain record | `src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs` | persisted drain state, retry count, last failure reason |
| Projection tests | `tests/Hexalith.EventStore.Server.Tests/Projections/*.cs` | focused Tier 2 projection, tracker, poller, and lock regression tests |
| Drain tests | `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs` | drain integrity, poison, reminder, pending-counter, and activity-signal tests |
| Runtime projection tests | `tests/Hexalith.EventStore.IntegrationTests/ContractTests/ProjectionMalformedResponseE2ETests.cs` and `ValidProjectionRoundTripE2ETests.cs` | use only when runtime Dapr/Aspire behavior must be proven |

## Current Code Intelligence

- `ProjectionUpdateOrchestrator.DeliverProjectionAsync` currently reads the full event stream with `GetEventsAsync(0)` before invoking `/project`. This is safe for full-replay handlers but means checkpoint drift must be detected separately if DW1 chooses to use checkpoint state as evidence.
- `/project` invocation currently calls `EnsureSuccessStatusCode()` and `ReadFromJsonAsync<ProjectionResponse>()`; many failures collapse into `ProjectionUpdateFailed` unless the response reaches the existing invalid-response branches.
- The orchestrator writes the projection actor before attempting `SaveDeliveredSequenceAsync`. A checkpoint save failure must remain non-regressive: the projection may be fresh, but the checkpoint must not move backwards or hide retry exhaustion.
- `ProjectionCheckpointTracker` already validates identity fields on read/save/track, uses three ETag retries, stores checkpoints under `projection-checkpoints:{actorId}`, and pages tracked identities with page size 100.
- `ProjectionPollerService` already prevents same-process overlap with `_activeIdentities`, caps delivered identities per tick at 100, and advances known domains after enumeration failure to avoid tight retry storms.
- `AggregateActor.DrainUnpublishedEventsAsync` currently rejects `EventCount` versus range mismatch before publish, preserves records and reminder state on failure, decrements `pending_command_count` only after successful drain, and stores raw exception or publisher failure text in `eventstore.failure_reason`.

## Latest Technical Notes

- Dapr actor reminders are persisted and continue across deactivation/failover until explicitly removed or invocation limits are exhausted. Dapr also states actor timer/reminder callbacks respect turn-based actor concurrency. Use this to justify reminder re-entrancy decisions, but still pin EventStore side effects with tests when code changes. Source: <https://docs.dapr.io/developing-applications/building-blocks/actors/actors-timers-reminders/>
- Dapr service invocation returns the upstream status code when the called service responds and can return `500` for network/transient failures. DW1 diagnostics should therefore distinguish upstream 4xx/5xx from Dapr/runtime transport failures. Source: <https://docs.dapr.io/reference/api/service_invocation_api/>
- Dapr state management supports optimistic concurrency with ETags; keep tracker writes ETag-guarded and do not replace failed ETag saves with blind writes. Source: <https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/>

## Tasks / Subtasks

- [ ] Task 0: Baseline and choose the minimum safe policy set (AC: #1, #7, #12)
    - [ ] 0.1 Re-read the DW1 section in the deferred-work triage proposal and the relevant `deferred-work.md` entries.
    - [ ] 0.2 Classify each selected deferred item as `patch-now`, `decision-now`, `accepted-debt`, `duplicate`, or `not-DW1`.
    - [ ] 0.3 Record any architecture/product decisions before editing production code.
    - [ ] 0.4 Confirm no public API, query contract, SignalR, admin endpoint, or Dapr component change is needed.

- [ ] Task 1: Harden projection delivery diagnostics (AC: #1, #2, #3, #10)
    - [ ] 1.1 Add focused tests for `/project` upstream 4xx, upstream 5xx, malformed JSON, unsupported or missing content type if supported by the test seam, empty `ProjectionType`, and invalid `State`.
    - [ ] 1.2 Add stable reason-code logging for each failure class without logging payload data.
    - [ ] 1.3 Separate host cancellation from service timeout/transient invocation failure.
    - [ ] 1.4 Add a checkpoint-drift test or decision record covering checkpoint greater than aggregate event sequence.

- [ ] Task 2: Preserve projection serialization and checkpoint non-regression (AC: #1, #4, #11)
    - [ ] 2.1 Add or update same-aggregate overlap tests that prove the per-aggregate lock serializes delivery.
    - [ ] 2.2 Assert checkpoint saves never move backwards and failed saves leave a useful operator signal.
    - [ ] 2.3 Avoid global locks or static caches beyond the existing `KeyedSemaphore` contract.

- [ ] Task 3: Harden tracker enumeration and scaling notes (AC: #5, #6, #10, #11)
    - [ ] 3.1 Add scope-index corruption coverage and identity-index corruption coverage.
    - [ ] 3.2 Assert corrupt or missing pages do not create tight retry loops.
    - [ ] 3.3 Preserve ETag-guarded page/index writes and page size 100.
    - [ ] 3.4 Document any still-deferred scaling limits with concrete thresholds and owner.

- [ ] Task 4: Resolve drain poison and activity signal gaps (AC: #7, #8, #9, #11)
    - [ ] 4.1 Decide whether corrupt drain records get terminal disposition, bounded backoff, or accepted-debt status.
    - [ ] 4.2 If code changes, add tests proving no partial publish, no premature drain removal, no pending-counter decrement on failure, and reminder behavior remains intentional.
    - [ ] 4.3 Replace high-cardinality activity failure text with stable reason codes while keeping useful structured logs.
    - [ ] 4.4 Capture Dapr reminder semantics in the Dev Agent Record or code comments only where needed.

- [ ] Task 5: Validate and close bookkeeping (AC: #11, #13)
    - [ ] 5.1 Run targeted projection and drain tests individually.
    - [ ] 5.2 Run the four Tier 1 unit test projects individually if production code changed.
    - [ ] 5.3 If Tier 3 runtime behavior changed, run the affected integration tests with the Aspire/Dapr prerequisites recorded.
    - [ ] 5.4 Update this story's Dev Agent Record, File List, Change Log, Verification Status, and any deferred-work dispositions.

## Dev Notes

### Architecture Guardrails

- Event store keys remain write-once for events. DW1 may inspect state and checkpoint records, but must not mutate event keys or compensate by deleting/re-writing events.
- Dapr actor state operations inside actors must continue to use `IActorStateManager`; do not bypass with `DaprClient` from `AggregateActor`.
- Dapr service invocation remains the `/project` transport. Do not add direct HTTP URLs or domain-service-specific clients.
- Command status writes and projection checkpoint writes are advisory/non-regressive; they can warn and retry, but must not corrupt event state.
- Logs and activities may include envelope metadata such as tenant/domain/aggregate/correlation, but never event payload data.

### Previous Story Intelligence

- R4-A6 established that incomplete drain ranges and `EventCount` mismatches must not publish, remove drain records, unregister reminders, or decrement `pending_command_count`.
- R11-A1 established that checkpoint saves must use max-sequence/non-regression semantics and should fail open without blocking projection state updates.
- R11-A2 established the current polling-mode guardrails: tracked identity registration for polling, bounded per-tick delivery, overlap skip, enumeration failure logging, and ETag-guarded tracker page/index writes.
- R11-A4 and R9 proof stories repeatedly require same tenant/domain/projection identity in evidence; do not weaken identity claims when adding diagnostics.

### Testing Guidance

- Prefer focused Tier 2 tests for projection and drain logic. Use integration tests only when Dapr/Aspire runtime behavior is the subject of the change.
- Keep tests side-effect based: assert no publish, no record removal, no reminder unregister, no pending-counter decrement, stable reason-code logs, and checkpoint non-regression.
- Run test projects individually per repository guidance. The full `Hexalith.EventStore.Server.Tests` project has historical order/shared-fixture sensitivity; targeted slices are the gate unless the changed area requires broader coverage.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md#Proposal-B-DW1-Projection-and-Drain-Hardening`] - DW1 scope and acceptance direction.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md`] - raw deferrals grouped into this story.
- [Source: `_bmad-output/implementation-artifacts/post-epic-4-r4a6-drain-integrity-guard.md`] - drain integrity guard and review/elicitation traces.
- [Source: `_bmad-output/implementation-artifacts/post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md`] - checkpoint non-regression and fail-open projection guidance.
- [Source: `_bmad-output/implementation-artifacts/post-epic-11-r11a2-polling-mode-product-behavior.md`] - polling-mode tracker and enumeration hardening.
- [Source: `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`] - current delivery, `/project`, lock, and checkpoint save behavior.
- [Source: `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`] - current tracker persistence and enumeration behavior.
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`] - current drain reminder and pending-counter behavior.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Pre-dev hardening preflight: `_bmad-output/process-notes/predev-preflight-latest.json`, timestamp `2026-05-04T17:49:52Z`, result `pass`.

### Completion Notes List

- Created ready-for-dev story from first backlog row in the Post-Epic Deferred Work Cleanup package.
- No implementation work has been performed for this story.
- No `project-context.md` file was present in the repository at story creation.

### File List

- `_bmad-output/implementation-artifacts/post-epic-deferred-dw1-projection-and-drain-hardening.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Markdown and YAML validation should be run before dev handoff if local tooling is available.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-04 | 0.1 | Created ready-for-dev DW1 projection and drain hardening story. | Codex automation |
