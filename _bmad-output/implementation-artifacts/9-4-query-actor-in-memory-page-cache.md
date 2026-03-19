# Story 9.4: Query Actor In-Memory Page Cache

Status: done

## Story

As a platform developer,
I want query actors to serve as in-memory page caches with no state store persistence,
so that repeated queries return cached data without hitting the microservice.

## Context: What Already Exists

This is a **validation and gap-analysis story**, not a greenfield story. The `CachingProjectionActor` base class, `QueryRouter`, `QueriesController` (Gate 1 + Gate 3), and supporting tests are already substantially implemented. The work is to **audit every acceptance criterion against the existing code, fill any gaps, and ensure full test coverage**.

**Why this audit matters:** If the in-memory page cache silently fails to cache, every query hits the microservice — destroying the query pipeline's performance promise (NFR36: cache hit within 10ms p99, NFR37: cache miss within 200ms p99). If the cache fails to invalidate on ETag change, clients receive stale data indefinitely. If actor deactivation doesn't reset the projection type mapping, queries using the wrong ETag actor will serve perpetually stale or fresh data depending on the mismatch direction. This audit prevents all three failure modes.

### Existing Query Actor & Caching Infrastructure

| Component | Location | Status |
|-----------|----------|--------|
| CachingProjectionActor (abstract base, Gate 2) | `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs` | Built |
| IProjectionActor (DAPR actor interface) | `src/Hexalith.EventStore.Server/Actors/IProjectionActor.cs` | Built |
| QueryEnvelope (DAPR-serializable envelope) | `src/Hexalith.EventStore.Server/Actors/QueryEnvelope.cs` | Built |
| QueryResult (DAPR-serializable result with ProjectionType) | `src/Hexalith.EventStore.Server/Actors/QueryResult.cs` | Built |
| QueryRouter (3-tier routing, actor proxy creation) | `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` | Built |
| IQueryRouter (router interface) | `src/Hexalith.EventStore.Server/Queries/IQueryRouter.cs` | Built |
| QueryActorIdHelper (3-tier actor ID derivation) | `src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs` | Built |
| IETagService (fail-open ETag fetch) | `src/Hexalith.EventStore.Server/Queries/IETagService.cs` | Built |
| DaprETagService (actor proxy, 3s timeout) | `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs` | Built |
| SelfRoutingETag (encode/decode/generate) | `src/Hexalith.EventStore.Server/Queries/SelfRoutingETag.cs` | Built |
| ETagActor (persist-then-cache, migration) | `src/Hexalith.EventStore.Server/Actors/ETagActor.cs` | Built |
| QueriesController (Gate 1 + Gate 3) | `src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs` | Built |
| SubmitQueryHandler (MediatR handler) | `src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs` | Built |
| SubmitQuery (MediatR request) | `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs` | Built |
| IQueryContract (compile-time metadata) | `src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs` | Built |
| IQueryResponse (compile-time ProjectionType) | `src/Hexalith.EventStore.Contracts/Queries/IQueryResponse.cs` | Built |
| GetCounterStatusQuery (sample query) | `samples/Hexalith.EventStore.Sample/Counter/Queries/GetCounterStatusQuery.cs` | Built |
| FakeETagActor (test double) | `src/Hexalith.EventStore.Testing/Fakes/FakeETagActor.cs` | Built |
| Server DI registration (QueryRouter, ETagService) | `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` | Built |

### Existing Test Coverage

| Test File | Tier | Tests |
|-----------|------|-------|
| `Server.Tests/Actors/CachingProjectionActorTests.cs` | 2 | 18+ tests (cache hit/miss, ETag changes, discovery, fallback, flip-flop, DataContract compat) |
| `Server.Tests/Queries/QueryRouterTests.cs` | 2 | Routing tier selection, actor proxy creation, exception handling |
| `Server.Tests/Queries/QueryActorIdHelperTests.cs` | 2 | 3-tier ID derivation, checksum computation, colon validation |
| `Server.Tests/Queries/DaprETagServiceTests.cs` | 2 | Proxy, fail-open, actor ID format |
| `Server.Tests/Queries/SelfRoutingETagTests.cs` | 2 | 18+ tests (encode, decode, roundtrip) |
| `Server.Tests/Controllers/QueriesControllerTests.cs` | 2 | Gate 1 paths (wildcard, mixed, decode, match, 304) |
| `IntegrationTests/ContractTests/QueryEndpointE2ETests.cs` | 3 | Auth/validation |
| `IntegrationTests/ContractTests/QueryValidationE2ETests.cs` | 3 | Query validation |

## Acceptance Criteria

1. **Given** a query actor on first activation (cold call),
   **When** it receives a query,
   **Then** it forwards the query to the microservice via `ExecuteQueryAsync` (FR54)
   **And** caches both the data and the projection type mapping in memory (no DAPR state store persistence)
   **And** returns the result with a self-routing ETag header (set by `QueriesController` Gate 3).

2. **Given** a warm query actor with cached data,
   **When** it receives a subsequent query,
   **Then** it checks the ETag actor for the learned projection type
   **And** returns cached data on ETag match (within 10ms at p99, NFR36 — verified by architecture: DAPR actor single-turn memory-read + IETagService proxy latency; document in Completion Notes, not benchmarked)
   **And** re-queries the microservice on ETag mismatch (within 200ms at p99, NFR37 — verified by architecture: DAPR service invocation round-trip; document in Completion Notes, not benchmarked).

3. **Given** DAPR idle timeout deactivates the query actor,
   **When** the next query arrives,
   **Then** the mapping resets — the cold call re-learns the projection type from the microservice (FR63)
   **And** in-memory cache is empty (no DAPR state store persistence to reload from).

## Definition of Done

Story is complete when:
- **Required:** Tasks 0-6 pass — all audit checks verified, gaps filled, tests green
- **Conditional:** Task 6 — run Tier 1+2 tests only if any `src/` or `tests/` files were modified during Tasks 0-5
- All three acceptance criteria verified against actual code (not epics wording)
- Build passes: `dotnet build Hexalith.EventStore.slnx --configuration Release`
- No regressions in existing Tier 1 or Tier 2 tests
- Audit results table produced in Completion Notes (AC # / Expected / Actual / Pass-Fail)
- Branch: `feat/story-9-4-query-actor-in-memory-page-cache` — create before making any code or test changes
- Scope boundary: up to 3 minor fixes (<1hr each) and missing tests are in scope; algorithmic changes or >3 gaps trigger a follow-up story

## Tasks / Subtasks

- [x] Task 0: Audit CachingProjectionActor cold call flow (AC: #1)
  - [x] Create branch `feat/story-9-4-query-actor-in-memory-page-cache` before any code or test changes
  - [x] Verify `CachingProjectionActor` inherits from `Actor` and implements `IProjectionActor`
  - [x] Verify `QueryAsync(QueryEnvelope)` is the entry point, calling `IETagService.GetCurrentETagAsync()` first
  - [x] Verify on cold call (no cached ETag, no cached payload): `ExecuteQueryAsync(envelope)` is called (forwarding to microservice)
  - [x] Verify successful result is cached in memory: `_cachedPayload` stores `result.Payload.Clone()` (CRITICAL: Clone prevents dangling JsonElement references) and `_cachedETag` stores the current ETag value
  - [x] Verify cache uses NO DAPR state store persistence — only private fields `_cachedETag`, `_cachedPayload`, `_discoveredProjectionType`
  - [x] Verify `QueryResult` includes `ProjectionType` field for runtime discovery (FR63; projection type mapping caching verified in Task 2)
  - [x] Note: Gate 3 ETag response header audit is in Task 4 — do not duplicate here

- [x] Task 1: Audit warm cache hit and miss paths (AC: #2)
  - [x] Verify cache hit condition: `currentETag is not null && currentETag == _cachedETag && _cachedPayload is not null` — note: `_cachedPayload` is `JsonElement?` (nullable struct), so `is not null` correctly distinguishes "never cached" from "cached with data"
  - [x] Verify behavior when `ExecuteQueryAsync` returns `Success=true` with `default(JsonElement)`: `Clone()` on `default(JsonElement)` produces a non-null `JsonElement?` wrapper — confirmed acceptable (empty payload cached is valid: no-data response prevents repeated calls)
  - [x] Verify cache hit returns cached payload WITHOUT calling `ExecuteQueryAsync` (no microservice round-trip)
  - [x] Verify cache hit returns `_discoveredProjectionType` in the result
  - [x] Verify cache miss on ETag mismatch: `_cachedETag != currentETag` triggers `ExecuteQueryAsync` re-query
  - [x] Verify cache miss updates `_cachedPayload` and `_cachedETag` with fresh data
  - [x] Verify null ETag (ETagService fail-open returns null) always executes query and does NOT cache (no caching without an ETag to validate freshness)
  - [x] Verify `IETagService` exception contract: `CachingProjectionActor.QueryAsync` does NOT catch exceptions from `IETagService.GetCurrentETagAsync()` — fail-open is enforced at the `IETagService` implementation level (e.g., `DaprETagService` catches and returns null). If a custom implementation throws, the exception propagates to the caller. Confirmed by design and documented in Completion Notes.
  - [x] Verify failed query result (`Success == false`) is NOT cached — next call re-queries
  - [x] Verify Tier 3 actors (`{QueryType}:{TenantId}`) correctly serve the same cached data for all callers with empty payload — this is correct by design (same query type + tenant = same projection)

- [x] Task 2: Audit runtime projection type discovery (AC: #1, #2, FR63)
  - [x] Verify `GetEffectiveProjectionType(fallbackDomain)` returns `_discoveredProjectionType ?? fallbackDomain` — cold calls use `envelope.Domain`, warm calls use discovered type
  - [x] Verify first successful response with valid `ProjectionType` sets `_discoveredProjectionType` (one-time discovery)
  - [x] Verify when discovered projection type differs from `envelope.Domain`: first call does NOT cache (ETag was fetched using wrong projection type) — returns result but skips `_cachedPayload`/`_cachedETag` update
  - [x] Verify when discovered projection type matches `envelope.Domain`: first call caches normally
  - [x] Verify subsequent mismatch in `ProjectionType` (flip-flop) is logged as warning but first discovery wins — `_discoveredProjectionType` is NOT updated
  - [x] Verify `ValidateProjectionTypeOrNull` rejects: null, empty/whitespace, contains colon, exceeds 100 characters — invalid types fall back to `envelope.Domain`
  - [x] Verify projection type is still discovered even when ETag is null (for correct routing on future calls once ETag becomes available)

- [x] Task 3: Audit actor deactivation resets in-memory state (AC: #3)
  - [x] Verify `CachingProjectionActor` does NOT override `OnDeactivateAsync` — DAPR actor deactivation destroys the actor instance, naturally clearing `_cachedETag`, `_cachedPayload`, and `_discoveredProjectionType`
  - [x] Verify `CachingProjectionActor` does NOT override `OnActivateAsync` — there is no state to load from DAPR state store (pure in-memory cache)
  - [x] Verify that after deactivation + reactivation (new instance), `_discoveredProjectionType` is null → cold call uses `envelope.Domain` for ETag lookup, then re-discovers projection type from microservice response
  - [x] Verify this reset behavior is correct per FR63: "the mapping resets — the cold call re-learns the projection type from the microservice"

- [x] Task 4: Audit QueryRouter and Gate 3 ETag response header (AC: #1)
  - [x] Verify `QueryRouter` creates actor proxy via `IActorProxyFactory.CreateActorProxy<IProjectionActor>(actorId, "ProjectionActor")`
  - [x] Verify `QueryRouter.RouteQueryAsync` constructs `QueryEnvelope` with all required fields (TenantId, Domain, AggregateId, QueryType, Payload, CorrelationId, UserId, EntityId)
  - [x] Verify `QueryRouterResult` propagates `ProjectionType` from `QueryResult` to `SubmitQueryResult` for Gate 3
  - [x] Verify `QueriesController` Gate 3 (after MediatR pipeline returns 200):
    - If `currentETag` is null (Gate 1 was skipped or cache miss): fetch ETag from `IETagService` using `result.ProjectionType ?? request.Domain`
    - Set `Response.Headers.ETag` with double-quoted value
    - Fail-open: if ETag fetch fails, no ETag header is set (acceptable)
  - [x] Verify `QueryEnvelope` and `QueryResult` have `[DataContract]` / `[DataMember]` attributes for DAPR serialization

- [x] Task 5: Validate test coverage completeness
  - [x] Run CachingProjectionActor tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~CachingProjectionActor"` — 21 tests passed (18 existing + 3 new)
  - [x] Run full query pipeline tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~CachingProjectionActor|FullyQualifiedName~QueryRouter|FullyQualifiedName~QueryActorIdHelper|FullyQualifiedName~QueriesController|FullyQualifiedName~ETagService|FullyQualifiedName~SubmitQueryHandler"` — 103 tests passed
  - [x] Verify CachingProjectionActor tests cover:
    - Cold call (cache miss → ExecuteQueryAsync called)
    - Warm cache hit (same ETag → ExecuteQueryAsync NOT called)
    - ETag change (different ETag → ExecuteQueryAsync re-called)
    - Null ETag (fail-open → always execute, never cache)
    - Failed query result not cached
    - Self-routing ETag full value comparison on cache hit
    - Runtime discovery: discovered type used for subsequent ETag lookups
    - Runtime discovery: cache hit returns discovered ProjectionType
    - Discovery mismatch: first call skips cache when projection type differs from domain
    - Null/empty/invalid ProjectionType: fallback to envelope.Domain
    - Flip-flop: first discovery wins
    - Same projection type as domain: caches normally
    - DataContract backward compatibility (old format without ProjectionType)
  - [x] Check if deactivation/reactivation test exists: GAP — added `QueryAsync_DeactivationReactivation_FreshInstanceHasNullCachedState`
  - [x] Check if end-to-end test exists: GAP — added `QueryAsync_FullCacheLifecycle_MissHitInvalidationMiss`
  - [x] Check if QueriesController Gate 3 ETag response header test exists: EXISTS — `Submit_ETagHeaderFormat_DoubleQuoted` verifies double-quoted format
  - [x] Check if invalid-then-valid projection type sequence test exists: GAP — added `QueryAsync_InvalidThenValidProjectionType_DiscoverySucceedsOnSecondCall`
  - [x] 3 gaps found and filled (within scope limit)

- [x] Task 6: Fill any gaps identified during audit (bounded: up to 3 minor gaps, <1hr each)
  - [x] Fix any acceptance criteria violations found — none found
  - [x] Add any missing tests — 3 tests added (deactivation reset, full lifecycle, invalid-then-valid discovery)
  - [x] No more than 3 gaps found — exactly 3 gaps, all minor test additions
  - [x] Ensure build passes: `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 warnings, 0 errors
  - [x] Run full Tier 1+2 tests — Tier 1: 698 passed, Tier 2: 1526 passed (0 failures)

## Dev Notes

### Architecture: Query Actor In-Memory Page Cache (Gate 2)

The `CachingProjectionActor` is the Gate 2 component in the three-gate query caching pipeline:

**Gate 1 — Controller-Level ETag Pre-Check (QueriesController):**
```
Client If-None-Match header → decode self-routing ETag → IETagService.GetCurrentETagAsync()
    → GUID match → HTTP 304 (no actor activation)
    → GUID mismatch or no ETag → proceed to Gate 2
```

**Gate 2 — Actor-Level In-Memory Cache (CachingProjectionActor):**
```
QueryAsync(envelope) → IETagService.GetCurrentETagAsync(effectiveProjectionType, tenantId)
    → ETag match + cached payload exists → return cached (cache hit, no microservice call)
    → ETag mismatch → ExecuteQueryAsync(envelope) → cache new result (cache miss, microservice call)
    → Null ETag → ExecuteQueryAsync(envelope) → do NOT cache (cold ETag actor)
```

**Gate 3 — ETag Response Header (QueriesController):**
```
After 200 OK → IETagService.GetCurrentETagAsync(projectionType, tenantId)
    → Set ETag response header (double-quoted, RFC 7232)
    → Fail-open: no header if ETag unavailable
```

### Key Design Decisions

1. **No DAPR state store persistence for cache data:** Cache data lives only in the actor instance's private fields (`_cachedETag`, `_cachedPayload`, `_discoveredProjectionType`). DAPR idle timeout deactivation destroys the instance and all cached data. This is by design — query actors are ephemeral caches, not durable stores.

2. **Clone() for safe caching:** `result.Payload.Clone()` is CRITICAL. `JsonElement` values reference the underlying `JsonDocument` memory. Without cloning, the cached `JsonElement` becomes a dangling reference when the original `JsonDocument` is disposed.

3. **Runtime projection type discovery (FR63):** The query actor discovers the actual projection type from the first successful microservice response. Before discovery, it falls back to `envelope.Domain` for ETag lookups. After discovery, it uses the discovered type. Discovery mismatch on first call (projection type differs from domain) causes the first call to skip caching — the ETag was fetched using the wrong projection type.

4. **First discovery wins:** If the microservice returns different `ProjectionType` values across calls, the first valid discovery is kept. Subsequent mismatches are logged as warnings but do NOT update `_discoveredProjectionType`. This prevents cache thrashing from inconsistent microservice responses.

5. **Fail-open on null ETag:** When `IETagService.GetCurrentETagAsync()` returns null (ETag actor cold start, service unavailable, timeout), the query always executes (`ExecuteQueryAsync` called) and the result is NOT cached. Without an ETag to compare freshness, caching would risk serving stale data indefinitely.

6. **Projection type validation:** ProjectionType is validated before use: must be non-null, non-empty, ≤100 characters, no colons (colons are the actor ID separator). Invalid types fall back to `envelope.Domain`.

7. **IETagService exception contract:** Fail-open behavior is enforced at the `IETagService` *implementation* level, not at the `CachingProjectionActor` consumer level. `DaprETagService` catches exceptions internally and returns null. `CachingProjectionActor.QueryAsync` does NOT wrap `eTagService.GetCurrentETagAsync()` in a try-catch — if a custom `IETagService` implementation throws, the exception propagates. This is by design: misconfigured implementations should fail loud, not silently degrade.

8. **Gate 2→Gate 3 ETag race is safe by construction:** Between Gate 2 (actor reads ETag) and Gate 3 (controller reads ETag for response header), a projection change notification could regenerate the ETag. If this happens, the controller may set a *newer* ETag on the response than what the actor cached against. This is safe: the next request with the newer ETag will miss at Gate 2 (actor's `_cachedETag` won't match), causing a re-query. Worst case is one extra cache miss — no stale data served.

9. **DAPR actor concurrency guarantees:** DAPR actors use single-threaded turn-based concurrency. Once `QueryAsync` starts, the actor turn is held — DAPR won't deactivate mid-turn, and concurrent queries to the same actor are queued. No mutex or locking is needed in `CachingProjectionActor`.

### Previous Story Intelligence (Story 9-3)

Story 9-3 was a validation/audit story for ETag actor and projection change notification. Key learnings:
- **Colon separator in actor ID confirmed:** Actor IDs use `{projectionType}:{tenantId}` with colons, NOT hyphens
- **Test counts at Story 9-3 completion:** ETag-focused filter: 106 passed; full Server.Tests: 1523 passed, 0 failures; Tier 1: 698 passed
- **Fail-open pattern confirmed everywhere:** ETag decode failures, actor unavailability, timeouts all degrade gracefully
- **Persist-then-cache (FM-1):** ETagActor persists to DAPR state before updating in-memory cache
- **Dual transport for notifications:** PubSub (default) and Direct transports for projection change notification
- **SignalR is optional:** `NoOpProjectionChangedBroadcaster` is default; SignalR enabled via `EventStore:SignalR:Enabled = true`
- **Pre-existing test:** `ErrorReferenceEndpointTests.AllProblemTypeUris_HaveCorrespondingErrorModel` — ignore if still failing

### Key Code Patterns

- **CachingProjectionActor:** Abstract base class. Primary constructor with `ActorHost`, `IETagService`, `ILogger`. Implements `IProjectionActor.QueryAsync()`. Cache is three private fields: `_cachedETag` (string?), `_cachedPayload` (JsonElement?), `_discoveredProjectionType` (string?). Uses source-generated `LoggerMessage` (EventIds 1070-1076).

- **QueryRouter:** Routes via `IActorProxyFactory.CreateActorProxy<IProjectionActor>(actorId, "ProjectionActor")`. Actor ID derived by `QueryActorIdHelper.DeriveActorId()`. Handles "actor not found" exceptions gracefully (returns NotFound).

- **QueriesController Gate 3:** After MediatR pipeline returns 200, fetches ETag from `IETagService` (if not already known from Gate 1), sets `Response.Headers.ETag`. Fail-open if ETag unavailable.

- **QueryEnvelope/QueryResult:** Both use `[DataContract]`/`[DataMember]` attributes MANDATORY for DAPR actor proxy serialization. `QueryResult` has optional `ProjectionType` field (added for FR63, backward-compatible via DataContract semantics).

- **TestCachingProjectionActor:** Test double in `CachingProjectionActorTests.cs`. Returns preconfigured results in sequence. Tracks `ExecuteCallCount` for verification.

### Testing Pattern

- **xUnit** with **Shouldly** assertions, **NSubstitute** for mocking
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`
- CachingProjectionActor tests use `ActorHost.CreateForTest<T>()` with mocked `IETagService`
- `TestCachingProjectionActor` (private class in test file) extends `CachingProjectionActor` with configurable return values
- No need for DAPR container — pure in-memory actor tests
- Integration tests in `QueriesControllerTests.cs` use `WebApplicationFactory`

### Project Structure Notes

- `CachingProjectionActor`, `IProjectionActor`, `QueryEnvelope`, `QueryResult` in `Hexalith.EventStore.Server/Actors/`
- `QueryRouter`, `IQueryRouter`, `QueryActorIdHelper`, `IETagService`, `DaprETagService`, `SelfRoutingETag` in `Hexalith.EventStore.Server/Queries/`
- `SubmitQueryHandler` in `Hexalith.EventStore.Server/Pipeline/`
- `SubmitQuery`, `SubmitQueryResult` in `Hexalith.EventStore.Server/Pipeline/Queries/`
- `QueriesController` in `Hexalith.EventStore.CommandApi/Controllers/`
- `IQueryContract`, `IQueryResponse`, `SubmitQueryRequest`, `SubmitQueryResponse` in `Hexalith.EventStore.Contracts/Queries/`
- DI registration in `Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.4: Query Actor In-Memory Page Cache]
- [Source: _bmad-output/planning-artifacts/epics.md#FR54, FR63]
- [Source: _bmad-output/planning-artifacts/epics.md#NFR36, NFR37]
- [Source: _bmad-output/implementation-artifacts/9-3-etag-actor-and-projection-change-notification.md]
- [Source: src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs]
- [Source: src/Hexalith.EventStore.Server/Actors/IProjectionActor.cs]
- [Source: src/Hexalith.EventStore.Server/Actors/QueryEnvelope.cs]
- [Source: src/Hexalith.EventStore.Server/Actors/QueryResult.cs]
- [Source: src/Hexalith.EventStore.Server/Queries/QueryRouter.cs]
- [Source: src/Hexalith.EventStore.Server/Queries/IETagService.cs]
- [Source: src/Hexalith.EventStore.Server/Queries/DaprETagService.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs]
- [Source: src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

None — clean audit with no implementation blockers.

### Completion Notes List

**Audit Results Table:**

| AC # | Expected Behavior | Actual Code | Pass/Fail |
|------|-------------------|-------------|-----------|
| AC 1 | Cold call forwards to microservice, caches result, returns with self-routing ETag | `CachingProjectionActor.QueryAsync` line 45: `ExecuteQueryAsync` called on cache miss; lines 80-81: caches `Payload.Clone()` + ETag; Gate 3 sets `Response.Headers.ETag` | PASS |
| AC 2 | Warm hit returns cached data (10ms p99); miss re-queries (200ms p99) | Line 39: cache hit returns immediately without `ExecuteQueryAsync`; lines 80-82: cache miss re-queries and updates cache. NFR36/37 verified by architecture: single DAPR actor turn memory read + IETagService proxy latency (not benchmarked) | PASS |
| AC 3 | Deactivation resets mapping, cold call re-learns from microservice | No `OnActivateAsync`/`OnDeactivateAsync` overrides; DAPR destroys instance on idle timeout; new instance has null fields → cold call behavior | PASS |

**IETagService Exception Contract (Task 1):** Confirmed by design. `CachingProjectionActor.QueryAsync` does NOT catch exceptions from `IETagService.GetCurrentETagAsync()`. Fail-open is enforced at the implementation level (`DaprETagService` catches internally and returns null). Custom implementations that throw will propagate the exception to the caller — this is intentional to surface misconfiguration.

**default(JsonElement) caching edge case (Task 1):** `Clone()` on `default(JsonElement)` returns a `JsonElement` with `ValueKind == Undefined`. When assigned to `JsonElement?`, `is not null` returns true. This means an empty/default payload gets cached. This is acceptable — a no-data response from the microservice is a valid result, and caching it prevents repeated calls.

**NFR36/NFR37 (Task 1):** Cache hit latency (NFR36: 10ms p99) is verified by architecture: DAPR actor single-turn memory read of private fields plus IETagService proxy call. Cache miss latency (NFR37: 200ms p99) is verified by architecture: DAPR service invocation round-trip. Not benchmarked in unit tests — existing `Submit_ETagPreCheckPerformance_P99UnderFiveMilliseconds` test in QueriesControllerTests validates Gate 1 path performance.

**Test gaps filled (Task 6):** 3 tests added to `CachingProjectionActorTests.cs`:
1. `QueryAsync_DeactivationReactivation_FreshInstanceHasNullCachedState` — verifies new actor instance starts cold after simulated deactivation
2. `QueryAsync_FullCacheLifecycle_MissHitInvalidationMiss` — verifies complete cycle: cold miss → warm hit → ETag change → cache miss with fresh data
3. `QueryAsync_InvalidThenValidProjectionType_DiscoverySucceedsOnSecondCall` — verifies invalid projection type (colon) is rejected, then valid type discovered on subsequent cache miss

**Post-review hardening (2026-03-19):** Addressed follow-up review findings by strengthening two tests:
1. `QueryAsync_DeactivationReactivation_FreshInstanceHasNullCachedState` now verifies mapping reset + relearn behavior by asserting fallback-domain and discovered-projection ETag lookups across actor instances.
2. `QueryAsync_FullCacheLifecycle_MissHitInvalidationMiss` now includes a fourth call to verify cache re-warm after invalidation miss.
3. Re-ran targeted tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~CachingProjectionActorTests"` → 21 passed, 0 failed.
4. Re-ran broader query pipeline regression slice: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~CachingProjectionActor|FullyQualifiedName~QueryRouter|FullyQualifiedName~QueryActorIdHelper|FullyQualifiedName~QueriesController|FullyQualifiedName~ETagService|FullyQualifiedName~SubmitQueryHandler"` → 103 passed, 0 failed.
5. Re-ran DoD build validation: `dotnet build Hexalith.EventStore.slnx --configuration Release` → build succeeded, 0 errors.

**Final test counts:** CachingProjectionActor: 21 tests (18 existing + 3 new); Query pipeline filter: 103 tests; Full Server.Tests: 1526 passed; Tier 1: 698 passed.

### File List

- `tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs` (modified — 3 tests added)
- `_bmad-output/implementation-artifacts/9-4-query-actor-in-memory-page-cache.md` (modified — task checkboxes, dev agent record, status)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — status updated)

### Change Log

- 2026-03-19: Story 9-4 audit complete. All 3 acceptance criteria verified against code. 3 test gaps filled (deactivation reset, full cache lifecycle, invalid-then-valid projection type discovery). No source code changes needed — all `src/` files passed audit. Tier 1: 698 passed, Tier 2: 1526 passed, 0 regressions.
- 2026-03-19: Follow-up review hardening complete. Strengthened deactivation/reactivation mapping assertions and added post-invalidation cache re-warm assertion in `CachingProjectionActorTests`; targeted class run remains green (21/21).
- 2026-03-19: Post-hardening regression rerun complete for query pipeline slice (`CachingProjectionActor|QueryRouter|QueryActorIdHelper|QueriesController|ETagService|SubmitQueryHandler`), 103/103 passing.
- 2026-03-19: Post-hardening DoD build rerun complete for `Hexalith.EventStore.slnx` in Release configuration; build succeeded.
