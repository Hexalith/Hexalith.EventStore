# Story 7.3: Per-Consumer Rate Limiting

Status: ready-for-dev

## Story

As a platform developer,
I want per-consumer rate limiting at the Command API,
so that individual consumers cannot overwhelm the system.

## Acceptance Criteria

1. **Given** an authenticated consumer,
   **When** they exceed the configurable threshold (default: 100 commands/second/consumer),
   **Then** the system returns `429 Too Many Requests` with `Retry-After` header (NFR34)
   **And** the consumer identity is derived from the JWT `sub` claim.

## Context: What Already Exists

Per-tenant rate limiting is **fully implemented** (Stories 2.9 + 7.2). The following infrastructure is in place:

- `RateLimitingOptions.cs` — Options record with `PermitLimit=1000`, `WindowSeconds=60`, `SegmentsPerWindow=6`, `QueueLimit=0`, `TenantPermitLimits` dictionary
- `ValidateRateLimitingOptions` — `IValidateOptions<RateLimitingOptions>` validator
- `ServiceCollectionExtensions.cs` (lines 133–270) — `PartitionedRateLimiter<HttpContext, string>` keyed by `eventstore:tenant` claim with `SlidingWindowRateLimiter`, `OnRejected` callback producing RFC 7807 ProblemDetails with `Retry-After`, `correlationId`, `tenantId` extensions
- `DaprRateLimitConfigSync.cs` — `IHostedService` for DAPR config store sync (optional, graceful fallback)
- `Program.cs` line 28 — `app.UseRateLimiter()` positioned after `UseAuthentication`, before `UseAuthorization`
- `ProblemTypeUris.RateLimitExceeded` — `"https://hexalith.io/problems/rate-limit-exceeded"`
- Health endpoints (`/health`, `/alive`, `/ready`) exempted from rate limiting
- `RateLimitingIntegrationTests.cs` — 11 per-tenant integration tests
- `PerTenantRateLimitingTests.cs` — 6 per-tenant override tests

**Consumer identity** is already available via JWT:
- `EventStoreClaimsTransformation` extracts the `sub` claim and creates a `ClaimTypes.NameIdentifier` claim
- `TestJwtTokenGenerator.GenerateToken(subject: "test-user")` already supports setting the `sub` claim

**This story layers per-consumer rate limiting alongside the existing per-tenant rate limiting.**

## Tasks / Subtasks

- [ ] Task 1: Extend `RateLimitingOptions` with per-consumer configuration (AC: #1)
  - [ ] 1.1 Add `ConsumerPermitLimit` property (default: `100`) — maximum commands per window per consumer
  - [ ] 1.2 Add `ConsumerWindowSeconds` property (default: `1`) — per-consumer sliding window duration
  - [ ] 1.3 Add `ConsumerSegmentsPerWindow` property (default: `1`) — segments for per-consumer sliding window. Note: with `SegmentsPerWindow=1` this is effectively a fixed window (not truly sliding). Add a code comment explaining this is intentional for the "per second" NFR34 requirement — users who want smoother sliding behavior can increase segments (e.g., `ConsumerWindowSeconds=10`, `ConsumerSegmentsPerWindow=10`, `ConsumerPermitLimit=1000`)
  - [ ] 1.4 Add `Dictionary<string, int> ConsumerPermitLimits` for per-consumer overrides (same pattern as `TenantPermitLimits`)
  - [ ] 1.5 Update `ValidateRateLimitingOptions` to validate: `ConsumerPermitLimit > 0`, `ConsumerWindowSeconds > 0`, `ConsumerSegmentsPerWindow >= 1`, all `ConsumerPermitLimits` values > 0

- [ ] Task 2: Add per-consumer rate limiter using `CreateChained` (AC: #1)
  - [ ] 2.1 Refactor `GlobalLimiter` from a single `PartitionedRateLimiter.Create<HttpContext, string>` to `PartitionedRateLimiter.CreateChained<HttpContext>(tenantLimiter, consumerLimiter)`. Add a code comment: "CreateChained short-circuits on first rejection — Retry-After reflects the rejecting limiter's window (tenant=60s, consumer=1s). This is correct: clients should retry based on the limit they actually hit."
  - [ ] 2.2 Extract the existing per-tenant limiter into a local variable (no logic changes)
  - [ ] 2.3 Create the per-consumer limiter: `PartitionedRateLimiter.Create<HttpContext, string>` keyed by the JWT `sub` claim. Use `string.IsNullOrEmpty()` guard (not null-coalescing) to catch both null and empty `sub` values: `string consumerId = context.User?.FindFirst("sub")?.Value; if (string.IsNullOrEmpty(consumerId)) consumerId = "anonymous";`
  - [ ] 2.4 Per-consumer limiter must exempt health endpoints (`/health`, `/alive`, `/ready`) — same check as per-tenant limiter
  - [ ] 2.5 Per-consumer limiter resolves permit limit: `ConsumerPermitLimits[consumerId]` > `ConsumerPermitLimit` (default)
  - [ ] 2.6 Per-consumer limiter uses `ConsumerWindowSeconds` and `ConsumerSegmentsPerWindow` from options
  - [ ] 2.7 Per-consumer limiter reuses `QueueLimit` from the shared options (QueueLimit=0 means immediate rejection). Add a code comment noting this is intentionally shared — if per-consumer queuing is ever needed, it would require a separate `ConsumerQueueLimit` property

- [ ] Task 3: Enhance `OnRejected` callback with consumer context (AC: #1)
  - [ ] 3.1 Extract the `sub` claim value as `consumerId` and include it in the ProblemDetails `Extensions` dictionary alongside existing `correlationId` and `tenantId`
  - [ ] 3.2 Include `consumerId` in the warning log template
  - [ ] 3.3 Update the `Detail` message to be generic: "Rate limit exceeded. Please retry after the specified interval." (remove tenant-specific wording since either limiter could trigger rejection)

- [ ] Task 4: Update `appsettings.json` with consumer rate limiting defaults (AC: #1)
  - [ ] 4.1 Add `ConsumerPermitLimit`, `ConsumerWindowSeconds`, `ConsumerSegmentsPerWindow`, and `ConsumerPermitLimits` under `"EventStore:RateLimiting"`

- [ ] Task 5: Add unit tests for per-consumer options validation (AC: #1)
  - [ ] 5.1 Test: `ValidateRateLimitingOptions` rejects `ConsumerPermitLimit <= 0`
  - [ ] 5.2 Test: `ValidateRateLimitingOptions` rejects `ConsumerWindowSeconds <= 0`
  - [ ] 5.3 Test: `ValidateRateLimitingOptions` rejects `ConsumerSegmentsPerWindow < 1`
  - [ ] 5.4 Test: `ValidateRateLimitingOptions` rejects `ConsumerPermitLimits` values <= 0
  - [ ] 5.5 Test: `ValidateRateLimitingOptions` accepts valid consumer options

- [ ] Task 6: Add integration tests for per-consumer rate limiting (AC: #1)
  - [ ] 6.1 Create `PerConsumerRateLimitingWebApplicationFactory` extending `JwtAuthenticatedWebApplicationFactory` with `ConsumerPermitLimit=2`, `ConsumerWindowSeconds=60` (use long window for test stability — production default is `ConsumerWindowSeconds=1` per NFR34, but 1-second windows cause flaky CI tests), `ConsumerSegmentsPerWindow=1`
  - [ ] 6.2 Test: Same consumer exceeds per-consumer limit → 429
  - [ ] 6.3 Test: Different consumers (different `sub` claims, same tenant) → independent per-consumer limits
  - [ ] 6.4 Test: Per-consumer override applies (consumer in `ConsumerPermitLimits` gets higher limit)
  - [ ] 6.5 Test: 429 response includes `consumerId` in ProblemDetails extensions
  - [ ] 6.6 Test: Health endpoints remain exempted from per-consumer limiting
  - [ ] 6.7 Test: When consumer hits per-consumer limit, 429 response includes valid `Retry-After` header and both `tenantId` and `consumerId` in ProblemDetails extensions (validates `CreateChained` short-circuit produces correct response regardless of which limiter rejects)
  - [ ] 6.8 Test: Anonymous consumer partition — unauthenticated request to a non-health endpoint that bypasses auth falls back to `"anonymous"` consumer partition key (verify 429 after exceeding limit with `consumerId: "anonymous"` in ProblemDetails)

- [ ] Task 7: Verify existing tests pass — zero regressions (AC: #1)
  - [ ] 7.1 Run Tier 1 tests: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ && dotnet test tests/Hexalith.EventStore.Client.Tests/ && dotnet test tests/Hexalith.EventStore.Sample.Tests/ && dotnet test tests/Hexalith.EventStore.Testing.Tests/`
  - [ ] 7.2 Run Tier 2 tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/`
  - [ ] 7.3 Run existing rate limiting tests: all 11 `RateLimitingIntegrationTests` + all 6 `PerTenantRateLimitingTests` must pass unchanged
  - [ ] 7.4 Run full Tier 3: `dotnet test tests/Hexalith.EventStore.IntegrationTests/`

## Dev Notes

### Critical: This Is an Enhancement, Not a New Build

The per-tenant rate limiting infrastructure is fully operational. This story ADDS a second rate limiter dimension (per-consumer) alongside the existing per-tenant one. Do NOT:
- Rewrite the existing per-tenant rate limiter logic
- Change the middleware pipeline order in `Program.cs`
- Modify the existing `OnRejected` callback structure (only add `consumerId`)
- Touch health endpoint exemption logic (replicate it for the consumer limiter)
- Alter existing integration tests

### Architecture: `CreateChained` Composition

ASP.NET Core's `PartitionedRateLimiter.CreateChained<TResource>()` composes multiple limiters into one. A request is rejected if **any** chained limiter rejects it. `CreateChained` short-circuits on first rejection — subsequent limiters are not evaluated. This means:

- The `Retry-After` value in the `OnRejected` callback comes from whichever limiter rejected first (tenant window = 60s, consumer window = 1s). Clients may see different `Retry-After` values depending on which limit they hit. This is correct behavior — add a code comment documenting it.
- The ordering of limiters in `CreateChained(tenantLimiter, consumerLimiter)` determines evaluation order. Tenant limiter runs first. If tenant rejects, consumer limiter is not checked.

This is the idiomatic approach for layered rate limiting.

```csharp
// Conceptual structure in ServiceCollectionExtensions.cs
var tenantLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
{
    // Existing per-tenant logic (unchanged)
    string tenantId = context.User?.FindFirst("eventstore:tenant")?.Value ?? "anonymous";
    // ... health check exemption, options resolution ...
    return RateLimitPartition.GetSlidingWindowLimiter(tenantId, _ => new SlidingWindowRateLimiterOptions { ... });
});

var consumerLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
{
    // Health check exemption (same pattern)
    string path = context.Request.Path.Value ?? string.Empty;
    if (path.Equals("/health", ...) || path.Equals("/alive", ...) || path.Equals("/ready", ...))
        return RateLimitPartition.GetNoLimiter<string>("__health");

    string consumerId = context.User?.FindFirst("sub")?.Value ?? "anonymous";
    IOptionsMonitor<RateLimitingOptions> monitor = context.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitingOptions>>();
    RateLimitingOptions opts = monitor.CurrentValue;

    int permitLimit = opts.ConsumerPermitLimits.TryGetValue(consumerId, out int consumerLimit)
        ? consumerLimit
        : opts.ConsumerPermitLimit;

    return RateLimitPartition.GetSlidingWindowLimiter(consumerId, _ => new SlidingWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromSeconds(opts.ConsumerWindowSeconds),
        SegmentsPerWindow = opts.ConsumerSegmentsPerWindow,
        QueueLimit = opts.QueueLimit,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    });
});

rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.CreateChained(tenantLimiter, consumerLimiter);
```

### Consumer Identity: JWT `sub` Claim

The consumer identity is the JWT `sub` (subject) claim, which identifies the authenticated user/service account. This is already available in the `ClaimsPrincipal` after authentication.

- Use `context.User?.FindFirst("sub")?.Value ?? "anonymous"` for the partition key
- Do NOT use `ClaimTypes.NameIdentifier` (it's a derived claim set by `EventStoreClaimsTransformation`)
- The `sub` claim is the canonical OIDC subject identifier
- **Anonymous partition:** Unauthenticated requests (e.g., those bypassing auth, CORS preflight) all map to the `"anonymous"` consumer partition. With `ConsumerPermitLimit=100/s` this is generous, but add a code comment noting that all anonymous traffic shares one consumer bucket. Health endpoints are exempted, so this only affects non-health unauthenticated requests.
- **Configurable anonymous throttle:** `ConsumerPermitLimits: { "anonymous": 10 }` is a supported pattern for explicitly throttling unauthenticated traffic. Document this in a code comment on the `ConsumerPermitLimits` property as a usage example.
- **Cross-IdP `sub` collision:** Two different identity providers could issue tokens with the same `sub` value (e.g., `"user-123"`). These would share a consumer rate limit partition even though they're different users. The per-tenant limiter provides secondary protection since they'd be in different tenants. This is an acceptable trade-off — a composite key like `{tenant}:{sub}` would conflate the two independent rate limiting dimensions

### Per-Consumer vs Per-Tenant: Independent Concerns

| Dimension | Per-Tenant (existing) | Per-Consumer (this story) |
|---|---|---|
| Partition key | `eventstore:tenant` claim | `sub` claim |
| Default limit | 1,000 commands/minute | 100 commands/second |
| Default window | 60 seconds | 1 second |
| Override dictionary | `TenantPermitLimits` | `ConsumerPermitLimits` |
| Purpose | Prevent one tenant from starving others | Prevent one consumer from overwhelming the system |

A request can be rejected by EITHER limiter. Example: a consumer within a well-behaved tenant can still be individually rate-limited if they burst too fast.

**Primary protection target:** M2M service accounts (e.g., `sub: "order-processing-service"`) — the saga storm scenario from GAP-9. All instances of a service account share one consumer partition, which is the intended behavior (limit the *service*, not the *instance*). Human users (unique `sub` per person) are effectively invisible to the consumer limiter at 100/sec — no human generates that traffic. The `ConsumerPermitLimits` override dictionary is the escape valve for legitimate high-throughput M2M consumers.

### PartitionedRateLimiter Caching Behavior (same as Story 7.2)

`CreateChained` does not change the caching behavior — each inner `PartitionedRateLimiter` caches `SlidingWindowRateLimiter` instances per partition key independently. Updated options via `IOptionsMonitor` only affect new partitions. Existing active partitions keep old limits until idle timeout. This is acceptable.

### appsettings.json Update

```json
{
  "EventStore": {
    "RateLimiting": {
      "PermitLimit": 1000,
      "WindowSeconds": 60,
      "SegmentsPerWindow": 6,
      "QueueLimit": 0,
      "TenantPermitLimits": {},
      "ConsumerPermitLimit": 100,
      "ConsumerWindowSeconds": 1,
      "ConsumerSegmentsPerWindow": 1,
      "ConsumerPermitLimits": {}
    }
  }
}
```

### OnRejected Enhancement

The existing `OnRejected` callback should be updated minimally:

1. Extract `consumerId` from `sub` claim (use `IsNullOrEmpty` to catch empty strings, same as limiter):
   ```csharp
   string? rawConsumerId = context.HttpContext.User?.FindFirst("sub")?.Value;
   string consumerId = string.IsNullOrEmpty(rawConsumerId) ? "unknown" : rawConsumerId;
   ```

2. Add to ProblemDetails extensions:
   ```csharp
   Extensions = {
       ["correlationId"] = correlationId,
       ["tenantId"] = tenantId,
       ["consumerId"] = consumerId,
   }
   ```

3. Update log template:
   ```csharp
   logger.LogWarning(
       "Rate limit exceeded: CorrelationId={CorrelationId}, TenantId={TenantId}, ConsumerId={ConsumerId}, SourceIP={SourceIP}",
       correlationId, tenantId, consumerId, sourceIp);
   ```

4. Update `Detail` message:
   ```csharp
   Detail = "Rate limit exceeded. Please retry after the specified interval."
   ```

5. **Do NOT add `consumerId` to the fallback catch block** (lines ~227+). The fallback exists for when the primary `OnRejected` throws — adding another `FindFirst("sub")` in the catch could also throw if `HttpContext.User` is in a bad state. Keep the fallback minimal.

### Test Factory for Per-Consumer Tests

Create `PerConsumerRateLimitingWebApplicationFactory` extending `JwtAuthenticatedWebApplicationFactory`:

```csharp
// Use long window (60s) with SegmentsPerWindow=1 for test stability
// Short windows (1s) cause flaky tests in CI
["EventStore:RateLimiting:ConsumerPermitLimit"] = "2",
["EventStore:RateLimiting:ConsumerWindowSeconds"] = "60",
["EventStore:RateLimiting:ConsumerSegmentsPerWindow"] = "1",
// Set high tenant limit so per-tenant limiter doesn't interfere
["EventStore:RateLimiting:PermitLimit"] = "1000",
// Per-consumer override for testing
["EventStore:RateLimiting:ConsumerPermitLimits:premium-consumer"] = "4",
```

Use different `subject` values in `TestJwtTokenGenerator.GenerateToken(subject: "consumer-a")` to create distinct consumers.

### Existing Files to Modify

| File | Change |
|---|---|
| `src/Hexalith.EventStore.CommandApi/Configuration/RateLimitingOptions.cs` | Add `ConsumerPermitLimit`, `ConsumerWindowSeconds`, `ConsumerSegmentsPerWindow`, `ConsumerPermitLimits`; update validator |
| `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` | Refactor `GlobalLimiter` to `CreateChained`, add consumer limiter, enhance `OnRejected` with `consumerId` |
| `src/Hexalith.EventStore.CommandApi/appsettings.json` | Add consumer rate limiting properties |

### New Files to Create

| File | Purpose |
|---|---|
| `tests/Hexalith.EventStore.IntegrationTests/CommandApi/PerConsumerRateLimitingTests.cs` | Integration tests for per-consumer rate limiting |
| `tests/Hexalith.EventStore.IntegrationTests/Helpers/PerConsumerRateLimitingWebApplicationFactory.cs` | Test factory with per-consumer overrides |

### Existing Files — DO NOT MODIFY

| File | Reason |
|---|---|
| `src/Hexalith.EventStore.CommandApi/Program.cs` | Middleware pipeline is correct |
| `src/Hexalith.EventStore.CommandApi/Authentication/EventStoreClaimsTransformation.cs` | Already extracts `sub` claim — no changes needed |
| `src/Hexalith.EventStore.CommandApi/ErrorHandling/ProblemTypeUris.cs` | Same URI for all rate limit rejections |
| `tests/.../RateLimitingIntegrationTests.cs` | Existing 11 tests must pass unchanged |
| `tests/.../PerTenantRateLimitingTests.cs` | Existing 6 tests must pass unchanged |
| `tests/.../RateLimitingWebApplicationFactory.cs` | Existing test factory must remain unchanged |
| `tests/.../PerTenantRateLimitingWebApplicationFactory.cs` | Existing test factory must remain unchanged |
| `src/Hexalith.EventStore.CommandApi/Configuration/DaprRateLimitConfigSync.cs` | DAPR sync is separate. Dynamic DAPR config store sync for `ConsumerPermitLimits` is **out of scope** — NFR34 requires "configurable threshold" (satisfied by `appsettings.json` + `IOptionsMonitor`), not dynamic DAPR updates. Extending `DaprRateLimitConfigSync` for consumer overrides is a future enhancement if needed |

### Files That Do NOT Exist — Do NOT Create

| File | Why |
|---|---|
| `src/Hexalith.EventStore.CommandApi/Middleware/ConsumerRateLimitingMiddleware.cs` | Rate limiting is via `AddRateLimiter()` in `ServiceCollectionExtensions.cs`, NOT as custom middleware |

### Coding Conventions (from .editorconfig)

- File-scoped namespaces: `namespace X.Y.Z;`
- Allman braces (new line before `{`)
- Private fields: `_camelCase`
- Async suffix on async methods
- 4-space indentation, CRLF, UTF-8
- Nullable enabled, implicit usings enabled
- Warnings as errors (`TreatWarningsAsErrors = true`)

### Test Conventions

- xUnit 2.9.3, Shouldly 4.3.0 (fluent), NSubstitute 5.3.0
- Rate limiting tests live in Tier 3: `tests/Hexalith.EventStore.IntegrationTests/CommandApi/`
- Test factory pattern: extend `JwtAuthenticatedWebApplicationFactory`, override config via `AddInMemoryCollection`
- Use `TestJwtTokenGenerator.GenerateToken(subject: "consumer-id", tenants: [...], domains: [...])` to create consumer-specific tokens
- Each test method should use unique consumer IDs to avoid cross-test interference
- Use `extern alias commandapi;` at top of test files referencing CommandApi types

### Project Structure Notes

- Rate limiting is a CommandApi concern — all code lives in `src/Hexalith.EventStore.CommandApi/`
- Configuration options: `Configuration/` subdirectory
- Service registration: `Extensions/ServiceCollectionExtensions.cs`
- Options binding pattern: `"EventStore:RateLimiting"` config section with `ValidateOnStart()`

### Previous Story Intelligence (Story 7.2)

- Story 7.2 completed 2026-03-18; per-tenant rate limiting with DAPR dynamic config fully working
- Established two-tier resolution pattern: `TenantPermitLimits[tenantId]` > `PermitLimit` (default)
- Key learning: Pre-existing Tier 3 failures — 75/192 tests fail on main before any changes; 4/12 rate limiting tests return 400 (command validation issue, not rate limiting)
- `DaprRateLimitConfigSync` initially caused startup timeout — fixed with 5s `CancellationToken` timeout
- `IOptionsMonitor<RateLimitingOptions>` is already wired — consumer options will automatically benefit from dynamic reload
- `PartitionedRateLimiter` caching behavior is documented — same applies to per-consumer partitions

### Git Intelligence

Recent commits (2026-03-18):
- `e2eeec8` feat: Update sprint status and add Story 7.2 for Per-Tenant Rate Limiting
- `ff7a64c` Merge Story 7.1: Configurable Aggregate Snapshots
- `2933980` Merge Story 6.3: Health and Readiness Endpoints

Story 7.2 changes are uncommitted on the working tree (status: review). The per-consumer limiter builds directly on these uncommitted changes.

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Epic 7, Story 7.3]
- [Source: _bmad-output/planning-artifacts/prd.md — NFR34]
- [Source: _bmad-output/planning-artifacts/architecture.md — D8: Rate Limiting]
- [Source: src/Hexalith.EventStore.CommandApi/Configuration/RateLimitingOptions.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs#L133-270]
- [Source: src/Hexalith.EventStore.CommandApi/Authentication/EventStoreClaimsTransformation.cs]
- [Source: tests/Hexalith.EventStore.IntegrationTests/CommandApi/RateLimitingIntegrationTests.cs]
- [Source: tests/Hexalith.EventStore.IntegrationTests/CommandApi/PerTenantRateLimitingTests.cs]
- [Source: _bmad-output/implementation-artifacts/7-2-per-tenant-rate-limiting.md — Previous story intelligence]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
