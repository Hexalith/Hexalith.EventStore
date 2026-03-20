# Story 10.3: Automatic SignalR Group Rejoining on Reconnection

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a domain service developer,
I want the SignalR client helper to automatically rejoin groups on connection recovery,
So that real-time notifications resume after network interruptions without manual intervention.

## Acceptance Criteria

1. **Given** the SignalR client helper NuGet package (`Hexalith.EventStore.SignalR`),
   **When** a Blazor Server circuit reconnects, a WebSocket drops, or a network interruption recovers,
   **Then** the client automatically rejoins its SignalR groups (FR59)
   **And** real-time push notifications resume without developer intervention.

## Scope

This is an **audit and gap-fill** story. The core FR59 reconnection logic already exists in `EventStoreSignalRClient`. The dev agent must:

1. Audit the existing reconnection implementation against all AC scenarios
2. Identify and fill test gaps for edge cases not yet covered
3. Harden the reconnection policy for production resilience
4. Ensure the client handles permanent disconnection gracefully

**Out of scope:** Blazor `CircuitHandler` subclass, client-side DI registration extension method, connection state observable API. These are consumer-facing features beyond the "automatic rejoin" requirement.

## Tasks / Subtasks

- [x] Task 0: Audit existing reconnection implementation (AC: #1)
    - [x] 0.1 Verify `WithAutomaticReconnect()` covers WebSocket drops and network interruptions
    - [x] 0.2 Verify `OnReconnectedAsync` → `JoinAllGroupsAsync()` chain for group rejoin
    - [x] 0.3 Verify callback preservation across reconnections (local `_subscribedGroups` survives)
    - [x] 0.4 Verify fail-open pattern in `JoinAllGroupsAsync` (catches exceptions, logs warning)
    - [x] 0.5 Verify `_disposeCts` prevents rejoin during disposal
    - [x] 0.6 Assess default reconnect policy: `WithAutomaticReconnect()` without parameters uses [0s, 2s, 10s, 30s] then permanently disconnects — evaluate if this is sufficient for production Blazor Server apps

- [x] Task 1: Audit Blazor Server circuit reconnection scenario (AC: #1)
    - [x] 1.1 Blazor Server uses WebSocket transport by default — circuit reconnection triggers `HubConnection.Reconnected` event → already handled by `OnReconnectedAsync`
    - [x] 1.2 Verify no additional integration needed: Blazor circuit recovery re-establishes the WebSocket, SignalR client auto-reconnect covers this
    - [x] 1.3 Document that the signal-only model requires clients to re-query projections after reconnect to catch missed signals (known limitation, not a gap)

- [x] Task 2: Audit existing test coverage (AC: #1)
    - [x] 2.1 Inventory current tests in `EventStoreSignalRClientTests.cs` (currently 20 tests)
    - [x] 2.2 Map each test to AC scenarios
    - [x] 2.3 Identify untested edge cases

- [x] Task 3: Fill test gaps (AC: #1)
    - [x] 3.1 Add test: `OnReconnectedAsync_MultipleReconnections_RejoinsSameGroupsEachTime` — verify idempotent rejoin across repeated reconnections
    - [x] 3.2 Add test: `OnReconnectedAsync_SubscribeDuringReconnection_NewGroupIncludedInRejoin` — verify groups added between disconnect and reconnect are joined
    - [x] 3.3 Add test: `OnReconnectedAsync_CallbacksFireAfterMultipleReconnections` — verify callbacks survive N reconnections, not just one
    - [x] 3.4 (Optional) Add test: `Closed_Event_LogsWarningWhenAllRetriesExhausted` — if `Closed` event handling is added

- [x] Task 4: Evaluate and document reconnect policy hardening (AC: #1)
    - [x] 4.1 Document default policy behavior: 4 retries over ~42s then permanent disconnect
    - [x] 4.2 Assess if `EventStoreSignalRClientOptions` should expose a configurable `IRetryPolicy`
    - [x] 4.3 If adding configurability: add `RetryPolicy` property to `EventStoreSignalRClientOptions` with sensible default
    - [x] 4.4 If NOT adding configurability: document the limitation as an elicitation for future work

- [x] Task 5: Run full test suite
    - [x] 5.1 `dotnet test tests/Hexalith.EventStore.SignalR.Tests/` — all Tier 1 tests pass (24 passed)
    - [x] 5.2 `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
    - [x] 5.3 `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` — no regressions (267 passed)
    - [x] 5.4 `dotnet test tests/Hexalith.EventStore.Client.Tests/` — no regressions (297 passed)

## Dev Notes

### Existing Implementation Summary

FR59 is **already fully implemented** in `EventStoreSignalRClient`. The reconnection chain works:

```
WebSocket drops / network recovers
  → SignalR WithAutomaticReconnect() retries connection
  → HubConnection.Reconnected event fires
  → OnReconnectedAsync(connectionId?) called
  → JoinAllGroupsAsync() iterates _subscribedGroups
  → For each group: InvokeAsync("JoinGroup", projectionType, tenantId)
  → Callbacks preserved (local ConcurrentDictionary, not sent over wire)
  → Push notifications resume
```

### Files to Read (DO NOT modify unless gap found)

| File                                                                      | Purpose                                         |
| ------------------------------------------------------------------------- | ----------------------------------------------- |
| `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs`              | Core client with reconnection logic (202 lines) |
| `src/Hexalith.EventStore.SignalR/EventStoreSignalRClientOptions.cs`       | Client configuration (18 lines)                 |
| `tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs` | All client tests (321 lines, 19 tests)          |

### Files to Reference (for architectural context)

| File                                                                               | Purpose                                        |
| ---------------------------------------------------------------------------------- | ---------------------------------------------- |
| `src/Hexalith.EventStore.CommandApi/SignalR/ProjectionChangedHub.cs`               | Server-side hub with `JoinGroup`/`LeaveGroup`  |
| `src/Hexalith.EventStore.CommandApi/SignalR/SignalRServiceCollectionExtensions.cs` | Server DI registration                         |
| `src/Hexalith.EventStore.CommandApi/SignalR/SignalROptions.cs`                     | Server-side options (`MaxGroupsPerConnection`) |

### Key Implementation Details

**Reconnection policy:** `WithAutomaticReconnect()` without parameters uses default delays `[0s, 2s, 10s, 30s]`. After 4 failed retries (~42 seconds), the connection enters `Disconnected` state permanently and the `Closed` event fires. Currently, the `Closed` event is **not handled** — no logging, no consumer notification.

**Blazor Server circuit reconnection:** Blazor Server uses WebSocket transport by default. When a circuit reconnects, the underlying WebSocket reconnects, which triggers `HubConnection.Reconnected`. No separate Blazor-specific integration is needed — the existing `WithAutomaticReconnect()` mechanism covers this scenario.

**Signal-only model limitation:** After reconnection, signals missed during downtime are NOT replayed. This is by design (ADR-18.5a). Clients must re-query projections on reconnect to establish baseline state. This is a consumer responsibility, not a client helper responsibility.

**Group name format:** `{projectionType}:{tenantId}` — colons rejected in both parts via `ValidateGroupPart()`.

**Thread safety:** `ConcurrentDictionary<string, GroupSubscription>` for `_subscribedGroups`. `ConcurrentDictionary<Guid, Action>` inside each `GroupSubscription` for callbacks. Thread-safe iteration in `JoinAllGroupsAsync`.

**Disposal safety:** `_disposeCts.IsCancellationRequested` checked in `JoinAllGroupsAsync` loop. `OperationCanceledException` breaks the loop. `DisposeAsync` cancels `_disposeCts` then clears `_subscribedGroups`.

### Existing Test Coverage Map

| Test                                                               | Reconnection Scenario          |
| ------------------------------------------------------------------ | ------------------------------ |
| `OnReconnectedAsync_WithSubscribedGroups_CompletesWithoutThrowing` | Basic reconnect with groups    |
| `OnReconnectedAsync_NoSubscribedGroups_CompletesWithoutThrowing`   | Reconnect with empty state     |
| `OnReconnectedAsync_AfterUnsubscribe_DoesNotRejoinRemovedGroup`    | Unsubscribed groups excluded   |
| `OnReconnectedAsync_PreservesCallbacks_AfterReconnection`          | Callback survival verification |
| `OnReconnectedAsync_NullConnectionId_CompletesWithoutThrowing`     | Null connectionId edge case    |
| `DisposeAsync_PreventsSubsequentReconnectionRejoin`                | Disposal prevents rejoin       |

### Test Gaps Identified

1. **Multiple consecutive reconnections** — Tests only verify single reconnection. Need to verify groups are rejoined correctly after 2+ reconnections.
2. **Subscribe during disconnected state** — Tests subscribe before connecting. Need to verify that groups added while disconnected are included in the next reconnect rejoin.
3. **Callback invocation after N reconnections** — Tests verify callbacks survive one reconnection. Need to verify they survive multiple.

### Elicitations for Dev to Resolve During Audit

1. **LOW:** `Closed` event — Should the client log a warning when all reconnect retries are exhausted? Currently silent. Recommendation: Add a `_logger?.LogWarning` in a `Closed` handler, but do NOT auto-restart (consumer decides).
2. **MEDIUM:** Configurable retry policy — Should `EventStoreSignalRClientOptions` expose an `IRetryPolicy`? Default `WithAutomaticReconnect()` gives up after 42 seconds. For long-running Blazor Server apps, an infinite-retry policy with exponential backoff may be more appropriate. Recommendation: Add `RetryPolicy` property defaulting to null (use SignalR default). If non-null, pass to `WithAutomaticReconnect(retryPolicy)`.
3. **LOW:** `AccessTokenProvider` on reconnect — The `WithUrl` configuration sets `AccessTokenProvider` once at construction. SignalR automatically calls it on reconnect for fresh tokens. Verify this is documented correctly — no code change needed.

### Anti-Patterns to Avoid

- **DO NOT** add a Blazor `CircuitHandler` subclass — out of scope, consumer responsibility
- **DO NOT** add client-side DI registration (`AddEventStoreSignalRClient`) — out of scope for this story
- **DO NOT** add a "catch-up" mechanism for missed signals — contradicts signal-only model (ADR-18.5a)
- **DO NOT** change group name format — must remain `{projectionType}:{tenantId}` matching ETag actor ID
- **DO NOT** modify server-side hub code (`ProjectionChangedHub`) — this story is client-only
- **DO NOT** add `Microsoft.Extensions.DependencyInjection` dependency to `Hexalith.EventStore.SignalR` — it's a lean client NuGet package

### Project Structure Notes

- `Hexalith.EventStore.SignalR` is a published NuGet package — changes affect downstream consumers
- The package has minimal dependencies: `Microsoft.AspNetCore.SignalR.Client` + `Hexalith.EventStore.Contracts`
- Tests are in `tests/Hexalith.EventStore.SignalR.Tests/` (Tier 1 — no external dependencies)
- Test technique: Reflection-based invocation of private methods (`InvokeOnReconnectedAsync`, `InvokeProjectionChanged`)

### References

- [Source: src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs] — FR59 reconnection implementation
- [Source: src/Hexalith.EventStore.SignalR/EventStoreSignalRClientOptions.cs] — Client configuration
- [Source: tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs] — 19 existing tests
- [Source: src/Hexalith.EventStore.CommandApi/SignalR/ProjectionChangedHub.cs] — Server hub (JoinGroup/LeaveGroup)
- [Source: _bmad-output/implementation-artifacts/10-1-signalr-hub-and-projection-change-broadcasting.md] — Story 10-1 audit learnings
- [Source: _bmad-output/implementation-artifacts/10-2-redis-backplane-for-multi-instance-signalr.md] — Story 10-2 Redis backplane context
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-10] — Epic 10 requirements (FR55, FR56, FR59)

### Previous Story Intelligence

**From Story 10-1 (done):**

- Audit + gap-fill pattern: Read code first, verify ACs against implementation, add tests only for gaps
- 3 test gaps filled (disabled-state, broadcast chain order, conditional hub mapping)
- SignalR `ProjectionChangedHub` uses `_connectionGroups` static `ConcurrentDictionary` for server-side group tracking
- `MaxGroupsPerConnection` limit (default 50) enforced server-side
- Fail-open broadcast pattern (ADR-18.5a) — exceptions caught, logged, not rethrown

**From Story 10-2 (ready-for-dev):**

- Redis backplane uses `AddStackExchangeRedis()` with `AbortOnConnectFail = false`
- Single-instance fallback when no Redis configured
- `SignalRServiceCollectionExtensions.ConfigureBackplane()` handles env var fallback
- Test pattern: Mock `IHubContext` with NSubstitute for broadcaster tests

**Git patterns (last 5 commits):**

- Commit message format: `feat: <description> for Story X-Y`
- Branch naming: `feat/story-10-1-signalr-hub-and-projection-change-broadcasting`
- Audit stories produce focused test additions, not large code changes

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Build error: xUnit1030 (ConfigureAwait(false) in tests) → fixed with ConfigureAwait(true)
- Build error: CA2007 (missing ConfigureAwait) → fixed with ConfigureAwait(true)

### Completion Notes List

- **Task 0 (Audit):** All 6 audit subtasks verified. Existing FR59 reconnection chain is correct: `WithAutomaticReconnect()` → `Reconnected` event → `OnReconnectedAsync` → `JoinAllGroupsAsync()`. Fail-open pattern, disposal safety, and callback preservation all confirmed.
- **Task 1 (Blazor Server):** No additional Blazor-specific integration needed. WebSocket transport reconnection triggers `HubConnection.Reconnected`, already handled. Signal-only model limitation documented (clients re-query on reconnect).
- **Task 2 (Test audit):** 20 existing tests inventoried. 6 directly test reconnection. 3 edge case gaps identified: multiple consecutive reconnections, subscribe-during-reconnection, callbacks after N reconnections.
- **Task 3 (Test gaps filled):** 4 new tests added (3 required + 1 optional):
    - `OnReconnectedAsync_MultipleReconnections_RejoinsSameGroupsEachTime`
    - `OnReconnectedAsync_SubscribeDuringReconnection_NewGroupIncludedInRejoin`
    - `OnReconnectedAsync_CallbacksFireAfterMultipleReconnections`
    - `Closed_Event_LogsWarningWhenAllRetriesExhausted`
- **Task 4 (Reconnect policy hardening):** Added `RetryPolicy` property to `EventStoreSignalRClientOptions` (defaults to null = SignalR default [0s, 2s, 10s, 30s]). Client constructor wires it to `WithAutomaticReconnect(retryPolicy)` when non-null. Also added `Closed` event handler that logs a warning when all retries are exhausted (does NOT auto-restart — consumer decides).
- **Task 5 (Full test suite):** All Tier 1 tests pass. Build: 0 errors, 0 warnings. SignalR: 24 passed. Contracts: 267 passed. Client: 297 passed. Sample: 47 passed. Testing: 67 passed.

### File List

- `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs` — Added `Closed` event handler (`OnClosedAsync`), wired configurable `RetryPolicy` in constructor
- `src/Hexalith.EventStore.SignalR/EventStoreSignalRClientOptions.cs` — Added `RetryPolicy` property (`IRetryPolicy?`)
- `tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs` — Added 4 new tests for reconnection edge cases

### Change Log

- 2026-03-20: Story 10-3 implemented. Audited FR59 reconnection implementation, filled 3 test gaps + 1 optional, added configurable `RetryPolicy` and `Closed` event warning logging. Total SignalR tests: 24 (was 20).
