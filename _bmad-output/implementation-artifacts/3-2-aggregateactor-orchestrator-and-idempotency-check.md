# Story 3.2: AggregateActor Orchestrator & Idempotency Check

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **system operator**,
I want the AggregateActor to be a thin orchestrator that first checks for duplicate commands (idempotency) before proceeding with processing,
So that replayed or duplicate commands don't produce duplicate events.

## Acceptance Criteria

1. **AggregateActor implements thin orchestrator pattern** - Given the AggregateActor receives a `CommandEnvelope` via `ProcessCommandAsync`, When the actor begins processing, Then it delegates to a 5-step pipeline in strict order: (1) idempotency check, (2) tenant validation [STUB], (3) state rehydration [STUB], (4) domain service invocation [STUB], (5) state machine execution [STUB]. Steps 2-5 are stubs that pass through -- only Step 1 is implemented in this story. The stub for steps 2-5 logs at Debug level that it was skipped (e.g., `"Step 2: Tenant validation -- STUB, skipped until Story 3.3"`).

2. **Idempotency check detects duplicate commands** - Given a command with causation ID "abc-123" has already been processed by this aggregate actor, When a second command arrives with the same causation ID "abc-123", Then the IdempotencyChecker returns the previously stored `CommandProcessingResult` without executing Steps 2-5, And the actor logs at Information level: `"Duplicate command detected: CausationId={CausationId}, ActorId={ActorId}. Returning cached result."`. **Note:** The idempotency key is `causationId` (not `correlationId`) because the Replay endpoint (Story 2.7) resubmits commands with the same correlationId but a new causationId. Using correlationId would block replays. DAPR retries preserve both IDs, so causationId correctly catches retries while allowing replays.

3. **Idempotency state stored via IActorStateManager** - When the actor completes processing a new (non-duplicate) command, Then the `CommandProcessingResult` is stored in actor state with key `idempotency:{causationId}` via `IActorStateManager.SetStateAsync` (enforcement rule #6), And the stored result is committed atomically with any other state changes via `SaveStateAsync`. The idempotency record contains: `CausationId`, `CorrelationId`, `Accepted`, `ErrorMessage`, `ProcessedAt` (DateTimeOffset). The `CorrelationId` is stored so that `ToResult()` can reconstruct a self-contained `CommandProcessingResult` without depending on caller context.

4. **IdempotencyChecker is a focused, injectable component** - The `IdempotencyChecker` is a separate class (not inlined in the actor) implementing `IIdempotencyChecker`, And it is injected into the AggregateActor's `ProcessCommandAsync` method via the actor's service provider (actors do NOT use constructor injection for scoped services -- they resolve from `IServiceProvider` per Dapr actor patterns), And the checker encapsulates all IActorStateManager calls for idempotency key reads/writes.

5. **Non-duplicate commands proceed through pipeline** - Given a command arrives with a causation ID not previously processed, When the IdempotencyChecker finds no cached result, Then the actor proceeds to Steps 2-5 (all stubs in this story), And after the stubs return, the actor creates a `CommandProcessingResult(Accepted: true, CorrelationId: command.CorrelationId)`, And stores it via IdempotencyChecker for future duplicate detection, And returns the result.

6. **Existing tests unbroken** - All existing tests (estimated ~371 from Story 3.1) continue to pass after the AggregateActor is transformed from STUB to orchestrator. Unit tests that previously tested the STUB behavior are updated to verify the new orchestrator flow. Integration tests continue to work via the mocked/faked actor infrastructure.

## Prerequisites

**BLOCKING: Story 3.1 MUST be complete (done status) before starting Story 3.2.** Story 3.2 depends on:
- `AggregateActor` STUB implementation created in Story 3.1 (to be transformed into orchestrator)
- `IAggregateActor` interface with `ProcessCommandAsync(CommandEnvelope)` method (Story 3.1)
- `CommandProcessingResult` record in `Server/Actors/` (Story 3.1)
- `ICommandRouter` and `CommandRouter` routing commands to actor (Story 3.1)
- `SubmitCommandExtensions.ToCommandEnvelope()` conversion (Story 3.1)
- `AddEventStoreServer()` DI registration (Story 3.1)
- `FakeAggregateActor` test fake (Story 3.1)
- All Epic 2 infrastructure (authentication, authorization, validation, status tracking, replay, concurrency handling)

**Before beginning any Task below, verify:** Run existing tests to confirm all Story 3.1 artifacts are in place. All existing tests must pass before proceeding.

**CausationId verification:** Check what `SubmitCommandExtensions.ToCommandEnvelope()` (Story 3.1) sets for `CausationId`. If it always sets `CausationId = CorrelationId` for original submissions, then `CausationId` is never null when it reaches the actor and the `?? command.CorrelationId` fallback in Task 4.2 is defensive-only. If it leaves `CausationId` as null, the fallback is required. Either way, the code is correct -- this just determines whether the fallback path is exercised in practice.

## Tasks / Subtasks

- [ ] Task 0: Verify prerequisites and existing artifacts (BLOCKING)
  - [ ] 0.1 Run all existing tests -- they must pass before proceeding
  - [ ] 0.2 Confirm `AggregateActor` STUB exists in `Server/Actors/AggregateActor.cs` with `ProcessCommandAsync` method
  - [ ] 0.3 Confirm `CommandProcessingResult` record exists in `Server/Actors/CommandProcessingResult.cs`
  - [ ] 0.4 Confirm `IAggregateActor` interface exists in `Server/Actors/IAggregateActor.cs`
  - [ ] 0.5 Confirm `IActorStateManager` is available on the `AggregateActor` base class via `this.StateManager`
  - [ ] 0.6 Confirm Dapr.Actors 1.16.1 is in Server.csproj dependencies

- [ ] Task 1: Create IIdempotencyChecker interface (AC: #4)
  - [ ] 1.1 Create `IIdempotencyChecker` interface in `Server/Actors/`
  - [ ] 1.2 Define two methods:
    - `Task<CommandProcessingResult?> CheckAsync(string causationId)` -- returns cached result or null
    - `Task RecordAsync(string causationId, CommandProcessingResult result)` -- stores result for future checks
  - [ ] 1.3 Namespace: `Hexalith.EventStore.Server.Actors`

- [ ] Task 2: Create IdempotencyChecker implementation (AC: #3, #4)
  - [ ] 2.1 Create `IdempotencyChecker` class in `Server/Actors/` implementing `IIdempotencyChecker`
  - [ ] 2.2 Constructor: `IdempotencyChecker(IActorStateManager stateManager, ILogger<IdempotencyChecker> logger)`
  - [ ] 2.3 `CheckAsync` implementation: call `stateManager.TryGetStateAsync<IdempotencyRecord>("idempotency:{causationId}")`, return the cached `CommandProcessingResult` if found, null otherwise
  - [ ] 2.4 `RecordAsync` implementation: create `IdempotencyRecord` with (CausationId, Result, ProcessedAt = DateTimeOffset.UtcNow), call `stateManager.SetStateAsync("idempotency:{causationId}", record)` -- note: does NOT call `SaveStateAsync` here; that is the actor's responsibility to batch all state changes
  - [ ] 2.5 Log at Debug level on check hit: `"Idempotency cache hit: CausationId={CausationId}"`
  - [ ] 2.6 Log at Debug level on check miss: `"Idempotency cache miss: CausationId={CausationId}"`
  - [ ] 2.7 Log at Debug level on record: `"Idempotency record stored: CausationId={CausationId}"`
  - [ ] 2.8 Use `ArgumentNullException.ThrowIfNull()` and `ArgumentException.ThrowIfNullOrWhiteSpace()` on parameters (CA1062)

- [ ] Task 3: Create IdempotencyRecord data type (AC: #3)
  - [ ] 3.1 Create `IdempotencyRecord` record in `Server/Actors/`: `record IdempotencyRecord(string CausationId, string CorrelationId, bool Accepted, string? ErrorMessage, DateTimeOffset ProcessedAt)` -- CorrelationId stored for self-contained `ToResult()` reconstruction
  - [ ] 3.2 This record is what gets serialized into IActorStateManager. It must be JSON-serializable (all primitive/nullable types, no complex references)
  - [ ] 3.3 The record is intentionally NOT `CommandProcessingResult` directly -- it's a storage-optimized DTO that can evolve independently of the public API type
  - [ ] 3.4 Add static factory method: `static IdempotencyRecord FromResult(string causationId, CommandProcessingResult result)` -> maps fields (extracts CorrelationId from result)
  - [ ] 3.5 Add instance method: `CommandProcessingResult ToResult()` -> reconstructs `CommandProcessingResult` from stored fields

- [ ] Task 4: Transform AggregateActor from STUB to orchestrator (AC: #1, #5)
  - [ ] 4.1 Modify `AggregateActor.ProcessCommandAsync` to implement the 5-step delegation pattern
  - [ ] 4.2 Step 1 (idempotency): Resolve causationId as `command.CausationId ?? command.CorrelationId` (fallback for commands where causationId is null -- original submissions). Create `IdempotencyChecker` by resolving `ILogger<IdempotencyChecker>` from `IServiceProvider` and passing `this.StateManager`. The `IdempotencyChecker` is created per-call (lightweight, no state of its own beyond the injected stateManager)
  - [ ] 4.3 Call `idempotencyChecker.CheckAsync(command.CausationId)` -- if result is not null, log duplicate detection and return the cached result immediately. Note: `CausationId` is the correct idempotency key (see design decision F8)
  - [ ] 4.4 Steps 2-5 (STUBS): Log at Debug level for each stub step, e.g., `logger.LogDebug("Step 2: Tenant validation -- STUB (Story 3.3)")`. These are inline no-ops, not separate classes yet
  - [ ] 4.5 After all steps pass, create result: `new CommandProcessingResult(Accepted: true, CorrelationId: command.CorrelationId)`
  - [ ] 4.6 Record idempotency: `await idempotencyChecker.RecordAsync(command.CausationId, result)`
  - [ ] 4.7 Call `await StateManager.SaveStateAsync()` to atomically commit the idempotency record (and any future state changes from Steps 2-5)
  - [ ] 4.8 Return the result
  - [ ] 4.9 Preserve existing logging: the command receipt log from Story 3.1 should remain at the start of ProcessCommandAsync
  - [ ] 4.10 Exception handling: Do NOT wrap the orchestrator in try/catch -- let exceptions propagate to the caller (the CommandRouter). DAPR actor infrastructure handles actor-level errors. The exception handler chain at the API layer (ConcurrencyConflictExceptionHandler -> GlobalExceptionHandler) deals with surfacing errors
  - [ ] 4.11 `ConfigureAwait(false)` on all async calls (CA2007)

- [ ] Task 5: Update AddEventStoreServer() if needed (AC: #4)
  - [ ] 5.1 Review `Server/Configuration/ServiceCollectionExtensions.AddEventStoreServer()` -- IdempotencyChecker does NOT need DI registration because it is created directly in the actor using `this.StateManager` (actor state managers are per-actor-instance, not DI-resolvable)
  - [ ] 5.2 If any new DI registrations are needed (e.g., for future extensibility), add them here
  - [ ] 5.3 Document in code comment: "IdempotencyChecker is created per-actor-call, not via DI, because it requires the actor's IActorStateManager instance"

- [ ] Task 6: Write unit tests for IdempotencyChecker (AC: #2, #3)
  - [ ] 6.1 Create `IdempotencyCheckerTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Actors/`
  - [ ] 6.2 `CheckAsync_NoExistingRecord_ReturnsNull` -- verify TryGetStateAsync returns not-found, CheckAsync returns null
  - [ ] 6.3 `CheckAsync_ExistingRecord_ReturnsCachedResult` -- verify stored record is returned as CommandProcessingResult
  - [ ] 6.4 `RecordAsync_StoresIdempotencyRecord` -- verify SetStateAsync called with correct key and record
  - [ ] 6.5 `RecordAsync_DoesNotCallSaveState` -- verify SaveStateAsync is NOT called (actor's responsibility)
  - [ ] 6.6 `CheckAsync_NullCausationId_ThrowsArgumentException` -- verify guard clause
  - [ ] 6.7 `RecordAsync_NullCausationId_ThrowsArgumentException` -- verify guard clause
  - [ ] 6.8 `RecordAsync_NullResult_ThrowsArgumentNullException` -- verify guard clause
  - [ ] 6.9 Mock `IActorStateManager` using NSubstitute. Use `stateManager.TryGetStateAsync<IdempotencyRecord>(key)` returning `ConditionalValue<IdempotencyRecord>`. Key pattern: `idempotency:{causationId}`

- [ ] Task 7: Write unit tests for IdempotencyRecord (AC: #3)
  - [ ] 7.1 Create `IdempotencyRecordTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Actors/`
  - [ ] 7.2 `FromResult_MapsAllFields` -- verify factory method maps CommandProcessingResult fields correctly (including CorrelationId from result)
  - [ ] 7.3 `ToResult_ReconstructsCommandProcessingResult` -- verify roundtrip (CorrelationId preserved from stored record, not from caller context)
  - [ ] 7.4 `JsonRoundtrip_PreservesAllFields` -- verify JSON serialization/deserialization for IActorStateManager compatibility

- [ ] Task 8: Write unit tests for AggregateActor orchestrator (AC: #1, #2, #5)
  - [ ] 8.1 Update existing `AggregateActorTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Actors/`
  - [ ] 8.2 `ProcessCommandAsync_NewCommand_ExecutesFullPipeline` -- verify all 5 steps are invoked in order (Step 1 real, Steps 2-5 stubs)
  - [ ] 8.3 `ProcessCommandAsync_NewCommand_ReturnsAccepted` -- verify Accepted=true with correct CorrelationId
  - [ ] 8.4 `ProcessCommandAsync_NewCommand_StoresIdempotencyRecord` -- verify idempotency record written to StateManager
  - [ ] 8.5 `ProcessCommandAsync_NewCommand_CallsSaveStateAsync` -- verify SaveStateAsync called after all steps
  - [ ] 8.6 `ProcessCommandAsync_DuplicateCommand_ReturnsCachedResult` -- verify existing idempotency record returned
  - [ ] 8.7 `ProcessCommandAsync_DuplicateCommand_SkipsSteps2Through5` -- verify Steps 2-5 are NOT executed for duplicates
  - [ ] 8.8 `ProcessCommandAsync_DuplicateCommand_DoesNotCallSaveState` -- verify SaveStateAsync NOT called for duplicates (no state changes needed)
  - [ ] 8.9 `ProcessCommandAsync_ValidCommand_LogsCommandReceipt` -- verify existing receipt logging preserved
  - [ ] 8.10 `ProcessCommandAsync_DuplicateCommand_LogsDuplicateDetection` -- verify duplicate detection logging
  - [ ] 8.11 Create actor under test using Dapr.Actors test utilities or direct construction with mock `ActorHost` and mock `IActorStateManager`

- [ ] Task 9: Update FakeAggregateActor if needed (AC: #6)
  - [ ] 9.1 Review `Testing/Fakes/FakeAggregateActor.cs` -- the fake should continue to work as-is for integration tests since it implements `IAggregateActor` directly (not AggregateActor)
  - [ ] 9.2 If the fake needs idempotency simulation, add an optional in-memory dictionary to track processed causationIds
  - [ ] 9.3 The fake does NOT need to replicate the full orchestrator pattern -- it's for testing the caller (CommandRouter), not the actor internals

- [ ] Task 10: Update existing AggregateActor tests (AC: #6)
  - [ ] 10.1 Update any Story 3.1 tests that directly tested the STUB behavior to verify the new orchestrator flow
  - [ ] 10.2 Ensure the `ProcessCommandAsync_ValidCommand_ReturnsAccepted` test still passes (behavior unchanged for first-time commands)
  - [ ] 10.3 Ensure the `ProcessCommandAsync_ValidCommand_LogsCommandReceipt` test still passes (logging preserved)
  - [ ] 10.4 Ensure the `ProcessCommandAsync_ValidCommand_ReturnsCorrelationId` test still passes

- [ ] Task 11: Write integration tests (AC: #1, #2, #6)
  - [ ] 11.1 Add to `CommandRoutingIntegrationTests.cs` or create separate file:
  - [ ] 11.2 `PostCommands_DuplicateCausationId_ReturnsSuccess` -- submit same command twice (same causationId), verify both return 202 (second is idempotent, not an error)
  - [ ] 11.3 `PostCommands_DuplicateCausationId_DoesNotDuplicateProcessing` -- verify the fake actor receives only one invocation (the second is short-circuited by idempotency)
  - [ ] 11.4 `PostCommands_SameCorrelationIdDifferentCausationId_BothProcessed` -- verify replay scenario: same correlationId but different causationId results in both commands being processed (not blocked by idempotency)
  - [ ] 11.5 Verify all existing integration tests still pass (authentication, authorization, validation, replay, status, concurrency, routing)

- [ ] Task 12: Run all tests and verify zero regressions (AC: #6)
  - [ ] 12.1 Run all existing tests -- zero regressions expected
  - [ ] 12.2 Run new tests -- all must pass
  - [ ] 12.3 Verify total test count (estimated: ~371 existing from Story 3.1 + ~22 new = ~393)

## Dev Notes

### Architecture Compliance

**AggregateActor as Thin Orchestrator (architecture core pattern):**
The architecture specifies a 5-step delegation pattern. Story 3.2 transforms the Story 3.1 STUB into the beginning of the real orchestrator. Only Step 1 (idempotency check) is fully implemented. Steps 2-5 remain as inline stubs that log and pass through.

**Architecture Data Flow (Story 3.2 scope):**
```
AggregateActor.ProcessCommandAsync(CommandEnvelope command)
    |-- Log command receipt (preserved from Story 3.1)
    |-- Step 1: IdempotencyChecker.CheckAsync(causationId)
    |      |-- If duplicate: log + return cached result (DONE)
    |      |-- If new: continue to Step 2
    |-- Step 2: Tenant validation (STUB -> Story 3.3)
    |-- Step 3: State rehydration (STUB -> Story 3.4)
    |-- Step 4: Domain service invocation (STUB -> Story 3.5)
    |-- Step 5: State machine execution (STUB -> Story 3.11)
    |-- Create CommandProcessingResult(Accepted: true)
    |-- IdempotencyChecker.RecordAsync(causationId, result)
    |-- StateManager.SaveStateAsync() [atomic commit]
    |-- Return result
```

**Idempotency Key Pattern:**
State key: `idempotency:{causationId}` stored in `IActorStateManager`. Since IActorStateManager scopes state to the actor instance (identified by `{tenant}:{domain}:{aggregateId}`), there is no risk of cross-aggregate collisions. The key is just `idempotency:{causationId}` -- no need to include tenant/domain/aggregateId in the key since actor state is already partitioned.

**Why causationId, not correlationId:** See design decision F8. The Replay endpoint (Story 2.7) resubmits commands with the same `correlationId` but a new `causationId`. Using `correlationId` as the idempotency key would block replays. DAPR retries preserve both IDs, so `causationId` correctly catches retries while allowing intentional replays.

**Enforcement Rules to Follow:**
- Rule #5: Never log event payload data -- only envelope metadata (SEC-5, NFR12)
- Rule #6: Use `IActorStateManager` for all actor state operations -- never DaprClient bypass
- Rule #9: correlationId in every structured log entry (causationId also logged where relevant for idempotency tracing)
- Rule #10: Register services via `Add*` extension methods (IdempotencyChecker does not use DI -- see design decision F3 below)
- Rule #12: Status/archive writes are advisory (unchanged from Story 3.1)
- Rule #13: No stack traces in production error responses

### Critical Design Decisions

**F1 (Architecture): The AggregateActor is a THIN orchestrator, not a monolith.**
The actor body is ~20 lines of delegation. Each step delegates to a focused component. IdempotencyChecker is the first such component. Future stories (3.3-3.11) add TenantValidator, EventStreamReader, DomainServiceInvoker, and StateMachine.

**F2 (Pre-mortem): Idempotency check is Step 1 because it's the CHEAPEST.**
A single state read. If the command is a duplicate, ALL subsequent work (tenant validation, state rehydration, domain invocation, event persistence) is skipped. This prevents expensive re-processing and eliminates the risk of duplicate events.

**F3 (Design): IdempotencyChecker is NOT DI-registered.**
DAPR actors have a specific lifecycle. The `IActorStateManager` is scoped to the actor instance and is available only via `this.StateManager`. You CANNOT inject it via DI constructor because the state manager is set by the DAPR runtime after actor construction. Therefore, `IdempotencyChecker` is created directly in `ProcessCommandAsync` by passing `this.StateManager` to its constructor. This is a standard DAPR actor pattern -- components that need `IActorStateManager` are created by the actor, not resolved from DI.

**F4 (Atomicity): IdempotencyChecker.RecordAsync does NOT call SaveStateAsync.**
The `SetStateAsync` call buffers the state change. The actor calls `SaveStateAsync` once at the end to atomically commit ALL state changes (idempotency record + any future event persistence from Steps 3-5). This ensures that if the actor crashes between Step 1 and the final save, the idempotency record is NOT committed and the command can be safely retried.

**F5 (Data Integrity): IdempotencyRecord is a self-contained, storage-optimized DTO.**
It is NOT `CommandProcessingResult` directly. This allows the storage format to evolve independently of the public API type. The record is flat (all primitive/nullable types) for reliable JSON serialization in DAPR state store. It stores both `CausationId` (the idempotency key) and `CorrelationId` (for self-contained `ToResult()` reconstruction). This ensures the cached result can be returned without depending on the current request's context.

**F6 (Scope): Story 3.2 does NOT implement idempotency cleanup/TTL.**
Idempotency records accumulate in the actor state. For v1, this is acceptable because:
- Records are small (~100 bytes each)
- Each aggregate processes a bounded number of commands
- DAPR actor deactivation (60-min idle timeout) releases memory
- Persistent state grows slowly
- TTL-based cleanup of idempotency records is a future optimization, not a v1 requirement

**F7 (First Principles): Duplicate detection is per-aggregate, not global.**
Two different aggregates CAN have commands with the same causation ID (unlikely but theoretically possible if IDs collide). Idempotency is scoped to the actor instance (aggregate), which is correct -- the question is "has THIS aggregate already processed THIS causation ID?"

**F8 (Pre-mortem/First Principles): Idempotency key is `causationId`, NOT `correlationId`.**
This is a critical design decision discovered during advanced elicitation. The Replay endpoint (Story 2.7) resubmits commands with the **same** `correlationId` but a **new** `causationId`. If we used `correlationId` as the idempotency key, replays would be incorrectly blocked as duplicates. The correct behavior matrix:
- **Original submission:** correlationId=X, causationId=X → processed (new)
- **DAPR retry (infrastructure):** correlationId=X, causationId=X → blocked (correct -- same causationId)
- **Replay (Story 2.7):** correlationId=X, causationId=Y → processed (correct -- different causationId)
Using `causationId` gives us retry protection without blocking intentional replays. This is a v2 consideration for TTL: idempotency records keyed by causationId may accumulate for replayed commands, but this is acceptable per F6 (small records, bounded growth).

**What Already Exists (from Stories 1.1-3.1):**
- `CommandEnvelope` in Contracts -- 9-parameter record with AggregateIdentity, CorrelationId, etc.
- `AggregateIdentity` in Contracts -- canonical tuple with `ActorId` derivation
- `IAggregateActor` in Server/Actors/ -- DAPR actor interface extending IActor (Story 3.1)
- `AggregateActor` in Server/Actors/ -- STUB implementation that logs and returns Accepted (Story 3.1)
- `CommandProcessingResult` in Server/Actors/ -- record with `Accepted`, `ErrorMessage`, `CorrelationId` (Story 3.1)
- `ICommandRouter` + `CommandRouter` in Server/Commands/ (Story 3.1)
- `SubmitCommandExtensions` in Server/Commands/ -- SubmitCommand -> CommandEnvelope conversion (Story 3.1)
- `AddEventStoreServer()` in Server/Configuration/ -- DI extension (Story 3.1)
- `FakeAggregateActor` in Testing/Fakes/ (Story 3.1)
- `SubmitCommandHandler` with status write + archive write + CommandRouter call (Stories 2.6-2.7, modified in 3.1)
- MediatR pipeline: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> SubmitCommandHandler
- IExceptionHandler chain: Validation -> Authorization -> ConcurrencyConflict -> Global
- All Epic 2 infrastructure (JWT auth, authorization, validation, status tracking, replay, concurrency handling)

**What Story 3.2 Adds:**
1. **`IIdempotencyChecker`** -- interface in Server/Actors/
2. **`IdempotencyChecker`** -- implementation using IActorStateManager in Server/Actors/
3. **`IdempotencyRecord`** -- storage DTO record in Server/Actors/
4. **Modified `AggregateActor`** -- transformed from STUB to 5-step orchestrator (Step 1 real, Steps 2-5 stubs)

**What Story 3.2 Does NOT Change:**
- `IAggregateActor` interface (unchanged)
- `CommandProcessingResult` record (unchanged -- it already has the fields needed)
- `ICommandRouter` / `CommandRouter` (unchanged)
- `SubmitCommandHandler` (unchanged)
- `AddEventStoreServer()` (unchanged or minimal -- IdempotencyChecker is not DI-registered)
- `FakeAggregateActor` (minor update at most)
- Program.cs (unchanged)

### AggregateActor Orchestrator Pattern

```csharp
// In Server/Actors/AggregateActor.cs (after Story 3.2 transformation)
namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Aggregate actor -- thin orchestrator for command processing.
/// Story 3.2: Implements 5-step delegation pipeline.
/// Step 1 (idempotency) is real. Steps 2-5 are stubs for Stories 3.3-3.11.
/// </summary>
public class AggregateActor(ActorHost host, ILogger<AggregateActor> logger)
    : Actor(host), IAggregateActor
{
    public async Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);

        logger.LogInformation(
            "Actor {ActorId} received command: CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}",
            Host.Id,
            command.CorrelationId,
            command.TenantId,
            command.Domain,
            command.AggregateId,
            command.CommandType);

        // Step 1: Idempotency check (keyed by CausationId -- see F8)
        var causationId = command.CausationId ?? command.CorrelationId;
        var idempotencyChecker = new IdempotencyChecker(
            StateManager,
            host.LoggerFactory.CreateLogger<IdempotencyChecker>());

        CommandProcessingResult? cached = await idempotencyChecker
            .CheckAsync(causationId)
            .ConfigureAwait(false);

        if (cached is not null)
        {
            logger.LogInformation(
                "Duplicate command detected: CausationId={CausationId}, CorrelationId={CorrelationId}, ActorId={ActorId}. Returning cached result.",
                causationId,
                command.CorrelationId,
                Host.Id);
            return cached;
        }

        // Step 2: Tenant validation (STUB -- Story 3.3)
        logger.LogDebug("Step 2: Tenant validation -- STUB (Story 3.3)");

        // Step 3: State rehydration (STUB -- Story 3.4)
        logger.LogDebug("Step 3: State rehydration -- STUB (Story 3.4)");

        // Step 4: Domain service invocation (STUB -- Story 3.5)
        logger.LogDebug("Step 4: Domain service invocation -- STUB (Story 3.5)");

        // Step 5: State machine execution (STUB -- Story 3.11)
        logger.LogDebug("Step 5: State machine execution -- STUB (Story 3.11)");

        // Create result and store for idempotency
        var result = new CommandProcessingResult(
            Accepted: true,
            CorrelationId: command.CorrelationId);

        await idempotencyChecker
            .RecordAsync(causationId, result)
            .ConfigureAwait(false);

        // Atomic commit of all state changes
        await StateManager.SaveStateAsync().ConfigureAwait(false);

        return result;
    }
}
```

### IdempotencyChecker Pattern

```csharp
// In Server/Actors/IdempotencyChecker.cs
namespace Hexalith.EventStore.Server.Actors;

public class IdempotencyChecker(
    IActorStateManager stateManager,
    ILogger<IdempotencyChecker> logger) : IIdempotencyChecker
{
    private const string KeyPrefix = "idempotency:";

    public async Task<CommandProcessingResult?> CheckAsync(string causationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(causationId);

        string key = $"{KeyPrefix}{causationId}";
        var result = await stateManager
            .TryGetStateAsync<IdempotencyRecord>(key)
            .ConfigureAwait(false);

        if (result.HasValue)
        {
            logger.LogDebug("Idempotency cache hit: CausationId={CausationId}", causationId);
            return result.Value.ToResult();
        }

        logger.LogDebug("Idempotency cache miss: CausationId={CausationId}", causationId);
        return null;
    }

    public async Task RecordAsync(string causationId, CommandProcessingResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(causationId);
        ArgumentNullException.ThrowIfNull(result);

        string key = $"{KeyPrefix}{causationId}";
        var record = IdempotencyRecord.FromResult(causationId, result);

        await stateManager
            .SetStateAsync(key, record)
            .ConfigureAwait(false);

        logger.LogDebug("Idempotency record stored: CausationId={CausationId}", causationId);
    }
}
```

### DAPR Actor State Manager Notes

**IActorStateManager API used in this story:**
- `TryGetStateAsync<T>(string stateName)` -- returns `ConditionalValue<T>` (has `.HasValue` and `.Value`)
- `SetStateAsync<T>(string stateName, T value)` -- buffers state change (does NOT persist immediately)
- `SaveStateAsync()` -- atomically commits ALL buffered state changes to the state store

**Critical: SetStateAsync vs SaveStateAsync:**
`SetStateAsync` only buffers. Nothing is persisted until `SaveStateAsync` is called. This is by design -- it allows batching multiple state changes into a single atomic commit. The actor calls `SaveStateAsync` once at the end of `ProcessCommandAsync` to ensure all state changes are committed together.

**Mock setup for unit tests:**
```csharp
// NSubstitute mock for IActorStateManager
var stateManager = Substitute.For<IActorStateManager>();

// Configure TryGetStateAsync to return "not found"
stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>())
    .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

// Configure TryGetStateAsync to return a cached record (keyed by causationId)
var record = new IdempotencyRecord("test-causation", "test-corr", true, null, DateTimeOffset.UtcNow);
stateManager.TryGetStateAsync<IdempotencyRecord>("idempotency:test-causation")
    .Returns(new ConditionalValue<IdempotencyRecord>(true, record));
```

**ConditionalValue<T> from Dapr.Actors:**
This is in `Dapr.Actors.Runtime` namespace. It's a struct with `bool HasValue` and `T Value`. When state is not found, `HasValue` is false and `Value` is default.

### Technical Requirements

**Existing Types to Use:**
- `CommandEnvelope` from `Hexalith.EventStore.Contracts.Commands` -- input to ProcessCommandAsync
- `AggregateIdentity` from `Hexalith.EventStore.Contracts.Identity` -- canonical tuple (available via CommandEnvelope.AggregateIdentity)
- `IAggregateActor` from `Hexalith.EventStore.Server.Actors` -- actor interface (Story 3.1)
- `AggregateActor` from `Hexalith.EventStore.Server.Actors` -- STUB to be transformed (Story 3.1)
- `CommandProcessingResult` from `Hexalith.EventStore.Server.Actors` -- result record (Story 3.1)
- `ActorHost` from `Dapr.Actors.Runtime` -- actor host context (has `LoggerFactory` property)
- `Actor` from `Dapr.Actors.Runtime` -- base class (has `StateManager` property)
- `IActorStateManager` from `Dapr.Actors.Runtime` -- actor state management
- `ConditionalValue<T>` from `Dapr.Actors.Runtime` -- TryGetStateAsync return type

**New Types to Create:**
- `IIdempotencyChecker` -- interface in Server/Actors/
- `IdempotencyChecker` -- implementation in Server/Actors/
- `IdempotencyRecord` -- storage DTO record in Server/Actors/

**NuGet Packages Required:**
- `Dapr.Actors` 1.16.1 (already in Server.csproj from Story 3.1) -- `IActor`, `ActorId`
- `Dapr.Actors.AspNetCore` 1.16.1 (already from Story 3.1) -- `AddActors()`, `MapActorsHandlers()`
- All existing packages remain unchanged
- NO new NuGet packages needed for Story 3.2

### File Structure Requirements

**New files to create:**
```
src/Hexalith.EventStore.Server/
  Actors/
    IIdempotencyChecker.cs          # NEW: Idempotency check interface
    IdempotencyChecker.cs           # NEW: Implementation using IActorStateManager
    IdempotencyRecord.cs            # NEW: Storage DTO record

tests/Hexalith.EventStore.Server.Tests/
  Actors/
    IdempotencyCheckerTests.cs      # NEW: Unit tests for IdempotencyChecker
    IdempotencyRecordTests.cs       # NEW: Unit tests for IdempotencyRecord
```

**Existing files to modify:**
```
src/Hexalith.EventStore.Server/
  Actors/
    AggregateActor.cs               # MODIFY: Transform STUB to 5-step orchestrator

tests/Hexalith.EventStore.Server.Tests/
  Actors/
    AggregateActorTests.cs          # MODIFY: Update STUB tests, add orchestrator tests

tests/Hexalith.EventStore.IntegrationTests/
  CommandApi/
    CommandRoutingIntegrationTests.cs # MODIFY: Add duplicate command integration tests
```

**Files NOT modified:**
```
src/Hexalith.EventStore.Server/
  Actors/
    IAggregateActor.cs              # NO CHANGE: Interface unchanged
    CommandProcessingResult.cs      # NO CHANGE: Record already has needed fields
  Commands/
    ICommandRouter.cs               # NO CHANGE
    CommandRouter.cs                # NO CHANGE
    SubmitCommandExtensions.cs      # NO CHANGE
  Configuration/
    ServiceCollectionExtensions.cs  # NO CHANGE (or minimal comment)
  Pipeline/
    SubmitCommandHandler.cs         # NO CHANGE

src/Hexalith.EventStore.Testing/
  Fakes/
    FakeAggregateActor.cs           # NO CHANGE (or minimal)

src/Hexalith.EventStore.CommandApi/
  Program.cs                        # NO CHANGE
```

### Testing Requirements

**Test Projects:**
- `tests/Hexalith.EventStore.Server.Tests/` -- Unit tests for IdempotencyChecker, IdempotencyRecord, AggregateActor orchestrator
- `tests/Hexalith.EventStore.IntegrationTests/` -- Integration tests for duplicate command handling

**Test Patterns (established in Stories 1.6, 2.1-3.1):**
- Method naming: `{Method}_{Scenario}_{ExpectedResult}`
- Arrange/Act/Assert pattern
- Shouldly for assertions
- `WebApplicationFactory<CommandApiProgram>` for integration tests
- `TestJwtTokenGenerator` for creating JWT tokens with specific claims
- Primary constructors for DI injection
- Feature folder organization in test projects mirroring source
- NSubstitute for mocking `IActorStateManager`

**Unit Test Strategy for IdempotencyChecker:**
Mock `IActorStateManager` using NSubstitute. Verify:
- `TryGetStateAsync` called with correct key pattern `idempotency:{causationId}`
- Correct handling of `ConditionalValue` (found vs not found)
- `SetStateAsync` called with correct key and `IdempotencyRecord`
- `SaveStateAsync` NOT called (actor's responsibility)

**Unit Test Strategy for AggregateActor:**
Create actor using Dapr.Actors test utilities. Mock `IActorStateManager`. Verify:
- 5-step pipeline execution order
- Idempotency check short-circuits for duplicates
- Non-duplicate commands proceed to stubs and create result
- `SaveStateAsync` called once at end for new commands
- `SaveStateAsync` NOT called for duplicates
- Logging at correct levels

**Integration Test Strategy:**
Use existing `FakeAggregateActor` in `WebApplicationFactory`. Submit commands via POST `/api/v1/commands` and verify:
- First submission returns 202 Accepted
- Duplicate submission also returns 202 Accepted (idempotent)
- FakeAggregateActor can optionally track invocation count

**Note on DAPR Actor testing complexity:**
Testing DAPR actors in unit tests requires creating an `ActorHost` mock. The Dapr SDK provides `ActorHost.CreateForTest<T>()` for test scenarios. If this is not available in the SDK version, use reflection or a thin wrapper to set the `StateManager` property.

**Minimum Tests (~22):**

IdempotencyChecker Unit Tests (8) -- in `IdempotencyCheckerTests.cs`:
1. `CheckAsync_NoExistingRecord_ReturnsNull`
2. `CheckAsync_ExistingRecord_ReturnsCachedResult`
3. `RecordAsync_StoresIdempotencyRecord`
4. `RecordAsync_DoesNotCallSaveState`
5. `CheckAsync_NullCausationId_ThrowsArgumentException`
6. `RecordAsync_NullCausationId_ThrowsArgumentException`
7. `RecordAsync_NullResult_ThrowsArgumentNullException`
8. `CheckAsync_CorrectKeyFormat_UsesIdempotencyPrefix`

IdempotencyRecord Unit Tests (3) -- in `IdempotencyRecordTests.cs`:
9. `FromResult_MapsAllFields`
10. `ToResult_ReconstructsResult`
11. `JsonRoundtrip_PreservesAllFields`

AggregateActor Orchestrator Tests (8) -- in `AggregateActorTests.cs`:
12. `ProcessCommandAsync_NewCommand_ExecutesFullPipeline`
13. `ProcessCommandAsync_NewCommand_ReturnsAccepted`
14. `ProcessCommandAsync_NewCommand_StoresIdempotencyRecord`
15. `ProcessCommandAsync_NewCommand_CallsSaveStateAsync`
16. `ProcessCommandAsync_DuplicateCommand_ReturnsCachedResult`
17. `ProcessCommandAsync_DuplicateCommand_SkipsSteps2Through5`
18. `ProcessCommandAsync_DuplicateCommand_DoesNotCallSaveState`
19. `ProcessCommandAsync_ValidCommand_LogsCommandReceipt`

Integration Tests (3+) -- in `CommandRoutingIntegrationTests.cs`:
20. `PostCommands_DuplicateCausationId_ReturnsSuccess`
21. `PostCommands_SameCorrelationIdDifferentCausationId_BothProcessed` (replay scenario)
22. `PostCommands_ExistingTests_StillPass` (regression check)

**Current test count:** ~371 test methods from Story 3.1. Story 3.2 adds ~22 new tests, bringing estimated total to ~393.

### Previous Story Intelligence

**From Story 3.1 (Command Router & Actor Activation):**
- `AggregateActor` created as STUB in `Server/Actors/` -- this is what Story 3.2 transforms
- Actor constructor pattern: `AggregateActor(ActorHost host, ILogger<AggregateActor> logger) : Actor(host)`
- `Host.Id` used to log actor ID (available from `Actor` base class)
- Actor registered via `options.Actors.RegisterActor<AggregateActor>()` in AddEventStoreServer()
- `CommandProcessingResult(bool Accepted, string? ErrorMessage = null, string? CorrelationId = null)` -- record
- Story 3.1 design decisions F5 and F6 explicitly note the STUB scope and that Story 3.2 begins real orchestrator
- Test fakes: `FakeAggregateActor` in Testing/Fakes/ implements `IAggregateActor` directly (not AggregateActor subclass)
- Integration tests mock `ICommandRouter` at test level (simpler than full DAPR actor infrastructure)

**From Story 2.8 (Optimistic Concurrency Conflict Handling):**
- `ConcurrencyConflictExceptionHandler` unwraps DAPR `ActorMethodInvocationException` -- relevant for future stories but not 3.2
- Exception handler chain preserves 3.2's exceptions propagating correctly through the pipeline

**From Story 2.7 (Command Replay Endpoint):**
- `ArchivedCommandExtensions` follow same extension pattern for `SubmitCommandExtensions` from Story 3.1
- Advisory write pattern (try/catch, log Warning, continue) applies to status/archive writes but NOT actor invocation or idempotency

**Key Patterns (mandatory for all new code):**
- Primary constructors for DI: `public class Foo(IDep dep) : Base`
- Records for immutable data
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` on public methods (CA1062)
- `ArgumentException.ThrowIfNullOrWhiteSpace()` for string parameters
- Feature folder organization
- `namespace Hexalith.EventStore.{Project}.{Feature};`
- Registration via `Add*` extension methods (rule #10) -- except actor-scoped components

### Git Intelligence

**Recent commit patterns (last 5 merged):**
- `Stories 2.4 & 2.5: JWT Authentication & Endpoint Authorization (#24)` -- multi-story PRs acceptable
- `Story 2.3: MediatR Pipeline & Logging Behavior + Story planning`
- `Story 2.2: Command Validation & RFC 7807 Error Responses`
- `Story 2.1: CommandApi Host & Minimal Endpoint Scaffolding + Story 2.2 context`
- `Story 1.6: Contracts Unit Tests (Tier 1) (#19)`

**Patterns observed:**
- Stories implemented sequentially in dedicated feature branches
- PR titles follow `Story X.Y: Description (#PR)` format
- Clean merge commits from pull requests
- NSubstitute used for mocking across all test projects
- Shouldly for all assertions
- Primary constructors throughout codebase

### Latency Design Note

**Story 3.2 adds one IActorStateManager read per command (idempotency check).** For new commands, this adds ~1-2ms (single key lookup via DAPR sidecar, NFR8). For duplicate commands, this is the ONLY cost -- all subsequent processing is skipped. The `SaveStateAsync` call at the end adds ~1-2ms for the state write. Total additional latency for new commands: ~2-4ms (well within the 50ms budget NFR1).

### DAPR Actor IActorStateManager Scoping Note

Each DAPR actor instance has its own `IActorStateManager` that scopes all state to that actor. State keys are automatically namespaced by the actor type and ID. This means `idempotency:abc-123` (where abc-123 is a causationId) for actor `acme:orders:order-42` is completely isolated from `idempotency:abc-123` for actor `acme:orders:order-99`. No explicit namespacing needed in the key.

### Project Structure Notes

**Alignment with Architecture:**
- `IIdempotencyChecker`, `IdempotencyChecker`, `IdempotencyRecord` in `Server/Actors/` per architecture directory structure (alongside `IAggregateActor`, `AggregateActor`, `CommandProcessingResult`)
- Test files mirror source structure in feature folders (`Server.Tests/Actors/`)
- No new projects or packages added -- all changes within existing Server project

**Dependency Graph (unchanged from Story 3.1):**
```
CommandApi -> Server -> Contracts
Server/Actors/AggregateActor -> Dapr.Actors.Runtime (Actor, ActorHost, IActorStateManager)
Server/Actors/IdempotencyChecker -> Dapr.Actors.Runtime (IActorStateManager)
Testing/Fakes/FakeAggregateActor -> Server (IAggregateActor)
Tests: Server.Tests -> Server + Dapr.Actors (unit testing)
Tests: IntegrationTests -> CommandApi (via WebApplicationFactory)
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 3.2: AggregateActor Orchestrator & Idempotency Check]
- [Source: _bmad-output/planning-artifacts/architecture.md#Communication Patterns - Actor Processing Pipeline]
- [Source: _bmad-output/planning-artifacts/architecture.md#AggregateActor as Thin Orchestrator]
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure - Server/Actors/]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines - Rules #5, #6, #9, #10, #12]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1: Event Storage Strategy - Actor-level ACID]
- [Source: _bmad-output/implementation-artifacts/3-1-command-router-and-actor-activation.md]
- [Source: https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-actors/dotnet-actors-howto/ - DAPR .NET Actors SDK]
- [Source: https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/ - DAPR Actors Overview]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
