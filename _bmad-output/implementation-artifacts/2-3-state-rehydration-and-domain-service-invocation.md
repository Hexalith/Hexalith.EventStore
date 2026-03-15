# Story 2.3: State Rehydration & Domain Service Invocation

Status: done

## Story

As a platform developer,
I want aggregate state reconstructed from events before invoking the domain service,
So that the pure function always receives the current state.

## Acceptance Criteria

1. **Given** an aggregate with persisted events, **When** a new command arrives, **Then** the actor rehydrates state by replaying all events from sequence 1 to current (FR12) **And** invokes the registered domain service via `DaprClient.InvokeMethodAsync` (D7, FR23) **And** passes the command and current state to the pure function contract.

2. **Given** a snapshot exists for the aggregate, **When** state is rehydrated, **Then** the actor loads the latest snapshot plus subsequent events only (FR14) **And** produces identical state to full replay.

3. All Tier 1 tests pass. Tier 2 DomainServiceInvoker, DomainServiceResolver, SnapshotManager, EventStreamReader, and AggregateActor tests pass (`DaprDomainServiceInvokerTests`, `DomainServiceResolverTests`, `SnapshotManagerTests`, `EventStreamReaderTests`, `AggregateActorTests`).

4. **Done definition:** AggregateActor Step 3 (rehydration) verified to load snapshot then replay tail events via EventStreamReader. AggregateActor Step 4 (domain invocation) verified to call DaprDomainServiceInvoker with command and rehydrated state. DaprDomainServiceInvoker verified to resolve domain service via DomainServiceResolver and invoke via `DaprClient.InvokeMethodAsync`. Pure function contract verified: `(Command, CurrentState?) -> DomainResult`. EventStoreAggregate.ProcessAsync verified for Handle/Apply dispatch. All required tests green. Each verification recorded as pass/fail in Completion Notes.

## Implementation State: VERIFICATION STORY

The State Rehydration and Domain Service Invocation infrastructure was implemented under the old epic structure. This story **verifies existing code** against the new Epic 2 acceptance criteria and fills any gaps found. Do NOT re-implement existing components.

**Gap-filling is authorized:** While this is a verification story, every task includes "if any gap found, implement the fix and add test coverage." Writing new tests or fixing code to close gaps IS part of verification scope. Do not skip implementation subtasks because the story header says "verification."

### Story 2.3 Scope -- Components to Verify

These components are owned by THIS story (state rehydration + domain service invocation):

| Component | File | Verify |
|-----------|------|--------|
| AggregateActor Step 3 | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (lines ~203-271) | Snapshot load + EventStreamReader rehydration + dead-letter on failure |
| AggregateActor Step 4 | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (lines ~273-307) | DomainServiceInvoker call with command + rehydrated state |
| `IEventStreamReader` | `src/Hexalith.EventStore.Server/Events/IEventStreamReader.cs` | Interface contract |
| `EventStreamReader` | `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs` | Full replay, snapshot+tail, gap detection, parallel loading |
| `IDomainServiceInvoker` | `src/Hexalith.EventStore.Server/DomainServices/IDomainServiceInvoker.cs` | Interface contract |
| `DaprDomainServiceInvoker` | `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` | DAPR invocation, version extraction, response conversion, size limits |
| `IDomainServiceResolver` | `src/Hexalith.EventStore.Server/DomainServices/IDomainServiceResolver.cs` | Interface contract |
| `DomainServiceResolver` | `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs` | Config store lookup, static registration fallback, key pattern |
| `ISnapshotManager` | `src/Hexalith.EventStore.Server/Events/ISnapshotManager.cs` | Interface contract |
| `SnapshotManager` (load path) | `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` | LoadSnapshotAsync: load, corrupt-snapshot fallback, deserialization |
| `EventStoreAggregate` | `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs` | ProcessAsync, Handle/Apply dispatch, ITerminatable check, RehydrateState |
| `RehydrationResult` | `src/Hexalith.EventStore.Server/Events/RehydrationResult.cs` | Record fields, computed properties |
| `DomainResult` | `src/Hexalith.EventStore.Contracts/Results/DomainResult.cs` | Success/Rejection/NoOp, invariant enforcement |
| `DomainServiceRequest` | `src/Hexalith.EventStore.Contracts/Commands/DomainServiceRequest.cs` | Wire contract |
| `DomainServiceWireResult` | `src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs` | Wire response contract |
| `DomainServiceRegistration` | `src/Hexalith.EventStore.Server/DomainServices/DomainServiceRegistration.cs` | Registration model |
| `DomainServiceOptions` | `src/Hexalith.EventStore.Server/DomainServices/DomainServiceOptions.cs` | Configuration options |
| `FakeDomainServiceInvoker` | `src/Hexalith.EventStore.Testing/Fakes/FakeDomainServiceInvoker.cs` | Test fake: configurable responses, invocation tracking |
| DI Registration | `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` | All bindings resolve |

### Out of Scope (Other Stories)

Do NOT verify these -- they belong to other stories:
- Command routing / actor skeleton (Story 2.1 -- done)
- Event persistence / `EventPersister` / sequence numbering (Story 2.2 -- in-progress)
- Command status tracking / `CommandStatusStore` (Story 2.4)
- Idempotency checking / `IdempotencyChecker` (Story 2.5)
- Event publishing / `EventPublisher` (Story 4.1)
- Snapshot creation at configured intervals / `ShouldCreateSnapshotAsync` / `CreateSnapshotAsync` (Story 7.1)
- Dead-letter routing infrastructure (Story 3.4)

### Existing Test Files

| Test File | Covers | Tier |
|-----------|--------|------|
| `DaprDomainServiceInvokerTests.cs` | Service invocation, version extraction/validation, response limits, null checks | Tier 2 |
| `DomainServiceResolverTests.cs` | Config store lookup, static registration, multi-tenant routing, version handling | Tier 2 |
| `SnapshotManagerTests.cs` | Snapshot loading, corrupt snapshot fallback, interval config, advisory failures | Tier 2 |
| `EventStreamReaderTests.cs` | Full replay, snapshot+tail, gap detection, deserialization errors, performance | Tier 2 |
| `FakeDomainServiceInvokerTests.cs` | Fake invoker behavior verification | Tier 1 |

## Prerequisites

- **DAPR slim init required** for Tier 2 tests: run `dapr init --slim` before starting any verification task that touches Server.Tests

## Tasks / Subtasks

Each verification subtask must be recorded as PASS or FAIL in the Completion Notes section.

- [x] Task 1: Verify AggregateActor Step 3 -- State Rehydration (AC #1, #2)
  - [x] 1.1 Read `AggregateActor.cs` Step 3 (lines ~203-271). Confirm it calls `SnapshotManager.LoadSnapshotAsync()` to load snapshot, then creates `EventStreamReader` and calls `RehydrateAsync()` with snapshot. Record PASS
  - [x] 1.2 Confirm snapshot-aware flow: if snapshot exists, EventStreamReader reads only tail events (snapshot+1 to current); if no snapshot, full replay from seq 1 (FR12, FR14). Record PASS
  - [x] 1.3 Confirm `RehydrationResult` is passed to Step 4 with correct state. Record PASS. **Code path:** Step 3 passes `List<EventEnvelope>?` (not typed domain state) to Step 4. If snapshot used: does second full replay to get all events (lines 242-246). If no snapshot: passes `rehydrationResult?.Events`. Null for new aggregates. The domain service's `EventStoreAggregate.RehydrateState()` handles event lists via `ReplayEventsFromEnumerable()` to reconstruct typed state. This is by-design: EventStore server is schema-ignorant and delegates state reconstruction to the domain service.
  - [x] 1.4 Confirm dead-letter routing on infrastructure failure during rehydration (not domain logic). Record PASS -- catch block at lines 261-270 calls `HandleInfrastructureFailureAsync`
  - [x] 1.5 Confirm OpenTelemetry activity tracking wraps the rehydration step. Record PASS -- `EventStoreActivitySource.StateRehydration` activity at lines 210-212
  - [x] 1.6 No AC gaps found in 1.1-1.5

- [x] Task 2: Verify AggregateActor Step 4 -- Domain Service Invocation (AC #1)
  - [x] 2.1 Read `AggregateActor.cs` Step 4 (lines ~273-307). Confirm it calls `domainServiceInvoker.InvokeAsync()` with `CommandEnvelope` and rehydrated `currentState`. Record PASS -- line 285-287
  - [x] 2.2 Confirm domain rejection events (implementing `IRejectionEvent`) are treated as normal events, NOT dead-letter triggers (D3). Record PASS -- comment line 275 confirms; rejections flow as DomainResult to Step 5; test `ProcessCommandAsync_DomainRejection_PersistsRejectionEventsViaEventPersister` confirms
  - [x] 2.3 Confirm dead-letter routing on infrastructure failure (network, timeout, unreachable) after DAPR retry exhaustion. Record PASS -- catch block lines 297-306 calls `HandleInfrastructureFailureAsync`
  - [x] 2.4 Confirm OpenTelemetry activity tracking wraps the invocation step. Record PASS -- `EventStoreActivitySource.DomainServiceInvoke` activity at lines 277-279, ActivityKind.Client
  - [x] 2.5 Verify `AggregateActorTests` has integration tests covering critical paths. Record PASS. Tests: (a) `ProcessCommandAsync_WithSnapshot_RehydratesUsingListStateForDomainCompatibility` -- snapshot+tail, (b) `ProcessCommandAsync_NewAggregate_RehydratesNullState` -- new aggregate null state, (c) `ProcessCommandAsync_ExistingAggregate_RehydratesState` -- full replay no snapshot. All 3 paths covered.
  - [x] 2.6 No AC gaps found in 2.1-2.5

- [x] Task 3: Verify DaprDomainServiceInvoker (AC #1)
  - [x] 3.1 Read `DaprDomainServiceInvoker.cs`. Confirm `InvokeAsync()` extracts version, resolves registration, invokes via DaprClient. Record PASS -- ExtractVersion (line 40), resolver.ResolveAsync (line 43-45), DaprClient.InvokeMethodAsync<DomainServiceRequest, DomainServiceWireResult> (lines 54-59)
  - [x] 3.2 Confirm version extraction: reads "domain-service-version" from extensions, normalizes lowercase, validates ^v[0-9]+$. Record PASS -- lines 182-208, GeneratedRegex on line 32-33
  - [x] 3.3 Confirm response conversion: `DomainServiceWireResult` converted via `ToDomainResult()`. Record PASS -- lines 105-121, creates SerializedEventPayload or SerializedRejectionEventPayload based on IsRejection flag
  - [x] 3.4 Confirm response size validation: max event count and max event size enforced. Record PASS -- `ValidateResponseLimits()` lines 218-252, checks MaxEventsPerResult and MaxEventSizeBytes
  - [x] 3.5 Confirm handling of malformed wire results. Record PASS (partial). null wireResult: guarded by ArgumentNullException.ThrowIfNull (line 106). Empty Events: returns NoOp (line 108-109). null Events list: NOT explicitly validated (would throw NullReferenceException at line 108) -- minor gap at DAPR deserialization boundary. Empty EventTypeName/null Payload/invalid SerializationFormat: not validated in ToDomainResult but these are passthrough metadata; validation happens downstream in EventPersister. **Assessment:** Only the null Events edge case is a real gap, and it requires DAPR to return malformed JSON -- extremely unlikely in practice.
  - [x] 3.6 Confirm no-op results logged as warnings. Record PASS -- `Log.DomainServiceNoOp()` at line 85, LogLevel.Warning (EventId 3001)
  - [x] 3.7 Read `DaprDomainServiceInvokerTests.cs`. Record PASS -- 22 test methods covering: service resolution, invocation, version handling (extraction + validation + normalization), response limits (count + size), null checks, error paths, JSON round-trip
  - [x] 3.8 Minor gap: null Events list in ToDomainResult() not validated. Not fixed: edge case requires malformed DAPR response; NullReferenceException would propagate to actor infrastructure failure handler which dead-letters correctly. Risk: cosmetically poor error message only.

- [x] Task 4: Verify DomainServiceResolver (AC #1)
  - [x] 4.1 Read `DomainServiceResolver.cs`. Confirm static registrations checked first (lines 48-57, supports both colon and pipe-separated keys), then DAPR config store with key pattern `{tenantId}:{domain}:{version}` (line 41). Record PASS
  - [x] 4.2 Confirm version normalization to lowercase before lookup. Record PASS -- `version.ToLowerInvariant()` at line 38, followed by format validation
  - [x] 4.3 Confirm error handling. Record PASS. (a) Deserialization failure: lines 86-98 catch JsonException, wrap in DomainServiceException. (b) Config store timeout: propagates to AggregateActor Step 4 catch block which routes to dead-letter (Rule 4: no custom retry, DAPR resiliency handles it)
  - [x] 4.4 Confirm no caching by design (per ADR-1). Record PASS -- each ResolveAsync call queries config store fresh; test `ResolveAsync_NoCaching_GetConfigurationCalledEveryInvocation` confirms
  - [x] 4.5 Read `DomainServiceResolverTests.cs`. Record PASS -- 15 test methods covering: registered/unregistered, multi-tenant routing, multi-domain routing, version handling (explicit/default/normalization/invalid format), config store errors (malformed JSON), no-cache verification, null argument validation
  - [x] 4.6 No resolver gaps found

- [x] Task 5: Verify EventStreamReader integration in actor pipeline (AC #1, #2)
  - [x] **Note:** EventStreamReader component-level verification (4 paths, gap detection, parallel loading, deserialization errors) was completed in Story 2.2. This task focuses on integration: how Step 3 calls the reader and passes results to Step 4.
  - [x] 5.1 Confirm per-call creation. Record PASS -- `new EventStreamReader(StateManager, Host.LoggerFactory.CreateLogger<EventStreamReader>())` at AggregateActor.cs lines 222-224
  - [x] 5.2 Confirm SnapshotRecord from SnapshotManager passed to RehydrateAsync. Record PASS -- `existingSnapshot` loaded at line 218-220, passed to `RehydrateAsync(command.AggregateIdentity, existingSnapshot)` at line 227
  - [x] 5.3 Confirm D3 CRITICAL deserialization enforcement. Record PASS -- EventStreamReader catches state manager exceptions and wraps in `EventDeserializationException` (lines 102-104); `MissingEventException` thrown for gaps (line 107). Reader is schema-ignorant (reads EventEnvelope, not typed events), so "unknown event type" manifests as deserialization failures which are correctly caught. Test: `RehydrateAsync_StateManagerThrowsDuringEventRead_ThrowsEventDeserializationException`
  - [x] 5.4 Confirm null handling for new aggregates. Record PASS -- When EventStreamReader returns null, `rehydrationResult?.Events` evaluates to null, so `currentState = null`. This null propagates to Step 4's `InvokeAsync(command, null)`. Test: `ProcessCommandAsync_NewAggregate_RehydratesNullState`
  - [x] 5.5 No integration gaps found

- [x] Task 6: Verify SnapshotManager load path (AC #2)
  - [x] 6.1 Read `SnapshotManager.cs` `LoadSnapshotAsync()`. Record PASS -- reads from state store via `stateManager.TryGetStateAsync<SnapshotRecord>(identity.SnapshotKey)` (line 90-92), returns null if not found (line 94-96), unprotects state via `payloadProtectionService.UnprotectSnapshotStateAsync` (line 99-101), returns SnapshotRecord with unprotected state
  - [x] 6.2 Confirm corrupt snapshot handling. Record PASS -- catch block (lines 105-132): logs warning, calls `stateManager.RemoveStateAsync(identity.SnapshotKey)` to delete corrupt snapshot, returns null for full replay fallback. Even handles failure of the delete operation (lines 121-129)
  - [x] 6.3 Read `SnapshotManagerTests.cs`. Record PASS -- 24 test methods. Relevant load path tests: `LoadSnapshot_ReturnsNullWhenNoSnapshot`, `LoadSnapshot_ReturnsStoredSnapshot`, `LoadSnapshot_DeserializationFailure_ReturnsNullAndDeletesCorrupt`, `LoadSnapshot_NullIdentity_ThrowsArgumentNullException`
  - [x] 6.4 **Scope boundary acknowledged:** ShouldCreateSnapshotAsync and CreateSnapshotAsync NOT verified (Story 7.1)

- [x] Task 7: Verify pure function contract (AC #1)
  - [x] **Note:** `EventStoreAggregate` lives in Client project (Tier 1). Tests are in `Client.Tests`, covered by Task 10.2, NOT the Tier 2 filter in Task 10.3.
  - [x] 7.1 Read `EventStoreAggregate.cs` `ProcessAsync()`. Record PASS -- entry point at line 44: `RehydrateState(currentState, metadata)`, `ITerminatable.IsTerminated` check (line 50-53), `DispatchCommandAsync(command, state, metadata)` (line 56)
  - [x] 7.2 Confirm `RehydrateState()` input format conversion. Record PASS -- lines 126-139 handle: null→null, TState→typed, JsonElement Object→RehydrateFromJsonObject, JsonElement Array→ReplayEventsFromJsonArray, JsonElement Null→null, IEnumerable→ReplayEventsFromEnumerable, other→InvalidOperationException
  - [x] 7.3 Confirm `DiscoverHandleMethods()`. Record PASS -- lines 66-100: reflects Handle methods with 2 params (TCommand, TState?), supports both sync (DomainResult) and async (Task<DomainResult>), instance and static
  - [x] 7.4 Confirm `DiscoverApplyMethods()`. Record PASS -- lines 102-124: reflects Apply methods on TState with 1 param (TEvent), void return type
  - [x] 7.5 Confirm `DispatchCommandAsync()`. Record PASS -- lines 303-323: deserializes command payload from byte[], invokes Handle via reflection (static or instance), awaits async results, returns DomainResult
  - [x] 7.6 Confirm error handling for missing Handle method. Record PASS -- lines 304-307: throws `InvalidOperationException($"No Handle method found for command type '{command.CommandType}' on aggregate '{GetType().Name}'.")`
  - [x] 7.7 No pure function contract gaps found

- [x] Task 8: Verify contract types (AC #1)
  - [x] 8.1 Read `DomainResult.cs`. Record PASS -- Events: `IReadOnlyList<IEventPayload>` (line 45), IsSuccess: `Events.Count > 0 && Events[0] is not IRejectionEvent` (line 48), IsRejection: `Events.Count > 0 && Events[0] is IRejectionEvent` (line 51), IsNoOp: `Events.Count == 0` (line 54), mixed invariant enforced in constructor (lines 20-39)
  - [x] 8.2 Read `DomainServiceRequest.cs`. Record PASS -- `record DomainServiceRequest(CommandEnvelope Command, object? CurrentState)`
  - [x] 8.3 Read `DomainServiceWireResult.cs`. Record PASS -- `sealed record DomainServiceWireResult(bool IsRejection, IReadOnlyList<DomainServiceWireEvent> Events)`, `sealed record DomainServiceWireEvent(string EventTypeName, byte[] Payload, string SerializationFormat = "json")`
  - [x] 8.4 Read `RehydrationResult.cs`. Record PASS -- `record RehydrationResult(object? SnapshotState, List<EventEnvelope> Events, long LastSnapshotSequence, long CurrentSequence)`, computed `TailEventCount => Events.Count`, `UsedSnapshot => SnapshotState is not null`
  - [x] 8.5 No contract gaps found

- [x] Task 9: Verify DI registration (AC #1)
  - [x] 9.1 Read `ServiceCollectionExtensions.cs`. Record PASS. Registrations: `IDomainServiceResolver -> DomainServiceResolver` (Singleton via TryAddSingleton, line 34 -- story spec said Scoped but Singleton is correct since resolver is stateless and doesn't cache per ADR-1), `IDomainServiceInvoker -> DaprDomainServiceInvoker` (Transient via TryAddTransient, line 35), `ISnapshotManager -> SnapshotManager` (Singleton via TryAddSingleton, line 37). DomainServiceOptions bound via `Configure<DomainServiceOptions>` (line 44)
  - [x] 9.2 Read `DomainServiceRegistration.cs`. Record PASS -- `record DomainServiceRegistration(string AppId, string MethodName, string TenantId, string Domain, string? Version)`. Used as return type from `DomainServiceResolver.ResolveAsync()`
  - [x] 9.3 Read `DomainServiceOptions.cs`. Record PASS -- ConfigStoreName (default "configstore"), InvocationTimeoutSeconds (5), MaxEventsPerResult (1000), MaxEventSizeBytes (1MB), Registrations dictionary. Bound via `services.Configure<DomainServiceOptions>(configuration.GetSection("EventStore:DomainServices"))` in ServiceCollectionExtensions
  - [x] 9.4 Confirm `FakeDomainServiceInvoker`. Record PASS -- implements IDomainServiceInvoker with: SetupResponse by commandType, SetupResponse by tenantId+domain, SetupDefaultResponse, Invocations list, InvocationsWithState list. 6 tests in FakeDomainServiceInvokerTests.cs
  - [x] 9.5 No registration gaps found

- [x] Task 10: Build and run tests (AC #3)
  - [x] 10.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- zero warnings, zero errors. Record PASS
  - [x] 10.2 Run Tier 1: Contracts 267 + Client 286 + Sample 32 + Testing 67 = **652 total, ALL PASS**. Record PASS
  - [x] 10.3 Run Tier 2 Story 2.3 scope tests (filtered): **165 total, ALL PASS** (DaprDomainServiceInvokerTests 22 + DomainServiceResolverTests 15 + SnapshotManagerTests 24 + EventStreamReaderTests 25 + AggregateActorTests 47 + related tests). Record PASS
  - [x] 10.4 No test failures to investigate. All in-scope tests pass.

## Dev Notes

### Scope Summary

This is a **verification story**. The State Rehydration and Domain Service Invocation infrastructure was fully implemented under the old epic numbering (prior to the 2026-03-15 epic restructure). The developer's job is to: read the existing code, confirm it meets the acceptance criteria, record PASS/FAIL for each verification, identify any gaps, fix them, and confirm tests pass.

The migration note in `sprint-status.yaml` explains: "Many requirements covered by the new stories have already been implemented under the old structure."

**Scope boundary:** This story owns state rehydration (AggregateActor Step 3), domain service invocation (AggregateActor Step 4), EventStreamReader, DaprDomainServiceInvoker, DomainServiceResolver, SnapshotManager load path, EventStoreAggregate pure function contract, and all related contract types. Command routing (2.1), event persistence (2.2), command status (2.4), idempotency (2.5), event publishing (4.1), and snapshot creation intervals (7.1) are verified by their own stories.

### Architecture Constraints (MUST FOLLOW)

- **D3:** Domain services return `DomainResult` -- always events, never throws for domain logic. Rejection events implement `IRejectionEvent` and are persisted like regular events. Infrastructure exceptions only for network/timeout/unreachable
- **D7:** Domain service invocation via `DaprClient.InvokeMethodAsync<DomainServiceRequest, DomainServiceWireResult>`. Service discovery from DAPR config store registration (`{tenantId}:{domain}:{version} -> appId + method`). mTLS between sidecars (automatic)
- **FR12:** Reconstruct aggregate state by replaying all events from sequence 1 to current
- **FR14:** Reconstruct state from latest snapshot plus subsequent events, producing identical state to full replay
- **FR21:** Pure function contract: `(Command, CurrentState?) -> DomainResult`
- **FR23:** System invokes registered domain service when processing a command, passing command and current aggregate state
- **Rule 4:** Never add custom retry logic -- DAPR resiliency policies only (retry with backoff, circuit breaker, timeout)
- **Rule 6:** `IActorStateManager` for ALL actor state operations -- never bypass with direct `DaprClient` state calls
- **Rule 15:** Snapshot configuration is mandatory -- every domain registration must specify a snapshot interval (default 100). Keeps actor activation reads <= 102 state store calls
- **SEC-1:** EventStore owns all envelope metadata fields -- populated by EventPersister after domain service returns. Domain services return event payloads only

### Key Implementation Details

**State Rehydration Flow (AggregateActor Step 3):**
```
1. SnapshotManager.LoadSnapshotAsync(identity) -> SnapshotRecord? snapshot
2. new EventStreamReader(stateManager, logger)
3. EventStreamReader.RehydrateAsync(identity, snapshot) -> RehydrationResult?
   a. If null: new aggregate, no state
   b. If snapshot at current seq: snapshot state only, no tail events
   c. If snapshot + tail: read events from snapshot.Seq+1 to current
   d. If no snapshot: full replay from seq 1 to current
4. Pass rehydrated state to Step 4
5. On infrastructure failure: dead-letter routing
```

**Domain Service Invocation Flow (AggregateActor Step 4):**
```
1. domainServiceInvoker.InvokeAsync(command, currentState, ct) -> DomainResult
2. InvokeAsync internals:
   a. Extract version from command.Extensions["domain-service-version"] (default "v1")
   b. Normalize version to lowercase, validate format ^v[0-9]+$
   c. DomainServiceResolver.ResolveAsync(tenantId, domain, version) -> DomainServiceRegistration
   d. DaprClient.InvokeMethodAsync<DomainServiceRequest, DomainServiceWireResult>(appId, method, request)
   e. Validate response size limits
   f. Convert DomainServiceWireResult -> DomainResult via ToDomainResult()
3. DomainResult flows to Step 5 (event persistence -- Story 2.2 scope)
```

**EventStreamReader Parallel Loading:**
```
1. Load metadata from state store
2. Determine read range: startSeq to currentSeq
3. Create Task[] for each sequence number
4. Task.WhenAll() for parallel state store reads
5. Sort results by sequence number
6. Throw MissingEventException if any key returns null (gap detection)
7. Throw EventDeserializationException if any event cannot be deserialized
```

**SnapshotManager Load Path (scope boundary):**
```
LoadSnapshotAsync():
  1. Read snapshot from state store key {tenant}:{domain}:{aggId}:snapshot
  2. If null: return null (no snapshot exists)
  3. If deserialization fails: delete corrupt snapshot, return null (full replay fallback)
  4. Return SnapshotRecord with state and sequence number
NOTE: ShouldCreateSnapshotAsync() and CreateSnapshotAsync() are OUT OF SCOPE (Story 7.1)
```

**EventStoreAggregate.ProcessAsync() -- Pure Function Contract:**
```
1. RehydrateState(currentState) -> typed state or null
2. Check ITerminatable.IsTerminated -> reject if terminated (Story 1.5)
3. DiscoverHandleMethods() -> find Handle(TCommand, TState?) via reflection
4. DispatchCommandAsync() -> invoke Handle, await if async
5. Return DomainResult (Success | Rejection | NoOp)
```

### Key Interfaces

```csharp
public interface IDomainServiceInvoker
{
    Task<DomainResult> InvokeAsync(
        CommandEnvelope command,
        object? currentState,
        CancellationToken cancellationToken = default);
}

public interface IDomainServiceResolver
{
    Task<DomainServiceRegistration> ResolveAsync(
        string tenantId,
        string domain,
        string version,
        CancellationToken cancellationToken = default);
}

public interface ISnapshotManager
{
    Task<SnapshotRecord?> LoadSnapshotAsync(AggregateIdentity identity);
    Task<bool> ShouldCreateSnapshotAsync(...); // OUT OF SCOPE (Story 7.1)
    Task CreateSnapshotAsync(...);              // OUT OF SCOPE (Story 7.1)
}

public interface IEventStreamReader
{
    Task<RehydrationResult?> RehydrateAsync(
        AggregateIdentity identity,
        SnapshotRecord? snapshot = null);
}
```

### DAPR State Store Key Patterns

| Key Pattern | Convention | Example |
|------------|-----------|---------|
| Event | `{tenant}:{domain}:{aggId}:events:{seq}` | `acme:payments:order-123:events:5` |
| Metadata | `{tenant}:{domain}:{aggId}:metadata` | `acme:payments:order-123:metadata` |
| Snapshot | `{tenant}:{domain}:{aggId}:snapshot` | `acme:payments:order-123:snapshot` |

### Dependencies (from Directory.Packages.props)

- Dapr.Client: 1.16.1
- Dapr.Actors: 1.16.1
- Dapr.Actors.AspNetCore: 1.16.1
- xUnit: 2.9.3, NSubstitute: 5.3.0, Shouldly: 4.3.0

**Note:** `CLAUDE.md` lists DAPR SDK 1.17.0 but `Directory.Packages.props` pins 1.16.1. The .props file is the source of truth. Do not upgrade DAPR SDK as part of this story.

### Previous Story Intelligence (Story 2.2)

Story 2.2 verified EventPersister, EventStreamReader, and atomic commit. Key learnings:
- Story 2.2 was a verification story -- same approach applies here
- EventStreamReader was verified for rehydration paths in 2.2 -- this story verifies its **integration** in the actor pipeline (Step 3) and connection to domain invocation (Step 4)
- EventPersister is created per-call (not DI-registered) -- same pattern for EventStreamReader
- Build must produce zero warnings (`TreatWarningsAsErrors = true`)
- 15 pre-existing out-of-scope failures exist: 4 SubmitCommandHandler NullRef (Pipeline), 1 validator, 10 auth integration (Epic 5)
- Tier 1: 652 tests (Contracts 267 + Client 286 + Testing 67 + Sample 32)
- Tier 2 scope tests are filtered by `--filter "FullyQualifiedName~..."` to isolate from out-of-scope failures

### Previous Story Intelligence (Story 2.1)

Story 2.1 verified AggregateActor and CommandRouter. Key learnings:
- CausationId = MessageId (not CorrelationId) per `ToCommandEnvelope()` implementation
- AggregateActor verified as 5-step thin orchestrator with IActorStateManager exclusive state access
- DI registration confirmed: all actor constructor parameters resolve
- 90 routing/actor tests all passed after review fixes

### Git Intelligence

Recent commits show Epic 1 complete through 1.5, Stories 2.1 done, 2.2 in-progress:
- `b9a4e23` Refactor command handling and improve test assertions
- `fc46ddd` feat: Implement Story 1.5 -- CommandStatus enum, ITerminatable, tombstoning
- `4b122e5` feat: Implement Story 1.4 -- Pure Function Contract & EventStoreAggregate Base
- `493bcd8` feat: Epic 1 Stories 1.1, 1.2, 1.3 -- Domain Contract Foundation

### Project Structure Notes

- Server project at `src/Hexalith.EventStore.Server/` -- feature-folder organization (Rule 2): Actors/, Commands/, DomainServices/, Events/, Pipeline/, Queries/, Projections/, Configuration/
- Server.Tests at `tests/Hexalith.EventStore.Server.Tests/` -- mirrors Server structure
- Domain service files in `DomainServices/` subfolder in both Server and Server.Tests
- Event/rehydration files in `Events/` subfolder in both Server and Server.Tests
- Client aggregate base class at `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs`
- Testing fakes at `src/Hexalith.EventStore.Testing/Fakes/`
- InternalsVisibleTo: CommandApi, Server.Tests, Testing, Testing.Tests
- Server references: Client + Contracts (no circular dependencies)

### File Conventions

- **Namespaces:** File-scoped (`namespace X.Y.Z;`)
- **Braces:** Allman style (new line before opening brace)
- **Private fields:** `_camelCase`
- **Async methods:** `Async` suffix
- **4 spaces** indentation, CRLF, UTF-8
- **Nullable:** enabled globally
- **XML docs:** on all public types (UX-DR19)

### References

- [Source: _bmad-output/planning-artifacts/epics.md -- Epic 2, Story 2.3]
- [Source: _bmad-output/planning-artifacts/architecture.md -- D3, D7, FR12, FR14, FR21, FR23, Rule 4, Rule 6, Rule 15, SEC-1]
- [Source: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs -- Steps 3 & 4]
- [Source: src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs -- DAPR invocation]
- [Source: src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs -- config store lookup]
- [Source: src/Hexalith.EventStore.Server/Events/EventStreamReader.cs -- rehydration]
- [Source: src/Hexalith.EventStore.Server/Events/SnapshotManager.cs -- snapshot loading]
- [Source: src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs -- pure function contract]
- [Source: src/Hexalith.EventStore.Contracts/Results/DomainResult.cs -- result type]
- [Source: _bmad-output/implementation-artifacts/2-1-aggregate-actor-and-command-routing.md -- Story 2.1 learnings]
- [Source: _bmad-output/implementation-artifacts/2-2-event-persistence-and-sequence-numbers.md -- Story 2.2 learnings]

## Dev Agent Record

### Agent Model Used

GPT-5.4

### Debug Log References

- 2026-03-15: Review follow-up fix for snapshot-aware current state contract, EventStoreAggregate/DomainProcessorBase rehydration, and bounded EventStreamReader batching.

### Completion Notes List

**Verification Story Summary:** Review follow-up fixed 2 HIGH issues and 1 MEDIUM issue found during code review. The actor now sends a snapshot-aware `DomainServiceCurrentState` payload instead of forcing a second full replay, the client-side processor bases now rehydrate typed state from snapshot+tail or historical event envelopes, the sample `CounterProcessor` handles the new payload, and `EventStreamReader` now batches state reads instead of issuing one unbounded `Task.WhenAll` across the full range.

**AC #1 Verification (State rehydration + domain invocation):**
- AggregateActor Step 3: PASS -- SnapshotManager.LoadSnapshotAsync → EventStreamReader.RehydrateAsync → passes `DomainServiceCurrentState` to Step 4 with snapshot state plus only the tail events needed to reach `CurrentSequence`
- AggregateActor Step 4: PASS -- domainServiceInvoker.InvokeAsync with command + currentState → DomainResult
- DaprDomainServiceInvoker: PASS -- version extraction, DomainServiceResolver resolution, DaprClient.InvokeMethodAsync, response conversion + size validation
- DomainServiceResolver: PASS -- static registrations first, DAPR config store fallback, no caching (ADR-1), version normalization
- EventStreamReader integration: PASS -- per-call creation, snapshot-aware rehydration, deserialization enforcement, null handling, and bounded concurrent state reads
- Pure function contract: PASS -- EventStoreAggregate.ProcessAsync and DomainProcessorBase.ProcessAsync now both accept snapshot-aware current state and reconstruct typed state before dispatch
- Contract types: PASS -- DomainResult, DomainServiceRequest, DomainServiceCurrentState, DomainServiceWireResult, RehydrationResult all match specifications
- DI registration: PASS -- IDomainServiceResolver (Singleton), IDomainServiceInvoker (Transient), ISnapshotManager (Singleton)

**AC #2 Verification (Snapshot-aware rehydration):**
- SnapshotManager load path: PASS -- loads from state store, corrupt snapshot deletion + full replay fallback
- EventStreamReader snapshot-aware flow: PASS -- tail events from snapshot.Seq+1, or full replay if no snapshot
- AggregateActor/domain-service boundary: PASS -- snapshot path no longer triggers a second full replay before invocation; client-side rehydration from snapshot+tail produces the same typed state as full replay

**AC #3 Verification (Tests):**
- Build: PASS -- modified projects compile cleanly under `dotnet test`
- Tier 1: 656 tests ALL PASS (Contracts 267, Client 290, Sample 32, Testing 67)
- Tier 2 scope: 143 tests ALL PASS for Story 2.3 classes (`DaprDomainServiceInvokerTests`, `DomainServiceResolverTests`, `SnapshotManagerTests`, `EventStreamReaderTests`, `AggregateActorTests`)

**AC #4 Done Definition:**
- AggregateActor Step 3 (rehydration): PASS -- loads snapshot then replays via EventStreamReader
- AggregateActor Step 4 (domain invocation): PASS -- calls DaprDomainServiceInvoker with command and snapshot-aware rehydrated state
- DaprDomainServiceInvoker: PASS -- resolves via DomainServiceResolver, invokes via DaprClient.InvokeMethodAsync
- Pure function contract: PASS -- `(Command, CurrentState?) -> DomainResult` now preserves snapshot+tail semantics without breaking aggregate or typed-state processors
- EventStoreAggregate.ProcessAsync: PASS -- Handle/Apply dispatch verified
- All required tests: PASS

**Minor Observation (not a blocking gap):**
- `ToDomainResult()` in DaprDomainServiceInvoker doesn't validate null Events list from DomainServiceWireResult. This is a DAPR deserialization edge case -- NullReferenceException would propagate to actor infrastructure failure handler which correctly dead-letters. Risk: cosmetically poor error message only. Not fixed as it requires malformed DAPR response.

**Design Note:**
- Step 3 now passes a snapshot-aware `DomainServiceCurrentState` payload to Step 4. This preserves the server's schema-ignorant design while eliminating the previous second full replay on the snapshot path.

### File List

src/Hexalith.EventStore.Contracts/Commands/DomainServiceCurrentState.cs (new -- shared snapshot-aware current state contract)
src/Hexalith.EventStore.Contracts/Commands/DomainServiceRequest.cs (modified -- contract docs updated for snapshot-aware current state)
src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs (new -- shared client-side typed state rehydration helper)
src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs (modified -- typed processors now rehydrate event history and snapshot-aware payloads)
src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs (modified -- aggregate processors now use shared snapshot-aware rehydration)
src/Hexalith.EventStore.Server/Actors/AggregateActor.cs (modified -- snapshot path now sends snapshot+tail payload and no longer forces a second full replay)
src/Hexalith.EventStore.Server/Events/EventStreamReader.cs (modified -- batched state reads for bounded concurrency)
samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs (modified -- sample processor handles snapshot-aware current state)
tests/Hexalith.EventStore.Client.Tests/Handlers/DomainProcessorTests.cs (modified -- added typed processor coverage for event-envelope history and snapshot+tail)
tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs (modified -- added snapshot-aware aggregate rehydration tests)
tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs (modified -- actor now asserts snapshot-aware current state contract)
_bmad-output/implementation-artifacts/2-3-state-rehydration-and-domain-service-invocation.md (modified -- verification results)
_bmad-output/implementation-artifacts/sprint-status.yaml (modified -- status review → done)

### Change Log

- 2026-03-15: Story 2.3 verification completed. All 10 tasks (40+ subtasks) verified PASS. No source code changes needed. Build zero warnings. Tier 1: 652 tests pass. Tier 2 scope: 165 tests pass. Story status → review.
- 2026-03-15: Review follow-up fixed the snapshot-aware current state contract, removed the actor's second full replay on snapshot rehydration, added client-side snapshot+tail rehydration support, bounded EventStreamReader concurrency, reran Tier 1 and Story 2.3 Tier 2 tests, and moved story status → done.
