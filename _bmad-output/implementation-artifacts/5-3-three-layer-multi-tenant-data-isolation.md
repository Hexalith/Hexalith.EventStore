# Story 5.3: Three-Layer Multi-Tenant Data Isolation

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform developer**,
I want tenant isolation enforced at data path, storage key, and pub/sub topic layers,
So that failure at one layer cannot compromise tenant isolation.

**Note:** This is a **verification story**. The three-layer multi-tenant data isolation infrastructure is already fully implemented across Epics 1-5 (old numbering). Previous stories (old 5.2 Data Path Isolation, old 5.3 Pub/Sub Topic Isolation) added 48 security tests. This story formally verifies the complete isolation model against the new Epic 5 acceptance criteria, verifies SEC-1 (metadata ownership), SEC-4 (extension sanitization), SEC-5 (payload redaction), and fills any remaining test gaps. If verification uncovers a non-trivial issue (architectural flaw, security vulnerability, or change requiring > 30 minutes of work), STOP and escalate to the user rather than fixing inline.

## Acceptance Criteria

1. **Data path isolation ensures commands for one tenant never route to another (FR27)** -- Given commands for different tenants with the same domain and aggregate ID suffix, When routed through `CommandRouter`, Then `AggregateIdentity.ActorId` derives tenant-prefixed actor IDs (`tenant-a:orders:order-001` vs `tenant-b:orders:order-001`), And DAPR actor runtime guarantees separate actor instances per ActorId, And `TenantValidator` at actor Step 2 validates command tenant matches actor identity BEFORE state rehydration (SEC-2).

2. **Storage key isolation ensures event streams are inaccessible cross-tenant (FR28)** -- Given events persisted for different tenants, When state store keys are constructed, Then all keys follow tenant-scoped patterns: events `{tenant}:{domain}:{aggId}:events:{seq}`, metadata `{tenant}:{domain}:{aggId}:metadata`, snapshots `{tenant}:{domain}:{aggId}:snapshot`, pipeline `{tenant}:{domain}:{aggId}:pipeline:{stage}`, And `AggregateIdentity` rejects colons/control chars/non-ASCII in tenant IDs (preventing namespace escape), And keys are write-once (Enforcement #11).

3. **Pub/sub topic isolation ensures subscribers only receive authorized events (FR29)** -- Given events published to per-tenant-per-domain topics, When the topic name is derived, Then `AggregateIdentity.PubSubTopic` produces `{tenant}.{domain}.events` (D6), And `TopicNameValidator` enforces D6 pattern, And DAPR pub/sub component scoping restricts which app-ids can publish and subscribe, And subscriber-side scoping is configured in all pub/sub YAML files.

4. **Isolation enforced at all three layers simultaneously (NFR13)** -- Given the three isolation layers are: Layer 1 (actor identity + command routing), Layer 2 (DAPR policies: access control, state store scoping, pub/sub scoping), Layer 3 (command metadata validation: TenantValidator SEC-2), When a command flows through the pipeline, Then all three layers independently prevent cross-tenant data access, And failure at one layer does not compromise isolation at other layers.

5. **EventStore owns all 15 envelope metadata fields (SEC-1)** -- Given a domain service returns event payloads via `DomainResult`, When `EventPersister.PersistEventsAsync` creates `EventEnvelope` records, Then EventStore populates ALL 15 metadata fields: `MessageId` (ULID), `AggregateId`, `AggregateType`/`Domain`, `TenantId`, `SequenceNumber` (gapless), `CorrelationId`, `CausationId`, `UserId`, `DomainServiceVersion`, `EventTypeName`, `MetadataVersion`, `SerializationFormat`, `Timestamp`, `GlobalPosition`, plus `Extensions` (sanitized). Domain services return event payloads ONLY -- they cannot set or override any metadata field. **Note:** The epics reference "14 envelope metadata fields" -- the actual implementation has 15 (GlobalPosition was added post-epic authoring). 15 is correct.

6. **Extension metadata sanitized at API gateway (SEC-4)** -- Given a command submission includes extension metadata, When `ExtensionMetadataSanitizer.Sanitize` runs at the API gateway, Then extensions are validated for: max count (32), max key length (128), max value length (2048), max total size (4096 bytes), key pattern (`^[a-zA-Z0-9][a-zA-Z0-9._-]*$`), control characters rejected, and injection patterns (XSS, SQL, LDAP, path traversal) blocked. Invalid extensions cause 400 Bad Request with ProblemDetails before entering the pipeline.

7. **Event payload data never appears in logs (SEC-5)** -- Given events are persisted and published, When structured logging occurs at any stage (command received, actor processing, event persistence, event publication), Then event payload data (`byte[]`) is NEVER included in any log entry. `EventEnvelope.ToString()` redacts payload with `[REDACTED]`. Only envelope metadata fields (correlationId, tenantId, domain, aggregateId, eventTypeName, sequenceNumber) may be logged.

### Definition of Done

This story is complete when: all 7 ACs are verified as implemented and tested, three-layer isolation (data path + storage key + pub/sub topic) prevents cross-tenant access at each layer independently, EventStore metadata ownership is confirmed immutable, extension metadata sanitization blocks injection attacks, payload redaction prevents data leakage, and no regressions exist in Tier 1 or Tier 2 suites.

## Tasks / Subtasks

- [ ] Task 0: Verify prerequisites and baseline (BLOCKING)
  - [ ] 0.1 Run all Tier 1 tests -- confirm all pass (baseline: >= 659)
  - [ ] 0.2 Run Tier 2 tests `Hexalith.EventStore.Server.Tests` -- record actual pass count as baseline. **Baseline note:** Use the actual count from this run as your baseline for Task 7.3. Do NOT attempt to reconcile with historical baselines from old stories (929, 607, 1448) -- those were recorded at different points in development history and do not form a linear progression.
  - [ ] 0.3 Inventory existing security test files and counts:
    - `StorageKeyIsolationTests.cs` -- count tests (expected: ~17)
    - `DataPathIsolationTests.cs` -- count tests (expected: ~8 cases)
    - `DomainServiceIsolationTests.cs` -- count tests (expected: ~9 cases)
    - `TenantInjectionPreventionTests.cs` -- count tests (expected: ~19 cases)
    - `PubSubTopicIsolationEnforcementTests.cs` -- count tests (expected: ~8)
    - `SubscriptionScopingDocumentationTests.cs` -- count tests (expected: ~4)
    - `TopicIsolationTests.cs` -- count tests
    - `MultiTenantPublicationTests.cs` -- count tests
    - `ExtensionMetadataSanitizerTests.cs` -- count tests
    - `ActorTenantIsolationTests.cs` -- count tests
  - [ ] 0.4 Read `EventPersister.cs` -- verify all metadata fields populated by EventStore (AC #5, SEC-1)
  - [ ] 0.5 Read `ExtensionMetadataSanitizer.cs` -- verify sanitization rules (AC #6, SEC-4)
  - [ ] 0.6 Read `EventEnvelope.cs` (both Contracts and Server versions) -- verify `ToString()` redacts payload (AC #7, SEC-5)
  - [ ] 0.7 Read `CommandsController.cs` -- verify UserId from JWT `sub` claim, extension sanitization call, logging (AC #5, #6)

- [ ] Task 1: Verify data path isolation (AC: #1, #4)
  - [ ] 1.1 Confirm `CommandRouter` derives actor ID from `AggregateIdentity(command.Tenant, command.Domain, command.AggregateId).ActorId`
  - [ ] 1.2 Confirm actor ID format is `{tenant}:{domain}:{aggregateId}` (colon-separated)
  - [ ] 1.3 Confirm `TenantValidator.Validate` runs at actor Step 2, BEFORE `EventStreamReader` at Step 3 (SEC-2)
  - [ ] 1.4 Confirm `TenantValidator` uses `StringComparison.Ordinal` (case-sensitive)
  - [ ] 1.5 Review existing `DataPathIsolationTests.cs` coverage -- confirm routing, concurrent processing, three-layer, and TenantId flow tests exist
  - [ ] 1.6 If any data path isolation logic is missing or incorrect, fix it

- [ ] Task 2: Verify storage key isolation (AC: #2, #4)
  - [ ] 2.1 Confirm `AggregateIdentity` derives all storage keys with tenant prefix:
    - `EventStreamKeyPrefix` = `{tenant}:{domain}:{aggId}:events:`
    - `MetadataKey` = `{tenant}:{domain}:{aggId}:metadata`
    - `SnapshotKey` = `{tenant}:{domain}:{aggId}:snapshot`
    - `PipelineKeyPrefix` = `{tenant}:{domain}:{aggId}:pipeline:`
  - [ ] 2.2 Confirm tenant ID validation rejects colons, control chars (<0x20), non-ASCII (>=0x7F), URL-encoded colons (%3A), dots
  - [ ] 2.3 Confirm tenant ID regex: `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`, max 64 chars
  - [ ] 2.4 Review existing `StorageKeyIsolationTests.cs` -- confirm key disjointness, injection prevention, and shared state manager tests exist
  - [ ] 2.5 Confirm event store keys are write-once (Enforcement #11) -- `EventPersister` never updates existing event keys
  - [ ] 2.6 Verify `CommandStatusKey` includes tenant: confirm pattern is `{tenant}:{correlationId}:status` (SEC-3 -- prevents cross-tenant info leakage via correlation ID collision)
  - [ ] 2.7 Verify `PipelineKeyPrefix` includes tenant: confirm pattern is `{tenant}:{domain}:{aggId}:pipeline:` and different tenants produce disjoint pipeline keys
  - [ ] 2.8 Identify test gaps for command status and pipeline key isolation:
    - [ ] 2.8.1 Test: `CommandStatusKey_IncludesTenantPrefix_PreventsCrossTenantLeakage` -- verify status key for tenant-a is disjoint from tenant-b even with same correlationId (SEC-3)
    - [ ] 2.8.2 Test: `PipelineKeyPrefix_DifferentTenants_DisjointKeys` -- verify pipeline checkpoint keys are tenant-scoped
  - [ ] 2.9 If any storage key isolation logic is missing or incorrect, fix it

- [ ] Task 3: Verify pub/sub topic isolation (AC: #3, #4)
  - [ ] 3.1 Confirm `AggregateIdentity.PubSubTopic` produces `{tenant}.{domain}.events` (D6 pattern, dot-separated)
  - [ ] 3.2 Confirm `TopicNameValidator` enforces D6 pattern regex
  - [ ] 3.3 Confirm `EventPublisher` publishes to `identity.PubSubTopic` with CloudEvents metadata
  - [ ] 3.4 Confirm `DeadLetterPublisher` uses `deadletter.{tenant}.{domain}.events` pattern
  - [ ] 3.5 Review pub/sub YAML configs (local, RabbitMQ, Kafka) for:
    - `publishingScopes`: only `commandapi` can publish (Story 5.1)
    - `subscriptionScopes`: subscriber-side scoping configured (old Story 5.3)
    - Dead-letter topics scoped separately
  - [ ] 3.6 Review existing `PubSubTopicIsolationEnforcementTests.cs` and `SubscriptionScopingDocumentationTests.cs` -- confirm coverage
  - [ ] 3.7 If any pub/sub topic isolation logic is missing or incorrect, fix it

- [ ] Task 4: Verify metadata ownership (AC: #5, SEC-1)
  - [ ] 4.1 Read `EventPersister.PersistEventsAsync` -- verify ALL metadata fields are populated by EventStore:
    - `MessageId` = `UniqueIdHelper.GenerateSortableUniqueStringId()` (ULID)
    - `AggregateId` = from `identity.AggregateId`
    - `AggregateType` (Domain) = from `identity.Domain`
    - `TenantId` = from `identity.TenantId`
    - `SequenceNumber` = gapless `currentSequence + 1 + i`
    - `CorrelationId` = from `command.CorrelationId`
    - `CausationId` = from `command.CausationId ?? command.CorrelationId`
    - `UserId` = from `command.UserId`
    - `DomainServiceVersion` = from method parameter
    - `EventTypeName` = from event payload type
    - `MetadataVersion` = hardcoded to 1
    - `SerializationFormat` = from protection service (default "json")
    - `Timestamp` = `DateTimeOffset.UtcNow`
    - `GlobalPosition` = set to 0 (populated by event store)
  - [ ] 4.2 Confirm domain services return `DomainResult` containing only event payloads (no metadata control)
  - [ ] 4.3 Confirm `DomainResult` and `DomainEvent` types do not expose metadata setters
  - [ ] 4.4 Check if `EventEnvelopeAssertions.ShouldHaveValidMetadata` actually checks all 15 fields -- read the assertion method and verify it asserts non-null/non-default on EACH field. If it only checks a subset, the metadata ownership verification has a false-positive risk. Fix or document any missing field checks.
  - [ ] 4.5 Identify test gaps for metadata ownership. Known potential gaps:
    - [ ] 4.5.1 Test: `PersistEventsAsync_PopulatesAllMetadataFields_FromIdentityAndCommand` -- verify all 15 fields are populated (not null/default) in the persisted EventEnvelope
    - [ ] 4.5.2 Test: `PersistEventsAsync_SequenceNumbers_AreGapless` -- verify N events produce sequences currentSeq+1 through currentSeq+N
    - [ ] 4.5.3 Test: `PersistEventsAsync_MessageId_IsUniqueSortableULID` -- verify each event gets a distinct MessageId
    - [ ] 4.5.4 Test: `PersistEventsAsync_CausationId_FallsBackToCorrelationId` -- verify fallback when command.CausationId is null
    - [ ] 4.5.5 Test: `PersistEventsAsync_Timestamp_IsUtcNow` -- verify timestamp offset is UTC (offset == TimeSpan.Zero) and value is within 5 seconds of test execution time. Do NOT assert exact equality -- `DateTimeOffset.UtcNow` is inherently racy in tests.
    - [ ] 4.5.6 Test: `DomainResult_DoesNotExposeMetadataSetters` -- this is structurally guaranteed by C# records, so treat as a design-documentation test, not a security gap. The more valuable regression guardrail is verifying `IDomainServiceInvoker.InvokeAsync` method signature does not accept metadata parameters -- if someone adds metadata to the invocation contract in the future, this test catches it.
    - [ ] 4.5.7 Test: `EventEnvelope_SerializedFormat_SeparatesMetadataFromPayload` -- verify that when an EventEnvelope is serialized (JSON), metadata fields and payload are structurally separated (not flattened into one object). A naive subscriber deserializing `{metadata + payload}` as a flat object must NOT be able to shadow metadata fields (e.g., a payload containing a `TenantId` property must not override the envelope's `TenantId`). This prevents metadata poisoning via malicious domain service payloads.
    - [ ] 4.5.8 Test: `PersistEventsAsync_ZeroEvents_SkipsGracefully` -- verify that if `DomainResult` contains zero events (empty list), `EventPersister` handles it gracefully without creating empty metadata entries or throwing. The domain service contract expects events, but a buggy domain service could return empty.
    - [ ] 4.5.9 Test: `EventMetadata_Validation_AcceptsMetadataVersionGreaterThanOne` -- verify `EventMetadata` validation accepts `MetadataVersion >= 1`, not just `== 1`. If validation rejects version 2+, future metadata schema migration is blocked. `MetadataVersion` is currently hardcoded to 1, but the validation constraint must allow forward compatibility.
  - [ ] 4.6 Add any missing metadata ownership tests

- [ ] Task 5: Verify extension metadata sanitization (AC: #6, SEC-4)
  - [ ] 5.1 Read `ExtensionMetadataSanitizer.cs` -- verify all sanitization rules:
    - Max 32 extensions, max key 128 chars, max value 2048 chars, max total 4096 bytes
    - Key pattern: `^[a-zA-Z0-9][a-zA-Z0-9._-]*$`
    - Control chars rejected (0x00-0x1F except \t \n \r)
    - XSS injection patterns blocked
    - SQL injection patterns blocked
    - LDAP injection patterns blocked
    - Path traversal patterns blocked
  - [ ] 5.2 Confirm `CommandsController.Submit` calls `ExtensionMetadataSanitizer.Sanitize` BEFORE routing to MediatR
  - [ ] 5.3 Confirm sanitization failure returns 400 with ProblemDetails (not 500)
  - [ ] 5.4 Review existing `ExtensionMetadataSanitizerTests.cs` -- inventory coverage
  - [ ] 5.5 Identify test gaps for extension sanitization. Known potential gaps:
    - [ ] 5.5.1 Test: XSS injection via `<script>alert(1)</script>` in extension value
    - [ ] 5.5.2 Test: SQL injection via `'; DROP TABLE events; --` in extension value
    - [ ] 5.5.3 Test: LDAP injection via `)(cn=*))(objectClass=*` in extension value
    - [ ] 5.5.4 Test: Path traversal via `../../etc/passwd` in extension value
    - [ ] 5.5.5 Test: Control char 0x00 (null byte) in extension key or value
    - [ ] 5.5.6 Test: Extension key starting with non-alphanumeric char
    - [ ] 5.5.7 Test: Total size exactly at 4096 byte limit (boundary)
    - [ ] 5.5.8 Test: 33 extensions (one over limit)
  - [ ] 5.6 Add any missing sanitization tests (only if gaps found in existing `ExtensionMetadataSanitizerTests.cs`)

- [ ] Task 6: Verify payload redaction and logging compliance (AC: #7, SEC-5)
  - [ ] 6.1 Read `EventEnvelope.cs` (Contracts) -- confirm `ToString()` redacts payload with `[REDACTED]`
  - [ ] 6.2 Read `EventEnvelope.cs` (Server) -- confirm same redaction pattern
  - [ ] 6.3 Use Grep tool to search for `Payload` or `payload` in all `*.cs` files under `src/` that contain `Log` or `logger` -- specifically search for structured log templates containing `{Payload}`, `{EventPayload}`, or `{payload}` in `LoggerMessage`, `_logger.Log*`, or `Log.*` calls. This is a codebase scan, not a unit test.
  - [ ] 6.4 Verify `LoggingBehavior` does NOT log command or event payloads
  - [ ] 6.5 Verify `EventPersister` structured logging does NOT include payload data
  - [ ] 6.6 Verify `EventPublisher` structured logging does NOT include payload data
  - [ ] 6.7 Identify test gaps for payload redaction. Known potential gaps:
    - [ ] 6.7.1 Test: `EventEnvelope_ToString_RedactsPayload` -- verify payload is `[REDACTED]` in string representation
    - [ ] 6.7.2 Test: `EventEnvelope_ToString_IncludesMetadataFields` -- verify metadata IS included in string output
    - [ ] 6.7.3 Test: `StructuredLogging_NeverIncludesPayloadData` -- grep-style verification that no structured log template in the codebase references `{Payload}` or `{EventPayload}`
    - [ ] 6.7.4 Test: `EventEnvelope_ToString_NullPayload_StillRedacts` -- verify that when `Payload` is null (e.g., rejection event with no payload data), `ToString()` still produces `[REDACTED]` and does not print "null", empty string, or throw NullReferenceException.
  - [ ] 6.8 Add any missing payload redaction tests

- [ ] Task 7: Final verification
  - [ ] 7.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- zero warnings, zero errors
  - [ ] 7.2 Run all Tier 1 tests -- confirm pass count (baseline: >= 659)
  - [ ] 7.3 Run all Tier 2 tests -- confirm pass count (baseline: use actual count from Task 0.2)
  - [ ] 7.4 Confirm all 7 acceptance criteria are satisfied
  - [ ] 7.5 Report final test count delta
  - [ ] 7.6 Write verification report in Completion Notes (test inventory per file, gaps found, new tests added, deviations observed)

**Effort guidance:** If Tasks 1-6 pass verification with no issues found and test coverage is comprehensive, expect ~2-3 hours (verification + gap-closure tests). If SEC-1 metadata ownership or SEC-5 payload redaction tests are sparse, additional test creation may be needed. Escalate non-trivial issues per the story note above.

## Dev Notes

### Metadata Field Count: Epics Say 14, Implementation Has 15

The epics.md text references "all 14 envelope metadata fields" for SEC-1. The actual implementation in `EventPersister.cs` populates **15 fields** (GlobalPosition was added post-epic authoring). 15 is the correct count. Do not waste time trying to reconcile "14 vs 15" -- verify all 15 fields listed in Task 4.1.

### CRITICAL: This is a Verification Story

The three-layer multi-tenant data isolation infrastructure is **already fully implemented**. Previous stories under the old epic numbering (old 5.1: DAPR Access Control, old 5.2: Data Path Isolation, old 5.3: Pub/Sub Topic Isolation) added 48 security tests validating the isolation layers. StorageKeyIsolationTests (17 tests) validates key disjointness. ExtensionMetadataSanitizerTests validates SEC-4. This story formally verifies the COMPLETE isolation model against the new Epic 5 acceptance criteria and fills remaining gaps, particularly around SEC-1 (metadata ownership), SEC-4 (extension sanitization completeness), and SEC-5 (payload redaction).

### Architecture Compliance

- **Six-Layer Auth Pipeline (layers relevant to this story):**
  - **Layer 4 (MediatR Authorization):** `AuthorizationBehavior` -- tenant validation at pipeline level (Story 5.2)
  - **Layer 5 (Actor Tenant Validation):** `TenantValidator` at actor Step 2 -- SEC-2 defense-in-depth, validates before state access
  - **Layer 6 (DAPR Access Control):** Access control policies, state store scoping, pub/sub scoping (old Story 5.1)
  - Layers 1-3 (JWT, Claims, Endpoint) verified in new Stories 5.1 and 5.2

- **Triple-Layer Isolation Enforcement (NFR13):**
  - **Layer 1 (Actor Identity):** `AggregateIdentity.ActorId` = `{tenant}:{domain}:{aggId}` -- `CommandRouter` derives from command metadata
  - **Layer 2 (DAPR Policies):** Access control (`accesscontrol.yaml`), state store scoping, pub/sub scoping (`pubsub.yaml`)
  - **Layer 3 (Command Metadata):** `TenantValidator` at actor Step 2 validates command tenant matches actor identity

- **SEC-1 (Metadata Ownership):** `EventPersister` is the SOLE point where envelope metadata is created. Domain services return `DomainResult` with event payloads only. All 15 metadata fields are populated from trusted sources (AggregateIdentity, CommandEnvelope, system clock, ULID generator). Domain services CANNOT set or override any metadata field.

- **SEC-4 (Extension Sanitization):** `ExtensionMetadataSanitizer` validates at the API gateway (`CommandsController.Submit`), BEFORE extensions enter the MediatR pipeline. Rejected extensions return 400 Bad Request. Configurable via `ExtensionMetadataOptions` bound to config.

- **SEC-5 (Payload Redaction):** Both `EventEnvelope` types (Contracts + Server) override `ToString()` to redact `Payload` with `[REDACTED]`. Structured logging must NEVER include `{Payload}` template parameters. Only metadata fields may appear in logs.

- **State Store Key Patterns (D1):**
  - Event: `{tenant}:{domain}:{aggId}:events:{seq}` (write-once)
  - Metadata: `{tenant}:{domain}:{aggId}:metadata` (ETag concurrency)
  - Snapshot: `{tenant}:{domain}:{aggId}:snapshot` (interval-based)
  - Pipeline: `{tenant}:{domain}:{aggId}:pipeline:{stage}` (crash recovery)
  - Command status: `{tenant}:{correlationId}:status` (tenant-scoped)

- **Pub/Sub Topic Pattern (D6):** `{tenant}.{domain}.events` (dot-separated). Dead-letter: `deadletter.{tenant}.{domain}.events`. TopicNameValidator enforces pattern.

- **Enforcement Rules (relevant):**
  - #4: No custom retry logic -- DAPR resiliency only
  - #5: Never log event payload data -- envelope metadata only
  - #6: IActorStateManager for all actor state operations
  - #11: Event store keys are write-once
  - #14: DAPR sidecar call timeout is 5 seconds

### Key Source Files

| File | Purpose |
|------|---------|
| `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` | Canonical identity: ActorId, key patterns, PubSubTopic, validation |
| `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` | 5-step orchestrator with tenant validation at Step 2 |
| `src/Hexalith.EventStore.Server/Actors/TenantValidator.cs` | Actor-level SEC-2 tenant validation |
| `src/Hexalith.EventStore.Server/Events/EventPersister.cs` | Metadata ownership -- populates all 15 fields (SEC-1) |
| `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` | Pub/sub publication with CloudEvents metadata |
| `src/Hexalith.EventStore.Server/Events/TopicNameValidator.cs` | D6 topic pattern enforcement |
| `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs` | Dead-letter publication with tenant-scoped topics |
| `src/Hexalith.EventStore.Server/Commands/CommandRouter.cs` | Actor ID derivation from AggregateIdentity |
| `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` | API gateway: UserId extraction, extension sanitization |
| `src/Hexalith.EventStore.CommandApi/Validation/ExtensionMetadataSanitizer.cs` | SEC-4 extension sanitization |
| `src/Hexalith.EventStore.CommandApi/Configuration/ExtensionMetadataOptions.cs` | Sanitization config defaults |
| `src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs` | Contract EventEnvelope with ToString() redaction |
| `src/Hexalith.EventStore.Server/Events/EventEnvelope.cs` | Server EventEnvelope with ToString() redaction |
| `src/Hexalith.EventStore.Contracts/Events/EventMetadata.cs` | 15-field metadata record with validation |
| `src/Hexalith.EventStore.Testing/Assertions/EventEnvelopeAssertions.cs` | ShouldHaveValidMetadata assertion helper |
| `tests/Hexalith.EventStore.Server.Tests/Security/StorageKeyIsolationTests.cs` | Storage key disjointness tests (~17) |
| `tests/Hexalith.EventStore.Server.Tests/Security/DataPathIsolationTests.cs` | Routing isolation tests (~8 cases) |
| `tests/Hexalith.EventStore.Server.Tests/Security/DomainServiceIsolationTests.cs` | Domain service tenant context tests (~9 cases) |
| `tests/Hexalith.EventStore.Server.Tests/Security/TenantInjectionPreventionTests.cs` | Injection prevention tests (~19 cases) |
| `tests/Hexalith.EventStore.Server.Tests/Security/PubSubTopicIsolationEnforcementTests.cs` | Subscriber scoping tests (~8) |
| `tests/Hexalith.EventStore.Server.Tests/Security/SubscriptionScopingDocumentationTests.cs` | Documentation presence tests (~4) |
| `tests/Hexalith.EventStore.Server.Tests/Security/ExtensionMetadataSanitizerTests.cs` | Extension sanitization tests |
| `tests/Hexalith.EventStore.Server.Tests/Actors/ActorTenantIsolationTests.cs` | Actor-level tenant isolation |

### Existing Patterns to Follow

- **Verification task structure:** Same as Stories 5.1 and 5.2 -- read code first, verify against ACs, then fill test gaps.
- **Test naming:** `{Method}_{Scenario}_{ExpectedResult}` convention.
- **Assertion library:** Shouldly 4.3.0 fluent assertions.
- **Mocking:** NSubstitute 5.3.0 for interface mocks.
- **No YAML parsing library in tests:** Use `File.ReadAllText` + `ShouldContain`/`ShouldNotContain` for YAML validation (matches existing `AccessControlPolicyTests.cs` pattern). Exception: if `AccessControlPolicyTests.cs` already uses YamlDotNet, follow that pattern.
- **File paths in tests:** `Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", ...))`
- **Security tests:** Feature folder at `tests/Hexalith.EventStore.Server.Tests/Security/`
- **Actor test setup:** `ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions { ActorId = new ActorId("test-tenant:test-domain:agg-001") })` with mock StateManager injection via reflection

### Cross-Story Dependencies

- **Story 5.1 (JWT Authentication & Claims Transformation)** -- DONE. Produces tenant claims consumed by authorization pipeline.
- **Story 5.2 (Claims-Based Command Authorization)** -- **HARD BLOCKING PREREQUISITE** (must be done before this story). Without Layer 4 tenant authorization verified, the "all three layers enforced simultaneously" claim in AC #4 is incomplete. The six-layer auth pipeline is sequential -- verifying layers 5-6 without confirmed layer 4 is architecturally unsound. Story 5.2 is currently in `review` status.
- **Story 5.4 (DAPR Service-to-Service Access Control)** -- depends on the three-layer model verified here.
- **Story 5.5 (E2E Security Testing with Keycloak)** -- will exercise the full isolation model with real OIDC tokens.

### Previous Story Intelligence

**Story 5.1 (JWT Authentication & Claims Transformation, new numbering)** -- status: done:
- Added 21 gap-closure tests across 3 test files.
- Fixed pre-existing duplicate `backpressure-exceeded` entry.
- Test baseline after 5.1: Tier 1: 659, Tier 2: 1448.
- Pattern: Verification-style tasks with clear baseline checks, reading existing code before modifying.

**Story 5.2 (Claims-Based Command Authorization, new numbering)** -- status: review (HARD BLOCKING prerequisite):
- 10 acceptance criteria covering tenant validation, RBAC, 403 responses, actor defense-in-depth.
- Currently in code review -- must reach `done` before this story begins.
- Critical for this story: Tenant authorization at MediatR Layer 4 feeds into Layer 1 data path isolation. Without confirmed Layer 4, AC #4 ("all three layers enforced simultaneously") cannot be fully verified.

**Old Story 5.2 (Data Path Isolation Verification)** -- status: done:
- 36 new test cases across 3 test files: DataPathIsolationTests (8 cases), DomainServiceIsolationTests (9 cases), TenantInjectionPreventionTests (19 cases).
- Final count: 929 tests (893 existing + 36 new).
- Verified: CommandRouter routing, TenantValidator SEC-2, AggregateIdentity injection prevention, Unicode homoglyphs, concurrent processing, domain service tenant scoping, config key isolation.
- Key learning: Actor test setup uses reflection for StateManager injection. DaprClient.InvokeMethodAsync is difficult to mock directly -- verify via resolver instead.

**Old Story 5.3 (Pub/Sub Topic Isolation Enforcement)** -- status: done:
- 12 new tests across 2 test files: PubSubTopicIsolationEnforcementTests (8), SubscriptionScopingDocumentationTests (4).
- Final count: 607 server tests at time of completion.
- Fixed production YAML bug: `commandapi=*` was literal string not wildcard (DAPR doesn't support wildcards).
- Enhanced subscriber scoping in local and production pub/sub YAML configs.
- Key learning: DAPR `subscriptionScopes` format is `app1=topic1,topic2;app2=topic3`. No wildcard support.

### Git Intelligence

Recent commits (relevant context):
- `91fd854` -- Enhance verification tasks for claims-based command authorization (Story 5.2 prep)
- `6ae83e1` -- Update sprint status and implement claims-based command authorization
- `687a7e0` -- Merge PR #108: per-aggregate backpressure fix
- Authorization and security infrastructure built across Epics 1-5 (old numbering)

### UserId Provenance Trust Chain

`UserId` in the persisted event metadata originates from the JWT `sub` claim, extracted by `CommandsController.Submit`, passed through `CommandEnvelope`, and written by `EventPersister`. The trust chain is: JWT validation (Layer 1, Story 5.1) -> `CommandsController` extracts `sub` -> `CommandEnvelope.UserId` -> `EventPersister` writes to metadata. If JWT validation is bypassed, UserId could be forged. This trust chain is verified end-to-end by Story 5.5 (E2E with Keycloak). For this story, verify the `EventPersister` correctly reads from `command.UserId` (Task 4.1) -- the upstream JWT trust is Story 5.1/5.5's responsibility.

### Epic 11 Projection Isolation Warning

Epic 11 introduces `EventReplayProjectionActor` for server-managed projections. Projection actors that read events across tenants (e.g., for admin dashboards or cross-tenant analytics) could bypass the three-layer tenant isolation model verified in this story. **Future projection actors MUST maintain tenant-scoped key queries** -- they must never query event keys without a tenant prefix. This constraint cannot be tested now (Epic 11 code doesn't exist), but documenting it here prevents future developers from accidentally breaking isolation when implementing projection replay.

### Anti-Patterns to Avoid

- **DO NOT rewrite existing isolation code.** Verify and fix gaps only. The three-layer isolation infrastructure is production-ready.
- **DO NOT modify claims transformation (Story 5.1), authorization behavior (Story 5.2), or DAPR access control (Story 5.4).** Those are separate stories.
- **DO NOT duplicate tests that already exist.** The old Stories 5.2 and 5.3 added comprehensive security tests. Review existing coverage FIRST (Task 0.3), then add ONLY what's missing.
- **DO NOT add Keycloak or real OIDC testing.** That is Story 5.5 (D11). This story uses unit/integration tests.
- **DO NOT add new NuGet dependencies.** All required packages are already referenced.
- **DO NOT change AggregateIdentity validation regexes or key patterns.** These are architectural decisions.
- **DO NOT log event payload data or JWT tokens.** SEC-5 is non-negotiable.
- **DO NOT modify EventPersister metadata field assignments.** SEC-1 metadata ownership is an architectural invariant.

### Testing Standards

- **Framework:** xUnit 2.9.3 / Shouldly 4.3.0 / NSubstitute 5.3.0
- **Test naming:** `{Method}_{Scenario}_{ExpectedResult}`
- **Existing security tests span 10+ test files** across Security/, Events/, Actors/ directories
- **Tier separation:** Tier 1 (unit, no DAPR) for contract/identity tests. Tier 2 (DAPR slim) for server/actor tests. Tier 3 (full Aspire) for integration tests.
- **Test helpers:** `EventEnvelopeAssertions.ShouldHaveValidMetadata` for metadata completeness checks.
- **YAML test pattern:** `File.ReadAllText` + Shouldly string assertions (or YamlDotNet if existing tests use it).

### Project Structure Notes

- Security tests in `tests/Hexalith.EventStore.Server.Tests/Security/`
- Event tests in `tests/Hexalith.EventStore.Server.Tests/Events/`
- Actor tests in `tests/Hexalith.EventStore.Server.Tests/Actors/`
- Contract tests in `tests/Hexalith.EventStore.Contracts.Tests/`
- Extension sanitization tests in `tests/Hexalith.EventStore.Server.Tests/Security/`
- No new project folders expected
- No new NuGet packages needed

### Advanced Elicitation Findings

**5-method analysis applied to story content:**

1. **Red Team vs Blue Team** (5 attack vectors): Identified metadata poisoning via payload field shadowing (RT-1) as untested. Extension covert channel (RT-3) and timing side-channel (RT-4) accepted as out-of-scope risks. State store enumeration (RT-2) covered by Story 5.1 DAPR scoping. CloudEvents source field (RT-5) is by-design.
2. **Security Audit Personas** (Auditor + Hacker): Auditor identified UserId provenance trust chain from JWT `sub` through full pipeline as under-documented (SA-1). Hacker confirmed correlation ID collision is prevented by tenant-scoped status keys (SEC-3, already added in review round).
3. **Failure Mode Analysis** (3 scenarios): Partial write prevented by DAPR actor ACID (FM-1, architectural). Snapshot tenant mismatch structurally prevented by tenant-in-key (FM-2). ToString() with null payload edge case identified (FM-3).
4. **Chaos Monkey Scenarios** (3 scenarios): Zero-event DomainResult edge case identified (CM-1). Max-length key and concurrent metadata writes are covered by existing tests and architectural guarantees (CM-2, CM-3).
5. **Pre-Mortem Analysis** (3 future failure scenarios): Epic 11 projection isolation warning documented (PM-1). Extension sanitization regex limitations accepted (PM-2). MetadataVersion forward compatibility constraint identified (PM-3).

**Impact:** 4 new test cases added (4.5.7 metadata/payload separation, 4.5.8 zero-event handling, 4.5.9 MetadataVersion forward compat, 6.7.4 null payload ToString). 2 Dev Notes added (UserId provenance, Epic 11 projection warning). 5 findings accepted as covered or out-of-scope.

### References

- [Source: epics.md#Story-5.3] Three-Layer Multi-Tenant Data Isolation acceptance criteria
- [Source: prd.md#FR27] Data path isolation -- commands never routed cross-tenant
- [Source: prd.md#FR28] Storage key isolation -- event streams inaccessible cross-tenant
- [Source: prd.md#FR29] Pub/sub topic isolation -- subscribers only authorized events
- [Source: prd.md#NFR13] Multi-tenant isolation at all three layers
- [Source: architecture.md#SEC-1] EventStore owns all envelope metadata fields
- [Source: architecture.md#SEC-2] Tenant validation BEFORE state rehydration
- [Source: architecture.md#SEC-4] Extension metadata sanitized at API gateway
- [Source: architecture.md#SEC-5] Event payload data never appears in logs
- [Source: architecture.md#D1] Event storage key patterns
- [Source: architecture.md#D6] Pub/sub topic naming pattern
- [Source: architecture.md#NFR13] Triple-layer isolation enforcement
- [Source: architecture.md#Enforcement-5] Never log event payload data
- [Source: architecture.md#Enforcement-11] Event store keys are write-once
- [Source: 5-1-jwt-authentication-and-claims-transformation.md] Test baseline: Tier 1 659, Tier 2 1448
- [Source: 5-2-data-path-isolation-verification.md] 36 security tests for data path isolation
- [Source: 5-3-pubsub-topic-isolation-enforcement.md] 12 tests for pub/sub subscriber scoping
- [Source: 5-2-claims-based-command-authorization.md] Story 5.2 architecture context

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
