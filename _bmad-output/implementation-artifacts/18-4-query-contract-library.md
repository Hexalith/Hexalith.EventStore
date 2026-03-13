# Story 18.4: Query Contract Library

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **domain service developer**,
I want a **query contract library defining mandatory query metadata fields as typed static members**,
So that **query routing is type-safe and consistent across all domain services**.

## Acceptance Criteria

1. **IQueryContract interface with static abstract members** — **Given** the Contracts NuGet package, **When** a developer defines a query class implementing `IQueryContract`, **Then** mandatory fields `Domain`, `QueryType`, and `ProjectionType` are enforced as `static abstract string` properties (FR57). TenantId remains a per-request field (not type-level metadata).
2. **Convention-based QueryType derivation** — **Given** a query class named `GetCounterStatusQuery`, **When** the `NamingConventionEngine` resolves the query type name, **Then** it strips the "Query" suffix, converts PascalCase to kebab-case → `"get-counter-status"`, and validates against the existing kebab-case regex `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$` (matching `GetDomainName()` convention)
3. **Attribute override for QueryType** — **Given** a query class annotated with `[EventStoreQueryType("custom-name")]`, **When** the `NamingConventionEngine` resolves the query type name, **Then** it returns the attribute value instead of the convention-derived name (following `EventStoreDomainAttribute` pattern)
4. **QueryContractMetadata record** — **Given** a resolved query contract, **When** metadata is extracted, **Then** a `QueryContractMetadata(string QueryType, string Domain, string ProjectionType)` record holds the resolved values (FR57)
5. **QueryContractResolver resolves IQueryContract types** — **Given** a type implementing `IQueryContract`, **When** `QueryContractResolver.Resolve<TQuery>()` is called, **Then** it reads static abstract members, validates all fields against kebab-case rules, and returns `QueryContractMetadata`. Results are cached per type (thread-safe `ConcurrentDictionary`, matching `NamingConventionEngine` cache pattern)
6. **Type-safe QueryActorIdHelper overload** — **Given** a query contract type `TQuery : IQueryContract`, **When** `QueryActorIdHelper.DeriveActorId<TQuery>(tenantId, entityId, payload)` is called, **Then** it delegates to the existing `DeriveActorId(TQuery.QueryType, tenantId, entityId, payload)` — providing compile-time safety for the QueryType segment (FR57: single source of truth for routing)
7. **ProjectionType enables finer-grained ETag lookup** — **Given** a query contract where `Domain != ProjectionType` (e.g., a "reporting" domain with "order-summary" projection type), **When** the ETag actor ID is derived, **Then** it uses `{ProjectionType}:{TenantId}` (not `{Domain}:{TenantId}`), enabling finer-grained invalidation than the current Domain == ProjectionType assumption (ADR-18.3c future direction)
8. **QueryType must not contain colons** — **Given** a query contract with a QueryType containing a colon (e.g., `"get:counter"`), **When** validated, **Then** it throws `ArgumentException` (colons are reserved as actor ID separators, matching Story 18-2 AC #13)
9. **IQueryContract in Contracts package (zero dependencies)** — **Given** the `Hexalith.EventStore.Contracts` NuGet package, **When** referenced, **Then** `IQueryContract` and `QueryContractMetadata` are available with zero infrastructure dependencies. Convention resolution and validation are in the Client package.
10. **All existing tests pass** — All Tier 1, Tier 2, and Tier 3 tests continue to pass with zero behavioral change

## Tasks / Subtasks

- [ ] Task 1: Create `IQueryContract` interface (AC: #1, #9)
  - [ ] 1.1 Create `src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs`
  - [ ] 1.2 Three `static abstract string` properties: `QueryType`, `Domain`, `ProjectionType`
  - [ ] 1.3 XML doc: QueryType = routing key (kebab-case, no colons), Domain = owning domain, ProjectionType = ETag scope
  - [ ] 1.4 Zero dependencies — pure interface contract

- [ ] Task 2: Create `QueryContractMetadata` record (AC: #4, #9)
  - [ ] 2.1 Create `src/Hexalith.EventStore.Contracts/Queries/QueryContractMetadata.cs`
  - [ ] 2.2 Record: `QueryContractMetadata(string QueryType, string Domain, string ProjectionType)`
  - [ ] 2.3 XML doc: immutable container for resolved query contract metadata

- [ ] Task 3: Create `EventStoreQueryTypeAttribute` (AC: #3)
  - [ ] 3.1 Create `src/Hexalith.EventStore.Contracts/Queries/EventStoreQueryTypeAttribute.cs`
  - [ ] 3.2 Constructor: `EventStoreQueryTypeAttribute(string queryType)` — validates non-null/whitespace, no colons
  - [ ] 3.3 Property: `string QueryType { get; }`
  - [ ] 3.4 `[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]`
  - [ ] 3.5 Follow exact `EventStoreDomainAttribute` constructor validation pattern (in Client package)

- [ ] Task 4: Extend NamingConventionEngine for query types (AC: #2, #3, #8)
  - [ ] 4.0 Refactor: extract existing private `ValidateDomainArgument` into `public static void ValidateKebabCase(string value, string parameterName)` — needed by `QueryContractResolver`. Update all internal callers (`GetStateStoreName`, `GetPubSubTopic`, `GetCommandEndpoint`, `GetProjectionChangedTopic`) to use the new public method. Keep `ValidateDomainArgument` as a private alias or remove it entirely.
  - [ ] 4.1 Modify `src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs`
  - [ ] 4.2 Add `GetQueryTypeName(Type type)` method — convention-based query type derivation:
    - Check for `EventStoreQueryTypeAttribute` → return attribute value if present
    - Strip "Query" suffix from type name (add to `_knownQuerySuffixes` array)
    - Convert PascalCase → kebab-case (reuse existing `_wordBoundaryRegex`)
    - Validate against existing `_domainNameRegex`
    - Validate no colons (AC #8)
  - [ ] 4.3 Add `GetQueryTypeName<T>()` generic overload
  - [ ] 4.4 Add separate `ConcurrentDictionary<Type, string>` cache for query types (don't mix with domain name cache)
  - [ ] 4.5 Update `ClearCache()` to also clear query type cache

- [ ] Task 5: Create `QueryContractResolver` (AC: #5, #7)
  - [ ] 5.1 Create `src/Hexalith.EventStore.Client/Queries/QueryContractResolver.cs`
  - [ ] 5.2 Static method: `QueryContractMetadata Resolve<TQuery>() where TQuery : IQueryContract`
    - Read `TQuery.QueryType`, `TQuery.Domain`, `TQuery.ProjectionType`
    - Validate all three against kebab-case regex (reuse `NamingConventionEngine` validation)
    - Validate QueryType has no colons
    - Return `new QueryContractMetadata(queryType, domain, projectionType)`
  - [ ] 5.3 `ConcurrentDictionary<Type, QueryContractMetadata>` cache (thread-safe)
  - [ ] 5.4 Static method: `string GetETagActorId<TQuery>(string tenantId) where TQuery : IQueryContract`
    - Returns `$"{TQuery.ProjectionType}:{tenantId}"` — uses ProjectionType, not Domain (AC #7)
    - Validates tenantId non-null/whitespace

- [ ] Task 6: Add generic overload to QueryActorIdHelper (AC: #6)
  - [ ] 6.1 Modify `src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs`
  - [ ] 6.2 Add `static string DeriveActorId<TQuery>(string tenantId, string? entityId, byte[] payload) where TQuery : IQueryContract`
    - Delegates to existing `DeriveActorId(TQuery.QueryType, tenantId, entityId, payload)`
  - [ ] 6.3 XML doc: type-safe overload using query contract metadata. Include warning: "This method reads TQuery.QueryType directly without format validation. Call QueryContractResolver.Resolve&lt;TQuery&gt;() at least once (e.g., during application startup) to validate contract metadata before using this method in hot paths."

- [ ] Task 7: Unit tests — Tier 1: IQueryContract and metadata (AC: #1, #4, #8, #9)
  - [ ] 7.1 Create `tests/Hexalith.EventStore.Contracts.Tests/Queries/IQueryContractTests.cs`
    - Define test query class implementing `IQueryContract` with valid metadata
    - Verify static abstract members are accessible
    - Verify QueryContractMetadata record equality/immutability
  - [ ] 7.2 Create `tests/Hexalith.EventStore.Contracts.Tests/Queries/EventStoreQueryTypeAttributeTests.cs`
    - Test valid construction
    - Test null/whitespace rejection
    - Test colon rejection in query type name

- [ ] Task 8: Unit tests — Tier 1: NamingConventionEngine query type derivation (AC: #2, #3, #8)
  - [ ] 8.1 Update `tests/Hexalith.EventStore.Client.Tests/Conventions/NamingConventionEngineTests.cs`
    - Test `GetQueryTypeName()` with standard name: `GetCounterStatusQuery` → `"get-counter-status"`
    - Test `GetQueryTypeName()` with no "Query" suffix: `OrderSummary` → `"order-summary"`
    - Test `GetQueryTypeName()` with attribute override: `[EventStoreQueryType("custom")]` → `"custom"`
    - Test colon rejection: attribute value with colon → `ArgumentException`
    - Test invalid characters: uppercase, special chars → `ArgumentException`
    - Test max length (64 chars)
    - Test cache behavior (second call returns same value)
    - Test `ClearCache()` also clears query type cache (call `GetQueryTypeName`, clear, call again — should re-resolve)
    - Test `GetQueryTypeName` vs `Resolve` independence: type `OrderListQuery` with `static string QueryType => "list-orders"` — `GetQueryTypeName` derives from type name → `"order-list"` (strips suffix, converts), while `Resolve` reads the static member → `"list-orders"`. They are independent paths and CAN return different values. This is by design.

- [ ] Task 9: Unit tests — Tier 1: QueryContractResolver (AC: #5, #7)
  - [ ] 9.1 Create `tests/Hexalith.EventStore.Client.Tests/Queries/QueryContractResolverTests.cs`
    - Test `Resolve<TQuery>()` with valid contract → correct metadata
    - Test `Resolve<TQuery>()` with mismatched Domain/ProjectionType → still valid (they CAN differ)
    - Test `GetETagActorId<TQuery>()` → `"{ProjectionType}:{tenantId}"`
    - Test cache hit (second call, same type)
    - Test validation: invalid kebab-case in static members → `ArgumentException`
    - Test empty-string contract: `Resolve<EmptyDomainQuery>()` where `Domain => ""` → `ArgumentException`
    - Test null-suppressed contract: `Resolve<NullQueryTypeQuery>()` where `QueryType => null!` → `ArgumentNullException`

- [ ] Task 10: Unit tests — Tier 1: QueryActorIdHelper generic overload (AC: #6)
  - [ ] 10.1 Update `tests/Hexalith.EventStore.Server.Tests/Queries/QueryActorIdHelperTests.cs`
    - Test `DeriveActorId<TestQuery>(tenantId, entityId, payload)` Tier 1 → `"{QueryType}:{tenantId}:{entityId}"`
    - Test `DeriveActorId<TestQuery>(tenantId, null, empty)` Tier 3 → `"{QueryType}:{tenantId}"`
    - Test `DeriveActorId<TestQuery>(tenantId, null, payload)` Tier 2 → `"{QueryType}:{tenantId}:{checksum}"`
    - Verify result matches non-generic overload with same QueryType string

- [ ] Task 11: Verify zero regression (AC: #10)
  - [ ] 11.1 All Tier 1 tests pass: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ && dotnet test tests/Hexalith.EventStore.Client.Tests/ && dotnet test tests/Hexalith.EventStore.Sample.Tests/ && dotnet test tests/Hexalith.EventStore.Testing.Tests/`
  - [ ] 11.2 All Tier 2 tests pass: `dotnet test tests/Hexalith.EventStore.Server.Tests/`
  - [ ] 11.3 Full solution build: `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings

## Architectural Decisions

**ADR-18.4a: IQueryContract Uses Static Abstract Interface Members (C# 14)**
- **Choice:** `IQueryContract` uses `static abstract string` properties for QueryType, Domain, ProjectionType
- **Rejected:** Instance properties on a base class — doesn't provide compile-time enforcement, allows runtime modification
- **Rejected:** Attributes only (no interface) — attributes are metadata-only, not constrained by the type system; easy to forget or misapply
- **Rejected:** Source generators — adds build complexity, harder to debug, overkill for 3 string properties
- **Trade-off:** Requires C# 11+ (static abstract members in interfaces). Project targets C# 14 / .NET 10, so this is not a constraint.
- **Rationale:** Static abstract members provide true compile-time enforcement — a type implementing `IQueryContract` MUST define all three properties. The compiler catches missing metadata, not runtime validation.

**ADR-18.4b: Contracts Package Contains Interface Only, Client Contains Resolution Logic**
- **Choice:** `IQueryContract` and `QueryContractMetadata` in Contracts (zero dependencies). `QueryContractResolver` and `NamingConventionEngine` extensions in Client.
- **Rejected:** All in Contracts — would add NamingConventionEngine logic to a package that's currently dependency-free
- **Rejected:** All in Client — would make `IQueryContract` unavailable without Client package reference
- **Trade-off:** Developers need both Contracts (for interface) and Client (for resolution). But both are already required for any EventStore integration.
- **Rationale:** Follows existing split: Contracts has `SubmitQueryRequest` (data types), Client has `NamingConventionEngine` (convention logic). The `EventStoreQueryTypeAttribute` goes in Contracts alongside `IQueryContract` since it's attribute metadata, not resolution logic.

**ADR-18.4c: ProjectionType as Separate Field (Not Derived from Domain)**
- **Choice:** `IQueryContract.ProjectionType` is an explicit static property, independent of `Domain`
- **Rejected:** Deriving ProjectionType from Domain automatically — prevents finer-grained ETag scoping
- **Trade-off:** Most queries will have Domain == ProjectionType. Developers must explicitly set both (no default derivation from one to the other). This is intentional — explicit is better than implicit for routing metadata.
- **Rationale:** ADR-18.3c foreshadowed this: "Story 18.4 adds explicit ProjectionType metadata to query contracts, enabling finer-grained mapping." A reporting domain might have multiple projection types (order-summary, order-detail), each with independent ETag scoping.

**ADR-18.4d: EventStoreQueryTypeAttribute in Contracts/Queries Namespace**
- **Choice:** `EventStoreQueryTypeAttribute` lives in `Hexalith.EventStore.Contracts/Queries/` alongside `IQueryContract` and `QueryContractMetadata` — same namespace, same folder
- **Rejected:** Separate `Contracts/Attributes/` folder — creating a new folder for a single file is overengineered
- **Rejected:** In Client package alongside `EventStoreDomainAttribute` — but query contracts are defined by domain developers who reference Contracts, not Client
- **Trade-off:** Contracts package gains one attribute class. `EventStoreDomainAttribute` is in Client (where it was originally placed for convention engine access) — this creates a minor inconsistency, but the rationale is sound.
- **Rationale:** The attribute is used ON query contract classes in domain code. Domain developers reference Contracts to get `IQueryContract`. Having the attribute in the same package and namespace means they don't need Client just for the attribute. Resolution logic (reading the attribute) is in Client's `NamingConventionEngine`.

**ADR-18.4e: No Pipeline Integration in This Story**
- **Choice:** This story creates the contract library and type-safe helpers. It does NOT modify `QueriesController`, `QueryRouter`, or `SubmitQuery` to use `IQueryContract`.
- **Rejected:** Full pipeline integration — too large for a single story, would touch controller, MediatR pipeline, router, and all tests
- **Trade-off:** Developers can use `QueryContractResolver` and `DeriveActorId<TQuery>()` for type-safe routing, but the existing string-based pipeline continues unchanged. Integration is a follow-up story.
- **Rationale:** The library itself is the deliverable (FR57). Pipeline integration is a consumer of the library, not part of the library itself. Ship the contract, iterate on integration.

## Pre-mortem Findings

**PM-1: NamingConventionEngine Extension vs. New Class**
- Extending `NamingConventionEngine` with `GetQueryTypeName()` keeps all convention logic in one place. Creating a separate `QueryConventionEngine` would scatter convention logic across classes. Extension is the right choice — the engine already handles domain names, adding query type names is a natural extension. The "Query" suffix stripping should use a separate array (`_knownQuerySuffixes`) from `_knownSuffixes` (which has "Aggregate", "Projection", "Processor").

**PM-2: QueryType Colon Validation Consistency**
- `SubmitQueryRequestValidator` already rejects colons in QueryType (added in Story 18-2). The `NamingConventionEngine.GetQueryTypeName()` and `QueryContractResolver.Resolve()` must also reject colons. This is defense-in-depth — the validator catches API input, the resolver catches developer misconfiguration.

**PM-3: Static Abstract Members and Test Doubles**
- `IQueryContract` uses `static abstract` members. Unit tests define test query classes implementing the interface. Each test class has its own static values. This is straightforward — no mocking needed for static abstract members; just define test types with the right values.

**PM-4: Cache Isolation Between Domain Names and Query Type Names**
- `NamingConventionEngine` currently has one `ConcurrentDictionary<Type, string> _cache` for domain names. Adding query type name resolution requires a SEPARATE cache (`_queryTypeCache`) because the same type could theoretically have both a domain name and a query type name resolved. Using the same cache would cause key collisions.

**PM-5: EventStoreQueryTypeAttribute Placement — Future Consistency**
- `EventStoreDomainAttribute` is in `Client/Attributes/`. `EventStoreQueryTypeAttribute` is placed in `Contracts/Queries/` (same namespace as `IQueryContract`). This creates a minor inconsistency between attribute locations. However, the rationale (ADR-18.4d) is sound — query contracts are defined in domain code that references Contracts, not Client. Placing the attribute alongside the interface it annotates is the simplest approach. A future refactoring could move `EventStoreDomainAttribute` to Contracts for consistency, but that's out of scope.

**PM-6: QueryContractResolver Thread Safety**
- The resolver uses `ConcurrentDictionary.GetOrAdd()` like `NamingConventionEngine`. The lambda inside `GetOrAdd` reads static abstract members (pure, thread-safe) and validates strings (pure, thread-safe). No locking needed.

**PM-7: ValidateKebabCase Must Be Public**
- `QueryContractResolver.Resolve()` needs to validate all three static members against kebab-case rules. The existing `ValidateDomainArgument` is **private** in `NamingConventionEngine`. Task 4.0 refactors it into a `public static void ValidateKebabCase(string value, string parameterName)` method. This is a clean refactoring — all existing callers (`GetStateStoreName`, `GetPubSubTopic`, etc.) switch to the new public method. Without this refactoring, the resolver would duplicate the regex validation logic.

**PM-8: NamingConventionEngine God-Class Risk**
- Adding `GetQueryTypeName()` with its own cache, suffix array, and validation creates two parallel resolution paths (domain + query type). To mitigate: (a) reuse existing regex instances (`_wordBoundaryRegex`, `_domainNameRegex`), (b) keep `_knownQuerySuffixes` separate from `_knownSuffixes`, (c) factor shared validation into `ValidateKebabCase()`. The engine remains cohesive because all convention logic lives in one place.

**PM-9: _knownQuerySuffixes Extensibility**
- Currently `["Query"]`. Future types like `"GetCounterStatusRequest"` or `"CounterSpecification"` won't be stripped. This is intentional — keep the suffix list minimal and explicit. The `[EventStoreQueryType]` attribute provides an escape hatch for non-standard names.

**PM-10: IQueryContract Doesn't Include TenantId as Static Member**
- FR57 lists TenantId as "mandatory." But TenantId is per-request (each API call specifies a tenant), not per-query-type. The `IQueryContract` enforces type-level metadata (Domain, QueryType, ProjectionType). TenantId remains a parameter in `SubmitQueryRequest`, `QueryActorIdHelper.DeriveActorId<TQuery>(tenantId, ...)`, etc. FR57's "mandatory" means it's always required in query requests — already enforced by `SubmitQueryRequestValidator`.

**PM-11: Colon Validation Across Three Layers**
- Colons are validated in three places: (1) `EventStoreQueryTypeAttribute` constructor, (2) `QueryContractResolver.Resolve()`, (3) `QueryActorIdHelper.DeriveActorId()` (existing). All check `Contains(':')`. This is defense-in-depth. Add a comment in each noting the shared constraint: "Colons reserved as actor ID separator — validated at attribute, resolver, and helper layers."

## Dev Notes

### Compile-Time vs Runtime Usage

The generic `DeriveActorId<TQuery>()` overload requires the concrete type at compile time. This is useful in **domain service code** where the developer knows their query type. The **server pipeline** (`QueriesController` → `QueryRouter`) receives `QueryType` as a string from HTTP requests and will always use the string-based overload. This is by design — the contract library provides type safety for domain developers, not for the server pipeline.

**Before (string-based — silent runtime failure):**
```csharp
// Developer typos "counter" as "conter" — compiles fine, fails silently at runtime
var request = new SubmitQueryRequest("acme", "conter", "counter-1", "get-counter-status");
// Query routes to non-existent projection actor → 404 at runtime, no compile-time warning
```

**After (contract-based — compile-time safety):**
```csharp
// Developer defines query contract — compiler enforces all 3 properties
public class GetCounterStatusQuery : IQueryContract
{
    public static string QueryType => "get-counter-status";
    public static string Domain => "counter";
    public static string ProjectionType => "counter";
}

// Type-safe routing — QueryType comes from the contract, not a string literal
string actorId = QueryActorIdHelper.DeriveActorId<GetCounterStatusQuery>(tenantId, entityId, payload);
// If the contract has a typo, QueryContractResolver.Resolve() catches it at startup
```

### IQueryContract Interface Design

```csharp
namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Defines mandatory query metadata as typed static members.
/// Implement this interface on query contract classes to get compile-time
/// safety for query routing metadata (FR57).
/// </summary>
public interface IQueryContract
{
    /// <summary>
    /// Gets the query type name used for actor ID routing (first segment).
    /// Must be kebab-case, no colons (reserved as actor ID separator).
    /// Example: "get-counter-status"
    /// </summary>
    static abstract string QueryType { get; }

    /// <summary>
    /// Gets the owning domain name (kebab-case).
    /// Example: "counter"
    /// </summary>
    static abstract string Domain { get; }

    /// <summary>
    /// Gets the projection type for ETag scope.
    /// Used to derive ETag actor ID: {ProjectionType}:{TenantId}.
    /// Often equals Domain, but can differ for cross-domain queries.
    /// Example: "counter"
    /// </summary>
    static abstract string ProjectionType { get; }
}
```

### QueryContractMetadata Record

```csharp
namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Immutable container for resolved query contract metadata.
/// Produced by QueryContractResolver from IQueryContract implementations.
/// </summary>
public record QueryContractMetadata(
    string QueryType,
    string Domain,
    string ProjectionType);
```

### EventStoreQueryTypeAttribute

```csharp
namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Overrides the convention-derived query type name for a query class.
/// When applied, NamingConventionEngine.GetQueryTypeName returns this
/// attribute's value instead of deriving from the type name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EventStoreQueryTypeAttribute : Attribute
{
    public EventStoreQueryTypeAttribute(string queryType)
    {
        ArgumentNullException.ThrowIfNull(queryType);
        if (string.IsNullOrWhiteSpace(queryType))
        {
            throw new ArgumentException(
                "Query type name cannot be empty or whitespace.", nameof(queryType));
        }

        if (queryType.Contains(':'))
        {
            throw new ArgumentException(
                "Query type name cannot contain colons (reserved as actor ID separator).",
                nameof(queryType));
        }

        QueryType = queryType;
    }

    public string QueryType { get; }
}
```

### NamingConventionEngine Extension

```csharp
// Add to existing NamingConventionEngine class:

private static readonly string[] _knownQuerySuffixes = ["Query"];
private static readonly ConcurrentDictionary<Type, string> _queryTypeCache = new();

public static string GetQueryTypeName(Type type)
{
    ArgumentNullException.ThrowIfNull(type);
    return _queryTypeCache.GetOrAdd(type, static t => ResolveQueryTypeName(t));
}

public static string GetQueryTypeName<T>() => GetQueryTypeName(typeof(T));

private static string ResolveQueryTypeName(Type type)
{
    EventStoreQueryTypeAttribute? attribute = type.GetCustomAttribute<EventStoreQueryTypeAttribute>();
    if (attribute is not null)
    {
        string attributeValue = attribute.QueryType;
        ValidateQueryTypeName(attributeValue, type);
        return attributeValue;
    }

    string typeName = type.Name;
    string stripped = StripQuerySuffix(typeName);

    if (stripped.Length == 0)
    {
        throw new ArgumentException(
            $"Type '{typeName}' produces an empty query type name after suffix stripping.",
            nameof(type));
    }

    string kebab = _wordBoundaryRegex.Replace(stripped, "-$1$2$3").ToLowerInvariant();
    ValidateQueryTypeName(kebab, type);
    return kebab;
}

private static string StripQuerySuffix(string typeName)
{
    foreach (string suffix in _knownQuerySuffixes)
    {
        if (typeName.EndsWith(suffix, StringComparison.Ordinal))
        {
            return typeName[..^suffix.Length];
        }
    }

    return typeName;
}

private static void ValidateQueryTypeName(string name, Type type)
{
    if (name.Length > 64)
    {
        throw new ArgumentException(
            $"Query type name derived from type '{type.Name}' exceeds 64 characters: '{name}'.");
    }

    if (!_domainNameRegex.IsMatch(name))
    {
        throw new ArgumentException(
            $"Query type name '{name}' derived from type '{type.Name}' is invalid. " +
            "Must match ^[a-z0-9]([a-z0-9-]*[a-z0-9])?$ (lowercase alphanumeric + hyphens).");
    }

    if (name.Contains(':'))
    {
        throw new ArgumentException(
            $"Query type name '{name}' from type '{type.Name}' cannot contain colons " +
            "(reserved as actor ID separator).");
    }
}
```

### QueryContractResolver

```csharp
namespace Hexalith.EventStore.Client.Queries;

using System.Collections.Concurrent;

using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Contracts.Queries;

public static class QueryContractResolver
{
    private static readonly ConcurrentDictionary<Type, QueryContractMetadata> _cache = new();

    public static QueryContractMetadata Resolve<TQuery>()
        where TQuery : IQueryContract
    {
        return _cache.GetOrAdd(typeof(TQuery), static _ =>
        {
            string queryType = TQuery.QueryType;
            string domain = TQuery.Domain;
            string projectionType = TQuery.ProjectionType;

            NamingConventionEngine.ValidateKebabCase(queryType, "QueryType");
            NamingConventionEngine.ValidateKebabCase(domain, "Domain");
            NamingConventionEngine.ValidateKebabCase(projectionType, "ProjectionType");

            if (queryType.Contains(':'))
            {
                throw new ArgumentException(
                    $"QueryType '{queryType}' cannot contain colons (reserved as actor ID separator).");
            }

            return new QueryContractMetadata(queryType, domain, projectionType);
        });
    }

    /// <summary>
    /// Gets the ETag actor ID for a query contract using ProjectionType (not Domain).
    /// Format: {ProjectionType}:{TenantId}
    /// </summary>
    public static string GetETagActorId<TQuery>(string tenantId)
        where TQuery : IQueryContract
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId cannot be empty or whitespace.", nameof(tenantId));
        }

        return $"{TQuery.ProjectionType}:{tenantId}";
    }
}
```

**Prerequisite:** Task 4.0 refactors `NamingConventionEngine.ValidateDomainArgument` → public `ValidateKebabCase()`. The resolver calls this public method — no duplicated validation logic.

### QueryActorIdHelper Generic Overload

```csharp
// Add to existing QueryActorIdHelper class:

/// <summary>
/// Type-safe overload using query contract metadata for compile-time QueryType safety.
/// </summary>
public static string DeriveActorId<TQuery>(
    string tenantId, string? entityId, byte[] payload)
    where TQuery : IQueryContract
{
    return DeriveActorId(TQuery.QueryType, tenantId, entityId, payload);
}
```

### Usage Guidance: GetQueryTypeName vs Resolve

Two distinct APIs exist for query metadata — they serve different purposes:

| API | Package | Input | Purpose |
|-----|---------|-------|---------|
| `NamingConventionEngine.GetQueryTypeName(Type)` | Client | Any type | Convention-based name derivation from type name (strips "Query" suffix → kebab-case). Does NOT read `IQueryContract` static members. |
| `QueryContractResolver.Resolve<TQuery>()` | Client | `IQueryContract` type | Reads and validates static abstract members from the interface. Returns cached `QueryContractMetadata`. |

**When to use which:**
- **Domain developers defining queries:** Implement `IQueryContract`, call `Resolve<TQuery>()` to validate
- **Convention engine / discovery:** `GetQueryTypeName()` for assembly scanning when the type may not implement `IQueryContract`
- **Type-safe routing:** `DeriveActorId<TQuery>()` (calls `TQuery.QueryType` directly — ensure `Resolve<TQuery>()` was called at least once for validation)

**They CAN return different values for the same type.** `GetQueryTypeName(typeof(MyQuery))` derives from the type NAME. `Resolve<MyQuery>().QueryType` reads the static MEMBER. If the developer sets `QueryType => "custom-name"`, the convention engine won't know about it unless `[EventStoreQueryType("custom-name")]` is also applied. This is by design — the attribute bridges the two paths.

### Example Query Contract (for tests and sample reference)

```csharp
// Test helper / sample pattern:
public class GetCounterStatusQuery : IQueryContract
{
    public static string QueryType => "get-counter-status";
    public static string Domain => "counter";
    public static string ProjectionType => "counter";
}

// Cross-domain example (Domain != ProjectionType):
public class GetOrderSummaryQuery : IQueryContract
{
    public static string QueryType => "get-order-summary";
    public static string Domain => "reporting";
    public static string ProjectionType => "order-summary";
}
```

### Project Structure Notes

```text
src/Hexalith.EventStore.Contracts/Queries/
    IQueryContract.cs                              # NEW ← Task 1
    QueryContractMetadata.cs                       # NEW ← Task 2
    EventStoreQueryTypeAttribute.cs                # NEW ← Task 3
    SubmitQueryRequest.cs                          # UNCHANGED
    SubmitQueryResponse.cs                         # UNCHANGED

src/Hexalith.EventStore.Client/Conventions/
    NamingConventionEngine.cs                      # MODIFIED ← Task 4

src/Hexalith.EventStore.Client/Queries/
    QueryContractResolver.cs                       # NEW ← Task 5

src/Hexalith.EventStore.Server/Queries/
    QueryActorIdHelper.cs                          # MODIFIED ← Task 6
    QueryRouter.cs                                 # UNCHANGED
    IQueryRouter.cs                                # UNCHANGED
```

### Files to Create (4)

```text
src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs
src/Hexalith.EventStore.Contracts/Queries/QueryContractMetadata.cs
src/Hexalith.EventStore.Contracts/Queries/EventStoreQueryTypeAttribute.cs
src/Hexalith.EventStore.Client/Queries/QueryContractResolver.cs
```

### Files to Modify — Production (2)

```text
src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs    (refactor ValidateDomainArgument → public ValidateKebabCase; add GetQueryTypeName, query suffix stripping, query type cache)
src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs            (add generic DeriveActorId<TQuery> overload)
```

### Files to Create — Tests (3)

```text
tests/Hexalith.EventStore.Contracts.Tests/Queries/IQueryContractTests.cs
tests/Hexalith.EventStore.Contracts.Tests/Queries/EventStoreQueryTypeAttributeTests.cs
tests/Hexalith.EventStore.Client.Tests/Queries/QueryContractResolverTests.cs
```

### Files to Modify — Tests (2)

```text
tests/Hexalith.EventStore.Client.Tests/Conventions/NamingConventionEngineTests.cs  (add GetQueryTypeName tests)
tests/Hexalith.EventStore.Server.Tests/Queries/QueryActorIdHelperTests.cs          (add generic overload tests)
```

### Files NOT to Modify

- `QueriesController.cs` — no pipeline integration in this story (ADR-18.4e)
- `QueryRouter.cs` — routing logic unchanged
- `SubmitQuery.cs` — MediatR request unchanged
- `SubmitQueryRequest.cs` — API contract unchanged
- `QueryEnvelope.cs` — envelope unchanged
- `QueryResult.cs` — result type unchanged
- `IProjectionActor.cs` — actor interface unchanged
- `ETagActor.cs` — ETag actor unchanged
- `CachingProjectionActor.cs` — caching actor unchanged (from Story 18-3)
- `DaprProjectionChangeNotifier.cs` — notification path unchanged

### Build Verification Checkpoints

After each major task group, verify the build to catch errors early:
- After Tasks 1-3: `dotnet build src/Hexalith.EventStore.Contracts/`
- After Task 4: `dotnet build src/Hexalith.EventStore.Client/`
- After Task 5: `dotnet build src/Hexalith.EventStore.Client/`
- After Task 6: `dotnet build src/Hexalith.EventStore.Server/`
- After Tasks 7-10: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ && dotnet test tests/Hexalith.EventStore.Client.Tests/ && dotnet test tests/Hexalith.EventStore.Server.Tests/`
- Task 11: Full solution build + all Tier 1/2 tests

### Architecture Compliance

- **File-scoped namespaces, Allman braces, 4-space indent** per `.editorconfig`
- **TreatWarningsAsErrors = true** — zero warnings allowed
- **Nullable enabled** — no nullable concerns in this story (all properties are required `string`)
- **Contracts package: zero dependencies** — `IQueryContract`, `QueryContractMetadata`, `EventStoreQueryTypeAttribute` have no NuGet references
- **Convention engine pattern** — follow existing `GetDomainName()` → cache → resolve → validate flow
- **Thread-safe caching** — `ConcurrentDictionary.GetOrAdd()` with pure lambdas
- **Colon validation** — consistent with Story 18-2 AC #13 for actor ID separator safety
- **Kebab-case validation** — reuse existing `_domainNameRegex` pattern

### Previous Story Intelligence

**From Story 18-1 (done — ETag Actor & Projection Change Notification):**
- ETag actor ID format: `{ProjectionType}:{TenantId}` — this story adds `IQueryContract.ProjectionType` as the explicit source for the `ProjectionType` segment, replacing the implicit Domain == ProjectionType assumption
- `NamingConventionEngine.GetProjectionChangedTopic()` already uses `projectionType` as a parameter — consistent with `IQueryContract.ProjectionType`

**From Story 18-2 (review — 3-Tier Query Actor Routing):**
- `QueryActorIdHelper.DeriveActorId(queryType, tenantId, entityId, payload)` — this story adds a generic overload `DeriveActorId<TQuery>(tenantId, entityId, payload)` that reads QueryType from the type's static abstract member
- QueryType colon prohibition already enforced in `SubmitQueryRequestValidator` — this story adds the same validation in convention engine and resolver (defense-in-depth)
- `QueryActorIdHelper` validates no colons in routing segments — the generic overload inherits this validation automatically

**From Story 18-3 (ready-for-dev — Query Endpoint with ETag Pre-Check & Cache):**
- ADR-18.3c explicitly states: "Story 18.4 (Query Contract Library) adds explicit ProjectionType metadata to query contracts, enabling finer-grained mapping. For now, Domain = ProjectionType is sufficient."
- The `DaprETagService` uses `Domain` as ProjectionType for ETag lookup — future integration story can use `IQueryContract.ProjectionType` instead

**From Story 16-2 (done — NamingConventionEngine):**
- `NamingConventionEngine` pattern: `GetDomainName(Type)` → `_cache.GetOrAdd()` → `ResolveDomainName()` → attribute check → suffix strip → PascalCase-to-kebab → validate
- Known suffixes: `["Aggregate", "Projection", "Processor"]` — query types use separate suffix list `["Query"]`
- Regex: `_wordBoundaryRegex` for PascalCase splitting, `_domainNameRegex` for validation — both reused for query types
- Cache clearing: `ClearCache()` internal method for test isolation — must also clear query type cache

**From Story 16-8 (done — Unit tests for convention engine):**
- Test patterns: `GetDomainName_WithAttribute_ReturnsAttributeValue`, `GetDomainName_StripsSuffix_And_ConvertsToPascalCase`, etc. — follow same naming convention for `GetQueryTypeName` tests
- Edge cases: empty after suffix stripping, >64 char names, invalid characters — all apply to query types

### Git Intelligence

Recent commits:
```
a7fe357 Update sprint status to reflect completed epics and adjust generated dates
648a9db Add Implementation Readiness Assessment Report for Hexalith.EventStore
8c97752 Add integration tests for actor-based authorization and service unavailability
d8fcbc0 Add unit tests for SubmitQuery, QueryRouter, and validation logic
```

Working tree has uncommitted changes from Stories 18-1, 18-2, 18-3 (story files + code). Story 18-4 creates new files in Contracts, Client, and Server — no file conflicts with in-progress stories.

### Scope Boundary

**IN scope:**
- `IQueryContract` interface with static abstract members (FR57)
- `QueryContractMetadata` record
- `EventStoreQueryTypeAttribute` for query type name override
- `NamingConventionEngine.GetQueryTypeName()` convention-based derivation
- `QueryContractResolver` for type-safe metadata resolution
- `QueryActorIdHelper.DeriveActorId<TQuery>()` generic overload
- Tier 1 unit tests for all new types

**OUT of scope:**
- Pipeline integration (modifying `QueriesController`, `QueryRouter`, `SubmitQuery` to use contracts) — follow-up story
- Startup validation of all discovered `IQueryContract` types via `Resolve()` during `UseEventStore()` — follow-up integration story should add this for fail-fast validation
- Modifying `SubmitQueryRequest` to carry `ProjectionType` — follow-up
- Typed query request builders (e.g., `QueryRequestBuilder<TQuery>`) — follow-up
- `IQueryContract<TProjection>` convenience interface where `ProjectionType` auto-derives from the projection type's domain name (reduces boilerplate when Domain == ProjectionType, which is the 95% case) — follow-up enhancement
- Sample application query contract implementation — follow-up (Story 18-6 may include this)
- `IQueryContract` discovery via `AssemblyScanner` — follow-up
- SignalR group derivation from `IQueryContract.ProjectionType` — Story 18-5

### References

- [Source: prd.md line 815 — FR57: Query contract library (NuGet) with typed static members]
- [Source: epics.md lines 1383-1403 — Story 9.4: Query Contract Library]
- [Source: 18-2-3-tier-query-actor-routing.md — QueryActorIdHelper, 3-tier routing, colon prohibition]
- [Source: 18-3-query-endpoint-with-etag-pre-check-and-cache.md — ADR-18.3c: Domain = ProjectionType for now, Story 18.4 enables finer-grained mapping]
- [Source: src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs — Convention engine pattern, PascalCase-to-kebab, cache, validation]
- [Source: src/Hexalith.EventStore.Client/Attributes/EventStoreDomainAttribute.cs — Attribute override pattern for convention engine]
- [Source: src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs — Current string-based query request contract]
- [Source: src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs — Current DeriveActorId with raw strings]
- [Source: src/Hexalith.EventStore.Server/Actors/IProjectionActor.cs — Actor interface with 3-tier routing docs]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
