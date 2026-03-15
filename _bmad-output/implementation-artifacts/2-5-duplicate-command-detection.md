# Story 2.5: Duplicate Command Detection

Status: ready-for-dev

## Story

As a platform developer,
I want duplicate commands detected and handled idempotently,
So that at-least-once delivery from callers doesn't produce duplicate events.

## Acceptance Criteria

1. **Given** a command with a messageId (ULID) that has already been processed, **When** the actor receives it, **Then** the system returns an idempotent success response (FR49) **And** no duplicate events are persisted.

2. **Given** a command targeting an aggregate with a concurrent write in progress, **When** an optimistic concurrency conflict is detected, **Then** the command is rejected via `ConcurrencyConflictException` and the caller is informed with a 409 Conflict response (FR7).

3. **Given** idempotency is checked at the actor level, **When** the `IdempotencyChecker` queries actor state, **Then** it uses key pattern `idempotency:{causationId}` where causationId defaults to `command.CausationId ?? command.CorrelationId` (the client-supplied messageId ULID).

4. **Given** a command completes processing (any terminal path), **When** the result is determined, **Then** an `IdempotencyRecord` is stored via `IActorStateManager.SetStateAsync` (staging only) and committed atomically with other actor state changes via `SaveStateAsync`.

5. All Tier 1 tests pass. Tier 2 IdempotencyChecker, AggregateActor idempotency, ConcurrencyConflictException, and ConcurrencyConflictExceptionHandler tests pass.

6. **Done definition:** IdempotencyChecker verified to check and record idempotency via actor state with correct key pattern. AggregateActor Step 1 verified to return cached result for duplicate commands (FR49). ConcurrencyConflictException verified to be thrown on SaveStateAsync InvalidOperationException at all commit points (FR7). ConcurrencyConflictExceptionHandler verified to return 409 with RFC 7807 ProblemDetails and write advisory Rejected status (Rule 12). IdempotencyRecord verified for correct FromResult/ToResult conversion. FakeAggregateActor verified for idempotency simulation. All required tests green. Each verification recorded as pass/fail in Completion Notes.

## Implementation State: VERIFICATION STORY

The Duplicate Command Detection infrastructure was implemented under the old epic structure. This story **verifies existing code** against the new Epic 2 acceptance criteria and fills any gaps found. Do NOT re-implement existing components.

**Gap-filling is authorized:** While this is a verification story, every task includes "if any gap found, implement the fix and add test coverage." Writing new tests or fixing code to close gaps IS part of verification scope. Do not skip implementation subtasks because the story header says "verification."

**Conflict resolution policy:** If existing code conflicts with the new acceptance criteria, the new AC takes precedence -- modify the code to comply.

**CRITICAL: Verify from source code, not from this story.** The Dev Notes section contains implementation details and code snippets for context, but these may be stale. For every PASS/FAIL verdict, read the actual `.cs` file directly. Do NOT mark tasks PASS based solely on the story's description of the code.

### Story 2.5 Scope -- Components to Verify

These components are owned by THIS story (idempotency checking + optimistic concurrency):

| Component | File | Verify |
|-----------|------|--------|
| `IIdempotencyChecker` | `src/Hexalith.EventStore.Server/Actors/IIdempotencyChecker.cs` | Interface: CheckAsync + RecordAsync |
| `IdempotencyChecker` | `src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs` | Check via TryGetStateAsync, Record via SetStateAsync, key pattern `idempotency:{causationId}` |
| `IdempotencyRecord` | `src/Hexalith.EventStore.Server/Actors/IdempotencyRecord.cs` | DTO with FromResult/ToResult conversions, ProcessedAt timestamp |
| `CommandProcessingResult` | `src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs` | Record with Accepted, ErrorMessage, CorrelationId, EventCount, ResultPayload |
| AggregateActor Step 1 | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (search: `Step 1: Idempotency check`) | Duplicate detection, cached result return, CausationId derivation |
| AggregateActor RecordAsync calls | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (search: `idempotencyChecker.RecordAsync`) | All terminal paths record idempotency |
| `ConcurrencyConflictException` | `src/Hexalith.EventStore.Server/Commands/ConcurrencyConflictException.cs` | Exception with CorrelationId, AggregateId, TenantId, ConflictSource properties |
| AggregateActor SaveStateAsync wraps | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (search: `throw new ConcurrencyConflictException`) | InvalidOperationException -> ConcurrencyConflictException at all commit points |
| `ConcurrencyConflictExceptionHandler` | `src/Hexalith.EventStore.CommandApi/ErrorHandling/ConcurrencyConflictExceptionHandler.cs` | 409 Conflict, RFC 7807, advisory Rejected status, exception chain unwrapping |
| `FakeAggregateActor` | `src/Hexalith.EventStore.Testing/Fakes/FakeAggregateActor.cs` | SimulateIdempotency flag, CausationId tracking |

### Out of Scope (Other Stories)

Do NOT verify these -- they belong to other stories:
- Command routing / actor skeleton (Story 2.1 -- done)
- Event persistence / `EventPersister` (Story 2.2 -- done)
- State rehydration / domain service invocation (Story 2.3 -- in-progress)
- Command status tracking / `DaprCommandStatusStore` (Story 2.4 -- ready-for-dev)
- Event publishing / `EventPublisher` (Story 4.1)
- REST endpoint contract details and HTTP response formats (Story 3.1, 3.5) -- **Note:** `ConcurrencyConflictExceptionHandler` (409 response) is IN scope for this story because it is the user-facing half of FR7 duplicate command detection. The actor throws the exception; the handler translates it to HTTP semantics. Both halves belong here.
- Dead-letter routing infrastructure (Story 3.4)

### Existing Test Files

| Test File | Covers | Tier |
|-----------|--------|------|
| `IdempotencyCheckerTests.cs` | Check no record, check existing, record stores, no SaveState, null/whitespace args, key format | Tier 2 |
| `AggregateActorTests.cs` | Duplicate detection returns cached, duplicate rejected cached, tenant mismatch records idempotency, CausationId fallback, no-op saves idempotency | Tier 2 |
| `ConcurrencyConflictExceptionTests.cs` | Constructor variants, property values, detail template | Tier 2 |

## Prerequisites

- **DAPR slim init required** for Tier 2 tests: run `dapr init --slim` before starting any verification task that touches Server.Tests
- **Story ordering note:** Stories 2.3 and 2.4 may modify `AggregateActor.cs`. Method-name searches (not line numbers) are used throughout to mitigate rebase risk.

## Tasks / Subtasks

Each verification subtask must be recorded as PASS or FAIL in the Completion Notes section.

- [ ] Task 1: Verify IdempotencyChecker check path (AC #1, #3)
  - [ ] 1.1 Read `IdempotencyChecker.cs`. Confirm `CheckAsync()` builds key as `idempotency:{causationId}` and queries via `stateManager.TryGetStateAsync<IdempotencyRecord>(key)`. Record PASS/FAIL
  - [ ] 1.2 Confirm cache hit returns `result.Value.ToResult()` converting `IdempotencyRecord` back to `CommandProcessingResult`. Record PASS/FAIL
  - [ ] 1.3 Confirm cache miss returns `null` (caller proceeds with normal processing). Record PASS/FAIL
  - [ ] 1.4 Confirm guard clause: `ArgumentException.ThrowIfNullOrWhiteSpace` for causationId. Record PASS/FAIL
  - [ ] 1.5 Confirm structured logging: `IdempotencyCacheHit` (EventId 5000) and `IdempotencyCacheMiss` (EventId 5001). Record PASS/FAIL
  - [ ] 1.6 If any check path gap found, implement the fix and add test coverage

- [ ] Task 2: Verify IdempotencyChecker record path (AC #4)
  - [ ] 2.1 Read `IdempotencyChecker.cs` `RecordAsync()`. Confirm it stores via `stateManager.SetStateAsync(key, record)` (staging only, no SaveStateAsync -- caller commits atomically). Record PASS/FAIL
  - [ ] 2.2 Confirm `IdempotencyRecord.FromResult(causationId, result)` creates record with correct field mapping. Record PASS/FAIL
  - [ ] 2.3 Confirm guard clauses: `ArgumentException.ThrowIfNullOrWhiteSpace` for causationId, `ArgumentNullException.ThrowIfNull` for result. Record PASS/FAIL
  - [ ] 2.4 Confirm structured logging: `IdempotencyRecordStored` (EventId 5002). Record PASS/FAIL
  - [ ] 2.5 If any record path gap found, implement the fix and add test coverage

- [ ] Task 3: Verify IdempotencyRecord contract (AC #4)
  - [ ] 3.1 Read `IdempotencyRecord.cs`. Confirm record has: CausationId (string), CorrelationId (string?), Accepted (bool), ErrorMessage (string?), ProcessedAt (DateTimeOffset). Record PASS/FAIL
  - [ ] 3.2 Confirm `FromResult(causationId, result)` maps: CausationId from parameter, CorrelationId from result, Accepted from result, ErrorMessage from result, ProcessedAt = `DateTimeOffset.UtcNow`. Record PASS/FAIL
  - [ ] 3.3 Confirm `ToResult()` reconstructs `CommandProcessingResult(Accepted, ErrorMessage, CorrelationId)`. Note: EventCount and ResultPayload are NOT preserved in the idempotency record -- this is by design (idempotency returns a success/fail signal, not the full result). Record PASS/FAIL
  - [ ] 3.4 Confirm `FromResult` guard clauses: ThrowIfNullOrWhiteSpace for causationId, ThrowIfNull for result. Record PASS/FAIL
  - [ ] 3.5 Check for dedicated `IdempotencyRecordTests.cs`. If none exists, create one with at minimum: (a) `FromResult` accepted roundtrip -- verify Accepted, CorrelationId, ErrorMessage preserved through `FromResult -> ToResult`, (b) `FromResult` rejected roundtrip -- verify rejected with ErrorMessage preserved, (c) `ToResult` does NOT preserve EventCount/ResultPayload (by-design assertion). Record PASS/FAIL with test count
  - [ ] 3.6 If any contract gap found, implement the fix

- [ ] Task 4: Verify AggregateActor Step 1 -- idempotency check (AC #1, #3)
  - [ ] 4.1 Read `AggregateActor.cs` ProcessCommandAsync. Confirm CausationId derivation: `string causationId = command.CausationId ?? command.CorrelationId` (FR4: messageId serves as idempotency key, mapped to CausationId in the envelope). Record PASS/FAIL
  - [ ] 4.2 Confirm Step 1 calls `idempotencyChecker.CheckAsync(causationId)`. If cached result is not null, returns it immediately without further processing. No duplicate events are persisted (FR49). Record PASS/FAIL
  - [ ] 4.3 Confirm duplicate detection logs: `"Duplicate command detected: CausationId={CausationId}, CorrelationId={CorrelationId}, ActorId={ActorId}. Returning cached result."` Record PASS/FAIL
  - [ ] 4.4 Confirm idempotency check is wrapped in an OpenTelemetry activity (`EventStoreActivitySource.IdempotencyCheck`). Record PASS/FAIL
  - [ ] 4.5 If any Step 1 gap found, implement the fix and add test coverage

- [ ] Task 5: Verify idempotency recording at ALL terminal paths (AC #4)
  - [ ] 5.1 Trace all `idempotencyChecker.RecordAsync` call sites in `AggregateActor.cs`. Confirm recording happens at each of these terminal paths:
    - **Tenant mismatch rejection** (search: `TenantMismatch` near `RecordAsync`): records Accepted=false with error message, commits via SaveStateAsync
    - **CompleteTerminalAsync** (search: `private async Task<CommandProcessingResult> CompleteTerminalAsync`): records for both accepted (Completed) and rejected (domain rejection) paths
    - **HandleInfrastructureFailureAsync** (search: `private async Task<CommandProcessingResult> HandleInfrastructureFailureAsync`): records Accepted=false for infrastructure failures
    - **PublishFailed path** (search: `PublishFailed` near `RecordAsync`): records in both normal and resume publish-failed paths
    Record PASS/FAIL for each path
  - [ ] 5.2 Confirm each RecordAsync is followed by SaveStateAsync (either directly or through CompleteTerminalAsync's commit). SetStateAsync is staging only -- without SaveStateAsync the record is lost on actor deactivation. Record PASS/FAIL
  - [ ] 5.3 If any terminal path is missing idempotency recording, implement the fix and add test coverage

- [ ] Task 6: Verify ConcurrencyConflictException (AC #2)
  - [ ] 6.1 Read `ConcurrencyConflictException.cs`. Confirm properties: CorrelationId, AggregateId, TenantId (string?), ConflictSource (string?), Detail (string?). Confirm all constructors (parameterless, message, message+inner, primary domain constructor). Record PASS/FAIL
  - [ ] 6.2 Confirm `DefaultDetailTemplate` includes: aggregate ID, description of conflict, retry suggestion. Record PASS/FAIL
  - [ ] 6.3 In `AggregateActor.cs`, search all `throw new ConcurrencyConflictException` sites. Confirm each wraps `catch (InvalidOperationException ex)` after `StateManager.SaveStateAsync()`. Count the sites (expected: 4 -- Step 5 event persistence, PublishFailed commit, resume CompletePublishFailed commit, CompleteTerminal commit). Record PASS/FAIL with count
  - [ ] 6.4 Confirm concurrency exceptions are NOT caught by the generic infrastructure failure handler: verify `catch (Exception ex) when (ex is not OperationCanceledException and not ConcurrencyConflictException)` at Step 5 persistence. ConcurrencyConflictException must propagate to the caller unhandled by the actor. Record PASS/FAIL
  - [ ] 6.5 Read `ConcurrencyConflictExceptionTests.cs`. Count test methods. Confirm coverage of constructor variants and property assertions. Record PASS/FAIL with test count
  - [ ] 6.6 If any gap found, implement the fix and add test coverage

- [ ] Task 7: Verify ConcurrencyConflictExceptionHandler -- 409 response (AC #2)
  - [ ] 7.1 Read `ConcurrencyConflictExceptionHandler.cs`. Confirm it implements `IExceptionHandler`. Confirm it returns 409 Conflict with RFC 7807 `ProblemDetails`. Record PASS/FAIL
  - [ ] 7.2 Confirm exception chain unwrapping: `FindConcurrencyConflict` walks `InnerException` chain (max depth 10) and `AggregateException.InnerExceptions` to find `ConcurrencyConflictException`. This is needed because DAPR actor proxy wraps exceptions in `ActorMethodInvocationException`. Record PASS/FAIL
  - [ ] 7.3 Confirm advisory status write: on conflict, writes `CommandStatus.Rejected` with `FailureReason: "ConcurrencyConflict"` to `ICommandStatusStore` (Rule 12 -- non-blocking). Record PASS/FAIL
  - [ ] 7.4 Confirm ProblemDetails includes: `correlationId` extension, `aggregateId` extension, `tenantId` extension, `conflictSource` extension. Record PASS/FAIL
  - [ ] 7.5 Confirm DI registration: `ConcurrencyConflictExceptionHandler` registered in `ServiceCollectionExtensions.cs` via `AddExceptionHandler<ConcurrencyConflictExceptionHandler>()`. Record PASS/FAIL
  - [ ] 7.6 Check for dedicated `ConcurrencyConflictExceptionHandlerTests.cs`. If no test file exists, **create one** with at minimum: (a) returns 409 for `ConcurrencyConflictException`, (b) returns false for non-`ConcurrencyConflictException`, (c) unwraps `ActorMethodInvocationException` wrapping, (d) unwraps `AggregateException` wrapping, (e) advisory status write occurs (Rule 12), (f) advisory status write failure does not block 409 response. This handler is the user-facing half of FR7 -- untested means FR7 is unverified. Record PASS/FAIL with test count
  - [ ] 7.7 If any gap found, implement the fix and add test coverage

- [ ] Task 8: Verify FakeAggregateActor idempotency simulation (AC #1)
  - [ ] 8.1 Read `FakeAggregateActor.cs`. Confirm it implements `IAggregateActor`. Confirm `SimulateIdempotency` flag enables causation ID tracking via `ConcurrentDictionary`. Record PASS/FAIL
  - [ ] 8.2 Confirm idempotency simulation: when SimulateIdempotency=true, second call with same causationId returns cached result without incrementing ProcessedCount. CausationId derived as `command.CausationId ?? command.CorrelationId` (matches real actor). Record PASS/FAIL
  - [ ] 8.3 Confirm `ReceivedCommands` still records all commands (including duplicates) for assertion. Record PASS/FAIL
  - [ ] 8.4 If any test fake gap found, implement the fix

- [ ] Task 9: Verify CommandProcessingResult contract
  - [ ] 9.1 Read `CommandProcessingResult.cs`. Confirm record has: Accepted (bool), ErrorMessage (string? = null), CorrelationId (string? = null), EventCount (int = 0), ResultPayload (string? = null). Confirm `[DataContract]` and `[DataMember]` attributes for DAPR actor serialization. Record PASS/FAIL
  - [ ] 9.2 If any contract gap found, implement the fix

- [ ] Task 10: Build and run tests (AC #5)
  - [ ] 10.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- zero warnings. Record PASS/FAIL
  - [ ] 10.2 Run Tier 1: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` + `Client.Tests` + `Sample.Tests` + `Testing.Tests` -- all pass. Record PASS/FAIL with counts
  - [ ] 10.3 Run Tier 2 Story 2.5 scope tests (requires `dapr init --slim`): `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~IdempotencyChecker|FullyQualifiedName~AggregateActor|FullyQualifiedName~ConcurrencyConflict"` -- all pass. **Note:** the `AggregateActor` filter is intentionally broad and will run tests beyond idempotency scope (e.g., rehydration, status tracking). Out-of-scope failures in `AggregateActorTests` should be logged in Completion Notes but NOT investigated -- they belong to other stories. Record PASS/FAIL with counts
  - [ ] 10.4 If any test fails, investigate root cause and fix only if failure is within Story 2.5 scope (idempotency, duplicate detection, concurrency conflict). Log out-of-scope failures for later stories

## Dev Notes

### Scope Summary

This is a **verification story**. The Duplicate Command Detection infrastructure was fully implemented under the old epic numbering (prior to the 2026-03-15 epic restructure). The developer's job is to: read the existing code, confirm it meets the acceptance criteria, record PASS/FAIL for each verification, identify any gaps, fix them, and confirm tests pass.

**Scope boundary:** This story owns the idempotency check/record mechanism (IdempotencyChecker, IdempotencyRecord, IIdempotencyChecker), AggregateActor Step 1 duplicate detection, ConcurrencyConflictException (throw + handler), and the FakeAggregateActor idempotency simulation. Command routing (2.1), event persistence (2.2), state rehydration (2.3), command status tracking (2.4), and REST endpoint contracts (3.x) are verified by their own stories.

### Architecture Constraints (MUST FOLLOW)

- **FR49:** Detect duplicate commands by tracking processed command message IDs (client-generated ULIDs) per aggregate, returning idempotent success response for already-processed commands
- **FR7:** Reject duplicate commands targeting an aggregate with optimistic concurrency conflict (ETag mismatch on aggregate metadata key)
- **FR4:** CorrelationId defaults to client-supplied messageId (ULID) -- also serves as idempotency key
- **D1:** Actor-level ACID via `IActorStateManager.SaveStateAsync` -- all state changes (including idempotency records) committed atomically
- **Rule 6:** `IActorStateManager` for ALL actor state operations -- IdempotencyChecker uses SetStateAsync/TryGetStateAsync (staging only)
- **Rule 12:** Command status writes on concurrency conflict are advisory -- ConcurrencyConflictExceptionHandler writes Rejected status but never blocks the 409 response

### Two Distinct Duplicate Detection Mechanisms

This story covers two related but separate mechanisms:

| Mechanism | Trigger | Response | Location |
|-----------|---------|----------|----------|
| **Idempotency check** (FR49) | Same CausationId arrives twice | Return cached `CommandProcessingResult` (success or failure) | AggregateActor Step 1, before any state access |
| **Concurrency conflict** (FR7) | ETag mismatch during `SaveStateAsync` | Throw `ConcurrencyConflictException` -> 409 Conflict | AggregateActor at any SaveStateAsync call |

Idempotency prevents re-processing of already-completed commands (at-least-once safety). Concurrency conflict detects race conditions via ETag mismatch on state store commits. Both protect against duplicate events but at different layers.

**Actor Turn-Based Concurrency -- Primary Duplicate Protection:** DAPR actors enforce **single-threaded turn-based concurrency**. Only one message is processed at a time per actor instance. If two commands with the same CausationId arrive simultaneously, the second queues until the first completes and commits its idempotency record. There is NO race condition between `CheckAsync` (cache miss) and `RecordAsync` (commit) within the same actor. `ConcurrencyConflictException` handles the rare edge case of stale ETags after actor migration between nodes, not concurrent processing within the same actor. Do NOT attempt to add locking or concurrency guards -- DAPR's actor runtime already provides this guarantee.

### CausationId as Idempotency Key -- Design Decision F8

The idempotency key is `CausationId`, not `MessageId` or `CorrelationId`:
- `CausationId` defaults to `command.CausationId ?? command.CorrelationId`
- Per FR4, `CorrelationId` defaults to the client-supplied `MessageId` (ULID)
- So for first-party commands: `CausationId` = `MessageId` = idempotency key
- For saga/workflow retries: `CausationId` may differ from `MessageId`, allowing the same logical operation to be idempotent while the wrapper command has a new MessageId

### Idempotency Record Accumulation -- Accepted Technical Debt

Idempotency records are stored in actor state via `IActorStateManager.SetStateAsync` with **no TTL or cleanup mechanism**. Unlike command status entries (which use DAPR `ttlInSeconds` metadata for 24-hour expiry), actor state keys persist indefinitely. For long-lived aggregates processing thousands of commands, idempotency keys accumulate without bound.

This is **accepted technical debt for v1**. Do NOT implement cleanup logic, TTL simulation, or bounded collections as part of this story. A future story may introduce actor-reminder-based pruning or a sliding window approach, but that is out of scope here. The current design prioritizes correctness (never losing idempotency protection) over storage efficiency.

### PRD vs Implementation Note

The PRD mentions storing "processed messageIds" in the aggregate metadata key `{tenant}:{domain}:{aggregateId}:metadata`. The actual implementation uses **separate per-causation-ID keys** (`idempotency:{causationId}`) in actor state. This is architecturally superior: avoids unbounded growth of the metadata record and enables O(1) lookups. Do NOT "fix" this to match the PRD -- the implementation is correct.

### CheckAsync is Intentionally Fail-Closed

`IdempotencyChecker.CheckAsync` has **no try/catch**. If actor state is unavailable (e.g., state store down), the exception propagates and the command fails. This is **intentional fail-closed behavior**: if we cannot verify whether a command was already processed, we must not process it (risk: duplicate events, corrupted state). This is the opposite of Rule 12's fail-open pattern used for advisory status writes, where the cost of missing a status update is low. Do NOT add try/catch to CheckAsync to "improve resilience" -- fail-closed is the correct idempotency posture.

### API Response for Duplicate Commands

When a duplicate is detected at Step 1, the actor returns the cached `CommandProcessingResult`. The API handler treats this as a normal successful response and returns **202 Accepted** -- the same status as the original submission. The caller **cannot** distinguish a duplicate from a first-time acceptance. This is by-design per FR49 ("returning idempotent success response"): the caller's intent was to submit the command, and the system confirms it was processed. Whether it was processed now or previously is an internal implementation detail.

### IdempotencyRecord Does NOT Preserve Full Result

`IdempotencyRecord.ToResult()` reconstructs `CommandProcessingResult(Accepted, ErrorMessage, CorrelationId)` -- but `EventCount` and `ResultPayload` default to 0 and null respectively. This is by design: the idempotency response confirms "this command was already processed" with its success/fail outcome, not the full enriched result. Do NOT attempt to add EventCount/ResultPayload to IdempotencyRecord.

### Key Implementation Details

**Idempotency Check Flow (AggregateActor Step 1):**
```
1. Derive causationId = command.CausationId ?? command.CorrelationId
2. idempotencyChecker.CheckAsync(causationId)
   -> TryGetStateAsync<IdempotencyRecord>("idempotency:{causationId}")
3. If found: log duplicate, return cached.ToResult()
4. If not found: continue to Step 1b (pipeline resume detection)
```

**Idempotency Recording Flow (all terminal paths):**
```
idempotencyChecker.RecordAsync(causationId, result)
  -> IdempotencyRecord.FromResult(causationId, result)
  -> SetStateAsync("idempotency:{causationId}", record)  // staging only
  -> ... other terminal state changes ...
  -> StateManager.SaveStateAsync()  // atomic commit of all staged changes
```

**ConcurrencyConflict Flow:**
```
try { await StateManager.SaveStateAsync(); }
catch (InvalidOperationException ex) {
    throw new ConcurrencyConflictException(correlationId, aggregateId, tenantId,
        conflictSource: "StateStore", innerException: ex);
}
// Exception propagates through DAPR actor proxy -> wrapped in ActorMethodInvocationException
// ConcurrencyConflictExceptionHandler unwraps chain -> returns 409 + ProblemDetails
```

### Key Interfaces

```csharp
public interface IIdempotencyChecker {
    Task<CommandProcessingResult?> CheckAsync(string causationId);
    Task RecordAsync(string causationId, CommandProcessingResult result);
}

public record IdempotencyRecord(
    string CausationId, string? CorrelationId, bool Accepted,
    string? ErrorMessage, DateTimeOffset ProcessedAt);

[DataContract]
public record CommandProcessingResult(
    bool Accepted, string? ErrorMessage = null, string? CorrelationId = null,
    int EventCount = 0, string? ResultPayload = null);
```

### Dependencies (from Directory.Packages.props)

- Dapr.Client: 1.16.1
- Dapr.Actors: 1.16.1
- Dapr.Actors.AspNetCore: 1.16.1
- xUnit: 2.9.3, NSubstitute: 5.3.0, Shouldly: 4.3.0

**Note:** `CLAUDE.md` lists DAPR SDK 1.17.0 but `Directory.Packages.props` pins 1.16.1. The .props file is the source of truth. Do not upgrade DAPR SDK as part of this story.

### Previous Story Intelligence (Story 2.4)

Story 2.4 verified command status tracking. Key learnings:
- Story 2.4 was a verification story -- same approach applies here
- Build must produce zero warnings (`TreatWarningsAsErrors = true`)
- 15 pre-existing out-of-scope failures exist: 4 SubmitCommandHandler NullRef (Pipeline), 1 validator, 10 auth integration (Epic 5)
- Tier 1: 652 tests (Contracts 267 + Client 286 + Testing 67 + Sample 32)
- Tier 2 scope tests are filtered by `--filter "FullyQualifiedName~..."` to isolate from out-of-scope failures
- Two distinct "status" concepts: PipelineState (checkpoint, crash-recovery) vs CommandStatusRecord (advisory, API consumer). This story similarly has two distinct duplicate detection concepts (idempotency vs concurrency conflict)

### Previous Story Intelligence (Story 2.3)

- Verification story approach confirmed effective
- AggregateActor has grown complex (~1300 lines); use method-name searches not line numbers

### Previous Story Intelligence (Story 2.1)

- IdempotencyChecker is created per-call (not DI-registered) because it requires the actor's IActorStateManager instance
- CausationId = MessageId (not CorrelationId) per `ToCommandEnvelope()` implementation
- AggregateActor is a 5-step thin orchestrator; idempotency is Step 1

### Git Intelligence

Recent commits show Epic 1 complete, Epic 2 Stories 2.1-2.2 done, 2.3 in-progress:
- `b9a4e23` Refactor command handling and improve test assertions
- `fc46ddd` feat: Implement Story 1.5 -- CommandStatus enum, ITerminatable, tombstoning
- `4b122e5` feat: Implement Story 1.4 -- Pure Function Contract & EventStoreAggregate Base
- `493bcd8` feat: Epic 1 Stories 1.1, 1.2, 1.3 -- Domain Contract Foundation

### Project Structure Notes

- Server project at `src/Hexalith.EventStore.Server/` -- feature-folder organization: Actors/, Commands/, DomainServices/, Events/, Pipeline/
- Server.Tests at `tests/Hexalith.EventStore.Server.Tests/` -- mirrors Server structure
- CommandApi at `src/Hexalith.EventStore.CommandApi/` -- ErrorHandling/, Extensions/
- Idempotency files in `Actors/` subfolder (IdempotencyChecker, IdempotencyRecord, IIdempotencyChecker, CommandProcessingResult)
- ConcurrencyConflictException in `Commands/` subfolder
- ConcurrencyConflictExceptionHandler in `ErrorHandling/` subfolder of CommandApi
- Testing fakes at `src/Hexalith.EventStore.Testing/Fakes/`

### File Conventions

- **Namespaces:** File-scoped (`namespace X.Y.Z;`)
- **Braces:** Allman style (new line before opening brace)
- **Private fields:** `_camelCase`
- **Async methods:** `Async` suffix
- **4 spaces** indentation, CRLF, UTF-8
- **Nullable:** enabled globally
- **XML docs:** on all public types

### References

- [Source: _bmad-output/planning-artifacts/epics.md -- Epic 2, Story 2.5]
- [Source: _bmad-output/planning-artifacts/architecture.md -- D1, FR7, FR49, Rule 6, Rule 12]
- [Source: _bmad-output/planning-artifacts/prd.md -- FR4, FR7, FR49]
- [Source: src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs -- idempotency implementation]
- [Source: src/Hexalith.EventStore.Server/Actors/IIdempotencyChecker.cs -- interface contract]
- [Source: src/Hexalith.EventStore.Server/Actors/IdempotencyRecord.cs -- storage DTO]
- [Source: src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs -- result record]
- [Source: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs -- Step 1 check, RecordAsync at all terminal paths]
- [Source: src/Hexalith.EventStore.Server/Commands/ConcurrencyConflictException.cs -- concurrency exception]
- [Source: src/Hexalith.EventStore.CommandApi/ErrorHandling/ConcurrencyConflictExceptionHandler.cs -- 409 handler]
- [Source: src/Hexalith.EventStore.Testing/Fakes/FakeAggregateActor.cs -- test fake]
- [Source: tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyCheckerTests.cs -- unit tests]
- [Source: tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs -- actor tests]
- [Source: tests/Hexalith.EventStore.Server.Tests/Commands/ConcurrencyConflictExceptionTests.cs -- exception tests]
- [Source: _bmad-output/implementation-artifacts/2-4-command-status-tracking.md -- Story 2.4 learnings]
- [Source: _bmad-output/implementation-artifacts/2-3-state-rehydration-and-domain-service-invocation.md -- Story 2.3 learnings]
- [Source: _bmad-output/implementation-artifacts/2-1-aggregate-actor-and-command-routing.md -- Story 2.1 learnings]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
