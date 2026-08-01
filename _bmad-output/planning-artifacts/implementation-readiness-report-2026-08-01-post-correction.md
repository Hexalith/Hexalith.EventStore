---
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
assessment: post-correction
source_assessment: implementation-readiness-report-2026-08-01.md
includedDocuments:
  prd:
    - prd.md
  architecture:
    - architecture.md
  epics:
    - epics.md
  ux:
    - ux.md
    - ux-designs/ux-eventstore-2026-07-05/index.md
    - ux-designs/ux-eventstore-2026-07-05/DESIGN.md
    - ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-08-01
**Project:** eventstore

## Document Discovery

The assessment uses the following canonical planning set:

| Document type | Selected authority | Supporting material | Discovery result |
| --- | --- | --- | --- |
| PRD | `prd.md` | — | Found; no competing whole or sharded authority. |
| Architecture | `architecture.md` | — | Found; no competing whole or sharded authority. |
| Epics and stories | `epics.md` | — | Found; no competing whole or sharded authority. |
| UX | `ux.md` | `ux-designs/ux-eventstore-2026-07-05/index.md`, `DESIGN.md`, and `EXPERIENCE.md` | Found; the whole document is the canonical handoff and the shard is its intentional supporting design packet, not a competing authority. |

No required planning document is missing. The Administrator confirmed this selection on 2026-08-01.

## PRD Analysis

### Functional Requirements

| ID | Complete requirement text |
| --- | --- |
| FR1 | Domain modules built on Hexalith.EventStore must be domain-centric, containing domain code such as aggregates, commands, events, projections, query handlers, validators, and contracts, while platform boilerplate is supplied by EventStore libraries. |
| FR2 | The platform must provide a domain-service SDK with `AddEventStoreDomainService`, `UseEventStoreDomainService`, and `MapEventStoreDomainService` so a domain service host can be reduced to the canonical SDK host shape. |
| FR3 | The domain-service SDK must expose the canonical DAPR-facing endpoints `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata`. |
| FR4 | The platform must provide a domain query-handler seam using `IDomainQueryHandler`, discovery, dispatch, operational metadata reporting, gateway-side query-type capture, handler-aware routing to domain `/query` endpoints, and end-to-end `QueryResponseMetadata` propagation for freshness, projection version, ETag, served-at, degraded/warning state, and paging evidence, carrying an explicit query-response provenance classification (projection-backed, handler-computed, or unknown) that governs whether that evidence is projection-backed. Projection-backed responses must additionally preserve a lossless lifecycle representation or owner-approved mapping for `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`; consumers must not infer lifecycle from ETags or claim projection-confirmed success without projection-backed provenance. |
| FR5 | The platform must provide generic persisted read-model lifecycle and write contracts with ETag-aware reads/writes, coordinated read-model and sequence/checkpoint erasure, and detail/index batch writes or an approved equivalent. Batch behavior must define partial-failure recovery, idempotency, ordering, flush completion, optimistic concurrency, DAPR behavior, and deterministic in-memory testing semantics. |
| FR6 | The platform must provide a reusable DataProtection-backed query cursor codec with scope validation, payload limits, tamper/key-rotation handling, and caller-supplied purpose isolation. |
| FR7 | The platform must provide an asynchronous, cancellation-aware projection-handler seam supporting multiple named projections per domain and coordinated detail/index persistence, plus a generic domain-event subscription/consumer pipeline with deduplication and endpoint mapping. Projection delivery must tolerate duplicate and out-of-order events through the actual handler path, and full rebuilds must remain correct across paging boundaries. |
| FR8 | The platform must provide Aspire, telemetry, and health-check extensions for domain modules, including `AddEventStoreDomainModule`, convention telemetry, and DAPR state-store health checks. |
| FR9 | The Sample domain and Tenants domain must adopt platform SDK seams so duplicated request routers, projection actors, cursor codecs, state-store plumbing, telemetry, health checks, and per-domain Aspire wiring are removed or reduced to domain-specific logic. |
| FR10 | The EventStore package set must include the domain-service and service-default packages as publishable packages, and release packaging must publish only the manifest-governed EventStore package set. |
| FR11 | The platform must provide a REST API source-generator contract seam with `ICommandContract`, `IQueryContract`, optional `RestRouteAttribute`, and assembly-level `RestApiAttribute`. |
| FR12 | The REST API generator must discover command and query contracts and emit typed, OpenAPI-visible controllers that delegate to `IEventStoreGatewayClient` and forward canonical query metadata headers when the gateway supplies them. The generator test suite must cover discovery, routing conventions, diagnostics, generated output, query metadata headers, `304`, and safe problem-detail behavior. An accepted generated command must emit an absolute, gateway-authoritative command-status `Location` URI when the gateway supplies a valid target; it must omit `Location` when the target is absent, invalid, or unavailable rather than emit a relative or dangling external-host URI. |
| FR13 | Generated REST controllers must live in dedicated external-facing API hosts, not interactive UI hosts; interactive UI hosts must consume EventStore client libraries directly. |
| FR14 | The Sample proof must introduce a contracts-only Sample contracts library and an external Sample API host, move shared contracts there, and prove generated query and command controllers through that external API host. |
| FR15 | The Tenants proof must move generated Tenants controllers to an external Tenants API host, while Tenants UI consumes client libraries and no longer hosts hand-written per-message controllers; any Tenants freshness, projection-version, ETag, or paging evidence shown by generated APIs or UI must come from the platform query metadata path. |
| FR16 | The projection-changed transport must add an additive metadata-rich detail path with optional group scope, bounded metadata, scoped SignalR groups, DAPR notification support where needed, and preserved signal-only compatibility. |
| FR17 | Live DAPR sidecar tests must be tagged and removed from the per-push release gate, then run in a dedicated integration workflow with sidecar warm-up and readiness retry. |
| FR18 | `DaprETagService` must allow an overridable actor request timeout while preserving the production default. |
| FR19 | Root-declared Git submodules must live under `references/`, and solution, project, documentation, Aspire metadata, and LLM instruction paths must resolve through the `references/` layout. |
| FR20 | The Aspire Keycloak resource must be named `security` while preserving Keycloak as the implementation technology and updating fixtures/resource lookups accordingly. |
| FR21 | Cross-repo Hexalith library dependencies use source project references only when `UseHexalithProjectReferences=true` is explicitly supplied and the root-declared source exists. An unset or explicit `false` value selects package references in every configuration, including Debug; Release and configuration-less evaluation therefore remain package-safe. Every source-owned NuGet dependency version used by a Hexalith repository must be declared in `references/Hexalith.Builds/Props/Directory.Packages.props`; consuming `Directory.Packages.props` files import that catalog and declare no local `PackageVersion`, version override, or fallback version property. |
| FR22 | Commands used to restore, build, test, pack, and run semantic-release must assert package-reference mode and avoid packaging submodule projects. |
| FR25 | EventStore workflows must use shared Hexalith.Builds security gates through `@main`, keep third-party actions SHA-pinned through shared workflows, and define NuGet package publish scope in `tools/release-packages.json`. |
| FR23 | Persisted events must receive non-zero, actor-allocated global positions; CloudEvent IDs must use the event `MessageId`; duplicate command replies must preserve the original command result fields. |
| FR24 | The global-position allocation strategy must be renegotiated toward sharding per tenant or domain, and the frozen global-ordering spec must be updated before implementation. |
| FR27 | Pipeline and idempotency correctness remediation must use exact command identity for resume; provide an EventStore-owned, tenant-scoped durable admission contract accepting only a trusted, versioned canonical-intent descriptor and fixed retention tier; reject live conflicting intent and return non-retryable `idempotency_key_expired` for any expired-key reuse before aggregate, domain, or external execution; separate replay-result retention from metadata-only consumed-key evidence; and never convert consumed, unavailable, corrupt, or unsafe legacy state into a fresh miss. Command status/archive identity, transient retryability, and tenant-before-state validation remain required. |
| FR29 | Replay and dispatch remediation must make event apply-method resolution boundary-safe and ambiguity-detecting, and must use one shared `JsonSerializerOptions` path for command, rehydrate, project, and pub/sub payload serialization. |
| FR30 | Crash recovery remediation must detect events committed but not published and complete their publication, drain them, or recover them without requiring resubmission with the same correlation ID. |
| FR31 | Append durability remediation must start with a live-sidecar two-writer race test and DAPR conflict-exception spike before choosing an optimistic-concurrency fencing design. |
| FR26 | Phase 0 architecture remediation must close immediate safe fixes: clear staged state on infrastructure failure, protect anonymous admin endpoints, strip committed admin secrets, enforce production auth guards, add tenant-filter parity, gate admin Swagger, require destructive CLI confirmation, use ULID-safe admin correlation middleware, and correct stale test-baseline documentation. |
| FR28 | Trust-boundary remediation must require app-layer credentials for internal, domain-service, projection-notification, and admin-computation endpoints, and must remove trust in wire-asserted administrator flags. |
| FR32 | Runtime topology remediation must make the AppHost-loaded DAPR pub/sub, ACL, and key-prefix posture match the posture asserted by tests and production deploy templates. |
| FR33 | Cost and evolution remediation must introduce folded snapshots, reduce projection replay cost, add projection sequence guards, support event schema versioning/upcasting, validate event metadata identity components, and add cancellation-token seams to published processing/query/projection interfaces. |
| FR34 | Delivery, admin, and deployment remediation must document at-least-once unordered delivery, add poison/dead-letter handling, bound in-memory deduplication, normalize admin claims, audit every state-mutating admin action, hide deferred admin operations, add OpenBao-backed DAPR secret-store configuration for production operational and application secrets, require application retrieval through the DAPR Secrets API, restrict Kubernetes Secrets to documented bootstrap credentials only when no approved mounted or projected credential mechanism is available, add readiness/app-health checks, and restore meaningful IntegrationTests CI coverage. |
| FR35 | Backlog capabilities must be tracked for GDPR aggregate erasure/tombstoning, Admin interactive OIDC login, an aggregate test kit, and REST generator hardening. |
| FR36 | Before a consuming module deletes local projection/query infrastructure, EventStore must produce an owner-reviewed parity packet proving every required capability through production paths, record an approved runtime SHA, and require the consumer's checked-out EventStore SHA to match that approval. |
| FR37 | EventStore must provide an optional shared payload-protection engine package built on `IEventPayloadProtectionService` and the existing provider-neutral metadata, outcome, workflow, and redaction contracts. The engine must implement the approved `pdenc-v2` format and byte-stable authenticated-data contract, preserve `json+pdenc-v1`, `json-redacted`, legacy-unprotected, and snapshot read compatibility, expose `IPersonalDataPolicy` and `IErasureStateProvider` extension seams, supply reusable key-lifecycle and resilience mechanics behind shared contracts, include at least one integration-proven production backend, and produce EventStore-owner plus Parties dual-provider parity and rollback evidence before G5 is available. |

**Total FRs: 37**

### Non-Functional Requirements

| ID | Complete requirement text |
| --- | --- |
| NFR1 | Security must fail closed for public, internal, domain-service, projection-notification, and admin surfaces; no endpoint may rely only on network posture or caller-supplied admin flags. The only anonymous exception is the health/liveness/readiness probe endpoints (`/health`, `/alive`, `/ready`), which are explicitly pinned `AllowAnonymous` and support-safe (AD-16); the fail-closed default is never weakened to reach probes. |
| NFR2 | Tenant isolation must be preserved across state keys, actor IDs, topics, admin queries, generated REST APIs, SignalR groups, and deployment configuration. Tenant provisioning must reject the reserved `system` tenant name. |
| NFR3 | Production authentication must reject insecure symmetric-key mode unless explicitly break-glassed, require HTTPS metadata where appropriate, and pin accepted JWT algorithms. |
| NFR4 | Committed configuration must not contain forgeable administrator signing keys, credentials, bearer tokens, decoded JWT payloads, or other operational secrets. |
| NFR5 | SignalR detail metadata must remain bounded and metadata-only; framework logs must not expose metadata values above Debug level. |
| NFR6 | Event delivery semantics are at-least-once and unordered; subscribers must deduplicate by `MessageId` and order events only where domain semantics make `SequenceNumber` meaningful. Safety against duplicate and out-of-order delivery must be enforced and proven through the production projection dispatcher, handler, persistence, marker, and checkpoint path rather than only aggregate replay or transport-level tests. |
| NFR7 | Event persistence and command processing must avoid silent data loss: staged-state flushes, stale pipeline records, append races, and committed-but-unpublished events must be explicitly guarded or recovered. Command processing must also prevent duplicate side effects across reservation, fencing, execution, recovery, expiry, compaction, restart, and concurrent hosts; a consumed key cannot become executable fresh work because its replay result expired or storage became unreadable. |
| NFR8 | Snapshot and projection behavior must have a bounded cost model as streams grow, must avoid unnecessary full-stream replay when projections are already current, and must expose projection freshness/version evidence through platform query metadata when callers depend on lifecycle decisions; freshness/version evidence is authoritative only for query responses whose route provenance is projection-backed, and handler-computed or unknown-provenance responses must not be presented as authoritative lifecycle evidence. Paged rebuild output must equal canonical aggregate replay and must never overwrite a complete live model with page-only state. |
| NFR9 | Release behavior must be reproducible and independent of local submodule checkout state; Release builds must use package references for external Hexalith libraries unless intentionally overridden. |
| NFR10 | CI/CD must separate deterministic release-gate tests from live-sidecar/integration tests while preserving live-sidecar coverage in a dedicated lane. |
| NFR11 | Package publishing must be manifest-driven and must not publish submodule packages or packages outside the EventStore release inventory. |
| NFR12 | Backward compatibility must be preserved for additive framework changes such as SignalR signal-only projection notifications and existing generic gateway APIs. |
| NFR13 | Generated code and source-generator packages must build cleanly under warnings-as-errors and must follow EventStore code style, nullable, ULID, and `ConfigureAwait(false)` rules. |
| NFR14 | Interactive UI hosts must not expose generated or hand-written per-message MVC command/query controllers; UI command/query flows consume client libraries. |
| NFR15 | Admin UX must not present deferred backup, restore, import, compaction, or other unavailable operations as functional; unavailable operations must be hidden/disabled or return `501`. |
| NFR16 | Integration and higher-tier tests must assert persisted state-store/read-model/end-state evidence, not only HTTP status codes or mock call counts. Erasure, batch recovery, handler idempotency, and rebuild equivalence require persisted detail, index, marker, lifecycle, and checkpoint evidence through their production paths. Durable-admission evidence must inspect production-path state and prove restart survival, multi-host serialization, inclusive expiry boundaries, atomic tombstone compaction, leakage constraints, and zero downstream execution for replay, conflict, expired, corrupt, and unsafe legacy outcomes. |
| NFR17 | Operational hardening must use the canonical DAPR `openbao` component for production operational and application secrets. Dependent DAPR components must use `secretKeyRef` with `auth.secretStore: openbao`; application code must use the DAPR Secrets API; and per-application access must be default-deny. OpenBao bootstrap credentials are platform inputs and may use Kubernetes Secrets only when no approved mounted or projected mechanism is available. Operational hardening must also support DAPR app-health checks, readiness-tagged health checks, resiliency targets, immutable image tags, and documented crypto-shred boundaries. |
| NFR18 | AOT/trimming is explicitly not a target while reflection conventions remain load-bearing, and that constraint must be documented. |
| NFR19 | Payload protection must fail closed and preserve byte-stable, versioned cryptographic semantics. Deleted, missing, denied, unavailable, malformed, tampered, and opaque states must remain bounded typed outcomes. Key material must be zeroed when no longer needed; caches must be invalidated on lifecycle changes; development-only backends must not start as production proof; and rollout, historical reads, downgrade, and rollback after writing the newest format must be integration-tested. |

**Total NFRs: 19**

### Additional Requirements

- Planning authority is separated explicitly: the PRD owns requirement truth and scope; Architecture owns component, integration, topology, and decision gates; UX owns interaction governance; and `epics.md` owns slicing, sequencing, acceptance criteria, and implementation handoff.
- The approved OQ8 proposal and design digest govern the Story 4.8 ledger and executable Stories 4.9-4.15; older FR27/NFR7/NFR16 wording cannot weaken that sequence.
- Repository/build guardrails require `.slnx`, project-level tests, centralized package versions, explicit source-reference opt-in, package-safe defaults, SDK containers, an exact two-platform OCI index, and root-declared `references/` submodules only.
- Identity/security guardrails require ULID-safe identifiers, forbid `Guid.TryParse` for EventStore identity fields, validate tenant access before resource disclosure, and require application credentials across internal trust boundaries.
- UI governance requires FrontComposer and Fluent UI Blazor V5, forbids theme redefinition and unsafe raw rendering, requires accordion behavior for multi-section pages, preserves projection-confirmed Tenants states, and keeps unavailable Admin operations hidden, disabled, or `501`.
- Phase 4 MVP comprises Epics 1-7. Epic 8 is committed post-MVP scope under FR37/NFR19 and does not block MVP completion; Story 8.1 may authorize only Story 8.2, with the remaining children gated through Story 8.11.
- Explicit non-goals include full GDPR aggregate/event erasure, Admin interactive OIDC implementation, aggregate test-kit implementation, expanded REST generator hardening, AOT/trimming, generated controllers in UI hosts, and treating `202` or notification delivery as projection-confirmed success.
- No PRD-level ownership or MVP-scope question remains open. No inline assumptions are recorded.

### PRD Completeness Assessment

The PRD is complete enough for traceability validation: it is marked final, identifies a single authority for requirement prose, contains contiguous exact sets FR1-FR37 and NFR1-NFR19, distinguishes the seven-epic MVP from separately gated Epic 8, defines constraints/non-goals/success metrics, and records the August story ownership changes. The requirement statements are specific and test-oriented, with measurable limits supplied where readiness previously found ambiguity. No missing FR/NFR identifier or unresolved PRD-level ownership question was found.

## Epic Coverage Validation

### Coverage Matrix

| FR Number | PRD Requirement | Epic/story coverage | Status |
| --- | --- | --- | --- |
| FR1 | Domain modules built on Hexalith.EventStore must be domain-centric, containing domain code such as aggregates, commands, events, projections, query handlers, validators, and contracts, while platform boilerplate is supplied by EventStore libraries. | Stories 1.1, 1.11 | ✓ Covered |
| FR2 | The platform must provide a domain-service SDK with `AddEventStoreDomainService`, `UseEventStoreDomainService`, and `MapEventStoreDomainService` so a domain service host can be reduced to the canonical SDK host shape. | Story 1.1 | ✓ Covered |
| FR3 | The domain-service SDK must expose the canonical DAPR-facing endpoints `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata`. | Story 1.1 | ✓ Covered |
| FR4 | The platform must provide the full domain query-handler, routing, response-metadata, provenance, and authoritative lifecycle contract stated in the PRD. | Stories 1.2, 1.9, 1.13, 1.16, 2.7 | ✓ Covered |
| FR5 | The platform must provide generic persisted read-model lifecycle/write contracts and fully defined detail/index batch behavior. | Stories 1.3, 1.4, 1.9, 1.13, 1.14, 1.15 | ✓ Covered |
| FR6 | The platform must provide a reusable DataProtection-backed query cursor codec with scope validation, payload limits, tamper/key-rotation handling, and caller-supplied purpose isolation. | Stories 1.5, 1.9, 1.13 | ✓ Covered |
| FR7 | The platform must provide the asynchronous projection-handler and domain-event subscription/consumer seams, including duplicate/out-of-order safety and correct paged rebuilds. | Stories 1.6, 1.10, 1.13, 1.17, 1.18, 1.19 | ✓ Covered |
| FR8 | The platform must provide Aspire, telemetry, and health-check extensions for domain modules. | Story 1.7 | ✓ Covered |
| FR9 | Sample and Tenants must adopt platform SDK seams and remove or reduce duplicated platform plumbing to domain-specific logic. | Stories 1.8, 1.9, 1.10, 1.11, 1.13 | ✓ Covered |
| FR10 | The EventStore package set must publish the domain-service and service-default packages only through the governed release manifest. | Stories 1.11, 1.12 | ✓ Covered |
| FR11 | The platform must provide the REST API source-generator contract seam. | Stories 2.1, 2.4 | ✓ Covered |
| FR12 | The generator must emit typed OpenAPI-visible controllers, preserve gateway query metadata, cover required diagnostics/outputs, and use only valid gateway-authoritative command-status locations. | Stories 2.2, 2.9, 2.11 | ✓ Covered |
| FR13 | Generated controllers must live in dedicated external API hosts; interactive UI hosts must consume EventStore clients directly. | Stories 2.3, 2.5, 2.6, 2.10, 7.14 | ✓ Covered |
| FR14 | Sample must introduce a contracts-only library and external API host and prove generated query/command controllers there. | Stories 2.3, 2.10 | ✓ Covered |
| FR15 | Tenants must move generated controllers to an external API host, use client libraries in its UI, and source displayed freshness evidence from platform query metadata. | Stories 2.4, 2.5, 2.6, 2.7, 2.11, 2.12, 4.7, 7.19 | ✓ Covered |
| FR16 | Projection-changed transport must add the bounded metadata-rich detail path while preserving signal-only compatibility. | Story 2.8 | ✓ Covered |
| FR17 | Live DAPR sidecar tests must move from the per-push release gate to a dedicated integration workflow with readiness handling. | Stories 3.1, 3.10 | ✓ Covered |
| FR18 | `DaprETagService` must allow an overridable actor request timeout while preserving the production default. | Story 3.2 | ✓ Covered |
| FR19 | Root-declared submodules and all related paths must use the `references/` layout. | Story 3.3 | ✓ Covered |
| FR20 | The Aspire Keycloak resource must be named `security`, with fixtures and lookups updated. | Story 3.4 | ✓ Covered |
| FR21 | Cross-repository Hexalith references must be explicitly source-opted-in, otherwise package-safe, with source-owned dependency versions centralized in Hexalith.Builds. | Stories 2.12, 3.5, 3.11 | ✓ Covered |
| FR22 | Restore, build, test, pack, and semantic-release commands must assert package-reference mode and exclude submodule packaging. | Stories 2.12, 3.6, 3.8, 3.11, 3.12 | ✓ Covered |
| FR23 | Persisted events, CloudEvent IDs, and duplicate command replies must preserve their specified position and identity semantics. | Story 4.1 | ✓ Covered |
| FR24 | The global-position strategy and frozen ordering spec must be renegotiated before implementation. | Story 4.6 | ✓ Covered |
| FR25 | Workflows must use shared security gates, SHA-pinned third-party actions, and the release package manifest. | Stories 3.7, 3.8, 3.9, 3.11, 3.12 | ✓ Covered |
| FR26 | Phase 0 must close the enumerated immediate architecture, authentication, secret, tenant, CLI, correlation, and documentation remediations. | Stories 2.10, 5.1, 5.2, 5.3, 5.4 | ✓ Covered |
| FR27 | Pipeline/idempotency remediation must implement the complete trusted durable-admission, conflict, expiry, replay-evidence, and fail-closed identity contract. | Stories 2.9, 4.2, ledger 4.8, implementation Stories 4.9-4.15 | ✓ Covered |
| FR28 | Internal trust boundaries must require application credentials and reject wire-asserted administrator authority. | Stories 2.10, 5.5 | ✓ Covered |
| FR29 | Replay/dispatch must use boundary-safe apply resolution and one shared JSON options path. | Story 4.3 | ✓ Covered |
| FR30 | Crash recovery must recover committed-but-unpublished events without same-correlation resubmission. | Story 4.4 | ✓ Covered |
| FR31 | Append durability must begin with the live-sidecar two-writer race and DAPR conflict spike. | Story 4.5 | ✓ Covered |
| FR32 | AppHost-loaded DAPR topology must match test and production assertions. | Stories 5.6, 5.7, 5.8, 5.9 | ✓ Covered |
| FR33 | Cost/evolution remediation must cover folded snapshots, projection replay/sequence, upcasting, identity validation, and cancellation seams. | Stories 1.19, 6.1-6.6 | ✓ Covered |
| FR34 | Delivery, Admin, secret-store, health, deployment, and IntegrationTests remediation must cover the complete PRD scope. | Stories 2.6, 2.11, 3.10, 7.1-7.14, 7.19, 7.20 | ✓ Covered |
| FR35 | The four named deferred capabilities must each remain independently tracked. | Stories 7.15-7.18 | ✓ Covered |
| FR36 | Consumer deletion of local infrastructure requires an owner-reviewed production-path parity packet and an exact approved/checked-out EventStore runtime SHA match. | Stories 1.2-1.4, 1.9, 1.10, 1.14-1.20, 3.13 | ✓ Covered |
| FR37 | The optional shared payload-protection engine must satisfy the complete provider-neutral, versioned format, compatibility, lifecycle, production-backend, parity, and rollback contract. | Stories 8.1-8.11 | ✓ Covered |

### Missing Requirements

None. All PRD functional requirements have at least one explicit story path. No story claims an FR identifier outside the PRD's FR1-FR37 set.

### Coverage Statistics

- Total PRD FRs: 37
- FRs covered in epics: 37
- Missing PRD FRs: 0
- Extra FR identifiers in epics: 0
- Coverage percentage: 100%

## UX Alignment Assessment

### UX Document Status

Found and complete. `ux.md` is the canonical top-level handoff; `DESIGN.md` and `EXPERIENCE.md` are the detailed visual and behavioral authorities. All are final and updated 2026-08-01.

### UX ↔ PRD Alignment

The UX packet covers the PRD's user-facing scope without adding a second product boundary. It preserves the existing `Hexalith.EventStore.Admin.UI`, separates accepted work from evidence-confirmed completion, carries projection provenance and all lifecycle states, keeps deferred operations non-functional, defines tenant-safe denial behavior, and covers accessibility, localization, responsive layouts, and support-safe diagnostics. The Sample and Tenants journeys preserve the typed-client-only UI boundary required by FR13-FR15/NFR14.

No substantive PRD/UX scope conflict was found.

### UX ↔ Architecture Alignment

Architecture AD-4, AD-8, AD-10, AD-14, AD-15, AD-18, and AD-21 support the UX interaction model: one in-place Admin UI host, one FrontComposer module entry, canonical dashboard tabs/deep links, typed clients, fail-closed authorization, SignalR as a freshness nudge, and projection-backed evidence as the only basis for confirmed success. Architecture also binds Fluent UI Blazor V5/FrontComposer packages, the `eventstore-admin-ui` resource identity, accessibility/localization handoff, and support-safe data boundaries.

Quantitative UI performance budgets are consistently deferred in both UX and Architecture because no measured production baseline exists. This is a documented non-blocking follow-up, not an invented readiness gate.

### Alignment Issues

None substantive.

### Warnings

- Documentation caution: two detailed UX traceability labels associate lifecycle/provenance directly with FR36 (`DESIGN.md` and `EXPERIENCE.md`). FR4 is the primary requirement authority for that contract, while FR36 governs consumer parity proof. The behavioral text is correct, so this is a source-label cleanup rather than an implementation ambiguity.
- Documentation caution: the detailed UX frontmatter still cites the 2026-07-05 readiness report as historical source material and does not list the August correction artifacts. The files' status, update date, and actual contracts are current, so this does not create competing authority.

## Epic Quality Review

### Review Scope

The review covered all 8 epics and all 107 numbered sections in `epics.md`: 106 executable/planning stories plus the deliberately non-executable Story 4.8 evidence ledger. It checked epic value, ordering, explicit dependency gates, story sizing, acceptance-criteria structure, error paths, brownfield integration, persistence timing, and FR/NFR traceability.

### Epic Compliance Summary

| Epic | User-value outcome | Independence / ordering | Story sizing and structure | Result |
| --- | --- | --- | --- | --- |
| Epic 1 — Domain Author Self-Service Platform | Domain authors receive reusable platform seams and source/package parity closure. | Pass. Deployed-runtime parity moved to later Story 3.13 and cannot gate or reopen Epic 1. | Focused implementation and evidence slices. | Pass |
| Epic 2 — External Integration Surfaces | External API consumers and UI developers receive distinct safe integration paths. | Pass. Story 2.6 uses deterministic presentation fixtures; Story 2.11 independently owns production provenance. | Focused, BDD-complete stories. | Pass |
| Epic 3 — Release And Repository Reliability | Maintainers receive reproducible repository and release behavior. | Pass; dependencies use completed earlier work only. | Stories 3.5 and 3.11 now have immutable repository/family inventories and explicit follow-up rules. | Pass with bounded-scope caution |
| Epic 4 — Event Correctness And Recovery | Operators and consumers receive durable correctness and recovery behavior. | Pass. Stories 4.9-4.15 form a backward-only chain. | The former oversized 4.8 scope is split into seven implementation/closure stories; 4.8 is only a ledger. | Pass |
| Epic 5 — Security And Tenant Isolation | Tenants and operators receive fail-closed protection. | Pass. | Focused stories; Story 5.10 closes the reserved-`system` tenant rule with zero-state/zero-downstream evidence. | Pass |
| Epic 6 — Bounded Cost And Event Evolution | Platform users receive bounded-cost and evolution capabilities. | Pass; each spec gate precedes its paired implementation. | Enablers are explicitly excluded from runtime-value accounting; Story 6.2 now has a numeric overhead invariant. | Pass |
| Epic 7 — Operator Trust, Admin Honesty, And Future Capabilities | Operators receive delivery, Admin, deployment, test-evidence, and explicitly deferred-product outcomes. | Pass; five independent delivery tracks expose closure boundaries and no forward dependency. | Story 7.14 is split into shell/routes, typed-client/evidence, and accessibility/localization/responsive conformance. | Pass with cohesion caution |
| Epic 8 — Shared Payload Protection | Security owners and domains receive an optional production-proven protection engine. | Pass. Story 8.1 gates 8.2 and every later dependency points backward. | The former oversized 8.2 scope is split through Stories 8.2-8.11 with distinct owner/test/release/rollback boundaries. | Pass |

### Dependency Analysis

- No forward story dependency was found. Every explicit dependency declaration points to an earlier story or a completed earlier epic.
- Story 3.13 consumes completed Stories 1.20 and 3.12 without changing either status.
- Stories 4.9-4.15 form the ordered admission implementation/evidence chain after the non-executable 4.8 ledger.
- Stories 7.19 and 7.20 consume only earlier Admin work.
- Epic 8 follows `8.1 → 8.2 → 8.3 → (8.4 and 8.5) → 8.6 → 8.7 → 8.8 → 8.9 → 8.10 → 8.11`; no child relies on a future story.

### Story and Acceptance-Criteria Quality

- All 106 executable/planning stories have a user/owner value statement, an acceptance-criteria section, and at least one complete Given/When/Then scenario.
- Every Given scenario has a corresponding When and Then. Story 7.6 is no longer the BDD outlier.
- Failure, denial, cancellation, conflict, stale/unavailable evidence, persistence, and support-safe paths are present where applicable.
- State/database artifacts are introduced with the stories that use them; there is no up-front “create all storage” story.
- This is correctly treated as brownfield work: existing hosts, legacy routes/formats, source/package modes, migration, compatibility, and owner approval boundaries are explicit.
- Architecture mandates no greenfield starter template, so no starter-template setup story is required.

### Critical Violations

None.

### Major Issues

None.

### Minor Concerns and Documentation Cautions

1. Story 4.8 intentionally retains a story identifier while being classified as a non-executable evidence ledger and having no user-story/BDD body. Sprint status correctly excludes it as executable work, and Stories 4.9-4.15 own delivery. Keep that exclusion mechanically enforced; after migration consumers no longer require the identifier, relabeling it as an evidence-ledger section would remove the structural exception.
2. Stories 3.5 and 3.11 remain large coordination stories, but their seven-repository and five-family/revision inventories are now closed and discoveries route to named follow-ups. Treat expansion of either accepted inventory as a new story, exactly as their criteria require.
3. Epic 7 remains broad at the top level. Its five independently schedulable delivery tracks and three-way Admin split make execution bounded, but reporting should continue at track/story granularity rather than treating the epic title alone as a single release increment.

### Best-Practices Result

The four prior critical violations and six prior major issues are resolved in the active plan. The remaining observations are documentation/reporting cautions with explicit guardrails; none creates a forward dependency, an epic-sized executable story, or an ambiguous implementation boundary.

## Summary and Recommendations

### Overall Readiness Status

## READY

The corrected planning baseline is implementation-ready. All required artifacts exist; the PRD contains exact contiguous sets of 37 FRs and 19 NFRs; functional story coverage is 37/37 (100%); UX, PRD, and Architecture are substantively aligned; and no critical or major epic-quality violation remains.

`READY` does not bypass story activation or approval gates. Story 4.8 remains a non-executable ledger, sprint status controls which Stories 4.9-4.15 may proceed, and Epic 8 implementation remains unauthorized until Story 8.1 records every required approval and explicit content-digest-bound authorization for Story 8.2. Later Epic 8 stories remain predecessor-gated through Story 8.11.

### Critical Issues Requiring Immediate Action

None.

### Recommended Next Steps

1. Start only work whose sprint status and story-specific activation gates authorize it; do not interpret this planning-readiness result as approval of Story 8.2 or any external release/deployment mutation.
2. Keep Story 4.8 mechanically excluded from executable sprint work and preserve the backward-only 4.9-4.15 evidence chain.
3. Preserve the frozen Story 3.5 repository inventory and Story 3.11 audit boundary; route later discoveries to named follow-up stories instead of expanding completed scope.
4. Report Epic 7 progress by its five delivery tracks and the separate Stories 7.14, 7.19, and 7.20 boundaries.
5. At the next documentation-only cleanup, change the two detailed UX provenance/lifecycle source labels to cite FR4 as the primary requirement and add the August correction artifacts to the UX source metadata.
6. After any material PRD, architecture, UX, dependency, or story-boundary change, rerun implementation readiness and publish a new dated/suffixed report rather than overwriting this assessment or its historical predecessor.

### Final Note

This post-correction assessment identified five non-blocking cautions across two categories: UX source traceability (2) and epic/story documentation or reporting structure (3). It found zero critical violations, zero major issues, complete functional coverage, and no forward dependency. The prior `NOT READY` assessment remains preserved as historical evidence; this report records the corrected `READY` decision.

- **Assessment date:** 2026-08-01
- **Requested by:** Administrator
- **Assessor:** Codex, using the BMad Implementation Readiness workflow
