# Story 16.3: Assembly Scanner and Auto-Discovery

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a domain service developer,
I want my aggregate and projection types to be automatically discovered from assemblies at startup,
so that I can register all domain services with `AddEventStore()` without manually listing each type, enabling true zero-configuration domain service wiring.

## Acceptance Criteria

1. **AC1 — AssemblyScanner class exists:** A static `AssemblyScanner` class exists in `Hexalith.EventStore.Client/Discovery/AssemblyScanner.cs` that scans one or more assemblies for types inheriting from `EventStoreAggregate<>` or `EventStoreProjection<>`. This is the single source of truth for type discovery -- all downstream stories (16-4 through 16-10) must use this scanner, never discover types independently.

2. **AC2 — Discovers EventStoreAggregate subclasses:** `ScanForAggregates(Assembly assembly)` returns all concrete (non-abstract), non-generic types that inherit from `EventStoreAggregate<TState>` (for any `TState`). Abstract base classes and open generics are excluded.

3. **AC3 — Discovers EventStoreProjection subclasses:** `ScanForProjections(Assembly assembly)` returns all concrete (non-abstract), non-generic types that inherit from `EventStoreProjection<TReadModel>` (for any `TReadModel`). Abstract base classes and open generics are excluded.

4. **AC4 — Combined discovery method:** `ScanForDomainTypes(Assembly assembly)` returns a `DiscoveryResult` containing both aggregate types and projection types from a single scan pass for efficiency.

5. **AC5 — Multi-assembly scanning:** `ScanForDomainTypes(IEnumerable<Assembly> assemblies)` accepts multiple assemblies and returns a combined `DiscoveryResult` with de-duplicated types. This supports scenarios where domain types span multiple assemblies (e.g., shared aggregate libraries).

6. **AC6 — Domain name resolution integration:** Each discovered type's domain name is resolved via `NamingConventionEngine.GetDomainName(type)` during discovery. If naming resolution throws `ArgumentException` (invalid name), the scanner wraps it in a clear error message naming the offending type and assembly, and throws `InvalidOperationException`.

7. **AC7 — Duplicate domain name detection (within-category):** Duplicate detection is scoped per category: aggregates are checked against aggregates, projections against projections. An aggregate and a projection sharing the same domain name (e.g., `OrderAggregate` and `OrderProjection` both producing `"order"`) is VALID and expected — they represent the same domain from different perspectives. Two aggregates (or two projections) resolving to the same domain name — even across different assemblies in a multi-assembly scan — throws `InvalidOperationException` with a message listing both conflicting types, their assemblies, and the duplicate domain name.

8. **AC8 — DiscoveryResult record:** An immutable `DiscoveryResult` record (or class) is returned containing:
   - `IReadOnlyList<DiscoveredDomain> Aggregates` — discovered aggregate types with domain names
   - `IReadOnlyList<DiscoveredDomain> Projections` — discovered projection types with domain names
   - `int TotalCount` — total discovered types (convenience property)

9. **AC9 — DiscoveredDomain record:** Each discovered type is represented as a `DiscoveredDomain` sealed record containing:
   - `Type Type` — the discovered CLR type
   - `string DomainName` — the resolved domain name (from `NamingConventionEngine`)
   - `Type StateType` — the `TState` or `TReadModel` generic argument extracted from the base class
   - `DomainKind Kind` — enum value (`Aggregate` or `Projection`) so downstream consumers (Story 16-4) know the type category without re-checking inheritance

10. **AC10 — DomainKind enum:** A `DomainKind` enum exists in `Hexalith.EventStore.Client/Discovery/DomainKind.cs` with values `Aggregate` and `Projection`.

11. **AC11 — Thread-safe caching:** Assembly scan results are cached in a `ConcurrentDictionary<Assembly, DiscoveryResult>` so that repeated scans of the same assembly are O(1). An `internal static void ClearCache()` method is provided for test isolation (exposed via `InternalsVisibleTo`).

12. **AC12 — Zero new NuGet dependencies:** Implementation uses only `System.Reflection` (framework-implicit) and existing Client types. No new package references added to `Hexalith.EventStore.Client.csproj`.

13. **AC13 — Backward compatibility:** No existing public types are modified. `IDomainProcessor`, `DomainProcessorBase<TState>`, `EventStoreAggregate<TState>`, `EventStoreProjection<TReadModel>`, `NamingConventionEngine`, `EventStoreDomainAttribute`, and `AddEventStoreClient<TProcessor>()` remain unchanged.

14. **AC14 — Scope boundary:** The `AddEventStore()` extension method is NOT in scope for this story. DI registration integration is Story 16-4. This story delivers ONLY the assembly scanning engine and its result types.

## Tasks / Subtasks

- [x] Task 1: Create `DomainKind` enum and `DiscoveredDomain` record (AC: #9, #10)
  - [x] 1.1: Create `src/Hexalith.EventStore.Client/Discovery/` folder
  - [x] 1.2: Implement `DomainKind` enum (`Aggregate`, `Projection`) in `DomainKind.cs`
  - [x] 1.3: Implement `DiscoveredDomain` sealed record with `Type`, `DomainName`, `StateType`, and `Kind` properties

- [x] Task 2: Create `DiscoveryResult` record (AC: #8)
  - [x] 2.1: Implement `DiscoveryResult` record with `Aggregates`, `Projections`, and `TotalCount` properties

- [x] Task 3: Create `AssemblyScanner` static class (AC: #1, #2, #3, #4, #5, #6, #7, #11)
  - [x] 3.1: Implement `ScanForAggregates(Assembly assembly)` — finds concrete types inheriting `EventStoreAggregate<>`
  - [x] 3.2: Implement `ScanForProjections(Assembly assembly)` — finds concrete types inheriting `EventStoreProjection<>`
  - [x] 3.3: Implement `ScanForDomainTypes(Assembly assembly)` — single-pass combined scan returning `DiscoveryResult`
  - [x] 3.4: Implement `ScanForDomainTypes(IEnumerable<Assembly> assemblies)` — multi-assembly with de-duplication and null-per-element guard
  - [x] 3.5: Implement `internal static DiscoveryResult ScanForDomainTypes(IEnumerable<Type> types)` — internal overload for testability with internal stub types
  - [x] 3.6: Implement generic type argument extraction (get `TState` from `EventStoreAggregate<TState>`) with `!result.IsGenericParameter` safety assertion
  - [x] 3.7: Integrate `NamingConventionEngine.GetDomainName(type)` for domain name resolution
  - [x] 3.8: Implement within-category duplicate domain name detection (aggregates vs aggregates, projections vs projections) with clear error messaging including assembly info
  - [x] 3.9: Implement `ConcurrentDictionary<Assembly, DiscoveryResult>` caching with `internal static void ClearCache()`

- [x] Task 4: Create unit tests (AC: all)
  - [x] 4.1: Create `tests/Hexalith.EventStore.Client.Tests/Discovery/AssemblyScannerTests.cs`
  - [x] 4.2: Create test stub types (concrete aggregates, projections, abstract classes, open generics, intermediate generic base, nested public type)
  - [x] 4.3: Test aggregate discovery (finds concrete, excludes abstract and open generic) — assert EXACT count
  - [x] 4.4: Test projection discovery (finds concrete, excludes abstract and open generic) — assert EXACT count
  - [x] 4.5: Test combined scan returns both aggregates and projections with correct `TotalCount`
  - [x] 4.6: Test multi-assembly scan with de-duplication
  - [x] 4.7: Test domain name resolution integration (convention-derived and attribute-override)
  - [x] 4.8: Test within-category duplicate domain name throws `InvalidOperationException`
  - [x] 4.9: Test cross-category same domain name (aggregate + projection = "order") succeeds without error
  - [x] 4.10: Test cross-assembly same-category duplicate domain name throws `InvalidOperationException`
  - [x] 4.11: Test naming engine error wrapping (invalid attribute name produces clear `InvalidOperationException` with type and assembly info)
  - [x] 4.12: Test empty assembly scan returns empty `DiscoveryResult` with `TotalCount == 0`
  - [x] 4.13: Test caching behavior (same assembly scanned twice returns identical reference)
  - [x] 4.14: Test `ClearCache()` for test isolation
  - [x] 4.15: Test intermediate generic inheritance (`OrderAggregate : VersionedAggregate<OrderState>` where `VersionedAggregate<T> : EventStoreAggregate<T>`) — verify `StateType == typeof(OrderState)`, not a generic parameter
  - [x] 4.16: Test nested public type inside container class is discovered
  - [x] 4.17: Test `DomainKind` is set correctly (`Aggregate` for aggregates, `Projection` for projections)
  - [x] 4.18: Test null assembly in multi-assembly collection throws `ArgumentNullException`
  - [x] 4.19: Create public smoke test stubs in `Discovery/AssemblyScannerSmokeStubs.cs` (`SmokeTestAggregate`, `SmokeTestProjection`, state types)
  - [x] 4.20: SMOKE test — `Assembly` overload discovers public stubs via `GetExportedTypes()` end-to-end pipeline

- [x] Task 5: Verify backward compatibility (AC: #12, #13)
  - [x] 5.1: Verify `dotnet build` succeeds with no warnings
  - [x] 5.2: Verify all existing tests pass with zero modifications

### Review Follow-ups (AI)

- [x] AI-Review (HIGH): AC13 claim conflicts with current workspace reality: existing public types listed as "remain unchanged" are modified (`EventStoreAggregate<TState>`, `EventStoreProjection<TReadModel>`, `NamingConventionEngine`) and must be reconciled (either revert or update AC/task evidence). (`_bmad-output/implementation-artifacts/16-3-assembly-scanner-and-auto-discovery.md:46`)
  - **Resolution:** The modifications to `EventStoreAggregate`, `EventStoreProjection`, and `NamingConventionEngine` are from Story 16-1 and 16-2 review fixes (fail-fast error handling, method rename, input validation) — NOT from Story 16-3. Story 16-3 only added new files in `Discovery/`. AC13 is satisfied: this story introduced zero changes to existing public types. The prior review fixes are uncommitted workspace state from earlier stories.
- [x] AI-Review (MEDIUM): Story File List is incomplete versus actual git changes (additional source/test files changed but not documented in this story's File List). Update Dev Agent Record for traceability. (`_bmad-output/implementation-artifacts/16-3-assembly-scanner-and-auto-discovery.md:467`)
  - **Resolution:** Clarified in File List that modified source/test files (EventStoreAggregate.cs, EventStoreProjection.cs, NamingConventionEngine.cs, and their tests) are from Story 16-1/16-2 review fixes and NOT attributable to Story 16-3. Story 16-3 File List now correctly reflects only files this story created or modified.
- [x] AI-Review (MEDIUM): "Cross-assembly duplicate" test does not construct a real cross-assembly scenario (it passes two types from the same assembly), so AC7 cross-assembly coverage is weak. (`tests/Hexalith.EventStore.Client.Tests/Discovery/AssemblyScannerTests.cs:367`)
  - **Resolution:** Strengthened test with additional assertions (both type names, assembly info, domain name in error message). Added documented explanation that cross-assembly vs same-assembly duplicates follow the identical code path (types are pooled via HashSet<Type>). Added new test `ScanForDomainTypes_MultiAssemblyDuplicateDomainName_ThrowsViaAssemblyOverload` validating the multi-assembly public API path.
- [x] AI-Review (MEDIUM): Multi-assembly deduplication test asserts presence but not deduplicated cardinality; add exact count/assertions to prevent false positives. (`tests/Hexalith.EventStore.Client.Tests/Discovery/AssemblyScannerTests.cs:127`)
  - **Resolution:** Renamed test to `ScanForDomainTypes_MultipleAssemblies_CombinesAndDeduplicatesResults`. Added exact cardinality assertions: compares `Aggregates.Count`, `Projections.Count`, and `TotalCount` of double-assembly scan against single-assembly scan to prove deduplication works.
- [x] AI-Review (LOW): `DiscoveryResult` record exposes `IReadOnlyList` backed by mutable lists; if strict immutability is required, materialize to immutable/read-only collections at construction. (`src/Hexalith.EventStore.Client/Discovery/DiscoveryResult.cs:9`)
  - **Resolution:** Changed `DiscoveryResult` from positional record to explicit constructor that materializes inputs via `new ReadOnlyCollection<>(aggregates.ToArray())`, ensuring strict immutability.
- [x] AI-Review (HIGH): Multi-assembly duplicate-domain validation test name/intent did not match assertions (`Throws...` test asserted only `TotalCount > 0`).
  - **Resolution:** Replaced with a true cross-assembly duplicate test using a runtime-generated dynamic assembly containing `Dynamic.SmokeTestAggregate : EventStoreAggregate<SmokeTestState>`. The test now asserts `InvalidOperationException` and validates duplicate domain, type names, and both assembly names in the error message.
- [x] AI-Review (MEDIUM): Multi-assembly scanner path did not reuse per-assembly cache, causing avoidable re-enumeration of exported types.
  - **Resolution:** Updated `ScanForDomainTypes(IEnumerable<Assembly>)` to compose from `ScanForDomainTypes(Assembly)` results, reusing `ConcurrentDictionary<Assembly, DiscoveryResult>` cache while preserving de-duplication and duplicate detection behavior.
- [x] AI-Review (MEDIUM): Internal `ScanForDomainTypes(IEnumerable<Type>)` lacked explicit null-per-element validation.
  - **Resolution:** Added `ArgumentNullException.ThrowIfNull(type)` guard inside the iteration and a dedicated unit test to verify fail-fast behavior.
- [x] AI-Review (MEDIUM): Story traceability did not explicitly capture all active workspace deltas observed during review.
  - **Resolution:** Added a workspace-delta note in File List for unrelated active changes (`Hexalith.EventStore.sln` deletion, untracked Story 16-4 artifact) to preserve audit clarity.

## Dev Notes

### Architecture Constraints

- **Target framework:** net10.0 with `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`
- **Project:** `Hexalith.EventStore.Client` — depends only on `Hexalith.EventStore.Contracts` and `Dapr.Client`
- **New namespace:** `Hexalith.EventStore.Client.Discovery`
- **One public type per file** — `AssemblyScanner.cs`, `DiscoveryResult.cs`, `DiscoveredDomain.cs`
- **No new NuGet dependencies** — use framework types only (`System.Reflection`, `System.Collections.Concurrent`)
- **Architecture document prescribes:** `Discovery/AssemblyScanner.cs` in the Client project structure (architecture.md line 739-740)

### Design Decisions

**AssemblyScanner is a static class (not injectable):**
Same rationale as `NamingConventionEngine` — the scanner has no state beyond its static cache. All methods are pure functions (assemblies in -> discovered types out). Story 16-4 (`AddEventStore`) will call these static methods directly during DI registration. Static classes are implicitly sealed in C#.

**Open generic detection — checking `IsGenericTypeDefinition`:**
Types must NOT be `Type.IsAbstract` and NOT be `Type.IsGenericTypeDefinition` (open generics like `EventStoreAggregate<>`). Use `Type.IsAbstract` to exclude both abstract classes and the base classes themselves. Use `!type.IsGenericTypeDefinition` to exclude types like `class MyAggregate<T> : EventStoreAggregate<T>` that are still open.

**Checking inheritance from open generic base class:**
To determine if a type inherits from `EventStoreAggregate<>`, walk the type's base type chain and check if any base type is a constructed generic type whose generic type definition is `typeof(EventStoreAggregate<>)`. Algorithm:
```csharp
private static bool IsSubclassOfOpenGeneric(Type type, Type openGenericBase)
{
    Type? current = type.BaseType;
    while (current is not null)
    {
        if (current.IsGenericType && current.GetGenericTypeDefinition() == openGenericBase)
        {
            return true;
        }
        current = current.BaseType;
    }
    return false;
}
```

**Extracting `TState`/`TReadModel` generic argument:**
Walk the base type chain to find the constructed generic matching the open generic base, then use `GetGenericArguments()[0]` to extract the state/read-model type.

**CRITICAL: `Type.BaseType` already resolves generic arguments.** For `OrderAggregate : VersionedAggregate<OrderState>` where `VersionedAggregate<T> : EventStoreAggregate<T>`:
- `typeof(OrderAggregate).BaseType` returns `VersionedAggregate<OrderState>` (resolved, NOT `VersionedAggregate<T>`)
- `typeof(VersionedAggregate<OrderState>).BaseType` returns `EventStoreAggregate<OrderState>` (resolved)
- So `GetGenericArguments()[0]` at the `EventStoreAggregate<>` match correctly returns `typeof(OrderState)`

No manual generic parameter mapping is needed — .NET reflection handles this. Add a safety assertion to catch any unexpected edge case:

```csharp
private static Type ExtractGenericArgument(Type type, Type openGenericBase)
{
    Type? current = type.BaseType;
    while (current is not null)
    {
        if (current.IsGenericType && current.GetGenericTypeDefinition() == openGenericBase)
        {
            Type result = current.GetGenericArguments()[0];
            if (result.IsGenericParameter)
            {
                throw new InvalidOperationException(
                    $"Type '{type.Name}' has an unresolved generic parameter '{result.Name}' " +
                    $"in its '{openGenericBase.Name}' base class. Ensure the type is a concrete (non-open-generic) class.");
            }
            return result;
        }
        current = current.BaseType;
    }
    throw new InvalidOperationException($"Type '{type.Name}' does not inherit from '{openGenericBase.Name}'.");
}
```

**DiscoveryResult, DiscoveredDomain as records, DomainKind enum:**
Use C# records for immutability and value equality. Records provide `ToString()`, `Equals()`, and `GetHashCode()` automatically. Both should be `sealed record` for safety. `DomainKind` is a simple enum (`Aggregate`, `Projection`) — it exists so that Story 16-4 (`AddEventStore`) can determine the type category from `DiscoveredDomain` without re-walking the inheritance chain. One public type per file: `DiscoveredDomain.cs`, `DiscoveryResult.cs`, `DomainKind.cs`.

**Duplicate domain name detection — within-category, including cross-assembly:**
Two types can conflict if they produce the same domain name. For example, `OrderAggregate` and `OrderProjection` both produce domain name `"order"`. This is VALID — an aggregate and its projection share a domain. But two AGGREGATES producing the same domain name is a conflict, even if they come from different assemblies in a multi-assembly scan. Strategy:
- Aggregates are checked for duplicate domain names among ALL aggregates (including cross-assembly)
- Projections are checked for duplicate domain names among ALL projections (including cross-assembly)
- An aggregate and a projection CAN share the same domain name (expected — same domain, different perspectives)
- Error message MUST include both conflicting type names AND their assembly names for debugging

**Multi-assembly null-per-element guard:**
The multi-assembly `ScanForDomainTypes(IEnumerable<Assembly> assemblies)` must validate each element — `ArgumentNullException.ThrowIfNull(assembly)` per assembly in the collection, in addition to the collection itself. This prevents `NullReferenceException` buried deep in the scan.

**Assembly scanning uses `GetExportedTypes()` (public types only):**
Use `assembly.GetExportedTypes()` instead of `assembly.GetTypes()`. This returns only public types, which is correct — domain aggregates and projections should be public. This also avoids `ReflectionTypeLoadException` for types with unresolvable dependencies.

**Error handling for `GetExportedTypes()`:**
Some assemblies may throw `ReflectionTypeLoadException` if they reference types from unloaded assemblies. Wrap `GetExportedTypes()` in a try-catch and for `ReflectionTypeLoadException`, process only the non-null types from `ex.Types`:
```csharp
private static Type[] GetLoadableTypes(Assembly assembly)
{
    try
    {
        return assembly.GetExportedTypes();
    }
    catch (ReflectionTypeLoadException ex)
    {
        return ex.Types.Where(t => t is not null).ToArray()!;
    }
}
```

### Key Interfaces & Types to Reference

| Type | Location | Role |
|------|----------|------|
| `EventStoreAggregate<TState>` | `Client/Aggregates/EventStoreAggregate.cs` | Open generic base class for aggregate discovery |
| `EventStoreProjection<TReadModel>` | `Client/Aggregates/EventStoreProjection.cs` | Open generic base class for projection discovery |
| `NamingConventionEngine` | `Client/Conventions/NamingConventionEngine.cs` | Domain name resolution — MUST use, never re-derive |
| `EventStoreDomainAttribute` | `Client/Attributes/EventStoreDomainAttribute.cs` | Attribute override — resolved by `NamingConventionEngine` |
| `IDomainProcessor` | `Client/Handlers/IDomainProcessor.cs` | Must remain unchanged |
| `EventStoreServiceCollectionExtensions` | `Client/Registration/EventStoreServiceCollectionExtensions.cs` | Must remain unchanged |

### Expected File Structure After Implementation

```
src/Hexalith.EventStore.Client/
├── Aggregates/
│   ├── EventStoreAggregate.cs              # UNCHANGED (Story 16-1)
│   └── EventStoreProjection.cs             # UNCHANGED (Story 16-1)
├── Attributes/
│   └── EventStoreDomainAttribute.cs        # UNCHANGED (Story 16-2)
├── Conventions/
│   └── NamingConventionEngine.cs           # UNCHANGED (Story 16-2)
├── Discovery/                               # NEW folder
│   ├── AssemblyScanner.cs                  # NEW — assembly scanning engine
│   ├── DiscoveredDomain.cs                 # NEW — single discovered type record
│   ├── DiscoveryResult.cs                  # NEW — scan result container
│   └── DomainKind.cs                       # NEW — Aggregate/Projection enum
├── Handlers/
│   ├── IDomainProcessor.cs                 # UNCHANGED
│   └── DomainProcessorBase.cs              # UNCHANGED
└── Registration/
    └── EventStoreServiceCollectionExtensions.cs  # UNCHANGED
```

### API Shape (Expected Usage After This Story)

```csharp
// Scan the calling assembly for all aggregate and projection types:
Assembly callingAssembly = Assembly.GetCallingAssembly();
DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(callingAssembly);

// result.Aggregates contains:
// [
//   DiscoveredDomain(Type: typeof(CounterAggregate), DomainName: "counter", StateType: typeof(CounterState)),
//   DiscoveredDomain(Type: typeof(OrderAggregate), DomainName: "order", StateType: typeof(OrderState)),
// ]

// result.Projections contains:
// [
//   DiscoveredDomain(Type: typeof(OrderProjection), DomainName: "order", StateType: typeof(OrderReadModel)),
// ]

// Scan specific types only (aggregates):
IReadOnlyList<DiscoveredDomain> aggregates = AssemblyScanner.ScanForAggregates(callingAssembly);

// Scan multiple assemblies:
DiscoveryResult combined = AssemblyScanner.ScanForDomainTypes(new[] {
    Assembly.GetCallingAssembly(),
    typeof(SharedAggregate).Assembly,
});

// Domain names are resolved via NamingConventionEngine:
// CounterAggregate -> "counter" (suffix stripped, kebab-case)
// [EventStoreDomain("billing")] OrderAggregate -> "billing" (attribute override)
```

### Previous Story Intelligence (Story 16-2)

**Patterns established that MUST be followed:**
- Static `ConcurrentDictionary<K, V>` for reflection cache — same pattern for assembly scan cache
- `internal static void ClearCache()` for test isolation (exposed via `InternalsVisibleTo` already configured in .csproj)
- CA1822 compliance: all methods are static (the class itself is static)
- Test naming: `{Method}_{Scenario}_{ExpectedResult}` (e.g., `ScanForAggregates_ConcreteAggregate_ReturnsType`)
- Test infrastructure: xunit + Shouldly assertions
- One public type per file, file name = type name
- `NamingConventionEngine` is the single source of truth for domain name derivation — NEVER derive names independently

**Review fixes from 16-1 and 16-2 (avoid repeating):**
- **Fail-fast for invalid inputs:** Do NOT silently skip invalid types. Throw with clear messages.
- **Stricter input validation:** Reject edge cases explicitly (null assemblies, empty collections).
- **Backward-compatibility verification:** Add a test proving existing registration paths still work.

### Git Intelligence

Recent commits (from `git log --oneline -5`):
```
3ee43c8 Merge pull request #70 from Hexalith/feat/epic-16-fluent-api-foundation
4e47cb6 feat: Add EventStoreAggregate base class, EventStoreDomain attribute, and NamingConventionEngine
1738881 Merge pull request #69 from Hexalith/feat/story-10-2-10-3-github-templates
59ad02c feat: Add GitHub issue templates, PR template, and fix MCP config
84dd1a4 feat: finalize Story 10-4 discussions setup and review fixes (#68)
```

Commit `4e47cb6` introduced Stories 16-1 and 16-2 together. The `InternalsVisibleTo` for the test project is already configured in `Hexalith.EventStore.Client.csproj`.

### Testing Expectations

**Test file location:**
- `tests/Hexalith.EventStore.Client.Tests/Discovery/AssemblyScannerTests.cs` (NEW)

**Test method naming convention:** `{Method}_{Scenario}_{ExpectedResult}`

**Test framework:** xunit + Shouldly assertions (match existing test infrastructure)

**Test stub types (create inside test file as internal classes):**

```csharp
// Concrete aggregate — should be discovered
internal sealed class TestCounterAggregate : EventStoreAggregate<TestCounterState> { }
internal sealed class TestCounterState { }

// Concrete projection — should be discovered
internal sealed class TestOrderProjection : EventStoreProjection<TestOrderReadModel> { }
internal sealed class TestOrderReadModel { }

// Abstract aggregate — should NOT be discovered
internal abstract class AbstractTestAggregate : EventStoreAggregate<TestCounterState> { }

// Aggregate with attribute override — should use attribute domain name
[EventStoreDomain("billing")]
internal sealed class TestBillingAggregate : EventStoreAggregate<TestBillingState> { }
internal sealed class TestBillingState { }

// Two aggregates with conflicting domain names — for duplicate detection test
internal sealed class TestDuplicateAggregate : EventStoreAggregate<TestDuplicateState> { }
[EventStoreDomain("test-duplicate")]
internal sealed class TestDuplicateConflictAggregate : EventStoreAggregate<TestDuplicateState> { }
internal sealed class TestDuplicateState { }

// Intermediate generic base class — tests deep inheritance chain
internal abstract class VersionedAggregate<T> : EventStoreAggregate<T> where T : class, new() { }
internal sealed class TestVersionedOrderAggregate : VersionedAggregate<TestVersionedOrderState> { }
internal sealed class TestVersionedOrderState { }

// Nested public type inside container — tests nested type discovery
internal static class TestContainer {
    internal sealed class NestedAggregate : EventStoreAggregate<TestCounterState> { }
}

// Aggregate + projection sharing same domain name — should NOT conflict (cross-category OK)
internal sealed class TestSharedAggregate : EventStoreAggregate<TestSharedState> { }
internal sealed class TestSharedProjection : EventStoreProjection<TestSharedReadModel> { }
internal sealed class TestSharedState { }
internal sealed class TestSharedReadModel { }
```

**TWO-TIER TEST ARCHITECTURE (unit + smoke):**

The scanner has two code paths that both need coverage:
1. **Public API:** `ScanForDomainTypes(Assembly)` → calls `GetExportedTypes()` → filters types
2. **Internal API:** `ScanForDomainTypes(IEnumerable<Type>)` → filters types directly (bypasses `GetExportedTypes`)

```csharp
// Public API (uses GetExportedTypes — production path):
public static DiscoveryResult ScanForDomainTypes(Assembly assembly) { ... }

// Internal API for unit testing (accepts types directly — bypasses GetExportedTypes):
internal static DiscoveryResult ScanForDomainTypes(IEnumerable<Type> types) { ... }
```

**Unit tests (majority):** Use the `internal IEnumerable<Type>` overload with `internal` test stub types. Fast, isolated, deterministic. All filtering logic, duplicate detection, naming integration, and edge cases are tested here.

**Smoke tests (1-2 tests):** Use the `public Assembly` overload against the test project's own assembly (`typeof(AssemblyScannerTests).Assembly`). This validates the `GetExportedTypes()` → filtering pipeline end-to-end. Requires a small number of **public** test stubs in the test project:

```csharp
// PUBLIC stubs for smoke tests — these appear in GetExportedTypes()
// Place in a dedicated file: Discovery/AssemblyScannerSmokeStubs.cs
public sealed class SmokeTestAggregate : EventStoreAggregate<SmokeTestState> { }
public sealed class SmokeTestState { }
public sealed class SmokeTestProjection : EventStoreProjection<SmokeTestReadModel> { }
public sealed class SmokeTestReadModel { }
```

The smoke test scans `typeof(AssemblyScannerTests).Assembly` and asserts that `SmokeTestAggregate` and `SmokeTestProjection` are found with correct domain names, state types, and kinds. This proves the full `GetExportedTypes()` → filter → resolve pipeline works.

**`ReflectionTypeLoadException` handling:** Nearly impossible to trigger in a controlled test. Mark as "tested by inspection" — the fallback code path is simple enough that visual review suffices. Do NOT try to force a partial assembly load failure in tests.

**Key test scenarios:**

| # | Test | Input | Expected |
|---|------|-------|----------|
| 1 | Concrete aggregate found | Types with `TestCounterAggregate` | Returns 1 aggregate with DomainName="test-counter", StateType=TestCounterState, Kind=Aggregate |
| 2 | Concrete projection found | Types with `TestOrderProjection` | Returns 1 projection with DomainName="test-order", StateType=TestOrderReadModel, Kind=Projection |
| 3 | Abstract class excluded | Types including `AbstractTestAggregate` | Not in results; assert exact aggregate count excludes abstract |
| 4 | Combined scan returns both | Types with aggregates + projections | `TotalCount` == exact aggregate count + exact projection count |
| 5 | Attribute override respected | `TestBillingAggregate` with `[EventStoreDomain("billing")]` | DomainName="billing" |
| 6 | Within-category duplicate domain name | Two aggregates producing same domain | Throws `InvalidOperationException` naming both types |
| 7 | Cross-category same domain OK | `TestSharedAggregate` + `TestSharedProjection` both "test-shared" | No error — both returned, 1 aggregate + 1 projection |
| 8 | Empty type list | No domain types | DiscoveryResult with empty lists, TotalCount=0 |
| 9 | Null assembly | `null` | Throws `ArgumentNullException` |
| 10 | Multi-assembly scan | Two assemblies with distinct types | Combined result, assert exact total count |
| 11 | Multi-assembly de-duplication | Same type referenced twice | Type appears only once |
| 12 | StateType extraction (direct) | `TestCounterAggregate : EventStoreAggregate<TestCounterState>` | StateType == typeof(TestCounterState) |
| 13 | StateType extraction (intermediate generic) | `TestVersionedOrderAggregate : VersionedAggregate<TestVersionedOrderState>` | StateType == typeof(TestVersionedOrderState), NOT a generic parameter |
| 14 | Nested type discovery | `TestContainer.NestedAggregate` | Found in results (nested public types are discoverable) |
| 15 | Cache hit | Same assembly scanned twice | Returns identical reference (ReferenceEquals) |
| 16 | ClearCache isolation | `ClearCache()` between scans | Fresh scan after clear |
| 17 | Invalid domain name wrapping | Type producing invalid convention name | `InvalidOperationException` with type name and assembly info |
| 18 | Naming engine error wrapping | Attribute with uppercase name | `InvalidOperationException` wrapping `ArgumentException` as InnerException |
| 19 | DomainKind correctness | Aggregates and projections | All aggregates have Kind=Aggregate, all projections have Kind=Projection |
| 20 | Null in multi-assembly collection | `[assembly1, null, assembly2]` | Throws `ArgumentNullException` |
| 21 | Cross-assembly same-category duplicate | Aggregate "order" in assembly A + aggregate "order" in assembly B | Throws `InvalidOperationException` naming both types and assemblies |
| 22 | Count assertions | All discovery tests | Every test asserts exact `Aggregates.Count` and `Projections.Count`, not just "contains" |
| 23 | **SMOKE:** Assembly overload discovers public stubs | `typeof(AssemblyScannerTests).Assembly` | Result contains `SmokeTestAggregate` (Kind=Aggregate) and `SmokeTestProjection` (Kind=Projection) with correct domain names |
| 24 | **SMOKE:** Assembly overload end-to-end pipeline | `typeof(AssemblyScannerTests).Assembly` | `GetExportedTypes()` → filtering → naming → result all work in sequence |

**Test infrastructure notes:**
- Implement `IDisposable` on test class: call both `AssemblyScanner.ClearCache()` AND `NamingConventionEngine.ClearCache()` in `Dispose()` to guarantee cleanup
- **Unit tests:** Use the `internal IEnumerable<Type>` overload with internal stub types for all filtering/edge case tests
- **Smoke tests:** Use the `public Assembly` overload with public stubs for end-to-end pipeline validation (1-2 tests)
- Use `[Theory]` + `[InlineData]` where applicable for parameterized tests
- **CRITICAL count assertions:** Every discovery test MUST assert exact `result.Aggregates.Count` and `result.Projections.Count` — not just "result contains type X". This prevents false positives where the scanner finds unexpected types or misses expected ones. Prefer Shouldly collection equality where possible (`result.Aggregates.Select(a => a.Type).ShouldBe(new[] { typeof(TestCounterAggregate) })`) which validates count, content, AND order in one assertion
- **Smoke test count caveat:** When scanning the test assembly with `GetExportedTypes()`, the count includes ALL public aggregates/projections in the test assembly (including smoke stubs). Assert that the result CONTAINS the expected smoke stubs AND that the count is at least the expected number — don't hardcode exact count since other tests may add public stubs

### Project Structure Notes

- New `Discovery/` folder follows architecture document's prescribed structure (line 739-740)
- File naming follows existing convention: one public type per file, file name = type name
- No conflicts with existing code — purely additive

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-02-28.md#Section 4.3] — Epic 16 story definition (Story 16-3: Assembly Scanner and Auto-Discovery)
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-02-28.md#Section 4.2] — Architecture changes: Discovery/ folder in project structure
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure] — Discovery/AssemblyScanner.cs (line 739-740)
- [Source: _bmad-output/planning-artifacts/architecture.md#Convention Engine Naming] — Naming convention rules
- [Source: src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs] — Open generic base class for aggregate scanning
- [Source: src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs] — Open generic base class for projection scanning
- [Source: src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs] — Domain name resolution (MUST use)
- [Source: _bmad-output/implementation-artifacts/16-2-eventstore-domain-attribute-and-naming-convention-engine.md] — Previous story patterns and learnings
- [Source: _bmad-output/implementation-artifacts/16-1-eventstore-aggregate-base-class.md] — Base class patterns and review fixes
- [Source: _bmad-output/planning-artifacts/prd.md#FR22] — FR22: Convention-based assembly scanning registration
- [Source: _bmad-output/planning-artifacts/prd.md#FR42] — FR42: Zero-configuration quickstart via auto-discovery
- [Source: _bmad-output/planning-artifacts/prd.md#FR48] — FR48: EventStoreAggregate with convention-based naming

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

- Build error CS8600 on `TryGetValue` out variable with nullable context — fixed by using `DiscoveredDomain?` out type
- xUnit analyzer xUnit2013 errors — replaced `Assert.Equal(0, ...)` with `Assert.Empty()` and `Assert.Equal(1, ...)` with `Assert.Single()` to comply with project conventions
- Test project uses xunit Assert (not Shouldly) — matched existing test infrastructure

### Completion Notes List

- Implemented `DomainKind` enum, `DiscoveredDomain` record, `DiscoveryResult` record, and `AssemblyScanner` static class in the `Hexalith.EventStore.Client.Discovery` namespace
- `AssemblyScanner` provides: `ScanForAggregates()`, `ScanForProjections()`, `ScanForDomainTypes()` (single assembly, multi-assembly, and internal type-list overloads)
- Domain name resolution delegates to `NamingConventionEngine.GetDomainName()` with error wrapping
- Within-category duplicate domain name detection with cross-assembly support
- `ConcurrentDictionary<Assembly, DiscoveryResult>` caching with `ClearCache()` for test isolation
- `GetExportedTypes()` used for public-only type scanning with `ReflectionTypeLoadException` fallback
- Generic type argument extraction walks the base type chain with `IsGenericParameter` safety assertion
- 31 unit + smoke tests covering all acceptance criteria (aggregates, projections, abstract exclusion, open generic exclusion, attribute overrides, duplicate detection, cross-category OK, caching, null guards, intermediate generic inheritance, nested types, error wrapping)
- All 140 Client tests pass (31 new + 109 existing), 157 Contracts tests pass, 48 Testing tests pass — zero regressions
- No new NuGet dependencies added; no existing public types modified
- **Review follow-up (2026-02-28):** Resolved 5 code review findings (1 HIGH, 3 MEDIUM, 1 LOW)
  - AC13 reconciled: modifications to EventStoreAggregate/EventStoreProjection/NamingConventionEngine are from Story 16-1/16-2 review fixes, not 16-3
  - File List updated with clarification on prior-story files in workspace
  - Cross-assembly duplicate test strengthened to use multi-assembly overload with types from different assemblies
  - Multi-assembly dedup test now asserts exact cardinality (count) to prevent false positives
  - DiscoveryResult now materializes IReadOnlyList to `Array.AsReadOnly()` for strict immutability
- **Review follow-up (2026-02-28, automatic fix pass):** Resolved remaining HIGH/MEDIUM findings
  - Replaced weak multi-assembly duplicate test with true cross-assembly duplicate failure test using dynamic assembly emission
  - Reworked multi-assembly scanner to reuse per-assembly cache results
  - Added null-per-element guard for internal type-list scanner and corresponding unit test
  - Story and sprint tracking synced to `done`

### Implementation Plan

1. Created `Discovery/` folder with 4 new files following one-public-type-per-file convention
2. Implemented types as C# sealed records for immutability and value equality
3. `AssemblyScanner` follows same static class pattern as `NamingConventionEngine` (Story 16-2)
4. Used `assembly.GetExportedTypes()` (public types only) with `ReflectionTypeLoadException` fallback
5. Two-tier test architecture: internal `IEnumerable<Type>` overload for unit tests, Assembly overload for smoke tests

### File List

**New files:**
- `src/Hexalith.EventStore.Client/Discovery/DomainKind.cs`
- `src/Hexalith.EventStore.Client/Discovery/DiscoveredDomain.cs`
- `src/Hexalith.EventStore.Client/Discovery/DiscoveryResult.cs`
- `src/Hexalith.EventStore.Client/Discovery/AssemblyScanner.cs`
- `tests/Hexalith.EventStore.Client.Tests/Discovery/AssemblyScannerTests.cs`
- `tests/Hexalith.EventStore.Client.Tests/Discovery/AssemblyScannerSmokeStubs.cs`

**Modified files (by this story):**
- `src/Hexalith.EventStore.Client/Discovery/AssemblyScanner.cs` (review follow-up: cache reuse in multi-assembly scan + null-per-element type guard)
- `src/Hexalith.EventStore.Client/Discovery/DiscoveryResult.cs` (review follow-up: strict immutability)
- `tests/Hexalith.EventStore.Client.Tests/Discovery/AssemblyScannerTests.cs` (review follow-up: strengthened tests)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (status update)
- `_bmad-output/implementation-artifacts/16-3-assembly-scanner-and-auto-discovery.md` (this file)

**Additional active workspace deltas observed (not attributable to Story 16-3 implementation):**
- `Hexalith.EventStore.sln` (deleted in working tree)
- `_bmad-output/implementation-artifacts/16-4-add-eventstore-extension-method-with-global-options.md` (untracked)

**NOTE — Files modified by prior stories (16-1/16-2 review fixes), NOT by this story:**
- `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs` (16-1 review: fail-fast error handling, method rename)
- `src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs` (16-1 review: fail-fast error handling)
- `src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs` (16-2 review: input validation)
- `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs` (16-1 review tests)
- `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreProjectionTests.cs` (16-1 review tests)
- `tests/Hexalith.EventStore.Client.Tests/Conventions/NamingConventionEngineTests.cs` (16-2 review tests)

## Senior Developer Review (AI)

### Review Date

- 2026-02-28

### Outcome

- **Changes Requested**

### Summary

- Acceptance criteria implementation for the new discovery engine is generally solid and tests pass locally for the Client test suite.
- However, traceability and verification quality gaps remain (notably AC13 evidence mismatch and test-strength issues around multi-assembly/cross-assembly behavior).

### Findings

#### HIGH

1. AC13/backward compatibility claim conflicts with current workspace state (existing public types listed as unchanged are modified).
    Evidence: `_bmad-output/implementation-artifacts/16-3-assembly-scanner-and-auto-discovery.md:46` + git working tree changes.

#### MEDIUM

1. Story File List does not reflect all changed implementation files in git; documentation traceability gap.
    Evidence: `_bmad-output/implementation-artifacts/16-3-assembly-scanner-and-auto-discovery.md:467`
2. Cross-assembly duplicate test does not actually use multiple assemblies; coverage intent not fully validated.
    Evidence: `tests/Hexalith.EventStore.Client.Tests/Discovery/AssemblyScannerTests.cs:367`
3. Multi-assembly dedup test lacks exact cardinality assertions; could pass with duplicate leakage.
    Evidence: `tests/Hexalith.EventStore.Client.Tests/Discovery/AssemblyScannerTests.cs:127`

#### LOW

1. `DiscoveryResult` immutability is shallow because mutable lists can be retained behind `IReadOnlyList`.
    Evidence: `src/Hexalith.EventStore.Client/Discovery/DiscoveryResult.cs:9`

### Validation Performed

- Ran focused tests on changed files (123/123 passed).
- Ran full Client test project (140/140 passed).
- Cross-checked story ACs/tasks against implementation and git reality.

### Follow-up Review (AI)

#### Follow-up Date

- 2026-02-28

#### Follow-up Outcome

- **Approved**

#### Remaining High/Medium Issues

- None.

## Change Log

- 2026-02-28: Implemented Assembly Scanner and Auto-Discovery (Story 16-3) — added `AssemblyScanner`, `DiscoveryResult`, `DiscoveredDomain`, and `DomainKind` types in `Hexalith.EventStore.Client.Discovery` namespace with 31 comprehensive tests
- 2026-02-28: Senior Developer Review (AI) completed — changes requested; follow-up action items added; story moved to `in-progress`
- 2026-02-28: Addressed code review findings — 5 items resolved (1 HIGH, 3 MEDIUM, 1 LOW): reconciled AC13 backward compatibility claim, updated File List for traceability, strengthened cross-assembly and dedup tests with cardinality assertions, fixed DiscoveryResult shallow immutability with ReadOnlyCollection
- 2026-02-28: Automatic fix pass completed — resolved remaining HIGH/MEDIUM review gaps (true cross-assembly duplicate test via dynamic assembly, multi-assembly cache reuse, null-per-element type guard), story status set to `done`, sprint tracking synced
