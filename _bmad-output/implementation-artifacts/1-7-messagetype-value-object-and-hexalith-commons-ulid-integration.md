# Story 1.7: MessageType Value Object & Hexalith.Commons ULID Integration

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **domain service developer**,
I want a `MessageType` value object that validates `{domain}-{name}-v{ver}` and ULID generation via `Hexalith.Commons.UniqueIds` for identity fields,
So that message routing is safe and IDs are lexicographically sortable.

**FRs:** FR2 (validation), D12 (ULID everywhere), D13 (MessageType convention)

## Acceptance Criteria

1. **MessageType.Parse** - `MessageType.Parse("tenants-create-tenant-v1")` returns domain=`tenants`, name=`create-tenant`, version=`1`. `MessageType.Parse("invalid")` throws with descriptive error.

2. **MessageType.Assemble** - `MessageType.Assemble("tenants", typeof(TenantCreated), 1)` produces `tenants-tenant-created-v1` (PascalCase to kebab conversion).

3. **MessageType properties** - Exposes `Domain` (string), `Name` (string), `Version` (int) properties parsed from the validated string. Provides `ToString()` returning the canonical `{domain}-{name}-v{ver}` format.

4. **Hexalith.Commons.UniqueIds dependency** - Contracts package depends on `Hexalith.Commons.UniqueIds` (NuGet, v2.13.0). `Directory.Packages.props` updated. No custom `UlidId` value object.

5. **UniqueIdHelper integration** - `UniqueIdHelper.GenerateSortableUniqueStringId()` generates valid 26-char Crockford Base32 ULID. `UniqueIdHelper.ExtractTimestamp(string)` extracts creation timestamp. `UniqueIdHelper.ToGuid(string)` converts ULID to Guid. `UniqueIdHelper.ToSortableUniqueId(Guid)` converts Guid back to ULID. ULIDs sort lexicographically by creation time.

6. **String-typed ULID fields** - No custom `UlidId` value object. ULID fields (messageId, aggregateId, correlationId, causationId) remain `string`-typed throughout Contracts.

7. **Serialization round-trip** - `MessageType` serializes as a JSON string (not an object). A custom `JsonConverter<MessageType>` writes `ToString()` on serialize and calls `Parse()` on deserialize. Round-trip preserves value equality.

8. **Tests** - `MessageType` parsing tests cover valid conventions, malformed strings, edge cases. `Assemble` negative tests cover null domain, null type, version <= 0. ULID generation and validation tests use `UniqueIdHelper` directly. All Tier 1 tests pass.

9. **MessageType max length** - Total `MessageType` string length must not exceed 192 characters (domain max 64 + name max 120 + `-v` + version digits). `Parse` and `Assemble` reject strings exceeding this limit.

## Tasks / Subtasks

- [x] Task 1: Add `Hexalith.Commons.UniqueIds` dependency (AC: #4)
    - [x] 1.1 Add `<PackageVersion Include="Hexalith.Commons.UniqueIds" Version="2.13.0" />` to `Directory.Packages.props` under a new `Hexalith` ItemGroup label
    - [x] 1.2 Add `<PackageReference Include="Hexalith.Commons.UniqueIds" />` to `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj`
    - [x] 1.3 Verify `dotnet restore` and `dotnet build` succeed

- [x] Task 2: Implement `MessageType` value object (AC: #1, #2, #3, #7, #9)
    - [x] 2.1 Create `src/Hexalith.EventStore.Contracts/Messages/KebabConverter.cs` — `internal static` helper with PascalCase-to-kebab regex (copied from NamingConventionEngine, kept internal for future extraction)
    - [x] 2.2 Create `src/Hexalith.EventStore.Contracts/Messages/MessageType.cs`
    - [x] 2.3 Implement `Parse(string)` — regex-validated parsing of `{domain}-{name}-v{ver}` format, max 192 chars
    - [x] 2.4 Implement `TryParse(string, out MessageType?)` — non-throwing variant
    - [x] 2.5 Implement `Assemble(string domain, Type messageType, int version)` — uses `KebabConverter` for PascalCase-to-kebab, no suffix stripping (raw type name converted as-is, unlike NamingConventionEngine which strips Aggregate/Projection suffixes)
    - [x] 2.6 Expose `Domain`, `Name`, `Version` properties + `ToString()` returning canonical format
    - [x] 2.7 Implement value equality (record or manual Equals/GetHashCode)
    - [x] 2.8 Implement `MessageTypeJsonConverter : JsonConverter<MessageType>` — serialize as JSON string via `ToString()`, deserialize via `Parse()`
    - [x] 2.9 Apply `[JsonConverter(typeof(MessageTypeJsonConverter))]` attribute to `MessageType`

- [x] Task 3: Write MessageType tests (AC: #1, #2, #3, #7, #8)
    - [x] 3.1 Create `tests/Hexalith.EventStore.Contracts.Tests/Messages/MessageTypeTests.cs`
    - [x] 3.2 Test Parse with valid inputs: single-segment name, multi-segment name, various versions
    - [x] 3.3 Test Parse with invalid inputs: no version suffix, missing domain, empty string, null, no hyphens, version=0, non-numeric version
    - [x] 3.4 Test TryParse returns false for invalid inputs (no exceptions)
    - [x] 3.5 Test Assemble with PascalCase types: `TenantCreated` -> `tenant-created`, `OrderItemAdded` -> `order-item-added`
    - [x] 3.6 Test Assemble with single-word types: `Incremented` -> `incremented`
    - [x] 3.7 Test Assemble negative cases: null domain throws, null type throws, empty domain throws, version=0 throws, version=-1 throws
    - [x] 3.8 Test Assemble intentional repeated segment: domain=`counter`, type=`CounterIncremented` -> `counter-counter-incremented-v1` (correct per convention — no deduplication)
    - [x] 3.9 Test max length enforcement: Parse and Assemble reject strings exceeding 192 chars
    - [x] 3.10 Test value equality: equal instances, unequal instances
    - [x] 3.11 Test ToString() round-trip: `Parse(mt.ToString())` equals `mt`
    - [x] 3.12 Test JSON serialization round-trip: serialize as `"tenants-create-tenant-v1"` (string, not object), deserialize back to equal instance

- [x] Task 4: Write UniqueIdHelper integration tests (AC: #5, #6, #8)
    - [x] 4.1 Create `tests/Hexalith.EventStore.Contracts.Tests/Identity/UniqueIdHelperIntegrationTests.cs`
    - [x] 4.2 Test `GenerateSortableUniqueStringId()` produces 26-char string
    - [x] 4.3 Test `ExtractTimestamp()` returns valid DateTimeOffset close to now
    - [x] 4.4 Test `ToGuid()` and `ToSortableUniqueId()` round-trip
    - [x] 4.5 Test lexicographic ordering: two ULIDs generated sequentially maintain sort order
    - [x] 4.6 Test `ExtractTimestamp()` throws on invalid/malformed input (empty string, null, 25-char truncated, 27-char overflow, non-Base32 characters)

- [x] Task 5: Verify all tests pass (AC: #8)
    - [x] 5.1 Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` — all pass, zero errors
    - [x] 5.2 Run `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings

## Out of Scope

This story does NOT modify any existing types. Specifically:

- **`EventMetadata`** — `EventTypeName` remains a raw `string`. A future story may change it to `MessageType`, but that is out of scope here.
- **`CommandEnvelope`** — `CommandType` remains a raw `string`. Same reasoning.
- **`AggregateIdentity`** — No changes.
- **`NamingConventionEngine`** — No changes. The kebab conversion logic is duplicated into Contracts, not shared.
- **Existing tests** — No modifications to existing 147+ tests. New tests only.

## Dev Notes

### Architecture Compliance

- **D12 (ULID Everywhere):** Use `Hexalith.Commons.UniqueIds.UniqueIdHelper` — NOT a raw ULID library. No custom `UlidId` value object. ULID fields are `string`-typed. [Source: architecture.md D12, lines 468-478]
- **D13 (MessageType Convention):** Format is `{domain}-{name}-v{ver}` in kebab-case. Domain is the first segment, version is the last segment after `-v`, name is everything between. [Source: epics.md Story 1.7, lines 561-591]
- **Enforcement Rule 17:** Convention-derived resource names use kebab-case; PascalCase-to-kebab is automatic with suffix stripping. [Source: architecture.md, line 1131]

### PascalCase-to-Kebab Conversion via KebabConverter

The `NamingConventionEngine` in Client package (`src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs:20`) already implements this regex:

```
@"(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])|(?<=[a-zA-Z])([0-9])"
```

**DO NOT reference Client from Contracts** (Contracts is the lowest-level package — no upward dependencies). Create `KebabConverter` as an `internal static` class in `Contracts/Messages/KebabConverter.cs` with a single method `ConvertToKebab(string pascalCase) -> string`. This keeps the logic testable and extractable to a shared package later if the duplication becomes a maintenance concern.

**Key difference from NamingConventionEngine:** `KebabConverter` does NOT strip suffixes (Aggregate, Projection, etc.). It performs raw PascalCase-to-kebab conversion only. Suffix stripping is a Client-layer concern for domain name derivation, not a Contracts-layer concern for message type names.

Examples:

- `TenantCreated` -> `tenant-created`
- `OrderItemAdded` -> `order-item-added`
- `CounterIncremented` -> `counter-incremented`
- `CounterAggregate` -> `counter-aggregate` (no suffix stripping — this is intentional)

### File Placement

- New class: `src/Hexalith.EventStore.Contracts/Messages/KebabConverter.cs` (internal static helper)
- New class: `src/Hexalith.EventStore.Contracts/Messages/MessageType.cs` (new `Messages/` folder)
- New class: `src/Hexalith.EventStore.Contracts/Messages/MessageTypeJsonConverter.cs` (custom JsonConverter)
- New tests: `tests/Hexalith.EventStore.Contracts.Tests/Messages/MessageTypeTests.cs`
- New tests: `tests/Hexalith.EventStore.Contracts.Tests/Identity/UniqueIdHelperIntegrationTests.cs`
- Modified: `Directory.Packages.props` (add package version)
- Modified: `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj` (add PackageReference)

### Existing Patterns to Follow

- **Namespace:** `Hexalith.EventStore.Contracts.Messages` (file-scoped, per .editorconfig)
- **Validation style:** Eager — throw `ArgumentException` / `FormatException` on invalid input (matches `AggregateIdentity` pattern)
- **Record types:** Use `record` or `record struct` for value semantics (matches `EventMetadata`, `EventEnvelope`)
- **Test framework:** xUnit + Shouldly assertions (e.g., `result.ShouldBe(expected)`)
- **Test naming:** `MethodName_Scenario_ExpectedResult` or descriptive `[Fact]` names
- **Braces:** Allman style (new line before opening brace) per .editorconfig
- **Private fields:** `_camelCase` prefix

### Anti-Patterns to Avoid

- **DO NOT create a custom `UlidId` value object** — this was explicitly rejected in sprint-change-proposal-2026-03-15
- **DO NOT add a direct ULID library dependency** (e.g., `Ulid`, `NUlid`) — use `Hexalith.Commons.UniqueIds` which transitively brings `ByteAether.Ulid`
- **DO NOT reference `Hexalith.EventStore.Client`** from Contracts — Contracts is dependency-free (except the new `Hexalith.Commons.UniqueIds`)
- **DO NOT make MessageType mutable** — it must be a value type or record with immutable properties
- **DO NOT use `Guid.NewGuid()`** for ID generation in any new code — use `UniqueIdHelper.GenerateSortableUniqueStringId()` per D12

### MessageType Format Specification

Format: `{domain}-{name}-v{ver}`

- **domain**: Always a **single** kebab segment — no hyphens within domain (e.g., `tenants`, `counter`, `order`). This is consistent with `NamingConventionEngine.GetDomainName()` which always produces single-segment names: `CounterAggregate` -> `counter`, `TenantAggregate` -> `tenant`, `OrderAggregate` -> `order`.
- **name**: One or more kebab segments (e.g., `create-tenant`, `counter-incremented`, `order-item-added`)
- **ver**: Integer version >= 1 after `-v` suffix (e.g., `1`, `2`)
- **Max total length**: 192 characters

**Full examples**:

- `tenants-create-tenant-v1` — domain=`tenants`, name=`create-tenant`, version=1
- `counter-counter-incremented-v1` — domain=`counter`, name=`counter-incremented`, version=1 (repeated `counter` is correct and intentional — domain is the routing segment, name is the full type name converted to kebab)
- `order-order-item-added-v2` — domain=`order`, name=`order-item-added`, version=2

Parse algorithm:

1. Validate non-null/empty, max 192 chars
2. Find last `-v{digits}` suffix — extract version, validate >= 1
3. Find first hyphen — everything before is domain (single segment, no hyphens)
4. Everything between first hyphen and version suffix is the name (strip leading/trailing hyphens)
5. Validate domain matches `^[a-z0-9]+$` (single segment — no hyphens allowed in domain)
6. Validate name is non-empty and matches kebab-case pattern `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`
7. Validate version >= 1

### UniqueIdHelper API Reference (Hexalith.Commons.UniqueIds v2.13.0)

```csharp
// Generate a new ULID as 26-char Crockford Base32 string
string id = UniqueIdHelper.GenerateSortableUniqueStringId();

// Extract UTC creation timestamp from ULID string
DateTimeOffset ts = UniqueIdHelper.ExtractTimestamp(id);

// Convert ULID string to System.Guid (identity-preserving, NOT sort-preserving)
Guid guid = UniqueIdHelper.ToGuid(id);

// Convert Guid back to ULID string
string ulid = UniqueIdHelper.ToSortableUniqueId(guid);
```

### Project Structure Notes

- Contracts package currently has zero external dependencies — `Hexalith.Commons.UniqueIds` will be its FIRST external package reference
- Test project (`Contracts.Tests`) already references Client, Server, Testing packages transitively — adding Contracts dependency on `Hexalith.Commons.UniqueIds` requires no test project changes
- The `Messages/` folder does not exist yet in either `src/` or `tests/` Contracts — create both

### Previous Story Intelligence (Story 1.6)

- **147 tests** passing across 9 test files covering all existing Contracts types
- Key patterns: eager validation, defensive copying, record equality, boundary testing
- Tests were built incrementally during Stories 1.2-1.4 as part of TDD
- All existing IDs in tests use hard-coded strings (`"order-123"`, `"acme"`, `"corr-1"`) — not ULIDs. Story 1.7 tests should use `UniqueIdHelper` for ULID tests but can keep simple strings for MessageType-specific tests

### Git Intelligence

Recent commits show Epic 18 (Query Pipeline) completion. No conflicts with Story 1.7 scope (Contracts package, pure types).

### References

- [Source: architecture.md D12, lines 468-478] — ULID decision and API surface
- [Source: epics.md Story 1.7, lines 561-591] — Acceptance criteria
- [Source: sprint-change-proposal-2026-03-15.md] — UlidId removal, UniqueIdHelper adoption
- [Source: NamingConventionEngine.cs] — PascalCase-to-kebab regex pattern (DO NOT reference, copy logic)
- [Source: AggregateIdentity.cs] — Validation pattern and kebab-case regex to follow

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Assemble max-length test initially failed — dummy type name was too short when kebab-converted (153 chars + domain + version = 164, under 192). Extended class name to produce ~187 kebab chars → 198 total. Fixed in one iteration.

### Completion Notes List

- ✅ Task 1: Added `Hexalith.Commons.UniqueIds` v2.13.0 as first external Contracts dependency. Centralized version in `Directory.Packages.props` under `Hexalith` label.
- ✅ Task 2: Implemented `MessageType` as a `sealed partial record` with `Parse`, `TryParse`, `Assemble`, value equality, and `MessageTypeJsonConverter`. Used `[GeneratedRegex]` for source-generated compiled regex. `KebabConverter` is `internal static partial` — duplicates NamingConventionEngine regex without suffix stripping.
- ✅ Task 3: 35 MessageType tests covering Parse valid/invalid, TryParse, Assemble PascalCase/negative/repeated-segment, max length, value equality, ToString round-trip, JSON serialization round-trip.
- ✅ Task 4: 9 UniqueIdHelper integration tests covering generation, timestamp extraction, Guid round-trip, lexicographic ordering, and invalid input rejection.
- ✅ Task 5: Full Tier 1 regression suite passes (608 tests, 0 failures). Release build: 0 warnings, 0 errors.
- ✅ Review fixes applied: `MessageType.Assemble` now rejects CLR type names that convert to invalid non-ASCII kebab-case, and `MessageTypeJsonConverter` now rejects JSON `null` instead of silently returning `null`.
- ✅ Review housekeeping applied: reconciled the Dev Agent Record `File List` with review-driven artifact updates and noted one unrelated pre-existing untracked planning artifact observed in the workspace during review.

### Change Log

- 2026-03-15: Story 1.7 implemented — MessageType value object, KebabConverter, MessageTypeJsonConverter, UniqueIdHelper integration, 44 new tests
- 2026-03-15: Code review fixes applied — enforced `MessageType.Assemble` invariants for generated names, rejected JSON `null` for `MessageType`, added focused regression tests, and synchronized review artifacts
- 2026-03-15: Second code review — fixed non-Base32 test input length (27→26 chars), removed redundant `RegexOptions.Compiled` from `[GeneratedRegex]` attributes in MessageType.cs and KebabConverter.cs

### File List

- `Directory.Packages.props` — Modified (added Hexalith.Commons.UniqueIds v2.13.0)
- `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj` — Modified (added PackageReference)
- `src/Hexalith.EventStore.Contracts/Messages/KebabConverter.cs` — New (internal PascalCase-to-kebab converter)
- `src/Hexalith.EventStore.Contracts/Messages/MessageType.cs` — New, then modified in review (sealed partial record with Parse/TryParse/Assemble; now validates generated kebab names in `Assemble`)
- `src/Hexalith.EventStore.Contracts/Messages/MessageTypeJsonConverter.cs` — New, then modified in review (JSON string serializer; now rejects `null` and non-string JSON tokens explicitly)
- `tests/Hexalith.EventStore.Contracts.Tests/Messages/MessageTypeTests.cs` — New, then modified in review (35 original tests plus focused regression coverage for unicode CLR type names and JSON `null`)
- `tests/Hexalith.EventStore.Contracts.Tests/Identity/UniqueIdHelperIntegrationTests.cs` — New (9 tests)
- `_bmad-output/implementation-artifacts/1-7-messagetype-value-object-and-hexalith-commons-ulid-integration.md` — Modified during review (status, review fix notes, file list reconciliation)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — Modified during review (story status synchronized to `done`)
