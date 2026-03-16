# Story 3.5: Concurrency, Auth & Infrastructure Error Responses

Status: in-progress

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an **API consumer**,
I want consistent, actionable error responses for auth failures, concurrency conflicts, and service unavailability,
So that my retry logic and error handling work correctly (UX-DR1 through UX-DR11).

## Acceptance Criteria

1. **401 Unauthorized -- missing JWT** - Given a request with no Authorization header, When the request is received, Then the system returns `401 Unauthorized` with `WWW-Authenticate: Bearer realm="hexalith-eventstore"` header per RFC 6750 (UX-DR4), And the ProblemDetails `type` is `https://hexalith.io/problems/authentication-required` (UX-DR8), And `detail` is "Authentication is required to access this resource.", And no `correlationId` extension is present (pre-pipeline, UX-DR2).

2. **401 Unauthorized -- expired JWT** - Given a request with an expired JWT, When the request is received, Then the system returns `401 Unauthorized` with `WWW-Authenticate: Bearer realm="hexalith-eventstore", error="invalid_token", error_description="The token has expired"` header per RFC 6750 (UX-DR4), And the ProblemDetails `type` is `https://hexalith.io/problems/token-expired` (UX-DR8), And `detail` is "The provided authentication token has expired.", And no `correlationId` extension is present (pre-pipeline, UX-DR2).

3. **401 Unauthorized -- invalid JWT** - Given a request with an invalid JWT (wrong signature, wrong issuer, wrong audience), When the request is received, Then the system returns `401 Unauthorized` with `WWW-Authenticate: Bearer realm="hexalith-eventstore", error="invalid_token", error_description="The token is invalid"` header, And the ProblemDetails `type` is `https://hexalith.io/problems/authentication-required`, And `detail` is "The provided authentication token is invalid.", And no `correlationId` extension is present.

4. **403 Forbidden -- tenant authorization** - Given a JWT without required tenant authorization, When the command is submitted, Then the system returns `403 Forbidden` with ProblemDetails naming the specific rejected tenant (UX-DR9), And does NOT enumerate the caller's authorized tenants, And includes `correlationId` extension (UX-DR2), And the ProblemDetails `type` is `https://hexalith.io/problems/forbidden`.

5. **409 Conflict -- optimistic concurrency** - Given an optimistic concurrency conflict, When two commands race for the same entity, Then the system returns `409 Conflict` with `Retry-After: 1` header (UX-DR5, UX-DR10), And the ProblemDetails `type` is `https://hexalith.io/problems/concurrency-conflict`, And no sequence numbers, internal state, or implementation-specific details are leaked (no `aggregateId`, no `conflictSource` extensions), And no event sourcing terminology appears in `detail` ("aggregate", "event stream", "actor", "DAPR" are forbidden -- UX-DR6), And includes `correlationId` extension (UX-DR2).

6. **503 Service Unavailable -- DAPR sidecar** - Given the DAPR sidecar is unavailable, When a command is submitted, Then the system returns `503 Service Unavailable` with `Retry-After: 30` header (UX-DR5, UX-DR11), And the ProblemDetails `type` is `https://hexalith.io/problems/service-unavailable`, And `detail` says "command processing pipeline" (never "DAPR sidecar", "actor", or any internal component name -- UX-DR11), And no `correlationId` extension is present (pre-pipeline, UX-DR2).

7. **503 Service Unavailable -- authorization service** - Given the authorization service (actor) is unavailable, When a command is submitted, Then the system returns `503 Service Unavailable` with `Retry-After: 30` header (UX-DR5), And the ProblemDetails `type` is `https://hexalith.io/problems/service-unavailable`, And `detail` says "command processing pipeline" (never "Authorization service" -- UX-DR11), And no `correlationId` extension is present (UX-DR2).

8. **No event sourcing terminology in any error response** - All ProblemDetails responses (400, 401, 403, 409, 503) MUST NOT contain the words "aggregate", "event stream", "actor", "DAPR", "sidecar" in `title`, `detail`, or extension keys (UX-DR6).

9. **Hexalith-specific type URIs for all error categories** - All ProblemDetails `type` fields use stable, unique Hexalith URIs (UX-DR7): `https://hexalith.io/problems/validation-error` (400), `https://hexalith.io/problems/authentication-required` (401 missing/invalid), `https://hexalith.io/problems/token-expired` (401 expired), `https://hexalith.io/problems/forbidden` (403), `https://hexalith.io/problems/concurrency-conflict` (409), `https://hexalith.io/problems/service-unavailable` (503). Generic RFC URIs like `https://tools.ietf.org/html/rfc9457#section-3` are replaced.

10. **All existing tests pass** - All Tier 1 and Tier 2 tests continue to pass after all changes.

## Implementation Status Assessment

**CRITICAL CONTEXT: Much of the error handling infrastructure is already implemented** from prior work (Stories 2.4, 2.5, 3.1-3.4). This story refines the existing handlers to achieve full UX-DR compliance. The primary work is fixing gaps, not building from scratch.

### Already Implemented

| Component | File | Status | Gaps |
|-----------|------|--------|------|
| JWT auth with ProblemDetails | `CommandApi/Authentication/ConfigureJwtBearerOptions.cs` | Complete | Missing WWW-Authenticate header (UX-DR4), uses generic type URI, includes correlationId on 401 (violates UX-DR2), no distinct expired vs missing type URIs (UX-DR8) |
| 403 Forbidden handler | `CommandApi/ErrorHandling/AuthorizationExceptionHandler.cs` | Complete | Uses generic type URI, verify tenant naming (UX-DR9) |
| 409 Concurrency handler | `CommandApi/ErrorHandling/ConcurrencyConflictExceptionHandler.cs` | Complete | Uses generic type URI, exposes `aggregateId` and `conflictSource` extensions (UX-DR10), detail message says "aggregate" (UX-DR6) |
| 503 Auth service handler | `CommandApi/ErrorHandling/AuthorizationServiceUnavailableHandler.cs` | Complete | Uses generic type URI, says "Authorization service" (UX-DR11), includes correlationId (violates UX-DR2), Retry-After varies (should be 30s per UX-DR5) |
| 400 Validation handler | `CommandApi/ErrorHandling/ValidationProblemDetailsFactory.cs` | Complete | Already uses `hexalith.io/problems/validation-error` -- compliant |
| 500 Global handler | `CommandApi/ErrorHandling/GlobalExceptionHandler.cs` | Complete | Uses generic type URI |
| 404 Query not found | `CommandApi/ErrorHandling/QueryNotFoundExceptionHandler.cs` | Complete | Uses generic type URI |
| Domain rejection handler | `CommandApi/ErrorHandling/DomainCommandRejectedExceptionHandler.cs` | Complete | Uses domain-specific type URIs -- compliant |
| ConcurrencyConflictException | `Server/Commands/ConcurrencyConflictException.cs` | Complete | Detail message template contains "aggregate" (UX-DR6) |

### Existing Test Coverage

| Test File | Tier | Tests |
|-----------|------|-------|
| `ConcurrencyConflictExceptionHandlerTests.cs` | T2 | Handler behavior, DAPR unwrapping, advisory status write |
| `AuthorizationExceptionHandlerTests.cs` | T2 | 403 response structure |
| `AuthorizationServiceUnavailableHandlerTests.cs` | T2 | 503 response, Retry-After |
| `AuthorizationServiceUnavailableExceptionTests.cs` | T2 | Exception construction |
| `ValidationExceptionHandlerTests.cs` | T2 | 400 validation response |
| `QueryNotFoundExceptionHandlerTests.cs` | T2 | 404 response |
| `JwtAuthenticationIntegrationTests.cs` | T3 | Full JWT flow |
| `AuthorizationIntegrationTests.cs` | T3 | Full auth flow |

## Tasks / Subtasks

- [ ] Task 0: Verify prerequisites and baseline (BLOCKING)
  - [ ] 0.1 Run all Tier 1 tests -- confirm all pass (baseline: 656 from Story 3.4)
  - [ ] 0.2 Run Tier 2 tests `Hexalith.EventStore.Server.Tests` -- confirm pass count (baseline: 1297 from Story 3.4, 21 pre-existing DAPR infra failures)
  - [ ] 0.3 Read ALL error handler files listed in Implementation Status Assessment table
  - [ ] 0.4 Read ALL existing test files listed in Existing Test Coverage table

- [ ] Task 1: Create centralized ProblemDetails type URI constants (AC: #9)
  - [ ] 1.1 Create a `ProblemTypeUris` static class in `CommandApi/ErrorHandling/` with constants: `ValidationError = "https://hexalith.io/problems/validation-error"`, `AuthenticationRequired = "https://hexalith.io/problems/authentication-required"`, `TokenExpired = "https://hexalith.io/problems/token-expired"`, `BadRequest = "https://hexalith.io/problems/bad-request"`, `Forbidden = "https://hexalith.io/problems/forbidden"`, `NotFound = "https://hexalith.io/problems/not-found"`, `ConcurrencyConflict = "https://hexalith.io/problems/concurrency-conflict"`, `RateLimitExceeded = "https://hexalith.io/problems/rate-limit-exceeded"`, `ServiceUnavailable = "https://hexalith.io/problems/service-unavailable"`, `CommandStatusNotFound = "https://hexalith.io/problems/command-status-not-found"`, `InternalServerError = "https://hexalith.io/problems/internal-server-error"`
  - [ ] 1.2 Update `ValidationProblemDetailsFactory.cs` to use `ProblemTypeUris.ValidationError` (already compliant URI, just centralize the constant)
  - [ ] 1.3 Update `GlobalExceptionHandler.cs` type from `https://tools.ietf.org/html/rfc9457#section-3` to `ProblemTypeUris.InternalServerError`
  - [ ] 1.4 Update `QueryNotFoundExceptionHandler.cs` type to `ProblemTypeUris.CommandStatusNotFound`
  - [ ] 1.5 Update any other handlers using generic RFC type URIs -- scan all files in `CommandApi/ErrorHandling/` and `CommandApi/Controllers/` for `rfc9457` or `rfc6585` strings
  - [ ] 1.6 Check `CommandStatusController.cs` which uses `https://hexalith.io/problems/bad-request`, `https://hexalith.io/problems/forbidden`, `https://hexalith.io/problems/command-status-not-found` inline -- centralize to `ProblemTypeUris`
  - [ ] 1.7 **Fix `ReplayController.cs`** which has TWO helper methods (`CreateProblemDetails` and `CreateConflictProblemDetails`) both using `"https://tools.ietf.org/html/rfc9457#section-3"` for 400, 403, 404, and 409 responses. Replace with appropriate `ProblemTypeUris.*` constants (`BadRequest`, `Forbidden`, `NotFound`, `ConcurrencyConflict`).
  - [ ] 1.8 **Fix 429 rate limiter response** in `ServiceCollectionExtensions.cs` (rate limit rejection callback). It uses `"https://tools.ietf.org/html/rfc6585#section-4"` as type URI and constructs an anonymous type instead of a proper `ProblemDetails` object. Replace with `ProblemTypeUris.RateLimitExceeded` and use a real `ProblemDetails` instance (architecture Rule #7: ProblemDetails for ALL error responses).

- [ ] Task 2: Fix 401 Unauthorized responses (AC: #1, #2, #3)
  - [ ] 2.1 In `ConfigureJwtBearerOptions.cs` `OnChallenge` handler: Add `WWW-Authenticate` header per RFC 6750. For missing token: `Bearer realm="hexalith-eventstore"`. For expired token: `Bearer realm="hexalith-eventstore", error="invalid_token", error_description="The token has expired"`. For invalid token: `Bearer realm="hexalith-eventstore", error="invalid_token", error_description="The token is invalid"`. Determine the failure type from `context.AuthenticateFailure` and `context.Error`.
  - [ ] 2.2 Set ProblemDetails `type` to `ProblemTypeUris.TokenExpired` when `context.AuthenticateFailure is SecurityTokenExpiredException`, otherwise `ProblemTypeUris.AuthenticationRequired`
  - [ ] 2.3 Remove `correlationId` from the 401 ProblemDetails extensions (pre-pipeline rejection, UX-DR2). Remove the line `Extensions = { ["correlationId"] = correlationId }`.
  - [ ] 2.4 Remove the `tenantId` extension from 401 responses -- `TryAddTenantExtensionAsync` call should be removed from the OnChallenge handler. 401 is a pre-pipeline rejection and should contain no request-specific context.
  - [ ] 2.5 Verify the `detail` messages match AC text exactly: "Authentication is required to access this resource." (missing), "The provided authentication token has expired." (expired), "The provided authentication token is invalid." (invalid signature/issuer/audience). **NOTE:** The current `GetDetailMessage` method returns "The provided authentication token has an invalid issuer." for `SecurityTokenInvalidIssuerException` (line ~148). This MUST be changed to "The provided authentication token is invalid." to consolidate all invalid-token scenarios into a single message per AC #3.
  - [ ] 2.6 **IMPORTANT**: Keep all logging unchanged -- the server-side `_logger.LogWarning` calls still log `correlationId`, `sourceIp`, `tenantId`, `commandType` for security auditing. Only the CLIENT RESPONSE (ProblemDetails body) removes these fields.

- [ ] Task 3: Fix 403 Forbidden responses (AC: #4)
  - [ ] 3.1 In `AuthorizationExceptionHandler.cs`: Update `type` to `ProblemTypeUris.Forbidden`
  - [ ] 3.2 Verify `CommandAuthorizationException.Reason` names the specific rejected tenant. Read `CommandAuthorizationException.cs` to confirm the `Reason` property includes the rejected tenant name. If not, update the exception construction sites.
  - [ ] 3.3 Verify the handler does NOT include any enumeration of authorized tenants in the response. The `tenantId` extension should reference the rejected tenant only, not a list.
  - [ ] 3.4 Verify no event sourcing terminology in the `detail` message (UX-DR6)

- [ ] Task 4: Fix 409 Conflict responses (AC: #5)
  - [ ] 4.1 In `ConcurrencyConflictExceptionHandler.cs`: Update `type` to `ProblemTypeUris.ConcurrencyConflict`
  - [ ] 4.2 Remove `aggregateId` extension from ProblemDetails (leaks internal addressing -- UX-DR10). The consumer already knows which entity they targeted.
  - [ ] 4.3 Remove `conflictSource` extension from ProblemDetails (leaks implementation details like "StateStore", "ActorReentrancy" -- UX-DR10)
  - [ ] 4.4 Remove `tenantId` extension from ProblemDetails if present (the consumer already knows their tenant; UX-DR10 says no internal state leakage)
  - [ ] 4.5 Replace `conflict.Message` in `Detail` with a safe message that avoids "aggregate" and does not reveal the internal concurrency model: `"A concurrency conflict occurred. Please retry the command."` (UX-DR6, UX-DR10 -- "between read and write" leaks optimistic locking implementation detail)
  - [ ] 4.6 Verify `Retry-After: 1` header is still present (UX-DR5) -- already correct, do not change
  - [ ] 4.7 Verify `correlationId` extension is still present (UX-DR2) -- already correct, do not change
  - [ ] 4.8 **Check `ConcurrencyConflictException.cs` default detail message** -- if the default `Message` template contains "aggregate", update it to avoid the term. This is used in server-side logging (acceptable) but also flows to the client via `conflict.Message` if not overridden. Since we're now using a hardcoded safe message in the handler (4.5), the exception message change is optional but recommended for consistency.
  - [ ] 4.9 Verify advisory status write still works correctly after removing extensions (the status write uses `conflict.AggregateId` and `conflict.TenantId` internally -- those must NOT be removed from the exception object, only from the ProblemDetails response)

- [ ] Task 5: Fix 503 Service Unavailable responses (AC: #6, #7)
  - [ ] 5.1 In `AuthorizationServiceUnavailableHandler.cs`: Update `type` to `ProblemTypeUris.ServiceUnavailable`
  - [ ] 5.2 Change `Detail` from "Authorization service is temporarily unavailable. Please retry." to "The command processing pipeline is temporarily unavailable. Please retry after the specified interval." (UX-DR11 -- never name internal components)
  - [ ] 5.3 Change `Retry-After` from `unavailable.RetryAfterSeconds.ToString()` to `"30"` (UX-DR5 mandates 30s for 503)
  - [ ] 5.4 Remove `correlationId` from ProblemDetails extensions (pre-pipeline rejection, UX-DR2)
  - [ ] 5.5 **Create a new `DaprSidecarUnavailableHandler`** in `CommandApi/ErrorHandling/`. **FIRST:** Inspect the DAPR SDK NuGet packages (`Dapr.Client`, `Dapr.AspNetCore`, `Dapr.Actors`) to identify the exact exception types thrown when the sidecar is unreachable. Check for `DaprApiException`, `DaprException`, `InvocationException`, and `Grpc.Core.RpcException` in the package dependency tree. Verify that `Grpc.Net.Client` or `Grpc.Core` is transitively available without adding a new package reference. **THEN:** Implement the handler to detect DAPR sidecar unavailability by walking the InnerException chain (same pattern as `ConcurrencyConflictExceptionHandler.FindConcurrencyConflict`) looking for the identified exception types with gRPC `StatusCode.Unavailable`, `HttpRequestException` (connection refused), or DAPR-specific unavailability exceptions. Returns 503 with: `Retry-After: 30`, no correlationId, "command processing pipeline" language, `ProblemTypeUris.ServiceUnavailable`. Use `CancellationToken.None` for `WriteAsJsonAsync` (same pattern as 409 handler). **NOTE:** No new exception type is needed -- the handler detects DAPR unavailability from the raw exception chain.
  - [ ] 5.6 Register `DaprSidecarUnavailableHandler` in the DI exception handler chain in `ServiceCollectionExtensions.cs`. Insert the registration AFTER `QueryNotFoundExceptionHandler` and BEFORE `GlobalExceptionHandler` (matching the handler chain order documented in Dev Notes). Find the `GlobalExceptionHandler` registration line and add the new handler immediately above it.
  - [ ] 5.8 **Fix `AuthorizationServiceUnavailableHandler.cs`** -- change `cancellationToken` to `CancellationToken.None` in `WriteAsJsonAsync` call (line 51) to match the 409 handler pattern and prevent truncated error responses
  - [ ] 5.9 **IMPORTANT**: Keep server-side logging unchanged -- the `logger.LogError` calls still log internal details (`ActorType`, `ActorId`, `Reason`) for diagnostics. Only the CLIENT RESPONSE hides these.

- [ ] Task 6: Verify UX-DR6 compliance across all handlers (AC: #8, #9)
  - [ ] 6.1 Scan ALL ProblemDetails `title`, `detail`, and extension key values across all error handlers for forbidden terms: "aggregate", "event stream", "event store", "actor", "DAPR", "sidecar", "pub/sub", "state store"
  - [ ] 6.2 Check `DomainCommandRejectedExceptionHandler.cs` -- verify rejection `detail` messages from domain services don't leak terminology
  - [ ] 6.2a **Check `QueryNotFoundExceptionHandler.cs`** -- the detail message likely contains the word "aggregate" (e.g., "No projection found for the requested aggregate."). Replace with a safe alternative.
  - [ ] 6.3 Check `CommandStatusController.cs` error responses for terminology leakage
  - [ ] 6.4 Check `ReplayController.cs` error responses for terminology leakage
  - [ ] 6.5 Verify `Instance` field across all handlers uses `httpContext.Request.Path` (not `httpContext.Request.PathAndQuery` or `httpContext.Request.GetDisplayUrl()`) to avoid leaking query parameter values in error responses
  - [ ] 6.6 Fix any violations found

- [ ] Task 7: Update and add tests (AC: #10)
  - [ ] 7.1 **Update `ConcurrencyConflictExceptionHandlerTests.cs`**: Verify 409 response uses `ProblemTypeUris.ConcurrencyConflict`, verify `aggregateId` and `conflictSource` are NOT in extensions, verify `detail` message does not contain "aggregate"
  - [ ] 7.2 **Update `AuthorizationExceptionHandlerTests.cs`**: Verify 403 response uses `ProblemTypeUris.Forbidden`, verify response names rejected tenant
  - [ ] 7.3 **Update `AuthorizationServiceUnavailableHandlerTests.cs`**: Verify 503 uses `ProblemTypeUris.ServiceUnavailable`, verify `Retry-After: 30`, verify no `correlationId`, verify detail says "command processing pipeline"
  - [ ] 7.4 **Add 401 tests**: Use `WebApplicationFactory<Program>` with real HTTP requests carrying invalid/expired/missing JWTs, then assert response headers and body. Reference existing pattern in `tests/Hexalith.EventStore.IntegrationTests/CommandApi/JwtAuthenticationIntegrationTests.cs`. Alternatively, if keeping to Tier 2, directly invoke the `JwtBearerEvents` delegates by constructing a `JwtBearerChallengeContext` with mocked `HttpContext` and `AuthenticationProperties`. Test scenarios: (a) missing JWT returns 401 with `WWW-Authenticate: Bearer realm="hexalith-eventstore"`, `ProblemTypeUris.AuthenticationRequired`, no `correlationId`; (b) expired JWT returns 401 with expired-specific `WWW-Authenticate` and `ProblemTypeUris.TokenExpired`; (c) invalid JWT returns 401 with invalid-specific `WWW-Authenticate` and `ProblemTypeUris.AuthenticationRequired`.
  - [ ] 7.5 **Add DAPR sidecar unavailability test**: Create an `RpcException(new Grpc.Core.Status(StatusCode.Unavailable, ""))`, optionally wrap it in a `DaprException`, throw it through `DaprSidecarUnavailableHandler.TryHandleAsync` with a mocked `HttpContext`. Assert: 503 status, `Retry-After: 30`, no `correlationId` in ProblemDetails, detail contains "command processing pipeline", type is `ProblemTypeUris.ServiceUnavailable`.
  - [ ] 7.6 **Add UX-DR6 compliance test**: Create a test that scans all ProblemDetails `detail` messages for forbidden terminology. This prevents future regressions.
  - [ ] 7.7 **Add type URI consistency test**: Verify all handlers use `ProblemTypeUris.*` constants, not inline strings
  - [ ] 7.8 **Add 429 rate limiter response test**: After Task 1.8 refactors the rate limiter rejection callback, verify that a rate-limited request returns proper `ProblemDetails` with `ProblemTypeUris.RateLimitExceeded`, `application/problem+json` content type, and `Retry-After` header.
  - [ ] 7.9 **Scan all test files for hardcoded type URI strings**: Search all `tests/` directories for `rfc9457`, `rfc6585`, and `tools.ietf.org` string literals in test assertions. Update any assertions to use `ProblemTypeUris.*` constants or the corresponding Hexalith URI strings. Existing tests that assert on the old generic URIs will fail silently after Task 1 changes if not updated.
  - [ ] 7.10 Run all Tier 1 tests -- **require test output as proof**
  - [ ] 7.11 Run all Tier 2 tests -- **require test output as proof**
  - [ ] **Note:** Tier 3 tests (`IntegrationTests/`) are out of scope -- they require full DAPR + Docker per CLAUDE.md. However, Tier 3 tests that assert on 401 response fields (`correlationId`, `tenantId`, type URIs) will need updating when those tests are next run. This is expected and not a blocker for this story.

## Dev Notes

### Architecture Constraints

- **Rule #5:** Never log command/event payload data -- envelope metadata only (SEC-5)
- **Rule #7:** ProblemDetails for ALL API error responses -- never custom error shapes
- **Rule #9:** correlationId in every structured log entry (server-side) -- BUT NOT in pre-pipeline client responses (401, 503)
- **Rule #13:** No stack traces in production error responses -- `ProblemDetails.detail` contains human-readable message only
- **UX-DR1:** RFC 7807/9457 ProblemDetails on ALL error responses with `type`, `title`, `status`, `detail`, `instance`
- **UX-DR2:** `correlationId` extension on 400, 403, 409 ONLY. Absent on 401, 503 (pre-pipeline rejections)
- **UX-DR4:** `WWW-Authenticate` header on 401 per RFC 6750 with `realm`, `error`, `error_description`
- **UX-DR5:** `Retry-After` header: `1` on 409, `30` on 503
- **UX-DR6:** No event sourcing terminology in any error response
- **UX-DR7:** Error `type` URIs are stable, unique per error category, resolve to documentation
- **UX-DR8:** Distinct `type` URIs for missing (`authentication-required`) vs. expired (`token-expired`) JWT
- **UX-DR9:** 403 names rejected tenant, does NOT enumerate authorized tenants
- **UX-DR10:** 409 leaks no sequence numbers or internal state
- **UX-DR11:** 503 says "command processing pipeline", never internal component names

### Key Distinction: Server-Side Logging vs. Client Response

**The most common mistake in this story is confusing what the SERVER logs with what the CLIENT sees.**

- **Server-side logging** (`ILogger.LogWarning/LogError`): MUST include all diagnostic details -- `correlationId`, `tenantId`, `aggregateId`, `actorType`, `actorId`, `sourceIp`, `reason`. This is for ops debugging. Do NOT strip these.
- **Client response** (ProblemDetails JSON body + HTTP headers): MUST be sanitized per UX-DR rules. No internal component names, no implementation details, selective `correlationId` inclusion.

### Handler Registration Order

The exception handler chain in `ServiceCollectionExtensions.cs` processes in DI registration order. Current chain:

```
1. ValidationExceptionHandler              -> 400
2. AuthorizationServiceUnavailableHandler  -> 503 (auth actor down)
3. AuthorizationExceptionHandler           -> 403
4. ConcurrencyConflictExceptionHandler     -> 409
5. DomainCommandRejectedExceptionHandler   -> 422/409/404
6. QueryNotFoundExceptionHandler           -> 404
7. [NEW] DaprSidecarUnavailableHandler     -> 503 (sidecar down) -- register AFTER QueryNotFoundExceptionHandler
8. GlobalExceptionHandler                  -> 500 (catchall -- MUST remain last)
```

The new DAPR sidecar handler MUST be registered before `GlobalExceptionHandler` or sidecar failures will surface as generic 500 errors.

### DAPR Sidecar Unavailability Detection

When the DAPR sidecar is down, `DaprClient` calls fail with gRPC errors. Possible exception types to detect:
- `Grpc.Core.RpcException` with `StatusCode.Unavailable`
- `System.Net.Http.HttpRequestException` (connection refused)
- `Dapr.DaprException` wrapping gRPC errors

Check `Dapr.Client` namespace for the actual exception type. The handler should walk the InnerException chain (same pattern as `ConcurrencyConflictExceptionHandler.FindConcurrencyConflict`) looking for gRPC `Unavailable` status codes.

### RFC 6750 WWW-Authenticate Header Format

Per RFC 6750 Section 3:
```
WWW-Authenticate: Bearer realm="hexalith-eventstore"
WWW-Authenticate: Bearer realm="hexalith-eventstore", error="invalid_token", error_description="The token has expired"
```

In ASP.NET Core, set via:
```csharp
context.Response.Headers.WWWAuthenticate = "Bearer realm=\"hexalith-eventstore\"";
// or for expired:
context.Response.Headers.WWWAuthenticate = "Bearer realm=\"hexalith-eventstore\", error=\"invalid_token\", error_description=\"The token has expired\"";
```

Note: The default JWT Bearer middleware adds a basic `WWW-Authenticate: Bearer` header, but we've suppressed it with `context.HandleResponse()` in OnChallenge. We need to add our own enriched header.

### Error Type URI Constants (for reference)

```csharp
public static class ProblemTypeUris
{
    public const string ValidationError = "https://hexalith.io/problems/validation-error";
    public const string AuthenticationRequired = "https://hexalith.io/problems/authentication-required";
    public const string TokenExpired = "https://hexalith.io/problems/token-expired";
    public const string BadRequest = "https://hexalith.io/problems/bad-request";
    public const string Forbidden = "https://hexalith.io/problems/forbidden";
    public const string NotFound = "https://hexalith.io/problems/not-found";
    public const string ConcurrencyConflict = "https://hexalith.io/problems/concurrency-conflict";
    public const string RateLimitExceeded = "https://hexalith.io/problems/rate-limit-exceeded";
    public const string ServiceUnavailable = "https://hexalith.io/problems/service-unavailable";
    public const string CommandStatusNotFound = "https://hexalith.io/problems/command-status-not-found";
    public const string InternalServerError = "https://hexalith.io/problems/internal-server-error";
}
```

### 429 Rate Limiter Callback Complexity

The rate limiter rejection callback in `ServiceCollectionExtensions.cs` runs inside `OnRejected` which provides a `OnRejectedContext` (not a standard `HttpContext` action filter). The `WriteAsJsonAsync` extension is available on `HttpResponse` so it works, but the current code constructs an anonymous type rather than `ProblemDetails`. When refactoring (Task 1.8), ensure:
- The `OnRejected` lambda has access to `HttpContext` via `context.HttpContext`
- `WriteAsJsonAsync` is called with explicit `"application/problem+json"` content type
- The `Retry-After` header calculation from `RateLimitMetadata` is preserved
- Test the callback by verifying the response body deserializes as valid `ProblemDetails`

### Concurrency Conflict Safe Detail Message

Replace the current detail (which contains "aggregate" and leaks the optimistic locking model):
```
BEFORE: "An optimistic concurrency conflict occurred on aggregate '{aggregateId}'. Another command was processed for this aggregate between read and write. Retry the command to process against the updated state."
AFTER:  "A concurrency conflict occurred. Please retry the command."
```
The shorter message avoids both event sourcing terminology ("aggregate") AND implementation model leakage ("between read and write" reveals optimistic locking).

### Previous Story Intelligence (Story 3.4)

- **Test count baseline:** Tier 1 = 656 pass, Tier 2 = 1297 pass (21 pre-existing DAPR infra failures)
- **DAPR CLI:** v1.17.0, Runtime: v1.17.1
- **Code style:** Egyptian braces throughout (NOT Allman despite `.editorconfig` claiming Allman), file-scoped namespaces, `ConfigureAwait(false)` on all async calls
- **Error response pattern:** RFC 7807/9457 `ProblemDetails` with `application/problem+json` content type
- **Advisory write pattern:** Status writes wrapped in try/catch (rule #12), cancellation-aware
- **CancellationToken.None for responses:** The 409 handler uses `CancellationToken.None` for `WriteAsJsonAsync` to ensure the full ProblemDetails body is written. Apply the same pattern for 503.
- **Exception unwrapping pattern:** Walk InnerException chain with max depth 10, check AggregateException.InnerExceptions (see `ConcurrencyConflictExceptionHandler.FindConcurrencyConflict`)
- **Content type caveat:** Must explicitly pass `"application/problem+json"` to `WriteAsJsonAsync` -- the parameterless overload always uses `application/json`

### Key File Locations

| Purpose | Path |
|---------|------|
| JWT auth configuration | `src/Hexalith.EventStore.CommandApi/Authentication/ConfigureJwtBearerOptions.cs` |
| 403 handler | `src/Hexalith.EventStore.CommandApi/ErrorHandling/AuthorizationExceptionHandler.cs` |
| 403 exception | `src/Hexalith.EventStore.CommandApi/ErrorHandling/CommandAuthorizationException.cs` |
| 409 handler | `src/Hexalith.EventStore.CommandApi/ErrorHandling/ConcurrencyConflictExceptionHandler.cs` |
| 409 exception | `src/Hexalith.EventStore.Server/Commands/ConcurrencyConflictException.cs` |
| 503 auth handler | `src/Hexalith.EventStore.CommandApi/ErrorHandling/AuthorizationServiceUnavailableHandler.cs` |
| 503 auth exception | `src/Hexalith.EventStore.CommandApi/ErrorHandling/AuthorizationServiceUnavailableException.cs` |
| 400 validation factory | `src/Hexalith.EventStore.CommandApi/ErrorHandling/ValidationProblemDetailsFactory.cs` |
| 500 global handler | `src/Hexalith.EventStore.CommandApi/ErrorHandling/GlobalExceptionHandler.cs` |
| 404 query handler | `src/Hexalith.EventStore.CommandApi/ErrorHandling/QueryNotFoundExceptionHandler.cs` |
| Domain rejection handler | `src/Hexalith.EventStore.CommandApi/ErrorHandling/DomainCommandRejectedExceptionHandler.cs` |
| DI registration | `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` |
| Command controller | `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` |
| Status controller | `src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs` |
| Replay controller | `src/Hexalith.EventStore.CommandApi/Controllers/ReplayController.cs` |
| 409 handler tests | `tests/Hexalith.EventStore.Server.Tests/Commands/ConcurrencyConflictExceptionHandlerTests.cs` |
| 403 handler tests | `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/AuthorizationExceptionHandlerTests.cs` |
| 503 handler tests | `tests/Hexalith.EventStore.Server.Tests/Authorization/AuthorizationServiceUnavailableHandlerTests.cs` |
| 503 exception tests | `tests/Hexalith.EventStore.Server.Tests/Authorization/AuthorizationServiceUnavailableExceptionTests.cs` |
| Validation handler tests | `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/ValidationExceptionHandlerTests.cs` |
| 404 handler tests | `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/QueryNotFoundExceptionHandlerTests.cs` |

### Testing Patterns

- **Framework:** xUnit 2.9.3 with Shouldly 4.3.0 assertions and NSubstitute 5.3.0 mocking
- **HTTP context mocking:** Use `DefaultHttpContext` with manually configured `Items`, `Response.Body = new MemoryStream()`, and `Request` properties
- **Response body reading:** After `WriteAsJsonAsync`, seek `Response.Body` to 0 and deserialize with `JsonDocument` to assert ProblemDetails fields
- **Header assertions:** `httpContext.Response.Headers["Retry-After"].ToString().ShouldBe("30")`
- **WWW-Authenticate assertions:** `httpContext.Response.Headers.WWWAuthenticate.ToString().ShouldContain("Bearer realm=")`

### Project Structure Notes

- Egyptian braces (NOT Allman) -- follow existing handler code exactly
- File-scoped namespaces (`namespace X.Y.Z;`)
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for parameter validation
- Source-generated logging via `[LoggerMessage]` attributes on partial methods
- `CancellationToken.None` when writing authoritative error responses (prevent truncated bodies)

### References

- [Source: _bmad-output/planning-artifacts/epics.md - Story 3.5: Concurrency, Auth & Infrastructure Error Responses]
- [Source: _bmad-output/planning-artifacts/epics.md - UX Design Requirements UX-DR1 through UX-DR11]
- [Source: _bmad-output/planning-artifacts/architecture.md - D5: Error Response Format]
- [Source: _bmad-output/planning-artifacts/architecture.md - Cross-Cutting Rules #7, #9, #13]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md - v1 API Error Experience]
- [Source: _bmad-output/implementation-artifacts/3-4-dead-letter-routing-and-command-replay.md - Previous story patterns and test baseline]
- [Source: src/Hexalith.EventStore.CommandApi/ErrorHandling/ - All existing error handlers]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
