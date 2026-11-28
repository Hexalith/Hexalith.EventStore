---
title: 'Story 2.18: Bind Conditional Queries To The Returned Representation'
type: 'bugfix'
created: '2026-11-28'
status: 'in-review'
baseline_commit: '629d3325684d603887dfb2d916909a70b83e765a'
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
- [x] `src/Hexalith.EventStore/Controllers/QueriesController.cs` -- Remove L397-407 post-read `eTagService.GetCurrentETagAsync` call, validate producer `Metadata.ETag` as a wire-safe strong validator before header/body/304 emission, and keep constructor signatures warning-free -- stops detached validator attachment while preserving NFR12.
- [x] `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs` -- In `ExecuteQueryAsync`, compute a deterministic 16-byte SHA-256 self-routing ETag from valid `ProjectionType` + `StateBytes` and pass it via `QueryResponseMetadata(ETag: ...)` -- binds replay projection validators to the returned bytes without schema changes.
- [x] `tests/Hexalith.EventStore.ProviderVerification/StatefulQueryRouter.cs` -- Bind `Metadata.ETag` directly for tagged provider states using `StatefulETagService` constants -- aligns Pact fixture with producer-bound validators.
- [x] `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs` -- Replace post-read `IETagService` tests with producer-bound tests, cover every I/O matrix row, and assert `IETagService` is never invoked by `Submit` -- proves AC 1–3 at controller layer.
- [x] `tests/Hexalith.EventStore.Server.Tests/Actors/EventReplayProjectionActorTests.cs` -- Test deterministic self-routing ETag generation, change on state update, and omission on invalid projection type -- proves actor-level representation binding.
- [x] `tests/Hexalith.EventStore.Server.Tests/Integration/QueryResponseProvenancePersistenceTests.cs` -- Add typed + untyped Kestrel/`EventStoreGatewayClient` tests for untagged rev-1 with rev-2 advance (`200`, no ETag, preserved metadata, no follow-up `304`) and producer-bound `200`/`304`/changed `200` -- proves controller + client contract.
- [x] `docs/reference/query-api.md` -- Update Conditional Requests docs to remove post-read fallback wording -- keeps docs truthful.

**Acceptance Criteria:**
- Given a `ProjectionBacked` read returning revision 1 without a validator while revision 2 advances, when served through `QueriesController` and `EventStoreGatewayClient`, then the response is `200` with revision 1's payload, independent projection metadata, and no header/body ETag, and a conditional follow-up carrying revision 2's tag returns `200`.
- Given an eligible `ProjectionBacked` read with a producer-bound strong validator (including `EventReplayProjectionActor`), when queried and repeated after auth and routing, then initial returns `200` + ETag, matching repeat yields `304`, changed representation yields `200`, and paging/freshness/`HandlerComputed`/`Unknown` restrictions hold.
- Given Release build and affected test suites, when executed, then all pass with zero warnings/errors.

## Implementation Notes

- The gateway validates the producer token after authorization and routing and uses the same accepted value in the response header, body metadata, and conditional comparison. Replay projections derive a self-routing validator from the selected state's bytes. The actor cache continues to use its existing invalidation stamp.
- The regression tests select revision 1, persist revision 2, and then return revision 1. The detached service fixture exposes a self-routing tag for revision 2, so its use would attach the wrong validator and permit a false `304`. Raw HTTP, typed SDK, and untyped SDK cases check the original read and the conditional retry.

## Spec Change Log

- 2026-11-28. Implemented the approved producer-bound validator design. Review strengthened the wire, cache, length-limit, and provider-state assertions and clarified the query documentation.

## Review Triage Log

| Finding | Verdict | Route and evidence |
| --- | --- | --- |
| Blind 1. Paging metadata on `304` | Medium | Deferred. The bodyless response has no paging carrier. This behavior predates the change, and the approved story preserves the conditional guard for explicit request paging. |
| Blind 2. Dotted opaque tags treated as self-routing | Medium | Deferred. The existing decoder accepts any nonempty suffix after a decodable prefix, which can make two quoted opaque tokens appear to name different projections. |
| Blind 3. Repeated conditional parsing | Low | Rejected. The two existing parse calls use the same bounded parser and preserve the same result. Consolidating their control flow is outside this correction. |
| Blind 4. Unnamed projection fixtures | Medium | Patch. The fixtures supplied no producer tag and could pass with the required projection-name guard removed. |
| Blind 5. Raw ETag omission | Medium | Patch. Client normalization replaces body metadata from the header. Assert both wire carriers directly alongside the typed and untyped client checks. |
| Blind 6. Replay producer transport | Medium | Patch. The Kestrel test substitutes the actor invoker and cannot prove replay actor remoting. Add a content-derived tag assertion to the existing live Dapr proof. |
| Blind 7. Cache hit may compare two null tags | Low | Patch. Assert the producer tag survives a warm hit and changes when the cache stamp invalidates the previous state. |
| Blind 8. Pact fixture service shares producer values | Low | Rejected. Controller and Kestrel tests already assert that the service receives no calls, including the regression where the service holds a later tag. The provider states preserve the consumer's established validator values. |
| Blind 9. SDK and raw HTTP wording | Low | Patch. The SDK quotes an opaque token before transmission, while the server splits bare list members at commas. Document both behaviors and the validator length limit. |
| Blind 10. Stale architecture and router description | Low | Patch. Update the pre-routing lookup description and remove the obsolete fallback claim from the router comment. |
| Blind 11. Verification section lists expected results | Low | Rejected as a code change. Actual verification and the guarded completion result are recorded during this workflow. |
| Blind 12. Review diff contains an intervening commit | False | Rejected. The workflow requires the full diff from the captured baseline. The workflow and Python fixture hunks belong to intervening commit `4505d0688c4f191fe480d10f6accf6be7644c8c0`, as confirmed by the current worktree diff. |
| Edge 1. Detached fixture tag cannot reproduce old `304` | Medium | Patch. The old service-only matcher required a self-routing tag, and the in-memory store returned a numeric token. Make the test service return a self-routing validator for the advanced persisted entry. |
| Edge 2. Client normalization masks raw ETag | Medium | Patch. Shared cause and fix with Blind 5. |
| Verification 1. Replay validator across Dapr | Medium | Patch. Shared cause and fix with Blind 6. |
| Verification 2. Inclusive 4096-character limit | Low | Patch. Exercise the maximum supported length in the producer-bound `200`, `304`, and changed `200` sequence. |
| Verification 3. Provider state coverage | Medium | Patch. Exercise the three newly tagged states with the existing real Kestrel provider test and assert their consumer-pinned status and tag. |

## Verification

**Commands:**

- `dotnet build Hexalith.EventStore.slnx -c Release -warnaserror` uses the repository's enabled NuGet audit.
- `dotnet test --project tests/<project>/<project>.csproj -c Release --no-build --no-restore` runs each of the six requested suites after the solution build. The Server run uses the pinned signed Tenants Contracts 7.0.0 archive in a private fixture package root.
- `dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net11.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*NativeQueryRegisteredActorsAndLifecycleKeepActualChannelCatalogAndMethodBoundaries'` passed all six cases using the pinned Dapr 1.19.1 runtime. This checks the expected content-derived tag after persisted actor reactivation and native remoting.
- `python3 -B tools/validate-phase-4-story-completion.py --spec _bmad-output/implementation-artifacts/spec-2-18-bind-conditional-queries-to-the-returned-representation.md --target spec --write` checks the guarded completion transition.

**Results on the reviewed tree**

- Release solution build passed with zero warnings and errors. The focused Server matrix passed 534 cases without skips, and `git diff --check` passed.
- Server passed 8,653 cases with 25 existing skips. Client passed 2,490 with two existing skips. Query Routing passed 15, REST API Generators passed 320, and Sample passed 268, all without failures or skips.
- `dotnet test --project tests/Hexalith.EventStore.ProviderVerification.Tests/Hexalith.EventStore.ProviderVerification.Tests.csproj -c Release --no-build --no-restore` exited 2 with 82 passes and one failure. `ObserveLive_RecordsCurrentProvenanceWithoutInventingApproval` requires a clean provider source worktree and reports `RuntimeMatches == false` for the local edits. The real Kestrel Pact cases passed.
- The standalone Aspire baseline started but `timeout 35s aspire wait eventstore --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` timed out while the `security` resource was unhealthy. The AppHost was stopped after inspection.
- The guarded completion command exited 1 with `ADAPTER_UNAVAILABLE` for NFR12. Completion requires a story-owned evidence adapter, fresh authenticated approval, and a seal. The spec remains `in-review` and the sprint row stays `in-progress` with the full Provider Verification check outstanding.
