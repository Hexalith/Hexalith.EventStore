# Story 1.3: MessageType Value Object & Hexalith.Commons ULID Integration

Status: done

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
  - [x] 1.1 Add `<PackageVersion Include="Hexalith.Commons.UniqueIds" Version="2.13.0" />` to `Directory.Packages.props` under `Hexalith` ItemGroup label
  - [x] 1.2 Add `<PackageReference Include="Hexalith.Commons.UniqueIds" />` to `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj`
  - [x] 1.3 Verify `dotnet restore` and `dotnet build` succeed

- [x] Task 2: Implement `MessageType` value object (AC: #1, #2, #3, #7, #9)
  - [x] 2.1 Create `src/Hexalith.EventStore.Contracts/Messages/KebabConverter.cs` — `internal static` helper with PascalCase-to-kebab regex
  - [x] 2.2 Create `src/Hexalith.EventStore.Contracts/Messages/MessageType.cs` — sealed partial record with Parse/TryParse/Assemble
  - [x] 2.3 Implement `Parse(string)` — regex-validated parsing of `{domain}-{name}-v{ver}` format, max 192 chars
  - [x] 2.4 Implement `TryParse(string, out MessageType?)` — non-throwing variant
  - [x] 2.5 Implement `Assemble(string domain, Type messageType, int version)` — uses `KebabConverter` for PascalCase-to-kebab, no suffix stripping
  - [x] 2.6 Expose `Domain`, `Name`, `Version` properties + `ToString()` returning canonical format
  - [x] 2.7 Value equality via sealed record semantics
  - [x] 2.8 Implement `MessageTypeJsonConverter : JsonConverter<MessageType>` — serialize as JSON string, deserialize via `Parse()`
  - [x] 2.9 Apply `[JsonConverter(typeof(MessageTypeJsonConverter))]` attribute to `MessageType`

- [x] Task 3: Write MessageType tests (AC: #1, #2, #3, #7, #8)
  - [x] 3.1 Create `tests/Hexalith.EventStore.Contracts.Tests/Messages/MessageTypeTests.cs`
  - [x] 3.2 Parse valid inputs: single-segment name, multi-segment name, various versions
  - [x] 3.3 Parse invalid inputs: no version suffix, missing domain, empty string, null, no hyphens, version=0, non-numeric version
  - [x] 3.4 TryParse returns false for invalid inputs (no exceptions)
  - [x] 3.5 Assemble PascalCase types: `TenantCreated` -> `tenant-created`, `OrderItemAdded` -> `order-item-added`
  - [x] 3.6 Assemble single-word types: `Incremented` -> `incremented`
  - [x] 3.7 Assemble negative cases: null domain throws, null type throws, empty domain throws, version=0 throws, version=-1 throws
  - [x] 3.8 Assemble repeated segment: domain=`counter`, type=`CounterIncremented` -> `counter-counter-incremented-v1`
  - [x] 3.9 Max length enforcement: Parse and Assemble reject strings exceeding 192 chars
  - [x] 3.10 Value equality: equal instances, unequal instances
  - [x] 3.11 ToString() round-trip: `Parse(mt.ToString())` equals `mt`
  - [x] 3.12 JSON serialization round-trip: serialize as string, deserialize back to equal instance

- [x] Task 4: Write UniqueIdHelper integration tests (AC: #5, #6, #8)
  - [x] 4.1 Create `tests/Hexalith.EventStore.Contracts.Tests/Identity/UniqueIdHelperIntegrationTests.cs`
  - [x] 4.2 Test `GenerateSortableUniqueStringId()` produces 26-char string
  - [x] 4.3 Test `ExtractTimestamp()` returns valid DateTimeOffset close to now
  - [x] 4.4 Test `ToGuid()` and `ToSortableUniqueId()` round-trip
  - [x] 4.5 Test lexicographic ordering: sequential ULIDs maintain sort order
  - [x] 4.6 Test `ExtractTimestamp()` throws on invalid input (empty, null, truncated, overflow, non-Base32)

- [x] Task 5: Verify all tests pass (AC: #8)
  - [x] 5.1 Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` — all pass
  - [x] 5.2 Run `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings

## Dev Notes

### Implementation Note

This story was originally implemented as **Story 1.7** under the previous epic structure. When epics were regenerated with new numbering (2026-03-15), the story was renumbered to **1.3**. All code, tests, and reviews are complete. See `1-7-messagetype-value-object-and-hexalith-commons-ulid-integration.md` for the original dev agent record with full completion notes and change log.

### Architecture Compliance

- **D12 (ULID Everywhere):** `Hexalith.Commons.UniqueIds.UniqueIdHelper` — NOT a raw ULID library. No custom `UlidId` value object. ULID fields are `string`-typed.
- **D13 (MessageType Convention):** Format `{domain}-{name}-v{ver}` in kebab-case. Domain is single segment (no hyphens), version >= 1.
- **Rule 17:** Convention-derived resource names use kebab-case; PascalCase-to-kebab is automatic.

### Out of Scope

- **`EventMetadata`** — `EventTypeName` remains a raw `string`.
- **`CommandEnvelope`** — `CommandType` remains a raw `string`.
- **`AggregateIdentity`** — No changes.
- **`NamingConventionEngine`** — No changes. Kebab conversion logic duplicated into Contracts, not shared.
- **Existing tests** — No modifications to existing tests. New tests only.

### Project Structure Notes

- `src/Hexalith.EventStore.Contracts/Messages/` — Contains MessageType.cs, KebabConverter.cs, MessageTypeJsonConverter.cs
- `tests/Hexalith.EventStore.Contracts.Tests/Messages/` — Contains MessageTypeTests.cs
- `tests/Hexalith.EventStore.Contracts.Tests/Identity/` — Contains UniqueIdHelperIntegrationTests.cs

### References

- [Source: _bmad-output/planning-artifacts/architecture.md — D12, D13, Rule 17]
- [Source: _bmad-output/planning-artifacts/epics.md — Epic 1, Story 1.3]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-15.md — UlidId removal, UniqueIdHelper adoption]
- [Source: _bmad-output/implementation-artifacts/1-7-messagetype-value-object-and-hexalith-commons-ulid-integration.md — Original implementation record]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context) — original implementation under Story 1.7

### Debug Log References

- Assemble max-length test initially failed — dummy type name too short when kebab-converted. Extended class name to produce ~187 kebab chars. Fixed in one iteration.

### Completion Notes List

- Task 1: Added `Hexalith.Commons.UniqueIds` v2.13.0 as first external Contracts dependency
- Task 2: Implemented `MessageType` as sealed partial record with `[GeneratedRegex]` for source-generated compiled regex. `KebabConverter` is `internal static partial`
- Task 3: 35 MessageType tests covering Parse valid/invalid, TryParse, Assemble PascalCase/negative/repeated-segment, max length, value equality, ToString round-trip, JSON serialization round-trip
- Task 4: 9 UniqueIdHelper integration tests covering generation, timestamp extraction, Guid round-trip, lexicographic ordering, invalid input rejection
- Task 5: Full Tier 1 regression suite passes (608 tests, 0 failures). Release build: 0 warnings, 0 errors
- Code reviewed twice, all fixes applied (unicode CLR type name rejection, JSON null rejection, regex cleanup)

### File List

- `Directory.Packages.props` — Modified (added Hexalith.Commons.UniqueIds v2.13.0)
- `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj` — Modified (added PackageReference)
- `src/Hexalith.EventStore.Contracts/Messages/KebabConverter.cs` — New (internal PascalCase-to-kebab converter)
- `src/Hexalith.EventStore.Contracts/Messages/MessageType.cs` — New (sealed partial record with Parse/TryParse/Assemble)
- `src/Hexalith.EventStore.Contracts/Messages/MessageTypeJsonConverter.cs` — New (JSON string serializer)
- `tests/Hexalith.EventStore.Contracts.Tests/Messages/MessageTypeTests.cs` — New (35 tests)
- `tests/Hexalith.EventStore.Contracts.Tests/Identity/UniqueIdHelperIntegrationTests.cs` — New (9 tests)

### Change Log

- 2026-03-15: Story implemented as 1.7, code reviewed twice, all fixes applied (44 new tests, 0 failures)
- 2026-03-15: Renumbered to Story 1.3 in new epic structure — no code changes, status preserved as done
