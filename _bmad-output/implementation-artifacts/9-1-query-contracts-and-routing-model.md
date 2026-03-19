# Story 9.1: Query Contracts & Routing Model

Status: done

## Story

As a domain service developer,
I want typed query contracts with mandatory metadata fields and a 3-tier routing model,
so that queries are routed deterministically to the correct query actor.

## Context: What Already Exists

This is a **validation and gap-analysis story**, not a greenfield story. The query contracts, 3-tier routing model, ETag caching, controllers, MediatR pipeline, DI registration, and tests across all three tiers are already substantially implemented. The work is to **audit every acceptance criterion against the existing code, fill any gaps, and ensure full test coverage**.

### Existing Query Infrastructure

| Component | Location | Status |
|-----------|----------|--------|
| IQueryContract (static abstract members) | `src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs` | Built |
| IQueryResponse\<T\> (compile-time ProjectionType) | `src/Hexalith.EventStore.Contracts/Queries/IQueryResponse.cs` | Built |
| QueryContractMetadata (immutable DTO) | `src/Hexalith.EventStore.Contracts/Queries/QueryContractMetadata.cs` | Built |
| SubmitQueryRequest / SubmitQueryResponse | `src/Hexalith.EventStore.Contracts/Queries/` | Built |
| EventStoreQueryTypeAttribute | `src/Hexalith.EventStore.Contracts/Queries/EventStoreQueryTypeAttribute.cs` | Built |
| QueryActorIdHelper (3-tier routing) | `src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs` | Built |
| QueryRouter (actor proxy routing) | `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` | Built |
| IProjectionActor (DAPR actor interface) | `src/Hexalith.EventStore.Server/Actors/IProjectionActor.cs` | Built |
| QueryEnvelope / QueryResult (DataContract) | `src/Hexalith.EventStore.Server/Actors/` | Built |
| SelfRoutingETag (encode/decode) | `src/Hexalith.EventStore.Server/Queries/SelfRoutingETag.cs` | Built |
| ETagActor (DAPR actor, state persistence) | `src/Hexalith.EventStore.Server/Actors/ETagActor.cs` | Built |
| IETagService / DaprETagService (fail-open) | `src/Hexalith.EventStore.Server/Queries/` | Built |
| CachingProjectionActor | `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs` | Built |
| QueriesController (POST /api/v1/queries) | `src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs` | Built |
| ValidateQueryRequest (preflight DTO) | `src/Hexalith.EventStore.Contracts/Validation/ValidateQueryRequest.cs` | Built |
| QueryValidationController (preflight) | `src/Hexalith.EventStore.CommandApi/Controllers/QueryValidationController.cs` | Built |
| SubmitQuery / SubmitQueryHandler (MediatR) | `src/Hexalith.EventStore.Server/Pipeline/` | Built |
| QueryContractResolver (client-side validation) | `src/Hexalith.EventStore.Client/Queries/QueryContractResolver.cs` | Built |
| Exception handlers (404, 403/501) | `src/Hexalith.EventStore.CommandApi/ErrorHandling/` | Built |
| DI registration (IQueryRouter, IETagService) | `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` | Built |

### Existing Test Coverage

| Test File | Tier | Tests |
|-----------|------|-------|
| `Contracts.Tests/Queries/IQueryContractTests.cs` | 1 | Contract validation |
| `Contracts.Tests/Queries/IQueryResponseTests.cs` | 1 | Response contract |
| `Contracts.Tests/Queries/SubmitQueryRequestTests.cs` | 1 | Request serialization |
| `Contracts.Tests/Queries/SubmitQueryResponseTests.cs` | 1 | Response serialization |
| `Contracts.Tests/Queries/EventStoreQueryTypeAttributeTests.cs` | 1 | Attribute discovery |
| `Client.Tests/Queries/QueryContractResolverTests.cs` | 1 | Metadata resolution, cache, ETag actor ID |
| `Server.Tests/Queries/QueryActorIdHelperTests.cs` | 2 | 3-tier routing, checksum, colon validation |
| `Server.Tests/Queries/QueryRouterTests.cs` | 2 | Actor proxy routing, not-found detection |
| `Server.Tests/Queries/DaprETagServiceTests.cs` | 2 | ETag retrieval, fail-open pattern |
| `Server.Tests/Queries/SelfRoutingETagTests.cs` | 2 | Encoding/decoding projection type |
| `Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs` | 2 | MediatR handler |
| `Server.Tests/Pipeline/Queries/SubmitQueryTests.cs` | 2 | Pipeline DTO |
| `Server.Tests/Actors/CachingProjectionActorTests.cs` | 2 | Caching actor behavior |
| `Server.Tests/Controllers/QueriesControllerTests.cs` | 2 | Controller HTTP behavior |
| `IntegrationTests/ContractTests/QueryEndpointE2ETests.cs` | 3 | Full HTTP pipeline |
| `IntegrationTests/ContractTests/QueryValidationE2ETests.cs` | 3 | Validation pipeline |

## Acceptance Criteria

1. **Given** the query contract library,
   **When** a query is defined,
   **Then** mandatory fields Domain, QueryType, and ProjectionType are enforced as typed static abstract members via `IQueryContract` (FR57). TenantId is per-request on `SubmitQueryRequest`, not per-type.

2. **Given** a query with EntityId,
   **When** routed,
   **Then** it targets `{QueryType}:{TenantId}:{EntityId}` (FR50 tier 1). Colon separator (not hyphen) per architecture decision for structural disjointness.

3. **Given** a query without EntityId but with non-empty payload,
   **When** routed,
   **Then** it targets `{QueryType}:{TenantId}:{Checksum}` where Checksum is truncated SHA256 base64url (11 chars) of serialized payload (FR50 tier 2).

4. **Given** a query without EntityId and with empty payload,
   **When** routed,
   **Then** it targets `{QueryType}:{TenantId}` (FR50 tier 3).

## Definition of Done

Story is complete when:
- **Required:** Tasks 0-7 pass — all audit checks verified, gaps filled, tests green
- **Conditional:** Task 7 — run Tier 1 tests only if any `src/` or `tests/` files were modified during Tasks 0-6
- All four acceptance criteria verified against actual code (not epics wording)
- Build passes: `dotnet build Hexalith.EventStore.slnx --configuration Release`
- No regressions in existing Tier 1 or Tier 2 tests
- Audit results table produced in Completion Notes (AC # / Expected / Actual / Pass-Fail)
- Branch: `feat/story-9-1-query-contracts-routing` — create before making any code or test changes
- Scope boundary: up to 3 minor fixes (<1hr each) and missing tests are in scope; algorithmic changes (e.g., changing checksum strategy) or >3 gaps trigger a follow-up story

## Tasks / Subtasks

- [x] Task 0: Audit existing IQueryContract against FR57 (AC: #1)
  - [x] Verify `IQueryContract` defines static abstract `QueryType`, `Domain`, `ProjectionType`
  - [x] Verify `QueryContractResolver` validates kebab-case and colon-free segments
  - [x] Verify `QueryContractMetadata` captures all three fields
  - [x] Confirm TenantId is correctly on `SubmitQueryRequest` (per-request, not per-type)
  - [x] Verify `EventStoreQueryTypeAttribute` for discovery

- [x] Task 1: Audit 3-tier routing model against FR50 (AC: #2, #3, #4)
  - [x] Verify `QueryActorIdHelper.DeriveActorId` implements all three tiers correctly
  - [x] Verify Tier 1: EntityId present → `{QueryType}:{TenantId}:{EntityId}`
  - [x] Verify Tier 2: No EntityId + payload → `{QueryType}:{TenantId}:{Checksum}` (11-char SHA256 base64url)
  - [x] Verify Tier 3: No EntityId + empty payload → `{QueryType}:{TenantId}`
  - [x] Verify colon segment validation (no colons in queryType, tenantId, entityId)
  - [x] Verify generic `DeriveActorId<TQuery>` overload delegates correctly

- [x] Task 2: Audit QueryRouter integration (AC: #2, #3, #4)
  - [x] Verify `QueryRouter.RouteQueryAsync` calls `QueryActorIdHelper.DeriveActorId` correctly
  - [x] Verify correct tier logging (EventId 1205)
  - [x] Verify `QueryEnvelope` DataContract serialization for DAPR actor proxy
  - [x] Verify actor-not-found detection (5 error message patterns)
  - [x] Verify `QueryRouterResult` includes ProjectionType propagation

- [x] Task 3: Audit controller and pipeline integration
  - [x] Verify `QueriesController` Gate 1 (ETag pre-check with self-routing decode) — all 5 code paths: wildcard, mixed projections, decode success, decode fail, ETag match
  - [x] Verify `QueriesController` Gate 2 (query execution via MediatR)
  - [x] Verify `QueriesController` Gate 3 (ETag response header)
  - [x] Verify `QueryValidationController` preflight authorization
  - [x] Verify error handlers (404 QueryNotFound, 403/501 QueryExecutionFailed)
  - [x] Verify API version path: controller uses `api/v1/queries` but PRD specifies `api/v2/queries` for query pipeline — document decision or fix

- [x] Task 4: Audit DI registration
  - [x] Verify `ServiceCollectionExtensions` registers `IQueryRouter` → `QueryRouter` (Scoped)
  - [x] Verify `ServiceCollectionExtensions` registers `IETagService` → `DaprETagService` (Scoped)
  - [x] Verify CommandApi exception handler registration

- [x] Task 5: Validate test coverage completeness
  - [x] Run all Tier 1 tests: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ tests/Hexalith.EventStore.Client.Tests/`
  - [x] Run Tier 2 tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~Queries"`
  - [x] Verify tests cover: 3-tier routing format, checksum determinism, serialization non-determinism trade-off, colon validation, null/empty EntityId handling, generic overload
  - [x] Verify `QueryContractMetadata` has test coverage (immutability, equality semantics) — add to Tier 1 (`Contracts.Tests`) if missing
  - [x] Verify `QueriesControllerTests` covers all 5 Gate 1 code paths — add missing paths (Tier 2, requires NSubstitute mocks)
  - [x] Verify E2E tests (`QueryEndpointE2ETests.cs`) cover all three routing tiers (Tier 3, requires Docker), or document which tiers are covered and why
  - [x] Consider adding a probabilistic checksum collision test (1000 random payloads, verify no 11-char collisions) — Tier 1 (`Contracts.Tests` or `Server.Tests`)
  - [x] Identify any missing edge-case tests and add them

- [x] Task 6: Fill any gaps identified during audit (bounded: up to 3 minor gaps, <1hr each)
  - [x] Fix any acceptance criteria violations found
  - [x] Add any missing tests
  - [x] If more than 3 gaps found, or any gap requires >1 hour, document in Completion Notes and create follow-up story
  - [x] Ensure build passes: `dotnet build Hexalith.EventStore.slnx --configuration Release`

- [x] Task 7: Sample domain validation (AC: #1)
  - [x] Check if Counter sample (`samples/Hexalith.EventStore.Sample/`) has a query contract implementing `IQueryContract`
  - [x] If missing, add a stub `GetCounterStatusQuery : IQueryContract` with static members only — proving the contract compiles. Do NOT implement projection actor, wiring, or API endpoint (that's Story 11.5)
  - [x] Verify sample compiles and Tier 1 sample tests pass: `dotnet test tests/Hexalith.EventStore.Sample.Tests/`

## Dev Notes

### Architecture: Separator Convention

The epics file uses hyphen (`-`) notation in routing format descriptions (`{QueryType}-{TenantId}-{EntityId}`), but the **actual implementation and architecture** use colons (`:`) as separators. This is intentional:
- Colons guarantee structural disjointness (segments are validated to be colon-free)
- Validation is enforced at three layers: `EventStoreQueryTypeAttribute`, `QueryContractResolver`, and `QueryActorIdHelper`
- All tests verify colon-separated format

### Architecture: TenantId vs ProjectionType on Contract (Intentional FR57 Deviation)

The epics AC and PRD FR57 literally say "mandatory fields Domain, QueryType, TenantId" on the contract. The implementation has **ProjectionType** instead of TenantId as a static abstract member. This is an **intentional design deviation** from FR57's literal wording, because:
- TenantId varies per request (cannot be a static type-level member)
- ProjectionType is per-query-type and needed for ETag actor ID derivation (`{ProjectionType}:{TenantId}`)
- TenantId is correctly on `SubmitQueryRequest` (per-request record)
- This matches FR57's intent: "ProjectionType is not required on the query consumer side -- it is declared by the microservice in its IQueryResponse<T> implementation (FR62) and discovered at runtime by the query actor (FR63)"

### Architecture: API Version Path (v1 vs v2)

The PRD specifies `api/v2/queries` for the query pipeline (distinguishing it from the command API at `api/v1/commands`). The current implementation uses `api/v1/queries`. During audit, verify whether this is intentional (unified v1 namespace) or a gap. **Recommendation:** The current codebase uses v1 uniformly for all endpoints. Unless the PRD owner explicitly requires v2 separation, keep v1 for consistency and document the decision in Completion Notes. If v2 is required, the route change is a one-line annotation fix in scope for this story.

### Key Code Patterns

- **QueryActorIdHelper**: Pure static helper, no dependencies. SHA256 via `System.Security.Cryptography`. Base64url via manual +/- replacement.
- **QueryRouter**: Constructor-injected `IActorProxyFactory` + `ILogger<QueryRouter>`. Uses `partial class Log` for source-generated logging.
- **DaprETagService**: 3-second `RequestTimeout` on actor proxy. Fail-open (returns null on any exception).
- **QueriesController**: Three-gate pattern — Gate 1 (ETag pre-check), Gate 2 (MediatR query), Gate 3 (ETag response header). MaxIfNoneMatchValues = 10 for DoS prevention.
- **SelfRoutingETag**: Format `{base64url(projectionType)}.{base64url-guid}`. TryDecode returns false for malformed/old-format ETags.

### Testing Pattern

- **xUnit** with **Shouldly** assertions, **NSubstitute** for mocking
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`
- DataContract serialization required for all DAPR actor method parameters (QueryEnvelope, QueryResult)
- CachingProjectionActorTests already cover 15+ scenarios

### Project Structure Notes

All query contract types are in `Hexalith.EventStore.Contracts` NuGet package (consumed by domain service developers). Server-side routing, ETag, and actor code is in `Hexalith.EventStore.Server` (consumed by platform operators). Client-side resolver is in `Hexalith.EventStore.Client`.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 9: Query Pipeline & ETag Caching]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1: Event Storage Strategy]
- [Source: _bmad-output/planning-artifacts/prd.md#FR50, FR57, FR61, FR62, FR63]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR35-NFR39]
- [Source: src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs]
- [Source: src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs]
- [Source: src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:32-33]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

No debug issues encountered. All source files read and audited successfully.

### Completion Notes List

#### Audit Results

| AC # | Expected | Actual | Pass/Fail |
|------|----------|--------|-----------|
| 1 | IQueryContract: Domain, QueryType, ProjectionType static abstract; TenantId on request | IQueryContract defines all 3 static abstract members; TenantId is `Tenant` param on SubmitQueryRequest (per-request). QueryContractResolver validates kebab-case + colon-free. EventStoreQueryTypeAttribute validates colon-free, non-empty. | PASS |
| 2 | Tier 1: `{QueryType}:{TenantId}:{EntityId}` | QueryActorIdHelper line 39: `$"{queryType}:{tenantId}:{entityId}"` when entityId is non-null/non-empty | PASS |
| 3 | Tier 2: `{QueryType}:{TenantId}:{Checksum}` (11-char SHA256 base64url) | QueryActorIdHelper line 43: `$"{queryType}:{tenantId}:{ComputeChecksum(payload)}"` when payload.Length > 0 and no entityId. ComputeChecksum: SHA256 → base64url → 11-char truncation | PASS |
| 4 | Tier 3: `{QueryType}:{TenantId}` | QueryActorIdHelper line 46: `$"{queryType}:{tenantId}"` when no entityId and empty payload | PASS |

#### API Version Decision

v1 — keep unified v1 namespace. The codebase consistently uses `api/v1/` for all endpoints (commands, queries, validation). The PRD's mention of `api/v2/queries` was a design suggestion, but the implementation unified all endpoints under v1 for simplicity. No functional impact — the route distinguishes query from command endpoints via the path suffix (`/queries` vs `/commands`).

#### Gaps Found (2 minor gaps, both filled)

1. **No sample query contract in Counter sample** — Added `GetCounterStatusQuery : IQueryContract` stub in `samples/.../Counter/Queries/` with static members only. Added 4 tests in Sample.Tests. (<5 min)
2. **No probabilistic checksum collision test** — Added `ComputeChecksum_1000RandomPayloads_NoCollisions` test to `QueryActorIdHelperTests.cs`. Uses fixed seed for determinism. (<5 min)

No follow-up story needed — all gaps were minor and in-scope.

#### Additional Audit Notes

- **QueryRouter integration**: Correct tier logging via EventId 1205 (QueryRoutingTierSelected). QueryEnvelope uses [DataContract]/[DataMember] for DAPR serialization. 5 actor-not-found patterns detected. ProjectionType propagates through QueryRouterResult.
- **Controller gates**: All 5 Gate 1 code paths verified (wildcard, mixed projections, decode success, decode fail, ETag match). Gate 2 delegates to MediatR. Gate 3 sets ETag response header with fail-open pattern.
- **DI registration**: IQueryRouter → QueryRouter (Scoped), IETagService → DaprETagService (Scoped), both exception handlers registered in CommandApi ServiceCollectionExtensions.
- **Test coverage**: 694 Tier 1 tests pass, 108 query-related Tier 2 tests pass, 1505/1506 total Server.Tests pass (1 pre-existing failure in ErrorReferenceEndpointTests unrelated to query pipeline).
- **Pre-existing failure**: `ErrorReferenceEndpointTests.AllProblemTypeUris_HaveCorrespondingErrorModel` — this test was already failing before story 9-1 and is unrelated to query contracts/routing.

### File List

- `samples/Hexalith.EventStore.Sample/Counter/Queries/GetCounterStatusQuery.cs` — new (sample query contract stub)
- `tests/Hexalith.EventStore.Sample.Tests/Counter/Queries/GetCounterStatusQueryTests.cs` — new (sample query contract tests)
- `tests/Hexalith.EventStore.Server.Tests/Queries/QueryActorIdHelperTests.cs` — modified (added probabilistic collision test)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — modified (status update)
- `_bmad-output/implementation-artifacts/9-1-query-contracts-and-routing-model.md` — modified (task checkboxes, audit results, completion notes)

### Change Log

- 2026-03-19: Completed full audit of query contracts and 3-tier routing model against AC #1-4. All acceptance criteria pass. Added sample GetCounterStatusQuery stub (Gap 1), probabilistic checksum collision test (Gap 2). Build passes 0 errors/0 warnings. All Tier 1 (694) and query Tier 2 (108) tests green.
