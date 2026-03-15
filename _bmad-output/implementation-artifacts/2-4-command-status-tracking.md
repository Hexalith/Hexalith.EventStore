# Story 2.4: Command Status Tracking

Status: done

## Story

As an API consumer,
I want command processing status tracked through a checkpointed state machine,
So that I can query the lifecycle stage of any submitted command.

## Acceptance Criteria

1. **Given** a command enters the pipeline, **When** the API layer receives it, **Then** status is written as `Received` at key `{tenant}:{correlationId}:status` (D2) **And** subsequent transitions (Processing, EventsStored) are checkpointed inside the actor via PipelineState **And** all transitions (Processing, EventsStored, EventsPublished, Completed) are written as advisory status via WriteAdvisoryStatusAsync.

2. **Given** a terminal status is reached, **When** the status is Completed, Rejected, PublishFailed, or TimedOut, **Then** the status entry includes stage, timestamp, aggregate ID, and terminal-specific detail (event count, rejection type, failure reason, or timeout duration) **And** a default 24-hour TTL is set via DAPR `ttlInSeconds` metadata.

3. **Given** status write fails, **When** the state store is temporarily unavailable, **Then** the command pipeline continues without blocking (Rule 12).

4. All Tier 1 tests pass. Tier 2 DaprCommandStatusStore, ActorStateMachine, AggregateActor status-related, and SubmitCommandHandler status tests pass (`DaprCommandStatusStoreTests`, `ActorStateMachineTests`, `SubmitCommandHandlerStatusTests`, `CommandStatusControllerTests`).

5. **Done definition:** DaprCommandStatusStore verified to write status at key `{tenant}:{correlationId}:status` with 24-hour TTL via `ttlInSeconds` metadata. SubmitCommandHandler verified to write `Received` status before actor invocation (advisory per Rule 12). AggregateActor verified to write advisory status at each stage transition: Processing, EventsStored, EventsPublished, Completed/Rejected/PublishFailed. ActorStateMachine verified to checkpoint PipelineState via IActorStateManager for crash-recovery resume. WriteAdvisoryStatusAsync verified to catch and log failures without blocking (Rule 12). CommandStatusRecord verified to include terminal-specific fields. InMemoryCommandStatusStore verified for testing use. All required tests green. Each verification recorded as pass/fail in Completion Notes.

## Implementation State: VERIFICATION STORY

The Command Status Tracking infrastructure was implemented under the old epic structure. This story **verifies existing code** against the new Epic 2 acceptance criteria and fills any gaps found. Do NOT re-implement existing components.

**Gap-filling is authorized:** While this is a verification story, every task includes "if any gap found, implement the fix and add test coverage." Writing new tests or fixing code to close gaps IS part of verification scope. Do not skip implementation subtasks because the story header says "verification."

**Conflict resolution policy:** If existing code conflicts with the new acceptance criteria, the new AC takes precedence -- modify the code to comply.

**CRITICAL: Verify from source code, not from this story.** The Dev Notes section contains implementation details and code snippets for context, but these may be stale. For every PASS/FAIL verdict, read the actual `.cs` file directly. Do NOT mark tasks PASS based solely on the story's description of the code.

### Story 2.4 Scope -- Components to Verify

These components are owned by THIS story (command status tracking + checkpointed state machine):

| Component | File | Verify |
|-----------|------|--------|
| `CommandStatus` enum | `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs` | 8 states with stable int assignments |
| `CommandStatusRecord` | `src/Hexalith.EventStore.Contracts/Commands/CommandStatusRecord.cs` | Terminal-specific fields (EventCount, RejectionEventType, FailureReason, TimeoutDuration) |
| `ICommandStatusStore` | `src/Hexalith.EventStore.Server/Commands/ICommandStatusStore.cs` | Interface: WriteStatusAsync + ReadStatusAsync |
| `DaprCommandStatusStore` | `src/Hexalith.EventStore.Server/Commands/DaprCommandStatusStore.cs` | DAPR SaveState with TTL, advisory read error handling |
| `CommandStatusConstants` | `src/Hexalith.EventStore.Server/Commands/CommandStatusConstants.cs` | Key format `{tenant}:{correlationId}:status`, default TTL 86400 |
| `CommandStatusOptions` | `src/Hexalith.EventStore.Server/Commands/CommandStatusOptions.cs` | TtlSeconds + StateStoreName config |
| `PipelineState` | `src/Hexalith.EventStore.Server/Actors/PipelineState.cs` | In-actor checkpoint record |
| `ActorStateMachine` | `src/Hexalith.EventStore.Server/Actors/ActorStateMachine.cs` | Checkpoint/Load/Cleanup via IActorStateManager |
| AggregateActor status writes | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` | WriteAdvisoryStatusAsync at each stage, Rule 12 compliance |
| AggregateActor `CompleteTerminalAsync` | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (search: `private async Task<CommandProcessingResult> CompleteTerminalAsync`) | Terminal status with event count and rejection type |
| SubmitCommandHandler `Received` write | `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs` | Advisory Received status before routing, Rule 12 |
| `CommandStatusController` | `src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs` | Tenant-scoped query (SEC-3) |
| `CommandStatusResponse` | `src/Hexalith.EventStore.CommandApi/Models/CommandStatusResponse.cs` | FromRecord factory, ISO 8601 duration |
| `InMemoryCommandStatusStore` | `src/Hexalith.EventStore.Testing/Fakes/InMemoryCommandStatusStore.cs` | Test fake with history tracking + TTL simulation |
| DI Registration | `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` (lines 101-104) | ICommandStatusStore -> DaprCommandStatusStore, CommandStatusOptions bound |

### Out of Scope (Other Stories)

Do NOT verify these -- they belong to other stories:
- Command routing / actor skeleton (Story 2.1 -- done)
- Event persistence / `EventPersister` (Story 2.2 -- review)
- State rehydration / domain service invocation (Story 2.3 -- ready-for-dev)
- Idempotency checking / `IdempotencyChecker` (Story 2.5)
- Event publishing / `EventPublisher` (Story 4.1)
- Command status query endpoint API contract (Story 3.3) -- this story verifies the **tracking infrastructure and controller logic**; Story 3.3 owns the HTTP contract (status codes, RFC 7807 format, Retry-After headers, OpenAPI spec, endpoint routing conventions)
- Dead-letter routing infrastructure (Story 3.4)

### Existing Test Files

| Test File | Covers | Tier |
|-----------|--------|------|
| `DaprCommandStatusStoreTests.cs` | Write key pattern, TTL metadata, read success/null/error, exception propagation | Tier 2 |
| `ActorStateMachineTests.cs` | Checkpoint, load, cleanup, key pattern, overwrite, nonexistent key | Tier 2 |
| `SubmitCommandHandlerStatusTests.cs` | Received status write, Rule 12 failure tolerance, warning logging | Tier 2 |
| `CommandStatusControllerTests.cs` | 200/404/403/400, tenant scoping, multi-tenant, terminal field inclusion | Tier 2 |
| `CommandStatusTests.cs` | Enum values and serialization | Tier 1 |
| `CommandStatusRecordTests.cs` | Record construction and equality | Tier 1 |

## Prerequisites

- **DAPR slim init required** for Tier 2 tests: run `dapr init --slim` before starting any verification task that touches Server.Tests
- **Story ordering note:** Story 2.3 (State Rehydration & Domain Service Invocation) is `ready-for-dev` and may modify `AggregateActor.cs` if gaps are found. If Story 2.3 is executed before this story, re-verify method search anchors in Task 4 before starting. Method-name searches (not line numbers) are used throughout to mitigate this risk.

## Tasks / Subtasks

Each verification subtask must be recorded as PASS or FAIL in the Completion Notes section.

- [x] Task 1: Verify DaprCommandStatusStore write path (AC #1, #2)
  - [x] 1.1 Read `DaprCommandStatusStore.cs`. Confirm `WriteStatusAsync()` builds key via `CommandStatusConstants.BuildKey(tenantId, correlationId)` producing `{tenant}:{correlationId}:status` (D2). Record PASS/FAIL
  - [x] 1.2 Confirm TTL metadata: `ttlInSeconds` set from `CommandStatusOptions.TtlSeconds` (default 86400 = 24 hours per AC #2). Record PASS/FAIL
  - [x] 1.3 Confirm `DaprClient.SaveStateAsync` called with state store name, key, record, and metadata. Record PASS/FAIL
  - [x] 1.4 Confirm `WriteStatusAsync` propagates exceptions to caller (advisory handling is caller's responsibility per method remarks). Record PASS/FAIL
  - [x] 1.5 Confirm guard clauses: `ArgumentException.ThrowIfNullOrWhiteSpace` for tenantId and correlationId, `ArgumentNullException.ThrowIfNull` for status. Record PASS/FAIL
  - [x] 1.6 If any write path gap found, implement the fix and add test coverage

- [x] Task 2: Verify DaprCommandStatusStore read path (AC #1)
  - [x] 2.1 Read `DaprCommandStatusStore.cs` `ReadStatusAsync()`. Confirm it reads from DAPR state store using same key pattern. Record PASS/FAIL
  - [x] 2.2 Confirm error handling: catches all exceptions except `OperationCanceledException`, logs warning, returns null (graceful degradation). Record PASS/FAIL
  - [x] 2.3 Read `DaprCommandStatusStoreTests.cs`. Count test methods. Confirm coverage of: write with correct key, TTL metadata value, exception propagation on write, read existing, read nonexistent (null), read failure returns null. Record PASS/FAIL with test count
  - [x] 2.4 If any read path gap found, implement the fix and add test coverage

- [x] Task 3: Verify SubmitCommandHandler "Received" status write (AC #1, #3)
  - [x] 3.1 Read `SubmitCommandHandler.cs`. Confirm `Handle()` writes `CommandStatus.Received` status to `ICommandStatusStore` before calling `commandRouter.RouteCommandAsync()`. Record PASS/FAIL
  - [x] 3.2 Confirm the `CommandStatusRecord` includes: Status=Received, Timestamp=UtcNow, AggregateId=request.AggregateId. Confirm terminal-specific fields (EventCount, RejectionEventType, FailureReason, TimeoutDuration) are null for non-terminal state. Record PASS/FAIL
  - [x] 3.3 Confirm Rule 12 compliance: status write is wrapped in try/catch, catches all exceptions except `OperationCanceledException`, logs warning via `Log.StatusWriteFailed`, continues pipeline execution. Record PASS/FAIL
  - [x] 3.4 Read `SubmitCommandHandlerStatusTests.cs`. Count test methods. Confirm coverage of: Received status written, status write failure still returns result (Rule 12), warning logged on failure. Record PASS/FAIL with test count
  - [x] 3.5 **Pre-existing failure warning:** 4 tests in `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs` fail with NullReferenceException at line 81 (PayloadProtectionTests, CausationIdLoggingTests, LogLevelConventionTests, StructuredLoggingCompletenessTests). These are Pipeline-scope failures, NOT status-scope. Only `SubmitCommandHandlerStatusTests` in the `Commands/` folder are in scope for this story. Do not investigate Pipeline failures.
  - [x] 3.6 If any handler gap found, implement the fix and add test coverage

- [x] Task 4: Verify AggregateActor advisory status writes (AC #1, #3)
  - [x] 4.1 Read `AggregateActor.cs` `WriteAdvisoryStatusAsync()` (search: `private async Task WriteAdvisoryStatusAsync`). Confirm it wraps `commandStatusStore.WriteStatusAsync()` in try/catch, catches all exceptions except `OperationCanceledException`, logs warning, never throws (Rule 12). Record PASS/FAIL
  - [x] 4.2 Trace the full status write sequence through the actor pipeline. Search for each `WriteAdvisoryStatusAsync` call site and confirm these advisory writes occur in order:
    - `Processing` status written after tenant validation, before state rehydration (search: `WriteAdvisoryStatusAsync(command, CommandStatus.Processing)`)
    - `EventsStored` status written after atomic SaveStateAsync commit of events (search: `WriteAdvisoryStatusAsync(command, CommandStatus.EventsStored)`)
    - `EventsPublished` status written after successful event publication (search: `WriteAdvisoryStatusAsync(command, CommandStatus.EventsPublished)`)
    - Terminal status (Completed or Rejected) written in `CompleteTerminalAsync()` (search: `private async Task<CommandProcessingResult> CompleteTerminalAsync`)
    - `PublishFailed` terminal status written on publication failure (search: `WriteAdvisoryStatusAsync(\n.*command,\n.*CommandStatus.PublishFailed`)
    Record PASS/FAIL
  - [x] 4.3 Confirm `WriteAdvisoryStatusAsync` passes terminal-specific fields: `eventCount` for Completed, `rejectionEventType` for Rejected, `failureReason` for PublishFailed. Confirm `TimeoutDuration` is always null -- `TimedOut` status exists in the enum (UX-DR16) but v1 has no timeout detection mechanism; no code path produces this status. This is by-design, not a gap. Record PASS/FAIL
  - [x] 4.4 Confirm `CompleteTerminalAsync()` (search: `private async Task<CommandProcessingResult> CompleteTerminalAsync`) determines terminal status: `accepted ? CommandStatus.Completed : CommandStatus.Rejected`. Confirm it passes event count and rejection type to the advisory write. Record PASS/FAIL
  - [x] 4.5 Confirm `HandleInfrastructureFailureAsync()` (search: `private async Task<CommandProcessingResult> HandleInfrastructureFailureAsync`) writes `CommandStatus.Rejected` advisory status with failure reason on infrastructure errors at Steps 3-5. Record PASS/FAIL
  - [x] 4.6 Verify or add an isolated test proving `WriteAdvisoryStatusAsync` swallows exceptions without blocking the pipeline (Rule 12 at actor level). This is distinct from `SubmitCommandHandlerStatusTests` which tests Rule 12 at the handler level. The actor's `WriteAdvisoryStatusAsync` has its own try/catch and must be independently verified. If no such test exists in `AggregateActorTests`, add one. Record PASS/FAIL
  - [x] 4.7 If any advisory write gap found, implement the fix and add test coverage

- [x] Task 5: Verify ActorStateMachine checkpoint mechanism (AC #1)
  - [x] 5.1 Read `ActorStateMachine.cs`. Confirm `CheckpointAsync()` stores `PipelineState` at key `{pipelineKeyPrefix}{correlationId}` via `IActorStateManager.SetStateAsync()` (staging only, no SaveStateAsync). Record PASS/FAIL
  - [x] 5.2 Confirm `LoadPipelineStateAsync()` reads pipeline state via `TryGetStateAsync<PipelineState>()` for crash-recovery resume. Record PASS/FAIL
  - [x] 5.3 Confirm `CleanupPipelineAsync()` removes pipeline state key via `TryRemoveStateAsync()`. Record PASS/FAIL
  - [x] 5.4 Confirm AggregateActor checkpoints Processing state (search: `new PipelineState(` near `CommandStatus.Processing`) and commits with `SaveStateAsync()` before continuing. Confirm EventsStored checkpoint (search: `new PipelineState(` near `CommandStatus.EventsStored`) is committed atomically with events via the same SaveStateAsync. Note: only Processing and EventsStored are checkpointed via PipelineState. EventsPublished and terminal states are advisory writes only (not checkpointed). Record PASS/FAIL
  - [x] 5.5 Confirm AggregateActor resume detection (search: `LoadPipelineStateAsync` in `ProcessCommandAsync`): loads existing pipeline state, resumes from EventsStored if found (search: `ResumeFromEventsStoredAsync`), cleans up stale Processing state if found. Record PASS/FAIL
  - [x] 5.6 Read `ActorStateMachineTests.cs`. Count test methods. Confirm coverage of: checkpoint stores correct state, load existing/nonexistent, cleanup removes key, key pattern convention, checkpoint overwrites, cleanup nonexistent does not throw. Record PASS/FAIL with test count
  - [x] 5.7 If any checkpoint gap found, implement the fix and add test coverage

- [x] Task 6: Verify PipelineState and CommandStatusRecord contracts (AC #2)
  - [x] 6.1 Read `PipelineState.cs`. Confirm record has: CorrelationId, CurrentStage (CommandStatus), CommandType, StartedAt (DateTimeOffset), EventCount (int?), RejectionEventType (string?), ResultPayload (string?). Record PASS/FAIL
  - [x] 6.2 Read `CommandStatusRecord.cs`. Confirm record has: Status (CommandStatus), Timestamp (DateTimeOffset), AggregateId (string?), EventCount (int?), RejectionEventType (string?), FailureReason (string?), TimeoutDuration (TimeSpan?). Confirm terminal-specific fields match AC #2 requirements. Record PASS/FAIL
  - [x] 6.3 Read `CommandStatus.cs`. Confirm 8 states with stable integer assignments: Received=0, Processing=1, EventsStored=2, EventsPublished=3, Completed=4, Rejected=5, PublishFailed=6, TimedOut=7 (UX-DR16). Record PASS/FAIL
  - [x] 6.4 Read `CommandStatusConstants.cs`. Confirm `BuildKey()` produces `{tenantId}:{correlationId}:status`. Confirm `DefaultTtlSeconds = 86400` and `DefaultStateStoreName = "statestore"`. Record PASS/FAIL
  - [x] 6.5 Read `CommandStatusOptions.cs`. Confirm TtlSeconds defaults to 86400 and StateStoreName defaults to "statestore". Record PASS/FAIL
  - [x] 6.6 Check edge-case behavior for `CommandStatusOptions.TtlSeconds` with value <= 0. Determine if DAPR state store rejects, ignores, or immediately expires entries with invalid TTL. Note finding but do NOT add validation unless a real misconfiguration risk exists (low risk -- config binding from `appsettings.json` with sensible defaults). Record observation
  - [x] 6.7 If any contract gap found, implement the fix

- [x] Task 7: Verify CommandStatusResponse and Controller (AC #2)
  - [x] 7.1 Read `CommandStatusResponse.cs`. Confirm `FromRecord()` maps all fields from `CommandStatusRecord` and converts `TimeoutDuration` to ISO 8601 string via `XmlConvert.ToString()`. Record PASS/FAIL
  - [x] 7.2 Read `CommandStatusController.cs`. Confirm tenant-scoped access: extracts `eventstore:tenant` claims, iterates authorized tenants, returns 404 for unauthorized/missing (SEC-3). Record PASS/FAIL
  - [x] 7.3 Read `CommandStatusControllerTests.cs`. Count test methods. Confirm coverage of: existing status 200, nonexistent 404, tenant mismatch 404, no tenant claims 403, multi-tenant search, completed event count, rejected type, rejected failure reason, timed out ISO 8601 duration, whitespace correlation ID 400. Record PASS/FAIL with test count
  - [x] 7.4 If any controller/response gap found, implement the fix and add test coverage

- [x] Task 8: Verify InMemoryCommandStatusStore test fake (AC #1)
  - [x] 8.1 Read `InMemoryCommandStatusStore.cs`. Confirm it implements `ICommandStatusStore`, uses `ConcurrentDictionary`, simulates TTL expiry, tracks write history via `GetStatusHistory()`. Record PASS/FAIL
  - [x] 8.2 Confirm guard clauses match `DaprCommandStatusStore`: null/whitespace checks on tenantId, correlationId, and status. Record PASS/FAIL
  - [x] 8.3 Confirm `GetAllStatuses()` and `GetStatusCount()` exist for test assertions. Confirm `Clear()` method exists for test cleanup. Record PASS/FAIL
  - [x] 8.4 Verify TTL simulation correctness: the fake uses `DateTimeOffset.UtcNow.AddSeconds(TtlSeconds)` at write time and checks `entry.Expiry <= DateTimeOffset.UtcNow` at read time. Confirm expired entries return null. If no dedicated `InMemoryCommandStatusStoreTests.cs` exists, note as accepted risk (the fake is exercised indirectly by `SubmitCommandHandlerStatusTests` and `CommandStatusControllerTests`). Record PASS/FAIL
  - [x] 8.5 If any test fake gap found, implement the fix

- [x] Task 9: Verify DI registration (AC #1)
  - [x] 9.1 Read `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` (lines 101-104). Confirm:
    - `CommandStatusOptions` bound to `EventStore:CommandStatus` configuration section
    - `ICommandStatusStore -> DaprCommandStatusStore` registered as Singleton
    Record PASS/FAIL
  - [x] 9.2 Confirm AggregateActor constructor accepts `ICommandStatusStore commandStatusStore` parameter and it resolves from DI. Record PASS/FAIL
  - [x] 9.3 Verify DaprClient lifetime compatibility: `ICommandStatusStore` is registered as Singleton, and its constructor takes `DaprClient`. Confirm `DaprClient` is also Singleton (registered via `AddDaprClient()` which defaults to Singleton). A Singleton capturing a Scoped/Transient dependency is a production bug (captive dependency). Record PASS/FAIL
  - [x] 9.4 If any registration gap found, fix it

- [x] Task 10: Build and run tests (AC #4)
  - [x] 10.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- zero warnings. Record PASS/FAIL
  - [x] 10.2 Run Tier 1: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` + `Client.Tests` + `Sample.Tests` + `Testing.Tests` -- all pass. Record PASS/FAIL with counts
  - [x] 10.3 Run Tier 2 Story 2.4 scope tests (requires `dapr init --slim`): `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~DaprCommandStatusStore|FullyQualifiedName~ActorStateMachine|FullyQualifiedName~SubmitCommandHandlerStatus|FullyQualifiedName~CommandStatusController|FullyQualifiedName~AggregateActor"` -- all pass. Record PASS/FAIL with counts. Note: `AggregateActor` tests are included because Task 4.6 verifies/adds the Rule 12 advisory write isolation test in that test class
  - [x] 10.4 If any test fails, investigate root cause and fix only if failure is within Story 2.4 scope (status tracking, advisory writes, checkpointed state machine). Log out-of-scope failures for later stories

## Dev Notes

### Scope Summary

This is a **verification story**. The Command Status Tracking infrastructure was fully implemented under the old epic numbering (prior to the 2026-03-15 epic restructure). The developer's job is to: read the existing code, confirm it meets the acceptance criteria, record PASS/FAIL for each verification, identify any gaps, fix them, and confirm tests pass.

The migration note in `sprint-status.yaml` explains: "Many requirements covered by the new stories have already been implemented under the old structure."

**Scope boundary:** This story owns command status tracking (DaprCommandStatusStore, WriteAdvisoryStatusAsync, SubmitCommandHandler Received write), the checkpointed state machine (ActorStateMachine, PipelineState), and all contract types (CommandStatusRecord, CommandStatus enum, CommandStatusConstants, CommandStatusOptions). Command routing (2.1), event persistence (2.2), state rehydration (2.3), idempotency (2.5), and the query API contract details (3.3) are verified by their own stories.

### Architecture Constraints (MUST FOLLOW)

- **D2:** Command status storage at dedicated key `{tenant}:{correlationId}:status` in DAPR state store. Lifecycle: `Received -> Processing -> EventsStored -> EventsPublished -> Completed | Rejected | PublishFailed | TimedOut`
- **Rule 12:** Command status writes are advisory -- failure to write/update status must never block or fail the command processing pipeline. Status is ephemeral metadata, not a source of truth
- **Rule 6:** `IActorStateManager` for ALL actor state operations -- ActorStateMachine uses SetStateAsync/TryGetStateAsync/TryRemoveStateAsync (staging only, never SaveStateAsync)
- **SEC-3:** Command status queries are tenant-scoped (CommandStatusController JWT tenant match)
- **UX-DR16:** `CommandStatus` enum with exactly 8 states: Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, TimedOut
- **UX-DR27:** Shared lifecycle model -- `CommandStatus` values identical in API `status` field and SDK enum
- **NFR25:** Checkpointed state machine for crash-recovery resume

### TimedOut Status -- By-Design Gap

The `CommandStatus.TimedOut = 7` enum value exists per UX-DR16 but **no code path in v1 produces this status**. This is by-design: timeout detection requires infrastructure (e.g., actor reminder-based watchdog or API-layer timeout tracking) that is not yet implemented. The enum value is reserved for forward compatibility. The dev should NOT attempt to implement timeout detection as part of this story, and should NOT flag the absence of a `TimedOut` producer as a gap.

### Two Distinct "Status" Concepts -- Do Not Confuse

This story covers two related but separate mechanisms. Confusing them is the #1 dev mistake risk:

| Concept | Type | Storage | Purpose | Advisory? |
|---------|------|---------|---------|-----------|
| **PipelineState** (checkpoint) | `PipelineState` record | Actor state via `IActorStateManager` | Crash-recovery resume inside the actor | No -- failure is fatal (command fails) |
| **CommandStatusRecord** (advisory) | `CommandStatusRecord` record | External DAPR state store via `DaprClient` | API consumer queries lifecycle stage | Yes -- failure is swallowed (Rule 12) |

PipelineState is an **internal implementation detail** for actor reliability. CommandStatusRecord is an **external-facing convenience cache** for API consumers. They track similar stages but serve fundamentally different purposes and have opposite failure semantics.

**Anti-pattern warning:** Never read CommandStatusRecord to make pipeline decisions. Status is ephemeral metadata with TTL, not a source of truth. The event stream is authoritative.

### Key Implementation Details

**Status Write Flow -- API Layer (SubmitCommandHandler):**
```
1. SubmitCommandHandler.Handle() receives SubmitCommand
2. try { statusStore.WriteStatusAsync(tenant, correlationId, Received) }
   catch { Log.StatusWriteFailed(); /* continue -- Rule 12 */ }
3. archiveStore.WriteCommandAsync() (advisory)
4. commandRouter.RouteCommandAsync() (NOT advisory -- failure propagates)
```

**Status Write Flow -- Actor Pipeline (AggregateActor):**
```
Step 1: Idempotency check (no status write)
Step 2: Tenant validation (no status write)
Checkpoint Processing -> SaveStateAsync() -> WriteAdvisoryStatusAsync(Processing)
Step 3: State rehydration
Step 4: Domain service invocation
Step 5: Event persistence + atomic SaveStateAsync()
  -> WriteAdvisoryStatusAsync(EventsStored)
  -> Event publication
  -> WriteAdvisoryStatusAsync(EventsPublished)
  -> CompleteTerminalAsync() -> WriteAdvisoryStatusAsync(Completed | Rejected)
  OR -> WriteAdvisoryStatusAsync(PublishFailed) on publication failure
```

**Checkpointed State Machine (ActorStateMachine):**
```
CheckpointAsync(prefix, PipelineState) -> IActorStateManager.SetStateAsync (staging)
LoadPipelineStateAsync(prefix, corrId) -> IActorStateManager.TryGetStateAsync (resume)
CleanupPipelineAsync(prefix, corrId) -> IActorStateManager.TryRemoveStateAsync (terminal)

Key pattern: {tenant}:{domain}:{aggId}:pipeline:{correlationId}
Derived from AggregateIdentity.PipelineKeyPrefix

Crash-recovery resume:
  - If PipelineState found at EventsStored: skip re-persistence, resume from publication
  - If PipelineState found at Processing: clean up, reprocess from scratch
  - If no PipelineState: normal processing
```

**Advisory Write Pattern (WriteAdvisoryStatusAsync):**
```csharp
try {
    await commandStatusStore.WriteStatusAsync(
        command.TenantId, command.CorrelationId,
        new CommandStatusRecord(status, UtcNow, aggregateId, eventCount, rejectionType, failureReason, timeoutDuration));
}
catch (OperationCanceledException) { throw; }
catch (Exception ex) {
    logger.LogWarning(ex, "Advisory status write failed: CorrelationId={}, Status={}", ...);
    // Rule #12: swallow -- never block pipeline
}
```

**DAPR State Store Key Patterns:**

| Key Pattern | Convention | Example |
|------------|-----------|---------|
| Status | `{tenant}:{correlationId}:status` | `acme:corr-abc123:status` |
| Pipeline | `{tenant}:{domain}:{aggId}:pipeline:{correlationId}` | `acme:payments:order-123:pipeline:corr-abc123` |

### Key Interfaces

```csharp
public interface ICommandStatusStore {
    Task WriteStatusAsync(string tenantId, string correlationId, CommandStatusRecord status, CancellationToken ct);
    Task<CommandStatusRecord?> ReadStatusAsync(string tenantId, string correlationId, CancellationToken ct);
}

public interface IActorStateMachine {
    Task CheckpointAsync(string pipelineKeyPrefix, PipelineState state);
    Task<PipelineState?> LoadPipelineStateAsync(string pipelineKeyPrefix, string correlationId);
    Task CleanupPipelineAsync(string pipelineKeyPrefix, string correlationId);
}

public record PipelineState(
    string CorrelationId,
    CommandStatus CurrentStage,
    string CommandType,
    DateTimeOffset StartedAt,
    int? EventCount,
    string? RejectionEventType,
    string? ResultPayload = null);

public record CommandStatusRecord(
    CommandStatus Status,
    DateTimeOffset Timestamp,
    string? AggregateId,
    int? EventCount,
    string? RejectionEventType,
    string? FailureReason,
    TimeSpan? TimeoutDuration);
```

### Dependencies (from Directory.Packages.props)

- Dapr.Client: 1.16.1
- Dapr.Actors: 1.16.1
- Dapr.Actors.AspNetCore: 1.16.1
- xUnit: 2.9.3, NSubstitute: 5.3.0, Shouldly: 4.3.0

**Note:** `CLAUDE.md` lists DAPR SDK 1.17.0 but `Directory.Packages.props` pins 1.16.1. The .props file is the source of truth. Do not upgrade DAPR SDK as part of this story.

### Previous Story Intelligence (Story 2.3)

Story 2.3 verified state rehydration and domain service invocation. Key learnings:
- Story 2.3 was a verification story -- same approach applies here
- Build must produce zero warnings (`TreatWarningsAsErrors = true`)
- 15 pre-existing out-of-scope failures exist: 4 SubmitCommandHandler NullRef (Pipeline), 1 validator, 10 auth integration (Epic 5)
- Tier 1: 652 tests (Contracts 267 + Client 286 + Testing 67 + Sample 32)
- Tier 2 scope tests are filtered by `--filter "FullyQualifiedName~..."` to isolate from out-of-scope failures

### Previous Story Intelligence (Story 2.2)

Story 2.2 verified event persistence. Key learnings:
- EventPersister is created per-call (not DI-registered) -- ActorStateMachine follows same pattern
- SaveStateAsync never called by per-call helpers (EventPersister, ActorStateMachine) -- only by AggregateActor
- 2 test gaps fixed (EventDeserializationException + 15-field metadata test)

### Previous Story Intelligence (Story 2.1)

Story 2.1 verified AggregateActor and CommandRouter. Key learnings:
- ICommandStatusStore is registered in CommandApi (not Server) -- architecturally correct since CommandApi hosts the actor runtime
- CausationId = MessageId (not CorrelationId) per `ToCommandEnvelope()` implementation
- AggregateActor is a 5-step thin orchestrator; status writes happen between steps
- DI registration: all actor constructor parameters confirmed to resolve

### Git Intelligence

Recent commits show Epic 1 complete through 1.5, Story 2.1 done, 2.2 in review:
- `b9a4e23` Refactor command handling and improve test assertions
- `fc46ddd` feat: Implement Story 1.5 -- CommandStatus enum, ITerminatable, tombstoning
- `4b122e5` feat: Implement Story 1.4 -- Pure Function Contract & EventStoreAggregate Base
- `493bcd8` feat: Epic 1 Stories 1.1, 1.2, 1.3 -- Domain Contract Foundation

### Project Structure Notes

- Server project at `src/Hexalith.EventStore.Server/` -- feature-folder organization (Rule 2): Actors/, Commands/, DomainServices/, Events/, Pipeline/, Queries/, Projections/, Configuration/
- Server.Tests at `tests/Hexalith.EventStore.Server.Tests/` -- mirrors Server structure
- CommandApi at `src/Hexalith.EventStore.CommandApi/` -- Controllers/, Models/, Extensions/, Middleware/
- Status store files in `Commands/` subfolder in Server and Server.Tests
- Actor/pipeline files in `Actors/` subfolder in Server and Server.Tests
- Handler files in `Pipeline/` subfolder in Server, handler status tests in `Commands/` in Server.Tests
- Testing fakes at `src/Hexalith.EventStore.Testing/Fakes/`
- InternalsVisibleTo: CommandApi, Server.Tests, Testing, Testing.Tests
- Server references: Client + Contracts (no circular dependencies)

### File Conventions

- **Namespaces:** File-scoped (`namespace X.Y.Z;`)
- **Braces:** Allman style (new line before opening brace)
- **Private fields:** `_camelCase`
- **Async methods:** `Async` suffix
- **4 spaces** indentation, CRLF, UTF-8
- **Nullable:** enabled globally
- **XML docs:** on all public types (UX-DR19)

### References

- [Source: _bmad-output/planning-artifacts/epics.md -- Epic 2, Story 2.4]
- [Source: _bmad-output/planning-artifacts/architecture.md -- D2, Rule 12, SEC-3, UX-DR16, UX-DR27, NFR25]
- [Source: src/Hexalith.EventStore.Server/Commands/DaprCommandStatusStore.cs -- DAPR status implementation]
- [Source: src/Hexalith.EventStore.Server/Commands/ICommandStatusStore.cs -- interface contract]
- [Source: src/Hexalith.EventStore.Server/Commands/CommandStatusConstants.cs -- key format and defaults]
- [Source: src/Hexalith.EventStore.Server/Commands/CommandStatusOptions.cs -- configuration]
- [Source: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs -- WriteAdvisoryStatusAsync, CompleteTerminalAsync]
- [Source: src/Hexalith.EventStore.Server/Actors/ActorStateMachine.cs -- checkpoint mechanism]
- [Source: src/Hexalith.EventStore.Server/Actors/PipelineState.cs -- pipeline checkpoint record]
- [Source: src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs -- Received status write]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs -- query endpoint]
- [Source: src/Hexalith.EventStore.CommandApi/Models/CommandStatusResponse.cs -- API response model]
- [Source: src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs -- enum definition]
- [Source: src/Hexalith.EventStore.Contracts/Commands/CommandStatusRecord.cs -- record definition]
- [Source: src/Hexalith.EventStore.Testing/Fakes/InMemoryCommandStatusStore.cs -- test fake]
- [Source: _bmad-output/implementation-artifacts/2-1-aggregate-actor-and-command-routing.md -- Story 2.1 learnings]
- [Source: _bmad-output/implementation-artifacts/2-2-event-persistence-and-sequence-numbers.md -- Story 2.2 learnings]
- [Source: _bmad-output/implementation-artifacts/2-3-state-rehydration-and-domain-service-invocation.md -- Story 2.3 learnings]

## Dev Agent Record

### Agent Model Used

GPT-5.4

### Debug Log References

- 2026-03-15: Senior review follow-up removed GUID-only correlation ID validation from status queries, clarified `Rejected` contract semantics for infrastructure failures, added regression coverage, and reran Story 2.4 targeted server-side tests.

### Completion Notes List

**Senior review follow-up:** Fixed the two HIGH findings and the MEDIUM bookkeeping issue from code review. Status queries now accept the repository's ULID/string correlation IDs, rejected infrastructure failures are explicitly modeled via `FailureReason` with `RejectionEventType = null`, and story verification notes now match the actual test coverage that was rerun.

**Task 1: DaprCommandStatusStore write path** -- ALL PASS
- 1.1 PASS: `WriteStatusAsync()` line 32 calls `CommandStatusConstants.BuildKey(tenantId, correlationId)` producing `{tenant}:{correlationId}:status`
- 1.2 PASS: TTL metadata `ttlInSeconds` set from `opts.TtlSeconds` (line 37), default 86400 via `CommandStatusConstants.DefaultTtlSeconds`
- 1.3 PASS: `daprClient.SaveStateAsync(opts.StateStoreName, key, status, metadata: metadata)` at line 40-45
- 1.4 PASS: No try/catch in `WriteStatusAsync` -- exceptions propagate to caller. Method remarks document this design
- 1.5 PASS: Guard clauses at lines 28-30: `ArgumentException.ThrowIfNullOrWhiteSpace` for tenantId/correlationId, `ArgumentNullException.ThrowIfNull` for status
- 1.6 No gaps found

**Task 2: DaprCommandStatusStore read path** -- ALL PASS
- 2.1 PASS: `ReadStatusAsync()` at line 55 uses `CommandStatusConstants.BuildKey(tenantId, correlationId)` then `daprClient.GetStateAsync<CommandStatusRecord>(opts.StateStoreName, key)`
- 2.2 PASS: Lines 71-80: catches `OperationCanceledException` (rethrows), catches all other exceptions (logs warning, returns null)
- 2.3 PASS: 6 test methods covering: write correct key, TTL metadata, exception propagation on write, read existing, read null, read failure returns null
- 2.4 No gaps found

**Task 3: SubmitCommandHandler "Received" status write** -- ALL PASS
- 3.1 PASS: `Handle()` writes `CommandStatus.Received` at lines 37-48 before `commandRouter.RouteCommandAsync()` at line 78
- 3.2 PASS: `CommandStatusRecord(Received, UtcNow, request.AggregateId, null, null, null, null)` -- all terminal fields null
- 3.3 PASS: try/catch at lines 35-57: catches `OperationCanceledException` (rethrows), catches all others (calls `Log.StatusWriteFailed`, continues)
- 3.4 PASS: 3 test methods: Received status written, failure still returns result (Rule 12), warning logged on failure
- 3.5 Acknowledged: 4 out-of-scope Pipeline test failures -- not investigated per story scope
- 3.6 No gaps found

**Task 4: AggregateActor advisory status writes** -- ALL PASS (1 gap filled)
- 4.1 PASS: `WriteAdvisoryStatusAsync` at line 1347: try/catch wraps `commandStatusStore.WriteStatusAsync()`, catches `OperationCanceledException` (rethrows), catches all others (logs warning via `logger.LogWarning`, never throws)
- 4.2 PASS: Full status write sequence verified at lines: Processing (203), EventsStored (375), EventsPublished (411, 902), Terminal Completed/Rejected in CompleteTerminalAsync (1319), PublishFailed (492, 1111), Rejected via HandleInfrastructureFailureAsync (1267)
- 4.3 PASS: `eventCount` passed for Completed (line 1321), `rejectionEventType` for Rejected (line 1322), `failureReason` for PublishFailed (lines 493-496). `TimeoutDuration` always null (line 1366) -- by-design per UX-DR16
- 4.4 PASS: `CompleteTerminalAsync` at line 1317: `accepted ? CommandStatus.Completed : CommandStatus.Rejected`. Passes `eventCount` (line 1321) and `rejectionEventType` (line 1322)
- 4.5 PASS: `HandleInfrastructureFailureAsync` at line 1267 writes `CommandStatus.Rejected` with `failureReason = exception.Message`; this keeps infrastructure failures distinguishable from domain rejections because `RejectionEventType` remains null
- 4.6 PASS: **GAP FOUND AND FILLED** -- No isolated test existed for actor-level Rule 12. Added `ProcessCommandAsync_AdvisoryStatusWriteFails_StillReturnsAccepted` test in `AggregateActorTests.cs` with new `CreateActorWithStatusStoreMock()` helper. Test configures `commandStatusStore.WriteStatusAsync` to throw `InvalidOperationException` on every call and verifies pipeline still returns `Accepted=true`
- 4.7 PASS: Review follow-up added `ProcessCommandAsync_DomainInfrastructureFailure_WritesRejectedStatusWithFailureReason`, proving infrastructure failures persist `Rejected` with `FailureReason` and no rejection event type. Original Task 4 gap remains filled.

**Task 5: ActorStateMachine checkpoint mechanism** -- ALL PASS
- 5.1 PASS: `CheckpointAsync()` at line 21: key = `{pipelineKeyPrefix}{correlationId}`, uses `stateManager.SetStateAsync(key, state)` (staging only)
- 5.2 PASS: `LoadPipelineStateAsync()` at line 41: `stateManager.TryGetStateAsync<PipelineState>(key)`, returns `result.Value` if found, null otherwise
- 5.3 PASS: `CleanupPipelineAsync()` at line 65: `stateManager.TryRemoveStateAsync(key)` -- discards return value
- 5.4 PASS: Processing checkpoint at lines 191-200, committed via `SaveStateAsync()` at line 200. EventsStored checkpoint at lines 349-358, committed atomically with events. EventsPublished at line 400 and terminal states are advisory writes only (not checkpointed via PipelineState -- they use `WriteAdvisoryStatusAsync`)
- 5.5 PASS: Resume detection at line 116: `LoadPipelineStateAsync`. EventsStored resumes via `ResumeFromEventsStoredAsync` (line 135). Processing/unexpected stage cleaned up (lines 142-145) and reprocessed from scratch
- 5.6 PASS: 7 test methods covering: checkpoint stores correct state, load existing/nonexistent, cleanup removes key, key pattern convention, checkpoint overwrites, cleanup nonexistent does not throw
- 5.7 No gaps found

**Task 6: PipelineState and CommandStatusRecord contracts** -- ALL PASS
- 6.1 PASS: `PipelineState` record has all 7 fields: CorrelationId (string), CurrentStage (CommandStatus), CommandType (string), StartedAt (DateTimeOffset), EventCount (int?), RejectionEventType (string?), ResultPayload (string? = null)
- 6.2 PASS: `CommandStatusRecord` record has all 7 fields: Status (CommandStatus), Timestamp (DateTimeOffset), AggregateId (string?), EventCount (int?), RejectionEventType (string?), FailureReason (string?), TimeoutDuration (TimeSpan?)
- 6.3 PASS: 8 states with exact integer assignments: Received=0, Processing=1, EventsStored=2, EventsPublished=3, Completed=4, Rejected=5, PublishFailed=6, TimedOut=7
- 6.4 PASS: `BuildKey()` returns `$"{tenantId}:{correlationId}:status"`. `DefaultTtlSeconds = 86400`, `DefaultStateStoreName = "statestore"`
- 6.5 PASS: `TtlSeconds` defaults to `CommandStatusConstants.DefaultTtlSeconds` (86400), `StateStoreName` defaults to `CommandStatusConstants.DefaultStateStoreName` ("statestore")
- 6.6 Observation: DAPR state store behavior with TTL <= 0 is DAPR-component-specific. Redis treats TTL 0 as "no expiry". Validation not added per low risk assessment (config binding with sensible defaults)
- 6.7 No gaps found

**Task 7: CommandStatusResponse and Controller** -- ALL PASS
- 7.1 PASS: `FromRecord()` maps all fields. `TimeoutDuration` converted via `XmlConvert.ToString(record.TimeoutDuration.Value)` for ISO 8601 format
- 7.2 PASS: Tenant-scoped via `User.FindAll("eventstore:tenant")` claims. Iterates tenants, returns 404 for unauthorized/not found (SEC-3). Returns 403 for no tenant claims
- 7.3 PASS: 10 test methods covering: 200 existing, 404 nonexistent, 404 tenant mismatch, 403 no claims, multi-tenant search, completed event count, rejected type, rejected failure reason, timed out ISO 8601 duration, whitespace correlation ID 400. The controller now correctly accepts ULID/string correlation IDs instead of enforcing GUID format
- 7.4 No gaps found

**Task 8: InMemoryCommandStatusStore test fake** -- ALL PASS
- 8.1 PASS: Implements `ICommandStatusStore`, uses `ConcurrentDictionary<string, (CommandStatusRecord, DateTimeOffset)>`, simulates TTL, tracks history via `ConcurrentQueue` per key
- 8.2 PASS: Guard clauses match: `ThrowIfNullOrWhiteSpace` for tenantId/correlationId, `ThrowIfNull` for status
- 8.3 PASS: `GetAllStatuses()`, `GetStatusCount()`, and `Clear()` all present
- 8.4 PASS: TTL simulation: write stores `UtcNow.AddSeconds(TtlSeconds)` as expiry, read checks `entry.Expiry <= UtcNow` and returns null for expired. No dedicated test file -- accepted risk (fake exercised by handler and controller tests)
- 8.5 No gaps found

**Task 9: DI registration** -- ALL PASS
- 9.1 PASS: Line 102-103: `AddOptions<CommandStatusOptions>().BindConfiguration("EventStore:CommandStatus")`. Line 104: `AddSingleton<ICommandStatusStore, DaprCommandStatusStore>()`
- 9.2 PASS: `AggregateActor` constructor at line 42: `ICommandStatusStore commandStatusStore` parameter. Resolves from DI via actor host
- 9.3 PASS: `DaprClient` registered as Singleton by `AddDaprClient()`. `DaprCommandStatusStore` also Singleton. No captive dependency
- 9.4 No gaps found

**Task 10: Build and run tests** -- ALL PASS
- 10.1 PASS: Build succeeded with 0 warnings, 0 errors
- 10.2 PASS: Tier 1 -- 656 tests (Contracts 267 + Client 290 + Testing 67 + Sample 32)
- 10.3 PASS: Story 2.4 targeted server-side rerun passed -- 71 tests all green across `DaprCommandStatusStoreTests`, `SubmitCommandHandlerStatusTests`, `CommandStatusControllerTests`, `ActorStateMachineTests`, and `AggregateActorTests`
- 10.4 No in-scope test failures

**Summary:** All 10 tasks verified PASS. The original actor-level Rule 12 gap remains fixed, review follow-up corrected the status query contract and rejected-status semantics, and all acceptance criteria are satisfied.

### Senior Developer Review (AI)

- Review date: 2026-03-15
- Reviewer: GitHub Copilot (GPT-5.4)
- Outcome: High and medium review issues fixed automatically; story is now `done`.

#### Findings Fixed

1. `CommandStatusController` no longer rejects valid ULID/string correlation IDs with GUID-only validation.
2. `Rejected` status contract documentation now matches production semantics: domain rejections use `RejectionEventType`, infrastructure failures use `FailureReason`.
3. Story bookkeeping now reflects the actual Story 2.4 test coverage and counts that were rerun.

#### Validation

- Edited files compiled cleanly with no diagnostics.
- Story 2.4 targeted server-side verification rerun passed: 71/71 tests.

### Change Log

- 2026-03-15: Story 2.4 verification complete. Added `ProcessCommandAsync_AdvisoryStatusWriteFails_StillReturnsAccepted` test and `CreateActorWithStatusStoreMock()` helper to AggregateActorTests.cs (Task 4.6 gap fill)
- 2026-03-15: Senior review follow-up removed GUID-only status query validation, clarified rejected-status contract docs for infrastructure failures, added controller and actor regressions, and moved story status to done.

### File List

- src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs (modified -- accept non-GUID correlation IDs and only reject missing/whitespace input)
- src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs (modified -- clarified `Rejected` semantics in XML docs)
- src/Hexalith.EventStore.Contracts/Commands/CommandStatusRecord.cs (modified -- clarified `RejectionEventType` and `FailureReason` semantics)
- tests/Hexalith.EventStore.Server.Tests/Commands/CommandStatusControllerTests.cs (modified -- ULID/string-compatible lookup coverage, whitespace validation, rejected failure reason mapping)
- tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs (modified -- added Rule 12 advisory write test and infrastructure-failure rejected-status regression)
- _bmad-output/implementation-artifacts/2-4-command-status-tracking.md (modified -- review remediation and verification notes)
