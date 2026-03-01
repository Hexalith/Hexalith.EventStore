# Story 16.6: Five-Layer Cascading Configuration

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a domain service developer,
I want per-domain configuration options that cascade through five layers (convention defaults, global code options, domain self-config, external config, explicit override),
so that I can customize individual domain behaviors at the most appropriate level — from zero-config conventions to deployment-time overrides — without abandoning the convention-based defaults for everything else.

## Acceptance Criteria

1. **AC1 — `EventStoreDomainOptions` class:** A new `public class EventStoreDomainOptions` exists in `Hexalith.EventStore.Client/Configuration/EventStoreDomainOptions.cs`. It is a POCO with settable properties following the standard .NET Options pattern. Properties include:
   - `string? StateStoreName` — overrides convention-derived `{domain}-eventstore`
   - `string? TopicPattern` — overrides convention-derived `{domain}.events`
   - `string? DeadLetterTopicPattern` — overrides convention-derived `deadletter.{domain}.events`
   - All properties default to `null`, meaning "use convention default". A non-null value overrides the convention.

2. **AC2 — `OnConfiguring()` virtual method on `EventStoreAggregate<TState>`:** A `protected virtual void OnConfiguring(EventStoreDomainOptions options)` method exists on `EventStoreAggregate<TState>`. The default implementation is empty (no-op). Subclasses can override to set per-domain options imperatively. This method is called once during cascade resolution, NOT during command processing.

3. **AC3 — `appsettings.json` binding:** During `AddEventStore()`, the SDK binds the `EventStore` configuration section to `EventStoreOptions` via `services.Configure<EventStoreOptions>(configuration.GetSection("EventStore"))` when an `IConfiguration` is available in the service collection. Per-domain settings bind from `EventStore:Domains:{domainName}` sections. No `appsettings.json` section is required — the binding is opportunistic (no error if section is absent).

4. **AC4 — Explicit per-domain override via `AddEventStore()` lambda:** The `EventStoreOptions` class gains a method `ConfigureDomain(string domainName, Action<EventStoreDomainOptions> configure)` that registers an explicit per-domain configuration callback. Example: `options.ConfigureDomain("counter", d => d.StateStoreName = "custom-counter-store")`. These callbacks are stored and applied during cascade resolution as Layer 5 (highest priority).

5. **AC5 — Five-layer cascade resolution:** During `UseEventStore()`, for each discovered domain, the cascade resolver produces a final `EventStoreDomainOptions` by applying layers in order (each layer overrides non-null values from prior layers):
   - **Layer 1 — Convention defaults:** `NamingConventionEngine`-derived names (state store = `{domain}-eventstore`, topic = `{domain}.events`, dead-letter = `deadletter.{domain}.events`). Always applied.
   - **Layer 2 — Global code options:** Global defaults from `EventStoreOptions` properties (e.g., a global `DefaultStateStorePrefix` that changes the suffix pattern). Applied if set.
   - **Layer 3 — Domain self-config:** `OnConfiguring()` override on the aggregate/projection type. Applied by instantiating the type via `Activator.CreateInstance()` (parameterless constructor required by `where TState : class, new()` constraint on the aggregate, but the aggregate itself needs a parameterless constructor too — use `Activator.CreateInstance(domainType)` with try/catch for types that don't support it, skip Layer 3 gracefully with a debug log).
   - **Layer 4 — External config:** `appsettings.json` section `EventStore:Domains:{domainName}` bound via `IConfiguration`. Applied if section exists.
   - **Layer 5 — Explicit override:** `ConfigureDomain()` callbacks registered in the `AddEventStore()` lambda. Applied if registered.

6. **AC6 — Resolved options stored in `EventStoreDomainActivation`:** The `EventStoreDomainActivation` record (from Story 16-5) already has `StateStoreName`, `TopicPattern`, and `DeadLetterTopicPattern` properties. After cascade resolution, these properties reflect the final resolved values (not just convention defaults). No new properties needed on `EventStoreDomainActivation` — the existing properties now carry resolved values instead of convention-only values.

7. **AC7 — `EventStoreOptions` extensions:** Extend `EventStoreOptions` with:
   - `Dictionary<string, Action<EventStoreDomainOptions>> DomainConfigurations` — internal storage for `ConfigureDomain()` callbacks (not user-facing, used by cascade resolver)
   - `string? DefaultStateStoreSuffix` — global override for state store suffix (default: `"eventstore"`, producing `{domain}-{suffix}`)
   - `string? DefaultTopicSuffix` — global override for topic suffix (default: `"events"`, producing `{domain}.{suffix}`)
   - The `ConfigureDomain()` method stores callbacks in `DomainConfigurations`

8. **AC8 — No new NuGet dependencies:** The implementation uses only packages already available in the Client SDK (`Microsoft.Extensions.Options`, `Microsoft.Extensions.Configuration.Abstractions` — both transitively available). Do NOT add `Microsoft.Extensions.Configuration.Binder` or `Microsoft.Extensions.Options.ConfigurationExtensions` as direct references unless they are NOT transitively available (verify first). If they are needed and not transitive, add them — but document the reason.

9. **AC9 — Backward compatibility:** All existing Story 16-4 and 16-5 tests continue to pass without modification. Applications that don't use any configuration beyond `AddEventStore()` / `UseEventStore()` see identical behavior — convention defaults are the same as before. The cascade resolver produces the same `EventStoreDomainActivation` values as the current convention-only logic when no overrides are configured.

10. **AC10 — Cascade resolution logging:** When `EnableRegistrationDiagnostics` is `true`, log at `Debug` level for each domain which layers contributed to the final configuration. Example: `"Domain 'counter' configuration resolved: Layer 1 (convention), Layer 4 (appsettings.json: StateStoreName)"`

11. **AC11 — `OnConfiguring()` on `EventStoreProjection<TReadModel>`:** Add the same `protected virtual void OnConfiguring(EventStoreDomainOptions options)` to `EventStoreProjection<TReadModel>` (if it exists as a base class). If projections don't have a base class yet, skip this — projections can use Layers 4-5 for overrides.

12. **AC12 — Null domain name guard in `ConfigureDomain()`:** `ConfigureDomain()` validates `domainName` with `ArgumentException.ThrowIfNullOrWhiteSpace(domainName)`.

## Tasks / Subtasks

- [x] Task 1: Create `EventStoreDomainOptions` class (AC: #1)
  - [x] 1.1: Create `src/Hexalith.EventStore.Client/Configuration/EventStoreDomainOptions.cs`
  - [x] 1.2: Implement POCO class with nullable `StateStoreName`, `TopicPattern`, `DeadLetterTopicPattern` properties
  - [x] 1.3: Add XML documentation explaining null = convention default

- [x] Task 2: Extend `EventStoreOptions` with domain configuration support (AC: #4, #7)
  - [x] 2.1: Add `DomainConfigurations` dictionary (internal, not user-facing)
  - [x] 2.2: Add `ConfigureDomain(string domainName, Action<EventStoreDomainOptions> configure)` method with null guard (AC12)
  - [x] 2.3: Add `DefaultStateStoreSuffix` and `DefaultTopicSuffix` nullable properties
  - [x] 2.4: Add XML documentation

- [x] Task 3: Add `OnConfiguring()` to `EventStoreAggregate<TState>` (AC: #2)
  - [x] 3.1: Add `protected virtual void OnConfiguring(EventStoreDomainOptions options)` method
  - [x] 3.2: Default implementation is empty (no-op)
  - [x] 3.3: Add XML documentation explaining this is Layer 3 of cascade

- [x] Task 4: Add `OnConfiguring()` to `EventStoreProjection<TReadModel>` if applicable (AC: #11)
  - [x] 4.1: Check if `EventStoreProjection<TReadModel>` base class exists — YES, it exists
  - [x] 4.2: Added same `OnConfiguring()` method with `InvokeOnConfiguring()` internal entry point

- [x] Task 5: Implement `appsettings.json` binding in `AddEventStore()` (AC: #3)
  - [x] 5.1: Per ADR-3/ADR-4, IConfiguration resolution happens in UseEventStore() via host.Services.GetService<IConfiguration>()
  - [x] 5.2: Binding happens during cascade resolution (Layer 4) in UseEventStore()
  - [x] 5.3: Per-domain binding (`EventStore:Domains:{name}`) happens during cascade resolution in `UseEventStore()`

- [x] Task 6: Implement cascade resolution in `UseEventStore()` (AC: #5, #6, #10)
  - [x] 6.1: Created `ResolveDomainOptions` internal static method in `EventStoreHostExtensions`
  - [x] 6.2: Apply Layer 1: Convention defaults from `NamingConventionEngine`
  - [x] 6.3: Apply Layer 2: Global defaults from `EventStoreOptions` (`DefaultStateStoreSuffix`, `DefaultTopicSuffix`)
  - [x] 6.4: Apply Layer 3: `OnConfiguring()` — instantiate domain type via Activator.CreateInstance, call InvokeOnConfiguring via reflection, merge non-null results
  - [x] 6.5: Apply Layer 4: `IConfiguration` section `EventStore:Domains:{domainName}`, bind to `EventStoreDomainOptions`, merge non-null
  - [x] 6.6: Apply Layer 5: `ConfigureDomain()` callbacks from `EventStoreOptions.DomainConfigurations`
  - [x] 6.7: Use resolved values when constructing `EventStoreDomainActivation` (replacing current convention-only logic)
  - [x] 6.8: Add diagnostic logging showing which layers contributed (when `EnableRegistrationDiagnostics` is true)

- [x] Task 7: Create unit tests (AC: all)
  - [x] 7.1: Test convention-only cascade (no overrides) produces same results as current behavior
  - [x] 7.2: Test global `DefaultStateStoreSuffix` overrides convention suffix
  - [x] 7.3: Test `OnConfiguring()` override on aggregate changes per-domain values
  - [x] 7.4: Test `appsettings.json` binding overrides convention and OnConfiguring values
  - [x] 7.5: Test `ConfigureDomain()` explicit override takes highest priority
  - [x] 7.6: Test partial override (change StateStoreName, keep convention TopicPattern)
  - [x] 7.7: Test cascade with all 5 layers active simultaneously
  - [x] 7.8: Test `ConfigureDomain()` with null/empty/whitespace domain name throws `ArgumentException`
  - [x] 7.9: Test domain type without parameterless constructor skips Layer 3 gracefully
  - [x] 7.10: Test all existing 16-4 and 16-5 tests still pass (backward compat) — 185/185 pass
  - [x] 7.11: Test `EventStoreDomainOptions` defaults are all null

- [x] Task 8: Verify build and backward compatibility (AC: #8, #9)
  - [x] 8.1: Verify `dotnet build` succeeds with no warnings (Client + Client.Tests)
  - [x] 8.2: Verify ALL existing tests pass with zero modifications — 185/185 pass
  - [x] 8.3: Added `Microsoft.Extensions.Configuration.Binder` — needed for `IConfigurationSection.Bind()` (Layer 4), not transitively available from existing deps

## Dev Notes

### Architecture Constraints

- **Target framework:** net10.0 with `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`
- **Project:** `Hexalith.EventStore.Client` — depends only on `Hexalith.EventStore.Contracts` and `Dapr.Client` (plus `Microsoft.Extensions.Hosting.Abstractions` added in 16-5)
- **New files:** `EventStoreDomainOptions.cs` in `Configuration/`
- **Modified files:** `EventStoreOptions.cs`, `EventStoreAggregate.cs`, `EventStoreHostExtensions.cs`, `EventStoreServiceCollectionExtensions.cs`
- **One public type per file** — each new type in its own file
- **CRITICAL: No ASP.NET Core dependency** — the Client SDK is a plain class library

### Design Decisions

**ADR-1: Null-based override detection — DECIDED**
`EventStoreDomainOptions` uses nullable properties where `null` means "not set at this layer, use value from previous layer." This enables partial overrides — change one property without affecting others. This is the standard pattern used by EF Core's `DbContextOptions` and ASP.NET's layered configuration.

**ADR-2: `OnConfiguring()` instantiation strategy — DECIDED**
Layer 3 requires instantiating the domain type to call `OnConfiguring()`. Use `Activator.CreateInstance(domainType)` wrapped in try/catch. If the type lacks a parameterless constructor, skip Layer 3 with a `Debug` log: `"Domain '{name}': skipping OnConfiguring (no parameterless constructor)"`. This is non-fatal because:
- `EventStoreAggregate<TState>` has a parameterless constructor (it's a base class)
- Subclasses SHOULD have parameterless constructors for this pattern
- If they don't, Layers 4-5 still work for per-domain overrides
- The `OnConfiguring()` result is NOT cached — it's called once during `UseEventStore()` resolution

**ADR-3: Cascade resolution location — DECIDED**
The cascade runs inside `UseEventStore()` (not `AddEventStore()`), because:
- `IConfiguration` is available via `host.Services` (not just `IServiceCollection`)
- `DiscoveryResult` is finalized after `AddEventStore()` runs
- The activation context is populated during `UseEventStore()` — this is where final values are needed
- Keeps `AddEventStore()` focused on registration, `UseEventStore()` on resolution

**ADR-4: `IConfiguration` access pattern — DECIDED**
During `UseEventStore()`, resolve `IConfiguration` from `host.Services.GetService<IConfiguration>()` (nullable — may not be registered in minimal hosts). If available, use `configuration.GetSection("EventStore:Domains:{domainName}")` and bind to a new `EventStoreDomainOptions` instance. If `IConfiguration` is not registered or section doesn't exist, skip Layer 4.

**Cascade Resolution Pseudocode:**
```csharp
EventStoreDomainOptions ResolveDomainOptions(
    DiscoveredDomain domain,
    EventStoreOptions globalOptions,
    IConfiguration? configuration)
{
    // Layer 1: Convention defaults
    var resolved = new EventStoreDomainOptions
    {
        StateStoreName = NamingConventionEngine.GetStateStoreName(domain.DomainName),
        TopicPattern = $"{domain.DomainName}.events",
        DeadLetterTopicPattern = $"deadletter.{domain.DomainName}.events",
    };

    // Layer 2: Global overrides
    if (globalOptions.DefaultStateStoreSuffix is not null)
        resolved.StateStoreName = $"{domain.DomainName}-{globalOptions.DefaultStateStoreSuffix}";
    if (globalOptions.DefaultTopicSuffix is not null)
    {
        resolved.TopicPattern = $"{domain.DomainName}.{globalOptions.DefaultTopicSuffix}";
        resolved.DeadLetterTopicPattern = $"deadletter.{domain.DomainName}.{globalOptions.DefaultTopicSuffix}";
    }

    // Layer 3: Domain self-config (OnConfiguring)
    try
    {
        if (Activator.CreateInstance(domain.Type) is EventStoreAggregate aggregate)
        {
            var domainOpts = new EventStoreDomainOptions();
            aggregate.CallOnConfiguring(domainOpts); // internal method
            MergeNonNull(resolved, domainOpts);
        }
    }
    catch { /* skip Layer 3 */ }

    // Layer 4: appsettings.json
    if (configuration is not null)
    {
        var section = configuration.GetSection($"EventStore:Domains:{domain.DomainName}");
        if (section.Exists())
        {
            var configOpts = new EventStoreDomainOptions();
            section.Bind(configOpts);
            MergeNonNull(resolved, configOpts);
        }
    }

    // Layer 5: Explicit overrides
    if (globalOptions.DomainConfigurations.TryGetValue(domain.DomainName, out var configure))
    {
        var explicitOpts = new EventStoreDomainOptions();
        configure(explicitOpts);
        MergeNonNull(resolved, explicitOpts);
    }

    return resolved;
}

void MergeNonNull(EventStoreDomainOptions target, EventStoreDomainOptions source)
{
    if (source.StateStoreName is not null) target.StateStoreName = source.StateStoreName;
    if (source.TopicPattern is not null) target.TopicPattern = source.TopicPattern;
    if (source.DeadLetterTopicPattern is not null) target.DeadLetterTopicPattern = source.DeadLetterTopicPattern;
}
```

### Key Interfaces & Types to Reference

| Type | Location | Role |
|------|----------|------|
| `EventStoreOptions` | `Client/Configuration/EventStoreOptions.cs` | Global options — extend with cascade support |
| `EventStoreAggregate<TState>` | `Client/Aggregates/EventStoreAggregate.cs` | Add `OnConfiguring()` virtual method |
| `EventStoreHostExtensions` | `Client/Registration/EventStoreHostExtensions.cs` | Modify `UseEventStore()` to use cascade resolver |
| `EventStoreActivationContext` | `Client/Registration/EventStoreActivationContext.cs` | Unchanged — receives resolved values |
| `EventStoreDomainActivation` | `Client/Registration/EventStoreDomainActivation.cs` | Unchanged — carries resolved values |
| `NamingConventionEngine` | `Client/Conventions/NamingConventionEngine.cs` | Layer 1 convention defaults |
| `DiscoveryResult` | `Client/Discovery/DiscoveryResult.cs` | Input to cascade resolver |
| `DiscoveredDomain` | `Client/Discovery/DiscoveredDomain.cs` | Per-domain metadata |
| `IConfiguration` | `Microsoft.Extensions.Configuration.Abstractions` | Layer 4 external config |

### Expected File Structure After Implementation

```
src/Hexalith.EventStore.Client/
├── Aggregates/
│   └── EventStoreAggregate.cs             # MODIFIED — add OnConfiguring()
├── Configuration/
│   ├── EventStoreDomainOptions.cs         # NEW — per-domain options
│   └── EventStoreOptions.cs              # MODIFIED — add ConfigureDomain(), suffixes
├── Conventions/
│   └── NamingConventionEngine.cs         # UNCHANGED
├── Discovery/                             # UNCHANGED
├── Registration/
│   ├── EventStoreActivationContext.cs    # UNCHANGED
│   ├── EventStoreDomainActivation.cs     # UNCHANGED
│   ├── EventStoreHostExtensions.cs       # MODIFIED — cascade resolution
│   └── EventStoreServiceCollectionExtensions.cs  # MODIFIED — appsettings binding
```

### API Shape (Expected Usage After This Story)

```csharp
// === LAYER 1: Convention only (same as before) ===
builder.Services.AddEventStore();
host.UseEventStore();
// counter-eventstore, counter.events, deadletter.counter.events

// === LAYER 2: Global suffix override ===
builder.Services.AddEventStore(options =>
{
    options.DefaultStateStoreSuffix = "store";  // now: counter-store
});

// === LAYER 3: Domain self-config ===
public class BillingAggregate : EventStoreAggregate<BillingState>
{
    protected override void OnConfiguring(EventStoreDomainOptions options)
    {
        options.StateStoreName = "postgresql-billing";
    }
}

// === LAYER 4: appsettings.json ===
// appsettings.json:
// {
//   "EventStore": {
//     "Domains": {
//       "counter": {
//         "StateStoreName": "redis-counter-store"
//       }
//     }
//   }
// }

// === LAYER 5: Explicit override (highest priority) ===
builder.Services.AddEventStore(options =>
{
    options.ConfigureDomain("counter", d =>
    {
        d.StateStoreName = "custom-counter-store";
    });
});
```

### Previous Story Intelligence (Story 16-5)

**Patterns established that MUST be followed:**
- `ArgumentNullException.ThrowIfNull()` for parameter validation
- `ArgumentException.ThrowIfNullOrWhiteSpace()` for string validation
- CA1822 compliance: static methods where possible
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`
- Test framework: xunit + Assert (NOT Shouldly)
- One public type per file, file name = type name
- `AssemblyScanner.ClearCache()` and `NamingConventionEngine.ClearCache()` in test teardown
- Discarding return values with `_ =` for method chains

**Key decisions from 16-5 relevant to 16-6:**
- `EventStoreDomainActivation` already has `StateStoreName`, `TopicPattern`, `DeadLetterTopicPattern` — these now carry cascade-resolved values instead of convention-only
- `UseEventStore()` is where activation happens — cascade resolution fits naturally here
- `EventStoreActivationContext.TryActivate()` pattern unchanged
- The `IHost` extension pattern (not `WebApplication`) is locked in

**Review fixes from previous stories (avoid repeating):**
- Fail-fast for invalid inputs: throw with clear messages, never silently skip
- Strict input validation: reject null, empty, and edge cases explicitly
- xUnit analyzer compliance: use `Assert.Empty()`, `Assert.Single()`, etc.
- Every public method needs XML documentation

### Git Intelligence

Recent commits show Epic 16 pattern:
```
016c01b fix(story-16-4): close review findings and mark done (#72)
633b985 feat: Add AssemblyScanner auto-discovery and harden aggregate/projection error handling
4e47cb6 feat: Add EventStoreAggregate base class, EventStoreDomain attribute, and NamingConventionEngine
```

The `EventStoreAggregate<TState>` base class (from 16-1) uses reflection-based command dispatch with `ConcurrentDictionary<Type, AggregateMetadata>` caching. Adding `OnConfiguring()` is a simple virtual method addition — no impact on the dispatch pipeline.

### Testing Expectations

**Test file location:**
- `tests/Hexalith.EventStore.Client.Tests/Configuration/CascadeConfigurationTests.cs` (NEW)

**Test method naming convention:** `{Method}_{Scenario}_{ExpectedResult}`

**Test framework:** xunit + Assert (match existing test patterns)

**Test approach:** Build real `IHost` with `IConfiguration` from in-memory provider for appsettings simulation.

**Key test scenarios:**

| # | Test | Setup | Expected |
|---|------|-------|----------|
| 1 | Convention only (backward compat) | `AddEventStore(asm)` + `UseEventStore()` | Same activation values as 16-5 |
| 2 | Global suffix override | `DefaultStateStoreSuffix = "store"` | `{domain}-store` instead of `{domain}-eventstore` |
| 3 | OnConfiguring per-domain | Aggregate with `OnConfiguring()` setting StateStoreName | Per-domain value used |
| 4 | appsettings.json override | In-memory config with `EventStore:Domains:counter:StateStoreName` | Config value overrides convention |
| 5 | ConfigureDomain explicit | `options.ConfigureDomain("counter", d => ...)` | Explicit value wins |
| 6 | Full 5-layer cascade | All layers set, each setting different property | Correct priority per property |
| 7 | Partial override | Only StateStoreName overridden, topic stays convention | Non-overridden properties keep convention value |
| 8 | ConfigureDomain null name | `options.ConfigureDomain(null!, ...)` | `ArgumentException` |
| 9 | No parameterless constructor | Domain type without default ctor | Layer 3 skipped, others work |
| 10 | Missing config section | No `EventStore:Domains` in config | Layer 4 skipped, others work |
| 11 | EventStoreDomainOptions defaults | `new EventStoreDomainOptions()` | All properties null |

**Test infrastructure notes:**
- Use `ConfigurationBuilder().AddInMemoryCollection(...)` for appsettings simulation
- Use `Host.CreateDefaultBuilder().ConfigureAppConfiguration(...)` for config injection
- Create a test aggregate with `OnConfiguring()` override in test project
- Call `AssemblyScanner.ClearCache()` and `NamingConventionEngine.ClearCache()` in `Dispose()`

### Scope Boundary

**IN scope for 16-6:**
- `EventStoreDomainOptions` per-domain options class
- `OnConfiguring()` virtual method on `EventStoreAggregate<TState>`
- `EventStoreOptions` extensions (`ConfigureDomain()`, `DefaultStateStoreSuffix`, `DefaultTopicSuffix`)
- `appsettings.json` binding for `EventStore` and `EventStore:Domains:{name}`
- Five-layer cascade resolution in `UseEventStore()`
- Diagnostic logging for cascade resolution
- Unit tests for all cascade scenarios

**NOT in scope for 16-6 (handled by later stories):**
- Advanced serializer configuration (`UseSerializer<T>()`) — future story
- Multi-tenancy configuration (`MultiTenancy.Enabled`, `TenantResolver`) — future story
- DAPR component configuration (`Dapr.PubSubComponent`, `Dapr.StateStoreComponent`) — future story
- Retry policy configuration — future story
- `UseEventStore()` selective activation (`activate => activate.CommandEndpoints = false`) — future story
- Sample app integration (Story 16-7)
- WebApplication compatibility testing (Story 16-10)

### Project Structure Notes

- `EventStoreDomainOptions.cs` follows architecture document's prescribed structure in `Configuration/`
- `OnConfiguring()` follows EF Core's `DbContext.OnConfiguring()` pattern (familiar to .NET developers)
- Cascade resolution is additive to `UseEventStore()` — modifies how `EventStoreDomainActivation` values are computed
- No new public types beyond `EventStoreDomainOptions` — cascade resolution is internal

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-02-28.md#Section 4.2] — 5-layer cascade table
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-02-28.md#Section 4.3] — Story 16-6 definition
- [Source: _bmad-output/planning-artifacts/architecture.md#Convention Override Priority] — 5-layer cascade priority rules
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure] — `EventStoreDomainOptions.cs` in `Configuration/`
- [Source: _bmad-output/brainstorming/brainstorming-session-2026-02-28.md#SCAMPER-A #1] — OnConfiguring pattern
- [Source: _bmad-output/brainstorming/brainstorming-session-2026-02-28.md#SCAMPER-E #1] — Optional appsettings.json binding
- [Source: _bmad-output/brainstorming/brainstorming-session-2026-02-28.md#Theme 3] — Cascading configuration theme
- [Source: _bmad-output/brainstorming/brainstorming-session-2026-02-28.md#Priority 3] — Cascade implementation action plan
- [Source: src/Hexalith.EventStore.Client/Configuration/EventStoreOptions.cs] — Current global options to extend
- [Source: src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs] — Base class to add OnConfiguring()
- [Source: src/Hexalith.EventStore.Client/Registration/EventStoreHostExtensions.cs] — UseEventStore() to modify for cascade
- [Source: src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs] — Convention engine for Layer 1
- [Source: _bmad-output/implementation-artifacts/16-5-use-eventstore-extension-method-with-activation.md] — Previous story patterns

## Change Log

- 2026-02-28: Implemented five-layer cascading configuration (Tasks 1-8). Added `EventStoreDomainOptions` POCO, extended `EventStoreOptions` with `ConfigureDomain()` and global suffix overrides, added `OnConfiguring()` to both `EventStoreAggregate<TState>` and `EventStoreProjection<TReadModel>`, implemented cascade resolver in `UseEventStore()`, added `Microsoft.Extensions.Configuration.Binder` dependency, created 15 unit tests covering all cascade scenarios. All 185 tests pass.
- 2026-03-01: Applied AI review remediation. Added opportunistic `EventStore` section binding in `AddEventStore()` (`EventStoreServiceCollectionExtensions`), hardened Layer 3 cascade resolution to gracefully skip on broader activation/invocation failures, and added regression tests verifying appsettings global binding at registration and runtime activation resolution.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- CA1062 compliance required `ArgumentNullException.ThrowIfNull(options)` in test stub `OnConfiguring()` overrides
- Test stubs made `internal` to avoid polluting `GetExportedTypes()` discovery in existing tests that scan the test assembly
- `Microsoft.Extensions.Configuration.Binder` added as explicit dependency (not transitively available) for `IConfigurationSection.Bind()` support in Layer 4

### Completion Notes List

- Task 1: Created `EventStoreDomainOptions` POCO with 3 nullable properties and XML docs
- Task 2: Extended `EventStoreOptions` with `ConfigureDomain()`, `DefaultStateStoreSuffix`, `DefaultTopicSuffix`, internal `DomainConfigurations` dictionary
- Task 3: Added `OnConfiguring()` virtual method + `InvokeOnConfiguring()` internal method to `EventStoreAggregate<TState>`
- Task 4: Same pattern added to `EventStoreProjection<TReadModel>` (base class exists)
- Task 5: IConfiguration binding implemented as part of cascade resolution in UseEventStore() per ADR-3/ADR-4
- Task 6: Cascade resolver implemented as `ResolveDomainOptions()` internal static method with all 5 layers, diagnostic logging, and `MergeNonNull()` helper
- Task 7: 15 unit tests covering all scenarios (convention-only, global suffix, OnConfiguring, appsettings, explicit override, full cascade, partial override, null guards, no-ctor skip, backward compat, projection OnConfiguring)
- Task 8: Build clean (0 warnings, 0 errors for Client + Client.Tests), all 185 tests pass, 1 new dependency documented
- 2026-03-01 review remediation: AC3 gap closed by binding `EventStore` section in `AddEventStore()` when `IConfiguration` is present; added tests proving bound options flow into `UseEventStore()` resolved activation values.
- 2026-03-01 review remediation: Layer 3 graceful-skip behavior hardened with broader activation error handling while preserving debug diagnostics.

### File List

- `src/Hexalith.EventStore.Client/Configuration/EventStoreDomainOptions.cs` — NEW
- `src/Hexalith.EventStore.Client/Configuration/EventStoreOptions.cs` — MODIFIED
- `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs` — MODIFIED
- `src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs` — MODIFIED
- `src/Hexalith.EventStore.Client/Registration/EventStoreHostExtensions.cs` — MODIFIED
- `src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj` — MODIFIED
- `Directory.Packages.props` — MODIFIED
- `tests/Hexalith.EventStore.Client.Tests/Configuration/CascadeConfigurationTests.cs` — NEW

## Senior Developer Review (AI)

### Outcome

Review approved after fixes. Previously identified HIGH/MEDIUM findings were remediated in code and test coverage.

### Findings Closed

1. **AC3 implementation fidelity** — fixed by opportunistic binding of `EventStore` section in `AddEventStore()` when `IConfiguration` exists.
2. **Layer 3 graceful skip robustness** — fixed by broad activation-error catch/log path to avoid hard-failing `UseEventStore()` on non-fatal activation issues.
3. **Coverage gap for AC3** — fixed with new tests validating registration-time `EventStore` options binding and runtime suffix application in activation resolution.

### Validation Evidence

- Client test suite executed successfully after remediation (`Hexalith.EventStore.Client.Tests`).

