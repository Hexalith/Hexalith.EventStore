# Story 22.4: Query Behavior Policy and Error Taxonomy

Status: ready-for-dev

Context created: 2026-05-12
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
Epic: Epic 22 - Public Gateway and Downstream Integration Contracts
Scope: FR93-FR95, with direct dependency awareness for Story 22.1 public gateway DTO/client/fake boundaries, Story 22.2 projection adapter routing, and Story 22.3 gateway-owned authorization.

## Story

As an API consumer,
I want query behavior frozen across paging, filters, cache, and degraded reads,
so that Parties API behavior is predictable when routed through EventStore.

## Query Behavior Contract

- EventStore owns the public query behavior policy for `POST /api/v1/queries`: paging bounds, default page size, maximum page size, cursor/offset semantics, blank search behavior, filter validation, deterministic ordering, cache/freshness metadata, and non-auth query failure taxonomy.
- `Hexalith.EventStore.Contracts` owns API-facing query request/response metadata constants and DTOs that downstream callers or query implementers need. `Hexalith.EventStore.Client` owns HTTP convenience behavior and ProblemDetails parsing. `Hexalith.EventStore.Testing` owns deterministic query fakes/builders.
- Query responses must expose stable metadata for `correlationId`, `etag`, `isNotModified`, `isStale`, `isDegraded`, `projectionVersion`, `servedAt`, paging metadata, and warning codes when those concepts apply.
- Query errors must map malformed request, unsupported filter, invalid page, projection missing, projection stale beyond policy, degraded search, malformed projection response, projection timeout, and authorization failures to stable ProblemDetails types and machine-readable reason codes.
- Authorization failures reuse Story 22.3's 401/403 taxonomy. This story may align query error surfaces with that taxonomy, but must not reopen tenant/RBAC adapter scope.
- Projection adapter-edge malformed-response categories from Story 22.2 feed this story's taxonomy; this story owns final HTTP ProblemDetails and client/testing behavior for query-facing categories.

## Current Implementation Intelligence

- `SubmitQueryRequest` currently carries `tenant`, `domain`, `aggregateId`, `queryType`, optional `projectionType`, optional `payload`, optional `entityId`, and optional `projectionActorType`. It has no first-class paging, cursor, offset, filter, search, ordering, freshness, timeout, or stale/degraded policy fields.
- `SubmitQueryResponse` currently carries `correlationId`, `payload`, `success`, and `errorMessage`. The controller currently returns only the original successful two-field shape through `new SubmitQueryResponse(result.CorrelationId, result.Payload)`, so `Success` and `ErrorMessage` do not yet carry useful HTTP query results.
- `QueriesController.Submit` supports a 1 MB request body, requires `[Authorize]`, requires a `sub` claim, stores `RequestTenantId`, serializes `Payload` to UTF-8 bytes, defaults omitted `EntityId` to `AggregateId`, defaults omitted `ProjectionType` to `Domain`, and forwards optional `ProjectionActorType`.
- `QueriesController` performs a Gate 1 ETag pre-check before MediatR routing. It decodes self-routing ETags from `If-None-Match`, skips wildcard `*`, skips mixed decoded projection types, limits matching to ten header values, treats old/malformed/weak ETags as cache misses, and fails open when `IETagService` throws.
- Current ETag behavior returns `304 Not Modified` with a quoted `ETag` header and no body when the decoded projection's current ETag matches. On `200 OK`, the controller fetches the current ETag using runtime `SubmitQueryResult.ProjectionType`, request `projectionType`, or request `domain` fallback.
- `SubmitQueryHandler` maps `QueryRouterResult.NotFound` to `QueryNotFoundException`, `Forbidden` error text to a 403 `QueryExecutionFailedException`, `not found` / `no projection state available` text to `QueryNotFoundException`, and `not implemented` text to a 501 `QueryExecutionFailedException`. Unknown query failures throw generic `InvalidOperationException("Projection query execution failed.")`.
- `QueryRouter` uses the three-tier actor ID model from Story 22.2: entity-scoped, payload-checksum, then tenant-wide. It treats actor-not-found infrastructure messages as `NotFound=true`; other actor invocation failures are rethrown.
- `QueryResult.GetPayload()` deserializes payload bytes to `JsonElement`; malformed JSON currently bubbles from deserialization rather than mapping to a query-specific ProblemDetails category.
- `ProblemTypeUris` currently has generic `ValidationError`, `BadRequest`, `Forbidden`, `NotFound`, `NotImplemented`, `ServiceUnavailable`, and `InternalServerError` constants. It does not have query-specific type URIs for unsupported filter, invalid page, stale beyond policy, degraded search, malformed projection response, or projection timeout.
- `QueryNotFoundExceptionHandler` returns generic 404 `NotFound` with `correlationId` only and intentionally does not expose tenant, domain, aggregate ID, or actor ID.
- `QueryExecutionFailedExceptionHandler` maps 403 to `Forbidden`, 501 to `NotImplemented`, and all other mapped query failures to `InternalServerError`. It includes `correlationId`, and includes `tenantId` only for 403.
- `EventStoreGatewayClient.SubmitQueryAsync` returns `EventStoreQueryResult` with `CorrelationId`, `Payload`, `IsNotModified`, and `ETag`. It throws `EventStoreGatewayException` for non-success status codes and currently extracts only `type`, `title`, `detail`, and `correlationId` from ProblemDetails.
- `FakeEventStoreGatewayClient` can return success, typed query results, not-modified results, and configured `EventStoreGatewayException`, but it does not provide query-policy builders for invalid page, unsupported filter, stale/degraded, malformed projection response, or timeout categories.
- `docs/reference/query-api.md` documents query execution, `If-None-Match`, 304, current error status table, SignalR invalidation, and projection type naming. It does not yet freeze paging/filter/search/order semantics or the full FR93-FR95 query ProblemDetails taxonomy.

## Acceptance Criteria

1. **Query request policy is explicit and enforced.**
   - Given a query request includes paging, search, filters, ordering, cursor, or offset fields
   - When EventStore validates the request
   - Then default page size, maximum page size, cursor/offset semantics, blank search behavior, filter validation, and deterministic ordering rules are enforced by public contract or explicitly documented as unsupported for the current endpoint shape.
   - And unsupported or malformed policy inputs return stable ProblemDetails types and reason codes rather than free-text-only failures.

2. **Paging and ordering semantics are deterministic.**
   - Given a list/search query returns multiple items
   - When paging metadata is present
   - Then the contract defines page size defaults, max page size, next cursor or offset semantics, total count expectations if available, and deterministic ordering requirements.
   - And repeated calls with the same projection version and equivalent query parameters return stable item order.

3. **Cache and freshness metadata are public contract surface.**
   - Given a query returns `200 OK` or `304 Not Modified`
   - When the client inspects the response and headers
   - Then `etag`, `isNotModified`, `isStale`, `isDegraded`, `projectionVersion`, `servedAt`, paging metadata, and warning codes are present in documented locations when applicable.
   - And missing ETag data from cold start or ETag actor failure is documented as a fail-open cache optimization loss, not a failed query.

4. **Query error taxonomy is stable and machine-readable.**
   - Given EventStore encounters malformed request, unsupported filter, invalid page, projection missing, projection stale beyond policy, degraded search, malformed projection response, projection timeout, authorization failure, actor-not-found infrastructure failure, or not-implemented query behavior
   - When it maps the outcome to HTTP
   - Then each category has a stable status code, ProblemDetails type URI, reason code extension, and sanitized client detail.
   - And internal actor type names, DAPR addresses, state-store keys, aggregate/projection state, query payload bytes, membership details, tokens, and protected data are not exposed.

5. **Projection adapter and authorization handoffs remain intact.**
   - Given Story 22.2 defines projection adapter routing and Story 22.3 defines gateway tenant/RBAC enforcement
   - When Story 22.4 implements query policy and taxonomy
   - Then it reuses those boundaries without moving adapter ownership, bypassing gateway authorization, or requiring domain services to own EventStore query policy.
   - And adapter-edge failures are translated consistently without changing the public generic query actor contract unless Story 22.2 has already made the relevant type public.

6. **Client and testing package behavior aligns with the taxonomy.**
   - Given downstream services use `Hexalith.EventStore.Client` or `Hexalith.EventStore.Testing`
   - When query success, not-modified, stale, degraded, invalid page, unsupported filter, malformed projection response, timeout, not-found, and authorization failures are simulated or returned
   - Then client results/exceptions and testing fakes expose deterministic status, type URI, reason code, ETag/freshness metadata, and warning code behavior without requiring callers to parse free-text details.

7. **Documentation and evidence are recorded before review.**
   - Query API docs, package docs, and problem reference docs describe the policy, metadata, and taxonomy.
   - Contracts, Client, Testing, and Server focused tests pass individually for touched areas.
   - Dev Agent Record, File List, Verification Status, and Change Log are updated before moving the story to review.

## Tasks / Subtasks

- [ ] **ST0 - Baseline current query policy and classify contract gaps.** (AC: 1, 2, 3, 4, 5, 6)
    - [ ] Read this story, Epic 22, PRD FR93-FR95, architecture ADR-P6/P7/P8/P9, Stories 22.1-22.3, and `docs/reference/query-api.md` before code edits.
    - [ ] Inventory `SubmitQueryRequest`, `SubmitQueryResponse`, `QueriesController`, `SubmitQueryHandler`, `QueryRouter`, `QueryActorIdHelper`, `QueryResult`, `ProblemTypeUris`, query exception handlers, gateway client, and testing fakes.
    - [ ] Record a decision table for each policy dimension: paging, cursor/offset, search, filters, ordering, freshness, stale/degraded, malformed projection response, timeout, not-found, not-implemented, and authorization handoff.
    - [ ] Explicitly mark any policy not implemented in this story as unsupported/deferred with public error behavior; do not leave it implicit.

- [ ] **ST1 - Define public query policy contracts.** (AC: 1, 2, 3)
    - [ ] Add or extend public Contracts types/constants for query paging, freshness metadata, warning codes, and query reason-code extension names where needed.
    - [ ] Preserve source and wire compatibility for existing `SubmitQueryRequest` and `SubmitQueryResponse` callers unless an explicit SemVer-relevant contract decision is recorded.
    - [ ] Define defaults and bounds for page size and maximum page size; avoid magic values buried only in validators.
    - [ ] Define cursor/offset behavior. If both cannot be supported safely, choose one public mode and return a stable unsupported/invalid ProblemDetails category for the other.
    - [ ] Define blank search behavior and filter validation behavior for Get/List/Search style queries without making Parties-specific field names the platform contract.

- [ ] **ST2 - Enforce validation and deterministic routing inputs.** (AC: 1, 2, 5)
    - [ ] Update `SubmitQueryRequestValidator` or equivalent model validation so paging/filter/search/order policy failures become deterministic validation output.
    - [ ] Preserve existing tenant/domain/aggregate/query/projection/entity validation and colon separator rules.
    - [ ] Ensure query payload handling does not log or echo payload bytes when invalid filters/search values are rejected.
    - [ ] Add focused validator/controller tests for default page size, max page size, invalid page/cursor/offset, blank search, unsupported filter, deterministic order defaults, and payload-size boundaries where applicable.

- [ ] **ST3 - Freeze query response metadata behavior.** (AC: 3, 6)
    - [ ] Decide whether metadata lives in `SubmitQueryResponse`, `EventStoreQueryResult`, headers, or a nested metadata object, then document it consistently.
    - [ ] Add `etag`/`isNotModified` behavior without breaking existing 304 empty-body semantics.
    - [ ] Add or map `isStale`, `isDegraded`, `projectionVersion`, `servedAt`, paging metadata, and warning codes where the server can answer them authoritatively.
    - [ ] If the current runtime cannot know a metadata value yet, document it as omitted/null and add a reasoned deferred decision instead of fabricating it.
    - [ ] Extend `FakeEventStoreGatewayClient` and tests so downstream test code can simulate metadata and not-modified behavior deterministically.

- [ ] **ST4 - Implement stable query ProblemDetails taxonomy.** (AC: 4, 5, 6)
    - [ ] Add query-specific `ProblemTypeUris` constants and problem reference docs for malformed request/invalid page, unsupported filter, projection missing, projection stale beyond policy, degraded search, malformed projection response, projection timeout, and not-implemented query behavior as needed.
    - [ ] Add a stable reason-code extension name and concrete reason codes; keep naming aligned with Story 22.1 and Story 22.3 gateway ProblemDetails conventions.
    - [ ] Update `QueryNotFoundExceptionHandler`, `QueryExecutionFailedExceptionHandler`, validation ProblemDetails output, and any global handler path needed for malformed projection JSON or timeout categories.
    - [ ] Replace free-text-only `SubmitQueryHandler` classification where possible with typed categories. If adapter contracts still return free text from Story 22.2, isolate that compatibility mapping and cover it with tests.
    - [ ] Add sanitization tests proving actor/DAPR/state-store/query payload/protected-data details do not leak.

- [ ] **ST5 - Align Client and Testing support.** (AC: 3, 4, 6)
    - [ ] Extend `EventStoreGatewayException` parsing if ProblemDetails reason codes, warning codes, or additional extensions become public client behavior.
    - [ ] Preserve current `SubmitQueryAsync` 304 behavior: typed query results return default payload with `IsNotModified=true` and ETag when present.
    - [ ] Add client tests for query ProblemDetails categories and metadata extraction.
    - [ ] Add testing fake/builders for success metadata, stale/degraded warnings, invalid page, unsupported filter, projection missing, malformed projection response, timeout, not-implemented, and auth failures.

- [ ] **ST6 - Update docs and generated API references.** (AC: 1, 2, 3, 4, 7)
    - [ ] Update `docs/reference/query-api.md` with query policy, request/response metadata, ETag/304 behavior, stale/degraded semantics, and error taxonomy.
    - [ ] Update `docs/reference/nuget-packages.md` to clarify Contracts/Client/Testing ownership for query policy and fakes.
    - [ ] Add or update `docs/reference/problems/*` pages and `docs/reference/problems/index.md` for new query problem types.
    - [ ] Refresh generated API docs for any public Contracts, Client, or Testing API changes.
    - [ ] Keep docs platform-level; use Parties/GetParty/ListParties/SearchParties as examples only, not as required EventStore type names.

- [ ] **ST7 - Validate and record evidence.** (AC: 7)
    - [ ] Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests` if Contracts query types/constants change.
    - [ ] Run `dotnet test tests/Hexalith.EventStore.Client.Tests` if gateway client result/exception behavior changes.
    - [ ] Run `dotnet test tests/Hexalith.EventStore.Testing.Tests` if fake/builders change.
    - [ ] Run focused Server query/controller/error tests, starting with `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "QueriesController|SubmitQuery|QueryRouter|QueryNotFound|QueryExecutionFailed|SubmitQueryRequestValidator"`.
    - [ ] Run `dotnet test tests/Hexalith.EventStore.Sample.Tests` if sample query contracts or examples change.
    - [ ] Run docs/markdown validation where available.
    - [ ] Update Dev Agent Record, File List, Verification Status, and Change Log.

## Developer Notes

Architecture and product guardrails:

- ADR-P6 is controlling for package ownership: public gateway contracts, ProblemDetails extension names, reason codes, ETag/304 behavior, correlation ID behavior, and query result metadata are SemVer-relevant.
- ADR-P7 is controlling for adapter handoff: query policy may translate adapter failures, but must not make downstream services reference `Hexalith.EventStore.Server` for public query policy unless Story 22.2 explicitly documented that path.
- ADR-P8 is controlling for auth handoff: tenant/RBAC checks happen before invoking domain services or projection adapters. Do not let query policy validation bypass or duplicate gateway authorization.
- ADR-P9 is controlling for downstream policy: query policy, publishing, replay, and payload-protection behavior are platform contracts, not Parties-specific implementation details.
- Existing ETag behavior is intentionally fail-open for cache optimization failures. Do not convert ETag actor unavailability into query failure unless a product/architecture decision explicitly changes that policy.
- Existing 304 behavior has an empty body. Keep it unless docs, client behavior, and tests intentionally approve a SemVer-relevant change.

Implementation traps to avoid:

- Do not add query policy only in docs. Validation, DTO/constants, client parsing, fakes, tests, and ProblemDetails docs must agree.
- Do not rely on `ErrorMessage` free text as the long-term taxonomy. If compatibility mapping is needed, keep it isolated and tested.
- Do not use Parties-specific filter names as the EventStore platform contract. Define generic policy and use Parties examples only to prove representative Get/List/Search behavior.
- Do not log or return query payload bytes, protected data, actor IDs where they expose internals, DAPR addresses, state-store keys, stack traces, membership lists, or tokens.
- Do not change query actor ID derivation from Story 22.2 unless that story's contract has already been updated.
- Do not alter tenant/RBAC reason-code ownership from Story 22.3. Query taxonomy should include authorization failures by reference/alignment, not by creating a conflicting taxonomy.
- Do not run broad solution-level tests first. Use focused slices because `Hexalith.EventStore.Server.Tests` has a known CA2007 warning-as-error risk in this workspace.

Current file intelligence:

- Public query contracts:
    - `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs`
    - `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryResponse.cs`
    - `src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs`
    - `src/Hexalith.EventStore.Contracts/Queries/IQueryResponse.cs`
    - `src/Hexalith.EventStore.Contracts/Queries/QueryContractMetadata.cs`
- Query controller and validation:
    - `src/Hexalith.EventStore/Controllers/QueriesController.cs`
    - `src/Hexalith.EventStore/Controllers/QueryValidationController.cs`
    - `src/Hexalith.EventStore/Validation/SubmitQueryRequestValidator.cs`
    - `src/Hexalith.EventStore/Validation/ValidateQueryRequestValidator.cs`
- Server query pipeline and adapter handoff:
    - `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs`
    - `src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs`
    - `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`
    - `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs`
    - `src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs`
    - `src/Hexalith.EventStore.Server/Actors/QueryEnvelope.cs`
    - `src/Hexalith.EventStore.Server/Actors/QueryResult.cs`
- ETag/cache behavior:
    - `src/Hexalith.EventStore.Server/Queries/IETagService.cs`
    - `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs`
    - `src/Hexalith.EventStore.Server/Queries/SelfRoutingETag.cs`
    - `src/Hexalith.EventStore.Server/Actors/ETagActor.cs`
    - `src/Hexalith.EventStore.Testing/Fakes/FakeETagActor.cs`
- Error handling and ProblemDetails:
    - `src/Hexalith.EventStore/ErrorHandling/ProblemTypeUris.cs`
    - `src/Hexalith.EventStore/ErrorHandling/QueryNotFoundExceptionHandler.cs`
    - `src/Hexalith.EventStore/ErrorHandling/QueryExecutionFailedExceptionHandler.cs`
    - `src/Hexalith.EventStore/ErrorHandling/ValidationProblemDetailsFactory.cs`
    - `src/Hexalith.EventStore/ErrorHandling/AuthorizationExceptionHandler.cs`
- Client and testing:
    - `src/Hexalith.EventStore.Client/Gateway/IEventStoreGatewayClient.cs`
    - `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`
    - `src/Hexalith.EventStore.Client/Gateway/EventStoreQueryResult.cs`
    - `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayException.cs`
    - `src/Hexalith.EventStore.Testing/Fakes/FakeEventStoreGatewayClient.cs`
    - `src/Hexalith.EventStore.Testing/Fakes/FakeProjectionActor.cs`
- Docs:
    - `docs/reference/query-api.md`
    - `docs/reference/nuget-packages.md`
    - `docs/reference/problems/index.md`
    - `docs/reference/problems/*.md`
- Focused tests:
    - `tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryRequestTests.cs`
    - `tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryResponseTests.cs`
    - `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs`
    - `tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeEventStoreGatewayClientTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/QueryNotFoundExceptionHandlerTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/QueryExecutionFailedExceptionHandlerTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Validation/SubmitQueryRequestValidatorTests.cs`

Testing standards:

- Use xUnit v3, Shouldly, and NSubstitute where existing test projects already use them.
- Run test projects individually per repository guidance.
- Prefer contract/unit/controller/error-handler tests before integration tests.
- Integration tests under `tests/Hexalith.EventStore.IntegrationTests` require Docker and a running Aspire/DAPR environment.
- If AppHost or DAPR access-control changes become necessary, run through Aspire and record resource/log evidence. This story should not need apphost topology changes unless query timeout/freshness policy requires a new service invocation path.

## Files Likely Touched

- `src/Hexalith.EventStore.Contracts/Queries/*`
- `src/Hexalith.EventStore/Controllers/QueriesController.cs`
- `src/Hexalith.EventStore/Validation/SubmitQueryRequestValidator.cs`
- `src/Hexalith.EventStore/ErrorHandling/ProblemTypeUris.cs`
- `src/Hexalith.EventStore/ErrorHandling/QueryNotFoundExceptionHandler.cs`
- `src/Hexalith.EventStore/ErrorHandling/QueryExecutionFailedExceptionHandler.cs`
- `src/Hexalith.EventStore/ErrorHandling/ValidationProblemDetailsFactory.cs`
- `src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs`
- `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreQueryResult.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayException.cs`
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventStoreGatewayClient.cs`
- `src/Hexalith.EventStore.Testing/Builders/*` if query policy builders are added.
- `docs/reference/query-api.md`
- `docs/reference/nuget-packages.md`
- `docs/reference/problems/*`
- Related focused tests under `tests/Hexalith.EventStore.Contracts.Tests`, `tests/Hexalith.EventStore.Client.Tests`, `tests/Hexalith.EventStore.Testing.Tests`, and `tests/Hexalith.EventStore.Server.Tests`.

## Out of Scope

- Moving public command/query DTOs, broad gateway client API shape, or package ownership beyond the query-policy metadata required here; Story 22.1 owns the broader closure.
- Moving projection adapter contracts, actor serialization, actor type naming, or actor ID derivation beyond taxonomy integration; Story 22.2 owns that boundary.
- Implementing tenant lifecycle, membership, role, permission, or 401/403 auth reason-code adapters; Story 22.3 owns that boundary.
- Pub/sub ordering, retries, outbox/drain, dead-letter policy, and backend deployment matrices; Story 22.5 owns those.
- Stream read/replay APIs and projection rebuild checkpoints; Story 22.6 owns those.
- Payload/snapshot protection hooks, unreadable protected data behavior, crypto-shredding, and protected-data redaction across all operational surfaces; Stories 22.7a through 22.7d own those.
- Broad AppHost, DAPR access-control, Parties repository, Admin UI, or Keycloak changes unless directly required by query policy validation and approved by evidence.

## References

- `_bmad-output/planning-artifacts/epics.md#Story 22.4: Query Behavior Policy and Error Taxonomy`
- `_bmad-output/planning-artifacts/prd.md#Public Gateway and Downstream Integration Contracts - v1.1 (FR83-FR104)`
- `_bmad-output/planning-artifacts/architecture.md#ADR-P6: Public Gateway Contracts Live in Contracts, Client, and Testing Packages`
- `_bmad-output/planning-artifacts/architecture.md#ADR-P7: Projection Query Adapter Is a Public Domain Integration Contract`
- `_bmad-output/planning-artifacts/architecture.md#ADR-P8: EventStore Owns Gateway Tenant/RBAC Enforcement`
- `_bmad-output/planning-artifacts/architecture.md#ADR-P9: Downstream Query, Publishing, Replay, and Payload-Protection Policies Are Platform Contracts`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-parties-integration-contract-gaps.md`
- `_bmad-output/implementation-artifacts/22-1-gateway-command-query-contract-closure-and-package-docs.md`
- `_bmad-output/implementation-artifacts/22-2-projection-adapter-contract-and-generic-query-actor-model.md`
- `_bmad-output/implementation-artifacts/22-3-gateway-owned-tenant-and-rbac-enforcement.md`
- `_bmad-output/project-context.md`
- `_bmad-output/process-notes/story-creation-lessons.md#L08 - Party Review Vs. Elicitation`
- `docs/reference/query-api.md`
- `docs/reference/problems/index.md`
- `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-12T20:02:08Z - Pre-dev hardening preflight passed via `_bmad-output/process-notes/predev-preflight-latest.json`.
- 2026-05-12T22:03:52+02:00 - Story creation context gathered from Epic 22, PRD FR93-FR95, architecture ADR-P6/P7/P8/P9, Stories 22.1-22.3, current query controller/handler/router/contracts/client/fake/docs/test surfaces, recent commits, project context, and lessons ledger.

### Completion Notes List

- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, generated API docs, or submodules.
- Party-mode review has NOT yet been run for this story.
- Advanced elicitation has NOT yet been run for this story.

### File List

- `_bmad-output/implementation-artifacts/22-4-query-behavior-policy-and-error-taxonomy.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, generated API docs, or submodules.
- YAML validation passed for `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- `git diff --check` passed for the story artifact and sprint status with line-ending conversion warnings only.
- `npx markdownlint-cli2 _bmad-output/implementation-artifacts/22-4-query-behavior-policy-and-error-taxonomy.md` passed with 0 errors.
- Party-mode review has NOT yet been run for this story.
- Advanced elicitation has NOT yet been run for this story.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-12 | 0.1 | Created ready-for-dev story for query behavior policy and error taxonomy. | Codex automation |
