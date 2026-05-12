# Sprint Change Proposal: EventStore Parties Integration Contract Gaps

Date: 2026-05-12
Project: Hexalith.EventStore
Trigger: Parties integration is duplicating or assuming EventStore gateway, query, tenant/RBAC, replay, publishing, and payload-protection semantics that are not yet first-class EventStore contracts.
Mode: Batch
Prepared by: Codex (Developer)

## 1. Issue Summary

Parties is exposing a real platform contract gap: EventStore is implemented enough for its own sample/admin flows, but several integration guarantees that downstream bounded contexts need are either located in the service assembly, only documented informally, or not frozen as stable public contracts.

Concrete examples:

- `SubmitCommandRequest` and `SubmitCommandResponse` live in `src/Hexalith.EventStore/Models`, so Parties must duplicate the command gateway DTO instead of referencing `Hexalith.EventStore.Contracts` or `Hexalith.EventStore.Client`.
- `SubmitQueryRequest` and `SubmitQueryResponse` already live in `Contracts`, but the high-level client API, paging/cache behavior, and query policy taxonomy are not complete enough for Parties to treat `POST /api/v1/queries` as a stable gateway.
- `IProjectionActor`, `QueryEnvelope`, and `QueryResult` live in `Hexalith.EventStore.Server`, so domain projections that serve queries through EventStore cannot depend on a public projection adapter contract.
- `ITenantValidator` and `IRbacValidator` exist in the EventStore host boundary, but the gateway contract with `Hexalith.Tenants` is not fully frozen as fail-closed lifecycle/membership/role enforcement with stable 401/403 reason codes.
- Replay, publishing, payload protection, and GDPR crypto-shredding have pieces in place, but not enough public contract and operational policy for Parties to rely on them.

This is a significant integration-contract correction, not a local bug fix.

## 2. Checklist Findings

| Item | Status | Finding |
| ---- | ------ | ------- |
| 1.1 Triggering issue | Done | Parties integration needs EventStore-owned gateway/query contracts and runtime guarantees instead of duplicated DTOs and local authorization behavior. |
| 1.2 Core problem | Done | Requirement gap plus public-surface placement issue. Current implementation has useful pieces, but some are in service/server assemblies or under-specified for downstream services. |
| 1.3 Evidence | Done | `SubmitCommandRequest` is in `src/Hexalith.EventStore/Models`; `IProjectionActor`, `QueryEnvelope`, and `QueryResult` are in `src/Hexalith.EventStore.Server/Actors`; query request/response and payload protection hooks are in `Contracts` but need policy hardening. |
| 2.1 Current epic impact | Done | Epics 3, 4, 5, 8, 9, 11, 13, 16, and 20 are affected because their completed scope did not fully freeze downstream integration contracts. |
| 2.2 Epic-level changes | Action-needed | Add a new post-epic integration-hardening epic rather than reopening completed epics. |
| 2.3 Remaining planned epics | Done | No existing planned epic is invalidated; all current numbered epics are done. |
| 2.4 New epic needed | Done | New Epic 22 is recommended: Public Gateway and Downstream Integration Contracts. |
| 2.5 Priority/order | Done | Gateway DTO/client and projection adapter first, then tenant/RBAC, query policy, publishing guarantees, replay, and payload protection. |
| 3.1 PRD conflict | Action-needed | PRD has FR42/FR50-FR64/FR77 and GDPR roadmap material, but needs new FRs for API-facing gateway DTOs, projection adapter contracts, fail-closed tenant/RBAC integration, query policy, publishing guarantees, replay/read streams, and crypto-shredding completeness. |
| 3.2 Architecture conflict | Action-needed | Architecture must move public integration types out of service/server-only assemblies, define backend capability matrices, and document gateway-owned authorization boundaries. |
| 3.3 UX conflict | Done | No UI redesign. API-as-UX is affected: error taxonomy and client behavior need predictable ProblemDetails and high-level client APIs. |
| 3.4 Other artifacts | Action-needed | Docs, OpenAPI, NuGet package guide, Contracts/Client tests, Testing fakes, integration tests, and deployment/DAPR component docs require updates. |
| 4.1 Direct adjustment | Viable | Best path if grouped as one new post-epic package with focused stories. Effort high, risk medium-high due to public contract changes. |
| 4.2 Rollback | Not viable | No recent implementation should be reverted; the issue is missing public contract, not a bad completed path. |
| 4.3 MVP review | Viable | This should become a v1.1 integration-hardening gate before Parties depends on EventStore as a stable platform. |
| 4.4 Recommended path | Done | Hybrid: add Epic 22 and treat it as a v1.1 gate, with no rollback. |
| 5.1-5.5 Proposal components | Done | Detailed below. |
| 6.1-6.5 Approval/handoff | Action-needed | Awaits Jerome approval before sprint-status, PRD, architecture, and story file updates. |

## 3. Impact Analysis

### Epic Impact

Affected completed epics:

- **Epic 3: Command REST API & Error Experience** - command gateway DTOs are API-facing but not exposed from public contracts/client packages.
- **Epic 4: Event Distribution & Pub/Sub** - durable at-least-once publication exists conceptually, but backend-specific ordering, retry/outbox, dead-letter, and deployment settings are not frozen as a downstream guarantee.
- **Epic 5: Security & Multi-Tenant Isolation** - tenant isolation exists, but EventStore must own tenant lifecycle/membership/role enforcement before invoking Parties.
- **Epic 8: Aspire Orchestration, Sample App & Testing** - Client package exists, but not as a high-level command/query gateway client with test fakes.
- **Epic 9: Query Pipeline & ETag Caching** - query routing/ETag basics exist, but paging, blank search, degraded search, cache semantics, and malformed response taxonomy remain under-specified.
- **Epic 11: Server-Managed Projection Builder** - projection DTOs exist, but generic query actor/projection adapter contracts are still server-internal.
- **Epic 13: Documentation & Developer Onboarding** - docs must stop implying downstream consumers can infer behavior from examples.
- **Epic 16/20: Admin DBA/Debugging** - replay/admin inspection exists, but Parties needs operator-safe per-tenant rebuild APIs, not just admin reconstruction views.

### PRD Impact

The PRD should add a new requirement cluster after FR82:

- FR83-FR86: Gateway command/query client contracts
- FR87-FR89: Projection adapter/query serving contract
- FR90-FR92: Gateway-owned tenant/RBAC enforcement
- FR93-FR95: Query behavior policy
- FR96-FR98: Event publication guarantees
- FR99-FR101: Stream replay/projection rebuild APIs
- FR102-FR104: Payload/snapshot protection and crypto-shredding

MVP impact: this is a **v1.1 integration-hardening gate** for Parties and other downstream Hexalith modules. It should not be deferred to generic v2 admin scope because it affects runtime correctness and downstream duplication now.

### Architecture Impact

Required architecture changes:

- Move API-facing gateway DTOs to `Hexalith.EventStore.Contracts` and high-level HTTP methods/fakes to `Hexalith.EventStore.Client` / `Hexalith.EventStore.Testing`.
- Move or duplicate stable query actor contract types into `Contracts` if domain services are expected to implement a generic adapter.
- Define a gateway-owned authorization boundary that calls `Hexalith.Tenants` through a stable adapter and fails closed on unavailable/unknown tenant data.
- Add a query behavior and error taxonomy section covering paging, filters, blank search, stale/degraded reads, ETag/304 semantics, malformed projection responses, and ProblemDetails mapping.
- Add a pub/sub guarantee matrix by backend, including ordering key/session metadata, retry/outbox behavior, dead-letter topic policy, and DAPR deployment settings.
- Add stream replay/read APIs with checkpointed progress and operator-safe failure recovery.
- Complete the payload protection story: hooks exist, but key deletion/invalidation, backup restore safety, log/admin redaction, and leakage tests must become contractual.

### UX/API Impact

No graphical UI change is required. The API and SDK user experience changes:

- API consumers get `SubmitCommandAsync` and `SubmitQueryAsync` instead of constructing raw HTTP calls.
- ProblemDetails reason codes become stable and programmatically testable.
- Query responses expose enough metadata for cache, stale/degraded, and paging behavior without domain-specific inference.
- Parties can delete local duplicate gateway DTOs once the public packages ship.

## 4. Recommended Approach

Create **Epic 22: Public Gateway and Downstream Integration Contracts**.

Scope classification: **Major** because it changes public package boundaries, PRD requirements, architecture policy, and cross-repository integration behavior. It does not require a total replan, but it does require PM/Architect review before implementation begins.

Recommended story sequence:

1. **22.1 Gateway command/query contracts and typed client**
2. **22.2 Projection adapter contract and generic query actor contract**
3. **22.3 Gateway-owned Hexalith.Tenants/RBAC enforcement**
4. **22.4 Query behavior policy and error taxonomy**
5. **22.5 Event publishing guarantees and backend deployment matrix**
6. **22.6 Stream replay/read APIs and projection rebuild checkpoints**
7. **22.7 Payload protection, snapshot encryption, and crypto-shredding safety**

## 5. Detailed Change Proposals

### Proposal A: PRD Requirement Additions

Section: Product Requirements Document, Functional Requirements after FR82.

OLD:

```markdown
### Administration Tooling — v2 (FR68-FR82)

- FR82: Every trace, metric, and log view in the admin Web UI deep-links to the corresponding detail in the configured external observability tool rather than replicating its UI
```

NEW:

```markdown
### Public Gateway and Downstream Integration Contracts — v1.1 (FR83-FR104)

- FR83: EventStore.Contracts exposes API-facing `SubmitCommandRequest`, `SubmitCommandResponse`, `SubmitQueryRequest`, `SubmitQueryResponse`, validation request/response DTOs, command status DTOs, replay/read DTOs, and stable ProblemDetails extension names used by HTTP gateway clients.
- FR84: EventStore.Client exposes high-level `SubmitCommandAsync`, `SubmitQueryAsync`, `ValidateCommandAsync`, `ValidateQueryAsync`, command status, replay, and stream-read client methods that handle correlation IDs, ETags, 304 responses, ProblemDetails mapping, and typed cancellation.
- FR85: EventStore.Testing exposes deterministic gateway client fakes/builders for command, query, status, replay, ProblemDetails, ETag, and tenant/RBAC failure paths.
- FR86: EventStore documents package ownership rules: API-facing wire contracts live in Contracts; HTTP convenience clients live in Client; runtime server internals remain in Server/EventStore.
- FR87: EventStore.Contracts exposes a stable projection adapter contract for generic query serving, including `QueryEnvelope`, `QueryResult`, projection type metadata, and malformed-response taxonomy, or explicitly documents the generic DAPR actor contract domain services must implement.
- FR88: EventStore can route `Get*`, `List*`, and `Search*` domain queries through `POST /api/v1/queries` without domain services owning tenant authorization or gateway-specific DTOs.
- FR89: EventStore docs define when a domain should use a generic `IProjectionActor.QueryAsync(QueryEnvelope)` adapter versus domain-specific projection actors, including actor type naming, serialization, and test expectations.
- FR90: EventStore gateway validates tenant existence, lifecycle state, user membership, and role/permission before invoking a domain service or projection adapter.
- FR91: EventStore integrates with Hexalith.Tenants through `ITenantValidator` and `IRbacValidator` adapters with fail-closed behavior when tenant/RBAC data is missing, stale, unavailable, or ambiguous.
- FR92: EventStore exposes stable 401/403 ProblemDetails type URIs and reason codes for authentication failure, tenant not found, tenant disabled/suspended, user not a member, insufficient role, insufficient permission, and authorization service unavailable.
- FR93: EventStore query contracts define paging bounds, default page size, maximum page size, cursor/offset semantics, blank search behavior, filter validation, and deterministic ordering requirements.
- FR94: EventStore query responses define metadata fields for `correlationId`, `etag`, `isNotModified`, `isStale`, `isDegraded`, `projectionVersion`, `servedAt`, paging metadata, and optional warning codes.
- FR95: EventStore query error taxonomy defines malformed request, unsupported filter, invalid page, projection missing, projection stale beyond policy, degraded search, malformed projection response, projection timeout, and authorization failures as stable ProblemDetails types.
- FR96: EventStore-published events are durable and at-least-once; per-aggregate causal order is preserved when the configured pub/sub backend supports ordering/session keys, and backend limitations are documented.
- FR97: EventStore documents and validates pub/sub ordering metadata, partition/session key selection, retry/outbox behavior, dead-letter topic policy, replay/drain behavior, and backend-specific deployment settings.
- FR98: EventStore integration tests prove publish-after-persist recovery, duplicate delivery tolerance, per-aggregate causal ordering where supported, and dead-letter handling for supported pub/sub backends.
- FR99: EventStore exposes stream read/replay APIs for projection rebuild with tenant/domain/aggregate scoping, sequence checkpoints, continuation tokens, and resumable progress tracking.
- FR100: EventStore supports operator-safe projection rebuild flows with pause/resume/cancel, failure reason capture, idempotent checkpoint advancement, and no cross-tenant leakage.
- FR101: EventStore documents how domain services rebuild projections from EventStore streams without reading state-store internals.
- FR102: EventStore supports event payload and snapshot protection hooks with metadata that identifies protection state without exposing protected data.
- FR103: EventStore supports crypto-shredding workflows through key deletion/invalidation semantics, restored-backup safety checks, and explicit behavior for unreadable protected payloads.
- FR104: EventStore logs, admin APIs, CLI, MCP, and ProblemDetails never leak protected payload or snapshot data, including during replay, rebuild, backup validation, and failure diagnostics.
```

Rationale: keeps the additions explicit and testable without rewriting earlier completed epics.

### Proposal B: Epic 22 Story Additions

Section: `epics.md`, after Epic 21 or after the current post-epic packages.

NEW:

```markdown
## Epic 22: Public Gateway and Downstream Integration Contracts

EventStore becomes the stable gateway contract for downstream bounded contexts such as Parties. Domain services no longer duplicate command/query DTOs, perform tenant authorization on request paths, or infer projection/replay/publishing behavior from server internals.

### Story 22.1: Gateway Command/Query Contracts and Typed Client

As a downstream service developer,
I want API-facing command/query DTOs and high-level client methods from EventStore packages,
So that Parties can call EventStore without duplicating gateway wire contracts.

Acceptance Criteria:

- `SubmitCommandRequest` and `SubmitCommandResponse` move to, or are source-compatible through, `Hexalith.EventStore.Contracts`.
- `SubmitQueryRequest` and `SubmitQueryResponse` remain in Contracts and gain any missing metadata needed by the query policy.
- `Hexalith.EventStore.Client` exposes `SubmitCommandAsync` and `SubmitQueryAsync` APIs that map 2xx, 304, 4xx, and 5xx responses to stable result/problem types.
- Client APIs handle caller-provided and generated correlation IDs consistently and return the effective correlation ID.
- `Hexalith.EventStore.Testing` includes fake gateway clients for success, validation, auth, conflict, not-modified, stale/degraded, and unavailable paths.

### Story 22.2: Projection Adapter Contract and Query Serving Model

As a domain service developer,
I want a stable projection query adapter contract,
So that Parties can serve GetParty, ListParties, and SearchParties through EventStore queries.

Acceptance Criteria:

- `QueryEnvelope`, `QueryResult`, projection type metadata, and malformed response categories are public contracts or explicitly documented generic actor contracts.
- Domain-specific projection actors may wrap the generic contract but cannot require EventStore query callers to know domain actor types.
- Query adapter tests prove entity, list, and search query routing for a sample domain.
- Documentation names the required actor type, method signature, serialization attributes, and payload/error behavior.

### Story 22.3: Gateway-Owned Tenant and RBAC Enforcement

As a platform owner,
I want EventStore to validate tenant lifecycle, membership, and role before invoking domain services,
So that Parties request paths do not perform tenant authorization.

Acceptance Criteria:

- `ITenantValidator` and `IRbacValidator` integrate with Hexalith.Tenants through a stable adapter boundary.
- Tenant not found, disabled/suspended, missing membership, insufficient role, insufficient permission, and validator-unavailable cases fail closed.
- 401/403 ProblemDetails type URIs and reason codes are stable and documented.
- Domain command and query invocation tests prove Parties is not called after failed tenant/RBAC validation.

### Story 22.4: Query Behavior Policy and Error Taxonomy

As an API consumer,
I want query behavior frozen across paging, filters, cache, and degraded reads,
So that Parties API behavior is predictable when routed through EventStore.

Acceptance Criteria:

- Query contracts define page size defaults, maximums, cursor/offset behavior, blank search behavior, filter validation, and ordering rules.
- Query responses include cache and freshness metadata: `etag`, `isNotModified`, `isStale`, `isDegraded`, `projectionVersion`, `servedAt`, paging metadata, and warning codes as applicable.
- 304/ETag behavior is represented in client result types without forcing callers to parse raw HTTP responses.
- Malformed projection responses and projection timeouts map to stable ProblemDetails types.

### Story 22.5: Event Publishing Guarantees and Backend Deployment Matrix

As a downstream projection owner,
I want EventStore-published events to be durable, at-least-once, and causally ordered per aggregate where supported,
So that Parties projections can rely on EventStore publication guarantees.

Acceptance Criteria:

- Event publication docs define durability, at-least-once delivery, duplicate tolerance, ordering/session metadata, retry/outbox behavior, and dead-letter policy.
- Backend matrix covers Redis/RabbitMQ/Azure Service Bus or the currently supported pub/sub backends.
- Deployment docs include required DAPR metadata/settings for ordering, sessions, dead-lettering, and retries.
- Integration tests prove publish-after-persist recovery and backend-specific ordering behavior where supported.

### Story 22.6: Stream Replay/Read APIs and Projection Rebuild Checkpoints

As a projection owner,
I want per-tenant resumable stream replay APIs,
So that Parties projections can rebuild from EventStore streams safely.

Acceptance Criteria:

- EventStore exposes stream read/replay APIs scoped by tenant, domain, aggregate, sequence range, and checkpoint.
- Rebuild progress is persisted with idempotent checkpoint advancement and operator-visible status.
- APIs support pause, resume, cancel, retry, and failure reason capture.
- Documentation shows how a domain rebuilds projections without reading EventStore state-store keys directly.

### Story 22.7: Payload Protection, Snapshot Encryption, and Crypto-Shredding Safety

As a platform owner handling PII,
I want event and snapshot payload protection to support crypto-shredding,
So that GDPR deletion workflows remain safe across live data, backups, logs, and admin surfaces.

Acceptance Criteria:

- Existing payload/snapshot protection hooks are documented as public extension points with required metadata semantics.
- Key deletion/invalidation behavior is specified for live reads, replay, projection rebuild, backup restore, and admin inspection.
- Restored-backup safety checks detect missing/invalid keys before protected data is served.
- Logs, ProblemDetails, admin UI, CLI, MCP, and test artifacts never expose protected payload or snapshot content.
```

### Proposal C: Architecture Amendments

Section: Architecture Decision Document, add ADR after current public API/package decisions.

NEW:

```markdown
#### ADR-22: Public Gateway Contracts Live in Contracts and Client Packages

API-facing HTTP DTOs are public wire contracts and live in `Hexalith.EventStore.Contracts`. High-level HTTP convenience APIs live in `Hexalith.EventStore.Client`. The EventStore service assembly may compose these contracts into MediatR commands, but downstream modules must never reference service/server-only assemblies or duplicate gateway DTOs.

Consequences:

- `SubmitCommandRequest` and `SubmitCommandResponse` move out of the EventStore service assembly or become forwarding/source-compatible types in Contracts.
- ProblemDetails extension names and reason codes are versioned as contract surface.
- Query 304/stale/degraded behavior is represented in client result types, not left to raw HTTP parsing.
- `Hexalith.EventStore.Testing` owns fakes/builders for every public gateway client path.
```

Add architecture section:

```markdown
### Downstream Authorization Boundary

EventStore owns tenant and RBAC enforcement for command and query gateway paths. Domain services such as Parties receive only requests that have passed tenant existence, tenant lifecycle, membership, and role/permission validation. If Hexalith.Tenants cannot answer authoritatively, EventStore fails closed before domain invocation.
```

Add architecture section:

```markdown
### Pub/Sub and Replay Guarantee Matrix

EventStore documents guarantees per supported backend:

- At-least-once delivery
- Duplicate delivery expectation
- Per-aggregate causal ordering support
- Ordering/session key metadata
- Retry policy
- Dead-letter routing
- Outbox/drain behavior
- Required DAPR component metadata
```

Rationale: these decisions prevent Parties from binding to accidental implementation locations.

### Proposal D: Documentation Updates

Update these docs:

- `docs/reference/command-api.md`: source DTOs from Contracts; document client package methods and correlation ID behavior.
- `docs/reference/query-api.md`: add paging, blank search, filters, metadata fields, ETag/304 client mapping, stale/degraded policy, malformed-response taxonomy.
- `docs/reference/nuget-packages.md`: clarify package ownership for Contracts, Client, Testing, SignalR.
- `docs/guides/security-model.md`: add gateway-owned Hexalith.Tenants/RBAC enforcement and fail-closed matrix.
- `docs/guides/dapr-component-reference.md`: add pub/sub backend ordering/retry/dead-letter settings.
- `docs/guides/disaster-recovery.md`: add protected payload restore safety and projection rebuild checkpoint recovery.
- New `docs/reference/stream-replay-api.md`: stream read/replay contract for projection rebuild.
- New `docs/guides/payload-protection-and-crypto-shredding.md`: encryption hooks, key invalidation, backup restore safety, and leakage rules.

### Proposal E: Sprint Status Update

Do not update `sprint-status.yaml` until this proposal is approved.

After approval, append:

```yaml
  # Epic 22: Public Gateway and Downstream Integration Contracts
  # Added by sprint-change-proposal-2026-05-12-eventstore-parties-integration-contract-gaps.md.
  epic-22: backlog
  22-1-gateway-command-query-contracts-and-typed-client: backlog
  22-2-projection-adapter-contract-and-query-serving-model: backlog
  22-3-gateway-owned-tenant-and-rbac-enforcement: backlog
  22-4-query-behavior-policy-and-error-taxonomy: backlog
  22-5-event-publishing-guarantees-and-backend-deployment-matrix: backlog
  22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints: backlog
  22-7-payload-protection-snapshot-encryption-and-crypto-shredding-safety: backlog
```

## 6. Implementation Handoff

Scope classification: **Major**.

Recommended routing:

| Work | Owner |
| ---- | ----- |
| PRD requirement additions and scope gate | Product Manager |
| Architecture/public package boundary decisions | Architect |
| Story creation and sequencing | Product Owner / Scrum Master |
| Contracts, Client, Testing, Server implementation | Developer |
| Tenant/RBAC and ProblemDetails security tests | Test Architect |
| Parties integration validation | Parties developer + EventStore developer |

Implementation order:

1. Approve this proposal.
2. Update `prd.md`, `architecture.md`, `epics.md`, and `sprint-status.yaml`.
3. Create Story 22.1 first and validate it against the Parties duplication case.
4. Do not let Parties depend on EventStore service/server assemblies as a workaround.
5. Treat EventStore public package changes as SemVer-relevant.

## 7. Success Criteria

- Parties removes duplicated EventStore command/query gateway DTOs.
- Parties uses `Hexalith.EventStore.Contracts`, `Hexalith.EventStore.Client`, and `Hexalith.EventStore.Testing` for gateway contracts, client calls, and fakes.
- EventStore rejects invalid tenant/RBAC states before any Parties invocation.
- `POST /api/v1/queries` can serve GetParty, ListParties, and SearchParties through a documented projection adapter contract.
- Query paging, filtering, blank search, stale/degraded behavior, ETag/304 semantics, and malformed responses are covered by contract tests.
- Event publication guarantees are documented and tested for supported backends.
- Projection rebuild uses EventStore stream replay/read APIs with checkpoints, not state-store internals.
- Payload/snapshot protection behavior is safe across logs, admin surfaces, replay, rebuild, and restored backups.

## 8. Recommendation

Approve Epic 22 as a v1.1 integration-hardening gate before Parties treats EventStore as a stable platform dependency. This keeps the platform promise honest: EventStore owns the gateway, security, event, replay, and protection contracts; domain services own domain behavior.

