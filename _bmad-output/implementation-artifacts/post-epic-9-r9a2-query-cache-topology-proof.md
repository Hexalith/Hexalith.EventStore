# Post-Epic-9 R9-A2: Query Cache Topology Proof

Status: done

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
- The current ETag actor scope is `{projectionType}:{tenantId}`, not aggregate/domain/entity. The proof must use a unique aggregate ID and a targeted Aspire test run, and any unexpected validator churn from another `counter` projection update in `tenant-a` is diagnostic evidence, not a successful cache-topology transition for this story's aggregate.

## Acceptance Criteria

1. **A focused Aspire topology proof exists.** Add one ordered Tier 3 integration-test scenario under `tests/Hexalith.EventStore.IntegrationTests/ContractTests/`, using `[Collection("AspireContractTests")]` and `AspireContractTestFixture`, that drives only the public EventStore HTTP surface through the running Aspire topology. The passing evidence is the automated test result plus its Dev Agent Record notes, not manual Aspire screenshots or exploratory log inspection.

2. **The proof uses one stable query identity.** The scenario uses the same tenant, domain, aggregate ID, projection type, entity ID, query type, payload, and JWT tenant/domain claims across commands and all queries. The mandatory identity tuple is tenant `tenant-a`, domain `counter`, projection type `counter`, query type `get-counter-status`, one unique aggregate ID per test run, `entityId = aggregateId`, payload ID aligned to the aggregate ID, and JWT tenant/domain claims that authorize that exact identity. Because ETag state is currently scoped by `{projectionType}:{tenantId}`, the test must be targeted and serialized through the existing Aspire contract-test collection; it must not infer aggregate-specific cache success from validator changes caused by unrelated `tenant-a` `counter` activity.

3. **The cold query populates the topology.** Submit the first `IncrementCounter` command through `POST /api/v1/commands`, poll the public command-status endpoint until `Completed`, assert persisted-event evidence such as `eventCount > 0` when present, then poll `POST /api/v1/queries` until `200 OK` returns expected count `1` and an `ETag` response header. This first successful query is the cold path for the selected identity.

4. **The returned ETag is self-routing and current.** Assert that the first successful response includes a strong quoted HTTP validator whose inner value follows the self-routing `{base64url(projectionType)}.{base64url-guid}` shape and decodes to `counter` through `SelfRoutingETag` or equivalent test-side verification. Do not accept old-format GUID-only ETags.

5. **A warm same-identity request returns 304.** Re-send the exact same query with `If-None-Match` set to the server-provided ETag header exactly as returned, including quotes. The passing result is `304 NotModified`, which proves Gate 1 accepted the current validator before the request entered the projection actor path. If the endpoint returns `200 OK`, the test must record enough response detail to diagnose whether Gate 1 was skipped, the ETag actor returned a different value, unrelated shared-scope validator churn occurred, or a same-aggregate projection changed concurrently.

6. **Projection change invalidates the old ETag.** Submit a second `IncrementCounter` command for the same aggregate through `POST /api/v1/commands` and wait for public command status `Completed`. Then send the same query with the original ETag in `If-None-Match` until the projection change is observed. The passing state is `200 OK` with expected count `2` and an `ETag` different from the original. A final `304 NotModified` after the second projection is visible is a failure.

7. **Cache re-warm is proven after invalidation.** After the changed-count `200 OK` response, re-send the exact same query with the new ETag. The passing result is `304 NotModified`. This is the re-warm proof: the topology must converge to a new current validator after the projection update.

8. **Gate 2 evidence is recorded without brittle internals.** Record evidence that the query actor path was exercised by the cold `200 OK` and post-invalidation `200 OK` query executions, and capture existing structured logs or traces for `Stage=CacheMiss`, `CacheHit`, `ETagPreCheckMatch`, or equivalent stages when accessible from the test/AppHost evidence. Do not treat the warm `304 NotModified` as proof of a Gate 2 cache hit by itself because Gate 1 may short-circuit first. If direct log/trace capture is unavailable in the fixture, document that limitation and keep the public HTTP sequence as the non-brittle topology proof. Do not call projection actors directly, inspect Dapr state directly, add test-only production hooks, or bypass Dapr/ETag actors solely to observe private cache fields.

9. **Eventual consistency is bounded and diagnostic.** During polling windows, temporary `404 NotFound`, old counts, missing ETags, parse errors, transient command/query responses, or unexpected shared-scope ETag churn are retry states only until the deadline. Once a `200 OK` response proves the expected current count, a missing, malformed, same-as-old, wrong-projection, or unrelatedly-changed ETag is a contract failure rather than a transient. Timeout failures must include tenant, domain, aggregate ID, projection type, query type, command IDs, original ETag, new ETag if observed, any unexpected intermediate ETags, last status code, last response body, parsed count, parse error, trace/correlation headers if available, and the phase that timed out.

10. **The proof remains topology-focused.** Do not absorb R9-A1 stale-validator scope beyond what is necessary for this broader sequence, R9-A8 query-latency/operational NFR evidence, SignalR client behavior, Redis backplane behavior, release governance, or new production cache design. This story proves the existing topology or exposes a routed defect.

11. **Existing query/projection tests are not weakened.** Preserve `ValidProjectionRoundTripE2ETests`, `ProjectionMalformedResponseE2ETests`, `QueryEndpointE2ETests`, and query/ETag unit tests. Shared helper extraction is allowed only when explicit query identity, token permissions, bounded waits, ETag handling, and diagnostic failures stay at least as strong as the existing tests.

12. **The proof is repeatable from the command line.** Record the targeted command in this story's Dev Agent Record, for example `dotnet test tests/Hexalith.EventStore.IntegrationTests --filter "FullyQualifiedName~QueryCacheTopologyProofE2ETests"`. If Docker, DAPR placement/scheduler, Aspire startup, access-control policy, dev authentication, public API availability, or a pre-existing shared-topology failure blocks execution, record the exact blocker instead of marking the story complete.

13. **No production behavior is changed without a real defect.** The expected implementation is test/evidence work. Product code changes are allowed only if the focused proof exposes a real cache, ETag, routing, or projection-update defect; any such change must include focused unit coverage for the defective branch and preserve fail-open behavior for malformed, wildcard, mixed-projection, and unavailable ETag actor cases. This story proves the healthy topology path and must not turn ETag infrastructure unavailability into fail-closed production behavior.

14. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and `last_updated` names the query-cache topology proof result. At code-review signoff, both become `done`.

## Tasks / Subtasks

- [x] Task 1: Add the focused Tier 3 topology test (AC: #1, #2)
    - [x] Create `QueryCacheTopologyProofE2ETests` or add a clearly named test to an existing focused query/ETag contract-test file.
    - [x] Prefer a scenario name that states the topology contract, such as `QueryCacheTopology_WhenProjectionChanges_InvalidatesOldETagAndCachesNewETag`.
    - [x] Use `[Trait("Category", "E2E")]`, `[Trait("Tier", "3")]`, and `[Collection("AspireContractTests")]`.
    - [x] Use `AspireContractTestFixture.EventStoreClient`; do not launch a separate AppHost from inside the test.

- [x] Task 2: Prove cold query and initial ETag (AC: #2, #3, #4)
    - [x] Submit the first `IncrementCounter` through `POST /api/v1/commands`.
    - [x] Poll command status to terminal success and fail on terminal non-success status with the command ID and final status body.
    - [x] Poll `POST /api/v1/queries` with explicit `projectionType = "counter"` and `entityId = aggregateId` until expected count `1` and ETag are present.
    - [x] Parse direct-object and base64-string query payload forms, matching the existing `ValidProjectionRoundTripE2ETests` pattern.
    - [x] Assert the returned ETag is quoted, self-routing, and decodes to `counter`.

- [x] Task 3: Prove warm 304 behavior (AC: #5)
    - [x] Re-send the exact same query with `If-None-Match` set to the baseline ETag exactly as the server returned it.
    - [x] Assert `304 NotModified`.
    - [x] Preserve enough response detail to diagnose an unexpected `200 OK`, `404 NotFound`, missing ETag, or mismatched projection type.

- [x] Task 4: Prove invalidation after projection change (AC: #6, #9)
    - [x] Submit a second `IncrementCounter` for the same aggregate and wait for `Completed`.
    - [x] Poll the same query with the original baseline ETag in `If-None-Match`.
    - [x] Treat pre-delivery `404`, old count, parse errors, or missing ETag as bounded retry states.
    - [x] Pass only when `200 OK` returns expected count `2` and a new ETag different from the original.
    - [x] Fail with a diagnostic message if `304` remains after the changed projection is visible.

- [x] Task 5: Prove cache re-warm and record topology evidence (AC: #7, #8)
    - [x] Re-send the exact same query with the new post-change ETag.
    - [x] Assert `304 NotModified`.
    - [x] Capture available structured log, trace, or test-output evidence for `ETagPreCheckMatch`, `CacheMiss`, `CacheHit`, or equivalent stages when accessible.
    - [x] Separate claims carefully: warm `304` proves Gate 1 validator behavior; cold and post-change `200` responses prove the projection actor query path; only explicit logs/traces should be used to claim a Gate 2 cache hit.
    - [x] If log/trace capture is unavailable in the test fixture, record that limitation and keep the public HTTP sequence as the non-brittle proof.

- [x] Task 6: Keep helper and product changes narrow (AC: #10, #11, #13)
    - [x] Reuse or extract query-request, status-poll, payload-parse, and ETag helpers only if they preserve explicit identity and diagnostics.
    - [x] Do not modify R9-A1/R9-A8 scope, SignalR tests, Redis backplane tests, release-governance docs, or production cache design solely for this proof.
    - [x] If production code changes are required, add focused unit coverage around the defective query, ETag, cache, or projection-update branch.

- [x] Task 7: Validate and record evidence (AC: #12, #14)
    - [x] Run the targeted query-cache topology proof test.
    - [x] If shared helpers changed, run affected neighbors such as `ValidProjectionRoundTripE2ETests`, `ProjectionMalformedResponseE2ETests`, and `QueryEndpointE2ETests`.
    - [x] Run the relevant unit slice if production query/ETag/cache code changed.
    - [x] Record commands, results, environment caveats, and blockers in this story's Dev Agent Record.

### Review Findings

Code review 2026-05-04 (3-layer adversarial: Blind Hunter / Edge Case Hunter / Acceptance Auditor). 36 raw findings consolidated to 17 unique. 0 decision-needed, 6 patches, 6 defers, 5 dismissed (+9 absorbed in dedupe).

- [x] [Review][Patch] P1 Submit retry loop swallows 4xx + missing `correlationId` as transient timeouts — added `FailFastSubmitException` thrown on `IsFailFast` status (now includes 422) and on missing/empty `correlationId` with raw body in failure; retry loop catches it before generic `Exception` and re-throws instead of retrying [`QueryCacheTopologyProofE2ETests.cs`] (HIGH; sources: blind+edge)
- [x] [Review][Patch] P2 Post-delivery `304` after second-command `Completed` not classified as sharp failure (AC #6) — `PollUntilProjectedCountAsync` now counts post-phase observations and `NotModified` responses; on timeout, `ClassifyTimeoutFailureMode` emits `POST_DELIVERY_STUCK_AT_304 (AC #6 violation: ...)` when every post-change observation returned `304` [`QueryCacheTopologyProofE2ETests.cs`] (MEDIUM; sources: auditor+edge)
- [x] [Review][Patch] P3 Warm-baseline `304` is single-shot; AC #9 classifies missing/transient ETag as bounded retry — added `AssertWarmNotModifiedAsync` helper with 3-attempt bounded retry (200 ms inter-attempt delay) accepting `304` as terminal success; intermediate observations recorded in `history` for diagnostics [`QueryCacheTopologyProofE2ETests.cs`] (MEDIUM; sources: auditor+edge)
- [x] [Review][Patch] P4 Warm `304` does not assert server-echoed ETag equals baseline — `AssertWarmNotModifiedAsync` now asserts that any ETag header returned with `304` ordinal-equals the baseline, and throws with the diff string when divergent (Gate 1 echo contract enforced) [`QueryCacheTopologyProofE2ETests.cs`] (MEDIUM; source: edge)
- [x] [Review][Patch] P5 `TryAddWithoutValidation` discards return; if `If-None-Match` is rejected the request goes out without the validator and `200` is misclassified as server bug — `CreateProjectionQueryRequest` now wraps allocation in `try/finally`, asserts the return value, and throws `ShouldAssertException` containing the raw UTF-8 hex of the rejected ETag (request disposed on failure) [`QueryCacheTopologyProofE2ETests.cs`] (MEDIUM; sources: blind+edge)
- [x] [Review][Patch] P6 Outer 4-min cancellation throws bare `OperationCanceledException` with no `FailureContext`/phase/history — test body now tracks `currentPhase`/`firstCorrelationId`/`secondCorrelationId`/`originalETag`/`currentExpectedCount` as locals; outer `try/catch (OperationCanceledException) when (cts.IsCancellationRequested)` re-throws as `ShouldAssertException` with full `FailureContext` and the original `OperationCanceledException` as inner [`QueryCacheTopologyProofE2ETests.cs`] (MEDIUM; source: edge)
- [x] [Review][Defer] D1 `eventCount.GetInt32()` does not branch on `ValueKind` for serializer drift [`QueryCacheTopologyProofE2ETests.cs:537-547`] — deferred, diagnostic polish only; server currently emits `Number`
- [x] [Review][Defer] D2 No active mid-test detection of cross-test shared-scope `{counter:tenant-a}` ETag churn [`QueryCacheTopologyProofE2ETests.cs:445-486`] — deferred, mitigated by `[Collection("AspireContractTests")]` serialization; spec acknowledges trade-off
- [x] [Review][Defer] D3 `history` list grows unboundedly; long `FailureContext` may be runner-truncated [`QueryCacheTopologyProofE2ETests.cs:217, 378, 678-682`] — deferred, diagnostic polish only
- [x] [Review][Defer] D4 `QueryObservation.FromResponse` parses count only on `200`; weaker diagnostic on non-`OK` with embedded body [`QueryCacheTopologyProofE2ETests.cs:789-808`] — deferred, diagnostic polish only
- [x] [Review][Defer] D5 `MessageId` submitted as `Guid.NewGuid().ToString()`, not ULID (CLAUDE.md R2-A7 drift) [`QueryCacheTopologyProofE2ETests.cs:212, 504-510`] — deferred, pre-existing pattern in sibling Tier 3 tests; ULID reconciliation is its own backlog item
- [x] [Review][Defer] D6 Outer CT not propagated into shared `PollUntilTerminalStatusAsync` [`QueryCacheTopologyProofE2ETests.cs:295-299`] — deferred, fix requires shared-helper signature change; AC #13 and Dev Agent Record commit to no shared-helper changes in this story

Dismissed (5 unique + 9 deduped): regex `{22}` hardcode (server contract emits 22), `Convert.FromBase64String` in `ParseCountFromPayload` (server emits standard base64; sibling test uses same pattern), 404 not in `IsFailFast` for cold (spec classifies 404 as retry state), `ConfigureAwait` selective removal (correct per xUnit1030 scope), and various theoretical/cosmetic concerns.

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
- The executable proof sequence is one ordered contract: first command completed; cold query returns `200`, count `1`, and ETag A; exact same query with `If-None-Match: A` returns `304`; second command completed; original ETag A returns `200`, count `2`, and ETag B; exact same query with `If-None-Match: B` returns `304`.
- Keep the query identity identical across all query calls. Tenant, domain, aggregate ID, projection type, entity ID, query type, payload ID, and JWT tenant/domain claims must line up.
- ETag validation starts only after a successful count assertion. A `304` before state is proven is a false positive.
- The original ETag is the stale validator after the second command. Keep sending that original ETag until the new projection count appears with a different ETag.
- The new ETag is the re-warm validator. The final `304` must use the post-change ETag, not the baseline ETag.
- The ETag actor scope is currently `{projectionType}:{tenantId}`. Keep the test targeted and avoid parallel shared-tenant `counter` mutators; if an ETag changes without the selected aggregate reaching the expected count, surface that as shared-scope/interference diagnostics rather than passing the topology proof.
- Do not overclaim private cache behavior. Gate 1 `304` responses prove HTTP validator short-circuiting; Gate 2 cache-hit claims require existing log/trace evidence or must be recorded as unavailable.
- If `200 OK` with the old count appears after the second command, keep polling until the deadline; that is eventual consistency, not success.
- Do not turn malformed, wildcard, mixed-projection, or ETag actor failure behavior into hard errors. Those fail-open semantics are existing product behavior and must remain unchanged.
- Prefer stable public outcomes over private cache-field inspection. Logs/traces are useful evidence, but the test should not become brittle by depending on exact event ordering that the HTTP contract does not expose.
- If the proof fails because DAPR service invocation is blocked, projection actors are not registered, or sidecars are unavailable, record the blocker and route remediation to the owning projection/infrastructure story.
- Keep helper extraction local to integration-test infrastructure unless an existing shared helper already fits. Avoid creating a general cache-test framework for this single topology proof.

### Deferred Review Decisions

- Decide outside this story whether this Tier 3 proof should run in normal CI, nightly CI, or an environment-gated integration lane.
- Decide outside this story whether future APIs should expose stronger cache diagnostics; this story must not add diagnostics unless a real defect requires it.
- Decide outside this story whether future integration-test auth should support a test-specific tenant for cache/ETag isolation instead of reusing `tenant-a`.
- Decide outside this story whether cross-tenant/domain negative cache behavior needs a separate acceptance test.
- Do not standardize broader ETag syntax here beyond the current public self-routing header behavior needed by this proof.

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

GPT-5 Codex

### Debug Log References

- 2026-05-04T12:47:00+02:00: `aspire run --detach --non-interactive --apphost .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json` with `EnableKeycloak=false`; Aspire MCP showed `eventstore`, `eventstore-admin`, `sample`, Dapr sidecars, `statestore`, and `pubsub` healthy. Admin UI logged a startup SignalR warning unrelated to the query topology.
- 2026-05-04T12:49:00+02:00: `dotnet test .\tests\Hexalith.EventStore.IntegrationTests\Hexalith.EventStore.IntegrationTests.csproj --filter "FullyQualifiedName~QueryCacheTopologyProofE2ETests"` initially failed at build with xUnit1030 because the test method used `ConfigureAwait(false)`. Removed those calls from the test method body.
- 2026-05-04T12:50:00+02:00: `dotnet test .\tests\Hexalith.EventStore.IntegrationTests\Hexalith.EventStore.IntegrationTests.csproj --filter "FullyQualifiedName~QueryCacheTopologyProofE2ETests"` passed: 1/1.
- 2026-05-04T12:51:00+02:00: Unit slices passed individually: Client 334/334, Contracts 281/281, Sample 63/63, Testing 78/78.
- 2026-05-04T12:52:00+02:00: Combined neighbor filter `FullyQualifiedName~ValidProjectionRoundTripE2ETests|FullyQualifiedName~QueryEndpointE2ETests|FullyQualifiedName~ProjectionMalformedResponseE2ETests` matched no tests and crashed the xUnit process; treated as invalid runner/filter syntax, not product evidence.
- 2026-05-04T12:54:00+02:00: Neighbor filters passed individually: `ValidProjectionRoundTripE2ETests` 1/1, `QueryEndpointE2ETests` 4/4, `ProjectionMalformedResponseE2ETests` 1/1.

### Completion Notes List

- Added `QueryCacheTopologyProofE2ETests` as a focused Tier 3 Aspire contract test for the ordered public HTTP sequence: first command completed, cold query `200` count 1 with quoted self-routing ETag, same-identity warm `304`, second command completed, stale original ETag yields `200` count 2 with a different self-routing ETag, and post-change ETag re-warms to `304`.
- Kept command and query identity explicit and stable: tenant `tenant-a`, domain/projection `counter`, query type `get-counter-status`, unique aggregate/entity ID per run, and command/query payload IDs aligned to the aggregate ID.
- Added local diagnostics for polling failures, observed ETags, trace/correlation headers when returned, serialized query identity, parsed counts, parse errors, command correlation IDs, and shared-scope ETag churn.
- Did not change production query, ETag, cache, SignalR, Redis, or release-governance behavior. No shared test helpers were modified.
- Direct Gate 2 log/trace capture is not exposed through `AspireContractTestFixture` after fixture disposal, so the story records that limitation and uses the non-brittle public HTTP sequence as the proof. The test does not claim a private cache hit without explicit log evidence.

### File List

- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/QueryCacheTopologyProofE2ETests.cs`
- `_bmad-output/implementation-artifacts/post-epic-9-r9a2-query-cache-topology-proof.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Party-Mode Review

- Date/time: 2026-05-03T13:57:23+02:00
- Selected story key: `post-epic-9-r9a2-query-cache-topology-proof`
- Command/skill invocation used: `/bmad-party-mode post-epic-9-r9a2-query-cache-topology-proof; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary: tighten the story so dev proves one ordered public HTTP topology contract instead of a broad or exploratory cache smoke test; pin the stable query identity, expected counts, server-provided quoted ETags, bounded polling, failure diagnostics, and fail-open guardrails.
- Changes applied: clarified ACs for public HTTP-only proof, mandatory identity tuple, expected count transitions, ETag header/`If-None-Match` handling, retry-state classification, environment blockers, fail-open semantics, scenario naming, and helper scope; added deferred review decisions.
- Findings deferred: CI lane policy for Tier 3 proof; future cache diagnostics API shape; cross-tenant/domain negative cache tests; broader ETag syntax standardization.
- Final recommendation: ready-for-dev

### Advanced Elicitation

- Date/time: 2026-05-04T12:12:24+02:00
- Selected story key: `post-epic-9-r9a2-query-cache-topology-proof`
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-9-r9a2-query-cache-topology-proof`
- Batch 1 method names: Self-Consistency Validation; Red Team vs Blue Team; Security Audit Personas; Failure Mode Analysis; Comparative Analysis Matrix
- Reshuffled Batch 2 method names: Chaos Monkey Scenarios; Occam's Razor Application; First Principles Analysis; 5 Whys Deep Dive; Lessons Learned Extraction
- Findings summary: the story was already ready, but needed sharper guardrails around shared `{projectionType}:{tenantId}` ETag scope, Gate 1 versus Gate 2 proof claims, and diagnostics for unexpected validator churn.
- Changes applied: clarified that the targeted test must not pass on unrelated shared-scope ETag changes; separated warm `304` Gate 1 proof from optional Gate 2 cache-hit evidence; expanded timeout diagnostics for intermediate ETags and interference; added a deferred tenant-isolation decision.
- Findings deferred: whether integration-test auth should support per-test tenants; broader cache diagnostics API shape remains outside this story.
- Final recommendation: ready-for-dev

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-04 | 1.1 | Code review applied 6 patches (P1 fail-fast submit, P2 stuck-at-304 classifier, P3 warm-baseline bounded retry, P4 ETag echo assertion, P5 If-None-Match header rejection guard, P6 outer-CTS diagnostic envelope); 6 defers recorded. Targeted test passed 1/1. Story moved review → done. | Claude (code review) |
| 2026-05-04 | 1.0 | Implemented and validated Tier 3 query-cache topology proof; moved story to review. | Codex |
| 2026-05-04 | 0.3 | Applied advanced elicitation hardening for ETag scope, Gate 1/Gate 2 evidence, and diagnostics. | Codex automation |
| 2026-05-03 | 0.2 | Applied party-mode review hardening for R9-A2 topology proof boundaries. | Codex automation |
| 2026-05-03 | 0.1 | Created ready-for-dev R9-A2 query cache topology proof story. | Codex automation |
