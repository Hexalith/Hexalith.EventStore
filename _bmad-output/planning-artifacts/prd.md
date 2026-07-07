---
title: eventstore Phase 4 Implementation Readiness Recovery PRD
status: final
created: 2026-07-05
updated: 2026-07-05
project: eventstore
source_artifacts:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md
  - _bmad-output/planning-artifacts/epics.md
---

# PRD: eventstore Phase 4 Implementation Readiness Recovery

## 0. Document Purpose

This PRD is the authoritative Phase 4 functional and non-functional requirements baseline for Hexalith.EventStore. It exists to close the implementation-readiness blocker reported on 2026-07-05: the epic plan contained FR1-FR35 and NFR1-NFR18, but no standalone PRD existed for PRD-to-epic traceability.

This document owns product requirement intent, MVP scope, non-goals, success metrics, and FR/NFR traceability. `_bmad-output/planning-artifacts/epics.md` owns implementation slicing and sequencing. The required architecture and UX planning artifacts remain separate handoffs and must not be replaced by this PRD.

## 1. Planning Baseline

The Phase 4 baseline is derived from the approved sprint-change proposals already consolidated in `epics.md` plus the approved readiness-recovery proposal dated 2026-07-05.

The baseline correction does not reduce MVP scope. It separates planning responsibilities:

- `prd.md` owns FR/NFR truth and readiness traceability.
- `architecture.md` must own component, integration, topology, and decision-record gates.
- `ux.md` must own UI governance, user-flow evidence, and support-safe interaction rules.
- `epics.md` owns story slicing, sequencing, acceptance criteria, and implementation handoff.

Implementation should not resume as a full Phase 4 package until this PRD, the architecture artifact, the UX artifact, story splits, and high-risk NFR traceability are reconciled and readiness is re-run.

## 2. Vision

Hexalith.EventStore Phase 4 turns a working DAPR-native event sourcing platform into a safer and more reusable developer platform. Domain authors should be able to build domain modules with domain code only, while EventStore libraries own hosting, query routing, projection dispatch, read-model storage, cursor protection, telemetry, health checks, Aspire wiring, and packaging.

The same phase hardens external integration and operational trust. External REST APIs should be generated into dedicated API hosts, interactive UI hosts should consume client libraries directly, and operators should get fail-closed security, tenant isolation, bounded recovery behavior, honest admin surfaces, reproducible release gates, and integration tests that prove persisted state rather than only smoke status.

The central product bet is that Phase 4 succeeds only if platform reuse and operational hardening are delivered together. A domain-centric SDK without security, delivery, release, and evidence discipline would be easier to adopt but unsafe to operate; hardening without better seams would leave every domain reimplementing the same boilerplate.

## 3. Target Users And Jobs

### 3.1 Target Users

- Domain authors who build EventStore-backed domain services such as Sample and Tenants.
- External API host developers who expose generated typed REST surfaces.
- Interactive UI developers maintaining Sample Blazor UI, Tenants UI, and Admin UI.
- Platform maintainers responsible for package shape, release workflows, and shared seams.
- Operators responsible for tenant isolation, event delivery, runtime topology, deployment, and admin trust.
- Product owners maintaining the forward backlog for deferred capabilities.

### 3.2 Jobs To Be Done

- Build a domain module without reimplementing EventStore hosting, DAPR endpoint routing, telemetry, state-store, cursor, projection, or Aspire plumbing.
- Expose external typed REST APIs without putting generated or hand-written per-message controllers inside interactive UI hosts.
- Operate EventStore with fail-closed auth, tenant isolation, safe secret handling, bounded replay/projection cost, crash recovery, and clear delivery semantics.
- Release EventStore packages reproducibly from a manifest-governed package set without leaking source-reference or submodule state into release output.
- Trust UI and admin surfaces because they present accepted, confirmed, deferred, and unavailable states honestly and safely.
- Prove high-risk behavior with targeted tests that assert persisted Redis/state-store/read-model/CloudEvent evidence, not only HTTP status or mock calls.

### 3.3 Product Shape

This is a brownfield developer-platform and operations-hardening PRD. Its primary form factors are .NET libraries, DAPR/Aspire hosted services, generated REST API hosts, CI/CD workflows, and supporting Admin/Sample/Tenants Blazor surfaces. Detailed UX design belongs in `ux.md`; this PRD captures UI-facing requirements and governance boundaries only.

## 4. Glossary

- **Admin UI** - EventStore administrative Blazor surface. It must never present unavailable backup, restore, import, compaction, or deferred operations as functional.
- **Aggregate Identity** - The contract identity made from tenant, domain, and aggregate id. EventStore envelope identifiers use ULID semantics where applicable.
- **Architecture Artifact** - `_bmad-output/planning-artifacts/architecture.md`, the Phase 4 decision and invariant document required before implementation readiness can return to READY.
- **DAPR Boundary** - The state, pub/sub, service invocation, actor, config, access-control, and resiliency infrastructure abstraction boundary.
- **Domain Module** - A domain-centric EventStore-backed module containing aggregates, commands, events, projections, query handlers, validators, and contracts, but not reusable platform boilerplate.
- **Domain-Service SDK** - EventStore SDK surface that supplies canonical host composition, DAPR endpoints, discovery, telemetry, health checks, projection dispatch, query routing, event consumers, read-model store, and cursor codec.
- **External API Host** - A dedicated host for generated REST controllers. It is separate from interactive UI hosts.
- **Interactive UI Host** - A Blazor or similar user-facing host. It consumes EventStore client libraries and must not host generated or hand-written per-message MVC command/query controllers.
- **Projection-Confirmed Success** - UI success state backed by read-model/projection evidence, not only command acceptance or SignalR notification.
- **Readiness Recovery** - The approved planning correction that creates PRD, architecture, and UX artifacts, splits oversized stories, and maps high-risk NFRs before Phase 4 execution.
- **Support-Safe State** - UI, logs, diagnostics, and errors that do not expose tokens, decoded JWT payloads, raw metadata, raw payloads, cursor internals, ETag internals, stack traces, or secrets.
- **UX Artifact** - `_bmad-output/planning-artifacts/ux.md`, the Phase 4 UI governance and user-flow document required for UI-affecting stories.

## 5. Product Concerns

Phase 4 carries these concerns and the PRD must preserve them through downstream planning:

- Security and fail-closed authorization across public, internal, domain-service, projection-notification, admin, and generated REST surfaces.
- Tenant isolation across state keys, actor ids, topics, admin queries, generated APIs, SignalR groups, and deployment configuration.
- Public API and package contract stability for EventStore libraries, REST generator output, and domain-service seams.
- DAPR/Aspire runtime topology parity across AppHost, tests, production component templates, ACLs, app ids, topics, and sidecar arguments.
- Event correctness, idempotency, crash recovery, append durability, and delivery semantics under duplicate, concurrent, late, and failure conditions.
- Release reproducibility, package manifest discipline, submodule path policy, and shared workflow governance.
- UI governance for FrontComposer and Fluent UI V5 usage, projection-confirmed success, honest unavailable operations, support-safe rendering, accessibility, and localization evidence.
- Cost and evolution boundaries for snapshots, projection replay, sequence guards, event versioning, upcasting, and cancellation seams.
- Integration evidence quality, especially persisted state-store/read-model/CloudEvent assertions.

## 6. Features And Functional Requirements

### 6.1 Domain Author Self-Service Platform

**Description:** Domain authors can implement domain behavior while EventStore supplies reusable hosting, query, projection, read-model, cursor, telemetry, health, Aspire, and packaging seams.

| ID | Requirement |
| --- | --- |
| FR1 | Domain modules built on Hexalith.EventStore must be domain-centric, containing domain code such as aggregates, commands, events, projections, query handlers, validators, and contracts, while platform boilerplate is supplied by EventStore libraries. |
| FR2 | The platform must provide a domain-service SDK with `AddEventStoreDomainService`, `UseEventStoreDomainService`, and `MapEventStoreDomainService` so a domain service host can be reduced to the canonical SDK host shape. |
| FR3 | The domain-service SDK must expose the canonical DAPR-facing endpoints `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata`. |
| FR4 | The platform must provide a domain query-handler seam using `IDomainQueryHandler`, discovery, dispatch, operational metadata reporting, gateway-side query-type capture, handler-aware routing to domain `/query` endpoints, and end-to-end `QueryResponseMetadata` propagation for freshness, projection version, ETag, served-at, degraded/warning state, and paging evidence, carrying an explicit query-response provenance classification (projection-backed, handler-computed, or unknown) that governs whether that evidence is projection-backed. |
| FR5 | The platform must provide a generic persisted read-model store and write policy with optimistic-concurrency merge-on-write, multi-key/index support, DAPR implementation, and in-memory testing support. |
| FR6 | The platform must provide a reusable DataProtection-backed query cursor codec with scope validation, payload limits, tamper/key-rotation handling, and caller-supplied purpose isolation. |
| FR7 | The platform must provide a generic projection-handler seam for `/project` dispatch and a generic domain-event subscription/consumer pipeline with deduplication and endpoint mapping. |
| FR8 | The platform must provide Aspire, telemetry, and health-check extensions for domain modules, including `AddEventStoreDomainModule`, convention telemetry, and DAPR state-store health checks. |
| FR9 | The Sample domain and Tenants domain must adopt platform SDK seams so duplicated request routers, projection actors, cursor codecs, state-store plumbing, telemetry, health checks, and per-domain Aspire wiring are removed or reduced to domain-specific logic. |
| FR10 | The EventStore package set must include the domain-service and service-default packages as publishable packages, and release packaging must publish only the manifest-governed EventStore package set. |

**Done evidence:** Release build is clean under warnings-as-errors; Sample and Tenants adoption proofs preserve domain semantics; guardrails prevent domain modules from reintroducing reusable platform boilerplate.

### 6.2 External Integration Surfaces

**Description:** External developers get generated typed REST APIs in dedicated API hosts while interactive UI hosts stay client-library consumers and projection notifications remain scoped, bounded, and backward compatible.

| ID | Requirement |
| --- | --- |
| FR11 | The platform must provide a REST API source-generator contract seam with `ICommandContract`, `IQueryContract`, optional `RestRouteAttribute`, and assembly-level `RestApiAttribute`. |
| FR12 | The REST API generator must discover command/query contracts and emit typed, OpenAPI-visible controllers that delegate to `IEventStoreGatewayClient`, forward canonical query metadata headers when supplied by the gateway, and include tests covering discovery, routing conventions, diagnostics, generated output, query metadata headers, `304`, and safe problem-detail behavior. |
| FR13 | Generated REST controllers must live in dedicated external-facing API hosts, not interactive UI hosts; interactive UI hosts must consume EventStore client libraries directly. |
| FR14 | The Sample proof must introduce a contracts-only Sample contracts library and an external Sample API host, move shared contracts there, and prove generated query and command controllers through that external API host. |
| FR15 | The Tenants proof must move generated Tenants controllers to an external Tenants API host, while Tenants UI consumes client libraries and no longer hosts hand-written per-message controllers; any Tenants freshness, projection-version, ETag, or paging evidence shown by generated APIs or UI must come from the platform query metadata path. |
| FR16 | The projection-changed transport must add an additive metadata-rich detail path with optional group scope, bounded metadata, scoped SignalR groups, DAPR notification support where needed, and preserved signal-only compatibility. |

**Done evidence:** Generated controllers delegate through the gateway; Sample and Tenants UI hosts contain no generated or hand-written per-message MVC controllers; SignalR and optional DAPR notification paths prove scoped detail delivery and signal-only compatibility.

### 6.3 Release And Repository Reliability

**Description:** Maintainers can release reproducibly with correct dependency mode, aligned `references/` submodule layout, deterministic release gates, dedicated live-sidecar coverage, shared security workflows, and manifest-governed package output.

| ID | Requirement |
| --- | --- |
| FR17 | Live DAPR sidecar tests must be tagged and removed from the per-push release gate, then run in a dedicated integration workflow with sidecar warm-up and readiness retry. |
| FR18 | `DaprETagService` must allow an overridable actor request timeout while preserving the production default. |
| FR19 | Root-declared Git submodules must live under `references/`, and solution, project, documentation, Aspire metadata, and LLM instruction paths must resolve through the `references/` layout. |
| FR20 | The Aspire Keycloak resource must be named `security` while preserving Keycloak as the implementation technology and updating fixtures/resource lookups accordingly. |
| FR21 | Cross-repo Hexalith library dependencies must use Debug source project references when explicitly enabled and Release package references by default, with package versions pinned centrally. |
| FR22 | Release restore, build, test, pack, and semantic-release commands must assert package-reference mode and avoid packaging submodule projects. |
| FR25 | EventStore workflows must use shared Hexalith.Builds security gates through `@main`, keep third-party actions SHA-pinned through shared workflows, and define NuGet package publish scope in `tools/release-packages.json`. |

**Done evidence:** Release/package-mode validation cannot publish submodule packages; CI separates release-gate tests from live-sidecar tests; documentation and path scans no longer depend on root-level Hexalith submodule paths.

### 6.4 Event Correctness And Recovery

**Description:** Operators and consumers can trust persisted event metadata, idempotency, replay dispatch, append behavior, crash recovery, and global-position semantics under duplicate, concurrent, and failure conditions.

| ID | Requirement |
| --- | --- |
| FR23 | Persisted events must receive non-zero, actor-allocated global positions; CloudEvent ids must use the event `MessageId`; duplicate command replies must preserve the original command result fields. |
| FR24 | The global-position allocation strategy must be renegotiated toward sharding per tenant or domain, and the frozen global-ordering spec must be updated before implementation. |
| FR27 | Pipeline correctness remediation must make resume/idempotency matching use `MessageId`, `CausationId`, and `CommandType`; key command status/archive by message id; preserve retryability for transient failures; and validate tenant access before idempotency reads. |
| FR29 | Replay and dispatch remediation must make event apply-method resolution boundary-safe and ambiguity-detecting, and must use one shared `JsonSerializerOptions` path for command, rehydrate, project, and pub/sub payload serialization. |
| FR30 | Crash recovery remediation must detect events committed but not published and complete publication or drain/recover them without requiring resubmission with the same correlation id. |
| FR31 | Append durability remediation must start with a live-sidecar two-writer race test and DAPR conflict-exception spike before choosing an optimistic-concurrency fencing design. |

**Done evidence:** Tests prove CloudEvent id stability, duplicate result fidelity, stale pipeline rejection, replay ambiguity handling, stored-but-unpublished recovery, and real DAPR conflict behavior before append fencing design changes.

### 6.5 Security And Tenant Isolation

**Description:** Administrators, tenants, and domain services are protected by fail-closed authentication, scoped authorization, safe configuration, app-layer internal credentials, tenant-aware topology, and removal of trusted wire assertions.

| ID | Requirement |
| --- | --- |
| FR26 | Phase 0 architecture remediation must close immediate safe fixes: clear staged state on infrastructure failure, protect anonymous admin endpoints, strip committed admin secrets, enforce production auth guards, add tenant-filter parity, gate admin Swagger, require destructive CLI confirmation, use ULID-safe admin correlation middleware, and correct stale test-baseline documentation. |
| FR28 | Trust-boundary remediation must require app-layer credentials for internal, domain-service, projection-notification, and admin-computation endpoints, and must remove trust in wire-asserted administrator flags. |
| FR32 | Runtime topology remediation must make the AppHost-loaded DAPR pub/sub, ACL, and key-prefix posture match the posture asserted by tests and production deploy templates. |

**Done evidence:** Anonymous and cross-tenant admin access fails closed; production auth rejects insecure modes unless explicitly break-glassed; committed config contains no forgeable admin secrets; runtime topology tests inspect actual sidecar component paths and ACL posture.

### 6.6 Bounded Cost And Event Evolution

**Description:** Platform users can operate long-lived streams with bounded snapshot and projection cost, sequence-safe projection updates, event schema versioning/upcasting, validated event identity metadata, and cancellation-aware public seams.

| ID | Requirement |
| --- | --- |
| FR33 | Cost and evolution remediation must introduce folded snapshots, reduce projection replay cost, add projection sequence guards, support event schema versioning/upcasting, validate event metadata identity components, and add cancellation-token seams to published processing/query/projection interfaces. |

**Done evidence:** Stories 6.1, 6.3, and 6.5 produce approved specs at named paths before their dependent implementation stories start; dependent implementation stories verify the approved specs exist and that code conforms to them.

### 6.7 Operator Trust, Admin Honesty, And Future Capabilities

**Description:** Operators get explicit delivery semantics, bounded poison handling, attributable admin actions, honest unavailable-operation behavior, hardened deployment posture, meaningful higher-tier test evidence, and explicit backlog artifacts for deferred capabilities.

| ID | Requirement |
| --- | --- |
| FR34 | Delivery, admin, and deployment remediation must document at-least-once unordered delivery, add poison/dead-letter handling, bound in-memory deduplication, normalize admin claims, audit every state-mutating admin action, hide deferred admin operations, add secret-store-backed configuration, add readiness/app-health checks, and restore meaningful IntegrationTests CI coverage. |
| FR35 | Backlog capabilities must be tracked for GDPR aggregate erasure/tombstoning, Admin interactive OIDC login, an aggregate test kit, and REST generator hardening. |

**Done evidence:** Admin unavailable operations are hidden/disabled or return `501`; audit records remain support-safe; integration tests assert persisted state evidence; backlog artifacts exist for GDPR-1, IAM-1, KIT-1, and REST generator hardening.

## 7. Cross-Cutting Non-Functional Requirements

| ID | Requirement |
| --- | --- |
| NFR1 | Security must fail closed for public, internal, domain-service, projection-notification, and admin surfaces; no endpoint may rely only on network posture or caller-supplied admin flags. |
| NFR2 | Tenant isolation must be preserved across state keys, actor IDs, topics, admin queries, generated REST APIs, SignalR groups, and deployment configuration. |
| NFR3 | Production authentication must reject insecure symmetric-key mode unless explicitly break-glassed, require HTTPS metadata where appropriate, and pin accepted JWT algorithms. |
| NFR4 | Committed configuration must not contain forgeable administrator signing keys, credentials, bearer tokens, decoded JWT payloads, or other operational secrets. |
| NFR5 | SignalR detail metadata must remain bounded and metadata-only; framework logs must not expose metadata values above Debug level. |
| NFR6 | Event delivery semantics are at-least-once and unordered; subscribers must deduplicate by `MessageId` and order only where domain semantics make `SequenceNumber` meaningful. |
| NFR7 | Event persistence and command processing must avoid silent data loss: staged-state flushes, stale pipeline records, append races, and committed-but-unpublished events must be explicitly guarded or recovered. |
| NFR8 | Snapshot and projection behavior must have a bounded cost model as streams grow, must avoid unnecessary full-stream replay when already current, and must expose projection freshness/version evidence through platform query metadata when callers depend on current/stale decisions; freshness/version evidence is authoritative only for query responses whose route provenance is projection-backed, and handler-computed or unknown-provenance responses must not be presented as current or stale. |
| NFR9 | Release behavior must be reproducible and independent of local submodule checkout state; Release builds must use package references for external Hexalith libraries unless intentionally overridden. |
| NFR10 | CI/CD must separate deterministic release-gate tests from live-sidecar/integration tests while preserving live-sidecar coverage in a dedicated lane. |
| NFR11 | Package publishing must be manifest-driven and must not publish submodule packages or packages outside the EventStore release inventory. |
| NFR12 | Backward compatibility must be preserved for additive framework changes such as SignalR signal-only projection notifications and existing generic gateway APIs. |
| NFR13 | Generated code and source-generator packages must build cleanly under warnings-as-errors and must follow EventStore code style, nullable, ULID, and `ConfigureAwait(false)` rules. |
| NFR14 | Interactive UI hosts must not expose generated or hand-written per-message MVC command/query controllers; UI command/query flows consume client libraries. |
| NFR15 | Admin UX must not present deferred backup, restore, import, compaction, or other unavailable operations as functional; unavailable operations must be hidden/disabled or return `501`. |
| NFR16 | Integration and higher-tier tests must assert persisted state-store/read-model/end-state evidence, not only HTTP status codes or mock call counts. |
| NFR17 | Operational hardening must support secret stores, DAPR app health checks, readiness-tagged health checks, resiliency targets, immutable image tags, and documented crypto-shred boundaries. |
| NFR18 | AOT/trimming is explicitly not a target while reflection conventions remain load-bearing, and that constraint must be documented. |

## 8. Constraints And Guardrails

### 8.1 Repository And Build

- Use `Hexalith.EventStore.slnx` only for restore and build.
- Run unit tests by project; do not make solution-level `dotnet test` the EventStore default.
- Keep package versions centralized in `Directory.Packages.props`.
- Preserve Debug source-reference and Release package-reference behavior.
- Use .NET SDK container support, not Dockerfiles.
- Never initialize nested submodules; only root-declared submodules under `references/` are valid.

### 8.2 Identity And Authorization

- Message, correlation, causation, and EventStore aggregate identifiers must use ULID-safe handling where EventStore envelope semantics require sortable unique ids.
- `Guid.TryParse` is forbidden for `messageId`, `correlationId`, `aggregateId`, and `causationId`.
- Tenant access must be validated before status, idempotency, state, projection, admin, or generated REST data can disclose resource existence.
- Domain-service, internal, projection-notification, and admin-computation endpoints require app-layer credentials and must not trust caller-supplied admin flags.

### 8.3 UI Governance

- UI-facing work must use FrontComposer and Blazor Fluent UI V5 components.
- Prefer FrontComposer/Fluent components over raw CSS, raw HTML controls, JavaScript, or third-party controls.
- Do not redefine theme primitives.
- Multi-section page-like surfaces use `FluentAccordion` with the primary section expanded by default.
- UI states must remain support-safe and never render tokens, decoded JWT payloads, raw EventStore metadata, raw payloads, stack traces, cursor internals, or ETag internals.
- Sample UI command submission remains a demo of accepted submission, not proof of downstream completion.
- Tenants UI must preserve projection-confirmed success states.
- Admin UI must hide or disable deferred operations; any remaining endpoint returns `501`.

## 9. MVP Scope

### 9.1 In Scope

- All seven Phase 4 epics currently listed in `epics.md`.
- Domain-service SDK, read-model, cursor, projection, event-consumer, telemetry, health, Aspire, and packaging seams.
- REST generator contract and controller emission work with Sample and Tenants external API proofs.
- Release/repository reliability corrections including references layout, package-mode validation, live-sidecar re-tiering, shared workflow reuse, and manifest-driven package scope.
- Event correctness and recovery remediation including idempotency, replay dispatch, crash recovery, append evidence, and global-position sharding renegotiation.
- Security and tenant isolation remediation, including production auth guards, secret stripping, admin endpoint protections, trust-boundary closure, and topology parity.
- Cost/evolution work behind spec-first gates for folded snapshots, projection cost/sequence guards, and event versioning/upcasting.
- Operator/admin/deployment/test recovery work and explicit backlog artifacts for deferred capability tracks.

### 9.2 Out Of Scope For MVP

- GDPR aggregate erasure/tombstoning implementation; backlog artifact only.
- Admin interactive OIDC login implementation; backlog artifact only.
- Aggregate test kit implementation; backlog artifact only.
- REST generator hardening beyond the approved Epic 2 proof scope; backlog artifact only.
- AOT/trimming support while reflection conventions remain load-bearing.
- Moving generated REST controllers into interactive UI hosts.
- Treating HTTP `202`, SignalR notification, or command acceptance as projection-confirmed UI success.

## 10. Success Metrics

**Primary**

- **SM1:** Implementation readiness re-run no longer reports missing PRD as a blocker. Validates FR1-FR35 and NFR1-NFR18 traceability.
- **SM2:** Every FR1-FR35 maps to at least one epic and story in `epics.md`. Validates all functional requirements.
- **SM3:** High-risk NFRs NFR1-NFR4, NFR7, NFR10-NFR11, and NFR14-NFR17 map to concrete story coverage before Phase 4 implementation resumes. Validates security, release, UI, test, and operational hardening NFRs.

**Secondary**

- **SM4:** Oversized stories identified by the readiness report are split or explicitly accepted as coordinated slices with named owners and validation commands. Validates implementation readiness and reviewability.
- **SM5:** Required architecture and UX artifacts exist under `_bmad-output/planning-artifacts` and are referenced by the epic plan. Validates downstream usability.

**Counter-Metrics**

- **SM-C1:** Do not optimize for reducing the number of Phase 4 stories if doing so preserves unreviewable multi-concern stories. Counterbalances SM4.
- **SM-C2:** Do not count API smoke responses as integration evidence where persisted state-store/read-model/CloudEvent evidence is required. Counterbalances SM1 and SM3.
- **SM-C3:** Do not satisfy UI readiness by documenting intent only; UI stories still need component/governance evidence in `ux.md` and tests. Counterbalances SM5.

## 11. Traceability

### 11.1 FR To Epic Coverage

| FR | Primary epic coverage |
| --- | --- |
| FR1 | Epic 1 - Domain author self-service platform |
| FR2 | Epic 1 - Domain-service SDK host shape |
| FR3 | Epic 1 - Canonical domain-service DAPR endpoints |
| FR4 | Epic 1 - Domain query-handler seam and gateway routing |
| FR5 | Epic 1 - Generic persisted read-model store and write policy |
| FR6 | Epic 1 - Reusable protected query cursor codec |
| FR7 | Epic 1 - Generic projection-handler and domain-event consumer seams |
| FR8 | Epic 1 - Aspire, telemetry, and health-check platform extensions |
| FR9 | Epic 1 - Sample and Tenants adoption of platform SDK seams |
| FR10 | Epic 1 - DomainService and ServiceDefaults packaging |
| FR11 | Epic 2 - REST API source-generator contract seam |
| FR12 | Epic 2 - Generated typed REST controllers and generator tests |
| FR13 | Epic 2 - External API hosts for generated REST; UI uses client libraries |
| FR14 | Epic 2 - Sample contracts library and external Sample API proof |
| FR15 | Epic 2 - Tenants external API proof and UI client-library adoption |
| FR16 | Epic 2 - Metadata-rich, scope-aware projection-changed transport |
| FR17 | Epic 3 - Live-sidecar tests re-tiered off release gate |
| FR18 | Epic 3 - Overridable DaprETagService actor timeout |
| FR19 | Epic 3 - Submodules under references layout |
| FR20 | Epic 3 - Aspire Keycloak resource renamed to security |
| FR21 | Epic 3 - Debug source references and Release package references |
| FR22 | Epic 3 - Release commands assert package mode and avoid submodule packaging |
| FR23 | Epic 4 - Non-zero global positions, MessageId CloudEvent ids, duplicate result fidelity |
| FR24 | Epic 4 - Global-position sharding spec renegotiation |
| FR25 | Epic 3 - Shared Hexalith.Builds gates and manifest-driven package scope |
| FR26 | Epic 5 - Phase 0 security and safe-remediation fixes |
| FR27 | Epic 4 - Resume/idempotency integrity and command status re-keying |
| FR28 | Epic 5 - Defense-in-depth trust boundary |
| FR29 | Epic 4 - Replay and dispatch determinism |
| FR30 | Epic 4 - Crash recovery for committed-but-unpublished events |
| FR31 | Epic 4 - Append durability verify-first spike |
| FR32 | Epic 5 - Runtime topology and deployment posture parity |
| FR33 | Epic 6 - Bounded cost and event evolution |
| FR34 | Epic 7 - Delivery, admin, deploy, and IntegrationTests recovery |
| FR35 | Epic 7 - Backlog capability tracking |

### 11.2 High-Risk NFR Story Coverage

| NFR | Primary story coverage |
| --- | --- |
| NFR1 | 5.2, 5.3, 5.5, 7.2 |
| NFR2 | 2.4, 5.2, 5.5, 5.6 |
| NFR3 | 5.3 |
| NFR4 | 5.3, 7.3 |
| NFR7 | 4.1, 4.2, 4.4, 4.5, 5.1 |
| NFR10 | 3.1, 7.4 |
| NFR11 | 3.6 |
| NFR14 | 2.3, 2.4 |
| NFR15 | 7.2 |
| NFR16 | 7.4 |
| NFR17 | 5.6, 7.3 |

### 11.3 Required Follow-On Readiness Work

The PRD, architecture, UX, and epics artifacts now exist under `_bmad-output/planning-artifacts` and reference each other. The remaining readiness gate is verification: re-run implementation readiness after the story-quality corrections below are reviewed.

- `epics.md` contains coordinated-slice gates for Stories 1.3, 1.6, 2.4, 3.7, 5.6, 7.2, 7.3, and 7.4, including owners, review boundaries, and validation commands. Implementation story files must either split those stories or carry the coordinated-slice gate forward.
- Story 5.2 now requires concrete request-size limits: `1_048_576` bytes for representative admin JSON write/sandbox bodies and `10 * 1024 * 1024` bytes for `AdminBackupsController.ImportStream`, with bounded rejection tests and no upstream service invocation on excessive requests.
- Stories 6.1, 6.3, and 6.5 now name required spec output paths and approval evidence before Stories 6.2, 6.4, and 6.6 can start:
  - `_bmad-output/implementation-artifacts/spec-folded-snapshot.md`
  - `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md`
  - `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md`
- Story 7.5 is reclassified as a planning/backlog artifact story with exact deliverables:
  - `_bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md`
  - `_bmad-output/planning-artifacts/backlog/iam-1-admin-oidc-login.md`
  - `_bmad-output/planning-artifacts/backlog/kit-1-aggregate-test-kit.md`
  - `_bmad-output/planning-artifacts/backlog/rest-generator-hardening.md`

## 12. Open Questions

No PRD-level product-scope questions are open after the approved 2026-07-05 readiness-recovery proposal. The remaining open work is downstream planning and backlog refinement listed in section 11.3.

## 13. Assumptions Index

No inline `[ASSUMPTION]` tags are present in this PRD. The PRD follows the approved change proposal and preserves Phase 4 scope without reduction.
