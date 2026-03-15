# Story 1.4: Pure Function Contract & EventStoreAggregate Base

Status: ready-for-dev

## Story

As a domain service developer,
I want an `IDomainProcessor` interface and an `EventStoreAggregate` base class,
So that I can implement domain logic as pure functions with convention-based method discovery.

## Acceptance Criteria

1. **IDomainProcessor contract** — `IDomainProcessor` defines `ProcessAsync(CommandEnvelope, object?) -> Task<DomainResult>`. The typed contract is enforced by `EventStoreAggregate<TState>` which dispatches to `Handle(TCommand, TState?) -> DomainResult` methods via reflection.

2. **EventStoreAggregate<TState>** — inheriting from it enables reflection-based discovery of `Handle` methods (command dispatch) and `Apply` methods (state projection) with no manual registration.

3. **Convention-based DAPR resource naming** — aggregate type name derived as kebab-case from class name with automatic suffix stripping (`CounterAggregate` -> `counter`). Attribute overrides via `[EventStoreDomain("name")]` validated at startup for non-empty, kebab-case compliance (Rule 17).

4. **Public API surface** — only domain-service-developer-facing types are public (UX-DR20). All public types have XML documentation (UX-DR19).

5. All existing and new Tier 1 tests pass.

6. **Done definition:** IDomainProcessor, DomainProcessorBase<TState>, EventStoreAggregate<TState>, NamingConventionEngine, EventStoreDomainAttribute verified complete. All public types have XML docs. All Tier 1 tests green.

## Tasks / Subtasks

- [ ] Task 1: Audit IDomainProcessor & DomainProcessorBase<TState> (AC: #1)
  - [ ] 1.1 Verify `IDomainProcessor` at `src/Hexalith.EventStore.Client/Handlers/IDomainProcessor.cs` — contract: `ProcessAsync(CommandEnvelope, object?) -> Task<DomainResult>`
  - [ ] 1.2 Verify `DomainProcessorBase<TState>` at `src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs` — typed state casting, JsonElement deserialization, delegates to abstract `HandleAsync(CommandEnvelope, TState?)`
  - [ ] 1.3 Verify XML docs on both types are complete (summary, param, returns, typeparam)
  - [ ] 1.4 Run `DomainProcessorTests.cs` — confirm all 7 tests pass

- [ ] Task 2: Audit EventStoreAggregate<TState> (AC: #2)
  - [ ] 2.1 Verify `EventStoreAggregate<TState>` at `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs` — implements IDomainProcessor, reflection-based Handle/Apply discovery, metadata caching, state rehydration from JsonElement/typed/IEnumerable
  - [ ] 2.2 Verify Handle method discovery: method name `Handle`, 2 params `(TCommand, TState?)`, returns `DomainResult` or `Task<DomainResult>`, public static or instance, `BindingFlags.DeclaredOnly`
  - [ ] 2.3 Verify Apply method discovery: method name `Apply`, 1 param `(TEvent)`, returns `void`, public instance, discovered on `TState` type
  - [ ] 2.4 Verify command dispatch: payload deserialized from `CommandEnvelope.Payload` (byte[]) via `JsonSerializer.Deserialize`, dispatched to matching Handle method by `CommandType` name
  - [ ] 2.5 Verify state rehydration: supports null, typed TState, JsonElement object, JsonElement array (event replay with eventTypeName + payload), JsonElement null, IEnumerable (typed event replay)
  - [ ] 2.6 Verify metadata caching uses `ConcurrentDictionary<Type, AggregateMetadata>` for thread-safe per-aggregate-type caching
  - [ ] 2.7 Verify XML docs on class, ProcessAsync, OnConfiguring
  - [ ] 2.8 Run `EventStoreAggregateTests.cs` — confirm all 35+ tests pass

- [ ] Task 3: Audit NamingConventionEngine & EventStoreDomainAttribute (AC: #3)
  - [ ] 3.1 Verify `NamingConventionEngine` at `src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs` — PascalCase-to-kebab, suffix stripping (Aggregate/Projection/Processor), attribute override, kebab validation regex `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`, max 64 chars, ConcurrentDictionary cache
  - [ ] 3.2 Verify `EventStoreDomainAttribute` at `src/Hexalith.EventStore.Client/Attributes/EventStoreDomainAttribute.cs` — non-empty validation, `AllowMultiple = false`, `Inherited = false`
  - [ ] 3.3 Verify resource name derivation: `GetStateStoreName("{domain}-eventstore")`, `GetPubSubTopic("{tenantId}.{domain}.events")`, `GetCommandEndpoint("{domain}-commands")`
  - [ ] 3.4 Verify XML docs on all public methods
  - [ ] 3.5 Run `NamingConventionEngineTests.cs` — confirm all 40+ tests pass

- [ ] Task 4: Audit public API surface and XML documentation (AC: #4)
  - [ ] 4.1 List all public types in `Hexalith.EventStore.Client` project — verify only developer-facing types are public (UX-DR20)
  - [ ] 4.2 Verify XML documentation on ALL public types, methods, and properties (UX-DR19)
  - [ ] 4.3 Verify internal types: AggregateMetadata, HandleMethodInfo should be internal/private (implementation details)
  - [ ] 4.4 Fix any missing XML docs or incorrect visibility

- [ ] Task 5: Verify Counter sample demonstrates the pattern (AC: #1, #2, #3)
  - [ ] 5.1 Verify `CounterAggregate` at `samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs` — inherits `EventStoreAggregate<CounterState>`, static Handle methods for IncrementCounter/DecrementCounter/ResetCounter, returns DomainResult
  - [ ] 5.2 Verify `CounterState` at `samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs` — Apply methods for CounterIncremented/CounterDecremented/CounterReset
  - [ ] 5.3 Run `CounterAggregateTests.cs` — confirm all 7 tests pass including DI resolution and UseEventStore activation

- [ ] Task 6: Full Tier 1 regression verification (AC: #5, #6)
  - [ ] 6.1 Run `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings, zero errors
  - [ ] 6.2 Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/`
  - [ ] 6.3 Run `dotnet test tests/Hexalith.EventStore.Client.Tests/`
  - [ ] 6.4 Run `dotnet test tests/Hexalith.EventStore.Sample.Tests/`
  - [ ] 6.5 Run `dotnet test tests/Hexalith.EventStore.Testing.Tests/`

## Dev Notes

### Scope Summary

This is an **audit-and-verify** story, NOT greenfield. All four components are fully implemented and tested under earlier stories (16-2 through 16-8 in the old numbering). The dev agent must verify completeness, fix any XML doc gaps, verify public API surface, and confirm all tests pass.

### Existing Implementation State

| File | Status | Notes |
|------|--------|-------|
| `src/.../Handlers/IDomainProcessor.cs` | Complete | Non-generic contract: `ProcessAsync(CommandEnvelope, object?) -> Task<DomainResult>` |
| `src/.../Handlers/DomainProcessorBase.cs` | Complete | Generic typed base with JsonElement state deserialization |
| `src/.../Aggregates/EventStoreAggregate.cs` | Complete | Reflection-based Handle/Apply, metadata cache, 5 rehydration paths |
| `src/.../Conventions/NamingConventionEngine.cs` | Complete | PascalCase-to-kebab, suffix strip, attribute override, validation |
| `src/.../Attributes/EventStoreDomainAttribute.cs` | Complete | Non-empty validation, AllowMultiple=false, Inherited=false |
| `samples/.../Counter/CounterAggregate.cs` | Complete | Reference implementation of EventStoreAggregate pattern |
| `samples/.../Counter/State/CounterState.cs` | Complete | Reference state with Apply methods |
| `tests/.../Aggregates/EventStoreAggregateTests.cs` | Complete | 35+ tests: dispatch, rehydration, cache, error handling, Base64 |
| `tests/.../Handlers/DomainProcessorTests.cs` | Complete | 7 tests: direct, typed, null, wrong type, JsonElement |
| `tests/.../Conventions/NamingConventionEngineTests.cs` | Complete | 40+ tests: suffix strip, kebab, attribute, validation, cache, concurrency |
| `tests/.../Counter/CounterAggregateTests.cs` | Complete | 7 tests: all commands + DI resolution + UseEventStore activation |

### Why IDomainProcessor is Non-Generic

The epics AC references `IDomainProcessor<TCommand, TState>` but the actual implementation uses `IDomainProcessor` (non-generic). This is intentional:

- **Server-side invocation** (`AggregateActor` → `DaprDomainServiceInvoker`) doesn't know command types at compile time — it passes `CommandEnvelope` + `object?` state
- **Typed dispatch** happens inside `EventStoreAggregate<TState>` via reflection — the Handle method signature `Handle(TCommand, TState?) -> DomainResult` is the true typed contract
- **DomainProcessorBase<TState>** provides typed state for legacy `CounterProcessor`-style implementations
- Making the interface generic would require the server to know all command types, breaking the DAPR service invocation model (D7)
- The Counter sample proves the pattern works: `CounterAggregate : EventStoreAggregate<CounterState>` with `IDomainProcessor` DI registration

This is a **deliberate architecture decision**, not a gap. Do NOT create a generic `IDomainProcessor<TCommand, TState>` interface.

### Handle Method Discovery Rules

Handle methods on the aggregate class must follow:
- **Name:** exactly `Handle` (case-sensitive, ordinal)
- **Parameters:** exactly 2 — `(TCommand, TState?)` where TState matches the aggregate's generic parameter
- **Return:** `DomainResult` (sync) or `Task<DomainResult>` (async)
- **Visibility:** public, static or instance, `DeclaredOnly` (not inherited)
- **Dispatch key:** `CommandEnvelope.CommandType` matched to `TCommand.Name` (the CLR type name)

### Apply Method Discovery Rules

Apply methods on the **state class** (not the aggregate) must follow:
- **Name:** exactly `Apply` (case-sensitive, ordinal)
- **Parameters:** exactly 1 — `(TEvent)` where TEvent is the event type
- **Return:** `void`
- **Visibility:** public instance
- **Dispatch key:** event type name matched to `TEvent.Name`

### Architecture Constraints

- **D3:** Domain errors as events (DomainResult.Rejection), infrastructure errors as exceptions. Handle never throws for domain logic.
- **D7:** Server invokes domain service via `DaprClient.InvokeMethodAsync` — interface MUST accept `object?` state, not generic.
- **D12:** All ULID fields are `string`-typed.
- **FR21:** Pure function contract: `(Command, CurrentState?) -> DomainResult`. EventStore owns all metadata enrichment.
- **FR48:** Convention-based DAPR resource naming with EventStoreAggregate inheritance.
- **Rule 17:** Convention-derived resource names are kebab-case; suffix stripping automatic; attribute overrides validated at startup.
- **SEC-1:** EventStore owns ALL envelope metadata. Domain services return ONLY event payloads.
- **UX-DR19:** XML documentation on all public types.
- **UX-DR20:** Minimal public surface area — only developer-facing types public.

### XML Documentation Audit Points

Verify XML docs exist and are accurate on:
- `IDomainProcessor` — interface summary, ProcessAsync method
- `DomainProcessorBase<TState>` — class summary, typeparam, ProcessAsync, HandleAsync
- `EventStoreAggregate<TState>` — class summary, typeparam, ProcessAsync, OnConfiguring
- `NamingConventionEngine` — class summary, all 8 public methods
- `EventStoreDomainAttribute` — class summary, constructor, DomainName property

### Public API Surface Audit (UX-DR20)

Types that SHOULD be public in Client package:
- `IDomainProcessor` — developer implements this (low-level)
- `DomainProcessorBase<TState>` — developer inherits this (mid-level)
- `EventStoreAggregate<TState>` — developer inherits this (high-level, recommended)
- `EventStoreProjection<TReadModel>` — developer inherits this (projections)
- `NamingConventionEngine` — developer queries resource names
- `EventStoreDomainAttribute` — developer applies to override naming
- `EventStoreServiceCollectionExtensions` — developer calls AddEventStore()
- `EventStoreHostExtensions` — developer calls UseEventStore()
- `EventStoreActivationContext` — developer inspects activation results
- `EventStoreDomainActivation` — developer inspects per-domain activation

Types that MUST be internal/private:
- `AggregateMetadata` (private sealed record in EventStoreAggregate)
- `HandleMethodInfo` (private sealed record in EventStoreAggregate)
- `AssemblyScanner` (internal, used by registration)
- Configuration types if not developer-facing

### Previous Story Intelligence (Story 1.2 & 1.3)

**Story 1.2** (in review): Added MessageId to CommandEnvelope. ~60+ construction sites updated. Key learning: named arguments at all construction sites. This story does NOT need to touch CommandEnvelope construction sites.

**Story 1.3** (done): Added MessageType value object and Hexalith.Commons.UniqueIds. Contracts package now has external NuGet dependency. 44 new tests.

**Git intelligence:** Latest commit `493bcd8` merged Epic 1 Stories 1.1-1.3. All Tier 1 tests were green (634+ tests).

### What Could Go Wrong

1. **Missing XML docs** — some public methods may lack `<summary>`, `<param>`, `<returns>`, `<typeparam>` tags
2. **Incorrect visibility** — internal implementation types accidentally public
3. **Test regressions** — Story 1.2 is still in review with uncommitted test changes (6 modified test files in git status). These may need to be committed or stashed before running tests
4. **Build warnings** — `TreatWarningsAsErrors` is enabled; any missing XML doc warnings will fail the build

### Git Status Warning

Git status shows 6 modified test files from Story 1.2 (still in review):
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPersisterTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/PersistThenPublishResilienceTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Logging/LogLevelConventionTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Logging/StructuredLoggingCompletenessTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterOriginTracingTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Pipeline/LoggingBehaviorTests.cs`

These are Server.Tests (Tier 2) and should not affect Tier 1 test execution, but verify `dotnet build` succeeds with these changes present.

### Standards

- **Assertions:** `Assert.Equal` / `Assert.True` / `Assert.Throws` (xUnit). Don't mix Shouldly into Client.Tests.
- **Braces:** Egyptian/K&R for records and one-liners per existing code.
- **Run:** `dotnet test tests/Hexalith.EventStore.Client.Tests/` + `dotnet test tests/Hexalith.EventStore.Sample.Tests/`

### Project Structure Notes

- `src/Hexalith.EventStore.Client/Handlers/` — IDomainProcessor.cs, DomainProcessorBase.cs
- `src/Hexalith.EventStore.Client/Aggregates/` — EventStoreAggregate.cs, EventStoreProjection.cs
- `src/Hexalith.EventStore.Client/Conventions/` — NamingConventionEngine.cs
- `src/Hexalith.EventStore.Client/Attributes/` — EventStoreDomainAttribute.cs
- `src/Hexalith.EventStore.Client/Registration/` — ServiceCollectionExtensions, HostExtensions, ActivationContext
- `src/Hexalith.EventStore.Client/Discovery/` — AssemblyScanner.cs
- `samples/Hexalith.EventStore.Sample/Counter/` — CounterAggregate.cs, State/CounterState.cs
- `tests/Hexalith.EventStore.Client.Tests/Aggregates/` — EventStoreAggregateTests.cs
- `tests/Hexalith.EventStore.Client.Tests/Handlers/` — DomainProcessorTests.cs
- `tests/Hexalith.EventStore.Client.Tests/Conventions/` — NamingConventionEngineTests.cs
- `tests/Hexalith.EventStore.Sample.Tests/Counter/` — CounterAggregateTests.cs

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Epic 1, Story 1.4]
- [Source: _bmad-output/planning-artifacts/architecture.md — D3, D7, D12, FR21, FR48, Rule 17, SEC-1, UX-DR19, UX-DR20]
- [Source: src/Hexalith.EventStore.Client/Handlers/IDomainProcessor.cs — non-generic contract]
- [Source: src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs — typed state base]
- [Source: src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs — reflection-based discovery]
- [Source: src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs — kebab naming]
- [Source: src/Hexalith.EventStore.Client/Attributes/EventStoreDomainAttribute.cs — attribute override]
- [Source: _bmad-output/implementation-artifacts/1-2-command-types-domainresult-and-error-contract.md — Story 1.2 construction site learnings]
- [Source: _bmad-output/implementation-artifacts/1-3-messagetype-value-object-and-hexalith-commons-ulid-integration.md — Story 1.3 done]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
