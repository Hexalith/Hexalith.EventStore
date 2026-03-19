# Story 8.2: Counter Sample Domain Service

Status: ready-for-dev

## Story

As a developer evaluating EventStore,
I want a working Counter domain service as a reference implementation,
so that I can understand the pure function programming model by example.

## Acceptance Criteria

1. **Given** the sample domain service,
   **When** inspected,
   **Then** it implements `IncrementCounter` command, `CounterIncremented` event, and `CounterState` (FR41, UX-DR23)
   **And** demonstrates the pure function contract `(Command, CurrentState?) -> DomainResult`
   **And** demonstrates rejection events via `IRejectionEvent`.

2. **Given** the domain service is registered,
   **When** configured via DAPR,
   **Then** it supports at least 2 independent domains within the same EventStore instance (FR24)
   **And** supports at least 2 tenants within the same domain with isolated event streams (FR22, FR25).

## Context: What Already Exists

The Counter sample domain service is **already substantially implemented** from previous epic work. This story validates completeness, adds a second domain for FR24, and adds multi-tenant isolation tests for FR22/FR25.

### Existing Counter Domain (`samples/Hexalith.EventStore.Sample/Counter/`)

- **Commands**: `IncrementCounter`, `DecrementCounter`, `ResetCounter`, `CloseCounter` — all parameterless records
- **Events**: `CounterIncremented`, `CounterDecremented`, `CounterReset`, `CounterClosed` — all `IEventPayload`
- **Rejection event**: `CounterCannotGoNegative` — implements `IRejectionEvent` (D3 pattern)
- **State**: `CounterState` — implements `ITerminatable`, has `Apply()` for each event type, no-op `Apply(AggregateTerminated)`
- **Aggregate**: `CounterAggregate : EventStoreAggregate<CounterState>` — fluent API with static `Handle` methods demonstrating all three result states (Success, Rejection, NoOp)
- **Legacy processor**: `CounterProcessor : IDomainProcessor` — backward-compatible path with switch-based dispatch
- **Program.cs**: `AddEventStore()` + `UseEventStore()` + `/process` endpoint receiving `DomainServiceRequest` and returning `DomainServiceWireResult`
- **AppHost wiring**: DAPR sidecar with `app-id: sample`, zero infrastructure access (D4)

### Existing Tests (`tests/Hexalith.EventStore.Sample.Tests/`)

- `CounterAggregateTests` — all Handle scenarios including tombstoning, null state, replay
- `CounterProcessorTests` — legacy path parity
- `FluentApiRegistrationIntegrationTests` — five-layer cascade, keyed/non-keyed resolution, discovery, activation, backward compatibility

### What This Story Must Complete

1. **Add a second domain** (e.g., `Greeting`) to demonstrate FR24 (multi-domain support)
2. **Add multi-tenant isolation tests** to demonstrate FR22/FR25 (tenant-scoped routing)
3. **Validate all existing Counter tests pass** — zero regressions
4. **Verify the sample is a complete reference implementation** covering all patterns

## Tasks / Subtasks

- [ ] Task 1: Add a minimal second domain to demonstrate FR24 multi-domain (AC: #2)
  - [ ] 1.1 Create `samples/Hexalith.EventStore.Sample/Greeting/Commands/SendGreeting.cs` — parameterless record implementing the command pattern
  - [ ] 1.2 Create `samples/Hexalith.EventStore.Sample/Greeting/Events/GreetingSent.cs` — `IEventPayload` event
  - [ ] 1.3 Create `samples/Hexalith.EventStore.Sample/Greeting/State/GreetingState.cs` — minimal state with `MessageCount` property and `Apply(GreetingSent)` method
  - [ ] 1.4 Create `samples/Hexalith.EventStore.Sample/Greeting/GreetingAggregate.cs` — `EventStoreAggregate<GreetingState>` with single `Handle(SendGreeting, GreetingState?)` returning `DomainResult.Success`
  - [ ] 1.5 Verify `AddEventStore()` auto-discovers both `CounterAggregate` and `GreetingAggregate` from the same assembly scan — no registration changes needed in `Program.cs`. Verification: run the sample and confirm `UseEventStore()` activation logs show both `counter` and `greeting` domains, or confirm via Task 2 test assertions.

- [ ] Task 2: Add multi-domain discovery and routing tests (AC: #2)
  - [ ] 2.1 **REGRESSION FIX (CRITICAL):** Update `FluentApiRegistrationIntegrationTests` assertions that will break when GreetingAggregate is added to the assembly:
    - `AddEventStore_SampleAssembly_DiscoveryResultContainsExactlyOneCounterAggregate` (line 33): change `Assert.Equal(1, discovery.TotalCount)` to `2`, replace `Assert.Single(discovery.Aggregates)` with `Assert.Equal(2, discovery.Aggregates.Count)`, and verify both `counter` and `greeting` are present
    - `UseEventStore_SampleAssembly_ActivationContextHasCorrectCounterProperties` (line 50): change `Assert.Single(context.Activations)` to `Assert.Equal(2, context.Activations.Count)`, verify both domains are activated with correct resource names
  - [ ] 2.2 Add test in `tests/Hexalith.EventStore.Sample.Tests/Registration/MultiDomainRegistrationTests.cs` verifying `AssemblyScanner.ScanForDomainTypes` discovers exactly 2 aggregates: `counter` and `greeting`
  - [ ] 2.3 Add test verifying both domains are resolved via keyed DI: `GetRequiredKeyedService<IDomainProcessor>("counter")` and `GetRequiredKeyedService<IDomainProcessor>("greeting")`
  - [ ] 2.4 Add test verifying `UseEventStore()` activates both domains with correct convention-derived DAPR resource names (`counter-eventstore`, `greeting-eventstore`)
  - [ ] 2.5 Create `tests/Hexalith.EventStore.Sample.Tests/Greeting/GreetingAggregateTests.cs` containing:
    - Test dispatching `SendGreeting` command through `GreetingAggregate.ProcessAsync` and verifying `GreetingSent` event is returned
    - Test with null state returning `DomainResult.Success` — proves happy-path works under same conditions that trigger Counter's edge cases
    - Test verifying `GreetingState.MessageCount` increments after applying `GreetingSent` event

- [ ] Task 3: Add multi-tenant isolation tests (AC: #2)
  - [ ] 3.1 Add test verifying `CommandEnvelope` with `TenantId="tenant-a"` and `TenantId="tenant-b"` produce distinct `AggregateIdentity` values for the same domain and aggregate ID
  - [ ] 3.2 Add test verifying `NamingConventionEngine.GetPubSubTopic` produces tenant-scoped topics: `tenant-a.counter.events` vs `tenant-b.counter.events`
  - [ ] 3.3 Add test verifying state rehydration is tenant-independent — two CounterAggregates processing identical commands for different tenants produce identical DomainResults (pure function contract: same inputs = same outputs, tenant does not affect domain logic)

- [ ] Task 4: Validate existing tests — zero regressions (AC: #1)
  - [ ] 4.1 Run Tier 1 tests: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ && dotnet test tests/Hexalith.EventStore.Client.Tests/ && dotnet test tests/Hexalith.EventStore.Sample.Tests/ && dotnet test tests/Hexalith.EventStore.Testing.Tests/`
  - [ ] 4.2 Run Tier 2 tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/`
  - [ ] 4.3 If any test failures are caused by this story's changes, fix them. Pre-existing failures unrelated to this story should be documented but NOT fixed.

- [ ] Task 5: Verify sample completeness as reference implementation (AC: #1)
  - [ ] 5.1 Confirm existing `CounterAggregateTests` already cover all four DomainResult patterns: Success (IncrementCounter), Rejection (DecrementCounter at zero), NoOp (ResetCounter at zero), Tombstoning (CloseCounter + subsequent commands) — these tests ARE the proof, no new verification code needed
  - [ ] 5.2 Confirm existing tests cover `IRejectionEvent` via `CounterCannotGoNegative` and `ITerminatable` via `CounterState.IsTerminated` + `AggregateTerminated` — already proven by existing test suite
  - [ ] 5.3 Confirm Greeting domain tests (created in Task 2.5) cover null-state happy path — no additional test files needed
  - [ ] 5.4 Record updated Tier 1 test count baseline in Dev Agent Record (previous: 659, expected increase from new tests)

## Dev Notes

### REGRESSION TRAP: FluentApiRegistrationIntegrationTests Will Break

Adding `GreetingAggregate` to the sample assembly means `AssemblyScanner` will discover 2 aggregates instead of 1. The following existing tests have **hard-coded single-aggregate assertions** that WILL fail:

- `AddEventStore_SampleAssembly_DiscoveryResultContainsExactlyOneCounterAggregate` (line 38: `Assert.Equal(1, discovery.TotalCount)`, line 39: `Assert.Single(discovery.Aggregates)`)
- `UseEventStore_SampleAssembly_ActivationContextHasCorrectCounterProperties` (line 56: `Assert.Single(context.Activations)`)

**You MUST update these assertions in Task 2.1 BEFORE running tests.** This is the #1 regression risk in this story.

### Critical: This Is a Validation & Completion Story, Not a Rewrite

The Counter sample domain is already implemented and tested. Do NOT:
- Rewrite existing Counter commands, events, state, or aggregate — they are correct
- Change `CounterAggregate` Handle method signatures — they follow the fluent API convention
- Change `CounterProcessor` — it provides backward compatibility
- Modify `Program.cs` beyond what's needed for verification — `AddEventStore()` auto-discovers all domains
- Change the AppHost wiring — the DAPR sidecar configuration is correct (D4)
- Add infrastructure dependencies to the sample — domain services must have zero infrastructure access

### Greeting Domain: Minimal by Design

The second domain exists solely to demonstrate FR24 (multi-domain). Keep it intentionally minimal:
- One command, one event, one state, one aggregate — no rejection events, no tombstoning
- This contrasts with Counter which shows the full pattern repertoire
- A developer should see: "Counter = comprehensive example, Greeting = minimal quickstart"
- Add an XML doc comment on `GreetingAggregate`: `/// <summary>Minimal domain demonstrating multi-domain registration. See CounterAggregate for full pattern repertoire (rejection, no-op, tombstoning).</summary>`

### Greeting Domain Implementation Pattern

Follow the exact same patterns as Counter. The `IEventPayload` interface is resolved via implicit usings configured in the project (verify `GlobalUsings.cs` or `<Using>` items in `.csproj`). If `IEventPayload` is NOT in implicit usings, add `using Hexalith.EventStore.Contracts.Events;` explicitly in each Greeting file.

```csharp
// samples/Hexalith.EventStore.Sample/Greeting/Commands/SendGreeting.cs
namespace Hexalith.EventStore.Sample.Greeting.Commands;

public sealed record SendGreeting();

// samples/Hexalith.EventStore.Sample/Greeting/Events/GreetingSent.cs
namespace Hexalith.EventStore.Sample.Greeting.Events;

public sealed record GreetingSent() : IEventPayload;

// samples/Hexalith.EventStore.Sample/Greeting/State/GreetingState.cs
namespace Hexalith.EventStore.Sample.Greeting.State;

public sealed class GreetingState
{
    public int MessageCount { get; private set; }

    public void Apply(GreetingSent e) => MessageCount++;
}

// samples/Hexalith.EventStore.Sample/Greeting/GreetingAggregate.cs
namespace Hexalith.EventStore.Sample.Greeting;

/// <summary>Minimal domain demonstrating multi-domain registration. See CounterAggregate for full pattern repertoire (rejection, no-op, tombstoning).</summary>
public sealed class GreetingAggregate : EventStoreAggregate<GreetingState>
{
    public static DomainResult Handle(SendGreeting command, GreetingState? state)
        => DomainResult.Success(new IEventPayload[] { new GreetingSent() });
}
```

**Convention-derived names** (via `NamingConventionEngine`):
- Domain name: `greeting` (from `GreetingAggregate` → strip `Aggregate` suffix → kebab-case)
- State store: `greeting-eventstore`
- Topic: `{tenantId}.greeting.events`

### Multi-Tenant Testing: What to Test at This Layer

Multi-tenant isolation is enforced at the **server layer** (actor state keys, DAPR component scoping), NOT at the domain service layer. Domain services are pure functions — they receive `(Command, State?)` and return events. Tenant routing is transparent to domain logic.

At this story's scope, verify:
1. `AggregateIdentity` correctly differentiates tenants (contract-level)
2. `NamingConventionEngine.GetPubSubTopic` produces tenant-scoped topic names (convention-level)
3. Pure function contract: domain logic is tenant-agnostic (same command + same state = same result regardless of tenant)

Do NOT attempt to test actual DAPR state store isolation — that's server-level (Epic 2) and E2E (Story 8.5, Tier 3).

### Multi-Domain Test Pattern

```csharp
// Verify auto-discovery finds both domains
[Fact]
public void AddEventStore_SampleAssembly_DiscoversBothCounterAndGreetingDomains()
{
    ServiceCollection services = new();
    services.AddEventStore(typeof(CounterAggregate).Assembly);
    ServiceProvider provider = services.BuildServiceProvider();

    DiscoveryResult discovery = provider.GetRequiredService<DiscoveryResult>();
    discovery.Aggregates.Count.ShouldBe(2);
    discovery.Aggregates.ShouldContain(a => a.DomainName == "counter");
    discovery.Aggregates.ShouldContain(a => a.DomainName == "greeting");
}

// Verify keyed resolution for both domains
[Fact]
public void AddEventStore_SampleAssembly_ResolvesBothDomainsViaKeyedDI()
{
    ServiceCollection services = new();
    services.AddEventStore(typeof(CounterAggregate).Assembly);
    // ... UseEventStore activation ...

    IDomainProcessor counter = provider.GetRequiredKeyedService<IDomainProcessor>("counter");
    IDomainProcessor greeting = provider.GetRequiredKeyedService<IDomainProcessor>("greeting");

    counter.ShouldBeOfType<CounterAggregate>();
    greeting.ShouldBeOfType<GreetingAggregate>();
}
```

### Key Package Versions (from Directory.Packages.props)

| Package | Version |
|---|---|
| xUnit | 2.9.3 |
| Shouldly | 4.3.0 |
| NSubstitute | 5.3.0 |
| coverlet.collector | 6.0.4 |
| .NET SDK | 10.0.103 |

### WARNING: 75 Pre-Existing Tier 3 Test Failures

There are 75 pre-existing Tier 3 test failures on `main`. These existed BEFORE this story and are NOT regressions. Do NOT attempt to fix them. Only fix failures directly caused by changes in this story.

### Coding Conventions (from .editorconfig)

- File-scoped namespaces: `namespace X.Y.Z;`
- Allman braces (new line before `{`)
- Private fields: `_camelCase`
- Async suffix on async methods
- 4-space indentation, CRLF, UTF-8
- Nullable enabled, implicit usings enabled
- Warnings as errors (`TreatWarningsAsErrors = true`)

### Project Structure Notes

- Sample domain service lives in `samples/` NOT `src/` — it is NOT a published NuGet package
- Tests live in `tests/Hexalith.EventStore.Sample.Tests/`
- Greeting domain follows same folder structure as Counter: `Greeting/Commands/`, `Greeting/Events/`, `Greeting/State/`, `Greeting/GreetingAggregate.cs`
- All new files must use the same namespace convention: `Hexalith.EventStore.Sample.Greeting.*`
- No changes to `.csproj` files expected — SDK-style project uses default globbing, new `.cs` files are auto-included
- **Legacy path note:** `AddEventStoreClient<CounterProcessor>()` (backward-compat registration) bypasses discovery entirely and will NOT see `GreetingAggregate`. Do not mix legacy and fluent registration paths in the same service. The existing AC8 backward-compat test is isolated and will continue to work unchanged.

### Existing Files to MODIFY (assertion updates only)

| File | Change |
|---|---|
| `tests/Hexalith.EventStore.Sample.Tests/Registration/FluentApiRegistrationIntegrationTests.cs` | Update aggregate count assertions from 1 to 2 in AC1 and AC2 tests |

### Existing Files — DO NOT MODIFY

| File | Reason |
|---|---|
| `samples/Hexalith.EventStore.Sample/Counter/**/*` | Counter domain is complete and correct |
| `samples/Hexalith.EventStore.Sample/Program.cs` | `AddEventStore()` auto-discovers — no changes needed |
| `src/Hexalith.EventStore.AppHost/Program.cs` | AppHost wiring is correct |
| `src/Hexalith.EventStore.Client/**/*` | Client framework is complete |
| `src/Hexalith.EventStore.Contracts/**/*` | Contracts are stable |

### New Files to Create

| File | Purpose |
|---|---|
| `samples/Hexalith.EventStore.Sample/Greeting/Commands/SendGreeting.cs` | Minimal command |
| `samples/Hexalith.EventStore.Sample/Greeting/Events/GreetingSent.cs` | Minimal event |
| `samples/Hexalith.EventStore.Sample/Greeting/State/GreetingState.cs` | Minimal state |
| `samples/Hexalith.EventStore.Sample/Greeting/GreetingAggregate.cs` | Minimal fluent aggregate |
| `tests/Hexalith.EventStore.Sample.Tests/Greeting/GreetingAggregateTests.cs` | Greeting domain tests |
| `tests/Hexalith.EventStore.Sample.Tests/Registration/MultiDomainRegistrationTests.cs` | FR24 multi-domain tests |
| `tests/Hexalith.EventStore.Sample.Tests/MultiTenant/MultiTenantIsolationTests.cs` | FR22/FR25 tests |

### Previous Story Intelligence (Story 8.1)

- Story 8.1 added `PrerequisiteValidator` to AppHost, all Tier 1 (659 pass) and Tier 2 (1504/1505) pass
- Pre-existing Tier 2 failure: `ErrorReferenceEndpointTests.AllProblemTypeUris_HaveCorrespondingErrorModel` — NOT a regression
- Pre-existing Tier 3 failures (75/192) — NOT regressions
- Key learning: do not modify existing infrastructure code that is already battle-tested
- Pattern: validate and complete, don't rewrite

### Git Intelligence

Recent commits (2026-03-18):
- `96e725f` feat: Complete Story 8.1 Aspire AppHost & DAPR topology with prerequisite validation
- `93d0230` Implement per-consumer rate limiting (Story 7.3)
- `3edd174` feat: Implement per-consumer rate limiting alongside existing per-tenant limits
- `e2eeec8` feat: Update sprint status and add Story 7.2 for Per-Tenant Rate Limiting
- `ff7a64c` Merge Story 7.1: Configurable Aggregate Snapshots

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Epic 8, Story 8.2]
- [Source: _bmad-output/planning-artifacts/prd.md — FR22, FR24, FR25, FR41, UX-DR23]
- [Source: _bmad-output/planning-artifacts/architecture.md — D3 error contract, D4 access control, D7 service invocation]
- [Source: _bmad-output/implementation-artifacts/8-1-aspire-apphost-and-dapr-topology.md — Previous story intelligence]
- [Source: samples/Hexalith.EventStore.Sample/ — Existing Counter domain implementation]
- [Source: tests/Hexalith.EventStore.Sample.Tests/ — Existing test suite]
- [Source: src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs — AddEventStore registration]
- [Source: src/Hexalith.EventStore.Client/Discovery/AssemblyScanner.cs — Convention-based discovery]
- [Source: src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs — Domain naming conventions]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
