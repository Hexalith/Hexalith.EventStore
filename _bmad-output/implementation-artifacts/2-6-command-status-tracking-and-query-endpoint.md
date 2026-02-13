# Story 2.6: Command Status Tracking & Query Endpoint

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an **API consumer**,
I want to query the processing status of a previously submitted command using its correlation ID via GET `/commands/status/{correlationId}`,
So that I can monitor command lifecycle progression (FR5).

## Acceptance Criteria

1. **Status query returns current lifecycle state** - Given a command was previously submitted and a correlation ID was returned, When I GET `/api/v1/commands/status/{correlationId}`, Then the response is `200 OK` with a JSON body containing the current command status (Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, TimedOut) as a `CommandStatusRecord` including timestamp, aggregateId, and terminal-state-specific fields (eventCount, rejectionEventType, failureReason, timeoutDuration).

2. **Status read from dedicated state store key** - The status is read from a dedicated DAPR state store key `{tenant}:{correlationId}:status` (D2), queried via `DaprClient.GetStateAsync`. The status reader does NOT activate an actor -- it reads directly from the state store.

3. **24-hour default TTL on status entries** - Status entries are written with a 24-hour TTL via DAPR state store `ttlInSeconds` metadata (D2). After expiry, the status is no longer queryable and returns 404.

4. **Tenant-scoped status queries (SEC-3)** - The JWT tenant must match the command's tenant embedded in the state store key. The status query endpoint extracts the authenticated user's `eventstore:tenant` claims and verifies the queried correlation ID's tenant matches. Querying a status belonging to a different tenant returns `404 Not Found` (not 403, to avoid information leakage about which correlation IDs exist for other tenants).

5. **404 for non-existent or expired correlation ID** - Querying a non-existent or expired correlation ID returns `404 Not Found` as RFC 7807 ProblemDetails with `correlationId` extension.

6. **"Received" status written at API layer** - When a command is submitted via POST `/api/v1/commands`, the SubmitCommandHandler writes a `Received` status to the state store BEFORE returning the correlation ID. This ensures status is queryable even if actor activation fails. Status writes are advisory -- failure to write status MUST NOT block the command processing pipeline (enforcement rule #12).

7. **Status endpoint requires authentication** - The GET status endpoint requires a valid JWT token (`[Authorize]` attribute). Unauthenticated requests return `401 Unauthorized`.

## Prerequisites

**BLOCKING: Stories 2.4 (JWT Authentication) and 2.5 (Endpoint Authorization) MUST be complete before starting Story 2.6.** Story 2.6 depends on:
- `[Authorize]` attribute enforcement on controllers (Story 2.4)
- `EventStoreClaimsTransformation` providing `eventstore:tenant` claims (Story 2.4)
- `TestJwtTokenGenerator` for integration tests (Story 2.4)
- Established ProblemDetails error handling patterns (Stories 2.1-2.2)
- MediatR pipeline with LoggingBehavior, ValidationBehavior, AuthorizationBehavior (Stories 2.3, 2.5)

**Before beginning any Task below, verify:** Run existing tests to confirm Stories 2.4-2.5 artifacts are in place. If `DaprClient` is not registered or `EventStoreClaimsTransformation` does not exist, prerequisites are not met -- do NOT proceed.

## Tasks / Subtasks

- [x] Task 0: Verify prerequisites and DAPR state store availability (BLOCKING)
  - [x] 0.1 Confirm Stories 2.4-2.5 artifacts are in place (`[Authorize]`, `EventStoreClaimsTransformation`, `TestJwtTokenGenerator`, `AuthorizationBehavior`)
  - [x] 0.2 Run all existing tests -- they must pass before proceeding
  - [x] 0.3 Verify `Dapr.Client` package is referenced in Server project and `Dapr.AspNetCore` in CommandApi project

- [x] Task 1: Create ICommandStatusStore abstraction and DAPR implementation (AC: #2, #3, #6)
  - [x] 1.1 Create `ICommandStatusStore` interface in `Server/Commands/` with methods: `WriteStatusAsync(string tenantId, string correlationId, CommandStatusRecord status, CancellationToken ct)` and `ReadStatusAsync(string tenantId, string correlationId, CancellationToken ct)` returning `CommandStatusRecord?`
  - [x] 1.2 Create `DaprCommandStatusStore` in `Server/Commands/` implementing `ICommandStatusStore` using `DaprClient.SaveStateAsync` and `DaprClient.GetStateAsync`
  - [x] 1.3 State store key format: `{tenantId}:{correlationId}:status` per D2. Use a constant for the state store name (e.g., `CommandStatusConstants.StateStoreName = "statestore"`)
  - [x] 1.4 Write operations include TTL metadata: `new Dictionary<string, string> { { "ttlInSeconds", ttlSeconds.ToString() } }` with default 24 hours (86400 seconds), configurable via `CommandStatusOptions.TtlSeconds`
  - [x] 1.5 Read operations: `DaprClient.GetStateAsync<CommandStatusRecord>(storeName, key)` returns `null` for non-existent/expired keys
  - [x] 1.6 All DaprClient calls wrapped with `ConfigureAwait(false)` and advisory error handling: catch exceptions, log at Warning level, and return gracefully (enforcement rule #12 -- status writes must never block pipeline)
  - [x] 1.7 Create `CommandStatusOptions` record in `Server/Commands/` with `TtlSeconds` (default 86400) and `StateStoreName` (default "statestore")

- [x] Task 2: Create InMemoryCommandStatusStore for testing (AC: #2)
  - [x] 2.1 Create `InMemoryCommandStatusStore` in `Testing/Fakes/` implementing `ICommandStatusStore`
  - [x] 2.2 Use `ConcurrentDictionary<string, (CommandStatusRecord Record, DateTimeOffset Expiry)>` for storage with key = `{tenantId}:{correlationId}:status`
  - [x] 2.3 Implement TTL expiry simulation: `ReadStatusAsync` returns null if entry has expired
  - [x] 2.4 Add helper methods for test assertions: `GetAllStatuses()`, `GetStatusCount()`, `Clear()`

- [x] Task 3: Modify SubmitCommandHandler to write "Received" status (AC: #6)
  - [x] 3.1 Inject `ICommandStatusStore` into `SubmitCommandHandler` via primary constructor
  - [x] 3.2 After creating the `SubmitCommandResult`, call `_statusStore.WriteStatusAsync(command.Tenant, command.CorrelationId, new CommandStatusRecord(CommandStatus.Received, DateTimeOffset.UtcNow, command.AggregateId), cancellationToken)` BEFORE returning the result
  - [x] 3.3 Wrap the status write in try/catch: log at Warning level if write fails but DO NOT throw -- return the SubmitCommandResult regardless (enforcement rule #12: advisory status writes never block pipeline)
  - [x] 3.4 Log successful status write at Debug level: correlationId, tenantId, status=Received

- [x] Task 4: Create CommandStatusController with GET endpoint (AC: #1, #4, #5, #7)
  - [x] 4.1 Create `CommandStatusController` in `CommandApi/Controllers/` with route `[Route("api/v1/commands/status")]` and `[Authorize]` attribute
  - [x] 4.2 Create `GET /{correlationId}` action that accepts `correlationId` as a route parameter (GUID format validation via `[RegularExpression]` or FluentValidation)
  - [x] 4.3 Extract authenticated user's `eventstore:tenant` claims from `User.FindAll("eventstore:tenant")`
  - [x] 4.4 Call `_statusStore.ReadStatusAsync(tenantId, correlationId, cancellationToken)` for each authorized tenant until a status is found (the command could be under any authorized tenant)
  - [x] 4.5 If status found: return `200 OK` with `CommandStatusResponse` containing status, timestamp, aggregateId, and terminal-state fields
  - [x] 4.6 If no status found across all authorized tenants: return `404 Not Found` as ProblemDetails with `correlationId` extension. Detail: `"No command status found for correlation ID '{correlationId}'."`
  - [x] 4.7 If user has zero `eventstore:tenant` claims: return `403 Forbidden` as ProblemDetails. Detail: `"No tenant authorization claims found. Access denied."`
  - [x] 4.8 Store correlationId and tenantId in HttpContext.Items for error handler access
  - [x] 4.9 Add `[ProducesResponseType]` attributes for OpenAPI documentation: 200, 401, 403, 404

- [x] Task 5: Create CommandStatusResponse model (AC: #1)
  - [x] 5.1 Create `CommandStatusResponse` record in `CommandApi/Models/` with: `Status` (string -- enum name), `StatusCode` (int -- enum value), `Timestamp` (DateTimeOffset), `AggregateId` (string?), `EventCount` (int?), `RejectionEventType` (string?), `FailureReason` (string?), `TimeoutDuration` (string? -- ISO 8601 duration format)
  - [x] 5.2 Create a static factory method `FromRecord(CommandStatusRecord record)` for mapping from the domain record

- [x] Task 6: Register ICommandStatusStore in DI (AC: all)
  - [x] 6.1 In `ServiceCollectionExtensions.AddCommandApi()`, register `DaprCommandStatusStore` as `ICommandStatusStore` (singleton, since DaprClient is thread-safe)
  - [x] 6.2 Register `CommandStatusOptions` from configuration section "EventStore:CommandStatus"
  - [x] 6.3 Ensure `DaprClient` is registered via `builder.Services.AddDaprClient()` in Program.cs (first DAPR client registration in the project)
  - [x] 6.4 In integration tests, override registration with `InMemoryCommandStatusStore` via WebApplicationFactory

- [x] Task 7: Write unit tests for DaprCommandStatusStore (AC: #2, #3)
  - [x] 7.1 `WriteStatusAsync_ValidStatus_CallsSaveStateWithCorrectKey` -- verify key format `{tenant}:{correlationId}:status`
  - [x] 7.2 `WriteStatusAsync_IncludesTtlMetadata_Default86400Seconds` -- verify TTL metadata passed to SaveStateAsync
  - [x] 7.3 `WriteStatusAsync_DaprClientThrows_LogsWarningAndDoesNotThrow` -- verify advisory write behavior (rule #12)
  - [x] 7.4 `ReadStatusAsync_ExistingKey_ReturnsRecord` -- verify successful read
  - [x] 7.5 `ReadStatusAsync_NonExistentKey_ReturnsNull` -- verify null for missing entry
  - [x] 7.6 `ReadStatusAsync_DaprClientThrows_LogsWarningAndReturnsNull` -- verify graceful failure on read

- [x] Task 8: Write unit tests for SubmitCommandHandler status write (AC: #6)
  - [x] 8.1 `Handle_ValidCommand_WritesReceivedStatusToStore` -- verify status written with Received, correct tenant, correlationId
  - [x] 8.2 `Handle_StatusWriteFails_StillReturnsResult` -- verify handler doesn't fail if status write throws (rule #12)
  - [x] 8.3 `Handle_StatusWriteFails_LogsWarning` -- verify warning logged on status write failure

- [x] Task 9: Write unit tests for CommandStatusController (AC: #1, #4, #5, #7)
  - [x] 9.1 `GetStatus_ExistingStatus_Returns200WithRecord` -- verify successful status retrieval
  - [x] 9.2 `GetStatus_NonExistentCorrelationId_Returns404ProblemDetails` -- verify 404 response
  - [x] 9.3 `GetStatus_TenantMismatch_Returns404ProblemDetails` -- verify tenant-scoped (SEC-3), returns 404 not 403
  - [x] 9.4 `GetStatus_NoTenantClaims_Returns403ProblemDetails` -- verify 403 when no tenant claims
  - [x] 9.5 `GetStatus_MultipleTenantClaims_TriesAllTenants` -- verify searches across all authorized tenants
  - [x] 9.6 `GetStatus_CompletedStatus_IncludesEventCount` -- verify terminal-state metadata in response
  - [x] 9.7 `GetStatus_RejectedStatus_IncludesRejectionEventType` -- verify rejection metadata
  - [x] 9.8 `GetStatus_InvalidCorrelationIdFormat_Returns400` -- verify format validation

- [x] Task 10: Write integration tests for command status flow (AC: #1, #2, #4, #5, #6, #7)
  - [x] 10.1 `PostCommands_ThenGetStatus_Returns200WithReceivedStatus` -- submit command, query status, verify Received
  - [x] 10.2 `GetStatus_NonExistentCorrelationId_Returns404ProblemDetails` -- query unknown ID returns 404
  - [x] 10.3 `GetStatus_WrongTenant_Returns404ProblemDetails` -- query with JWT for tenant A, status belongs to tenant B -> 404 (SEC-3)
  - [x] 10.4 `GetStatus_NoAuthentication_Returns401` -- no JWT token returns 401
  - [x] 10.5 `GetStatus_ValidTenant_Returns200` -- query with matching tenant JWT returns 200
  - [x] 10.6 `GetStatus_NoTenantClaims_Returns403` -- JWT with no tenant claims returns 403
  - [x] 10.7 `PostCommands_StatusWriteIncludesAggregateId` -- verify Received status includes aggregateId from command
  - [x] 10.8 `GetStatus_ResponseIncludesCorrelationIdInProblemDetails` -- verify 404 ProblemDetails has correlationId extension
  - [x] 10.9 `GetStatus_ExpiredCorrelationId_Returns404` -- verify TTL expiry behavior (InMemory simulation)
  - [x] 10.10 `PostCommands_StatusLocationHeader_MatchesGetEndpoint` -- verify Location header from POST matches the GET endpoint path

- [x] Task 11: Update existing tests if needed (AC: all)
  - [x] 11.1 Update `SubmitCommandHandler` unit tests to provide `ICommandStatusStore` mock (existing tests may break without it)
  - [x] 11.2 Update WebApplicationFactory configuration to register `InMemoryCommandStatusStore`
  - [x] 11.3 Verify all existing tests still pass after adding DaprClient and status store dependencies

## Dev Notes

### Architecture Compliance

**D2: Command Status Storage -- Dedicated State Store Key:**
- Key pattern: `{tenant}:{correlationId}:status`
- Written at API layer (`SubmitCommandHandler`) before actor invocation
- Received status written BEFORE actor activation -- ensures queryable even if actor fails
- Query model: `GET /api/v1/commands/status/{correlationId}` reads directly from state store -- no actor activation required
- TTL: Default 24-hour (`86400` seconds) via DAPR state store `ttlInSeconds` metadata
- Terminal statuses: `Completed` (eventCount), `Rejected` (rejectionEventType), `PublishFailed` (failureReason), `TimedOut` (timeoutDuration)

**SEC-3: Command status queries are tenant-scoped:**
- JWT tenant must match the status entry's tenant
- Returns `404 Not Found` for tenant mismatches (not 403) to avoid information leakage
- The state store key itself includes the tenant, so a tenant A user can't retrieve tenant B's status even by guessing correlation IDs

**Enforcement Rules to Follow:**
- Rule #5: Never log event payload data -- only envelope metadata (SEC-5, NFR12)
- Rule #7: ProblemDetails for all API error responses -- 404 must be ProblemDetails
- Rule #9: correlationId in every structured log entry and OpenTelemetry activity
- Rule #10: Register services via `Add*` extension methods -- never inline in Program.cs
- Rule #12: **CRITICAL** -- Command status writes are advisory. Failure to write/update status MUST NEVER block or fail the command processing pipeline. Status is ephemeral metadata, not a source of truth
- Rule #13: No stack traces in production error responses

**First DAPR State Store Integration:**
Story 2.6 is the **first story to introduce DaprClient for state store operations**. This establishes patterns that all subsequent stories (3.x event persistence, etc.) will follow. Design decisions here are precedent-setting.

### Critical Design Decisions

**What Already Exists (from Stories 2.1-2.5):**
- `CommandsController` with `[Authorize]` and POST `/api/v1/commands` -- returns 202 with Location header pointing to `/api/v1/commands/status/{correlationId}`
- `CommandStatus` enum (8 states) and `CommandStatusRecord` (immutable record with status metadata) in Contracts package
- `SubmitCommandHandler` -- MediatR handler that returns `SubmitCommandResult` with correlation ID (currently stub, no status write)
- MediatR pipeline: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> SubmitCommandHandler
- `CorrelationIdMiddleware` generates/propagates correlation IDs via `HttpContext.Items["CorrelationId"]`
- ProblemDetails error handling: `ValidationExceptionHandler` (400), `AuthorizationExceptionHandler` (403), `GlobalExceptionHandler` (500)
- `AggregateIdentity` with derived keys for state store, but no command status key helper (status key uses different pattern)
- `TestJwtTokenGenerator` for integration tests with configurable claims
- DAPR packages referenced (`Dapr.Client` 1.16.1, `Dapr.AspNetCore` 1.16.1) but no DaprClient usage yet
- `InMemoryStateManager` (IActorStateManager fake) in Testing package -- for actor state, NOT command status

**What Story 2.6 Adds:**
1. **`ICommandStatusStore`** -- abstraction for command status CRUD (read/write to state store)
2. **`DaprCommandStatusStore`** -- DAPR implementation using `DaprClient.SaveStateAsync`/`GetStateAsync` with TTL metadata
3. **`InMemoryCommandStatusStore`** -- test fake for integration tests (simulates TTL expiry)
4. **`CommandStatusOptions`** -- configuration for TTL and state store name
5. **`CommandStatusController`** -- GET `/api/v1/commands/status/{correlationId}` endpoint with tenant-scoped queries
6. **`CommandStatusResponse`** -- API response model mapping from `CommandStatusRecord`
7. **Modified `SubmitCommandHandler`** -- writes "Received" status before returning
8. **`DaprClient` registration** -- first `AddDaprClient()` call in the project (Program.cs)

**Status Query Flow:**
```
GET /api/v1/commands/status/{correlationId}
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
CommandStatusController.GetStatus()
    |-- Extract eventstore:tenant claims from User
    |-- If no tenant claims -> 403 ProblemDetails
    |-- For each authorized tenant:
    |     Call _statusStore.ReadStatusAsync(tenant, correlationId)
    |     If found -> return 200 OK with CommandStatusResponse
    |-- If not found in any tenant -> 404 ProblemDetails
```

**Status Write Flow (in SubmitCommandHandler):**
```
SubmitCommandHandler.Handle(SubmitCommand)
    |-- Create SubmitCommandResult with correlationId
    |-- TRY: Write Received status to state store
    |     Key: {tenant}:{correlationId}:status
    |     Value: CommandStatusRecord(Received, UtcNow, aggregateId)
    |     Metadata: { "ttlInSeconds": "86400" }
    |-- CATCH: Log Warning, continue (rule #12)
    |-- Return SubmitCommandResult
```

**ICommandStatusStore Interface Design:**
```csharp
public interface ICommandStatusStore
{
    Task WriteStatusAsync(
        string tenantId,
        string correlationId,
        CommandStatusRecord status,
        CancellationToken cancellationToken = default);

    Task<CommandStatusRecord?> ReadStatusAsync(
        string tenantId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
```

**DaprClient Registration (first in project):**
```csharp
// In Program.cs -- FIRST DaprClient registration
builder.Services.AddDaprClient();
```

This must come BEFORE `AddCommandApi()` so that `DaprClient` is available for injection.

**CommandStatusResponse Model:**
```json
{
  "status": "Received",
  "statusCode": 0,
  "timestamp": "2026-02-13T10:30:00Z",
  "aggregateId": "order-123",
  "eventCount": null,
  "rejectionEventType": null,
  "failureReason": null,
  "timeoutDuration": null
}
```

For terminal states, additional fields are populated:
- Completed: `"eventCount": 3`
- Rejected: `"rejectionEventType": "OrderRejected"`
- PublishFailed: `"failureReason": "Pub/sub unavailable after retry exhaustion"`
- TimedOut: `"timeoutDuration": "PT30S"` (ISO 8601)

**404 ProblemDetails format:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9457#section-3",
  "title": "Not Found",
  "status": 404,
  "detail": "No command status found for correlation ID 'abc-def-123'.",
  "instance": "/api/v1/commands/status/abc-def-123",
  "correlationId": "request-correlation-id"
}
```

**Why 404 for tenant mismatch (not 403):**
SEC-3 requires tenant-scoped queries, but returning 403 for a valid correlation ID belonging to a different tenant leaks information about which correlation IDs exist. By returning 404 for both "doesn't exist" and "wrong tenant", we prevent cross-tenant information leakage while still enforcing isolation.

**Advisory Status Write Pattern (Enforcement Rule #12):**
```csharp
try
{
    await _statusStore.WriteStatusAsync(
        command.Tenant,
        command.CorrelationId,
        new CommandStatusRecord(CommandStatus.Received, DateTimeOffset.UtcNow, command.AggregateId),
        cancellationToken).ConfigureAwait(false);
}
catch (Exception ex)
{
    _logger.LogWarning(ex,
        "Failed to write command status for {CorrelationId}. Status tracking may be incomplete. Command processing continues.",
        command.CorrelationId);
}
```

### Technical Requirements

**Existing Types to Use:**
- `CommandStatus` from `Hexalith.EventStore.Contracts.Commands` -- 8-state enum
- `CommandStatusRecord` from `Hexalith.EventStore.Contracts.Commands` -- immutable status record
- `SubmitCommand` from `Hexalith.EventStore.Server.Pipeline.Commands` -- MediatR command with Tenant, Domain, CorrelationId, AggregateId
- `SubmitCommandResult` from same namespace -- result with CorrelationId
- `CorrelationIdMiddleware.HttpContextKey` (constant `"CorrelationId"`) -- access correlation ID
- `ProblemDetails` from `Microsoft.AspNetCore.Mvc` -- RFC 7807 response format
- `DaprClient` from `Dapr.Client` -- state store operations (SaveStateAsync, GetStateAsync)
- `TestJwtTokenGenerator` from integration tests -- generate JWT tokens with tenant claims
- `EventStoreActivitySources.CommandApi` -- ActivitySource for tracing

**New Types to Create:**
- `ICommandStatusStore` -- abstraction interface for status read/write
- `DaprCommandStatusStore` -- DAPR implementation with DaprClient
- `InMemoryCommandStatusStore` -- test fake in Testing package
- `CommandStatusOptions` -- configuration record (TTL, store name)
- `CommandStatusController` -- GET endpoint controller
- `CommandStatusResponse` -- API response model
- `CommandStatusConstants` -- constants (state store name, key pattern)

**NuGet Packages Already Available (no new packages needed):**
- `Dapr.Client` 1.16.1 -- `DaprClient.SaveStateAsync`, `DaprClient.GetStateAsync`
- `Dapr.AspNetCore` 1.16.1 -- `AddDaprClient()` extension
- `MediatR` 14.0.0 -- `IRequestHandler<TRequest, TResponse>`
- `FluentValidation` 12.1.1 -- request validation
- `Microsoft.AspNetCore.Mvc.Testing` 10.0.0 -- WebApplicationFactory
- `Shouldly` 4.3.0 -- test assertions
- `NSubstitute` 5.3.0 -- mocking DaprClient for unit tests

### File Structure Requirements

**New files to create:**
```
src/Hexalith.EventStore.Server/
├── Commands/
│   ├── ICommandStatusStore.cs              # NEW: Abstraction for status read/write
│   ├── DaprCommandStatusStore.cs           # NEW: DAPR implementation with DaprClient
│   ├── CommandStatusOptions.cs             # NEW: Configuration (TTL, store name)
│   └── CommandStatusConstants.cs           # NEW: Constants (store name, key pattern)

src/Hexalith.EventStore.CommandApi/
├── Controllers/
│   └── CommandStatusController.cs          # NEW: GET /api/v1/commands/status/{correlationId}
├── Models/
│   └── CommandStatusResponse.cs            # NEW: API response model

src/Hexalith.EventStore.Testing/
├── Fakes/
│   └── InMemoryCommandStatusStore.cs       # NEW: Test fake with TTL simulation

tests/Hexalith.EventStore.Server.Tests/
├── Commands/
│   ├── DaprCommandStatusStoreTests.cs      # NEW: Unit tests for DAPR implementation
│   └── SubmitCommandHandlerStatusTests.cs  # NEW: Tests for status write in handler

tests/Hexalith.EventStore.IntegrationTests/
├── CommandApi/
│   └── CommandStatusIntegrationTests.cs    # NEW: Integration tests for status flow
```

**Existing files to modify:**
```
src/Hexalith.EventStore.Server/
├── Pipeline/
│   └── SubmitCommandHandler.cs             # MODIFY: Inject ICommandStatusStore, write Received status

src/Hexalith.EventStore.CommandApi/
├── Extensions/
│   └── ServiceCollectionExtensions.cs      # MODIFY: Register ICommandStatusStore, CommandStatusOptions
├── Program.cs                              # MODIFY: Add builder.Services.AddDaprClient() (first DAPR registration)
```

**Existing files to verify (no changes expected):**
```
src/Hexalith.EventStore.Contracts/
├── Commands/
│   ├── CommandStatus.cs                    # VERIFY: Enum exists with 8 states
│   └── CommandStatusRecord.cs              # VERIFY: Record exists with all fields

src/Hexalith.EventStore.CommandApi/
├── Controllers/
│   └── CommandsController.cs               # VERIFY: Location header already points to /api/v1/commands/status/{correlationId}
├── Middleware/
│   └── CorrelationIdMiddleware.cs          # VERIFY: Provides correlation ID
├── ErrorHandling/
│   ├── ValidationExceptionHandler.cs       # VERIFY: Still handles 400s
│   ├── AuthorizationExceptionHandler.cs    # VERIFY: Still handles 403s (from 2.5)
│   └── GlobalExceptionHandler.cs           # VERIFY: Still catch-all for 500s
```

### Testing Requirements

**Test Projects:**
- `tests/Hexalith.EventStore.Server.Tests/` -- Unit tests for DaprCommandStatusStore and SubmitCommandHandler status write
- `tests/Hexalith.EventStore.IntegrationTests/` -- Integration tests for full status query flow

**Test Patterns (established in Stories 1.6, 2.1-2.5):**
- Method naming: `{Method}_{Scenario}_{ExpectedResult}`
- Arrange/Act/Assert pattern
- Shouldly for assertions
- `WebApplicationFactory<CommandApiProgram>` for integration tests
- `ConfigureAwait(false)` on all async test methods
- `TestJwtTokenGenerator` for creating JWT tokens with specific claims
- Primary constructors for DI injection
- Feature folder organization in test projects mirroring source

**Unit Test Strategy for DaprCommandStatusStore:**
Mock `DaprClient` using NSubstitute. Verify correct key format, TTL metadata, and advisory error handling.

```csharp
// Example test setup:
var daprClient = Substitute.For<DaprClient>();
var options = Options.Create(new CommandStatusOptions());
var logger = new TestLogger<DaprCommandStatusStore>();
var store = new DaprCommandStatusStore(daprClient, options, logger);
```

**Unit Test Strategy for SubmitCommandHandler:**
Inject `InMemoryCommandStatusStore` (or NSubstitute mock of `ICommandStatusStore`). Verify Received status is written with correct fields. Verify handler doesn't fail when status write throws.

**Integration Test Strategy:**
Use `InMemoryCommandStatusStore` registered in WebApplicationFactory. Submit commands via POST, then query status via GET. Verify tenant scoping by using different JWT tokens.

```csharp
// Submit command
var submitResponse = await client.PostAsJsonAsync("/api/v1/commands", request);
submitResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
var submitResult = await submitResponse.Content.ReadFromJsonAsync<SubmitCommandResponse>();

// Query status
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var statusResponse = await client.GetAsync($"/api/v1/commands/status/{submitResult!.CorrelationId}");
statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
var status = await statusResponse.Content.ReadFromJsonAsync<CommandStatusResponse>();
status!.Status.ShouldBe("Received");
```

**Minimum Tests (25):**

DaprCommandStatusStore Unit Tests (6) -- in `DaprCommandStatusStoreTests.cs`:
1. `WriteStatusAsync_ValidStatus_CallsSaveStateWithCorrectKey`
2. `WriteStatusAsync_IncludesTtlMetadata_Default86400Seconds`
3. `WriteStatusAsync_DaprClientThrows_LogsWarningAndDoesNotThrow`
4. `ReadStatusAsync_ExistingKey_ReturnsRecord`
5. `ReadStatusAsync_NonExistentKey_ReturnsNull`
6. `ReadStatusAsync_DaprClientThrows_LogsWarningAndReturnsNull`

SubmitCommandHandler Status Tests (3) -- in `SubmitCommandHandlerStatusTests.cs`:
7. `Handle_ValidCommand_WritesReceivedStatusToStore`
8. `Handle_StatusWriteFails_StillReturnsResult`
9. `Handle_StatusWriteFails_LogsWarning`

CommandStatusController Unit Tests (8) -- in `CommandStatusControllerTests.cs` (or inline with integration):
10. `GetStatus_ExistingStatus_Returns200WithRecord`
11. `GetStatus_NonExistentCorrelationId_Returns404ProblemDetails`
12. `GetStatus_TenantMismatch_Returns404ProblemDetails`
13. `GetStatus_NoTenantClaims_Returns403ProblemDetails`
14. `GetStatus_MultipleTenantClaims_TriesAllTenants`
15. `GetStatus_CompletedStatus_IncludesEventCount`
16. `GetStatus_RejectedStatus_IncludesRejectionEventType`
17. `GetStatus_InvalidCorrelationIdFormat_Returns400`

Integration Tests (8) -- in `CommandStatusIntegrationTests.cs`:
18. `PostCommands_ThenGetStatus_Returns200WithReceivedStatus`
19. `GetStatus_NonExistentCorrelationId_Returns404ProblemDetails`
20. `GetStatus_WrongTenant_Returns404ProblemDetails`
21. `GetStatus_NoAuthentication_Returns401`
22. `GetStatus_ValidTenant_Returns200`
23. `GetStatus_NoTenantClaims_Returns403`
24. `GetStatus_ResponseIncludesCorrelationIdInProblemDetails`
25. `PostCommands_StatusLocationHeader_MatchesGetEndpoint`

**Current test count:** ~283 (est. after Story 2.5). Story 2.6 adds 25 new tests, bringing estimated total to ~308.

### Previous Story Intelligence

**From Story 2.5 (Endpoint Authorization & Command Rejection):**
- Two-level authorization pattern: pre-pipeline tenant check (controller) + in-pipeline ABAC (AuthorizationBehavior)
- `eventstore:tenant` claims queried via `User.FindAll("eventstore:tenant")`
- Case-insensitive claim comparison with `StringComparison.OrdinalIgnoreCase`
- Empty/whitespace claim filtering with `.Where(v => !string.IsNullOrWhiteSpace(v))`
- `CommandAuthorizationException` + `AuthorizationExceptionHandler` -> 403 ProblemDetails
- Tenant and domain authorization are independent (not paired)
- `HttpContext.Items["RequestTenantId"]` stores tenant for error handlers
- Key pattern: defensive null guards on HttpContext/User + IsAuthenticated check

**From Story 2.4 (JWT Authentication & Claims Transformation):**
- `EventStoreClaimsTransformation` normalizes JWT claims to `eventstore:tenant`, `eventstore:domain`, `eventstore:permission`
- `JwtBearerEvents.OnChallenge` returns 401 ProblemDetails with correlationId
- `TestJwtTokenGenerator` creates tokens with configurable tenant, domain, permission arrays
- Two-mode JWT: OIDC production / symmetric key development
- All existing integration tests use valid JWT tokens

**From Story 2.3 (MediatR Pipeline & Logging Behavior):**
- `EventStoreActivitySources.CommandApi` ActivitySource for OpenTelemetry
- `IHttpContextAccessor` registered via `services.AddHttpContextAccessor()`
- Correlation ID access: `HttpContext.Items["CorrelationId"]`

**From Story 2.2 (Command Validation & RFC 7807 Error Responses):**
- `IExceptionHandler` pattern for ProblemDetails
- ProblemDetails always include `correlationId` extension
- Content type: `application/problem+json`
- `ValidateModelFilter` runs FluentValidation BEFORE controller action

**From Story 2.1 (CommandApi Host & Minimal Endpoint Scaffolding):**
- `SubmitCommandHandler` is a stub -- logs command and returns result
- `CommandsController.Submit()` sets Location header to status endpoint URL
- Primary constructors for DI, records for immutable data
- `ConfigureAwait(false)` on all async calls

**Key Patterns (mandatory for all new code):**
- Primary constructors for DI: `public class Foo(IDep dep) : Base`
- Records for immutable data
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` on public methods (CA1062)
- Feature folder organization
- `namespace Hexalith.EventStore.{Project}.{Feature};`
- Registration via `Add*` extension methods

### Elicitation-Derived Hardening Notes

**H1 -- DaprClient availability during startup:** DaprClient requires the DAPR sidecar to be running. In integration tests using WebApplicationFactory, the sidecar is NOT available. The `InMemoryCommandStatusStore` must be used for integration tests. Unit tests should mock DaprClient via NSubstitute. NEVER make test success depend on a running DAPR sidecar.

**H2 -- TTL behavior varies by state store backend:** DAPR's `ttlInSeconds` metadata is supported by most state stores but behavior varies (Redis uses native TTL, PostgreSQL uses a cleanup process). The architecture should not depend on exact-second TTL precision. Tests should validate "expired = not found" semantics rather than exact timing.

**H3 -- Tenant in state store key prevents cross-tenant access:** The key pattern `{tenant}:{correlationId}:status` means a tenant A user cannot accidentally or maliciously read tenant B's status even if they know the correlation ID. The state store key itself is the isolation boundary. The controller's tenant check is defense-in-depth.

**H4 -- Status write timing in SubmitCommandHandler:** The Received status MUST be written BEFORE the handler returns (and thus before the 202 response reaches the client). This way, if the client immediately polls the status endpoint using the Location header, the Received status will already be available. However, since this is advisory (rule #12), a race condition where status write is slightly delayed is acceptable -- the client will get 404 and retry.

**H5 -- GET endpoint path consistency:** The `CommandsController` Location header already uses `/api/v1/commands/status/{correlationId}`. The new `CommandStatusController` route MUST match this exactly: `[Route("api/v1/commands/status")]` with action `[HttpGet("{correlationId}")]`.

**H6 -- Future status updates from actor processing:** Story 2.6 only writes the `Received` status at the API layer. Status transitions to `Processing`, `EventsStored`, `EventsPublished`, and terminal states will be implemented in Story 3.11 (Actor State Machine). The `ICommandStatusStore` interface and `DaprCommandStatusStore` implementation must support these future writes -- the design here is the foundation for the full lifecycle.

### Project Structure Notes

**Alignment with Architecture:**
- `ICommandStatusStore` and `DaprCommandStatusStore` go in `Server/Commands/` -- follows the architecture doc's `Server/Commands/CommandStatusWriter.cs` placement
- `CommandStatusController` goes in `CommandApi/Controllers/` alongside existing `CommandsController`
- `CommandStatusResponse` goes in `CommandApi/Models/` alongside existing `SubmitCommandResponse`
- `InMemoryCommandStatusStore` goes in `Testing/Fakes/` alongside existing `InMemoryStateManager` and `FakeDomainServiceInvoker`
- Registration in `AddCommandApi()` per enforcement rule #10

**Dependency Graph Relevant to This Story:**
```
CommandApi -> Server -> Contracts
CommandApi -> Dapr.AspNetCore (AddDaprClient)
Server -> Dapr.Client (DaprClient for state store)
Server -> Contracts (CommandStatusRecord)
Testing -> Server (ICommandStatusStore interface)
Tests: IntegrationTests -> CommandApi (via WebApplicationFactory)
Tests: Server.Tests -> Server (unit testing DaprCommandStatusStore)
```

**Architecture Reference -- Data Flow After Story 2.6:**
```
POST /api/v1/commands
    |
    v
CorrelationIdMiddleware -> JWT Auth -> [Authorize] -> Tenant pre-check
    |
    v
MediatR: Logging -> Validation -> Authorization -> SubmitCommandHandler
    |-- Write Received status to state store (advisory, D2)
    |     Key: {tenant}:{correlationId}:status
    |     TTL: 86400s (24 hours)
    |-- Return SubmitCommandResult (202 Accepted + Location header)

GET /api/v1/commands/status/{correlationId}
    |
    v
CorrelationIdMiddleware -> JWT Auth -> [Authorize]
    |
    v
CommandStatusController.GetStatus()
    |-- Extract eventstore:tenant claims
    |-- For each tenant: ReadStatusAsync(tenant, correlationId)
    |-- Found? -> 200 OK CommandStatusResponse
    |-- Not found? -> 404 ProblemDetails
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.6: Command Status Tracking & Query Endpoint]
- [Source: _bmad-output/planning-artifacts/architecture.md#D2: Command Status Storage]
- [Source: _bmad-output/planning-artifacts/architecture.md#Security-Critical Architectural Constraints - SEC-3]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines - Rules #5, #7, #9, #10, #12, #13]
- [Source: _bmad-output/planning-artifacts/architecture.md#Data Flow - Status query and write paths]
- [Source: _bmad-output/planning-artifacts/architecture.md#DAPR State Store Keys - D1, D2]
- [Source: _bmad-output/planning-artifacts/architecture.md#Process Patterns - Error Handling]
- [Source: _bmad-output/planning-artifacts/prd.md#Command API Specification - GET /commands/{correlationId}/status]
- [Source: _bmad-output/planning-artifacts/prd.md#Data Schemas - Composite Key Strategy]
- [Source: _bmad-output/implementation-artifacts/2-5-endpoint-authorization-and-command-rejection.md]
- [Source: _bmad-output/implementation-artifacts/2-4-jwt-authentication-and-claims-transformation.md (referenced)]
- [Source: _bmad-output/implementation-artifacts/2-3-mediatr-pipeline-and-logging-behavior.md (referenced)]
- [Source: _bmad-output/implementation-artifacts/2-2-command-validation-and-rfc-7807-error-responses.md (referenced)]
- [Source: _bmad-output/implementation-artifacts/2-1-commandapi-host-and-minimal-endpoint-scaffolding.md (referenced)]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Fixed xUnit1030 analyzer errors: test methods must not use `ConfigureAwait(false)` per xUnit rules
- Fixed CS8620 nullable reference type mismatch in NSubstitute mock for `DaprClient.GetStateAsync<CommandStatusRecord>`
- Updated existing `SubmitCommandHandlerTests` to inject `InMemoryCommandStatusStore` (required after adding `ICommandStatusStore` dependency)

### Completion Notes List

- Implemented `ICommandStatusStore` abstraction with `DaprCommandStatusStore` (DAPR state store) and `InMemoryCommandStatusStore` (test fake with TTL simulation)
- Created `CommandStatusController` with `GET /api/v1/commands/status/{correlationId}` endpoint, tenant-scoped queries (SEC-3), and ProblemDetails error responses
- Modified `SubmitCommandHandler` to write "Received" status to state store before returning (advisory per rule #12)
- Registered `DaprClient` in Program.cs (`AddDaprClient()`) -- first DAPR client registration in the project
- Registered `DaprCommandStatusStore` as singleton `ICommandStatusStore` in `AddCommandApi()`
- Created `CommandStatusResponse` model with `FromRecord()` factory method, ISO 8601 duration format for TimeoutDuration
- All 311 tests pass (27 new tests added): 6 DaprCommandStatusStore unit tests, 3 SubmitCommandHandler status tests, 8 CommandStatusController unit tests, 10 integration tests
- Zero regressions on existing 284 tests

### Change Log

- 2026-02-13: Story 2.6 implementation complete -- Command status tracking & query endpoint with DAPR state store integration, tenant-scoped queries, advisory status writes, and comprehensive test coverage (27 new tests)
- 2026-02-13: Code review fixes applied (8 issues: 1 CRITICAL, 2 HIGH, 3 MEDIUM, 2 LOW):
  - [C1] Fixed TimeoutDuration to use XmlConvert.ToString() for ISO 8601 duration (was using TimeSpan "c" format)
  - [H1] Added OperationCanceledException re-throw in DaprCommandStatusStore and SubmitCommandHandler catch blocks
  - [H2] Fixed ProblemDetails correlationId for 400 path -- moved requestCorrelationId computation before GUID validation
  - [M1] Added missing [ProducesResponseType(400)] OpenAPI attribute
  - [M2] Upgraded ArgumentNullException.ThrowIfNull to ArgumentException.ThrowIfNullOrWhiteSpace for tenantId/correlationId
  - [M3] Fixed flaky TTL test: changed TTL from 0 to -1, removed Task.Delay(50)
  - [L1] Removed unnecessary HttpContext null checks in CreateProblemDetails
  - [L2] Changed GetAllStatuses() to return snapshot copy instead of live reference
  - Added new test: GetStatus_TimedOutStatus_IncludesIso8601Duration (total: 28 new tests, 312 overall)

### File List

**New files:**
- src/Hexalith.EventStore.Server/Commands/ICommandStatusStore.cs
- src/Hexalith.EventStore.Server/Commands/DaprCommandStatusStore.cs
- src/Hexalith.EventStore.Server/Commands/CommandStatusConstants.cs
- src/Hexalith.EventStore.Server/Commands/CommandStatusOptions.cs
- src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs
- src/Hexalith.EventStore.CommandApi/Models/CommandStatusResponse.cs
- src/Hexalith.EventStore.Testing/Fakes/InMemoryCommandStatusStore.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/DaprCommandStatusStoreTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/SubmitCommandHandlerStatusTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/CommandStatusControllerTests.cs
- tests/Hexalith.EventStore.IntegrationTests/CommandApi/CommandStatusIntegrationTests.cs

**Modified files:**
- src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs (added ICommandStatusStore injection, Received status write)
- src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs (registered ICommandStatusStore, CommandStatusOptions)
- src/Hexalith.EventStore.CommandApi/Program.cs (added builder.Services.AddDaprClient())
- tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs (updated for ICommandStatusStore dependency)
- tests/Hexalith.EventStore.IntegrationTests/Helpers/JwtAuthenticatedWebApplicationFactory.cs (override DaprCommandStatusStore with InMemoryCommandStatusStore)
