# Post-Epic-9 R9-A2: Query Cache Topology Proof

Status: ready-for-dev

<!-- Source: epic-9-retro-2026-04-30.md - R9-A2 -->
<!-- Source: post-epic-10-r10a8-r9-r10-follow-through-tracking.md - R9/R10 reconciliation -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a QA-focused query-pipeline maintainer,
I want a repeatable Aspire proof of the query-cache topology from cold query through invalidation and re-warm,
so that Epic 9 cache confidence is backed by running cross-component evidence instead of only focused actor/controller tests.

## Story Context

Epic 9 delivered the query pipeline, self-routing ETags, ETag actors, projection invalidation, and in-memory `CachingProjectionActor` behavior. The retrospective still left a high-priority topology gap: no single evidence artifact proves the complete running sequence of cold query, ETag header capture, warm `304 NotModified`, projection-change invalidation, and cache re-warm under Aspire.

R9-A1 owns the narrower stale HTTP validator proof. This story is broader: it must prove that Gate 1 (`If-None-Match` pre-check), Gate 2 (`CachingProjectionActor` in-memory cache), ETag actor regeneration, projection update delivery, and the public query endpoint cooperate in the running topology. It should be evidence/test hardening, not new cache infrastructure. Prefer a focused Tier 3 integration test and an evidence note over product code changes. If the topology fails, diagnose and route the real defect; do not weaken the proof or replace it with unit-level evidence.

Existing evidence to reuse:

- R9-A1 is ready-for-dev and covers stale `If-None-Match` after projection change, but it explicitly does not absorb full cache topology or re-warm evidence.
- R11-A4 added `ValidProjectionRoundTripE2ETests`, proving command-to-query delivery and same-ETag conditional behavior for a valid projection response.
- `QueriesController` handles Gate 1 by decoding self-routing ETags from `If-None-Match`, fetching the current projection ETag, returning `304` only on match, and otherwise routing the query through MediatR.
- `CachingProjectionActor` handles Gate 2 by comparing the current ETag with its cached ETag and payload bytes. A matching ETag returns cached bytes; a changed ETag executes the query and refreshes its in-memory cache.
- `ETagActor` persists the current self-routing ETag for `{projectionType}:{tenantId}` and `DaprETagService` intentionally fails open when ETag lookup is unavailable.

## Acceptance Criteria

1. **A focused Aspire topology proof exists.** Add or extend a Tier 3 integration test under `tests/Hexalith.EventStore.IntegrationTests/ContractTests/`, using `[Collection("AspireContractTests")]` and `AspireContractTestFixture`, that drives the public EventStore HTTP surface through the running Aspire topology.

2. **The proof uses one stable query identity.** The scenario uses the same tenant, domain, aggregate ID, projection type, entity ID, query type, payload, and JWT tenant/domain claims across commands and all queries. Prefer the sample counter path: tenant `tenant-a`, domain `counter`, projection type `counter`, query type `get-counter-status`, unique aggregate ID, and `entityId = aggregateId`.

3. **The cold query populates the topology.** Submit an `IncrementCounter` command, poll command status until `Completed`, assert persisted-event evidence such as `eventCount > 0` when present, then poll `POST /api/v1/queries` until `200 OK` returns the expected projected count and an `ETag` header. This first successful query is the cold path for the selected identity.

4. **The returned ETag is self-routing and current.** Assert that the first successful response includes a strong quoted HTTP validator whose inner value follows the self-routing `{base64url(projectionType)}.{base64url-guid}` shape and decodes to `counter` through `SelfRoutingETag` or equivalent test-side verification. Do not accept old-format GUID-only ETags.

5. **A warm same-identity request returns 304.** Re-send the exact same query with `If-None-Match` set to the server-provided ETag. The passing result is `304 NotModified`. If the endpoint returns `200 OK`, the test must record enough response detail to diagnose whether Gate 1 was skipped, the ETag actor returned a different value, or a same-aggregate projection changed concurrently.

6. **Projection change invalidates the old ETag.** Submit a second `IncrementCounter` command for the same aggregate and wait for `Completed`. Then send the same query with the original ETag in `If-None-Match` until the projection change is observed. The passing state is `200 OK` with the incremented count and an `ETag` different from the original. A final `304 NotModified` after the second projection is visible is a failure.

7. **Cache re-warm is proven after invalidation.** After the changed-count `200 OK` response, re-send the exact same query with the new ETag. The passing result is `304 NotModified`. This is the re-warm proof: the topology must converge to a new current validator after the projection update.

8. **Gate 2 evidence is recorded without brittle internals.** Record evidence that the query actor cache path was exercised, either by asserting existing structured logs for `Stage=CacheMiss` followed by `Stage=CacheHit`/`ETagPreCheckMatch` when accessible from the test/AppHost evidence, or by documenting why direct log capture was unavailable and proving the public sequence instead. Do not call projection actors directly or add test-only production hooks solely to observe private cache fields.

9. **Eventual consistency is bounded and diagnostic.** During polling windows, temporary `404 NotFound`, old counts, missing ETags, parse errors, or transient command/query responses are retry states only until the deadline. Timeout failures must include tenant, domain, aggregate ID, projection type, query type, original ETag, new ETag if observed, last status code, last response body, parsed count, parse error, and the phase that timed out.

10. **The proof remains topology-focused.** Do not absorb R9-A1 stale-validator scope beyond what is necessary for this broader sequence, R9-A8 query-latency/operational NFR evidence, SignalR client behavior, Redis backplane behavior, release governance, or new production cache design. This story proves the existing topology or exposes a routed defect.

11. **Existing query/projection tests are not weakened.** Preserve `ValidProjectionRoundTripE2ETests`, `ProjectionMalformedResponseE2ETests`, `QueryEndpointE2ETests`, and query/ETag unit tests. Shared helper extraction is allowed only when explicit query identity, token permissions, bounded waits, ETag handling, and diagnostic failures stay at least as strong as the existing tests.

12. **The proof is repeatable from the command line.** Record the targeted command in this story's Dev Agent Record, for example `dotnet test tests/Hexalith.EventStore.IntegrationTests --filter "FullyQualifiedName~QueryCacheTopologyProofE2ETests"`. If Docker, DAPR placement/scheduler, Aspire startup, access-control policy, or a pre-existing shared-topology failure blocks execution, record the exact blocker instead of marking the story complete.

13. **No production behavior is changed without a real defect.** The expected implementation is test/evidence work. Product code changes are allowed only if the focused proof exposes a real cache, ETag, routing, or projection-update defect; any such change must include focused unit coverage for the defective branch and preserve fail-open behavior for malformed, wildcard, mixed-projection, and unavailable ETag actor cases.

14. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and `last_updated` names the query-cache topology proof result. At code-review signoff, both become `done`.

## Tasks / Subtasks

- [ ] Task 1: Add the focused Tier 3 topology test (AC: #1, #2)
    - [ ] Create `QueryCacheTopologyProofE2ETests` or add a clearly named test to an existing focused query/ETag contract-test file.
    - [ ] Use `[Trait("Category", "E2E")]`, `[Trait("Tier", "3")]`, and `[Collection("AspireContractTests")]`.
    - [ ] Use `AspireContractTestFixture.EventStoreClient`; do not launch a separate AppHost from inside the test.

- [ ] Task 2: Prove cold query and initial ETag (AC: #2, #3, #4)
    - [ ] Submit the first `IncrementCounter` through `POST /api/v1/commands`.
    - [ ] Poll command status to terminal success and fail on terminal non-success status with the final status body.
    - [ ] Poll `POST /api/v1/queries` with explicit `projectionType = "counter"` and `entityId = aggregateId` until the expected count and ETag are present.
    - [ ] Parse direct-object and base64-string query payload forms, matching the existing `ValidProjectionRoundTripE2ETests` pattern.
    - [ ] Assert the returned ETag is quoted, self-routing, and decodes to `counter`.

- [ ] Task 3: Prove warm 304 behavior (AC: #5)
    - [ ] Re-send the exact same query with `If-None-Match` set to the baseline ETag exactly as the server returned it.
    - [ ] Assert `304 NotModified`.
    - [ ] Preserve enough response detail to diagnose an unexpected `200 OK`, `404 NotFound`, missing ETag, or mismatched projection type.

- [ ] Task 4: Prove invalidation after projection change (AC: #6, #9)
    - [ ] Submit a second `IncrementCounter` for the same aggregate and wait for `Completed`.
    - [ ] Poll the same query with the original baseline ETag in `If-None-Match`.
    - [ ] Treat pre-delivery `404`, old count, parse errors, or missing ETag as bounded retry states.
    - [ ] Pass only when `200 OK` returns the incremented count and a new ETag different from the original.
    - [ ] Fail with a diagnostic message if `304` remains after the changed projection is visible.

- [ ] Task 5: Prove cache re-warm and record topology evidence (AC: #7, #8)
    - [ ] Re-send the exact same query with the new post-change ETag.
    - [ ] Assert `304 NotModified`.
    - [ ] Capture available structured log, trace, or test-output evidence for `ETagPreCheckMatch`, `CacheMiss`, `CacheHit`, or equivalent stages when accessible.
    - [ ] If log/trace capture is unavailable in the test fixture, record that limitation and keep the public HTTP sequence as the non-brittle proof.

- [ ] Task 6: Keep helper and product changes narrow (AC: #10, #11, #13)
    - [ ] Reuse or extract query-request, status-poll, payload-parse, and ETag helpers only if they preserve explicit identity and diagnostics.
    - [ ] Do not modify R9-A1/R9-A8 scope, SignalR tests, Redis backplane tests, release-governance docs, or production cache design solely for this proof.
    - [ ] If production code changes are required, add focused unit coverage around the defective query, ETag, cache, or projection-update branch.

- [ ] Task 7: Validate and record evidence (AC: #12, #14)
    - [ ] Run the targeted query-cache topology proof test.
    - [ ] If shared helpers changed, run affected neighbors such as `ValidProjectionRoundTripE2ETests`, `ProjectionMalformedResponseE2ETests`, and `QueryEndpointE2ETests`.
    - [ ] Run the relevant unit slice if production query/ETag/cache code changed.
    - [ ] Record commands, results, environment caveats, and blockers in this story's Dev Agent Record.

## Dev Notes

### Existing Implementation To Reuse

- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractTestFixture.cs` starts the full Aspire topology with `EnableKeycloak=false`, waits for `eventstore`, `eventstore-admin`, and `sample`, and exposes `EventStoreClient`.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/ValidProjectionRoundTripE2ETests.cs` already demonstrates the public command-to-query path, explicit counter query identity, bounded projection polling, payload parsing, ETag capture, and current-ETag conditional request pattern.
- `tests/Hexalith.EventStore.IntegrationTests/Helpers/ContractTestHelpers.cs` has command submission/status helpers, but `CreateQueryRequest` does not serialize `ProjectionType` or `EntityId`. Use a local explicit query builder or extend the helper carefully.
- `src/Hexalith.EventStore/Controllers/QueriesController.cs` owns the HTTP query boundary and Gate 1 ETag pre-check. It returns `304` only when a self-routing `If-None-Match` decodes to one projection type and matches the current ETag from `IETagService`.
- `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs` owns Gate 2 in-memory caching. It uses the current ETag to return cached payload bytes when possible and calls `ExecuteQueryAsync` on cache miss.
- `src/Hexalith.EventStore.Server/Actors/ETagActor.cs` persists self-routing ETags for `{projectionType}:{tenantId}` and regenerates them through `SelfRoutingETag.GenerateNew`.
- `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs` derives ETag actor IDs as `{projectionType}:{tenantId}` and fails open to `null` on actor errors.
- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` routes reads to projection actors using `ProjectionType` when present, otherwise `QueryType`.
- `src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs` derives Tier 1 actor IDs as `{QueryType}:{TenantId}:{EntityId}` with colon-separated, colon-free segments.

### Implementation Guardrails

- Use public HTTP surfaces only. Do not seed actor state, call `IETagActor` directly, invoke `EventReplayProjectionActor` directly, or call the sample `/project` endpoint as the proof.
- The proof sequence is strict: first command completed, cold query `200` with count and ETag, warm query `304`, second command completed, old ETag becomes stale with changed-count `200`, new ETag then returns `304`.
- Keep the query identity identical across all query calls. Tenant, domain, aggregate ID, projection type, entity ID, query type, payload ID, and JWT tenant/domain claims must line up.
- ETag validation starts only after a successful count assertion. A `304` before state is proven is a false positive.
- The original ETag is the stale validator after the second command. Keep sending that original ETag until the new projection count appears with a different ETag.
- The new ETag is the re-warm validator. The final `304` must use the post-change ETag, not the baseline ETag.
- If `200 OK` with the old count appears after the second command, keep polling until the deadline; that is eventual consistency, not success.
- Do not turn malformed, wildcard, mixed-projection, or ETag actor failure behavior into hard errors. Those fail-open semantics are existing product behavior and must remain unchanged.
- Prefer stable public outcomes over private cache-field inspection. Logs/traces are useful evidence, but the test should not become brittle by depending on exact event ordering that the HTTP contract does not expose.
- If the proof fails because DAPR service invocation is blocked, projection actors are not registered, or sidecars are unavailable, record the blocker and route remediation to the owning projection/infrastructure story.

### Suggested File Touches

- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/QueryCacheTopologyProofE2ETests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Helpers/ContractTestHelpers.cs` only if helper extraction avoids duplication without hiding explicit query identity
- `_bmad-output/implementation-artifacts/post-epic-9-r9a2-query-cache-topology-proof.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Architecture And Version Notes

- Package versions are centrally pinned in `Directory.Packages.props`: Aspire Hosting `13.2.2`, Aspire Hosting Testing `13.2.1`, Dapr `1.17.7`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `5.3.0`.
- Aspire integration tests use `DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_EventStore_AppHost>()`; prefer the existing fixture pattern over shelling out to `aspire run` from inside a test.
- Query authentication uses dev-mode JWTs in Tier 3 tests. Query tokens need tenant/domain claims plus `query:read`; command submit/status helpers use command permissions.
- Self-routing ETags are HTTP header validators, not response body fields. Preserve server-provided quoting when sending `If-None-Match`.
- Query actor IDs and ETag actor IDs are separate contracts: query actors are scoped by projection/query identity and entity or payload, while ETag actors are scoped by `{projectionType}:{tenantId}`.

### Previous Story Intelligence

- Epic 9 retro R9-A2 explicitly asks for Aspire query-cache topology verification covering cold query, ETag header, warm `304`, projection change invalidation, and cache re-warm.
- R10-A8 reconciled R9-A2 as `needs-new-tracking` because R11-A3/R11-A4 prove adjacent command-to-query and ETag behavior, but not the complete topology sequence named by R9-A2.
- R9-A1 must remain separate and focused on stale HTTP validator proof. This story may share helpers with R9-A1 but must not collapse R9-A1 into this broader proof.
- R9-A8 remains separate and owns query latency/NFR operational evidence. This story can record pass/fail timings as diagnostics, but it should not create latency benchmarks or NFR acceptance claims.

### Project Structure Notes

- Keep new Tier 3 proof tests under `tests/Hexalith.EventStore.IntegrationTests/ContractTests/`.
- Keep shared HTTP/auth helpers under `tests/Hexalith.EventStore.IntegrationTests/Helpers/`.
- Keep production query/ETag/cache logic in the existing `src/Hexalith.EventStore` and `src/Hexalith.EventStore.Server` boundaries if a real defect is found.
- Do not initialize nested submodules.
- Do not edit generated preflight JSON audit files.

## References

- `_bmad-output/implementation-artifacts/epic-9-retro-2026-04-30.md` - R9-A2 source action and topology-proof gap.
- `_bmad-output/implementation-artifacts/post-epic-10-r10a8-r9-r10-follow-through-tracking.md` - reconciliation that opened this backlog row.
- `_bmad-output/implementation-artifacts/post-epic-9-r9a1-http-stale-etag-proof.md` - sibling stale-validator story and scope boundary.
- `_bmad-output/implementation-artifacts/post-epic-11-r11a4-valid-projection-round-trip.md` - adjacent valid projection and current-ETag conditional request proof.
- `docs/reference/query-api.md` - public Query API, conditional request, projection invalidation, and SignalR responsibility documentation.
- `src/Hexalith.EventStore/Controllers/QueriesController.cs` - Gate 1 query endpoint and `If-None-Match` behavior.
- `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs` - Gate 2 in-memory ETag cache behavior.
- `src/Hexalith.EventStore.Server/Actors/ETagActor.cs` - persisted self-routing ETag state.
- `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs` - ETag actor lookup and fail-open behavior.
- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` - projection actor routing.
- `src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs` - actor ID derivation.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/ValidProjectionRoundTripE2ETests.cs` - existing Tier 3 proof pattern to reuse.
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractTestFixture.cs` - existing Aspire integration-test fixture.
- `tests/Hexalith.EventStore.IntegrationTests/Helpers/ContractTestHelpers.cs` - command/status helper patterns and query helper limitation.

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent.

### Debug Log References

### Completion Notes List

### File List

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-03 | 0.1 | Created ready-for-dev R9-A2 query cache topology proof story. | Codex automation |
