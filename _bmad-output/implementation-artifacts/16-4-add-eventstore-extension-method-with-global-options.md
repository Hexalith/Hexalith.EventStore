# Story 16.4: AddEventStore Extension Method with Global Options

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a domain service developer,
I want a one-line `AddEventStore()` extension method that auto-discovers my aggregate and projection types and registers them with optional global options,
so that I can wire up my entire event-sourced domain with zero infrastructure knowledge, achieving the "zero-config" DX promise.

## Acceptance Criteria

1. **AC1 — `AddEventStore()` zero-config overload:** A `public static IServiceCollection AddEventStore(this IServiceCollection services)` extension method exists in `EventStoreServiceCollectionExtensions` that scans the **calling assembly** for aggregate and projection types via `AssemblyScanner.ScanForDomainTypes(Assembly.GetCallingAssembly())` and registers each discovered aggregate as `IDomainProcessor` with scoped lifetime. Returns `IServiceCollection` for method chaining.

2. **AC2 — `AddEventStore(Action<EventStoreOptions>)` overload:** A second overload `AddEventStore(this IServiceCollection services, Action<EventStoreOptions> configureOptions)` accepts a lambda to configure global `EventStoreOptions` before discovery and registration. The options are registered in DI via `services.Configure<EventStoreOptions>(configureOptions)` so downstream code can inject `IOptions<EventStoreOptions>`.

3. **AC3 — `AddEventStore(Assembly[])` overload:** A third overload `AddEventStore(this IServiceCollection services, params Assembly[] assemblies)` scans the specified assemblies instead of the calling assembly. This supports scenarios where domain types live in separate class libraries.

4. **AC4 — `AddEventStore(Action<EventStoreOptions>, Assembly[])` overload:** A fourth overload combining options configuration and explicit assemblies: `AddEventStore(this IServiceCollection services, Action<EventStoreOptions> configureOptions, params Assembly[] assemblies)`.

5. **AC5 — `EventStoreOptions` class:** A public class `EventStoreOptions` exists in `Hexalith.EventStore.Client/Configuration/EventStoreOptions.cs` with properties for Layer 2 global cross-cutting configuration. This is a POCO class (not a record — following the standard .NET Options pattern for `IOptions<T>` binding). Initial properties are minimal — only what's needed now. Story 16-6 will extend this class with cascading configuration properties.

6. **AC6 — Aggregate registration as `IDomainProcessor`:** Each discovered aggregate (from `DiscoveryResult.Aggregates`) is registered using type-based DI registration: `services.AddScoped(typeof(IDomainProcessor), aggregateType)`. The DI container handles constructor injection automatically. This is the idiomatic .NET pattern — no factory delegates or `ActivatorUtilities` needed.

7. **AC7 — Keyed service registration (forward-looking):** Each aggregate is ALSO registered as a keyed service with its domain name as the key: `services.AddKeyedScoped(typeof(IDomainProcessor), domainName, aggregateType)`. This lays groundwork for the actor pipeline to resolve a specific domain's processor by key in future stories. The current actor pipeline (Story 3-5) does NOT yet use keyed resolution — the non-keyed registration (AC6) provides backward compatibility. Keyed registration is cheap to add now and avoids a breaking DI change later.

8. **AC8 — Discovery result stored in DI:** The `DiscoveryResult` from assembly scanning is registered as a singleton: `services.AddSingleton(discoveryResult)`. This allows Story 16-5 (`UseEventStore`) and other downstream consumers to access the list of discovered domains without re-scanning.

9. **AC9 — Null/empty guards:** All overloads validate `services` with `ArgumentNullException.ThrowIfNull(services)`. The options overloads validate `configureOptions` with `ArgumentNullException.ThrowIfNull(configureOptions)`. The assembly overloads validate `assemblies` with `ArgumentNullException.ThrowIfNull(assemblies)` and throw `ArgumentException` if the array is empty.

10. **AC10 — Idempotent registration ("first call wins"):** Calling `AddEventStore()` multiple times does NOT duplicate registrations. Use a marker service (e.g., `services.Any(s => s.ServiceType == typeof(DiscoveryResult))`) to detect prior registration and skip if already registered. Return early with `services` for chaining. **Important:** This means `AddEventStore(asm1)` followed by `AddEventStore(asm2)` silently ignores the second call — the second assembly's types are NOT discovered. To scan multiple assemblies, use the multi-assembly overload: `AddEventStore(asm1, asm2)`.

11. **AC11 — Backward compatibility:** The existing `AddEventStoreClient<TProcessor>()` method remains unchanged and continues to work. Developers can mix manual and auto-discovered registrations. No existing public types are modified.

12. **AC12 — Zero new NuGet dependencies:** Implementation uses only `Microsoft.Extensions.DependencyInjection.Abstractions` (already transitively available via `Dapr.Client`) and existing Client types. No new package references added to `Hexalith.EventStore.Client.csproj`.

13. **AC13 — Assembly.GetCallingAssembly() correctness:** Both the zero-config overload (AC1) AND the options-only overload (AC2) MUST use `[MethodImpl(MethodImplOptions.NoInlining)]` to prevent JIT inlining. Both overloads call `Assembly.GetCallingAssembly()` to auto-discover the user's assembly. **Critical:** The calling assembly MUST be captured in the public overload method itself, BEFORE delegating to the private `AddEventStoreCore()` method. If `GetCallingAssembly()` is called inside the core method, it returns the extension class's own assembly — wrong result.

14. **AC14 — Projections discovered but NOT registered:** `DiscoveryResult` contains both `Aggregates` and `Projections` from the scanner. Only aggregates are registered as `IDomainProcessor` (AC6/AC7). Projections are NOT `IDomainProcessor` implementations and are NOT registered as DI services in this story. They exist in the `DiscoveryResult` singleton for Story 16-5 (`UseEventStore`) to consume for subscription activation.

## Tasks / Subtasks

- [x] Task 1: Create `EventStoreOptions` class (AC: #5)
  - [x] 1.1: Create `src/Hexalith.EventStore.Client/Configuration/` folder
  - [x] 1.2: Implement `EventStoreOptions` public class in `EventStoreOptions.cs` with minimal initial properties
  - [x] 1.3: Add XML documentation

- [x] Task 2: Implement `AddEventStore()` extension methods (AC: #1, #2, #3, #4, #6, #7, #8, #9, #10, #13)
  - [x] 2.1: Add zero-config `AddEventStore()` overload with `[MethodImpl(MethodImplOptions.NoInlining)]` and `Assembly.GetCallingAssembly()`
  - [x] 2.2: Add `AddEventStore(Action<EventStoreOptions>)` overload
  - [x] 2.3: Add `AddEventStore(params Assembly[])` overload
  - [x] 2.4: Add `AddEventStore(Action<EventStoreOptions>, params Assembly[])` overload
  - [x] 2.5: Implement core registration logic (shared private method): scan assemblies, register aggregates as IDomainProcessor (non-keyed + keyed), register DiscoveryResult as singleton
  - [x] 2.6: Implement idempotency check via `DiscoveryResult` marker
  - [x] 2.7: Add null/empty guards on all parameters

- [x] Task 3: Create unit tests (AC: all)
  - [x] 3.1: Create test file `tests/Hexalith.EventStore.Client.Tests/Registration/AddEventStoreTests.cs`
  - [x] 3.2: Test zero-config overload discovers and registers aggregates from test assembly
  - [x] 3.3: Test options overload registers `EventStoreOptions` in DI via `IOptions<EventStoreOptions>`
  - [x] 3.4: Test explicit assembly overload scans specified assemblies
  - [x] 3.5: Test keyed service registration resolves correct aggregate by domain name
  - [x] 3.6: Test `DiscoveryResult` singleton is registered and accessible
  - [x] 3.7: Test idempotent registration (calling twice doesn't duplicate)
  - [x] 3.8: Test null `services` throws `ArgumentNullException`
  - [x] 3.9: Test null `configureOptions` throws `ArgumentNullException`
  - [x] 3.10: Test null/empty `assemblies` throws appropriate exception
  - [x] 3.11: Test backward compatibility: `AddEventStoreClient<T>()` still works alongside `AddEventStore()`
  - [x] 3.12: Test registered aggregates resolve as `IDomainProcessor` from service provider
  - [x] 3.13: Test options lambda is applied (configure a property, resolve IOptions, verify value)

- [x] Task 4: Verify build and backward compatibility (AC: #11, #12)
  - [x] 4.1: Verify `dotnet build` succeeds with no warnings
  - [x] 4.2: Verify ALL existing tests pass with zero modifications
  - [x] 4.3: Verify no new NuGet dependencies added

## Dev Notes

### Architecture Constraints

- **Target framework:** net10.0 with `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`
- **Project:** `Hexalith.EventStore.Client` — depends only on `Hexalith.EventStore.Contracts` and `Dapr.Client`
- **New namespace:** `Hexalith.EventStore.Client.Configuration` (for `EventStoreOptions`)
- **Modified file:** `Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs` (add new methods)
- **One public type per file** — `EventStoreOptions.cs` in `Configuration/`
- **No new NuGet dependencies** — `Microsoft.Extensions.DependencyInjection.Abstractions` is already transitively available via `Dapr.Client`
- **Architecture document prescribes:** `Configuration/EventStoreOptions.cs` and `AddEventStore()` in `ServiceCollectionExtensions.cs` (architecture.md line 743-751)

### Design Decisions

**`EventStoreOptions` is a POCO class, not a record (justified architecture deviation):**
The architecture document (line 565) prescribes `*Options.cs record types` as a general convention. However, .NET's Options pattern (`IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`) requires mutable POCO classes with parameterless constructors for configuration binding. Records have init-only properties and value semantics that conflict with the binding model. This deviation is standard practice across all .NET projects using the Options pattern. Use a simple class with `{ get; set; }` properties.

**Initial `EventStoreOptions` properties (minimal for 16-4):**
```csharp
public class EventStoreOptions {
    // Placeholder for future global cross-cutting configuration.
    // Story 16-6 will add cascading config properties.
    // Examples of future properties (NOT for 16-4):
    //   public int DefaultSnapshotInterval { get; set; } = 100;
    //   public JsonSerializerOptions? JsonSerializerOptions { get; set; }
}
```
Keep the class empty or near-empty for now. The purpose of 16-4 is the registration pipeline, not the options schema. Story 16-6 will fill in properties.

**Core registration method signature (internal shared logic):**
```csharp
private static IServiceCollection AddEventStoreCore(
    IServiceCollection services,
    Action<EventStoreOptions>? configureOptions,
    IEnumerable<Assembly> assemblies)
```
All four public overloads delegate to this single internal method to avoid code duplication.

**`Assembly.GetCallingAssembly()` anti-inlining (BOTH overloads):**
```csharp
// Zero-config overload — captures calling assembly
[MethodImpl(MethodImplOptions.NoInlining)]
public static IServiceCollection AddEventStore(this IServiceCollection services) {
    ArgumentNullException.ThrowIfNull(services);
    return AddEventStoreCore(services, configureOptions: null, [Assembly.GetCallingAssembly()]);
}

// Options-only overload — ALSO captures calling assembly
[MethodImpl(MethodImplOptions.NoInlining)]
public static IServiceCollection AddEventStore(
    this IServiceCollection services,
    Action<EventStoreOptions> configureOptions) {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configureOptions);
    return AddEventStoreCore(services, configureOptions, [Assembly.GetCallingAssembly()]);
}
```
`[MethodImpl(MethodImplOptions.NoInlining)]` prevents JIT from inlining this method into the caller's caller, which would cause `GetCallingAssembly()` to return the wrong assembly. **Critical:** `GetCallingAssembly()` MUST be called in the public method, NEVER inside `AddEventStoreCore()`. The explicit assembly overloads (AC3/AC4) do NOT need `NoInlining` since they don't call `GetCallingAssembly()`.

**Aggregate registration strategy — idiomatic type-based:**
```csharp
foreach (DiscoveredDomain agg in result.Aggregates) {
    Type aggregateType = agg.Type;
    string domainName = agg.DomainName;

    // Non-keyed: backward compat + enumeration of all processors
    services.AddScoped(typeof(IDomainProcessor), aggregateType);

    // Keyed: domain-specific resolution (forward-looking for actor pipeline)
    services.AddKeyedScoped(typeof(IDomainProcessor), domainName, aggregateType);
}
```
Use the type-based overloads of `AddScoped`/`AddKeyedScoped` — the DI container handles constructor injection automatically. This avoids closure allocations, has better AOT support, and matches the pattern used by `AddEventStoreClient<T>()`. Do NOT use `ActivatorUtilities` or factory delegates — they add unnecessary complexity for this use case.

**Idempotency pattern:**
```csharp
if (services.Any(s => s.ServiceType == typeof(DiscoveryResult))) {
    return services; // Already registered
}
```
Check for `DiscoveryResult` singleton as the marker. This is lightweight and doesn't require a dedicated marker type.

**Options registration:**
```csharp
if (configureOptions is not null) {
    services.Configure(configureOptions);
}
```
`services.Configure<T>(Action<T>)` is an extension method from `Microsoft.Extensions.Options` which is part of the `Microsoft.NETCore.App` shared framework on net10.0 — no explicit package reference needed. It internally calls `AddOptions<T>()` so `IOptions<T>`, `IOptionsSnapshot<T>`, and `IOptionsMonitor<T>` all work. If `configureOptions` is null (zero-config path), `EventStoreOptions` is still available via DI with defaults if any consumer requests `IOptions<EventStoreOptions>`. **Build verification:** If `services.Configure<T>()` produces a compile error, add `<PackageReference Include="Microsoft.Extensions.Options" />` to the csproj — but this should NOT be needed on net10.0.

### Key Interfaces & Types to Reference

| Type | Location | Role |
|------|----------|------|
| `AssemblyScanner` | `Client/Discovery/AssemblyScanner.cs` | Assembly scanning — use `ScanForDomainTypes()` |
| `DiscoveryResult` | `Client/Discovery/DiscoveryResult.cs` | Scan result — register as singleton |
| `DiscoveredDomain` | `Client/Discovery/DiscoveredDomain.cs` | Per-type metadata with `Type`, `DomainName`, `Kind` |
| `DomainKind` | `Client/Discovery/DomainKind.cs` | `Aggregate` or `Projection` enum |
| `IDomainProcessor` | `Client/Handlers/IDomainProcessor.cs` | Service interface for aggregate registration |
| `NamingConventionEngine` | `Client/Conventions/NamingConventionEngine.cs` | NOT directly used — `AssemblyScanner` calls it internally |
| `EventStoreDomainAttribute` | `Client/Attributes/EventStoreDomainAttribute.cs` | NOT directly used — resolved by scanner/engine |
| `EventStoreServiceCollectionExtensions` | `Client/Registration/EventStoreServiceCollectionExtensions.cs` | ADD new methods here |

### Expected File Structure After Implementation

```
src/Hexalith.EventStore.Client/
├── Aggregates/
│   ├── EventStoreAggregate.cs              # UNCHANGED (Story 16-1)
│   └── EventStoreProjection.cs             # UNCHANGED (Story 16-1)
├── Attributes/
│   └── EventStoreDomainAttribute.cs        # UNCHANGED (Story 16-2)
├── Configuration/                           # NEW folder
│   └── EventStoreOptions.cs               # NEW — global options (Layer 2)
├── Conventions/
│   └── NamingConventionEngine.cs           # UNCHANGED (Story 16-2)
├── Discovery/                               # UNCHANGED (Story 16-3)
│   ├── AssemblyScanner.cs
│   ├── DiscoveredDomain.cs
│   ├── DiscoveryResult.cs
│   └── DomainKind.cs
├── Handlers/
│   ├── IDomainProcessor.cs                 # UNCHANGED
│   └── DomainProcessorBase.cs              # UNCHANGED
└── Registration/
    └── EventStoreServiceCollectionExtensions.cs  # MODIFIED — add AddEventStore() overloads
```

### API Shape (Expected Usage After This Story)

```csharp
// ZERO-CONFIG — auto-discovers aggregates in calling assembly
builder.Services.AddEventStore();

// WITH GLOBAL OPTIONS — configure cross-cutting options
builder.Services.AddEventStore(options => {
    // Story 16-6 will add options properties
});

// EXPLICIT ASSEMBLIES — scan specific assemblies
builder.Services.AddEventStore(
    typeof(OrderAggregate).Assembly,
    typeof(ShippingAggregate).Assembly);

// OPTIONS + ASSEMBLIES — full control
builder.Services.AddEventStore(
    options => { /* configure */ },
    typeof(OrderAggregate).Assembly);

// EXISTING MANUAL REGISTRATION — still works (backward compat)
builder.Services.AddEventStoreClient<CounterProcessor>();

// MIX AND MATCH — manual + auto-discovered
builder.Services.AddEventStore();
builder.Services.AddEventStoreClient<LegacyProcessor>();
```

### Previous Story Intelligence (Story 16-3)

**Patterns established that MUST be followed:**
- Static `ConcurrentDictionary<K, V>` for reflection cache — already in `AssemblyScanner` and `NamingConventionEngine`
- `internal static void ClearCache()` for test isolation — `AssemblyScanner.ClearCache()` and `NamingConventionEngine.ClearCache()` must be called in test teardown
- CA1822 compliance: static methods where possible
- Test naming: `{Method}_{Scenario}_{ExpectedResult}` (e.g., `AddEventStore_NoAggregates_RegistersEmptyDiscoveryResult`)
- Test infrastructure: xunit + Assert (NOT Shouldly — existing registration tests use xunit Assert)
- One public type per file, file name = type name
- `AssemblyScanner` is the single source of truth for type discovery — NEVER scan independently

**Review fixes from 16-1, 16-2, and 16-3 (avoid repeating):**
- **Fail-fast for invalid inputs:** Throw with clear messages, never silently skip
- **Strict input validation:** Reject null, empty, and edge cases explicitly
- **Backward-compatibility verification:** Add a test proving `AddEventStoreClient<T>()` still works
- xUnit analyzer compliance: use `Assert.Empty()` not `Assert.Equal(0, ...)`, use `Assert.Single()` not `Assert.Equal(1, ...)`

### Git Intelligence

Recent commits:
```
3ee43c8 Merge pull request #70 from Hexalith/feat/epic-16-fluent-api-foundation
4e47cb6 feat: Add EventStoreAggregate base class, EventStoreDomain attribute, and NamingConventionEngine
1738881 Merge pull request #69 from Hexalith/feat/story-10-2-10-3-github-templates
```

The `InternalsVisibleTo` for the test project is already configured in `Hexalith.EventStore.Client.csproj`. The `Discovery/` folder with `AssemblyScanner` and related types already exists from story 16-3.

### Testing Expectations

**Test file location:**
- `tests/Hexalith.EventStore.Client.Tests/Registration/AddEventStoreTests.cs` (NEW)

**Test method naming convention:** `{Method}_{Scenario}_{ExpectedResult}`

**Test framework:** xunit + Assert (match existing `ServiceCollectionExtensionsTests.cs` patterns)

**Test approach — use explicit assembly overload for most tests:**
The `AddEventStore()` methods call `AssemblyScanner` internally. **WARNING:** Do NOT use the zero-config `AddEventStore()` overload in tests — `Assembly.GetCallingAssembly()` in a test context may return the xUnit runner assembly instead of the test project. Always use the explicit `AddEventStore(params Assembly[])` overload with `typeof(SmokeTestAggregate).Assembly` to guarantee the correct assembly is scanned. The public smoke stubs from story 16-3 (`SmokeTestAggregate`, `SmokeTestProjection`) serve as discoverable types.

**Key test scenarios:**

| # | Test | Setup | Expected |
|---|------|-------|----------|
| 1 | Zero-config registers aggregates | `AddEventStore()` with test assembly containing public stubs | `IDomainProcessor` resolves to aggregate instance |
| 2 | Options lambda applied | `AddEventStore(opts => ...)` | `IOptions<EventStoreOptions>` available with configured values |
| 3 | Explicit assembly overload | `AddEventStore(typeof(TestStub).Assembly)` | Discovers stubs from specified assembly |
| 4 | DiscoveryResult singleton | `AddEventStore()` then resolve `DiscoveryResult` | Returns non-null result with discovered types |
| 5 | Keyed service resolution | `AddEventStore()` then resolve `IDomainProcessor` by domain name key | Returns correct aggregate type |
| 6 | Idempotent registration | Call `AddEventStore()` twice | No duplicate service descriptors |
| 7 | Null services | `null.AddEventStore()` | `ArgumentNullException` |
| 8 | Null configureOptions | `AddEventStore(null!)` | `ArgumentNullException` |
| 9 | Null/empty assemblies | `AddEventStore(assemblies: null!)` or `AddEventStore()` with empty array | Appropriate exception |
| 10 | Backward compat | `AddEventStoreClient<T>()` then `AddEventStore()` | Both registrations work |
| 11 | Empty assembly (no domain types) | `AddEventStore(assemblyWithNoAggregates)` | Registers `DiscoveryResult` with empty lists, no exceptions |
| 12 | Multiple assemblies | `AddEventStore(asm1, asm2)` | Combined discovery from both |
| 13 | Options + assemblies combined | `AddEventStore(opts => ..., asm)` | Both options and assembly scanning work |

**Test infrastructure notes:**
- Implement `IDisposable` on test class: call `AssemblyScanner.ClearCache()` and `NamingConventionEngine.ClearCache()` in `Dispose()` to guarantee cleanup
- Use `ServiceCollection` (not mock) for real DI container testing
- Use `BuildServiceProvider()` to resolve and verify registrations
- Reuse the public smoke test stubs from story 16-3 (`SmokeTestAggregate`, `SmokeTestProjection` in the test project) OR create dedicated test stubs if isolation is needed
- **CRITICAL:** Must add `using Microsoft.Extensions.DependencyInjection;` for keyed service resolution in tests
- **WARNING:** Do NOT test the zero-config `AddEventStore()` overload for assembly discovery — `GetCallingAssembly()` is unreliable in test runners. Test it only for "method exists and doesn't throw" if needed. Use explicit assembly overloads for all discovery verification tests.

### Required Using Directives (for the extension methods file)

```csharp
using System.Reflection;
using System.Runtime.CompilerServices;
using Hexalith.EventStore.Client.Configuration;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions; // for TryAdd if needed
```

### Scope Boundary

**IN scope for 16-4:**
- `AddEventStore()` extension methods (all overloads)
- `EventStoreOptions` class (minimal, empty or near-empty)
- Aggregate registration as `IDomainProcessor` (non-keyed + keyed)
- `DiscoveryResult` singleton registration
- Idempotency check
- Unit tests

**NOT in scope for 16-4 (handled by later stories):**
- `UseEventStore()` activation method (Story 16-5)
- `EventStoreDomainOptions` per-domain options (Story 16-6)
- Cascading configuration resolution (Story 16-6)
- `OnConfiguring()` override on aggregates (Story 16-6)
- Projection DI registration — projections are NOT `IDomainProcessor` implementations and are NOT registered as services. They exist in the `DiscoveryResult` singleton for Story 16-5 to consume for subscription activation. Do NOT add projection registration in this story.
- `appsettings.json` binding (Story 16-6)

### Project Structure Notes

- New `Configuration/` folder follows architecture document's prescribed structure (line 748-751)
- Extension methods added to existing `EventStoreServiceCollectionExtensions.cs` (not a new file)
- File naming follows existing convention: one public type per file, file name = type name
- No conflicts with existing code — purely additive

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-02-28.md#Section 4.3] — Epic 16 story definition (Story 16-4: AddEventStore Extension Method with Global Options)
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-02-28.md#Section 4.2] — Architecture changes: Configuration/ folder, 5-layer cascade
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-02-28.md#Section 3] — Progressive disclosure: Beginner API = `AddEventStore()`
- [Source: _bmad-output/planning-artifacts/architecture.md#Convention Override Priority] — 5-layer cascade, Layer 2 = `AddEventStore(options => ...)`
- [Source: _bmad-output/planning-artifacts/architecture.md#Structure Patterns] — Configuration = `*Options.cs` record types, DI = `Add*` extension methods
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure] — Configuration/EventStoreOptions.cs (line 748-751)
- [Source: src/Hexalith.EventStore.Client/Discovery/AssemblyScanner.cs] — Assembly scanning (MUST use, never scan independently)
- [Source: src/Hexalith.EventStore.Client/Discovery/DiscoveryResult.cs] — Scan result to register as singleton
- [Source: src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs] — Existing AddEventStoreClient<T>() to preserve
- [Source: src/Hexalith.EventStore.Client/Handlers/IDomainProcessor.cs] — Service interface for aggregate registration
- [Source: _bmad-output/implementation-artifacts/16-3-assembly-scanner-and-auto-discovery.md] — Previous story patterns and learnings
- [Source: _bmad-output/planning-artifacts/prd.md#FR22] — FR22: Convention-based assembly scanning registration
- [Source: _bmad-output/planning-artifacts/prd.md#FR42] — FR42: Zero-configuration quickstart via auto-discovery
- [Source: _bmad-output/planning-artifacts/prd.md#FR48] — FR48: EventStoreAggregate with convention-based naming

## Change Log

- 2026-02-28: Implemented AddEventStore() extension methods with 4 overloads, EventStoreOptions class, aggregate registration (non-keyed + keyed), DiscoveryResult singleton, idempotency, and 13 unit tests. All 156 tests pass.
- 2026-02-28: Senior code review follow-up fixes applied: added explicit zero-config overload coverage, verified configured EventStoreOptions value application, clarified XML docs that only aggregates are registered as services, and aligned story/testing metadata counts.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

- Build compilation error CS0121 (ambiguous null! cast between `Action<EventStoreOptions>` and `Assembly[]` overloads) — resolved with explicit cast in test
- Build warning CS0184 (SmokeTestProjection never implements IDomainProcessor) — resolved by checking service descriptors instead of runtime type check

### Completion Notes List

- Created `EventStoreOptions` as an empty POCO class in `Configuration/` folder per AC5
- Added minimal `EventStoreOptions.EnableRegistrationDiagnostics` property to verify options-lambda application through `IOptions<EventStoreOptions>`
- Implemented all 4 `AddEventStore()` overloads delegating to a private `AddEventStoreCore()` method
- Zero-config and options-only overloads use `[MethodImpl(MethodImplOptions.NoInlining)]` with `Assembly.GetCallingAssembly()` per AC13
- Aggregates registered as non-keyed `IDomainProcessor` (AC6) and keyed by domain name (AC7)
- `DiscoveryResult` registered as singleton (AC8)
- Idempotency via `DiscoveryResult` marker check (AC10)
- All parameter validation with `ArgumentNullException.ThrowIfNull` and `ArgumentException` for empty arrays (AC9)
- Projections discovered in `DiscoveryResult` but NOT registered as services (AC14)
- Existing `AddEventStoreClient<T>()` unchanged (AC11)
- No new NuGet dependencies (AC12)
- 15 unit tests in `AddEventStoreTests` covering all acceptance criteria, including zero-config calling-assembly behavior and options value application; full `Hexalith.EventStore.Client.Tests` now passes with 157/157 tests.

### File List

- `src/Hexalith.EventStore.Client/Configuration/EventStoreOptions.cs` (NEW)
- `src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs` (MODIFIED)
- `tests/Hexalith.EventStore.Client.Tests/Registration/AddEventStoreTests.cs` (NEW)

### Senior Developer Review (AI)

#### Reviewer

GitHub Copilot (GPT-5.3-Codex)

#### Review Date

2026-02-28

#### Outcome

Approved after fixes.

#### Findings Resolved

- High: Task 3.2 had been marked complete without explicit zero-config overload coverage. Added dedicated test using a no-inline helper to validate calling-assembly discovery and aggregate registration.
- High: Task 3.13 had been marked complete without proving configured option values. Added minimal options property and assertion via `IOptions<EventStoreOptions>`.
- Medium: XML summary text suggested projections were registered in DI. Updated summary to state only aggregates are registered and discovery results include projections.
- Medium: Story metadata listed 13 tests while file contained more. Updated story to reflect current test inventory and suite totals.

#### Validation Evidence

- Focused test file run: `AddEventStoreTests.cs` passing.
- Full project run: `Hexalith.EventStore.Client.Tests` passing (`157 passed, 0 failed`).
