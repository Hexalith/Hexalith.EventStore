---
baseline_commit: 3ad3500eaf0db3d1abb8507763cd10e641ef60bc
created: 2026-07-05
source_story_key: 7-6-query-metadata-propagation
source_epic: "Epic 7 - Operator Trust, Admin Honesty, And Future Capabilities"
source_action: "Epic D retrospective action item 2"
source_files:
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-query-metadata-propagation.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/epics.md
---

# Story 7.6: Query Metadata Propagation Contract And Gateway Evidence

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **Hexalith.EventStore platform maintainer**,
I want **query metadata to propagate through platform result and HTTP contracts**,
so that **generated APIs, UI hosts, and operators can distinguish freshness, projection version, cache validation, and paging evidence without ad hoc payload fields**.

## Story Context

This story is the implementation handoff for the Epic D retrospective action item:

> Specify query metadata propagation through the gateway for freshness, projection version, ETag, and paging evidence.

The planning contract is now recorded in:

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-query-metadata-propagation.md`
- `_bmad-output/planning-artifacts/prd.md` FR4, FR12, FR15, NFR8
- `_bmad-output/planning-artifacts/architecture.md` AD-14
- `_bmad-output/planning-artifacts/epics.md` Story 7.6

This story implements the platform path. It is not a UI story and does not move generated controllers into interactive UI hosts. Generated REST controllers remain dedicated external API host artifacts and continue to delegate through `IEventStoreGatewayClient`.

## Acceptance Criteria

1. **Platform result types preserve metadata additively.**
   - Given a domain query handler or projection actor returns query metadata
   - When the result crosses `QueryResult`, `QueryRouterResult`, `SubmitQueryResult`, `SubmitQueryResponse`, and `EventStoreQueryResult`
   - Then `QueryResponseMetadata` is preserved additively through each platform type
   - And the gateway no longer drops domain-produced freshness, projection version, paging, warning, or degraded-state metadata.

2. **Gateway metadata merge rules are explicit and tested.**
   - Given the gateway creates HTTP response metadata
   - When domain metadata and gateway metadata both exist
   - Then metadata is merged by explicit rules: domain/projection evidence wins for freshness, projection version, paging, degraded state, and warnings; gateway ETag header value wins for the HTTP validator; gateway fills `ServedAt` only when absent; `IsNotModified` is set by the HTTP outcome.

3. **Unknown freshness is not treated as current.**
   - Given freshness metadata is unavailable
   - When a query response is returned
   - Then the platform represents freshness as unknown, not current
   - And UI/generated REST callers must not treat missing `IsStale` as projection-confirmed freshness.

4. **Projection version and ETag are not conflated.**
   - Given projection version metadata is unavailable
   - When an ETag exists
   - Then the platform may expose the ETag as cache metadata only
   - And it must not label the ETag as `ProjectionVersion` unless the metadata producer explicitly supplies that value or the story documents that equivalence for the projection type.

5. **Paging evidence is authoritative and cursor-safe.**
   - Given a query returns paged data
   - When the handler can authoritatively describe the page
   - Then `QueryPagingMetadata` includes effective page size, offset or next cursor, total count when known, and whether another page exists
   - And cursors remain opaque and are not parsed, logged, displayed as support text, or treated as ordering proof.

6. **Generated external APIs forward support-safe metadata headers.**
   - Given a generated external API action receives `EventStoreQueryResult.Metadata`
   - When it returns `200` or `304`
   - Then it forwards canonical support-safe headers for ETag, projection version, served-at, stale state, degraded state, warning codes, and bounded paging evidence
   - And no generated controller relies on payload-specific fields to decide projection-confirmed state.

7. **Freshness policy fails closed when freshness is unknown.**
   - Given query freshness policy requests `RequireFresh` or `MaxStaleness`
   - When authoritative freshness metadata is present
   - Then the gateway can enforce the policy consistently
   - And when freshness is unknown it fails closed according to the existing `query_projection_stale` taxonomy rather than silently treating the response as current.

8. **Verification proves real metadata propagation.**
   - Given the implementation is validated
   - When focused tests run
   - Then tests prove real domain-handler metadata reaches the gateway response, generated external API headers, `304` behavior, client typed results, invalid cursor/problem details, and paging evidence
   - And higher-tier proof uses persisted read-model/end-state evidence where applicable, not only mocked gateway metadata.

## Tasks / Subtasks

- [ ] **Task 1: Preflight and contract inventory** (AC: 1-8)
  - [ ] Read this story and the sprint change proposal before editing code.
  - [ ] Inspect the existing query metadata contracts: `QueryResponseMetadata`, `QueryPagingMetadata`, and `QueryResult`.
  - [ ] Inspect gateway, server, client, and generator paths before changing signatures.
  - [ ] Confirm no generated controller is moved into an interactive UI host.

- [ ] **Task 2: Additive contracts and server result propagation** (AC: 1, 2)
  - [ ] Add or expose `QueryResponseMetadata` on every platform result hop needed by the query pipeline.
  - [ ] Preserve binary/source compatibility where feasible by using additive members and overloads.
  - [ ] Ensure handler-served query metadata and projection-actor metadata both reach the gateway.
  - [ ] Add focused unit tests for each hop that currently drops metadata.

- [ ] **Task 3: Gateway HTTP metadata merge and freshness policy** (AC: 2, 3, 4, 7)
  - [ ] Implement the AD-14 merge rules in the gateway response path.
  - [ ] Preserve strong ETag validator behavior and `304` semantics.
  - [ ] Keep missing freshness as unknown, not current.
  - [ ] Enforce `RequireFresh` and `MaxStaleness` only from authoritative freshness metadata and fail closed when freshness is unknown.

- [ ] **Task 4: Paging evidence and cursor safety** (AC: 5)
  - [ ] Preserve producer-authored paging metadata through the pipeline.
  - [ ] Avoid treating request paging echo as proof of total count, next cursor, or page completeness.
  - [ ] Ensure cursors and ETags remain opaque in logs, headers, problem details, and UI-facing metadata.

- [ ] **Task 5: Client and generated REST header behavior** (AC: 6, 8)
  - [ ] Preserve metadata in `IEventStoreGatewayClient` typed query results.
  - [ ] Forward support-safe generated API headers only when metadata values are present and bounded.
  - [ ] Cover `200`, `304`, freshness, projection version, ETag, warning/degraded state, and paging headers.
  - [ ] Do not decide projection-confirmed state from payload-specific fields.

- [ ] **Task 6: Documentation after implementation** (AC: 8)
  - [ ] Update `docs/reference/query-api.md` only after the behavior is implemented and tested.
  - [ ] Document ETag/projection-version separation, unknown freshness, paging evidence, and supported headers.
  - [ ] Keep support-safe guidance for cursors, ETags, metadata, and problem details.

- [ ] **Task 7: Verify and record evidence** (AC: 8)
  - [ ] Run focused Contracts tests.
  - [ ] Run focused Client tests.
  - [ ] Run focused Server/gateway query tests.
  - [ ] Run focused REST generator tests if generated header behavior changes.
  - [ ] Run Release build with package-reference mode.
  - [ ] Record commands, results, and blockers in the Dev Agent Record.

## Dev Notes

### Top Guardrails

1. **No payload-field metadata contract.** Freshness, projection version, ETag, degraded/warning state, and paging evidence cross platform metadata contracts, not domain-specific payload fields.
2. **No ETag/projection-version conflation.** ETag is a cache validator. Projection version is only present when a metadata producer supplies it or a projection contract explicitly documents equivalence.
3. **Unknown freshness is not current.** Missing `IsStale` means unknown. UI and generated REST callers cannot claim projection-confirmed freshness from absence.
4. **Cursors and ETags remain opaque.** Do not parse, log, display, or expose internals as support text.
5. **No UI-host controller regression.** Generated REST stays in dedicated external API hosts and calls `IEventStoreGatewayClient`.
6. **No submodule edits without approval.** Tenants proof changes under `references/Hexalith.Tenants` need explicit scope approval.

### Likely Code Areas

| Area | Candidate files |
| --- | --- |
| Contracts | `src/Hexalith.EventStore.Contracts/Queries/QueryResult.cs`, `QueryResponseMetadata.cs`, `QueryPagingMetadata.cs` |
| Server query routing | `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs`, `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs`, `SubmitQueryHandler.cs` |
| Gateway host | `src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs`, `DaprDomainQueryInvoker.cs`, `Controllers/QueriesController.cs` |
| Client | `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs` and result DTOs |
| REST generator | `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` |
| Tests | Contracts, Client, Server/gateway query tests, REST generator tests, Sample/Tenants proof tests where in scope |

### Verification Commands

Use exact projects as needed; do not run solution-level `dotnet test`.

```bash
dotnet test tests/Hexalith.EventStore.Contracts.Tests/
dotnet test tests/Hexalith.EventStore.Client.Tests/
dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/
dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false
```

If server/gateway tests are added or touched, run the focused project or test filter that owns them. `Hexalith.EventStore.Server.Tests` has known CA2007 baseline risk; record exact blockers rather than broadening the baseline without intent.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-query-metadata-propagation.md`] - approved change proposal and before/after artifact edits.
- [Source: `_bmad-output/planning-artifacts/architecture.md` AD-14] - canonical metadata flow and merge rules.
- [Source: `_bmad-output/planning-artifacts/prd.md` FR4, FR12, FR15, NFR8] - product requirements updated for metadata propagation.
- [Source: `_bmad-output/planning-artifacts/epics.md` Story 7.6] - story-level acceptance criteria.
- [Source: `docs/reference/query-api.md`] - update only after implementation proves the contract.

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

TBD

### Completion Notes List

TBD

### File List

TBD

## Change Log

| Date | Change |
|---|---|
| 2026-07-05 | Created implementation-ready story for query metadata propagation through gateway and generated API headers. Status ready-for-dev. |
