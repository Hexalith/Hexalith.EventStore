# Story 9.3: ETag Actor & Projection Change Notification

Status: done

## Story

As a platform developer,
I want one ETag actor per projection+tenant that tracks the current projection version and regenerates on change notification,
so that ETag pre-checks can return HTTP 304 without activating query actors.

## Context: What Already Exists

This is a **validation and gap-analysis story**, not a greenfield story. The ETag actor, projection change notification pipeline (both PubSub and Direct transports), SignalR broadcasting, cross-process notification controller, and tests are already substantially implemented. The work is to **audit every acceptance criterion against the existing code, fill any gaps, and ensure full test coverage**.

**Why this audit matters:** If projection change notifications silently fail to regenerate ETags, the query pipeline serves stale data indefinitely — clients never receive HTTP 304 mismatches to trigger re-fetches. No error, no crash, just invisible stale data violating eventual consistency guarantees. If SignalR broadcasting fails silently but notifications still work, real-time clients never learn about changes. This audit prevents both failure modes.

### Existing ETag Actor & Notification Infrastructure

| Component | Location | Status |
|-----------|----------|--------|
| ETagActor (DAPR actor, state persistence, migration) | `src/Hexalith.EventStore.Server/Actors/ETagActor.cs` | Built |
| IETagActor (interface: GetCurrentETagAsync, RegenerateAsync) | `src/Hexalith.EventStore.Server/Actors/IETagActor.cs` | Built |
| IETagService (fail-open interface) | `src/Hexalith.EventStore.Server/Queries/IETagService.cs` | Built |
| DaprETagService (actor proxy, 3s timeout) | `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs` | Built |
| SelfRoutingETag (encode/decode/generate) | `src/Hexalith.EventStore.Server/Queries/SelfRoutingETag.cs` | Built |
| IProjectionChangeNotifier (client interface) | `src/Hexalith.EventStore.Client/Projections/IProjectionChangeNotifier.cs` | Built |
| IProjectionChangedBroadcaster (SignalR interface) | `src/Hexalith.EventStore.Client/Projections/IProjectionChangedBroadcaster.cs` | Built |
| DaprProjectionChangeNotifier (PubSub + Direct) | `src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs` | Built |
| NoOpProjectionChangedBroadcaster (default) | `src/Hexalith.EventStore.Server/Projections/NoOpProjectionChangedBroadcaster.cs` | Built |
| ProjectionChangedNotification (contract DTO) | `src/Hexalith.EventStore.Contracts/Projections/ProjectionChangedNotification.cs` | Built |
| ProjectionNotificationController (cross-process) | `src/Hexalith.EventStore.CommandApi/Controllers/ProjectionNotificationController.cs` | Built |
| ProjectionChangedHub (SignalR hub) | `src/Hexalith.EventStore.CommandApi/SignalR/ProjectionChangedHub.cs` | Built |
| SignalRProjectionChangedBroadcaster | `src/Hexalith.EventStore.CommandApi/SignalR/SignalRProjectionChangedBroadcaster.cs` | Built |
| SignalROptions (config) | `src/Hexalith.EventStore.CommandApi/SignalR/SignalROptions.cs` | Built |
| SignalRServiceCollectionExtensions (DI) | `src/Hexalith.EventStore.CommandApi/SignalR/SignalRServiceCollectionExtensions.cs` | Built |
| IProjectionChangedClient (typed SignalR client) | `src/Hexalith.EventStore.CommandApi/SignalR/IProjectionChangedClient.cs` | Built |
| ProjectionChangeNotifierOptions (config) | `src/Hexalith.EventStore.Server/Configuration/ProjectionChangeNotifierOptions.cs` | Built |
| NamingConventionEngine.GetProjectionChangedTopic | `src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs` | Built |
| EventStoreProjection.FireProjectionChangeNotification | `src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs` | Built |
| QueriesController Gate 1 (ETag pre-check) | `src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs` | Built |
| FakeETagActor (test double) | `src/Hexalith.EventStore.Testing/Fakes/FakeETagActor.cs` | Built |
| Server DI registration (ETagActor, services) | `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` | Built |

### Existing Test Coverage

| Test File | Tier | Tests |
|-----------|------|-------|
| `Server.Tests/Actors/ETagActorTests.cs` | 2 | 8 tests (persist-then-cache, activation, migration, cold start) |
| `Server.Tests/Queries/DaprETagServiceTests.cs` | 2 | 8 tests (proxy, fail-open, actor ID format) |
| `Server.Tests/Queries/SelfRoutingETagTests.cs` | 2 | 18+ tests (encode, decode, roundtrip) |
| `Server.Tests/Controllers/QueriesControllerTests.cs` | 2 | Gate 1 paths (wildcard, mixed, decode, match, 304) |
| `Server.Tests/Projections/DaprProjectionChangeNotifierTests.cs` | 2 | 2 tests (PubSub path, Direct path) |
| `Server.Tests/Projections/DaprProjectionChangeNotifierSignalRTests.cs` | 2 | 3 tests (broadcast after regenerate, PubSub skips broadcast, broadcast failure) |
| `Server.Tests/Projections/NoOpProjectionChangedBroadcasterTests.cs` | 2 | 3 tests (synchronous completion) |
| `Server.Tests/Integration/ETagActorIntegrationTests.cs` | 2 | 14 tests (cross-process, in-process, actor behavior) |
| `IntegrationTests/ContractTests/QueryEndpointE2ETests.cs` | 3 | Auth/validation (no ETag-specific E2E) |

## Acceptance Criteria

1. **Given** an ETag actor keyed by `{ProjectionType}:{TenantId}`,
   **When** a projection change is notified via `NotifyProjectionChangedAsync(projectionType, tenantId, entityId?)` (FR52),
   **Then** the ETag is regenerated (new GUID portion in self-routing format) (FR51)
   **And** all previously-issued ETags for that projection+tenant pair become stale via GUID mismatch (FR58, coarse invalidation — query actor cache is Story 9-4 scope; here "invalidated" means ETag staleness).

2. **Given** a query arrives with a valid `If-None-Match` header,
   **When** the decoded ETag's GUID matches the current ETag actor value,
   **Then** the endpoint returns HTTP 304 without activating the query actor (FR53)
   **And** the pre-check completes within 5ms at p99 for warm actors (NFR35 — verified by architecture: DAPR actor single-turn memory-read latency is sub-millisecond by design; document in Completion Notes, not benchmarked in tests).

3. **Given** a projection change notification arrives via DAPR pub/sub (cross-process path),
   **When** `ProjectionNotificationController` receives it,
   **Then** the ETag actor `RegenerateAsync()` is called
   **And** the projection changed broadcaster is invoked (SignalR or no-op depending on configuration, fail-open)
   **And** non-200 responses trigger DAPR pub/sub retries (CM-1).

## Definition of Done

Story is complete when:
- **Required:** Tasks 0-6 pass — all audit checks verified, gaps filled, tests green
- **Conditional:** Task 6 — run Tier 1+2 tests only if any `src/` or `tests/` files were modified during Tasks 0-5
- All three acceptance criteria verified against actual code (not epics wording)
- Build passes: `dotnet build Hexalith.EventStore.slnx --configuration Release`
- No regressions in existing Tier 1 or Tier 2 tests
- Audit results table produced in Completion Notes (AC # / Expected / Actual / Pass-Fail)
- Branch: `feat/story-9-3-etag-actor-projection-notification` — create before making any code or test changes
- Scope boundary: up to 3 minor fixes (<1hr each) and missing tests are in scope; algorithmic changes or >3 gaps trigger a follow-up story

## Tasks / Subtasks

- [x] Task 0: Audit ETag actor keying and RegenerateAsync flow (AC: #1)
  - [x] Create branch `feat/story-9-3-etag-actor-projection-notification` before any code or test changes
  - [x] Verify `ETagActor` is registered with DAPR actor type name `"ETagActor"` (constant `ETagActorTypeName`)
  - [x] Verify actor ID format is `{projectionType}:{tenantId}` (colon separator, NOT hyphen — matches Story 9-1 separator convention)
  - [x] Verify `RegenerateAsync()` generates a new self-routing ETag via `SelfRoutingETag.GenerateNew(projectionType)`
  - [x] Verify `RegenerateAsync()` follows persist-then-cache pattern (FM-1): state persisted to DAPR via `StateManager.SetStateAsync` + `SaveStateAsync` before updating in-memory cache
  - [x] Verify the projection type is extracted from actor ID via `ExtractProjectionType()` (splits on `:`, takes first segment)
  - [x] Verify `GetCurrentETagAsync()` returns the cached ETag value (or null on cold start)

- [x] Task 1: Audit NotifyProjectionChangedAsync end-to-end flow (AC: #1)
  - [x] Verify `IProjectionChangeNotifier.NotifyProjectionChangedAsync(projectionType, tenantId, entityId?, cancellationToken)` interface exists in Client package
  - [x] Verify `DaprProjectionChangeNotifier` implements both transport modes:
    - **PubSub (default):** Publishes `ProjectionChangedNotification` to DAPR topic `{tenantId}.{projectionType}.projection-changed` via `NamingConventionEngine.GetProjectionChangedTopic`
    - **Direct:** Calls `IETagActor.RegenerateAsync()` via actor proxy in-process
  - [x] Verify Direct transport always calls `IProjectionChangedBroadcaster.BroadcastChangedAsync()` after `RegenerateAsync()` (ADR-18.5a)
  - [x] Verify PubSub transport does NOT call broadcaster (broadcasting happens in `ProjectionNotificationController` on receipt)
  - [x] Verify `ProjectionChangeNotifierOptions` configuration binds to `EventStore:ProjectionChanges` with `Transport` enum (PubSub/Direct)
  - [x] Verify DI registration: `IProjectionChangeNotifier` -> `DaprProjectionChangeNotifier` (singleton) in `ServiceCollectionExtensions.AddEventStoreServer()`
  - [x] Verify DI wiring sets `Notifier` property on `EventStoreProjection` instances via `AddEventStoreServer()` — if null, Path 3 auto-notifications silently fail (FM-5)

- [x] Task 2: Audit coarse invalidation and ETag regeneration (AC: #1, FR58)
  - [x] Verify that `RegenerateAsync()` creates a new GUID for the ETag, effectively invalidating all previously-issued ETags for that projection+tenant pair
  - [x] Verify that the new ETag replaces the previous value in DAPR state store (single key, not append)
  - [x] Verify that coarse invalidation is by design: ANY projection change for a `{projectionType}:{tenantId}` pair invalidates ALL cached query results (no entity-level granularity yet)
  - [x] Verify `entityId?` parameter is accepted but currently unused (reserved for future fine-grained invalidation)

- [x] Task 3: Audit Gate 1 ETag pre-check and HTTP 304 flow (AC: #2)
  - [x] Verify `QueriesController.Submit()` implements Gate 1: parse `If-None-Match` → `AnalyzeHeaderProjectionTypes()` → `IETagService.GetCurrentETagAsync()` → compare → return 304 or proceed
  - [x] Verify `AnalyzeHeaderProjectionTypes()` decodes self-routing ETag to extract projection type
  - [x] Verify ETag comparison: if decoded ETag GUID matches current actor ETag GUID, return HTTP 304 with `ETag` response header (double-quoted per RFC 7232)
  - [x] Verify on cache miss (ETag mismatch or no ETag): query proceeds to Gate 2 (full MediatR pipeline) and Gate 3 (ETag response header on 200)
  - [x] Verify `DaprETagService` constructs actor proxy with actor ID `{projectionType}:{tenantId}` matching `ETagActor` ID format
  - [x] Verify fail-open semantics: any ETag decode failure, actor unavailability, or timeout results in cache miss (full query execution)
  - [x] Verify `MaxIfNoneMatchValues = 10` DoS protection

- [x] Task 4: Audit cross-process projection notification path (AC: #3)
  - [x] Verify `ProjectionNotificationController` endpoint: `POST /projections/changed`
  - [x] Verify DAPR subscription route: `*.*.projection-changed` matches `{tenantId}.{projectionType}.projection-changed` topic pattern
  - [x] Verify controller workflow: validate `ProjectionChangedNotification` → create actor proxy for `{projectionType}:{tenantId}` → call `RegenerateAsync()` → call `BroadcastChangedAsync()` (fail-open)
  - [x] Verify `ProjectionChangedNotification` record has required `ProjectionType`, `TenantId` and optional `EntityId`
  - [x] Verify non-200 response on failure triggers DAPR pub/sub retries (CM-1 pattern)
  - [x] Verify validation rejects empty/null `ProjectionType` or `TenantId`
  - [x] Verify SignalR broadcast failure does not cause non-200 response (fail-open, ADR-18.5a)
  - [x] Verify `NamingConventionEngine.GetProjectionChangedTopic(projectionType, tenantId)` output format matches the DAPR subscription route `*.*.projection-changed` (e.g., produces `{tenantId}.{projectionType}.projection-changed` with dot separators)

- [x] Task 5: Validate test coverage completeness
  - [x] Run all ETag actor and projection notification tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~ETagActor|FullyQualifiedName~ProjectionChange|FullyQualifiedName~ProjectionNotification|FullyQualifiedName~QueriesController|FullyQualifiedName~ETagService"` — expect 50+ tests matching this filter (Story 9-2 baseline: 96 ETag-focused tests)
  - [x] Verify ETagActor tests cover: RegenerateAsync persist-then-cache, OnActivateAsync cold start, OnActivateAsync warm load, old-format migration, migration failure fallback
  - [x] Verify DaprProjectionChangeNotifier tests cover: PubSub transport publishes to correct topic, Direct transport calls RegenerateAsync, Direct transport calls BroadcastChangedAsync after regenerate, broadcaster failure doesn't break notification
  - [x] Verify ProjectionNotificationController integration tests cover: cross-process notification triggers RegenerateAsync, invalid notifications rejected, SignalR broadcast triggered
  - [x] Verify QueriesController tests cover Gate 1: ETag match returns 304, ETag mismatch proceeds to query, missing If-None-Match skips Gate 1, wildcard skips Gate 1, mixed projection types skip Gate 1
  - [x] Check if end-to-end test exists: submit notification → ETag regenerated → query with old ETag returns 200 (not 304)
  - [x] Check if SignalR hub test exists for group join/leave/broadcast
  - [x] Check if `EventStoreProjection.FireProjectionChangeNotification()` test exists (in-process auto-notification path)
  - [x] If any gaps found, add tests bounded by scope — **priority order: (1) notification→ETag staleness verification test (notify → regenerate → old ETag comparison → mismatch returns 200 not 304), (2) EventStoreProjection auto-notification test (Path 3 DI wiring), (3) SignalR hub group management test**

- [x] Task 6: Fill any gaps identified during audit (bounded: up to 3 minor gaps, <1hr each)
  - [x] Fix any acceptance criteria violations found
  - [x] Add any missing tests
  - [x] If more than 3 gaps found, or any gap requires >1 hour, document in Completion Notes and create follow-up story
  - [x] Ensure build passes: `dotnet build Hexalith.EventStore.slnx --configuration Release`
  - [x] Run full Tier 1+2 tests if any `src/` or `tests/` files were modified

## Dev Notes

### Architecture: ETag Actor & Projection Change Notification Design

The ETag actor and projection change notification system forms the cache invalidation backbone of the query pipeline. Two parallel paths trigger ETag regeneration:

**Path 1 — Cross-Process (PubSub):**
```
External Domain Service → DAPR pub/sub → ProjectionNotificationController
    → IETagActor.RegenerateAsync() → IProjectionChangedBroadcaster.BroadcastChangedAsync()
```

**Path 2 — In-Process (Direct):**
```
DaprProjectionChangeNotifier (Direct transport) → IETagActor.RegenerateAsync()
    → IProjectionChangedBroadcaster.BroadcastChangedAsync()
```

**Path 3 — Auto-Notification (EventStoreProjection):**
```
EventStoreProjection.Project() → FireProjectionChangeNotification()
    → IProjectionChangeNotifier.NotifyProjectionChangedAsync() → (Path 1 or 2)
```

**Query-side effect:**
```
QueriesController Gate 1 → decode If-None-Match → IETagService.GetCurrentETagAsync()
    → compare GUID portions → 304 (match) or proceed to full query (miss)
```

### Key Design Decisions

1. **Colon separator in actor ID:** Actor IDs use `{projectionType}:{tenantId}` with colons, NOT hyphens. The epics file notation `{ProjectionType}-{TenantId}` is illustrative only. This was validated in Story 9-1.

2. **Coarse invalidation (FR58):** ANY projection change for a `{projectionType}:{tenantId}` pair regenerates the ETag and invalidates ALL cached query results for that pair. Entity-level (fine-grained) invalidation is deferred — the `entityId?` parameter is accepted but unused.

3. **Persist-then-cache (FM-1):** `ETagActor.RegenerateAsync()` persists the new ETag to DAPR state store BEFORE updating the in-memory cache. This prevents serving a new ETag that wasn't durably stored.

4. **Fail-open everywhere:** ETag decode failures, actor unavailability, DaprETagService timeouts (3s), and SignalR broadcast failures all degrade gracefully to cache misses or no-ops. The query pipeline never blocks due to ETag infrastructure issues.

5. **Dual transport for notifications:** `ProjectionChangeNotifierOptions.Transport` selects between PubSub (default, for cross-process scenarios) and Direct (for low-latency in-process scenarios). PubSub path relies on `ProjectionNotificationController` to call `RegenerateAsync()`; Direct path calls it immediately.

6. **SignalR is optional:** `NoOpProjectionChangedBroadcaster` is the default. SignalR broadcasting is enabled only when `EventStore:SignalR:Enabled = true`, registering `SignalRProjectionChangedBroadcaster` via `AddEventStoreSignalR()`.

7. **Topic naming:** Cross-process notifications use topic `{tenantId}.{projectionType}.projection-changed` via `NamingConventionEngine.GetProjectionChangedTopic()`. DAPR subscription route `*.*.projection-changed` matches this pattern.

8. **CM-1 (retry on failure):** `ProjectionNotificationController` returns non-200 on failure, triggering DAPR pub/sub retries. However, SignalR broadcast failure does NOT cause non-200 (ADR-18.5a) — only actor regeneration failure does.

### Previous Story Intelligence (Story 9-2)

Story 9-2 was a validation/audit story for self-routing ETag encode/decode. Key learnings:
- **Separator convention confirmed:** Code uses colons (`:`) not hyphens (`-`) — epics notation is illustrative only
- **Pre-existing test failure:** `ErrorReferenceEndpointTests.AllProblemTypeUris_HaveCorrespondingErrorModel` — ignore if still failing (unchanged across stories)
- **Test counts at Story 9-2 completion:** ETag-focused filter: 96 passed; full Server.Tests: 1517/1518 (1 pre-existing failure)
- **W/ weak ETag prefix:** Handled correctly by construction — fail-open to cache miss
- **Non-ASCII projection types:** Base64url handles UTF-8 correctly — roundtrip tests added
- **ETagActor migration:** Old-format ETags (GUID-only) auto-migrate to self-routing format on activation

### Key Code Patterns

- **ETagActor:** DAPR actor with state key `"etag"`. Actor ID `{projectionType}:{tenantId}`. Persist-then-cache on regenerate. Auto-migration from old format on activate. Falls back to cold start (null) on any state read failure (FM-2).
- **DaprProjectionChangeNotifier:** Singleton. Two transports. Always broadcasts on Direct; never on PubSub (broadcasting happens at controller level). Structured logging (EventIds 1051, 1088).
- **ProjectionNotificationController:** POST `/projections/changed`. DAPR subscription `*.*.projection-changed`. Validates notification, calls actor proxy, broadcasts, returns 200/500.
- **SignalR hub:** Groups keyed by `{projectionType}:{tenantId}`. Max 50 groups per connection. Clean group cleanup on disconnect. Signal-only (no data payload). Defense-in-depth: colons forbidden in projectionType/tenantId parameters.
- **DaprETagService:** Scoped. 3-second actor proxy timeout. Fail-open (returns null on exception). Actor ID `{projectionType}:{tenantId}`.
- **EventStoreProjection:** Base class with `FireProjectionChangeNotification()` called after successful projection. Notifier injected post-construction by DI.

### Testing Pattern

- **xUnit** with **Shouldly** assertions, **NSubstitute** for mocking
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`
- ETagActor tests use mocked `IActorStateManager` for state persistence verification
- Integration tests in `Server.Tests/Integration/ETagActorIntegrationTests.cs` use `WebApplicationFactory` with mocked `IActorProxyFactory`
- `FakeETagActor` in Testing project tracks regeneration count and call history

### Project Structure Notes

- `ETagActor` and `IETagActor` in `Hexalith.EventStore.Server/Actors/`
- `DaprProjectionChangeNotifier` in `Hexalith.EventStore.Server/Projections/`
- `IProjectionChangeNotifier` and `IProjectionChangedBroadcaster` in `Hexalith.EventStore.Client/Projections/`
- `ProjectionChangedNotification` in `Hexalith.EventStore.Contracts/Projections/`
- `ProjectionNotificationController` in `Hexalith.EventStore.CommandApi/Controllers/`
- SignalR components in `Hexalith.EventStore.CommandApi/SignalR/`
- `ProjectionChangeNotifierOptions` in `Hexalith.EventStore.Server/Configuration/`
- DI registration in `Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`
- `FakeETagActor` in `Hexalith.EventStore.Testing/Fakes/`

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.3: ETag Actor & Projection Change Notification]
- [Source: _bmad-output/planning-artifacts/epics.md#FR51, FR52, FR53, FR58]
- [Source: _bmad-output/planning-artifacts/epics.md#NFR35]
- [Source: _bmad-output/implementation-artifacts/9-2-self-routing-etag-encode-decode.md]
- [Source: src/Hexalith.EventStore.Server/Actors/ETagActor.cs]
- [Source: src/Hexalith.EventStore.Server/Actors/IETagActor.cs]
- [Source: src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/ProjectionNotificationController.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs]
- [Source: src/Hexalith.EventStore.CommandApi/SignalR/ProjectionChangedHub.cs]
- [Source: src/Hexalith.EventStore.Client/Projections/IProjectionChangeNotifier.cs]
- [Source: src/Hexalith.EventStore.Contracts/Projections/ProjectionChangedNotification.cs]
- [Source: src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

### Completion Notes List

**Audit Results Table:**

| AC # | Expected | Actual | Pass/Fail |
|------|----------|--------|-----------|
| AC1 (ETag keying + regeneration) | ETag actor keyed by `{projectionType}:{tenantId}`, RegenerateAsync creates new GUID, persist-then-cache, coarse invalidation | ETagActor.cs uses colon separator, `SelfRoutingETag.GenerateNew()`, `SetStateAsync`→`SaveStateAsync`→cache pattern, single-key overwrite | PASS |
| AC2 (Gate 1 pre-check + 304) | QueriesController decodes If-None-Match, compares GUID, returns 304 or proceeds | `AnalyzeHeaderProjectionTypes()` decodes self-routing ETag, GUID match → 304 with double-quoted ETag header, fail-open on any error | PASS |
| AC3 (Cross-process notification) | ProjectionNotificationController receives pub/sub, calls RegenerateAsync + BroadcastChangedAsync, non-200 on failure | POST `/projections/changed` with `*.*.projection-changed` subscription, validates→proxies→regenerates→broadcasts(fail-open), 500 on actor failure | PASS |

**NFR35 (p99 5ms warm actor pre-check):** Verified by architecture — DAPR actor single-turn memory-read latency is sub-millisecond by design. The QueriesController test `Submit_ETagPreCheckPerformance_P99UnderFiveMilliseconds` validates this architecturally.

**Gaps Found and Filled (2 of 3 max):**
1. **notification→ETag staleness test** — Added `NotificationCausesETagStaleness_OldETagNoLongerMatches` in `ETagActorIntegrationTests.cs`. Verifies: initial ETag → notification → ETag regenerated → old ETag is stale (different from new).
2. **EventStoreProjection auto-notification tests** — Added 4 tests in `EventStoreProjectionTests.cs`: `Project_WithNotifierAndTenantId_CallsNotifyProjectionChangedAsync`, `Project_WithoutNotifier_DoesNotThrow` (FM-5), `Project_WithNotifierButNoTenantId_SkipsNotification`, `ProjectFromJson_WithNotifier_CallsNotifyProjectionChangedAsync`.
3. **SignalR hub group management test** — Already existed (6 tests in `ProjectionChangedHubTests.cs`). No gap.

**Post-Review Hardening (2026-03-19):**
- Strengthened `NotificationCausesETagStaleness_OldETagNoLongerMatches` in `ETagActorIntegrationTests.cs` to assert actor proxy routing uses exact actor ID `test-projection:acme`.
- Re-ran targeted test: 1 passed, 0 failed.
- Re-ran story filter (`ETagActor|ProjectionChange|ProjectionNotification|QueriesController|ETagService`): 106 passed, 0 failed.

**No AC violations found.** All source code matches the acceptance criteria.

**Test Counts:**
- ETag-focused filter: 106 passed (up from 96 baseline)
- Full Server.Tests: 1523 passed, 0 failures
- Tier 1 (all 5 projects): 698 passed, 0 failures
- Total new tests added: 5 (1 staleness + 4 auto-notification)

### File List

- `tests/Hexalith.EventStore.Server.Tests/Integration/ETagActorIntegrationTests.cs` (modified — added staleness verification test)
- `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreProjectionTests.cs` (modified — added 4 auto-notification tests with SpyProjectionChangeNotifier)
- `_bmad-output/implementation-artifacts/9-3-etag-actor-and-projection-change-notification.md` (modified — story status and task tracking)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — story status updates)

### Change Log

- 2026-03-19: Completed audit of all acceptance criteria (Tasks 0-4). All existing code verified correct.
- 2026-03-19: Identified 2 test coverage gaps (Task 5): missing staleness verification test and missing EventStoreProjection auto-notification tests.
- 2026-03-19: Filled gaps (Task 6): Added 5 new tests. Build passes. All Tier 1+2 tests pass (0 regressions).
- 2026-03-19: Post-review fix applied: added explicit actor ID routing assertion (`test-projection:acme`) to staleness integration test and re-validated 9-3 scope tests (106/106).
