# Story 16.7: Updated Sample with Fluent API

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a domain service developer evaluating Hexalith.EventStore,
I want the Counter sample application to use `EventStoreAggregate<TState>` with `AddEventStore()` / `UseEventStore()` as its primary registration path,
so that I can see the fluent API in action and use it as a template for my own domain services.

## Acceptance Criteria

1. **AC1 — CounterAggregate replaces CounterProcessor as primary path:** A new `CounterAggregate : EventStoreAggregate<CounterState>` class exists in `samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs`. It has typed `Handle` methods for `IncrementCounter`, `DecrementCounter`, and `ResetCounter` commands, returning `DomainResult`. The existing `CounterProcessor` file is preserved (not deleted) but is no longer referenced in `Program.cs`.

2. **AC2 — Program.cs uses fluent two-line pattern:** The sample `Program.cs` uses:
   ```csharp
   builder.Services.AddEventStore();
   // ... build host ...
   app.UseEventStore();
   ```
   instead of `builder.Services.AddEventStoreClient<CounterProcessor>()`. The `CounterAggregate` is auto-discovered by assembly scanning (no explicit type reference needed in Program.cs).

3. **AC3 — Handle methods use typed signatures:** Each Handle method follows the `EventStoreAggregate<TState>` convention:
   ```csharp
   public static DomainResult Handle(IncrementCounter command, CounterState? state)
   ```
   Methods can be `static` (preferred for pure functions). The base class dispatches by command type name via reflection.

4. **AC4 — CounterState.Apply methods unchanged:** The existing `CounterState` class with its `Apply(CounterIncremented)`, `Apply(CounterDecremented)`, `Apply(CounterReset)` methods remains unchanged. The `EventStoreAggregate<TState>` base class auto-discovers Apply methods on TState.

5. **AC5 — Existing CounterProcessor preserved:** `CounterProcessor.cs` remains in the project but is not used in the default registration path. Add a comment at the top: `// Legacy IDomainProcessor implementation. See CounterAggregate for the fluent API approach.` **IMPORTANT:** `CounterProcessor` is NOT discovered by `AssemblyScanner` because it implements `IDomainProcessor` directly — the scanner only discovers types inheriting from `EventStoreAggregate<TState>` or `EventStoreProjection<TReadModel>`. This means `CounterProcessor` is completely inert when `AddEventStore()` is used instead of `AddEventStoreClient<CounterProcessor>()`.

6. **AC6 — `/process` endpoint unchanged:** The existing `/process` endpoint continues to work with the same request/response contract (`DomainServiceRequest` / `DomainServiceWireResult`). The `IDomainProcessor` resolved from DI is now the `CounterAggregate` (registered by `AddEventStore()` as both unkeyed and keyed by domain name `"counter"`).

7. **AC7 — All existing sample tests pass:** Tests in `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterProcessorTests.cs` pass unchanged — they instantiate `CounterProcessor` directly via `new CounterProcessor()` (no DI resolution), and `CounterProcessor` still exists in the project. Create a new `CounterAggregateTests.cs` alongside it that instantiates `CounterAggregate` directly and verifies the same command/state scenarios through the `IDomainProcessor.ProcessAsync()` interface.

8. **AC8 — Convention-derived names verified:** After `UseEventStore()`, verify (via diagnostic logging or a startup log) that the auto-discovered `counter` domain produces:
   - State store: `counter-eventstore`
   - Topic: `counter.events`
   - Dead-letter: `deadletter.counter.events`

9. **AC9 — `aspire run` succeeds:** The sample continues to build and run under the Aspire AppHost (`src/Hexalith.EventStore.AppHost/`). The DAPR sidecar configuration remains unchanged.

10. **AC10 — No new NuGet dependencies in sample project:** The sample already references `Hexalith.EventStore.Client` — no additional packages needed.

## Tasks / Subtasks

- [x] Task 1: Create `CounterAggregate` class (AC: #1, #3)
  - [x] 1.1: Create `samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs`
  - [x] 1.2: Inherit from `EventStoreAggregate<CounterState>`
  - [x] 1.3: Add `[EventStoreDomain("counter")]` attribute (or rely on convention: "CounterAggregate" -> "counter") — Used convention (no attribute needed)
  - [x] 1.4: Implement `Handle(IncrementCounter command, CounterState? state)` — returns `DomainResult.Success(new[] { new CounterIncremented() })`
  - [x] 1.5: Implement `Handle(DecrementCounter command, CounterState? state)` — check `state?.Count == 0` for rejection
  - [x] 1.6: Implement `Handle(ResetCounter command, CounterState? state)` — check `state?.Count == 0` for no-op
  - [x] 1.7: All Handle methods should be `public static` (pure functions, CA1822 compliant)

- [x] Task 2: Update `Program.cs` to fluent API (AC: #2, #6)
  - [x] 2.1: Replace `builder.Services.AddEventStoreClient<CounterProcessor>()` with `builder.Services.AddEventStore()`
  - [x] 2.2: Add `app.UseEventStore()` after `builder.Build()` and before `app.Run()`
  - [x] 2.3: Remove `using Hexalith.EventStore.Sample.Counter` if only used for `CounterProcessor` reference
  - [x] 2.4: Keep `/process` endpoint unchanged — `IDomainProcessor` resolves to `CounterAggregate` now
  - [x] 2.5: Verify necessary usings are present (`Hexalith.EventStore.Client.Registration` for `AddEventStore`/`UseEventStore`)

- [x] Task 3: Mark `CounterProcessor` as legacy (AC: #5)
  - [x] 3.1: Add comment at top of `CounterProcessor.cs`: `// Legacy IDomainProcessor implementation. See CounterAggregate for the fluent API approach.`
  - [x] 3.2: Do NOT delete the file — it serves as a reference for the manual approach

- [x] Task 4: Verify build and tests (AC: #7, #9, #10)
  - [x] 4.1: Run `dotnet build` for the sample project — zero errors, zero warnings
  - [x] 4.2: Run existing tests in `Hexalith.EventStore.Sample.Tests` — all 16 pass (8 existing + 8 new)
  - [x] 4.3: Verify `CounterAggregate` handles all three commands correctly (increment, decrement with rejection, reset with no-op) — verified via CounterAggregateTests

- [x] Task 5: Verify convention names match server expectations (AC: #8)
  - [x] 5.1: Enable diagnostics or check activation context — Verified via runtime `UseEventStore()` activation test and startup log (`EventStore activated: 1 domains (counter [Aggregate: CounterAggregate])`)
  - [x] 5.2: Verify domain name resolves to `counter` (from `CounterAggregate` -> strip `Aggregate` suffix -> kebab-case) — Confirmed
  - [x] 5.3: Verify state store = `counter-eventstore`, topic = `counter.events`, dead-letter = `deadletter.counter.events` — Confirmed via cascade Layer 1 defaults
  - [x] 5.4: Cross-check with server-side `EventPublisherOptions` if accessible — Convention names match expected patterns

## Dev Notes

### Architecture Constraints

- **Target framework:** net10.0 with `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`
- **Project:** `samples/Hexalith.EventStore.Sample/` — references `Hexalith.EventStore.Client` and `Hexalith.EventStore.ServiceDefaults`
- **New file:** `CounterAggregate.cs` in `Counter/`
- **Modified files:** `Program.cs`, `CounterProcessor.cs` (comment only)
- **One public type per file**
- **No new NuGet dependencies**

### Key Interfaces & Types to Reference

| Type | Location | Role |
|------|----------|------|
| `EventStoreAggregate<TState>` | `Client/Aggregates/EventStoreAggregate.cs` | Base class — auto-discovers Handle/Apply methods via reflection |
| `EventStoreDomain` attribute | `Client/Attributes/EventStoreDomainAttribute.cs` | Optional domain name override (convention default strips `Aggregate` suffix) |
| `AddEventStore()` | `Client/Registration/EventStoreServiceCollectionExtensions.cs` | DI registration — scans calling assembly |
| `UseEventStore()` | `Client/Registration/EventStoreHostExtensions.cs` | Runtime activation — populates `EventStoreActivationContext` |
| `CounterState` | `Sample/Counter/State/CounterState.cs` | Existing state class with Apply methods — UNCHANGED |
| `DomainResult` | `Contracts/Results/DomainResult.cs` | Success/Rejection/NoOp return type |
| `IDomainProcessor` | `Client/Handlers/IDomainProcessor.cs` | Interface implemented by `EventStoreAggregate<TState>` |
| `NamingConventionEngine` | `Client/Conventions/NamingConventionEngine.cs` | `CounterAggregate` -> `counter` domain name |

### Handle Method Pattern

The `EventStoreAggregate<TState>` base class discovers Handle methods via reflection. Requirements:
- Method name must be exactly `Handle`
- First parameter: the command type (e.g., `IncrementCounter`)
- Second parameter: `TState?` (nullable state, e.g., `CounterState?`)
- Return type: `DomainResult` (sync) or `Task<DomainResult>` (async)
- Can be `static` or instance (static preferred for pure functions)

```csharp
// Pattern from EventStoreAggregate.cs DiscoverHandleMethods():
// - Searches Public | Instance | Static | DeclaredOnly
// - Matches method.Name == "Handle"
// - parameters.Length == 2
// - parameters[1].ParameterType == typeof(TState) (NOT nullable — the base class uses TState directly)
// - Returns DomainResult or Task<DomainResult>
```

**CRITICAL:** The second parameter type must be `CounterState?` — the base class checks `parameters[1].ParameterType == typeof(TState)`. Since `CounterState` is a reference type, `CounterState?` is the same CLR type as `CounterState` (nullable annotations are compile-time only). So `CounterState?` in the signature matches `typeof(CounterState)`.

### CounterAggregate Implementation

```csharp
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;

namespace Hexalith.EventStore.Sample.Counter;

/// <summary>
/// Counter aggregate using the fluent EventStoreAggregate API.
/// Replaces CounterProcessor as the primary domain implementation.
/// </summary>
public sealed class CounterAggregate : EventStoreAggregate<CounterState>
{
    public static DomainResult Handle(IncrementCounter command, CounterState? state)
        => DomainResult.Success(new IEventPayload[] { new CounterIncremented() });

    public static DomainResult Handle(DecrementCounter command, CounterState? state)
    {
        if ((state?.Count ?? 0) == 0)
        {
            return DomainResult.Rejection(new IRejectionEvent[] { new CounterCannotGoNegative() });
        }
        return DomainResult.Success(new IEventPayload[] { new CounterDecremented() });
    }

    public static DomainResult Handle(ResetCounter command, CounterState? state)
    {
        if ((state?.Count ?? 0) == 0)
        {
            return DomainResult.NoOp();
        }
        return DomainResult.Success(new IEventPayload[] { new CounterReset() });
    }
}
```

### Updated Program.cs

```csharp
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEventStore();  // Auto-discovers CounterAggregate

WebApplication app = builder.Build();

app.UseEventStore();  // Activates domains with convention-derived names

app.MapDefaultEndpoints();
app.MapGet("/", () => "Hexalith EventStore Sample Domain Service");

app.MapPost("/process", async (DomainServiceRequest request, IDomainProcessor processor) => {
    DomainResult result = await processor.ProcessAsync(request.Command, request.CurrentState).ConfigureAwait(false);
    return Results.Ok(DomainServiceWireResult.FromDomainResult(result));
});

app.Run();
```

### Previous Story Intelligence (Stories 16-5 and 16-6)

**Patterns established that MUST be followed:**
- `ArgumentNullException.ThrowIfNull()` for parameter validation
- CA1822 compliance: static methods where possible (Handle methods should be static)
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`
- Test framework: xunit + Assert (NOT Shouldly)
- One public type per file, file name = type name
- `AssemblyScanner.ClearCache()` and `NamingConventionEngine.ClearCache()` in test teardown

**Key decisions relevant to 16-7:**
- `AddEventStore()` with no arguments scans the calling assembly (the Sample assembly)
- `UseEventStore()` extends `IHost` — `WebApplication` implements `IHost` so it works
- The `CounterAggregate` will be auto-discovered by `AssemblyScanner` (it inherits `EventStoreAggregate<TState>`)
- `NamingConventionEngine` strips `Aggregate` suffix and converts to kebab-case: `CounterAggregate` -> `counter`
- `AddEventStore()` registers discovered types as `IDomainProcessor` (both unkeyed and keyed by domain name)
- The existing `/process` endpoint resolves `IDomainProcessor` from DI — with only one aggregate, the unkeyed registration resolves to `CounterAggregate`

**CRITICAL: DI Registration Details:**
- `AddEventStore()` registers discovered aggregates as **Scoped** `IDomainProcessor` — both unkeyed (`services.AddScoped(typeof(IDomainProcessor), aggregate.Type)`) and keyed by domain name (`services.AddKeyedScoped(typeof(IDomainProcessor), "counter", typeof(CounterAggregate))`).
- `CounterProcessor` is **NOT discovered** by `AssemblyScanner` — it implements `IDomainProcessor` directly, not via `EventStoreAggregate<TState>`. The scanner only checks `IsSubclassOfOpenGeneric(type, typeof(EventStoreAggregate<>))`. So `CounterProcessor` is completely inert — zero conflict risk.
- The `/process` endpoint resolves unkeyed `IDomainProcessor` — with only `CounterAggregate` registered, it resolves correctly.
- `AddEventStore()` uses `Assembly.GetCallingAssembly()` (with `[MethodImpl(MethodImplOptions.NoInlining)]`). From top-level statements in the sample's `Program.cs`, this returns the sample project assembly, which contains `CounterAggregate`. This is verified to work correctly.

### Git Intelligence

Recent commits:
```
016c01b fix(story-16-4): close review findings and mark done (#72)
633b985 feat: Add AssemblyScanner auto-discovery and harden aggregate/projection error handling
4e47cb6 feat: Add EventStoreAggregate base class, EventStoreDomain attribute, and NamingConventionEngine
```

### Testing Expectations

**Test file location:**
- Existing: `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterProcessorTests.cs`
- Existing tests should continue passing (CounterProcessor still exists)

**New test considerations:**
- If adding `CounterAggregate` tests, create `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs`
- Test that `CounterAggregate` handles all 3 commands with correct results
- Test state-dependent behavior (decrement at zero -> rejection, reset at zero -> no-op)
- Test via the `IDomainProcessor` interface (same contract as CounterProcessor)

### Scope Boundary

**IN scope for 16-7:**
- New `CounterAggregate` class
- Updated `Program.cs` with fluent API
- Legacy comment on `CounterProcessor`
- Build and test verification

**NOT in scope for 16-7:**
- Deleting `CounterProcessor` (preserved as reference)
- Modifying `CounterState` or event/command types
- Changing DAPR component configuration
- Modifying the Aspire AppHost
- README/quickstart updates (Story 16-9)
- WebApplication compatibility testing (Story 16-10)

### Project Structure Notes

- `CounterAggregate.cs` sits alongside `CounterProcessor.cs` in `Counter/` — both implement the same domain, one via fluent API, one via manual `IDomainProcessor`
- No changes to `Counter/Commands/`, `Counter/Events/`, or `Counter/State/` folders
- No changes to project references or dependencies

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-02-28.md#Section 4.2] — Story 16-7 definition
- [Source: _bmad-output/implementation-artifacts/16-5-use-eventstore-extension-method-with-activation.md] — UseEventStore() patterns
- [Source: _bmad-output/implementation-artifacts/16-6-five-layer-cascading-configuration.md] — Cascade config patterns
- [Source: src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs] — Base class with Handle method discovery
- [Source: samples/Hexalith.EventStore.Sample/Program.cs] — Current Program.cs to modify
- [Source: samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs] — Legacy processor to preserve
- [Source: samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs] — State class (unchanged)

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

No debug issues encountered. Clean implementation.

### Completion Notes List

- Created `CounterAggregate` class inheriting `EventStoreAggregate<CounterState>` with three static Handle methods for IncrementCounter, DecrementCounter, and ResetCounter commands
- Updated `Program.cs` from `AddEventStoreClient<CounterProcessor>()` to `AddEventStore()` / `UseEventStore()` fluent two-line pattern
- Removed `using Hexalith.EventStore.Sample.Counter` from Program.cs (no longer needed without CounterProcessor reference)
- Added legacy comment to top of `CounterProcessor.cs`
- Created `CounterAggregateTests.cs` with 8 tests:
  - 6 command/state tests via `IDomainProcessor.ProcessAsync()`
  - 1 DI registration test proving unkeyed + keyed (`"counter"`) `IDomainProcessor` resolves to `CounterAggregate`
  - 1 runtime activation test proving convention-derived names (`counter-eventstore`, `counter.events`, `deadletter.counter.events`)
- All 16 sample tests pass (8 existing CounterProcessor + 8 CounterAggregate)
- Full solution unit tests pass: Client.Tests (187), Contracts.Tests (157), Testing.Tests (48), Sample.Tests (16) — all green
- Integration tests have pre-existing failures (infrastructure-dependent Aspire/Dapr tests) unrelated to this story
- Convention naming verified: `CounterAggregate` → `counter` domain, `counter-eventstore` state store, `counter.events` topic, `deadletter.counter.events` dead-letter
- Aspire AppHost startup smoke check succeeds with `EnableKeycloak=false` (dashboard URL emitted and apphost running)
- No new NuGet dependencies added

### File List

- `samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs` (NEW)
- `samples/Hexalith.EventStore.Sample/Program.cs` (MODIFIED)
- `samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs` (MODIFIED — legacy comment added)
- `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs` (NEW)

## Change Log

- 2026-03-01: Story 16-7 implemented — Migrated Counter sample from manual `AddEventStoreClient<CounterProcessor>()` to fluent `AddEventStore()` / `UseEventStore()` API with new `CounterAggregate` class. Added comprehensive tests.
- 2026-03-01: Senior review fixes applied — strengthened evidence for AC6/AC8/AC9 by adding DI + activation tests and runtime AppHost startup verification; status set to done.

## Senior Developer Review (AI)

- Reviewer: Jerome (AI-assisted)
- Date: 2026-03-01
- Outcome: **Approved**

### Review Summary

- AC1/AC2/AC3/AC4/AC5/AC10 validated in source.
- AC6 validated with DI registration test proving unkeyed and keyed (`"counter"`) `IDomainProcessor` resolve to `CounterAggregate`.
- AC7 validated with direct processor tests and interface-based aggregate command tests.
- AC8 validated with activation-context assertion test and runtime startup log (`EventStore activated: 1 domains (counter [Aggregate: CounterAggregate])`).
- AC9 validated with Aspire AppHost startup smoke check (`aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`).

### Files Updated During Review

- `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs`
- `_bmad-output/implementation-artifacts/16-7-updated-sample-with-fluent-api.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
