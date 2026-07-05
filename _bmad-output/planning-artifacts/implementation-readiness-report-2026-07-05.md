---
project: eventstore
date: 2026-07-05
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
status: needs_work
assessor: Codex using bmad-check-implementation-readiness
includedFiles:
  prd:
    - _bmad-output/planning-artifacts/prd.md
  architecture:
    - _bmad-output/planning-artifacts/architecture.md
  epics:
    - _bmad-output/planning-artifacts/epics.md
  ux:
    - _bmad-output/planning-artifacts/ux.md
supportingArtifacts:
  ux:
    - _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md
    - _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md
archivedDuplicates:
  - _bmad-output/planning-artifacts/archive/2026-07-05-duplicate-document-exports/prd-eventstore-2026-07-05/prd.md
  - _bmad-output/planning-artifacts/archive/2026-07-05-duplicate-document-exports/architecture-eventstore-2026-07-05/ARCHITECTURE-SPINE.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-07-05
**Project:** eventstore

## Step 1: Document Discovery

### PRD Files Found

**Whole Documents:**
- `_bmad-output/planning-artifacts/prd.md` (33137 bytes, modified 2026-07-05 12:43)

**Sharded Documents:**
- None

### Architecture Files Found

**Whole Documents:**
- `_bmad-output/planning-artifacts/architecture.md` (19884 bytes, modified 2026-07-05 12:43)

**Sharded Documents:**
- None

### Epics & Stories Files Found

**Whole Documents:**
- `_bmad-output/planning-artifacts/epics.md` (85057 bytes, modified 2026-07-05 12:44)

**Sharded Documents:**
- None

### UX Design Files Found

**Whole Documents:**
- `_bmad-output/planning-artifacts/ux.md` (6702 bytes, modified 2026-07-05 10:17)

**Supporting Artifacts:**
- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/validation-report.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/mockups/`
- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/imports/`

### Discovery Issues

- Resolved duplicate PRD export by archiving `_bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/prd.md`.
- Resolved duplicate Architecture export by archiving `_bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/ARCHITECTURE-SPINE.md`.
- No merge was required for archived files because each was byte-identical to its retained root document.
- UX detailed artifacts were retained in place because `_bmad-output/planning-artifacts/ux.md` is a canonical handoff that intentionally links to those implementation contracts.

### Confirmed Assessment Inputs

- PRD: `_bmad-output/planning-artifacts/prd.md`
- Architecture: `_bmad-output/planning-artifacts/architecture.md`
- Epics and stories: `_bmad-output/planning-artifacts/epics.md`
- UX: `_bmad-output/planning-artifacts/ux.md`

## Step 2: PRD Analysis

### Functional Requirements

FR1: Domain modules built on Hexalith.EventStore must be domain-centric, containing domain code such as aggregates, commands, events, projections, query handlers, validators, and contracts, while platform boilerplate is supplied by EventStore libraries.

FR2: The platform must provide a domain-service SDK with `AddEventStoreDomainService`, `UseEventStoreDomainService`, and `MapEventStoreDomainService` so a domain service host can be reduced to the canonical SDK host shape.

FR3: The domain-service SDK must expose the canonical DAPR-facing endpoints `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata`.

FR4: The platform must provide a domain query-handler seam using `IDomainQueryHandler`, discovery, dispatch, operational metadata reporting, gateway-side query-type capture, handler-aware routing to domain `/query` endpoints, and end-to-end `QueryResponseMetadata` propagation for freshness, projection version, ETag, served-at, degraded/warning state, and paging evidence.

FR5: The platform must provide a generic persisted read-model store and write policy with optimistic-concurrency merge-on-write, multi-key/index support, DAPR implementation, and in-memory testing support.

FR6: The platform must provide a reusable DataProtection-backed query cursor codec with scope validation, payload limits, tamper/key-rotation handling, and caller-supplied purpose isolation.

FR7: The platform must provide a generic projection-handler seam for `/project` dispatch and a generic domain-event subscription/consumer pipeline with deduplication and endpoint mapping.

FR8: The platform must provide Aspire, telemetry, and health-check extensions for domain modules, including `AddEventStoreDomainModule`, convention telemetry, and DAPR state-store health checks.

FR9: The Sample domain and Tenants domain must adopt platform SDK seams so duplicated request routers, projection actors, cursor codecs, state-store plumbing, telemetry, health checks, and per-domain Aspire wiring are removed or reduced to domain-specific logic.

FR10: The EventStore package set must include the domain-service and service-default packages as publishable packages, and release packaging must publish only the manifest-governed EventStore package set.

FR11: The platform must provide a REST API source-generator contract seam with `ICommandContract`, `IQueryContract`, optional `RestRouteAttribute`, and assembly-level `RestApiAttribute`.

FR12: The REST API generator must discover command/query contracts and emit typed, OpenAPI-visible controllers that delegate to `IEventStoreGatewayClient`, forward canonical query metadata headers when supplied by the gateway, and include tests covering discovery, routing conventions, diagnostics, generated output, query metadata headers, `304`, and safe problem-detail behavior.

FR13: Generated REST controllers must live in dedicated external-facing API hosts, not interactive UI hosts; interactive UI hosts must consume EventStore client libraries directly.

FR14: The Sample proof must introduce a contracts-only Sample contracts library and an external Sample API host, move shared contracts there, and prove generated query and command controllers through that external API host.

FR15: The Tenants proof must move generated Tenants controllers to an external Tenants API host, while Tenants UI consumes client libraries and no longer hosts hand-written per-message controllers; any Tenants freshness, projection-version, ETag, or paging evidence shown by generated APIs or UI must come from the platform query metadata path.

FR16: The projection-changed transport must add an additive metadata-rich detail path with optional group scope, bounded metadata, scoped SignalR groups, DAPR notification support where needed, and preserved signal-only compatibility.

FR17: Live DAPR sidecar tests must be tagged and removed from the per-push release gate, then run in a dedicated integration workflow with sidecar warm-up and readiness retry.

FR18: `DaprETagService` must allow an overridable actor request timeout while preserving the production default.

FR19: Root-declared Git submodules must live under `references/`, and solution, project, documentation, Aspire metadata, and LLM instruction paths must resolve through the `references/` layout.

FR20: The Aspire Keycloak resource must be named `security` while preserving Keycloak as the implementation technology and updating fixtures/resource lookups accordingly.

FR21: Cross-repo Hexalith library dependencies must use Debug source project references when explicitly enabled and Release package references by default, with package versions pinned centrally.

FR22: Release restore, build, test, pack, and semantic-release commands must assert package-reference mode and avoid packaging submodule projects.

FR23: Persisted events must receive non-zero, actor-allocated global positions; CloudEvent ids must use the event `MessageId`; duplicate command replies must preserve the original command result fields.

FR24: The global-position allocation strategy must be renegotiated toward sharding per tenant or domain, and the frozen global-ordering spec must be updated before implementation.

FR25: EventStore workflows must use shared Hexalith.Builds security gates through `@main`, keep third-party actions SHA-pinned through shared workflows, and define NuGet package publish scope in `tools/release-packages.json`.

FR26: Phase 0 architecture remediation must close immediate safe fixes: clear staged state on infrastructure failure, protect anonymous admin endpoints, strip committed admin secrets, enforce production auth guards, add tenant-filter parity, gate admin Swagger, require destructive CLI confirmation, use ULID-safe admin correlation middleware, and correct stale test-baseline documentation.

FR27: Pipeline correctness remediation must make resume/idempotency matching use `MessageId`, `CausationId`, and `CommandType`; key command status/archive by message id; preserve retryability for transient failures; and validate tenant access before idempotency reads.

FR28: Trust-boundary remediation must require app-layer credentials for internal, domain-service, projection-notification, and admin-computation endpoints, and must remove trust in wire-asserted administrator flags.

FR29: Replay and dispatch remediation must make event apply-method resolution boundary-safe and ambiguity-detecting, and must use one shared `JsonSerializerOptions` path for command, rehydrate, project, and pub/sub payload serialization.

FR30: Crash recovery remediation must detect events committed but not published and complete publication or drain/recover them without requiring resubmission with the same correlation id.

FR31: Append durability remediation must start with a live-sidecar two-writer race test and DAPR conflict-exception spike before choosing an optimistic-concurrency fencing design.

FR32: Runtime topology remediation must make the AppHost-loaded DAPR pub/sub, ACL, and key-prefix posture match the posture asserted by tests and production deploy templates.

FR33: Cost and evolution remediation must introduce folded snapshots, reduce projection replay cost, add projection sequence guards, support event schema versioning/upcasting, validate event metadata identity components, and add cancellation-token seams to published processing/query/projection interfaces.

FR34: Delivery, admin, and deployment remediation must document at-least-once unordered delivery, add poison/dead-letter handling, bound in-memory deduplication, normalize admin claims, audit every state-mutating admin action, hide deferred admin operations, add secret-store-backed configuration, add readiness/app-health checks, and restore meaningful IntegrationTests CI coverage.

FR35: Backlog capabilities must be tracked for GDPR aggregate erasure/tombstoning, Admin interactive OIDC login, an aggregate test kit, and REST generator hardening.

**Total FRs:** 35

### Non-Functional Requirements

NFR1: Security must fail closed for public, internal, domain-service, projection-notification, and admin surfaces; no endpoint may rely only on network posture or caller-supplied admin flags.

NFR2: Tenant isolation must be preserved across state keys, actor IDs, topics, admin queries, generated REST APIs, SignalR groups, and deployment configuration.

NFR3: Production authentication must reject insecure symmetric-key mode unless explicitly break-glassed, require HTTPS metadata where appropriate, and pin accepted JWT algorithms.

NFR4: Committed configuration must not contain forgeable administrator signing keys, credentials, bearer tokens, decoded JWT payloads, or other operational secrets.

NFR5: SignalR detail metadata must remain bounded and metadata-only; framework logs must not expose metadata values above Debug level.

NFR6: Event delivery semantics are at-least-once and unordered; subscribers must deduplicate by `MessageId` and order only where domain semantics make `SequenceNumber` meaningful.

NFR7: Event persistence and command processing must avoid silent data loss: staged-state flushes, stale pipeline records, append races, and committed-but-unpublished events must be explicitly guarded or recovered.

NFR8: Snapshot and projection behavior must have a bounded cost model as streams grow, must avoid unnecessary full-stream replay when already current, and must expose projection freshness/version evidence through platform query metadata when callers depend on current/stale decisions.

NFR9: Release behavior must be reproducible and independent of local submodule checkout state; Release builds must use package references for external Hexalith libraries unless intentionally overridden.

NFR10: CI/CD must separate deterministic release-gate tests from live-sidecar/integration tests while preserving live-sidecar coverage in a dedicated lane.

NFR11: Package publishing must be manifest-driven and must not publish submodule packages or packages outside the EventStore release inventory.

NFR12: Backward compatibility must be preserved for additive framework changes such as SignalR signal-only projection notifications and existing generic gateway APIs.

NFR13: Generated code and source-generator packages must build cleanly under warnings-as-errors and must follow EventStore code style, nullable, ULID, and `ConfigureAwait(false)` rules.

NFR14: Interactive UI hosts must not expose generated or hand-written per-message MVC command/query controllers; UI command/query flows consume client libraries.

NFR15: Admin UX must not present deferred backup, restore, import, compaction, or other unavailable operations as functional; unavailable operations must be hidden/disabled or return `501`.

NFR16: Integration and higher-tier tests must assert persisted state-store/read-model/end-state evidence, not only HTTP status codes or mock call counts.

NFR17: Operational hardening must support secret stores, DAPR app health checks, readiness-tagged health checks, resiliency targets, immutable image tags, and documented crypto-shred boundaries.

NFR18: AOT/trimming is explicitly not a target while reflection conventions remain load-bearing, and that constraint must be documented.

**Total NFRs:** 18

### Additional Requirements

- Repository/build guardrails require `Hexalith.EventStore.slnx` for restore/build, per-project unit test execution, centralized package versions, preserved Debug source-reference and Release package-reference behavior, .NET SDK container support, and root-declared-only submodules under `references/`.
- Identity and authorization guardrails require ULID-safe handling for message/correlation/causation/EventStore aggregate identifiers, forbid `Guid.TryParse` for key identifiers, validate tenant access before disclosure, and require app-layer credentials for internal/domain/projection/admin-computation endpoints.
- UI governance requires FrontComposer and Blazor Fluent UI V5, no raw interactive-control substitution where Fluent/FrontComposer exists, no theme redefinition, support-safe states, projection-confirmed Sample/Tenants behavior, and hidden/disabled or `501` Admin deferred operations.
- MVP scope includes all seven Phase 4 epics, platform SDK seams, generated REST proofs, release/repository reliability corrections, event correctness/recovery, security/tenant isolation remediation, cost/evolution spec-first work, operator/admin/deployment/test recovery, and backlog artifacts.
- MVP explicitly excludes implementation of GDPR erasure/tombstoning, Admin interactive OIDC login, aggregate test kit, REST generator hardening beyond Epic 2 proof scope, AOT/trimming support, generated REST controllers in interactive UI hosts, and treating HTTP `202`, SignalR, or command acceptance as projection-confirmed success.
- Success metrics require readiness rerun to close missing-PRD blocker, every FR1-FR35 to map to epics/stories, high-risk NFRs to map to concrete story coverage, oversized stories to be split or explicitly accepted, and required architecture/UX artifacts to exist and be referenced.
- Required follow-on readiness work remains: architecture artifact, UX artifact, epics references, story splits or coordinated-slice acceptance, Story 5.2 request-size tightening, Story 6.1/6.3/6.5 spec output paths and approval evidence, and Story 7.5 backlog/planning reclassification or exact artifact deliverables.

### PRD Completeness Assessment

The PRD is sufficiently complete for traceability analysis: it defines purpose, vision, target users, product concerns, 35 functional requirements, 18 non-functional requirements, constraints, scope, success metrics, traceability tables, follow-on readiness work, open questions, and assumptions. No PRD-level product-scope questions are open.

Planning-quality note: the FR table groups some requirements by product area rather than strict numeric order, but all FR1-FR35 identifiers are present exactly once as requirement definitions. Downstream validation should verify every FR and high-risk NFR mapping against `epics.md` rather than relying only on the PRD's own traceability table.

## Step 3: Epic Coverage Validation

### Epic FR Coverage Extracted

- FR1, FR2, FR3: Story 1.1 - Canonical Domain-Service SDK Host
- FR4: Story 1.2 - Domain Query Handler Routing; also Story 7.6 - Query Metadata Propagation Contract And Gateway Evidence
- FR5, FR6: Story 1.3 - Generic Read Models And Query Cursors; also Story 7.6
- FR7: Story 1.4 - Projection And Domain Event Consumer Seams
- FR8: Story 1.5 - Domain Module Hosting Observability
- FR9: Story 1.6 - Sample And Tenants Domain-Centric Adoption
- FR10: Story 1.7 - DomainService Packaging And Guardrails
- FR11: Story 2.1 - REST Contract Seam For Command And Query Messages
- FR12: Story 2.2 - REST API Generator Discovery And Controller Emission; also Story 7.6
- FR13, FR14: Story 2.3 - Sample External API Host Proof
- FR15: Story 2.4 - Tenants External API Host Adoption; also Story 7.6
- FR16: Story 2.5 - Scoped Metadata-Rich Projection Notifications
- FR17: Story 3.1 - Re-Tier Live-Sidecar Tests From Release Gate
- FR18: Story 3.2 - Harden DAPR ETag Timeout For Integration Conditions
- FR19: Story 3.3 - References-Based Submodule Layout
- FR20: Story 3.4 - Aspire Security Resource Naming
- FR21: Story 3.5 - Debug Source References And Release Package References
- FR22: Story 3.6 - Manifest-Driven Release Packaging
- FR23: Story 4.1 - Event Identity And Duplicate Result Fidelity
- FR24: Story 4.6 - Global Position Sharding Spec Renegotiation
- FR25: Story 3.7 - Shared CI/CD Security Gates And Supply-Chain Backlog
- FR26: Stories 5.1, 5.2, 5.3, and 5.4
- FR27: Story 4.2 - Resume And Idempotency Integrity
- FR28: Story 5.5 - Internal And Domain-Service Trust Boundary
- FR29: Story 4.3 - Deterministic Replay Dispatch And Serialization
- FR30: Story 4.4 - Committed Event Publication Recovery
- FR31: Story 4.5 - Append Durability Race Evidence
- FR32: Story 5.6 - Runtime Topology And Deploy Parity
- FR33: Stories 6.1 through 6.6
- FR34: Stories 7.1 through 7.4; also Story 7.6
- FR35: Story 7.5 - Track Future Capability Backlog

**Total FRs in story coverage:** 35

### Coverage Matrix

| FR Number | PRD Requirement Summary | Epic/Story Coverage | Status |
| --- | --- | --- | --- |
| FR1 | Domain modules remain domain-centric while platform boilerplate is supplied by EventStore libraries. | Epic 1 / Story 1.1 | Covered |
| FR2 | Provide domain-service SDK host APIs for canonical domain service host shape. | Epic 1 / Story 1.1 | Covered |
| FR3 | Expose canonical domain-service DAPR endpoints. | Epic 1 / Story 1.1 | Covered |
| FR4 | Provide domain query-handler seam, handler-aware routing, and metadata propagation. | Epic 1 / Story 1.2; Epic 7 / Story 7.6 | Covered |
| FR5 | Provide generic persisted read-model store and write policy. | Epic 1 / Story 1.3; Epic 7 / Story 7.6 | Covered |
| FR6 | Provide reusable protected query cursor codec. | Epic 1 / Story 1.3; Epic 7 / Story 7.6 | Covered |
| FR7 | Provide generic projection-handler and domain-event consumer seams. | Epic 1 / Story 1.4 | Covered |
| FR8 | Provide Aspire, telemetry, and health-check extensions for domain modules. | Epic 1 / Story 1.5 | Covered |
| FR9 | Sample and Tenants adopt platform SDK seams and remove duplicate infrastructure. | Epic 1 / Story 1.6 | Covered |
| FR10 | Include DomainService and ServiceDefaults in manifest-governed publishable package set. | Epic 1 / Story 1.7 | Covered |
| FR11 | Provide REST API source-generator contract seam. | Epic 2 / Story 2.1 | Covered |
| FR12 | Generator emits typed controllers delegating through gateway and forwarding metadata. | Epic 2 / Story 2.2; Epic 7 / Story 7.6 | Covered |
| FR13 | Generated REST controllers live in external API hosts, not interactive UI hosts. | Epic 2 / Story 2.3 | Covered |
| FR14 | Sample contracts library and external Sample API host prove generated endpoints. | Epic 2 / Story 2.3 | Covered |
| FR15 | Tenants generated API moves external while UI consumes client libraries and metadata path. | Epic 2 / Story 2.4; Epic 7 / Story 7.6 | Covered |
| FR16 | Add scoped metadata-rich projection-changed transport while preserving compatibility. | Epic 2 / Story 2.5 | Covered |
| FR17 | Re-tier live DAPR sidecar tests into dedicated integration workflow. | Epic 3 / Story 3.1 | Covered |
| FR18 | Make `DaprETagService` actor request timeout overridable. | Epic 3 / Story 3.2 | Covered |
| FR19 | Move root-declared submodules under `references/` and update path resolution. | Epic 3 / Story 3.3 | Covered |
| FR20 | Rename Aspire Keycloak resource to `security`. | Epic 3 / Story 3.4 | Covered |
| FR21 | Preserve Debug source references and Release package references for Hexalith dependencies. | Epic 3 / Story 3.5 | Covered |
| FR22 | Release commands assert package-reference mode and avoid submodule packaging. | Epic 3 / Story 3.6 | Covered |
| FR23 | Non-zero global positions, CloudEvent `MessageId`, and duplicate result fidelity. | Epic 4 / Story 4.1 | Covered |
| FR24 | Renegotiate global-position sharding strategy and frozen spec before implementation. | Epic 4 / Story 4.6 | Covered |
| FR25 | Use shared Hexalith.Builds gates and manifest-driven package publish scope. | Epic 3 / Story 3.7 | Covered |
| FR26 | Phase 0 safe fixes for staged state, admin endpoints, secrets, auth guards, tenant filters, Swagger, CLI, ULIDs, docs. | Epic 5 / Stories 5.1-5.4 | Covered |
| FR27 | Resume/idempotency matching, message-id status keying, retryability, tenant validation. | Epic 4 / Story 4.2 | Covered |
| FR28 | App-layer credentials for internal/domain/projection/admin endpoints and removal of wire admin trust. | Epic 5 / Story 5.5 | Covered |
| FR29 | Boundary-safe replay dispatch and shared serializer options. | Epic 4 / Story 4.3 | Covered |
| FR30 | Recover committed-but-unpublished events without same-correlation resubmission. | Epic 4 / Story 4.4 | Covered |
| FR31 | Verify real DAPR append conflict behavior before fencing design. | Epic 4 / Story 4.5 | Covered |
| FR32 | Align runtime DAPR topology, ACL, pub/sub, and key-prefix posture. | Epic 5 / Story 5.6 | Covered |
| FR33 | Folded snapshots, projection cost/sequence guards, event versioning/upcasting, identity validation, cancellation seams. | Epic 6 / Stories 6.1-6.6 | Covered |
| FR34 | Delivery semantics, poison handling, bounded dedup, admin audit/honesty, deploy hardening, integration evidence. | Epic 7 / Stories 7.1-7.4 and 7.6 | Covered |
| FR35 | Track GDPR erasure, Admin OIDC, aggregate test kit, and REST generator hardening backlog. | Epic 7 / Story 7.5 | Covered |

### Missing Requirements

No PRD functional requirements are missing from epic/story coverage.

### Coverage Statistics

- Total PRD FRs: 35
- FRs covered in epics/stories: 35
- Coverage percentage: 100%
- FRs claimed in epics but not present in PRD: 0

### Coverage Notes

- Story 7.6 adds cross-cutting coverage for FR4, FR5, FR6, FR12, FR15, and FR34, and also lists NFR coverage. The NFR entries are not counted as extra FRs.
- `epics.md` contained stale overview text saying no standalone PRD, architecture, or UX contract was present in the selected planning-artifacts folder. This was corrected during the readiness pass so the epic plan now references `prd.md`, `architecture.md`, and `ux.md` directly.

## Step 4: UX Alignment Assessment

### UX Document Status

Found.

- Primary UX handoff: `_bmad-output/planning-artifacts/ux.md`
- Detailed visual contract: `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md`
- Detailed experience contract: `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`
- Validation and visual references: `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/validation-report.md`, `mockups/`, and `imports/`

### UX To PRD Alignment

- PRD FR13 and NFR14 require interactive UI hosts to consume client libraries and avoid generated or hand-written per-message MVC controllers. UX Foundation, Source Traceability, and Sample/Tenants flows enforce the same boundary.
- PRD FR15 requires Tenants UI to use client libraries and preserve query metadata/freshness evidence. UX Flow 6 and Projection freshness indicator rules align with that requirement.
- PRD FR34 and NFR15 require honest deferred/unavailable admin operations. UX Deferred & Backlog IA, State Patterns, Flow 4, and Support-Safe Operations require hidden/disabled unavailable operations or server `501`.
- PRD UI governance requires FrontComposer and Blazor Fluent UI V5, no theme redefinition, support-safe states, Sample accepted-submission behavior, Tenants projection-confirmed success, and admin deferred-operation honesty. UX `ux.md`, `DESIGN.md`, and `EXPERIENCE.md` cover each rule.
- PRD success/counter-metrics reject HTTP `202`, SignalR, or command acceptance as proof of UI success. UX State Patterns and key flows consistently model accepted/evidence-pending separately from projection-confirmed success.

### UX To Architecture Alignment

- Architecture AD-4 supports the UX/client-host boundary by requiring generated REST controllers to live only in dedicated external API hosts.
- Architecture AD-8 supports the UX state model by treating SignalR and projection notifications as freshness signals only; projection/read-model evidence confirms visible success.
- Architecture AD-10 supports fail-closed, support-safe, and honest unavailable-operation UI behavior.
- Architecture AD-14 supports UX freshness, projection-version, ETag, stale/current/unknown, and paging evidence through platform `QueryResponseMetadata` and gateway-owned headers.
- Architecture Consistency Conventions explicitly require module UI to use FrontComposer and Fluent UI Blazor V5, projection-confirmed success, support-safe rendering, accessibility, and localization.
- Architecture intentionally leaves detailed journeys, screen states, component-level patterns, accessibility, and localization evidence to `ux.md`; that is acceptable because the UX artifact now exists and is referenced by `epics.md`.

### Alignment Issues

No blocking UX/PRD/Architecture alignment gaps remain after correcting stale `epics.md` references to the missing PRD/architecture/UX artifacts.

### Warnings

- UX has non-blocking assumptions that implementation must close or explicitly accept: EventStore UI service host integration point, final dashboard tab names, and desktop-first ergonomics for complex mutations.
- UI stories must still produce component/governance evidence. Documentation alone is insufficient for PRD counter-metric SM-C3.
- Story 7.6 is important for UI correctness because UX and architecture both rely on platform-owned query metadata for freshness, projection-confirmed success, stale/current/unknown state, and generated API headers.
- The UX detailed artifact folder is supporting contract material, not a duplicate to archive. Keep `DESIGN.md`, `EXPERIENCE.md`, mockups, imports, and validation reports in place while using `ux.md` as the primary readiness input.

## Step 5: Epic Quality Review

### Epic Structure Validation

| Epic | User Value Assessment | Independence Assessment | Result |
| --- | --- | --- | --- |
| Epic 1 - Domain Author Self-Service Platform | Clear developer-platform value for domain authors. | Can stand alone as platform SDK foundation. | Pass |
| Epic 2 - External Integration Surfaces | Clear value for external API developers and UI maintainers. | Uses Epic 1/client/gateway seams but does not depend on later epics. | Pass |
| Epic 3 - Release And Repository Reliability | Clear release-maintainer value despite technical implementation. | Independent of later hardening epics. | Pass |
| Epic 4 - Event Correctness And Recovery | Clear operator/consumer trust value. | Independent of later security/deploy epics. | Pass |
| Epic 5 - Security And Tenant Isolation | Clear admin/tenant/operator security value. | Can run after existing platform state; no future-epic dependency. | Pass |
| Epic 6 - Bounded Cost And Event Evolution | Clear long-lived-stream/operator value, with explicit spec-first sequencing. | Depends on its own earlier spec stories only. | Pass with sequencing controls |
| Epic 7 - Operator Trust, Admin Honesty, And Future Capabilities | Clear operator and product-owner value. | Mostly independent; Story 7.6 must precede UI/generated API work that relies on metadata evidence. | Pass with split/timing concerns |

### Critical Violations

No critical best-practice violations were found. No epic requires a future epic to function, and every PRD FR has a story-level implementation path.

### Major Issues

1. Oversized stories remain in the plan.

The PRD itself calls out required story splits or coordinated-slice acceptance for Stories 1.3, 1.6, 2.4, 3.7, 5.6, 7.2, 7.3, and 7.4. The quality review confirms those are still broad, multi-concern implementation slices:

- Story 1.3 combines read-model store, optimistic-concurrency policy, testing fake, cursor codec, paging metadata, and Tenants migration.
- Story 1.6 combines Sample adoption, Tenants adoption, governance tests, and DAPR/Aspire validation.
- Story 2.4 combines Tenants contract changes, generated external API host, UI client-library migration, query metadata evidence, submodule tests, and CI blocker handling.
- Story 3.7 combines shared workflow callers, action pinning policy, npm install/cache behavior, signature/provenance blockers, NuGet publishing secret posture, and supply-chain backlog.
- Story 5.6 combines AppHost component paths, production component templates, topology tests, ACL posture, route/topic wiring, and deployment documentation.
- Story 7.2 combines claims normalization, audit records, deferred-operation UI/server behavior, and shared typed client planning.
- Story 7.3 combines secret stores, readiness/app-health, resiliency targets, and immutable image tag posture.
- Story 7.4 combines CI lane recovery, persisted evidence assertions, integration helper extraction, fake-test relabeling, and perf/advisory lane cleanup.

Recommendation: split these before implementation or add explicit coordinated-slice acceptance with named owners, validation commands, and review boundaries.

2. Spec-gated stories lack exact spec output paths in the story text.

Stories 6.1, 6.3, and 6.5 require approved specs before implementation, but the story acceptance criteria do not name exact artifact paths for the folded snapshot spec, projection delivery/sequence guard spec, and event versioning/upcasting/cancellation spec. The PRD explicitly requires named spec output paths and approval evidence before dependent implementation stories start.

Recommendation: add exact paths and an approval/evidence condition to Stories 6.1, 6.3, and 6.5; require Stories 6.2, 6.4, and 6.6 to verify those approved paths before code changes.

3. Story 5.2 request-size acceptance is too weak.

Story 5.2 says oversized admin requests fail safely and that limits are "tested or documented." The PRD requires tightening this acceptance. "Tested or documented" is not a reliable implementation gate for a security boundary.

Recommendation: replace with a concrete request-size limit, the affected endpoints/body types, expected response shape, and required tests. Documentation-only should require an explicit exception and owner.

4. Story 7.5 is backlog/planning work but still reads like an implementation story.

Story 7.5 covers GDPR-1, IAM-1, KIT-1, REST generator hardening, deferred-work ingestion, and query metadata scheduling. That is a backlog curation bundle, not one independently completable implementation story. It also lacks exact artifact paths for GDPR-1, IAM-1, and KIT-1.

Recommendation: either reclassify Story 7.5 as backlog/planning work with exact deliverable paths, or split it into separate backlog-artifact stories. The stale "create or schedule Story 7.6" wording was corrected during this readiness pass because Story 7.6 now exists.

### Minor Concerns

- Story 7.6 is cohesive around query metadata propagation, but it is cross-cutting across platform result types, HTTP metadata, generated APIs, client results, policy enforcement, and persisted evidence. If implementation estimate or review risk is high, split it into core metadata propagation, HTTP/generated API forwarding, and policy/test evidence slices.
- Several acceptance criteria use broad validation phrases such as "relevant test projects run", "as intended", or "where available." These are acceptable at epic-planning altitude but should be made exact in implementation story files.
- Epic 6 implementation stories correctly depend on earlier spec stories inside the same epic; this is not a forward dependency violation, but it must be enforced in sprint sequencing.

### Dependency Analysis

- No Epic N requires Epic N+1 to function.
- No story requires a future story as an implementation blocker after the Story 7.5 reference to Story 7.6 was corrected.
- The only intentional dependencies are backward dependencies within Epic 6: implementation stories depend on prior spec stories.
- No database/table-front-loading issue applies. This is a DAPR/event-store brownfield platform, and state/persistence changes are introduced in the stories that need them.
- No greenfield starter-template story is required. The PRD states no starter template is mandated.

### Best Practices Compliance Summary

- Epic user value: Pass
- Epic independence: Pass
- FR traceability: Pass
- Story sizing: Fails for the oversized stories listed above
- Forward dependencies: Pass after Story 7.5 wording correction
- Acceptance criteria specificity: Needs remediation for request-size limits, spec output paths, and broad validation phrases

## Step 6: Summary and Recommendations

### Overall Readiness Status

NEEDS WORK.

The planning baseline is materially improved and no longer blocked by missing PRD/architecture/UX artifacts or unresolved duplicate document formats. PRD FR coverage is complete: all 35 PRD functional requirements have story-level coverage in `epics.md`.

The remaining blocker is implementation quality, not requirements coverage. Several stories are still too large, some spec-gated work lacks exact output paths and approval gates, and a security-facing acceptance criterion remains too loose.

### Critical Issues Requiring Immediate Action

No critical issue blocks traceability. The following major issues should be addressed before broad Phase 4 implementation begins:

1. Split or explicitly coordinate oversized stories: 1.3, 1.6, 2.4, 3.7, 5.6, 7.2, 7.3, and 7.4.
2. Add exact spec artifact paths and approval evidence to Stories 6.1, 6.3, and 6.5; require Stories 6.2, 6.4, and 6.6 to verify those specs before implementation.
3. Tighten Story 5.2 request-size acceptance with concrete limits, response behavior, and tests.
4. Reclassify or split Story 7.5 into exact backlog-artifact deliverables for GDPR-1, IAM-1, KIT-1, and REST generator hardening.
5. Review broad validation wording such as "relevant tests", "as intended", and "where available" when creating implementation story files.

### Completed Corrections During This Assessment

- Archived exact duplicate PRD and Architecture document exports under `_bmad-output/planning-artifacts/archive/2026-07-05-duplicate-document-exports/`.
- Kept UX detailed artifacts in place because they are supporting contracts linked by canonical `ux.md`, not duplicate readiness inputs.
- Updated `epics.md` to reference `prd.md`, `architecture.md`, and `ux.md` directly.
- Corrected stale Story 7.5 wording that still said Story 7.6 had to be created or scheduled even though Story 7.6 now exists.

### Recommended Next Steps

1. Run story splitting against the eight oversized stories and update `epics.md` with smaller implementation slices or explicit coordinated-slice acceptance.
2. Patch Epic 6 story criteria with named spec paths and approval checks.
3. Patch Story 5.2 and Story 7.5 acceptance criteria so each has objective implementation evidence.
4. Re-run this readiness check after the story-quality edits.
5. After readiness returns READY, create implementation story files from the remediated epic plan.

### Final Note

This assessment identified 7 active issues requiring attention across story sizing, spec-gate readiness, and acceptance-criteria precision. It also fixed 4 document hygiene issues during the run. Proceeding as-is would preserve traceability, but it would hand implementers stories that are too broad for reliable review and validation.
