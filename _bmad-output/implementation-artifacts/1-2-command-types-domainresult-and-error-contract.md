# Story 1.2: Command Types, DomainResult & Error Contract

Status: ready-for-dev

## Story

As a domain service developer,
I want typed command envelopes and a domain result contract,
So that I can return events (including rejection events) from my pure function without throwing exceptions.

## Acceptance Criteria

1. **CommandEnvelope** contains `MessageId` (ULID string), `AggregateId` (ULID string), `CommandType` (string), `TenantId` (string), and `Payload` (byte[]). MessageId is the unique command identity and idempotency key (FR49, D16); CorrelationId is a separate field for request tracing that defaults to MessageId when not provided (FR4).

2. **DomainResult** contains an immutable list of event payloads (`IReadOnlyList<IEventPayload>`), with `IsSuccess`, `IsRejection`, `IsNoOp` classification. Mixed results (both regular and rejection events) are rejected at construction.

3. Rejection events implement `IRejectionEvent` marker interface and follow past-tense negative naming convention (Rule 8): e.g., `InsufficientFundsDetected`, `OrderRejected`.

4. All ULID fields (`MessageId`, `AggregateId`) are `string`-typed, generated via `UniqueIdHelper.GenerateSortableUniqueStringId()` (D12).

5. `CommandEnvelope.ToString()` redacts Payload (SEC-5, Rule 5).

6. All public types have XML documentation (UX-DR19).

7. All existing and new Tier 1 tests pass.

8. **Done definition:** MessageId field present on CommandEnvelope with validation, DomainResult and IRejectionEvent verified complete, all downstream projects compile, all Tier 1 tests green (`dotnet build Hexalith.EventStore.slnx` + all Tier 1 test projects).

## Tasks / Subtasks

- [ ] Task 1: Add `MessageId` field to CommandEnvelope (AC: #1, #4, #5)
  - [ ] 1.1 Add `MessageId` (string, ULID) as first positional parameter in CommandEnvelope record. MessageId needs non-empty validation, so follow the **CorrelationId pattern**: use an explicit property declaration with `[DataMember]` attribute and validation logic (not `[property: DataMember]` on the positional param). Without `[DataMember]`, MessageId vanishes during DAPR actor state serialization
  - [ ] 1.2 Add non-empty validation for MessageId (same pattern as CorrelationId)
  - [ ] 1.3 Update ToString() to include MessageId (keep Payload redacted)
  - [ ] 1.4 Verify Extensions defensive copy still works
- [ ] Task 2: Update SubmitCommand to include MessageId (AC: #1)
  - [ ] 2.1 Add `MessageId` field to `SubmitCommand` record in `src/Hexalith.EventStore.Server/Pipeline/Commands/SubmitCommand.cs`
  - [ ] 2.2 Update `SubmitCommandExtensions.ToCommandEnvelope()` to pass MessageId
  - [ ] 2.3 Fix CausationId derivation in `SubmitCommandHandler.cs` (line 25): change `string causationId = request.CorrelationId` to `string causationId = request.MessageId` — CausationId is the specific command that caused events, not the tracing ID
  - [ ] 2.4 Update `SubmitCommandHandler` logging to include MessageId in the `CommandReceived` log message
- [ ] Task 3: Update SubmitCommandRequest API DTO (AC: #1)
  - [ ] 3.1 Add **required** `MessageId` (string) field to `SubmitCommandRequest` in `src/Hexalith.EventStore.CommandApi/Models/SubmitCommandRequest.cs` — client owns MessageId generation (ULID, idempotency key per FR49)
  - [ ] 3.2 Add **optional** `CorrelationId` (string?) field to `SubmitCommandRequest` — for cross-system tracing (FR4)
  - [ ] 3.3 In the mapping layer (controller or extension that converts SubmitCommandRequest → SubmitCommand), default `CorrelationId = request.CorrelationId ?? request.MessageId` (FR4, D16). No server-side MessageId generation — missing MessageId is a 400 validation error (FR2)
- [ ] Task 4: Update CommandEnvelopeBuilder in Testing project (AC: #1)
  - [ ] 4.1 Add `_messageId` field with ULID default
  - [ ] 4.2 Add `WithMessageId(string)` fluent method
  - [ ] 4.3 Update `Build()` to pass MessageId as named argument
- [ ] Task 5: Fix all construction sites, update tests, verify build (AC: #7, #8) — **Do NOT start until Tasks 1-4 are complete**
  - [ ] 5.1 Grep `new CommandEnvelope(` **AND** `new SubmitCommandRequest(` **AND** `new SubmitCommand(` across ALL projects: `src/`, `tests/`, `samples/`. Also grep `typeof(CommandEnvelope)` and `nameof(CommandEnvelope)` to catch reflection-based usage
  - [ ] 5.2 Update all `new CommandEnvelope(` callers — use **named arguments** (positional record swap safety)
  - [ ] 5.3 Update all `new SubmitCommandRequest(` callers — add MessageId + CorrelationId
  - [ ] 5.4 Update all `new SubmitCommand(` callers — add MessageId
  - [ ] 5.5 Update `SubmitCommandExtensions.ToCommandEnvelope()` in Server
  - [ ] 5.6 Update `CommandEnvelopeTests.cs` — all construction sites need MessageId parameter
  - [ ] 5.7 Add MessageId validation test: empty and whitespace must throw `ArgumentException`
  - [ ] 5.8 Add test verifying MessageId appears in ToString() output
  - [ ] 5.9 Add DataContract serialization roundtrip test for CommandEnvelope to `Contracts.Tests` — verifies `[DataMember]` on MessageId survives serialize/deserialize cycle (no Tier 1 serialization coverage currently exists)
  - [ ] 5.10 Update `tests/Hexalith.EventStore.Testing.Tests/Builders/CommandEnvelopeBuilderTests.cs` — builder tests must verify MessageId field + WithMessageId()
  - [ ] 5.11 Update Server.Tests (direct construction): `PayloadProtectionTests.cs`, `TenantInjectionPreventionTests.cs`, `SecurityAuditLoggingTests.cs`, `EndToEndTraceTests.cs`, `DataPathIsolationTests.cs`, `DomainServiceIsolationTests.cs`, `DaprDomainServiceInvokerTests.cs`, `CausationIdLoggingTests.cs`, `FakeDeadLetterPublisherTests.cs`, `Logging/PayloadProtectionTests.cs`
  - [ ] 5.12 Verify Server.Tests (builder-based) compile: `DaprSerializationRoundTripTests.cs`, `SnapshotIntegrationTests.cs`, `AggregateActorIntegrationTests.cs`, `ActorConcurrencyConflictTests.cs`, `EventPersistenceIntegrationTests.cs`, `ActorTenantIsolationTests.cs`, `CommandRoutingIntegrationTests.cs`, `FakeDomainServiceInvokerTests.cs`
  - [ ] 5.13 Update Client.Tests: `EventStoreAggregateTests.cs`
  - [ ] 5.14 Update Sample.Tests: `CounterProcessorTests.cs`
  - [ ] 5.15 Update IntegrationTests: `CommandEnvelopeSerializationTests.cs`, `ValidationTests.cs`, `ConcurrencyConflictIntegrationTests.cs`
  - [ ] 5.16 Verify `dotnet build Hexalith.EventStore.slnx --configuration Release` succeeds with zero warnings — **must build ALL test projects (Tier 1, 2, and 3) to catch compilation errors even in tests that won't be executed**
  - [ ] 5.17 Run ALL Tier 1 test projects (Contracts.Tests, Client.Tests, Sample.Tests, Testing.Tests)
- [ ] Task 6: Verify DomainResult + IRejectionEvent regressions (AC: #2, #3) — run-only, no code changes expected
  - [ ] 6.1 Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` — confirms DomainResult tests (13 tests), IRejectionEvent coverage via Counter sample, and all CommandEnvelope changes pass
  - [ ] 6.2 Verify Counter sample's `CounterCannotGoNegative` implements IRejectionEvent and follows past-tense negative naming (Rule 8)

## Dev Notes

### Scope Summary

Add `MessageId` (ULID string) to `CommandEnvelope`, propagate to `SubmitCommand`, `SubmitCommandRequest`, and `CommandEnvelopeBuilder`, fix CausationId derivation in `SubmitCommandHandler`, fix all ~40 construction sites across the solution (including SubmitCommandRequest and SubmitCommand callers), add DataContract serialization test, and update all tests. DomainResult and IRejectionEvent are already complete — regression verify only.

### Task Execution Order

Task 1 (CommandEnvelope) -> Task 2 (SubmitCommand + handler) -> Task 3 (API DTO) -> Task 4 (builder) -> **STOP: verify Tasks 1-4 compile locally** -> Task 5 (fix all downstream + tests) -> Task 6 (regression verification).

### Existing Implementation State

This is NOT greenfield — it's an **audit-and-complete** story. Most types exist and are tested.

| File | Status | Notes |
|------|--------|-------|
| `src/.../Commands/CommandEnvelope.cs` | **Needs update** | Has 9 fields, needs MessageId added as first param |
| `src/.../Results/DomainResult.cs` | Complete | IsSuccess/IsRejection/IsNoOp, factory methods, mixed-event rejection |
| `src/.../Events/IRejectionEvent.cs` | Complete | Marker interface extending IEventPayload |
| `src/.../Events/IEventPayload.cs` | Complete | Marker interface |
| `src/.../Results/DomainServiceWireResult.cs` | Complete | Wire-safe conversion with FromDomainResult() |
| `src/.../Commands/CommandStatus.cs` | Complete | 8-state enum (Received through TimedOut) |
| `src/.../Messages/MessageType.cs` | Complete | Kebab format validation `{domain}-{name}-v{ver}` |
| `src/.../Testing/Builders/CommandEnvelopeBuilder.cs` | **Needs update** | Add MessageId field + WithMessageId() |
| `src/.../Server/Commands/SubmitCommandExtensions.cs` | **Needs update** | ToCommandEnvelope() needs MessageId mapping |
| `src/.../Server/Pipeline/Commands/SubmitCommand.cs` | **Needs update** | Add MessageId field |
| `src/.../CommandApi/Models/SubmitCommandRequest.cs` | **Needs update** | Add optional MessageId field |

### MessageId vs CorrelationId Design

**Current state:** CommandEnvelope has `CorrelationId` but no `MessageId`. CorrelationId serves dual duty as both command identity/idempotency key and request tracing ID.

**Target state per architecture (D16, FR4, FR49):**
- `MessageId` — unique command identity, idempotency key, **client-generated** ULID. Required. Missing = 400 (FR2).
- `CorrelationId` — request tracing ID, **optional from client**. EventStore defaults to MessageId if not provided (FR4).
- The client's MessageId also becomes the Event's `CausationId` for command-to-event tracing.

**Ownership:** Client owns MessageId (commands from the client). EventStore owns CorrelationId defaulting (events in event store). No server-side MessageId generation.

**Why split:** A client may want to correlate multiple commands under one CorrelationId (e.g., saga pattern) while each command retains its own unique MessageId for idempotency.

**CausationId derivation change:** `SubmitCommandHandler.cs` currently sets `CausationId = CorrelationId`. After this story, `CausationId = MessageId` — the causation chain traces from the specific command (MessageId) that caused the events, not the tracing ID (CorrelationId).

**Client SDK note:** Client developers generate MessageId via `UniqueIdHelper.GenerateSortableUniqueStringId()` from `Hexalith.Commons.UniqueIds`, which is already a transitive dependency of the Contracts/Client packages (D12).

### DomainResult — No AggregateType Needed

The epics AC mentions "aggregate type (short kebab)" on DomainResult. The current implementation intentionally omits it:
- The aggregate actor already knows its type from registration (Rule 17, KebabConverter)
- The actor populates aggregate type in EventMetadata after the domain service returns (SEC-1)
- Carrying aggregateType on DomainResult would be redundant and create a validation surface for mismatches
- DomainServiceWireResult also omits it — consistent design

This is a **deliberate architecture decision**, not a gap.

### Positional Record Safety Warning

CommandEnvelope will have 10 string/byte[]/nullable parameters after adding MessageId. Positional records are **swap-vulnerable** — reordering string params compiles fine but silently corrupts data. At every construction site, **use named arguments**:

```csharp
// GOOD — named arguments prevent silent swap bugs
new CommandEnvelope(
    MessageId: messageId,
    TenantId: tenantId,
    Domain: domain,
    AggregateId: aggregateId,
    CommandType: commandType,
    Payload: payload,
    CorrelationId: correlationId,
    CausationId: causationId,
    UserId: userId,
    Extensions: extensions)

// BAD — positional args are fragile with 10 params
new CommandEnvelope(messageId, tenantId, domain, ...)
```

### High-Risk Construction Sites

**`SubmitCommandExtensions.ToCommandEnvelope()` (Server project, line 37)** — Production code that builds CommandEnvelope from SubmitCommand. Must add MessageId mapping. Currently maps CorrelationId from SubmitCommand.

**`CommandEnvelopeBuilder.Build()` (Testing project, line 68)** — Must add `_messageId` field with ULID default and pass as named argument.

**`CommandEnvelopeTests.cs` (Contracts.Tests)** — 12 construction sites. All need MessageId added as first named arg.

**Server.Tests (multiple files)** — ~20 construction sites across PayloadProtectionTests, TenantInjectionPreventionTests, SecurityAuditLoggingTests, EndToEndTraceTests, DataPathIsolationTests, DomainServiceIsolationTests, DaprDomainServiceInvokerTests, CausationIdLoggingTests, FakeDeadLetterPublisherTests.

### Downstream Impact Assessment

Adding MessageId to CommandEnvelope (a positional record) will break all existing construction sites. Search for:
- `new CommandEnvelope(` — all callers need updated parameter lists (use named args!)
- `new SubmitCommandRequest(` — API DTO also changes (MessageId added, CorrelationId added)
- `new SubmitCommand(` — MediatR command also changes (MessageId added)
- `typeof(CommandEnvelope)` / `nameof(CommandEnvelope)` — catch reflection-based usage
- CommandEnvelopeBuilder.Build() — update internal construction

**Full blast radius — grep ALL changed types across ALL of these:**
- `src/Hexalith.EventStore.Server/Commands/SubmitCommandExtensions.cs` — production caller
- `src/Hexalith.EventStore.Testing/Builders/CommandEnvelopeBuilder.cs` — builder construction
- `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandEnvelopeTests.cs` — 12 sites
- `tests/Hexalith.EventStore.Server.Tests/Security/` — PayloadProtectionTests, TenantInjectionPreventionTests, DomainServiceIsolationTests, DataPathIsolationTests, SecurityAuditLoggingTests
- `tests/Hexalith.EventStore.Server.Tests/Telemetry/EndToEndTraceTests.cs` — 3 sites
- `tests/Hexalith.EventStore.Server.Tests/Logging/` — PayloadProtectionTests, CausationIdLoggingTests
- `tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprDomainServiceInvokerTests.cs` — 6 sites
- `tests/Hexalith.EventStore.Server.Tests/Events/FakeDeadLetterPublisherTests.cs`
- `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs`
- `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterProcessorTests.cs` — 2 sites
- `tests/Hexalith.EventStore.IntegrationTests/Serialization/CommandEnvelopeSerializationTests.cs` — 3 sites
- `tests/Hexalith.EventStore.IntegrationTests/CommandApi/ValidationTests.cs` — constructs SubmitCommandRequest
- `tests/Hexalith.EventStore.IntegrationTests/CommandApi/ConcurrencyConflictIntegrationTests.cs` — constructs SubmitCommandRequest
- `tests/Hexalith.EventStore.Testing.Tests/Builders/CommandEnvelopeBuilderTests.cs` — tests the builder itself
- `tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeDomainServiceInvokerTests.cs` — uses builder
- `tests/Hexalith.EventStore.Server.Tests/` (builder-based, compile-verify only): `DaprSerializationRoundTripTests.cs`, `SnapshotIntegrationTests.cs`, `AggregateActorIntegrationTests.cs`, `ActorConcurrencyConflictTests.cs`, `EventPersistenceIntegrationTests.cs`, `ActorTenantIsolationTests.cs`, `CommandRoutingIntegrationTests.cs`

### Architecture Constraints

- **Pre-release:** No production data exists. Dev state stores can be wiped (`dapr init --slim` to reset). No backward-compat shim needed. **Note:** Any local DAPR state stores with serialized CommandEnvelopes from previous Tier 2/3 test runs will fail to deserialize after the schema change — run `dapr init --slim` before Tier 2 testing.
- **SEC-1:** EventStore owns ALL metadata fields. Domain services return ONLY event payloads. DomainResult carries events only — server enriches with full 15-field EventMetadata.
- **D3:** Domain errors as events; infrastructure errors as exceptions. DomainResult always returns events, never throws for domain logic. IRejectionEvent distinguishes rejection from state-change.
- **D12:** All ULID fields are `string`-typed, generated via `Hexalith.Commons.UniqueIds.UniqueIdHelper`.
- **D16:** Ultra-thin client command: messageId, aggregateId, commandType, payload, optional correlationId.
- **FR49:** Duplicate detection by tracking processed command MessageIds per aggregate.
- **Rule 8:** Events named in past tense (state-change) or past-tense negative (rejection).

### Deferred Items

- **FluentValidation rule for MessageId ULID format** — FR2 requires valid ULID format validation on MessageId. This is deferred to Epic 3 (Story 3.2: Command Validation & 400 Error Responses) where all FluentValidation rules are implemented. This story only adds the field and non-empty validation.

### Standards

- **Braces:** Current CommandEnvelope uses Egyptian/K&R for records — follow existing pattern.
- **Tests:** `CommandEnvelopeTests.cs` uses `Assert.Equal` (xUnit). `DomainResultTests.cs` uses `Assert.Equal` (xUnit). Don't mix styles within a file.
- **Run:** `dotnet test tests/Hexalith.EventStore.Contracts.Tests/`

### Previous Story Intelligence (Story 1.1)

Story 1.1 added 4 fields to `EventMetadata` (a positional record) with the same blast-radius pattern. Key learnings:
- Named arguments are critical at every construction site to prevent swap bugs
- Grep across ALL projects (`src/`, `tests/`, `samples/`) — don't miss any callers
- Update test field-count assertions (e.g., `HasExactlyNFields` tests)
- EventEnvelopeBuilder needed matching `With*()` methods and updated `Build()` — same pattern applies to CommandEnvelopeBuilder
- Run `dotnet build Hexalith.EventStore.slnx --configuration Release` to catch ALL compilation errors before running tests

### Project Structure Notes

- All contract types live in `src/Hexalith.EventStore.Contracts/` under appropriate subdirectories (Commands/, Results/, Events/, Messages/)
- Testing builders live in `src/Hexalith.EventStore.Testing/Builders/`
- API DTOs live in `src/Hexalith.EventStore.CommandApi/Models/`
- Server pipeline commands live in `src/Hexalith.EventStore.Server/Pipeline/Commands/`

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Epic 1, Story 1.2]
- [Source: _bmad-output/planning-artifacts/architecture.md — D3, D12, D16, SEC-1, FR4, FR49, Rule 8]
- [Source: _bmad-output/planning-artifacts/prd.md — FR1, FR2, FR4, FR21, FR23, FR49]
- [Source: src/Hexalith.EventStore.Contracts/Commands/CommandEnvelope.cs — current 9-field implementation]
- [Source: src/Hexalith.EventStore.Contracts/Results/DomainResult.cs — complete implementation]
- [Source: src/Hexalith.EventStore.Contracts/Events/IRejectionEvent.cs — complete implementation]
- [Source: _bmad-output/implementation-artifacts/1-1-core-identity-and-event-envelope.md — previous story blast-radius learnings]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
