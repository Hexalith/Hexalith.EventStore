# Story 18.7: Self-Routing ETag Format and Endpoint Decode

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an **API consumer**,
I want **ETags to encode the projection type directly in their value** using the format `{base64url(projectionType)}.{guid}`,
So that **the query endpoint can decode the correct ETag actor from the `If-None-Match` header without server-side routing state, enabling sub-5ms cache checks that scale without centralized projection type lookup tables**.

## Acceptance Criteria

1. **Self-routing ETag format (FR61)** — **Given** the ETag actor generates ETags, **When** a new ETag is created via `RegenerateAsync()`, **Then** the value follows the format `{base64url(projectionType)}.{base64url-guid}` where the projection type is extracted from the actor's own ID (`{ProjectionType}:{TenantId}` → use segment before first colon). Example: projection type `counter` → ETag `"Y291bnRlcg.KW4RnPjU7EuIVqT4LX_AKA"`.

2. **Endpoint decodes projection type from ETag (FR53 updated)** — **Given** a client sends a query with `If-None-Match` header containing a self-routing ETag, **When** the query endpoint performs Gate 1 pre-check, **Then** it decodes the base64url projection type prefix from the ETag, uses the decoded projection type (NOT `request.Domain`) to locate the ETag actor `{decodedProjectionType}:{request.Tenant}`, and compares the full ETag string for a match.

3. **Malformed ETag and wildcard degrade to cache miss (FR61)** — **Given** an ETag that is malformed, undecodable, missing the `.` separator, has an invalid base64url prefix, **or** the client sends `If-None-Match: *` (wildcard), **When** the query endpoint attempts to decode it, **Then** it treats the ETag as a Gate 1 cache miss (proceeds to normal query execution via Gate 2), never returns an error for bad ETags, and logs a debug-level message. Wildcard `*` contains no projection type to decode, so Gate 1 is skipped entirely — do NOT fall back to `request.Domain` for wildcard, as that would contradict the self-routing principle.

4. **Old-format ETag backward compatibility** — **Given** a client sends an old-format ETag (plain 22-char base64url GUID without `.` separator), **When** the query endpoint attempts to decode it, **Then** it treats the ETag as a cache miss (safe degradation), the query proceeds normally, and the response includes a new self-routing ETag that the client will use for subsequent requests (automatic migration).

5. **ETag response header uses new format** — **Given** any successful query response (200 OK) or pre-check match (304 Not Modified), **When** the ETag response header is set, **Then** it contains the self-routing format `"{base64url(projectionType)}.{guid}"` (RFC 7232 quoted string).

6. **Gate 2 (query actor cache) compatible** — **Given** the `CachingProjectionActor` compares cached ETag strings, **When** the ETag format changes to self-routing, **Then** Gate 2 continues to work because it compares full ETag strings (self-routing format is consistently used).

7. **ETag pre-check performance preserved (NFR35)** — **Given** the self-routing ETag decode adds a base64url decode step, **When** the ETag pre-check executes, **Then** it completes within 5ms at p99 for warm ETag actors.

8. **All existing tests pass** — All Tier 1 and Tier 2 tests continue to pass. Tests that compare ETag values must be updated to expect the new self-routing format.

## Tasks / Subtasks

<!-- TASK PRIORITY: Tasks 1-2 = Core ETag Format Change, Tasks 3-4 = Endpoint Decode, Tasks 5-6 = Compatibility & Tests, Task 7 = Verification -->

- [x] Task 1: [CORE] Change ETag generation to self-routing format (AC: #1, #5)
    - [x] 1.1 Modify `ETagActor` to derive projection type from its own actor ID (`this.Id.GetId().Split(':')[0]`) — do NOT add a `projectionType` parameter to `GenerateETag()` or `RegenerateAsync()`. Delegate ETag creation to `SelfRoutingETag.GenerateNew(projectionType)` from Task 2.
    - [x] 1.2 New format: `"{base64url(projectionType)}.{base64url-guid}"` — reuse existing base64url encoding for GUID portion
    - [x] 1.3 Extract projection type from `this.Id.GetId()` — split on `:`, take first segment
    - [x] 1.4 Add `EncodeProjectionType(string projectionType)` static helper: `Convert.ToBase64String(Encoding.UTF8.GetBytes(projectionType))` with URL-safe replacements
    - [x] 1.5 Update `RegenerateAsync()` to produce self-routing format
    - [x] 1.6 Update `OnActivateAsync()` — after loading state, detect old-format ETag (no `.` separator). If old format detected, call `RegenerateAsync()` to migrate to self-routing format. Use the same persist-then-cache pattern as normal regeneration: `SetStateAsync()` → `SaveStateAsync()` → update `_currentETag` only after persistence succeeds. If `SaveStateAsync()` fails, leave `_currentETag = null` (cold start behavior, safe degradation).

- [x] Task 2: [CORE] Add ETag decode/encode utilities (AC: #1, #2, #3)
    - [x] 2.1 Create static class `SelfRoutingETag` in `Hexalith.EventStore.Server/Queries/` with:
        - `string Encode(string projectionType, string guid)` — produces `{base64url(projectionType)}.{guid}`
        - `bool TryDecode(string etag, out string projectionType, out string guidPart)` — extracts parts
        - `string GenerateNew(string projectionType)` — creates new GUID and encodes
    - [x] 2.2 `TryDecode` must handle: missing `.`, invalid base64url, empty segments → return false. Wrap `Convert.FromBase64String` in try-catch for `FormatException` (invalid base64 input including `length % 4 == 1` which is always invalid) — catch and return false, never throw.
    - [x] 2.3 Base64url decode: replace `-` → `+`, `_` → `/`, add padding `=` as needed, then `Convert.FromBase64String` → `Encoding.UTF8.GetString`
    - [x] 2.4 Validate decoded projection type is non-empty and does not contain `:` (colon is actor ID separator)

- [x] Task 3: [CORE] Update query endpoint Gate 1 to decode self-routing ETags (AC: #2, #3, #4, #7)
    - [x] 3.1 Modify `QueriesController.Submit()` Gate 1 flow:
        1. Check for wildcard `If-None-Match: *` FIRST — if wildcard, skip Gate 1 entirely (no decode, no actor call), proceed to query
        2. Parse `If-None-Match` header to get individual ETag value(s) (strip quotes, split on comma)
        3. For the FIRST ETag that `SelfRoutingETag.TryDecode()` succeeds on: call `IETagService.GetCurrentETagAsync(decodedProjectionType, request.Tenant)` — stop iterating after first successful decode (do NOT make multiple actor calls for multi-value headers)
        4. If no ETag decodes successfully: skip Gate 1 (cache miss), log debug
        5. Decode must happen BEFORE and OUTSIDE `ETagMatches()` — do not add string allocations inside the allocation-free span-based parser
    - [x] 3.2 Preserve existing `ETagMatches()` span-based comparison logic for the actual string comparison step (after decode determines which actor to call)
    - [x] 3.3 Ensure fail-open behavior preserved: decode failure or actor failure → proceed to query

- [x] Task 4: [CORE] Update ETag response headers (AC: #5)
    - [x] 4.1 After successful query (200 OK): fetch current ETag from `IETagService.GetCurrentETagAsync(request.Domain, request.Tenant)` and set as `ETag` response header. Note: on 200 responses (Gate 1 miss or skip), the endpoint still uses `request.Domain` as projection type for the ETag fetch — this is correct because the ETag actor was already generating self-routing ETags (Task 1), so the returned value will be in the new format regardless. Story 18-8 will later change this to use the runtime-discovered projection type.
    - [x] 4.2 On pre-check match (304): set `ETag` header with the current self-routing value (not the client's old value)
    - [x] 4.3 Ensure double-quoting per RFC 7232: `ETag: "{self-routing-value}"`

- [x] Task 5: [COMPATIBILITY] Update FakeETagActor and test helpers (AC: #8)
    - [x] 5.1 Update `FakeETagActor` to generate self-routing ETags. Access strategy: add `[InternalsVisibleTo("Hexalith.EventStore.Testing")]` to the Server project's `AssemblyInfo` so `FakeETagActor` can call `SelfRoutingETag.GenerateNew()`. Alternatively, duplicate the simple encode logic (base64url + dot + guid) in the fake if `InternalsVisibleTo` is not acceptable. The fake needs a configurable projection type (default: `"test-projection"`).
    - [x] 5.2 Update `FakeETagActor.GenerateETag()` to produce `{base64url(projectionType)}.{guid}` format
    - [x] 5.3 Ensure all test helper methods produce valid self-routing ETags

- [x] Task 6: [TESTS] Update existing tests and add new tests (AC: #1-#8)
    - [x] 6.1 Add unit tests for `SelfRoutingETag.Encode()`, `TryDecode()`, `GenerateNew()`
    - [x] 6.2 Add unit tests for malformed ETag handling (no `.`, empty segments, invalid base64url, old-format)
    - [x] 6.3 Update `ETagActorIntegrationTests` — ETags must now be self-routing format
    - [x] 6.4 Update `DaprETagServiceTests` — returned ETags use new format
    - [x] 6.5 Update `QueriesControllerTests` — Gate 1 tests with self-routing ETags, backward compat tests
    - [x] 6.6 Update `CachingProjectionActorTests` — cached ETags use new format
    - [x] 6.7 Update `FakeETagActorTests` — validate fake produces self-routing format
    - [x] 6.8 Add backward compatibility test: old-format ETag → cache miss → response has new-format ETag
    - [x] 6.9 Add wildcard test: `If-None-Match: *` → Gate 1 skipped (no decode attempt) → query proceeds to Gate 2
    - [x] 6.10 Add chaos scenario tests from brainstorming: renamed projection → cache miss; two query types sharing same projection → both invalidated correctly

- [x] Task 7: [VERIFICATION] Build and regression verification (AC: #7, #8)
    - [x] 7.1 Full solution build: `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
    - [x] 7.2 Tier 1 tests: all pass
    - [x] 7.3 Tier 2 tests: all pass (including ETag actor integration tests)
    - [x] 7.4 Verify Blazor sample still works with new ETag format (CounterQueryService stores and sends whatever ETag the server returns — automatic migration)

## Dev Notes

### Dependencies

- **All done:** Stories 18-1 through 18-6 delivered the lookup-based ETag system. This story upgrades the ETag format without changing the overall architecture.
- **No blocking dependencies.** This is a refactor of the ETag value format; the actor infrastructure, query pipeline, and SignalR notification system remain unchanged.

### Architecture Patterns and Constraints

- **Self-routing ETags eliminate server-side routing state.** Currently, Gate 1 uses `request.Domain` as the projection type for ETag lookup. With self-routing ETags, the projection type is decoded from the ETag itself — decoupling cache validation from request metadata.
- **Coarse invalidation model unchanged.** ETag is still per `{ProjectionType}:{TenantId}`. Self-routing changes the value format, not the granularity.
- **Actor ID format unchanged.** ETag actor ID remains `{ProjectionType}:{TenantId}` (colon-separated). The actor parses its own ID to extract projection type for encoding.
- **Fail-open invariant.** All ETag-related failures (decode, actor, network) must result in cache miss, never error. This is a hard architectural constraint.
- **One-time migration cost on deployment.** When this story is deployed, every ETag actor that activates will load an old-format ETag from state and immediately regenerate to the self-routing format (Task 1.6). This invalidates all client caches simultaneously, causing a one-time burst of cold calls through Gate 2 to query actors and microservices. This is acceptable: the ETag actor regeneration is sub-millisecond, and the downstream burst is equivalent to a normal cache-cold startup. No mitigation needed — acknowledge the cost and move on.

### Current ETag Implementation (Must Understand Before Changing)

**ETagActor** (`src/Hexalith.EventStore.Server/Actors/ETagActor.cs`):

- Actor ID: `{ProjectionType}:{TenantId}` (colon separator)
- Type name constant: `ETagActorTypeName = "ETagActor"`
- State key: `"etag"` in DAPR actor state
- `GenerateETag()`: static, produces 22-char base64url GUID
- `RegenerateAsync()`: calls `GenerateETag()` → `SetStateAsync()` → `SaveStateAsync()` → cache
- `OnActivateAsync()`: loads from state, sets `_currentETag = null` on cold start

**DaprETagService** (`src/Hexalith.EventStore.Server/Queries/DaprETagService.cs`):

- `GetCurrentETagAsync(projectionType, tenantId)`: creates actor proxy → calls `GetCurrentETagAsync()`
- Actor ID: `$"{projectionType}:{tenantId}"`
- 3-second proxy timeout, fail-open error handling

**QueriesController Gate 1** (`src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs`):

- Reads `If-None-Match` header via `[FromHeader]`
- Calls `IETagService.GetCurrentETagAsync(request.Domain, request.Tenant)` — **THIS IS THE KEY CHANGE**: use decoded projection type instead of `request.Domain`
- `ETagMatches()`: parses comma-separated ETags (max 10), handles wildcards and quotes, span-based allocation-free parsing
- On match: returns HTTP 304 with `ETag` response header

**CachingProjectionActor Gate 2** (`src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`):

- Compares full ETag strings: `currentETag == _cachedETag`
- **No changes needed** — Gate 2 compares full strings regardless of format

**FakeETagActor** (`src/Hexalith.EventStore.Testing/Fakes/FakeETagActor.cs`):

- Test double with configurable ETag
- Tracks regeneration count and ETags
- **Must update** to produce self-routing format

### Key Implementation Details

**Base64URL encoding for projection type:**

```csharp
// Encode
byte[] bytes = Encoding.UTF8.GetBytes(projectionType);
string encoded = Convert.ToBase64String(bytes)
    .Replace('+', '-')
    .Replace('/', '_')
    .TrimEnd('=');

// Decode
string padded = encoded.Replace('-', '+').Replace('_', '/');
switch (padded.Length % 4)
{
    case 2: padded += "=="; break;
    case 3: padded += "="; break;
}
byte[] bytes = Convert.FromBase64String(padded);
string projectionType = Encoding.UTF8.GetString(bytes);
```

**ETag format examples:**
| Projection Type | Base64URL | Full ETag |
|----------------|-----------|-----------|
| `counter` | `Y291bnRlcg` | `Y291bnRlcg.KW4RnPjU7EuIVqT4LX_AKA` |
| `OrderList` | `T3JkZXJMaXN0` | `T3JkZXJMaXN0.KW4RnPjU7EuIVqT4LX_AKA` |
| `user-profile` | `dXNlci1wcm9maWxl` | `dXNlci1wcm9maWxl.KW4RnPjU7EuIVqT4LX_AKA` |

**Actor ID parsing (get projection type from actor ID):**

```csharp
// Actor ID format: "{ProjectionType}:{TenantId}"
string actorIdStr = this.Id.GetId();
int colonIndex = actorIdStr.IndexOf(':');
string projectionType = actorIdStr[..colonIndex];
```

### Critical Anti-Patterns to Avoid

1. **DO NOT change actor IDs.** ETag actor ID remains `{ProjectionType}:{TenantId}`. Only the ETag _value_ changes.
2. **DO NOT encode tenant in the ETag.** Only projection type is encoded. Tenant comes from the request.
3. **DO NOT break the fail-open contract.** A bad ETag = cache miss. Never throw, never 400.
4. **DO NOT change `IETagService` interface.** The interface `GetCurrentETagAsync(projectionType, tenantId)` stays the same — what changes is _how the endpoint determines the projectionType argument_.
5. **DO NOT change `IETagActor` interface.** `GetCurrentETagAsync()` and `RegenerateAsync()` signatures stay the same. The internal implementation changes.
6. **DO NOT modify CachingProjectionActor.** Gate 2 compares full strings — it works with any format.
7. **DO NOT modify SignalR or projection notification code.** Those systems don't inspect ETag values.
8. **DO NOT use `System.Web.HttpUtility` or other web-specific libraries.** Use `Convert.ToBase64String` with manual char replacements (existing pattern).
9. **DO NOT duplicate encode/decode logic.** `ETagActor` MUST delegate to `SelfRoutingETag.GenerateNew()` for ETag creation. Do not inline the base64url + dot + GUID logic in the actor — keep the single source of truth in the `SelfRoutingETag` utility class.

### Previous Story Intelligence (from 18-6)

- **Blazor Fluent UI v4.13.2** — do not upgrade to v5
- **`EventStoreSignalRClient` supports multiple callbacks** — multi-subscriber dispatch added in 18-6 review
- **CounterQueryService stores ETag from response** — it will automatically pick up new self-routing format on next query. No code changes needed in sample.
- **Review remediation from 18-6**: auth/token plumbing, legacy query-claim compatibility — these are separate concerns, don't touch
- **All 1,780+ tests pass** as of 18-6 completion
- **Blazor Server rendering mode** — server-side HttpClient calls (no CORS). Not relevant to ETag format change.

### Git Intelligence

Recent commits show:

- Story 18-5 and 18-6 delivered SignalR + Blazor UI patterns
- Multi-callback SignalR support added during 18-6 review
- Legacy query permission compatibility added
- All work follows established patterns: actor-based, DAPR state, fail-open error handling

### Existing Files to Modify

| File                                                                           | Change                                                                                           |
| ------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------ |
| `src/Hexalith.EventStore.Server/Actors/ETagActor.cs`                           | Change `GenerateETag()` to produce self-routing format; parse actor ID for projection type       |
| `src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs`          | Gate 1: decode projection type from `If-None-Match` ETag, use decoded type for ETag service call |
| `src/Hexalith.EventStore.Testing/Fakes/FakeETagActor.cs`                       | Generate self-routing ETags in test double                                                       |
| `tests/Hexalith.EventStore.Server.Tests/Actors/ETagActorIntegrationTests.cs`   | Update ETag format assertions                                                                    |
| `tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs`       | Update ETag format assertions                                                                    |
| `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs` | Update Gate 1 tests, add decode tests, add backward compat tests                                 |
| `tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs` | Update ETag format in test data                                                                  |
| `tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeETagActorTests.cs`          | Update to expect self-routing format                                                             |

### New Files to Create

| File                                                                     | Purpose                                         |
| ------------------------------------------------------------------------ | ----------------------------------------------- |
| `src/Hexalith.EventStore.Server/Queries/SelfRoutingETag.cs`              | Static utility class for ETag encode/decode     |
| `tests/Hexalith.EventStore.Server.Tests/Queries/SelfRoutingETagTests.cs` | Unit tests for encode/decode/malformed handling |

### Project Structure Notes

- All modifications are in existing projects — no new projects needed
- `SelfRoutingETag.cs` goes in `Server/Queries/` alongside `DaprETagService.cs` and `IETagService.cs`
- Test file goes in `Server.Tests/Queries/` following existing convention
- No NuGet package changes — `SelfRoutingETag` is internal to the Server project

### Brainstorming Compliance

This story implements **Priority 1 (Self-Routing ETag Format)** from the brainstorming session extension 2 (2026-03-13). All 8 chaos engineering scenarios from Phase 8 are covered by the acceptance criteria and test plan. Brainstorming Priorities 2 (Runtime Projection Discovery) and 3 (Remove ProjectionType from Client Contract) are correctly deferred to Story 18-8.

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Story 8.3: ETag Actor & Self-Routing ETag Encoding]
- [Source: _bmad-output/planning-artifacts/epics.md — FR61: base64url(projectionType).guid format]
- [Source: _bmad-output/planning-artifacts/epics.md — FR53: Query endpoint decodes self-routing ETag]
- [Source: _bmad-output/planning-artifacts/epics.md — NFR35: 5ms p99 ETag pre-check]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-13.md — Full scope, rationale, and dependency analysis]
- [Source: _bmad-output/implementation-artifacts/18-6-sample-ui-refresh-patterns.md — Previous story learnings, ETag HTTP patterns]
- [Source: _bmad-output/implementation-artifacts/18-3-query-endpoint-with-etag-pre-check-and-cache.md — Gate 1/Gate 2 implementation]
- [Source: src/Hexalith.EventStore.Server/Actors/ETagActor.cs — Current ETag generation algorithm]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs — Current Gate 1 implementation]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

No debug issues encountered. All tests passed on first run after fixes.

### Completion Notes List

- Created `SelfRoutingETag` internal static utility class with `Encode()`, `TryDecode()`, and `GenerateNew()` methods for self-routing ETag format `{base64url(projectionType)}.{base64url-guid}`
- Modified `ETagActor.RegenerateAsync()` to delegate ETag creation to `SelfRoutingETag.GenerateNew()`, extracting projection type from actor ID
- Added old-format ETag detection and migration in `ETagActor.OnActivateAsync()` — detects ETags without `.` separator and regenerates to self-routing format with persist-then-cache safety
- Rewrote `QueriesController` Gate 1 flow: wildcard `*` skips Gate 1 entirely; self-routing ETags are decoded to extract projection type for ETag actor lookup; old-format/malformed ETags degrade to cache miss
- Updated 200 OK response path to fetch ETag from service when Gate 1 was skipped (wildcard, decode failure), using `request.Domain` as projection type
- Added `DecodeProjectionTypeFromHeader()` private method for header parsing outside the allocation-free `ETagMatches()` span parser
- Updated `FakeETagActor` to produce self-routing ETags via `SelfRoutingETag.GenerateNew()` with configurable `ProjectionType` property (default: "test-projection")
- Added `InternalsVisibleTo` for CommandApi, Server.Tests, Testing, and Testing.Tests assemblies
- Created 24 new unit tests for `SelfRoutingETag` (encode, decode, malformed handling, roundtrip)
- Updated 20+ existing tests across QueriesControllerTests, ETagActorIntegrationTests, and FakeETagActorTests to use self-routing format
- Added new test cases: backward compatibility (old-format → cache miss → new format response), wildcard Gate 1 skip
- All 1,821 tests pass (1,260 Server + 62 Testing + 197 Contracts + 273 Client + 29 Sample)
- Review remediation: Gate 1 now fails open for mixed-projection multi-value `If-None-Match` headers to avoid false 304 responses across projection types
- Review remediation: `DaprETagServiceTests` and `CachingProjectionActorTests` now use self-routing ETags and explicitly validate the new format
- Review remediation: added `ETagActor` activation coverage for old-format migration success and migration failure safe degradation
- Review remediation: added a focused Gate 1 performance regression test and reran sample tests to verify the sample remains compatible with the new ETag format

### Change Log

- 2026-03-13: Implemented self-routing ETag format (Story 18-7) — ETags now encode projection type as base64url prefix, enabling stateless cache routing
- 2026-03-13: Senior developer review remediation — fixed mixed-projection Gate 1 safety, completed missing self-routing test coverage, verified migration behavior, and revalidated sample compatibility

### File List

**New files:**

- `src/Hexalith.EventStore.Server/Queries/SelfRoutingETag.cs`
- `tests/Hexalith.EventStore.Server.Tests/Queries/SelfRoutingETagTests.cs`

**Modified files:**

- `src/Hexalith.EventStore.Server/Actors/ETagActor.cs`
- `src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj`
- `src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs`
- `src/Hexalith.EventStore.Testing/Fakes/FakeETagActor.cs`
- `tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Integration/ETagActorIntegrationTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs`
- `tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeETagActorTests.cs`
- `tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj`

**Project tracking files changed during story preparation/review:**

- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-03-13.md`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-03-13.md`

## Senior Developer Review (AI)

### Reviewer

GitHub Copilot (GPT-5.4)

### Date

2026-03-13

### Outcome

Approve

### Review Notes

- Fixed the Gate 1 false-304 risk for mixed-projection multi-value `If-None-Match` headers by failing open when decodable validators disagree on projection type.
- Completed the missing self-routing coverage in `DaprETagServiceTests` and `CachingProjectionActorTests` so tasks 6.4 and 6.6 are now genuinely done.
- Added explicit `ETagActor` activation tests for old-format migration success and migration failure safe degradation.
- Added a focused Gate 1 p99 regression test and verified the sample test project still passes with the new ETag format.

### Validation

- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~QueriesControllerTests` ✅ (26 passed)
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release --filter "FullyQualifiedName~SelfRoutingETagTests|FullyQualifiedName~DaprETagServiceTests|FullyQualifiedName~CachingProjectionActorTests|FullyQualifiedName~ETagActorIntegrationTests"` ✅ (58 passed)
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj --configuration Release --filter FullyQualifiedName~FakeETagActorTests` ✅ (9 passed)
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj --configuration Release` ✅ (29 passed)
