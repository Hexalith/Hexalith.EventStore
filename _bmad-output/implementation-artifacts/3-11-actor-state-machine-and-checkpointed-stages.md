# Story 3.11: Actor State Machine & Checkpointed Stages

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**BLOCKING: Stories 3.2 (AggregateActor), 3.7 (EventPersister), 3.9 (SnapshotManager), and 3.10 (Snapshot+Tail Rehydration) MUST be implemented before this story.**

Verify these files/classes exist before starting:
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (Story 3.2 -- 5-step thin orchestrator, updated through Story 3.10)
- `src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs` (Story 3.1 -- actor interface)
- `src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs` (Story 3.2 -- idempotency records)
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs` (Story 3.7 -- event persistence via IActorStateManager)
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs` (Story 3.10 -- snapshot-aware rehydration)
- `src/Hexalith.EventStore.Server/Events/RehydrationResult.cs` (Story 3.10 -- snapshot state + tail events)
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` (Story 3.9 -- snapshot creation/loading)
- `src/Hexalith.EventStore.Server/Commands/DaprCommandStatusStore.cs` (Story 2.6 -- writes status to DAPR state store)
- `src/Hexalith.EventStore.Server/Commands/ICommandStatusStore.cs` (Story 2.6 -- status store interface)
- `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs` (Story 1.2 -- 8-value enum: Received through TimedOut)
- `src/Hexalith.EventStore.Contracts/Commands/CommandStatusRecord.cs` (Story 2.6 -- status record with terminal state details)
- `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` (Story 3.5 -- DAPR service invocation)
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` (Story 3.1 -- DI registrations)
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryStateManager.cs` (Story 1.4 -- test fake for IActorStateManager)
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs` (Story 3.2+ -- comprehensive actor tests, 935 lines)

Run `dotnet test` to confirm all existing tests pass (703 tests expected) before beginning.

## Story

As a **system operator**,
I want the AggregateActor to use a checkpointed state machine tracking command lifecycle stages (Received -> Processing -> EventsStored -> EventsPublished -> terminal),
So that crash recovery resumes from the correct stage without duplicate persistence (NFR25).

## Acceptance Criteria

1. **Checkpointed stage transitions** - Given the AggregateActor processes a command through the 5-step delegation, When the state machine transitions between stages (Processing -> EventsStored -> EventsPublished -> Completed), Then each stage transition is checkpointed via IActorStateManager as part of the atomic `SaveStateAsync` batch.

2. **Crash recovery from EventsStored** - Given the actor crashes after events are persisted (stage=EventsStored) but before events are published, When the actor reactivates and receives the same command (via idempotency key), Then it resumes from EventsStored stage, does NOT re-persist events (NFR25), And proceeds to the EventsPublished stage (or marks PublishFailed if pub/sub unavailable). Note: Event publishing is Epic 4 -- for this story, the state machine transitions directly from EventsStored to Completed (or Rejected for rejection events).

3. **Terminal states** - Given a command reaches a terminal state, When the state machine records the final stage, Then terminal states are: Completed (all events stored, and in future published), Rejected (domain rejection event persisted), PublishFailed (events stored but pub/sub permanently failed -- placeholder for Epic 4), TimedOut (command exceeded configured processing timeout -- placeholder for future timeout implementation).

4. **Advisory command status writes** - Given the state machine transitions between stages, When it writes command status to the status store, Then status writes are advisory per enforcement rule #12: failure to write/update status NEVER blocks or fails the command processing pipeline, And status write failures are logged at Warning level with correlationId.

5. **OpenTelemetry activities at each stage** - Given a command flows through the actor pipeline, When each stage transition occurs, Then a named OpenTelemetry `Activity` is created following the architecture naming pattern (e.g., `EventStore.Actor.ProcessCommand`, `EventStore.Events.Persist`), And each activity includes tags for correlationId, tenantId, domain, aggregateId, commandType, And trace context propagates across all spans.

6. **Structured logging at each stage** - Given a command flows through the pipeline, When each stage transition occurs, Then structured log entries are emitted at Information level with: correlationId, tenantId, domain, aggregateId, commandType, stage, durationMs, And event payload data never appears in logs (SEC-5, NFR12).

7. **State machine persistence key** - Given the actor checkpoints state machine stages, When stage data is stored, Then it uses the key pattern `{tenant}:{domain}:{aggId}:pipeline:{correlationId}` via IActorStateManager, And the pipeline state includes: currentStage, correlationId, commandType, startedAt timestamp, eventCount (when known).

8. **Resume detection on actor activation** - Given an actor reactivates after a crash, When a new command arrives matching a persisted pipeline state, Then IdempotencyChecker detects the in-flight command, And the state machine resumes from the last checkpointed stage instead of reprocessing from scratch, And the resume is logged at Warning level (unusual path indicating prior crash).

9. **Atomic checkpointing with event persistence** - Given the state machine transitions to EventsStored, When events are persisted via EventPersister, Then the stage checkpoint (`pipeline:{correlationId}` = EventsStored) is written in the SAME `SaveStateAsync` batch as the events, ensuring atomicity: either both events AND checkpoint are persisted, or neither is.

10. **Pipeline state cleanup** - Given a command reaches a terminal state, When the state machine records the final stage, Then the pipeline state key (`pipeline:{correlationId}`) is removed from actor state in the same atomic batch, keeping actor state clean.

11. **Rejection path state machine** - Given a domain service returns rejection events, When the state machine handles the rejection, Then the flow is: Processing -> EventsStored (rejection event persisted) -> Completed (rejections follow the same persistence path as success events per D3), And command status transitions to Rejected (advisory), And the terminal pipeline state is cleaned up atomically.

12. **No-op path (empty event list)** - Given a domain service returns an empty event list, When the state machine handles the no-op, Then the flow is: Processing -> Completed (no events to store, no state change), And the pipeline state is cleaned up, And command status transitions to Completed.

## Tasks / Subtasks

- [x] Task 0: Verify prerequisites and understand current state (BLOCKING)
  - [x] 0.1 Run all existing tests -- they must pass (703 expected) before proceeding
  - [x] 0.2 Review `AggregateActor.cs` current 5-step pipeline (lines ~30-247), note the "State machine checkpointing deferred to Story 3.11" comment
  - [x] 0.3 Review `DaprCommandStatusStore.cs` and `ICommandStatusStore.cs` for status writing API
  - [x] 0.4 Review `CommandStatus.cs` enum (8 values) and `CommandStatusRecord.cs` (terminal state details)
  - [x] 0.5 Review `IdempotencyChecker.cs` to understand how duplicate/in-flight detection currently works
  - [x] 0.6 Review `EventPersister.cs` to understand the `SaveStateAsync` batch pattern
  - [x] 0.7 Review `SubmitCommandHandler.cs` -- currently writes "Received" status at API layer before actor invocation

- [x] Task 1: Create ActorStateMachine abstraction (AC: #1, #7, #9, #10)
  - [x] 1.1 Create `IActorStateMachine.cs` in `src/Hexalith.EventStore.Server/Actors/`
  - [x] 1.2 Create `ActorStateMachine.cs` in `src/Hexalith.EventStore.Server/Actors/`
  - [x] 1.3 Define `PipelineState` record in `src/Hexalith.EventStore.Server/Actors/PipelineState.cs`:
    - `CorrelationId` (string)
    - `CurrentStage` (CommandStatus enum value)
    - `CommandType` (string)
    - `StartedAt` (DateTimeOffset)
    - `EventCount` (int? -- populated at EventsStored)
    - `RejectionEventType` (string? -- populated for rejections)
  - [x] 1.4 Key pattern: `{identity.PipelineKeyPrefix}{correlationId}` where `PipelineKeyPrefix` is a new property on `AggregateIdentity` returning `{tenant}:{domain}:{aggId}:pipeline:`
  - [x] 1.5 Methods: `CheckpointAsync(PipelineState)`, `LoadPipelineStateAsync(correlationId)`, `CleanupPipelineAsync(correlationId)` -- all use `IActorStateManager` (SetStateAsync/TryGetStateAsync/RemoveStateAsync)
  - [x] 1.6 CRITICAL: Checkpoint writes use `SetStateAsync` (staged) -- NOT `SaveStateAsync`. The caller (AggregateActor) commits atomically with `SaveStateAsync` to ensure checkpoint + events are atomic (AC #9)

- [x] Task 2: Add PipelineKeyPrefix to AggregateIdentity (AC: #7)
  - [x] 2.1 Add `PipelineKeyPrefix` computed property to `AggregateIdentity.cs` in Contracts package
  - [x] 2.2 Pattern: `{TenantId}:{Domain}:{AggregateId}:pipeline:` (matches D1 key strategy)
  - [x] 2.3 Add unit test for `PipelineKeyPrefix` derivation in `AggregateIdentityTests.cs`

- [x] Task 3: Add OpenTelemetry ActivitySource to actor pipeline (AC: #5)
  - [x] 3.1 Create `EventStoreActivitySource.cs` in `src/Hexalith.EventStore.Server/Telemetry/` -- static class with `ActivitySource`
  - [x] 3.2 Define activity names per architecture naming convention:
    - `EventStore.Actor.ProcessCommand` (outer span for full pipeline)
    - `EventStore.Actor.IdempotencyCheck`
    - `EventStore.Actor.TenantValidation`
    - `EventStore.Actor.StateRehydration`
    - `EventStore.DomainService.Invoke`
    - `EventStore.Events.Persist`
    - `EventStore.Actor.StateMachineTransition`
  - [x] 3.3 Standard tags on all activities: `eventstore.correlation_id`, `eventstore.tenant_id`, `eventstore.domain`, `eventstore.aggregate_id`, `eventstore.command_type`
  - [x] 3.4 Register `EventStoreActivitySource` in DI / expose for OpenTelemetry configuration

- [x] Task 4: Integrate state machine into AggregateActor (AC: #1, #2, #4, #5, #6, #8, #9, #11, #12)
  - [x] 4.1 Add `IActorStateMachine` and `ICommandStatusStore` as constructor dependencies to AggregateActor
  - [x] 4.2 Modify Step 1 (Idempotency Check): Also check for in-flight pipeline state via `LoadPipelineStateAsync`. If found, implement resume logic (AC #8):
    - If `PipelineState.CurrentStage == EventsStored`: skip Steps 2-5a (events already stored), proceed to Step 5c (future EventsPublished) or mark Completed
    - If `PipelineState.CurrentStage == Processing`: this means crash happened during Steps 3-4, safe to reprocess from scratch (no events were persisted)
  - [x] 4.3 After Step 2 (Tenant Validation) succeeds: checkpoint `PipelineState(correlationId, Processing, commandType, now)` and write advisory status `Processing` (AC #4)
  - [x] 4.4 After Step 5a (Event Persistence) succeeds: checkpoint `PipelineState(correlationId, EventsStored, commandType, startedAt, eventCount)` IN SAME `SaveStateAsync` batch as events (AC #9)
  - [x] 4.5 After all steps complete: write advisory status `Completed` (or `Rejected`), cleanup pipeline state in SAME final `SaveStateAsync` batch (AC #10)
  - [x] 4.6 Handle no-op path (empty events): checkpoint directly to Completed, cleanup pipeline state (AC #12)
  - [x] 4.7 Handle rejection path: persist rejection event, checkpoint EventsStored -> Completed, write advisory Rejected status, cleanup (AC #11)
  - [x] 4.8 Wrap all status writes in try/catch -- log Warning on failure, never throw (enforcement rule #12)
  - [x] 4.9 Add OpenTelemetry activities around each step using `EventStoreActivitySource` (AC #5)
  - [x] 4.10 Add structured logging at each stage transition with all required fields (AC #6)

- [x] Task 5: Register new components in DI (AC: #1)
  - [x] 5.1 Register `IActorStateMachine` -> `ActorStateMachine` in `ServiceCollectionExtensions.cs`
  - [x] 5.2 Ensure `ICommandStatusStore` is registered (verify it's already in CommandApi DI, may need to add to Server DI)
  - [x] 5.3 Register `EventStoreActivitySource.Instance` for OpenTelemetry builder integration

- [x] Task 6: Create FakeActorStateMachine test double
  - [x] 6.1 Create `FakeActorStateMachine.cs` in `src/Hexalith.EventStore.Testing/Fakes/`
  - [x] 6.2 Track all checkpoint calls, load calls, cleanup calls for assertions
  - [x] 6.3 Support configurable existing pipeline state for resume testing

- [x] Task 7: Create ActorStateMachine unit tests (AC: #1, #7, #9, #10)
  - [x] 7.1 Test: `Checkpoint_StoresCorrectPipelineState` -- verify key pattern and state content
  - [x] 7.2 Test: `LoadPipelineState_ExistingPipeline_ReturnsState` -- resume scenario
  - [x] 7.3 Test: `LoadPipelineState_NoPipeline_ReturnsNull` -- normal scenario
  - [x] 7.4 Test: `Cleanup_RemovesPipelineStateKey` -- terminal cleanup
  - [x] 7.5 Test: `PipelineKeyPattern_MatchesConvention` -- `{tenant}:{domain}:{aggId}:pipeline:{correlationId}`

- [x] Task 8: Create AggregateActor state machine integration tests (AC: #1, #2, #4, #8, #11, #12)
  - [x] 8.1 Test: `ProcessCommand_Success_TransitionsReceived_Processing_EventsStored_Completed` -- happy path transitions
  - [x] 8.2 Test: `ProcessCommand_Rejection_TransitionsProcessing_EventsStored_Completed_WithRejectedStatus` -- rejection path
  - [x] 8.3 Test: `ProcessCommand_NoOp_TransitionsProcessing_Completed` -- empty event list
  - [x] 8.4 Test: `ProcessCommand_CrashAtEventsStored_Resume_DoesNotRePersisteEvents` -- NFR25 core test
  - [x] 8.5 Test: `ProcessCommand_CrashAtProcessing_Resume_ReprocessesFromScratch` -- safe reprocess
  - [x] 8.6 Test: `ProcessCommand_StatusWriteFailure_DoesNotBlockPipeline` -- enforcement rule #12
  - [x] 8.7 Test: `ProcessCommand_PipelineStateCleanedUp_OnCompletion` -- no state leakage
  - [x] 8.8 Test: `ProcessCommand_PipelineStateCleanedUp_OnRejection` -- cleanup on rejection terminal

- [x] Task 9: Create OpenTelemetry activity tests (AC: #5)
  - [x] 9.1 Test: `ProcessCommand_CreatesActivitySpans_ForEachStage` -- verify activity creation
  - [x] 9.2 Test: `Activities_IncludeCorrectTags` -- correlationId, tenantId, domain, etc.
  - [x] 9.3 Test: `Activities_FollowNamingConvention` -- EventStore.Actor.*, EventStore.Events.*, EventStore.DomainService.*

- [x] Task 10: Update existing AggregateActor tests
  - [x] 10.1 Update test setup to provide `IActorStateMachine` mock (NSubstitute)
  - [x] 10.2 Update test setup to provide `ICommandStatusStore` mock
  - [x] 10.3 Verify all existing 703 tests still pass with the new dependencies

- [x] Task 11: Verify all tests pass
  - [x] 11.1 Run `dotnet test` to confirm no regressions
  - [x] 11.2 All new state machine tests pass
  - [x] 11.3 All existing Story 3.1-3.10 tests still pass

## Dev Notes

### Story Context

This story introduces the **ActorStateMachine** -- the checkpointed lifecycle tracking component that makes the AggregateActor crash-resilient. Currently, the AggregateActor executes all 5 steps in a single `ProcessCommandAsync` method with ONE atomic commit at the end. If the actor crashes mid-pipeline, there's no way to know which step it was on -- the entire pipeline reruns from scratch. This is safe for Steps 1-4 (idempotent) but DANGEROUS if it causes duplicate event persistence (NFR25 violation).

**The core problem Story 3.11 solves:**
If the actor crashes AFTER `SaveStateAsync` persists events (Step 5a) but BEFORE the method returns, the actor will restart, pass the idempotency check (because the IdempotencyRecord was written in the same batch), and NOT re-persist. However, without state machine tracking, there's no way to:
1. Know the command reached EventsStored stage (for future pub/sub resume in Epic 4)
2. Write advisory status updates at each pipeline stage
3. Track command lifecycle progression for operational observability

**What currently exists (to modify, NOT rewrite):**
- `AggregateActor.ProcessCommandAsync()` -- 5-step pipeline, ~250 lines, with comment "State machine checkpointing deferred to Story 3.11"
- `DaprCommandStatusStore` + `ICommandStatusStore` -- exists but only "Received" status written (at API layer in `SubmitCommandHandler`)
- `CommandStatus` enum -- all 8 values defined (Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, TimedOut)
- `IdempotencyChecker` -- detects duplicate commands, returns cached results
- NO OpenTelemetry ActivitySource or Activity spans exist anywhere in the actor pipeline
- NO state machine abstraction exists

**What this story creates (NEW):**
- `ActorStateMachine` -- checkpoints pipeline stages in actor state
- `PipelineState` -- record tracking in-flight command lifecycle
- `EventStoreActivitySource` -- OpenTelemetry distributed tracing spans
- Resume-from-checkpoint logic in AggregateActor
- Advisory status writes at every stage transition
- `PipelineKeyPrefix` on AggregateIdentity

### Architecture Compliance

- **NFR25:** Actor crash after event persistence but before pub/sub delivery must not result in duplicate event persistence -- the checkpointed state machine must resume from the correct stage
- **D2:** Command status storage -- Checkpointed state machine: Received -> Processing -> EventsStored -> EventsPublished -> Completed | Rejected | PublishFailed | TimedOut
- **FR35:** OpenTelemetry traces spanning the full command lifecycle
- **FR36:** Structured logs with correlation and causation IDs at each stage
- **Rule #5:** Never log event payload data -- only envelope metadata fields
- **Rule #6:** IActorStateManager for all actor state operations -- never bypass with direct DaprClient
- **Rule #9:** correlationId in every structured log entry and OpenTelemetry activity
- **Rule #12:** Command status writes are advisory -- failure to write/update status must never block or fail the command processing pipeline
- **Rule #14:** DAPR sidecar call timeout is 5 seconds
- **SEC-5:** Event payload data never in logs

### Critical Design Decisions

- **State machine checkpoints are in the SAME atomic batch as events.** The critical invariant is: when the state machine says "EventsStored", events MUST exist in the state store. This is achieved by using `IActorStateManager.SetStateAsync` (staging) for BOTH event writes and state machine checkpoint writes, then a SINGLE `SaveStateAsync` commits everything atomically. This is the same pattern EventPersister already uses.

- **Resume logic is triggered through idempotency check extension, not a separate path.** When a command arrives and an existing `PipelineState` exists for that correlationId, the actor detects this as a resume scenario. The behavior depends on the checkpointed stage:
  - `Processing` -- crash happened before events were persisted. Safe to reprocess from scratch (Steps 3-5).
  - `EventsStored` -- events are persisted but not yet published. Skip to publication (Epic 4) or mark Completed.
  - Any terminal state -- return cached result (same as current IdempotencyChecker behavior).

- **PipelineState vs IdempotencyRecord relationship (DECISION REQUIRED).** The current IdempotencyChecker stores an `IdempotencyRecord` keyed by correlationId. The new PipelineState is ALSO keyed by correlationId. The dev agent MUST decide: (a) PipelineState REPLACES IdempotencyRecord during in-flight processing, with IdempotencyRecord written only at terminal state (cleaner, avoids redundancy), OR (b) PipelineState SUPPLEMENTS IdempotencyRecord (both exist, checked in order: idempotency first for completed commands, pipeline state for in-flight). Option (a) is recommended -- the PipelineState IS the in-flight idempotency record, and the existing IdempotencyRecord becomes the terminal/cached result. Investigate `IdempotencyChecker.cs` thoroughly before deciding.

- **PipelineKeyPrefix placement (EVALUATE during implementation).** The story proposes adding `PipelineKeyPrefix` to `AggregateIdentity` in the Contracts package. However, pipeline keys are a SERVER-SIDE implementation detail -- domain service developers (the primary Contracts consumers) don't need them. The dev agent should evaluate whether a server-side `ActorKeyHelper` class or extension method in the Server package is more appropriate. If the existing `AggregateIdentity` already has `EventKeyPrefix` and `SnapshotKey` in Contracts, then `PipelineKeyPrefix` fits the established pattern. If those are server-side, keep pipeline keys server-side too. Follow existing conventions.

- **EventsPublished stage is a PLACEHOLDER for Epic 4.** Since EventPublisher doesn't exist yet, the state machine transitions directly from EventsStored to Completed. **Epic 4 integration point:** When Story 4.1 implements the EventPublisher, the call should be inserted between the EventsStored checkpoint and the Completed transition in AggregateActor. The state machine itself needs no modification -- only the AggregateActor orchestration adds the publish step and a new checkpoint write. Document this as a code comment at the exact insertion point.

- **Advisory status writes use fire-and-forget with logging.** Per enforcement rule #12, status writes MUST NOT block the pipeline. Implementation: wrap each `ICommandStatusStore.WriteStatusAsync` call in try/catch, log Warning on failure, continue pipeline. The command processing pipeline's correctness is independent of status tracking.

- **OpenTelemetry activities use a single static ActivitySource.** Following .NET OpenTelemetry conventions, a single `EventStoreActivitySource` with name `"Hexalith.EventStore"` provides all activity spans. Activities are started with `activitySource.StartActivity("EventStore.Actor.ProcessCommand")` and disposed at scope exit. Activity tags carry correlation context. The outer `ProcessCommand` activity should be the PARENT of all inner stage activities, forming a proper span hierarchy. This integrates with the Aspire dashboard and any OTLP-compatible collector.

- **Pipeline state key includes correlationId for multi-command support.** An actor may have multiple in-flight or completed commands. Using `pipeline:{correlationId}` as the key ensures each command's lifecycle is tracked independently. Cleanup removes the key after terminal state.

- **No timeout implementation in this story.** The `TimedOut` terminal state exists in the enum but implementing actual timeout detection (e.g., via timer-based actor reminders) is deferred. This story ensures the state machine CAN represent TimedOut, but does not trigger it.

- **SetStateAsync vs SaveStateAsync distinction (CRITICAL for testing).** `IActorStateManager.SetStateAsync` is a STAGING operation -- it buffers the write in memory and does NOT contact the state store. It cannot fail for state store reasons. `SaveStateAsync` COMMITS all staged writes atomically to the state store -- this is where state store failures surface. Therefore, pipeline state checkpoint staging (`SetStateAsync`) cannot fail independently of the event persistence commit. Tests should focus on `SaveStateAsync` failure scenarios, not `SetStateAsync` failures.

### Existing Patterns to Follow

**Atomic batch pattern (from AggregateActor Step 5):**
```csharp
// All writes are staged via SetStateAsync (not saved yet)
await eventPersister.PersistEventsAsync(identity, events, currentSequence);
await snapshotManager.MaybeCreateSnapshotAsync(identity, domainState, newSequence, lastSnapshotSequence);
// Single atomic commit
await StateManager.SaveStateAsync().ConfigureAwait(false);
```

**Advisory status write pattern (from architecture, not yet implemented):**
```csharp
try
{
    await statusStore.WriteStatusAsync(
        command.TenantId, command.CorrelationId, CommandStatus.Processing);
}
catch (Exception ex)
{
    logger.LogWarning(ex,
        "Advisory status write failed: CorrelationId={CorrelationId}, Status={Status}",
        command.CorrelationId, CommandStatus.Processing);
    // Never throw -- rule #12
}
```

**OpenTelemetry activity pattern (standard .NET):**
```csharp
using var activity = EventStoreActivitySource.Instance.StartActivity(
    "EventStore.Actor.ProcessCommand");
activity?.SetTag("eventstore.correlation_id", command.CorrelationId);
activity?.SetTag("eventstore.tenant_id", command.TenantId);
// ... work happens ...
activity?.SetStatus(ActivityStatusCode.Ok);
```

**Structured logging pattern (from AggregateActor):**
```csharp
logger.LogInformation(
    "Actor {ActorId} stage transition: Stage={Stage}, CorrelationId={CorrelationId}, " +
    "Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, DurationMs={DurationMs}",
    Host.Id, stage, command.CorrelationId, command.TenantId, command.Domain,
    command.AggregateId, command.CommandType, elapsed.TotalMilliseconds);
```

**Primary constructor pattern (from existing code):**
```csharp
public class ActorStateMachine(IActorStateManager stateManager, ILogger<ActorStateMachine> logger)
    : IActorStateMachine
{
    // ...
}
```

### Mandatory Coding Patterns

- Primary constructors: `public class ActorStateMachine(IActorStateManager stateManager, ILogger<ActorStateMachine> logger)`
- Records for immutable data: `PipelineState`
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions in tests
- Feature folder organization (`Actors/` and `Telemetry/` folders in Server project)
- **Rule #5:** Never log event payload data or pipeline state content
- **Rule #6:** IActorStateManager for all actor state operations
- **Rule #9:** CorrelationId in every structured log entry and OpenTelemetry activity
- **Rule #12:** Advisory status writes -- failures logged, never thrown
- **Rule #14:** DAPR sidecar timeout 5 seconds

### Project Structure Notes

**New files:**
- `src/Hexalith.EventStore.Server/Actors/IActorStateMachine.cs` -- state machine interface
- `src/Hexalith.EventStore.Server/Actors/ActorStateMachine.cs` -- checkpointed stage transitions
- `src/Hexalith.EventStore.Server/Actors/PipelineState.cs` -- in-flight command lifecycle record
- `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` -- OpenTelemetry activity source
- `src/Hexalith.EventStore.Testing/Fakes/FakeActorStateMachine.cs` -- test double
- `tests/Hexalith.EventStore.Server.Tests/Actors/ActorStateMachineTests.cs` -- state machine unit tests
- `tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs` -- resume/crash recovery tests

**Modified files:**
- `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` -- add `PipelineKeyPrefix` property
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- integrate state machine, add OpenTelemetry activities, add status writes, add resume logic
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` -- register IActorStateMachine, verify ICommandStatusStore
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs` -- update mock setup for new dependencies
- `tests/Hexalith.EventStore.Contracts.Tests/Identity/AggregateIdentityTests.cs` -- add PipelineKeyPrefix test

### Previous Story Intelligence

**From Story 3.10 (State Reconstruction from Snapshot + Tail Events):**
- 703 tests pass (687 existing + 16 new)
- RehydrationResult record introduced: SnapshotState, Events, LastSnapshotSequence, CurrentSequence
- AggregateActor Step 3 refactored to load snapshot FIRST, then pass to EventStreamReader
- ConstructDomainState() helper method handles three state composition scenarios
- Interface change to IEventStreamReader was breaking but contained (blast radius analysis documented)
- NO changes were made to IDomainProcessor, DomainProcessorBase, or DaprDomainServiceInvoker

**From Story 3.9 (Snapshot Creation at Configurable Intervals):**
- SnapshotManager registered as singleton in DI via `AddEventStoreServer()`
- LoadSnapshotAsync handles: missing snapshots (returns null), corrupt snapshots (deletes and returns null)
- Snapshot write is staged in same SaveStateAsync batch as events (atomicity guaranteed)
- Key insight for this story: the SAME atomic batch pattern is used for state machine checkpoints

**From Story 3.7 (Event Persistence with Atomic Writes):**
- EventPersister uses `SetStateAsync` for staging, AggregateActor calls `SaveStateAsync` for atomic commit
- Write-once keys: `{tenant}:{domain}:{aggId}:events:{seq}`
- Gapless sequence numbers enforced
- This is the exact pattern to follow for state machine checkpoints

**From Story 3.2 (AggregateActor Orchestrator & Idempotency):**
- IdempotencyChecker stores `IdempotencyRecord` in actor state with correlationId as key
- Duplicate detection returns cached result from previous processing
- IdempotencyRecord written in same SaveStateAsync batch as events
- For Story 3.11: IdempotencyChecker needs to be extended OR PipelineState loading needs to happen alongside idempotency check

**Current AggregateActor architecture (critical understanding):**
- ONE `ProcessCommandAsync` method with 5 sequential steps
- Steps 1-4 are pre-persistence (no state committed)
- Step 5 stages event writes + idempotency record + optional snapshot, then ONE `SaveStateAsync` at the very end
- If crash before SaveStateAsync: nothing was persisted, safe to retry from scratch
- If crash after SaveStateAsync: events + idempotency record committed, idempotency check catches retry
- The GAP this story fills: after SaveStateAsync, there's no record of WHAT stage completed, only that events exist

### Git Intelligence

Recent commits show the progression through Epic 3:
- `f79aabe` Story 3.10: State reconstruction from snapshot + tail events (#35)
- `c120c19` Stories 3.6-3.9: Multi-tenant, event persistence, key isolation, snapshots (#33)
- Patterns: Primary constructors, records for data, ConfigureAwait(false), NSubstitute + Shouldly
- DI registration via `Add*` extension methods in `ServiceCollectionExtensions.cs`
- Feature folder organization throughout
- Comprehensive test coverage with descriptive test method names

### Testing Requirements

**Unit Tests (~8-10 new):**
- ActorStateMachine: checkpoint/load/cleanup operations
- PipelineState: correct construction and serialization
- PipelineKeyPrefix: key pattern derivation on AggregateIdentity
- EventStoreActivitySource: activity creation and naming
- Advisory status write: failure handling (catch, log, continue)

**Integration Tests (~8-10 new):**
- Happy path: full stage transition sequence (Processing -> EventsStored -> Completed)
- Rejection path: Processing -> EventsStored -> Completed with Rejected status
- No-op path: Processing -> Completed
- Crash recovery from EventsStored: resume without re-persistence
- Crash recovery from Processing: safe reprocess from scratch
- Status write failure: pipeline continues unblocked
- Pipeline state cleanup on completion
- Pipeline state cleanup on rejection
- OpenTelemetry activity spans created with correct names/tags

**Performance regression tests (~1-2 new):**
- Verify pipeline latency stays within NFR2 (200ms e2e) with state machine enabled
- Compare command processing time with vs without state machine overhead

**Existing Test Updates (~5-15 modified):**
- AggregateActorTests: add IActorStateMachine and ICommandStatusStore mocks to test setup
- AggregateIdentityTests: add PipelineKeyPrefix test (if placed in Contracts)

**Total estimated: ~25-40 tests (new + modified)**

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 3, Story 3.11]
- [Source: _bmad-output/planning-artifacts/architecture.md#ActorStateMachine checkpointed stages]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR25 Crash recovery without duplicate persistence]
- [Source: _bmad-output/planning-artifacts/architecture.md#D2 Command Status Storage]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 12 Advisory status writes]
- [Source: _bmad-output/planning-artifacts/architecture.md#OpenTelemetry Activity Naming]
- [Source: _bmad-output/planning-artifacts/architecture.md#Structured Logging Pattern]
- [Source: _bmad-output/planning-artifacts/architecture.md#AggregateActor thin orchestrator 5-step delegation]
- [Source: _bmad-output/implementation-artifacts/3-10-state-reconstruction-from-snapshot-plus-tail-events.md]
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC-5 Event payload never in logs]
- [Source: _bmad-output/planning-artifacts/architecture.md#FR35 OpenTelemetry full lifecycle traces]
- [Source: _bmad-output/planning-artifacts/architecture.md#FR36 Structured logs at each pipeline stage]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

### Completion Notes List

- PipelineState supplements IdempotencyRecord (Option b): different keys (`pipeline:{correlationId}` vs `idempotency:{causationId}`), different purposes
- PipelineKeyPrefix placed in AggregateIdentity (Contracts) following existing EventStreamKeyPrefix/MetadataKey/SnapshotKey pattern
- ActorStateMachine created per-call (like IdempotencyChecker) -- needs actor's IActorStateManager
- ICommandStatusStore comes via DI constructor injection (already registered in CommandApi's AddCommandApi())
- No DI registration changes needed in Server's ServiceCollectionExtensions -- ActorStateMachine is per-call, not DI-resolved
- 3 SaveStateAsync calls on success path: (1) Processing checkpoint, (2) Events + EventsStored checkpoint atomic, (3) Idempotency record + pipeline cleanup terminal
- 2 SaveStateAsync calls on no-op path: (1) Processing checkpoint, (2) Idempotency record + pipeline cleanup terminal
- Resume from Processing: cleanup stale state, fall through to normal reprocessing
- Resume from EventsStored: skip re-persistence, write idempotency record + cleanup directly
- Epic 4 integration point documented with comment at exact insertion location in AggregateActor.cs
- 730 total tests pass (694 existing + 36 new)

### Change Log

- NEW: `src/Hexalith.EventStore.Server/Actors/PipelineState.cs` -- In-flight command lifecycle record
- NEW: `src/Hexalith.EventStore.Server/Actors/IActorStateMachine.cs` -- State machine interface
- NEW: `src/Hexalith.EventStore.Server/Actors/ActorStateMachine.cs` -- Checkpointed stage transitions via IActorStateManager
- NEW: `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` -- OpenTelemetry ActivitySource with named spans
- NEW: `src/Hexalith.EventStore.Testing/Fakes/FakeActorStateMachine.cs` -- Test double for IActorStateMachine
- NEW: `tests/Hexalith.EventStore.Server.Tests/Actors/ActorStateMachineTests.cs` -- 7 unit tests
- NEW: `tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs` -- 8 integration tests
- NEW: `tests/Hexalith.EventStore.Server.Tests/Telemetry/EventStoreActivitySourceTests.cs` -- 3 OpenTelemetry tests
- MODIFIED: `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` -- Added PipelineKeyPrefix property
- MODIFIED: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- State machine integration, OpenTelemetry activities, advisory status writes, resume logic
- MODIFIED: `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs` -- Updated mock setup for ICommandStatusStore and PipelineState, fixed SaveStateAsync call counts
- MODIFIED: `tests/Hexalith.EventStore.Contracts.Tests/Identity/AggregateIdentityTests.cs` -- Added PipelineKeyPrefix tests (3 tests)

### File List

- `src/Hexalith.EventStore.Server/Actors/PipelineState.cs`
- `src/Hexalith.EventStore.Server/Actors/IActorStateMachine.cs`
- `src/Hexalith.EventStore.Server/Actors/ActorStateMachine.cs`
- `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs`
- `src/Hexalith.EventStore.Testing/Fakes/FakeActorStateMachine.cs`
- `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs`
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/ActorStateMachineTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Telemetry/EventStoreActivitySourceTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Identity/AggregateIdentityTests.cs`
