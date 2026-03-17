# Story 4.2: Resilient Publication & Backlog Draining

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform developer**,
I want event persistence to continue when pub/sub is unavailable, with automatic backlog draining on recovery,
So that pub/sub outages never block the command pipeline.

## Acceptance Criteria

1. **Events persist regardless of pub/sub status** - Given the pub/sub system is temporarily unavailable, When events are persisted by the aggregate actor, Then events are stored successfully in the DAPR state store regardless of pub/sub status (FR20). Command processing returns `Accepted: true` even when publication fails.

2. **CommandStatus transitions to PublishFailed** - Given event publication fails after DAPR retry exhaustion, When the actor pipeline completes, Then the command's advisory status transitions to `PublishFailed` (D2) with failure reason. The `PipelineState` checkpoint records `CommandStatus.PublishFailed`.

3. **UnpublishedEventsRecord created on failure** - Given publication fails, When the actor stores the drain record, Then an `UnpublishedEventsRecord` is persisted in actor state with key `drain:{correlationId}` containing: CorrelationId, StartSequence, EndSequence, EventCount, CommandType, IsRejection flag, FailedAt timestamp, RetryCount=0, LastFailureReason.

4. **DAPR actor reminder registered for drain** - Given an `UnpublishedEventsRecord` is stored, When the publish failure path completes, Then a DAPR actor reminder named `drain-unpublished-{correlationId}` is registered with `InitialDrainDelay` (default 30s) and `DrainPeriod` (default 1min) from `EventDrainOptions`.

5. **Backlog draining on recovery** - Given the pub/sub system recovers and a drain reminder fires, When `ReceiveReminderAsync` executes `DrainUnpublishedEventsAsync`, Then events in the recorded sequence range (StartSequence..EndSequence) are loaded from actor state and re-published via `EventPublisher.PublishEventsAsync`. On success: drain record removed, reminder unregistered, advisory status updated to `Completed` (or `Rejected` if `IsRejection=true`).

6. **Drain retry on continued failure** - Given a drain attempt fails (pub/sub still unavailable), When the reminder handler completes, Then the `UnpublishedEventsRecord.RetryCount` is incremented, `LastFailureReason` updated, record saved, and the reminder continues firing at `DrainPeriod` intervals. The reminder is NOT unregistered.

7. **Crash recovery resumes drain** - Given the actor crashes after `EventsStored` checkpoint but before `EventsPublished`, When the actor reactivates and processes the next command, Then it detects the `EventsStored` pipeline state, attempts to publish, and if publication fails, creates the drain record and registers the reminder (same as normal failure path).

8. **Multiple drain records per aggregate** - Given multiple commands fail publication on the same aggregate, When drain records are stored, Then each gets an independent record (keyed by correlationId) and independent reminder. Drains execute independently per correlationId using the recorded sequence range.

9. **Orphaned reminder handling** - Given a drain reminder fires but no matching `UnpublishedEventsRecord` exists, When the handler loads state, Then a Warning log is emitted and the handler returns gracefully without error.

10. **Configurable drain timing** - Given `EventDrainOptions` bound to `EventStore:Drain` configuration section, Then `InitialDrainDelay` (default 30s), `DrainPeriod` (default 1min), and `MaxDrainPeriod` (default 30min) are configurable.

11. **DAPR resiliency policies handle transient failures** - Given DAPR resiliency policies are configured, When transient pub/sub failures occur, Then DAPR retries with exponential backoff (outbound: 5 retries / 10s max interval production, 3 retries dev) and circuit breaker (trip on >10 consecutive failures production, >5 dev) BEFORE the application-level drain mechanism activates. The drain handles prolonged outages beyond DAPR retry exhaustion.

12. **No event data loss** - Given events are persisted before publication is attempted (ADR-P2), When pub/sub fails and drain eventually succeeds, Then the exact same events (same sequence numbers, same data) are delivered to subscribers. Zero events silently dropped (NFR22, NFR24).

13. **All existing tests pass** - All Tier 1 (baseline: >= 659) and Tier 2 (baseline: >= 1380) tests continue to pass.

### Definition of Done

This story is complete when: all 13 ACs are verified against existing code, any identified test gaps are closed, and no regressions exist in Tier 1 or Tier 2 suites.

### Scope Constraint

**This is primarily a verification story.** The drain/reminder/recovery infrastructure was built during previous epic work. The dev agent should confirm correctness, ensure test coverage is complete, and close any identified test gaps. Do NOT modify any `src/` files unless a gap analysis reveals an actual bug. Do NOT refactor, "improve," or restructure existing code.

## Implementation Status Assessment

**CRITICAL CONTEXT: Story 4.2 is fully implemented.** All resilient publication and backlog draining infrastructure was built during previous epic work. This story is a **verification and gap-analysis pass** — the dev agent should confirm correctness, ensure test coverage is complete, and close identified test gaps.

### Cross-Story Dependencies

- **Story 4.1 (CloudEvents Publication & Topic Routing)** — prerequisite. Provides `EventPublisher` never-throw contract and `EventPublishResult` that triggers the drain path. **Status: done** (5 new gap-closure tests added, all 9 ACs verified).
- **Story 4.3 (Per-Aggregate Backpressure)** — depends on this story's drain infrastructure being verified correct before adding backpressure limits on top.

### Already Implemented

| Component | File | Status | Coverage |
|-----------|------|--------|----------|
| `UnpublishedEventsRecord` | `Server/Actors/UnpublishedEventsRecord.cs` | Complete | State key `drain:{correlationId}`, reminder name `drain-unpublished-{correlationId}`, `IncrementRetry()` |
| `EventDrainOptions` | `Server/Configuration/EventDrainOptions.cs` | Complete | `InitialDrainDelay=30s`, `DrainPeriod=1min`, `MaxDrainPeriod=30min`, bound to `EventStore:Drain` |
| `AggregateActor` publish failure path | `Server/Actors/AggregateActor.cs` L429-506 | Complete | Creates `PublishFailed` checkpoint, stores drain record, registers reminder |
| `AggregateActor.ReceiveReminderAsync` | `Server/Actors/AggregateActor.cs` L510-699 | Complete | Validates reminder name, loads record, loads events by sequence range, re-publishes, success: remove/unregister/status update, failure: increment/save |
| `AggregateActor` crash resume path | `Server/Actors/AggregateActor.cs` L828-889 | Complete | Detects `EventsStored` pipeline state, retries publish, creates drain on failure |
| `RegisterDrainReminderAsync` | `Server/Actors/AggregateActor.cs` L727-788 | Complete | Reads schedule from `EventDrainOptions`, registers DAPR reminder |
| `CommandStatus.PublishFailed` | `Contracts/Commands/CommandStatus.cs` L29-30 | Complete | `PublishFailed = 6` |
| `PipelineState` | `Server/Actors/PipelineState.cs` | Complete | Tracks pipeline stage with `CurrentStage` |
| DAPR resiliency (production) | `deploy/dapr/resiliency.yaml` | Complete | outbound: 5 retries exp backoff 10s, breaker trip >10 |
| DAPR resiliency (development) | `AppHost/DaprComponents/resiliency.yaml` | Complete | outbound: 3 retries exp backoff 10s, breaker trip >5 |
| DI registration | `Server/Configuration/ServiceCollectionExtensions.cs` | Complete | `EventDrainOptions` bound, all drain components wired |

### Existing Test Coverage (24 drain/resilience-related tests)

| Test File | Tier | Tests | Covers |
|-----------|------|-------|--------|
| `EventDrainRecoveryTests.cs` | T2 | 14 | Drain success (re-publish, record removed, reminder unregistered, status updated), drain failure (retry count, record preserved, logs warning, reminder continues), orphaned reminder, multiple unpublished (independent drain, sequence ranges), rejection events, unknown reminder, full drain cycle, multiple failures then success, data integrity, topic correctness |
| `PersistThenPublishResilienceTests.cs` | T2 | 10 | PublishFailed stores drain record, correct sequence range, reminder registered, events still in state store, success has no drain record, resume path (stores record, registers reminder, metadata scenarios) |

### Identified Test Gaps

| Gap | Severity | Notes |
|-----|----------|-------|
| No test for `EventDrainOptions` configuration binding from `IConfiguration` | Low | Options bound via DI; runtime-verifiable |
| No test for `MaxDrainPeriod` cap behavior | Low | `MaxDrainPeriod` is defined but usage in reminder registration should be verified |
| No test confirming drain reminder uses `EventDrainOptions.InitialDrainDelay` and `DrainPeriod` values | Medium | Tests use default options but don't verify the values flow to `RegisterReminderAsync` |
| No end-to-end integration test (command → publish fail → drain reminder → re-publish → subscriber) | Low (deferred) | Requires full DAPR runtime (Tier 3) |

## Tasks / Subtasks

- [ ] Task 0: Verify prerequisites and baseline (BLOCKING)
  - [ ] 0.1 Run all Tier 1 tests — confirm all pass (baseline: >= 659)
  - [ ] 0.2 Run Tier 2 tests `Hexalith.EventStore.Server.Tests` — confirm pass count (baseline: >= 1380)
  - [ ] 0.3 Read `AggregateActor.cs` — verify publish failure path (L429-506): creates `PublishFailed` checkpoint, stores `UnpublishedEventsRecord`, registers drain reminder
  - [ ] 0.4 Read `UnpublishedEventsRecord.cs` — verify record shape, state key format, reminder name format
  - [ ] 0.5 Read `EventDrainOptions.cs` — verify defaults: InitialDrainDelay=30s, DrainPeriod=1min, MaxDrainPeriod=30min

**Note:** Tasks 1-5 are independent verification reads with no code changes. They may be parallelized.

- [ ] Task 1: Verify event persistence continues during pub/sub failure (AC: #1, #12)
  - [ ] 1.1 Confirm `AggregateActor.ProcessCommandAsync` persists events to state store (step 5a: `EventPersister`) BEFORE attempting publication (step 5c: `EventPublisher`) — ADR-P2 ordering
  - [ ] 1.2 Confirm `ProcessCommandAsync` returns `Accepted: true` even when `EventPublishResult.Success == false`
  - [ ] 1.3 Confirm events are available in state store at keys `{tenant}:{domain}:{aggId}:events:{seq}` after publish failure
  - [ ] 1.4 Cross-reference with `PersistThenPublishResilienceTests.ProcessCommand_PublishFailed_EventsStillInStateStore`

- [ ] Task 2: Verify CommandStatus and PipelineState transitions (AC: #2)
  - [ ] 2.1 Confirm `PipelineState` checkpoint with `CommandStatus.PublishFailed` is created on publish failure (L436-444)
  - [ ] 2.2 Confirm advisory `CommandStatusRecord` with `PublishFailed` status and failure reason is written via `ICommandStatusStore`
  - [ ] 2.3 Confirm status write is advisory (failure does not block processing — Rule #12)
  - [ ] 2.4 Verify `CommandStatus.PublishFailed = 6` in `CommandStatus.cs`

- [ ] Task 3: Verify UnpublishedEventsRecord creation and drain reminder (AC: #3, #4, #10)
  - [ ] 3.1 Confirm drain record creation at L459-472: all fields populated correctly (CorrelationId, StartSequence, EndSequence, EventCount, CommandType, IsRejection, FailedAt, RetryCount=0, LastFailureReason)
  - [ ] 3.2 Confirm state key format: `drain:{correlationId}` (via `UnpublishedEventsRecord.GetStateKey`)
  - [ ] 3.3 Confirm `RegisterDrainReminderAsync` at L727-788 registers DAPR reminder with name `drain-unpublished-{correlationId}`
  - [ ] 3.4 Confirm reminder schedule reads from `EventDrainOptions`: `InitialDrainDelay` as dueTime, `DrainPeriod` as period
  - [ ] 3.5 Confirm `EventDrainOptions` bound to configuration section `EventStore:Drain`
  - [ ] 3.6 Verify whether `MaxDrainPeriod` is enforced in `RegisterDrainReminderAsync` (L727-788). If it caps the reminder period, document how. If it is unused/dead config, flag in Dev Notes as cosmetic debt — do NOT fix in a verification story.
  - [ ] 3.7 Cross-reference with `PersistThenPublishResilienceTests.ProcessCommand_PublishFailed_DrainReminderRegistered`

- [ ] Task 4: Verify drain recovery on reminder (AC: #5, #6, #8, #9)
  - [ ] 4.1 Confirm `ReceiveReminderAsync` validates reminder name starts with `drain-unpublished-` (L514-521)
  - [ ] 4.2 Confirm `DrainUnpublishedEventsAsync` loads drain record from state (key: `drain:{correlationId}`)
  - [ ] 4.3 Confirm events loaded from state using exact sequence range (StartSequence to EndSequence). Note: verify behavior when an event in the range is missing from state store (state store data loss edge case) — does the drain skip, fail, or retry? Document the observed behavior in Completion Notes.
  - [ ] 4.4 Confirm events re-published via `EventPublisher.PublishEventsAsync` with original correlationId
  - [ ] 4.5 On success: confirm record removed (`RemoveStateAsync`), reminder unregistered, advisory status updated to `Completed` or `Rejected` based on `IsRejection` flag
  - [ ] 4.6 On failure: confirm `IncrementRetry()` called, updated record saved, reminder NOT unregistered
  - [ ] 4.7 Confirm orphaned reminder (no matching record) logs Warning and returns gracefully
  - [ ] 4.8 Confirm multiple drain records per aggregate are independent (different correlationIds, different sequence ranges)
  - [ ] 4.9 Cross-reference with `EventDrainRecoveryTests` (14 tests)

- [ ] Task 5: Verify crash recovery path (AC: #7)
  - [ ] 5.1 Confirm `AggregateActor` resume logic detects `EventsStored` pipeline state (L828-889)
  - [ ] 5.2 Confirm resume path loads events by sequence range from metadata
  - [ ] 5.3 Confirm resume path attempts publish, and on failure creates drain record + registers reminder
  - [ ] 5.4 Cross-reference with `PersistThenPublishResilienceTests.ResumeFromEventsStored_PublishFails_*` tests (3 tests)

- [ ] Task 6: Verify DAPR resiliency policies (AC: #11)
  - [ ] 6.1 Read `deploy/dapr/resiliency.yaml` — confirm pubsubRetryOutbound (5 retries, exp backoff, 10s max), pubsubBreaker (trip >10)
  - [ ] 6.2 Read `AppHost/DaprComponents/resiliency.yaml` — confirm pubsubRetryOutbound (3 retries, exp backoff, 10s max), pubsubBreaker (trip >5)
  - [ ] 6.3 Confirm no custom retry logic in `EventPublisher.cs` or `AggregateActor.cs` (Rule #4)
  - [ ] 6.4 Cross-reference with `EventPublisherRetryComplianceTests` (4 tests from Story 4.1)

- [ ] Task 7: Close identified test gaps
  - [ ] 7.1 **Add test** `ProcessCommand_PublishFailed_DrainReminderUsesConfiguredTiming` in `PersistThenPublishResilienceTests.cs` — create actor with custom `EventDrainOptions(InitialDrainDelay: 45s, DrainPeriod: 2min)`, trigger publish failure, verify `RegisterReminderAsync` receives reminder with matching dueTime and period values. **Rationale:** Existing tests use default options but never verify the configured timing values flow through to the reminder registration.
  - [ ] 7.2 Run all Tier 1 + Tier 2 tests — confirm no regressions from new test
  - [ ] 7.3 Report final test count delta

- [ ] Task 8: Final verification
  - [ ] 8.1 Confirm all 13 acceptance criteria are satisfied
  - [ ] 8.2 Run `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings
  - [ ] 8.3 Run all Tier 1 tests — pass count >= 659
  - [ ] 8.4 Run all Tier 2 tests — pass count >= 1380
  - [ ] 8.5 If any new tests added, report final test count delta

## Dev Notes

### Architecture Compliance

- **ADR-P2 (Persist-Then-Publish):** Events MUST be persisted to state store BEFORE publishing to pub/sub. The actor pipeline enforces: `EventsStored` checkpoint -> `PublishEventsAsync` -> `EventsPublished` checkpoint. Publish failure triggers drain path; events are already safe in state store.
- **Rule #4 (No Custom Retry):** Application code never retries pub/sub calls. DAPR resiliency handles transient failures (exp backoff + circuit breaker). The drain mechanism handles prolonged outages beyond DAPR retry exhaustion.
- **Rule #12 (Advisory Status):** Command status writes are advisory — failure to write/update status must never block or fail the command processing pipeline. Status is ephemeral metadata, not a source of truth.
- **Rule #11 (Write-Once Events):** Event store keys are immutable. Drain re-publishes the same events; it does not re-persist them.
- **D2 (Command Status Storage):** Status lifecycle: `Received -> Processing -> EventsStored -> EventsPublished -> Completed | Rejected | PublishFailed | TimedOut`. `PublishFailed` is terminal until drain succeeds.

### Key Source Files

| File | Purpose |
|------|---------|
| `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` | Publish failure path (L429-506), drain handler (L510-699), crash resume (L828-889), reminder registration (L727-788) |
| `src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs` | Drain record: state key, reminder name, increment retry |
| `src/Hexalith.EventStore.Server/Actors/PipelineState.cs` | Checkpoint tracking |
| `src/Hexalith.EventStore.Server/Configuration/EventDrainOptions.cs` | Configurable drain timing |
| `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` | Never-throw publication, returns `EventPublishResult` |
| `src/Hexalith.EventStore.Server/Events/EventPublishResult.cs` | Success/failure result record |
| `src/Hexalith.EventStore.Server/Commands/CommandStatusStore.cs` | Advisory status writes |
| `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs` | `PublishFailed = 6` |
| `deploy/dapr/resiliency.yaml` | Production DAPR retry + circuit breaker |
| `src/Hexalith.EventStore.AppHost/DaprComponents/resiliency.yaml` | Development DAPR retry + circuit breaker |
| `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs` | 14 drain recovery tests |
| `tests/Hexalith.EventStore.Server.Tests/Events/PersistThenPublishResilienceTests.cs` | 10 persist-then-publish resilience tests |

### DAPR Resiliency Configuration

**Two-layer failure handling:**
1. **DAPR resiliency policies** (transient failures): Exponential backoff retries + circuit breaker. These fire automatically at the sidecar level before application code sees the failure.
2. **Application-level drain** (prolonged outages): After DAPR retry exhaustion, `EventPublisher` returns `Success=false`, triggering the drain record + reminder mechanism.

**Production (`deploy/dapr/resiliency.yaml`):**
- Outbound: 5 retries, exp backoff, 10s max interval, circuit breaker trips at >10 consecutive failures
- Timeout: 10s per pub/sub call

**Development (`AppHost/DaprComponents/resiliency.yaml`):**
- Outbound: 3 retries, exp backoff, 10s max interval, circuit breaker trips at >5 consecutive failures

### Testing Standards

- **Framework:** xUnit 2.9.3 / Shouldly 4.3.0 / NSubstitute 5.3.0
- **Tier 2 tests** use `NSubstitute.For<IActorStateManager>()` and `NSubstitute.For<IEventPublisher>()` for drain tests
- **Test naming:** `{Method}_{Scenario}_{ExpectedResult}`
- **Actor test setup:** Uses `ActorHost.CreateForTest<AggregateActor>` with reflection to inject mock `StateManager`
- **Timer/reminder verification:** Uses `ActorTimerManager` mock in `CreateActorWithTimerManager()` factory

### Previous Story Intelligence

**Story 4.1 (CloudEvents Publication & Topic Routing)** — status: review:
- Verification story pattern: Implementation Status Assessment table + task list with subtask checkboxes
- Added 5 new gap-closure tests (+2 Facts, +1 Theory with 3 InlineData). Final Tier 2: 1380 passed.
- 1 pre-existing test failure in `DaprSidecarUnavailableHandlerTests.TryHandleAsync_RawRpcExceptionUnavailableWithoutDaprContext_ReturnsFalse` — unrelated to drain/resilience.
- The `EventPublisher` never-throw contract and `EventPublishResult` that Story 4.2 depends on were confirmed correct.

### Git Intelligence

Recent commits (relevant to Story 4.2):
- `50b6e75` — Story 4.1 documentation and sprint status update (includes `EventPublisherTests.cs` gap-closure tests)
- `2698892` — Story 3.6 merge (OpenAPI/Swagger)
- Drain infrastructure was built in earlier epics under old numbering; confirmed present in current codebase

### Known Operational Risks & Limitations

- **Drain storm on mass recovery:** If pub/sub is down for an extended period and many aggregates accumulate drain records, all reminders fire within the `InitialDrainDelay` window on recovery. No jitter or stagger is applied. Multi-replica deployments get natural stagger via DAPR actor placement distribution; single-instance deployments do not. Consider adding jitter in a future story if single-instance drain storms become an operational concern.
- **Unbounded drain record accumulation:** If pub/sub is permanently unavailable and commands keep flowing, drain records accumulate indefinitely per aggregate with no TTL or max-count limit. Each record is < 200 bytes, so storage impact is minimal. A future story could add a max drain record count with dead-letter escalation.
- **Drain re-entrancy:** The drain mechanism is re-entrant by design. If the actor crashes mid-drain after successful re-publish but before `RemoveStateAsync` commits, the next reminder fires and re-publishes the same events (at-least-once duplicate). This is correct behavior under the at-least-once delivery guarantee (FR18). Subscriber idempotency is the consumer's responsibility.
- **No drain-specific OTel metrics:** Warning logs exist for drain failures, but no dedicated OTel activity or metric for "aggregates with pending drains." Operators monitoring drain health must rely on structured log queries. Epic 6 (Observability) should add drain-specific counters/gauges.
- **Source code XML comments reference old epic numbering:** `UnpublishedEventsRecord.cs` and `EventDrainOptions.cs` reference "Story 4.4" (old numbering) instead of "Story 4.2". Cosmetic debt — do NOT fix in a verification story.

### Future Work Cross-References

- **Command status transition UX:** After successful drain, advisory status flips from `PublishFailed` to `Completed`/`Rejected`. API consumers polling status see a non-monotonic transition. No notification mechanism exists for status changes. Consider addressing in Epic 9 (Query Pipeline) or Epic 10 (SignalR Notifications).
- **Operator drain tooling:** No admin API to list pending drain records per aggregate or force-trigger immediate drain. Operators must rely on structured log queries. Consider an operations endpoint in a future Epic 6 (Observability) story.

### Project Structure Notes

All drain-related code aligns with architecture file tree:
- Actor-related in `Server/Actors/` (feature folder)
- Configuration in `Server/Configuration/`
- Events in `Server/Events/`
- DAPR configs in `AppHost/DaprComponents/` and `deploy/dapr/`
- Tests mirror source structure in `Server.Tests/Actors/` and `Server.Tests/Events/`

No file relocations or restructuring needed.

### References

- [Source: architecture.md#ADR-P2] Persist-Then-Publish Event Flow
- [Source: architecture.md#D2] Command Status Storage — PublishFailed terminal status
- [Source: architecture.md#Rule-4] No Custom Retry — DAPR resiliency only
- [Source: architecture.md#Rule-12] Advisory Status — never blocks pipeline
- [Source: epics.md#Story-4.2] Resilient Publication & Backlog Draining acceptance criteria
- [Source: prd.md#FR20] Events persist when pub/sub unavailable, drain backlog on recovery
- [Source: prd.md#NFR22] Zero events lost under any tested failure scenario
- [Source: prd.md#NFR24] After pub/sub recovery, all events delivered via DAPR retry policies
- [Source: prd.md#NFR25] Actor crash after persistence but before publication — checkpointed state machine resumes

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
