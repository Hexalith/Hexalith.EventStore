# Story 16.2: EventStoreDomain Attribute and Naming Convention Engine

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a domain service developer,
I want domain names to be automatically derived from my aggregate/projection class names using kebab-case conventions, with an optional `[EventStoreDomain("custom")]` attribute override,
so that I can register domain services without manually specifying DAPR resource names for state stores, pub/sub topics, and command endpoints.

## Acceptance Criteria

1. **AC1 — NamingConventionEngine exists:** A static `NamingConventionEngine` class exists in `Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs` that derives a kebab-case domain name from a .NET type name. This is the single source of truth for domain name derivation -- all downstream stories (16-3 through 16-10) must use this engine, never derive names independently.

2. **AC2 — Suffix stripping:** The engine strips the FIRST matching suffix from the END of the type name, checking in order: `Aggregate`, `Projection`, `Processor`. If no known suffix is found, the full type name is used. If suffix stripping produces an empty string (e.g., class named `Aggregate`), throw `ArgumentException`. Examples: `OrderAggregate` -> `order`, `UserManagementProjection` -> `user-management`, `PaymentProcessor` -> `payment`, `OrderHandler` -> `order-handler`.

3. **AC3 — PascalCase to kebab-case with acronym support:** Multi-word PascalCase names are split on word boundaries and joined with hyphens in lowercase. Consecutive uppercase letters (acronyms) are kept together as a single word. Word boundary rules include lowercase-to-uppercase (`userManagement` -> `user-management`), uppercase-to-uppercase+lowercase for acronym endings (`HTTPClient` -> `http-client`), letter-to-digit (`Order2` -> `order-2`, `V2Order` -> `v2-order`), and digit-to-uppercase (`Order2Checkout` -> `order-2-checkout`). Digit-to-lowercase transition itself does not create a boundary (`2x` stays together), but letter-to-digit still does (`order2x` -> `order-2x`).

4. **AC4 — EventStoreDomainAttribute exists:** A sealed `EventStoreDomainAttribute` class exists in `Hexalith.EventStore.Client/Attributes/EventStoreDomainAttribute.cs` with `[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]`. Constructor takes a `string domainName` and throws `ArgumentException` if empty/whitespace. Note: `Inherited = false` means derived classes do NOT inherit the parent's attribute -- they get convention-derived names unless they declare their own attribute.

5. **AC5 — Attribute override:** When `[EventStoreDomain("custom-name")]` is present on a type, `NamingConventionEngine` returns the attribute value instead of the derived name. Two-phase validation: the attribute constructor validates non-empty/non-whitespace; the engine validates kebab-case format compliance.

6. **AC6 — Domain name validation:** All derived and attribute-supplied domain names are validated against the existing `AggregateIdentity.Domain` regex pattern: `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$` (lowercase alphanumeric + hyphens, no leading/trailing hyphens, max 64 chars). Invalid names throw `ArgumentException` with a clear message naming the type and the invalid value.

7. **AC7 — Resource name derivation methods:** `NamingConventionEngine` provides static methods to derive DAPR resource names from a domain name. These are Layer 1 convention defaults -- Story 16-6 will provide override mechanisms above this layer:
   - `GetStateStoreName(string domain)` -> `"{domain}-eventstore"` (e.g., `"order-eventstore"`)
   - `GetPubSubTopic(string tenantId, string domain)` -> `"{tenantId}.{domain}.events"` (e.g., `"acme.order.events"`)
   - `GetCommandEndpoint(string domain)` -> `"{domain}-commands"` (e.g., `"order-commands"`)

8. **AC8 — Thread-safe caching:** Domain name resolution results are cached in a static `ConcurrentDictionary<Type, string>` so that repeated lookups for the same type are O(1) after first call. An `internal static void ClearCache()` method is provided for test isolation (exposed via `InternalsVisibleTo`).

9. **AC9 — Type-agnostic API with null guard:** `GetDomainName(Type type)` accepts ANY type -- it does not check for `EventStoreAggregate`/`EventStoreProjection` base classes. Type filtering is the assembly scanner's responsibility (Story 16-3). A generic convenience method `GetDomainName<T>()` is also provided. Null input throws `ArgumentNullException` (`ArgumentNullException.ThrowIfNull(type)` as first line).

10. **AC10 — Zero new NuGet dependencies:** Implementation uses only `System.Text.RegularExpressions` (framework-implicit) and existing Contracts types. No new package references added to `Hexalith.EventStore.Client.csproj`.

11. **AC11 — Backward compatibility:** No existing public types are modified. `IDomainProcessor`, `DomainProcessorBase<TState>`, `EventStoreAggregate<TState>`, `EventStoreProjection<TReadModel>`, and `AddEventStoreClient<TProcessor>()` remain unchanged.

12. **AC12 — Scope boundary:** `EventStoreDomainOptions` is NOT in scope for this story. Per-domain configuration is Story 16-6. This story delivers ONLY the convention engine (Layer 1) and the attribute override mechanism.

## Tasks / Subtasks

- [x] Task 1: Create `EventStoreDomainAttribute` (AC: #4, #5)
  - [x] 1.1: Create `src/Hexalith.EventStore.Client/Attributes/` folder
  - [x] 1.2: Implement `EventStoreDomainAttribute` sealed class with `DomainName` property and constructor validation (non-empty, non-whitespace)
- [x] Task 2: Create `NamingConventionEngine` (AC: #1, #2, #3, #5, #6, #7, #8, #9)
  - [x] 2.1: Create `src/Hexalith.EventStore.Client/Conventions/` folder
  - [x] 2.2: Implement `GetDomainName(Type type)` and `GetDomainName<T>()` with suffix stripping + PascalCase-to-kebab-case + acronym handling
  - [x] 2.3: Implement attribute override detection (`type.GetCustomAttribute<EventStoreDomainAttribute>()`)
  - [x] 2.4: Implement domain name validation using the same regex pattern as `AggregateIdentity` (kebab-case, max 64 chars)
  - [x] 2.5: Implement `ConcurrentDictionary<Type, string>` caching with `internal static void ClearCache()` for test isolation
  - [x] 2.6: Implement `GetStateStoreName(string domain)`, `GetPubSubTopic(string tenantId, string domain)`, `GetCommandEndpoint(string domain)` resource derivation methods
- [x] Task 3: Add `InternalsVisibleTo` for test project (AC: #8)
  - [x] 3.1: Add `<InternalsVisibleTo Include="Hexalith.EventStore.Client.Tests" />` to `Hexalith.EventStore.Client.csproj` (this is new -- Story 16-1 did not need it)
- [x] Task 4: Create unit tests (AC: all)
  - [x] 4.1: Create `tests/Hexalith.EventStore.Client.Tests/Attributes/EventStoreDomainAttributeTests.cs`
  - [x] 4.2: Create `tests/Hexalith.EventStore.Client.Tests/Conventions/NamingConventionEngineTests.cs`
  - [x] 4.3: Test suffix stripping for all 3 suffixes (Aggregate, Projection, Processor) and compound suffixes (`AggregateProjection`)
  - [x] 4.4: Test PascalCase to kebab-case conversion: single word, multi-word, acronyms at start/middle/end, digit boundaries
  - [x] 4.5: Test attribute override path and `Inherited = false` behavior (derived class without attribute gets convention name)
  - [x] 4.6: Test validation rejects invalid names (uppercase attribute, leading/trailing hyphens, empty after suffix strip, >64 chars)
  - [x] 4.7: Test resource name derivation methods
  - [x] 4.8: Test caching behavior (call `ClearCache()` between tests to ensure isolation)
  - [x] 4.9: Test `GetDomainName<T>()` generic convenience method
- [x] Task 5: Verify backward compatibility (AC: #10, #11)
  - [x] 5.1: Verify `dotnet build` succeeds with no warnings
  - [x] 5.2: Verify all existing tests pass with zero modifications

## Dev Notes

### Architecture Constraints

- **Target framework:** net10.0 with `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`
- **Project:** `Hexalith.EventStore.Client` -- depends only on `Hexalith.EventStore.Contracts` and `Dapr.Client`
- **New namespaces:** `Hexalith.EventStore.Client.Attributes`, `Hexalith.EventStore.Client.Conventions`
- **One public type per file** -- `EventStoreDomainAttribute.cs` and `NamingConventionEngine.cs`
- **No new NuGet dependencies** -- use framework types only (`System.Text.RegularExpressions`, `System.Collections.Concurrent`)
- **Enforcement Rule #17 (Architecture):** Convention-derived resource names use kebab-case; type suffix stripping is automatic; attribute overrides validated at startup for non-empty, kebab-case compliance

### Design Decisions

**NamingConventionEngine is a static class (not injectable):**
The engine has no state beyond its static cache. All methods are pure functions (input type -> output string). No need for DI, inheritance, or instance lifecycle. Story 16-3 (AssemblyScanner) and 16-4 (AddEventStore) will call these static methods directly. Mocking is unnecessary -- test the real thing. Static classes are implicitly sealed in C#.

**GetDomainName accepts ANY type (type-agnostic):**
The engine does not check whether the input type inherits from `EventStoreAggregate` or `EventStoreProjection`. It purely transforms a type name into a kebab-case domain name. Type filtering (deciding WHICH types to pass in) is the assembly scanner's responsibility (Story 16-3). This separation keeps the engine reusable and prevents circular coupling.

**Suffix stripping -- first match from end, hardcoded list:**
Strip the FIRST matching suffix from the END of the type name, checking in order: `Aggregate`, `Projection`, `Processor`. These map 1:1 to the three framework base class types. If no suffix matches, the full type name is converted. If suffix stripping produces an empty string (e.g., class named `Aggregate`), throw `ArgumentException`. For compound names like `AggregateProjection`, `Projection` is stripped first (it matches the end) producing `Aggregate` which then converts to `"aggregate"`.

**PascalCase-to-kebab-case algorithm -- canonical truth table:**

Use `Regex.Replace` with pattern `(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])|(?<=[a-zA-Z])([0-9])` to handle all boundary types:

| Input (after suffix strip) | Output | Boundary Rule |
|---------------------------|--------|---------------|
| `Order` | `order` | Single word, lowercase only |
| `UserManagement` | `user-management` | lowercase-to-uppercase boundary |
| `OrderItem` | `order-item` | lowercase-to-uppercase boundary |
| `ShoppingCartCheckout` | `shopping-cart-checkout` | Multiple boundaries |
| `HTTPClient` | `http-client` | Acronym: uppercase-to-uppercase+lowercase |
| `IOStream` | `io-stream` | 2-char acronym at start |
| `AWSLambda` | `aws-lambda` | 3-char acronym at start |
| `MyHTTPClient` | `my-http-client` | Acronym in middle |
| `Order2` | `order-2` | Letter-to-digit boundary |
| `V2Order` | `v-2-order` | Digit surrounded by letters |
| `Order2Checkout` | `order-2-checkout` | Digit-to-uppercase boundary |
| `order2x` | `order-2x` | `r→2` letter-to-digit boundary applies; `2→x` keeps together |
| `A` | `a` | Single character (valid) |
| `IO` | `io` | All-uppercase 2-char (no internal boundary) |

**CRITICAL: The regex pattern `(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])|(?<=[a-zA-Z])([0-9])` handles three boundary types:**
1. `(?<=[a-z0-9])([A-Z])` -- lowercase/digit followed by uppercase (standard word boundary)
2. `(?<=[A-Z])([A-Z][a-z])` -- uppercase followed by uppercase+lowercase (end of acronym)
3. `(?<=[a-zA-Z])([0-9])` -- letter followed by digit (number boundary)

Each match inserts a hyphen before the captured group. The replacement string is `-$1$2$3` (hyphen + whichever group matched). Then `.ToLowerInvariant()` the entire result. Be precise about `Regex.Replace` semantics -- the hyphen goes BEFORE the captured character(s), not after:
```csharp
// Correct implementation pattern:
private static readonly Regex _wordBoundaryRegex = new(
    @"(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])|(?<=[a-zA-Z])([0-9])",
    RegexOptions.Compiled);

string kebab = _wordBoundaryRegex.Replace(stripped, "-$1$2$3").ToLowerInvariant();
```

**Two-phase attribute validation:**
- Phase 1 (attribute constructor): Validates non-empty, non-whitespace. This catches obvious errors at construction time.
- Phase 2 (NamingConventionEngine.GetDomainName): Validates the attribute value against kebab-case regex. An attribute like `[EventStoreDomain("BILLING")]` compiles successfully but throws `ArgumentException` at runtime when the engine validates it. This is acceptable -- startup-time failure with a clear message.

**Inherited = false on EventStoreDomainAttribute:**
Derived classes do NOT inherit the parent's `[EventStoreDomain]` attribute. If `BaseAggregate` has `[EventStoreDomain("base")]` and `DerivedAggregate : BaseAggregate` has no attribute, `DerivedAggregate` gets its convention-derived name (not "base"). This is intentional -- each aggregate has its own domain identity.

**Domain name validation reuses AggregateIdentity pattern:**
The validation regex `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$` is the SAME pattern used in `AggregateIdentity.ValidateTenantOrDomain()` (line 13). Since `AggregateIdentity` is in the Contracts package and the validation method is private, use the same regex pattern in `NamingConventionEngine`. The max length constraint (64 chars) must also be enforced.

**Caching strategy with test isolation:**
Story 16-1 established the `ConcurrentDictionary<Type, AggregateMetadata>` pattern. This story uses `ConcurrentDictionary<Type, string>` for domain name resolution. Thread-safe for concurrent first invocations during app startup. An `internal static void ClearCache()` method is provided so tests can reset state between test methods via `InternalsVisibleTo`.

**Resource name derivation -- Layer 1 convention defaults:**
The naming patterns from the architecture document (Section: "Convention Engine Naming") are canonical:
- State store: `{domain}-eventstore`
- Pub/sub topic: `{tenantId}.{domain}.events` (D6 pattern)
- Command endpoint: `{domain}-commands`

These methods take the already-validated domain name as input -- they do NOT re-derive from type. This separation allows Story 16-6 (cascading config) to override the domain name at any cascade layer and still get correct resource names. These are DEFAULTS only -- 16-6 will provide a higher-level override mechanism.

### Key Interfaces & Types to Reference

| Type | Location | Role |
|------|----------|------|
| `AggregateIdentity` | `Contracts/Identity/AggregateIdentity.cs` | Domain name validation regex pattern (line 13) |
| `EventStoreAggregate<TState>` | `Client/Aggregates/EventStoreAggregate.cs` | Base class that 16-3 will scan for; uses naming engine output |
| `EventStoreProjection<TReadModel>` | `Client/Aggregates/EventStoreProjection.cs` | Base class that 16-3 will scan for; uses naming engine output |
| `IDomainProcessor` | `Client/Handlers/IDomainProcessor.cs` | Must remain unchanged |
| `CommandEnvelope.Domain` | `Contracts/Commands/CommandEnvelope.cs` | Field that receives the convention-derived domain name |

### Expected File Structure After Implementation

```
src/Hexalith.EventStore.Client/
├── Aggregates/
│   ├── EventStoreAggregate.cs              # UNCHANGED (Story 16-1)
│   └── EventStoreProjection.cs             # UNCHANGED (Story 16-1)
├── Attributes/                              # NEW folder
│   └── EventStoreDomainAttribute.cs         # NEW -- [EventStoreDomain("name")] attribute
├── Conventions/                             # NEW folder
│   └── NamingConventionEngine.cs            # NEW -- type -> kebab-case domain derivation
├── Handlers/
│   ├── IDomainProcessor.cs                 # UNCHANGED
│   └── DomainProcessorBase.cs              # UNCHANGED
└── Registration/
    └── EventStoreServiceCollectionExtensions.cs  # UNCHANGED
```

### API Shape (Expected Usage After This Story)

```csharp
// Automatic derivation from type name:
string domain = NamingConventionEngine.GetDomainName(typeof(OrderAggregate));
// Returns: "order"

string domain2 = NamingConventionEngine.GetDomainName(typeof(UserManagementAggregate));
// Returns: "user-management"

// Attribute override:
[EventStoreDomain("billing")]
public class OrderAggregate : EventStoreAggregate<OrderState> { }
string domain3 = NamingConventionEngine.GetDomainName(typeof(OrderAggregate));
// Returns: "billing"

// Resource name derivation:
string stateStore = NamingConventionEngine.GetStateStoreName("order");
// Returns: "order-eventstore"

string topic = NamingConventionEngine.GetPubSubTopic("acme", "order");
// Returns: "acme.order.events"

string endpoint = NamingConventionEngine.GetCommandEndpoint("order");
// Returns: "order-commands"

// Generic convenience method:
string domainGeneric = NamingConventionEngine.GetDomainName<OrderAggregate>();
// Returns: "order"
```

### Previous Story Intelligence (Story 16-1)

**Patterns established that MUST be followed:**
- Static `ConcurrentDictionary<Type, ...>` for reflection cache -- same pattern for naming cache
- CA1822 compliance: all `NamingConventionEngine` methods are static (the class itself is static), so CA1822 is inherently satisfied
- Test naming: `{Method}_{Scenario}_{ExpectedResult}` (e.g., `GetDomainName_OrderAggregate_ReturnsOrder`)
- Test infrastructure: xunit assertions (`Assert.*`)
- One public type per file, file name = type name

**Review fixes applied in 16-1 (avoid repeating these mistakes):**
- **Fail-fast for unknown/invalid inputs:** Do NOT silently return defaults. Throw `ArgumentException` with clear message for any invalid domain name.
- **Stricter input validation:** Reject edge cases explicitly (empty after suffix strip, uppercase attribute values, leading/trailing hyphens).
- **Backward-compatibility verification test:** Add a test proving existing `AddEventStoreClient<TProcessor>()` still works.

### Testing Expectations

**Test file locations:**
- `tests/Hexalith.EventStore.Client.Tests/Attributes/EventStoreDomainAttributeTests.cs` (NEW)
- `tests/Hexalith.EventStore.Client.Tests/Conventions/NamingConventionEngineTests.cs` (NEW)

**Test method naming convention:** `{Method}_{Scenario}_{ExpectedResult}`

**Test framework:** xunit assertions (`Assert.*`, match existing test infrastructure)

**Key test scenarios for NamingConventionEngine:**

| # | Test | Input | Expected |
|---|------|-------|----------|
| 1 | Single word aggregate | `CounterAggregate` | `"counter"` |
| 2 | Multi-word aggregate | `UserManagementAggregate` | `"user-management"` |
| 3 | Projection suffix | `OrderProjection` | `"order"` |
| 4 | Processor suffix | `PaymentProcessor` | `"payment"` |
| 5 | No known suffix | `OrderHandler` | `"order-handler"` |
| 6 | Single word no suffix | `Order` (class name) | `"order"` |
| 7 | Acronym at start | `HTTPClientAggregate` | `"http-client"` |
| 8 | Acronym 2-char | `IOAggregate` | `"io"` |
| 9 | Acronym 3-char at start | `AWSLambdaAggregate` | `"aws-lambda"` |
| 10 | Acronym in middle | `MyHTTPClientAggregate` | `"my-http-client"` |
| 11 | Letter-to-digit boundary | `Order2Aggregate` | `"order-2"` |
| 12 | Digit-to-uppercase boundary | `V2OrderAggregate` | `"v-2-order"` |
| 13 | Letter-to-digit + digit-to-lowercase | class `order2x` scenario | `"order-2x"` |
| 14 | Compound suffix | `AggregateProjection` | `"aggregate"` (strips `Projection` from end) |
| 15 | Attribute override | `[EventStoreDomain("billing")] OrderAggregate` | `"billing"` |
| 16 | Attribute with uppercase | `[EventStoreDomain("BILLING")]` | Throws `ArgumentException` (regex validation) |
| 17 | Attribute with empty name | `[EventStoreDomain("")]` | Throws `ArgumentException` (constructor) |
| 18 | Name too long (>64 chars) | very long class name | Throws `ArgumentException` |
| 19 | Empty after suffix strip | class named `Aggregate` | Throws `ArgumentException` |
| 20 | Leading hyphen result | type that would produce leading hyphen | Throws `ArgumentException` |
| 21 | Cache hit | Same type called twice | Returns identical string reference |
| 22 | Cache isolation | Call `ClearCache()` between tests | No cross-test contamination |
| 23 | Inherited = false | `DerivedAggregate` inherits from `[EventStoreDomain("base")] BaseAggregate` | Returns convention name for derived, NOT "base" |
| 24 | Generic convenience | `GetDomainName<CounterAggregate>()` | `"counter"` |
| 25 | State store derivation | `"order"` | `"order-eventstore"` |
| 26 | Pub/sub topic derivation | `("acme", "order")` | `"acme.order.events"` |
| 27 | Command endpoint derivation | `"order"` | `"order-commands"` |
| 28 | Null type input | `null` | Throws `ArgumentNullException` |

**Key test scenarios for EventStoreDomainAttribute:**

| # | Test | Input | Expected |
|---|------|-------|----------|
| 1 | Valid domain name | `"billing"` | Property set to `"billing"` |
| 2 | Empty string | `""` | Throws `ArgumentException` |
| 3 | Null string | `null` | Throws `ArgumentNullException` |
| 4 | Whitespace | `"  "` | Throws `ArgumentException` |
| 5 | Valid kebab-case | `"user-management"` | Property set to `"user-management"` |

**Test infrastructure notes:**
- Implement `IDisposable` on test classes and call `NamingConventionEngine.ClearCache()` in `Dispose()` to guarantee cleanup even on test failure. This is more robust than constructor-only cleanup.
- Use `[Theory]` + `[InlineData]` for the truth table tests -- each row in the kebab-case truth table becomes an `[InlineData]` case. This gives 14+ test cases from a single test method:
  ```csharp
  [Theory]
  [InlineData(typeof(OrderAggregate), "order")]
  [InlineData(typeof(UserManagementAggregate), "user-management")]
  // ... all truth table rows
  public void GetDomainName_ValidInputs_ReturnsExpectedKebabCase(Type input, string expected) { ... }
  ```
- Separate positive and negative paths into distinct `[Theory]` methods: `GetDomainName_ValidInputs_ReturnsExpectedKebabCase` and `GetDomainName_InvalidInputs_ThrowsArgumentException`
- Add null input test: `GetDomainName_NullType_ThrowsArgumentNullException`
- Create small test-only stub classes (e.g., `internal class CounterAggregate { }`, `[EventStoreDomain("billing")] internal class BillingAggregate { }`) inside the test file for type resolution tests
- These test stubs do NOT need to extend `EventStoreAggregate<T>` because `GetDomainName` is type-agnostic

### Project Structure Notes

- New `Attributes/` folder follows architecture document's prescribed structure (line 735-736)
- New `Conventions/` folder follows architecture document's prescribed structure (line 737-738)
- File naming follows existing convention: one public type per file, file name = type name
- No conflicts with existing code -- purely additive

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-02-28.md#Section 4.3] -- Epic 16 story definition (Story 16-2: EventStoreDomain Attribute and Naming Convention Engine)
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-02-28.md#Section 4.2] -- Architecture changes: Convention Engine Naming Patterns, project structure, enforcement rule #17
- [Source: _bmad-output/planning-artifacts/architecture.md#Convention Engine Naming] -- Naming convention rules (lines 532-553)
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rules] -- Rule #17: kebab-case conventions (line 1117)
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure] -- Attributes/ and Conventions/ folders (lines 735-738)
- [Source: src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs#line 13] -- Domain validation regex
- [Source: src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs] -- ConcurrentDictionary caching pattern reference
- [Source: _bmad-output/implementation-artifacts/16-1-eventstore-aggregate-base-class.md] -- Previous story learnings and patterns
- [Source: _bmad-output/planning-artifacts/prd.md#FR48] -- FR48: EventStoreAggregate with convention-based naming

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

- Initial test run: 2 failures — `order2x` expected output corrected from `"order2x"` to `"order-2x"` (letter-to-digit boundary at `r→2` is correct per the regex spec; AC3's digit-to-lowercase NO boundary rule means `2→x` has no boundary, but the preceding `r→2` transition is letter-to-digit which DOES produce a boundary); `Aggregate` suffix strip guard `typeName.Length > suffix.Length` changed to `>=` equality handling to allow empty result and throw.
- After fixes: 79/79 tests pass, 0 warnings.

### Completion Notes List

- Implemented `EventStoreDomainAttribute` as a sealed class with `[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]`. Constructor validates non-null, non-empty, non-whitespace.
- Implemented `NamingConventionEngine` as a static class with:
  - `GetDomainName(Type)` and `GetDomainName<T>()` — type-agnostic domain name derivation
  - PascalCase-to-kebab-case conversion using compiled regex with 3 boundary rules (lowercase/digit→uppercase, uppercase→uppercase+lowercase, letter→digit)
  - Suffix stripping for `Aggregate`, `Projection`, `Processor` (first match from end)
  - Attribute override detection via `GetCustomAttribute<EventStoreDomainAttribute>()`
  - Domain name validation using regex `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$` with 64-char max
  - `ConcurrentDictionary<Type, string>` caching with `internal static void ClearCache()` for test isolation
  - Resource name derivation: `GetStateStoreName`, `GetPubSubTopic`, `GetCommandEndpoint`
- Added `InternalsVisibleTo` for test project in Client.csproj
- 5 attribute tests + 28 naming convention engine tests (18 via Theory/InlineData + 10 individual Facts)
- All 79 Client tests pass (including 56 pre-existing tests — zero regressions)
- All 157 Contracts tests pass (zero regressions)
- Build succeeds with 0 warnings, 0 errors
- No new NuGet dependencies added (AC10 satisfied)
- No existing public types modified (AC11 satisfied)

### File List

- `src/Hexalith.EventStore.Client/Attributes/EventStoreDomainAttribute.cs` (NEW)
- `src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs` (NEW)
- `src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj` (MODIFIED — added InternalsVisibleTo)
- `tests/Hexalith.EventStore.Client.Tests/Attributes/EventStoreDomainAttributeTests.cs` (NEW)
- `tests/Hexalith.EventStore.Client.Tests/Conventions/NamingConventionEngineTests.cs` (NEW)

### Senior Developer Review (AI)

- 2026-02-28: Adversarial review executed for Story 16-2.
- Fixed: Added strict input validation guards to `NamingConventionEngine` resource-name derivation methods (`GetStateStoreName`, `GetPubSubTopic`, `GetCommandEndpoint`) for null/empty/whitespace, regex compliance, and max length.
- Fixed: Added negative-path unit tests for invalid/null tenant/domain inputs in resource-name derivation.
- Fixed: Story wording aligned to implemented/acquired behavior for `order2x` (`order-2x`) and test framework wording aligned to actual `xUnit Assert.*` usage.
- Verification: `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj` → **109 passed, 0 failed**.
- Git vs story note: current working tree includes unrelated ongoing changes outside this story scope; Story 16-2 implementation files and tests were directly verified by content and execution.

### Change Log

- 2026-02-28: Story 16-2 implemented — EventStoreDomainAttribute and NamingConventionEngine with full test coverage (79 tests, 0 regressions)
- 2026-02-28: Senior Developer Review (AI) follow-up — added resource-name input validation, expanded negative-path tests, aligned AC/test wording, and verified 109/109 client tests passing.
