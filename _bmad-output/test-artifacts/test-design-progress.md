---
workflowStatus: 'completed'
totalSteps: 5
stepsCompleted: ['step-01-detect-mode', 'step-02-load-context', 'step-03-risk-and-testability', 'step-04-coverage-plan', 'step-05-generate-output']
lastStep: 'step-05-generate-output'
nextStep: ''
outputFile: '_bmad-output/test-artifacts/test-design/test-design-epic-1.md'
lastSaved: '2026-05-07'
mode: epic-level
scope: 'Epic 1 — Domain Contract Foundation'
inputDocuments:
  - _bmad-output/implementation-artifacts/1-1-core-identity-and-event-envelope.md
  - _bmad-output/implementation-artifacts/1-2-command-types-domainresult-and-error-contract.md
  - _bmad-output/implementation-artifacts/1-3-messagetype-value-object-and-hexalith-commons-ulid-integration.md
  - _bmad-output/implementation-artifacts/1-4-pure-function-contract-and-eventstoreaggregate-base.md
  - _bmad-output/implementation-artifacts/1-5-commandstatus-enum-and-aggregate-tombstoning.md
  - _bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md
  - _bmad-output/planning-artifacts/epics.md
  - .claude/skills/bmad-testarch-test-design/resources/knowledge/risk-governance.md
  - .claude/skills/bmad-testarch-test-design/resources/knowledge/probability-impact.md
  - .claude/skills/bmad-testarch-test-design/resources/knowledge/test-levels-framework.md
  - .claude/skills/bmad-testarch-test-design/resources/knowledge/test-priorities-matrix.md
testStackType: backend
ciPlatform: github-actions
testFramework: 'xUnit + Shouldly + NSubstitute (Tier 1) / xUnit + DAPR (Tier 2) / Aspire host (Tier 3)'
---

# Test Design Progress — Epic 1 (Domain Contract Foundation)

## Step 1: Mode Detection & Prerequisites

**Mode:** Epic-Level Test Design
**Scope:** Epic 1 — Domain Contract Foundation (retroactive coverage / risk audit; epic status = done)
**Reason:**

- User explicit intent: "epic 1"
- File-based detection: `_bmad-output/implementation-artifacts/sprint-status.yaml` exists → Epic-Level Mode default
- All 21 epics already `done`; this is a retroactive risk-based coverage audit, not a forward-looking plan

### Stories in Scope

| ID | Title | Status |
|----|-------|--------|
| 1-1 | core-identity-and-event-envelope | done |
| 1-2 | command-types-domainresult-and-error-contract | done |
| 1-3 | messagetype-value-object-and-hexalith-commons-ulid-integration | done |
| 1-4 | pure-function-contract-and-eventstoreaggregate-base | done |
| 1-5 | commandstatus-enum-and-aggregate-tombstoning | done |
| epic-1-retrospective | done | |

### Prerequisites Confirmed

- Epic-1 story specs (5 files) at `_bmad-output/implementation-artifacts/1-{1..5}-*.md`
- Epic 1 retrospective: `_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md`
- Epics index: `_bmad-output/planning-artifacts/epics.md`
- Source code: `src/Hexalith.EventStore.Contracts/`
- Existing tests: `tests/Hexalith.EventStore.Contracts.Tests/` (Tier 1)

### Notes

- Prior system-level test design (2026-03-29) archived to `test-design/archive/test-design-progress-system-level-2026-03-29.md`
- Project-context.md not present — leaning on CLAUDE.md + Epic 1 retro for context

## Step 2: Context & Knowledge Loading

### Stack Detection

- `test_stack_type`: **backend** (`.csproj` everywhere; no `playwright.config.*`/`cypress.config.*` in project root; Admin.UI uses bUnit, no browser-driver E2E in Epic 1 scope)
- `tea_browser_automation: auto` — no browser exploration needed for Epic 1 (pure type/contract surface)
- `tea_use_playwright_utils: true` but **API-only profile** is the right fit — Epic 1 is .NET contract layer, no Playwright tests in scope
- `tea_use_pactjs_utils: true` — Pact is configured for the project but Epic 1 ships type contracts, not service contracts (no consumer/provider interactions yet)

### Knowledge Fragments Loaded (Epic-Level Required)

- ✅ `risk-governance.md` (core) — scoring matrix, gate decision rules, traceability
- ✅ `probability-impact.md` (core) — 1-3 × 1-3 = 1-9 scale, action thresholds (DOCUMENT/MONITOR/MITIGATE/BLOCK)
- ✅ `test-levels-framework.md` (core) — unit / integration / E2E selection rules
- ✅ `test-priorities-matrix.md` (core) — P0–P3 criteria, coverage targets

Skipped (out of scope for Epic 1 contract layer):
- Playwright/UI fragments (no browser tests)
- Webhook/contract-testing fragments (no service-to-service contracts in Epic 1)
- Pact MCP (defer to Epic 11+ projection contracts)

### Existing Test Coverage Inventory (Epic 1 surface)

**Tier 1 — `tests/Hexalith.EventStore.Contracts.Tests/`** (15 test files Epic-1-relevant):

| Surface | Test File | Story |
|---------|-----------|-------|
| AggregateIdentity | Identity/AggregateIdentityTests.cs | 1.1 |
| IdentityParser | Identity/IdentityParserTests.cs | 1.1 |
| UniqueIdHelper integration | Identity/UniqueIdHelperIntegrationTests.cs | 1.3 |
| EventMetadata | Events/EventMetadataTests.cs | 1.1 |
| EventEnvelope | Events/EventEnvelopeTests.cs | 1.1 |
| IEventPayload markers | Events/EventPayloadTests.cs | 1.2 |
| AggregateTerminated | Events/AggregateTerminatedTests.cs | 1.5 |
| ITerminatable | Aggregates/ITerminatableTests.cs | 1.5 |
| CommandEnvelope | Commands/CommandEnvelopeTests.cs | 1.2 |
| CommandStatus enum | Commands/CommandStatusTests.cs | 1.5 |
| CommandStatusRecord | Commands/CommandStatusRecordTests.cs | 1.5 |
| CommandStatusExtensions | Commands/CommandStatusExtensionsTests.cs | 1.5 R1-A4 ✅ |
| MessageType | Messages/MessageTypeTests.cs | 1.3 |
| DomainResult | Results/DomainResultTests.cs | 1.2 |

**Tier 1 — `tests/Hexalith.EventStore.Client.Tests/`** (Epic-1-relevant):

| Surface | Test File | Story |
|---------|-----------|-------|
| EventStoreAggregate (reflection dispatch + tombstoning guard) | Aggregates/EventStoreAggregateTests.cs | 1.4, 1.5 |
| MissingApplyMethodException | Aggregates/MissingApplyMethodExceptionTests.cs | 1.5 R1-A6 ✅ |
| EventStoreProjection (XML doc audit only) | Aggregates/EventStoreProjectionTests.cs | 1.4 (audit) |
| DomainProcessorBase | Handlers/DomainProcessorTests.cs | 1.4 |
| NamingConventionEngine | Conventions/NamingConventionEngineTests.cs | 1.4 |
| EventStoreDomainAttribute | Attributes/EventStoreDomainAttributeTests.cs | 1.4 |

**Tier 1 — `tests/Hexalith.EventStore.Testing.Tests/`** (Epic-1-relevant):

| Surface | Test File | Story |
|---------|-----------|-------|
| TerminatableComplianceAssertions | Compliance/TerminatableComplianceAssertionsTests.cs | 1.5 R1-A2 ✅ |

**Tier 2 — `tests/Hexalith.EventStore.Server.Tests/Actors/`** (Epic-1-relevant lifecycle):

| Surface | Test File | Source |
|---------|-----------|--------|
| Tombstoning lifecycle (deactivate→reactivate→replay) | TombstoningLifecycleTests.cs | 1.5 R1-A7 ✅ |

### Action-Item Carryover Status (per epic-1-retro)

| ID | Item | Severity | Status (2026-05-07) |
|----|------|----------|---------------------|
| R1-A1 | Thread `AggregateType` through pipeline (remove `"unknown"` placeholder) | High | ✅ DONE — `EventPersister.cs:85` uses real `AggregateType` parameter |
| R1-A2 | `AssertTerminatableCompliance<TState>()` test helper | High | ✅ DONE — `TerminatableComplianceAssertions.cs` shipped + tests |
| R1-A3 | Enable `<GenerateDocumentationFile>` on all 6 packages | Medium | ✅ DONE — `Directory.Build.props:35` solution-wide |
| R1-A4 | `CommandStatus.IsTerminal()` extension | Medium | ✅ DONE — shipped in `CommandStatusExtensions` |
| R1-A5 | Single-event `DomainResult.Rejection(IRejectionEvent e)` overload | **Low** | ❌ NOT SHIPPED — only `IReadOnlyList<IRejectionEvent>` exists |
| R1-A6 | Custom `MissingApplyMethodException` replacing generic `InvalidOperationException` | Medium | ✅ DONE — exception type + tests in Client |
| R1-A7 | Tier 2 actor lifecycle tombstoning test | High | ✅ DONE — `TombstoningLifecycleTests.cs` |
| R1-A8 | Process: CRITICAL findings include verification command | Low | ✅ Documented in retro; informally applied (not code) |

**Outstanding:** Only **R1-A5** (Low priority — ergonomic-only, no correctness implication).

## Step 3: Risk & Testability Assessment

> Mode is Epic-Level (already-shipped epic). System-level testability review skipped per workflow rules. Going straight to risk assessment.

### Risk Matrix — Epic 1 Domain Contract Foundation

Scoring: Probability × Impact (1–3 × 1–3 = 1–9). Action thresholds: 1–3 DOCUMENT, 4–5 MONITOR, 6–8 MITIGATE, 9 BLOCK.

| ID | Cat | Risk | Reasoning | P | I | Score | Action | Status |
|----|-----|------|-----------|---|---|-------|--------|--------|
| R-T1 | TECH | **Positional-record swap vulnerability** on `EventMetadata` (15 params) and `CommandEnvelope` (10 params). Reordering same-typed args silently corrupts data; compiler stays green. | Mitigated only by named-argument discipline + per-field assertion tests. New construction sites added by future stories re-introduce the vector. | 2 | 3 | **6** | MITIGATE | OPEN — process & test rule, not code |
| R-T2 | TECH | **`[DataMember]` runtime contract** on positional records. MessageId on `CommandEnvelope` uses explicit property + `[DataMember]` because `[property: DataMember]` on a positional param vanishes during DAPR actor-state serialization. No compile-time enforcement. | Only Tier 1 `DataContract` serialization round-trip tests catch regressions. Story 1.2 added one for `CommandEnvelope`. None for `EventMetadata`/`EventEnvelope`. | 2 | 3 | **6** | MITIGATE | **Test gap** — see TG-1 |
| R-D1 | DATA | **ULID format silent acceptance.** Per D12, ULID fields are bare `string`. Contracts validates non-empty only; ULID-format validation is deferred to Epic 3 FluentValidation. Pre-validation, a GUID-shaped value or arbitrary string slips through and breaks downstream lex-sort + parsers. | `Ulid.TryParse` enforced only at API boundaries (Epic 3); inter-component callers (server → projection, sample tests) can construct envelopes with non-ULID strings. | 2 | 2 | **4** | MONITOR | OPEN — by design (Epic 3 owns) |
| R-T3 | TECH | **`Apply(AggregateTerminated)` runtime-only constraint.** Any state implementing `ITerminatable` must also have a no-op `Apply(AggregateTerminated)` or actor reactivation throws. | Now mitigated by `TerminatableComplianceAssertions` (R1-A2 ✅) + `MissingApplyMethodException` (R1-A6 ✅). Risk downgraded from HIGH to MONITOR — discoverable, but adoption is opt-in. | 2 | 2 | **4** | MONITOR | Downgraded — see TG-2 |
| R-T4 | TECH | **Reflection silent-skip on signature mismatch** in `EventStoreAggregate.Handle/Apply` discovery. Wrong return type silently yields "no matching method" failure at command time, not at startup. | Story 1.4 task 2.4 added an explicit "wrong return type silently skipped" test. But there's no startup-time validation that an aggregate has at least one Handle method or that all referenced commands have a Handle. | 2 | 2 | **4** | MONITOR | OPEN — see TG-3 |
| R-S1 | SEC | **ToString() payload-redaction coverage** for `Server.EventEnvelope` (separate flat record from `Contracts.EventEnvelope`). | `PayloadProtectionTests.cs` (both Server.Tests/Security and Server.Tests/Logging) covers both envelope types. | 1 | 3 | **3** | DOCUMENT | Covered |
| R-S2 | SEC | **Tenant injection via composite AggregateIdentity strings.** Colon-prohibition + regex validation stops `tenant:domain:id` collisions. | Validated in `AggregateIdentityTests.cs`. | 1 | 3 | **3** | DOCUMENT | Covered |
| R-B1 | BUS | **Tombstoning is irreversible** — accidental terminal-event emission has no undo. | Counter sample tests cover the close path; tombstoning guard test asserts post-termination commands are rejected. Per design (D3 / Rule 11). | 1 | 3 | **3** | DOCUMENT | Covered |
| R-D2 | DATA | **MessageType serialization round-trip drift.** Custom `JsonConverter<MessageType>` writes `ToString()` and reads via `Parse()`. KebabConverter or regex change could silently break `Assemble` while keeping `Parse` green. | Story 1.3 includes round-trip tests; KebabConverter is stable. | 1 | 2 | **2** | DOCUMENT | Covered |
| R-D3 | DATA | **Mixed-event DomainResult.** Constructor rejects mixed regular/rejection lists; once constructed, list is `IReadOnlyList<IEventPayload>` (immutable). | `DomainResultTests.cs` covers mixed-rejection path. | 1 | 2 | **2** | DOCUMENT | Covered |
| R-O1 | OPS | **Pre-release schema-break invalidates DAPR dev/CI state stores.** Each `EventMetadata`/`CommandEnvelope` field add forces `dapr init --slim` before Tier 2 testing. | Documented in story 1.2 dev notes; pre-release status accepted (no prod data). | 2 | 1 | **2** | DOCUMENT | Accepted |
| R-P1 | PERF | **Reflection on every command** — mitigated by `ConcurrentDictionary` metadata cache. | `NamingConventionEngineTests.cs` + `EventStoreAggregateTests.cs` cover thread-safety + cache. | 1 | 1 | **1** | DOCUMENT | Covered |

### Risk Summary

- **MITIGATE (score 6–8):** 2 risks — R-T1 (positional swap), R-T2 (DataMember runtime contract)
- **MONITOR (score 4–5):** 3 risks — R-D1 (ULID format), R-T3 (ITerminatable Apply), R-T4 (reflection silent-skip)
- **DOCUMENT (score 1–3):** 7 risks — all currently covered or accepted by design
- **BLOCK (score 9):** 0 risks ✅
- **No CRITICAL findings.** Epic 1 is currently shippable (and shipped). Two MITIGATE risks are reviewer/test-rule items, not code defects.

### Test-Gap (TG) Identification

Translating MITIGATE/MONITOR risks into actionable test gaps for the coverage plan in Step 4:

| TG | Risk | Gap | Proposed Test Level |
|----|------|-----|---------------------|
| TG-1 | R-T2 | No `DataContract` serialization round-trip test for `EventMetadata` (15 fields) and `Contracts.EventEnvelope`. Only `CommandEnvelope` has one (Story 1.2). | Tier 1 Unit (Contracts.Tests) |
| TG-2 | R-T3 | `TerminatableComplianceAssertions` ships, but **no test pipeline rule** ensures *every* `ITerminatable` implementation in the solution is checked. Counter sample tests call it; new domains may not. | Tier 1 architectural test (e.g., scan-all-implementors test) |
| TG-3 | R-T4 | No startup-time validation that an `EventStoreAggregate<TState>` exposes ≥1 `Handle` method, and no test that aggregates fail registration cleanly when zero Handle methods are declared. | Tier 1 Unit (Client.Tests) |
| TG-4 | R-T1 | Process rule, not test gap — but a **field-count canary** test (`EventMetadata_HasExactly15Fields`, `CommandEnvelope_HasExactly10Fields`) keeps inadvertent additions visible. Already exists for EventMetadata; verify CommandEnvelope. | Tier 1 Unit (Contracts.Tests) — verify |
| TG-5 | R-D1 | No Contracts-level negative test that bare-string non-ULID values are at least non-empty (current behavior). Validates the *current* contract; will need rework when Epic 3 adds format validation. | Documentation test (assert current behavior) |

## Step 4: Coverage Plan & Execution Strategy

### Atomic Test Scenarios — Risk-Driven (Test ID format `1.{story}-{level}-{seq}`)

#### Story 1.1 — Core Identity & Event Envelope

| Test ID | Scenario | Risk | Level | Priority | Existing? |
|---------|----------|------|-------|----------|-----------|
| 1.1-UNIT-001 | `EventMetadata` 15-field count canary (regression on field add/reorder) | R-T1 | Unit | P0 | ✅ `EventMetadata_HasExactly15Fields` |
| 1.1-UNIT-002 | `EventMetadata` per-field round-trip with named-arg construction | R-T1 | Unit | P0 | ✅ `Metadata_ExposesAll15Fields` |
| 1.1-UNIT-003 | `MetadataVersion < 1` throws `ArgumentOutOfRangeException` | DATA validation | Unit | P0 | ✅ |
| 1.1-UNIT-004 | `GlobalPosition < 0` throws `ArgumentOutOfRangeException` | DATA validation | Unit | P0 | ✅ |
| 1.1-UNIT-005 | Each non-empty string field rejects null/whitespace at construction | DATA validation | Unit | P0 | ✅ |
| 1.1-UNIT-006 | `EventEnvelope.ToString()` redacts payload, includes all 15 metadata fields | R-S1 | Unit | P0 | ✅ |
| 1.1-UNIT-007 | `Extensions` dictionary defensively copied (mutating original doesn't affect envelope) | DATA immutability | Unit | P1 | ✅ |
| 1.1-UNIT-008 | `AggregateIdentity.Parse` rejects colon in tenant/domain/id components | R-S2 | Unit | P0 | ✅ |
| 1.1-UNIT-009 | `AggregateIdentity` derived keys (ActorId, EventStreamKeyPrefix, MetadataKey, SnapshotKey, PubSubTopic) are correct | DATA contract | Unit | P0 | ✅ |
| **1.1-UNIT-010** | **`DataContract` round-trip for `EventMetadata` (DAPR actor-state serialization survives all 15 fields)** | **R-T2 / TG-1** | **Unit** | **P1** | **❌ GAP — only `CommandEnvelope` has this** |
| **1.1-UNIT-011** | **`DataContract` round-trip for `Contracts.EventEnvelope` (envelope + metadata + payload + extensions)** | **R-T2 / TG-1** | **Unit** | **P1** | **❌ GAP** |
| 1.1-UNIT-012 | `Server.EventEnvelope.ToString()` payload redaction (separate flat record) | R-S1 | Unit | P0 | ✅ Server.Tests `PayloadProtectionTests` |

#### Story 1.2 — Command Types, DomainResult & Error Contract

| Test ID | Scenario | Risk | Level | Priority | Existing? |
|---------|----------|------|-------|----------|-----------|
| 1.2-UNIT-001 | `CommandEnvelope.MessageId` rejects null/whitespace | DATA validation | Unit | P0 | ✅ |
| 1.2-UNIT-002 | `CommandEnvelope.ToString()` includes MessageId, redacts Payload | R-S1 | Unit | P0 | ✅ |
| 1.2-UNIT-003 | `DataContract` round-trip for `CommandEnvelope` (MessageId `[DataMember]` survives DAPR) | R-T2 | Unit | P0 | ✅ |
| 1.2-UNIT-004 | `DomainResult.Success` / `IsRejection` / `IsNoOp` classification | BUS contract | Unit | P0 | ✅ `DomainResultTests` |
| 1.2-UNIT-005 | `DomainResult` rejects mixed (regular + rejection) at construction | R-D3 | Unit | P0 | ✅ |
| 1.2-UNIT-006 | `IRejectionEvent` marker recognized; rejection events route through rejection list | BUS contract | Unit | P0 | ✅ via `EventPayloadTests` + Counter |
| 1.2-UNIT-007 | `Counter` sample `CounterCannotGoNegative` follows past-tense negative naming (Rule 8) | BUS naming | Unit | P1 | ✅ `CounterAggregateTests` |
| **1.2-UNIT-008** | **`CommandEnvelope` 10-field count canary** | **R-T1 / TG-4** | **Unit** | **P1** | **❓ VERIFY — may not exist as explicit count test** |

#### Story 1.3 — MessageType & UniqueIdHelper Integration

| Test ID | Scenario | Risk | Level | Priority | Existing? |
|---------|----------|------|-------|----------|-----------|
| 1.3-UNIT-001 | `MessageType.Parse` valid `{domain}-{name}-v{ver}` returns correct components | BUS contract | Unit | P0 | ✅ |
| 1.3-UNIT-002 | `MessageType.Parse` rejects malformed (no version, empty, no hyphens, version=0, non-numeric, > 192 chars) | DATA validation | Unit | P0 | ✅ |
| 1.3-UNIT-003 | `MessageType.Assemble` PascalCase→kebab via `KebabConverter` (`TenantCreated` → `tenant-created`) | R-D2 | Unit | P0 | ✅ |
| 1.3-UNIT-004 | `MessageType.Assemble` rejects null domain, null type, version ≤ 0, > 192 chars | DATA validation | Unit | P0 | ✅ |
| 1.3-UNIT-005 | `MessageType` JSON round-trip (custom `JsonConverter` writes string, reads via Parse) | R-D2 | Unit | P0 | ✅ |
| 1.3-UNIT-006 | `MessageType.ToString()` round-trip: `Parse(mt.ToString()) == mt` | R-D2 | Unit | P0 | ✅ |
| 1.3-UNIT-007 | `UniqueIdHelper.GenerateSortableUniqueStringId()` produces 26-char Crockford Base32 | DATA contract | Unit | P0 | ✅ |
| 1.3-UNIT-008 | Sequential ULIDs maintain lexicographic order | DATA contract | Unit | P0 | ✅ |
| 1.3-UNIT-009 | `ExtractTimestamp` rejects empty/null/truncated/non-Base32 | DATA validation | Unit | P0 | ✅ |
| 1.3-UNIT-010 | `ToGuid` ↔ `ToSortableUniqueId` round-trip | DATA contract | Unit | P0 | ✅ |

#### Story 1.4 — IDomainProcessor / EventStoreAggregate / Conventions

| Test ID | Scenario | Risk | Level | Priority | Existing? |
|---------|----------|------|-------|----------|-----------|
| 1.4-UNIT-001 | `EventStoreAggregate.Handle` discovery by name + 2-arg shape + return type | BUS contract | Unit | P0 | ✅ `EventStoreAggregateTests` |
| 1.4-UNIT-002 | `Handle` discovery silently skips wrong return type (e.g., `string`) | R-T4 | Unit | P0 | ✅ Story 1.4 task 2.4 |
| 1.4-UNIT-003 | `Apply` discovery on TState by name + 1-arg + void return | BUS contract | Unit | P0 | ✅ |
| 1.4-UNIT-004 | Command dispatch deserializes `Payload` (byte[]) by `CommandType` | BUS contract | Unit | P0 | ✅ |
| 1.4-UNIT-005 | State rehydration: null, typed TState, JsonElement object, JsonElement array, null, IEnumerable | BUS contract | Unit | P0 | ✅ |
| 1.4-UNIT-006 | Metadata cache thread-safety (parallel discovery yields stable cache) | R-P1 | Unit | P1 | ✅ |
| 1.4-UNIT-007 | `NamingConventionEngine` PascalCase→kebab, suffix strip, attribute override, regex validation | BUS contract | Unit | P0 | ✅ 40+ tests |
| 1.4-UNIT-008 | `EventStoreDomainAttribute` non-empty, AllowMultiple=false, Inherited=false | BUS contract | Unit | P0 | ✅ |
| 1.4-UNIT-009 | `MissingApplyMethodException` thrown with state-type + event-type context | R-T3 / R1-A6 | Unit | P0 | ✅ `MissingApplyMethodExceptionTests` |
| **1.4-UNIT-010** | **Aggregate registration test: zero-Handle aggregate fails fast at `UseEventStore`/cascade resolution (or, if not, document allowed)** | **R-T4 / TG-3** | **Unit** | **P2** | **❌ GAP — silent-skip is by design but no zero-handler boundary test** |

#### Story 1.5 — CommandStatus Enum & Aggregate Tombstoning

| Test ID | Scenario | Risk | Level | Priority | Existing? |
|---------|----------|------|-------|----------|-----------|
| 1.5-UNIT-001 | `CommandStatus` has exactly 8 states with explicit int assignments | BUS contract | Unit | P0 | ✅ `CommandStatusTests` |
| 1.5-UNIT-002 | `CommandStatus.IsTerminal()` extension for Completed / Rejected / PublishFailed / TimedOut | BUS contract | Unit | P0 | ✅ `CommandStatusExtensionsTests` (R1-A4 ✅) |
| 1.5-UNIT-003 | `ITerminatable.IsTerminated` shape | BUS contract | Unit | P0 | ✅ |
| 1.5-UNIT-004 | `AggregateTerminated` is `IRejectionEvent`, has `AggregateType` + `AggregateId` | BUS contract | Unit | P0 | ✅ |
| 1.5-UNIT-005 | `EventStoreAggregate.ProcessAsync` rejects on `state is ITerminatable { IsTerminated: true }` | R-B1 | Unit | P0 | ✅ `EventStoreAggregateTests` |
| 1.5-UNIT-006 | Non-`ITerminatable` state types skip the guard (backward-compat) | R-T3 | Unit | P0 | ✅ |
| 1.5-UNIT-007 | Null state with `ITerminatable` state type processes normally (null is not `ITerminatable`) | R-T3 | Unit | P0 | ✅ |
| 1.5-UNIT-008 | Counter sample: `CloseCounter` produces `CounterClosed` event | BUS contract | Unit | P0 | ✅ `CounterAggregateTests` |
| 1.5-UNIT-009 | Counter sample: post-`CounterClosed` command emits `AggregateTerminated` rejection | R-B1 | Unit | P0 | ✅ |
| 1.5-UNIT-010 | Counter sample: rehydration replays `CounterClosed` + `AggregateTerminated`, state stays `IsTerminated == true` | R-T3 | Unit | P0 | ✅ |
| 1.5-UNIT-011 | `TerminatableComplianceAssertions.AssertTerminatableCompliance<TState>()` flags missing `Apply(AggregateTerminated)` | R-T3 / R1-A2 | Unit | P0 | ✅ |
| 1.5-INT-001 | Tier 2 actor-lifecycle tombstoning: deactivate → reactivate → replay preserves termination | R-T3 / R1-A7 | Integration | P0 | ✅ `TombstoningLifecycleTests` |
| **1.5-UNIT-012** | **Architectural test: every `ITerminatable` implementation in solution invokes `AssertTerminatableCompliance` (or has `Apply(AggregateTerminated)` reflectively present)** | **R-T3 / TG-2** | **Unit** | **P2** | **❌ GAP — Counter calls it; no solution-wide enforcement** |

### New / Improved Tests Recommended (Net Adds from Risk-Driven Gaps)

| New Test ID | What | Test Level | Priority | Rationale (risk → mitigation) |
|-------------|------|------------|----------|-------------------------------|
| 1.1-UNIT-010 | `DataContract` round-trip for `EventMetadata` | Tier 1 Unit | **P1** | Closes R-T2 / TG-1 — the silent `[DataMember]` trap is the hardest-to-debug class of bug across the project |
| 1.1-UNIT-011 | `DataContract` round-trip for `Contracts.EventEnvelope` | Tier 1 Unit | **P1** | Same. Pair with 1.1-UNIT-010. |
| 1.2-UNIT-008 | Verify/add `CommandEnvelope_HasExactly10Fields` canary | Tier 1 Unit | P1 | TG-4 / R-T1 — keeps the field count visible to reviewers when the next migration adds a field |
| 1.4-UNIT-010 | Zero-Handle aggregate boundary test (or explicit doc-test) | Tier 1 Unit | P2 | TG-3 / R-T4 — pin down current behavior so future framework refactors don't surprise |
| 1.5-UNIT-012 | Solution-wide `ITerminatable` compliance scan | Tier 1 Unit | P2 | TG-2 / R-T3 — closes adoption-gap on the runtime-only constraint |

> **Net adds: 5 tests, 2 of them P1.** No E2E, no API, no fixture work. Pure Tier 1 contract coverage.

### Coverage Targets (Epic 1 retroactive)

| Priority | Existing Coverage | Target | Status |
|----------|-------------------|--------|--------|
| P0 | ~95% (every AC in stories 1.1-1.5 has at least one test; rehydration/tombstoning fully covered) | 100% | ✅ Met |
| P1 | ~80% (gaps: TG-1 DataContract round-trip for EventMetadata + EventEnvelope; TG-4 verify) | ≥ 95% | ⚠️ 3 tests short |
| P2 | ~70% (gaps: TG-2 solution-wide compliance scan; TG-3 zero-handle boundary) | ≥ 60% | ✅ Met (gaps optional) |
| P3 | n/a — no exploratory/cosmetic risks identified | best effort | n/a |

### Execution Strategy (PR / Nightly / Weekly)

| Lane | Suite | Wall-Clock | Cadence |
|------|-------|------------|---------|
| **PR** | All Tier 1 (Contracts, Client, Sample, Testing, SignalR) | ~2–4 min | every PR (per CLAUDE.md) |
| **PR (post-DAPR init)** | Tier 2 Server.Tests including `TombstoningLifecycleTests` | ~5–10 min | every PR (CI runs `dapr init`) |
| **Nightly** | Full Release-build solution + Tier 1+2+3 (IntegrationTests) | ~15–25 min | nightly + on `main` merge |
| **On-demand** | None for Epic 1 surface (no perf/chaos suites needed for type contracts) | — | — |

Epic 1's tests fit comfortably in PR lane — no weekly tier required. The 5 net-adds proposed above each run in <1s — they don't change wall-clock budget.

### Resource Estimates (effort to close P1+P2 gaps)

| Gap | Story | Approx Effort |
|-----|-------|---------------|
| 1.1-UNIT-010 (`EventMetadata` DataContract round-trip) | 1.1 | ~1–2 h (mirror `CommandEnvelope` pattern) |
| 1.1-UNIT-011 (`Contracts.EventEnvelope` DataContract round-trip) | 1.1 | ~1–2 h (same pattern) |
| 1.2-UNIT-008 (CommandEnvelope field-count canary verify) | 1.2 | ~0.5 h (read + assert) |
| 1.4-UNIT-010 (zero-Handle boundary) | 1.4 | ~1–2 h (small aggregate fixture, assert behavior) |
| 1.5-UNIT-012 (solution-wide ITerminatable scan) | 1.5 | ~2–4 h (assembly scan + reflection-based assertion + helper extension) |
| **Total (P1+P2 close-out)** | | **~6–11 h, single dev** |

Upper bound covers code review + retro patches typical for this project (Epic 2 retro flagged 5/5 stories as review-driven-patch).

### Quality Gates

- **P0 pass rate = 100%** (already met for Epic 1; 651 Tier 1 tests green at epic close, plus Tier 2 `TombstoningLifecycleTests`)
- **P1 pass rate ≥ 95%** — currently failing on R-T2 silent-trap coverage (TG-1). Closing 1.1-UNIT-010 and 1.1-UNIT-011 brings P1 to 100%
- **P2 pass rate ≥ 80%** — currently met; closing TG-2 + TG-3 raises confidence but is not a blocker
- **No CRITICAL (score 9) risks** — ✅ confirmed (max score is 6)
- **High-risk mitigations (score ≥ 6) tracked**:
  - R-T1 (positional swap): tracked via named-arg policy in story specs + per-field assertion tests. Defer to story-spec convention; no new test asset
  - R-T2 (DataMember runtime): close TG-1 with the 2 new round-trip tests above
- **Coverage target ≥ 80%**: existing ratio of test files to public types in `Hexalith.EventStore.Contracts` and `Hexalith.EventStore.Client` is high; line-coverage measurement via `coverlet.collector` is enabled per CLAUDE.md but not currently published as an Epic 1 number — recommend a Tier 1 line-coverage report as a one-off audit if Jerome wants the percentage on record
