# Story 17.2: Actor-Based Validator Implementations

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **deployment operator**,
I want **actor-based authorization validators that delegate tenant and RBAC checks to application-managed DAPR actors**,
so that **JWT tokens stay lean (identity only) and authorization can be managed dynamically at runtime without requiring token re-issuance**.

## Acceptance Criteria

1. **ActorTenantValidator** implements `ITenantValidator` (from Story 17-1), uses `IActorProxyFactory` to call the DAPR actor whose type name comes from `EventStoreAuthorizationOptions.TenantValidatorActorName`
2. **ActorRbacValidator** implements `IRbacValidator` (from Story 17-1), uses `IActorProxyFactory` to call the DAPR actor whose type name comes from `EventStoreAuthorizationOptions.RbacValidatorActorName`
3. **ITenantValidatorActor** interface defined in `Server/Actors/Authorization/` inheriting `Dapr.Actors.IActor` with method `ValidateTenantAccessAsync(TenantValidationRequest) → ActorValidationResponse`
4. **IRbacValidatorActor** interface defined in `Server/Actors/Authorization/` inheriting `Dapr.Actors.IActor` with method `ValidatePermissionAsync(RbacValidationRequest) → ActorValidationResponse`
5. **Serializable request/response DTOs** defined for actor communication: `TenantValidationRequest`, `RbacValidationRequest`, `ActorValidationResponse` — all in `Server/Actors/Authorization/`
6. **DI factory delegates** in `ServiceCollectionExtensions.AddCommandApi()` updated: when config has non-null actor name, resolve `ActorTenantValidator`/`ActorRbacValidator` instead of throwing `InvalidOperationException`
7. **AuthorizationServiceUnavailableException** created for actor-unreachable scenarios, with `ActorTypeName`, `ActorId`, and `Reason` properties (server-side only — these MUST NOT appear in the HTTP response body)
8. **AuthorizationServiceUnavailableHandler** implements `IExceptionHandler`, returns **503 Service Unavailable** with `Retry-After` header and RFC 9457 ProblemDetails body containing a **generic message only** — no actor type, actor ID, or internal details exposed to the caller (fail-closed, NOT 403)
9. **FakeTenantValidatorActor** and **FakeRbacValidatorActor** in `Testing/Fakes/` following `FakeAggregateActor` pattern — configurable results, configurable exceptions, invocation recording for assertions
10. Actor-based RBAC validator CAN distinguish `"command"` vs `"query"` `messageCategory` values (unlike claims-based which produces identical results for both)
11. When actor invocation succeeds and returns `IsAuthorized = false` → proxy returns `Denied` result (caller produces 403 via normal auth flow)
12. When actor invocation **fails** (unreachable, timeout, unexpected exception) → proxy throws `AuthorizationServiceUnavailableException` → handler produces 503 + Retry-After
13. When actor returns **null response** → proxy treats as unavailable and throws `AuthorizationServiceUnavailableException` (defensive: buggy actor should not grant access)
14. Proxy extracts userId from `ClaimTypes.NameIdentifier` claim ONLY — no fallback to `Identity.Name` (security: display names are not unique identifiers). Throws `InvalidOperationException` if `NameIdentifier` claim is absent.
15. Proxy calls `cancellationToken.ThrowIfCancellationRequested()` before actor invocation (DAPR actor calls do not natively support CancellationToken)
16. Both proxy classes use `LoggerMessage` source-generated structured logging (follow `CommandRouter.Log` partial class pattern)
17. Authorization-service unavailability returns the fixed `Retry-After: 30` contract rather than a configurable per-exception value
18. All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change
19. Unit tests cover all new implementations with edge cases

## Tasks / Subtasks

- [x] Task 1: Create actor interfaces and DTOs (AC: #3, #4, #5)
    - [x] 1.1 Create `Server/Actors/Authorization/ITenantValidatorActor.cs` — interface extending `IActor`
    - [x] 1.2 Create `Server/Actors/Authorization/IRbacValidatorActor.cs` — interface extending `IActor`
    - [x] 1.3 Create `Server/Actors/Authorization/TenantValidationRequest.cs` — serializable record
    - [x] 1.4 Create `Server/Actors/Authorization/RbacValidationRequest.cs` — serializable record
    - [x] 1.5 Create `Server/Actors/Authorization/ActorValidationResponse.cs` — shared serializable response record
- [x] Task 2: Preserve actor-name configuration while standardizing 503 retry behavior (AC: #17)
    - [x] 2.1 Keep `EventStoreAuthorizationOptions` focused on actor type selection only
    - [x] 2.2 Standardize authorization-service 503 responses on fixed `Retry-After: 30`
    - [x] 2.3 Verify tests assert the fixed 30-second retry contract
- [x] Task 3: Create AuthorizationServiceUnavailableException (AC: #7)
    - [x] 3.1 Create `CommandApi/ErrorHandling/AuthorizationServiceUnavailableException.cs` with `ActorTypeName`, `ActorId`, and `Reason` properties plus standard exception constructors
- [x] Task 4: Create AuthorizationServiceUnavailableHandler (AC: #8)
    - [x] 4.1 Create `CommandApi/ErrorHandling/AuthorizationServiceUnavailableHandler.cs` implementing `IExceptionHandler`
    - [x] 4.2 Returns 503 + `Retry-After` header + generic ProblemDetails body — NO internal details (actor type, actor ID) in response
    - [x] 4.3 Register handler in `AddCommandApi()` — add BEFORE `AuthorizationExceptionHandler` in the handler chain (503 check before 403 check)
- [x] Task 5: Create actor-based validator proxies (AC: #1, #2, #10, #11, #12, #13, #14, #15, #16)
    - [x] 5.1 Create `CommandApi/Authorization/ActorTenantValidator.cs` implementing `ITenantValidator`
    - [x] 5.2 Create `CommandApi/Authorization/ActorRbacValidator.cs` implementing `IRbacValidator`
    - [x] 5.3 Both extract userId from `ClaimsPrincipal` using `ClaimTypes.NameIdentifier` ONLY (no fallback), create actor proxy via `IActorProxyFactory`, call actor, map response
    - [x] 5.4 Both use `tenantId` as `ActorId` (each tenant has its own validator actor instance — this is a proxy convention, not an actor interface requirement)
    - [x] 5.5 Error handling: actor response `IsAuthorized=false` → return `Denied` result; null response → throw `AuthorizationServiceUnavailableException`; actor invocation failure → throw `AuthorizationServiceUnavailableException`
    - [x] 5.6 Add `cancellationToken.ThrowIfCancellationRequested()` before actor invocation
    - [x] 5.7 Add `LoggerMessage` source-generated structured logging via private `Log` partial class (follow `CommandRouter.Log` pattern, EventIds 1200-1219)
- [x] Task 6: Update DI registration (AC: #6)
    - [x] 6.1 Register `ActorTenantValidator` and `ActorRbacValidator` as concrete services
    - [x] 6.2 Update `ITenantValidator` factory delegate: replace `throw new InvalidOperationException(...)` with `return sp.GetRequiredService<ActorTenantValidator>()`
    - [x] 6.3 Update `IRbacValidator` factory delegate: replace `throw new InvalidOperationException(...)` with `return sp.GetRequiredService<ActorRbacValidator>()`
- [x] Task 7: Create test fakes in Testing package (AC: #9)
    - [x] 7.1 Create `Testing/Fakes/FakeTenantValidatorActor.cs` implementing `ITenantValidatorActor`
    - [x] 7.2 Create `Testing/Fakes/FakeRbacValidatorActor.cs` implementing `IRbacValidatorActor`
    - [x] 7.3 Follow `FakeAggregateActor` pattern: `ConfiguredResult`, `ConfiguredException`, invocation recording via `ConcurrentQueue`
- [x] Task 8: Write unit tests (AC: #19)
    - [x] 8.1 `ActorTenantValidatorTests.cs` — proxy logic: userId extraction (NameIdentifier only), actor proxy creation, response mapping, null response handling, 503 on actor failure, cancellation check
    - [x] 8.2 `ActorRbacValidatorTests.cs` — proxy logic: userId extraction, messageCategory pass-through, response mapping, null response handling, 503 on actor failure, cancellation check
    - [x] 8.3 `AuthorizationServiceUnavailableExceptionTests.cs` — exception properties and constructors
    - [x] 8.4 `AuthorizationServiceUnavailableHandlerTests.cs` — 503 response, Retry-After header, generic ProblemDetails body (no internal details), correlationId inclusion, non-matching exceptions pass through
    - [x] 8.5 DI registration tests: verify actor-based validators resolve when config has non-null actor names
    - [x] 8.6 DI registration tests: verify mixed-config scenarios (claims tenant + actor RBAC, actor tenant + claims RBAC)
- [x] Task 9: Verify zero regression (AC: #18)
    - [x] 9.1 Run all Tier 1 tests — zero failures
    - [x] 9.2 Run new unit tests — all pass
    - [x] 9.3 Verify existing `CommandApiAuthorizationRegistrationTests` still pass (claims-based default path unchanged)
    - [x] 9.4 Verify existing `EventStoreAuthorizationOptionsTests` still pass after keeping only actor-name settings
    - [x] 9.5 Update `AddCommandApi_ConfiguredTenantActor_FailsStartupValidationAsync` and `AddCommandApi_ConfiguredRbacActor_FailsStartupValidationAsync` — startup validator should now SUCCEED when actor names are configured (implementation exists)

## Dev Notes

### Architecture: Where Actor Interfaces Live

**Deviation from sprint change proposal:** The proposal suggested putting `ITenantValidatorActor`/`IRbacValidatorActor` in the `Contracts` package. However, **Contracts has NO Dapr.Actors dependency** — it's a lightweight package with zero infrastructure dependencies. Adding `Dapr.Actors` would violate this design principle.

**Decision:** Follow the established `IAggregateActor` pattern — actor interfaces go in **Server** (which already has `Dapr.Actors`, `Dapr.Client`, `Dapr.Actors.AspNetCore`). Application developers who implement custom validator actors already reference Server for hosting. The `FakeAggregateActor` test pattern in Testing also references Server.

```
Server/Actors/                          ← existing folder
├── IAggregateActor.cs                  ← EXISTING pattern to follow
├── AggregateActor.cs                   ← EXISTING implementation
├── Authorization/                      ← NEW subfolder
│   ├── ITenantValidatorActor.cs        ← NEW actor interface
│   ├── IRbacValidatorActor.cs          ← NEW actor interface
│   ├── TenantValidationRequest.cs      ← NEW serializable DTO
│   ├── RbacValidationRequest.cs        ← NEW serializable DTO
│   └── ActorValidationResponse.cs      ← NEW shared response DTO
```

### Actor Interface Design

DAPR actor methods support at most **one parameter** (serialized as JSON body). Use request DTOs:

```csharp
// Server/Actors/Authorization/ITenantValidatorActor.cs
/// <summary>
/// DAPR actor interface for application-managed tenant authorization.
/// </summary>
/// <remarks>
/// <para>Applications implement this interface to provide dynamic tenant access control
/// managed at runtime via actor state, replacing static JWT claims.</para>
/// <para><b>Implementation guidance:</b></para>
/// <list type="bullet">
/// <item>The actor ID is the tenant ID — each tenant has its own actor instance.</item>
/// <item>Keep activation cost low — avoid expensive I/O in OnActivateAsync.</item>
/// <item>If your backing store is unavailable, THROW an exception (do not return denied).
/// The proxy converts exceptions to 503, preserving fail-closed semantics.</item>
/// <item>DAPR idle timeout (default 60 min) deactivates unused actors automatically.</item>
/// </list>
/// </remarks>
public interface ITenantValidatorActor : IActor
{
    Task<ActorValidationResponse> ValidateTenantAccessAsync(TenantValidationRequest request);
}

// Server/Actors/Authorization/IRbacValidatorActor.cs
/// <summary>
/// DAPR actor interface for application-managed RBAC authorization.
/// </summary>
/// <remarks>
/// <para>Applications implement this interface to provide dynamic role-based access control
/// managed at runtime via actor state.</para>
/// <para><b>messageCategory:</b> Unlike claims-based authorization (which treats "command" and
/// "query" identically), actor-based implementations CAN distinguish read vs write operations
/// using <see cref="RbacValidationRequest.MessageCategory"/>.</para>
/// <para>See <see cref="ITenantValidatorActor"/> remarks for general implementation guidance.</para>
/// </remarks>
public interface IRbacValidatorActor : IActor
{
    Task<ActorValidationResponse> ValidatePermissionAsync(RbacValidationRequest request);
}
```

### Serializable DTOs

```csharp
// Request DTOs — extracted from ClaimsPrincipal by proxy, serialized to actor
public record TenantValidationRequest(string UserId, string TenantId);

public record RbacValidationRequest(
    string UserId,
    string TenantId,
    string Domain,
    string MessageType,
    string MessageCategory);  // "command" or "query"

// Shared response — matches shape of TenantValidationResult/RbacValidationResult
public record ActorValidationResponse(bool IsAuthorized, string? Reason = null);
```

### Actor Proxy Pattern — Follow CommandRouter

The `CommandRouter.cs` establishes the actor proxy pattern. The validator proxies must follow it exactly:

```csharp
// From CommandRouter.cs (lines 38-42) — THE pattern to follow:
IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
    new ActorId(actorId),
    nameof(AggregateActor));
return await proxy.ProcessCommandAsync(envelope).ConfigureAwait(false);
```

For validator proxies:

- `actorType` = the configured name from `EventStoreAuthorizationOptions` (NOT `nameof()` — the name comes from config)
- `actorId` = `tenantId` (each tenant has its own validator actor instance)

```csharp
// ActorTenantValidator pattern:
ITenantValidatorActor proxy = _actorProxyFactory.CreateActorProxy<ITenantValidatorActor>(
    new ActorId(tenantId),
    _options.TenantValidatorActorName);  // From config, NOT nameof()

ActorValidationResponse response = await proxy.ValidateTenantAccessAsync(
    new TenantValidationRequest(userId, tenantId)).ConfigureAwait(false);

// On actor failure, preserve actor diagnostics in the exception:
catch (Exception ex)
{
    Log.ActorTenantValidationFailed(logger, ex, userId, tenantId,
        _options.Value.TenantValidatorActorName!,
        ex.InnerException?.GetType().Name ?? ex.GetType().Name);

    throw new AuthorizationServiceUnavailableException(
        _options.Value.TenantValidatorActorName!,
        tenantId,
        ex.Message,
        ex);
}
```

**CRITICAL difference from CommandRouter:** The actor type name comes from **configuration** (`EventStoreAuthorizationOptions`), not from `nameof()`. This allows different deployments to use different actor implementations.

**ActorId convention:** Using `tenantId` as `ActorId` is a **proxy-level convention**, not an actor interface requirement. The actor interface is agnostic to how actor IDs are assigned. This convention means each tenant gets its own validator actor instance, allowing per-tenant authorization state. If a future deployment needs a different actor ID strategy (e.g., singleton, per-domain), only the proxy needs to change — the actor interface remains the same.

### UserId Extraction from ClaimsPrincipal

The proxy must extract a userId string from `ClaimsPrincipal` to pass to the actor. The actor cannot receive `ClaimsPrincipal` (not serializable).

**SECURITY: Use `ClaimTypes.NameIdentifier` ONLY — NO fallback to `Identity.Name`.**

`Identity.Name` maps to display names which are **not unique identifiers**. If a JWT has a display name but no `sub` claim, passing a shared display name as userId to the actor could cause the actor to grant permissions to the wrong user. Reject the request instead.

```csharp
string userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? throw new InvalidOperationException(
        "No user identifier (NameIdentifier/sub) found in claims. "
        + "Actor-based authorization requires a unique user identifier.");
```

### Error Handling: 503 vs 403 Distinction

**CRITICAL: Fail-closed design (Amendment A3)**

| Scenario                             | Actor Response | Proxy Behavior                                   | HTTP Result            |
| ------------------------------------ | -------------- | ------------------------------------------------ | ---------------------- |
| Actor returns `IsAuthorized = true`  | Success        | Return `TenantValidationResult.Allowed`          | 2xx (normal flow)      |
| Actor returns `IsAuthorized = false` | Denial         | Return `TenantValidationResult.Denied(reason)`   | 403 (normal auth flow) |
| Actor returns **null**               | Buggy actor    | Throw `AuthorizationServiceUnavailableException` | **503** + Retry-After  |
| Actor unreachable (placement down)   | Exception      | Throw `AuthorizationServiceUnavailableException` | **503** + Retry-After  |
| Actor method throws exception        | Exception      | Throw `AuthorizationServiceUnavailableException` | **503** + Retry-After  |
| Actor timeout                        | Exception      | Throw `AuthorizationServiceUnavailableException` | **503** + Retry-After  |

The proxy wraps ALL actor invocation exceptions (including `Dapr.Actors.ActorMethodInvocationException`) and null responses in `AuthorizationServiceUnavailableException`. The only non-503 path is when the actor successfully returns a non-null response. Log the inner exception at Error level with full details server-side for operator diagnostics.

### AuthorizationServiceUnavailableException Design

Follow `CommandAuthorizationException` pattern but with 503-specific properties:

```csharp
public class AuthorizationServiceUnavailableException : Exception
{
    public AuthorizationServiceUnavailableException(
        string actorTypeName, string actorId, string reason, Exception innerException)
        : base($"Authorization service unavailable: actor '{actorTypeName}' (ID: {actorId}): {reason}", innerException)
    {
        ActorTypeName = actorTypeName;
        ActorId = actorId;
        Reason = reason;
    }

    public string ActorTypeName { get; }
    public string ActorId { get; }
    public string Reason { get; }
}
```

Include standard parameterless, `(string)`, and `(string, Exception)` constructors for exception best practices. The exception carries actor diagnostics only; the HTTP handler owns the fixed `Retry-After: 30` contract.

### AuthorizationServiceUnavailableHandler Design

Follow `AuthorizationExceptionHandler` pattern exactly, but with 503 instead of 403.

**SECURITY: The response body MUST NOT contain `ActorTypeName`, `ActorId`, or any internal infrastructure details.** These reveal deployment topology to callers. Log full details server-side only.

```csharp
public class AuthorizationServiceUnavailableHandler(
    ILogger<AuthorizationServiceUnavailableHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not AuthorizationServiceUnavailableException unavailable)
            return false;

        string correlationId = httpContext.Items["CorrelationId"]?.ToString() ?? "unknown";

        // Log at Error level with FULL internal details (server-side only)
        logger.LogError(exception,
            "Authorization service unavailable: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, ActorType={ActorType}, ActorId={ActorId}, Reason={Reason}",
            "AuthorizationServiceUnavailable", correlationId, unavailable.ActorTypeName, unavailable.ActorId, unavailable.Reason);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        httpContext.Response.Headers.RetryAfter = "30";

        // SECURITY: Generic message only — no actor type, actor ID, or internal details
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Service Unavailable",
            Type = "https://tools.ietf.org/html/rfc9457#section-3",
            Detail = "Authorization service is temporarily unavailable. Please retry.",
            Instance = httpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId },
        };

        await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null,
            "application/problem+json", ct).ConfigureAwait(false);

        return true;
    }
}
```

**Registration order in `AddCommandApi()`:**

```csharp
services.AddExceptionHandler<ValidationExceptionHandler>();
services.AddExceptionHandler<AuthorizationServiceUnavailableHandler>();  // 503 — BEFORE 403
services.AddExceptionHandler<AuthorizationExceptionHandler>();           // 403
services.AddExceptionHandler<ConcurrencyConflictExceptionHandler>();
services.AddExceptionHandler<GlobalExceptionHandler>();
```

### DI Registration Update

**Exact lines to change** in `ServiceCollectionExtensions.cs`:

```csharp
// BEFORE (Story 17-1 placeholder):
_ = services.AddScoped<ITenantValidator>(sp => {
    EventStoreAuthorizationOptions opts = sp.GetRequiredService<IOptions<EventStoreAuthorizationOptions>>().Value;
    if (opts.TenantValidatorActorName is null) {
        return sp.GetRequiredService<ClaimsTenantValidator>();
    }
    // Story 17-2 adds: return sp.GetRequiredService<ActorTenantValidator>();
    throw new InvalidOperationException(
        $"Actor-based tenant validator '{opts.TenantValidatorActorName}' is configured but not yet implemented. Install Story 17-2.");
});

// AFTER (Story 17-2):
_ = services.AddScoped<ActorTenantValidator>();
_ = services.AddScoped<ActorRbacValidator>();

_ = services.AddScoped<ITenantValidator>(sp => {
    EventStoreAuthorizationOptions opts = sp.GetRequiredService<IOptions<EventStoreAuthorizationOptions>>().Value;
    if (opts.TenantValidatorActorName is null) {
        return sp.GetRequiredService<ClaimsTenantValidator>();
    }
    return sp.GetRequiredService<ActorTenantValidator>();
});

_ = services.AddScoped<IRbacValidator>(sp => {
    EventStoreAuthorizationOptions opts = sp.GetRequiredService<IOptions<EventStoreAuthorizationOptions>>().Value;
    if (opts.RbacValidatorActorName is null) {
        return sp.GetRequiredService<ClaimsRbacValidator>();
    }
    return sp.GetRequiredService<ActorRbacValidator>();
});
```

**DI prerequisite:** `IActorProxyFactory` must be available in the container. This requires `services.AddDaprClient()` (called in `Program.cs`) or `services.AddActors()` (called in `AddEventStoreServer()`). Both are already called in the existing CommandApi host setup. If `IActorProxyFactory` is not registered and an actor-based validator is configured, resolution will fail at the `ActorTenantValidator`/`ActorRbacValidator` constructor — this is the correct fail-fast behavior.

**ActorTenantValidator constructor dependencies:**

- `IActorProxyFactory actorProxyFactory` (from DAPR DI — requires `AddDaprClient()`)
- `IOptions<EventStoreAuthorizationOptions> options` (for actor type names)
- `ILogger<ActorTenantValidator> logger`

**ActorRbacValidator constructor dependencies:**

- `IActorProxyFactory actorProxyFactory`
- `IOptions<EventStoreAuthorizationOptions> options`
- `ILogger<ActorRbacValidator> logger`

### CancellationToken Handling

`ITenantValidator.ValidateAsync` and `IRbacValidator.ValidateAsync` accept `CancellationToken`, but DAPR actor proxy calls do not natively support cancellation. The proxy must:

1. Call `cancellationToken.ThrowIfCancellationRequested()` **before** creating the actor proxy
2. The actor call itself runs without cancellation (DAPR handles timeouts via sidecar-level resiliency)
3. After the actor call returns, normal flow resumes with the token available for downstream work

### Structured Logging Design

Both `ActorTenantValidator` and `ActorRbacValidator` must use `LoggerMessage` source-generated structured logging, following the `CommandRouter.Log` partial class pattern. This is a project convention — do NOT use `logger.LogXxx()` string interpolation.

```csharp
// Example for ActorTenantValidator:
public partial class ActorTenantValidator(
    IActorProxyFactory actorProxyFactory,
    IOptions<EventStoreAuthorizationOptions> options,
    ILogger<ActorTenantValidator> logger) : ITenantValidator
{
    // ... ValidateAsync implementation ...

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1200,
            Level = LogLevel.Debug,
            Message = "Actor tenant validation: UserId={UserId}, TenantId={TenantId}, ActorType={ActorType}, Stage=ActorTenantValidation")]
        public static partial void ActorTenantValidation(
            ILogger logger, string userId, string tenantId, string actorType);

        [LoggerMessage(
            EventId = 1201,
            Level = LogLevel.Debug,
            Message = "Actor tenant validation allowed: UserId={UserId}, TenantId={TenantId}, Stage=ActorTenantValidationAllowed")]
        public static partial void ActorTenantValidationAllowed(
            ILogger logger, string userId, string tenantId);

        // COMPLIANCE: Denial logged at Warning (not Debug) for SOC 2 audit trail
        [LoggerMessage(
            EventId = 1202,
            Level = LogLevel.Warning,
            Message = "Actor tenant validation denied: SecurityEvent={SecurityEvent}, UserId={UserId}, TenantId={TenantId}, Reason={Reason}, Stage=ActorTenantValidationDenied")]
        public static partial void ActorTenantValidationDenied(
            ILogger logger, string securityEvent, string userId, string tenantId, string? reason);

        [LoggerMessage(
            EventId = 1203,
            Level = LogLevel.Error,
            Message = "Actor tenant validation failed: UserId={UserId}, TenantId={TenantId}, ActorType={ActorType}, InnerExceptionType={InnerExceptionType}, Stage=ActorTenantValidationFailed")]
        public static partial void ActorTenantValidationFailed(
            ILogger logger, Exception ex, string userId, string tenantId, string actorType, string innerExceptionType);
    }
}
```

**EventId allocation:** Use 1200-1209 for `ActorTenantValidator`, 1210-1219 for `ActorRbacValidator`. Verify no collisions with existing EventIds — `CommandRouter` uses 1100-1101. Check all `[LoggerMessage(EventId = ...)]` in the project before finalizing.

**Logging level policy:**

- **Debug** — validation request initiated, validation allowed (success path)
- **Warning** — validation denied by actor (security audit trail — must be visible in production logs for SOC 2 compliance)
- **Error** — actor invocation failure (with `InnerExceptionType` to help operators distinguish network failures vs actor bugs)

**InnerExceptionType field:** Include `ex.InnerException?.GetType().Name ?? ex.GetType().Name` in the Error log. This lets operators filter for `ActorMethodInvocationException` (actor has a bug), `HttpRequestException` (network), `TimeoutException` (timeout), or `Grpc.Core.RpcException` (gRPC failure) without parsing stack traces.

### EventStoreAuthorizationOptions

Keep `EventStoreAuthorizationOptions` focused on actor type selection:

```csharp
public record EventStoreAuthorizationOptions
{
    public string? TenantValidatorActorName { get; init; }
    public string? RbacValidatorActorName { get; init; }
}
```

The `ValidateEventStoreAuthorizationOptions` validator only needs to validate the actor name settings. Authorization-service 503 responses use the fixed `Retry-After: 30` contract.

**Configuration shape update:**

```json
{
    "EventStore": {
        "Authorization": {
            "TenantValidatorActorName": null,
            "RbacValidatorActorName": null
        }
    }
}
```

### messageCategory Contract Difference

**Claims-based (Story 17-1):** `ClaimsRbacValidator` produces **identical results** for `"command"` and `"query"` messageCategory — claims don't distinguish read/write.

**Actor-based (this story):** `ActorRbacValidator` forwards `messageCategory` to the actor, which **CAN** distinguish command vs query operations. The actor receives `RbacValidationRequest.MessageCategory` and can implement different permission logic per category.

The proxy passes `messageCategory` through to the actor without validation. The proxy does NOT validate messageCategory values — this responsibility belongs to the actor implementation, not the proxy. Rationale: future messageCategory values (e.g., `"notification"`, `"subscription"`) should not require a proxy code change. The actor decides whether to accept or reject unrecognized values based on its own authorization logic.

### Actor Activation Cost Guidance

Validator actors should be **lightweight** — avoid expensive I/O (database queries, HTTP calls) in `OnActivateAsync`. Authorization rules can be loaded lazily on first `ValidateTenantAccessAsync`/`ValidatePermissionAsync` call, or pre-loaded from actor state via `IActorStateManager`.

DAPR deactivates idle actors after 60 minutes by default. For validator actors handling steady traffic, this means they stay warm. For rarely-accessed tenants, actors will be re-activated on the next request — keep activation cost under 50ms to avoid noticeable latency spikes.

### Test Fake Pattern — Follow FakeAggregateActor

From `Testing/Fakes/FakeAggregateActor.cs`:

- Uses `ConcurrentQueue<T>` for invocation recording
- `ConfiguredResult` property for setting return value
- `ConfiguredException` property for simulating failures
- Implements the actor interface directly (does NOT inherit from Actor base class — fakes are not real DAPR actors)

```csharp
// Testing/Fakes/FakeTenantValidatorActor.cs
public class FakeTenantValidatorActor : ITenantValidatorActor
{
    private readonly ConcurrentQueue<TenantValidationRequest> _receivedRequests = new();

    public IReadOnlyCollection<TenantValidationRequest> ReceivedRequests => [.. _receivedRequests];
    public ActorValidationResponse? ConfiguredResult { get; set; }
    public Exception? ConfiguredException { get; set; }

    public Task<ActorValidationResponse> ValidateTenantAccessAsync(TenantValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _receivedRequests.Enqueue(request);

        if (ConfiguredException is not null) throw ConfiguredException;

        return Task.FromResult(ConfiguredResult ?? new ActorValidationResponse(true));
    }
}
```

### Unit Test Strategy

Tests go in `tests/Hexalith.EventStore.Server.Tests/Authorization/`:

**ActorTenantValidatorTests.cs:**

- `ValidateAsync_ActorAllows_ReturnsAllowed` — actor returns `IsAuthorized=true`
- `ValidateAsync_ActorDenies_ReturnsDeniedWithReason` — actor returns `IsAuthorized=false`
- `ValidateAsync_ActorReturnsNull_ThrowsServiceUnavailable` — null response treated as unavailable
- `ValidateAsync_ActorUnreachable_ThrowsServiceUnavailable` — proxy wraps exception in `AuthorizationServiceUnavailableException`
- `ValidateAsync_ExtractsUserIdFromNameIdentifierClaim` — verifies correct userId extraction
- `ValidateAsync_NoNameIdentifierClaim_ThrowsInvalidOperation` — no fallback to `Identity.Name`
- `ValidateAsync_PassesTenantIdAsActorId` — verifies actor ID is the tenant ID
- `ValidateAsync_UsesConfiguredActorTypeName` — verifies actor type from options
- `ValidateAsync_ChecksCancellationBeforeActorCall` — verifies `OperationCanceledException` on pre-cancelled token
- `ValidateAsync_ServiceUnavailableExceptionPreservesDiagnostics` — verifies actor type, actor ID, and reason are preserved

**ActorRbacValidatorTests.cs:**

- Same patterns as tenant validator tests (allow, deny, null, unreachable, userId, cancellation)
- `ValidateAsync_ForwardsMessageCategory` — verifies `"command"` and `"query"` reach actor
- `ValidateAsync_ForwardsDomainAndMessageType`
- `ValidateAsync_PassesThroughUnknownMessageCategory` — proxy does NOT validate (actor's responsibility)

**AuthorizationServiceUnavailableExceptionTests.cs:**

- Constructor sets properties correctly (ActorTypeName, ActorId, Reason)
- Message format includes actor type, ID, reason
- Standard exception constructors work

**AuthorizationServiceUnavailableHandlerTests.cs:**

- Matching exception → 503 + Retry-After + ProblemDetails
- Non-matching exception → returns false (pass through)
- Retry-After header value is the fixed 30-second contract
- ProblemDetails content type is `application/problem+json`
- **SECURITY: ProblemDetails body does NOT contain actorTypeName or actorId** — only generic message + correlationId
- ProblemDetails includes correlationId from HttpContext

**DI registration tests (extend existing `CommandApiAuthorizationRegistrationTests.cs`):**

- When `TenantValidatorActorName` is set → resolves `ActorTenantValidator` (not claims-based)
- When `RbacValidatorActorName` is set → resolves `ActorRbacValidator` (not claims-based)
- When both null → still resolves claims-based (no regression)
- Mixed config: claims tenant + actor RBAC → resolves `ClaimsTenantValidator` + `ActorRbacValidator`
- Mixed config: actor tenant + claims RBAC → resolves `ActorTenantValidator` + `ClaimsRbacValidator`

**CRITICAL — `BuildProvider()` must register `IActorProxyFactory`:** The existing test helper does NOT register `IActorProxyFactory`. After this story, `ActorTenantValidator`/`ActorRbacValidator` constructors require it. Add `services.AddSingleton(Substitute.For<IActorProxyFactory>())` to `BuildProvider()` BEFORE `AddCommandApi()`. Without this, DI resolution fails with a missing-service exception.

### Existing Test Patterns to Follow

From `ClaimsTenantValidatorTests.cs`:

```csharp
private static ClaimsPrincipal CreatePrincipal(params string[] tenants)
{
    var claims = tenants.Select(t => new Claim("eventstore:tenant", t)).ToList();
    return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
}
```

Use `NSubstitute` for `IActorProxyFactory`, `IOptions<EventStoreAuthorizationOptions>`. Use `Shouldly` assertions.

### Naming Convention: `ClassName_Scenario_ExpectedResult()`

Follow the existing test naming convention in this project.

### Project Structure Notes

- Actor interfaces in `Server/Actors/Authorization/` — new subfolder, follows `Server/Actors/` convention
- Proxy implementations in `CommandApi/Authorization/` — alongside `ClaimsTenantValidator`, `ClaimsRbacValidator`
- Exception + handler in `CommandApi/ErrorHandling/` — alongside `CommandAuthorizationException`, `AuthorizationExceptionHandler`
- Test fakes in `Testing/Fakes/` — alongside `FakeAggregateActor`
- Tests in `Server.Tests/Authorization/` — alongside existing `ClaimsTenantValidatorTests.cs`, `ClaimsRbacValidatorTests.cs`
- Namespace: `Hexalith.EventStore.Server.Actors.Authorization` for actor interfaces/DTOs
- Namespace: `Hexalith.EventStore.CommandApi.Authorization` for proxy implementations
- Namespace: `Hexalith.EventStore.CommandApi.ErrorHandling` for exception + handler
- Namespace: `Hexalith.EventStore.Testing.Fakes` for test fakes

### Files to Create

```
src/Hexalith.EventStore.Server/Actors/Authorization/
├── ITenantValidatorActor.cs
├── IRbacValidatorActor.cs
├── TenantValidationRequest.cs
├── RbacValidationRequest.cs
└── ActorValidationResponse.cs

src/Hexalith.EventStore.CommandApi/Authorization/
├── ActorTenantValidator.cs
└── ActorRbacValidator.cs

src/Hexalith.EventStore.CommandApi/ErrorHandling/
├── AuthorizationServiceUnavailableException.cs
└── AuthorizationServiceUnavailableHandler.cs

src/Hexalith.EventStore.Testing/Fakes/
├── FakeTenantValidatorActor.cs
└── FakeRbacValidatorActor.cs

tests/Hexalith.EventStore.Server.Tests/Authorization/
├── ActorTenantValidatorTests.cs
├── ActorRbacValidatorTests.cs
├── AuthorizationServiceUnavailableExceptionTests.cs
└── AuthorizationServiceUnavailableHandlerTests.cs
```

### Files to Modify

```
src/Hexalith.EventStore.CommandApi/Configuration/EventStoreAuthorizationOptions.cs
    — Keep only TenantValidatorActorName and RbacValidatorActorName

src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs
  — Lines 64-85: Replace placeholder throws with actual ActorTenantValidator/ActorRbacValidator resolution
  — Add registrations: services.AddScoped<ActorTenantValidator>() and services.AddScoped<ActorRbacValidator>()
  — Add exception handler: services.AddExceptionHandler<AuthorizationServiceUnavailableHandler>() before AuthorizationExceptionHandler

tests/Hexalith.EventStore.Server.Tests/Configuration/CommandApiAuthorizationRegistrationTests.cs
  — REPLACE `AddCommandApi_ConfiguredTenantActor_ThrowsPlaceholderException` → positive test verifying ActorTenantValidator resolves
  — REPLACE `AddCommandApi_ConfiguredRbacActor_ThrowsPlaceholderException` → positive test verifying ActorRbacValidator resolves
  — UPDATE `AddCommandApi_ConfiguredTenantActor_FailsStartupValidationAsync` → startup validator now SUCCEEDS (implementation exists)
  — UPDATE `AddCommandApi_ConfiguredRbacActor_FailsStartupValidationAsync` → startup validator now SUCCEEDS
  — ADD mixed-config tests: claims tenant + actor RBAC, actor tenant + claims RBAC
  — CRITICAL: `BuildProvider()` helper must register a mock `IActorProxyFactory` (via NSubstitute `Substitute.For<IActorProxyFactory>()`)
    because `ActorTenantValidator`/`ActorRbacValidator` constructors require it. Without this, DI resolution fails with an unrelated
    missing-service exception instead of verifying the correct validator type. Register BEFORE calling `AddCommandApi()`.

tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreAuthorizationOptionsTests.cs
    — Keep tests focused on actor-name validation only
```

### Files NOT to Modify

- `CommandsController.cs` — inline tenant check stays until Story 17-3
- `AuthorizationBehavior.cs` — inline logic stays until Story 17-3
- `EventStoreAuthorizationOptions.cs` — kept focused on actor-name selection
- `ClaimsTenantValidator.cs`, `ClaimsRbacValidator.cs` — no changes
- `ITenantValidator.cs`, `IRbacValidator.cs` — no changes
- `TenantValidationResult.cs`, `RbacValidationResult.cs` — no changes

### Scope Boundary

**IN scope:** Actor interfaces, serializable DTOs, actor-based proxy implementations, 503 exception + handler, test fakes, DI updates, unit tests.

**OUT of scope (later stories):**

- Refactoring `AuthorizationBehavior`/`CommandsController` to USE validators → Story 17-3
- Query contracts and endpoints → Stories 17-4, 17-5, 17-6
- Validation endpoints → Stories 17-7, 17-8
- Integration and E2E tests → Story 17-9
- Actual application-side actor implementations (the APPLICATION implements `ITenantValidatorActor`/`IRbacValidatorActor` — we provide the interfaces, proxies, and test fakes)

### Backward Compatibility

- Claims-based remains the DEFAULT when `EventStoreAuthorizationOptions` values are null
- No existing endpoint behavior changes
- No JWT claim format changes
- No NuGet package API changes (new public types are additive)
- The only behavioral change: configuring non-null actor names no longer throws `InvalidOperationException` — it resolves the actor-based implementation

### Deployment Sequence for Operators

When migrating from claims-based to actor-based authorization:

1. **Deploy the actor implementation first** — the application must register and activate the actor type (e.g., `RegisterActor<MyTenantValidatorActor>()`)
2. **Verify actor health** — confirm the actor responds to DAPR actor API calls (use DAPR dashboard or direct HTTP call to sidecar)
3. **Update config** — set `EventStore:Authorization:TenantValidatorActorName` to the actor type name
4. **Monitor** — watch for 503 responses indicating actor unreachability

**CRITICAL: Actor deployment must precede config changes.** If the config points to a non-existent actor type, all requests will receive 503 until the actor is deployed and reachable. This is correct fail-closed behavior, but operators should be aware.

**Mixed configurations are supported:** Each validator factory delegate is independent. You can use claims-based tenant validation (`TenantValidatorActorName = null`) with actor-based RBAC validation (`RbacValidatorActorName = "MyRbacActor"`), or vice versa. This allows incremental migration.

### Previous Story Intelligence (from Story 17-1)

- **DI pattern:** Factory delegate with `IOptions<T>` at resolve-time (NOT `BuildServiceProvider()`)
- **Test fixture issue:** 84 pre-existing build errors from constructor signature changes were fixed in 17-1 (IDeadLetterPublisher, IEventPayloadProtectionService). Use `NoOpEventPayloadProtectionService` in test fixtures, not unconfigured NSubstitute mocks.
- **Naming collision:** `ITenantValidator` exists in BOTH `CommandApi.Authorization` (API-level, Layer 4) AND `Server.Actors` (defense-in-depth, Layer 5). Use full namespaces to disambiguate. The NEW actor interfaces (`ITenantValidatorActor`) have distinct names — no collision.
- **String comparison asymmetry:** Tenant = Ordinal (case-sensitive), Domain = OrdinalIgnoreCase, Permission = OrdinalIgnoreCase. This is the claims-based convention. Actor-based validators delegate this to the actor — the proxy does NOT perform string comparisons.
- **Exception handler content type:** Use `WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, "application/problem+json", ct)` — the parameterless overload overrides ContentType to application/json.
- **Test naming:** `ClassName_Scenario_ExpectedResult()`

### References

- [Source: sprint-change-proposal-2026-03-08-auth-query.md — Section 4.2, 4.3 Story 17-2, Amendment A3 (503 fail-closed)]
- [Source: architecture.md — Six-layer auth model, D5 Authorization Pipeline]
- [Source: CommandRouter.cs — Actor proxy invocation pattern (IActorProxyFactory, CreateActorProxy)]
- [Source: IAggregateActor.cs — Actor interface pattern (: IActor)]
- [Source: FakeAggregateActor.cs — Test fake pattern (ConfiguredResult, ConfiguredException, ConcurrentQueue)]
- [Source: AuthorizationExceptionHandler.cs — IExceptionHandler pattern for ProblemDetails responses]
- [Source: CommandAuthorizationException.cs — Custom exception pattern]
- [Source: ServiceCollectionExtensions.cs — Lines 64-85, factory delegate placeholder for Story 17-2]
- [Source: EventStoreAuthorizationOptions.cs — TenantValidatorActorName, RbacValidatorActorName config]
- [Source: 17-1-authorization-options-and-validator-abstractions.md — Previous story learnings and dev notes]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Build error fix: `AuthorizationServiceUnavailableHandlerTests.cs` — nullable `ContentType` required `.ShouldNotBeNull()` before `.ShouldContain()`, and `ConfigureAwait(false)` required on `ReadResponseBody`/`ReadProblemDetails` helpers (CA2007).
- 2026-03-11 review fix: `EventStoreClaimsTransformation` now mirrors JWT `sub` into `ClaimTypes.NameIdentifier` so actor-based validators work with the repository's `MapInboundClaims = false` authentication pipeline without relaxing the NameIdentifier-only validator contract.

### Completion Notes List

- All 9 tasks completed with all subtasks marked [x]
- 5 actor interface/DTO files created in `Server/Actors/Authorization/`
- 2 proxy implementations created in `CommandApi/Authorization/` with LoggerMessage structured logging (EventIds 1200-1213)
- 1 exception + 1 handler created in `CommandApi/ErrorHandling/` for 503 fail-closed pattern
- 2 test fakes created in `Testing/Fakes/` following FakeAggregateActor pattern
- 4 new test files created: ActorTenantValidatorTests (11 tests), ActorRbacValidatorTests (12 tests), AuthorizationServiceUnavailableExceptionTests (6 tests), AuthorizationServiceUnavailableHandlerTests (6 tests)
- DI registration updated: factory delegates now resolve ActorTenantValidator/ActorRbacValidator when actor names configured
- Existing DI registration tests rewritten: placeholder tests replaced with positive resolution tests, startup validation tests updated to verify success, mixed-config tests added, BuildProvider registers mock IActorProxyFactory
- Authorization-service unavailability uses the fixed `Retry-After: 30` contract
- Review follow-up: claims transformation now normalizes JWT `sub` into `ClaimTypes.NameIdentifier`, and focused authentication + actor-validator regression tests cover the runtime path used by Keycloak/dev tokens.
- Zero regressions verified in this review session: focused authentication and actor-validator suites pass, plus the full `Hexalith.EventStore.Server.Tests` project passes.

### File List

**New files:**

- src/Hexalith.EventStore.Server/Actors/Authorization/ITenantValidatorActor.cs
- src/Hexalith.EventStore.Server/Actors/Authorization/IRbacValidatorActor.cs
- src/Hexalith.EventStore.Server/Actors/Authorization/TenantValidationRequest.cs
- src/Hexalith.EventStore.Server/Actors/Authorization/RbacValidationRequest.cs
- src/Hexalith.EventStore.Server/Actors/Authorization/ActorValidationResponse.cs
- src/Hexalith.EventStore.CommandApi/Authorization/ActorTenantValidator.cs
- src/Hexalith.EventStore.CommandApi/Authorization/ActorRbacValidator.cs
- src/Hexalith.EventStore.CommandApi/ErrorHandling/AuthorizationServiceUnavailableException.cs
- src/Hexalith.EventStore.CommandApi/ErrorHandling/AuthorizationServiceUnavailableHandler.cs
- src/Hexalith.EventStore.Testing/Fakes/FakeTenantValidatorActor.cs
- src/Hexalith.EventStore.Testing/Fakes/FakeRbacValidatorActor.cs
- tests/Hexalith.EventStore.Server.Tests/Authorization/ActorTenantValidatorTests.cs
- tests/Hexalith.EventStore.Server.Tests/Authorization/ActorRbacValidatorTests.cs
- tests/Hexalith.EventStore.Server.Tests/Authorization/AuthorizationServiceUnavailableExceptionTests.cs
- tests/Hexalith.EventStore.Server.Tests/Authorization/AuthorizationServiceUnavailableHandlerTests.cs

**Modified files:**

- src/Hexalith.EventStore.CommandApi/Configuration/EventStoreAuthorizationOptions.cs — Kept focused on actor-name selection
- src/Hexalith.EventStore.CommandApi/Authentication/EventStoreClaimsTransformation.cs — Mirrors JWT `sub` into `ClaimTypes.NameIdentifier` for actor-validator compatibility while preserving raw claim names
- src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs — Added actor-based validator registrations, replaced placeholders, added AuthorizationServiceUnavailableHandler
- tests/Hexalith.EventStore.Server.Tests/Configuration/CommandApiAuthorizationRegistrationTests.cs — Rewritten: placeholder tests → positive resolution tests, startup validation tests → success, added mixed-config tests, BuildProvider registers IActorProxyFactory
- tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreAuthorizationOptionsTests.cs — Kept focused on actor-name validation
- tests/Hexalith.EventStore.Server.Tests/Authentication/EventStoreClaimsTransformationTests.cs — Added `sub` → `NameIdentifier` normalization coverage and idempotency assertions
- \_bmad-output/implementation-artifacts/17-2-actor-based-validator-implementations.md — Review notes, status, and verification evidence synchronized
- \_bmad-output/implementation-artifacts/sprint-status.yaml — Story status synchronized to `done`

## Change Log

- 2026-03-11: Story 17-2 implemented — Actor-based validator proxies, 503 fail-closed exception handling, DI registration update, test fakes, comprehensive unit tests (35 new tests, 8 updated tests)
- 2026-03-11: Senior review fixes — normalized raw JWT `sub` into `ClaimTypes.NameIdentifier` during claims transformation, expanded auth regression coverage, and synchronized story/sprint review bookkeeping.

## Senior Developer Review (AI)

### Reviewer

GitHub Copilot (GPT-5.4) — 2026-03-11

### Outcome

Approved after review fixes; actor-based validators now align with the repository's real JWT claim shape and the story bookkeeping matches the corrected implementation state.

### Findings addressed

- Fixed the runtime auth mismatch between `MapInboundClaims = false` tokens (`sub`) and actor validators (`ClaimTypes.NameIdentifier`) by normalizing `sub` into `ClaimTypes.NameIdentifier` during claims transformation.
- Expanded authentication regression coverage so the normalization behavior and idempotency are tested explicitly.
- Reconciled the story file list and change log with review-driven artifact updates (`17-2` story file and `sprint-status.yaml`).

### Scope note

- The live request pipeline continues to use the existing inline claims-based authorization in `CommandsController` and `AuthorizationBehavior`; wiring those abstractions into the runtime request path remains the planned scope of Story 17-3, so no cross-story behavior change was introduced here.

### Verification

- `EventStoreClaimsTransformationTests`, `ActorTenantValidatorTests`, `ActorRbacValidatorTests`, `CommandApiAuthorizationRegistrationTests`, and `EventStoreAuthorizationOptionsTests` pass after the review fix.
- Full `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj` run passes in this review session.

### Status recommendation

Story 17.2 can be marked `done`.
