# Story 17.3: Refactor AuthorizationBehavior

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **deployment operator**,
I want **the authorization pipeline consolidated into the MediatR `AuthorizationBehavior` via the `ITenantValidator` and `IRbacValidator` abstractions, with the controller-level duplicate tenant check removed**,
so that **all authorization decisions flow through a single pluggable layer (Layer 4), enabling seamless switching between claims-based and actor-based authorization via configuration alone**.

## Acceptance Criteria

1. **`AuthorizationBehavior` injects `ITenantValidator`** via constructor and calls `ValidateAsync(user, command.Tenant, cancellationToken)` — if `!IsAuthorized`, throws `CommandAuthorizationException` with the validator's `Reason`
2. **`AuthorizationBehavior` injects `IRbacValidator`** via constructor and calls `ValidateAsync(user, command.Tenant, command.Domain, command.CommandType, "command", cancellationToken)` — if `!IsAuthorized`, throws `CommandAuthorizationException` with the validator's `Reason`
3. **`AuthorizationBehavior` inline domain/permission checking code (lines 45-84) is completely removed** — replaced by the `IRbacValidator` call
4. **`CommandsController.Submit()` inline tenant check (lines 39-64) is completely removed** — including the `CreateForbiddenProblemDetails` helper and `LogTenantAuthorizationFailure` helper, since all auth goes through the behavior
5. **`CommandsController` retains non-auth concerns only:** `HttpContext.Items["RequestTenantId"]` assignment (needed by rate limiter), `userId` extraction, extension metadata sanitization, `SubmitCommand` creation, and MediatR send
6. **Existing `AuthorizationBehaviorTests.cs` assertions remain semantically identical** — constructor updated to inject claims-based validators, all 10 test scenarios produce the same outcomes
7. **New regression tests prove behavioral equivalence:** identical inputs to `ITenantValidator.ValidateAsync` and `IRbacValidator.ValidateAsync` produce the same accept/deny decisions as the pre-refactoring inline code
8. **`CommandsControllerTenantTests.cs` (7 characterization tests from Story 17-1) are reworked** — the inline tenant check they characterized no longer exists; tests are replaced with delegation verification tests proving requests pass through to MediatR pipeline where behavior handles authorization
9. **All existing Tier 1 and Tier 2 tests continue to pass** with zero behavioral change in authorization outcomes
10. **503 Service Unavailable flow works end-to-end**: when actor-based validators are configured and the actor is unreachable, `AuthorizationServiceUnavailableException` propagates from behavior to `AuthorizationServiceUnavailableHandler` → 503 with `Retry-After`
11. **DI integration test** confirms `AuthorizationBehavior<SubmitCommand, SubmitCommandResult>` resolves from the real DI container with `ITenantValidator` and `IRbacValidator` correctly injected — proving the full MediatR pipeline resolves without runtime errors

## Tasks / Subtasks

- [x] Task 1: Refactor `AuthorizationBehavior` constructor and tenant validation (AC: #1, #3)
    - [x] 1.1 Add `ITenantValidator tenantValidator` and `IRbacValidator rbacValidator` to the primary constructor. Add `using Hexalith.EventStore.CommandApi.Authorization;` — use the `CommandApi.Authorization` namespace, NOT `Server.Actors.ITenantValidator` (different concern, different namespace — see Story 17-1 naming collision note)
    - [x] 1.2 After authentication check, call `tenantValidator.ValidateAsync(user, command.Tenant, cancellationToken)`
    - [x] 1.3 On `!IsAuthorized`, log via `Log.AuthorizationFailed` and throw `CommandAuthorizationException` with `tenantResult.Reason`
    - [x] 1.4 Remove the tenant claims collection (lines 40-43) that was used only for logging context — move claim reading into the logging path only (when RBAC denial needs tenantClaims for the log message)
- [x] Task 2: Replace inline RBAC logic with `IRbacValidator` call (AC: #2, #3)
    - [x] 2.1 Replace domain check (lines 45-62) and permission check (lines 64-84) with `rbacValidator.ValidateAsync(user, command.Tenant, command.Domain, command.CommandType, "command", cancellationToken)`
    - [x] 2.2 On `!IsAuthorized`, log via `Log.AuthorizationFailed` and throw `CommandAuthorizationException` with `rbacResult.Reason`
    - [x] 2.3 Preserve `Log.AuthorizationPassed` call on success
    - [x] 2.4 Remove the `using Hexalith.EventStore.CommandApi.Pipeline;` import for `AuthorizationConstants` if no longer needed directly (constants now used inside `ClaimsRbacValidator`)
- [x] Task 3: Remove inline tenant check from `CommandsController` (AC: #4, #5)
    - [x] 3.1 Remove lines 39-64 (authentication context check, authentication check, tenant claims extraction, tenant validation, and `AuthorizedTenant` item)
    - [x] 3.2 Remove `CreateForbiddenProblemDetails` helper method
    - [x] 3.3 Remove `LogTenantAuthorizationFailure` helper method
    - [x] 3.4 **KEEP** `[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]` — 403s still occur via `AuthorizationExceptionHandler`; exception handler responses do NOT auto-appear in OpenAPI; removing the attribute would hide 403 from API documentation. **ADD** `[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]` to document the actor-based auth unavailable scenario in OpenAPI
    - [x] 3.5 **Keep** `HttpContext.Items["RequestTenantId"] = request.Tenant` — needed by rate limiter `OnRejected` callback
    - [x] 3.6 **MANDATORY: Search ALL consumers of `HttpContext.Items["AuthorizedTenant"]`** before removal — `grep -r "AuthorizedTenant"` across entire solution. If ANY downstream middleware, filter, or handler reads this item for security decisions, removing it creates an authorization bypass. If unused, remove. If used, move the assignment into behavior (after successful tenant validation) or a post-auth middleware.
- [x] Task 4: Update `AuthorizationBehaviorTests.cs` (AC: #6, #7)
    - [x] 4.1 Update `CreateBehavior` helper to instantiate `ClaimsTenantValidator` and `ClaimsRbacValidator`, injecting them into the new constructor
    - [x] 4.2 **CRITICAL: Also update `NonSubmitCommandRequest_PassesThrough` test** (line 177-192) — this test creates the behavior DIRECTLY without using the `CreateBehavior` helper, so it also needs the new constructor parameters (`tenantValidator`, `rbacValidator`). Missing this causes a compile error.
    - [x] 4.3 Verify all 10 existing test methods produce identical pass/fail results with no assertion changes
    - [x] 4.4 Add new test: `AuthorizationBehavior_UserWithNoTenantClaims_ThrowsAuthorizationException` — since tenant validation now happens in behavior
    - [x] 4.5 Add new test: `AuthorizationBehavior_UserWithWrongTenant_ThrowsAuthorizationException` — tenant mismatch
    - [x] 4.6 Add new test: `AuthorizationBehavior_UserWithMatchingTenant_Succeeds` — explicit tenant success scenario
    - [x] 4.7 Add new test: `AuthorizationBehavior_TenantPasses_RbacFails_ThrowsAuthorizationException` — validates the sequential tenant-then-RBAC flow within a single behavior invocation (this is the most important NEW scenario — previously these checks happened in different layers)
    - [x] 4.8 Add NSubstitute mock test(s) to verify behavior calls `ITenantValidator.ValidateAsync` and `IRbacValidator.ValidateAsync` with the correct parameters (command.Tenant, command.Domain, command.CommandType, "command") — proves delegation, not just outcome correctness
- [x] Task 5: Rework `CommandsControllerTenantTests.cs` (AC: #8)
    - [x] 5.1 The 7 characterization tests characterized inline tenant checks that NO LONGER EXIST in the controller. Replace them with delegation verification tests:
        - (a) Verify that a request WITHOUT valid tenant claims reaches MediatR and fails with `CommandAuthorizationException` (caught by `AuthorizationExceptionHandler` → 403), OR
        - (b) Replace with behavior-level tests if controller tests are redundant with Task 4's behavior tests
    - [x] 5.2 Decide approach and implement — prefer (a) to prove the controller delegates correctly
- [x] Task 6: DI integration test (AC: #11)
    - [x] 6.1 Add test in `CommandApiAuthorizationRegistrationTests.cs` (or new file) that builds a real `ServiceProvider` from `AddCommandApi()`, resolves `IPipelineBehavior<SubmitCommand, SubmitCommandResult>`, and confirms `AuthorizationBehavior` receives non-null `ITenantValidator` and `IRbacValidator`
    - [x] 6.2 Test with default config (null actor names) → confirms `ClaimsTenantValidator` + `ClaimsRbacValidator` injected
    - [x] 6.3 Add defensive test: if `ValidateAsync` returns null (unexpected), behavior throws `InvalidOperationException` (server bug → 500 via `GlobalExceptionHandler`), NOT `CommandAuthorizationException` (which would produce a misleading 403) and NOT `NullReferenceException`
- [x] Task 7: Verify zero regression (AC: #9, #10)
    - [x] 7.1 Run all Tier 1 tests: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ tests/Hexalith.EventStore.Client.Tests/ tests/Hexalith.EventStore.Sample.Tests/ tests/Hexalith.EventStore.Testing.Tests/`
    - [x] 7.2 Run Tier 2 tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/`
    - [x] 7.3 Confirm 503 flow: actor-based validator configured → actor unreachable → behavior lets `AuthorizationServiceUnavailableException` propagate → handler returns 503

## Dev Notes

### Critical: This is a Mechanical Refactoring — Preserve All Authorization Semantics

The refactoring replaces HOW authorization decisions are executed (inline code → interface calls) without changing WHAT decisions are made. Claims-based validators use code extracted in Story 17-1 with identical logic. The behavior change is:

**Before (dual-layer):**

```text
Controller (Layer 3): Tenant check → 403 ProblemDetails (direct return)
Behavior (Layer 4):   Domain + Permission check → throw → handler → 403
```

**After (single-layer):**

```text
Controller (Layer 3): [Authorize] attribute only (identity verification)
Behavior (Layer 4):   ITenantValidator + IRbacValidator → throw → handler → 403
```

### Why Consolidate: Query Endpoint Reuse

This consolidation is not just cleanup — it's prerequisite architecture. Story 17-5 (`POST /api/v1/queries`) will route through the same MediatR pipeline with `messageCategory: "query"`. By consolidating all authorization into the behavior NOW, the query endpoint gets tenant + RBAC validation for free. Without this refactoring, tenant validation would need to be duplicated in `QueriesController` — exactly the kind of code duplication this story eliminates.

### Pipeline Ordering Change — Intentional

**Before:** Controller runs tenant check FIRST → then MediatR pipeline (logging → validation → RBAC auth)
**After:** MediatR pipeline runs in order: logging → validation → authorization (tenant + RBAC)

This means:

- A request that is both **malformed AND unauthorized** now gets **400 (validation)** instead of **403 (auth)**
- An unauthorized request IS logged by `LoggingBehavior` before being rejected by `AuthorizationBehavior`

Both are intentional and acceptable:

- Rejecting malformed requests before auth is standard practice (less work for auth validators)
- Logging unauthorized attempts is desirable for security audit trails
- The `LoggingBehavior` only logs command metadata (tenant, domain, type) — same info the caller already provided

### Exception Handling: What Happens When Validators Throw

| Exception Type                             | Source                               | Handler                                  | HTTP Status |
| ------------------------------------------ | ------------------------------------ | ---------------------------------------- | ----------- |
| `CommandAuthorizationException`            | Behavior (auth denied)               | `AuthorizationExceptionHandler`          | 403         |
| `AuthorizationServiceUnavailableException` | Actor validators (actor unreachable) | `AuthorizationServiceUnavailableHandler` | 503         |
| Any other exception                        | Unexpected validator failure         | `GlobalExceptionHandler`                 | 500         |

The behavior MUST NOT catch any exception from validators. Let all exceptions propagate to the registered handlers. The handler registration order in `ServiceCollectionExtensions` is correct: 503 handler before 403 handler before global handler.

### Atomic Deployment Requirement

The controller change (remove inline tenant check) and the behavior change (add tenant validation) MUST deploy together. A partial deployment where the controller change is live but the behavior hasn't been updated would bypass tenant validation entirely — the `[Authorize]` attribute only checks JWT identity, not tenant access. Implementing both in a single commit ensures atomic deployment.

### String Comparison Asymmetry — MUST Be Preserved

The validators (extracted in Story 17-1) already preserve this:

- **Tenant** check: `StringComparison.Ordinal` (case-SENSITIVE) — in `ClaimsTenantValidator`
- **Domain** check: `StringComparison.OrdinalIgnoreCase` (case-INSENSITIVE) — in `ClaimsRbacValidator`
- **Permission** check: `StringComparison.OrdinalIgnoreCase` (case-INSENSITIVE) — in `ClaimsRbacValidator`

**Do NOT introduce any normalization.** The asymmetry is intentional and already unit-tested.

### Error Response Format Parity

Controller and exception handler produce compatible 403 responses:

| Aspect       | Controller (before)         | ExceptionHandler (after)           |
| ------------ | --------------------------- | ---------------------------------- |
| Status       | 403                         | 403                                |
| Content-Type | `application/problem+json`  | `application/problem+json`         |
| Detail       | Validator's reason text     | `authException.Reason` (same text) |
| Extensions   | `correlationId`, `tenantId` | `correlationId`, `tenantId`        |

Story 17-1 review aligned `ClaimsTenantValidator` denial messages with the controller's exact messages:

- `"No tenant authorization claims found. Access denied."`
- `"Not authorized to submit commands for tenant '{tenantId}'."`

These flow unchanged through `CommandAuthorizationException.Reason` → `AuthorizationExceptionHandler` → `ProblemDetails.Detail`.

### Subtle Behavioral Difference: Serialization Path — Acceptable

- **Before (controller):** `ObjectResult` → MVC content negotiation → JSON formatter
- **After (handler):** `WriteAsJsonAsync` → `System.Text.Json` directly

Both produce valid `application/problem+json` with identical fields (`status`, `title`, `type`, `detail`, `instance`, `extensions.correlationId`, `extensions.tenantId`). The exact JSON byte representation (property order, whitespace, escaping) MAY differ between MVC serialization and direct `System.Text.Json`. Any API client parsing the status code + `detail` field will not break. If a test verifies exact JSON bytes, it needs updating — test the semantic content instead.

### Subtle Behavioral Difference: Double Logging — Intentional Consistency

After refactoring, tenant auth failures will be logged TWICE:

1. `AuthorizationBehavior.Log.AuthorizationFailed` (before throwing) — structured with correlation/causation IDs, tenant claims CSV, source IP
2. `AuthorizationExceptionHandler` logs at Warning level (when catching) — structured with security event, correlation ID, tenant, domain, command type, reason

This is ALREADY the existing behavior for domain and permission failures today. The refactoring makes tenant denial logging consistent with RBAC denial logging. Do NOT attempt to deduplicate — the two log entries serve different purposes (pipeline context vs handler context).

### HttpContext Items Handling

| Item               | Kept in Controller? | Reason                                                                                        |
| ------------------ | ------------------- | --------------------------------------------------------------------------------------------- |
| `RequestTenantId`  | **YES**             | Used by rate limiter `OnRejected` callback (line 152-154 of `ServiceCollectionExtensions.cs`) |
| `AuthorizedTenant` | **INVESTIGATE**     | Search all consumers. If unused, remove. If needed, set after successful behavior auth.       |

### 503 Service Unavailable Flow

When actor-based validators are configured:

1. `AuthorizationBehavior` calls `ITenantValidator.ValidateAsync()` or `IRbacValidator.ValidateAsync()`
2. Actor-based implementations (`ActorTenantValidator`, `ActorRbacValidator`) call DAPR actor proxy
3. If actor unreachable → throws `AuthorizationServiceUnavailableException`
4. Exception propagates OUT of behavior (NOT caught as CommandAuthorizationException)
5. `AuthorizationServiceUnavailableHandler` (registered BEFORE `AuthorizationExceptionHandler` in DI) catches it → 503 + `Retry-After`

**The behavior MUST NOT catch `AuthorizationServiceUnavailableException`.** Let it propagate.

### Namespace Disambiguation — Critical

The project has TWO `ITenantValidator` interfaces in different namespaces:

- `Hexalith.EventStore.CommandApi.Authorization.ITenantValidator` — **THIS ONE** (API-level authorization, async, returns result object)
- `Hexalith.EventStore.Server.Actors.ITenantValidator` — DIFFERENT concern (defense-in-depth actor-level tenant isolation, synchronous, void)

The behavior MUST import `using Hexalith.EventStore.CommandApi.Authorization;`. If both namespaces are somehow in scope, use the fully qualified name. Story 17-1 documented this collision — see "Naming Collision Avoidance" section.

### Validator Denial Reasons — Pass-Through, No Double-Concatenation

The behavior receives `tenantResult.Reason` or `rbacResult.Reason` from the validator and passes it directly to `CommandAuthorizationException.Reason`. The exception handler then uses `authException.Reason` as `ProblemDetails.Detail`. The reason text flows through unchanged — the behavior does NOT prepend, append, or wrap the reason. This ensures the API consumer sees exactly the validator's denial message.

### Constructor Change Impact

```csharp
// BEFORE (current):
public partial class AuthorizationBehavior<TRequest, TResponse>(
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)

// AFTER (refactored):
public partial class AuthorizationBehavior<TRequest, TResponse>(
    IHttpContextAccessor httpContextAccessor,
    ITenantValidator tenantValidator,
    IRbacValidator rbacValidator,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
```

DI resolution: Both `ITenantValidator` and `IRbacValidator` are already registered in `ServiceCollectionExtensions.AddCommandApi()` (Story 17-1 + 17-2). The MediatR pipeline resolves the behavior via DI, so the new parameters are injected automatically. No DI registration changes needed.

### Refactored Behavior Pseudocode

```csharp
public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) {
    if (request is not SubmitCommand command)
        return await next();

    // Get user from HttpContext (existing code)
    HttpContext httpContext = httpContextAccessor.HttpContext ?? throw ...;
    ClaimsPrincipal user = httpContext.User ?? throw ...;

    if (user.Identity?.IsAuthenticated != true)
        throw new CommandAuthorizationException(command.Tenant, null, null, "User is not authenticated.");

    string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? "unknown";
    string causationId = correlationId;
    string? sourceIp = httpContext.Connection.RemoteIpAddress?.ToString();

    // NEW: Tenant validation (moved from controller)
    TenantValidationResult tenantResult = await tenantValidator
        .ValidateAsync(user, command.Tenant, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException(
            "ITenantValidator.ValidateAsync returned null. This is a server bug, not a user authorization failure.");
    if (!tenantResult.IsAuthorized) {
        Log.AuthorizationFailed(logger, correlationId, causationId, "N/A",
            command.Tenant, command.Domain, command.CommandType,
            tenantResult.Reason ?? "Tenant access denied.", sourceIp);
        throw new CommandAuthorizationException(
            command.Tenant, command.Domain, command.CommandType,
            tenantResult.Reason ?? "Tenant access denied.");
    }

    // REFACTORED: RBAC validation (was inline domain + permission checks)
    RbacValidationResult rbacResult = await rbacValidator
        .ValidateAsync(user, command.Tenant, command.Domain,
            command.CommandType, "command", cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException(
            "IRbacValidator.ValidateAsync returned null. This is a server bug, not a user authorization failure.");
    if (!rbacResult.IsAuthorized) {
        // Collect tenant claims for logging context only
        var tenantClaims = user.FindAll("eventstore:tenant")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
        string tenantClaimsCsv = tenantClaims.Count == 0 ? "none" : string.Join(",", tenantClaims);
        Log.AuthorizationFailed(logger, correlationId, causationId, tenantClaimsCsv,
            command.Tenant, command.Domain, command.CommandType,
            rbacResult.Reason ?? "RBAC check failed.", sourceIp);
        throw new CommandAuthorizationException(
            command.Tenant, command.Domain, command.CommandType,
            rbacResult.Reason ?? "RBAC check failed.");
    }

    Log.AuthorizationPassed(logger, correlationId, causationId,
        command.Tenant, command.Domain, command.CommandType);
    return await next().ConfigureAwait(false);
}
```

### Refactored Controller Pseudocode

```csharp
public async Task<IActionResult> Submit([FromBody] SubmitCommandRequest request, CancellationToken ct) {
    ArgumentNullException.ThrowIfNull(request);

    string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
        ?? Guid.NewGuid().ToString();

    // KEPT: Store tenant for rate limiter error handler
    if (!string.IsNullOrEmpty(request.Tenant))
        HttpContext.Items["RequestTenantId"] = request.Tenant;

    // REMOVED: All inline auth checks (now in AuthorizationBehavior)
    // [Authorize] attribute ensures JWT identity (Layer 3)
    // AuthorizationBehavior handles tenant + RBAC (Layer 4)

    string userId = User.FindFirst("sub")?.Value ?? "unknown";
    // ... userId warning log (unchanged) ...

    // SEC-4: Extension metadata sanitization (unchanged)
    SanitizeResult sanitizeResult = extensionSanitizer.Sanitize(request.Extensions);
    // ... sanitization handling (unchanged) ...

    var command = new SubmitCommand(...);
    SubmitCommandResult result = await mediator.Send(command, ct).ConfigureAwait(false);
    // ... Location header, Accepted response (unchanged) ...
}
```

### Existing Test Patterns

From `AuthorizationBehaviorTests.cs`:

- `DefaultHttpContext` with `ClaimsPrincipal` for HTTP context mocking
- `NSubstitute` for `IHttpContextAccessor`
- `Shouldly` assertions
- `TestLogger<T>` helper class
- `LogEntry(LogLevel, string)` record type (from `LoggingBehaviorTests.cs` — `internal` access)
- Naming: `ClassName_Scenario_ExpectedResult()`

Updated `CreateBehavior` helper:

```csharp
private AuthorizationBehavior<SubmitCommand, SubmitCommandResult> CreateBehavior(ClaimsPrincipal principal) {
    var httpContext = new DefaultHttpContext { User = principal };
    httpContext.Items["CorrelationId"] = "test-correlation-id";
    IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
    _ = accessor.HttpContext.Returns(httpContext);
    // NEW: inject claims-based validators for behavioral equivalence
    var tenantValidator = new ClaimsTenantValidator();
    var rbacValidator = new ClaimsRbacValidator();
    return new AuthorizationBehavior<SubmitCommand, SubmitCommandResult>(
        accessor, tenantValidator, rbacValidator, _logger);
}
```

### Test File Locations

```text
tests/Hexalith.EventStore.Server.Tests/
├── Pipeline/
│   └── AuthorizationBehaviorTests.cs       # MODIFY — update constructor, add tenant scenarios
├── Authorization/
│   ├── ClaimsTenantValidatorTests.cs       # UNCHANGED (Story 17-1)
│   └── ClaimsRbacValidatorTests.cs         # UNCHANGED (Story 17-1)
├── Configuration/
│   └── EventStoreAuthorizationOptionsTests.cs  # UNCHANGED (Story 17-1)
│   └── CommandApiAuthorizationRegistrationTests.cs  # UNCHANGED (Story 17-1)
├── Controllers/
│   └── CommandsControllerTenantTests.cs    # MODIFY — update for removed inline checks
```

### Files to Modify

```text
src/Hexalith.EventStore.CommandApi/
├── Pipeline/
│   └── AuthorizationBehavior.cs            # MAJOR — inject validators, replace inline logic
├── Controllers/
│   └── CommandsController.cs               # MAJOR — remove inline tenant check + helpers

tests/Hexalith.EventStore.Server.Tests/
├── Pipeline/
│   └── AuthorizationBehaviorTests.cs       # MODERATE — update constructor, add tenant tests
├── Controllers/
│   └── CommandsControllerTenantTests.cs    # MODERATE — update for delegation pattern
```

### Files NOT to Modify

- `ServiceCollectionExtensions.cs` — DI already wired from Stories 17-1 + 17-2
- `ClaimsTenantValidator.cs` — extracted logic, working and tested
- `ClaimsRbacValidator.cs` — extracted logic, working and tested
- `ActorTenantValidator.cs` / `ActorRbacValidator.cs` — implementations from Story 17-2
- `AuthorizationExceptionHandler.cs` — already handles `CommandAuthorizationException`
- `AuthorizationServiceUnavailableHandler.cs` — already handles 503 flow
- `AuthorizationConstants.cs` — constants still used by `ClaimsRbacValidator`

### Project Structure Notes

- No new source files created — this is a refactoring story
- DI integration tests may be added to the existing `CommandApiAuthorizationRegistrationTests.cs` or a new test file if needed
- Namespace `Hexalith.EventStore.CommandApi.Authorization` already contains all validator types
- Authorization imports added to `AuthorizationBehavior.cs`: `using Hexalith.EventStore.CommandApi.Authorization;`

### Backward Compatibility

- Claims-based is still the DEFAULT. No configuration changes needed for existing deployments.
- No endpoint behavior changes visible to API consumers (same 403 ProblemDetails format).
- No JWT claim format changes.
- No NuGet package API changes.
- When actor-based validators are configured, the behavior automatically uses them via DI — no additional wiring needed.

### Previous Story Intelligence

**From Story 17-1 (done):**

- `ClaimsTenantValidator` denial messages were aligned with exact `CommandsController` messages during review — this ensures the refactoring produces identical 403 response text.
- `ClaimsRbacValidator` produces identical results for "command" and "query" messageCategory values — safe to hardcode "command" in the behavior call.
- Pre-existing Server.Tests regressions (84 build errors from `IDeadLetterPublisher` / `IEventPayloadProtectionService` constructor changes) were fixed as part of Story 17-1. These are now stable.
- The `NoOpEventPayloadProtectionService` fix across Server test fixtures is in place — do not re-introduce `NSubstitute` unconfigured substitutes for `IEventPayloadProtectionService`.

**From Story 17-2 (review):**

- `ActorTenantValidator` and `ActorRbacValidator` throw `AuthorizationServiceUnavailableException` when actor is unreachable — the behavior MUST NOT catch this exception (let it propagate to the handler).
- `ActorTenantValidator` extracts userId from `ClaimTypes.NameIdentifier` — different from controller's `User.FindFirst("sub")`. These are the SAME claim (`sub` == `NameIdentifier` in JWT). No conflict.
- `FakeTenantValidatorActor` and `FakeRbacValidatorActor` in Testing package available for integration testing.

### Architecture: Six-Layer Auth Model After Refactoring

```text
Layer 1: TLS/HTTPS (infrastructure) — UNCHANGED
Layer 2: JWT Authentication (identity verification) — UNCHANGED
Layer 3: Endpoint Authorization — [Authorize] attribute (identity only) — SIMPLIFIED
Layer 4: MediatR AuthorizationBehavior → ITenantValidator + IRbacValidator — CONSOLIDATED
Layer 5: DAPR Actor tenant validation (defense-in-depth) — UNCHANGED
Layer 6: DAPR access control policies (infrastructure) — UNCHANGED
```

### Regression Test Strategy

Four independent proofs of correctness:

1. **Existing behavior test assertions pass:** `AuthorizationBehaviorTests.cs` assertions unchanged (only constructor setup changes). Same inputs → same pass/deny outcomes.
2. **Validator unit tests (from Story 17-1) verify extraction correctness:** `ClaimsTenantValidatorTests` and `ClaimsRbacValidatorTests` prove the extracted logic matches the original inline code.
3. **DI integration test:** Real `ServiceProvider` from `AddCommandApi()` resolves `AuthorizationBehavior` with correct validators injected — proves the MediatR pipeline resolves without runtime errors.
4. **Full Tier 2 test run:** All 885+ Server.Tests continue to pass after the refactoring.

### Scope Boundary

**IN scope:** Refactor `AuthorizationBehavior` to use validators, remove controller inline auth, update existing tests, verify regression.

**Do NOT generalize the `request is not SubmitCommand` type check.** The behavior currently only handles `SubmitCommand`. Story 17-5 will extend it to also handle query requests (e.g., `is SubmitQuery` with `messageCategory: "query"`). Premature generalization now would break the pipeline for non-command requests that should pass through without authorization.

**OUT of scope (later stories):**

- Query endpoint (`POST /api/v1/queries`) → Story 17-5 (will reuse behavior with "query" messageCategory)
- Validation endpoints (`/commands/validate`, `/queries/validate`) → Stories 17-7, 17-8
- Query contracts → Story 17-4
- Projection actor contract → Story 17-6
- Integration/E2E tests for actor-based flow → Story 17-9

### References

- [Source: sprint-change-proposal-2026-03-08-auth-query.md — Section 4.2, 4.3 Story 17-3]
- [Source: 17-1-authorization-options-and-validator-abstractions.md — Extraction Points, Interface Design, DI Registration]
- [Source: 17-2-actor-based-validator-implementations.md — Actor validator patterns, 503 flow, DI updates]
- [Source: AuthorizationBehavior.cs — Current inline domain+permission logic (lines 45-84)]
- [Source: CommandsController.cs — Current inline tenant logic (lines 39-64)]
- [Source: ServiceCollectionExtensions.cs — DI registration (already wired from 17-1 + 17-2)]
- [Source: AuthorizationExceptionHandler.cs — Catches CommandAuthorizationException → 403]
- [Source: AuthorizationServiceUnavailableHandler.cs — Catches actor unavailable → 503]
- [Source: AuthorizationBehaviorTests.cs — Existing test patterns (10 tests)]
- [Source: CommandsControllerTenantTests.cs — Controller characterization tests (7 tests)]
- [Source: architecture.md — Six-layer auth model, D5 Authorization Pipeline]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- SecurityAuditLoggingTests.cs needed tenant claims added to test principals (4 instances) because tenant validation now runs first in behavior before domain checks.
- `AuthorizedTenant` HttpContext item confirmed unused in all source code — only referenced in documentation. Safely removed.
- IntegrationTests pre-existing build errors (EventPersister constructor) are unrelated to this story.

### Completion Notes List

- **Task 1+2:** Refactored `AuthorizationBehavior` to inject `ITenantValidator` and `IRbacValidator` via constructor. Replaced inline tenant claims extraction, domain check, and permission check with validator `ValidateAsync` calls. Added null-result defensive checks (`InvalidOperationException` for server bugs). `AuthorizationServiceUnavailableException` propagates naturally (not caught by behavior).
- **Task 3:** Removed controller inline tenant check (lines 39-64), `CreateForbiddenProblemDetails` helper, `LogTenantAuthorizationFailure` helper, and `AuthorizedTenant` HttpContext item. Added `503 ServiceUnavailable` `ProducesResponseType` attribute. Kept `RequestTenantId` for rate limiter.
- **Task 4:** Updated `CreateBehavior` helper and `NonSubmitCommandRequest_PassesThrough` test with new constructor. Added 7 new tests: tenant-no-claims, tenant-mismatch, tenant-match-success, tenant-pass-rbac-fail (sequential flow), and 2 NSubstitute delegation verification tests.
- **Task 5:** Replaced 7 characterization tests with 7 delegation verification tests proving controller delegates to MediatR, passes correct command fields, stores `RequestTenantId`, and no longer sets `AuthorizedTenant`.
- **Task 6:** Added DI integration test confirming `AuthorizationBehavior` resolves with correct validators from real `ServiceProvider`. Added defensive null-result test proving `InvalidOperationException` (500) instead of `NullReferenceException`.
- **Task 7:** All Tier 1 tests pass (465 total: 157 Contracts + 231 Client + 48 Testing + 29 Sample). All Tier 2 tests pass (941 Server.Tests). Zero regressions.
- **Additional:** Fixed `SecurityAuditLoggingTests.cs` (4 constructor updates + tenant claims added) as a necessary cascade of the constructor change.
- **Reviewer follow-up (2026-03-11):** Added real-pipeline controller coverage proving `CommandsController` requests reach `AuthorizationBehavior`, added a controller → behavior → actor-validator → `AuthorizationServiceUnavailableHandler` regression test for the 503 + `Retry-After` flow, and refreshed stale post-refactor commentary in `SecurityAuditLoggingTests.cs`.

### File List

- `src/Hexalith.EventStore.CommandApi/Pipeline/AuthorizationBehavior.cs` — MODIFIED (inject ITenantValidator/IRbacValidator, replace inline logic)
- `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` — MODIFIED (remove inline tenant check, helpers, AuthorizedTenant; add 503 ProducesResponseType)
- `tests/Hexalith.EventStore.Server.Tests/Pipeline/AuthorizationBehaviorTests.cs` — MODIFIED (update constructor, add 7 new tests)
- `tests/Hexalith.EventStore.Server.Tests/Controllers/CommandsControllerTenantTests.cs` — MODIFIED (replaced 7 characterization tests with 7 delegation tests)
- `tests/Hexalith.EventStore.Server.Tests/Configuration/CommandApiAuthorizationRegistrationTests.cs` — MODIFIED (added 2 DI integration tests)
- `tests/Hexalith.EventStore.Server.Tests/Security/SecurityAuditLoggingTests.cs` — MODIFIED (updated 4 constructor calls, added tenant claims to principals)

## Senior Developer Review (AI)

### Review Date

2026-03-11

### Outcome

Approved after reviewer fixes.

### Findings Resolved

- Strengthened `CommandsControllerTenantTests.cs` so controller authorization coverage now passes through the real MediatR pipeline and proves `AuthorizationBehavior` enforces authorization, not just a mocked `IMediator`.
- Added a regression test covering the controller → behavior → actor-based validator → `AuthorizationServiceUnavailableHandler` chain and verifying `503 Service Unavailable` plus `Retry-After`.
- Updated stale commentary in `SecurityAuditLoggingTests.cs` to match the post-refactor authorization layering.

### Validation Performed

- Re-ran focused server tests for controller delegation, authorization behavior, actor validators, and the 503 handler after the reviewer fixes.

## Change Log

- **2026-03-11:** Story 17-3 implementation — Consolidated all authorization decisions into `AuthorizationBehavior` via `ITenantValidator` + `IRbacValidator`. Removed duplicate inline tenant check from `CommandsController`. All 1406 Tier 1+2 tests pass with zero regressions.
- **2026-03-11:** Reviewer follow-up — added real-pipeline controller authorization coverage, verified the 503 authorization-unavailable flow, and marked the story done.
