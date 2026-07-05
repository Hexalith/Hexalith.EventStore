---
title: Sprint Change Proposal - Query Metadata Propagation Contract
date: 2026-07-05
project: eventstore
status: approved-for-implementation
source:
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture.md
  - docs/reference/query-api.md
  - docs/operations/query-operational-evidence.md
  - src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs
  - src/Hexalith.EventStore.Contracts/Queries/QueryResult.cs
  - src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs
  - src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs
  - src/Hexalith.EventStore/Controllers/QueriesController.cs
  - src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs
  - src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs
---

# Sprint Change Proposal: Query Metadata Propagation Contract

## 1. Issue Summary

The Epic D retrospective left an open architecture action item:

> Specify query metadata propagation through the gateway for freshness, projection version, ETag, and paging evidence.

The trigger is not a new product feature. It is a planning and architecture gap found after the generated REST and Tenants API proof work: query metadata exists partially in contracts and generated API tests, but the platform does not yet define or preserve an authoritative end-to-end metadata path from domain query handlers/projection actors through the gateway and generated external APIs.

Evidence:

- `_bmad-output/implementation-artifacts/deferred-work.md` records that generated query freshness/projection metadata remains conditional evidence only until the platform-owned gateway contract is specified.
- `epics.md` Story 7.5 states downstream stories depending on stale/current state or projection version must first specify the platform-owned query metadata contract for freshness, projection version, ETag, paging, and related evidence through the gateway.
- `architecture.md` AD-14 says query/read-model evidence metadata must cross the gateway as platform metadata, but it does not define the exact propagation contract or merge rules.
- `src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs` already defines `ETag`, `IsNotModified`, `IsStale`, `IsDegraded`, `ProjectionVersion`, `ServedAt`, `Paging`, and `WarningCodes`.
- `src/Hexalith.EventStore.Contracts/Queries/QueryResult.cs`, `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs`, and `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs` do not carry `QueryResponseMetadata`, so domain-produced freshness metadata is dropped on the real gateway path.
- `src/Hexalith.EventStore/Controllers/QueriesController.cs` currently fabricates gateway metadata from the ETag actor, current time, and request paging. It does not preserve domain handler metadata.
- `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` forwards `ETag`, `X-Hexalith-Projection-Version`, `X-Hexalith-Served-At`, and `X-Hexalith-Is-Stale` when the gateway client result contains metadata, but current production gateway flow cannot reliably supply the real domain metadata.

## 2. Impact Analysis

### Epic Impact

**Epic 1 - Domain Author Self-Service Platform:** impacted. Stories 1.2 and 1.3 need acceptance criteria that the domain query handler/read-model/cursor seams return metadata through platform contracts, not derived types or ad hoc payload fields.

**Epic 2 - External Integration Surfaces:** impacted. Stories 2.2, 2.3, and 2.4 need generated REST and Sample/Tenants proof criteria that headers and client metadata are backed by the real gateway path.

**Epic 6 - Bounded Cost And Event Evolution:** lightly impacted. Projection-version semantics should align with projection sequence guards and cost-reduction work, but this proposal does not implement those guards.

**Epic 7 - Operator Trust, Admin Honesty, And Future Capabilities:** impacted. Story 7.5 currently tracks the gap as a backlog condition. It should be split into a dedicated story for the query metadata propagation contract and implementation.

No epic becomes obsolete. Epic order does not need a major reset, but the query metadata story must be completed before UI or generated REST stories claim stale/current state, projection version, or paging evidence as production-backed.

### Story Impact

Affected stories:

- Story 1.2: Domain Query Handler Routing
- Story 1.3: Generic Read Models And Query Cursors
- Story 2.2: REST API Generator Discovery And Controller Emission
- Story 2.3: Sample External API Host Proof
- Story 2.4: Tenants External API Host Adoption
- Story 6.3/6.4: Projection Delivery Cost And Sequence Guard Spec/Implementation
- Story 7.5: Track Future Capability Backlog

Recommended new implementation story:

- Story 7.6: Query Metadata Propagation Contract And Gateway Evidence

### Artifact Conflicts

**PRD:** no MVP scope reduction is needed. FR4, FR5, FR6, FR12, FR15, and FR34 need clarification that the metadata contract is first-class and platform-owned.

**Architecture:** AD-14 needs a concrete flow and merge policy. The current invariant is directionally correct but too broad for implementation.

**UX:** no separate UX document was discovered. UI-facing implications remain limited to existing support-safe and projection-confirmed rules: UI must not display raw ETag/cursor internals and must not treat missing freshness metadata as current.

**Docs:** `docs/reference/query-api.md` currently says rich freshness/projection evidence is not guaranteed end to end. It should be updated after implementation to describe the now-supported contract and header/body metadata behavior.

**Sprint tracking:** `sprint-status.yaml` has the action item open. It should remain open until the proposal is approved and the dedicated story artifact exists.

### Technical Impact

Likely implementation touches:

- `src/Hexalith.EventStore.Contracts/Queries/QueryResult.cs`
- `src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs`
- `src/Hexalith.EventStore.Contracts/Queries/QueryPagingMetadata.cs`
- `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs`
- `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs`
- `src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs`
- `src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs`
- `src/Hexalith.EventStore/Queries/DaprDomainQueryInvoker.cs`
- `src/Hexalith.EventStore/Controllers/QueriesController.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs`
- focused tests in Contracts, Client, Server, REST generator, Sample, and Tenants proof lanes

Submodule code under `references/Hexalith.Tenants` should not be edited without explicit approval. The platform story can define the contract and EventStore behavior first; Tenants adoption can follow as a separate, scoped change if needed.

## 3. Recommended Approach

Recommended path: **Direct Adjustment with backlog split**.

Create a dedicated story for query metadata propagation and tighten PRD, architecture, epics, and query API docs around the authoritative contract. This is moderate scope because the change crosses Contracts, Server, gateway host, Client, generated REST output, and proof tests. It does not require rollback and does not require MVP reduction.

Rollback is not useful because the current partial metadata code is additive and can be completed without reverting Epic D proof work. MVP review is not required because this closes an existing readiness/action-item gap inside current scope.

Effort estimate: **Medium**.

Risk level: **Medium**. The primary risk is accidental wire-contract breakage in `QueryResult` or generated REST responses. Mitigate by using additive record members, compatibility tests, and explicit 304/unknown-freshness rules.

Sequencing:

1. Approve this proposal.
2. Create Story 7.6 as ready-for-dev or backlog, depending on sprint capacity.
3. Update `epics.md`, `architecture.md`, and `prd.md` with the accepted text below.
4. Implement the platform path and focused tests.
5. Update `docs/reference/query-api.md` once behavior is real.
6. Mark the Epic D action item done only after the story artifact and planning updates exist.

## 4. Detailed Change Proposals

### 4.1 New Story

Story: Story 7.6: Query Metadata Propagation Contract And Gateway Evidence
Section: New story under Epic 7

OLD:

No dedicated implementation story exists. Story 7.5 includes only this acceptance criterion:

```markdown
**Given** generated query freshness metadata remains partial
**When** downstream stories depend on stale/current state or projection version
**Then** they must first specify the platform-owned query metadata contract that carries freshness, projection version, ETag, paging, and related evidence through the gateway
**And** UI or generated REST acceptance criteria must not rely on ad hoc payload fields for projection-confirmed state.
```

NEW:

```markdown
### Story 7.6: Query Metadata Propagation Contract And Gateway Evidence

**Requirements covered:** FR4, FR5, FR6, FR12, FR15, FR34, NFR8, NFR12, NFR14, NFR15, NFR16

As a platform maintainer,
I want query metadata to propagate through platform result and HTTP contracts,
So that generated APIs, UI hosts, and operators can distinguish freshness, projection version, cache validation, and paging evidence without ad hoc payload fields.

**Acceptance Criteria:**

**Given** a domain query handler or projection actor returns query metadata
**When** the result crosses `QueryResult`, `QueryRouterResult`, `SubmitQueryResult`, `SubmitQueryResponse`, and `EventStoreQueryResult`
**Then** `QueryResponseMetadata` is preserved additively through each platform type
**And** the gateway no longer drops domain-produced freshness, projection version, paging, warning, or degraded-state metadata.

**Given** the gateway creates HTTP response metadata
**When** domain metadata and gateway metadata both exist
**Then** metadata is merged by explicit rules: domain/projection evidence wins for freshness, projection version, paging, degraded state, and warnings; gateway ETag header value wins for the HTTP validator; gateway fills `ServedAt` only when absent; `IsNotModified` is set by the HTTP outcome.

**Given** freshness metadata is unavailable
**When** a query response is returned
**Then** the platform represents freshness as unknown, not current
**And** UI/generated REST callers must not treat missing `IsStale` as projection-confirmed freshness.

**Given** projection version metadata is unavailable
**When** an ETag exists
**Then** the platform may expose the ETag as cache metadata only
**And** it must not label the ETag as `ProjectionVersion` unless the metadata producer explicitly supplies that value or the story documents that equivalence for the projection type.

**Given** a query returns paged data
**When** the handler can authoritatively describe the page
**Then** `QueryPagingMetadata` includes effective page size, offset or next cursor, total count when known, and whether another page exists
**And** cursors remain opaque and are not parsed, logged, displayed as support text, or treated as ordering proof.

**Given** a generated external API action receives `EventStoreQueryResult.Metadata`
**When** it returns `200` or `304`
**Then** it forwards canonical support-safe headers for ETag, projection version, served-at, stale state, degraded state, warning codes, and bounded paging evidence
**And** no generated controller relies on payload-specific fields to decide projection-confirmed state.

**Given** query freshness policy requests `RequireFresh` or `MaxStaleness`
**When** authoritative freshness metadata is present
**Then** the gateway can enforce the policy consistently
**And** when freshness is unknown it fails closed according to the existing `query_projection_stale` taxonomy rather than silently treating the response as current.

**Given** the implementation is validated
**When** focused tests run
**Then** tests prove real domain-handler metadata reaches the gateway response, generated external API headers, `304` behavior, client typed results, invalid cursor/problem details, and paging evidence
**And** higher-tier proof uses persisted read-model/end-state evidence where applicable, not only mocked gateway metadata.
```

Rationale: isolates the action item into one implementable story and prevents UI/generated REST proof stories from claiming metadata support before the gateway path preserves it.

### 4.2 PRD Updates

Artifact: `_bmad-output/planning-artifacts/prd.md`

Section: 6.1 Domain Author Self-Service Platform, FR4

OLD:

```markdown
FR4 | The platform must provide a domain query-handler seam using `IDomainQueryHandler`, discovery, dispatch, operational metadata reporting, gateway-side query-type capture, and handler-aware routing to domain `/query` endpoints.
```

NEW:

```markdown
FR4 | The platform must provide a domain query-handler seam using `IDomainQueryHandler`, discovery, dispatch, operational metadata reporting, gateway-side query-type capture, handler-aware routing to domain `/query` endpoints, and end-to-end `QueryResponseMetadata` propagation for freshness, projection version, ETag, served-at, degraded/warning state, and paging evidence.
```

Section: 6.2 External Integration Surfaces, FR12

OLD:

```markdown
FR12 | The REST API generator must discover command/query contracts and emit typed, OpenAPI-visible controllers that delegate to `IEventStoreGatewayClient`, with tests covering discovery, routing conventions, diagnostics, and generated output.
```

NEW:

```markdown
FR12 | The REST API generator must discover command/query contracts and emit typed, OpenAPI-visible controllers that delegate to `IEventStoreGatewayClient`, forward canonical query metadata headers when supplied by the gateway, and include tests covering discovery, routing conventions, diagnostics, generated output, query metadata headers, `304`, and safe problem-detail behavior.
```

Section: 6.2 External Integration Surfaces, FR15

OLD:

```markdown
FR15 | The Tenants proof must move generated Tenants controllers to an external Tenants API host, while Tenants UI consumes client libraries and no longer hosts hand-written per-message controllers.
```

NEW:

```markdown
FR15 | The Tenants proof must move generated Tenants controllers to an external Tenants API host, while Tenants UI consumes client libraries and no longer hosts hand-written per-message controllers; any Tenants freshness, projection-version, ETag, or paging evidence shown by generated APIs or UI must come from the platform query metadata path.
```

Section: 7 Cross-Cutting Non-Functional Requirements, NFR8

OLD:

```markdown
NFR8 | Snapshot and projection behavior must have a bounded cost model as streams grow and must avoid unnecessary full-stream replay when already current.
```

NEW:

```markdown
NFR8 | Snapshot and projection behavior must have a bounded cost model as streams grow, must avoid unnecessary full-stream replay when already current, and must expose projection freshness/version evidence through platform query metadata when callers depend on current/stale decisions.
```

Rationale: clarifies existing scope without creating a new FR/NFR or reducing MVP.

### 4.3 Architecture Updates

Artifact: `_bmad-output/planning-artifacts/architecture.md`

Section: AD-14 - Query Evidence Crosses The Gateway As Platform Metadata

OLD:

```markdown
Query/read-model evidence metadata is carried through platform result and HTTP-header contracts owned by the gateway, not ad hoc payload fields. Domain handlers and projection actors may produce freshness, projection version, paging, ETag, and stale/current/unknown state only through those contracts; UI hosts render confirmation and stale states from that evidence.
```

NEW:

```markdown
Query/read-model evidence metadata is carried through `QueryResponseMetadata` and HTTP response headers owned by the gateway, not ad hoc payload fields. The canonical flow is:

Domain/projection query result -> `QueryResult.Metadata` -> `QueryRouterResult.Metadata` -> `SubmitQueryResult.Metadata` -> `SubmitQueryResponse.Metadata` -> `EventStoreQueryResult.Metadata` -> generated external API headers or UI client state.

Merge rules are explicit:

- Domain/projection metadata is authoritative for freshness, projection version, paging, degraded state, and warning codes.
- The gateway is authoritative for the HTTP ETag header and may fill `QueryResponseMetadata.ETag` from the selected strong validator when the producer omitted it.
- The gateway fills `ServedAt` only when absent.
- `IsNotModified` is derived from the HTTP outcome.
- Missing freshness is unknown, not current.
- ETag and projection version are distinct unless a projection explicitly defines them as equivalent.
- Paging metadata is evidence only when produced by the query handler/projection; request paging echoed by the gateway is not proof of total count, next cursor, or page completeness.

Generated REST controllers may forward metadata through support-safe headers such as `ETag`, `X-Hexalith-Projection-Version`, `X-Hexalith-Served-At`, `X-Hexalith-Is-Stale`, `X-Hexalith-Is-Degraded`, `X-Hexalith-Warning-Codes`, `X-Hexalith-Page-Size`, `X-Hexalith-Page-Offset`, `X-Hexalith-Next-Cursor`, `X-Hexalith-Total-Count`, and `X-Hexalith-Has-More` only when those metadata values are present and bounded. Cursors and ETags remain opaque and must not be parsed, displayed as support text, or logged as diagnostic detail.
```

Rationale: turns AD-14 from an invariant into an implementable contract with merge rules and header semantics.

### 4.4 Epic Updates

Artifact: `_bmad-output/planning-artifacts/epics.md`

Story: 1.2 Domain Query Handler Routing

OLD:

```markdown
**Given** query routing is tested
**When** focused unit tests execute
**Then** domain-side dispatch, metadata capture, handler-aware routing, and fallback behavior are verified
**And** the implementation remains backward compatible with projection-actor query routing.
```

NEW:

```markdown
**Given** query routing is tested
**When** focused unit tests execute
**Then** domain-side dispatch, operational metadata capture, handler-aware routing, fallback behavior, and `QueryResponseMetadata` propagation are verified
**And** the implementation remains backward compatible with projection-actor query routing.
```

Story: 1.3 Generic Read Models And Query Cursors

OLD:

```markdown
**Given** a domain query returns paged results
**When** it creates a cursor with `IQueryCursorCodec` and `QueryCursorScope`
**Then** the cursor is protected with a caller-supplied Data Protection purpose
**And** decoding fails safely for wrong scope, wrong query type, malformed payload, tampering, oversize payload, or key rotation.
```

NEW:

```markdown
**Given** a domain query returns paged results
**When** it creates a cursor with `IQueryCursorCodec` and `QueryCursorScope`
**Then** the cursor is protected with a caller-supplied Data Protection purpose
**And** decoding fails safely for wrong scope, wrong query type, malformed payload, tampering, oversize payload, or key rotation
**And** successful paged responses can return `QueryPagingMetadata` with effective page size, offset or next cursor, total count when known, and has-more evidence without exposing cursor internals.
```

Story: 2.2 REST API Generator Discovery And Controller Emission

OLD:

```markdown
**Given** generated query actions execute
**When** a request reaches the generated controller
**Then** the controller delegates to `IEventStoreGatewayClient.SubmitQueryAsync`
**And** it maps success, `304`, ETag, not-found, forbidden, and validation outcomes consistently with gateway query semantics.
```

NEW:

```markdown
**Given** generated query actions execute
**When** a request reaches the generated controller
**Then** the controller delegates to `IEventStoreGatewayClient.SubmitQueryAsync`
**And** it maps success, `304`, ETag, freshness, projection version, served-at, degraded/warning state, paging metadata, not-found, forbidden, and validation outcomes consistently with gateway query semantics.
```

Story: 2.3 Sample External API Host Proof

OLD:

```markdown
**Then** generated query and command endpoints compile and behave as expected
**And** any smoke tests verify ETag/`304` query behavior and accepted command behavior through the external API host.
```

NEW:

```markdown
**Then** generated query and command endpoints compile and behave as expected
**And** any smoke tests verify ETag/`304` query behavior, metadata header behavior when available, and accepted command behavior through the external API host.
```

Story: 2.4 Tenants External API Host Adoption

OLD:

```markdown
**Then** the generated REST surface preserves existing external behavior
**And** any CI-gated DAPR/Aspire blockers are documented with exact commands and failure reasons.
```

NEW:

```markdown
**Then** the generated REST surface preserves existing external behavior
**And** freshness, projection-version, ETag, and paging evidence is backed by the real platform query metadata path rather than only mocked gateway-client metadata
**And** any CI-gated DAPR/Aspire blockers are documented with exact commands and failure reasons.
```

Story: 7.5 Track Future Capability Backlog

OLD:

```markdown
**Given** generated query freshness metadata remains partial
**When** downstream stories depend on stale/current state or projection version
**Then** they must first specify the platform-owned query metadata contract that carries freshness, projection version, ETag, paging, and related evidence through the gateway
**And** UI or generated REST acceptance criteria must not rely on ad hoc payload fields for projection-confirmed state.
```

NEW:

```markdown
**Given** generated query freshness metadata remains partial
**When** downstream stories depend on stale/current state, projection version, ETag, or paging evidence
**Then** Story 7.6 must be created or scheduled to define and implement the platform-owned query metadata propagation contract
**And** UI or generated REST acceptance criteria must not rely on ad hoc payload fields for projection-confirmed state.
```

Rationale: keeps the original guardrail but converts it into a concrete backlog split.

### 4.5 Query API Documentation Update

Artifact: `docs/reference/query-api.md`

Section: Projection Evidence Metadata

OLD:

```markdown
ETag metadata is available through the gateway response headers. Rich freshness and projection evidence, such as stale/current state and projection version, is not yet guaranteed end to end through the public gateway contract. Domain handlers may know those values, but clients and generated REST controllers must not treat stale indicators or projection-version headers as production-backed until the platform query result/header contract carries that metadata across the gateway.
```

NEW, after implementation:

```markdown
Query evidence metadata is available through the gateway `metadata` object and selected response headers. The platform preserves `QueryResponseMetadata` from domain query handlers and projection actors through the gateway and client libraries.

Authoritative metadata fields:

- `eTag`: normalized strong cache validator. Also emitted as the HTTP `ETag` header when available.
- `isNotModified`: true only for HTTP `304` outcomes surfaced by the client.
- `isStale`: true for stale, false for authoritatively current, absent/null for unknown freshness.
- `projectionVersion`: opaque projection/read-model version when the metadata producer supplies it.
- `servedAt`: UTC timestamp for when the response was served.
- `paging`: effective page size, offset or next cursor, total count when known, and has-more evidence.
- `warningCodes`: stable warning codes, such as degraded query paths.

ETag and projection version are separate values. Clients must not infer projection version from ETag unless the projection contract explicitly documents that equivalence. Missing freshness metadata means unknown, not current.
```

Rationale: docs should remain honest until the code path is implemented, then document supported behavior.

## 5. Implementation Handoff

Change scope: **Moderate**.

Route to: Product Owner / Developer agents, with architect review before implementation starts.

Approval: Approved by Administrator on 2026-07-05.

Responsibilities:

- **Architect/Product Owner:** approve this proposal, add Story 7.6, update PRD/architecture/epics text, and decide whether the story is ready-for-dev or backlog.
- **Developer:** implement additive metadata propagation through contracts, routing, gateway, client, generated REST headers, and tests.
- **Test Architect:** add or review tests for real metadata propagation, `304`, freshness unknown/current/stale, projection version, invalid cursor/problem details, paging evidence, and state/read-model evidence where integration-tier validation applies.
- **Technical Writer:** update `docs/reference/query-api.md` after implementation, not before.

Success criteria:

- Planning artifacts define the authoritative query metadata contract and merge rules.
- Story 7.6 exists with concrete acceptance criteria and validation commands.
- Real domain/projection metadata reaches `SubmitQueryResponse.Metadata`, `EventStoreQueryResult.Metadata`, and generated external API headers.
- ETag and projection version are not conflated accidentally.
- Missing freshness is treated as unknown.
- Paging evidence is platform metadata, bounded, and cursor-safe.
- The Epic D sprint-status action item is marked done only after the proposal is approved and Story 7.6 is created.

## 6. Checklist Results

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story identified | Done | Trigger comes from Epic D retrospective action item and Story 7.5 final AC. |
| 1.2 Core problem defined | Done | Existing metadata contracts/tests are partial; real gateway path drops domain metadata. |
| 1.3 Evidence gathered | Done | PRD, epics, architecture, deferred-work, query API docs, and query/gateway code inspected. |
| 2.1 Current epic impact | Done | Epic 7 backlog tracking is directly affected; Epics 1 and 2 implementation stories also need tighter ACs. |
| 2.2 Epic-level changes | Done | No new epic required; add Story 7.6 and update existing story ACs. |
| 2.3 Remaining epics reviewed | Done | Epic 6 has light sequencing impact for projection version/sequence semantics. |
| 2.4 Invalidates future epics | N/A | No planned epic is obsolete. |
| 2.5 Priority/order | Done | Story 7.6 must precede claims of production-backed stale/current/projection-version evidence. |
| 3.1 PRD conflicts | Done | PRD needs clarification only; no MVP reduction. |
| 3.2 Architecture conflicts | Done | AD-14 exists but needs concrete contract and merge rules. |
| 3.3 UX conflicts | N/A | No UI artifact found; UI rules remain support-safe and projection-confirmed. |
| 3.4 Other artifacts | Done | Query API docs and sprint-status action item are affected. |
| 4.1 Direct adjustment | Viable | Chosen. Medium effort, medium risk. |
| 4.2 Rollback | Not viable | Existing partial metadata support is additive and should be completed. |
| 4.3 MVP review | Not viable | Scope is already inside Phase 4 readiness/backlog tracking. |
| 4.4 Path selected | Done | Direct adjustment with backlog split. |
| 5.1 Issue summary | Done | See section 1. |
| 5.2 Impact/artifacts | Done | See section 2. |
| 5.3 Path rationale | Done | See section 3. |
| 5.4 MVP impact/action plan | Done | No MVP reduction; action plan defined. |
| 5.5 Handoff plan | Done | PO/Developer/Test Architect/Technical Writer responsibilities listed. |
| 6.1 Checklist completion | Done | All applicable analysis items addressed. |
| 6.2 Proposal accuracy | Done | Proposal reflects current partial metadata code and planning artifacts. |
| 6.3 User approval | Done | Approved by Administrator on 2026-07-05. |
| 6.4 Sprint status update | Done | Story 7.6 was created and the Epic D action item was marked done after the planning updates landed. |
| 6.5 Next steps | Done | Approve, revise, or reject this proposal. |
