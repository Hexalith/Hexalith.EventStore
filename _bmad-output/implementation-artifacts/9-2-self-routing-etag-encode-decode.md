# Story 9.2: Self-Routing ETag Encode/Decode

Status: done

## Story

As a platform developer,
I want self-routing ETags that embed the projection type,
so that the query endpoint can extract routing information without additional lookups.

## Context: What Already Exists

This is a **validation and gap-analysis story**, not a greenfield story. The self-routing ETag encoding/decoding, controller ETag pre-check (Gate 1), ETag actor, DaprETagService, and unit tests are already substantially implemented. The work is to **audit every acceptance criterion against the existing code, fill any gaps (especially around RFC 7232 compliance and edge cases), and ensure full test coverage**.

**Why this audit matters:** If ETag encoding has a subtle bug (e.g., non-ASCII projection types break base64url), query caching silently degrades — every query hits the microservice instead of returning 304. No error, no crash, just invisible performance regression violating NFR35 (5ms p99 ETag pre-check). This audit prevents that.

### Existing ETag Infrastructure

| Component | Location | Status |
|-----------|----------|--------|
| SelfRoutingETag (encode/decode/generate) | `src/Hexalith.EventStore.Server/Queries/SelfRoutingETag.cs` | Built |
| IETagService (fail-open interface) | `src/Hexalith.EventStore.Server/Queries/IETagService.cs` | Built |
| DaprETagService (actor proxy, 3s timeout) | `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs` | Built |
| ETagActor (DAPR actor, state persistence) | `src/Hexalith.EventStore.Server/Actors/ETagActor.cs` | Built |
| QueriesController Gate 1 (ETag pre-check) | `src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs` | Built |
| QueriesController Gate 3 (ETag response header) | `src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs` | Built |
| SelfRoutingETagTests (18 tests) | `tests/Hexalith.EventStore.Server.Tests/Queries/SelfRoutingETagTests.cs` | Built |
| QueriesControllerTests (Gate 1 paths) | `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs` | Built |
| QueryEndpointE2ETests (auth/validation) | `tests/Hexalith.EventStore.IntegrationTests/ContractTests/QueryEndpointE2ETests.cs` | Built |

### Existing Test Coverage

| Test File | Tier | Tests |
|-----------|------|-------|
| `Server.Tests/Queries/SelfRoutingETagTests.cs` | 2 | Encode (3), GenerateNew (4), TryDecode (10), Roundtrip (6) — 18 total |
| `Server.Tests/Controllers/QueriesControllerTests.cs` | 2 | Gate 1: wildcard, mixed projections, decode success, decode fail, ETag match |
| `Server.Tests/Queries/DaprETagServiceTests.cs` | 2 | ETag retrieval, fail-open pattern |
| `Server.Tests/Actors/CachingProjectionActorTests.cs` | 2 | Caching actor behavior (15+ scenarios) |
| `IntegrationTests/ContractTests/QueryEndpointE2ETests.cs` | 3 | Auth/validation (no ETag-specific E2E) |

## Acceptance Criteria

1. **Given** an ETag is generated,
   **When** encoded,
   **Then** the format is `{base64url(projectionType)}.{base64url-guid}` (FR61)
   **And** ETags are wrapped in double quotes in HTTP response headers per RFC 7232.

2. **Given** a client sends an `If-None-Match` header,
   **When** the ETag is decoded,
   **Then** the projection type is extracted for routing to the correct ETag actor (`{ProjectionType}:{TenantId}`).

3. **Given** a malformed, undecodable, or missing ETag,
   **When** processed,
   **Then** it is treated as a cache miss — safe degradation by construction (FR61).

## Definition of Done

Story is complete when:
- **Required:** Tasks 0-6 pass — all audit checks verified, gaps filled, tests green
- **Conditional:** Task 6 — run Tier 1+2 tests only if any `src/` or `tests/` files were modified during Tasks 0-5
- All three acceptance criteria verified against actual code (not epics wording)
- Build passes: `dotnet build Hexalith.EventStore.slnx --configuration Release`
- No regressions in existing Tier 1 or Tier 2 tests
- Audit results table produced in Completion Notes (AC # / Expected / Actual / Pass-Fail)
- Branch: `feat/story-9-2-self-routing-etag` — create before making any code or test changes
- Scope boundary: up to 3 minor fixes (<1hr each) and missing tests are in scope; algorithmic changes (e.g., changing encoding strategy) or >3 gaps trigger a follow-up story
- **Known gap (out of scope):** No Tier 3 E2E test sends `If-None-Match` with a valid self-routing ETag to verify HTTP 304 round-trip through the full HTTP pipeline. This requires Docker and is deferred — document in Completion Notes if not addressed

## Tasks / Subtasks

- [x] Task 0: Audit SelfRoutingETag.Encode and GenerateNew against FR61 (AC: #1)
  - [x] Verify `SelfRoutingETag.Encode` produces format `{base64url(projectionType)}.{guid}`
  - [x] Verify `SelfRoutingETag.GenerateNew` generates a fresh GUID, base64url-encodes it (22 chars), and delegates to `Encode`
  - [x] Verify base64url encoding: `+` → `-`, `/` → `_`, padding `=` stripped
  - [x] Verify GUID portion is 22 characters (16 raw bytes → base64url-encoded without padding = 22 chars). Note: this is NOT a raw GUID string (36 chars with hyphens) — it is the base64url encoding of `Guid.NewGuid().ToByteArray()`
  - [x] Verify existing Encode tests cover: simple types, hyphenated types, mixed-case types

- [x] Task 1: Audit RFC 7232 ETag quoting in HTTP headers (AC: #1)
  - [x] Verify `QueriesController` wraps ETag in double quotes: `Response.Headers.ETag = $"\"{currentETag}\""`
  - [x] Verify this occurs at both Gate 1 match (HTTP 304) and Gate 3 (HTTP 200) response paths
  - [x] Verify `AnalyzeHeaderProjectionTypes` strips quotes from `If-None-Match` values before decoding
  - [x] Verify `ETagMatches` strips quotes from `If-None-Match` values for comparison
  - [x] Check for `W/` weak ETag prefix handling — if `If-None-Match` sends `W/"etag"`, verify behavior (RFC 7232 §2.3: weak comparison uses `W/` prefix; strong ETags don't). Document decision if not handled.
  - [x] Verify no `W/` prefix is emitted on ETag response header (strong validator is correct for query cache)

- [x] Task 2: Audit TryDecode for projection type extraction (AC: #2)
  - [x] Verify `SelfRoutingETag.TryDecode` correctly splits on first `.` separator
  - [x] Verify base64url decoding with padding restoration (mod 4 handling for 0, 2, 3 cases; mod 1 rejected)
  - [x] Verify decoded projection type is validated (non-empty, no colon `:`)
  - [x] Verify `QueriesController.AnalyzeHeaderProjectionTypes` uses `TryDecode` to extract projection type from `If-None-Match`
  - [x] Verify extracted projection type is passed to `IETagService.GetCurrentETagAsync(projectionType, tenantId)`
  - [x] Verify `DaprETagService` constructs actor ID as `{projectionType}:{tenantId}` from the decoded type
  - [x] Verify `DaprETagService` uses the correct DAPR actor type name when creating the actor proxy (must match `ETagActor`'s registered type name)

- [x] Task 3: Audit malformed/missing ETag handling (AC: #3)
  - [x] Verify `TryDecode` returns false for: null, empty string, no dot separator, empty prefix, empty guid part, invalid base64, colon in decoded type, old-format GUID-only ETags
  - [x] Verify `QueriesController` treats null/missing `If-None-Match` as Gate 1 skip (proceeds to full query)
  - [x] Verify `QueriesController` treats decode failure as cache miss with `Log.ETagDecodeSkipped` (EventId 1066)
  - [x] Verify wildcard `*` in `If-None-Match` skips Gate 1 entirely (not treated as error)
  - [x] Verify mixed projection types in multi-value `If-None-Match` skip Gate 1 with `Log.MixedProjectionTypesSkipped` (EventId 1067)
  - [x] Verify `MaxIfNoneMatchValues = 10` DoS protection is enforced

- [x] Task 4: Audit ETagActor old-format migration (related to AC: #3)
  - [x] Verify `ETagActor.OnActivateAsync` detects old-format ETags (no `.` separator)
  - [x] Verify automatic migration: old GUID-only → new self-routing format using actor's projection type
  - [x] Verify persist-then-cache pattern (FM-1): state persisted to DAPR before caching in memory
  - [x] Verify migration failure falls back to cold start (graceful degradation)

- [x] Task 5: Validate test coverage completeness
  - [x] Run all Tier 2 ETag-related tests (broader than just SelfRoutingETag): `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~SelfRoutingETag|FullyQualifiedName~ETagService|FullyQualifiedName~ETagActor|FullyQualifiedName~QueriesController"`
  - [x] Verify existing 18 SelfRoutingETag tests cover all `TryDecode` rejection paths
  - [x] Check if quoted ETag test exists — note: quote stripping happens in `QueriesController.AnalyzeHeaderProjectionTypes` and `ETagMatches`, NOT in `SelfRoutingETag.TryDecode`. Test belongs in `QueriesControllerTests`, not `SelfRoutingETagTests`
  - [x] Check if `W/` weak ETag prefix test exists for `ETagMatches` or `AnalyzeHeaderProjectionTypes`
  - [x] Check if multi-value `If-None-Match` test exists (e.g., `"etag1", "etag2"` with comma-separated values)
  - [x] Verify QueriesControllerTests covers both Gate 1 match (304) and Gate 3 response (200 with ETag header)
  - [x] Check if `ETagActor` migration tests exist (e.g., `tests/Hexalith.EventStore.Server.Tests/Actors/ETagActorTests.cs`) — verify old-format detection, migration to self-routing format, and migration failure fallback are all tested. If no test file exists, flag as a gap. **Note: ETagActor tests require DAPR actor infrastructure — these are Tier 2 tests. Place in `Server.Tests`, not `Contracts.Tests`.**
  - [x] Check if non-ASCII projection type roundtrip test exists (e.g., `TryDecode(Encode("données", guid))`) — base64url handles UTF-8 but verify no edge case with multi-byte characters
  - [x] If any gaps found, add tests bounded by scope — **priority order: (1) ETagActor migration tests, (2) controller quote/W/ handling tests, (3) non-ASCII roundtrip test**. Up to 3 minor gaps, <1hr each

- [x] Task 6: Fill any gaps identified during audit (bounded: up to 3 minor gaps, <1hr each)
  - [x] Fix any acceptance criteria violations found
  - [x] Add any missing tests
  - [x] If more than 3 gaps found, or any gap requires >1 hour, document in Completion Notes and create follow-up story
  - [x] Ensure build passes: `dotnet build Hexalith.EventStore.slnx --configuration Release`
  - [x] Run full Tier 1+2 tests if any `src/` or `tests/` files were modified

## Dev Notes

### Architecture: Self-Routing ETag Design

The self-routing ETag embeds the projection type directly in the ETag value, eliminating server-side routing lookups. The format is:

```
{base64url(projectionType)}.{base64url(guid)}
```

Example: projection type `"counter"` → base64url `"Y291bnRlcg"` → ETag `"Y291bnRlcg.KW4RnPjU7EuIVqT4LX_AKA"`

Key design decisions:
- **Base64url encoding** (RFC 4648 §5): `+` → `-`, `/` → `_`, no `=` padding — safe for HTTP headers
- **First-dot split**: `TryDecode` splits on the first `.` only, allowing dots in the GUID portion
- **Colon rejection**: Decoded projection types containing `:` are rejected (colons are the multi-tenancy separator in actor IDs)
- **Strong ETag**: No `W/` prefix — the ETag represents an exact cache state, not a semantic equivalent
- **Concurrency race condition (accepted)**: If two requests decode the same ETag concurrently and one triggers a regeneration mid-flight, the stale ETag comparison becomes a cache miss. The fail-open pattern handles this correctly — the request proceeds to full query execution. This is an accepted race condition, not a bug.
- **Non-existent projection type in valid ETag (accepted)**: A valid-looking ETag whose projection type doesn't match any registered projection will pass `TryDecode` (which only validates format, not existence). The ETag actor lookup will either create a new empty actor or return null (fail-open → cache miss → full query). This is correct by design — `TryDecode` is a format validator, not a registry lookup. Do not flag this as a security gap.

### Architecture: Three-Gate Query Controller Pattern

The `QueriesController` implements a three-gate pattern:
1. **Gate 1 (ETag pre-check):** Decode `If-None-Match` → extract projection type → lookup ETag actor → return 304 if match
2. **Gate 2 (Query execution):** Full MediatR query pipeline → actor routing → microservice call
3. **Gate 3 (ETag response):** Fetch current ETag → set response header with double quotes per RFC 7232

Gate 1 is fail-open: any decode failure, actor unavailability, or mixed projection types result in proceeding to Gate 2 (full query). This ensures self-routing ETags are a pure optimization — never a correctness requirement.

### Architecture: FR61 Spec vs Implementation Detail

FR61 specifies format `{base64url(projectionType)}.{guid}`. The implementation uses `{base64url(projectionType)}.{base64url(guid)}` where the GUID is also base64url-encoded. This is an intentional refinement — raw GUIDs contain characters (`{`, `-`) that are not safe in HTTP ETag values. The 22-character base64url GUID is the standard compact representation.

### Key Code Patterns

- **SelfRoutingETag**: Pure static utility, no dependencies. `internal static class` — not part of public API surface.
- **DaprETagService**: 3-second `RequestTimeout` on actor proxy. Fail-open (returns null on any exception, logged at EventId 1061).
- **ETagActor**: Persist-then-cache pattern (FM-1). Automatic migration from old-format ETags. Actor ID: `{projectionType}:{tenantId}`.
- **QueriesController**: `MaxIfNoneMatchValues = 10` for DoS prevention. `AnalyzeHeaderProjectionTypes` uses span-based parsing for zero-allocation performance. Source-generated logging via `partial class Log`.

### Previous Story Intelligence (Story 9-1)

Story 9-1 was a validation/audit story that verified query contracts and 3-tier routing. Key learnings:
- **Separator convention**: Code uses colons (`:`) not hyphens (`-`) as separators — epics file notation is illustrative only
- **TenantId vs ProjectionType**: FR57's literal "TenantId" on contract was intentionally replaced with `ProjectionType` (static abstract) because TenantId varies per request
- **API version**: All endpoints use `api/v1/` uniformly (not `api/v2/queries` as mentioned in PRD)
- **Pre-existing test failure**: `ErrorReferenceEndpointTests.AllProblemTypeUris_HaveCorrespondingErrorModel` was already failing before Story 9-1 — ignore if still failing
- **Test counts**: 694 Tier 1 tests, 108 query-related Tier 2 tests, 1505/1506 total Server.Tests (1 pre-existing failure)

### Testing Pattern

- **xUnit** with **Shouldly** assertions, **NSubstitute** for mocking
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`
- SelfRoutingETag tests use `[Theory]` with `[InlineData]` for roundtrip parametric tests
- Controller tests require NSubstitute mocks for `IMediator`, `IETagService`, and `ILogger<QueriesController>`

### Project Structure Notes

- `SelfRoutingETag` is `internal static` in `Hexalith.EventStore.Server` — tested via `[InternalsVisibleTo]`
- `IETagService` and `DaprETagService` are in `Hexalith.EventStore.Server/Queries/`
- `ETagActor` is in `Hexalith.EventStore.Server/Actors/`
- `QueriesController` is in `Hexalith.EventStore.CommandApi/Controllers/`
- DI registration: `IETagService` → `DaprETagService` (Scoped) in `ServiceCollectionExtensions.cs`

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.2: Self-Routing ETag Encode/Decode]
- [Source: _bmad-output/planning-artifacts/prd.md#FR61]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR35-NFR39]
- [Source: _bmad-output/implementation-artifacts/9-1-query-contracts-and-routing-model.md]
- [Source: src/Hexalith.EventStore.Server/Queries/SelfRoutingETag.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs]
- [Source: src/Hexalith.EventStore.Server/Actors/ETagActor.cs]
- [Source: src/Hexalith.EventStore.Server/Queries/DaprETagService.cs]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Pre-existing failure: `ErrorReferenceEndpointTests.AllProblemTypeUris_HaveCorrespondingErrorModel` (1505/1506 in Story 9-1, now 1517/1518) — unchanged, not related to this story.

### Completion Notes List

**Audit Results Table:**

| AC # | Expected | Actual | Pass/Fail |
|------|----------|--------|-----------|
| #1 (Encode format) | `{base64url(projectionType)}.{base64url-guid}` with RFC 7232 double quotes | `SelfRoutingETag.Encode` at L19-22, `GenerateNew` at L29-36, `QueriesController` L96 + L138 wrap in `"..."` | PASS |
| #2 (Decode routing) | Extract projection type from `If-None-Match`, route to ETag actor `{ProjectionType}:{TenantId}` | `TryDecode` first-dot split L54, `AnalyzeHeaderProjectionTypes` L149-177, `DaprETagService` L28 actor ID format | PASS |
| #3 (Malformed/missing) | Treated as cache miss — safe degradation | `TryDecode` rejects all malformed inputs, `QueriesController` fails open with EventId 1066/1067, wildcard skips Gate 1 | PASS |

**No acceptance criteria violations found.**

**Gaps identified and filled (3/3, all within scope):**
1. **ETagActor tests (priority #1):** Expanded `ETagActorTests.cs` to cover actor behavior, not only utility checks: `RegenerateAsync` persist+cache path, `OnActivateAsync` load path for self-routing ETags, old-format migration with persisted replacement, migration failure fallback to cold start, and state-read failure fallback.
2. **W/ weak ETag prefix tests (priority #2):** Added and hardened tests for weak validator handling (`AnalyzeHeaderProjectionTypes_WeakETagPrefix_FailsDecodeReturnsNull`, `Submit_WeakETagPrefix_CacheMissReturns200`) with explicit control-flow assertions (mediator invocation and ETag service interaction) to prove fail-open query execution.
3. **Non-ASCII routing coverage (priority #3):** Added non-ASCII roundtrip coverage in `SelfRoutingETagTests` and a controller-level routing test (`Submit_NonAsciiProjectionTypeInIfNoneMatch_RoutesUsingDecodedProjectionType`) validating decoded projection type propagation to ETag lookup.

**W/ weak ETag decision documented:** `W/"etag"` is handled correctly by construction — the `AnalyzeHeaderProjectionTypes` quote stripping only removes outer `"..."` delimiters, so `W/"etag"` passes through with `W/` prefix intact → `TryDecode` fails → cache miss → full query. This is the correct fail-open behavior for a strong ETag validator. No explicit `W/` stripping is implemented or needed.

**Known gap (out of scope):** No Tier 3 E2E test sends `If-None-Match` with a valid self-routing ETag to verify HTTP 304 round-trip through the full HTTP pipeline. This requires Docker and is deferred per DoD.

**Test counts after audit and hardening pass:**
- ETag-focused Tier 2 filter (`SelfRoutingETag|ETagService|ETagActor|QueriesController`): 96 passed, 0 failed
- Focused regression slice (`QueriesControllerTests|ETagActorTests`): 42 passed, 0 failed

### File List

- `tests/Hexalith.EventStore.Server.Tests/Actors/ETagActorTests.cs` (NEW) — 8 tests for ETagActor
- `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs` (MODIFIED) — 2 new W/ prefix tests
- `tests/Hexalith.EventStore.Server.Tests/Queries/SelfRoutingETagTests.cs` (MODIFIED) — 2 new non-ASCII InlineData entries
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (MODIFIED) — status updated
- `_bmad-output/implementation-artifacts/9-2-self-routing-etag-encode-decode.md` (MODIFIED) — story file updated

### Change Log

- 2026-03-19: Story 9-2 audit completed. All 3 acceptance criteria verified against actual code. 3 test coverage gaps identified and filled: (1) ETagActor tests, (2) W/ weak ETag prefix tests, (3) non-ASCII roundtrip tests. 12 new tests added, 0 source code changes required, 0 AC violations found.
- 2026-03-19: Post-review hardening pass completed. Strengthened weak-ETag control-flow assertions, added controller-level non-ASCII routing coverage, and expanded actor lifecycle/migration tests with mocked state manager. Validation: targeted suites green (42/42) and broader ETag-focused filter green (96/96).
