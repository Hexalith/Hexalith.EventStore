# Story 18.8: IQueryResponse Enforcement and Runtime Discovery

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **domain service developer**,
I want **query responses to enforce a mandatory `ProjectionType` field at compile time, and query actors to discover the projection type at runtime from the microservice's first cold call response**,
So that **ETag cache lookups use the correct projection type (not `request.Domain`) — decoupling cache validation from request metadata, preventing silent caching failures when domain and projection type differ, and enabling the self-routing ETag architecture from Story 18-7 to work end-to-end**.

## Acceptance Criteria

1. **IQueryResponse compile-time enforcement (FR62)** — **Given** the Contracts package defines `IQueryResponse<T>`, **When** a microservice implements it, **Then** it must provide a non-empty `ProjectionType` string enforced at compile time via a required interface member. An empty or whitespace-only `ProjectionType` returned at runtime is treated as an error by the query actor (log warning, fall back to `envelope.Domain`).

2. **QueryResult carries ProjectionType through actor proxy (FR62)** — **Given** `QueryResult` is the DataContract-serialized response from projection actor to query router, **When** `ExecuteQueryAsync` returns a `QueryResult`, **Then** the result includes a `string? ProjectionType` field (DataMember). The CachingProjectionActor validates non-empty on success; null/empty triggers fallback to `envelope.Domain`.

3. **Runtime projection type discovery on cold call (FR63)** — **Given** a CachingProjectionActor receives its first query (cold call), **When** `ExecuteQueryAsync` returns a successful `QueryResult` with a non-empty `ProjectionType`, **Then** the actor stores the `ProjectionType` in an in-memory field `_discoveredProjectionType` and uses it (instead of `envelope.Domain`) for all subsequent `IETagService.GetCurrentETagAsync()` calls.

4. **Projection type mapping resets on actor deactivation (FR63)** — **Given** a CachingProjectionActor has a cached `_discoveredProjectionType`, **When** the actor is deactivated by DAPR idle timeout and re-activated, **Then** the `_discoveredProjectionType` is null (default field state) and the next cold call re-learns it from the microservice.

5. **End-to-end projection type flow through pipeline** — **Given** a query completes successfully with a `ProjectionType` in the `QueryResult`, **When** the result flows through `QueryRouter` → `SubmitQueryHandler` → `QueriesController`, **Then** the `ProjectionType` is carried through `QueryRouterResult.ProjectionType` → `SubmitQueryResult.ProjectionType` → used by the endpoint for the ETag response header fetch (replacing `request.Domain` fallback).

6. **QueriesController uses runtime ProjectionType for ETag response (FR63)** — **Given** the query completed and `SubmitQueryResult.ProjectionType` is non-empty, **When** the endpoint sets the `ETag` response header, **Then** it calls `IETagService.GetCurrentETagAsync(result.ProjectionType, request.Tenant)` instead of `IETagService.GetCurrentETagAsync(request.Domain, request.Tenant)`. If `ProjectionType` is null/empty, fall back to `request.Domain` (backward compatibility).

7. **Short projection type name guidance (FR64)** — **Given** projection type names are base64url-encoded in self-routing ETags, **When** documentation/XML-doc guidance is provided, **Then** it recommends short names (e.g., `counter` not `Hexalith.EventStore.Sample.CounterProjection`) and explains that longer names produce longer ETag HTTP header values.

8. **All existing tests pass** — All Tier 1 and Tier 2 tests continue to pass after modifications. New tests validate the runtime discovery flow and compile-time enforcement.

## Tasks / Subtasks

<!-- TASK PRIORITY: Tasks 1-2 = Contract & Data Changes, Tasks 3-4 = Runtime Discovery, Task 5 = Pipeline Flow, Task 6 = Endpoint Update, Task 7 = Tests, Task 8 = Verification -->

- [x] Task 1: [CONTRACTS] Add IQueryResponse<T> interface (AC: #1, #7)
    - [x] 1.1 Create `IQueryResponse<T>` in `src/Hexalith.EventStore.Contracts/Queries/`:
        ```csharp
        public interface IQueryResponse<out T>
        {
            T Data { get; }
            string ProjectionType { get; }
        }
        ```
    - [x] 1.2 Add XML-doc on `ProjectionType` recommending short kebab-case names: "Use short projection type names (e.g., `counter`, `order-list`) — they are base64url-encoded in self-routing ETags, so longer names produce longer HTTP header values (FR64)."
    - [x] 1.3 Do NOT add any abstract base class or record implementation — keep it as a pure interface. Microservice developers decide their concrete implementation.
    - [x] 1.4 Ensure `T` is covariant (`out T`) to allow `IQueryResponse<DerivedType>` to be assigned to `IQueryResponse<BaseType>`.

- [x] Task 2: [DATA] Add ProjectionType to QueryResult and pipeline records (AC: #2, #5)
    - [x] 2.1 Add `[property: DataMember] string? ProjectionType = null` to `QueryResult` record in `src/Hexalith.EventStore.Server/Actors/QueryResult.cs`. Place after `ErrorMessage`. Default `null` for backward compatibility — existing callers that don't set it get null.
    - [x] 2.2 Add `string? ProjectionType = null` to `QueryRouterResult` record in `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs`. Place after `ErrorMessage`.
    - [x] 2.3 Add `string? ProjectionType = null` to `SubmitQueryResult` record in `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs`. Place after `Payload`.
    - [x] 2.4 Update `QueryRouter.RouteQueryAsync()` to pass `result.ProjectionType` to the `QueryRouterResult`:
        - Line ~62: `return new QueryRouterResult(Success: true, Payload: result.Payload, NotFound: false, ProjectionType: result.ProjectionType);`
    - [x] 2.5 Update `SubmitQueryHandler.Handle()` to pass `routerResult.ProjectionType` to `SubmitQueryResult`:
        - Line ~40: `return new SubmitQueryResult(request.CorrelationId, routerResult.Payload.Value, routerResult.ProjectionType);`

- [x] Task 3: [CORE] CachingProjectionActor runtime projection type discovery (AC: #3, #4)
    - [x] 3.1 Add field `private string? _discoveredProjectionType;` to `CachingProjectionActor` (alongside existing `_cachedETag` and `_cachedPayload`).
    - [x] 3.2 Create helper method to get the effective projection type:
        ```csharp
        private string GetEffectiveProjectionType(string fallbackDomain)
            => _discoveredProjectionType ?? fallbackDomain;
        ```
    - [x] 3.3 In `QueryAsync()`, change the ETag lookup from `envelope.Domain` to `GetEffectiveProjectionType(envelope.Domain)`:
        ```csharp
        string? currentETag = await eTagService
            .GetCurrentETagAsync(GetEffectiveProjectionType(envelope.Domain), envelope.TenantId)
            .ConfigureAwait(false);
        ```
    - [x] 3.3a Add runtime validation for `ProjectionType` before storing. Reject values that contain `:` (colon — actor ID separator) or exceed 100 characters (prevents bloated base64url in ETag headers). On invalid value, log warning and fall back to `envelope.Domain`:
        ```csharp
        private static bool IsValidProjectionType(string projectionType)
            => projectionType.Length <= 100 && !projectionType.Contains(':');
        ```
    - [x] 3.4 After `ExecuteQueryAsync()` returns successfully, store `ProjectionType` ONLY on first discovery (when `_discoveredProjectionType` is still null). If a subsequent call returns a different value, log a warning but do NOT change the cached value — this prevents flip-flopping microservices from destabilizing the cache:

        ```csharp
        if (result.Success && currentETag is not null)
        {
            // Runtime projection type discovery (FR63) — store ONLY on first discovery
            if (!string.IsNullOrWhiteSpace(result.ProjectionType))
            {
                if (_discoveredProjectionType is null)
                {
                    _discoveredProjectionType = result.ProjectionType;
                    Log.ProjectionTypeDiscovered(logger, envelope.CorrelationId, Id.GetId(), result.ProjectionType);
                }
                else if (!string.Equals(_discoveredProjectionType, result.ProjectionType, StringComparison.Ordinal))
                {
                    Log.ProjectionTypeMismatch(logger, envelope.CorrelationId, Id.GetId(),
                        _discoveredProjectionType, result.ProjectionType);
                    // DO NOT update — first discovery wins until actor deactivation
                }
            }

            _cachedPayload = result.Payload.Clone();
            _cachedETag = currentETag;
            Log.CacheMiss(logger, ...);
        }
        ```

    - [x] 3.5 Add log message for projection type discovery: `Log.ProjectionTypeDiscovered(logger, correlationId, actorId, projectionType)` — LogLevel.Debug, EventId 1074.
    - [x] 3.6 Add log message for projection type mismatch: `Log.ProjectionTypeMismatch(logger, correlationId, actorId, cachedProjectionType, newProjectionType)` — LogLevel.Warning, EventId 1075. This fires if a microservice returns a different ProjectionType on a subsequent call — indicates a bug in the microservice, not in EventStore.
    - [x] 3.7 **NO OnDeactivateAsync override needed.** DAPR creates a fresh actor instance on re-activation after idle timeout — fields reset to default (null) automatically.
    - [x] 3.8 Update XML-doc on `ExecuteQueryAsync` to guide microservice developers. Add a `<remarks>` block: "Set `ProjectionType` on the returned `QueryResult` for optimal ETag caching. If `ProjectionType` is null, the actor falls back to `envelope.Domain` for ETag lookups — which is correct only when domain name equals projection type. Use the same short kebab-case name as your `IQueryContract.ProjectionType`."

- [x] Task 4: [CORE] Handle ProjectionType re-discovery after ETag fetch (AC: #3)
    - [x] 4.1 IMPORTANT EDGE CASE: On the very first cold call, `_discoveredProjectionType` is null, so the ETag lookup uses `envelope.Domain`. After `ExecuteQueryAsync` returns, we learn the real `ProjectionType`. If it differs from `envelope.Domain`, the ETag we just fetched may be for the wrong projection. In this case, the actor should NOT cache the result — the next request will use the correct projection type for the ETag lookup.
    - [x] 4.2 Implement the edge case handling. The key insight: on first cold call when `_discoveredProjectionType` is null, the ETag was fetched using `envelope.Domain`. If the microservice returns a DIFFERENT projection type, that ETag may be wrong — so skip caching this once. The `_discoveredProjectionType` is set BEFORE the early return so the second call uses the correct projection type:

        ```csharp
        // After ExecuteQueryAsync, before normal caching...
        if (result.Success && !string.IsNullOrWhiteSpace(result.ProjectionType)
            && IsValidProjectionType(result.ProjectionType))
        {
            if (_discoveredProjectionType is null)
            {
                bool projectionTypeDiffersFromDomain =
                    !string.Equals(result.ProjectionType, envelope.Domain, StringComparison.Ordinal);

                _discoveredProjectionType = result.ProjectionType; // SET before early return
                Log.ProjectionTypeDiscovered(logger, envelope.CorrelationId, Id.GetId(), result.ProjectionType);

                if (projectionTypeDiffersFromDomain)
                {
                    // ETag was fetched using envelope.Domain — may be wrong projection.
                    // Don't cache. Next request will use correct _discoveredProjectionType.
                    return result;
                }
            }
        }

        // Normal caching logic follows (ETag was fetched with correct projection type)...
        ```

    - [x] 4.3 On second call after discovery, `_discoveredProjectionType` is already set → ETag lookup uses correct projection type → cache works normally.

- [x] Task 5: [ENDPOINT] QueriesController uses runtime ProjectionType (AC: #6)
    - [x] 5.1 Modify the post-query ETag fetch block in `QueriesController.Submit()` (currently lines 122-131). Change from `request.Domain` to non-empty `result.ProjectionType`, otherwise fall back to `request.Domain`:
        ```csharp
        if (currentETag is null)
        {
            string projectionTypeForETag = string.IsNullOrWhiteSpace(result.ProjectionType)
                ? request.Domain
                : result.ProjectionType;
            try
            {
                currentETag = await eTagService
                    .GetCurrentETagAsync(projectionTypeForETag, request.Tenant, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Fail-open: no ETag header on response is acceptable
            }
        }
        ```
    - [x] 5.2 The `result` variable comes from `SubmitQueryResult`. Extract it from the mediator response:

        ```csharp
        SubmitQueryResult result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        // Use runtime-discovered projection type for ETag response header
        if (currentETag is null)
        {
            string projectionTypeForETag = string.IsNullOrWhiteSpace(result.ProjectionType)
                ? request.Domain
                : result.ProjectionType;
            // ... rest of fetch
        }
        ```

    - [x] 5.3 Update the inline comment from "Uses request.Domain as projection type" to "Uses runtime-discovered projection type (FR63), falls back to request.Domain".

- [x] Task 6: [SAMPLE] Verify sample compatibility (AC: #8)
    - [x] 6.1 The sample `CounterQueryService` (Blazor UI) makes HTTP POST to `/api/v1/queries` and reads the response JSON + ETag header. **No changes needed** — the wire format (`SubmitQueryResponse`) does not include `ProjectionType` (it's server-internal).
    - [x] 6.2 The sample does NOT implement `CachingProjectionActor` directly — it's a Blazor UI client. No sample changes needed for this story.
    - [x] 6.3 Verify sample still builds and runs correctly after QueryResult changes.

- [x] Task 7: [TESTS] Add and update tests (AC: #1-#8)
    - [x] 7.1 Add unit test for `IQueryResponse<T>`: create a concrete test implementation, verify it compiles with mandatory `ProjectionType`.
    - [x] 7.2 Update `TestCachingProjectionActor` in `CachingProjectionActorTests.cs` to return `QueryResult` with `ProjectionType` set.
    - [x] 7.3 Add test: **Runtime discovery** — cold call returns `ProjectionType="order-list"`, verify second call's ETag lookup uses `"order-list"` not `envelope.Domain`.
    - [x] 7.4 Add test: **Discovery mismatch skips cache** — first cold call uses `envelope.Domain="orders"` for ETag, discovers `ProjectionType="order-list"`. First call should NOT be cached. Second call should use correct projection type.
    - [x] 7.5 Add test: **Null ProjectionType falls back to envelope.Domain** — `QueryResult.ProjectionType=null`, verify ETag lookup uses `envelope.Domain` (backward compatibility).
    - [x] 7.6 Add test: **Empty ProjectionType falls back** — `QueryResult.ProjectionType=""`, verify fallback to `envelope.Domain`.
    - [x] 7.7 Update existing `CachingProjectionActorTests` — existing tests should continue to pass with `ProjectionType=null` in their QueryResults (backward compat default).
    - [x] 7.8 Add `QueriesControllerTests` test: query response with `ProjectionType="order-list"` → ETag response header fetched using `"order-list"` not `request.Domain`.
    - [x] 7.9 Add `QueriesControllerTests` test: query response with `ProjectionType=null` → ETag response header fetched using `request.Domain` (fallback).
    - [x] 7.10 Add `QueryRouterTests` test (or update existing): verify `ProjectionType` passes through from `QueryResult` to `QueryRouterResult`.
    - [x] 7.11 Add `SubmitQueryHandlerTests` test (or update existing): verify `ProjectionType` passes through from `QueryRouterResult` to `SubmitQueryResult`.
    - [x] 7.12 Verify `IQueryResponse<T>` covariance compiles: `IQueryResponse<DerivedDto> → IQueryResponse<BaseDto>`.
    - [x] 7.13 Add **DataContract serialization backward compat test**: serialize a `QueryResult` WITHOUT `ProjectionType` (simulate old actor binary), deserialize → verify `ProjectionType` is null (not throw).
    - [x] 7.14 Add test: **ProjectionType with colon rejected** — `QueryResult.ProjectionType="evil:type"`, verify `CachingProjectionActor` falls back to `envelope.Domain` for ETag lookup (colon is actor ID separator, must be rejected).
    - [x] 7.15 Add test: **ProjectionType exceeding 100 chars rejected** — verify fallback to `envelope.Domain`.
    - [x] 7.16 Add test: **Flip-flopping ProjectionType** — first call returns `ProjectionType="order-list"`, second call returns `ProjectionType="order-summary"`. Verify `_discoveredProjectionType` stays `"order-list"` (first discovery wins) and a warning is logged.
    - [ ] 7.17 Add **Tier 2 integration test** (if feasible within existing DAPR slim init infrastructure): end-to-end domain≠projectionType scenario — submit a query where `Domain="orders"` but the projection actor returns `ProjectionType="order-list"`. Verify the ETag response header encodes `order-list` (not `orders`) in its self-routing prefix. This validates the full pipeline: CachingProjectionActor → QueryRouter → SubmitQueryHandler → QueriesController.

- [x] Task 8: [VERIFICATION] Build and regression verification (AC: #7, #8)
    - [x] 8.1 Full solution build: `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
    - [x] 8.2 Tier 1 tests: all pass
    - [x] 8.3 Tier 2 tests: all pass
    - [x] 8.4 Verify Blazor sample still builds (no wire format change for clients)

## Dev Notes

### Dependencies

- **Story 18-7 (self-routing ETags):** Must be merged/complete. Self-routing ETag format (`{base64url(projectionType)}.{guid}`) is required for the runtime-discovered projection type to have value — the endpoint needs to decode projection type from the client's ETag and also set the correct projection type in the response ETag. Story 18-7 status: `review`.
- **Stories 18-1 through 18-6:** All done. ETag actor, query routing, caching, SignalR, and sample UI all in place.
- **No external dependencies.** All changes are internal to the Server and Contracts packages.

### Architecture Patterns and Constraints

- **Fail-open invariant.** All ETag-related failures → cache miss, never error. If `ProjectionType` is null/empty, fall back to `envelope.Domain`. NEVER throw for missing/invalid projection type.
- **Coarse invalidation model unchanged.** ETag is still per `{ProjectionType}:{TenantId}`. This story changes which projection type is used for lookup — not the granularity.
- **Actor ID format unchanged.** Projection actor ID remains 3-tier (`QueryType:Tenant:EntityId|Checksum`). ETag actor ID remains `ProjectionType:Tenant`.
- **DataContract serialization.** `QueryResult` uses `[DataContract]`/`[DataMember]` for DAPR actor proxy serialization. New `ProjectionType` field MUST have `[DataMember]` attribute. Default `null` ensures backward compatibility with existing actors that don't set it.
- **No state store for projection type mapping.** `_discoveredProjectionType` is in-memory only (same as `_cachedETag` and `_cachedPayload`). On actor deactivation/reactivation, it's reset to null. This is by design — the mapping is always re-learned from the microservice on cold call.
- **Projection type isolation.** Colons (`:`) are forbidden in projection type names (actor ID separator). Enforce via validation or documentation.
- **No changes to `IETagService` or `IETagActor` interfaces.** `GetCurrentETagAsync(projectionType, tenantId)` works exactly the same — we're just passing in the correct projection type now.
- **`IQueryContract.ProjectionType` vs `IQueryResponse.ProjectionType` consistency.** These are defined in different packages (Contracts vs Server runtime) and not enforced to match. `IQueryContract.ProjectionType` is compile-time metadata used by the Client's `QueryContractResolver`. `IQueryResponse.ProjectionType` is runtime metadata used by the Server's `CachingProjectionActor`. Microservice developers SHOULD ensure they return the same value from both — but EventStore does not enforce this. Document this as a microservice developer responsibility in the XML-doc guidance.
- **One query type → one projection type per actor instance.** The 3-tier routing model creates actor IDs as `{QueryType}:{TenantId}[:{EntityId|Checksum}]`. Each actor instance serves a single query type + tenant combination. The first-discovery-wins rule for `_discoveredProjectionType` assumes the microservice always returns the same `ProjectionType` for a given query type. If a microservice returns different projection types based on payload content, only the first value is used until actor deactivation.

### Current Code State (Must Understand Before Changing)

**QueryResult** (`src/Hexalith.EventStore.Server/Actors/QueryResult.cs`):

```csharp
[DataContract]
public record QueryResult(
    [property: DataMember] bool Success,
    [property: DataMember] JsonElement Payload,
    [property: DataMember] string? ErrorMessage = null);
// → ADD: [property: DataMember] string? ProjectionType = null
```

**CachingProjectionActor** (`src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`):

- Line 35-37: `eTagService.GetCurrentETagAsync(envelope.Domain, envelope.TenantId)` — **THIS IS THE KEY CHANGE**: use `GetEffectiveProjectionType(envelope.Domain)` instead
- Line 47: `ExecuteQueryAsync(envelope)` returns `QueryResult` — after this, extract `ProjectionType`
- Line 49-55: Caching block — add `_discoveredProjectionType` storage here
- Fields: `_cachedETag`, `_cachedPayload` — add `_discoveredProjectionType`

**QueriesController** (`src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs`):

- Lines 122-131: Post-query ETag fetch uses `request.Domain` — **CHANGE to use non-empty `result.ProjectionType`, otherwise fall back to `request.Domain`**
- The `result` variable is `SubmitQueryResult` (from mediator.Send)

**QueryRouter** (`src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`):

- Line 62: Returns `new QueryRouterResult(Success: true, Payload: result.Payload, ...)` — add `ProjectionType: result.ProjectionType`

**SubmitQueryHandler** (`src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs`):

- Line 40: Returns `new SubmitQueryResult(request.CorrelationId, routerResult.Payload.Value)` — add `routerResult.ProjectionType`

**QueryRouterResult** (`src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs`):

```csharp
public record QueryRouterResult(bool Success, JsonElement? Payload, bool NotFound, string? ErrorMessage = null);
// → ADD: string? ProjectionType = null
```

**SubmitQueryResult** (`src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs`):

```csharp
public record SubmitQueryResult(string CorrelationId, JsonElement Payload);
// → ADD: string? ProjectionType = null
```

### Critical Anti-Patterns to Avoid

1. **DO NOT make ProjectionType required (non-nullable) on QueryResult.** It must be `string?` with default `null` for backward compatibility. Existing projection actors that don't set it must continue to work.
2. **DO NOT change IProjectionActor.QueryAsync signature.** The interface stays `Task<QueryResult> QueryAsync(QueryEnvelope envelope)`. The change is internal to `QueryResult`.
3. **DO NOT change CachingProjectionActor.ExecuteQueryAsync signature.** It stays `Task<QueryResult> ExecuteQueryAsync(QueryEnvelope envelope)`. Implementers set `ProjectionType` on their returned `QueryResult`.
4. **DO NOT persist `_discoveredProjectionType` to DAPR state store.** In-memory only.
5. **DO NOT throw if ProjectionType is null or empty.** Fall back to `envelope.Domain` gracefully — log, don't crash.
6. **DO NOT modify SignalR, notification, or ETag actor code.** This story only changes how projection types are discovered and used for ETag lookups.
7. **DO NOT modify SubmitQueryResponse wire format.** The `ProjectionType` is server-internal; clients don't see it. `SubmitQueryResponse(CorrelationId, Payload)` stays unchanged.
8. **DO NOT add ProjectionType to QueryEnvelope.** The envelope carries request data. ProjectionType comes from the response.
9. **DO NOT add a constructor or factory method to IQueryResponse<T>.** It's a pure interface — microservice developers choose their own implementation pattern.
10. **DO NOT overwrite `_discoveredProjectionType` after first discovery.** Only set it when it's null. If the microservice returns a different value on subsequent calls, log a warning but keep the first value. This prevents flip-flopping microservices from destabilizing the cache.
11. **DO NOT accept ProjectionType containing `:` (colon).** Colons are actor ID separators. A colon in the projection type would corrupt the ETag actor ID (`{ProjectionType}:{TenantId}`). Validate and reject at runtime.
12. **DO NOT accept ProjectionType longer than 100 characters.** Long projection types produce bloated base64url-encoded ETag HTTP headers. Validate and reject at runtime.

### Previous Story Intelligence (from 18-7)

- **Self-routing ETag format** `{base64url(projectionType)}.{guid}` — story 18-7 changed ETag values but Gate 2 (`CachingProjectionActor`) still uses `envelope.Domain` for ETag lookups. THIS story fixes that.
- **`SelfRoutingETag` utility class** in `Server/Queries/` — encode/decode already done. No changes needed.
- **`InternalsVisibleTo`** already added for CommandApi, Server.Tests, Testing, Testing.Tests assemblies (added in 18-7).
- **`ETagMatches()` span-based parser** — do not touch. It compares full ETag strings regardless of format.
- **`AnalyzeHeaderProjectionTypes()`** — extracts projection type from client's `If-None-Match` header. This works for Gate 1 (inbound). Story 18-8 fixes Gate 2 and the response path.
- **FakeETagActor** generates self-routing ETags with configurable `ProjectionType` property.
- **All 1,821+ tests pass** as of 18-7 completion.
- **Blazor Server rendering mode** — server-side HttpClient calls. Not relevant to this story.

### Git Intelligence

Recent commits show:

- Story 18-7 delivered self-routing ETags (ETagActor, QueriesController, SelfRoutingETag utility)
- Stories 18-5 and 18-6 delivered SignalR + Blazor UI patterns
- All work follows: actor-based architecture, DAPR state, fail-open error handling, DataContract serialization
- Test patterns: NSubstitute mocks, Shouldly assertions, `ActorHost.CreateForTest<T>()` for actor tests

### Existing Files to Modify

| File                                                                           | Change                                                                         |
| ------------------------------------------------------------------------------ | ------------------------------------------------------------------------------ |
| `src/Hexalith.EventStore.Server/Actors/QueryResult.cs`                         | Add `string? ProjectionType = null` DataMember field                           |
| `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`              | Add `_discoveredProjectionType` field, use for ETag lookup, store on cold call |
| `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs`                  | Add `string? ProjectionType = null` field                                      |
| `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`                        | Pass `result.ProjectionType` through to `QueryRouterResult`                    |
| `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs`               | Add `string? ProjectionType = null` to `SubmitQueryResult`                     |
| `src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs`                | Pass `routerResult.ProjectionType` through to `SubmitQueryResult`              |
| `src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs`          | Use non-empty `result.ProjectionType`, otherwise fall back to `request.Domain` |
| `tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs` | Update `TestCachingProjectionActor`, add runtime discovery tests               |
| `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs` | Add tests for ProjectionType passthrough to ETag response                      |

### New Files to Create

| File                                                          | Purpose                                                       |
| ------------------------------------------------------------- | ------------------------------------------------------------- |
| `src/Hexalith.EventStore.Contracts/Queries/IQueryResponse.cs` | `IQueryResponse<T>` compile-time enforcement interface (FR62) |

### Project Structure Notes

- `IQueryResponse<T>` goes in `Contracts/Queries/` alongside `IQueryContract.cs` — both are compile-time contract interfaces
- All pipeline record changes are in existing files — no new Server files needed
- No NuGet package changes — `IQueryResponse<T>` is in the already-published Contracts package
- No new project references needed

### Brainstorming Compliance

This story implements **Priority 2 (Runtime Projection Discovery)** from the brainstorming session extension 2 (2026-03-13). It completes the self-routing ETag architecture: Story 18-7 encoded projection type in the ETag value, and this story ensures the correct projection type is used for all ETag lookups and response headers. Priority 3 (Remove ProjectionType from Client Contract) is a future enhancement — `IQueryContract.ProjectionType` remains as a compile-time convenience but is no longer the sole source of truth for runtime routing.

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Story 8.1: Query Contract Library & IQueryResponse Compile-Time Enforcement]
- [Source: _bmad-output/planning-artifacts/epics.md — Story 8.2: Query Actor Routing & Runtime Projection Type Discovery]
- [Source: _bmad-output/planning-artifacts/epics.md — FR62: IQueryResponse<T> compile-time ProjectionType enforcement]
- [Source: _bmad-output/planning-artifacts/epics.md — FR63: Query actor discovers projection type at runtime from first cold call]
- [Source: _bmad-output/planning-artifacts/epics.md — FR64: Short projection type name guidance]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-13.md — Section 4.3: Story 18-8 scope and dependencies]
- [Source: _bmad-output/implementation-artifacts/18-7-self-routing-etag-format-and-endpoint-decode.md — Previous story learnings, current ETag implementation]
- [Source: src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs — Current Gate 2 ETag lookup using envelope.Domain]
- [Source: src/Hexalith.EventStore.Server/Actors/QueryResult.cs — Current QueryResult record without ProjectionType]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs — Current post-query ETag fetch using request.Domain]
- [Source: src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs — Existing compile-time ProjectionType on query contracts]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

Code review follow-up fixes applied: warm-cache projection type propagation, empty/invalid ProjectionType fallback, real DataContract backward-compat regression test, and story/task tracking correction.

### Completion Notes List

- Created `IQueryResponse<T>` interface in Contracts with covariant `out T` and mandatory `ProjectionType` (FR62)
- Added `string? ProjectionType = null` to `QueryResult` (DataMember), `QueryRouterResult`, and `SubmitQueryResult` — all backward compatible with null defaults
- Implemented runtime projection type discovery in `CachingProjectionActor`: first-discovery-wins pattern with `_discoveredProjectionType` field
- Handled cold-call edge case (Task 4): when discovered projection type differs from `envelope.Domain`, first call skips caching to avoid storing ETag from wrong projection
- Added validation: reject `ProjectionType` containing `:` (colon) or exceeding 100 characters
- Updated `QueriesController` to use runtime-discovered projection type only when non-empty, otherwise fall back to `request.Domain` (FR63)
- Added 2 log messages: `ProjectionTypeDiscovered` (EventId 1074, Debug) and `ProjectionTypeMismatch` (EventId 1075, Warning)
- Added warning logging for rejected projection types and preserved discovered projection type on warm cache hits
- Threaded `ProjectionType` through full pipeline: `QueryRouter` and `SubmitQueryHandler` passthrough
- Sample compatibility verified: no changes needed (wire format unchanged)
- Added regression coverage for warm-cache ProjectionType propagation, empty ProjectionType endpoint fallback, and real DataContract backward compatibility
- No Tier 2 integration test added for domain!=projectionType (would need a custom test projection actor registered with DAPR — deferred to manual verification)

### Change Log

- 2026-03-13: Story 18-8 implementation complete — IQueryResponse<T> compile-time enforcement, runtime projection type discovery, pipeline passthrough, QueriesController ETag response update
- 2026-03-13: Code review fixes — preserved ProjectionType on cache hits, corrected empty ProjectionType fallback, added real backward-compat test, and corrected deferred Tier 2 task tracking

### File List

#### New Files

- `src/Hexalith.EventStore.Contracts/Queries/IQueryResponse.cs` — `IQueryResponse<T>` interface (FR62)
- `tests/Hexalith.EventStore.Contracts.Tests/Queries/IQueryResponseTests.cs` — compile-time enforcement and covariance tests

#### Modified Files

- `src/Hexalith.EventStore.Server/Actors/QueryResult.cs` — added `string? ProjectionType = null` DataMember field
- `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs` — runtime projection type discovery, `_discoveredProjectionType` field, `GetEffectiveProjectionType()`, `IsValidProjectionType()`, new log messages
- `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs` — added `string? ProjectionType = null`
- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` — passes `result.ProjectionType` to `QueryRouterResult`
- `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs` — added `string? ProjectionType = null` to `SubmitQueryResult`
- `src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs` — passes `routerResult.ProjectionType` to `SubmitQueryResult`
- `src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs` — uses non-empty `result.ProjectionType`, otherwise falls back to `request.Domain` for ETag response fetch
- `tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs` — runtime discovery, mismatch, warm-cache ProjectionType propagation, fallbacks, validation, flip-flopping, backward compat
- `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs` — 2 new tests (ProjectionType ETag routing, null fallback)
- `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs` — 2 new tests (ProjectionType passthrough)
- `tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs` — 2 new tests (ProjectionType passthrough)
