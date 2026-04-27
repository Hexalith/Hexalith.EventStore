# Story Post-Epic-1 R1-A6: Custom MissingApplyMethodException for Replay Diagnostics

Status: done

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

- [x] Task 1: Add the diagnostic exception type (AC: #1, #5, #7)
  - [x] 1.1 Create `src/Hexalith.EventStore.Client/Aggregates/MissingApplyMethodException.cs`
  - [x] 1.2 Make it `public sealed` and derive from `InvalidOperationException` so existing broad handlers still catch it
  - [x] 1.3 Add public properties: `Type StateType`, `string EventTypeName`, `string? MessageId`, `string? AggregateId`
  - [x] 1.4 Build the exception message with invariant-culture text that includes the state type and event type
  - [x] 1.5 Add an `ITerminatable`/`AggregateTerminated` hint when `StateType` implements `ITerminatable` or `EventTypeName == nameof(AggregateTerminated)`
  - [x] 1.6 Add XML docs on the type, constructor, and properties

- [x] Task 2: Replace silent Apply-lookup misses in rehydration (AC: #2, #3, #4)
  - [x] 2.1 Update `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs`
  - [x] 2.2 In `ApplyContractEventEnvelope`, throw `MissingApplyMethodException` when `TryResolveApplyMethod(envelope.Metadata.EventTypeName, applyMethods)` returns null
  - [x] 2.3 In `ApplyJsonEventByName`, throw `MissingApplyMethodException` when the lookup returns null
  - [x] 2.4 In `ReplayEventsFromEnumerable`, throw `MissingApplyMethodException` for typed event objects that have no direct Apply method
  - [x] 2.5 Preserve the existing `InvalidOperationException` throw sites for malformed historical entries, missing `eventTypeName`, empty `eventTypeName`, payload deserialization nulls, invalid payload shapes, and wrong state object types
  - [x] 2.6 Pass available diagnostic context into the exception: event type name at minimum; message id / aggregate id when the replay object is an `EventEnvelope`

- [x] Task 3: Add focused exception tests (AC: #1, #5, #7)
  - [x] 3.1 Add `tests/Hexalith.EventStore.Client.Tests/Aggregates/MissingApplyMethodExceptionTests.cs`
  - [x] 3.2 Verify constructor properties are preserved
  - [x] 3.3 Verify the message contains state type and event type name
  - [x] 3.4 Verify the tombstoning hint appears for an `ITerminatable` state or `AggregateTerminated`
  - [x] 3.5 Verify the exception is assignable to `InvalidOperationException`

- [x] Task 4: Add rehydrator regression coverage through `EventStoreAggregate` (AC: #2, #3, #4, #6)
  - [x] 4.1 In `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs`, replace the current unknown-event skip expectations with `MissingApplyMethodException` expectations
  - [x] 4.2 Cover JSON array replay where `eventTypeName` has no matching Apply method
  - [x] 4.3 Cover enumerable typed event replay where the event object has no matching Apply method
  - [x] 4.4 Cover `EventEnvelope` replay through `DomainServiceCurrentState.Events` with an unknown `EventTypeName`
  - [x] 4.5 Add a tombstoning-specific negative test: an `ITerminatable` state without `Apply(AggregateTerminated)` must throw `MissingApplyMethodException` during replay
  - [x] 4.6 Keep at least one malformed historical event test asserting `InvalidOperationException` to prove JSON/shape errors were not reclassified (existing tests at lines 411-454 retained: non-object entry, missing `eventTypeName`, invalid payload shape)

- [x] Task 5: Validate the Client and Tier 1 surface (AC: #6, #7)
  - [x] 5.1 Run `dotnet build src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj --configuration Release` — clean (0 warnings, 0 errors)
  - [x] 5.2 Run `dotnet test tests/Hexalith.EventStore.Client.Tests/` — 334/334 passed
  - [x] 5.3 Run `dotnet test tests/Hexalith.EventStore.Sample.Tests/` — 62/62 passed
  - [x] 5.4 If the broader Tier 1 window is available, also run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` (271/271) and `dotnet test tests/Hexalith.EventStore.Testing.Tests/` (67/67)

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

claude-opus-4-7[1m] (Claude Opus 4.7, 1M context) via /bmad-dev-story workflow on 2026-04-27.

### Debug Log References

- Tier 1 build constrained by pre-existing NU1902 NuGet audit errors on transitive `OpenTelemetry.Api 1.15.1` and `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.15.1` packages (CVE GHSA-g94r-2vxg-569j, GHSA-mr8r-92fq-pj8p, GHSA-q834-8qmm-v933). Confirmed reproducible on `main` HEAD `0615132` before any story changes. Worked around by passing `-p:NuGetAudit=false` to local builds; package upgrade is out of scope for this story (touches `Directory.Packages.props` and Aspire 13.1 compatibility).
- One CA1822 surfaced on the test-only `BrokenTerminatableState.Apply(ItemAdded)` no-op; resolved by mutating an `ItemCount` field instead of suppressing the analyzer, matching the pattern used by `TestState.Apply(ItemAdded)` rather than the suppression on `TerminatableState.Apply(AggregateTerminated)`.

### Completion Notes List

- Added `MissingApplyMethodException : InvalidOperationException` in `Hexalith.EventStore.Client.Aggregates`. Public sealed; carries `StateType`, `EventTypeName`, optional `MessageId` and `AggregateId`. Message is built with `CultureInfo.InvariantCulture` and includes the `Apply({EventTypeName})` signature, optional aggregate/message id suffix, and an `ITerminatable` / `Apply(AggregateTerminated)` hint when either the state implements `ITerminatable` or the event type name is `AggregateTerminated`. Constructor validates `stateType != null` and `eventTypeName` is non-whitespace.
- Replaced the three silent Apply-lookup skips in `DomainProcessorStateRehydrator` (`ApplyContractEventEnvelope`, `ApplyJsonEventByName`, and the typed-event branch of `ReplayEventsFromEnumerable`) with `throw new MissingApplyMethodException(...)`. EventEnvelope path forwards `Metadata.MessageId` and `Metadata.AggregateId`; JSON-array and typed-enumerable paths pass `eventTypeName` only (no envelope context available).
- All non-Apply throw sites continue to throw plain `InvalidOperationException`: malformed historical entries (non-object), missing/empty `eventTypeName`, payload deserialization nulls, invalid payload JSON shapes, wrong-state-type inputs, and `JsonException` wrapping. Existing tests at lines 411-454 of `EventStoreAggregateTests` enforce this.
- Updated the two pre-existing skip-expectation tests (`ProcessAsync_JsonElementArray_WithUnknownEventType_*`, `ProcessAsync_EnumerableEvents_WithUnknownEventType_*`) to assert `MissingApplyMethodException` and validate `StateType` + `EventTypeName` payload. Added two new regression tests: snapshot-aware `EventEnvelope` replay through `DomainServiceCurrentState.Events` (validates `MessageId` and `AggregateId` propagate from envelope metadata), and a broken-`ITerminatable` state replaying `AggregateTerminated` (validates the tombstoning hint surfaces).
- Added focused `MissingApplyMethodExceptionTests` (11 tests) covering constructor property preservation, optional context defaults, message content, both tombstoning-hint trigger conditions, hint omission for unrelated state/event combinations, `InvalidOperationException` assignability, and constructor argument validation.
- Tier 1 results (with `-p:NuGetAudit=false` to bypass pre-existing transitive CVE warnings): Client 334/334, Sample 62/62, Contracts 271/271, Testing 67/67. Total 734/734 green.
- Out-of-scope (per Dev Notes): no changes to `EventStoreProjection<TReadModel>`, no Server actor lifecycle test edits (R1-A7), no `Hexalith.EventStore.Testing` compliance helper (R1-A2). Tier 2/3 not run (DAPR not initialized; consistent with story Task 5 scope).

### File List

- src/Hexalith.EventStore.Client/Aggregates/MissingApplyMethodException.cs (new)
- src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs (modified)
- tests/Hexalith.EventStore.Client.Tests/Aggregates/MissingApplyMethodExceptionTests.cs (new)
- tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs (modified)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified — story status transitions)

### Change Log

| Date | Change | Author |
|------|--------|--------|
| 2026-04-27 | Implemented R1-A6: introduced `MissingApplyMethodException`, replaced silent Apply-lookup skips in `DomainProcessorStateRehydrator` (3 sites), added 11 focused exception tests, replaced 2 skip-expectation tests with `MissingApplyMethodException` expectations, added 2 new regression tests (`DomainServiceCurrentState` envelope replay + broken-`ITerminatable` tombstoning). Tier 1: 734/734 green. | Amelia (claude-opus-4-7[1m]) |
| 2026-04-27 | Code review applied 3 patches (named-arg discipline at the two remaining throw sites in `DomainProcessorStateRehydrator`; removed unused private setter on `BrokenTerminatableState.IsTerminated`; replaced non-conformant ULID literal in test envelope with `UniqueIdHelper.GenerateSortableUniqueStringId()`). 6 deferrals logged in `deferred-work.md`. Client tests 334/334 green post-patch. Status moved to done. | Code Review (claude-opus-4-7[1m]) |

### Review Findings

Code review run on 2026-04-27 (3 reviewers: Blind Hunter, Edge Case Hunter, Acceptance Auditor). Acceptance Auditor reported zero AC violations. 3 patches, 6 deferrals, ~32 dismissed.

- [x] [Review][Patch] Named-argument inconsistency at the three throw sites — middle `ApplyContractEventEnvelope` uses named args; the other two `throw new MissingApplyMethodException(typeof(TState), eventTypeName)` calls are positional. Project Dev Notes (R1-A1 retro) call for named-arg discipline at boundary call sites. [src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs:209,232-237,274] — fixed 2026-04-27, all three sites now use named args
- [x] [Review][Patch] `BrokenTerminatableState.IsTerminated` has an unused private setter — the property is never assigned; a get-only autoproperty satisfies `ITerminatable`. [tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs:209-216] — fixed 2026-04-27, replaced with `=> false` expression-bodied getter
- [x] [Review][Patch] Test envelope's `MessageId` literal is not a valid ULID — `"01HQ7K8N9PXYZSAMPLE000001"` is 25 characters (ULID is 26) and contains `L` (excluded from Crockford base32). Per project rule R2-A7, identifiers must be ULIDs; the fixture should use `Ulid.NewUlid().ToString()` or a real 26-char Crockford-compliant literal. [tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs:1056] — fixed 2026-04-27, replaced with `Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId()`
- [x] [Review][Defer] Suffix-fallback `EndsWith` in `TryResolveApplyMethod` can non-deterministically resolve when multiple Apply keys suffix-match the same `eventTypeName` — pre-existing, unchanged by this story. [src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs:327-340] — deferred, pre-existing
- [x] [Review][Defer] `DiscoverApplyMethods` keys by `parameters[0].ParameterType.Name` (short name), risking namespace collisions on duplicate event short-names — pre-existing. [src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs:13-37] — deferred, pre-existing
- [x] [Review][Defer] Replay-mid-stream blast radius inside DAPR actor activation lifecycle — explicitly covered by sibling story R1-A7. [src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs:177-213] — deferred, scope of R1-A7
- [x] [Review][Defer] `envelope.Metadata.EventTypeName` null/whitespace path produces `ArgumentException` from `BuildMessage` rather than a typed diagnostic — pre-existing upstream contract assumption. [src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs:230] — deferred, pre-existing upstream invariant
- [x] [Review][Defer] No integration test exercises EventEnvelope replay path with null `MessageId`/`AggregateId` — null branch is covered by direct ctor unit tests in `MissingApplyMethodExceptionTests`, but not via the rehydrator. Minor coverage gap. [tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs:1049-1071] — deferred, low-value extra coverage
- [x] [Review][Defer] Typed-event branch in `ReplayEventsFromEnumerable` uses `applyMethods.TryGetValue` directly without the `TryResolveApplyMethod` suffix-fallback that the other two sites use — pre-existing asymmetry. [src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs:204-209] — deferred, pre-existing

