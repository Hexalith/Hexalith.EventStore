# Story 10.1: SignalR Hub & Projection Change Broadcasting

Status: done

## Story

As a platform developer,
I want a SignalR hub inside EventStore that broadcasts "changed" signals when projections update,
so that connected clients receive real-time push notifications without polling.

## Context: What Already Exists

This is a **validation and gap-analysis story**, not a greenfield story. The SignalR hub, broadcaster, client helper, DI registration, and integration with the ETag regeneration flow are already substantially implemented. The work is to **audit every acceptance criterion against the existing code, fill any gaps, and ensure full test coverage**.

**Why this audit matters:** If the SignalR hub doesn't correctly broadcast "changed" signals to the right groups, clients either (a) never receive notifications (silent failure — they keep polling or show stale data), or (b) receive notifications for wrong projection types (cross-tenant data leakage or spurious refreshes). FR55 exists specifically to enable real-time push without polling, and the signal-only model (no data in the broadcast) is a deliberate security/performance choice — sending data would create a second cache invalidation surface and risk leaking projection state cross-tenant.

### Existing SignalR Infrastructure

| Component | Location | Status |
|-----------|----------|--------|
| ProjectionChangedHub (SignalR Hub, FR55/FR56) | `src/Hexalith.EventStore.CommandApi/SignalR/ProjectionChangedHub.cs` | Built |
| IProjectionChangedClient (strongly-typed hub interface) | `src/Hexalith.EventStore.CommandApi/SignalR/IProjectionChangedClient.cs` | Built |
| SignalRProjectionChangedBroadcaster (IProjectionChangedBroadcaster impl) | `src/Hexalith.EventStore.CommandApi/SignalR/SignalRProjectionChangedBroadcaster.cs` | Built |
| SignalROptions (Enabled, BackplaneRedisConnectionString, MaxGroupsPerConnection) | `src/Hexalith.EventStore.CommandApi/SignalR/SignalROptions.cs` | Built |
| SignalRServiceCollectionExtensions (AddEventStoreSignalR DI) | `src/Hexalith.EventStore.CommandApi/SignalR/SignalRServiceCollectionExtensions.cs` | Built |
| NoOpProjectionChangedBroadcaster (default when SignalR disabled) | `src/Hexalith.EventStore.Server/Projections/NoOpProjectionChangedBroadcaster.cs` | Built |
| IProjectionChangedBroadcaster (interface in Client) | `src/Hexalith.EventStore.Client/Projections/IProjectionChangedBroadcaster.cs` | Built |
| DaprProjectionChangeNotifier (calls broadcaster after ETag regen) | `src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs` | Built |
| ProjectionNotificationController (pub/sub receiver, calls broadcaster) | `src/Hexalith.EventStore.CommandApi/Controllers/ProjectionNotificationController.cs` | Built |
| ETagActor (regenerates ETags on projection change) | `src/Hexalith.EventStore.Server/Actors/ETagActor.cs` | Built |
| EventStoreSignalRClient (client helper with auto-reconnect) | `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs` | Built |
| EventStoreSignalRClientOptions (HubUrl, AccessTokenProvider) | `src/Hexalith.EventStore.SignalR/EventStoreSignalRClientOptions.cs` | Built |
| Aspire AppHost wiring (SignalR enabled, hub URL env var) | `src/Hexalith.EventStore.AppHost/Program.cs` | Built |
| Blazor UI SignalR integration | `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs` | Built |

### Existing Test Coverage

| Test File | Tier | Tests | Note |
|-----------|------|-------|------|
| `SignalR.Tests/EventStoreSignalRClientTests.cs` | 1 | Client-side tests (subscribe, unsubscribe, reconnect) | Report exact count in audit |
| `Server.Tests/SignalR/ProjectionChangedHubTests.cs` | 2 | Hub JoinGroup/LeaveGroup, resource guards | Report exact count in audit |
| `Server.Tests/SignalR/SignalRProjectionChangedBroadcasterTests.cs` | 2 | Broadcaster sends to correct group, fail-open | Report exact count in audit |
| `Server.Tests/Integration/SignalRHubEndpointTests.cs` | 2 | Hub endpoint accessibility via WebApplicationFactory | Report exact count in audit |
| `Server.Tests/Actors/ETagActorTests.cs` | 2 | ETag regeneration, self-routing format, migration | Report exact count in audit |

## Acceptance Criteria

1. **Given** a SignalR hub hosted inside the EventStore server,
   **When** a projection's ETag is regenerated,
   **Then** a signal-only "changed" message is broadcast to connected clients (FR55)
   **And** clients are grouped by ETag actor ID (`{ProjectionType}:{TenantId}`)
   **And** the signal contains no projection data — clients re-query on receipt.
   **Note:** "Signal-only" means the `ProjectionChanged(projectionType, tenantId)` method sends only the type and tenant identifiers. No projection state, no ETag value, no entity-level data. Clients use these identifiers to decide which query to re-execute.

2. **Given** the SignalR hub endpoint,
   **When** the CommandApi starts,
   **Then** the hub is conditionally mapped at `/hubs/projection-changes` (FR56).
   **Note:** "Conditionally" means the hub is only mapped when `EventStore:SignalR:Enabled` is `true`. When disabled, `NoOpProjectionChangedBroadcaster` is used and the hub endpoint does not exist.

## Definition of Done

Story is complete when:
- **Required:** Tasks 0-5 pass — all audit checks verified, gaps filled, tests green
- **Conditional:** Task 5 — run Tier 1+2 tests only if any `src/` or `tests/` files were modified during Tasks 0-4
- All two acceptance criteria verified against actual code (not epics wording)
- Build passes: `dotnet build Hexalith.EventStore.slnx --configuration Release`
- No regressions in existing Tier 1 or Tier 2 tests
- Audit results table produced in Completion Notes (AC # / Expected / Actual / Pass-Fail) — AC #1 MUST have sub-rows for both Direct and PubSub broadcast paths (both paths must be verified independently)
- Branch: `feat/story-10-1-signalr-hub-and-projection-change-broadcasting` — create before making any code or test changes
- Scope boundary: up to 3 minor fixes (<1hr each) and missing tests are in scope; algorithmic changes or >3 gaps trigger a follow-up story

## Tasks / Subtasks

- [x] Task 0: Audit SignalR Hub & Broadcasting Chain (AC: #1)
  - [x] Create branch `feat/story-10-1-signalr-hub-and-projection-change-broadcasting` before any code or test changes
  - [x] Verify `ProjectionChangedHub` in `CommandApi/SignalR/ProjectionChangedHub.cs`:
    - Inherits `Hub<IProjectionChangedClient>` (strongly-typed hub)
    - Exposes `JoinGroup(projectionType, tenantId)` and `LeaveGroup(projectionType, tenantId)` methods
    - Group name format: `"{projectionType}:{tenantId}"` (colon-separated, matches ETag actor ID format)
    - Has `HubPath` constant = `"/hubs/projection-changes"`
  - [x] Verify `IProjectionChangedClient` in `CommandApi/SignalR/IProjectionChangedClient.cs`:
    - Defines `Task ProjectionChanged(string projectionType, string tenantId)` — signal-only, no data payload
  - [x] Verify `SignalRProjectionChangedBroadcaster` in `CommandApi/SignalR/SignalRProjectionChangedBroadcaster.cs`:
    - Implements `IProjectionChangedBroadcaster`
    - `BroadcastChangedAsync(projectionType, tenantId, cancellationToken)` calls `hubContext.Clients.Group(groupName).ProjectionChanged(projectionType, tenantId)`
    - Fail-open (ADR-18.5a): catches all exceptions, logs warning, does NOT throw
  - [x] Verify the broadcast is triggered by ETag regeneration with **sequential await ordering** — trace the flow:
    1. `DaprProjectionChangeNotifier.NotifyProjectionChangedAsync()`: verify `await RegenerateAsync()` completes *then* `await BroadcastChangedAsync()` fires — sequential, NOT parallel, NOT fire-and-forget-before-regen. The ETag must be current before clients are told to re-query.
    2. `ProjectionNotificationController.OnProjectionChanged()`: same sequential ordering — `await RegenerateAsync()` then `await BroadcastChangedAsync()`
  - [x] Verify signal carries NO projection data — only `projectionType` and `tenantId` string parameters
  - [x] Verify group name matches ETag actor ID format (`{ProjectionType}:{TenantId}`) so clients subscribe to the same grouping used by the caching layer
  - [x] **[ELICITATION #1 — Double-Broadcast Risk]** Verify PubSub transport mode does NOT double-broadcast:
    - CONFIRMED: In PubSub mode, `DaprProjectionChangeNotifier` publishes to DAPR pub/sub and returns immediately (line 49). It does NOT call `BroadcastChangedAsync()` directly. Only the `ProjectionNotificationController` broadcasts after regen. No double-broadcast risk.
  - [x] **[ELICITATION #4 — RegenerateAsync Failure Handling]** Verify error handling when `RegenerateAsync()` throws:
    - **Direct mode:** Sequential `await regen; await broadcast;` without try/catch around regen. If regen throws, broadcast is skipped. This is acceptable — the in-process caller receives the exception and can retry.
    - **PubSub mode:** `RegenerateAsync()` is inside try/catch in `ProjectionNotificationController` (lines 50-73). If regen throws, catch on line 69 returns 500 triggering DAPR retry. Broadcast only fires after successful regen. Pattern (b) is used.

- [x] Task 1: Audit Hub Resource Guards & Security (AC: #1, related)
  - [x] Verify `MaxGroupsPerConnection` limit enforced in `ProjectionChangedHub.JoinGroup()`:
    - Default: 50 groups per connection (from `SignalROptions`)
    - Exceeding limit throws `HubException` (lines 48-51)
  - [x] Verify input validation in `JoinGroup`:
    - `projectionType` and `tenantId` must not contain colons — throws `HubException` (lines 39-41)
    - Both must be non-empty — `ArgumentException.ThrowIfNullOrWhiteSpace` (lines 35-36)
  - [x] Verify connection lifecycle tracking:
    - `OnConnectedAsync()` logs connection with EventId 1082 (lines 95-98)
    - `OnDisconnectedAsync()` cleans up `_connectionGroups` via `TryRemove` (lines 101-106)
  - [x] Verify rollback on `AddToGroupAsync` failure (transactional semantics — `addedToTracking` flag tracks state, rollback in catch block removes from HashSet, lines 56-71)
  - [x] Verify structured logging with EventIds (1080-1083 range) for join/leave/connect/disconnect

- [x] Task 2: Audit Conditional Hub Mapping (AC: #2)
  - [x] Verify `SignalRServiceCollectionExtensions.AddEventStoreSignalR()` in `CommandApi/SignalR/SignalRServiceCollectionExtensions.cs`:
    - Binds config from section `"EventStore:SignalR"` (line 23)
    - Calls `services.AddSignalR()` unconditionally (line 29)
    - When `Enabled == true`: registers `SignalRProjectionChangedBroadcaster` via `AddSingleton` (line 40, overrides TryAdd)
    - When `Enabled == false`: returns early (line 33), `NoOpProjectionChangedBroadcaster` remains
  - [x] Verify `SignalROptions` in `CommandApi/SignalR/SignalROptions.cs`:
    - `Enabled` defaults to `false` (default bool value)
    - `BackplaneRedisConnectionString` nullable (line 21), env var fallback `EVENTSTORE_SIGNALR_REDIS` in `ConfigureBackplane` (line 47)
    - `MaxGroupsPerConnection` default: 50 (line 27)
    - `ValidateSignalROptions` validates MaxGroupsPerConnection > 0 (line 38) and Redis string non-empty if set (lines 42-45)
  - [x] Verify `Program.cs` in CommandApi:
    - Calls `builder.Services.AddEventStoreSignalR(builder.Configuration)` (line 19)
    - Conditionally maps hub: `if (signalROptions?.Enabled == true) app.MapHub<ProjectionChangedHub>(ProjectionChangedHub.HubPath)` (lines 51-53)
    - Hub path = `/hubs/projection-changes` via constant
  - [x] Verify AppHost in `AppHost/Program.cs`:
    - Sets `EventStore__SignalR__Enabled` to `"true"` (line 77)
    - Passes hub URL to Blazor UI via `EventStore__SignalR__HubUrl` environment variable (line 84)

- [x] Task 3: Audit NoOp/Enabled Toggle Behavior (AC: #2, related)
  - [x] Verify `NoOpProjectionChangedBroadcaster` in `Server/Projections/NoOpProjectionChangedBroadcaster.cs`:
    - Implements `IProjectionChangedBroadcaster` (line 9)
    - Returns `Task.CompletedTask` immediately (line 15)
  - [x] Verify DI registration order:
    1. `AddEventStoreServer()` registers `NoOpProjectionChangedBroadcaster` via `TryAddSingleton` (ServiceCollectionExtensions.cs:42)
    2. `AddEventStoreSignalR()` (when Enabled) replaces with `AddSingleton<SignalRProjectionChangedBroadcaster>` (line 40)
    - Toggle works correctly: disabled = NoOp, enabled = SignalR
  - [x] Verify `DaprProjectionChangeNotifier` broadcast behavior per transport mode:
    - **Direct mode:** calls `RegenerateAsync()` then `BroadcastChangedAsync()` sequentially (lines 52-67)
    - **PubSub mode:** publishes to pub/sub ONLY, returns immediately (lines 42-50). Does NOT call `BroadcastChangedAsync()` directly. No double-broadcast.
  - [x] Verify `ProjectionNotificationController` calls `IProjectionChangedBroadcaster` after ETag regeneration (lines 55-65)

- [x] Task 4: Validate test coverage completeness
  - [x] Run SignalR client tests: `dotnet test tests/Hexalith.EventStore.SignalR.Tests/` — 20 passed
  - [x] Run SignalR hub/broadcaster tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~SignalR"` — passed
  - [x] Run ETag actor tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~ETagActor"` — passed
  - [x] Verify `ProjectionChangedHubTests` covers (5 tests):
    - JoinGroup adds connection to correct group name `{projectionType}:{tenantId}`
    - LeaveGroup removes connection from group
    - MaxGroupsPerConnection limit enforcement
    - Colon-in-input rejection
    - AddToGroupAsync failure rollback (quota not consumed)
  - [x] Verify `SignalRProjectionChangedBroadcasterTests` covers (3 tests):
    - Sends to correct group `{projectionType}:{tenantId}`
    - Fail-open: exception during broadcast does NOT propagate
    - P99 dispatch remains under 100ms
  - [x] Verify `SignalRHubEndpointTests` covers (1 test, gap found for disabled state):
    - Hub endpoint `/hubs/projection-changes` accessible when enabled — covered
    - Hub endpoint NOT accessible when disabled — GAP (filled in Task 5)
  - [x] Verify `EventStoreSignalRClientTests` covers (19 tests):
    - Subscribe registers callback — covered
    - Unsubscribe removes callback — covered
    - Auto-reconnect rejoins all tracked groups (FR59) — 6 tests covering reconnection, callback preservation, unsubscribe-before-reconnect
    - HubUrl validation (absolute URL required) — covered
    - ProjectionType/TenantId colon validation — covered
  - [x] Check for GAP: broadcast chain call-order assertion — GAP FOUND (filled in Task 5)
  - [x] Check for GAP: disabled-state end-to-end test — GAP FOUND (filled in Task 5)
  - [x] Check for GAP: conditional hub mapping (enabled vs disabled) — GAP FOUND for disabled state (filled in Task 5)

- [x] Task 5: Fill any gaps identified during audit (bounded: up to 3 minor gaps, <1hr each)
  - [x] No acceptance criteria violations found — all ACs pass
  - [x] Added 3 missing tests (prioritized by risk):
    1. Disabled-state end-to-end: `NotifyProjectionChangedAsync_DirectTransport_WithNoOpBroadcaster_RegeneratesETagSuccessfully` — verifies ETag regen works when NoOp broadcaster is injected
    2. Broadcast chain call-order: `NotifyProjectionChangedAsync_DirectTransport_RegenerateCompletesBeforeBroadcast` — verifies RegenerateAsync called before BroadcastChangedAsync via callback ordering
    3. Conditional hub mapping disabled: `NegotiateEndpoint_WhenSignalRDisabled_IsNotAccessible` — verifies hub endpoint returns 404 when SignalR Enabled=false
  - [x] No more than 3 gaps — exactly 3 gaps filled
  - [x] Build passes: `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
  - [x] Full Tier 1+2 tests pass: Tier 1: 698 passed, Tier 2 (Server.Tests): 1531 passed (+3 new tests)

## Dev Notes

### Architecture: Signal-Only Broadcasting Model

The SignalR integration follows a **signal-only model** (ADR-18.5a):
- Broadcasts contain ONLY `projectionType` and `tenantId` — no projection state, no ETag values
- Clients receive the signal and decide whether to re-query via the REST query endpoint
- This prevents: (a) projection data leakage across SignalR groups, (b) stale data served from SignalR instead of the ETag-validated query pipeline, (c) double-caching surface (SignalR + query actor)

### Key Design Decisions

1. **Fail-Open Broadcasting:** SignalR broadcast failures (hub unavailable, Redis backplane down, group empty) are caught and logged — they NEVER block ETag regeneration or command processing. The system degrades to polling-only when SignalR is unavailable.

2. **Conditional Hub Mapping:** The hub endpoint only exists when `EventStore:SignalR:Enabled = true`. This is a startup-time decision, not runtime — changing the config requires restart. The NoOp broadcaster ensures zero overhead when disabled.

3. **Group Name = ETag Actor ID:** SignalR groups use the same `{ProjectionType}:{TenantId}` format as ETag actor IDs. This is deliberate — it ensures clients subscribe to exactly the same scope that triggers cache invalidation.

4. **MaxGroupsPerConnection (50):** Prevents a single client from subscribing to an unbounded number of projection types. This is a resource exhaustion guard (CFR-1), not a business rule.

5. **Colon Validation:** Both hub `JoinGroup` and client `SubscribeAsync` reject colons in projectionType/tenantId. Colons are the group name separator — allowing them would enable group name injection (e.g., subscribing to `"a:b:c"` could cross group boundaries).

6. **Two Broadcasting Paths:** Projection changes reach the broadcaster via TWO paths:
   - **Direct (in-process):** `DaprProjectionChangeNotifier` → `IETagActor.RegenerateAsync()` → `IProjectionChangedBroadcaster.BroadcastChangedAsync()`
   - **PubSub (cross-process):** External notification → `ProjectionNotificationController` → `IETagActor.RegenerateAsync()` → `IProjectionChangedBroadcaster.BroadcastChangedAsync()`
   Both paths call the broadcaster AFTER ETag regeneration, ensuring the ETag is always current before clients re-query.

### Known Limitation: No Catch-Up on Connect

The signal-only model has **no replay mechanism**. When a client connects (or reconnects after downtime), it joins groups and waits for future signals. Projection changes that occurred while disconnected are not replayed — the client must perform an initial query on connect to establish baseline state, not rely solely on signal receipt. The Blazor sample should demonstrate this pattern (initial load + signal-driven refresh). This is by design (signals are ephemeral, queries are authoritative via ETag), not a gap for this story.

### Scope Boundaries with Stories 10-2 and 10-3

- **Story 10-2 (Redis Backplane):** Task 2 audits that `BackplaneRedisConnectionString` option exists and validates correctly. Do NOT audit Redis backplane *functional behavior* — that is Story 10-2's scope.
- **Story 10-3 (Auto-Rejoin on Reconnection):** Task 4 verifies FR59-related client tests exist. Do NOT fill gaps in reconnection/rejoin behavior — that is Story 10-3's scope.

### Tenant Isolation in SignalR Groups (Scope Note)

Groups are `{ProjectionType}:{TenantId}`, so tenant-A clients cannot receive tenant-B signals by construction (different group names). However, there is no *authorization check* at `JoinGroup` — a client with a valid hub connection could subscribe to any group by name. This is acceptable for Story 10-1 because:
- The signal is data-free (no projection state leaked even if wrong group joined)
- Hub authentication (JWT on the SignalR connection) is a Story 10.2/10.3 concern (Redis backplane / reconnection)
- Note this as a future hardening item, not a gap for this story

### Audit Reporting Requirements

The dev agent MUST report **exact test counts** per test file in the audit table (e.g., "ProjectionChangedHubTests: 12 tests"). This serves as a baseline for regression detection in future stories. See Story 9-5 completion notes for the expected format.

### Previous Story Intelligence (Story 9-5)

Story 9-5 was an audit story for IQueryResponse compile-time enforcement. Key learnings:
- **Audit-only stories work well:** All ACs verified against code with minimal source changes. 2 test gaps filled.
- **Test counts at Story 9-5 completion:** CachingProjectionActor: 23 tests; Full Server.Tests: 1528 passed; Tier 1: 698 passed
- **Pre-existing test failure to ignore:** `ErrorReferenceEndpointTests.AllProblemTypeUris_HaveCorrespondingErrorModel` — ignore if still failing
- **Clone() pattern critical for JsonElement caching** — important if touching query result handling
- **First discovery wins** for projection type — do not change this behavior

### Git Intelligence (Recent Commits)

Last 5 commits are Epic 9 stories (9-1 through 9-5). All followed the audit/gap-fill pattern with feature branches merged via PRs. This confirms the audit-then-fill workflow is the established pattern.

### Key Code Paths to Audit

**Broadcast chain (AC #1):**
```
Projection changes → DaprProjectionChangeNotifier.NotifyProjectionChangedAsync()
    → IETagActor.RegenerateAsync() (new ETag persisted)
    → IProjectionChangedBroadcaster.BroadcastChangedAsync(projectionType, tenantId)
        → SignalRProjectionChangedBroadcaster (when enabled):
            → IHubContext<ProjectionChangedHub, IProjectionChangedClient>
                .Clients.Group("{projectionType}:{tenantId}")
                .ProjectionChanged(projectionType, tenantId)
        → NoOpProjectionChangedBroadcaster (when disabled):
            → Task.CompletedTask (no-op)
```

**Pub/sub receiver chain (AC #1, alternate path):**
```
DAPR pub/sub topic "*.*.projection-changed"
    → ProjectionNotificationController.OnProjectionChanged(notification)
        → IETagActor.RegenerateAsync()
        → IProjectionChangedBroadcaster.BroadcastChangedAsync()
```

**Conditional hub mapping (AC #2):**
```
Program.cs:
    builder.Services.AddEventStoreSignalR(builder.Configuration)
        → Binds "EventStore:SignalR" config
        → services.AddSignalR()
        → if Enabled: register SignalRProjectionChangedBroadcaster (overrides NoOp)

    if signalROptions?.Enabled == true:
        app.MapHub<ProjectionChangedHub>("/hubs/projection-changes")
```

### Testing Pattern

- **xUnit** with **Shouldly** assertions (Tier 1), **NSubstitute** for mocking (Tier 2)
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`
- SignalR client tests are Tier 1 (no DAPR dependency, pure client logic)
- Hub and broadcaster tests are Tier 2 (use `WebApplicationFactory` or NSubstitute mocks)
- ETag actor tests are Tier 2 (use `ActorHost.CreateForTest<T>()`)

### Project Structure Notes

- Hub and broadcaster in `Hexalith.EventStore.CommandApi/SignalR/` — NOT in the Server package, because the hub depends on ASP.NET Core SignalR hosting
- `IProjectionChangedBroadcaster` interface in `Hexalith.EventStore.Client/Projections/` — allows Server package to call broadcaster without depending on CommandApi
- `NoOpProjectionChangedBroadcaster` in `Hexalith.EventStore.Server/Projections/` — default registration
- `EventStoreSignalRClient` in `Hexalith.EventStore.SignalR/` — published NuGet package for client consumers
- `SignalROptions` and DI extensions in CommandApi — configuration is host-level, not library-level

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.1: SignalR Hub & Projection Change Broadcasting]
- [Source: _bmad-output/planning-artifacts/epics.md#FR55]
- [Source: _bmad-output/planning-artifacts/epics.md#FR56]
- [Source: _bmad-output/planning-artifacts/architecture.md]
- [Source: _bmad-output/implementation-artifacts/9-5-iqueryresponse-compile-time-enforcement.md]
- [Source: src/Hexalith.EventStore.CommandApi/SignalR/ProjectionChangedHub.cs]
- [Source: src/Hexalith.EventStore.CommandApi/SignalR/IProjectionChangedClient.cs]
- [Source: src/Hexalith.EventStore.CommandApi/SignalR/SignalRProjectionChangedBroadcaster.cs]
- [Source: src/Hexalith.EventStore.CommandApi/SignalR/SignalROptions.cs]
- [Source: src/Hexalith.EventStore.CommandApi/SignalR/SignalRServiceCollectionExtensions.cs]
- [Source: src/Hexalith.EventStore.Server/Projections/NoOpProjectionChangedBroadcaster.cs]
- [Source: src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/ProjectionNotificationController.cs]
- [Source: src/Hexalith.EventStore.Server/Actors/ETagActor.cs]
- [Source: src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Program.cs]
- [Source: src/Hexalith.EventStore.AppHost/Program.cs]
- [Source: tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/SignalR/ProjectionChangedHubTests.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/SignalR/SignalRProjectionChangedBroadcasterTests.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Integration/SignalRHubEndpointTests.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Actors/ETagActorTests.cs]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

No debug issues encountered.

### Completion Notes List

**Audit Results Table (AC # / Expected / Actual / Pass-Fail):**

| AC # | Path | Expected | Actual | Pass/Fail |
|------|------|----------|--------|-----------|
| #1 (Direct) | DaprProjectionChangeNotifier → RegenerateAsync → BroadcastChangedAsync | Sequential await: regen then broadcast, signal-only, group=`{projType}:{tenantId}` | Confirmed: lines 52-67, sequential await, signal carries only projectionType+tenantId | PASS |
| #1 (PubSub) | DaprProjectionChangeNotifier → PubSub → ProjectionNotificationController → RegenerateAsync → BroadcastChangedAsync | No double-broadcast, regen before broadcast in controller | Confirmed: notifier returns after publish (line 49), controller does regen then broadcast (lines 55-65) | PASS |
| #2 | Conditional hub mapping at `/hubs/projection-changes` | Hub mapped when Enabled=true, not mapped when Enabled=false, NoOp broadcaster when disabled | Confirmed: Program.cs lines 47-53 conditional map, DI toggle via TryAddSingleton/AddSingleton | PASS |

**Test Counts at Story 10-1 Completion:**

| Test File | Count | Tier |
|-----------|-------|------|
| EventStoreSignalRClientTests | 19 | 1 |
| (other Tier 1 test projects) | 679 | 1 |
| **Tier 1 Total** | **698** | **1** |
| ProjectionChangedHubTests | 5 | 2 |
| SignalRProjectionChangedBroadcasterTests | 3 | 2 |
| SignalRHubEndpointTests | 2 (+1 new) | 2 |
| ETagActorTests | 10 | 2 |
| DaprProjectionChangeNotifierTests | 2 | 2 |
| DaprProjectionChangeNotifierSignalRTests | 5 (+2 new) | 2 |
| NoOpProjectionChangedBroadcasterTests | 3 | 2 |
| **Tier 2 Total (Server.Tests)** | **1531** | **2** |

**Gaps Filled (3/3 max):**
1. Added disabled-state end-to-end test verifying ETag regen works with NoOp broadcaster
2. Added broadcast chain call-order verification test (RegenerateAsync before BroadcastChangedAsync)
3. Added conditional hub mapping disabled-state test (negotiate returns 404 when Enabled=false)

**Elicitation Findings:**
- Elicitation #1 (Double-Broadcast): No risk — PubSub mode notifier does not broadcast directly
- Elicitation #4 (RegenerateAsync Failure): Direct mode propagates exception (caller retries); PubSub mode returns 500 (DAPR retries). Both are acceptable patterns.

### File List

- `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionChangeNotifierSignalRTests.cs` (modified — added 2 gap-fill tests)
- `tests/Hexalith.EventStore.Server.Tests/Integration/SignalRHubEndpointTests.cs` (modified — added disabled-state hub mapping test)
- `tests/Hexalith.EventStore.Server.Tests/Integration/SignalRDisabledWebApplicationFactory.cs` (new — WebApplicationFactory with SignalR Enabled=false)
- `_bmad-output/implementation-artifacts/10-1-signalr-hub-and-projection-change-broadcasting.md` (modified — task checkboxes, completion notes, status)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — story status updated)

### Change Log

- 2026-03-19: Story 10-1 audit complete. All acceptance criteria verified against code. 3 test gaps filled (disabled-state end-to-end, call-order verification, conditional hub mapping disabled). No source code changes required. Build: 0 errors, 0 warnings. Tier 1: 698 passed, Tier 2: 1531 passed (+3 new tests, 0 regressions).
