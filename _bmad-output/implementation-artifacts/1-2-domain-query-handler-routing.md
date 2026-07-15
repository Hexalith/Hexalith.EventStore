# Story 1.2: Domain Query Handler Routing

> Historical execution artifact. The July 15 restructure reissues the active story as
> `1-2-domain-query-routing-and-response-provenance.md`; the completed implementation and
> review evidence in this file remains authoritative for the original routing slice.

Status: ready-for-dev

## Story

As a domain author,
I want domain queries to be implemented as plain query handlers and routed by the platform,
so that my domain can expose query behavior without hosting a custom projection/query actor.

## Correct-Course Reconciliation

This story is created after the approved 2026-07-05 query metadata sequencing correction. The active scope is not the older A7-only "handler dispatch" scope from the June 2 proposal. Story 1.2 now owns the platform metadata path required by downstream query/read-model/API work: `QueryResponseMetadata` propagation, gateway metadata merge rules, freshness-policy fail-closed behavior, and typed client metadata exposure.

Superseded scope is explicitly excluded from implementation:
- Do not defer query metadata propagation to deleted Story 7.6.
- Do not treat generated REST/UI metadata as production-backed until the real gateway path preserves it.
- Do not move Tenants adoption into this story; Tenants migration belongs to Story 1.6 and later Epic 2 proof work.
- Do not absorb Story 1.3 cursor/read-model work. This story must preserve existing `QueryPagingMetadata` when producers supply it and avoid claiming request paging echo as authoritative evidence.

Sources: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-query-metadata-sequencing.md`, `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-query-metadata-propagation.md`, `_bmad-output/planning-artifacts/epics.md`, `_bmad-output/planning-artifacts/architecture.md`.

## Acceptance Criteria

1. Given a domain module registers one or more `IDomainQueryHandler` implementations, when `AddEventStoreDomainService()` runs, then handlers are discovered and registered by domain and query type, and duplicate or unsupported query routes fail predictably without manual switch-based dispatch.

2. Given the domain-service SDK exposes operational index metadata, when the gateway reads `/admin/operational-index-metadata`, then handler-served query types are advertised per domain, and the gateway persists or caches that metadata for routing decisions.

3. Given the gateway receives a query for a handler-served domain/query type, when `HandlerAwareQueryRouter` resolves the route, then it invokes the target domain service `/query` endpoint, and it falls back to the projection-actor router when no handler is declared.

4. Given a domain query handler or projection actor returns query metadata, when the result crosses `QueryResult`, `QueryRouterResult`, `SubmitQueryResult`, `SubmitQueryResponse`, and `EventStoreQueryResult`, then `QueryResponseMetadata` is preserved additively through each platform type, and the gateway no longer drops domain-produced freshness, projection version, paging, warning, or degraded-state metadata.

5. Given the gateway creates HTTP response metadata, when domain metadata and gateway metadata both exist, then metadata is merged by explicit rules: domain/projection evidence wins for freshness, projection version, paging, degraded state, and warnings; gateway ETag header value wins for the HTTP validator; gateway fills `ServedAt` only when absent; `IsNotModified` is set by the HTTP outcome.

6. Given freshness metadata is unavailable, when a query response is returned or `RequireFresh` / `MaxStaleness` is requested, then freshness is represented as unknown, not current, and freshness-dependent requests fail closed according to the existing `query_projection_stale` taxonomy instead of silently treating unknown freshness as current.

7. Given query routing is tested, when focused unit tests execute, then domain-side dispatch, operational metadata capture, handler-aware routing, fallback behavior, metadata propagation, gateway merge behavior, typed client metadata exposure, and backward-compatible projection-actor routing are verified.

## Tasks / Subtasks

- [ ] Preserve and harden domain-side handler discovery and dispatch. (AC: 1)
  - [ ] Keep `IDomainQueryHandler` in `src/Hexalith.EventStore.DomainService` as the domain author seam.
  - [ ] Add or verify duplicate handler behavior for same domain/query type. If duplicates are currently accepted by first-match behavior, make failure deterministic and covered by tests.
  - [ ] Keep `/query` mapped by `MapEventStoreDomainService()` and returning `QueryResult`.

- [ ] Preserve and test operational metadata capture. (AC: 2)
  - [ ] Keep `AdminOperationalIndexMetadata.DomainMetadata.QueryTypes` additive and backward compatible.
  - [ ] Keep gateway materialization of `admin:query-types:{domain}` through `AdminOperationalIndexHostedService`.
  - [ ] Add tests that prove multiple metadata sources merge query types without losing command/event/projection catalog data.

- [ ] Preserve and harden handler-aware routing. (AC: 3)
  - [ ] Keep `AddEventStoreDomainQueryRouting()` registered after server query-router registration so the `HandlerAwareQueryRouter` decorator wins.
  - [ ] Verify handler-based queries call `IDomainQueryInvoker` and non-handler queries delegate to the projection actor path unchanged.
  - [ ] Preserve cancellation propagation and fail-safe registry behavior: metadata read failures must route to the projection actor path, not create false handler support.

- [ ] Add `QueryResponseMetadata` to every platform query result hop. (AC: 4)
  - [ ] Add an optional `Metadata` member to `QueryResult` as an additive `DataMember` without changing its pinned DataContract namespace.
  - [ ] Add optional metadata to `QueryRouterResult` and `SubmitQueryResult`.
  - [ ] Pass metadata through `QueryRouter`, `HandlerAwareQueryRouter`, `SubmitQueryHandler`, and `QueriesController`.
  - [ ] Preserve source compatibility by adding optional parameters/defaults where records are positional.

- [ ] Implement gateway metadata merge rules. (AC: 5, 6)
  - [ ] In `QueriesController`, merge producer metadata with gateway metadata instead of replacing it.
  - [ ] Treat producer metadata as authoritative for `IsStale`, `ProjectionVersion`, `Paging`, `IsDegraded`, and `WarningCodes`.
  - [ ] Use the selected strong ETag header value for `QueryResponseMetadata.ETag`; fill it only when the producer omitted ETag or when the HTTP validator must win.
  - [ ] Fill `ServedAt` only when absent; set `IsNotModified` from the HTTP outcome.
  - [ ] Do not infer `ProjectionVersion` from ETag unless a projection explicitly produced that value.
  - [ ] Do not treat request `Paging` echo as proof of total count, next cursor, or page completeness.
  - [ ] Keep missing freshness as `IsStale = null`; do not serialize false/current by default.
  - [ ] For explicit freshness policy, either enforce against authoritative metadata or fail closed with `query_projection_stale`; never return a current-looking response when freshness is unknown.

- [ ] Preserve typed client metadata exposure. (AC: 4, 5, 7)
  - [ ] Keep `EventStoreGatewayClient.NormalizeMetadata` additive: it may fill ETag and `IsNotModified`, but it must not overwrite producer freshness, projection version, paging, degraded state, or warning codes.
  - [ ] Verify both `SubmitQueryAsync` and `SubmitQueryAsync<T>` expose identical metadata.
  - [ ] Verify `304` returns metadata with `IsNotModified = true` and the normalized strong ETag.

- [ ] Add focused tests. (AC: 1-7)
  - [ ] Contracts tests: `QueryResult` DataContract round-trip with and without metadata; old-shape/missing metadata remains valid.
  - [ ] DomainService tests: duplicate handler detection or deterministic failure; `/query` metadata survives dispatcher results.
  - [ ] Query routing tests: handler path and projection path both preserve metadata.
  - [ ] Server pipeline tests: `SubmitQueryHandler` passes metadata through and preserves existing failure classification.
  - [ ] Controller tests: merge rules, unknown freshness, explicit freshness policy fail-closed, request paging not treated as authoritative evidence.
  - [ ] Client tests: untyped/typed metadata preservation and `304` metadata.

- [ ] Update documentation only where behavior becomes real. (AC: 4-6)
  - [ ] Update `docs/reference/query-api.md` Projection Evidence Metadata after implementation, replacing the current warning that rich freshness/projection evidence is not guaranteed end to end.
  - [ ] Keep docs support-safe: no cursor internals, ETag internals, raw metadata, or raw payload examples as operational guidance.

## Dev Notes

### Current State

Existing handler-routing foundation is already present:
- `src/Hexalith.EventStore.DomainService/IDomainQueryHandler.cs` defines the domain author seam.
- `src/Hexalith.EventStore.DomainService/DomainQueryDispatcher.cs` resolves the first matching handler by domain/query type and currently returns `QueryResult.Failure(...)` when none matches.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` discovers handlers and maps `POST /query`.
- `src/Hexalith.EventStore.DomainService/AdminOperationalIndexMetadata.cs` reports handler query types in operational metadata.
- `src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs` writes `admin:query-types:{domain}`.
- `src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs` decorates `IQueryRouter` and routes handler-supported queries through `IDomainQueryInvoker`.
- `src/Hexalith.EventStore/Program.cs` calls `builder.Services.AddEventStoreDomainQueryRouting()`.

Current metadata gap:
- `src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs` already contains ETag, not-modified, stale, degraded, projection version, served-at, paging, and warning-code fields.
- `src/Hexalith.EventStore.Contracts/Queries/QueryResult.cs` does not carry `QueryResponseMetadata`, so domain/projection metadata cannot cross the DAPR actor/domain-service boundary today.
- `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs` and `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs` do not carry metadata.
- `src/Hexalith.EventStore/Controllers/QueriesController.cs` currently synthesizes metadata from ETag/current time/request paging and can drop producer evidence.
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs` already exposes metadata on `EventStoreQueryResult` and typed results; it must preserve richer metadata, not replace it.

Existing freshness behavior:
- `src/Hexalith.EventStore/Validation/SubmitQueryRequestValidator.cs` currently rejects explicit freshness policy fields as reserved and uses `QueryProblemReasonCodes.ProjectionStale`.
- `docs/reference/query-api.md` documents `query_projection_stale` as the fail-closed taxonomy and states rich freshness/projection evidence is not yet guaranteed end to end. Update it only after this story makes that true.

### Architecture Compliance

- Follow Architecture AD-14: query evidence crosses the gateway as `QueryResponseMetadata`, not payload-specific fields.
- Preserve AD-3: the EventStore gateway remains the command/query policy boundary.
- Preserve AD-2: domain modules remain domain-centric; do not add domain-specific routing switches or actors for this story.
- Preserve AD-12: tests that claim high-risk evidence must inspect state/read-model/end-state where applicable. This story is mostly Tier 1 unit coverage; any integration claim must go beyond status-code smoke tests.
- Do not modify submodule files for this story.

### Files Expected To Change

Likely updates:
- `src/Hexalith.EventStore.Contracts/Queries/QueryResult.cs`
- `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs`
- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`
- `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs`
- `src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs`
- `src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs`
- `src/Hexalith.EventStore/Controllers/QueriesController.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`
- `docs/reference/query-api.md`

Likely tests:
- `tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs`
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs`
- `tests/Hexalith.EventStore.QueryRouting.Tests/HandlerAwareQueryRouterTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs`
- `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs`

### Patterns To Preserve

- C# files stay single-type where new types are added.
- Use file-scoped namespaces, Allman braces, nullable-safe boundary validation, and `ConfigureAwait(false)` on awaited calls.
- Use Shouldly assertions and NSubstitute in tests; do not add raw `Assert.*` in new tests.
- Use `Hexalith.EventStore.slnx` for restore/build only. Run test projects individually.
- Do not add package versions to `.csproj`; versions are centralized in `Directory.Packages.props` and imported Hexalith build props.
- Query IDs/correlation IDs remain ULIDs or non-whitespace aggregate IDs per existing rules; never introduce `Guid.TryParse` validation for EventStore identifiers.

### Version And External Documentation Notes

Repo-pinned versions win. Current local package pins include .NET SDK `10.0.301`, Aspire `13.4.6`, Dapr client/ASP.NET/actors `1.18.4`, MediatR `14.2.0`, FluentValidation `12.1.1`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `6.0.0-rc.1`.

External docs checked on 2026-07-06:
- Microsoft `DataContractSerializer` .NET 10 docs: adding metadata to `QueryResult` must remain additive and preserve old serialized shapes. https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.datacontractserializer?view=net-10.0
- Microsoft ASP.NET Core response header docs: ETag remains an HTTP response header owned by the gateway. https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.headers.responseheaders?view=aspnetcore-10.0
- Dapr .NET client service invocation docs: current `DaprClient.CreateInvokeMethodRequest` plus `HttpClient` service-invocation pattern remains valid. https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-client/
- Dapr service invocation docs: invoked services are addressed by application ID and method path; keep `/query` as the domain-service method. https://docs.dapr.io/developing-applications/building-blocks/service-invocation/howto-invoke-discover-services/

### Previous Story Intelligence

Story 1.1 (`_bmad-output/implementation-artifacts/spec-1-1-canonical-domain-service-sdk-host.md`) completed the canonical DomainService SDK host contract and review patches. Carry forward:
- Keep `AddEventStoreDomainService()` / `UseEventStoreDomainService()` as the canonical host shape.
- Preserve SDK route-table tests and method-aware `/project` preservation behavior.
- Do not reintroduce Sample DAPR or host boilerplate.
- Continue using guardrail tests for domain-module authoring instead of broad implementation rewrites.

Recent commits:
- `385954ee feat(domain-service): add canonical SDK host` introduced the 1.1 host hardening and tests.
- `a897fb21 feat: Implement Correct-Course Story Rewrite Gate and Domain-Owned Contracts Library Guidance` is the process change that makes this story rewrite mandatory.
- `b1f09312 Update submodule references and add sprint change proposal for query metadata sequencing` is the source of this story's expanded metadata scope.

## Project Structure Notes

This is platform code in EventStore-owned projects. Do not edit `references/Hexalith.Tenants` or other submodules in this story. Do not create new `*.AppHost`, `*.Aspire`, or `*.ServiceDefaults` projects. Do not move generated REST metadata work from Story 2.2 into this story; if docs mention generated headers, keep it to the platform metadata contract this story makes available.

No UI implementation is in scope. The UX impact is indirect: downstream UIs must treat missing freshness as unknown and must not display cursor/ETag internals. This story supplies the metadata path those UIs will consume later.

## Testing Requirements

Recommended validation commands:

```bash
dotnet build Hexalith.EventStore.slnx --configuration Release
dotnet test tests/Hexalith.EventStore.Contracts.Tests/
dotnet test tests/Hexalith.EventStore.Client.Tests/
dotnet test tests/Hexalith.EventStore.DomainService.Tests/
dotnet test tests/Hexalith.EventStore.QueryRouting.Tests/
dotnet test tests/Hexalith.EventStore.Server.Tests/
```

`tests/Hexalith.EventStore.Server.Tests/` is known to have a pre-existing CA2007 warnings-as-errors build issue in the baseline. If it still blocks execution, document the exact failure and run all other focused test projects above. Do not use solution-level `dotnet test`.

## References

- `_bmad-output/planning-artifacts/epics.md` - Epic 1, Story 1.2 active acceptance criteria.
- `_bmad-output/planning-artifacts/prd.md` - FR4 domain query-handler seam and metadata propagation.
- `_bmad-output/planning-artifacts/architecture.md` - AD-14 query evidence metadata flow and merge rules.
- `_bmad-output/planning-artifacts/ux.md` - projection-confirmed/support-safe downstream state.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-query-metadata-propagation.md`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-query-metadata-sequencing.md`
- `_bmad-output/implementation-artifacts/spec-1-1-canonical-domain-service-sdk-host.md`
- `docs/reference/query-api.md`
- `docs/operations/query-operational-evidence.md`
- `src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs`
- `src/Hexalith.EventStore.Contracts/Queries/QueryResult.cs`
- `src/Hexalith.EventStore/Controllers/QueriesController.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`

## Open Questions

None blocking. The implementation can choose whether explicit freshness policy is enforced after query execution when authoritative metadata exists or rejected before execution until enough metadata producers exist. In either case, the result must fail closed with `query_projection_stale` and must not represent unknown freshness as current.

## Dev Agent Record

### Agent Model Used

TBD by dev agent.

### Debug Log References

### Completion Notes List

### File List

## Completion Note

Ultimate context engine analysis completed - comprehensive developer guide created.
