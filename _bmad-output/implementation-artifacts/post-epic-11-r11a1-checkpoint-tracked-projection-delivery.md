# Post-Epic-11 R11-A1: Checkpoint-Tracked Projection Delivery

Status: done

<!--
2026-05-01 re-review reopened this story (done → in-progress) on a CRITICAL finding: the
incremental delivery path silently corrupts any projection handler that rebuilds state from
scratch (the canonical example is `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs`).
Current-pass remediation chose the conservative safety path: immediate delivery reverts to full replay
(`GetEventsAsync(0)`) while retaining checkpoint writes, and per-aggregate projection updates are serialized.
Closure is gated on the carve-out sibling story `post-epic-11-r11a1b-incremental-projection-contract-decision`
making the binary architectural choice for future incremental reads (extend ProjectionRequest with prior state vs
require incremental-aware handlers vs another sequence-aware projection write contract). Original 11-3 ADR-3
explicitly chose full-replay for correctness.
Patches P1 (outer OCE filter), P2 (no-op save short-circuit), P3 (drop misleading `_ =`), and current-pass
correctness/test/doc patches applied and validated by 60/60 targeted tests + 0/0 Server build.
-->


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

3. **Immediate projection delivery reads incrementally.** `ProjectionUpdateOrchestrator.UpdateProjectionAsync` reads the checkpoint before calling the aggregate actor and calls `GetEventsAsync(lastDeliveredSequence)` instead of `GetEventsAsync(0)`. When no checkpoint exists, the first delivery uses sequence 0 and behaves like the current full-replay bootstrap. **[Resolved by R11-A1b: full-replay-permanent — immediate delivery permanently calls `GetEventsAsync(0)` to preserve rebuild-from-scratch projection handlers; checkpoint state is retained for coordination/observability and future polling, not for trimming reads.]**

4. **Checkpoint is saved only after successful projection write.** The tracker updates `LastDeliveredSequence` to the highest `SequenceNumber` returned by the aggregate actor only after the domain service returns a valid `ProjectionResponse` and `IProjectionWriteActor.UpdateProjectionAsync` completes successfully. Do not infer the new checkpoint from `lastDeliveredSequence + events.Length`; sequence gaps, filtered reads, or future event-shape changes must not create an off-by-one skip. Resolver failures, aggregate read failures, HTTP failures, invalid projection responses, and actor write failures do not advance the checkpoint.

5. **At-least-once semantics are preserved.** If a projection update fails after events are read but before checkpoint save, a later trigger resends the same events from the previous checkpoint. Duplicate delivery is acceptable; silent event skipping is not. **[Resolved by R11-A1b: full-replay-permanent — every trigger resends from sequence 0; at-least-once is trivially preserved by full-replay-on-every-trigger.]**

6. **Checkpoint writes never regress.** A delayed or duplicate projection update cannot lower a checkpoint that already advanced to a higher sequence for the same aggregate. Prefer the same bounded ETag retry shape already used by `DaprCommandActivityTracker` / `DaprStreamActivityTracker`: `GetStateAndETagAsync`, merge with `Math.Max(existing.LastDeliveredSequence, proposedSequence)`, then `TrySaveStateAsync`. If the save cannot win after the bounded retry budget, log and leave the checkpoint unchanged rather than falling back to a blind write.

7. **Command processing remains fail-open.** Projection checkpoint failures are logged and swallowed inside the projection update path. Checkpoint read failure falls back to sequence 0 for that update so delivery may replay but cannot skip events. Checkpoint save failure leaves the previous checkpoint unchanged so a later trigger retries the same events. These failures must not make `EventPublisher.PublishEventsAsync` report failure and must not cause command submission or event publication to fail. **[Resolved by R11-A1b: full-replay-permanent — the immediate path no longer reads the checkpoint, so checkpoint read failures cannot affect delivery. Checkpoint save fail-open behavior remains as specified.]**

8. **Projection wire contract stays unchanged.** `ProjectionRequest`, `ProjectionResponse`, `ProjectionEventDto`, `ProjectionState`, `IProjectionWriteActor`, and `EventReplayProjectionActor` remain compatible. Domain services still receive only public projection event fields and still own projection apply logic.

9. **Polling mode remains deliberately deferred.** `RefreshIntervalMs > 0` keeps the current `PollingModeDeferred` behavior. This story may make checkpoint state usable by a future poller, but it must not implement `ProjectionPollerService` or change polling product behavior; that is `post-epic-11-r11a2-polling-mode-product-behavior`.

10. **Tests pin the incremental path.** Unit coverage proves: missing checkpoint uses `GetEventsAsync(0)`; existing checkpoint `N` uses `GetEventsAsync(N)`; checkpoint read failure replays from `0` without throwing; success saves the maximum returned sequence; no-events does not save; every failure class in AC #4 leaves the checkpoint unchanged; checkpoint save failure is swallowed after the projection state write; duplicate or stale saves cannot lower the checkpoint. **[Resolved by R11-A1b: full-replay-permanent — `existing checkpoint N uses GetEventsAsync(N)` is intentionally inverted: existing-checkpoint and missing-checkpoint cases both use `GetEventsAsync(0)`. Pinned by `UpdateProjectionAsync_ImmediateDelivery_AlwaysReadsFullHistory`, `_ExistingCheckpoint_ReplaysFromZeroUntilIncrementalContractIsResolved`, and `_CheckpointReadFails_ReplaysFromZeroWithoutThrowing`.]**

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

### Verification (2026-05-01, dev-story re-entry)

Re-entered via `/bmad-dev-story` to re-verify the three patches applied during the 2026-05-01 re-review pass (P1 outer OCE filter, P2 no-op save short-circuit, P3 dropped misleading discard). No code/test changes in this re-entry — verification only.

**Patch confirmation:**

- **P1 (outer OCE filter)** — `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:151` reads `catch (Exception ex) when (ex is not OperationCanceledException)`. Confirmed.
- **P2 (no-op save short-circuit)** — `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:60-62` short-circuits `if (existing is not null && existing.LastDeliveredSequence >= deliveredSequence) return true;` before any `TrySaveStateAsync` call. Confirmed.
- **P3 (dropped misleading discard)** — `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:106` is a side-effect statement `httpResponse.EnsureSuccessStatusCode();` (no `_ =` prefix). Confirmed.

**Test runs (Debug build, no Docker required):**

- `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~ProjectionUpdateOrchestratorTests|FullyQualifiedName~ProjectionCheckpoint"` → **31/31 PASS** (678 ms, 0 failed, 0 skipped).
- `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~EventReplayProjectionActorTests|FullyQualifiedName~AggregateActorGetEventsTests|FullyQualifiedName~ProjectionUpdateOrchestratorRefreshIntervalTests"` → **26/26 PASS** (130 ms, 0 failed, 0 skipped).

**Server build (Release, TreatWarningsAsErrors=true):**

- `dotnet build src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj --configuration Release` → **0 warnings, 0 errors** (2.30 s).

**Closure status:**

All 7 tasks remain `[x]`. The three Re-Review (2026-05-01) patches remain `[x]` and are visible in the codebase. Story closure remains gated on the single CRITICAL Decision-Needed finding in the Re-Review section: rebuild-from-scratch projection handlers (canonical: `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs:21-36`) are silently corrupted by incremental delivery because `EventReplayProjectionActor.cs:43` overwrites without merge and `ProjectionRequest` does not carry prior projection state. Per project precedent (post-epic-2-r2a5 -> r2a5b, post-epic-4-r4a2 -> r4a2b), the binary architectural decision (full-replay revert vs. extend `ProjectionRequest` with prior state vs. require incremental-aware handlers) belongs in the sibling carve-out story `post-epic-11-r11a1b-incremental-projection-contract-decision`, currently `backlog` (spec not yet authored). r11a1 cannot transition `in-progress -> review` until r11a1b authors -> develops -> merges. Recommended next action: `/bmad-create-story post-epic-11-r11a1b-incremental-projection-contract-decision`.

### R11-A1b Closure (2026-05-01)

Closed by `post-epic-11-r11a1b-incremental-projection-contract-decision` with chosen path `full-replay-permanent` at `7c7ce7e`. The orchestrator uses `GetEventsAsync(0)` by design for immediate and polling projection delivery, preserving rebuild-from-scratch domain handlers while checkpoint writes remain available for polling coordination and observability.

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

## Review Findings (Re-Review 2026-05-01)

Date/time: 2026-05-01 (independent re-review via `/bmad-code-review post-epic-11-r11a1-checkpoint-tracked-projection-delivery`)
Layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor (independent — prior Review Findings section was NOT consulted as ground truth)

### Decision-Needed

- [x] [Review][Decision] **Counter sample (and any rebuild-from-scratch projection handler) is silently corrupted by incremental delivery** — RESOLVED in current pass by reverting immediate delivery to full replay (`GetEventsAsync(0)`) while retaining checkpoint writes. Future incremental delivery remains gated on `post-epic-11-r11a1b-incremental-projection-contract-decision`.
  **Closed by R11-A1b at `7c7ce7e`** with chosen path: `full-replay-permanent`.

### Patch

- [x] [Review][Patch] Outer `catch (Exception ex)` in `UpdateProjectionAsync` does not filter `OperationCanceledException`, while the inner checkpoint-read and checkpoint-save catches DO [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:151`] — APPLIED. Outer catch now reads `catch (Exception ex) when (ex is not OperationCanceledException)`. Pinned by new test `UpdateProjectionAsync_OperationCanceledException_PropagatesThroughOuterCatch` in `ProjectionUpdateOrchestratorTests.cs`.
- [x] [Review][Patch] `SaveDeliveredSequenceAsync` performs a no-op write (with refreshed `UpdatedAt`) when the caller passes a `deliveredSequence` ≤ existing checkpoint, burning all 3 ETag retries and emitting a misleading `CheckpointSaveExhausted` warning under contention [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:56`] — APPLIED. Tracker now short-circuits `if (existing is not null && existing.LastDeliveredSequence >= deliveredSequence) return true;` after the ETag read, before any `TrySaveStateAsync` call. Existing `_ExistingHigherCheckpoint_KeepsMaximumSequence` test renamed to `_ExistingHigherCheckpoint_SkipsSaveAndReturnsTrue` (now asserts `DidNotReceiveWithAnyArgs.TrySaveStateAsync`); new sibling `_ExistingEqualCheckpoint_SkipsSaveAndReturnsTrue` test added for the duplicate-delivery shape.
- [x] [Review][Patch] `_ = httpResponse.EnsureSuccessStatusCode();` reads as accidentally-discarded [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:106`] — APPLIED. Discard removed; the call now stands as a side-effect statement.

### Defer

- [x] [Review][Defer] Default `HttpClient` from unnamed factory has 100s default timeout and no Polly handler [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:104`, `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:36`] — Spec did not require named/typed client. Precedent: `Hexalith.EventStore.Admin.Server` uses `CreateClient(_serverOptions.EventStoreAppId)` with explicit `client.Timeout = TimeSpan.FromSeconds(5)`. Track in projection hardening follow-up.
- [x] [Review][Defer] HTTP 5xx response handling is silent: `EnsureSuccessStatusCode()` throws → outer `catch (Exception)` logs Warning; no metric increment, no per-aggregate failure counter, no escalation [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:106,151`] — Behavior matches AC ("invalid response no-save"). Operational signal-loss out of R11-A1 scope.
- [x] [Review][Defer] Empty `ProjectionType` early return at `:109` causes infinite re-fetch on next trigger with no negative-cache, no escalation [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:109`] — Matches AC #4 "invalid response no-save"; signal-loss is the same concern as the 5xx case.
- [x] [Review][Defer] No backoff/jitter in retry loop + retry count of 3 + unbounded fan-out from `EventPublisher.PublishEventsAsync` (`Task.Run`) → ETag exhaustion under realistic burst load [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:47-91`] — Matches `DaprCommandActivityTracker` precedent (no backoff, retry=3) which the spec mandates. Concurrent-trigger serialization explicitly out of scope per story prior deferred section.
- [x] [Review][Defer] Final-attempt exception in retry loop escapes as exception (logged as `CheckpointSaveFailed`) rather than `false` return (logged as `CheckpointSaveExhausted`) — same root cause emits two different `Stage=` tags depending on whether storage failed via mismatch or via throw [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:85-91`, `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:142,146`] — Observability quirk; alerting that keys on `Stage=ProjectionCheckpointSaveExhausted` will miss half the exhaustion population.
- [x] [Review][Defer] Concurrent first-time saves on the same key (both see `existing == null`, `etag == ""`) not pinned by a Tier 2 test [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:49-71`] — In real DAPR (Redis), empty etag means create-only-if-not-exists; the loser of a race needs round 2 to converge. Add a Tier 2 race test in a follow-up.
- [x] [Review][Defer] Sequence overflow: `AggregateActor.GetEventsAsync` casts `(int)(fromSequence + 1)` `checked` (`src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:596-597`); a persisted long checkpoint near `int.MaxValue` would now trigger `OverflowException` swallowed by the outer catch forever — pre-existing actor bug; new persistent checkpoint preserves the bad state across restarts. Already in story prior deferred section.
- [x] [Review][Defer] Generic `catch (Exception ex)` in retry loop (with `when` filter on attempts) catches OOM/JsonException/ArgumentException at Debug log level [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:85-91`] — Matches `DaprCommandActivityTracker` precedent which the spec mandates.
- [x] [Review][Defer] No content-type / charset validation before `ReadFromJsonAsync` — a misconfigured downstream returning `text/html` (e.g., proxy error page with 200 OK) throws `NotSupportedException` swallowed by the outer catch [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:107`] — Defensive corner case.
- [x] [Review][Defer] `JsonResponseHandler` and `HttpClient` instances created in `CreateHttpClientFactory` test helper are not disposed across the test suite [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs:961-974`] — Test hygiene only; xUnit process exit cleans up.
- [x] [Review][Defer] Tracker tests for missing-key save use `(null!, "etag-1")` instead of `(null, "")` — fictional shape [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs:457-459, 397`] — Real DAPR returns empty etag for missing keys; tests pass functionally because `TrySaveStateAsync` is mocked, but the empty-etag-create-only-if-not-exists semantics are not pinned.
- [x] [Review][Defer] `new DaprClientBuilder().Build()` in unit tests couples to DAPR SDK URL composition [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs:830,859,890,918`] — Pre-existing pattern documented in spec Dev Notes.
- [x] [Review][Defer] `ProjectionCheckpoint` does not persist `LastDeliveredCorrelationId` / `LastDeliveredEventTypeName` — admin tooling cannot answer "which command/event last advanced this projection?" without joining the event stream [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpoint.cs:11-16`, `ProjectionCheckpointTracker.cs:57-62`] — Out of R11-A1 scope; future enhancement.

### Dismissed (with rationale)

- `JsonElement` not `Clone()`d before `using HttpResponseMessage` exits — REFUTED. `ProjectionState.FromJsonElement` calls `JsonSerializer.SerializeToUtf8Bytes(state)` synchronously at `src/Hexalith.EventStore.Server/Actors/ProjectionState.cs:33` — the bytes are materialized before the awaited `UpdateProjectionAsync` is invoked, so the underlying JsonDocument cannot be disposed during the read window. The defensive `Clone()` removed in this diff was not load-bearing.
- `events.Max(...)` may differ from projected events if `projectionEvents` is a subset — REFUTED. `ProjectionUpdateOrchestrator.cs:86-96` builds `projectionEvents` 1:1 from `events` with no filtering; `events == projectionEvents` in count and order.
- No encoding on tenant/domain/aggregate IDs that may contain `:` → key collision risk — REFUTED. `AggregateIdentity` constructor regex (`AggregateIdentity.cs:107-113, 131-132`) forbids `:` in all components; the documented 4-layer isolation model relies on this (`:50-54`).
- `internal static GetStateKey` requires `InternalsVisibleTo` "silent surface expansion" — code organization note, not a bug; tests compile, attribute is present.
- `events[^1].SequenceNumber` is "more efficient" than `events.Max(...)` — DISMISSED. AC #4 explicitly mandates `events.Max(e => e.SequenceNumber)` "not from event count or checkpoint offset"; switching to indexed access would violate the spec literal.

### Acceptance Auditor result

All 12 ACs assessed as Met (independent re-audit). The CRITICAL finding above sits at the *integration* boundary between R11-A1 (orchestrator semantics changed) and R11-A4 (valid round-trip proof) — neither story alone surfaces it. The Auditor focused on AC literal text against the diff; the integration regression only becomes visible when the diff is composed with `CounterProjectionHandler.cs` (out of the diff scope) and `EventReplayProjectionActor.cs` (overwrite semantics).

### Review Findings (Current Pass 2026-05-01)

- [x] [Review][Decision] Incremental delivery corrupts rebuild-from-scratch projection handlers — RESOLVED by conservative full-replay safety patch. `ProjectionUpdateOrchestrator` now calls `GetEventsAsync(0)` for immediate delivery while retaining checkpoint writes after successful projection writes. Future incremental reads remain gated on `post-epic-11-r11a1b-incremental-projection-contract-decision`.
- [x] [Review][Decision] Concurrent projection triggers can regress projection state while the checkpoint remains advanced — RESOLVED by per-aggregate serialization in `ProjectionUpdateOrchestrator`; concurrent fire-and-forget triggers for the same `AggregateIdentity.ActorId` now execute one at a time; full-replay-permanent contract under R11-A1b makes the regression scenario impossible by construction (no delta-only delivery).
- [x] [Review][Decision] Checkpoints are scoped per aggregate, not per projection state lifecycle or projection type — RESOLVED for current immediate delivery by full replay: checkpoint state no longer trims immediate event reads, so projection actor state reset/type changes cannot skip historical events in this path. Future incremental/polling scope remains part of the sibling contract decision.
- [x] [Review][Decision] Checkpoint-ahead-of-stream drift has no recovery policy — RESOLVED for current immediate delivery by full replay: the stored checkpoint is no longer passed to `GetEventsAsync` for immediate projection updates. Future incremental/polling recovery policy remains part of the sibling contract decision.
- [x] [Review][Patch] Validate persisted checkpoint identity before trusting `LastDeliveredSequence` [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:26`] — tracker now rejects mismatched `TenantId`, `Domain`, or `AggregateId` with `InvalidOperationException`, pinned by `ReadLastDeliveredSequenceAsync_MismatchedCheckpointIdentity_ThrowsInvalidOperationException`.
- [x] [Review][Patch] Add invalid-projection-response coverage for null/undefined state [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs:330`] — added `UpdateProjectionAsync_InvalidProjectionResponseWithNullState_DoesNotSaveCheckpoint`.
- [x] [Review][Patch] Add retry coverage for transient checkpoint storage exceptions [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs:230`] — added `SaveDeliveredSequenceAsync_TransientStorageException_RetriesUntilSaveSucceeds`.
- [x] [Review][Patch] Pin missing-checkpoint save behavior with the realistic empty ETag shape [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs:51`] — missing-checkpoint save test now uses `string.Empty` ETag and asserts it is passed to `TrySaveStateAsync`.
- [x] [Review][Patch] Projection builder documentation overstates idempotency while the handler-state contract is unresolved [`docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md:84`] — docs now state immediate delivery uses full replay while checkpoints remain available for future incremental/polling work, and that delta-only projection requests require a future contract decision.

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

### Review Findings (Re-Re-Review 2026-05-01)

Date/time: 2026-05-01 (independent re-review via `/bmad-code-review post-epic-11-r11a1-checkpoint-tracked-projection-delivery`, full-file scope over the 10 files in current File List)
Layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor (independent — prior findings sections were not consulted as ground truth)
Acceptance Auditor result: All 12 ACs Met or Drifted-but-reconciled with R11-A1b. Note that AC #3, #5, #7, and #10 still carry the original incremental-delivery wording while code/tests/docs implement `full-replay-permanent`; this is editorial drift, not a correctness gap.

#### Patch

- [x] [Review][Patch] Static `s_projectionLocks` ConcurrentDictionary leaks `SemaphoreSlim` per aggregate forever [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:39,65`] — Per-aggregate serialization (added in this story to close the concurrent-trigger state-regression decision) keys a `SemaphoreSlim` by `identity.ActorId` (`tenant:domain:aggregateId`) and never removes or disposes it. In a multi-tenant system with many short-lived aggregates the dictionary grows unbounded and each `SemaphoreSlim` holds kernel handles. Add an eviction strategy (LRU / TTL / refcount on `Release` when wait queue empty) and `Dispose` evicted semaphores. Add a regression test that pumps N distinct identities and asserts the dictionary count stays bounded.
- [x] [Review][Patch] Identity-index writes are non-atomic read-modify-write on shared state-store keys [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:131-249`] — `TrackIdentityAsync` / `AddScopeAsync` / `AddIdentityAsync` / `AppendScopeAsync` / `AppendIdentityAsync` use plain `SaveStateAsync` (no ETag, no transaction). Two concurrent first-time `TrackIdentityAsync` calls for the same scope can lose data, create duplicate scope/identity entries, orphan a page when the index save races the page save, or compound a corrupted `LastPageCount`. Apply the same ETag-guarded retry pattern as `SaveDeliveredSequenceAsync` (or use `ExecuteStateTransactionAsync` for the page+index pair). Add a `Task.WhenAll` regression test that registers N parallel identities for the same scope and asserts no duplicates and exactly N entries.
- [x] [Review][Patch] `GetRefreshIntervalMs` is non-deterministic when two domain keys differ only by case [`src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs:31-43,57-65`] — `Domains` is a case-sensitive `Dictionary<string,…>`; lookup falls back to a case-insensitive linear scan whose order is implementation-defined. If config contains both `Counter` and `counter`, the case-sensitive miss + foreach scan picks an arbitrary winner. Either reject case-collision keys in `Validate()` (e.g. via a `HashSet<string>(StringComparer.OrdinalIgnoreCase)` uniqueness check) or construct `Domains` with `StringComparer.OrdinalIgnoreCase`. Add a test that registers two case-colliding domains and asserts validation rejects them.
- [x] [Review][Patch] `SaveDeliveredSequenceAsync` short-circuit silently accepts a stored record whose identity does not match the requested `AggregateIdentity` [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:75-77`] — `ReadLastDeliveredSequenceAsync` treats identity mismatch as data corruption and throws `InvalidOperationException`; the save path reads the same record and returns `true` without checking identity. Inconsistent semantics between read and save. Either both throw on mismatch or both ignore. Recommend validating identity in the save path before the `LastDeliveredSequence >=` short-circuit, mirroring the read-path guard.
- [x] [Review][Patch] Orchestrator tests never assert the HTTP request body sent to `/project` [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`] — Fake HTTP handlers ignore `request.Content`. `ProjectionEventDto_MapsOnlyPublicFields_FromEventEnvelope` re-implements the orchestrator's mapping inside the test and asserts the test's own mapping output — that is duplicated production code, not verification. Capture `request.Content` in at least one happy-path orchestrator test, deserialize as `ProjectionRequest`, and assert tenant/domain/aggregateId/event-count match the inputs.
- [x] [Review][Patch] AC #3, #5, #7, and #10 still carry the original incremental-delivery wording [`_bmad-output/implementation-artifacts/post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md`] — Code (`ProjectionUpdateOrchestrator.cs:87-89` calls `GetEventsAsync(0)` always), tests (`UpdateProjectionAsync_ImmediateDelivery_AlwaysReadsFullHistory`, `_ExistingCheckpoint_ReplaysFromZeroUntilIncrementalContractIsResolved`), and docs (`docs/superpowers/specs/2026-03-15-...:80-103`) all implement `full-replay-permanent` per the R11-A1b carve-out. The AC literal text was not updated. Annotate each impacted AC with `[Resolved by R11-A1b: full-replay-permanent]` or rewrite the AC bodies. Editorial only; functionally nothing changes.

#### Defer

- [x] [Review][Defer] CancellationToken not propagated to `IAggregateActor.GetEventsAsync` and `IProjectionWriteActor.UpdateProjectionAsync` [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:87-89,143-145`] — Pre-existing pattern; `EventPublisher` passes `CancellationToken.None` today, so production cancellation is not affected. Track in projection-hardening follow-up.
- [x] [Review][Defer] `events.Max(e => e.SequenceNumber)` does not assert the `IAggregateActor` monotonic-ordering contract [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:147`] — Max is order-independent for the saved checkpoint, but if events arrive out-of-order the projection handler is given them out-of-order, which is an upstream-contract concern not visible from R11-A1's diff.
- [x] [Review][Defer] Tenant-isolation test exercises only one tenant rather than asserting cross-tenant resolver/actor independence [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorRefreshIntervalTests.cs:212-236`] — Test improvement, not a correctness bug. A two-tenant interleaved test would harden the assertion.
- [x] [Review][Defer] Identity-page deserialization (`JsonException`) not handled in `ReadStateAsync<T>` [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:251-257`] — A schema-drifted persisted entry would halt `EnumerateTrackedIdentitiesAsync` for all subsequent identities. Polling-scope concern (R11-A2).
- [x] [Review][Defer] Page-count math drift recovery missing [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:222-249`] — If the persisted `LastPageCount` exceeds `IdentityPageSize` due to a prior race or hand-edit, `Append*Async` silently appends to an oversized page. A defensive `if (LastPageCount > IdentityPageSize) throw` would surface the corruption instead of compounding it. Polling-scope concern.
- [x] [Review][Defer] `RefreshIntervalMs` upper bound not validated [`src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs:53-55,62-64`] — `int.MaxValue` (~24 days) is accepted as valid. A sane upper bound (e.g. 24h) and a warn-near-it check would catch obvious misconfiguration.
- [x] [Review][Defer] Identity-mismatch `InvalidOperationException` from `ReadLastDeliveredSequenceAsync` is silently swallowed by the orchestrator's outer `catch` [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:43-46` → `ProjectionUpdateOrchestrator.cs:162`] — Under R11-A1b the orchestrator does not call `ReadLastDeliveredSequenceAsync` on the immediate path, but a future poller will; the "loud" data-corruption signal is functionally a Warning log. Observability follow-up: emit a dedicated `Stage=ProjectionCheckpointIdentityCorruption` metric so alerting can fire.
- [x] [Review][Defer] `Validate(...)` lambda lets `InvalidOperationException` from `o.Validate()` escape rather than returning `false` [`src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:68-71`] — Pattern-consistent with `SnapshotOptions` registration on the same file. `ValidateOnStart` will surface the unwrapped exception type rather than `OptionsValidationException`. Tidying nit.

#### Dismissed (with rationale)

- Singleton tracker reads `IOptions<ProjectionOptions>.Value` — REFUTED. `IOptions<T>` is by-design startup-only; if hot-reload were needed the project would already use `IOptionsMonitor`. Pattern-consistent with the rest of the codebase.
- DI ambiguity Transient orchestrator + static lock dictionary — REFUTED. Static state across orchestrator instances is the deliberate design for cross-instance per-aggregate serialization (R11-A1 Re-Review decision); see Patch #1 for the resulting leak.
- Logging `TenantId`/`AggregateId` is PII — REFUTED. Project pattern across all DAPR loggers; CLAUDE.md does not require redaction. Tenant identifiers are non-sensitive in this product.
- Off-by-one "retry {Attempt}" log message — DISMISSED. Mirrors `DaprCommandActivityTracker` precedent which the spec mandates.
- `s_projectionLocks` `s_` prefix vs `_camelCase` editor convention — DISMISSED. Build is green with `TreatWarningsAsErrors=true`; project allows the `s_` prefix for static fields by convention.
- `WaitAsync` cancelled then `Release` in `finally` — REFUTED. `WaitAsync` is invoked outside the `try` block (`:66`); if it throws, the `finally` does not run, so no `SemaphoreFullException` from releasing an unowned semaphore.
- `GetEventsAsync` returns null array; null event in array — REFUTED. `IAggregateActor` proxy contract returns `Task<EventEnvelope[]>` with non-null array, non-null elements. Defensive coding not warranted.
- `events.Max` on empty array — DISMISSED. Self-acknowledged safe by E45 due to local-array immutability after the `events.Length == 0` guard at `:91`.
- Negative `SequenceNumber` — DISMISSED. Contract violation upstream; `SaveDeliveredSequenceAsync` would throw `ArgumentOutOfRangeException` and be caught by the outer fail-open path.
- `RefreshIntervalMs` upper-bound `int.MaxValue` overflow downstream — DISMISSED here (covered by Defer #6 as a config-validation concern, not a correctness bug).
- Test relies on static `s_projectionLocks` state across tests — DISMISSED. Tests use distinct ULID-based aggregate identities; cross-test pollution is mitigated by key disjointness.
- OCE test does not cancel `CancellationToken` — DISMISSED. The test pins "outer catch does not absorb OCE." Whether the OCE is thrown directly by the substitute or originates from a cancelled token, both flow through the same code path.
- Domain param null in `GetRefreshIntervalMs` — DISMISSED. Caller passes `identity.Domain` which `AggregateIdentity` constructor enforces non-null/non-empty.
- `Domains` may be null after deserialization — DISMISSED. Property initializer (`= []`) plus configuration binding never produces a null map.
- Charset / large response / content-type — covered by spec's existing Defer section (lines 290-298).

## Review Findings (Re-Re-Re-Review 2026-05-01)

Date/time: 2026-05-01 (independent triple-layer re-review via `/bmad-code-review post-epic-11-r11a1-checkpoint-tracked-projection-delivery`, full File List scope over 10 files, 2273 lines)
Layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor (independent — prior findings sections were not consulted as ground truth)
Acceptance Auditor result: All 12 ACs Met or Drifted-but-reconciled with R11-A1b `full-replay-permanent`. Editorial drift on AC #3, #5, #7, #10 unchanged from prior Re-Re-Review.
Findings counts (post-triage): 0 decision-needed, 7 patch, 17 defer, 5 dismissed.

### Patch

- [x] [Review][Patch] `s_projectionLocks` ConcurrentDictionary leaks `SemaphoreSlim` per aggregate forever — confirms Re-Re-Review Patch #1 still open [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:39,65`] — Per-aggregate serialization keys a `SemaphoreSlim` by `identity.ActorId` (`tenant:domain:aggregateId`) and never removes/disposes. In a multi-tenant system with many short-lived aggregates the dictionary grows unbounded and each `SemaphoreSlim` holds kernel handles. Apply refcount-on-release with `TryRemove` + `Dispose` when no waiters (preferred), or LRU/TTL eviction. Add a regression test that pumps N distinct identities and asserts the dictionary count returns to baseline. Confirmed by all three review layers (Blind, Edge, Auditor).
- [x] [Review][Patch] Identity-index writes are non-atomic read-modify-write on shared state-store keys — confirms Re-Re-Review Patch #2 still open [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:121-249`] — `TrackIdentityAsync` / `AddScopeAsync` / `AddIdentityAsync` / `AppendScopeAsync` / `AppendIdentityAsync` use plain `SaveStateAsync` (no ETag, no transaction). Concurrent first-time `TrackIdentityAsync` calls for the same scope can lose data, double-append entries, corrupt `LastPageCount`, or orphan a page when the index save races the page save. Apply ETag-guarded retry mirroring `SaveDeliveredSequenceAsync`, or use `ExecuteStateTransactionAsync` for the page+index pair. Add a `Task.WhenAll` regression test that registers N parallel identities for the same scope and asserts no duplicates and exactly N entries. Subsumes the BH8/EC10 page-overflow drift and the EC16 `TrackIdentityAsync`-not-in-lock burst exposure.
- [x] [Review][Patch] `EnumerateTrackedIdentitiesAsync` crashes the entire poll tick on a single corrupted page with null `Identities`/`Scopes` array [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:147,164`] — `foreach (var x in identityPage.Identities)` raises `NullReferenceException` outside the per-identity try/catch on a `{"Scopes": null}` record (manual debug, partial restore, schema drift), tearing down `ProjectionPollerService.PollOnceAsync` for the entire tick. Coalesce reads with `?? []` on `Scopes`/`Identities`, or enforce constructor non-null guards on the records. Genuinely new finding (Edge Case Hunter only — not in prior review sections).
- [x] [Review][Patch] `GetRefreshIntervalMs` is non-deterministic when two domain keys differ only by case — confirms Re-Re-Review Patch #3 still open [`src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs:31-43,57-65`] — `Domains` uses default ordinal-case-sensitive comparer; on miss, falls back to a foreach scan whose iteration order is implementation-defined, so two case-colliding domain keys (`Counter` vs `counter`) silently pick a non-deterministic winner. `Validate()` does not detect case-collision. Either reject case-collision keys in `Validate()` via a `HashSet<string>(StringComparer.OrdinalIgnoreCase)` uniqueness check, or build the dictionary with `OrdinalIgnoreCase` at bind time. Add a test that registers two case-colliding domains and asserts validation rejects them.
- [x] [Review][Patch] `SaveDeliveredSequenceAsync` short-circuit silently accepts a stored record whose identity does not match the requested `AggregateIdentity` — confirms Re-Re-Review Patch #4 still open [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:75-77`] — `ReadLastDeliveredSequenceAsync:43-46` throws `InvalidOperationException` on identity mismatch (data-corruption signal), but the save path reads the same record and returns `true` from the `LastDeliveredSequence >=` short-circuit without checking identity. Asymmetric semantics between read and save. Mirror the read-path identity guard before the save-path short-circuit — either both throw or both ignore.
- [x] [Review][Patch] Orchestrator tests never assert the HTTP request body sent to `/project` — confirms Re-Re-Review Patch #5 still open [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs:559-599,675-680,682-699`] — `JsonResponseHandler` ignores `request.Content`. `ProjectionEventDto_MapsOnlyPublicFields_FromEventEnvelope` re-implements the orchestrator's mapping inside the test and asserts the test's own output (duplicated production code, not verification). `CountingProjectionResponseHandler` deserializes `ProjectionRequest` from the request body but only counts events for the response — does not Shouldly-assert tenant/domain/aggregateId/SequenceNumber set. Add a happy-path test that captures `request.Content`, deserializes as `ProjectionRequest`, and asserts every field including the exact `SequenceNumber` set matches the orchestrator's `events` input.
- [x] [Review][Patch] AC #3, #5, #7, and #10 still carry the original incremental-delivery wording — confirms Re-Re-Review Patch #6 still open [`_bmad-output/implementation-artifacts/post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md`] — Code (`ProjectionUpdateOrchestrator.cs:87-89` always passes 0), tests (`UpdateProjectionAsync_ImmediateDelivery_AlwaysReadsFullHistory`, `_ExistingCheckpoint_ReplaysFromZeroUntilIncrementalContractIsResolved`, `_CheckpointReadFails_ReplaysFromZeroWithoutThrowing`), and docs (`docs/superpowers/specs/2026-03-15-...:80-103`) all implement `full-replay-permanent` per the R11-A1b carve-out. AC literal text was not annotated. Annotate each impacted AC with `[Resolved by R11-A1b: full-replay-permanent]` or rewrite the AC bodies. Editorial only; functionally moot.

### Defer

- [x] [Review][Defer] `Validate(o => { o.Validate(); return true; }, "...")` lambda pattern is misleading — final error string is dead code [`src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:64-71`] — Pattern-consistent with `SnapshotOptions` registration on the same file; refactor to `IValidateOptions<T>` (matching `ValidateProjectionChangeNotifierOptions`) would touch both. Tidying nit.
- [x] [Review][Defer] HTTP 5xx / 4xx responses logged as generic `ProjectionUpdateFailed` Warning with stack trace [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:117-128,162`] — Already in spec defer §"Defer" #2 (5xx silence). High noise, no distinction between transient retry-able and domain bugs. Operational signal-loss out of R11-A1 scope.
- [x] [Review][Defer] `AddIdentityAsync` linear-scan duplicate detection is O(N pages × IdentityPageSize) state reads per polling-mode publish [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:209-215`] — Hash-set / bloom-filter / per-identity existence marker key would short-circuit. Polling-scope perf concern; out of R11-A1 scope.
- [x] [Review][Defer] `IProjectionCheckpointTracker.ReadLastDeliveredSequenceAsync` has no production caller under R11-A1b full-replay-permanent [`src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs:11-15`, `ProjectionCheckpointTracker.cs:43-47`] — Method is reserved for future polling-mode reads; orchestrator tests assert `DidNotReceiveWithAnyArgs.ReadLastDeliveredSequenceAsync`. Either remove or document as observability-only. Decision out of R11-A1 scope.
- [x] [Review][Defer] `SaveDeliveredSequenceAsync` ETag retry final-attempt logs at Debug only [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:108-115`] — `CheckpointSaveExhausted` Warning fires on `saved == false` (ETag mismatch path); a thrown final-attempt storage exception escapes to the orchestrator's outer catch (`ProjectionUpdateFailed` Warning) but no tracker-level Warning distinguishes "hot key" from "store outage". Already in spec defer §"Defer" #5. Observability quirk.
- [x] [Review][Defer] Tracker test mocks for `GetStateAsync` omit `consistencyMode`/`metadata` argument matchers [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs:36-40,81-85`] — Some tests already use `Arg.Any<ConsistencyMode?>()`/`Arg.Any<IReadOnlyDictionary<string,string>>()` (see `:234-236`). If production code passes any non-null `consistencyMode`/`metadata` argument now or in the future, these tests would silently miss the call. Test hygiene noise.
- [x] [Review][Defer] Unused `ReadLastDeliveredSequenceAsync` mock setups in orchestrator tests camouflage intent [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs:67-68,202-203,227-229,254-255,299-300,379-380,411-412,467-468,495-496`] — `DidNotReceiveWithAnyArgs` assertions confirm production never calls them; mock setups are noise but make a future re-introduction silent rather than loud.
- [x] [Review][Defer] `_DomainServiceFails_DoesNotThrow` test relies on `DaprClient.CreateInvokeMethodRequest` substitute behavior to produce the failure path [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs:321-339`] — `CreateInvokeMethodRequest` is virtual and may return a default `HttpRequestMessage` rather than throwing; the test's "no exception escapes" assertion holds for the wrong reason. Explicitly arranging `httpClientFactory.CreateClient()` to return an `HttpClient` with a throwing handler would make the failure mode deterministic. Test rigor improvement.
- [x] [Review][Defer] `projectionEvents` array allocates O(events.Length) every immediate trigger and full HTTP body has no upper bound [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:97-107`] — Design choice locked by R11-A1b `full-replay-permanent`. For hyper-active aggregates a snapshot mechanism would be needed. Out of R11-A1 scope.
- [x] [Review][Defer] `Domains.TryGetValue` exact-match fallback to O(n) case-insensitive `foreach` scan per command [`src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs:31-43`] — Perf nit subsumed by P4 fix when `OrdinalIgnoreCase` comparer path is chosen; standalone if Validate-rejection path is chosen.
- [x] [Review][Defer] Slow domain-service `/project` endpoint stalls per-aggregate `SemaphoreSlim` for `HttpClient` default 100s; head-of-line blocking under poller bursts [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:65-167`] — Per-aggregate serialization is the explicit trade-off accepted in story Re-Re-Review decision (concurrent-trigger state-regression mitigation). Trigger coalescing / `WaitAsync(TimeSpan)` budget out of R11-A1 scope.
- [x] [Review][Defer] `IHttpClientFactory.CreateClient()` default 100s timeout, no `MaxResponseContentBufferSize`, no charset enforcement [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:115-118`] — Already in spec defer §"Defer" #1. Named/typed client with explicit budget belongs to projection-hardening follow-up.
- [x] [Review][Defer] `ReadFromJsonAsync<ProjectionResponse>` returns null on `Content-Type: text/plain` or empty body, falls through as `InvalidProjectionResponse` "ProjectionType is null or empty" [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:118-123`] — Already in spec defer §"Defer" #9. Distinct telemetry for "200 with non-JSON" vs "ProjectionType missing" out of scope.
- [x] [Review][Defer] `DateTimeOffset.UtcNow` for `ProjectionCheckpoint.UpdatedAt` is non-monotonic under clock skew / VM pause / DST transitions [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:85`] — `TimeProvider` is already in DI (`ServiceCollectionExtensions.cs:50`); injection improvement is a follow-up. Operators should not treat `UpdatedAt` as authoritative ordering.
- [x] [Review][Defer] `AggregateActor.GetEventsAsync` `(int)(fromSequence + 1)` checked cast overflows on aggregates with > `int.MaxValue` events [`src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:596-597`] — Pre-existing actor bug; persistent checkpoint preserves the bad state across restarts. Already in spec defer §"Defer" #7.
- [x] [Review][Defer] `s_projectionLocks` keys are case-sensitive ordinal but `AggregateIdentity` only lowercases tenant+domain (AggregateId case is preserved) [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:39,65`] — Case-variant `AggregateId` would acquire two different locks for one logical aggregate; downstream DAPR state-store case-handling is backend-dependent. Subsumed by P1 if case-normalization is included; otherwise standalone.
- [x] [Review][Defer] No tests assert (a) two parallel `DeliverProjectionAsync` calls for the same identity execute serially, (b) cancellation of first does not strand the second, (c) `s_projectionLocks` count returns to baseline [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`] — Test gap; regression risk on `WaitAsync` cancellation semantics if P1 changes lock acquire/release shape.

### Dismissed (with rationale)

- Polling registration `OperationCanceledException` escapes via `EventPublisher` fire-and-forget callsite — DISMISSED. The `when (ex is not OperationCanceledException)` filter on the `TrackIdentityAsync` catch is intentional; cooperative cancellation must propagate so the caller can unwind cleanly during shutdown. Inner OCE filters in checkpoint-read/save paths follow the same intentional pattern. Pinned by `UpdateProjectionAsync_OperationCanceledException_PropagatesThroughOuterCatch`.
- Outer-catch swallow of post-write checkpoint failures has no Warning-level visibility — REFUTED. `Log.CheckpointSaveFailed` (Warning, EventId 1119) and `Log.CheckpointSaveExhausted` (Warning, EventId 1120) already cover both failure shapes at Warning level; the outer `Log.ProjectionUpdateFailed` (Warning, EventId 1115) covers everything else. Visibility is sufficient for operator alerting.
- `events.Max(e => e.SequenceNumber)` should be replaced with `events[^1].SequenceNumber` — DISMISSED. AC #4 explicitly mandates `events.Max(e => e.SequenceNumber)` "not from event count or checkpoint offset"; switching to indexed access would violate the spec literal. Already dismissed in prior Re-Re-Review section.
- `ProjectionResponse.State` accepts JSON scalars/booleans/numbers/strings without rejection — REFUTED. `ProjectionState` stores opaque bytes per design (`ProjectionState.cs:33` `JsonSerializer.SerializeToUtf8Bytes`); the domain service owns the projection state shape and may legitimately return scalar/array values. The `JsonValueKind.Null/Undefined` guard is the only invariant required; tighter validation would over-constrain the contract.
- `projectionLock.Release()` over-release theoretical race between `WaitAsync` return and `try` block opening — DISMISSED. The pattern is canonical for .NET 10 where `ThreadAbortException` injection is no longer a concern; defensive `bool acquired = false` would be churn against current language guarantees. `WaitAsync` is invoked outside the `try` block (`:66`); if it throws, the `finally` does not run and no `SemaphoreFullException` from releasing an unowned semaphore can occur.
