# Story 1.3: Client Package - Domain Processor Contract & Registration

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **domain service developer**,
I want a Client NuGet package providing IDomainProcessor interface with the pure function contract `(Command, CurrentState?) -> List<DomainEvent>`, a DomainProcessorBase helper, and an `AddEventStoreClient()` DI extension,
so that I can implement and register domain services with a clean, testable programming model.

## Prerequisites

Before starting this story, the dev agent MUST verify:

- [x] Story 1.2 completed (Contracts package with EventEnvelope, CommandEnvelope, AggregateIdentity, DomainResult, IRejectionEvent, IEventPayload)
- [x] `dotnet build` succeeds with zero errors/warnings on current main branch
- [x] Client project exists at `src/Hexalith.EventStore.Client/` with BuildVerification.cs placeholder and ProjectReference to Contracts

## Acceptance Criteria

1. **IDomainProcessor Interface** - Given the Client package is referenced, When I implement IDomainProcessor, Then the contract is `Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)` matching the pure function model (FR21). The interface has exactly one method.

2. **DomainProcessorBase Abstract Class** - Given the Client package is referenced, When I extend `DomainProcessorBase<TState>` (where `TState : class`), Then it provides:
   - Implementation of `IDomainProcessor.ProcessAsync(CommandEnvelope command, object? currentState)` that safely casts `object?` to `TState?` and delegates to a protected abstract method
   - Protected abstract method `Task<DomainResult> HandleAsync(CommandEnvelope command, TState? currentState)` enabling typed state access
   - Sensible null-state handling: when `currentState` is null (new aggregate), `TState?` receives null (not an exception)
   - Invalid state type handling: when `currentState` is non-null but not `TState`, throws `InvalidOperationException` with a clear message naming expected vs actual type
   - No command routing mechanism — domain developers dispatch based on `command.CommandType` in their `HandleAsync` implementation (a simple switch/match is transparent and debuggable)

3. **AddEventStoreClient Extension** - Given the Client package is referenced, When I call `services.AddEventStoreClient<TProcessor>()` where TProcessor implements IDomainProcessor, Then:
   - The domain processor is registered in the DI container as `IDomainProcessor` with **scoped** lifetime (per-request processing)
   - The extension method is on `IServiceCollection` in a `ServiceCollectionExtensions` class
   - The `ServiceCollectionExtensions` class is in the `Microsoft.Extensions.DependencyInjection` namespace (for discoverability via IntelliSense) with the file located at `Registration/ServiceCollectionExtensions.cs`
   - The registration follows the `Add*` pattern per enforcement rule #10
   - The method returns `IServiceCollection` for chaining

4. **Client Package Dependencies** - Given the Client .csproj is inspected, Then it contains:
   - A ProjectReference to Hexalith.EventStore.Contracts
   - A PackageReference to `Dapr.Client` (per architecture NuGet dependency table — not used by current types but needed for future endpoint mapping)
   - `Microsoft.Extensions.DependencyInjection.Abstractions` if not transitively available from Dapr.Client (verify during implementation)
   - No other NuGet package dependencies

5. **Clean Build** - Given all types are implemented, When I run `dotnet build`, Then the solution builds with zero errors and zero warnings, and `dotnet pack` produces a valid Client .nupkg.

6. **Unit Tests** - Given the Client types are implemented, When I run `dotnet test`, Then tests verify:
   - A concrete IDomainProcessor implementation can be instantiated and called
   - DomainProcessorBase<TState> correctly casts non-null `object?` to `TState?`
   - DomainProcessorBase<TState> passes null through as null `TState?` (new aggregate scenario)
   - DomainProcessorBase<TState> throws `InvalidOperationException` for wrong state type
   - AddEventStoreClient<T> registers IDomainProcessor resolvable from `IServiceCollection`/`IServiceProvider`

## Tasks / Subtasks

### Task 1: Create IDomainProcessor interface (AC: #1)

- [x] 1.1 Create `Handlers/IDomainProcessor.cs` — interface with single method `Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)`
- [x] 1.2 Delete `BuildVerification.cs` (replaced by real types)

**Verification:** `dotnet build` succeeds; interface matches pure function contract (FR21)

### Task 2: Create DomainProcessorBase abstract class (AC: #2)

- [x] 2.1 Create `Handlers/DomainProcessorBase.cs` — generic abstract class `DomainProcessorBase<TState> : IDomainProcessor where TState : class`
- [x] 2.2 Implement `ProcessAsync(CommandEnvelope, object?)` with pattern-match state casting: null → null, TState → typed, other → InvalidOperationException
- [x] 2.3 Define protected abstract `Task<DomainResult> HandleAsync(CommandEnvelope command, TState? currentState)`

**Verification:** DomainProcessorBase compiles; typed state access works; null state passes through; wrong type throws with clear message

### Task 3: Create DI registration extension (AC: #3)

- [x] 3.1 Create `Registration/ServiceCollectionExtensions.cs` — `AddEventStoreClient<TProcessor>()` extension method on `IServiceCollection` in `Microsoft.Extensions.DependencyInjection` namespace
- [x] 3.2 Register TProcessor as IDomainProcessor with scoped lifetime
- [x] 3.3 Return `IServiceCollection` for chaining
- [x] 3.4 Verify `Microsoft.Extensions.DependencyInjection.Abstractions` is transitively available from Dapr.Client — if not, add explicit PackageReference

**Verification:** AddEventStoreClient registers processor in DI; resolving IDomainProcessor returns the registered implementation

### Task 4: Unit tests (AC: #6)

- [x] 4.1 Create test file(s) in `tests/Hexalith.EventStore.Contracts.Tests/` (or add Client test project if preferred)
- [x] 4.2 Test: concrete IDomainProcessor implementation can process a command and return DomainResult
- [x] 4.3 Test: DomainProcessorBase<TestState> casts valid state correctly
- [x] 4.4 Test: DomainProcessorBase<TestState> passes null currentState as null TState?
- [x] 4.5 Test: DomainProcessorBase<TestState> throws InvalidOperationException for wrong state type with descriptive message
- [x] 4.6 Test: AddEventStoreClient<T> registers and resolves IDomainProcessor from ServiceProvider

**Verification:** All tests pass; `dotnet test` succeeds across full solution

### Task 5: Build and pack verification (AC: #4, #5)

- [x] 5.1 Run `dotnet build` — zero errors, zero warnings
- [x] 5.2 Run `dotnet pack` — Client .nupkg produced
- [x] 5.3 Verify Client.csproj has correct dependencies (Contracts + Dapr.Client, nothing unexpected)
- [x] 5.4 Verify all types are in correct feature folders matching architecture

## Dev Notes

### Technical Design Decisions

**IDomainProcessor — the pure function contract (FR21, D3):**
- Single method: `Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)`
- Returns `DomainResult` (from Contracts) which wraps `IReadOnlyList<IEventPayload>`
- `object? currentState` is untyped because EventStore is domain-agnostic; each domain service casts to its own state type
- The `Task<>` wrapper enables async domain logic (e.g., external lookups, though pure functions are preferred)
- Domain services ALWAYS return events, never throw for domain logic (D3). Exceptions = infrastructure failures only

**DomainProcessorBase<TState> — typed state casting only:**
- Generic abstract class: `DomainProcessorBase<TState> : IDomainProcessor where TState : class`
- Single responsibility: safely cast `object?` currentState to `TState?` and delegate to typed `HandleAsync`
- **No command routing mechanism.** Domain developers write a switch/match on `command.CommandType` in their `HandleAsync`. This is transparent, debuggable, and every C# developer understands it. Adding dictionary-based routing or handler registration is premature abstraction for a contract that most domain services implement with 3-10 command types
- `where TState : class` constraint is correct because: (a) `currentState` arrives as `object?` (boxed), (b) value type aggregate states are pathological, (c) `class` enables clean null pattern matching
- Pattern-match implementation:

```csharp
public abstract class DomainProcessorBase<TState> : IDomainProcessor
    where TState : class
{
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)
    {
        TState? typedState = currentState switch
        {
            null => null,
            TState s => s,
            _ => throw new InvalidOperationException(
                $"Expected state type '{typeof(TState).Name}' but received '{currentState.GetType().Name}'.")
        };
        return HandleAsync(command, typedState);
    }

    protected abstract Task<DomainResult> HandleAsync(CommandEnvelope command, TState? currentState);
}
```

**AddEventStoreClient — DI registration (enforcement rule #10):**
- Extension method on `IServiceCollection`, returns `IServiceCollection` for chaining
- Namespace: `Microsoft.Extensions.DependencyInjection` (standard .NET convention for DI extensions — enables IntelliSense discoverability without requiring a `using` statement)
- File location: `Registration/ServiceCollectionExtensions.cs` (feature folder convention)
- Registration: `services.AddScoped<IDomainProcessor, TProcessor>()` — scoped lifetime because domain processors are invoked per HTTP request via DAPR service invocation
- Generic constraint: `where TProcessor : class, IDomainProcessor`

**Endpoint mapping is DEFERRED:**
- This story creates the contract (IDomainProcessor) and DI registration (AddEventStoreClient) but does NOT create the HTTP endpoint that receives DAPR service invocation calls and delegates to IDomainProcessor
- The endpoint mapping (e.g., `app.MapEventStoreEndpoints()` or a controller) will be created when the Sample domain service is built (Story 7.1) or when Server-side invocation is implemented (Story 3.5)
- This is intentional: the Client package is the SDK contract layer, not the hosting layer

**Why Dapr.Client dependency is kept but not used by current types:**
- The architecture NuGet dependency table explicitly lists `Dapr.Client` for the Client package
- Current types (IDomainProcessor, DomainProcessorBase, ServiceCollectionExtensions) do NOT reference DaprClient
- The dependency is retained because: (a) it was intentionally added in Story 1.1 per architecture, (b) future endpoint mapping and DAPR integration will need it, (c) removing and re-adding creates churn
- Domain service developers may also use `DaprClient` directly for auxiliary operations

### Project Structure Notes

**Target file structure (architecture-mandated):**
```
src/Hexalith.EventStore.Client/
├── Hexalith.EventStore.Client.csproj  # Contracts + Dapr.Client dependencies
├── Handlers/
│   ├── IDomainProcessor.cs            # (Command, State?) -> DomainResult contract
│   └── DomainProcessorBase.cs         # Abstract base with typed state casting only
└── Registration/
    └── ServiceCollectionExtensions.cs # AddEventStoreClient<TProcessor>() in Microsoft.Extensions.DependencyInjection namespace
```

**Files to DELETE:**
- `src/Hexalith.EventStore.Client/BuildVerification.cs` (Story 1.1 placeholder — replace with real types)

**Note:** `Configuration/EventStoreClientOptions.cs` is NOT created in this story. Add only when configuration is actually needed (YAGNI).

**Alignment with architecture:**
- Feature folder convention: Handlers/, Registration/ ✓
- One public type per file ✓
- File name = type name ✓
- `Add*` extension method pattern per enforcement rule #10 ✓

### Previous Story 1.2 Intelligence

**Key learnings from Story 1.2 implementation:**
- Record types used for all immutable value objects (EventEnvelope, EventMetadata, CommandEnvelope, AggregateIdentity, CommandStatusRecord, DomainResult)
- `IEventPayload` is the base marker interface for all domain events; `IRejectionEvent` extends it
- `DomainResult` wraps `IReadOnlyList<IEventPayload>` with factory methods (Success, Rejection, NoOp) and mixed-event validation
- `CommandEnvelope` eagerly validates all required fields at construction time (TenantId, Domain, AggregateId via AggregateIdentity; CommandType, Payload, CorrelationId, UserId)
- File-scoped namespaces, XML doc comments on all public types/members
- `TreatWarningsAsErrors=true` — all warnings must be fixed
- CPM active — no Version= on PackageReference entries in .csproj
- Code review caught: missing validation, insufficient defensive copies — validate thoroughly

**Existing types the Client package builds on:**
- `Hexalith.EventStore.Contracts.Commands.CommandEnvelope` — the command input to processors
- `Hexalith.EventStore.Contracts.Results.DomainResult` — the return type from processors
- `Hexalith.EventStore.Contracts.Events.IEventPayload` — base type for all domain events
- `Hexalith.EventStore.Contracts.Events.IRejectionEvent` — marker for rejection events (D3)

### Git Intelligence

**Recent commits:**
- `80a7f88` Story 1.2: Contracts Package - Event Envelope & Core Types (#15)
- `46794e1` Story 1.2: Contracts package - Event Envelope & Core Types
- `44dcad5` Merge PR #14 (Story 1.1 code review fixes)

**Patterns from previous work:**
- PR-based workflow (feature branch → PR → merge to main)
- Comprehensive unit tests accompany all new types
- Code review catches validation gaps — be thorough with input validation
- XML doc comments required on all public API surface

### Critical Guardrails for Dev Agent

1. **IDomainProcessor has exactly ONE method** — `Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)`. No overloads, no additional methods
2. **DomainProcessorBase<TState> with `where TState : class`** — cast `object?` safely via pattern match; null state = null typed state (new aggregate); wrong type = `InvalidOperationException` with message naming expected vs actual type
3. **No command routing in base class** — no dictionary registration, no handler maps, no fluent API. Domain developers write a switch/match in their `HandleAsync`. This is a deliberate architectural decision, not a gap
4. **AddEventStoreClient follows `Add*` pattern** — per enforcement rule #10, register via extension method, never inline. Returns `IServiceCollection` for chaining. Scoped lifetime
5. **ServiceCollectionExtensions namespace is `Microsoft.Extensions.DependencyInjection`** — NOT `Hexalith.EventStore.Client.Registration`. File lives in Registration/ folder but namespace follows .NET DI convention for discoverability
6. **Feature folders ONLY** — Handlers/, Registration/. No Models/, Services/, Interfaces/, Configuration/ folders (unless EventStoreClientOptions is actually needed)
7. **One public type per file** — filename must match type name exactly
8. **File-scoped namespaces** — `namespace Hexalith.EventStore.Client.Handlers;` (except ServiceCollectionExtensions which uses `Microsoft.Extensions.DependencyInjection`)
9. **Delete BuildVerification.cs** — it's a Story 1.1 placeholder; replace with real types
10. **XML doc comments on all public types and members** — required for NuGet package documentation
11. **No Version= on PackageReference** — CPM manages versions via Directory.Packages.props
12. **Domain logic NEVER throws** — IDomainProcessor implementations return DomainResult (success events, rejection events, or no-op per D3). Exceptions are infrastructure failures only
13. **Async suffix** — `ProcessAsync`, `HandleAsync` per .editorconfig convention
14. **IDomainProcessor and DomainProcessorBase must NOT reference DaprClient** — they are pure contracts using only Contracts types (CommandEnvelope, DomainResult). Dapr.Client is in the .csproj for future use, not for these types
15. **Unit tests are REQUIRED** — previous stories all had comprehensive tests. This story must include tests for typed state casting, null handling, wrong type exception, and DI registration
16. **Endpoint mapping is DEFERRED** — do NOT create HTTP endpoints, controllers, or MapEventStoreEndpoints() in this story. The Client package is contracts + DI, not hosting

### References

- [Source: _bmad-output/planning-artifacts/architecture.md#Core Architectural Decisions - Client package in NuGet dependency table]
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries - Client directory structure: Handlers/, Registration/, Configuration/]
- [Source: _bmad-output/planning-artifacts/architecture.md#Implementation Patterns - Add* extension method pattern (enforcement rule #10)]
- [Source: _bmad-output/planning-artifacts/architecture.md#Communication Patterns - Actor Step 4: domain service invocation]
- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.3 - Acceptance criteria: IDomainProcessor, DomainProcessorBase, AddEventStoreClient]
- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.3 - FR21: Pure function domain processor contract]
- [Source: _bmad-output/planning-artifacts/architecture.md#Core Architectural Decisions - D3: Domain errors as events, D7: DAPR service invocation]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines - Rule #10: Add* extensions]
- [Source: _bmad-output/implementation-artifacts/1-2-contracts-package-event-envelope-and-core-types.md - Previous story patterns and learnings]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

None — implementation completed without issues.

### Completion Notes List

- Implemented IDomainProcessor interface with single `ProcessAsync` method matching FR21 pure function contract
- Implemented DomainProcessorBase<TState> with pattern-match state casting (null passthrough, typed cast, InvalidOperationException for wrong type)
- Implemented AddEventStoreClient<TProcessor>() DI extension in Microsoft.Extensions.DependencyInjection namespace with scoped lifetime
- Verified Microsoft.Extensions.DependencyInjection.Abstractions is transitively available from Dapr.Client (no explicit PackageReference needed)
- Created dedicated Hexalith.EventStore.Client.Tests project with 9 unit tests — all pass
- Full regression suite: 158 tests pass (9 new + 149 existing), zero failures
- dotnet build: zero errors, zero warnings
- dotnet pack: Client .nupkg produced successfully
- Deleted BuildVerification.cs placeholder

### File List

- `src/Hexalith.EventStore.Client/Handlers/IDomainProcessor.cs` (new)
- `src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs` (new)
- `src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs` (new)
- `src/Hexalith.EventStore.Client/BuildVerification.cs` (deleted)
- `tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj` (new)
- `tests/Hexalith.EventStore.Client.Tests/Handlers/DomainProcessorTests.cs` (new)
- `tests/Hexalith.EventStore.Client.Tests/Registration/ServiceCollectionExtensionsTests.cs` (new)
- `Hexalith.EventStore.slnx` (modified — added Client.Tests project)

## Change Log

- 2026-02-12: Story 1.3 implemented — IDomainProcessor, DomainProcessorBase<TState>, AddEventStoreClient<TProcessor> DI extension, 6 unit tests
- 2026-02-12: Code review fixes — Added null guards (command param, services param), renamed ServiceCollectionExtensions to EventStoreServiceCollectionExtensions to avoid namespace collision, improved test coverage (null passthrough verification, scoped lifetime test, null command test), 9 total tests
