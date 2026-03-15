# Story 1.1: Core Identity & Event Envelope

Status: done

## Story

As a domain service developer,
I want a canonical identity scheme and event metadata envelope,
So that all events carry consistent, complete metadata from the start.

## Acceptance Criteria

1. **AggregateIdentity** encapsulates the `tenant:domain:aggregate-id` tuple with parse/format methods. All three components are required, non-empty strings.

2. **EventMetadata** record contains exactly **15 named parameters** (11 existing + 4 new). FR11 specifies 14 logical fields (extension bag is #14, lives on EventEnvelope as `Extensions`); we carry `SerializationFormat` as a 15th parameter because it's a property of the serialized event.

   **FR11-to-C# mapping (parameter order):**

   | # | FR11 Name | C# Parameter | Type | Validation |
   |---|-----------|-------------|------|------------|
   | 1 | event message ID | `MessageId` | string (ULID) | non-empty |
   | 2 | aggregate ID | `AggregateId` | string (ULID) | non-empty |
   | 3 | aggregate type | `AggregateType` | string | non-empty |
   | 4 | tenant | `TenantId` | string | non-empty |
   | 5 | domain | `Domain` | string | non-empty |
   | 6 | sequence number | `SequenceNumber` | long | >= 1 (FR12) |
   | 7 | global position | `GlobalPosition` | long | >= 0 |
   | 8 | timestamp | `Timestamp` | DateTimeOffset | - |
   | 9 | correlation ID | `CorrelationId` | string (ULID) | non-empty |
   | 10 | causation ID | `CausationId` | string (ULID) | non-empty |
   | 11 | user identity | `UserId` | string | non-empty |
   | 12 | domain service version | `DomainServiceVersion` | string | non-empty |
   | 13 | event type | `EventTypeName` | string | non-empty |
   | 14 | metadata version | `MetadataVersion` | int | >= 1 (FR65) |
   | — | *(not in FR11)* | `SerializationFormat` | string | non-empty |
   | 14* | extension bag | *On EventEnvelope as `Extensions`* | — | — |

   - `MetadataVersion` validated >= 1 at construction (FR65). Values < 1 throw `ArgumentOutOfRangeException`.
   - `GlobalPosition` validated >= 0 at construction. Values < 0 throw `ArgumentOutOfRangeException`.

3. All ULID fields (`messageId`, `aggregateId`, `correlationId`, `causationId`) are `string`-typed and generated via `UniqueIdHelper.GenerateSortableUniqueStringId()` (D12).

4. EventEnvelope.ToString() redacts payload (SEC-5, Rule 5).

5. Extensions dictionary is defensively copied to preserve immutability.

6. All public types have XML documentation (UX-DR19).

7. All existing and new Tier 1 tests pass.

8. **Server EventEnvelope** (`src/Hexalith.EventStore.Server/Events/EventEnvelope.cs`) — a separate flat record with 13 parameters (11 metadata + Payload + Extensions) — is updated to include the same 4 new metadata fields (`MessageId`, `AggregateType`, `GlobalPosition`, `MetadataVersion`), keeping both envelope types in sync.

9. **Done definition:** All 15 EventMetadata parameters present with validation, both Contracts and Server EventEnvelope types carry the new fields, EventEnvelope.ToString() includes all fields with payload redacted, all downstream projects compile, all Tier 1 tests green (`dotnet build Hexalith.EventStore.slnx` + all Tier 1 test projects).

## Tasks / Subtasks

- [x] Task 1: Audit EventMetadata fields against FR11 14-field specification (AC: #2)
  - [x] 1.1 Add `MessageId` field (event message ID) — currently missing
  - [x] 1.2 Add `AggregateType` field (aggregate type, distinct from Domain) — currently missing
  - [x] 1.3 Add `GlobalPosition` field (cross-aggregate monotonic position) — currently missing
  - [x] 1.4 Add `MetadataVersion` field (integer, starting at 1, FR65) — currently missing
  - [x] 1.5 Keep `SerializationFormat` on EventMetadata (decided: it's a property of the serialized event). No action needed beyond verifying its position in the parameter order.
- [x] Task 2: Update EventEnvelope to reflect EventMetadata changes (AC: #2, #4, #5)
  - [x] 2.1 Update ToString() to include new fields while keeping payload redacted
  - [x] 2.2 Verify defensive copy of Extensions dictionary
- [x] Task 3: Verify AggregateIdentity completeness (AC: #1)
  - [x] 3.1 Confirm parse/format methods work correctly
  - [x] 3.2 Confirm all derived keys (ActorId, EventStreamKeyPrefix, MetadataKey, SnapshotKey, PubSubTopic) are correct
  - [x] 3.3 Confirm validation (regex, length, colon prohibition)
- [x] Task 4: Verify ULID integration (AC: #3)
  - [x] 4.1 Confirm `Hexalith.Commons.UniqueIds` dependency is referenced
  - [x] 4.2 Confirm all ULID fields are `string`-typed (not custom value objects)
- [x] Task 5: Update Tier 1 tests (AC: #8, #9)
  - [x] 5.1 Update EventMetadataTests for new fields (construction, validation). **Explicitly update `EventMetadata_HasExactly11Fields` test → change assertion to 15.**
  - [x] 5.2 Add MetadataVersion validation test: values < 1 must throw `ArgumentOutOfRangeException`
  - [x] 5.3 Add GlobalPosition validation test: values < 0 must throw `ArgumentOutOfRangeException`
  - [x] 5.4 Update EventEnvelopeTests for new fields and updated ToString()
  - [x] 5.5 Verify AggregateIdentityTests pass (existing)
  - [x] 5.6 Verify IdentityParserTests pass (existing)
  - [x] 5.7 Run full `dotnet test tests/Hexalith.EventStore.Contracts.Tests/`
- [x] Task 6: Verify XML documentation on all public types (AC: #7)
- [x] Task 7: Fix all compilation errors across the ENTIRE solution caused by EventMetadata changes (AC: #9)
  - [x] 7.1 Grep `EventMetadata` across ALL projects (not just listed ones): `src/`, `tests/`, `samples/`
  - [x] 7.2 Update all callers of EventMetadata constructor (new parameters added)
  - [x] 7.3 Update any callers that destructure or pattern-match EventMetadata
  - [x] 7.4 Verify `dotnet build Hexalith.EventStore.slnx --configuration Release` succeeds with zero warnings
  - [x] 7.5 Run ALL Tier 1 test projects (Contracts.Tests, Client.Tests, Sample.Tests, Testing.Tests)
- [x] Task 8: Update Server EventEnvelope to match new metadata fields (AC: #9)
  - [x] 8.1 Add `MessageId`, `AggregateType`, `GlobalPosition`, `MetadataVersion` parameters to `src/Hexalith.EventStore.Server/Events/EventEnvelope.cs`
  - [x] 8.2 Update ToString() to include new fields
  - [x] 8.3 Update `PayloadProtectionTests.cs` ServerEventEnvelope construction (line 167)
  - [x] 8.4 Grep `new ServerEventEnvelope(` and `Server.Events.EventEnvelope` across all test files
  - [x] 8.5 Check for any **conversion logic** between `Contracts.EventEnvelope` and `Server.EventEnvelope` in `EventPersister.cs`, `EventStreamReader.cs`, or `AggregateActor.cs` — update field mappings

### Review Follow-ups (AI)

- [x] AI-Review (HIGH): EventPersister, FakeEventPersister, EventEnvelopeBuilder used Guid.NewGuid() instead of UniqueIdHelper.GenerateSortableUniqueStringId() for MessageId (AC#3/D12 violation) — fixed
- [x] AI-Review (HIGH): EventEnvelopeBuilder.Build() created composite AggregateId (`tenant:domain:id`) instead of bare id — fixed, tests updated
- [x] AI-Review (MEDIUM): EventMetadata had no non-empty validation for new string fields (MessageId, AggregateType) — fixed, ArgumentException thrown on null/whitespace
- [ ] AI-Review (MEDIUM): EventPersister.cs:82 and FakeEventPersister.cs:60 use `AggregateType: "unknown"` hardcoded placeholder — needs architecture change to pass aggregate type through the pipeline
- [x] AI-Review (MEDIUM): EventEnvelopeTests.Metadata_ExposesAll15Fields was missing Timestamp assertion — fixed

## Dev Notes

### Scope Summary

Add 4 missing fields (`MessageId`, `AggregateType`, `GlobalPosition`, `MetadataVersion`) to `EventMetadata`, propagate to `Server.EventEnvelope`, update `EventEnvelopeBuilder`, fix all construction sites, and run all tests. AggregateIdentity is already complete — verify only.

### Task Execution Order

Task 1 (EventMetadata) → Task 2 (Contracts EventEnvelope) → Task 8 (Server EventEnvelope) → Task 7 (fix all downstream) → Task 5 (tests) → Tasks 3, 4, 6 (verification).

### Existing Implementation State

The Contracts project is **substantially implemented**. This story is NOT greenfield — it's an audit-and-complete story. Key existing files:

| File | Status | Notes |
|------|--------|-------|
| `src/.../Identity/AggregateIdentity.cs` | Complete | Regex validation, key derivation, colon prohibition all implemented |
| `src/.../Identity/IdentityParser.cs` | Complete | Parse, TryParse, ParseStateStoreKey all implemented |
| `src/.../Events/EventMetadata.cs` | **Needs update** | Has 11 fields, FR11 requires 14 |
| `src/.../Events/EventEnvelope.cs` | Needs update | Depends on EventMetadata changes |
| `src/.../Server/Events/EventEnvelope.cs` | **Needs update** | Separate flat record — also needs 4 new fields (AC #9) |
| `src/.../Events/IEventPayload.cs` | Complete | Marker interface |
| `src/.../Events/IRejectionEvent.cs` | Complete | Marker interface extending IEventPayload |
| `src/.../Events/ISerializedEventPayload.cs` | Complete | EventTypeName, PayloadBytes, SerializationFormat |

### EventMetadata Gap Analysis (FR11)

**Current EventMetadata has 11 fields:**
- AggregateId, TenantId, Domain, SequenceNumber, Timestamp, CorrelationId, CausationId, UserId, DomainServiceVersion, EventTypeName, SerializationFormat

**FR11 requires 14 fields. Missing fields:**

1. **`MessageId`** (string, ULID) — Unique event identifier. Add as first parameter.
2. **`AggregateType`** (string) — Aggregate type name (e.g., "counter"), distinct from `Domain` (e.g., "counter-domain"). Domain is the bounded context; AggregateType is the specific aggregate within it. **Validation: non-empty only.** No kebab-case or format validation in Contracts — the server derives this value from the class name via `KebabConverter` (Rule 17) and populates it.
3. **`GlobalPosition`** (long, >= 0) — Cross-aggregate monotonic position for global ordering. Assigned by the persistence layer, not the domain service. Use `long` (not nullable) — the Contracts type defines the shape; the server assigns the value at persistence time. A value of 0 is never valid in a persisted event but is acceptable as a construction default for in-flight processing.
4. **`MetadataVersion`** (int, >= 1) — Schema version for the metadata envelope itself, starting at 1 (FR65). Enables future envelope schema evolution. Validated at construction: values < 1 throw `ArgumentOutOfRangeException`.

**`SerializationFormat` decision (resolved):** Keep on EventMetadata. It's a property of the serialized event and belongs with metadata. The 14 fields in FR11 are a logical grouping; the extension bag lives on EventEnvelope as `Extensions`. No further evaluation needed.

### Architecture Constraints

- **Pre-release:** No production data exists. Dev state stores can be wiped (`dapr init --slim`). No backward-compat shim needed.

- **SEC-1:** EventStore owns ALL metadata fields. Domain services return ONLY event payloads. The server populates MessageId, AggregateType, GlobalPosition, MetadataVersion after domain service returns.
- **D1:** State store key pattern `{tenant}:{domain}:{aggId}:events:{seq}` — identity derives from AggregateIdentity.
- **D12:** All ULID fields are `string`-typed, generated via `Hexalith.Commons.UniqueIds.UniqueIdHelper`.
- **Rule 8:** Events named in past tense. Rejection events in past-tense negative.
- **Rule 11:** Event store keys are write-once — never updated or deleted.

### Positional Record Safety Warning

EventMetadata has 15 `string`/`long`/`int` parameters. Positional records are **swap-vulnerable** — reordering string params compiles fine but silently corrupts data. At every construction site, **use named arguments**:
```csharp
// GOOD — named arguments prevent silent swap bugs
new EventMetadata(
    MessageId: messageId,
    AggregateId: aggregateId,
    AggregateType: aggregateType,
    // ...etc
)

// BAD — positional args are fragile with 15 params
new EventMetadata(messageId, aggregateId, aggregateType, ...)
```

### High-Risk Construction Sites

**`EventEnvelopeBuilder.Build()` (Testing project, line 93)** — Currently passes `$"{_tenantId}:{_domain}:{_aggregateIdPart}"` as the FIRST positional arg (was AggregateId). After adding MessageId as first param, this composite string would silently become MessageId. The builder MUST be updated to:
1. Add `_messageId`, `_aggregateType`, `_globalPosition`, `_metadataVersion` fields with defaults
2. Add corresponding `With*()` methods
3. Use **named arguments** in the `Build()` method

**`PayloadProtectionTests.cs` (Server.Tests)** — Constructs both `Contracts.EventEnvelope` (via EventMetadata) and `Server.EventEnvelope` directly. Multiple construction sites at lines 22-24, 54-56, 167-169, 182-184. All need 4 new params.

### Downstream Impact Assessment

Adding fields to EventMetadata (a positional record) will break all existing construction sites. Search for:
- `new EventMetadata(` — all callers need updated parameter lists (use named args!)
- `new ServerEventEnvelope(` — Server's flat EventEnvelope also needs new fields
- Pattern matches on EventMetadata — any deconstruction patterns need updating

**Full blast radius — grep `EventMetadata` AND `Server.Events.EventEnvelope` across ALL of these:**
- `src/Hexalith.EventStore.Server/` — EventPersister, AggregateActor, any pipeline processors
- `src/Hexalith.EventStore.Client/` — any client-side metadata handling
- `src/Hexalith.EventStore.CommandApi/` — any API-layer metadata construction
- `src/Hexalith.EventStore.Testing/` — test helpers creating EventMetadata instances
- `tests/Hexalith.EventStore.Contracts.Tests/Events/EventMetadataTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Events/EventEnvelopeTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/` — any server tests constructing metadata
- `tests/Hexalith.EventStore.Client.Tests/` — any client tests
- `tests/Hexalith.EventStore.Sample.Tests/` — sample tests
- `tests/Hexalith.EventStore.Testing.Tests/` — testing utility tests
- `samples/Hexalith.EventStore.Sample/` — sample domain

### Standards

- **Braces:** Current code uses Egyptian/K&R for records — follow existing pattern (not .editorconfig Allman for records). See `.editorconfig` for all other conventions.
- **Tests:** Follow each test file's existing assertion style — `EventMetadataTests.cs` uses `Assert.Equal` (xUnit), `PayloadProtectionTests.cs` uses `Shouldly`. Don't mix styles within a file.
- **Run:** `dotnet test tests/Hexalith.EventStore.Contracts.Tests/`

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Epic 1, Story 1.1]
- [Source: _bmad-output/planning-artifacts/architecture.md — D1, D12, SEC-1, FR11, FR65]
- [Source: _bmad-output/planning-artifacts/prd.md — FR11, FR12, FR65]
- [Source: src/Hexalith.EventStore.Contracts/Events/EventMetadata.cs — current 11-field implementation]
- [Source: src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs — current envelope]
- [Source: src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs — complete implementation]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

No debug issues encountered. All changes were straightforward field additions.

### Completion Notes List

- Added 4 new fields to EventMetadata: `MessageId`, `AggregateType`, `GlobalPosition`, `MetadataVersion` with FR11/FR65 validation
- Updated Contracts EventEnvelope ToString() to include all 15 metadata fields with payload redacted
- Updated Server EventEnvelope from 13 to 17 parameters with same 4 new fields
- Updated EventEnvelopeBuilder with new fields, defaults, `With*()` methods, and named arguments in `Build()`
- Updated EventEnvelopeAssertions to validate all 15 fields
- Updated FakeEventPersister and EventPersister with new parameters
- Updated EventPublisher conversion logic with new field mappings
- Fixed 30+ construction sites across test files (Server.Tests, Contracts.Tests, Testing.Tests)
- Added new tests: GlobalPosition validation (<0 throws), MetadataVersion validation (<1 throws)
- Updated field count assertion: `EventMetadata_HasExactly11Fields` → `EventMetadata_HasExactly15Fields`
- Updated `Metadata_ExposesAll11Fields` → `Metadata_ExposesAll15Fields`
- Build: 0 warnings, 0 errors
- All 628 Tier 1 tests pass (Contracts: 257, Client: 280, Sample: 29, Testing: 62)
- AggregateIdentity verified complete (93 tests pass)
- ULID dependency `Hexalith.Commons.UniqueIds` confirmed in Contracts.csproj
- All public types have XML documentation

### File List

- src/Hexalith.EventStore.Contracts/Events/EventMetadata.cs (modified — added 4 fields + validation)
- src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs (modified — updated ToString, XML docs)
- src/Hexalith.EventStore.Server/Events/EventEnvelope.cs (modified — added 4 fields, updated ToString)
- src/Hexalith.EventStore.Server/Events/EventPersister.cs (modified — new params in construction)
- src/Hexalith.EventStore.Server/Events/EventPublisher.cs (modified — new params in conversion logic)
- src/Hexalith.EventStore.Testing/Builders/EventEnvelopeBuilder.cs (modified — new fields/With methods/named args)
- src/Hexalith.EventStore.Testing/Assertions/EventEnvelopeAssertions.cs (modified — 15-field validation)
- src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs (modified — new params)
- tests/Hexalith.EventStore.Contracts.Tests/Events/EventMetadataTests.cs (modified — new tests, 15-field assertion)
- tests/Hexalith.EventStore.Contracts.Tests/Events/EventEnvelopeTests.cs (modified — 15-field helper/assertions)
- tests/Hexalith.EventStore.Testing.Tests/Builders/EventEnvelopeBuilderTests.cs (modified — new field assertions)
- tests/Hexalith.EventStore.Server.Tests/Events/EventEnvelopeTests.cs (modified — new params + assertions)
- tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Events/PersistThenPublishResilienceTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Events/SnapshotCreationIntegrationTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprSerializationRoundTripTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Events/AtLeastOnceDeliveryTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Events/TopicIsolationTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherRetryComplianceTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Events/SubscriberIdempotencyTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Events/SnapshotRehydrationTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Events/EventStreamReaderTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Logging/CausationIdLoggingTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Logging/StructuredLoggingCompletenessTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Logging/LogLevelConventionTests.cs (modified — new params)
- tests/Hexalith.EventStore.Server.Tests/Telemetry/EndToEndTraceTests.cs (modified — new params)

### Change Log

- 2026-03-15: Added 4 FR11 metadata fields (MessageId, AggregateType, GlobalPosition, MetadataVersion) to EventMetadata, Contracts EventEnvelope, and Server EventEnvelope. Updated all 30+ construction sites, test helpers, and assertions. All 628 Tier 1 tests pass. Build: 0 warnings, 0 errors.
- 2026-03-15: Code review fixes — replaced Guid.NewGuid() with UniqueIdHelper.GenerateSortableUniqueStringId() for MessageId in EventPersister, FakeEventPersister, EventEnvelopeBuilder (AC#3/D12); fixed EventEnvelopeBuilder to use bare AggregateId instead of composite; added non-empty validation for MessageId and AggregateType in EventMetadata; added missing Timestamp assertion in EventEnvelopeTests. All 636 Tier 1 tests pass. Build: 0 warnings, 0 errors. Remaining action item: AggregateType "unknown" placeholder needs pipeline change.
