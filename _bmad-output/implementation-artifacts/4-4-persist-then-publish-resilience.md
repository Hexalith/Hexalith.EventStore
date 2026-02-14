# Story 4.4: Persist-Then-Publish Resilience

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**BLOCKING: Story 4.3 (At-Least-Once Delivery & DAPR Retry Policies) MUST be implemented before this story.**

Story 4.3 configures DAPR resiliency policies (outbound/inbound retry, circuit breaker, timeouts) for pub/sub delivery. Story 4.1 provides the `EventPublisher` that publishes events via `DaprClient.PublishEventAsync`. Story 4.2 provides `TopicNameValidator` and multi-tenant topic isolation. This story adds the **recovery drain mechanism** -- when pub/sub is unavailable and DAPR retries are exhausted, events remain safe in the state store and are automatically re-published when pub/sub recovers.

Verify these files/classes exist before starting:
- `src/Hexalith.EventStore.Server/Events/IEventPublisher.cs` (Story 4.1 -- event publication interface)
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` (Story 4.1 -- DAPR pub/sub implementation, ZERO custom retry logic)
- `src/Hexalith.EventStore.Server/Events/EventPublishResult.cs` (Story 4.1 -- publication result record)
- `src/Hexalith.EventStore.Server/Events/ITopicNameValidator.cs` (Story 4.2 -- topic validation interface)
- `src/Hexalith.EventStore.Server/Events/TopicNameValidator.cs` (Story 4.2 -- D6 topic name validation)
- `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs` (Stories 4.1/4.3 -- pub/sub config with DeadLetterTopicPrefix)
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (Stories 3.2-4.1 -- 5-step pipeline with publication + PublishFailed handling)
- `src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs` (Story 3.1 -- actor interface)
- `src/Hexalith.EventStore.Server/Actors/ActorStateMachine.cs` (Story 3.11 -- checkpointed stages)
- `src/Hexalith.EventStore.Server/Actors/PipelineState.cs` (Story 3.11 -- in-flight command lifecycle record)
- `src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs` (Story 3.2 -- command deduplication)
- `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` (Story 1.2 -- includes `PubSubTopic` property)
- `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` (Story 3.11 -- OpenTelemetry activity source)
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` (Stories 3.1+ -- DI registrations)
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs` (Stories 4.1/4.2 -- test double with configurable failure modes)
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryStateManager.cs` (Story 1.4 -- test fake for IActorStateManager)
- `src/Hexalith.EventStore.AppHost/DaprComponents/resiliency.yaml` (Story 4.3 -- pub/sub resiliency policies)

Run `dotnet test` to confirm all existing tests pass before beginning. Stories 4.1-4.3 should have ~810-840 tests.

## Story

As a **system operator**,
I want events to remain safe in the state store when the pub/sub system is temporarily unavailable, with automatic drain of the backlog on recovery,
So that events are never lost even during infrastructure outages (FR20, NFR24).

## Acceptance Criteria

1. **Events safe in state store during pub/sub outage** - Given events have been persisted (state machine at EventsStored), When the pub/sub system is unavailable during publication, Then the state machine transitions to PublishFailed (already implemented in Story 4.1), And events remain safely in the state store as write-once keys (D1), And zero events are lost (NFR22).

2. **Unpublished events tracking** - Given event publication fails and the actor transitions to PublishFailed, When the terminal state is committed, Then an `UnpublishedEventsRecord` is stored in actor state with the event sequence range, correlation ID, failure timestamp, and retry count, And the record is committed in the SAME `SaveStateAsync` batch as the PublishFailed terminal state.

3. **DAPR actor reminder registration for drain** - Given an `UnpublishedEventsRecord` is stored after publication failure, When the PublishFailed state is committed, Then a DAPR actor reminder named `drain-unpublished-{correlationId}` is registered, And the reminder fires after an initial configurable delay (default 30 seconds), And the reminder recurs at a configurable period (default 60 seconds) until drain succeeds.

4. **Successful drain re-publishes events** - Given a drain reminder fires and the pub/sub system has recovered, When the actor handles the reminder, Then it loads the `UnpublishedEventsRecord` from actor state, And reads the events from state store using the recorded sequence range, And re-publishes them via `EventPublisher.PublishEventsAsync`, And on success: removes the `UnpublishedEventsRecord`, unregisters the drain reminder, And updates advisory command status from PublishFailed to Completed (or Rejected if rejection events).

5. **Drain handles continued failure** - Given a drain reminder fires but pub/sub is still unavailable, When the re-publication attempt fails, Then the `UnpublishedEventsRecord.RetryCount` is incremented, And the `LastFailureReason` is updated, And the reminder continues to fire at the configured period, And the failure is logged at Warning level with correlation ID, tenant, retry count.

6. **Multiple unpublished commands per aggregate** - Given an aggregate processes multiple commands that all fail publication, When drain reminders fire, Then each unpublished command has its own `UnpublishedEventsRecord` and reminder, And they are drained independently, And draining one command does not affect others.

7. **Resume from EventsStored also registers drain on failure** - Given an actor crashes and resumes from EventsStored stage, When the resume re-publication attempt also fails (pub/sub still down), Then the `ResumeFromEventsStoredAsync` path ALSO stores an `UnpublishedEventsRecord` and registers a drain reminder, And the existing resume path is preserved for immediate retry, but now has drain as a fallback.

8. **Actor does not block waiting for pub/sub** - Given event publication fails, When the actor transitions to PublishFailed, Then the actor immediately returns the failure result to the caller (already implemented), And the drain reminder handles recovery asynchronously, And no command processing is blocked by pending drain operations.

9. **OpenTelemetry tracing for drain operations** - Given a drain reminder fires, When the drain attempt executes, Then an OpenTelemetry activity named `EventStore.Events.Drain` is created, And activity tags include: correlationId, tenantId, domain, aggregateId, retryCount, eventCount, And success/failure status is recorded on the activity.

10. **Structured logging for drain lifecycle** - Given drain operations execute, When logging events, Then drain start/success/failure are logged with: correlationId, tenantId, domain, aggregateId, retryCount, eventCount, And event payload data never appears in logs (SEC-5, NFR12), And drain success is logged at Information level, drain failure at Warning level.

11. **Configuration for drain behavior** - Given the drain mechanism is configurable, When the system starts, Then `EventDrainOptions` provides: `InitialDrainDelay` (default 30s), `DrainPeriod` (default 60s), `MaxDrainPeriod` (default 30min), And options are registered via the DI options pattern bound to `EventStore:Drain` configuration section.

12. **At-least-once guarantee completion** - Given events are persisted to the state store before publication, When publication fails and the drain mechanism eventually succeeds, Then all events persisted during the outage are delivered to subscribers after recovery (NFR24), And the at-least-once guarantee from state store to subscriber is now complete (Story 4.3 retries + Story 4.4 drain), And subscribers handle duplicate deliveries via CloudEvents `id` deduplication (per Story 4.3 subscriber contract).

## Tasks / Subtasks

- [x]Task 0: Verify prerequisites and understand current state (BLOCKING)
  - [x]0.1 Run all existing tests -- they must pass before proceeding
  - [x]0.2 Review `AggregateActor.cs` -- understand the PublishFailed paths (both main flow and `ResumeFromEventsStoredAsync`)
  - [x]0.3 Review `AggregateActor.cs` -- note that `Actor` base class provides `RegisterReminderAsync`, `UnregisterReminderAsync`, and supports `IRemindable` interface
  - [x]0.4 Review `CompletePublishFailedAsync` method -- this is the primary insertion point for storing UnpublishedEventsRecord
  - [x]0.5 Review `LoadPersistedEventsForResumeAsync` -- this method already reads events by sequence range (reuse for drain)
  - [x]0.6 Review `EventPublisher.cs` -- confirm single-call pattern and failure return behavior
  - [x]0.7 Review `FakeEventPublisher.cs` -- understand configurable failure modes for drain testing
  - [x]0.8 Review DAPR actor reminder API: `RegisterReminderAsync(name, state, dueTime, period)` and `IRemindable.ReceiveReminderAsync`

- [x]Task 1: Create UnpublishedEventsRecord (AC: #2, #6)
  - [x]1.1 Create `UnpublishedEventsRecord.cs` in `src/Hexalith.EventStore.Server/Actors/`
  - [x]1.2 Record definition: `public record UnpublishedEventsRecord(string CorrelationId, long StartSequence, long EndSequence, int EventCount, string CommandType, bool IsRejection, DateTimeOffset FailedAt, int RetryCount, string? LastFailureReason)`
  - [x]1.3 Add constant for state key prefix: `public const string StateKeyPrefix = "drain:";`
  - [x]1.4 Add helper: `public static string GetStateKey(string correlationId) => $"{StateKeyPrefix}{correlationId}";`
  - [x]1.5 Add helper: `public static string GetReminderName(string correlationId) => $"drain-unpublished-{correlationId}";`
  - [x]1.6 Add `IncrementRetry` method: `public UnpublishedEventsRecord IncrementRetry(string? failureReason) => this with { RetryCount = RetryCount + 1, LastFailureReason = failureReason };`

- [x]Task 2: Create EventDrainOptions (AC: #11)
  - [x]2.1 Create `EventDrainOptions.cs` in `src/Hexalith.EventStore.Server/Configuration/`
  - [x]2.2 Properties: `TimeSpan InitialDrainDelay { get; init; } = TimeSpan.FromSeconds(30);`
  - [x]2.3 Properties: `TimeSpan DrainPeriod { get; init; } = TimeSpan.FromMinutes(1);`
  - [x]2.4 Properties: `TimeSpan MaxDrainPeriod { get; init; } = TimeSpan.FromMinutes(30);`
  - [x]2.5 Register via `services.AddOptions<EventDrainOptions>().Bind(configuration.GetSection("EventStore:Drain"))` in `ServiceCollectionExtensions.cs`

- [x]Task 3: Add IRemindable to AggregateActor (AC: #3, #4, #5, #9, #10)
  - [x]3.1 Add `IRemindable` interface to `AggregateActor` class declaration
  - [x]3.2 Add `IOptions<EventDrainOptions>` constructor parameter to AggregateActor
  - [x]3.3 Implement `ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)`:
    - If reminderName starts with `"drain-unpublished-"`, extract correlationId and call `DrainUnpublishedEventsAsync`
    - Otherwise, log Warning and ignore unknown reminder
  - [x]3.4 Create private method `DrainUnpublishedEventsAsync(string correlationId)`:
    - Create OpenTelemetry activity `EventStore.Events.Drain` with tags
    - Load `UnpublishedEventsRecord` from actor state using `UnpublishedEventsRecord.GetStateKey(correlationId)`
    - If record not found, unregister the orphaned reminder and return
    - Load events from state store using a dedicated sequence-range loader (`LoadPersistedEventsRangeAsync`) with AggregateIdentity reconstructed from actor ID
    - Call `EventPublisher.PublishEventsAsync` with loaded events
    - On success: remove record, unregister reminder, write advisory status (Completed or Rejected), log Information
    - On failure: increment retry count, save updated record, log Warning, reminder fires again
  - [x]3.5 Helper method to reconstruct `AggregateIdentity` from actor `Host.Id` -- the actor ID contains `{tenant}:{domain}:{aggId}` which was the original identity

- [x]Task 4: Store UnpublishedEventsRecord on PublishFailed (AC: #2, #3, #8)
  - [x]4.1 In the **main ProcessCommandAsync PublishFailed path** (around line 350-392), BEFORE pipeline cleanup, store UnpublishedEventsRecord in actor state
  - [x]4.2 Calculate `startSequence = persistResult.NewSequenceNumber - domainResult.Events.Count + 1`
  - [x]4.3 Calculate `endSequence = persistResult.NewSequenceNumber`
  - [x]4.4 Create record: `new UnpublishedEventsRecord(command.CorrelationId, startSequence, endSequence, domainResult.Events.Count, command.CommandType, domainResult.IsRejection, DateTimeOffset.UtcNow, RetryCount: 0, LastFailureReason: publishResult.FailureReason)`
  - [x]4.5 Store via `StateManager.SetStateAsync(UnpublishedEventsRecord.GetStateKey(command.CorrelationId), record)` -- staged, committed with the same SaveStateAsync
  - [x]4.6 AFTER SaveStateAsync succeeds, register drain reminder: `RegisterReminderAsync(UnpublishedEventsRecord.GetReminderName(command.CorrelationId), null, drainOptions.Value.InitialDrainDelay, drainOptions.Value.DrainPeriod)`
  - [x]4.7 Wrap reminder registration in try/catch -- reminder failure must NOT fail the command processing (defensive, per rule #12 spirit)

- [x]Task 5: Store UnpublishedEventsRecord on ResumeFromEventsStored PublishFailed (AC: #7)
  - [x]5.1 In `CompletePublishFailedAsync` method, add UnpublishedEventsRecord storage
  - [x]5.2 Calculate sequence range from `existingPipeline.EventCount` and aggregate metadata (same pattern as LoadPersistedEventsForResumeAsync)
  - [x]5.3 Store UnpublishedEventsRecord in actor state before SaveStateAsync
  - [x]5.4 Register drain reminder after SaveStateAsync succeeds
  - [x]5.5 Note: `CompletePublishFailedAsync` is called from BOTH the resume path AND can be called from other recovery paths -- the record storage should work for all

- [x]Task 6: Reconstruct AggregateIdentity from ActorId helper (AC: #4)
  - [x]6.1 Create private method `GetAggregateIdentityFromActorId()` in AggregateActor
  - [x]6.2 The ActorId format is `{tenant}:{domain}:{aggId}` (set by CommandRouter when creating the actor proxy)
  - [x]6.3 Parse the actor ID string using `AggregateIdentity.Parse()` or equivalent string splitting
  - [x]6.4 Return a valid `AggregateIdentity` for use in drain operations
  - [x]6.5 If parsing fails, log Error and throw (this indicates a fundamental actor ID corruption)

- [x]Task 7: Create drain operation unit tests (AC: #4, #5, #6, #9, #10)
  - [x]7.1 Create `EventDrainRecoveryTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Actors/`
  - [x]7.2 Test: `ReceiveReminder_DrainSucceeds_EventsRePublished` -- Drain reminder fires, events loaded from state, re-published via FakeEventPublisher, record removed
  - [x]7.3 Test: `ReceiveReminder_DrainSucceeds_ReminderUnregistered` -- Verify reminder is unregistered after successful drain
  - [x]7.4 Test: `ReceiveReminder_DrainSucceeds_AdvisoryStatusUpdated` -- Advisory status updated to Completed after successful drain
  - [x]7.5 Test: `ReceiveReminder_DrainFails_RetryCountIncremented` -- Drain fails, record updated with incremented RetryCount
  - [x]7.6 Test: `ReceiveReminder_DrainFails_RecordPreserved` -- Drain fails, UnpublishedEventsRecord remains in state
  - [x]7.7 Test: `ReceiveReminder_DrainFails_ReminderContinuesFiring` -- Reminder not unregistered on failure
  - [x]7.8 Test: `ReceiveReminder_RecordNotFound_ReminderUnregistered` -- Orphaned reminder cleanup
  - [x]7.9 Test: `ReceiveReminder_MultipleUnpublished_DrainedIndependently` -- Two failed correlations drain independently
  - [x]7.10 Test: `ReceiveReminder_RejectionEvents_DrainedAndStatusRejected` -- Rejection events re-published, status = Rejected (not Completed)
  - [x]7.11 Test: `ReceiveReminder_UnknownReminder_Ignored` -- Non-drain reminder names are ignored with warning log

- [x]Task 8: Create PublishFailed record storage tests (AC: #2, #3, #7)
  - [x]8.1 Create `PersistThenPublishResilienceTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Events/`
  - [x]8.2 Test: `ProcessCommand_PublishFailed_UnpublishedRecordStored` -- Verify UnpublishedEventsRecord is in actor state after PublishFailed
  - [x]8.3 Test: `ProcessCommand_PublishFailed_RecordContainsCorrectSequenceRange` -- Verify startSequence and endSequence match persisted events
  - [x]8.4 Test: `ProcessCommand_PublishFailed_DrainReminderRegistered` -- Verified via ActorTimerManager substitute injected through ActorTestOptions.TimerManager
  - [x]8.5 Test: `ProcessCommand_PublishFailed_EventsStillInStateStore` -- Verify events are readable from state store after PublishFailed (NFR22)
  - [x]8.6 Test: `ProcessCommand_PublishSuccess_NoUnpublishedRecord` -- Verify no drain record stored on success path
  - [x]8.7 Test: `ResumeFromEventsStored_PublishFails_UnpublishedRecordStored` -- Verify resume path also stores drain record
  - [x]8.8 Test: `ResumeFromEventsStored_PublishFails_DrainReminderRegistered` -- Verified via ActorTimerManager substitute injected through ActorTestOptions.TimerManager

- [x]Task 9: Create end-to-end drain recovery tests (AC: #1, #4, #12)
  - [x]9.1 Create tests in `EventDrainRecoveryTests.cs`
  - [x]9.2 Test: `FullDrainCycle_PublishFails_ThenDrainSucceeds_EventsDelivered` -- Command processed, publish fails, drain reminder fires, pub/sub now available, events delivered
  - [x]9.3 Test: `FullDrainCycle_MultipleFails_ThenSuccess_RetryCountAccurate` -- Multiple drain attempts fail, then succeed, retry count reflects actual attempts
  - [x]9.4 Test: `FullDrainCycle_EventsSameAsOriginal_NoDataLoss` -- Events delivered by drain are byte-identical to originally persisted events (NFR22)
  - [x]9.5 Test: `FullDrainCycle_TopicCorrect_MatchesOriginalPublication` -- Drain publishes to same per-tenant-per-domain topic as original would have

- [x]Task 10: Create UnpublishedEventsRecord unit tests (AC: #2)
  - [x]10.1 Add tests in a new or existing test file
  - [x]10.2 Test: `GetStateKey_ReturnsCorrectFormat` -- `drain:{correlationId}`
  - [x]10.3 Test: `GetReminderName_ReturnsCorrectFormat` -- `drain-unpublished-{correlationId}`
  - [x]10.4 Test: `IncrementRetry_IncrementsCount_UpdatesReason` -- Immutable record update
  - [x]10.5 Test: `Construction_AllFieldsPreserved` -- Round-trip construction verification

- [x]Task 11: Create EventDrainOptions tests (AC: #11)
  - [x]11.1 Test: `DefaultValues_CorrectDefaults` -- 30s initial delay, 60s period, 30min max
  - [x]11.2 Test: `ConfigurationBinding_OverridesDefaults` -- Options bind from configuration section

- [x]Task 12: Update existing AggregateActor tests (AC: #8)
  - [x]12.1 Update test setup to provide `IOptions<EventDrainOptions>` mock with default values
  - [x]12.2 Verify all existing tests still pass with the new constructor parameter
  - [x]12.3 Verify all existing PublishFailed tests now also check for UnpublishedEventsRecord in state

- [x]Task 13: Verify all tests pass
  - [x]13.1 Run `dotnet test` to confirm no regressions
  - [x]13.2 All new drain recovery tests pass
  - [x]13.3 All new persist-then-publish resilience tests pass
  - [x]13.4 All new record/options unit tests pass
  - [x]13.5 All existing Story 4.1-4.3 tests still pass

## Dev Notes

### Story Context

This is the **fourth story in Epic 4: Event Distribution & Dead-Letter Handling**. It completes the persist-then-publish resilience guarantee by adding an automatic recovery drain mechanism for events that couldn't be published due to pub/sub outages. Together with Story 4.3 (DAPR retry policies), this story ensures **production-grade at-least-once delivery** (NFR24).

**What Stories 4.1-4.3 already implemented (to BUILD ON, not replicate):**
- `EventPublisher` publishes events via `DaprClient.PublishEventAsync` -- ZERO custom retry logic (rule #4)
- On failure: catches exception, returns `EventPublishResult(false, publishedCount, failureReason)` -- does NOT rethrow
- `AggregateActor` transitions to `PublishFailed` terminal state when publication fails
- `CompletePublishFailedAsync` method handles PublishFailed for both main flow and resume path
- `ResumeFromEventsStoredAsync` handles crash recovery when actor resumes from EventsStored stage
- `LoadPersistedEventsForResumeAsync` reads events from state store by sequence range for resume operations; drain uses `LoadPersistedEventsRangeAsync`
- DAPR resiliency policies: outbound retry (3 local / 5 production), circuit breaker, timeouts (Story 4.3)
- Per-tenant-per-domain topic isolation via `AggregateIdentity.PubSubTopic` (Story 4.2)
- FakeEventPublisher supports configurable failure and partial failure modes

**What this story adds (NEW):**
- `UnpublishedEventsRecord` -- tracks unpublished events with sequence range and retry metadata
- `EventDrainOptions` -- configurable drain timing (initial delay, period, max period)
- `IRemindable` implementation on `AggregateActor` -- DAPR actor reminders for durable drain
- `DrainUnpublishedEventsAsync` -- loads events, re-publishes, cleans up on success
- Unpublished record storage on all PublishFailed paths (main flow + resume)
- Advisory status update after successful drain (PublishFailed -> Completed/Rejected)

**What this story modifies (EXISTING):**
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- add IRemindable, drain handler, unpublished record storage on PublishFailed paths, EventDrainOptions constructor parameter
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` -- register EventDrainOptions
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs` -- add EventDrainOptions mock to test setup
- `tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs` -- add EventDrainOptions mock

### Architecture Compliance

- **FR20:** System continues persisting events when pub/sub is unavailable; drains backlog on recovery
- **NFR22:** Zero events lost under any tested failure scenario
- **NFR24:** After pub/sub recovery, all events persisted during outage delivered to subscribers
- **NFR25:** Actor crash after persistence but before publication: checkpointed state machine resumes from correct stage
- **Rule #4:** No custom retry logic in EventPublisher. The drain mechanism is NOT application-level retry of the publish call -- it's a recovery service that re-reads events from state store and re-publishes. The distinction: retry = "try the same call again"; drain = "load from source of truth and re-execute"
- **Rule #5:** Never log event payload data -- only envelope metadata fields
- **Rule #6:** IActorStateManager for all actor state operations
- **Rule #9:** correlationId in every structured log entry and OpenTelemetry activity
- **Rule #12:** Advisory status writes never block pipeline -- drain status updates are also advisory
- **SEC-5:** Event payload data never in logs
- **D1:** Single-key-per-event storage -- events remain safely at `{tenant}:{domain}:{aggId}:events:{seq}`
- **ADR-P2:** Persist-then-publish event flow: state store is source of truth, pub/sub is distribution

### Critical Design Decisions

- **DAPR actor reminders are the drain mechanism.** Reminders are durable (survive actor deactivation and crashes), per-actor (each aggregate manages its own recovery), self-cleaning (unregistered after success), and built into the DAPR actor framework. No external registry, background service, or global coordinator needed. Each aggregate autonomously recovers its own unpublished events.

- **Drain is NOT application-level retry (rule #4 compliance).** DAPR resiliency policies handle transient pub/sub failures (Story 4.3). The drain mechanism handles prolonged outages where DAPR retries are exhausted and the circuit breaker is open. Drain reads events from the state store (source of truth) and re-publishes them -- it's a recovery workflow, not a retry of a failed call. This distinction is critical for rule #4 compliance.

- **UnpublishedEventsRecord is stored in the SAME atomic batch as PublishFailed.** Using `SetStateAsync` (staging) before `SaveStateAsync` (commit), the drain record is committed atomically with the terminal state. If SaveStateAsync fails, neither the terminal state nor the drain record is written -- consistent state guaranteed.

- **Drain reminder registered AFTER SaveStateAsync succeeds.** Reminder registration is an external side effect (not part of actor state batch). It's registered after the atomic commit succeeds. If reminder registration fails (unlikely), the unpublished record exists but no reminder fires. This is safe: the record is discoverable on next actor activation for manual recovery. Reminder failure is logged but does NOT fail the command.

- **Drain uses dedicated range loading.** Resume operations use `LoadPersistedEventsForResumeAsync` while drain operations use `LoadPersistedEventsRangeAsync` with `StartSequence`/`EndSequence` from `UnpublishedEventsRecord`.

- **AggregateIdentity reconstruction from ActorId.** The actor ID format is `{tenant}:{domain}:{aggId}` (set by CommandRouter in Story 3.1). During drain, we reconstruct an `AggregateIdentity` from the actor's `Host.Id` to derive the correct pub/sub topic and state store keys. If the AggregateIdentity class has a `Parse(string actorId)` static method, use it. Otherwise, split on `:` and construct.

- **Multiple drain records per aggregate.** If an aggregate processes multiple commands that all fail publication (unlikely but possible during prolonged outage), each gets its own `drain:{correlationId}` key and `drain-unpublished-{correlationId}` reminder. They drain independently. DAPR actors are single-threaded, so concurrent drain callbacks are serialized.

- **Advisory status update after drain.** When drain succeeds, write advisory status: `Completed` if events were state-change events, `Rejected` if `UnpublishedEventsRecord.IsRejection` is true. This gives operators visibility that the command ultimately succeeded. The original idempotency record still shows the initial PublishFailed result -- it is NOT updated (idempotency records are the permanent record of the command processing outcome at actor level).

- **Subscribers must be idempotent (per Story 4.3 contract).** If the original publication partially succeeded (events 1-2 published, 3-5 failed), the drain re-publishes ALL events (1-5). Events 1-2 will be delivered twice. Subscribers use the CloudEvents `id` field (`{correlationId}:{sequenceNumber}`) for deduplication. This is a fundamental property of at-least-once delivery.

### Existing Patterns to Follow

**Current PublishFailed handling (main flow, AggregateActor.cs lines 349-392):**
```csharp
// Publication failed: transition to PublishFailed terminal state
var publishFailedState = new PipelineState(..., CommandStatus.PublishFailed, ...);
await stateMachine.CheckpointAsync(pipelineKeyPrefix, publishFailedState).ConfigureAwait(false);

// Cleanup pipeline and commit atomically
await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
    .ConfigureAwait(false);

var failResult = new CommandProcessingResult(Accepted: false, ...);
await idempotencyChecker.RecordAsync(causationId, failResult).ConfigureAwait(false);

await StateManager.SaveStateAsync().ConfigureAwait(false);
```

**INSERT UnpublishedEventsRecord storage BEFORE SaveStateAsync:**
```csharp
// NEW (Story 4.4): Store drain record for recovery
var unpublishedRecord = new UnpublishedEventsRecord(
    command.CorrelationId,
    startSequence,
    endSequence,
    domainResult.Events.Count,
    command.CommandType,
    domainResult.IsRejection,
    DateTimeOffset.UtcNow,
    RetryCount: 0,
    LastFailureReason: publishResult.FailureReason);
await StateManager.SetStateAsync(
    UnpublishedEventsRecord.GetStateKey(command.CorrelationId),
    unpublishedRecord).ConfigureAwait(false);

// EXISTING: Atomic commit (now includes drain record)
await StateManager.SaveStateAsync().ConfigureAwait(false);

// NEW (Story 4.4): Register drain reminder AFTER successful commit
try
{
    await RegisterReminderAsync(
        UnpublishedEventsRecord.GetReminderName(command.CorrelationId),
        null,
        drainOptions.Value.InitialDrainDelay,
        drainOptions.Value.DrainPeriod).ConfigureAwait(false);
}
catch (Exception ex)
{
    logger.LogWarning(ex,
        "Drain reminder registration failed: CorrelationId={CorrelationId}. Manual recovery may be needed.",
        command.CorrelationId);
}
```

**Drain handler pattern:**
```csharp
public async Task ReceiveReminderAsync(
    string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
{
    if (!reminderName.StartsWith("drain-unpublished-", StringComparison.Ordinal))
    {
        logger.LogWarning("Unknown reminder: {ReminderName}", reminderName);
        return;
    }

    string correlationId = reminderName["drain-unpublished-".Length..];
    await DrainUnpublishedEventsAsync(correlationId).ConfigureAwait(false);
}
```

**LoadPersistedEventsForResumeAsync (resume path):**
```csharp
private async Task<IReadOnlyList<EventEnvelope>> LoadPersistedEventsForResumeAsync(
    AggregateIdentity identity, int eventCount)
{
    // Loads events from state store by reading metadata for current sequence,
    // then reading events from (currentSequence - eventCount + 1) to currentSequence
}
```

**DAPR Actor Reminder API (from Dapr.Actors.Runtime):**
```csharp
// Register (from Actor base class):
await RegisterReminderAsync(
    reminderName: "drain-unpublished-cmd-123",
    state: null,           // Optional byte[] passed to callback
    dueTime: TimeSpan.FromSeconds(30),    // First fire
    period: TimeSpan.FromMinutes(1));      // Recurrence

// Unregister:
await UnregisterReminderAsync("drain-unpublished-cmd-123");

// Interface:
public interface IRemindable
{
    Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period);
}
```

**OpenTelemetry activity pattern (from existing code):**
```csharp
using Activity? activity = EventStoreActivitySource.Instance.StartActivity(
    "EventStore.Events.Drain");
activity?.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId);
activity?.SetTag(EventStoreActivitySource.TagTenantId, identity.TenantId);
activity?.SetTag(EventStoreActivitySource.TagDomain, identity.Domain);
activity?.SetTag(EventStoreActivitySource.TagAggregateId, identity.AggregateId);
activity?.SetTag("eventstore.retry_count", record.RetryCount);
activity?.SetTag(EventStoreActivitySource.TagEventCount, record.EventCount);
```

### Mandatory Coding Patterns

- Primary constructors: `public class AggregateActor(..., IOptions<EventDrainOptions> drainOptions)`
- Records for immutable data: `UnpublishedEventsRecord`, `EventDrainOptions`
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions in tests
- Feature folder organization
- **Rule #4:** No custom retry logic in EventPublisher -- the drain mechanism is recovery, not retry
- **Rule #5:** Never log event payload data -- only envelope metadata fields
- **Rule #6:** IActorStateManager for all actor state operations
- **Rule #9:** correlationId in every structured log entry and OpenTelemetry activity
- **Rule #12:** Advisory status writes -- failure logged, never thrown
- **SEC-5:** Event payload data never in logs

### Project Structure Notes

**New files:**
- `src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs` -- drain tracking record
- `src/Hexalith.EventStore.Server/Configuration/EventDrainOptions.cs` -- drain timing configuration
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs` -- drain reminder and recovery tests
- `tests/Hexalith.EventStore.Server.Tests/Events/PersistThenPublishResilienceTests.cs` -- resilience behavior tests
- `tests/Hexalith.EventStore.Server.Tests/Actors/UnpublishedEventsRecordTests.cs` -- record unit tests
- `tests/Hexalith.EventStore.Server.Tests/Configuration/EventDrainOptionsTests.cs` -- options tests

**Modified files:**
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- add IRemindable, EventDrainOptions parameter, drain handler, unpublished record storage on PublishFailed paths
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` -- register EventDrainOptions
- `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` -- add `EventsDrain` activity name constant
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs` -- add EventDrainOptions mock to test setup
- `tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs` -- add EventDrainOptions mock to test setup

### Previous Story Intelligence

**From Story 4.3 (At-Least-Once Delivery & DAPR Retry Policies):**
- Story 4.3 explicitly states: "Production at-least-once requires BOTH Story 4.3 AND Story 4.4"
- "Story 4.4's recovery mechanism handles prolonged outages"
- "Recovery of unpublished events from the state store is handled by Story 4.4's drain mechanism"
- Conservative outbound retries (3 local / 5 production) BECAUSE Story 4.4 handles the long tail
- Circuit breaker fast-fail: when open, actor receives immediate PublishFailed -- perfect trigger for drain
- EventPublisherOptions has `DeadLetterTopicPrefix` and `GetDeadLetterTopic()` (for Story 4.5)
- ~810 tests pass after Story 4.3

**From Story 4.1 (Event Publisher with CloudEvents 1.0):**
- EventPublisher makes single `DaprClient.PublishEventAsync` call per event -- ZERO retry logic
- On failure: returns `EventPublishResult(false, publishedCount, ex.Message)` -- no rethrow
- CloudEvents `id` = `{correlationId}:{sequenceNumber}` -- deterministic dedup key for subscribers
- `EventPersistResult` contains `PersistedEnvelopes` (the events available for publication)
- EventPublisher registered as transient in DI

**From Story 4.2 (Per-Tenant-Per-Domain Topic Isolation):**
- `AggregateIdentity.PubSubTopic` returns `$"{TenantId}.{Domain}.events"` (D6)
- TopicNameValidator validates D6 format compliance
- FakeEventPublisher: `GetPublishedTopics()`, `GetEventsForTopic()`, `AssertNoEventsForTopic()`
- FakeEventPublisher: configurable failure via `SetupFailure()` and `SetupPartialFailure()`

**From Story 3.11 (Actor State Machine):**
- ActorStateMachine checkpoints via `SetStateAsync` (staged), committed by `SaveStateAsync`
- `CompletePublishFailedAsync` handles PublishFailed for both main and resume paths
- `LoadPersistedEventsForResumeAsync` reads events by sequence range from state store
- `ResumeFromEventsStoredAsync` already re-publishes events on resume from crash
- OpenTelemetry activities via `EventStoreActivitySource.Instance.StartActivity()`

**Current AggregateActor constructor (after Story 4.1):**
```csharp
public class AggregateActor(
    ActorHost host,
    ILogger<AggregateActor> logger,
    IDomainServiceInvoker domainServiceInvoker,
    ISnapshotManager snapshotManager,
    ICommandStatusStore commandStatusStore,
    IEventPublisher eventPublisher)
    : Actor(host), IAggregateActor
```
**After this story:**
```csharp
public class AggregateActor(
    ActorHost host,
    ILogger<AggregateActor> logger,
    IDomainServiceInvoker domainServiceInvoker,
    ISnapshotManager snapshotManager,
    ICommandStatusStore commandStatusStore,
    IEventPublisher eventPublisher,
    IOptions<EventDrainOptions> drainOptions)
    : Actor(host), IAggregateActor, IRemindable
```

### AggregateIdentity Reconstruction

The AggregateActor's `Host.Id.GetId()` returns the actor ID string. Review how CommandRouter constructs the actor ID to understand the format. From Story 3.1, the CommandRouter uses `AggregateIdentity.ActorId` which is `{tenant}:{domain}:{aggId}`. So parsing the actor ID:

```csharp
private AggregateIdentity GetAggregateIdentityFromActorId()
{
    string actorId = Host.Id.GetId();
    // AggregateIdentity.ActorId format: "{tenant}:{domain}:{aggId}"
    string[] parts = actorId.Split(':', 3);
    if (parts.Length != 3)
    {
        throw new InvalidOperationException(
            $"Cannot parse actor ID into AggregateIdentity: {actorId}");
    }
    return new AggregateIdentity(parts[0], parts[1], parts[2]);
}
```

Verify the exact format by checking `AggregateIdentity.ActorId` property in `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs`.

### Git Intelligence

Recent commits show the progression through Epic 4:
- `42bcd85` feat: Implement at-least-once delivery and DAPR retry policies (Story 4.3 - in progress)
- `72d7a53` Story 4.1: Event Publisher with CloudEvents 1.0 (#37)
- `226a260` Story 3.11: Actor state machine and checkpointed stages (#36)
- Patterns: Primary constructors, records, ConfigureAwait(false), NSubstitute + Shouldly
- DI registration via `Add*` extension methods in `ServiceCollectionExtensions.cs`
- Feature folder organization throughout
- OpenTelemetry activities with consistent tag naming via `EventStoreActivitySource`

### Testing Requirements

**Drain Recovery Tests (~11 new):**
- Drain succeeds: events re-published, record removed, reminder unregistered, status updated
- Drain fails: retry count incremented, record preserved, reminder continues
- Orphaned reminder: record not found, reminder unregistered
- Multiple unpublished: drained independently
- Rejection events: drained with correct status
- Unknown reminder: ignored

**Persist-Then-Publish Resilience Tests (~6 new):**
- PublishFailed: unpublished record stored, correct sequence range, drain reminder registered
- Events still in state store after PublishFailed (NFR22)
- Success path: no drain record stored
- Resume path: also stores record and registers reminder

**End-to-End Drain Cycle Tests (~4 new):**
- Full cycle: publish fails -> drain succeeds -> events delivered
- Multiple failures then success: retry count accurate
- Events identical after drain: no data loss
- Topic correct after drain: matches original

**Record and Options Tests (~6 new):**
- UnpublishedEventsRecord: key format, reminder name, IncrementRetry, construction
- EventDrainOptions: default values, configuration binding

**Existing Test Updates (~10-20 modified):**
- AggregateActorTests + StateMachineIntegrationTests: add EventDrainOptions mock
- PublishFailed tests: verify drain record stored

**Total estimated: ~27-35 tests (new + modified)**

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 4, Story 4.4]
- [Source: _bmad-output/planning-artifacts/architecture.md#FR20 Persist-then-publish resilience]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR22 Zero events lost]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR24 Pub/sub recovery delivery]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR25 Crash recovery without duplicate persistence]
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-P2 Persist-then-publish event flow]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 4 No custom retry]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 6 IActorStateManager only]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 12 Advisory status writes]
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC-5 Event payload never in logs]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1 Event storage strategy]
- [Source: _bmad-output/implementation-artifacts/4-1-event-publisher-with-cloudevents-1-0.md]
- [Source: _bmad-output/implementation-artifacts/4-2-per-tenant-per-domain-topic-isolation.md]
- [Source: _bmad-output/implementation-artifacts/4-3-at-least-once-delivery-and-dapr-retry-policies.md]
- [Source: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs -- PublishFailed paths (main + resume)]
- [Source: src/Hexalith.EventStore.Server/Events/EventPublisher.cs -- single-call pattern]
- [Source: src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs -- configurable failure modes]
- [Source: DAPR docs: https://docs.dapr.io/developing-applications/building-blocks/actors/actors-timers-reminders/]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

None -- implementation proceeded without debug issues.

### Completion Notes List

- Implemented `UnpublishedEventsRecord` with state key prefix `drain:`, reminder name helper, and immutable `IncrementRetry` method
- Implemented `EventDrainOptions` with configurable `InitialDrainDelay` (30s), `DrainPeriod` (60s), `MaxDrainPeriod` (30min), registered via DI options pattern
- Added `IRemindable` interface and `IOptions<EventDrainOptions>` constructor parameter to `AggregateActor`
- Implemented `ReceiveReminderAsync` -- routes `drain-unpublished-{correlationId}` reminders to `DrainUnpublishedEventsAsync`, ignores unknown reminders
- Implemented `DrainUnpublishedEventsAsync` -- loads events from state store via `LoadPersistedEventsRangeAsync`, re-publishes, cleans up on success (remove record, unregister reminder, advisory status), increments retry on failure
- Added `GetAggregateIdentityFromActorId` helper to reconstruct `AggregateIdentity` from `{tenant}:{domain}:{aggId}` actor ID format
- Added `UnpublishedEventsRecord` storage in main `ProcessCommandAsync` PublishFailed path (committed atomically with terminal state)
- Added `UnpublishedEventsRecord` storage in `CompletePublishFailedAsync` resume path (AC #7)
- Drain reminder registered AFTER `SaveStateAsync` succeeds; reminder failure logged but does not fail command processing
- Added `EventsDrain` activity name constant to `EventStoreActivitySource`
- Registered `EventDrainOptions` in `ServiceCollectionExtensions.AddEventStoreServer`
- Updated all existing test factory methods with `IOptions<EventDrainOptions>` parameter
- 27 new tests covering: drain record unit tests (6), options tests (2), drain recovery tests (15), persist-then-publish resilience tests (6)
- Resolved reminder-registration assertions (8.4 and 8.8) by using DAPR's `ActorTestOptions.TimerManager` property to inject an `ActorTimerManager` NSubstitute mock. This allows direct assertion of `RegisterReminderAsync` calls with the expected `drain-unpublished-{correlationId}` reminder name.
- Current workspace verification (2026-02-14): Story 4.4 targeted test set passed (34/34).

### Change Log

- 2026-02-14: Completed remaining tests 8.4 and 8.8 -- drain reminder registration assertions via `ActorTimerManager` substitute. All tasks complete. Full test suite: 553/553 passed.
- 2026-02-14: Code review remediation applied -- added explicit reminder lifecycle tests (`ReceiveReminder_DrainSucceeds_ReminderUnregistered`, `ReceiveReminder_DrainFails_ReminderContinuesFiring`), added parse-failure error logging in `GetAggregateIdentityFromActorId`, and aligned Story 4.4 documentation to implementation.

### File List

New files:

- src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs
- src/Hexalith.EventStore.Server/Configuration/EventDrainOptions.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/UnpublishedEventsRecordTests.cs
- tests/Hexalith.EventStore.Server.Tests/Configuration/EventDrainOptionsTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs
- tests/Hexalith.EventStore.Server.Tests/Events/PersistThenPublishResilienceTests.cs

Modified files:

- src/Hexalith.EventStore.Server/Actors/AggregateActor.cs
- src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs
- src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/EventPublicationIntegrationTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/MultiTenantPublicationTests.cs
- tests/Hexalith.EventStore.Server.Tests/Events/AtLeastOnceDeliveryTests.cs

Additional currently changed workspace files (outside Story 4.4 implementation scope):

- .markdownlintignore
- deploy/dapr/accesscontrol.yaml
- deploy/dapr/pubsub-kafka.yaml
- deploy/dapr/pubsub-rabbitmq.yaml
- deploy/dapr/resiliency.yaml
- deploy/dapr/statestore-cosmosdb.yaml
- deploy/dapr/statestore-postgresql.yaml
- deploy/dapr/subscription-sample-counter.yaml
- samples/Hexalith.EventStore.Sample/Program.cs
- src/Hexalith.EventStore.AppHost/DaprComponents/accesscontrol.yaml
- src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml
- src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml
- src/Hexalith.EventStore.AppHost/DaprComponents/subscription-sample-counter.yaml
- src/Hexalith.EventStore.Server/Events/DeadLetterMessage.cs
- src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs
- src/Hexalith.EventStore.Server/Events/IDeadLetterPublisher.cs
- src/Hexalith.EventStore.Testing/Fakes/FakeDeadLetterPublisher.cs
- src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/DeadLetterRoutingTests.cs
- tests/Hexalith.EventStore.Server.Tests/Configuration/ResiliencyConfigurationTests.cs
- tests/Hexalith.EventStore.Server.Tests/Configuration/SubscriptionConfigurationTests.cs
- tests/Hexalith.EventStore.Server.Tests/Events/DeadLetterMessageTests.cs
- tests/Hexalith.EventStore.Server.Tests/Events/DeadLetterPublisherTests.cs
- tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs
- tests/Hexalith.EventStore.Server.Tests/Events/FakeDeadLetterPublisherTests.cs
- tests/Hexalith.EventStore.Server.Tests/Security/AccessControlPolicyTests.cs
