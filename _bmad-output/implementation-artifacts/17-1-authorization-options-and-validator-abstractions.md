# Story 17.1: Authorization Options and Validator Abstractions

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **deployment operator**,
I want **configurable authorization that can delegate tenant and RBAC checks to application-managed DAPR actors instead of requiring all permissions in JWT claims**,
so that **JWT tokens stay lean (identity only) and the application can manage authorization dynamically at runtime**.

## Acceptance Criteria

1. **EventStoreAuthorizationOptions** configuration record exists with `TenantValidatorActorName` (string?) and `RbacValidatorActorName` (string?) properties, bound to `EventStore:Authorization` config section
2. **ITenantValidator** interface exists in `CommandApi/Authorization/` with async method `ValidateAsync(ClaimsPrincipal user, string tenantId, CancellationToken)` returning `TenantValidationResult`
3. **IRbacValidator** interface exists in `CommandApi/Authorization/` with async method `ValidateAsync(ClaimsPrincipal user, string tenantId, string domain, string messageType, string messageCategory, CancellationToken)` returning `RbacValidationResult` — where `messageCategory` is `"command"` or `"query"` (Amendment A1). The `domain` parameter is required for the domain claim check extracted from `AuthorizationBehavior.cs` line 53.
4. **ClaimsTenantValidator** extracts the exact tenant-checking logic from the current `CommandsController.cs` (lines 48-61) ONLY — preserving identical behavior. Note: `AuthorizationBehavior.cs` lines 40-43 only collect tenant claims for logging, they do NOT validate tenants.
5. **ClaimsRbacValidator** extracts the exact domain and permission-checking logic from the current `AuthorizationBehavior.cs` (lines 46-84) — preserving identical behavior
6. Both claims-based implementations are registered as the **default** when `TenantValidatorActorName` / `RbacValidatorActorName` are `null`
7. **Characterization tests** are written FIRST (before any extraction) that capture the current auth behavior of `AuthorizationBehavior` and `CommandsController` inline checks — these tests must pass before AND after extraction (Amendment A5)
8. Options class has an `IValidateOptions<T>` validator (matching project convention for options classes): `null` values are VALID (means claims-based default); only non-null values are validated as non-empty, non-whitespace strings
9. DI registration uses conditional logic: `null` config → claims-based; non-null → placeholder for actor-based (actual actor implementations are Story 17-2)
10. All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change

## Tasks / Subtasks

- [ ] Task 1: Create `EventStoreAuthorizationOptions` (AC: #1, #8)
  - [ ] 1.1 Create `src/Hexalith.EventStore.CommandApi/Configuration/EventStoreAuthorizationOptions.cs` record
  - [ ] 1.2 Create `ValidateEventStoreAuthorizationOptions` as `IValidateOptions<T>` validator (NOT FluentValidation — see AC #8)
  - [ ] 1.3 Register options in `AddCommandApi()` bound to `EventStore:Authorization`
- [ ] Task 2: Write characterization tests for existing auth behavior (AC: #7)
  - [ ] 2.1 Verify existing `AuthorizationBehaviorTests.cs` covers behavior-level characterization (domain, permission, wildcard, case-insensitive, no-claims-pass-through) — these already exist and serve as characterization tests
  - [ ] 2.2 **NEW: Controller-level tenant check characterization tests** — the inline check in `CommandsController.cs` lines 48-61 has ZERO test coverage today. Write tests for: no tenant claims → 403, wrong tenant → 403, matching tenant → proceeds, multiple tenants with one matching → succeeds
  - [ ] 2.3 Verify all characterization tests pass against CURRENT code before extraction
- [ ] Task 3: Create validator abstractions (AC: #2, #3)
  - [ ] 3.1 Create `ITenantValidator` interface in `CommandApi/Authorization/`
  - [ ] 3.2 Create `IRbacValidator` interface in `CommandApi/Authorization/`
  - [ ] 3.3 Create `TenantValidationResult` and `RbacValidationResult` types
- [ ] Task 4: Extract claims-based implementations (AC: #4, #5)
  - [ ] 4.1 Create `ClaimsTenantValidator` extracting from current inline code
  - [ ] 4.2 Create `ClaimsRbacValidator` extracting from current inline code
  - [ ] 4.3 Unit tests for both implementations
  - [ ] 4.4 **messageCategory contract test:** Verify `ClaimsRbacValidator` produces identical results for `"command"` and `"query"` messageCategory values (claims-based validation does not distinguish read/write — this is a contract that Story 17-2's actor-based implementation will diverge on)
- [ ] Task 5: Wire up DI registration (AC: #6, #9)
  - [ ] 5.1 Register `ITenantValidator` → `ClaimsTenantValidator` (default)
  - [ ] 5.2 Register `IRbacValidator` → `ClaimsRbacValidator` (default)
  - [ ] 5.3 Add conditional logic stub for actor-based registration (Story 17-2)
- [ ] Task 6: Verify zero regression (AC: #10)
  - [ ] 6.1 Run all Tier 1 tests (existing `AuthorizationBehaviorTests` must pass UNCHANGED — `AuthorizationBehavior.cs` is not modified in this story)
  - [ ] 6.2 Run new characterization tests against extracted validator implementations
  - [ ] 6.3 Two independent proofs of correctness: (a) existing behavior tests pass untouched, (b) new validator unit tests match extracted logic

## Dev Notes

### Critical: Naming Collision Avoidance

The **existing** `ITenantValidator` at `src/Hexalith.EventStore.Server/Actors/ITenantValidator.cs` is a DIFFERENT concern:
- **Existing (Server.Actors):** Defense-in-depth tenant isolation at DAPR actor level — validates command tenant matches actor ID tenant. Synchronous `void Validate(string commandTenantId, string actorId)`.
- **New (CommandApi.Authorization):** API-level authorization — validates user's claims/actor-delegated permission to access a tenant. Async, returns result object.

These are complementary layers (Layer 4 vs Layer 3 in the six-layer auth model). Use the full namespace to disambiguate. The new interface lives in `Hexalith.EventStore.CommandApi.Authorization` namespace.

### Architecture: Six-Layer Auth Model (Layers 3-4 Change)

```
Layer 1: TLS/HTTPS (infrastructure)
Layer 2: JWT Authentication (identity verification)
Layer 3: Endpoint Authorization → [Authorize] attribute (identity only)   ← SIMPLIFIED
Layer 4: MediatR AuthorizationBehavior → ITenantValidator + IRbacValidator ← EXTRACTED TO INTERFACES
Layer 5: DAPR Actor tenant validation (defense-in-depth, unchanged)
Layer 6: DAPR access control policies (infrastructure, unchanged)
```

### Extraction Points — Exact Code to Extract

**From `CommandsController.cs` (lines 48-61) ONLY → `ClaimsTenantValidator`:**
```csharp
// This inline tenant check moves into ClaimsTenantValidator.ValidateAsync():
var tenantClaims = User.FindAll("eventstore:tenant")
    .Select(c => c.Value)
    .Where(v => !string.IsNullOrWhiteSpace(v))
    .ToList();
if (tenantClaims.Count == 0) → TenantValidationResult.Denied("No tenant claims")
if (!tenantClaims.Any(t => Equals(t, request.Tenant, Ordinal))) → Denied
else → TenantValidationResult.Allowed
```

Note: `AuthorizationBehavior.cs` lines 40-43 also collect tenant claims, but ONLY for logging context on failure — there is NO tenant validation in the behavior. Tenant validation is exclusively in the controller.

**CRITICAL — String Comparison Asymmetry (preserve exactly, do NOT normalize):**
- **Tenant check** uses `StringComparison.Ordinal` (case-SENSITIVE, exact match) — this is deliberate because tenant IDs are system-assigned identifiers
- **Domain check** uses `StringComparison.OrdinalIgnoreCase` (case-INSENSITIVE) — this is deliberate because domain names may vary in casing
- **Permission check** uses `StringComparison.OrdinalIgnoreCase` (case-INSENSITIVE)
- Do NOT "fix" or "normalize" these to the same comparison type. The asymmetry is intentional.

**From `AuthorizationBehavior.cs` (lines 46-84) → `ClaimsRbacValidator`:**
```csharp
// Domain checking (lines 46-62) — uses `domain` parameter:
var domainClaims = user.FindAll("eventstore:domain")...
if (domainClaims.Count > 0 && !domainClaims.Any(d =>
    string.Equals(d, domain, OrdinalIgnoreCase))) → Denied

// Permission checking (lines 65-84) — uses `messageType` parameter:
var permissionClaims = user.FindAll("eventstore:permission")...
if (permissionClaims.Count > 0 && !hasWildcard && !hasSubmit && !hasSpecific) → Denied

// Story 17-3 call site will be:
// _rbacValidator.ValidateAsync(user, command.Tenant, command.Domain,
//     command.CommandType, "command", ct)
```

### Interface Design

```csharp
// New interfaces (CommandApi/Authorization/)
public interface ITenantValidator
{
    Task<TenantValidationResult> ValidateAsync(
        ClaimsPrincipal user, string tenantId, CancellationToken ct);
}

public interface IRbacValidator
{
    Task<RbacValidationResult> ValidateAsync(
        ClaimsPrincipal user, string tenantId, string domain,
        string messageType, string messageCategory, CancellationToken ct);
    // domain: required for domain claim check (AuthorizationBehavior line 53)
    // messageType: command type or query type
    // messageCategory: "command" or "query" (Amendment A1)
}

// Result types
public record TenantValidationResult(bool IsAuthorized, string? Reason = null)
{
    public static TenantValidationResult Allowed => new(true);
    public static TenantValidationResult Denied(string reason) => new(false, reason);
}

public record RbacValidationResult(bool IsAuthorized, string? Reason = null)
{
    public static RbacValidationResult Allowed => new(true);
    public static RbacValidationResult Denied(string reason) => new(false, reason);
}
```

### Result Type Design Decision

`TenantValidationResult` and `RbacValidationResult` have identical shape but are **separate records**. Do NOT introduce a shared base. Rationale:
- Different semantic meaning (tenant access vs role-based permission)
- Different evolution path: Story 17-2's actor-based implementations will add richer failure context (actor name, retry info) that diverges between the two
- Separate types prevent accidental type confusion at call sites

### messageCategory Contract

`ClaimsRbacValidator` accepts `messageCategory` (`"command"` / `"query"`) but produces **identical results** for both values. Claims-based authorization does not distinguish read vs write operations. This is by design — the parameter exists for Story 17-2's actor-based implementation which CAN discriminate. Unit tests must verify this invariant.

`ClaimsRbacValidator` uses the `domain` parameter to check domain claims (line 53 extraction) and the `messageType` parameter to check permission claims (lines 65-84 extraction). Both checks are within a single `ValidateAsync` call. Claims-based implementation does NOT validate `messageCategory` values — any string is accepted. Story 17-2's actor-based implementation should reject unrecognized values.

### Configuration Shape

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

- `null` (default) → claims-based authorization (current behavior, zero change)
- Non-null → DAPR actor-based authorization (Story 17-2 implements this path)

### Options Record and Validator

```csharp
// CommandApi/Configuration/EventStoreAuthorizationOptions.cs
public record EventStoreAuthorizationOptions
{
    public string? TenantValidatorActorName { get; init; }
    public string? RbacValidatorActorName { get; init; }
}

// IValidateOptions<T> pattern (matches ValidateEventStoreAuthenticationOptions,
// ValidateExtensionMetadataOptions, ValidateRateLimitingOptions in this project).
// Do NOT use FluentValidation AbstractValidator — that's for request DTOs only.
public class ValidateEventStoreAuthorizationOptions
    : IValidateOptions<EventStoreAuthorizationOptions>
{
    public ValidateOptionsResult Validate(string? name, EventStoreAuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // null = claims-based (VALID default). Only validate non-null values.
        if (options.TenantValidatorActorName is not null
            && string.IsNullOrWhiteSpace(options.TenantValidatorActorName))
        {
            return ValidateOptionsResult.Fail(
                "EventStore:Authorization:TenantValidatorActorName, when specified, must not be empty or whitespace.");
        }

        if (options.RbacValidatorActorName is not null
            && string.IsNullOrWhiteSpace(options.RbacValidatorActorName))
        {
            return ValidateOptionsResult.Fail(
                "EventStore:Authorization:RbacValidatorActorName, when specified, must not be empty or whitespace.");
        }

        return ValidateOptionsResult.Success;
    }
}
```

**Critical:** Do NOT make actor names required. `null` is the expected default for all existing deployments. Do NOT use FluentValidation for options classes — this project uses `IValidateOptions<T>` for all configuration options (see `ValidateEventStoreAuthenticationOptions`, `ValidateRateLimitingOptions`, `ValidateExtensionMetadataOptions`).

### DI Registration Pattern

Use the **factory delegate pattern** — register concrete implementations and resolve via `IOptions<T>` at runtime. Do NOT use `BuildServiceProvider()` (anti-pattern).

```csharp
// In AddCommandApi():
services.AddOptions<EventStoreAuthorizationOptions>()
    .BindConfiguration("EventStore:Authorization")
    .ValidateOnStart();

services.AddSingleton<IValidateOptions<EventStoreAuthorizationOptions>,
    ValidateEventStoreAuthorizationOptions>();

// Register concrete implementations (claims-based always available)
services.AddScoped<ClaimsTenantValidator>();
services.AddScoped<ClaimsRbacValidator>();

// Factory delegate selects implementation at resolve-time
services.AddScoped<ITenantValidator>(sp => {
    var opts = sp.GetRequiredService<IOptions<EventStoreAuthorizationOptions>>().Value;
    if (opts.TenantValidatorActorName is null)
        return sp.GetRequiredService<ClaimsTenantValidator>();
    // Story 17-2 adds: return sp.GetRequiredService<ActorTenantValidator>();
    throw new InvalidOperationException(
        $"Actor-based tenant validator '{opts.TenantValidatorActorName}' is configured but not yet implemented. Install Story 17-2.");
});

services.AddScoped<IRbacValidator>(sp => {
    var opts = sp.GetRequiredService<IOptions<EventStoreAuthorizationOptions>>().Value;
    if (opts.RbacValidatorActorName is null)
        return sp.GetRequiredService<ClaimsRbacValidator>();
    // Story 17-2 adds: return sp.GetRequiredService<ActorRbacValidator>();
    throw new InvalidOperationException(
        $"Actor-based RBAC validator '{opts.RbacValidatorActorName}' is configured but not yet implemented. Install Story 17-2.");
});
```

This follows the same pattern as `ConfigureJwtBearerOptions.cs` — options read at resolve-time, no eager `BuildServiceProvider()` call.

### Existing Test Patterns to Follow

From `AuthorizationBehaviorTests.cs`:
- Use `DefaultHttpContext` with `ClaimsPrincipal` for HTTP context mocking
- Use `NSubstitute` for `IHttpContextAccessor`
- Use `Shouldly` assertions
- Use `TestLogger<T>` pattern for log verification
- Naming: `ClassName_Scenario_ExpectedResult()`

**Controller tests (Task 2.2) require a DIFFERENT approach than behavior tests:**
- `CommandsController` accesses `User`, `HttpContext`, `Response` directly via `ControllerBase`
- Must set up `ControllerContext` with `DefaultHttpContext` and `ClaimsPrincipal`:
  ```csharp
  var controller = new CommandsController(mediator, sanitizer, logger);
  controller.ControllerContext = new ControllerContext {
      HttpContext = new DefaultHttpContext { User = principal }
  };
  ```
- Do NOT try to use `IHttpContextAccessor` — controllers access context directly

### Test File Locations

```
tests/Hexalith.EventStore.Server.Tests/
├── Pipeline/
│   └── AuthorizationBehaviorTests.cs       # EXISTING — do not modify
├── Authorization/
│   ├── ClaimsTenantValidatorTests.cs       # NEW — unit tests for extracted validator
│   └── ClaimsRbacValidatorTests.cs         # NEW — unit tests for extracted validator
├── Configuration/
│   └── EventStoreAuthorizationOptionsTests.cs  # NEW — options validator tests
├── Controllers/
│   └── CommandsControllerTenantTests.cs    # NEW — characterization tests for inline tenant check
```

### Files to Create

```
src/Hexalith.EventStore.CommandApi/
├── Authorization/
│   ├── ITenantValidator.cs           # Interface
│   ├── IRbacValidator.cs             # Interface
│   ├── TenantValidationResult.cs     # Result type
│   ├── RbacValidationResult.cs       # Result type
│   ├── ClaimsTenantValidator.cs      # Extracted from controller only (NOT behavior)
│   └── ClaimsRbacValidator.cs        # Extracted from behavior
├── Configuration/
│   └── EventStoreAuthorizationOptions.cs  # Options + validator
```

### Files to Modify

```
src/Hexalith.EventStore.CommandApi/
├── Extensions/ServiceCollectionExtensions.cs  # Add auth options + DI registration
```

### Files NOT to Modify Yet

- `CommandsController.cs` — inline tenant check stays until Story 17-3 (refactor)
- `AuthorizationBehavior.cs` — inline logic stays until Story 17-3 (refactor)
- These files will be refactored in Story 17-3 to USE the new abstractions

### Regression Test Strategy

Two independent proofs of correctness:
1. **Existing tests pass unchanged:** `AuthorizationBehaviorTests.cs` must pass without modification — `AuthorizationBehavior.cs` is NOT refactored in this story (that's 17-3). If existing tests break, something is wrong with the DI wiring.
2. **New validator unit tests match inline code:** The extracted `ClaimsTenantValidator` and `ClaimsRbacValidator` unit tests prove the extracted logic matches the inline code behavior.

This story creates abstractions and implementations but nothing *calls* them through the pipeline yet. That means: unit tests only, no integration tests showing validators called through MediatR. Integration proof comes in Story 17-3.

### Scope Boundary

**IN scope:** Options class, interfaces, result types, claims-based implementations, DI registration, characterization tests, unit tests.

**OUT of scope (later stories):**
- Actor-based validator implementations → Story 17-2
- Refactoring `AuthorizationBehavior` / `CommandsController` to USE the interfaces → Story 17-3
- `ValidateCommandRequest`/`ValidateQueryRequest` with optional `AggregateId` (Amendment A2) → Stories 17-4, 17-7, 17-8
- Query contracts and endpoints → Stories 17-4, 17-5, 17-6

### Backward Compatibility

- Claims-based is the DEFAULT. No configuration needed for existing deployments.
- No existing endpoint behavior changes.
- No JWT claim format changes.
- No NuGet package API changes.

### Project Structure Notes

- All new files in `CommandApi` project (not `Contracts` — authorization is server-side concern)
- `Authorization/` subfolder follows existing `Authentication/`, `Configuration/`, `Pipeline/` conventions
- Namespace: `Hexalith.EventStore.CommandApi.Authorization`

### References

- [Source: sprint-change-proposal-2026-03-08-auth-query.md — Section 4.2, 4.3 Story 17-1]
- [Source: architecture.md — Six-layer auth model, D5 Authorization Pipeline]
- [Source: CommandsController.cs — Lines 48-61, pre-pipeline tenant check]
- [Source: AuthorizationBehavior.cs — Lines 40-84, domain + permission checks]
- [Source: ServiceCollectionExtensions.cs — AddCommandApi() DI registration]
- [Source: AuthorizationBehaviorTests.cs — Test patterns and helpers]
- [Source: Server/Actors/ITenantValidator.cs — Existing actor-level validator (DIFFERENT concern)]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
