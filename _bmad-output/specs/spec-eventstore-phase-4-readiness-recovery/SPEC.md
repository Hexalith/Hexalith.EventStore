---
id: SPEC-eventstore-phase-4-readiness-recovery
companions:
  - requirements-traceability.md
  - readiness-gates.md
  - glossary.md
  - ../../planning-artifacts/architecture.md
  - ../../planning-artifacts/epics.md
  - ../../project-context.md
sources:
  - ../../planning-artifacts/prd.md
  - ../../planning-artifacts/implementation-readiness-report-2026-07-15.md
  - ../../planning-artifacts/sprint-change-proposal-2026-07-15.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability only.

# eventstore Phase 4 Implementation Readiness Recovery

## Why

Hexalith.EventStore Phase 4 must turn a working DAPR-native event-sourcing platform into a safer reusable developer platform before full implementation resumes. The pressure is both product and readiness: domain authors need platform-owned seams instead of copied boilerplate, operators need fail-closed trust and persisted evidence, and the planning set must stop using `epics.md` as a proxy for PRD, architecture, UX, and implementation slicing.

## Capabilities

- **CAP-1**
  - **intent:** Domain authors can build EventStore-backed domain modules with domain code only while EventStore libraries supply hosting, query, projection, read-model, cursor, telemetry, health, Aspire, packaging, and consumer-parity seams.
  - **success:** Sample and Tenants adoption preserve domain behavior, production-path evidence proves the generic replacement seams, and consumer infrastructure remains until an owner-approved parity packet matches the exact consumed EventStore source, package, or image identity.

- **CAP-2**
  - **intent:** External API developers can expose typed generated REST endpoints in dedicated API hosts while interactive UI hosts consume client libraries directly.
  - **success:** Generated controllers delegate to `IEventStoreGatewayClient`, Sample/Tenants UI hosts contain no generated or hand-written per-message MVC command/query controllers, and handler-computed or unknown responses never claim projection-confirmed state.

- **CAP-3**
  - **intent:** Maintainers can release reproducibly with references-based submodules, deterministic package mode, dedicated live-sidecar coverage, shared security workflows, and manifest-governed package output.
  - **success:** Release validation cannot publish submodule packages, and CI separates deterministic release-gate tests from live-sidecar tests.

- **CAP-4**
  - **intent:** Operators and consumers can trust event identity, idempotency, replay dispatch, append behavior, global-position semantics, and crash recovery under duplicates, concurrency, and failures.
  - **success:** Tests prove CloudEvent id stability, duplicate result fidelity, stale pipeline rejection, replay ambiguity handling, stored-but-unpublished recovery, and real DAPR conflict behavior.

- **CAP-5**
  - **intent:** Public, internal, domain-service, projection-notification, admin, and generated REST surfaces fail closed and preserve tenant isolation.
  - **success:** Anonymous and cross-tenant admin access fails, production auth rejects insecure modes unless break-glassed, committed config contains no forgeable secrets, and topology tests inspect actual sidecar component paths and ACL posture.

- **CAP-6**
  - **intent:** Long-lived streams can evolve with bounded snapshot/projection cost, sequence-safe projection updates, event versioning/upcasting, validated identity metadata, and cancellation-aware public seams.
  - **success:** The exact bounded `ProjectionDispatchResult` and replay-equivalent paged rebuild baseline are proven before folded snapshot, projection cost/sequence guard, and event versioning/upcasting specs authorize dependent implementation.

- **CAP-7**
  - **intent:** Operators get explicit delivery semantics, poison/dead-letter handling, attributable admin actions, honest unavailable-operation behavior, hardened deployment posture, meaningful higher-tier evidence, and tracked future capability backlog.
  - **success:** `Hexalith.EventStore.Admin.UI` is the single consolidated FrontComposer-based EventStore UI, unavailable operations are hidden, disabled, or return `501`, audit records are support-safe, integration tests assert persisted evidence, and four independently governed backlog artifacts exist.

- **CAP-8**
  - **intent:** Phase 4 has a coherent planning baseline before full implementation resumes.
  - **success:** The seven-epic plan has no forward dependency, all eight oversized parents are replaced by focused children, active identifiers and evidence are auditably migrated, and a fresh implementation-readiness assessment reports no structural blocker.

## Constraints

- PRD owns FR/NFR truth and readiness traceability; architecture owns component, integration, topology, and decision-record gates; UX owns UI governance and flows; `epics.md` owns story slicing, sequencing, acceptance criteria, and implementation handoff.
- Full Phase 4 implementation should not resume until the PRD, architecture artifact, UX artifact, story splits, and high-risk NFR traceability are reconciled and readiness is re-run.
- The platform remains DAPR-backed hexagonal event sourcing: the EventStore gateway is the policy edge, DAPR actors own aggregate write serialization, domain services are pure domain adapters, and external adapters call platform seams.
- Generated REST controllers live only in dedicated external API hosts and delegate to `IEventStoreGatewayClient`; interactive UI hosts consume EventStore Client libraries directly and host no generated or hand-written per-message MVC command/query controllers.
- `AggregateActor` owns durable event mutation; domain code returns `DomainResult` and never writes EventStore state directly.
- Read models use `IReadModelStore` plus `ReadModelWritePolicy`; cursors use `IQueryCursorCodec` plus `QueryCursorScope` and remain opaque, bounded, scoped, and fail safe.
- Projection and pub/sub delivery are at-least-once and unordered; notifications are freshness signals, not proof of success; consumers deduplicate by EventStore `MessageId`.
- Runtime topology changes must update AppHost, DAPR component/configuration YAML, app IDs, sidecar options, ACLs, topics, scopes, and topology tests together.
- Security fails closed above infrastructure scoping: application-layer credentials and tenant authorization are required before data disclosure.
- Release is manifest-governed through `tools/release-packages.json`; Release/package validation uses package-reference mode by default; submodule packages are not produced by EventStore release jobs.
- High-risk verification must assert persisted Redis/state-store/read-model/CloudEvent bodies, topology YAML or sidecar arguments, package outputs, and security denials.
- Folded snapshots, projection delivery cost, projection sequence guards, event versioning/upcasting, identity metadata validation, cancellation-token public seams, and global-position sharding require approved specs before implementation stories start.
- Preserve the frozen `/project/v2` wire response and emit one server-owned `ProjectionDispatchResult` Version 1 with bounded ordinal route entries, stable status codes, and explicit `Advanced` or `NotAdvanced` checkpoint state; no equivalent shape is allowed without a new architecture decision.
- `src/Hexalith.EventStore.Admin.UI` remains the only EventStore UI host and the `eventstore-admin-ui` resource. It composes matching FrontComposer Shell and Contracts.UI `3.2.2` packages with Fluent UI V5, owns the `event-store-admin` module, and redirects legacy routes to canonical dashboard deep links.
- Consumer infrastructure removal requires an EventStore-owner-approved parity packet and exact identity evidence for the consumed EventStore source SHA, package versions and hashes, or deployed image digest; never compare a consumer repository SHA to the EventStore SHA.
- The approved replan preserves seven-epic order and MVP scope, rehomes platform provenance into Story 1.2, and leaves generated REST/Tenants provenance consumption in Epic 2.
- A split child inherits `done` only through an evidence crosswalk naming implementation, focused tests, review results, and external approval/exact SHA where applicable; otherwise it remains `review`.
- Tenants adoption requires maintainer approval, approved PR/commit evidence, exact Tenants SHA, repository boundary, source/package-mode validation, and an explicit disposition when approval is unavailable.
- Use `Hexalith.EventStore.slnx` for restore/build; run unit tests by project; keep package versions centralized; do not recurse submodules or modify submodule files without explicit approval.
- EventStore envelope identifiers use ULID-safe handling where required; `Guid.TryParse` is forbidden for `messageId`, `correlationId`, `aggregateId`, and `causationId`.
- UI-facing work must use FrontComposer and Blazor Fluent UI V5, remain support-safe, avoid theme redefinition, and keep detailed UX evidence in `ux.md`.
- AOT/trimming is not a target while reflection conventions remain load-bearing.

## Non-goals

- Do not reduce Phase 4 MVP scope as part of readiness recovery.
- Do not implement GDPR aggregate erasure/tombstoning, Admin interactive OIDC login, aggregate test kit, or REST generator hardening beyond approved Epic 2 proof scope; create backlog artifacts only.
- Do not move generated REST controllers into interactive UI hosts.
- Do not treat HTTP `202`, SignalR notification, or command acceptance as projection-confirmed UI success.
- Do not target AOT/trimming while reflection conventions remain load-bearing.
- Do not create an additional EventStore UI host or preserve duplicate legacy page implementations.
- Do not roll back implementation solely because planning identities are being restructured.

## Success signal

Implementation readiness can be re-run against this package and finds complete FR1-FR36/NFR1-NFR18 coverage, no later-epic prerequisite, no oversized active parent, and deterministic owner/evidence gates. The resulting plan preserves architecture AD-1 through AD-22, canonical UX, exact story migration history, and persisted-evidence validation.

## Assumptions

- No additional audience-facing artifact was requested; this SPEC plus companions are the deliverable.
