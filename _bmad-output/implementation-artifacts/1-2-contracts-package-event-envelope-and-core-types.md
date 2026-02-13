# Story 1.2: Contracts Package - Event Envelope & Core Types

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **domain service developer**,
I want a Contracts NuGet package defining EventMetadata, EventEnvelope, CommandEnvelope, AggregateIdentity, IRejectionEvent, DomainResult, CommandStatus enum, and CommandStatusRecord,
so that I can build domain services against stable, versioned contracts with zero external dependencies.

## Prerequisites

Before starting this story, the dev agent MUST verify:

- [ ] Story 1.1 completed: Solution scaffold with 12 projects, build infrastructure, and Contracts project stub exist
- [ ] `dotnet build` passes with zero errors and zero warnings on the existing solution
- [ ] Contracts project currently contains only `BuildVerification.cs` placeholder (to be replaced)

## Acceptance Criteria

1. **EventMetadata Record** - Given the Contracts package is referenced, When I inspect the EventMetadata record, Then it contains exactly the 11 metadata fields as a grouped record: AggregateId (string), TenantId (string), Domain (string), SequenceNumber (long), Timestamp (DateTimeOffset), CorrelationId (string), CausationId (string), UserId (string), DomainServiceVersion (string), EventTypeName (string), SerializationFormat (string). This record enables passing metadata without payload (SEC-5 logging compliance).

2. **EventEnvelope Record** - Given the EventEnvelope record, When I inspect it, Then it composes: Metadata (EventMetadata), Payload (byte[]? -- nullable for events with no payload data), and Extensions (IReadOnlyDictionary<string, string>, defaults to empty, never null). EventEnvelope is a `record class` with 3 clear parameters, not 14 flat fields.

3. **AggregateIdentity** - Given the AggregateIdentity `readonly record struct` with TenantId, Domain, and AggregateId properties, When I access its computed properties, Then it correctly derives: `ActorId` (`{tenant}:{domain}:{aggregateId}`), `EventStreamKeyPrefix` (`{tenant}:{domain}:{aggregateId}:events`), `MetadataKey` (`{tenant}:{domain}:{aggregateId}:metadata`), `SnapshotKey` (`{tenant}:{domain}:{aggregateId}:snapshot`), `PubSubTopic` (`{tenant}.{domain}.events`), `QueueSession` (`{tenant}:{domain}:{aggregateId}`) from the canonical tuple (FR26). And `FormatEventKey(long sequenceNumber)` returns `{tenant}:{domain}:{aggregateId}:events:{seq}`. And `FormatCommandStatusKey(string correlationId)` returns `{tenant}:{correlationId}:status`. And `static AggregateIdentity Parse(string actorId)` parses `tenant:domain:aggregateId` format. And constructor validation rejects: null/empty/whitespace components, components containing colons or dots (key/topic separator conflicts), components exceeding 128 characters, components not matching `^[a-zA-Z0-9][a-zA-Z0-9_-]*$`. Validation is case-sensitive (e.g., `ACME` and `acme` are distinct tenants).

4. **CommandEnvelope Record** - Given the CommandEnvelope record, When I inspect it, Then it contains: TenantId (string), Domain (string), AggregateId (string), CommandType (string), Payload (byte[]? -- nullable for commands with no payload), CorrelationId (string), CausationId (string), UserId (string), Extensions (IReadOnlyDictionary<string, string>, defaults to empty, never null), and Timestamp (DateTimeOffset). And it exposes a computed `AggregateIdentity` property that constructs `new AggregateIdentity(TenantId, Domain, AggregateId)` -- preventing manual reconstruction errors across Epics 2-3.

5. **IRejectionEvent Marker Interface** - Given a domain event type implements IRejectionEvent, When I check the type, Then it is correctly identified as a rejection event via the marker interface. IRejectionEvent inherits from IEventPayload (D3).

6. **DomainResult** - Given the DomainResult `record class`, When I use its factory methods, Then `DomainResult.Success(events)` requires a non-empty list with NO IRejectionEvent instances (throws ArgumentException otherwise), And `DomainResult.Rejection(rejectionEvents)` requires a non-empty list where ALL events implement IRejectionEvent (throws ArgumentException otherwise), And `DomainResult.NoOp()` creates a result with an empty event list. Properties: `Events` (IReadOnlyList<IEventPayload>), `IsSuccess`, `IsRejection`, `IsNoOp`. Constructor is private -- only factory methods allowed.

7. **CommandStatus Enum** - Given the CommandStatus enum, When I inspect its values, Then it defines exactly: Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, TimedOut (matching the checkpointed state machine lifecycle).

8. **CommandStatusRecord** - Given the CommandStatusRecord `record class`, When I inspect it, Then it contains: Status (CommandStatus), Timestamp (DateTimeOffset), AggregateId (string?), CorrelationId (string), and terminal-state context: EventCount (int? -- populated for Completed), RejectionEventType (string? -- populated for Rejected), FailureReason (string? -- populated for PublishFailed), TimeoutDuration (TimeSpan? -- populated for TimedOut). Per D2, this is the rich status record stored at `{tenant}:{correlationId}:status`.

9. **IEventPayload Marker Interface** - Given a domain event type, When it is used in the system, Then it implements IEventPayload as the base marker for all domain event payloads (both state-change and rejection events).

10. **Zero External Dependencies** - Given the Contracts package .csproj, When I inspect it, Then it has zero PackageReference entries. All types use only `System.*` and `Microsoft.*` BCL types.

11. **Clean Build** - Given the updated Contracts package, When I run `dotnet build`, Then the entire solution builds with zero errors and zero warnings.

12. **Existing Tests Pass** - Given the updated Contracts package, When I run `dotnet test`, Then all existing placeholder tests still pass.

## Tasks / Subtasks

### Task 1: Create Identity types (AC: #3)

- [ ] 1.1 Create `Identity/` feature folder in Contracts project
- [ ] 1.2 Implement `AggregateIdentity.cs` readonly record struct with constructor validation (regex `^[a-zA-Z0-9][a-zA-Z0-9_-]*$`, max 128 chars, no colons/dots), computed derivation properties (ActorId, EventStreamKeyPrefix, MetadataKey, SnapshotKey, PubSubTopic, QueueSession), helper methods (FormatEventKey, FormatCommandStatusKey, Parse, ToString)
- [ ] 1.3 Delete `BuildVerification.cs` from Contracts project (no longer needed once real types exist)

### Task 2: Create Event types (AC: #1, #2, #5, #9)

- [ ] 2.1 Create `Events/` feature folder in Contracts project
- [ ] 2.2 Implement `IEventPayload.cs` marker interface
- [ ] 2.3 Implement `IRejectionEvent.cs` marker interface (extends IEventPayload)
- [ ] 2.4 Implement `EventMetadata.cs` record class grouping the 11 metadata fields
- [ ] 2.5 Implement `EventEnvelope.cs` record class composing EventMetadata + Payload (byte[]?) + Extensions (IReadOnlyDictionary, default empty)

### Task 3: Create Command types (AC: #4, #7, #8)

- [ ] 3.1 Create `Commands/` feature folder in Contracts project
- [ ] 3.2 Implement `CommandEnvelope.cs` record class with AggregateIdentity computed property
- [ ] 3.3 Implement `CommandStatus.cs` enum (8 values)
- [ ] 3.4 Implement `CommandStatusRecord.cs` record class with Status + context fields per D2

### Task 4: Create Result types (AC: #6)

- [ ] 4.1 Create `Results/` feature folder in Contracts project
- [ ] 4.2 Implement `DomainResult.cs` record class with private constructor, factory methods (Success/Rejection/NoOp) with validation, and semantic properties (IsSuccess/IsRejection/IsNoOp)

### Task 5: Verify build and tests (AC: #10, #11, #12)

- [ ] 5.1 Verify Contracts .csproj has zero PackageReference entries
- [ ] 5.2 Run `dotnet build` - zero errors, zero warnings
- [ ] 5.3 Run `dotnet test` - all existing tests pass

## Dev Notes

### Architecture Constraints (MUST FOLLOW)

- **Zero external dependencies**: Contracts is the leaf node of the dependency graph. No NuGet PackageReference entries. Only BCL types (`System.*`)
- **Feature folders**: `Events/`, `Commands/`, `Identity/`, `Results/` -- NOT type-based folders like `Models/` or `Interfaces/`
- **One public type per file**: File name matches type name exactly
- **Immutable record classes**: Use `record class` for EventEnvelope, EventMetadata, CommandEnvelope, CommandStatusRecord, DomainResult (reference types, heap-allocated, suitable for larger objects)
- **Readonly record struct**: Use `readonly record struct` for AggregateIdentity (small value type, 3 strings, frequently passed around)
- **Enum for CommandStatus**: Simple enum, not a record
- **Marker interfaces**: IEventPayload and IRejectionEvent are empty marker interfaces
- **Null safety**: `IReadOnlyDictionary` properties (Extensions) MUST default to empty dictionary, never null. Payload is `byte[]?` (nullable -- some events/commands carry no payload data)
- **Namespace convention**: `Hexalith.EventStore.Contracts.Events`, `Hexalith.EventStore.Contracts.Commands`, `Hexalith.EventStore.Contracts.Identity`, `Hexalith.EventStore.Contracts.Results`
- **Architecture deviation note**: The architecture doc shows `Identity/IdentityParser.cs` as a separate file. This is intentionally merged into `AggregateIdentity.Parse()` static method for simplicity -- parsing logic belongs on the type itself

### EventMetadata Field Specification (11 Metadata Fields)

Per architecture D1 and FR11, the EventMetadata `record class` groups exactly these 11 metadata fields:

| # | Field | Type | Description |
|---|-------|------|-------------|
| 1 | AggregateId | string | Unique aggregate identifier |
| 2 | TenantId | string | Tenant identifier for isolation |
| 3 | Domain | string | Domain name (e.g., "orders", "payments") |
| 4 | SequenceNumber | long | Strictly ordered, gapless within aggregate stream |
| 5 | Timestamp | DateTimeOffset | When the event was persisted |
| 6 | CorrelationId | string | End-to-end request correlation |
| 7 | CausationId | string | Direct cause (command or parent event) |
| 8 | UserId | string | Authenticated user who triggered the command |
| 9 | DomainServiceVersion | string | Version of domain service that produced the event |
| 10 | EventTypeName | string | Fully qualified event type name for deserialization |
| 11 | SerializationFormat | string | Format of payload (e.g., "application/json") |

**Why a separate record**: Enables passing metadata without payload for logging (SEC-5 compliance), metadata-only operations in the Server package, and reduces EventEnvelope constructor to 3 clear parameters instead of 14 flat fields.

### EventEnvelope Composition

EventEnvelope is a `record class` that composes:

| Field | Type | Description |
|-------|------|-------------|
| Metadata | EventMetadata | The 11 metadata fields grouped |
| Payload | byte[]? | Opaque event payload (nullable -- some events carry no payload, e.g., CounterReset) |
| Extensions | IReadOnlyDictionary<string, string> | Extensibility metadata bag (defaults to empty, NEVER null) |

**CRITICAL**: EventStore owns and populates ALL 11 metadata fields (SEC-1). Domain services return payloads only. The Contracts package defines the shape; the Server package fills it.

### AggregateIdentity Derivation Rules (FR26)

The AggregateIdentity encapsulates the canonical `tenant:domain:aggregate-id` tuple. All addressing in the system derives from this single source:

| Derived Value | Pattern | Example |
|--------------|---------|---------|
| ActorId | `{tenant}:{domain}:{aggregateId}` | `acme:payments:order-123` |
| EventStreamKeyPrefix | `{tenant}:{domain}:{aggregateId}:events` | `acme:payments:order-123:events` |
| MetadataKey | `{tenant}:{domain}:{aggregateId}:metadata` | `acme:payments:order-123:metadata` |
| SnapshotKey | `{tenant}:{domain}:{aggregateId}:snapshot` | `acme:payments:order-123:snapshot` |
| PubSubTopic | `{tenant}.{domain}.events` | `acme.payments.events` |
| QueueSession | `{tenant}:{domain}:{aggregateId}` | `acme:payments:order-123` |
| EventKey (via method) | `{tenant}:{domain}:{aggregateId}:events:{seq}` | `acme:payments:order-123:events:42` |
| CommandStatusKey (via method) | `{tenant}:{correlationId}:status` | `acme:abc-def-123:status` |

**Validation rules** (enforced in constructor, throw `ArgumentException` with descriptive messages):
- TenantId, Domain, and AggregateId must all be non-null, non-empty, non-whitespace
- Each component must match regex: `^[a-zA-Z0-9][a-zA-Z0-9_-]*$` (starts with alphanumeric, then alphanumeric/underscore/hyphen)
- Maximum 128 characters per component (prevents excessively long DAPR state store keys)
- No colons (`:`) within components -- colons are key separators in state store key patterns
- No dots (`.`) within components -- dots are separators in pub/sub topic patterns
- Validation is case-sensitive: `ACME` and `acme` are treated as distinct values
- Error messages must identify which component failed and why (e.g., "TenantId 'acme.corp' contains invalid character '.' at position 4")

### CommandStatus Lifecycle (D2)

```
Received -> Processing -> EventsStored -> EventsPublished -> Completed
                                      \-> Rejected
                                       \-> PublishFailed
                                        \-> TimedOut
```

Terminal states: `Completed`, `Rejected`, `PublishFailed`, `TimedOut`

### CommandStatusRecord Specification (D2)

Per architecture D2 ("Status enum + record"), the CommandStatusRecord captures rich status context stored at `{tenant}:{correlationId}:status`:

| Field | Type | Description |
|-------|------|-------------|
| Status | CommandStatus | Current lifecycle stage |
| Timestamp | DateTimeOffset | When this status was recorded |
| AggregateId | string? | Aggregate being processed (null before routing) |
| CorrelationId | string | End-to-end request correlation |
| EventCount | int? | Populated for Completed -- number of events persisted |
| RejectionEventType | string? | Populated for Rejected -- the rejection event type name |
| FailureReason | string? | Populated for PublishFailed -- description of failure |
| TimeoutDuration | TimeSpan? | Populated for TimedOut -- how long before timeout |

This record is what the status endpoint returns (Epic 2, Story 2.6) and what gets stored in the DAPR state store with 24-hour TTL.

### DomainResult Semantics (D3)

| Outcome | Events | IsSuccess | IsRejection | IsNoOp |
|---------|--------|-----------|-------------|--------|
| State change | 1+ non-rejection events | true | false | false |
| Business rejection | 1+ IRejectionEvent | false | true | false |
| No-op (valid, no change) | empty list | false | false | true |

**CRITICAL**: Domain rejections are expressed as events (IRejectionEvent), NOT exceptions. Empty event list = valid acknowledgment with no state change. Exceptions indicate infrastructure failures only.

**Factory method validation rules**:
- `DomainResult.Success(events)`: Throws `ArgumentException` if events is null, empty, or contains any IRejectionEvent. Use `NoOp()` for empty lists, `Rejection()` for rejection events.
- `DomainResult.Rejection(events)`: Throws `ArgumentException` if events is null, empty, or contains any event NOT implementing IRejectionEvent. All events must be rejection events.
- `DomainResult.NoOp()`: Always valid -- returns a result with `Array.Empty<IEventPayload>()`.

### Previous Story 1.1 Learnings (APPLY THESE)

1. **SDK is 10.0.102** (not 10.0.103) -- global.json already pinned
2. **TreatWarningsAsErrors=true** is active -- code must compile with zero warnings
3. **Delete `BuildVerification.cs`** when adding real types -- it was a placeholder only
4. **No Version= in PackageReference** -- but this story adds NO packages (zero deps), so not applicable
5. **Feature folder convention** already established (story 1.1 used it for DAPR components)
6. **.editorconfig** enforces naming conventions -- `_camelCase` for private fields, `I` prefix for interfaces, `Async` suffix for async methods
7. **Namespace convention**: `Hexalith.EventStore.{Project}.{FeatureFolder}` -- verified from story 1.1

### Git Intelligence (Recent Commits)

- `bb88124` Story 1.1 completed: Solution scaffold with all 12 projects, build infrastructure, DAPR components
- Solution builds and tests pass as of this commit
- No code changes pending on main branch (clean working tree)

### Existing Source Tree (Story 1.1 Output)

The Contracts project currently contains:
```
src/Hexalith.EventStore.Contracts/
  Hexalith.EventStore.Contracts.csproj
  BuildVerification.cs              <-- DELETE THIS, replace with real types
```

After this story, it should contain:
```
src/Hexalith.EventStore.Contracts/
  Hexalith.EventStore.Contracts.csproj
  Identity/
    AggregateIdentity.cs           # readonly record struct
  Events/
    IEventPayload.cs               # marker interface
    IRejectionEvent.cs             # marker interface (extends IEventPayload)
    EventMetadata.cs               # record class - 11 metadata fields grouped
    EventEnvelope.cs               # record class - composes EventMetadata + Payload + Extensions
  Commands/
    CommandEnvelope.cs             # record class - with AggregateIdentity computed property
    CommandStatus.cs               # enum - 8 lifecycle states
    CommandStatusRecord.cs         # record class - rich status per D2
  Results/
    DomainResult.cs                # record class - factory methods with validation
```

### Type Design Guidance

**EventMetadata** (`record class`): Group the 11 metadata fields into a single record. Constructor validates all string fields are non-null/non-empty. SequenceNumber must be >= 0. This record is the SEC-5 enabler -- pass it to logging without the payload.

**EventEnvelope** (`record class`): Composes `EventMetadata Metadata`, `byte[]? Payload`, `IReadOnlyDictionary<string, string> Extensions`. Only 3 constructor parameters. Extensions defaults to `ImmutableDictionary<string, string>.Empty` (from `System.Collections.Immutable`, which IS part of the BCL -- no external dependency). Validates Metadata is non-null.

**AggregateIdentity** (`readonly record struct`): Small value type (3 strings). Constructor validates all components per regex rules. Derivation as computed properties (not methods):
- `string ActorId` => `$"{TenantId}:{Domain}:{AggregateId}"`
- `string EventStreamKeyPrefix` => `$"{TenantId}:{Domain}:{AggregateId}:events"`
- `string MetadataKey` => `$"{TenantId}:{Domain}:{AggregateId}:metadata"`
- `string SnapshotKey` => `$"{TenantId}:{Domain}:{AggregateId}:snapshot"`
- `string PubSubTopic` => `$"{TenantId}.{Domain}.events"`
- `string QueueSession` => `$"{TenantId}:{Domain}:{AggregateId}"`
- `string FormatEventKey(long sequenceNumber)` => `$"{EventStreamKeyPrefix}:{sequenceNumber}"`
- `static string FormatCommandStatusKey(string tenantId, string correlationId)` => `$"{tenantId}:{correlationId}:status"`
- `static AggregateIdentity Parse(string actorId)` -- splits on `:`, validates exactly 3 segments
- Override `ToString()` => `ActorId`

**CommandEnvelope** (`record class`): Include all fields for command submission. Add computed property `AggregateIdentity AggregateIdentity => new(TenantId, Domain, AggregateId)`. Extensions defaults to empty dictionary.

**CommandStatusRecord** (`record class`): Wraps CommandStatus enum with contextual data per D2. All terminal-state fields are nullable (only populated for their respective terminal state).

**DomainResult** (`record class`): Private constructor. Factory methods with strict validation:
- `Success(IReadOnlyList<IEventPayload> events)` -- non-empty, no IRejectionEvent
- `Rejection(IReadOnlyList<IEventPayload> events)` -- non-empty, all IRejectionEvent
- `NoOp()` -- empty list, `Array.Empty<IEventPayload>()`

### CRITICAL GUARDRAILS FOR DEV AGENT

1. **DO NOT add any NuGet PackageReference** to the Contracts project -- zero external dependencies is a hard requirement. `System.Collections.Immutable` is part of the BCL runtime, not an external package.
2. **DO NOT use mutable types** -- all contract types MUST be immutable records (or enums)
3. **DO NOT add Payload as `object`** -- Payload is `byte[]?` (opaque, schema-ignorant, nullable for no-payload events)
4. **DO NOT add `Serialization/` folder in this story** -- EventSerializer involves serialization logic and may need external deps; defer to when actually needed
5. **DO NOT add validation logic that depends on external libraries** -- use BCL ArgumentException/ArgumentNullException only
6. **DO NOT change any other project** except Contracts -- this story is scoped to Contracts types only
7. **DO NOT forget to delete `BuildVerification.cs`** -- it was a scaffold placeholder from Story 1.1
8. **DO NOT flatten EventMetadata fields into EventEnvelope** -- use composition (EventEnvelope contains EventMetadata). This is critical for SEC-5 logging compliance.
9. **DO use `IReadOnlyDictionary<string, string>`** for Extensions (not `IDictionary`) -- contracts should expose read-only interfaces. Default to `ImmutableDictionary<string, string>.Empty`, NEVER null.
10. **DO use `IReadOnlyList<IEventPayload>`** for DomainResult.Events (not `List<>`) -- immutable contract
11. **DO validate AggregateIdentity components** with regex `^[a-zA-Z0-9][a-zA-Z0-9_-]*$`, max 128 chars, no colons or dots within components. Include descriptive error messages identifying which component failed.
12. **DO add `AggregateIdentity` computed property** to CommandEnvelope -- prevents manual construction errors in Epics 2-3
13. **DO validate DomainResult factory methods** -- `Success()` rejects empty lists and IRejectionEvent instances; `Rejection()` rejects non-IRejectionEvent instances

### Project Structure Notes

- Alignment with architecture document project structure: EXACT match required per architecture `Contracts/Events/`, `Contracts/Commands/`, `Contracts/Identity/`, `Contracts/Results/`
- The `Serialization/` folder shown in the architecture doc is deferred -- EventSerializer may need implementation details not appropriate for this story
- The `Identity/IdentityParser.cs` shown in architecture is intentionally merged into `AggregateIdentity.Parse()` -- a separate parser file adds no value when parsing logic is trivial and belongs on the type itself
- `EventMetadata.cs` is added per architecture doc (`Events/EventMetadata.cs` explicitly listed in project structure)
- `CommandStatusRecord.cs` is added per architecture D2 annotation ("Status enum + record")
- No conflicts with Story 1.1 output -- Contracts project is clean except for BuildVerification.cs placeholder

### References

- [Source: _bmad-output/planning-artifacts/architecture.md#Data Architecture - D1 Event Storage Strategy, key patterns]
- [Source: _bmad-output/planning-artifacts/architecture.md#Core Architectural Decisions - D2 Command Status, D3 Domain Error Contract]
- [Source: _bmad-output/planning-artifacts/architecture.md#Naming Patterns - DAPR state store keys, pub/sub topics, event naming]
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries - Contracts directory structure]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines - Rules #1, #2, #8, #11]
- [Source: _bmad-output/planning-artifacts/architecture.md#Security Constraints - SEC-1 EventStore owns envelope metadata]
- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.2 - Acceptance Criteria and BDD scenarios]
- [Source: _bmad-output/planning-artifacts/prd.md - FR11 (event envelope 11 fields), FR26 (canonical identity tuple)]
- [Source: _bmad-output/implementation-artifacts/1-1-solution-structure-and-build-infrastructure.md - Previous story learnings]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
