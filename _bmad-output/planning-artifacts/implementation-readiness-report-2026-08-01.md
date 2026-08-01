---
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
overallReadinessStatus: NOT READY
includedFiles:
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

### PRD Files Found

**Whole Documents:**

- `prd.md` (50,545 bytes, modified 2026-07-20 08:34 CEST)

**Sharded Documents:** None found.

### Architecture Files Found

**Whole Documents:**

- `architecture.md` (68,126 bytes, modified 2026-08-01 07:36 CEST)

**Sharded Documents:** None found.

### Epics and Stories Files Found

**Whole Documents:**

- `epics.md` (204,576 bytes, modified 2026-07-27 23:23 CEST)

**Sharded Documents:** None found.

### UX Design Files Found

**Top-level compatibility handoff:**

- `ux.md` (1,101 bytes, modified 2026-07-11 12:40 CEST)

**Canonical sharded document set:**

- Folder: `ux-designs/ux-eventstore-2026-07-05/`
  - `index.md`
  - `DESIGN.md`
  - `EXPERIENCE.md`
  - Supporting review and validation artifacts

Content validation confirmed that `ux.md` is a compatibility handoff rather than a competing specification. The sharded `DESIGN.md` and `EXPERIENCE.md` documents are the canonical UX sources and contain the lifecycle requirements summarized by the handoff. No unresolved document-format conflict remains.

### Confirmed Assessment Sources

- PRD: `prd.md`
- Architecture: `architecture.md`
- Epics and stories: `epics.md`
- UX: `ux-designs/ux-eventstore-2026-07-05/index.md`, `DESIGN.md`, and `EXPERIENCE.md`
- UX compatibility handoff: `ux.md`

All required document types were found.

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

FR12: The REST API generator must discover command and query contracts and emit typed, OpenAPI-visible controllers that delegate to `IEventStoreGatewayClient` and forward canonical query metadata headers when the gateway supplies them. The generator test suite must cover discovery, routing conventions, diagnostics, generated output, query metadata headers, `304`, and safe problem-detail behavior. An accepted generated command must emit an absolute, gateway-authoritative command-status `Location` URI when the gateway supplies a valid target; it must omit `Location` when the target is absent, invalid, or unavailable rather than emit a relative or dangling external-host URI.

FR13: Generated REST controllers must live in dedicated external-facing API hosts, not interactive UI hosts; interactive UI hosts must consume EventStore client libraries directly.

FR14: The Sample proof must introduce a contracts-only Sample contracts library and an external Sample API host, move shared contracts there, and prove generated query and command controllers through that external API host.

FR15: The Tenants proof must move generated Tenants controllers to an external Tenants API host, while Tenants UI consumes client libraries and no longer hosts hand-written per-message controllers; any Tenants freshness, projection-version, ETag, or paging evidence shown by generated APIs or UI must come from the platform query metadata path.

FR16: The projection-changed transport must add an additive metadata-rich detail path with optional group scope, bounded metadata, scoped SignalR groups, DAPR notification support where needed, and preserved signal-only compatibility.

FR17: Live DAPR sidecar tests must be tagged and removed from the per-push release gate, then run in a dedicated integration workflow with sidecar warm-up and readiness retry.

FR18: `DaprETagService` must allow an overridable actor request timeout while preserving the production default.

FR19: Root-declared Git submodules must live under `references/`, and solution, project, documentation, Aspire metadata, and LLM instruction paths must resolve through the `references/` layout.

FR20: The Aspire Keycloak resource must be named `security` while preserving Keycloak as the implementation technology and updating fixtures/resource lookups accordingly.

FR21: Cross-repo Hexalith library dependencies use source project references only when `UseHexalithProjectReferences=true` is explicitly supplied and the root-declared source exists. An unset or explicit `false` value selects package references in every configuration, including Debug; Release and configuration-less evaluation therefore remain package-safe. Every source-owned NuGet dependency version used by a Hexalith repository must be declared in `references/Hexalith.Builds/Props/Directory.Packages.props`; consuming `Directory.Packages.props` files import that catalog and declare no local `PackageVersion`, version override, or fallback version property.

FR22: Commands used to restore, build, test, pack, and run semantic-release must assert package-reference mode and avoid packaging submodule projects.

FR23: Persisted events must receive non-zero, actor-allocated global positions; CloudEvent IDs must use the event `MessageId`; duplicate command replies must preserve the original command result fields.

FR24: The global-position allocation strategy must be renegotiated toward sharding per tenant or domain, and the frozen global-ordering spec must be updated before implementation.

FR25: EventStore workflows must use shared Hexalith.Builds security gates through `@main`, keep third-party actions SHA-pinned through shared workflows, and define NuGet package publish scope in `tools/release-packages.json`.

FR26: Phase 0 architecture remediation must close immediate safe fixes: clear staged state on infrastructure failure, protect anonymous admin endpoints, strip committed admin secrets, enforce production auth guards, add tenant-filter parity, gate admin Swagger, require destructive CLI confirmation, use ULID-safe admin correlation middleware, and correct stale test-baseline documentation.

FR27: Pipeline and idempotency correctness remediation must use exact command identity for resume; provide an EventStore-owned, tenant-scoped durable admission contract accepting only a trusted, versioned canonical-intent descriptor and fixed retention tier; reject live conflicting intent and return non-retryable `idempotency_key_expired` for any expired-key reuse before aggregate, domain, or external execution; separate replay-result retention from metadata-only consumed-key evidence; and never convert consumed, unavailable, corrupt, or unsafe legacy state into a fresh miss. Command status/archive identity, transient retryability, and tenant-before-state validation remain required.

FR28: Trust-boundary remediation must require app-layer credentials for internal, domain-service, projection-notification, and admin-computation endpoints, and must remove trust in wire-asserted administrator flags.

FR29: Replay and dispatch remediation must make event apply-method resolution boundary-safe and ambiguity-detecting, and must use one shared `JsonSerializerOptions` path for command, rehydrate, project, and pub/sub payload serialization.

FR30: Crash recovery remediation must detect events committed but not published and complete their publication, drain them, or recover them without requiring resubmission with the same correlation ID.

FR31: Append durability remediation must start with a live-sidecar two-writer race test and DAPR conflict-exception spike before choosing an optimistic-concurrency fencing design.

FR32: Runtime topology remediation must make the AppHost-loaded DAPR pub/sub, ACL, and key-prefix posture match the posture asserted by tests and production deploy templates.

FR33: Cost and evolution remediation must introduce folded snapshots, reduce projection replay cost, add projection sequence guards, support event schema versioning/upcasting, validate event metadata identity components, and add cancellation-token seams to published processing/query/projection interfaces.

FR34: Delivery, admin, and deployment remediation must document at-least-once unordered delivery, add poison/dead-letter handling, bound in-memory deduplication, normalize admin claims, audit every state-mutating admin action, hide deferred admin operations, add OpenBao-backed DAPR secret-store configuration for production operational and application secrets, require application retrieval through the DAPR Secrets API, restrict Kubernetes Secrets to documented bootstrap credentials only when no approved mounted or projected credential mechanism is available, add readiness/app-health checks, and restore meaningful IntegrationTests CI coverage.

FR35: Backlog capabilities must be tracked for GDPR aggregate erasure/tombstoning, Admin interactive OIDC login, an aggregate test kit, and REST generator hardening.

FR36: Before a consuming module deletes local projection/query infrastructure, EventStore must produce an owner-reviewed parity packet proving every required capability through production paths, record an approved runtime SHA, and require the consumer's checked-out EventStore SHA to match that approval.

FR37: EventStore must provide an optional shared payload-protection engine package built on `IEventPayloadProtectionService` and the existing provider-neutral metadata, outcome, workflow, and redaction contracts. The engine must implement the approved `pdenc-v2` format and byte-stable authenticated-data contract, preserve `json+pdenc-v1`, `json-redacted`, legacy-unprotected, and snapshot read compatibility, expose `IPersonalDataPolicy` and `IErasureStateProvider` extension seams, supply reusable key-lifecycle and resilience mechanics behind shared contracts, include at least one integration-proven production backend, and produce EventStore-owner plus Parties dual-provider parity and rollback evidence before G5 is available.

**Total FRs: 37**

### Non-Functional Requirements

NFR1: Security must fail closed for public, internal, domain-service, projection-notification, and admin surfaces; no endpoint may rely only on network posture or caller-supplied admin flags. The only anonymous exception is the health/liveness/readiness probe endpoints (`/health`, `/alive`, `/ready`), which are explicitly pinned `AllowAnonymous` and support-safe (AD-16); the fail-closed default is never weakened to reach probes.

NFR2: Tenant isolation must be preserved across state keys, actor IDs, topics, admin queries, generated REST APIs, SignalR groups, and deployment configuration. Tenant provisioning must reject the reserved `system` tenant name.

NFR3: Production authentication must reject insecure symmetric-key mode unless explicitly break-glassed, require HTTPS metadata where appropriate, and pin accepted JWT algorithms.

NFR4: Committed configuration must not contain forgeable administrator signing keys, credentials, bearer tokens, decoded JWT payloads, or other operational secrets.

NFR5: SignalR detail metadata must remain bounded and metadata-only; framework logs must not expose metadata values above Debug level.

NFR6: Event delivery semantics are at-least-once and unordered; subscribers must deduplicate by `MessageId` and order events only where domain semantics make `SequenceNumber` meaningful. Safety against duplicate and out-of-order delivery must be enforced and proven through the production projection dispatcher, handler, persistence, marker, and checkpoint path rather than only aggregate replay or transport-level tests.

NFR7: Event persistence and command processing must avoid silent data loss: staged-state flushes, stale pipeline records, append races, and committed-but-unpublished events must be explicitly guarded or recovered. Command processing must also prevent duplicate side effects across reservation, fencing, execution, recovery, expiry, compaction, restart, and concurrent hosts; a consumed key cannot become executable fresh work because its replay result expired or storage became unreadable.

NFR8: Snapshot and projection behavior must have a bounded cost model as streams grow, must avoid unnecessary full-stream replay when projections are already current, and must expose projection freshness/version evidence through platform query metadata when callers depend on lifecycle decisions; freshness/version evidence is authoritative only for query responses whose route provenance is projection-backed, and handler-computed or unknown-provenance responses must not be presented as authoritative lifecycle evidence. Paged rebuild output must equal canonical aggregate replay and must never overwrite a complete live model with page-only state.

NFR9: Release behavior must be reproducible and independent of local submodule checkout state; Release builds must use package references for external Hexalith libraries unless intentionally overridden.

NFR10: CI/CD must separate deterministic release-gate tests from live-sidecar/integration tests while preserving live-sidecar coverage in a dedicated lane.

NFR11: Package publishing must be manifest-driven and must not publish submodule packages or packages outside the EventStore release inventory.

NFR12: Backward compatibility must be preserved for additive framework changes such as SignalR signal-only projection notifications and existing generic gateway APIs.

NFR13: Generated code and source-generator packages must build cleanly under warnings-as-errors and must follow EventStore code style, nullable, ULID, and `ConfigureAwait(false)` rules.

NFR14: Interactive UI hosts must not expose generated or hand-written per-message MVC command/query controllers; UI command/query flows consume client libraries.

NFR15: Admin UX must not present deferred backup, restore, import, compaction, or other unavailable operations as functional; unavailable operations must be hidden/disabled or return `501`.

NFR16: Integration and higher-tier tests must assert persisted state-store/read-model/end-state evidence, not only HTTP status codes or mock call counts. Erasure, batch recovery, handler idempotency, and rebuild equivalence require persisted detail, index, marker, lifecycle, and checkpoint evidence through their production paths. Durable-admission evidence must inspect production-path state and prove restart survival, multi-host serialization, inclusive expiry boundaries, atomic tombstone compaction, leakage constraints, and zero downstream execution for replay, conflict, expired, corrupt, and unsafe legacy outcomes.

NFR17: Operational hardening must use the canonical DAPR `openbao` component for production operational and application secrets. Dependent DAPR components must use `secretKeyRef` with `auth.secretStore: openbao`; application code must use the DAPR Secrets API; and per-application access must be default-deny. OpenBao bootstrap credentials are platform inputs and may use Kubernetes Secrets only when no approved mounted or projected mechanism is available. Operational hardening must also support DAPR app-health checks, readiness-tagged health checks, resiliency targets, immutable image tags, and documented crypto-shred boundaries.

NFR18: AOT/trimming is explicitly not a target while reflection conventions remain load-bearing, and that constraint must be documented.

NFR19: Payload protection must fail closed and preserve byte-stable, versioned cryptographic semantics. Deleted, missing, denied, unavailable, malformed, tampered, and opaque states must remain bounded typed outcomes. Key material must be zeroed when no longer needed; caches must be invalidated on lifecycle changes; development-only backends must not start as production proof; and rollout, historical reads, downgrade, and rollback after writing the newest format must be integration-tested.

**Total NFRs: 19**

### Additional Requirements

#### Authority and artifact boundaries

- For Story 4.8, the approved 2026-07-20 OQ8 sprint-change proposal and the Architecture + Security + Test-approved OQ8 design version 1.0.0, SHA-256 `1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8`, govern. Earlier FR27, NFR7, NFR16, or architecture wording cannot weaken that authority.
- `prd.md` owns FR/NFR truth; `architecture.md` owns component, integration, topology, and decision-record gates; `ux.md` owns UI governance, user-flow evidence, and support-safe interaction rules; `epics.md` owns story slicing, sequencing, acceptance criteria, and implementation handoff.
- Full Phase 4 implementation should not resume until the PRD, architecture, UX, story splits, high-risk NFR traceability, and a new readiness assessment are complete.

#### Repository and build constraints

- Use `Hexalith.EventStore.slnx` only for restore and build.
- Run unit tests by project; do not make solution-level `dotnet test` the EventStore default.
- Keep every source-owned NuGet dependency version in `references/Hexalith.Builds/Props/Directory.Packages.props`; consuming `Directory.Packages.props` files only configure central package management and import the shared catalog.
- Keep the shared Builds catalog on the latest validated compatible versions from configured package sources. Prefer stable releases for stable pins; validate prerelease channels, aligned families, framework/SDK coupling, and major upgrades as units. Every retained exception requires its reason, evidence, and removal trigger; package search omissions or unlisting are not reasons to downgrade.
- Require explicit `UseHexalithProjectReferences=true` for source intent. Unset or explicit `false` means package intent in Debug, Release, and configuration-less evaluation.
- Use .NET SDK container support, not Dockerfiles.
- Publish the EventStore container as one immutable OCI image index containing exactly `linux/amd64` and `linux/arm64`; release validation must fail closed for any other manifest shape or platform set.
- Initialize only root-declared submodules under `references/`; never initialize nested submodules.

#### Identity and authorization constraints

- Message, correlation, causation, and EventStore aggregate identifiers use ULID-safe handling where EventStore envelope semantics require sortable unique IDs.
- `Guid.TryParse` is forbidden for `messageId`, `correlationId`, `aggregateId`, and `causationId`.
- Tenant access must be validated before status, idempotency, state, projection, admin, or generated REST data can disclose resource existence.
- Domain-service, internal, projection-notification, and admin-computation endpoints require app-layer credentials and must not trust caller-supplied administrator flags.

#### UI governance constraints

- UI-facing work must use FrontComposer and Blazor Fluent UI V5 components, preferring those components over raw CSS, raw HTML controls, JavaScript, or third-party controls.
- Theme primitives must not be redefined.
- Multi-section page-like surfaces use `FluentAccordion` with the primary section expanded by default.
- UI states must remain support-safe and never render tokens, decoded JWT payloads, raw EventStore metadata, raw payloads, stack traces, cursor internals, or ETag internals.
- Sample UI command submission demonstrates accepted submission, not downstream completion. Tenants UI preserves projection-confirmed success.
- Admin UI hides or disables deferred operations; any remaining endpoint returns `501`.

#### Scope and sequencing constraints

- Phase 4 MVP includes Epics 1-7 and FR1-FR36/NFR1-NFR18.
- Epic 8, FR37, and NFR19 are committed post-MVP scope and do not block Phase 4 MVP completion.
- GDPR aggregate/event tombstoning, broker-history deletion, backup erasure, audit-record deletion, and provider/operator key-custody operations remain outside MVP under GDPR-1; generic projection read-model/checkpoint erasure remains in scope under FR5.
- Admin interactive OIDC login, aggregate test-kit implementation, and REST-generator hardening beyond the approved Epic 2 proof remain backlog-only for MVP.
- AOT/trimming remains out of scope while reflection conventions are load-bearing.
- Generated REST controllers remain outside interactive UI hosts, and HTTP `202`, SignalR notification, or command acceptance never establish projection-confirmed UI success.
- Stories 6.1, 6.3, and 6.5 must produce approved specifications at the named implementation-artifact paths before Stories 6.2, 6.4, and 6.6 begin.
- Parties projection/query parity remains blocked until Stories 1.14-1.19 are complete and reviewed and Story 1.20 records an owner-approved `available` packet tied to the exact consumed EventStore runtime SHA.
- Parties payload-protection G5 remains blocked until Story 8.1 approves the security specification and Story 8.2 supplies the owner/security-approved availability packet, exact identities, compatibility evidence, and rollback evidence.

#### Assumptions and open questions

- The PRD declares no inline assumptions and no open PRD-level ownership or MVP-scope questions.
- Remaining payload-protection design decisions are intentionally delegated to Story 8.1 and its approved security specification.

### PRD Completeness Assessment

The PRD is complete as a requirements baseline: all identifiers FR1-FR37 and NFR1-NFR19 are present, full requirement text is explicit, MVP versus committed post-MVP scope is separated, governing authority is stated, and the document includes source-level FR-to-epic and high-risk NFR-to-story traceability. No requirement-number gaps exist despite the source tables presenting some FRs out of numeric order.

Clarity is generally high because requirements include named seams, paths, behaviors, failure postures, and done evidence. Several FRs and NFRs are deliberately compound—especially FR4, FR5, FR7, FR12, FR21, FR27, FR34, FR37, NFR7, NFR16, NFR17, and NFR19—so implementation readiness depends on the epic/story artifact preserving each constituent obligation rather than merely citing the parent identifier. Spec-first gates appropriately defer design detail for global ordering, durable idempotency, bounded-cost/evolution, and payload protection. Epic and story coverage is evaluated in the next step rather than assumed from the PRD's own traceability tables.

## Epic Coverage Validation

### Epic FR Coverage Extracted

- Epic 1 claims FR1-FR10 and FR36.
- Epic 2 claims FR11-FR16.
- Epic 3 claims FR17-FR22 and FR25.
- Epic 4 claims FR23, FR24, FR27, FR29, FR30, and FR31.
- Epic 5 claims FR26, FR28, and FR32.
- Epic 6 claims FR33.
- Epic 7 claims FR34 and FR35.
- Epic 8 claims FR37 as committed post-MVP scope.

**Total distinct FRs claimed in epics: 37**

### Coverage Matrix

| FR Number | PRD Requirement | Epic and story coverage | Status |
| --- | --- | --- | --- |
| FR1 | Domain modules are domain-centric while EventStore supplies platform boilerplate. | Epic 1; Stories 1.1, 1.11 | ✓ Covered |
| FR2 | Provide the canonical domain-service SDK host extensions. | Epic 1; Story 1.1 | ✓ Covered |
| FR3 | Map the five canonical DAPR-facing domain-service endpoints. | Epic 1; Story 1.1 | ✓ Covered |
| FR4 | Provide query-handler discovery/routing, operational metadata, complete response-metadata propagation, provenance, and authoritative six-state lifecycle handling. | Epic 1; Stories 1.2, 1.9, 1.13, 1.16; supporting Story 2.7 | ✓ Covered |
| FR5 | Provide persisted read-model lifecycle/write contracts, ETag behavior, coordinated erasure, and recoverable detail/index batching. | Epic 1; Stories 1.3, 1.4, 1.9, 1.13, 1.14, 1.15 | ✓ Covered |
| FR6 | Provide a DataProtection-backed, scoped, bounded, tamper-safe query cursor codec. | Epic 1; Stories 1.5, 1.9, 1.13 | ✓ Covered |
| FR7 | Provide asynchronous multi-projection handling and generic domain-event consumption with production-path duplicate/out-of-order safety and replay-equivalent rebuilds. | Epic 1; Stories 1.6, 1.10, 1.13, 1.17, 1.18, 1.19 | ✓ Covered |
| FR8 | Provide domain-module Aspire, telemetry, and DAPR health-check extensions. | Epic 1; Story 1.7 | ✓ Covered |
| FR9 | Migrate Sample and Tenants to the platform seams and remove duplicated infrastructure. | Epic 1; Stories 1.8, 1.9, 1.10, 1.11, 1.13 | ✓ Covered |
| FR10 | Publish DomainService and ServiceDefaults and restrict publishing to the manifest inventory. | Epic 1; Stories 1.11, 1.12 | ✓ Covered |
| FR11 | Provide the REST source-generator contract seam. | Epic 2; Stories 2.1, 2.4 | ✓ Covered |
| FR12 | Generate typed OpenAPI controllers with gateway delegation, metadata/`304`/problem-details tests, and safe absolute-or-absent command-status `Location`. | Epic 2; Stories 2.2, 2.9, 2.11 | ✓ Covered |
| FR13 | Keep generated controllers in dedicated external API hosts and UI hosts on client libraries. | Epic 2; Stories 2.3, 2.5, 2.6, 2.10; supporting Epic 7 Story 7.14 | ✓ Covered |
| FR14 | Add Sample contracts-only library and dedicated external API host proof. | Epic 2; Stories 2.3, 2.10 | ✓ Covered |
| FR15 | Move Tenants controllers to an external host and preserve platform-owned query metadata through API/UI consumption. | Epic 2; Stories 2.4-2.7, 2.11, 2.12; supporting Stories 4.7, 7.14 | ✓ Covered |
| FR16 | Add bounded, optionally scoped metadata-rich projection notifications while preserving signal-only compatibility. | Epic 2; Story 2.8 | ✓ Covered |
| FR17 | Re-tier live-sidecar tests into a dedicated integration workflow with readiness/warm-up behavior. | Epic 3; Stories 3.1, 3.10 | ✓ Covered |
| FR18 | Make the DAPR ETag actor timeout overridable while retaining the production default. | Epic 3; Story 3.2 | ✓ Covered |
| FR19 | Place root submodules under `references/` and align all repository paths. | Epic 3; Story 3.3 | ✓ Covered |
| FR20 | Rename the Aspire Keycloak resource to `security` without changing the implementation technology. | Epic 3; Story 3.4 | ✓ Covered |
| FR21 | Enforce explicit source opt-in, package-safe defaults, and Builds-owned package versions. | Epic 3; Stories 3.5, 3.11; supporting Story 2.12 | ✓ Covered |
| FR22 | Make restore/build/test/pack/release commands assert package mode and avoid submodule packaging. | Epic 3; Stories 3.6, 3.8, 3.11, 3.12; supporting Story 2.12 | ✓ Covered |
| FR23 | Assign non-zero actor global positions, use event `MessageId` as CloudEvent ID, and preserve duplicate-result fidelity. | Epic 4; Story 4.1 | ✓ Covered |
| FR24 | Renegotiate and update the frozen global-ordering spec toward tenant/domain sharding before implementation. | Epic 4; Story 4.6 | ✓ Covered |
| FR25 | Use shared Builds security gates, SHA-pinned third-party actions through shared workflows, and manifest-driven NuGet scope. | Epic 3; Stories 3.7, 3.8, 3.9, 3.11, 3.12 | ✓ Covered |
| FR26 | Close Phase 0 security and safe-remediation fixes across staged state, admin auth/secrets/filters/Swagger/CLI/correlation, and documentation. | Epic 5; Stories 5.1-5.4; supporting Story 2.10 | ✓ Covered |
| FR27 | Enforce exact resume identity and tenant-scoped durable admission with conflict, expiry, consumed-state, archive/status, retry, and tenant-before-state guarantees. | Epic 4; Stories 4.2, 4.8; forward-compatible support in Story 2.9 | ✓ Covered |
| FR28 | Require app-layer internal credentials and eliminate wire-asserted administrator trust. | Epic 5; Story 5.5; supporting Story 2.10 | ✓ Covered |
| FR29 | Make apply-method resolution boundary-safe/ambiguity-detecting and unify serializer options across processing paths. | Epic 4; Story 4.3 | ✓ Covered |
| FR30 | Recover committed-but-unpublished events without same-correlation resubmission. | Epic 4; Story 4.4 | ✓ Covered |
| FR31 | Establish live-DAPR two-writer/conflict evidence before choosing append fencing. | Epic 4; Story 4.5 | ✓ Covered |
| FR32 | Align AppHost-loaded DAPR components, ACLs, prefixes, tests, and deployment templates. | Epic 5; Stories 5.6-5.9 | ✓ Covered |
| FR33 | Deliver spec-gated folded snapshots, projection-cost/sequence guards, event versioning/upcasting, identity validation, and cancellation seams. | Epic 6; Stories 6.1-6.6; correctness support in Story 1.19 | ✓ Covered |
| FR34 | Deliver delivery/dead-letter/dedup, claims/audit/admin honesty, OpenBao/app-health/resiliency/images, and meaningful integration coverage. | Epic 7; Stories 7.1-7.14; supporting Stories 2.6, 2.11, 3.10 | ✓ Covered |
| FR35 | Track four explicit deferred backlog capabilities. | Epic 7; Stories 7.15-7.18 | ✓ Covered |
| FR36 | Produce owner-reviewed production-path projection/query parity and bind consumer adoption to exact approved EventStore runtime identity. | Epic 1; Stories 1.2-1.4, 1.9, 1.10, 1.14-1.20 | ✓ Covered |
| FR37 | Deliver the optional shared payload-protection engine, stable formats, extension seams, production backend, parity, and rollback proof. | Epic 8; Stories 8.1, 8.2 | ✓ Covered |

### Missing Requirements

No PRD functional requirement is missing from the epics and stories document. No functional requirement identifier is present in the epic coverage map that is absent from the PRD.

### Coverage Statistics

- Total PRD FRs: 37
- FRs covered in epics: 37
- Missing PRD FRs: 0
- Extra epic FR identifiers: 0
- Coverage: 100%

The coverage result validates existence of an implementation path only. It does not yet judge story sizing, dependency direction, acceptance-criteria quality, or whether compound requirements are decomposed safely; those checks occur in later workflow steps.

## UX Alignment Assessment

### UX Document Status

**Found.** The canonical UX source is the sharded set rooted at `ux-designs/ux-eventstore-2026-07-05/index.md`, with `DESIGN.md` and `EXPERIENCE.md` as the binding detailed contracts. The top-level `ux.md` is a compatibility handoff to that set.

### UX ↔ PRD Alignment

The UX and PRD align on the following requirements and user outcomes:

- Interactive UI hosts consume typed client libraries and do not host generated or hand-written per-message MVC command/query controllers (FR13, FR15, NFR14).
- Sample submission presents command acceptance and evidence-pending state rather than downstream completion.
- Tenants and Admin success requires projection/read-model evidence; HTTP `202` and SignalR are not completion proof.
- The projection lifecycle remains distinguishable as `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`, with `Unknown` as the fail-safe state when provenance is not authoritative.
- Deferred backup, restore, import, compaction, and other unavailable capabilities are hidden, disabled with honest copy, or backed by `501` rather than simulated as functional (FR34, NFR15).
- Tenant isolation, access denial, protected payloads, admin audit outcomes, and operational failures use support-safe states without revealing resource existence or sensitive internals.
- The UI uses FrontComposer and Fluent UI Blazor V5, avoids theme redefinition and raw interactive controls, and uses `FluentAccordion` for multi-section surfaces.
- Accessibility, localization, responsive behavior, stable selectors, and keyboard/focus/live-region behavior refine the PRD's explicit UI governance and accessibility/localization evidence concerns without contradicting product scope.
- UX journeys cover the PRD's target administrators, platform operators, domain/sample developers, and tenant administrators through incident triage, access review, command investigation, deferred-operation discovery, Sample acceptance, and Tenants projection confirmation.

No UX behavior contradicts the PRD. Detailed dashboard tabs, responsive breakpoints, component patterns, microcopy, live-region priorities, and visual tokens are legitimate UX-level elaboration of PRD requirements rather than unplanned product scope.

### UX ↔ Architecture Alignment

- AD-3/AD-4 and the architecture dependency map support the UX typed-client boundary and prohibit controller ownership in interactive hosts.
- AD-8 supports the UX rule that SignalR is a freshness nudge and projection/read-model evidence is required for visible success.
- AD-14/AD-15 and the projection lifecycle convention support the UX freshness indicator, provenance gating, six authoritative lifecycle states, and `Unknown` fallback.
- AD-10/AD-16 support fail-closed authorization, support-safe denial, and bounded anonymous probe behavior used by operational status surfaces.
- AD-21 selects `src/Hexalith.EventStore.Admin.UI` as the single consolidated UI, retains `eventstore-admin-ui`, provides the stable `event-store-admin` / **Event Store Admin** module identity, preserves canonical deep links, redirects non-canonical legacy routes, and prohibits an additional UI host.
- The architecture stack and dependency diagram explicitly provide FrontComposer Shell, FrontComposer Contracts.UI, and Fluent UI Blazor V5, supporting the UX component inventory.
- Story 7.14, referenced by AD-21, provides component/route, dependency-mode, accessibility, localization, responsive, evidence-state, and typed-client validation for the consolidated dashboard.
- Architecture consistency conventions explicitly require UI success to be projection-confirmed, support-safe, accessible, and localized.

No required UX component or interaction depends on an architecture capability that is absent from the architecture spine.

### Alignment Issues

1. **Minor wording ambiguity — UI host evolution.** UX describes the “future EventStore UI service” as the target and legacy `Admin.UI` as source evidence. AD-21 later makes the ownership decision explicit: `src/Hexalith.EventStore.Admin.UI` evolves in place and no new EventStore UI host is created. The artifacts are operationally compatible, but the UX wording should eventually name the in-place evolution to remove the possibility of interpreting “future service” as a second host.
2. **Minor source-traceability omission.** Architecture references canonical UX behavior and delegates detailed flows to the UX artifacts, but its frontmatter `sources` list does not include `ux.md`, `DESIGN.md`, or `EXPERIENCE.md`. This does not create a design gap because AD-21 and the consistency conventions implement the relevant boundaries, but adding the canonical UX sources would make traceability explicit.

### Warnings

- Quantitative EventStore UI performance/load-time budgets are intentionally absent. Architecture explicitly defers numerical gates until a measured production baseline exists, while retaining responsive-layout, accessibility, evidence-state, and support-safety obligations. This is a documented non-blocking gap, not a current contradiction.
- The UX index is dated 2026-07-09 while `DESIGN.md`, `EXPERIENCE.md`, and the top-level handoff reflect the 2026-07-11 lifecycle correction. The canonical detailed documents contain the corrected states, so no requirement is missing; updating the index date would reduce audit ambiguity.

## Epic Quality Review

### Review Scope

The review covered all 8 epics and all 87 active story sections in `epics.md`, including epic value, epic ordering, explicit dependency gates, story sizing, acceptance-criteria structure, failure-path coverage, brownfield integration, persistence timing, and FR/NFR traceability.

### Epic Compliance Summary

| Epic | User-value outcome | Independence / ordering | Story sizing | Acceptance criteria | Traceability |
| --- | --- | --- | --- | --- | --- |
| Epic 1 — Domain Author Self-Service Platform | Pass: domain authors and consuming modules receive reusable platform seams and parity closure. | **Fail on deployed mode:** Story 1.20 requires later Epic 3 Story 3.12. | Mostly focused; closure story is large but evidence-oriented. | Strong BDD and failure-path coverage. | FR1-FR10 and FR36 present. |
| Epic 2 — External Integration Surfaces | Pass: external API developers and UI developers receive distinct, safe integration paths. | **Fail:** Story 2.6 cites later Story 2.11 as the exclusive owner of evidence required by its own acceptance. | Generally focused. | Strong and specific. | FR11-FR16 present. |
| Epic 3 — Release And Repository Reliability | Pass for the release-maintainer user: reproducible, governed publication is an operational capability, not merely setup. | Pass against later-epic direction; it may use Epic 1/2 outputs. | **Concern:** Stories 3.5 and 3.11 span shared catalog plus multiple repositories/families and external approvals. | Strong but scope is open-ended in the catalog stories. | FR17-FR22 and FR25 present. |
| Epic 4 — Event Correctness And Recovery | Pass: operators and consumers receive durable correctness and recovery behavior. | Pass against later epics. | **Fail:** Story 4.8 is explicitly multi-slice and epic-sized. | Detailed, but Story 4.8 delegates a large behavior matrix to its governing design. | FR23, FR24, FR27, FR29-FR31 present. |
| Epic 5 — Security And Tenant Isolation | Pass: tenants and operators receive fail-closed protection. | Pass. | Focused child stories. | Strong security and negative-path criteria. | FR26, FR28, FR32 present; one NFR2 clause is missing from story ACs. |
| Epic 6 — Bounded Cost And Event Evolution | Pass at epic outcome level. | Pass; spec gates precede their implementation stories and depend only on earlier work. | Runtime stories are focused; Stories 6.1, 6.3, and 6.5 are enablement/spec gates rather than independently usable runtime increments. | Specific, with one non-quantified “bounded size” criterion. | FR33 present. |
| Epic 7 — Operator Trust, Admin Honesty, And Future Capabilities | Individual stories contain user/owner value, but the epic combines several distinct outcome families. | Pass against future dependencies. | **Concern:** 18 stories; Story 7.14 is a broad whole-dashboard migration. | Strong overall; Story 7.6 alone is not in Given/When/Then form. | FR34-FR35 present. |
| Epic 8 — Shared Payload Protection | Pass: security owners and domain modules receive a reusable protection capability. | Pass; Story 8.2 correctly follows Story 8.1 and earlier epics. | **Fail:** Story 8.2 is explicitly multi-slice and epic-sized. | Comprehensive but too broad for one review boundary. | FR37/NFR19 present. |

### 🔴 Critical Violations

#### CRIT-1 — Epic 1 has a forward cross-epic dependency

Story 1.20 states that deployed-mode completion requires Story 3.12 and that Epic 1 cannot become done on that path until Epic 3 supplies a conforming release. This directly violates the rule that Epic 1 must stand alone and Epic N cannot require Epic N+1.

**Impact:** Epic status is mode-dependent and can be reopened by later-epic work; sequencing and completion reporting cannot be interpreted monotonically.

**Recommendation:** Limit Story 1.20/Epic 1 closure to source/package parity identities that Epic 1 can prove independently. Move deployed-image parity into a distinct post-3.12 closure story in Epic 3 or a later consumer-adoption epic. That later story may consume the completed Epic 1 parity packet but must not gate Epic 1 itself.

#### CRIT-2 — Story 2.6 depends on later Story 2.11

Story 2.6 requires the typed-client boundary to supply an evidence state “under the Story 2.11 provenance contract” and directs its completion evidence to cite Story 2.11, while Story 2.11 appears later in the epic and exclusively owns the required provenance proof.

**Impact:** Story 2.6 is not independently completable in sequence and cannot be reviewed truthfully before Story 2.11.

**Recommendation:** Move the Story 2.11 provenance-consumption work before Story 2.6, or remove provenance-dependent acceptance from Story 2.6 and place the complete integration/presentation slice in the later story. Renumbering must update the migration crosswalk and evidence references.

#### CRIT-3 — Story 4.8 is an epic-sized story

Story 4.8 explicitly declares itself multi-slice. It combines trusted canonical-intent adapters, protected identity derivation, actor serialization and fencing, authorization precedence, replay/expiry policy, atomic tombstone compaction, digest rotation/directory promotion, legacy migration, multi-host PostgreSQL+DAPR proof, and a final approval packet.

**Impact:** The story cannot be implemented, tested, reviewed, or rolled back as one bounded independent increment. Task slices inside one story do not create independent completion/review boundaries.

**Recommendation:** Split it into separately reviewable stories for contract/trusted adapter, admission actor and current fencing, expiry/tombstone retention, digest-directory rotation, legacy migration, production multi-host evidence, and final OQ8 closure. Preserve the approved design digest and use a final closure story that consumes completed evidence rather than carrying implementation.

#### CRIT-4 — Story 8.2 is an epic-sized story

Story 8.2 explicitly postpones decomposition while combining engine/package creation, `pdenc-v2` cryptography, backward readers, key lifecycle, domain extension seams, a production backend, no-leak verification, release-inventory mutation, Parties dual-provider parity, rollback after v2 writes, and final G5 approval.

**Impact:** One story spans multiple packages, a provider boundary, a consuming repository, release governance, security review, and rollback. It lacks the independent review and failure isolation required for implementation readiness.

**Recommendation:** Author the promised decomposition now, before implementation readiness is granted: core format/engine, compatibility readers, key lifecycle, production adapter conformance, package/release integration, Parties parity, rollback rehearsal, and final G5 proof packet. Story 8.1 remains the approval gate; each child receives its own owner, tests, and rollback boundary.

### 🟠 Major Issues

#### MAJ-1 — NFR2's reserved-tenant rule has no story acceptance criterion

NFR2 explicitly requires tenant provisioning to reject the reserved `system` tenant name. No story text or acceptance criterion in `epics.md` contains that rule, even though the PRD's NFR map cites Stories 2.5, 5.2, 5.5, and 5.6.

**Impact:** A concrete tenant-isolation boundary can be omitted while NFR2 appears mapped at identifier level.

**Recommendation:** Add a focused acceptance criterion and negative test to the story that owns tenant provisioning validation. Require rejection before persistence or downstream invocation, support-safe output, and proof that no `system` tenant state is created.

#### MAJ-2 — Stories 3.5 and 3.11 have open-ended cross-repository scope

Story 3.5 remains `in-progress` whenever any affected repository lacks authorization or migration, and Story 3.11 refreshes the shared catalog across every changed family plus representative consumers. Neither story enumerates a closed repository/family set in its identity.

**Impact:** Completion depends on external maintainers and a scope that can expand as the catalog changes, undermining independent completion and rollback.

**Recommendation:** Separate the Builds-owned catalog/platform change from one consumer-adoption story per repository or explicitly bounded consumer group. Split the refresh into version-family changes with independent validation and rollback. Keep unresolved repositories in named follow-up stories rather than holding one story open indefinitely.

#### MAJ-3 — Epic 7 is an omnibus epic and Story 7.14 is too broad

Epic 7 contains 18 stories spanning delivery/dead letters, Admin authorization/audit/client behavior, OpenBao, health/resiliency/images, integration CI/test classification, a full Admin dashboard migration, and four unrelated backlog products. Story 7.14 itself covers in-place host migration, dependency modes, canonical and legacy routes, all evidence states, accessibility, localization, responsiveness, and support safety.

**Impact:** Epic completion does not represent one cohesive user outcome, and Story 7.14 lacks a bounded UI/route inventory suitable for one implementation and review boundary.

**Recommendation:** Split Epic 7 into cohesive outcome epics or explicit release trains: delivery/recovery operations, Admin trust and UX, deployment/secret/health hardening, test-evidence recovery, and future-product backlog. Split Story 7.14 into shell/route migration, typed-client and evidence-state integration, and accessibility/localization/responsive conformance, or supply a closed route/component inventory and staged review boundaries.

#### MAJ-4 — Story 8.2 conflicts with the repository's shared-entry-point governance

Story 8.2 requires package inventory changes to update `AGENTS.md`. The applicable repository instructions require `AGENTS.md`, `CLAUDE.md`, and `.github/copilot-instructions.md` to remain synchronized universal baselines and direct repository-specific configuration to repository documentation or configuration instead.

**Impact:** Implementing the acceptance criterion literally would violate the governing repository instructions and risk desynchronizing shared entry points.

**Recommendation:** Replace `AGENTS.md` in the acceptance criterion with authoritative release inventory/configuration (`tools/release-packages.json`), package-governance tests, project context, and repository-specific release documentation. Update the shared entry points only if intentionally changing their normalized universal baseline across all three files.

#### MAJ-5 — Story 2.10 and AD-18 describe a stale registration API

Story 2.10 and AD-18 say routing-header ownership is wired through `AddEventStoreGatewayClient(appId, apiToken?)`. The current repository baseline and implementation use a separate chained `AddEventStoreDaprServiceInvocation(appId, apiToken)` call; `AddEventStoreGatewayClient` registers the typed client and command-status builder only. Current source and tests explicitly enforce the separate registration.

**Impact:** The plan directs future implementation/review toward an API shape that no longer matches the code or the foundational project context, risking removal of the explicit opt-in and handler-order guarantee.

**Recommendation:** Reconcile Story 2.10 and AD-18 to the current two-step registration contract, including the requirement that `AddEventStoreDaprServiceInvocation` be registered last/innermost and that omission remain visible to guardrail validation.

#### MAJ-6 — Spec-only Story 6.1/6.3/6.5 are enablement milestones, not usable increments

These stories explicitly produce approved documents and state that they do not count as runtime implementation progress. The spec-first gates are necessary, but they do not independently deliver the epic's user outcome.

**Impact:** Story completion metrics can overstate delivered bounded-cost/evolution capability.

**Recommendation:** Retain them as explicitly typed enabler/gate work outside user-value completion reporting, or make each spec approval a required task/milestone attached to its implementation story while preserving an independent approval boundary. Epic 6 must not be reported as delivering runtime value until Stories 6.2, 6.4, and 6.6 complete.

### 🟡 Minor Concerns

1. **BDD formatting:** Story 7.6 uses eight numbered declarative criteria instead of Given/When/Then. The criteria are specific and testable, but converting them would align the only formatting outlier among 87 stories.
2. **Copied requirement drift:** The epics “Requirements Inventory” shortens authoritative PRD text for FR12, FR22, FR27, FR30, FR34, NFR2, NFR6-NFR8, NFR16, and NFR17. Later stories recover most omitted clauses, but copied summaries invite future traceability errors. Remove the duplicate text or synchronize it exactly; retain the PRD as the sole textual authority.
3. **Identifier migration ambiguity:** The migration section says the former Story 1.6 is superseded by Stories 1.8-1.11 while an active Story 1.6 with different projection/consumer scope exists. The separate crosswalk may resolve this, but the epics document should explicitly label identifier reuse to avoid incorrect status inheritance.
4. **Non-measurable bounds:** Story 6.2 says snapshot payload size remains “bounded” without stating the measurable invariant or growth relationship. Story 7.14 similarly quantifies neither the canonical route inventory nor required responsive/accessibility cases. Add a concrete invariant or closed fixture inventory.

### Best-Practices Passes

- Every epic states an identifiable user, operator, maintainer, or product-owner outcome; none is merely “setup database” or “create models.”
- Apart from the two explicit forward-dependency defects, dependencies generally point backward: spec stories precede implementation, platform seams precede consumers, and optimization follows correctness.
- Persistence artifacts are introduced with the stories that need them; no story creates all tables/state structures upfront.
- Architecture explicitly states that no greenfield starter template is mandated, so no missing starter-template story exists.
- The plan correctly treats the repository as brownfield: it contains migration, compatibility, package/source-mode, legacy route, current-host, and cross-repository approval boundaries.
- Acceptance criteria are unusually specific overall and commonly include invalid input, denial, partial failure, cancellation, retry, persisted end state, and support-safe diagnostics.
- Functional traceability remains 37/37 despite the story-quality violations.

## Summary and Recommendations

### Overall Readiness Status

## NOT READY

The planning baseline is strong on requirements completeness, functional traceability, UX detail, architectural invariants, negative-path criteria, and persisted-evidence expectations. However, it is not implementation-ready because the active MVP path contains forbidden forward dependencies and epic-sized stories that cannot be completed or reviewed as independent increments.

This status does not arise from missing PRD/architecture/UX artifacts or missing FR identifiers: all required documents exist and FR coverage is 37/37. It arises from the implementation structure and from a small number of requirements/governance contradictions that can cause incorrect execution despite nominal traceability.

### Critical Issues Requiring Immediate Action

1. **Remove Story 1.20's dependency on later Epic 3 Story 3.12.** Deployed-image parity must become a post-3.12 closure story; it cannot gate Epic 1.
2. **Remove Story 2.6's dependency on later Story 2.11.** Reorder provenance consumption before UI presentation or move all provenance-dependent acceptance into the later story.
3. **Decompose Story 4.8 now.** Trusted intent, admission/fencing, expiry/tombstones, rotation, migration, production evidence, and closure require separate story/review boundaries.
4. **Decompose Story 8.2 before post-MVP implementation.** Core engine, compatibility, key lifecycle, production adapter, packaging, Parties parity, rollback, and G5 closure cannot remain one story.

### Recommended Next Steps

1. Correct the dependency graph and update the story migration crosswalk, story identifiers, status inheritance, and every affected reference.
2. Create focused child stories for 4.8 and 8.2, each with one owner, one review boundary, explicit inputs/outputs, focused persisted-state/security evidence, and independent rollback.
3. Add the missing NFR2 acceptance criterion that rejects reserved tenant name `system` before persistence or downstream execution and proves no state is created.
4. Reconcile Story 2.10 and architecture AD-18 with the implemented two-step client registration: `AddEventStoreGatewayClient(...)` followed by innermost `AddEventStoreDaprServiceInvocation(...)`.
5. Remove the Story 8.2 requirement to update repository-specific package inventory in `AGENTS.md`; bind inventory to release configuration/tests and repository-specific documentation instead.
6. Bound Stories 3.5 and 3.11 by explicit repository/package-family inventories and split consumer adoption into independently authorized stories.
7. Split Epic 7 and Story 7.14 into cohesive delivery, Admin UX/trust, deployment hardening, test-evidence, and backlog outcomes, or provide closed inventories and staged review boundaries.
8. Treat Stories 6.1, 6.3, and 6.5 as enabler/approval gates rather than delivered runtime-value increments, while preserving the mandatory spec-first rule.
9. Resolve documentation cautions: clarify in-place `Admin.UI` evolution in UX, add canonical UX artifacts to architecture sources, refresh the UX index date, synchronize copied requirement text or remove it, clarify reused story identifiers, convert Story 7.6 to BDD form, and quantify bounded fixture/inventory expectations.
10. Re-run implementation readiness after corrections. Do not begin broad Phase 4 implementation until the critical dependency and sizing defects are absent from the active plan.

### Final Note

This assessment identified 18 findings across four categories: dependency direction, story/epic sizing and cohesion, requirements/governance traceability, and document alignment. Four are critical, six are major, four are minor, and four are UX/documentation cautions. Address the four critical violations before proceeding with broad implementation; the major issues should be corrected in the same planning pass because two conflict directly with the current repository baseline.

**Assessment date:** 2026-08-01  
**Assessor:** Codex, acting as implementation-readiness Product Manager and requirements-traceability reviewer
