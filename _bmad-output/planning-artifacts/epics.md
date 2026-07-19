---
stepsCompleted:
  - step-01-validate-prerequisites
  - step-02-design-epics
  - step-03-create-stories
  - step-04-final-validation
inputDocuments:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-02.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-20-ai-response-progress-transport.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-ci-release-retier.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-26-submodule-references.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-26.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-29.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02-global-event-ordering.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02-rest-api-external-host.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-04.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-query-metadata-propagation.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-query-metadata-sequencing.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-09.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-09-implementation-readiness-corrections.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-13.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-17.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18-story-3-5-reconciliation.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md
  - _bmad-output/specs/spec-eventstore-phase-4-readiness-recovery/SPEC.md
---

# eventstore - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for eventstore, decomposing the formal PRD, architecture, UX handoff, and approved sprint change proposals into implementable stories.

The current Phase 4 planning baseline is `_bmad-output/planning-artifacts/prd.md`, `_bmad-output/planning-artifacts/architecture.md`, `_bmad-output/planning-artifacts/ux.md`, `_bmad-output/specs/spec-eventstore-phase-4-readiness-recovery/SPEC.md` with its companions, and the approved sprint change proposals in `_bmad-output/planning-artifacts`. The PRD owns FR/NFR truth, the architecture artifact owns implementation invariants and decision gates, the UX handoff owns UI governance and journeys, the SPEC preserves the complete downstream contract, and this epics document owns implementation slicing and story acceptance criteria. PRD section 7 is authoritative whenever copied NFR text drifts.

## Implementation Readiness Execution Gates

The 2026-07-15 implementation readiness assessment found complete FR1-FR36 traceability but blocked broad Phase 4 implementation on a later-epic prerequisite and eight oversized implementation stories. The approved July 15 direct adjustment preserves the seven-epic MVP while replacing those parents with focused children.

### Query Metadata Sequencing Gate

The platform-owned query metadata propagation contract must be implemented by the earliest stories that depend on it, not by a later Epic 7 story.

- Story 1.2 owns platform result metadata propagation, gateway merge rules, freshness policy enforcement, and typed client metadata exposure.
- Stories 1.3-1.5 separately own persisted read models, deterministic testing, and authoritative protected-cursor/paging behavior.
- Story 2.2 owns generated REST query metadata headers, `304` behavior, and safe problem-detail behavior.
- Stories 2.4-2.7 own Tenants contract, external-host, UI, and the pre-authorization registration/provenance proof; Story 2.12 owns the Story 1.20-authorized source/package adoption proof.
- Stories 7.15-7.18 remain backlog-artifact work only.

The former query-metadata Story 7.6 remains superseded; its acceptance criteria stay with the earlier platform/consumer stories, while the approved July 15 plan assigns identifier 7.6 to the focused secret-store child and records that reuse in the migration crosswalk.

### Query Response Provenance Gate

Query-response provenance is governed by architecture invariants AD-14 and AD-15. Story 1.2 owns the EventStore platform contract, route stamping, route-aware gateway enforcement, typed-client propagation, and real-gateway-path evidence before any consumer may claim current/stale projection state. Story 2.11 owns generated REST and Tenants consumption only.

No UI or generated-API story may render current/stale state or projection version unless provenance is `ProjectionBacked`; handler-computed and unknown routes render `Unknown`. Story 4.7 remains the maintainer-approved Tenants producer follow-up and does not block the EventStore platform prerequisite.

### Focused Story And Migration Gates

The former coordinated-slice parents are superseded and must not be recreated as active implementation stories. Their replacements are:

- Story 1.3 -> Stories 1.3-1.5.
- Story 1.6 -> Stories 1.8-1.11.
- Story 2.4 -> Stories 2.4-2.7.
- Story 2.7 -> retained pre-authorization scope in Story 2.7 plus authorized adoption in Story 2.12.
- Story 3.7 -> Stories 3.7-3.9.
- Story 5.6 -> Stories 5.6-5.9.
- Story 7.2 -> Stories 7.2-7.5.
- Story 7.3 -> Stories 7.6-7.9.
- Story 7.4 -> Stories 7.10-7.13.
- Story 7.5 -> Stories 7.15-7.18, with new Story 7.14 owning the consolidated EventStore Admin dashboard migration.

Every child names one owner, one review boundary, deterministic acceptance criteria, and focused validation. `_bmad-output/planning-artifacts/story-id-migration-2026-07-15.md` is the audit authority for old/new identifiers, status inheritance, evidence, and active-file supersession. A child inherits `done` only when that crosswalk names existing implementation, focused tests, and review results. Tenants children that cross the Tenants repository boundary additionally require maintainer approval and an exact SHA; the EventStore-only Story 2.7 prerequisite does not. Missing evidence leaves a child in `review`, not `done`.

| Child stories | Owner / review boundary | Focused validation |
| --- | --- | --- |
| 1.3-1.5 | Amelia / Murat reviews store, fake, and cursor contracts independently | Client, Testing, DomainService, and focused Server query tests |
| 1.8-1.11 | Amelia / Winston reviews Sample, Tenants query/read-model, Tenants projection/consumer, and guardrails independently | Sample, DomainService, AppHost, and scoped Tenants suites |
| 2.4-2.7, 2.12 | Amelia / Sally reviews UI evidence; EventStore reviewers own the Story 2.7 prerequisite and the Tenants maintainer approves Story 2.12 identity adoption | RestApi.Generators plus scoped Tenants Contracts, Integration, UI, live source-topology, and package/source-mode builds |
| 3.7-3.9 | Amelia / Paige reviews workflow migration, safety validation, and backlog separately | Workflow scans, manifest governance tests, Release build, and documented supply-chain evidence |
| 5.6-5.9 | Winston / Amelia reviews AppHost, production YAML, drift tests, and docs separately | AppHost tests, topology scans, dedicated integration lane, and docs checks |
| 7.2-7.14 | Owner/reviewer named by each Admin, deployment, testing, workflow, or UI child | Focused Admin, AppHost, integration, workflow, and UI suites named by each child |
| 7.15-7.18 | John / specialist reviewer named by backlog domain | Independent artifact-structure validation for each backlog product |

### Spec-Gated Story Outputs

Epic 6 implementation stories are blocked until their paired specs exist and carry approval evidence.

| Spec story | Required output path | Dependent implementation story |
| --- | --- | --- |
| 6.1 | `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` | 6.2 |
| 6.3 | `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md` | 6.4 |
| 6.5 | `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` | 6.6 |

Approval evidence must include approver, date, accepted scope, rejected alternatives, open decisions, and explicit authorization for the dependent implementation story to start.

### Backlog Artifact Outputs

Stories 7.15-7.18 are independent planning/backlog artifact stories, not runtime implementation stories. Each completes only when its own exact artifact satisfies scope, non-goals, dependencies, risks, and validation expectations:

- `_bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md`
- `_bmad-output/planning-artifacts/backlog/iam-1-admin-oidc-login.md`
- `_bmad-output/planning-artifacts/backlog/kit-1-aggregate-test-kit.md`
- `_bmad-output/planning-artifacts/backlog/rest-generator-hardening.md`

### Parties Projection/Query Parity Gate

Story 1.13 completed its investigation and correctly produced a `still blocked` owner packet. Its `done` status means the investigation is complete; it does not mean the SDK prerequisite is available. Parties Story 8.6 remains blocked until all follow-up implementation and closure stories complete:

1. Stories 1.14-1.16 establish read-model lifecycle, coordinated writes, and complete lifecycle metadata.
2. Story 1.17 establishes asynchronous named multi-projection dispatch.
3. Stories 1.18 and 1.19 prove production-path delivery idempotency and replay-equivalent paged rebuilds.
4. Story 1.20 re-runs parity, records explicit EventStore owner approval, and binds exact source, package, or deployed-image identity.

The numbered capability sequence governs evidence acceptance and final parity closure; it is not a serial execution lock. Stories 1.14-1.19 may be implemented and reviewed in parallel once the contracts they directly consume exist. An unresolved review item in one story blocks Story 1.20 closure, but does not block another implementation story unless it exposes a direct contract contradiction; a direct contradiction must halt the affected story and be routed through change control.

Cursor scope compatibility may reuse Story 1.13 evidence. Every other blocked item must be reclassified `available` by Story 1.20. Source-mode consumers verify the EventStore submodule SHA; package-mode consumers verify exact package versions and hashes; deployed consumers verify the image digest maps to the approved EventStore SHA. The consuming repository SHA is never compared to the EventStore SHA.

Story 1.20 closes the projection/query SDK prerequisite for Parties Story 8.6 only. It does not deliver or approve the G5 payload-protection engine, `pdenc-v2`, key mechanics, production backend, or Parties Story 8.7 migration.

### Payload-Protection Security Gate

The optional shared payload-protection engine is committed post-MVP work under Epic 8 and is independent from Story 1.20.

- Story 8.2 cannot start until Story 8.1 produces `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md` with named architecture and security approval plus explicit implementation authorization.
- No EventStore hook, Story 22.7 artifact, custom-provider example, LocalDev/in-memory backend, or interface-only backend counts as the G5 engine or as production proof.
- Story 1.20 cannot classify G5 and does not block or authorize Story 8.2.
- Parties keeps G5 `needs-additive-api`, Story 8.7 in backlog, and its local provider/DI rollback path until Story 8.2 is implemented, reviewed, released or pinned, and proven through dual-provider compatibility and rollback after `pdenc-v2` writes.

## Requirements Inventory

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

FR21: Cross-repo Hexalith library dependencies use source project references only when `UseHexalithProjectReferences=true` is explicitly supplied and the root-declared source exists. An unset or explicit `false` value selects package references in every configuration, including Debug; Release and configuration-less evaluation therefore remain package-safe. Every source-owned NuGet dependency version used by a Hexalith repository must be declared in `references/Hexalith.Builds/Props/Directory.Packages.props`; consuming `Directory.Packages.props` files import that catalog and declare no local `PackageVersion`, version override, or fallback version property.

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

FR37: EventStore must provide an optional shared payload-protection engine package built on `IEventPayloadProtectionService` and the existing provider-neutral metadata, outcome, workflow, and redaction contracts. The engine must implement the approved `pdenc-v2` format and byte-stable authenticated-data contract, preserve `json+pdenc-v1`, `json-redacted`, legacy-unprotected, and snapshot read compatibility, expose `IPersonalDataPolicy` and `IErasureStateProvider` extension seams, supply reusable key-lifecycle and resilience mechanics behind shared contracts, include at least one integration-proven production backend, and produce EventStore-owner plus Parties dual-provider parity and rollback evidence before G5 is available.

### NonFunctional Requirements

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

NFR15: Admin UX must not present deferred backup, restore, import, compaction, or other unavailable operations as functional; unavailable operations must be hidden/disabled or return 501.

NFR16: Integration and higher-tier tests must assert persisted state-store/read-model/end-state evidence, not only HTTP status codes or mock call counts. Erasure, batch recovery, handler idempotency, and rebuild equivalence require persisted detail, index, marker, lifecycle, and checkpoint evidence through their production paths.

NFR17: Operational hardening must support secret stores, DAPR app health checks, readiness-tagged health checks, resiliency targets, immutable image tags, and documented crypto-shred boundaries.

NFR18: AOT/trimming is explicitly not a target while reflection conventions remain load-bearing, and that constraint must be documented.

NFR19: Payload protection must fail closed and preserve byte-stable, versioned cryptographic semantics. Deleted, missing, denied, unavailable, malformed, tampered, and opaque states must remain bounded typed outcomes. Key material must be zeroed when no longer needed; caches must be invalidated on lifecycle changes; development-only backends must not start as production proof; and rollout, historical reads, downgrade, and rollback after writing the newest format must be integration-tested.

### Additional Requirements

- Standalone PRD, architecture, and UX design contracts are present under `_bmad-output/planning-artifacts`; this epics document must stay aligned with those artifacts and the approved sprint change proposals.
- No greenfield starter template is mandated. A `dotnet new hexalith-domain` template is mentioned as an optional/deferred platform capability, not a required starting point.
- Use `Hexalith.EventStore.slnx` only for restore/build; do not introduce or use `.sln` files.
- Run unit tests per project; do not make solution-level `dotnet test` the default EventStore validation path.
- Keep `.csproj` package references versionless; all package versions must remain in `Directory.Packages.props`.
- Require explicit `UseHexalithProjectReferences=true` for source intent; unset or explicit `false` remains package intent in Debug, Release, and configuration-less evaluation. Rerun restore after changing dependency mode.
- Use .NET SDK container support, not Dockerfiles, and keep container repository settings centralized.
- Keep DAPR access-control YAML, sidecar app IDs, topics, and AppHost resource names aligned whenever topology changes.
- Use ULIDs for message, correlation, causation, and aggregate identifiers where EventStore envelope semantics require sortable unique ids; do not use `Guid.TryParse` for these identifiers.
- Apply `ConfigureAwait(false)` to awaited calls in production code and maintain warnings-as-errors cleanliness.
- Generated REST controllers must delegate to the gateway rather than bypassing gateway auth, validation, status, archive, and observability behavior.
- Domain services must not own reusable hosting, ServiceDefaults, Aspire wiring, query actors, cursor codecs, state-store wrappers, telemetry sources, or health-check classes when the platform supplies them.
- AppHost changes require an Aspire restart because the app model is built at startup.
- Release workflow changes must preserve semantic-release behavior and Conventional Commit driven versioning.
- Shared Hexalith.Builds workflow/action references are intentionally `@main`; third-party action pinning policy remains enforced by the shared workflows.
- Any global-ordering sharding implementation must update the frozen `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md` before code changes.
- Specs are required before implementing folded snapshots, projection delivery cost changes, and event schema versioning/upcasting.
- Full aggregate/event GDPR tombstoning, broker-history deletion, physical backup erasure, audit-record deletion, and provider/operator key-custody operations remain outside Phase 4 MVP and must not be hidden inside unrelated remediation stories. Generic projection read-model/checkpoint erasure is active Story 1.14 scope. The optional EventStore-owned shared payload-protection engine is separately committed under post-MVP Epic 8; Stories 22.7a-d supplied prerequisites, not that engine.
- AD-19 fixes the normalized server result as `ProjectionDispatchResult` Version 1 with bounded ordinal route entries, stable status codes, and explicit checkpoint-advance state; no equivalent result shape is accepted without a new architecture decision.
- AD-21 makes `src/Hexalith.EventStore.Admin.UI` the single consolidated EventStore UI under resource `eventstore-admin-ui`, FrontComposer module `event-store-admin`, matching Shell/Contracts.UI `3.2.2`, and Fluent UI V5. No additional UI host is created.
- AD-22 requires owner-approved exact EventStore artifact identity before consumer infrastructure removal; use source SHA, package versions/hashes, or deployed image digest as applicable, never the consumer repository SHA.
- AD-23 makes EventStore the owner of the optional shared payload-protection engine, stable formats, shared key mechanics, production-backend conformance, release provenance, and G5 proof while provider/operators retain production key custody and credentials and domains retain legal policy.

### UX Design Requirements

The UX design contract is `_bmad-output/planning-artifacts/ux.md`, with detailed supporting contracts in `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md` and `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`. UI-related requirements remain represented in the functional/non-functional requirements above, especially FR13, FR15, FR34, NFR14, and NFR15.

### FR Coverage Map

FR1: Epic 1 - Domain author self-service platform.

FR2: Epic 1 - Domain-service SDK host shape.

FR3: Epic 1 - Canonical domain-service DAPR endpoints.

FR4: Epic 1 - Domain query-handler seam and gateway routing.

FR5: Epic 1 - Generic persisted read-model store and write policy.

FR6: Epic 1 - Reusable protected query cursor codec.

FR7: Epic 1 - Generic projection-handler and domain-event consumer seams.

FR8: Epic 1 - Aspire, telemetry, and health-check platform extensions.

FR9: Epic 1 - Sample and Tenants adoption of platform SDK seams.

FR10: Epic 1 - DomainService and ServiceDefaults packaging.

FR11: Epic 2 - REST API source-generator contract seam.

FR12: Epic 2 - Generated typed REST controllers and generator tests.

FR13: Epic 2 - External API hosts for generated REST; UI uses client libraries.

FR14: Epic 2 - Sample contracts library and external Sample API proof.

FR15: Epic 2 - Tenants external API proof and UI client-library adoption.

FR16: Epic 2 - Metadata-rich, scope-aware projection-changed transport.

FR17: Epic 3 - Live-sidecar tests re-tiered off release gate.

FR18: Epic 3 - Overridable DaprETagService actor timeout.

FR19: Epic 3 - Submodules under references layout.

FR20: Epic 3 - Aspire Keycloak resource renamed to security.

FR21: Epic 3 - Shared Builds package catalog with explicit source opt-in and package-safe defaults.

FR22: Epic 3 - Release commands assert package mode and avoid submodule packaging.

FR23: Epic 4 - Non-zero global positions, MessageId CloudEvent ids, duplicate result fidelity.

FR24: Epic 4 - Global-position sharding spec renegotiation.

FR25: Epic 3 - Shared Hexalith.Builds gates and manifest-driven package scope.

FR26: Epic 5 - Phase 0 security and safe-remediation fixes.

FR27: Epic 4 - Resume/idempotency integrity and command status re-keying.

FR28: Epic 5 - Defense-in-depth trust boundary.

FR29: Epic 4 - Replay and dispatch determinism.

FR30: Epic 4 - Crash recovery for committed-but-unpublished events.

FR31: Epic 4 - Append durability verify-first spike.

FR32: Epic 5 - Runtime topology and deployment posture parity.

FR33: Epic 6 - Bounded cost and event evolution.

FR34: Epic 7 - Delivery, admin, deploy, and IntegrationTests recovery.

FR35: Epic 7 - Backlog capability tracking.

FR36: Epic 1 - Projection/query parity implementation and owner-approved runtime-pin closure.

FR37: Epic 8 - Shared payload-protection security specification, engine implementation, production backend, and Parties G5 parity.

## Epic List

### Epic 1: Domain Author Self-Service Platform

**Epic type:** Platform Capability

Domain authors can build and run EventStore-backed domain modules with minimal boilerplate and reusable platform seams, while the platform owns hosting, query, projection, read-model, cursor, telemetry, health, Aspire, and packaging concerns.

**FRs covered:** FR1, FR2, FR3, FR4, FR5, FR6, FR7, FR8, FR9, FR10, FR36

### Epic 2: External Integration Surfaces

**Epic type:** External Integration Capability

External application developers can consume typed, generated REST APIs, while interactive UI hosts use client libraries directly and real-time projection notifications remain scoped, metadata-only, and backward compatible.

**FRs covered:** FR11, FR12, FR13, FR14, FR15, FR16

### Epic 3: Release And Repository Reliability

**Epic type:** Release Reliability

Maintainers can release reproducibly with one authoritative, latest-compatible shared package catalog, correct package/reference mode, aligned submodule and Aspire resource layout, deterministic release gates, live-sidecar integration coverage, shared supply-chain workflows, and manifest-governed package output.

**FRs covered:** FR17, FR18, FR19, FR20, FR21, FR22, FR25

### Epic 4: Event Correctness And Recovery

**Epic type:** Correctness Remediation

Operators and consumers can trust persisted event metadata, idempotency, command status, replay dispatch, append behavior, global-position allocation, and crash recovery semantics under duplicate, concurrent, and failure conditions.

**Sequencing note:** Prioritize data-loss, idempotency, replay, and recovery slices before global-position sharding.

**FRs covered:** FR23, FR24, FR27, FR29, FR30, FR31

### Epic 5: Security And Tenant Isolation

**Epic type:** Security Remediation

Administrators, tenants, and domain services are protected by fail-closed authentication, scoped authorization, safe configuration, app-layer internal credentials, tenant-aware runtime topology, and removal of trusted wire assertions.

**Sequencing note:** Prioritize Phase 0 safe fixes and trust-boundary closure before topology hardening.

**FRs covered:** FR26, FR28, FR32

### Epic 6: Bounded Cost And Event Evolution

**Epic type:** Spec-Gated Cost And Evolution

Platform users can operate long-lived streams with bounded snapshot and projection cost, sequence-safe projection updates, explicit global-position scaling, event schema versioning/upcasting, validated event metadata, and cancellation-aware processing seams.

**Sequencing note:** Stories in this epic must begin with frozen specs before implementation.

**FRs covered:** FR33

### Epic 7: Operator Trust, Admin Honesty, And Future Capabilities

**Epic type:** Operations And Backlog Capability

Operators get honest admin UX, attributable admin actions, production deployment hardening, reliable higher-tier test evidence, and explicit backlog tracks for erasure, admin OIDC, aggregate test kits, and generator hardening.

**FRs covered:** FR34, FR35

### Epic 8: Shared Payload Protection

**Epic type:** Post-MVP Security Platform Capability

Platform security owners and domain modules can use an optional, reusable, production-proven payload-protection engine without duplicating cryptographic formats and key-lifecycle mechanics, while providers/operators retain key custody and domains retain legal policy.

**Sequencing note:** Story 8.1 is an approval gate for Story 8.2. Epic 8 does not block Phase 4 MVP, but Story 8.2 blocks Parties Story 8.7 migration.

**FRs covered:** FR37

## Epic 1: Domain Author Self-Service Platform

Domain authors can build and run EventStore-backed domain modules with minimal boilerplate and reusable platform seams, while the platform owns hosting, query, projection, read-model, cursor, telemetry, health, Aspire, and packaging concerns.

### Story 1.1: Canonical Domain-Service SDK Host

**Requirements covered:** FR1, FR2, FR3

As a domain author,
I want a canonical EventStore domain-service SDK host,
So that I can run a domain module with platform-provided hosting and DAPR endpoints instead of hand-written boilerplate.

**Acceptance Criteria:**

**Given** a domain module references the EventStore domain-service SDK
**When** its host calls `builder.AddEventStoreDomainService()` and `app.UseEventStoreDomainService()`
**Then** the host registers EventStore domain services, service defaults, and domain assembly discovery
**And** the domain host does not require hand-written request router, default endpoint, or operational metadata wiring.

**Given** a domain service uses the SDK host extensions
**When** the application maps domain-service endpoints
**Then** `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata` are available through SDK-owned mappings
**And** route mapping remains compatible with domains that already provide a bespoke `/project` route.

**Given** the Sample domain host is used as the in-repository proof
**When** its `Program.cs` is inspected
**Then** the domain hosting surface is reduced to the canonical SDK calls
**And** moved SDK infrastructure is deleted from the Sample project.

**Given** the SDK host is built and tested
**When** the relevant build and unit-test projects run
**Then** the Release build is clean under warnings-as-errors
**And** focused SDK and Sample tests verify endpoint mapping, assembly discovery, and compatibility behavior.

### Story 1.2: Domain Query Routing And Response Provenance

**Requirements covered:** FR4, FR36, NFR8, NFR16

As a domain author,
I want domain queries to be implemented as plain query handlers and routed by the platform,
So that my domain can expose query behavior without hosting a custom projection/query actor and consumers can distinguish genuine projection evidence from handler-computed results.

**Acceptance Criteria:**

**Given** a domain module registers one or more `IDomainQueryHandler` implementations
**When** `AddEventStoreDomainService()` runs
**Then** the handlers are discovered and registered by domain and query type
**And** duplicate or unsupported query routes fail predictably without manual switch-based dispatch.

**Given** the domain-service SDK exposes operational index metadata
**When** the gateway reads `/admin/operational-index-metadata`
**Then** handler-served query types are advertised per domain
**And** the gateway persists or caches that metadata for routing decisions.

**Given** the gateway receives a query for a handler-served domain/query type
**When** `HandlerAwareQueryRouter` resolves the route
**Then** it invokes the target domain service `/query` endpoint
**And** it falls back to the projection-actor router when no handler is declared.

**Given** a domain query handler or projection actor returns query metadata
**When** the result crosses `QueryResult`, `QueryRouterResult`, `SubmitQueryResult`, `SubmitQueryResponse`, and `EventStoreQueryResult`
**Then** `QueryResponseMetadata` is preserved additively through each platform type
**And** the gateway no longer drops domain-produced freshness, projection version, paging, warning, or degraded-state metadata.

**Given** the gateway creates HTTP response metadata
**When** domain metadata and gateway metadata both exist
**Then** metadata is merged by explicit rules: domain/projection evidence wins for freshness, projection version, paging, degraded state, and warnings; gateway ETag header value wins for the HTTP validator; gateway fills `ServedAt` only when absent; `IsNotModified` is set by the HTTP outcome.

**Given** freshness metadata is unavailable
**When** a query response is returned or `RequireFresh` / `MaxStaleness` is requested
**Then** freshness is represented as unknown, not current
**And** freshness-dependent requests fail closed according to the existing `query_projection_stale` taxonomy instead of silently treating unknown freshness as current.

**Given** a handler route, projection-backed route, or unresolved route produces a response
**When** `HandlerAwareQueryRouter` and the gateway stamp metadata
**Then** provenance is respectively `HandlerComputed`, `ProjectionBacked`, or `Unknown`
**And** that classification is preserved through `QueryResult.Metadata`, router/gateway responses, typed and untyped clients, and external adapters.

**Given** a route is `HandlerComputed` or `Unknown`
**When** the gateway merges response metadata
**Then** it attaches no projection-actor ETag, projection version, freshness, or lifecycle evidence
**And** consumers render `Unknown` rather than `Current` or `Stale`.

**Given** a route is `ProjectionBacked`
**When** the gateway exposes lifecycle evidence
**Then** projection version and lifecycle originate from persisted read-model freshness rather than an ETag alias
**And** real-gateway-path tests prove the handler route omits projection evidence while the persisted projection route preserves genuine evidence.

**Given** query routing is tested
**When** focused unit tests execute
**Then** domain-side dispatch, operational metadata capture, handler-aware routing, fallback behavior, route stamping, metadata propagation, gateway merge behavior, typed client metadata exposure, and backward-compatible projection-actor routing are verified.

### Story 1.3: Persisted Read-Model Store And Write Policy

**Requirements covered:** FR5, FR36, NFR7, NFR16
**Owner / review boundary:** Amelia (Developer); Murat (Test Architect) reviews only the production store and write-policy contract.
**Focused validation:** `dotnet test tests/Hexalith.EventStore.Client.Tests/ --configuration Release`; `dotnet build Hexalith.EventStore.slnx --configuration Release`.

As a domain projection author,
I want a platform-owned persisted read-model store and write policy,
So that ETag-aware reads, writes, and bounded merge retries behave consistently without domain-owned DAPR state wrappers.

**Acceptance Criteria:**

**Given** a projection or query handler uses `IReadModelStore` and `ReadModelWritePolicy`
**When** it reads, creates, replaces, applies, or merges an entry
**Then** ETags and first-write concurrency are enforced by the DAPR adapter
**And** singleton/index merges use a bounded retry budget with a deterministic conflict result.

**Given** the public registration seam is used
**When** services resolve the store and policy
**Then** the contracts remain additive, cancellation-aware, and independently configurable
**And** no Tenants source change is required by this platform story.

**Given** focused store tests run
**When** success, first write, conflict, retry exhaustion, cancellation, and DAPR failure are exercised
**Then** observable results and persisted state match the documented policy
**And** mock call counts alone do not close the story.

### Story 1.4: Deterministic Read-Model Testing Fake

**Requirements covered:** FR5, FR36, NFR16
**Owner / review boundary:** Amelia (Developer); Murat (Test Architect) reviews deterministic fake semantics only.
**Focused validation:** `dotnet test tests/Hexalith.EventStore.Testing.Tests/ --configuration Release`; focused in-memory store tests in `Hexalith.EventStore.Client.Tests`.

As a domain test author,
I want an in-memory read-model fake with production-equivalent observable semantics,
So that conflict, retry, partial-failure, and JSON round-trip behavior can be tested without live DAPR infrastructure.

**Acceptance Criteria:**

**Given** a test uses the platform fake
**When** entries are saved, read, replaced, or deleted
**Then** first-write and ETag behavior match the production store contract
**And** values cross a serialization boundary rather than sharing mutable object references.

**Given** a test needs deterministic failure
**When** it injects conflict or partial-failure behavior
**Then** the configured attempt fails at the named boundary and retry behavior is reproducible
**And** unrelated keys and attempts remain unaffected.

**Given** the fake and production contract tests run
**When** equivalent scenarios are compared
**Then** their public outcomes agree
**And** fake-only behavior is not labeled as live integration evidence.

### Story 1.5: Protected Query Cursor Codec

**Requirements covered:** FR6, NFR2, NFR16
**Owner / review boundary:** Amelia (Developer); Murat (Test Architect) reviews cursor protection and query-boundary evidence only.
**Focused validation:** cursor/registration tests in `Hexalith.EventStore.Client.Tests`, `Hexalith.EventStore.DomainService.Tests`, and focused query validation/problem-detail tests in `Hexalith.EventStore.Server.Tests`.

As a domain query author,
I want a reusable protected query cursor codec,
So that paged queries preserve opaque scope without exposing or trusting client-controlled cursor internals.

**Acceptance Criteria:**

**Given** a handler encodes a cursor with `IQueryCursorCodec` and `QueryCursorScope`
**When** the cursor is decoded with the same caller-supplied Data Protection purpose and scope
**Then** the bounded payload round-trips
**And** wrong tenant/domain/query scope, malformed input, tampering, oversize payload, or key rotation fails safely.

**Given** a producer supplies paging evidence
**When** it crosses query and typed-client results
**Then** effective page size, offset or next cursor, total count when known, and `HasMore` remain producer-authored
**And** gateway request paging is never promoted to authoritative evidence.

**Given** cursor input is rejected
**When** the query surface returns problem details and logs the failure
**Then** it uses the support-safe `query_invalid_page` taxonomy
**And** raw cursor, decoded position, scope, protected payload, and ETag internals are never parsed for support text or disclosed.

### Story 1.6: Projection And Domain Event Consumer Seams

**Requirements covered:** FR7

As a domain author,
I want platform seams for projection dispatch and domain-event consumption,
So that I can keep projection and subscription behavior domain-specific while reusing the platform plumbing.

**Acceptance Criteria:**

**Given** a domain implements `IDomainProjectionHandler`
**When** the SDK maps `/project`
**Then** the SDK dispatches projection requests to the matching handler
**And** it yields when the application has already mapped a bespoke `/project` route.

**Given** a domain consumes events from EventStore pub/sub
**When** it registers platform domain-event handlers and maps domain-event endpoints
**Then** the platform provides the event envelope, context, handler dispatch, marker-based deduplication, and endpoint mapping
**And** domain code only implements the handler logic and domain-specific options.

**Given** a domain requires payload integrity checks
**When** `PayloadAggregateIdPropertyName` or equivalent options are configured
**Then** the event consumer validates the payload aggregate identity before applying side effects
**And** invalid or duplicate events are handled consistently with at-least-once delivery expectations.

**Given** projection and subscription seams are tested
**When** focused unit tests execute
**Then** projection dispatch, custom-route yielding, event handler registration, deduplication, and endpoint mapping are verified
**And** the Client library remains ASP.NET-free while endpoint mapping stays in the DomainService SDK.

### Story 1.7: Domain Module Hosting Observability

**Requirements covered:** FR8

As a platform operator,
I want domain modules to use shared Aspire, telemetry, and health-check conventions,
So that local topology, diagnostics, and health behavior are consistent across every domain.

**Acceptance Criteria:**

**Given** an AppHost adds a domain module
**When** it calls the EventStore Aspire domain-module extension
**Then** the domain receives the expected DAPR sidecar, state-store, pub/sub, and app-id wiring
**And** the extension supports shared or intentionally isolated DAPR resources.

**Given** a domain module emits diagnostic telemetry
**When** it registers platform domain telemetry conventions
**Then** ActivitySource, Meter, and health-check names are derived consistently from the domain name
**And** per-domain telemetry code does not recreate platform-owned sources or meters.

**Given** a domain module depends on a DAPR state store
**When** the platform DAPR state-store health check is registered
**Then** health probes verify the configured state-store path
**And** the health-check name follows the shared domain convention.

**Given** the hosting and observability seams are adopted
**When** the AppHost and related tests are updated
**Then** duplicated sidecar, telemetry, and health-check wiring is removed from domain modules
**And** Release builds remain clean under warnings-as-errors.

### Story 1.8: Sample Domain-Centric Adoption

**Requirements covered:** FR9, NFR14
**Owner / review boundary:** Amelia (Developer); Winston (Architect) reviews the Sample domain/platform boundary only.
**Focused validation:** `Hexalith.EventStore.Sample.Tests`, `Hexalith.EventStore.DomainService.Tests`, and the Release solution build.

As a platform maintainer,
I want the Sample domain to be the minimal domain-centric reference,
So that domain authors can see platform SDK adoption without copied hosting or infrastructure.

**Acceptance Criteria:**

**Given** the Sample domain adopts the canonical host, projection, query, and event-consumer seams
**When** its source and project graph are inspected
**Then** it contains domain behavior and contracts only
**And** moved request routing, operational metadata, ServiceDefaults, Aspire, state-store, cursor, telemetry, and health plumbing is absent.

**Given** the Sample UI issues commands and queries
**When** host boundaries are scanned
**Then** it consumes typed EventStore client seams
**And** it hosts no generated or hand-written per-message MVC command/query controllers.

**Given** focused Sample and SDK tests run
**When** aggregate dispatch, query, projection, health, and guardrail behavior is exercised
**Then** domain behavior is preserved
**And** the Release build remains clean under warnings-as-errors.

### Story 1.9: Tenants Query And Read-Model Adoption

**Requirements covered:** FR4, FR5, FR6, FR9, FR36, NFR2, NFR8, NFR16
**Owner / review boundary:** Amelia (Developer); Winston (Architect) and the Tenants maintainer approve the query/read-model boundary.
**Focused validation:** scoped Tenants query, read-model, cursor, RBAC, audit, and Client tests in source mode plus exact package-mode evidence.

As a Tenants maintainer,
I want Tenants queries and read models to consume EventStore platform seams,
So that tenant RBAC, audit, pagination, and lifecycle behavior remain intact without local platform clones.

**Acceptance Criteria:**

**Given** Tenants adopts `IDomainQueryHandler`, `IReadModelStore`, `ReadModelWritePolicy`, and `IQueryCursorCodec`
**When** local query/read-model infrastructure is removed
**Then** RBAC, audit, cursor scope, paging, ETag, and persisted lifecycle behavior remain equivalent
**And** route provenance follows Story 1.2 rather than an ETag-derived alias.

**Given** the change crosses the Tenants repository boundary
**When** completion is requested
**Then** the evidence names the Tenants maintainer-approved PR/commit, exact Tenants SHA, accepted scope, source/package mode, and validation results
**And** without approval the child remains `review` or `backlog` and no submodule edit is silently authorized.

**Given** focused production-path tests run
**When** tenant-scoped reads, conflicts, invalid cursors, lifecycle evidence, and cross-tenant denial are exercised
**Then** persisted end state and support-safe errors are asserted
**And** mock-only proof cannot close the child.

### Story 1.10: Tenants Projection And Event-Consumer Adoption

**Requirements covered:** FR7, FR9, FR36, NFR2, NFR6, NFR16
**Owner / review boundary:** Amelia (Developer); Winston (Architect) and the Tenants maintainer approve projection/consumer replacement.
**Focused validation:** scoped Tenants projection, event-publication, duplicate/out-of-order, health, and persisted-state tests in source and package modes.

As a Tenants maintainer,
I want projections and domain-event consumers to use EventStore platform dispatch and delivery seams,
So that local actor/plumbing removal preserves tenant isolation and delivery correctness.

**Acceptance Criteria:**

**Given** Tenants adopts named projection and event-consumer seams
**When** local projection actors, marker plumbing, telemetry, and health duplication are removed
**Then** domain-specific projection logic remains
**And** duplicate, out-of-order, checkpoint, audit, and tenant behavior is preserved through production paths.

**Given** the replacement is approved
**When** completion evidence is recorded
**Then** it cites the Tenants maintainer-approved PR/commit, exact SHA, source/package mode, persisted-state tests, and rollback boundary
**And** absent approval leaves local infrastructure intact and the child non-`done`.

**Given** one projection or consumer fails
**When** retry and recovery run
**Then** successful durable state is not misreported or lost
**And** checkpoints advance only after required durable work.

### Story 1.11: Domain-Module Adoption Guardrails

**Requirements covered:** FR1, FR9, FR10, NFR14
**Owner / review boundary:** Amelia (Developer); Winston (Architect) reviews repository-boundary and anti-boilerplate policy only.
**Focused validation:** `DomainModuleAuthoringGuardrailTests`, Sample tests, AppHost configuration tests, and read-only scans of initialized domain-module roots.

As a platform maintainer,
I want domain-module architecture guardrails,
So that future Sample, Tenants, and third-party modules cannot silently reintroduce reusable platform boilerplate.

**Acceptance Criteria:**

**Given** initialized domain-module roots are scanned
**When** a module owns reusable hosting, Aspire, ServiceDefaults, projection/query actors, cursor codecs, state-store wrappers, telemetry, health checks, or per-message UI controllers prohibited by AD-2/AD-4
**Then** the guardrail fails with the platform seam that must replace it
**And** domain-specific contracts, handlers, projections, validators, and explicitly approved exceptions remain allowed.

**Given** a scan reaches a root-declared submodule
**When** it evaluates source
**Then** it remains read-only unless maintainer approval exists
**And** it never initializes nested submodules.

**Given** focused governance tests run
**When** approved and prohibited fixtures are evaluated
**Then** matching is deterministic and support-safe
**And** package/source-mode project boundaries remain valid.

### Story 1.12: DomainService Packaging And Guardrails

**Requirements covered:** FR10

As a release maintainer,
I want the domain-service SDK, service defaults, documentation, and guardrails to be packaged and governed,
So that the domain-centric model is reusable and hard to regress.

**Acceptance Criteria:**

**Given** EventStore packages are released
**When** the release package manifest and pack scripts run
**Then** `Hexalith.EventStore.DomainService` and `Hexalith.EventStore.ServiceDefaults` are included as intended
**And** packages outside the EventStore release manifest are not produced or published.

**Given** future agents or developers author a domain module
**When** they read repository instructions and generated project context
**Then** the domain-centric host shape, query/projection/read-model/cursor seams, Aspire extension, and anti-boilerplate rules are documented
**And** instructions clearly state that generated REST APIs belong to external API hosts, not interactive UI hosts.

**Given** a domain module attempts to reintroduce duplicated infrastructure
**When** guardrail tests or governance checks run
**Then** they flag own `*.Aspire`, `*.ServiceDefaults`, reusable projection/query actor, cursor codec, state-store wrapper, telemetry, or health-check anti-patterns where prohibited
**And** the failure explains the platform seam that should be used instead.

**Given** package metadata and governance are validated
**When** Release build, pack, and focused package-governance tests run
**Then** package dependencies are reproducible
**And** the solution remains clean under warnings-as-errors.

### Story 1.13: Projection/Query SDK Owner Parity Proof

**Requirements covered:** FR4, FR5, FR6, FR7, FR9, NFR8, NFR16
**Classification:** Completed investigation/proof story for cross-repo domain migration. Its `done` status means the investigation and blocked packet were completed; it does not mean SDK parity is available. Implementation closure is owned by Stories 1.14-1.20.

As an EventStore platform owner,
I want reviewed proof that the projection/query SDK can replace a non-trivial domain's local projection/query mechanics,
So that consuming modules can delete local rollback code only after EventStore owner evidence proves parity.

**Acceptance Criteria:**

**Given** a consuming domain requires projection/query SDK replacement proof
**When** the owner proof story starts
**Then** it records the current EventStore commit SHA and inspects `IDomainProjectionHandler`, `IDomainQueryHandler`, `IReadModelStore`, `ReadModelWritePolicy`, `IQueryCursorCodec`, `QueryCursorScope`, and domain-service registration APIs.

**Given** the proof is built for Parties Story 8.6 AC1
**When** each required item is evaluated
**Then** G3 read-model erasure hooks, G10 index batching or approved equivalent, G6 freshness mapping, duplicate/out-of-order replay behavior, full rebuild verification, cursor scope compatibility, and the intended EventStore pin are each classified as `already available`, `additive API/test added`, or `blocked`.

**Given** any required proof item is not satisfied
**When** the proof packet is produced
**Then** the final decision is `still blocked`, the missing API or behavior is named precisely, and no consuming story is authorized to mark the projection/query SDK row `available`.

**Given** additive code is needed
**When** implementation changes are made
**Then** they remain generic EventStore SDK capabilities and do not add Parties-specific domain logic to EventStore.

**Given** every required item is satisfied
**When** validation completes
**Then** the proof packet records source paths, test paths, validation commands/results, owner approval source, rollback note, known limitations, and final decision `available`.

**Given** the owner proof is available
**When** a consuming repo records it in its prerequisite matrix
**Then** the consuming repo must still verify its checked-out EventStore pin matches the approved SHA before source migration or local rollback deletion starts.

### Story 1.14: Read-Model And Projection Checkpoint Erasure

**Requirements covered:** FR5, FR36, NFR2, NFR16

As a domain projection maintainer,
I want generic read-model and checkpoint erasure,
So that removing or recreating an aggregate cannot leave stale projection state that discards valid future events.

**Acceptance Criteria:**

**Given** a projection store requires lifecycle cleanup
**When** its public contract is used
**Then** `IReadModelStore` exposes an asynchronous, cancellation-aware delete/erase operation with explicit absent-key and ETag-conflict behavior
**And** deleting an already-absent key is idempotent.

**Given** a tenant/domain/aggregate/projection scope is erased
**When** the platform-owned lifecycle operation completes
**Then** the selected read-model keys and companion delivery/rebuild sequence or checkpoint keys are absent
**And** recreating the same aggregate identifier accepts sequence-one delivery without a stale high-water mark.

**Given** the keys share a transaction-capable state store
**When** erasure runs
**Then** the operation uses an atomic transaction
**And** a backend without that capability uses a documented resumable protocol whose partial completion never reports success and whose retry converges safely.

**Given** DAPR and in-memory implementations are exercised
**When** success, absent state, ETag conflict, cancellation, injected partial failure, and retry are tested
**Then** both implementations expose equivalent observable behavior
**And** the fake supports deterministic conflict and partial-failure injection.

**Given** tenant isolation is enforced
**When** an erasure request targets another tenant's scope
**Then** it cannot delete or reveal that state
**And** persisted-state tests verify the denial and both tenants' end state.

**Given** Story 1.14 completes
**When** its scope is reviewed
**Then** it has not erased event streams, snapshots, broker history, backups, audit evidence, or cryptographic keys
**And** full GDPR aggregate/event tombstoning remains owned by GDPR-1.

### Story 1.15: Coordinated Read-Model Batch Writes

**Requirements covered:** FR5, FR36, NFR7, NFR16

As a projection author,
I want coordinated detail and index writes,
So that a projection cannot expose an updated detail model with a missing or inconsistent index entry.

**Acceptance Criteria:**

**Given** one projection delivery produces multiple typed writes or deletes within one configured state-store component
**When** the generic asynchronous batch contract executes
**Then** each operation carries its key, value or deletion intent, and concurrency policy
**And** existing single-key APIs remain compatible.

**Given** the configured DAPR store supports transactions
**When** a detail/index batch is flushed
**Then** it uses one state transaction
**And** a non-transactional backend uses an explicitly documented resumable equivalent; cross-store work is never described as atomic.

**Given** failure occurs before durable completion
**When** the result is returned
**Then** it reports a structured incomplete/conflict outcome rather than success
**And** recovery completes or safely compensates incomplete work without losing already durable state.

**Given** a stable projection batch identity has completed
**When** the same logical batch is retried
**Then** the retry is an idempotent success and the batch is not applied twice
**And** conflicting reuse of the identity fails predictably.

**Given** the batch reports success
**When** persisted state is inspected
**Then** every operation has been durably accepted by the configured store
**And** cancellation or deferred flush never implies rollback/completion unless the backend guarantees it explicitly.

**Given** deterministic and live-DAPR tests run
**When** they exercise success, ETag conflict, duplicate batch, cancellation, and injected failure between detail and index operations
**Then** detail, index, batch/recovery marker, and checkpoint end state is asserted directly
**And** mock calls alone are not accepted as G10 proof.

### Story 1.16: Complete Projection Freshness Lifecycle

**Requirements covered:** FR4, FR36, NFR8, NFR15

As a projection/query consumer,
I want a complete, provenance-safe projection lifecycle contract,
So that operational states are not collapsed into a stale Boolean or inferred from an ETag.

**Acceptance Criteria:**

**Given** the public lifecycle contract is defined
**When** it is serialized or deserialized
**Then** it supports exact stable values `Unknown = 0`, `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`
**And** missing, numeric, case-variant, or unrecognized input fails safely to `Unknown`.

**Given** authoritative platform evidence exists
**When** lifecycle is classified
**Then** persisted projection evidence produces `Current` or `Stale`, active rebuild lifecycle produces `Rebuilding`, serviceable output with a known degraded dependency produces `Degraded`, failed/unreachable authoritative storage produces `Unavailable`, and explicit non-authoritative consumer fallback produces `LocalOnly`
**And** lifecycle is never inferred from ETag presence, HTTP success, payload fields, or SignalR.

**Given** query provenance is available from Story 1.2
**When** lifecycle metadata crosses the query path
**Then** only `ProjectionBacked` responses may carry an authoritative lifecycle state
**And** handler-computed, missing, or invalid provenance yields `Unknown` without reopening Story 1.2 route-selection scope.

**Given** legacy metadata remains supported
**When** lifecycle is projected into compatibility fields
**Then** `Current` may produce `IsStale=false`, `Stale` may produce `IsStale=true`, and other lifecycle states do not fabricate stale/current evidence
**And** `IsDegraded` remains an additive compatibility view rather than the source of the lifecycle state.

**Given** a lifecycle-aware response is produced
**When** it crosses query results, gateway responses, typed/untyped clients, generated REST output, and UI-facing metadata
**Then** the state and canonical bounded header are preserved only when authoritative
**And** handler-computed routes omit authoritative lifecycle headers.

**Given** consumer and UX mapping is validated
**When** all six Parties states are exercised
**Then** mutation availability follows the approved UX table, `LocalOnly` never counts as projection-confirmed success, and tests cover serialization, omission, provenance gating, and persisted-path propagation.

### Story 1.17: Asynchronous Multi-Projection Dispatch

**Requirements covered:** FR7, FR36, NFR7, NFR12

**Sequencing correction:** Story 1.17 builds on the implemented platform seams from Stories 1.6, 1.14, 1.15, and 1.16, but unresolved review findings in another story are not a serial completion lock. Story 1.17 may implement and complete independently unless it exposes a direct contract contradiction. Story 1.20 remains blocked until Stories 1.14-1.19 are complete and reviewed.

As a domain projection author,
I want asynchronous named projection handlers with one-to-many dispatch,
So that one domain can durably maintain detail and index projections through platform seams.

**Acceptance Criteria:**

**Given** a named projection handler performs persistence
**When** it handles a request
**Then** the contract is asynchronous and cancellation-aware, can await `IReadModelStore`, `ReadModelWritePolicy`, and Story 1.15 batches, and completes only after required persistence finishes
**And** production awaits use `ConfigureAwait(false)`.

**Given** handlers are registered
**When** routes are validated
**Then** handlers are uniquely identified by `(Domain, ProjectionType)`, multiple projection types may share one domain, and duplicate pairs fail deterministically with a support-safe diagnostic.

**Given** one delivery applies to multiple projections
**When** dispatch runs
**Then** every applicable named handler is invoked, detail and index outcomes remain distinguishable, and observable invocation order is deterministic.

**Given** one projection handler fails after another completes durably
**When** the server normalizes the frozen `/project/v2` wire response
**Then** it emits exactly one bounded `ProjectionDispatchResult` Version 1 with one ordinal entry per admitted `(Domain, ProjectionType)` route
**And** every entry contains the stable `ProjectionDispatchStatus` code and explicit `ProjectionCheckpointAdvanceState`.

**Given** a route returns or encounters any completion, retry, validation, transport, persistence, checkpoint, malformed-outcome, or cancellation condition
**When** normalization follows AD-19
**Then** only `Completed` or `AlreadyCompleted` after required durable persistence, any legacy actor write, and checkpoint save records `Advanced`
**And** retryable work records `Retryable`/`NotAdvanced`, deterministic validation records `Failed`/`NotAdvanced`, uncertainty records `Indeterminate`/`NotAdvanced`, and cancellation fabricates no result and advances nothing.

**Given** existing synchronous single-projection consumers remain
**When** the new contract ships
**Then** they continue through a compatibility adapter or an explicitly approved breaking-version plan
**And** no consumer silently receives a changed JSON shape or ambiguous domain-only route.

**Given** a test domain registers detail and index handlers
**When** the production dispatch path executes
**Then** both async persistence operations are awaited and both persisted outputs are independently verified
**And** a one-handler failure proves truthful result and checkpoint behavior without Parties-specific EventStore logic.

### Story 1.18: Projection-Handler Delivery Idempotency

**Requirements covered:** FR7, FR36, NFR6, NFR7, NFR16

As an operator,
I want projection delivery to be duplicate-safe and order-safe through the real handler path,
So that at-least-once unordered delivery cannot corrupt detail or index state.

**Acceptance Criteria:**

**Given** delivery identity is persisted
**When** projection state is addressed
**Then** idempotency/checkpoint state is scoped by tenant, domain, aggregate, and projection type, duplicate detection uses EventStore `MessageId`, and `SequenceNumber` is never treated as globally ordered.

**Given** the same completed `MessageId` is delivered again
**When** the handler path receives it
**Then** the delivery is an idempotent no-op, an in-progress duplicate is deferred/retryable, and the logical detail/index batch is not applied twice.

**Given** an out-of-order event arrives
**When** its sequence is already applied, contains a future gap, or conflicts with a different message identity/content
**Then** a lower applied sequence is ignored safely, a gap returns retryable without advancing the checkpoint, and a conflict fails safely without persisted-state change.

**Given** projection writes and completion markers are persisted
**When** the coordinated operation completes or retries after partial failure
**Then** checkpoints advance only after required durable writes and retry converges to the same final state as one successful delivery.

**Given** deduplication history is bounded
**When** an event falls outside retained history
**Then** the platform invokes an explicit rebuild/reconciliation path rather than silently applying it
**And** the retention/compaction policy is documented and tested.

**Given** production-path tests run
**When** they exercise one in-order delivery, exact duplicate, reversed order, gap then missing event, duplicate during partial failure, and conflicting sequence/message identity through orchestration, `/project`, async dispatch, handler, and durable store
**Then** final detail, index, batch marker, and checkpoint state equals the single in-order baseline.

**Given** Stories 6.3/6.4 later optimize delivery cost
**When** their implementation is reviewed
**Then** they preserve this minimum correctness baseline.

### Story 1.19: Correct Paged Rebuild And Replay Equivalence

**Requirements covered:** FR7, FR33, FR36, NFR8, NFR16

As an operator,
I want paged projection rebuilds to be replay-equivalent,
So that rebuilding a long stream cannot replace correct state with a partial-page model.

**Acceptance Criteria:**

**Given** a projection handler participates in rebuild
**When** its contract is inspected
**Then** it declares or is adapted to full-replay or incremental semantics; full-replay handlers receive the complete required prefix, incremental handlers receive prior staged state plus a contiguous page, and a page is never presented as a complete stream.

**Given** paged rebuild begins
**When** work is incomplete
**Then** operation-scoped staging or equivalent non-live isolation holds its detail/index state
**And** the last complete live model is not overwritten until every required projection completes durably.

**Given** a stream exceeds the configured page size
**When** rebuild reads every page
**Then** page boundaries neither duplicate, skip, nor reorder events
**And** a bounded `toPosition` produces the same result as canonical replay through that position.

**Given** rebuild is canceled or a handler/store fails
**When** the operation stops and resumes
**Then** the last complete live model remains intact, progress resumes from a safe boundary, and page-read progress is not misreported as projection completion.

**Given** detail and index projections rebuild together
**When** the operation outcome is written
**Then** each projection remains independently observable but the operation cannot report success while any required projection is incomplete
**And** promotion and checkpoints follow Stories 1.15-1.18.

**Given** replay-equivalence evidence is produced
**When** a fixture larger than two pages runs through canonical replay/full-sequence projection and the production paged rebuild orchestrator
**Then** persisted detail, index, projection versions, and checkpoints are semantically equal
**And** tests also cover empty streams, exact page boundaries, bounded positions, cancellation, failure, and resume.

**Given** correctness precedes cost optimization
**When** a temporary full-sequence strategy is required
**Then** it has an explicit safety bound and failure mode
**And** Stories 6.3/6.4 may optimize it later without changing equivalence guarantees.

### Story 1.20: Owner-Approved Parity Closure And Runtime Pin

**Requirements covered:** FR36, NFR12, NFR16

As an EventStore platform owner,
I want a reviewed parity-closure packet tied to an exact runtime commit,
So that Parties Story 8.6 resumes only against capabilities that are implemented, verified, and approved.

**Acceptance Criteria:**

**Given** closure starts
**When** prerequisites are checked
**Then** Stories 1.14-1.19 are complete and reviewed, Story 1.2 is complete before Story 1.16 evidence is accepted, and public API/compatibility decisions are recorded.

**Given** parity is re-evaluated
**When** the packet is produced
**Then** read-model/checkpoint erasure, coordinated batching, six-state lifecycle, duplicate/out-of-order handler safety, rebuild equivalence, cursor compatibility, async persistence, and multiple projections per domain are each classified
**And** every item must be `available`; any unresolved item keeps the final decision `still blocked`.

**Given** evidence is recorded
**When** each requirement is reviewed
**Then** the packet cites source paths, test paths, exact commands/results, persisted-state evidence, known limitations, and rollback guidance
**And** mock-only or isolated aggregate-replayer proof cannot close handler-path requirements.

**Given** the proof result is ready
**When** owner review occurs
**Then** the packet records EventStore reviewer, approval date/source or PR, accepted scope, residual limitations, and explicit consumer-migration authorization
**And** story-creation authorization does not count as proof-result approval.

**Given** the runtime implementation is approved
**When** repository identity is recorded
**Then** one exact EventStore source commit containing the approved implementation/evidence is named and its working tree/test results correspond to that commit
**And** release provenance maps that SHA to exact consumed package versions and hashes plus the deployed EventStore image digest where applicable.

**Given** the packet is handed to Parties
**When** Parties evaluates its prerequisite
**Then** source mode verifies `references/Hexalith.EventStore` resolves to the approved EventStore SHA, package mode verifies exact package identities, and deployed mode verifies the image digest maps to that SHA
**And** a mismatch leaves Story 8.6 blocked, the Parties repository SHA is never compared to the EventStore SHA, and EventStore approval does not itself modify Parties or delete rollback code.

**Given** Story 1.20 completion is requested
**When** the packet still says `still blocked`
**Then** the story remains `in-progress` with the blocking condition recorded and a scoped corrective item is created
**And** Story 1.20 and Epic 1 become `done` only after the final decision is `available`.

**Explicit exclusion:** Story 1.20 closes the EventStore projection/query SDK prerequisite for Parties Story 8.6 only. It does not deliver or approve the G5 payload-protection engine, `pdenc-v2`, reusable key mechanics, a production backend, or Parties Story 8.7 migration. Only Story 8.2's approved `available` proof packet may close G5.

## Epic 2: External Integration Surfaces

External application developers can consume typed, generated REST APIs, while interactive UI hosts use client libraries directly and real-time projection notifications remain scoped, metadata-only, and backward compatible.

### Story 2.1: REST Contract Seam For Command And Query Messages

**Requirements covered:** FR11

As a domain contract author,
I want command and query messages to declare their generated REST surface explicitly,
So that external API hosts can generate typed endpoints without convention-only discovery or copied contract types.

**Acceptance Criteria:**

**Given** a command is intended for generated REST exposure
**When** the command implements `ICommandContract`
**Then** it exposes static `Domain` and `CommandType` values and an instance `AggregateId`
**And** those values are used by generated controllers to build gateway command requests.

**Given** a command or query needs a custom HTTP route
**When** it is annotated with `RestRouteAttribute`
**Then** the generator honors the configured verb and route template
**And** route templates can include domain-specific path parameters without changing the generic gateway contract.

**Given** an external API host opts in to generated REST controllers
**When** it applies assembly-level `RestApiAttribute`
**Then** route prefix, tag, and tenant-source behavior are available to the generator
**And** the contract assembly remains reusable by the domain service, external API host, and interactive UI metadata consumers.

**Given** the contract seam is tested
**When** Contracts tests run
**Then** command marker behavior, route metadata, tenant-source options, and invalid metadata cases are covered
**And** existing `IQueryContract` behavior remains backward compatible.

### Story 2.2: REST API Generator Discovery And Controller Emission

**Requirements covered:** FR12

As an external API host developer,
I want a Roslyn generator to emit typed REST controllers from domain contracts,
So that external applications get OpenAPI-visible endpoints without hand-written per-message controllers.

**Acceptance Criteria:**

**Given** an external API host references the REST API generator as an analyzer
**When** the host compilation includes opted-in command and query contracts
**Then** generated controllers are emitted into the host compilation
**And** non-marker types are ignored.

**Given** generated query actions execute
**When** a request reaches the generated controller
**Then** the controller delegates to `IEventStoreGatewayClient.SubmitQueryAsync`
**And** it maps success, `304`, ETag, freshness, projection version, served-at, degraded/warning state, paging metadata, not-found, forbidden, and validation outcomes consistently with gateway query semantics.

**Given** a generated external API action receives `EventStoreQueryResult.Metadata`
**When** it returns `200` or `304`
**Then** it forwards canonical support-safe headers for ETag, projection version, served-at, stale state, degraded state, warning codes, and bounded paging evidence only when values are present and bounded
**And** no generated controller relies on payload-specific fields to decide projection-confirmed state.

**Given** generated command actions execute
**When** a request reaches the generated controller
**Then** the controller generates ULID message/correlation identifiers where required and delegates to `IEventStoreGatewayClient.SubmitCommandAsync`
**And** route/body aggregate mismatches return a safe `400` response without rewriting command payloads.

**Given** generator misuse or unsupported declarations exist
**When** generator tests run through `CSharpGeneratorDriver`
**Then** diagnostics cover duplicate routes, unsupported route metadata, missing command contract members, and invalid tenant-source usage
**And** generated code follows file-scoped namespaces, nullable, `ConfigureAwait(false)`, and warnings-as-errors rules.

**Given** generator tests validate query metadata behavior
**When** generated query actions are exercised
**Then** tests cover real gateway-client metadata, `304`, header omission for absent metadata, safe problem details, and no exposure of cursor or ETag internals as support text.

### Story 2.3: Sample External API Host Proof

**Requirements covered:** FR13, FR14

As an external application developer,
I want the Sample domain to expose generated REST endpoints through a dedicated API host,
So that I can see the intended integration pattern without coupling it to the interactive Sample UI.

**Acceptance Criteria:**

**Given** Sample query and command contracts are shared between hosts
**When** they move into a contracts-only Sample contracts library
**Then** the domain service, Sample API host, and Sample Blazor UI reference the same compiled contract identities
**And** no contract file is compile-linked into the UI as a second type.

**Given** the Sample API host is configured for generated REST
**When** it references the contracts library and generator analyzer
**Then** generated query and command controllers are available in the external API host
**And** the host configures controllers, inbound auth, service defaults, and gateway-client access without Razor or interactive UI concerns.

**Given** the Sample Blazor UI needs command and query behavior
**When** it is updated for the corrected architecture
**Then** it consumes EventStore client libraries directly
**And** it hosts no generated or hand-written per-message MVC command/query controllers.

**Given** the Sample external API proof is validated
**When** Release build and focused Sample tests run
**Then** generated query and command endpoints compile and behave as expected
**And** any smoke tests verify ETag/`304` query behavior, metadata header behavior when available, and accepted command behavior through the external API host.

### Story 2.4: Tenants REST Contract Metadata And Routes

**Requirements covered:** FR11, FR15, NFR13
**Owner / review boundary:** Amelia (Developer); the Tenants maintainer approves contract identity and route metadata.
**Focused validation:** scoped Tenants Contracts tests and EventStore REST generator diagnostics/output tests.

As a Tenants contract maintainer,
I want command and query contracts to declare the external REST surface,
So that generated tenant APIs remain stable without duplicating controller logic.

**Acceptance Criteria:**

**Given** a Tenants command or query is externally exposed
**When** contract metadata is inspected
**Then** route, verb, tenant source, aggregate/entity binding, and API scope are explicit
**And** tenant detail, tenant users, user tenants, global administrators, and audit routes remain unambiguous.

**Given** invalid or duplicate route metadata exists
**When** generator diagnostics run
**Then** compilation fails with deterministic support-safe diagnostics
**And** no runtime fallback invents a route.

**Given** completion is requested
**When** cross-repository evidence is reviewed
**Then** it names the Tenants maintainer-approved PR/commit, exact SHA, accepted contract scope, and focused test results
**And** absent approval keeps this child non-`done`.

### Story 2.5: Dedicated External Tenants API Host

**Requirements covered:** FR13, FR15, NFR2, NFR14
**Owner / review boundary:** Amelia (Developer); the Tenants maintainer reviews the API-host boundary.
**Focused validation:** Tenants generated-controller integration tests plus EventStore AppHost topology/ACL tests.

As an external tenant-management integrator,
I want generated Tenants controllers in one dedicated external API host,
So that gateway policy remains the front door and domain/UI hosts expose no per-message API surface.

**Acceptance Criteria:**

**Given** `Hexalith.Tenants.Api` references Tenants contracts and the EventStore generator
**When** it builds and starts
**Then** generated command/query controllers delegate only to `IEventStoreGatewayClient`
**And** the host contains inbound auth, controller mapping, service defaults, and gateway-client wiring without domain implementation or UI dependencies.

**Given** AppHost and DAPR ACLs include `tenants-api`
**When** topology is inspected
**Then** the host may invoke only the EventStore gateway command/query operations it needs
**And** it receives no state-store, pub/sub, or direct Tenants domain-service persistence access.

**Given** unauthorized, invalid, mismatched, not-modified, or gateway-failure cases run through compiled generated routes
**When** results are asserted
**Then** responses are support-safe and no unintended gateway call occurs
**And** the evidence records maintainer approval, exact Tenants SHA, and focused test results before `done`.

### Story 2.6: Tenants UI Client-Library Alignment And UX Evidence

**Requirements covered:** FR13, FR15, FR34, NFR14, NFR15
**Owner / review boundary:** Amelia (Developer); Sally (UX Designer) and the Tenants maintainer review UI-host and evidence-state behavior.
**Focused validation:** Tenants UI tests, canonical UX conformance checks, and structural controller/analyzer scans.

As a Tenants operator,
I want the interactive UI to consume typed client libraries and display honest evidence states,
So that it remains an interactive host rather than a second external API surface.

**Acceptance Criteria:**

**Given** Tenants UI issues commands and queries
**When** its project graph and endpoints are inspected
**Then** it uses Tenants/EventStore client libraries
**And** it has no REST generator analyzer, assembly opt-in, generated controller mapping, or hand-written per-message MVC controller.

**Given** query provenance is `ProjectionBacked`, `HandlerComputed`, or `Unknown`
**When** lifecycle is rendered
**Then** only projection-backed evidence may show `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, or `LocalOnly`
**And** handler-computed, missing, or invalid provenance renders `Unknown` without claiming projection-confirmed success.

**Given** UI acceptance is recorded
**When** Sally and the Tenants maintainer review the focused evidence
**Then** support-safe denied/loading/stale/rebuilding/degraded/unavailable/local-only states follow canonical UX
**And** the exact approved Tenants SHA is recorded before `done`.

### Story 2.7: Pre-Authorization Registration And Provenance Correction

**Requirements covered:** FR4, FR15, NFR12, NFR16
**Owner / review boundary:** Amelia (Developer); an EventStore reviewer verifies only the registration/proof-harness and fail-closed provenance boundary. No Tenants dependency-identity approval is required because this story changes none.
**Focused validation:** the live source-topology query-provenance E2E, operational-metadata registration/index tests, and a repository-boundary check proving no Tenants, EventStore, or Builds dependency identity changed.

As an EventStore platform maintainer,
I want configured domain bindings and the live proof harness to reflect the domains actually hosted,
So that Story 1.20 can select a runtime from valid handler-routing and provenance evidence without consumer migration.

**Acceptance Criteria:**

**Given** the current EventStore source topology
**When** the live query-provenance E2E starts
**Then** the AppHost is compiled with `UseHexalithProjectReferences=true`, the root-declared Tenants resource exists and becomes healthy
**And** no nested submodule initialization is required.

**Given** the configured sample and Tenants bindings
**When** operational metadata is loaded
**Then** every configured binding maps to a domain actually hosted by its selected service, genuine metadata failures remain fail-closed, and `admin:query-types:tenants` contains `list-tenants`
**And** the exact E2E returns `200` with `HandlerComputed` provenance and no projection-validator leakage.

**Given** Story 1.20 is blocked, non-authorizing, incomplete, or lacks any required source, package, or approval identity
**When** the preceding criteria and the scoped fail-closed boundary are satisfied
**Then** Story 2.7 may enter `review` without changing any Tenants, EventStore, or Builds dependency identity, and existing rollback paths remain intact
**And** Story 1.20 authorization is not a prerequisite for review of this pre-authorization correction.

**Given** a compatibility failure beyond the scoped EventStore registration/proof-harness correction requires consumer behavior or deployment-topology changes
**When** the failure is classified
**Then** Story 2.7 fails closed and routes it to Story 2.12 when it belongs to authorized identity adoption, or to another separately approved story
**And** Story 2.7 does not broaden its scope silently.

### Story 2.8: Scoped Metadata-Rich Projection Notifications

**Requirements covered:** FR16

As a real-time client developer,
I want projection-changed notifications to carry bounded metadata and optional group scope,
So that clients can filter stale or out-of-order updates before re-querying without receiving tenant-wide noise.

**Acceptance Criteria:**

**Given** an existing signal-only projection notification consumer
**When** the projection-changed transport is extended
**Then** the existing `ProjectionChanged(projectionType, tenantId)` path and group naming remain backward compatible
**And** existing consumers build and pass unchanged.

**Given** a producer needs to broadcast metadata-rich projection details
**When** it calls the detail overload with projection type, tenant id, optional group scope, and metadata
**Then** the broadcaster sends the detail payload only to the matching scoped group
**And** tenant-wide groups do not receive scoped-only details.

**Given** notification metadata is provided by a domain
**When** metadata exceeds configured entry or byte limits
**Then** the framework rejects the detail notification before broadcast and never clips metadata
**And** metadata values are not logged above Debug level.

**Given** clients join or leave scoped projection groups
**When** the hub receives group operations
**Then** tenant authorization still runs before group membership changes
**And** group names validate projection type, tenant id, and scope using safe character and reserved-separator rules.

**Given** projection notification tests run
**When** SignalR and optional DAPR notification paths are exercised
**Then** scoped detail delivery, auth rejection, metadata rejection, and Redis backplane fan-out are covered
**And** an already-authorized legacy signal-only notification may remain compatible only without detail metadata, under existing validated group scope, and never as a tenant/group authorization bypass.

### Story 2.9: Generated Command-Status Location Policy

**Requirements covered:** FR12 (generated controller emission); governed by AD-3, AD-4, AD-17; forward-compatible with FR27.

As an external API consumer,
I want a generated command's `202 Accepted` to point me at a status resource I can actually reach — or at nothing — never at a dangling URL,
So that I never poll a 404 status link and my client's base authority stays correct.

**Acceptance Criteria:**

**Given** an external API host configured with a gateway command-status base URI
**When** a generated command controller returns `202 Accepted`
**Then** it emits an absolute `Location` of the form `{gatewayStatusBase}/api/v1/commands/status/{statusKey}` resolved at request time from a runtime option
**And** `statusKey` is the single command-status tracking field on `SubmitCommandResponse` (today `CorrelationId`), with no hard-coded assumption that `CorrelationId == MessageId`.

**Given** an external API host with no configured gateway command-status base
**When** a generated command controller returns `202 Accepted`
**Then** it emits `Retry-After` and no `Location` header
**And** no relative `/api/v1/commands/status/...` URL is ever emitted (fail-closed, AD-17).

**Given** a generated command that fails at the gateway
**When** the controller maps the gateway problem response
**Then** no `Location` header is emitted (behavior unchanged).

**Given** the generator and a compiled external API host under test
**When** generator-output and runtime tests run
**Then** absolute-when-configured, absent-when-unconfigured, and no-relative-URL behaviors are asserted
**And** the pre-existing assertions of the relative `/api/v1/commands/status/{CorrelationId}` string are replaced by the new policy assertions (`RestApiControllerGenerationTests`, `RestApiGeneratedControllerErrorSemanticsTests`).

**Given** the Sample external API host is the reference generated host
**When** the policy lands
**Then** the Sample host demonstrates configured (absolute `Location`) and fail-closed (no `Location`) behavior
**And** the spec-2-2 and spec-2-3 command-status `Location` deferred-work entries are closed.

**Sequencing note:** Independent of FR27 command-status re-keying — implementable now against the current `CorrelationId` key; the identifier value migrates transparently when Epic 4 re-keys.
**Placement note:** This is the renumbered post-retro hardening follow-on formerly tracked as Story 2.6.

### Story 2.10: Outbound DAPR Routing-Header Ownership

**Requirements covered:** FR13, FR14 (hardening); security posture FR26/FR28. Trigger: Epic 2 retro open action; defect from Story 2.3 review (deferred-work, spec-2-3). Governed by AD-18.

As a platform maintainer,
I want outbound DAPR service-invocation clients to own and replace the sidecar routing headers,
So that a caller- or inbound-supplied `dapr-app-id` / `dapr-api-token` can never duplicate or hijack sidecar routing or leak a token.

**Acceptance Criteria:**

**Given** a platform-owned outbound DAPR service-invocation handler in `Hexalith.EventStore.Client`
**When** it processes an outbound gateway request
**Then** it removes any pre-existing `dapr-app-id` and sets the configured app id as the single value, removes any pre-existing `dapr-api-token` and sets the configured token only when present (else leaves none)
**And** it runs as the innermost handler in the gateway-client chain.

**Given** the handler is wired through `AddEventStoreGatewayClient(appId, apiToken?)`
**When** Sample.Api, Sample.BlazorUI, and Admin.UI build
**Then** their three local `DaprAppIdHandler` copies are deleted
**And** each host wires only the platform extension.

**Given** a request already carries a conflicting `dapr-app-id` / `dapr-api-token`
**When** the outbound handler runs
**Then** the sidecar receives exactly one authoritative value and the injected value is discarded
**And** this is proven by a unit test that seeds pre-existing headers (single-value assertion), not only the happy path.

**Given** the guardrail runs
**When** a host declares a local DAPR routing-header handler or uses `TryAddWithoutValidation` for `dapr-app-id` / `dapr-api-token`
**Then** a structural test fails with a support-safe message.

**Given** the Tenants submodule carries an identical `DaprAppIdHandler`
**When** this story completes
**Then** the equivalent submodule change is recorded under the Story 2.12 Tenants approval/package-mode boundary
**And** it is not silently modified here.

**Given** Release build and focused tests run
**When** the change lands
**Then** all configured tests pass, including the new replacement and guardrail tests.

**Placement note:** Renumbered from Story 2.7; correct-course rationale remains in `sprint-change-proposal-2026-07-07-outbound-dapr-routing-header-policy.md`.

### Story 2.11: Query Provenance Consumption In Generated REST And Tenants

**Requirements covered:** FR12, FR15, FR34, NFR8, NFR14, NFR16; consumes Story 1.2 and is governed by AD-14/AD-15.

As an external API and Tenants UI consumer,
I want route provenance from Story 1.2 preserved and rendered safely,
So that generated REST and UI surfaces never present an opaque ETag or handler-computed response as projection-backed lifecycle evidence.

**Acceptance Criteria:**

**Given** generated REST receives `ProjectionBacked` metadata from Story 1.2
**When** it emits query headers or `304`
**Then** it forwards only present bounded projection version, lifecycle/freshness, ETag, served-at, warning, and paging evidence
**And** runtime tests prove the values originate from persisted projection state through the real gateway path.

**Given** generated REST or Tenants UI receives `HandlerComputed`, `Unknown`, missing, or invalid provenance
**When** it renders headers or lifecycle state
**Then** it omits projection-backed version/freshness headers and renders `Unknown`
**And** it never derives lifecycle from ETag, HTTP success, payload fields, or SignalR.

**Given** a query returns not-modified
**When** generated REST evaluates `304`
**Then** the response requires the strong gateway-authoritative validator allowed by the route provenance contract
**And** missing or invalid evidence fails safely without fabricating projection state.

**Given** a Tenants producer still aliases `ProjectionVersion := ETag`
**When** consumer behavior is exercised before Story 4.7 receives maintainer approval
**Then** the affected route remains `Unknown`
**And** this consumer story does not silently edit the Tenants producer or reopen Story 1.2 platform scope.

**Given** focused consumer tests run
**When** projection-backed, handler-computed, unknown, and invalid-provenance paths are exercised
**Then** generated headers, UI evidence states, and persisted read-model proof are asserted
**And** mock-only gateway metadata cannot close the story.

### Story 2.12: Tenants Runtime Identity Adoption And Package-Mode Validation

**Requirements covered:** FR15, FR21, FR22, NFR9, NFR12, NFR16
**Owner / review boundary:** Amelia (Developer); the Tenants maintainer reviews compatibility, the exact Tenants commit, and exact dependency identities. EventStore and release-owner approvals come from Story 1.20 and are not recreated here.
**Focused validation:** separate Debug/source and Release/package restores/builds; scoped Tenants Contracts, Integration, UI, and Server tests; exact package-byte/hash verification; and no mixed source/package EventStore graph.

As a Tenants release maintainer,
I want Tenants to adopt only the owner-approved EventStore runtime identity in source and package modes,
So that consumer migration is reproducible, maintainer-approved, and tied to the exact Story 1.20 evidence.

**Acceptance Criteria:**

**Given** Story 1.20 has not durably recorded `final_decision: available`, `authorize_consumer_migration: true`, a 40-hex `tested_runtime_sha`, named EventStore and release-owner approvals, and the approved package version plus SHA-256 inventory
**When** Story 2.12 activation is evaluated
**Then** it remains `backlog`, no implementation story file is created, and no Tenants, EventStore, or Builds dependency identity changes.

**Given** Story 1.20 authorizes migration and names the approved EventStore source SHA
**When** Debug/source mode is adopted
**Then** `references/Hexalith.EventStore` gitlink and checkout both equal that SHA, no EventStore submodule content is edited
**And** only Tenants-root-declared submodules are initialized.

**Given** the approved package version and hashes
**When** Release/package mode restores from an isolated cache
**Then** every resolved `Hexalith.EventStore*` asset is a package at the exact version, fetched bytes match the approved hashes
**And** the selected Builds commit already exposes that version.

**Given** Gateway is in the EventStore release manifest
**When** the dependency graph is aligned
**Then** `Hexalith.EventStore.Gateway` follows the same conditional source/package policy as DomainService
**And** Release assets contain no mixed Gateway-project/DomainService-package graph or any EventStore project reference.

**Given** source and package modes are aligned
**When** validation runs
**Then** Tenants preserves its domain-service, AppHost, and UI registration and passes the focused source/package restore, build, projection/query/provenance/freshness, and package-compatibility evidence
**And** completion records the Tenants maintainer-approved commit and exact accepted Tenants SHA.

## Epic 3: Release And Repository Reliability

Maintainers can release reproducibly with correct package/reference mode, aligned submodule and Aspire resource layout, deterministic release gates, live-sidecar integration coverage, shared supply-chain workflows, and manifest-governed package output.

### Story 3.1: Re-Tier Live-Sidecar Tests From Release Gate

**Requirements covered:** FR17

As a release maintainer,
I want live DAPR sidecar tests to run outside the per-push release gate,
So that releases are not blocked by cold-start sidecar flakiness while live-sidecar coverage remains preserved.

**Acceptance Criteria:**

**Given** tests require a live `daprd` sidecar
**When** project ownership is inspected
**Then** they live in `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`
**And** `tests/Hexalith.EventStore.Server.Tests` contains only deterministic tests.

**Given** the release workflow runs
**When** it executes Server.Tests
**Then** it runs `tests/Hexalith.EventStore.Server.Tests` unfiltered with no `Category!=LiveSidecar` selection
**And** it does not install or initialize DAPR solely for the release gate.

**Given** the dedicated integration workflow runs
**When** it provisions DAPR and executes `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`
**Then** live-sidecar tests run in their dedicated project/lane
**And** failures are visible without blocking semantic-release publishing.

**Given** live-sidecar tests start on a cold CI runner
**When** the shared DAPR fixture initializes
**Then** it performs readiness retry and warm-up actor round trips
**And** placement, activation, and Redis state paths are hot before assertions depend on them.

> Companion: **Story 3.10** provides the local DAPR/Aspire generated-API smoke preflight
> that classifies environment blockers before live-sidecar evidence is trusted.

### Story 3.2: Harden DAPR ETag Timeout For Integration Conditions

**Requirements covered:** FR18

As a test maintainer,
I want ETag actor request timeout to be overridable in tests,
So that cold-start integration latency does not produce false fail-open results.

**Acceptance Criteria:**

**Given** production code constructs `DaprETagService` through normal DI
**When** no custom request timeout is supplied
**Then** the existing production default timeout is preserved
**And** existing service registration remains compatible.

**Given** a live-sidecar test needs longer actor activation tolerance
**When** it constructs `DaprETagService` with an explicit timeout
**Then** the service uses the supplied timeout for actor proxy calls
**And** the test can assert persisted ETag behavior without relying on fail-open nulls.

**Given** timeout behavior is validated
**When** focused unit or integration tests run
**Then** both default and override paths are covered
**And** the change does not weaken fail-open behavior for genuine production actor failures.

### Story 3.3: References-Based Submodule Layout

**Requirements covered:** FR19

As a repository maintainer,
I want root-declared submodules to live under `references/`,
So that external Hexalith module checkouts are separated from EventStore source and tooling paths are consistent.

**Acceptance Criteria:**

**Given** root-declared submodules are configured
**When** `.gitmodules` is inspected
**Then** every root-declared Hexalith submodule path is under `references/`
**And** no root-level `Hexalith.*` submodule directory remains required.

**Given** the solution and MSBuild props are evaluated
**When** restore and Release build run against `Hexalith.EventStore.slnx`
**Then** project references and source path properties resolve through `references/`
**And** no stale root-level submodule path is required.

**Given** documentation, generated API reference docs, Aspire metadata, and LLM instructions mention Hexalith submodules
**When** repository-wide path scans run
**Then** references point to `references/Hexalith.*`
**And** nested submodules are not initialized or required.

**Given** consuming AppHosts need EventStore project metadata
**When** metadata helpers resolve EventStore project paths
**Then** they use the shared `references/Hexalith.EventStore` convention
**And** focused AppHost tests verify the updated paths.

### Story 3.4: Aspire Security Resource Naming

**Requirements covered:** FR20

As an operator,
I want the Aspire identity-provider resource to be named `security`,
So that the topology exposes the service role instead of the Keycloak implementation name.

**Acceptance Criteria:**

**Given** Keycloak is used as the identity-provider implementation
**When** the AppHost builds the Aspire model
**Then** the resource name is `security`
**And** Keycloak-specific realm import, ports, dependencies, and auth behavior remain unchanged.

**Given** integration fixtures need the identity-provider resource
**When** they resolve endpoints or create HTTP clients
**Then** they use the `security` resource name
**And** Keycloak-specific token and realm logic remains implementation-specific.

**Given** the Aspire topology is started
**When** `aspire describe` is inspected
**Then** the resource display name and `OTEL_SERVICE_NAME` are `security`
**And** dependent resources wait on `security`, not `keycloak`.

### Story 3.5: Shared Package Catalog And Source/Package Reference Modes

**Requirements covered:** FR21
**Activation gate:** Story 3.3 must reach `done` with current references-layout verification evidence before Story 3.5 starts.

As a package maintainer,
I want external Hexalith dependencies selected by build intent,
So that Debug builds can source-debug while Release builds depend on published packages.

**Acceptance Criteria:**

**Given** `UseHexalithProjectReferences=true` is explicitly supplied
**When** a build evaluates external Hexalith references and the root-declared source exists
**Then** the project/source edge is selected
**And** missing source falls back to the centrally pinned package edge.

**Given** `UseHexalithProjectReferences` is unset or explicitly `false`
**When** Debug, Release, or configuration-less evaluation runs
**Then** package references are selected
**And** no external source edge is activated.

**Given** `UseHexalithProjectReferences` is not explicitly set
**When** a Release build evaluates project references
**Then** external Hexalith package references are selected by default
**And** every package version resolves from `references/Hexalith.Builds/Props/Directory.Packages.props`.

**Given** project files reference external Hexalith libraries
**When** source and package modes are evaluated
**Then** each dependency has exactly one active source per mode
**And** host applications that are not library packages are not disguised as package dependencies.

**Given** cross-repo consumers such as Tenants depend on reusable EventStore gateway host components
**When** `UseHexalithProjectReferences=false` or Release package mode is selected
**Then** `Hexalith.EventStore.Gateway` is consumed through a centrally pinned `PackageReference` or explicitly documented as a deliberate source-only exception with validation coverage
**And** the dependency graph does not mix a source `Hexalith.EventStore.Gateway` with package-mode EventStore dependencies such as `Hexalith.EventStore.DomainService`, `Client`, `Server`, or `ServiceDefaults`.

**Given** dependency mode changes between restores
**When** validation commands run
**Then** restore is rerun before build or test
**And** stale project-reference assets cannot leak into package-mode validation.

**Given** source-owned Hexalith projects/root package props and the shared Builds catalog/governance surfaces are scanned
**When** NuGet version declarations are evaluated
**Then** every source-owned dependency version originates from `references/Hexalith.Builds/Props/Directory.Packages.props`
**And** consuming props contain no local `PackageVersion`, `VersionOverride`, or fallback dependency-version property.

**Given** an affected repository has not authorized or completed its migration
**When** Story 3.5 completion is evaluated
**Then** the repository, owner/approval requirement, scope, rollback boundary, and prescribed validation remain recorded as an open Story 3.5 blocker
**And** the story remains `in-progress` without editing that repository outside its maintainer's authority.

**Given** EventStore's existing local package-version entries
**When** the catalog migration is applied
**Then** `NBomber.Http` and `xunit.v3.extensibility.core` exist in Builds and all EventStore-local version declarations are removed
**And** effective evaluation resolves each migrated package ID exactly once from Builds.

**Given** local overrides are removed
**When** package mode restores and focused validation runs
**Then** adoption of the current Builds versions, including `System.CommandLine`, `ModelContextProtocol`, and `Microsoft.Extensions.TimeProvider.Testing`, is explicit and verified
**And** the migration is not accepted as a formatting-only change.

**Given** package-version documentation, scripts, samples, and dependency-update automation are reviewed
**When** this story completes
**Then** they identify Builds as the owner, `scripts/check-doc-versions.sh` reads the shared catalog successfully, and no official sample invites repository-local package versions
**And** consumer repositories do not open competing local-version updates.

**Given** tool-manifest, SDK, ephemeral consumer-fixture, or cache versions are encountered
**When** the governance scan reports them
**Then** they are classified explicitly
**And** they are not rewritten as NuGet CPM entries.

### Story 3.6: Manifest-Driven Release Packaging

**Requirements covered:** FR22

As a release maintainer,
I want EventStore package scope declared in a manifest,
So that release output is reviewable and cannot accidentally publish submodule packages.

**Acceptance Criteria:**

**Given** `tools/release-packages.json` declares the EventStore release inventory
**When** release pack scripts run
**Then** only manifest-listed EventStore packages are built and packed
**And** `GeneratePackageOnBuild=false` prevents submodule package emission during dependent builds.

**Given** package output is produced
**When** release validation scripts inspect the package directory
**Then** output exactly matches the manifest and release version
**And** unexpected `Hexalith.Commons.*`, `Hexalith.Tenants.*`, or other submodule packages fail validation.

**Given** semantic-release runs prepare and publish commands
**When** it packs and publishes NuGet artifacts
**Then** package-mode properties are asserted explicitly
**And** publish commands are scoped to `Hexalith.EventStore.*.nupkg` artifacts.

**Given** package metadata is validated
**When** generated NuGet packages are inspected
**Then** external Hexalith dependencies appear as package dependencies
**And** local source project paths do not leak into release package metadata
**And** `Hexalith.EventStore.Gateway` package metadata carries package dependencies, not source paths, so external package-mode consumers can restore without EventStore source checkout state.

### Story 3.7: Shared Workflow Caller Migration

**Requirements covered:** FR25, NFR10
**Owner / review boundary:** Amelia (Developer); Paige (Technical Writer) reviews workflow/documentation alignment.
**Focused validation:** workflow syntax/scans, deterministic Server.Tests, live-sidecar project listing, and Release solution build.

As a repository maintainer,
I want CI and release to use shared Hexalith.Builds workflow callers,
So that module-specific workflow code stays thin while the deterministic and live-sidecar lanes remain separate.

**Acceptance Criteria:**

**Given** CI, release, CodeQL, dependency review, and commitlint workflows are inspected
**When** the migration is complete
**Then** they are thin approved shared callers using `@main`
**And** local files retain only module triggers, concurrency, permissions, secrets, and inputs.

**Given** deterministic and live-sidecar suites run
**When** caller inputs are evaluated
**Then** `Server.Tests` runs unfiltered in the blocking lane and `Server.LiveSidecar.Tests` runs in the dedicated non-release-blocking DAPR lane
**And** semantic-release does not depend on live-sidecar success.

### Story 3.8: Workflow Reference And Validation Safety

**Requirements covered:** FR22, FR25, NFR9, NFR11
**Owner / review boundary:** Amelia (Developer); Paige (Technical Writer) reviews reference/cache and operational documentation safety.
**Focused validation:** shared-reference scans, package wrapper validation, release-secret preflight tests, and manifest-governance tests.

As a release maintainer,
I want workflow references, caches, package validators, and publish ordering to fail safely,
So that shared caller migration cannot publish the wrong artifacts or reuse an incompatible graph.

**Acceptance Criteria:**

**Given** shared workflow references and cache keys are scanned
**When** validation runs
**Then** approved Hexalith.Builds callers use the intended reference and dependency-mode inputs
**And** caches cannot mix Debug/source and Release/package assets.

**Given** package and release helpers run
**When** manifest output, consumer restore, credentials, and head SHA are checked
**Then** only the 14 manifest packages and approved `eventstore` container mapping can proceed
**And** secret or identity failure occurs before any irreversible publish.

**Given** docs and governance tests are reviewed
**When** stale custom-workflow or filter assumptions appear
**Then** validation fails or documentation is corrected
**And** `docs/ci.md` matches the actual workflow topology.

### Story 3.9: Supply-Chain Publishing Backlog

**Requirements covered:** FR25, NFR11
**Classification:** Planning/backlog artifact; it does not silently enable trusted publishing, attestations, SBOMs, or provenance.
**Owner / review boundary:** Paige (Technical Writer); Amelia (Developer) reviews feasibility and current workflow ownership.
**Focused validation:** `rg` scans for `NUGET_API_KEY`, trusted publishing, attestation, SBOM, and provenance plus structure review of `_bmad-output/planning-artifacts/backlog/supply-chain-publishing.md`.

As a release owner,
I want unresolved supply-chain publishing work recorded as a focused backlog product,
So that credential modernization and artifact provenance are not hidden inside completed caller migration.

**Acceptance Criteria:**

**Given** current publish workflows and documentation are inspected
**When** remaining supply-chain gaps are cataloged in `_bmad-output/planning-artifacts/backlog/supply-chain-publishing.md`
**Then** each trusted-publishing, attestation, SBOM, provenance, or credential item names scope, owner, dependency, risk, and validation expectation
**And** completed manifest/package safeguards are not reopened without a new approved story.

**Given** the backlog artifact is reviewed
**When** completion is requested
**Then** Paige and Amelia record the accepted inventory and evidence paths
**And** runtime/publishing changes remain unauthorized by this planning story.

### Story 3.10: Generated API DAPR/Aspire Smoke Preflight

**Requirements covered:** FR17 and FR34 validation enablement; governs NFR16 evidence quality. This is a validation/tooling story, not a runtime product capability. Companion to Story 3.1. Re-homed 2026-07-07 from the defunct TEST-1.1.

As a developer validating generated API proofs,
I want a local DAPR/Aspire smoke preflight that reports environment readiness, sidecar state, and generated API endpoints,
So that runtime blockers are classified support-safely before they are accepted as evidence against generated REST behavior.

**Acceptance Criteria:**

**Given** a developer is about to record an "Aspire smoke blocked" note or treat a generated-API endpoint failure as a product defect
**When** the preflight runs read-only by default
**Then** it classifies environment prerequisites (Docker, Aspire CLI, DAPR CLI/runtime, `daprd`/`placement`/`scheduler`, placement/scheduler reachability) as `blocked` separately from generated-API product failures
**And** starting placement, scheduler, or Aspire requires an explicit flag.

**Given** a live local topology is running
**When** the preflight discovers resources via `aspire describe`
**Then** it reports the generated Sample API host (`Hexalith.EventStore.Sample.Api`), EventStore, Redis/statestore, and their DAPR sidecars, with Tenants (`Hexalith.Tenants.Api`) reported only when present (`not-applicable` otherwise)
**And** it prefers HTTP endpoints for local VM smoke calls.

**Given** the optional Sample generated-API smoke is requested
**When** it exercises the generated command and query endpoints
**Then** it verifies accepted-command and ETag/`304` behavior, the persisted event, and resulting read-model/query state, never relying on status codes alone
**And** all output is support-safe (no tokens, JWTs, connection strings, private addresses, raw payloads, or stack traces).

**Given** the preflight completes
**When** it reports its result
**Then** it emits generated API endpoints, DAPR sidecar readiness, placement/scheduler readiness, and support-safe failure details (Epic 2 retro item 4 completion gate)
**And** missing persisted event or read-model/query evidence exits with the distinct `state-evidence-failure` result rather than success.

### Story 3.11: Validated Central Package Catalog Refresh

**Requirements covered:** FR21, FR22, FR25, NFR9, NFR10, NFR16
**Owner / review boundary:** Amelia (Developer) coordinates the Builds and EventStore changes; the Hexalith.Builds maintainer approves the catalog and exact Builds commit; affected repository maintainers approve coupled-family compatibility; Murat (Test Architect) reviews grouped validation evidence.
**Focused validation:** Builds catalog validator and restore/build/test; EventStore Release/package-mode restore, build, focused tests, pack validation, and documentation-version checks; affected representative-consumer validation for every changed family.

As a Hexalith release maintainer,
I want the shared NuGet catalog refreshed to latest validated compatible package versions,
So that all consuming repositories inherit current dependencies from one reproducible and compatibility-proven authority.

**Acceptance Criteria:**

**Given** Story 3.5 is not done or the Builds catalog does not contain every migrated EventStore package ID
**When** Story 3.11 activation is evaluated
**Then** the refresh remains `backlog`
**And** no catalog-wide version change is accepted.

**Given** the configured NuGet sources and the evaluated Builds catalog
**When** the freshness audit runs
**Then** it records every package ID, current version, latest stable and prerelease candidates as applicable, listing state, release-family membership, proposed disposition, and audit timestamp
**And** unresolved packages are never silently dropped.

**Given** a stable pin
**When** a compatible stable update exists
**Then** the latest validated stable release is selected
**And** intentional prerelease channels, major-version changes, framework/SDK-coupled packages, and stable/prerelease transitions require explicit disposition and proof.

**Given** packages share a Hexalith release property or another coupled version family
**When** versions are selected
**Then** the family uses an owner-approved common release inventory or an explicitly documented split
**And** individually newest but incompatible package versions are never combined. The `Hexalith.Tenants` divergence requires Tenants release-owner evidence before its pin changes.

**Given** the newest discovered release is incompatible, unavailable, unlisted, or older than the current pin
**When** the current compatible version is retained
**Then** the catalog records the reason, supporting validation or upstream constraint, and a removal trigger
**And** `Microsoft.OpenApi` remains on the latest proven 2.x line until ASP.NET Core 10 compatibility with v3 is demonstrated, while `Microsoft.SourceLink.GitHub` is not downgraded merely because search reports an older version.

**Given** candidate families are applied in reviewable groups
**When** validation runs
**Then** the Builds central-catalog validator, Builds validation, EventStore package-mode validation, and affected representative-consumer checks pass
**And** each group has an independent rollback boundary.

**Given** the refresh is submitted for review
**When** maintainers inspect its evidence
**Then** it identifies the exact Builds commit, package-source audit timestamp, accepted versions, retained exceptions, validation commands/results, and rollback grouping for each family
**And** Builds owns NuGet catalog update proposals while consumer repositories do not open competing local-version updates.

### Story 3.12: Multi-Platform EventStore Container Publishing Correction

**Requirements covered:** FR22, FR25, NFR9, NFR11, NFR16, NFR17; governed by AD-11, AD-12, AD-22, and Story 1.20 Acceptance Boundary 8.

**Owner / review boundary:** Amelia (Developer) coordinates EventStore and the shared publishing integration; the Hexalith.Builds maintainer approves the shared publisher implementation and exact Builds commit; Murat (Test Architect) reviews platform-set, digest, child-config, and smoke evidence; the release owner alone authorizes external publication and disposes the resulting artifact identity.

**Focused validation:** shared publisher contract tests; negative manifest/index fixtures; EventStore release-caller validation; immutable registry inspection; both-platform child-config validation; `linux/amd64` and `linux/arm64` smoke; exact 14-package inventory and independent SHA-256 verification.

As an EventStore release owner,
I want the shared release path to publish an exact two-platform OCI index,
So that a corrective EventStore release can satisfy Story 1.20 AC8 without changing package scope or overwriting v3.75.0.

**Acceptance Criteria:**

**Given** v3.75.0 is inspected
**When** its release assets and container registry object are recorded
**Then** all 14 package hashes from this proposal are preserved as observed non-authorizing evidence, the container digest/media type/config platform are recorded, and its missing `linux/arm64` descriptor is an explicit failed result
**And** no v3.75.0 package, Git tag, container tag, manifest, or registry object is overwritten or reclassified as approved.

**Given** the Hexalith.Builds shared publisher is corrected under maintainer authority
**When** the EventStore container mapping is published for a new semantic version
**Then** .NET SDK container support produces immutable `linux/amd64` and `linux/arm64` children and the version tag resolves to an OCI image index with media type `application/vnd.oci.image.index.v1+json`
**And** the index contains exactly one descriptor for each required platform, with no duplicate, extra, `unknown/unknown`, or variant descriptor.

**Given** the published index and child manifests are inspected
**When** release validation runs against immutable digests
**Then** the raw index bytes hash to the registry-reported index digest, every descriptor digest resolves, and each child config's `os`/`architecture` equals its descriptor
**And** a single-platform manifest, wrong media type, missing/duplicate/extra platform, config mismatch, unresolved child, or digest mismatch fails the release evidence gate.

**Given** the two immutable child digests are runnable
**When** platform smoke validation executes
**Then** both `linux/amd64` and `linux/arm64` variants start successfully and pass the same bounded support-safe EventStore health smoke
**And** emulation/setup failure is reported separately from a product image failure and cannot be recorded as a pass.

**Given** a corrective release is ready for external publication
**When** the publish action is requested
**Then** a separate durable release-owner authority record binds the repository, new version, source SHA, exact container repository, two-platform scope, owner, date, rationale, and validity window before registry or NuGet mutation occurs
**And** this planning/story approval alone grants no registry, NuGet, commit, branch, submodule, or push authority.

**Given** the corrective semantic release completes
**When** its evidence is assembled
**Then** exactly the 14 IDs in `tools/release-packages.json` share the new version and have independently verified SHA-256 values, while the container evidence records repository, index digest, raw-index hash, child digests/configs, exact platform set, source SHA, workflow run, and smoke results
**And** the package inventory remains 14, only `eventstore` is published as a container, and package/container version provenance is coherent.

**Given** Story 3.12 has produced a conforming release
**When** its handoff reaches Story 1.20
**Then** Story 1.20 independently revalidates every package/container identity, remaining production-path result, approval, and A/B/C authorization gate before selecting approved fields or authorizing migration
**And** Story 3.12 does not modify Parties or Tenants, approve G5, authorize consumer migration, or mark Story 1.20/Epic 1 done.

**Explicit exclusions:** no Dockerfile; no EventStore runtime behavior change; no new package or container mapping; no v3.75.0 mutation; no trusted-publishing, signing, SBOM, attestation, or credential-modernization expansion; no consumer dependency update; and no Story 1.20 proof-result approval.

**Rationale:** A focused release story fixes the shared publishing defect while preserving the independent evidence and human-approval boundaries of Story 1.20.

## Epic 4: Event Correctness And Recovery

Operators and consumers can trust persisted event metadata, idempotency, command status, replay dispatch, append behavior, global-position allocation, and crash recovery semantics under duplicate, concurrent, and failure conditions.

### Story 4.1: Event Identity And Duplicate Result Fidelity

**Requirements covered:** FR23

As an event consumer,
I want persisted event identity and duplicate command results to be stable and complete,
So that subscribers can deduplicate reliably and retried commands receive semantically identical responses.

**Acceptance Criteria:**

**Given** events are persisted from a command
**When** global position allocation is available
**Then** each event receives a non-zero actor-allocated global position
**And** local aggregate sequence numbers remain unchanged.

**Given** a CloudEvent is published for a persisted event
**When** the CloudEvent id is assigned
**Then** it uses the persisted event `MessageId`
**And** same-correlation, same-sequence events from different aggregates cannot collide.

**Given** a duplicate command is resolved from idempotency state
**When** the stored command result is returned
**Then** event count, result payload, backpressure fields, accepted/error state, and correlation information match the original result
**And** callers do not observe a degraded duplicate response.

**Given** focused server tests run
**When** event persistence, event publishing, idempotency record, and idempotency checker tests execute
**Then** global position stamping, CloudEvent id selection, and result fidelity are verified.

### Story 4.2: Resume And Idempotency Integrity

**Requirements covered:** FR27

As an operator,
I want command pipeline resume and idempotency checks to match the exact command being processed,
So that stale pipeline state cannot hijack a different command or prevent a valid retry.

**Acceptance Criteria:**

**Given** a pipeline record exists for an earlier command
**When** a different command reuses the same correlation id
**Then** resume logic compares `MessageId`, `CausationId`, and `CommandType`
**And** the stale record is drained or ignored without skipping execution of the new command.

**Given** idempotency state is checked for a command
**When** the caller is not authorized for the tenant
**Then** tenant validation happens before idempotency data is read
**And** unauthorized callers cannot infer command status or duplicate state across tenants.

**Given** a command previously failed because of transient infrastructure or persistence conflict
**When** the same message id is retried
**Then** retryable/transient records do not permanently block progress
**And** terminal domain outcomes remain safely deduplicated.

**Given** command status and archive records are written
**When** they are keyed or queried
**Then** primary lookup uses `{tenant}:{messageId}`
**And** correlation id is treated as an indexed field rather than the command identity.

### Story 4.3: Deterministic Replay Dispatch And Serialization

**Requirements covered:** FR29

As a domain maintainer,
I want event replay and projection dispatch to resolve event types deterministically,
So that rehydration cannot apply the wrong event or silently drop payload data.

**Acceptance Criteria:**

**Given** two event CLR type names share a suffix
**When** apply-method resolution runs during aggregate or projection replay
**Then** matching requires a `.` namespace boundary or exact full-name match
**And** ambiguous candidates fail with a clear diagnostic instead of choosing the wrong handler.

**Given** event dispatch dictionaries are built
**When** event types are registered
**Then** fully qualified event type names are registered as supported keys
**And** legacy short-name resolution remains compatible where unambiguous.

**Given** payloads are serialized and deserialized across command, rehydrate, project, and pub/sub paths
**When** those paths process event payloads
**Then** they use one shared `JsonSerializerOptions` definition
**And** casing or converter drift cannot silently produce empty/default payloads in one path.

**Given** replay and dispatch tests run
**When** suffix collision, ambiguity, and serialization cases are exercised
**Then** correct dispatch, clear failures, and shared serializer behavior are verified.

### Story 4.4: Committed Event Publication Recovery

**Requirements covered:** FR30

As an operator,
I want the system to recover events committed but not published,
So that a crash after persistence does not permanently lose subscriber delivery.

**Acceptance Criteria:**

**Given** an aggregate actor persisted events and metadata but crashed before publish completed
**When** the actor activates or a recovery sweep runs
**Then** it detects the persisted pipeline state at the stored/published boundary
**And** it resumes publication or converts the state into a drain/recovery record.

**Given** recovery publishes previously committed events
**When** the publish operation is retried
**Then** CloudEvent ids remain stable because they use event `MessageId`
**And** subscribers can deduplicate repeated delivery safely.

**Given** recovery cannot complete publication after bounded attempts
**When** the failure path is reached
**Then** the command status exposes retryable/recoverable state
**And** the system does not require resubmission with the identical correlation id to make progress.

**Given** crash-recovery tests run
**When** stored-but-unpublished pipeline states are simulated
**Then** publication completion, duplicate-safe retry, and unrecoverable diagnostics are verified.

### Story 4.5: Append Durability Race Evidence

**Requirements covered:** FR31

As an architect,
I want real DAPR conflict behavior proven before changing append fencing,
So that optimistic-concurrency design is based on observed state-store semantics instead of assumptions.

**Acceptance Criteria:**

**Given** two writers concurrently append to the same aggregate stream key
**When** the LiveSidecar race test runs against real Redis through DAPR
**Then** the resulting stream remains gapless and duplicate-free
**And** the test records whether one writer fails, retries, or both writes serialize safely.

**Given** DAPR actor-state transactions encounter a conflict
**When** the spike captures the thrown exception
**Then** the actual exception type and retry surface are documented
**And** existing `InvalidOperationException` conflict handling is confirmed or flagged as dead code.

**Given** the evidence is collected
**When** architecture reviews the result
**Then** the decision to add, change, or defer explicit ETag fencing is recorded
**And** no fencing implementation starts before this verification is complete.

**Given** the LiveSidecar test is added
**When** CI lanes are evaluated
**Then** the test is categorized correctly outside the deterministic release gate
**And** its blocker or result is documented in the appropriate integration lane.

### Story 4.6: Global Position Sharding Spec Renegotiation

**Requirements covered:** FR24

As a platform architect,
I want the global-position allocation strategy renegotiated and specified before sharding,
So that ordering metadata scales without violating the frozen global-ordering contract.

**Acceptance Criteria:**

**Given** the existing global-ordering spec is marked frozen after approval
**When** sharding is proposed
**Then** `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md` is updated through the required approval path
**And** the change records that positions can be gappy and are not strictly commit-ordered.

**Given** sharding options are evaluated
**When** tenant-scoped and domain-scoped allocation are compared
**Then** the selected option documents ordering guarantees, bottleneck reduction, failure modes, and migration impact
**And** consumers know how to interpret positions across shards.

**Given** implementation begins after spec approval
**When** global-position allocation is changed
**Then** existing per-event identity and CloudEvent id behavior remains stable
**And** focused tests verify monotonicity within the selected shard boundary.

### Story 4.7: Tenants Query Provenance Follow-Up

**Requirements covered:** FR15, NFR8, NFR16
**Classification:** External-authority follow-up requiring Tenants maintainer approval. This story is not the EventStore platform provenance prerequisite; that work is owned by Story 1.2.

As a platform maintainer coordinating with Tenants maintainers,
I want Tenants producer-side query freshness aliases removed or explicitly classified as non-projection-backed,
So that Tenants never presents an opaque ETag as projection version or current/stale evidence.

**Acceptance Criteria:**

**Given** Tenants producer code aliases `ProjectionVersion := ETag`
**When** maintainer-approved submodule work is scheduled
**Then** the aliasing is removed and genuine projection-backed freshness is sourced from persisted read-model evidence
**Or** the route is explicitly classified `HandlerComputed` or `Unknown` and consumers render Unknown.

**Given** maintainer approval is not yet available
**When** EventStore platform provenance enforcement ships through Story 1.2
**Then** EventStore blocks fabricated Current/Stale claims by route classification
**And** this Tenants follow-up remains visible without blocking the EventStore-owned platform story.

**Given** completion is requested
**When** authority and runtime identity are checked
**Then** the evidence names the Tenants maintainer-approved PR/commit, exact Tenants SHA, accepted scope, source/package mode, and focused production-path validation
**And** without that evidence the story remains `backlog` or `review`; EventStore closes the platform risk through the `Unknown` fallback contract rather than silently declaring the producer fixed.

## Epic 5: Security And Tenant Isolation

Administrators, tenants, and domain services are protected by fail-closed authentication, scoped authorization, safe configuration, app-layer internal credentials, tenant-aware runtime topology, and removal of trusted wire assertions.

### Story 5.1: Infrastructure Failure Cache Clear

**Requirements covered:** FR26

As an operator,
I want infrastructure-failure rejection paths to clear staged actor state before committing,
So that rejected outcomes cannot accidentally flush partially staged events.

**Acceptance Criteria:**

**Given** an aggregate actor has staged event or metadata state
**When** an infrastructure failure path handles a rejection
**Then** `StateManager.ClearCacheAsync()` runs before the rejection state is persisted
**And** previously staged events cannot be committed with the rejection outcome.

**Given** the concurrency-conflict path already clears cache
**When** the infrastructure-failure path is updated
**Then** both failure paths follow the same staged-state safety pattern
**And** the implementation uses `ConfigureAwait(false)` on awaited calls.

**Given** a test injects failure after events are staged
**When** the rejection path completes
**Then** stream metadata and event state remain unchanged
**And** only the intended rejection/status state is observable.

### Story 5.2: Admin Endpoint Authorization And Tenant Filters

**Requirements covered:** FR26

As a tenant administrator,
I want admin query endpoints to require authorization and tenant scoping,
So that cross-tenant event and command data is not exposed through anonymous or over-broad admin routes.

**Acceptance Criteria:**

**Given** public admin stream, trace, and command query controllers are called without credentials
**When** the request reaches the gateway
**Then** the response is unauthorized
**And** no cross-tenant event, trace, or command data is returned.

**Given** an authenticated caller lacks global administrator rights or tenant scope
**When** it requests another tenant's admin data
**Then** authorization denies the request
**And** tenant-filter behavior matches the gateway's tenant isolation rules.

**Given** `AdminCommandsQueryController` accepts a count or similar query limit
**When** the caller supplies an excessive value
**Then** the count is clamped to a safe maximum
**And** tests cover both default and excessive-count behavior.

**Given** admin write or sandbox JSON endpoints accept request bodies, including stream sandbox, projection reset/replay, consistency check, tenant command, dead-letter action, storage snapshot-policy, backup export/admission, and crypto-shredding workflow bodies
**When** request-size limits are applied
**Then** the default maximum request body size is `1_048_576` bytes and oversized requests fail safely with a bounded `413` or safe ProblemDetails response before DAPR or admin command services are invoked
**And** focused Admin.Server.Host or Admin.Server tests cover exact-limit accepted behavior, excessive-request rejection, and no upstream service invocation for at least one representative JSON write endpoint and the stream sandbox endpoint.

**Given** `AdminBackupsController.ImportStream` accepts exported stream content
**When** its endpoint-specific import limit is applied
**Then** the maximum request body size is `10 * 1024 * 1024` bytes
**And** focused tests cover accepted content at or below the limit, rejection above the limit, and bounded support-safe error output.

**Given** an admin endpoint has no request body or an operation remains intentionally unavailable
**When** request-size applicability is reviewed
**Then** the implementation story records the exact endpoint, body type, and reason the limit is not applicable
**And** documentation-only closure is forbidden unless an owner explicitly records a deferred implementation exception.

### Story 5.3: Production Authentication Guards And Secret Stripping

**Requirements covered:** FR26

**Architecture gate:** AD-16 (health/probe anonymous-access contract). If this story or any host introduces a global fallback authorization policy or default-deny endpoint convention, the explicit probe-anonymity contract lands in the same or an earlier slice — never after.

As a security operator,
I want production authentication to fail closed and committed configs to contain no forgeable admin identity,
So that insecure local-development authentication cannot leak into deployed environments.

**Acceptance Criteria:**

**Given** the Admin UI base configuration is committed
**When** it is inspected
**Then** it contains no signing key, username, password, or global-admin identity
**And** development-only credentials live only in development configuration.

**Given** the gateway or Admin.Server.Host starts outside Development
**When** no authority is configured or symmetric-key mode would be used
**Then** startup fails unless an explicit break-glass option is configured
**And** the failure message identifies the unsafe authentication posture.

**Given** production JWT validation is configured
**When** options are validated
**Then** HTTPS metadata is required where applicable
**And** accepted token algorithms are pinned through token validation parameters.

**Given** a host introduces a global fallback authorization policy or a default-deny endpoint convention as part of fail-closed hardening
**When** that policy is applied
**Then** the health, liveness, and readiness endpoints `/health`, `/alive`, and `/ready` are explicitly pinned `AllowAnonymous` (or an equivalent auth-exempt convention) in the same or an earlier slice
**And** the fallback policy or deny-by-default posture is not weakened, scoped down, or removed to make probes reachable (AD-16).

**Given** the fail-closed default is active on a host
**When** health-endpoint authorization is exercised on the real host pipeline
**Then** `/health`, `/alive`, and `/ready` return their health status to an unauthenticated caller
**And** a representative protected endpoint on the same host denies an unauthenticated caller
**And** anonymous probe responses remain support-safe, disclosing no component names, dependency detail, versions, tenant data, or exception text outside Development.

**Given** authentication guard tests run
**When** Development and Production options are evaluated
**Then** development fallback remains available
**And** production insecure configurations fail deterministically.

### Story 5.4: Admin Surface Safety Hygiene

**Requirements covered:** FR26

As an administrator,
I want admin tooling and documentation to avoid unsafe defaults and misleading test guidance,
So that operational workflows do not encourage accidental destructive or insecure behavior.

**Acceptance Criteria:**

**Given** admin Swagger is configured
**When** the application runs outside Development
**Then** admin Swagger is disabled or gated appropriately
**And** tests verify the environment-specific behavior.

**Given** CLI commands perform destructive admin operations
**When** a caller invokes them without explicit `--confirm` or `--yes`
**Then** the command refuses to proceed
**And** the confirmation behavior is covered by focused CLI tests.

**Given** Admin.Server.Host reads or propagates correlation identifiers
**When** correlation middleware validates identifiers
**Then** it uses ULID-safe parsing or accepted non-whitespace semantics rather than `Guid.TryParse`
**And** tests cover valid ULID and invalid inputs.

**Given** repository guidance describes test baselines
**When** CLAUDE/project-context documentation is updated
**Then** it no longer claims Server.Tests cannot build if that is stale
**And** it documents the current release gate and integration-test lane accurately.

### Story 5.5: Internal And Domain-Service Trust Boundary

**Requirements covered:** FR28

**Architecture gate:** AD-16 (health/probe anonymous-access contract). The domain-service credential enforcement covers only the canonical DAPR endpoints; the probe endpoints stay explicitly anonymous and support-safe.

As a platform security maintainer,
I want internal and domain-service endpoints to require app-layer credentials,
So that sidecar, gateway, and domain-service calls cannot mint trust from headers or wire flags alone.

**Acceptance Criteria:**

**Given** the Dapr internal authentication handler receives a `dapr-caller-app-id` header
**When** no DAPR app API token or equivalent internal credential proves sidecar origin
**Then** the handler does not mint global administrator claims
**And** forged plaintext headers are rejected.

**Given** the domain-service SDK maps `/process`, `/query`, `/replay-state`, `/project`, and operational metadata endpoints
**When** requests reach those endpoints
**Then** an app-layer credential check is enforced
**And** unauthenticated network-local callers cannot execute domain operations.

**Given** the domain-service SDK enforces app-layer credentials on its canonical endpoints
**When** the credential requirement is applied, including any fallback or default-deny policy on the host
**Then** the requirement covers `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata`
**And** the health, liveness, and readiness endpoints `/health`, `/alive`, and `/ready` remain explicitly `AllowAnonymous` and support-safe so DAPR app-health and orchestration probes are not blocked (AD-16).

**Given** command or query wire envelopes include administrator-related flags
**When** the domain service evaluates authorization context
**Then** it ignores or removes wire-asserted `IsGlobalAdmin` semantics
**And** gateway-verified claims are the source of authorization truth.

**Given** projection notification endpoints receive pub/sub callbacks
**When** a caller posts projection-changed data
**Then** sidecar/pubsub caller identity is verified
**And** forged external requests cannot broadcast projection changes.

### Story 5.6: AppHost Component Loading And Sidecar-Argument Parity

**Requirements covered:** FR32, NFR2, NFR17
**Owner / review boundary:** Winston (Architect); Amelia (Developer) reviews AppHost implementation and tests.
**Focused validation:** `Hexalith.EventStore.AppHost.Tests` and AppHost/DAPR component-path scans.

As an operator,
I want AppHost to load the intended DAPR components explicitly,
So that local sidecars cannot silently use unscoped generated components.

**Acceptance Criteria:**

**Given** AppHost starts each DAPR sidecar
**When** sidecar annotations and arguments are inspected
**Then** the intended state-store, scoped/dead-letter pub/sub, ACL, and resiliency component paths are explicit for the named resource
**And** placement, scheduler, app-health, app-id, and component arguments match architecture conventions.

**Given** a component path or argument is absent, duplicated, or points to a generated fallback
**When** AppHost tests run
**Then** the focused test fails with the resource and expected path
**And** deny-by-default posture is not relaxed to make the test pass.

### Story 5.7: Production DAPR Component And ACL Parity

**Requirements covered:** FR32, NFR1, NFR2, NFR17
**Owner / review boundary:** Winston (Architect); Amelia (Developer) reviews production YAML and deployment boundary.
**Focused validation:** structured scans/tests for `deploy/dapr`, access-control, component scopes, key prefixes, topics, and app IDs.

As a deployment operator,
I want production DAPR components and ACLs to match the approved runtime posture,
So that tenant isolation does not change between local proof and deployment.

**Acceptance Criteria:**

**Given** production state-store, pub/sub, subscription, resiliency, and access-control YAML is parsed
**When** scopes and metadata are compared with AppHost resources
**Then** key prefixes, tenant/app scopes, topic routes, app IDs, and allowed operations match
**And** access control remains deny by default.

**Given** a new resource, topic, component, or route is introduced
**When** production templates are updated
**Then** all affected scopes and ACLs change in the same story
**And** no broad wildcard silently replaces a named rule.

### Story 5.8: Runtime Topology Drift Tests

**Requirements covered:** FR32, NFR2, NFR16, NFR17
**Owner / review boundary:** Amelia (Developer); Murat (Test Architect) reviews actual-runtime evidence.
**Focused validation:** AppHost tests plus the dedicated integration lane asserting sidecar arguments and loaded component posture.

As a quality maintainer,
I want topology drift tests against configured and running resources,
So that stale AppHost, YAML, ACL, or deployment assumptions fail before release.

**Acceptance Criteria:**

**Given** AppHost configuration and production templates are loaded
**When** drift tests compare resources, component paths, app IDs, scopes, topics, and ACL rules
**Then** every expected mapping is explicit and exact
**And** missing, extra, or conflicting mappings fail deterministically.

**Given** the dedicated DAPR/Aspire lane is available
**When** runtime evidence is collected
**Then** actual sidecar arguments and component access are asserted for enumerated high-risk resources
**And** documentation or mocks alone cannot close runtime parity.

### Story 5.9: Deployment And Operator Documentation Alignment

**Requirements covered:** FR32, NFR17
**Owner / review boundary:** Paige (Technical Writer); Winston (Architect) reviews topology accuracy.
**Focused validation:** documentation link/path checks and structured comparison with AppHost/deploy artifact names.

As an operator,
I want deployment documentation to name the topology that code and tests enforce,
So that runbooks do not direct operators toward stale app IDs, components, topics, or ACLs.

**Acceptance Criteria:**

**Given** Stories 5.6-5.8 establish the runtime topology
**When** deployment and operator docs are updated
**Then** component paths, app IDs, resource names, topics, scopes, key-prefix posture, health behavior, and deny-by-default ACLs match the verified artifacts
**And** local-only or environment-specific differences are explicit.

**Given** documentation validation runs
**When** referenced paths and identifiers are checked
**Then** stale or missing references fail
**And** no runtime behavior change is hidden in this documentation-only child.

## Epic 6: Bounded Cost And Event Evolution

Platform users can operate long-lived streams with bounded snapshot and projection cost, sequence-safe projection updates, explicit global-position scaling, event schema versioning/upcasting, validated event metadata, and cancellation-aware processing seams.

### Story 6.1: Folded Snapshot Frozen Spec

**Requirements covered:** FR33
**Classification:** Architecture/readiness gate. Completion authorizes Story 6.2 to start but does not count as runtime implementation progress.

As a platform architect,
I want folded snapshot behavior specified before implementation,
So that snapshot cost is bounded without changing recovery semantics unexpectedly.

**Acceptance Criteria:**

**Given** automatic snapshots currently risk nesting event history
**When** the folded snapshot spec is written
**Then** it defines the target folded-state payload shape, keying, replay behavior, migration handling, and compatibility rules
**And** it explains how the manual replay-state path is reused or aligned.

**Given** snapshot storage affects recovery and retention
**When** the spec is reviewed
**Then** it documents broker/snapshot plaintext boundary, retention implications, and crypto-shred considerations
**And** approval is recorded before implementation starts.

**Given** Story 6.1 is completed
**When** the folded snapshot spec is produced
**Then** the exact output path is `_bmad-output/implementation-artifacts/spec-folded-snapshot.md`
**And** the artifact records approver, approval date, accepted scope, rejected alternatives, open decisions, migration posture, and explicit authorization for Story 6.2 to start.

### Story 6.2: Folded Snapshot Implementation

**Requirements covered:** FR33

As an operator,
I want automatic snapshots to store folded aggregate state,
So that snapshot payload size stays bounded as event streams grow.

**Acceptance Criteria:**

**Given** a stream reaches the automatic snapshot threshold
**When** the snapshot is written
**Then** the persisted snapshot contains folded state rather than nested full event history
**And** snapshot keying remains compatible with the approved spec.

**Given** Story 6.2 implementation starts
**When** implementation preflight runs
**Then** `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` exists and contains approval evidence
**And** implementation tasks cite the approved spec sections they satisfy.

**Given** an aggregate is rehydrated from events and snapshots
**When** a folded snapshot exists
**Then** replay reconstructs the same state as a full event replay
**And** legacy or absent snapshots continue to work according to the migration plan.

**Given** snapshot cost tests run
**When** a stream grows across multiple snapshot intervals
**Then** snapshot payload size remains bounded
**And** Release build remains clean under warnings-as-errors.

### Story 6.3: Projection Delivery Cost And Sequence Guard Spec

**Requirements covered:** FR33
**Classification:** Architecture/readiness gate. Completion authorizes Story 6.4 to start but does not count as runtime implementation progress.

As a platform architect,
I want projection cost and ordering behavior specified before implementation,
So that projection optimizations do not introduce out-of-order state regressions.

**Acceptance Criteria:**

**Given** projection delivery currently full-replays streams
**When** the projection cost spec is written
**Then** it defines checkpoint short-circuit behavior, tail delivery, incremental handler assumptions, and fallback paths
**And** it orders source-sequence guards before cost-reduction changes.

**Given** projections may be updated by multiple replicas
**When** the sequence-guard design is specified
**Then** it defines how stale or out-of-order source sequence writes are rejected or ignored
**And** it documents actor-id and domain scoping assumptions.

**Given** Story 6.3 is completed
**When** the projection cost and sequence guard spec is produced
**Then** the exact output path is `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md`
**And** the artifact records approver, approval date, accepted scope, rejected alternatives, open decisions, and explicit authorization for Story 6.4 to start.

**Given** Stories 1.18 and 1.19 own the production correctness baseline
**When** this optimization spec defines checkpoint short-circuit, tail delivery, and sequence-guard changes
**Then** it starts from their approved duplicate, gap, page-safety, staging, promotion, and replay-equivalence invariants
**And** it does not redefine or weaken those guarantees.

### Story 6.4: Projection Cost And Sequence Guard Implementation

**Requirements covered:** FR33

As an operator,
I want projections to avoid unnecessary full-stream replay while preventing stale writes,
So that projection updates remain correct and cheaper on long streams.

**Acceptance Criteria:**

**Given** a projection checkpoint already equals the stream head
**When** projection delivery runs
**Then** it can short-circuit after reading metadata
**And** no unnecessary full-stream replay is performed.

**Given** Story 6.4 implementation starts
**When** implementation preflight runs
**Then** `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md` exists and contains approval evidence
**And** Stories 1.18 and 1.19 are complete
**And** implementation tasks cite the approved spec sections and correctness-baseline scenarios they preserve.

**Given** a projection checkpoint trails the stream head
**When** incremental projection delivery is available
**Then** only the required tail is delivered to the projection path
**And** fallback behavior remains correct for handlers that require full replay.

**Given** a projection update carries a stale source sequence
**When** the projection actor or store attempts to write it
**Then** the sequence guard prevents state regression
**And** tests cover same-sequence, stale-sequence, and newer-sequence updates.

**Given** projection cost tests run
**When** long-stream projection scenarios are exercised
**Then** reduced replay behavior and correctness guards are both verified
**And** the full Stories 1.18/1.19 persisted-outcome corpus remains green.

### Story 6.5: Event Versioning And Upcasting Spec

**Requirements covered:** FR33
**Classification:** Architecture/readiness gate. Completion authorizes Story 6.6 to start but does not count as runtime implementation progress.

As a domain maintainer,
I want event schema evolution specified before contracts harden,
So that domains can change event payloads without breaking replay.

**Acceptance Criteria:**

**Given** event metadata currently lacks an explicit payload-versioning path
**When** the versioning spec is written
**Then** it defines persisted event type, payload version, legacy fallback, and migration behavior
**And** it identifies public contract changes before implementation starts.

**Given** replay requires old events to become current domain payloads
**When** upcasting is specified
**Then** the spec defines `IEventUpcaster` chain ordering, failure behavior, telemetry, and test expectations
**And** allow-list polymorphic deserialization remains intact.

**Given** public seams such as `IDomainProcessor`, `/query`, and `/project` need cancellation support
**When** the spec is finalized
**Then** cancellation-token contract changes are included
**And** the breaking-change or compatibility impact is documented.

**Given** Story 6.5 is completed
**When** the event versioning and upcasting spec is produced
**Then** the exact output path is `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md`
**And** the artifact records approver, approval date, accepted scope, rejected alternatives, open decisions, public contract impact, and explicit authorization for Story 6.6 to start.

### Story 6.6: Event Versioning And Upcasting Implementation

**Requirements covered:** FR33

As a domain maintainer,
I want replay to understand event contract type and payload version,
So that old events can be safely upcast and processed by current domain code.

**Acceptance Criteria:**

**Given** new events are persisted
**When** event metadata is written
**Then** metadata includes the kebab event contract type and explicit payload version
**And** CLR-name based resolution remains available only as documented legacy fallback.

**Given** Story 6.6 implementation starts
**When** implementation preflight runs
**Then** `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` exists and contains approval evidence
**And** implementation tasks cite the approved spec sections they satisfy.

**Given** replay reads an older payload version
**When** an applicable upcaster chain is registered
**Then** the payload is transformed to the current version before applying domain logic
**And** missing or failed upcasters produce clear diagnostics without silent data corruption.

**Given** event metadata is constructed
**When** tenant, domain, and aggregate identity components are supplied
**Then** `AggregateIdentity` component validation is applied
**And** invalid identity data is rejected at the boundary.

**Given** processing, query, or projection operations are canceled
**When** cancellation tokens are propagated through the updated public seams
**Then** long-running operations observe cancellation
**And** tests cover token propagation without breaking existing behavior.

## Epic 7: Operator Trust, Admin Honesty, And Future Capabilities

Operators get honest admin UX, attributable admin actions, production deployment hardening, reliable higher-tier test evidence, and explicit backlog tracks for erasure, admin OIDC, aggregate test kits, and generator hardening.

### Story 7.1: Delivery Contract And Poison Handling

**Requirements covered:** FR34

As an event subscriber operator,
I want delivery semantics and poison handling to be explicit and enforced,
So that subscriber failures do not become infinite retry storms or hidden data-loss paths.

**Acceptance Criteria:**

**Given** EventStore publishes domain events to subscribers
**When** delivery contracts are documented
**Then** the contract states at-least-once and unordered delivery
**And** subscribers are instructed to deduplicate by `MessageId` and use `SequenceNumber` only within the correct aggregate semantics.

**Given** a drain or subscriber delivery repeatedly fails
**When** retry limits or max age are exceeded
**Then** the event is moved to a dead-letter or poison-handling path
**And** diagnostics preserve enough metadata to investigate without exposing raw payload secrets.

**Given** duplicate deliveries arrive while the first delivery is still in progress
**When** deduplication state is checked
**Then** in-progress duplicates are handled as retryable or deferred
**And** the in-memory dedup set is bounded to prevent unbounded growth.

**Given** delivery tests run
**When** duplicate, late, out-of-order, retry-exhausted, and dead-letter scenarios are exercised
**Then** delivery semantics and poison handling are verified.

### Story 7.2: Admin Claims Normalization

**Requirements covered:** FR34, NFR1, NFR2
**Owner / review boundary:** Amelia (Developer); Winston (Architect) reviews authorization semantics.
**Focused validation:** Admin.Server claims/authorization tests and cross-tenant denial tests.

As an administrator,
I want Admin claims normalized exactly like gateway claims,
So that missing or malformed tenant/permission input cannot widen access.

**Acceptance Criteria:**

**Given** Admin.Server receives user claims
**When** transformation runs
**Then** `tenants` and `permissions` use the shared canonical representation
**And** null, missing, malformed, or empty tenant scope denies access rather than granting all-tenant visibility.

**Given** normalized claims are used by an Admin query or action
**When** same-tenant, cross-tenant, global-admin, and missing-permission cases run
**Then** authorization matches gateway policy
**And** denial exposes no resource existence or unsafe claim detail.

### Story 7.3: State-Mutating Admin Audit

**Requirements covered:** FR34, NFR2, NFR15, NFR16
**Owner / review boundary:** Amelia (Developer); Murat (Test Architect) reviews durable audit evidence.
**Focused validation:** Admin.Server/CLI mutation tests asserting authenticated actor, tenant, action, outcome, correlation, and persisted audit record.

As an operator,
I want every state-mutating admin action attributable,
So that privileged changes can be audited without exposing sensitive payloads.

**Acceptance Criteria:**

**Given** an authorized state-mutating Admin action succeeds, fails, or is denied
**When** the action completes
**Then** a structured audit record captures authenticated user id, tenant, action, outcome, and ULID-safe correlation id
**And** it excludes tokens, raw payloads, secrets, and stack traces.

**Given** audit persistence fails
**When** a mutation would otherwise proceed
**Then** the configured fail-closed policy is applied and the outcome is support-safe
**And** focused tests assert both business state and audit end state.

### Story 7.4: Honest Deferred Admin Operations

**Requirements covered:** FR34, NFR15
**Owner / review boundary:** Amelia (Developer); Sally (UX Designer) reviews UI honesty and route behavior.
**Focused validation:** Admin.Server and Admin.UI tests for every enumerated deferred operation.

As an administrator,
I want unavailable operations represented honestly,
So that backup, restore, import, compaction, and other deferred capabilities cannot appear to succeed.

**Acceptance Criteria:**

**Given** an Admin operation is not implemented
**When** the UI and server route inventory is inspected
**Then** the UI hides or disables it and any retained endpoint returns `501`
**And** no mock success, optimistic toast, or fabricated completion state is shown.

**Given** each deferred operation is exercised
**When** focused UI/server tests run
**Then** the enumerated route/control behavior and support-safe message are asserted
**And** implementation remains owned by its explicit future backlog.

### Story 7.5: Shared Typed Admin Client

**Requirements covered:** FR34, NFR12, NFR14
**Owner / review boundary:** Amelia (Developer); Winston (Architect) reviews the typed-client boundary.
**Focused validation:** Admin.Abstractions, Admin.Server, Admin.UI, and Admin.Cli client-contract tests.

As an Admin surface developer,
I want one shared typed Admin client,
So that UI and tools do not duplicate request/response mapping or bypass authorization semantics.

**Acceptance Criteria:**

**Given** Admin UI and CLI call supported server operations
**When** client composition is inspected
**Then** both consume the shared typed contract/client surface
**And** duplicated per-host HTTP mapping is removed without adding per-message MVC controllers to interactive UI hosts.

**Given** success, denial, not-found, unavailable, validation, and cancellation outcomes occur
**When** typed-client tests run
**Then** each outcome maps consistently and support-safely
**And** planning a client is insufficient: the implementation and focused tests must exist.

### Story 7.6: Secret-Store Configuration

**Requirements covered:** FR34, NFR4, NFR17
**Owner / review boundary:** Winston (Architect); Amelia (Developer) reviews deployment configuration and tests.
**Focused validation:** structured production-manifest scans for secret stores, `secretKeyRef`, and prohibited plaintext placeholders.

As a production operator,
I want deployment secrets resolved through approved secret stores,
So that committed component configuration contains no forgeable operational credentials.

**Acceptance Criteria:**

**Given** production DAPR or application configuration requires a secret
**When** manifests are parsed
**Then** the value resolves through the named secret-store component and `secretKeyRef`
**And** no plaintext key, token, password, signing key, or unsafe `{env:...}` substitute remains in committed production posture.

**Given** a required secret or store is missing
**When** deployment validation runs
**Then** it fails before the dependent resource starts
**And** diagnostics name the configuration key without disclosing the value.

### Story 7.7: Readiness And DAPR App-Health

**Requirements covered:** FR34, NFR1, NFR17
**Architecture gate:** AD-16.
**Owner / review boundary:** Amelia (Developer); Winston (Architect) reviews probe ownership and fail-closed compatibility.
**Focused validation:** AppHost and real-host-pipeline tests for the explicit resource/probe inventory.

As an operator,
I want explicit readiness and DAPR app-health behavior,
So that orchestration removes unhealthy traffic without weakening endpoint authorization.

**Acceptance Criteria:**

**Given** EventStore, Admin.Server, Sample, and configured domain-service resources expose probes
**When** AppHost sidecar arguments and host endpoints are inspected
**Then** the resource inventory explicitly maps liveness to `/alive`, readiness to `/ready`, health to `/health`, and DAPR app-health to the architecture-approved path
**And** required state-store checks carry the `ready` tag.

**Given** fail-closed authorization is active
**When** unauthenticated probes and a representative protected endpoint run on the same host
**Then** `/health`, `/alive`, and `/ready` remain explicitly anonymous/support-safe while the protected endpoint denies access
**And** no fallback policy is weakened to reach probes.

### Story 7.8: DAPR Resiliency

**Requirements covered:** FR34, NFR17
**Owner / review boundary:** Winston (Architect); Amelia (Developer) reviews runtime target alignment.
**Focused validation:** structured resiliency YAML tests and focused timeout/retry/circuit-breaker behavior tests.

As an operator,
I want DAPR resiliency policies to cover the exact application targets used at runtime,
So that invocation failures have bounded, documented behavior.

**Acceptance Criteria:**

**Given** runtime service invocation targets are enumerated
**When** resiliency YAML is parsed
**Then** every required app target has the approved timeout, retry, and circuit-breaker policy
**And** no stale or missing app ID is silently ignored.

**Given** transient, terminal, timeout, and open-circuit conditions occur
**When** focused tests exercise the policy
**Then** attempts and failure timing remain bounded
**And** documentation states which operations are safe to retry.

### Story 7.9: Immutable Production Images

**Requirements covered:** FR34, NFR11, NFR17
**Owner / review boundary:** Amelia (Developer); Paige (Technical Writer) reviews deployment/release identity documentation.
**Focused validation:** release/deploy scans proving production overlays use an immutable git-SHA tag or digest mapped to release provenance.

As a deployment operator,
I want production overlays to reference immutable image identity,
So that a deployed EventStore version can be traced to one approved build.

**Acceptance Criteria:**

**Given** production overlays reference EventStore images
**When** manifests are rendered or scanned
**Then** they use an immutable git-SHA tag or digest, not only a mutable tag
**And** release provenance maps that identity to the source commit and manifest-governed artifacts.

**Given** an image identity is missing, mutable-only, or unmapped
**When** deployment validation runs
**Then** production promotion fails
**And** support-safe diagnostics identify the affected resource.

### Story 7.10: Integration CI Recovery

**Requirements covered:** FR34, NFR10, NFR16
**Owner / review boundary:** Amelia (Developer); Murat (Test Architect) reviews lane ownership and execution evidence.
**Focused validation:** workflow syntax plus infra-free and dedicated Aspire/DAPR integration commands.

As a quality maintainer,
I want every integration suite assigned to a runnable CI lane,
So that high-risk behavior is not left as an undocumented local-only promise.

**Acceptance Criteria:**

**Given** integration projects and subsets are inventoried
**When** workflow ownership is checked
**Then** infra-free/lightweight suites run in deterministic CI and full Aspire/DAPR suites run in a dedicated documented lane
**And** each excluded suite has an owner, blocker, and expiry rather than a permanent skip.

**Given** a lane is triggered
**When** environment setup or product tests fail
**Then** blocked infrastructure and product failures are classified separately
**And** results remain visible without misclassifying release-gate status.

### Story 7.11: Persisted-State Evidence And Read-Back Helpers

**Requirements covered:** FR34, NFR7, NFR16
**Owner / review boundary:** Amelia (Developer); Murat (Test Architect) reviews persisted evidence semantics.
**Focused validation:** `Hexalith.EventStore.Testing.Integration.Tests` and enumerated high-risk integration scenarios.

As an integration-test author,
I want shared read-back helpers and mandatory persisted evidence,
So that success cannot be inferred only from HTTP status or mock calls.

**Acceptance Criteria:**

**Given** command acceptance, query projection, delivery, erasure, batch recovery, idempotency, rebuild, or publication recovery is tested
**When** the scenario completes
**Then** the test asserts its required Redis/state-store/read-model/CloudEvent detail, index, marker, lifecycle, checkpoint, or publication end state
**And** the evidence is enumerated per scenario rather than qualified by “where applicable.”

**Given** multiple suites need state inspection
**When** helpers are extracted
**Then** `Hexalith.EventStore.Testing.Integration` provides support-safe typed read-back seams
**And** helpers do not hide tenant scope, eventual-consistency waits, or assertion boundaries.

### Story 7.12: Fake And Integration Test Reclassification

**Requirements covered:** FR34, NFR10, NFR16
**Owner / review boundary:** Murat (Test Architect); Amelia (Developer) reviews project moves and build impact.
**Focused validation:** test-project inventory, workflow classification checks, and focused real-infrastructure replacements.

As a quality maintainer,
I want test labels to reflect actual external dependencies,
So that fake-only evidence is not mistaken for integration proof.

**Acceptance Criteria:**

**Given** a test uses only substitutes or in-memory fakes
**When** classification is reviewed
**Then** it moves to unit scope unless rewritten against the external dependency it claims to verify
**And** retained integration tests name the real state store, DAPR sidecar, broker, Aspire topology, or host boundary they exercise.

**Given** projects are reclassified
**When** CI inventories run
**Then** every test project belongs to one explicit workflow or dated quarantine
**And** deterministic and live lanes remain separate.

### Story 7.13: Advisory And Performance Workflow Hygiene

**Requirements covered:** FR34, NFR10
**Owner / review boundary:** Amelia (Developer); Murat (Test Architect) reviews trigger and result semantics.
**Focused validation:** workflow syntax, trigger scans, and one recorded green or quarantined result per advisory/performance job.

As a quality owner,
I want advisory and performance workflows to be runnable and accountable,
So that permanently red or never-triggered jobs do not masquerade as quality controls.

**Acceptance Criteria:**

**Given** an advisory or performance job exists
**When** workflow configuration is inspected
**Then** it has a runnable trigger such as `workflow_dispatch`
**And** required browser/tool setup and bounded timeout are explicit.

**Given** the job is evaluated
**When** completion evidence is recorded
**Then** it has a green run or a quarantine entry with owner, reason, and expiry
**And** removal requires an explicit disposition rather than silent deletion.

### Story 7.14: Consolidated EventStore Admin Dashboard Migration

**Requirements covered:** FR13, FR15, FR34, NFR14, NFR15
**Owner / review boundary:** Amelia (Developer); Sally (UX Designer) and Winston (Architect) review the UI/service boundary.
**Focused validation:** Admin.UI component/route tests, AppHost resource tests, FrontComposer dependency-mode checks, accessibility/localization evidence, and typed-client boundary scans.

As an EventStore operator,
I want one consolidated Event Store Admin dashboard,
So that operational navigation, evidence states, and legacy deep links remain coherent without creating another UI host.

**Acceptance Criteria:**

**Given** the consolidated UI is implemented
**When** project and AppHost ownership are inspected
**Then** `src/Hexalith.EventStore.Admin.UI` evolves in place and retains resource/container identity `eventstore-admin-ui`
**And** no additional EventStore UI host is created.

**Given** the UI composes its shell
**When** Debug/source and Release/package graphs are validated
**Then** matching `3.2.2` versions of `Hexalith.FrontComposer.Shell` and `Hexalith.FrontComposer.Contracts.UI` plus Fluent UI V5 are used
**And** the stable module identity is `event-store-admin` with label **Event Store Admin**.

**Given** canonical and legacy routes are requested
**When** navigation resolves them
**Then** canonical dashboard deep links render one page implementation and keep the module selected
**And** every other legacy route redirects to a canonical deep link rather than preserving duplicate pages.

**Given** Admin query/command flows and evidence states are exercised
**When** UI tests run
**Then** typed clients remain the only boundary, deferred operations are honest, provenance/lifecycle states follow canonical UX, and output is support-safe, accessible, responsive, and localizable
**And** no unsupported quantitative performance release gate is invented without a measured production baseline.

### Story 7.15: GDPR Aggregate Erasure Backlog

**Requirements covered:** FR35
**Classification:** Planning/backlog artifact; no runtime erasure is authorized.
**Owner / review boundary:** John (Product Manager); Winston (Architect) reviews persistence/legal boundary assumptions.
**Focused validation:** independent artifact-structure review of `_bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md`.

As a product owner,
I want GDPR aggregate erasure and tombstoning tracked independently,
So that legal retention, crypto-shred, replay, backup, and audit questions are not hidden in projection cleanup.

**Acceptance Criteria:**

**Given** GDPR-1 remains deferred
**When** its artifact is reviewed
**Then** it contains scope, non-goals, dependencies, risks, and validation expectations for streams, snapshots, read models, broker history, backups, audit evidence, retention, tombstones, and crypto-shred
**And** generic projection erasure in Story 1.14 does not imply GDPR completion.

### Story 7.16: Admin Interactive OIDC Backlog

**Requirements covered:** FR35
**Classification:** Planning/backlog artifact; no interactive login implementation is authorized.
**Owner / review boundary:** John (Product Manager); Winston (Architect) and Sally (UX Designer) review identity/UX boundaries.
**Focused validation:** independent artifact-structure review of `_bmad-output/planning-artifacts/backlog/iam-1-admin-oidc-login.md`.

As a product owner,
I want Admin interactive OIDC login tracked independently,
So that authorization-code/PKCE, forwarded user identity, claim normalization, and session UX remain separate from immediate auth guards.

**Acceptance Criteria:**

**Given** IAM-1 remains deferred
**When** its artifact is reviewed
**Then** it contains scope, non-goals, dependencies, risks, and validation expectations for PKCE, token forwarding, denied tenants, expired sessions, and audit identity
**And** it does not weaken current production auth or substitute service identity for a user.

### Story 7.17: Aggregate Test-Kit Backlog

**Requirements covered:** FR35
**Classification:** Planning/backlog artifact; no runtime/test-kit package implementation is authorized.
**Owner / review boundary:** John (Product Manager); Murat (Test Architect) reviews testing/package boundaries.
**Focused validation:** independent artifact-structure review of `_bmad-output/planning-artifacts/backlog/kit-1-aggregate-test-kit.md`.

As a product owner,
I want the aggregate test kit tracked independently,
So that fixture ergonomics, replay determinism, idempotency, rejection, and package dependencies receive focused design.

**Acceptance Criteria:**

**Given** KIT-1 remains deferred
**When** its artifact is reviewed
**Then** it contains scope, non-goals, dependencies, risks, and validation expectations for `Given(events).When(command).Then(events)`, replay, metadata, unsupported handlers, and lightweight package consumption
**And** it does not replace domain-specific business tests or pull Server internals into domain test packages.

### Story 7.18: REST Generator Hardening Backlog

**Requirements covered:** FR35
**Classification:** Planning/backlog artifact; completed first-wave fixes remain closed and future runtime changes require new story approval.
**Owner / review boundary:** John (Product Manager); Amelia (Developer) reviews generator feasibility and evidence links.
**Focused validation:** independent artifact-structure review of `_bmad-output/planning-artifacts/backlog/rest-generator-hardening.md` and linked deferred-work evidence.

As a product owner,
I want remaining REST generator hardening tracked independently,
So that diagnostics, incrementality, authorization, request limits, safe errors, and binding edge cases are not lost after proof stories complete.

**Acceptance Criteria:**

**Given** REST generator hardening remains
**When** its backlog artifact is reviewed
**Then** it contains scope, non-goals, dependencies, risks, validation expectations, resolved first-wave items, and named target source/test artifacts for remaining work
**And** unsupported shapes, duplicate JSON names, invalid bindings, route constraints, case-insensitive matching, referenced-contract incrementality, generated auth/error semantics, and request-size/status-location follow-ups remain auditable.

## Epic 8: Shared Payload Protection

Platform security owners and domain modules can use an optional, reusable, production-proven payload-protection engine without duplicating cryptographic formats and key-lifecycle mechanics, while providers/operators retain key custody and domains retain legal policy.

### Story 8.1: Shared Payload-Protection Security Spec And ADR

**Requirements covered:** FR37, NFR19
**Classification:** Security/architecture gate; no runtime implementation is authorized.
**Owner / review boundary:** Winston (Architect) with a named Security Reviewer; the EventStore owner, Release owner, Operations owner, and Parties maintainer approve their boundaries.
**Focused validation:** independent structure/threat-model/test-vector review of `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md` plus approval-evidence validation.

As a platform security owner,
I want the shared payload-protection ownership and durable security contract approved before implementation,
So that the engine cannot make story-local choices that strand persisted history or weaken key custody.

**Acceptance Criteria:**

**Given** the EventStore-owned optional engine is proposed
**When** the security specification is approved
**Then** it fixes package and dependency boundaries, backend ownership, operator custody, Parties-retained policy, and whether an ADR-selected adapter requires a companion package
**And** it does not treat an interface-only or development backend as production.

**Given** `pdenc-v2` will persist durable data
**When** the wire contract is frozen
**Then** the canonical envelope, AES-GCM parameters, nonce/tag representation, algorithm identifiers, byte-stable AAD encoding, canonical property-path encoding, and format/version rules are test-vector ready
**And** AAD binds tenant, domain, aggregate, event or snapshot type, property path, key version, and format, or records the explicitly approved equivalent.

**Given** historical data must remain usable
**When** compatibility is specified
**Then** `json+pdenc-v1`, `json-redacted`, legacy-unprotected, current Story 22.7 metadata, and snapshot reads have explicit routing and typed failure behavior
**And** rollout, mixed history, downgrade, and rollback-after-v2-write policies are defined.

**Given** policy and key lifecycle are shared
**When** contracts are frozen
**Then** `IPersonalDataPolicy`, `IErasureStateProvider`, policy discovery including `PersonalDataAttribute` or an approved equivalent, key paths, state keys, actor/reminder names, metric names, audit fields, and versioning rules are exact
**And** storage, wrapping, rotation, retry, circuit breaking, cache invalidation, and key zeroing responsibilities are assigned.

**Given** a production backend is required
**When** backend selection and restrictions are approved
**Then** at least one non-development adapter, its integration environment, credentials/custody boundary, failure taxonomy, and conformance evidence are named
**And** LocalDev/in-memory startup restrictions outside Development are explicit.

**Given** the security review evaluates misuse and failure
**When** the threat model and test vectors are inspected
**Then** cross-tenant/cross-aggregate substitution, nonce reuse, path ambiguity, metadata tampering, malformed/oversized envelopes, key deletion, provider denial/unavailability, cache staleness, downgrade, and rollback are covered
**And** no-leak boundaries include logs, traces, metrics, exceptions, ProblemDetails, evidence, exports, processing records, certificates, and reports.

**Given** Story 8.1 completes
**When** its output is recorded
**Then** the exact path is `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`
**And** it records approvers, date, accepted scope, rejected alternatives, open decisions, threat model, test vectors, migration posture, and explicit authorization for Story 8.2 to start.

### Story 8.2: Shared Payload-Protection Engine And Parties G5 Parity

**Requirements covered:** FR37, NFR1-NFR4, NFR7, NFR9-NFR12, NFR16-NFR17, NFR19
**Owner / review boundary:** Amelia (Developer); a named EventStore owner and Security Reviewer approve implementation; Murat reviews verification; the Release owner approves artifact provenance; the Parties maintainer approves consumer parity.
**Focused validation:** EventStore contract/engine/server/package suites; ADR-selected production-backend integration; owner golden vectors; Parties local/shared dual-provider compatibility; no-leak scans; package-only consumer validation; post-v2-write rollback rehearsal.

As a platform security owner,
I want an optional shared payload-protection engine built on EventStore's provider-neutral hooks,
So that domain modules can protect persisted payloads without implementing reusable cryptographic and key-lifecycle infrastructure.

**Acceptance Criteria:**

**Given** implementation preflight runs
**When** Story 8.2 starts
**Then** the approved Story 8.1 specification exists at the exact required path and explicitly authorizes implementation
**And** code tasks cite the approved sections they satisfy.

**Given** the optional engine is packaged
**When** release/package validation runs
**Then** `Hexalith.EventStore.PayloadProtection` and any ADR-approved companion adapter are packable, opt-in, centrally versioned, and manifest-governed
**And** the package cannot replace the current no-op default without explicit DI configuration and production-safe option validation.

**Given** Story 8.2 adds one engine package or an ADR-approved engine/adapter package set
**When** release inventory is updated
**Then** `tools/release-packages.json`, `AGENTS.md`, inventory tests, package metadata, SBOM/provenance evidence, and package-only consumer validation agree on the exact set
**And** the current 14-package inventory changes only in the same implementation slice that creates the approved packable project or projects.

**Given** a selected event property or snapshot value is written
**When** `pdenc-v2` protection runs
**Then** AES-GCM authenticated data binds tenant, domain, aggregate, event or snapshot type, canonical property path, key version, and format—or the exact approved equivalent—and matches byte-stable golden vectors
**And** nonce reuse, unbounded input, path ambiguity, and cross-scope ciphertext substitution fail safely.

**Given** existing history is read
**When** the engine encounters `json+pdenc-v1`, `json-redacted`, legacy-unprotected data, current Story 22.7 metadata, protected snapshots, or mixed-version streams
**Then** every supported form remains readable according to the approved policy
**And** unreadable history never silently downgrades to plaintext or unprotected state.

**Given** protection cannot return plaintext
**When** a key is deleted, missing, denied, or unavailable, or metadata/ciphertext is malformed, tampered, opaque, or version-unknown
**Then** those conditions remain separate bounded typed outcomes with explicit retry/permanence semantics
**And** logs, traces, metrics, exceptions, ProblemDetails, evidence, exports, processing records, certificates, and reports pass no-leak scans.

**Given** reusable key lifecycle is enabled
**When** keys are created, stored, wrapped, unwrapped, rotated, cached, invalidated, erased, retried, audited, or denied
**Then** generic behavior is supplied behind shared contracts with bounded retry and circuit-breaker behavior
**And** cache invalidation and zeroing of plaintext key buffers are verified on success, failure, cancellation, rotation, and erasure paths.

**Given** production proof is requested
**When** backend conformance runs
**Then** at least one Story 8.1-selected, pluggable non-development backend is exercised against its real service boundary
**And** LocalDev/in-memory implementations cannot satisfy production proof and fail startup outside their allowed environment.

**Given** policy and erasure behavior vary by domain
**When** `IPersonalDataPolicy`, `IErasureStateProvider`, and approved discovery metadata are used
**Then** the engine remains domain-neutral while Parties can retain its legal policy, erasure orchestration, certificates/reports, and UX semantics
**And** no Parties-specific rule enters EventStore.

**Given** compatibility validation runs
**When** EventStore owner goldens and Parties dual-provider tests execute
**Then** protected/redacted/legacy reads, typed unreadable outcomes, key zeroing, no-leak diagnostics, Art.20 exports, Art.30 processing records, erasure reports/certificates, and persisted state pass through both retained Parties-local and shared-provider paths
**And** HTTP-only, mock-only, or interface-shape evidence cannot close G5.

**Given** rollback is rehearsed
**When** the shared engine has written `pdenc-v2` data and the approved rollback procedure is executed
**Then** retained software and configuration can read or safely route that history according to the ADR without data or metadata loss
**And** switching DI before any v2 write is explicitly insufficient evidence.

**Given** completion is requested
**When** the G5 proof packet is reviewed
**Then** it records exact EventStore source SHA, package IDs/versions/hashes, production-backend identity/version, test commands/results, persisted evidence, named reviewer approvals, limitations, historical-data policy, and rollback instructions
**And** the packet decision is `available`; otherwise Story 8.2 and Epic 8 remain non-`done` and Parties G5 remains `needs-additive-api`.

**Produces:** `_bmad-output/implementation-artifacts/8-2-shared-payload-protection-engine-and-parties-g5-parity-proof-packet.md`.
