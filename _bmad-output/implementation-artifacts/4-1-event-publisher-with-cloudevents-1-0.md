# Story 4.1: Event Publisher with CloudEvents 1.0

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**BLOCKING: Story 3.11 (ActorStateMachine & Checkpointed Stages) MUST be implemented before this story.**

Epic 4 begins event distribution. The ActorStateMachine from Story 3.11 manages the checkpointed stage transitions (EventsStored -> EventsPublished -> Completed). This story fills the gap between EventsStored and Completed by implementing actual pub/sub publication.

Verify these files/classes exist before starting:
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (Stories 3.2-3.11 -- 5-step thin orchestrator with state machine checkpointing)
- `src/Hexalith.EventStore.Server/Actors/ActorStateMachine.cs` (Story 3.11 -- checkpointed stages)
- `src/Hexalith.EventStore.Server/Actors/IActorStateMachine.cs` (Story 3.11 -- state machine interface)
- `src/Hexalith.EventStore.Server/Actors/PipelineState.cs` (Story 3.11 -- in-flight command lifecycle record)
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs` (Story 3.7 -- event persistence via IActorStateManager)
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs` (Story 3.10 -- snapshot-aware rehydration)
- `src/Hexalith.EventStore.Server/Commands/DaprCommandStatusStore.cs` (Story 2.6 -- advisory status writes)
- `src/Hexalith.EventStore.Server/Commands/ICommandStatusStore.cs` (Story 2.6 -- status store interface)
- `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs` (Story 1.2 -- 8-value enum including EventsPublished)
- `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` (Story 1.2 -- includes `PubSubTopic` property)
- `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` (Story 3.11 -- OpenTelemetry activity source)
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` (Story 3.1+ -- DI registrations)
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryStateManager.cs` (Story 1.4 -- test fake for IActorStateManager)
- `src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj` (must already include `Dapr.Client` package reference)

Run `dotnet test` to confirm all existing tests pass before beginning. Story 3.11 should have ~730-750 tests.

## Story

As a **subscriber system**,
I want persisted events published to a DAPR pub/sub component wrapped in CloudEvents 1.0 envelope format,
So that I receive events in a standard, interoperable format (FR17).

## Acceptance Criteria

1. **CloudEvents 1.0 envelope** - Given events have been persisted by the AggregateActor (state machine at EventsStored), When the EventPublisher publishes events, Then each event is wrapped in a CloudEvents 1.0 envelope with `type`, `source`, `id`, `time`, `datacontenttype`, and `data` fields via DAPR's native CloudEvents wrapping.

2. **DAPR pub/sub publication** - Given the EventPublisher receives persisted EventEnvelope instances, When it publishes each event, Then it uses `DaprClient.PublishEventAsync` with the DAPR pub/sub component name and the appropriate topic, And CloudEvents fields are set via DAPR metadata: `type` = event type name (e.g., `CounterIncremented`), `source` = `hexalith-eventstore/{tenant}/{domain}`, `id` = `{correlationId}:{sequenceNumber}` (globally unique per event).

3. **State machine integration** - Given the EventPublisher successfully publishes all events for a command, When publication completes, Then the state machine transitions from EventsStored to EventsPublished, And the EventsPublished checkpoint is written in the same atomic `SaveStateAsync` batch, And advisory command status is updated to EventsPublished (then Completed).

4. **Pub/sub delivery latency** - Given events are published via the EventPublisher, When measuring end-to-end delivery time, Then pub/sub delivery latency (event persistence to subscriber delivery confirmation) is under 50ms at p99 (NFR5).

5. **OpenTelemetry activity** - Given events are published, When the EventPublisher executes, Then an OpenTelemetry activity named `EventStore.Events.Publish` spans the publication step, And activity tags include: `eventstore.correlation_id`, `eventstore.tenant_id`, `eventstore.domain`, `eventstore.aggregate_id`, `eventstore.event_count`, `eventstore.topic`.

6. **Structured logging** - Given events are published successfully, When the publisher completes, Then a structured log entry at Information level is emitted with: correlationId, tenantId, domain, aggregateId, eventCount, topic, durationMs, And event payload data never appears in logs (SEC-5, NFR12).

7. **Publication failure handling** - Given the pub/sub system is unavailable during publication, When `DaprClient.PublishEventAsync` throws, Then the state machine remains at EventsStored (events are safe in state store), And the command status transitions to PublishFailed (advisory), And the failure is logged at Error level with correlationId, tenantId, topic, and exception message (no stack traces in responses, rule #13), And the actor does NOT block waiting for pub/sub recovery -- it moves to the PublishFailed terminal state.

8. **Multi-event publication** - Given a command produces N events (N >= 1), When the EventPublisher publishes them, Then ALL N events are published to the same topic in sequence order, And if publication fails mid-batch (e.g., event 3 of 5 fails), Then the state machine transitions to PublishFailed (partial publication is acceptable since subscribers must be idempotent per at-least-once delivery guarantee).

9. **Topic derivation** - Given events are published for a specific aggregate, When the EventPublisher determines the target topic, Then it derives the topic from `AggregateIdentity.PubSubTopic` which produces `{tenant}.{domain}.events` (D6), And the topic name is logged with each publication.

10. **Pub/sub component name configuration** - Given the EventPublisher needs the DAPR pub/sub component name, When it resolves the component, Then the pub/sub component name is configurable via `EventPublisherOptions.PubSubName` (default: `"pubsub"`), And the configuration is injected via DI options pattern.

11. **No-op and rejection path** - Given a command produces no events (no-op) or produces rejection events only, When the state machine handles the publication step, Then no-op commands skip publication entirely (nothing to publish), And rejection events ARE published to the same topic as regular events (per D3: rejection events are normal events, not error paths).

12. **Atomic checkpoint with publication result** - Given events have been published (or publication failed), When the state machine records the outcome, Then the EventsPublished checkpoint (or PublishFailed terminal state) is written via `IActorStateManager.SetStateAsync` (staged), And the caller commits atomically with `SaveStateAsync`, And pipeline state cleanup happens in the same batch for terminal states.

## Tasks / Subtasks

- [x] Task 0: Verify prerequisites and understand current state (BLOCKING)
  - [x]0.1 Run all existing tests -- they must pass before proceeding
  - [x]0.2 Review `AggregateActor.cs` -- locate the exact insertion point for event publication (between EventsStored checkpoint and Completed transition). Story 3.11 notes: "When Story 4.1 implements the EventPublisher, the call should be inserted between the EventsStored checkpoint and the Completed transition"
  - [x]0.3 Review `ActorStateMachine.cs` and `PipelineState.cs` to understand checkpoint API
  - [x]0.4 Review `EventStoreActivitySource.cs` for existing OpenTelemetry activity patterns
  - [x]0.5 Review `AggregateIdentity.PubSubTopic` property (already exists: `$"{TenantId}.{Domain}.events"`)
  - [x]0.6 Review `DaprClient.PublishEventAsync` API -- DAPR auto-wraps in CloudEvents 1.0; use metadata dictionary to set `type`, `source`, `id` fields
  - [x]0.7 Review how `EventPersister.PersistEventsAsync` returns persisted `EventEnvelope` instances (the publisher needs the envelopes with populated metadata)

- [x] Task 1: Create EventPublisher abstraction (AC: #1, #2, #5, #6, #7, #8, #9, #10)
  - [x]1.1 Create `IEventPublisher.cs` in `src/Hexalith.EventStore.Server/Events/`
  - [x]1.2 Create `EventPublisher.cs` in `src/Hexalith.EventStore.Server/Events/`
  - [x]1.3 Create `EventPublisherOptions.cs` in `src/Hexalith.EventStore.Server/Configuration/`
  - [x]1.4 Method signature: `Task<EventPublishResult> PublishEventsAsync(AggregateIdentity identity, IReadOnlyList<EventEnvelope> events, string correlationId, CancellationToken cancellationToken = default)`
  - [x]1.5 `EventPublishResult` record: `bool Success`, `int PublishedCount`, `string? FailureReason`
  - [x]1.6 Implementation: iterate events in sequence order, call `DaprClient.PublishEventAsync` for each with CloudEvents metadata
  - [x]1.7 CloudEvents metadata per event: `{ "cloudevent.type": event.EventTypeName, "cloudevent.source": $"hexalith-eventstore/{identity.TenantId}/{identity.Domain}", "cloudevent.id": $"{correlationId}:{event.SequenceNumber}" }`
  - [x]1.8 Topic: `identity.PubSubTopic` (already `{tenant}.{domain}.events`)
  - [x]1.9 Pub/sub component name from `IOptions<EventPublisherOptions>.Value.PubSubName`
  - [x]1.10 Wrap publication in OpenTelemetry activity `EventStore.Events.Publish` with tags
  - [x]1.11 Structured logging: Information on success, Error on failure (never log payload data)
  - [x]1.12 On failure: catch exception, log Error, return `EventPublishResult(false, publishedSoFar, ex.Message)` -- do NOT rethrow

- [x] Task 2: Create EventPublisherOptions (AC: #10)
  - [x]2.1 Create record in `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs`
  - [x]2.2 Property: `string PubSubName { get; init; } = "pubsub";`
  - [x]2.3 Register via `services.AddOptions<EventPublisherOptions>().BindConfiguration("EventStore:Publisher")` in `ServiceCollectionExtensions.cs`

- [x] Task 3: Integrate EventPublisher into AggregateActor (AC: #3, #7, #11, #12)
  - [x]3.1 Add `IEventPublisher` as constructor dependency to AggregateActor
  - [x]3.2 Locate the insertion point identified in Task 0.2 (between EventsStored and Completed)
  - [x]3.3 After EventsStored checkpoint: if events were persisted (not no-op), call `eventPublisher.PublishEventsAsync(identity, persistedEvents, command.CorrelationId)`
  - [x]3.4 If publication succeeds: checkpoint `EventsPublished` via state machine, then transition to Completed
  - [x]3.5 If publication fails: transition to `PublishFailed` terminal state, cleanup pipeline, write advisory status
  - [x]3.6 No-op path: skip publication entirely (no events to publish)
  - [x]3.7 Rejection path: publish rejection events same as regular events (per D3)
  - [x]3.8 Advisory status writes: `EventsPublished` then `Completed` (or `PublishFailed`) -- wrapped in try/catch per rule #12
  - [x]3.9 EventsPublished checkpoint staged via SetStateAsync, committed with SaveStateAsync atomically

- [x] Task 4: Register EventPublisher in DI (AC: #10)
  - [x]4.1 Register `IEventPublisher` -> `EventPublisher` in `ServiceCollectionExtensions.cs`
  - [x]4.2 Register `EventPublisherOptions` configuration binding
  - [x]4.3 Verify DaprClient is already registered (it should be via `AddActors` or DAPR middleware)

- [x] Task 5: Create FakeEventPublisher test double
  - [x]5.1 Create `FakeEventPublisher.cs` in `src/Hexalith.EventStore.Testing/Fakes/`
  - [x]5.2 Track all publish calls: events published, topic used, correlationId
  - [x]5.3 Support configurable failure mode for testing PublishFailed path
  - [x]5.4 Support configurable partial failure (fail on event N of M)

- [x] Task 6: Create EventPublisher unit tests (AC: #1, #2, #5, #6, #7, #8, #9, #10)
  - [x]6.1 Test: `PublishEventsAsync_SingleEvent_CallsDaprPublishWithCloudEventsMetadata`
  - [x]6.2 Test: `PublishEventsAsync_MultipleEvents_PublishesInSequenceOrder`
  - [x]6.3 Test: `PublishEventsAsync_CorrectTopic_DerivedFromAggregateIdentity`
  - [x]6.4 Test: `PublishEventsAsync_CloudEventsType_MatchesEventTypeName`
  - [x]6.5 Test: `PublishEventsAsync_CloudEventsSource_IncludesTenantAndDomain`
  - [x]6.6 Test: `PublishEventsAsync_CloudEventsId_CombinesCorrelationIdAndSequence`
  - [x]6.7 Test: `PublishEventsAsync_UsesConfiguredPubSubName`
  - [x]6.8 Test: `PublishEventsAsync_PubSubFailure_ReturnsFailureResult_DoesNotThrow`
  - [x]6.9 Test: `PublishEventsAsync_PartialFailure_ReturnsPublishedCount`
  - [x]6.10 Test: `PublishEventsAsync_EmptyEventList_ReturnsSuccessWithZeroCount`
  - [x]6.11 Test: `PublishEventsAsync_CreatesOpenTelemetryActivity_WithCorrectTags`
  - [x]6.12 Test: `PublishEventsAsync_LogsSuccess_WithoutPayloadData`
  - [x]6.13 Test: `PublishEventsAsync_LogsFailure_WithCorrelationIdAndTopic`

- [x] Task 7: Create AggregateActor publication integration tests (AC: #3, #7, #11, #12)
  - [x]7.1 Test: `ProcessCommand_Success_TransitionsEventStored_EventsPublished_Completed`
  - [x]7.2 Test: `ProcessCommand_PublishFails_TransitionsToPublishFailed`
  - [x]7.3 Test: `ProcessCommand_NoOp_SkipsPublication_TransitionsDirectlyToCompleted`
  - [x]7.4 Test: `ProcessCommand_Rejection_PublishesRejectionEvents_ThenCompleted`
  - [x]7.5 Test: `ProcessCommand_PublishFailed_PipelineStateCleaned`
  - [x]7.6 Test: `ProcessCommand_EventsPublished_StatusWrittenAdvisory`
  - [x]7.7 Test: `ProcessCommand_PublishFailed_StatusWrittenAdvisory`
  - [x]7.8 Test: `ProcessCommand_PublishSuccess_EventsPublishedCheckpointAtomic`

- [x] Task 8: Update existing AggregateActor tests
  - [x]8.1 Update test setup to provide `IEventPublisher` mock (NSubstitute)
  - [x]8.2 Default mock behavior: return `EventPublishResult(true, eventCount, null)` for all calls
  - [x]8.3 Verify all existing tests still pass with the new dependency

- [x] Task 9: Verify all tests pass
  - [x]9.1 Run `dotnet test` to confirm no regressions
  - [x]9.2 All new EventPublisher tests pass
  - [x]9.3 All existing Story 3.1-3.11 tests still pass

## Dev Notes

### Story Context

This is the **first story in Epic 4: Event Distribution & Dead-Letter Handling**. It introduces the EventPublisher -- the component that takes persisted events and distributes them to subscribers via DAPR pub/sub with CloudEvents 1.0 wrapping.

**What currently exists (to modify, NOT rewrite):**
- `AggregateActor.ProcessCommandAsync()` -- 5-step pipeline with state machine checkpointing (Story 3.11). Currently transitions directly from EventsStored to Completed. Story 3.11 explicitly notes: "EventsPublished stage is a PLACEHOLDER for Epic 4. When Story 4.1 implements the EventPublisher, the call should be inserted between the EventsStored checkpoint and the Completed transition."
- `AggregateIdentity.PubSubTopic` -- already returns `$"{TenantId}.{Domain}.events"` (D6 topic naming)
- `CommandStatus.EventsPublished` -- enum value exists but is never used yet
- `PipelineState` -- supports tracking EventsPublished stage (field `CurrentStage` accepts any `CommandStatus` value)
- `EventStoreActivitySource` -- OpenTelemetry activity source with `EventStore.Events.Publish` activity name pre-defined (Story 3.11)
- `Dapr.Client` NuGet package -- already referenced in Server.csproj; provides `DaprClient.PublishEventAsync`

**What this story creates (NEW):**
- `IEventPublisher` -- interface for event publication
- `EventPublisher` -- implementation using `DaprClient.PublishEventAsync` with CloudEvents metadata
- `EventPublishResult` -- result record (success/failure/partial)
- `EventPublisherOptions` -- configurable pub/sub component name
- `FakeEventPublisher` -- test double for unit/integration tests
- Integration of EventPublisher into AggregateActor between EventsStored and Completed

**What this story modifies (EXISTING):**
- `AggregateActor.cs` -- insert publication step between EventsStored and Completed
- `ServiceCollectionExtensions.cs` -- register IEventPublisher and EventPublisherOptions
- Existing AggregateActor tests -- add IEventPublisher mock to test setup

### Architecture Compliance

- **FR17:** Event publication via pub/sub with CloudEvents 1.0 envelope format
- **D6:** Topic naming `{tenant}.{domain}.events`
- **NFR5:** Pub/sub delivery latency under 50ms at p99
- **NFR22:** Zero events lost under any tested failure scenario
- **NFR25:** Actor crash after event persistence but before pub/sub delivery must not result in duplicate event persistence (state machine resumes from EventsStored)
- **NFR28:** System must function with any DAPR-compatible pub/sub component supporting CloudEvents 1.0 and at-least-once delivery
- **Rule #4:** Never add custom retry logic -- all retries are DAPR resiliency policies
- **Rule #5:** Never log event payload data -- only envelope metadata fields
- **Rule #6:** IActorStateManager for all actor state operations
- **Rule #9:** correlationId in every structured log entry and OpenTelemetry activity
- **Rule #12:** Command status writes are advisory -- failure never blocks pipeline
- **Rule #13:** No stack traces in production error responses
- **SEC-1:** EventStore owns all 11 envelope metadata fields (already populated by EventPersister)
- **SEC-5:** Event payload data never in logs

### Critical Design Decisions

- **DAPR handles CloudEvents wrapping natively.** `DaprClient.PublishEventAsync` automatically wraps the published data in a CloudEvents 1.0 envelope. Use the metadata dictionary to override specific CloudEvents fields (`cloudevent.type`, `cloudevent.source`, `cloudevent.id`). Do NOT manually construct CloudEvents JSON -- let DAPR do it. This ensures compatibility with all DAPR pub/sub components (NFR28).

- **Publish the full EventEnvelope as the CloudEvents `data` field.** The subscriber receives the complete 11-field EventEnvelope (with metadata + payload + extensions). This gives subscribers everything they need. The CloudEvents wrapper adds routing metadata (type, source, id) that enables topic routing and deduplication.

- **EventPublisher does NOT call SaveStateAsync.** Like EventPersister and SnapshotManager, the EventPublisher performs its work (publication) but does NOT commit actor state. The AggregateActor orchestrator calls `SaveStateAsync` to commit the EventsPublished checkpoint atomically. However, UNLIKE event persistence, publication is an EXTERNAL side effect -- once `PublishEventAsync` is called, the message is sent regardless of whether `SaveStateAsync` succeeds. This means: if `SaveStateAsync` fails after publication, the event is published but the checkpoint isn't saved. On resume, the actor will re-publish (acceptable because subscribers must handle duplicates per at-least-once delivery).

- **Partial publication is acceptable.** If publishing 5 events and event 3 fails, events 1-2 are already published. The state machine transitions to PublishFailed. On recovery (Story 4.4), a drain mechanism can detect which events were published and resume from where it left off. For Story 4.1, partial failure = PublishFailed terminal state.

- **Rejection events ARE published.** Per D3, rejection events are normal events persisted to the event stream. They follow the same publication path. Subscribers receive rejection events on the same topic and can filter by `cloudevent.type` if they want to distinguish rejections from state-change events.

- **No retry logic in EventPublisher.** Per enforcement rule #4, all retries are DAPR resiliency policies configured at the sidecar level. The EventPublisher makes a single `PublishEventAsync` call per event. DAPR's resiliency component handles transient failures with configured retry, circuit breaker, and timeout policies. If DAPR exhausts retries, the exception propagates to the EventPublisher which catches it and returns a failure result.

- **EventPublisher is transient-scoped.** Unlike SnapshotManager (singleton), EventPublisher depends on `DaprClient` which is typically scoped or transient in DAPR middleware. Register as transient in DI. The EventPublisher holds no state between calls.

- **CloudEvents `source` format.** Using `hexalith-eventstore/{tenant}/{domain}` as the source URI provides clear identification of the event origin. This follows CloudEvents spec which recommends URIs for the `source` attribute. The tenant and domain in the source enable subscriber-side filtering.

- **CloudEvents `id` uniqueness.** Using `{correlationId}:{sequenceNumber}` guarantees global uniqueness: correlationId is unique per command, and sequenceNumber is unique per event within an aggregate stream. This combination is unique across all events ever produced by the system.

- **Integration point is BETWEEN EventsStored and Completed in AggregateActor.** The state machine flow becomes: `Processing -> EventsStored -> [PUBLISH EVENTS] -> EventsPublished -> Completed`. The exact code location is where Story 3.11 left the placeholder comment. The new flow for the happy path adds two state transitions: checkpoint EventsPublished after publication, then transition to Completed.

### Existing Patterns to Follow

**DaprClient.PublishEventAsync pattern (from DAPR docs):**
```csharp
var metadata = new Dictionary<string, string>
{
    ["cloudevent.type"] = eventEnvelope.EventTypeName,
    ["cloudevent.source"] = $"hexalith-eventstore/{identity.TenantId}/{identity.Domain}",
    ["cloudevent.id"] = $"{correlationId}:{eventEnvelope.SequenceNumber}"
};

await daprClient.PublishEventAsync(
    pubsubName,
    identity.PubSubTopic,
    eventEnvelope,
    metadata,
    cancellationToken).ConfigureAwait(false);
```

**Atomic batch pattern (from AggregateActor -- to extend, not change):**
```csharp
// EXISTING (Story 3.11): Events persisted + EventsStored checkpoint
await eventPersister.PersistEventsAsync(identity, events, currentSequence);
await stateMachine.CheckpointAsync(pipelineKey, new PipelineState(..., EventsStored, ...));
await StateManager.SaveStateAsync().ConfigureAwait(false);

// NEW (Story 4.1): Publish events + EventsPublished checkpoint
var publishResult = await eventPublisher.PublishEventsAsync(identity, persistedEvents, correlationId);
if (publishResult.Success)
{
    await stateMachine.CheckpointAsync(pipelineKey, new PipelineState(..., EventsPublished, ...));
    // ... then transition to Completed with cleanup
}
else
{
    // Transition to PublishFailed terminal state
}
await StateManager.SaveStateAsync().ConfigureAwait(false);
```

**Advisory status write pattern (from Story 3.11):**
```csharp
try
{
    await statusStore.WriteStatusAsync(
        command.TenantId, command.CorrelationId, CommandStatus.EventsPublished);
}
catch (Exception ex)
{
    logger.LogWarning(ex,
        "Advisory status write failed: CorrelationId={CorrelationId}, Status={Status}",
        command.CorrelationId, CommandStatus.EventsPublished);
    // Never throw -- rule #12
}
```

**OpenTelemetry activity pattern (from Story 3.11):**
```csharp
using var activity = EventStoreActivitySource.Instance.StartActivity(
    "EventStore.Events.Publish");
activity?.SetTag("eventstore.correlation_id", correlationId);
activity?.SetTag("eventstore.tenant_id", identity.TenantId);
activity?.SetTag("eventstore.domain", identity.Domain);
activity?.SetTag("eventstore.aggregate_id", identity.AggregateId);
activity?.SetTag("eventstore.event_count", events.Count);
activity?.SetTag("eventstore.topic", identity.PubSubTopic);
```

**Primary constructor pattern (from existing code):**
```csharp
public class EventPublisher(
    DaprClient daprClient,
    IOptions<EventPublisherOptions> options,
    ILogger<EventPublisher> logger) : IEventPublisher
{
    // ...
}
```

### Mandatory Coding Patterns

- Primary constructors: `public class EventPublisher(DaprClient daprClient, IOptions<EventPublisherOptions> options, ILogger<EventPublisher> logger)`
- Records for immutable data: `EventPublishResult`, `EventPublisherOptions`
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions in tests
- Feature folder organization (`Events/` folder in Server project for publisher, `Configuration/` for options)
- **Rule #4:** No custom retry logic -- DAPR resiliency handles retries
- **Rule #5:** Never log event payload data -- only envelope metadata fields
- **Rule #6:** IActorStateManager for all actor state operations
- **Rule #9:** correlationId in every structured log entry and OpenTelemetry activity
- **Rule #12:** Advisory status writes -- failures logged, never thrown
- **Rule #13:** No stack traces in error responses
- **SEC-5:** Event payload data never in logs

### Project Structure Notes

**New files:**
- `src/Hexalith.EventStore.Server/Events/IEventPublisher.cs` -- publication interface
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` -- DAPR pub/sub implementation with CloudEvents metadata
- `src/Hexalith.EventStore.Server/Events/EventPublishResult.cs` -- publication result record
- `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs` -- pub/sub component configuration
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs` -- test double
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs` -- unit tests
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublishResultTests.cs` -- result record tests
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventPublicationIntegrationTests.cs` -- actor integration tests

**Modified files:**
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- insert publication step between EventsStored and Completed transitions
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` -- register IEventPublisher and EventPublisherOptions
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs` -- add IEventPublisher mock to test setup

### Previous Story Intelligence

**From Story 3.11 (Actor State Machine & Checkpointed Stages):**
- ActorStateMachine checkpoints pipeline stages via `SetStateAsync` (staged, not committed)
- PipelineState record tracks: CorrelationId, CurrentStage, CommandType, StartedAt, EventCount, RejectionEventType
- Key pattern: `{identity.PipelineKeyPrefix}{correlationId}`
- EventsPublished stage is currently a PLACEHOLDER -- this story fills it
- Code comment at exact insertion point: "State machine checkpointing deferred to Story 3.11" / "EventsPublished stage is a PLACEHOLDER for Epic 4"
- Advisory status writes wrapped in try/catch per rule #12
- OpenTelemetry activities created via `EventStoreActivitySource.Instance.StartActivity()`
- All checkpoint writes use SetStateAsync (staging), caller commits with SaveStateAsync

**From Story 3.10 (State Reconstruction from Snapshot + Tail Events):**
- 703 tests pass (687 existing + 16 new)
- RehydrationResult separates snapshot state from tail events
- AggregateActor Step 3 loads snapshot FIRST, then tail events
- No changes to IDomainProcessor or DomainProcessorBase

**From Story 3.7 (Event Persistence):**
- EventPersister returns persisted EventEnvelope instances (needed for publication)
- Events stored as write-once keys: `{tenant}:{domain}:{aggId}:events:{seq}`
- All 11 envelope metadata fields populated by EventStore (SEC-1)
- EventPersister uses SetStateAsync for staging, actor calls SaveStateAsync for commit

**Current AggregateActor state machine flow (from Story 3.11):**
```
Processing -> EventsStored -> [PLACEHOLDER for EventsPublished] -> Completed | Rejected | PublishFailed
```
After this story:
```
Processing -> EventsStored -> EventsPublished -> Completed
                          |-> PublishFailed (if pub/sub unavailable)
```
And for rejections:
```
Processing -> EventsStored -> EventsPublished -> Completed (with Rejected advisory status)
```

### Git Intelligence

Recent commits show the progression through Epic 3:
- `f79aabe` Story 3.10: State reconstruction from snapshot + tail events (#35)
- `c120c19` Stories 3.6-3.9: Multi-tenant, event persistence, key isolation, snapshots (#33)
- Story 3.11 is in-progress (files exist but not yet committed)
- Patterns: Primary constructors, records, ConfigureAwait(false), NSubstitute + Shouldly
- DI registration via `Add*` extension methods in `ServiceCollectionExtensions.cs`
- Feature folder organization throughout
- Comprehensive test coverage with descriptive test method names

### Testing Requirements

**Unit Tests (~13 new):**
- EventPublisher: single event, multi-event, topic derivation, CloudEvents metadata fields
- EventPublisher: failure handling, partial failure, empty event list
- EventPublisher: OpenTelemetry activity creation, structured logging
- EventPublisher: configurable pub/sub name
- EventPublishResult: correct construction

**Integration Tests (~8 new):**
- AggregateActor: full state machine transitions with publication (happy path)
- AggregateActor: publication failure -> PublishFailed terminal state
- AggregateActor: no-op skips publication
- AggregateActor: rejection events published
- AggregateActor: advisory status writes at EventsPublished stage
- AggregateActor: checkpoint atomicity for EventsPublished
- AggregateActor: pipeline cleanup on PublishFailed

**Existing Test Updates (~10-20 modified):**
- AggregateActorTests: add IEventPublisher mock to test setup
- All existing actor tests must pass with new dependency

**Total estimated: ~25-40 tests (new + modified)**

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 4, Story 4.1]
- [Source: _bmad-output/planning-artifacts/architecture.md#EventPublisher in project structure]
- [Source: _bmad-output/planning-artifacts/architecture.md#D6 Pub/Sub Topic Naming]
- [Source: _bmad-output/planning-artifacts/architecture.md#FR17 Event publication via pub/sub with CloudEvents 1.0]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR5 Pub/sub delivery latency under 50ms]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR22 Zero events lost]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR25 Crash recovery without duplicate persistence]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR28 Any DAPR-compatible pub/sub]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 4 No custom retry]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 12 Advisory status writes]
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC-5 Event payload never in logs]
- [Source: _bmad-output/planning-artifacts/architecture.md#AggregateActor thin orchestrator pipeline]
- [Source: _bmad-output/planning-artifacts/architecture.md#OpenTelemetry Activity Naming: EventStore.Events.Publish]
- [Source: _bmad-output/planning-artifacts/architecture.md#Structured Logging Pattern: Events published fields]
- [Source: _bmad-output/implementation-artifacts/3-11-actor-state-machine-and-checkpointed-stages.md]
- [Source: _bmad-output/implementation-artifacts/3-10-state-reconstruction-from-snapshot-plus-tail-events.md]
- [Source: DAPR docs: https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-cloudevents/]

## Change Log

- 2026-02-14: Story 4.1 implemented -- EventPublisher with CloudEvents 1.0 (all 9 tasks, 25 new tests, 755 total pass)

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- EventPersister return type changed from `Task<long>` to `Task<EventPersistResult>` to provide persisted envelopes for publication (implicit prerequisite not listed in story tasks but required for EventPublisher to function)
- EventStoreActivitySource did not have `EventStore.Events.Publish` constant pre-defined as story notes suggested; added it along with `TagEventCount` and `TagTopic` tag constants
- AggregateActor EventsPublished checkpoint is batched with terminal SaveStateAsync (3 total saves for success path, not 4)
- PublishFailed path: pipeline cleanup, idempotency record, and SaveStateAsync all happen in one atomic batch

### Completion Notes List

- Task 0: All prerequisites verified. 730 existing tests pass. Insertion point confirmed at line 294 of AggregateActor.cs.
- Task 1: Created IEventPublisher, EventPublisher, EventPublishResult. EventPublisher uses DaprClient.PublishEventAsync with CloudEvents metadata override. OpenTelemetry activity "EventStore.Events.Publish" with 6 tags. Structured logging at Information/Error levels. No payload data in logs (SEC-5). No custom retry logic (rule #4).
- Task 2: Created EventPublisherOptions with PubSubName default "pubsub". Registered via options pattern binding "EventStore:Publisher".
- Task 3: Integrated EventPublisher into AggregateActor between EventsStored and Completed. Success path: checkpoint EventsPublished + advisory status + proceed to Completed. Failure path: checkpoint PublishFailed + cleanup + advisory status. No-op skips publication. Rejection events ARE published (D3).
- Task 4: Registered IEventPublisher -> EventPublisher as transient. EventPublisherOptions bound to configuration. DaprClient already registered via AddActors.
- Task 5: Created FakeEventPublisher with configurable failure and partial failure modes. Tracks all publish calls for test assertions.
- Task 6: 13 unit tests for EventPublisher covering CloudEvents metadata, topic derivation, configurable pub/sub name, failure handling, partial failure, empty list, OpenTelemetry activity, structured logging.
- Task 7: 8 integration tests for AggregateActor publication covering success transitions, PublishFailed, no-op skip, rejection publish, pipeline cleanup, advisory status writes, checkpoint atomicity.
- Task 8: Updated CreateActorWithMockState and CreateActorWithAllMocks in AggregateActorTests + CreateActor in StateMachineIntegrationTests to include IEventPublisher mock with default success behavior. All existing tests pass.
- Task 9: All 755 tests pass (730 existing + 25 new). Zero regressions.
- Supporting change: EventPersister now returns EventPersistResult (NewSequenceNumber + PersistedEnvelopes) instead of just long. FakeEventPersister updated accordingly.

### File List

**New files:**
- `src/Hexalith.EventStore.Server/Events/IEventPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/EventPublishResult.cs`
- `src/Hexalith.EventStore.Server/Events/EventPersistResult.cs`
- `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs`
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublishResultTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventPublicationIntegrationTests.cs`

**Modified files:**
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (added IEventPublisher dependency, publication step between EventsStored and Completed)
- `src/Hexalith.EventStore.Server/Events/IEventPersister.cs` (return type changed to EventPersistResult)
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs` (collects and returns persisted envelopes)
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` (registered IEventPublisher + EventPublisherOptions)
- `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` (added EventsPublish, TagEventCount, TagTopic constants)
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs` (updated return type to EventPersistResult)
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs` (added IEventPublisher mock to test setup)
- `tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs` (added IEventPublisher mock to test setup)
