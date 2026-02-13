# Story 1.4: Testing Package - In-Memory Test Helpers

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **domain service developer**,
I want a Testing NuGet package providing InMemoryStateManager, FakeDomainServiceInvoker, test builders (CommandEnvelopeBuilder, AggregateIdentityBuilder), and assertion helpers,
so that I can unit-test my domain processor pure functions with zero DAPR runtime dependency (FR45).

## Prerequisites

Before starting this story, the dev agent MUST verify:

- [x] Story 1.3 completed (Client package with IDomainProcessor, DomainProcessorBase, AddEventStoreClient)
- [x] Story 1.2 completed (Contracts package with EventEnvelope, CommandEnvelope, AggregateIdentity, DomainResult, IRejectionEvent, IEventPayload)
- [x] `dotnet build` succeeds with zero errors/warnings on current main branch
- [x] Testing project exists at `src/Hexalith.EventStore.Testing/` with BuildVerification.cs placeholder and ProjectReferences to Contracts + Server

## Acceptance Criteria

1. **InMemoryStateManager** - Given the Testing package is referenced, When I use InMemoryStateManager in a unit test, Then it simulates `Dapr.Actors.Runtime.IActorStateManager` without a DAPR sidecar. It supports `GetStateAsync`, `SetStateAsync`, `RemoveStateAsync`, `ContainsStateAsync`, `SaveStateAsync`, and `TryGetStateAsync`. All state is stored in an in-memory dictionary. `SaveStateAsync` commits pending changes atomically (mimicking DAPR actor turn semantics). State is inspectable for test assertions.

2. **FakeDomainServiceInvoker** - Given the Testing package is referenced, When I use FakeDomainServiceInvoker in a unit test, Then I can configure canned `DomainResult` responses for specific command types, tenant+domain combinations, or any command. It implements an `IDomainServiceInvoker` interface (defined in this story in the Server project as a minimal contract) enabling injection of predictable responses without DAPR service invocation.

3. **CommandEnvelopeBuilder** - Given the Testing package is referenced, When I use `new CommandEnvelopeBuilder()` in a test, Then it creates valid `CommandEnvelope` instances with sensible defaults (auto-generated TenantId, Domain, AggregateId, CommandType, Payload, CorrelationId, UserId). Each property can be overridden via fluent `.WithTenantId("acme")` methods. `.Build()` returns the constructed `CommandEnvelope`.

4. **AggregateIdentityBuilder** - Given the Testing package is referenced, When I use `new AggregateIdentityBuilder()` in a test, Then it creates valid `AggregateIdentity` instances with sensible defaults (lowercase tenant, lowercase domain, valid aggregate ID). Each property can be overridden via fluent methods. `.Build()` returns the constructed `AggregateIdentity`.

5. **EventEnvelopeBuilder** - Given the Testing package is referenced, When I use `new EventEnvelopeBuilder()` in a test, Then it creates valid `EventEnvelope` instances with sensible defaults (valid EventMetadata with all 11 fields, non-empty payload, optional extensions). Each field can be overridden via fluent methods. `.Build()` returns the constructed `EventEnvelope`.

6. **Assertion Helpers** - Given the Testing package is referenced, When I use assertion helpers in a test, Then:
   - `DomainResultAssertions.ShouldBeSuccess(result, expectedCount)` verifies the result is successful with the expected event count
   - `DomainResultAssertions.ShouldBeRejection(result)` verifies the result contains rejection events
   - `DomainResultAssertions.ShouldBeNoOp(result)` verifies the result has no events
   - `DomainResultAssertions.ShouldContainEvent<TEvent>(result)` verifies the result contains an event of the specified type
   - `EventSequenceAssertions.ShouldHaveSequentialNumbers(envelopes)` verifies event envelopes have strictly sequential sequence numbers
   - `EventEnvelopeAssertions.ShouldHaveValidMetadata(envelope)` verifies all 11 metadata fields are populated

7. **No DAPR Runtime Required** - Given all test helpers are used, When tests execute, Then no DAPR sidecar, no containers, and no external infrastructure is required. All helpers are pure in-memory implementations.

8. **Clean Build** - Given all types are implemented, When I run `dotnet build`, Then the solution builds with zero errors and zero warnings, and `dotnet pack` produces a valid Testing .nupkg.

9. **Unit Tests** - Given the Testing types are implemented, When I run `dotnet test`, Then tests verify:
   - InMemoryStateManager stores and retrieves state correctly
   - InMemoryStateManager SaveStateAsync commits pending changes
   - InMemoryStateManager TryGetStateAsync returns false for missing keys
   - FakeDomainServiceInvoker returns configured canned responses
   - CommandEnvelopeBuilder produces valid CommandEnvelope with defaults
   - CommandEnvelopeBuilder fluent overrides work correctly
   - AggregateIdentityBuilder produces valid AggregateIdentity with defaults
   - EventEnvelopeBuilder produces valid EventEnvelope with defaults
   - All assertion helpers correctly pass for valid inputs and fail for invalid inputs

## Tasks / Subtasks

### Task 1: Create IDomainServiceInvoker interface in Server project (AC: #2)

- [x] 1.1 Create `src/Hexalith.EventStore.Server/DomainServices/IDomainServiceInvoker.cs` — interface with `Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState)` method
- [x] 1.2 Delete `src/Hexalith.EventStore.Server/BuildVerification.cs` (replaced by real type)

**Verification:** `dotnet build` succeeds; interface is minimal and focused

### Task 2: Create InMemoryStateManager (AC: #1)

- [x] 2.1 Create `src/Hexalith.EventStore.Testing/Fakes/InMemoryStateManager.cs` — implements `Dapr.Actors.Runtime.IActorStateManager`
- [x] 2.2 Implement in-memory dictionary storage with pending/committed state tracking
- [x] 2.3 Implement `SaveStateAsync` to commit pending changes atomically
- [x] 2.4 Expose `CommittedState` property for test assertions (read-only dictionary)

**Verification:** InMemoryStateManager compiles; all IActorStateManager methods implemented

### Task 3: Create FakeDomainServiceInvoker (AC: #2)

- [x] 3.1 Create `src/Hexalith.EventStore.Testing/Fakes/FakeDomainServiceInvoker.cs` — implements `IDomainServiceInvoker`
- [x] 3.2 Implement canned response configuration via `SetupResponse(string commandType, DomainResult result)` and `SetupDefaultResponse(DomainResult result)`
- [x] 3.3 Track invocation history for assertions (`Invocations` list)

**Verification:** FakeDomainServiceInvoker compiles; canned responses work

### Task 4: Create test builders (AC: #3, #4, #5)

- [x] 4.1 Create `src/Hexalith.EventStore.Testing/Builders/CommandEnvelopeBuilder.cs` — fluent builder with sensible defaults
- [x] 4.2 Create `src/Hexalith.EventStore.Testing/Builders/AggregateIdentityBuilder.cs` — fluent builder with sensible defaults
- [x] 4.3 Create `src/Hexalith.EventStore.Testing/Builders/EventEnvelopeBuilder.cs` — fluent builder with sensible defaults

**Verification:** All builders compile; `.Build()` produces valid instances

### Task 5: Create assertion helpers (AC: #6)

- [x] 5.1 Create `src/Hexalith.EventStore.Testing/Assertions/DomainResultAssertions.cs` — static assertion methods for DomainResult
- [x] 5.2 Create `src/Hexalith.EventStore.Testing/Assertions/EventSequenceAssertions.cs` — static assertion for sequential sequence numbers
- [x] 5.3 Create `src/Hexalith.EventStore.Testing/Assertions/EventEnvelopeAssertions.cs` — static assertion for valid metadata fields

**Verification:** All assertions compile; they throw on invalid input with clear messages

### Task 6: Unit tests (AC: #9)

- [x] 6.1 Create test project or add tests for InMemoryStateManager (get/set/remove/contains/save/tryget)
- [x] 6.2 Tests for FakeDomainServiceInvoker (canned responses, invocation tracking)
- [x] 6.3 Tests for all builders (defaults produce valid instances, fluent overrides work)
- [x] 6.4 Tests for all assertion helpers (pass for valid, fail for invalid)

**Verification:** All tests pass; `dotnet test` succeeds across full solution

### Task 7: Build and pack verification (AC: #8)

- [x] 7.1 Delete `src/Hexalith.EventStore.Testing/BuildVerification.cs`
- [x] 7.2 Run `dotnet build` — zero errors, zero warnings
- [x] 7.3 Run `dotnet pack` — Testing .nupkg produced
- [x] 7.4 Verify Testing.csproj has correct dependencies (Contracts + Server + Shouldly + NSubstitute + xunit)

## Dev Notes

### Technical Design Decisions

**InMemoryStateManager — simulating DAPR actor state (FR45):**
- Implements `Dapr.Actors.Runtime.IActorStateManager` (from the `Dapr.Actors` NuGet package, version 1.16.1)
- The Testing project gets `Dapr.Actors` transitively through its Server ProjectReference (Server has `Dapr.Actors` 1.16.1)
- Uses two dictionaries: `_pendingState` (uncommitted changes) and `_committedState` (committed state)
- `SetStateAsync` writes to `_pendingState`; `GetStateAsync` checks `_pendingState` first, then `_committedState`
- `SaveStateAsync` atomically moves all `_pendingState` entries to `_committedState` and clears `_pendingState`
- `RemoveStateAsync` marks key for removal in `_pendingState`; removal is applied on `SaveStateAsync`
- Exposes `CommittedState` as `IReadOnlyDictionary<string, object>` for test assertions
- `TryGetStateAsync` returns `ConditionalValue<T>` (DAPR type) — true with value if found, false if not
- **IActorStateManager has additional methods** that need stub implementation: `GetStateNamesAsync`, `ClearCacheAsync`, `AddStateAsync`, `SetStateAsync` with TTL overloads. Implement all required interface members; stub TTL-related overloads to ignore TTL (store state normally)
- **Note:** `AddStateAsync` should throw `InvalidOperationException` if key already exists (consistent with DAPR behavior)
- Thread safety is NOT required (DAPR actors are single-threaded turn-based)

**FakeDomainServiceInvoker — canned domain service responses:**
- Implements `IDomainServiceInvoker` (created in this story in Server project)
- `IDomainServiceInvoker` has a single method: `Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState)`
- This mirrors the same signature as `IDomainProcessor` but represents the server-side invocation contract (the server calls domain services via DAPR; in tests, we fake this)
- Configuration: `SetupResponse(string commandType, DomainResult result)` — return specific result for a command type
- Configuration: `SetupDefaultResponse(DomainResult result)` — fallback for unmatched command types
- Configuration: `SetupResponse(string tenantId, string domain, DomainResult result)` — return result for tenant+domain combo
- Tracking: `Invocations` property (`IReadOnlyList<CommandEnvelope>`) — records every command passed to `InvokeAsync`
- If no response is configured and no default exists, throw `InvalidOperationException` with message identifying the unconfigured command type

**Builders — fluent test data creation:**
- **CommandEnvelopeBuilder** defaults: TenantId="test-tenant", Domain="test-domain", AggregateId="test-agg-001", CommandType="TestCommand", Payload=UTF8 bytes of "{}", CorrelationId=new Guid, UserId="test-user"
- **AggregateIdentityBuilder** defaults: TenantId="test-tenant", Domain="test-domain", AggregateId="test-agg-001"
- **EventEnvelopeBuilder** defaults: valid EventMetadata (AggregateId="test-tenant:test-domain:test-agg-001", SequenceNumber=1, Timestamp=DateTimeOffset.UtcNow, all 11 fields populated), Payload=UTF8 bytes of "{}", Extensions=null
- All builders follow the pattern:

```csharp
public sealed class CommandEnvelopeBuilder
{
    private string _tenantId = "test-tenant";
    private string _domain = "test-domain";
    // ... other fields with defaults

    public CommandEnvelopeBuilder WithTenantId(string tenantId) { _tenantId = tenantId; return this; }
    public CommandEnvelopeBuilder WithDomain(string domain) { _domain = domain; return this; }
    // ... other With* methods

    public CommandEnvelope Build() => new(
        _tenantId, _domain, _aggregateId, _commandType,
        _payload, _correlationId, _causationId, _userId, _extensions);
}
```

- Builders are NOT immutable (mutable for fluent API simplicity); each `With*` returns `this`
- Builder defaults must produce instances that pass all validation in the Contracts types (AggregateIdentity regex, CommandEnvelope required fields)

**Assertion helpers — static methods with xUnit Assert:**
- Use `Assert.*` methods (project pattern — existing tests use Assert, not Shouldly)
- Static classes with descriptive assertion methods
- Throw `Xunit.Sdk.XunitException` (via Assert) on failure with clear messages
- `ShouldContainEvent<TEvent>` uses LINQ `.OfType<TEvent>()` on `result.Events`
- `ShouldHaveSequentialNumbers` verifies envelopes have SequenceNumber values 1, 2, 3, ... or N, N+1, N+2, ...
- `ShouldHaveValidMetadata` checks all 11 fields are non-null/non-empty and Payload is non-null

### Project Structure Notes

**Target file structure (architecture-mandated):**
```
src/Hexalith.EventStore.Testing/
├── Hexalith.EventStore.Testing.csproj  # Contracts + Server + Shouldly + NSubstitute
├── Builders/
│   ├── CommandEnvelopeBuilder.cs       # Fluent builder for CommandEnvelope
│   ├── AggregateIdentityBuilder.cs     # Fluent builder for AggregateIdentity
│   └── EventEnvelopeBuilder.cs         # Fluent builder for EventEnvelope
├── Fakes/
│   ├── InMemoryStateManager.cs         # Fake IActorStateManager (from Dapr.Actors.Runtime)
│   └── FakeDomainServiceInvoker.cs     # Fake IDomainServiceInvoker (from Server)
└── Assertions/
    ├── DomainResultAssertions.cs        # DomainResult assertion helpers
    ├── EventSequenceAssertions.cs       # Event sequence validation
    └── EventEnvelopeAssertions.cs       # Envelope metadata validation

src/Hexalith.EventStore.Server/
├── DomainServices/
│   └── IDomainServiceInvoker.cs        # NEW: Minimal interface for domain service invocation
```

**Files to DELETE:**
- `src/Hexalith.EventStore.Testing/BuildVerification.cs` (Story 1.1 placeholder — replace with real types)
- `src/Hexalith.EventStore.Server/BuildVerification.cs` (Story 1.1 placeholder — replace with real type)

**Alignment with architecture:**
- Feature folder convention: Builders/, Fakes/, Assertions/ (matches architecture's Builders/, Fakes/, Assertions/ layout)
- One public type per file
- File name = type name
- Testing depends on Contracts + Server (matches architecture dependency boundaries)

### Previous Story 1.3 Intelligence

**Key learnings from Story 1.3 implementation:**
- Record types used for all immutable value objects in Contracts
- `DomainResult` wraps `IReadOnlyList<IEventPayload>` with factory methods (Success, Rejection, NoOp) and mixed-event validation
- `CommandEnvelope` eagerly validates all required fields at construction (TenantId, Domain, AggregateId via AggregateIdentity; CommandType, Payload, CorrelationId, UserId)
- `IDomainProcessor` has single method: `Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)`
- File-scoped namespaces, XML doc comments on all public types/members
- `TreatWarningsAsErrors=true` — all warnings must be fixed
- CPM active — no `Version=` on PackageReference entries in .csproj
- Code review caught validation gaps in previous stories — be thorough
- Tests use `Assert.*` (xUnit), NOT Shouldly despite Shouldly being available
- Previous story created a dedicated test project (Client.Tests) for Client package tests
- Naming: `EventStoreServiceCollectionExtensions` (prefixed to avoid namespace collision)

**Existing Contracts types the Testing package builds on:**
- `Hexalith.EventStore.Contracts.Commands.CommandEnvelope` — constructor requires: TenantId, Domain, AggregateId, CommandType, Payload (byte[]), CorrelationId, CausationId (nullable), UserId, Extensions (nullable)
- `Hexalith.EventStore.Contracts.Identity.AggregateIdentity` — constructor requires: TenantId (lowercase alphanum+hyphens, max 64), Domain (lowercase alphanum+hyphens, max 64), AggregateId (alphanum+dots/hyphens/underscores, max 256)
- `Hexalith.EventStore.Contracts.Events.EventEnvelope` — constructor requires: EventMetadata, Payload (byte[]), Extensions (nullable)
- `Hexalith.EventStore.Contracts.Events.EventMetadata` — contains all 11 metadata fields (AggregateId, TenantId, Domain, SequenceNumber, Timestamp, CorrelationId, CausationId, UserId, DomainServiceVersion, EventTypeName, SerializationFormat)
- `Hexalith.EventStore.Contracts.Results.DomainResult` — factory methods: Success(events), Rejection(rejectionEvents), NoOp()
- `Hexalith.EventStore.Contracts.Events.IEventPayload` — marker interface for event payloads
- `Hexalith.EventStore.Contracts.Events.IRejectionEvent` — marker interface extending IEventPayload

### Git Intelligence

**Recent commits:**
- `ac8c77a` Merge PR #16 (Story 1.3 - Client Package)
- `fe4bf48` Story 1.3: Client Package - Domain Processor Contract & Registration
- `80a7f88` Story 1.2: Contracts Package - Event Envelope & Core Types (#15)

**Patterns from previous work:**
- PR-based workflow (feature branch -> PR -> merge to main)
- Comprehensive unit tests accompany all new types
- Commit message format: "Story X.Y: Description"
- Branch naming: `feature/story-X.Y-description`
- Code review catches validation gaps — be thorough
- XML doc comments required on all public API surface
- 158 existing tests across solution (Story 1.3 count)

### Critical Guardrails for Dev Agent

1. **InMemoryStateManager implements `Dapr.Actors.Runtime.IActorStateManager`** — the interface comes from `Dapr.Actors` package (1.16.1) via Server transitive dependency. Do NOT create a custom IActorStateManager interface
2. **IDomainServiceInvoker goes in Server project** — `src/Hexalith.EventStore.Server/DomainServices/IDomainServiceInvoker.cs`. Single method: `Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState)`. This is a minimal placeholder interface; the full implementation (DaprDomainServiceInvoker) comes in Story 3.5
3. **Builder defaults must pass validation** — CommandEnvelopeBuilder defaults must satisfy all CommandEnvelope constructor validation (non-empty strings, valid AggregateIdentity regex patterns). AggregateIdentityBuilder defaults must be lowercase alphanum+hyphens
4. **Use `Assert.*` for assertions** — existing codebase uses xUnit Assert, NOT Shouldly. Keep consistency. Shouldly is in the .csproj for the Testing NuGet consumers, not for internal assertions
5. **Feature folders ONLY** — Builders/, Fakes/, Assertions/. No Models/, Services/, Interfaces/, Helpers/ folders
6. **One public type per file** — filename must match type name exactly
7. **File-scoped namespaces** — `namespace Hexalith.EventStore.Testing.Builders;` etc.
8. **XML doc comments on ALL public types and members** — required for NuGet package documentation
9. **No `Version=` on PackageReference** — CPM manages versions via Directory.Packages.props
10. **Delete BuildVerification.cs from BOTH Testing AND Server projects** — they are Story 1.1 placeholders
11. **InMemoryStateManager is NOT thread-safe** — DAPR actors are single-threaded turn-based; no need for ConcurrentDictionary
12. **SaveStateAsync must be called to commit** — mimics DAPR actor behavior where state changes are pending until SaveStateAsync. This is critical for testing atomic write semantics (D1)
13. **FakeDomainServiceInvoker tracks invocations** — `Invocations` property lets tests verify the correct commands were sent to the domain service
14. **AddStateAsync throws if key exists** — mimics DAPR behavior; this is important for testing write-once event keys (enforcement rule #11)
15. **Testing .csproj should keep Shouldly and NSubstitute** — these are for consumers of the Testing NuGet package, not for internal use
16. **Test project for Testing package** — create tests either in a new `Hexalith.EventStore.Testing.Tests` project or within an existing test project. Follow the pattern from Story 1.3 (dedicated test project preferred)
17. **EventMetadata constructor parameters** — check the actual constructor of EventMetadata before building defaults. Read the file `src/Hexalith.EventStore.Contracts/Events/EventMetadata.cs` to get exact parameter names and types

### References

- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries - Testing directory: Builders/, Fakes/, Assertions/]
- [Source: _bmad-output/planning-artifacts/architecture.md#Core Architectural Decisions - D1: Event storage with IActorStateManager]
- [Source: _bmad-output/planning-artifacts/architecture.md#Communication Patterns - Actor Step 4: domain service invocation via IDomainServiceInvoker]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines - Rule #6: IActorStateManager for all actor state, Rule #11: write-once keys]
- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.4 - Acceptance criteria: InMemoryStateManager, FakeDomainServiceInvoker, builders, assertions]
- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.4 - FR45: Unit tests without DAPR runtime dependency]
- [Source: _bmad-output/implementation-artifacts/1-3-client-package-domain-processor-contract-and-registration.md - Previous story patterns and learnings]
- [Source: src/Hexalith.EventStore.Testing/Hexalith.EventStore.Testing.csproj - Current project dependencies: Contracts + Server + Shouldly + NSubstitute]
- [Source: src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj - Server dependencies: Contracts + Dapr.Client + Dapr.Actors + Dapr.Actors.AspNetCore + MediatR]
- [Source: Directory.Packages.props - Package versions: Dapr.Actors 1.16.1, Shouldly 8.2.0, NSubstitute 5.3.0, xunit 2.9.3]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

None — clean implementation with no errors or retries.

### Completion Notes List

- Task 1: Created IDomainServiceInvoker interface in Server/DomainServices/ with single InvokeAsync method. Deleted Server BuildVerification.cs placeholder.
- Task 2: Implemented InMemoryStateManager with full IActorStateManager interface (17 methods including TTL overloads). Uses pending/committed dual-dictionary pattern to simulate DAPR actor turn-based state semantics. AddStateAsync throws if key exists. SaveStateAsync commits atomically.
- Task 3: Implemented FakeDomainServiceInvoker with canned response configuration by command type, tenant+domain combo, or default. Tracks all invocations for assertions. Throws InvalidOperationException when no response configured.
- Task 4: Created CommandEnvelopeBuilder, AggregateIdentityBuilder, EventEnvelopeBuilder — all fluent builders with sensible defaults that pass Contracts validation.
- Task 5: Created DomainResultAssertions (ShouldBeSuccess/Rejection/NoOp/ContainEvent), EventSequenceAssertions (ShouldHaveSequentialNumbers), EventEnvelopeAssertions (ShouldHaveValidMetadata) — all using xUnit Assert.
- Task 6: Created Hexalith.EventStore.Testing.Tests project with 46 tests covering all fakes, builders, and assertion helpers. All pass.
- Task 7: Solution builds with 0 errors/0 warnings. dotnet pack produces Testing .nupkg. Added xunit dependency to Testing.csproj for assertion helpers.

### Change Log

- 2026-02-12: Story 1.4 implementation complete — Testing package with InMemoryStateManager, FakeDomainServiceInvoker, 3 builders, 3 assertion helper classes, and 46 unit tests.
- 2026-02-13: Code review fixes — [H1] Fixed EventEnvelopeBuilder AggregateId inconsistency (composite ID now auto-computed from TenantId/Domain/AggregateIdPart). [H2] Replaced xunit with xunit.assert in Testing.csproj to prevent test runner crash. [M1] Removed unused RemovalSentinel constant. [M2] Deleted nul file artifact. [M3] Added 2 tests for AggregateId consistency. Total: 48 tests.

### File List

New files:
- src/Hexalith.EventStore.Server/DomainServices/IDomainServiceInvoker.cs
- src/Hexalith.EventStore.Testing/Fakes/InMemoryStateManager.cs
- src/Hexalith.EventStore.Testing/Fakes/FakeDomainServiceInvoker.cs
- src/Hexalith.EventStore.Testing/Builders/CommandEnvelopeBuilder.cs
- src/Hexalith.EventStore.Testing/Builders/AggregateIdentityBuilder.cs
- src/Hexalith.EventStore.Testing/Builders/EventEnvelopeBuilder.cs
- src/Hexalith.EventStore.Testing/Assertions/DomainResultAssertions.cs
- src/Hexalith.EventStore.Testing/Assertions/EventSequenceAssertions.cs
- src/Hexalith.EventStore.Testing/Assertions/EventEnvelopeAssertions.cs
- tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj
- tests/Hexalith.EventStore.Testing.Tests/Fakes/InMemoryStateManagerTests.cs
- tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeDomainServiceInvokerTests.cs
- tests/Hexalith.EventStore.Testing.Tests/Builders/CommandEnvelopeBuilderTests.cs
- tests/Hexalith.EventStore.Testing.Tests/Builders/AggregateIdentityBuilderTests.cs
- tests/Hexalith.EventStore.Testing.Tests/Builders/EventEnvelopeBuilderTests.cs
- tests/Hexalith.EventStore.Testing.Tests/Assertions/DomainResultAssertionsTests.cs
- tests/Hexalith.EventStore.Testing.Tests/Assertions/EventSequenceAssertionsTests.cs
- tests/Hexalith.EventStore.Testing.Tests/Assertions/EventEnvelopeAssertionsTests.cs

Modified files:
- src/Hexalith.EventStore.Testing/Hexalith.EventStore.Testing.csproj (xunit.assert dependency, was xunit)
- src/Hexalith.EventStore.Testing/Builders/EventEnvelopeBuilder.cs (AggregateId auto-computed from parts)
- src/Hexalith.EventStore.Testing/Fakes/InMemoryStateManager.cs (removed unused RemovalSentinel)
- tests/Hexalith.EventStore.Testing.Tests/Builders/EventEnvelopeBuilderTests.cs (added AggregateId consistency tests)
- Directory.Packages.props (added xunit.assert version)
- Hexalith.EventStore.slnx (added Testing.Tests project)

Deleted files:
- src/Hexalith.EventStore.Server/BuildVerification.cs
- src/Hexalith.EventStore.Testing/BuildVerification.cs
