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

2. **The proof uses one stable query identity.** Every query in the scenario uses the same tenant, domain, aggregate ID, projection type, entity ID, query type, query route, request headers that define query identity, and serialized payload shape. Use the sample counter path unless a better existing fixture is already available: tenant `tenant-a` or `sample-tenant`, domain `counter`, projection type `counter`, query type `get-counter-status`, and a unique aggregate ID with `entityId = aggregateId`. Prefer a test-isolated tenant when the dev auth and sample query path allow it; otherwise document that the `AspireContractTests` collection serializes same-tenant counter proofs so another same-tenant `counter` update cannot invalidate the coarse `counter:{tenant}` ETag between the baseline query and current-ETag assertion. The command and query tokens must carry tenant/domain claims aligned with that identity, with command submit/status calls using command permissions and query calls using `query:read`.

3. **The baseline query proves projected state before ETag checks.** Submit one `IncrementCounter` command, poll command status until `Completed`, assert event evidence such as `eventCount > 0` when present, then separately poll `POST /api/v1/queries` until `200 OK` returns the expected baseline projected count, the expected query identity, and an `ETag` header. Capture the exact serialized query payload or a deterministic payload fingerprint at this point so later requests can prove they are byte-equivalent for cache identity purposes. Do not send `If-None-Match` before the target aggregate's projected state has been proven by the expected count and identity.

4. **A current self-routing ETag returns HTTP 304.** Re-send the exact same query with `If-None-Match` set to the quoted ETag from the successful baseline query. The expected result is `304 NotModified` with no query response body requirement. If the endpoint returns `200 OK`, the response must still be investigated and must not be counted as the AC #4 pass unless the test documents a legitimate concurrent projection change tied to the same aggregate; unrelated same-tenant projection churn is a repeatability defect or environment blocker, not a success path.

5. **A stale ETag re-queries after projection change.** Submit a second `IncrementCounter` command for the same aggregate, poll command status until `Completed`, then repeatedly send the same byte-equivalent query with the original baseline ETag in `If-None-Match` until projection delivery is observed. The observable state delta is the same aggregate's projected counter value increasing by exactly one from the baseline count. The passing final state is `200 OK` with the incremented projected count and an `ETag` header different from the original baseline ETag, proving that the coarse `counter:{tenant}` validator changed while the query identity stayed fixed. A final `304 NotModified` after the second projection is delivered is a failure.

6. **Eventual consistency is bounded and diagnostic.** During projection catch-up poll windows, temporary `404 NotFound`, `200 OK` with the old count, missing ETag, or parse errors are retry states only until the deadline. Fail fast on unexpected authorization, routing, serialization, or command terminal-failure responses because those are not projection-lag signals. On timeout or stale-validator failure, the failure message must include tenant, domain, aggregate ID, projection type, entity ID, query type, serialized payload identity or fingerprint, command correlation IDs, expected count, last status code, last response body snippet, parsed count, parse error, original ETag, last observed ETag, observed status/count/ETag history, elapsed polling duration, and any detected same-tenant/projection concurrency that could have invalidated `counter:{tenant}`. Do not dump broad Aspire logs unless the assertion fails and the failure message references the relevant excerpt.

7. **ETag formatting and self-routing are preserved.** Capture and assert that the ETag value is a strong quoted HTTP validator whose inner value follows the self-routing `{base64url(projectionType)}.{guid}` shape and decodes to the expected projection type through existing helpers or equivalent test-side verification. The assertion must reject weak validators such as `W/"..."`, unquoted values, missing dot separators, invalid base64url projection prefixes, non-GUID suffixes, old-format GUID-only ETags, and ETags produced by changing query identity. A test-side shape check equivalent to `"^[A-Za-z0-9_-]+\\.[0-9a-fA-F-]{36}$"` is acceptable after removing the surrounding quotes.

8. **The stale-validator proof does not broaden into R9-A2.** This story may prove one cold baseline, one warm `304`, one projection change, and one stale-validator re-query for one stable self-routing ETag path. It must not absorb the full Aspire query-cache topology sequence, cache re-warm evidence, query latency benchmarks, SignalR delivery, Redis backplane behavior, distributed invalidation, multi-node behavior, reverse-proxy cache behavior, metrics, dashboards, runbooks, or operational evidence patterns. R9-A2 and R9-A8 own those follow-ups.

9. **Existing query and projection tests are not weakened.** Preserve `ValidProjectionRoundTripE2ETests`, `ProjectionMalformedResponseE2ETests`, and controller/unit tests. Prefer a single focused, searchable stale-ETag contract test instead of broadening adjacent tests until failures are hard to diagnose. Shared helper extraction is allowed only when it keeps explicit query identity, token permissions, bounded waits, and diagnostic failures at least as strong as the existing tests.

10. **The proof is repeatable from the command line.** Record the targeted command in this story's Dev Agent Record, for example `dotnet test tests/Hexalith.EventStore.IntegrationTests --filter "FullyQualifiedName~HttpStaleETagProofE2ETests"`, plus the test class/method name, pass/fail result, environment caveats, and any timeout diagnostics observed. If Docker, DAPR placement/scheduler, Aspire startup, or access-control policy blocks execution, record the exact blocker instead of marking the story complete.

11. **No production behavior is changed without a real defect.** The expected implementation is test/evidence work. Product code changes are allowed only if the focused proof exposes a real stale-ETag defect in `QueriesController`, `SelfRoutingETag`, `DaprETagService`, or their direct HTTP integration. Before changing production code, record the observed HTTP evidence in this story's Dev Agent Record. Any such change must include focused unit coverage for the defective branch and must preserve fail-open behavior for malformed, wildcard, mixed-projection, or unavailable ETag actors.

12. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and `last_updated` names the stale ETag proof result. At code-review signoff, both become `done`.

## Tasks / Subtasks

- [ ] Task 1: Add the focused Tier 3 scenario (AC: #1, #2)
    - [ ] Create `HttpStaleETagProofE2ETests` or add a clearly named test to an existing focused ETag/projection contract-test file.
    - [ ] Use `[Trait("Category", "E2E")]`, `[Trait("Tier", "3")]`, and `[Collection("AspireContractTests")]`.
    - [ ] Use `AspireContractTestFixture.EventStoreClient`; do not launch a separate AppHost from inside the test.
    - [ ] Build the query request explicitly or extend `ContractTestHelpers.CreateQueryRequest` so `ProjectionType` and `EntityId` are serialized; do not rely on a helper path that omits them.
    - [ ] Use a unique aggregate ID and, when supported by the fixture, a test-isolated tenant; if the tenant must be shared, rely on the collection serialization and record that assumption in the test name or failure diagnostics.

- [ ] Task 2: Prove the baseline projected state and current ETag (AC: #2, #3, #7)
    - [ ] Submit the first `IncrementCounter` through `POST /api/v1/commands`.
    - [ ] Poll command status to terminal success and fail on terminal non-success status with the final status body.
    - [ ] Poll `POST /api/v1/queries` with explicit `projectionType = "counter"` and `entityId = aggregateId` until the expected count and ETag are present.
    - [ ] Parse direct-object and base64-string query payload forms, matching the existing `CounterQueryService` and R11-A4 test pattern.
    - [ ] Preserve the existing integration-test query payload convention; do not introduce a new serialization shape only for this proof.
    - [ ] Capture the deterministic serialized query payload or fingerprint and reuse it for the current and stale `If-None-Match` requests.
    - [ ] Assert the returned ETag is quoted, self-routing, and decodes to `counter`.
    - [ ] Assert the command and query authorization token claims match the tenant/domain under test and include `query:read` for query calls.

- [ ] Task 3: Assert the current ETag 304 path (AC: #4)
    - [ ] Re-send the exact same query with `If-None-Match` set to the baseline ETag exactly as the server returned it.
    - [ ] Assert `304 NotModified`.
    - [ ] Preserve enough response detail to diagnose an unexpected `200 OK`, `404 NotFound`, missing ETag, or same-tenant/projection invalidation between baseline and revalidation.

- [ ] Task 4: Assert the stale ETag re-query path after projection change (AC: #5, #6)
    - [ ] Submit a second `IncrementCounter` for the same aggregate and wait for `Completed`.
    - [ ] Poll the same byte-equivalent query with the original baseline ETag in `If-None-Match`.
    - [ ] Treat pre-delivery `404`, old count, parse errors, or missing ETag as bounded retry states.
    - [ ] Pass only when `200 OK` returns the incremented count and a new ETag different from the original.
    - [ ] Fail with a diagnostic message if `304` remains after the new projected count should be visible.
    - [ ] Keep command-completion waits separate from projection-visibility waits; command completion alone is not projection delivery.

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
- Do not assert through repositories, direct DAPR state APIs, database reads, in-memory test hooks, projection internals, or product service instances. The proof boundary is HTTP command submission, public command-status observation, and HTTP query revalidation.
- Preserve one proof identity across both commands and all queries. Tenant, domain, aggregate ID, projection type, entity ID, query type, payload ID, and JWT tenant/domain claims must line up.
- Treat the ETag actor's `projectionType:{tenant}` scope as part of the test design. Avoid concurrent same-tenant `counter` writes between the baseline and current-ETag request, or record the environment as blocked/flaky instead of accepting a false negative or false positive.
- ETag validation starts only after a successful count assertion. A `304` before state is proven is a false positive.
- The original ETag is the stale validator after the second command. The proof should keep sending that original ETag until the new projection count appears with a different ETag.
- If the query returns `200 OK` with the old count after the second command, keep polling until the deadline; that is eventual consistency, not success.
- If the final result is `304` after the second command's projection is visible, treat it as a cache invalidation defect.
- Do not turn malformed, wildcard, mixed-projection, or ETag actor failure behavior into hard errors. Those fail-open semantics are existing product behavior and must remain unchanged.
- Keep diagnostics more valuable than the happy path. Stale-cache failures are difficult to reproduce without the last status body, query body, parsed count, and observed ETags.
- Defer weak-vs-strong policy outside this endpoint family, route-prefix encoding versioning, multi-projection invalidation guarantees, and replica-wide stale-validator semantics to R9-A2/R9-A8 or a separate architecture decision.

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

### Party-Mode Review

- Date: 2026-05-03T13:39:02+02:00
- Selected story key: `post-epic-9-r9a1-http-stale-etag-proof`
- Command/skill invocation used: `/bmad-party-mode post-epic-9-r9a1-http-stale-etag-proof; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: All reviewers recommended `needs-story-update`, focused on making the stale-validator proof mechanically exact before development. The shared concerns were stable serialized query identity, explicit query helper serialization for `ProjectionType`/`EntityId`, separate command-completion and projection-visibility waits, exact strong self-routing ETag shape checks, bounded diagnostics, auth claim alignment, and scope protection from R9-A2/R9-A8.
- Changes applied: Tightened AC #2-#11 and related tasks/guardrails for auth claims, query identity, ETag regex/prefix validation, second-command count delta, retry/fail-fast semantics, diagnostic fields, helper limitations, public-HTTP-only proof boundaries, evidence recording, and production-defect escalation.
- Findings deferred: Broader cache topology, cache re-warm behavior, multi-node or replica semantics, reverse-proxy behavior, operational dashboards/log evidence, weak-vs-strong policy beyond this endpoint family, and route-prefix encoding versioning remain outside R9-A1 and belong to R9-A2/R9-A8 or a separate architecture decision.
- Final recommendation: needs-story-update

### Advanced Elicitation

- Date: 2026-05-04T11:02:22+02:00
- Selected story key: `post-epic-9-r9a1-http-stale-etag-proof`
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-9-r9a1-http-stale-etag-proof`
- Batch 1 method names: Self-Consistency Validation; Red Team vs Blue Team; Security Audit Personas; Failure Mode Analysis; Comparative Analysis Matrix
- Reshuffled Batch 2 method names: Chaos Monkey Scenarios; Occam's Razor Application; First Principles Analysis; 5 Whys Deep Dive; Lessons Learned Extraction
- Findings summary: The story was already strong after party-mode review, but elicitation found one repeatability trap: the ETag actor is scoped by projection type and tenant, so same-tenant counter writes outside this proof can invalidate the current ETag before the 304 assertion. It also found that stable query identity needed an observable serialized payload/fingerprint, and that command/status authorization should stay distinct from query authorization in the proof.
- Changes applied: Added tenant/projection isolation guidance, deterministic query payload/fingerprint capture, byte-equivalent query reuse for both current and stale validator requests, command-vs-query token wording, same-tenant/projection concurrency diagnostics, and a guardrail that unrelated validator churn is an environment blocker rather than a passing condition.
- Findings deferred: Broader cross-aggregate invalidation semantics, replica-wide validator consistency, weak-validator policy outside this endpoint family, and multi-node cache topology remain outside R9-A1 and belong to R9-A2/R9-A8 or a separate architecture decision.
- Final recommendation: ready-for-dev

### Agent Model Used

TBD by dev-story agent.

### Debug Log References

### Completion Notes List

### File List

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-04 | 0.3 | Advanced elicitation hardened tenant/projection isolation, byte-equivalent query identity, and stale-validator diagnostics. | Codex automation |
| 2026-05-03 | 0.2 | Party-mode review tightened stale ETag proof identity, polling, diagnostics, auth, and scope guardrails. | Codex automation |
| 2026-05-03 | 0.1 | Created ready-for-dev R9-A1 HTTP stale ETag proof story. | Codex automation |
