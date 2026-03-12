# Story 17.8: Query Validation Endpoint

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an **API consumer building a client application**,
I want **a REST endpoint (`POST /api/v1/queries/validate`) that performs a pre-flight authorization check for a given tenant, domain, and query type without actually submitting a query**,
so that **client UIs can enable/disable read-operation controls, show authorization warnings, or verify query permissions before attempting query submission, reducing failed 403 responses and improving user experience**.

## Acceptance Criteria

1. **`QueryValidationController`** exists at route `api/v1/queries/validate` with `[ApiController]`, `[Authorize]`, `[Consumes("application/json")]`, and a `POST` action accepting `ValidateQueryRequest` from body
2. **FluentValidation** -- `ValidateQueryRequestValidator` (from Story 17-4) auto-validates the request via `ValidateModelFilter`, returning 400 ProblemDetails on invalid input before any authorization logic runs
3. **Tenant validation** -- controller calls `ITenantValidator.ValidateAsync(user, request.Tenant, cancellationToken)` with the authenticated JWT user principal
4. **RBAC validation** -- controller calls `IRbacValidator.ValidateAsync(user, request.Tenant, request.Domain, request.QueryType, "query", cancellationToken)` -- note: `messageCategory` is `"query"` (not `"command"`)
5. **Optional AggregateId** -- when `request.AggregateId` is provided, it is passed to both validators for fine-grained ACL (future support). For now, the claims-based and actor-based validators may ignore it, but it MUST be available in the controller for forward compatibility (Amendment A2)
6. **200 OK with `PreflightValidationResult`** -- returned in ALL cases (both authorized and unauthorized):
   - Authorized: `{ "isAuthorized": true, "reason": null }`
   - Unauthorized (tenant): `{ "isAuthorized": false, "reason": "Not authorized for tenant 'acme'." }`
   - Unauthorized (RBAC): `{ "isAuthorized": false, "reason": "Not authorized for query type 'GetOrderDetails' in domain 'orders'." }`
7. **HTTP 403** only occurs when the caller's own JWT is invalid or missing -- handled by the `[Authorize]` attribute, NOT by this controller's logic
8. **HTTP 503** -- when a configured actor-based validator is unreachable, `AuthorizationServiceUnavailableException` propagates to the existing `AuthorizationServiceUnavailableHandler` -> 503 with `Retry-After`
9. **No MediatR pipeline** -- the validation endpoint calls validators directly from the controller. It does NOT go through MediatR (no LoggingBehavior, no ValidationBehavior, no AuthorizationBehavior). The MediatR pipeline is for command/query processing, not for thin pre-flight checks.
10. **Structured logging** -- controller logs: pre-flight check received (Debug), authorization result (Debug for pass, Warning for deny with security event), using the same structured logging patterns as `CommandValidationController` and `AuthorizationBehavior`
11. **OpenAPI documentation** -- `QueryValidationController` exposes `ProducesResponseType` attributes for 200, 400, 401, 403, 429, 503
12. **Security** -- `PreflightValidationResult.Reason` contains only human-readable, safe messages from validators. No internal system details (actor names, stack traces, connection strings) are exposed. The controller does NOT append internal context to the reason.
13. **Unit tests** for `QueryValidationController` covering authorized, unauthorized (tenant), unauthorized (RBAC), 503 propagation, null AggregateId, non-null AggregateId, JWT user extraction, and correlationId extraction
14. **All existing Tier 1 and Tier 2 tests continue to pass** with zero behavioral change

## Tasks / Subtasks

- [ ] Task 1: Create `QueryValidationController` (AC: #1, #3, #4, #5, #6, #9, #10, #11)
    - [ ] 1.1 Create `src/Hexalith.EventStore.CommandApi/Controllers/QueryValidationController.cs`
        ```csharp
        namespace Hexalith.EventStore.CommandApi.Controllers;

        using System.Security.Claims;

        using Hexalith.EventStore.CommandApi.Authorization;
        using Hexalith.EventStore.CommandApi.Middleware;
        using Hexalith.EventStore.Contracts.Validation;

        using Microsoft.AspNetCore.Authorization;
        using Microsoft.AspNetCore.Http;
        using Microsoft.AspNetCore.Mvc;

        [ApiController]
        [Authorize]
        [Route("api/v1/queries/validate")]
        [Consumes("application/json")]
        public partial class QueryValidationController(
            ITenantValidator tenantValidator,
            IRbacValidator rbacValidator,
            ILogger<QueryValidationController> logger) : ControllerBase
        {
            [HttpPost]
            [RequestSizeLimit(1_048_576)]
            [ProducesResponseType(typeof(PreflightValidationResult), StatusCodes.Status200OK)]
            [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
            [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
            [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
            public async Task<IActionResult> Validate(
                [FromBody] ValidateQueryRequest request,
                CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(request);

                string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
                    ?? Guid.NewGuid().ToString();

                // Store tenant for rate limiter OnRejected callback
                if (!string.IsNullOrEmpty(request.Tenant))
                {
                    HttpContext.Items["RequestTenantId"] = request.Tenant;
                }

                ClaimsPrincipal user = HttpContext.User;

                // Extract UserId from JWT for logging -- mirror CommandsController pattern
                string userId = User.FindFirst("sub")?.Value ?? "unknown";
                if (userId == "unknown")
                {
                    logger.LogWarning(
                        "JWT 'sub' claim missing for pre-flight query validation. CorrelationId={CorrelationId}.",
                        correlationId);
                }

                Log.PreflightQueryCheckReceived(logger, correlationId, request.Tenant,
                    request.Domain, request.QueryType, request.AggregateId);

                // Tenant validation
                TenantValidationResult tenantResult = await tenantValidator
                    .ValidateAsync(user, request.Tenant, cancellationToken)
                    .ConfigureAwait(false);

                if (!tenantResult.IsAuthorized)
                {
                    Log.PreflightQueryDenied(logger, correlationId, request.Tenant,
                        request.Domain, request.QueryType,
                        tenantResult.Reason ?? "Tenant access denied.", "tenant");

                    return Ok(new PreflightValidationResult(
                        false, tenantResult.Reason ?? "Tenant access denied."));
                }

                // RBAC validation
                RbacValidationResult rbacResult = await rbacValidator
                    .ValidateAsync(user, request.Tenant, request.Domain,
                        request.QueryType, "query", cancellationToken)
                    .ConfigureAwait(false);

                if (!rbacResult.IsAuthorized)
                {
                    Log.PreflightQueryDenied(logger, correlationId, request.Tenant,
                        request.Domain, request.QueryType,
                        rbacResult.Reason ?? "RBAC check failed.", "rbac");

                    return Ok(new PreflightValidationResult(
                        false, rbacResult.Reason ?? "RBAC check failed."));
                }

                Log.PreflightQueryPassed(logger, correlationId, request.Tenant,
                    request.Domain, request.QueryType);

                return Ok(new PreflightValidationResult(true));
            }

            private static partial class Log
            {
                [LoggerMessage(
                    EventId = 1045,
                    Level = LogLevel.Debug,
                    Message = "Pre-flight query validation received: CorrelationId={CorrelationId}, Tenant={Tenant}, Domain={Domain}, QueryType={QueryType}, AggregateId={AggregateId}")]
                public static partial void PreflightQueryCheckReceived(
                    ILogger logger,
                    string correlationId,
                    string tenant,
                    string domain,
                    string queryType,
                    string? aggregateId);

                [LoggerMessage(
                    EventId = 1046,
                    Level = LogLevel.Debug,
                    Message = "Pre-flight query validation passed: CorrelationId={CorrelationId}, Tenant={Tenant}, Domain={Domain}, QueryType={QueryType}")]
                public static partial void PreflightQueryPassed(
                    ILogger logger,
                    string correlationId,
                    string tenant,
                    string domain,
                    string queryType);

                [LoggerMessage(
                    EventId = 1047,
                    Level = LogLevel.Warning,
                    Message = "Pre-flight query validation denied: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, Tenant={Tenant}, Domain={Domain}, QueryType={QueryType}, Reason={Reason}, DeniedBy={DeniedBy}")]
                public static partial void PreflightQueryDenied(
                    ILogger logger,
                    string correlationId,
                    string tenant,
                    string domain,
                    string queryType,
                    string reason,
                    string deniedBy,
                    string securityEvent = "PreflightQueryAuthorizationDenied");
            }
        }
        ```
    - [ ] 1.2 **CRITICAL: Log partial class in SAME FILE.** The `private static partial class Log` block goes inside the same `QueryValidationController.cs` file -- NOT a separate file. The code snippet above shows them as one file with `public partial class QueryValidationController` containing the action method AND the nested `Log` class. `ILogger` and `LoggerMessage` are available via implicit usings (`Microsoft.Extensions.Logging` is globally imported in the CommandApi project).
    - [ ] 1.3 **CRITICAL: No MediatR.** The controller injects `ITenantValidator` + `IRbacValidator` directly. No `IMediator`. The pre-flight check is too thin to warrant a pipeline -- it's just two validator calls.
    - [ ] 1.4 **CRITICAL: Controllers directory already exists.** `src/Hexalith.EventStore.CommandApi/Controllers/` already contains `CommandsController.cs`, `QueriesController.cs`, and `CommandValidationController.cs` (from Story 17-7). Do NOT create a new `Controllers` directory -- just add the new file alongside them.
    - [ ] 1.5 **CRITICAL: Always 200 OK.** Both authorized and unauthorized results return `Ok(new PreflightValidationResult(...))`. Do NOT throw `CommandAuthorizationException` -- that would trigger the 403 exception handler. The pre-flight endpoint answers "are you authorized?" -- even a "no" answer is a successful API response.
    - [ ] 1.6 **AggregateId forward compatibility.** The `ValidateQueryRequest.AggregateId` is `string?` (nullable). Current validators don't use it, but the controller receives it and logs it. When fine-grained ACL is implemented, the validators will accept it via an optional parameter with default value -- non-breaking interface change since validator interfaces are internal (not in Contracts NuGet).
    - [ ] 1.7 **Event IDs.** Use 1045-1047 range for pre-flight query validation log events. These IDs were reserved by Story 17-7 specifically for this story. Avoid collision with existing EventIds: AuthorizationBehavior uses 1020-1029, CommandRouter uses 1010-1019, SubmitCommandHandler uses 1000-1009, CommandValidationController uses 1040-1044.
    - [ ] 1.8 **messageCategory = "query"** -- deliberate distinction from CommandValidationController which uses `"command"`. This allows actor-based RBAC validators (Story 17-2) to enforce different permissions for reads vs writes.

- [ ] Task 2: Verify DI registration -- no changes needed (AC: #2)
    - [ ] 2.1 **Controllers auto-discovered** -- `QueryValidationController` is in the `Hexalith.EventStore.CommandApi` assembly. `AddControllers()` (in `ServiceCollectionExtensions.AddCommandApi()`) auto-discovers all controllers.
    - [ ] 2.2 **FluentValidation auto-discovered** -- `ValidateQueryRequestValidator` is in the `CommandApi` assembly. `AddValidatorsFromAssemblyContaining<SubmitCommandRequestValidator>()` scans the same assembly.
    - [ ] 2.3 **ValidateModelFilter already registered** -- the `ValidateModelFilter` global action filter validates all controller action arguments against their FluentValidation validators. `ValidateQueryRequest` will be validated by `ValidateQueryRequestValidator` automatically.
    - [ ] 2.4 **ITenantValidator + IRbacValidator already registered** -- Story 17-1 factory delegates in `AddCommandApi()` resolve to claims-based or actor-based implementations.
    - [ ] 2.5 **No DI changes needed in ServiceCollectionExtensions.cs.**

- [ ] Task 3: Unit tests for `QueryValidationController` (AC: #13)
    - [ ] 3.1 Create `tests/Hexalith.EventStore.Server.Tests/Controllers/QueryValidationControllerTests.cs`
    - [ ] 3.2 Test: `QueryValidationController_AuthorizedUser_Returns200WithAuthorized` -- both tenant and RBAC pass -> `PreflightValidationResult(true, null)`
    - [ ] 3.3 Test: `QueryValidationController_UnauthorizedTenant_Returns200WithDenied` -- tenant validation fails -> `PreflightValidationResult(false, "Not authorized for tenant...")`
    - [ ] 3.4 Test: `QueryValidationController_UnauthorizedRbac_Returns200WithDenied` -- tenant passes but RBAC fails -> `PreflightValidationResult(false, "RBAC check failed...")`
    - [ ] 3.5 Test: `QueryValidationController_RbacCalledWithQueryCategory` -- verify `IRbacValidator.ValidateAsync` received `messageCategory="query"` using NSubstitute `Received()` assertion
    - [ ] 3.6 Test: `QueryValidationController_NullAggregateId_Succeeds` -- `ValidateQueryRequest` with `AggregateId=null` processes correctly
    - [ ] 3.7 Test: `QueryValidationController_WithAggregateId_Succeeds` -- `ValidateQueryRequest` with `AggregateId="order-123"` processes correctly (logged, validators called)
    - [ ] 3.8 Test: `QueryValidationController_CorrelationIdExtractedFromHttpContext` -- verify correlationId from `HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]` is used
    - [ ] 3.9 Test: `QueryValidationController_TenantStoredInHttpContextForRateLimiter` -- verify `HttpContext.Items["RequestTenantId"]` is set
    - [ ] 3.10 Test: `QueryValidationController_TenantServiceUnavailable_Propagates503` -- when `ITenantValidator` throws `AuthorizationServiceUnavailableException`, the exception propagates (caught by existing exception handler -> 503). Test that the controller does NOT catch it.
    - [ ] 3.11 Test: `QueryValidationController_RbacServiceUnavailable_Propagates503` -- same for `IRbacValidator` throwing `AuthorizationServiceUnavailableException`
    - [ ] 3.12 Test: `QueryValidationController_NullRequest_ThrowsArgumentNullException` -- verify `ArgumentNullException.ThrowIfNull(request)` guard
    - [ ] 3.13 Test: `QueryValidationController_MissingSubClaim_LogsWarning` -- JWT with no `sub` claim triggers warning log. Verify using NSubstitute or by checking that the controller doesn't throw
    - [ ] 3.14 **Use NSubstitute** for `ITenantValidator`, `IRbacValidator` mocks
    - [ ] 3.15 **Use Shouldly** assertions (Server.Tests convention)
    - [ ] 3.16 **Use `DefaultHttpContext`** with `ClaimsPrincipal` for HttpContext -- pattern from `AuthorizationBehaviorTests.cs` and `CommandValidationControllerTests.cs`
    - [ ] 3.17 **Naming:** `QueryValidationController_Scenario_ExpectedResult()`

- [ ] Task 4: Verify zero regression (AC: #14)
    - [ ] 4.1 Run Tier 1 tests: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ tests/Hexalith.EventStore.Client.Tests/ tests/Hexalith.EventStore.Sample.Tests/ tests/Hexalith.EventStore.Testing.Tests/`
    - [ ] 4.2 Run Tier 2 tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/`
    - [ ] 4.3 Verify build succeeds: `dotnet build Hexalith.EventStore.slnx --configuration Release`

## Dev Notes

### Design: Mirror of Story 17-7 (CommandValidationController)

This story is an intentional mirror of Story 17-7. The `QueryValidationController` follows the exact same architecture, patterns, and conventions as `CommandValidationController`. The only differences are:

| Aspect | 17-7 (Command) | 17-8 (Query) |
|--------|----------------|--------------|
| Route | `api/v1/commands/validate` | `api/v1/queries/validate` |
| Request type | `ValidateCommandRequest` | `ValidateQueryRequest` |
| Request field | `CommandType` | `QueryType` |
| messageCategory | `"command"` | `"query"` |
| EventIds | 1040-1042 | 1045-1047 |
| SecurityEvent | `PreflightAuthorizationDenied` | `PreflightQueryAuthorizationDenied` |
| Controller class | `CommandValidationController` | `QueryValidationController` |

All design decisions, error handling flows, and security considerations from Story 17-7 apply identically.

### Design: Direct Validator Calls, NOT MediatR Pipeline

The validation endpoint does NOT use MediatR. Rationale (same as 17-7):

1. **No processing to pipeline:** Pre-flight checks have no side effects (no queries, no events, no state changes). The MediatR pipeline (LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> Handler) is designed for command/query processing.
2. **AuthorizationBehavior conflict:** If the request went through MediatR, the `AuthorizationBehavior` would throw `CommandAuthorizationException` on authorization failure -> 403 ProblemDetails. But the validation endpoint MUST return `200 OK` with `PreflightValidationResult(false, reason)` -- a successful API response indicating "no, you're not authorized."
3. **Simplicity:** Two validator calls + result mapping. No need for a MediatR handler, MediatR request type, or pipeline behavior registration.
4. **Performance:** Fewer allocations, no pipeline traversal. Pre-flight checks should be fast -- clients may call them frequently.

### Design: 200 OK for ALL Results -- Critical Semantics

```text
Pre-flight endpoint:   200 OK { isAuthorized: true }        <- authorized
                       200 OK { isAuthorized: false, ... }  <- not authorized (successful check!)

Query endpoint:        200 OK { ... }                        <- query result
                       403 Forbidden                         <- not authorized (error!)
```

The validation endpoint and the query endpoint have different HTTP semantics for authorization failure:

- **Validation:** "Can I do this?" -> 200 OK always (the answer is in the body)
- **Query:** "Do this." -> 403 Forbidden (the query is rejected)

**Do NOT throw `CommandAuthorizationException`** in the validation controller. That exception is caught by `AuthorizationExceptionHandler` -> 403 ProblemDetails. The validation endpoint must NEVER produce 403 from its own logic.

### Design: Route `api/v1/queries/validate` -- Nested Under Queries

The validation endpoint is nested under the queries route prefix:

```text
POST /api/v1/queries            -> QueriesController (submit)
POST /api/v1/queries/validate   -> QueryValidationController (pre-flight)
```

This is a **separate controller** (not an action on `QueriesController`) because:
- Different dependencies (no `IMediator`, no query routing)
- Different response semantics (200 OK PreflightValidationResult vs 200 OK SubmitQueryResponse)
- Different logging concerns (pre-flight vs processing)
- Clean separation of concerns -- mirrors the Command/CommandValidation controller split

**Route resolution:** ASP.NET Core attribute routing resolves `POST /api/v1/queries` to `QueriesController.Submit()` and `POST /api/v1/queries/validate` to `QueryValidationController.Validate()` without ambiguity because the routes differ (`queries` vs `queries/validate`).

### Error Handling Flow

```text
Valid request + authorized:
  -> 200 OK { isAuthorized: true }

Valid request + unauthorized (tenant):
  -> 200 OK { isAuthorized: false, reason: "Not authorized for tenant 'acme'." }

Valid request + unauthorized (RBAC):
  -> 200 OK { isAuthorized: false, reason: "RBAC check failed." }

Invalid request (bad Tenant format, missing Domain, etc.):
  -> 400 Bad Request (ProblemDetails) -- from ValidateModelFilter + ValidateQueryRequestValidator

Missing/invalid JWT:
  -> 401 Unauthorized -- from [Authorize] attribute

Rate limited:
  -> 429 Too Many Requests -- from GlobalLimiter

Actor-based validator unreachable:
  -> 503 Service Unavailable -- from AuthorizationServiceUnavailableHandler
```

The controller does NOT need its own exception handler. It either:
- Returns `Ok(PreflightValidationResult(...))` for successful checks
- Lets `AuthorizationServiceUnavailableException` propagate for 503
- Lets the existing pipeline handle 400, 401, 429

### `messageCategory` = `"query"` -- Deliberate

Story 17-7 created the command validation endpoint with `messageCategory = "command"`. This query validation endpoint uses `"query"`. This distinction allows actor-based RBAC validators (Story 17-2) to enforce different permissions for reads vs writes. The `ClaimsRbacValidator` currently ignores `messageCategory`, but `ActorRbacValidator` passes it through to the actor, which CAN implement read/write discrimination.

### AggregateId Forward Compatibility (Amendment A2)

`ValidateQueryRequest.AggregateId` is `string?` (nullable, default `null`). Current validators (`ClaimsTenantValidator`, `ClaimsRbacValidator`, `ActorTenantValidator`, `ActorRbacValidator`) do not use it. The controller:
1. Receives it in the request
2. Logs it (for audit trail)
3. Does NOT pass it to validators (they don't accept it yet)

When fine-grained ACL is implemented:
- Validator interfaces will gain an optional `aggregateId` parameter
- This controller will pass `request.AggregateId` to the validators
- No structural change to the controller -- just an additional parameter

### Logging: Event ID Range 1045-1047

```text
1045: PreflightQueryCheckReceived (Debug) -- incoming query validation request
1046: PreflightQueryPassed (Debug) -- query authorization check succeeded
1047: PreflightQueryDenied (Warning) -- query authorization check failed (security event)
```

These IDs were reserved by Story 17-7 for this story. Full EventId allocation:
- 1000-1009: SubmitCommandHandler
- 1010-1019: CommandRouter
- 1020-1029: AuthorizationBehavior
- 1040-1044: CommandValidationController (Story 17-7)
- 1045-1047: QueryValidationController (this story)
- 1048-1049: Reserved for future pre-flight extensions

### Security: Authorization Oracle -- Accepted Risk

The pre-flight endpoint is, by design, an authorization oracle. An authenticated attacker can call `/queries/validate` in a loop across all tenants, domains, and query types to map the authorization model. This is **accepted risk** because:
1. The endpoint's purpose is to tell callers what they can and can't do
2. Rate limiting (`GlobalLimiter`) throttles enumeration speed by tenant
3. JWT authentication (`[Authorize]`) prevents anonymous scanning
4. The endpoint reveals authorization decisions, not authorization *rules*

### Architecture Decisions (ADR Summary)

**ADR-1: No MediatR Pipeline** -- Identical rationale to Story 17-7. The `AuthorizationBehavior` throws `CommandAuthorizationException` -> 403, which conflicts with the 200 OK semantics. Direct validator calls avoid this.

**ADR-2: Separate Controller** -- `QueryValidationController` is separate from `QueriesController` because they have different dependencies (no `IMediator`, no `IQueryRouter`), different HTTP semantics (200 PreflightValidationResult vs 200 SubmitQueryResponse), and different purposes.

**ADR-3: AggregateId Received but Not Forwarded** -- Same as 17-7. Controller receives and logs `AggregateId` but doesn't pass it to validators.

### Validator Lifetime: Scoped -- No Concurrency Concern

`ITenantValidator` and `IRbacValidator` are registered as **scoped** services (factory delegates in `AddCommandApi()`). Each HTTP request gets its own validator instances. No shared state between concurrent validation requests.

### Rate Limiting -- Already Applies

The `GlobalLimiter` applies to all routes except `/health`, `/alive`, `/ready`. The new `/api/v1/queries/validate` route is automatically rate-limited by tenant. No changes needed.

### OpenTelemetry -- Already Applies

OpenTelemetry instrumentation on ASP.NET Core applies to the new controller automatically. No additional instrumentation needed.

### Project Structure Notes

```text
src/Hexalith.EventStore.CommandApi/
+-- Controllers/
|   +-- CommandsController.cs              # EXISTING -- command submission
|   +-- QueriesController.cs               # EXISTING (Story 17-5) -- query submission
|   +-- CommandValidationController.cs     # EXISTING (Story 17-7) -- command pre-flight
|   +-- QueryValidationController.cs       # NEW <- Task 1
+-- Authorization/
|   +-- ITenantValidator.cs                # EXISTING -- injected by controller
|   +-- IRbacValidator.cs                  # EXISTING -- injected by controller
|   +-- TenantValidationResult.cs          # EXISTING -- returned by ITenantValidator
|   +-- RbacValidationResult.cs            # EXISTING -- returned by IRbacValidator
+-- ErrorHandling/
|   +-- AuthorizationServiceUnavailableHandler.cs  # EXISTING -- handles 503
|   +-- AuthorizationExceptionHandler.cs           # EXISTING -- NOT triggered
|   +-- CommandAuthorizationException.cs           # EXISTING -- NOT thrown
+-- Filters/
|   +-- ValidateModelFilter.cs             # EXISTING -- validates ValidateQueryRequest
+-- Validation/
|   +-- ValidateQueryRequestValidator.cs   # EXISTING (Story 17-4) -- auto-discovered
+-- Extensions/
|   +-- ServiceCollectionExtensions.cs     # EXISTING -- no changes needed

tests/Hexalith.EventStore.Server.Tests/
+-- Controllers/
|   +-- CommandValidationControllerTests.cs # EXISTING (Story 17-7) -- pattern reference
|   +-- QueryValidationControllerTests.cs   # NEW <- Task 3
```

### Files to Create

```text
src/Hexalith.EventStore.CommandApi/Controllers/QueryValidationController.cs
tests/Hexalith.EventStore.Server.Tests/Controllers/QueryValidationControllerTests.cs
```

### Files NOT to Modify

- `ServiceCollectionExtensions.cs` -- controller auto-discovered, validators already registered
- `QueriesController.cs` -- query flow unchanged
- `CommandValidationController.cs` -- command validation flow unchanged
- `AuthorizationBehavior.cs` -- not involved in validation endpoint
- `ValidateQueryRequestValidator.cs` -- already created in Story 17-4
- `ValidateModelFilter.cs` -- already handles FluentValidation for all controllers
- Any Story 17-1/17-2/17-3/17-4/17-5/17-6/17-7 files -- all working and tested
- Any existing test files -- purely additive story

### Test Conventions

**Server.Tests:** Shouldly assertions (`.ShouldBe()`, `.ShouldBeNull()`, `.ShouldNotBeNull()`). NSubstitute for mocking. See `CommandValidationControllerTests.cs` for the direct pattern reference (Story 17-7).

**HttpContext mocking:** Use `DefaultHttpContext` with `ClaimsPrincipal` -- set `HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]` for correlationId, set `HttpContext.User` with claims for authentication.

**Naming:** `QueryValidationController_Scenario_ExpectedResult()` (e.g., `QueryValidationController_AuthorizedUser_Returns200WithAuthorized`)

### Controller Test Setup Pattern

```csharp
// Arrange
var tenantValidator = Substitute.For<ITenantValidator>();
var rbacValidator = Substitute.For<IRbacValidator>();
var logger = Substitute.For<ILogger<QueryValidationController>>();

tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), "acme", Arg.Any<CancellationToken>())
    .Returns(TenantValidationResult.Allowed);

rbacValidator.ValidateAsync(
    Arg.Any<ClaimsPrincipal>(), "acme", "orders", "GetOrderDetails", "query",
    Arg.Any<CancellationToken>())
    .Returns(RbacValidationResult.Allowed);

var controller = new QueryValidationController(tenantValidator, rbacValidator, logger);
var httpContext = new DefaultHttpContext();
httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
    [new Claim("sub", "user-1")], "Bearer"));
controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

var request = new ValidateQueryRequest("acme", "orders", "GetOrderDetails");

// Act
IActionResult result = await controller.Validate(request, CancellationToken.None);

// Assert
var okResult = result.ShouldBeOfType<OkObjectResult>();
var validationResult = okResult.Value.ShouldBeOfType<PreflightValidationResult>();
validationResult.IsAuthorized.ShouldBeTrue();
validationResult.Reason.ShouldBeNull();
```

### Previous Story Intelligence

**From Story 17-7 (ready-for-dev -- direct pattern reference):**
- `CommandValidationController` is the exact structural template for this story. Mirror its code, tests, logging, and error handling.
- EventIds 1045-1047 were explicitly reserved for this story's query validation log events.
- Same design decisions apply: no MediatR, always 200 OK, separate controller, direct validator calls.
- Test setup pattern with `DefaultHttpContext` + `ClaimsPrincipal` + NSubstitute is established.

**From Story 17-4 (done):**
- `ValidateQueryRequest` record exists in `Contracts/Validation/` with `Tenant`, `Domain`, `QueryType`, `AggregateId?`.
- `PreflightValidationResult` record exists in `Contracts/Validation/` with `IsAuthorized`, `Reason?` -- shared between command and query validation.
- `ValidateQueryRequestValidator` exists in `CommandApi/Validation/` with full tenant/domain/queryType validation rules, including injection prevention. Auto-discovered via FluentValidation assembly scan.

**From Story 17-1 (done):**
- `ITenantValidator` and `IRbacValidator` interfaces are stable. `ValidateAsync` signatures won't change.
- `TenantValidationResult` and `RbacValidationResult` are simple records with `IsAuthorized` and `Reason`.
- Claims-based validators always return results (never throw for auth denial). Actor-based validators may throw `AuthorizationServiceUnavailableException` if the actor is unreachable.
- `TenantValidationResult.Allowed` and `RbacValidationResult.Allowed` static properties exist for test convenience.

**From Story 17-2 (done):**
- `ActorTenantValidator` and `ActorRbacValidator` throw `AuthorizationServiceUnavailableException` when the DAPR actor is unreachable. The validation endpoint controller lets this propagate to the existing `AuthorizationServiceUnavailableHandler` -> 503.
- Actor-based validators pass `messageCategory` to the actor. This story uses `"query"` to enable read/write discrimination.

**From Story 17-3 (done):**
- `AuthorizationBehavior` calls the same `ITenantValidator` + `IRbacValidator` interfaces. The validation controller mirrors this logic but returns `PreflightValidationResult` instead of throwing `CommandAuthorizationException`.

**From Story 17-5 (review):**
- `QueriesController` is the query submission controller at `api/v1/queries`. The query validation controller is separate (different route, different concerns) but follows the same controller conventions.

### Git Intelligence

Recent commits are documentation-focused (Stories 15-5, 15-6). Epic 17 Stories 17-1 through 17-6 are in working tree but not yet committed to main. Story 17-7 is ready-for-dev. All Server project and CommandApi files are in a stable state. The query validation controller is a greenfield addition -- no existing code to modify.

### Backward Compatibility

- No existing types modified -- purely additive
- No existing endpoint behavior changes
- No NuGet package breaking changes
- Query flow through MediatR pipeline unchanged
- All existing controllers, behaviors, and exception handlers unaffected

### Scope Boundary

**IN scope:** `QueryValidationController` with direct validator calls, structured logging, unit tests.

**OUT of scope (other stories):**
- `POST /api/v1/commands/validate` -> Story 17-7 (done/ready-for-dev)
- `POST /api/v1/queries` -> Story 17-5 (review)
- `IProjectionActor`, `QueryEnvelope`, `QueryResult` -> Story 17-6 (done)
- Fine-grained AggregateId-based ACL -> future story
- Integration/E2E tests -> Story 17-9

### References

- [Source: sprint-change-proposal-2026-03-08-auth-query.md -- Section 4.2 Validation Endpoint Flow, Section 4.3 Story 17-8]
- [Source: sprint-change-proposal-2026-03-08-auth-query.md -- FR51 (pre-flight query authorization), Amendment A1 (messageCategory), Amendment A2 (optional AggregateId)]
- [Source: 17-7-command-validation-endpoint.md -- Direct pattern reference (mirror story)]
- [Source: QueriesController.cs -- Query controller pattern (route, [Authorize], correlationId)]
- [Source: CommandValidationController.cs -- Exact structural template (Story 17-7)]
- [Source: ValidateQueryRequest.cs (Contracts/Validation/) -- API input contract from Story 17-4]
- [Source: PreflightValidationResult.cs (Contracts/Validation/) -- API output contract from Story 17-4]
- [Source: ValidateQueryRequestValidator.cs (CommandApi/Validation/) -- FluentValidation from Story 17-4]
- [Source: ValidateModelFilter.cs (CommandApi/Filters/) -- Global FluentValidation filter for all controllers]
- [Source: ITenantValidator.cs (CommandApi/Authorization/) -- Tenant validation interface]
- [Source: IRbacValidator.cs (CommandApi/Authorization/) -- RBAC validation interface]
- [Source: AuthorizationServiceUnavailableHandler.cs (CommandApi/ErrorHandling/) -- 503 handler for actor failures]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
