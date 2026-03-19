# Story 10.2: Redis Backplane for Multi-Instance SignalR

Status: done

## Story

As a platform developer,
I want a Redis backplane for SignalR message distribution,
so that push notifications work across multiple EventStore server replicas.

## Context: What Already Exists

This is a **validation and gap-analysis story**, not a greenfield story. The Redis backplane configuration, `AddStackExchangeRedis()` wiring, and env var fallback are already substantially implemented (Story 18-5). The work is to **audit every acceptance criterion against the existing code, fill DI wiring test gaps, and verify NFR38 latency compliance**.

**Why this audit matters:** If the Redis backplane doesn't correctly distribute SignalR messages across instances, clients connected to instance B never receive "changed" signals triggered on instance A. This breaks the fundamental promise of real-time push in any production deployment with >1 replica. FR56 specifically requires multi-instance distribution, and NFR38 requires delivery within 100ms at p99 — both must be verified, not assumed.

### Existing Redis Backplane Infrastructure

| Component | Location | Status |
|-----------|----------|--------|
| SignalROptions (BackplaneRedisConnectionString property) | `src/Hexalith.EventStore.CommandApi/SignalR/SignalROptions.cs:21` | Built |
| ValidateSignalROptions (null-or-non-empty validation) | `src/Hexalith.EventStore.CommandApi/SignalR/SignalROptions.cs:33-48` | Built |
| ConfigureBackplane (AddStackExchangeRedis call) | `src/Hexalith.EventStore.CommandApi/SignalR/SignalRServiceCollectionExtensions.cs:45-52` | Built |
| EVENTSTORE_SIGNALR_REDIS env var fallback | `src/Hexalith.EventStore.CommandApi/SignalR/SignalRServiceCollectionExtensions.cs:47` | Built |
| Microsoft.AspNetCore.SignalR.StackExchangeRedis package ref | `src/Hexalith.EventStore.CommandApi/Hexalith.EventStore.CommandApi.csproj:26` | Built |
| SignalROptionsValidationTests | `tests/Hexalith.EventStore.Server.Tests/SignalR/SignalROptionsValidationTests.cs` | Built (4 tests) |
| SignalRProjectionChangedBroadcasterTests (incl. p99 benchmark) | `tests/Hexalith.EventStore.Server.Tests/SignalR/SignalRProjectionChangedBroadcasterTests.cs` | Built (3 tests) |
| SignalR hub endpoint tests (enabled/disabled) | `tests/Hexalith.EventStore.Server.Tests/Integration/SignalRHubEndpointTests.cs` | Built (2 tests) |
| AppHost SignalR Enabled env var | `src/Hexalith.EventStore.AppHost/Program.cs:77` | Built |
| AppHost SignalR HubUrl env var | `src/Hexalith.EventStore.AppHost/Program.cs:84` | Built |

### What Is NOT Yet Wired

| Gap | Description |
|-----|-------------|
| DI wiring test: backplane activation | No test verifying `RedisHubLifetimeManager` is registered when `BackplaneRedisConnectionString` is set |
| DI wiring test: backplane absent | No test verifying `DefaultHubLifetimeManager` is used when no connection string is provided |
| AppHost Redis resource for backplane | No Redis container resource in AppHost — **out of scope** for this story (single-instance local dev doesn't need backplane) |
| Multi-instance integration test | No cross-process Redis broadcast test — **out of scope** (Tier 3 concern, requires Docker + Redis) |

## Acceptance Criteria

1. **Given** multiple EventStore server instances,
   **When** a projection change is broadcast,
   **Then** all connected clients across all instances receive the signal (FR56)
   **And** Redis is used as the backplane (a DAPR-managed Redis instance may be reused).
   **Note:** "All instances" means the SignalR `AddStackExchangeRedis()` backplane is configured so that `IHubContext.Clients.Group(...)` messages are distributed via Redis pub/sub to all server processes, not just the local one. The DAPR-managed Redis instance (used for state store and pub/sub) can be reused for the SignalR backplane connection.

2. **Given** SignalR signal delivery,
   **When** broadcaster dispatch time is measured,
   **Then** `BroadcastChangedAsync()` dispatch completes within 100ms at p99 (NFR38 — controllable portion).
   **Note:** The existing `SignalRProjectionChangedBroadcasterTests.BroadcastChangedAsync_Performance_CompletesWithinP99Budget` test measures broadcaster dispatch time against a mock hub context. True end-to-end latency (including Redis network RTT, WebSocket delivery, Blazor circuit rendering) is deployment-dependent and verified via observability (OpenTelemetry traces), not CI tests. This story verifies the controllable dispatch portion and documents the boundary.

## Definition of Done

Story is complete when:
- **Required:** Tasks 0-5 pass — all audit checks verified, gaps filled, tests green
- **Conditional:** Task 5 — run Tier 1+2 tests only if any `src/` or `tests/` files were modified during Tasks 0-4
- Both acceptance criteria verified against actual code (not epics wording)
- Build passes: `dotnet build Hexalith.EventStore.slnx --configuration Release`
- No regressions in existing Tier 1 or Tier 2 tests
- Audit results table produced in Completion Notes (AC # / Expected / Actual / Pass-Fail)
- Branch: `feat/story-10-2-redis-backplane-for-multi-instance-signalr` — create before making any code or test changes
- Scope boundary: up to 3 minor fixes (<1hr each) and missing tests are in scope; algorithmic changes or >3 gaps trigger a follow-up story

## Tasks / Subtasks

- [x] Task 0: Audit Redis Backplane Configuration Path (AC: #1)
  - [x] Create branch `feat/story-10-2-redis-backplane-for-multi-instance-signalr` before any code or test changes
  - [x] Verify `SignalROptions.BackplaneRedisConnectionString` in `CommandApi/SignalR/SignalROptions.cs`:
    - Property is `string?`, nullable, defaults to `null` (line 21)
    - XML doc mentions env var fallback `EVENTSTORE_SIGNALR_REDIS` (line 18-19)
  - [x] Verify `ValidateSignalROptions.Validate()` in `CommandApi/SignalR/SignalROptions.cs`:
    - BackplaneRedisConnectionString must be `null` or a non-whitespace string (lines 42-45)
    - Whitespace-only strings rejected with descriptive error message
  - [x] Verify `ConfigureBackplane()` in `CommandApi/SignalR/SignalRServiceCollectionExtensions.cs`:
    - Resolves connection string: `options.BackplaneRedisConnectionString ?? Environment.GetEnvironmentVariable("EVENTSTORE_SIGNALR_REDIS")` (lines 46-47)
    - If non-null/non-whitespace: calls `builder.AddStackExchangeRedis(...)` with `Action<RedisOptions>` overload (line 50)
    - If null/empty: backplane NOT configured (single-instance mode, no Redis dependency)
  - [x] Verify `AddEventStoreSignalR()` calls `ConfigureBackplane()` when Enabled=true (line 37)
  - [x] Verify `ConfigureBackplane()` is NOT called when Enabled=false (early return at line 33)
  - [x] Verify `Microsoft.AspNetCore.SignalR.StackExchangeRedis` package is referenced:
    - `Directory.Packages.props`: `<PackageVersion Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" Version="10.0.5" />` (line 47)
    - `CommandApi.csproj`: `<PackageReference Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" />` (line 26)
  - [x] Verify that `AddStackExchangeRedis()` registers `RedisHubLifetimeManager<THub>` as `HubLifetimeManager<THub>` — this is the mechanism that distributes hub messages via Redis pub/sub to all connected server instances
  - [x] **[ELICITATION #1 — AddStackExchangeRedis Idempotency]** Finding: `AddStackExchangeRedis()` uses `AddSingleton` (not `TryAdd`), so duplicate calls register multiple descriptors. DI resolves the last one — harmless, but produces extra Redis connections. Document as future hardening item.
  - [x] **[ELICITATION #2 — Connection String Format Validation]** Finding: Low-severity. Startup failure from invalid connection string is visible via health checks/crash logs. Not worth a gap slot. Document as future hardening item.
  - [x] **[ELICITATION #3 — `abortConnect=false` for Fail-Open Startup] (HIGH)** Finding: Confirmed — `StackExchange.Redis` defaults `abortConnect=true`. **Code change applied** in Task 4: switched `ConfigureBackplane` to `Action<RedisOptions>` overload with `AbortOnConnectFail = false`. Server now degrades gracefully when Redis is unreachable at startup.

- [x] Task 1: Confirm AppHost Backplane Wiring Is Out of Scope (AC: #1)
  - [x] Verify `src/Hexalith.EventStore.AppHost/Program.cs` does NOT wire a Redis resource for SignalR backplane
  - [x] **This is correct and intentional:** Local Aspire dev uses in-memory DAPR components (`state.in-memory` in `HexalithEventStoreExtensions.cs:38`). Single-instance local dev does NOT need a Redis backplane. Adding a Redis container resource just for SignalR backplane would add Docker requirements to every local dev session for no benefit.
  - [x] **Document the production configuration path** in completion notes: set `EVENTSTORE_SIGNALR_REDIS` env var or `EventStore__SignalR__BackplaneRedisConnectionString` config to the Redis connection string for multi-instance deployments
  - [x] **Verify PM-9 mitigation:** Confirmed — `BackplaneRedisConnectionString` config is independent from DAPR's Redis. Env var `EVENTSTORE_SIGNALR_REDIS` is also independent. Production can use separate Redis for backplane to avoid contention.
  - [x] **[ELICITATION #4 — Redis Channel Prefix for Multi-Deployment Isolation]** Finding: Default channel prefix is based on hub type name (`ProjectionChangedHub`). If staging and production share Redis, cross-environment leakage would occur. **Decision:** Separate Redis instances per environment is the assumed isolation model. Adding `ChannelPrefix` config would require new `SignalROptions` property — scope creep. Documented as known limitation for follow-up.
  - [x] No AppHost changes in this story — if multi-instance local testing is needed, that's a separate story

- [x] Task 2: Audit Existing Test Coverage for Redis Backplane (AC: #1, #2)
  - [x] Verify `SignalROptionsValidationTests` covers (4 tests):
    - DefaultValues_AreCorrect: BackplaneRedisConnectionString is null by default — covered
    - Validation_WithPositiveGroupLimit_Succeeds — covered
    - Validation_WithNonPositiveGroupLimit_Fails — covered
    - Validation_WithWhitespaceBackplaneConnectionString_Fails — covered
  - [x] Verify `SignalRProjectionChangedBroadcasterTests` covers (3 tests):
    - BroadcastChangedAsync_ValidInput_ForwardsToGroupClient — covered
    - BroadcastChangedAsync_ClientFailure_DoesNotThrow — covered (fail-open)
    - BroadcastChangedAsync_P99Dispatch_RemainsUnder100Milliseconds — covered (NFR38 dispatch budget)
  - [x] Check for GAP: confirmed — no test for `ConfigureBackplane()` activating Redis. Filled in Task 4.
  - [x] Check for GAP: confirmed — no test for backplane NOT activated when no connection string. Filled in Task 4.
  - [x] Check for GAP: env var fallback test — not worth gap slot. Code inspection confirms `?? Environment.GetEnvironmentVariable("EVENTSTORE_SIGNALR_REDIS")` at line 47. Env var mutation in tests is fragile.
  - [x] **Multi-instance integration test is OUT OF SCOPE:** Documented as future Tier 3 test case.

- [x] Task 3: Audit NFR38 Latency Compliance (AC: #2)
  - [x] Verify existing p99 benchmark test in `SignalRProjectionChangedBroadcasterTests`:
    - Measures time for `BroadcastChangedAsync()` against mock `IHubContext`
    - Verifies dispatch completes within 100ms at p99
    - Note: this measures **local dispatch**, not end-to-end including Redis network hop
  - [x] Document latency boundary:
    - **In-scope (testable in CI):** Broadcaster dispatch time (projectionType/tenantId → `hubContext.Clients.Group(...).ProjectionChanged(...)`)
    - **Out-of-scope (deployment-dependent):** Redis network RTT, SignalR WebSocket delivery to client, Blazor circuit rendering
    - The existing test validates the controllable portion. True end-to-end latency depends on infrastructure and is verified via observability (OpenTelemetry traces, NFR38 monitoring)
  - [x] Verify that PM-9 (Redis contention) mitigation is documented: production deployments MAY use a dedicated Redis instance for SignalR backplane via separate connection string
  - [x] **[ELICITATION #5 — RedisHubLifetimeManager Failure Mode] (HIGH)** Finding: `RedisHubLifetimeManager.SendGroupAsync()` throws `RedisConnectionException` when Redis is unreachable at runtime. The broadcaster's existing try/catch in `BroadcastChangedAsync()` (lines 20-30) catches all exceptions — **fail-open holds**. The existing test `BroadcastChangedAsync_ClientFailure_DoesNotThrow` covers this path. Confirmed: no silent message drops.

- [x] Task 4: Fill any gaps identified during audit (bounded: up to 3 minor gaps, <1hr each)
  - [x] Priority 1: Added `AddEventStoreSignalR_WithRedisConnectionString_RegistersRedisHubLifetimeManager` test — uses direct `ServiceCollection` with in-memory config. Asserts `RedisHubLifetimeManager` is registered via service descriptor inspection. Approach avoids `WebApplicationFactory` timing issues.
  - [x] Priority 2: Added `AddEventStoreSignalR_WithoutRedisConnectionString_UsesDefaultHubLifetimeManager` test — verifies `DefaultHubLifetimeManager` is the last registered `HubLifetimeManager<>` when no connection string is set. **[ELICITATION #7]** Both types are public — used name-based assertion (`FullName.ShouldContain(...)`) for clarity.
  - [x] Priority 3: Code fix for Elicitation #3 — switched `ConfigureBackplane` from `builder.AddStackExchangeRedis(redis)` (string overload) to `builder.AddStackExchangeRedis(o => { o.Configuration = ConfigurationOptions.Parse(redis); o.Configuration.AbortOnConnectFail = false; })`. Server now degrades gracefully when Redis is unavailable at startup.
  - [x] No more than 3 gaps — 3 gaps filled (2 tests + 1 code fix), within budget
  - [x] Build passes: `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
  - [x] Full Tier 1+2 tests pass (src and test files modified)

- [x] Task 5: Run Full Test Suite (conditional)
  - [x] Source and test files modified — full Tier 1+2 run required
    - Tier 1: Contracts 267 + Client 297 + Sample 47 + Testing 67 + SignalR 20 = **698 passed**
    - Tier 2: Server **1533 passed** (up from 1531 at Story 10-1 — +2 new backplane wiring tests)
  - [x] All tests pass, no regressions
  - [x] Actual test counts reported: Tier 1 = 698, Tier 2 = 1533 (total 2231)

## Dev Notes

### Architecture: Redis Backplane for Multi-Instance SignalR (ADR-18.5b)

**Choice:** Use `Microsoft.AspNetCore.SignalR.StackExchangeRedis` (`AddStackExchangeRedis()`) for multi-instance message distribution.

**How it works:**
- `AddStackExchangeRedis(connectionString)` replaces the default `DefaultHubLifetimeManager<THub>` with `RedisHubLifetimeManager<THub>`
- `RedisHubLifetimeManager` uses Redis pub/sub channels to distribute group messages across all connected server processes
- When instance A calls `hubContext.Clients.Group("counter:acme").ProjectionChanged(...)`, Redis pub/sub delivers the message to all instances, and each instance delivers to its locally connected clients in that group

**Rejected alternatives (ADR-18.5b):**
- Custom `IHubLifetimeManager<T>` via DAPR pub/sub — ~500+ LOC, no official support, high maintenance
- Azure SignalR Service — not all deployments are on Azure

**Configuration path:**
```
EventStore:SignalR:Enabled = true
EventStore:SignalR:BackplaneRedisConnectionString = "redis-host:6379"
   OR
EVENTSTORE_SIGNALR_REDIS=redis-host:6379
```

**Single-instance fallback:** When neither config value nor env var is set, backplane is not activated. `DefaultHubLifetimeManager` handles local-only group messaging. This is correct for single-instance deployments.

### Key Code Paths to Audit

**Backplane activation (AC #1):**
```
Program.cs → AddEventStoreSignalR(configuration)
  → SignalRServiceCollectionExtensions.AddEventStoreSignalR():
    → services.AddSignalR() → returns ISignalRServerBuilder
    → if Enabled:
      → ConfigureBackplane(signalRBuilder, options):
        → redis = options.BackplaneRedisConnectionString ?? env("EVENTSTORE_SIGNALR_REDIS")
        → if (!string.IsNullOrWhiteSpace(redis)):
          → builder.AddStackExchangeRedis(redis)
            → registers RedisHubLifetimeManager<THub>
            → replaces DefaultHubLifetimeManager<THub>
      → register SignalRProjectionChangedBroadcaster (overrides NoOp)
```

**Broadcast flow through Redis backplane (AC #1):**
```
ETag regeneration → BroadcastChangedAsync(projectionType, tenantId)
  → SignalRProjectionChangedBroadcaster:
    → hubContext.Clients.Group("{projType}:{tenantId}").ProjectionChanged(...)
      → RedisHubLifetimeManager (when backplane active):
        → Publish message to Redis pub/sub channel
        → All instances subscribed to channel receive the message
        → Each instance delivers to locally connected clients in the group
      → DefaultHubLifetimeManager (when no backplane):
        → Deliver to locally connected clients only
```

### Advanced Elicitations (Party Mode Review)

Seven elicitations were identified during party-mode review. Two are **high severity**:

| # | Finding | Severity | Action |
|---|---------|----------|--------|
| 1 | `AddStackExchangeRedis` idempotency on double-call | Low | Audit in Task 0, document finding |
| 2 | Connection string format not validated (parse-level) | Medium | Audit in Task 0, evaluate as gap slot |
| 3 | `abortConnect=true` default crashes server if Redis is down at startup — contradicts ADR-18.5f fail-open | **High** | Audit in Task 0, likely requires code change (gap slot in Task 4) |
| 4 | No Redis channel prefix — cross-deployment signal leakage if shared Redis | Medium | Audit in Task 1, evaluate combined with #3 |
| 5 | `RedisHubLifetimeManager` may silently drop messages when Redis is down (no exception for fail-open catch) | **High** | Audit in Task 3, verify source code behavior |
| 6 | Test count baseline may have drifted since Story 10-1 | Low | Report actual counts in Task 5 |
| 7 | `DefaultHubLifetimeManager<T>` may be `internal` — affects type assertion approach | Medium | Verify accessibility before writing tests in Task 4 |

**Impact on scope:** Elicitation #3 (`abortConnect=false`) is likely a 1-line code change in `ConfigureBackplane` (switch to `Action<RedisOptions>` overload). This fits within the 3-gap budget. If combined with #4 (channel prefix), it's still one method change. Elicitation #5 is audit-only (verify Microsoft source behavior, document finding).

### Pre-Mortem Risks (from Story 18-5)

**PM-3: Redis Backplane Requires Sticky Sessions or WebSocket-Only**
- The Redis backplane requires sticky sessions (session affinity at the load balancer) unless all clients use WebSockets with `SkipNegotiation = true`
- Blazor Server uses WebSockets by default — not an issue for the primary use case
- Blazor WebAssembly or browser JS clients may need sticky sessions — document this

**PM-9: Redis Backplane + DAPR Redis Contention**
- If SignalR backplane uses the same Redis instance as DAPR state store/pubsub, high SignalR traffic could affect DAPR performance
- **Mitigation:** Production deployments MAY use a dedicated Redis instance via separate `BackplaneRedisConnectionString` config
- The env var `EVENTSTORE_SIGNALR_REDIS` allows pointing at a separate Redis independent of DAPR's Redis

**PM-12: Reconnect Storm on Redis Backplane Outage**
- If Redis goes down momentarily and 10K clients reconnect: `WithAutomaticReconnect()` default policy (0, 2, 10, 30s) provides natural jitter. `OnReconnectedAsync` iterates groups sequentially. `MaximumParallelInvocationsPerClient = 1` throttles per-connection.

### Scope Boundaries with Stories 10-1 and 10-3

- **Story 10-1 (SignalR Hub & Broadcasting):** Completed. Verified hub mapping, broadcast chain, signal-only model, fail-open behavior. Story 10-1 explicitly noted: "Do NOT audit Redis backplane *functional behavior* — that is Story 10-2's scope."
- **Story 10-3 (Auto-Rejoin on Reconnection):** Do NOT verify/modify `EventStoreSignalRClient` reconnection behavior — that is Story 10-3's scope. Only audit backplane message distribution.

### AppHost Wiring Considerations

The current Aspire AppHost uses in-memory DAPR components for local dev (`state.in-memory` in `HexalithEventStoreExtensions.cs:38`). The DaprComponents YAML files in AppHost reference Redis (`redisHost`, `redisPassword`) but these are for production DAPR deployments.

For SignalR backplane in local dev:
- **Single-instance (default):** No backplane needed. Works with `DefaultHubLifetimeManager`.
- **Multi-instance testing:** Requires a Redis container. Options:
  - `builder.AddRedis("signalr-redis")` in AppHost, pass connection string via `EventStore__SignalR__BackplaneRedisConnectionString`
  - Or set `EVENTSTORE_SIGNALR_REDIS` env var manually
- **Production:** Set `EventStore:SignalR:BackplaneRedisConnectionString` to the DAPR-managed Redis (or a dedicated Redis) connection string

### Testing Pattern

- **xUnit** with **Shouldly** assertions (Tier 1), **NSubstitute** for mocking (Tier 2)
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`
- Integration tests use `WebApplicationFactory<CommandApiProgram>` with `extern alias commandapi`
- `SignalRHubWebApplicationFactory` sets `EventStore:SignalR:Enabled = true` via in-memory config
- For backplane DI wiring tests, extend with `EventStore:SignalR:BackplaneRedisConnectionString` config pointing at a dummy address (e.g., `localhost:9999`). The test goal is to verify DI registration (`RedisHubLifetimeManager` vs `DefaultHubLifetimeManager`), NOT Redis connectivity. If `AddStackExchangeRedis()` connects eagerly at startup and throws, catch the Redis connection exception — the exception itself proves the backplane was activated. If it connects lazily, resolve and assert the `IHubLifetimeManager<>` type directly.

### CRITICAL: Test Factory Pattern

All CommandApi integration tests use the `extern alias commandapi` pattern because `CommandApiProgram` is in a `<Project Sdk="Microsoft.NET.Sdk.Web">` project. The test project references it via:
```xml
<ProjectReference Include="..\..\src\Hexalith.EventStore.CommandApi\Hexalith.EventStore.CommandApi.csproj">
  <Aliases>commandapi</Aliases>
</ProjectReference>
```

Test files must begin with:
```csharp
extern alias commandapi;
```

And reference the program class as:
```csharp
using CommandApiProgram = commandapi::Program;
```

### Previous Story (10-1) Intelligence

Story 10-1 was an audit story for SignalR hub and broadcasting. Key learnings:
- **Audit-only stories work well:** All ACs verified against code with 3 test gaps filled
- **Test counts at Story 10-1 completion:** Tier 1: 698 passed, Tier 2: 1531 passed
- **3 gaps filled in 10-1:** disabled-state end-to-end test, broadcast chain call-order verification, conditional hub mapping disabled-state test
- **Pre-existing test failure to ignore:** `ErrorReferenceEndpointTests.AllProblemTypeUris_HaveCorrespondingErrorModel` — ignore if still failing
- **Branch naming convention:** `feat/story-10-2-redis-backplane-for-multi-instance-signalr`
- **Commit message pattern:** `feat: <description of changes for Story 10-2>`

### Git Intelligence (Recent Commits)

Last 5 commits: Story 10-1 (SignalR hub audit + test gaps), Story 9-5 (IQueryResponse audit), Story 9-4 (query actor cache). All followed the audit/gap-fill pattern with feature branches merged via PRs.

### Package Version Reference

From `Directory.Packages.props`:
- `Microsoft.AspNetCore.SignalR.StackExchangeRedis` Version `10.0.5`
- `Microsoft.AspNetCore.SignalR.Client` Version `10.0.5`
- These match the .NET 10 SDK version (`10.0.103` from `global.json`)

### Project Structure Notes

- Redis backplane config in `Hexalith.EventStore.CommandApi/SignalR/` — same package as the hub (host-level concern)
- `AddStackExchangeRedis()` is called on `ISignalRServerBuilder` returned by `services.AddSignalR()`
- AppHost in `Hexalith.EventStore.AppHost/Program.cs` — orchestration layer
- Aspire extensions in `Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` — topology builder

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.2: Redis Backplane for Multi-Instance SignalR]
- [Source: _bmad-output/planning-artifacts/epics.md#FR56]
- [Source: _bmad-output/planning-artifacts/epics.md#NFR38]
- [Source: _bmad-output/implementation-artifacts/18-5-signalr-real-time-notifications.md#ADR-18.5b]
- [Source: _bmad-output/implementation-artifacts/18-5-signalr-real-time-notifications.md#PM-3, PM-9, PM-12]
- [Source: _bmad-output/implementation-artifacts/10-1-signalr-hub-and-projection-change-broadcasting.md]
- [Source: src/Hexalith.EventStore.CommandApi/SignalR/SignalROptions.cs]
- [Source: src/Hexalith.EventStore.CommandApi/SignalR/SignalRServiceCollectionExtensions.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Hexalith.EventStore.CommandApi.csproj]
- [Source: src/Hexalith.EventStore.AppHost/Program.cs]
- [Source: src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/SignalR/SignalROptionsValidationTests.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/SignalR/SignalRProjectionChangedBroadcasterTests.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Integration/SignalRHubEndpointTests.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Integration/SignalRHubWebApplicationFactory.cs]
- [Source: Directory.Packages.props — line 47: Microsoft.AspNetCore.SignalR.StackExchangeRedis 10.0.5]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

None required — audit story with straightforward gap fills.

### Completion Notes List

**Audit Results Table:**

| AC # | Expected | Actual | Pass/Fail |
|------|----------|--------|-----------|
| AC #1 (FR56) | Redis backplane configured via `AddStackExchangeRedis()` for multi-instance distribution | `ConfigureBackplane()` correctly calls `AddStackExchangeRedis()` when connection string provided, registers `RedisHubLifetimeManager<THub>`. Now uses `Action<RedisOptions>` overload with `AbortOnConnectFail = false` for fail-open. | **PASS** |
| AC #2 (NFR38) | Dispatch completes within 100ms at p99 | Existing `BroadcastChangedAsync_P99Dispatch_RemainsUnder100Milliseconds` test validates controllable dispatch portion. Latency boundary documented. | **PASS** |

**Gaps Filled (3/3 budget):**
1. Test: `AddEventStoreSignalR_WithRedisConnectionString_RegistersRedisHubLifetimeManager` — verifies `RedisHubLifetimeManager` DI registration when connection string is provided
2. Test: `AddEventStoreSignalR_WithoutRedisConnectionString_UsesDefaultHubLifetimeManager` — verifies `DefaultHubLifetimeManager` is used in single-instance mode
3. Code fix: `ConfigureBackplane` switched from `AddStackExchangeRedis(string)` to `AddStackExchangeRedis(Action<RedisOptions>)` with `AbortOnConnectFail = false` — server no longer crashes when Redis is unavailable at startup

**Elicitation Findings:**
- #1 (Idempotency): Safe — last registration wins via DI. Future hardening item.
- #2 (Connection string format): Low-severity — startup failure visible via health checks. Future hardening item.
- #3 (abortConnect): **Fixed** — code change applied. Gap slot used.
- #4 (Channel prefix): Known limitation — separate Redis per environment is the assumed model. Future hardening if shared Redis needed.
- #5 (Failure mode): **Confirmed** — `RedisHubLifetimeManager` throws on publish failure. Broadcaster's try/catch catches it. Fail-open holds.
- #7 (Type accessibility): Both `RedisHubLifetimeManager<T>` and `DefaultHubLifetimeManager<T>` are public. Name-based assertion used for clarity.

**Production Configuration Path:**
- Set `EventStore:SignalR:BackplaneRedisConnectionString` in config or `EVENTSTORE_SIGNALR_REDIS` env var
- Can point at DAPR's Redis or a dedicated instance (PM-9 contention mitigation)

**Test Counts:**
- Tier 1: 698 passed (unchanged from Story 10-1)
- Tier 2: 1533 passed (+2 new backplane wiring tests from 1531)
- Total: 2231 passed, 0 failed

### Change Log

- 2026-03-20: Story 10-2 audit completed. 3 gaps filled: 2 DI wiring tests + `abortConnect=false` code fix. All Tier 1+2 tests pass.
- 2026-03-20: BMAD code review completed (clean). Follow-up: backplane wiring tests now resolve `HubLifetimeManager<ProjectionChangedHub>` from DI instead of descriptor scanning. Status → **done**.

### File List

- `src/Hexalith.EventStore.CommandApi/SignalR/SignalRServiceCollectionExtensions.cs` (modified — `ConfigureBackplane` switched to `Action<RedisOptions>` overload with `AbortOnConnectFail = false`)
- `tests/Hexalith.EventStore.Server.Tests/Integration/SignalRBackplaneWiringTests.cs` (new — 2 DI wiring tests for backplane activation/deactivation)
