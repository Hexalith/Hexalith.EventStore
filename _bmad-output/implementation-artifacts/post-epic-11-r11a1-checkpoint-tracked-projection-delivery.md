# Post-Epic-11 R11-A1: Checkpoint-Tracked Projection Delivery

Status: done

<!-- Source: epic-11-retro-2026-04-30.md - Action item R11-A1 -->
<!-- Source: epic-12-retro-2026-04-30.md - R12-A5 carry-forward backlog -->
<!-- Source: docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md - Projection Checkpoint Tracker -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform engineer hardening the server-managed projection builder,
I want projection delivery to resume from a persisted per-aggregate checkpoint instead of replaying every event from sequence 0 on every trigger,
so that projection updates remain eventually consistent and scale beyond the sample-size full-replay path without blocking command processing.

## Story Context

Epic 11 shipped the Mode B server-managed projection path: `EventPublisher` fires a background update, `ProjectionUpdateOrchestrator` reads events from `IAggregateActor.GetEventsAsync`, sends `ProjectionRequest` to the domain service `/project` endpoint, writes `ProjectionState` to `EventReplayProjectionActor`, regenerates ETags, and broadcasts projection change signals. That path is already wired and must be reused.

The open R11-A1 risk is precise: `ProjectionUpdateOrchestrator` currently contains a TODO and calls `GetEventsAsync(0)`, so every trigger replays the full aggregate history. The server-managed projection design already defined a per-aggregate checkpoint tracker that stores the last successfully delivered sequence and updates it only after `UpdateProjectionAsync` succeeds. This story implements that tracker and switches immediate delivery to incremental reads. It does not implement polling mode, change the `/project` contract, change query routing, or move projection state ownership into EventStore domain types.

## Acceptance Criteria

1. **Projection checkpoint state is represented explicitly.** A server-side checkpoint model records at least `TenantId`, `Domain`, `AggregateId`, `LastDeliveredSequence`, and `UpdatedAt`. The checkpoint identity is derived from the canonical aggregate identity (`tenant:domain:aggregateId`) and stored under a deterministic key that does not expose or duplicate `AggregateActor` event-state keys.

2. **A checkpoint tracker abstraction is added and registered.** `Hexalith.EventStore.Server` exposes an injectable tracker, for example `IProjectionCheckpointTracker`, with methods to read the last delivered sequence and save a new checkpoint. It is registered from `EventStoreServerServiceCollectionExtensions.AddEventStoreServer`.

3. **Immediate projection delivery reads incrementally.** `ProjectionUpdateOrchestrator.UpdateProjectionAsync` reads the checkpoint before calling the aggregate actor and calls `GetEventsAsync(lastDeliveredSequence)` instead of `GetEventsAsync(0)`. When no checkpoint exists, the first delivery uses sequence 0 and behaves like the current full-replay bootstrap.

4. **Checkpoint is saved only after successful projection write.** The tracker updates `LastDeliveredSequence` to the highest `SequenceNumber` returned by the aggregate actor only after the domain service returns a valid `ProjectionResponse` and `IProjectionWriteActor.UpdateProjectionAsync` completes successfully. Do not infer the new checkpoint from `lastDeliveredSequence + events.Length`; sequence gaps, filtered reads, or future event-shape changes must not create an off-by-one skip. Resolver failures, aggregate read failures, HTTP failures, invalid projection responses, and actor write failures do not advance the checkpoint.

5. **At-least-once semantics are preserved.** If a projection update fails after events are read but before checkpoint save, a later trigger resends the same events from the previous checkpoint. Duplicate delivery is acceptable; silent event skipping is not.

6. **Checkpoint writes never regress.** A delayed or duplicate projection update cannot lower a checkpoint that already advanced to a higher sequence for the same aggregate. Prefer the same bounded ETag retry shape already used by `DaprCommandActivityTracker` / `DaprStreamActivityTracker`: `GetStateAndETagAsync`, merge with `Math.Max(existing.LastDeliveredSequence, proposedSequence)`, then `TrySaveStateAsync`. If the save cannot win after the bounded retry budget, log and leave the checkpoint unchanged rather than falling back to a blind write.

7. **Command processing remains fail-open.** Projection checkpoint failures are logged and swallowed inside the projection update path. Checkpoint read failure falls back to sequence 0 for that update so delivery may replay but cannot skip events. Checkpoint save failure leaves the previous checkpoint unchanged so a later trigger retries the same events. These failures must not make `EventPublisher.PublishEventsAsync` report failure and must not cause command submission or event publication to fail.

8. **Projection wire contract stays unchanged.** `ProjectionRequest`, `ProjectionResponse`, `ProjectionEventDto`, `ProjectionState`, `IProjectionWriteActor`, and `EventReplayProjectionActor` remain compatible. Domain services still receive only public projection event fields and still own projection apply logic.

9. **Polling mode remains deliberately deferred.** `RefreshIntervalMs > 0` keeps the current `PollingModeDeferred` behavior. This story may make checkpoint state usable by a future poller, but it must not implement `ProjectionPollerService` or change polling product behavior; that is `post-epic-11-r11a2-polling-mode-product-behavior`.

10. **Tests pin the incremental path.** Unit coverage proves: missing checkpoint uses `GetEventsAsync(0)`; existing checkpoint `N` uses `GetEventsAsync(N)`; checkpoint read failure replays from `0` without throwing; success saves the maximum returned sequence; no-events does not save; every failure class in AC #4 leaves the checkpoint unchanged; checkpoint save failure is swallowed after the projection state write; duplicate or stale saves cannot lower the checkpoint.

11. **Existing projection behavior still passes.** Existing `ProjectionUpdateOrchestratorTests`, `EventReplayProjectionActorTests`, `AggregateActorGetEventsTests`, `ProjectionUpdateOrchestratorRefreshIntervalTests`, and projection contract tests remain green. Any changed assertion that previously expected `GetEventsAsync(0)` must be narrowed to the no-checkpoint first-delivery case.

12. **Documentation records the closed limitation.** Developer-facing projection documentation or the Epic 11 retro follow-up record states that immediate projection delivery is now checkpoint-tracked. Any remaining limitation, such as duplicate delivery under concurrent fire-and-forget triggers, is documented honestly as at-least-once behavior.

## Tasks / Subtasks

- [x] Task 1: Add projection checkpoint model and tracker contract (AC: #1, #2)
  - [x] Define a compact checkpoint record under `src/Hexalith.EventStore.Server/Projections/`.
  - [x] Define tracker methods for reading the current checkpoint and saving a successful sequence.
  - [x] Keep the key derivation centralized and covered by tests.

- [x] Task 2: Implement persistent checkpoint storage (AC: #1, #4, #5, #6, #7)
  - [x] Use DAPR state management through an injectable service boundary, not direct aggregate actor state-key access.
  - [x] Use a configurable checkpoint state-store name with a repo-consistent default of `statestore`; do not hard-code aggregate actor state keys.
  - [x] Make missing state return sequence 0.
  - [x] Treat checkpoint read failure as fail-open full replay from sequence 0 and log tenant, domain, aggregate ID, and exception type.
  - [x] Make stale saves non-regressing by keeping the maximum observed sequence.
  - [x] Prefer DAPR state ETag/compare-and-set for checkpoint writes where the SDK path is practical; use the repo-local `DaprCommandActivityTracker` / `DaprStreamActivityTracker` bounded retry pattern as the precedent.
  - [x] If the bounded ETag retry path exhausts its attempts, return a save-failed result and let the orchestrator log/swallow it; do not perform a last blind save that could regress a checkpoint.
  - [x] Log checkpoint read/save failures with tenant, domain, aggregate ID, and exception type only; do not log event payloads.

- [x] Task 3: Wire tracker into DI (AC: #2)
  - [x] Register the tracker in `EventStoreServerServiceCollectionExtensions`.
  - [x] Keep existing service lifetimes consistent with `ProjectionUpdateOrchestrator` and DAPR client usage.

- [x] Task 4: Update `ProjectionUpdateOrchestrator` incremental flow (AC: #3, #4, #5, #7, #8, #9)
  - [x] Read checkpoint after the polling-mode guard and before creating or invoking the aggregate actor.
  - [x] If checkpoint read fails, continue with sequence 0 and rely on duplicate-safe at-least-once projection delivery.
  - [x] Replace the hard-coded `GetEventsAsync(0)` with `GetEventsAsync(lastDeliveredSequence)`.
  - [x] Save checkpoint only after `UpdateProjectionAsync` succeeds.
  - [x] Compute the saved sequence as `events.Max(e => e.SequenceNumber)`, not from event count or checkpoint offset.
  - [x] If checkpoint save fails after projection state is written, log and return without throwing; the next trigger must replay from the old checkpoint.
  - [x] Leave the `RefreshIntervalMs > 0` early return unchanged.
  - [x] Preserve the outer catch/fail-open behavior.

- [x] Task 5: Expand server unit tests (AC: #3, #4, #5, #6, #7, #10, #11)
  - [x] Add focused tracker tests for missing, read, save, max-sequence, invalid key, retry-exhaustion, and storage failure behavior.
  - [x] Update orchestrator tests so the first-delivery case still proves `GetEventsAsync(0)`.
  - [x] Add orchestrator tests for existing checkpoint, checkpoint read failure replay-from-zero, successful save, non-contiguous returned sequences saving the maximum sequence, no-events no-save, invalid response no-save, actor-write failure no-save, and checkpoint save failure no-throw.
  - [x] Keep the current DAPR invocation testability limitation in mind: use the existing HttpClient/fake-handler patterns where possible and do not overfit to non-virtual `DaprClient` members.

- [x] Task 6: Update documentation or retro follow-up notes (AC: #12)
  - [x] Update the server-managed projection builder doc or implementation notes to say immediate delivery is checkpoint-tracked.
  - [x] Record that polling remains separate R11-A2 scope.

- [x] Task 7: Run required validation (AC: #10, #11)
  - [x] `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~ProjectionUpdateOrchestratorTests|FullyQualifiedName~ProjectionCheckpoint"`
  - [x] `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~EventReplayProjectionActorTests|FullyQualifiedName~AggregateActorGetEventsTests|FullyQualifiedName~ProjectionUpdateOrchestratorRefreshIntervalTests"`
  - [x] If touched contracts require it: `dotnet test tests/Hexalith.EventStore.Contracts.Tests`

## Dev Notes

### Existing Implementation To Reuse

- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` already triggers the projection orchestrator after successful event publication and deliberately catches projection update failures in the background task.
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` is the only immediate-delivery flow to change. The current TODO above `GetEventsAsync(0)` is the target.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` and `IAggregateActor.GetEventsAsync(long fromSequence)` already support partial reads, and `AggregateActorGetEventsTests` covers after-sequence behavior, empty results, missing events, and negative inputs.
- `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs` already persists opaque `ProjectionState`, regenerates projection change notifications through `IProjectionChangeNotifier`, and fails open if notification fails.
- `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs` already treats `0` as immediate mode and `>0` as polling mode; do not change that semantic here.

### Implementation Guardrails

- Do not change public projection DTOs. `ProjectionEventDto` must continue to expose only `EventTypeName`, `Payload`, `SerializationFormat`, `SequenceNumber`, `Timestamp`, and `CorrelationId`.
- Do not read DAPR actor event keys directly. `AggregateActor.GetEventsAsync` remains the boundary that hides aggregate actor state layout.
- Do not add custom retry loops. DAPR resiliency and later triggers provide retry behavior.
- Do not make projection delivery block or fail command processing.
- Do not implement polling. R11-A2 owns the product decision and poller behavior.
- Do not turn projection checkpointing into exactly-once delivery. The contract is at-least-once with no silent skips.
- Do not place checkpoint state inside `AggregateActor` actor-state keys. Keep checkpoint keys in a dedicated namespace such as `projection-checkpoints:{identity.ActorId}` so they are deterministic without duplicating the `:events:` or `:metadata` key spaces.

### Suggested File Touches

- `src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpoint.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`
- Optional docs: `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md`

### Architecture And Version Notes

- Package versions are centrally pinned in `Directory.Packages.props`: Dapr `1.17.7`, Aspire `13.2.2`, .NET extensions `10.x`, xUnit v3, Shouldly, and NSubstitute. Do not introduce new packages for checkpointing without a clear reason.
- DAPR actor execution is single-threaded per actor method, but `EventPublisher` can start concurrent background projection tasks. Design checkpoint writes to tolerate duplicate deliveries and stale saves.
- Current DAPR state docs continue to require a configured actor state store for actors and support state APIs with ETags; use repo-local DAPR component configuration and existing DaprClient registration patterns rather than hard-coded store names. Official reference checked: https://docs.dapr.io/reference/api/state_api/ and https://docs.dapr.io/developing-applications/building-blocks/actors/actors-features-concepts/.
- `DaprCommandActivityTracker` and `DaprStreamActivityTracker` already demonstrate the repo's preferred optimistic-concurrency loop: read state plus ETag, merge, `TrySaveStateAsync`, retry on ETag mismatch, and fail open after the bounded retry budget. Projection checkpoint persistence should follow that shape unless implementation proves a better local abstraction.
- The orchestrator tests currently rely on fake `HttpClient` boundaries and note that some `DaprClient` members are non-virtual under NSubstitute. Keep checkpoint tracker tests focused on the tracker boundary, and keep orchestrator tests focused on checkpoint read/save calls, sequence arguments, and fail-open behavior.

### Previous Story Intelligence

- Epic 11 retro says full replay was intentionally accepted for the sample but is not the final production shape. This story closes that debt for immediate mode.
- Story 11.3 tests explicitly called out that non-virtual DAPR methods limit pure unit coverage. Keep behavior-focused tests around resolver, actor proxy, event read, and checkpoint boundaries, and reserve full DAPR invocation proof for integration coverage.
- Epic 12 proved the sample UI demo path but did not address projection delivery mechanics. Do not use UI smoke evidence as proof that checkpoint tracking is done.
- Previous projection tests still contain Story 11-3 wording that "full replay from sequence 0" is expected. This story must rename or narrow those assertions so sequence 0 is only the no-checkpoint bootstrap case.

### Project Structure Notes

- Keep projection infrastructure in `src/Hexalith.EventStore.Server/Projections/`.
- Keep actor read/write contracts in `src/Hexalith.EventStore.Server/Actors/`.
- Keep tests beside existing server tests under `tests/Hexalith.EventStore.Server.Tests/Projections/`.
- Use existing logging source-generator style for new structured logs.

## References

- `_bmad-output/implementation-artifacts/epic-11-retro-2026-04-30.md` - R11-A1 action item and full replay risk.
- `_bmad-output/implementation-artifacts/epic-12-retro-2026-04-30.md` - R12-A5 carry-forward of R11-A1 through R11-A4.
- `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md` - Projection Checkpoint Tracker, immediate trigger, error handling, and testing guidance.
- `_bmad-output/planning-artifacts/epics.md` - Epic 11 stories and server-managed projection builder requirements.
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` - current full-replay TODO and delivery flow.
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` - fire-and-forget trigger and fail-open projection update handling.
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorGetEventsTests.cs` - existing partial event-read coverage.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~ProjectionUpdateOrchestratorTests|FullyQualifiedName~ProjectionCheckpoint" --no-restore`
- `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~EventReplayProjectionActorTests|FullyQualifiedName~AggregateActorGetEventsTests|FullyQualifiedName~ProjectionUpdateOrchestratorRefreshIntervalTests" --no-restore`

### Completion Notes List

- Added persisted projection checkpoint model, injectable tracker, configurable checkpoint state store, and DI registration.
- Switched immediate projection delivery from unconditional full replay to checkpoint-based `GetEventsAsync(lastDeliveredSequence)`.
- Saves checkpoints only after successful projection actor writes, using DAPR ETag optimistic concurrency with bounded retry and max-sequence merge.
- Preserved fail-open behavior for checkpoint read/save failures and polling-mode deferral.
- Updated projection design documentation to state immediate delivery is checkpoint-tracked and polling remains R11-A2 scope.

### File List

- `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md`
- `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs`
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpoint.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorRefreshIntervalTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`

## Party-Mode Review

- Date/time: 2026-05-01T10:34:19+02:00
- Selected story key: `post-epic-11-r11a1-checkpoint-tracked-projection-delivery`
- Command/skill invocation used: `/bmad-party-mode post-epic-11-r11a1-checkpoint-tracked-projection-delivery; review;`
- Participating BMAD agents: Bob (Scrum Master), Winston (Architect), Amelia (Developer Agent), Murat (Master Test Architect), Paige (Technical Writer)
- Findings summary:
  - Bob: The story was ready-for-dev but checkpoint read/save failure semantics were not explicit enough for development handoff.
  - Winston: AC #6 needed a concrete non-regression write strategy so concurrent fire-and-forget triggers cannot silently lower checkpoints.
  - Amelia: The orchestrator task order needed to say where checkpoint reads happen relative to the polling guard and aggregate actor proxy creation.
  - Murat: AC #10 and Task 5 needed explicit tests for checkpoint read failure and checkpoint save failure, not only domain/actor failure classes.
  - Paige: Documentation guardrails needed to distinguish projection checkpoint keys from existing aggregate actor event and metadata keys.
- Changes applied:
  - Clarified AC #7 fail-open behavior for checkpoint read failure and checkpoint save failure.
  - Expanded AC #10 and Task 5 with read-failure replay and save-failure no-throw test obligations.
  - Added Task 2 guidance for configurable `statestore`, DAPR ETag/compare-and-set where practical, and read-before-write max fallback.
  - Tightened Task 4 sequencing around polling-mode guard, aggregate actor creation, and checkpoint save failure.
  - Added an implementation guardrail for a dedicated projection checkpoint key namespace.
- Findings deferred:
  - No product-scope or architecture-policy decisions deferred. The DAPR ETag/compare-and-set API choice remains an implementation detail to verify during `bmad-dev-story`.
- Final recommendation: ready-for-dev

## Review Findings

Date/time: 2026-05-01T (post-implementation review via `/bmad-code-review post-epic-11-r11a1-checkpoint-tracked-projection-delivery`)
Layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor

- [x] [Review][Patch] Tracker swallows all retry-loop exceptions on every attempt, hiding storage failures behind Debug logs [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:73-91`] — Add the precedent guard `when (attempt < MaxEtagRetries - 1)` so the final-attempt exception bubbles to the orchestrator's `Log.CheckpointSaveFailed` Warning. AC #6 says "Prefer the same bounded ETag retry shape already used by `DaprCommandActivityTracker`," and that file uses `when (attempt < MaxEtagRetries - 1)` (line 183) precisely so persistent store failures surface at Warning level instead of being silently downgraded.
- [x] [Review][Patch] No tracker unit test asserts `ReadLastDeliveredSequenceAsync` propagates non-OCE storage exceptions [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs`] — Task 5 requires "tracker tests for ... read ... and storage failure behavior." Add a test where `daprClient.GetStateAsync<ProjectionCheckpoint>` throws a non-OCE exception and assert it propagates so the orchestrator's outer try/catch handles it.
- [x] [Review][Patch] Existing failure-path orchestrator tests do not assert `SaveDeliveredSequenceAsync` was NOT called [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`] — AC #10 + Task 5 require proof "every failure class in AC #4 leaves the checkpoint unchanged." Add `await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(...)` to `_DomainServiceFails_DoesNotThrow`, `_ResolverFails_DoesNotThrow`, `_GetEventsAsyncFails_DoesNotThrow`.
- [x] [Review][Patch] Missing test: invalid `ProjectionResponse` (null/empty `ProjectionType` or null `State`) does not save checkpoint [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`] — Task 5 enumerates "invalid response no-save." Add a test that returns `{"projectionType":""}` or null state and asserts `SaveDeliveredSequenceAsync` is not called.
- [x] [Review][Patch] Missing test: `IProjectionWriteActor.UpdateProjectionAsync` failure does not save checkpoint [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`] — Task 5 enumerates "actor-write failure no-save." Add a test where `writeActor.UpdateProjectionAsync` throws and asserts `SaveDeliveredSequenceAsync` is not called.
- [x] [Review][Patch] `CheckpointSaveExhausted` log lacks the attempted sequence number for operator correlation [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:142,323-328`] — Add `attemptedSequence` parameter so operators can correlate the warning with the projection actor's persisted state without consulting traces.
- [x] [Review][Patch] No test pins `OperationCanceledException` pass-through in tracker [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs`] — The tracker has an explicit `catch (OperationCanceledException) { throw; }` that prevents the catch-all from absorbing cancellation. A future refactor that drops this guard would silently regress cancellation semantics. Add a test that throws OCE from `GetStateAndETagAsync` and asserts it propagates.
- [x] [Review][Defer] Checkpoint > actor's `CurrentSequence` permanently silences projections [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:80-83`] — deferred. AC #5 says silent skipping is forbidden, but the corruption scenario (restored state-store backup, key drift between aggregate and checkpoint stores) requires drift detection beyond R11-A1 scope. Add diagnostic when `events.Length == 0 && lastDeliveredSequence > 0` to a follow-up story.
- [x] [Review][Defer] Concurrent fire-and-forget projection triggers can interleave so an older `UpdateProjectionAsync` lands after a newer one, regressing projection state while the checkpoint stays at max [`src/Hexalith.EventStore.Server/Events/EventPublisher.cs:135-143`] — deferred. The design doc already concedes "duplicate delivery can still occur under concurrent fire-and-forget triggers"; the **state-regression** failure mode is a stronger concern but requires per-aggregate serialization (Channel/SemaphoreSlim) outside R11-A1 scope. Track in R11-A2 or successor.
- [x] [Review][Defer] Outer orchestrator `catch (Exception ex)` absorbs `OperationCanceledException` from `ReadLastDeliveredSequenceAsync` if a future caller passes a non-`None` token [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:151`] — deferred, pre-existing. EventPublisher passes `CancellationToken.None`, so production cancellation is not affected today.
- [x] [Review][Defer] `MaxEtagRetries = 3` is a hardcoded const not exposed via `ProjectionOptions` — deferred. Story Dev Notes explicitly punt the retry count to bmad-dev-story so long as the bounded/non-regression contract holds; a separate story can add tunability.
- [x] [Review][Defer] `SaveDeliveredSequenceAsync` rewrites identical state when caller passes a sequence ≤ existing checkpoint, costing a state-store round-trip [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:198-218`] — deferred (perf NIT). Short-circuiting `if (existing?.LastDeliveredSequence >= deliveredSequence) return true;` is a future micro-optimization.
- [x] [Review][Defer] `AggregateActor.GetEventsAsync` casts `(int)(fromSequence + 1)` `checked` (`AggregateActor.cs:596`); a persisted long checkpoint near `int.MaxValue` would now trigger `OverflowException` that the orchestrator silently swallows — deferred, pre-existing actor bug not caused by R11-A1.

## Advanced Elicitation

- Date/time: 2026-05-01T11:35:12+02:00
- Selected story key: `post-epic-11-r11a1-checkpoint-tracked-projection-delivery`
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-11-r11a1-checkpoint-tracked-projection-delivery`
- Batch 1 method names: Self-Consistency Validation; Red Team vs Blue Team; Architecture Decision Records; Pre-mortem Analysis; Failure Mode Analysis
- Reshuffled Batch 2 method names: Security Audit Personas; Comparative Analysis Matrix; Chaos Monkey Scenarios; Occam's Razor Application; Lessons Learned Extraction
- Findings summary:
  - Self-consistency and pre-mortem passes found a skip risk if the implementation saves `checkpoint + count` instead of the highest returned `SequenceNumber`.
  - Architecture and comparative passes found an existing repo precedent for bounded DAPR ETag saves in `DaprCommandActivityTracker` and `DaprStreamActivityTracker`.
  - Failure-mode and chaos passes found the retry-exhaustion case needed to be fail-open, not converted into a blind non-ETag save.
  - Security and lessons passes found the story should explicitly avoid payload logging and should narrow old Story 11-3 "full replay" assertions.
- Changes applied:
  - AC #4 now requires saving the maximum returned event sequence and forbids count-derived checkpoint math.
  - AC #6 and Task 2 now point to the repo's bounded ETag retry pattern and forbid blind fallback saves after retry exhaustion.
  - Task 4 now calls out max-sequence calculation during orchestrator save.
  - Task 5 now requires retry-exhaustion and non-contiguous sequence tests.
  - Dev Notes now cite repo-local optimistic-concurrency precedents, DAPR testability boundaries, and the need to narrow old full-replay assertions.
- Findings deferred:
  - No product-scope or architecture-policy decisions deferred. Exact option names and retry count can be chosen during `bmad-dev-story` while preserving the bounded ETag/non-regression contract above.
- Final recommendation: ready-for-dev

