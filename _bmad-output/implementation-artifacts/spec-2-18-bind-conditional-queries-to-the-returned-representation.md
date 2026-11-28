---
title: 'Story 2.18: Bind Conditional Queries To The Returned Representation'
type: 'bugfix'
created: '2026-11-28'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
story_key: '2-18-bind-conditional-queries-to-the-returned-representation'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** When a `ProjectionBacked` query omits an ETag, `QueriesController.Submit` calls `IETagService.GetCurrentETagAsync` post-read and attaches that tag to the response header and body. If the projection advances from revision 1 to revision 2 between the read and the lookup, revision 1 is labeled with revision 2's ETag and a follow-up conditional request can falsely return `304` (FR4-C3, NFR8, NFR12).

**Approach:** Remove the post-read `IETagService` lookup in `QueriesController.Submit` so ETags are emitted only from valid strong producer-bound validators on eligible `ProjectionBacked` reads. Also bind a deterministic representation-specific strong self-routing validator in `EventReplayProjectionActor.ExecuteQueryAsync` from the exact returned `ProjectionState.StateBytes`. Replace post-read lookup tests and verify deterministic revision-1/revision-2 interleaving and producer-bound `200`/`304` behavior through both `QueriesController` and `EventStoreGatewayClient`.

**Decisions (owner, 2026-11-28):**
- **Option B (`EventReplayProjectionActor`):** Bind a strong self-routing ETag in `EventReplayProjectionActor.ExecuteQueryAsync` using `SelfRoutingETag.Encode(projectionType, base64url(SHA256(StateBytes)[..16]))` (no `ProjectionState` schema or write-path change), keeping built-in replay projections, `Sample.BlazorUI`, smoke preflight, and Tier 3 E2E tests race-free while untagged producers continue returning `200` without an ETag.
- **Keep full spec:** Retain as a single cohesive spec.

## Boundaries & Constraints

**Always:**
- Emit `ETag` (header and `Metadata.ETag`) only when a `ProjectionBacked` response with non-whitespace `ProjectionType` supplies a valid strong producer validator.
- On missing or non-strong/malformed producer validators, return `200 OK` with no `ETag` header and `Metadata.ETag == null`, preserving payload and independent projection metadata (`Provenance`, `Lifecycle`, `ProjectionVersion`, `IsStale`, `IsDegraded`, `ServedAt`, `Paging`, `WarningCodes`).
- Evaluate `If-None-Match` only after auth and routing; return `304` only on a matching eligible producer-bound strong validator and `200` on changed representations or ineligible shapes (explicit paging/search/filters/orderBy/freshness, non-identity payload, lossy version/warnings, weak/wildcard/malformed/over-limit/mixed headers, `HandlerComputed` or `Unknown` routes).
- In `EventReplayProjectionActor.ExecuteQueryAsync`, derive the validator solely from the read `ProjectionState`'s valid `ProjectionType` and `StateBytes` (16-byte SHA-256 prefix -> 22-char base64url suffix); omit `ETag` if `ProjectionType` is invalid.
- Keep `QueriesController` constructor signatures (`IETagService` parameter unused) and `IETagService`/`DaprETagService`/`ETagActor`/`CachingProjectionActor` intact for NFR12 compatibility and Gate 2 actor caching.
- Use `python3 -B tools/validate-phase-4-story-completion.py` for completion; keep status open if refused for high-risk `NFR12`.

**Never:**
- Call `IETagService.GetCurrentETagAsync` from `QueriesController.Submit`.
- Change `ProjectionState` wire serialization or write paths to attach validators.
- Weaken `EventStoreGatewayClient`, `RestApiControllerEmitter`, or `QueryResponseProvenancePolicy` provenance/freshness checks.
- Omit `NFR12` coverage, write `status: done` directly, or commit `evidence/` folders.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Untagged rev-1 with rev-2 advance | `ProjectionBacked` rev-1 payload, `Metadata.ETag == null`; `IETagService` holds rev-2 tag | `200 OK`, rev-1 payload, no header/body ETag, independent metadata preserved; `IETagService` uncalled; follow-up `If-None-Match` with rev-2 tag returns `200` | N/A |
| Producer-bound strong ETag | Eligible `ProjectionBacked` read with `"v1"`, matching repeat, then changed `"v2"` | Initial `200` + `"v1"` (header + body); repeat `304` after auth/routing + provenance/metadata headers; changed payload `200` + `"v2"` | N/A |
| Invalid producer ETag | `ProjectionBacked` read with `W/"v1"`, `*`, `"quoted"`, `bad"q`, `a\r\nb`, leading/trailing space, or >4096 chars | `200 OK`, payload and independent metadata preserved, no header/body ETag; conditional request returns `200` | Fail closed (omit tag) |
| Ineligible route or shape | `HandlerComputed`/`Unknown`, blank `ProjectionType`, explicit policy inputs, non-identity payload, or lossy version/warnings | Non-`ProjectionBacked`/blank target omit ETag and return `200`; policy/payload/lossy-header cases return `200` (no `304`) | Freshness policy 400 `query_projection_stale` unchanged |
| `EventReplayProjectionActor` read | Persisted `ProjectionState` for rev-1, then updated `StateBytes` for rev-2 | Returns `Metadata.ETag` in `<base64url(projectionType)>.<22-char base64url>` bound to `StateBytes`; identical bytes yield same tag, changed bytes yield different tag; invalid `ProjectionType` omits ETag | N/A |
| Gateway client untagged read | Typed + untyped `EventStoreGatewayClient.SubmitQueryAsync` against Kestrel returning untagged rev-1 while rev-2 advances | `IsNotModified == false`, `ETag == null`, `Metadata.ETag == null`, payload and metadata preserved; conditional retry returns `200` | Existing 502 on invalid `304` unchanged |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore/Controllers/QueriesController.cs:65,76-116,388-494,629-704` -- Remove post-read `eTagService.GetCurrentETagAsync` (L397-407); validate strong producer ETag; keep constructor signatures and existing conditional/provenance/freshness/lossless-header guards.
- `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs:331-358` -- Bind deterministic strong `SelfRoutingETag` from `(ProjectionType, SHA256(StateBytes)[..16])` into `QueryResult.Metadata.ETag` (reusing `SelfRoutingETag.Encode`).
- `src/Hexalith.EventStore.Server/Queries/SelfRoutingETag.cs:12-36` -- Reuse `SelfRoutingETag.Encode` (no signature break).
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs:386-462,673-728` & `src/Hexalith.EventStore.Contracts/Queries/QueryResponseProvenancePolicy.cs:28-47` -- Reuse unchanged.
- `tests/Hexalith.EventStore.ProviderVerification/StatefulQueryRouter.cs:23-64` & `StatefulETagService.cs:5-27` -- Bind state-specific producer ETags in `StatefulQueryRouter` so `RealKestrelPactTests` passes without post-read lookup.
- `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs:554-1680,2025-2074` -- Replace post-read lookup expectations with producer-bound and untagged-interleaving tests.
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventReplayProjectionActorTests.cs` -- Add unit tests for deterministic content-bound self-routing ETag on `ExecuteQueryAsync` (same bytes, changed bytes, invalid projection type).
- `tests/Hexalith.EventStore.Server.Tests/Integration/QueryResponseProvenancePersistenceTests.cs:36-269` -- Add Kestrel + `EventStoreGatewayClient` untagged rev-1/rev-2 interleaving and producer-bound 200/304/200 tests.
- `docs/reference/query-api.md:159-195` -- Remove post-read validator fallback wording.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.EventStore/Controllers/QueriesController.cs` -- Remove L397-407 post-read `eTagService.GetCurrentETagAsync` call, validate producer `Metadata.ETag` as a wire-safe strong validator before header/body/304 emission, and keep constructor signatures warning-free -- stops detached validator attachment while preserving NFR12.
- [ ] `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs` -- In `ExecuteQueryAsync`, compute a deterministic 16-byte SHA-256 self-routing ETag from valid `ProjectionType` + `StateBytes` and pass it via `QueryResponseMetadata(ETag: ...)` -- binds replay projection validators to the returned bytes without schema changes.
- [ ] `tests/Hexalith.EventStore.ProviderVerification/StatefulQueryRouter.cs` -- Bind `Metadata.ETag` directly for tagged provider states using `StatefulETagService` constants -- aligns Pact fixture with producer-bound validators.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs` -- Replace post-read `IETagService` tests with producer-bound tests, cover every I/O matrix row, and assert `IETagService` is never invoked by `Submit` -- proves AC 1–3 at controller layer.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Actors/EventReplayProjectionActorTests.cs` -- Test deterministic self-routing ETag generation, change on state update, and omission on invalid projection type -- proves actor-level representation binding.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Integration/QueryResponseProvenancePersistenceTests.cs` -- Add typed + untyped Kestrel/`EventStoreGatewayClient` tests for untagged rev-1 with rev-2 advance (`200`, no ETag, preserved metadata, no follow-up `304`) and producer-bound `200`/`304`/changed `200` -- proves controller + client contract.
- [ ] `docs/reference/query-api.md` -- Update Conditional Requests docs to remove post-read fallback wording -- keeps docs truthful.

**Acceptance Criteria:**
- Given a `ProjectionBacked` read returning revision 1 without a validator while revision 2 advances, when served through `QueriesController` and `EventStoreGatewayClient`, then the response is `200` with revision 1's payload, independent projection metadata, and no header/body ETag, and a conditional follow-up carrying revision 2's tag returns `200`.
- Given an eligible `ProjectionBacked` read with a producer-bound strong validator (including `EventReplayProjectionActor`), when queried and repeated after auth and routing, then initial returns `200` + ETag, matching repeat yields `304`, changed representation yields `200`, and paging/freshness/`HandlerComputed`/`Unknown` restrictions hold.
- Given Release build and affected test suites, when executed, then all pass with zero warnings/errors.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet build Hexalith.EventStore.slnx -c Release -warnaserror -p:NuGetAudit=false` -- expected: 0 warnings, 0 errors.
- Build and run `Hexalith.EventStore.Server.Tests`, `Hexalith.EventStore.ProviderVerification.Tests`, `Hexalith.EventStore.Client.Tests`, `Hexalith.EventStore.QueryRouting.Tests`, `Hexalith.EventStore.RestApi.Generators.Tests`, and `Hexalith.EventStore.Sample.Tests` in `Release` -- expected: 0 failed.
- `python3 -B tools/validate-phase-4-story-completion.py --spec _bmad-output/implementation-artifacts/spec-2-18-bind-conditional-queries-to-the-returned-representation.md --target spec --write` -- expected: verify Phase 4 gate; keep status open and record diagnostic if refused for NFR12.
