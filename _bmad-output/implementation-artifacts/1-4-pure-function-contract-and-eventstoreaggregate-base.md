# Story 1.4: Pure Function Contract & EventStoreAggregate Base

Status: done
Complexity: XS (audit-and-verify — no new code expected beyond XML doc fixes)

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

6. **Done definition:** All public types in the Client package (IDomainProcessor, DomainProcessorBase<TState>, EventStoreAggregate<TState>, EventStoreProjection<TReadModel>, NamingConventionEngine, EventStoreDomainAttribute, registration extensions, activation types) verified complete. XML doc generation enabled and all public types documented. All Tier 1 tests green.

## Tasks / Subtasks

- [x] Task 1: Audit IDomainProcessor & DomainProcessorBase<TState> (AC: #1)
  - [x] 1.1 Verify `IDomainProcessor` at `src/Hexalith.EventStore.Client/Handlers/IDomainProcessor.cs` — contract: `ProcessAsync(CommandEnvelope, object?) -> Task<DomainResult>`
  - [x] 1.2 Verify `DomainProcessorBase<TState>` at `src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs` — typed state casting, JsonElement deserialization, delegates to abstract `HandleAsync(CommandEnvelope, TState?)`
  - [x] 1.3 Verify XML docs on both types are complete (summary, param, returns, typeparam)
  - [x] 1.4 Run `DomainProcessorTests.cs` — confirm all 7 tests pass

- [x] Task 2: Audit EventStoreAggregate<TState> (AC: #2)
  - [x] 2.1 Verify `EventStoreAggregate<TState>` at `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs` — implements IDomainProcessor, reflection-based Handle/Apply discovery, metadata caching, state rehydration from JsonElement/typed/IEnumerable
  - [x] 2.2 Verify Handle method discovery: method name `Handle`, 2 params `(TCommand, TState?)`, returns `DomainResult` or `Task<DomainResult>`, public static or instance, `BindingFlags.DeclaredOnly`
  - [x] 2.3 Verify Apply method discovery: method name `Apply`, 1 param `(TEvent)`, returns `void`, public instance, discovered on `TState` type
  - [x] 2.4 Verify Handle discovery silently skips methods with wrong return type (e.g., `string` instead of `DomainResult`) — this is by-design behavior. If no test covers this, you MUST add one to `EventStoreAggregateTests.cs` (do not mark complete via code inspection alone)
  - [x] 2.5 Verify command dispatch: payload deserialized from `CommandEnvelope.Payload` (byte[]) via `JsonSerializer.Deserialize`, dispatched to matching Handle method by `CommandType` name
  - [x] 2.6 Verify state rehydration: supports null, typed TState, JsonElement object, JsonElement array (event replay with eventTypeName + payload), JsonElement null, IEnumerable (typed event replay)
  - [x] 2.7 Verify metadata caching uses `ConcurrentDictionary<Type, AggregateMetadata>` for thread-safe per-aggregate-type caching
  - [x] 2.8 Verify `OnConfiguring` virtual method and `InvokeOnConfiguring` internal method are exercised by existing tests — `CounterAggregateTests.UseEventStore_SampleAssembly_ActivatesCounterWithConventionDerivedResourceNames` triggers cascade resolution which calls `InvokeOnConfiguring`. Confirm this path is covered.
  - [x] 2.9 Verify XML docs on class, ProcessAsync, OnConfiguring
  - [x] 2.10 Run `EventStoreAggregateTests.cs` — confirm all 35+ tests pass

- [x] Task 3: Audit NamingConventionEngine & EventStoreDomainAttribute (AC: #3)
  - [x] 3.1 Verify `NamingConventionEngine` at `src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs` — PascalCase-to-kebab, suffix stripping (Aggregate/Projection/Processor), attribute override, kebab validation regex `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`, max 64 chars, ConcurrentDictionary cache
  - [x] 3.2 Verify `EventStoreDomainAttribute` at `src/Hexalith.EventStore.Client/Attributes/EventStoreDomainAttribute.cs` — non-empty validation, `AllowMultiple = false`, `Inherited = false`
  - [x] 3.3 Verify resource name derivation: `GetStateStoreName("{domain}-eventstore")`, `GetPubSubTopic("{tenantId}.{domain}.events")`, `GetCommandEndpoint("{domain}-commands")`
  - [x] 3.4 Verify XML docs on all public methods
  - [x] 3.5 Run `NamingConventionEngineTests.cs` — confirm all 40+ tests pass

- [x] Task 4: Audit public API surface and XML documentation (AC: #4)
  - [x] 4.1 List all public types in `Hexalith.EventStore.Client` project by grepping: `grep -rn "public class\|public interface\|public record\|public enum\|public abstract\|public sealed\|public static class\|public partial" src/Hexalith.EventStore.Client/` — verify only developer-facing types are public (UX-DR20). Cross-reference grep results against the 10-type Public API Surface list in Dev Notes — if count differs, update the Public API Surface list to match reality and explain each addition/removal in Completion Notes
  - [x] 4.2 Verify `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is set in `src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj` OR inherited from `Directory.Build.props`. Also confirm CS1591 is NOT in `<NoWarn>` at either level. Without this, XML doc completeness is not enforced by the build. **If not set:** enable it ONLY for `Hexalith.EventStore.Client.csproj` and fix all resulting CS1591 warnings within that project. Do not enable it in other .csproj files — the blast radius would cascade warnings across the solution.
  - [x] 4.3 Verify XML documentation on ALL public types, methods, and properties (UX-DR19) — includes `EventStoreProjection<TReadModel>` at `src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs`. Verify EACH of the 10 types in the XML Documentation Audit Points list by reading the source file. Do not spot-check — verify all 10 exhaustively and list each one checked in Completion Notes.
  - [x] 4.4 Verify internal types: AggregateMetadata, HandleMethodInfo should be internal/private (implementation details)
  - [x] 4.5 Fix any missing XML docs — for each fix, note the file and what was added/changed. Common gaps: missing `<typeparam>`, missing `<returns>`, missing `<param>` on public methods
  - [x] 4.6 Fix any incorrect visibility — if an internal type is accidentally public, change to internal and verify build still compiles

- [x] Task 5: Verify Counter sample demonstrates the pattern (AC: #1, #2, #3)
  - [x] 5.1 Verify `CounterAggregate` at `samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs` — inherits `EventStoreAggregate<CounterState>`, static Handle methods for IncrementCounter/DecrementCounter/ResetCounter, returns DomainResult
  - [x] 5.2 Verify `CounterState` at `samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs` — Apply methods for CounterIncremented/CounterDecremented/CounterReset
  - [x] 5.3 Run `CounterAggregateTests.cs` — confirm all 7 tests pass including DI resolution and UseEventStore activation

- [x] Task 6: Full Tier 1 regression verification (AC: #5, #6)
  - [x] 6.1 Run `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings, zero errors
  - [x] 6.2 Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` — baseline: ~261 tests (from Story 1.2 run)
  - [x] 6.3 Run `dotnet test tests/Hexalith.EventStore.Client.Tests/` — baseline: ~280 tests (from Story 1.2 run)
  - [x] 6.4 Run `dotnet test tests/Hexalith.EventStore.Sample.Tests/` — baseline: ~29 tests (from Story 1.2 run)
  - [x] 6.5 Run `dotnet test tests/Hexalith.EventStore.Testing.Tests/` — baseline: ~64 tests (from Story 1.2 run)
  - [x] 6.6 Record actual test counts in Completion Notes — total baseline is 634+ from Story 1.2. If count is LOWER than baseline, this is a regression — investigate and fix before proceeding. If HIGHER, document which new tests were added and why.

## Dev Notes

### Scope Summary

This is an **audit-and-verify** story, NOT greenfield. All four components are fully implemented and tested under earlier stories (16-2 through 16-8 in the old numbering). The dev agent must verify completeness, fix any XML doc gaps, verify public API surface, and confirm all tests pass.

### Pre-Flight Check

Before starting any tasks, run `git log --oneline -5 -- src/Hexalith.EventStore.Client/` to check for changes since commit `493bcd8` (Epic 1 Stories 1.1-1.3 merge). If the Client package was modified after that commit, re-read the changed files and adjust verification expectations — the Existing Implementation State table may be stale.

### Why 30+ Subtasks for an XS Story

The verbose task list is intentional. This story will be executed by an LLM dev agent, not a human. LLM agents are prone to "audit theater" — claiming verification without actually checking. Each explicit subtask forces mechanical verification of a specific property. The subtasks are fast to execute (mostly file reads and test runs) but must not be skipped or hand-waved.

### EventStoreProjection Scope

`EventStoreProjection<TReadModel>` at `src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs` is **in scope for the public API surface and XML documentation audit** (Task 4) because it's a public developer-facing type in the Client package. It is NOT in scope for functional verification — projections are covered by Epic 11. Verify it has XML docs and correct visibility only.

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
- `EventStoreProjection<TReadModel>` — class summary, typeparam, Project, ProjectFromJson (XML doc audit only, not functional)
- `NamingConventionEngine` — class summary, all 8 public methods
- `EventStoreDomainAttribute` — class summary, constructor, DomainName property
- `EventStoreServiceCollectionExtensions` — AddEventStore methods
- `EventStoreHostExtensions` — UseEventStore method
- `EventStoreActivationContext` — class summary, Activations property
- `EventStoreDomainActivation` — class summary, all public properties

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

These are Server.Tests (Tier 2) and should not affect Tier 1 test execution, but verify `dotnet build` succeeds with these changes present. **If `dotnet build` fails due to these modified Server.Tests files, run `git stash push -- tests/Hexalith.EventStore.Server.Tests/` to stash ONLY those files (do NOT use bare `git stash` which stashes all changes). After build/test, restore with `git stash pop`. Do NOT modify Server.Tests files — they belong to Story 1.2.**

**If Story 1.2 has been merged** since this story was created (check `git log --oneline -3`), the stash guidance is moot and test baselines may have shifted. Use the latest main branch test counts as the new baseline.

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

GPT-5.4

### Debug Log References

Pre-flight check: Client package had changes after 493bcd8 (commits e85f64d, 0f446b7). Verified actual code state before auditing.

2026-03-15 code review follow-up: fixed UTF-8 extension size accounting in `SubmitCommandRequestValidator`, aligned `CommandEnvelopeBuilder` default `CorrelationId` semantics with `MessageId`, and re-ran Tier 1 regression plus full Release build.

### Completion Notes List

**Task 1:** IDomainProcessor and DomainProcessorBase<TState> verified complete. Contract matches AC#1. XML docs complete on both types. DomainProcessorTests: 7/7 passed.

**Task 2:** EventStoreAggregate<TState> verified complete. 37/37 tests passed (baseline was 35+). Subtask 2.4: No existing test covered wrong-return-type Handle method skipping. Added 2 new tests: `ProcessAsync_HandleMethodWithWrongReturnType_IsSilentlySkipped` and `ProcessAsync_WrongReturnTypeAggregate_ValidHandler_StillWorks`. Both pass, confirming the discovery correctly skips Handle methods returning non-DomainResult types.

**Task 3:** NamingConventionEngine verified complete. All 8 public methods have XML docs. 94/94 tests passed (baseline 40+). EventStoreDomainAttribute verified: non-empty validation, AllowMultiple=false, Inherited=false.

**Task 4 — Public API Surface Audit:**
- Found 15 public types (after AssemblyScanner fix) vs story's 10-type list. The 10 expected types are present.
- 5 additional public types not in story's list (added since story was written): QueryContractResolver, EventStoreOptions, EventStoreDomainOptions, DomainKind, IProjectionChangedBroadcaster, IProjectionChangeNotifier, IEventStoreProjection, DiscoveryResult, DiscoveredDomain. All are developer-facing types that legitimately need public visibility.
- **Visibility fix:** AssemblyScanner changed from `public` to `internal` per story requirement (only used within Client project; InternalsVisibleTo already set for test project). Build succeeds with 0 warnings.
- **GenerateDocumentationFile:** Was NOT set in Client.csproj. Added `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. Build compiles with 0 CS1591 warnings — all public types already had complete XML docs.
- AggregateMetadata and HandleMethodInfo are `private sealed record` inside EventStoreAggregate — correct.

**Task 4.3 — XML Documentation Audit (all 10 types verified exhaustively):**
1. IDomainProcessor — summary, ProcessAsync (param, returns) ✅
2. DomainProcessorBase<TState> — summary, typeparam, ProcessAsync (inheritdoc), HandleAsync (param, returns) ✅
3. EventStoreAggregate<TState> — summary, typeparam, ProcessAsync (inheritdoc), OnConfiguring (param, remarks) ✅
4. EventStoreProjection<TReadModel> — summary, typeparam, Notifier, Logger, TenantId, OnConfiguring, InvokeOnConfiguring, Project (param, returns), ProjectFromJson (param, returns) ✅
5. NamingConventionEngine — summary, GetDomainName (2 overloads), GetQueryTypeName (2 overloads), GetStateStoreName, GetPubSubTopic, GetCommandEndpoint, GetProjectionChangedTopic, ValidateKebabCase — all documented ✅
6. EventStoreDomainAttribute — summary, constructor (param, exception), DomainName ✅
7. EventStoreServiceCollectionExtensions — summary, AddEventStoreClient (typeparam, param, returns), AddEventStore (4 overloads, all documented) ✅
8. EventStoreHostExtensions — summary, UseEventStore (param, returns, exception) ✅
9. EventStoreActivationContext — summary, IsActivated, Activations (exception) ✅
10. EventStoreDomainActivation — summary, all 7 record params documented ✅

**Task 4.5:** No missing XML docs found. All public types, methods, and properties had complete documentation.

**Task 5:** CounterAggregate and CounterState verified. CounterAggregateTests: 8/8 passed (baseline was 7; 8th test was added previously for DI resolution).

**Task 6 — Tier 1 Regression:**
- Contracts.Tests: 266 (current baseline)
- Client.Tests: 286 (current baseline; includes prior wrong-return-type tests)
- Sample.Tests: 32 (current baseline)
- Testing.Tests: 67 (+2 builder semantics tests, +1 existing baseline growth)
- **Total: 651**
- Full Release build: 0 warnings, 0 errors

**Code review fixes applied (2026-03-15):**
- Fixed UTF-8 byte-budget validation for `SubmitCommandRequest.Extensions` so multi-byte characters are measured by actual UTF-8 payload size rather than `charCount * 2`.
- Added validator regression coverage proving oversized UTF-8 extension payloads are rejected.
- Updated `CommandEnvelopeBuilder` so default `CorrelationId` follows `MessageId` unless explicitly overridden, matching command submission semantics.
- Added testing coverage for default and explicit correlation-id behavior.
- Reconciled story metadata to distinguish verified historical implementation files from review follow-up fixes and documented unrelated workspace changes as out of scope.

**Review scope note:** unrelated workspace edits in Story 1.2 artifacts and other non-1.4 files were observed during review and left untouched.

### Implementation Plan

Audit-and-verify with focused code and test changes:
1. Added `GenerateDocumentationFile=true` to Client.csproj (Task 4.2)
2. Changed AssemblyScanner from `public` to `internal` (Task 4.6)
3. Added 2 tests for wrong-return-type Handle method skipping (Task 2.4)
4. Fixed UTF-8 extension size validation and added a validator regression test.
5. Fixed `CommandEnvelopeBuilder` default correlation-id semantics and added regression tests.

### File List

- Verified existing implementation files:
  - `src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj` — XML doc generation enabled.
  - `src/Hexalith.EventStore.Client/Discovery/AssemblyScanner.cs` — `AssemblyScanner` visibility is internal.
  - `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs` — wrong-return-type handler coverage present.
- Code review follow-up fixes:
  - `src/Hexalith.EventStore.CommandApi/Validation/SubmitCommandRequestValidator.cs` — use actual UTF-8 byte counts for extension-size validation.
  - `tests/Hexalith.EventStore.Server.Tests/Validation/ValidatorConsistencyTests.cs` — added UTF-8 extension-size regression coverage.
  - `src/Hexalith.EventStore.Testing/Builders/CommandEnvelopeBuilder.cs` — default `CorrelationId` now follows `MessageId` unless explicitly overridden.
  - `tests/Hexalith.EventStore.Testing.Tests/Builders/CommandEnvelopeBuilderTests.cs` — added correlation-id semantic regression coverage.

### Change Log

- 2026-03-15: Story 1.4 audit-and-verify completed. Enabled XML doc generation for Client package, fixed AssemblyScanner visibility (public→internal), added 2 tests for Handle method wrong-return-type skipping. All 636 Tier 1 tests green.
- 2026-03-15: Code review follow-up fixed UTF-8 extension-size accounting, aligned `CommandEnvelopeBuilder` default correlation-id behavior with `MessageId`, added regression tests, and closed the story as done.
