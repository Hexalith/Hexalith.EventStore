# Story 16.1: EventStoreAggregate Base Class

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a domain service developer,
I want to implement event-sourced aggregates by inheriting from `EventStoreAggregate` with typed `Apply` methods,
so that I can focus on pure domain logic without manually implementing `IDomainProcessor`, deserializing commands, or managing state rehydration boilerplate.

## Acceptance Criteria

1. **AC1 — Base class exists:** An abstract generic `EventStoreAggregate<TState>` class (constrained `where TState : class, new()`) exists in `Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs` that implements `IDomainProcessor` internally. The `new()` constraint is required because state rehydration via event replay must instantiate an empty `TState` to apply events against.

2. **AC2 — Typed command dispatch via reflection:** The base class automatically dispatches incoming `CommandEnvelope` to a `Handle(TCommand command, TState? state)` method on the concrete aggregate, matching by `command.CommandType` against declared `Handle` method parameter types. The developer writes strongly-typed Handle methods instead of string-matching on command type names.

3. **AC3 — State rehydration via Apply methods:** The base class rehydrates aggregate state by instantiating `TState` (parameterless constructor) and calling `Apply(TEvent)` methods on it for each historical event, matching event types by reflection. This mirrors the existing `CounterState.Apply(...)` pattern.

4. **AC4 — DomainResult production:** Handle methods return `DomainResult` (success, rejection, or no-op) using the existing `DomainResult.Success()`, `DomainResult.Rejection()`, and `DomainResult.NoOp()` factory methods. No new result types introduced.

5. **AC5 — IDomainProcessor bridge:** `EventStoreAggregate.ProcessAsync(CommandEnvelope, object?)` bridges to the typed Handle methods by: (a) rehydrating state from `object?` (handling `null`, `TState`, `JsonElement`, and event-replay scenarios), (b) deserializing the command payload, (c) invoking the matching Handle method.

6. **AC6 — EventStoreProjection<TReadModel> base class:** An abstract `EventStoreProjection<TReadModel>` class exists in `Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs` that provides a similar typed `Apply(TEvent)` pattern for read-model projections.

7. **AC7 — Backward compatibility:** The existing `IDomainProcessor` interface, `DomainProcessorBase<TState>`, and `EventStoreServiceCollectionExtensions.AddEventStoreClient<TProcessor>()` remain unchanged. No existing public API breaks.

8. **AC8 — Zero DAPR dependency in base class:** `EventStoreAggregate` and `EventStoreProjection<T>` reference only `Hexalith.EventStore.Contracts` types (`CommandEnvelope`, `DomainResult`, `IEventPayload`, `IRejectionEvent`). No DAPR-specific types in the base class itself.

## Tasks / Subtasks

- [x] Task 1: Create `EventStoreAggregate` abstract base class (AC: #1, #2, #3, #4, #5, #8)
  - [x] 1.1: Create `src/Hexalith.EventStore.Client/Aggregates/` folder
  - [x] 1.2: Implement `EventStoreAggregate<TState> where TState : class, new()` abstract class implementing `IDomainProcessor`
  - [x] 1.3: Implement reflection-based command dispatch (scan for `Handle(TCommand, TState?)` methods)
  - [x] 1.4: Implement state rehydration from `object?` → `TState` (null, typed, JsonElement, event array)
  - [x] 1.5: Implement command payload deserialization (`JsonSerializer.Deserialize<T>(command.Payload)`)
  - [x] 1.6: Cache reflection metadata per aggregate type for performance (static `ConcurrentDictionary<Type, ...>` — must be thread-safe for concurrent first invocations)
- [x] Task 2: Create `EventStoreProjection<TReadModel>` abstract base class (AC: #6, #8)
  - [x] 2.1: Implement `EventStoreProjection<TReadModel>` in `Aggregates/EventStoreProjection.cs`
  - [x] 2.2: Provide typed `Apply(TEvent)` method discovery pattern (same reflection cache approach)
- [x] Task 3: Verify backward compatibility (AC: #7)
  - [x] 3.1: Verify `IDomainProcessor` interface is unchanged
  - [x] 3.2: Verify `DomainProcessorBase<TState>` is unchanged
  - [x] 3.3: Verify `AddEventStoreClient<TProcessor>()` continues to work with both old and new patterns
  - [x] 3.4: Ensure all existing tests pass with zero modifications

## Dev Notes

### Architecture Constraints

- **Target framework:** net10.0 with `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`
- **Project:** `Hexalith.EventStore.Client` — depends only on `Hexalith.EventStore.Contracts` and `Dapr.Client`
- **Namespace:** `Hexalith.EventStore.Client.Aggregates`
- **One public type per file** — `EventStoreAggregate.cs` and `EventStoreProjection.cs`
- **No new NuGet dependencies** — only `System.Text.Json` (implicit via framework) and existing Contracts types

### Design Decisions

**EventStoreAggregate is abstract, not sealed:**
The class is designed as a base class that concrete aggregates inherit from. It bridges the gap between the developer's typed Handle/Apply methods and the infrastructure's `IDomainProcessor.ProcessAsync(CommandEnvelope, object?)` contract.

**Single-threaded per aggregate instance — NO instance-level synchronization needed:**
The server-side DAPR actor (`AggregateActor`) guarantees that commands for a given aggregate ID are processed sequentially in chronological order. `ProcessAsync` is never called concurrently on the same aggregate instance. Do NOT add `lock`, `SemaphoreSlim`, `volatile`, or any instance-level thread safety — it would be dead code. The only concurrency concern is the static reflection cache (different aggregate types may initialize concurrently during app startup), which is handled by `ConcurrentDictionary`.

**Reflection-based dispatch with caching (deliberate trade-off):**
Handle methods are discovered via reflection at first use and cached in a static `ConcurrentDictionary<Type, ...>` per aggregate type. This is a deliberate Phase 1 trade-off — source generators would eliminate reflection entirely but triple the story scope. The cached reflection approach is the "boring technology" choice: simple, well-understood, and sufficient for the expected throughput. The cache maps `string commandTypeName → (MethodInfo handleMethod, Type commandParameterType, bool isAsync)`.

**State rehydration follows CounterProcessor pattern:**
The existing `CounterProcessor` handles 4 state shapes: `null`, `CounterState`, `JsonElement`, and `IEnumerable` (event replay). The base class must handle the same shapes generically:
- `null` → `default(TState)` (new aggregate)
- `TState` → use directly
- `JsonElement` (object shape) → `JsonSerializer.Deserialize<TState>(jsonElement)`
- `JsonElement` (array shape) or `IEnumerable` → replay events through Apply methods to reconstruct state

**Apply method signature convention:**
State classes declare `public void Apply(TEvent e)` methods where `TEvent` implements `IEventPayload`. The base class discovers these via reflection and calls them during state rehydration.

**Handle method signature convention:**
Aggregate classes declare methods matching one of:
- `DomainResult Handle(TCommand command, TState? state)` (sync)
- `Task<DomainResult> Handle(TCommand command, TState? state)` (async)
Where `TCommand` is the deserialized command type, matched by `command.CommandType == typeof(TCommand).Name`.

**CRITICAL — Sync/async return type detection in reflection dispatch:**
The reflection cache must store the return type of each Handle method. When dispatching, check if the return type is `Task<DomainResult>` — if so, `await` the result. If it is `DomainResult` (sync), wrap in `Task.FromResult`. The dispatch code must handle both paths correctly:
```csharp
// Pseudocode for dispatch logic:
object result = handleMethod.Invoke(this, new object?[] { command, state });
return result switch {
    Task<DomainResult> asyncResult => await asyncResult,
    DomainResult syncResult => syncResult,
    _ => throw new InvalidOperationException(...)
};
```
Failing to detect the return type will cause `InvalidCastException` at runtime for async Handle methods.

**EventStoreProjection<TReadModel> is simpler:**
Projections only need Apply methods (no Handle/command processing). They receive events and update a read model. The base class provides the Apply discovery pattern but does NOT implement `IDomainProcessor`.

### Key Interfaces & Types to Reference

| Type | Location | Role |
|------|----------|------|
| `IDomainProcessor` | `Client/Handlers/IDomainProcessor.cs` | Interface that `EventStoreAggregate` must implement |
| `DomainProcessorBase<TState>` | `Client/Handlers/DomainProcessorBase.cs` | Existing simpler base class — must remain untouched |
| `CommandEnvelope` | `Contracts/Commands/CommandEnvelope.cs` | Input to ProcessAsync — has `CommandType`, `Payload` (byte[]) |
| `DomainResult` | `Contracts/Results/DomainResult.cs` | Output of ProcessAsync — `Success()`, `Rejection()`, `NoOp()` |
| `IEventPayload` | `Contracts/Events/IEventPayload.cs` | Marker interface for all domain events |
| `IRejectionEvent` | `Contracts/Events/IRejectionEvent.cs` | Marker interface for rejection events |
| `CounterState` | `samples/.../Counter/State/CounterState.cs` | Reference pattern for Apply methods |
| `CounterProcessor` | `samples/.../Counter/CounterProcessor.cs` | Reference pattern this base class replaces |

### Expected File Structure After Implementation

```
src/Hexalith.EventStore.Client/
├── Aggregates/                              # NEW folder
│   ├── EventStoreAggregate.cs              # NEW — abstract base class
│   └── EventStoreProjection.cs             # NEW — projection base class
├── Handlers/
│   ├── IDomainProcessor.cs                 # UNCHANGED
│   └── DomainProcessorBase.cs              # UNCHANGED
└── Registration/
    └── EventStoreServiceCollectionExtensions.cs  # UNCHANGED
```

### API Shape (Expected Usage After This Story)

```csharp
// Developer writes this:
public class CounterAggregate : EventStoreAggregate<CounterState>
{
    public DomainResult Handle(IncrementCounter command, CounterState? state)
        => DomainResult.Success(new IEventPayload[] { new CounterIncremented() });

    public DomainResult Handle(DecrementCounter command, CounterState? state)
    {
        if ((state?.Count ?? 0) == 0)
            return DomainResult.Rejection(new IRejectionEvent[] { new CounterCannotGoNegative() });
        return DomainResult.Success(new IEventPayload[] { new CounterDecremented() });
    }

    public DomainResult Handle(ResetCounter command, CounterState? state)
    {
        if ((state?.Count ?? 0) == 0)
            return DomainResult.NoOp();
        return DomainResult.Success(new IEventPayload[] { new CounterReset() });
    }
}

// State class (same pattern as today):
public sealed class CounterState
{
    public int Count { get; private set; }
    public void Apply(CounterIncremented e) => Count++;
    public void Apply(CounterDecremented e) => Count--;
    public void Apply(CounterReset e) => Count = 0;
}

// Registration (works with existing AddEventStoreClient):
builder.Services.AddEventStoreClient<CounterAggregate>();
```

### Project Structure Notes

- New `Aggregates/` folder follows the architecture document's prescribed structure for `Hexalith.EventStore.Client`
- File naming follows existing convention: one public type per file, file name = type name
- Namespace follows existing pattern: `Hexalith.EventStore.Client.Aggregates`
- No conflicts with existing code — purely additive

### Testing Expectations

- Unit tests for `EventStoreAggregate` should go in `tests/Hexalith.EventStore.Client.Tests/`
- Test naming: `EventStoreAggregateTests.cs` with method pattern `{Method}_{Scenario}_{ExpectedResult}`
- Use existing test infrastructure: xunit, Shouldly assertions, builder pattern from Testing package
- Key test scenarios:
  - Command dispatch to correct Handle method
  - Unknown command type throws `InvalidOperationException`
  - State rehydration from null, typed state, JsonElement object, JsonElement array
  - Handle method returning Success, Rejection, NoOp
  - Sync Handle method returns `DomainResult` correctly
  - Async Handle method (`Task<DomainResult>`) awaits and returns correctly
  - Mixed sync/async Handle methods on same aggregate work correctly
  - Reflection cache populated correctly
  - Multiple aggregate types don't interfere with each other's caches
  - **Static cache independence:** Multiple different aggregate types initializing their reflection caches concurrently (e.g., during app startup) must not interfere with each other. Note: same-type concurrent access is not a production scenario (actor guarantees sequential per-aggregate-ID), but the `ConcurrentDictionary` handles it defensively
  - **Unmatched command type on aggregate with Handle methods:** Aggregate has Handle methods but none match the dispatched `CommandType` — must throw `InvalidOperationException` with clear message naming the unmatched type

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-02-28.md#Section 4.3] — Epic 16 story definition
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-02-28.md#Section 4.2] — Architecture changes for Client package structure
- [Source: _bmad-output/planning-artifacts/architecture.md#Convention Naming Patterns] — Naming convention engine design
- [Source: _bmad-output/brainstorming/brainstorming-session-2026-02-28.md#Priority 1] — Zero-config quickstart action plan
- [Source: _bmad-output/planning-artifacts/prd.md#FR48] — FR48: EventStoreAggregate with typed Apply methods
- [Source: src/Hexalith.EventStore.Client/Handlers/IDomainProcessor.cs] — Interface to implement
- [Source: src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs] — Existing base class pattern
- [Source: samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs] — State rehydration reference
- [Source: samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs] — Apply method pattern reference

## Change Log

- 2026-02-28: Implemented `EventStoreAggregate&lt;TState&gt;` and `EventStoreProjection&lt;TReadModel&gt;` base classes with reflection-based dispatch, state rehydration, and comprehensive unit tests. All existing tests pass with zero modifications.
- 2026-02-28: Senior code review follow-up fixes applied automatically: hardened JsonElement object state rehydration, fail-fast behavior for unknown replay events, stricter enumerable state handling, and backward-compatibility registration coverage for `EventStoreAggregate&lt;TState&gt;` with `AddEventStoreClient&lt;TProcessor&gt;()`.
- 2026-02-28: Adversarial review remediation pass: enforced fail-fast handling for malformed historical JSON event entries and payload deserialization errors, added concurrent first-use metadata cache test coverage, hardened projection replay behavior, and synchronized story File List with actual changed files.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- CA1822 fix: Handle methods discovered via reflection can be either instance or static. Updated DiscoverHandleMethods to scan `BindingFlags.Static | BindingFlags.Instance` and dispatch accordingly. This accommodates CA1822 analyzer compliance for Handle methods that don't access instance state.
- JsonElement object deserialization: `private set` properties on state classes are not settable by `System.Text.Json` default deserialization. This is an inherent limitation — real aggregates typically use event replay for state reconstruction, not JSON object deserialization.

### Completion Notes List

- Task 1: Created `EventStoreAggregate<TState>` in `Aggregates/EventStoreAggregate.cs`. Implements `IDomainProcessor` via `ProcessAsync`. Supports both sync (`DomainResult`) and async (`Task<DomainResult>`) Handle methods, both instance and static. Reflection metadata cached in `ConcurrentDictionary<Type, AggregateMetadata>`. State rehydration handles null, TState, JsonElement (object/array/null), and IEnumerable.
- Task 2: Created `EventStoreProjection<TReadModel>` in `Aggregates/EventStoreProjection.cs`. Provides `Project(IEnumerable)` and `ProjectFromJson(JsonElement)` methods with same reflection-cached Apply discovery pattern.
- Task 3: Verified backward compatibility — `IDomainProcessor`, `DomainProcessorBase<TState>`, and `AddEventStoreClient<TProcessor>()` are completely unchanged. All 249 existing unit tests pass (Client: 36, Contracts: 157, Sample: 8, Testing: 48). 23 new tests added (16 aggregate + 7 projection).
- Review Fix 1 (HIGH): Replaced fragile JsonElement object deserialization path with reflection-driven property hydration so private-set state properties are correctly rehydrated.
- Review Fix 2 (HIGH): Aggregate replay no longer silently skips unknown events; unknown event types now throw `InvalidOperationException` with clear context.
- Review Fix 3 (MEDIUM): Restricted non-typed state replay path by excluding string state values from IEnumerable replay handling.
- Review Fix 4 (MEDIUM): Added registration compatibility test proving `AddEventStoreClient&lt;TProcessor&gt;()` works with new `EventStoreAggregate&lt;TState&gt;` implementations.
- Review Fix 5 (HIGH): Aggregate replay now fails fast on malformed JSON historical entries (non-object items, missing/blank `eventTypeName`) instead of silently skipping.
- Review Fix 6 (HIGH): Aggregate replay now wraps payload deserialization failures in `InvalidOperationException` with explicit state/event context.
- Review Fix 7 (MEDIUM): Projection replay now fails fast for malformed/unknown events and payload deserialization failures.
- Review Fix 8 (MEDIUM): Added concurrent first-use cache initialization test to validate static metadata cache independence under parallel startup-like conditions.
- Review Fix 9 (MEDIUM): Story File List updated to include additional changed source/test files present in git.
- Verification: `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj` passed (87/87).

### File List

- src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs (NEW)
- src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs (NEW)
- src/Hexalith.EventStore.Client/Attributes/EventStoreDomainAttribute.cs (NEW)
- src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs (NEW)
- src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj (MODIFIED)
- tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs (NEW)
- tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreProjectionTests.cs (NEW)
- tests/Hexalith.EventStore.Client.Tests/Attributes/EventStoreDomainAttributeTests.cs (NEW)
- tests/Hexalith.EventStore.Client.Tests/Conventions/NamingConventionEngineTests.cs (NEW)
- tests/Hexalith.EventStore.Client.Tests/Registration/ServiceCollectionExtensionsTests.cs (MODIFIED)

## Senior Developer Review (AI)

### Reviewer

GitHub Copilot (GPT-5.3-Codex)

### Outcome

Approve — all identified HIGH and MEDIUM review issues were fixed in code and validated by tests.

### Review Notes

- Fixed state rehydration reliability for JsonElement object input by using explicit property mapping on `TState`.
- Enforced deterministic replay by failing fast when an event cannot be mapped to an `Apply` method.
- Tightened state-shape handling by preventing string values from being treated as event streams.
- Added backward-compatibility test proving registration path works with both legacy `DomainProcessorBase&lt;TState&gt;` and new `EventStoreAggregate&lt;TState&gt;` implementations.
- Enforced fail-fast behavior for malformed JSON historical event entries and payload deserialization failures during aggregate state replay.
- Enforced fail-fast behavior for malformed/unknown events during projection replay.
- Added concurrent first-use cache initialization test coverage for aggregate metadata reflection cache.
