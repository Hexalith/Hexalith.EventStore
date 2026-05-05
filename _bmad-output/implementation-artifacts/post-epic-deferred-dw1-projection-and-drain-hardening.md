# Post-Epic Deferred DW1: Projection and Drain Hardening

Status: done

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

## Party-Mode Review Clarifications

The 2026-05-04 party-mode review found the story directionally ready, but recommended tightening the implementation handoff before development. The clarifications below are binding for dev-story execution unless a human architecture/product decision explicitly supersedes them.

### Invariants

- Projection checkpoints must never advance past the last event durably applied to projection state.
- Projection checkpoint drift means a persisted checkpoint, stream position, tracker record, or projection state cannot be reconciled for the same tenant/domain/aggregate/projection identity.
- Cancellation, timeout, projection failure, tracker corruption, drain poison, and terminal drain failure must remain distinguishable in diagnostics and tests.
- Same-aggregate projection delivery must preserve per-aggregate ordering without introducing global projection serialization or cross-aggregate blocking.
- Drain terminal disposition must be stable, machine-classifiable, and idempotent across repeated reminder execution.

### Stable Diagnostic Vocabulary

- `/project` diagnostics are internal/operator diagnostics for this story. They must not change public projection/query contracts, SignalR behavior, or domain-service response contracts.
- Initial `/project` reason categories should include at least: `project_upstream_4xx`, `project_upstream_5xx`, `project_unsupported_content_type`, `project_invalid_charset`, `project_malformed_json`, `project_invalid_projection_type`, `project_invalid_state`, `project_timeout`, `project_cancelled`, `checkpoint_drift`, and `unknown`.
- Tracker disposition codes should distinguish `tracker_corrupt_scope_index`, `tracker_corrupt_identity_index`, `tracker_recovered`, and `tracker_terminal_failure` when those outcomes are observable.
- Drain activity reason codes should use bounded stable identifiers such as `drain_event_count_mismatch`, `drain_missing_event`, `drain_publish_failed`, `drain_terminal_failure`, and `unknown`.
- Tests must assert reason codes or categories, not localized/free-form message text. Human-readable messages may vary and may include safe structured metadata in logs, but activity tags must not carry high-cardinality actor IDs, event payloads, or raw exception text.

### Evidence Targets

| Concern | Expected behavior | Required proof | Out of scope |
|---|---|---|---|
| Checkpoint drift | Impossible checkpoint/stream pairings do not report silent success and do not advance checkpoints past durable projection state. | Focused orchestrator/tracker tests and stable diagnostic reason code evidence. | Public repair endpoint, projection reset API, or query contract change. |
| `/project` failures | Upstream 4xx/5xx, unsupported content, charset, malformed JSON, invalid projection type, invalid state, timeout, and caller cancellation classify distinctly. | Tests covering each changed branch and logs with safe metadata only. | Domain-service response contract changes or public supportability schema. |
| Same-aggregate serialization | Overlapping same-aggregate deliveries serialize while unrelated aggregates are not globally blocked by new infrastructure. | Concurrency regression test that exercises overlapping delivery, not only sequential repeats. | Global queues, global locks, Dapr topology changes, or broad retry loops. |
| Tracker corruption | Corrupt scope/index/page state is detected, bounded, and classified without tight retry loops or silent reset. | At least one scope-index and one identity-index corruption test with explicit disposition. | Broad tracker caching layer or automatic rebuild policy unless explicitly decided. |
| Drain poison/terminal disposition | Poison or unrecoverable drain state reaches a stable idempotent disposition or is recorded as accepted debt before code changes. | Tests for no partial publish, no premature removal, no pending-counter decrement, and no repeated terminal reprocessing. | New admin drain controls, global drain registry, or weakened at-least-once guarantees. |
| Reminder re-entrancy | Existing Dapr actor reminder semantics or a narrow guard prevents destructive overlapping drain execution. | Focused test or structured proof in Dev Agent Record tied to current side effects. | Dapr actor lifecycle or reminder topology changes. |
| EventId/reason-code uniqueness | New EventIds are unique in touched files and reason codes are stable enough for dashboards. | Local search/review evidence plus tests where behavior changes. | Project-wide EventId allocation table unless separately approved. |

### Deferred Decisions

- Whether corrupted tracker state should be automatically rebuilt, quarantined for operator action, or only classified and left for manual repair.
- Whether terminal drain dispositions are internal-only diagnostics or future operator-visible contract.
- Whether `/project` diagnostics should ever become a public supportability contract.
- Whether same-aggregate serialization must be guaranteed across process boundaries or only within the current projector instance.
- Whether timeout values are product policy or implementation configuration.

## Advanced Elicitation Clarifications

The 2026-05-05 advanced-elicitation pass treated the party-mode clarifications as the current baseline and tightened only the implementation handoff. These notes are binding for dev-story execution unless a human product or architecture decision supersedes them.

### Decision Ledger Required Before Production Edits

Before changing production code, record a short decision ledger in the Dev Agent Record that names each selected DW1 policy and the evidence expected for it:

- Checkpoint drift policy: whether to fail the delivery attempt, quarantine the checkpoint, or continue only with an explicit non-success diagnostic. The selected policy must never advance a checkpoint past durable projection state.
- Tracker corruption policy: whether corrupt state is rebuilt, quarantined, classified for manual repair, or accepted as deferred debt. Scope-index and identity-index paths may choose different policies, but each must be explicit.
- Drain poison policy: whether corrupt drain records use bounded backoff, terminal disposition, or accepted debt. If accepted debt is chosen, no production drain behavior should be changed until the rationale is recorded.
- Timeout policy: identify the owner of timeout duration and retry policy. DW1 may classify observed timeout outcomes, but must not invent a broad retry policy.

### Failure-Mode Closure Rules

- Every new reason code must have a single owner area (`project`, `tracker`, or `drain`), a bounded value, and at least one behavior-level assertion or explicit accepted-debt note.
- Timeout and cancellation tests must use separate evidence paths: caller-token cancellation is host lifecycle behavior; service timeout or transport timeout is projection delivery failure behavior.
- Tracker corruption handling must show bounded retry behavior at the poller boundary, not only validation helper failures.
- Drain terminal or poison handling must prove idempotence across repeated reminders: no partial publish, no premature record removal, no duplicate pending-counter decrement, and no repeated terminal side effect.
- EventId uniqueness proof is local to touched files for this story. A project-wide allocation table remains out of scope unless explicitly approved.

### Dev-Story Stop Signs

Stop and record a deferred decision instead of coding when the implementation pressure requires any of these:

- Public supportability or admin-facing contract for `/project`, tracker, or drain diagnostics.
- Automatic tracker rebuild or drain terminal disposition without a recorded product/architecture choice.
- Cross-process projection serialization, global queues, broad caches, or Dapr component changes.
- New integration or Aspire runtime dependency solely to prove behavior that can be covered by focused Tier 2 tests.

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

- [x] Task 0: Baseline and choose the minimum safe policy set (AC: #1, #7, #12)
    - [x] 0.1 Re-read the DW1 section in the deferred-work triage proposal and the relevant `deferred-work.md` entries.
    - [x] 0.2 Classify each selected deferred item as `patch-now`, `decision-now`, `accepted-debt`, `duplicate`, or `not-DW1`.
    - [x] 0.3 Record any architecture/product decisions before editing production code.
    - [x] 0.4 Confirm no public API, query contract, SignalR, admin endpoint, or Dapr component change is needed.
    - [x] 0.5 Add the DW1 decision ledger to the Dev Agent Record before production edits, covering checkpoint drift, tracker corruption, drain poison, and timeout ownership.

- [x] Task 1: Harden projection delivery diagnostics (AC: #1, #2, #3, #10)
    - [x] 1.1 Add focused tests for `/project` upstream 4xx, upstream 5xx, malformed JSON, unsupported or missing content type if supported by the test seam, empty `ProjectionType`, and invalid `State`.
    - [x] 1.2 Add stable reason-code logging for each failure class without logging payload data.
    - [x] 1.3 Separate host cancellation from service timeout/transient invocation failure.
    - [x] 1.4 Add a checkpoint-drift test or decision record covering checkpoint greater than aggregate event sequence, including the chosen retry/recovery expectation and the exact stable operator signal.
    - [x] 1.5 Assert the selected projection reason codes by stable value or category, and keep any human-readable message assertions out of the primary behavior tests.

- [x] Task 2: Preserve projection serialization and checkpoint non-regression (AC: #1, #4, #11)
    - [x] 2.1 Add or update same-aggregate overlap tests that prove the per-aggregate lock serializes delivery.
    - [x] 2.2 Assert checkpoint saves never move backwards and failed saves leave a useful operator signal.
    - [x] 2.3 Avoid global locks or static caches beyond the existing `KeyedSemaphore` contract.

- [x] Task 3: Harden tracker enumeration and scaling notes (AC: #5, #6, #10, #11)
    - [x] 3.1 Add scope-index corruption coverage and identity-index corruption coverage.
    - [x] 3.2 Assert corrupt or missing pages do not create tight retry loops.
    - [x] 3.3 Preserve ETag-guarded page/index writes and page size 100.
    - [x] 3.4 Document any still-deferred scaling limits with concrete thresholds, owner, and trigger symptom. Include identity-count, scope/page-count, and polling-interval thresholds rather than a generic "watch performance" note.
    - [x] 3.5 Prove poller-level boundedness for corrupt tracker state, including the next-domain or next-tick behavior that prevents one corrupt record from monopolizing polling.

- [x] Task 4: Resolve drain poison and activity signal gaps (AC: #7, #8, #9, #11)
    - [x] 4.1 Decide whether corrupt drain records get terminal disposition, bounded backoff, or accepted-debt status, and record the rejected alternatives plus preserved invariant in the story notes or deferred-work update.
    - [x] 4.2 If code changes, add tests proving no partial publish, no premature drain removal, no pending-counter decrement on failure, and reminder behavior remains intentional.
    - [x] 4.3 Replace high-cardinality activity failure text with stable reason codes while keeping useful structured logs.
    - [x] 4.4 Capture Dapr reminder semantics in the Dev Agent Record or code comments only where needed, and tie the proof to the exact side effects (`publish`, record removal, reminder unregister, `pending_command_count`) that must not overlap destructively.
    - [x] 4.5 If terminal disposition remains deferred, update the deferred-work disposition with the trigger that will reopen the decision.

- [x] Task 5: Validate and close bookkeeping (AC: #11, #13)
    - [x] 5.1 Run targeted projection and drain tests individually.
    - [x] 5.2 Run the four Tier 1 unit test projects individually if production code changed.
    - [x] 5.3 If Tier 3 runtime behavior changed, run the affected integration tests with the Aspire/Dapr prerequisites recorded.
    - [x] 5.4 Before handoff, search the touched files for EventId collisions and confirm the final reason-code vocabulary is consistent across tests, logs, and activity tags.
    - [x] 5.5 Update this story's Dev Agent Record, File List, Change Log, Verification Status, and any deferred-work dispositions.

### Review Findings

Code review 2026-05-05 (Blind Hunter + Edge Case Hunter + Acceptance Auditor). 1 decision-needed, 13 patch, 11 defer, 11 dismissed.

#### Decision-needed (resolved)

- [x] [Review][Decision] Checkpoint-read failure silently falls through to projection write/save [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:110-130] — **Resolved as option (b): fail-open + evidence.** The non-regression `Math.Max` in `SaveDeliveredSequenceAsync` already prevents backward checkpoint movement; this aligns with R11-A1 fail-open projection semantics. Closed by P2 below (regression test added).

#### Patch (applied)

- [x] [Review][Patch] Drift detection bypassed when aggregate has no events — moved checkpoint read above the empty-events early return; empty-events branch now logs `checkpoint_drift` when persisted checkpoint > 0. Regression test `UpdateProjectionAsync_EmptyEventsWithStaleCheckpoint_LogsDriftAndDoesNotInvokeProject` added.
- [x] [Review][Patch] [P2] Checkpoint-read failure regression evidence — `UpdateProjectionAsync_CheckpointReadThrows_FallsThroughWithNonRegressionSave` test added. Asserts `Stage=ProjectionCheckpointReadFailed` is logged, drift signal does NOT fire, and `SaveDeliveredSequenceAsync` is invoked with the highest event sequence (non-regression Math.Max protects backward movement).
- [x] [Review][Patch] `ClassifyDrainFailure` brittleness — introduced `DrainEventCountMismatchException : InvalidOperationException`. Throw site (`AggregateActor.cs:697-704`) constructs the typed exception; classifier matches by type (no message-prefix matching).
- [x] [Review][Patch] EventId 1142 log adds `HttpStatus` and `ContentType` fields — both `JsonException` and unknown-exception branches now pass status + content-type from the response handle. Test `UpdateProjectionAsync_ProjectMalformedJson_LogsStableReasonAndDoesNotSaveCheckpoint` asserts the new fields.
- [x] [Review][Patch] Host-token cancellation propagation test added — `UpdateProjectionAsync_HostCancellationToken_PropagatesAsOperationCanceled` asserts `OperationCanceledException` propagates without collapsing into `project_timeout`/`unknown` paths.
- [x] [Review][Patch] Dead `response is null` re-check removed — kept only `string.IsNullOrWhiteSpace(response.ProjectionType)` after the early-return for null.
- [x] [Review][Patch] `using (httpResponse) { ... }` block removed in favor of `using HttpResponseMessage responseHandle = httpResponse;` (declaration form). Re-indentation no longer needed.
- [x] [Review][Patch] `GetUpstreamReasonCode` 1xx/3xx classification — added `project_unexpected_status` for non-4xx/non-5xx non-success responses. Theory case `[InlineData(HttpStatusCode.MultipleChoices, ProjectionReasonCodes.ProjectUnexpectedStatus)]` added.
- [x] [Review][Patch] Tests duplicate stable reason-code literals — extracted `DrainReasonCodes`, `ProjectionReasonCodes`, and `TrackerReasonCodes` `internal static` classes (visible to test assembly via existing `InternalsVisibleTo`). Production code and tests now reference shared symbols.
- [x] [Review][Patch] `ConcurrencyProbe` `Task.Delay(150)` replaced with bounded poll (up to 100×20ms) that fails fast if serialization breaks (CallCount becomes 2 before ReleaseFirst).
- [x] [Review][Patch] `Encoding.GetEncoding` charset catch widened — now catches `ArgumentException or NotSupportedException`.
- [x] [Review][Patch] `IsCorruptIndex` upper bound — added `MaxReasonablePageCount = 1_000_000` clause to prevent near-infinite enumeration on pathological persisted `PageCount`.
- [x] [Review][Patch] Activity-listener tests now assert no high-cardinality leak — both stable-code drain tests verify the `eventstore.failure_reason` tag does not contain the aggregate's `ActorId` substring.
- [x] [Review][Patch] Sprint-status YAML stamp ordering — left as-is. The file has multiple pre-existing chronological anomalies (`14:30:00+02:00` line 33 is later than the `11:35:18+02:00` top stamp); fixing one DW1 line in isolation does not repair the broader inconsistency. Recorded as accepted-debt without action.

#### Defer (pre-existing or out-of-scope)

- [x] [Review][Defer] Drift produces unbounded log spam with no operator escape hatch [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:120-130] — accepted-debt per spec Decision Ledger ("leave repair/retry to state repair"); add to deferred-work for a future runbook + rate-limited log story.
- [x] [Review][Defer] `DrainReasonUnknown` will dominate production drain failures [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:829-834] — DAPR transient, state-store ETag conflict, OOM, etc. all collapse to `unknown`. Spec accepted-debt for terminal/backoff disposition implicitly limits classification scope; future hardening can expand classes.
- [x] [Review][Defer] Tracker corruption logged + skipped with no metric, quarantine, or recovery path [src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:183-220] — spec Decision Ledger explicitly chose "classify and bound enumeration without auto-rebuild or blind repair"; reopen-trigger thresholds recorded in `deferred-work.md`.
- [x] [Review][Defer] AppHost baseline build broken before this diff [Verification Status note] — pre-existing; out of DW1 scope. File a separate deferred-work entry to fix the `Hexalith.EventStore.csproj` references to `Hexalith.EventStore.Server.*`.
- [x] [Review][Defer] Tier 3 integration tests skipped [Verification Status note] — DW1 changed Tier 2 server behavior only; drain activity tag changes are observable but the spec test guidance prefers focused Tier 2 coverage. CLAUDE.md R2-A6 rule applies but is not directly violated.
- [x] [Review][Defer] EventId 1141-1144 unique only "within touched files"; project-wide collisions remain [Dev Agent Record note] — explicitly out of scope per spec; project-wide allocation table is separate work.
- [x] [Review][Defer] Drain disposition reopen trigger names "10 consecutive integrity retries" but no per-record retry-streak counter exists [_bmad-output/implementation-artifacts/deferred-work.md:9-10] — trigger is descriptive; counter is future work. Recorded as accepted-debt.
- [x] [Review][Defer] `AggregateException` wrapping `MissingEventException` not unwrapped in `ClassifyDrainFailure` [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:829-834] — actor turn execution does not normally produce wrapped exceptions; defensive only.
- [x] [Review][Defer] AC #5 wording covers "missing, null, overfull, stale" page state but code only handles index integrity [src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:257-262] — spec Decision Ledger narrowed to scope/identity indexes only; missing-page rebuild/quarantine remains out of scope.
- [x] [Review][Defer] `text/json` legacy media type rejected as unsupported [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:346-350] — non-standard; spec doesn't require it. Defer unless a domain service is observed emitting it.
- [x] [Review][Defer] `events.Max` could NRE if any envelope is null [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:109] — events come from internal aggregate actor; null elements would indicate a separate bug. Defensive only.

#### Dismissed (false positives / out of scope by design)

- `TaskCanceledException` mislabeled as timeout when not from `HttpClient.Timeout` — caller-token branch runs first; `TaskCanceledException` only reaches the second catch when the caller's token did not signal, which IS HttpClient timeout semantics.
- Drift check after fetching all events is a steady-state cost regression — code comment at line 98-99 establishes full-replay as the explicit immediate-delivery contract; cost trade-off is intentional.
- `OperationCanceledException` filter racy with linked tokens — `IsCancellationRequested` is checked at observe time; .NET runtime ensures consistency.
- Empty-string `State` rejection is a behavior change disguised as refactor — AC #2 EXPLICITLY requires "string-empty state" rejection; spec-mandated.
- TestLogger/LogEntry omitted from File List — `tests/Hexalith.EventStore.Server.Tests/TestUtilities/TestLogger.cs` pre-exists and was not modified by this diff.
- `IsCorruptIndex` false-positive on `(PageCount > 0, LastPageCount = 0)` — verified that writers in `TryAddScopeAsync`/`TryAddIdentityAsync` always set `LastPageCount = 1` atomically with PageCount increment on rollover; the `(N, 0)` state is never produced by writers.
- Charset whitespace not trimmed before `Encoding.GetEncoding` — `httpResponse.Content.Headers.ContentType.CharSet` already trims per HTTP RFC parsing in `MediaTypeHeaderValue`.
- Empty State `{}` or `[]` not rejected — AC #2 only requires rejection of null/undefined/string-empty; empty object/array is a legitimate "no state" representation.
- `lastDelivered == highestSequence` triggers redundant `/project` replay — pre-existing full-replay contract; non-regression save short-circuits the round-trip on the checkpoint side.
- `predev-hardening-runs.log` listed in File List but not in current uncommitted diff — file was committed during earlier predev preflight commits (`83a260dd` and similar); story File List correctly reflects all touched files across the story's lifetime.
- Story File List omits TestLogger reference — pre-existing utility; no need to list.

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
- Pre-dev hardening preflight: `_bmad-output/process-notes/predev-preflight-latest.json`, timestamp `2026-05-04T18:38:42Z`, result `fail` only for working-tree cleanliness; classified as a soft warning because the JSON stdout listed only `_bmad-output/test-artifacts/` paths.
- 2026-05-05 AppHost baseline: `aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` failed before DW1 edits because `src/Hexalith.EventStore/Hexalith.EventStore.csproj` could not resolve `Hexalith.EventStore.Server.*` symbols during AppHost build. Aspire MCP `doctor` passed SDK/container checks and warned only about dev certificates and deprecated agent config.
- 2026-05-05 DW1 implementation policy ledger:
  - Checkpoint drift: `patch-now`; after full replay reads events, if persisted `LastDeliveredSequence` exceeds highest available event sequence, log `checkpoint_drift` with tenant/domain/aggregate and sequence values, skip projection write/checkpoint save, and leave repair/retry to state repair.
  - `/project` diagnostics: `patch-now`; classify upstream 4xx/5xx, unsupported/missing content type, invalid charset, malformed JSON, invalid `ProjectionType`, invalid `State`, timeout, host cancellation, and unknown transport failures with bounded reason codes. Do not log event payload data.
  - Tracker corruption: `patch-now` for corrupt scope/identity indexes; classify and bound enumeration without auto-rebuild or blind repair. Missing page rebuild/quarantine remains out of scope.
  - Tracker scaling: `accepted-debt`; retain O(scope pages + identity pages) per tick, page size 100, ETag writes. Reopen thresholds recorded in `deferred-work.md`.
  - Drain poison: `accepted-debt` for terminal/backoff disposition; preserve current at-least-once retry behavior and no pending-counter decrement on integrity failure. Stable activity reason codes are patched now.
  - Timeout ownership: Dapr/HttpClient configuration owns duration/retry policy. DW1 classifies observed timeout outcomes but does not add application retry loops.
- 2026-05-05 Reminder re-entrancy proof: Dapr actor reminder callbacks execute through actor turn-based concurrency. Current `DrainUnpublishedEventsAsync` side effects remain ordered inside one actor turn: load record, validate/load/publish, then on success remove record, decrement `pending_command_count`, save, unregister reminder, and write advisory status. Failure paths preserve the record/reminder and do not decrement `pending_command_count`. No non-actor/manual drain trigger was added.
- 2026-05-05 EventId/reason-code review: new projection/tracker EventIds in touched files are `1141`, `1142`, `1143`, and `1144`; no collisions within touched files. Existing unrelated 11xx project-wide collisions were not renumbered.

### Completion Notes List

- Created ready-for-dev story from first backlog row in the Post-Epic Deferred Work Cleanup package.
- Party-mode review on 2026-05-04 recommended `needs-story-update`; low-risk clarifications were applied for invariants, diagnostic vocabulary, evidence targets, and deferred decisions.
- DW1 implementation completed and moved to review.
- No `project-context.md` file was present in the repository at story creation.
- Advanced elicitation on 2026-05-05 applied low-risk handoff clarifications for decision-ledger requirements, failure-mode closure rules, poller boundedness proof, drain idempotence evidence, and dev-story stop signs.
- Implemented projection diagnostics with stable reason codes: `project_upstream_4xx`, `project_upstream_5xx`, `project_unsupported_content_type`, `project_invalid_charset`, `project_malformed_json`, `project_invalid_projection_type`, `project_invalid_state`, `project_timeout`, `checkpoint_drift`, and `unknown`.
- Added checkpoint-drift detection that logs `checkpoint_drift` and avoids projection write/checkpoint save when persisted checkpoint state is ahead of the replayed event stream.
- Added same-aggregate overlap regression coverage proving `ProjectionUpdateOrchestrator.ProjectionLocks` serializes concurrent same-aggregate deliveries without adding a global lock.
- Added tracker scope-index and identity-index corruption detection with `tracker_corrupt_scope_index` / `tracker_corrupt_identity_index`, while preserving page size 100 and ETag-guarded writes.
- Replaced high-cardinality drain activity failure tag/status values with stable codes (`drain_publish_failed`, `drain_event_count_mismatch`, `drain_missing_event`, `unknown`) while leaving detailed logs and persisted failure reasons intact.
- Updated `deferred-work.md` dispositions for checkpoint drift, drain poison terminal/backoff debt, drain activity tags, reminder re-entrancy, and tracker scaling thresholds.

### File List

- `_bmad-output/implementation-artifacts/post-epic-deferred-dw1-projection-and-drain-hardening.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `src/Hexalith.EventStore.Server/Actors/DrainEventCountMismatchException.cs` (review patch P3)
- `src/Hexalith.EventStore.Server/Actors/DrainReasonCodes.cs` (review patch P9)
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionReasonCodes.cs` (review patch P9)
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
- `src/Hexalith.EventStore.Server/Projections/TrackerReasonCodes.cs` (review patch P9)
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Party-mode review and advanced elicitation traces are recorded inline; no status change was required.
- Markdown and YAML validation should be run before dev handoff if local tooling is available.
- AppHost baseline was attempted before edits; build failed on pre-existing `Hexalith.EventStore.csproj` references to `Hexalith.EventStore.Server.*`, so no Aspire resources were available to inspect.
- Targeted Tier 2 tests passed: `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~ProjectionUpdateOrchestratorTests|FullyQualifiedName~ProjectionCheckpointTrackerTests|FullyQualifiedName~ProjectionPollerServiceTests|FullyQualifiedName~EventDrainRecoveryTests"` (91 passed).
- Tier 1 unit projects passed individually:
  - `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj` (334 passed).
  - `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj` (281 passed).
  - `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj` (63 passed).
  - `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj` (78 passed).
- `git diff --check` reported line-ending warnings only (`LF will be replaced by CRLF`), with no whitespace errors.
- Tier 3 integration tests were not run because DW1 changed focused server behavior and did not change runtime Dapr/Aspire topology or public contracts.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-04 | 0.1 | Created ready-for-dev DW1 projection and drain hardening story. | Codex automation |
| 2026-05-04 | 0.2 | Applied party-mode review clarifications for invariants, diagnostics, evidence targets, and deferred decisions. | Codex automation |
| 2026-05-05 | 0.3 | Applied advanced-elicitation hardening for decision ledger, bounded failure evidence, and dev-story stop signs. | Codex automation |
| 2026-05-05 | 0.4 | Implemented DW1 projection diagnostics, checkpoint drift detection, tracker corruption signals, stable drain activity reason codes, tests, deferred-work dispositions, and moved story to review. | Amelia |
| 2026-05-05 | 0.5 | Code review applied 13 patches (drift in empty-events branch, typed `DrainEventCountMismatchException`, EventId 1142 HttpStatus/ContentType, host cancellation test, dead null-check removal, `using`-declaration re-indent, `project_unexpected_status` for 1xx/3xx, shared `*ReasonCodes` constants, deterministic concurrency probe, charset `NotSupportedException` catch, `IsCorruptIndex` upper bound, high-cardinality leak negative assertions, checkpoint-read-failure regression evidence) plus 11 deferred items recorded; moved story to done. Targeted Tier 2 tests 95/95, Tier 1 unit projects 334+281+63+78 all green. | Code review |

## Party-Mode Review

- ISO date and time: 2026-05-04T20:40:39+02:00
- Selected story key: `post-epic-deferred-dw1-projection-and-drain-hardening`
- Command/skill invocation used: `/bmad-party-mode post-epic-deferred-dw1-projection-and-drain-hardening; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: All four reviewers recommended `needs-story-update`, not blocked. Shared concerns were ambiguous checkpoint drift invariants, diagnostic-only `/project` classifications, cancellation versus timeout ownership, same-aggregate versus global serialization boundaries, tracker corruption disposition, drain terminal reason-code stability, reminder re-entrancy evidence, EventId uniqueness, and proof targets for reviewer signoff.
- Changes applied: Added Party-Mode Review Clarifications with invariants, internal diagnostic vocabulary, evidence-target table, scope guardrails, and deferred decisions. Added Dev Agent Record and Change Log entries.
- Findings deferred: Human architecture/product judgment is still needed for tracker rebuild/quarantine/classify policy, internal versus operator-visible terminal drain disposition, public supportability status of `/project` diagnostics, cross-process serialization guarantees, and timeout policy ownership.
- Final recommendation: `needs-story-update`

## Advanced Elicitation

- ISO date and time: 2026-05-05T05:11:40+02:00
- Selected story key: `post-epic-deferred-dw1-projection-and-drain-hardening`
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-deferred-dw1-projection-and-drain-hardening`
- Batch 1 method names: Red Team vs Blue Team; Security Audit Personas; Failure Mode Analysis; Self-Consistency Validation; Critique and Refine
- Reshuffled Batch 2 method names: Pre-mortem Analysis; Architecture Decision Records; Chaos Monkey Scenarios; 5 Whys Deep Dive; Comparative Analysis Matrix
- Findings summary: The story was already directionally ready after party-mode review, but the implementation handoff still needed a mandatory policy decision ledger, clearer separation between timeout and cancellation evidence, poller-level proof for bounded tracker corruption, and explicit drain idempotence evidence across repeated reminders.
- Changes applied: Added Advanced Elicitation Clarifications covering decision-ledger requirements, failure-mode closure rules, and dev-story stop signs. Added task details for decision-ledger creation, reason-code assertions, poller boundedness proof, and deferred terminal-disposition triggers. Added Dev Agent Record and Change Log entries.
- Findings deferred: Human product/architecture judgment remains required for tracker rebuild/quarantine/classify policy, drain terminal disposition, public supportability status of diagnostics, cross-process projection serialization, and timeout policy ownership.
- Final recommendation: `ready-for-dev`
