# Story 16.10: Integration Test for Fluent API Registration Path

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a library maintainer,
I want an integration test that validates the full fluent API registration path — from `AddEventStore()` auto-discovery through `UseEventStore()` cascade activation to `IDomainProcessor.ProcessAsync()` command dispatch — using the real Counter sample assembly,
so that any regression in the discovery-registration-activation-dispatch pipeline is caught before it reaches consumers.

## Acceptance Criteria

1. **AC1 — Discovery result validation:** A test verifies that `DiscoveryResult` singleton is resolvable from the built `IServiceProvider`, contains exactly 1 aggregate (`CounterAggregate`) with domain name `"counter"` and state type `CounterState`, and 0 projections. The `TotalCount` is 1.

2. **AC2 — Activation context validation:** A test verifies that after `UseEventStore()`, the `EventStoreActivationContext` has `IsActivated == true` and `Activations` contains exactly 1 entry with:
   - `DomainName == "counter"`
   - `Kind == DomainKind.Aggregate`
   - `Type == typeof(CounterAggregate)`
   - `StateType == typeof(CounterState)`
   - `StateStoreName == "counter-eventstore"`
   - `TopicPattern == "counter.events"`
   - `DeadLetterTopicPattern == "deadletter.counter.events"`

3. **AC3 — Keyed service resolution:** A test creates a service scope via `host.Services.CreateScope()` and verifies that `scope.ServiceProvider.GetRequiredKeyedService<IDomainProcessor>("counter")` returns a `CounterAggregate` instance.

4. **AC4 — Non-keyed enumeration:** A test creates a service scope and verifies that `scope.ServiceProvider.GetServices<IDomainProcessor>()` returns at least one instance of type `CounterAggregate`.

5. **AC5 — Cascade Layer 5 integration:** A test verifies that when `AddEventStore()` is called with Layer 5 `ConfigureDomain("counter", o => o.StateStoreName = "custom-store")`, UseEventStore activation resolves `StateStoreName == "custom-store"` while `TopicPattern` and `DeadLetterTopicPattern` retain Layer 1 convention defaults.

6. **AC6 — Cascade Layer 4 appsettings integration:** A test verifies that appsettings configuration `EventStore:Domains:counter:TopicPattern = "override.events"` is picked up by UseEventStore and reflected in the activation context. The test also verifies that `StateStoreName` and `DeadLetterTopicPattern` retain their Layer 1 convention defaults (`"counter-eventstore"` and `"deadletter.counter.events"` respectively), confirming selective override behavior.

7. **AC7 — Command dispatch for all Counter commands:** Tests resolve `IDomainProcessor` via keyed DI (`"counter"`) from a scoped service provider and verify that the resolved aggregate handles all three Counter commands through ProcessAsync:
   - `IncrementCounter` → `DomainResult.IsSuccess` with `CounterIncremented`
   - `DecrementCounter` with zero state → `DomainResult.IsRejection` with `CounterCannotGoNegative`
   - `ResetCounter` with zero state → `DomainResult.IsNoOp`

8. **AC8 — Backward compatibility with CounterProcessor:** A **separate** test (independent host, no `AddEventStore()` call — do NOT use the shared `BuildTestHost()` helper) verifies that `AddEventStoreClient<CounterProcessor>()` registers a functional `IDomainProcessor` that handles `IncrementCounter` and returns `DomainResult.IsSuccess` with a `CounterIncremented` event. The test also verifies that `host.Services.GetService<DiscoveryResult>()` returns `null`, confirming the fluent API discovery path was not activated. This confirms the legacy registration path is unbroken and isolated.

9. **AC9 — All tests run green:** `dotnet test tests/Hexalith.EventStore.Sample.Tests/` passes with zero failures. No new NuGet dependencies required beyond what the project already has.

10. **AC10 — UseEventStore without AddEventStore throws:** A test builds an `IHost` **without** calling `AddEventStore()`, then verifies that `host.UseEventStore()` throws `InvalidOperationException` with a message containing `"AddEventStore()"`.

11. **AC11 — Cascade Layer 2 global suffix override:** A test calls `AddEventStore(options => options.DefaultStateStoreSuffix = "store", typeof(CounterAggregate).Assembly)` and verifies activation resolves `StateStoreName == "counter-store"` (Layer 2 override of Layer 1 convention `"counter-eventstore"`), while `TopicPattern` retains the Layer 1 convention default `"counter.events"`.

12. **AC12 — Cascade Layer 3 OnConfiguring:** OUT OF SCOPE. `CounterAggregate` does not override `OnConfiguring()`, so Layer 3 cannot be tested with the real sample assembly. Layer 3 integration is covered by unit tests in `CascadeConfigurationTests` (story 16-8). If a future story adds an aggregate with `OnConfiguring` override to the sample, this AC should be revisited.

13. **AC13 — Scoped service lifetime verification:** A test creates two service scopes from the same host, resolves `IDomainProcessor` (keyed by `"counter"`) from each scope, and verifies the two instances are NOT the same reference (`Assert.NotSame`), confirming scoped (not singleton) lifetime registration.

## Tasks / Subtasks

- [x] Task 1: Create FluentApiRegistrationIntegrationTests class (AC: #9)
  - [x] 1.1: Create `tests/Hexalith.EventStore.Sample.Tests/Registration/FluentApiRegistrationIntegrationTests.cs`
  - [x] 1.2: Implement IDisposable with `AssemblyScanner.ClearCache()` + `NamingConventionEngine.ClearCache()` cleanup — ADAPTED: ClearCache() is `internal` to Client assembly and not accessible from Sample.Tests without modifying `src/` (out of scope per AC9). Cache isolation is unnecessary here because all tests scan the same assembly (CounterAggregate.Assembly), and the static caches are keyed by assembly, always returning the correct cached result. Each test builds its own isolated IHost instead.
  - [x] 1.3: Create shared `BuildTestHost()`, `BuildTestHostWithAppSettings()`, and `CreateCommand<T>()` helpers

- [x] Task 2: Implement discovery result tests (AC: #1)
  - [x] 2.1: Test DiscoveryResult singleton resolution
  - [x] 2.2: Assert aggregate count, domain name, state type, projection count

- [x] Task 3: Implement activation context tests (AC: #2)
  - [x] 3.1: Test IsActivated == true after UseEventStore()
  - [x] 3.2: Assert all 7 activation record properties

- [x] Task 4: Implement DI resolution and guardrail tests (AC: #3, #4, #10, #13)
  - [x] 4.1: Test keyed IDomainProcessor resolution with CreateScope() by domain name "counter"
  - [x] 4.2: Test non-keyed enumeration with CreateScope() includes CounterAggregate
  - [x] 4.3: Test UseEventStore() without AddEventStore() throws InvalidOperationException
  - [x] 4.4: Test scoped lifetime: two scopes produce different IDomainProcessor instances (Assert.NotSame)

- [x] Task 5: Implement cascade configuration tests (AC: #5, #6, #11)
  - [x] 5.1: Test Layer 5 ConfigureDomain override with partial property
  - [x] 5.2: Test Layer 4 appsettings override via AddInMemoryCollection (verify non-overridden properties retain defaults)
  - [x] 5.3: Test Layer 2 global DefaultStateStoreSuffix override (verify TopicPattern retains convention default)

- [x] Task 6: Implement command dispatch tests (AC: #7)
  - [x] 6.1: Test IncrementCounter → Success with CounterIncremented
  - [x] 6.2: Test DecrementCounter with zero state → Rejection
  - [x] 6.3: Test ResetCounter with zero state → NoOp

- [x] Task 7: Implement backward compatibility test (AC: #8)
  - [x] 7.1: Test AddEventStoreClient<CounterProcessor> legacy path (separate host, no AddEventStore, no BuildTestHost helper)
  - [x] 7.2: Verify DiscoveryResult is null (fluent API discovery not activated)

- [x] Task 8: Final validation (AC: #9)
  - [x] 8.1: Run full `dotnet test tests/Hexalith.EventStore.Sample.Tests/` — 29 passed, 0 failed
  - [x] 8.2: Verify zero changes under `src/` — confirmed, no story-introduced changes in src/

### Review Follow-ups (AI)

- [x] AI-Review (High): Implement required story test class at `tests/Hexalith.EventStore.Sample.Tests/Registration/FluentApiRegistrationIntegrationTests.cs`; no file exists yet, so AC coverage cannot be validated against the specified integration-path contract. [missing file] — RESOLVED: File created with 13 test methods covering all ACs.
- [x] AI-Review (High): Add explicit AC1 assertions in the integration path for `DiscoveryResult` exact shape (`TotalCount == 1`, one `CounterAggregate`, zero projections); current sample tests do not assert this full contract. — RESOLVED: `AddEventStore_SampleAssembly_DiscoveryResultContainsExactlyOneCounterAggregate` test asserts TotalCount, Aggregates count, Projections count, domain name, type, state type, and kind.
- [x] AI-Review (High): Add AC5/AC6/AC11 cascade integration assertions for real sample assembly activation (Layer 5 override, Layer 4 appsettings override, Layer 2 global suffix override) in the story-specific integration test flow. — RESOLVED: Three dedicated tests: `UseEventStore_Layer5ConfigureDomain_OverridesStateStoreNameOnly`, `UseEventStore_Layer4AppSettings_OverridesTopicPatternOnly`, `UseEventStore_Layer2GlobalSuffix_OverridesStateStoreNameRetainsTopicPattern`.
- [x] AI-Review (High): Add AC8 backward-compatibility test with an independent host that uses `AddEventStoreClient<CounterProcessor>()` only and asserts `DiscoveryResult` is `null` (no fluent discovery activation). — RESOLVED: `AddEventStoreClient_LegacyPath_RegistersFunctionalProcessorWithoutDiscovery` test with independent host.
- [x] AI-Review (High): Add AC13 scoped lifetime verification by resolving keyed `IDomainProcessor` from two different scopes and asserting `Assert.NotSame`. — RESOLVED: `UseEventStore_TwoScopes_ProduceDifferentDomainProcessorInstances` test.
- [x] AI-Review (Medium): Align scoped service resolution with story guidance: resolve `IDomainProcessor` from a created scope, not the root `ServiceProvider`, in sample tests. — RESOLVED: All new integration tests use `host.Services.CreateScope()` for scoped resolution. Existing CounterAggregateTests.cs is out of scope for this story (it tests the aggregate directly, not the registration path).
- [x] AI-Review (Medium): Implement cache-isolation pattern requested by story (`IDisposable` with `AssemblyScanner.ClearCache()` and `NamingConventionEngine.ClearCache()` in ctor/dispose). — RESOLVED (adapted): ClearCache() is `internal` to Client assembly. Modifying `src/` to add InternalsVisibleTo is out of scope per AC9. Cache isolation is unnecessary here because all tests scan the same assembly, and caches are keyed by assembly returning correct results. Each test builds its own isolated IHost.
- [x] AI-Review (Medium): Update Dev Agent Record with actual file list and completion notes; currently empty despite implementation activity. — RESOLVED: File list and completion notes updated.

## Dev Notes

### Architecture Constraints

- **Target framework:** net10.0 with `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`
- **Test project:** `tests/Hexalith.EventStore.Sample.Tests/` — already references `Hexalith.EventStore.Sample` (which transitively references `Hexalith.EventStore.Client`)
- **No new test projects.** No new NuGet dependencies. All tests go in the existing Sample.Tests project.
- **One public type per file**, file name = type name
- **CA1822 compliance:** static methods where possible

### Test Framework and Conventions

| Convention | Standard |
|-----------|----------|
| Framework | xunit (NOT NUnit, NOT MSTest) |
| Assertions | `Assert.*` (NOT Shouldly, NOT FluentAssertions) |
| Naming | `{Method}_{Scenario}_{ExpectedResult}` |
| Test class | `public sealed class FluentApiRegistrationIntegrationTests : IDisposable` |
| Setup | Constructor clears caches; Dispose clears caches |
| Async | `[Fact] public async Task ...` with `await` (not `.Result`) |
| Test isolation | `AssemblyScanner.ClearCache()` + `NamingConventionEngine.ClearCache()` in both ctor and Dispose |

### Host Building Pattern

Use `Host.CreateDefaultBuilder()` — same pattern as existing `UseEventStoreTests` in Client.Tests:

```csharp
private static IHost BuildTestHost(Action<EventStoreOptions>? configureOptions = null) {
    IHostBuilder builder = Host.CreateDefaultBuilder();
    builder.ConfigureServices(services => {
        if (configureOptions is not null) {
            services.AddEventStore(configureOptions, typeof(CounterAggregate).Assembly);
        } else {
            services.AddEventStore(typeof(CounterAggregate).Assembly);
        }
    });
    IHost host = builder.Build();
    host.UseEventStore();
    return host;
}
```

**CRITICAL:** Use the `AddEventStore(params Assembly[])` overload with `typeof(CounterAggregate).Assembly` — NOT the parameterless `AddEventStore()` which uses `Assembly.GetCallingAssembly()` (which would be the test assembly, not the sample assembly).

**NOTE:** `AddServiceDefaults()` (Aspire service defaults) is NOT required for these tests. `AddEventStore()` has no dependency on Aspire service defaults. Use `Host.CreateDefaultBuilder()` directly.

**HOST DISPOSAL:** Use `using var host = BuildTestHost(...)` in each test method to ensure host disposal even on assertion failure. Do not store hosts as class fields — each test gets its own isolated host.

**SERVICE RESOLUTION:** ALWAYS resolve services from `host.Services` (or a scope created from it). NEVER build a separate `ServiceProvider` from the `IServiceCollection` — this bypasses `UseEventStore()` activation and will produce false positives.

**DO NOT START THE HOST:** Do NOT call `host.StartAsync()` or `host.RunAsync()`. The `BuildTestHost()` pattern correctly calls `host.Build()` + `host.UseEventStore()` without starting. Starting the host would trigger `IHostedService` instances and attempt infrastructure connections.

### For Layer 2 global suffix test (AC11):

```csharp
using var host = BuildTestHost(options => options.DefaultStateStoreSuffix = "store");
var context = host.Services.GetRequiredService<EventStoreActivationContext>();
var activation = Assert.Single(context.Activations);
Assert.Equal("counter-store", activation.StateStoreName);       // Layer 2 override
Assert.Equal("counter.events", activation.TopicPattern);        // Layer 1 retained
```

### For appsettings Layer 4 test (AC6):

```csharp
builder.ConfigureAppConfiguration(config => {
    config.AddInMemoryCollection(new Dictionary<string, string?> {
        ["EventStore:Domains:counter:TopicPattern"] = "override.events",
    });
});
```

This pattern matches existing `CascadeConfigurationTests` in Client.Tests.

### CommandEnvelope Construction

Reuse the same helper pattern from existing `CounterAggregateTests`:

```csharp
private static CommandEnvelope CreateCommand<T>(T command) where T : notnull
    => new(
        TenantId: "test-tenant",
        Domain: "counter",
        AggregateId: "counter-1",
        CommandType: typeof(T).Name,
        Payload: JsonSerializer.SerializeToUtf8Bytes(command),
        CorrelationId: "corr-1",
        CausationId: null,
        UserId: "test-user",
        Extensions: null);
```

### Key Types and Namespaces

| Type | Namespace | Purpose |
|------|-----------|---------|
| `CounterAggregate` | `Hexalith.EventStore.Sample.Counter` | Fluent API aggregate under test |
| `CounterProcessor` | `Hexalith.EventStore.Sample.Counter` | Legacy processor for backward compat |
| `CounterState` | `Hexalith.EventStore.Sample.Counter.State` | State class with Apply methods |
| `IncrementCounter` | `Hexalith.EventStore.Sample.Counter.Commands` | Command type |
| `DecrementCounter` | `Hexalith.EventStore.Sample.Counter.Commands` | Command type |
| `ResetCounter` | `Hexalith.EventStore.Sample.Counter.Commands` | Command type |
| `CounterIncremented` | `Hexalith.EventStore.Sample.Counter.Events` | Event type |
| `CounterDecremented` | `Hexalith.EventStore.Sample.Counter.Events` | Event type |
| `CounterReset` | `Hexalith.EventStore.Sample.Counter.Events` | Event type |
| `CounterCannotGoNegative` | `Hexalith.EventStore.Sample.Counter.Events` | Rejection event type |
| `DiscoveryResult` | `Hexalith.EventStore.Client.Discovery` | Scanner output |
| `DiscoveredDomain` | `Hexalith.EventStore.Client.Discovery` | Per-type discovery record |
| `DomainKind` | `Hexalith.EventStore.Client.Discovery` | Aggregate vs Projection enum |
| `EventStoreActivationContext` | `Hexalith.EventStore.Client.Registration` | Runtime activation manifest |
| `EventStoreDomainActivation` | `Hexalith.EventStore.Client.Registration` | Per-domain activation record |
| `EventStoreOptions` | `Hexalith.EventStore.Client.Configuration` | Global options |
| `EventStoreDomainOptions` | `Hexalith.EventStore.Client.Configuration` | Per-domain options |
| `IDomainProcessor` | `Hexalith.EventStore.Client.Handlers` | Core domain processing interface |
| `DomainResult` | `Hexalith.EventStore.Contracts.Results` | Command processing result |
| `CommandEnvelope` | `Hexalith.EventStore.Contracts.Commands` | Command wrapper record |
| `AssemblyScanner` | `Hexalith.EventStore.Client.Discovery` | Assembly scanning (static, has ClearCache) |
| `NamingConventionEngine` | `Hexalith.EventStore.Client.Conventions` | Convention engine (static, has ClearCache) |
| `AddEventStore()` | `Hexalith.EventStore.Client.Registration.EventStoreServiceCollectionExtensions` | Registration extension |
| `UseEventStore()` | `Hexalith.EventStore.Client.Registration.EventStoreHostExtensions` | Activation extension |
| `AddEventStoreClient<T>()` | `Hexalith.EventStore.Client.Registration.EventStoreServiceCollectionExtensions` | Legacy registration |

### Change Detector Assertions

AC1 and AC2 assert exact counts (1 aggregate, 0 projections, 1 activation) for the current sample assembly. If aggregates or projections are added to the sample assembly in a future story, update these assertions accordingly. The exact counts serve as intentional change detectors.

### OnConfiguring Hazard

If `CounterAggregate` gains an `OnConfiguring()` override in a future story, AC2 assertions must be updated to reflect Layer 3 cascade values instead of Layer 1 convention defaults.

### Convention-Derived Names for Counter Domain

| Property | Convention Value |
|----------|-----------------|
| Domain name | `counter` (strips `Aggregate` suffix, kebab-cases `Counter`) |
| StateStoreName | `counter-eventstore` |
| TopicPattern | `counter.events` |
| DeadLetterTopicPattern | `deadletter.counter.events` |

### File Structure

New file goes in:
```
tests/
  Hexalith.EventStore.Sample.Tests/
    Counter/
      CounterAggregateTests.cs          # Existing
      CounterProcessorTests.cs          # Existing
    Registration/
      FluentApiRegistrationIntegrationTests.cs  # NEW
```

### Keyed Service Resolution Pattern

```csharp
using var scope = host.Services.CreateScope();
var processor = scope.ServiceProvider.GetRequiredKeyedService<IDomainProcessor>("counter");
Assert.IsType<CounterAggregate>(processor);
```

**CRITICAL:** Aggregates are registered as **scoped** services (not singleton). You MUST create a scope before resolving `IDomainProcessor`:
- `services.AddScoped(typeof(IDomainProcessor), aggregate.Type)` — non-keyed
- `services.AddKeyedScoped(typeof(IDomainProcessor), aggregate.DomainName, aggregate.Type)` — keyed

### Previous Story Intelligence (Stories 16-8, 16-9)

**Story 16-8** added comprehensive unit tests. Patterns established:
- `IDisposable` pattern on all test classes for cache isolation
- `AssemblyScanner.ClearCache()` + `NamingConventionEngine.ClearCache()` in teardown
- xunit + Assert (NOT Shouldly)
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`
- One public type per file

**Story 16-9** updated README/Quickstart docs. No code patterns relevant to this story.

**Stories 16-5 / 16-6** established:
- UseEventStore builds an `IHost` via `Host.CreateDefaultBuilder()`
- CascadeConfiguration uses `AddInMemoryCollection` for appsettings binding
- `ConfigureDomain("counter", o => ...)` for Layer 5 explicit overrides
- Each host instance needs fresh caches for isolation

### Git Intelligence

Recent commits (relevant to this story):
```
b422bab feat: Enhance UseEventStore validation and add external blockers template
7dde37b feat: Add UseEventStore activation, cascading config, and story 16-7 spec
016c01b fix(story-16-4): close review findings and mark done
633b985 feat: Add AssemblyScanner auto-discovery and harden aggregate/projection error handling
4e47cb6 feat: Add EventStoreAggregate base class, EventStoreDomain attribute, and NamingConventionEngine
```

All production code for stories 16-1 through 16-6 is committed. Stories 16-7 through 16-9 may or may not be complete — this story should work regardless since it tests the SDK layer (not sample docs).

### Scope Boundary

**IN scope for 16-10:**
- Integration tests validating the full fluent API registration pipeline
- Tests using real CounterAggregate from the sample assembly
- Cascade configuration integration tests (Layer 2, Layer 4, and Layer 5; Layer 3 deferred — see AC12)
- Backward compatibility test for AddEventStoreClient<CounterProcessor>
- New test file in `tests/Hexalith.EventStore.Sample.Tests/Registration/`

**NOT in scope for 16-10:**
- Modifying any file under `src/`
- Adding new test projects or NuGet dependencies
- Aspire topology E2E tests (those live in IntegrationTests project)
- DAPR sidecar or actor pipeline tests
- HTTP endpoint tests (the /process endpoint is tested elsewhere)
- Performance testing

### Project Structure Notes

- New test file: `tests/Hexalith.EventStore.Sample.Tests/Registration/FluentApiRegistrationIntegrationTests.cs`
- No changes to existing files
- Sample.Tests project already references Sample project (and transitively Client)
- Test class goes in `Hexalith.EventStore.Sample.Tests.Registration` namespace

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-02-28.md#Section 4.3] — Story 16-10 definition: "Integration Test for Fluent API Registration Path"
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-02-28.md#Implementation Sequence] — Story 16-10 depends on all previous stories
- [Source: tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs] — Existing aggregate test patterns and CommandEnvelope helper
- [Source: tests/Hexalith.EventStore.Sample.Tests/Counter/CounterProcessorTests.cs] — Legacy processor test patterns
- [Source: tests/Hexalith.EventStore.Client.Tests/Registration/UseEventStoreTests.cs] — UseEventStore host building patterns
- [Source: tests/Hexalith.EventStore.Client.Tests/Configuration/CascadeConfigurationTests.cs] — Cascade config test patterns
- [Source: tests/Hexalith.EventStore.Client.Tests/Registration/AddEventStoreTests.cs] — AddEventStore DI test patterns
- [Source: src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs] — AddEventStore implementation
- [Source: src/Hexalith.EventStore.Client/Registration/EventStoreHostExtensions.cs] — UseEventStore implementation
- [Source: src/Hexalith.EventStore.Client/Registration/EventStoreActivationContext.cs] — Activation context
- [Source: src/Hexalith.EventStore.Client/Registration/EventStoreDomainActivation.cs] — Activation record
- [Source: src/Hexalith.EventStore.Client/Discovery/AssemblyScanner.cs] — Scanner with ClearCache()
- [Source: src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs] — Convention engine with ClearCache()
- [Source: samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs] — Fluent API aggregate
- [Source: samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs] — Legacy processor
- [Source: samples/Hexalith.EventStore.Sample/Program.cs] — Sample app using AddEventStore()/UseEventStore()

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- ClearCache() methods on AssemblyScanner and NamingConventionEngine are `internal` (only visible to Client.Tests via InternalsVisibleTo). Since modifying `src/` is out of scope per AC9, the IDisposable cache-clear pattern was adapted: each test builds its own isolated IHost, and caches are keyed by assembly, so scanning the same assembly always returns the correct cached result.
- xUnit analyzer xUnit2013 enforces `Assert.Single`/`Assert.Empty` over `Assert.Equal(1, ...)` for collection size checks. Adapted AC1 assertions accordingly.

### Completion Notes List

- 2026-03-01: Adversarial review executed against Story 16.10 using git state + code inspection + test execution.
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj -v minimal` passed (16/16).
- Story remains in-progress because multiple AC-specific integration validations are still missing from the story-defined implementation path.
- 2026-03-01: All 8 review follow-up items addressed. Created FluentApiRegistrationIntegrationTests.cs with 13 test methods covering AC1-AC8, AC10, AC11, AC13 (AC9 validated by test run, AC12 out of scope per story).
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/` passed 29/29 (13 new + 16 existing).
- `dotnet test tests/Hexalith.EventStore.Client.Tests/` passed 231/231 (no regressions).
- Zero changes under `src/` confirmed.
- 2026-03-01: Follow-up code review rerun completed. Revalidated story AC coverage against `FluentApiRegistrationIntegrationTests` and reran `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj -v minimal` (29/29 passing).
- 2026-03-01: Resolved story metadata inconsistencies from earlier review pass (status and reviewer summary alignment).

### Senior Developer Review (AI)

Reviewer: Jerome (AI-assisted)
Date: 2026-03-01
Outcome: Approved

Summary:

- Git repository detected and reviewed.
- Story AC implementation verified in `tests/Hexalith.EventStore.Sample.Tests/Registration/FluentApiRegistrationIntegrationTests.cs`.
- Sample test suite rerun from current workspace state: 29 passed, 0 failed.
- Review found documentation/status consistency issues only; no remaining AC implementation gaps.

Findings:

- [LOW] Story status remained `review` even after AC coverage and green test validation. Fixed by updating status to `done`.
- [LOW] Prior reviewer summary still reflected an older “Changes Requested” state after follow-ups were completed. Fixed by refreshing this review section to current evidence.
- [LOW] Workspace contains unrelated modified files outside this story scope; retained story `File List` as story-specific and documented this as context rather than attributing unrelated changes.

Decision:

- AC-targeted implementation is complete for Story 16.10, with AC12 explicitly out of scope per story definition.
- Story status set to **done**.

### File List

- tests/Hexalith.EventStore.Sample.Tests/Registration/FluentApiRegistrationIntegrationTests.cs (NEW)

### Change Log

- 2026-03-01: Created FluentApiRegistrationIntegrationTests.cs with 13 integration tests covering full fluent API registration path (AC1-AC8, AC10, AC11, AC13). Addressed all 8 code review follow-up items (5 High, 3 Medium). All 29 Sample.Tests pass, 231 Client.Tests pass with zero regressions. No changes to `src/`.
- 2026-03-01: Performed final adversarial review rerun, refreshed reviewer summary to approved, and marked story status `done` after revalidating AC coverage and sample test pass (29/29).
