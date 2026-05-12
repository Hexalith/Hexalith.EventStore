# Sprint Change Proposal: EventStore Requirements Gaps for Parties Integration

Date: 2026-05-12
Project: Hexalith.EventStore
Trigger: Parties now depends on EventStore as a stable platform gateway, but several command/query, tenant/RBAC, projection, replay, publishing, and payload-protection guarantees are either only partially implemented, server-internal, or not frozen as PRD/architecture requirements.
Mode: Batch
Prepared by: Codex (Developer)

## 1. Issue Summary

Parties exposed a platform-contract gap. EventStore is no longer just an internal command/event runtime; it is becoming the gateway that downstream bounded contexts rely on for command submission, query routing, tenant/RBAC enforcement, event publication, projection rebuild, and PII-safe storage semantics.

Current repository evidence shows that some of the first-class gateway work has already started:

- `SubmitCommandRequest` and `SubmitCommandResponse` now exist in `src/Hexalith.EventStore.Contracts/Commands`.
- The service assembly still carries compatibility wrappers under `src/Hexalith.EventStore/Models`, so package ownership and deprecation guidance must be made explicit.
- `IEventStoreGatewayClient` in `src/Hexalith.EventStore.Client/Gateway` exposes high-level `SubmitCommandAsync` and `SubmitQueryAsync` methods.
- `FakeEventStoreGatewayClient` exists in `src/Hexalith.EventStore.Testing/Fakes`.
- `SubmitQueryRequest` and `SubmitQueryResponse` are in `Contracts`.

The remaining gaps are still significant:

- Query projection adapter contracts (`IProjectionActor`, `QueryEnvelope`, `QueryResult`) remain in `Hexalith.EventStore.Server`, which is not an appropriate public dependency for Parties.
- Tenant/RBAC validator abstractions exist in the service/server boundary, but the PRD and docs do not freeze Hexalith.Tenants integration, fail-closed lifecycle/membership/role behavior, or stable 401/403 reason codes.
- Query policy is not complete enough for Parties to treat `POST /api/v1/queries` as a stable API for GetParty, ListParties, and SearchParties.
- Event publishing docs cover at-least-once and dead-lettering in places, but not a backend-specific ordering/session/outbox/dead-letter deployment matrix.
- Replay/admin reconstruction exists, but Parties needs tenant/domain-scoped stream read/replay APIs with resumable checkpoints for projection rebuild.
- Payload protection hooks exist, but crypto-shredding, key invalidation, restored-backup safety, and protected-data leakage rules are not frozen as runtime requirements.

This is a major planning correction: the codebase has partial implementation, but PRD, architecture, epics, sprint status, docs, and tests still need a single approved integration-hardening package.

## 2. Checklist Findings

| Item | Status | Finding |
| ---- | ------ | ------- |
| 1.1 Triggering issue | Done | Parties integration needs EventStore-owned public contracts and runtime guarantees instead of duplicated DTOs, service-assembly references, or domain-owned request authorization. |
| 1.2 Core problem | Done | Requirement gap plus public-surface placement issue. Some gateway client work is implemented, but the full downstream contract is not approved or documented. |
| 1.3 Evidence | Done | Gateway DTO/client/fake now exist; projection query contracts remain server-bound; PRD lacks FR83+ style requirements; sprint status lacks Epic 22; docs do not freeze the seven Parties-facing policy clusters. |
| 2.1 Current epic impact | Done | Completed Epics 3, 4, 5, 8, 9, 11, 13, 16, and 20 are affected by follow-on contract hardening. |
| 2.2 Epic-level changes | Action-needed | Add a new post-epic integration-hardening epic instead of reopening completed epics. |
| 2.3 Remaining planned epics | Done | Current numbered epics are done; no existing planned epic is invalidated. |
| 2.4 New epic needed | Done | New Epic 22 is recommended: Public Gateway and Downstream Integration Contracts. |
| 2.5 Priority/order | Done | Prioritize public gateway contract closure, then projection adapter, tenant/RBAC, query policy, publishing guarantees, replay/rebuild, and payload protection. |
| 3.1 PRD conflict | Action-needed | Add explicit v1.1 requirements for all seven gap clusters. |
| 3.2 Architecture conflict | Action-needed | Public package boundaries, projection adapter ownership, authorization boundary, pub/sub guarantee matrix, replay APIs, and payload protection need architecture decisions. |
| 3.3 UX conflict | Done | No visual UI change; API/SDK user experience changes through typed clients, stable ProblemDetails, paging/cache metadata, and predictable failure behavior. |
| 3.4 Other artifacts | Action-needed | Update docs, OpenAPI, generated API docs, package guidance, tests, deployment docs, and `sprint-status.yaml` after approval. |
| 4.1 Direct adjustment | Viable | Best path: add one integration-hardening epic with focused stories. Effort high; risk medium-high due to public contract implications. |
| 4.2 Rollback | Not viable | Existing gateway-client work should be completed and documented, not reverted. |
| 4.3 MVP review | Viable | Treat as a v1.1 platform-readiness gate before Parties depends on EventStore as stable infrastructure. |
| 4.4 Recommended path | Done | Hybrid direct adjustment plus v1.1 gate. No rollback. |
| 5.1-5.5 Proposal components | Done | Detailed below. |
| 6.1-6.5 Approval/handoff | Action-needed | Awaits Jerome approval before PRD, epics, architecture, and sprint status are changed. |

## 3. Impact Analysis

### Epic Impact

- **Epic 3: Command REST API & Error Experience** - command gateway contracts now exist in Contracts, but compatibility wrapper ownership, ProblemDetails extensions, and client behavior must be finalized.
- **Epic 4: Event Distribution & Pub/Sub** - at-least-once and drain/dead-letter behavior exist, but backend ordering/session metadata and deployment validation must be made contractual.
- **Epic 5: Security & Multi-Tenant Isolation** - gateway-owned tenant/RBAC enforcement must integrate with Hexalith.Tenants and fail closed before Parties is invoked.
- **Epic 8: Aspire Orchestration, Sample App & Testing** - Client and Testing packages now have relevant pieces, but package docs and fakes need to cover all public paths.
- **Epic 9: Query Pipeline & ETag Caching** - ETag/304 exists, but paging, blank search, filter validation, degraded/stale reads, and malformed projection responses are not frozen.
- **Epic 11: Server-Managed Projection Builder** - generic projection actor contracts are still server-internal and therefore awkward for domain services.
- **Epic 13: Documentation & Developer Onboarding** - downstream consumers need explicit contracts, not inference from examples.
- **Epic 16/20: Admin DBA/Debugging** - admin reconstruction/debugging exists, but Parties needs operator-safe projection rebuild APIs and checkpoints.

### PRD Impact

Add a new v1.1 requirement cluster after the existing functional requirements:

- Public command/query gateway contracts and high-level client APIs.
- Stable projection adapter or documented generic actor query contract.
- Gateway-owned tenant existence, lifecycle, membership, role, and permission enforcement.
- Query behavior policy for paging, blank search, filtering, freshness, cache, metadata, and errors.
- Durable at-least-once event publishing guarantees with backend capability matrix.
- Tenant/domain-scoped stream read/replay APIs for resumable projection rebuild.
- Payload/snapshot protection, key invalidation, crypto-shredding, backup-restore safety, and no protected-data leakage.

MVP impact: this is a **v1.1 integration-hardening gate** for Parties and other downstream Hexalith modules. It should not be deferred to generic v2 admin scope because it affects runtime correctness and downstream package boundaries now.

### Architecture Impact

Architecture must decide and document:

- Which DTOs live in `Hexalith.EventStore.Contracts`, which client methods live in `Hexalith.EventStore.Client`, and which compatibility wrappers remain in the service assembly.
- Whether `QueryEnvelope`, `QueryResult`, and a generic `IProjectionActor` contract move to Contracts, or whether EventStore publishes a separate domain projection adapter package.
- How EventStore invokes Hexalith.Tenants through `ITenantValidator` and `IRbacValidator`, including fail-closed behavior when tenant/RBAC state is unavailable or ambiguous.
- A stable ProblemDetails reason-code taxonomy for 401/403, query failures, malformed projection responses, replay failures, and payload-protection failures.
- Backend-specific pub/sub settings for ordering keys, sessions, retry, dead-lettering, and outbox/drain behavior.
- Stream read/replay APIs with continuation tokens/checkpoints and tenant/domain scoping.
- Payload/snapshot encryption hooks, key lifecycle semantics, backup restore safety, and redaction guarantees.

## 4. Recommended Approach

Create **Epic 22: Public Gateway and Downstream Integration Contracts**.

Scope classification: **Major**. This changes public package boundaries, PRD requirements, architecture policy, cross-repository behavior, and Parties readiness. It does not require a total replan because it can be delivered as a focused post-epic package.

Recommended story sequence:

1. **22.1 Gateway command/query contract closure and package docs**
2. **22.2 Projection adapter contract and generic query actor model**
3. **22.3 Gateway-owned Hexalith.Tenants/RBAC enforcement**
4. **22.4 Query behavior policy and error taxonomy**
5. **22.5 Event publishing guarantees and backend deployment matrix**
6. **22.6 Stream replay/read APIs and projection rebuild checkpoints**
7. **22.7 Payload protection, snapshot encryption, and crypto-shredding safety**

## 5. Detailed Change Proposals

### Proposal A: PRD Additions

Add a `Public Gateway and Downstream Integration Contracts - v1.1` section with these requirements:

- EventStore.Contracts exposes stable API-facing command/query, status, validation, replay/read, ProblemDetails extension, and metadata contracts.
- EventStore.Client exposes high-level command/query/status/replay client methods that handle correlation IDs, ETags, 304, ProblemDetails, typed cancellation, and result mapping.
- EventStore.Testing exposes deterministic fakes/builders for success, validation, auth, conflict, not-modified, stale/degraded, unavailable, replay, and payload-protection paths.
- EventStore publishes either a stable projection adapter contract or a documented generic actor contract for query serving.
- EventStore owns tenant lifecycle, membership, role, and permission enforcement before domain invocation.
- Query contracts define paging bounds, blank search behavior, filter validation, deterministic ordering, metadata fields, cache semantics, and stable error taxonomy.
- Event publishing is durable and at-least-once; per-aggregate causal order is preserved where the backend supports it and documented where it does not.
- Stream read/replay APIs support per-tenant projection rebuild with checkpoints, pause/resume/cancel, progress, and operator-safe recovery.
- Payload and snapshot protection support key deletion/invalidation, restored-backup safety, and redaction across logs/admin/CLI/MCP/ProblemDetails.

### Proposal B: Epic 22 Story Additions

Add this epic after Epic 21:

```markdown
## Epic 22: Public Gateway and Downstream Integration Contracts

EventStore becomes the stable gateway contract for downstream bounded contexts such as Parties. Domain services no longer duplicate EventStore gateway DTOs, depend on EventStore service/server assemblies, perform request-path tenant authorization, or infer query/replay/publishing behavior from implementation details.
```

Story acceptance summary:

- **22.1 Gateway command/query contract closure and package docs**: confirm Contracts owns wire DTOs, Client owns high-level methods, Testing owns fakes, service wrappers are compatibility-only, and docs/OpenAPI/generator output match.
- **22.2 Projection adapter contract and generic query actor model**: publish or document `QueryEnvelope`, `QueryResult`, actor naming, serialization, malformed-response taxonomy, and sample Get/List/Search query routing.
- **22.3 Gateway-owned Hexalith.Tenants/RBAC enforcement**: integrate fail-closed tenant lifecycle/membership/role checks and stable 401/403 ProblemDetails reason codes; prove Parties is not invoked after failed authorization.
- **22.4 Query behavior policy and error taxonomy**: freeze paging, blank search, filters, stale/degraded reads, ETag/304, metadata, and query failure mapping.
- **22.5 Event publishing guarantees and backend deployment matrix**: document and test at-least-once, duplicate tolerance, ordering/session metadata, retry/outbox/drain, and dead-letter behavior per supported backend.
- **22.6 Stream replay/read APIs and projection rebuild checkpoints**: expose tenant/domain/aggregate/sequence-scoped APIs with continuation tokens, idempotent checkpoints, progress, pause/resume/cancel, and failure recovery.
- **22.7 Payload protection, snapshot encryption, and crypto-shredding safety**: specify encryption hooks, key deletion/invalidation, restored-backup behavior, unreadable protected payload semantics, and no-leak tests.

### Proposal C: Architecture Amendments

Add architecture decisions for:

- **Public package ownership**: public wire contracts in Contracts, HTTP convenience APIs in Client, deterministic fakes in Testing, runtime internals in Server/EventStore.
- **Projection query adapter**: domain services implement a stable public adapter instead of referencing `Hexalith.EventStore.Server`.
- **Authorization boundary**: EventStore validates tenant/RBAC before command/query domain invocation; domain services remain domain-focused.
- **Query policy**: paging, filters, cache, freshness, malformed response, timeout, and ProblemDetails semantics are part of the platform contract.
- **Pub/sub guarantee matrix**: supported backends list durability, at-least-once delivery, duplicate behavior, causal ordering support, ordering/session keys, retry, dead-letter routing, and DAPR component settings.
- **Replay/rebuild contract**: projection rebuild reads from EventStore streams through public APIs, not state-store internals.
- **Payload protection**: payload/snapshot encryption and crypto-shredding are treated as security contracts, not optional implementation notes.

### Proposal D: Documentation Updates

Update or add:

- `docs/reference/command-api.md` - public DTO package ownership, compatibility wrapper note, correlation ID behavior, client method guidance.
- `docs/reference/query-api.md` - paging, blank search, filters, metadata, ETag/304 mapping, stale/degraded policy, malformed response taxonomy.
- `docs/reference/nuget-packages.md` - Contracts/Client/Testing ownership and Parties-style integration guidance.
- `docs/guides/security-model.md` - gateway-owned Hexalith.Tenants/RBAC enforcement and fail-closed matrix.
- `docs/guides/dapr-component-reference.md` - pub/sub backend ordering/retry/dead-letter/session settings.
- `docs/guides/disaster-recovery.md` - replay/rebuild checkpoints and protected payload restore safety.
- New `docs/reference/stream-replay-api.md`.
- New `docs/guides/payload-protection-and-crypto-shredding.md`.

### Proposal E: Sprint Status Update

After approval, append backlog entries to `_bmad-output/implementation-artifacts/sprint-status.yaml`:

```yaml
  # Epic 22: Public Gateway and Downstream Integration Contracts
  epic-22: backlog
  22-1-gateway-command-query-contract-closure-and-package-docs: backlog
  22-2-projection-adapter-contract-and-generic-query-actor-model: backlog
  22-3-gateway-owned-tenant-and-rbac-enforcement: backlog
  22-4-query-behavior-policy-and-error-taxonomy: backlog
  22-5-event-publishing-guarantees-and-backend-deployment-matrix: backlog
  22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints: backlog
  22-7-payload-protection-snapshot-encryption-and-crypto-shredding-safety: backlog
```

Do not update sprint status until the proposal is approved.

## 6. Implementation Handoff

Scope classification: **Major**.

Recommended routing:

| Work | Owner |
| ---- | ----- |
| PRD requirement additions and v1.1 scope gate | Product Manager |
| Public package boundary and architecture policy | Architect |
| Epic/story creation and sprint status update | Product Owner / Scrum Master |
| Contracts, Client, Testing, Server implementation | Developer |
| Security/query/replay/publishing/payload-protection test strategy | Test Architect |
| Parties validation and DTO-removal proof | Parties developer + EventStore developer |

Implementation order:

1. Approve this proposal.
2. Update `prd.md`, `architecture.md`, `epics.md`, and `sprint-status.yaml`.
3. Create Story 22.1 first and reconcile the already-implemented gateway DTO/client/fake work with docs and tests.
4. Block Parties from referencing EventStore service/server assemblies as a workaround.
5. Treat public package and ProblemDetails changes as SemVer-relevant.

## 7. Success Criteria

- Parties uses EventStore.Contracts, EventStore.Client, and EventStore.Testing for gateway contracts, calls, and fakes.
- Parties does not duplicate EventStore command/query DTOs.
- Parties does not perform request-path tenant authorization for EventStore gateway calls.
- EventStore rejects invalid tenant/RBAC states before any Parties invocation.
- `POST /api/v1/queries` can support GetParty, ListParties, and SearchParties through a documented public projection adapter.
- Query paging, blank search, filters, stale/degraded data, ETag/304, and malformed responses have contract tests.
- Event publishing guarantees are documented and validated for supported backends.
- Projection rebuild uses EventStore stream read/replay APIs with checkpoints.
- Payload/snapshot protection is safe across live reads, replay, rebuild, logs, admin surfaces, and restored backups.

## 8. Recommendation

Approve Epic 22 as a v1.1 integration-hardening gate. The repository already contains useful pieces of the gateway client contract, so the next move is not a blank-slate build; it is to finish the public surface, freeze the cross-service policies, and align PRD/architecture/stories/status so Parties can depend on EventStore without accidental coupling.
