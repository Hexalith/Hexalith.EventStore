# Story 17.5: Queries Controller and Query Router

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an **API consumer**,
I want **a REST endpoint (`POST /api/v1/queries`) that accepts query requests, routes them through the MediatR pipeline with full authorization, and forwards them to projection actors via a query router**,
so that **client applications can query aggregate projections with the same authentication, validation, and authorization guarantees as command submissions**.

## Acceptance Criteria

1. **`QueriesController`** exists at route `api/v1/queries` with `[ApiController]`, `[Authorize]`, `[Consumes("application/json")]`, and a `POST` action accepting `SubmitQueryRequest` from body
2. **`SubmitQuery`** MediatR request record exists in `Server/Pipeline/Queries/` with properties: `Tenant`, `Domain`, `AggregateId`, `QueryType`, `Payload` (byte[]), `CorrelationId`, `UserId` — implements `IRequest<SubmitQueryResult>`
3. **`SubmitQueryResult`** record exists with `CorrelationId` (string) and `Payload` (JsonElement) — returned on successful projection read
4. **`SubmitQueryHandler`** MediatR handler routes queries through `IQueryRouter` to the projection actor and returns `SubmitQueryResult`
5. **`IQueryRouter`** interface exists in `Server/Queries/` with `RouteQueryAsync(SubmitQuery, CancellationToken) → QueryRouterResult`
6. **`QueryRouter`** implementation uses `IActorProxyFactory` to create a proxy to `IProjectionActor` (from Story 17-6) via `AggregateIdentity.ActorId` routing — same identity derivation as `CommandRouter`
7. **`AuthorizationBehavior`** extended to also authorize `SubmitQuery` requests — uses `ITenantValidator` + `IRbacValidator` with `messageCategory="query"`
8. **404 Not Found** returned when projection actor does not exist or is not registered — controller handles this at the response level (Amendment A6), NOT by constructing a `SubmitQueryResponse` with error data
9. **200 OK** with `SubmitQueryResponse` body on success (synchronous, unlike 202 Accepted for commands)
10. **FluentValidation validator DI registration** — `SubmitQueryRequestValidator` (from Story 17-4) auto-registered via `AddValidatorsFromAssemblyContaining<SubmitCommandRequestValidator>()`
11. **MediatR pipeline** applies to queries: LoggingBehavior → ValidationBehavior → AuthorizationBehavior → SubmitQueryHandler (same order as commands)
12. **OpenAPI documentation** — `QueriesController` exposes `ProducesResponseType` attributes for 200, 400, 401, 403, 404, 429, 503
13. **Unit tests** for `QueriesController`, `SubmitQueryHandler`, `QueryRouter`, and extended `AuthorizationBehavior`
14. **All existing Tier 1 and Tier 2 tests continue to pass** with zero behavioral change

## Tasks / Subtasks

- [x] Task 1: Create MediatR query pipeline types (AC: #2, #3)
    - [x] 1.1 Create `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs`
    - [x] 1.2 Create `SubmitQueryResult` record in the same file
- [x] Task 2: Create `IQueryRouter` and `QueryRouter` (AC: #5, #6, #8)
    - [x] 2.1 Create `src/Hexalith.EventStore.Server/Queries/IQueryRouter.cs`
    - [x] 2.2 Create `QueryRouterResult.cs`
    - [x] 2.3 Create `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`
    - [x] 2.4 Add structured `Log` messages: `QueryRouting`, `QueryRouted`, `ActorInvocationFailed`, `ProjectionActorNotFound`
- [x] Task 3: Create `SubmitQueryHandler` (AC: #4)
    - [x] 3.1 Create `src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs`
    - [x] 3.2 Implement Handle with QueryNotFoundException on NotFound
    - [x] 3.3 Create `src/Hexalith.EventStore.Server/Queries/QueryNotFoundException.cs`
- [x] Task 4: Create `QueryNotFoundExceptionHandler` (AC: #8)
    - [x] 4.1 Exception in `Server/Queries/`, handler in `CommandApi/ErrorHandling/`
    - [x] 4.2 Create `QueryNotFoundExceptionHandler.cs` — 404 ProblemDetails, no internal details
    - [x] 4.3 Registered in `AddCommandApi()` AFTER auth handlers, BEFORE `GlobalExceptionHandler`
- [x] Task 5: Create `QueriesController` (AC: #1, #9, #12)
    - [x] 5.1 Create controller with `[ApiController]`, `[Authorize]`, `[Route("api/v1/queries")]`, `[Consumes("application/json")]`
    - [x] 5.2 Implement `Submit` action with correlationId, tenant, userId extraction, payload serialization
    - [x] 5.3 Add `ProducesResponseType` attributes for 200, 400, 401, 403, 404, 429, 503
- [x] Task 6: Extend `AuthorizationBehavior` for queries (AC: #7, #11)
    - [x] 6.1 Pattern-match both `SubmitCommand` and `SubmitQuery` into common tuple
    - [x] 6.2 Replace `command.*` references with extracted local variables
    - [x] 6.3 Pass `messageCategory` variable to `rbacValidator.ValidateAsync`
    - [x] 6.4 `CommandAuthorizationException` reused for both (intentional)
    - [x] 6.5 Renamed log field `CommandType` → `MessageType`
    - [x] 6.6 Added `using Hexalith.EventStore.Server.Pipeline.Queries;`
- [x] Task 7: Update DI registration (AC: #10, #11)
    - [x] 7.1 MediatR already scans Server assembly — no change needed
    - [x] 7.2 Registered `IQueryRouter` → `QueryRouter` as scoped in Server `ServiceCollectionExtensions`
    - [x] 7.3 Registered `QueryNotFoundExceptionHandler` in CommandApi `ServiceCollectionExtensions`
    - [x] 7.4 FluentValidation auto-discovers `SubmitQueryRequestValidator` — no change needed
- [x] Task 8: Unit tests for `SubmitQuery` and `SubmitQueryResult` (AC: #13)
    - [x] 8.1 Created `SubmitQueryTests.cs`
    - [x] 8.2 Created `SubmitQueryResultTests.cs` (in same file)
- [x] Task 9: Unit tests for `QueriesController` (AC: #13)
    - [x] 9.1 Created `QueriesControllerTests.cs`
    - [x] 9.2 Test: valid query → 200 OK with payload
    - [x] 9.3 Test: correlationId extraction
    - [x] 9.4 Test: RequestTenantId stored
    - [x] 9.5 Test: userId from JWT sub claim
    - [x] 9.6 Test: null Payload → empty byte array
    - [x] 9.7 NSubstitute + Shouldly
- [x] Task 10: Unit tests for `QueryRouter` (AC: #13)
    - [x] 10.1 Created `QueryRouterTests.cs`
    - [x] 10.2 Test: routes to correct actor via AggregateIdentity.ActorId
    - [x] 10.3 Test: ActorMethodInvocationException → NotFound=true
    - [x] 10.4 Test: correct QueryEnvelope construction
    - [x] 10.5 NSubstitute + Shouldly
- [x] Task 11: Unit tests for extended `AuthorizationBehavior` (AC: #13)
    - [x] 11.1 Updated `AuthorizationBehaviorTests.cs`
    - [x] 11.2 Test: SubmitQuery_ValidUser_PassesThrough
    - [x] 11.3 Test: SubmitQuery_UnauthorizedTenant_ThrowsAuthorizationException
    - [x] 11.4 Test: SubmitQuery_UnauthorizedRbac_ThrowsAuthorizationException
    - [x] 11.5 Test: SubmitQuery_RbacValidatorCalledWithQueryCategory — verified "query" category
    - [x] 11.6 All 14 existing command-path tests pass unchanged
- [x] Task 12: Unit tests for `SubmitQueryHandler` (AC: #13)
    - [x] 12.1 Created `SubmitQueryHandlerTests.cs`
    - [x] 12.2 Test: successful query → correct result
    - [x] 12.3 Test: NotFound → throws QueryNotFoundException
    - [x] 12.4 Test: router exception propagates
- [x] Task 13: Unit tests for `QueryNotFoundExceptionHandler` (AC: #8, #13)
    - [x] 13.1 Created `QueryNotFoundExceptionHandlerTests.cs`
    - [x] 13.2 Test: QueryNotFoundException → 404
    - [x] 13.3 Test: non-QueryNotFoundException → false
    - [x] 13.4 Test: no internal details in response body
- [x] Task 14: Verify zero regression (AC: #14)
    - [x] 14.1 Tier 1: 489 tests passed (176+231+29+53)
    - [x] 14.2 Tier 2: 1065 tests passed (1042 existing + 23 new)
    - [x] 14.3 Existing command flow unchanged

## Dev Notes

### CRITICAL DEPENDENCY: Story 17-6 Must Be Complete First

Per the [sprint change proposal dependency chart](sprint-change-proposal-2026-03-08-auth-query.md — Section 4.4):

```text
Story 17-5 depends on 17-3 (done), 17-4 (in-progress), 17-6 (backlog)
```

Story 17-6 creates:

- `IProjectionActor` interface with `QueryAsync(QueryEnvelope) → QueryResult`
- `QueryEnvelope` and `QueryResult` types
- Actor proxy registration

**If 17-6 is not yet complete** when dev starts on 17-5, the `QueryRouter` implementation cannot compile because it references `IProjectionActor`, `QueryEnvelope`, and `QueryResult`. Options:

- **Preferred:** Complete 17-6 first, then implement 17-5
- **Alternative:** Create `IQueryRouter` interface and `QueryRouter` stub that throws `NotImplementedException`, then flesh out after 17-6

### Design: Synchronous Query vs Asynchronous Command

| Aspect           | Command (`POST /api/v1/commands`)        | Query (`POST /api/v1/queries`)                |
| ---------------- | ---------------------------------------- | --------------------------------------------- |
| HTTP Status      | 202 Accepted                             | 200 OK                                        |
| Response body    | `SubmitCommandResponse(CorrelationId)`   | `SubmitQueryResponse(CorrelationId, Payload)` |
| Response headers | `Location` + `Retry-After`               | None needed                                   |
| Processing       | Fire-and-forget (async actor processing) | Synchronous (wait for projection)             |
| Status tracking  | `ICommandStatusStore` writes "Received"  | None (no status tracking for reads)           |
| Archive          | `ICommandArchiveStore` archives command  | None (reads have no side effects)             |
| Extensions       | Supported (audit trail metadata)         | NOT supported                                 |

### AuthorizationBehavior Extension Pattern

The current behavior uses a type-specific pattern:

```csharp
// CURRENT (only handles commands)
if (request is not SubmitCommand command)
    return await next().ConfigureAwait(false);
// ... uses command.Tenant, command.Domain, command.CommandType, "command"
```

The extension must extract a common "authorizable" tuple:

```csharp
// EXTENDED (handles both commands and queries)
string tenant, domain, messageType, messageCategory;
if (request is SubmitCommand cmd) {
    (tenant, domain, messageType, messageCategory) = (cmd.Tenant, cmd.Domain, cmd.CommandType, "command");
} else if (request is SubmitQuery qry) {
    (tenant, domain, messageType, messageCategory) = (qry.Tenant, qry.Domain, qry.QueryType, "query");
} else {
    return await next().ConfigureAwait(false);
}
// ... rest of method uses tenant, domain, messageType, messageCategory
```

**Do NOT introduce a shared `IAuthorizable` interface** or extract a base type. The pattern matching is simpler and doesn't force coupling between `SubmitCommand` and `SubmitQuery`. If a third authorizable type appears in the future, consider the interface then.

### CommandAuthorizationException for Queries — Intentional

Both commands and queries throw `CommandAuthorizationException` on auth failure. The name says "Command" but the semantics are "any API authorization failure → 403". Renaming to `AuthorizationException` would break existing code. The exception handler (`AuthorizationExceptionHandler`) catches `CommandAuthorizationException` and produces 403 ProblemDetails — this is correct for both commands and queries.

If the naming bothers future maintainers, a rename can be done as a separate refactoring story. Do NOT rename in this story.

### QueriesController — Simpler Than CommandsController

The queries controller is simpler because queries:

- Have no `Extensions` → no `ExtensionMetadataSanitizer` dependency
- Have no status tracking → no `ICommandStatusStore` interaction
- Have no archive → no `ICommandArchiveStore` interaction
- Return 200 OK → no `Location` / `Retry-After` headers
- Have nullable `Payload` → handle null gracefully

```csharp
// QueriesController pseudocode
[HttpPost]
public async Task<IActionResult> Submit([FromBody] SubmitQueryRequest request, CancellationToken ct) {
    ArgumentNullException.ThrowIfNull(request);

    string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
        ?? Guid.NewGuid().ToString();

    if (!string.IsNullOrEmpty(request.Tenant))
        HttpContext.Items["RequestTenantId"] = request.Tenant;

    string userId = User.FindFirst("sub")?.Value ?? "unknown";
    if (userId == "unknown")
        logger.LogWarning("JWT 'sub' claim missing. CorrelationId={CorrelationId}.", correlationId);

    byte[] payloadBytes = request.Payload.HasValue
        ? JsonSerializer.SerializeToUtf8Bytes(request.Payload.Value)
        : [];

    var query = new SubmitQuery(
        Tenant: request.Tenant,
        Domain: request.Domain,
        AggregateId: request.AggregateId,
        QueryType: request.QueryType,
        Payload: payloadBytes,
        CorrelationId: correlationId,
        UserId: userId);

    SubmitQueryResult result = await mediator.Send(query, ct).ConfigureAwait(false);

    return Ok(new SubmitQueryResponse(result.CorrelationId, result.Payload));
}
```

### QueryRouter — Mirrors CommandRouter

```csharp
// QueryRouter pseudocode
public async Task<QueryRouterResult> RouteQueryAsync(SubmitQuery query, CancellationToken ct) {
    var identity = new AggregateIdentity(query.Tenant, query.Domain, query.AggregateId);
    string actorId = identity.ActorId;

    Log.QueryRouting(logger, query.CorrelationId, ...);

    var envelope = new QueryEnvelope(  // Type from Story 17-6
        query.Tenant, query.Domain, query.AggregateId,
        query.QueryType, query.Payload, query.CorrelationId, query.UserId);

    try {
        IProjectionActor proxy = actorProxyFactory.CreateActorProxy<IProjectionActor>(
            new ActorId(actorId),
            nameof(ProjectionActor));  // Actor type name from Story 17-6

        QueryResult result = await proxy.QueryAsync(envelope).ConfigureAwait(false);
        return new QueryRouterResult(Success: true, Payload: result.Payload, NotFound: false);
    }
    catch (ActorMethodInvocationException ex) when (/* actor not found pattern */) {
        Log.ProjectionActorNotFound(logger, ...);
        return new QueryRouterResult(Success: false, Payload: null, NotFound: true);
    }
    catch (Exception ex) {
        Log.ActorInvocationFailed(logger, ex, ...);
        throw;
    }
}
```

**Actor not found detection:** When DAPR cannot find an actor registration, it throws `Dapr.Actors.ActorMethodInvocationException` or a similar exception. The exact exception type depends on the DAPR SDK version (1.17.0). Check DAPR SDK source for the correct exception hierarchy. If no specific "not found" exception exists, catch the general actor invocation exception and check the message/inner exception for "actor type not registered" patterns.

### QueryNotFoundException — New Exception Type

```csharp
// QueryNotFoundException.cs
namespace Hexalith.EventStore.Server.Queries;

public class QueryNotFoundException : Exception {
    public QueryNotFoundException(string tenant, string domain, string aggregateId, string queryType)
        : base($"No projection found for {tenant}:{domain}:{aggregateId} (query type: {queryType})") {
        Tenant = tenant;
        Domain = domain;
        AggregateId = aggregateId;
        QueryType = queryType;
    }

    // Standard constructors for serialization
    public QueryNotFoundException() : base() { ... }
    public QueryNotFoundException(string message) : base(message) { ... }
    public QueryNotFoundException(string message, Exception inner) : base(message, inner) { ... }

    public string Tenant { get; }
    public string Domain { get; }
    public string AggregateId { get; }
    public string QueryType { get; }
}
```

### QueryNotFoundExceptionHandler — 404 Pattern

```csharp
// Handler returns 404 ProblemDetails
// SECURITY: Do NOT include tenant, domain, aggregateId, or actor ID in response body
var problemDetails = new ProblemDetails {
    Status = StatusCodes.Status404NotFound,
    Title = "Not Found",
    Type = "https://tools.ietf.org/html/rfc9457#section-3",
    Detail = "No projection found for the requested aggregate.",
    Instance = httpContext.Request.Path,
    Extensions = { ["correlationId"] = correlationId },
};
```

### Exception Handler Registration Order

```csharp
// In ServiceCollectionExtensions.AddCommandApi()
_ = services.AddExceptionHandler<ValidationExceptionHandler>();         // 400
_ = services.AddExceptionHandler<AuthorizationServiceUnavailableHandler>();  // 503 (before 403)
_ = services.AddExceptionHandler<AuthorizationExceptionHandler>();           // 403
_ = services.AddExceptionHandler<ConcurrencyConflictExceptionHandler>();     // 409
_ = services.AddExceptionHandler<QueryNotFoundExceptionHandler>();           // 404 (NEW)
_ = services.AddExceptionHandler<GlobalExceptionHandler>();                  // 500 (last)
```

### Payload Serialization: `JsonElement?` → `byte[]` → `JsonElement`

The query payload traverses three representations:

1. **API input:** `SubmitQueryRequest.Payload` is `JsonElement?` (nullable — queries may have no parameters)
2. **Pipeline internal:** `SubmitQuery.Payload` is `byte[]` — serialized for transport to actor (mirrors `SubmitCommand.Payload: byte[]`)
3. **API output:** `SubmitQueryResult.Payload` is `JsonElement` — returned from projection actor via `QueryResult.Payload` (Story 17-6)

Serialization in controller:

```csharp
byte[] payloadBytes = request.Payload.HasValue
    ? JsonSerializer.SerializeToUtf8Bytes(request.Payload.Value)
    : [];  // Empty byte array for parameterless queries
```

The `SubmitQueryResult.Payload` is already `JsonElement` (deserialized by actor proxy / QueryRouter from `QueryResult`). The controller passes it directly to `SubmitQueryResponse`.

### DI Registration — What Changes

```csharp
// NEW registrations needed in AddCommandApi():
_ = services.AddScoped<IQueryRouter, QueryRouter>();
_ = services.AddExceptionHandler<QueryNotFoundExceptionHandler>();

// NO changes needed for:
// - MediatR: SubmitQueryHandler is in Server assembly, already scanned
// - FluentValidation: SubmitQueryRequestValidator is in CommandApi assembly, already scanned
// - AuthorizationBehavior: Already registered as open generic, applies to all IRequest types
// - LoggingBehavior + ValidationBehavior: Already registered as open generics
```

### LoggingBehavior — Already Works for Queries

`LoggingBehavior<TRequest, TResponse>` is an open generic behavior. It logs `typeof(TRequest).Name` which will show `SubmitQuery` automatically. No changes needed.

### ValidationBehavior — Already Works for Queries

`ValidationBehavior<TRequest, TResponse>` looks up `IValidator<TRequest>`. For `SubmitQuery`, there's no `IValidator<SubmitQuery>` registered (validators are for API DTOs: `SubmitQueryRequest`). However, the `ValidateModelFilter` on the controller pipeline validates the API DTO before MediatR. **Verify:** Does `ValidationBehavior` gracefully skip when no validator exists for the request type? Check the implementation — it likely injects `IEnumerable<IValidator<TRequest>>` and skips if empty. If it throws on empty validators, a no-op validator or a skip check is needed.

### Null Payload Edge Cases

| Scenario                                     | `SubmitQueryRequest.Payload`              | `SubmitQuery.Payload`   | Notes                                        |
| -------------------------------------------- | ----------------------------------------- | ----------------------- | -------------------------------------------- |
| Parameterless query (e.g., GetCurrentState)  | `null`                                    | `[]` (empty byte array) | Valid — FluentValidation allows null Payload |
| Query with parameters (e.g., SearchByFilter) | `JsonElement` (Object)                    | Serialized bytes        | Standard path                                |
| Undefined ValueKind                          | Rejected by `SubmitQueryRequestValidator` | N/A                     | 400 Bad Request before reaching handler      |

### Rate Limiting — Already Applies

The `GlobalLimiter` in `ServiceCollectionExtensions` applies to all routes except `/health`, `/alive`, `/ready`. The new `/api/v1/queries` route is automatically rate-limited by tenant. No changes needed.

### OpenTelemetry — Already Applies

OpenTelemetry instrumentation on ASP.NET Core and MediatR applies to the new controller and pipeline automatically. No additional instrumentation needed.

### Project Structure Notes

```text
src/Hexalith.EventStore.Server/
├── Commands/
│   ├── ICommandRouter.cs              # EXISTING — pattern reference
│   └── CommandRouter.cs               # EXISTING — pattern reference
├── Queries/                           # NEW directory
│   ├── IQueryRouter.cs                # NEW
│   ├── QueryRouter.cs                 # NEW
│   ├── QueryRouterResult.cs           # NEW
│   └── QueryNotFoundException.cs      # NEW
├── Pipeline/
│   ├── Commands/
│   │   └── SubmitCommand.cs           # EXISTING — pattern reference
│   ├── Queries/                       # NEW directory
│   │   └── SubmitQuery.cs             # NEW (includes SubmitQueryResult)
│   ├── SubmitCommandHandler.cs        # EXISTING — pattern reference
│   └── SubmitQueryHandler.cs          # NEW

src/Hexalith.EventStore.CommandApi/
├── Controllers/
│   ├── CommandsController.cs          # EXISTING — pattern reference
│   └── QueriesController.cs           # NEW
├── ErrorHandling/
│   └── QueryNotFoundExceptionHandler.cs  # NEW
├── Pipeline/
│   └── AuthorizationBehavior.cs       # MODIFY — extend for SubmitQuery
├── Extensions/
│   └── ServiceCollectionExtensions.cs # MODIFY — register IQueryRouter, exception handler

tests/Hexalith.EventStore.Server.Tests/
├── Pipeline/
│   ├── AuthorizationBehaviorTests.cs  # MODIFY — add query authorization tests
│   └── Queries/                       # NEW directory
│       ├── SubmitQueryTests.cs        # NEW
│       ├── SubmitQueryResultTests.cs  # NEW (or in same file)
│       └── SubmitQueryHandlerTests.cs # NEW
├── Queries/                           # NEW directory
│   └── QueryRouterTests.cs           # NEW
├── Controllers/
│   └── QueriesControllerTests.cs     # NEW
├── ErrorHandling/
│   └── QueryNotFoundExceptionHandlerTests.cs  # NEW
```

### Files to Create

```text
src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs
src/Hexalith.EventStore.Server/Queries/IQueryRouter.cs
src/Hexalith.EventStore.Server/Queries/QueryRouter.cs
src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs
src/Hexalith.EventStore.Server/Queries/QueryNotFoundException.cs
src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs
src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs
src/Hexalith.EventStore.CommandApi/ErrorHandling/QueryNotFoundExceptionHandler.cs
tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryTests.cs
tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs
tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs
tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs
tests/Hexalith.EventStore.Server.Tests/ErrorHandling/QueryNotFoundExceptionHandlerTests.cs
```

### Files to Modify

```text
src/Hexalith.EventStore.CommandApi/Pipeline/AuthorizationBehavior.cs — extend for SubmitQuery
src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs — register IQueryRouter, exception handler
tests/Hexalith.EventStore.Server.Tests/Pipeline/AuthorizationBehaviorTests.cs — add query authorization tests
```

### Files NOT to Modify

- `CommandsController.cs` — command flow unchanged
- `SubmitCommandRequestValidator.cs` — no refactoring of shared patterns
- `SubmitQueryRequestValidator.cs` — already created in Story 17-4, validators auto-discovered
- `SubmitQueryRequest.cs` / `SubmitQueryResponse.cs` — contracts from Story 17-4, used as-is
- Any Story 17-1/17-2/17-3 files — all working and tested
- `CommandRouter.cs` — command routing unchanged
- `LoggingBehavior.cs` / `ValidationBehavior.cs` — open generics, work automatically

### Test Conventions

**Server.Tests:** Shouldly assertions (`.ShouldBe()`, `.ShouldBeNull()`, `.ShouldNotBeEmpty()`). NSubstitute for mocking. See `AuthorizationBehaviorTests.cs` for reference.

**Naming:** `ClassName_Scenario_ExpectedResult()` (e.g., `QueriesController_ValidRequest_Returns200WithPayload`)

**HttpContext mocking:** Use `DefaultHttpContext` with `ClaimsPrincipal` — pattern established in `AuthorizationBehaviorTests.cs` and `CommandsControllerTenantTests.cs`.

### Previous Story Intelligence

**From Story 17-3 (done):**

- `AuthorizationBehavior` now injects `ITenantValidator` + `IRbacValidator` via constructor. The behavior calls validators with `command.Tenant`, `command.Domain`, `command.CommandType`, `"command"`. Extending to queries means adding a parallel code path with `query.Tenant`, `query.Domain`, `query.QueryType`, `"query"`.
- All 941 Server.Tests pass. The authorization pipeline is stable.
- `AuthorizedTenant` HttpContext item was removed — queries don't need it either.
- `SecurityAuditLoggingTests.cs` was updated for the new constructor — check if query-path changes require further updates.

**From Story 17-4 (in-progress):**

- `SubmitQueryRequest` and `SubmitQueryResponse` already exist in `Contracts/Queries/`.
- `SubmitQueryRequestValidator` exists in `CommandApi/Validation/` with same regex patterns as `SubmitCommandRequestValidator`.
- `PreflightValidationResult` exists in `Contracts/Validation/` — used by Stories 17-7/17-8, NOT this story.
- FluentValidation validators auto-discover from assembly scan — no manual registration needed.

**From Story 17-1 (done):**

- `ITenantValidator` and `IRbacValidator` interfaces already support `messageCategory` parameter.
- `ClaimsRbacValidator` treats `"command"` and `"query"` identically (same permission check).
- Actor-based validators use `messageCategory` to distinguish — query authorization may differ from command authorization at the actor level.

**From Story 17-2 (done):**

- `ActorRbacValidator` passes `messageCategory` to actor via `RbacValidationRequest.MessageCategory`.
- `AuthorizationServiceUnavailableException` propagates from actor validators through behavior to handler → 503. This flow also applies to query authorization when actor-based auth is configured.

### Git Intelligence

Recent commits are documentation-focused (Stories 15-5, 15-6). Epic 17 Stories 17-1 through 17-4 are in the working tree but not yet committed to main. All Server project files are in a stable state. The query pipeline is greenfield — no existing code to refactor (except the `AuthorizationBehavior` extension).

### Backward Compatibility

- No existing types modified (except `AuthorizationBehavior` extension — additive change)
- No existing endpoint behavior changes
- No NuGet package breaking changes
- Command flow through MediatR pipeline unchanged — the `is SubmitCommand` branch stays the same
- `is not SubmitCommand` passthrough now also checks `is SubmitQuery` — all other request types still pass through

### Scope Boundary

**IN scope:** `QueriesController`, `SubmitQuery` MediatR types, `IQueryRouter` + `QueryRouter`, `SubmitQueryHandler`, `QueryNotFoundException` + handler, `AuthorizationBehavior` extension, DI registration, unit tests.

**OUT of scope (later stories):**

- `IProjectionActor`, `QueryEnvelope`, `QueryResult` types → Story 17-6
- `POST /api/v1/commands/validate` → Story 17-7
- `POST /api/v1/queries/validate` → Story 17-8
- Integration/E2E tests → Story 17-9

### References

- [Source: sprint-change-proposal-2026-03-08-auth-query.md — Section 4.2 Query Processing Flow, Section 4.3 Story 17-5]
- [Source: sprint-change-proposal-2026-03-08-auth-query.md — Amendment A6 (404 for missing projection actors)]
- [Source: CommandsController.cs — Controller pattern (route, [Authorize], MediatR send)]
- [Source: CommandRouter.cs — Routing pattern (AggregateIdentity, IActorProxyFactory, actor proxy)]
- [Source: SubmitCommand.cs — MediatR request record pattern]
- [Source: SubmitCommandHandler.cs — MediatR handler pattern]
- [Source: AuthorizationBehavior.cs — Current implementation (SubmitCommand only)]
- [Source: ServiceCollectionExtensions.cs — DI registration patterns, handler ordering]
- [Source: SubmitQueryRequest.cs (Contracts/Queries/) — API input contract from Story 17-4]
- [Source: SubmitQueryResponse.cs (Contracts/Queries/) — API output contract from Story 17-4]
- [Source: SubmitQueryRequestValidator.cs (CommandApi/Validation/) — FluentValidation from Story 17-4]
- [Source: 17-3-refactor-authorization-behavior.md — Behavior refactoring, validator injection, pipeline ordering]
- [Source: 17-4-query-contracts.md — Query/validation contracts, Payload semantics, validator patterns]

## Change Log

- **2026-03-12:** Implemented Story 17-5 — QueriesController, QueryRouter, SubmitQueryHandler, QueryNotFoundExceptionHandler, AuthorizationBehavior extension for queries, DI registration, and 23 unit tests. Also implemented prerequisite Story 17-6 (IProjectionActor, QueryEnvelope, QueryResult, FakeProjectionActor) with 29 tests.
- **2026-03-12:** Senior Developer Review (AI) fixes — tightened QueryRouter not-found detection to avoid masking actor failures as 404s, handled unsuccessful/empty query results explicitly in SubmitQueryHandler, and added regression tests for both paths.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- AuthorizationBehavior log field renamed `CommandType` → `MessageType` to support both commands and queries
- QueryRouter uses `public const string ProjectionActorTypeName = "ProjectionActor"` instead of `nameof(ProjectionActor)` since `ProjectionActor` is application-provided
- IQueryRouter registered as scoped in Server's `EventStoreServerServiceCollectionExtensions` (alongside ICommandRouter) rather than CommandApi's `AddCommandApi()` — follows existing router registration pattern

### Completion Notes List

- All 14 acceptance criteria satisfied
- 23 new tests added for Story 17-5; 29 new tests for prerequisite Story 17-6
- Original implementation reported total suite: 1554 tests passing (489 Tier 1 + 1065 Tier 2), zero regressions
- Pre-existing IntegrationTests (Tier 3) build errors unrelated to this story
- Senior Developer Review (AI) fixed two runtime issues: over-broad 404 mapping for actor invocation failures and missing guards for unsuccessful/empty projection results
- Review noted additional unrelated working-tree changes outside Story 17-5; they were not folded into this story's implementation file list
- Senior Developer Review (AI) re-ran the focused query-path suite after fixes: 36 tests passed

### File List

**Created (Story 17-6 prerequisite):**

- src/Hexalith.EventStore.Server/Actors/IProjectionActor.cs
- src/Hexalith.EventStore.Server/Actors/QueryEnvelope.cs
- src/Hexalith.EventStore.Server/Actors/QueryResult.cs
- src/Hexalith.EventStore.Testing/Fakes/FakeProjectionActor.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/QueryEnvelopeTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/QueryResultTests.cs
- tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeProjectionActorTests.cs

**Created (Story 17-5):**

- src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs
- src/Hexalith.EventStore.Server/Queries/IQueryRouter.cs
- src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs
- src/Hexalith.EventStore.Server/Queries/QueryRouter.cs
- src/Hexalith.EventStore.Server/Queries/QueryNotFoundException.cs
- src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs
- src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs
- src/Hexalith.EventStore.CommandApi/ErrorHandling/QueryNotFoundExceptionHandler.cs
- tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryTests.cs
- tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs
- tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs
- tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs
- tests/Hexalith.EventStore.Server.Tests/ErrorHandling/QueryNotFoundExceptionHandlerTests.cs

**Modified:**

- src/Hexalith.EventStore.CommandApi/Pipeline/AuthorizationBehavior.cs — extended for SubmitQuery
- src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs — registered QueryNotFoundExceptionHandler
- src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs — registered IQueryRouter → QueryRouter
- src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs — hardened failure handling for unsuccessful and empty projection results
- src/Hexalith.EventStore.Server/Queries/QueryRouter.cs — narrowed 404 detection to actor-not-found cases and preserved genuine actor failures
- tests/Hexalith.EventStore.Server.Tests/Pipeline/AuthorizationBehaviorTests.cs — added 4 query authorization tests
- tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs — added regression tests for router failure and empty payload handling
- tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs — added regression tests for actor failure propagation and unsuccessful query results

## Senior Developer Review (AI)

### Reviewer

Jerome (GitHub Copilot, GPT-5.4) on 2026-03-12

### Outcome

Approved after fixes.

### Review Notes

- Fixed a high-severity bug where `QueryRouter` mapped any `ActorMethodInvocationException` to `404 Not Found`; it now only maps recognized actor-not-found patterns and rethrows genuine actor failures.
- Fixed a high-severity bug where unsuccessful projection responses could be treated as success and where `SubmitQueryHandler` could dereference a null payload.
- Added regression tests covering actor failure propagation, actor-returned unsuccessful query results, router failure handling, and empty payload guards.
- Observed unrelated working-tree changes outside Story 17-5 during review; these were intentionally left outside this story's implementation list.
