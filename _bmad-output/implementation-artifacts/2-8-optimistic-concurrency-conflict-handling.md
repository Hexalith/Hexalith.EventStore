# Story 2.8: Optimistic Concurrency Conflict Handling

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an **API consumer**,
I want the system to detect and report optimistic concurrency conflicts when two commands target the same aggregate simultaneously,
So that I know to retry or handle the conflict (FR7).

## Acceptance Criteria

1. **409 Conflict for concurrency conflicts** - Given two concurrent commands target the same aggregate, When the second command encounters an optimistic concurrency conflict (ETag mismatch propagated from the actor processing layer), Then the API returns `409 Conflict` as RFC 7807 ProblemDetails (NFR26).

2. **ProblemDetails extensions for concurrency conflicts** - The 409 ProblemDetails includes `correlationId`, `aggregateId`, and a human-readable `detail` message explaining the concurrency conflict. The `detail` message guides the consumer to retry the command.

3. **Command status updated to Rejected** - When a concurrency conflict occurs, the command status is updated to `Rejected` with reason "ConcurrencyConflict" via the advisory status write pattern (enforcement rule #12). Status update failure does not block the 409 response.

4. **Consumer can retry** - The consumer can retry the command after receiving a 409 Conflict response. The retried command will be processed against the updated aggregate state. The 409 response includes a `Retry-After: 1` header and sufficient information for the consumer to decide on retry strategy. Consumers should apply exponential backoff with jitter to avoid thundering herd on high-contention aggregates.

**Scope note:** This story covers the API layer's handling and response formatting for concurrency conflicts. The actual ETag-based conflict detection at the state store level is implemented in Epic 3 (Story 3.7). For Epic 2 testing, concurrency conflicts are simulated via a thrown `ConcurrencyConflictException` from a mock/stub of the actor processing layer.

## Prerequisites

**BLOCKING: Stories 2.6 (Command Status Tracking) and 2.7 (Command Replay) SHOULD be complete before starting Story 2.8.** Story 2.8 depends on:
- `[Authorize]` attribute enforcement on controllers (Story 2.4)
- `EventStoreClaimsTransformation` providing `eventstore:tenant` claims (Story 2.4)
- `TestJwtTokenGenerator` for integration tests (Story 2.4)
- `ICommandStatusStore` for writing Rejected status (Story 2.6)
- `DaprClient` registration via `AddDaprClient()` (Story 2.6)
- `CommandStatusRecord` and `CommandStatus` enum (Contracts package)
- Established ProblemDetails error handling patterns (Stories 2.1-2.2)
- MediatR pipeline with LoggingBehavior, ValidationBehavior, AuthorizationBehavior (Stories 2.3, 2.5)
- IExceptionHandler chain: ValidationExceptionHandler -> AuthorizationExceptionHandler -> GlobalExceptionHandler (Stories 2.2, 2.5)

**Before beginning any Task below, verify:** Run existing tests to confirm Stories 2.4-2.7 artifacts are in place. All existing tests must pass before proceeding.

## Tasks / Subtasks

- [x] Task 0: Verify prerequisites and existing artifacts (BLOCKING)
  - [x] 0.1 Confirm Stories 2.4-2.7 artifacts are in place (`[Authorize]`, `EventStoreClaimsTransformation`, `TestJwtTokenGenerator`, `AuthorizationBehavior`, `ICommandStatusStore`, `DaprCommandStatusStore`, `InMemoryCommandStatusStore`, `ICommandArchiveStore`)
  - [x] 0.2 Run all existing tests -- they must pass before proceeding
  - [x] 0.3 Verify `IExceptionHandler` chain is registered in `AddCommandApi()`: ValidationExceptionHandler -> AuthorizationExceptionHandler -> GlobalExceptionHandler

- [x] Task 1: Create ConcurrencyConflictException in Server package (AC: #1)
  - [x] 1.1 Create `ConcurrencyConflictException` class in `Server/Commands/` extending `Exception`
  - [x] 1.2 Primary domain constructor accepts: `string correlationId`, `string aggregateId`, `string? tenantId = null`, `string? detail = null`, `string? conflictSource = null`, `Exception? innerException = null`
  - [x] 1.3 Properties: `CorrelationId` (string), `AggregateId` (string), `TenantId` (string?), `ConflictSource` (string?) -- ConflictSource for future extensibility (e.g., "StateStore", "ActorReentrancy")
  - [x] 1.6 Add standard exception constructors for serialization support: parameterless, message-only, message+inner (CorrelationId/AggregateId default to empty string)
  - [x] 1.4 Default detail message: `"An optimistic concurrency conflict occurred on aggregate '{aggregateId}'. Another command was processed for this aggregate between read and write. Retry the command to process against the updated state."`
  - [x] 1.5 This exception will be thrown by actor processing code (Story 3.7) when ETag mismatch is detected. For now, it defines the contract.

- [x] Task 2: Create ConcurrencyConflictExceptionHandler in CommandApi (AC: #1, #2, #3)
  - [x] 2.1 Create `ConcurrencyConflictExceptionHandler` in `CommandApi/ErrorHandling/` implementing `IExceptionHandler`
  - [x] 2.2 Handle `ConcurrencyConflictException` (including when wrapped in InnerException chain by DAPR actor proxy) -- use `FindConcurrencyConflict` helper method that walks InnerException chain (max depth 10). Return `false` only if no `ConcurrencyConflictException` found at any depth
  - [x] 2.3 Build `409 Conflict` ProblemDetails with:
    - `type`: `"https://tools.ietf.org/html/rfc9457#section-3"`
    - `title`: `"Conflict"`
    - `status`: `409`
    - `detail`: Exception's detail message (human-readable, describes concurrency conflict and advises retry)
    - `instance`: `HttpContext.Request.Path`
    - Extensions: `correlationId` (from request), `aggregateId` (from exception)
  - [x] 2.4 Include `tenantId` extension if available from exception or HttpContext.Items
  - [x] 2.5 Write advisory Rejected status before returning 409 response:
    - Use `ICommandStatusStore.WriteStatusAsync` with `CommandStatus.Rejected` and `FailureReason: "ConcurrencyConflict"`
    - Wrap in try/catch per enforcement rule #12 (advisory, never blocks)
    - Log status write failure at Warning level
  - [x] 2.6 Log concurrency conflict at Warning level with structured properties: correlationId, aggregateId, tenantId
  - [x] 2.7 Set response Content-Type to `application/problem+json`
  - [x] 2.8 Add `Retry-After: 1` response header to help consumers pace retries and avoid thundering herd
  - [x] 2.9 Return `true` to indicate the exception was handled

- [x] Task 3: Register ConcurrencyConflictExceptionHandler in DI (AC: all)
  - [x] 3.1 In `ServiceCollectionExtensions.AddCommandApi()`, register `ConcurrencyConflictExceptionHandler` BEFORE `GlobalExceptionHandler` in the `IExceptionHandler` chain
  - [x] 3.2 Handler order: ValidationExceptionHandler -> AuthorizationExceptionHandler -> ConcurrencyConflictExceptionHandler -> GlobalExceptionHandler

- [x] Task 4: Write unit tests for ConcurrencyConflictException (AC: #1)
  - [x] 4.1 `Constructor_WithAllParameters_SetsProperties` -- verify CorrelationId, AggregateId, TenantId, Message set correctly
  - [x] 4.2 `Constructor_WithNullDetail_UsesDefaultMessage` -- verify default detail message includes aggregateId
  - [x] 4.3 `Constructor_WithCustomDetail_UsesCustomMessage` -- verify custom detail overrides default
  - [x] 4.4 `Constructor_WithInnerException_PreservesInnerException` -- verify inner exception chain
  - [x] 4.5 `Constructor_Parameterless_SetsDefaults` -- verify CorrelationId/AggregateId are empty string, default message
  - [x] 4.6 `Constructor_MessageOnly_SetsMessage` -- verify message and empty CorrelationId/AggregateId
  - [x] 4.7 `Constructor_MessageAndInner_PreservesInnerException` -- verify serialization-style constructor
  - [x] 4.8 `Constructor_WithConflictSource_SetsProperty` -- verify ConflictSource property

- [x] Task 5: Write unit tests for ConcurrencyConflictExceptionHandler (AC: #1, #2, #3)
  - [x] 5.1 `TryHandleAsync_ConcurrencyConflictException_Returns409ProblemDetails` -- verify 409 status, ProblemDetails format
  - [x] 5.2 `TryHandleAsync_ConcurrencyConflictException_IncludesCorrelationIdExtension` -- verify correlationId in extensions
  - [x] 5.3 `TryHandleAsync_ConcurrencyConflictException_IncludesAggregateIdExtension` -- verify aggregateId in extensions
  - [x] 5.4 `TryHandleAsync_ConcurrencyConflictException_IncludesTenantIdExtension` -- verify tenantId when available
  - [x] 5.5 `TryHandleAsync_ConcurrencyConflictException_WritesRejectedStatus` -- verify status write with Rejected + ConcurrencyConflict reason
  - [x] 5.6 `TryHandleAsync_StatusWriteFails_StillReturns409` -- verify advisory pattern (rule #12)
  - [x] 5.7 `TryHandleAsync_StatusWriteFails_LogsWarning` -- verify warning logged on status write failure
  - [x] 5.8 `TryHandleAsync_OtherException_ReturnsFalse` -- verify handler passes through non-concurrency exceptions
  - [x] 5.9 `TryHandleAsync_ConcurrencyConflictException_LogsWarning` -- verify structured logging with correlationId, aggregateId
  - [x] 5.10 `TryHandleAsync_WrappedConcurrencyConflictException_Returns409` -- verify handler unwraps InnerException chain (simulates DAPR ActorMethodInvocationException wrapping)
  - [x] 5.11 `TryHandleAsync_DeeplyNestedConcurrencyConflictException_Returns409` -- verify unwrapping works at depth > 1 (e.g., AggregateException -> ActorMethodInvocationException -> ConcurrencyConflictException)
  - [x] 5.12 `TryHandleAsync_409Response_IncludesRetryAfterHeader` -- verify Retry-After: 1 header in response

- [x] Task 6: Write integration tests for concurrency conflict handling (AC: #1, #2, #3, #4)
  - [x] 6.1 Create test infrastructure: override `SubmitCommandHandler` with a test handler that throws `ConcurrencyConflictException` when a special command type is submitted
  - [x] 6.2 `PostCommands_ConcurrencyConflict_Returns409ProblemDetails` -- submit command that triggers simulated conflict, verify 409 response
  - [x] 6.3 `PostCommands_ConcurrencyConflict_ProblemDetailsIncludesCorrelationId` -- verify correlationId extension
  - [x] 6.4 `PostCommands_ConcurrencyConflict_ProblemDetailsIncludesAggregateId` -- verify aggregateId extension
  - [x] 6.5 `PostCommands_ConcurrencyConflict_ProblemDetailsIncludesDetailMessage` -- verify human-readable detail about concurrency conflict
  - [x] 6.6 `PostCommands_ConcurrencyConflict_StatusUpdatedToRejected` -- verify status written with Rejected + ConcurrencyConflict reason via InMemoryCommandStatusStore
  - [x] 6.7 `PostCommands_ConcurrencyConflict_RetrySucceeds` -- submit conflict command, then submit normal command, verify 202 Accepted (demonstrates retry path)
  - [x] 6.8 `PostCommands_ConcurrencyConflict_NoAuthentication_Returns401` -- verify auth still enforced (no regression)
  - [x] 6.9 `PostCommands_ConcurrencyConflict_ResponseContentType` -- verify `application/problem+json` Content-Type
  - [x] 6.10 `PostCommands_ConcurrencyConflict_Returns409NotFallback500` -- verify that the handler chain ordering is correct by confirming the response is 409 (not 500 from GlobalExceptionHandler). This catches registration-order regressions where ConcurrencyConflictExceptionHandler is accidentally registered after GlobalExceptionHandler
  - [x] 6.11 `PostCommands_ConcurrencyConflict_ResponseIncludesRetryAfterHeader` -- verify Retry-After: 1 header present in integration response

- [x] Task 7: Verify no regressions (AC: all)
  - [x] 7.1 Run all existing tests -- zero regressions expected
  - [x] 7.2 Verify existing exception handler chain still works (validation 400, authorization 403, global 500)
  - [x] 7.3 Verify ReplayController's inline 409 handling (replay conflicts) still works independently

## Dev Notes

### Architecture Compliance

**FR7: Optimistic Concurrency Conflict Rejection:**
- The system detects and reports optimistic concurrency conflicts (ETag mismatch) as `409 Conflict`
- Conflicts are never silently overwritten (NFR26)
- The consumer receives actionable information to decide on retry strategy
- The actual ETag detection is in Epic 3 (Story 3.7); Story 2.8 builds the API-layer handling infrastructure

**D1: Event Storage Strategy -- ETag-Based Concurrency:**
- Architecture specifies ETag-based optimistic concurrency on the aggregate metadata key: `{tenant}:{domain}:{aggId}:metadata`
- When the actor processes a command, it reads the metadata ETag, writes events, and updates metadata with the ETag
- If another command modified the metadata between read and write, the ETag mismatch causes a conflict
- The actor code (Epic 3) will throw `ConcurrencyConflictException`, which propagates through MediatR back to the API layer
- Story 2.8 provides the `IExceptionHandler` that maps this exception to 409 ProblemDetails

**D5: RFC 7807 Problem Details + Extensions:**
- Concurrency conflict 409 response follows the same ProblemDetails format as all other error responses
- Extensions include `correlationId`, `aggregateId`, and optionally `tenantId`
- No stack traces in responses (enforcement rule #13)

**Enforcement Rules to Follow:**
- Rule #5: Never log event payload data -- only envelope metadata (SEC-5, NFR12)
- Rule #7: ProblemDetails for all API error responses -- 409 must be ProblemDetails
- Rule #9: correlationId in every structured log entry and OpenTelemetry activity
- Rule #10: Register services via `Add*` extension methods -- never inline in Program.cs
- Rule #12: **CRITICAL** -- Command status writes are advisory. Status update to Rejected MUST NOT block the 409 response
- Rule #13: No stack traces in production error responses

### Critical Design Decisions

**What Already Exists (from Stories 2.1-2.7):**
- `CommandsController` with `[Authorize]` and POST `/api/v1/commands` -- returns 202 with Location header
- `CommandStatusController` with GET `/api/v1/commands/status/{correlationId}` -- tenant-scoped status queries (Story 2.6)
- `ReplayController` with POST `/api/v1/commands/replay/{correlationId}` -- inline 409 for replay conflicts (Story 2.7)
- `ICommandStatusStore` + `DaprCommandStatusStore` + `InMemoryCommandStatusStore` -- status read/write (Story 2.6)
- `ICommandArchiveStore` + `DaprCommandArchiveStore` + `InMemoryCommandArchiveStore` -- archive read/write (Story 2.7)
- `CommandStatus` enum (8 states) and `CommandStatusRecord` in Contracts package
- `SubmitCommand` MediatR command and `SubmitCommandHandler`
- MediatR pipeline: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> SubmitCommandHandler
- `CorrelationIdMiddleware` generates/propagates correlation IDs
- `EventStoreClaimsTransformation` normalizes JWT claims to `eventstore:*`
- ProblemDetails error handling chain: `ValidationExceptionHandler` (400) -> `AuthorizationExceptionHandler` (403) -> `GlobalExceptionHandler` (500)
- `TestJwtTokenGenerator` for integration tests with configurable claims
- `DaprClient` registered via `AddDaprClient()` (Story 2.6)
- `CommandStatusOptions` with TtlSeconds and StateStoreName (Story 2.6)

**What Story 2.8 Adds:**
1. **`ConcurrencyConflictException`** -- exception class in Server/Commands/ that actor processing code (Epic 3) will throw when ETag conflicts are detected. Carries correlationId, aggregateId, tenantId, and a human-readable detail message
2. **`ConcurrencyConflictExceptionHandler`** -- IExceptionHandler in CommandApi/ErrorHandling/ that maps ConcurrencyConflictException to 409 ProblemDetails with extensions, writes advisory Rejected status
3. **Updated exception handler chain**: ValidationExceptionHandler -> AuthorizationExceptionHandler -> **ConcurrencyConflictExceptionHandler** -> GlobalExceptionHandler

**Concurrency Conflict Flow (API Layer):**
```
POST /api/v1/commands
    |
    v
CorrelationIdMiddleware -> JWT Auth -> [Authorize] -> Tenant pre-check
    |
    v
MediatR: Logging -> Validation -> Authorization -> SubmitCommandHandler
    |-- Write Received status (advisory, Story 2.6)
    |-- Archive original command (advisory, Story 2.7)
    |-- [FUTURE: Story 3.1+] Route to actor -> actor processes -> ETag mismatch detected
    |-- Actor throws ConcurrencyConflictException(correlationId, aggregateId, tenantId)
    |-- Exception propagates back through MediatR pipeline
    |
    v
ExceptionHandler middleware catches exception:
    |-- ConcurrencyConflictExceptionHandler recognizes ConcurrencyConflictException
    |-- Write Rejected status: CommandStatusRecord(Rejected, UtcNow, aggregateId,
    |     FailureReason: "ConcurrencyConflict") -- advisory per rule #12
    |-- Build 409 ProblemDetails with correlationId + aggregateId extensions
    |-- Return 409 Conflict as application/problem+json
```

**Exception Propagation Path:**
The `ConcurrencyConflictException` will be thrown inside the actor processing pipeline (Epic 3, Story 3.7) when the DAPR state store returns an ETag mismatch during `IActorStateManager.SaveStateAsync`. The exception propagates through:
1. Actor method -> MediatR handler (SubmitCommandHandler) -> MediatR pipeline (LoggingBehavior catches and re-throws with logging) -> ASP.NET Core exception handling middleware -> `ConcurrencyConflictExceptionHandler`

**DAPR Actor Exception Wrapping:** DAPR actor proxy may wrap actor-thrown exceptions in `ActorMethodInvocationException`. The `ConcurrencyConflictExceptionHandler` includes a `FindConcurrencyConflict` helper that walks the `InnerException` chain (up to depth 10) to find the underlying `ConcurrencyConflictException`. This ensures the handler works regardless of whether the exception arrives directly or wrapped.

The MediatR pipeline will log the exception at the LoggingBehavior level (already implemented in Story 2.3). The exception handler then converts it to a 409 ProblemDetails response.

**409 ProblemDetails Format:**
```
HTTP/1.1 409 Conflict
Content-Type: application/problem+json
Retry-After: 1
```
```json
{
  "type": "https://tools.ietf.org/html/rfc9457#section-3",
  "title": "Conflict",
  "status": 409,
  "detail": "An optimistic concurrency conflict occurred on aggregate 'order-123'. Another command was processed for this aggregate between read and write. Retry the command to process against the updated state.",
  "instance": "/api/v1/commands",
  "correlationId": "request-correlation-id",
  "aggregateId": "order-123",
  "tenantId": "acme"
}
```

**ConcurrencyConflictException Class:**
```csharp
// In Server/Commands/
namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Thrown when an optimistic concurrency conflict is detected during aggregate event persistence.
/// The actor processing layer (Story 3.7) throws this when an ETag mismatch occurs on the
/// aggregate metadata key, indicating another command was processed for the same aggregate
/// between state read and event write.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    private const string DefaultDetailTemplate =
        "An optimistic concurrency conflict occurred on aggregate '{0}'. " +
        "Another command was processed for this aggregate between read and write. " +
        "Retry the command to process against the updated state.";

    /// <summary>Standard parameterless constructor (serialization support).</summary>
    public ConcurrencyConflictException()
        : base("An optimistic concurrency conflict occurred.")
    {
        CorrelationId = string.Empty;
        AggregateId = string.Empty;
    }

    /// <summary>Standard message-only constructor (serialization support).</summary>
    public ConcurrencyConflictException(string message)
        : base(message)
    {
        CorrelationId = string.Empty;
        AggregateId = string.Empty;
    }

    /// <summary>Standard message+inner constructor (serialization support).</summary>
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
        CorrelationId = string.Empty;
        AggregateId = string.Empty;
    }

    /// <summary>Primary domain constructor with full context.</summary>
    public ConcurrencyConflictException(
        string correlationId,
        string aggregateId,
        string? tenantId = null,
        string? detail = null,
        string? conflictSource = null,
        Exception? innerException = null)
        : base(detail ?? string.Format(DefaultDetailTemplate, aggregateId), innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);

        CorrelationId = correlationId;
        AggregateId = aggregateId;
        TenantId = tenantId;
        ConflictSource = conflictSource;
    }

    public string CorrelationId { get; }
    public string AggregateId { get; }
    public string? TenantId { get; }

    /// <summary>
    /// Optional identifier for the source of the conflict (e.g., "StateStore", "ActorReentrancy").
    /// Provides future extensibility for distinguishing conflict origins without breaking changes.
    /// </summary>
    public string? ConflictSource { get; }
}
```

**ConcurrencyConflictExceptionHandler Class:**
```csharp
// In CommandApi/ErrorHandling/
namespace Hexalith.EventStore.CommandApi.ErrorHandling;

public class ConcurrencyConflictExceptionHandler(
    ICommandStatusStore statusStore,
    ILogger<ConcurrencyConflictExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // C1: Unwrap InnerException chain -- DAPR actor proxy may wrap exceptions
        // in ActorMethodInvocationException. Check the full chain for ConcurrencyConflictException.
        ConcurrencyConflictException? conflict = FindConcurrencyConflict(exception);
        if (conflict is null)
            return false;

        string correlationId = httpContext.Items["CorrelationId"]?.ToString()
            ?? conflict.CorrelationId;

        logger.LogWarning(
            "Concurrency conflict: CorrelationId={CorrelationId}, AggregateId={AggregateId}, TenantId={TenantId}",
            correlationId,
            conflict.AggregateId,
            conflict.TenantId);

        // Advisory status write: Rejected with ConcurrencyConflict reason (rule #12)
        try
        {
            if (conflict.TenantId is not null)
            {
                await statusStore.WriteStatusAsync(
                    conflict.TenantId,
                    conflict.CorrelationId,
                    new CommandStatusRecord(
                        CommandStatus.Rejected,
                        DateTimeOffset.UtcNow,
                        conflict.AggregateId,
                        EventCount: null,
                        RejectionEventType: null,
                        FailureReason: "ConcurrencyConflict",
                        TimeoutDuration: null),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to write Rejected status for concurrency conflict. CorrelationId={CorrelationId}",
                correlationId);
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Type = "https://tools.ietf.org/html/rfc9457#section-3",
            Detail = conflict.Message,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["aggregateId"] = conflict.AggregateId,
            },
        };

        string? tenantId = conflict.TenantId
            ?? (httpContext.Items.TryGetValue("RequestTenantId", out var t) && t is string ts
                ? ts : null);

        if (tenantId is not null)
            problemDetails.Extensions["tenantId"] = tenantId;

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        httpContext.Response.ContentType = "application/problem+json";
        // C2: Add Retry-After header to help consumers pace retries and avoid thundering herd
        httpContext.Response.Headers["Retry-After"] = "1";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Walks the InnerException chain looking for ConcurrencyConflictException.
    /// DAPR actor proxy wraps actor-thrown exceptions in ActorMethodInvocationException,
    /// so the handler must unwrap to find the real cause. Limits depth to 10 to prevent
    /// infinite loops on circular exception references.
    /// </summary>
    private static ConcurrencyConflictException? FindConcurrencyConflict(Exception? exception)
    {
        const int maxDepth = 10;
        Exception? current = exception;
        for (int i = 0; i < maxDepth && current is not null; i++)
        {
            if (current is ConcurrencyConflictException conflict)
                return conflict;
            current = current.InnerException;
        }
        return null;
    }
}
```

**Why a dedicated IExceptionHandler (not inline like ReplayController 409):**
The ReplayController's 409 is a domain-level "you can't replay this" decision made inside the controller. The concurrency conflict 409 is an infrastructure-level error that can occur during any command processing -- it propagates as an exception through MediatR and needs to be caught at the middleware level. Using `IExceptionHandler` is the correct pattern because:
1. The exception originates deep in the actor/state store layer, not in a controller
2. It follows the established pattern for cross-cutting error handling (validation, authorization, global)
3. It centralizes the 409 response formatting in one place
4. The handler can write advisory status updates before responding

**Distinction from ReplayController's 409:**
- ReplayController 409: Deterministic, controller-level -- "this command's status doesn't allow replay"
- Concurrency 409: Non-deterministic, infrastructure-level -- "two commands raced and one lost"
- Both use RFC 7807 ProblemDetails but with different extensions and detail messages
- They are completely independent -- ReplayController continues using inline ProblemDetails

### Technical Requirements

**Existing Types to Use:**
- `CommandStatus` from `Hexalith.EventStore.Contracts.Commands` -- 8-state enum (specifically `Rejected`)
- `CommandStatusRecord` from `Hexalith.EventStore.Contracts.Commands` -- immutable status record (use `FailureReason` field)
- `ICommandStatusStore` from `Hexalith.EventStore.Server.Commands` -- status write for Rejected (Story 2.6)
- `InMemoryCommandStatusStore` from `Hexalith.EventStore.Testing.Fakes` -- test fake (Story 2.6)
- `SubmitCommand` from `Hexalith.EventStore.Server.Pipeline.Commands` -- MediatR command
- `CorrelationIdMiddleware.HttpContextKey` (constant `"CorrelationId"`) -- access correlation ID
- `ProblemDetails` from `Microsoft.AspNetCore.Mvc` -- RFC 7807 response format
- `IExceptionHandler` from `Microsoft.AspNetCore.Diagnostics` -- exception handler chain
- `TestJwtTokenGenerator` from integration tests -- generate JWT tokens with tenant claims
- `EventStoreActivitySources.CommandApi` -- ActivitySource for tracing

**New Types to Create:**
- `ConcurrencyConflictException` -- exception class for ETag-based concurrency conflicts (Server package, Commands/ folder)
- `ConcurrencyConflictExceptionHandler` -- IExceptionHandler for 409 ProblemDetails (CommandApi package, ErrorHandling/ folder)

**NuGet Packages Already Available (no new packages needed):**
- `MediatR` 14.0.0 -- `IMediator.Send`, `IRequestHandler`
- `Microsoft.AspNetCore.Mvc.Testing` 10.0.0 -- WebApplicationFactory
- `Shouldly` 4.3.0 -- test assertions
- `NSubstitute` 5.3.0 -- mocking for unit tests

### File Structure Requirements

**New files to create:**
```
src/Hexalith.EventStore.Server/
  Commands/
    ConcurrencyConflictException.cs          # NEW: Exception for ETag concurrency conflicts

src/Hexalith.EventStore.CommandApi/
  ErrorHandling/
    ConcurrencyConflictExceptionHandler.cs   # NEW: IExceptionHandler -> 409 ProblemDetails

tests/Hexalith.EventStore.Server.Tests/
  Commands/
    ConcurrencyConflictExceptionTests.cs     # NEW: Unit tests for exception class

tests/Hexalith.EventStore.Server.Tests/
  Commands/
    ConcurrencyConflictExceptionHandlerTests.cs  # NEW: Unit tests for exception handler

tests/Hexalith.EventStore.IntegrationTests/
  CommandApi/
    ConcurrencyConflictIntegrationTests.cs   # NEW: Integration tests with simulated conflicts
```

**Existing files to modify:**
```
src/Hexalith.EventStore.CommandApi/
  Extensions/
    ServiceCollectionExtensions.cs           # MODIFY: Register ConcurrencyConflictExceptionHandler in handler chain
```

**Existing files to verify (no changes expected):**
```
src/Hexalith.EventStore.Contracts/
  Commands/
    CommandStatus.cs                         # VERIFY: Enum has Rejected state
    CommandStatusRecord.cs                   # VERIFY: Record has FailureReason field

src/Hexalith.EventStore.CommandApi/
  ErrorHandling/
    ValidationExceptionHandler.cs            # VERIFY: Still handles 400s
    AuthorizationExceptionHandler.cs         # VERIFY: Still handles 403s
    GlobalExceptionHandler.cs                # VERIFY: Still catch-all for 500s
  Controllers/
    ReplayController.cs                      # VERIFY: Inline 409 still independent
    CommandsController.cs                    # VERIFY: POST endpoint unchanged
```

### Testing Requirements

**Test Projects:**
- `tests/Hexalith.EventStore.Server.Tests/` -- Unit tests for ConcurrencyConflictException and ConcurrencyConflictExceptionHandler
- `tests/Hexalith.EventStore.IntegrationTests/` -- Integration tests for full concurrency conflict flow

**Test Patterns (established in Stories 1.6, 2.1-2.7):**
- Method naming: `{Method}_{Scenario}_{ExpectedResult}`
- Arrange/Act/Assert pattern
- Shouldly for assertions
- `WebApplicationFactory<CommandApiProgram>` for integration tests
- `TestJwtTokenGenerator` for creating JWT tokens with specific claims
- Primary constructors for DI injection
- Feature folder organization in test projects mirroring source

**Unit Test Strategy for ConcurrencyConflictException:**
Direct construction tests verifying properties and default/custom messages.

**Unit Test Strategy for ConcurrencyConflictExceptionHandler:**
Create a mock `HttpContext` with `ICommandStatusStore` (InMemoryCommandStatusStore or NSubstitute mock). Invoke `TryHandleAsync` with `ConcurrencyConflictException` and verify:
- 409 status code
- ProblemDetails format with correct extensions
- Rejected status written to status store
- Advisory pattern maintained (status write failure doesn't break handler)
- Non-concurrency exceptions return `false`

```csharp
// Example test setup:
var statusStore = new InMemoryCommandStatusStore();
var logger = new TestLogger<ConcurrencyConflictExceptionHandler>();
var handler = new ConcurrencyConflictExceptionHandler(statusStore, logger);

var httpContext = new DefaultHttpContext();
httpContext.Items["CorrelationId"] = "test-correlation-id";
httpContext.Request.Path = "/api/v1/commands";

var exception = new ConcurrencyConflictException(
    correlationId: "cmd-correlation-id",
    aggregateId: "order-123",
    tenantId: "acme");

var result = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);
result.ShouldBeTrue();
httpContext.Response.StatusCode.ShouldBe(409);
```

**Integration Test Strategy:**
To simulate concurrency conflicts without the actor layer, the integration tests need a way to make `SubmitCommandHandler` throw `ConcurrencyConflictException`. Two approaches:

**Approach A (Preferred): Configurable test handler that throws on a trigger**
Override `SubmitCommandHandler` registration in the test WebApplicationFactory with a decorator that checks for a special "trigger" command type (e.g., `commandType: "SimulateConcurrencyConflict"`) and throws `ConcurrencyConflictException` instead of processing normally.

```csharp
// In test project -- decorator handler
public class ConcurrencyConflictSimulatingHandler(
    SubmitCommandHandler inner,
    ICommandStatusStore statusStore,
    ICommandArchiveStore archiveStore,
    ILogger<SubmitCommandHandler> logger) : IRequestHandler<SubmitCommand, SubmitCommandResult>
{
    public async Task<SubmitCommandResult> Handle(SubmitCommand request, CancellationToken cancellationToken)
    {
        if (request.CommandType == "SimulateConcurrencyConflict")
        {
            // Write Received status first (mimics normal flow up to conflict point)
            var result = await inner.Handle(request, cancellationToken);
            throw new ConcurrencyConflictException(
                request.CorrelationId,
                request.AggregateId,
                request.Tenant);
        }
        return await inner.Handle(request, cancellationToken);
    }
}
```

**Approach B: Use MediatR pipeline behavior that intercepts**
Add a test-only `IPipelineBehavior` that throws `ConcurrencyConflictException` for specific command types. This avoids modifying handler registration.

Tests follow the pattern:
1. Submit a command with `commandType: "SimulateConcurrencyConflict"` and valid JWT
2. Verify 409 response with ProblemDetails format
3. Verify status updated to Rejected with ConcurrencyConflict reason
4. Submit a normal command afterward to verify retry path works

```csharp
// Submit command that triggers simulated conflict
var request = new { tenant = "test-tenant", domain = "orders", aggregateId = "order-123",
    commandType = "SimulateConcurrencyConflict", payload = "dGVzdA==" };
var response = await client.PostAsJsonAsync("/api/v1/commands", request);
response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
problemDetails!.Status.ShouldBe(409);
problemDetails.Extensions["aggregateId"].ToString().ShouldBe("order-123");
```

**Minimum Tests (28):**

ConcurrencyConflictException Unit Tests (8) -- in `ConcurrencyConflictExceptionTests.cs`:
1. `Constructor_WithAllParameters_SetsProperties`
2. `Constructor_WithNullDetail_UsesDefaultMessage`
3. `Constructor_WithCustomDetail_UsesCustomMessage`
4. `Constructor_WithInnerException_PreservesInnerException`
5. `Constructor_Parameterless_SetsDefaults`
6. `Constructor_MessageOnly_SetsMessage`
7. `Constructor_MessageAndInner_PreservesInnerException`
8. `Constructor_WithConflictSource_SetsProperty`

ConcurrencyConflictExceptionHandler Unit Tests (12) -- in `ConcurrencyConflictExceptionHandlerTests.cs`:
9. `TryHandleAsync_ConcurrencyConflictException_Returns409ProblemDetails`
10. `TryHandleAsync_ConcurrencyConflictException_IncludesCorrelationIdExtension`
11. `TryHandleAsync_ConcurrencyConflictException_IncludesAggregateIdExtension`
12. `TryHandleAsync_ConcurrencyConflictException_IncludesTenantIdExtension`
13. `TryHandleAsync_ConcurrencyConflictException_WritesRejectedStatus`
14. `TryHandleAsync_StatusWriteFails_StillReturns409`
15. `TryHandleAsync_StatusWriteFails_LogsWarning`
16. `TryHandleAsync_OtherException_ReturnsFalse`
17. `TryHandleAsync_ConcurrencyConflictException_LogsWarning`
18. `TryHandleAsync_WrappedConcurrencyConflictException_Returns409`
19. `TryHandleAsync_DeeplyNestedConcurrencyConflictException_Returns409`
20. `TryHandleAsync_409Response_IncludesRetryAfterHeader`

Integration Tests (8) -- in `ConcurrencyConflictIntegrationTests.cs`:
21. `PostCommands_ConcurrencyConflict_Returns409ProblemDetails`
22. `PostCommands_ConcurrencyConflict_ProblemDetailsIncludesCorrelationId`
23. `PostCommands_ConcurrencyConflict_ProblemDetailsIncludesAggregateId`
24. `PostCommands_ConcurrencyConflict_ProblemDetailsIncludesDetailMessage`
25. `PostCommands_ConcurrencyConflict_StatusUpdatedToRejected`
26. `PostCommands_ConcurrencyConflict_RetrySucceeds`
27. `PostCommands_ConcurrencyConflict_NoAuthentication_Returns401`
28. `PostCommands_ConcurrencyConflict_ResponseContentType`

Additional Integration Tests (5) -- in `ConcurrencyConflictIntegrationTests.cs`:
29. `PostCommands_ConcurrencyConflict_ProblemDetailsIncludesTenantId`
30. `PostCommands_ConcurrencyConflict_Returns409NotFallback500`
31. `PostCommands_ConcurrencyConflict_ResponseIncludesRetryAfterHeader`

**Current test count:** ~295 (after Stories 2.6-2.7). Story 2.8 adds 31 new tests, bringing estimated total to ~326.

### Previous Story Intelligence

**From Story 2.7 (Command Replay Endpoint):**
- Inline 409 ProblemDetails pattern used in ReplayController for non-replayable status -- different from the exception handler approach in Story 2.8
- `ArchivedCommandExtensions` with `ToArchivedCommand` and `ToSubmitCommand` factory methods
- `catch (OperationCanceledException) { throw; }` pattern established for advisory writes
- Advisory write pattern consistently applied: try/catch, log Warning, continue
- H5 hardening: null status -> 409 (indeterminate, not replayable)

**From Story 2.6 (Command Status Tracking & Query Endpoint):**
- `ICommandStatusStore` + `DaprCommandStatusStore` + `InMemoryCommandStatusStore` implementations
- `CommandStatusOptions` with TtlSeconds (86400) and StateStoreName ("statestore")
- `CommandStatusRecord` with `FailureReason` field (nullable string) -- Story 2.8 will use this for "ConcurrencyConflict"
- First `DaprClient` registration via `AddDaprClient()` in Program.cs
- Advisory status write pattern established

**From Story 2.5 (Endpoint Authorization & Command Rejection):**
- `CommandAuthorizationException` + `AuthorizationExceptionHandler` pattern -- Story 2.8 follows this exact pattern for `ConcurrencyConflictException` + `ConcurrencyConflictExceptionHandler`
- `IExceptionHandler` registration order matters -- more specific handlers before GlobalExceptionHandler
- `HttpContext.Items["RequestTenantId"]` for passing tenant info to error handlers

**From Story 2.2 (Command Validation & RFC 7807 Error Responses):**
- `ValidationExceptionHandler` established the IExceptionHandler pattern
- ProblemDetails always include `correlationId` extension
- `application/problem+json` Content-Type

**Key Patterns (mandatory for all new code):**
- Primary constructors for DI: `public class Foo(IDep dep) : Base`
- Records for immutable data
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` / `ArgumentException.ThrowIfNullOrWhiteSpace()` on public methods
- Feature folder organization
- `namespace Hexalith.EventStore.{Project}.{Feature};`
- Registration via `Add*` extension methods

### Elicitation-Derived Hardening Notes

**H1 -- Exception handler ordering:** The `ConcurrencyConflictExceptionHandler` MUST be registered AFTER `AuthorizationExceptionHandler` and BEFORE `GlobalExceptionHandler`. If registered after GlobalExceptionHandler, it will never execute because GlobalExceptionHandler catches all exceptions. ASP.NET Core's `IExceptionHandler` chain executes in registration order and stops at the first handler that returns `true`.

**H2 -- Status write race condition:** When a concurrency conflict occurs, the `SubmitCommandHandler` may have already written a `Received` status (Story 2.6) before the exception was thrown. The `ConcurrencyConflictExceptionHandler` then overwrites it with `Rejected` + `ConcurrencyConflict`. This is correct behavior -- the latest status wins, and the Rejected status accurately reflects the outcome. However, if the advisory Rejected write fails (rule #12), the status will remain `Received`. The consumer polling the status endpoint will see `Received` with no further updates. This is an acceptable edge case per rule #12 (status is ephemeral, not source of truth). **Consumers MUST treat the HTTP response code (409) as the authoritative signal of the conflict, not rely solely on status polling.** The status endpoint is advisory/best-effort, and the synchronous HTTP response is always the primary indicator of the command outcome.

**H3 -- TenantId availability in exception handler:** The `ConcurrencyConflictException` carries `TenantId` (set by the actor layer in Epic 3). However, in Story 2.8 testing with simulated conflicts, the tenantId comes from the `SubmitCommand.Tenant` field. The handler checks both the exception's `TenantId` and `HttpContext.Items["RequestTenantId"]` as fallback. If neither is available (shouldn't happen in production flow), the status write is skipped (no tenantId = can't construct the state store key).

**H4 -- Multiple concurrent conflicts:** If many clients simultaneously send commands to the same aggregate, multiple 409 responses will be returned. Each rejected command's status is independently updated to Rejected. The consumers should implement exponential backoff with jitter to avoid a thundering herd on retry. The 409 `detail` message advises "Retry the command to process against the updated state" but does not dictate retry timing -- that's the consumer's responsibility.

**H5 -- Concurrency conflict vs. replay conflict (disambiguation):** Story 2.8's 409 is for infrastructure-level ETag conflicts (non-deterministic, happens during concurrent processing). Story 2.7's 409 is for business-logic replay validation (deterministic, command is in wrong status for replay). They are completely independent:
- Concurrency 409: extensions include `aggregateId`, triggered by `ConcurrencyConflictException` via IExceptionHandler
- Replay 409: extensions include `currentStatus`, triggered by inline ProblemDetails in ReplayController
- A consumer can distinguish them by checking which extensions are present

**H6 -- Integration test approach:** Since the actor layer doesn't exist yet (Epic 3), integration tests must simulate concurrency conflicts. The preferred approach is a test-only `IPipelineBehavior` registered in the test WebApplicationFactory that throws `ConcurrencyConflictException` for a specific `commandType`. This avoids modifying production code for testing purposes. The behavior should be injected after the existing pipeline (logging, validation, authorization) so that the full pipeline executes before the simulated conflict.

**H7 -- LoggingBehavior interaction:** The existing `LoggingBehavior` (Story 2.3) catches and re-throws exceptions while logging them. When a `ConcurrencyConflictException` propagates through the MediatR pipeline, LoggingBehavior will log it as an error. This is acceptable -- the handler will log it as a warning with concurrency-specific structured properties, and LoggingBehavior's error log provides the pipeline-level context. No changes to LoggingBehavior are needed.

**H8 -- SEC-2 dependency for aggregateId safety:** The 409 ProblemDetails includes `aggregateId` in the response extensions. This is safe to expose only because tenant validation (SEC-2, enforced by AuthorizationBehavior in Story 2.5) runs before any state access occurs. If tenant validation were bypassed, a malicious caller could potentially observe aggregate IDs belonging to other tenants via concurrency error responses. Verify that the AuthorizationBehavior always executes before the handler that triggers ConcurrencyConflictException. The exception handler chain ordering (Auth 403 before Conflict 409) provides defense-in-depth: if an unauthorized request somehow triggered a concurrency conflict, the AuthorizationExceptionHandler would catch it first.

**H9 -- Test simulation fidelity gap:** Story 2.8 integration tests simulate concurrency conflicts using a test-only `IPipelineBehavior` that throws `ConcurrencyConflictException` directly. In production (Epic 3, Story 3.7), the exception will originate from the DAPR state store layer, potentially wrapped in `ActorMethodInvocationException`. The `FindConcurrencyConflict` helper handles unwrapping, but the full wrapping behavior cannot be verified until Epic 3. **Cross-reference requirement:** When Story 3.7 is implemented, add integration tests that verify the real DAPR actor exception wrapping is correctly unwrapped by `ConcurrencyConflictExceptionHandler`. Document this as a known fidelity gap in Epic 3's prerequisites.

### Project Structure Notes

**Alignment with Architecture:**
- `ConcurrencyConflictException` goes in `Server/Commands/` -- it's a domain exception thrown by the server-side processing pipeline, alongside other command-related types
- `ConcurrencyConflictExceptionHandler` goes in `CommandApi/ErrorHandling/` -- alongside existing `ValidationExceptionHandler`, `AuthorizationExceptionHandler`, `GlobalExceptionHandler`
- Registration in `AddCommandApi()` per enforcement rule #10
- No new projects or packages required

**Dependency Graph Relevant to This Story:**
```
CommandApi -> Server -> Contracts
CommandApi/ErrorHandling/ConcurrencyConflictExceptionHandler -> Server/Commands/ConcurrencyConflictException
CommandApi/ErrorHandling/ConcurrencyConflictExceptionHandler -> Server/Commands/ICommandStatusStore
Server/Commands/ConcurrencyConflictException -> (no additional deps, just System)
Tests: Server.Tests -> Server (unit testing exception + handler)
Tests: IntegrationTests -> CommandApi (via WebApplicationFactory)
```

**Architecture Reference -- Complete Error Handler Chain After Story 2.8:**
```
Exception occurs in MediatR pipeline or controller
    |
    v
ASP.NET Core ExceptionHandler middleware iterates IExceptionHandler chain:
    |
    |-- 1. ValidationExceptionHandler: handles FluentValidation.ValidationException -> 400 ProblemDetails
    |      (from Story 2.2)
    |
    |-- 2. AuthorizationExceptionHandler: handles CommandAuthorizationException -> 403 ProblemDetails
    |      (from Story 2.5)
    |
    |-- 3. ConcurrencyConflictExceptionHandler: handles ConcurrencyConflictException -> 409 ProblemDetails
    |      (NEW in Story 2.8, writes advisory Rejected status)
    |
    |-- 4. GlobalExceptionHandler: handles ALL remaining exceptions -> 500 ProblemDetails
    |      (from Story 2.2, catch-all)
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.8: Optimistic Concurrency Conflict Handling]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1: Event Storage Strategy -- ETag concurrency on metadata]
- [Source: _bmad-output/planning-artifacts/architecture.md#D5: Error Response Format -- RFC 7807 Problem Details]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines -- Rules #5, #7, #9, #10, #12, #13]
- [Source: _bmad-output/planning-artifacts/architecture.md#Process Patterns -- Error Handling]
- [Source: _bmad-output/planning-artifacts/architecture.md#Data Flow -- Command submission path]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR26 -- Optimistic concurrency conflicts detected and reported]
- [Source: _bmad-output/planning-artifacts/prd.md#FR7 -- Optimistic concurrency conflict rejection]
- [Source: _bmad-output/implementation-artifacts/2-7-command-replay-endpoint.md]
- [Source: _bmad-output/implementation-artifacts/2-6-command-status-tracking-and-query-endpoint.md]
- [Source: _bmad-output/implementation-artifacts/2-5-endpoint-authorization-and-command-rejection.md (referenced)]
- [Source: _bmad-output/implementation-artifacts/2-2-command-validation-and-rfc-7807-error-responses.md (referenced)]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

None -- clean implementation with no debugging issues.

### Completion Notes List

- Task 0: All 12 prerequisites verified (361 existing tests pass). IExceptionHandler chain confirmed in correct order.
- Task 1: Created `ConcurrencyConflictException` in `Server/Commands/` with 4 constructors (parameterless, message-only, message+inner, full domain constructor). Properties: CorrelationId, AggregateId, TenantId, ConflictSource. Default detail message template uses aggregateId.
- Task 2: Created `ConcurrencyConflictExceptionHandler` in `CommandApi/ErrorHandling/` implementing `IExceptionHandler`. Features: InnerException chain walking via `FindConcurrencyConflict` (max depth 10), advisory Rejected status write (rule #12), 409 ProblemDetails with correlationId/aggregateId/tenantId extensions, Retry-After: 1 header, application/problem+json content type.
- Task 3: Registered handler in `AddCommandApi()` between AuthorizationExceptionHandler and GlobalExceptionHandler. Chain: Validation -> Authorization -> ConcurrencyConflict -> Global.
- Task 4: 8 unit tests for ConcurrencyConflictException covering all constructors and properties.
- Task 5: 12 unit tests for ConcurrencyConflictExceptionHandler covering 409 response, ProblemDetails extensions, advisory status write, status write failure tolerance (rule #12), non-concurrency exception passthrough, InnerException unwrapping (1 and 2 levels deep), Retry-After header, structured logging.
- Task 6: 11 integration tests using `ConcurrencyConflictSimulatingHandler` that throws on `commandType: "SimulateConcurrencyConflict"`. Tests verify full HTTP pipeline: 409 response, ProblemDetails format, extensions, status write, retry path (202 after 409), auth enforcement, content type, handler ordering (409 not 500), Retry-After header.
- Task 7: Full regression suite passes (392 total tests, 0 failures). Previous handler chain (validation 400, authorization 403, global 500) unaffected. ReplayController inline 409 independent.

### Change Log

- 2026-02-13: Story 2.8 implementation complete -- ConcurrencyConflictException, ConcurrencyConflictExceptionHandler, DI registration, 31 new tests (8 unit + 12 handler unit + 11 integration). Total test count: 392.

### File List

**New files:**
- `src/Hexalith.EventStore.Server/Commands/ConcurrencyConflictException.cs`
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/ConcurrencyConflictExceptionHandler.cs`
- `tests/Hexalith.EventStore.Server.Tests/Commands/ConcurrencyConflictExceptionTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Commands/ConcurrencyConflictExceptionHandlerTests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/CommandApi/ConcurrencyConflictIntegrationTests.cs`

**Modified files:**
- `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` (added ConcurrencyConflictExceptionHandler registration)

### Code Review Fixes (2026-02-13)

**Review conducted by:** Claude Sonnet 4.5 (adversarial code review workflow)
**Issues found:** 6 High, 3 Medium, 2 Low
**Issues fixed:** 9 (6 High + 3 Medium)

**Fixes applied:**

1. **H1 - Missing null tenantId test:** Added test `TryHandleAsync_NullTenantId_SkipsStatusWrite` verifying 409 response when tenantId is null but status write is skipped (by design).

2. **H2 - Missing depth limit tests:** Added tests `TryHandleAsync_Depth10Nesting_Returns409` (verifies max depth 10 works) and `TryHandleAsync_Depth11Nesting_ReturnsFalse` (verifies depth > 10 returns false as intended).

3. **H3 - Handler registration order not enforced:** Added test `AddCommandApi_RegistersExceptionHandlers_InCorrectOrder` in new file `ServiceCollectionExtensionsTests.cs` that asserts DI registration order: Validation → Authorization → ConcurrencyConflict → Global.

4. **H4 - Missing correlation ID preference documentation:** Added XML comment explaining why handler prefers `httpContext.Items["CorrelationId"]` (API-level tracing) over `conflict.CorrelationId` (fallback for actor-generated conflicts).

5. **H5 - OperationCanceledException blocks 409 response:** Changed exception handling to check `cancellationToken.IsCancellationRequested` before status write, and catch `OperationCanceledException` during write without re-throwing, ensuring 409 response is always sent even if cancellation occurs mid-write.

6. **H6 - Test count verification:** Verified actual test count is 420 total (up from 392 after bulk Stories 2.6-2.9 commit). Story 2.8 now has 35 total tests (31 original + 4 review fixes).

7. **M1 - ConflictSource property unused:** Added documentation clarifying property is reserved for Epic 3 (Story 3.7) and currently unused in v1.

8. **M2 - Thread safety concern:** Verified InMemoryCommandStatusStore already uses `ConcurrentDictionary` (line 14), which is thread-safe. No fix needed.

9. **M3 - Missing structured logging context:** Added `AggregateId` and `TenantId` to status write failure log (line 68-72), providing full diagnostic context for status store failures.

**New test files created:**
- `tests/Hexalith.EventStore.Server.Tests/Extensions/ServiceCollectionExtensionsTests.cs` (1 test)

**Modified files during review:**
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/ConcurrencyConflictExceptionHandler.cs` (documentation + cancellation handling improvements)
- `src/Hexalith.EventStore.Server/Commands/ConcurrencyConflictException.cs` (documentation clarification)
- `tests/Hexalith.EventStore.Server.Tests/Commands/ConcurrencyConflictExceptionHandlerTests.cs` (added 3 tests)

**Review outcome:** Story marked complete after fixes. All 420 tests pass (100% pass rate). Zero regressions.

### Code Review Fixes #2 (2026-02-13)

**Review conducted by:** Claude Opus 4.6 (adversarial code review workflow)
**Issues found:** 3 High, 3 Medium, 1 Low
**Issues fixed:** 6 (3 High + 3 Medium)

**Fixes applied:**

1. **H1 - `FindConcurrencyConflict` doesn't traverse `AggregateException.InnerExceptions`:** Refactored to recursive implementation that traverses both `InnerException` chain AND `AggregateException.InnerExceptions` collection. Added test `TryHandleAsync_AggregateExceptionWithMultipleInners_FindsConflict` verifying conflict found when it's NOT the first inner exception.

2. **H2 - Missing `ArgumentNullException.ThrowIfNull(exception)`:** Added null guard for `exception` parameter in `TryHandleAsync` to match project coding standard.

3. **H3 - `TryHandleAsync_NullTenantId_SkipsStatusWrite` test incomplete:** Enhanced test to also verify `tenantId` is absent from ProblemDetails extensions when tenantId is null.

4. **M1 - `ServiceCollectionExtensionsTests` in wrong test project:** Moved from `Server.Tests/Extensions/` to `IntegrationTests/CommandApi/` with proper `extern alias commandapi` references. Deleted old file and empty directory.

5. **M2 - `FindConcurrencyConflict` visibility too broad:** Changed from `internal static` to `private static` (no tests call it directly).

6. **M3 - `WriteAsJsonAsync` uses cancellationToken risking partial response:** Changed to `CancellationToken.None` for the final 409 ProblemDetails write to ensure complete response delivery.

**L1 (not fixed - documentation only):** Story "Completion Notes" says 392 total tests but review outcome says 420. Stale documentation, no code impact.

**Modified files during review:**
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/ConcurrencyConflictExceptionHandler.cs` (AggregateException traversal, null guard, private visibility, CancellationToken.None)
- `tests/Hexalith.EventStore.Server.Tests/Commands/ConcurrencyConflictExceptionHandlerTests.cs` (1 new test, 1 enhanced test)
- `tests/Hexalith.EventStore.IntegrationTests/CommandApi/ServiceCollectionExtensionsTests.cs` (moved from Server.Tests)

**Deleted files:**
- `tests/Hexalith.EventStore.Server.Tests/Extensions/ServiceCollectionExtensionsTests.cs` (moved to IntegrationTests)

**Review outcome:** Story remains done. All 421 tests pass (100% pass rate). Zero regressions.
