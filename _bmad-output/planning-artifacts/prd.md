---
title: eventstore Phase 4 Implementation Readiness Recovery PRD
status: final
document_status: final
implementation_readiness_status: blocked
implementation_readiness_result: reject
implementation_readiness_assessed: 2026-09-10
implementation_readiness_baseline: 293c69c42d35dee26682d42f05c943c0b65786f4
implementation_readiness_baseline_status: examined-dirty-unapproved
implementation_readiness_report: _bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/validation-report.md
created: 2026-07-05
updated: 2026-09-10
project: eventstore
source_artifacts:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02-global-event-ordering.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02-rest-api-external-host.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-04.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-correct-course-story-rewrites.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-domain-contracts-library-guidance.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-generated-api-error-semantics-tests.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-generated-api-smoke-preflight.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-query-metadata-propagation.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-query-metadata-sequencing.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-readiness-quality.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-rest-generator-hardening.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-tenants-package-mode-gateway.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-followup-review-disposition-2-2-2-3.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-generated-api-command-status-location-policy.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-generated-api-smoke-preflight-rehome.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-health-endpoint-anonymous-access-contract.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-outbound-dapr-routing-header-policy.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-rest-generator-hardening.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-route-provenance-contract.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-signalr-hub-leave-validation.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-story-1-7-followup-review-disposition.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-09-implementation-readiness-corrections.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-09.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-10.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-13-outbound-dapr-routing-header-policy-closure.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-13.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-story-1-16-review-and-story-1-20-proof-closure.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-17.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18-story-3-1-live-sidecar-topology.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18-story-3-5-reconciliation.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19-openbao-secret-store.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-20-oq8-durable-idempotency-admission.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-20-readiness-maj-1-maj-2.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-20.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-21.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-27-story-2-12-runtime-identity-rescope.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-01-story-3-11-operator-approval.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-01.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-12-story-3-13-step-3-gate.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-12.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-14.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-16.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-20-commit-message-line-limits.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-20.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-29.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-09-07.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-09-08-nfr3-nfr4-authentication-ratification.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-09-08.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-08-01-post-correction.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-08-01.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/validation-report.md
  - _bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/review-rubric.md
  - _bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/review-platform-contract.md
  - _bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/reconcile-validation-2026-09-10.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-09-09-architecture-condensation.md
---

# PRD: eventstore Phase 4 Implementation Readiness Recovery

## 0. Document Purpose

This PRD is the authoritative Phase 4 functional and non-functional requirements baseline for Hexalith.EventStore. It exists to close the implementation-readiness blocker reported on 2026-07-05: the original epic plan contained FR1-FR35 and NFR1-NFR18, but no standalone PRD existed for PRD-to-epic traceability. The approved 2026-07-11 Parties projection/query parity correction adds FR36. The approved 2026-07-16 payload-protection ownership correction adds FR37 and NFR19 as a committed post-MVP capability. This capability does not enlarge the Phase 4 MVP.

This document owns product requirement intent, MVP scope, non-goals, success metrics, and FR/NFR traceability. `_bmad-output/planning-artifacts/epics.md` owns implementation slicing and sequencing. The required architecture and UX planning artifacts remain separate handoffs and must not be replaced by this PRD.

Document finality and implementation readiness are separate states. This document can be finalized as the truthful requirements baseline while implementation readiness remains blocked. The 2026-09-10 review examined `HEAD` `293c69c42d35dee26682d42f05c943c0b65786f4` plus a dirty worktree; that is an evidence observation, not a clean, atomic, or approved baseline. The bound validation report returned `Poor` / `Reject`, and the executable gate contract in §11.4 currently computes `Reject`. This PRD therefore authorizes no `READY` verdict, Phase 4 MVP completion, release, deployment, consumer migration, or dependent implementation handoff until every mandatory gate passes against one approved source identity and readiness is re-run.

## 1. Planning Baseline

The Phase 4 baseline is derived from the approved sprint-change proposals already consolidated in `epics.md` plus the approved readiness-recovery proposal dated 2026-07-05.

The baseline correction does not reduce MVP scope. It separates planning responsibilities:

- `prd.md` owns FR/NFR truth and readiness traceability.
- `architecture.md` must own component, integration, topology, and decision-record gates.
- `ux.md` must own UI governance, user-flow evidence, and support-safe interaction rules.
- `epics.md` owns story slicing, sequencing, acceptance criteria, and implementation handoff.

The 2026-08-01 implementation-readiness re-run returned `READY` in `implementation-readiness-report-2026-08-01-post-correction.md`; that verdict is historical and superseded. Readiness re-opens whenever FR or NFR text changes, an epic retrospective is rejected, or a proposal alters scope. All three later occurred. The current verdict is stated in §0, and its blocking prerequisites are tracked in §12.

The frontmatter `source_artifacts` list records provenance, not equal authority or automatic approval. The 2026-09-08 tracking/ownership proposal documents an applied change, but its header does not state whether the proposal was approved. The two 2026-08-01 readiness reports are historical, with the post-correction report superseding the original for that date; neither overrides the bound 2026-09-10 `Reject` result. The baseline register in §11.3 and exit contract in §11.4 bind approval state, content identity, affected clauses, validation, and supersession; list membership and matching hashes alone never imply approval.

### 1.1 OQ8 Authority Order

For the Story 4.8 evidence ledger and active Stories 4.9-4.15, the approved 2026-07-20 OQ8 sprint change proposal and the Architecture + Security + Test-approved OQ8 design version 1.0.0 govern. This reconciled PRD, architecture, epics, and canonical SPEC package project that authority into EventStore.

**Governing design identity.** The design is owned by the external Hexalith.Folders repository (`github.com/Hexalith/Hexalith.Folders`), at path `docs/exit-criteria/oq8-idempotency-design.md`, commit `a9cfea91c8a987ef7a836c216e633a92321fc3c2` (2026-08-04), SHA-256 `1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8`. Hexalith.Folders is not an EventStore submodule and the design bytes are not tracked in this repository; EventStore binds the design by digest only, and Folders must supply and verify those bytes. The path, commit, and digest were confirmed against a Folders checkout on 2026-09-08 and are now recorded in this PRD and `architecture.md`, but not yet as a complete identity in `epics.md`, the OQ8 evidence packet, the validator, and CI. Nothing inside this repository can reproduce the governing bytes. The 2026-07-20 proposal that granted this authority cited the design while it was still untracked in a Folders working tree; the 2026-08-04 commit named above is the binding identity and supersedes that working-tree citation. Until EventStore retains a permitted immutable copy, a complete approved normative projection, or a signed or content-addressed Folders attestation, and propagates the full identity through every governed artifact and validator, Gate G-OQ8 in §11.4 fails. No OQ8 closure claim under FR27, NFR7, or NFR16 is authoritative.

**Scope of supersession.** The design governs where it is strictly more specific than this PRD, namely: tenant + key-digest partitioning and the prohibition on persisting or logging raw idempotency keys; the trusted canonical-intent descriptor field set and its exclusions; the admission state machine and its same-request versus different-request outcomes; the ordering invariant that admission precedes aggregate, provider, repository, audit, and projection work; the exact retention and compaction timers; the tombstone field allowlist; the public expired-key contract; the fail-closed treatment of unavailable, malformed, unknown-schema, and unsafe-legacy records; and the production-path evidence denominators. Pre-change FR27, NFR7, and NFR16 wording is superseded only on those points. Every other clause of FR27, NFR7, and NFR16 remains in force, and neither the design nor this section may weaken the 4.9-4.15 sequence or its Story 4.15 closure gate.

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
- Prove high-risk behavior with targeted tests that assert persisted evidence from Redis, the state store, read models, and CloudEvents, not only HTTP status or mock calls.

### 3.3 User Journeys

The journeys below restate existing requirements as cross-role, failure-aware flows. They do not add delivery scope or replace the detailed, currently draft `ux.md` artifact.

- **UJ1 - Alex adopts the domain-service SDK.** Alex, a domain author, references the package-safe EventStore SDK, registers and maps the canonical host seams, and keeps the module limited to domain behavior. Guardrail and adoption evidence must expose remaining boilerplate rather than relabel it as domain logic; any residual router, state-store, projection, telemetry, health, or Aspire plumbing leaves FR1-FR10 and SM8 open.
- **UJ2 - Morgan exposes a generated external API.** Morgan, an external API host developer, supplies command/query contracts and generates controllers in a dedicated host. Before routing or state access, the host resolves exactly one authorized canonical tenant; invalid tenant input fails closed. Accepted commands use a valid `MessageId` as the sole public status identity; missing or invalid identity omits `Location` or rejects safely, never substitutes `CorrelationId`. Invalid metadata, ambiguous routing, or unsafe errors remain support-safe and non-authorizing.
- **UJ3 - Priya observes projection-confirmed success.** Priya, a Tenants UI maintainer, submits a command through the client library and first sees acceptance or evidence-pending state. Only projection-backed query metadata can move the UI to projection-confirmed success; stale, rebuilding, degraded, unavailable, local-only, handler-computed, or unknown-provenance results remain distinct and never become inferred success.
- **UJ4 - Nora investigates and recovers a failed operation.** Nora, an operator, follows tenant-scoped status, persisted event, admission, projection, checkpoint, and dead-letter evidence without exposing sensitive payloads or blindly retrying. A missing, conflicting, expired, corrupt, unavailable, or partially published state follows the bounded recovery contract; absent proof leaves the operation unresolved rather than successful.
- **UJ5 - Riley authorizes a release and consumer removal.** Riley, a release owner, evaluates one immutable release subject and its independent receipts. A consumer owner separately evaluates an exact consumer/removal subject. Missing, mixed-lineage, stale, unauthenticated, expired, or mismatched evidence authorizes neither release/deployment nor consumer infrastructure removal, even when source/package capability exists.

### 3.4 Product Shape

This is a brownfield developer-platform and operations-hardening PRD. Its primary form factors are .NET libraries, DAPR/Aspire hosted services, generated REST API hosts, CI/CD workflows, and supporting Admin/Sample/Tenants Blazor surfaces. Detailed UX design belongs in `ux.md`; this PRD captures UI-facing requirements and governance boundaries only.

## 4. Glossary

- **Admin UI** - EventStore administrative Blazor surface. It must never present unavailable backup, restore, import, compaction, or deferred operations as functional.
- **Aggregate Identity** - The contract identity made from tenant, domain, and aggregate ID. EventStore envelope identifiers use ULID semantics where applicable.
- **Architecture Artifact** - `_bmad-output/planning-artifacts/architecture.md`, the Phase 4 decision and invariant document required before implementation readiness can return to READY.
- **Break-glass** - A narrowly scoped, explicitly configured option that admits an otherwise rejected posture in a bounded set of environments. The only break-glass in this PRD is `AllowInsecureSymmetricKey` (NFR3), which never reaches Production.
- **Canonical-Intent Descriptor** - The trusted, versioned description of what a command intends, from which the idempotency digest is computed. Its exact field set and exclusions are fixed by the OQ8 design (§1.1).
- **Corrective Release** - A release produced to replace a rejected candidate. Story 3.14 owns the corrective release that Story 3.15 verifies; a corrective release requires a separate durable release-owner authority record.
- **DAPR Boundary** - The state, pub/sub, service invocation, actor, config, access-control, and resiliency infrastructure abstraction boundary.
- **Domain Module** - A domain-centric EventStore-backed module containing aggregates, commands, events, projections, query handlers, validators, and contracts, but not reusable platform boilerplate.
- **Domain-Service SDK** - EventStore SDK surface that supplies canonical host composition, DAPR endpoints, discovery, telemetry, health checks, projection dispatch, query routing, event consumers, read-model store, and cursor codec.
- **Durable Admission** - The tenant-scoped, persisted gate that accepts or rejects a command before any aggregate, provider, repository, audit, or projection work occurs. Governed by the OQ8 design (§1.1) and implemented by Stories 4.9-4.13.
- **External API Host** - A dedicated host for generated REST controllers. It is separate from interactive UI hosts.
- **Folders** - Hexalith.Folders (`github.com/Hexalith/Hexalith.Folders`), an external Hexalith repository that is not an EventStore submodule. It owns the governing OQ8 design document bound by digest in §1.1.
- **G5** - The Parties-side payload-protection availability gate. It stays `needs-additive-api` until Story 8.11 supplies an owner/security-approved `available` packet (§11.3).
- **Goldens** - Owner-owned byte-stable test vectors that pin a cryptographic or serialization format so a later change cannot silently alter output. Required for `pdenc-v2` under FR37.
- **Interactive UI Host** - A Blazor or similar user-facing host. It consumes EventStore client libraries and must not host generated or hand-written per-message MVC command/query controllers.
- **OQ8** - The durable idempotency admission exit criterion governing Stories 4.9-4.15. Its authority order, governing document identity, and scope of supersession are stated in §1.1.
- **Owner Roles** - This PRD distinguishes five: the **EventStore owner**, who approves parity and EventStore evidence packets; the **release owner**, who authorizes external publication; the **consumer owner**, who authorizes removal of infrastructure from one bound consumer repository and commit; the **security approver**, who approves the payload-protection specification and G5; and the **product owner**, who owns scope and the forward backlog. One person may hold more than one role, so an approval by identity alone is not evidence of independent review, and an approval issued by the author of the work it approves satisfies the record but not the control. Today only Story 8.11 carries a non-authorship control; generalizing it to every place this PRD says "owner-approved" is an owed refinement (OR10), not yet a binding requirement, and this glossary entry does not create one.
- **Parity** - Used in several senses, each with its own proof standard. **Source/package parity** - the EventStore libraries expose every capability a consumer needs, proven by Story 1.20's packet. **Deployed-runtime parity** - a released, deployed runtime demonstrably behaves as the packet claims, proven only by Story 3.15's three receipts. **Query parity** - a generic query path returns what a hand-written one did. **Topology parity** - AppHost, tests, and deploy templates assert the same posture. **Dual-provider parity** - two payload-protection providers interoperate byte-for-byte. **Tenant-filter parity** - admin queries filter by tenant identically across surfaces. Never treat evidence for one kind as evidence for another.
- **Parity Packet** - The owner-reviewed evidence bundle that records each required capability as `available` or not, cites persisted production-path evidence, and names an exact runtime SHA. A packet fails closed: a missing receipt grants nothing.
- **Parties** - Hexalith.Parties, an external Hexalith domain module that consumes EventStore. It has its own story numbering: references to Parties Story 8.6 and Parties Story 8.7 are Parties stories, not EventStore Epic 8 stories: EventStore's own Story 8.6 is Azure Key Vault Production Adapter Conformance, and its own Story 8.7 is Server Persistence And Snapshot Integration.
- **`pdenc-v2`** - The approved versioned payload-protection encoding format, with a byte-stable authenticated-data contract, introduced by FR37 alongside preserved `json+pdenc-v1`, `json-redacted`, and legacy-unprotected read compatibility.
- **Production Path** - Evidence qualifies as production-path when the real OS process, DAPR sidecar, durable state component, actor placement, and host pipeline are exercised. Substitution is permitted only for time, external secrets, and network reachability, and every packet must declare which substitutions it used against this list. Test-owned startup filters, file-backed clocks, and proof-suffixed packages are substitutions and must be declared, never presented as unqualified production-path proof.
- **Projection-Confirmed Success** - UI success state backed by read-model/projection evidence, not only command acceptance or SignalR notification.
- **Provenance Classification** - The query-response declaration of whether evidence is projection-backed, handler-computed, or unknown (FR4). Freshness and version evidence is authoritative only for projection-backed responses (NFR8).
- **Readiness Recovery** - The approved planning correction that creates PRD, architecture, and UX artifacts, splits oversized stories, and maps high-risk NFRs before Phase 4 execution.
- **Retention Tier** - The fixed class that determines how long a durable-admission record and its tombstone are kept. Tier values and timers are fixed by the OQ8 design (§1.1), not chosen per call site.
- **Support-Safe State** - UI, logs, diagnostics, and errors that do not expose tokens, decoded JWT payloads, raw metadata, raw payloads, cursor internals, ETag internals, stack traces, or secrets.
- **UX Artifact** - `_bmad-output/planning-artifacts/ux.md`, the Phase 4 UI governance and user-flow document required for UI-affecting stories.

## 5. Product Concerns

Phase 4 carries these concerns and the PRD must preserve them through downstream planning:

- Security and fail-closed authorization across public, internal, domain-service, projection-notification, admin, and generated REST surfaces.
- Tenant isolation across state keys, actor IDs, topics, admin queries, generated APIs, SignalR groups, and deployment configuration.
- Public API and package contract stability for EventStore libraries, REST generator output, and domain-service seams.
- DAPR/Aspire runtime topology parity across AppHost, tests, production component templates, ACLs, app IDs, topics, and sidecar arguments.
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
| FR4 | The platform must provide a domain query-handler seam using `IDomainQueryHandler`, discovery, dispatch, operational metadata reporting, gateway-side query-type capture, handler-aware routing to domain `/query` endpoints, and end-to-end `QueryResponseMetadata` propagation for freshness, projection version, ETag, served-at, degraded/warning state, and paging evidence, carrying an explicit query-response provenance classification (projection-backed, handler-computed, or unknown) that governs whether that evidence is projection-backed. Projection-backed responses must additionally preserve a lossless lifecycle representation or owner-approved mapping for `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`; consumers must not infer lifecycle from ETags or claim projection-confirmed success without projection-backed provenance. |
| FR5 | The platform must provide generic persisted read-model lifecycle and write contracts with ETag-aware reads/writes, coordinated read-model and sequence/checkpoint erasure, and detail/index batch writes, or an equivalent approved by the architecture owner and recorded in `architecture.md`. Batch behavior must define partial-failure recovery, idempotency, ordering, flush completion, optimistic concurrency, DAPR behavior, and deterministic in-memory testing semantics. |
| FR6 | The platform must provide a reusable DataProtection-backed query cursor codec with scope validation, payload limits, tamper/key-rotation handling, and caller-supplied purpose isolation. |
| FR7 | The platform must provide an asynchronous, cancellation-aware projection-handler seam supporting multiple named projections per domain and coordinated detail/index persistence, plus a generic domain-event subscription/consumer pipeline with deduplication and endpoint mapping. Projection delivery must tolerate duplicate and out-of-order events through the actual handler path, and full rebuilds must remain correct across paging boundaries. |
| FR8 | The platform must provide Aspire, telemetry, and health-check extensions for domain modules, including `AddEventStoreDomainModule`, convention telemetry, and DAPR state-store health checks. |
| FR9 | The Sample domain and Tenants domain must adopt platform SDK seams so duplicated request routers, projection actors, cursor codecs, state-store plumbing, telemetry, health checks, and per-domain Aspire wiring are removed or reduced to domain-specific logic. |
| FR10 | The EventStore package set must include the domain-service and service-default packages as publishable packages, and release packaging must publish only the manifest-governed EventStore package set. |

**Done evidence:** Release build is clean under warnings-as-errors; Sample and Tenants adoption proofs preserve domain semantics; guardrail tests fail a fixture domain module that reintroduces reusable platform boilerplate through the covered channels. Residual, accepted evasions are recorded rather than claimed closed: a handler class not named `*Dapr*`, a non-literal route setter, a route value resolved across files or computed, a hardcoded three-entry host-root list, and non-`.cs` sources such as `.razor` `@code` blocks are not detected (DW-64, DW-65, DW-77, DW-78, DW-79), and the Tenants domain-service host retains transitional `AddDaprClient`, `UseCloudEvents`, controller, MediatR, and router composition. These remain an open residual risk, measured by SM8 and bounded by SM-C4, not delivered coverage.

### 6.2 External Integration Surfaces

**Description:** External developers get generated typed REST APIs in dedicated API hosts while interactive UI hosts stay client-library consumers and projection notifications remain scoped, bounded, and backward compatible.

| ID | Requirement |
| --- | --- |
| FR11 | The platform must provide a REST API source-generator contract seam with `ICommandContract`, `IQueryContract`, optional `RestRouteAttribute`, and assembly-level `RestApiAttribute`. |
| FR12 | The REST API generator must discover command and query contracts and emit typed, OpenAPI-visible controllers that delegate to `IEventStoreGatewayClient` and forward canonical query metadata headers when the gateway supplies them. Every generated controller must enforce the canonical tenant boundary in NFR2 before routing or state access. The generator test suite must cover discovery, routing conventions, diagnostics, generated output, query metadata headers, `304`, safe problem-detail behavior, and compiled-controller tenant negatives. An accepted generated command may emit an absolute, gateway-authoritative command-status `Location` URI only when a valid canonical `MessageId` selects the status resource. `CorrelationId` remains diagnostic and must never select that resource. An absent, blank, malformed, noncanonical, or unavailable `MessageId` must omit `Location` or cause a safe rejection; it must never fall back to `CorrelationId` or emit a relative or dangling external-host URI. |
| FR13 | Generated REST controllers must live in dedicated external-facing API hosts, not interactive UI hosts; interactive UI hosts must consume EventStore client libraries directly. |
| FR14 | The Sample proof must introduce a contracts-only Sample contracts library and an external Sample API host, move shared contracts there, and prove generated query and command controllers through that external API host. |
| FR15 | The Tenants proof must move generated Tenants controllers to an external Tenants API host, while Tenants UI consumes client libraries and no longer hosts hand-written per-message controllers; any Tenants freshness, projection-version, ETag, or paging evidence shown by generated APIs or UI must come from the platform query metadata path. Platform-wide Tenants operations must use a distinct authenticated platform-operation scope and must not invent or route through a synthetic `system` tenant. |
| FR16 | The projection-changed transport must add an additive metadata-rich detail path with optional group scope, bounded metadata per NFR5, scoped SignalR groups, and preserved signal-only compatibility. A DAPR notification path is required only where a subscriber cannot hold a SignalR connection; where no such subscriber exists, the requirement is met by the SignalR path alone. |

**Done evidence:** Generated controllers delegate through the gateway; Sample and Tenants UI hosts contain no generated or hand-written per-message MVC controllers; SignalR and optional DAPR notification paths prove scoped detail delivery and signal-only compatibility. Compiled and runtime controller tests must reject every invalid tenant case in NFR2 before downstream calls, and command tests must prove that only a valid canonical `MessageId` can produce `Location`, including missing, invalid, and `MessageId`/`CorrelationId` divergence cases. Current Stories 2.2, 2.4, 2.5, and 2.9 completion labels do not satisfy these corrective outcomes; Gate G-TENANT and Gate G-STATUS-ID remain failed until approved corrective stories or reopened stories pass.

### 6.3 Release And Repository Reliability

**Description:** Maintainers can release reproducibly with correct dependency mode, aligned `references/` submodule layout, deterministic release gates, dedicated live-sidecar coverage, shared security workflows, and manifest-governed package output.

| ID | Requirement |
| --- | --- |
| FR17 | Live DAPR sidecar tests must be tagged and removed from the per-push release gate, then run in a dedicated integration workflow with sidecar warm-up and readiness retry. |
| FR18 | `DaprETagService` must allow an overridable actor request timeout while preserving the production default. |
| FR19 | Root-declared Git submodules must live under `references/`, and solution, project, documentation, Aspire metadata, and LLM instruction paths must resolve through the `references/` layout. |
| FR20 | The Aspire Keycloak resource must be named `security` while preserving Keycloak as the implementation technology and updating fixtures/resource lookups accordingly. |
| FR21 | Cross-repo Hexalith library dependencies use source project references only when `UseHexalithProjectReferences=true` is explicitly supplied and the root-declared source exists. An unset or explicit `false` value selects package references in every configuration, including Debug; Release and configuration-less evaluation therefore remain package-safe. Every source-owned NuGet dependency version used by a Hexalith repository must be declared in `references/Hexalith.Builds/Props/Directory.Packages.props`; consuming `Directory.Packages.props` files import that catalog and declare no local `PackageVersion`, version override, or fallback version property. |
| FR22 | Commands used to restore, build, test, pack, and run semantic-release must assert package-reference mode and avoid packaging submodule projects. |
| FR25 | EventStore workflows must use shared Hexalith.Builds security gates through `@main`, keep third-party actions SHA-pinned through shared workflows, and define NuGet package publish scope in `tools/release-packages.json`. |

**Done evidence:** Release/package-mode validation cannot publish submodule packages; CI separates release-gate tests from live-sidecar tests; documentation and path scans no longer depend on root-level Hexalith submodule paths.

### 6.4 Event Correctness And Recovery

**Description:** Operators and consumers can trust persisted event metadata, idempotency, replay dispatch, append behavior, crash recovery, and global-position semantics under duplicate, concurrent, and failure conditions.

| ID | Requirement |
| --- | --- |
| FR23 | Persisted events must receive non-zero, actor-allocated global positions; CloudEvent IDs must use the event `MessageId`; duplicate command replies must preserve the original command result fields. |
| FR24 | The global-position allocation strategy must be renegotiated toward sharding per tenant or domain, and the frozen global-ordering spec must be updated before implementation. |
| FR27 | Pipeline and idempotency correctness remediation must use exact command identity for resume; provide an EventStore-owned, tenant-scoped durable admission contract accepting only a trusted, versioned canonical-intent descriptor and fixed retention tier; reject live conflicting intent and return non-retryable `idempotency_key_expired` for any expired-key reuse before aggregate, domain, or external execution; separate replay-result retention from metadata-only consumed-key evidence; and never convert consumed, unavailable, corrupt, or unsafe legacy state into a fresh miss. Command status/archive identity, transient retryability, and tenant-before-state validation remain required. |
| FR29 | Replay and dispatch remediation must make event apply-method resolution boundary-safe and ambiguity-detecting, and must use one shared `JsonSerializerOptions` path for command, rehydrate, project, and pub/sub payload serialization. |
| FR30 | Crash recovery remediation must detect events committed but not published and complete their publication, drain them, or recover them without requiring resubmission with the same correlation ID. |
| FR31 | Append durability remediation must start with a live-sidecar two-writer race test and DAPR conflict-exception spike before choosing an optimistic-concurrency fencing design. |

**Done evidence:** Tests prove CloudEvent ID stability, duplicate result fidelity, stale pipeline rejection, replay ambiguity handling, and stored-but-unpublished recovery. FR31 and FR24 are deliberately pre-implementation requirements and their done evidence is evidence, not a guard: FR31 closes when the live-sidecar two-writer race and DAPR conflict behavior are observed, which Story 4.5 did with the outcome `same-key-overwrite-raw-durable-write-lost`, and FR24 closes when the frozen global-ordering spec has an approved successor, which authorizes planning only. Neither FR delivers an append fence or a sharded allocator; both implementations are out of MVP scope under §9.2.

### 6.5 Security And Tenant Isolation

**Description:** Administrators, tenants, and domain services are protected by fail-closed authentication, scoped authorization, safe configuration, app-layer internal credentials, tenant-aware topology, and removal of trusted wire assertions.

| ID | Requirement |
| --- | --- |
| FR26 | Phase 0 architecture remediation must close immediate safe fixes: clear staged state on infrastructure failure; protect anonymous admin endpoints; enforce tenant-filter and safe-query behavior; reject representative admin JSON write/sandbox bodies over `1_048_576` bytes and `AdminBackupsController.ImportStream` bodies over `10 * 1024 * 1024` bytes before any upstream invocation; strip committed admin secrets; enforce production auth guards; gate admin Swagger; require destructive CLI confirmation; use ULID-safe admin correlation middleware; and correct stale test-baseline documentation. |
| FR28 | Trust-boundary remediation must require app-layer credentials for internal, domain-service, projection-notification, and admin-computation endpoints, and must remove trust in wire-asserted administrator flags. |
| FR32 | Runtime topology remediation must make the AppHost-loaded DAPR pub/sub, ACL, and key-prefix posture match the posture asserted by tests and production deploy templates. |

**Done evidence:** Anonymous and cross-tenant admin access fails closed; production auth rejects insecure modes unless explicitly break-glassed; committed config contains no forgeable admin secrets; runtime topology tests inspect actual sidecar component paths and ACL posture.

### 6.6 Bounded Cost And Event Evolution

**Description:** Platform users can operate long-lived streams with bounded snapshot and projection cost, sequence-safe projection updates, event schema versioning/upcasting, validated event identity metadata, and cancellation-aware public seams.

| ID | Requirement |
| --- | --- |
| FR33 | Cost and evolution remediation must introduce folded snapshots, reduce projection replay cost so that replaying an already-current projection performs zero event reads, add projection sequence guards, support event schema versioning/upcasting, reject an event whose metadata identity components are absent or not ULID-safe rather than accepting it, and add cancellation-token seams to published processing/query/projection interfaces. |

**Done evidence:** Stories 6.1, 6.3, and 6.5 produce approved specs at named paths before their dependent implementation stories start; dependent implementation stories verify the approved specs exist and that code conforms to them.

### 6.7 Operator Trust, Admin Honesty, And Future Capabilities

**Description:** Operators get explicit delivery semantics, bounded poison handling, attributable admin actions, honest unavailable-operation behavior, hardened deployment posture, meaningful higher-tier test evidence, and explicit backlog artifacts for deferred capabilities.

| ID | Requirement |
| --- | --- |
| FR34 | Delivery, admin, and deployment remediation must document at-least-once unordered delivery; add poison/dead-letter handling; bound in-memory deduplication; normalize admin claims; audit every state-mutating admin action; hide deferred admin operations; provide the typed Admin boundary and consolidated FrontComposer/Fluent UI shell with required evidence, governance, and accessibility; add OpenBao-backed DAPR secret-store configuration for production operational and application secrets; require application retrieval through the DAPR Secrets API; restrict Kubernetes Secrets to documented bootstrap credentials only when no approved mounted or projected credential mechanism is available; add readiness/app-health checks; apply the declared DAPR resiliency policies; require immutable image identity; and restore IntegrationTests CI coverage such that the lane runs on every push to `main` and asserts persisted state for at least the FR23, FR27, and FR30 paths. |
| FR35 | Backlog capabilities must be tracked for GDPR aggregate erasure/tombstoning, Admin interactive OIDC login, an aggregate test kit, and REST generator hardening. |

**Done evidence:** Admin unavailable operations are hidden/disabled or return `501`; audit records remain support-safe; integration tests assert persisted state evidence; backlog artifacts exist for GDPR-1, IAM-1, KIT-1, and REST generator hardening.

### 6.8 Consumer Projection/Query Parity Closure

**Description:** Consuming domain modules may remove local projection/query infrastructure only after EventStore implements, proves, and owner-approves every required generic replacement capability against one exact runtime commit.

| ID | Requirement |
| --- | --- |
| FR36 | Before a consuming module deletes local projection/query infrastructure, three independent outcomes must pass: EventStore capability availability, release/deployment authority, and per-consumer removal authorization. EventStore must produce an EventStore-owner-reviewed parity packet proving every required capability through production paths and binding an exact runtime SHA. Release/deployment requires its own authenticated release-owner authority. Per-consumer removal requires an authenticated immutable consumer-owner receipt binding the consumer repository and commit, the applicable-mode matrix, the exact removal-subject digest, the role-registry identity, a `consumer-removal-authorized` outcome, issuance time, validity or expiry, and invalidation on any bound identity change or expiry. The consumer's checked-out EventStore SHA must match the approved runtime SHA. Missing or invalid consumer authority always prohibits removal even when platform capability and release authority pass. |

**Done evidence:** FR36 carries independently failing capability, release/deployment, and consumer-removal states and is closed only when every applicable state is closed for the bound consumer subject.

- **Source/package parity - CLOSED.** Stories 1.14-1.19 are complete and reviewed; Story 1.20 records every projection/query parity item as `available`, cites persisted production-path evidence, records explicit owner approval, and names the exact EventStore runtime SHA that Parties verifies before Parties Story 8.6 resumes.
- **Frozen-evidence integrity - CLOSED.** Story 1.21 repaired three `environment.txt` files that drifted from Story 1.20's frozen `critical-evidence-sha256.txt` manifests. Story 1.20's evidence is immutable by intent, and 1.21 is the record of the one authorized repair; the manifests are not self-verifying without it.
- **Deployed-runtime parity - OPEN.** Story 3.13 records the immutable v3.94.1 candidate as rejected and non-authorizing. Story 3.14 produces a separately authorized corrective release, and Story 3.15 owns positive closure. Story 3.15 is `in-progress` and its packet fails closed at 0 of 3 receipts, where the verifier exits 1 and grants nothing. The 2026-09-07 Epic 3 retrospective is `rejected` and records that FR36 is not closed.
- **Per-consumer removal authority - OPEN.** No authenticated Consumer-owner receipt currently binds a consumer repository/commit, exact removal subject, role-registry identity, validity, and `consumer-removal-authorized` outcome. Platform parity or release evidence cannot substitute for this receipt.

Story 1.20 does not cover deployed-runtime parity, payload-protection G5, or Parties Story 8.7.

### 6.9 Optional Shared Payload Protection

**Description:** Domain modules may opt into a reusable EventStore-owned payload-protection engine without duplicating durable cryptographic formats and key-lifecycle infrastructure, while providers/operators retain production key custody and domains retain legal policy.

| ID | Requirement |
| --- | --- |
| FR37 | EventStore must provide an optional shared payload-protection engine package built on `IEventPayloadProtectionService` and the existing provider-neutral metadata, outcome, workflow, and redaction contracts. The engine must implement the approved `pdenc-v2` format and byte-stable authenticated-data contract, preserve `json+pdenc-v1`, `json-redacted`, legacy-unprotected, and snapshot read compatibility, expose `IPersonalDataPolicy` and `IErasureStateProvider` extension seams, supply reusable key-lifecycle and resilience mechanics behind shared contracts, include at least one integration-proven production backend, and produce EventStore-owner plus Parties dual-provider parity and rollback evidence before G5 is available. |

**Done evidence:** The approved security ADR exists; package/API inventory and production-backend integration are verified; EventStore owner goldens and Parties dual-provider compatibility pass; rollback succeeds after `pdenc-v2` writes; and the G5 packet records exact source, package, backend, review, limitation, historical-data, and rollback identity.

## 7. Cross-Cutting Non-Functional Requirements

| ID | Requirement |
| --- | --- |
| NFR1 | Security must fail closed for public, internal, domain-service, projection-notification, and admin surfaces; no endpoint may rely only on network posture or caller-supplied admin flags. The only anonymous exception is the health/liveness/readiness probe endpoints (`/health`, `/alive`, `/ready`), which are explicitly pinned `AllowAnonymous` and support-safe (AD-16); the fail-closed default is never weakened to reach probes. |
| NFR2 | Tenant isolation must be preserved across state keys, actor IDs, topics, admin queries, generated REST APIs, SignalR groups, and deployment configuration. Every tenant-scoped request must resolve exactly one explicit authorized tenant, canonicalized to lowercase with the shared tenant canonicalizer and valid under the 1-64-character `AggregateIdentity` tenant grammar. Missing, duplicate, conflicting, malformed, unauthorized, uppercase or otherwise noncanonical, and reserved `system` tenant values must be rejected before routing, state access, status lookup, or resource-existence disclosure. Platform-wide operations use a distinct authenticated platform-operation scope; they must not synthesize a tenant. |
| NFR3 | Authentication must fail closed on signing-key posture in every externally reachable or JWT-binding host, including the EventStore gateway, Admin Server Host, Sample API, Tenants API, generated-host fixtures, and every future host that binds the platform JWT capability. Each host must consume the same shared, versioned JWT contract and expose evidence of its contract fingerprint; a hand-written subset is nonconforming. Production must always reject symmetric-key mode, including when `AllowInsecureSymmetricKey` is enabled; the break-glass option admits symmetric mode only in environments that are neither Development nor Production. Authority/OIDC discovery must require HTTPS metadata outside Development. `AllowedAlgorithms` must be a nonempty, explicitly configured allowlist with no implicit default: asymmetric mode accepts only the approved RS256/RS384/RS512, PS256/PS384/PS512, and ES256/ES384/ES512 set, and Development or break-glass symmetric mode accepts only HS256. Issuer, audience, signature, and lifetime validation remain mandatory in every mode, with clock skew fixed at 60 seconds. Role and tenant validation also remain mandatory in every mode and are owned by NFR1 and NFR2. Story 5.3 evidence alone covers only its bound hosts and cannot establish Tenants, generated-host, or future-host conformance. |
| NFR4 | No committed configuration, including clearly named Development configuration, may contain a forgeable administrator signing key, username, password, credential, bearer token, decoded JWT payload, or other operational secret. Development and test credentials are injected only through this closed list of channels - .NET user-secrets, environment variables, runtime-generated test fixtures, and the Aspire AppHost parameter/secret mechanism - and cannot be loaded as a non-Development fallback. Adding a channel to this list requires a proposal. |
| NFR5 | SignalR detail metadata must remain bounded and metadata-only: at most 16 entries and 2048 total UTF-8 bytes, as configured by `ProjectionChangeNotifierOptions.DefaultMaxDetailMetadataEntries` and `DefaultMaxDetailMetadataBytes`. The metadata dictionary is deliberately opaque and carries no allow-listed key set, so boundedness is enforced by entry count and byte size only. Framework logs must not expose metadata values above Debug level. |
| NFR6 | Event delivery semantics are at-least-once and unordered; subscribers must deduplicate by `MessageId` and order events only where domain semantics make `SequenceNumber` meaningful. Safety against duplicate and out-of-order delivery must be enforced and proven through the production projection dispatcher, handler, persistence, marker, and checkpoint path rather than only aggregate replay or transport-level tests. |
| NFR7 | Event persistence and command processing must prevent silent data loss across five classes: (a) staged-state flush loss; (b) stale pipeline records; (c) append races; (d) committed-but-unpublished events; and (e) duplicate side effects across reservation, admission fencing, execution, recovery, expiry, compaction, restart, and concurrent hosts. A loss class is delivered only when an implemented guard or recovery prevents the loss within the supported operating envelope and production-path evidence proves that the guard or recovery prevents the loss. An out-of-scope declaration, deferral record, or test that merely observes the loss never satisfies NFR7 or SM11. The Story 4.11 current fence is an internal admission capability and must never be presented as class (c) provider-level append fencing or write-once storage. |
| NFR8 | Snapshot and projection behavior must have a bounded cost model as streams grow. The snapshot specification at `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` has status `approved-authorized`, binds normative SHA-256 `0b456b5fcc49c6f7431e3476cefd073c184cf181d64bf84fb76414c1110d16b2`, sets `MaxSnapshotEnvelopeOverheadBytes` to 4096, and authorizes Story 6.2. This approval does not deliver the runtime outcome or resolve Story 6.1 lifecycle drift. The projection bound remains gated by the missing approved specification at `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md`; that specification must bind a numeric budget, workload and page-size model, exact pass condition, evidence identity, named approval, and Story 6.4 authorization. No numeric value is inferred by this PRD, and Story 6.4 may not start while the specification is absent or unapproved. Snapshot and projection behavior must also avoid unnecessary full-stream replay when projections are already current, and must expose projection freshness/version evidence through platform query metadata when callers depend on lifecycle decisions; freshness/version evidence is authoritative only for query responses whose route provenance is projection-backed, and handler-computed or unknown-provenance responses must not be presented as authoritative lifecycle evidence. Paged rebuild output must equal canonical aggregate replay and must never overwrite a complete live model with page-only state. |
| NFR9 | Release behavior must be reproducible and independent of local submodule checkout state; Release builds must use package references for external Hexalith libraries unless intentionally overridden. |
| NFR10 | CI/CD must separate deterministic release-gate tests from live-sidecar/integration tests while preserving live-sidecar coverage in a dedicated lane. |
| NFR11 | Package publishing must be manifest-driven and must not publish submodule packages or packages outside the EventStore release inventory. |
| NFR12 | Backward compatibility is inventory-backed across all released public surfaces: .NET source and binary APIs and abstractions; generator attributes, diagnostics, generated source, OpenAPI, REST routes, status and metadata headers, and Problem Details codes; gateway and domain-service endpoints; event, command, query, and result envelopes and serialization formats; DAPR methods, topics, and CloudEvent wire contracts; and NuGet package identities, assets, dependencies, and target-framework compatibility. Additive changes require compatible baseline evidence; deprecations require a documented replacement and migration path; removals or incompatible source, binary, route, wire, serialization, or package changes require an approved breaking-change proposal and SemVer-major release. Release must fail unless API and wire baselines plus representative package-only consumers pass against the manifest-governed inventory. The SignalR signal-only contract, existing generic gateway APIs, and FR37 read formats remain protected members of this larger set rather than its boundary. |
| NFR13 | Generated code and source-generator packages must build cleanly under warnings-as-errors and must follow EventStore code style, nullable, ULID, and `ConfigureAwait(false)` rules. |
| NFR14 | Interactive UI hosts must not expose generated or hand-written per-message MVC command/query controllers; UI command/query flows consume client libraries. |
| NFR15 | Admin UX must not present deferred backup, restore, import, compaction, or other unavailable operations as functional; unavailable operations must be hidden/disabled or return `501`. |
| NFR16 | Integration and higher-tier tests must assert persisted state-store/read-model/end-state evidence, not only HTTP status codes or mock call counts. Erasure, batch recovery, handler idempotency, and rebuild equivalence require persisted detail, index, marker, lifecycle, and checkpoint evidence through their production paths. Durable-admission evidence must inspect production-path state and prove restart survival, multi-host serialization, inclusive expiry boundaries, atomic tombstone compaction, leakage constraints, and zero downstream execution for replay, conflict, expired, corrupt, and unsafe legacy outcomes. |
| NFR17 | Operational hardening must use the canonical DAPR `openbao` component for production operational and application secrets. Dependent DAPR components must use `secretKeyRef` with `auth.secretStore: openbao`; application code must use the DAPR Secrets API; and per-application access must be default-deny. OpenBao bootstrap credentials are platform inputs and may use Kubernetes Secrets only when no approved mounted or projected mechanism is available. Operational hardening must also support DAPR app-health checks and readiness-tagged health checks; the resiliency policy set defined in `deploy/dapr/resiliency.yaml` - retries `defaultRetry`, `pubsubRetryOutbound`, `pubsubRetryInbound`; timeouts `daprSidecar`, `pubsubTimeout`, `subscriberTimeout`; circuit breakers `defaultBreaker`, `pubsubBreaker` - applied to the `eventstore` app and the `pubsub` and `statestore` components; immutable image tags; and documented crypto-shred boundaries. |
| NFR18 | AOT/trimming is explicitly not a target while reflection conventions remain load-bearing. That constraint must be documented at `docs/reference/aot-and-trimming-posture.md`; the document does not yet exist and no story currently owns it (see §12). |
| NFR19 | Payload protection must fail closed and preserve byte-stable, versioned cryptographic semantics. Deleted, missing, denied, unavailable, malformed, tampered, and opaque states must remain bounded typed outcomes. Key material must be zeroed before the buffer holding it leaves the scope that decrypted or derived it, proven by a test that inspects the buffer after disposal; caches must be invalidated on lifecycle changes; development-only backends must not start as production proof; and rollout, historical reads, downgrade, and rollback after writing the newest format must be integration-tested. |

### 7.1 Stable Clause Acceptance And Closure

The top-level FR/NFR IDs remain stable. The clause IDs below expose existing independently testable consequences without renumbering or expanding scope. A story status closes only the clauses mapped to that story, and only when each clause has a named, content-bound passing evidence artifact. A parent requirement closes only when every listed clause passes. A `done` label, ownership declaration, smoke status, or evidence for another clause never substitutes. Until this ledger is reconciled into `epics.md` and bound evidence, Gate G-CLAUSE in §11.4 fails.

| Clause | Primary slice(s) | Required observable consequence |
| --- | --- | --- |
| FR4-C1 | 1.2 | Query-handler registration, discovery, and dispatch work through the published seam. |
| FR4-C2 | 1.2, 1.16 | The gateway captures query type and selects the handler-aware domain `/query` route. |
| FR4-C3 | 2.7 | Freshness, version, ETag, served-at, warning/degraded, and paging metadata propagate losslessly. |
| FR4-C4 | 2.11 | Every response declares projection-backed, handler-computed, or unknown provenance. |
| FR4-C5 | 1.16, 2.7, 2.11 | Projection-backed responses preserve all six lifecycle states: `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`. |
| FR4-C6 | 2.11 | Consumers cannot infer lifecycle from ETags or claim projection-confirmed success without projection-backed provenance. |
| FR5-C1 | 1.3 | Persisted lifecycle reads/writes honor the published ETag contract. |
| FR5-C2 | 1.14 | Read-model detail, index, sequence, and checkpoint erasure is coordinated and verifiable. |
| FR5-C3 | 1.15 | Detail/index batch writes define and prove partial-failure recovery, idempotency, ordering, flush completion, optimistic concurrency, DAPR behavior, and deterministic in-memory behavior. |
| FR7-C1 | 1.17 | Projection handlers are asynchronous, cancellation-aware, and support multiple named projections. |
| FR7-C2 | 1.6 | The generic domain-event subscription and consumer seam is published and mapped. |
| FR7-C3 | 1.15 | Projection detail/index persistence is coordinated. |
| FR7-C4 | 1.18 | Duplicate and out-of-order events are safe through the production dispatcher, handler, persistence, marker, and checkpoint path. |
| FR7-C5 | 1.19 | Paged rebuild output equals canonical replay and cannot overwrite a complete live model with page-only state. |
| FR12-C1 | 2.2 | Discovery, emission, delegation, headers, diagnostics, `304`, and safe Problem Details pass generated and compiled tests. |
| FR12-C2 | corrective successor or reopened 2.2/2.4/2.5 | Compiled controllers enforce every NFR2 tenant rejection before downstream work; platform-wide operations do not synthesize `system`. |
| FR12-C3 | corrective successor or reopened 2.9 | Only a valid canonical `MessageId` selects an absolute gateway-authoritative status `Location`; missing or invalid identity omits it or rejects safely. |
| FR12-C4 | corrective successor or reopened 2.9 | A `CorrelationId` never selects a status resource, including when it differs from or is present without `MessageId`. |
| FR26-C1 | 5.1 | Infrastructure failure clears staged state and cannot leak it into later work. |
| FR26-C2 | 5.2 | Admin authorization, tenant filtering, safe queries, and the `1_048_576`/`10 * 1024 * 1024` byte limits fail before upstream invocation. |
| FR26-C3 | 5.3 | Production authentication fails closed and committed forgeable administrator secrets are absent. |
| FR26-C4 | 5.4 | Swagger gating, destructive CLI confirmation, ULID-safe correlation, and test-baseline guidance are verified. |
| FR33-C1 | 6.1 | The folded-snapshot specification is approved and content-bound before runtime work. |
| FR33-C2 | 6.2 | Folded snapshot runtime behavior conforms to the approved specification. |
| FR33-C3 | 6.3 | The projection-cost and sequence-guard specification binds quantitative budgets and authorization. |
| FR33-C4 | 6.4 | Current projections perform zero event reads, while tail/sequence handling remains safe under the approved bound. |
| FR33-C5 | 6.5 | Event evolution, identity validation, and cancellation seams are specified and approved. |
| FR33-C6 | 6.6 | Upcasting, metadata rejection, and published cancellation behavior conform to the approved specification. |
| FR34-C1 | 7.1 | At-least-once/unordered delivery, poison/dead-letter handling, and bounded deduplication are implemented and proven. |
| FR34-C2 | 7.2 | Admin claims are normalized without trusting caller-supplied flags. |
| FR34-C3 | 7.3 | Every state-mutating admin action emits a support-safe attributable audit record. |
| FR34-C4 | 7.4 | Deferred operations are hidden or disabled, and any remaining endpoint returns `501`. |
| FR34-C5 | 7.5 | The typed Admin boundary is the single supported client seam. |
| FR34-C6 | 7.6 | OpenBao, `secretKeyRef`, DAPR Secrets API, default-deny access, and bounded bootstrap credentials conform. |
| FR34-C7 | 7.7 | Readiness and DAPR app-health checks prove the declared runtime posture. |
| FR34-C8 | 7.8 | The named DAPR retry, timeout, and circuit-breaker policies apply to the declared apps and components. |
| FR34-C9 | 7.9 | Runtime images use an immutable, content-bound identity. |
| FR34-C10 | 7.10, 7.11 | IntegrationTests run on every push to `main` and prove persisted FR23, FR27, and FR30 outcomes. |
| FR34-C11 | 7.14, 7.19, 7.20 | The consolidated Admin shell, evidence UX, governance, localization, and accessibility outcomes pass. |
| NFR17-C1 | 7.6 | OpenBao/component references, DAPR Secrets API, default-deny access, and bootstrap boundaries pass. |
| NFR17-C2 | 7.7 | DAPR app-health and readiness-tagged checks pass. |
| NFR17-C3 | 7.8 | The complete named resiliency-policy set is applied and verified. |
| NFR17-C4 | 7.9 | Immutable image identity is proven. |
| NFR17-C5 | unassigned | Crypto-shred boundaries have a uniquely named owner and content-bound evidence; supporting Epic 8 work cannot substitute for missing MVP ownership. |

## 8. Constraints And Guardrails

### 8.1 Repository And Build

- Use `Hexalith.EventStore.slnx` only for restore and build.
- Run unit tests by project; do not make solution-level `dotnet test` the EventStore default.
- Keep every source-owned NuGet dependency version in `references/Hexalith.Builds/Props/Directory.Packages.props`; consuming `Directory.Packages.props` files only configure CPM and import the shared catalog.
- Keep the shared Builds catalog on the latest validated compatible versions from configured package sources. Prefer the latest stable release for stable pins; validate intentional prerelease channels, aligned families, framework/SDK coupling, and major upgrades as units. Document every retained exception with its reason, evidence, and removal trigger, and never downgrade because search omits or unlists a package.
- Require explicit `UseHexalithProjectReferences=true` for source intent; unset or explicit `false` remains package intent in Debug, Release, and configuration-less evaluation.
- Use .NET SDK container support, not Dockerfiles.
- Publish the EventStore container as one immutable OCI image index containing exactly the supported platform set, `linux/amd64` and `linux/arm64`; release validation must fail closed on any other manifest shape or platform set. No EventStore build file declares this platform set: it is enforced by the pinned Hexalith.Builds `publish-containers` gate reached through `release.yml`, with EventStore-side evidence produced by `tools/release_evidence_handlers/v3.py`. The constraint is therefore not verifiable from this repository alone.
- Never initialize nested submodules; only root-declared submodules under `references/` are valid.

### 8.2 Identity And Authorization

- Message, correlation, causation, and EventStore aggregate identifiers must use ULID-safe handling where EventStore envelope semantics require sortable unique IDs.
- `Guid.TryParse` is forbidden for `messageId`, `correlationId`, `aggregateId`, and `causationId`.
- Tenant access must be validated before status, idempotency, state, projection, admin, or generated REST data can disclose resource existence. Exactly one explicit tenant is canonicalized and authorized under NFR2; ambiguous, invalid, noncanonical, or reserved input fails before downstream work.
- A public command-status resource is selected only by a valid canonical `MessageId`. `CorrelationId` remains diagnostic and cannot be substituted for status identity.
- Every externally reachable or JWT-binding host consumes the shared versioned NFR3 contract; host-local subsets and implicit defaults are nonconforming.
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

**Safety boundary:** Provider-level append fencing and write-once storage enforcement remain outside MVP implementation scope. FR31 delivers race and conflict evidence only; it grants no authority to implement a fence. This scope exclusion is not a safety waiver and cannot count toward NFR7 or SM11. Until a provider-portable fence is implemented and proven, or an approved and enforced supported operating envelope makes the race impossible, NFR7 class (c), implementation readiness, and Phase 4 MVP completion remain blocked (OR4).

### 9.1 In Scope

- Epics 1-7 as listed in `epics.md`; `epics.md` also lists Epic 8, which is committed post-MVP under §9.3.
- Domain-service SDK, read-model, cursor, projection, event-consumer, telemetry, health, Aspire, and packaging seams.
- REST generator contract and controller emission work with Sample and Tenants external API proofs.
- Release/repository reliability corrections including references layout, package-mode validation, live-sidecar re-tiering, shared workflow reuse, manifest-driven package scope, and the Story 3.16 latest-compatible dependency and root-submodule refresh that implements the §8.1 catalog-currency constraint and is required for Epic 3 closure.
- Event correctness and recovery remediation including idempotency, replay dispatch, crash recovery, append evidence, and global-position sharding renegotiation.
- Security and tenant isolation remediation, including production auth guards, secret stripping, admin endpoint protections, trust-boundary closure, and topology parity.
- Cost/evolution work behind spec-first gates for folded snapshots, projection cost/sequence guards, and event versioning/upcasting.
- Operator/admin/deployment/test recovery work and explicit backlog artifacts for deferred capability tracks.
- Projection/query parity completion: generic read-model/checkpoint erasure, coordinated batch writes, six-state lifecycle compatibility, asynchronous multi-projection dispatch, production-path idempotency, correct paged rebuilds, and owner-approved runtime-pin closure.

### 9.2 Out Of Scope For MVP

- Full GDPR aggregate/event tombstoning, broker-history deletion, physical backup erasure, audit-record deletion, and provider/operator key-custody operations remain outside the Phase 4 MVP under GDPR-1. Generic projection read-model/checkpoint erasure is in scope under FR5 and Story 1.14. The optional shared payload-protection engine and Parties G5 parity are a committed post-MVP capability in Epic 8. Stories 22.7a-d, EventStore stories under the pre-restructure numbering scheme described in the approved 2026-07-16 proposal, supplied prerequisites - provider-neutral hooks and protection metadata, typed unreadable outcomes, key-lifecycle workflow, restored-backup admission, and redaction/recovery contracts - not that engine.
- Global-position sharding implementation. FR24 delivers an approved successor specification only; completion authorizes downstream planning, not implementation, deployment, or migration.
- Admin interactive OIDC login implementation; backlog artifact only.
- Aggregate test kit implementation; backlog artifact only.
- REST generator hardening beyond the approved Epic 2 proof scope; backlog artifact only.
- AOT/trimming support while reflection conventions remain load-bearing.
- Moving generated REST controllers into interactive UI hosts.
- Treating HTTP `202`, SignalR notification, or command acceptance as projection-confirmed UI success.

### 9.3 Committed Post-MVP Scope

- Epic 8 owns the optional shared payload-protection security specification and implementation under FR37/NFR19.
- Epic 8 does not block Phase 4 MVP completion. Story 8.1 approval authorizes only Story 8.2 to begin, Stories 8.2-8.11 implement and prove the capability in sequence, and Story 8.11 blocks Parties Story 8.7 migration until the engine is implemented, reviewed, released or pinned, proven through dual-provider compatibility, and successfully rolled back after `pdenc-v2` writes.
- Provider/operator root-key custody, production credentials, KMS/HSM/secret-store service operation, and environment policy remain operational responsibilities rather than EventStore-owned secret material.

## 10. Success Metrics

Metrics are marked **achieved**, **partially met**, or **not met**; a metric without a tag is still being measured and has no interim disposition to report. An achieved metric is retained for the record but no longer discriminates between success and failure for the remaining epics.

**Planning-recovery metrics (achieved)**

- **SM1 (achieved 2026-07-05):** Implementation readiness no longer reports a missing PRD as a blocker. Satisfied by this document's existence; it cannot fail and does not measure Phase 4 delivery.
- **SM3 (achieved 2026-08-01):** High-risk NFRs NFR1-NFR4, NFR7, NFR10-NFR11, and NFR14-NFR17 map to concrete story coverage. The original deadline, "before Phase 4 implementation resumes", elapsed when Epics 4 and 5 began; §11.2 now carries the mapping and NFR7 carries its per-class delivery state.
- **SM5 (achieved):** Required architecture and UX artifacts exist under `_bmad-output/planning-artifacts` and are referenced by the epic plan.

**Live delivery metrics**

- **SM2 (partially met):** Every requirement from FR1 through FR37 maps to at least one epic and to either one primary owning story or a named set of disjoint primary slices in §11.1. Epic 8 is explicitly classified post-MVP, and §11.2 carries the equivalent NFR-to-story mapping. The mapping is now internally complete and unambiguous after the 2026-09-08 retirement of OR12. SM2 remains partially met until the PRD-to-epics ownership drift guard exists and `epics.md` is reconciled to this PRD's current digest rather than its stale input baseline (OR8, OR14).
- **SM4:** The oversized stories identified by readiness review are decomposed per `_bmad-output/planning-artifacts/story-id-migration-2026-08-01.md`. Story 4.8 is now a non-executable evidence ledger decomposed into Stories 4.9-4.15 and must never carry an execution status. Stories 7.14 and 8.2 retain their numbers as the first focused child of their own decompositions - 7.14 as the Admin shell and canonical-route boundary, 8.2 as payload-protection contracts and golden vectors - with siblings 7.19-7.20 and 8.3-8.11. Both remain `backlog`, which is decomposition, not completion.
- **SM6 (not met):** See §6.8. FR36's source/package and frozen-evidence sub-states are closed; the deployed-runtime sub-state is open at Story 3.15, 0 of 3 receipts.
- **SM7:** Story 8.11 records the payload-protection G5 packet as owner/security-approved `available`, exact source/package/backend identities are recorded, EventStore goldens and Parties dual-provider parity pass, and Story 8.10 rollback succeeds after `pdenc-v2` writes before Parties Story 8.7 resumes. Validates FR37/NFR19.

**Product-bet metrics**

These measure the §2 thesis that platform reuse and operational hardening must be delivered together. Each is a count with a target, so it can fail.

- **SM8 (FR9, reuse):** Duplicated platform plumbing remaining in the Tenants domain module - request routers, projection actors, cursor codecs, state-store plumbing, telemetry registration, health checks, and per-domain Aspire wiring. Target zero. Currently non-zero: the Tenants domain-service host retains transitional `AddDaprClient`, `UseCloudEvents`, controller, MediatR, and router composition (DW-64).
- **SM9 (FR13, NFR14, reuse):** Generated or hand-written per-message MVC command/query controllers hosted inside interactive UI hosts. Target zero, measured by the guardrail suite rather than by inspection.
- **SM10 (NFR1, hardening):** Surfaces in the NFR1 fail-closed set - public, internal, domain-service, projection-notification, admin - that carry at least one negative test proving anonymous or forged-flag access is rejected. Target: every surface, with the health/liveness/readiness probes recorded as the single pinned anonymous exception.
- **SM11 (not met; NFR7, hardening):** Silent-loss classes with an implemented guard or recovery that prevents loss in the supported operating envelope and is proven through production paths. Target five of five; deferral paperwork and scope exclusion never count. Currently three of five are delivered: class (a) staged-state flush loss is guarded by Story 5.1; class (b) stale pipeline records are guarded by Story 4.2; and class (d) committed-but-unpublished events are recovered by Story 4.4. Class (c) has a reproduced `same-key-overwrite-raw-durable-write-lost` outcome and no fence or approved enforced operating envelope. Class (e) has implementation evidence from Stories 4.9-4.14, but closure remains pending because the Story 4.15 lifecycle sources disagree and the required pre-review validation fails. See OR4 and OR15.
- **SM12 (NFR16, hardening):** Share of high-tier tests in the required evidence set that assert persisted state-store, read-model, marker, lifecycle, or checkpoint end-state rather than HTTP status or mock call counts. Target: the full required set, per NFR16.

**Counter-Metrics**

- **SM-C1:** Do not optimize for reducing the number of Phase 4 stories if doing so preserves unreviewable multi-concern stories. Counterbalances SM4.
- **SM-C2:** Do not count API smoke responses as integration evidence where persisted state-store/read-model/CloudEvent evidence is required. Counterbalances SM1, SM3, and SM12.
- **SM-C3:** Do not satisfy UI readiness by documenting intent only; UI stories still need component/governance evidence in `ux.md` and tests. Counterbalances SM5.
- **SM-C4:** Do not reach SM8 or SM9 by moving plumbing into a shared host or a test-only shim rather than into an EventStore library seam, and do not count a guardrail as coverage where §6.1 records an accepted evasion. Counterbalances SM8 and SM9.
- **SM-C5:** Do not reach SM10 or SM11 by weakening what the guard asserts. A negative test that passes because the guarded condition can no longer arise, or a loss class retired by narrowing its definition rather than by guarding it, does not count. Counterbalances SM10 and SM11.

## 11. Traceability

### 11.1 FR To Epic Coverage

| FR | Primary epic coverage | Primary owning story or disjoint slices (`epics.md`) |
| --- | --- | --- |
| FR1 | Epic 1 - Domain author self-service platform | 1.11 |
| FR2 | Epic 1 - Domain-service SDK host shape | 1.1 |
| FR3 | Epic 1 - Canonical domain-service DAPR endpoints | 1.1 |
| FR4 | Epic 1 - Domain query-handler seam and gateway routing; Epic 2 - metadata/provenance | 1.2, 1.16, 2.7, 2.11 - clause slices in §7.1 |
| FR5 | Epic 1 - Generic persisted read-model store and write policy | 1.3, 1.14, 1.15 - clause slices in §7.1 |
| FR6 | Epic 1 - Reusable protected query cursor codec | 1.5 |
| FR7 | Epic 1 - Generic projection-handler and domain-event consumer seams | 1.6, 1.15, 1.17-1.19 - clause slices in §7.1 |
| FR8 | Epic 1 - Aspire, telemetry, and health-check platform extensions | 1.7 |
| FR9 | Epic 1 - Sample and Tenants adoption of platform SDK seams | 1.10 |
| FR10 | Epic 1 - DomainService and ServiceDefaults packaging | 1.12 |
| FR11 | Epic 2 - REST API source-generator contract seam | 2.1 |
| FR12 | Epic 2 - Generated typed REST controllers and generator tests | 2.2 - discovery/emission/delegation/query-metadata slice; corrective successor or reopened 2.2/2.4/2.5 - tenant-boundary slice; corrective successor or reopened 2.9 - sole-`MessageId` `Location` slice |
| FR13 | Epic 2 - External API hosts for generated REST; UI uses client libraries | 2.3 |
| FR14 | Epic 2 - Sample contracts library and external Sample API proof | 2.3 |
| FR15 | Epic 2 - Tenants external API proof and UI client-library adoption | 2.4 - contract metadata/routes; 2.5 - API host; 2.6 - UI client/UX; 2.11 - query provenance; 2.12 - runtime identity/package mode; corrective successor or reopened 2.4/2.5 - platform-operation/tenant boundary |
| FR16 | Epic 2 - Metadata-rich, scope-aware projection-changed transport | 2.8 |
| FR17 | Epic 3 - Live-sidecar tests re-tiered off release gate | 3.1 |
| FR18 | Epic 3 - Overridable DaprETagService actor timeout | 3.2 |
| FR19 | Epic 3 - Submodules under references layout; root submodule refresh in Story 3.16 | 3.3 |
| FR20 | Epic 3 - Aspire Keycloak resource renamed to security | 3.4 |
| FR21 | Epic 3 - Ecosystem-wide Builds package catalog with explicit source opt-in and package-safe defaults; latest-compatible catalog refresh in Story 3.16 | 3.5 |
| FR22 | Epic 3 - Release commands assert package mode and avoid submodule packaging | 3.6 |
| FR23 | Epic 4 - Non-zero global positions, MessageId CloudEvent IDs, duplicate result fidelity | 4.1 |
| FR24 | Epic 4 - Global-position sharding spec renegotiation | 4.6 |
| FR25 | Epic 3 - Shared Hexalith.Builds gates and manifest-driven package scope | 3.7 |
| FR26 | Epic 5 - Phase 0 security and safe-remediation fixes | 5.1-5.4 - clause slices in §7.1; the `done` Story 5.3 status does not establish all-host NFR3 conformance |
| FR27 | Epic 4 - Resume/idempotency integrity, command status re-keying, and Stories 4.9-4.15 durable tenant/key admission and closure | 4.2, 4.9-4.15 - named disjoint slices |
| FR28 | Epic 5 - Defense-in-depth trust boundary | 5.5 |
| FR29 | Epic 4 - Replay and dispatch determinism | 4.3 |
| FR30 | Epic 4 - Crash recovery for committed-but-unpublished events | 4.4 |
| FR31 | Epic 4 - Append durability verify-first spike | 4.5 |
| FR32 | Epic 5 - Runtime topology and deployment posture parity | 5.6-5.9 - named disjoint slices |
| FR33 | Epic 6 - Bounded cost and event evolution | 6.1-6.6 - clause slices in §7.1; 6.4 remains unauthorized while its bound specification is absent |
| FR34 | Epic 7 - Delivery, admin, deploy, and IntegrationTests recovery | 7.1-7.11, 7.14, 7.19, 7.20 - clause slices in §7.1 |
| FR35 | Epic 7 - Backlog capability tracking | 7.15-7.18 - planning slices only; no runtime authority |
| FR36 | Epic 1 - projection/query parity source/package closure; Epic 3 - deployed-runtime parity closure; consumer-specific authority | 1.20 - source/package slice; 3.15 - deployed-runtime slice; per-consumer removal authority - unassigned blocking slice |
| FR37 | Epic 8 - Shared payload-protection security specification and Stories 8.2-8.11 implementation, production backend, release, Parties parity, rollback, and G5 closure | 8.1-8.11 - gated disjoint slices; 8.11 owns whole-capability closure |

Story ownership is declared by each `### Story` section in `epics.md`; this table records the intended requirement slices so SM2 can be read on one page. It records ownership only, never delivery or lifecycle state. Supporting stories are intentionally omitted. A multi-story entry lists explicitly named, disjoint primary slices; the requirement closes only when every clause in §7.1 has content-bound passing evidence. Corrective and unassigned slices above expose current gaps that `epics.md` does not yet own. Downstream handoff remains blocked until those gaps and the PRD/architecture/UX/epics approval sequence are reconciled under OR8 and OR14.

### 11.2 High-Risk NFR Story Coverage

SM3's Phase 4 gate covers NFR1-NFR4, NFR7, NFR10-NFR11, and NFR14-NFR17. The table also tracks NFR5-NFR6, NFR8-NFR9, and NFR12-NFR13 so missing primary ownership cannot remain hidden, NFR18 for its documentation debt, and NFR19 for the separately gated post-MVP commitment validated by SM7.

Each row lists every story whose own `epics.md` section declares that NFR, whether as primary or supporting coverage; `epics.md` remains authoritative. A story appearing in a row means it declares the requirement, not that the requirement is delivered - see NFR7, whose per-class delivery state is stated in §7. Stories whose sections declare coverage only through a hyphen range are footnoted rather than silently expanded.

| NFR | Declaring stories (`epics.md`) |
| --- | --- |
| NFR1 | 2.8, 2.10, 3.10, 5.2, 5.4-5.5, 5.7, 7.2-7.4, 7.7, 7.19, 8.1, 8.3, 8.5-8.7, 8.9, 8.11 |
| NFR2 | 1.5, 1.9-1.10, 1.14, 2.5, 3.10, 5.2, 5.5-5.8, 5.10, 7.1-7.4, 7.19, 8.7 |
| NFR3 | 5.3, 8.3, 8.6 |
| NFR4 | 5.3-5.4, 7.6, 8.1, 8.5-8.6, 8.9, 8.11 |
| NFR5 | **Primary: unassigned.** Supporting: 2.8, 7.1. The primary-owner gap is blocking; declaration is not delivery. |
| NFR6 | 1.6, 1.10, 1.18, 2.8, 4.1, 4.3, 4.6, 6.4, 7.1 |
| NFR7 | 1.3, 1.15, 1.17-1.19, 4.1-4.2, 4.4-4.6, 4.8-4.15, 5.1, 6.4-6.6, 7.8, 7.11, 8.1-8.2, 8.4-8.5, 8.7, 8.9-8.11 |
| NFR8 | 1.2, 1.9, 1.13, 1.16, 1.19, 2.11, 4.7, 6.1-6.4 |
| NFR9 | 2.12, 3.3-3.6, 3.8, 3.11-3.16, 8.1, 8.8, 8.11 |
| NFR10 | 3.1, 3.7-3.8, 3.11, 7.10, 7.12-7.13 |
| NFR11 | 3.6, 3.8-3.9, 3.12, 3.14-3.16, 7.9, 8.8 |
| NFR12 | **Primary:** 1.17, 3.13, 3.15. **Supporting:** 1.20, 2.7, 2.8, 2.12, 3.16, 6.1-6.6, 7.5, 8.2, 8.4, 8.9, 8.10; range-declared 8.1 and 8.11. Existing declarations do not yet cover the expanded public-surface inventory. |
| NFR13 | **Primary: unassigned.** Supporting: 2.4, 2.9. Story 2.2 is a candidate generator-build owner but does not declare NFR13; the gap is blocking. |
| NFR14 | 1.8, 1.11, 2.3, 2.5-2.6, 2.10-2.11, 7.5, 7.14 |
| NFR15 | 1.16, 2.6, 2.8, 3.10, 7.3-7.5, 7.19-7.20 |
| NFR16 | 1.2-1.5, 1.9-1.10, 1.13-1.15, 1.17-1.21, 2.7-2.8, 2.11-2.12, 3.1-3.2, 3.4-3.6, 3.8, 3.10-3.15, 4.2, 4.4-4.5, 4.7-4.15, 5.8, 6.4, 6.6, 7.1, 7.3, 7.6-7.13, 7.19-7.20, 8.1, 8.7-8.11 |
| NFR17 | 3.12, 3.14, 5.6-5.9, 7.6-7.10, 8.1, 8.6, 8.11 |
| NFR18 | 6.5, 6.6 - supporting reflection-posture references only. No story owns the required `docs/reference/aot-and-trimming-posture.md` document; see §12. |
| NFR19 | 6.5-6.6, 8.1-8.7, 8.9-8.11 |

Range-notation footnote: the following stories declare coverage through a hyphen range whose endpoints alone are literal, and are therefore not counted above - NFR2 and NFR3 in 8.1, 8.5, 8.9, 8.11; NFR10 in 8.1, 8.8, 8.11; NFR11 in 8.1, 8.11; NFR15 in 2.11.

### 11.3 Required Follow-On Readiness Work

The PRD, architecture, UX, epics, sprint status, and implementation evidence exist, but they do not form one clean, reviewed, approved baseline. The 2026-09-10 reconciliation examined `HEAD` `293c69c42d35dee26682d42f05c943c0b65786f4` plus a dirty worktree. Raw digest equality is integrity evidence only; it never proves substantive reconciliation, owner approval, or an atomic baseline. Implementation readiness stays blocked until §12 and every mandatory row in §11.4 pass. A readiness re-run is the final step, not a substitute for those corrections.

| Artifact or evidence | Current identity/status on 2026-09-10 | Approval or conflict state | Handoff effect |
| --- | --- | --- | --- |
| `prd.md` | Updated by this reconciliation; its digest must be captured externally after final bytes settle. `epics.md` still records the superseded pre-update digest `b99effdb414209da373433d9b4d2075072ca950e89534b22b81155236f650662`. | This document may be final while readiness remains blocked. It is not yet reconciled into an approved epic baseline. | Blocks implementation-readiness `READY` and downstream handoff. |
| `architecture.md` | SHA-256 `7e3dbc7bd335034bd9b98cadfed8b14650b7d811321b326b8f32e1b280960d51`; `status: draft`; AD-26 remains `[ASSUMPTION]`. | Useful fail-closed input, but reviewer closure and explicit owner ratification of AD-26 or an approved replacement are absent. | Cannot authorize implementation, release, or deployment. |
| `ux.md` | SHA-256 `2927f97d4fe7262ec5886084a974d9d5c21240560e6563ab84f84b90e4dd670a`; top-level status `final`. | `epics.md` matches this top-level digest, but records stale detailed `DESIGN.md` and `EXPERIENCE.md` digests: recorded/current `76be2697...`/`3f4f0181...` and `06de15b3...`/`11f75403...`. | UI handoff is not one content-bound approved source set. |
| `epics.md` | SHA-256 `d067c8fbffce47d7d0518396265f862093ec1a513cab73cb9fea1e88185cf33b`; its pre-update PRD and architecture digests matched after commit `0994c378`. | That commit refreshed hashes without the OR14 review/approval sequence. This PRD update makes the recorded PRD digest stale again. | Hash-only refresh is prohibited; substantive reconciliation and renewed approval are required. |
| `sprint-status.yaml` and story wrappers | Dirty worktree snapshot. | 4.15: tracker/epics `review`, wrapper `done`; 5.2: tracker `done`, wrapper `in-review`, epics `backlog`; 5.3: wrapper and worktree tracker `done`, committed tracker and epics stale; 5.4: tracker `review`, wrapper `done`, epics `backlog`; 6.1: tracker `review`, wrapper `done`, canonical spec `approved-authorized`, epics `backlog` and says absent. | No conflicting lifecycle value can supply delivery or gate evidence. |
| OQ8 evidence | Full external identity is present only in this PRD and architecture; other governed artifacts bind incomplete identity. `python3 tools/validate-oq8-platform-evidence.py --pre-review` exits 1 with `Reviewed public document body drift: docs/guides/configuration-reference.md`. | Story 4.15's `eventStorePlatformComplete: true` is a bounded source-only packet claim; it grants no release, deployment, runtime-pin, consumer-migration, Folders-closure, or readiness authority. | G-OQ8 fails independently of lifecycle alignment and OR11 remains open. |

- Stories 1.20 and 3.12 remain `done` and are not reopened by the deployed-runtime correction. Story 1.20 remains the completed source/package parity gate. Story 3.13 records the immutable v3.94.1 candidate as rejected and non-authorizing because its config provenance is malformed and its retained authority forbids deployment. Story 3.15 owns positive deployed-runtime parity for the corrective release produced by Story 3.14. Neither result reopens Story 1.20 or authorizes Parties 8.6, G5, deployment, or consumer migration. This planning update authorizes no release, deployment, Git, or submodule mutation; external publication under Story 3.14 requires a separate durable release-owner authority record.

- Parties payload-protection G5 remains `needs-additive-api`. Story 8.1 is approved and `done`, and it authorizes Story 8.2 alone for exact-digest/source preflight; Story 8.2 is authorized but still `backlog` pending its story file, and Stories 8.3-8.11 remain predecessor-gated. G5 closes only after Stories 8.2-8.10 complete their gated implementation and evidence and Story 8.11 supplies an owner/security-approved `available` packet with exact source/package/backend identities, EventStore goldens, Parties dual-provider compatibility, and rollback after `pdenc-v2` writes.

- The former coordinated-slice parents are superseded by focused children under the dated restructurings. The pre-2026-07-15 legacy Story 1.6 means Sample/Tenants coordinated adoption; current Story 1.6 means Projection And Domain Event Consumer Seams. `_bmad-output/planning-artifacts/story-id-migration-2026-07-15.md` remains the July audit authority, and `_bmad-output/planning-artifacts/story-id-migration-2026-08-01.md` governs Story 3.13, the 4.9-4.15 OQ8 split, Story 5.10, the 7.14/7.19/7.20 Admin UI split, and the 8.2-8.11 payload-protection sequence.
- Story 5.2 now requires concrete request-size limits: `1_048_576` bytes for representative admin JSON write/sandbox bodies and `10 * 1024 * 1024` bytes for `AdminBackupsController.ImportStream`, with bounded rejection tests and no upstream service invocation on excessive requests.
- The approved Story 6.1 specification authorizes Story 6.2; lifecycle reconciliation remains OR5. The projection-cost gate remains OR16 before Story 6.4, and the event-versioning specification remains required before Story 6.6.
- Story 5.3 reached `done` at review loop 8 in its wrapper, and the dirty worktree tracker also says `done`; the committed tracker and `epics.md` narrative remain stale. This local transition does not establish an approved atomic baseline and cannot close NFR3 for Tenants, generated hosts, or future JWT-binding hosts.
- The former Story 7.5 backlog reclassification is carried by Stories 7.15-7.18, one per planning/backlog artifact, with exact deliverables:
  - `_bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md`
  - `_bmad-output/planning-artifacts/backlog/iam-1-admin-oidc-login.md`
  - `_bmad-output/planning-artifacts/backlog/kit-1-aggregate-test-kit.md`
  - `_bmad-output/planning-artifacts/backlog/rest-generator-hardening.md`

### 11.4 Phase 4 MVP Exit Decision Contract

`READY` is computed, not asserted: every mandatory MVP gate below must be `PASS` against one current, clean, content-bound, approved source digest set. Any missing, stale, mismatched, unapproved, red, unavailable, or conflicting input computes `Reject`. A story label or local documentation edit cannot change a gate result. Every passing row must retain its exact command or lane, evidence identity, evaluator result, and independent approval. Any governed source, requirement, validator, evidence, lifecycle, role registry, or approval change invalidates the affected row and G-READINESS. An alternate implementation or enforced operating envelope named below is a conformance path, not a waiver; no mandatory row permits deferral, risk text, or owner assertion to substitute for proof.

| Gate | Governed requirements / blockers | Required evidence and validation | Evaluator and approval | Current result and blocking effect | Waiver / invalidation |
| --- | --- | --- | --- | --- | --- |
| G-BASELINE | OR5, OR8, OR14, OR15; SM2 | One manifest binding final PRD, architecture, detailed UX, epics, sprint status, story wrappers, and relevant evidence digests after substantive reconciliation; a guard must compare FR/NFR text, §7.1/§11 ownership, artifact statuses, and lifecycle values. The exact validator lane/command is not yet defined. | Product, architecture, epic, UX, test, and tracker owners; approval must follow validation of the same manifest. | **FAIL/BLOCKED.** Architecture is draft, detailed UX digests are stale, this PRD now differs from the epics input digest, lifecycle values conflict, and the guard is absent. Blocks every downstream handoff. | None. Any bound byte, status, mapping, or approval change invalidates the row. |
| G-OQ8 | FR27; NFR7, NFR16; OR11 | Available governing bytes by permitted immutable copy, complete approved normative projection, or signed/content-addressed Folders attestation, binding repository, path, commit, and SHA-256 in PRD, architecture, epics, packet, validator, and CI; `python3 tools/validate-oq8-platform-evidence.py --pre-review` must pass on the bound subject. | Architecture, Security, Test, EventStore, and Folders content owners; non-authorship control from G-HIGH-RISK. | **FAIL/BLOCKED.** Governing bytes are unavailable inside EventStore, identity propagation is incomplete, and validation exits 1 on `docs/guides/configuration-reference.md` drift. Blocks OQ8, Story 4.15 contribution, readiness, release, and consumer handoff. | None. Any design identity, projected byte, public-document subject, packet, validator, or approval change invalidates the row. |
| G-TENANT | FR12-C2, FR15; NFR2; OR20 | Approved corrective or reopened story; compiled generated-controller and Tenants runtime negatives for missing, duplicate, conflicting, malformed, unauthorized, uppercase/noncanonical, and reserved `system` tenant inputs, proving rejection before routing/state access and proving platform operations use distinct authenticated scope. Exact CI lane/command must be bound by that story. | EventStore, Tenants, Security, and Test owners with independent approval. | **FAIL/BLOCKED.** Current generated and Tenants paths return or route synthetic/raw tenant values while affected stories are labelled `done`. Blocks tenant-safety, readiness, release, and deployment claims. | None. Generator, canonicalizer, Tenants host, route, test, or approval changes invalidate the row. |
| G-STATUS-ID | FR12-C3-C4; AD-17/AD-32; OR21 | Approved corrective or reopened Story 2.9; compiled and runtime tests for missing, blank, malformed, noncanonical, and divergent `MessageId`/`CorrelationId`, proving only `MessageId` can select status `Location`. Exact CI lane/command must be bound by that story. | EventStore API and Test owners with independent approval. | **FAIL/BLOCKED.** Current generator and tests pin a `MessageId ?? CorrelationId` fallback while Story 2.9 is labelled `done`. Blocks FR12, readiness, and external API release. | None. Status route, identity builder, generator, tests, or approval changes invalidate the row. |
| G-APPEND | NFR7 class (c), SM11, OR4 | An approved tracked story owned by EventStore persistence that either implements provider-portable write-once/append fencing or mechanically enforces a supported no-second-writer operating envelope, plus supported-provider and production-path proof that silent overwrite cannot occur. Current reproduction and DW-326 remain input evidence, not closure. | Product, architecture, persistence, and Test owners with independent approval. | **FAIL/BLOCKED.** `same-key-overwrite-raw-durable-write-lost` is reproduced; no fence, enforced envelope, owner-assigned story, or passing prevention evidence exists. Blocks MVP completion, readiness, release, and deployment. | No risk-acceptance waiver. An approved enforced envelope is an alternate conformance path. Any provider, topology, writer, test, evidence, or envelope change invalidates the row. |
| G-CLAUSE | FR4, FR5, FR7, FR12, FR26, FR33, FR34, NFR17; OR7 | §7.1 must be mirrored in `epics.md`; each clause must bind a primary slice and exact passing evidence; an automated all-clauses-required validator must reject missing, duplicate, unsupported, or status-only closure. Exact validator lane/command is not yet defined. | Product, epic, architecture, and Test owners. | **FAIL/BLOCKED.** The PRD ledger now exists, but downstream clause ownership, evidence identities, and the validator are not reconciled; NFR17-C5 remains unassigned. Blocks parent-requirement closure and readiness. | None. Clause text, mapping, story, evidence, or validator changes invalidate the row. |
| G-NFR8 | NFR8, FR33-C3-C4, OR16 | Approved `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md` binding numeric budget, workload/page-size model, exact pass condition, evidence identity, and Story 6.4 authorization; the dependent validation command must be recorded in that spec. | Epic 6, architecture, and Test owners with approval of the same spec digest. | **FAIL/BLOCKED.** The specification, numeric bound, approval, validation command, and Story 6.4 authority are absent. Blocks Story 6.4, NFR8 acceptance, and readiness. | None. Spec, workload, bound, implementation, or evidence changes invalidate the row. |
| G-COMPAT | NFR12; OR22 | Manifest-backed public-surface inventory; source/binary API and wire baselines; SemVer/deprecation/removal policy checks; representative package-only consumers; exact release-lane command and content-bound results. | EventStore API, release, consumer, and Test owners with independent approval. | **FAIL/BLOCKED.** Fourteen release packages exist, but repository-wide API/wire baselines, complete inventory, representative consumers, and an enforcing release gate are absent. Blocks compatibility and release claims. | Only an approved SemVer-major breaking-change proposal can authorize an incompatible release; it does not waive inventory, migration, evidence, or approval. Any public surface or baseline change invalidates the row. |
| G-AUTH-HOSTS | NFR1-NFR3; OR23 | One versioned JWT contract/fingerprint consumed by every externally reachable or JWT-binding host, including Tenants and generated-host fixtures, with negative Production/break-glass/algorithm/issuer/audience/signature/lifetime/role/tenant tests and exact release evidence. | Security, EventStore, Tenants, release, and Test owners with independent approval. | **FAIL/BLOCKED.** Story 5.3 is locally `done` for its bounded hosts, but Tenants hand-rolls a weaker subset and no all-host/future-host conformance gate exists. Blocks NFR3, readiness, release, and deployment. | None. Contract, host inventory, fingerprint, host code, tests, or approval changes invalidate the row. |
| G-CONSUMER | FR36; AD-22; OR24 | Passing capability packet, separate authenticated release/deployment authority, and for each consumer an authenticated immutable consumer-owner receipt binding repository/commit, mode matrix, removal-subject digest, role-registry identity, `consumer-removal-authorized`, issuance, validity, and invalidation rules. | EventStore owner, release owner, and the bound consumer owner; non-authorship control from G-HIGH-RISK. | **FAIL/BLOCKED.** Per-consumer owner, receipt, removal-subject binding, and validity evidence are absent. No consumer may remove local infrastructure. | None. Any bound source, package, runtime, consumer, mode, removal subject, role registry, receipt, or expiry change invalidates the applicable authority. |
| G-HIGH-RISK | NFR7, NFR12, NFR16 and every readiness-authorizing high-risk row; OR10, OR13 | Each high-risk story must bind an exact test lane and command, trigger, subject/evidence identities, pass condition, status-transition enforcement, and a sealed CI validator, second identity, or both as the approved non-authorship control. | Product, EventStore, Security where applicable, and Test owners; approver independence must be machine-verifiable. | **FAIL/BLOCKED.** The general non-authorship rule, exact correctness-gate contract, and guarded status transition are not approved. Blocks use of high-risk evidence for readiness, release, or migration. | None. Subject, validator, identity, roster, result, or status changes invalidate the row. |
| G-NFR18 | NFR18; OR6 | `docs/reference/aot-and-trimming-posture.md`, named primary story, content digest, review, and documentation/build validation command. | Platform and architecture owners. | **FAIL/BLOCKED.** Document, owner, evidence, and validation command are absent. Blocks NFR18 coverage and readiness. | None. Reflection posture, public API, document, owner, or validation changes invalidate the row. |
| G-READINESS | OR1 | A new persisted implementation-readiness report that consumes the single approved manifest and all passing mandatory rows; it must explicitly compute the aggregate and bind its validator/evaluator identity. | Product owner after all preceding evaluators approve the same source set. | **FAIL/BLOCKED — aggregate `Reject`.** Every mandatory row above must pass first. The 2026-08-01 `READY` report is historical and non-authorizing. | None. Any mandatory-row invalidation immediately returns the aggregate to `Reject`. |

**Post-MVP gate, excluded from the Phase 4 MVP aggregate:**

| Gate | Governed scope | Current result | Rule |
| --- | --- | --- | --- |
| G5 | FR37, NFR19, Epic 8 payload protection and Parties migration | **BLOCKED / `needs-additive-api`.** | G5 cannot block Phase 4 MVP completion, but Parties Story 8.7 cannot proceed until the owner/security-approved, exact-identity, dual-provider, backend, historical-read, and rollback packet is `available`. |

## 12. Open Questions And Owed Refinements

No previously settled FR ownership or Epic 8 scope decision is reopened by this update. Product-policy, acceptance, authority, and cross-artifact questions remain open. This PRD records them as blockers instead of inferring answers. The remaining payload-protection design decisions stay gated by Story 8.1 and the approved security specification listed in §11.3.

The following refinements are owed. Each states what this PRD does not yet settle, who owns the follow-up, and when it must be revisited. Every blocking item must close before the next implementation-readiness re-run can return `READY`.

**Safety and authority blockers**

| # | Owed refinement | Owner | Trigger |
| --- | --- | --- | --- |
| OR4 | **Blocking.** Resolve NFR7 class (c). Deliver and prove provider-portable append fencing, or define, enforce, and prove a supported operating envelope in which the observed append race cannot occur. Any risk acceptance must name the product and architecture approvers, exact bounds, evidence, expiry/revisit trigger, and prohibited completion claims; an owned deferral alone cannot satisfy NFR7 or SM11. | Product owner with architecture owner | Before Phase 4 MVP completion, readiness `READY`, release, or deployment |
| OR10 | **Blocking.** Define the non-authorship control for high-risk closure. State whether a sealed CI validator run, a second identity, or both are required to distinguish independently approved evidence from an author's assertion, and amend the binding FR/NFR or gate text rather than relying on the glossary. | Product owner with EventStore and test owners | Before any high-risk NFR is used to authorize `READY`, release, or migration |
| OR11 | **Blocking.** Make the OQ8 design authority reproducible inside the EventStore evidence boundary using a permitted immutable copy, a complete approved normative projection, or a signed or content-addressed Folders attestation. Propagate repository, path, commit, and digest into `architecture.md`, `epics.md`, OQ8 packets, and the validator; absence or mismatch must fail readiness. | Architecture owner with Folders content owner | Before OQ8 closure, readiness `READY`, release, or consumer handoff |
| OR13 | **Blocking.** Bind the correctness gate: name the exact test lane and command, trigger, evidence identity, pass condition, independent approval rule, and status-transition enforcement required before a story may be recorded `done` against a high-risk NFR. | Test owner | Before the next readiness re-run |

**Baseline and lifecycle blockers**

| # | Owed refinement | Owner | Trigger |
| --- | --- | --- | --- |
| OR5 | **Blocking.** Reconcile the Story 6.1 lifecycle against the approved canonical `_bmad-output/implementation-artifacts/spec-folded-snapshot.md`. The canonical specification has status `approved-authorized`; the wrapper says `done`; the tracker says `review`; and `epics.md` marks the story `backlog` and says the artifact is absent. The PRD recognizes the specification and Story 6.2 authorization but does not resolve those external lifecycle sources. | Epic 6 owner with tracker owner | Before Story 6.1 is called complete or Story 6.2 records implementation progress |
| OR6 | **Blocking.** Produce `docs/reference/aot-and-trimming-posture.md` and give NFR18 an owning story. The constraint is currently stated only in this PRD, which NFR18 itself says is insufficient. | Platform maintainer | Before NFR18 is marked covered or readiness returns `READY` |
| OR8 | **Blocking.** Add a guard that diffs this PRD's FR/NFR text and §11 ownership against the `epics.md` Requirements Inventory and story declarations, so neither requirements nor ownership can silently diverge again. | Test owner | Before the next readiness re-run |
| OR14 | **Blocking.** Reconcile `architecture.md` to this PRD and OQ8 identity, then reconcile `epics.md` to both. Only after review and renewed approval may `epics.md` replace its stale PRD and architecture input digests; a hash-only refresh is forbidden. | Architecture owner, epic owner, and product owner | Before downstream handoff or the next readiness re-run |
| OR15 | **Blocking.** Resolve lifecycle contradictions for Stories 4.15, 5.2, 5.4, and 6.1 across `sprint-status.yaml`, story wrappers/evidence, and `epics.md`; retain a passing OQ8 pre-review receipt for Story 4.15. Add guarded status-transition validation so the sources cannot diverge silently again. | Story owners with tracker owner | Before affected FR/NFR delivery claims or the next readiness re-run |
| OR16 | **Blocking.** Produce and approve `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md` with the numeric projection-cost bound and authorization required by NFR8 before Story 6.4 starts. | Epic 6 owner with architecture owner | Before Story 6.4 or NFR8 projection-cost acceptance |

**Acceptance and exit blockers**

| # | Owed refinement | Owner | Trigger |
| --- | --- | --- | --- |
| OR7 | **Blocking.** Sub-letter the omnibus requirements FR26, FR33, FR34, and NFR17 so each clause carries one testable consequence, or add a clause-to-story-to-evidence table with an all-clauses-required completion rule. Today a `done` story can appear in these rows without closing any named clause. Deferred 2026-07-16; still open. | Product owner with epic/story owners | Before the next readiness re-run |
| OR17 | **Blocking.** Define one Phase 4 MVP exit-decision table that separates mandatory gates from post-MVP commitments and, for each gate, identifies the evidence, evaluator, waiver policy, and current result. | Product owner | Before the next readiness re-run |

**Final readiness gate**

| # | Owed refinement | Owner | Trigger |
| --- | --- | --- | --- |
| OR1 | **Blocking.** Re-run implementation readiness only after every other blocking refinement in this section is resolved and the reconciled PRD, architecture, epics, evidence, and lifecycle sources form one approved baseline. The 2026-08-01 `READY` verdict is historical; the bound 2026-09-09 validation result is `Poor` / `Reject`. | Product owner | After all blocking refinements close; also whenever FR/NFR text changes, a retrospective is rejected, or a proposal alters scope |

**Non-blocking refinements**

| # | Owed refinement | Owner | Trigger |
| --- | --- | --- | --- |
| OR9 | Editorial restructure: front-load full scope and relocate volatile readiness history and replaceable mechanisms into a generated ledger or addendum. Deliberately deferred since 2026-07-16 to preserve stable downstream section anchors; §5 Product Concerns, which largely restates §6 and §7, should be folded in the same pass. | Product owner | Next major PRD revision, when anchor churn is acceptable |
| OR18 | Bind the exact shared Hexalith.Builds workflow identity and EventStore evidence-handler identity that enforce the §8.1 OCI platform set, and verify that the caller pin resolves to the validated workflow bytes. | Release owner | Before the next container promotion |
| OR19 | Reconcile the stale Epic 2 tracker rollup and the truncated Story 2.12 tracker key through the tracker owner's guarded process. | Tracker owner | Before the next sprint-status rollup |

Identifiers OR2, OR3, and OR12 are retired, not reused; the remaining numbering is unchanged so existing reviews that cite an OR number keep pointing at the same item.

**Retired 2026-09-08 (sprint change proposal `sprint-change-proposal-2026-09-08.md`).** Three owed refinements closed and are recorded here rather than deleted, because each was retired by correcting an artifact rather than by re-reading it.

- **OR2 (Story 5.3).** Resolved against `sprint-status.yaml`. `spec-5-3` is `in-progress` at review loop 3 with fourteen of twenty items complete, six open, and no `Auto Run Result` marker; the story's own Boundaries forbid it from modifying the tracker, and commit `c83cc4c3` flipped the row inside a test-only change that never mentions a status change. The row is now `in-progress` and the `epics.md` paragraph, which said `backlog` and asserted committed development signing-key and administrator credential values that no longer exist, now states the partial delivery. **NFR3 and NFR4 coverage may not be claimed from Story 5.3 until it closes.**
- **OR3 (Stories 4.5, 4.6, 5.1).** Resolved per story, not per source. Stories 4.5 and 5.1 are genuinely `done` - their specs record `done` at review loops 4 and 7 with sealed and test-backed evidence - and the `epics.md` paragraphs saying otherwise were stale; those paragraphs were corrected. Story 4.6 is not done: its spec records `approval_state: absent` and `implementation_authorized: false` with three outstanding operator actions, so the tracker row flipped by commit `8d6f7dac` was corrected back to `awaiting-operator`. NFR7 class (a) is now confirmed guarded (§7, SM11); class (c) is unchanged, because Story 4.5's completion delivers race evidence only and grants no fencing authority - that gap remains OR4.
- **OR12 (FR primary ownership).** Two findings. First, the claim that FR1's only primary owner was an Epic 4 story was itself wrong: `epics.md` records primary FR1 ownership at Story **1.11**, Epic 1, consistent with the FR Coverage Map and every other artifact. No change was needed and none was made. Second, the eight duplicate primary claims were real and are now resolved: FR11, FR13, FR19, FR21, FR22, and FR25 each have one primary owner with the other claimants demoted to supporting coverage, and FR12 and FR15 are partitioned into explicitly named disjoint slices governed by new completion rules, matching the FR27/FR33/FR34/FR36 idiom already in use. The Primary-ownership rule now states that partition condition explicitly instead of leaving it as unwritten practice.

## 13. Assumptions Index

No inline `[ASSUMPTION]` tags are present in this PRD. The PRD follows the approved change proposals, preserves Phase 4 scope without reduction or expansion, and records Epic 8 separately as committed post-MVP work.
