# Story 2.7: Command Replay Endpoint

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an **API consumer**,
I want to replay a previously failed command via POST `/api/v1/commands/replay/{correlationId}` after the root cause has been fixed,
So that I can recover from transient or fixed failures without re-creating the command (FR6).

## Acceptance Criteria

1. **Replay resubmits original command** - Given a command previously failed (status: Rejected, PublishFailed, or TimedOut), When I POST `/api/v1/commands/replay/{correlationId}`, Then the original command is resubmitted to the processing pipeline with the same correlation ID and a new causation ID is generated to distinguish the replay from the original submission.

2. **Status reset to Received** - When the replay is initiated, Then the command status is reset to `Received` as part of the `SubmitCommandHandler` flow (same advisory write pattern as initial submission, enforcement rule #12). The controller does NOT manually reset status -- the handler does it atomically when it processes the replayed command, avoiding a stuck-state window.

3. **409 Conflict for non-replayable statuses** - Replaying a command with status `Completed` or `Processing` (or any in-flight status: `Received`, `EventsStored`, `EventsPublished`) returns `409 Conflict` as RFC 7807 ProblemDetails with `correlationId` extension and a human-readable `detail` explaining the current status and why replay is not allowed.

4. **404 for non-existent or expired correlation ID** - Replaying a non-existent or expired correlation ID returns `404 Not Found` as ProblemDetails with `correlationId` extension. Detail: `"No command found for correlation ID '{correlationId}'."`.

5. **Tenant-scoped replay authorization (SEC-3)** - Authorization rules apply to the replay: the JWT must be authorized for the command's tenant/domain. Tenant scoping follows the same pattern as Story 2.6 status queries -- the JWT `eventstore:tenant` claims must include the archived command's tenant. Returns `404 Not Found` for tenant mismatches (not 403) to avoid information leakage.

6. **403 for missing tenant claims** - If the user has zero `eventstore:tenant` claims, return `403 Forbidden` as ProblemDetails. Detail: `"No tenant authorization claims found. Access denied."`.

7. **Replay endpoint requires authentication** - The POST replay endpoint requires a valid JWT token (`[Authorize]` attribute). Unauthenticated requests return `401 Unauthorized`.

## Prerequisites

**BLOCKING: Stories 2.4 (JWT Authentication), 2.5 (Endpoint Authorization), and 2.6 (Command Status Tracking) MUST be complete before starting Story 2.7.** Story 2.7 depends on:
- `[Authorize]` attribute enforcement on controllers (Story 2.4)
- `EventStoreClaimsTransformation` providing `eventstore:tenant` claims (Story 2.4)
- `TestJwtTokenGenerator` for integration tests (Story 2.4)
- `ICommandStatusStore` for reading current status and writing reset status (Story 2.6)
- `DaprClient` registration via `AddDaprClient()` (Story 2.6)
- `CommandStatusRecord` and `CommandStatus` enum (Contracts package)
- Established ProblemDetails error handling patterns (Stories 2.1-2.2)
- MediatR pipeline with LoggingBehavior, ValidationBehavior, AuthorizationBehavior (Stories 2.3, 2.5)

**Before beginning any Task below, verify:** Run existing tests to confirm Stories 2.4-2.6 artifacts are in place. If `ICommandStatusStore` does not exist or `DaprClient` is not registered, prerequisites are not met -- do NOT proceed.

## Tasks / Subtasks

- [ ] Task 0: Verify prerequisites and existing artifacts (BLOCKING)
  - [ ] 0.1 Confirm Stories 2.4-2.6 artifacts are in place (`[Authorize]`, `EventStoreClaimsTransformation`, `TestJwtTokenGenerator`, `AuthorizationBehavior`, `ICommandStatusStore`, `DaprCommandStatusStore`, `InMemoryCommandStatusStore`, `CommandStatusController`)
  - [ ] 0.2 Run all existing tests -- they must pass before proceeding
  - [ ] 0.3 Verify `DaprClient` is registered via `AddDaprClient()` in Program.cs (established in Story 2.6)
  - [ ] 0.4 Verify `CommandStatusRecord` exists in Contracts with all 7 fields

- [ ] Task 1: Create ICommandArchiveStore abstraction and DAPR implementation (AC: #1, #4, #5)
  - [ ] 1.1 Create `ICommandArchiveStore` interface in `Server/Commands/` with methods: `WriteCommandAsync(string tenantId, string correlationId, ArchivedCommand command, CancellationToken ct)` and `ReadCommandAsync(string tenantId, string correlationId, CancellationToken ct)` returning `ArchivedCommand?`
  - [ ] 1.2 Create `ArchivedCommand` record in `Contracts/Commands/` with fields: `Tenant` (string), `Domain` (string), `AggregateId` (string), `CommandType` (string), `Payload` (byte[]), `Extensions` (Dictionary<string, string>?), `OriginalTimestamp` (DateTimeOffset) -- captures all data needed to reconstruct a `SubmitCommand`
  - [ ] 1.3 Create `DaprCommandArchiveStore` in `Server/Commands/` implementing `ICommandArchiveStore` using `DaprClient.SaveStateAsync` and `DaprClient.GetStateAsync`
  - [ ] 1.4 State store key format: `{tenantId}:{correlationId}:command` per D2 key namespace convention. Use a constant `CommandArchiveConstants.KeySuffix = "command"`
  - [ ] 1.5 Write operations include TTL metadata matching status TTL: use same `CommandStatusOptions.TtlSeconds` (default 86400 seconds / 24 hours) so command archive and status expire together
  - [ ] 1.6 Read operations: `DaprClient.GetStateAsync<ArchivedCommand>(storeName, key)` returns `null` for non-existent/expired keys
  - [ ] 1.7 All DaprClient calls wrapped with `ConfigureAwait(false)` and advisory error handling: catch exceptions, log at Warning level, and return gracefully for writes. For reads, catch and log but return `null` (failed archive read prevents replay, which is acceptable)

- [ ] Task 2: Create InMemoryCommandArchiveStore for testing (AC: #1)
  - [ ] 2.1 Create `InMemoryCommandArchiveStore` in `Testing/Fakes/` implementing `ICommandArchiveStore`
  - [ ] 2.2 Use `ConcurrentDictionary<string, (ArchivedCommand Command, DateTimeOffset Expiry)>` for storage with key = `{tenantId}:{correlationId}:command`
  - [ ] 2.3 Implement TTL expiry simulation: `ReadCommandAsync` returns null if entry has expired
  - [ ] 2.4 Add helper methods for test assertions: `GetAllArchived()`, `GetArchiveCount()`, `Clear()`

- [ ] Task 3: Modify SubmitCommandHandler to archive original command (AC: #1)
  - [ ] 3.1 Inject `ICommandArchiveStore` into `SubmitCommandHandler` via primary constructor
  - [ ] 3.2 After creating the `SubmitCommandResult` and AFTER the existing status write (from Story 2.6), call `_archiveStore.WriteCommandAsync(command.Tenant, command.CorrelationId, ArchivedCommand.FromSubmitCommand(command), cancellationToken)` BEFORE returning the result
  - [ ] 3.3 Wrap the archive write in try/catch: log at Warning level if write fails but DO NOT throw -- return the SubmitCommandResult regardless (enforcement rule #12: advisory writes never block pipeline)
  - [ ] 3.4 Log successful archive write at Debug level: correlationId, tenantId

- [ ] Task 4: Create ReplayController with POST endpoint (AC: #1, #2, #3, #4, #5, #6, #7)
  - [ ] 4.1 Create `ReplayController` in `CommandApi/Controllers/` with route `[Route("api/v1/commands/replay")]` and `[Authorize]` attribute
  - [ ] 4.2 Create `POST /{correlationId}` action that accepts `correlationId` as a route parameter
  - [ ] 4.3 Extract authenticated user's `eventstore:tenant` claims from `User.FindAll("eventstore:tenant")` with empty/whitespace filtering (same pattern as Story 2.5/2.6)
  - [ ] 4.4 If user has zero `eventstore:tenant` claims: return `403 Forbidden` as ProblemDetails. Detail: `"No tenant authorization claims found. Access denied."`
  - [ ] 4.5 For each authorized tenant, attempt to read the archived command via `_archiveStore.ReadCommandAsync(tenant, correlationId, ct)`. Stop on first match
  - [ ] 4.6 If no archived command found across all authorized tenants: return `404 Not Found` as ProblemDetails with `correlationId` extension. Detail: `"No command found for correlation ID '{correlationId}'."`
  - [ ] 4.7 Read current status via `_statusStore.ReadStatusAsync(foundTenant, correlationId, ct)`
  - [ ] 4.8 Validate status is replayable: only `Rejected`, `PublishFailed`, or `TimedOut` are replayable terminal statuses
  - [ ] 4.9 If status is `Completed`, `Processing`, `Received`, `EventsStored`, or `EventsPublished`: return `409 Conflict` as ProblemDetails with `correlationId` and `currentStatus` extensions. Detail varies:
    - Completed: `"Command '{correlationId}' has already completed successfully. Replay is not permitted for completed commands. Replay is permitted only for commands with terminal failure status (Rejected, PublishFailed, TimedOut)."`
    - Processing/Received/EventsStored/EventsPublished: `"Command '{correlationId}' is currently in-flight (status: {status}). Wait for processing to complete or time out before replaying. Replay is permitted only for commands with terminal failure status (Rejected, PublishFailed, TimedOut)."`
  - [ ] 4.10 If status is null (no status record found but archived command exists -- edge case, e.g., status expired before archive): return `409 Conflict` as ProblemDetails. Detail: `"Status tracking for command '{correlationId}' has expired. Cannot determine replayability. Replay is permitted only for commands with terminal failure status (Rejected, PublishFailed, TimedOut)."` Extensions: `correlationId`, `currentStatus: "Unknown"`. **Rationale:** null status could mean the command completed successfully and its status expired -- replaying a Completed command violates AC #3
  - [ ] 4.11 Create new `SubmitCommand` from the archived command: same Tenant, Domain, AggregateId, CommandType, Payload, same CorrelationId, same Extensions. **Do NOT manually reset status to Received** -- the `SubmitCommandHandler` will write Received status as part of its normal flow (Story 2.6). This avoids a stuck-state window where status reads as Received but the pipeline hasn't started yet.
  - [ ] 4.12 Send the new `SubmitCommand` through the MediatR pipeline via `_mediator.Send(command, ct)` -- this routes through LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> SubmitCommandHandler (the full pipeline ensures consistent processing, and the handler writes Received status atomically)
  - [ ] 4.13 Return `202 Accepted` with response body containing `correlationId` and `isReplay: true` flag, `Location` header pointing to status endpoint, and `Retry-After: 1` header
  - [ ] 4.14 Store correlationId and tenantId in HttpContext.Items for error handler access
  - [ ] 4.15 Add `[ProducesResponseType]` attributes for OpenAPI documentation: 202, 401, 403, 404, 409
  - [ ] 4.16 Log replay initiation at Information level with structured properties: `correlationId`, `tenantId`, `previousStatus`, `isReplay=true`. This provides operator visibility into replay activity without requiring a separate tracking store

- [ ] Task 5: Create ReplayCommandResponse model (AC: #1)
  - [ ] 5.1 Create `ReplayCommandResponse` record in `CommandApi/Models/` with: `CorrelationId` (string), `IsReplay` (bool -- always `true`), `PreviousStatus` (string? -- the status before replay, e.g., "Rejected")
  - [ ] 5.2 This is a distinct response type from `SubmitCommandResponse` to clearly signal to clients that this is a replay operation

- [ ] Task 6: Create ConflictExceptionHandler for 409 responses (AC: #3)
  - [ ] 6.1 Create `CommandConflictException` in `CommandApi/ErrorHandling/` with properties: `CorrelationId` (string), `CurrentStatus` (string), `Reason` (string)
  - [ ] 6.2 Create `ConflictExceptionHandler` implementing `IExceptionHandler` that handles `CommandConflictException`
  - [ ] 6.3 Map to 409 ProblemDetails with: `Status = 409`, `Title = "Conflict"`, `Type = "https://tools.ietf.org/html/rfc9457#section-3"`, `Detail` from exception `Reason`, `Instance` = request path, `Extensions = { correlationId, currentStatus }`
  - [ ] 6.4 Register `ConflictExceptionHandler` in `AddCommandApi()` BEFORE `GlobalExceptionHandler` (same pattern as `AuthorizationExceptionHandler`)
  - [ ] 6.5 **Alternative approach**: Instead of exception+handler, the controller can return ProblemDetails directly (as `ObjectResult`). Choose whichever is more consistent with the existing codebase pattern. If the controller handles 409 inline (like Story 2.6 handles 404 inline), skip the exception+handler and return ProblemDetails directly from the controller

- [ ] Task 7: Register ICommandArchiveStore in DI (AC: all)
  - [ ] 7.1 In `ServiceCollectionExtensions.AddCommandApi()`, register `DaprCommandArchiveStore` as `ICommandArchiveStore` (singleton, since DaprClient is thread-safe)
  - [ ] 7.2 In integration tests, override registration with `InMemoryCommandArchiveStore` via WebApplicationFactory

- [ ] Task 8: Write unit tests for DaprCommandArchiveStore (AC: #1, #4)
  - [ ] 8.1 `WriteCommandAsync_ValidCommand_CallsSaveStateWithCorrectKey` -- verify key format `{tenant}:{correlationId}:command`
  - [ ] 8.2 `WriteCommandAsync_IncludesTtlMetadata_Default86400Seconds` -- verify TTL metadata passed to SaveStateAsync
  - [ ] 8.3 `WriteCommandAsync_DaprClientThrows_LogsWarningAndDoesNotThrow` -- verify advisory write behavior (rule #12)
  - [ ] 8.4 `ReadCommandAsync_ExistingKey_ReturnsArchivedCommand` -- verify successful read
  - [ ] 8.5 `ReadCommandAsync_NonExistentKey_ReturnsNull` -- verify null for missing entry
  - [ ] 8.6 `ReadCommandAsync_DaprClientThrows_LogsWarningAndReturnsNull` -- verify graceful failure on read

- [ ] Task 9: Write unit tests for SubmitCommandHandler archive write (AC: #1)
  - [ ] 9.1 `Handle_ValidCommand_WritesArchivedCommandToStore` -- verify archived command written with correct tenant, correlationId, all fields
  - [ ] 9.2 `Handle_ArchiveWriteFails_StillReturnsResult` -- verify handler doesn't fail if archive write throws (rule #12)
  - [ ] 9.3 `Handle_ArchiveWriteFails_LogsWarning` -- verify warning logged on archive write failure
  - [ ] 9.4 `Handle_ArchivedCommand_ContainsAllOriginalFields` -- verify Tenant, Domain, AggregateId, CommandType, Payload, Extensions are preserved

- [ ] Task 10: Write unit tests for ReplayController (AC: #1, #2, #3, #4, #5, #6, #7)
  - [ ] 10.1 `Replay_RejectedStatus_Returns202WithReplayResponse` -- verify replay of Rejected command succeeds
  - [ ] 10.2 `Replay_PublishFailedStatus_Returns202` -- verify replay of PublishFailed command succeeds
  - [ ] 10.3 `Replay_TimedOutStatus_Returns202` -- verify replay of TimedOut command succeeds
  - [ ] 10.4 `Replay_CompletedStatus_Returns409ProblemDetails` -- verify 409 for Completed commands
  - [ ] 10.5 `Replay_ProcessingStatus_Returns409ProblemDetails` -- verify 409 for in-flight commands
  - [ ] 10.6 `Replay_ReceivedStatus_Returns409ProblemDetails` -- verify 409 for Received (already being processed)
  - [ ] 10.7 `Replay_NonExistentCorrelationId_Returns404ProblemDetails` -- verify 404 for unknown ID
  - [ ] 10.8 `Replay_TenantMismatch_Returns404ProblemDetails` -- verify tenant-scoped (SEC-3), returns 404 not 403
  - [ ] 10.9 `Replay_NoTenantClaims_Returns403ProblemDetails` -- verify 403 when no tenant claims
  - [ ] 10.10 `Replay_StatusResetByHandler` -- verify status is reset to Received by SubmitCommandHandler (not by controller), verifying mediator.Send is called and handler writes status
  - [ ] 10.11 `Replay_ResubmitsThroughMediatRPipeline` -- verify _mediator.Send() is called with correct SubmitCommand
  - [ ] 10.12 `Replay_PreservesOriginalCorrelationId` -- verify same correlation ID is used for resubmission
  - [ ] 10.13 `Replay_NullStatus_ArchiveExists_Returns409ProblemDetails` -- verify edge case where status expired returns 409 (indeterminate, not replayable)

- [ ] Task 11: Write integration tests for replay flow (AC: #1, #2, #3, #4, #5, #6, #7)
  - [ ] 11.1 `PostReplay_RejectedCommand_Returns202` -- submit command, set status to Rejected, replay, verify 202 with isReplay=true
  - [ ] 11.2 `PostReplay_PublishFailedCommand_Returns202` -- submit, set PublishFailed, replay, verify 202
  - [ ] 11.3 `PostReplay_TimedOutCommand_Returns202` -- submit, set TimedOut, replay, verify 202
  - [ ] 11.4 `PostReplay_CompletedCommand_Returns409ProblemDetails` -- submit, set Completed, replay, verify 409 with correlationId and currentStatus extensions
  - [ ] 11.5 `PostReplay_InFlightCommand_Returns409ProblemDetails` -- submit (status Received), replay immediately, verify 409
  - [ ] 11.6 `PostReplay_NonExistentCorrelationId_Returns404ProblemDetails` -- replay unknown ID, verify 404
  - [ ] 11.7 `PostReplay_WrongTenant_Returns404ProblemDetails` -- submit as tenant A, replay as tenant B, verify 404 (SEC-3)
  - [ ] 11.8 `PostReplay_NoAuthentication_Returns401` -- no JWT token returns 401
  - [ ] 11.9 `PostReplay_NoTenantClaims_Returns403` -- JWT with no tenant claims returns 403
  - [ ] 11.10 `PostReplay_ReplayedCommand_StatusResetToReceived` -- submit, set Rejected, replay, query status, verify Received
  - [ ] 11.11 `PostReplay_LocationHeader_PointsToStatusEndpoint` -- verify Location header format
  - [ ] 11.12 `PostReplay_ResponseIncludesCorrelationIdInProblemDetails` -- verify 404/409 ProblemDetails have correlationId extension
  - [ ] 11.13 `PostReplay_ResponseIncludesPreviousStatus` -- verify replay response includes previousStatus field
  - [ ] 11.14 `PostReplay_ReplayedThenFailedAgain_CanReplayAgain` -- submit, set Rejected, replay, set Rejected again, replay again, verify 202 (re-replay is permitted per H2)
  - [ ] 11.15 `PostReplay_ValidationRulesChanged_Returns400` -- submit command, add stricter validation rule, set status to Rejected, replay, verify 400 from ValidationBehavior (replayed command goes through full pipeline including validation)

- [ ] Task 12: Update existing tests if needed (AC: all)
  - [ ] 12.1 Update `SubmitCommandHandler` unit tests to provide `ICommandArchiveStore` mock (existing tests may break without it)
  - [ ] 12.2 Update WebApplicationFactory configuration to register `InMemoryCommandArchiveStore`
  - [ ] 12.3 Verify all existing tests still pass after adding archive store dependency to SubmitCommandHandler

## Dev Notes

### Architecture Compliance

**FR6: Failed Command Replay:**
- The replay endpoint enables operational recovery from transient or fixed failures
- Original command data is preserved in the archive store alongside the status store
- Replay generates a new causation chain (new causation ID) while maintaining the same correlation ID for end-to-end traceability
- Authorization rules are fully enforced on replay requests

**D2: Command Status Storage -- Extended for Replay:**
- Status key: `{tenant}:{correlationId}:status` (existing from Story 2.6)
- Archive key: `{tenant}:{correlationId}:command` (NEW in Story 2.7)
- Both use same TTL (24-hour default) so they expire together
- Both use same DAPR state store instance
- Both follow advisory write pattern (rule #12)

**SEC-3: Tenant-Scoped Replay Authorization:**
- JWT tenant must match the archived command's tenant
- Returns `404 Not Found` for tenant mismatches (not 403) to avoid information leakage about which correlation IDs exist for other tenants
- Same isolation pattern as Story 2.6 status queries

**Enforcement Rules to Follow:**
- Rule #5: Never log event payload data -- only envelope metadata (SEC-5, NFR12)
- Rule #7: ProblemDetails for all API error responses -- 404, 403, 409 must all be ProblemDetails
- Rule #9: correlationId in every structured log entry and OpenTelemetry activity
- Rule #10: Register services via `Add*` extension methods -- never inline in Program.cs
- Rule #12: **CRITICAL** -- Status writes and archive writes are advisory. Failure MUST NEVER block or fail the command processing pipeline
- Rule #13: No stack traces in production error responses

### Critical Design Decisions

**What Already Exists (from Stories 2.1-2.6):**
- `CommandsController` with `[Authorize]` and POST `/api/v1/commands` -- returns 202 with Location header
- `CommandStatusController` with GET `/api/v1/commands/status/{correlationId}` -- tenant-scoped status queries (Story 2.6)
- `ICommandStatusStore` + `DaprCommandStatusStore` + `InMemoryCommandStatusStore` -- status read/write (Story 2.6)
- `CommandStatus` enum (8 states) and `CommandStatusRecord` in Contracts package
- `SubmitCommand` MediatR command record with Tenant, Domain, AggregateId, CommandType, Payload, CorrelationId, Extensions
- `SubmitCommandHandler` -- writes Received status (Story 2.6), returns SubmitCommandResult
- MediatR pipeline: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> SubmitCommandHandler
- `CorrelationIdMiddleware` generates/propagates correlation IDs
- `EventStoreClaimsTransformation` normalizes JWT claims to `eventstore:*`
- ProblemDetails error handling: `ValidationExceptionHandler` (400), `AuthorizationExceptionHandler` (403), `GlobalExceptionHandler` (500)
- `TestJwtTokenGenerator` for integration tests with configurable claims
- `DaprClient` registered via `AddDaprClient()` (Story 2.6)
- `CommandStatusOptions` with TtlSeconds and StateStoreName (Story 2.6)

**What Story 2.7 Adds:**
1. **`ArchivedCommand`** -- record in Contracts storing the original command data for replay
2. **`ICommandArchiveStore`** -- abstraction for command archive read/write
3. **`DaprCommandArchiveStore`** -- DAPR implementation using `DaprClient` with key `{tenant}:{correlationId}:command`
4. **`InMemoryCommandArchiveStore`** -- test fake with TTL simulation
5. **`ReplayController`** -- POST `/api/v1/commands/replay/{correlationId}` endpoint
6. **`ReplayCommandResponse`** -- API response model with isReplay flag and previousStatus
7. **Modified `SubmitCommandHandler`** -- now archives original command alongside status write
8. **409 Conflict handling** -- for non-replayable command statuses (via inline ProblemDetails or ConflictExceptionHandler)

**Replay Flow:**
```
POST /api/v1/commands/replay/{correlationId}
    |
    v
CorrelationIdMiddleware (propagate)
    |
    v
JWT Authentication (layer 1) + Claims Transformation (layer 2) [Story 2.4]
    |
    v
[Authorize] policy (layer 3) [Story 2.4]
    |
    v
ReplayController.Replay()
    |-- Extract eventstore:tenant claims from User
    |-- If no tenant claims -> 403 ProblemDetails
    |-- For each authorized tenant:
    |     Call _archiveStore.ReadCommandAsync(tenant, correlationId)
    |     If found -> continue with this tenant
    |-- If not found in any tenant -> 404 ProblemDetails
    |
    |-- Read current status via _statusStore.ReadStatusAsync(tenant, correlationId)
    |-- Validate replayable:
    |     Rejected, PublishFailed, TimedOut -> proceed
    |     Completed -> 409 ProblemDetails
    |     Received, Processing, EventsStored, EventsPublished -> 409 ProblemDetails
    |     null (no status found) -> 409 ProblemDetails (indeterminate -- may have been Completed)
    |
    |-- Create SubmitCommand from archived data (same correlationId)
    |     (NO manual status reset -- SubmitCommandHandler handles it atomically)
    |-- Send through MediatR pipeline:
    |     LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> SubmitCommandHandler
    |     (SubmitCommandHandler writes Received status + re-archives -- no stuck-state window)
    |-- Return 202 Accepted with ReplayCommandResponse + Location header
```

**Command Archive Flow (in SubmitCommandHandler):**
```
SubmitCommandHandler.Handle(SubmitCommand)
    |-- Create SubmitCommandResult with correlationId
    |-- TRY: Write Received status to state store (existing from Story 2.6)
    |     Key: {tenant}:{correlationId}:status
    |     Value: CommandStatusRecord(Received, UtcNow, aggregateId)
    |     Metadata: { "ttlInSeconds": "86400" }
    |-- CATCH: Log Warning, continue (rule #12)
    |
    |-- TRY: Archive original command (NEW in Story 2.7)
    |     Key: {tenant}:{correlationId}:command
    |     Value: ArchivedCommand(Tenant, Domain, AggregateId, CommandType, Payload, Extensions, UtcNow)
    |     Metadata: { "ttlInSeconds": "86400" }
    |-- CATCH: Log Warning, continue (rule #12)
    |
    |-- Return SubmitCommandResult
```

**ArchivedCommand Record:**
```csharp
// In Contracts/Commands/
public record ArchivedCommand(
    string Tenant,
    string Domain,
    string AggregateId,
    string CommandType,
    byte[] Payload,
    Dictionary<string, string>? Extensions,
    DateTimeOffset OriginalTimestamp)
{
    public static ArchivedCommand FromSubmitCommand(SubmitCommand command) =>
        new(command.Tenant, command.Domain, command.AggregateId,
            command.CommandType, command.Payload, command.Extensions,
            DateTimeOffset.UtcNow);
}
```

**ICommandArchiveStore Interface:**
```csharp
public interface ICommandArchiveStore
{
    Task WriteCommandAsync(
        string tenantId,
        string correlationId,
        ArchivedCommand command,
        CancellationToken cancellationToken = default);

    Task<ArchivedCommand?> ReadCommandAsync(
        string tenantId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
```

**Replayable Status Determination:**
```csharp
private static readonly HashSet<CommandStatus> ReplayableStatuses =
[
    CommandStatus.Rejected,
    CommandStatus.PublishFailed,
    CommandStatus.TimedOut,
];

private static bool IsReplayable(CommandStatus status) => ReplayableStatuses.Contains(status);
```

**409 ProblemDetails formats:**

For completed commands:
```json
{
  "type": "https://tools.ietf.org/html/rfc9457#section-3",
  "title": "Conflict",
  "status": 409,
  "detail": "Command 'abc-def-123' has already completed successfully. Replay is not permitted for completed commands. Replay is permitted only for commands with terminal failure status (Rejected, PublishFailed, TimedOut).",
  "instance": "/api/v1/commands/replay/abc-def-123",
  "correlationId": "request-correlation-id",
  "currentStatus": "Completed"
}
```

For in-flight commands:
```json
{
  "type": "https://tools.ietf.org/html/rfc9457#section-3",
  "title": "Conflict",
  "status": 409,
  "detail": "Command 'abc-def-123' is currently in-flight (status: Processing). Wait for processing to complete or time out before replaying. Replay is permitted only for commands with terminal failure status (Rejected, PublishFailed, TimedOut).",
  "instance": "/api/v1/commands/replay/abc-def-123",
  "correlationId": "request-correlation-id",
  "currentStatus": "Processing"
}
```

**202 Replay Response format:**
```json
{
  "correlationId": "abc-def-123",
  "isReplay": true,
  "previousStatus": "Rejected"
}
```

**Why replay resubmits through the full pipeline:**
The replayed command goes through the complete MediatR pipeline (logging, validation, authorization, handler). This ensures:
1. The replay is logged consistently (LoggingBehavior)
2. The command data is still structurally valid (ValidationBehavior)
3. The replaying user is authorized for the command's tenant/domain (AuthorizationBehavior)
4. Status tracking and archiving are handled uniformly (SubmitCommandHandler)

The SubmitCommandHandler will re-write the Received status and re-archive the command -- both are idempotent (overwriting with same data + fresh timestamp/TTL).

**Causation ID tracking:**
The epics specify "a new causation ID is generated to distinguish the replay from the original submission." At the API layer (Epic 2), causation ID is not yet a first-class field in `SubmitCommand`. The causation ID will be generated when the actor processes the command (Epic 3, Story 3.11). For Story 2.7, the replay's `isReplay: true` flag in the response and the status reset serve as the distinguishing markers. When the actor layer is implemented, it will generate a new causation ID for replayed commands (the correlation ID will be the link to the original, and the distinct causation ID will mark the replay chain).

### Technical Requirements

**Existing Types to Use:**
- `CommandStatus` from `Hexalith.EventStore.Contracts.Commands` -- 8-state enum
- `CommandStatusRecord` from `Hexalith.EventStore.Contracts.Commands` -- immutable status record
- `SubmitCommand` from `Hexalith.EventStore.Server.Pipeline.Commands` -- MediatR command
- `SubmitCommandResult` from same namespace -- result with CorrelationId
- `ICommandStatusStore` from `Hexalith.EventStore.Server.Commands` -- status read/write (Story 2.6)
- `InMemoryCommandStatusStore` from `Hexalith.EventStore.Testing.Fakes` -- test fake (Story 2.6)
- `CommandStatusOptions` from `Hexalith.EventStore.Server.Commands` -- TTL config (Story 2.6)
- `CorrelationIdMiddleware.HttpContextKey` (constant `"CorrelationId"`) -- access correlation ID
- `ProblemDetails` from `Microsoft.AspNetCore.Mvc` -- RFC 7807 response format
- `DaprClient` from `Dapr.Client` -- state store operations
- `TestJwtTokenGenerator` from integration tests -- generate JWT tokens with tenant claims
- `EventStoreActivitySources.CommandApi` -- ActivitySource for tracing

**New Types to Create:**
- `ArchivedCommand` -- record for storing original command data (Contracts package)
- `ICommandArchiveStore` -- abstraction interface for archive read/write (Server package)
- `DaprCommandArchiveStore` -- DAPR implementation with DaprClient (Server package)
- `InMemoryCommandArchiveStore` -- test fake in Testing package
- `ReplayController` -- POST replay endpoint controller (CommandApi)
- `ReplayCommandResponse` -- API response model (CommandApi)
- Optionally: `CommandConflictException` + `ConflictExceptionHandler` for 409 responses (CommandApi)

**NuGet Packages Already Available (no new packages needed):**
- `Dapr.Client` 1.16.1 -- `DaprClient.SaveStateAsync`, `DaprClient.GetStateAsync`
- `Dapr.AspNetCore` 1.16.1 -- `AddDaprClient()` extension
- `MediatR` 14.0.0 -- `IMediator.Send`, `IRequestHandler`
- `Microsoft.AspNetCore.Mvc.Testing` 10.0.0 -- WebApplicationFactory
- `Shouldly` 4.3.0 -- test assertions
- `NSubstitute` 5.3.0 -- mocking DaprClient for unit tests

### File Structure Requirements

**New files to create:**
```
src/Hexalith.EventStore.Contracts/
├── Commands/
│   └── ArchivedCommand.cs              # NEW: Record for storing original command data

src/Hexalith.EventStore.Server/
├── Commands/
│   ├── ICommandArchiveStore.cs         # NEW: Abstraction for command archive read/write
│   ├── DaprCommandArchiveStore.cs      # NEW: DAPR implementation with DaprClient
│   └── CommandArchiveConstants.cs      # NEW: Constants (key suffix = "command")

src/Hexalith.EventStore.CommandApi/
├── Controllers/
│   └── ReplayController.cs            # NEW: POST /api/v1/commands/replay/{correlationId}
├── Models/
│   └── ReplayCommandResponse.cs       # NEW: API response model (correlationId, isReplay, previousStatus)
├── ErrorHandling/
│   ├── CommandConflictException.cs     # NEW (optional): Typed exception for 409 responses
│   └── ConflictExceptionHandler.cs    # NEW (optional): IExceptionHandler -> 409 ProblemDetails

src/Hexalith.EventStore.Testing/
├── Fakes/
│   └── InMemoryCommandArchiveStore.cs  # NEW: Test fake with TTL simulation

tests/Hexalith.EventStore.Server.Tests/
├── Commands/
│   ├── DaprCommandArchiveStoreTests.cs # NEW: Unit tests for DAPR archive implementation
│   └── SubmitCommandHandlerArchiveTests.cs # NEW: Tests for archive write in handler

tests/Hexalith.EventStore.IntegrationTests/
├── CommandApi/
│   └── ReplayIntegrationTests.cs       # NEW: Integration tests for replay flow
```

**Existing files to modify:**
```
src/Hexalith.EventStore.Server/
├── Pipeline/
│   └── SubmitCommandHandler.cs         # MODIFY: Inject ICommandArchiveStore, archive command

src/Hexalith.EventStore.CommandApi/
├── Extensions/
│   └── ServiceCollectionExtensions.cs  # MODIFY: Register ICommandArchiveStore, optionally ConflictExceptionHandler
```

**Existing files to verify (no changes expected):**
```
src/Hexalith.EventStore.Contracts/
├── Commands/
│   ├── CommandStatus.cs                # VERIFY: Enum has all 8 states
│   └── CommandStatusRecord.cs          # VERIFY: Record with all fields

src/Hexalith.EventStore.Server/
├── Commands/
│   ├── ICommandStatusStore.cs          # VERIFY: Interface exists (Story 2.6)
│   ├── DaprCommandStatusStore.cs       # VERIFY: Implementation exists (Story 2.6)
│   └── CommandStatusOptions.cs         # VERIFY: TtlSeconds + StateStoreName config

src/Hexalith.EventStore.CommandApi/
├── Controllers/
│   ├── CommandsController.cs           # VERIFY: Location header format matches
│   └── CommandStatusController.cs      # VERIFY: Tenant-scoped query pattern (Story 2.6)
├── ErrorHandling/
│   ├── ValidationExceptionHandler.cs   # VERIFY: Still handles 400s
│   ├── AuthorizationExceptionHandler.cs # VERIFY: Still handles 403s (Story 2.5)
│   └── GlobalExceptionHandler.cs       # VERIFY: Still catch-all for 500s
```

### Testing Requirements

**Test Projects:**
- `tests/Hexalith.EventStore.Server.Tests/` -- Unit tests for DaprCommandArchiveStore and SubmitCommandHandler archive write
- `tests/Hexalith.EventStore.IntegrationTests/` -- Integration tests for full replay flow

**Test Patterns (established in Stories 1.6, 2.1-2.6):**
- Method naming: `{Method}_{Scenario}_{ExpectedResult}`
- Arrange/Act/Assert pattern
- Shouldly for assertions
- `WebApplicationFactory<CommandApiProgram>` for integration tests
- `ConfigureAwait(false)` on all async test methods
- `TestJwtTokenGenerator` for creating JWT tokens with specific claims
- Primary constructors for DI injection
- Feature folder organization in test projects mirroring source

**Unit Test Strategy for DaprCommandArchiveStore:**
Mock `DaprClient` using NSubstitute. Verify correct key format, TTL metadata, and advisory error handling.

```csharp
var daprClient = Substitute.For<DaprClient>();
var options = Options.Create(new CommandStatusOptions());
var logger = new TestLogger<DaprCommandArchiveStore>();
var store = new DaprCommandArchiveStore(daprClient, options, logger);
```

**Unit Test Strategy for ReplayController:**
Inject `InMemoryCommandArchiveStore` + `InMemoryCommandStatusStore` (or NSubstitute mocks). Pre-populate with archived commands at various statuses. Verify replay behavior for each status.

**Integration Test Strategy:**
Use `InMemoryCommandArchiveStore` and `InMemoryCommandStatusStore` registered in WebApplicationFactory. The flow:
1. Submit command via POST `/api/v1/commands` (archives and writes Received status)
2. Manually set status to a target state (Rejected, PublishFailed, etc.) via the in-memory store
3. Replay via POST `/api/v1/commands/replay/{correlationId}`
4. Verify response code and body

```csharp
// Submit command
var submitResponse = await client.PostAsJsonAsync("/api/v1/commands", request);
var submitResult = await submitResponse.Content.ReadFromJsonAsync<SubmitCommandResponse>();

// Set status to Rejected (simulate failure)
var archiveStore = factory.Services.GetRequiredService<ICommandArchiveStore>() as InMemoryCommandArchiveStore;
var statusStore = factory.Services.GetRequiredService<ICommandStatusStore>() as InMemoryCommandStatusStore;
await statusStore.WriteStatusAsync("test-tenant", submitResult!.CorrelationId,
    new CommandStatusRecord(CommandStatus.Rejected, DateTimeOffset.UtcNow, "agg-1",
        null, "OrderRejected", null, null),
    CancellationToken.None);

// Replay
var replayResponse = await client.PostAsync(
    $"/api/v1/commands/replay/{submitResult.CorrelationId}", null);
replayResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
var replayResult = await replayResponse.Content.ReadFromJsonAsync<ReplayCommandResponse>();
replayResult!.IsReplay.ShouldBeTrue();
replayResult.PreviousStatus.ShouldBe("Rejected");
```

**Minimum Tests (37):**

DaprCommandArchiveStore Unit Tests (6) -- in `DaprCommandArchiveStoreTests.cs`:
1. `WriteCommandAsync_ValidCommand_CallsSaveStateWithCorrectKey`
2. `WriteCommandAsync_IncludesTtlMetadata_Default86400Seconds`
3. `WriteCommandAsync_DaprClientThrows_LogsWarningAndDoesNotThrow`
4. `ReadCommandAsync_ExistingKey_ReturnsArchivedCommand`
5. `ReadCommandAsync_NonExistentKey_ReturnsNull`
6. `ReadCommandAsync_DaprClientThrows_LogsWarningAndReturnsNull`

SubmitCommandHandler Archive Tests (4) -- in `SubmitCommandHandlerArchiveTests.cs`:
7. `Handle_ValidCommand_WritesArchivedCommandToStore`
8. `Handle_ArchiveWriteFails_StillReturnsResult`
9. `Handle_ArchiveWriteFails_LogsWarning`
10. `Handle_ArchivedCommand_ContainsAllOriginalFields`

ReplayController Unit Tests (13) -- in `ReplayControllerTests.cs`:
11. `Replay_RejectedStatus_Returns202WithReplayResponse`
12. `Replay_PublishFailedStatus_Returns202`
13. `Replay_TimedOutStatus_Returns202`
14. `Replay_CompletedStatus_Returns409ProblemDetails`
15. `Replay_ProcessingStatus_Returns409ProblemDetails`
16. `Replay_ReceivedStatus_Returns409ProblemDetails`
17. `Replay_NonExistentCorrelationId_Returns404ProblemDetails`
18. `Replay_TenantMismatch_Returns404ProblemDetails`
19. `Replay_NoTenantClaims_Returns403ProblemDetails`
20. `Replay_ResetsStatusToReceived`
21. `Replay_ResubmitsThroughMediatRPipeline`
22. `Replay_PreservesOriginalCorrelationId`
23. `Replay_NullStatus_ArchiveExists_Returns409ProblemDetails`

Integration Tests (14) -- in `ReplayIntegrationTests.cs`:
24. `PostReplay_RejectedCommand_Returns202`
25. `PostReplay_PublishFailedCommand_Returns202`
26. `PostReplay_TimedOutCommand_Returns202`
27. `PostReplay_CompletedCommand_Returns409ProblemDetails`
28. `PostReplay_InFlightCommand_Returns409ProblemDetails`
29. `PostReplay_NonExistentCorrelationId_Returns404ProblemDetails`
30. `PostReplay_WrongTenant_Returns404ProblemDetails`
31. `PostReplay_NoAuthentication_Returns401`
32. `PostReplay_NoTenantClaims_Returns403`
33. `PostReplay_StatusResetToReceived`
34. `PostReplay_LocationHeader_PointsToStatusEndpoint`
35. `PostReplay_ResponseIncludesPreviousStatus`
36. `PostReplay_ReplayedThenFailedAgain_CanReplayAgain`
37. `PostReplay_ValidationRulesChanged_Returns400`

**Current test count:** ~308 (est. after Story 2.6). Story 2.7 adds 37 new tests, bringing estimated total to ~345.

### Previous Story Intelligence

**From Story 2.6 (Command Status Tracking & Query Endpoint):**
- `ICommandStatusStore` abstraction for status read/write with `DaprCommandStatusStore` and `InMemoryCommandStatusStore` implementations
- State store key: `{tenant}:{correlationId}:status` with 24-hour TTL
- `DaprClient.SaveStateAsync`/`GetStateAsync` patterns established
- `CommandStatusOptions` with `TtlSeconds` (86400) and `StateStoreName` ("statestore")
- `CommandStatusController` tenant-scoped queries: searches all authorized tenants, returns 404 for mismatch
- `SubmitCommandHandler` writes `Received` status before returning (advisory, rule #12)
- Advisory write pattern: try/catch, log Warning, continue
- First `AddDaprClient()` registration in Program.cs
- `InMemoryCommandStatusStore` with TTL simulation for testing

**From Story 2.5 (Endpoint Authorization & Command Rejection):**
- Two-level authorization: pre-pipeline tenant check (controller) + in-pipeline ABAC (AuthorizationBehavior)
- `eventstore:tenant` claims queried via `User.FindAll("eventstore:tenant")` with empty/whitespace filtering
- Case-insensitive claim comparison with `StringComparison.OrdinalIgnoreCase`
- `CommandAuthorizationException` + `AuthorizationExceptionHandler` -> 403 ProblemDetails
- `HttpContext.Items["RequestTenantId"]` stores tenant for error handlers

**From Story 2.4 (JWT Authentication & Claims Transformation):**
- `EventStoreClaimsTransformation` normalizes JWT claims to `eventstore:tenant`, `eventstore:domain`, `eventstore:permission`
- `TestJwtTokenGenerator` creates tokens with configurable tenant, domain, permission arrays
- Two-mode JWT: OIDC production / symmetric key development

**From Story 2.3 (MediatR Pipeline & Logging Behavior):**
- `EventStoreActivitySources.CommandApi` ActivitySource for OpenTelemetry
- Correlation ID access: `HttpContext.Items["CorrelationId"]`

**From Story 2.2 (Command Validation & RFC 7807 Error Responses):**
- `IExceptionHandler` pattern for ProblemDetails
- ProblemDetails always include `correlationId` extension
- `ValidateModelFilter` runs FluentValidation BEFORE controller action

**From Story 2.1 (CommandApi Host & Minimal Endpoint Scaffolding):**
- `SubmitCommandHandler` currently logs command and returns result (+ status write from 2.6)
- `CommandsController.Submit()` sets Location header to status endpoint URL
- Primary constructors for DI, records for immutable data

**Key Patterns (mandatory for all new code):**
- Primary constructors for DI: `public class Foo(IDep dep) : Base`
- Records for immutable data
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` on public methods (CA1062)
- Feature folder organization
- `namespace Hexalith.EventStore.{Project}.{Feature};`
- Registration via `Add*` extension methods

### Elicitation-Derived Hardening Notes

**H1 -- Archive and status TTL alignment:** Both the archive key (`{tenant}:{correlationId}:command`) and status key (`{tenant}:{correlationId}:status`) MUST use the same TTL. If the status expires but the archive doesn't (or vice versa), the replay endpoint will encounter inconsistent state. Using the same `CommandStatusOptions.TtlSeconds` for both ensures they expire together.

**H2 -- Replay of already-replayed commands:** A command that was replayed and then failed again (status transitions back to Rejected/PublishFailed/TimedOut) should be replayable again. There is no limit on replay attempts. Each replay resets the status to Received and resubmits.

**H3 -- Race condition: concurrent replays of the same command:** If two users simultaneously replay the same failed command, both will reset the status to Received and resubmit. The actor layer (Epic 3) will handle deduplication via idempotency check (Story 3.2). At the API layer (Story 2.7), concurrent replays are not harmful -- the status will be reset to Received, and the command will be submitted twice (the pipeline is idempotent).

**H4 -- Archive overwrite on replay resubmission:** When a replayed command flows through `SubmitCommandHandler`, the handler will re-archive the command (overwriting the existing archive with identical data + fresh TTL). This is intentional and beneficial -- it refreshes the TTL so the archive doesn't expire during extended retry scenarios.

**H5 -- Null status edge case (HARDENED):** If the status entry has expired (or was never written due to advisory failure) but the archive still exists, the replay MUST NOT proceed. Return `409 Conflict` with detail explaining status has expired and replayability cannot be determined. **Rationale:** A null status could mean the command completed successfully and the 24-hour TTL expired. Replaying a previously-Completed command violates AC #3 and could cause duplicate side effects. The safe default is to reject indeterminate states.

**H6 -- Replay response vs. submit response:** The replay endpoint returns `ReplayCommandResponse` (with `isReplay: true` and `previousStatus`) rather than `SubmitCommandResponse`. This clearly signals to clients that this is a replay, not a new submission. The Location header still points to the status endpoint for polling.

**H9 -- Orphan archive hardening:** If the archive write in `SubmitCommandHandler` fails (advisory, rule #12), the original command is silently lost for replay purposes. The command proceeds to processing but can never be replayed if it fails later. This is acceptable per rule #12 (advisory writes never block), but operators should monitor archive write failure rates. If archive write failures are persistent (e.g., state store misconfiguration), replay will be permanently unavailable. Log archive write failures at Warning level with sufficient context (correlationId, tenantId) for alerting.

**H8 -- Replay rate limiting (deferred to Story 2.9):** The replay endpoint MUST be covered by the per-tenant rate limiting introduced in Story 2.9. Until then, the endpoint has no rate limiting. This is acceptable for development but must be addressed before production. The replay endpoint is particularly sensitive because it re-executes the full command pipeline, making it an amplification vector if abused. Story 2.9 MUST apply rate limiting to both `/api/v1/commands` and `/api/v1/commands/replay/{correlationId}`.

**H7 -- SubmitCommandHandler receives replayed commands:** When a replay reaches `SubmitCommandHandler`, the handler doesn't know it's a replay -- it processes it identically to a new command (writes status, archives). This is by design. The ReplayController validated replayability before sending through the pipeline. The handler's idempotent operations (status overwrite, archive overwrite) are safe.

**H10 -- ArchivedCommand deserialization hardening:** When reading `ArchivedCommand` from the DAPR state store via `GetStateAsync<ArchivedCommand>`, deserialization could fail if the schema evolved between the original write and the replay read (e.g., a new required field was added to `ArchivedCommand`). Mitigations: (1) All `ArchivedCommand` fields should have sensible defaults or be nullable where appropriate. (2) `DaprCommandArchiveStore.ReadCommandAsync` should catch `JsonException` (or general deserialization exceptions), log at Warning level with correlationId, and return `null` -- which will surface as a 404 to the caller. (3) The `ArchivedCommand` record should be treated as a stable contract once Story 2.7 ships -- breaking changes to its schema require migration consideration.

### Project Structure Notes

**Alignment with Architecture:**
- `ICommandArchiveStore` and `DaprCommandArchiveStore` go in `Server/Commands/` -- parallel to `ICommandStatusStore`/`DaprCommandStatusStore`
- `ArchivedCommand` goes in `Contracts/Commands/` -- it's a shared data contract used by Server and Testing packages
- `ReplayController` goes in `CommandApi/Controllers/` alongside existing `CommandsController` and `CommandStatusController`
- `ReplayCommandResponse` goes in `CommandApi/Models/` alongside existing response types
- `InMemoryCommandArchiveStore` goes in `Testing/Fakes/` alongside `InMemoryCommandStatusStore`
- Registration in `AddCommandApi()` per enforcement rule #10

**Architecture doc reference:** The architecture specifies `ReplayController.cs` at `CommandApi/Controllers/ReplayController.cs` with route `POST /api/v1/commands/{id}/replay` (FR6). The epics use `/commands/replay/{correlationId}`. Follow the architecture doc's pattern: `POST /api/v1/commands/replay/{correlationId}`.

**Dependency Graph Relevant to This Story:**
```
CommandApi -> Server -> Contracts
CommandApi -> Dapr.AspNetCore (DaprClient via Story 2.6)
Server -> Dapr.Client (DaprClient for state store)
Server -> Contracts (ArchivedCommand, CommandStatusRecord)
Testing -> Server (ICommandArchiveStore interface)
Testing -> Contracts (ArchivedCommand)
Tests: IntegrationTests -> CommandApi (via WebApplicationFactory)
Tests: Server.Tests -> Server (unit testing DaprCommandArchiveStore)
```

**Architecture Reference -- Data Flow After Story 2.7:**
```
POST /api/v1/commands (original submission)
    |-> CorrelationIdMiddleware -> JWT Auth -> [Authorize] -> Tenant pre-check
    |-> MediatR: Logging -> Validation -> Authorization -> SubmitCommandHandler
    |     |-- Write Received status (D2, Story 2.6)
    |     |-- Archive original command (D2, Story 2.7)
    |     |-- Return SubmitCommandResult
    |-> 202 Accepted + Location header

POST /api/v1/commands/replay/{correlationId} (replay)
    |-> CorrelationIdMiddleware -> JWT Auth -> [Authorize]
    |-> ReplayController.Replay()
    |     |-- Validate tenant claims
    |     |-- Read archived command (tenant-scoped)
    |     |-- Read current status
    |     |-- Validate replayable (Rejected/PublishFailed/TimedOut; null -> 409)
    |     |-- Create SubmitCommand from archive
    |     |-- Send through MediatR pipeline (handler writes Received status)
    |-> 202 Accepted + Location header (isReplay: true)

GET /api/v1/commands/status/{correlationId} (unchanged from Story 2.6)
    |-> CommandStatusController -> tenant-scoped status query
    |-> 200 OK | 404 Not Found
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.7: Command Replay Endpoint]
- [Source: _bmad-output/planning-artifacts/architecture.md#D2: Command Status Storage]
- [Source: _bmad-output/planning-artifacts/architecture.md#Security-Critical Architectural Constraints - SEC-3]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines - Rules #5, #7, #9, #10, #12, #13]
- [Source: _bmad-output/planning-artifacts/architecture.md#Data Flow - Command submission and status paths]
- [Source: _bmad-output/planning-artifacts/architecture.md#DAPR State Store Keys - D2]
- [Source: _bmad-output/planning-artifacts/architecture.md#Process Patterns - Error Handling]
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure - CommandApi/Controllers/ReplayController.cs]
- [Source: _bmad-output/planning-artifacts/prd.md#FR6: Command replay via Command API]
- [Source: _bmad-output/implementation-artifacts/2-6-command-status-tracking-and-query-endpoint.md]
- [Source: _bmad-output/implementation-artifacts/2-5-endpoint-authorization-and-command-rejection.md]
- [Source: _bmad-output/implementation-artifacts/2-4-jwt-authentication-and-claims-transformation.md (referenced)]
- [Source: _bmad-output/implementation-artifacts/2-3-mediatr-pipeline-and-logging-behavior.md (referenced)]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
