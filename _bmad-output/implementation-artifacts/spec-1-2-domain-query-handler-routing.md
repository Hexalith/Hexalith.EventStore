---
title: '1.2 Domain Query Handler Routing'
type: 'feature'
created: '2026-07-06'
status: 'done'
baseline_revision: '2289f3fd7a048acae9b7f668d374ba1837df9a36'
final_revision: '35e2465e10d6ae1b69b361fc37504ab285b9ec80'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/1-2-domain-query-handler-routing.md'
warnings: ['oversized']
---

<intent-contract>

## Intent

**Problem:** Domain query handlers can already be discovered and routed, but duplicate handler routes are silently first-match and producer query metadata is dropped before it reaches gateway clients. This blocks downstream freshness, projection-version, paging, degraded-state, and warning evidence from being trusted.

**Approach:** Harden the existing handler route table and operational metadata path, then add `QueryResponseMetadata` as an additive hop through domain/projection `QueryResult`, router results, pipeline results, gateway response merge rules, and typed client exposure.

## Boundaries & Constraints

**Always:** Preserve `IDomainQueryHandler` as the author seam; keep `/query` on `MapEventStoreDomainService()`; keep `HandlerAwareQueryRouter` as a decorator over the projection actor route; preserve DataContract namespace/source compatibility with optional members; treat missing freshness as unknown; use `.slnx` only for restore/build and run tests by project.

**Block If:** Metadata propagation requires changing existing public query payload shapes in a non-additive way, changing DAPR actor method names, modifying submodule files, or deciding a new freshness taxonomy outside `query_projection_stale`.

**Never:** Do not move Story 1.3 read-model/cursor work into this story; do not implement generated REST/UI metadata behavior; do not route Tenants reads or migrate Tenants; do not infer projection version from ETag; do not treat request paging echo as authoritative page evidence.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Handler route | Domain service has one handler for `(domain, queryType)` | `/query` and gateway handler routing invoke that handler and preserve its payload, projection type, and metadata | No error expected |
| Duplicate handler route | Two handlers claim same domain/query type | Startup/dispatch fails deterministically with a clear duplicate-route error instead of first-match behavior | Build/test failure proves duplicate is rejected |
| Unsupported route | No handler metadata exists or registry read fails | Gateway delegates to projection actor route unchanged | Registry failure is fail-safe, not handler support |
| Producer metadata | Handler or projection actor returns metadata | `QueryResult` -> `QueryRouterResult` -> `SubmitQueryResult` -> `SubmitQueryResponse` -> client preserves metadata additively | Malformed/missing payload behavior remains unchanged |
| Gateway merge | Producer and gateway metadata both exist | Producer wins for freshness, projection version, paging, degraded state, warnings; gateway strong ETag wins HTTP validator; gateway fills `ServedAt` only if absent; HTTP outcome sets `IsNotModified` | Unknown freshness stays `null`; explicit freshness policy fails closed |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` -- query handler discovery and deterministic duplicate-route validation.
- `src/Hexalith.EventStore.DomainService/DomainQueryDispatcher.cs` -- `/query` runtime dispatch and metadata-preserving handler result path.
- `src/Hexalith.EventStore.DomainService/AdminOperationalIndexMetadata.cs` -- handler-served query type advertisement.
- `src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs` -- gateway materialization and merge of `admin:query-types:{domain}`.
- `src/Hexalith.EventStore/Queries/DaprDomainQueryHandlerRegistry.cs` -- fail-safe query type lookup cache for handler-aware routing.
- `src/Hexalith.EventStore.Contracts/Queries/QueryResult.cs` -- additive DataContract metadata member for actor/domain-service boundaries.
- `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs` and `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs` -- internal metadata hops.
- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`, `src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs`, and `src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs` -- actor, handler, and MediatR propagation.
- `src/Hexalith.EventStore/Controllers/QueriesController.cs` -- gateway metadata merge and freshness fail-closed policy.
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs` -- additive normalization and typed/untyped metadata parity.
- `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs` -- audit cache-hit behavior so metadata is not lost or made misleading.
- `docs/reference/query-api.md` -- replace outdated warning only after the end-to-end metadata path is real.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` and `src/Hexalith.EventStore.DomainService/DomainQueryDispatcher.cs` -- reject duplicate `(Domain, QueryType)` handler routes deterministically while keeping unsupported queries as `QueryResult.Failure`.
- [x] `src/Hexalith.EventStore.DomainService/AdminOperationalIndexMetadata.cs`, `src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs`, and `src/Hexalith.EventStore/Queries/DaprDomainQueryHandlerRegistry.cs` -- preserve query-type advertisement, merged catalog data, state-key materialization, cache behavior, and fail-safe fallback.
- [x] `src/Hexalith.EventStore.Contracts/Queries/QueryResult.cs` -- add optional `QueryResponseMetadata? Metadata` as an additive `DataMember`; update factory methods without breaking existing positional callers.
- [x] `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs`, `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs`, `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`, `src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs`, and `src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs` -- propagate metadata through projection-actor and handler-aware routes.
- [x] `src/Hexalith.EventStore/Controllers/QueriesController.cs` -- merge producer and gateway metadata by AD-14 rules, leave unknown freshness as `null`, fail closed for explicit freshness policy when freshness is unknown/stale, and avoid inventing authoritative paging evidence from request inputs.
- [x] `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs` -- keep normalization additive and prove typed/untyped query results expose the same metadata, including `304` not-modified metadata.
- [x] `docs/reference/query-api.md` -- document the implemented evidence contract and keep cursor, ETag, and raw metadata internals support-safe.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/`, `tests/Hexalith.EventStore.DomainService.Tests/`, `tests/Hexalith.EventStore.QueryRouting.Tests/`, `tests/Hexalith.EventStore.Server.Tests/`, and `tests/Hexalith.EventStore.Client.Tests/` -- add focused coverage for the matrix and acceptance criteria.

**Acceptance Criteria:**
- Given discovered domain query handlers, when the SDK host starts, then handler routes are registered by domain/query type and duplicate routes fail predictably.
- Given operational metadata from one or more domain services, when gateway indexes are materialized, then handler query types merge without losing command, event, aggregate, or projection catalog data.
- Given a handler-supported query, when routed by the gateway, then `IDomainQueryInvoker` is used and non-handler queries still use the projection actor route.
- Given handler or projection results include metadata, when results cross every platform query hop, then freshness, projection version, paging, degraded state, warnings, ETag, served-at, and not-modified fields are preserved or merged by explicit rules.
- Given freshness evidence is missing and freshness is requested, when the gateway responds, then it fails closed with `query_projection_stale` instead of returning current-looking metadata.

## Spec Change Log

## Review Triage Log

### 2026-07-06 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 11: (high 1, medium 8, low 2)
- defer: 1: (high 0, medium 1, low 0)
- reject: 0
- addressed_findings:
  - `[high]` `[patch]` Appended metadata parameters removed old binary signatures on query result contracts; added compatibility constructors/factory overloads and reflection tests for `QueryResult`, `QueryRouterResult`, and `SubmitQueryResult`.
  - `[medium]` `[patch]` `MaxStaleness` could pass with `IsStale=false` and no age check; enforcement now requires producer `ServedAt` within the requested window and fails closed otherwise.
  - `[medium]` `[patch]` Projection cache hits could reuse old `IsStale=false`/`ServedAt` evidence; cache hits now preserve stable metadata while clearing time-sensitive freshness evidence.
  - `[medium]` `[patch]` Freshness failures could inherit a success ETag header; the controller now enforces freshness before assigning the 200-response ETag header.
  - `[medium]` `[patch]` Client metadata ETag precedence contradicted docs; `EventStoreGatewayClient` now lets the HTTP strong ETag override body metadata and tests assert that behavior.
  - `[medium]` `[patch]` Failure metadata on `QueryResult.Failure` was dropped by router failure paths; projection and handler-aware routers now propagate failure metadata into `QueryRouterResult`.
  - `[medium]` `[patch]` Query-type matching was case-sensitive in `DaprDomainQueryHandlerRegistry` but case-insensitive in dispatch; registry lookup and cached sets now use ordinal-ignore-case semantics.
  - `[medium]` `[patch]` Duplicate query handlers were not rejected during SDK host setup; endpoint mapping now validates registered handler routes before routes are added.
  - `[low]` `[patch]` Duplicate handler detection used a synthetic separator key; shared validation now compares domain/query pairs directly without separator-collision risk.
  - `[low]` `[patch]` Query API docs still assigned projection freshness checks to a future story; the stale sentence was updated to reflect implemented gateway freshness enforcement.
  - `[medium]` `[patch]` Validator still rejected meaningful freshness policies before controller enforcement; it now allows freshness requests and only rejects negative `MaxStaleness`.

### 2026-07-06 — Review pass (follow-up)
- intent_gap: 0
- bad_spec: 0
- patch: 1: (high 0, medium 1, low 0)
- defer: 0
- reject: 16
- addressed_findings:
  - `[medium]` `[patch]` `EnforceFreshnessPolicy` subtracted a caller-controlled `MaxStaleness` from the served-at timestamp (`servedAt.Subtract(maxStaleness)`); a very large `MaxStaleness` (e.g. `TimeSpan.MaxValue`) with a fresh producer `ServedAt` underflowed `DateTimeOffset.MinValue` and threw an unhandled `ArgumentOutOfRangeException` (HTTP 500 instead of a clean freshness outcome). Now compares the age as a bounded `TimeSpan` difference (`servedAt - producerServedAt <= maxStaleness`) — algebraically identical for valid inputs, overflow-safe for any `MaxStaleness` — and added a `TimeSpan.MaxValue` regression test in `QueriesControllerTests`.
- reject rationale (notable): request-paging-echo removal, validation no longer blanket-rejecting freshness, cache-hit clearing of time-sensitive `IsStale`/`ServedAt`, and `MaxStaleness == 0` failing closed are all intent-contract-mandated behavior; the "missing `[DataMember(Order)]`" finding is a false premise (0 of 36 `DataMember`s in the Contracts project use `Order` — appending order would be inconsistent with the repo convention); router metadata dropped on missing/malformed-payload paths and the client ETag/`IsNotModified` divergences have no consumer-visible effect (`SubmitQueryHandler` throws without router-failure metadata; the gateway always sets `IsNotModified=false` on 200); the domain-key casing gap is unreachable because the request validator forces lowercase domains.

## Design Notes

Existing handler routing is the foundation. Treat this as hardening and metadata propagation, not a new routing architecture. Add optional record parameters at the end of existing positional contracts where possible, and keep `QueryResponseMetadata` as the single evidence carrier.

## Verification

**Commands:**
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- expected: succeeds with warnings as errors.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` -- expected: query contract/DataContract tests pass.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/` -- expected: client metadata tests pass.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- expected: handler discovery/dispatch/metadata tests pass.
- `dotnet test tests/Hexalith.EventStore.QueryRouting.Tests/` -- expected: handler-aware routing tests pass.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/` -- expected: focused server tests pass, or document the known pre-existing CA2007 blocker exactly.

## Auto Run Result

Status: `done`

Summary:
- Implemented deterministic duplicate domain/query handler route validation at SDK endpoint mapping, admin metadata generation, and runtime dispatch.
- Propagated optional `QueryResponseMetadata` through domain/projection results, router results, MediatR query results, gateway response metadata, and gateway client normalization.
- Enforced explicit freshness policies fail-closed, including `MaxStaleness` age checks, while keeping unknown freshness as `null` when no policy is requested.
- Added compatibility overloads for existing public query result signatures and focused tests for metadata propagation, ETag precedence, failure metadata, cache-hit freshness normalization, and route validation.
- Updated query API documentation and logged one deferred route-provenance gap in `_bmad-output/implementation-artifacts/deferred-work.md`.

Review Findings:
- Patched 11 review findings: 1 high, 8 medium, 2 low.
- Deferred 1 medium finding: handler-backed query routes need an explicit provenance contract before gateway projection ETag fallback can be made route-aware.

Verification:
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` -- passed.
- `dotnet test tests/Hexalith.EventStore.QueryRouting.Tests/` -- passed.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- passed.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/` -- passed.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/` -- passed.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed with 0 warnings and 0 errors.
- `git diff --check` -- passed.

Residual Risks:
- Handler-backed query responses still need route provenance so the gateway can decide whether projection ETags are valid for that response path.
- Follow-up review is recommended because review rework touched public query contract compatibility, freshness enforcement, and startup route validation.

### Follow-up review pass — 2026-07-06

Ran an independent adversarial + edge-case review of the story diff (baseline `2289f3fd`..HEAD). Triage: 1 medium patch, 0 intent_gap, 0 bad_spec, 0 defer, 16 reject.

- Patched: `EnforceFreshnessPolicy` could throw an unhandled `ArgumentOutOfRangeException` (HTTP 500) when a caller supplied a very large `MaxStaleness` (up to `TimeSpan.MaxValue`) alongside a fresh producer `ServedAt`, because it subtracted the `TimeSpan` from the served-at timestamp and underflowed `DateTimeOffset.MinValue`. Now compares the age as a bounded `TimeSpan` difference (`servedAt - producerServedAt <= maxStaleness`), which is algebraically identical for valid inputs and overflow-safe for any `MaxStaleness`. Added a `TimeSpan.MaxValue` regression test.
- Files changed this pass: `src/Hexalith.EventStore/Controllers/QueriesController.cs` (freshness age comparison hardened) and `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs` (regression test).
- Rejected findings were either intent-contract-mandated behavior (paging-echo removal, freshness fail-closed on unknown/zero staleness, cache-hit clearing of time-sensitive freshness), a false premise (`DataMember(Order)` is used nowhere in the Contracts project — 0 of 36 members), or had no consumer-visible effect (router-failure metadata is discarded by `SubmitQueryHandler`; the domain-key casing gap is unreachable because the request validator forces lowercase domains).
- Verification: `dotnet build Hexalith.EventStore.slnx -c Release` → 0 warnings / 0 errors; `Server.Tests` 2244 passed / 25 skipped, `Contracts.Tests` 562, `Client.Tests` 489, `DomainService.Tests` 47, `QueryRouting.Tests` 5 — all green.
- No further follow-up review recommended: the single fix is localized and semantically preserving with a regression test.
