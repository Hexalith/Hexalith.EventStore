# Story 22.2: Projection Adapter Contract and Generic Query Actor Model

Status: ready-for-dev

Context created: 2026-05-12
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
Epic: Epic 22 - Public Gateway and Downstream Integration Contracts
Scope: FR87-FR89, with direct dependency awareness for Story 22.1 public gateway DTO/client boundaries and later Story 22.3 tenant/RBAC enforcement plus Story 22.4 query policy/error taxonomy.

## Story

As a domain service developer,
I want a stable projection query adapter contract,
so that Parties can serve GetParty, ListParties, and SearchParties through EventStore queries.

## Public Projection Adapter Boundary Contract

- `Hexalith.EventStore.Contracts` is the preferred home for any public `QueryEnvelope`, `QueryResult`, projection adapter interfaces, query result metadata, and malformed-response reason contracts that downstream domain services need to implement query serving.
- If a contract cannot be moved from `Hexalith.EventStore.Server` in this story, the generic DAPR actor contract must be explicitly documented as stable and implementation-neutral, including actor type name, actor ID derivation, serialization format, and response taxonomy.
- `Hexalith.EventStore.Server` may keep adapters/shims for runtime compatibility, but downstream bounded contexts must not reference `Hexalith.EventStore.Server` only to implement query actors.
- `Hexalith.EventStore.Testing` must not force downstream tests to depend on server internals for public projection query fakes once the public contract is established.
- Public adapter behavior is SemVer-relevant. Do not silently change actor ID format, `DataContract` member names/order, payload encoding, `ProjectionType` semantics, or malformed response classification without tests and docs.

## Current Implementation Intelligence

- `src/Hexalith.EventStore.Server/Actors/QueryEnvelope.cs` currently owns the DAPR actor query envelope. It is `[DataContract]`, carries `TenantId`, `Domain`, `AggregateId`, `QueryType`, `Payload` as `byte[]`, `CorrelationId`, `UserId`, and optional `EntityId`, and deliberately redacts payload bytes in `ToString()`.
- `src/Hexalith.EventStore.Server/Actors/QueryResult.cs` currently owns the actor response. It is `[DataContract]`, carries `Success`, optional `PayloadBytes`, optional `ErrorMessage`, and optional `ProjectionType`; helper methods serialize/deserialize payload JSON.
- `src/Hexalith.EventStore.Server/Actors/IProjectionActor.cs` currently depends on `Dapr.Actors.IActor` and exposes `Task<QueryResult> QueryAsync(QueryEnvelope envelope)`.
- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` currently derives actor IDs with a 3-tier model: entity-scoped, payload-checksum, or tenant-wide. It defaults actor type name to `"ProjectionActor"` unless `SubmitQuery.ProjectionActorType` overrides it.
- `src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs` enforces colon-free `QueryType`, `TenantId`, and `EntityId`, computes an 11-character base64url truncated SHA256 checksum for payload-routed queries, and has a typed overload for `IQueryContract`.
- `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs` is a server runtime base class, not a public domain-service requirement. It caches by query type, payload checksum, and user ID, discovers valid `ProjectionType`, and rejects invalid projection type values by falling back to the envelope domain.
- `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs` is the built-in generic server projection actor registered as `"ProjectionActor"`. It stores `ProjectionState` under `projection-state`, regenerates/broadcasts ETags through `IProjectionChangeNotifier`, and returns persisted projection state via `QueryResult.FromPayload`.
- `src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs`, `QueryContractMetadata.cs`, and `src/Hexalith.EventStore.Client/Queries/QueryContractResolver.cs` already provide typed query metadata and kebab-case validation, but they do not by themselves expose the DAPR actor adapter contract.
- `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs` already includes `projectionType`, `entityId`, and `projectionActorType`. Story 22.2 must freeze how those fields map into the adapter contract or document why any field remains compatibility-only.
- `src/Hexalith.EventStore.Testing/Fakes/FakeProjectionActor.cs` currently references `Hexalith.EventStore.Server.Actors.QueryEnvelope`, `QueryResult`, and `IProjectionActor`. `src/Hexalith.EventStore.Testing/Hexalith.EventStore.Testing.csproj` currently references `Hexalith.EventStore.Server`, so downstream tests can accidentally inherit server coupling.
- `samples/Hexalith.EventStore.Sample/Counter/Queries/GetCounterStatusQuery.cs` is the existing concrete query-contract example using `IQueryContract` with `get-counter-status`, `counter`, and `counter`.
- `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs` handles `/project` projection building, not generic query serving. Do not mistake `/project` projection rebuild/update behavior for the query actor contract.
- `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs` is a caller-side HTTP query example. It sends `entityId = "counter-1"` and parses payload that may be direct JSON or base64-encoded JSON; this parsing ambiguity should be resolved or documented by the public adapter/client contract.

## Acceptance Criteria

1. **Projection adapter contract ownership is explicit and usable.**
   - Given a downstream domain service needs to serve queries through EventStore
   - When it implements the generic projection adapter
   - Then the required contract surface for `QueryEnvelope`, `QueryResult`, actor interface or documented generic actor shape, actor type naming, serialization attributes, projection metadata, and malformed response categories is public in `Hexalith.EventStore.Contracts` or explicitly documented as a stable DAPR actor contract.
   - And a downstream implementation does not need to reference `Hexalith.EventStore.Server` only to compile the public query-serving boundary.
   - And existing server runtime behavior continues to compile through compatibility shims or updated namespaces.

2. **Query routing maps HTTP query requests to adapter calls deterministically.**
   - Given a query caller invokes `POST /api/v1/queries`
   - When the request represents Get, List, or Search behavior
   - Then EventStore derives the target actor type and actor ID from the documented rules without exposing domain actor internals to the caller.
   - And `projectionType`, `projectionActorType`, `entityId`, payload checksum routing, and tenant scoping are each covered by focused tests.
   - And actor IDs remain colon-structured and reject colon-containing routing segments before invocation.

3. **Entity, list, and search query serving examples are covered.**
   - Given a domain service exposes entity, list, and search projection actors or wrappers
   - When EventStore routes GetParty, ListParties, and SearchParties-style requests
   - Then contract tests prove entity-scoped, tenant-wide, and payload-scoped query routing paths.
   - And the tests use a representative public adapter implementation/fake rather than depending on server-only types as the downstream contract.

4. **Malformed projection responses become a stable taxonomy or explicit defer.**
   - Given a projection actor returns failure, missing payload, invalid JSON payload bytes, inconsistent `ProjectionType`, unknown actor type, timeout, or unsupported query behavior
   - When EventStore maps that result to the HTTP query pipeline
   - Then each category has a stable result/error classification in code and docs, or is explicitly deferred to Story 22.4 without pretending it is closed.
   - And actor-not-found infrastructure failures are distinguished from domain-level not-found results.
   - And logs do not include query payload bytes or protected data.

5. **Testing package stops reinforcing server coupling for public query adapters.**
   - Given downstream tests use `Hexalith.EventStore.Testing`
   - When they need to fake projection query serving
   - Then fakes/builders use the public adapter contract when available.
   - And any remaining `Hexalith.EventStore.Server` reference in Testing is documented as a runtime-test utility dependency, not the public downstream adapter requirement.
   - And focused tests cover successful, failed, malformed, entity/list/search, and routing-metadata assertions.

6. **Documentation and package guidance align with the chosen contract.**
   - Given package docs, query API docs, and generated API docs are refreshed
   - When a Parties developer reads the guidance
   - Then they can identify which package to reference, which types or DAPR actor methods to implement, how actor type/ID routing works, how payload bytes are encoded, and how `ProjectionType` affects ETag/cache behavior.
   - And docs distinguish `/project` projection update/rebuild endpoints from `POST /api/v1/queries` query-serving actors.
   - And docs state that tenant/RBAC enforcement is owned by Story 22.3 and detailed query paging/error taxonomy is owned by Story 22.4.

7. **Regression and evidence are recorded before review.**
   - Contracts, Client, Server query focused tests, Testing focused tests, and Sample query/projection tests pass individually when touched.
   - Documentation validation is run where available.
   - Dev Agent Record, File List, Verification Status, and Change Log are updated before moving the story to review.

## Tasks / Subtasks

- [ ] **ST0 - Baseline current projection query boundary and classify ownership.** (AC: 1, 2, 5, 6)
    - [ ] Read Story 22.1, Epic 22, PRD FR87-FR89, architecture ADR-P7, and this story before code edits.
    - [ ] Inventory server-bound query actor types in `src/Hexalith.EventStore.Server/Actors` and routing types in `src/Hexalith.EventStore.Server/Queries`.
    - [ ] Inventory existing public query metadata contracts in `src/Hexalith.EventStore.Contracts/Queries` and client resolver behavior in `src/Hexalith.EventStore.Client/Queries`.
    - [ ] Inventory `Hexalith.EventStore.Testing` server dependency and `FakeProjectionActor` usage.
    - [ ] Record an ownership decision table covering move-to-Contracts, keep-server-with-public-docs, and defer-to-22.4 categories.

- [ ] **ST1 - Publish or document the adapter contract without breaking runtime behavior.** (AC: 1, 4, 5)
    - [ ] Prefer moving stable `QueryEnvelope`, `QueryResult`, and the minimal projection adapter interface to `Hexalith.EventStore.Contracts` if dependency shape permits.
    - [ ] If DAPR `IActor` inheritance prevents a clean Contracts move, define a public implementation-neutral contract plus a server adapter interface and document the DAPR actor method signature.
    - [ ] Preserve `[DataContract]` / `[DataMember]` behavior for actor remoting; add serialization compatibility tests before changing namespaces or constructors.
    - [ ] Preserve payload redaction in `ToString()` and logging. Never log payload bytes.
    - [ ] Add or update XML docs for public types so generated API docs identify downstream implementation requirements.

- [ ] **ST2 - Harden routing semantics for Get/List/Search patterns.** (AC: 2, 3)
    - [ ] Add focused tests proving entity-scoped Get routing uses `EntityId`.
    - [ ] Add focused tests proving tenant-wide List routing uses an empty payload and no `EntityId`.
    - [ ] Add focused tests proving Search routing uses the payload checksum and does not leak search payload data into actor IDs or logs.
    - [ ] Verify `ProjectionType` drives routing/ETag semantics where intended and does not conflict with `QueryType`.
    - [ ] Verify `ProjectionActorType` remains explicit and documented; do not let arbitrary caller input bypass future Story 22.3 authorization decisions.

- [ ] **ST3 - Define malformed response classification at the adapter edge.** (AC: 4)
    - [ ] Classify `QueryResult.Success == false` values currently mapped by `SubmitQueryHandler`: forbidden, not found/no projection state, not implemented, and unknown failure.
    - [ ] Add explicit tests for missing payload on success, invalid payload bytes, actor-not-found infrastructure patterns, and domain-level not-found results.
    - [ ] Record deferred items for Story 22.4 if full ProblemDetails type URI/reason-code taxonomy cannot be frozen here.
    - [ ] Ensure unsupported or malformed projection behavior fails predictably and never returns a fake success envelope.

- [ ] **ST4 - Decouple or label Testing package projection fakes.** (AC: 5)
    - [ ] Update `FakeProjectionActor` and related tests to use public adapter types if they move to Contracts.
    - [ ] If Testing must still reference Server for runtime actor test helpers, add public query-adapter fakes/builders that do not require downstream code to import Server-only types.
    - [ ] Add tests for fake success, failure, malformed payload, entity/list/search envelopes, and recorded invocations.

- [ ] **ST5 - Align docs and generated API references.** (AC: 6)
    - [ ] Update `docs/reference/query-api.md` with adapter package ownership, actor type/ID derivation, `QueryEnvelope` and `QueryResult` fields, payload encoding, and malformed-response behavior.
    - [ ] Update `docs/reference/nuget-packages.md` with projection adapter guidance for Contracts, Client, Testing, and Server internals.
    - [ ] Update generated API docs for any new or moved public Contracts/Testing types.
    - [ ] Add a short note that `/project` projection updates and `POST /api/v1/queries` query serving are separate contracts.

- [ ] **ST6 - Validate and record evidence.** (AC: 7)
    - [ ] Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests`.
    - [ ] Run `dotnet test tests/Hexalith.EventStore.Client.Tests`.
    - [ ] Run `dotnet test tests/Hexalith.EventStore.Testing.Tests`.
    - [ ] Run focused Server query tests if server routing or actor shims changed. If `Hexalith.EventStore.Server.Tests` hits the known CA2007 blocker outside the focused slice, record it without broadening scope.
    - [ ] Run `dotnet test tests/Hexalith.EventStore.Sample.Tests` if sample query/projection contracts changed.
    - [ ] Run markdown or docs validation where available.
    - [ ] Update Dev Agent Record, File List, Verification Status, and Change Log.

## Developer Notes

Architecture and product guardrails:

- ADR-P7 is the controlling decision: EventStore must provide a stable public projection adapter model for generic query serving or explicitly document the generic DAPR actor contract domain services implement.
- Story 22.1 already owns gateway command/query DTO, client, fake, and package-doc closure. Reuse its package-boundary decisions; do not reopen command DTO/client behavior unless the adapter contract requires a narrow doc link.
- Story 22.3 owns tenant lifecycle, membership, role, and permission validation before invoking domain services or projection adapters. This story may document that the adapter assumes gateway authorization has already run, but must not implement tenant/RBAC policy.
- Story 22.4 owns detailed query paging/filter/freshness/error taxonomy. This story owns adapter-edge malformed-response categories only far enough to avoid ambiguous or dishonest query serving behavior.
- Story 22.6 owns stream read/replay APIs and projection rebuild checkpoints. Do not broaden this story into replay APIs.
- Stories 22.7a through 22.7d own payload/snapshot protection and redaction across operational surfaces. Preserve no-payload-logging behavior and record protection-taxonomy defers if needed.

Implementation traps to avoid:

- Do not make `Hexalith.EventStore.Contracts` depend on `Hexalith.EventStore.Server`.
- Do not move `CachingProjectionActor` or `EventReplayProjectionActor` into Contracts; they are runtime implementations.
- Do not break DAPR actor remoting by removing `[DataContract]` / `[DataMember]` attributes or changing payload from `byte[]` to `JsonElement` across actor boundaries without replacement serialization proof.
- Do not treat `/project` projection rebuild/update contracts as the same thing as query actor serving.
- Do not let `ProjectionActorType` become an undocumented escape hatch that undermines later gateway-owned authorization.
- Do not document Parties-specific names as the only supported query contract. Use Parties/GetParty/ListParties/SearchParties as representative examples.
- Do not use broad integration tests as the first proof. Start with Contracts/Client/Testing/Server unit slices, then add integration evidence only if a runtime behavior cannot be proven otherwise.

Current file intelligence:

- Server query actor boundary:
    - `src/Hexalith.EventStore.Server/Actors/QueryEnvelope.cs`
    - `src/Hexalith.EventStore.Server/Actors/QueryResult.cs`
    - `src/Hexalith.EventStore.Server/Actors/IProjectionActor.cs`
    - `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`
    - `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs`
    - `src/Hexalith.EventStore.Server/Actors/ProjectionState.cs`
    - `src/Hexalith.EventStore.Server/Actors/IProjectionWriteActor.cs`
- Server routing and pipeline:
    - `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`
    - `src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs`
    - `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs`
    - `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs`
    - `src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs`
- Public query/gateway contracts:
    - `src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs`
    - `src/Hexalith.EventStore.Contracts/Queries/QueryContractMetadata.cs`
    - `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs`
    - `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryResponse.cs`
    - `src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs`
    - `src/Hexalith.EventStore.Contracts/Projections/ProjectionResponse.cs`
    - `src/Hexalith.EventStore.Contracts/Projections/ProjectionEventDto.cs`
- Client/query helpers:
    - `src/Hexalith.EventStore.Client/Queries/QueryContractResolver.cs`
    - `src/Hexalith.EventStore.Client/Gateway/IEventStoreGatewayClient.cs`
    - `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`
- Testing:
    - `src/Hexalith.EventStore.Testing/Fakes/FakeProjectionActor.cs`
    - `src/Hexalith.EventStore.Testing/Hexalith.EventStore.Testing.csproj`
    - `tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeProjectionActorTests.cs`
- Sample query/projection references:
    - `samples/Hexalith.EventStore.Sample/Counter/Queries/GetCounterStatusQuery.cs`
    - `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs`
    - `samples/Hexalith.EventStore.Sample/Program.cs`
    - `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs`
- Focused tests:
    - `tests/Hexalith.EventStore.Contracts.Tests/Queries/IQueryContractTests.cs`
    - `tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryRequestTests.cs`
    - `tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryResponseTests.cs`
    - `tests/Hexalith.EventStore.Client.Tests/Queries/QueryContractResolverTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Queries/QueryActorIdHelperTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Actors/QueryEnvelopeTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Actors/QueryResultTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Actors/EventReplayProjectionActorTests.cs`
    - `tests/Hexalith.EventStore.Sample.Tests/Counter/Queries/GetCounterStatusQueryTests.cs`

Testing standards:

- Use xUnit v3, Shouldly, and NSubstitute where the existing test project already uses them.
- Run test projects individually per repository guidance.
- Prefer contract/unit tests for public adapter moves before integration tests.
- Integration tests under `tests/Hexalith.EventStore.IntegrationTests` require Docker and a running Aspire/DAPR environment.
- `Hexalith.EventStore.Server.Tests` has known pre-existing CA2007 warning-as-error risk in this workspace. Run focused filters first when server behavior changes and record any unrelated blocker exactly.

## Files Likely Touched

- `src/Hexalith.EventStore.Contracts/Queries/*`
- `src/Hexalith.EventStore.Contracts/Projections/*` only if projection adapter query contracts join existing projection update DTOs.
- `src/Hexalith.EventStore.Server/Actors/QueryEnvelope.cs`
- `src/Hexalith.EventStore.Server/Actors/QueryResult.cs`
- `src/Hexalith.EventStore.Server/Actors/IProjectionActor.cs`
- `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`
- `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs`
- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`
- `src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs`
- `src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs`
- `src/Hexalith.EventStore.Testing/Fakes/FakeProjectionActor.cs`
- `src/Hexalith.EventStore.Testing/Builders/*` only if public query-adapter builders are added.
- `src/Hexalith.EventStore.Testing/Hexalith.EventStore.Testing.csproj`
- `docs/reference/query-api.md`
- `docs/reference/nuget-packages.md`
- `docs/reference/api/**` only if public API docs are regenerated.
- Related focused test files under `tests/Hexalith.EventStore.Contracts.Tests`, `tests/Hexalith.EventStore.Client.Tests`, `tests/Hexalith.EventStore.Server.Tests`, `tests/Hexalith.EventStore.Testing.Tests`, and `tests/Hexalith.EventStore.Sample.Tests`.

## Out of Scope

- Gateway-owned tenant lifecycle, membership, role, permission validation, and 401/403 reason taxonomy. Story 22.3 owns that.
- Full query paging, filter validation, blank search behavior, deterministic ordering, stale/degraded freshness policy, timeout policy, and final query ProblemDetails taxonomy. Story 22.4 owns that except for adapter-edge categories needed here.
- Pub/sub delivery guarantees, backend deployment matrices, retries, drains, and dead-letter topics. Story 22.5 owns that.
- Stream read/replay APIs and operator-safe projection rebuild checkpoints. Story 22.6 owns that.
- Payload/snapshot protection hooks, unreadable protected data behavior, crypto-shredding, restored-backup safety, and cross-surface redaction policy. Stories 22.7a through 22.7d own that.
- Broad DAPR access-control changes, AppHost topology changes, Parties repository changes, or admin UI changes.
- Rewriting the projection update `/project` endpoint beyond doc clarification that it is separate from query serving.

## References

- `_bmad-output/planning-artifacts/epics.md#Story 22.2: Projection Adapter Contract and Generic Query Actor Model`
- `_bmad-output/planning-artifacts/prd.md#Public Gateway and Downstream Integration Contracts - v1.1 (FR83-FR104)`
- `_bmad-output/planning-artifacts/architecture.md#ADR-P7: Projection Query Adapter Is a Public Domain Integration Contract`
- `_bmad-output/planning-artifacts/architecture.md#Projection Query Adapter Contract`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-parties-integration-contract-gaps.md`
- `_bmad-output/implementation-artifacts/22-1-gateway-command-query-contract-closure-and-package-docs.md`
- `_bmad-output/project-context.md`
- `_bmad-output/process-notes/story-creation-lessons.md#L08 - Party Review Vs. Elicitation`
- `docs/reference/query-api.md`
- `docs/reference/nuget-packages.md`
- `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-12T19:20:28Z - Pre-dev hardening preflight returned a soft working-tree warning via `_bmad-output/process-notes/predev-preflight-latest.json`; dirty paths were ordinary docs/src development files outside BMAD-owned story-operation paths.
- 2026-05-12T21:23:39+02:00 - Story creation context gathered from Epic 22, PRD FR87-FR89, architecture ADR-P7, Story 22.1, current Server query actor/routing implementation, Contracts query metadata, Testing projection fake, sample counter query/projection code, recent commits, project context, and lessons ledger.

### Completion Notes List

- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, docs, generated API docs, or submodules.
- Preflight soft warning was left untouched: `docs/reference/command-api.md`, `src/Hexalith.EventStore/Controllers/CommandsController.cs`, `src/Hexalith.EventStore/Filters/ValidateModelFilter.cs`, `src/Hexalith.EventStore/Middleware/CorrelationIdMiddleware.cs`, `src/Hexalith.EventStore/Models/ReplayCommandResponse.cs`, and `src/Hexalith.EventStore/Validation/SubmitCommandRequestValidator.cs`.
- Party-mode review has NOT yet been run for this story.
- Advanced elicitation has NOT yet been run for this story.

### File List

- `_bmad-output/implementation-artifacts/22-2-projection-adapter-contract-and-generic-query-actor-model.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight soft warning classified from the canonical JSON and unrelated dirty development files were not modified by this job.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, docs, generated API docs, or submodules.
- YAML validation passed for `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- `git diff --check` passed for the story artifact, sprint status, and run log with line-ending conversion warnings only.
- `npx markdownlint-cli2 _bmad-output/implementation-artifacts/22-2-projection-adapter-contract-and-generic-query-actor-model.md` passed with 0 errors.
- Party-mode review has NOT yet been run for this story.
- Advanced elicitation has NOT yet been run for this story.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-12 | 0.1 | Created ready-for-dev story for projection adapter contract and generic query actor model. | Codex automation |
