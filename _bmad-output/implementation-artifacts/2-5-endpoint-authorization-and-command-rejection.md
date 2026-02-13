# Story 2.5: Endpoint Authorization & Command Rejection

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an **API consumer**,
I want the system to authorize my command submissions based on JWT claims for tenant, domain, and command type, rejecting unauthorized commands at the API gateway before processing,
So that the system enforces access control at the perimeter (FR31, FR32).

## Acceptance Criteria

1. **403 for unauthorized tenant (pre-pipeline)** - Given I am authenticated with a valid JWT, When I submit a command for a tenant not in my authorized tenants (`eventstore:tenant` claims), Then the response is `403 Forbidden` as RFC 7807 ProblemDetails with `tenantId` extension and human-readable `detail` message, And the command is rejected before entering the MediatR pipeline.

2. **403 for unauthorized domain or command type (in-pipeline)** - When I submit a command for a domain not in my authorized domains (`eventstore:domain` claims) or a command type not in my authorized permissions (`eventstore:permission` claims), Then the response is `403 Forbidden` as RFC 7807 ProblemDetails with appropriate detail message, And AuthorizationBehavior in the MediatR pipeline enforces claims-based ABAC.

3. **AuthorizationBehavior pipeline position** - AuthorizationBehavior is the third behavior in the MediatR pipeline, after ValidationBehavior and before CommandHandler. Pipeline order: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> CommandHandler (enforcement rule #3).

4. **Failed authorization logging** - When authorization fails at either level, Then the failure is logged at `Warning` level with: correlationId, tenantId, domain, commandType, failure reason, source IP. The JWT token itself MUST NOT appear in any log output (NFR11). Event payload data MUST NOT be logged (SEC-5, NFR12).

5. **Wildcard and absent-claim semantics** - If the authenticated user has no `eventstore:domain` claims, all domains are authorized. If the user has no `eventstore:permission` claims, all command types are authorized. The `eventstore:tenant` claim is always required — a request with no tenant claims is rejected with 403 for any command.

## Prerequisites

**BLOCKING: Story 2.4 (JWT Authentication & Claims Transformation) MUST be complete before starting Story 2.5.** Story 2.5 depends on:
- `[Authorize]` attribute on `CommandsController` (provides authentication gate)
- `EventStoreClaimsTransformation` (provides `eventstore:tenant`, `eventstore:domain`, `eventstore:permission` normalized claims)
- `TestJwtTokenGenerator` (required for all integration tests)
- `WebApplicationFactory` JWT configuration (symmetric key for test auth)

**Before beginning any Task below, verify:** Run existing tests to confirm Story 2.4 artifacts are in place. If `[Authorize]` is not on the controller or `EventStoreClaimsTransformation` does not exist, Story 2.4 is not complete — do NOT proceed.

## Tasks / Subtasks

- [x] Task 0: Verify Story 2.4 prerequisites are in place (BLOCKING)
  - [x] 0.1 Confirm `[Authorize]` attribute exists on `CommandsController`
  - [x] 0.2 Confirm `EventStoreClaimsTransformation` implements `IClaimsTransformation` and is registered
  - [x] 0.3 Confirm `TestJwtTokenGenerator` exists in `IntegrationTests/Helpers/`
  - [x] 0.4 Run all existing tests — they must pass before proceeding

- [x] Task 1: Implement pre-pipeline tenant authorization in CommandsController (AC: #1, #4)
  - [x] 1.1 In `CommandsController.Submit()`, BEFORE calling `_mediator.Send()`: first guard against null `HttpContext` / `User` (return 403 immediately if null — defensive against misconfigured middleware), then verify `User.Identity?.IsAuthenticated == true` (defensive belt-and-suspenders check beyond `[Authorize]`), then check if `User` has an `eventstore:tenant` claim matching `request.Tenant` (case-insensitive comparison). Filter claims with `.Where(c => !string.IsNullOrWhiteSpace(c.Value))` to reject empty/whitespace-only claim values
  - [x] 1.2 If no matching tenant claim found, return 403 ProblemDetails immediately with: `Status = 403`, `Title = "Forbidden"`, `Type = "https://tools.ietf.org/html/rfc9457#section-3"`, `Detail = "Not authorized to submit commands for tenant '{tenant}'."`, `Instance = request path`, `Extensions = { correlationId, tenantId }`
  - [x] 1.3 If user has zero `eventstore:tenant` claims at all, return 403 with `Detail = "No tenant authorization claims found. Access denied."`
  - [x] 1.4 Log failed tenant authorization at `Warning` level: correlationId, tenantId (attempted), source IP (`HttpContext.Connection.RemoteIpAddress`), commandType, domain. NEVER log the JWT token (NFR11)
  - [x] 1.5 Store the authorized tenant in `HttpContext.Items["AuthorizedTenant"]` for downstream use after successful check
  - [x] 1.6 Ensure `ConfigureAwait(false)` on all async calls (CA2007), `ArgumentNullException.ThrowIfNull()` on public methods (CA1062)

- [x] Task 2: Create AuthorizationBehavior<TRequest, TResponse> MediatR behavior (AC: #2, #3, #4, #5)
  - [x] 2.1 Create `AuthorizationBehavior.cs` in `CommandApi/Pipeline/` implementing `IPipelineBehavior<TRequest, TResponse>`
  - [x] 2.2 Inject `IHttpContextAccessor` and `ILogger<AuthorizationBehavior<TRequest, TResponse>>`
  - [x] 2.3 Guard against null `HttpContext` or `HttpContext.User` — throw `InvalidOperationException` if null (indicates middleware misconfiguration, not a user error). Also verify `User.Identity?.IsAuthenticated == true` as defensive check
  - [x] 2.4 If `TRequest` is `SubmitCommand`, extract `Domain` and `CommandType` from the command
  - [x] 2.5 **Domain authorization**: If user has any `eventstore:domain` claims (after filtering with `.Where(c => !string.IsNullOrWhiteSpace(c.Value))`), verify command's `Domain` matches one of them (case-insensitive). If no `eventstore:domain` claims exist, skip domain check (all domains authorized per AC #5). Note: tenant and domain authorization are independent — they are NOT checked as a pair (having tenant A and domain X does not mean "tenant A with domain X"; it means "any authorized tenant" + "any authorized domain")
  - [x] 2.6 **Command type authorization**: If user has any `eventstore:permission` claims (after filtering empty/whitespace), verify command's `CommandType` matches one (case-insensitive). Support wildcard via `AuthorizationConstants.WildcardPermission` constant (`"commands:*"`) matching any command type. If no `eventstore:permission` claims exist, skip command type check (all types authorized per AC #5)
  - [x] 2.7 On authorization failure, throw `CommandAuthorizationException` with tenant, domain, commandType, and reason
  - [x] 2.8 Log failed authorization at `Warning` level: correlationId, tenant, domain, commandType, failureReason. NEVER log JWT token or payload. On successful authorization, log at `Debug` level: correlationId, tenant, domain, commandType (useful for troubleshooting authorization flow without polluting production logs)
  - [x] 2.9 For non-`SubmitCommand` request types, pass through without authorization checks (only command submissions are authorized)

- [x] Task 3: Create CommandAuthorizationException and exception handler (AC: #1, #2)
  - [x] 3.1 Create `CommandAuthorizationException` in `CommandApi/ErrorHandling/` with properties: `TenantId` (string), `Domain` (string?), `CommandType` (string?), `Reason` (string)
  - [x] 3.2 Create `AuthorizationExceptionHandler` implementing `IExceptionHandler` that handles `CommandAuthorizationException`
  - [x] 3.3 Map to 403 ProblemDetails with: `Status = 403`, `Title = "Forbidden"`, `Type = "https://tools.ietf.org/html/rfc9457#section-3"`, `Detail` = human-readable message from exception `Reason`, `Instance` = request path, `Extensions = { correlationId, tenantId }`
  - [x] 3.4 Set response content type to `application/problem+json`
  - [x] 3.5 Register `AuthorizationExceptionHandler` in `AddCommandApi()` via `services.AddExceptionHandler<AuthorizationExceptionHandler>()` — register BEFORE `GlobalExceptionHandler` so it takes priority for auth exceptions

- [x] Task 4: Register AuthorizationBehavior in pipeline (AC: #3)
  - [x] 4.1 In `ServiceCollectionExtensions.AddCommandApi()`, add `cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>))` AFTER `ValidationBehavior` and BEFORE `SubmitCommandHandler`. Pipeline registration order: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior
  - [x] 4.2 Verify `IHttpContextAccessor` is already registered (should be from Story 2.3)

- [x] Task 5: Write unit tests for AuthorizationBehavior (AC: #2, #3, #4, #5)
  - [x] 5.1 `AuthorizationBehavior_UserWithMatchingDomain_Succeeds` — verify command passes when user has matching `eventstore:domain` claim
  - [x] 5.2 `AuthorizationBehavior_UserWithNoDomainClaims_Succeeds` — verify all domains allowed when no domain claims present (AC #5)
  - [x] 5.3 `AuthorizationBehavior_UserWithWrongDomain_ThrowsAuthorizationException` — verify `CommandAuthorizationException` thrown
  - [x] 5.4 `AuthorizationBehavior_UserWithMatchingPermission_Succeeds` — verify command passes with matching permission
  - [x] 5.5 `AuthorizationBehavior_UserWithNoPermissionClaims_Succeeds` — verify all command types allowed when no permission claims (AC #5)
  - [x] 5.6 `AuthorizationBehavior_UserWithWrongPermission_ThrowsAuthorizationException` — verify exception thrown
  - [x] 5.7 `AuthorizationBehavior_UserWithWildcardPermission_Succeeds` — verify `commands:*` matches any command type
  - [x] 5.8 `AuthorizationBehavior_NonSubmitCommandRequest_PassesThrough` — verify non-SubmitCommand types skip authorization
  - [x] 5.9 `AuthorizationBehavior_CaseInsensitiveDomainMatch_Succeeds` — verify case-insensitive domain matching
  - [x] 5.10 `AuthorizationBehavior_FailedAuth_LogsWarningWithoutJwtToken` — verify structured log with correlation ID, no JWT token

- [x] Task 6: Write unit tests for AuthorizationExceptionHandler and CommandAuthorizationException (AC: #1, #2)
  - [x] 6.1 `AuthorizationExceptionHandler_HandlesCommandAuthorizationException_Returns403ProblemDetails` — verify handler produces 403 ProblemDetails with correct structure (status, title, type, detail, correlationId, tenantId extensions)
  - [x] 6.2 `AuthorizationExceptionHandler_IgnoresOtherExceptions_ReturnsFalse` — verify handler returns `false` for non-`CommandAuthorizationException` types (e.g., `InvalidOperationException`)
  - [x] 6.3 `AuthorizationExceptionHandler_SetsContentType_ApplicationProblemJson` — verify response content type is `application/problem+json`
  - [x] 6.4 `CommandAuthorizationException_Properties_SetCorrectly` — verify TenantId, Domain, CommandType, Reason properties are set and accessible
  - [x] 6.5 `CommandAuthorizationException_ToString_DoesNotLeakSensitiveData` — verify `ToString()` / `Message` contains only tenant, domain, commandType, reason — NOT JWT token content or event payload

- [x] Task 7: Write integration tests for authorization flow (AC: #1, #2, #4, #5)
  - [x] 7.1 `PostCommands_TenantNotInClaims_Returns403ProblemDetails` — verify 403 with `tenantId` extension for unauthorized tenant
  - [x] 7.2 `PostCommands_NoTenantClaims_Returns403ProblemDetails` — verify 403 when JWT has zero tenant claims
  - [x] 7.3 `PostCommands_DomainNotInClaims_Returns403ProblemDetails` — verify 403 for unauthorized domain (when domain claims exist)
  - [x] 7.4 `PostCommands_CommandTypeNotInClaims_Returns403ProblemDetails` — verify 403 for unauthorized command type (when permission claims exist)
  - [x] 7.5 `PostCommands_NoDomainClaims_Returns202Accepted` — verify all domains allowed when no domain claims (AC #5)
  - [x] 7.6 `PostCommands_NoPermissionClaims_Returns202Accepted` — verify all command types allowed when no permission claims (AC #5)
  - [x] 7.7 `PostCommands_WildcardPermission_Returns202Accepted` — verify `commands:*` grants access to any command type
  - [x] 7.8 `PostCommands_MatchingTenantDomainPermission_Returns202Accepted` — verify fully authorized request succeeds
  - [x] 7.9 `PostCommands_AuthFailure_Returns403BeforeMediatRPipeline` — verify tenant rejection doesn't trigger LoggingBehavior entry log (proving pre-pipeline rejection)
  - [x] 7.10 `PostCommands_AuthFailure_LogsWarningWithCorrelationId` — verify structured log entry for failed auth contains correlationId but NOT the JWT token
  - [x] 7.11 `PostCommands_PipelineOrder_AuthorizationAfterValidation` — verify that a request failing BOTH validation and authorization returns 400 (validation error), NOT 403 — proving ValidationBehavior runs before AuthorizationBehavior in the pipeline
  - [x] 7.12 `PostCommands_AuthorizationException_Returns403Not500` — verify `CommandAuthorizationException` produces 403 ProblemDetails (not 500), confirming `AuthorizationExceptionHandler` is registered before `GlobalExceptionHandler` in the IExceptionHandler chain

- [x] Task 8: Update existing integration tests if needed (AC: all)
  - [x] 8.1 Review existing integration tests from Stories 2.1-2.4 — ensure they use JWT tokens with sufficient claims (tenant, domain, permission) to pass the new authorization checks
  - [x] 8.2 Update `TestJwtTokenGenerator.GenerateToken()` default claims to include test tenant, domain, and `commands:*` permission so existing tests don't break
  - [x] 8.3 Verify all existing tests still pass after adding authorization

## Dev Notes

### Architecture Compliance

**Six-Layer Authentication Model (Architecture):** Story 2.5 completes layer 3 (endpoint-level tenant pre-check) and implements layer 4 (MediatR AuthorizationBehavior) of the six-layer defense in depth:

- **Layer 1: JWT Authentication** — `Microsoft.AspNetCore.Authentication.JwtBearer` (Story 2.4) ✓
- **Layer 2: Claims Transformation** — `IClaimsTransformation` normalizes JWT -> `eventstore:*` claims (Story 2.4) ✓
- **Layer 3: Endpoint Authorization** — `[Authorize]` attribute (Story 2.4) + tenant pre-check in controller action (Story 2.5)
- **Layer 4: MediatR AuthorizationBehavior** — Claims-based ABAC for domain + command type (Story 2.5)
- Layer 5: Actor-level TenantValidator (Story 3.3)
- Layer 6: DAPR access control policies (Story 5.1)

**MediatR Pipeline Order (Enforcement Rule #3):**
```
LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> CommandHandler
```
MediatR executes `AddOpenBehavior` registrations in order, with the first registered being outermost. AuthorizationBehavior MUST be registered AFTER ValidationBehavior so that structurally invalid commands are rejected before authorization is checked (avoid leaking authorization state for malformed requests).

**Enforcement Rules to Follow:**
- Rule #5: Never log event payload data — envelope metadata only (SEC-5, NFR12)
- Rule #7: ProblemDetails for all API error responses — 403 must be ProblemDetails
- Rule #9: correlationId in every structured log entry and OpenTelemetry activity
- Rule #10: Register services via `Add*` extension methods — never inline in Program.cs
- Rule #13: No stack traces in production error responses

**NFR Compliance:**
- NFR11: Failed authorization attempts logged with request metadata (source IP, attempted tenant, command type) WITHOUT logging the JWT token itself
- NFR12: Event payload data never in logs — only command metadata (tenant, domain, aggregateId, commandType) may be logged
- FR31: Authorization based on JWT claims for tenant, domain, and command type
- FR32: Unauthorized commands rejected at API gateway before processing pipeline

### Elicitation-Derived Hardening Notes

**C5 — Pre-existing `eventstore:*` claims in JWT:** If the incoming JWT already contains claims prefixed with `eventstore:`, `EventStoreClaimsTransformation` (Story 2.4) may duplicate or conflict with them. Verify that Story 2.4's `IClaimsTransformation` is idempotent and does not blindly add duplicates. Authorization code in Story 2.5 should not assume claim uniqueness — `FindAll()` may return duplicates. This is a Story 2.4 dependency; if not already handled, raise as a defect against Story 2.4 before implementing authorization.

**E1 — Tenant pre-check as reusable filter:** The tenant pre-check logic in `CommandsController.Submit()` will be needed again for future controllers (Stories 2.6 Command Status Query, 2.7 Command Replay). Consider extracting to a reusable `IActionFilter` or `IEndpointFilter` in a follow-up story. For Story 2.5, implement inline in the controller action — extraction is out of scope but document the duplication risk.

**E2 — Tenant and domain authorization are independent:** The authorization model checks tenant, domain, and command type independently — NOT as a combined tuple. Having `eventstore:tenant = A` and `eventstore:domain = X` means "authorized for tenant A (any domain)" AND "authorized for domain X (any tenant)", NOT "authorized for tenant A only in domain X". This is by design per the claims-based ABAC model. If paired tenant-domain authorization is needed in the future, it would require a different claim structure (e.g., `eventstore:tenant:A:domain:X`).

**O1 — Case normalization across auth layers:** All claim value comparisons MUST use `StringComparison.OrdinalIgnoreCase` consistently across both the controller tenant pre-check and `AuthorizationBehavior`. This is canonical across all auth layers. If Story 2.4's `EventStoreClaimsTransformation` normalizes claim values to lowercase, document that — but authorization code must still be case-insensitive as a defensive measure.

**CR5 — ValidateModelFilter runs BEFORE controller action:** The codebase has `ValidateModelFilter` registered via `AddControllers(options => options.Filters.Add<ValidateModelFilter>())`. This filter runs FluentValidation on the request model BEFORE the controller action method executes. For structurally invalid requests (missing required fields), the filter returns 400 ProblemDetails directly — the tenant pre-check in `Submit()` never runs. This is correct security behavior: don't leak authorization state for malformed requests. Do NOT add redundant model-level validation in the controller action; `ValidateModelFilter` already handles it.

**EN1 — Empty/null `request.Tenant` handling:** The tenant pre-check compares claims against `request.Tenant`. If `request.Tenant` is empty/null/whitespace, FluentValidation (via `ValidateModelFilter` or `ValidationBehavior`) should reject it before the tenant pre-check runs. The authorization code assumes `request.Tenant` is a non-empty string. If for any reason an empty tenant reaches the pre-check, the case-insensitive comparison with actual claims will fail and produce 403, which is acceptable.

### Critical Design Decisions

**What Already Exists (from Stories 2.1-2.4):**
- `CommandsController` with `[Authorize]` attribute and POST `/api/v1/commands` — currently requires authentication but no authorization beyond "is authenticated"
- `IClaimsTransformation` (`EventStoreClaimsTransformation`) extracts `eventstore:tenant`, `eventstore:domain`, `eventstore:permission` claims from JWT (Story 2.4)
- `LoggingBehavior` registered as outermost MediatR behavior (Story 2.3)
- `ValidationBehavior` registered as second MediatR behavior (Story 2.1/2.2)
- `SubmitCommand` MediatR command record with `Tenant`, `Domain`, `AggregateId`, `CommandType`, `CorrelationId` properties
- `ValidationExceptionHandler` (400), `GlobalExceptionHandler` (500) — IExceptionHandler pattern for ProblemDetails
- `CorrelationIdMiddleware` stores correlation ID in `HttpContext.Items["CorrelationId"]`
- Controller stores tenant in `HttpContext.Items["RequestTenantId"]` for error handlers
- `TestJwtTokenGenerator` helper for generating JWT tokens in integration tests
- `WebApplicationFactory` configured with symmetric JWT key for testing
- `EventStoreActivitySources.CommandApi` — ActivitySource for OpenTelemetry tracing
- `Microsoft.AspNetCore.Authentication.JwtBearer` already referenced, `UseAuthentication()`/`UseAuthorization()` in middleware
- All existing tests pass (242 as of Story 2.3; ~256 expected after Story 2.4 adds ~14 auth tests)

**What Story 2.5 Adds:**
1. **Tenant pre-check in `CommandsController.Submit()`** — Before `_mediator.Send()`, verify `User` has `eventstore:tenant` claim matching request tenant. Returns 403 ProblemDetails directly from controller on failure. Includes null HttpContext/User guards (C1), empty/whitespace claim filtering (C2), and defensive IsAuthenticated check (E4)
2. **`AuthorizationBehavior<TRequest, TResponse>`** — MediatR pipeline behavior that checks `eventstore:domain` and `eventstore:permission` claims against the `SubmitCommand` fields. Includes null guards (C1), claim filtering (C2), debug-level success logging (O2)
3. **`AuthorizationConstants`** — Static class with `WildcardPermission = "commands:*"` constant (E3) and any other authorization-related constants
4. **`CommandAuthorizationException`** — Typed exception for authorization failures with tenant, domain, commandType, reason
5. **`AuthorizationExceptionHandler`** — `IExceptionHandler` mapping `CommandAuthorizationException` to 403 ProblemDetails
6. **Updated `TestJwtTokenGenerator` defaults** — Existing tests need sufficient claims to pass new authorization

**Two-Level Authorization Design:**

The story implements authorization at two distinct levels, both producing 403 ProblemDetails:

| Level | Location | Checks | When Rejected |
|-------|----------|--------|---------------|
| Pre-pipeline | Controller action (before `_mediator.Send()`) | `eventstore:tenant` claim matches `request.Tenant` | Before MediatR pipeline — LoggingBehavior never fires |
| In-pipeline | `AuthorizationBehavior` (MediatR behavior #3) | `eventstore:domain` and `eventstore:permission` claims | After logging and validation, before handler |

**Tenant Pre-Check Logic (Controller Level):**
```csharp
// In CommandsController.Submit(), before _mediator.Send()

// C1: Guard against null HttpContext/User (defensive — should never happen with [Authorize])
if (HttpContext?.User is null)
{
    return CreateForbiddenProblemDetails("Authentication context unavailable.", correlationId, request.Tenant);
}

// E4: Belt-and-suspenders IsAuthenticated check
if (User.Identity?.IsAuthenticated != true)
{
    return CreateForbiddenProblemDetails("User is not authenticated.", correlationId, request.Tenant);
}

// C2: Filter empty/whitespace claim values
var tenantClaims = User.FindAll("eventstore:tenant")
    .Select(c => c.Value)
    .Where(v => !string.IsNullOrWhiteSpace(v))
    .ToList();

if (tenantClaims.Count == 0)
{
    // No tenant claims at all -> 403
    return CreateForbiddenProblemDetails("No tenant authorization claims found. Access denied.", correlationId, request.Tenant);
}
if (!tenantClaims.Any(t => string.Equals(t, request.Tenant, StringComparison.OrdinalIgnoreCase)))
{
    // Tenant not in authorized list -> 403
    return CreateForbiddenProblemDetails($"Not authorized to submit commands for tenant '{request.Tenant}'.", correlationId, request.Tenant);
}
```

**AuthorizationBehavior Logic (MediatR Pipeline):**
```csharp
// C1: Guard against null HttpContext/User
var httpContext = _httpContextAccessor.HttpContext
    ?? throw new InvalidOperationException("HttpContext is not available in AuthorizationBehavior.");
var user = httpContext.User
    ?? throw new InvalidOperationException("HttpContext.User is not available in AuthorizationBehavior.");

// E4: Defensive IsAuthenticated check
if (user.Identity?.IsAuthenticated != true)
{
    throw new CommandAuthorizationException(
        request is SubmitCommand cmd ? cmd.Tenant : "unknown",
        null, null, "User is not authenticated.");
}

// Only applies to SubmitCommand requests
if (request is SubmitCommand command)
{
    // C2: Filter empty/whitespace claim values
    var domainClaims = user.FindAll("eventstore:domain")
        .Select(c => c.Value)
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .ToList();
    var permissionClaims = user.FindAll("eventstore:permission")
        .Select(c => c.Value)
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .ToList();

    // Domain check (only if domain claims exist)
    if (domainClaims.Count > 0 && !domainClaims.Any(d => string.Equals(d, command.Domain, StringComparison.OrdinalIgnoreCase)))
    {
        throw new CommandAuthorizationException(command.Tenant, command.Domain, command.CommandType,
            $"Not authorized for domain '{command.Domain}'.");
    }

    // Permission check (only if permission claims exist) — E3: use constant
    if (permissionClaims.Count > 0)
    {
        bool hasWildcard = permissionClaims.Any(p => string.Equals(p, AuthorizationConstants.WildcardPermission, StringComparison.OrdinalIgnoreCase));
        bool hasSpecific = permissionClaims.Any(p => string.Equals(p, command.CommandType, StringComparison.OrdinalIgnoreCase));
        if (!hasWildcard && !hasSpecific)
        {
            throw new CommandAuthorizationException(command.Tenant, command.Domain, command.CommandType,
                $"Not authorized for command type '{command.CommandType}'.");
        }
    }

    // O2: Debug-level success logging for troubleshooting
    _logger.LogDebug("Authorization succeeded for {CorrelationId}: tenant={Tenant}, domain={Domain}, commandType={CommandType}",
        correlationId, command.Tenant, command.Domain, command.CommandType);
}

return await next().ConfigureAwait(false);
```

**403 ProblemDetails format** (same structure for all authorization failures, only `detail` varies):
```json
{
  "type": "https://tools.ietf.org/html/rfc9457#section-3",
  "title": "Forbidden",
  "status": 403,
  "detail": "<see variants below>",
  "instance": "/api/v1/commands",
  "correlationId": "abc-123-def-456",
  "tenantId": "acme-corp"
}
```

| Failure | `detail` message |
|---------|-----------------|
| No tenant claims | `"No tenant authorization claims found. Access denied."` |
| Wrong tenant | `"Not authorized to submit commands for tenant '{tenant}'."` |
| Wrong domain | `"Not authorized for domain '{domain}'."` |
| Wrong command type | `"Not authorized for command type '{commandType}'."` |

**IExceptionHandler Registration Order:**
Exception handlers are evaluated in registration order. `AuthorizationExceptionHandler` must be registered BEFORE `GlobalExceptionHandler` so that `CommandAuthorizationException` is handled specifically (403) rather than falling through to the generic 500 handler.

```csharp
// In AddCommandApi():
services.AddExceptionHandler<ValidationExceptionHandler>();     // 400
services.AddExceptionHandler<AuthorizationExceptionHandler>();  // 403 (NEW)
services.AddExceptionHandler<GlobalExceptionHandler>();         // 500 (catch-all)
```

### Technical Requirements

**Existing Types to Use:**
- `SubmitCommand` from `Hexalith.EventStore.Server.Pipeline.Commands` — MediatR command with Tenant, Domain, CommandType, CorrelationId
- `SubmitCommandResult` from same namespace — Result with CorrelationId
- `CorrelationIdMiddleware.HttpContextKey` (constant `"CorrelationId"`) — access correlation ID in controller and behavior
- `ProblemDetails` from `Microsoft.AspNetCore.Mvc` — RFC 7807 response format
- `IHttpContextAccessor` from `Microsoft.AspNetCore.Http` — access HttpContext in MediatR behaviors
- `EventStoreActivitySources.CommandApi` — ActivitySource for tracing (if adding authorization activity tags)
- `ClaimsPrincipal.FindAll("eventstore:tenant")` — query normalized claims from Story 2.4 IClaimsTransformation
- `TestJwtTokenGenerator` from `Hexalith.EventStore.IntegrationTests.Helpers` — generate JWT tokens with custom claims for testing

**NuGet Packages Already Available (in Directory.Packages.props):**
- `MediatR` 14.0.0 — `IPipelineBehavior<TRequest, TResponse>`, `AddOpenBehavior()`
- `Microsoft.Extensions.Logging.Abstractions` — `ILogger<T>`, structured logging
- `Shouldly` 4.3.0 — test assertions
- `Microsoft.AspNetCore.Mvc.Testing` 10.0.0 — WebApplicationFactory for integration tests
- `NSubstitute` 5.3.0 — available for mocking if needed

**No new NuGet packages needed.** All dependencies are already available.

### File Structure Requirements

**New files to create:**
```
src/Hexalith.EventStore.CommandApi/
├── Pipeline/
│   ├── AuthorizationBehavior.cs             # NEW: MediatR pipeline behavior #3 for ABAC
│   └── AuthorizationConstants.cs            # NEW: WildcardPermission = "commands:*" and other auth constants
├── ErrorHandling/
│   ├── CommandAuthorizationException.cs     # NEW: Typed exception for auth failures
│   └── AuthorizationExceptionHandler.cs     # NEW: IExceptionHandler -> 403 ProblemDetails

tests/Hexalith.EventStore.Server.Tests/
├── Pipeline/
│   └── AuthorizationBehaviorTests.cs        # NEW: Unit tests for AuthorizationBehavior
└── ErrorHandling/
    └── AuthorizationExceptionHandlerTests.cs # NEW: Unit tests for exception handler + exception type

tests/Hexalith.EventStore.IntegrationTests/
└── CommandApi/
    └── AuthorizationIntegrationTests.cs     # NEW: Integration tests for authorization flow
```

**Existing files to modify:**
```
src/Hexalith.EventStore.CommandApi/
├── Controllers/
│   └── CommandsController.cs                # MODIFY: Add tenant pre-check before _mediator.Send()
├── Extensions/
│   └── ServiceCollectionExtensions.cs       # MODIFY: Register AuthorizationBehavior + AuthorizationExceptionHandler
```

**Existing files to verify (no changes expected):**
```
src/Hexalith.EventStore.CommandApi/
├── Program.cs                               # VERIFY: No changes needed (middleware order unchanged)
├── Pipeline/
│   ├── LoggingBehavior.cs                   # VERIFY: Still outermost behavior
│   └── ValidationBehavior.cs                # VERIFY: Still second behavior
├── Authentication/
│   └── EventStoreClaimsTransformation.cs    # VERIFY: Claims available for authorization
├── ErrorHandling/
│   ├── ValidationExceptionHandler.cs        # VERIFY: Still handles 400s
│   └── GlobalExceptionHandler.cs            # VERIFY: Still catch-all for 500s

tests/Hexalith.EventStore.IntegrationTests/
├── Helpers/
│   └── TestJwtTokenGenerator.cs             # VERIFY/MODIFY: Default claims need tenant/domain/permission
```

### Testing Requirements

**Test Projects:**
- `tests/Hexalith.EventStore.Server.Tests/` — Unit tests for AuthorizationBehavior
- `tests/Hexalith.EventStore.IntegrationTests/` — Integration tests for authorization flow + verify existing tests

**Test Patterns (established in Stories 1.6, 2.1, 2.2, 2.3, 2.4):**
- Method naming: `{Method}_{Scenario}_{ExpectedResult}`
- Arrange/Act/Assert pattern
- Shouldly for assertions
- `WebApplicationFactory<Program>` with extern alias for integration tests
- `ConfigureAwait(false)` on all async test methods
- `TestJwtTokenGenerator` for creating JWT tokens with specific claims

**Unit Test Strategy for AuthorizationBehavior:**
Mock `IHttpContextAccessor` to provide a `ClaimsPrincipal` with specific claims. Create `SubmitCommand` instances with different tenant/domain/commandType values. Verify behavior passes through or throws `CommandAuthorizationException`.

```csharp
// Example test setup:
var claims = new List<Claim>
{
    new("eventstore:tenant", "test-tenant"),
    new("eventstore:domain", "orders"),
    new("eventstore:permission", "commands:*"),
};
var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
var httpContext = new DefaultHttpContext { User = principal };
httpContext.Items["CorrelationId"] = "test-correlation-id";
var accessor = Substitute.For<IHttpContextAccessor>();
accessor.HttpContext.Returns(httpContext);
```

**Integration Test Strategy for Authorization:**
Use `TestJwtTokenGenerator` to create tokens with specific claim combinations:

```csharp
// Token with specific tenant only
var token = TestJwtTokenGenerator.GenerateToken(
    tenants: ["tenant-a"],
    domains: [],       // no domain claims -> all domains allowed
    permissions: []);  // no permission claims -> all types allowed

// Token with restricted scope
var restrictedToken = TestJwtTokenGenerator.GenerateToken(
    tenants: ["tenant-a"],
    domains: ["orders"],
    permissions: ["PlaceOrder", "CancelOrder"]);
```

**CRITICAL: Verify Existing Tests Don't Break:**
After adding authorization, all existing integration tests MUST still pass. The `TestJwtTokenGenerator.GenerateToken()` default should include at least one tenant claim matching the test tenant used in existing tests, plus `commands:*` wildcard permission. Verify the default claims in `TestJwtTokenGenerator` are sufficient.

**Minimum Tests (27):**

AuthorizationBehavior Unit Tests (10) — in `AuthorizationBehaviorTests.cs`:
1. `AuthorizationBehavior_UserWithMatchingDomain_Succeeds`
2. `AuthorizationBehavior_UserWithNoDomainClaims_Succeeds`
3. `AuthorizationBehavior_UserWithWrongDomain_ThrowsAuthorizationException`
4. `AuthorizationBehavior_UserWithMatchingPermission_Succeeds`
5. `AuthorizationBehavior_UserWithNoPermissionClaims_Succeeds`
6. `AuthorizationBehavior_UserWithWrongPermission_ThrowsAuthorizationException`
7. `AuthorizationBehavior_UserWithWildcardPermission_Succeeds`
8. `AuthorizationBehavior_NonSubmitCommandRequest_PassesThrough`
9. `AuthorizationBehavior_CaseInsensitiveDomainMatch_Succeeds`
10. `AuthorizationBehavior_FailedAuth_LogsWarningWithoutJwtToken`

ExceptionHandler & Exception Unit Tests (5) — in `AuthorizationExceptionHandlerTests.cs`:
11. `AuthorizationExceptionHandler_HandlesCommandAuthorizationException_Returns403ProblemDetails`
12. `AuthorizationExceptionHandler_IgnoresOtherExceptions_ReturnsFalse`
13. `AuthorizationExceptionHandler_SetsContentType_ApplicationProblemJson`
14. `CommandAuthorizationException_Properties_SetCorrectly`
15. `CommandAuthorizationException_ToString_DoesNotLeakSensitiveData`

Integration Tests (12) — in `AuthorizationIntegrationTests.cs`:
16. `PostCommands_TenantNotInClaims_Returns403ProblemDetails`
17. `PostCommands_NoTenantClaims_Returns403ProblemDetails`
18. `PostCommands_DomainNotInClaims_Returns403ProblemDetails`
19. `PostCommands_CommandTypeNotInClaims_Returns403ProblemDetails`
20. `PostCommands_NoDomainClaims_Returns202Accepted`
21. `PostCommands_NoPermissionClaims_Returns202Accepted`
22. `PostCommands_WildcardPermission_Returns202Accepted`
23. `PostCommands_MatchingTenantDomainPermission_Returns202Accepted`
24. `PostCommands_TenantAuthFailure_RejectedBeforeMediatRPipeline`
25. `PostCommands_AuthFailure_LogsWarningWithCorrelationId`
26. `PostCommands_PipelineOrder_AuthorizationAfterValidation` (verify 400 not 403 for invalid+unauthorized)
27. `PostCommands_AuthorizationException_Returns403Not500` (verify AuthorizationExceptionHandler registration order)

**Current test count:** 242 tests (as of Story 2.3). Expected ~256 after Story 2.4. Story 2.5 adds 27 new tests, bringing estimated total to ~283.

### Previous Story Intelligence

**From Story 2.4 (JWT Authentication & Claims Transformation):**
- JWT authentication middleware fully configured with two-mode support (OIDC production / symmetric key development)
- `EventStoreClaimsTransformation` extracts `eventstore:tenant`, `eventstore:domain`, `eventstore:permission` claims from JWT
- `[Authorize]` attribute on `CommandsController` — requires authentication
- `TestJwtTokenGenerator` helper creates JWT tokens with configurable claims
- `WebApplicationFactory` configured with symmetric JWT key for tests
- `JwtBearerEvents.OnChallenge` returns 401 ProblemDetails with correlationId
- JWT claim convention: `tenants` (array), `tenant_id`/`tid` (single), `domains` (array), `permissions` (array)
- Claims transformation is idempotent (safe to run multiple times)
- All existing integration tests updated to include valid JWT tokens
- Key insight: `User.FindAll("eventstore:tenant")` returns all normalized tenant claims after transformation

**From Story 2.3 (MediatR Pipeline & Logging Behavior):**
- LoggingBehavior registered as outermost MediatR pipeline behavior
- `EventStoreActivitySources.CommandApi` ActivitySource for OpenTelemetry tracing
- Pipeline order enforced: LoggingBehavior -> ValidationBehavior -> (AuthorizationBehavior reserved) -> CommandHandler
- AuthorizationBehavior position was RESERVED but NOT yet implemented
- Correlation ID access: `IHttpContextAccessor` -> `HttpContext.Items["CorrelationId"]`
- `IHttpContextAccessor` already registered via `services.AddHttpContextAccessor()`

**From Story 2.2 (Command Validation & RFC 7807 Error Responses):**
- `IExceptionHandler` pattern well established for ProblemDetails responses
- `ValidationExceptionHandler` (400) and `GlobalExceptionHandler` (500) handle all error scenarios
- ProblemDetails always include `correlationId` extension and optionally `tenantId`
- Content type: `application/problem+json`
- ProblemDetails `type` URI: `https://tools.ietf.org/html/rfc9457#section-3`
- Tenant extracted from `HttpContext.Items["RequestTenantId"]` for error context

**From Story 2.1 (CommandApi Host & Minimal Endpoint Scaffolding):**
- Controller stores tenant in `HttpContext.Items["RequestTenantId"]` early in the action method
- `CorrelationIdMiddleware` runs FIRST in middleware pipeline
- Primary constructors for DI injection, records for immutable data
- `ConfigureAwait(false)` on all async calls, `ArgumentNullException.ThrowIfNull()` on public methods

**Key Patterns (mandatory for all new code):**
- Primary constructors for DI: `public class Foo(IDep dep) : Base`
- Records for immutable data: `public record CommandAuthorizationException(...)`
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` on public methods (CA1062)
- Extern alias for WebApplicationFactory tests
- Feature folder organization: Pipeline/, ErrorHandling/, Authentication/
- `namespace Hexalith.EventStore.CommandApi.{Feature};`
- Feature branches: `feature/story-X.Y-description`, commit messages: `"Story X.Y: Title"`

### Project Structure Notes

**Alignment with Architecture:**
- **Architecture doc discrepancy:** The cross-cutting concerns table lists `Server/Pipeline/AuthorizationBehavior.cs`, but all existing pipeline behaviors (`LoggingBehavior`, `ValidationBehavior`) are actually in `CommandApi/Pipeline/`. Follow the ACTUAL codebase pattern, not the architecture doc's projected path.
- `AuthorizationBehavior` goes in `CommandApi/Pipeline/` alongside `LoggingBehavior` and `ValidationBehavior` — follows feature folder convention (enforcement rule #2)
- `CommandAuthorizationException` and `AuthorizationExceptionHandler` go in `CommandApi/ErrorHandling/` alongside existing exception handlers
- Registration in `AddCommandApi()` per enforcement rule #10
- Test files follow established pattern: `Pipeline/AuthorizationBehaviorTests.cs` for unit tests, `CommandApi/AuthorizationIntegrationTests.cs` for integration tests

**Dependency Graph Relevant to This Story:**
```
CommandApi -> Server -> Contracts
CommandApi -> ServiceDefaults
Tests: IntegrationTests -> CommandApi (via WebApplicationFactory)
Tests: Server.Tests -> Server + CommandApi
```

**Architecture Reference — Full Data Flow After Story 2.5:**
```
POST /api/v1/commands
    │
    ▼
CorrelationIdMiddleware (generate/propagate)
    │
    ▼
RateLimiting (per-tenant sliding window, D8) [Story 2.9]
    │
    ▼
JWT Authentication (layer 1) + Claims Transformation (layer 2) [Story 2.4]
    │
    ▼
[Authorize] policy (layer 3) [Story 2.4]
    │
    ▼
CommandsController.Submit()
    ├── Tenant pre-check (layer 3.5) [Story 2.5] ← 403 if tenant unauthorized
    │
    ▼
MediatR Pipeline:
    ├── LoggingBehavior (entry log) [Story 2.3]
    ├── ValidationBehavior [Story 2.2] ← 400 if invalid
    ├── AuthorizationBehavior (layer 4) [Story 2.5] ← 403 if domain/command unauthorized
    └── SubmitCommandHandler → Actor activation [Story 3.1+]
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.5: Endpoint Authorization & Command Rejection]
- [Source: _bmad-output/planning-artifacts/architecture.md#Authentication & Security - D4]
- [Source: _bmad-output/planning-artifacts/architecture.md#Communication Patterns - Six-Layer Auth]
- [Source: _bmad-output/planning-artifacts/architecture.md#Communication Patterns - MediatR Pipeline Behaviors]
- [Source: _bmad-output/planning-artifacts/architecture.md#Process Patterns - Error Handling]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines - Rules #3, #5, #7, #9, #10, #13]
- [Source: _bmad-output/planning-artifacts/architecture.md#Security-Critical Architectural Constraints - SEC-2, SEC-5]
- [Source: _bmad-output/planning-artifacts/architecture.md#Data Flow - Authorization layers]
- [Source: _bmad-output/implementation-artifacts/2-4-jwt-authentication-and-claims-transformation.md]
- [Source: _bmad-output/implementation-artifacts/2-3-mediatr-pipeline-and-logging-behavior.md]
- [Source: _bmad-output/implementation-artifacts/2-2-command-validation-and-rfc-7807-error-responses.md]
- [Source: _bmad-output/implementation-artifacts/2-1-commandapi-host-and-minimal-endpoint-scaffolding.md]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

### Completion Notes List

- Task 0: All Story 2.4 prerequisites verified — [Authorize] on controller, EventStoreClaimsTransformation registered, TestJwtTokenGenerator exists, all 257 tests pass.
- Task 1: Added pre-pipeline tenant authorization to CommandsController.Submit() with null HttpContext/User guards, IsAuthenticated check, tenant claim filtering/matching (case-insensitive), 403 ProblemDetails on failure, and structured Warning log. Stores authorized tenant in HttpContext.Items["AuthorizedTenant"].
- Task 2: Created AuthorizationBehavior<TRequest, TResponse> MediatR pipeline behavior in CommandApi/Pipeline/. Checks eventstore:domain and eventstore:permission claims for SubmitCommand requests. Supports wildcard (commands:*), case-insensitive matching, and absent-claim-means-all semantics. Non-SubmitCommand requests pass through.
- Task 3: Created CommandAuthorizationException (with TenantId, Domain, CommandType, Reason properties) and AuthorizationExceptionHandler (IExceptionHandler returning 403 ProblemDetails with correlationId/tenantId extensions, application/problem+json content type).
- Task 4: Registered AuthorizationBehavior as third MediatR pipeline behavior (after ValidationBehavior) and AuthorizationExceptionHandler between ValidationExceptionHandler and GlobalExceptionHandler in ServiceCollectionExtensions.AddCommandApi().
- Task 5: Wrote 10 unit tests for AuthorizationBehavior covering: matching/wrong/absent domain claims, matching/wrong/absent/wildcard permission claims, non-SubmitCommand passthrough, case-insensitive matching, and warning log without JWT token leak.
- Task 6: Wrote 5 unit tests for AuthorizationExceptionHandler and CommandAuthorizationException covering: 403 ProblemDetails structure, ignore non-auth exceptions, content type verification, exception property validation, and no sensitive data leak in ToString().
- Task 7: Wrote 12 integration tests covering: tenant/domain/command type authorization failures (403), absent domain/permission claims (202), wildcard permission (202), fully authorized request (202), pre-pipeline rejection verification, warning log with correlationId, pipeline order (validation before authorization), and exception handler registration order.
- Task 8: Fixed existing JwtAuthenticationIntegrationTests.PostCommands_ValidTokenWithTenantClaims_ClaimsTransformedCorrectly — updated request to use tenant="tenant-a" and domain="orders" to match JWT claims after authorization was added. All 257 original tests still pass.

### Change Log

- **AuthorizationExceptionHandler content type fix**: `WriteAsJsonAsync(value, cancellationToken)` overload in .NET 10 always overrides ContentType to `application/json; charset=utf-8`. Fixed by using explicit content type parameter: `WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, "application/problem+json", cancellationToken)`.
- **DefaultHttpContext.Response.Body in unit tests**: `DefaultHttpContext` uses `Stream.Null` by default, which prevents reading response body in tests. Fixed by assigning `new MemoryStream()` to `Response.Body` in test setup.
- **Existing test breakage**: `PostCommands_ValidTokenWithTenantClaims_ClaimsTransformedCorrectly` broke because request tenant="test-tenant" didn't match JWT claims tenants=["tenant-a","tenant-b"]. Fixed by aligning request with JWT claims.

### File List

**New files created:**
- `src/Hexalith.EventStore.CommandApi/Pipeline/AuthorizationBehavior.cs` — MediatR pipeline behavior #3 for domain/command type ABAC
- `src/Hexalith.EventStore.CommandApi/Pipeline/AuthorizationConstants.cs` — WildcardPermission constant ("commands:*")
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/CommandAuthorizationException.cs` — Typed exception for authorization failures
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/AuthorizationExceptionHandler.cs` — IExceptionHandler returning 403 ProblemDetails
- `tests/Hexalith.EventStore.Server.Tests/Pipeline/AuthorizationBehaviorTests.cs` — 10 unit tests for AuthorizationBehavior
- `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/AuthorizationExceptionHandlerTests.cs` — 5 unit tests for exception handler + exception type
- `tests/Hexalith.EventStore.IntegrationTests/CommandApi/AuthorizationIntegrationTests.cs` — 12 integration tests for authorization flow

**Modified files:**
- `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` — Added pre-pipeline tenant authorization, ILogger injection, CreateForbiddenProblemDetails helper
- `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` — Registered AuthorizationBehavior and AuthorizationExceptionHandler
- `tests/Hexalith.EventStore.IntegrationTests/CommandApi/JwtAuthenticationIntegrationTests.cs` — Fixed test to align request with JWT claims after authorization added

**Test count: 284 total (257 original + 27 new)**
