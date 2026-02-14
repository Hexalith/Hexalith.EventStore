# Story 3.7: Event Persistence with Atomic Writes & Sequence Numbers

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**BLOCKING: Stories 3.1-3.6 MUST have implementation artifacts before starting this story.**

Verify these files/classes exist before starting:
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (Story 3.2 -- 5-step orchestrator)
- `src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs` (Story 3.2)
- `src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs` (Story 1.2)
- `src/Hexalith.EventStore.Contracts/Events/EventMetadata.cs` (Story 1.2)
- `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` (Story 1.2)
- `src/Hexalith.EventStore.Contracts/Results/DomainResult.cs` (Story 1.3)
- `src/Hexalith.EventStore.Server/Commands/CommandRouter.cs` (Story 3.1)

Run `dotnet test` to confirm all existing tests pass before beginning.

## Story

As a **system operator**,
I want events persisted in an append-only, immutable event store with strictly ordered gapless sequence numbers and atomic writes,
So that event streams are consistent and trustworthy (FR9, FR10, FR16).

## Acceptance Criteria

1. **Event persistence via IActorStateManager** - Given the domain service returns one or more events, When Step 5 (state machine execution) persists events, Then `EventPersister` writes events via `IActorStateManager` using write-once keys `{tenant}:{domain}:{aggId}:events:{seq}` (D1, enforcement rule #11), And only `IActorStateManager` is used for state store access (enforcement rule #6).

2. **Strictly ordered gapless sequence numbers** - Given events are being persisted for an aggregate, When sequence numbers are assigned, Then sequence numbers start at 1 for a new aggregate and increment by 1 for each event (FR10), And there are no gaps in the sequence within an aggregate stream, And sequence numbers are derived from `AggregateMetadata.CurrentSequence` (incremented per event).

3. **11-field envelope metadata populated by EventStore** - Given the domain service returns `IEventPayload` instances, When EventPersister creates `EventEnvelope` records, Then EventStore populates ALL 11 envelope metadata fields (SEC-1): `AggregateId`, `TenantId`, `Domain`, `SequenceNumber`, `Timestamp`, `CorrelationId`, `CausationId`, `UserId`, `DomainServiceVersion`, `EventTypeName`, `SerializationFormat`, And domain services do NOT set any metadata fields -- EventStore owns them entirely.

4. **Atomic writes** - Given a command produces N events (N >= 1), When events are persisted, Then all N events are written via `IActorStateManager.SetStateAsync` for each event key plus metadata update, And `IActorStateManager.SaveStateAsync()` commits all changes atomically (D1: actor-level ACID), And a partial subset is NEVER persisted (FR16).

5. **Immutability enforced** - Given an event has been persisted at key `{tenant}:{domain}:{aggId}:events:{seq}`, Then the event is never modified or deleted after persistence (FR9, enforcement rule #11), And EventPersister uses `SetStateAsync` (not `AddOrUpdateStateAsync`) and never calls `RemoveStateAsync` on event keys.

6. **ETag-based optimistic concurrency on aggregate metadata** - Given two concurrent commands target the same aggregate, When both attempt to update `AggregateMetadata` at key `{tenant}:{domain}:{aggId}:metadata`, Then the actor's single-threaded turn-based model prevents true concurrency within one actor, But if the aggregate metadata ETag does not match (e.g., actor rebalancing scenario), Then `SaveStateAsync` fails with a concurrency exception, And the command processing fails with `ConcurrencyConflictException` (from Story 2.8).

7. **Performance requirement** - Given events are being persisted, When the event append operation completes, Then the latency is under 10ms at p99 (NFR3).

8. **AggregateMetadata updated atomically with events** - Given events are being persisted, When EventPersister writes events, Then it also updates `AggregateMetadata` at key `{tenant}:{domain}:{aggId}:metadata` with the new `CurrentSequence` (last event's sequence number) and `LastModified` timestamp, And the metadata update is part of the same atomic `SaveStateAsync` batch.

## Tasks / Subtasks

- [x] Task 0: Verify prerequisites and existing artifacts (BLOCKING)
  - [x] 0.1 Run all existing tests -- they must pass before proceeding
  - [x] 0.2 Confirm `AggregateActor` has 5-step orchestrator with Step 5 as STUB (Story 3.2)
  - [x] 0.3 Confirm `EventEnvelope` and `EventMetadata` records exist in Contracts
  - [x] 0.4 Confirm `AggregateIdentity` has `EventStreamKeyPrefix`, `MetadataKey` properties
  - [x] 0.5 Confirm `DomainResult` has `Events` property returning `IReadOnlyList<IEventPayload>`
  - [x] 0.6 Confirm `IActorStateManager` available on `AggregateActor` via `this.StateManager`

- [x] Task 1: Create IEventPersister interface (AC: #1, #4)
  - [x] 1.1 Create `IEventPersister` interface in `Server/Events/`
  - [x] 1.2 Define method: `Task PersistEventsAsync(AggregateIdentity identity, CommandEnvelope command, DomainResult domainResult, string domainServiceVersion)`
  - [x] 1.3 This method takes the identity, original command (for metadata extraction), domain result (event payloads), and domain service version
  - [x] 1.4 Namespace: `Hexalith.EventStore.Server.Events`

- [x] Task 2: Create EventPersister implementation (AC: #1, #2, #3, #4, #5, #8)
  - [x] 2.1 Create `EventPersister` class in `Server/Events/` implementing `IEventPersister`
  - [x] 2.2 Constructor: `EventPersister(IActorStateManager stateManager, ILogger<EventPersister> logger)` -- lightweight, created per actor call (same pattern as IdempotencyChecker, EventStreamReader)
  - [x] 2.3 `PersistEventsAsync` implementation:
    - Load current `AggregateMetadata` from `stateManager.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)` to get current sequence number
    - If metadata not found, start at sequence 0 (first event will be sequence 1)
    - For each `IEventPayload` in `domainResult.Events`:
      - Assign sequence number: `currentSequence + 1 + index`
      - Build `EventMetadata` with all 11 fields populated by EventStore (SEC-1):
        - `AggregateId` from `identity.AggregateId`
        - `TenantId` from `identity.TenantId`
        - `Domain` from `identity.Domain`
        - `SequenceNumber` = assigned sequence
        - `Timestamp` = `DateTimeOffset.UtcNow`
        - `CorrelationId` from `command.CorrelationId`
        - `CausationId` from `command.CausationId ?? command.CorrelationId`
        - `UserId` from `command.UserId`
        - `DomainServiceVersion` = passed parameter
        - `EventTypeName` = `eventPayload.GetType().FullName` (or `Name` if FullName is null)
        - `SerializationFormat` = `"json"`
      - Serialize payload to `byte[]` using `System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(eventPayload)`
      - Create `EventEnvelope(metadata, payloadBytes, extensions: null)`
      - Write via `stateManager.SetStateAsync($"{identity.EventStreamKeyPrefix}{sequenceNumber}", envelope)` (write-once key, rule #11)
    - Update `AggregateMetadata` with new `CurrentSequence` and `LastModified`:
      - `stateManager.SetStateAsync(identity.MetadataKey, new AggregateMetadata(newSequence, DateTimeOffset.UtcNow, null))`
    - Do NOT call `SaveStateAsync()` here -- the caller (AggregateActor) calls it once to commit all changes atomically (D1)
  - [x] 2.4 Log at Information level: `"Events persisted: Count={EventCount}, Sequences={FirstSeq}-{LastSeq}, AggregateId={ActorId}, CorrelationId={CorrelationId}"` (rule #9)
  - [x] 2.5 Log at Debug level each individual event write: `"Persisting event: Key={Key}, Type={EventTypeName}, Seq={Seq}"`
  - [x] 2.6 NEVER log event payload data (enforcement rule #5)
  - [x] 2.7 Use `ArgumentNullException.ThrowIfNull()` on all parameters (CA1062)
  - [x] 2.8 `ConfigureAwait(false)` on all async calls (CA2007)

- [x] Task 3: Verify AggregateMetadata record exists (from Story 3.4) (AC: #2, #8)
  - [x] 3.1 Confirm `AggregateMetadata` record exists in `Server/Events/` with properties: `long CurrentSequence`, `DateTimeOffset LastModified`, `string? ETag`
  - [x] 3.2 If not present, create it (Story 3.4 should have created this)

- [x] Task 4: Update AggregateActor to integrate EventPersister in Step 5 (AC: #1, #4)
  - [x] 4.1 In `AggregateActor.ProcessCommandAsync`, replace the Step 5 STUB with EventPersister call
  - [x] 4.2 Create `EventPersister` by resolving `ILogger<EventPersister>` from `host.LoggerFactory.CreateLogger<EventPersister>()` and passing `this.StateManager` (same pattern as IdempotencyChecker, EventStreamReader, TenantValidator)
  - [x] 4.3 After domain service invocation (Step 4) returns `DomainResult`:
    - If `domainResult.IsNoOp`, skip persistence (no events to store)
    - Otherwise, call `await eventPersister.PersistEventsAsync(command.AggregateIdentity, command, domainResult, domainServiceVersion).ConfigureAwait(false)`
  - [x] 4.4 After ALL state changes (idempotency record + events + metadata), call `await StateManager.SaveStateAsync().ConfigureAwait(false)` ONCE for atomic commit (D1)
  - [x] 4.5 Update `CommandProcessingResult` to include event count: add `int EventCount` property
  - [x] 4.6 If `SaveStateAsync` throws a concurrency exception (ETag mismatch), catch and rethrow as `ConcurrencyConflictException` from Story 2.8

- [x] Task 5: Create FakeEventPersister for testing (AC: all)
  - [x] 5.1 Create `FakeEventPersister` in `Testing/Fakes/` implementing `IEventPersister`
  - [x] 5.2 Store persisted events in an in-memory list for test assertions
  - [x] 5.3 Expose `IReadOnlyList<EventEnvelope> PersistedEvents` for verification
  - [x] 5.4 Support configurable failure (throw exception on demand) for error path testing

- [x] Task 6: Unit tests for EventPersister (AC: #1, #2, #3, #4, #5, #8)
  - [x] 6.1 Test: New aggregate -- first event gets sequence 1, metadata created with CurrentSequence=1
  - [x] 6.2 Test: Existing aggregate with CurrentSequence=5 -- next event gets sequence 6
  - [x] 6.3 Test: Multiple events from single command -- sequences are gapless (e.g., 6, 7, 8)
  - [x] 6.4 Test: All 11 metadata fields populated correctly from command and identity (SEC-1)
  - [x] 6.5 Test: Event payload serialized to JSON bytes
  - [x] 6.6 Test: Event keys follow write-once pattern `{tenant}:{domain}:{aggId}:events:{seq}` (rule #11)
  - [x] 6.7 Test: AggregateMetadata updated with new CurrentSequence and LastModified
  - [x] 6.8 Test: No-op result (empty events) -- no events persisted, no metadata change
  - [x] 6.9 Test: SaveStateAsync NOT called by EventPersister (caller responsibility)
  - [x] 6.10 Test: EventPersister never calls RemoveStateAsync on event keys (immutability, FR9)
  - [x] 6.11 Test: Rejection events persisted same as regular events (D3 -- everything is an event)

- [x] Task 7: Unit tests for AggregateActor Step 5 integration (AC: #4, #6)
  - [x] 7.1 Test: After Step 5, SaveStateAsync called exactly once (atomic commit)
  - [x] 7.2 Test: No-op domain result -- SaveStateAsync still called (for idempotency record)
  - [x] 7.3 Test: CommandProcessingResult includes correct EventCount
  - [x] 7.4 Test: Concurrency exception from SaveStateAsync rethrown as ConcurrencyConflictException

- [x] Task 8: Integration tests (AC: #1-#8)
  - [x] 8.1 Test: Full command processing pipeline persists events with correct keys and metadata
  - [x] 8.2 Test: Multiple commands to same aggregate produce gapless sequence numbers
  - [x] 8.3 Test: Events readable after persistence via EventStreamReader (round-trip validation)
  - [x] 8.4 Test: Atomic write -- all events from single command visible together (not partial)

- [x] Task 9: Verify all existing tests pass
  - [x] 9.1 Run `dotnet test` to confirm no regressions
  - [x] 9.2 Estimate: ~500+ existing tests should still pass

## Dev Notes

### Story Context

This story implements the core event persistence mechanism -- the heart of the event sourcing system. It creates `EventPersister`, a focused component that writes events to the DAPR actor state store using write-once keys with gapless sequence numbers. The critical design is that EventPersister uses `IActorStateManager.SetStateAsync` to queue state changes, and the calling `AggregateActor` commits everything atomically via a single `SaveStateAsync` call (D1: actor-level ACID).

**Key insight:** DAPR actors provide turn-based single-threaded concurrency per actor instance. Within a single actor turn, multiple `SetStateAsync` calls are batched and committed atomically by `SaveStateAsync`. This is the mechanism that guarantees FR16 (atomic writes) universally across all DAPR-compatible state store backends.

### Architecture Compliance

- **FR9:** Append-only, immutable event store -- events never modified or deleted after persistence
- **FR10:** Strictly ordered, gapless sequence numbers within each aggregate stream
- **FR16:** Atomic event writes -- command produces 0 or N events, never partial subset
- **D1:** Single-key-per-event storage: `{tenant}:{domain}:{aggId}:events:{seq}`
- **D1:** Actor-level ACID via `IActorStateManager` batch + `SaveStateAsync`
- **SEC-1:** EventStore owns all 11 envelope metadata fields -- domain services return payloads only
- **NFR3:** Event append latency under 10ms at p99
- **NFR25:** Checkpointed state machine recovery -- no duplicate event persistence
- **Rule #5:** Never log event payload data
- **Rule #6:** IActorStateManager for all actor state operations
- **Rule #9:** CorrelationId in every structured log entry
- **Rule #11:** Write-once event keys -- never updated or deleted

### Security Constraints

- **SEC-1:** EventStore populates ALL 11 metadata fields. Domain services return `IEventPayload` instances only. EventPersister builds the full `EventEnvelope` with metadata derived from `CommandEnvelope` and `AggregateIdentity`. This prevents event stream poisoning via malicious domain services.
- **Rule #5:** Event payload bytes are NEVER logged. Only envelope metadata (event type, sequence number, aggregate ID) appears in log entries.

### Critical Design Decisions

- **EventPersister does NOT call SaveStateAsync.** It only queues state changes via `SetStateAsync`. The `AggregateActor` calls `SaveStateAsync` once after ALL state changes (idempotency record from Step 1 + events from Step 5 + metadata) to achieve atomic commit. This is the D1 pattern.
- **Sequence numbers derived from AggregateMetadata.CurrentSequence.** The metadata key `{tenant}:{domain}:{aggId}:metadata` stores the last persisted sequence number. EventPersister reads this, then assigns `currentSequence + 1` through `currentSequence + N` for N events.
- **Write-once keys (rule #11).** Once an event is written at `{tenant}:{domain}:{aggId}:events:{seq}`, it is immutable. EventPersister uses `SetStateAsync` which overwrites, but since sequence numbers always increment, the same key is never written twice in correct operation.
- **Rejection events are persisted like regular events (D3).** Domain rejections (`IRejectionEvent`) are stored in the event stream, incrementing the sequence number. They are events, not errors.

### Previous Story Intelligence

**From Story 3.2 (AggregateActor Orchestrator):**
- AggregateActor has 5-step orchestrator pattern
- Step 5 is currently a STUB to be replaced
- IdempotencyChecker pattern: lightweight class created per actor call, receives `StateManager` and `ILogger`
- Single `SaveStateAsync()` call at end commits all changes atomically

**From Story 3.4 (Event Stream Reader):**
- `AggregateMetadata` record with `CurrentSequence`, `LastModified`, `ETag` -- stored at `{tenant}:{domain}:{aggId}:metadata`
- `EventStreamReader` reads events from keys `{identity.EventStreamKeyPrefix}{seq}`
- Round-trip: EventPersister writes what EventStreamReader reads -- key patterns MUST match exactly

**From Story 3.6 (Multi-Domain/Multi-Tenant):**
- Composite key isolation verified for multi-tenant scenarios
- Domain service response size validation (max 1000 events per result)
- `DomainServiceOptions.MaxEventsPerResult` limits event count

**From Story 2.8 (Concurrency Conflict Handling):**
- `ConcurrencyConflictException` exists in `Server/Commands/`
- `ConcurrencyConflictExceptionHandler` converts to 409 Conflict ProblemDetails
- Use this exception when `SaveStateAsync` fails due to ETag mismatch

### Git Intelligence

Recent commits show Epic 2 completion and Epic 3 stories 3.1-3.2 infrastructure. The codebase follows:
- Primary constructors with DI
- Records for immutable data
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions
- Feature folder organization

### Mandatory Coding Patterns

- Primary constructors: `public class Foo(IDep dep) : Base`
- Records for immutable data
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions
- Feature folder organization
- **Rule #4:** No custom retry logic (DAPR resiliency only)
- **Rule #5:** Never log event payload data
- **Rule #6:** IActorStateManager for all actor state operations
- **Rule #9:** CorrelationId in every structured log entry
- **Rule #11:** Event store keys are write-once -- never updated or deleted

### Project Structure Notes

- `src/Hexalith.EventStore.Server/Events/` -- EventPersister, IEventPersister (new)
- `src/Hexalith.EventStore.Server/Events/` -- EventStreamReader, AggregateMetadata (existing from Story 3.4)
- `src/Hexalith.EventStore.Server/Actors/` -- AggregateActor (modify Step 5)
- `src/Hexalith.EventStore.Contracts/Events/` -- EventEnvelope, EventMetadata (existing)
- `src/Hexalith.EventStore.Testing/Fakes/` -- FakeEventPersister (new)
- `tests/Hexalith.EventStore.Server.Tests/Events/` -- EventPersisterTests (new)
- `tests/Hexalith.EventStore.Server.Tests/Actors/` -- AggregateActorTests (extend)
- `tests/Hexalith.EventStore.IntegrationTests/` -- EventPersistenceIntegrationTests (new)

### Testing Requirements

**Unit Tests (~15-20 new):**
- EventPersister: sequence assignment, metadata population, key patterns, immutability (11 tests)
- AggregateActor Step 5: atomic commit, concurrency exception, event count (4 tests)

**Integration Tests (~4-6 new):**
- Full pipeline event persistence round-trip
- Multi-command gapless sequences
- EventStreamReader round-trip validation
- Atomic write verification

**Total estimated new tests: ~19-26**

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 3, Story 3.7]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1 Event Storage Strategy]
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC-1 EventStore owns all metadata]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 11 Write-once keys]
- [Source: _bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concern 7 Crash Recovery]
- [Source: _bmad-output/implementation-artifacts/3-4-event-stream-reader-and-state-rehydration.md]
- [Source: _bmad-output/implementation-artifacts/3-2-aggregateactor-orchestrator-and-idempotency-check.md]
- [Source: _bmad-output/implementation-artifacts/2-8-optimistic-concurrency-conflict-handling.md]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- No blocking issues encountered during implementation.

### Completion Notes List

- Created `IEventPersister` interface and `EventPersister` implementation in `Server/Events/`
- EventPersister writes events via `IActorStateManager.SetStateAsync` using write-once keys `{tenant}:{domain}:{aggId}:events:{seq}`
- All 11 envelope metadata fields populated by EventStore (SEC-1): AggregateId, TenantId, Domain, SequenceNumber, Timestamp, CorrelationId, CausationId, UserId, DomainServiceVersion, EventTypeName, SerializationFormat
- EventPersister does NOT call `SaveStateAsync` -- caller (AggregateActor) commits atomically (D1)
- Replaced AggregateActor Step 5 STUB with real EventPersister integration
- Added `EventCount` property to `CommandProcessingResult`
- Added concurrency exception handling: `InvalidOperationException` from `SaveStateAsync` wrapped as `ConcurrencyConflictException`
- Domain service version extracted from command extensions via `DaprDomainServiceInvoker.ExtractVersion`
- No-op results skip persistence but still commit idempotency record
- Created `FakeEventPersister` in Testing for downstream test support
- 19 unit tests for EventPersister (sequence assignment, metadata population, key patterns, immutability, no-op, rejection events, guard clauses incl. empty/whitespace version)
- 8 unit tests for AggregateActor Step 5 integration (atomic commit, event count, concurrency exception, rejection event persistence, OperationCanceledException passthrough)
- 5 integration tests (round-trip, gapless sequences, atomic write verification)
- 1 existing test updated to reflect new Step 5 behavior (no longer a STUB)
- Updated existing test `ProcessCommandAsync_DomainSuccess_ProceedsToStep5` → `ProcessCommandAsync_DomainSuccess_PersistsEventsViaStep5`
- All 608 tests pass (576 original + 32 new, 0 regressions)

### Change Log

- 2026-02-14: Story 3.7 implementation complete -- event persistence with atomic writes and gapless sequence numbers
- 2026-02-14: Code review fixes applied:
  - H1: AggregateActor now persists rejection events via EventPersister (D3 compliance)
  - H2: Narrowed SaveStateAsync catch from all exceptions to InvalidOperationException
  - M1: FakeEventPersister uses per-aggregate sequence tracking
  - M2: Added OperationCanceledException passthrough test
  - M3: EventPersister validates domainServiceVersion for empty/whitespace
  - M4: Updated File List documentation
  - L1: Corrected test counts
  - L2: Rejection result now includes EventCount

### File List

**New files:**
- `src/Hexalith.EventStore.Server/Events/IEventPersister.cs`
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs`
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPersisterTests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Events/EventPersistenceIntegrationTests.cs`

**Modified files:**
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (Step 5 STUB replaced with EventPersister integration; rejection path now persists events per D3; narrowed concurrency catch)
- `src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs` (added EventCount property)
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs` (updated Step 5 test, added 8 new Step 5 integration tests)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (sprint tracking sync)
