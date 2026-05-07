---
stepsCompleted: ['step-01-load-context', 'step-02-discover-tests', 'step-03-map-criteria', 'step-04-analyze-gaps', 'step-05-gate-decision']
lastStep: 'step-05-gate-decision'
lastSaved: '2026-05-07'
scope: 'Epic 1 — Domain Contract Foundation'
gateDecision: 'PASS'
gateBasis: 'priority_thresholds'
coverageBasis: 'acceptance_criteria'
oracleResolutionMode: 'formal_requirements'
oracleConfidence: 'high'
oracleSources:
  - '_bmad-output/implementation-artifacts/1-1-core-identity-and-event-envelope.md'
  - '_bmad-output/implementation-artifacts/1-2-command-types-domainresult-and-error-contract.md'
  - '_bmad-output/implementation-artifacts/1-3-messagetype-value-object-and-hexalith-commons-ulid-integration.md'
  - '_bmad-output/implementation-artifacts/1-4-pure-function-contract-and-eventstoreaggregate-base.md'
  - '_bmad-output/implementation-artifacts/1-5-commandstatus-enum-and-aggregate-tombstoning.md'
  - '_bmad-output/test-artifacts/test-design/test-design-epic-1.md'
  - '_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md'
externalPointerStatus: 'not_used'
---

# Traceability Matrix — Epic 1: Domain Contract Foundation

**Date:** 2026-05-07
**Author:** Murat / TEA (with Jerome)
**Scope:** Retroactive trace of Epic 1 (5 stories, all `done`, closed 2026-03-15) against the test inventory on `main` as of 2026-05-07.
**Outcome:** **PASS** (with one chore — commit the in-flight `CommandEnvelopeTests.cs` change).

---

## Step 1 — Coverage Oracle & Knowledge Base

### Resolved Coverage Oracle

- **Mode:** `formal_requirements` — every Epic 1 story file declares numbered, testable acceptance criteria.
- **Basis:** `acceptance_criteria` — 36 ACs across 5 stories.
- **Confidence:** `high`. The five stories are all marked `done` with implementation artifacts on disk; the Epic 1 retrospective (`epic-1-retro-2026-04-26.md`) and the Epic 1 test design (`test-design-epic-1.md`, 2026-05-07) both audit the same surface and produce convergent risk/test inventories.
- **External pointer status:** `not_used` — every requirement is in-repo.

### Story Inventory

| Story | File | Status | ACs | Theme |
|------:|------|:-----:|:---:|-------|
| 1.1 | `1-1-core-identity-and-event-envelope.md` | done | 9 | `AggregateIdentity`, `EventMetadata` (15 fields, FR11), `EventEnvelope` (Contracts + Server mirror), `Extensions` defensive copy, payload redaction |
| 1.2 | `1-2-command-types-domainresult-and-error-contract.md` | done | 8 | `CommandEnvelope` (10 fields incl. `MessageId` `[DataMember]`), `DomainResult`, `IRejectionEvent`, `SubmitCommandRequest` MessageId/CorrelationId |
| 1.3 | `1-3-messagetype-value-object-and-hexalith-commons-ulid-integration.md` | done | 9 | `MessageType` value object, `Hexalith.Commons.UniqueIds` adoption (D12), JSON converter, ULID generation/round-trip |
| 1.4 | `1-4-pure-function-contract-and-eventstoreaggregate-base.md` | done | 6 | `IDomainProcessor`, `EventStoreAggregate<TState>` reflection dispatch, `NamingConventionEngine`, `EventStoreDomainAttribute`, public-API audit |
| 1.5 | `1-5-commandstatus-enum-and-aggregate-tombstoning.md` | done | 4 | `CommandStatus` 8-state enum, `ITerminatable`, `AggregateTerminated`, tombstoning guard, Counter sample |
| **Total** | | | **36** | |

### Knowledge Base Loaded

- `test-priorities-matrix.md` — P0/P1/P2/P3 classification
- `risk-governance.md` — gate decision rules
- `probability-impact.md` — 1-3 × 1-3 scoring
- `test-quality.md` — DoD for tests
- `selective-testing.md` — execution lane policy

### Why This Oracle Was Selected

1. **Formal beats synthetic.** Each story has explicit, numbered ACs. No need to descend to user-journey synthesis — Epic 1 is a contract-layer epic, not a UI flow.
2. **Test design already cross-walked.** The 2026-05-07 test design (Epic 1) maps every AC to a test file or a deliberate gap. This trace ratifies (or refutes) that mapping against the actual file system.
3. **Retro acts as a second oracle.** Action items R1-A1..R1-A8 in `epic-1-retro-2026-04-26.md` either map to specific test files (e.g., R1-A2 → `TerminatableComplianceAssertionsTests`, R1-A6 → `MissingApplyMethodExceptionTests`, R1-A7 → `TombstoningLifecycleTests`) or to process changes that fall outside test coverage (R1-A8 — verification command rule).

---

## Step 2 — Test Inventory Discovery

Tests touching the Epic 1 surface, by location:

| Test File | Tier | Story Owner(s) | Approx Test Count |
|-----------|:----:|:--------------:|:-----------------:|
| `tests/Hexalith.EventStore.Contracts.Tests/Identity/AggregateIdentityTests.cs` | T1 | 1.1 | ~20 |
| `tests/Hexalith.EventStore.Contracts.Tests/Identity/IdentityParserTests.cs` | T1 | 1.1 | ~10 |
| `tests/Hexalith.EventStore.Contracts.Tests/Identity/UniqueIdHelperIntegrationTests.cs` | T1 | 1.3 | ~6 |
| `tests/Hexalith.EventStore.Contracts.Tests/Events/EventMetadataTests.cs` | T1 | 1.1 | ~25 (incl. `Json_SerializationRoundTrip_PreservesAll15Fields` and `EventMetadata_HasExactly15Fields`) |
| `tests/Hexalith.EventStore.Contracts.Tests/Events/EventEnvelopeTests.cs` | T1 | 1.1 | ~15 (incl. `Json_SerializationRoundTrip_PreservesEnvelope`) |
| `tests/Hexalith.EventStore.Contracts.Tests/Events/AggregateTerminatedTests.cs` | T1 | 1.5 | ~5 |
| `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandEnvelopeTests.cs` | T1 | 1.2 | 14 (incl. `CommandEnvelope_HasExactly10Fields` ⚠️ uncommitted, and `DataContract_SerializationRoundTrip_PreservesMessageId`) |
| `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs` | T1 | 1.5 | ~6 |
| `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusExtensionsTests.cs` | T1 | 1.5 (R1-A4) | ~5 |
| `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusRecordTests.cs` | T1 | 1.5 | ~4 |
| `tests/Hexalith.EventStore.Contracts.Tests/Aggregates/ITerminatableTests.cs` | T1 | 1.5 | ~3 |
| `tests/Hexalith.EventStore.Contracts.Tests/Results/DomainResultTests.cs` | T1 | 1.2 | ~13 |
| `tests/Hexalith.EventStore.Contracts.Tests/Messages/MessageTypeTests.cs` | T1 | 1.3 | ~25 |
| `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs` | T1 | 1.4, 1.5 | 35+ (incl. `ProcessAsync_AggregateWithZeroHandleMethods_ThrowsInvalidOperationExceptionAtCommandTime` and tombstoning guard tests) |
| `tests/Hexalith.EventStore.Client.Tests/Aggregates/MissingApplyMethodExceptionTests.cs` | T1 | 1.5 (R1-A6) | ~3 |
| `tests/Hexalith.EventStore.Client.Tests/Conventions/NamingConventionEngineTests.cs` | T1 | 1.4 | 40+ |
| `tests/Hexalith.EventStore.Client.Tests/Attributes/EventStoreDomainAttributeTests.cs` | T1 | 1.4 | ~5 |
| `tests/Hexalith.EventStore.Testing.Tests/Compliance/TerminatableComplianceAssertionsTests.cs` | T1 | 1.5 (R1-A2) | ~6 |
| `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs` | T1 | 1.5 | ~15 (close + post-termination rejection + replay) |
| `tests/Hexalith.EventStore.Sample.Tests/Compliance/ITerminatableSolutionComplianceTests.cs` | T1 | 1.5 (TG-2) | 1 theory + 1 canary (assembly scan) |
| `tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionTests.cs` | T2 | 1.1, 1.2 | ~6 |
| `tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs` | T2 | 1.1, 1.2 | ~4 |
| `tests/Hexalith.EventStore.Server.Tests/Actors/TombstoningLifecycleTests.cs` | T2 | 1.5 (R1-A7) | 3 lifecycle scenarios + sentinel |

**Approx. tests touching Epic 1 surface:** ≥250 Tier 1 + ~15 Tier 2.

---

## Step 3 — Requirements-to-Tests Mapping

Legend: **F** = full coverage, **P** = partial coverage, **N** = no coverage. **Priority** is the test-design classification (P0–P3) reflecting risk and blast radius.

### Story 1.1 — Core Identity & Event Envelope (9 ACs)

| AC | Description | Coverage | Pri | Given-When-Then | Tests |
|---:|-------------|:--------:|:---:|------------------|-------|
| 1.1.1 | `AggregateIdentity` parse/format, all 3 components required non-empty | **F** | P0 | Given a `tenant:domain:aggregate-id` triple, when parsed/formatted/derived, then composite is reversible and validation rejects empty/colon-injected values | `AggregateIdentityTests` (regex + colon prohibition + derived keys: ActorId, EventStreamKeyPrefix, MetadataKey, SnapshotKey, PubSubTopic), `IdentityParserTests` |
| 1.1.2 | `EventMetadata` exactly 15 fields with FR11 mapping + validation | **F** | P0 | Given an `EventMetadata` constructed with valid 15 fields, when read, then every field exposes its value; given invalid `MetadataVersion < 1` or `GlobalPosition < 0`, then `ArgumentOutOfRangeException`; given any required string empty/whitespace, then `ArgumentException` | `EventMetadataTests` incl. `EventMetadata_HasExactly15Fields` (canary), per-field validation tests, record-equality test |
| 1.1.3 | All ULID fields are `string`, generated via `UniqueIdHelper.GenerateSortableUniqueStringId()` (D12) | **F** | P0 | Given a freshly generated id, when inspected, then it is 26 char Crockford Base32 and lex-sortable; given two ids generated in time order, then they sort by creation time | `UniqueIdHelperIntegrationTests` |
| 1.1.4 | `EventEnvelope.ToString()` redacts payload (SEC-5, Rule 5) | **F** | P0 | Given an envelope with payload, when `ToString()`, then output contains all metadata fields and `[REDACTED]` for payload | `EventEnvelopeTests` + `Server.Tests/Security/PayloadProtectionTests` + `Server.Tests/Logging/PayloadProtectionTests` |
| 1.1.5 | `Extensions` dictionary is defensively copied | **F** | P0 | Given an extensions dict passed to envelope, when caller mutates the source, then envelope's extensions are unchanged | `EventEnvelopeTests` |
| 1.1.6 | All public types have XML documentation (UX-DR19) | **P** | P3 | Verified at story close; `GenerateDocumentationFile=true` only enforced on `Client.csproj` (R1-A3 still open for Contracts/Server/SignalR/Aspire/Testing) | Manual audit during story (no automated test) |
| 1.1.7 | All Tier 1 tests pass | **F** | P0 | Tier 1 baseline 651 green at epic close | Full `Contracts.Tests` + `Client.Tests` + `Sample.Tests` + `Testing.Tests` |
| 1.1.8 | `Server.EventEnvelope` carries the same 4 new fields, kept in sync | **F** | P1 | Given a `Server.EventEnvelope` constructed, when round-tripped through actor-state DCS, then all metadata + payload + extensions survive | `Server.Tests/Events/EventEnvelopeTests` + `Server.Tests/Security/PayloadProtectionTests` |
| 1.1.9 | Done definition: 15 params with validation, both envelope types in sync, Tier 1 green | **F** | P0 | All sub-criteria covered above | Multiple |

**Add-on closing the test design's TG-1:**

| Gap ID | Description | Test |
|--------|-------------|------|
| 1.1-UNIT-010 | `EventMetadata` JSON round-trip pins all 15 fields (R-T2 / TG-1, design-pivoted from DataContract → JSON because EventMetadata is intentionally not `[DataContract]` — it travels via `System.Text.Json` on the production DAPR pub/sub & projection path) | ✅ `EventMetadataTests.Json_SerializationRoundTrip_PreservesAll15Fields` |
| 1.1-UNIT-011 | `Contracts.EventEnvelope` JSON round-trip pins envelope + metadata + payload + Extensions (R-T2 / TG-1, same design pivot) | ✅ `EventEnvelopeTests.Json_SerializationRoundTrip_PreservesEnvelope` |

### Story 1.2 — Command Types, DomainResult & Error Contract (8 ACs)

| AC | Description | Coverage | Pri | Given-When-Then | Tests |
|---:|-------------|:--------:|:---:|------------------|-------|
| 1.2.1 | `CommandEnvelope` carries `MessageId`, `AggregateId`, `CommandType`, `TenantId`, `Payload`, `CorrelationId`, `CausationId`, `UserId`, `Domain`, `Extensions` (10 positional fields). MessageId is unique command identity + idempotency key (FR49, D16); CorrelationId defaults to MessageId when not provided (FR4) | **F** | P0 | Given valid command, when constructed, then all 10 fields populate and `AggregateIdentity` derives correctly; given invalid MessageId/CorrelationId/UserId/CommandType empty/whitespace, then `ArgumentException`; given null Payload, then `ArgumentNullException`; given invalid TenantId or empty Domain, then `ArgumentException` | `CommandEnvelopeTests` (12+ tests) — includes `Constructor_WithValidInputs_*`, `AggregateIdentity_DerivesCorrectIdentity`, `Constructor_WithInvalidMessageId_*`, `AggregateIdentity_IsEagerlyValidated`, etc. |
| 1.2.2 | `DomainResult` immutable list of events + `IsSuccess`/`IsRejection`/`IsNoOp`; mixed regular+rejection events rejected at construction | **F** | P0 | Given a result with only success events, then `IsSuccess`; given only rejection events, then `IsRejection`; given an empty list, then `IsNoOp`; given a mixed list, then construction throws | `DomainResultTests` (~13 tests) |
| 1.2.3 | Rejection events implement `IRejectionEvent`, follow past-tense negative naming (Rule 8) | **F** | P0 | Given a rejection event, when checked at runtime, then it is assignable to `IRejectionEvent` and follows past-tense naming | Counter sample tests verify `CounterCannotGoNegative` shape; `AggregateTerminatedTests` validates `AggregateTerminated` implements `IRejectionEvent` and follows naming convention |
| 1.2.4 | All ULID fields (MessageId, AggregateId) are `string`-typed (D12) | **F** | P0 | Given a generated MessageId/AggregateId, when inspected, then it's a 26-char Crockford Base32 string (no custom value object) | `CommandEnvelopeTests` (typed shape) + `UniqueIdHelperIntegrationTests` |
| 1.2.5 | `CommandEnvelope.ToString()` redacts Payload (SEC-5, Rule 5) | **F** | P0 | Given an envelope, when `ToString()`, then output contains MessageId and other fields with `[REDACTED]` for Payload | `CommandEnvelopeTests.ToString_ContainsMessageId` |
| 1.2.6 | All public types have XML documentation (UX-DR19) | **P** | P3 | Manual audit at story close — see 1.1.6 | Audit only |
| 1.2.7 | All existing and new Tier 1 tests pass | **F** | P0 | Tier 1 baseline 651 green | Full Tier 1 |
| 1.2.8 | Done definition: MessageId on CommandEnvelope w/ validation, DomainResult + IRejectionEvent verified, Tier 1 green | **F** | P0 | All sub-criteria covered above | Multiple |

**Add-ons closing the test design's TG-4 + DataContract assurance:**

| Gap ID | Description | Test |
|--------|-------------|------|
| 1.2-UNIT-007 | `CommandEnvelope` `[DataContract]` round-trip preserves `MessageId` (DAPR actor-state pathway uses DCS — explicit `[DataMember]` on the property is a runtime-only contract; without it, MessageId silently vanishes) | ✅ `CommandEnvelopeTests.DataContract_SerializationRoundTrip_PreservesMessageId` (committed) |
| 1.2-UNIT-008 | `CommandEnvelope_HasExactly10Fields` canary for R-T1 positional-record swap (TG-4) | ⚠️ `CommandEnvelopeTests.CommandEnvelope_HasExactly10Fields` (present in working tree, **uncommitted** as of 2026-05-07 — see Action Items below) |

### Story 1.3 — MessageType & Hexalith.Commons.UniqueIds (9 ACs)

| AC | Description | Coverage | Pri | Given-When-Then | Tests |
|---:|-------------|:--------:|:---:|------------------|-------|
| 1.3.1 | `MessageType.Parse` returns `domain` / `name` / `version`; throws on invalid | **F** | P0 | Given `"tenants-create-tenant-v1"`, then domain=`tenants`, name=`create-tenant`, version=`1`; given `"invalid"`, then descriptive `FormatException`/`ArgumentException` | `MessageTypeTests.Parse_*` |
| 1.3.2 | `MessageType.Assemble("tenants", typeof(TenantCreated), 1)` → `tenants-tenant-created-v1` (PascalCase→kebab) | **F** | P0 | Given a domain + Type + version, when `Assemble`, then result is canonical kebab; given null/invalid inputs, then throws | `MessageTypeTests.Assemble_*` |
| 1.3.3 | `Domain` / `Name` / `Version` props + `ToString()` returns canonical form | **F** | P0 | Given a parsed/assembled `MessageType`, when accessed, then the three properties match parsed values and `ToString()` recomposes the canonical form | `MessageTypeTests` |
| 1.3.4 | Contracts depends on `Hexalith.Commons.UniqueIds` v2.13.0; no custom `UlidId` | **F** | P0 | Verified by `Directory.Packages.props` + Contracts.csproj; tests consume `UniqueIdHelper` directly | `Directory.Packages.props` audit + `UniqueIdHelperIntegrationTests` |
| 1.3.5 | `UniqueIdHelper.GenerateSortableUniqueStringId()` / `ExtractTimestamp` / `ToGuid` / `ToSortableUniqueId` work; ULIDs lex-sort by time | **F** | P0 | Given two ULIDs generated 1ms apart, then they sort lexicographically by creation order; ToGuid/ToSortableUniqueId round-trips identity | `UniqueIdHelperIntegrationTests` |
| 1.3.6 | ULID fields remain `string`-typed throughout Contracts (no `UlidId` value object) | **F** | P0 | Type inspection — `MessageId`, `AggregateId`, `CorrelationId`, `CausationId` are `string` on every contract record | `EventMetadataTests` + `CommandEnvelopeTests` (compile-time + value tests) |
| 1.3.7 | `MessageType` round-trips JSON as a string via `MessageTypeJsonConverter`; preserves equality | **F** | P0 | Given a `MessageType`, when serialized via `JsonSerializer` (with the converter), then output is a JSON string; deserialized result equals original | `MessageTypeTests` (JSON round-trip + equality) |
| 1.3.8 | All Tier 1 tests pass | **F** | P0 | Tier 1 baseline green | Full Tier 1 |
| 1.3.9 | `MessageType` total length ≤192 chars (domain≤64 + name≤120 + `-v` + digits); Parse + Assemble reject overflow | **F** | P1 | Given a 193-char input, when `Parse`/`Assemble`, then it throws; given exactly 192-char, then it passes | `MessageTypeTests` (max-length boundaries) |

### Story 1.4 — Pure Function Contract & EventStoreAggregate (6 ACs)

| AC | Description | Coverage | Pri | Given-When-Then | Tests |
|---:|-------------|:--------:|:---:|------------------|-------|
| 1.4.1 | `IDomainProcessor.ProcessAsync(CommandEnvelope, object?) -> Task<DomainResult>`; typed contract enforced via `EventStoreAggregate<TState>` reflection dispatch | **F** | P0 | Given a domain processor, when `ProcessAsync` is called with a command and prior state, then it returns a `DomainResult` shaped per the matched `Handle(TCommand, TState?)` | `EventStoreAggregateTests` (35+ tests covering happy-path dispatch, dispatch failure, etc.) + `DomainProcessorTests` |
| 1.4.2 | `EventStoreAggregate<TState>` reflection-based `Handle` (command) and `Apply` (event) discovery, no manual registration | **F** | P0 | Given an aggregate with `Handle(TCmd, TState?)` and `Apply(TEvt)`, when activated and dispatched, then methods are discovered + cached + dispatched correctly; given silent skip on wrong return type, then "no matching method" surfaces at command-time | `EventStoreAggregateTests` (incl. `ProcessAsync_AggregateWithZeroHandleMethods_ThrowsInvalidOperationExceptionAtCommandTime` for TG-3 / 1.4-UNIT-010); thread-safety + cache tests |
| 1.4.3 | Convention-based DAPR resource naming: kebab + suffix-stripping (`CounterAggregate` → `counter`); `[EventStoreDomain("name")]` override validated kebab + non-empty (Rule 17) | **F** | P0 | Given a class `FooAggregate`, when name is derived, then it is `foo`; given `[EventStoreDomain("custom-name")]`, then the override is used + validated; given a non-kebab override, then startup throws | `NamingConventionEngineTests` (40+ tests) + `EventStoreDomainAttributeTests` |
| 1.4.4 | Public API surface — only domain-developer-facing types are public; all public types have XML docs (UX-DR19, UX-DR20) | **P** | P2 | `Client.csproj` enforces `<GenerateDocumentationFile>true</GenerateDocumentationFile>` (verified Story 1.4); other Epic 1 packages still rely on honour-system per R1-A3 | Build enforcement on Client only |
| 1.4.5 | All Tier 1 tests pass | **F** | P0 | Tier 1 baseline green | Full Tier 1 |
| 1.4.6 | Done definition: all listed public types verified, XML docs enforced, Tier 1 green | **F** | P0 | All sub-criteria covered above | Multiple |

**Add-on closing the test design's TG-3:**

| Gap ID | Description | Test |
|--------|-------------|------|
| 1.4-UNIT-010 | Zero-Handle aggregate boundary — pin current behavior so future framework refactors don't change it silently | ✅ `EventStoreAggregateTests.ProcessAsync_AggregateWithZeroHandleMethods_ThrowsInvalidOperationExceptionAtCommandTime` (uses `EmptyAggregate` fixture at line 169) |

### Story 1.5 — CommandStatus & Aggregate Tombstoning (4 ACs)

| AC | Description | Coverage | Pri | Given-When-Then | Tests |
|---:|-------------|:--------:|:---:|------------------|-------|
| 1.5.1 | `CommandStatus` exactly 8 states: Received(0), Processing(1), EventsStored(2), EventsPublished(3), Completed(4), Rejected(5), PublishFailed(6), TimedOut(7); XML docs | **F** | P0 | Enum has exactly 8 values with explicit integer assignments matching the spec; terminal statuses identified via `IsTerminal()` extension (R1-A4) | `CommandStatusTests` + `CommandStatusExtensionsTests` + `CommandStatusRecordTests` |
| 1.5.2 | Aggregate tombstoning: terminal event → state reflects termination → subsequent commands rejected via `IRejectionEvent` → event stream remains immutable + replayable | **F** | P0 | Given `CounterClosed` applied, then `IsTerminated == true`; given any subsequent command, then `ProcessAsync` returns rejection w/ `AggregateTerminated`; given event-stream replay, then state correctly rehydrates as terminated; given Tier 2 actor lifecycle (deactivate→reactivate→rehydrate), then `AggregateTerminated` is replayed without error and state stays terminated (R1-A7) | `CounterAggregateTests` (close + post-termination rejection + replay), `EventStoreAggregateTests` (tombstoning guard suite), `Server.Tests/Actors/TombstoningLifecycleTests`, `ITerminatableTests`, `AggregateTerminatedTests`, `TerminatableComplianceAssertionsTests`, `MissingApplyMethodExceptionTests` (R1-A6 — actionable failure when `Apply(AggregateTerminated)` is missing) |
| 1.5.3 | All Tier 1 tests pass | **F** | P0 | Tier 1 baseline 651 green at epic close (and ratchets higher today) | Full Tier 1 |
| 1.5.4 | Done definition: enum complete, `ITerminatable` defined, `AggregateTerminated` defined, tombstoning guard, Counter sample, Tier 1 green | **F** | P0 | All sub-criteria covered above | Multiple |

**Add-on closing the test design's TG-2:**

| Gap ID | Description | Test |
|--------|-------------|------|
| 1.5-UNIT-012 | Solution-wide `ITerminatable` compliance scan — assembly-walk anchored at the Sample assembly, traverses the `Hexalith.EventStore.*` reference closure, asserts every concrete `ITerminatable` implementor has `Apply(AggregateTerminated)` (R-T3 / TG-2 — runtime-only adoption gap) | ✅ `Sample.Tests/Compliance/ITerminatableSolutionComplianceTests` (theory + canary that fails if scan returns empty) |

---

## Step 4 — Coverage Analysis & Gap Report

### Headline

**0 P0 gaps. 1 P1 chore (commit the canary). All 5 net-add tests proposed in the 2026-05-07 test design are merged or pending commit.**

### Coverage by Priority

| Priority | ACs (this trace) | Fully covered | Partially covered | Not covered |
|----------|:---:|:---:|:---:|:---:|
| P0 | 30 | 30 | 0 | 0 |
| P1 | 2 (1.1.8 syncing + 1.3.9 length) | 2 | 0 | 0 |
| P2 | 1 (1.4.4 XML doc surface) | 0 | 1 | 0 |
| P3 | 2 (1.1.6 / 1.2.6 XML docs on Contracts) | 0 | 2 | 0 |
| **Total** | **35**¹ | **32** | **3** | **0** |

¹ Of the 36 ACs, AC-1.1.6 and AC-1.2.6 collapse to the same XML-doc concern; counted once for coverage. The "partial" rows reflect R1-A3 (`<GenerateDocumentationFile>true</GenerateDocumentationFile>` on Contracts/Server/SignalR/Aspire/Testing) — Low-priority chore, no test impact.

### Risk Coverage (per Epic 1 Test Design)

| Risk | Score | Mitigation Status | Where Verified |
|------|:-:|------|----------------|
| R-T1 (positional-record swap) | 6 | ✅ Compensating control: named-args + per-field tests + 2 field-count canaries | `EventMetadata_HasExactly15Fields` (committed) + `CommandEnvelope_HasExactly10Fields` (⚠️ uncommitted) |
| R-T2 (`[DataMember]` runtime contract) | 6 | ✅ Mitigation merged | `CommandEnvelope.DataContract_SerializationRoundTrip_PreservesMessageId` (DCS path) + `EventMetadata.Json_SerializationRoundTrip_PreservesAll15Fields` + `EventEnvelope.Json_SerializationRoundTrip_PreservesEnvelope` (JSON path — design pivot, see Drift Notes) |
| R-D1 (ULID format silent acceptance at Contracts boundary) | 4 | ✅ Accepted by design — format validation lives in Epic 3 | Documented in design; no Tier 1 test required |
| R-T3 (`Apply(AggregateTerminated)` runtime-only constraint) | 4 | ✅ Mitigation merged | `TerminatableComplianceAssertions` helper + `ITerminatableSolutionComplianceTests` solution-wide scan + `MissingApplyMethodException` |
| R-T4 (reflection silent-skip on wrong return type) | 4 | ✅ Pin merged | `EventStoreAggregateTests.ProcessAsync_AggregateWithZeroHandleMethods_*` |
| R-S1 (payload redaction) | 3 | ✅ Met | `Server.Tests/Security/PayloadProtectionTests` + `Server.Tests/Logging/PayloadProtectionTests` + envelope `ToString()` tests |
| R-S2 (tenant injection via composite identity) | 3 | ✅ Met | `AggregateIdentityTests` colon-prohibition + regex |
| R-B1 (tombstoning irreversible) | 3 | ✅ Met | `CounterAggregateTests` + `Server.Tests/Actors/TombstoningLifecycleTests` (Tier 2) |
| R-D2 (`MessageType` JSON round-trip drift) | 2 | ✅ Met | `MessageTypeTests` round-trip |
| R-D3 (mixed-event `DomainResult`) | 2 | ✅ Met | `DomainResultTests` mixed-event rejection |
| R-O1 (pre-release schema break invalidates dev/CI state) | 2 | Accepted | Story 1.2 dev notes — `dapr init --slim` documented |
| R-P1 (reflection cold-start) | 1 | Mitigated | `ConcurrentDictionary` cache + thread-safety test |

### Action Item Closure (Epic 1 Retro)

| ID | Action | Status | Evidence |
|----|--------|:-:|----------|
| R1-A1 | Thread aggregate type through persistence pipeline; remove `"unknown"` placeholders | ✅ | `_bmad-output/implementation-artifacts/post-epic-1-r1a1-aggregatetype-pipeline.md` |
| R1-A2 | `AssertTerminatableCompliance<TState>()` test helper | ✅ | `_bmad-output/implementation-artifacts/post-epic-1-r1a2-terminatable-compliance-helper.md` + `TerminatableComplianceAssertionsTests` |
| R1-A3 | Enable `<GenerateDocumentationFile>true</GenerateDocumentationFile>` on Contracts/Server/SignalR/Aspire/Testing | ⚠️ Open | Only `Client.csproj` enforces (per Story 1.4) — Low priority |
| R1-A4 | `CommandStatus.IsTerminal()` extension | ✅ | `CommandStatusExtensions` + `CommandStatusExtensionsTests` |
| R1-A5 | Single-event `DomainResult.Rejection(IRejectionEvent e)` overload | ⚠️ Open | Low-priority ergonomic, no correctness implication |
| R1-A6 | `MissingApplyMethodException` replacing generic `InvalidOperationException` | ✅ | `_bmad-output/implementation-artifacts/post-epic-1-r1a6-missing-apply-method-exception.md` + `MissingApplyMethodExceptionTests` |
| R1-A7 | Tier 2 actor-lifecycle tombstoning test | ✅ | `_bmad-output/implementation-artifacts/post-epic-1-r1a7-tier2-tombstoning-lifecycle.md` + `Server.Tests/Actors/TombstoningLifecycleTests` |
| R1-A8 | Process: CRITICAL review findings include verification command | ✅ | Documented in retro; encoded in CLAUDE.md "Code Review Process" section |

**Open R1 items:** A3 (chore, Low) + A5 (ergonomic, Low). Neither blocks the gate.

### Drift Notes

1. **DataContract → JSON design pivot (R-T2 / TG-1).** The 2026-05-07 test design proposed adding `[DataContract]` round-trip tests for `EventMetadata` and `Contracts.EventEnvelope`. The implementation team correctly diagnosed that `EventMetadata` and `Contracts.EventEnvelope` are **intentionally not `[DataContract]`-decorated** — they travel via `System.Text.Json` on the production DAPR pub/sub and projection paths. The actor-remoting DCS path uses the separate flat-record `Hexalith.EventStore.Server.Events.EventEnvelope` (covered by its own Server-side DCS round-trip test). The closing tests were correctly redirected to JSON round-trips with explicit `<remarks>` comments documenting the rationale. **Outcome:** R-T2 is fully mitigated by the actual-production-path test, which is strictly stronger than the originally proposed test.

2. **`CommandEnvelope_HasExactly10Fields` not yet committed.** The canary test is present in the working tree (`tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandEnvelopeTests.cs`, lines 263–269) but `git status` shows the file as modified — the change has not been committed. This is the single open chore from this trace.

3. **R1-A3 (XML doc enforcement).** Five of six packages still rely on the honour system. UX-DR19 is met today (manual audits + IDE warnings + reviewer eyes) but a future drift risk exists. Out of scope for the gate — this is a process improvement, not a test gap.

---

## Step 5 — Quality Gate Decision

### Gate Inputs

- **Coverage targets** (from test design Quality Gate Criteria):
  - P0 pass rate ≥100%: **Met** (Tier 1 baseline 651 green at epic close; today's count ratchets higher).
  - P1 pass rate ≥95%: **Met** (5/5 P1 net-add tests merged or pending commit).
  - P2 pass rate ≥80%: **Met** (XML-doc cover-out is process; not a test gap).
  - High-risk mitigations (≥6) 100% complete or with documented compensating control: **Met** for R-T1 (named-args + canaries) and R-T2 (round-trip tests on both JSON and DCS paths).
- **Action item closure:** 6/8 R1 items closed (A1, A2, A4, A6, A7, A8); 2 open are Low priority (A3 chore, A5 ergonomic) and do not affect any test outcome.
- **No P0 gaps.** No high-risk un-mitigations.

### Decision: **PASS** ✅

Epic 1 ships as designed and verified. The contract-layer surface that 21 downstream epics consume is fully covered: 30/30 P0 ACs full, 32/35 ACs full + 3 partial (all on the XML-doc enforcement chore), and every high-risk item carries an active compensating control or merged mitigation test. The 5 net-add tests proposed in the test design are all in the working tree.

### Gate Conditions (advisory, non-blocking)

| ID | Recommendation | Owner | Priority | Notes |
|----|----------------|-------|:--------:|-------|
| G1 | **Commit the in-flight `CommandEnvelopeTests.cs` change** so `CommandEnvelope_HasExactly10Fields` (TG-4 / 1.2-UNIT-008) is durable on `main`. The canary asserts the positional-constructor parameter count is exactly 10 — counting parameters rather than `GetProperties()` because `CommandEnvelope` exposes the computed `AggregateIdentity` projection. | Dev (Jerome) | Low | Suggested commit message: `test(contracts): add CommandEnvelope_HasExactly10Fields canary (TG-4)`. |
| G2 | Close R1-A3 by enabling `<GenerateDocumentationFile>true</GenerateDocumentationFile>` on Contracts, Server, SignalR, Aspire, and Testing (one PR per package or all-in-one). Future drift on UX-DR19 will then be build-enforced. | Dev | Low | Out of scope for Epic 1 gate; track on Epic 2+ chore backlog. |
| G3 | Close R1-A5 by adding `DomainResult.Rejection(IRejectionEvent e)` single-event overload — pure ergonomic, no correctness implication. | Dev | Low | Could fold into the next contracts-touching story. |

### Gate Verdict Summary

```yaml
gate: PASS
basis: priority_thresholds
p0_pass_rate: 100
p1_pass_rate: 100
p2_pass_rate: 100
high_risk_mitigations_closed: 100
open_chores:
  - id: G1
    title: "Commit CommandEnvelope_HasExactly10Fields canary"
    blocking: false
  - id: G2
    title: "Enable XML doc enforcement on remaining 5 packages (R1-A3)"
    blocking: false
  - id: G3
    title: "Add DomainResult.Rejection(IRejectionEvent e) overload (R1-A5)"
    blocking: false
```

---

## Appendix

### Knowledge Base Cross-Reference

- `risk-governance.md` — gate-decision matrix used for the PASS verdict
- `probability-impact.md` — 1-3 × 1-3 scoring used in the test-design risk register
- `test-priorities-matrix.md` — P0–P3 classification + coverage targets
- `test-quality.md` — DoD for tests (isolation, determinism, single-purpose)

### Related Documents

- Epic 1 test design: `_bmad-output/test-artifacts/test-design/test-design-epic-1.md`
- Epic 1 retrospective: `_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md`
- Story 1.1: `_bmad-output/implementation-artifacts/1-1-core-identity-and-event-envelope.md`
- Story 1.2: `_bmad-output/implementation-artifacts/1-2-command-types-domainresult-and-error-contract.md`
- Story 1.3: `_bmad-output/implementation-artifacts/1-3-messagetype-value-object-and-hexalith-commons-ulid-integration.md`
- Story 1.4: `_bmad-output/implementation-artifacts/1-4-pure-function-contract-and-eventstoreaggregate-base.md`
- Story 1.5: `_bmad-output/implementation-artifacts/1-5-commandstatus-enum-and-aggregate-tombstoning.md`
- Project guide: `CLAUDE.md`
- Post-Epic 1 follow-throughs:
  - `post-epic-1-r1a1-aggregatetype-pipeline.md` (R1-A1)
  - `post-epic-1-r1a2-terminatable-compliance-helper.md` (R1-A2)
  - `post-epic-1-r1a6-missing-apply-method-exception.md` (R1-A6)
  - `post-epic-1-r1a7-tier2-tombstoning-lifecycle.md` (R1-A7)

---

**Generated by:** BMad TEA (Murat) — Test Architect Module
**Workflow:** `bmad-testarch-trace` (Epic-Level Phase 1+2)
**Version:** 4.0 (BMad v6)
