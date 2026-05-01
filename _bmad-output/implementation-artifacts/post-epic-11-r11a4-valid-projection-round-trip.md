# Post-Epic-11 R11-A4: Valid Projection Round-Trip

Status: ready-for-dev

<!-- Source: epic-11-retro-2026-04-30.md - Action item R11-A4 -->
<!-- Source: epic-12-retro-2026-04-30.md - R12-A5 carry-forward backlog -->
<!-- Source: docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md - projection update and query flow -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a QA-focused platform engineer hardening the server-managed projection builder,
I want a repeatable integration path proving that a valid sample projection response reaches query results,
so that projection-builder confidence is backed by an automated command-to-query round trip instead of only unit tests, UI smoke notes, or malformed-response failure coverage.

## Story Context

Epic 11 delivered the server-managed projection builder path: EventStore persists events, invokes the sample domain service `/project` endpoint through DAPR, writes the returned `ProjectionState` to `EventReplayProjectionActor`, regenerates ETags, broadcasts projection-change signals, and serves the state through `POST /api/v1/queries`. The retrospective left R11-A4 open because the repo had a malformed `/project` fail-open Tier 3 test, sample handler unit tests, and UI smoke evidence, but no focused automated proof that the valid `/project` path reaches the query endpoint without `QueryNotFoundException`.

This story adds that focused proof. It should be an integration-test/runbook story, not a product feature story. Reuse the existing Aspire topology and the sample counter domain. Do not add another projection implementation, bypass EventStore by calling `/project` directly as the main proof, or fold in R11-A1 checkpointing, R11-A2 polling, or R11-A3 browser/AppHost evidence scope.

## Acceptance Criteria

1. **A valid projection round-trip test exists.** Add a Tier 3 test, for example `ValidProjectionRoundTripE2ETests`, under `tests/Hexalith.EventStore.IntegrationTests/ContractTests/` using the existing `AspireContractTests` collection and `AspireContractTestFixture`.

2. **The test uses the real EventStore command surface.** It submits one or more `IncrementCounter` commands through `POST /api/v1/commands` against tenant `tenant-a` or `sample-tenant`, domain `counter`, and a unique aggregate ID. It must not seed actor state, invoke `EventReplayProjectionActor` directly, or use `CounterProjectionHandler.Project` as the only proof.

3. **Command completion is tied to persisted events.** The test polls `GET /api/v1/commands/status/{correlationId}` until `Completed` and asserts event evidence such as `eventCount > 0` when present. If the helper hides status details, add a helper overload or local assertion rather than accepting only HTTP 202.

4. **The query route waits for eventual projection delivery.** After command completion, the test polls `POST /api/v1/queries` until it receives `200 OK` with the projected counter state. Temporary `404 NotFound` or stale zero results are acceptable during the poll window because projection delivery is fire-and-forget.

5. **The query targets the same actor identity as the write path.** The query payload must set `domain = "counter"`, `projectionType = "counter"`, `queryType = "get-counter-status"`, `aggregateId = <same aggregate>`, and `entityId = <same aggregate>`. Do not rely on a random payload-only Tier 2 route when proving aggregate-scoped projection delivery.

6. **The projected count is asserted from the response body.** After one increment, the query payload count is `1`; after multiple increments, it equals the number of successful increments. The parser must handle the response shape already used by `CounterQueryService`: payload may be a direct JSON object or a base64-encoded JSON object.

7. **ETag behavior is included in the valid path.** The successful query response includes an `ETag` header. A follow-up query with `If-None-Match` for the returned ETag either returns `304 NotModified` or returns `200 OK` with an unchanged count if the projection changed again before the assertion. Do not make the proof depend on old-format ETags.

8. **`QueryNotFoundException` is not the passing path.** The final successful assertion must prove that query routing did not end in `404 NotFound` / `QueryNotFoundException`. If the final poll times out, the failure message must include the last status code and response body to make projection actor lookup failures diagnosable.

9. **The sample `/project` handler remains the real projection source.** The fixture must not set `EventStore__SampleFaults__MalformedProjectResponse=true`. If the test does any optional direct sample-service probe, it must be only a preflight assertion that `/project` is valid JSON and must not replace the EventStore-driven round trip.

10. **Existing malformed-response coverage stays separate.** `ProjectionMalformedResponseE2ETests` remains the fault-path proof. This story may share helpers with it, but must not weaken its assertion that malformed `/project` responses do not break command processing.

11. **The proof is repeatable from the command line.** Record the targeted command in this story's Dev Agent Record, for example `dotnet test tests/Hexalith.EventStore.IntegrationTests --filter "FullyQualifiedName~ValidProjectionRoundTripE2ETests"`. If Docker or DAPR infrastructure is unavailable, record the exact blocker instead of marking the story complete.

12. **No product scope is silently expanded.** Do not implement checkpoint tracking, polling mode, new projection contracts, UI/browser proof, or new public APIs in this story. If the valid round trip fails because one of those areas is defective, capture the blocker and route it to the owning R11 story.

## Tasks / Subtasks

- [ ] Task 1: Add the valid projection Tier 3 test shell (AC: #1, #9, #10)
  - [ ] Create `tests/Hexalith.EventStore.IntegrationTests/ContractTests/ValidProjectionRoundTripE2ETests.cs`.
  - [ ] Use `[Trait("Category", "E2E")]`, `[Trait("Tier", "3")]`, and `[Collection("AspireContractTests")]`.
  - [ ] Use `AspireContractTestFixture.EventStoreClient` for command and query calls.

- [ ] Task 2: Submit and verify real commands (AC: #2, #3)
  - [ ] Submit at least one `IncrementCounter` command against a unique aggregate ID.
  - [ ] Poll status until `Completed`.
  - [ ] Assert persisted-event evidence when present in the status payload.

- [ ] Task 3: Add an eventual query assertion helper (AC: #4, #5, #6, #8)
  - [ ] Poll `POST /api/v1/queries` until `200 OK` with the expected count.
  - [ ] Include `entityId = aggregateId` and `projectionType = "counter"` explicitly in the query JSON.
  - [ ] If reusing `ContractTestHelpers.CreateQueryRequest`, extend that helper to serialize `ProjectionType` and `EntityId`; do not rely on controller defaulting for this proof.
  - [ ] Treat `404 NotFound` and zero-count payloads as retry states until the expected count appears; do not copy `CounterQueryService`'s UI-friendly 404-to-zero behavior into the test success path.
  - [ ] Preserve the last non-success response body for timeout diagnostics.
  - [ ] Parse direct-object and base64-string payload forms.

- [ ] Task 4: Pin ETag round-trip behavior (AC: #7)
  - [ ] Capture the first successful query response `ETag` header.
  - [ ] Assert the first successful response body has the expected count before sending any `If-None-Match` request.
  - [ ] Send a second query with `If-None-Match`.
  - [ ] Reuse the parsed response `EntityTagHeaderValue` or preserve the server-provided quotes exactly; avoid double-quoting or stripping weak-validator syntax.
  - [ ] Accept `304 NotModified`, or `200 OK` with the same expected count if a concurrent projection change invalidated the ETag.

- [ ] Task 5: Keep failure-path coverage isolated (AC: #9, #10, #12)
  - [ ] Verify the valid test does not enable malformed projection response fault injection.
  - [ ] Do not edit `ProjectionMalformedResponseE2ETests` except for shared helper extraction that preserves behavior.
  - [ ] Route any checkpoint, polling, SignalR, or UI proof gap to R11-A1, R11-A2, or R11-A3 instead of expanding this story.

- [ ] Task 6: Run and record validation (AC: #11)
  - [ ] `dotnet test tests/Hexalith.EventStore.IntegrationTests --filter "FullyQualifiedName~ValidProjectionRoundTripE2ETests"`
  - [ ] If helper changes touch shared contract-test helpers, also run the affected existing tests, especially `ProjectionMalformedResponseE2ETests` and `CommandLifecycleTests`.
  - [ ] Record command output, environment caveats, and any infrastructure blockers in the Dev Agent Record.

## Dev Notes

### Existing Implementation To Reuse

- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractTestFixture.cs` already starts the full Aspire topology with `EnableKeycloak=false`, waits for `eventstore`, `eventstore-admin`, and `sample`, and exposes `EventStoreClient`.
- `tests/Hexalith.EventStore.IntegrationTests/Helpers/ContractTestHelpers.cs` already has command submission and status-poll helpers using synthetic JWTs. Extend it carefully if a query helper needs explicit `entityId` and `projectionType`.
- `ContractTestHelpers.CreateQueryRequest` currently serializes `Tenant`, `Domain`, `AggregateId`, `QueryType`, and `Payload`; this story needs either a local request builder or a helper extension that also serializes `ProjectionType = "counter"` and `EntityId = aggregateId`.
- `src/Hexalith.EventStore/Controllers/CommandsController.cs` returns `202 Accepted`, `Location`, and `Retry-After` for accepted commands.
- `src/Hexalith.EventStore/Controllers/QueriesController.cs` defaults missing `EntityId` to `AggregateId`, uses projection type for self-routing ETags, and emits an `ETag` header when one is available.
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` is the real immediate projection path: resolve domain service, read aggregate events, invoke `/project`, and write the returned state to `ProjectionActor`.
- `samples/Hexalith.EventStore.Sample/Program.cs` maps the real `/project` endpoint to `CounterProjectionHandler.Project` unless the malformed fault flag is enabled.
- `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs` returns `ProjectionResponse("counter", { count = N })`.
- `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs` contains a proven parser for direct-object and base64-string payload shapes.

### Implementation Guardrails

- Do not use direct actor proxies in the test. The value is proving the public command and query surfaces plus DAPR service invocation.
- Do not call the sample service `/project` directly as the main assertion. That proves handler serialization, not EventStore projection delivery.
- If adding an optional direct `/project` preflight, send a valid `ProjectionRequest`; a null or malformed body belongs only to the existing fault-path test.
- Do not make the test depend on SignalR or browser refresh. R11-A3 owns trace-backed AppHost/UI evidence.
- Do not make the test require R11-A1 or R11-A2 to be implemented. The test should pass against immediate mode with full replay and should continue to pass after checkpointing/polling lands.
- Keep timeouts bounded and diagnostics rich. Projection delivery is background work, so the assertion should poll, but it should fail with useful response bodies rather than hanging.
- Use `TestJwtTokenGenerator` claims consistent with dev auth: include the target tenant and permissions for `command:submit`, `command:query`, and `query:read` as needed.

### Party-Mode Review Guardrails

- Keep the success oracle on the server contract: `Completed` with `eventCount > 0`, followed by `POST /api/v1/queries` returning the same aggregate's projected count. Browser/UI 404-to-zero behavior is not acceptable as the final proof.
- The test must prove the explicit query identity shape, not only the controller's fallback behavior. The serialized query body should show `domain = "counter"`, `projectionType = "counter"`, `queryType = "get-counter-status"`, `aggregateId = aggregateId`, and `entityId = aggregateId`.
- ETag validation starts only after a successful count assertion. A `304 NotModified` before the test has proven the target aggregate's projection state would be a false positive.
- If the run fails because DAPR service invocation is blocked by access-control policy or slim-mode infrastructure, record that as an execution blocker for this story; do not broaden the story into access-control, checkpoint, polling, SignalR, or UI remediation.

### Suggested File Touches

- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/ValidProjectionRoundTripE2ETests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Helpers/ContractTestHelpers.cs` (only if shared helpers reduce duplication)
- Optional evidence note in this story's Dev Agent Record after validation

### Architecture And Version Notes

- Package versions are centrally pinned in `Directory.Packages.props`: Aspire `13.2.2`, Dapr `1.17.7`, xUnit v3, Shouldly, and NSubstitute. Do not add test packages for this story unless existing integration-test infrastructure cannot express the proof.
- Aspire integration tests use `DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_EventStore_AppHost>()`; prefer that existing pattern over launching `aspire run` from inside the test.
- DAPR actor and service-invocation behavior is the point of the Tier 3 proof. Keep it in the test path.
- Query actor IDs must remain aligned with `QueryActorIdHelper.DeriveActorId(response.ProjectionType, tenant, aggregateId, [])` used by the write path in `ProjectionUpdateOrchestrator`; explicit `entityId = aggregateId` keeps the read path on that same Tier 1 actor identity.

### Previous Story Intelligence

- Epic 11 retro R11-A4 asks for a repeatable valid projection round-trip, not another malformed-response test.
- R11-A3 is ready-for-dev and covers full AppHost/browser/trace evidence. R11-A4 should produce an automated integration path that can run in CI-like environments with Docker.
- R11-A1 and R11-A2 may later change delivery mechanics, but a valid command-to-query round trip should remain stable if they preserve projection contracts.
- Epic 12 UI smoke evidence is useful but not sufficient for this story because it does not pin the server-side valid projection route.

### Project Structure Notes

- Keep new E2E tests under `tests/Hexalith.EventStore.IntegrationTests/ContractTests/`.
- Keep shared HTTP/auth helpers under `tests/Hexalith.EventStore.IntegrationTests/Helpers/`.
- Do not create production code solely for test observability unless the absence of an observable contract is the actual defect.

## References

- `_bmad-output/implementation-artifacts/epic-11-retro-2026-04-30.md` - R11-A4 action item and full projection-builder evidence gap.
- `_bmad-output/implementation-artifacts/epic-12-retro-2026-04-30.md` - R12-A5 carry-forward of R11-A1 through R11-A4.
- `_bmad-output/implementation-artifacts/post-epic-11-r11a3-apphost-projection-proof.md` - sibling manual/runtime evidence scope to avoid duplicating here.
- `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md` - Mode B projection update and query flow.
- `_bmad-output/planning-artifacts/epics.md` - Epic 11 story requirements, especially Story 11.5 full pipeline expectation.
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractTestFixture.cs` - existing Aspire contract-test fixture.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/ProjectionMalformedResponseE2ETests.cs` - existing fault-path proof to keep separate.
- `tests/Hexalith.EventStore.IntegrationTests/Helpers/ContractTestHelpers.cs` - command/status helper patterns and test JWT usage.
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` - current valid projection delivery path.
- `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs` - projection state write/read actor and fail-open notification path.
- `src/Hexalith.EventStore/Controllers/QueriesController.cs` - query endpoint, `EntityId` defaulting, and ETag response behavior.
- `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs` - valid sample projection response shape.
- `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs` - count parsing pattern for query payloads.

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

### Completion Notes List

### File List

## Party-Mode Review

- Date/time: 2026-05-01T11:04:16+02:00
- Selected story key: `post-epic-11-r11a4-valid-projection-round-trip`
- Command/skill invocation used: `/bmad-party-mode post-epic-11-r11a4-valid-projection-round-trip; review;`
- Participating BMAD agents: Bob (Scrum Master), Winston (Architect), Amelia (Developer Agent), Murat (Master Test Architect), Paige (Technical Writer), Sally (UX Designer), John (Product Manager)
- Findings summary:
  - Bob: The story was structurally ready, but the query-helper path needed clearer wording so implementation cannot satisfy AC #5 by relying only on controller defaulting.
  - Winston: ETag validation needed an ordering guard; `If-None-Match` should be tested only after a successful count proves the target aggregate projection exists.
  - Amelia: `ContractTestHelpers.CreateQueryRequest` currently omits `ProjectionType` and `EntityId`; the story needed an explicit implementation warning before development.
  - Murat: The eventual assertion must treat transient `404 NotFound` and stale zero counts as retry states, not as the final success path copied from the sample UI service.
  - Paige: The optional direct `/project` note needed to distinguish a valid `ProjectionRequest` probe from the existing malformed-response fault test.
  - Sally: No UI accessibility/localization changes are introduced; adopter-experience risk is diagnostic quality when projection actor lookup fails.
  - John: No product scope expansion is needed; checkpointing, polling, SignalR, UI proof, and access-control remediation remain outside this story.
- Changes applied:
  - Tightened Task 3 to require serialized `projectionType = "counter"` and `entityId = aggregateId`.
  - Added a guard that `404 NotFound` and zero-count payloads remain retry states until the expected count appears.
  - Strengthened Task 4 so ETag checks occur after a successful count assertion and preserve server-provided tag formatting.
  - Added Dev Notes for the current `ContractTestHelpers.CreateQueryRequest` limitation and valid `/project` probe shape.
  - Added Party-Mode Review Guardrails covering server-contract success, explicit query identity, ETag ordering, and infrastructure blocker routing.
- Findings deferred:
  - `project-context.md` preload was unavailable; no generated project-context artifact was found in this repository.
  - DAPR access-control or slim-mode runtime failures, if encountered during execution, must be recorded as blockers rather than fixed inside this story.
- Final recommendation: `ready-for-dev`
