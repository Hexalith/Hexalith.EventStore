# Story 7.1: Sample Counter Domain Service

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **new developer**,
I want a working sample Counter domain service implementing the pure function programming model with commands (IncrementCounter, DecrementCounter, ResetCounter), events (CounterIncremented, CounterDecremented, CounterReset), a rejection event (CounterCannotGoNegative), and state (CounterState),
so that I have a concrete reference implementation to learn from (FR41).

## Acceptance Criteria

1. **Given** the sample Counter domain service project exists **When** I review the CounterProcessor implementation **Then** it implements `IDomainProcessor` (via `DomainProcessorBase<CounterState>`) with the pure function contract `(Command, CurrentState?) -> List<DomainEvent>`
2. **And** `IncrementCounter` command produces `CounterIncremented` event
3. **And** `DecrementCounter` command produces `CounterDecremented` event (or `CounterCannotGoNegative` rejection if counter is 0)
4. **And** `ResetCounter` command produces `CounterReset` event (or no-op if counter is already 0)
5. **And** `CounterState` tracks the current count value and applies events to reconstruct state
6. **And** the domain service is registered in the DAPR config store for a sample tenant and domain
7. **And** the sample service demonstrates all three D3 outcomes: events (increment/decrement), rejection events (decrement at zero), empty list / no-op (reset when already zero)
8. **And** the sample exposes a DAPR service invocation endpoint (`POST /process`) matching the contract expected by `DaprDomainServiceInvoker`
9. **And** the sample domain service registers via `AddEventStoreClient<CounterProcessor>()` in DI

## Tasks / Subtasks

- [x] Task 1: Create Counter command types (AC: #1, #2, #3, #4)
  - [x] 1.1 Create `samples/Hexalith.EventStore.Sample/Counter/Commands/IncrementCounter.cs` -- record implementing `IEventPayload` or plain record (command payload, not event)
  - [x] 1.2 Create `samples/Hexalith.EventStore.Sample/Counter/Commands/DecrementCounter.cs`
  - [x] 1.3 Create `samples/Hexalith.EventStore.Sample/Counter/Commands/ResetCounter.cs`

- [x] Task 2: Create Counter event types (AC: #2, #3, #4, #7)
  - [x] 2.1 Create `samples/Hexalith.EventStore.Sample/Counter/Events/CounterIncremented.cs` -- record implementing `IEventPayload` (past tense naming per Rule #8)
  - [x] 2.2 Create `samples/Hexalith.EventStore.Sample/Counter/Events/CounterDecremented.cs` -- record implementing `IEventPayload`
  - [x] 2.3 Create `samples/Hexalith.EventStore.Sample/Counter/Events/CounterReset.cs` -- record implementing `IEventPayload`
  - [x] 2.4 Create `samples/Hexalith.EventStore.Sample/Counter/Events/CounterCannotGoNegative.cs` -- record implementing `IRejectionEvent` (past tense negative naming per Rule #8)

- [x] Task 3: Create CounterState aggregate state (AC: #5)
  - [x] 3.1 Create `samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs` -- class with `int Count` property and `Apply()` methods for each event type to reconstruct state from event replay

- [x] Task 4: Create CounterProcessor domain processor (AC: #1, #2, #3, #4, #7)
  - [x] 4.1 Create `samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs` -- extends `DomainProcessorBase<CounterState>`
  - [x] 4.2 Implement command type routing in `HandleAsync`: deserialize `command.Payload` to determine command type, dispatch to typed handlers
  - [x] 4.3 Implement `IncrementCounter` handler: return `DomainResult.Success([new CounterIncremented(...)])`
  - [x] 4.4 Implement `DecrementCounter` handler: if count == 0, return `DomainResult.Rejection([new CounterCannotGoNegative()])`; else return `DomainResult.Success([new CounterDecremented(...)])`
  - [x] 4.5 Implement `ResetCounter` handler: if count == 0, return `DomainResult.NoOp()`; else return `DomainResult.Success([new CounterReset()])`

- [x] Task 5: Update Sample Program.cs (AC: #8, #9)
  - [x] 5.1 Replace the existing placeholder idempotency demo endpoint code in `Program.cs`
  - [x] 5.2 Register `AddEventStoreClient<CounterProcessor>()` in the DI container
  - [x] 5.3 Add `POST /process` endpoint that accepts a `CommandEnvelope` (or deserialized equivalent), resolves `IDomainProcessor`, calls `ProcessAsync`, and returns the `DomainResult` as JSON
  - [x] 5.4 Keep `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()` for health/readiness

- [x] Task 6: DAPR config store registration (AC: #6)
  - [x] 6.1 Document the expected config store entry format for registering the sample: tenant=`sample-tenant`, domain=`counter`, appId=`sample`
  - [x] 6.2 Add configuration in AppHost `DaprComponents/` or document how the domain service resolver finds this service (currently via `DaprClient.InvokeMethodAsync` with appId `sample`)

- [x] Task 7: Unit tests for CounterProcessor (AC: #1, #2, #3, #4, #5, #7)
  - [x] 7.1 Add CounterProcessor unit tests in `tests/Hexalith.EventStore.Contracts.Tests/` (Tier 1, zero DAPR dependency)
  - [x] 7.2 Test: IncrementCounter on null state -> CounterIncremented event
  - [x] 7.3 Test: IncrementCounter on existing state (count=5) -> CounterIncremented event
  - [x] 7.4 Test: DecrementCounter on count=0 -> CounterCannotGoNegative rejection (D3 rejection outcome)
  - [x] 7.5 Test: DecrementCounter on count>0 -> CounterDecremented event
  - [x] 7.6 Test: ResetCounter on count=0 -> DomainResult.NoOp (D3 no-op outcome)
  - [x] 7.7 Test: ResetCounter on count>0 -> CounterReset event (D3 success outcome)
  - [x] 7.8 Test: Unknown command type -> InvalidOperationException

- [x] Task 8: Verify build and all tests pass (AC: all)
  - [x] 8.1 Run `dotnet build` to verify the solution compiles
  - [x] 8.2 Run `dotnet test` to ensure no regressions and new tests pass

## Dev Notes

### Architecture Patterns & Constraints

- **Pure function model (D3, FR21):** `CounterProcessor` must extend `DomainProcessorBase<CounterState>` which provides typed state casting. The domain processor receives `CommandEnvelope` + `CounterState?` and returns `DomainResult`
- **Three D3 outcomes must be demonstrated:**
  1. **Success events:** IncrementCounter -> CounterIncremented, DecrementCounter -> CounterDecremented
  2. **Rejection events:** DecrementCounter when count=0 -> CounterCannotGoNegative (implements `IRejectionEvent`)
  3. **No-op:** ResetCounter when count=0 -> `DomainResult.NoOp()` (empty event list)
- **Event naming (Rule #8):** State-change events in past tense (`CounterIncremented`, `CounterDecremented`, `CounterReset`); rejection events in past tense negative (`CounterCannotGoNegative`)
- **DI registration (Rule #10):** Use `AddEventStoreClient<CounterProcessor>()` extension method
- **Payload deserialization:** `CommandEnvelope.Payload` is `byte[]` and `CommandEnvelope.CommandType` is a string. The processor should switch on `command.CommandType` (e.g., `"IncrementCounter"`, `"DecrementCounter"`, `"ResetCounter"`) and deserialize `command.Payload` via `System.Text.Json.JsonSerializer.Deserialize<T>(command.Payload)` to the appropriate command record. Unknown command types should throw `InvalidOperationException` (infrastructure error per D3)
- **No payload in logs (Rule #5, SEC-5):** Even in the sample, do not log payload data
- **Feature folder structure (Rule #2):** Organize under `Counter/Commands/`, `Counter/Events/`, `Counter/State/`, with `CounterProcessor.cs` at the `Counter/` root

### Existing Code to Leverage

- **`DomainProcessorBase<TState>`** at `src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs` -- provides typed state casting from `object?` to `TState?`
- **`IDomainProcessor`** at `src/Hexalith.EventStore.Client/Handlers/IDomainProcessor.cs` -- the contract interface
- **`DomainResult`** at `src/Hexalith.EventStore.Contracts/Results/DomainResult.cs` -- has `Success()`, `Rejection()`, `NoOp()` factory methods
- **`IEventPayload`** at `src/Hexalith.EventStore.Contracts/Events/IEventPayload.cs` -- marker interface for all events
- **`IRejectionEvent`** at `src/Hexalith.EventStore.Contracts/Events/IRejectionEvent.cs` -- marker interface extending `IEventPayload`
- **`CommandEnvelope`** at `src/Hexalith.EventStore.Contracts/Commands/CommandEnvelope.cs` -- command payload with `Payload` (byte[]), `CommandType` (string), and identity fields
- **Sample project** at `samples/Hexalith.EventStore.Sample/` -- already exists with csproj referencing Client + ServiceDefaults + Dapr.AspNetCore. Current `Program.cs` has placeholder idempotency demo code to be replaced
- **AppHost** at `src/Hexalith.EventStore.AppHost/Program.cs` -- already wires the sample with DAPR sidecar (appId=`sample`, port=8081, access control)

### CounterState Design

**IMPORTANT:** The `Apply()` methods on `CounterState` exist for the EventStore Server's `EventStreamReader` to call during state rehydration (event replay). The `CounterProcessor` itself does NOT call `Apply()` -- it receives the already-reconstructed `CounterState` from the actor's rehydration step. The processor only reads state (e.g., `currentState.Count`) and returns events.

```csharp
// CounterState must support event replay for state reconstruction (called by EventStreamReader in Server package)
public class CounterState
{
    public int Count { get; private set; }

    public void Apply(CounterIncremented e) => Count++;
    public void Apply(CounterDecremented e) => Count--;
    public void Apply(CounterReset e) => Count = 0;
}
```

### Command Routing Pattern

The `CounterProcessor.HandleAsync` method should route based on `command.CommandType`:
- `"IncrementCounter"` -> handle increment
- `"DecrementCounter"` -> handle decrement with rejection guard
- `"ResetCounter"` -> handle reset with no-op guard
- Unknown command type -> throw `InvalidOperationException` (infrastructure error per D3)

### DAPR Service Invocation Endpoint

The `POST /process` endpoint is what `DaprDomainServiceInvoker` calls via `DaprClient.InvokeMethodAsync`. The request/response contract:
- **Request:** JSON body with `CommandEnvelope` fields (or a DTO matching what the invoker sends)
- **Response:** JSON body with `DomainResult` (events list with type discriminator)

### Project Structure Notes

- All new files go under `samples/Hexalith.EventStore.Sample/Counter/` following feature folder convention
- No new NuGet dependencies needed -- the csproj already has Client, ServiceDefaults, and Dapr.AspNetCore
- No changes needed to AppHost -- it already references the sample project with correct DAPR wiring

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 7.1: Sample Counter Domain Service]
- [Source: _bmad-output/planning-artifacts/architecture.md#D3: Domain Service Error Contract]
- [Source: _bmad-output/planning-artifacts/architecture.md#Implementation Patterns & Consistency Rules]
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries]
- [Source: src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs]
- [Source: src/Hexalith.EventStore.Client/Handlers/IDomainProcessor.cs]
- [Source: src/Hexalith.EventStore.Contracts/Results/DomainResult.cs]
- [Source: samples/Hexalith.EventStore.Sample/Program.cs]
- [Source: src/Hexalith.EventStore.AppHost/Program.cs]

## Dev Agent Record

### Agent Model Used
Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

- CA2007 build error on async lambda in Program.cs — fixed by adding ConfigureAwait(false)
- Integration tests have pre-existing failures (Dapr sidecar endpoint allocation) — not caused by this story

### Completion Notes List

- Implemented complete Counter domain service with 3 commands, 4 events (including rejection), CounterState, and CounterProcessor
- CounterProcessor demonstrates all three D3 outcomes: success events, rejection events, and no-op
- Replaced placeholder idempotency demo in Program.cs with proper DI registration and POST /process endpoint
- Added 7 unit tests covering all command handlers and edge cases (unknown command type)
- Task 6 (DAPR config store): AppHost already wires sample with DAPR sidecar (appId=`sample`, port=8081). Domain service resolution uses `DaprClient.InvokeMethodAsync` with appId matching the sidecar config. Config store entry format: tenant=`sample-tenant`, domain=`counter`, appId=`sample`
- All 979 unit tests pass with 0 regressions

### Change Log

- 2026-02-16: Story 7.1 implemented — Sample Counter domain service with full D3 outcome demonstration

### File List

- samples/Hexalith.EventStore.Sample/Counter/Commands/IncrementCounter.cs (new)
- samples/Hexalith.EventStore.Sample/Counter/Commands/DecrementCounter.cs (new)
- samples/Hexalith.EventStore.Sample/Counter/Commands/ResetCounter.cs (new)
- samples/Hexalith.EventStore.Sample/Counter/Events/CounterIncremented.cs (new)
- samples/Hexalith.EventStore.Sample/Counter/Events/CounterDecremented.cs (new)
- samples/Hexalith.EventStore.Sample/Counter/Events/CounterReset.cs (new)
- samples/Hexalith.EventStore.Sample/Counter/Events/CounterCannotGoNegative.cs (new)
- samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs (new)
- samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs (new)
- samples/Hexalith.EventStore.Sample/Program.cs (modified)
- tests/Hexalith.EventStore.Contracts.Tests/Counter/CounterProcessorTests.cs (new)
- tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj (modified)
