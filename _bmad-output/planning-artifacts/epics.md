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
---

# eventstore - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for eventstore, decomposing the formal PRD, architecture, UX handoff, and approved sprint change proposals into implementable stories.

The current Phase 4 planning baseline is `_bmad-output/planning-artifacts/prd.md`, `_bmad-output/planning-artifacts/architecture.md`, `_bmad-output/planning-artifacts/ux.md`, and the approved sprint change proposals in `_bmad-output/planning-artifacts`. The PRD owns FR/NFR truth, the architecture artifact owns implementation invariants and decision gates, the UX handoff owns UI governance and journeys, and this epics document owns implementation slicing and story acceptance criteria.

## Implementation Readiness Execution Gates

The 2026-07-05 implementation readiness assessment found complete FR traceability but identified story-quality gates that must be closed before broad Phase 4 implementation starts.

### Query Metadata Sequencing Gate

The platform-owned query metadata propagation contract must be implemented by the earliest stories that depend on it, not by a later Epic 7 story.

- Story 1.2 owns platform result metadata propagation, gateway merge rules, freshness policy enforcement, and typed client metadata exposure.
- Story 1.3 owns authoritative paging metadata, cursor opacity, invalid cursor handling, and read-model/end-state evidence for paging.
- Story 2.2 owns generated REST query metadata headers, `304` behavior, and safe problem-detail behavior.
- Story 2.4 owns Tenants API/UI proof against the real platform query metadata path.
- Story 7.5 remains backlog-artifact work only.

Story 7.6 has been deleted; its acceptance criteria are redistributed into the owning earlier stories above.

### Query Response Provenance Gate

Query-response provenance is governed by architecture invariant AD-15. Story 2.8 owns the EventStore platform contract and route-aware gateway enforcement before generated REST or UI consumers may claim current/stale projection evidence. No UI or generated-API story that renders current/stale state or projection version may proceed unless Story 2.8 is done, or the story explicitly renders handler-computed and unknown provenance as Unknown and avoids projection-backed evidence claims.

Story 4.7 no longer owns the EventStore platform prerequisite. Any Tenants producer aliasing fix that requires submodule maintainer approval is tracked as a non-blocking Tenants follow-up; until approved, affected Tenants routes must render Unknown rather than Current/Stale unless they source genuine projection-backed freshness.

### Coordinated-Slice Gates For Oversized Stories

The stories below may proceed in one of two ways:

- Split the story into the named implementation slices before creating implementation story files.
- Keep the story as a coordinated slice only if the named owner, review boundary, and validation commands are carried into the implementation story file.

Implementation handoff gate: a story file for any row in this table is not ready-for-dev or ready-for-review unless its active content includes the row's required slices/coordinated boundary, owner/review boundary, and validation commands. A superseded-scope note is insufficient if active acceptance criteria or tasks still instruct the abandoned design.

| Story | Required slices or coordinated boundary | Owner | Required validation commands |
| --- | --- | --- | --- |
| 1.3 | Read-model store/write policy; testing fake/conflict semantics; protected cursor codec. Tenants adoption moves to 1.6. | Amelia (Developer) with Murat (Test Architect) review | `dotnet build Hexalith.EventStore.slnx --configuration Release`; `dotnet test tests/Hexalith.EventStore.Client.Tests/`; `dotnet test tests/Hexalith.EventStore.Testing.Tests/`; `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` |
| 1.6 | Sample adoption; Tenants query/read-model adoption; Tenants projection/event-consumer adoption; governance guardrails. | Amelia (Developer) with Winston (Architect) review | `dotnet build Hexalith.EventStore.slnx --configuration Release`; `dotnet test tests/Hexalith.EventStore.Sample.Tests/`; `dotnet test tests/Hexalith.EventStore.DomainService.Tests/`; `dotnet test references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/`; `dotnet test references/Hexalith.Tenants/tests/Hexalith.Tenants.Client.Tests/` |
| 2.4 | Tenants REST contract metadata; external Tenants API host; Tenants UI client-library alignment; compatibility validation. | Amelia (Developer) with Sally (UX Designer) review for UI evidence | `dotnet build Hexalith.EventStore.slnx --configuration Release`; `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/`; `dotnet test references/Hexalith.Tenants/tests/Hexalith.Tenants.Contracts.Tests/`; `dotnet test references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/` |
| 3.7 | Shared workflow caller migration; workflow reference/cache validation; supply-chain publishing backlog. | Paige (Technical Writer) and Amelia (Developer) | `dotnet build Hexalith.EventStore.slnx --configuration Release`; `rg -n "Hexalith.Builds/.+@" .github references -g "*.yml" -g "*.yaml"`; `rg -n "NUGET_API_KEY|trusted publishing|attestation|SBOM|provenance" docs .github _bmad-output -g "*.md" -g "*.yml" -g "*.yaml"` |
| 5.6 | AppHost component loading parity; production DAPR component/ACL parity; topology drift tests; deployment documentation alignment. | Winston (Architect) with Amelia (Developer) | `dotnet build Hexalith.EventStore.slnx --configuration Release`; `dotnet test tests/Hexalith.EventStore.AppHost.Tests/`; `dotnet test tests/Hexalith.EventStore.IntegrationTests/` in the dedicated integration lane |
| 7.2 | Claims normalization; audit logging; honest deferred admin operations; shared typed-client reduction. | Amelia (Developer) with Sally (UX Designer) review for UI honesty | `dotnet build Hexalith.EventStore.slnx --configuration Release`; `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/`; `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/`; `dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests/` |
| 7.3 | Secret-store deployment configuration; readiness/app-health checks; DAPR resiliency policy; immutable image tags. | Winston (Architect) with Paige (Technical Writer) | `dotnet build Hexalith.EventStore.slnx --configuration Release`; `dotnet test tests/Hexalith.EventStore.AppHost.Tests/`; `rg -n "secretKeyRef|app-health|resiliency|immutable|git-SHA" deploy docs _bmad-output src/Hexalith.EventStore.AppHost/DaprComponents samples/dapr-components -g "*.yaml" -g "*.yml" -g "*.md"` |
| 7.4 | Integration CI lane recovery; persisted state evidence assertions; fake/integration reclassification; perf/advisory workflow hygiene. | Murat (Test Architect) with Amelia (Developer) | `dotnet build Hexalith.EventStore.slnx --configuration Release`; `dotnet test tests/Hexalith.EventStore.Testing.Integration.Tests/`; `dotnet test tests/Hexalith.EventStore.IntegrationTests/` in the dedicated integration lane |

### Spec-Gated Story Outputs

Epic 6 implementation stories are blocked until their paired specs exist and carry approval evidence.

| Spec story | Required output path | Dependent implementation story |
| --- | --- | --- |
| 6.1 | `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` | 6.2 |
| 6.3 | `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md` | 6.4 |
| 6.5 | `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` | 6.6 |

Approval evidence must include approver, date, accepted scope, rejected alternatives, open decisions, and explicit authorization for the dependent implementation story to start.

### Backlog Artifact Outputs

Story 7.5 is a planning/backlog artifact story, not an implementation story. It completes only when these exact artifacts exist:

- `_bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md`
- `_bmad-output/planning-artifacts/backlog/iam-1-admin-oidc-login.md`
- `_bmad-output/planning-artifacts/backlog/kit-1-aggregate-test-kit.md`
- `_bmad-output/planning-artifacts/backlog/rest-generator-hardening.md`

## Requirements Inventory

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

### NonFunctional Requirements

NFR1: Security must fail closed for public, internal, domain-service, projection-notification, and admin surfaces; no endpoint may rely only on network posture or caller-supplied admin flags.

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

NFR15: Admin UX must not present deferred backup, restore, import, compaction, or other unavailable operations as functional; unavailable operations must be hidden/disabled or return 501.

NFR16: Integration and higher-tier tests must assert persisted state-store/read-model/end-state evidence, not only HTTP status codes or mock call counts.

NFR17: Operational hardening must support secret stores, DAPR app health checks, readiness-tagged health checks, resiliency targets, immutable image tags, and documented crypto-shred boundaries.

NFR18: AOT/trimming is explicitly not a target while reflection conventions remain load-bearing, and that constraint must be documented.

### Additional Requirements

- Standalone PRD, architecture, and UX design contracts are present under `_bmad-output/planning-artifacts`; this epics document must stay aligned with those artifacts and the approved sprint change proposals.
- No greenfield starter template is mandated. A `dotnet new hexalith-domain` template is mentioned as an optional/deferred platform capability, not a required starting point.
- Use `Hexalith.EventStore.slnx` only for restore/build; do not introduce or use `.sln` files.
- Run unit tests per project; do not make solution-level `dotnet test` the default EventStore validation path.
- Keep `.csproj` package references versionless; all package versions must remain in `Directory.Packages.props`.
- Preserve the Debug-source/Release-package dependency policy through `UseHexalithProjectReferences`; rerun restore after changing dependency mode.
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
- GDPR erasure, Admin OIDC login, and aggregate test-kit work are backlog epics and must not be hidden inside unrelated remediation stories.

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

FR21: Epic 3 - Debug source references and Release package references.

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

## Epic List

### Epic 1: Domain Author Self-Service Platform

**Epic type:** Platform Capability

Domain authors can build and run EventStore-backed domain modules with minimal boilerplate and reusable platform seams, while the platform owns hosting, query, projection, read-model, cursor, telemetry, health, Aspire, and packaging concerns.

**FRs covered:** FR1, FR2, FR3, FR4, FR5, FR6, FR7, FR8, FR9, FR10

### Epic 2: External Integration Surfaces

**Epic type:** External Integration Capability

External application developers can consume typed, generated REST APIs, while interactive UI hosts use client libraries directly and real-time projection notifications remain scoped, metadata-only, and backward compatible.

**FRs covered:** FR11, FR12, FR13, FR14, FR15, FR16

### Epic 3: Release And Repository Reliability

**Epic type:** Release Reliability

Maintainers can release reproducibly with correct package/reference mode, aligned submodule and Aspire resource layout, deterministic release gates, live-sidecar integration coverage, shared supply-chain workflows, and manifest-governed package output.

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
**Then** `/process`, `/replay-state`, `/project`, and `/admin/operational-index-metadata` are available through SDK-owned mappings
**And** route mapping remains compatible with domains that already provide a bespoke `/project` route.

**Given** the Sample domain host is used as the in-repository proof
**When** its `Program.cs` is inspected
**Then** the domain hosting surface is reduced to the canonical SDK calls
**And** moved SDK infrastructure is deleted from the Sample project.

**Given** the SDK host is built and tested
**When** the relevant build and unit-test projects run
**Then** the Release build is clean under warnings-as-errors
**And** focused SDK and Sample tests verify endpoint mapping, assembly discovery, and compatibility behavior.

### Story 1.2: Domain Query Handler Routing

**Requirements covered:** FR4

As a domain author,
I want domain queries to be implemented as plain query handlers and routed by the platform,
So that my domain can expose query behavior without hosting a custom projection/query actor.

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

**Given** query routing is tested
**When** focused unit tests execute
**Then** domain-side dispatch, operational metadata capture, handler-aware routing, fallback behavior, metadata propagation, gateway merge behavior, typed client metadata exposure, and backward-compatible projection-actor routing are verified.

### Story 1.3: Generic Read Models And Query Cursors

**Requirements covered:** FR5, FR6

As a domain author,
I want platform-provided persisted read-model storage and protected query cursors,
So that I can implement domain-specific read behavior without reimplementing DAPR state access, optimistic concurrency, or cursor protection.

**Acceptance Criteria:**

**Given** a domain projection or query handler needs durable read-model state
**When** it uses `IReadModelStore` and `ReadModelWritePolicy`
**Then** it can read and write ETag-aware entries by key
**And** it can apply events or merge singleton/index read models with bounded optimistic-concurrency retries.

**Given** a domain needs deterministic tests for read-model behavior
**When** it uses the platform testing fake
**Then** the fake preserves first-write-wins and ETag conflict semantics
**And** tests can inject conflicts without relying on live DAPR infrastructure.

**Given** a domain query returns paged results
**When** it creates a cursor with `IQueryCursorCodec` and `QueryCursorScope`
**Then** the cursor is protected with a caller-supplied Data Protection purpose
**And** decoding fails safely for wrong scope, wrong query type, malformed payload, tampering, oversize payload, or key rotation
**And** successful paged responses can return authoritative `QueryPagingMetadata` with effective page size, offset or next cursor, total count when known, and has-more evidence without exposing cursor internals
**And** request paging echoed by the gateway is not treated as proof of total count, next cursor, or page completeness unless the query handler or projection produced that metadata.

**Given** cursor or paging inputs are invalid, malformed, wrong-scope, oversized, tampered, or expired after key rotation
**When** the query path rejects them
**Then** the response uses support-safe validation/problem details
**And** tests prove cursors remain opaque and are not parsed, logged, displayed as support text, or treated as ordering proof.

**Given** the read-model and cursor seams are proven against non-trivial platform scenarios
**When** the implementation validates ETag-aware reads/writes, merge-on-write behavior, deterministic conflict injection, protected cursor encoding/decoding, invalid cursor rejection, and paging metadata propagation
**Then** the platform read-model and cursor contracts are proven without modifying the Tenants submodule
**And** production Tenants read-model/cursor adoption remains out of Story 1.3 and is owned by Story 1.6.

### Story 1.4: Projection And Domain Event Consumer Seams

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

### Story 1.5: Domain Module Hosting Observability

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

### Story 1.6: Sample And Tenants Domain-Centric Adoption

**Requirements covered:** FR9

As a platform maintainer,
I want the Sample and Tenants domains to prove the domain-centric model,
So that future domain authors have working references that do not duplicate platform infrastructure.

**Acceptance Criteria:**

**Given** the Sample domain is the minimal reference implementation
**When** it adopts the SDK host, projection handler, and event-consumer seams
**Then** its host and supporting files contain only domain-specific behavior
**And** moved request routing, projection, and operational metadata infrastructure is deleted from the Sample project.

**Given** the Tenants domain is the non-trivial reference implementation
**When** it adopts `IDomainQueryHandler`, `IReadModelStore`, `ReadModelWritePolicy`, `IQueryCursorCodec`, platform domain-event consumers, platform telemetry, and platform health checks
**Then** tenant RBAC, audit, read-model, pagination, and projection semantics are preserved
**And** per-domain query actors, cursor codecs, state-store wrappers, telemetry classes, health checks, and reusable Aspire/ServiceDefaults plumbing are removed.

**Given** domain-module authoring rules are enforced
**When** Sample and Tenants references are inspected by guardrail tests or governance checks
**Then** domain modules do not reintroduce their own reusable hosting, Aspire, ServiceDefaults, projection actor, cursor, state-store, telemetry, or health-check infrastructure
**And** any remaining AppHost/topology ownership follows the current root repository architecture rules.

**Given** the adoption proofs are validated
**When** the relevant EventStore and domain test projects run
**Then** unit tiers remain green
**And** any CI-gated DAPR/Aspire integration gaps are documented with exact blockers and follow-up work.

### Story 1.7: DomainService Packaging And Guardrails

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

### Story 2.4: Tenants External API Host Adoption

**Requirements covered:** FR15

As an external tenant-management integrator,
I want Tenants typed REST endpoints to be generated in an external API host,
So that external applications can use stable tenant APIs while Tenants UI stays a client-library consumer.

**Acceptance Criteria:**

**Given** Tenants commands and queries are intended for external REST exposure
**When** their contracts implement the required EventStore REST contract seam and route metadata
**Then** rich routes such as tenant detail, tenant users, user tenants, global administrators, and tenant audit are declared in contracts
**And** the declarations do not duplicate controller logic.

**Given** an external Tenants API host references the Tenants contracts and generator
**When** it builds
**Then** generated controllers replace the former hand-written `TenantsQueryController` surface
**And** all generated actions delegate through `IEventStoreGatewayClient` so gateway auth, validation, status, archive, and observability remain the front door.

**Given** Tenants UI needs tenant command and query behavior
**When** it is aligned with the corrected architecture
**Then** it uses EventStore/Tenants client libraries instead of hosting MVC command/query controllers
**And** UI-specific flows continue to preserve projection-confirmed success and support-safe states.

**Given** Tenants external API adoption is validated
**When** submodule unit and integration tests run in their appropriate lanes
**Then** the generated REST surface preserves existing external behavior
**And** freshness, projection-version, ETag, and paging evidence is backed by the real platform query metadata path implemented by Stories 1.2, 1.3, and 2.2 rather than by mocked gateway-client metadata
**And** Tenants UI and generated API evidence must not rely on ad hoc payload fields or missing freshness metadata to claim projection-confirmed success
**And** the Tenants external API proof runs or records exact blockers for package-reference mode with `UseHexalithProjectReferences=false`, proving it does not depend on source-only EventStore project references or a mixed source `Hexalith.EventStore.Gateway` plus package `Hexalith.EventStore.DomainService` graph
**And** any CI-gated DAPR/Aspire blockers are documented with exact commands and failure reasons.

### Story 2.5: Scoped Metadata-Rich Projection Notifications

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
**Then** the framework rejects or clips the metadata according to documented options
**And** metadata values are not logged above Debug level.

**Given** clients join or leave scoped projection groups
**When** the hub receives group operations
**Then** tenant authorization still runs before group membership changes
**And** group names validate projection type, tenant id, and scope using safe character and reserved-separator rules.

**Given** projection notification tests run
**When** SignalR and optional DAPR notification paths are exercised
**Then** scoped detail delivery, signal-only compatibility, auth rejection, metadata bounds, fail-open broadcast behavior, and Redis backplane fan-out are covered.

### Story 2.6: Generated Command-Status Location Policy

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
**Placement note:** Epic 2 shipped its original five stories; Story 2.6 is a post-retro hardening follow-on, tracked `backlog`.

### Story 2.7: Outbound DAPR Routing-Header Ownership

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
**Then** the equivalent submodule change is recorded as a coordinated follow-up requiring maintainer approval (Story 2.4 lineage)
**And** it is not silently modified here.

**Given** Release build and focused tests run
**When** the change lands
**Then** all configured tests pass, including the new replacement and guardrail tests.

**Placement note:** Post-retro hardening follow-on; Epic 2 reopened to `in-progress`. Correct-course rationale in `sprint-change-proposal-2026-07-07-outbound-dapr-routing-header-policy.md`.

### Story 2.8: Query Response Provenance Contract And Route-Aware Gateway ETag

**Requirements covered:** FR4, FR12, FR15, FR34, NFR8, NFR16 (EventStore-owned query-response provenance slice); governed by AD-14 and AD-15.

As a consumer of platform query metadata,
I want every query response to declare explicit route provenance and the gateway to stop attaching projection ETags to handler-computed responses,
So that generated REST and UI code never present a gateway ETag or fabricated version as projection-backed current/stale evidence.

**Acceptance Criteria:**

**Given** a response from a domain query handler (`HandlerAwareQueryRouter`, `ProjectionType` null)
**When** the gateway builds `QueryResponseMetadata`
**Then** provenance is `HandlerComputed`
**And** the gateway attaches no projection-actor ETag, projection version, or `IsStale` derived from `request.Domain`/`request.ProjectionType`
**And** a Tier 2/3 test asserts on the real gateway path that no projection ETag and no `X-Hexalith-Projection-Version`/`X-Hexalith-Is-Stale` header is emitted for the handler route.

**Given** a response from a projection actor / read model with persisted `IReadModelFreshness`
**When** the gateway builds `QueryResponseMetadata`
**Then** provenance is `ProjectionBacked`
**And** `ProjectionVersion`/`IsStale` are sourced from the persisted read model, never aliased from the ETag
**And** a Tier 2/3 test asserts the genuine version/freshness values traverse the gateway.

**Given** a consumer, including generated REST headers or UI freshness indicators
**When** provenance is `HandlerComputed` or `Unknown`
**Then** it renders `Unknown`, never `Current`/`Stale`, and does not claim projection-confirmed success.

**Given** a Tenants producer still aliases `ProjectionVersion := ETag`
**When** the EventStore-owned platform enforcement lands without submodule maintainer approval for the producer fix
**Then** the affected route is classified `HandlerComputed` or `Unknown`
**And** the Tenants producer aliasing fix is tracked as a separate maintainer-approved follow-up, not as a blocker for EventStore platform provenance enforcement.

**Sequencing note:** This story is a Phase 4 readiness blocker for generated REST/UI current/stale evidence. Implement before any new generated API or UI story claims projection-backed freshness.

## Epic 3: Release And Repository Reliability

Maintainers can release reproducibly with correct package/reference mode, aligned submodule and Aspire resource layout, deterministic release gates, live-sidecar integration coverage, shared supply-chain workflows, and manifest-governed package output.

### Story 3.1: Re-Tier Live-Sidecar Tests From Release Gate

**Requirements covered:** FR17

As a release maintainer,
I want live DAPR sidecar tests to run outside the per-push release gate,
So that releases are not blocked by cold-start sidecar flakiness while live-sidecar coverage remains preserved.

**Acceptance Criteria:**

**Given** tests require a live `daprd` sidecar
**When** those test classes are categorized
**Then** they are marked with a `LiveSidecar` trait
**And** non-live-sidecar tests remain in the deterministic release gate.

**Given** the release workflow runs
**When** it executes Server.Tests
**Then** it filters out `Category=LiveSidecar`
**And** it does not install or initialize DAPR solely for the release gate.

**Given** the dedicated integration workflow runs
**When** it provisions DAPR and executes `Category=LiveSidecar`
**Then** live-sidecar tests run in their own lane
**And** failures are visible without blocking semantic-release publishing.

**Given** live-sidecar tests start on a cold CI runner
**When** the shared DAPR fixture initializes
**Then** it performs readiness retry and warm-up actor round trips
**And** placement, activation, and Redis state paths are hot before assertions depend on them.

> Companion: **Story 3.8** provides the local DAPR/Aspire generated-API smoke preflight
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

### Story 3.5: Debug Source References And Release Package References

**Requirements covered:** FR21

As a package maintainer,
I want external Hexalith dependencies selected by build intent,
So that Debug builds can source-debug while Release builds depend on published packages.

**Acceptance Criteria:**

**Given** `UseHexalithProjectReferences` is not explicitly set
**When** a Debug build evaluates project references
**Then** external Hexalith project references are enabled when root-declared submodule source exists
**And** developers can override the mode explicitly.

**Given** `UseHexalithProjectReferences` is not explicitly set
**When** a Release build evaluates project references
**Then** external Hexalith package references are selected by default
**And** package versions are pinned centrally in `Directory.Packages.props`.

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

### Story 3.7: Shared CI/CD Security Gates And Supply-Chain Backlog

**Requirements covered:** FR25

As a repository maintainer,
I want EventStore CI/CD to follow the same reusable workflow pattern as Hexalith.Tenants,
So that CI, release, security gates, package validation, and container publishing are governed through Hexalith.Builds with only module-specific inputs in EventStore.

**Acceptance Criteria:**

**Given** EventStore uses GitHub Actions for CI, release, CodeQL, dependency review, and commitlint
**When** workflow files are inspected
**Then** `.github/workflows/ci.yml` is a thin caller of `Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main`
**And** `.github/workflows/release.yml` is a thin caller of `Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@main`
**And** CodeQL, dependency-review, and commitlint remain thin shared workflow callers using `@main`
**And** caller workflows retain only module-specific triggers, concurrency, permissions, secrets, and workflow inputs.

**Given** EventStore Server.Tests previously contained both deterministic in-process tests and `Category=LiveSidecar` tests
**When** CI is migrated to the shared reusable workflow pattern
**Then** deterministic server tests still run in the blocking release gate without a `Category!=LiveSidecar` filter
**And** live-sidecar tests still run in a dedicated non-release-blocking lane with DAPR initialized
**And** the migration does not make live-sidecar failures block semantic-release publishing.

**Given** EventStore package release is manifest-governed
**When** shared CI runs consumer/package validation
**Then** EventStore provides compatible `scripts/pack-release-packages.py`, `scripts/validate-nuget-packages.py`, and `scripts/validate-consumer-package-references.py` entry points
**And** those scripts preserve `tools/release-packages.json` as the package inventory
**And** validation rejects submodule packages or package outputs outside the EventStore manifest.

**Given** EventStore release runs through `domain-release.yml@main`
**When** semantic-release publishes artifacts
**Then** NuGet publishing remains scoped to manifest-listed `Hexalith.EventStore.*` packages
**And** container publishing uses explicit EventStore project-to-repository mappings approved for release
**And** no sample or admin container is published accidentally
**And** release secrets are validated before any irreversible NuGet or container publish command runs.

**Given** the migration completes
**When** docs and workflow references are scanned
**Then** `docs/ci.md`, `.releaserc.json`, package-governance tests, and CI documentation describe the Tenants-style reusable workflow pattern accurately.

### Story 3.8: Generated API DAPR/Aspire Smoke Preflight

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
**Then** it verifies accepted-command and ETag/`304` behavior plus persisted/read-model end-state where available, never relying on status codes alone
**And** all output is support-safe (no tokens, JWTs, connection strings, private addresses, raw payloads, or stack traces).

**Given** the preflight completes
**When** it reports its result
**Then** it emits generated API endpoints, DAPR sidecar readiness, placement/scheduler readiness, and support-safe failure details (Epic 2 retro item 4 completion gate)
**And** it exits with distinct status categories for success, blocked environment, topology-not-running, generated-API failure, and state-evidence failure.

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

**Classification:** Coordinated follow-up requiring Tenants submodule maintainer approval. This story is not the EventStore platform provenance prerequisite; that work is owned by Story 2.8.

As a platform maintainer coordinating with Tenants maintainers,
I want Tenants producer-side query freshness aliases removed or explicitly classified as non-projection-backed,
So that Tenants never presents an opaque ETag as projection version or current/stale evidence.

**Acceptance Criteria:**

**Given** Tenants producer code aliases `ProjectionVersion := ETag`
**When** maintainer-approved submodule work is scheduled
**Then** the aliasing is removed and genuine projection-backed freshness is sourced from persisted read-model evidence
**Or** the route is explicitly classified `HandlerComputed` or `Unknown` and consumers render Unknown.

**Given** maintainer approval is not yet available
**When** EventStore platform provenance enforcement ships through Story 2.8
**Then** EventStore blocks fabricated Current/Stale claims by route classification
**And** this Tenants follow-up remains visible without blocking the EventStore-owned platform story.

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

### Story 5.6: Runtime Topology And Deploy Parity

**Requirements covered:** FR32

As an operator,
I want the topology loaded at runtime to match the security posture tested and deployed,
So that local AppHost, test, and production DAPR configurations enforce the same tenant isolation assumptions.

**Acceptance Criteria:**

**Given** the AppHost starts DAPR sidecars
**When** pub/sub components are configured
**Then** the scoped/dead-letter `pubsub.yaml` path is passed explicitly
**And** daprd does not silently load an unscoped generated component instead.

**Given** production DAPR component templates are used
**When** state-store and pub/sub YAML are inspected
**Then** key prefixes, tenant scopes, ACLs, and app-id rules match the posture asserted by tests
**And** deny-by-default rules are preserved.

**Given** AppHost topology tests run
**When** they inspect the generated or configured sidecar arguments
**Then** they assert the actual component paths and ACL posture loaded at runtime
**And** stale config drift fails tests.

**Given** topology, app-id, topic, or sidecar settings change
**When** documentation and deployment manifests are updated
**Then** DAPR access-control YAML, AppHost resource names, and route/topic wiring remain aligned.

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
**And** implementation tasks cite the approved spec sections they satisfy.

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
**Then** reduced replay behavior and correctness guards are both verified.

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

### Story 7.2: Admin Claims, Audit, And Honest Deferred Operations

**Requirements covered:** FR34

As an administrator,
I want admin actions to be correctly authorized, attributable, and honest about unsupported operations,
So that the admin plane is trustworthy for production operations.

**Acceptance Criteria:**

**Given** Admin.Server receives user claims
**When** claims transformation runs
**Then** `tenants` and `permissions` are normalized consistently with gateway rules
**And** null or missing tenant scope denies access rather than granting all-tenant visibility.

**Given** a state-mutating admin action executes
**When** authorization succeeds
**Then** a structured audit record includes the authenticated user id, tenant context, action, outcome, and correlation id
**And** audit logging does not expose tokens, raw payloads, or stack traces.

**Given** backup, restore, import, compaction, or other deferred operations are not implemented
**When** an operator views the Admin UI or calls the corresponding server endpoint
**Then** the UI hides or disables unavailable operations
**And** any remaining endpoint returns `501` instead of presenting fake success.

**Given** admin API clients are duplicated across hosts or tools
**When** a shared typed client is introduced or planned
**Then** behavior remains consistent
**And** duplicated request/response mapping is reduced without changing authorization semantics.

### Story 7.3: Production Deployment Hardening

**Requirements covered:** FR34

**Architecture gate:** AD-16 (health/probe anonymous-access contract). Enabling DAPR app-health/readiness probes assumes the probe endpoints are reachable anonymously; if a fail-closed policy is present, the AD-16 exemption must already be in place.

As a production operator,
I want deployment configuration to use secret stores, health checks, resiliency policy, and immutable images,
So that deployed EventStore environments have a defensible operational posture.

**Acceptance Criteria:**

**Given** production DAPR components require secrets
**When** deploy manifests are generated or inspected
**Then** secrets use secret-store components and `secretKeyRef`
**And** plaintext `{env:...}` placeholders are removed where production posture requires secret indirection.

**Given** services expose health checks
**When** readiness probes are configured
**Then** state-store health checks are tagged for readiness
**And** DAPR app health checks are enabled where supported
**And** the probe endpoints `/health`, `/alive`, and `/ready` are reachable anonymously per the AD-16 contract even when a fail-closed authorization policy is active.

**Given** DAPR resiliency policies are configured
**When** domain service invocations are inspected
**Then** the `apps` targets include the domain services the code assumes are covered
**And** retries, timeouts, and circuit-breaker behavior are documented.

**Given** container images are produced for release
**When** deploy manifests reference image tags
**Then** immutable git-SHA tags are supported or preferred
**And** mutable tags are not the only production deployment option.

### Story 7.4: Integration Test Recovery And State Evidence

**Requirements covered:** FR34

As a quality maintainer,
I want integration tests to run in meaningful CI lanes and assert persisted state evidence,
So that high-risk tenant, DAPR, and topology behavior is not validated only by local or smoke tests.

**Acceptance Criteria:**

**Given** IntegrationTests include infra-free or lightweight subsets
**When** CI workflows are updated
**Then** those subsets run in CI without requiring the full Aspire topology
**And** full Aspire-dependent tests have a dedicated documented lane or blocker.

**Given** integration tests currently assert only HTTP `202`, `200`, or mock call counts
**When** they are refactored
**Then** they assert Redis/state-store/read-model/CloudEvent end-state evidence where applicable
**And** shared read-back helpers are extracted into `Hexalith.EventStore.Testing.Integration`.

**Given** fake-simulated conflict tests are labeled as integration tests
**When** the test suite is reviewed
**Then** fake-only tests are moved to unit scope or rewritten against real infrastructure
**And** integration labels reflect actual external dependency coverage.

**Given** performance or advisory validation is needed
**When** CI workflows are updated
**Then** a `workflow_dispatch` perf-lab lane is available
**And** permanently red advisory jobs or never-driven skips are either fixed, quarantined with rationale, or removed.

### Story 7.5: Track Future Capability Backlog

**Requirements covered:** FR35

**Classification:** Planning/backlog artifact. This story does not authorize implementation of GDPR erasure/tombstoning, Admin interactive OIDC login, aggregate test kit, or REST generator hardening. It completes by producing exact backlog artifacts with scope, non-goals, dependencies, risks, and validation expectations.

**Required outputs:**

- `_bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md`
- `_bmad-output/planning-artifacts/backlog/iam-1-admin-oidc-login.md`
- `_bmad-output/planning-artifacts/backlog/kit-1-aggregate-test-kit.md`
- `_bmad-output/planning-artifacts/backlog/rest-generator-hardening.md`

As a product owner,
I want large deferred capabilities tracked as explicit backlog epics,
So that GDPR erasure, admin OIDC, aggregate testing, and generator hardening are not hidden inside unrelated remediation work.

**Acceptance Criteria:**

**Given** GDPR aggregate erasure and tombstoning are deferred
**When** backlog artifacts are updated
**Then** `_bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md` records crypto-shred, tombstone, broker, snapshot, retention, and verification scope
**And** it is not implemented opportunistically inside unrelated cleanup stories.

**Given** Admin interactive OIDC login is deferred
**When** backlog artifacts are updated
**Then** `_bmad-output/planning-artifacts/backlog/iam-1-admin-oidc-login.md` records authorization-code with PKCE, forwarded end-user tokens, and removal of ROPC/self-mint service identity
**And** it remains separate from immediate secret-stripping/auth-guard fixes.

**Given** an aggregate test kit is deferred
**When** backlog artifacts are updated
**Then** `_bmad-output/planning-artifacts/backlog/kit-1-aggregate-test-kit.md` records `Given(events).When(command).Then(events)` style fixtures, replay determinism, Apply idempotency, and package dependency boundaries
**And** it tracks the split needed for testing packages to avoid unnecessary Server dependencies.

**Given** REST generator hardening remains after Epic 2
**When** backlog artifacts are updated
**Then** `_bmad-output/planning-artifacts/backlog/rest-generator-hardening.md` records generator incrementality, generated-controller authz checks, and deferred-work items
**And** they are not lost when Epic D proof stories complete.

**Given** REST generator retrospective action items are recorded
**When** REST generator hardening is prepared
**Then** `_bmad-output/planning-artifacts/backlog/rest-generator-hardening.md` links the dedicated hardening story or backlog item that pulls from `_bmad-output/implementation-artifacts/deferred-work.md`
**And** it explicitly covers unsupported contract-shape diagnostics, duplicate command JSON-name diagnostics, invalid `RestQueryBinding` source diagnostics, empty constant binding diagnostics, route-template constraint behavior, case-insensitive route/JSON-name matching, referenced-contract incrementality, and generated external API error-semantics coverage.
