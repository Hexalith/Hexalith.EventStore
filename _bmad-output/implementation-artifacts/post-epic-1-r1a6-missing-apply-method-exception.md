# Story Post-Epic-1 R1-A6: Custom MissingApplyMethodException for Replay Diagnostics

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform operator,
I want missing state `Apply(...)` methods during aggregate replay to throw a specific diagnostic exception,
so that tombstoning and event-contract replay faults are visible, searchable, and distinguishable from JSON shape or infrastructure errors.

## Acceptance Criteria

1. `MissingApplyMethodException` exists in `Hexalith.EventStore.Client.Aggregates`, derives from `InvalidOperationException`, and exposes structured payload properties for the state type, event type name, optional message id, and optional aggregate id.

2. `DomainProcessorStateRehydrator` throws `MissingApplyMethodException` whenever replay encounters an event entry with no matching public instance `void Apply(TEvent)` method on the state type.

3. Existing JSON shape, invalid payload, deserialization, and wrong-state-type failures continue to throw `InvalidOperationException`; only Apply-lookup misses use the new exception.

4. The current silent-skip behavior for unknown replay events is removed from aggregate state rehydration. Rehydration must not silently ignore event stream entries that cannot be applied.

5. `MissingApplyMethodException` messages include enough context for operators and domain developers to diagnose the fault: state type, event type name, and an `ITerminatable`/`AggregateTerminated` hint when applicable.

6. Client tests cover the new exception type, all replay input shapes that can hit an Apply-lookup miss, and at least one non-Apply failure path that must remain `InvalidOperationException`.

7. Public API XML documentation is complete for the new exception and the Client project still builds with XML documentation warnings treated as errors.

## Tasks / Subtasks

- [ ] Task 1: Add the diagnostic exception type (AC: #1, #5, #7)
  - [ ] 1.1 Create `src/Hexalith.EventStore.Client/Aggregates/MissingApplyMethodException.cs`
  - [ ] 1.2 Make it `public sealed` and derive from `InvalidOperationException` so existing broad handlers still catch it
  - [ ] 1.3 Add public properties: `Type StateType`, `string EventTypeName`, `string? MessageId`, `string? AggregateId`
  - [ ] 1.4 Build the exception message with invariant-culture text that includes the state type and event type
  - [ ] 1.5 Add an `ITerminatable`/`AggregateTerminated` hint when `StateType` implements `ITerminatable` or `EventTypeName == nameof(AggregateTerminated)`
  - [ ] 1.6 Add XML docs on the type, constructor, and properties

- [ ] Task 2: Replace silent Apply-lookup misses in rehydration (AC: #2, #3, #4)
  - [ ] 2.1 Update `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs`
  - [ ] 2.2 In `ApplyContractEventEnvelope`, throw `MissingApplyMethodException` when `TryResolveApplyMethod(envelope.Metadata.EventTypeName, applyMethods)` returns null
  - [ ] 2.3 In `ApplyJsonEventByName`, throw `MissingApplyMethodException` when the lookup returns null
  - [ ] 2.4 In `ReplayEventsFromEnumerable`, throw `MissingApplyMethodException` for typed event objects that have no direct Apply method
  - [ ] 2.5 Preserve the existing `InvalidOperationException` throw sites for malformed historical entries, missing `eventTypeName`, empty `eventTypeName`, payload deserialization nulls, invalid payload shapes, and wrong state object types
  - [ ] 2.6 Pass available diagnostic context into the exception: event type name at minimum; message id / aggregate id when the replay object is an `EventEnvelope`

- [ ] Task 3: Add focused exception tests (AC: #1, #5, #7)
  - [ ] 3.1 Add `tests/Hexalith.EventStore.Client.Tests/Aggregates/MissingApplyMethodExceptionTests.cs`
  - [ ] 3.2 Verify constructor properties are preserved
  - [ ] 3.3 Verify the message contains state type and event type name
  - [ ] 3.4 Verify the tombstoning hint appears for an `ITerminatable` state or `AggregateTerminated`
  - [ ] 3.5 Verify the exception is assignable to `InvalidOperationException`

- [ ] Task 4: Add rehydrator regression coverage through `EventStoreAggregate` (AC: #2, #3, #4, #6)
  - [ ] 4.1 In `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs`, replace the current unknown-event skip expectations with `MissingApplyMethodException` expectations
  - [ ] 4.2 Cover JSON array replay where `eventTypeName` has no matching Apply method
  - [ ] 4.3 Cover enumerable typed event replay where the event object has no matching Apply method
  - [ ] 4.4 Cover `EventEnvelope` replay through `DomainServiceCurrentState.Events` with an unknown `EventTypeName`
  - [ ] 4.5 Add a tombstoning-specific negative test: an `ITerminatable` state without `Apply(AggregateTerminated)` must throw `MissingApplyMethodException` during replay
  - [ ] 4.6 Keep at least one malformed historical event test asserting `InvalidOperationException` to prove JSON/shape errors were not reclassified

- [ ] Task 5: Validate the Client and Tier 1 surface (AC: #6, #7)
  - [ ] 5.1 Run `dotnet build src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj --configuration Release`
  - [ ] 5.2 Run `dotnet test tests/Hexalith.EventStore.Client.Tests/`
  - [ ] 5.3 Run `dotnet test tests/Hexalith.EventStore.Sample.Tests/`
  - [ ] 5.4 If the broader Tier 1 window is available, also run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` and `dotnet test tests/Hexalith.EventStore.Testing.Tests/`

## Dev Notes

### Scope Summary

This is a focused diagnostics and correctness story in the Client replay path. It adds a public exception type and changes aggregate state rehydration so missing `Apply(...)` methods fail loudly with structured context.

This story must not change command dispatch semantics, projection behavior, DAPR actor orchestration, persistence, or tombstoning product behavior beyond making replay faults explicit.

### Why This Story Exists

Story 1.5 introduced the `ITerminatable` contract and documented a critical runtime obligation: any terminatable state must define a no-op `Apply(AggregateTerminated)` method because rejection events are persisted and replayed.

Epic 1 retrospective action R1-A6 captured the diagnostic gap: a missing Apply method during replay is currently not identifiable as its own fault class. Operators need a discriminator they can alert on, and domain developers need a message that points directly to the missing state method.

### Current Code Reality

The sprint change proposal says the replay path throws generic `InvalidOperationException` for missing Apply-method scenarios. The current source has drifted from that text.

As of story creation, `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs` silently skips missing Apply methods in these paths:

- `ReplayEventsFromEnumerable(...)` for typed event objects
- `ApplyContractEventEnvelope(...)` for `EventEnvelope` replay
- `ApplyJsonEventByName(...)` for JSON-array replay

Those skips are not acceptable for aggregate state rehydration. Event streams are the source of truth; if an event exists in the stream and the state cannot apply it, the aggregate state is not trustworthy. This story should replace those skips with `MissingApplyMethodException`.

Do not apply this rule to `EventStoreProjection<TReadModel>` in this story. Projection unknown-event handling is a separate read-model concern and already has its own exception behavior.

### Exception Contract

Suggested public shape:

```csharp
namespace Hexalith.EventStore.Client.Aggregates;

public sealed class MissingApplyMethodException : InvalidOperationException
{
    public MissingApplyMethodException(
        Type stateType,
        string eventTypeName,
        string? messageId = null,
        string? aggregateId = null)
        : base(...)
    {
        ...
    }

    public Type StateType { get; }

    public string EventTypeName { get; }

    public string? MessageId { get; }

    public string? AggregateId { get; }
}
```

Keep it in the Client package because the failure belongs to `EventStoreAggregate<TState>` and `DomainProcessorStateRehydrator`; do not add a Contracts dependency on Client.

### Apply Discovery Rules to Preserve

`DomainProcessorStateRehydrator.DiscoverApplyMethods(Type stateType)` currently discovers:

- public instance methods only
- method name exactly `Apply`
- exactly one parameter
- return type exactly `void`
- dictionary key from the event CLR type short name

Keep those rules unchanged. The story is about lookup-miss diagnostics, not changing the reflection contract.

### Tombstoning Guardrail

`CounterState` already demonstrates the correct pattern:

- `CounterState : ITerminatable`
- `Apply(CounterClosed)` sets `IsTerminated = true`
- `Apply(AggregateTerminated)` is a no-op

The regression gap is the broken-state case. Add a test-only terminatable state that omits `Apply(AggregateTerminated)` and replay an `AggregateTerminated` event. That must throw `MissingApplyMethodException` with the tombstoning hint.

### Architecture Constraints

- D3: domain rejections are events and are persisted like other events; replay must process the persisted event stream coherently.
- FR66: terminated aggregates reject subsequent commands while the event stream remains immutable and replayable.
- FR48: `EventStoreAggregate<TState>` uses typed Apply methods as the higher-level developer contract.
- Rule 8: event names remain past-tense; do not rename `AggregateTerminated`.
- Rule 11: event store keys and historical events are write-once; never "fix" this by ignoring or mutating historical events.
- UX-DR19: public Client API additions need XML documentation.
- UX-DR20: keep the public surface minimal. The exception is public because domain developers and operators need to catch and identify it.

### Testing Guidance

Prefer tests through `EventStoreAggregate.ProcessAsync(...)` because that is the public developer-facing replay path. Directly testing the internal rehydrator is less valuable unless an existing internal-test pattern is already present.

Update current skip tests instead of adding contradictory coverage:

- `ProcessAsync_JsonElementArray_WithUnknownEventType_SkipsUnknownEvent`
- `ProcessAsync_EnumerableEvents_WithUnknownEventType_SkipsUnknownEvent`

Those names and assertions should change to expect `MissingApplyMethodException`.

Add a `DomainServiceCurrentState`/`EventEnvelope` replay test because snapshot-aware replay is the actor-facing path most likely to contain persisted `AggregateTerminated` rejection events after reactivation.

### Previous Story Intelligence

From Story 1.5:

- `Apply(AggregateTerminated)` is not optional for `ITerminatable` states.
- The highest-risk failure happens after actor deactivation/reactivation, not during the first successful close.
- Rejection events are persisted, so replay must be able to process them like every other historical event.

From `post-epic-1-r1a1-aggregatetype-pipeline`:

- Keep follow-up stories tightly scoped.
- Prefer explicit boundary values and named arguments when adding context-bearing parameters.
- Do not combine post-retro cleanup stories; R1-A2 and R1-A7 depend on this diagnostic improvement but are separate stories.

### File Structure Notes

- New Client exception: `src/Hexalith.EventStore.Client/Aggregates/MissingApplyMethodException.cs`
- Rehydration behavior: `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs`
- New exception tests: `tests/Hexalith.EventStore.Client.Tests/Aggregates/MissingApplyMethodExceptionTests.cs`
- Replay regression tests: `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs`
- Do not edit Server actor lifecycle tests for this story; that is R1-A7.
- Do not add the Testing-package compliance helper; that is R1-A2.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26.md`]
- [Source: `_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md`]
- [Source: `_bmad-output/implementation-artifacts/1-5-commandstatus-enum-and-aggregate-tombstoning.md`]
- [Source: `_bmad-output/implementation-artifacts/post-epic-1-r1a1-aggregatetype-pipeline.md`]
- [Source: `_bmad-output/planning-artifacts/architecture.md`]
- [Source: `_bmad-output/planning-artifacts/prd.md`]
- [Source: `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs`]
- [Source: `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs`]
- [Source: `src/Hexalith.EventStore.Contracts/Aggregates/ITerminatable.cs`]
- [Source: `src/Hexalith.EventStore.Contracts/Events/AggregateTerminated.cs`]
- [Source: `samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs`]
- [Source: `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs`]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
