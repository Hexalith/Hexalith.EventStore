# Story 3.10: State Reconstruction from Snapshot + Tail Events

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**BLOCKING: Stories 3.4 (EventStreamReader), 3.7 (EventPersister), and 3.9 (SnapshotManager) MUST be implemented before this story.**

Verify these files/classes exist before starting:
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs` (Story 3.4 -- full replay implementation)
- `src/Hexalith.EventStore.Server/Events/IEventStreamReader.cs` (Story 3.4 -- interface)
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` (Story 3.9 -- snapshot creation/loading)
- `src/Hexalith.EventStore.Server/Events/ISnapshotManager.cs` (Story 3.9 -- interface with LoadSnapshotAsync)
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs` (Story 3.9 -- snapshot model)
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs` (Story 3.7 -- event persistence)
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (Story 3.2 -- 5-step orchestrator)
- `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` (Story 1.2 -- includes `SnapshotKey` property)
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryStateManager.cs` (Story 1.4 -- test fake for IActorStateManager)
- `src/Hexalith.EventStore.Testing/Fakes/FakeSnapshotManager.cs` (Story 3.9 -- test fake)

Run `dotnet test` to confirm all existing tests pass before beginning.

## Story

As a **system operator**,
I want the system to reconstruct aggregate state from the latest snapshot plus only subsequent events,
So that actor cold activation remains fast regardless of total event history (FR14, NFR4).

## Acceptance Criteria

1. **Snapshot-first rehydration** - Given an aggregate has a snapshot at sequence 500 and events 501-520, When the actor rehydrates state, Then EventStreamReader loads the snapshot first, then reads only events from sequence 501 onward, And the reconstructed state is identical to a full replay from sequence 1 to 520.

2. **Cold activation performance** - Given an aggregate with a snapshot + tail events, When the actor cold-activates, Then actor cold activation with snapshot + tail events completes within 50ms at p99 (NFR4), And state rehydration time remains constant regardless of total event count (NFR19).

3. **No-snapshot fallback** - Given an aggregate has no snapshot (new aggregate or snapshot deleted), When the actor rehydrates state, Then full replay from sequence 1 is used as fallback, And the behavior is identical to the current Story 3.4 implementation.

4. **Snapshot-based tail event reading** - Given a snapshot exists at sequence N, When EventStreamReader loads tail events, Then it reads only events from sequence N+1 to currentSequence using the composite key pattern `{tenant}:{domain}:{aggId}:events:{seq}`, And events are read in parallel (maintaining existing parallel read optimization from Story 3.4).

5. **State reconstruction correctness** - Given any combination of snapshot + tail events, When the system reconstructs state, Then the result is identical to what a full replay from sequence 1 would produce. Verification: run both full-replay and snapshot+tail rehydration for the same aggregate in tests and compare the resulting `RehydrationResult.SnapshotState` (after applying `Events`) against the full-replay `Events` list projected to the same state. Since EventStore is schema-ignorant, correctness is verified by ensuring the same event envelopes are available for domain projection in both paths.

6. **Snapshot sequence tracking for future snapshots** - Given a snapshot is loaded during rehydration, When the actor determines whether to create a new snapshot (Story 3.9 integration), Then `lastSnapshotSequence` is derived from the loaded snapshot's `SequenceNumber` property, And this eliminates the redundant snapshot load that currently happens after full replay in AggregateActor Step 3.

7. **Corrupt/incompatible snapshot handling** - Given a snapshot exists but cannot be deserialized, When EventStreamReader attempts to load it, Then SnapshotManager.LoadSnapshotAsync returns null (already handles graceful degradation per Story 3.9), And EventStreamReader falls back to full replay from sequence 1, And a warning is logged.

8. **Empty tail events** - Given a snapshot exists at the current sequence (snapshot at sequence 500, currentSequence is 500), When the actor rehydrates, Then the snapshot state is returned directly with no tail event reads, And this is the fastest rehydration path.

9. **Tail events maintain strict ordering** - Given tail events are loaded from sequence N+1, When events are applied to the snapshot state, Then events are applied in strict sequence order (as guaranteed by the existing EventStreamReader sort logic).

10. **Integration with AggregateActor Step 3** - Given the modified rehydration flow, When AggregateActor Step 3 executes, Then it calls EventStreamReader with snapshot-awareness, And the `lastSnapshotSequence` is naturally available for Step 5b (snapshot creation decision) without a separate snapshot load.

11. **AggregateActor Step 4 handles snapshot+tail state** - Given RehydrationResult contains `SnapshotState` (object?, from snapshot) and `Events` (List<EventEnvelope>, tail events only), When AggregateActor Step 4 invokes the domain service, Then it passes the composite state correctly: if `SnapshotState` is non-null, pass `RehydrationResult` (or its components) as `currentState` so the domain service can apply tail events on top of the snapshot state. If `SnapshotState` is null (full replay / new aggregate), pass the full event list as before. The domain service contract `(Command, CurrentState?) -> List<DomainEvent>` receives the state in a format it can process.

12. **RehydrationResult separates snapshot state from tail events** - Given EventStreamReader returns a RehydrationResult, Then `SnapshotState` (object?) contains the opaque domain state from the snapshot (or null if no snapshot), And `Events` (List<EventEnvelope>) contains ONLY the tail events after the snapshot (or ALL events if no snapshot), And `LastSnapshotSequence` (long) is the snapshot's sequence number (or 0), And `CurrentSequence` (long) is the highest event sequence number.

## Tasks / Subtasks

- [x] Task 0: Verify prerequisites and understand current state (BLOCKING)
  - [x] 0.1 Run all existing tests -- they must pass before proceeding
  - [x] 0.2 Review `EventStreamReader.cs` current full-replay implementation
  - [x] 0.3 Review `AggregateActor.cs` Step 3 current rehydration flow (loads snapshot separately after full replay)
  - [x] 0.4 Review `SnapshotManager.LoadSnapshotAsync` implementation (Story 3.9)
  - [x] 0.5 Review `InMemoryStateManager` to understand parallel read behavior in tests

- [x] Task 1: Update IEventStreamReader interface (AC: #1, #4, #6)
  - [x] 1.1 Modify `IEventStreamReader.cs` to add a new method or update `RehydrateAsync` signature
  - [x] 1.2 New signature: `Task<RehydrationResult?> RehydrateAsync(AggregateIdentity identity, SnapshotRecord? snapshot = null)`
  - [x] 1.3 Keep backward compatibility or update all callers

- [x] Task 2: Create RehydrationResult model (AC: #1, #6, #12)
  - [x] 2.1 Create `RehydrationResult.cs` in `src/Hexalith.EventStore.Server/Events/`
  - [x] 2.2 Properties (separated per elicitation analysis):
    - `SnapshotState` (object? -- opaque domain state from snapshot, null if no snapshot used or new aggregate)
    - `Events` (List<EventEnvelope> -- tail events after snapshot sequence, OR all events if no snapshot)
    - `LastSnapshotSequence` (long -- snapshot's SequenceNumber, 0 if no snapshot)
    - `CurrentSequence` (long -- highest event sequence number from metadata)
  - [x] 2.3 Use a record type for immutability
  - [x] 2.4 Computed property: `int TailEventCount => Events.Count` (for diagnostics/logging)
  - [x] 2.5 Computed property: `bool UsedSnapshot => SnapshotState is not null` (for logging rehydration mode)

- [x] Task 3: Implement snapshot-aware EventStreamReader (AC: #1, #3, #4, #5, #7, #8, #9)
  - [x] 3.1 Modify `EventStreamReader.RehydrateAsync` to accept optional `SnapshotRecord?`
  - [x] 3.2 If snapshot provided: use `snapshot.SequenceNumber` as start, read tail events from `snapshot.SequenceNumber + 1` to `currentSequence`
  - [x] 3.3 If snapshot provided and no tail events needed (snapshot at currentSequence): return snapshot state directly (AC: #8)
  - [x] 3.4 If no snapshot provided: fall back to full replay from sequence 1 (AC: #3)
  - [x] 3.5 Maintain parallel read optimization for tail events (existing `Task.WhenAll` pattern)
  - [x] 3.6 Return `RehydrationResult` with snapshotState, events (tail only or all), lastSnapshotSequence, currentSequence
  - [x] 3.7 Log rehydration mode: "snapshot+tail" or "full-replay" with event count and timing

- [x] Task 4: Update AggregateActor Step 3 and Step 4 for snapshot-first rehydration (AC: #6, #10, #11)
  - [x] 4.1 Modify AggregateActor Step 3 to load snapshot FIRST via `snapshotManager.LoadSnapshotAsync`
  - [x] 4.2 Pass loaded snapshot to `EventStreamReader.RehydrateAsync(identity, snapshot)`
  - [x] 4.3 Use `RehydrationResult.LastSnapshotSequence` for Step 5b snapshot creation decision
  - [x] 4.4 Remove the redundant separate snapshot load that currently happens after full replay
  - [x] 4.5 Construct `currentState` for Step 4 domain invocation from `RehydrationResult`:
    - If `SnapshotState` is non-null AND `Events` is non-empty: pass `RehydrationResult` (snapshot state + tail events) as `currentState` so domain service can apply tail events to snapshot state
    - If `SnapshotState` is non-null AND `Events` is empty: pass `SnapshotState` directly as `currentState` (snapshot IS current state)
    - If `SnapshotState` is null: pass `Events` list as `currentState` (full replay, same as current behavior)
  - [x] 4.6 CRITICAL: Investigated `IDomainProcessor`, `DomainProcessorBase`, `DaprDomainServiceInvoker` contract. Chose Option A approach: `ConstructDomainState()` method in AggregateActor returns `RehydrationResult` (snapshot+tail), `SnapshotState` directly (snapshot-only), or `Events` list (full replay) based on the three AC #11 cases. Domain service receives state in format it can process without modifying `IDomainProcessor` or `DomainProcessorBase`.

- [x] Task 5: Update FakeEventStreamReader test double
  - [x] 5.1 Created `FakeEventStreamReader` in `src/Hexalith.EventStore.Testing/Fakes/`
  - [x] 5.2 Support the updated interface with snapshot parameter
  - [x] 5.3 Records all rehydration calls for test assertions with configurable ResultToReturn

- [x] Task 6: Create unit tests for snapshot-aware EventStreamReader (AC: #1, #3, #4, #5, #7, #8, #9)
  - [x] 6.1 Test: `RehydrateAsync_WithSnapshot_ReadsOnlyTailEvents` -- snapshot at 500, events 501-520 read
  - [x] 6.2 Test: `RehydrateAsync_WithSnapshot_NoTailEvents_ReturnsSnapshotState` -- snapshot at current sequence
  - [x] 6.3 Test: `RehydrateAsync_WithoutSnapshot_FullReplay` -- null snapshot, all events from 1
  - [x] 6.4 Test: `RehydrateAsync_WithSnapshot_TailEventsInOrder` -- strict sequence ordering maintained
  - [x] 6.5 Test: `RehydrateAsync_WithSnapshot_ParallelReads` -- tail events loaded via Task.WhenAll
  - [x] 6.6 Test: `RehydrateAsync_WithSnapshot_CorrectKeyPattern` -- reads `{tenant}:{domain}:{aggId}:events:{501}` etc.
  - [x] 6.7 Test: `RehydrateAsync_WithSnapshot_ReturnsCorrectRehydrationResult` -- all fields populated
  - [x] 6.8 Test: `RehydrateAsync_NewAggregate_NoSnapshotNoEvents_ReturnsNull` -- unchanged behavior
  - [x] 6.9 Test: `RehydrateAsync_WithSnapshot_MissingTailEvent_ThrowsMissingEventException` -- gap detection in tail

- [x] Task 7: Create performance test for snapshot+tail rehydration (AC: #2)
  - [x] 7.1 Test: `RehydrateAsync_SnapshotPlusTailEvents_CompletesWithin50ms` -- NFR4 compliance
  - [x] 7.2 Test: `RehydrateAsync_SnapshotWithManyTailEvents_FasterThanFullReplay` -- comparative performance
  - [x] 7.3 Test: snapshot at 10000, tail events 10001-10020 must be significantly faster than full 10020-event replay

- [x] Task 8: Create integration tests for AggregateActor snapshot-based rehydration (AC: #6, #10)
  - [x] 8.1 Test: `RehydrateWithSnapshot_ReadsOnlyTailEvents_NotAllEvents` -- verify rehydration uses snapshot for tail-only reads
  - [x] 8.2 Test: `RehydrateWithSnapshot_LastSnapshotSequence_FlowsCorrectlyForSnapshotDecision` -- lastSnapshotSequence flows correctly
  - [x] 8.3 Test: `SnapshotPlusTail_TailEventsMatch_FullReplayEvents` -- state correctness comparison
  - [x] 8.4 Test: `NewAggregate_NoSnapshot_ReturnsNull` -- no-snapshot fallback for new aggregates

- [x] Task 9: Update existing EventStreamReader tests (AC: #3)
  - [x] 9.1 Update existing tests to work with new return type (`RehydrationResult?` vs `object?`)
  - [x] 9.2 Verify all existing tests still pass with the updated interface
  - [x] 9.3 AggregateActorTests unchanged -- NSubstitute defaults handle new signature without explicit mock updates

- [x] Task 10: Verify all tests pass
  - [x] 10.1 Run `dotnet test` to confirm no regressions -- 703 passed, 0 failed
  - [x] 10.2 All new snapshot+tail tests pass (16 new tests: 12 unit + 4 integration)
  - [x] 10.3 All existing Story 3.4-3.9 tests still pass

## Dev Notes

### Story Context

This story modifies the **EventStreamReader** component to leverage snapshots created by Story 3.9 (SnapshotManager). Currently, EventStreamReader always performs a full replay of ALL events from sequence 1 to currentSequence. This story changes it to load the latest snapshot first (if available) and replay only the "tail" events after the snapshot -- dramatically reducing actor cold activation time for aggregates with many events.

**Key change:** AggregateActor Step 3 currently does full replay then loads snapshot separately for sequence tracking. After this story, it loads the snapshot FIRST, passes it to EventStreamReader, and gets both the reconstructed state AND lastSnapshotSequence in one flow.

**What currently exists (to modify, NOT rewrite):**
- `EventStreamReader.RehydrateAsync(AggregateIdentity)` -- full replay, returns `object?` (a `List<EventEnvelope>`)
- `AggregateActor` Step 3 -- calls `RehydrateAsync`, then separately loads snapshot for sequence tracking
- `SnapshotManager.LoadSnapshotAsync` -- loads snapshot, handles corrupt/missing snapshots gracefully
- `IEventStreamReader` -- interface with single `RehydrateAsync` method

### Architecture Compliance

- **FR14:** State reconstruction from latest snapshot plus subsequent events
- **NFR4:** Actor cold activation with snapshot + tail events must complete within 50ms at p99
- **NFR19:** State rehydration time must remain constant regardless of total event count
- **D1:** Event storage strategy -- events stored as `{tenant}:{domain}:{aggId}:events:{seq}`
- **Rule #5:** Never log event payload data or snapshot state content
- **Rule #6:** IActorStateManager for all actor state operations
- **Rule #9:** CorrelationId in every structured log entry
- **Rule #12:** Advisory writes -- snapshot loading failure falls back gracefully to full replay
- **Rule #14:** DAPR sidecar call timeout is 5 seconds
- **Rule #15:** Mandatory snapshot configuration (default 100 events)
- **CRITICAL-1:** UnknownEvent during state rehydration is an error condition, not a skip-and-continue path

### Critical Design Decisions

- **RehydrationResult separates snapshot state from tail events (CRITICAL).** The `RehydrationResult` record has two distinct fields: `SnapshotState: object?` (the opaque domain state from the snapshot, or null) and `Events: List<EventEnvelope>` (tail events after the snapshot, or ALL events if no snapshot). This separation is essential because EventStore is schema-ignorant -- it cannot apply tail events to snapshot state. Only the domain service knows its own state shape and can project events onto it.
- **State projection gap -- the core design challenge.** Currently, `RehydrateAsync` returns `List<EventEnvelope>` as `object?`, which the domain service receives as `currentState` and internally projects. With snapshots, the domain service receives `(snapshotState + tailEvents)` and must apply tail events on top of the snapshot state before processing the new command. This may require changes to `IDomainProcessor` or `DomainProcessorBase` in the Client package, or the introduction of a new state composition pattern. The dev agent MUST investigate the actual domain service contract (`IDomainProcessor`, `DomainProcessorBase`, `DaprDomainServiceInvoker`) to determine the correct approach before implementing. Do NOT assume the existing contract handles this automatically.
- **Parallel reads preserved for tail events.** The existing `Task.WhenAll` optimization for parallel event reads is maintained for tail events. Only the range changes: from `1..currentSequence` to `(snapshotSequence+1)..currentSequence`. With default snapshot interval of 100, worst case is 99 parallel tail event reads (~2-4ms with DAPR sidecar overhead for parallel calls).
- **Backward compatibility: full replay fallback.** If no snapshot exists, the method falls back to full replay from sequence 1. In this case, `RehydrationResult.SnapshotState` is null and `Events` contains ALL events. This is the existing behavior and ensures correctness for new aggregates or aggregates where snapshots were deleted/corrupted.
- **SnapshotManager.LoadSnapshotAsync already handles corruption.** Story 3.9 implemented graceful degradation: if a snapshot can't be deserialized, it deletes the corrupt snapshot and returns null. EventStreamReader simply checks if snapshot is null and falls back to full replay. No additional corruption handling needed in this story.
- **No separate snapshot load needed in AggregateActor.** Currently, AggregateActor Step 3 does full replay, then separately loads the snapshot ONLY to get `lastSnapshotSequence` for the Step 5b snapshot creation decision. After this story, the snapshot is loaded FIRST, passed to EventStreamReader, and `lastSnapshotSequence` comes from the `RehydrationResult`. This eliminates the redundant state store read.
- **Interface change is breaking but contained.** Changing `IEventStreamReader.RehydrateAsync` from `Task<object?>` to `Task<RehydrationResult>` with an optional `SnapshotRecord?` parameter is a breaking change. The blast radius is contained to: AggregateActor (1 call site), existing EventStreamReaderTests, AggregateActorTests (mock setup), and FakeEventStreamReader. All are within the Server project/tests. No external consumers are affected.
- **Snapshot schema rollback resilience.** If a domain deploys a new state schema, creates snapshots, then rolls back: the old-schema domain service can't deserialize the new-schema snapshot. Story 3.9's graceful degradation handles this (delete corrupt snapshot, full replay). After full replay with the old schema, a new correct snapshot is created. This is self-healing.
- **Performance budget.** NFR4 requires <50ms at p99. Snapshot load (1 sidecar call, ~2ms) + metadata load (1 call, ~2ms) + parallel tail event reads (1 round trip for up to 99 events, ~2-4ms) = ~6-8ms. Well within budget. Full replay of 10,000 events would be ~20-40ms (parallel) vs ~6-8ms with snapshot. The optimization is significant for large aggregates.

### Existing Patterns to Follow

**Parallel read pattern (from current EventStreamReader):**
```csharp
// Read tail events in parallel
var readTasks = Enumerable.Range(startSequence, count)
    .Select(seq => stateManager.TryGetStateAsync<EventEnvelope>(
        $"{identity.EventKeyPrefix}{seq}"));
var results = await Task.WhenAll(readTasks).ConfigureAwait(false);
```

**Actor state pattern (from SnapshotManager.LoadSnapshotAsync):**
```csharp
ConditionalValue<SnapshotRecord> result =
    await stateManager.TryGetStateAsync<SnapshotRecord>(identity.SnapshotKey)
        .ConfigureAwait(false);
```

**RehydrationResult pattern (new, similar to existing records):**
```csharp
public record RehydrationResult(
    object? SnapshotState,              // Opaque domain state from snapshot, null if no snapshot
    List<EventEnvelope> Events,         // Tail events (or all events if no snapshot)
    long LastSnapshotSequence,          // Snapshot's SequenceNumber, 0 if no snapshot
    long CurrentSequence)               // Highest event sequence from metadata
{
    public int TailEventCount => Events.Count;
    public bool UsedSnapshot => SnapshotState is not null;
}
```

### Mandatory Coding Patterns

- Primary constructors: `public class EventStreamReader(IActorStateManager stateManager, ILogger<EventStreamReader> logger)`
- Records for immutable data: `RehydrationResult`
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions in tests
- Feature folder organization (`Events/` folder in Server project)
- **Rule #5:** Never log event payload data or snapshot state content
- **Rule #6:** IActorStateManager for all actor state operations
- **Rule #9:** CorrelationId in every structured log entry
- **Rule #14:** DAPR sidecar timeout 5 seconds

### Project Structure Notes

- `src/Hexalith.EventStore.Server/Events/RehydrationResult.cs` -- new model
- `src/Hexalith.EventStore.Server/Events/IEventStreamReader.cs` -- modified (updated return type/signature)
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs` -- modified (snapshot-aware rehydration)
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- modified (Step 3 uses snapshot-first flow, Step 4 handles snapshot+tail state composition, Step 5b uses RehydrationResult.LastSnapshotSequence)
- `src/Hexalith.EventStore.Client/Handlers/IDomainProcessor.cs` -- potentially modified (if domain service contract needs to accept snapshot state + tail events)
- `src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs` -- potentially modified (state projection from snapshot + events)
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventStreamReader.cs` -- new or modified test double
- `tests/Hexalith.EventStore.Server.Tests/Events/EventStreamReaderTests.cs` -- modified (add snapshot-aware tests, update existing tests for new return type)
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotRehydrationTests.cs` -- new integration tests

### Previous Story Intelligence

**From Story 3.9 (Snapshot Creation at Configurable Intervals):**
- SnapshotManager is registered as singleton in DI via `AddEventStoreServer()`
- `LoadSnapshotAsync` handles: missing snapshots (returns null), corrupt snapshots (deletes and returns null)
- SnapshotRecord contains `SequenceNumber` -- the event sequence when snapshot was taken
- AggregateActor currently loads snapshot AFTER full replay, only for `lastSnapshotSequence` tracking
- Snapshot write is staged in same `SaveStateAsync` batch as events (atomicity guaranteed)
- `lastSnapshotSequence = existingSnapshot?.SequenceNumber ?? 0`
- All 682 tests pass (35 from Story 3.9, 647 prior)

**From Story 3.4 (Event Stream Reader):**
- EventStreamReader uses parallel reads via `Task.WhenAll` for performance
- Returns `List<EventEnvelope>` as `object?` (comment notes Stories 3.5+ will add domain-specific projection)
- Validates strict sequence ordering, throws on missing events or deserialization failures
- Performance: 1000 events must load within 100ms (parallel read optimization)

**From Story 3.7 (Event Persistence):**
- EventPersister returns `Task<long>` (newSequence) after Story 3.9 integration
- Events stored as `{tenant}:{domain}:{aggId}:events:{seq}`
- Metadata stored as `{tenant}:{domain}:{aggId}:metadata` with currentSequence

### Git Intelligence

Recent commits show Stories 3.6-3.9 implemented together in PR #33. The codebase follows:
- Primary constructors with DI
- Records for immutable data
- `ConfigureAwait(false)` on all async calls
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions
- Feature folder organization
- DI registration via `Add*` extension methods in `ServiceCollectionExtensions.cs`

### Testing Requirements

**Unit Tests (~9-12 new):**
- EventStreamReader with snapshot: tail-only reads, no-tail (snapshot at current), full-replay fallback
- RehydrationResult: correct construction, all fields populated
- Key pattern verification for tail event reads
- Parallel read optimization for tail events
- Missing tail event detection
- Performance: snapshot+tail faster than full replay

**Integration Tests (~4-6 new):**
- End-to-end: process commands, trigger snapshot, process more, verify rehydration uses snapshot+tail
- State correctness: snapshot+tail result matches full-replay result
- No-snapshot fallback for new aggregates
- lastSnapshotSequence flows correctly to Step 5b

**Existing Test Updates (~5-10 modified):**
- EventStreamReaderTests: update for new return type
- AggregateActorTests: update Step 3 mock setup

**Total estimated: ~18-28 tests (new + modified)**

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 3, Story 3.10]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1 Event Storage Strategy]
- [Source: _bmad-output/planning-artifacts/architecture.md#FR14 State reconstruction from snapshot + tail events]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR4 Actor cold activation <50ms]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR19 State rehydration constant time]
- [Source: _bmad-output/planning-artifacts/architecture.md#EventStreamReader in project structure]
- [Source: _bmad-output/planning-artifacts/architecture.md#AggregateActor thin orchestrator pipeline]
- [Source: _bmad-output/planning-artifacts/architecture.md#CRITICAL-1 UnknownEvent handling]
- [Source: _bmad-output/implementation-artifacts/3-9-snapshot-creation-at-configurable-intervals.md]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

N/A -- no debug issues encountered during implementation.

### Completion Notes List

- **Task 4.6 design decision:** Investigated `IDomainProcessor`, `DomainProcessorBase`, and `DaprDomainServiceInvoker`. Chose Option A approach: `ConstructDomainState()` private method in AggregateActor implements AC #11's three cases without modifying the domain service contract. Domain services receive `RehydrationResult` (snapshot+tail), `SnapshotState` (snapshot-only), or `Events` list (full replay) depending on the scenario.
- **Interface change blast radius:** `IEventStreamReader.RehydrateAsync` signature changed from `Task<object?>` to `Task<RehydrationResult?>` with optional `SnapshotRecord?` parameter. Contained to: AggregateActor (1 call site), EventStreamReaderTests, FakeEventStreamReader, integration tests. AggregateActorTests did NOT need explicit updates -- NSubstitute default behavior handles the new signature.
- **Test count:** 687 existing tests + 16 new tests = 703 total. All pass. New tests: 12 unit tests (EventStreamReaderTests), 4 integration tests (SnapshotRehydrationTests).
- **No changes to Client package:** `IDomainProcessor`, `DomainProcessorBase`, and `DaprDomainServiceInvoker` were NOT modified. The domain service contract remains unchanged.

### Change Log

| Date | Change | Files |
|------|--------|-------|
| 2026-02-14 | Created RehydrationResult model (AC #12) | `src/Hexalith.EventStore.Server/Events/RehydrationResult.cs` |
| 2026-02-14 | Updated IEventStreamReader interface with snapshot parameter (AC #1, #4, #6) | `src/Hexalith.EventStore.Server/Events/IEventStreamReader.cs` |
| 2026-02-14 | Implemented snapshot-aware EventStreamReader (AC #1, #3, #4, #5, #7, #8, #9) | `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs` |
| 2026-02-14 | Updated AggregateActor Step 3 snapshot-first flow and Step 4 ConstructDomainState (AC #6, #10, #11) | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` |
| 2026-02-14 | Created FakeEventStreamReader test double | `src/Hexalith.EventStore.Testing/Fakes/FakeEventStreamReader.cs` |
| 2026-02-14 | Added 12 snapshot-aware unit tests + updated existing tests for new return type | `tests/Hexalith.EventStore.Server.Tests/Events/EventStreamReaderTests.cs` |
| 2026-02-14 | Created 4 snapshot rehydration integration tests | `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotRehydrationTests.cs` |
| 2026-02-14 | Updated integration tests for RehydrationResult return type | `tests/Hexalith.EventStore.IntegrationTests/Events/EventPersistenceIntegrationTests.cs` |
| 2026-02-14 | Updated multi-tenant integration tests for RehydrationResult return type | `tests/Hexalith.EventStore.IntegrationTests/Security/MultiTenantStorageIsolationTests.cs` |

### File List

**New files:**
- `src/Hexalith.EventStore.Server/Events/RehydrationResult.cs` -- immutable record separating snapshot state from tail events
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventStreamReader.cs` -- test double for IEventStreamReader with snapshot support
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotRehydrationTests.cs` -- 4 integration tests for snapshot-based rehydration

**Modified files:**
- `src/Hexalith.EventStore.Server/Events/IEventStreamReader.cs` -- updated signature: `Task<RehydrationResult?> RehydrateAsync(AggregateIdentity, SnapshotRecord?)`
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs` -- snapshot-aware rehydration with three paths (snapshot+tail, snapshot-only, full-replay)
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- Step 3 snapshot-first flow, Step 4 ConstructDomainState, lastSnapshotSequence from RehydrationResult
- `tests/Hexalith.EventStore.Server.Tests/Events/EventStreamReaderTests.cs` -- 12 new tests + existing tests updated for RehydrationResult
- `tests/Hexalith.EventStore.IntegrationTests/Events/EventPersistenceIntegrationTests.cs` -- updated for RehydrationResult return type
- `tests/Hexalith.EventStore.IntegrationTests/Security/MultiTenantStorageIsolationTests.cs` -- updated for RehydrationResult return type
