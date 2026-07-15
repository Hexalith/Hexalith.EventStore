---
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
inputDocuments:
  prd:
    - _bmad-output/planning-artifacts/prd.md
  architecture:
    - _bmad-output/planning-artifacts/architecture.md
  epics:
    - _bmad-output/planning-artifacts/epics.md
  ux:
    - _bmad-output/planning-artifacts/ux.md
    - _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/index.md
    - _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md
    - _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md
excludedDocuments:
  - path: _bmad-output/planning-artifacts/archive/
    reason: Archived duplicate and superseded document exports are not active planning sources.
---

# Implementation Readiness Assessment Report

**Date:** 2026-07-15
**Project:** eventstore

## Document Discovery

### PRD Files Found

**Whole Documents:**

- `prd.md` (37,979 bytes; modified 2026-07-13)

**Sharded Documents:** None.

### Architecture Files Found

**Whole Documents:**

- `architecture.md` (34,021 bytes; modified 2026-07-11)

**Sharded Documents:** None.

### Epics and Stories Files Found

**Whole Documents:**

- `epics.md` (135,841 bytes; modified 2026-07-13)

**Sharded Documents:** None.

### UX Design Files Found

**Top-Level Handoff:**

- `ux.md` (1,101 bytes; modified 2026-07-11) — included as the canonical locator for the detailed sharded UX source.

**Sharded Documents:**

- Folder: `ux-designs/ux-eventstore-2026-07-05/`
  - `index.md` (1,348 bytes; modified 2026-07-09)
  - `DESIGN.md` (15,081 bytes; modified 2026-07-11)
  - `EXPERIENCE.md` (26,561 bytes; modified 2026-07-11)
  - Supporting review, validation, mockup, and image artifacts

### Discovery Resolution

All required document categories are present. The top-level `ux.md` handoff and indexed sharded UX set form one composed authoritative UX source. Files under `archive/` are explicitly excluded, preserving planning history without introducing source ambiguity.

## PRD Analysis

### Functional Requirements

FR1: Domain modules built on Hexalith.EventStore must be domain-centric, containing domain code such as aggregates, commands, events, projections, query handlers, validators, and contracts, while platform boilerplate is supplied by EventStore libraries.

FR2: The platform must provide a domain-service SDK with `AddEventStoreDomainService`, `UseEventStoreDomainService`, and `MapEventStoreDomainService` so a domain service host can be reduced to the canonical SDK host shape.

FR3: The domain-service SDK must expose the canonical DAPR-facing endpoints `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata`.

FR4: The platform must provide a domain query-handler seam using `IDomainQueryHandler`, discovery, dispatch, operational metadata reporting, gateway-side query-type capture, handler-aware routing to domain `/query` endpoints, and end-to-end `QueryResponseMetadata` propagation for freshness, projection version, ETag, served-at, degraded/warning state, and paging evidence, carrying an explicit query-response provenance classification (projection-backed, handler-computed, or unknown) that governs whether that evidence is projection-backed. Projection-backed responses must additionally preserve a lossless lifecycle representation or owner-approved mapping for `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`; consumers must not infer lifecycle from ETags or claim projection-confirmed success without projection-backed provenance.

FR5: The platform must provide generic persisted read-model lifecycle and write contracts with ETag-aware reads/writes, coordinated read-model and sequence/checkpoint erasure, and detail/index batch writes or an approved equivalent. Batch behavior must define partial-failure recovery, idempotency, ordering, flush completion, optimistic concurrency, DAPR behavior, and deterministic in-memory testing semantics.

FR6: The platform must provide a reusable DataProtection-backed query cursor codec with scope validation, payload limits, tamper/key-rotation handling, and caller-supplied purpose isolation.

FR7: The platform must provide an asynchronous, cancellation-aware projection-handler seam supporting multiple named projections per domain and coordinated detail/index persistence, plus a generic domain-event subscription/consumer pipeline with deduplication and endpoint mapping. Projection delivery must tolerate duplicate and out-of-order events through the actual handler path, and full rebuilds must remain correct across paging boundaries.

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

FR36: Before a consuming module deletes local projection/query infrastructure, EventStore must produce an owner-reviewed parity packet proving every required capability through production paths, record an approved runtime SHA, and require the consumer's checked-out EventStore SHA to match that approval.

**Total FRs: 36**

### Non-Functional Requirements

NFR1: Security must fail closed for public, internal, domain-service, projection-notification, and admin surfaces; no endpoint may rely only on network posture or caller-supplied admin flags. The only anonymous exception is the health/liveness/readiness probe endpoints (`/health`, `/alive`, `/ready`), which are explicitly pinned `AllowAnonymous` and support-safe (AD-16); the fail-closed default is never weakened to reach probes.

NFR2: Tenant isolation must be preserved across state keys, actor IDs, topics, admin queries, generated REST APIs, SignalR groups, and deployment configuration.

NFR3: Production authentication must reject insecure symmetric-key mode unless explicitly break-glassed, require HTTPS metadata where appropriate, and pin accepted JWT algorithms.

NFR4: Committed configuration must not contain forgeable administrator signing keys, credentials, bearer tokens, decoded JWT payloads, or other operational secrets.

NFR5: SignalR detail metadata must remain bounded and metadata-only; framework logs must not expose metadata values above Debug level.

NFR6: Event delivery semantics are at-least-once and unordered; subscribers must deduplicate by `MessageId` and order only where domain semantics make `SequenceNumber` meaningful. Duplicate and out-of-order safety must be enforced and proven through the production projection dispatcher, handler, persistence, marker, and checkpoint path rather than only aggregate replay or transport-level tests.

NFR7: Event persistence and command processing must avoid silent data loss: staged-state flushes, stale pipeline records, append races, and committed-but-unpublished events must be explicitly guarded or recovered.

NFR8: Snapshot and projection behavior must have a bounded cost model as streams grow, must avoid unnecessary full-stream replay when already current, and must expose projection freshness/version evidence through platform query metadata when callers depend on lifecycle decisions; freshness/version evidence is authoritative only for query responses whose route provenance is projection-backed, and handler-computed or unknown-provenance responses must not be presented as authoritative lifecycle evidence. Paged rebuild output must equal canonical aggregate replay and must never overwrite a complete live model with page-only state.

NFR9: Release behavior must be reproducible and independent of local submodule checkout state; Release builds must use package references for external Hexalith libraries unless intentionally overridden.

NFR10: CI/CD must separate deterministic release-gate tests from live-sidecar/integration tests while preserving live-sidecar coverage in a dedicated lane.

NFR11: Package publishing must be manifest-driven and must not publish submodule packages or packages outside the EventStore release inventory.

NFR12: Backward compatibility must be preserved for additive framework changes such as SignalR signal-only projection notifications and existing generic gateway APIs.

NFR13: Generated code and source-generator packages must build cleanly under warnings-as-errors and must follow EventStore code style, nullable, ULID, and `ConfigureAwait(false)` rules.

NFR14: Interactive UI hosts must not expose generated or hand-written per-message MVC command/query controllers; UI command/query flows consume client libraries.

NFR15: Admin UX must not present deferred backup, restore, import, compaction, or other unavailable operations as functional; unavailable operations must be hidden/disabled or return `501`.

NFR16: Integration and higher-tier tests must assert persisted state-store/read-model/end-state evidence, not only HTTP status codes or mock call counts. Erasure, batch recovery, handler idempotency, and rebuild equivalence require persisted detail, index, marker, lifecycle, and checkpoint evidence through their production paths.

NFR17: Operational hardening must support secret stores, DAPR app health checks, readiness-tagged health checks, resiliency targets, immutable image tags, and documented crypto-shred boundaries.

NFR18: AOT/trimming is explicitly not a target while reflection conventions remain load-bearing, and that constraint must be documented.

**Total NFRs: 18**

### Additional Requirements

- Build and repository constraints require the `.slnx` solution, per-project tests, centralized package versions, Debug source/Release package dependency behavior, .NET SDK container support, and root-declared `references/` submodules only.
- Identity and authorization constraints require ULID-safe EventStore envelope identifiers, prohibit `Guid.TryParse` for protected identifiers, validate tenant access before resource disclosure, and require app-layer credentials at internal trust boundaries.
- UI governance requires FrontComposer and Fluent UI V5, prohibits theme redefinition and unsafe rendering, distinguishes accepted submission from projection-confirmed success, and requires deferred Admin operations to be hidden, disabled, or return `501`.
- Explicit MVP exclusions include full GDPR erasure, interactive Admin OIDC login, the aggregate test kit, extended REST-generator hardening, AOT/trimming, generated controllers in UI hosts, and treating acceptance signals as confirmed success.
- Stories 6.1, 6.3, and 6.5 must produce approved specification artifacts at the exact paths named by the PRD before dependent implementation stories begin.
- Projection/query parity remains gated on Stories 1.9-1.15, production-path evidence, explicit owner approval, and exact runtime-SHA matching before the consuming Parties work resumes.
- The PRD declares no open product-scope questions and contains no inline assumption tags.

### PRD Completeness Assessment

The PRD is final, authoritative, and unusually strong in requirement enumeration: it defines FR1-FR36 and NFR1-NFR18, scope, exclusions, success metrics, constraints, and its own preliminary traceability. No numbered requirement is missing from either sequence.

Two apparent clarity points were resolved during downstream validation:

1. The top-level `ux.md` is the canonical locator that explicitly delegates detailed authority to the indexed UX set; there is no competing UX specification.
2. The referenced Story 8.6 belongs to the external consuming Parties repository; EventStore remains a seven-epic plan with internal prerequisite coverage in Stories 1.9-1.15.

## Epic Coverage Validation

### Epic FR Coverage Extracted

- Epic 1 covers FR1-FR10 and FR36.
- Epic 2 covers FR11-FR16.
- Epic 3 covers FR17-FR22 and FR25.
- Epic 4 covers FR23, FR24, FR27, FR29, FR30, and FR31.
- Epic 5 covers FR26, FR28, and FR32.
- Epic 6 covers FR33.
- Epic 7 covers FR34 and FR35.

**Total distinct FRs in epics: 36**

### Coverage Matrix

The complete requirement text is preserved in the preceding PRD Analysis section; the requirement column below uses concise identifiers solely to keep the traceability matrix readable.

| FR Number | PRD Requirement | Epic and Story Coverage | Status |
| --- | --- | --- | --- |
| FR1 | Domain-centric domain modules | Epic 1, Story 1.1 | ✓ Covered |
| FR2 | Canonical domain-service SDK host shape | Epic 1, Story 1.1 | ✓ Covered |
| FR3 | Canonical DAPR-facing domain-service endpoints | Epic 1, Story 1.1 | ✓ Covered |
| FR4 | Query-handler seam, routing, metadata, provenance, and lifecycle | Epic 1, Stories 1.2, 1.8, 1.11; Epic 2, Story 2.8 | ✓ Covered |
| FR5 | Persisted read-model lifecycle, erasure, and coordinated writes | Epic 1, Stories 1.3, 1.8-1.10 | ✓ Covered |
| FR6 | Protected scoped query cursor codec | Epic 1, Stories 1.3 and 1.8 | ✓ Covered |
| FR7 | Async multi-projection and event-consumer seams | Epic 1, Stories 1.4, 1.8, 1.12-1.14 | ✓ Covered |
| FR8 | Domain-module Aspire, telemetry, and health extensions | Epic 1, Story 1.5 | ✓ Covered |
| FR9 | Sample and Tenants platform-seam adoption | Epic 1, Stories 1.6 and 1.8 | ✓ Covered |
| FR10 | DomainService, ServiceDefaults, and manifest packaging | Epic 1, Story 1.7 | ✓ Covered |
| FR11 | REST source-generator contract seam | Epic 2, Story 2.1 | ✓ Covered |
| FR12 | Generated controllers, metadata, diagnostics, and safe errors | Epic 2, Stories 2.2, 2.6, and 2.8 | ✓ Covered |
| FR13 | Generated controllers only in external API hosts | Epic 2, Stories 2.3 and 2.7 | ✓ Covered |
| FR14 | Sample contracts library and external API proof | Epic 2, Stories 2.3 and 2.7 | ✓ Covered |
| FR15 | Tenants external API and UI client-library adoption | Epic 2, Stories 2.4 and 2.8 | ✓ Covered |
| FR16 | Scoped metadata-rich projection notifications | Epic 2, Story 2.5 | ✓ Covered |
| FR17 | Dedicated live-sidecar integration lane | Epic 3, Stories 3.1 and 3.8 | ✓ Covered |
| FR18 | Overridable DAPR ETag actor timeout | Epic 3, Story 3.2 | ✓ Covered |
| FR19 | Root submodules under `references/` | Epic 3, Story 3.3 | ✓ Covered |
| FR20 | Aspire identity resource named `security` | Epic 3, Story 3.4 | ✓ Covered |
| FR21 | Debug source and Release package dependency modes | Epic 3, Story 3.5 | ✓ Covered |
| FR22 | Release commands assert package mode and package scope | Epic 3, Story 3.6 | ✓ Covered |
| FR23 | Global positions, CloudEvent identity, duplicate fidelity | Epic 4, Story 4.1 | ✓ Covered |
| FR24 | Global-position sharding specification | Epic 4, Story 4.6 | ✓ Covered |
| FR25 | Shared security workflows and package manifest | Epic 3, Story 3.7 | ✓ Covered |
| FR26 | Phase 0 immediate security and safety remediation | Epic 5, Stories 5.1-5.4; secondary hardening in Story 2.7 | ✓ Covered |
| FR27 | Resume/idempotency integrity and message-id status keying | Epic 4, Story 4.2 | ✓ Covered |
| FR28 | Internal/domain-service trust boundary | Epic 5, Story 5.5; secondary hardening in Story 2.7 | ✓ Covered |
| FR29 | Deterministic replay dispatch and shared serialization | Epic 4, Story 4.3 | ✓ Covered |
| FR30 | Recovery of committed-but-unpublished events | Epic 4, Story 4.4 | ✓ Covered |
| FR31 | Live-DAPR append race evidence before fencing | Epic 4, Story 4.5 | ✓ Covered |
| FR32 | Runtime topology and deployment posture parity | Epic 5, Story 5.6 | ✓ Covered |
| FR33 | Bounded snapshots/projections and event evolution | Epic 1, Story 1.14; Epic 6, Stories 6.1-6.6 | ✓ Covered |
| FR34 | Delivery, admin, deployment, and integration-test remediation | Epic 7, Stories 7.1-7.4; validation/provenance support in Stories 2.8 and 3.8 | ✓ Covered |
| FR35 | Explicit future-capability backlog artifacts | Epic 7, Story 7.5 | ✓ Covered |
| FR36 | Owner-approved parity packet and exact runtime pin | Epic 1, Stories 1.9-1.15 | ✓ Covered |

### Missing Requirements

No PRD functional requirement is missing from the epic/story plan. No epic-only FR identifier exists outside the PRD's FR1-FR36 range.

Story 8.6 is an external consuming-repository dependency referenced by the parity gate, not an eighth EventStore epic. EventStore-owned implementation coverage remains in Stories 1.9-1.15, resolving the PRD clarity point without adding an unmapped requirement.

### Coverage Statistics

- Total PRD FRs: 36
- FRs covered in epics: 36
- Missing PRD FRs: 0
- Extra epic FRs not present in the PRD: 0
- Coverage: 100%

## UX Alignment Assessment

### UX Document Status

**Found and final.** The UX contract is one composed source set:

- `ux.md` is the canonical top-level handoff and locator required by the PRD, architecture, and epics.
- `ux-designs/ux-eventstore-2026-07-05/index.md` is the indexed root of the detailed UX source.
- `DESIGN.md` owns visual identity, Fluent roles, tokens, and component visual rules.
- `EXPERIENCE.md` owns information architecture, behavior, states, interactions, accessibility, localization, and journeys.

#### Discovery Correction

The filename-only discovery step conservatively flagged `ux.md` and the indexed folder as duplicate formats. Content validation shows they are not competing specifications: `ux.md` explicitly delegates detailed authority to the sharded set. Both are therefore included as one canonical UX artifact. Only the explicitly superseded export under `archive/` remains excluded from assessment input.

### Legacy UX Merge Result

The current top-level `ux.md` and the actual superseded export `archive/ux-superseded-2026-07-05.md` were compared against the canonical `DESIGN.md` and `EXPERIENCE.md` contracts.

No missing legacy information required merging. The authoritative documents already preserve, with equal or greater detail:

- FrontComposer and Fluent UI V5 governance and the no-theme-redefinition rule;
- the single **Event Store Admin** module entry and dashboard-tab information architecture;
- accepted/evidence-pending versus projection-confirmed success;
- support-safe rendering, tenant isolation, and generated-REST/UI-host boundaries;
- deferred/unavailable operation behavior;
- WCAG 2.2 AA, keyboard, live-region, stable-selector, and localization evidence;
- `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, `LocalOnly`, and fail-safe `Unknown` lifecycle behavior.

The canonical UX source was intentionally left unchanged to avoid duplicate or stale wording.

### UX ↔ PRD Alignment

The UX source aligns strongly with the PRD:

- FR13, FR15, NFR14, and the external-host boundary are reflected in the client-library-only UI-host rules and Sample/Tenants journeys.
- FR34 and NFR15 are reflected in the honest deferred-operation states, disabled/hidden UI, and `501` server behavior.
- Projection-confirmed success, SignalR-as-nudge behavior, support-safe rendering, tenant-denial semantics, and admin auditability are carried into explicit states and flows.
- The UX covers all three UI-bearing product surfaces implied by the PRD: EventStore Admin, Sample UI, and Tenants UI.

No UX requirement was found that contradicts or materially expands the PRD scope.

### UX ↔ Architecture Alignment

Architecture support is strong across AD-4, AD-8, AD-10, AD-14, AD-15, AD-19, and AD-20:

- dedicated external API hosts and client-library UI hosts match the UX host boundary;
- projection/query provenance supports the UX `Current`/`Stale`/`Unknown` truth model;
- asynchronous multi-projection and replay-equivalence rules prevent partial/local evidence from appearing as confirmed success;
- fail-closed authorization, tenant isolation, opaque cursors/ETags, and support-safe evidence match the interaction rules;
- the stack and consistency conventions explicitly require FrontComposer and Fluent UI V5.

### Alignment Issues

1. **Future EventStore UI service ownership is underspecified.** UX says legacy `Admin.UI` is only source evidence and targets a consolidated EventStore UI service with one module entry and dashboard tabs. Architecture still names `Hexalith.EventStore.Admin.*`/`AdminUI` in the structural seed and topology without explicitly identifying whether that project evolves into the target service or a new host owns the migration. A UI implementation story should name the owning project, migration boundary, route/deep-link compatibility, and AppHost resource.
2. **No quantitative UI performance budget exists.** UX defines responsive breakpoints, skeleton behavior, dense grids, stale-data handling, and mobile triage, but neither UX nor architecture specifies measurable initial-load, interaction, grid, polling, or SignalR-refresh targets. This is a warning rather than a scope contradiction; add story-level performance acceptance criteria if UI responsiveness is release-critical.

### Warnings

- FrontComposer is mandated in architecture conventions and UX, but it is not represented explicitly in the architecture dependency diagram or stack table. Implementation stories should pin the intended FrontComposer package/source boundary to prevent ad hoc UI composition.
- Mockups and screenshots are illustrative only. `DESIGN.md` and `EXPERIENCE.md` remain authoritative on any conflict.

## Epic Quality Review

### Best-Practices Compliance By Epic

| Epic | User value | Independence | Story sizing | Dependency direction | Acceptance criteria | Assessment |
| --- | --- | --- | --- | --- | --- | --- |
| Epic 1 — Domain Author Self-Service Platform | Strong domain-author and operator outcome | **Fails:** Stories 1.11 and 1.15 require future Story 2.8 | **Fails:** Stories 1.3 and 1.6 are declared coordinated slices | Mostly specific BDD, but FR3 is incompletely claimed | Strong overall | 🔴 Not compliant |
| Epic 2 — External Integration Surfaces | Strong external-developer and UI-host outcome | Uses Epic 1 outputs appropriately, but Story 2.8 is sequenced too late for Epic 1 | **Fails:** Story 2.4 is a multi-surface coordinated slice | Mostly backward-facing | Several ambiguous alternatives | 🟠 Major correction required |
| Epic 3 — Release And Repository Reliability | Clear maintainer value through reproducible releases | Uses prior generated-API capability only for validation | **Fails:** Story 3.7 is a multi-workflow coordinated slice | No future-epic dependency | Story 3.1/3.7 conflict and weak conditional evidence in 3.8 | 🟠 Major correction required |
| Epic 4 — Event Correctness And Recovery | Strong operator and consumer correctness outcome | Independent of later epics; later optimization references are non-blocking | Generally well-sized | Correct | Strong except Story 4.7 traceability/authority | 🟡 Minor-to-major correction |
| Epic 5 — Security And Tenant Isolation | Strong security and tenant outcome | Independent of later epics | **Fails:** Story 5.6 is a topology/deploy/test/docs coordinated slice | Correct | Mostly testable; shared probe responsibility needs one clear owner | 🟠 Major correction required |
| Epic 6 — Bounded Cost And Event Evolution | Clear long-lived-stream and contract-evolution outcome | Correctly depends on earlier correctness baselines | Implementation pairs are focused; spec stories are technical enablers | Correct | Spec-first gates and approval evidence are unusually precise | ✓ Structurally acceptable with enabler caveat |
| Epic 7 — Operator Trust, Admin Honesty, And Future Capabilities | Strong operator/admin outcome | Uses prior platform/security capabilities appropriately | **Fails:** Stories 7.2-7.4 are coordinated slices; 7.5 bundles four independent backlog products | Correct | Several non-binary or qualitative criteria | 🟠 Major correction required |

### 🔴 Critical Violations

#### C1 — Epic 1 has a forbidden forward dependency on Epic 2

Story 1.11 accepts authoritative lifecycle metadata only when query provenance is available from Story 2.8. Story 1.15 explicitly requires Story 2.8 before parity closure. Epic 1 therefore cannot stand alone and cannot complete before a later epic, violating the required dependency direction.

**Remediation:** Move the platform provenance contract and route-aware gateway enforcement into Epic 1 before Story 1.11. If generated REST consumption belongs in Epic 2, split Story 2.8 into an Epic 1 platform prerequisite and an Epic 2 generated-API/UI adoption slice.

#### C2 — Eight implementation stories are acknowledged as oversized

The plan itself declares Stories 1.3, 1.6, 2.4, 3.7, 5.6, 7.2, 7.3, and 7.4 not ready for development or review unless they are split or carry coordinated-slice gates into later story files. They bundle independently testable concerns across packages, hosts, submodules, workflows, documentation, and/or UI surfaces.

The coordinated-slice escape hatch improves governance but does not make the stories independently completable under the quality standard used by this review.

**Remediation:** Split each story along the slice boundaries already listed in the execution-gate table. Preserve the named owner, review boundary, and focused validation commands in every resulting story. Do not mark the parent stories ready-for-dev until the decomposition is complete.

### 🟠 Major Issues

#### M1 — FR3 story-level acceptance is incomplete

Story 1.1 claims FR3, whose canonical endpoint list includes `/query`, but its endpoint acceptance criterion lists `/process`, `/replay-state`, `/project`, and `/admin/operational-index-metadata` only. Story 1.2 discusses routing to `/query` but does not claim FR3.

**Remediation:** Add `/query` to Story 1.1's canonical mapping criterion, or explicitly assign the `/query` portion of FR3 to Story 1.2 and update both requirement mappings.

#### M2 — Stories 3.1 and 3.7 conflict on release-gate filtering

Story 3.1 requires the release workflow to filter out `Category=LiveSidecar`. Story 3.7 says deterministic Server.Tests run in the blocking release gate **without** a `Category!=LiveSidecar` filter while live-sidecar tests still run separately. Both cannot describe the same final test topology unless tests are physically moved or another selection mechanism is named.

**Remediation:** Specify the final project/category topology and one exact release command. If live-sidecar tests move to a dedicated project, say so. Otherwise retain the explicit exclusion filter consistently.

#### M3 — Tenants submodule changes lack a consistent external-approval gate

Stories 1.6 and 2.4 require substantive Tenants source and UI changes, while Stories 2.7 and 4.7 correctly identify equivalent submodule work as maintainer-approved follow-up. The former stories do not state the authority, repository boundary, approved commit/PR, or fallback behavior when Tenants maintainer approval is unavailable.

**Remediation:** Add a uniform cross-repository change-control gate to Stories 1.6 and 2.4, including responsible maintainer, approval evidence, source/package mode, exact submodule SHA, and the behavior when approval is absent.

#### M4 — Several acceptance criteria permit incompatible outcomes

- Story 2.5 allows oversized metadata to be “rejected or clipped” and tests unspecified “fail-open broadcast behavior.” The selected behavior and security boundary are not deterministic.
- Story 1.12 permits a “bounded per-projection result or equivalent versioned result” without selecting a verifiable contract shape.
- Story 3.8 requires persisted/read-model evidence only “where available,” weakening its NFR16 validation role.
- Story 7.2 allows a typed client to be “introduced or planned,” so planning can satisfy an implementation criterion.
- Story 7.3 uses “where supported” and “supported or preferred” for health and immutable-image behavior.
- Story 7.4 relies on terms such as “meaningful,” “where applicable,” and “permanently red” without objective thresholds.

**Remediation:** Select one observable outcome per criterion, name the owning configuration or contract, and define exact pass/fail evidence. For Story 2.5, explicitly state that any fail-open behavior is limited to already-authorized legacy signal-only compatibility and can never bypass tenant/group authorization.

#### M5 — Story 4.7 lacks requirements traceability and a complete authority boundary

Story 4.7 is a real producer-side provenance correction but has no `Requirements covered` declaration. It depends on Tenants maintainer approval and can remain indefinitely visible without a clear EventStore completion/disposition rule.

**Remediation:** Map it explicitly to FR15 and the applicable provenance NFRs, name the Tenants owner/approval artifact, and define whether EventStore closes it through an approved external PR, an `Unknown` fallback contract, or a separately tracked external backlog item.

#### M6 — The epics requirement inventory has drifted from the PRD

The epics document reproduces an older, shorter NFR1 that omits the explicit anonymous, support-safe `/health`, `/alive`, and `/ready` exception now present in the PRD. Stories 5.3 and 5.5 contain the newer AD-16 behavior, but the local requirement inventory is no longer authoritative text.

**Remediation:** Refresh the epics NFR inventory from the final PRD or replace copied requirement text with stable references to the PRD to prevent future drift.

### 🟡 Minor Concerns

- Stories 6.1, 6.3, and 6.5 are technical specification/enabler stories rather than independently consumable user value. Their explicit classification and paired implementation gates make this acceptable, but they must not be counted as delivered runtime capability.
- Story 7.5 produces four unrelated backlog artifacts. Split it into four planning stories if independent ownership or completion tracking is required.
- FrontComposer is a mandatory UX/architecture dependency but is not visible in the architecture stack/dependency diagram; this weakens implementation handoff for UI stories.

### Positive Compliance Evidence

- Every primary implementation story uses a recognizable user/maintainer/operator outcome statement.
- Acceptance criteria predominantly follow Given/When/Then and cover failure, compatibility, security, and persisted-state evidence.
- Brownfield migration, compatibility, package-mode, submodule, DAPR, and runtime-topology concerns are represented; no greenfield starter template is required.
- Database-table timing is not applicable. DAPR/read-model keys and lifecycle state are introduced within the stories that first need them rather than through an upfront schema story.
- Epic 6 uses explicit spec-before-implementation pairs with named outputs and approval evidence.
- FR traceability remains complete despite the story-quality defects above.

### Required Quality Corrections Before Implementation

1. Eliminate the Epic 1 → Story 2.8 forward dependency.
2. Split all eight declared coordinated-slice implementation stories.
3. Repair FR3 `/query` story acceptance and mapping.
4. Reconcile the Story 3.1/3.7 release-test selection contradiction.
5. Add consistent Tenants submodule approval/SHA gates.
6. Replace alternative and qualitative acceptance criteria with deterministic evidence.
7. Refresh the copied NFR inventory and complete Story 4.7 traceability.

## Summary and Recommendations

### Overall Readiness Status

## NOT READY

The planning baseline has complete functional-requirement coverage and strong architecture/UX intent, but it is not safe to begin broad Phase 4 implementation. Two structural defects block implementation handoff: Epic 1 depends on a future Epic 2 story, and eight implementation stories remain oversized multi-concern slices.

### Critical Issues Requiring Immediate Action

1. **Remove the Epic 1 → Story 2.8 forward dependency.** Stories 1.11 and 1.15 cannot complete under the current ordering. Rehome the platform provenance prerequisite into Epic 1 before lifecycle/parity closure.
2. **Split the eight coordinated-slice stories.** Stories 1.3, 1.6, 2.4, 3.7, 5.6, 7.2, 7.3, and 7.4 are not independently completable in their current form.

### Recommended Next Steps

1. Split Story 2.8 into an Epic 1 platform provenance prerequisite and, if still needed, an Epic 2 generated-REST/UI consumption slice; update Stories 1.11 and 1.15 accordingly.
2. Decompose the eight oversized stories using the slice boundaries, owners, review boundaries, and focused validation commands already present in `epics.md`.
3. Repair story-level traceability: add `/query` to Story 1.1 or assign that FR3 portion to Story 1.2; map Story 4.7; refresh the copied NFR1 text.
4. Resolve the Story 3.1/3.7 live-sidecar selection contradiction with one final project/category topology and exact release command.
5. Add explicit Tenants maintainer approval, repository boundary, package/source mode, and exact-SHA gates to Stories 1.6 and 2.4.
6. Replace alternative or qualitative acceptance criteria in Stories 1.12, 2.5, 3.8, 7.2, 7.3, and 7.4 with one deterministic outcome and exact evidence.
7. Name the project/AppHost resource that owns the future consolidated EventStore UI service and represent the FrontComposer dependency in architecture. Add quantitative UI performance criteria if responsiveness is a release gate.
8. Re-run implementation readiness after the corrected `epics.md`, architecture handoff, and affected story files are reviewed.

### Issue Summary

This assessment identified **14 findings across four categories**:

- 2 critical structural violations;
- 7 major alignment, traceability, or acceptance-quality issues;
- 5 warnings/minor concerns;
- categories: dependency/sequence, story sizing/independence, traceability/acceptance criteria, and UX/architecture handoff.

### Final Note

The artifacts are materially stronger than the prior readiness baseline: all 36 FRs and 18 NFRs exist, FR coverage is 100%, architecture and UX are final, and no legacy UX information was lost. That strength does not offset the structural blockers. Address the two critical violations and major issues before authorizing broad implementation; proceeding as-is would knowingly accept sequencing, reviewability, and verification risk.

**Assessment completed:** 2026-07-15  
**Assessor:** Codex — BMAD Implementation Readiness, Product Manager role
