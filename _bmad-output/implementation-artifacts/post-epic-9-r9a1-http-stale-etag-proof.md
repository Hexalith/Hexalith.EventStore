# Post-Epic-9 R9-A1: HTTP Stale ETag Proof

Status: ready-for-dev

<!-- Source: epic-9-retro-2026-04-30.md - R9-A1 -->
<!-- Source: post-epic-10-r10a8-r9-r10-follow-through-tracking.md - R9/R10 reconciliation -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a QA-focused query-pipeline maintainer,
I want a repeatable full-HTTP proof that self-routing ETags return 304 when current and re-query when stale,
so that Epic 9 cache confidence is backed by running HTTP behavior after a real projection change.

## Story Context

Epic 9 delivered the query pipeline and self-routing ETag design, but its retrospective left one high-priority gap: no artifact directly proves the full HTTP behavior where a valid `If-None-Match` returns `304 NotModified` and that same ETag becomes stale after a projection change, causing a fresh `200 OK` query response. Later R11 evidence covers adjacent behavior, but not the exact stale-validator sequence R9-A1 asks for.

Existing evidence to reuse:

- R11-A4 added `ValidProjectionRoundTripE2ETests`, proving command-to-query projection delivery and same-ETag conditional behavior after a successful count assertion.
- R11-A3 records AppHost evidence for ETag regeneration after command processing, but it is a broader manual/runtime proof and does not directly assert stale `If-None-Match` after a projection change.
- The current query controller decodes self-routing ETags from `If-None-Match`, skips Gate 1 for wildcard/malformed/mixed-projection headers, returns `304` only when the decoded projection actor's current ETag matches, and otherwise proceeds through normal query routing.

This story is a focused confidence proof, not a new query feature. Prefer extending the existing Tier 3 contract-test pattern with a narrow scenario rather than changing product behavior. If the proof fails, diagnose and route the defect; do not hide the failure by weakening the oracle.

## Acceptance Criteria

1. **A focused full-HTTP stale ETag proof exists.** Add or extend a Tier 3 integration test under `tests/Hexalith.EventStore.IntegrationTests/ContractTests/`, using the existing `AspireContractTests` collection and `AspireContractTestFixture`, that drives `POST /api/v1/commands`, `GET /api/v1/commands/status/{correlationId}`, and `POST /api/v1/queries` through the running EventStore HTTP surface.

2. **The proof uses one stable query identity.** Every query in the scenario uses the same tenant, domain, aggregate ID, projection type, entity ID, query type, and payload. Use the sample counter path unless a better existing fixture is already available: tenant `tenant-a` or `sample-tenant`, domain `counter`, projection type `counter`, query type `get-counter-status`, and a unique aggregate ID with `entityId = aggregateId`.

3. **The baseline query proves projected state before ETag checks.** Submit one `IncrementCounter` command, poll command status until `Completed`, assert event evidence such as `eventCount > 0` when present, then poll `POST /api/v1/queries` until `200 OK` returns the expected projected count and an `ETag` header. Do not send `If-None-Match` before the scenario has proven the target aggregate's projected state.

4. **A current self-routing ETag returns HTTP 304.** Re-send the exact same query with `If-None-Match` set to the quoted ETag from the successful baseline query. The expected result is `304 NotModified` with no query response body requirement. If the endpoint returns `200 OK`, the response must still be investigated and must not be counted as the AC #4 pass unless the test documents a legitimate concurrent projection change tied to the same aggregate.

5. **A stale ETag re-queries after projection change.** Submit a second `IncrementCounter` command for the same aggregate, poll command status until `Completed`, then repeatedly send the same query with the original baseline ETag in `If-None-Match` until projection delivery is observed. The passing final state is `200 OK` with the incremented projected count and an `ETag` header different from the original baseline ETag. A final `304 NotModified` after the second projection is delivered is a failure.

6. **Eventual consistency is bounded and diagnostic.** During the post-change poll window, temporary `404 NotFound`, `200 OK` with the old count, missing ETag, or parse errors are retry states only until the deadline. On timeout, the failure message must include tenant, domain, aggregate ID, projection type, query type, original ETag, last status code, last response body, parsed count, parse error, and last observed ETag.

7. **ETag formatting and self-routing are preserved.** Capture and assert that the ETag value is a strong quoted HTTP validator whose inner value follows the self-routing `{base64url(projectionType)}.{guid}` shape and decodes to the expected projection type through existing helpers or equivalent test-side verification. Do not rely on old-format GUID-only ETags.

8. **The stale-validator proof does not broaden into R9-A2.** This story may prove one cold baseline, one warm `304`, one projection change, and one stale-validator re-query. It must not absorb the full Aspire query-cache topology sequence, cache re-warm evidence, query latency benchmarks, SignalR delivery, Redis backplane behavior, or operational evidence patterns. R9-A2 and R9-A8 own those follow-ups.

9. **Existing query and projection tests are not weakened.** Preserve `ValidProjectionRoundTripE2ETests`, `ProjectionMalformedResponseE2ETests`, and controller/unit tests. Shared helper extraction is allowed only when it keeps explicit query identity, token permissions, bounded waits, and diagnostic failures at least as strong as the existing tests.

10. **The proof is repeatable from the command line.** Record the targeted command in this story's Dev Agent Record, for example `dotnet test tests/Hexalith.EventStore.IntegrationTests --filter "FullyQualifiedName~HttpStaleETagProofE2ETests"`. If Docker, DAPR placement/scheduler, Aspire startup, or access-control policy blocks execution, record the exact blocker instead of marking the story complete.

11. **No production behavior is changed without a real defect.** The expected implementation is test/evidence work. Product code changes are allowed only if the focused proof exposes a real stale-ETag defect; any such change must include focused unit coverage for the defective branch and must preserve fail-open behavior for malformed, wildcard, mixed-projection, or unavailable ETag actors.

12. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and `last_updated` names the stale ETag proof result. At code-review signoff, both become `done`.

## Tasks / Subtasks

- [ ] Task 1: Add the focused Tier 3 scenario (AC: #1, #2)
    - [ ] Create `HttpStaleETagProofE2ETests` or add a clearly named test to an existing focused ETag/projection contract-test file.
    - [ ] Use `[Trait("Category", "E2E")]`, `[Trait("Tier", "3")]`, and `[Collection("AspireContractTests")]`.
    - [ ] Use `AspireContractTestFixture.EventStoreClient`; do not launch a separate AppHost from inside the test.

- [ ] Task 2: Prove the baseline projected state and current ETag (AC: #2, #3, #7)
    - [ ] Submit the first `IncrementCounter` through `POST /api/v1/commands`.
    - [ ] Poll command status to terminal success and fail on terminal non-success status with the final status body.
    - [ ] Poll `POST /api/v1/queries` with explicit `projectionType = "counter"` and `entityId = aggregateId` until the expected count and ETag are present.
    - [ ] Parse direct-object and base64-string query payload forms, matching the existing `CounterQueryService` and R11-A4 test pattern.
    - [ ] Assert the returned ETag is quoted, self-routing, and decodes to `counter`.

- [ ] Task 3: Assert the current ETag 304 path (AC: #4)
    - [ ] Re-send the exact same query with `If-None-Match` set to the baseline ETag exactly as the server returned it.
    - [ ] Assert `304 NotModified`.
    - [ ] Preserve enough response detail to diagnose an unexpected `200 OK`, `404 NotFound`, or missing ETag.

- [ ] Task 4: Assert the stale ETag re-query path after projection change (AC: #5, #6)
    - [ ] Submit a second `IncrementCounter` for the same aggregate and wait for `Completed`.
    - [ ] Poll the same query with the original baseline ETag in `If-None-Match`.
    - [ ] Treat pre-delivery `404`, old count, parse errors, or missing ETag as bounded retry states.
    - [ ] Pass only when `200 OK` returns the incremented count and a new ETag different from the original.
    - [ ] Fail with a diagnostic message if `304` remains after the new projected count should be visible.

- [ ] Task 5: Keep helper changes narrow (AC: #8, #9, #11)
    - [ ] Reuse or extract query-request and payload parsing helpers only if they preserve explicit identity and diagnostics.
    - [ ] Do not modify R9-A2/R9-A8 scope, SignalR tests, Redis backplane tests, or production cache topology solely for this proof.
    - [ ] If production code changes are required, add focused unit coverage around the defective stale-validator branch.

- [ ] Task 6: Validate and record evidence (AC: #10, #12)
    - [ ] Run the targeted stale ETag proof test.
    - [ ] If shared helpers changed, run affected neighbors such as `ValidProjectionRoundTripE2ETests` and `ProjectionMalformedResponseE2ETests`.
    - [ ] Run the relevant unit slice if production query/ETag code changed.
    - [ ] Record commands, results, environment caveats, and blockers in this story's Dev Agent Record.

## Dev Notes

### Existing Implementation To Reuse

- `src/Hexalith.EventStore/Controllers/QueriesController.cs` is the HTTP boundary for Gate 1. It reads `If-None-Match`, decodes self-routing ETags, calls `IETagService.GetCurrentETagAsync(decodedProjectionType, request.Tenant)`, returns `304` only when the current ETag matches, and otherwise sends `SubmitQuery` through MediatR.
- `src/Hexalith.EventStore.Server/Queries/SelfRoutingETag.cs` owns ETag encode/decode. Keep it the source of truth for self-routing format; do not duplicate production encode/decode logic in product code.
- `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs` derives ETag actor IDs as `{projectionType}:{tenantId}` and fails open to `null` on actor errors. Do not bypass it with direct actor proxies in the HTTP proof.
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractTestFixture.cs` already starts the full Aspire topology with `EnableKeycloak=false`, waits for `eventstore`, `eventstore-admin`, and `sample`, and exposes the EventStore `HttpClient`.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/ValidProjectionRoundTripE2ETests.cs` already demonstrates the public command-to-query path, explicit counter query identity, bounded projection polling, payload parsing, ETag capture, and current-ETag conditional request pattern.
- `tests/Hexalith.EventStore.IntegrationTests/Helpers/ContractTestHelpers.cs` has command submit/status helpers. Its `CreateQueryRequest` does not serialize `ProjectionType` or `EntityId`, so a local explicit query builder or a carefully extended helper is required for this proof.

### Implementation Guardrails

- Use the public HTTP surfaces only. Do not seed actor state, call `IETagActor` directly, invoke `EventReplayProjectionActor` directly, or call the sample `/project` endpoint as the proof.
- Preserve one proof identity across both commands and all queries. Tenant, domain, aggregate ID, projection type, entity ID, query type, payload ID, and JWT tenant/domain claims must line up.
- ETag validation starts only after a successful count assertion. A `304` before state is proven is a false positive.
- The original ETag is the stale validator after the second command. The proof should keep sending that original ETag until the new projection count appears with a different ETag.
- If the query returns `200 OK` with the old count after the second command, keep polling until the deadline; that is eventual consistency, not success.
- If the final result is `304` after the second command's projection is visible, treat it as a cache invalidation defect.
- Do not turn malformed, wildcard, mixed-projection, or ETag actor failure behavior into hard errors. Those fail-open semantics are existing product behavior and must remain unchanged.
- Keep diagnostics more valuable than the happy path. Stale-cache failures are difficult to reproduce without the last status body, query body, parsed count, and observed ETags.

### Suggested File Touches

- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/HttpStaleETagProofE2ETests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Helpers/ContractTestHelpers.cs` only if helper extraction avoids duplication without hiding explicit query identity
- `_bmad-output/implementation-artifacts/post-epic-9-r9a1-http-stale-etag-proof.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Architecture And Version Notes

- Package versions are centrally pinned in `Directory.Packages.props`; do not add test packages for this story unless existing xUnit, Shouldly, and Aspire integration-test infrastructure cannot express the proof.
- Query authentication uses dev-mode JWTs in Tier 3 tests. Query tokens need tenant and domain claims plus `query:read`; command submit/status helpers use command permissions.
- Self-routing ETags are HTTP header validators, not response body fields. Preserve server-provided quoting when sending `If-None-Match`.
- The ETag actor is coarse-grained by projection type and tenant. A counter aggregate update regenerates the `counter:{tenant}` ETag and may invalidate other counter queries for that tenant; the proof's count assertion must therefore be tied to its unique aggregate ID.

### Previous Story Intelligence

- R9-A1 exists because Epic 9 had strong unit/integration-slice coverage but lacked the full running HTTP stale-validator proof.
- R11-A4's valid projection test proves the command-to-query path and current-ETag conditional request. This story should reuse that structure and add the missing second-command stale-validator branch.
- R10-A8 intentionally created this backlog row because later R11 evidence did not directly prove stale `If-None-Match` after projection change.
- R9-A2 remains separate and should cover the broader query-cache topology sequence: cold query, ETag header, warm `304`, projection invalidation, and cache re-warm.

### Project Structure Notes

- Keep new Tier 3 proof tests under `tests/Hexalith.EventStore.IntegrationTests/ContractTests/`.
- Keep shared integration-test helpers under `tests/Hexalith.EventStore.IntegrationTests/Helpers/`.
- Keep production query/ETag logic in the existing `src/Hexalith.EventStore` and `src/Hexalith.EventStore.Server` boundaries if a real defect is found.
- Do not initialize nested submodules.
- Do not edit generated preflight JSON audit files.

## References

- `_bmad-output/implementation-artifacts/epic-9-retro-2026-04-30.md` - R9-A1 source action and stale ETag proof gap.
- `_bmad-output/implementation-artifacts/post-epic-10-r10a8-r9-r10-follow-through-tracking.md` - R9/R10 reconciliation that opened this backlog row.
- `_bmad-output/implementation-artifacts/post-epic-11-r11a4-valid-projection-round-trip.md` - adjacent valid projection and current-ETag conditional request proof.
- `_bmad-output/implementation-artifacts/post-epic-11-r11a3-apphost-projection-proof.md` - adjacent AppHost ETag regeneration evidence.
- `_bmad-output/implementation-artifacts/18-3-query-endpoint-with-etag-pre-check-and-cache.md` - Gate 1/Gate 2 design and fail-open semantics.
- `_bmad-output/implementation-artifacts/18-7-self-routing-etag-format-and-endpoint-decode.md` - self-routing ETag format and endpoint decode behavior.
- `_bmad-output/planning-artifacts/epics.md` - FR51, FR53, FR54, FR61, NFR35-NFR37 query/ETag requirements.
- `_bmad-output/planning-artifacts/prd.md` - Query API, `ETag` / `If-None-Match`, and self-routing cache success criteria.
- `src/Hexalith.EventStore/Controllers/QueriesController.cs` - current query endpoint behavior.
- `src/Hexalith.EventStore.Server/Queries/SelfRoutingETag.cs` - self-routing ETag encode/decode utility.
- `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs` - ETag actor lookup and fail-open behavior.
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
| 2026-05-03 | 0.1 | Created ready-for-dev R9-A1 HTTP stale ETag proof story. | Codex automation |
