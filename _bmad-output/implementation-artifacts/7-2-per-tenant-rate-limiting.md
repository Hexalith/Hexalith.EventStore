# Story 7.2: Per-Tenant Rate Limiting

Status: ready-for-dev

## Story

As a platform developer,
I want per-tenant rate limiting at the Command API,
so that one tenant's saga storm cannot degrade service for other tenants.

## Acceptance Criteria

1. **Given** ASP.NET Core `RateLimiting` middleware configured with `SlidingWindowRateLimiter` (D8),
   **When** a tenant exceeds the configurable threshold (default: 1,000 commands/minute/tenant),
   **Then** the system returns `429 Too Many Requests` with `Retry-After` header (NFR33)
   **And** rate limits are extracted from the JWT `eventstore:tenant` claim.

2. **Given** rate limits configured per tenant via DAPR config store,
   **When** the configuration is changed,
   **Then** the new limits take effect dynamically without system restart (NFR20)
   **And** new limits apply within one sliding window cycle (~60 seconds) for active tenants and immediately for new tenants.

## Context: What Already Exists

Per-tenant rate limiting infrastructure is **already implemented** (Story 2.9). The following are in place and working:

- `RateLimitingOptions.cs` — Options record with `PermitLimit=100`, `WindowSeconds=60`, `SegmentsPerWindow=6`, `QueueLimit=0`
- `ValidateRateLimitingOptions` — `IValidateOptions<RateLimitingOptions>` validator
- `ServiceCollectionExtensions.cs` (lines 123–259) — Full `PartitionedRateLimiter<HttpContext, string>` keyed by `eventstore:tenant` claim with `SlidingWindowRateLimiter`, `OnRejected` callback producing RFC 7807 ProblemDetails with `Retry-After` header, `correlationId`, `tenantId` extensions
- `Program.cs` line 28 — `app.UseRateLimiter()` positioned after `UseAuthentication`, before `UseAuthorization`
- `ProblemTypeUris.RateLimitExceeded` — `"https://hexalith.io/problems/rate-limit-exceeded"`
- Health endpoints (`/health`, `/alive`, `/ready`) exempted from rate limiting
- `RateLimitingIntegrationTests.cs` — 11 comprehensive integration tests covering 429 responses, Retry-After headers, ProblemDetails format, tenant isolation, health endpoint exemption
- `RateLimitingWebApplicationFactory.cs` — Test factory with `PermitLimit=2` for fast testing

**This story is NOT a green-field build.** It enhances the existing implementation with two specific changes.

## Tasks / Subtasks

- [ ] Task 1: Update default threshold to 1,000 commands/minute (AC: #1)
  - [ ] 1.1 Change `RateLimitingOptions.PermitLimit` default from `100` to `1000`
  - [ ] 1.2 Update any hardcoded references to the old default in comments/docs
  - [ ] 1.3 Verify existing integration tests still pass (test factory overrides to `PermitLimit=2`, so unaffected)

- [ ] Task 2: Add per-tenant rate limit overrides via three-tier resolution (AC: #1, #2)
  - [ ] 2.1 Extend `RateLimitingOptions` with `Dictionary<string, int> TenantPermitLimits` for per-tenant overrides (follow `SnapshotOptions` three-tier pattern from Story 7.1)
  - [ ] 2.2 Update `ValidateRateLimitingOptions` to validate per-tenant entries (each value > 0)
  - [ ] 2.3 Update the `PartitionedRateLimiter` factory lambda in `ServiceCollectionExtensions.cs` to resolve permit limit: `TenantPermitLimits[tenantId]` > `PermitLimit` (default)
  - [ ] 2.4 Bind from `"EventStore:RateLimiting"` config section (already bound, just add new property)

- [ ] Task 3: Add DAPR config store integration for dynamic updates (AC: #2)
  - [ ] 3.1 Change `IOptions<RateLimitingOptions>` to `IOptionsMonitor<RateLimitingOptions>` in the rate limiter factory lambda (use `.CurrentValue` instead of `.Value`). This is the minimal change that enables dynamic reload.
  - [ ] 3.2 Add `TenantPermitLimits` entries to `appsettings.json` under `"EventStore:RateLimiting"` as the static baseline (see appsettings example in Dev Notes). These values are the fallback when DAPR config store is unavailable.
  - [ ] 3.3 **Prescribed DAPR pattern:** Follow the existing `DomainServiceResolver` in `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs` which uses `daprClient.GetConfiguration(configStoreName, [configKey], ct)`. Create an `IHostedService` (e.g., `DaprRateLimitConfigSync`) that on startup reads DAPR config store keys and updates `IConfiguration` via `IConfigurationRoot.Reload()`. The service MUST be **optional** — if DAPR sidecar is unavailable (e.g., in WebApplicationFactory tests), log a warning and fall back to `appsettings.json` values. Do NOT throw on startup.
  - [ ] 3.4 DAPR config store keys are flat key-value pairs (not nested JSON). Key pattern example: `ratelimit:tenant-acme:permit-limit` → `"5000"`. The existing codebase uses pipe-separated keys like `tenant-a|orders|v1` for domain service config — follow whatever convention fits the flat key model.
  - [ ] 3.5 **No new NuGet packages needed.** `Dapr.AspNetCore` is already in `CommandApi.csproj` (line 21) and provides `DaprClient` transitively.
  - [ ] 3.6 **CRITICAL:** `PartitionedRateLimiter.Create` caches `SlidingWindowRateLimiter` instances per partition key. Updated options only affect partitions created *after* the change. Existing active partitions keep their old limits until they expire (after one idle window cycle, ~60 seconds). Accept this eventual-consistency — do NOT build a custom wrapper to force-recreate partitions. Document this in code comments.
  - [ ] 3.7 **Fallback behavior:** When DAPR config store is unavailable, the system MUST continue functioning using `appsettings.json` values. Log a warning, do not fail.

- [ ] Task 4: Add unit tests for per-tenant resolution in `tests/Hexalith.EventStore.IntegrationTests/CommandApi/` (AC: #1)
  - [ ] 4.1 Test: tenant with `TenantPermitLimits` override gets the override limit (not the default)
  - [ ] 4.2 Test: tenant without override gets the default `PermitLimit`
  - [ ] 4.3 Test: `ValidateRateLimitingOptions` rejects per-tenant values <= 0
  - [ ] 4.4 Note: Unit-style tests for options resolution live alongside integration tests since there is no separate `CommandApi.Tests` project

- [ ] Task 5: Add integration tests for per-tenant overrides and verify regression (AC: #1, #2)
  - [ ] 5.1 Test: per-tenant override applies correctly — configure tenant "premium" with higher limit, verify it can send more requests than the default before getting 429
  - [ ] 5.2 Test: verify existing 11 rate limiting tests still pass unchanged
  - [ ] 5.3 Note on dynamic reload testing: Do NOT write a flaky time-dependent test for DAPR config store subscription. The dynamic reload mechanism is validated by: (a) `IOptionsMonitor<T>` wiring is correct, (b) per-tenant resolution logic is tested, (c) DAPR subscription is validated manually in a DAPR environment. Document this decision in test comments.

- [ ] Task 6: Full regression (AC: #1, #2)
  - [ ] 6.1 Run Tier 1 tests: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ && dotnet test tests/Hexalith.EventStore.Client.Tests/ && dotnet test tests/Hexalith.EventStore.Sample.Tests/ && dotnet test tests/Hexalith.EventStore.Testing.Tests/`
  - [ ] 6.2 Run Tier 2 tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/`
  - [ ] 6.3 Run Tier 3 tests: `dotnet test tests/Hexalith.EventStore.IntegrationTests/`

## Dev Notes

### Critical: This Is an Enhancement, Not a New Build

The base per-tenant rate limiting works end-to-end. Do NOT:
- Rewrite the rate limiter registration in `ServiceCollectionExtensions.cs`
- Change the middleware pipeline order in `Program.cs`
- Modify the `OnRejected` callback (it's correct and comprehensive)
- Touch the health endpoint exemption logic
- Alter the existing integration tests

**Only** extend `RateLimitingOptions` and the partition factory lambda.

### Architecture Compliance

| Requirement | Reference | Status |
|---|---|---|
| ASP.NET Core RateLimiting middleware | D8 | Already implemented |
| SlidingWindowRateLimiter algorithm | D8 | Already implemented |
| JWT tenant claim extraction | D8, `eventstore:tenant` | Already implemented |
| 429 + Retry-After response | NFR33 | Already implemented |
| RFC 7807 ProblemDetails | D5 | Already implemented |
| Dynamic config via DAPR | NFR20 | **NEW — Task 3** |
| Default 1,000 cmd/min/tenant | NFR33 | **UPDATE — Task 1** |
| Per-tenant overrides | D8 | **NEW — Task 2** |

### Pattern to Follow: Three-Tier Configuration (Story 7.1)

`SnapshotOptions.cs` established the three-tier resolution pattern:

```csharp
public record SnapshotOptions
{
    public int DefaultInterval { get; init; } = 100;
    public Dictionary<string, int> DomainIntervals { get; init; } = [];
    public Dictionary<string, int> TenantDomainIntervals { get; init; } = [];
    // Resolution: TenantDomainIntervals > DomainIntervals > DefaultInterval
}
```

For rate limiting, a simpler two-tier pattern is sufficient (no domain dimension):

```csharp
// Extend existing RateLimitingOptions
public record RateLimitingOptions
{
    public int PermitLimit { get; init; } = 1000;  // Default (was 100)
    public int WindowSeconds { get; init; } = 60;
    public int SegmentsPerWindow { get; init; } = 6;
    public int QueueLimit { get; init; } = 0;
    public Dictionary<string, int> TenantPermitLimits { get; init; } = [];  // NEW
}
```

Resolution in the partition factory lambda:

```csharp
int permitLimit = rateLimitOptions.TenantPermitLimits.TryGetValue(tenantId, out int tenantLimit)
    ? tenantLimit
    : rateLimitOptions.PermitLimit;
```

### appsettings.json Example

The `"EventStore:RateLimiting"` section should look like this after Task 1 + Task 2:

```json
{
  "EventStore": {
    "RateLimiting": {
      "PermitLimit": 1000,
      "WindowSeconds": 60,
      "SegmentsPerWindow": 6,
      "QueueLimit": 0,
      "TenantPermitLimits": {
        "premium-tenant": 5000,
        "trial-tenant": 200
      }
    }
  }
}
```

### DAPR Config Store Dynamic Updates

Key considerations:

- Use `IOptionsMonitor<RateLimitingOptions>` instead of `IOptions<RateLimitingOptions>` to support runtime changes
- The current rate limiter factory reads options via `context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>()` — change to `IOptionsMonitor<RateLimitingOptions>` resolved via `GetRequiredService`, then use `.CurrentValue`
- DAPR config store keys are flat key-value pairs — see Task 3.4 for key pattern
- The existing `DomainServiceResolver` (`Server/DomainServices/DomainServiceResolver.cs`) is the reference pattern for `daprClient.GetConfiguration()` usage in this codebase
- The `DaprRateLimitConfigSync` hosted service must be optional — graceful fallback to `appsettings.json` when DAPR sidecar is unavailable (critical for `WebApplicationFactory` tests)
- **Enforcement Rule #4:** Never add custom retry logic — DAPR resiliency only

### PartitionedRateLimiter Caching Behavior (Critical)

`PartitionedRateLimiter.Create<HttpContext, string>()` caches `SlidingWindowRateLimiter` instances per partition key (tenant ID). Once a limiter is created for a tenant, it persists until it becomes idle for one full window cycle. This means:

- **New tenants:** Get the latest `TenantPermitLimits` immediately (new partition created with current options)
- **Active tenants:** Keep their existing limiter until it expires from idle timeout (~60s), then a new one is created with updated options
- **Acceptable trade-off:** This eventual-consistency is by design. Do NOT attempt to invalidate the `PartitionedRateLimiter` or build a custom wrapper to force-recreate partitions. The complexity isn't justified for a ~60-second convergence window.

### Future Consideration: Per-Tenant Window Overrides

The current story only overrides `PermitLimit` per tenant. If a future requirement needs per-tenant `WindowSeconds` or `SegmentsPerWindow` overrides (e.g., premium tenants with different window shapes), the `TenantPermitLimits` dictionary would need to become `Dictionary<string, TenantRateLimitOverride>` with a record type. Do NOT implement this now — only `PermitLimit` per tenant is in scope.

### Existing Files to Modify

| File | Change |
|---|---|
| `src/Hexalith.EventStore.CommandApi/Configuration/RateLimitingOptions.cs` | Add `TenantPermitLimits`, update default to 1000, update validator |
| `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` | Update partition factory to resolve per-tenant limits; change `IOptions<T>` to `IOptionsMonitor<T>` |
| `src/Hexalith.EventStore.CommandApi/appsettings.json` | Add `TenantPermitLimits` section under `EventStore:RateLimiting` |

### New Files to Create

| File | Purpose |
|---|---|
| `src/Hexalith.EventStore.CommandApi/Configuration/DaprRateLimitConfigSync.cs` | `IHostedService` that reads DAPR config store for per-tenant rate limit overrides. Optional — graceful fallback when sidecar unavailable. Follow `DomainServiceResolver` pattern. |

### Existing Files — DO NOT MODIFY

| File | Reason |
|---|---|
| `src/Hexalith.EventStore.CommandApi/Program.cs` | Middleware pipeline is correct. Do NOT add new `app.UseMiddleware<>()` calls. |
| `src/Hexalith.EventStore.CommandApi/ErrorHandling/ProblemTypeUris.cs` | URI already defined |
| `src/Hexalith.EventStore.CommandApi/Middleware/CorrelationIdMiddleware.cs` | Unrelated |
| `tests/.../RateLimitingIntegrationTests.cs` | Existing test assertions must pass unchanged |

### Files That CAN Be Modified (Test Helpers)

| File | Allowed Change |
|---|---|
| `tests/.../Helpers/RateLimitingWebApplicationFactory.cs` | Add `TenantPermitLimits` configuration for new per-tenant override tests. Do NOT change the existing `PermitLimit=2` default used by existing tests. |

### Files That Do NOT Exist — Do NOT Create

| File | Why |
|---|---|
| `src/Hexalith.EventStore.CommandApi/Middleware/TenantRateLimitingMiddleware.cs` | The architecture doc mentions this filename, but rate limiting is implemented via `AddRateLimiter()` in `ServiceCollectionExtensions.cs`, NOT as custom middleware. Do NOT create this file. The existing `PartitionedRateLimiter` approach is correct. |

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
- Tier 1: Unit tests (no external deps) — `Contracts.Tests`, `Client.Tests`, `Sample.Tests`, `Testing.Tests`
- Tier 2: Integration tests (DAPR slim) — `Server.Tests`
- Tier 3: Aspire E2E — `IntegrationTests`
- Rate limiting tests live in Tier 3: `tests/Hexalith.EventStore.IntegrationTests/CommandApi/`
- Test factory: `tests/Hexalith.EventStore.IntegrationTests/Helpers/RateLimitingWebApplicationFactory.cs`

### Project Structure Notes

- Rate limiting is a CommandApi concern — all rate limiting code lives in `src/Hexalith.EventStore.CommandApi/`
- Configuration options: `Configuration/` subdirectory
- Service registration: `Extensions/ServiceCollectionExtensions.cs`
- Error handling: `ErrorHandling/` subdirectory
- Options binding pattern: `"EventStore:RateLimiting"` config section with `ValidateOnStart()`
- Server-side registration: `AddEventStoreServer(IConfiguration)` in `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`

### Previous Story Intelligence (Story 7.1)

- Story 7.1 was completed on 2026-03-18, all 1504 Server.Tests and 659 Tier 1 tests passed
- Established the three-tier configuration resolution pattern (DefaultInterval > DomainIntervals > TenantDomainIntervals)
- Key learning: per-tenant-domain keys use `{tenantId}:{domain}` format
- `SnapshotOptions` validator uses inline `.Validate()` method on the record, not separate `IValidateOptions<T>` class
- Files modified: `SnapshotOptions.cs`, `ISnapshotManager.cs`, `SnapshotManager.cs`, `AggregateActor.cs`, `FakeSnapshotManager.cs`, plus test files

### Git Intelligence

Recent commits (all 2026-03-18):
- `ff7a64c` Merge Story 7.1: Configurable Aggregate Snapshots
- `2933980` Merge Story 6.3: Health and Readiness Endpoints
- `54edca0` Merge Story 6.2: Structured Logging Verification
- `c870241` Merge Story 4.3: Per-Aggregate Backpressure (HTTP 429)

Story 4.3 (backpressure) is the closest pattern for 429 handling at the server layer. Rate limiting (this story) operates at the HTTP middleware layer, which is a separate concern.

### References

- [Source: _bmad-output/planning-artifacts/architecture.md — D8: Rate Limiting]
- [Source: _bmad-output/planning-artifacts/prd.md — NFR33, NFR20]
- [Source: _bmad-output/planning-artifacts/epics.md — Epic 7, Story 7.2]
- [Source: src/Hexalith.EventStore.CommandApi/Configuration/RateLimitingOptions.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs#L123-259]
- [Source: tests/Hexalith.EventStore.IntegrationTests/CommandApi/RateLimitingIntegrationTests.cs]
- [Source: _bmad-output/implementation-artifacts/7-1-configurable-aggregate-snapshots.md — Three-tier pattern]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### Change Log

### File List
