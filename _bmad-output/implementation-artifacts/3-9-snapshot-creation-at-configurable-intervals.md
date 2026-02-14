# Story 3.9: Snapshot Creation at Configurable Intervals

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**BLOCKING: Stories 3.4 (EventStreamReader) and 3.7 (EventPersister) MUST be implemented before this story.**

Verify these files/classes exist before starting:
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs` (Story 3.4 -- state rehydration)
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs` (Story 3.7 -- event persistence with atomic writes)
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (Story 3.2 -- 5-step orchestrator)
- `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` (Story 1.2 -- includes `SnapshotKey` property)
- `src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs` (Story 1.2 -- 11-field envelope)
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryStateManager.cs` (Story 1.4 -- test fake for IActorStateManager)

Run `dotnet test` to confirm all existing tests pass before beginning.

## Story

As a **system operator**,
I want snapshots of aggregate state created at configurable intervals (default every 100 events),
So that state rehydration remains fast regardless of total event count (FR13, NFR19).

## Acceptance Criteria

1. **Snapshot creation at interval** - Given an aggregate has accumulated N events since the last snapshot (or since creation), When N reaches the configured snapshot interval (default 100 per enforcement rule #15), Then SnapshotManager captures the current aggregate state as a snapshot.

2. **Snapshot storage via IActorStateManager** - Given a snapshot is created, When SnapshotManager persists it, Then the snapshot is stored via IActorStateManager with key `{tenant}:{domain}:{aggId}:snapshot` (using `AggregateIdentity.SnapshotKey`), And the storage uses actor-scoped state operations only (enforcement rule #6).

3. **Snapshot includes sequence number** - Given a snapshot is created at sequence N, When the snapshot is stored, Then it includes the sequence number it was taken at, And any subsequent state reconstruction can use this sequence number to know which events to replay from.

4. **Mandatory snapshot configuration** - Given a domain service is registered, When its configuration is loaded, Then snapshot configuration is mandatory (enforcement rule #15) -- no aggregate can opt out, And a default interval of 100 events is used if not explicitly configured.

5. **Configurable per domain** - Given multiple domains are registered, When snapshot intervals are configured, Then the snapshot interval is configurable per domain via DAPR config store, And different domains can have different snapshot intervals based on their aggregate state size and event frequency.

6. **Snapshot triggers after event persistence** - Given a command produces events that are persisted (state machine at EventsStored stage), When the new sequence number crosses a snapshot interval boundary, Then SnapshotManager creates a snapshot of the current state, And snapshot creation failure does NOT fail the command processing (advisory, similar to status writes per rule #12).

7. **Snapshot overwrites previous** - Given a snapshot already exists for an aggregate, When a new snapshot is created at a higher sequence number, Then the new snapshot replaces the previous one (single snapshot per aggregate, not accumulating), And the key `{tenant}:{domain}:{aggId}:snapshot` always contains the latest snapshot.

8. **Snapshot content correctness** - Given an aggregate has processed events up to sequence N, When a snapshot is created, Then the snapshot state is identical to what full event replay from sequence 1 to N would produce, And the snapshot can be used as a starting point for reconstruction (verified by Story 3.10).

9. **No snapshot for empty aggregates** - Given a new aggregate with no events, When the first command is processed, Then no snapshot is created until the event count reaches the configured interval, And the system handles the no-snapshot case gracefully (full replay from sequence 1).

10. **Snapshot creation is atomic with event persistence** - Given events are being persisted via IActorStateManager's batch save, When a snapshot is due, Then the snapshot write is included in the same `SaveStateAsync` batch as the events, And atomicity is guaranteed by the DAPR actor state manager's turn-based model. Note: `SaveStateAsync` is an all-or-nothing DAPR runtime guarantee -- either all staged writes (events + snapshot + metadata) commit together, or none do.

11. **Snapshot deserialization resilience** - Given a snapshot exists but cannot be deserialized (domain state schema changed, data corruption), When `LoadSnapshotAsync` attempts to read it, Then the system falls back to full event replay from sequence 1 (events are the source of truth), And the corrupt/incompatible snapshot is deleted from the state store via `stateManager.RemoveStateAsync`, And a warning is logged with correlationId, tenantId, domain, aggregateId, And subsequent command processing succeeds via full replay.

12. **Minimum interval enforcement** - Given SnapshotOptions is configured, When the interval value (DefaultInterval or any DomainIntervals entry) is less than 10, Then configuration validation rejects it with a descriptive error message, And the system refuses to start with an invalid snapshot interval, And this prevents accidental performance degradation from overly frequent snapshots.

## Tasks / Subtasks

- [ ] Task 0: Verify prerequisites and understand current state (BLOCKING)
  - [ ] 0.1 Run all existing tests -- they must pass before proceeding
  - [ ] 0.2 Confirm `AggregateIdentity.SnapshotKey` property exists and returns `{TenantId}:{Domain}:{AggregateId}:snapshot`
  - [ ] 0.3 Review `EventPersister` (Story 3.7) to understand event write flow and where snapshot creation integrates
  - [ ] 0.4 Review `EventStreamReader` (Story 3.4) to understand how it will consume snapshots (Story 3.10)
  - [ ] 0.5 Review `InMemoryStateManager` to understand actor state batch semantics

- [ ] Task 1: Create SnapshotOptions configuration (AC: #4, #5)
  - [ ] 1.1 Create `SnapshotOptions.cs` in `src/Hexalith.EventStore.Server/Configuration/`
  - [ ] 1.2 Properties: `DefaultInterval` (int, default 100), `DomainIntervals` (Dictionary<string, int> for per-domain overrides)
  - [ ] 1.3 Validation: interval must be >= 10 (mandatory per rule #15; minimum 10 prevents accidental performance degradation from overly frequent snapshots)
  - [ ] 1.4 Register via `AddEventStoreServer()` DI extension (rule #10)
  - [ ] 1.5 Support loading from DAPR config store for dynamic updates (NFR20)

- [ ] Task 2: Create SnapshotRecord model (AC: #3, #8)
  - [ ] 2.1 Create `SnapshotRecord.cs` in `src/Hexalith.EventStore.Server/Events/`
  - [ ] 2.2 Properties: `SequenceNumber` (long), `State` (object -- serialized aggregate state), `CreatedAt` (DateTimeOffset), `Domain` (string), `AggregateId` (string), `TenantId` (string)
  - [ ] 2.3 Use a record type for immutability

- [ ] Task 3: Create ISnapshotManager interface (AC: #1, #2, #6)
  - [ ] 3.1 Create `ISnapshotManager.cs` in `src/Hexalith.EventStore.Server/Events/`
  - [ ] 3.2 Methods:
    - `Task<bool> ShouldCreateSnapshotAsync(string domain, long currentSequence, long lastSnapshotSequence)` -- determines if snapshot is due
    - `Task CreateSnapshotAsync(AggregateIdentity identity, long sequenceNumber, object state, IActorStateManager stateManager)` -- creates snapshot via actor state manager
    - `Task<SnapshotRecord?> LoadSnapshotAsync(AggregateIdentity identity, IActorStateManager stateManager)` -- reads existing snapshot (used by Story 3.10, but interface defined now)
  - [ ] 3.3 Namespace: `Hexalith.EventStore.Server.Events`

- [ ] Task 4: Implement SnapshotManager (AC: #1, #2, #3, #6, #7, #10)
  - [ ] 4.1 Create `SnapshotManager.cs` in `src/Hexalith.EventStore.Server/Events/`
  - [ ] 4.2 Constructor: `SnapshotManager(IOptions<SnapshotOptions> options, ILogger<SnapshotManager> logger)`
  - [ ] 4.3 `ShouldCreateSnapshotAsync`: Check if `(currentSequence - lastSnapshotSequence) >= interval` for the domain
  - [ ] 4.4 `CreateSnapshotAsync`: Wrap in try/catch. Use `stateManager.SetStateAsync(identity.SnapshotKey, snapshotRecord)` -- this stages the write in the actor's pending state batch. On serialization or any failure: log warning, skip snapshot, do NOT fail command processing (advisory per rule #12)
  - [ ] 4.5 `LoadSnapshotAsync`: Use `stateManager.TryGetStateAsync<SnapshotRecord>(identity.SnapshotKey)`. If deserialization fails (schema change, corruption): catch exception, log warning, call `stateManager.RemoveStateAsync(identity.SnapshotKey)` to delete corrupt snapshot, return null (caller falls back to full event replay)
  - [ ] 4.6 Log snapshot creation with: correlationId (if available), tenantId, domain, aggregateId, sequenceNumber (rule #9 -- correlationId in every log)
  - [ ] 4.7 NEVER log the snapshot state content (rule #5 -- no payload in logs)
  - [ ] 4.8 Use `ConfigureAwait(false)` on all async calls

- [ ] Task 5: Integrate SnapshotManager into event persistence flow (AC: #6, #10)
  - [ ] 5.1 Modify `EventPersister` (or the actor state machine step that calls it) to check `ShouldCreateSnapshotAsync` after event persistence
  - [ ] 5.2 If snapshot is due, call `CreateSnapshotAsync` BEFORE `SaveStateAsync` -- this stages the snapshot in the same actor state batch
  - [ ] 5.3 The snapshot write is part of the same `IActorStateManager.SaveStateAsync()` call that commits events, ensuring atomicity (AC: #10)
  - [ ] 5.4 If snapshot creation fails (e.g., serialization error), log a warning but DO NOT fail the command processing (advisory per rule #12 spirit)
  - [ ] 5.5 Determine last snapshot sequence: prefer reading the snapshot record's own `SequenceNumber` field as the authoritative source (avoids metadata sync risk). Alternatively, accept `lastSnapshotSequence` as a parameter from the caller who already loaded the snapshot during state rehydration (Story 3.10 will load the snapshot in Step 3). Avoid duplicating this value in aggregate metadata to prevent drift.

- [ ] Task 6: Register SnapshotManager in DI (AC: #4)
  - [ ] 6.1 Add `ISnapshotManager` -> `SnapshotManager` registration in `AddEventStoreServer()`
  - [ ] 6.2 Add `SnapshotOptions` configuration binding
  - [ ] 6.3 Follow existing DI patterns (rule #10)

- [ ] Task 7: Create FakeSnapshotManager test double
  - [ ] 7.1 Create `FakeSnapshotManager.cs` in `src/Hexalith.EventStore.Testing/Fakes/`
  - [ ] 7.2 In-memory implementation for unit tests
  - [ ] 7.3 Allow test assertions on: snapshots created, snapshot intervals checked, snapshot content

- [ ] Task 8: Create unit tests for SnapshotManager (AC: #1, #3, #4, #6, #7, #9)
  - [ ] 8.1 Create `SnapshotManagerTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Events/`
  - [ ] 8.2 Test: `ShouldCreateSnapshot_AtDefaultInterval_ReturnsTrue` -- at sequence 100 (0 events since last), should create
  - [ ] 8.3 Test: `ShouldCreateSnapshot_BelowInterval_ReturnsFalse` -- at sequence 50, should not create
  - [ ] 8.4 Test: `ShouldCreateSnapshot_AtMultipleOfInterval_ReturnsTrue` -- at sequence 200, 300, etc.
  - [ ] 8.5 Test: `ShouldCreateSnapshot_WithCustomDomainInterval_UsesOverride` -- per-domain config
  - [ ] 8.6 Test: `CreateSnapshot_StoresViaActorStateManager` -- verifies SetStateAsync called with correct key
  - [ ] 8.7 Test: `CreateSnapshot_IncludesSequenceNumber` -- snapshot record has correct sequence
  - [ ] 8.8 Test: `CreateSnapshot_OverwritesPrevious` -- second snapshot at same key replaces first
  - [ ] 8.9 Test: `LoadSnapshot_ReturnsNullWhenNoSnapshot` -- no snapshot exists
  - [ ] 8.10 Test: `LoadSnapshot_ReturnsStoredSnapshot` -- snapshot round-trips correctly
  - [ ] 8.11 Test: `SnapshotOptions_DefaultIntervalIs100` -- mandatory configuration
  - [ ] 8.12 Test: `SnapshotOptions_IntervalMustBeAtLeast10` -- values below 10 rejected
  - [ ] 8.13 Test: `CreateSnapshot_SerializationFailure_LogsWarningAndSkips` -- serialization error does not throw
  - [ ] 8.14 Test: `LoadSnapshot_DeserializationFailure_ReturnsNullAndDeletesCorrupt` -- schema mismatch returns null, corrupt key removed

- [ ] Task 9: Create unit tests for SnapshotRecord (AC: #3, #8)
  - [ ] 9.1 Test: `SnapshotRecord_CreatedWithCorrectProperties` -- all fields populated
  - [ ] 9.2 Test: `SnapshotRecord_IsImmutable` -- record type guarantees

- [ ] Task 10: Create integration test for snapshot in event pipeline (AC: #6, #10)
  - [ ] 10.1 Create `SnapshotCreationIntegrationTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Events/`
  - [ ] 10.2 Test: Process 100 commands for an aggregate, verify snapshot created at sequence 100
  - [ ] 10.3 Test: Process 250 commands, verify snapshot at 100 and then at 200 (overwritten, only latest snapshot exists)
  - [ ] 10.4 Test: Process 50 commands, verify no snapshot created
  - [ ] 10.5 Test: Snapshot key matches `AggregateIdentity.SnapshotKey` pattern
  - [ ] 10.6 Test: Snapshot creation is atomic with event persistence (both in same SaveStateAsync batch)

- [ ] Task 11: Verify all tests pass
  - [ ] 11.1 Run `dotnet test` to confirm no regressions
  - [ ] 11.2 All new snapshot tests pass
  - [ ] 11.3 Verify existing Story 3.4-3.8 tests still pass

## Dev Notes

### Story Context

This story implements the **SnapshotManager** component referenced in the architecture as part of Epic 3's event storage and state management subsystem. Snapshots are critical for keeping actor cold activation fast (NFR4: <50ms at p99) by bounding the number of events that need to be replayed during state rehydration.

**Key dependency:** This story creates snapshots. Story 3.10 (State Reconstruction from Snapshot + Tail Events) consumes them. The `LoadSnapshotAsync` method is defined here but primarily used by Story 3.10's modifications to `EventStreamReader`.

### Architecture Compliance

- **FR13:** Snapshot creation at configurable intervals (every N events)
- **NFR4:** Actor cold activation with snapshot + tail events <50ms at p99
- **NFR19:** State rehydration time remains constant regardless of total event count
- **D1:** Event storage strategy -- snapshot stored alongside events via `IActorStateManager`
- **Rule #5:** Never log snapshot state content (payload data)
- **Rule #6:** IActorStateManager for all actor state operations -- snapshot stored via actor state manager
- **Rule #10:** Services registered via Add* extension methods
- **Rule #12:** Advisory writes -- snapshot failure must not block command processing
- **Rule #15:** Snapshot configuration is mandatory (default 100 events)
- **SEC-1:** EventStore owns envelope metadata; snapshots store aggregate state only

### Critical Design Decisions

- **Snapshot is a single key per aggregate.** Unlike events (one key per event), there is exactly one snapshot key per aggregate: `{tenant}:{domain}:{aggId}:snapshot`. New snapshots overwrite old ones. This keeps state store growth bounded.
- **Snapshot write is batched with event writes.** The snapshot `SetStateAsync` call is staged before `SaveStateAsync`, meaning the snapshot write and event writes are committed atomically in the same DAPR actor state transaction. This prevents orphaned snapshots (snapshot at sequence N but events not persisted).
- **Snapshot creation is advisory.** If snapshot creation fails (serialization error, state manager error), the command processing MUST continue. Snapshots are an optimization, not a correctness requirement. Log the failure as a warning and continue. The aggregate will simply do a full replay until the next successful snapshot.
- **SnapshotManager does NOT call SaveStateAsync.** It only stages the snapshot via `SetStateAsync`. The caller (EventPersister or state machine) calls `SaveStateAsync` to commit the entire batch. This is critical for atomicity.
- **Snapshot record is the source of truth for `lastSnapshotSequence`.** Do NOT duplicate this value in aggregate metadata -- it creates a sync risk where metadata says sequence 200 but the actual snapshot is at 100 (if a snapshot write failed silently). Instead, the caller (AggregateActor Step 3 in Story 3.10) loads the snapshot during rehydration and passes `lastSnapshotSequence` to `ShouldCreateSnapshotAsync`. If no snapshot exists, assume 0.
- **Per-domain interval via SnapshotOptions.** Default 100 events, minimum 10. Override per domain via `DomainIntervals` dictionary. This allows high-frequency aggregates (many small events) to snapshot more often than low-frequency ones.
- **Snapshot deserialization failure = graceful degradation.** Unlike events (where backward-compatible deserialization is mandatory per CRITICAL-1), snapshots are an ephemeral optimization cache. If a snapshot can't be deserialized (domain state schema changed), delete it and fall back to full event replay. Events are the source of truth; snapshots are a cache. This is the snapshot equivalent of a cache miss -- performance impact but no correctness impact.
- **Snapshot serialization strategy.** The `State` field in `SnapshotRecord` stores the aggregate state as serialized by System.Text.Json. The domain service's state type is opaque to EventStore. Consider storing as `JsonElement` or `byte[]` to avoid type coupling, though the actor state manager handles serialization transparently.
- **State store payload size limits.** Snapshot size is bounded by aggregate state size, which is a domain concern. Be aware that DAPR state store backends have varying payload limits (Redis: 512MB, Cosmos DB: 2MB per document). For extremely large aggregate states, the domain should be designed to keep state compact. Consider logging a warning if serialized snapshot exceeds a configurable threshold (e.g., 1MB).
- **Interval change behavior.** If the snapshot interval is changed (e.g., from 100 to 50), existing aggregates will converge at their next command. An aggregate at sequence 150 (last snapshot at 100) won't get a new snapshot until sequence 200 with the old interval, but with the new interval of 50, it will snapshot at the next command (since 150 - 100 >= 50). No migration or backfill needed.

### Existing Patterns to Follow

**Configuration pattern (from CommandStatusOptions):**
```csharp
public record SnapshotOptions
{
    public int DefaultInterval { get; init; } = 100;
    public Dictionary<string, int> DomainIntervals { get; init; } = [];
}
```

**Actor state pattern (from IdempotencyChecker):**
```csharp
// Read snapshot
ConditionalValue<SnapshotRecord> result =
    await stateManager.TryGetStateAsync<SnapshotRecord>(identity.SnapshotKey);

// Write snapshot (staged, committed by later SaveStateAsync)
await stateManager.SetStateAsync(identity.SnapshotKey, snapshotRecord);
```

**Key derivation (from AggregateIdentity -- already exists):**
```csharp
public string SnapshotKey => $"{TenantId}:{Domain}:{AggregateId}:snapshot";
```

### Mandatory Coding Patterns

- Primary constructors: `public class SnapshotManager(IOptions<SnapshotOptions> options, ILogger<SnapshotManager> logger)`
- Records for immutable data: `SnapshotRecord`, `SnapshotOptions`
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions in tests
- Feature folder organization (`Events/` folder in Server project)
- **Rule #5:** Never log event payload data or snapshot state content
- **Rule #6:** IActorStateManager for all actor state operations
- **Rule #9:** CorrelationId in every structured log entry
- **Rule #15:** Snapshot configuration mandatory (default 100)

### Project Structure Notes

- `src/Hexalith.EventStore.Server/Events/ISnapshotManager.cs` -- new interface
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` -- new implementation
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs` -- new model
- `src/Hexalith.EventStore.Server/Configuration/SnapshotOptions.cs` -- new configuration
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` -- modified (add DI registration)
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs` -- modified (integrate snapshot creation)
- `src/Hexalith.EventStore.Testing/Fakes/FakeSnapshotManager.cs` -- new test double
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs` -- new unit tests
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotCreationIntegrationTests.cs` -- new integration tests

### Previous Story Intelligence

**From Story 3.8 (Storage Key Isolation):**
- Snapshot keys follow tenant-scoped pattern: `{tenant}:{domain}:{aggId}:snapshot`
- AggregateIdentity.SnapshotKey property already defined and validated
- 4-layer storage isolation model applies to snapshot keys too
- Key injection prevention via AggregateIdentity regex validation

**From Story 3.7 (Event Persistence):**
- EventPersister uses `IActorStateManager` batch semantics: multiple `SetStateAsync` calls followed by single `SaveStateAsync`
- All event writes are atomic within an actor turn -- `SaveStateAsync` is all-or-nothing (DAPR runtime guarantee)
- ETag-based optimistic concurrency on aggregate metadata key
- Aggregate metadata tracks `currentSequence`

**From Story 3.4 (Event Stream Reader):**
- EventStreamReader reads events from sequence 1 to current
- Uses composite key pattern `{tenant}:{domain}:{aggId}:events:{seq}`
- Story 3.10 will modify EventStreamReader to read snapshot first, then tail events

**From Story 3.6 (Multi-Domain/Multi-Tenant):**
- DomainServiceOptions exists for per-domain configuration
- Multiple domains supported -- snapshot intervals should be per-domain configurable

### Git Intelligence

Recent commit `00b7880` implements Stories 3.1-3.8 in a single PR. The codebase follows:
- Primary constructors with DI
- Records for immutable data
- `ConfigureAwait(false)` on all async calls
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions
- Feature folder organization
- DI registration via `Add*` extension methods in `ServiceCollectionExtensions.cs`

### Testing Requirements

**Unit Tests (~14-17 new):**
- SnapshotOptions: default interval, minimum 10 validation, per-domain overrides
- SnapshotManager.ShouldCreateSnapshot: at interval, below interval, multiple of interval, custom domain interval
- SnapshotManager.CreateSnapshot: stores via state manager, includes sequence number, overwrites previous, serialization failure handled gracefully
- SnapshotManager.LoadSnapshot: returns null when missing, returns stored snapshot, deserialization failure returns null and deletes corrupt snapshot
- SnapshotRecord: correct construction, immutability

**Integration Tests (~5-7 new):**
- End-to-end snapshot creation after N events
- Snapshot overwrite at subsequent intervals
- No snapshot below threshold
- Atomic snapshot + event persistence
- Snapshot key format verification

**Total estimated new tests: ~19-24**

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 3, Story 3.9]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1 Event Storage Strategy]
- [Source: _bmad-output/planning-artifacts/architecture.md#FR13 Snapshot creation at configurable intervals]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR4 Actor cold activation <50ms]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR19 State rehydration constant time]
- [Source: _bmad-output/planning-artifacts/architecture.md#Rule 15 Mandatory snapshot config]
- [Source: _bmad-output/planning-artifacts/architecture.md#SnapshotManager in project structure]
- [Source: _bmad-output/implementation-artifacts/3-8-storage-key-isolation-and-composite-key-strategy.md]
- [Source: _bmad-output/implementation-artifacts/3-7-event-persistence-with-atomic-writes-and-sequence-numbers.md]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
