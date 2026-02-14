# Story 3.4: Event Stream Reader & State Rehydration

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **system operator**,
I want the AggregateActor to reconstruct aggregate state by replaying all events in sequence from the event stream,
So that the actor has the correct current state before invoking the domain service (FR12).

## Acceptance Criteria

1. **Event stream reading for new aggregates** - Given the AggregateActor passes tenant validation (Step 2), When Step 3 (state rehydration) executes for a new aggregate (no prior events), Then the current state is null (passed to domain service as `CurrentState? = null`), And EventStreamReader confirms no events exist for this aggregate, And processing continues to Step 4 (domain service invocation stub) with null state.

2. **Event stream reading for existing aggregates** - When state rehydration executes for an existing aggregate, Then EventStreamReader reads all events from sequence 1 to current from the state store, And events are read using composite key pattern `{tenant}:{domain}:{aggId}:events:{seq}` (D1), And events are replayed in strict sequence order to reconstruct current state, And only IActorStateManager is used for state store access (enforcement rule #6).

3. **Performance requirement met** - Given an aggregate with 1,000 events, When EventStreamReader performs full replay from sequence 1 to 1,000, Then the replay completes within 100ms (NFR6), And the actor activation remains within the 5s DAPR sidecar timeout (enforcement rule #14).

4. **Backward-compatible deserialization enforced** - Given the EventStreamReader encounters an event it cannot deserialize, When the event type is unknown or the deserialization fails, Then the EventStreamReader throws `UnknownEventException` with the event sequence number, tenant, domain, aggregateId, and event type name (architecture CRITICAL-1 revision), And the actor does NOT skip the unknown event and continue (skipping produces incorrect state), And the exception propagates to the handler (where it's converted to a 500 ProblemDetails error).

5. **EventStreamReader is a focused, testable component** - The `EventStreamReader` is a separate class implementing `IEventStreamReader`, And it is injected into the AggregateActor's Step 3 via the actor's service provider (same pattern as IdempotencyChecker and TenantValidator), And it encapsulates all event loading and deserialization logic, And it has a single method: `Task<object?> RehydrateAsync(AggregateIdentity identity)` returning the rehydrated aggregate state (or null for new aggregates).

6. **Existing tests unbroken** - All existing tests (estimated ~414 from Story 3.3) continue to pass after the AggregateActor's Step 3 STUB is replaced with EventStreamReader. Unit tests verify event stream reading logic. Integration tests continue to work via the mocked/faked actor infrastructure.

## Prerequisites

**BLOCKING: Story 3.3 MUST be complete (done status) before starting Story 3.4.** Story 3.4 depends on:
- `AggregateActor` with 5-step orchestrator pattern (Step 3 is currently a STUB to be replaced) (Story 3.2)
- `TenantValidator` enforcing SEC-2 constraint (tenant validation BEFORE state rehydration) (Story 3.3)
- `IdempotencyChecker` for idempotency records (Story 3.2)
- `CommandProcessingResult` record with `Accepted`, `ErrorMessage`, `CorrelationId` (Story 3.1)
- `CommandEnvelope` with `TenantId`, `AggregateIdentity`, etc. (Story 1.2)
- `AggregateIdentity` with composite key derivation methods (Story 1.2)
- All Epic 2 infrastructure

**Before beginning any Task below, verify:** Run existing tests to confirm all Story 3.3 artifacts are in place. All existing tests must pass before proceeding.

## Tasks / Subtasks

- [ ] Task 0: Verify prerequisites and existing artifacts (BLOCKING)
  - [ ] 0.1 Run all existing tests -- they must pass before proceeding
  - [ ] 0.2 Confirm `AggregateActor` has 5-step orchestrator with Step 3 as STUB (Story 3.2)
  - [ ] 0.3 Confirm `TenantValidator` is in place executing before Step 3 (Story 3.3, SEC-2)
  - [ ] 0.4 Confirm `CommandEnvelope.AggregateIdentity` property exists
  - [ ] 0.5 Confirm `AggregateIdentity` has `EventStreamKeyPrefix` property returning `{tenant}:{domain}:{aggId}:events:`
  - [ ] 0.6 Confirm `IActorStateManager` is available on `AggregateActor` base class via `this.StateManager`
  - [ ] 0.7 Confirm Dapr.Actors 1.16.1+ is in Server.csproj dependencies

- [ ] Task 1: Create IEventStreamReader interface (AC: #5)
  - [ ] 1.1 Create `IEventStreamReader` interface in `Server/Events/`
  - [ ] 1.2 Define single method: `Task<object?> RehydrateAsync(AggregateIdentity identity)` -- returns rehydrated state or null for new aggregates
  - [ ] 1.3 Namespace: `Hexalith.EventStore.Server.Events`

- [ ] Task 2: Create UnknownEventException (AC: #4)
  - [ ] 2.1 Create `UnknownEventException` class in `Server/Events/` extending `InvalidOperationException`
  - [ ] 2.2 Properties: `long SequenceNumber`, `string TenantId`, `string Domain`, `string AggregateId`, `string EventTypeName`
  - [ ] 2.3 Constructor: `UnknownEventException(long sequenceNumber, string tenantId, string domain, string aggregateId, string eventTypeName)` with message: `$"UnknownEvent during state rehydration: sequence {sequenceNumber}, type '{eventTypeName}', aggregate {tenantId}:{domain}:{aggregateId}. Domain service must maintain backward-compatible deserialization for all event types."`
  - [ ] 2.4 Namespace: `Hexalith.EventStore.Server.Events`

- [ ] Task 3: Create EventEnvelope data type for deserialization (AC: #2, #4)
  - [ ] 3.1 Create `EventEnvelope` record in `Server/Events/` -- this is the STORAGE representation of events (distinct from the CommandEnvelope)
  - [ ] 3.2 Properties (11 metadata fields from architecture): `string AggregateId`, `string TenantId`, `string Domain`, `long SequenceNumber`, `DateTimeOffset Timestamp`, `string CorrelationId`, `string CausationId`, `string UserId`, `string DomainServiceVersion`, `string EventTypeName`, `string SerializationFormat`, `byte[] Payload`, `IDictionary<string, string>? Extensions`
  - [ ] 3.3 This record is what gets deserialized from IActorStateManager state keys
  - [ ] 3.4 Add property: `AggregateIdentity Identity => new(TenantId, Domain, AggregateId)` for convenience
  - [ ] 3.5 Namespace: `Hexalith.EventStore.Server.Events`

- [ ] Task 4: Create EventStreamReader implementation (AC: #1, #2, #3, #5)
  - [ ] 4.1 Create `EventStreamReader` class in `Server/Events/` implementing `IEventStreamReader`
  - [ ] 4.2 Constructor: `EventStreamReader(IActorStateManager stateManager, ILogger<EventStreamReader> logger)` -- lightweight, created per actor call
  - [ ] 4.3 `RehydrateAsync` implementation:
    - Extract event stream key prefix from identity: `identity.EventStreamKeyPrefix` (e.g., `"acme:orders:order-42:events:"`)
    - Load aggregate metadata to get current sequence number (key: `identity.MetadataKey` = `"{tenant}:{domain}:{aggId}:metadata"`)
    - If metadata does not exist, return null (new aggregate, no events) (AC #1)
    - If metadata exists, extract `currentSequence` property
    - Loop from sequence 1 to currentSequence, loading each event via `stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}{seq}")`
    - For each loaded event, deserialize the Payload and apply to aggregate state (STUB for Story 3.4 -- just accumulate events in a list for now, actual domain-specific state reconstruction is Story 3.5+)
    - Return the accumulated state object (or null if no events)
  - [ ] 4.4 Handle missing metadata: If `TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)` returns HasValue=false, log at Debug level `"New aggregate detected: no events found for {identity.ActorId}"` and return null
  - [ ] 4.5 Handle unknown event type during deserialization: If an event's `EventTypeName` cannot be deserialized, throw `UnknownEventException` with event details (AC #4)
  - [ ] 4.6 Performance logging: Log at Debug level the event count and elapsed time: `"State rehydrated: {eventCount} events in {elapsed}ms for {actorId}"`
  - [ ] 4.7 Use `ArgumentNullException.ThrowIfNull()` on identity parameter (CA1062)
  - [ ] 4.8 `ConfigureAwait(false)` on all async calls (CA2007)

- [ ] Task 5: Create AggregateMetadata data type (AC: #2)
  - [ ] 5.1 Create `AggregateMetadata` record in `Server/Events/`: `record AggregateMetadata(long CurrentSequence, DateTimeOffset LastModified, string? ETag)`
  - [ ] 5.2 This is what gets stored at the metadata key `{tenant}:{domain}:{aggId}:metadata`
  - [ ] 5.3 `CurrentSequence` is the last persisted event sequence number
  - [ ] 5.4 `ETag` is used for optimistic concurrency (Story 3.7+)
  - [ ] 5.5 Namespace: `Hexalith.EventStore.Server.Events`

- [ ] Task 6: Update AggregateActor to replace Step 3 STUB with EventStreamReader (AC: #1, #6)
  - [ ] 6.1 In `AggregateActor.ProcessCommandAsync`, replace the Step 3 STUB log line with actual EventStreamReader call
  - [ ] 6.2 Create `EventStreamReader` by resolving `ILogger<EventStreamReader>` from `host.LoggerFactory.CreateLogger<EventStreamReader>()` and passing `this.StateManager` (same pattern as IdempotencyChecker and TenantValidator)
  - [ ] 6.3 Call `object? currentState = await eventStreamReader.RehydrateAsync(command.AggregateIdentity).ConfigureAwait(false)`
  - [ ] 6.4 Store the currentState in a local variable for use in Step 4 (domain service invocation -- which is still a stub, so just log it for now)
  - [ ] 6.5 Log at Information level after rehydration: `"State rehydrated: {stateType} for ActorId={ActorId}, CorrelationId={CorrelationId}"` where stateType is `currentState?.GetType().Name ?? "null"`
  - [ ] 6.6 If EventStreamReader throws `UnknownEventException`, let it propagate to the caller (the CommandRouter will forward it to the exception handler chain which converts it to 500 ProblemDetails)
  - [ ] 6.7 The existing try/catch for `TenantMismatchException` (Story 3.3) should remain -- do NOT catch broader exceptions that would swallow `UnknownEventException`
  - [ ] 6.8 If rehydration passes, continue to Steps 4-5 as before (stubs)

- [ ] Task 7: Write unit tests for EventStreamReader (AC: #1, #2, #3, #4, #5)
  - [ ] 7.1 Create `EventStreamReaderTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Events/`
  - [ ] 7.2 `RehydrateAsync_NewAggregate_ReturnsNull` -- verify no metadata = null state (AC #1)
  - [ ] 7.3 `RehydrateAsync_ExistingAggregate_ReadsEventsFromSequence1` -- verify events loaded in order
  - [ ] 7.4 `RehydrateAsync_ExistingAggregate_UsesCorrectKeyPattern` -- verify composite key pattern `{tenant}:{domain}:{aggId}:events:{seq}` (AC #2)
  - [ ] 7.5 `RehydrateAsync_ThousandEvents_CompletesWithin100ms` -- verify NFR6 performance (AC #3)
  - [ ] 7.6 `RehydrateAsync_UnknownEventType_ThrowsUnknownEventException` -- verify backward compatibility enforcement (AC #4)
  - [ ] 7.7 `RehydrateAsync_UnknownEvent_ExceptionContainsSequenceAndType` -- verify exception details
  - [ ] 7.8 `RehydrateAsync_MissingEvent_ThrowsException` -- verify gap detection (event 5 exists but event 4 is missing)
  - [ ] 7.9 `RehydrateAsync_NullIdentity_ThrowsArgumentNullException` -- verify guard clause
  - [ ] 7.10 Mock `IActorStateManager` using NSubstitute -- configure `TryGetStateAsync<AggregateMetadata>` and `TryGetStateAsync<EventEnvelope>` to return test data

- [ ] Task 8: Write unit tests for AggregateActor state rehydration flow (AC: #1, #6)
  - [ ] 8.1 Update `AggregateActorTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Actors/`
  - [ ] 8.2 `ProcessCommandAsync_NewAggregate_RehydratesNullState` -- verify Step 3 returns null for new aggregate
  - [ ] 8.3 `ProcessCommandAsync_ExistingAggregate_RehydratesState` -- verify Step 3 loads events and returns state
  - [ ] 8.4 `ProcessCommandAsync_StateRehydrated_ProceedsToStep4` -- verify Steps 4-5 stubs execute after successful rehydration
  - [ ] 8.5 `ProcessCommandAsync_UnknownEvent_PropagatesException` -- verify UnknownEventException propagates to caller (not caught in actor)
  - [ ] 8.6 `ProcessCommandAsync_StateRehydration_LogsStateType` -- verify logging of rehydrated state type

- [ ] Task 9: Write integration tests (AC: #6)
  - [ ] 9.1 Integration tests operate at the HTTP level where the `ICommandRouter` is mocked. State rehydration at the actor level cannot be easily tested in integration tests without real DAPR infrastructure
  - [ ] 9.2 Verify all existing integration tests still pass -- this is the primary integration test validation
  - [ ] 9.3 Real state rehydration testing will be in Tier 2 (DAPR test containers) in Story 7.4

- [ ] Task 10: Update existing tests for state rehydration changes (AC: #6)
  - [ ] 10.1 Update `AggregateActorTests` that tested the Step 3 STUB to verify the new EventStreamReader flow
  - [ ] 10.2 Ensure all tests that construct mock `IActorStateManager` provide metadata and event data if testing existing aggregates
  - [ ] 10.3 Verify ALL existing tests pass after the changes

- [ ] Task 11: Run all tests and verify zero regressions (AC: #6)
  - [ ] 11.1 Run all existing tests -- zero regressions expected
  - [ ] 11.2 Run new tests -- all must pass
  - [ ] 11.3 Verify total test count (estimated: ~414 existing from Story 3.3 + ~20 new = ~434)

## Dev Notes

### Architecture Compliance

**FR12: Aggregate State Reconstruction via Event Replay:**
The EventStreamReader implements the core event sourcing pattern: reconstructing current aggregate state by replaying all events in the event stream from sequence 1 to current. This is Step 3 in the actor's 5-step orchestrator, executed AFTER tenant validation (SEC-2) and BEFORE domain service invocation.

**Architecture Data Flow (Story 3.4 scope):**
```
AggregateActor.ProcessCommandAsync(CommandEnvelope command)
    |-- Log command receipt (preserved from Story 3.1)
    |-- Step 1: IdempotencyChecker.CheckAsync(causationId)
    |      |-- If duplicate: return cached result
    |-- Step 2: TenantValidator.Validate(command.TenantId, Host.Id.GetId())  <-- Story 3.3 (SEC-2)
    |      |-- If mismatch: return rejection result
    |-- Step 3: EventStreamReader.RehydrateAsync(command.AggregateIdentity)  <-- THIS STORY
    |      |-- Load metadata key: {tenant}:{domain}:{aggId}:metadata
    |      |-- If no metadata: return null (new aggregate)
    |      |-- If metadata exists: load events from seq 1 to currentSequence
    |      |-- For each event: deserialize and accumulate (STUB: actual state reconstruction is domain-specific, Story 3.5+)
    |      |-- Return rehydrated state object (or null)
    |-- Step 4: Domain service invocation (STUB -> Story 3.5) [receives currentState from Step 3]
    |-- Step 5: State machine execution (STUB -> Story 3.11)
    |-- Create CommandProcessingResult(Accepted: true)
    |-- IdempotencyChecker.RecordAsync(causationId, result)
    |-- StateManager.SaveStateAsync() [atomic commit]
    |-- Return result
```

**D1: Event Storage Strategy - Single-Key-Per-Event with Composite Keys:**
From architecture document (Lines 357-363), events are stored with individual state store keys following the pattern:
- **Event key:** `{tenant}:{domain}:{aggId}:events:{seq}` (e.g., `acme:orders:order-42:events:5`)
- **Metadata key:** `{tenant}:{domain}:{aggId}:metadata` (e.g., `acme:orders:order-42:metadata`)
- **Snapshot key:** `{tenant}:{domain}:{aggId}:snapshot` (e.g., `acme:orders:order-42:snapshot`) -- Story 3.9+

The EventStreamReader uses `AggregateIdentity.EventStreamKeyPrefix` to derive the event key pattern. The metadata key stores `AggregateMetadata(CurrentSequence, LastModified, ETag)` which tells the reader how many events to load.

**SEC-2: Tenant Validation BEFORE State Rehydration (CRITICAL SECURITY CONSTRAINT):**
From architecture document (Lines 112, 536, 1055), tenant validation (Step 2, implemented in Story 3.3) MUST execute BEFORE state rehydration (Step 3, this story). This prevents a malicious command from triggering state loading for a tenant the user is not authorized to access. The ordering is enforced by the actor orchestrator's strict step sequence.

**NFR6: Performance Requirement - 1,000 Events in <100ms:**
From architecture document (Line 994), the EventStreamReader must complete state rehydration of 1,000 events within 100ms at p99. This drives the snapshot strategy (Story 3.9+) -- for aggregates with >100 events, snapshots are mandatory to keep reads ≤102 state store calls. For Story 3.4 (full replay only), the test `RehydrateAsync_ThousandEvents_CompletesWithin100ms` validates this constraint.

**Enforcement Rules to Follow:**
- Rule #5: Never log event payload data -- only envelope metadata (SEC-5, NFR12)
- Rule #6: Use `IActorStateManager` for all actor state operations -- NEVER bypass with DaprClient (CRITICAL for Story 3.4)
- Rule #9: correlationId in every structured log entry
- Rule #11: Event store keys are write-once -- never updated or deleted (Story 3.7+ enforcement)
- Rule #14: DAPR sidecar call timeout is 5 seconds -- all IActorStateManager calls must complete within 5s
- Rule #15: Snapshot configuration is mandatory (default 100 events) -- keeps reads ≤102 (Story 3.9+ implementation)

### Critical Design Decisions

**F1 (Architecture): EventStreamReader reads from sequence 1, NOT from latest snapshot.**
Story 3.4 implements FULL REPLAY only. Snapshot support (reading snapshot + tail events) is Story 3.9-3.10. This allows incremental implementation: get full replay working first, then optimize with snapshots. The `RehydrateAsync` signature is designed to support both: it returns `object?` (the rehydrated state) whether that state came from full replay or snapshot+tail.

**F2 (CRITICAL Revision from Architecture): UnknownEvent during rehydration is an ERROR, not a skip.**
From architecture document (Lines 1016-1022, CRITICAL-1 revision): The original design allowed skipping unknown event types during state rehydration. This is fundamentally incorrect. If events 5, 6, 7 exist and event 6 is 'unknown,' skipping it produces aggregate state that reflects only events 5 and 7 -- which is WRONG. The correct state MUST include event 6's effects. Therefore: Domain services MUST maintain backward-compatible deserialization for ALL event types they have ever produced. `UnknownEventException` during rehydration is an error condition that returns 500 to the caller. Recovery: redeploy previous domain service version, then add backward-compatible deserializer for the removed event type.

**F3 (Design): EventStreamReader is created per-call, not DI-registered.**
Same pattern as IdempotencyChecker (Story 3.2, F3) and TenantValidator (Story 3.3, F4). The reader is lightweight (only needs `IActorStateManager` and logger) and is created in the actor method. DAPR actors do not support standard constructor injection for scoped services. The `IActorStateManager` is scoped to the actor instance and is available only via `this.StateManager`.

**F4 (Scope): Story 3.4 does NOT implement domain-specific state reconstruction.**
The EventStreamReader loads events from the state store and accumulates them in a list (or similar structure). The actual domain-specific state reconstruction -- applying each event to aggregate state via domain logic -- is Story 3.5+ where the domain service is invoked. For Story 3.4, the "rehydrated state" is a placeholder (e.g., a list of EventEnvelopes or a generic state object). This allows Story 3.4 to focus on the infrastructure concern (reading events from state store) without getting blocked on domain-specific logic.

**F5 (Performance): Reading 1,000 events requires ~1,000 IActorStateManager reads.**
At ~1-2ms per DAPR sidecar call (NFR8), 1,000 reads = ~1,000-2,000ms naively. To meet the 100ms NFR6 constraint, the EventStreamReader must batch reads or use async parallelism (e.g., load 10 events concurrently). DAPR `IActorStateManager` supports batch reads via multiple async tasks. Consider: `var tasks = Enumerable.Range(1, currentSequence).Select(seq => stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}{seq}")).ToArray(); await Task.WhenAll(tasks);` This parallelizes the reads, reducing total time from 1,000 sequential round-trips to ~10-20 concurrent batches.

**F6 (Atomicity): EventStreamReader is READ-ONLY in Step 3.**
The reader does NOT modify state. It only reads events. All state writes (event persistence, metadata updates) happen in Step 5 (state machine execution, Story 3.11). This separation ensures that if rehydration fails (e.g., UnknownEventException), no partial state is persisted. The actor's `SaveStateAsync` in Step 5 commits ALL state changes atomically.

**F7 (Pre-mortem): Missing events in the sequence indicate data corruption.**
If the metadata says `CurrentSequence = 10` but event sequence 7 is missing (TryGetStateAsync returns HasValue=false), this indicates state store data corruption or a bug in the event persistence logic (Story 3.7+). The EventStreamReader should throw an exception (e.g., `MissingEventException`) rather than silently skip the gap. A gap in the event stream produces incorrect aggregate state. The exception should include the missing sequence number, tenant, domain, and aggregateId for debugging.

**F8 (Failure Mode): Metadata exists but CurrentSequence is 0 or negative.**
If the metadata key exists but `CurrentSequence` is ≤ 0, this is an invalid state. The reader should throw `InvalidOperationException($"Invalid aggregate metadata: CurrentSequence={currentSequence} for {identity.ActorId}")`. This guards against corrupt metadata writes.

**F9 (First Principles): Why metadata key instead of enumerating event keys?**
Storing metadata separately (`{tenant}:{domain}:{aggId}:metadata` with `CurrentSequence`) allows O(1) lookup of the event count without enumerating all keys. DAPR state stores (Redis, PostgreSQL, Cosmos DB) do not efficiently support key prefix enumeration. Storing the sequence count in metadata enables the reader to loop from 1 to N without a scan. This is a standard event sourcing pattern.

**F10 (Red Team): Malformed EventEnvelope deserialization could cause crashes.**
If an event key's value is corrupt JSON or missing required fields, JSON deserialization will throw. The EventStreamReader should catch `JsonException` (or equivalent serialization exceptions) and wrap it in a more descriptive exception: `EventDeserializationException($"Failed to deserialize event at sequence {seq} for {identity.ActorId}: {ex.Message}", ex)`. This helps operators diagnose state store corruption vs. schema evolution issues.

**What Already Exists (from Stories 1.1-3.3):**
- `CommandEnvelope` in Contracts -- has `AggregateIdentity` property (Story 1.2)
- `AggregateIdentity` in Contracts -- has `EventStreamKeyPrefix`, `MetadataKey`, `SnapshotKey` derivation methods (Story 1.2)
- `AggregateActor` with 5-step orchestrator (Steps 1-2 real, Steps 3-5 stubs) (Stories 3.2-3.3)
- `IdempotencyChecker` + `IdempotencyRecord` (Story 3.2)
- `TenantValidator` + `TenantMismatchException` (Story 3.3)
- `CommandProcessingResult` record (Story 3.1)
- `ICommandRouter` + `CommandRouter` (Story 3.1)
- All Epic 2 infrastructure

**What Story 3.4 Adds:**
1. **`IEventStreamReader`** -- interface in Server/Events/
2. **`EventStreamReader`** -- implementation loading events from IActorStateManager in Server/Events/
3. **`EventEnvelope`** -- storage DTO record for persisted events in Server/Events/
4. **`AggregateMetadata`** -- metadata record with CurrentSequence, LastModified, ETag in Server/Events/
5. **`UnknownEventException`** -- exception for backward compatibility violations in Server/Events/
6. **Modified `AggregateActor`** -- Step 3 STUB replaced with EventStreamReader call

**What Story 3.4 Does NOT Change:**
- `IAggregateActor` interface (unchanged)
- `CommandProcessingResult` record (unchanged)
- `IdempotencyChecker` / `TenantValidator` (unchanged)
- `CommandEnvelope` / `AggregateIdentity` in Contracts (unchanged)
- Steps 4-5 remain stubs (domain invocation is Story 3.5, state machine is Story 3.11)
- No event WRITING yet -- that's Story 3.7 (Event Persistence)
- No snapshot support yet -- that's Stories 3.9-3.10

### EventStreamReader Pattern

```csharp
// In Server/Events/EventStreamReader.cs
namespace Hexalith.EventStore.Server.Events;

public class EventStreamReader(
    IActorStateManager stateManager,
    ILogger<EventStreamReader> logger) : IEventStreamReader
{
    public async Task<object?> RehydrateAsync(AggregateIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var sw = Stopwatch.StartNew();

        // Load metadata to get current sequence number
        var metadataResult = await stateManager
            .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)
            .ConfigureAwait(false);

        if (!metadataResult.HasValue)
        {
            logger.LogDebug("New aggregate detected: no events found for {ActorId}", identity.ActorId);
            return null; // AC #1: new aggregate
        }

        var metadata = metadataResult.Value;
        if (metadata.CurrentSequence <= 0)
        {
            throw new InvalidOperationException(
                $"Invalid aggregate metadata: CurrentSequence={metadata.CurrentSequence} for {identity.ActorId}"); // F8
        }

        // Load all events from sequence 1 to currentSequence
        string keyPrefix = identity.EventStreamKeyPrefix;
        var events = new List<EventEnvelope>();

        // F5: Parallel reads for performance (meets NFR6: 1,000 events in <100ms)
        var loadTasks = Enumerable.Range(1, (int)metadata.CurrentSequence)
            .Select(async seq =>
            {
                var eventResult = await stateManager
                    .TryGetStateAsync<EventEnvelope>($"{keyPrefix}{seq}")
                    .ConfigureAwait(false);

                if (!eventResult.HasValue)
                {
                    throw new MissingEventException(seq, identity.TenantId, identity.Domain, identity.AggregateId); // F7
                }

                return (Sequence: seq, Event: eventResult.Value);
            })
            .ToArray();

        var loadedEvents = await Task.WhenAll(loadTasks).ConfigureAwait(false);

        // Sort by sequence (paranoid -- should already be ordered)
        foreach (var (_, evt) in loadedEvents.OrderBy(x => x.Sequence))
        {
            events.Add(evt);
        }

        sw.Stop();
        logger.LogDebug("State rehydrated: {EventCount} events in {ElapsedMs}ms for {ActorId}",
            events.Count, sw.ElapsedMilliseconds, identity.ActorId); // F4: performance logging

        // F4: For Story 3.4, return the list of events as the "state"
        // Stories 3.5+ will apply domain-specific state reconstruction
        return events;
    }
}
```

### AggregateActor Updated Orchestrator Pattern

```csharp
// In Server/Actors/AggregateActor.cs (after Story 3.4)
public async Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command)
{
    ArgumentNullException.ThrowIfNull(command);

    logger.LogInformation(
        "Actor {ActorId} received command: CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}",
        Host.Id, command.CorrelationId, command.TenantId, command.Domain, command.AggregateId, command.CommandType);

    // Step 1: Idempotency check (unchanged from Story 3.2)
    var causationId = command.CausationId ?? command.CorrelationId;
    var idempotencyChecker = new IdempotencyChecker(
        StateManager, host.LoggerFactory.CreateLogger<IdempotencyChecker>());

    CommandProcessingResult? cached = await idempotencyChecker
        .CheckAsync(causationId).ConfigureAwait(false);
    if (cached is not null)
    {
        logger.LogInformation("Duplicate command detected: CausationId={CausationId}, ActorId={ActorId}. Returning cached result.",
            causationId, Host.Id);
        return cached;
    }

    // SEC-2 CRITICAL: This MUST execute before any state access (Step 3+)
    // Step 2: Tenant validation (unchanged from Story 3.3)
    var tenantValidator = new TenantValidator(
        host.LoggerFactory.CreateLogger<TenantValidator>());
    try
    {
        tenantValidator.Validate(command.TenantId, Host.Id.GetId());
    }
    catch (TenantMismatchException ex)
    {
        logger.LogWarning(
            "Tenant validation rejected command: CorrelationId={CorrelationId}, CommandTenant={CommandTenant}, ActorTenant={ActorTenant}",
            command.CorrelationId, ex.CommandTenant, ex.ActorTenant);

        var rejectionResult = new CommandProcessingResult(
            Accepted: false, ErrorMessage: ex.Message, CorrelationId: command.CorrelationId);

        await idempotencyChecker.RecordAsync(causationId, rejectionResult).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return rejectionResult;
    }

    // Step 3: State rehydration (THIS STORY)
    var eventStreamReader = new EventStreamReader(
        StateManager, host.LoggerFactory.CreateLogger<EventStreamReader>());

    object? currentState = await eventStreamReader
        .RehydrateAsync(command.AggregateIdentity)
        .ConfigureAwait(false);

    logger.LogInformation("State rehydrated: {StateType} for ActorId={ActorId}, CorrelationId={CorrelationId}",
        currentState?.GetType().Name ?? "null", Host.Id, command.CorrelationId);

    // Step 4: Domain service invocation (STUB -- Story 3.5) [receives currentState]
    logger.LogDebug("Step 4: Domain service invocation -- STUB (Story 3.5), currentState type: {StateType}",
        currentState?.GetType().Name ?? "null");

    // Step 5: State machine execution (STUB -- Story 3.11)
    logger.LogDebug("Step 5: State machine execution -- STUB (Story 3.11)");

    // Create result and store for idempotency
    var result = new CommandProcessingResult(
        Accepted: true, CorrelationId: command.CorrelationId);

    await idempotencyChecker.RecordAsync(causationId, result).ConfigureAwait(false);
    await StateManager.SaveStateAsync().ConfigureAwait(false);
    return result;
}
```

### Technical Requirements

**Existing Types to Use:**
- `CommandEnvelope` from `Hexalith.EventStore.Contracts.Commands` -- has `AggregateIdentity` property
- `AggregateIdentity` from `Hexalith.EventStore.Contracts.Identity` -- has `EventStreamKeyPrefix`, `MetadataKey`, `ActorId` properties
- `AggregateActor` from `Hexalith.EventStore.Server.Actors` -- 5-step orchestrator (Story 3.2-3.3)
- `IActorStateManager` from `Dapr.Actors.Runtime` -- `TryGetStateAsync<T>(string key)` for loading events
- `ActorHost` from `Dapr.Actors.Runtime` -- `host.LoggerFactory.CreateLogger<T>()`
- `IdempotencyChecker` / `TenantValidator` from Server/Actors/ (Stories 3.2-3.3)

**New Types to Create:**
- `IEventStreamReader` -- interface in Server/Events/
- `EventStreamReader` -- implementation in Server/Events/
- `EventEnvelope` -- storage DTO record in Server/Events/ (11 metadata fields + Payload + Extensions)
- `AggregateMetadata` -- metadata record in Server/Events/ (CurrentSequence, LastModified, ETag)
- `UnknownEventException` -- exception in Server/Events/
- `MissingEventException` -- exception in Server/Events/ (F7)
- `EventDeserializationException` -- exception in Server/Events/ (F10)

**NuGet Packages Required:**
- NO new NuGet packages needed for Story 3.4
- All existing packages remain unchanged (Dapr.Actors 1.16.1+ from Story 3.1)

### File Structure Requirements

**New files to create:**
```
src/Hexalith.EventStore.Server/
  Events/
    IEventStreamReader.cs              # NEW: Event stream reading interface
    EventStreamReader.cs               # NEW: Implementation loading events from IActorStateManager
    EventEnvelope.cs                   # NEW: Storage DTO for persisted events (11 metadata fields)
    AggregateMetadata.cs               # NEW: Metadata record (CurrentSequence, LastModified, ETag)
    UnknownEventException.cs           # NEW: Exception for unknown event types during rehydration
    MissingEventException.cs           # NEW: Exception for gaps in event sequence (F7)
    EventDeserializationException.cs   # NEW: Exception for corrupt event data (F10)

tests/Hexalith.EventStore.Server.Tests/
  Events/
    EventStreamReaderTests.cs          # NEW: Unit tests for EventStreamReader
    EventEnvelopeTests.cs              # NEW: Unit tests for EventEnvelope serialization
```

**Existing files to modify:**
```
src/Hexalith.EventStore.Server/
  Actors/
    AggregateActor.cs                  # MODIFY: Replace Step 3 STUB with EventStreamReader call

tests/Hexalith.EventStore.Server.Tests/
  Actors/
    AggregateActorTests.cs             # MODIFY: Update Step 3 STUB tests, add state rehydration tests
```

**Files NOT modified:**
```
src/Hexalith.EventStore.Server/
  Actors/
    IAggregateActor.cs                 # NO CHANGE
    CommandProcessingResult.cs         # NO CHANGE
    IIdempotencyChecker.cs             # NO CHANGE
    IdempotencyChecker.cs              # NO CHANGE
    ITenantValidator.cs                # NO CHANGE
    TenantValidator.cs                 # NO CHANGE

src/Hexalith.EventStore.Contracts/
  Commands/
    CommandEnvelope.cs                 # NO CHANGE (already has AggregateIdentity)
  Identity/
    AggregateIdentity.cs               # NO CHANGE (already has EventStreamKeyPrefix, MetadataKey)

src/Hexalith.EventStore.Server/
  Configuration/
    ServiceCollectionExtensions.cs     # NO CHANGE (EventStreamReader not DI-registered)

src/Hexalith.EventStore.CommandApi/
  Program.cs                           # NO CHANGE
```

### Testing Requirements

**Test Projects:**
- `tests/Hexalith.EventStore.Server.Tests/` -- Unit tests for EventStreamReader, EventEnvelope, AggregateActor state flow
- `tests/Hexalith.EventStore.IntegrationTests/` -- Regression verification (real state rehydration testing is Tier 2, Story 7.4)

**Test Patterns (established in Stories 1.6, 2.1-3.3):**
- Method naming: `{Method}_{Scenario}_{ExpectedResult}`
- Arrange/Act/Assert pattern
- Shouldly for assertions
- Primary constructors for DI injection
- Feature folder organization in test projects mirroring source
- NSubstitute for mocking `IActorStateManager`

**Unit Test Strategy for EventStreamReader:**
Mock `IActorStateManager` using NSubstitute. Verify:
- `TryGetStateAsync<AggregateMetadata>(metadataKey)` called with correct key
- `TryGetStateAsync<EventEnvelope>(eventKey)` called for each sequence number
- Correct handling of `ConditionalValue` (found vs not found)
- UnknownEventException thrown for unknown event types
- MissingEventException thrown for sequence gaps
- Performance: 1,000 events loaded in <100ms (NFR6)

**Minimum Tests (~20):**

EventStreamReader Unit Tests (10) -- in `EventStreamReaderTests.cs`:
1. `RehydrateAsync_NewAggregate_ReturnsNull`
2. `RehydrateAsync_ExistingAggregate_ReadsEventsFromSequence1`
3. `RehydrateAsync_ExistingAggregate_UsesCorrectKeyPattern`
4. `RehydrateAsync_ThousandEvents_CompletesWithin100ms` (NFR6)
5. `RehydrateAsync_UnknownEventType_ThrowsUnknownEventException`
6. `RehydrateAsync_UnknownEvent_ExceptionContainsSequenceAndType`
7. `RehydrateAsync_MissingEvent_ThrowsMissingEventException` (F7)
8. `RehydrateAsync_InvalidMetadata_NegativeSequence_ThrowsException` (F8)
9. `RehydrateAsync_NullIdentity_ThrowsArgumentNullException`
10. `RehydrateAsync_EventsLoadedInOrder_VerifySequence`

EventEnvelope Unit Tests (3) -- in `EventEnvelopeTests.cs`:
11. `EventEnvelope_JsonRoundtrip_PreservesAllFields`
12. `EventEnvelope_ByteArrayPayload_SerializesAsBase64`
13. `EventEnvelope_NullExtensions_RoundtripsCorrectly`

AggregateActor State Rehydration Tests (5) -- in `AggregateActorTests.cs`:
14. `ProcessCommandAsync_NewAggregate_RehydratesNullState`
15. `ProcessCommandAsync_ExistingAggregate_RehydratesState`
16. `ProcessCommandAsync_StateRehydrated_ProceedsToStep4`
17. `ProcessCommandAsync_UnknownEvent_PropagatesException`
18. `ProcessCommandAsync_StateRehydration_LogsStateType`

Integration Tests (2+) -- existing tests regression:
19. `PostCommands_AllExistingTests_StillPass` (regression check)
20. `PostCommands_ValidCommand_Returns202Accepted` (unchanged behavior)

**Current test count:** ~414 test methods from Story 3.3. Story 3.4 adds ~20 new tests, bringing estimated total to ~434.

### Previous Story Intelligence

**From Story 3.3 (Tenant Validation at Actor Level):**
- `TenantValidator` executes as Step 2 and validates BEFORE any state access (SEC-2 constraint)
- Actor orchestrator enforces strict step ordering: 1 (idempotency) -> 2 (tenant) -> 3 (state) -> 4 (domain) -> 5 (state machine)
- Exception handling pattern: catch specific exceptions (`TenantMismatchException`) before any broader catches
- Actor logs at Information level after successful validation steps
- Components created per-call using `host.LoggerFactory.CreateLogger<T>()` and `this.StateManager`
- `UserId` now flows from JWT `sub` claim (replacing "system" placeholder)

**From Story 3.2 (AggregateActor Orchestrator & Idempotency Check):**
- Actor transformed from STUB to 5-step orchestrator (Step 1 real, Steps 2-5 stubs initially)
- `IdempotencyChecker` created per-call: `new IdempotencyChecker(StateManager, logger)`
- `IActorStateManager` pattern: `SetStateAsync` buffers, `SaveStateAsync` commits atomically
- `ConditionalValue<T>` pattern for `TryGetStateAsync`: check `.HasValue` before accessing `.Value`
- Idempotency key is `causationId` (not `correlationId`) to allow replays (F8 design decision)
- Rejection results ARE cached via IdempotencyChecker.RecordAsync
- `StateManager.SaveStateAsync()` called once at end to atomically commit ALL state changes

**From Story 3.1 (Command Router & Actor Activation):**
- `CommandEnvelope` has `AggregateIdentity` property (computed from TenantId, Domain, AggregateId)
- `AggregateIdentity` in Contracts has key derivation methods: `ActorId`, `EventStreamKeyPrefix`, `MetadataKey`, `SnapshotKey`
- Actor constructor pattern: `AggregateActor(ActorHost host, ILogger<AggregateActor> logger) : Actor(host)`
- `Host.Id.GetId()` returns the actor ID string (e.g., `"tenant:domain:aggregateId"`)
- Actor registered via `options.Actors.RegisterActor<AggregateActor>()` in AddEventStoreServer()
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` on public method parameters (CA1062)

**From Epic 2 Stories:**
- MediatR pipeline: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> SubmitCommandHandler
- Exception handler chain: Validation -> Authorization -> ConcurrencyConflict -> Global
- All exceptions from actor propagate to handler chain (converted to ProblemDetails responses)
- Correlation ID flows through entire pipeline and appears in all logs (rule #9)

**Key Patterns (mandatory for all new code):**
- Primary constructors for DI: `public class Foo(IDep dep) : Base`
- Records for immutable data: `record Foo(string Bar, int Baz)`
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` on public methods (CA1062)
- Feature folder organization: `Server/Events/`, `Server/Actors/`, `Server/Commands/`
- `namespace Hexalith.EventStore.{Project}.{Feature};`
- NSubstitute for mocking in tests
- Shouldly for assertions in tests

### Git Intelligence

**Recent commit patterns (last 10 commits):**
```
5ece433 Story 2.5 code review: mark done with review fixes (#27)
6cf6587 Stories 2.6-2.9: Command Status, Replay, Concurrency & Rate Limiting (#26)
fb817ea Update Claude Code local settings with tool permissions (#25)
74725aa Stories 2.4 & 2.5: JWT Authentication & Endpoint Authorization (#24)
8aaf036 Merge pull request #23 from Hexalith/feature/story-2.3-and-story-planning-2.4-2.5
f0d0a81 Story 2.3: MediatR Pipeline & Logging Behavior + Story planning for 2.4, 2.5
489e959 Merge pull request #22 from Hexalith/feature/story-2.2-command-validation-rfc7807
d3f19fd Story 2.2: Command Validation & RFC 7807 Error Responses
abd5f73 Update Claude Code local settings with tool permissions
2dce6f8 Merge pull request #20 from Hexalith/feature/story-2.1-commandapi-and-2.2-story
```

**Patterns observed:**
- Stories implemented sequentially in dedicated feature branches
- PR titles follow `Story X.Y: Description (#PR)` format
- Multi-story PRs are acceptable (Stories 2.4 & 2.5, Stories 2.6-2.9 bundled)
- Clean merge commits from pull requests
- Code review adjustments in follow-up commits (e.g., "Story 2.5 code review: mark done with review fixes")
- NSubstitute used for all mocking across test projects
- Shouldly for all assertions
- Primary constructors throughout codebase

**Commit message format recommendation for Story 3.4:**
```
Story 3.4: Event Stream Reader & State Rehydration

Implements Step 3 of the actor orchestrator:
- EventStreamReader loads events from IActorStateManager
- Composite key pattern: {tenant}:{domain}:{aggId}:events:{seq}
- AggregateMetadata tracks CurrentSequence
- UnknownEventException enforces backward compatibility
- Performance: 1,000 events in <100ms (NFR6)
- SEC-2: State rehydration AFTER tenant validation

Stories 3-1, 3-2, 3-3 are prerequisites (actor routing, idempotency, tenant validation).
Stories 3-5+ will add domain service invocation and event persistence.

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

### Latency Design Note

**Story 3.4 adds state rehydration overhead to actor activation:**
- New aggregates: ~1-2ms (single metadata read, HasValue=false, return null)
- Existing aggregates with N events: ~N ms with sequential reads, ~10-20ms with parallel batching (F5)
- 1,000 events: Must complete in <100ms (NFR6) -- requires parallel reads via `Task.WhenAll`
- Actor activation remains within 5s DAPR sidecar timeout (rule #14)

**Optimization strategy (F5):**
- Sequential reads: 1,000 events × 1-2ms/event = 1,000-2,000ms (FAILS NFR6)
- Parallel batching: 1,000 events / 50 concurrent batches = 20 batches × 2ms = 40ms (PASSES NFR6)
- Implementation: `Enumerable.Range(1, currentSequence).Select(async seq => await stateManager.TryGetStateAsync<EventEnvelope>(...)).ToArray(); await Task.WhenAll(tasks);`

**Future optimization (Stories 3.9-3.10):**
- Snapshot at sequence 100 → load 1 snapshot + 0-99 tail events
- Worst case: 1 snapshot read + 99 event reads = 100 reads instead of 1,000
- Keeps reads ≤102 (enforcement rule #15)

### Project Structure Notes

**Alignment with Architecture:**
- New `Server/Events/` folder for event stream components (EventStreamReader, EventEnvelope, AggregateMetadata, exceptions)
- Follows feature folder organization per architecture guidelines
- Test files mirror source structure: `Server.Tests/Events/EventStreamReaderTests.cs`
- No new projects or packages added -- all changes within existing Server project

**Dependency Graph:**
```
Server/Actors/AggregateActor -> Server/Events/EventStreamReader
Server/Events/EventStreamReader -> Dapr.Actors.Runtime (IActorStateManager)
Server/Events/EventEnvelope -> (no external deps, just data record)
Server/Events/AggregateMetadata -> (no external deps, just data record)
Server/Events/UnknownEventException -> System.InvalidOperationException
Tests: Server.Tests/Events -> Server/Events (unit testing)
```

**Package Dependency Boundaries (unchanged from Story 3.1):**
```
Contracts (zero deps) <- Server (+ Dapr.Actors, Dapr.Client) <- CommandApi (+ Dapr.AspNetCore)
Testing -> Contracts + Server
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 3.4: Event Stream Reader & State Rehydration (Lines 627-643)]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1: Event Storage Strategy (Lines 357-363)]
- [Source: _bmad-output/planning-artifacts/architecture.md#Composite Key Patterns (Lines 489-492)]
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC-2: Tenant Validation Before State Rehydration (Lines 112, 536, 1055)]
- [Source: _bmad-output/planning-artifacts/architecture.md#CRITICAL-1: UnknownEvent Handling (Lines 1016-1022)]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule #6: IActorStateManager Only (Line 628)]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule #15: Mandatory Snapshots (Line 637)]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR6: 1,000 Events in <100ms (Lines 46, 994)]
- [Source: _bmad-output/planning-artifacts/architecture.md#Actor Processing Pipeline (Lines 534-543)]
- [Source: _bmad-output/planning-artifacts/architecture.md#EventStreamReader Component (Lines 703-704)]
- [Source: _bmad-output/planning-artifacts/prd.md#FR12 - Aggregate state reconstruction via event replay]
- [Source: _bmad-output/implementation-artifacts/3-3-tenant-validation-at-actor-level.md]
- [Source: _bmad-output/implementation-artifacts/3-2-aggregateactor-orchestrator-and-idempotency-check.md]
- [Source: _bmad-output/implementation-artifacts/3-1-command-router-and-actor-activation.md]
- [Source: https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-actors/dotnet-actors-howto/ - DAPR .NET Actors SDK]
- [Source: https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/ - DAPR Actors Overview]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

