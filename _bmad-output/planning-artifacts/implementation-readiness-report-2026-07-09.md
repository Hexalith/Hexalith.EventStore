---
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
includedFiles:
  prd:
    - _bmad-output/planning-artifacts/prd.md
  architecture:
    - _bmad-output/planning-artifacts/architecture.md
  epics:
    - _bmad-output/planning-artifacts/epics.md
  ux:
    - _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/index.md
    - _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md
    - _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-07-09
**Project:** eventstore

## Step 1: Document Discovery

### PRD Files Found

**Whole Documents:**

- `_bmad-output/planning-artifacts/prd.md` (34,191 bytes, modified 2026-07-07 23:05)

**Sharded Documents:**

- No PRD `index.md` shard found.
- Related legacy/support folder found: `_bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/`

### Architecture Files Found

**Whole Documents:**

- `_bmad-output/planning-artifacts/architecture.md` (29,568 bytes, modified 2026-07-07 23:00)

**Sharded Documents:**

- No Architecture `index.md` shard found.
- Related review folder found: `_bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/`

### Epics & Stories Files Found

**Whole Documents:**

- `_bmad-output/planning-artifacts/epics.md` (109,012 bytes, modified 2026-07-07 23:04)

**Sharded Documents:**

- No Epics `index.md` shard found.

### UX Design Files Found

**Whole Documents:**

- None found.

**Sharded Documents:**

- Folder: `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/`
  - `index.md`
  - `DESIGN.md`
  - `EXPERIENCE.md`
  - review reports, mockups, imports, and validation artifacts

### Discovery Decision

- No blocking duplicate active whole+sharded document formats were found.
- Archived duplicate exports under `_bmad-output/planning-artifacts/archive/` are excluded from assessment.
- Assessment will use `prd.md`, `architecture.md`, `epics.md`, and the UX shard rooted at `ux-designs/ux-eventstore-2026-07-05/index.md`.

## Step 2: PRD Analysis

### Functional Requirements

FR1: Domain modules built on Hexalith.EventStore must be domain-centric, containing domain code such as aggregates, commands, events, projections, query handlers, validators, and contracts, while platform boilerplate is supplied by EventStore libraries.

FR2: The platform must provide a domain-service SDK with `AddEventStoreDomainService`, `UseEventStoreDomainService`, and `MapEventStoreDomainService` so a domain service host can be reduced to the canonical SDK host shape.

FR3: The domain-service SDK must expose the canonical DAPR-facing endpoints `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata`.

FR4: The platform must provide a domain query-handler seam using `IDomainQueryHandler`, discovery, dispatch, operational metadata reporting, gateway-side query-type capture, handler-aware routing to domain `/query` endpoints, and end-to-end `QueryResponseMetadata` propagation for freshness, projection version, ETag, served-at, degraded/warning state, and paging evidence, carrying an explicit query-response provenance classification (projection-backed, handler-computed, or unknown) that governs whether that evidence is projection-backed.

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

Total FRs: 35

### Non-Functional Requirements

NFR1: Security must fail closed for public, internal, domain-service, projection-notification, and admin surfaces; no endpoint may rely only on network posture or caller-supplied admin flags. The only anonymous exception is the health/liveness/readiness probe endpoints (`/health`, `/alive`, `/ready`), which are explicitly pinned `AllowAnonymous` and support-safe (AD-16); the fail-closed default is never weakened to reach probes.

NFR2: Tenant isolation must be preserved across state keys, actor IDs, topics, admin queries, generated REST APIs, SignalR groups, and deployment configuration.

NFR3: Production authentication must reject insecure symmetric-key mode unless explicitly break-glassed, require HTTPS metadata where appropriate, and pin accepted JWT algorithms.

NFR4: Committed configuration must not contain forgeable administrator signing keys, credentials, bearer tokens, decoded JWT payloads, or other operational secrets.

NFR5: SignalR detail metadata must remain bounded and metadata-only; framework logs must not expose metadata values above Debug level.

NFR6: Event delivery semantics are at-least-once and unordered; subscribers must deduplicate by `MessageId` and order only where domain semantics make `SequenceNumber` meaningful.

NFR7: Event persistence and command processing must avoid silent data loss: staged-state flushes, stale pipeline records, append races, and committed-but-unpublished events must be explicitly guarded or recovered.

NFR8: Snapshot and projection behavior must have a bounded cost model as streams grow, must avoid unnecessary full-stream replay when already current, and must expose projection freshness/version evidence through platform query metadata when callers depend on current/stale decisions; freshness/version evidence is authoritative only for query responses whose route provenance is projection-backed, and handler-computed or unknown-provenance responses must not be presented as current or stale.

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

Total NFRs: 18

### Additional Requirements

- The PRD is the authoritative Phase 4 FR/NFR baseline and exists to close the 2026-07-05 implementation-readiness blocker caused by missing standalone PRD traceability.
- `prd.md` owns FR/NFR truth and readiness traceability; `architecture.md` owns component, integration, topology, and decision-record gates; the UX artifact owns UI governance, user-flow evidence, and support-safe interaction rules; `epics.md` owns story slicing, sequencing, acceptance criteria, and implementation handoff.
- Full Phase 4 implementation should not resume until PRD, architecture, UX, story splits, and high-risk NFR traceability are reconciled and readiness is re-run.
- Implementation must preserve the Phase 4 product concerns listed in the PRD: fail-closed security, tenant isolation, package/public API stability, DAPR/Aspire parity, event correctness, release reproducibility, UI governance, bounded cost/evolution, and persisted evidence quality.
- Repository and build guardrails require `Hexalith.EventStore.slnx` for restore/build, per-project unit tests, centralized package versions, Debug source-reference and Release package-reference behavior, .NET SDK container support, and only root-declared submodules under `references/`.
- Identity and authorization guardrails forbid `Guid.TryParse` for `messageId`, `correlationId`, `aggregateId`, and `causationId`; require tenant access validation before resource existence disclosure; and require app-layer credentials for internal, domain-service, projection-notification, and admin-computation endpoints.
- UI governance requires FrontComposer and Blazor Fluent UI V5, no theme primitive redefinition, support-safe states, projection-confirmed success for Tenants UI, and honest unavailable Admin UI operations.
- MVP scope includes all seven Phase 4 epics in `epics.md`; out-of-scope work includes GDPR aggregate erasure/tombstoning implementation, Admin interactive OIDC login implementation, aggregate test kit implementation, REST generator hardening beyond approved Epic 2 proof scope, AOT/trimming support, generated REST controllers in interactive UI hosts, and treating `202`/SignalR/command acceptance as projection-confirmed success.
- Success metrics require FR1-FR35 epic/story coverage, high-risk NFR coverage for NFR1-NFR4, NFR7, NFR10-NFR11, and NFR14-NFR17, split or explicitly accepted oversized stories, and required architecture/UX artifacts.
- Required follow-on readiness work includes coordinated-slice gates for Stories 1.3, 1.6, 2.4, 3.7, 5.6, 7.2, 7.3, and 7.4; concrete Story 5.2 request-size limits; spec output gates for Stories 6.1, 6.3, and 6.5 before their dependent implementation stories; and Story 7.5 backlog deliverables.

### PRD Completeness Assessment

The PRD is complete enough for traceability validation: it defines the product purpose, user/jobs context, glossary, product concerns, 35 explicit functional requirements, 18 explicit non-functional requirements, constraints, MVP scope, success metrics, FR-to-epic coverage, high-risk NFR story coverage, follow-on readiness gates, open questions, and assumptions. The main readiness risk is downstream consistency: the PRD declares traceability targets that must be proven against `epics.md`, architecture, and UX artifacts in later workflow steps.

## Step 3: Epic Coverage Validation

### Epic FR Coverage Extracted

FR1: Covered in Epic 1, Story 1.1.
FR2: Covered in Epic 1, Story 1.1.
FR3: Covered in Epic 1, Story 1.1.
FR4: Covered in Epic 1, Story 1.2, with route-provenance hardening in Story 4.7.
FR5: Covered in Epic 1, Story 1.3.
FR6: Covered in Epic 1, Story 1.3.
FR7: Covered in Epic 1, Story 1.4.
FR8: Covered in Epic 1, Story 1.5.
FR9: Covered in Epic 1, Story 1.6.
FR10: Covered in Epic 1, Story 1.7.
FR11: Covered in Epic 2, Story 2.1.
FR12: Covered in Epic 2, Story 2.2, with generated command-status hardening in Story 2.6.
FR13: Covered in Epic 2, Story 2.3, with outbound DAPR routing-header hardening in Story 2.7.
FR14: Covered in Epic 2, Story 2.3, with outbound DAPR routing-header hardening in Story 2.7.
FR15: Covered in Epic 2, Story 2.4, with route-provenance hardening in Story 4.7.
FR16: Covered in Epic 2, Story 2.5.
FR17: Covered in Epic 3, Story 3.1, with companion smoke-preflight support in Story 3.8.
FR18: Covered in Epic 3, Story 3.2.
FR19: Covered in Epic 3, Story 3.3.
FR20: Covered in Epic 3, Story 3.4.
FR21: Covered in Epic 3, Story 3.5.
FR22: Covered in Epic 3, Story 3.6.
FR23: Covered in Epic 4, Story 4.1.
FR24: Covered in Epic 4, Story 4.6.
FR25: Covered in Epic 3, Story 3.7.
FR26: Covered in Epic 5, Stories 5.1, 5.2, 5.3, and 5.4.
FR27: Covered in Epic 4, Story 4.2.
FR28: Covered in Epic 5, Story 5.5.
FR29: Covered in Epic 4, Story 4.3.
FR30: Covered in Epic 4, Story 4.4.
FR31: Covered in Epic 4, Story 4.5.
FR32: Covered in Epic 5, Story 5.6.
FR33: Covered in Epic 6, Stories 6.1 through 6.6.
FR34: Covered in Epic 7, Stories 7.1 through 7.4, with route-provenance hardening in Story 4.7.
FR35: Covered in Epic 7, Story 7.5.

Total FRs in epics: 35

### Coverage Matrix

| FR Number | PRD Requirement | Epic Coverage | Status |
| --- | --- | --- | --- |
| FR1 | Domain modules built on Hexalith.EventStore must be domain-centric, containing domain code such as aggregates, commands, events, projections, query handlers, validators, and contracts, while platform boilerplate is supplied by EventStore libraries. | Epic 1, Story 1.1 | Covered |
| FR2 | The platform must provide a domain-service SDK with `AddEventStoreDomainService`, `UseEventStoreDomainService`, and `MapEventStoreDomainService` so a domain service host can be reduced to the canonical SDK host shape. | Epic 1, Story 1.1 | Covered |
| FR3 | The domain-service SDK must expose the canonical DAPR-facing endpoints `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata`. | Epic 1, Story 1.1 | Covered |
| FR4 | The platform must provide a domain query-handler seam using `IDomainQueryHandler`, discovery, dispatch, operational metadata reporting, gateway-side query-type capture, handler-aware routing to domain `/query` endpoints, and end-to-end `QueryResponseMetadata` propagation for freshness, projection version, ETag, served-at, degraded/warning state, and paging evidence, carrying an explicit query-response provenance classification that governs whether evidence is projection-backed. | Epic 1, Story 1.2; Epic 4, Story 4.7 | Covered |
| FR5 | The platform must provide a generic persisted read-model store and write policy with optimistic-concurrency merge-on-write, multi-key/index support, DAPR implementation, and in-memory testing support. | Epic 1, Story 1.3 | Covered |
| FR6 | The platform must provide a reusable DataProtection-backed query cursor codec with scope validation, payload limits, tamper/key-rotation handling, and caller-supplied purpose isolation. | Epic 1, Story 1.3 | Covered |
| FR7 | The platform must provide a generic projection-handler seam for `/project` dispatch and a generic domain-event subscription/consumer pipeline with deduplication and endpoint mapping. | Epic 1, Story 1.4 | Covered |
| FR8 | The platform must provide Aspire, telemetry, and health-check extensions for domain modules, including `AddEventStoreDomainModule`, convention telemetry, and DAPR state-store health checks. | Epic 1, Story 1.5 | Covered |
| FR9 | The Sample domain and Tenants domain must adopt platform SDK seams so duplicated request routers, projection actors, cursor codecs, state-store plumbing, telemetry, health checks, and per-domain Aspire wiring are removed or reduced to domain-specific logic. | Epic 1, Story 1.6 | Covered |
| FR10 | The EventStore package set must include the domain-service and service-default packages as publishable packages, and release packaging must publish only the manifest-governed EventStore package set. | Epic 1, Story 1.7 | Covered |
| FR11 | The platform must provide a REST API source-generator contract seam with `ICommandContract`, `IQueryContract`, optional `RestRouteAttribute`, and assembly-level `RestApiAttribute`. | Epic 2, Story 2.1 | Covered |
| FR12 | The REST API generator must discover command/query contracts and emit typed, OpenAPI-visible controllers that delegate to `IEventStoreGatewayClient`, forward canonical query metadata headers when supplied by the gateway, and include tests covering discovery, routing conventions, diagnostics, generated output, query metadata headers, `304`, and safe problem-detail behavior. | Epic 2, Story 2.2; Epic 2, Story 2.6 | Covered |
| FR13 | Generated REST controllers must live in dedicated external-facing API hosts, not interactive UI hosts; interactive UI hosts must consume EventStore client libraries directly. | Epic 2, Story 2.3; Epic 2, Story 2.7 | Covered |
| FR14 | The Sample proof must introduce a contracts-only Sample contracts library and an external Sample API host, move shared contracts there, and prove generated query and command controllers through that external API host. | Epic 2, Story 2.3; Epic 2, Story 2.7 | Covered |
| FR15 | The Tenants proof must move generated Tenants controllers to an external Tenants API host, while Tenants UI consumes client libraries and no longer hosts hand-written per-message controllers; any Tenants freshness, projection-version, ETag, or paging evidence shown by generated APIs or UI must come from the platform query metadata path. | Epic 2, Story 2.4; Epic 4, Story 4.7 | Covered |
| FR16 | The projection-changed transport must add an additive metadata-rich detail path with optional group scope, bounded metadata, scoped SignalR groups, DAPR notification support where needed, and preserved signal-only compatibility. | Epic 2, Story 2.5 | Covered |
| FR17 | Live DAPR sidecar tests must be tagged and removed from the per-push release gate, then run in a dedicated integration workflow with sidecar warm-up and readiness retry. | Epic 3, Story 3.1; companion Story 3.8 | Covered |
| FR18 | `DaprETagService` must allow an overridable actor request timeout while preserving the production default. | Epic 3, Story 3.2 | Covered |
| FR19 | Root-declared Git submodules must live under `references/`, and solution, project, documentation, Aspire metadata, and LLM instruction paths must resolve through the `references/` layout. | Epic 3, Story 3.3 | Covered |
| FR20 | The Aspire Keycloak resource must be named `security` while preserving Keycloak as the implementation technology and updating fixtures/resource lookups accordingly. | Epic 3, Story 3.4 | Covered |
| FR21 | Cross-repo Hexalith library dependencies must use Debug source project references when explicitly enabled and Release package references by default, with package versions pinned centrally. | Epic 3, Story 3.5 | Covered |
| FR22 | Release restore, build, test, pack, and semantic-release commands must assert package-reference mode and avoid packaging submodule projects. | Epic 3, Story 3.6 | Covered |
| FR23 | Persisted events must receive non-zero, actor-allocated global positions; CloudEvent ids must use the event `MessageId`; duplicate command replies must preserve the original command result fields. | Epic 4, Story 4.1 | Covered |
| FR24 | The global-position allocation strategy must be renegotiated toward sharding per tenant or domain, and the frozen global-ordering spec must be updated before implementation. | Epic 4, Story 4.6 | Covered |
| FR25 | EventStore workflows must use shared Hexalith.Builds security gates through `@main`, keep third-party actions SHA-pinned through shared workflows, and define NuGet package publish scope in `tools/release-packages.json`. | Epic 3, Story 3.7 | Covered |
| FR26 | Phase 0 architecture remediation must close immediate safe fixes: clear staged state on infrastructure failure, protect anonymous admin endpoints, strip committed admin secrets, enforce production auth guards, add tenant-filter parity, gate admin Swagger, require destructive CLI confirmation, use ULID-safe admin correlation middleware, and correct stale test-baseline documentation. | Epic 5, Stories 5.1, 5.2, 5.3, 5.4 | Covered |
| FR27 | Pipeline correctness remediation must make resume/idempotency matching use `MessageId`, `CausationId`, and `CommandType`; key command status/archive by message id; preserve retryability for transient failures; and validate tenant access before idempotency reads. | Epic 4, Story 4.2 | Covered |
| FR28 | Trust-boundary remediation must require app-layer credentials for internal, domain-service, projection-notification, and admin-computation endpoints, and must remove trust in wire-asserted administrator flags. | Epic 5, Story 5.5 | Covered |
| FR29 | Replay and dispatch remediation must make event apply-method resolution boundary-safe and ambiguity-detecting, and must use one shared `JsonSerializerOptions` path for command, rehydrate, project, and pub/sub payload serialization. | Epic 4, Story 4.3 | Covered |
| FR30 | Crash recovery remediation must detect events committed but not published and complete publication or drain/recover them without requiring resubmission with the same correlation id. | Epic 4, Story 4.4 | Covered |
| FR31 | Append durability remediation must start with a live-sidecar two-writer race test and DAPR conflict-exception spike before choosing an optimistic-concurrency fencing design. | Epic 4, Story 4.5 | Covered |
| FR32 | Runtime topology remediation must make the AppHost-loaded DAPR pub/sub, ACL, and key-prefix posture match the posture asserted by tests and production deploy templates. | Epic 5, Story 5.6 | Covered |
| FR33 | Cost and evolution remediation must introduce folded snapshots, reduce projection replay cost, add projection sequence guards, support event schema versioning/upcasting, validate event metadata identity components, and add cancellation-token seams to published processing/query/projection interfaces. | Epic 6, Stories 6.1, 6.2, 6.3, 6.4, 6.5, 6.6 | Covered |
| FR34 | Delivery, admin, and deployment remediation must document at-least-once unordered delivery, add poison/dead-letter handling, bound in-memory deduplication, normalize admin claims, audit every state-mutating admin action, hide deferred admin operations, add secret-store-backed configuration, add readiness/app-health checks, and restore meaningful IntegrationTests CI coverage. | Epic 7, Stories 7.1, 7.2, 7.3, 7.4; Epic 4, Story 4.7 | Covered |
| FR35 | Backlog capabilities must be tracked for GDPR aggregate erasure/tombstoning, Admin interactive OIDC login, an aggregate test kit, and REST generator hardening. | Epic 7, Story 7.5 | Covered |

### Missing Requirements

No missing PRD functional requirements were found. The epics document claims explicit coverage for every PRD FR1-FR35.

### FRs In Epics But Not In PRD

No extra FR identifiers were found in the epics document. Story 3.8 is explicitly marked as having no direct FR coverage and is positioned as retro-driven developer tooling that safeguards FR17 and FR34 evidence quality.

### Coverage Statistics

- Total PRD FRs: 35
- FRs covered in epics: 35
- Coverage percentage: 100%

## Step 4: UX Alignment Assessment

### UX Document Status

Found as a sharded UX artifact:

- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/index.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`
- Supporting review and validation reports in the same folder.

No top-level `_bmad-output/planning-artifacts/ux.md` file exists. The UX folder index states that the folder is the canonical UX source and that `DESIGN.md` and `EXPERIENCE.md` win on conflict with mockups, screenshots, validation artifacts, or legacy `Admin.UI` behavior.

### UX To PRD Alignment

- Aligned: PRD requirements for interactive UI hosts consuming client libraries are reflected in the UX foundation, source traceability, Sample flow, and Tenants flow.
- Aligned: PRD support-safe state rules are reflected in UX support-safe operations, state patterns, accessibility floor, and mutation flows.
- Aligned: PRD projection-confirmed success rules are reflected in command lifecycle, projection freshness, Sample accepted-submission, and Tenants projection-confirmed update flows.
- Aligned: PRD Admin UX honesty requirements are reflected in the Deferred & Backlog tab, deferred operation placeholder, unavailable operation visibility policy, and server `501` behavior.
- Aligned: PRD FrontComposer and Blazor Fluent UI V5 governance is reflected in the visual design and experience component patterns.
- Aligned: PRD accessibility/localization concerns are reflected in WCAG 2.2 AA targets, keyboard/focus rules, live-region priority, stable selectors, and resource-backed string guidance.

### UX To Architecture Alignment

- Aligned: AD-4 is supported by UX rules that generated REST controllers stay out of interactive UI hosts and UI hosts consume client libraries.
- Aligned: AD-8 is supported by UX rules that SignalR is only a freshness nudge and command acceptance is not projection-confirmed success.
- Aligned: AD-10 and AD-16 are supported by fail-closed UI states, support-safe rendering, role-gated mutations, and explicit probe/support-safe posture.
- Aligned: AD-14 and AD-15 are supported by projection freshness indicators that render current/stale only for projection-backed route provenance; handler-computed or unknown provenance renders unknown.
- Aligned: Architecture component boundaries support UX expectations for dashboard tabs, external API hosts, Admin surfaces, SignalR freshness, query metadata, support-safe errors, and deferred operations.

### Alignment Issues

- The canonical UX handoff path is inconsistent. PRD, architecture, and epics refer to `_bmad-output/planning-artifacts/ux.md`, but the actual canonical UX source is `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/index.md` plus `DESIGN.md` and `EXPERIENCE.md`. This can confuse readiness tooling or downstream story authors unless a top-level handoff file is created or all references are updated.
- UX validation artifacts include older findings from 2026-07-05 that are partially stale. Current UX spines now include final status, promoted visual references, Sample and Tenants flows, source traceability, tab-state coverage, mobile action policy, and live-region priority. Downstream reviewers should treat `DESIGN.md` and `EXPERIENCE.md` as canonical over older review findings, per the folder index.

### Warnings

- Warning: Create `_bmad-output/planning-artifacts/ux.md` as a canonical handoff index, or update PRD, architecture, and epics references to the current UX folder index before implementation story handoff.
- Warning: The visual design relies on Fluent role/token names rather than concrete CSS variable names or resolved light/dark token pairs. This appears intentional for FrontComposer/Fluent inheritance, but implementation stories should verify Fluent V5 conformance rather than copying legacy/raw CSS.

## Step 5: Epic Quality Review

### Review Scope

Reviewed 7 epics and 46 stories in `_bmad-output/planning-artifacts/epics.md` against epic/story quality standards: user value, independence, no forward dependencies, story sizing, testable acceptance criteria, brownfield integration fit, and traceability.

### Overall Quality Assessment

The epics are mostly valid for a brownfield developer-platform and operations-hardening program. They identify real users such as domain authors, external API host developers, release maintainers, operators, administrators, platform architects, and quality maintainers. Most stories use user-story format and Given/When/Then acceptance criteria.

The plan is not fully implementation-ready because it still relies on coordinated-slice gates, contains one material forward dependency, and includes several stories that are planning/tooling artifacts rather than independently shippable product increments. These issues do not invalidate FR coverage, but they do affect execution readiness.

### Critical Violations

#### CR-1: Forward Dependency From Epic 2 To Epic 4 Query Provenance

**Affected stories:** Story 2.2, Story 2.4, Story 4.7.

Epic 2 generated REST and Tenants UI/API stories depend on query metadata and provenance behavior that Story 4.7 later owns in Epic 4. The epics document states that no new UI or generated-API story rendering current/stale state may proceed until it cites AD-15 or avoids projection-backed claims, while Story 4.7 owns the platform contract and route-aware gateway enforcement.

**Why this violates best practice:** Epic 2 cannot be independently complete if its generated API/UI evidence must be reconciled by a later Epic 4 story. This is a forward dependency.

**Recommendation:** Move the query-response provenance platform contract and route-aware gateway enforcement into the earliest owning story before Epic 2 consumers, most likely Story 1.2 or Story 2.2. Leave Story 4.7 only as a follow-up guardrail/retro hardening story if needed.

#### CR-2: Coordinated-Slice Stories Are Still Too Large Unless Split Or Carried Forward Exactly

**Affected stories:** 1.3, 1.6, 2.4, 3.7, 5.6, 7.2, 7.3, 7.4.

The epics document explicitly lists these stories under "Coordinated-Slice Gates", with required slices, owners, review boundaries, and validation commands. This is good mitigation, but the current story list still contains oversized stories unless implementation story files split them or carry the coordinated-slice gate unchanged.

**Why this violates best practice:** Stories must be independently completable and reviewable. A coordinated slice may be acceptable only when the implementation handoff preserves the named owner, review boundary, and validation commands; otherwise these are epic-sized stories.

**Recommendation:** Before Phase 4 broad implementation starts, either split each affected story into the named slices or require each implementation story file to include the coordinated boundary table verbatim as a readiness gate.

### Major Issues

#### MJ-1: Story 1.3 Still Contains Tenants Adoption Despite The Slice Gate Moving Tenants Adoption To Story 1.6

The coordinated-slice gate says Story 1.3 owns read-model store/write policy, testing fake/conflict semantics, and protected cursor codec, while Tenants adoption moves to Story 1.6. Story 1.3 acceptance criteria still require adopting the read-model and cursor seams in a non-trivial domain and migrating existing Tenants-style read-model and cursor behavior.

**Impact:** The story boundary is internally inconsistent and may pull Tenants adoption back into Story 1.3.

**Recommendation:** Remove Tenants adoption acceptance criteria from Story 1.3, or explicitly rewrite it as a test-fixture/reference-domain proof that does not touch the Tenants submodule. Keep Tenants production adoption in Story 1.6.

#### MJ-2: Story 4.7 Contains A Submodule Approval Dependency Inside Acceptance Criteria

Story 4.7 says the Tenants producer aliasing fix is a submodule edit requiring explicit maintainer approval before it lands. That makes part of the story dependent on external approval/state.

**Impact:** The EventStore platform provenance contract may be independently completable, but the Tenants conformance fix is not guaranteed to be completable in the same story.

**Recommendation:** Split Story 4.7 into an EventStore-owned platform provenance story and a Tenants follow-up story requiring explicit maintainer approval, or mark the Tenants change as non-blocking follow-up evidence.

#### MJ-3: Story 3.8 Has No Direct FR Coverage

Story 3.8 is useful developer tooling, but it declares "Requirements covered: none directly" and is a companion to Story 3.1.

**Impact:** It weakens traceability discipline in an otherwise FR-mapped epic plan.

**Recommendation:** Either map Story 3.8 explicitly to FR17/FR34 and NFR16 as a validation-enablement story, or move it to tooling/backlog outside the implementation epic list.

#### MJ-4: Spec-Only Stories In Epic 6 Are Planning Gates, Not Runtime Product Increments

Stories 6.1, 6.3, and 6.5 produce frozen specs and approval evidence before implementation stories 6.2, 6.4, and 6.6. This is a sound risk-control mechanism, but these are planning/architecture gates rather than independently shippable runtime behavior.

**Impact:** If tracked as implementation stories, velocity/readiness can be misleading.

**Recommendation:** Keep them, but classify them explicitly as architecture gate stories or readiness artifacts, and do not count their completion as runtime implementation progress. Their dependent implementation stories must check the approved artifacts before coding starts.

#### MJ-5: Story 7.5 Is A Backlog Artifact Story, Not An Implementation Story

Story 7.5 explicitly says it does not authorize implementation and completes by producing backlog artifacts.

**Impact:** This is valid for FR35, but it should not be confused with implementation readiness for GDPR erasure, Admin OIDC, aggregate test kit, or REST generator hardening.

**Recommendation:** Keep Story 7.5 as a planning/backlog artifact with exact outputs, but label it outside normal implementation-story flow or mark it as planning-only in sprint status.

### Minor Concerns

#### MN-1: Several Epic Titles Are Technical, But User Outcomes Are Present

Epics such as "Release And Repository Reliability", "Event Correctness And Recovery", and "Bounded Cost And Event Evolution" are technical in title, but their descriptions identify maintainers, operators, consumers, and platform users as beneficiaries. This is acceptable for a developer-platform project, but implementation story files should retain the user/outcome phrasing.

#### MN-2: Some Post-Retro Placement Notes Mix Backlog Status Into Story Definitions

Stories 2.6, 2.7, and 3.8 include placement/status notes such as post-retro hardening, reopened epic status, or re-homed lineage. These notes are useful audit context, but can blur whether the story is ready, backlog, or in-progress.

**Recommendation:** Normalize status in a separate story metadata field when implementation stories are generated.

#### MN-3: `ux.md` Is Referenced As The UX Contract But The Actual Artifact Is Sharded

The epics document still references `_bmad-output/planning-artifacts/ux.md` as the UX design contract. The actual canonical artifact is the UX folder index. This is already recorded in Step 4 and should be fixed before story handoff.

### Epic Compliance Checklist

| Epic | User Value | Independent | Story Sizing | No Forward Dependencies | Acceptance Criteria | Traceability | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Epic 1 | Pass | Pass | Needs gate for 1.3 and 1.6 | Pass | Pass | Pass | Story 1.3 boundary conflict with Tenants adoption. |
| Epic 2 | Pass | Fail | Needs gate for 2.4 | Fail | Pass | Pass | Depends on later Story 4.7 query provenance unless moved earlier. |
| Epic 3 | Pass | Pass | Needs gate for 3.7 | Pass | Pass | Mixed | Story 3.8 has no direct FR coverage. |
| Epic 4 | Pass | Mostly pass | Mostly pass | Pass | Pass | Pass | Story 4.7 should split EventStore-owned and Tenants-submodule work. |
| Epic 5 | Pass | Pass | Needs gate for 5.6 | Pass | Pass | Pass | Security stories are testable and brownfield-aware. |
| Epic 6 | Pass with caveat | Pass | Planning gates mixed with implementation | Pass | Pass | Pass | Spec-only stories should be classified as architecture gates. |
| Epic 7 | Pass | Pass | Needs gates for 7.2, 7.3, 7.4 | Pass | Pass | Pass | Story 7.5 is planning/backlog artifact, not implementation. |

### Special Implementation Checks

- Starter template: No issue. The epics document states no greenfield starter template is mandated and `dotnet new hexalith-domain` is optional/deferred.
- Greenfield vs brownfield: Brownfield indicators are present. Stories consistently reference existing EventStore, Sample, Tenants, Admin, DAPR/Aspire, release, and CI surfaces.
- Database/entity timing: Not applicable as a relational database/table plan. DAPR state/read-model creation is mostly scoped to the stories that first need the state seams.
- Acceptance criteria format: Generally strong. Most stories use Given/When/Then and include failure, validation, security, or evidence paths. The main defects are story boundary and dependency issues, not missing BDD structure.

## Summary and Recommendations

### Overall Readiness Status

**NOT READY** for broad Phase 4 implementation.

The planning set has recovered the biggest original blocker: PRD requirements exist and all FR1-FR35 are covered in the epics document. However, implementation should not proceed broadly until the critical story-ordering and story-sizing defects are corrected. Targeted remediation of the planning artifacts can proceed immediately.

### Critical Issues Requiring Immediate Action

1. **Forward dependency:** Epic 2 generated API/UI work depends on query-response provenance behavior owned later by Story 4.7. Move the provenance contract earlier or split it so Epic 2 has no dependency on Epic 4.
2. **Oversized coordinated-slice stories:** Stories 1.3, 1.6, 2.4, 3.7, 5.6, 7.2, 7.3, and 7.4 are not independently implementation-ready unless split or carried forward exactly with their coordinated-slice gates.
3. **Story boundary conflict:** Story 1.3 still includes Tenants adoption despite the gate moving Tenants adoption to Story 1.6.
4. **UX handoff path mismatch:** PRD, architecture, and epics reference `_bmad-output/planning-artifacts/ux.md`, but the actual canonical UX source is the sharded UX folder index.

### Recommended Next Steps

1. Move AD-15/query-response provenance enforcement into the earliest prerequisite story before generated REST/UI consumers, then adjust Story 4.7 to remove the forward dependency.
2. Split or explicitly re-author the eight coordinated-slice stories before implementation story files are generated.
3. Rewrite Story 1.3 to remove Tenants adoption work, keeping Tenants migration in Story 1.6.
4. Split Story 4.7 into EventStore-owned platform provenance work and a separate Tenants-submodule follow-up requiring maintainer approval.
5. Create `_bmad-output/planning-artifacts/ux.md` as a canonical handoff index or update PRD, architecture, and epics to reference the UX folder index directly.
6. Reclassify Story 3.8, Epic 6 spec stories, and Story 7.5 as validation/planning gates or map them explicitly to FR/NFR coverage so implementation tracking remains honest.

### Issue Count

This assessment identified **12 issues requiring attention** across **4 categories**:

- 2 critical implementation-readiness violations.
- 5 major story-quality and classification issues.
- 3 minor cleanup concerns.
- 2 UX/document packaging warnings.

### Final Note

The artifacts are materially improved and requirements traceability is strong: PRD extraction found 35 FRs and 18 NFRs, and epic coverage is 100% for PRD FRs. The remaining work is not about missing product scope; it is about making the implementation plan executable without hidden forward dependencies, oversized stories, or artifact path ambiguity.

**Assessor:** Codex using `bmad-check-implementation-readiness`
**Assessment date:** 2026-07-09
