---
stepsCompleted:
  - step-01-validate-prerequisites
  - step-02-design-epics
  - step-03-create-stories
  - step-04-final-validation
inputDocuments:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md
  - _bmad-output/planning-artifacts/ux.md
inputDocumentDigests:
  _bmad-output/planning-artifacts/prd.md: 8f9c88e8b8665c2ded07a6a4df88db95d04339e5c728ef361ee9fbe9c115a699
  _bmad-output/planning-artifacts/architecture.md: 623bc23e453aba5a703c5aa1b208bf9f985f5937f5900f5e665f5cb5abe5ca94
  _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md: 3be78b6b856d3bb8e76451ebbfa550d9018bd4bce05e3dc4a2968758f7abf83e
  _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md: 6a058112512b3dcc4468bdf698d6949345ab7ba3844e1a61b725f9a0aca38a3c
  _bmad-output/planning-artifacts/ux.md: 3c827e922c2a05559eac09ad3bff638fed0e2aca789eacd733dbb904e4a42c8c
---

# eventstore - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for eventstore, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

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

### NonFunctional Requirements

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

### Additional Requirements

- This is a brownfield architecture; no starter-template or greenfield scaffold is specified. Stories must evolve the existing structural seed and `src/Hexalith.EventStore.Admin.UI` rather than create parallel platform or UI hosts.
- Preserve CQRS, DDD, and event sourcing on DAPR state, actors, pub/sub, and service invocation, with Aspire as the local orchestration and deployable-topology seed.
- Apply the OQ8 authority order to Stories 4.9-4.15: the approved 2026-07-20 sprint change and OQ8 design version 1.0.0 (SHA-256 `1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8`) override historical FR27/NFR7/NFR16 text.
- Apply the 2026-08-16 deployed-runtime parity correction atomically: Story 3.13 is the rejected, non-authorizing `v3.94.1` disposition; Story 3.14 owns a separately authorized corrective release; Story 3.15 owns positive exact-lineage parity closure; Epic 3 stays open until that closure.
- Keep domain modules limited to domain behavior and contracts; reusable hosting, DAPR endpoints, query/projection dispatch, persistence, cursor, telemetry, health, and Aspire concerns remain in EventStore platform libraries.
- Make the EventStore gateway the policy boundary for all external command/query entry points; generated APIs, UI, Admin, and domain services must not bypass it to call handlers, actors, state stores, projections, or DAPR endpoints directly.
- Keep generated controllers in dedicated external API hosts and interactive UI hosts as typed-client consumers; generated or hand-written per-message MVC command/query controllers are forbidden in interactive UI hosts.
- Run trusted canonical-intent admission after authentication, current authorization, and canonical validation but before `AggregateActor`; only the current non-zero fence may cross a side-effect boundary or finalize a terminal result.
- Keep `AggregateActor` as the sole event-append path and durable event-mutation coordinator. Domain processors return `DomainResult` and never write EventStore state directly.
- Preserve stable event identity: aggregate sequence is gapless per aggregate, `GlobalPosition` is non-zero, CloudEvent `id` is the persisted `MessageId`, and duplicate command replies preserve the original result.
- Use platform-owned read-model lifecycle and write contracts. Read-model/checkpoint erasure is one logical scoped operation, and detail/index batch semantics must define atomicity, partial-failure recovery, idempotency, ordering, flush completion, optimistic concurrency, and equivalent DAPR/in-memory behavior.
- Protect query cursors with `IQueryCursorCodec` and `QueryCursorScope`; cursors are opaque, bounded, DataProtection-backed, scope-validated, purpose-isolated, and fail safely on malformed, tampered, rotated-key, wrong-scope, or wrong-query-type inputs.
- Treat DAPR pub/sub and projection notifications as at-least-once, unordered freshness signals. Deduplicate on `MessageId`; scope sequence guards by tenant/domain/aggregate/projection; do not turn SignalR, `202`, or command acceptance into projection-confirmed success.
- Change AppHost resources, DAPR component/configuration YAML, app IDs, ACLs, scopes, topics, resiliency, placement/scheduler arguments, publish targets, and topology tests as one coherent slice.
- Require application-layer credentials and tenant authorization before any public, internal, domain-service, projection-notification, or admin-computation endpoint discloses data; infrastructure scoping and caller-supplied admin flags are insufficient.
- Keep `/health`, `/alive`, and `/ready` as the only explicit anonymous endpoints. Mark them `AllowAnonymous`, keep responses support-safe, and prove the same host still denies an unauthenticated representative protected endpoint.
- Carry query evidence through the canonical `QueryResponseMetadata` chain. Producer metadata owns freshness, projection version, paging, degraded state, and warnings; the gateway owns the HTTP ETag and fills `ServedAt` only when absent.
- Classify every query route as `ProjectionBacked`, `HandlerComputed`, or `Unknown`. Only projection-backed routes may carry authoritative `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, or `LocalOnly` lifecycle evidence; ETags never prove lifecycle or projection version.
- Forward bounded query metadata headers only when their values are present. Cursors and ETags remain opaque and must not be displayed, parsed, or logged as diagnostic detail.
- Generated command controllers may emit `Location` only for a successful `202`, only as an absolute gateway-authoritative command-status URI built from runtime configuration, and must omit it when the target is missing or invalid; external API hosts do not expose a competing status route.
- Sidecar-routed gateway clients must explicitly append `AddEventStoreDaprServiceInvocation(appId, apiToken)` last. Its platform handler removes and replaces any inbound/caller `dapr-app-id` and `dapr-api-token`, remains innermost, and is guarded against per-host reimplementation or omission.
- Projection handlers are asynchronous, cancellation-aware, and identified by `(Domain, ProjectionType)`. `/project/v2` remains the frozen wire envelope, while normalized results use the closed status/checkpoint matrix and advance only after all required durable work and checkpoint save complete.
- Paged projection rebuilds must be replay-equivalent: pages are read optimizations, work remains staged/non-live, cancellation or failure preserves the last complete model, and promotion occurs only after every required projection completes.
- Release and package validation are manifest-governed by `tools/release-packages.json`; source references require explicit `UseHexalithProjectReferences=true`, while unset/false remains package-safe in all configurations and package publication never uses source mode.
- Centralize every source-owned package version in `references/Hexalith.Builds/Props/Directory.Packages.props`; consuming catalogs import it without local version authority, and coupled package families/security patch bands move under grouped compatibility evidence.
- Publish released containers only through .NET SDK container support as one immutable OCI image index with exactly `linux/amd64` and `linux/arm64` child manifests, no nested/extra/unknown/variant platform entries, complete provenance labels, raw-byte digest/size validation, and successful smoke evidence for both immutable child digests.
- Produce and independently validate a canonical `ReleaseIdentity` binding repository, version/tag, source SHA, workflow/build authority, package manifest and hashes, OCI index/child/config chain, and smoke evidence; immutable failed releases remain non-authorizing and are corrected only by a later version.
- High-risk acceptance evidence must inspect persisted state/read-model/CloudEvent data, real topology, security denials, package output, and release registry evidence; HTTP statuses and mock calls alone are smoke signals.
- Gate global-position sharding, folded snapshots, projection cost/sequence guards, event versioning/upcasting, identity validation, and cancellation-contract work behind their named approved specification artifacts before implementation begins.
- Consumer infrastructure removal requires an owner-reviewed, content-bound exact-SHA parity packet, applicable source/package/deployed identity proofs, authenticated role receipts, and a consumer-owner authorization receipt; booleans, free-form approval, tags, or story completion never grant cross-repository mutation authority.
- Keep `v3.94.1` permanently rejected and non-authorizing; it selects no deployed identity and authorizes no deployment, Parties migration, G5 closure, or consumer infrastructure removal.
- Gate the optional payload-protection engine behind `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`: Story 8.1 authorizes Story 8.2, each later story requires predecessor evidence, and Story 8.11 alone may close G5 after production-backend, golden, dual-provider, release, and rollback proof.
- Implement production operational/application secret retrieval through the canonical DAPR `openbao` component using `secretstores.hashicorp.vault` v1, `secretKeyRef`, DAPR Secrets API access, default-deny per-app scopes, least-privilege policies, TLS verification, readiness gating, bounded in-memory caching, and publish-overlap-acknowledge-revoke rotation.
- Keep OpenBao bootstrap tokens, DAPR API tokens, and TLS trust material as uncommitted platform inputs. Prefer projected token files; allow a Kubernetes Secret only for an explicitly documented bootstrap-only exception, never for downstream application or operational secrets.
- Prove OpenBao integration with a real OpenBao instance through DAPR; LocalDev/in-memory implementations and Azure Container Apps managed DAPR without equivalent component and secret-scope support are not production evidence.
- Implement durable idempotency admission with tenant/digest-key partitioning, domain-separated HMAC-SHA-256 opaque-key digests, collision verification tags, trusted descriptors/fixed retention classes, exact replay, inclusive expiry, metadata-minimized tombstones, fail-closed corrupt/unreadable/unsafe legacy handling, directory-based digest rotation, and recoverable legacy migration.
- Use the production-equivalent OQ8 evidence profile `oq8-postgresql-v1`: DAPR 1.18.x, `statestore` using `state.postgresql` with `actorStateStore: true`, production resiliency, and at least two EventStore hosts with independent sidecars sharing one PostgreSQL backend.
- Preserve compensating-command repair semantics: never edit, delete, or rewrite persisted events to repair business state, and use support-safe structured external errors or domain rejections rather than leaking infrastructure details.
- Use `Hexalith.EventStore.slnx` for restore/build, run tests per project, keep ULID-safe envelope identity handling, forbid `Guid.TryParse` for EventStore identifiers, and follow warnings-as-errors, nullable, code-style, and `ConfigureAwait(false)` rules.
- Keep AOT/trimming, quantitative UI performance budgets, full aggregate/event GDPR erasure, Admin interactive OIDC, aggregate test-kit implementation, and REST generator hardening beyond the approved proof scope out of the current implementation unless their separately tracked gates authorize them.

### UX Design Requirements

UX-DR1: Evolve `src/Hexalith.EventStore.Admin.UI` in place as the single EventStore UI, retaining resource/container identity `eventstore-admin-ui`; do not create a second host or duplicate page implementation.

UX-DR2: Expose exactly one host-level FrontComposer module entry with stable module identity `event-store-admin` and label **Event Store Admin**; keep it selected for all dashboard tabs and deep links.

UX-DR3: Implement one EventStore dashboard whose child navigation uses URL-addressable, keyboard-operable `FluentTabs` for Overview, Commands, Streams & Events, Projections, Tenants & Access, Topology, Storage & Snapshots, Recovery, Deferred & Backlog, and Settings.

UX-DR4: Map every legacy Admin.UI route into its owning dashboard tab or canonical deep link; non-canonical routes redirect without preserving a second page implementation, and `/backups` resolves to Deferred & Backlog or an explicit unsupported state.

UX-DR5: Compose the dashboard with FrontComposer Shell and Contracts.UI plus Blazor Fluent UI V5, resolving all FrontComposer packages through the single Builds-catalog version and using no generated or hand-written per-message MVC controllers.

UX-DR6: Inherit Fluent theme roles for accent, neutral layers, foreground, borders, focus, and status treatments. Do not hard-code captured colors, gradients, custom palettes, legacy Fluent v4/FAST tokens, or redefined theme primitives.

UX-DR7: Meet WCAG 2.2 AA contrast and always pair Success, Warning, Danger, Neutral, stale, denied, failed, and lifecycle colors with readable state text.

UX-DR8: Use Fluent/FrontComposer typography with system font fallbacks, direct work-surface titles, compact section titles, and metadata roles; do not create a CSS heading ramp or use negative letter spacing.

UX-DR9: Use the 4px density system with compact 8px control gaps, 16px repeated-summary gaps, and 24px region gaps through component parameters/tokens; avoid nested cards, decorative floating sections, custom shadows, and marketing-style oversized cards.

UX-DR10: Implement the dashboard shell with a Fluent-themed app bar, compact host navigation, white/neutral content canvas, visible tenant/environment scope, connection freshness, and bounded support-safe utilities.

UX-DR11: Implement the dashboard header with a focusable page/tab title, environment and tenant context, connection status, and safe utility actions; do not use hero copy.

UX-DR12: Implement stat summaries with current values plus explicit evidence state; stale values remain visible only with a stale label and last-successful-refresh time.

UX-DR13: Implement filter bars with Fluent inputs above the grid they affect, persist useful filter state in the URL, and ensure denied filters never disclose tenant or resource existence.

UX-DR14: Implement evidence surfaces with `FluentDataGrid` or FrontComposer grid primitives for commands, streams, events, projections, tenants, topology, storage, recovery, and audit data, supporting sorting/filtering/paging and detail drill-in while keeping rows dense and scannable.

UX-DR15: Implement status badges with `FluentBadge`, accessible text, stable state identifiers/selectors, and no color-only meaning.

UX-DR16: Implement issue banners with `FluentMessageBar` or a FrontComposer equivalent that names the affected scope, operational consequence, and safe next action without raw internals.

UX-DR17: Implement destructive/state-mutating operations through `FluentDialog` with exact target identity, expected effect/evidence, permission context, explicit confirmation and cancellation, accepted-first status, and projection/evidence confirmation before success.

UX-DR18: Implement detail panels with `FluentDrawer`, `FluentDialog`, or FrontComposer panels; when two or more titled sibling sections exist, use one `FluentAccordion` with the primary evidence section expanded, while never hiding a page's only primary grid in an accordion.

UX-DR19: Implement a command lifecycle tracker that distinguishes `Received`, `Processing`, `EventsStored`, `EventsPublished`, `Completed`, `Rejected`, `PublishFailed`, and `TimedOut`, using text plus Fluent status styling.

UX-DR20: Implement a projection freshness indicator that renders `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, `LocalOnly`, or `Unknown`; render the six concrete lifecycle states only for projection-backed provenance, render handler-computed/missing/invalid provenance as `Unknown`, and never count `LocalOnly` as confirmed success.

UX-DR21: Disable mutations by default for every projection state except authoritative `Current`, unless a named consumer-owned exception is documented and tested.

UX-DR22: Implement deferred-operation placeholders as hidden-by-default or disabled/read-only states with the exact message “Unavailable in this release.” and tracking context only; do not render fake forms for backup, restore, import, compaction, GDPR erasure, OIDC login, test-kit, or generator-hardening work.

UX-DR23: If implemented, the command palette must use Fluent dialog/input/list primitives, supplement rather than replace visible navigation, obey tenant/role filtering, and never reveal hidden resources or runnable deferred operations.

UX-DR24: Use skeletons matching the eventual stat/grid layout for cold loading; if the Admin API is unavailable, show a global issue banner and only render last-known data when explicitly marked stale.

UX-DR25: Treat SignalR connection changes as freshness-state changes only; use polling or explicit refresh to retrieve authoritative evidence and keep disconnected/reconnected announcements support-safe.

UX-DR26: Model every mutation as validation → submit → accepted → evidence pending → projection-confirmed or terminal non-success; never collapse HTTP `202`, SignalR, or accepted state into completion.

UX-DR27: Render access denial fail closed with an accessible denied label, route/action context, and safe next action without confirming whether the tenant, user, stream, projection, service, or setting exists; return focus to the initiating control.

UX-DR28: Implement empty states per visible tenant/domain scope without mentioning denied resources; keep protected payloads redacted and show only bounded support metadata.

UX-DR29: Surface dead letters prominently with count, oldest age, affected visible tenant/domain, and role-gated safe retry/archive actions; mutations must be audited and evidence-confirmed.

UX-DR30: Provide inline Fluent validation, prevent submission on invalid or oversized input, avoid echoing raw payloads, and describe EventStore identifier shape without calling it a GUID.

UX-DR31: Preserve the last complete live model during projection rebuilds, show bounded progress when available, and distinguish rebuilding, degraded, unavailable, local-only, stale, and unknown states with their operational consequences.

UX-DR32: Make tabs, grids, dialogs, filters, accordions, command palette, and all actions fully keyboard-operable; provide one focusable page title, a host skip link to dashboard content, accessible names/roles, screen-reader row context, and focus restoration after dialogs and failures.

UX-DR33: Respect reduced motion and use live-region priorities: polite for accepted/evidence-pending/confirmed and freshness transitions, assertive for terminal failure/access denial/rejected destructive actions, and inline association plus polite summary for validation errors.

UX-DR34: Provide stable `data-testid` selectors for dashboard tabs, filters, status badges, dialogs, and evidence rows; tests use stable selectors and state identifiers rather than translated text or incidental Fluent markup.

UX-DR35: Source all visible copy from resource-backed complete strings, preserve labels as translatable while identifiers remain raw, and prohibit runtime sentence assembly, concatenated clauses, and English-only plural grammar.

UX-DR36: At widths `>=1280px`, show full navigation, horizontal tabs, and full grid columns; at `960-1279px`, compact navigation, allow horizontally scrolling tabs, and move secondary metadata to detail panels; below `960px`, collapse host navigation while retaining usable triage/read flows.

UX-DR37: On narrow screens, each mutation must either remain fully usable in a viewport-sized Fluent dialog, be disabled with a reason, or show a support-safe desktop-required state; incident triage, status checks, and simple recovery visibility must remain available.

UX-DR38: Never render bearer tokens, decoded JWTs, raw EventStore metadata or payloads, protected payloads, stack traces, cursors, ETags, secret values, or unbounded SignalR metadata; expose safe identifiers only when contractually required and keep every admin mutation attributable.

UX-DR39: Implement the incident-recovery journey so an operator can identify stale health, inspect protected dead-letter evidence, confirm a retry, see accepted/evidence-pending state, and verify reduced dead-letter count plus safe audit evidence without raw payload or stack-trace disclosure.

UX-DR40: Implement tenant-access mutation journeys so visible-scope filtering, current-freshness gating, exact tenant/user/role confirmation, accepted/evidence-pending status, projection-confirmed role updates, support-safe audit evidence, and fail-closed denied states are testable.

UX-DR41: Implement command-investigation journeys that search safe message/correlation identifiers, distinguish stored from published events, link to protected stream evidence, expose stale projection state, and route committed-but-unpublished cases to recovery without implying the command should be resubmitted.

UX-DR42: Keep the Sample UI as an accepted-submission demonstration and the Tenants UI as a projection-confirmed workflow: both use EventStore clients, show evidence-pending after submit, time out to pending/stale rather than success, and confirm only after authoritative read-model metadata changes.

### FR Coverage Map

FR1: Epic 1 - Domain modules remain domain-centric while EventStore supplies reusable platform infrastructure.
FR2: Epic 1 - Domain authors receive the canonical domain-service SDK host shape.
FR3: Epic 1 - Domain services expose the canonical DAPR-facing endpoints.
FR4: Epic 1 - Queries use handler discovery, gateway routing, canonical metadata, provenance, and lifecycle evidence.
FR5: Epic 1 - Read models use generic lifecycle, erasure, ETag-aware write, and coordinated batch contracts.
FR6: Epic 1 - Query paging uses a reusable protected cursor codec.
FR7: Epic 1 - Domains receive asynchronous multi-projection and domain-event consumer seams.
FR8: Epic 1 - Domain modules receive reusable Aspire, telemetry, and health extensions.
FR9: Epic 1 - Sample and Tenants adopt the shared SDK and remove duplicated infrastructure.
FR10: Epic 1 - DomainService and ServiceDefaults join the manifest-governed package set.
FR11: Epic 2 - Developers receive the REST source-generator contract seam.
FR12: Epic 2 - Generated controllers provide typed gateway delegation, metadata, safe errors, and authoritative status locations.
FR13: Epic 2 - Generated APIs live in dedicated external hosts while UI hosts remain client consumers.
FR14: Epic 2 - Sample proves the contracts-only library and generated external API host pattern.
FR15: Epic 2 - Tenants proves external generated APIs and metadata-aware UI client consumption.
FR16: Epic 2 - Projection notifications gain scoped, bounded detail while preserving signal-only compatibility.
FR17: Epic 3 - Live-sidecar tests move to a dedicated integration workflow with readiness handling.
FR18: Epic 3 - Maintainers can override the DAPR actor timeout without changing its production default.
FR19: Epic 3 - Root-declared submodules and repository paths use the `references/` layout.
FR20: Epic 3 - Aspire exposes Keycloak under the stable `security` resource identity.
FR21: Epic 3 - Dependency selection and central version authority remain package-safe and reproducible.
FR22: Epic 3 - Build, test, pack, and release commands assert package mode and exclude submodule projects.
FR23: Epic 4 - Event positions, CloudEvent identity, and duplicate command results remain stable.
FR24: Epic 4 - Global-position sharding proceeds only through an updated approved ordering specification.
FR25: Epic 3 - CI uses shared security gates, pinned actions, and manifest-defined package scope.
FR26: Epic 5 - Immediate security, tenant-filter, secret, admin, CLI, and identity defects fail closed.
FR27: Epic 4 - Durable tenant/key admission prevents conflicting, expired, corrupt, or replayed work from causing duplicate effects.
FR28: Epic 5 - Internal and administrative trust boundaries require application credentials and reject wire-asserted privilege.
FR29: Epic 4 - Replay dispatch is deterministic and all payload paths share canonical serialization.
FR30: Epic 4 - Committed-but-unpublished events recover without command resubmission.
FR31: Epic 4 - Append fencing decisions follow live two-writer and DAPR conflict evidence.
FR32: Epic 5 - AppHost runtime topology, DAPR ACLs, components, key prefixes, tests, and deployment posture remain aligned.
FR33: Epic 6 - Long-lived streams gain bounded snapshot/projection cost, sequence guards, schema evolution, identity validation, and cancellation seams.
FR34: Epic 7 - Operators gain explicit delivery semantics, recovery, audited administration, honest UI states, OpenBao secrets, health, and integration evidence.
FR35: Epic 7 - Deferred GDPR erasure, Admin OIDC, aggregate test kit, and generator-hardening capabilities remain explicitly tracked.
FR36: Epics 1 and 3 - Epic 1 closes source/package projection-query parity; Epic 3 closes exact-lineage deployed-runtime parity without treating rejected `v3.94.1` as authorization.
FR37: Epic 8 - Domains can opt into the shared payload-protection engine only after specification, implementation, production-backend, compatibility, release, rollback, and G5 evidence.

## Epic List

### Epic 1: Domain Authors Can Ship Domain-Centric Services
Domain authors can build complete EventStore-backed services without recreating hosting, query, projection, persistence, cursor, telemetry, health, or Aspire infrastructure; existing consuming modules may remove equivalent local projection/query infrastructure only after owner-approved source/package parity.
**Primary users:** Domain authors, EventStore platform maintainers, and consuming-module owners
**FRs covered:** FR1-FR10, FR36 source/package parity
**Cross-cutting coverage:** NFR6, NFR8, NFR12, NFR16
**Implementation notes:** Deliver a complete canonical host and SDK adoption path before downstream integration work. FR36 closure requires persisted production-path evidence, an exact approved EventStore SHA, and owner review; it is not implied by API presence.

### Epic 2: API and UI Developers Get Safe Integration Surfaces
Developers can expose typed external REST APIs and build interactive clients through supported gateway contracts while preserving metadata, scoping, and projection truth.
**Primary users:** External API host developers, interactive UI developers, and domain integrators
**FRs covered:** FR11-FR16
**Cross-cutting coverage:** NFR2, NFR5, NFR12-NFR16; UX-DR42, with shared consumer-flow acceptance coverage for UX-DR20, UX-DR25-UX-DR27, UX-DR30, UX-DR38, and UX-DR40
**Implementation notes:** Build on the completed gateway and metadata seams from Epic 1, but deliver dedicated external API hosts and client-only interactive hosts as a complete, independently usable integration pattern. Preserve route-bound query provenance, gateway-authoritative absolute-or-absent command-status locations, and handler-owned replacement of outbound DAPR control-plane headers.

### Epic 3: Maintainers Can Release Reproducible, Verifiable Artifacts
Maintainers can build, test, package, publish, and verify EventStore independently of local checkout state, reject invalid candidates without granting authority, and prove exact package and deployed-runtime lineage for a conforming release.
**Primary users:** Release maintainers, platform maintainers, deployment operators, and consuming-module owners
**FRs covered:** FR17-FR22, FR25, FR36 deployed-runtime parity
**Cross-cutting coverage:** NFR9-NFR11, NFR16-NFR17
**Implementation notes:** Repository and release reliability delivers value independently of later runtime work. `v3.94.1` remains immutable rejected evidence; only the separately authorized corrective release plus independent Story 3.15 verification may establish positive deployed-runtime parity. Planning, implementation, approval of this epic, and story completion never authorize an external publication; each external release mutation requires its separately bound durable authority record.

### Epic 4: Operators Can Trust Command and Event Integrity
Operators can rely on stable event identity, durable idempotency admission, deterministic replay, crash recovery, and evidence-driven append behavior under concurrency and failure.
**Primary users:** Platform operators, domain authors, and reliability engineers
**FRs covered:** FR23, FR24, FR27, FR29-FR31
**Cross-cutting coverage:** NFR6-NFR7, NFR16
**Implementation notes:** Apply the approved OQ8 authority and its strict internal sequence. Global-position sharding and append fencing remain specification/evidence-first; no later story may retroactively make an earlier unsafe outcome executable.

### Epic 5: Tenants and Administrators Are Protected by Fail-Closed Boundaries
Tenants and administrators receive consistent fail-closed authentication, authorization, tenant isolation, internal endpoint protection, and runtime topology enforcement.
**Primary users:** Tenant administrators, security engineers, and platform operators
**FRs covered:** FR26, FR28, FR32
**Cross-cutting coverage:** NFR1-NFR4, NFR16-NFR17
**Implementation notes:** Land Phase 0 safe fixes before any dependent surface regardless of epic numbering. Treat application authorization, AppHost topology, DAPR YAML, scopes, ACLs, and denial evidence as one aligned security posture.

### Epic 6: Long-Lived Streams Remain Efficient and Evolvable
Platform users can operate growing event streams with bounded snapshot/projection cost, sequence-safe updates, schema evolution, identity validation, and cancellation-aware APIs.
**Primary users:** Domain authors, EventStore maintainers, and operators of long-lived streams
**FRs covered:** FR33
**Cross-cutting coverage:** NFR8, NFR12, NFR18
**Implementation notes:** Keep folded snapshots, projection cost/sequence behavior, and event versioning/upcasting behind their named approved specifications. Each implementation slice must preserve prior correctness and compatibility guarantees.

### Epic 7: Operators Can Diagnose, Recover, and Administer Honestly
Operators can inspect delivery and projection evidence, recover poison events, use an accessible consolidated Admin UI, retrieve production secrets safely, and distinguish implemented, unavailable, accepted, and confirmed operations.
**Primary users:** Administrators, platform operators, support engineers, and incident responders
**FRs covered:** FR34, FR35
**Cross-cutting coverage:** NFR1-NFR2, NFR4-NFR6, NFR14-NFR17; primary implementation ownership for UX-DR1-UX-DR41
**Implementation notes:** Retain one cohesive operator outcome, but decompose it into small journey-focused stories for delivery, recovery, Admin UI, OpenBao, deployment, integration evidence, and backlog visibility. Each story owns one operator journey or one bounded infrastructure/evidence contract that fits a single development-agent context. Never recreate an oversized multi-concern story or render deferred work as functional.

### Epic 8: Domains Can Opt Into Portable Payload Protection - Post-MVP
Domain modules can opt into an EventStore-owned, provider-neutral payload-protection engine with stable formats, production backend proof, compatibility, release provenance, and rollback evidence.
**Primary users:** Domain owners, security architects, EventStore maintainers, and provider/operators
**FRs covered:** FR37
**Cross-cutting coverage:** NFR1-NFR4, NFR7, NFR9-NFR12, NFR16-NFR17, NFR19
**Implementation notes:** This post-MVP epic is strictly sequential: the approved security specification authorizes implementation, predecessor evidence gates every later slice, and Story 8.11 alone may close G5 after production-backend, golden, dual-provider, release, and rollback proof.

**Sequencing rule:** Epic numbers organize product outcomes; they do not grant blanket execution authority. Architecture decisions, safety prerequisites, exact evidence gates, and backward-only story dependencies govern implementation order. Relevant Epic 5 Phase 0 protections must precede exposed or administrative surfaces even when those surfaces have lower epic numbers.

**Historical continuity rule:** Before creating stories, compare the Git `HEAD` version of the former `epics.md` and the dated story-ID migrations as historical identity and omission-detection references, never as bulk-restoration sources. Account for all 107 historical stories as retained, corrected, superseded, or intentionally replaced; no story silently disappears. Preserve valid story IDs, supersession records, named evidence gates, and the explicit 3.13-3.15, 4.9-4.15, 7.14/7.19/7.20, and 8.1-8.11 sequences, but never carry forward a completion, approval, or authorization claim contradicted by current authority. The five confirmed input documents remain requirements authority; every conflict is recorded and surfaced for review rather than silently resolved from historical text.

**FR36 completion rule:** FR36 is complete only when Epic 1 source/package parity and Epic 3 positive deployed-runtime parity are both complete under their distinct evidence and approval gates. Neither half, and no rejected release candidate, closes the whole requirement.

**MVP boundary rule:** No Phase 4 MVP epic, story, readiness gate, or completion claim may depend on Epic 8. Epic 8 remains a separately gated post-MVP commitment.

**Operator value-chain rule:** Epic 4 produces trustworthy command/event evidence, Epic 5 protects that evidence and its access paths, and Epic 7 presents and acts on it. Each epic must deliver its own complete outcome without weakening or claiming completion for the others.

**Story traceability rule:** Every story must name its FR, NFR, UX-DR, and architecture-constraint coverage, plus the focused validation evidence required for acceptance. Step 3 must establish complete story-level coverage before the epic plan can claim completion; epic-level coverage never substitutes for story-level ownership or proof.

**External-authority rule:** When release, security, platform, test, consumer-owner, or provider/operator approval is unavailable, the story records the exact approval as an explicit gate or blocker. Planning never assumes that future approval will be granted and never substitutes self-declared roles, booleans, or story completion for authenticated authority.

**Primary-ownership rule:** Every FR, NFR, and UX-DR has exactly one primary story owner. Supporting stories identify themselves as supporting coverage and cannot independently close the requirement; duplicated cross-cutting evidence never creates ambiguous completion authority.

**Dependency rule:** Every story declares explicit backward-only prerequisites, including cross-epic safety prerequisites where required. Epic numbers are organizational labels and must not be interpreted as the dependency graph.

**Boundary-proof rule:** Epics 1, 2, 4, and 5 must deliver early producer/consumer contract evidence for query metadata, provenance, command status, event/recovery evidence, and fail-closed denial semantics before Epic 7 integrates or presents those contracts.

**Evidence-state rule:** `evidence-ready` is distinct from `externally-authorized` and `mutation-complete`. Preparing or validating evidence never authorizes publication, deployment, consumer infrastructure removal, or another external mutation; only the exact required authority plus successful bound mutation reaches the latter state.

**Post-MVP additive rule:** Epic 8 remains additive, opt-in, and disabled by default. Phase 4 MVP code must not require payload-protection engine packages, provider credentials, or engine runtime services, and the existing no-op/default behavior remains valid until explicit registration.

**Source-drift rule:** The frontmatter records SHA-256 digests for all five confirmed input documents before story generation. Final validation recomputes every digest and fails visibly on drift; changed inputs require reconciliation and renewed approval rather than silent continuation.

## Epic 1: Domain Authors Can Ship Domain-Centric Services

Domain authors can build complete EventStore-backed services without recreating hosting, query, projection, persistence, cursor, telemetry, health, or Aspire infrastructure; existing consuming modules may remove equivalent local projection/query infrastructure only after owner-approved source/package parity.

### Story 1.1: Canonical Domain-Service SDK Host

As a domain author,
I want a canonical EventStore domain-service SDK host,
So that I can run a domain module with platform-provided hosting and DAPR endpoints instead of hand-written boilerplate.

**Requirements coverage:** Primary FR2 and FR3; supporting FR1; no primary NFR or UX-DR ownership.

**Architecture constraints:** AD-1 and AD-2; brownfield evolution only.

**Dependencies:** None. This story establishes the first Epic 1 platform seam and has no future-story dependency.

**Historical reconciliation:** Retained as Story 1.1. The July migration and `spec-1-1-canonical-domain-service-sdk-host.md` record it as done. The regenerated scope preserves its contract-hardening evidence without treating full Sample adoption - which remains Story 1.8 - as a dependency.

**Acceptance Criteria:**

**Given** a domain-service host calls `AddEventStoreDomainService()` and `UseEventStoreDomainService()`
**When** the host is built
**Then** service defaults, EventStore activation, and domain assembly discovery are registered
**And** the host requires no hand-written request router, default endpoint, or operational-metadata wiring.

**Given** the canonical host is mapped
**When** its route table is inspected
**Then** `/`, `/health`, `/alive`, `/ready`, `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata` are present with their contractually correct HTTP methods
**And** `MapEventStoreDomainService()` remains available for advanced/manual mapping.

**Given** an application maps an exact POST-capable `/project` route before SDK mapping
**When** `MapEventStoreDomainService()` or `UseEventStoreDomainService()` runs
**Then** the SDK preserves the application route and does not create an ambiguous duplicate
**And** a GET-only `/project` route does not suppress the SDK-owned POST projection endpoint.

**Given** the Sample host is inspected as the canonical proof fixture
**When** domain-module guardrails run
**Then** it uses the DomainService SDK and Sample contracts without a direct DAPR package reference
**And** normal mode contains no hand-written router, default-endpoint, or operational-metadata plumbing while the intentional fault-injection route remains allowed.

**Given** getting-started guidance is inspected
**When** it describes the first domain-service host
**Then** it uses `AddEventStoreDomainService()` and `UseEventStoreDomainService()`
**And** it does not instruct domain authors to recreate platform infrastructure.

**Given** Story 1.1 validation runs
**When** `dotnet test tests/Hexalith.EventStore.DomainService.Tests/`, `dotnet test tests/Hexalith.EventStore.Sample.Tests/`, and `dotnet build Hexalith.EventStore.slnx --configuration Release` execute
**Then** focused host, route, guardrail, and Sample tests pass
**And** the Release build completes with zero warnings and errors.

### Story 1.2: Domain Query Routing and Response Provenance

As a domain author,
I want domain query handlers discovered and invoked with trustworthy response provenance,
So that consumers receive results through the correct route without mistaking handler-computed data for projection-backed evidence.

**Requirements coverage:** Primary FR4; supporting FR36, NFR8, and NFR16; supports UX-DR20, UX-DR21, UX-DR25, UX-DR26, and UX-DR31 at the platform-contract boundary.

**Architecture constraints:** AD-2, AD-3, AD-12, AD-14, and AD-15.

**Dependencies:** Story 1.1 only.

**Historical reconciliation:** Retained as Story 1.2 and recorded as done. This scope consolidates the former Story 1.2 routing contract and former Story 2.8 provenance contract. Generated REST and Tenants consumption remains Story 2.11; Tenants producer cleanup remains Story 4.7.

**Acceptance Criteria:**

**Given** a domain service contains one or more `IDomainQueryHandler` implementations
**When** its SDK host starts
**Then** handlers are discovered and registered by domain and query type
**And** duplicate routes fail deterministically rather than selecting the first match.

**Given** handler routes have been discovered
**When** operational index metadata is advertised and materialized by the gateway
**Then** every supported query type is included without losing command, event, aggregate, or projection metadata
**And** unavailable or invalid handler metadata causes safe fallback rather than unsafe handler routing.

**Given** a submitted query matches an advertised handler route
**When** `HandlerAwareQueryRouter` selects the execution path
**Then** it invokes the domain handler through `/query` and stamps the response `HandlerComputed`
**And** unsupported queries continue through the projection-actor route and are stamped `ProjectionBacked`.

**Given** query metadata crosses the platform
**When** it passes through `QueryResult`, `QueryRouterResult`, `SubmitQueryResult`, `SubmitQueryResponse`, and `EventStoreQueryResult`
**Then** provenance, freshness, projection version, paging, degraded state, warnings, ETag, served-at, and not-modified evidence are propagated additively
**And** existing public constructors, factories, deconstruction, DataContract payloads, and legacy payloads remain compatible.

**Given** the selected route is `HandlerComputed` or `Unknown`
**When** the gateway normalizes the response
**Then** it exposes no projection ETag, projection version, stale state, or projection-confirmed freshness
**And** it preserves unrelated authoritative evidence such as served-at, paging, degraded state, and warning codes.

**Given** the selected route is `ProjectionBacked`
**When** genuine persisted read-model evidence is returned
**Then** projection version and freshness traverse the complete production query path unchanged
**And** the HTTP ETag remains an opaque representation validator rather than being interpreted as projection version or lifecycle evidence.

**Given** a conditional request or explicit freshness policy
**When** route provenance is not authoritatively `ProjectionBacked`
**Then** the gateway does not return a projection-derived `304` or validator
**And** `RequireFresh` or `MaxStaleness` fails closed using `query_projection_stale` unless the required authoritative evidence is available.

**Given** raw and typed .NET gateway clients receive query responses
**When** provenance is present, missing, invalid, duplicated, or contradictory
**Then** canonical provenance is preserved when trustworthy and otherwise normalized to `Unknown`
**And** projection-looking evidence is removed whenever provenance cannot support it.

**Given** Story 1.2 validation runs
**When** the Release build, focused contract, client, domain-service, query-routing, server tests, and persisted-path gateway proof execute
**Then** routing, fallback, compatibility, provenance, freshness, and conditional-request behavior pass under warnings-as-errors
**And** the real-path proof covers handler execution, persisted projection evidence, strong ETag/`304`, and legacy provenance defaulting to `Unknown`.

### Story 1.3: Persisted Read-Model Store and Write Policy

As a domain projection author,
I want a platform-owned persisted read-model store and write policy,
So that ETag-aware reads, writes, and bounded merge retries behave consistently without domain-owned DAPR state wrappers.

**Requirements coverage:** Supports primary FR5 and FR36; supporting NFR7 and NFR16.

**Architecture constraints:** AD-2, AD-12, and AD-14; additive public contracts and optimistic concurrency.

**Dependencies:** Story 1.1. Story 1.2 metadata behavior must remain compatible but is not an implementation prerequisite.

**Historical reconciliation:** Retained as Story 1.3 and recorded as done. The former oversized generic-read-model-and-cursor scope is divided across Stories 1.3, 1.4, and 1.5 without reopening completed behavior.

**Acceptance Criteria:**

**Given** a projection or query handler uses `IReadModelStore`
**When** it reads, creates, or replaces a read-model entry
**Then** the DAPR adapter preserves keys, values, ETags, cancellation, and first-write concurrency
**And** concurrency conflicts return a deterministic unsuccessful result while infrastructure failures propagate.

**Given** a domain author uses `ReadModelWritePolicy`
**When** an aggregate entry or singleton/index entry is updated, applied, or merged
**Then** aggregate writes observe ETag concurrency and index merges use a bounded retry budget
**And** persistent conflict exhaustion is reported deterministically rather than retried without limit.

**Given** the public registration seam is used
**When** services resolve the production store and write policy
**Then** the contracts remain additive, independently configurable, and cancellation-aware
**And** no consumer-domain or Tenants source change is required.

**Given** Story 1.3 validation runs
**When** success, first write, conflict, retry, retry exhaustion, cancellation, and DAPR failure are exercised
**Then** focused client tests verify observable results and stored state rather than mock call counts alone
**And** `dotnet test tests/Hexalith.EventStore.Client.Tests/ --configuration Release` and `dotnet build Hexalith.EventStore.slnx --configuration Release` pass under warnings-as-errors.

### Story 1.4: Deterministic Read-Model Testing Fake

As a domain test author,
I want an in-memory read-model fake with production-equivalent observable semantics,
So that conflict, retry, partial-failure, and JSON round-trip behavior can be tested without live DAPR infrastructure.

**Requirements coverage:** Supports primary FR5 and FR36; supporting NFR16.

**Architecture constraints:** AD-12; test doubles must reproduce the public storage contract without being represented as live integration evidence.

**Dependencies:** Story 1.3.

**Historical reconciliation:** Retained as Story 1.4 and recorded as done. This is the deterministic-testing portion separated from the former Story 1.3 composite scope.

**Acceptance Criteria:**

**Given** a test uses the platform in-memory read-model store
**When** entries are saved, read, replaced, or deleted
**Then** first-write and ETag behavior match the production store contract
**And** values cross a JSON serialization boundary rather than sharing mutable object references.

**Given** a test configures a conflict or partial failure
**When** the named read-model operation and attempt execute
**Then** the failure occurs deterministically at that boundary and retry behavior is reproducible
**And** unrelated keys, operations, and attempts remain unaffected.

**Given** fake and production contract scenarios are compared
**When** success, concurrency conflict, retry exhaustion, cancellation, and deletion are exercised
**Then** their public outcomes agree wherever the shared contract applies
**And** fake-only proof is explicitly distinguished from persisted-path or live DAPR evidence.

**Given** Story 1.4 validation runs
**When** `dotnet test tests/Hexalith.EventStore.Testing.Tests/ --configuration Release` and the focused in-memory store tests in `tests/Hexalith.EventStore.Client.Tests/` execute
**Then** deterministic storage, cloning, ETag, and fault-injection tests pass under warnings-as-errors.

### Story 1.5: Protected Query Cursor Codec

As a domain query author,
I want a reusable protected query cursor codec,
So that paged queries preserve opaque scope without exposing or trusting client-controlled cursor internals.

**Requirements coverage:** Primary FR6; supporting NFR2 and NFR16.

**Architecture constraints:** AD-10, AD-12, and AD-14; opaque, bounded, purpose-isolated cursor contracts.

**Dependencies:** Story 1.2 for metadata carriage and Story 1.3 for the related domain-author registration surface.

**Historical reconciliation:** Retained as Story 1.5 and recorded as done. This is the cursor and paging-contract portion separated from the former Story 1.3 composite scope.

**Acceptance Criteria:**

**Given** a handler encodes a cursor using `IQueryCursorCodec` and `QueryCursorScope`
**When** the cursor is decoded using the same caller-supplied Data Protection purpose and scope
**Then** the bounded payload round-trips without revealing its protected internals
**And** registration remains opt-in because the platform cannot safely invent a cross-domain protection purpose.

**Given** cursor input has the wrong tenant, domain, query type, purpose, or key ring
**When** it is decoded
**Then** wrong scope, malformed input, tampering, oversize payload, and key rotation fail safely
**And** no protected payload, decoded position, or raw cursor is disclosed or logged.

**Given** a producer supplies paging evidence
**When** it crosses query contracts and typed or untyped client results
**Then** effective page size, offset or next cursor, total count when known, and nullable `HasMore` remain producer-authored
**And** gateway request paging is never promoted to authoritative result evidence.

**Given** cursor-only paging enters the generic query gateway
**When** validation and downstream routing execute
**Then** the request may reach the responsible handler or projection adapter for domain-specific validation
**And** cursor-plus-offset, negative offset, invalid page size, and oversized cursor inputs are rejected.

**Given** cursor input is rejected
**When** the query surface creates Problem Details and records diagnostics
**Then** it uses the support-safe `query_invalid_page` taxonomy and `invalid-cursor` category
**And** cursor, scope, position, protected payload, and ETag internals remain absent from responses and logs.

**Given** Story 1.5 validation runs
**When** cursor, scope, registration, contract, gateway-client, validation, and Problem Details tests execute in their focused projects
**Then** opacity, compatibility, scope isolation, paging evidence, and safe-failure behavior pass under warnings-as-errors.

### Story 1.6: Projection and Domain Event Consumer Seams

As a domain author,
I want platform seams for projection dispatch and domain-event consumption,
So that I can keep projection and subscription behavior domain-specific while reusing platform plumbing.

**Requirements coverage:** Supports primary FR7; NFR6 is completed by Story 1.18.

**Architecture constraints:** AD-2 and AD-9; Client remains ASP.NET-free and endpoint mapping remains in the DomainService SDK.

**Dependencies:** Story 1.1.

**Historical reconciliation:** Retained as Story 1.6 and recorded as done. Later stories harden asynchronous dispatch and handler-delivery idempotency without replacing these authoring seams.

**Acceptance Criteria:**

**Given** a domain implements `IDomainProjectionHandler`
**When** the SDK maps `/project`
**Then** projection requests are dispatched to the matching handler
**And** the SDK yields when the application already owns an exact POST-capable `/project` route.

**Given** a domain consumes events from EventStore pub/sub
**When** it registers platform domain-event handlers and maps the domain-event endpoint
**Then** the platform supplies the event envelope, consumer context, handler dispatch, marker-based deduplication, and endpoint mapping
**And** domain code supplies only handler logic and domain-specific options.

**Given** payload aggregate identity validation is configured
**When** an event reaches a consumer
**Then** the configured payload aggregate identity is validated before domain side effects
**And** invalid or duplicate deliveries are classified consistently with at-least-once delivery semantics.

**Given** projection and subscription seam tests execute
**When** dispatch, custom-route yielding, registration, deduplication, cancellation, and endpoint mapping are exercised
**Then** observable behavior matches the public SDK contract
**And** dependency guardrails prove the Client assembly remains independent of ASP.NET hosting packages.

### Story 1.7: Domain Module Hosting Observability

As a platform operator,
I want domain modules to use shared Aspire, telemetry, and health-check conventions,
So that local topology, diagnostics, and health behavior are consistent across every domain.

**Requirements coverage:** Primary FR8.

**Architecture constraints:** AD-2 and AD-13; domain-sensitive telemetry and explicit infrastructure readiness.

**Dependencies:** Story 1.1.

**Historical reconciliation:** Retained as Story 1.7 and recorded as done. This preserves the former hosting-observability work while leaving broad operational telemetry ownership with Epic 7.

**Acceptance Criteria:**

**Given** an AppHost adds a domain module through the public EventStore Aspire extension
**When** the shared or isolated module topology is built
**Then** the domain receives the expected DAPR sidecar, service invocation, and intentionally selected state-store or pub/sub references
**And** isolated modules are not silently granted shared infrastructure access.

**Given** a domain-service host discovers one or more domains
**When** platform diagnostics are registered and a request is admitted
**Then** ActivitySource and Meter names follow the shared domain convention and telemetry resolves by request domain
**And** an unknown or blank domain never emits through another domain's diagnostics.

**Given** a domain explicitly registers the DAPR state-store health check
**When** the dependency probe succeeds or fails
**Then** health reports `Healthy` or `Unhealthy` using the conventional registration name
**And** the probe reveals no state payload or sensitive infrastructure detail.

**Given** a domain-module DAPR sidecar is configured by the Aspire extension
**When** application-health options are inspected
**Then** the platform supplies the canonical health endpoint contract consistently in shared and isolated modes
**And** lower-level explicit configuration remains able to override platform defaults where required.

**Given** Story 1.7 validation runs
**When** `dotnet test tests/Hexalith.EventStore.DomainService.Tests/`, `dotnet test tests/Hexalith.EventStore.AppHost.Tests/`, and the Release build execute
**Then** domain diagnostics, health probes, and Aspire topology tests pass with zero warnings and errors.

### Story 1.8: Sample Domain-Centric Adoption

As a platform maintainer,
I want the Sample domain to be the minimal domain-centric reference,
So that domain authors can see platform SDK adoption without copied hosting or infrastructure.

**Requirements coverage:** Supports primary FR9, NFR14, and UX-DR42.

**Architecture constraints:** AD-1, AD-2, and AD-12; the Sample demonstrates the preferred path while remaining a real executable proof fixture.

**Dependencies:** Stories 1.1, 1.2, 1.5, 1.6, and 1.7.

**Historical reconciliation:** Retained as Story 1.8 and recorded as done. This story owns Sample adoption only; Tenants adoption is preserved separately in Stories 1.9 and 1.10.

**Acceptance Criteria:**

**Given** the Sample domain adopts the canonical host, projection, query, and event-consumer seams
**When** its source and project graph are inspected
**Then** it contains domain behavior, contracts, and intentional demonstration code only
**And** hand-written request routing, operational metadata, ServiceDefaults, Aspire, state-store, cursor, telemetry, health, and direct DAPR plumbing are absent.

**Given** Sample command processing executes
**When** the `counter` domain receives a supported command through SDK discovery
**Then** the domain aggregate handles the command without a parallel legacy `IDomainProcessor`
**And** unknown-command behavior remains owned by the aggregate and SDK contract.

**Given** the Sample UI issues commands and queries
**When** its host boundaries are scanned
**Then** it consumes typed EventStore client seams
**And** it contains no generated or hand-written per-message MVC command or query controllers.

**Given** the isolated Sample module is composed through Aspire
**When** topology and health settings are inspected
**Then** it receives canonical sidecar and liveness behavior without shared state-store or pub/sub access
**And** intentional fault-injection behavior remains explicit and isolated from the normal reference path.

**Given** Story 1.8 validation runs
**When** focused Sample, DomainService, and AppHost tests plus the Release build execute
**Then** aggregate dispatch, query, projection, health, topology, and structural guardrails pass under warnings-as-errors.

### Story 1.9: Tenants Query and Read-Model Adoption

As a Tenants maintainer,
I want Tenants queries and read models to consume EventStore platform seams,
So that tenant RBAC, audit, pagination, and lifecycle behavior remain intact without local platform clones.

**Requirements coverage:** Supports primary FR9; supporting FR4, FR5, FR6, FR36, NFR2, NFR8, and NFR16.

**Architecture constraints:** AD-2, AD-3, AD-12, AD-14, and AD-15; the Tenants repository remains owner-controlled.

**Dependencies:** Stories 1.2, 1.3, and 1.5.

**Historical reconciliation:** Retained as Story 1.9 and recorded as done. The Tenants maintainer-approved completion evidence identifies commit `56c506c18a4c72f5fee1005948f2f9e08c2a8a5b`; no new submodule edit is authorized by this regenerated story.

**Acceptance Criteria:**

**Given** Tenants adopts `IDomainQueryHandler`, `IReadModelStore`, `ReadModelWritePolicy`, and `IQueryCursorCodec`
**When** equivalent local query and read-model infrastructure is removed
**Then** RBAC, audit, cursor scope, paging, ETag, conflicts, and persisted lifecycle behavior remain equivalent
**And** query provenance follows Story 1.2 rather than an ETag-derived alias.

**Given** query provenance is projection-backed, handler-computed, unknown, invalid, or contradictory
**When** Tenants renders membership, global-administrator, audit, or lifecycle state
**Then** authoritative projection evidence is used only for `ProjectionBacked`
**And** every other classification fails closed without presenting current-looking projection state.

**Given** tenant-scoped query and read-model paths execute
**When** persisted reads, optimistic conflicts, invalid cursors, lifecycle precedence, and cross-tenant access are exercised
**Then** tests assert persisted end state, RBAC decisions, audit evidence, and support-safe failures
**And** mock-only interaction proof cannot establish completion.

**Given** completion crosses the Tenants repository boundary
**When** Story 1.9 is marked done
**Then** evidence records the maintainer-approved commit, exact Tenants SHA, accepted scope, source/package mode, and focused validation results
**And** preparing or reviewing evidence alone does not authorize a child commit or parent submodule-pointer mutation.

**Given** Story 1.9 validation runs
**When** the scoped Tenants UI, query, read-model, cursor, RBAC, audit, and production-path suites execute in their recorded source and package modes
**Then** behavior remains equivalent at the accepted SHA and all environment limitations are reported separately from deterministic results.

### Story 1.10: Tenants Projection and Event-Consumer Adoption

As a Tenants maintainer,
I want projections and domain-event consumers to use EventStore platform dispatch and delivery seams,
So that local actor and plumbing removal preserves tenant isolation and delivery correctness.

**Requirements coverage:** Primary FR9; supporting FR7, FR36, NFR2, NFR6, and NFR16.

**Architecture constraints:** AD-2, AD-9, and AD-12; Tenants-specific persisted projection behavior and repository authority remain explicit.

**Dependencies:** Stories 1.3, 1.6, 1.7, and 1.9.

**Historical reconciliation:** Retained as Story 1.10 and recorded as done. The accepted Tenants commit is `c59a13f6dc7699c7ea48b1d4b573c1c0dbf2dbcd`; functional rollback returns to `56c506c18a4c72f5fee1005948f2f9e08c2a8a5b`. This record does not authorize a new child or gitlink mutation.

**Acceptance Criteria:**

**Given** Tenants adopts the named projection and domain-event-consumer seams
**When** equivalent local actors, marker plumbing, telemetry, and health duplication are removed
**Then** domain-specific projection logic, tenant isolation, RBAC, audit, detail/index state, and freshness timestamps remain intact
**And** the bespoke persisted `/project` behavior remains explicit until a platform seam can replace it without semantic loss.

**Given** duplicate, out-of-order, or previously failed delivery reaches the production consumer path
**When** marker acquisition, persistence, and retry execute
**Then** duplicates are idempotent, stale sequences do not corrupt state, and markers advance only after required durable work
**And** a failed save releases the marker so a later delivery can recover successfully.

**Given** one projection or consumer operation fails
**When** retry and recovery run
**Then** successful durable state is neither lost nor falsely reported as failed
**And** partial progress never advances a completion checkpoint beyond durable evidence.

**Given** completion crosses the Tenants repository boundary
**When** Story 1.10 is marked done
**Then** evidence records the maintainer-approved commit, exact SHA, accepted scope, source/package modes, persisted-path tests, and rollback boundary
**And** absence of that authority leaves local infrastructure intact and the story non-complete.

**Given** Story 1.10 validation runs
**When** scoped package- and source-mode projection, event-publication, duplicate/out-of-order, health, real-host HTTP, and persisted-state tests execute
**Then** `/project`, `/query`, operational metadata, recovery, and persisted end-state behavior pass at the accepted SHA.

### Story 1.11: Domain-Module Adoption Guardrails

As a platform maintainer,
I want enforceable domain-module architecture guardrails,
So that Sample, Tenants, and future modules cannot silently reintroduce reusable platform boilerplate.

**Requirements coverage:** Primary FR1; supporting FR9, FR10, and NFR14.

**Architecture constraints:** AD-2, AD-4, and AD-12; scans are bounded, deterministic, and repository-authority aware.

**Dependencies:** Stories 1.1–1.10 provide the platform seams and approved reference exceptions the guardrails enforce.

**Historical reconciliation:** Retained as Story 1.11 and recorded as done. Packaging governance remains separately owned by Story 1.12; broad transitional host wiring and cross-file/computed route analysis remain explicit residual boundaries.

**Acceptance Criteria:**

**Given** initialized domain-module roots are scanned
**When** a module owns a covered reusable AppHost, Aspire, ServiceDefaults, projection/query actor, cursor codec, state-store wrapper, telemetry source, health check, canonical endpoint, or per-message UI controller seam
**Then** the guardrail fails and names the EventStore platform seam that must replace it
**And** domain contracts, handlers, projections, validators, and documented narrow exceptions remain allowed.

**Given** a source scan encounters comments, string literals, qualified types, target-typed construction, grouped routes, local constants, or common receiver forms
**When** prohibited seam detection executes
**Then** matching remains deterministic across the covered C# forms
**And** allowed source text does not become a false-positive substitute for executable behavior.

**Given** a scan reaches a root-declared submodule
**When** it evaluates initialized source
**Then** it remains read-only unless maintainer authority explicitly permits a mutation
**And** it never initializes or updates nested submodules.

**Given** the Sample or Tenants transitional `/project` exception is evaluated
**When** route guardrails run
**Then** only the documented opt-in Sample fault path or Tenants `ProjectionDispatcher` mapping is allowed
**And** unrelated canonical endpoint ownership still fails the guardrail.

**Given** Story 1.11 validation runs
**When** `DomainModuleAuthoringGuardrailTests`, Sample tests, AppHost configuration tests, and read-only initialized-root scans execute
**Then** approved and prohibited fixtures produce stable, support-safe outcomes
**And** deferred broad host-wiring or cross-file route-analysis limits are not misrepresented as completed coverage.

### Story 1.12: DomainService Packaging and Governance

As a release maintainer,
I want the domain-service SDK, service defaults, documentation, and release inventory governed together,
So that the domain-centric model is reusable and difficult to regress.

**Requirements coverage:** Primary FR10.

**Architecture constraints:** AD-1, AD-2, and AD-11; the release manifest is the package inventory authority.

**Dependencies:** Stories 1.1 and 1.11.

**Historical reconciliation:** Retained from former Story 1.12 and recorded as done. Source-architecture guardrails remain Story 1.11; source/package behavioral parity remains Story 1.20 and deployed parity remains Epic 3.

**Acceptance Criteria:**

**Given** EventStore release packages are inventoried
**When** manifest-governance tests evaluate `tools/release-packages.json`
**Then** `Hexalith.EventStore.DomainService` and `Hexalith.EventStore.ServiceDefaults` have unique expected package IDs and project paths and evaluate as packable in Release package mode
**And** active documentation reflects the manifest-driven package inventory without stale package counts.

**Given** release pack and validation scripts execute
**When** a versioned package set is produced
**Then** every manifest package is present exactly as expected
**And** missing or extra `.nupkg` artifacts cause validation to fail before publication.

**Given** a future domain author reads repository instructions and project context
**When** they follow the documented platform model
**Then** canonical hosting, projection, query, read-model, cursor, Aspire, and anti-boilerplate seams are described consistently
**And** generated REST APIs are assigned to dedicated external API hosts rather than interactive UI hosts.

**Given** Story 1.12 validation runs
**When** package-manifest tests, an actual manifest-driven pack, exact output validation, and `dotnet build Hexalith.EventStore.slnx --configuration Release` execute
**Then** the governed package set is reproducible and the build completes with zero warnings and errors.

### Story 1.13: Projection and Query SDK Owner Parity Proof

As an EventStore platform owner,
I want reviewed proof that the projection and query SDK can replace a non-trivial domain's local mechanics,
So that consuming modules delete rollback code only after evidence demonstrates parity.

**Requirements coverage:** Investigative support for FR4, FR5, FR6, FR7, FR9, NFR8, and NFR16; it owns no final capability closure.

**Architecture constraints:** AD-2, AD-12, and the external-authority, boundary-proof, and evidence-state rules.

**Dependencies:** Stories 1.1–1.12 provide the SDK surfaces being assessed.

**Historical reconciliation:** Retained as a completed investigation/proof story. Its `done` status means the assessment and blocked proof packet were completed; it does not claim parity was available. Stories 1.14–1.20 own the identified implementation and authorization gaps.

**Acceptance Criteria:**

**Given** a consuming domain requests projection/query replacement proof
**When** the owner assessment starts
**Then** it records the exact EventStore commit and cites the source and tests for projection handlers, query handlers, read-model storage, write policy, cursor scope, and registration APIs
**And** it distinguishes checked-out source from a maintainer-approved consumable pin.

**Given** each required parity item is evaluated
**When** G3 erasure, G10 batching or an approved equivalent, G6 freshness mapping, duplicate/out-of-order replay, full rebuild equivalence, cursor compatibility, and the intended pin are inspected
**Then** each is classified as `already available`, `additive API/test added`, or `blocked`
**And** claims rely on observable source and test evidence rather than interface names alone.

**Given** any required proof item is unsatisfied
**When** the proof packet is finalized
**Then** its decision is `still blocked`, the missing API or behavior is named precisely, and no consuming migration is authorized
**And** completed investigation status cannot be mistaken for completed platform parity.

**Given** an identified gap needs implementation
**When** follow-up work is defined
**Then** it is assigned to Stories 1.14–1.20 as a generic EventStore capability
**And** consumer-specific domain logic is not added to EventStore.

**Given** every parity item later becomes satisfied
**When** final availability evidence is produced
**Then** it records source paths, test paths, commands/results, approval source, exact SHA, rollback note, limitations, and the `available` decision
**And** each consuming repository must still verify its own pin before deleting local infrastructure.

### Story 1.14: Read-Model and Projection Checkpoint Erasure

As a domain projection maintainer,
I want coordinated erasure of aggregate-owned read models and projection checkpoints,
So that recreating an aggregate identifier does not leave stale projection state that suppresses valid future delivery.

**Requirements coverage:** Supports primary FR5 and FR36; supporting NFR2 and NFR16.

**Architecture constraints:** AD-2, AD-7, AD-8, AD-10, and AD-12; destructive lifecycle work is fail-closed, scoped, resumable, and evidence-backed.

**Dependencies:** Stories 1.3 and 1.13.

**Historical reconciliation:** Retained as Story 1.14 and recorded as done under the human-amended lifecycle contract. Released store/checkpoint interfaces remain unchanged; Redis is resumable-only; caller-listed registered slots are authoritative; no-canonical-writer returns `Unsupported`; and the disclosed in-flight write race and full Aspire cross-replica proof remain tracked follow-ups rather than hidden completion claims.

**Acceptance Criteria:**

**Given** a concrete store opts into `IReadModelConditionalEraser`
**When** a present aggregate-owned value is erased using its internally read current ETag
**Then** the value becomes absent, an already-absent value completes idempotently, and a stale ETag leaves the newer value intact with a conflict outcome
**And** existing implementations of released `IReadModelStore` and checkpoint interfaces remain source- and binary-compatible.

**Given** a GlobalAdministrator requests coordinated erasure
**When** tenant, domain, aggregate, projection, logical slot IDs, and stable operation ID are validated
**Then** the platform derives physical addresses only from registered canonical aggregate-owned slots
**And** raw keys, stores, ETags, legacy/opaque targets, shared indexes, cross-scope targets, and slots without a canonical writer are rejected before mutation.

**Given** an authorized erase begins without an active domain rebuild
**When** its caller-authoritative target manifest is processed
**Then** read-model targets, the aggregate-specific rebuild checkpoint, and the projection-scoped delivery checkpoint are erased in that order with durable progress
**And** operator-wide rebuild state, active indexes, other projections, and other tenants remain unchanged.

**Given** interruption, cancellation, conflict, ambiguous transport failure, or a repeated operation ID
**When** the persisted projection-lifecycle actor processes or resumes the operation
**Then** it reclassifies state, converges from recorded progress, never reports false success, and distinguishes denied, unsupported, active-rebuild, conflict, incomplete, canceled, and unknown outcomes
**And** a different operation ID conflicts while an erase is active.

**Given** the repository Redis/DAPR backend executes coordinated erasure
**When** targets span multiple persisted operations
**Then** the platform uses the durable resumable protocol and never claims transaction rollback or atomic completion from same-store placement or advertised `TRANSACTIONAL` capability
**And** live persisted evidence verifies the supported recovery mechanism.

**Given** tenant A and tenant B contain persisted state
**When** tenant A requests erasure through the authenticated Admin surface
**Then** GlobalAdministrator authorization is enforced before target resolution, tenant B remains unchanged and undisclosed, and ordinary Operators are denied
**And** the public request contains no physical storage coordinates.

**Given** a fresh aggregate identity has a stale projection-scoped delivery checkpoint
**When** authorized erasure completes and the first normal command creates sequence one
**Then** projection state is persisted without checkpoint-drift suppression
**And** the proof never deletes an event stream, snapshot, broker history, backup, audit evidence, shared index, or cryptographic key.

**Given** Story 1.14 validation runs
**When** focused Client, Testing, Server, Admin, DomainService, and live-sidecar persisted-state lanes execute
**Then** compatibility, canonical ownership, tenant isolation, resume behavior, checkpoint recovery, and domain-module guardrails pass
**And** waived Tier-3 and in-flight write-fence limitations remain visibly tracked rather than promoted to evidence.

### Story 1.15: Coordinated Read-Model Batch Writes

As a projection author,
I want coordinated detail and index writes,
So that a projection cannot expose an updated detail model with a missing or inconsistent index entry.

**Requirements coverage:** Primary FR5; supporting FR36, NFR7, and NFR16.

**Architecture constraints:** AD-2, AD-7, AD-8, and AD-12; batching is additive, same-component, and truthfully qualified by backend evidence.

**Dependencies:** Stories 1.3 and 1.13.

**Historical reconciliation:** Retained as Story 1.15 and recorded as done. Its Tier-3 live-sidecar lane was initially environment-blocked and correctly hard-gated production wiring; Story 1.17 later executed that batch lane successfully as part of its 34/34 live-sidecar evidence. The earlier deterministic results are still not misrepresented as the persisted proof.

**Acceptance Criteria:**

**Given** one projection delivery creates a bounded same-store batch
**When** `IReadModelBatchStore` validates the immutable manifest
**Then** every ordered write or delete preserves key, value or deletion intent, type, concurrency input, and canonical bytes
**And** empty, duplicate-key, mixed-store, invalid-identity, invalid-ETag, or configured-limit violations fail before state access while existing single-key APIs remain compatible.

**Given** a store is explicitly configured and live-qualified as transaction-safe
**When** a batch executes through the transaction-qualified profile
**Then** one ordered transaction carries logical operations and completion evidence and success requires persisted read-back verification
**And** same-store placement, DAPR capability metadata, or a void SDK response alone never qualifies the backend.

**Given** a store uses the default resumable profile
**When** execution is interrupted between operations
**Then** uncommitted candidates remain invisible through the platform read seam until the commit marker is durable
**And** retry with the same identity reconciles marker and envelope state without describing partial persistence as atomic or complete.

**Given** failure, optimistic conflict, cancellation, timeout, or ambiguous transport outcome occurs after possible dispatch
**When** execution returns
**Then** bounded caller-token-independent reconciliation produces `Completed`, `AlreadyCompleted`, `Conflict`, `Incomplete`, or `Indeterminate` as supported by evidence
**And** it never treats cancellation as rollback or independently retries detail and index values.

**Given** a stable batch identity is retried
**When** its versioned canonical fingerprint matches a terminal receipt
**Then** the result is idempotent already-completed success without reapplication
**And** reuse of the identity with a different ordered manifest fails before new logical mutation.

**Given** a batch reports completed success
**When** state is inspected through the configured platform seam
**Then** every detail/index operation and terminal receipt has the required durable logical state
**And** delivery and rebuild checkpoints remain unchanged because their advancement belongs to later dispatch stories.

**Given** deterministic fake and DAPR-adapter tests execute
**When** success, conflict, duplicate identity, cancellation, ambiguity, abort, compaction, and injected partial failure are exercised
**Then** both implementations expose equivalent observable outcomes and inspect stored values, envelopes, receipts, and unchanged checkpoints
**And** recorder calls are treated only as request-shape evidence.

**Given** production wiring depends on persisted backend semantics
**When** the Tier-3 batch live-sidecar lane executes in a suitable environment
**Then** Redis detail, index, envelope/receipt, cancellation, recovery, and unchanged-checkpoint end states are inspected directly before wiring is authorized
**And** Story 1.17's later successful execution closes the originally recorded environment gate without rewriting its historical blocker.

### Story 1.16: Complete Projection Freshness Lifecycle

As a projection and query consumer,
I want a complete, provenance-safe projection lifecycle contract,
So that operational states are not collapsed into a stale Boolean or inferred from an ETag.

**Requirements coverage:** Supports FR4, FR36, NFR8, NFR15, UX-DR20, UX-DR21, UX-DR25, UX-DR26, and UX-DR31.

**Architecture constraints:** AD-8, AD-12, AD-14, and AD-15; lifecycle is explicit evidence transported only with authoritative projection provenance.

**Dependencies:** Stories 1.2 and 1.14.

**Historical reconciliation:** Retained as Story 1.16 and recorded as done. Corrected runtime `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` received durable reviewer approval from `jpiquot`; the documented erase-query visibility limitation remains separately tracked.

**Acceptance Criteria:**

**Given** the public lifecycle contract is serialized or deserialized
**When** exact or unsafe wire input is processed
**Then** stable values `Unknown = 0`, `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` round-trip by exact canonical name
**And** missing, null, numeric, case-variant, object, array, or unknown input fails safely to `Unknown` without changing released constructor or enum compatibility.

**Given** persisted freshness or explicit operational evidence exists
**When** lifecycle is classified
**Then** current/aging timestamps map to `Current`, stale timestamps map to `Stale`, and directly observed rebuild, degraded dependency, unavailable storage, or local fallback evidence maps to its explicit state
**And** lifecycle is never inferred from ETag, HTTP success, payload fields, SignalR, `IsStale`, or `IsDegraded`.

**Given** query route provenance is known
**When** lifecycle crosses routers, controller, clients, generated REST, or testing fakes
**Then** only `ProjectionBacked` may retain a non-unknown authoritative lifecycle
**And** handler-computed, missing, invalid, cached, or contradictory evidence normalizes to `Unknown` without discarding unrelated safe metadata.

**Given** lifecycle is projected into legacy compatibility fields
**When** old consumers inspect `IsStale` and `IsDegraded`
**Then** `Current` may produce `IsStale=false`, `Stale` may produce `IsStale=true`, and other states do not fabricate stale/current evidence
**And** compatibility Booleans never create a lifecycle state.

**Given** a projection-backed `200` or bodyless `304` is returned
**When** lifecycle metadata and `X-Hexalith-Projection-Lifecycle` are reconciled
**Then** one exact bounded canonical value is preserved when authoritative
**And** missing, duplicate, invalid, mismatched, unknown, or non-projection headers fail closed.

**Given** consumer authorization and each lifecycle/provenance combination are evaluated
**When** the default EventStore mutation policy runs
**Then** only otherwise-authorized, projection-backed `Current` permits mutation
**And** `LocalOnly` never counts as projection-confirmed success.

**Given** persisted lifecycle may change while a query executes
**When** the query router checks the lifecycle epoch and retries for coherence
**Then** a monotonic persisted epoch prevents an absent-idle ABA from validating stale payload
**And** cancellation propagates while persistent lifecycle-store failure fails closed without executing every successful query twice.

**Given** Story 1.16 validation runs
**When** focused Contracts, Client, QueryRouting, Server, generator, Sample, Testing, and exact-runtime live-sidecar lanes execute
**Then** serialization, provenance gating, cache invalidation, lifecycle policy, persisted propagation, and compatibility pass under warnings-as-errors.

### Story 1.17: Asynchronous Multi-Projection Dispatch

As a domain projection author,
I want asynchronous named projection handlers with one-to-many dispatch,
So that one domain can durably maintain detail and index projections through platform seams.

**Requirements coverage:** Primary FR7 and NFR12; supporting FR36, NFR7, and NFR16.

**Architecture constraints:** AD-2, AD-7, AD-8, AD-12, AD-19, and AD-20; dispatch identity, route catalogs, outcomes, retries, and checkpoint advancement are explicit platform contracts.

**Dependencies:** Stories 1.6, 1.14, 1.15, and 1.16.

**Historical reconciliation:** Retained as Story 1.17 and recorded as done. The implementation preserves legacy v1 behavior, adds the versioned v2 path, incorporates 27 follow-up review patches, and records 34/34 live-sidecar tests including the previously gated Story 1.15 batch evidence.

**Acceptance Criteria:**

**Given** a named projection handler performs persistence
**When** it handles a request
**Then** `IAsyncDomainProjectionHandler` is asynchronous and cancellation-aware, receives the stable dispatch identity, and may await read-model stores, write policy, or coordinated batches
**And** completion is reported only after required durable work finishes and production awaits use `ConfigureAwait(false)`.

**Given** named handlers and operational metadata are registered
**When** the route catalog is validated and published
**Then** handlers are uniquely keyed by canonical `(Domain, ProjectionType)`, multiple projection types may share a domain, duplicate pairs fail deterministically, and observable routes use ordinal order
**And** one immutable catalog snapshot binds exact routes, app ID, service version, capability, and fingerprint to persisted indexes and runtime dispatch.

**Given** normal delivery selects named persistence routes
**When** pre-persistence admission runs
**Then** each projection passes projection-scoped checkpoint drift and lifecycle admission before remote invocation
**And** the v2 request contains only admitted projection names, the stable event-derived dispatch ID, and the exact catalog fingerprint.

**Given** `/project/v2` dispatches one request to multiple handlers
**When** handler results are normalized
**Then** the version-2 response contains one bounded, ordinal outcome for every admitted route using the closed `Completed`, `AlreadyCompleted`, `Retryable`, `Indeterminate`, or `Failed` status
**And** unexpected non-cancellation exceptions become support-safe `Indeterminate` outcomes while request cancellation propagates without fabricated results.

**Given** one handler completes durably and a sibling fails or remains uncertain
**When** the server reconciles outcomes
**Then** only the successful projection may complete any required legacy actor write and advance its own checkpoint
**And** retryable, indeterminate, failed, missing, duplicate, unrequested, malformed, or transport-interrupted outcomes advance no checkpoint.

**Given** immediate-mode partial work remains incomplete
**When** the durable retry scheduler and hosted worker run
**Then** payload-free sharded work records reload history through the recorded head, verify its message identity, reuse the dispatch ID and catalog fingerprint, and converge completed siblings idempotently
**And** terminal or exhausted work remains bounded and operator-visible rather than being acknowledged as success.

**Given** legacy synchronous consumers or rebuilds remain
**When** the additive v2 contract ships
**Then** the v1 `/project` request/response and explicitly mapped compatibility paths remain unchanged
**And** named persistence handlers are not invoked from the legacy paged rebuild path until Story 1.19 supplies safe staging and replay semantics.

**Given** Story 1.17 validation runs
**When** Contracts, Client, DomainService, AppHost, Server, and live DAPR/Redis lanes execute
**Then** deterministic fan-out, compatibility, partial failure, retry recreation, persisted detail/index state, durable receipts, independent checkpoints, and idempotent convergence pass under warnings-as-errors
**And** runtime versions and post-test process cleanup are recorded.

### Story 1.18: Projection-Handler Delivery Idempotency

As an operator,
I want projection delivery to be duplicate-safe and order-safe through the real handler path,
So that at-least-once unordered delivery cannot corrupt detail or index state.

**Requirements coverage:** Primary NFR6; supporting FR7, FR36, NFR7, and NFR16.

**Architecture constraints:** AD-7, AD-8, AD-10, AD-12, AD-19, and AD-20; persisted projection-scoped identity and conditional state transitions are authoritative.

**Dependencies:** Stories 1.14, 1.15, and 1.17.

**Historical reconciliation:** Retained as Story 1.18 and recorded as done. It evolves the scoped checkpoint into the version-2 delivery-state contract, preserves released interfaces and the frozen v2 dispatch envelope, and records the completed real DAPR/Redis evidence and maintenance-cutover runbook.

**Acceptance Criteria:**

**Given** a named projection delivery is admitted
**When** its identity is validated and persisted
**Then** delivery state is scoped by tenant, domain, aggregate, and projection type, duplicate identity uses the persisted EventStore `MessageId`, and sequences must be positive, strictly increasing, and contiguous within that aggregate stream
**And** sequence is never interpreted as global order or substituted for a missing message identity.

**Given** the same completed or active delivery appears again
**When** admission examines the versioned delivery row
**Then** a completed exact identity is an idempotent no-op, an active exact identity is coalesced/deferred, and no handler or batch is invoked twice
**And** a reclaimed expired reservation uses a higher fencing token and the same dispatch identity so an older attempt cannot finalize newer work.

**Given** a lower sequence, future gap, or conflicting sequence/message/content arrives
**When** retained receipt and prefix-fingerprint evidence is evaluated
**Then** an exact retained duplicate is ignored safely, a gap stays retryable without checkpoint advancement, and identity/content conflict fails without handler, read-model, marker, retry, or checkpoint mutation
**And** malformed or reversed event arrays are not sorted into apparent validity.

**Given** a handler requires coordinated detail and index persistence
**When** delivery completes or resumes after ambiguity
**Then** only proven `Completed` or `AlreadyCompleted` durable work may atomically replace the reservation, advance the contiguous sequence/checkpoint, and add bounded completion evidence
**And** crash, cancellation, conflict, or lost response reuses the same stable batch/dispatch identity and converges without reapplying logical writes.

**Given** retained completion history exceeds its configured bound
**When** deterministic compaction runs
**Then** the latest receipts, first-retained sequence, and cumulative prefix fingerprint preserve the proof horizon
**And** an old delivery outside provable evidence enters the authenticated reconciliation/rebuild-required path instead of being silently applied.

**Given** existing sequence-only rows must enter writer protocol version 2
**When** operators perform the maintenance cutover
**Then** old writers are quiesced, state is backed up, authoritative history is used for reconciliation, and readiness activates only after the durable protocol marker is committed
**And** mixed writers, downgrade overwrite, missing identity, or schema regression fail closed rather than inventing delivery receipts.

**Given** tenant-scoped reconciliation or lifecycle erasure is requested
**When** authorization and mutation execute
**Then** exact tenant/scope authority and attributable operator evidence are established before persisted access
**And** per-scope reconciliation state is included in erasure ordering while the store-global writer-protocol marker remains protected control-plane state.

**Given** Story 1.18 validation runs
**When** in-order, completed duplicate, active duplicate, reverse trigger, gap then missing event, partial failure retry, identity/content conflict, cutover, downgrade, and reconciliation scenarios traverse production orchestration, `/project/v2`, handlers, batch store, and Redis
**Then** persisted detail, index, batch receipt, delivery row, lifecycle, retry work, and checkpoint state match the single in-order baseline
**And** Release and complete live-sidecar lanes pass with no new warnings, errors, or leaked processes.

### Story 1.19: Correct Paged Rebuild and Replay Equivalence

As an operator,
I want paged projection rebuilds to be replay-equivalent,
So that rebuilding a long stream cannot replace correct state with a partial-page model.

**Requirements coverage:** Primary NFR8; supporting FR7, FR33, FR36, NFR7, and NFR16.

**Architecture constraints:** AD-2, AD-5–AD-8, AD-12–AD-15, AD-19, and AD-20; pages are bounded transport units, never complete-state claims.

**Dependencies:** Stories 1.14–1.18.

**Historical reconciliation:** Retained as Story 1.19 and recorded as done. The reissued review preserved the former Story 1.14 implementation history, patched all 13 in-scope findings, approved real DAPR/Redis equivalence evidence, and leaves only the pre-existing erase/unknown query-visibility policy deferred.

**Acceptance Criteria:**

**Given** a projection handler participates in rebuild
**When** its contract is inspected
**Then** it declares full-replay or incremental semantics, a full-replay handler receives the complete required prefix, and an incremental handler receives prior staged state plus a contiguous page
**And** no page is ever represented as the complete stream.

**Given** a paged rebuild begins
**When** its target and required projection routes are admitted
**Then** the operation durably freezes the initial stream head, acquires every required lifecycle fence, and holds actor, detail, index, freshness, and checkpoint candidates outside the live view
**And** the last complete live model remains visible until promotion succeeds.

**Given** the frozen stream contains multiple pages
**When** the orchestrator reads through the configured boundary
**Then** pages start at sequence one, remain ordered, contiguous, duplicate-free, and bounded by the frozen head or `toPosition`
**And** incremental count and serialized-byte ceilings fail with `rebuild_prefix_safety_limit_exceeded` before an oversized complete prefix is materialized.

**Given** all legacy and named projection candidates are prepared
**When** durable promotion executes
**Then** one marker-gated visibility boundary publishes coordinated named read models, legacy actor state, freshness/version state, and rebuild checkpoints only after every required route completes
**And** each promoted output and persisted freshness version is read back before checkpoint advancement or success is reported.

**Given** cancellation, preemption, crash, handler failure, malformed result, storage failure, or later-page read failure occurs
**When** terminalization or resume runs
**Then** live state remains intact, staged candidates are reconciled or discarded, lifecycle leases are released or recoverably retained, and progress resumes from a durable safe boundary
**And** page-read progress is never reported as projection completion.

**Given** lifecycle-aware queries execute during rebuild
**When** authoritative persisted lifecycle is observed
**Then** projection-backed results report `Rebuilding` until durable promotion and transition only through a version-coherent completion state
**And** unavailable lifecycle evidence fails closed without leaking stale current/fresh metadata.

**Given** replay-equivalence evidence is produced
**When** streams larger than two pages plus empty, exact-boundary, bounded-position, cancellation, failure, and resume cases pass through the production orchestrator, rebuild endpoint, batch store, and persisted backend
**Then** actor, detail, index, freshness versions, lifecycle, and rebuild checkpoints are semantically equal to canonical replay through the same position
**And** mock calls, status codes, or aggregate-only replay do not satisfy the proof.

**Given** later Stories 6.3 and 6.4 optimize projection cost
**When** they replace complete-prefix reconstruction or paging mechanics
**Then** they preserve this duplicate, gap, staging, promotion, lifecycle, and replay-equivalence baseline
**And** correctness limits and failure modes remain explicit.

### Story 1.20: Owner-Approved Parity Closure and Runtime Pin

As an EventStore platform owner,
I want a reviewed parity-closure packet tied to exact source and package identities,
So that consumer migration resumes only against capabilities that are implemented, verified, and approved.

**Requirements coverage:** Primary FR36 and NFR16; supporting NFR12.

**Architecture constraints:** AD-7, AD-8, AD-11, AD-12, AD-14, AD-15, AD-19, AD-20, and AD-22; evidence, artifact identities, authority, and mutation state remain distinct.

**Dependencies:** Story 1.2 and Stories 1.14–1.19.

**Historical reconciliation:** Retained as Story 1.20 and recorded as done with final decision `available`. The approved/tested runtime is `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`; documentation evidence commit is `21997d1974c4bc7022c77a5065edd9d327435c97`; named EventStore and release-owner approvals were issued by `jpiquot` on 2026-08-01. This closes source/package parity only; deployed-runtime parity remains the backward-only Epic 3 sequence.

**Acceptance Criteria:**

**Given** parity closure begins
**When** prerequisites and review authority are revalidated
**Then** Story 1.2 and Stories 1.14–1.19 are complete and reviewed, Story 1.16 has its exact-runtime follow-up approval, and every compatibility decision and accepted limitation is recorded
**And** stale status text or historical blocked packets cannot substitute for current evidence.

**Given** the final capability matrix is evaluated
**When** erasure, batching, lifecycle/provenance, duplicate/out-of-order delivery, rebuild equivalence, cursor compatibility, asynchronous persistence, multiple projections per domain, and cross-cutting compatibility are classified
**Then** every row is exactly `available` or the overall decision remains `still blocked`
**And** no partial migration authority is possible.

**Given** a candidate runtime is selected
**When** exact-SHA gates execute from a clean detached checkout
**Then** the same 40-hex source commit is present before and after all build, test, persisted-path, package, and container gates and satisfies the exact AD-11 security baseline
**And** mock-only, HTTP-only, dirty-tree, mismatched-runtime, unexpected-skip, or failed production-path evidence cannot close a row.

**Given** release identities are frozen under AD-22
**When** package and container artifacts are validated
**Then** all 14 approved NuGet packages share exact version `999.1.20-proof.fa2d1c9910f8` with byte-level SHA-256 evidence
**And** `registry.hexalith.com/eventstore@sha256:523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87` resolves to exactly `linux/amd64` and `linux/arm64` and maps back to the tested source SHA.

**Given** exact-SHA evidence and artifact identities exist
**When** the closure packet is reviewed
**Then** the hybrid evidence manifest, WORM-retained raw bundle, approval subject, limitations, rollback guidance, and durable EventStore/release-owner sources are independently hash-bound
**And** evidence preparation or publication authority never substitutes for owner approval of consumer migration.

**Given** evidence commit A, pointer-only commit B, and authorizing commit C are created
**When** the executable ancestry and mutation-boundary verifier runs
**Then** A is the evidence-only child of the tested runtime, B changes only the documentation pointer to A, and C changes only the verified decision, migration, story, Epic, and tracker fields
**And** any identity, ancestry, file-mode, evidence, approval, or prerequisite mismatch retains the fail-closed state.

**Given** the named EventStore and release owners approve the exact evidence subject
**When** authorizing commit C passes every gate
**Then** the packet records `final_decision: available` and `authorize_consumer_migration: true`
**And** the approved source SHA, package hashes, container digest, accepted limitations, and rollback boundary remain immutable inputs to consumer verification.

**Given** a consumer such as Parties evaluates the handoff
**When** it chooses source, package, or container consumption
**Then** it verifies its gitlink and checkout or exact package/container identities against the approved packet before deleting rollback infrastructure
**And** EventStore approval does not itself modify the consumer repository, change its pin, or authorize deployed-runtime parity claims.

### Story 1.21: Frozen Story 1.20 Evidence Integrity Repair

As an EventStore evidence owner,
I want the three SDK-token-sweep drifts repaired under explicit predecessor authority,
So that each frozen Story 1.20 critical manifest again verifies without granting Story 3.13 authority over Epic 1 evidence.

**Classification:** Post-closure Epic 1 evidence maintenance. This backlog story does not reopen Story 1.20, Epic 1, or any completed capability decision.

**Requirements coverage:** No new FR or NFR capability ownership. This story preserves the integrity of Story 1.20's completed FR36/NFR16 evidence.

**Architecture constraints:** AD-12 and AD-22. Frozen evidence bytes, package availability, approval state, and capability availability remain distinct; repair authority is exact-file and predecessor-byte scoped.

**Dependencies:** Completed Story 1.20 and introducing commit `089369bb`. Story 3.13 is evidence for discovery only and has no authority to execute this repair.

**Authority and status:** Approved as backlog maintenance by the 2026-08-12 Correct Course proposal. Implementation requires a separate EventStore evidence-owner authorization bound to the exact repair diff and Test Architect verification.

**Acceptance Criteria:**

**Given** the SDK-token sweep in commit `089369bb` introduced the known drift
**When** repair inputs are resolved before any write
**Then** each exact pre-sweep Git blob is pinned for the affected files
**And** missing, ambiguous, or non-predecessor bytes halt the repair.

**Given** the three affected evidence trees are inspected
**When** repair scope is calculated
**Then** it contains only the `environment.txt` file under each of `38f85086fc2513e06fe85482dfade96578d649e5`, `4983299103bfa5bbbd40e695767eb5ddbc1369d5`, and `ec0d35a082bcc70b090afa1c1544306008d767da`
**And** any broader difference halts the story.

**Given** exact predecessor blobs and exact-file scope are proven
**When** the evidence repair executes under its own authority record
**Then** the three files are restored byte-for-byte from their pre-sweep versions rather than normalized to the current SDK
**And** no other predecessor evidence, package, source, release, registry, runtime, consumer, or submodule state changes.

**Given** the three exact files have been restored
**When** integrity verification runs
**Then** every entry in each affected `critical-evidence-sha256.txt` manifest verifies
**And** a focused guardrail detects any future byte drift in the frozen evidence set.

**Given** proof-package evidence is evaluated separately
**When** each affected `nuget-sha256.txt` manifest is checked
**Then** its result is verified and reported independently from content integrity
**And** missing proof archives remain unrecoverable and are never relabeled as corruption, restored, rebuilt, or inferred.

**Given** completion is requested
**When** the exact repair diff and verification packet are reviewed
**Then** EventStore evidence-owner authorization and Test Architect verification bind to that exact subject
**And** Story 1.20's decision, approved identities, consumer authorization, and Epic 1 status remain unchanged.

## Epic 2: API and UI Developers Get Safe Integration Surfaces

Developers can expose typed external REST APIs and build interactive clients through supported gateway contracts while preserving metadata, scoping, and projection truth.

### Story 2.1: REST Contract Seam for Command and Query Messages

As a domain contract author,
I want command and query messages to declare their generated REST surface explicitly,
So that external API hosts can generate typed endpoints without convention-only discovery or copied contract types.

**Requirements coverage:** Primary FR11.

**Architecture constraints:** AD-3 and AD-4; contract metadata is explicit and generated controllers remain external-host concerns.

**Dependencies:** Epic 1's released Contracts and gateway seams; no other Epic 2 story is required.

**Historical reconciliation:** Retained as Story 2.1 and recorded as done. Public command/query shapes remain compatible; later controller emission and host adoption remain Stories 2.2–2.6.

**Acceptance Criteria:**

**Given** a command is intended for generated REST exposure
**When** it implements `ICommandContract`
**Then** static `Domain` and `CommandType` plus instance `AggregateId` remain accessible through compile-time and interface-constrained use
**And** existing command identity rules and released member names remain unchanged.

**Given** an existing query contract is used
**When** `IQueryContract` metadata is read
**Then** static query type, domain, and projection type behavior remains backward compatible
**And** no command-seam addition changes the query contract's released shape.

**Given** a command or query declares `RestRouteAttribute`
**When** verb, route template, and optional API scope are read
**Then** verb and template are preserved and optional scope is trimmed or normalized to null
**And** only contract-layer-owned invalid metadata is rejected, leaving route-shape validation to the generator.

**Given** an external API host applies assembly-level `RestApiAttribute`
**When** route prefix, tag, and tenant-source behavior are supplied
**Then** the generator receives stable route and tenant options plus a trimmed or null optional tag
**And** the same contract assembly remains reusable by the domain service, API host, and interactive UI metadata consumers.

**Given** `RestQueryBindingAttribute` supplies aggregate or entity binding metadata
**When** constant, route, none, unsupported, or missing-value combinations are validated
**Then** supported values are preserved and invalid aggregate-source or required-value cases fail deterministically
**And** attribute usage remains class-only, non-multiple, and non-inherited.

**Given** Story 2.1 validation runs
**When** Contracts tests, focused generator normalization regressions, and the Release build execute
**Then** marker behavior, route metadata, tenant source, tag/scope normalization, binding validation, and compatibility pass with zero warnings and errors.

### Story 2.2: REST API Generator Discovery and Controller Emission

As an external API host developer,
I want a Roslyn generator to emit typed REST controllers from domain contracts,
So that external applications get OpenAPI-visible endpoints without hand-written per-message controllers.

**Requirements coverage:** Primary FR12; supports UX-DR42 and the shared query-evidence presentation boundary.

**Architecture constraints:** AD-3, AD-4, AD-14, and AD-17; emitted controllers use only the gateway client and support-safe HTTP contracts.

**Dependencies:** Story 2.1 and Epic 1's gateway metadata contracts.

**Historical reconciliation:** Retained as Story 2.2 and recorded as done. Its deliberate follow-up-review disposition is accepted; later command-location and provenance-consumption hardening remain Stories 2.9 and 2.11.

**Acceptance Criteria:**

**Given** an external API host references the generator as an analyzer and opts in with `RestApiAttribute`
**When** compilation contains source or referenced command/query contracts
**Then** controllers are emitted for source contracts using the preserved convention fallback and for referenced contracts only when `ApiScope` exactly matches the host tag
**And** non-marker types, blank/mismatched referenced scopes, and tagless broad fallback are excluded.

**Given** a generated command endpoint receives a valid body and matching route values
**When** the action executes
**Then** it creates the gateway request with a sortable ULID message identity, serializes the unchanged payload, and calls `SubmitCommandAsync` once
**And** null bodies or route/body aggregate mismatches return support-safe `400 application/problem+json` before a gateway call.

**Given** a generated query endpoint receives a successful gateway result
**When** it returns `200` or bodyless `304`
**Then** it returns the raw payload or empty body and forwards only present, bounded protocol metadata such as strong ETag, version, served-at, stale/degraded state, warnings, and paging evidence
**And** it never derives projection-confirmed state from payload fields or opaque validators.

**Given** not-modified metadata lacks a trustworthy strong ETag
**When** the generated query action evaluates the result
**Then** it returns a documented support-safe gateway failure rather than an invalid `304`
**And** weak, malformed, whitespace-bearing, oversized, or quote-corrupted validators are not emitted.

**Given** the gateway raises a command or query failure
**When** generated problem mapping runs
**Then** status is normalized to the action's documented error set, `Retry-After` is emitted only when syntactically valid, bounded, and applicable, and permitted validation/problem extensions remain bounded
**And** tokens, stack traces, payloads, cursor contents, ETag internals, unsafe display text, control characters, and arbitrary warning codes are omitted.

**Given** generated OpenAPI and source output are inspected
**When** generator tests compile and invoke the emitted controllers
**Then** documented Problem Details responses, nullability, file-scoped namespaces, `ConfigureAwait(false)`, deterministic output, and gateway-only dependencies are present
**And** DAPR, MediatR, domain handlers, actors, and state stores are absent from generated controllers.

**Given** Story 2.2 validation runs
**When** generator discovery, source emission, runtime-controller, Contracts compatibility, and package-mode Release build lanes execute
**Then** accepted behavior passes under warnings-as-errors and remaining generator-hardening items stay explicitly deferred.

### Story 2.3: Sample External API Host Proof

As an external application developer,
I want the Sample domain to expose generated REST endpoints through a dedicated API host,
So that I can see the intended integration pattern without coupling it to the interactive Sample UI.

**Requirements coverage:** Primary FR13 and FR14; supports NFR14 and UX-DR42.

**Architecture constraints:** AD-3, AD-4, and AD-18; external API, interactive UI, and domain-service hosts retain distinct responsibilities.

**Dependencies:** Stories 2.1 and 2.2.

**Historical reconciliation:** Retained as Story 2.3 and recorded as done with deliberate follow-up-review acceptance. Generated status-location and outbound DAPR header ownership remain Stories 2.9 and 2.10; the Tier-1 proof does not claim a live Aspire run occurred.

**Acceptance Criteria:**

**Given** Sample command and query contracts are shared between hosts
**When** the project graph is inspected
**Then** the domain service, `Sample.Api`, and `Sample.BlazorUI` reference the same compiled `Sample.Contracts` identities
**And** no contract is compile-linked into another host as a duplicate type.

**Given** the dedicated `Sample.Api` host is compiled
**When** generator and host metadata are inspected
**Then** the generated Counter controller is the complete API controller set, exposes the expected authorized query and command actions under `api/{tenant}/counter`, and depends only on `IEventStoreGatewayClient`
**And** the API host references contracts, client, service defaults, and generator—not the domain implementation or interactive UI.

**Given** generated Sample query and command routes execute through the in-process host
**When** authenticated and unauthenticated requests exercise `200`, `304`, accepted commands, null bodies, route/body mismatches, or gateway failures
**Then** routing, model binding, auth middleware, cancellation, gateway request identity, metadata headers, empty not-modified responses, and support-safe problems match the generated contract
**And** gateway calls occur exactly once only for valid requests.

**Given** Sample API or Blazor UI resolves its local DAPR HTTP endpoint
**When** endpoint and port environment values are blank, padded, malformed, credential-bearing, path-bearing, or out of range
**Then** blank values normalize to the supported fallback and valid origins/ports normalize deterministically
**And** unsafe nonblank configuration fails early with an explicit configuration error.

**Given** the Sample domain service and Blazor UI are scanned
**When** architecture guardrails run
**Then** neither contains generator analyzers, REST API assembly opt-in, MVC registration/mapping, minimal command/query endpoints, or controller types
**And** the Blazor UI consumes EventStore client libraries directly without referencing `Sample.Api` or the domain implementation.

**Given** AppHost and DAPR access-control configuration is inspected
**When** `sample-api` topology is validated
**Then** gateway credentials and exactly scoped allowed POST operations are present without duplicate policy blocks
**And** the structural proof does not overstate self-hosted caller-identity enforcement.

**Given** Story 2.3 validation runs
**When** Sample, AppHost, DomainService, and package-mode Release lanes execute
**Then** compiled-controller, host-boundary, auth, handler-chain, topology, and configuration evidence passes under warnings-as-errors
**And** absence of a live Aspire topology is recorded separately from the green Tier-1 proof.

### Story 2.4: Tenants REST Contract Metadata and Routes

As a Tenants contract maintainer,
I want command and query contracts to declare the external REST surface,
So that generated tenant APIs remain stable without duplicating controller logic.

**Requirements coverage:** Primary FR11 and FR15; supporting NFR13.

**Architecture constraints:** AD-4, AD-10, and AD-12; contract identity is explicit and the Tenants repository remains owner-controlled.

**Dependencies:** Stories 2.1 and 2.2.

**Historical reconciliation:** Retained as Story 2.4 and recorded as done. Maintainer-approved implementation commit is `f3844d34e314b96b7e5caf63aee2c5a5f2cbcf6a`; accepted final Tenants SHA is `80d23613612088a0c3fee23eb149f34ce08e9729`. This story owns contract metadata and diagnostics only; host composition remains Story 2.5.

**Acceptance Criteria:**

**Given** a Tenants command or query is externally exposed
**When** its compiled contract metadata is inspected
**Then** HTTP verb, route template, system-tenant source, aggregate/entity binding, and API scope are explicit
**And** tenant detail, tenant users, user tenants, global administrators, and audit routes remain unambiguous.

**Given** referenced Tenants contracts are processed by the generator
**When** the target API scope matches or differs
**Then** only exact matching contract identities contribute actions
**And** no convention or runtime fallback invents a referenced route.

**Given** invalid, unsupported, or duplicate Tenants route metadata exists
**When** generator diagnostics execute
**Then** compilation fails with deterministic bounded diagnostics that identify the conflicting contract boundary
**And** tenant data, payloads, tokens, or other sensitive values are absent from diagnostic text.

**Given** contract identities are consumed in package and source modes
**When** the evaluated project graph and generated output are compared
**Then** both modes compile the intended Tenants contract assembly against the corresponding REST generator identity
**And** test-only source analyzer substitution cannot silently validate behavior different from the shipped package graph.

**Given** Story 2.4 completion crosses the Tenants repository boundary
**When** evidence is accepted
**Then** it records the maintainer-approved commit chain, exact final SHA, accepted route/verb/tenant/entity/API-scope boundary, and focused results
**And** the record does not independently authorize a later Tenants source or parent gitlink mutation.

**Given** Story 2.4 validation runs
**When** Tenants Contracts and generated-controller diagnostics/output tests execute against the accepted SHA
**Then** route identity, invalid/duplicate diagnostics, package/source parity, and compilation pass under the applicable warnings-as-errors policy.

### Story 2.5: Dedicated External Tenants API Host

As an external tenant-management integrator,
I want generated Tenants controllers in one dedicated external API host,
So that gateway policy remains the front door and domain/UI hosts expose no per-message API surface.

**Requirements coverage:** Primary FR13 and FR15; supporting NFR2, NFR14, and UX-DR42.

**Architecture constraints:** AD-3, AD-4, AD-10, and AD-18; inbound auth stays host-owned while platform routing headers are authoritative.

**Dependencies:** Stories 2.2 and 2.4.

**Historical reconciliation:** Retained as Story 2.5 and recorded as done. Accepted Tenants patch is `846f988a5f2fe1bce2e4fdb5a42b7c1c63ba61ae`; accepted final SHA is `6cc9eb3a44f45417aac76d7def9daba7544cd2fa`, with only a reviewed benign launch-profile delta affecting the API host after the patch.

**Acceptance Criteria:**

**Given** `Hexalith.Tenants.Api` references Tenants contracts and the EventStore generator
**When** its evaluated project graph and compiled assembly are inspected
**Then** the generated `TenantsRestController` is its only API controller and delegates only through `IEventStoreGatewayClient`
**And** the host includes authentication, controller mapping, service defaults, and gateway wiring without domain implementation, UI, state-store, pub/sub, or direct persistence dependencies.

**Given** inbound authentication is configured
**When** authority-discovery or symmetric-key modes start
**Then** issuer, audience, HTTPS metadata, dependency wiring, and a minimum 256-bit signing key are validated fail-closed
**And** unauthenticated generated routes are denied before gateway invocation.

**Given** bearer forwarding and DAPR service invocation handlers form the gateway chain
**When** an outbound request is sent
**Then** bearer forwarding remains outermost and the platform `AddEventStoreDaprServiceInvocation` handler is registered last/innermost to replace untrusted DAPR app-id and API-token headers
**And** repo-wide Tenants source guardrails reject local or append-only DAPR routing-header ownership.

**Given** AppHost and DAPR ACLs include `tenants-api`
**When** topology is inspected
**Then** the host may invoke only the required EventStore gateway command/query operations with `POST` allow actions
**And** it receives no direct Tenants domain-service or persistence component access.

**Given** unauthorized, invalid, mismatched, not-modified, command, query, cancellation, or gateway-failure cases traverse compiled generated routes
**When** runtime results are asserted
**Then** support-safe responses, exact gateway calls, token propagation, and metadata behavior match the generated contract
**And** invalid local requests produce no unintended gateway call.

**Given** the host is consumed in package mode
**When** the accepted SHA builds against EventStore Client and REST generator packages `3.82.0`
**Then** compilation completes with zero warnings and errors and focused compiled-route, handler-chain, structural, topology, and ACL lanes pass
**And** source-mode limitations caused by forbidden uninitialized nested submodules are reported without initializing them.

**Given** Story 2.5 completion is accepted
**When** external-authority evidence is reviewed
**Then** it records the admin-authorized patch, final Tenants SHA, enumerated delta, accepted host properties, and focused results
**And** the environment-blocked non-CI Tier-3 Aspire topology lane is not represented as executed evidence.

### Story 2.6: Tenants UI Client-Library Alignment and UX Evidence

As a Tenants operator,
I want the interactive UI to consume typed client libraries and display honest evidence states,
So that it remains an interactive host rather than a second external API surface.

**Requirements coverage:** Primary FR15; supporting FR13, FR34, NFR14, NFR15, UX-DR20, UX-DR25–UX-DR27, UX-DR30, UX-DR38, UX-DR40, and UX-DR42.

**Architecture constraints:** AD-4, AD-14, and AD-15; this story owns presentation and host alignment, while Story 2.11 exclusively owns production provenance/lifecycle classification.

**Dependencies:** Epic 1's typed client and lifecycle contracts plus Story 2.5's external-host separation. No future Story 2.11 implementation is required for deterministic presentation fixtures.

**Historical reconciliation:** Retained as Story 2.6 and recorded as done. SHA-bound presentation baseline and Sally review are tied to Tenants `55e6000a41e7846868ff7512b79e5f7a36464a37`; the later owner-expanded fourth-pass fixes are verified but uncommitted and therefore are not substituted for that source identity.

**Acceptance Criteria:**

**Given** Tenants UI issues commands and queries
**When** its source/package dependency graphs, compiled controllers, and runtime endpoint inventory are inspected independently
**Then** it uses Tenants/EventStore typed client libraries and exposes no generated or hand-written per-message API controller or endpoint
**And** REST generator, external API host, and domain-service implementation dependencies remain absent in both modes.

**Given** a deterministic typed-client fixture supplies `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, `LocalOnly`, or `Unknown`
**When** tenant detail, list, membership, global-administrator, audit, or empty collection surfaces render
**Then** lifecycle is shown as a distinct localized, support-safe, non-colour-only indicator while threshold freshness remains separately visible
**And** the fixture is not presented as proof of real gateway provenance or persisted read-model classification.

**Given** lifecycle evidence changes while a high-impact flow is open
**When** member, configuration, metadata, tenant-lifecycle, or global-administrator mutation availability is recomputed
**Then** only projection-confirmed `Current` remains enabled and every other lifecycle fails closed immediately
**And** denial preserves the user's context and explains the evidence state without exposing transport internals.

**Given** lifecycle status changes on a page or collection surface
**When** assistive technology observes the update
**Then** the surface-level status region announces the transition once, row badges remain non-live, and focus is not moved unexpectedly
**And** empty collections still expose the snapshot lifecycle state.

**Given** English and French resources are compared
**When** canonical lifecycle, denial, loading, and support-safe messages are validated
**Then** key sets remain in parity and operational language follows the approved UX vocabulary
**And** implementation does not redefine the design system or depend on color alone.

**Given** focused UX acceptance is requested
**When** Sally and the Tenants maintainer review the evidence
**Then** the artifact records the exact Tenants SHA, accepted surfaces, test inventory, resource parity, limitations, and approval source
**And** production gateway classification evidence is cited to Story 2.11 rather than double-owned here.

**Given** Story 2.6 validation runs
**When** source- and package-mode composition, gateway-fixture, component render, mutation-gate, route guard, localization, Server, and integration fallback lanes execute
**Then** focused and full UI evidence passes under warnings-as-errors and intentionally uninitialized nested submodules remain untouched.

### Story 2.7: Pre-Authorization Registration and Provenance Correction

As an EventStore platform maintainer,
I want configured domain bindings and the live proof harness to reflect the domains actually hosted,
So that runtime selection can rely on valid handler-routing and provenance evidence without prematurely migrating a consumer.

**Requirements coverage:** Supports FR4 and FR15; supporting NFR12 and NFR16.

**Architecture constraints:** AD-2, AD-12, AD-14, and AD-15; operational metadata remains atomic and fail-closed, and authoritative query provenance must be proved through the persisted production path.

**Dependencies:** Epic 1's query-routing and provenance contracts. This correction is independently reviewable before Story 1.20 authorization and changes no dependency identity.

**Historical reconciliation:** Retained as Story 2.7 and recorded as done. Commit `fd8ab24da230058f2f239765b68d5e0a135b4b76` removed the stale sample registrations; the correction subsequently remained an ancestor of the owner-approved Story 1.20 runtime and its complete 279/279 source integration proof. This is the current pre-authorization story, not the historical outbound-header Story 2.7 that is now Story 2.10.

**Acceptance Criteria:**

**Given** the source-topology query-provenance proof is built
**When** its AppHost is compiled and started
**Then** `UseHexalithProjectReferences=true` includes the root-declared Tenants resource and the harness waits for that real resource to become healthy
**And** no nested submodule initialization or package-mode topology substitution is required.

**Given** sample and Tenants domain-service registrations are loaded
**When** operational metadata is derived
**Then** every configured sample binding maps to an actually hosted `counter` or `greeting` domain, `admin:query-types:tenants` contains `list-tenants`, and genuine endpoint, payload, capability, or transport failures still suppress the derived index atomically
**And** absent or invalid metadata never becomes partial success.

**Given** the live persisted-path proof starts from cleared prior operational-index state
**When** the Tenants `list-tenants` route executes
**Then** the response is HTTP `200` with `HandlerComputed` provenance, no ETag or projection-version leakage, and persisted `admin:query-types:tenants` evidence confirms the handler capability
**And** the evidence cannot be satisfied by an HTTP-only assertion or mock call count.

**Given** Story 1.20 is incomplete, blocked, or non-authorizing
**When** this correction and its focused proof pass
**Then** Story 2.7 may complete without changing any Tenants, EventStore, or Builds dependency identity and existing rollback paths remain intact
**And** the correction itself does not authorize a gitlink, package, container, or deployment migration.

**Given** later compatibility work requires consumer behavior, deployment-topology, or approved-identity changes
**When** that work is identified
**Then** it is routed to Story 2.12 or another separately approved unit
**And** Story 2.7's EventStore-only scope is not silently broadened.

**Given** Story 2.7 completion evidence is reviewed
**When** the committed correction and later exact-candidate gate are traced
**Then** `fd8ab24da230058f2f239765b68d5e0a135b4b76` is present in the accepted runtime ancestry and the complete source integration lane remains green at 279/279
**And** historical references to the former outbound-header identifier remain distinguishable from this active story.

### Story 2.8: Scoped Metadata-Rich Projection Notifications

As a real-time client developer,
I want projection-changed notifications to carry bounded metadata and optional group scope,
So that clients can receive relevant freshness hints before re-querying without tenant-wide noise or payload disclosure.

**Requirements coverage:** Primary FR16; supporting NFR1, NFR6, NFR12, NFR15, and NFR16.

**Architecture constraints:** AD-8, AD-10, AD-12, AD-15, and AD-21; notifications remain at-least-once, unordered hints and never become projection-confirmed success evidence.

**Dependencies:** Existing projection notification, ETag-regeneration, SignalR authorization, and reusable client seams; it requires no later Epic 2 story.

**Historical reconciliation:** Retained as Story 2.8 and recorded as done, migrated from implementation artifact `spec-2-5-scoped-metadata-rich-projection-notifications.md`. Final reviewed revision is `c2d6faaad042a867b3bbd1e7ef74cdc491c1a487`; signal-only, scoped detail, DAPR, reusable-client, and live Redis backplane paths were verified.

**Acceptance Criteria:**

**Given** an existing signal-only projection-notification producer or consumer
**When** the additive detail path is introduced
**Then** `ProjectionChanged(projectionType, tenantId)`, `JoinGroup`, `LeaveGroup`, topic `{tenantId}.{projectionType}.projection-changed`, and group `{projectionType}:{tenantId}` remain compatible
**And** existing constructor, deconstruction, JSON-binding, and interface-implementation call sites continue to build or fail explicitly rather than silently losing detail.

**Given** a producer supplies projection type, tenant ID, optional group scope, and metadata
**When** direct or DAPR pub/sub notification transport runs
**Then** the ETag is regenerated before broadcast and a scoped detail reaches only the matching `{projectionType}:{tenantId}:{groupScope}` group
**And** a tenant-wide group does not receive scoped-only detail.

**Given** scope or metadata is null, malformed, separator-bearing, overlong, oversized, or explicitly empty
**When** producer, receiver, hub, runtime-proof, or broadcaster validation executes
**Then** aligned configured limits either reject invalid inbound data or deterministically bound producer/broadcast metadata according to the documented path
**And** empty detail intent is preserved, shared metadata is immutable, and metadata values are never logged above Debug.

**Given** a reusable SignalR client subscribes to scoped detail
**When** join, leave, callback, mismatch, reconnect, or failed-join behavior occurs
**Then** tenant authorization precedes membership changes, invalid joins consume no quota, mismatched scopes do not invoke callbacks, and reconnect rejoins the immutable scoped identity
**And** callback consumers receive isolated read-only metadata snapshots.

**Given** actor ETag regeneration or notification broadcast fails
**When** the projection-notification endpoint handles the request
**Then** actor failure returns non-success without broadcasting, while a post-regeneration broadcast failure follows the documented fail-open notification policy
**And** neither path represents the notification as command or projection completion.

**Given** focused and live validation runs
**When** contract, client, SignalR, server, validation, controller, direct/pubsub DAPR, and Redis-backplane lanes execute
**Then** scoped delivery, legacy compatibility, authorization denial, metadata bounds, cross-instance fan-out, and reconnect behavior pass under warnings-as-errors
**And** the live proof uses the packaged reusable SignalR client against the real hub/backplane path.

### Story 2.9: Generated Command-Status Location Policy

As an external API consumer,
I want a generated command's `202 Accepted` response to point to a status resource I can actually reach—or to omit the link,
So that I never poll a dangling route or use the external host as the wrong authority.

**Requirements coverage:** Primary FR12; supports FR27 without claiming its re-keying scope, NFR13, UX-DR26, and UX-DR42.

**Architecture constraints:** AD-3, AD-4, AD-12, and AD-17; generated status locations are absolute, runtime-resolved, gateway-authoritative, and fail-closed.

**Dependencies:** Story 2.2's command-controller emission and Story 2.3's compiled external-host proof. It is independent of later FR27 command-status re-keying.

**Historical reconciliation:** Retained as Story 2.9 and recorded as done, migrated from completed Story 2.6. It closes the relative-URL defects from the Story 2.2 and 2.3 deferred ledger while leaving unrelated REST-generator hardening outside this unit.

**Acceptance Criteria:**

**Given** a host configures a valid absolute HTTP(S) gateway status base
**When** a generated command action returns `202 Accepted`
**Then** `ICommandStatusLocationBuilder` emits `{gatewayStatusBase}/api/v1/commands/status/{escapedStatusKey}` using request-time configuration
**And** the status key is read once from the single tracking field on `SubmitCommandResponse`, with no assumption that `CorrelationId` equals `MessageId`.

**Given** no valid gateway status base is configured
**When** a generated command action returns `202 Accepted`
**Then** it emits the documented `Retry-After`, preserves the response tracking key, and omits `Location`
**And** it never emits a relative `/api/v1/commands/status/...` URI.

**Given** status-base configuration is relative, uses a non-HTTP(S) scheme, contains user information, query, or fragment, or otherwise cannot form the approved authority
**When** configuration or request-time resolution occurs
**Then** public configuration rejects the invalid value support-safely and direct invalid option state fails closed without a dangling `Location`
**And** an unconfigured default builder remains resolvable for every gateway-client host.

**Given** a generated command fails at the gateway
**When** problem mapping executes
**Then** no `Location` header is emitted and the existing bounded error and retry policy is preserved
**And** status-location composition never converts failure into acceptance.

**Given** a controller contains command actions or query actions only
**When** generator source is emitted
**Then** `ICommandStatusLocationBuilder` is injected only for controllers that emit a command action, while query-only controllers keep their existing constructor surface
**And** reserved generated identifiers and warnings-as-errors prevent ambiguous or unread generated code.

**Given** the generated controller and Sample external API host are tested
**When** configured, unconfigured, failure, malformed-base, query-only, and compiled-runtime cases execute
**Then** absolute-when-configured, absent-when-unconfigured, and never-relative behavior passes across Client, generator, and Sample suites
**And** the Story 2.2 and 2.3 command-status-location deferred entries are recorded resolved without closing unrelated generator backlog.

### Story 2.10: Outbound DAPR Routing-Header Ownership

As a platform maintainer,
I want outbound DAPR service-invocation clients to replace sidecar routing headers through one platform handler,
So that caller-supplied control-plane headers cannot duplicate, hijack, or leak routing identity or API tokens.

**Requirements coverage:** Supports primary FR13 and FR14; security support for FR26 and FR28 without claiming their Epic 5 closure; supporting NFR1 and NFR14.

**Architecture constraints:** AD-3, AD-4, AD-12, and AD-18; the platform handler is the sole routing-header setter and is registered last/innermost.

**Dependencies:** Story 2.3's external-host and handler-chain proof. Tenants repository changes require their separately authorized Story 2.5/2.12 evidence boundary.

**Historical reconciliation:** Retained as Story 2.10 and recorded as done, renumbered from the completed outbound-header Story 2.7. EventStore Client, Sample API, Sample Blazor UI, and Admin UI implementation and guardrail evidence passed; later Tenants owner-approved handler migration is recorded separately and is not attributed to this EventStore-only unit.

**Acceptance Criteria:**

**Given** an outbound request already contains `dapr-app-id` or `dapr-api-token`
**When** the platform `DaprServiceInvocationHandler` runs
**Then** it removes the existing app-id header and sets exactly one configured app ID, removes any existing token header, and sets exactly one configured token only when a token is present
**And** it never leaves an injected, duplicate, or stale routing-header value.

**Given** a sidecar-routed HTTP client has bearer, authorization, or other forwarding handlers
**When** its handler chain is composed
**Then** `.AddEventStoreDaprServiceInvocation(appId, apiToken)` is appended last so the platform handler is innermost and has final control-plane authority
**And** application authorization forwarding remains otherwise unchanged.

**Given** typed gateway clients and a plain named HTTP client require DAPR service invocation
**When** Sample API, Sample Blazor UI, and Admin UI wiring is inspected
**Then** both registration shapes use the reusable Client extension with their explicit app IDs
**And** all three host-local `DaprAppIdHandler` implementations are absent.

**Given** conflicting headers or no configured API token
**When** capturing-handler tests execute
**Then** the outgoing request contains one authoritative app-id value and either one authoritative token or no token header at all
**And** assertions inspect the final outgoing header collection rather than only a clean-request happy path.

**Given** a host attempts to reintroduce local routing-header ownership or append the protected headers
**When** source and assembly guardrails run
**Then** realistic host-local `DelegatingHandler` copies and `TryAddWithoutValidation` setters fail with an AD-18 support-safe diagnostic
**And** the sole approved setter remains the platform Client handler.

**Given** the Tenants repository needs equivalent handler ownership
**When** cross-repository work is performed
**Then** it proceeds only through the Tenants maintainer-authorized external-authority boundary recorded by Story 2.5, with later runtime-identity adoption retained by Story 2.12
**And** this story neither edits Tenants silently nor claims its source/package identity.

**Given** Story 2.10 validation runs
**When** Release build, Client, Sample, Admin UI, structural-guardrail, Tier-1 CI, and local topology checks execute
**Then** configured lanes pass with warnings-as-errors and healthy host/sidecar evidence
**And** deferred theoretical guardrail generalizations remain explicitly outside this delivered invariant.

### Story 2.11: Query Provenance Consumption in Generated REST and Tenants

As an external API and Tenants UI consumer,
I want route provenance preserved and classified safely across generated REST and interactive workflows,
So that opaque validators or handler-computed responses are never presented as projection-backed lifecycle evidence.

**Requirements coverage:** Primary FR12 and FR15; supporting FR4, FR34, NFR8, NFR14–NFR16, UX-DR20, UX-DR21, UX-DR25–UX-DR27, UX-DR38, and UX-DR40.

**Architecture constraints:** AD-3, AD-4, AD-12, AD-14, and AD-15; this is consumer-only scope and never infers lifecycle from ETag, HTTP success, payload fields, or SignalR.

**Dependencies:** Story 1.2's authoritative producer/routing contract, Story 2.2's generated REST path, and Story 2.6's Tenants presentation surface. It does not reopen those owners' scopes.

**Historical reconciliation:** Retained as Story 2.11 and recorded as done. The accepted admin-authored authority chain culminates in published Tenants merge `d2e5a1211f469041fdc593fd4e4678755f6863c8`, containing mutation-gate fix `5eed7a97b87988e2f1e286a0483490ca7ef75d2b`; later checkout `fc9a5d86436f95ace77930c0ec522fe2b3afdb45` does not replace that acceptance identity. The 2026-07-30 owner-expanded gate fixes are verified but uncommitted and are retained as such, not substituted for an approved source SHA.

**Acceptance Criteria:**

**Given** generated REST receives valid `ProjectionBacked` query metadata
**When** it emits a successful or not-modified response
**Then** it forwards only present, bounded ETag, projection version, lifecycle/freshness, served-at, warning, and paging evidence
**And** the values remain traceable through the real gateway to persisted projection state.

**Given** generated REST or Tenants receives `HandlerComputed`, `Unknown`, missing, or invalid provenance
**When** headers, retained rows, lifecycle indicators, or mutation availability are resolved
**Then** projection-backed headers are omitted, lifecycle becomes `Unknown`, and no mutation is armed
**And** ETag, HTTP success, legacy `IsStale`, payload fields, or SignalR never manufacture projection confirmation.

**Given** a not-modified response is considered for retained representation reuse
**When** its validator, provenance, or lifecycle evidence is missing or untrusted
**Then** `304` requires a strong gateway-authoritative validator permitted by route provenance, while lifecycle independently fails closed to `Unknown` unless explicitly valid on that response
**And** retained data does not inherit a prior `Current` lifecycle merely because its representation was reusable.

**Given** tenant detail, list, membership, configuration, metadata, tenant-lifecycle, global-administrator, audit, or correction surfaces consume query evidence
**When** lifecycle and provenance are transported to rows and intent gates
**Then** only projection-confirmed `Current` evidence can make a mutation eligible and all absent, invalid, or non-current combinations deny safely
**And** an available correction intent always satisfies the shared `ProjectionLifecyclePolicy.CanMutate` invariant.

**Given** a pre-conformance Tenants producer returns an incompatible audit alias or legacy projection evidence
**When** the production consumer deserializes and classifies it
**Then** the route degrades to unknown, empty, non-actionable evidence without editing the producer in this story
**And** producer correction remains with its separately authorized owner.

**Given** consumer-path validation runs
**When** generated REST runtime tests, exhaustive Tenants provenance/lifecycle matrices, component mutation guards, and the Tier-3 production-gateway persisted-path test execute
**Then** projection-backed, handler-computed, unknown, missing, invalid, `200`, `304`, retained-row, and incompatible-payload cases are covered
**And** mock-only metadata cannot close the story.

**Given** completion evidence is reviewed across repository boundaries
**When** the Tenants implementation and expanded review fixes are cited
**Then** the accepted published merge and its maintainer authority are recorded separately from later working-tree-only verification
**And** Story 2.6 UX approval cannot substitute for this provenance proof or vice versa.

### Story 2.12: Tenants Runtime Identity Adoption and Package-Mode Validation

As a Tenants release maintainer,
I want Tenants to adopt an authorized EventStore dependency graph in independently verified source and package modes,
So that consumer migration is reproducible, maintainer-approved, and honest about the exact identities each mode measures.

**Requirements coverage:** Primary FR15; supports FR21, FR22, and FR36 without claiming Epic 3 deployed-parity closure; supporting NFR9, NFR12, and NFR16.

**Architecture constraints:** AD-2–AD-4, AD-9–AD-12, AD-14, AD-15, AD-18, and the approved Story 2.12-scoped AD-22 exception; source and package identity evidence remain distinct and no UX redesign is permitted.

**Dependencies:** Completed Story 2.7 pre-authorization correction and Story 1.20's durable activation authorization. It adopts identities only after those earlier gates and does not recreate their approvals.

**Historical reconciliation:** Retained as Story 2.12 and recorded as done under the approved 2026-07-27 re-scope. Final accepted Tenants SHA is `f9e51c66745557da4f267ab40f32294f2f27fae7`; validated source identity is EventStore `150216c3831370146814fc23d6b1437e3c97a6d5`; validated catalog identity is Builds `53d53ae42abf7c87d385a078ab260531480bbf8a` resolving EventStore packages `3.83.0`. This supersedes the prior accepted Tenants SHA `578770679b9d3bc3fdf2a8a78190f24cdad8576e`; both receipts remain required history.

**Acceptance Criteria:**

**Given** Story 1.20 lacks any durable `available` decision, consumer-migration authorization, tested runtime SHA, named EventStore/release-owner approvals, or approved package inventory
**When** Story 2.12 activation is evaluated
**Then** it remains inactive and changes no Tenants, EventStore, or Builds dependency identity
**And** prose, tags, current branch heads, or booleans outside the verified packet cannot substitute for activation authority.

**Given** activation is authorized and Tenants tracks EventStore `main` through its approved dependency automation
**When** each pristine Debug/source lane is checked before restore
**Then** the Tenants gitlink equals the checked-out EventStore `HEAD`, that SHA is reachable from canonical EventStore `origin/main`, and the evidence records the exact SHA validated
**And** both repositories are clean, no EventStore content is edited, and only Tenants-root-declared submodules are initialized.

**Given** the Tenants-pinned Builds commit supplies centralized package authority
**When** Release/package mode restores into a fresh isolated package location
**Then** exactly one published `HexalithEventStoreVersion` governs every consumed `Hexalith.EventStore*` package, all resolved assets use that version as `type: package`, and the configured public source resolves it
**And** no Tenants-local `Version`, `VersionOverride`, fallback property, or `PackageVersion` entry supplies EventStore version authority.

**Given** `Hexalith.EventStore.Gateway` and DomainService can resolve from source or packages
**When** project XML, effective conditions, and evaluated graphs are inspected in both modes
**Then** Gateway follows the same complementary source/package policy as DomainService, source mode resolves only EventStore project edges inside the validated checkout, and package mode resolves no EventStore project edge, including guarded non-output references
**And** no mixed Gateway-project/DomainService-package graph is reachable.

**Given** source and package validation must not contaminate one another
**When** the dual-mode gate runs
**Then** two separate clean clones perform their own identity check, forced restore, build, asset analysis, and mode-matched tests without shared asset files or nested submodule initialization
**And** unattended execution is bounded, warning failures remain fatal, and a green status cannot be emitted after a failed step.

**Given** Tenants `f9e51c66745557da4f267ab40f32294f2f27fae7` is evaluated
**When** final source and package dependency evidence is parsed
**Then** the source lane records 60 EventStore project edges and zero package edges against EventStore `150216c3831370146814fc23d6b1437e3c97a6d5`, while the package lane records 61 package edges, zero project edges, and only version `3.83.0` from Builds `53d53ae42abf7c87d385a078ab260531480bbf8a`
**And** the known analyzer-edge count asymmetry and non-output-reference coverage are explained rather than hidden.

**Given** high-risk compatibility cannot close on compilation or carried-forward evidence alone
**When** Contracts, Server, UI, and Integration suites execute independently in both modes at the accepted Tenants SHA
**Then** Contracts and named dependency guards pass, Server passes 738/738, UI passes 1325/1325, Integration passes 167 with one documented skip and zero failures, and both builds complete with zero warnings and errors
**And** the persisted-path Integration lane—not mock metadata or compilation—satisfies the applicable NFR16 evidence boundary.

**Given** the approved re-scope deliberately decouples source-main tracking from the published package catalog
**When** the final receipt is reviewed
**Then** it discloses that source SHA `150216c3…` is ahead of the `v3.83.0` package tree and never claims exact source/package byte parity
**And** the Release solution's incidental compilation of unconsumed EventStore source is distinguished from the verified package-only Tenants resolution graph.

**Given** cross-repository completion is claimed
**When** authority, CI, drift, and receipt evidence are checked
**Then** maintainer `jpiquot` accepts the exact published Tenants SHA, the EventStore umbrella gitlink records it, accepted CI checks are bound, and the final and prior receipts identify all carried or superseded evidence
**And** later working-tree drift, a newer dependency head, deferred guard generalizations, or an unrelated gitlink cannot silently replace the accepted identity.

## Epic 3: Maintainers Can Release Reproducible, Verifiable Artifacts

Maintainers can build, test, package, publish, and verify EventStore independently of local checkout state, reject invalid candidates without granting authority, and prove exact package and deployed-runtime lineage for a conforming release.

### Story 3.1: Re-Tier Live-Sidecar Tests from the Release Gate

As a release maintainer,
I want deterministic and live-DAPR tests physically separated into independently executed lanes,
So that live infrastructure coverage remains visible without becoming a semantic-release dependency.

**Requirements coverage:** Primary FR17 and NFR10; supporting NFR16.

**Architecture constraints:** AD-11 and AD-12; test-project ownership, unfiltered commands, and persisted live evidence define the lane boundary.

**Dependencies:** None. Story 3.10 is a local preflight companion, not a prerequisite; later Integration-CI recovery and broad classification remain Epic 7 responsibilities.

**Historical reconciliation:** Retained as Story 3.1 and recorded as done after the approved dedicated-project correction. Final candidate evidence used FrontComposer `b6efcad5` and Tenants `7d7b7012`; PR #334 merged the closure as `7ab1f08d`. The nonconforming squash subject and ruleset bypass remain historical governance evidence, not a reason to manufacture a release-neutral success claim.

**Acceptance Criteria:**

**Given** a test requires a live DAPR sidecar or production-equivalent live topology
**When** project ownership and traits are enumerated
**Then** it resides under `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`, carries `Category=LiveSidecar`, and binds to its applicable live collection such as `DaprTestContainer` or `Oq8Postgresql`
**And** `Server.Tests` contains neither a live-sidecar trait nor the live DAPR fixture, while deterministic mocked/sentinel tests remain there.

**Given** blocking CI and release gating are inspected
**When** shared workflow inputs and same-head verification run
**Then** `Server.Tests` executes unfiltered without DAPR and `Server.LiveSidecar.Tests` is absent from the blocking project input
**And** release requires a successful deterministic CI result for the exact live `main` SHA without depending on Integration Tests.

**Given** the dedicated Integration Tests workflow runs
**When** its live-sidecar job executes
**Then** it initializes DAPR, runs `Server.LiveSidecar.Tests` unfiltered, and uploads TRX and coverage evidence even on test failure
**And** its triggers and concurrency remain independent of semantic-release publication.

**Given** the Redis/DAPR or OQ8 PostgreSQL fixture initializes
**When** live assertions begin
**Then** required Redis or PostgreSQL, placement, scheduler, health, and bounded warm-up prerequisites are established for the applicable topology
**And** state-dependent behavior is verified through fixture/read-back evidence rather than HTTP status or mock calls alone.

**Given** a control plane, container runtime, placement ring, or required app port is unavailable or contaminated
**When** a live test fails before trustworthy product evidence exists
**Then** the result is recorded support-safely as an environment blocker, including failed attempts, rather than averaged away or called a product pass
**And** no threshold, fixture, or lane boundary is weakened without a separately proven defect.

**Given** Story 3.1 validation runs against one candidate workspace
**When** Release build and both unfiltered projects execute
**Then** the build completes with zero warnings/errors, deterministic `Server.Tests` records 2,867 passed and 25 documented skips, and live-sidecar validation records the 49-test result plus any preceding environment-blocked attempt
**And** no trait filter is used to manufacture separation.

**Given** lane documentation and authority sources are reconciled
**When** completion evidence is reviewed
**Then** repository workflows, shared callers, solution/project files, `docs/ci.md`, exact dependency inputs, and the current 17-class/two-collection inventory are cited without turning counts into permanent invariants
**And** repository-specific rules are not copied into synchronized universal assistant instructions.

### Story 3.2: Harden DAPR ETag Timeout for Integration Conditions

As a test maintainer,
I want the DAPR ETag actor timeout to be overridable per service instance while preserving its production default,
So that cold-start integration latency does not masquerade as fail-open ETag behavior.

**Requirements coverage:** Primary FR18; supporting NFR16.

**Architecture constraints:** AD-12; deterministic tests prove timeout mapping and independence, while live evidence proves the persisted actor-state outcome.

**Dependencies:** Story 3.1's physical deterministic/live lane separation.

**Historical reconciliation:** Retained as Story 3.2 and recorded as done. The production seam originally shipped in PR #271 at commit `13320952`; this story closed the missing deterministic override guard without changing production behavior. Owner-ratified dependency advances and direct-to-main/process irregularities remain recorded history, not part of the FR18 runtime unit.

**Acceptance Criteria:**

**Given** normal DI constructs `DaprETagService` without a custom timeout
**When** it creates an ETag actor proxy
**Then** the per-instance `ActorProxyOptions.RequestTimeout` is three seconds and existing scoped `IETagService` registration remains compatible
**And** no unrelated `TimeSpan` or options registration changes the default.

**Given** a caller supplies an explicit timeout
**When** `GetCurrentETagAsync` creates its actor proxy
**Then** the exact supplied value is captured at call time in the `ActorProxyOptions` passed to `CreateActorProxy`
**And** two services constructed before either invocation retain independent timeout values rather than sharing mutable static options.

**Given** the live ETag test needs cold actor-activation tolerance
**When** it constructs the service with a 30-second override and regenerates an ETag
**Then** the service returns the exact Redis-persisted self-routing ETag instead of fail-open `null`
**And** the persisted value retains the `{base64url(projectionType)}.{opaque-id}` contract.

**Given** a genuine actor-edge failure or cancellation occurs
**When** ETag retrieval executes
**Then** non-cancellation actor failures preserve the documented logged fail-open `null`, while `OperationCanceledException` remains distinguishable and is rethrown
**And** argument and actor-identity validation remain unchanged.

**Given** the deterministic override tests are mutation-checked
**When** production options are temporarily made shared/static or the override is ignored
**Then** the call-time capture or per-instance-independence fact fails, and both pass again after restoring the production source byte-for-byte
**And** no live sidecar is required to detect either mapping regression.

**Given** Story 3.2 validation runs in Release/package mode
**When** focused, full deterministic, and live suites execute
**Then** `DaprETagServiceTests` passes 16/16, unfiltered `Server.Tests` passes 2,869 with 25 documented skips, the ETag live class passes 2/2, and the full live project passes 49/49 with zero failures
**And** builds complete with zero warnings/errors and persisted ETag evidence is cited by value rather than inferred from test status.

**Given** the FR18 boundary is reviewed
**When** completion is accepted
**Then** the three-second production default, per-instance options, fail-open/rethrow contract, 30-second live value, fixture thresholds, and CI lane wiring remain unchanged
**And** optional appsettings/`IOptions` runtime tuning and unrelated integration hardening remain deferred.

### Story 3.3: References-Based Submodule Layout

As a repository maintainer,
I want root-declared Hexalith modules, build paths, documentation, and Aspire metadata to use the `references/` convention,
So that external checkouts remain separated from EventStore source without fragile root-level path assumptions.

**Requirements coverage:** Primary FR19; supporting NFR9.

**Architecture constraints:** AD-9, AD-11, and AD-12; only root-declared submodules are in scope, `.slnx` remains authoritative, and the flexible resolver is preserved.

**Dependencies:** None. Story 3.5 consumes this verified layout but is not required to complete it.

**Historical reconciliation:** Retained as Story 3.3 and recorded as done. The layout shipped through the approved 2026-06-26 correction and was reverified at `1d42528b`; the later source-inspection hardening added `*.cs text eol=lf` and brought AppHost validation to 54/54. Concurrent Memories/Tenants gitlink bundling was retroactively accepted as an exception and is not represented as the layout implementation.

**Acceptance Criteria:**

**Given** the repository root `.gitmodules` is inspected
**When** every root-declared path is enumerated
**Then** all seven Hexalith submodules are declared beneath `references/` and no root-level `Hexalith.*` directory is required
**And** no nested submodule is initialized, updated, or treated as a root dependency.

**Given** package or explicit source mode evaluates repository paths
**When** `Directory.Build.props`, `Directory.Packages.props`, and `Hexalith.EventStore.slnx` are processed
**Then** local/default Builds, Tenants, Commons, and other module paths resolve through `references/`
**And** the 46-project `.slnx` restores and builds in Release/package mode without creating or using a legacy `.sln`.

**Given** assistant instructions, documentation, workflows, generated references, and root-owned source mention Hexalith checkouts
**When** stale-path scans run
**Then** actionable paths use `references/Hexalith.*`, while historical proposal examples, namespace/type names, submodule-owned content, and tree children visibly nested beneath `references/` are classified rather than rewritten
**And** the synchronized instruction entry points continue to locate the baseline without embedding EventStore-specific policy.

**Given** a consuming AppHost resolves EventStore project metadata
**When** `RepositoryProjectPaths` and `EventStorePlatformProjectMetadata` execute
**Then** the flexible helper supports the current repository, dependency checkout, parent layout, and `references/<module>/...` fallback cases
**And** it is not replaced by a fixed references-only path that would break valid hosting contexts.

**Given** tracked C# source may be checked out on different platforms
**When** AppHost source-inspection guards parse structural blocks
**Then** repository attributes enforce LF for `*.cs`, readers normalize CRLF narrowly where needed, and equivalent LF/CRLF fixtures produce the same result
**And** whole-file guards still reject infrastructure references outside an extracted local block.

**Given** Story 3.3 validation runs
**When** layout scans, `.slnx` listing, restore, Release build, resolver tests, AppHost tests, and Tier-1 regression execute
**Then** root layout and path facts pass, build has zero warnings/errors, AppHost passes 54/54, and no stale root-owned path remains
**And** exact command results distinguish project counts from output-line counts.

**Given** unrelated gitlink or submodule content changes are present
**When** story scope and completion evidence are reviewed
**Then** they are preserved and excluded unless separately authorized, reachable, and explicitly recorded
**And** this verification does not rerun the migration, redesign dependency modes, or absorb shared-workflow work.

### Story 3.4: Aspire Security Resource Naming

As an operator,
I want the Keycloak-backed Aspire resource to use the service-role identity `security`,
So that topology, diagnostics, generated deployment output, and guidance expose a stable role rather than an implementation name.

**Requirements coverage:** Primary FR20; supporting NFR9 and NFR16.

**Architecture constraints:** AD-9 and AD-12; resource identity changes are topology-wide, while Keycloak implementation/configuration terminology remains intact.

**Dependencies:** Story 3.3's verified AppHost/project layout. Source-mode Tenants topology coverage is a separately recorded CI follow-up, not a completion dependency.

**Historical reconciliation:** Retained as Story 3.4 and recorded as done at final revision `1f59b3f09fe7137c849fd52516e727f7c70a297b`. The production default already existed; this verification/reconciliation added literal app-model guards, operator-document corrections, generated Compose proof, and live topology evidence without changing production source.

**Acceptance Criteria:**

**Given** default EventStore security options with Keycloak enabled
**When** the reusable helper and actual AppHost model are built
**Then** the Keycloak-backed resource is named by the literal `security`, matching `DefaultResourceName`, while the supported override remains available
**And** realm import, authentication wiring, endpoint behavior, preferred-port walk-forward, and Keycloak-specific implementation names remain unchanged.

**Given** an application uses `WithSecurityDependency`
**When** its model annotations are inspected
**Then** reference and wait relationships target the `security` resource with their documented exact cardinalities and no relationship targets `keycloak`
**And** the expected dependents are EventStore, Admin Server, Admin UI, Sample API, Sample UI, plus Tenants resources only when those resources exist in the compiled topology.

**Given** integration fixtures, UI authority fixtures, operator documentation, agent guidance, and deployment examples refer to the identity provider
**When** Git-tracked text is audited
**Then** resource, service, DNS, endpoint-client, wait, and Compose identities use `security`
**And** Keycloak remains the correct word for images, implementation APIs, realms, token flows, options, and configuration keys.

**Given** the stale-role audit runs
**When** its positive controls and audited path coverage are mutation-tested
**Then** every intended tracked tree and root Markdown contributes input, seeded obsolete identities fail the guard, and generated/build output is excluded
**And** a missing path, Git error, or vacuous scan cannot be reported as clean.

**Given** Docker Compose output is generated into a validated temporary directory
**When** the artifact is inspected before bounded cleanup
**Then** it contains one `security` service, exactly five applicable dependents, internal `security:8080` DNS, `OTEL_SERVICE_NAME=security`, and no `keycloak` service or DNS identity
**And** documentation distinguishes the preferred host `:8180` from container target `:8080` and does not prescribe unsafe broad deletion.

**Given** the live AppHost is run with security enabled
**When** `aspire wait` and filtered `aspire describe` evidence are captured without printing secrets
**Then** `security` is a healthy running container with `OTEL_SERVICE_NAME=security`, the five application resources wait on it, and no display name is `keycloak`
**And** lifecycle or CLI-environment failures are reported separately rather than weakening the identity contract.

**Given** Story 3.4 validation runs
**When** package-mode restore/build, focused helper/model/port tests, full AppHost tests, Admin UI authority fixtures, JSON validation, audit, Compose publish, and live proof execute
**Then** Release build has zero warnings/errors, focused classes pass 8/8, 3/3, and 20/20, full AppHost passes 63/63, Admin UI classes pass 9/9 and 7/7, and generated/live identity checks pass
**And** deferred CLI-flag, shared-temp, source-mode-CI, and version-documentation issues remain explicit and do not become hidden acceptance claims.

### Story 3.5: Shared Package Catalog and Source/Package Reference Modes

As a package maintainer,
I want dependency mode selected explicitly and every source-owned NuGet version governed by Hexalith.Builds,
So that source debugging is intentional, package builds are reproducible, and consumers cannot mask shared updates.

**Requirements coverage:** Primary FR21; supporting NFR9 and NFR16.

**Architecture constraints:** AD-11 and AD-12; package mode is the universal default, source is explicit opt-in, and every owning repository retains its own approval boundary.

**Dependencies:** Completed Story 3.3 references layout plus completed Stories 1.20 and 2.12 for the reconciled Tenants Gateway graph.

**Historical reconciliation:** Retained as Story 3.5 and recorded as done. The closed seven-repository inventory is Builds, EventStore, Commons, FrontComposer, Memories, PolymorphicSerializations, and Tenants. Final read-only validation bound EventStore `e4618d9114c8824fd50fdfc8d135438aa261377c` and Builds `61e43b18b59176e33ef8d389028900292905fbad`; a later-discovered repository requires a named follow-up rather than reopening this frozen inventory.

**Acceptance Criteria:**

**Given** `UseHexalithProjectReferences=true` is explicitly supplied
**When** an external Hexalith dependency is evaluated
**Then** available root-declared source is selected, while missing source activates the centrally pinned package fallback
**And** explicit `UseHexalithProjectReferences` wins over contradictory legacy `UseNuGetDeps` input.

**Given** `UseHexalithProjectReferences` is unset or explicitly `false`
**When** Debug, Release, or configuration-less evaluation runs
**Then** package references are selected and no external source edge activates
**And** every conditional dependency resolves through exactly one active source/package edge in each mode.

**Given** a project is a same-repository component or a non-packaged application host
**When** dependency pairs are normalized
**Then** same-repository references remain project references and AppHost/Admin application edges remain genuine source-only host relationships
**And** no fake package identity is invented merely to make modes appear symmetrical.

**Given** a build changes dependency mode
**When** restore, build, or test validation runs
**Then** the selected mode performs a fresh restore before any `--no-restore` operation and shared workflows pass explicit Release/package intent
**And** stale project-reference assets cannot contaminate package evidence.

**Given** the seven inventoried repositories and shared Builds governance surfaces are scanned
**When** package-version authority is evaluated
**Then** every source-owned dependency version originates in the Builds catalog and consuming props/projects contain no local `PackageVersion`, `VersionOverride`, fallback version property, or versioned package reference
**And** each repository's exact owner, scope, commit, rollback, and validation evidence is retained.

**Given** EventStore's former local masks and packable ServiceDefaults edge are reconciled
**When** effective catalog and packed metadata are inspected
**Then** Builds supplies exactly one `NBomber.Http` `6.2.1` and `xunit.v3.extensibility.core` `3.2.2` row, the unused Commons.ServiceDefaults source edge is absent, and EventStore package metadata contains no unintended Commons.ServiceDefaults dependency
**And** migrated identities resolve exactly once.

**Given** current catalog versions are adopted
**When** behavioral consumers execute
**Then** `System.CommandLine` `2.0.10`, `ModelContextProtocol` `1.4.1`, `Microsoft.Extensions.TimeProvider.Testing` `10.8.0`, NBomber `6.5.0`, and Playwright `1.61.0` are inherited and exercised
**And** the migration cannot pass as a formatting-only edit.

**Given** Tenants consumes EventStore Gateway and DomainService
**When** source and package graphs are evaluated after authorized Story 2.12 alignment
**Then** both dependencies follow complementary mode conditions and package mode contains no mixed Gateway-project/EventStore-package graph
**And** no Tenants dependency identity is changed by this story itself.

**Given** documentation, scripts, samples, and dependency automation are reviewed
**When** catalog ownership is communicated and checked
**Then** Builds is named as the sole version owner, `scripts/check-doc-versions.sh` reads the shared catalog, official samples do not invite local versions, and consumer NuGet Dependabot updates are disabled
**And** tool manifests, SDK pins, ephemeral fixtures, and caches are explicitly classified as non-CPM rather than rewritten.

**Given** Story 3.5 validation runs across the closed inventory
**When** catalog validators, mode truth tables, ownership scans, fresh source/package builds, packed metadata, focused consumer suites, and full Debug/source integration execute
**Then** Builds validates 284 entries, EventStore Contracts passes 817/817, both EventStore solution modes build with zero warnings/errors, all inventoried consumer projects validate, and Integration passes 279/279 with native lease cleanup
**And** no nested submodule, unrelated version refresh, release-manifest change, publication, or gitlink mutation is smuggled into closure.

### Story 3.6: Manifest-Driven Release Packaging

As a release maintainer,
I want one fail-closed manifest and package-metadata contract to govern EventStore packing and validation,
So that release output cannot omit, rename, disguise, or accidentally include packages outside the approved inventory.

**Requirements coverage:** Primary FR22; supporting NFR9, NFR11, and NFR16.

**Architecture constraints:** AD-11 and AD-12; `tools/release-packages.json` is the sole package inventory and validation inspects archive bytes/metadata rather than filenames alone.

**Dependencies:** Story 3.5's package-safe dependency modes and shared catalog authority.

**Historical reconciliation:** Retained as Story 3.6 and recorded as done at final revision `13ccd4fdf6f3b9cc5f85c6747ca62566dd45204f`. The accepted inventory contains exactly 14 root-owned EventStore packages; the unscoped GitHub Release asset glob remains a documented follow-up mitigated by fail-closed prepare validation, not a second inventory authority.

**Acceptance Criteria:**

**Given** `tools/release-packages.json` is loaded
**When** its entries and evaluated projects are normalized
**Then** exactly 14 unique `Hexalith.EventStore*` package IDs map to existing, packable projects contained beneath root `src/`, with evaluated `PackageId` equality
**And** malformed, duplicate, missing, out-of-scope, submodule, mismatched, or omitted packable-project cases fail before packing.

**Given** a valid release inventory and semantic version
**When** pack commands are generated
**Then** exactly the 14 manifest projects are packed in manifest order with Release, `GeneratePackageOnBuild=false`, and `UseHexalithProjectReferences=false`
**And** dependent builds and non-manifest `src/` projects cannot emit additional packages.

**Given** release archives are produced
**When** semantic-release and CI validators inspect them
**Then** canonical filenames, embedded NuGet IDs, versions, package types, dependency groups, and archive paths match the manifest contract
**And** missing, extra, duplicate, renamed, foreign, mixed-version, noncanonical-case, forged-tool-type, or malformed archives fail closed.

**Given** a package contains internal or external Hexalith dependencies
**When** every applicable target-framework group is validated
**Then** complete unique dependency identities use the exact release/catalog versions and no source project/path resolution leaks into metadata or archive entries
**And** Gateway independently retains its required Admin.Abstractions, Server, Contracts, and ServiceDefaults package edges.

**Given** archive metadata or entries contain traversal, rooted, drive-relative, UNC, home-relative, `bin/`, `obj/`, `artifacts/`, project-file, or checkout-path shapes
**When** the whole namespaced nuspec and archive are scanned
**Then** validation rejects the archive with a bounded diagnostic naming the matched evidence
**And** XML encoding, attribute/element position, sibling sections, or path separator choice cannot bypass the check.

**Given** each manifest package is tested as a consumer
**When** isolated package-only fixtures restore and build
**Then** each of the 13 library packages resolves only from the local release directory plus approved public dependencies, and the one manifest-owned tool package installs from its isolated package cache
**And** direct sibling references or source checkout state cannot mask missing metadata.

**Given** semantic-release commands are governed
**When** prepare and publish ordering is inspected
**Then** pack precedes both archive-aware validators, credentials are checked before irreversible publish, and NuGet push is scoped exactly to `./nupkgs/Hexalith.EventStore.*.nupkg`
**And** validation timeouts and the whole-inventory deadline prevent a hung project evaluation from becoming an indefinite release.

**Given** Story 3.6 validation runs
**When** dry pack, Contracts build/tests, real 14-package pack, both validators, isolated consumers, mutation cases, and whitespace checks execute
**Then** 14 safe commands are produced, focused manifest tests pass 89/89, full Contracts passes 878/878, all archives validate, and all 14 isolated consumers succeed
**And** no package, registry, GitHub Release, submodule, or external publication mutation is performed by this story.

### Story 3.7: Shared Workflow Caller Migration

As a repository maintainer,
I want EventStore CI, release, and security workflows to delegate to approved Hexalith.Builds workflows,
So that module-specific automation stays thin while deterministic and live-infrastructure lanes retain distinct release semantics.

**Requirements coverage:** Primary FR25 and NFR10; supports FR17 and FR22.

**Architecture constraints:** AD-11 and AD-12; shared callers own reusable execution while repository files retain only module-specific triggers, permissions, secrets, concurrency, and inputs.

**Dependencies:** Story 3.1's physical test split and Story 3.6's manifest-backed package entry points.

**Historical reconciliation:** Retained as Story 3.7 and recorded as done, split from the completed combined `spec-3-7-shared-ci-cd-security-gates-and-supply-chain-backlog.md`. This story owns caller migration only; reference/cache/publish safety remains Story 3.8 and unresolved publishing capabilities remain Story 3.9.

**Acceptance Criteria:**

**Given** CI, release, CodeQL, dependency-review, and commitlint workflow files are inspected
**When** migration is complete
**Then** each is a thin caller of its approved Hexalith.Builds reusable workflow at the governed reference
**And** local YAML contains only EventStore triggers, concurrency, permissions, secrets, timeouts, and documented input values.

**Given** deterministic test inputs are passed to shared domain CI
**When** its test lane runs
**Then** `Server.Tests` executes unfiltered and no DAPR installation, `Category!=LiveSidecar` filter, or live project is required for the blocking lane
**And** every deterministic test project is classified into an executable shared-CI input.

**Given** live-sidecar coverage exists
**When** the dedicated Integration Tests workflow executes
**Then** it initializes DAPR and runs the physical `Server.LiveSidecar.Tests` project unfiltered with visible result artifacts
**And** release has no dependency on this advisory infrastructure lane.

**Given** browser, governance, or evidence suites are intentionally non-release-blocking
**When** advisory workflow inputs execute
**Then** required Playwright browser prerequisites are installed, outcomes remain visible, and every included or deferred project has explicit lane ownership
**And** advisory classification cannot be satisfied by an incidental documentation mention.

**Given** shared domain CI invokes EventStore package validation
**When** it calls repository `scripts/` compatibility entry points
**Then** those wrappers delegate to the manifest-governed pack, archive validation, and package-only consumer contracts
**And** CI's synthetic version is normalized consistently without suppressing downgrade failures.

**Given** shared domain release prepares artifacts
**When** the EventStore caller supplies its publication inputs
**Then** semantic-release remains responsible for the 14 manifest packages and the only container mapping is `src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore`
**And** no Sample, Admin, or additional container is introduced by caller migration.

**Given** a push reaches `main`
**When** commit and release governance runs
**Then** direct pushes are covered by commitlint and release verifies that the successful deterministic CI result belongs to the exact source head before continuing
**And** shared-caller delegation does not weaken branch or release semantics.

**Given** Story 3.7 validation runs
**When** workflow scans, wrapper compilation, package dry-run/validation, lane inventory, deterministic tests, live-project listing, Release build, and documentation checks execute
**Then** thin callers, 14-package scope, independent lanes, and `.slnx`-only operation pass with warnings-as-errors
**And** no shared Builds workflow, nested submodule, publication target, or external artifact is modified without separate authority.

### Story 3.8: Workflow Reference and Validation Safety

As a release maintainer,
I want workflow references, caches, source identity, credentials, package validators, and publish ordering to fail safely,
So that shared automation cannot reuse an incompatible graph or begin an irreversible release from untrusted inputs.

**Requirements coverage:** Supports primary FR22 and FR25; primary NFR9 and NFR11; supporting NFR10 and NFR16.

**Architecture constraints:** AD-11 and AD-12; reusable-workflow authority, manifest identity, exact source, and pre-publication checks are independently guarded.

**Dependencies:** Stories 3.6 and 3.7.

**Historical reconciliation:** Retained as Story 3.8 and recorded as done, split from the completed combined Story 3.7 specification and subsequent hardening. Shared development callers generally use their governed `@main` contract; the production release caller and `builds-execution-sha` are locked together at exact Builds commit `f75daebd4c522c081a6f62e274cf25e07971de69`.

**Acceptance Criteria:**

**Given** shared workflow and action references are scanned
**When** caller authority is evaluated
**Then** each reference uses its approved moving or immutable policy, and the production release caller's workflow revision exactly equals its `builds-execution-sha`
**And** a stale, mismatched, unapproved, or locally reimplemented reference fails governance.

**Given** source and package dependency modes can produce incompatible assets
**When** workflows restore or reuse caches
**Then** each mixed-mode cache key includes the relevant dependency-mode and catalog inputs, while fixed package-only lanes cannot restore source-mode build outputs
**And** a mode switch performs a fresh restore before `--no-restore` build or test.

**Given** release is manually dispatched
**When** the unprotected `verify-source` preflight runs
**Then** the dispatch ref is `refs/heads/main`, its source is a lowercase 40-hex SHA equal to the live main tip, and a successful completed push-CI run exists for that exact SHA
**And** the protected production release job cannot start on stale, branch, PR, or merely similar CI evidence.

**Given** release credentials and destinations are required
**When** semantic-release verifies or publishes
**Then** NuGet, container-publisher, and registry credentials plus destination/tag/source invariants are checked before Git-tag creation or any NuGet/container write, and the destination is rechecked immediately before publish
**And** absent, blank, malformed, or mismatched secret/identity evidence fails before irreversible mutation.

**Given** package and container scope are declared
**When** caller inputs and semantic-release commands are validated
**Then** the package manifest path and independent expected count remain `tools/release-packages.json` and 14, NuGet push uses only the EventStore-scoped glob, and only the `eventstore` container mapping is enabled
**And** prefix-collision IDs, missing/extra archives, or alternate manifest scope cannot redefine the gate.

**Given** release prepare and publish commands are inspected
**When** ordering assertions execute
**Then** packing precedes archive validation, all validation precedes publication, NuGet secrets are proven before NuGet push, and container publication uses the helper installed by the same governed shared publisher
**And** a partially prepared artifact set cannot be called a release success.

**Given** deterministic, advisory, and live lanes are classified
**When** guardrails scan projects and workflow inputs
**Then** live-sidecar tests remain outside release, advisory Playwright installs Chromium, evidence uploads occur on failure, and all projects have an executable or explicitly deferred owner
**And** timeouts are sufficient for the declared workload without becoming unbounded.

**Given** Story 3.8 validation runs
**When** YAML/reference scans, cache assertions, exact-source and secret-preflight mutation fixtures, manifest/package wrappers, release-order tests, Release build, and `docs/ci.md` consistency checks execute
**Then** every unsafe reference, source, cache, credential, package scope, or ordering mutation fails with a support-safe diagnostic and the accepted path remains green
**And** validation does not itself publish, tag, deploy, or modify registry state.

### Story 3.9: Supply-Chain Publishing Backlog

As a release owner,
I want unresolved supply-chain publishing work captured as a reviewed backlog product,
So that credential modernization and artifact provenance gaps remain visible without being silently implemented or conflated with completed safeguards.

**Requirements coverage:** Supports primary FR25 and NFR11.

**Architecture constraints:** AD-11 and AD-12; this is a planning/evidence artifact only and grants no workflow, credential, registry, repository-setting, or runtime authority.

**Dependencies:** Completed Stories 3.6–3.8. No later release story is a prerequisite; later evidence may be cited without retroactively authorizing this backlog.

**Historical reconciliation:** Retained as Story 3.9 and recorded as done. The accepted product `_bmad-output/planning-artifacts/backlog/supply-chain-publishing.md` contains seven open items and a five-theme crosswalk; Administrator was explicitly delegated Paige's artifact-owner and Amelia's feasibility-review roles on 2026-08-01.

**Acceptance Criteria:**

**Given** current release callers, semantic-release commands, credential guidance, package validators, security callers, and release evidence are inspected
**When** unresolved supply-chain work is cataloged
**Then** the standalone backlog contains exactly the evidenced open inventory rather than hiding items in a completed implementation story
**And** every cited repository-relative evidence path exists and records its verification date.

**Given** trusted publishing, attestations, SBOMs, provenance, and credential modernization are required themes
**When** theme completeness is reviewed
**Then** each theme maps to at least one open item or an explicit evidence-backed no-gap disposition
**And** the accepted crosswalk maps trusted publishing to SCP-1, attestations to SCP-4/SCP-5, SBOM to SCP-3, provenance to SCP-3–SCP-7, and credential modernization to SCP-1/SCP-2.

**Given** a backlog item is recorded
**When** its schema is validated
**Then** it states lifecycle, scope, accountable owner roles, dependencies, risks, minimum validation expectations, and current evidence paths
**And** naming an external owner records coordination need without claiming that owner has accepted delivery.

**Given** lifecycle classifications are used
**When** an item's status is reviewed
**Then** `open`, `blocked`, `accepted-risk`, and `closed` retain their documented distinct meanings and review requirements
**And** no open gap is represented as implemented merely because its feasibility was reviewed.

**Given** manifest, archive/consumer, exact-source/publisher, credential/destination, and OCI mapping controls are already complete
**When** the backlog baseline is assembled
**Then** each closed safeguard is listed with its current evidence paths and remains closed
**And** changing or reopening one requires a new approved implementation story.

**Given** focused read-only validation runs
**When** credential/theme, shared-reference, publication-capability, and evidence-path scans execute
**Then** current API-key/Zot use and all seven unresolved items are evidenced, the release caller's immutable pin and other callers' current policy are classified, and no hidden trusted publishing, SBOM, attestation, or signing capability is found
**And** validation performs no external publication or settings mutation.

**Given** Paige and Amelia review is required
**When** Administrator acts under the recorded explicit delegation
**Then** the seven-item inventory, feasibility boundary, evidence paths, and closed-control baseline receive a dated accepted disposition
**And** delegation does not accept implementation ownership for external maintainers.

**Given** Story 3.9 is complete
**When** any future team reads the artifact
**Then** it can select a separately approved, owner-coordinated implementation story with defined risks and validation expectations
**And** this planning story itself authorizes no publishing, workflow, credential, registry, package, container, repository, or runtime change.

### Story 3.10: Generated API DAPR/Aspire Smoke Preflight

As a developer validating generated API behavior,
I want a support-safe local preflight for environment, topology, sidecars, endpoints, and persisted state,
So that infrastructure blockers are distinguished from product defects before runtime evidence is accepted.

**Requirements coverage:** Validation support for FR17 and FR34; primary NFR16; supporting NFR1, NFR2, and NFR15.

**Architecture constraints:** AD-9 and AD-12; the tool is read-only by default, topology mutation requires an explicit flag, and HTTP status alone is never full evidence.

**Dependencies:** Story 3.1's live-lane boundary and Epic 2's generated Sample API surface.

**Historical reconciliation:** Retained as Story 3.10 and recorded as done, reissued from historical Story 3.8. The completed tool, commands, live results, and review record remain in the superseded artifact; the active identity strengthens future success evidence to require both persisted event and persisted read-model/query-state read-back without reopening the tooling implementation.

**Acceptance Criteria:**

**Given** the preflight runs with no start flags
**When** it checks a developer environment
**Then** it performs read-only Docker, Aspire CLI, DAPR CLI/runtime, `daprd`, placement, scheduler, topology, and endpoint probes without starting processes or containers
**And** any mode that starts control-plane or Aspire resources requires an explicit flag and reports the exact bounded command.

**Given** a required prerequisite is missing, unhealthy, disconnected, denied, or unreachable
**When** classification executes
**Then** it emits a support-safe `blocked-environment` result and minimal documented next action before product smoke begins
**And** a later timeout, HTTP failure, or higher numeric exit code cannot overwrite that root classification.

**Given** a live Aspire topology exists
**When** `aspire describe` output is parsed
**Then** EventStore, Sample domain, generated Sample API, Redis/statestore, and their DAPR sidecars are discovered by display name and published endpoints, with optional Tenants reported only when present
**And** local HTTP is preferred, an HTTPS-only or endpoint-less required host follows the documented fail-closed policy, and private addresses are not disclosed.

**Given** EventStore or generated-API sidecar metadata is available
**When** diagnostics inspect it
**Then** app ID, local DAPR endpoint/port, metadata availability, actor `hostReady`, placement connectivity, scheduler state, health, and access denial are classified distinctly
**And** DAPR API tokens, bearer tokens, JWTs, connection strings, raw metadata, payloads, or stack traces are never printed.

**Given** `--sample-api-smoke` is explicitly requested in approved local dev-auth mode
**When** the generated command and query routes execute
**Then** the command returns `202` with its valid `Location`/`Retry-After` contract, the query returns `200` with ETag, and revalidation returns `304`
**And** unreachable/auth/control-plane failures remain environment classifications while a healthy missing/broken generated route is a product failure.

**Given** a generated-API smoke seeks a successful evidence verdict
**When** state evidence is read
**Then** it proves both the persisted event and the corresponding persisted read-model/query-state end result through bounded production-path read-back
**And** missing, unreadable, stale-presence-only, or partial evidence returns `state-evidence-unavailable` or the distinct state-evidence failure instead of success.

**Given** human or JSON output is emitted
**When** external tool text enters the report
**Then** categories include `environment`, `aspire`, `dapr`, `generated-api`, `state-evidence`, and `next-action`, with distinct exits for success, blocked environment, topology absence, product failure, and state-evidence failure
**And** centralized redaction preserves safe scheme/context while removing secrets, compact JWTs, non-local identities, emails, concrete tenant/user IDs, private addresses, and raw traces.

**Given** Story 3.10 validation runs
**When** Bash syntax/tests, redaction/classification tests, Testing.Integration, AppHost regression, Release build, blocked-environment mutation, and an available live topology execute
**Then** argument parsing, exit precedence, temp cleanup, support-safe diagnostics, endpoint discovery, sidecar readiness, generated API behavior, and required persisted evidence pass without solution-level `dotnet test`
**And** exact environment blockers are retained when the live gate cannot run.

### Story 3.11: Validated Central Package Catalog Refresh

As a Hexalith release maintainer,
I want every central NuGet catalog row audited and updated only through compatibility-proven rollback groups,
So that all consumers inherit the latest validated compatible set from one reproducible authority.

**Requirements coverage:** Supports primary FR21, FR22, and FR25; primary NFR9; supporting NFR10 and NFR16.

**Architecture constraints:** AD-11 and AD-12; live discovery is time-varying, while the checked-in audit contract and release validation are deterministic.

**Dependencies:** Completed Story 3.5 ecosystem catalog migration and a complete Builds inventory.

**Historical reconciliation:** Retained as Story 3.11 and recorded as done. The frozen packet binds Builds `9dc0fe1ffbf33269fddf195fd12317def86728f0`, EventStore `caef47fcff54ade19f50cf752c25aeb74e639afa`, audit SHA-256 `507496549651a66f17dac221b2632b5ff9c5f4eb40055fbfeafcfd3c93e9bffa`, 284 package rows, 139 families, and five changed rollback groups: IdentityModel, bUnit/AngleSharp, Aspire DAPR hosting, Scriban, and SonarAnalyzer.

**Acceptance Criteria:**

**Given** Story 3.5 is incomplete or the evaluated Builds catalog is not authoritative and non-empty
**When** Story 3.11 activation is evaluated
**Then** no catalog-wide refresh is accepted
**And** partial local version changes cannot substitute for the prerequisite.

**Given** configured NuGet sources and the evaluated catalog
**When** the live freshness audit runs
**Then** every unique row records package ID, current version, stable and applicable prerelease candidates, per-source listing/resolution state, family, disposition, evidence, catalog revision, and zero-offset UTC timestamp
**And** duplicates, omissions, orphan families, output collisions, ambiguous sources, or incomplete counts fail closed.

**Given** a compatible stable update is available
**When** its disposition is selected
**Then** the latest validated stable version is preferred, while intentional prerelease channels, major changes, stable/prerelease transitions, and SDK/framework-coupled updates require explicit proof
**And** no version is guessed from incomplete search results.

**Given** packages share a Hexalith version property or compatibility family
**When** an update is accepted
**Then** every family member, relevant non-CPM exception, representative consumer, and rollback instruction agrees within one reviewable group
**And** IdentityModel/Microsoft.Identity.Web, bUnit/AngleSharp, Aspire/DAPR, and other coupled rows cannot be split silently.

**Given** a candidate is absent, unlisted, unresolved, older than the current pin, or lacks compatibility evidence
**When** the audit disposes it
**Then** the current compatible version is retained with source diagnostics, rationale, and a concrete recheck/removal trigger
**And** OpenAPI remains on proven 2.x, SourceLink is not downgraded, Tenants changes require its release owner, and Azure Monitor/StackExchange.Redis candidates remain unaccepted until their stated proofs pass.

**Given** the checked-in audit is validated offline
**When** catalog, per-source, family, exception, and workflow contracts are checked
**Then** every row reconciles to the evaluated catalog and accepted dispositions require listed evidence from every configured source
**And** fixture suites cover paging, large prerelease identifiers, absent/unlisted packages, unresolved sources, metadata invariants, family splits, and output/catalog collisions.

**Given** accepted rollback groups are applied
**When** Builds and EventStore validate from fresh package-mode restores with NuGet audit enabled
**Then** central/authoritative/consumer/exception/DAPR validators, Builds solution/tests, EventStore Release build, Contracts, SignalR, Admin UI, AppHost, REST generator, Server, release-package, and documentation-version checks pass
**And** each changed family can be reverted independently without local consumer version authority.

**Given** the frozen packet contains 284 rows
**When** a package or family is added after its audit timestamp
**Then** it is routed to a named follow-up unless the owner explicitly supersedes and reruns the complete packet
**And** the accepted inventory cannot expand implicitly during review.

**Given** completion is requested
**When** exact identities, results, and authority are reviewed
**Then** 13 accepted rows in five groups, 271 retained rows, eight feed-missing retained IDs, one configured source, validation results, rollback boundaries, and all retained exceptions are recorded
**And** Administrator's dated Builds-maintainer and EventStore-maintainer approvals bind only the exact approved commits and authorize no package publication.

### Story 3.12: Multi-Platform EventStore Container Publishing Correction

As an EventStore release owner,
I want the shared release path to publish an exact two-platform OCI index,
So that an immutable corrective release can restore the required container shape without overwriting a failed historical release or broadening the governed package scope.

**Requirements coverage:** Primary FR22 and FR25; supporting NFR9, NFR11, NFR16, and NFR17.

**Architecture constraints:** AD-11, AD-12, and AD-22; publication is an externally visible, authority-bearing mutation and all registry evidence is bound to immutable raw bytes and digests.

**Dependencies:** Completed Stories 3.6 and 3.8 establish the 14-package manifest and thin shared-workflow caller; the independently governed Story 1.20 packet consumes only observed evidence.

**Historical reconciliation:** Retained as Story 3.12 and recorded as done. Release v3.75.0 remains the immutable single-platform failure fixture. Partial release v3.77.1 remains permanently quarantined. Under durable authority comment `5016454096`, release v3.77.2 bound source `77a9a442c0e6d0408957888e10c3a9accd634c99`, Builds `9ec0a032d785dd0abdc14276e8784d6fdd826fd0`, workflow run `29694935552`, evidence artifact `8444768158`, and OCI index digest `sha256:db3ab41e187efc0de397fd1205660a0f685e2c94ecd8f4a8f1843ac567056bf6`. Later runtime-disposition stories do not reopen or reinterpret this completed publishing correction.

**Acceptance Criteria:**

**Given** v3.75.0 is used as regression evidence
**When** the shared OCI validator evaluates its immutable registry bytes
**Then** its single `linux/amd64` manifest and absent `linux/arm64` descriptor fail the exact-index contract deterministically
**And** no v3.75.0 package, tag, manifest, registry object, or observed 14-package hash is overwritten or reclassified.

**Given** the shared Hexalith.Builds publisher prepares an EventStore release
**When** the one approved `eventstore` mapping is published
**Then** native .NET SDK container support produces exactly `linux/amd64` and `linux/arm64` children beneath an OCI image index
**And** a single, missing, duplicate, extra, variant, blank, or `unknown/unknown` platform fails closed.

**Given** the version tag resolves to an index
**When** immutable validation runs
**Then** the untouched index bytes hash to the registry digest, each descriptor and config resolves by digest with matching size and media type, and every config OS/architecture agrees with its descriptor
**And** tag/digest disagreement, malformed data, unresolved content, or any digest, size, media-type, or platform mismatch fails the release gate.

**Given** both platform children are available
**When** bounded release smoke executes
**Then** each immutable child digest starts and passes the same loopback `/alive` contract, followed by deterministic cleanup
**And** emulation/setup failure, image-start failure, and liveness timeout remain distinct non-pass outcomes with support-safe evidence.

**Given** the first NuGet or registry mutation is requested
**When** release preflight evaluates authority
**Then** an authenticated, durable, unexpired release-owner record binds the repository, new version, source SHA, registry repository, exact platform set, approved Builds execution identity and helper hashes, owner, rationale, timestamp, and validity window
**And** planning approval, story approval, tag existence, or a prior authority record cannot authorize the mutation or be replayed for a different identity.

**Given** a release attempt publishes only part of its governed identity or later smoke fails
**When** retry or disposition is considered
**Then** the partial release remains immutable and non-authorizing, as demonstrated by quarantined v3.77.1
**And** a retry requires a fresh semantic version, destination-absence proof, and separately bound authority rather than tag or artifact replacement.

**Given** corrective release v3.77.2 completes
**When** its evidence bundle is independently inspected
**Then** exactly the 14 manifest-governed package IDs share version `3.77.2`, package payloads and signatures verify, only the EventStore container is published, the exact two child configs match their descriptors, and both digest-pinned smokes pass
**And** the retained evidence artifact and raw index independently reproduce the recorded package/container identity.

**Given** v3.77.2 evidence is handed to another story
**When** that story considers approval, deployment, or migration
**Then** it independently revalidates the evidence and its own owner, runtime, limitations, and authorization gates
**And** Story 3.12 does not modify Parties or Tenants, approve G5, authorize consumer migration, or manufacture deployed-runtime parity.

### Story 3.13: v3.94.1 Deployed Runtime Evidence Disposition

As an EventStore release owner,
I want the immutable v3.94.1 candidate reviewed and disposed through one content-bound evidence envelope,
So that its proven provenance defect is retained as rejected and non-authorizing while positive deployed-runtime parity remains owned by a successor story.

**Requirements coverage:** Supports FR36; primary NFR12 and NFR16; related release integrity under NFR9.

**Architecture constraints:** AD-11, AD-12, and AD-22; a complete negative disposition can close this evidence story but cannot mark a capability available or select a deployed identity.

**Dependencies:** Completed Stories 1.20 and 3.12 are immutable historical inputs only. Story 3.13 can complete independently of and in parallel with Story 3.14; it has no dependency on the corrective release.

**Planning reconciliation:** The stale positive-v3.94.1 title and outcome are superseded. The retained lineage is source `80d12ef5eee71a9fe3ea7be51171da4a71b69a28`, release `v3.94.1`, and all 14 manifest packages at version `3.94.1`; its immutable review-subject digest is `6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97`.

**Acceptance Criteria:**

**Given** the existing v3.94.1 evidence tree is the disposition input
**When** the envelope is assembled
**Then** every retained evidence byte, checksum, package identity, workflow fact, raw OCI index/child/config object, and Production runtime observation remains unchanged and content-bound to the existing review subject
**And** no registry readback, package download, runtime smoke, or subject regeneration is required unless independent verification finds a retained checksum mismatch.

**Given** the v3.94.1 lineage is evaluated
**When** its exact config provenance and authority state are recorded
**Then** the literal malformed value `https` remains recorded for `org.opencontainers.image.source`, `.url`, and `.documentation`, the revision label remains absent, and `deployment_authorized` remains `false`
**And** none of those failed facts may be omitted, normalized, reinterpreted, or reported as passing.

**Given** the machine-readable disposition is emitted
**When** its story-completion shape is validated
**Then** it contains `candidate: v3.94.1`, `candidate_disposition: rejected-non-authorizing`, `deployed_runtime_parity: unavailable-for-v3.94.1`, `selected_deployed_identity: null`, and `deployment_authorized: false`
**And** any pass outcome, non-null selected identity, authorized deployment, omitted defect, or mixed lineage fails closed.

**Given** a retained fact is missing, inconsistent, mutable-only, checksum-invalid, or drawn from another release
**When** the disposition verifier recomputes the evidence relationship
**Then** the envelope is rejected with a support-safe diagnostic and a concrete remediation or revalidation trigger
**And** a complete negative disposition never substitutes for Story 3.15 or closes positive FR36 deployed-runtime parity.

**Given** the disposition envelope and canonical subject are unchanged
**When** human acceptance is collected
**Then** exactly the authenticated EventStore owner, Release owner, and Test Architect each provide a verifiable receipt binding identity, role, the recomputed subject digest, explicit `rejected-non-authorizing` outcome, successor-work boundary, and valid timestamp
**And** the platform-owned verifier checks each receipt against the packet-bound owner-role registry; self-declared roles, free-form approval, stale receipts, release authority, story approval, and this planning approval are not receipts.

**Given** all envelope checks and all three receipts pass
**When** Story 3.13 is marked done
**Then** the retained result still selects no image, authorizes no deployment or consumer migration, and leaves positive deployed-runtime parity open for Story 3.15
**And** it does not reopen Stories 1.20 or 3.12, authorize Parties 8.6 or G5, mutate v3.94.1, or create a dependency between Stories 3.13 and 3.14.

### Story 3.14: Corrective OCI Provenance Release

As an EventStore release owner,
I want a new semantic release whose package, workflow, OCI graph, and config provenance bind to one exact source SHA,
So that Story 3.15 can independently validate a deployment-grade candidate without mutating v3.94.1.

**Requirements coverage:** Primary FR22 and FR25; supports FR36; primary NFR9, NFR11, NFR16, and NFR17.

**Architecture constraints:** AD-11 and AD-12, with the release identity and authority boundaries required by AD-22. EventStore remains a thin release caller; label emission and raw-config validation are owned by the EventStore release configuration and the SHA-pinned shared Builds publisher/validator.

**Dependencies:** Completed Stories 3.6, 3.8, and 3.12 provide the manifest, workflow, and exact-platform publishing foundations. Story 3.13 may proceed independently and is not a prerequisite.

**Acceptance Criteria:**

**Given** the v3.94.1 provenance defect is the regression input
**When** focused release-contract tests inspect emitted image configs before any publisher or release-configuration correction
**Then** they reproduce the literal `https` URL-label values and absent revision defect
**And** property evaluation, a hand-authored summary, or a mocked pass cannot substitute for inspecting emitted raw config bytes.

**Given** the owning release layers are corrected
**When** either platform config is inspected
**Then** both configs contain the same exact `org.opencontainers.image.source` absolute public HTTPS EventStore repository URL, absolute public HTTPS `.url` and `.documentation` URLs, exact 40-character release-source `.revision`, and exact semantic `.version`
**And** a missing, blank, malformed, divergent, or identity-inconsistent label fails the shared raw-config release gate.

**Given** v3.94.1 remains retained failed evidence
**When** corrective publication is prepared
**Then** a genuinely new semantic version and absent external destinations are required
**And** no v3.94.1 tag, package, index, child, config, or release record is deleted, overwritten, repointed, or reclassified.

**Given** an external release is ready
**When** its exact workflow run and attempt are selected but before the first package, tag, release, or registry write
**Then** an authenticated, durable, unexpired, one-use release-owner authority is reserved to that run/attempt and binds repository, semantic version/tag, source SHA, registry/repository, exact 14-package inventory, `linux/amd64` and `linux/arm64`, publisher/workflow/validator revisions, owner, rationale, timestamp, and validity window
**And** every attempted write must match the record exactly; missing, expired, replayed, consumed, unreserved, or mismatched authority fails closed before mutation.

**Given** an authorized corrective release executes
**When** artifacts are published
**Then** exactly the 14 IDs in `tools/release-packages.json` are published at the new version and only `eventstore` is published as one OCI index with exactly one direct child for each required platform
**And** no Dockerfile, runtime behavior change, package/container inventory change, local package authority, or unrelated credential/signing/SBOM/attestation expansion is introduced.

**Given** publication or validation fails after any external write
**When** retry is considered
**Then** the partial identity remains immutable and non-authorizing with its attempted writes and causal failure recorded
**And** retry requires another new version, destination-absence proof, and newly reserved one-use authority rather than deletion, overwrite, tag movement, or replay.

**Given** the corrective publication completes
**When** independent release validation reads retained package and registry bytes
**Then** every package ID/version/SHA-256, raw index/child/config digest and size, media type, exact platform, provenance label, and bounded Production smoke is re-derived and consistent with the exact workflow run/attempt, workflow revision, Builds execution SHA, source SHA, manifest digest, release tag, authority digest, and index digest
**And** environment/emulation failure remains distinct from product failure but neither is a pass.

**Given** release evidence is encoded
**When** the canonical `ReleaseIdentity` is emitted
**Then** the single versioned `ReleaseEvidenceCodec` produces retained UTF-8 canonical bytes binding the full repository/version/source/workflow/Builds/authority/package/OCI/smoke graph and records its codec identity, schema/version, and verifier-content digest
**And** hashing reserialized data, using another codec, trusting labels alone, or copying pass flags cannot establish that identity.

**Given** the complete corrective packet is handed to Story 3.15
**When** Story 3.14 completes
**Then** it supplies immutable evidence for independent exact-lineage validation but selects no deployed identity
**And** it authorizes no deployment, consumer migration, Parties 8.6, G5, or cross-repository infrastructure removal.

### Story 3.15: Corrected Deployed Runtime Parity Closure

As an EventStore release owner,
I want the Story 3.14 release independently mapped and accepted as one exact deployed-runtime lineage,
So that operators have a positive deployment-grade identity without relying on or splicing evidence from v3.94.1.

**Requirements coverage:** Primary FR36 and NFR12; supporting NFR9, NFR11, and NFR16.

**Architecture constraints:** AD-11, AD-12, and AD-22; this evidence-only closure establishes availability and an immutable selected identity but grants no deployment or consumer-mutation authority.

**Dependencies:** Only completed Stories 1.20 and 3.14. Story 3.13's v3.94.1 rejection is historical negative evidence, not a prerequisite or a source of facts for the new lineage.

**Acceptance Criteria:**

**Given** the Story 3.14 packet and trusted workflow/archive sources are available
**When** Story 3.15 derives the candidate identity
**Then** one exact `ReleaseIdentity` binds repository, semantic version/tag, source SHA, workflow run/attempt and revision, Builds execution SHA, one-use release-authority digest, package-manifest digest, every package ID/version/SHA-256, registry/repository, OCI index/child/config digest chain, provenance labels, and smoke-evidence digest
**And** every edge is independently derived from trusted workflow facts and retained raw bytes rather than labels, hand-authored mappings, inherited assertions, or Story 3.14 pass flags.

**Given** the canonical `ReleaseIdentity` bytes are evaluated
**When** the evidence codec and verifier are checked
**Then** the packet uses the single versioned `ReleaseEvidenceCodec`, records its schema/version and verifier-content digest, and hashes the exact retained UTF-8 canonical bytes without reserialization
**And** any alternate codec, byte change, digest mismatch, ambiguous encoding, or unverified producer fails closed.

**Given** source and package parity are evaluated
**When** the release inventory is independently downloaded and hashed
**Then** exactly the 14 manifest-governed package IDs share the selected semantic version and map through the exact release workflow and authority to the one source SHA
**And** any missing, extra, mutable-only, unavailable, inconsistent, or cross-lineage package fact rejects the candidate.

**Given** container and runtime parity are evaluated
**When** raw registry objects and Production evidence are independently inspected
**Then** the selected identity is the validated OCI index digest, its raw bytes map to exactly the `linux/amd64` and `linux/arm64` children/configs, both configs carry the identical valid release-bound provenance set, and each platform passes the same bounded support-safe smoke
**And** an observed running index, child, or config maps to the selected index only through that recorded immutable chain; tag resolution, ancestry, compatibility, or an unrecorded chain member never proves identity.

**Given** any identity, evidence, authority, or runtime fact is absent, expired, mutable-only, inconsistent, checksum-invalid, or drawn from v3.94.1 or another lineage
**When** closure is evaluated
**Then** `deployed_runtime_parity` remains unavailable, no deployed identity is selected, and a support-safe blocker plus rerun trigger is recorded
**And** no partial pass or prior human approval can override the failed technical result.

**Given** all independent technical checks pass
**When** the canonical review subject is created
**Then** its exact canonical bytes contain the `ReleaseIdentity` SHA-256 digest, selected OCI index digest, release-authority digest, explicit `deployed_runtime_parity: available` outcome, and the SHA-256 digest of every retained evidence object used by the decision
**And** missing references or any transitive identity/evidence change alters the recomputed subject digest and invalidates every receipt.

**Given** the canonical subject is unchanged
**When** acceptance is collected
**Then** exactly the authenticated EventStore owner, Release owner, and Test Architect each submit a verifiable receipt recording identity, role, the packet's recomputed subject digest, explicit positive-parity outcome, and valid timestamp
**And** the platform-owned verifier validates every signature or immutable approval identity against the packet-bound owner-role registry; self-declared roles, copied booleans, free-form approval, release authority, planning approval, and unverifiable receipts fail closed.

**Given** all technical evidence and the three bound receipts pass
**When** Story 3.15 completes
**Then** `deployed_runtime_parity` is `available` and the exact validated OCI index digest is recorded as the positive deployed identity
**And** Stories 1.20 and 3.12 remain closed, v3.94.1 remains rejected, and Epic 3 may close only after this result and all other Epic 3 stories are complete.

**Given** a consumer later proposes local infrastructure removal or deployment
**When** it cites Story 3.15
**Then** this packet may be used as immutable EventStore evidence but does not itself authorize either action
**And** deployment requires its own authority, while consumer removal requires the separate authenticated Consumer-owner receipt bound to that consumer repository/commit, packet subject, capability catalog, applicable-mode matrix, and exact removal subject; Parties 8.6 and G5 remain outside this story.

### Story 3.16: Latest-Compatible Dependency And Root Submodule Refresh

As a platform maintainer,
I want the shared NuGet catalog and root-declared submodule revisions refreshed from authoritative upstream evidence,
So that current development uses the latest compatible dependency set without weakening reproducibility or overwriting in-flight work.

Requirements coverage: Primary maintenance ownership of FR19 and FR21; supporting NFR9, NFR11, and NFR12.

Architecture constraints: AD-11 through AD-13. Builds remains the sole NuGet version authority; stable, prerelease, framework-coupled, and major families move only with compatible evidence; root gitlinks use exact reachable commits; nested submodules are excluded.

Dependencies: Completed Story 3.11 supplies the audit and validation contract but remains immutable. Existing Story 3.13 through 3.15 evidence remains bound to its original identities. Current unrelated Story 1.21 work must be preserved.

Current reconciliation: The 2026-08-20 live audit evaluated 284 rows, identified 43 stable-pin and four prerelease-channel candidates across 15 audit families, and left seven source-unresolved IDs retained. All seven checked-out root submodules matched then-current upstream main; only the parent Builds and Tenants gitlinks differed, and those advances were already present in the working tree.

Acceptance Criteria:

Given Story 3.16 implementation begins
When repository and source preflight runs
Then it records the current EventStore branch, status, remotes, recent history, exact parent gitlinks, each root submodule status/revision/upstream main HEAD, configured NuGet sources, catalog revision, and unrelated modified paths
And it preserves every pre-existing change, performs no nested initialization or update, and stops before overwriting or absorbing another story’s work.

Given the Builds catalog is audited
When live NuGet V3 registration and flat-container evidence is collected
Then every evaluated package row records current version, latest listed stable and prerelease candidates, listing state, source result, family, disposition, rollback group, rationale, evidence, and removal trigger
And missing, unlisted, or unresolved results never cause a guessed version, downgrade, omitted row, or false latest claim.

Given a stable pin has a newer stable candidate
When selection is proposed
Then the latest listed stable candidate is tested as the default
And any retained older version is accepted only as the latest validated compatible version with exact incompatibility evidence, an accountable owner, and a concrete recheck trigger.

Given an intentional prerelease pin has a newer prerelease candidate
When selection is proposed
Then it advances within the intentional channel as one compatible family
And it neither falls back to an older stable version nor crosses to another channel without explicit architecture and consumer evidence.

Given a family is coupled by SDK, runtime, compiler, adapter, UI, or test-host behavior
When any member changes
Then all required family rows and non-CPM exceptions align in one rollback-safe unit and representative consumers pass
And partial family upgrades, mixed AppHost SDK/package versions, or isolated major bumps are rejected.

Given Microsoft.OpenApi 3.x, xUnit 4.x, Roslyn 5.9, Aspire 13.5, or another major/framework-coupled candidate is considered
When compatibility validation runs
Then compile-time, runtime, generated-output, discovery/execution, package-mode, and public-surface effects applicable to that family are proved and required source adaptations are included
And version recency alone cannot override the existing ASP.NET Core OpenAPI, compiler-host, test-adapter, or AppHost contracts.

Given the accepted catalog is written
When Builds governance runs
Then central-version, authoritative-catalog, exception, Dapr, live-audit schema, offline-audit, family, and consumer-authority validators pass and the checked-in audit binds the exact Builds revision and validation results
And no PackageReference version is added to EventStore or another consumer project.

Given EventStore and in-scope root consumers evaluate the accepted catalog
When Debug/source and Release/package modes restore, build, test, generate, pack, and inspect dependency graphs
Then affected AppHost, Server, Contracts, generator, Admin UI, FrontComposer, Tenants, Memories, and release-package boundaries pass with warnings as errors and NuGet audit enabled
And a source-only success, stale assets file, skipped test lane, or one representative project cannot establish compatibility.

Given root submodule revisions are refreshed
When authoritative upstream main HEADs are resolved again immediately before application
Then each of the seven root gitlinks is either already equal or advances to the exact validated reachable revision, with Builds pointing to the accepted catalog commit
And no nested submodule, unrelated gitlink, detached unpushed commit, recursive update, remote-tracking guess, or local content change is silently included.

Given package or gitlink validation fails
When rollback is required
Then only the affected package family or gitlink group is reverted to its recorded before identity and validation is rerun
And frozen release/evidence packets, unrelated working-tree changes, and other accepted groups remain untouched.

Given Story 3.16 completion is requested
When final evidence is assembled
Then it binds exact before/after catalog rows, retained exceptions, configured-source results and UTC time, Builds/EventStore/submodule SHAs, commands/results, package and gitlink rollback groups, documentation snapshots, limitations, and named Builds/EventStore maintainer approvals
And it performs or implies no NuGet publication, deployment, nested-submodule action, commit, push, merge, or rewrite of Story 3.11 or Story 3.13 through 3.15 evidence without separate authority.

<!-- Epic 3 story set includes the approved Story 3.16 maintenance follow-up. -->

## Epic 4: Operators Can Trust Command and Event Integrity

Operators can rely on stable event identity, durable idempotency admission, deterministic replay, crash recovery, and evidence-driven append behavior under concurrency and failure.

### Story 4.1: Event Identity And Duplicate Result Fidelity

As an event consumer,
I want persisted event identity and duplicate command results to be stable and complete,
So that subscribers can deduplicate reliably and retried commands receive semantically identical responses.

**Requirements coverage:** Primary FR23 and NFR7; supports NFR6.

**Architecture constraints:** AD-5 and AD-6. Aggregate-local sequence remains gapless, global position remains non-zero but may contain reservation gaps, and `AggregateActor` remains the sole durable event-mutation coordinator.

**UX coverage:** No direct UI requirement. Under AD-8, a duplicate cached command response remains command-acceptance evidence and must not be presented as projection-confirmed success.

**Dependencies:** None beyond the shipped EventStore persistence, publisher, and idempotency seams.

**Historical reconciliation:** Retained as Story 4.1 and recorded as done. The production behavior was preserved; review strengthened caller-boundary evidence. Final gates passed: focused Server 82/82, full Server 2304 passed with 25 skips, Testing 145/145, and Release build with zero warnings/errors.

**Acceptance Criteria:**

**Given** a non-empty command result reaches the production aggregate persistence path
**When** events are committed
**Then** one contiguous range is reserved through the DAPR-backed global-position actor, every event receives a unique non-zero position, and aggregate-local sequence values remain gapless and unchanged
**And** a failed aggregate commit may leave a reserved global-position gap, so strict gapless global commit order is not promised.

**Given** a persisted event is published or republished
**When** CloudEvent metadata is created
**Then** `cloudevent.id` equals the persisted envelope `MessageId` and remains stable for the same event
**And** distinct events, including events from different aggregates with equal correlation and local sequence, retain distinct IDs.

**Given** corrected idempotency state resolves a command as a duplicate
**When** the cached result is returned through the actor boundary
**Then** `Accepted`, `ErrorMessage`, `CorrelationId`, `EventCount`, `ResultPayload`, `BackpressureExceeded`, `BackpressurePendingCount`, and `BackpressureThreshold` exactly match the stored original for accepted and rejected outcomes
**And** additive compatibility defaults are retained for older records that never stored a newer field.

**Given** normal EventStore server composition is inspected
**When** allocator and actor registrations execute
**Then** `IGlobalPositionAllocator` resolves to the DAPR actor-backed implementation, `GlobalPositionActor` is registered, committed allocator state advances, and testing fakes emit non-zero positions
**And** the zero-producing no-op allocator remains only a direct-test/compatibility fallback and cannot satisfy production evidence.

**Given** Story 4.1 validation runs
**When** focused allocator, persistence, publisher, subscriber-idempotency, duplicate-result, registration, full Server, Testing, and Release-build gates execute
**Then** persisted positions, stable message identity, all eight duplicate-result fields, and production wiring pass through observable state and caller behavior
**And** no idempotency re-keying, global-position sharding, projection semantics, publication recovery, package, topology, or public-contract change is introduced.

### Story 4.2: Resume And Idempotency Integrity

As an operator,
I want command pipeline resume and idempotency checks to match the exact command being processed,
So that stale pipeline state cannot hijack another command or prevent a valid retry.

**Requirements coverage:** Primary FR27 and NFR7; supports NFR16 through persisted production-path evidence.

**Architecture constraints:** AD-5, AD-6, AD-10, and AD-12. Exact command identity is `(MessageId, normalized CausationId, CommandType)`; correlation is tenant-scoped tracing/grouping metadata, never primary command identity.

**UX coverage:** No direct UI change. Any touched operator copy identifies the value as a message/status key, reports ambiguity safely, and never turns command acceptance into projection-confirmed success.

**Dependencies:** Completed Story 4.1 stable event identity and duplicate-result fidelity; publication recovery remains owned by Story 4.4.

**Historical reconciliation:** Retained as Story 4.2 and recorded as done. Final deterministic gates included Server 2339 passed with 25 skips, Client 637/637, REST generators 124/124, Testing 150/150, Admin Server 717 passed with 18 skips, LiveSidecar 29/29, focused integration 50/50, and a zero-warning/error Release build. Two unchanged root-guidance policy failures and the nonterminating broad live-routing lane were recorded separately rather than reported as passes.

**Acceptance Criteria:**

**Given** a pipeline checkpoint exists under a reused correlation ID
**When** an incoming command is considered for resume
**Then** normalized `MessageId`, `CausationId`, and `CommandType` must all match ordinally before any persisted stage resumes
**And** a mismatched pre-commit checkpoint is safely drained/ignored, while committed or unverifiable legacy state is preserved or handed to its original message-keyed recovery path without executing under the new identity.

**Given** command state may exist for an aggregate
**When** the incoming tenant does not match the actor tenant
**Then** typed tenant validation completes before any idempotency, pipeline, pending-count, event, snapshot, metadata, status, archive, correlation-index, or drain read/write
**And** the support-safe denial performs zero state mutation and reveals no cross-tenant command existence.

**Given** the same exact command is retried
**When** its prior outcome is classified
**Then** pre-commit transient infrastructure or exhausted persistence-conflict outcomes remain retryable, while accepted, no-op, and domain-rejected outcomes return the original eight-field result
**And** stored-but-unpublished events remain non-reexecuting recovery work on Story 4.4's drain path.

**Given** a message-keyed idempotency record is found
**When** its stored causation or command type differs, its identity is incomplete, or bounded legacy lookup cannot prove the exact tuple
**Then** processing fails closed with non-retryable support-safe `command_identity_conflict`
**And** no cached result from another command is returned and no pipeline, idempotency, status, archive, drain, or event evidence is deleted or overwritten.

**Given** terminal idempotency state is retained
**When** application-level expiry is evaluated
**Then** centrally validated retention defaults to 24 hours and cannot be shorter than status/archive retention, deterministic expiry uses the configured time source, and only an exact safe identity may expire or migrate
**And** actor-state TTL caching, missing legacy expiry, or an unavailable/corrupt record never silently becomes a fresh miss.

**Given** status and archive records are written or queried
**When** their primary keys and compatibility index are evaluated
**Then** authoritative keys use `{tenant}:{messageId}` plus the existing type suffix, records remain self-describing, and a bounded tenant-scoped one-to-many correlation index uses entry expiry, ETag concurrency, and deterministic retry/overflow behavior
**And** ambiguity directs the caller to `MessageId`, index failure never invalidates message-primary lookup, and no state-store scan or cross-tenant lookup is introduced.

**Given** command identity crosses gateway, replay, generated REST, client, error, or Admin seams
**When** tracking responses and routes are produced
**Then** message/status identity remains distinct from correlation metadata, absolute gateway-owned `Location` uses the message key when configured, replay generates a new ULID-safe `MessageId`, and identity conflicts return support-safe HTTP `409` without `Retry-After`
**And** generated external APIs remain gateway delegators and correlation compatibility never selects an arbitrary message.

**Given** Story 4.2 validation runs
**When** actor, state-machine, security, idempotency, store/fake, gateway, generator, replay, Admin, and higher-tier lanes execute
**Then** tests inspect committed tenant/message-keyed state, exact resume, drain handoff, retry outcomes, correlation-index state, and absence of unauthorized reads
**And** HTTP status or mock-call evidence alone is insufficient, duplicate-result fidelity and stable event IDs remain intact, and broad environment blockers are reported without weakening the deterministic gates.

### Story 4.3: Deterministic Replay Dispatch And Serialization

As a domain maintainer,
I want event replay and projection dispatch to resolve event types deterministically,
So that rehydration cannot apply the wrong event or silently bind an empty payload.

**Requirements coverage:** Primary FR29; supports NFR6.

**Architecture constraints:** AD-5 and AD-6 plus the architecture stack rule that command, rehydrate, project, and pub/sub payload readers share one platform serialization path. Persisted and wire bytes are unchanged by this readers-only correction.

**UX coverage:** No direct UI requirement; ambiguity is surfaced as a typed, support-safe diagnostic containing type identity only, never payload content.

**Dependencies:** Stable persisted event type/identity from Story 4.1; no dependency on later recovery or OQ8 work.

**Historical reconciliation:** Retained as Story 4.3 and recorded as done. The frozen readers-only spec was implemented by `37fdcd1f` with replay ambiguity context completed by `3eb561c8`; deferred writer unification and typed-event silent-drop work remain outside this story.

**Acceptance Criteria:**

**Given** apply methods are discovered for aggregate rehydrate or projection replay
**When** the dispatch table is built
**Then** one shared resolver registers each supported event type by exact full name and compatible short name while rejecting duplicate candidate ownership deterministically
**And** generic-definition and by-reference `Apply` overloads cannot become dispatch candidates.

**Given** a stored event type name is resolved
**When** exact and compatibility matching execute
**Then** resolution order is exact full name, exact short name, then the unique longest suffix anchored by `.` or `+`, after stripping assembly qualification
**And** unordered dictionary enumeration, unanchored suffix coincidence, or a shorter competing suffix can never choose the handler.

**Given** two or more candidates survive the winning exact or anchored key
**When** resolution runs
**Then** a byte-stable `AmbiguousApplyMethodException` names the stored event type and ordinally sorted candidate type names
**And** replay converts that ambiguity to its existing categorized failure contract rather than silently choosing a method or returning an unclassified server error.

**Given** legacy short names, nested CLR names, assembly-qualified names, suffix collisions, near-misses, and equal-name candidates are exercised
**When** both rehydrate and projection paths run
**Then** unambiguous compatibility cases resolve correctly, near-misses retain each path's existing not-found behavior, and all ambiguity cases fail consistently
**And** the pre-existing boundary-valid suffix tests remain unchanged and passing.

**Given** command or event payload JSON uses camelCase or historical PascalCase property names
**When** command, rehydrate, project, or pub/sub readers deserialize it
**Then** every reader uses the immutable shared `EventStorePayloadSerialization.Options` with case-insensitive binding and populates all properties
**And** no path silently returns a default-constructed payload because of casing drift.

**Given** this readers-only change is inspected
**When** source and regression guardrails run
**Then** exactly one event-type suffix resolver remains, every covered reader explicitly uses the shared options object, and deliberate anchor/options mutations make the guardrails fail
**And** `EventPersister`, `DomainServiceWireResult`, `FakeEventPersister`, payload-protection metadata, query cursors, Admin JSON, DAPR serializer options, persisted bytes, and wire bytes are unchanged.

### Story 4.4: Committed Event Publication Recovery

As an operator,
I want the system to recover events committed but not published,
So that a crash after persistence cannot permanently strand subscriber delivery.

**Requirements coverage:** Primary FR30 and NFR7; supports NFR16.

**Architecture constraints:** AD-5, AD-6, and AD-12. Recovery preserves `AggregateActor` mutation ownership, reuses stable persisted `MessageId` values, and proves durable state rather than only calls or responses.

**UX coverage:** No direct UI implementation. Command-status responses expose additive, support-safe recovery reason and tri-state retryability so later operator surfaces can distinguish armed recovery, terminal failure, and legacy unknown state honestly.

**Dependencies:** Completed Stories 4.1 and 4.2 provide stable publication identity, exact message-keyed recovery state, and recoverable idempotency classification.

**Historical reconciliation:** Retained as Story 4.4 and recorded as done. Recovery landed through `0776785f`, `86308550`, and follow-up `4b0a7b1d`; cross-aggregate sweeping and append fencing remain outside this story.

**Acceptance Criteria:**

**Given** events and an `EventsStored` checkpoint are staged for commit
**When** the actor-owned state batch is saved
**Then** a minimal deduplicated message/correlation entry in one fixed-name unpublished-publication index becomes durable in the same batch without another round trip
**And** capacity is checked before event commit so a new range is never deliberately committed without discoverable recovery evidence.

**Given** the process crashes after event commit but before a drain record exists
**When** that aggregate actor activates
**Then** activation reads the one known index key, reconstructs the drain from the complete stored sequence range, and registers its reminder without requiring command resubmission
**And** missing or incomplete source state is pruned or diagnosed without fabricating an event range.

**Given** a drain record committed but its reminder was lost
**When** actor activation processes the index entry
**Then** it re-registers the reminder without changing the retry count
**And** registration failure leaves durable state intact for another activation.

**Given** activation encounters multiple or malformed entries
**When** recovery work is evaluated
**Then** work is bounded per activation, dead entries are pruned, the whole hook catches failures and degrades safely, and later entries cannot starve permanently
**And** activation never publishes, calls another actor, scans actor state, or performs unbounded I/O.

**Given** a drain reminder republishes the persisted range
**When** publication succeeds or repeats
**Then** the complete range is sent with each CloudEvent ID equal to the persisted event `MessageId`, allowing subscriber deduplication, and terminal cleanup removes the drain/index state and balances pending count
**And** the recoverable idempotency record transitions to `Terminal` so normal retention resumes.

**Given** drain publication fails
**When** attempts remain below the configured default bound of eight
**Then** the incremented retry state remains durable, the reminder continues, and status reports `Retryable: true` with a bounded reason
**And** environment/product failure classification and support-safe telemetry retain the message identity without payload disclosure.

**Given** the bounded attempt count is exhausted
**When** dead-letter publication succeeds
**Then** the range is durably marked dead-lettered once before drain/index/reminder removal, pending count is decremented exactly once, idempotency becomes terminal, and status reports non-retryable `drain_attempts_exhausted`
**And** if the dead-letter sink fails, the record, index, reminder, and events remain for a later attempt rather than being dropped.

**Given** older command-status records are read
**When** recovery fields are absent
**Then** trailing optional fields deserialize compatibly and `Retryable: null` means legacy unknown, never permanent failure
**And** no `CommandStatus` enum member, integer value, terminal classification, `EventPublisher` retry loop, global sweep, or global-position behavior changes.

**Given** Story 4.4 validation runs
**When** crash-window, activation, per-creation-site index, capacity, stable-ID, success, bounded-failure, exhaustion, dead-letter-failure, retention-transition, response-mapping, and mutation tests execute
**Then** each guard is proven by a named failing mutation and persisted actor end state
**And** `EventPublisher.cs` and `CommandStatus.cs` remain unchanged while Server, Contracts, Client, and Release gates pass under their normal per-project workflow.

### Story 4.5: Append Durability Race Evidence

As an architect,
I want real DAPR conflict behavior proven before changing append fencing,
So that provider-portable concurrency design is based on observed production-path semantics rather than assumptions.

**Requirements coverage:** Primary FR31 and NFR7; supports NFR16.

**Architecture constraints:** AD-5 and AD-12. Actor-only append ownership remains required, but physical write-once enforcement is explicitly unproven until a separately approved provider-portable fence passes production-path evidence.

**UX coverage:** No direct UI implementation. Any exposed conflict or blocker remains a bounded support-safe classification and never claims that an accepted actor response proves durable write-once behavior.

**Dependencies:** Completed Stories 4.1 and 4.4 provide stable event identity, production allocator behavior, and recovery seams; the live evidence lane remains outside deterministic release gates.

**Current reconciliation:** Story 4.5 remains in progress. The initial DAPR 1.18.1 `state.redis`/Redis 6 capture observed `same-key-overwrite-raw-durable-write-lost`: a raw same-sequence write was proven durable, then silently replaced by the accepted actor write without an exception or retry. The sealed packet later drifted from its bound source files, is not enforced by CI, and cannot be refreshed until the DAPR test fixture's placement/scheduler ports and actual runtime identity are reconciled. No fencing implementation is authorized by the partial evidence.

**Acceptance Criteria:**

**Given** two independently identified writers target the same aggregate event/metadata keys
**When** the live-sidecar race holds the production actor path immediately before persistence and performs the competing raw actor-state transaction
**Then** both writer identities, gate timing, raw response, allocator attempts, exceptions, intermediate durable store reads, final events, and metadata sequence are captured without relying on HTTP status alone
**And** the actor-dispatched serialization control remains separate from the unsupported second-writer race.

**Given** the captured race reaches quiescence
**When** the outcome classifier evaluates the exact retained values
**Then** the stream is classified deterministically as gapless/duplicate-free serialization, recognized writer rejection, retry, overwrite/loss, corruption, empty state, or environment/probe failure
**And** every final event must match one of the two contender identities, sequence bounds are checked before enumeration, and infrastructure failure cannot masquerade as a conflict result.

**Given** the provider supports generic-state ETags
**When** a known-current token is used, then made stale by an intervening update
**Then** the first conditional write and direct Redis readback prove the accepted value, while the stale write returns the observed DAPR conflict status/code and is not persisted
**And** that control is explicitly not treated as proof that actor-state transactions supply or enforce the same token.

**Given** the aggregate append path contains conflict catches and a configured retry budget
**When** the race evidence is reviewed
**Then** each catch/retry surface is classified as observed, not reached, not exercised, or inconclusive with exact source and runtime evidence
**And** one negative provider run cannot label a path dead for other providers or justify changing catches, retries, ETags, or metadata.

**Given** live evidence is captured
**When** its support-safe packet is sealed
**Then** exact rerun commands, provider and DAPR identities, raw captures, deterministic classifier/parser results, source hashes, redaction checks, timestamps, limitations, and a complete evidence manifest validate from current tracked bytes
**And** CI or a blocking repository test executes that validator so later source/evidence drift cannot remain green.

**Given** the current packet fails source binding or runtime attribution
**When** Story 4.5 status is evaluated
**Then** it remains in progress with the exact mismatch, live-environment blocker, consequence, and rerun trigger recorded
**And** the earlier observation remains historical evidence but cannot be presented as a currently validated completion packet.

**Given** a conforming multi-profile packet is available
**When** architecture selects add/change/defer for append fencing
**Then** the decision names supported provider profiles, the provider-portable ETag/first-write or equivalent invariant, an accountable owner, activation trigger, and separately approved implementation/evidence story
**And** Story 4.5 changes no production persistence, ETag, retry, global-position, release-workflow, or test-category behavior and grants no authority to implement the fence.

**Given** the live lane is classified in CI
**When** required infrastructure is unavailable or behavior is product-invalid
**Then** environment blockers and product/evidence failures remain distinct, the test stays outside the deterministic release gate, and neither outcome is silently skipped or called passed
**And** no later story retroactively makes this story's unsafe or incomplete result executable.

### Story 4.6: Global Position Sharding Spec Renegotiation

As a platform architect,
I want the global-position allocation strategy renegotiated and specified before sharding,
So that ordering metadata can scale without silently violating the frozen global-ordering contract.

**Requirements coverage:** Primary FR24; supports NFR6 and NFR7.

**Architecture constraints:** AD-6 and AD-13. The current allocator remains authoritative until a human-approved replacement specification defines the exact shard boundary, ordering meaning, and migration contract.

**UX coverage:** No direct UI implementation. Any future operator surface must label shard-local versus globally comparable positions accurately and must not imply ordering guarantees the approved spec does not provide.

**Dependencies:** Completed Story 4.1 establishes the current non-zero, unique, gappy global-position and stable MessageId contracts. Story 4.5 becomes a prerequisite only if the selected design also changes append fencing or provider write semantics.

**Current reconciliation:** Story 4.6 remains backlog. The existing frozen `spec-dapr-global-event-ordering.md` is completed authority for the single global allocator and cannot be edited or superseded through implementation convenience.

**Acceptance Criteria:**

**Given** the approved global-ordering spec is frozen
**When** a sharded allocator is proposed
**Then** the required human renegotiation path records the exact old clauses being retained, amended, or superseded and produces a content-bound approved successor specification
**And** no source, persisted-state, public-contract, migration, or topology change starts before that approval.

**Given** tenant-scoped, domain-scoped, and any composite/hierarchical option are compared
**When** the architecture decision is made
**Then** each option documents allocation ownership, contention reduction, position uniqueness, monotonicity boundary, gap behavior, commit-order limitations, hot-shard behavior, failure recovery, scaling characteristics, and provider dependencies
**And** the selection names measurable reasons and rejected alternatives rather than assuming that a narrower actor ID is sufficient.

**Given** positions may no longer be totally comparable across shards
**When** the public and persisted ordering contract is specified
**Then** the representation, shard identity, equality/uniqueness rules, comparison rules, cursor/checkpoint semantics, projection/rebuild behavior, diagnostics, and unsupported comparisons are explicit
**And** consumers cannot accidentally sort shard-local scalar values as a trustworthy global order.

**Given** existing events and allocator state predate sharding
**When** migration and rollout are designed
**Then** the spec defines discovery, versioning, mixed-history reads, initialization, collision avoidance, cutover/rollback, partial deployment, downgrade behavior, and evidence needed to prove no identity reuse or sequence regression
**And** historical global positions remain immutable and interpretable under a documented compatibility rule.

**Given** later implementation changes allocation
**When** its focused and production-path tests execute
**Then** per-aggregate sequence remains gapless, event `MessageId` and CloudEvent ID remain stable, and positions are non-zero, unique, and monotonic within the approved shard boundary
**And** multi-host, restart/failover, mixed-version, migration, overflow, and failure tests prove the exact approved semantics through persisted state rather than mocks alone.

**Given** Story 4.6 completion is requested
**When** the specification packet is reviewed
**Then** the selected strategy, versioned schema/API impact, consumer guidance, rollout/rollback plan, evidence matrix, accountable owners, and approvals are complete and content-bound
**And** completion authorizes only downstream planning under that spec, not implementation, deployment, or migration by itself.

### Story 4.7: Tenants Query Provenance Follow-Up

As a platform maintainer coordinating with Tenants maintainers,
I want Tenants producer-side freshness aliases removed or explicitly classified as non-projection-backed,
So that Tenants never presents an opaque ETag as projection version or authoritative current/stale evidence.

**Requirements coverage:** Supports primary FR15, NFR8, and NFR16; owns no EventStore platform prerequisite or final FR15 closure.

**Architecture constraints:** AD-14 and AD-15. Genuine lifecycle/version evidence is authoritative only for `ProjectionBacked` routes; `HandlerComputed` and `Unknown` routes cannot synthesize it from ETag.

**UX coverage:** Supports UX-DR20, UX-DR21, UX-DR25–UX-DR27, UX-DR38, and UX-DR40 by ensuring affected Tenants UI/API consumers render `Unknown`, omit unsupported lifecycle/version claims, and keep ETags opaque until genuine projection evidence exists.

**Dependencies:** Completed Story 1.2 owns EventStore route provenance enforcement, and completed Story 2.11 owns generated REST/Tenants consumption. Neither depends on this external follow-up.

**Authority and status:** Story 4.7 remains backlog. Any Tenants source, test, documentation, commit, branch, pull-request, or gitlink mutation requires separately authenticated Tenants-maintainer authority; planning approval does not grant it.

**Acceptance Criteria:**

**Given** the exact authorized Tenants baseline is inspected
**When** query producers and routes are inventoried
**Then** every place that aliases `ProjectionVersion` or lifecycle freshness from ETag, timestamp, or another non-persisted surrogate is recorded with route provenance and consumer impact
**And** the inventory is complete for generated API, typed client, UI, package/source modes, and relevant producer tests before edits begin.

**Given** an affected route has genuine projection-backed read-model evidence
**When** its producer response is corrected
**Then** `ProjectionVersion`, freshness/lifecycle state, degraded/warning state, paging evidence, and ETag flow through the canonical platform metadata contract from persisted production-path evidence
**And** ETag remains an opaque HTTP validator rather than a parsed or displayed projection version.

**Given** an affected route is handler-computed or cannot prove persisted projection provenance
**When** its route classification and metadata are emitted
**Then** it is explicitly `HandlerComputed` or `Unknown`, authoritative lifecycle/version fields are absent, and consumers render `Unknown`
**And** the producer cannot fabricate `Current`, `Stale`, `Rebuilding`, `Degraded`, or `Unavailable` from response age or ETag.

**Given** Tenants maintainer approval is unavailable or the producer fix is incomplete
**When** EventStore and existing consumers run
**Then** Story 1.2's route classification blocks fabricated lifecycle claims and Story 2.11's generated/API/UI consumers omit or render unknown evidence safely
**And** Story 4.7 remains visible without reopening or blocking those completed EventStore-owned stories.

**Given** the Tenants change is validated
**When** focused and higher-tier lanes run in each applicable source/package mode
**Then** producer metadata, gateway propagation, generated headers, typed-client mapping, and UI presentation agree with route provenance and inspect persisted read-model state for projection-backed cases
**And** compilation, mock metadata, HTTP success, or an ETag alone cannot satisfy NFR16.

**Given** Story 4.7 completion is requested
**When** authority and runtime identity are reviewed
**Then** evidence names the authenticated Tenants maintainer approval, approved PR/commit, exact Tenants SHA, accepted scope, source/package mode, command results, and production-path proof
**And** without that exact evidence the story remains backlog or review; no EventStore gitlink movement, producer-fix claim, or external completion is inferred.

### Story 4.8: Durable Admission Evidence Ledger

**Classification:** Historical planning and implementation-evidence ledger; non-executable and intentionally exempt from a user-story/BDD implementation body.

**Historical requirements coverage:** FR27, NFR7, and NFR16. Closure authority is delegated exclusively to executable Stories 4.9–4.15.

**Architecture authority:** AD-5, AD-10, AD-12, and AD-25 under the approved 2026-07-20 OQ8 proposal and Architecture + Security + Test-approved OQ8 design version 1.0.0, SHA-256 `1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8`.

**UX coverage:** None directly. Public/support-safe errors and later operator evidence are owned by the applicable child stories; this ledger renders no capability or UI state.

Story 4.8 preserves the original umbrella acceptance boundary, task history, source-candidate evidence, unresolved findings, and authority chain at `_bmad-output/implementation-artifacts/4-8-durable-tenant-scoped-idempotency-admission-and-expired-key-precedence.md`. It has no sprint execution status and must never be classified `ready-for-dev`, `in-progress`, `review`, or `done`.

**Child mapping:**

- Former Tasks 2–3 map to Story 4.9, Trusted Admission Contract And Protected Identity.
- Former Task 4 maps to Story 4.10, Digest Directory Rotation And Key Retirement.
- Former Task 5 and the non-expiry replay/reconciliation portion of Task 6 map to Story 4.11, Admission State Machine And Current-Fence Enforcement.
- The expiry/public-response portion of Task 6 and compaction/deletion/legal-hold portion of Task 7 map to Story 4.12, Expiry Compaction And Tombstone Retention.
- The legacy inventory/migration portion of Task 7 maps to Story 4.13, Legacy Admission Migration And Fail-Closed Reconciliation.
- Former Task 8 implementation evidence and packet production map to Story 4.14, OQ8 Multi-Host Production Evidence.
- Former Task 8 review/release handoff and final documentation reconciliation map to Story 4.15, OQ8 Platform Closure And Handoff.

**Ledger preservation rules:**

- Former Task 1 remains shared planning history and Task 9 documentation follows the child that owns each behavior.
- Checked boxes and candidate commit `4fd0c34ff24c26dd6435f341eebe969a09bfc929` are historical evidence only; no child inherits `done`, approval, review acceptance, release identity, or external authorization from this ledger.
- Stories 4.9–4.15 form the backward-only sequence `4.9 → 4.10 → 4.11 → 4.12 → 4.13 → 4.14 → 4.15`; only independently accepted child evidence advances the chain.
- Only Story 4.15 may close the EventStore OQ8 platform gate. Folders retains ownership of its canonical OQ8 evidence and final cross-repository closure.
- Any ledger/source-candidate conflict is resolved from the current PRD, architecture, approved OQ8 authority, and child artifact—not by reopening or executing Story 4.8.

### Story 4.9: Trusted Admission Contract And Protected Identity

As a platform security maintainer,
I want admission identity derived only from trusted canonical intent and an opaque caller key,
So that public input cannot choose execution authority or leak protected material.

**Requirements coverage:** Primary ownership of the trusted-admission and protected-identity slice of FR27, NFR7, and NFR16.

**Architecture constraints:** AD-3, AD-5, AD-10, AD-12, and AD-25 under OQ8 design digest `1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8`.

**UX coverage:** No direct UI implementation. Public validation and conflict responses remain typed and support-safe; opaque keys, canonical intent, digests, stored identity, and authorization facts are never rendered or disclosed.

**Dependencies:** Approved OQ8 design and non-executable Story 4.8 ledger; first executable story in the strict 4.9–4.15 chain.

**Historical reconciliation:** Retained as Story 4.9 and recorded as done only after focused child review. Review patches closed mutable adapter metadata, public-controller propagation, target-binding, canonical-size, eager-registration, and exact multibyte-boundary gaps; 69 focused tests and the complete Server suite passed with 2,881 passes and 25 skips.

**Acceptance Criteria:**

**Given** authentication, current operation/tenant authorization, payload validation, and canonical domain validation succeed
**When** idempotency admission begins
**Then** exactly one registered server-trusted adapter supplies a versioned canonical-intent descriptor and fixed retention class from eagerly validated immutable metadata
**And** public JSON, headers, extensions, or callers cannot select or override adapter, operation, descriptor version, canonical bytes, policy, tier, digest, partition, actor, fence, state, or expiry authority.

**Given** adapter-controlled intent is encoded
**When** operation, canonical target, semantic payload/options, policy version, delegated task scope, and behavior-affecting credential scope are serialized
**Then** encoding is versioned, length-prefixed, type-tagged, ordinal, duplicate-property rejecting, schema-normalized, and bounded both per field and as a complete byte sequence
**And** transport correlation, bearer/provider tokens, clocks, traces, delivery attempts, retry metadata, and other non-semantic values cannot change intent identity.

**Given** a caller supplies an opaque idempotency key
**When** request validation and gateway mediation run
**Then** the unchanged key reaches only the trusted admission boundary, accepts/rejects exact UTF-8 byte bounds including the 4,096-byte limit, and remains distinct from ULID-safe `MessageId`, aggregate identity, correlation, and causation
**And** no generated downstream identity or public descriptor substitutes for it.

**Given** admission identity is derived
**When** tenant, key, collision, and canonical-intent material are processed
**Then** versioned domain-separated HMAC-SHA-256 derivations produce the tenant/key partition digest, collision-verification tag, and protected intent digest using the approved key version
**And** comparisons are constant-time and temporary secret/plaintext buffers are zeroed when no longer required.

**Given** one tenant reuses a raw key across another operation, aggregate, target, delegated scope, or behavior-affecting credential scope
**When** the trusted descriptors are compared
**Then** the protected actor identity still finds the same tenant/key authority and the changed intent produces a conflict rather than fresh execution
**And** the same raw key in another managed tenant remains cryptographically and durably isolated.

**Given** protected values pass through persistence and diagnostics
**When** state, status, archives, indexes, downstream envelopes, logs, traces, metrics, exceptions, Problem Details, and evidence are scanned using real sentinel keys and intent values
**Then** raw keys and canonical intent appear nowhere outside the bounded trusted processing boundary
**And** tests injecting only precomputed digests cannot satisfy the no-leak proof.

**Given** any adapter, operation, descriptor version, policy, key material, canonical input, authorization, or configuration is unknown, invalid, unavailable, oversized, or mutable
**When** admission is requested
**Then** it fails closed before admission-state access or downstream execution with a stable support-safe outcome
**And** no key is consumed, authority inferred, or protected detail disclosed.

### Story 4.10: Digest Directory Rotation And Key Retirement

As a platform security maintainer,
I want one versioned digest directory to govern key rotation and retirement,
So that rotation cannot create multiple admission authorities or expose caller keys.

**Requirements coverage:** Primary ownership of the digest-key rotation, collision, directory, and retirement slice of FR27, NFR7, and NFR16.

**Architecture constraints:** AD-10, AD-12, and AD-25 under the approved OQ8 design. Changing digest-key version changes actor identity, so directory mediation—not per-actor serialization alone—must preserve one authority.

**UX coverage:** No direct UI implementation. Key availability, collision, promotion, and retirement failures expose only stable support-safe codes and logical version/generation metadata, never key material or protected intent.

**Dependencies:** Completed Story 4.9 trusted descriptor and protected identity boundary.

**Historical reconciliation:** Retained as Story 4.10 and recorded as done only after focused child acceptance. The original ledger's 24/24 evidence was reviewed with the versioned key ring, reader-first directory, crash-resumable promotion/redirect, dedicated collision decision, and reference-gated retirement as one unit.

**Acceptance Criteria:**

**Given** the digest-key provider starts
**When** configuration and secret material are resolved
**Then** exactly one active writer version and an ordered set of retained reader versions are validated through an injectable generation-bound key ring
**And** missing, duplicate, invalid, active-as-retired, unavailable, or unsupported versions fail readiness without exposing configuration secrets.

**Given** a trusted opaque key is presented
**When** protected aliases are derived
**Then** domain-separated HMAC-SHA-256 produces active and retained-reader tenant/key aliases with collision-verification tags, constant-time comparison, and buffer zeroing
**And** all retained reader identities are consulted through the tenant directory before any fresh active-version authority can be created.

**Given** no directory mapping exists for any safe retained alias
**When** a new admission identity is created
**Then** the directory atomically chooses one canonical active-version actor and records the aliases required for later lookup
**And** concurrent hosts cannot create independent old/new authorities for the same tenant/raw key.

**Given** an existing record must move to a new digest-key version
**When** promotion executes
**Then** the persisted, idempotent phases are prepare target, copy with exact protected state, target acknowledgement while non-executable, durable source redirect, atomic directory flip, and target activation
**And** the prior canonical actor remains authoritative until the flip, while a crash at any phase resumes or rolls back to exactly one safe authority.

**Given** active and retained key versions are served during a rolling deployment
**When** a host lacks compatible directory routing or observes incomplete promotion
**Then** readiness or admission fails closed rather than allowing split authority, a fresh miss, or execution through an unacknowledged target
**And** no version inference from process age, deployment order, or active-key preference bypasses directory state.

**Given** a partition digest matches but its verification tag does not
**When** directory or admission state is evaluated
**Then** a dedicated collision/corrupt outcome is returned before intent comparison or execution
**And** it cannot degrade to conflict, missing, migration, retry, or fresh authority creation.

**Given** digest-key retirement is requested
**When** references are inventoried
**Then** retirement is refused while the version is active or referenced by any live admission record, tombstone, directory alias, promotion, legacy-migration entry, tenant-deletion lifecycle, or legal hold
**And** it succeeds only after all authoritative references are durably absent and independently verified.

**Given** rotation/retirement evidence is reviewed
**When** current/previous versions, concurrent directory selection, interrupted promotion at every phase, collision, unavailable key, mixed-host incompatibility, refusal, later retirement, and leakage fixtures run
**Then** persisted directory/actor before-and-after state and bounded outcomes match the approved protocol
**And** raw keys, key bytes, and protected intent appear in no state, log, trace, metric, error, or evidence artifact.

### Story 4.11: Admission State Machine And Current-Fence Enforcement

As a platform operator,
I want one durable admission state machine and one current fence,
So that retries, recovery, and concurrent hosts cannot duplicate protected side effects.

**Requirements coverage:** Primary ownership of the non-expiry state-machine, exact replay/recovery, and current-fence slice of FR27, NFR7, and NFR16.

**Architecture constraints:** AD-3, AD-5, AD-10, AD-12, and AD-25. The current fence is an internal admission capability, not a claim of provider-level append fencing or physical write-once storage.

**UX coverage:** No direct UI implementation. Pending, replay, conflict, recovery, unknown, corrupt, and unavailable results use stable typed support-safe semantics so consumers can present honest retry/permanence state without protected identity details.

**Dependencies:** Completed Story 4.10 directory/rotation authority and all earlier 4.9 evidence.

**Historical reconciliation:** Retained as Story 4.11 and recorded as done under its focused frozen specification. Completion belongs to its independently reviewed state/fence evidence; no unchecked Story 4.8 task or later multi-host evidence was inherited.

**Acceptance Criteria:**

**Given** no protected admission exists
**When** the trusted coordinator first grants execution authority
**Then** actor-serialized durable state atomically reserves one stable execution identity and a positive monotonic current fence
**And** state-store failure returns unavailable with no assumed reservation or downstream work.

**Given** a signed internal execution context crosses an EventStore boundary
**When** it is validated
**Then** its tenant, domain, aggregate, command, message, correlation, digest-key version, proof, and positive fence exactly match current admission state
**And** a zero, stale, missing, forged, tampered, expired, or identity-mismatched capability fails before aggregate, domain-service, provider, repository, projection, audit, scheduling, snapshot, persistence, or commit work.

**Given** a reservation is `Reserved`, `Pending`, `Recoverable`, `UnknownProviderOutcome`, `Terminal`, or `Expired`
**When** the explicit transition matrix receives a begin, resume, reconcile, complete, or invalid request
**Then** only the approved state/outcome transitions persist and every illegal transition leaves durable state unchanged
**And** unknown schema, corrupt, unavailable, ambiguous, or contradictory state never becomes missing or fresh work.

**Given** the same live intent is presented before expiry
**When** its state is pending, terminal, or conflicting
**Then** current authorization is re-evaluated without mutation on denial, equivalent terminal state replays the exact logical result, equivalent pending state returns bounded poll/retry semantics, and different intent returns permanent conflict
**And** every non-execute disposition performs zero aggregate, advisory-store, domain, provider, repository, projection, audit, or scheduling work.

**Given** a transient interruption occurs before any protected effect
**When** recovery classification is persisted
**Then** the admission becomes `Recoverable` with its stable execution identity, exact checkpoint, and existing current fence, and a safe resume reuses them without issuing another live fence
**And** no new command identity or unbounded actor-to-actor turn is manufactured.

**Given** a failure occurs after a protected effect may have happened but the result cannot be proven
**When** the state machine classifies the outcome
**Then** it becomes `UnknownProviderOutcome` and permits read-only reconciliation only
**And** uncertainty remains bounded/retryable but can never execute the mutation again as fresh work.

**Given** execution or reconciliation reaches a deterministic result
**When** terminal completion is attempted
**Then** the current capability is revalidated immediately before finalization and the exact original logical success or deterministic failure is persisted for replay
**And** stale capability, concurrent state change, or partial evidence fails closed without overwriting the current authority.

**Given** Story 4.11 validation runs
**When** exhaustive transition-table, restart/resume, replay/conflict, recovery/unknown, stale/tampered fence, repeated-boundary, exact-result, authorization-denial, zero-downstream-work, and deliberate mutation fixtures execute
**Then** persisted actor state and caller outcomes prove every named invariant with no raw-key/canonical-intent leakage
**And** this story claims no multi-host production closure, expiry/compaction, legacy migration, provider-specific fence, or OQ8 platform availability.

### Story 4.12: Expiry Compaction And Tombstone Retention

As a platform operator,
I want expired admissions compacted to durable non-executable evidence,
So that old-key reuse never becomes fresh work as replay payloads age out.

**Requirements coverage:** Primary ownership of the expiry, compaction, tenant-lifecycle, legal-hold, and tombstone-retention slice of FR27, NFR7, and NFR16.

**Architecture constraints:** AD-10, AD-12, and AD-25. Expiry is inclusive and authoritative; the approved tombstone is metadata-minimized and deliberately contains no fence.

**UX coverage:** No UI implementation. The public contract is a stable indistinguishable non-retryable `idempotency_key_expired` response with refresh-and-new-key guidance, no `Retry-After`, and no protected intent/tier/history disclosure.

**Dependencies:** Completed Story 4.11 state-machine and current-fence enforcement.

**Historical reconciliation:** Retained as Story 4.12 and recorded as done. Verification passed 106 focused tests, 3,045 Server tests with 25 pre-existing skips, zero build warnings, a clean LiveSidecar build, and the 1/1 Redis expiry/restart proof.

**Acceptance Criteria:**

**Given** an admission reaches durable terminal finalization
**When** retention is calculated
**Then** non-commit mutation replay expires at exactly 86,400 seconds, commit replay expires at `DateTimeOffset.AddYears(7)` including leap-day behavior, and retention starts only at finalization
**And** effective time is monotonic as `max(lastObservedAt, TimeProvider.GetUtcNow())`, so clock rollback cannot extend executability or recreate fresh state.

**Given** terminal replay state approaches expiry
**When** time is one tick before, exactly at, or one tick after `expiresAt`
**Then** replay occurs only before the boundary and `now >= expiresAt` is expired
**And** a durable reminder is armed before terminal state is saved so compaction occurs across restart without waiting for key reuse.

**Given** terminal replay expires
**When** compaction commits
**Then** one atomic actor turn removes replay payload and live intent digest and replaces them with the exact AD-25 tombstone fields: schema version, expired state, tenant partition, key digest, verification tag, digest-key version, retention class, first-consumed time, replay-expired time, and monotonic last-observed time
**And** no fence, payload, intent, secret, execution checkpoint, or delete-before-replace window exists.

**Given** equivalent or different intent, operation, target, or retention tier reuses a tombstoned key
**When** admission classifies it
**Then** expiry precedence runs before semantic comparison and every request returns the same HTTP `409` `idempotency_key_expired`, `retryable: false`, `clientAction: refresh_state_then_submit_with_new_key`, with no `Retry-After`
**And** it performs zero aggregate, domain, provider, repository, projection, audit, scheduling, status, archive, or other protected work.

**Given** a signed execution or reconciliation context was issued before terminal expiry
**When** it is used after admission becomes terminal, expired, compacted, redirected, or promoted
**Then** immutable signature verification is followed by durable current-authority validation and the context is rejected before every protected boundary
**And** a valid historical signature never proves current execution authority.

**Given** a tenant is active, entering deletion, retained, held, or purge-eligible
**When** admission, legal hold, retention, or purge runs
**Then** the tenant lifecycle actor serializes active admission with deletion entry, preserves tombstones for tenant lifetime plus 400 days after approved deletion-workflow entry, pauses/resumes the remaining interval during legal hold, and acknowledges purge only after eligible evidence and aliases are removed
**And** only active tenants admit work; corruption, contradictory timestamps/hold state, or a race blocks deletion and readmission rather than producing missing.

**Given** a purger processes eligible tenants
**When** destructive actor turns execute
**Then** work is bounded, cancellable between turns, lifecycle eligibility is revalidated in the serialized turn, and key-retirement references remain until purge acknowledgement
**And** compaction, hold, deletion, and purge races can never recreate executable admission state.

**Given** Story 4.12 validation runs
**When** fixed/calendar retention, boundary ticks, clock rollback, reminder ordering, cross-tier/intention indistinguishability, currentness, lifecycle, legal-hold, corruption, purge races, restart, Redis state, and zero-work fixtures execute
**Then** retained state contains only the expected tombstone after expiry/restart and all exact boundaries pass
**And** this story makes no legacy-migration, multi-host closure, UI, or final OQ8 availability claim.

### Story 4.13: Legacy Admission Migration And Fail-Closed Reconciliation

As a platform maintainer,
I want legacy admission state migrated only when one authority is provable,
So that ambiguous history cannot become executable fresh work.

**Requirements coverage:** Primary ownership of the closed-inventory, legacy-migration, redirect, rollback, and fail-closed reconciliation slice of FR27, NFR7, and NFR16.

**Architecture constraints:** AD-5, AD-10, AD-12, and AD-25. Inventory is evidence, never execution authority; the durable legacy-source redirect is the irreversible boundary.

**UX coverage:** No UI implementation. Unknown, unsupported, ambiguous, corrupt, collision, unavailable, expired, and inconsistent migration outcomes remain stable, bounded, and support-safe without revealing raw source keys, intent, protected results, or migration internals.

**Dependencies:** Completed Story 4.12 expiry/tombstone/lifecycle behavior and the full preceding 4.9–4.11 authority chain.

**Historical reconciliation:** Retained as Story 4.13 and recorded as done. Focused coordinator/fencing/handler/inventory gates, full Server tests, zero-warning build, diff hygiene, and live DAPR/Redis migration-restart persisted-state proof were recorded under the frozen specification; no Story 4.14 multi-host closure is claimed.

**Acceptance Criteria:**

**Given** legacy admission records may exist for a tenant
**When** the versioned inventory is closed
**Then** an immutable manifest binds tenant, supported scan/schema/key versions, every known source aggregate identity, protected aliases, exact logical-result evidence, ambiguity, and migration phase
**And** absence means `NoLegacy` only after a valid closed inventory or explicit clean-install policy, never because a scan, source, or version is unavailable.

**Given** a closed entry references an exact supported self-describing legacy source
**When** migration begins
**Then** read-only inspection proves source tenant, schema, identity, result, expiry/consumption, aliases, and checkpoint before a non-executable target is prepared
**And** missing fields are never manufactured and raw source-state keys or protected payloads never enter diagnostics or evidence.

**Given** exact migration proceeds
**When** each durable phase advances
**Then** the protocol is prepare target, hash-bound target acknowledgement, payload-free non-executable source redirect, inventory/directory authority flip, target activation, and exact logical replay
**And** every checkpoint is persisted and revalidated before the next phase so only one authority is executable.

**Given** failure, restart, response loss, or digest rotation occurs during migration
**When** recovery resumes
**Then** the target and aliases remain pinned to the migration identity, each phase is idempotently reproved, and pre-redirect recovery may remove only the unactivated prepared target
**And** after the source redirect, rollback is forbidden and reconciliation proceeds forward to the already-bound target without deleting source evidence first.

**Given** a `Migrated` marker or directory target is observed
**When** completion is validated
**Then** matching source redirect, target acknowledgement, activation, inventory phase, and directory authority must all agree before replay or new work
**And** an incomplete or contradictory marker preserves evidence and fails closed rather than granting authority.

**Given** state is unclosed, uninventoried, ambiguous, cross-tenant, unknown-schema/version, malformed, colliding, unavailable, expired, or inconsistent
**When** admission or reconciliation runs
**Then** it performs only bounded read-only diagnosis or an approved already-checkpointed resume, returning the stable unsafe/collision/conflict/unavailable/expired outcome
**And** it never becomes missing, promotes speculatively, invokes protected work, or discards consumed-key knowledge.

**Given** tenant deletion, legal hold, purge, or key retirement overlaps migration
**When** the lifecycle actor evaluates the next phase
**Then** migration is serialized with active-tenant authority, inventory/alias references prevent premature purge or retirement, and later rotation follows only a proven redirect chain
**And** cross-tenant authority, dual execution, and lifecycle bypass remain impossible.

**Given** Story 4.13 validation runs
**When** every supported/rejected legacy shape, manifest corruption, cross-aggregate ambiguity, phase crash, rollback boundary, forward recovery, rotation, expiry, purge, exact replay, leakage sentinel, zero-downstream, and Redis restart fixture executes
**Then** persisted target/redirect/inventory/directory state proves one executable authority and unchanged logical replay
**And** no raw idempotency key, canonical intent, digest material, protected result payload, or raw source key appears in state, logs, errors, metrics, traces, or evidence.

### Story 4.14: OQ8 Multi-Host Production Evidence

As a test and operations owner,
I want durable admission proven across independent EventStore hosts,
So that same-process fixtures cannot masquerade as production-path idempotency evidence.

**Requirements coverage:** Primary ownership of the production-equivalent multi-host evidence and machine-readable packet slice of FR27, NFR7, and NFR16.

**Architecture constraints:** AD-5, AD-9, AD-10, AD-12, and AD-25. The approved profile is `oq8-postgresql-v1`: DAPR 1.18.x, component `statestore`, `state.postgresql` with `actorStateStore: true`, production resiliency, and at least two EventStore hosts with independent sidecars sharing one PostgreSQL backend.

**UX coverage:** No UI implementation. Captured outcomes and diagnostics are support-safe, bounded, and machine-readable; protected identifiers, inputs, results, secrets, and private paths are excluded before hashing or retention.

**Dependencies:** Completed Stories 4.9–4.13 and their immutable behavior/evidence contracts.

**Historical reconciliation:** Retained as Story 4.14 and recorded as done against source baseline `e60a3777c581d70b62f67173ccc2372b5b64a425`. The exact production matrix passed without skips; 21 deterministic methods/33 cases passed without skips; Server, Release, diff, and packet-validation gates passed. The current validator still passes locally, while the integration workflow's shallow Git checkout remains a disclosed reproducibility risk that Story 4.15 must resolve or independently disprove before closure.

**Acceptance Criteria:**

**Given** the `oq8-postgresql-v1` fixture starts
**When** topology identity is inspected
**Then** two EventStore nodes and their DAPR sidecars run as independent OS processes, the actual Sample boundary is exercised, both share the tracked PostgreSQL actor state component and resiliency policy, and process/runtime/artifact identities are captured
**And** same-process hosts, direct actor calls, mocks, or a different state component remain supporting evidence only.

**Given** equivalent and different writers race from separate hosts
**When** admission, fencing, aggregate execution, and terminal replay complete
**Then** PostgreSQL before/after state and atomic boundary counters prove one canonical identity/fence, exactly one eligible protected execution, exact logical replay for equivalents, permanent conflict for different intent, and zero work for every non-execute request
**And** observations remain valid through owner-controlled crash points, process/sidecar restart, host failover, and response loss.

**Given** recoverable or unknown outcomes are induced
**When** another node takes over
**Then** recoverable work resumes the persisted checkpoint with the current fence, unknown outcome performs read-only reconciliation or remains blocked, and terminal state replays exactly
**And** uncertainty never creates a new execution identity, fence, reservation, aggregate mutation, or external effect.

**Given** mutation/commit retention reaches one tick before, exactly at, and one tick after expiry under concurrent requests
**When** autonomous compaction and restart occur
**Then** exact tier retention, inclusive expiry, atomic live-to-minimal-tombstone state, equivalent/different expired indistinguishability, and never-missing behavior are evidenced
**And** deterministic time support is clearly labeled while durable PostgreSQL state and restart observations remain production-path proof.

**Given** digest rotation/promotion, collision, migration, deletion/legal hold, purge, or key retirement is interrupted
**When** independent hosts continue
**Then** directory, inventory, redirect, lifecycle, and reference state prove one canonical authority, safe checkpoint recovery, correct refusal, and no dual execution
**And** corrupt, unavailable, ambiguous, collision, unknown-version, or unsafe state fails closed with zero protected work.

**Given** protected sentinels and structural database snapshots are captured
**When** the evidence bundle is sanitized
**Then** only invariant-bearing structural projections, hashes, counts, commands, bounded diagnostics, environment/runtime versions, source/input/artifact identities, timestamp, limitations, and review boundary are retained
**And** sentinel scanning proves raw keys, canonical intent, protected results, credentials, concrete user/tenant IDs, and private paths are absent before manifest hashing.

**Given** `_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml` and its Story 4.14 evidence tree are validated
**When** `tools/validate-oq8-platform-evidence.py` and CI reproduction run
**Then** the exact live method, complete deterministic support set, source tree/history, profile, commands/results, hashes, matrix, limitations, and no-closure declaration agree and any placeholder, drift, omission, leakage, checksum mismatch, shallow-history gap, or unsupported claim fails closed
**And** capture is uploaded only after successful validation with sufficient Git history to reproduce all bound identities.

**Given** Story 4.14 completes
**When** the packet is handed to Story 4.15
**Then** it remains evidence-ready only and claims no release/pin, Epic 4 closure, Folders mutation, or Folders OQ8 availability
**And** publication, push, submodule movement, external identity handoff, and final review require their separate authorities.

### Story 4.15: OQ8 Platform Closure And Handoff

As an EventStore platform owner,
I want one reviewed OQ8 platform closure packet,
So that downstream consumers receive an exact, non-overstated durable-admission capability.

**Requirements coverage:** Primary ownership of the EventStore OQ8 platform-review, source-only handoff, and final platform-evidence slice of FR27, NFR7, and NFR16.

**Architecture constraints:** AD-5, AD-10, AD-12, and AD-25. EventStore platform completeness, release authority, runtime/package pinning, and Folders-owned final closure are separate decisions.

**UX coverage:** No UI implementation. The handoff and validator emit bounded support-safe failures and preserve the exact limitations operator/consumer documentation must present; unavailable authority is never rendered as capability availability.

**Dependencies:** Completed Story 4.14 packet and independently completed Stories 4.9–4.13.

**Current reconciliation:** Story 4.15 remains `review` because sprint tracking conflicts with the spec frontmatter and packet's `complete` claim. The current packet validates, contains three approvals over review subject `4c4e4674f40477fea9af6513fabe58d6590305dda0816822318596ea23ec9389`, and binds landed source `4b0a7b1d3628a857f131cfbff99030714aefc747` (tree `21f9819026a1338efbab70d69991b3570c1b54f7`). The earlier spec reference to `e5fef514…` is superseded by that later content-bound packet identity. Lifecycle must be reconciled explicitly before this story is called done; Epic 4 remains in progress regardless because Stories 4.5–4.7 are not complete.

**Acceptance Criteria:**

**Given** Stories 4.9–4.14 request platform closure
**When** the invariant crosswalk is assembled
**Then** every OQ8-1 through OQ8-8 invariant maps to the exact child story, design version/digest, command/test count, production/deterministic observation, limitation, evidence path, and SHA-256 digest
**And** historical Story 4.8 checkboxes, malformed metadata, copied summaries, or missing/rejected child evidence grant no completion.

**Given** the immutable Story 4.14 capture is consumed
**When** Story 4.15 adds closure evidence
**Then** the original seven-file capture and manifest remain byte-unchanged while a separate checksummed closure layer binds capture, crosswalk, source/artifact identity, limitations, validator, workflows, tests, public documentation, and pre-review execution
**And** pending history is preserved rather than rewritten inside the capture.

**Given** source-only capability identity is selected
**When** the landed and current repository state are verified
**Then** landed commit `4b0a7b1d3628a857f131cfbff99030714aefc747`, its exact tree, all 26 landed paths, runtime artifact hashes, and all 24 non-evolved current capability paths are independently proved with ancestry and byte equality
**And** intentionally evolved workflow/validator paths are separately content-bound; later unbound repository work cannot expand the approved source boundary.

**Given** the approved OQ8 design bytes are not tracked in EventStore
**When** the review subject is created
**Then** it preserves design version `1.0.0` and SHA-256 `1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8`, explicitly records that Folders must provide and verify the design bytes, and binds every other retained input by digest
**And** EventStore does not fabricate, recreate, or claim to have rehashed unavailable Folders authority.

**Given** architecture, security, and test review is requested
**When** receipts are validated
**Then** exactly Winston/System Architect, the Security Reviewer, and Murat/Test Architect approve the same recomputed review-subject digest and accepted-limitations digest with their named scopes and explicit source-only outcome
**And** a missing, stale, mismatched, self-declared, unbound, or non-approved receipt fails closure without exposing protected content.

**Given** the closure validator and contract suite run
**When** manifests, YAML, source, lifecycle, receipts, authority fields, public docs, dependencies, and adversarial fixtures are evaluated
**Then** exact bytes and bounded schemas pass while drift, omissions, duplicate/malformed YAML, hidden bound-path changes, dependency/bootstrap errors, leakage, placeholders, excessive input, or an overstated claim fails closed with a support-safe field/reason
**And** sufficient Git history is available to reproduce every bound commit/path proof in local and CI execution.

**Given** the packet is approved for EventStore platform completion
**When** the source-only handoff is consumed
**Then** it instructs Folders to install the pinned validator requirements, validate unchanged capability paths, independently verify its design bytes, and use only the exact landed EventStore source identity
**And** Folders retains its canonical evidence and final cross-repository decision; EventStore does not claim Folders OQ8 closure.

**Given** authority fields are inspected
**When** Story 4.15 or its handoff is cited
**Then** only `eventStorePlatformComplete: true` and `handoffMode: source-only` may be true
**And** release, package, registry, deployment, runtime-pin, consumer-migration, external-repository, final-consumer, and Folders-final-closure authority remain explicitly false unless separately granted outside this story.

**Given** Story 4.15 completion is requested
**When** the current packet, receipts, source/path identity, CI reproducibility, public docs, and lifecycle trackers are revalidated
**Then** the spec, packet, sprint tracking, and epic plan must agree on the same truthful status before `done`
**And** any unresolved contradiction or later drift keeps Story 4.15 in review without invalidating preserved historical evidence or closing unrelated Epic 4 backlog work.

<!-- Epic 4 story set confirmed complete for planning. -->

## Epic 5: Tenants and Administrators Are Protected by Fail-Closed Boundaries

Tenants and administrators receive consistent fail-closed authentication, authorization, tenant isolation, internal endpoint protection, and runtime topology enforcement.

### Story 5.1: Infrastructure Failure Cache Clear

As an operator,
I want infrastructure-failure rejection paths to clear staged actor state before committing,
So that a rejected outcome cannot accidentally flush partially staged events or metadata.

**Requirements coverage:** Primary ownership of the staged-state Phase 0 slice of FR26 and NFR7.

**Architecture constraints:** AD-5 and AD-12. `AggregateActor` remains the sole durable event-mutation coordinator and high-risk validation inspects the committed end state.

**UX coverage:** No direct UI implementation. Rejection details remain redacted and support-safe; a failed command must never be presented as having produced durable events.

**Dependencies:** None; this is a Phase 0 safe-fix gate. Later actor, Admin, or topology stories cannot substitute for its state-safety proof.

**Current reconciliation:** Story 5.1 remains backlog. The current baseline calls `ClearCacheAsync()` in infrastructure-failure and exhausted conflict paths and contains focused assertions, but completion is not inherited from Story 4.2 or source inspection; this story must verify ordering and persisted state independently.

**Acceptance Criteria:**

**Given** an aggregate command path has staged event, metadata, snapshot, pipeline, pending, or other actor-state changes
**When** rehydration, domain-service invocation, event persistence, or another pre-terminal infrastructure failure is classified
**Then** `StateManager.ClearCacheAsync()` completes before any rejection checkpoint, cleanup, idempotency/status result, dead-letter outcome, or `SaveStateAsync()` is staged or committed
**And** every production await uses `ConfigureAwait(false)`.

**Given** the cache has been cleared
**When** the support-safe rejected outcome is persisted
**Then** only the intended rejection/pipeline-cleanup state is committed and advisory status/dead-letter behavior follows its existing bounded contract
**And** no previously staged event, metadata, snapshot, publication index, drain, projection trigger, or pending-count mutation leaks into that commit.

**Given** the persistence-conflict path retries or exhausts its configured budget
**When** staged state may have come from the failed attempt
**Then** every retry and terminal-conflict transition uses the same clear-before-restage pattern as the infrastructure-failure path
**And** no earlier attempt's cached events or metadata can be committed by a later save.

**Given** cache clearing, dead-letter publication, checkpointing, cleanup, or final save fails
**When** the error is observed
**Then** the earliest causal failure and committed-state consequence are classified without exposing payload or secret data
**And** the implementation never reports that staged event state was safely discarded unless the post-failure durable inspection proves it.

**Given** focused tests inject failure only after concrete event and metadata values are staged
**When** the rejection path completes
**Then** call ordering proves clear-before-restage/save, actor state is reloaded or inspected, the original stream metadata/event keys remain unchanged, and only the permitted rejection state is observable
**And** tests that merely assert `ClearCacheAsync()` was invoked cannot satisfy acceptance.

**Given** Story 5.1 completion is requested
**When** infrastructure-stage, conflict-retry, conflict-exhaustion, clear failure, dead-letter failure, and persisted-state regression lanes run
**Then** the focused tests, full Server regression lane, and Release build pass with exact results and no warning regression
**And** no retry policy, event identity, append fencing, idempotency semantics, public contract, package, UI, or topology change is introduced.

### Story 5.2: Admin Endpoint Authorization And Tenant Filters

As a tenant administrator,
I want every Admin query and mutation to enforce authentication, role policy, tenant scope, and bounded input before application services run,
So that anonymous, cross-tenant, over-privileged, or resource-exhausting requests fail closed without disclosing protected data.

**Requirements coverage:** Primary ownership of the Admin HTTP-boundary authorization, tenant-filter, query-count, and request-size slice of FR26, NFR1, and NFR2.

**Architecture constraints:** AD-3 and AD-10. Public Admin endpoints require application-layer credentials plus current role and tenant authorization; denial precedes data access or mutation. Size and count limits are enforced at the HTTP boundary, not left to downstream services.

**UX coverage:** Restricted screens and actions show the canonical support-safe access-denied state without confirming that a hidden tenant or resource exists; focus returns to the initiating filter or action. Oversized requests show a concise validation failure without echoing raw payloads, while unavailable operations remain hidden, disabled, or explicitly `501` rather than appearing successful.

**Dependencies:** Story 5.1 establishes the preceding Phase 0 staged-state safety gate. This story does not depend on later authentication-host, internal-boundary, or topology changes and must preserve the existing Admin client contracts.

**Current reconciliation:** Story 5.2 remains backlog. The current Admin Server broadly declares role policies and tenant filters, and backup import already declares a 10 MiB request limit, but the retained unit is not complete: recent-command counts are not visibly clamped at the controller boundary, the default 1 MiB cap is not declared across the named JSON-body surfaces, and completion-grade negative/boundary evidence for the entire endpoint matrix has not been established.

**Acceptance Criteria:**

**Given** an anonymous caller requests an Admin stream, trace, command, tenant, projection, storage, backup, consistency, dead-letter, DAPR, health-detail, or type-catalog surface that is not an explicit public probe
**When** endpoint authorization executes
**Then** the request returns the configured authentication challenge without invoking an application service or disclosing tenant, resource, count, existence, payload, or diagnostic data
**And** no controller, OpenAPI convention, fallback-policy exception, or deployment setting silently makes that surface anonymous.

**Given** an authenticated caller lacks the endpoint role or requests a tenant outside their current authorized tenant set
**When** any read or mutation is attempted
**Then** authorization fails before query, command, state-store, actor, DAPR, export, import, or audit work begins
**And** global-administrator and tenant-scoped behavior remains consistent across route, query, body, and effective-tenant inputs without trusting caller-supplied administrator flags.

**Given** the Admin recent-commands query receives its count parameter
**When** the value is omitted, valid, zero, negative, or above the supported maximum
**Then** the controller applies one documented safe default and clamps or rejects every out-of-range value according to the public contract before service invocation
**And** focused tests prove default, minimum, maximum, and excessive-value behavior without allocating or retrieving an unbounded result set.

**Given** an Admin JSON-body endpoint for stream sandbox execution, projection reset or replay, consistency checking, tenant commands, dead-letter actions, storage snapshot-policy changes, backup export or admission, or crypto-shredding is available
**When** the request body is read
**Then** a default maximum of `1_048_576` bytes is enforced before deserialization or application-service invocation, the exact limit remains processable when otherwise valid, and the first larger body returns bounded HTTP `413` Problem Details
**And** representative mutation and sandbox integration tests prove no partial action, raw-body echo, exception text, or hidden resource disclosure.

**Given** `AdminBackupsController.ImportStream` receives an import body
**When** its size is exactly, below, or above `10 * 1024 * 1024` bytes
**Then** the exact and smaller valid bodies reach normal validation while an oversized body is rejected at the HTTP boundary with bounded HTTP `413` Problem Details
**And** no import service, state mutation, temporary unbounded copy, payload log, or tenant existence disclosure occurs for the rejected request.

**Given** a named endpoint has no body or its operation is unavailable in the current product boundary
**When** the request-limit matrix is reviewed
**Then** the endpoint, body shape, owner, and reason are recorded as not applicable, hidden, disabled, or `501` under the architecture contract
**And** documentation-only treatment is forbidden unless an explicit owner-approved deferred exception names the executable closure story.

**Given** the full Admin boundary matrix is exercised
**When** anonymous, insufficient-role, wrong-tenant, global-admin, valid-tenant, default-count, excessive-count, exact-limit, and oversized-body cases run
**Then** persisted and interaction evidence proves consistent fail-closed behavior across representative read, write, sandbox, and import endpoints
**And** Admin Server tests, OpenAPI authorization metadata checks, the full Admin regression lane, and Release build pass without weakening explicit anonymous health-probe behavior.

### Story 5.3: Production Authentication Guards And Secret Stripping

As a security operator,
I want production authentication to fail closed and committed configuration to contain no forgeable administrator identity,
So that development-only credentials or insecure token validation cannot leak into deployed environments.

**Requirements coverage:** Primary ownership of the production authentication and committed-secret removal slices of FR26, NFR3, and NFR4.

**Architecture constraints:** AD-10 and AD-16. Application-layer credentials are mandatory outside the three explicit health probes; if a global fallback authorization policy or default-deny convention is introduced, explicit probe anonymity lands in this same slice and the default is never weakened to restore probe reachability.

**UX coverage:** Authentication and authorization failures render the canonical support-safe denied state and never expose bearer tokens, decoded claims, signing material, credential values, authority internals, or stack traces. Anonymous probe output is status-only outside Development.

**Dependencies:** Story 5.2 establishes the Admin endpoint matrix this host-level posture protects. This story is the authentication prerequisite for Story 5.5's internal/domain-service boundary.

**Current reconciliation:** Story 5.3 remains backlog. The Admin UI base configuration is currently free of development identity values, and Admin Server Host already validates development symmetric-key versus non-development authority posture. Completion is not established: accepted JWT algorithms are not visibly pinned, `MapDefaultEndpoints()` does not attach explicit anonymous metadata to each probe, and real-pipeline evidence has not proved the fallback-policy/probe contract under Production configuration.

**Acceptance Criteria:**

**Given** every committed non-development or base configuration file, deployment template, test fixture intended for production reuse, and generated configuration artifact is inspected
**When** administrator authentication settings are enumerated
**Then** no signing key, password, username, bearer token, client secret, forgeable role/global-admin identity, decoded JWT payload, or other operational secret is committed
**And** development-only credentials remain confined to clearly named Development configuration and cannot be loaded as a production fallback.

**Given** Admin Server Host, the gateway, or another protected host starts outside Development
**When** no trusted authority is configured, symmetric-key validation is selected, HTTPS metadata is disabled where metadata retrieval applies, or required issuer/audience values are missing
**Then** validated startup fails before the host becomes ready unless a narrowly named break-glass option explicitly permits the exact insecure mode
**And** the bounded failure identifies the unsafe configuration field and remediation without printing secret values.

**Given** the break-glass option is enabled outside Development
**When** startup and authentication occur
**Then** the override is explicit, observable, auditable, and limited to symmetric-key acceptance rather than bypassing issuer, audience, lifetime, signature, role, or tenant validation
**And** documentation and tests state that it is non-production/trusted-deployment behavior rather than a conforming production posture.

**Given** production JWT bearer validation is configured
**When** token-validation parameters and authority metadata are evaluated
**Then** issuer, audience, signature, and lifetime validation are enabled, HTTPS metadata is required where applicable, clock skew is bounded, and the accepted signing algorithms are explicitly allowlisted
**And** tokens using `none`, an unexpected symmetric/asymmetric family, or another non-allowlisted algorithm fail before claims transformation or endpoint execution.

**Given** a host applies a fallback authorization policy or any default-deny endpoint convention
**When** `/health`, `/alive`, and `/ready` are mapped
**Then** each endpoint carries explicit `AllowAnonymous` metadata or a proven equivalent in the same or an earlier implementation slice
**And** the fallback policy remains the fail-closed default for every endpoint without an intentional exemption.

**Given** the fail-closed default is active on the real host pipeline in Production mode
**When** an unauthenticated client calls `/health`, `/alive`, `/ready`, and a representative protected Admin endpoint
**Then** all three probes return their actual health status while the protected endpoint challenges the caller
**And** anonymous probe bodies disclose only `Healthy`, `Degraded`, or `Unhealthy` outcome/status—not component names, dependencies, connection targets, versions, tenant data, exception detail, or Development diagnostics.

**Given** Development mode uses the documented local symmetric-key path
**When** a valid development token and each unsafe production configuration fixture are exercised
**Then** local development authentication remains functional while absent authority, unapproved symmetric mode, insecure metadata, missing issuer/audience, weak key, and non-allowlisted algorithm fixtures fail deterministically in their intended environments
**And** focused option tests, real-host authentication/probe tests, secret scans, the Admin Host regression lane, and Release build pass.

### Story 5.4: Admin Surface Safety Hygiene

As an administrator,
I want Admin tooling and documentation to avoid unsafe defaults and misleading operational guidance,
So that routine support workflows do not encourage accidental destructive action, insecure discovery, or invalid verification.

**Requirements coverage:** Primary ownership of the Swagger gate, destructive CLI confirmation, Admin correlation-identity, and test-guidance slices of FR26; supporting NFR1 and NFR4 through support-safe operator surfaces.

**Architecture constraints:** AD-10 and the platform identity convention. OpenAPI exposure is environment-safe, destructive operations require explicit human or automation intent, and EventStore message/correlation identities use ULID-safe handling where sortable envelope identity applies.

**UX coverage:** Destructive CLI commands refuse execution by default and present concise target, impact, required role, and confirmation guidance without exposing protected payloads. Non-interactive automation uses an explicit `--yes`/`--confirm` contract; a successful HTTP acceptance is described as initiated or accepted, never completed without evidence.

**Dependencies:** Stories 5.2 and 5.3 establish the authorization and host-authentication posture this surface must preserve. No later topology story may be used to waive these local safeguards.

**Current reconciliation:** Story 5.4 remains backlog. OpenAPI is configurable but currently defaults enabled when the setting is absent; destructive CLI commands such as projection reset, snapshot-policy deletion, and non-dry-run restore execute without a confirmation flag; Admin correlation middleware still accepts/generates GUIDs; and `docs/brownfield/development-guide.md` still claims `Server.Tests` does not build despite current CI guidance saying it is an unfiltered release-gate project.

**Acceptance Criteria:**

**Given** Admin OpenAPI and Swagger UI configuration is absent or uses its default
**When** Admin Server Host runs outside Development
**Then** the OpenAPI document and Swagger UI routes are not mapped unless a separately named, authenticated, operator-approved exposure option explicitly enables them
**And** Development may enable them intentionally while environment-specific real-host tests prove the Production route and asset set returns no documentation surface.

**Given** every Admin CLI command is inventoried and classified as read-only, dry-run, mutating, destructive, or unavailable
**When** a destructive operation such as projection reset, snapshot-policy deletion, backup restore/import, tenant disablement, dead-letter disposition, or an equivalent retained command is invoked without explicit `--confirm` or `--yes`
**Then** the process refuses before creating an Admin API request, prints a support-safe summary of the exact target and expected effect, and returns a documented non-success exit code
**And** an unavailable operation remains unavailable rather than using confirmation to make a stub executable.

**Given** a caller supplies the documented confirmation flag
**When** the command is otherwise valid and authorized
**Then** exactly one intended request is issued with the same typed contract as before, machine-readable output remains stable, and messaging distinguishes accepted/initiated from evidence-confirmed completion
**And** focused tests cover omitted, false, true, dry-run, unauthorized, cancelled, malformed-target, and repeated-invocation behavior for representative destructive command families.

**Given** Admin Server Host receives `X-Correlation-ID`
**When** the identifier is a canonical ULID, an accepted bounded non-whitespace correlation token under the documented compatibility rule, blank, duplicated, oversized, malformed, or GUID-only legacy input
**Then** the middleware preserves only an accepted value and otherwise generates a canonical ULID-safe replacement
**And** request items, response headers, logs, and downstream context use one bounded value without `Guid.TryParse`, header injection, ambiguity, or protected-data disclosure.

**Given** repository guidance describes test health and required validation
**When** the guidance is reconciled with tracked projects and current workflows
**Then** the stale statement that `tests/Hexalith.EventStore.Server.Tests` cannot build is removed or replaced with current evidence
**And** the deterministic Release gate, unfiltered Server.Tests lane, separate live-sidecar Integration Tests lane, prerequisites, and known limitations are described consistently without claiming a passing lane that was not executed.

**Given** Story 5.4 completion is requested
**When** Production/Development Swagger tests, the destructive-command classification and confirmation suite, correlation middleware adversarial tests, documentation link/path checks, CLI/Admin Host regressions, and Release build run
**Then** each safeguard fails closed and all executable commands and guidance agree with the same current contracts
**And** no authentication policy, tenant scope, public Admin DTO, API success semantics, or deferred-operation status is weakened.

### Story 5.5: Internal And Domain-Service Trust Boundary

As a platform security maintainer,
I want internal, domain-service, projection-notification, and admin-computation endpoints to require application-layer credentials,
So that sidecars, gateways, domain services, or external callers cannot mint trust from network position, headers, or wire flags alone.

**Requirements coverage:** Primary ownership of FR28 and the internal/domain-service/projection-notification slice of NFR1 and NFR2.

**Architecture constraints:** AD-3, AD-10, AD-16, and AD-18. DAPR ACLs and mTLS are defense in depth, not application identity; platform-owned routing headers cannot be caller-controlled; only `/health`, `/alive`, and `/ready` are explicitly anonymous and support-safe.

**UX coverage:** No new interactive screen. Denied internal or notification requests produce bounded, non-disclosing operator evidence; a rejected/forged projection callback cannot update SignalR clients, regenerate freshness evidence, or make the Admin UI present stale data as current or a command as completed.

**Dependencies:** Story 5.3 establishes production credential validation and explicit probe anonymity. Story 5.2's tenant/role behavior remains authoritative for public Admin surfaces.

**Current reconciliation:** Story 5.5 remains backlog. `DaprInternalAuthenticationHandler` currently allowlists a plaintext `dapr-caller-app-id` header and mints `global_admin` without independently proving sidecar origin; canonical domain-service routes are mapped without an application-credential requirement; `ProjectionNotificationController` has no application authorization attribute; and command/query transport contracts still carry administrator semantics into server routing.

**Acceptance Criteria:**

**Given** the DAPR internal authentication handler receives `dapr-caller-app-id`, forwarded identity headers, or another caller assertion
**When** no valid DAPR app API token, signed workload credential, mutually authenticated application identity, or approved equivalent independently proves the caller
**Then** the handler does not authenticate the request, create a system principal, or mint global-administrator, tenant, domain, or permission claims
**And** an allowlisted plaintext app ID, loopback address, DAPR port, proxy header, ACL success, or network location alone never satisfies the application-layer check.

**Given** a valid internal credential is presented
**When** its app identity, audience, expiry, signature/token, configured caller allowlist, and requested operation are evaluated
**Then** the resulting principal contains only server-derived workload identity and least-required scopes for that caller
**And** wrong app, wrong audience, expired, replayed where prohibited, duplicated, caller-injected, absent, or misconfigured credentials fail before domain, actor, query, state, or administrator work.

**Given** the Domain-Service SDK maps `/process`, `/replay-state`, `/query`, `/project`, `/project/v2`, reconciliation/rebuild descendants, and `/admin/operational-index-metadata`
**When** a request reaches any canonical or SDK-owned operational endpoint
**Then** one shared application-credential policy is required before model binding reaches domain execution or catalog disclosure
**And** SDK route-inventory tests fail if a future internal endpoint omits the policy or a host override replaces an SDK route with a weaker endpoint.

**Given** credential enforcement or a fallback/default-deny convention is enabled on a domain-service host
**When** anonymous callers reach `/health`, `/alive`, `/ready`, the root/status route if retained, and every protected route
**Then** only the three probes are explicitly anonymous and support-safe; the root is removed, protected, or returns no operational detail under an explicit contract
**And** the credential requirement is never weakened to make DAPR app-health or orchestration probes reachable.

**Given** a command, query, domain-service, or other wire envelope includes `IsGlobalAdmin`, `actor:globalAdmin`, `global_admin`, administrator-role, tenant-override, or equivalent caller-supplied assertions
**When** authorization context is created or propagated
**Then** wire assertions are ignored, removed, or treated as untrusted data and cannot grant access
**And** authorization truth is rebuilt from the currently authenticated gateway/workload principal and re-evaluated at protected boundaries without changing the stable public contract more broadly than required.

**Given** the projection-changed pub/sub endpoint receives a callback
**When** sidecar/pub-sub application identity and notification tenant/topic scope are validated
**Then** only an approved authenticated publisher for the matching tenant/topic may regenerate ETags or broadcast a bounded notification
**And** forged, external, mismatched, absent-credential, duplicated-identity, or unauthorized callbacks produce no actor call, freshness change, SignalR broadcast, resource-existence disclosure, or payload echo.

**Given** credential verification infrastructure is unavailable, malformed, stale, or ambiguously configured
**When** a protected internal request arrives
**Then** the boundary fails closed with bounded challenge/forbidden/unavailable semantics appropriate to the caller contract
**And** logs and traces retain correlation and reason classification without tokens, raw claims, protected payloads, secret values, or tenant inventories.

**Given** Story 5.5 completion is requested
**When** forged-header, valid-credential, wrong-caller, wrong-audience, expiry, duplicate-header, route-inventory, wire-admin-flag, projection-callback, probe, and infrastructure-failure suites run against representative real host pipelines
**Then** zero downstream execution and unchanged durable/freshness state are proved for every denial, while valid internal flows still work
**And** DomainService, EventStore Host, SignalR, Admin, integration, and Release gates pass without relying only on HTTP status codes or mock authentication calls.

### Story 5.6: AppHost Component Loading And Sidecar-Argument Parity

As an operator,
I want AppHost to load the intended DAPR components and per-resource sidecar options explicitly,
So that local sidecars cannot silently use generated, duplicate, unscoped, or differently addressed runtime components.

**Requirements coverage:** Primary ownership of the AppHost-loaded runtime slice of FR32, NFR2, and NFR17.

**Architecture constraints:** AD-9, AD-12, and AD-16. AppHost and DAPR YAML form one topology contract; runtime proof inspects actual annotations/arguments and loaded metadata; app-health targets the explicitly anonymous support-safe probe.

**UX coverage:** The Admin topology and health surfaces label resources from actual sidecar/component evidence. Missing, mismatched, generated-fallback, or unreachable topology renders `Unknown`, degraded, or unavailable with a bounded operator action—not a healthy/current claim based only on intended AppHost configuration.

**Dependencies:** Story 5.5 defines the internal credential boundary that this sidecar topology transports. Story 5.7 owns production YAML/ACL parity; Story 5.8 owns the combined drift gate.

**Owner / review boundary:** Winston (Architect) owns topology invariants; Amelia (Developer) reviews AppHost implementation and focused tests.

**Current reconciliation:** Story 5.6 remains backlog. AppHost resolves per-service access-control paths, an explicit isolated state-store YAML, placement/scheduler addresses, and app IDs, but it does not pass the tracked `pubsub.yaml` through `pubSubComponentPath`; the Aspire extension therefore generates a default Redis pub/sub component without the tracked scopes/metadata. Complete per-resource annotation and effective-argument parity has not been proved.

**Acceptance Criteria:**

**Given** the EventStore AppHost builds its local distributed application model
**When** the `statestore`, `pubsub`, access-control configurations, and resiliency resources are resolved
**Then** every intended tracked YAML file is resolved to one explicit canonical path, existence-validated, and passed to the owning Aspire/DAPR resource exactly once
**And** no same-named generated component, broad resources directory, copied fallback, or ambient working-directory discovery can override or duplicate it.

**Given** each EventStore, Admin Server, Admin UI, domain-service, API, sample UI, and optional test-subscriber sidecar is modeled
**When** its annotations and effective arguments are inspected
**Then** app ID, app port ownership, DAPR HTTP port where fixed, access-control config, placement and scheduler endpoints, app-health enablement/path, component references, and required resiliency behavior match the named resource contract
**And** `/alive` is used for app health only under the explicit anonymous/support-safe contract from Story 5.3.

**Given** a resource is service-invocation-only or otherwise isolated from state/pub-sub
**When** its DAPR component references and resources paths are evaluated
**Then** it loads no state-store or pub/sub component and cannot gain access through a shared/generated fallback
**And** EventStore, Admin Server, domain modules, and the optional subscriber receive only their explicitly named component set.

**Given** the local pub/sub component is loaded
**When** its effective component metadata and scopes are compared with tracked `DaprComponents/pubsub.yaml`
**Then** dead-letter posture, publishing/subscription scopes, authorized app IDs, broker metadata ownership, and dev-only subscriber treatment are preserved
**And** the AppHost does not replace that contract with an unscoped toolkit-generated Redis pub/sub definition.

**Given** a component/config path is absent, duplicated, outside the approved source tree, unreadable, or resolves to a generated fallback
**When** AppHost validation or focused model tests run
**Then** the gate fails with the exact resource, option, expected canonical path/component, and bounded reason before a misleading healthy topology is emitted
**And** deny-by-default scopes, component isolation, ACLs, or probe policy are not relaxed to make startup or tests pass.

**Given** placement/scheduler endpoints are provided in containerized-host, native/slim, absent-default, malformed, or conflicting forms
**When** the AppHost resolves and applies them
**Then** every actor-capable sidecar receives the same validated effective addresses appropriate to the selected local runtime
**And** service-invocation-only sidecars do not acquire accidental actor/component access as a side effect.

**Given** Story 5.6 completion is requested
**When** `Hexalith.EventStore.AppHost.Tests`, structured AppHost model inspection, tracked component/config path scans, and a focused local sidecar startup/metadata probe run
**Then** actual loaded component identities and effective sidecar arguments agree with the modeled contract and no duplicate/fallback component exists
**And** Production YAML closure, deployment publication, and cross-surface parity are not claimed until Stories 5.7 and 5.8 complete.

### Story 5.7: Production DAPR Component And ACL Parity

As a deployment operator,
I want production DAPR components, subscriptions, resiliency, and access-control policies to match the approved runtime posture,
So that tenant isolation and allowed operations do not change between local proof and deployment.

**Requirements coverage:** Primary ownership of the production-DAPR slice of FR32, NFR1, and NFR2; supporting ownership of NFR17's component, resiliency, app-health, and secret-free topology posture. Story 7.6 retains primary ownership of OpenBao component, secret-scope, `secretKeyRef`, and real secret-retrieval closure.

**Architecture constraints:** AD-9, AD-10, and AD-12. Production YAML and AppHost identities are one governed topology; DAPR ACLs are deny-by-default defense in depth and never replace Story 5.5's application credentials.

**UX coverage:** No new interactive workflow. Admin topology views derive deployed app IDs, component availability, scopes, and health from runtime evidence; missing or mismatched production configuration renders unknown/degraded/unavailable and never exposes connection strings, policy internals that aid attack, or hidden tenant/topic inventories.

**Dependencies:** Story 5.6 establishes the intended AppHost/component model and Story 5.5 establishes protected application endpoints. Story 5.8 will bind both to one automated drift gate.

**Owner / review boundary:** Winston (Architect) owns the production topology and ACL invariants; Amelia (Developer) reviews deployment YAML and structured validation.

**Current reconciliation:** Story 5.7 remains backlog. Production files are broadly scoped and ACLs default deny, but material parity gaps remain visible: production state-store templates omit the local `keyPrefix: none` posture; the Admin Server production ACL has no policy for the AppHost-modeled `eventstore-admin-ui` caller; production and local caller/resource inventories differ; and example subscription/pub-sub scopes and topic grants are not yet reconciled as one executable topology. OpenBao is absent by design until Story 7.6 and cannot be claimed here.

**Acceptance Criteria:**

**Given** the approved deployable workload inventory is assembled
**When** EventStore, Admin Server, Admin UI, domain services, public API adapters, operators/subscribers, and any explicitly development-only resources are classified
**Then** every production app ID, receiving sidecar, caller relationship, component use, topic role, health path, and environment exclusion has exactly one owner and rationale
**And** a local-only test subscriber, Keycloak development resource, sample, or optional source-mode service is not silently granted production access.

**Given** every production state-store component is parsed structurally
**When** metadata and scopes are compared with the approved AppHost/runtime contract
**Then** component name, type/version, `actorStateStore`, key-prefix posture, authorized app IDs, backend-required metadata, and secret-reference ownership agree for each supported provider
**And** domain services and service-invocation-only workloads retain zero actor-state access while no inline credential or connection secret is committed.

**Given** every production pub/sub component and declarative subscription is parsed structurally
**When** publishing, subscription, component, tenant/topic, route, dead-letter, and authorized-app scopes are compared
**Then** only named publishers/subscribers can perform their approved operations, each subscription's app ID can actually load the component and receive its exact topic/route, and dead-letter access is separately authorized
**And** unset environment placeholders, example tenant topics, omitted scoping entries with default-open meaning, broad wildcards, or inconsistent provider variants fail deployment validation.

**Given** each production DAPR `Configuration` is evaluated
**When** trust domain, namespace, default action, caller app ID, operation path, HTTP verb, and action are resolved for its receiving sidecar
**Then** default and per-policy behavior is deny, only the approved caller-operation matrix is allowed, and every AppHost-modeled production invocation has an exact receiving policy
**And** `/**`, multi-verb, or other broad grants require an explicit architecture-approved bounded rationale and regression tests proving denied callers and operations remain denied.

**Given** production resiliency and app-health configuration is inspected
**When** targets, policies, timeouts, retries, circuit breakers, app-health enablement/path, placement/scheduler requirements, and workload bindings are compared with the runtime model
**Then** named resources use the approved policies and `/alive` contract without creating an anonymous operational surface
**And** absent, orphaned, duplicate, misspelled, or unused policy/target entries fail rather than becoming assumed protection.

**Given** a new workload, app ID, component, provider variant, topic, subscription, route, or operation is introduced
**When** the production topology changes
**Then** all affected component scopes, publishing/subscription scopes, subscriptions, ACL policies, resiliency targets, deployment bindings, documentation, and tests change in the same retained story
**And** a broad wildcard, default-open omission, placeholder, or copied local-development grant cannot substitute for named production authorization.

**Given** Story 5.7 completion is requested
**When** structured validation parses all production DAPR YAML variants and exercises representative allowed and denied service-invocation, state-store, publish, subscribe, dead-letter, and app-health paths
**Then** effective runtime observations agree with the approved production matrix and failures identify the exact file/resource/field without leaking secrets
**And** the story claims neither AppHost parity closure before Story 5.8 nor OpenBao/secret-retrieval completion before Story 7.6.

### Story 5.8: Runtime Topology Drift Tests

As a quality maintainer,
I want exact topology drift tests over modeled, configured, and running resources,
So that stale AppHost, YAML, ACL, component, topic, or sidecar assumptions fail before release.

**Requirements coverage:** Primary ownership of the topology-verification slice of FR32, NFR2, NFR16, and NFR17.

**Architecture constraints:** AD-9 and AD-12. Static comparison and production-equivalent runtime evidence are both required; HTTP success and mocks are supporting signals, not proof of component identity, tenant isolation, or access denial.

**UX coverage:** No new screen implementation. The same canonical topology identities and evidence states used by the gate feed Admin topology/health semantics; absent or contradictory runtime evidence maps to unknown/degraded/unavailable rather than a fabricated healthy state.

**Dependencies:** Completed implementation contracts from Stories 5.6 and 5.7. Story 5.8 verifies those two slices without changing their ownership or claiming Story 7.6 OpenBao completion.

**Owner / review boundary:** Amelia (Developer) owns executable drift automation; Murat (Test Architect) reviews production-path evidence and negative-fixture completeness.

**Current reconciliation:** Story 5.8 remains backlog. AppHost model tests and structured local/production YAML tests exist, but they are separate assertions and some preserve known divergence—for example, production tests explicitly expect no Admin Server inbound policy while AppHost routes Admin UI through that DAPR app ID. The dedicated integration workflow proves other live-sidecar behavior but does not yet retain the exact AppHost/YAML/sidecar topology matrix required here.

**Acceptance Criteria:**

**Given** the AppHost distributed-application model and every supported production DAPR template set are loaded
**When** a canonical normalized topology projection is derived from each source
**Then** workload/resource names, app IDs, receiving sidecars, callers, component paths/types/versions, component references/scopes, key-prefix posture, topics, subscriptions, routes, dead-letter topics, ACL operations, resiliency targets, health paths, and placement/scheduler arguments compare as exact typed mappings
**And** documented environment-specific differences are represented as explicit bounded exceptions with owner and rationale rather than ignored fields.

**Given** the normalized projections contain a missing, extra, duplicate, conflicting, placeholder, default-open, unbound, or differently cased identity
**When** the deterministic drift suite runs
**Then** it fails with the exact source, resource, field, expected value/set, and observed value/set
**And** fixtures prove every high-risk mapping class can fail independently without relying on comments, text substring matching, or test data copied from the implementation under test.

**Given** the dedicated DAPR/Aspire integration lane starts its enumerated high-risk topology
**When** runtime evidence is collected
**Then** actual sidecar process arguments/annotations, DAPR app IDs and runtime versions, loaded component/config identities, app-health path, component metadata, and subscription inventory match the canonical projection
**And** an intended YAML file that was not loaded, a generated fallback, an unexpected component, or a wrong receiving config fails the lane even if every application process starts.

**Given** representative allowed and denied workloads exercise state, service invocation, publish, subscribe, projection callback, dead-letter, and health paths
**When** the running topology handles those requests
**Then** authorized operations reach the intended app/component/topic while unauthorized, wrong-tenant, wrong-app, wrong-verb, wrong-topic, component-isolated, and absent-credential operations perform zero protected downstream work
**And** state-store keys/end state, broker/subscription observations, callback/freshness state, and sidecar denial evidence—not only status codes—prove the result.

**Given** a backend/provider production variant is supported
**When** the parity suite evaluates it
**Then** all provider-neutral identities and security invariants are identical while only the explicitly provider-owned metadata varies
**And** an unexecuted or unavailable provider/runtime lane is reported unproven rather than passed, skipped into closure, or inferred from another provider.

**Given** evidence is retained from the runtime lane
**When** it is uploaded or reviewed
**Then** it binds source revision, AppHost/model identity, exact YAML file digests, DAPR/runtime identity, command/test results, normalized mappings, runtime observations, limitations, and timestamps
**And** paths, tokens, credentials, connection strings, raw protected payloads, and tenant inventories are sanitized before hashing and publication.

**Given** Story 5.8 completion is requested
**When** AppHost tests, structured local/production YAML comparison, adversarial drift fixtures, the production-equivalent DAPR/Aspire lane, full affected regression suites, and Release build run
**Then** all configured and observed mappings agree with no unexpected skip or warning regression
**And** documentation-only assertions, mock component calls, self-reported pass flags, or current Admin UI rendering cannot close runtime parity.

### Story 5.9: Deployment And Operator Documentation Alignment

As an operator,
I want deployment and troubleshooting guidance to name the exact topology that code and tests enforce,
So that runbooks do not direct me toward stale app IDs, components, topics, ACLs, secret paths, or health assumptions.

**Requirements coverage:** Primary ownership of the operator-documentation slice of FR32 and supporting NFR17 topology/readiness guidance.

**Architecture constraints:** AD-9, AD-10, AD-16, and AD-24. Documentation describes verified executable topology and explicit limitations; it cannot create capability, weaken deny-by-default behavior, or present a nonconforming secret/runtime profile as production-ready.

**UX coverage:** This story directly owns the operator information experience: scannable environment-specific inventories, safe copyable commands, prerequisites before mutations, expected success/denial evidence, rollback or recovery direction, and concise failure classifications without secret values or hidden tenant inventories.

**Dependencies:** Stories 5.6–5.8 must first establish and verify the runtime topology. Story 7.6 remains the executable OpenBao implementation/evidence owner; this story documents its current availability truthfully and never marks it complete by prose.

**Owner / review boundary:** Paige (Technical Writer) owns operator clarity and path/link correctness; Winston (Architect) reviews topology and security accuracy.

**Current reconciliation:** Story 5.9 remains backlog. `deploy/README.md` contains useful deployment guidance but conflicts with current architecture and modeled topology: its comparison table says every production component is scoped only to `eventstore`; it recommends Kubernetes Secrets/Azure Key Vault instead of the adopted OpenBao/DAPR secret contract; it describes ACA paths without the AD-24 nonconformance limitation; and its generated-service/sidecar examples do not consistently include the Admin UI and other approved resource relationships.

**Acceptance Criteria:**

**Given** Stories 5.6–5.8 produce the approved normalized topology and retained runtime evidence
**When** deployment and operator documentation is updated
**Then** every component/config path, resource name, app ID, caller relationship, sidecar, component scope, topic/subscription/dead-letter route, key-prefix posture, resiliency target, placement/scheduler input, health path, and ACL policy named by the docs matches the verified artifacts
**And** the source/test/evidence path that owns each non-obvious invariant is linked without copying a second contradictory authority into prose.

**Given** local development, Docker Compose, Kubernetes, Azure Container Apps, source-debug, package/publish, and optional sample/test resources differ
**When** an operator selects a profile
**Then** prerequisites, supported status, exact included resources, DAPR runtime/version posture, app ports/IDs, component/config mounting or annotations, authentication authority, and known exclusions are explicit for that profile
**And** local-only allow-by-default or development credentials, generated publisher limitations, and nonconforming production targets are visually and textually distinguished from approved production behavior.

**Given** the adopted OpenBao architecture exists while Story 7.6 is incomplete
**When** secret setup and production readiness are documented
**Then** the docs name the canonical `openbao` target, default-deny application secret scopes, bootstrap-only credential boundary, TLS requirement, and current implementation/evidence status without claiming availability
**And** direct environment connection strings, general Kubernetes Secret storage, Azure Key Vault application SDK use, or ACA managed-DAPR substitution are not recommended as AD-24-conforming production alternatives.

**Given** an operator follows component, ACL, pub/sub, subscription, resiliency, or workload-addition guidance
**When** they make a topology change
**Then** the runbook requires the complete same-slice update set from Story 5.7, names the exact validation gates from Story 5.8, and warns against unresolved placeholders, default-open omissions, broad wildcards, mutable images, and copied development grants
**And** no instruction authorizes a destructive apply, deployment, secret creation, or publication without the normal environment/change authority.

**Given** an operator validates a running deployment or investigates a failure
**When** they execute the documented checks
**Then** the runbook distinguishes process readiness, sidecar health, component load, state access, publish/subscription/dead-letter delivery, service-invocation ACL denial, application-credential denial, and Admin topology evidence
**And** expected outputs are bounded and support-safe, with redaction guidance for tokens, connection strings, paths, raw payloads, protected identifiers, and tenant/topic inventories before sharing evidence.

**Given** documentation validation runs
**When** tracked links, file paths, app/resource/component identifiers, environment variables, commands, versions, and topology-table values are checked against their structured owners
**Then** stale, missing, renamed, extra, duplicated, or contradictory references fail with the document location and expected identity
**And** generated examples or diagrams are validated or explicitly labeled illustrative rather than accepted as executable configuration.

**Given** Story 5.9 completion is requested
**When** documentation lint, link/path/identifier checks, structured topology comparison, command syntax validation, security review, and representative operator walkthroughs pass
**Then** the docs accurately describe the completed 5.6–5.8 behavior and every retained limitation
**And** no runtime, YAML, test, application, release, deployment, or Story 7.6 status change is hidden in this documentation-only child.

### Story 5.10: Reserved System Tenant Provisioning Guard

As a platform security owner,
I want user-controlled tenant provisioning to reject the reserved `system` identity,
So that managed tenants cannot collide with platform-owned scope.

**Requirements coverage:** Primary ownership of NFR2's reserved-tenant provisioning rule.

**Architecture constraints:** AD-10 and the platform identity convention. The guard applies to user-controlled managed-tenant creation before side effects; it does not rename, expose, or disable legitimate platform-owned `system` identities used for internal Tenants/global-administrator routing.

**UX coverage:** The Create Tenant dialog performs the same reserved-name check as a convenience, shows concise inline Fluent validation, associates the error with the tenant-ID input, and returns focus there. It neither submits the request nor confirms whether any `system` tenant/resource exists; server-side validation remains authoritative for every client.

**Dependencies:** Story 5.2's authenticated Admin boundary and tenant-filter contract. No topology or later Admin UI migration story may substitute for the authoritative provisioning guard.

**Owner / review boundary:** Amelia (Developer) owns the implementation; Winston (Architect) reviews the managed-versus-platform tenant identity boundary, and Murat (Test Architect) reviews zero-state/zero-downstream evidence.

**Current reconciliation:** Story 5.10 remains backlog. `system` is intentionally the platform `TenantIdentity.DefaultTenantId`, but `CreateTenantRequest`, `AdminTenantsController`, `DaprTenantCommandService`, and the Admin UI creation form currently contain no reserved-name guard; the lowercase identifier passes their visible syntax rules and is submitted as a normal `CreateTenant` command.

**Acceptance Criteria:**

**Given** a user-controlled managed-tenant identifier otherwise passes the canonical syntax contract
**When** the provisioning boundary applies the platform's single documented tenant normalization and the result equals `system`
**Then** creation is rejected with one stable support-safe validation code and field association before command construction/submission
**And** the response does not reveal whether a tenant, actor, route, platform aggregate, user, topic, or configuration with that identity exists.

**Given** Admin UI, Admin API, CLI/MCP, generated REST, or another supported user-facing adapter can initiate tenant provisioning
**When** its current entry-point inventory is reviewed
**Then** every available entry point invokes the same semantic guard before EventStore/domain submission, unavailable entry points remain unavailable, and the authoritative server boundary rejects clients that omit local validation
**And** duplicated UI-only string checks or documentation warnings cannot satisfy the story.

**Given** the reserved-name rejection occurs
**When** application interactions and durable infrastructure are inspected
**Then** no command/admission request, domain-service invocation, actor activation, tenant event/record/read model, state key, snapshot, status/archive, publication index, pub/sub topic/message, Admin audit mutation, deployment/configuration mutation, or SignalR notification is created
**And** persisted before/after evidence—not only mock call counts or an HTTP error—proves zero state and zero downstream effects.

**Given** exact `system`, every normalization-equivalent input admitted by an outer transport, invalid syntax variants, and nearby valid identifiers such as `system-1` and `systemic` are exercised
**When** canonicalization and validation run in every available provisioning adapter
**Then** every admitted reserved equivalent receives the same safe rejection, invalid syntax retains its existing validation classification, and valid nearby identifiers continue through the unchanged provisioning contract
**And** Unicode, casing, whitespace, encoding, duplicate-field, or serialization tricks cannot bypass or broaden the reserved-name comparison.

**Given** legitimate platform bootstrap and internal routing use `system` as the platform tenant partition
**When** the new guard is active
**Then** those explicitly platform-owned operations continue to use their existing trusted contracts and cannot be reached through the managed-tenant provisioning API
**And** the exception is type/operation-bound rather than a caller flag, role, route string, or general bypass that could authorize user creation.

**Given** the user enters a reserved identifier in the Create Tenant dialog
**When** they leave the field or attempt confirmation
**Then** an accessible inline error explains that the identifier cannot be used, the action remains disabled or is refused, focus returns to the tenant-ID control, and no API request is sent
**And** raw server details, internal platform purpose, hidden resource existence, or a false success toast is never displayed.

**Given** Story 5.10 completion is requested
**When** boundary unit tests, controller/adapter integration tests, UI validation/accessibility tests, valid-neighbor regressions, platform-bootstrap regressions, and persisted zero-effect production-path tests run
**Then** the reserved name fails closed across the complete available provisioning inventory while platform-owned `system` behavior remains intact
**And** all affected Admin, EventStore, Tenants consumer/source-mode where applicable, UI, integration, and Release gates pass with exact results.

<!-- Epic 5 story set confirmed complete for planning. -->

## Epic 6: Bounded Cost And Event Evolution

Platform users can operate long-lived streams with bounded snapshot and projection cost, sequence-safe projection updates, event schema versioning/upcasting, validated event metadata, and cancellation-aware processing seams.

**Delivery accounting:** Stories 6.1, 6.3, and 6.5 are architecture/readiness enablers that authorize their paired implementations but do not count as delivered runtime capability. Stories 6.2, 6.4, and 6.6 are the runtime outcomes; all three must complete before Epic 6 may claim delivered runtime value.

### Story 6.1: Folded Snapshot Frozen Spec

As a platform architect,
I want folded snapshot behavior frozen in an approved specification before implementation,
So that snapshot cost becomes bounded without silently changing recovery, compatibility, or protected-data semantics.

**Requirements coverage:** Primary ownership of FR33's folded-snapshot specification gate; supporting NFR8 bounded-cost and NFR12 compatibility planning. This enabler does not deliver either runtime outcome.

**Architecture constraints:** AD-5, AD-6, AD-12, and AD-13. `AggregateActor` remains the sole snapshot-mutation coordinator, the existing stable event stream remains replay authority, and Story 6.2 cannot start without the named approved artifact.

**UX coverage:** No direct UI implementation. The specification defines the support-safe snapshot evidence exposed to Storage & Snapshots: sequence/freshness, state availability, bounded size, protection/readability, and failure classification without raw folded state, event payloads, secrets, stack traces, or false completion claims.

**Dependencies:** Existing snapshot, payload-protection, manual reconstruction, event-read, and actor-commit contracts. No dependency on post-MVP Epic 8; current no-op/legacy protection behavior must remain valid for the MVP.

**Classification:** Architecture/readiness gate. Completion authorizes Story 6.2 to start but does not count as runtime implementation progress.

**Current reconciliation:** Story 6.1 remains backlog and `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` is absent. Current automatic snapshots can persist `DomainServiceCurrentState`, which contains prior snapshot state plus replayed events, while manual snapshot creation reconstructs folded state through `/replay-state`; no approved contract yet selects and bounds one behavior.

**Acceptance Criteria:**

**Given** current automatic and manual snapshot paths are inventoried
**When** the folded-snapshot specification is written
**Then** it identifies every producer, reader, overwrite path, key, record/envelope field, serializer, protection hook, commit boundary, failure path, operator surface, and test seam
**And** it explicitly records where `DomainServiceCurrentState`, prior snapshots, tail events, manual `/replay-state` reconstruction, and `SnapshotRecord` currently diverge.

**Given** a target automatic snapshot is defined
**When** its persisted payload contract is frozen
**Then** the snapshot contains only the aggregate's folded state at one exact sequence plus the minimal versioned envelope/protection metadata needed to validate and read it
**And** it contains no event-history collection, `DomainServiceCurrentState`, replay timeline, nested prior snapshot, command/result payload, publication state, or mutable runtime object graph.

**Given** bounded snapshot cost must be objectively testable
**When** the byte model is specified
**Then** the artifact defines the canonical serialized byte measurements for folded-state payload and full persisted snapshot, selects a numeric `MaxSnapshotEnvelopeOverheadBytes`, and states which serializer/schema/protection modes the limit covers
**And** it requires identical folded-state bytes for identical state under the same schema/serializer regardless of source event count and `snapshot size <= folded-state size + MaxSnapshotEnvelopeOverheadBytes`.

**Given** snapshot creation occurs around a command that adds events
**When** sequence and atomicity behavior is specified
**Then** the folded state's exact covered sequence, event-tail boundary, snapshot key/overwrite semantics, actor fence, state-manager staging order, `SaveStateAsync` ownership, and advisory-versus-fail-closed failures are unambiguous
**And** no snapshot may claim events that are absent, omit events at or below its sequence, commit outside the owning actor batch, or become authority for an uncommitted append.

**Given** an aggregate is rehydrated from a folded snapshot and later events
**When** replay, absent, legacy, corrupt, provider-opaque, unreadable-protected, cancelled, and infrastructure-failure paths are specified
**Then** the resulting state and sequence must equal canonical full replay for the same event prefix, with explicit safe fallback/deletion/retention behavior for each typed condition
**And** legacy snapshots remain readable or safely bypassed under a named migration policy without deleting audit-relevant protected data or presenting partial state as authoritative.

**Given** manual snapshot creation already uses aggregate reconstruction
**When** automatic folding is designed
**Then** the spec either reuses the same canonical replay/apply contract or documents one byte- and state-equivalent shared abstraction, its domain-service boundary, cancellation behavior, and error taxonomy
**And** it forbids two independently evolving fold algorithms or a manual-only correctness path that automatic snapshots bypass.

**Given** snapshot persistence crosses payload-protection, retention, erasure, backup, and operator boundaries
**When** security and lifecycle effects are reviewed
**Then** plaintext/protected storage boundaries, metadata compatibility, unreadable outcomes, retention and backup implications, current crypto-shred limits, logging/redaction, and Epic 8 non-dependency are explicit
**And** the specification neither exposes raw state nor claims physical erasure, production key custody, or post-MVP protection capability that is not implemented.

**Given** the proposed format or migration could affect public/package contracts
**When** compatibility is analyzed
**Then** additive versus breaking changes, schema/version negotiation, rolling-upgrade and mixed-history behavior, downgrade/rollback limits, legacy writers/readers, and provider portability are recorded with rejected alternatives and open decisions
**And** no unresolved format, byte-bound, replay, protection, or migration decision may be deferred into Story 6.2.

**Given** Story 6.1 completion is requested
**When** `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` is reviewed
**Then** it records the exact accepted scope, design/version or content digest, numeric overhead bound, invariants, migration posture, validation matrix, rejected alternatives, open decisions, named approver, approval date, and explicit authorization for Story 6.2
**And** missing, stale, self-declared, conditional, or scope-mismatched approval keeps Story 6.1 backlog and Story 6.2 unauthorized.

### Story 6.2: Folded Snapshot Implementation

As an operator of long-lived streams,
I want automatic and manual snapshots to persist bounded folded aggregate state,
So that snapshot storage and rehydration cost do not grow with accumulated event history.

**Requirements coverage:** Primary ownership of FR33's folded-snapshot runtime slice and NFR8's snapshot bounded-cost outcome; supporting NFR12 compatibility.

**Architecture constraints:** AD-5, AD-6, AD-12, and AD-13. Implementation conforms exactly to the approved Story 6.1 specification; `AggregateActor` retains mutation/commit ownership and canonical event replay remains the correctness oracle.

**UX coverage:** Storage & Snapshots surfaces may show support-safe folded snapshot sequence, size/bound status, age, protection/readability, and failure evidence. They do not render folded state or raw events, and accepted/manual snapshot initiation is not shown as successful until persisted readback confirms it.

**Dependencies:** Story 6.1 must be complete with a valid approval that explicitly authorizes this implementation. Current snapshot protection/readability, manual overwrite, actor fencing, and event replay contracts remain prerequisites rather than replaceable behavior.

**Current reconciliation:** Story 6.2 remains backlog and is unauthorized because `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` is absent. Existing snapshot infrastructure, atomic staging, protection hooks, and manual reconstruction are reusable foundations, but the automatic path still snapshots a history-bearing current-state object and has no approved numeric overhead bound.

**Acceptance Criteria:**

**Given** Story 6.2 implementation preflight runs
**When** the Story 6.1 artifact and approval are inspected
**Then** `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` exists, its accepted version/content identity and named approval are valid, every required design decision is closed, and it explicitly authorizes Story 6.2
**And** implementation tasks and tests cite the exact approved sections they satisfy; drift or absent/stale approval stops work rather than selecting a local design.

**Given** a stream reaches its configured automatic snapshot threshold
**When** the owning actor stages the snapshot under a current execution fence
**Then** the persisted payload is the canonical folded aggregate state at the exact sequence selected by the approved spec, using its versioned envelope, serializer, key, and protection contract
**And** no prior snapshot object, replay/event collection, timeline, `DomainServiceCurrentState`, newly uncommitted state, or unrelated pipeline/publication content is embedded.

**Given** manual and automatic snapshot creation target the same aggregate event prefix
**When** each path folds state
**Then** both use the approved shared reconstruction/folding authority and produce semantically identical folded state and sequence evidence under the same schema/serializer
**And** either path's unsupported aggregate, partial replay, sequence mismatch, malformed response, or cancellation cannot write a snapshot or report completion.

**Given** a command stages events, metadata, publication recovery, pipeline state, and an eligible snapshot
**When** persistence succeeds, conflicts/retries, loses its fence, is cancelled, or fails before commit
**Then** the snapshot participates in the exact approved actor/state-manager atomicity boundary and is committed only with the event prefix it represents
**And** retries or failure cannot leave a future-sequence snapshot, history-bearing fallback, snapshot-only commit, or stale cached snapshot write.

**Given** an aggregate has an absent, current folded, legacy history-bearing, corrupt plaintext, provider-opaque, unreadable-protected, or older-version snapshot
**When** command-time and manual rehydration execute
**Then** each condition follows the approved migration/readability policy and reconstructed state equals full canonical replay for the same readable prefix
**And** protected unreadable evidence is retained, corrupt deletion is limited to the approved safe case, and partial or unknown state never reaches domain logic as authoritative.

**Given** equivalent aggregate state is produced after at least three snapshot intervals with materially different event counts
**When** canonical persisted bytes are measured using the approved schema, serializer, and protection-mode matrix
**Then** folded-state payload bytes are identical for identical state and every full snapshot satisfies `snapshot size <= folded-state payload size + MaxSnapshotEnvelopeOverheadBytes`
**And** structural inspection proves no event-history collection or nested prior snapshot is present and reports the exact payload/envelope byte counts.

**Given** legacy writers/readers or rolling mixed versions encounter the new snapshot format
**When** compatibility, rollback, and downgrade scenarios from the approved spec run
**Then** supported combinations rehydrate correctly and unsupported combinations fail or fall back exactly as specified without corrupting the event stream or overwriting the last usable snapshot
**And** public/package compatibility and provider-portable behavior remain within the approved boundary.

**Given** Admin or operator surfaces inspect snapshot state
**When** creation, readback, stale evidence, protection failure, corruption, or infrastructure unavailability occurs
**Then** they receive bounded typed status/size/sequence evidence and only show completion after persisted readback
**And** raw folded state, raw events, secrets, provider detail, stack traces, or unsupported crypto-shred claims are never exposed.

**Given** Story 6.2 completion is requested
**When** focused fold/byte/structure tests, automatic/manual parity, actor atomicity/conflict/cancellation, legacy/protected migration, production-path persisted replay-equivalence, Admin evidence, full Server regressions, and Release build run
**Then** all approved invariants pass with exact results under warnings-as-errors and no unexpected skips
**And** projection optimization, event upcasting, payload-protection engine work, or a changed snapshot design outside the approved Story 6.1 boundary is not introduced.

### Story 6.3: Projection Delivery Cost And Sequence Guard Spec

As a platform architect,
I want projection delivery cost and source-sequence behavior frozen in an approved specification,
So that long-stream optimizations cannot introduce out-of-order state regressions or weaken proven rebuild correctness.

**Requirements coverage:** Primary ownership of FR33's projection cost-and-sequence specification gate; supporting NFR8 bounded projection cost and NFR12 compatibility planning. This enabler does not deliver either runtime outcome.

**Architecture constraints:** AD-7, AD-8, AD-12, AD-13, and AD-20. The approved duplicate, gap, page-safety, staging, promotion, lifecycle, and replay-equivalence invariants from Stories 1.18 and 1.19 remain authoritative; Story 6.4 may optimize within them but cannot redefine or weaken them.

**UX coverage:** No direct UI implementation. The specification preserves truthful Projections lifecycle and provenance states and defines support-safe head/checkpoint, lag, delivery-mode, fallback, guard-rejection, and cost evidence. Optimization must never label a projection `Current` without authoritative production-path evidence or expose event/read-model payloads, secrets, provider detail, or stack traces.

**Dependencies:** Stories 1.18 and 1.19 and their approved production-path correctness evidence. Existing projection-scoped delivery state, EventStore message identity, checkpoints, lifecycle fences, rebuild staging, and bounded page contracts are foundations to preserve rather than alternate authorities.

**Classification:** Architecture/readiness gate. Completion authorizes Story 6.4 to start but does not count as runtime implementation progress.

**Current reconciliation:** Story 6.3 remains backlog and `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md` is absent. The current implementation already persists projection-scoped checkpoints and delivery state, rejects non-contiguous rebuild pages, and protects staged promotion, but no approved artifact freezes the live-delivery short-circuit/tail cost model, handler assumptions, cross-replica source-sequence guard, or fallback boundary for the optimization.

**Acceptance Criteria:**

**Given** current live delivery, retry, reconciliation, and rebuild paths are inventoried
**When** the projection cost-and-sequence specification is written
**Then** it identifies every stream-head read, event-range read, checkpoint/delivery-state read and write, dispatch mode, handler contract, retry/fallback path, lifecycle transition, persistence boundary, metric, and test seam
**And** it distinguishes live incremental delivery, replay/reconciliation, full-replay rebuild, incremental rebuild, and legacy compatibility behavior without treating one page as a complete stream.

**Given** projection work is already at the authoritative stream head
**When** checkpoint short-circuit behavior is specified
**Then** the artifact defines the minimal metadata and persisted evidence required to prove `checkpoint == head`, the permitted zero-event outcome, and the exact read/write/handler-call budget for that path
**And** no handler, full-stream read, checkpoint advance, lifecycle completion, or `Current` claim occurs from an unvalidated, stale, unscoped, unreadable, or unavailable checkpoint.

**Given** a valid projection checkpoint lags the frozen or observed stream head by `delta`
**When** tail delivery is specified
**Then** reads begin at `checkpoint + 1`, remain ordered, contiguous, duplicate-free, page-bounded, and capped at the admitted head, with a measurable cost model expressed in metadata reads, range reads, events delivered, and durable mutations as a function of `delta` and configured page size
**And** gap, reversal, duplicate conflict, changing head, unreadable event, cancellation, or limit exhaustion has a typed fail-closed or retry/reconciliation outcome without advancing beyond proven contiguous completion.

**Given** handlers support different application semantics
**When** optimized dispatch and fallback are designed
**Then** every route declares whether it can consume a prior durable projection state plus a contiguous tail or requires canonical full replay, and the capability/version negotiation and compatibility rules are explicit
**And** an unsupported, ambiguous, legacy, or failed incremental route uses the approved safe full-replay/rebuild path rather than receiving a tail as if it were a complete stream or silently fabricating state.

**Given** duplicate, concurrent, stale, same-sequence-conflicting, or future-gap updates may arrive through multiple replicas
**When** the source-sequence guard is specified before any cost reduction
**Then** its authoritative identity is tenant, domain, aggregate, and projection type; exact duplicate identity derives from the persisted EventStore `MessageId`; and positive contiguous aggregate sequence is never treated as global ordering
**And** compare-and-set/transaction, fencing, retry, same-sequence content conflict, stale no-op, gap deferral, and newer contiguous acceptance outcomes are defined so an older attempt cannot overwrite or finalize newer projection state.

**Given** projection state, detail/index batches, completion receipts, checkpoint, freshness, and lifecycle evidence can span durable components
**When** commit and crash-recovery behavior is specified
**Then** the artifact identifies the single proof boundary that permits contiguous checkpoint advancement and `Current` status, plus readback, ambiguity reconciliation, rollback/compensation, and resume behavior for every partial-failure point
**And** cost optimization cannot expose staged state, advance a checkpoint before durable handler completion, erase bounded duplicate proof, replace the last complete live model, or turn attempted work into success evidence.

**Given** Stories 1.18 and 1.19 own production correctness
**When** checkpoint short-circuit, tail delivery, sequence guards, paging, or fallback mechanics are proposed
**Then** a preservation matrix traces every duplicate, gap, fencing, proof-horizon, page-safety, staging, promotion, lifecycle, replay-equivalence, cancellation, and protected-event invariant to the proposed design and regression evidence
**And** any design that weakens those guarantees, relies only on aggregate-wide checkpoints, sorts malformed input into validity, publishes partial-page state, or bypasses the production handler/store path is rejected.

**Given** operators and Admin surfaces need truthful cost and freshness evidence
**When** telemetry and UX-facing contracts are specified
**Then** bounded-cardinality metrics and typed reason codes cover head/checkpoint/lag, events and pages read, selected delivery mode, short-circuit, fallback, guard rejection, reconciliation, and terminal outcome with tenant-safe correlation
**And** lifecycle/provenance remains `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, `Local-only`, or `Unknown` as warranted until authoritative persisted completion proves `Current`, without logging or returning raw payloads or secrets.

**Given** format, handler, checkpoint, or writer-protocol changes may affect deployed versions
**When** compatibility is analyzed
**Then** additive versus breaking changes, schema/version negotiation, migration/cutover, mixed-writer and rolling-upgrade behavior, rollback/downgrade limits, provider portability, rejected alternatives, and open decisions are explicit
**And** no unresolved sequence authority, commit boundary, fallback, quantitative cost, correctness-preservation, or migration decision may be deferred into Story 6.4.

**Given** Story 6.3 completion is requested
**When** `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md` is reviewed
**Then** it records the exact accepted scope, design/version or content digest, quantitative cost budgets, invariants and preservation matrix, compatibility posture, validation matrix, rejected alternatives, open decisions, named approver, approval date, and explicit authorization for Story 6.4
**And** missing, stale, self-declared, conditional, or scope-mismatched approval keeps Story 6.3 backlog and Story 6.4 unauthorized.

### Story 6.4: Projection Cost And Sequence Guard Implementation

As an operator of long-lived streams,
I want current projections to short-circuit and lagging projections to process only a safe contiguous tail,
So that projection work remains bounded while duplicate, stale, and out-of-order delivery cannot regress durable state.

**Requirements coverage:** Primary ownership of FR33's projection-cost and sequence-guard runtime slice and NFR8's avoidable-replay bounded-cost outcome; supporting NFR6 duplicate/order safety, NFR7 durable mutation safety, NFR12 compatibility, and NFR16 production-path evidence.

**Architecture constraints:** AD-7, AD-8, AD-12, AD-13, and AD-20. Implementation conforms exactly to the approved Story 6.3 artifact and preserves every production correctness invariant and proof boundary established by Stories 1.18 and 1.19.

**UX coverage:** Projections surfaces may show support-safe head/checkpoint/lag, delivery mode, lifecycle, fallback, guard-rejection, and cost evidence. They enable only approved actions for the authoritative state and never show `Current` merely because an optimized attempt started or returned locally; raw events, projection state, secrets, provider detail, and stack traces remain excluded.

**Dependencies:** Story 6.3 must be complete with a valid approval explicitly authorizing this implementation, and Stories 1.18 and 1.19 must remain complete with their production-path regression evidence. The version-2 delivery-state protocol, EventStore message identity, scoped checkpoints, lifecycle fencing, and staged rebuild/promotion mechanisms remain authoritative.

**Current reconciliation:** Story 6.4 remains backlog and is unauthorized because `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md` is absent. Existing live legacy delivery still calls `GetEventsAsync(0)` before evaluating projection progress, while the named path already supplies strong scoped idempotency and sequence foundations and rebuilds already enforce bounded contiguous pages; these foundations do not by themselves deliver the approved live-tail cost model.

**Acceptance Criteria:**

**Given** Story 6.4 implementation preflight runs
**When** the Story 6.3 artifact and its prerequisites are inspected
**Then** `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md` exists, its accepted version/content identity and named approval are valid, all required decisions are closed, Stories 1.18 and 1.19 remain satisfied, and the artifact explicitly authorizes Story 6.4
**And** implementation and tests trace to exact approved sections; absent, stale, conditional, or scope-mismatched approval stops work instead of selecting an optimization or guard locally.

**Given** a projection-scoped durable checkpoint equals the authoritatively read stream head
**When** live projection delivery runs
**Then** it completes the approved metadata-only short-circuit with zero event-envelope reads, zero handler/batch calls, zero projection-state writes, and zero checkpoint advancement
**And** the outcome is recorded with the approved support-safe evidence without inventing a lifecycle transition, freshness version, activation completion, or `Current` status unsupported by persisted production-path state.

**Given** a valid checkpoint is behind the admitted stream head by `delta`
**When** an incremental-capable projection is delivered
**Then** only the ordered contiguous range from `checkpoint + 1` through the admitted head is read in approved bounded pages and supplied with the prior durable state/context required by the approved handler contract
**And** measured metadata reads, range reads, event count, page count, handler calls, and durable mutations satisfy every quantitative Story 6.3 budget as a function of `delta` and configured page size rather than total stream length.

**Given** a route is full-replay-only, legacy, capability-ambiguous, incrementally unsupported, or rejects safe incremental handling
**When** live delivery selects its mode
**Then** it follows the exact approved canonical full-replay/rebuild fallback with bounded paging and explicit reason evidence
**And** no tail or page is presented as a whole stream, no partial-page result replaces the complete live model, and fallback does not bypass the duplicate/gap guard, lifecycle fence, staging, readback, or promotion contract.

**Given** an exact duplicate, active duplicate, stale lower sequence, same-sequence identity/content conflict, reversed input, future gap, or newer contiguous event reaches any optimized production route
**When** admission and the source-sequence guard execute
**Then** exact persisted `MessageId` duplicates are coalesced or no-op, stale work cannot mutate state, conflicts fail closed, gaps remain retryable/reconcilable, malformed order is rejected without sorting, and only proven contiguous newer work can proceed
**And** guard identity remains tenant/domain/aggregate/projection-scoped, aggregate sequence is never treated as global order, and legacy aggregate-wide checkpoints cannot authorize projection-scoped mutation.

**Given** two or more replicas race to process overlapping or differently ordered tails
**When** they reserve, apply, persist, checkpoint, retry, expire, or resume work
**Then** the approved conditional-write, transaction, reservation, and fencing protocol permits one logical contiguous outcome and prevents an older token or stale state from overwriting or finalizing newer work
**And** every loser converges to duplicate, retry, reconciliation, or typed conflict without repeated logical detail/index writes, lost completion proof, checkpoint regression, or duplicate notification.

**Given** projection state, coordinated read-model batches, completion receipts, checkpoint, freshness, and lifecycle evidence are updated
**When** success, cancellation, handler/storage failure, lost response, crash, or readback ambiguity occurs at each durable boundary
**Then** checkpoint and `Current` evidence advance only after proven durable handler completion and required persisted readback, using the same stable dispatch/batch identity on safe retry
**And** incomplete work remains fenced, stale, rebuilding, degraded, unavailable, or reconcilable as approved while the last complete live model remains visible and staged/ambiguous state is never promoted silently.

**Given** checkpoints or delivery evidence are absent, corrupt, provider-opaque, unreadable, ahead of the stream, outside the retained proof horizon, or from an unsupported protocol version
**When** optimized delivery evaluates them
**Then** each case follows its approved typed fail-closed, migration, fallback, or authenticated reconciliation path and emits bounded diagnostic evidence
**And** it never assumes sequence zero or freshness, destroys audit-relevant evidence, performs an unbounded replay accidentally, or reports a successful optimization from unknown provenance.

**Given** Admin and telemetry consumers inspect optimized projection delivery
**When** short-circuit, tail, fallback, duplicate, gap, conflict, reconciliation, failure, and success paths run
**Then** the approved bounded-cardinality metrics and typed reason codes report head/checkpoint/lag, pages/events read, selected mode, guard outcome, durable terminal state, and tenant-safe correlation
**And** displayed lifecycle/provenance and enabled actions agree with authoritative persisted evidence without exposing raw event/read-model content, secrets, cross-tenant identifiers, provider internals, or stack traces.

**Given** rolling upgrade, mixed writer/handler versions, migration, rollback, and downgrade scenarios execute
**When** optimized and legacy components interact
**Then** supported combinations negotiate and preserve state correctly while unsupported combinations fail or fall back exactly as approved without corrupting projection state, checkpoint, lifecycle, or completion history
**And** public/package compatibility, existing generic gateway behavior, and provider-portable semantics remain within the Story 6.3 boundary.

**Given** Story 6.4 completion is requested
**When** current-head, long-stream small-tail, multi-page tail, full-replay fallback, exact duplicate, stale, gap, conflict, multi-replica race, cancellation/crash/resume, corrupt evidence, protected-event, lifecycle, compatibility, and rollback scenarios run through production orchestration, handlers, persisted stores, and Redis/DAPR sidecars
**Then** persisted detail, index, batch receipt, delivery state, lifecycle, freshness, retry/reconciliation work, and checkpoints equal the canonical single in-order baseline, and exact read/call/byte evidence satisfies the approved cost budgets
**And** the complete Stories 1.18/1.19 regression corpus, focused tests, Release build, and live-sidecar lanes pass with no unexpected skips, warnings, errors, or leaked processes; event upcasting and cancellation-interface work remain outside this story.

### Story 6.5: Event Versioning And Upcasting Spec

As a platform architect,
I want event contracts, upcasting, identity validation, and cancellation seams frozen in an approved specification,
So that domains can evolve persisted events safely without CLR-name coupling, ambiguous replay, or incompatible processing APIs.

**Requirements coverage:** Primary ownership of FR33's event-versioning/upcasting, event-identity validation, and published cancellation-seam specification gate; supporting NFR7 no-silent-loss, NFR12 compatibility, NFR18 reflection posture, and NFR19 protected-data safety planning. This enabler does not deliver runtime capability.

**Architecture constraints:** AD-5, AD-6, AD-7, AD-12, and AD-13. Persisted streams remain immutable replay authority, event identity and version are explicit stable contracts, deserialization is allow-listed, and Story 6.6 cannot start without the named approved artifact.

**UX coverage:** No direct UI implementation. The specification defines support-safe Type Catalog, stream, replay, and failure evidence for stable event contract type, stored/current version, legacy resolution, upcast outcome, and cancellation without rendering raw payloads, protected data, assembly-qualified CLR names as public identity, secrets, provider detail, or stack traces.

**Dependencies:** Current `IEventContract`, event metadata/envelopes, serialization and allow-list conventions, aggregate replay/apply resolution, payload-protection hooks, domain processors, query/projection dispatchers, subscription contracts, and package compatibility baseline. Epic 8's optional production protection engine is not a prerequisite; current no-op/legacy protection behavior remains supported.

**Classification:** Architecture/readiness gate. Completion authorizes Story 6.6 to start but does not count as runtime implementation progress.

**Current reconciliation:** Story 6.5 remains backlog and `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` is absent. `IEventContract.EventType` already provides a validated kebab-case domain discriminator, but persisted `EventMetadata.EventTypeName`, replay, and subscriptions still use CLR-oriented names and expose no payload schema version; no `IEventUpcaster` chain exists. Query and asynchronous named-projection seams already accept cancellation tokens, while `IDomainProcessor.ProcessAsync` and the legacy synchronous projection seam do not provide the required uniform published contract.

**Acceptance Criteria:**

**Given** persisted events traverse command results, server storage, stream reads, replay, snapshots, projections, subscriptions, Admin metadata, testing builders, and public packages
**When** the evolution specification inventories the current contract
**Then** it traces every event-type/version producer, persisted and wire field, serializer/deserializer, protection boundary, registry/allow-list, CLR apply resolver, fallback, dispatcher, handler, diagnostic surface, and compatibility adapter
**And** it identifies where stable `IEventContract.EventType`, fully qualified `EventTypeName`, domain/aggregate identity, metadata version, and absent payload version currently diverge.

**Given** a new event is created after the versioned contract is adopted
**When** its metadata is produced and persisted
**Then** the artifact defines one canonical kebab-case event contract type, a positive payload schema version, their exact field names/types/default rules, validation grammar, uniqueness scope, and relationship to metadata-envelope and domain-service versions
**And** CLR type or assembly names are implementation mappings rather than new-event public/persisted identity, while `MessageId`, aggregate sequence, and immutable stored payload remain unchanged by read-time evolution.

**Given** legacy history lacks the new event contract type or payload version
**When** it is read alongside new history
**Then** the specification defines deterministic legacy CLR-name resolution, the assumed legacy version, collision/ambiguity handling, optional metadata migration posture, mixed-history behavior, and the exact point at which legacy fallback can be retired
**And** unknown, malformed, ambiguous, or non-allow-listed type evidence fails with a typed outcome rather than arbitrary runtime type loading, best-effort guessing, silent skip, or mutation of original stored bytes.

**Given** a stored event version is older than the registered current version
**When** the upcasting pipeline is designed
**Then** a published `IEventUpcaster` contract defines its canonical event-type scope, from/to versions, payload representation, deterministic single-step transform, cancellation behavior, and registration/discovery model
**And** chain construction is contiguous, uniquely ordered, bounded by a numeric maximum hop/version limit, cached only by safe immutable identity, and rejects gaps, branches, cycles, duplicates, downgrade edges, or non-advancing results before domain code executes.

**Given** an event is read for aggregate replay, projection, subscription, manual reconstruction, or inspection
**When** version adaptation executes
**Then** the artifact fixes one shared pipeline order for metadata/identity validation, payload readability/unprotection, format validation, chained upcasting, current-type allow-listed deserialization, and domain dispatch
**And** it specifies equivalent results across every consumer, prevents double upcasting, preserves original message/sequence/correlation evidence, and never rewrites persisted payload or protection metadata as a side effect of an ordinary read.

**Given** an upcaster is missing, throws, is cancelled, returns malformed/oversized content, changes identity, emits the wrong version, or cannot process protected/provider-opaque data
**When** the pipeline handles failure
**Then** the exact typed replay/projection/subscription/command-recovery outcome, retryability, checkpoint behavior, last-known-good state behavior, telemetry, and operator evidence are specified for each case
**And** partial state is not committed or presented as authoritative, checkpoints do not pass the failed event, protected evidence is not deleted, and logs or responses do not disclose raw payloads, secrets, or stack traces.

**Given** event metadata claims tenant, domain, aggregate id, aggregate type, event contract type, and sequence identity
**When** validation boundaries are frozen
**Then** the specification defines canonical normalization and equality rules against the addressed `AggregateIdentity`, command/result context, stream key, and registered event contract before append and again before trusted replay/dispatch
**And** missing, malformed, reserved-delimiter, cross-tenant, cross-domain, aggregate-mismatched, type-mismatched, or sequence-inconsistent evidence fails closed before persistence or domain handler execution without trusting payload-supplied identity.

**Given** event types and upcasters are discovered from application assemblies
**When** registry construction and trimming/reflection posture are specified
**Then** duplicate contract keys, duplicate version edges, incompatible payload types, invalid static metadata, and nondeterministic discovery fail startup/readiness with support-safe diagnostics
**And** only explicitly registered or validated allow-listed types can be materialized; arbitrary `Type.GetType`, unbounded reflection fallback, polymorphic gadget activation, and a new AOT/trimming commitment are excluded.

**Given** processing, query, and projection work can be abandoned by the caller or host
**When** published cancellation contracts are designed
**Then** `IDomainProcessor`, query, legacy and asynchronous projection, replay/upcaster, adapters, dispatchers, transport endpoints, and owned persistence/notification seams have one explicit token-propagation matrix from request abort through domain and I/O boundaries
**And** already cancellation-aware signatures remain coherent, synchronous compatibility is handled by named additive adapters or an explicitly approved breaking-version policy, and commit/terminalization points that deliberately use non-cancellable cleanup are narrowly documented.

**Given** cancellation occurs before dispatch, between upcast steps, during domain execution, before durable commit, after commit, or during notification/cleanup
**When** semantics are specified
**Then** each boundary defines whether work stops, rolls back, safely resumes, or completes durable bookkeeping; `OperationCanceledException` remains distinguishable from domain/infrastructure failure
**And** no event is partially appended, no handler observes a partially upcast sequence, no committed result is reported as uncommitted, and retry cannot duplicate domain side effects.

**Given** the new metadata and published interfaces affect packages and deployed domain services
**When** compatibility is analyzed
**Then** additive versus breaking changes, default/constructor/serializer behavior, wire negotiation, legacy adapters, rolling upgrade, mixed reader/writer versions, downgrade/rollback limits, package baselines, and provider portability are recorded with rejected alternatives and open decisions
**And** no unresolved stable identity, version default, upcaster order/failure, protection order, cancellation signature, or migration decision may be deferred into Story 6.6.

**Given** Story 6.5 completion is requested
**When** `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` is reviewed
**Then** it records the exact accepted scope, design/version or content digest, metadata schemas, registry and chain algorithms, numeric bounds, identity-validation matrix, cancellation matrix, compatibility/migration posture, validation matrix, rejected alternatives, open decisions, named approver, approval date, and explicit authorization for Story 6.6
**And** missing, stale, self-declared, conditional, or scope-mismatched approval keeps Story 6.5 backlog and Story 6.6 unauthorized.

### Story 6.6: Event Versioning And Upcasting Implementation

As a domain author,
I want versioned event contracts to upcast deterministically through cancellation-aware platform seams,
So that old and new event history can be processed safely without CLR-name coupling, identity confusion, or partial work.

**Requirements coverage:** Primary ownership of FR33's event-versioning/upcasting, event-identity validation, and cancellation-seam runtime outcomes; supporting NFR7 no-silent-loss, NFR12 compatibility, NFR16 persisted production-path evidence, NFR18 documented reflection posture, and NFR19 protected-data safety.

**Architecture constraints:** AD-5, AD-6, AD-7, AD-12, and AD-13. Implementation conforms exactly to the approved Story 6.5 artifact, preserves immutable persisted history, uses one allow-listed evolution pipeline across consumers, and keeps actor/dispatcher durable boundaries authoritative.

**UX coverage:** Type Catalog, stream, replay, and failure surfaces may show support-safe canonical event contract type, stored/current payload version, legacy/upcast state, hop count, and cancellation/failure reason. They do not render raw or protected payloads, promote assembly-qualified CLR names as public identity, expose secrets/provider internals/stack traces, or describe failed partial replay as current state.

**Dependencies:** Story 6.5 must be complete with a valid approval explicitly authorizing this implementation. Current event persistence, protection/readability, replay/apply, projection, subscription, domain processor, query, testing, and public package contracts are migration inputs; Epic 8's optional production payload-protection engine remains out of scope.

**Current reconciliation:** Story 6.6 remains backlog and is unauthorized because `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` is absent. Validated kebab-case event contracts and several cancellation-aware internal/public seams are reusable foundations, but persisted and wire events remain CLR-name-oriented without a payload schema version, no shared upcaster pipeline exists, identity validation is not frozen across every boundary, and `IDomainProcessor` plus the legacy projection seam remain cancellation-inconsistent.

**Acceptance Criteria:**

**Given** Story 6.6 implementation preflight runs
**When** the Story 6.5 artifact and approval are inspected
**Then** `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` exists, its accepted version/content identity and named approval are valid, all required decisions are closed, and it explicitly authorizes Story 6.6
**And** implementation and tests trace to exact approved sections; absent, stale, conditional, or scope-mismatched approval stops work rather than choosing metadata, upcasting, identity, or cancellation semantics locally.

**Given** a new `IEventContract` event is returned by domain processing
**When** its wire and persisted envelopes are built
**Then** they carry the approved canonical kebab-case event contract type and positive payload schema version consistently across command result, storage, stream read, replay, projection, publication/subscription, and Admin metadata
**And** its CLR type/assembly name is only the allow-listed runtime mapping, while message id, aggregate identity, sequence, metadata version, serialization format, protection metadata, and payload bytes follow the approved immutable schema.

**Given** a legacy event lacks the new stable contract fields
**When** any supported consumer reads it
**Then** the approved legacy CLR-name mapping and assumed version resolve it deterministically through the same registry and pipeline, and mixed old/new history produces the same current domain state as its canonical equivalent
**And** ambiguous, unknown, malformed, retired, or non-allow-listed legacy evidence returns the approved typed outcome without arbitrary type loading, silent skipping, stored-history rewrite, checkpoint advancement, or partial-state publication.

**Given** a stored payload version is older than the registered current version
**When** replay, projection, subscription, reconstruction, or inspection needs the event
**Then** the registered `IEventUpcaster` chain executes each unique contiguous approved step exactly once, in ascending version order, after readability/unprotection and before current-type allow-listed deserialization/domain dispatch
**And** the resulting contract type, version, payload bounds, identity invariants, hop limit, and deterministic output are validated after every step while original persisted bytes and message/sequence/correlation evidence remain unchanged.

**Given** registry discovery encounters duplicate contract keys, conflicting/current versions, duplicate or branching edges, gaps, cycles, downgrade/non-advancing edges, incompatible payload mappings, invalid static metadata, or a chain beyond the approved numeric bound
**When** the application starts or refreshes its catalog
**Then** readiness fails deterministically with the approved bounded diagnostic before affected domain work is admitted
**And** valid registration order cannot depend on assembly enumeration, host timing, culture, dictionary order, or unbounded reflection; the documented non-AOT/trimming posture remains explicit.

**Given** an upcaster is missing, throws, is cancelled, returns malformed or oversized data, changes identity, reports the wrong version/type, or encounters unreadable protected/provider-opaque input
**When** the shared pipeline handles the failure
**Then** it returns the approved typed, retryable/non-retryable outcome consistently to replay, projection, subscription, reconstruction, and Admin diagnostics and records only support-safe telemetry
**And** no domain handler receives a partial chain, no event or snapshot is overwritten/deleted, no projection or aggregate state is committed, no checkpoint passes the event, and no raw payload, key material, secret, or stack trace is exposed.

**Given** an event enters append, storage read, replay, projection, or publication through an addressed aggregate stream
**When** metadata identity is validated
**Then** tenant, domain, aggregate id, aggregate type, canonical event contract type, message identity, and positive contiguous sequence satisfy the approved grammar and equality rules against `AggregateIdentity`, stream/address context, command result, and registered contract before trusted use
**And** cross-tenant/domain/aggregate substitution, reserved delimiters, payload-claimed authority, type mismatch, malformed identity, and sequence inconsistency fail closed before persistence or domain execution with zero downstream mutation.

**Given** the same mixed-version event prefix is consumed through aggregate replay, full and incremental projection, subscription, manual reconstruction, snapshot recovery, and test helpers
**When** each path reaches domain code
**Then** all use the same registered evolution service and yield semantically identical current event objects, aggregate/projection state, sequence evidence, and typed failures
**And** no path bypasses upcasting, applies it twice, falls back to unregistered polymorphic deserialization, or changes duplicate/message fingerprint semantics because its payload was adapted in memory.

**Given** published processing, query, and projection APIs are updated under the approved compatibility plan
**When** callers use `IDomainProcessor`, query handlers, legacy or asynchronous projection handlers, replay/upcasters, and their adapters
**Then** the approved signatures expose and propagate the originating cancellation token through dispatch, domain execution, upcasting, reads/writes, and notifications until the documented durable boundary
**And** supported legacy callers retain source/binary/wire behavior exactly as approved, default-token adapters are explicit, and no adapter silently replaces an available caller token with `CancellationToken.None` before the terminalization boundary.

**Given** cancellation occurs before dispatch, between upcast steps, during domain processing/query/projection work, before durable commit, after commit, or during required cleanup
**When** the production pipeline observes it
**Then** pre-commit work stops without append/state/checkpoint mutation, post-commit work preserves committed truth and completes only the approved non-cancellable bookkeeping, and `OperationCanceledException` remains distinct from domain rejection or infrastructure failure
**And** retry/resume uses stable identities and cannot duplicate events, handler effects, read-model batches, completion markers, projection notifications, or upcast application.

**Given** rolling upgrades mix legacy and version-aware writers, readers, handlers, and packages
**When** supported, unsupported, rollback, and downgrade matrices run
**Then** supported combinations negotiate or adapt exactly as approved and preserve canonical state, while unsupported combinations fail closed before incompatible writes or reads
**And** no downgraded component overwrites newer metadata, strips version evidence, emits CLR identity for new events, corrupts protected history, or violates existing generic gateway/package compatibility.

**Given** operators inspect event evolution through Type Catalog, streams, replay, or diagnostics
**When** current, legacy-resolved, upcast, unknown, malformed, protected-unreadable, or cancelled cases occur
**Then** the surfaces show only approved stable contract/version, bounded hop/outcome, location/sequence, and typed support evidence with available actions matching authoritative state
**And** payloads, protected bytes, CLR assembly details, secrets, cross-tenant identifiers, provider internals, and stack traces remain redacted or absent.

**Given** Story 6.6 completion is requested
**When** new-event persistence, mixed legacy/current replay, every upcast-chain topology, registry startup failure, metadata substitution, protected readability, all consumer-path equivalence, cancellation at every boundary, rolling upgrade/rollback, public API compatibility, and Admin evidence tests run through production serializers, actors, dispatchers, handlers, state stores, pub/sub, and DAPR/Redis sidecars
**Then** persisted stream bytes remain immutable, reconstructed aggregate/projection/end state equals the canonical current-version baseline, failures produce zero forbidden downstream mutation, and exact token/registry/upcast evidence satisfies the approved matrices
**And** focused unit/contract/integration tests, full affected-project regressions, warnings-as-errors Release build, package/API compatibility checks, and live-sidecar lanes pass with no unexpected skips, warnings, errors, or leaked processes; folded-snapshot, projection-cost, and optional protection-engine redesign remain outside this story.

<!-- Epic 6 story set confirmed complete for planning. -->

## Epic 7: Operators Can Diagnose, Recover, and Administer Honestly

Operators can inspect delivery and projection evidence, recover poison events, use an accessible consolidated Admin UI, retrieve production secrets safely, and distinguish implemented, unavailable, accepted, and confirmed operations.

**Independent delivery tracks:**

- **7A Delivery semantics:** Story 7.1.
- **7B Admin trust and UX:** Stories 7.2–7.5, 7.14, 7.19, and 7.20.
- **7C Production operations:** Stories 7.6–7.9.
- **7D Test evidence:** Stories 7.10–7.13.
- **7E Planning backlog:** Stories 7.15–7.18.

Each track is independently schedulable and closable. No cross-track dependency exists unless a story states it explicitly; Epic 7 completes only when every required track reaches its own closure condition. Stories 7.14, 7.19, and 7.20 evolve `src/Hexalith.EventStore.Admin.UI` in place as one ordered UX sequence. Stories 7.15–7.18 are completed planning artifacts only and authorize no runtime implementation.

### Story 7.1: Delivery Contract And Poison Handling

As an event-subscriber operator,
I want delivery semantics and poison handling to be explicit and enforced,
So that subscriber failures cannot become infinite retry storms or hidden data-loss paths.

**Requirements coverage:** Primary ownership of FR34's at-least-once/unordered delivery, poison/dead-letter, and bounded in-memory deduplication slice; primary NFR6 delivery semantics, supporting NFR2 tenant isolation, NFR5 bounded metadata, and NFR16 persisted evidence. Supports the later UX-DR29 and UX-DR39 recovery journey without owning their UI implementation.

**Architecture constraints:** AD-6, AD-8, AD-10, and AD-12. Persisted `MessageId` is deduplication identity, aggregate `SequenceNumber` is never global order, poison evidence is tenant-scoped and support-safe, and production acceptance requires durable broker/state evidence.

**UX coverage:** No primary UX-DR ownership. This story supplies the bounded dead-letter count, oldest age, visible tenant/domain scope, failure category, retryability, and stable action identity that Story 7.19 must present; it never supplies raw payloads or a success claim for an accepted recovery attempt.

**Dependencies:** Stories 4.1 and 4.4 for stable event identity and committed-publication recovery, and Stories 5.2 and 5.4 for protected tenant-scoped Admin reads. Subscriber poison handling remains distinct from producer publication-drain recovery.

**Current reconciliation:** Story 7.1 remains backlog. DAPR components and a sample subscription already configure dead-letter topics, producer drain exhaustion can publish dead-letter records, Admin dead-letter query/action scaffolding exists, and `EventStoreDomainEventProcessor` deduplicates through marker stores with in-progress deferral. The default in-memory marker store remains unbounded, and no single production-proven subscriber contract currently binds retry/max-age exhaustion, durable poison evidence, acknowledgement, and bounded marker retention end to end.

**Acceptance Criteria:**

**Given** EventStore domain events are published and subscribed
**When** the public delivery contract and deployment guidance are inspected
**Then** they state that delivery is at-least-once and unordered, deduplication uses persisted `MessageId`, and `SequenceNumber` is meaningful only within the addressed aggregate/domain semantics
**And** CloudEvent id, envelope identity, retry acknowledgement, and dead-letter topic conventions agree across Client contracts, DAPR subscriptions/components, samples, and production deployment templates.

**Given** the same completed event is delivered more than once
**When** the production subscriber processor acquires its marker
**Then** the first successful logical handling durably completes one marker and every exact redelivery is an acknowledged no-op with zero handler side effects
**And** conflicting content for the same message identity fails closed or enters the approved poison path rather than being treated as an exact duplicate.

**Given** a duplicate arrives while the first attempt is still active
**When** marker admission executes
**Then** the duplicate remains retryable or deferred and cannot run handlers concurrently with the active owner
**And** cancellation, handler failure, marker-store failure, or lost completion response follows an explicit release/reconcile policy that never converts ambiguity into a second successful side effect.

**Given** the in-memory marker implementation is selected for development or tests
**When** completed and abandoned entries exceed configured retention time or count
**Then** deterministic expiry/compaction keeps the collection at or below its validated numeric capacity while preserving active-owner safety
**And** boundary, concurrent-acquire, completion, failure-release, expiry, and capacity-pressure tests prove no unbounded growth or premature active-entry eviction; documentation states that it is not durable production deduplication evidence.

**Given** subscriber processing repeatedly fails or exceeds the configured maximum delivery attempts or age
**When** the broker/subscription exhaustion boundary is reached
**Then** the message is acknowledged from the live subscription only after the configured tenant/domain dead-letter path durably accepts its poison record, or remains retryable when that transfer is unproven
**And** timeout, cancellation, dead-letter publication failure, duplicate exhaustion, and restart cannot lose the original live message or create multiple logical poison records.

**Given** a poison record is created
**When** its persisted/broker contract is inspected
**Then** it retains stable message/correlation/causation identity, visible tenant/domain/aggregate scope, original sequence/timestamp, attempt count, first/last failure time, bounded failure category/reason, source topic, and retryability without changing source event identity
**And** raw payloads, protected bytes, tokens, secret values, decoded claims, stack traces, arbitrary exception messages, and cross-tenant discovery data are absent from logs and operator-facing evidence.

**Given** poison evidence is queried or counted for later Admin recovery
**When** tenant and permission filtering executes
**Then** count, oldest age, category, and item metadata include only the caller's authorized visible scope and use opaque bounded continuation state
**And** missing, denied, malformed, or unavailable storage fails closed without confirming whether hidden tenant/domain/message records exist.

**Given** a poison message is later retried, skipped, or archived by an authorized audited Admin journey
**When** this story's backend contract is consumed
**Then** the action uses stable tenant/message identity, is idempotent under duplicate requests, returns accepted or terminal non-success rather than fabricated completion, and exposes authoritative end-state evidence for later confirmation
**And** retry never bypasses normal deduplication, identity validation, upcasting, authorization, or handler semantics and archive/skip never deletes the immutable source event.

**Given** Story 7.1 completion is requested
**When** duplicate, active duplicate, out-of-order, late, conflicting, handler-failure, retry/max-age exhaustion, dead-letter-transfer failure, restart, marker-capacity, cross-tenant denial, and recovery-contract scenarios run through the production subscriber, marker store, DAPR subscription, broker, dead-letter store/topic, and persisted readback
**Then** handler effects, marker state, live/dead-letter acknowledgement, attempt evidence, and tenant-scoped metadata match the single-delivery baseline with no lost or multiply handled event
**And** focused Client/Server/Admin contract tests, structured DAPR configuration tests, a real-sidecar/broker lane, and Release build pass with no unexpected skips, warnings, errors, or leaked processes.

### Story 7.2: Admin Claims Normalization

As an administrator,
I want Admin claims normalized exactly like gateway claims,
So that missing or malformed tenant and permission input cannot widen access.

**Requirements coverage:** Primary ownership of FR34's Admin claims-normalization slice and NFR2's Admin tenant-isolation slice; supporting NFR1 fail-closed authorization. Supports UX-DR27, UX-DR28, and UX-DR38 at the authorization contract boundary without owning their UI implementation.

**Architecture constraints:** AD-3, AD-10, and AD-12. Application identity and current tenant authorization precede disclosure, canonical normalized claims are shared rather than reinterpreted per controller, and denial proof includes production-host behavior rather than only transformation-unit tests.

**UX coverage:** No primary UX-DR ownership. This story supplies bounded denied/authorized context to Story 7.19; denied responses must not confirm resource existence, enumerate hidden tenant claims, echo tokens, or expose decoded JWT content.

**Dependencies:** Stories 5.2–5.5 for Admin endpoint authorization, tenant filtering, authentication guards, support-safe responses, and internal trust boundaries.

**Current reconciliation:** Story 7.2 remains backlog. `AdminClaimsTransformation` and Admin authorization policies are registered and tested, but the transformation derives roles mainly from already-normalized `eventstore:*` claims and does not share the gateway's parsing of `tenants`, `permissions`, `tenant_id`/`tid`, and subject identity. Early role-based idempotency and controller-local first-tenant resolution can also leave normalization and scope behavior inconsistent, so existing partial code is not completion evidence.

**Acceptance Criteria:**

**Given** gateway and Admin hosts receive the same authenticated principal
**When** raw `tenants`, `permissions`, `domains`, `tenant_id`/`tid`, `sub`, roles, and existing `eventstore:*` claims are normalized
**Then** both hosts use one shared canonical parsing/validation contract and produce equivalent normalized tenant, permission, domain, and name-identifier claims
**And** Admin-role derivation consumes only that normalized result rather than a controller-specific interpretation of raw JWT values.

**Given** a source claim is a JSON string array, a space-delimited value, repeated claim values, or an already-normalized value
**When** normalization executes
**Then** valid non-empty items are canonicalized, deduplicated ordinally, bounded by the approved count/length limits, and emitted once without changing the authenticated source identity
**And** malformed JSON, wrong JSON shape, null, blank, control/non-ASCII, reserved-delimiter, oversized, or over-count values do not fall back into broader authorization and produce only support-safe diagnostics.

**Given** an authenticated principal has no valid normalized tenant scope and is not a valid global administrator
**When** Admin role derivation or a tenant-scoped query/action executes
**Then** no `ReadOnly`, `Operator`, or tenant-wide role is inferred solely from authentication, malformed claims, or unrelated permissions and access is denied before data/state lookup
**And** an absent requested tenant never means all tenants or silently selects an arbitrary first tenant.

**Given** an authenticated principal has normalized permissions and tenant scope
**When** Admin roles are derived
**Then** `ReadOnly`, `Operator`, and `Admin` follow the single explicit permission/global-administrator mapping and least-privilege precedence defined by the shared contract
**And** unrelated permission strings, case/lookalike variants, boolean-like strings, caller-supplied Admin flags, or malformed role arrays cannot elevate privilege.

**Given** a principal contains a trusted global-administrator claim
**When** global scope is granted
**Then** the accepted issuer/schema/value and algorithm-authenticated provenance meet the production authentication policy and the normalized Admin role is idempotent
**And** `global_admin`, role aliases, or arrays from an untrusted/malformed representation do not become authority merely because their text resembles an accepted value.

**Given** normalization runs multiple times or across multiple authenticated identities
**When** existing canonical claims or Admin roles are present
**Then** the result remains idempotent, contains no duplicate or contradictory authority claims, and cannot skip validation merely because one target claim already exists
**And** unauthenticated identities remain unchanged and can never receive normalized authorization claims.

**Given** a same-tenant, cross-tenant, missing-tenant, multi-tenant, malformed-tenant, missing-permission, unrelated-permission, operator, and valid-global-admin request reaches each representative Admin query and mutation policy
**When** authorization and tenant filtering execute
**Then** only the allowed scope reaches the service/state boundary and every denied case performs zero protected lookup or mutation
**And** denial returns the same bounded status/category and ULID-safe correlation contract without resource-existence confirmation, authorized-tenant enumeration, raw claim values, tokens, or stack traces.

**Given** Story 7.2 completion is requested
**When** shared normalizer contract tests, gateway/Admin parity fixtures, transformation idempotency tests, every claim-shape boundary, controller/filter integration tests, production authentication-host tests, and cross-tenant negative tests run
**Then** normalized claims, derived roles, authorization outcomes, downstream call counts, and tenant-scoped results match the canonical least-privilege matrix
**And** affected Admin.Server, gateway, host, and Release builds pass with warnings-as-errors and no unexpected skips.

### Story 7.3: State-Mutating Admin Audit

As an operator,
I want every state-mutating Admin action attributable,
So that privileged changes can be audited without exposing sensitive data.

**Requirements coverage:** Primary ownership of FR34's Admin mutation-audit slice and NFR16's persisted Admin-audit evidence; supporting NFR1–NFR2 fail-closed authorization and tenant isolation and NFR15 honest outcomes. Supports UX-DR17, UX-DR38–UX-DR40 without owning their UI implementation.

**Architecture constraints:** AD-10 and AD-12. Current authentication/authorization and canonical validation precede mutation, durable audit admission precedes side effects, and HTTP or log output alone cannot prove an attributable terminal outcome.

**UX coverage:** No primary UX-DR ownership. This story supplies support-safe audit identity, actor, action, scope, accepted/terminal state, time, and reason evidence to Story 7.19; it never returns raw request/response bodies, event or read-model payloads, tokens, decoded claims, secrets, provider internals, or stack traces.

**Dependencies:** Story 7.2 for canonical actor/tenant/permission claims and Stories 5.2–5.5 for fail-closed Admin authorization and support-safe boundary behavior.

**Current reconciliation:** Story 7.3 remains backlog. `HttpContextAdminAuthContext` and a crypto-shredding-specific persisted audit path provide useful foundations, but no shared durable audit protocol or complete mutation inventory covers projection, snapshot, recovery/dead-letter, tenant/access, consistency, storage, and settings actions. Controller logs and operation-specific audit IDs do not satisfy the universal persisted end-state requirement.

**Acceptance Criteria:**

**Given** Admin routes, typed clients, services, and background continuations are inventoried
**When** mutation coverage is classified
**Then** every implemented state-changing operation has one canonical action name, tenant/scope and safe target identity, required permission, mutation owner, evidence source, and shared audit integration point
**And** read-only operations, deferred `501` routes, and background reconciliation are explicitly classified so no mutation is omitted or counted twice.

**Given** an authenticated Admin mutation request passes canonical validation and current authorization
**When** it is admitted
**Then** a durable audit intent with stable operation/audit identity is persisted and read back before any business side effect, containing authenticated subject, tenant/scope, action, safe target identity, permission context, ULID-safe correlation, request time, and `Accepted` state
**And** duplicate requests with the same operation identity converge on the same intent/result rather than creating repeated side effects or unrelated audit records.

**Given** authentication, authorization, tenant scope, or validation denies a mutation
**When** the request terminates
**Then** a bounded denial audit is persisted when the trusted audit boundary is available, records no protected-state lookup or existence conclusion, and uses the same safe action/correlation taxonomy
**And** audit content excludes raw claims, token material, submitted payloads, hidden tenant/resource identifiers, validation secrets, arbitrary exception text, and stack traces.

**Given** audit-intent persistence is unavailable, denied, malformed, times out, or cannot be read back
**When** a mutation would otherwise start
**Then** the mutation fails closed with zero downstream business calls and a support-safe unavailable/indeterminate response
**And** logging cannot substitute for the missing durable intent or allow an operator override through an untrusted request flag.

**Given** the business mutation succeeds, fails, is cancelled, remains accepted/pending, or has an ambiguous response
**When** audit terminalization runs
**Then** the record conditionally advances through the closed approved states with occurred/completed time, bounded reason code, and authoritative evidence reference while preserving the original actor, action, scope, and correlation
**And** only read-back-proven terminal state is reported as succeeded; post-side-effect audit ambiguity returns accepted/indeterminate and enters bounded reconciliation rather than falsely reporting success or retrying the mutation as fresh work.

**Given** a process crashes or loses its response after audit admission or mutation execution
**When** retry/recovery examines the stable operation identity
**Then** it reads the durable intent and authoritative business evidence, resumes only missing safe work, and terminalizes one audit history without duplicating the logical mutation
**And** abandoned accepted/indeterminate records remain discoverable with age/reason evidence and cannot silently disappear through TTL or compaction before the approved retention boundary.

**Given** multiple hosts race the same or conflicting Admin mutation
**When** audit and business admission execute
**Then** conditional writes/fencing serialize the stable operation identity, record the winning disposition, and preserve separate attributable conflicts where identities differ
**And** no loser overwrites a newer terminal audit, changes the recorded actor/scope/action, or applies the business mutation after losing authority.

**Given** authorized operators query audit evidence
**When** filtering, paging, retention, export, or tenant-scoped access runs
**Then** results are immutable or append-preserving, bounded, ordered by stable time/id, tenant-filtered before data access, and expose only approved safe fields and opaque pagination
**And** denied, unavailable, corrupt, or cross-tenant cases fail closed without revealing record existence, audit contents, cursors, ETags, provider keys, or storage details.

**Given** Story 7.3 completion is requested
**When** every mutation in the closed inventory is exercised for authorized success, accepted/pending, domain failure, validation failure, denial, cancellation, audit-intent outage, post-side-effect ambiguity, duplicate request, multi-host race, crash/resume, and cross-tenant access
**Then** persisted business state and durable audit intent/terminal history agree on actor, tenant, action, outcome, evidence, and correlation, with zero mutation whenever preflight audit or authority is unproven
**And** focused Admin.Server/CLI contract tests, production-host integration tests, real state-store readback, redaction/leakage tests, and Release build pass with no unexpected skips or warnings.

### Story 7.4: Honest Deferred Admin Operations

As an administrator,
I want unavailable operations represented honestly,
So that backup, restore, import, compaction, and other deferred capabilities cannot appear to run or succeed.

**Requirements coverage:** Primary ownership of FR34's deferred-Admin-operation slice, NFR15, and UX-DR22. Supporting NFR1–NFR2 ensure capability responses never bypass authentication or tenant scope.

**Architecture constraints:** AD-10 and AD-21. Authorization remains fail closed, `Admin.UI` remains the single eventual presentation host, and an unavailable capability is hidden, disabled/read-only, or returns `501`—never modeled as a successful or accepted mutation.

**UX coverage:** Primary UX-DR22. Every visible deferred affordance uses the exact message **“Unavailable in this release.”** plus tracking context only; fake forms, submit buttons, progress, job creation, optimistic toasts, or synthetic completion evidence are forbidden.

**Dependencies:** Story 7.2 for canonical authorized/denied scope behavior. Story 7.3 audit is not invoked because a deferred route must perform no state mutation; any future implementation must separately adopt its audit contract.

**Current reconciliation:** Story 7.4 remains backlog. Several services already return typed `Deferred` results and some routes return `501`, but backup and compaction pages still render detailed forms and “Submit Deferred Request” controls, some clients accept HTTP success with `Success=false`, and copy is inconsistent with UX-DR22's exact message. Existing defensive behavior is partial and the rendered fake workflows directly contradict the confirmed UX contract.

**Acceptance Criteria:**

**Given** Admin.Server, Admin.UI, Admin.Cli, typed-client, configuration, and documentation surfaces are inventoried
**When** the deferred-capability matrix is generated
**Then** every unavailable operation names its route/control, authorization policy, canonical dashboard destination, tracking artifact, UI disposition, server disposition, and owning future story
**And** the closed inventory includes backup creation/validation/restore, stream import/export where not truly implemented, compaction, manual snapshot work until its authorized implementation lands, health-history gaps, GDPR erasure, interactive OIDC login, aggregate test kit, REST generator hardening, and every other discovered mock/deferred action.

**Given** an unavailable operation has no useful read-only tracking context
**When** the UI is rendered
**Then** its action, form, dialog, route entry, and command-palette item are hidden by default
**And** direct legacy navigation resolves to Deferred & Backlog or an explicit unsupported state rather than rendering a second workflow or an empty actionable shell.

**Given** an unavailable operation has useful visible history or tracking context
**When** its canonical surface is rendered
**Then** history is read-only, the action is disabled or absent, and the exact resource-backed whole string “Unavailable in this release.” appears with its approved tracking identifier/link
**And** no input form, file picker, acknowledgement checkbox, confirmation dialog, “submit deferred request,” fake job, progress state, or success-capable control is rendered.

**Given** an authenticated authorized caller reaches a retained deferred server route directly
**When** the request passes authentication, tenant authorization, and bounded validation
**Then** the route returns HTTP `501 Not Implemented` with the canonical typed reason code, exact safe message, correlation, and tracking context and performs zero EventStore, DAPR, broker, provider, audit-admission, or background-job mutation
**And** HTTP `200`, `202`, a synthetic operation id, `Success=false` inside a success response, or a deferred job record cannot substitute for `501`.

**Given** an unauthenticated, unauthorized, cross-tenant, malformed, or oversized request targets a deferred route
**When** boundary processing runs
**Then** authentication/authorization/validation fails before capability disclosure according to the canonical security policy and no downstream service executes
**And** the response never confirms hidden resource existence, echoes submitted content, reveals raw claims, or changes denial into `501` merely because the operation is unavailable.

**Given** UI/client code receives `501`, denial, not-found, cancellation, timeout, malformed response, or service unavailability
**When** it maps the outcome
**Then** it renders the exact unavailable or applicable safe non-success state and preserves focus/selection without any accepted, pending, successful, retrying, or completed indication
**And** notifications, local state, stale job history, or an upstream `Success=true` payload cannot override the matrix's deferred classification.

**Given** GDPR erasure, Admin OIDC, aggregate test-kit, and generator-hardening planning artifacts are visible in Deferred & Backlog
**When** an operator inspects them
**Then** only description, status, dependency/risk summary, and tracking context are available and no runtime action is offered
**And** completion of Stories 7.15–7.18 remains planning completion only and grants no implementation or mutation authority.

**Given** a deferred capability is proposed for implementation in a later release
**When** its status changes
**Then** an explicitly approved implementation story updates server behavior, typed contracts, UI matrix, audit/security integration, documentation, and positive production-path evidence atomically
**And** deleting a `501`, enabling a hidden control, or changing copy alone cannot promote the capability to implemented.

**Given** Story 7.4 completion is requested
**When** automated route/control inventory scans and focused Admin.Server/Admin.UI/Admin.Cli tests exercise every matrix row, legacy deep link, direct endpoint, role/tenant case, malformed input, and response category
**Then** all unavailable surfaces are hidden or honest/read-only, every retained endpoint returns the approved `501`, and downstream mutation/audit/provider calls remain zero
**And** fake forms, deferred-submit controls, synthetic operation identifiers, optimistic completion copy, and noncanonical messages are absent from source/rendered output while affected Release builds pass.

### Story 7.5: Shared Typed Admin Client

As an Admin surface developer,
I want one shared typed Admin client boundary,
So that UI and tools do not duplicate transport mapping or bypass authorization and evidence semantics.

**Requirements coverage:** Primary ownership of FR34's shared Admin client-boundary slice and NFR14's Admin consumer-only boundary; supporting NFR12 additive compatibility and NFR15 honest unavailable outcomes. Supports UX-DR24–UX-DR30 and UX-DR38 at the typed-contract boundary without owning their UI implementation.

**Architecture constraints:** AD-3, AD-4, AD-10, and AD-21. Admin UI/CLI are consumers of one platform-owned typed client/contract package, transport and security remain host-owned, and interactive UI hosts gain no generated or hand-written per-message MVC command/query controllers.

**UX coverage:** No primary UX-DR ownership. The client preserves authoritative evidence/provenance, accepted versus terminal state, typed denial/unavailable/cancellation outcomes, bounded validation details, audit correlation, and opaque paging; Story 7.19 owns presentation.

**Dependencies:** Stories 7.2–7.4 for canonical claims/denial behavior, durable mutation-audit evidence, and the deferred-operation `501` contract.

**Current reconciliation:** Story 7.5 remains backlog. `Admin.Abstractions` contains server service contracts, but Admin.UI registers many feature-specific HTTP wrappers while Admin.Cli owns a separate `AdminApiClient` and constructs `HttpClient` per command. URL construction, serialization, error mapping, disposal, authentication, and cancellation behavior are duplicated; a named `AdminApi` client alone is not the shared typed boundary required here.

**Acceptance Criteria:**

**Given** supported Admin.Server endpoints and consumers are inventoried
**When** the shared client contract is defined
**Then** one reusable package exposes a single composition root and bounded typed feature facets for health, commands, streams/events, projections, tenants/access, topology, storage/snapshots, recovery/dead letters, audit, backlog, and settings
**And** every implemented route has exactly one request/response/result mapping while deferred routes remain represented only through the Story 7.4 unavailable contract.

**Given** Admin.UI or Admin.Cli performs a supported operation
**When** its dependency graph and source are inspected
**Then** it calls the shared typed client/facet and does not construct endpoint URLs, serialize request bodies, deserialize responses, parse `ProblemDetails`, or instantiate/dispose operation-local `HttpClient` itself
**And** existing UI `Admin*ApiClient` wrappers and CLI `AdminApiClient` mappings are removed or reduced to compatibility adapters that delegate without independent transport semantics.

**Given** host composition configures the shared client
**When** base address, authentication, DAPR service invocation, timeout, correlation, and resilience handlers are assembled
**Then** those concerns are injected once through `IHttpClientFactory`/platform registration in the consuming host with the approved handler order and caller cancellation propagation
**And** bearer tokens, DAPR app-id/api-token headers, inbound forwarding, retry policy, or environment-specific addresses cannot be set ad hoc by a page or command.

**Given** success, accepted/evidence-pending, validation, denial, not-found, conflict, deferred `501`, throttled, cancelled, timeout, unavailable, malformed, and unknown responses occur
**When** the client maps them
**Then** each becomes one closed typed outcome preserving only approved status, reason code, retryability/`Retry-After`, safe validation fields, audit/correlation identity, authoritative evidence/provenance, and opaque paging metadata
**And** HTTP `200`/`202`, an ETag, SignalR, a non-null payload, or a local exception never becomes confirmed business success without the required authoritative terminal evidence.

**Given** an error body is malformed, oversized, non-JSON, secret-bearing, or contains unknown extensions
**When** client diagnostics are produced
**Then** body reads, extension counts, field lengths, and logging are bounded; only allow-listed safe fields survive; and the original status/category/correlation remain usable when valid
**And** raw bodies, tokens, decoded claims, payloads, protected metadata, cursors, ETags, provider endpoints/credentials, arbitrary exception text, and stack traces are never returned or logged as operator detail.

**Given** tenant, identifier, filter, paging, and mutation inputs are supplied
**When** request construction runs
**Then** typed models enforce required shape/size, URI segments and query values are encoded once, continuation values remain opaque, and cancellation reaches the transport and server
**And** invalid input performs zero HTTP calls, raw payload is not echoed in validation, and EventStore ULIDs are never described or parsed as GUIDs.

**Given** a mutation is submitted through the shared client
**When** accepted and later evidence is queried
**Then** stable operation/audit/correlation identity permits polling or explicit refresh without resubmission and the client keeps `Accepted`, `EvidencePending`, terminal success, and terminal non-success distinct
**And** duplicate polling is side-effect free and cancellation/disconnection does not trigger a second mutation.

**Given** existing Admin consumers and packages upgrade
**When** compatibility and migration tests run
**Then** approved public models remain additive or use the documented compatibility adapter/version boundary, UI and CLI resolve the same contract package, and Debug/source and Release/package modes expose equivalent behavior
**And** Server service interfaces, domain contracts, or generic EventStore gateway APIs are not collapsed into the Admin HTTP client or broken incidentally.

**Given** Story 7.5 completion is requested
**When** route-to-method inventory tests, transport-handler tests, contract serialization tests, every outcome/error case, authentication/correlation propagation, cancellation, UI/CLI composition, duplicate-mapping scans, package/API compatibility, and representative real-host calls run
**Then** all Admin UI/CLI HTTP traffic traverses the one shared typed boundary with equivalent safe results and no duplicate request/response mapping remains
**And** Admin.Abstractions/client, Admin.Server, Admin.UI, Admin.Cli, and Release builds pass with warnings-as-errors and no unexpected skips.

### Story 7.6: OpenBao-Backed DAPR Secret Store

As a production operator,
I want operational and application secrets resolved through an OpenBao-backed DAPR component,
So that applications remain provider-independent and Kubernetes Secrets are not the system of record.

**Requirements coverage:** Primary ownership of FR34's OpenBao-backed production secret-store, DAPR Secrets API, and bootstrap-only Kubernetes Secret slice; primary NFR4 committed-secret exclusion and supporting NFR17 operational hardening and NFR16 real-provider evidence.

**Architecture constraints:** AD-9, AD-10, AD-12, and AD-24. The deployment overlay owns one canonical `openbao` component and value-free secret contract; applications use DAPR only; configuration, scopes, policies, readiness, rotation, and tests change as one coherent slice.

**UX coverage:** No primary UX-DR ownership. Topology/Settings may expose only component readiness, logical configuration key, non-secret generation, last successful refresh, and bounded failure category. UX-DR16 and UX-DR38 support-safety applies: no secret value, token path/content, policy content, provider response, or stack trace is rendered.

**Dependencies:** Stories 5.6–5.9 for AppHost/DAPR topology parity, production component/ACL enforcement, drift tests, and operator documentation. This operational secret store neither depends on nor authorizes Epic 8's payload-protection engine or production cryptographic key-custody backend.

**Current reconciliation:** Story 7.6 remains backlog. The repository has DAPR `GetSecretAsync` usage for the idempotency digest key, but no canonical `openbao` component, secret contract, AppHost resource, per-app secret scopes, policy artifacts, readiness gate, or real OpenBao-through-DAPR test. Current Kubernetes documentation recommends Kubernetes Secrets as the primary store and Azure guidance selects Key Vault/managed identity, contradicting AD-24's production posture rather than proving it.

**Acceptance Criteria:**

**Given** the production secret inventory is defined
**When** `deploy/dapr/openbao-secret-contract.yaml` is validated
**Then** each logical secret records its store-relative name, embedded map keys/value shape, consumer app IDs, dependent component/host, `startup-only` or `runtime-required` lifecycle, matching OpenBao policy path, rotation unit, and runtime generation/cache/overlap bounds without containing a value
**And** missing/extra consumers, duplicate logical names, incompatible shapes, unbounded cache/rotation data, or secret-like values fail validation.

**Given** deployment overlays compose the DAPR secret store
**When** structured manifests are inspected
**Then** exactly one singleton component is named `openbao`, uses `type: secretstores.hashicorp.vault`, `version: v1`, `vaultValueType: map`, and `vaultKVUsePrefix: true`, and the production DAPR runtime is pinned compatibly with the repository's `1.18.0` seed
**And** the overlay alone supplies environment-specific `vaultAddr`, `enginePath`, `vaultKVPrefix`, TLS metadata, component scopes, and policy bindings; applications and dependent component files author no competing copy.

**Given** production OpenBao transport and bootstrap are configured
**When** manifests, parameters, and runtime metadata are inspected
**Then** non-development profiles use HTTPS with certificate verification, prefer `vaultTokenMountPath` backed by a least-privilege projected token file, and keep the bootstrap token, DAPR API token, and TLS trust inputs uncommitted and outside the DAPR/OpenBao lookup cycle
**And** inline `vaultToken`, disabled TLS verification, plaintext token/config values, logged bootstrap material, or a dependency on the secret store to obtain its own bootstrap input fails deployment validation.

**Given** no approved projected/mounted bootstrap mechanism exists for a target platform
**When** a Kubernetes Secret exception is used
**Then** the documented exception names the exact bootstrap credential, consumer, reason, custody/rotation process, and removal trigger and contains no downstream application, database, broker, signing, operational, or payload-protection secret
**And** every other Kubernetes/DAPR secret-store grant is default-deny; automatic `kubernetes` stores have `defaultAccess: deny` and no application can retrieve from them absent a separately approved exact-key bootstrap exception.

**Given** state-store, pub/sub, or other DAPR components require credentials
**When** their production YAML is rendered
**Then** each value comes from `secretKeyRef` with `auth.secretStore: openbao`, maps exactly to the canonical contract, and its consuming component is covered by the least-privilege component/OpenBao scopes
**And** environment placeholders, Kubernetes application secrets, inline values, extra grants, and contract/scope/policy drift fail the same deployment gate.

**Given** an application requires a runtime logical secret
**When** its EventStore-owned provider retrieves it
**Then** injected `DaprClient.GetSecretAsync("openbao", logicalName, ...)` requests one approved map, validates exact keys/value shapes and required non-secret generation, zeroes/discards replaced sensitive buffers where applicable, and propagates cancellation
**And** application code imports no OpenBao/Vault SDK, performs no provider HTTP call, uses no `BulkGetSecret`, does not pin serving-path `metadata.version_id`, persists no value, and has no plaintext/alternate-provider fallback.

**Given** startup-only components or runtime-required consumers depend on secrets
**When** startup, refresh, expiry, missing/denied/malformed lookup, store outage, or generation mismatch occurs
**Then** each host validates its declared contract through DAPR before readiness; successful runtime values live only in memory for a cataloged `maxAge` shorter than rotation overlap; failed refresh disables the dependent operation and keeps readiness false until a bounded successful recheck
**And** consumers never merge generations, use expired values, claim ready from stale/unknown evidence, disclose values, or let a noncritical fake satisfy production readiness.

**Given** a planned runtime secret rotation occurs
**When** publish-overlap-acknowledge-revoke executes
**Then** the new OpenBao map generation is published, old/new credentials overlap, every runtime consumer acknowledges the new generation while ready, every startup-only component completes controlled sidecar rollout/initialization, and only then is old material revoked
**And** failure before full acknowledgement retains the old credential and rolls back by publishing a restored generation; early revocation, automatic token-reload assumptions, mixed-generation use, or acknowledgement inferred only from time is forbidden.

**Given** the OpenBao component bootstrap token rotates
**When** replacement is required before expiry or after compromise
**Then** the platform performs a controlled DAPR sidecar rollout/restart with the replacement bootstrap input, keeps readiness false until component initialization and declared secret validation succeed, and then revokes the prior token
**And** documentation does not claim automatic Vault-token renewal/reload unsupported by the component.

**Given** local development composes the dependency
**When** the Aspire resource graph starts
**Then** it provisions a pinned official OpenBao container, provides bootstrap through an Aspire secret parameter or protected temporary token file, health-checks it, waits before dependent sidecars, and scopes the canonical component only to consumers that require it
**And** development mode is labeled non-production and its token/value never appears in tracked files, command output, logs, traces, screenshots, or evidence artifacts.

**Given** production target documentation is rendered
**When** Kubernetes, progression, security, troubleshooting, and Azure Container Apps guidance is reviewed
**Then** it consistently names OpenBao/DAPR as the conforming production operational/application secret path and limits Kubernetes Secret use to the bootstrap exception
**And** Azure Container Apps managed DAPR is explicitly nonconforming until a separately approved profile proves OpenBao component support and equivalent secret-scope enforcement; Key Vault guidance cannot silently satisfy AD-24.

**Given** Story 7.6 completion is requested
**When** AppHost graph tests, structured contract/component/scope/policy/YAML validation, tracked-secret scans, application retrieval/cache/readiness/rotation tests, negative missing/denied/malformed/expired/cross-app cases, and a real pinned OpenBao instance accessed only through DAPR run
**Then** a seeded non-production secret is retrieved only by allowed consumers, dependent components initialize, readiness and rotation follow the contract, denied consumers learn no value/existence, and all evidence is value-free
**And** documentation validation and affected Release builds pass with no unexpected skips; fakes, LocalDev stores, direct Vault clients, Kubernetes application secrets, or Epic 8 key-custody tests cannot substitute for the real-provider lane.

### Story 7.7: Readiness And DAPR App-Health

As an operator,
I want explicit readiness and DAPR app-health behavior,
So that orchestration removes unhealthy traffic without weakening endpoint authorization.

**Requirements coverage:** Primary ownership of FR34's readiness and DAPR app-health slice; primary NFR17 probe/app-health operational hardening and supporting NFR1 fail-closed authorization and NFR16 real-host evidence.

**Architecture constraints:** AD-9, AD-10, AD-12, and AD-16. AppHost, sidecar options, deployment annotations, host endpoints, and tests form one topology contract; `/health`, `/alive`, and `/ready` are the only anonymous endpoints.

**UX coverage:** No primary UX-DR ownership. Overview/Topology may consume only support-safe component name, healthy/degraded/unhealthy state, observation time, and bounded reason; UX-DR12, UX-DR16, UX-DR24, and UX-DR38 prevent stale/unknown status from becoming a healthy or detailed-internal claim.

**Dependencies:** Stories 5.6–5.9 for runtime-topology and fail-closed alignment, plus Story 7.6 so required OpenBao/secret validation participates in readiness for hosts that consume it.

**Current reconciliation:** Story 7.7 remains backlog. `MapDefaultEndpoints` is present in several hosts, EventStore registers DAPR readiness checks, Admin host tests exercise the three probe paths, and domain-module sidecars enable app health at `/alive`. Main EventStore/Admin/UI/sample sidecars and publish overlays do not yet have one closed resource-to-probe/app-health matrix or complete parity evidence, so these partial foundations do not establish the requested topology-wide outcome.

**Acceptance Criteria:**

**Given** AppHost resources and deployment workloads are inventoried
**When** the probe/app-health matrix is validated
**Then** every EventStore, Admin.Server, Admin.UI, Sample UI/API, configured domain service, test subscriber, and other sidecar-backed application is classified by inbound traffic, required dependencies, `/health`/`/alive`/`/ready` ownership, DAPR app-health eligibility, and approved path
**And** no runtime or deployment resource is omitted, duplicated under a stale app id, or assigned a probe it does not actually map.

**Given** a platform host maps default endpoints
**When** `/alive`, `/ready`, and `/health` are called
**Then** `/alive` reports only process liveness, `/ready` evaluates exactly the host's traffic-critical dependencies, and `/health` returns the approved overall support-safe view
**And** checks are tagged consistently, an empty readiness predicate fails validation, and state store, pub/sub, config store, actor placement, sidecar, OpenBao/declared secrets, and downstream services participate only where the host truly depends on them.

**Given** DAPR app health gates inbound service invocation or pub/sub traffic
**When** sidecar options and deployment annotations are rendered
**Then** app health is enabled for every eligible receiver using the architecture-approved `/alive` path and consistent interval, timeout, and failure-threshold settings
**And** `/ready` is not used for DAPR app health where sidecar-dependent checks would create a feedback loop; outbound-only sidecars have an explicit tested exclusion rather than an accidental omission.

**Given** local AppHost and Docker/Kubernetes production topology are rendered
**When** resource references, `WaitFor`, health checks, sidecar arguments, and workload probes are compared
**Then** app ids, ports, paths, protocols, startup ordering, liveness/readiness/startup probes, and DAPR app-health settings match the closed matrix
**And** a missing/stale path, wrong port, absent sidecar flag, unsupported probe, or deployment-only/local-only resource fails topology validation before promotion.

**Given** fail-closed authentication and fallback authorization are enabled
**When** unauthenticated callers request the three probes and a representative protected endpoint on the same EventStore, Admin, UI/API, and domain host
**Then** only `/health`, `/alive`, and `/ready` are explicitly anonymous and support-safe while the protected endpoint returns the approved denial with zero data disclosure
**And** no default/fallback policy, middleware branch, DAPR ACL, or blanket anonymous metadata is weakened to restore probe reachability.

**Given** a probe dependency is healthy, degraded, unavailable, denied, timed out, malformed, or returns secret/provider detail
**When** endpoint classification and serialization run
**Then** liveness/readiness status and traffic eligibility follow the approved per-check criticality while output is bounded to check name, state, duration/time, and allow-listed reason code
**And** connection strings, endpoints with credentials, secret names/values, tokens, exception messages, stack traces, component configuration, or cross-tenant data are not returned or logged above the approved level.

**Given** an application becomes unhealthy or healthy again
**When** DAPR app-health polling reaches its configured threshold
**Then** the sidecar stops/resumes inbound traffic according to DAPR semantics without killing the application solely for one failed sample, and orchestration readiness independently reflects dependency recovery
**And** tests distinguish app-health traffic gating, pod/resource readiness, liveness restart eligibility, and dashboard observation rather than treating them as one status.

**Given** Story 7.7 completion is requested
**When** structured AppHost/sidecar/YAML/manifest tests, default-endpoint/tag tests, real authentication-host tests, dependency failure/recovery tests, and a live Aspire/DAPR lane exercise every matrix row
**Then** actual sidecar metadata/arguments and HTTP probe behavior match the declared topology, unhealthy traffic is gated, healthy traffic recovers, and protected endpoints remain denied throughout
**And** affected host and Release builds pass with no unexpected skips, warnings, leaked credentials, or leaked processes.

### Story 7.8: DAPR Resiliency

As an operator,
I want DAPR resiliency policies to cover the exact application and component targets used at runtime,
So that invocation and infrastructure failures have bounded, documented behavior.

**Requirements coverage:** Primary ownership of FR34's DAPR resiliency deployment slice; supporting NFR7 recovery safety, NFR16 runtime evidence, and NFR17 operational hardening.

**Architecture constraints:** AD-6, AD-9, and AD-12. AppHost/runtime app IDs and loaded resiliency targets remain one topology contract, retry safety depends on stable identities and recovery semantics, and YAML shape alone is not execution evidence.

**UX coverage:** No primary UX-DR ownership. Topology may display only loaded/unavailable status, target name/direction, policy name, and bounded configured values; UX-DR16 and UX-DR38 prohibit raw YAML, provider endpoints, credentials, or a green claim based only on a parseable file.

**Dependencies:** Stories 4.1–4.4 for stable identity/idempotency and publication recovery, Stories 5.6–5.9 for AppHost/DAPR parity, and Story 7.1 for bounded subscriber retry/dead-letter terminal behavior.

**Current reconciliation:** Story 7.8 remains backlog. Local and production resiliency YAML define retry, timeout, and circuit-breaker policies for `eventstore`, `pubsub`, and `statestore`, and tests validate basic references. The two files use materially different numeric policies, omit other runtime invocation targets such as Admin/domain services, and document effectively unbounded Kafka retries; no closed invocation-safety inventory or live transient/terminal/open-circuit proof establishes the required bounded behavior.

**Acceptance Criteria:**

**Given** runtime service invocations and DAPR component calls are inventoried from AppHost, code, configuration, and deployment overlays
**When** the resiliency target matrix is generated
**Then** every caller, exact app/component target, method/operation class, direction, idempotency/recovery identity, timeout owner, safe retry disposition, and terminal/dead-letter behavior is recorded
**And** conditional source/package resources, Admin/UI calls, EventStore-to-domain calls, state/config/secret stores, pub/sub inbound/outbound, actors, bindings, and any discovered target are included or explicitly excluded with a tested reason.

**Given** local and production resiliency YAML are parsed structurally
**When** policies and targets are validated against the matrix
**Then** every referenced retry, timeout, and circuit-breaker exists; every required current app/component id is targeted; and every target binds only policies approved for its operation class
**And** stale/unknown/missing app ids, orphaned policies, duplicate authority, misspelled direction, unsupported fields, empty target sets, or an unvalidated environment-only delta fails the topology check.

**Given** retry policies combine with component/provider retries
**When** effective behavior is calculated for Redis, PostgreSQL, RabbitMQ, Kafka, Azure Service Bus, OpenBao, and each supported production component
**Then** the total attempt count and maximum elapsed time have explicit finite numeric upper bounds accounting for multiplication, backoff, timeout, and broker/client defaults
**And** infinite/default-unknown provider retries, overflow, retry storms, unbounded max age, or comments that substitute for enforced configuration fail validation.

**Given** a non-idempotent or ambiguous mutation might be retried
**When** target safety is reviewed
**Then** retry is enabled only when stable EventStore operation/message identity, durable admission/fencing, and status/reconciliation semantics make re-execution safe; otherwise the policy uses zero retry or routes to polling/recovery
**And** HTTP/service invocation success, timeout, connection loss, or DAPR retry cannot create a fresh command identity, duplicate append/handler effect, repeated Admin mutation, or checkpoint advancement from ambiguous work.

**Given** transient, throttled, terminal, validation, authorization, not-found, conflict, timeout, cancellation, and malformed responses occur
**When** DAPR evaluates retry matching
**Then** only approved transient categories consume the bounded retry budget, `Retry-After`/backoff constraints are respected where supported, and terminal/security/client errors return without retry
**And** caller cancellation remains distinct from policy timeout, circuit-open remains a typed fast failure, and retries do not extend beyond the caller or operation deadline.

**Given** failures reach the circuit-breaker threshold
**When** closed, open, half-open, recovery, and recurrent-failure phases execute
**Then** request admission, half-open probe count, interval, open duration, and reset behavior match configured numeric policies and prevent a thundering herd
**And** circuit state does not erase durable recovery work, acknowledge poison/live messages prematurely, claim readiness, or expose internal failure bodies.

**Given** local and production profiles intentionally use different numeric policies
**When** parity validation runs
**Then** the same target/operation inventory and safety invariants apply, every delta is named with rationale and tested effective bounds, and production remains the release authority
**And** local settings cannot silently become a production overlay or make a production-only target invisible to validation.

**Given** application code performs a call covered by DAPR resiliency
**When** source and runtime traces are inspected
**Then** it makes one logical DAPR call and delegates retries to the approved layer unless an explicitly documented higher-level recovery loop uses a distinct durable identity and budget
**And** nested HTTP/library retry loops, timer retries, broker defaults, and DAPR policies are included in the effective bound rather than counted independently or ignored.

**Given** Story 7.8 completion is requested
**When** structured local/production YAML tests, AppHost/runtime target scans, provider effective-bound tests, and live DAPR scenarios exercise transient success, terminal response, timeout, cancellation, retry exhaustion, circuit open/half-open/recovery, broker outage, and state-store outage
**Then** actual attempt counts/timing and durable end state match the matrix, safe operations converge once, unsafe operations are not retried, and terminal poison/recovery evidence remains intact
**And** operator documentation, Admin support-safe parsing, full relevant regressions, and Release builds pass with no unexpected skips, warnings, or leaked processes.

### Story 7.9: Immutable Production Images

As a deployment operator,
I want production workloads to reference immutable image identities,
So that every deployed EventStore version can be traced to one approved build and rolled back without tag drift.

**Requirements coverage:** Primary ownership of FR34's immutable production-promotion slice; supporting NFR11 manifest-governed release inventory, NFR16 provenance evidence, and NFR17 deployment hardening.

**Architecture constraints:** AD-9, AD-11, AD-12, and AD-22. Production topology and image identity change together, the canonical validated OCI index digest—not a tag—is deployment authority, and every EventStore lineage edge derives from retained release evidence.

**UX coverage:** No primary UX-DR ownership. Topology may expose the approved repository, semantic version, shortened/full-copy immutable digest, source revision, and verification state as support-safe identifiers; it must not expose registry credentials, bearer tokens, workflow secrets, raw attestations, or call an unresolved tag “deployed.”

**Dependencies:** Stories 3.14 and 3.15 for a separately authorized conforming corrective release and independently validated exact-lineage `ReleaseIdentity`; Stories 5.6–5.9 for deploy-topology parity. Immutable rejected `v3.94.1` is permanently non-authorizing and cannot satisfy this dependency.

**Current reconciliation:** Story 7.9 remains backlog. Release tooling contains strong OCI index/digest provenance rules, but deployment documentation still teaches `latest`, `staging-latest`, and tag-only publish/push flows, while production overlays do not uniformly bind workloads to a validated digest. Advice to use a SemVer or Git-SHA tag is insufficient under current AD-11 because tags are resolvable names, not deployment authority.

**Acceptance Criteria:**

**Given** AppHost publish output and Docker/Kubernetes/other supported production profiles are inventoried
**When** the image matrix is validated
**Then** every workload and sidecar names its repository, owning release/source, platform requirements, provenance authority, allowed staging identity, and required production digest
**And** EventStore, any externally released Admin/UI image, domain/sample images included in a production profile, DAPR sidecars/services, OpenBao, data stores, brokers, and other container dependencies are included or explicitly externalized with an immutable platform-owned contract.

**Given** an EventStore production workload is rendered
**When** its image reference is inspected
**Then** it uses `repository@sha256:<64 lowercase hex>` for the exact validated OCI image-index digest recorded by the canonical `ReleaseIdentity`
**And** `latest`, `staging-latest`, SemVer-only, Git-SHA-tag-only, branch, channel, local-image, child-manifest-only, config-digest, or unresolved variable references fail production validation.

**Given** an EventStore image digest is proposed
**When** provenance validation runs
**Then** retained raw registry bytes prove the index digest, exact `linux/amd64` and `linux/arm64` child/config chain, source repository/revision/version labels, package/release manifest, workflow/Builds authority, and both immutable-child health smokes against one unmodified `ReleaseIdentity`
**And** missing evidence, mixed lineage, alternate codec, tag re-resolution, nonconforming index shape, failed/rejected release, or an observed digest outside the recorded chain blocks promotion.

**Given** `eventstore-admin-ui` or another EventStore-produced image is added to a production profile
**When** release ownership is checked
**Then** it has an explicit manifest inventory entry, AD-22 identity mapping, conforming multi-platform release evidence, and validated index digest before the profile may require it
**And** AppHost/container resource identity or a project `ContainerRepository` property alone cannot be treated as a released registry artifact.

**Given** third-party runtime images are rendered
**When** immutable-identity validation runs
**Then** each production reference resolves to an approved repository digest compatible with the required platform and pinned component/runtime contract, with a documented update owner and verification source
**And** version-looking mutable tags such as `1.18.0`, `7-alpine`, or provider channels are not accepted as immutable solely because they are more specific than `latest`.

**Given** a production image variable is missing, empty, malformed, tag-only, digest-mismatched, unmapped, or points to a non-approved registry/repository
**When** render or promotion validation executes
**Then** deployment fails before workload mutation with a support-safe diagnostic naming the affected resource and evidence category
**And** no default tag, locally cached image, previous successful value, registry response alone, or operator boolean silently fills the missing authority.

**Given** production workloads have been deployed
**When** runtime conformance is inspected
**Then** each observed pod/task/container image ID resolves to the exact approved digest or recorded platform child within its approved index chain and deployment evidence binds environment, workload, digest, release identity, time, and authorized deployment record
**And** requested manifest text, controller status, and actual runtime identity discrepancies are detected rather than flattened into a green deployment state.

**Given** rollback is required
**When** an operator selects a prior version
**Then** rollback references a previously validated immutable digest and retained compatible configuration/migration posture, creates a new attributable deployment record, and verifies runtime identity after rollout
**And** it never retags, deletes, overwrites, or re-resolves a historical release tag or claims rollback safety when schema/downgrade evidence is absent.

**Given** deployment and getting-started documentation is validated
**When** production commands, environment samples, Helm/Kubernetes/Docker/ACA examples, and troubleshooting steps are scanned
**Then** production examples use digest-bound identities and explain the `ReleaseIdentity` verification/promotion/rollback flow, while mutable tags are labeled staging/development-only
**And** no copy-pastable production path publishes, pushes, promotes, or deploys `latest`, `staging-latest`, an unvalidated SHA tag, or rejected `v3.94.1`.

**Given** Story 7.9 completion is requested
**When** structured publish-output/overlay scans, release-evidence verification, negative identity cases, representative platform renders, runtime image-ID readback, and rollback rehearsal run
**Then** every production workload uses and runs its approved immutable identity with exact lineage, every invalid case blocks before mutation, and evidence distinguishes prepared, externally authorized, deployed, and verified states
**And** no real registry publication or deployment is performed without its separately bound durable authority; validators, documentation tests, and affected Release builds pass with no unexpected skips or warnings.

### Story 7.10: Integration CI Recovery

As a quality maintainer,
I want every integration suite assigned to a runnable CI lane,
So that high-risk behavior is not left as an undocumented local-only promise.

**Requirements coverage:** Primary ownership of FR34's meaningful IntegrationTests CI-coverage slice and NFR10's deterministic-versus-live lane separation; supporting NFR16 high-risk evidence visibility and NFR17 pinned infrastructure.

**Architecture constraints:** AD-9 and AD-12. Integration topology matches AppHost/deployment contracts, release-gate semantics remain distinct from live infrastructure signal, and high-risk outcomes retain inspectable artifacts rather than only a workflow conclusion.

**UX coverage:** No primary UX-DR ownership. Workflow/check summaries may expose suite, lane, product/infra/quarantine classification, duration, and artifact link only; they must not expose test secrets, bearer tokens, connection strings, raw payload fixtures, or support-unsafe logs.

**Dependencies:** Stories 5.6–5.9 for topology parity and Stories 7.6–7.8 for OpenBao, health, and resiliency infrastructure used by dedicated live lanes. Story 7.10 assigns runnable ownership; later Stories 7.11–7.13 strengthen evidence, classification, and advisory hygiene without reopening this lane contract.

**Current reconciliation:** Story 7.10 remains backlog. CI runs deterministic projects and a dedicated `Server.LiveSidecar.Tests` DAPR/Redis/PostgreSQL lane with OQ8 evidence, while advisory suites have a visible non-blocking workflow. The full `Hexalith.EventStore.IntegrationTests` Aspire project is still explicitly omitted, several test projects lack a single machine-checked lane owner, and workflow evidence does not yet provide a complete suite-to-lane inventory or consistent product-versus-infrastructure classification.

**Acceptance Criteria:**

**Given** every tracked test project, category/trait, external dependency, evidence validator, and workflow command is inventoried
**When** the CI ownership manifest is validated
**Then** each executable suite/subset has exactly one primary lane, owner, trigger, gate/advisory status, command, timeout, environment profile, dependency list, expected artifacts, and quarantine reference if applicable
**And** unowned, multiply owned, stale-path, non-executed, newly added, or permanently excluded projects fail inventory validation.

**Given** test dependencies are classified
**When** lanes are assigned
**Then** deterministic infra-free tests remain in the release-gate lane, live DAPR/state-store/broker/provider tests run in named dedicated lanes, full Aspire distributed-application tests run in a dedicated Aspire lane, and browser/advisory/performance work remains visibly separate
**And** a project name containing `Integration`, a fake DAPR client, or an in-memory substitute cannot by itself select a live lane or count as external-infrastructure proof.

**Given** the full `Hexalith.EventStore.IntegrationTests` project runs in CI
**When** its Aspire lane provisions the environment
**Then** pinned .NET, Aspire, DAPR CLI/runtime, Docker/provider images, root-declared dependencies, ports/endpoints, authentication inputs, AppHost profile, readiness waits, and test filters match the documented command and repository topology
**And** the lane does not rely on a developer machine, recursive/nested submodule initialization, unpinned latest tools, pre-existing containers, ambient credentials, or manual setup.

**Given** live-sidecar, OpenBao, broker, PostgreSQL/Redis, or full-Aspire setup starts
**When** setup, readiness, tests, or teardown fail
**Then** the workflow records a bounded phase-specific `infrastructure-blocked`, `product-failed`, `test-contract-failed`, `cancelled`, or `passed` classification and preserves the original failing command/exit status
**And** infrastructure failure never becomes a product pass/fail, product failure is not dismissed as flaky infrastructure, and an unrun platform remains unproven.

**Given** a lane executes on pull request, main push, scheduled/manual trigger, retry, or cancellation
**When** concurrency and timeout behavior is evaluated
**Then** triggers, path/branch rules, concurrency groups, maximum durations, cancellation handling, retry ownership, and release-gate consequences match the manifest
**And** cancellation cannot leave sidecars, containers, networks, temp credentials/files, ports, or evidence writers active on self-hosted or reusable runners.

**Given** test execution completes or fails
**When** result publication runs
**Then** TRX/CTRF, coverage where required, environment/version manifest, phase classification, skip/quarantine report, and approved high-risk evidence are uploaded under stable names with retention and `if: always()` behavior
**And** absence of required files fails publication, while artifact redaction scans reject tokens, connection strings, real tenant/payload data, provider credentials, and stack traces outside approved test diagnostics.

**Given** a suite cannot yet run reliably
**When** it is quarantined instead of silently omitted
**Then** a tracked entry names exact project/filter, owner, reason, dependency/blocker, created date, expiry/review date, expected lane, and removal condition and the workflow reports it visibly
**And** an inline comment, `continue-on-error`, broad filter, empty test discovery, or permanent `[Fact(Skip)]` without the tracked entry cannot satisfy ownership.

**Given** workflow supply-chain and permission posture is validated
**When** actions, reusable workflows, scripts, secrets, and checkout/submodule steps are inspected
**Then** external actions are SHA-pinned, permissions are least privilege, untrusted pull requests receive no protected secrets, root submodules are initialized only as required, and all commands use the repository's pinned solution/configuration/source-mode contract
**And** a branch-floating reusable workflow or action cannot be a release-gate authority without the separately approved repository policy.

**Given** Story 7.10 completion is requested
**When** ownership-manifest validation, workflow syntax/static checks, deterministic lane, live-sidecar lane, full-Aspire lane, provider-specific lanes, failure-classification fixtures, artifact/redaction checks, and cleanup assertions run
**Then** every integration suite is executed or explicitly dated/quarantined, the full Aspire project has recorded runnable evidence, and deterministic release gates remain separated from live infrastructure outcomes
**And** one retained green or correctly classified blocked/failed run per required lane records exact run/revision/tool identities with no hidden exclusions or leaked processes.

### Story 7.11: Persisted-State Evidence And Read-Back Helpers

As an integration-test author,
I want shared read-back helpers and mandatory persisted end-state evidence,
So that success cannot be inferred only from HTTP status, polling text, or mock calls.

**Requirements coverage:** Primary ownership of NFR16's shared persisted-evidence/readback slice; supporting FR34 IntegrationTests recovery and NFR7 no-silent-loss verification.

**Architecture constraints:** AD-6, AD-7, AD-8, and AD-12. Helpers preserve stable identity and projection scope, distinguish freshness signals from durable truth, and expose evidence for assertions without replacing the production path or hiding provider assumptions.

**UX coverage:** No primary UX-DR ownership. These helpers verify the authoritative command/projection/recovery/audit evidence later consumed by Story 7.19; screenshots, rendered text, `202`, SignalR, and UI state alone are not persisted evidence.

**Dependencies:** Story 7.10 for runnable deterministic, live-sidecar, and Aspire lanes in which the helpers and migrated scenarios execute.

**Current reconciliation:** Story 7.11 remains backlog. `Hexalith.EventStore.Testing.Integration` provides topology fixtures, DAPR prerequisites, and benchmark helpers, while individual integration tests perform ad hoc Redis/DAPR reads. No shared typed read-back surface or closed scenario-to-evidence matrix currently requires consistent detail/index/marker/lifecycle/checkpoint/publication/audit assertions across high-risk tests.

**Acceptance Criteria:**

**Given** high-risk command, query/projection, delivery, erasure, read-model batch, idempotency, rebuild, publication recovery, poison recovery, Admin audit, secret readiness, and deployment scenarios are inventoried
**When** the evidence matrix is validated
**Then** each scenario names exact authoritative persisted records, expected identities/versions/states, absence proofs, provider/profile, ordering/atomicity boundary, timeout, and owning test/lane
**And** every row enumerates required event, metadata, status/archive, detail, index, batch marker/receipt, lifecycle, checkpoint, retry/recovery, CloudEvent, audit, or runtime evidence explicitly—never “where applicable.”

**Given** multiple suites need platform-state inspection
**When** helpers are added to `Hexalith.EventStore.Testing.Integration`
**Then** typed asynchronous seams can read EventStore event/metadata/status, projection detail/index/batch/lifecycle/checkpoint, publication/dead-letter, Admin audit, DAPR component, and CloudEvent evidence through their production storage/broker boundaries
**And** each call requires explicit tenant/domain/aggregate/projection/operation identity as applicable and returns value, presence, version/schema, ETag only as private concurrency evidence, provider/profile, and observation time without silently constructing broad or cross-tenant keys.

**Given** provider-portable behavior is under test
**When** state is read back
**Then** helpers prefer DAPR/platform contracts and keep backend-specific Redis/PostgreSQL/broker adapters behind an explicit capability/profile selected by the test
**And** direct Redis key/hash assumptions cannot satisfy a provider-neutral claim, while a provider-specific conformance test names and records that dependency rather than hiding it.

**Given** eventual consistency or asynchronous recovery must settle
**When** a helper waits for evidence
**Then** it uses a bounded cancellable polling contract with explicit target predicate, timeout, interval/backoff, maximum observations, accepted transient states, and captured last observation
**And** it uses no unbounded loop or arbitrary fixed sleep, does not swallow provider/cancellation failures, and cannot turn timeout/unknown into empty/default success.

**Given** a test asserts a successful mutation or delivery
**When** read-back completes
**Then** the test—not the helper—asserts exact persisted business output plus every required marker, lifecycle, checkpoint, receipt, publication, and audit relationship from the matrix
**And** an HTTP response, mock invocation, log line, generated operation id, UI toast, notification, or one state row cannot substitute for coordinated end-state proof.

**Given** a denied, conflicting, expired, corrupt, unreadable, cancelled, unavailable, or unsafe legacy path is tested
**When** negative evidence is captured
**Then** the helper supports bounded absence/non-advancement assertions for events, state, handlers, batches, markers, checkpoints, audit intent, and downstream work together with the expected durable failure/reconciliation record
**And** “not found” is accepted only after tenant-safe authoritative inspection and cannot be inferred from denial, timeout, malformed response, or an unavailable provider.

**Given** concurrent or crash-recovery behavior is tested
**When** before/after evidence is collected
**Then** stable operation/message/fence identities correlate all observations, captured versions prove monotonic transitions, and final state can be compared with the single-writer canonical baseline
**And** helpers do not reuse state between tests, hide race windows, normalize conflicting states, or overwrite the evidence that demonstrates the failure.

**Given** read-back diagnostics or artifacts are emitted
**When** values are formatted
**Then** output is bounded to safe identifiers, schemas, counts, state names, versions, timestamps, and hashes explicitly permitted by the evidence profile
**And** event/read-model payloads, protected bytes, raw state keys, tokens, secret values, decoded claims, connection strings, cursors, ETags, and provider exception bodies are redacted or retained only in access-controlled test-internal memory.

**Given** existing integration scenarios migrate
**When** helper adoption and compatibility are reviewed
**Then** duplicated ad hoc polling/key decoding is removed where the shared contract applies, intentional provider-specific reads remain documented, and public testing-package changes are additive and warnings-as-errors clean
**And** migration does not weaken assertions, replace production stores with fakes, or make tests pass by broadening timeouts or accepting more terminal states.

**Given** Story 7.11 completion is requested
**When** helper unit/contract tests, tenant/provider/timeout/cancellation/redaction negative tests, and every matrix scenario run in its Story 7.10 lane
**Then** persisted end-state or non-mutation evidence—not transport/mocks alone—proves each outcome and exact cross-record invariants hold through production paths
**And** the Testing.Integration package, migrated suites, Release build, and live/Aspire lanes pass with no unexpected skips, warnings, evidence leakage, or hidden eventual-consistency waits.

### Story 7.12: Fake And Integration Test Reclassification

As a quality maintainer,
I want test labels to reflect actual external dependencies and proof boundaries,
So that fake-only evidence cannot be mistaken for integration or production-path proof.

**Requirements coverage:** Primary ownership of NFR10's test-tier/classification accuracy slice; supporting FR34 CI recovery and NFR16 evidence integrity.

**Architecture constraints:** AD-9 and AD-12. A claimed topology/provider boundary must actually run, and test classification is derived from observed process/service/store boundaries rather than project names or aspirational comments.

**UX coverage:** No primary UX-DR ownership. This story affects quality evidence only; fake component/page tests may prove rendering contracts but never authoritative persisted UX evidence or real browser/host behavior.

**Dependencies:** Stories 7.10 and 7.11 for the canonical lane inventory and shared production-path read-back seams used by retained live tests.

**Current reconciliation:** Story 7.12 remains backlog. `Hexalith.EventStore.Testing.Integration.Tests` mostly tests fixtures/builders with recording substitutes and already runs in the deterministic CI list, while `Hexalith.EventStore.IntegrationTests` mixes in-memory/WebApplicationFactory tests with full Aspire, Redis, Keycloak, DAPR, and spawned-process proofs. Names, locations, and comments do not consistently communicate the boundary each test actually exercises.

**Acceptance Criteria:**

**Given** every test project, class, collection, category/trait, fixture, process, network endpoint, state store, broker, provider, and substitute is inventoried
**When** classification runs
**Then** each test declares one primary tier—unit, component/contract, host-pipeline, live-sidecar, full-Aspire, provider-conformance, browser E2E, advisory, or performance—and the exact boundary/evidence it proves
**And** classification is machine-checkable against project references, fixtures, traits, and Story 7.10 lane ownership rather than inferred only from folder/project names.

**Given** a test uses only mocks, substitutes, recording handlers, in-memory stores, synthetic clocks, or direct class calls
**When** it is reviewed
**Then** it moves to or is explicitly classified as deterministic unit/component/contract scope and its description states what it does not prove
**And** `Integration`, `E2E`, `Live`, provider names, or production-evidence claims are removed unless the corresponding external boundary actually runs.

**Given** a test claims host-pipeline integration
**When** its fixture executes
**Then** the real configured ASP.NET middleware/routing/auth/serialization pipeline runs in process, while each substituted external dependency is named and excluded from the claim
**And** a test-auth handler or in-memory state store does not invalidate the host-pipeline claim but cannot become OIDC, DAPR, provider, durability, restart, or cross-host proof.

**Given** a test remains in live-sidecar, full-Aspire, provider-conformance, or browser E2E scope
**When** its evidence is inspected
**Then** it starts/connects to the named real DAPR sidecar, AppHost resource graph, state store, broker, OpenBao/provider, browser, or process boundary and asserts the required production-path evidence from Story 7.11
**And** a fake client, mocked HTTP call, parsed YAML, generated manifest, or in-memory adapter alone cannot satisfy that tier.

**Given** one class contains cases from different tiers
**When** reclassification is applied
**Then** tests are split into the appropriate projects/collections or carry exact non-overlapping traits and fixtures that route them to one primary lane each
**And** no broad filter causes live tests to run accidentally in deterministic CI, fake tests to inflate live coverage, or one test to execute twice under conflicting gate semantics.

**Given** files/projects are moved or renamed
**When** repository and workflow updates run
**Then** namespaces, assembly attributes, project references, solution membership, coverage settings, test filters, result paths, documentation, and ownership manifest remain consistent
**And** tests, fixtures, assertions, or failure history are not silently deleted, disabled, duplicated, or weakened during mechanical relocation.

**Given** a test has an unavailable external prerequisite
**When** discovery/execution occurs
**Then** the owning live lane reports the Story 7.10 infrastructure/quarantine classification with exact prerequisite and expiry rather than allowing a runtime self-skip to masquerade as a passing test
**And** deterministic tests remain runnable without Docker/DAPR/provider/browser prerequisites and never probe developer-local ambient services.

**Given** classification drift is introduced
**When** CI inventory validation scans new/changed tests
**Then** missing tier/lane ownership, forbidden live dependency in deterministic scope, substitute-only live claim, untracked skip, stale path, and empty discovered test set fail with an actionable project/test identifier
**And** exception rules are explicit, owner-bound, dated, and do not relax NFR16 evidence requirements.

**Given** Story 7.12 completion is requested
**When** the full test inventory, classification validator, deterministic lane, host-pipeline suites, every live/provider/browser lane, and representative fake-versus-real negative fixtures run
**Then** each test executes exactly once in the correct lane, reported tier and actual dependencies agree, and all production-path claims cite real persisted/topology/browser evidence
**And** solution/Release builds and test discovery pass with no orphaned projects, hidden tests, unexpected skips, or fake-only integration claims.

### Story 7.13: Advisory And Performance Workflow Hygiene

As a quality maintainer,
I want advisory and performance work to remain runnable, visible, and owner-bound,
So that non-release-blocking evidence cannot quietly decay into permanent green-looking skips.

**Requirements coverage:** Primary ownership of NFR10's advisory/performance accountability slice; supporting FR34 operational-test recovery and NFR16 evidence integrity.

**Architecture constraints:** AD-9 and AD-12. Advisory signal remains explicitly distinct from release authority, workflow outcomes preserve exact suite status, and quantitative performance claims require a declared workload, environment, samples, and approved baseline rather than an invented threshold.

**UX coverage:** No primary UX-DR ownership. Browser and future UI-performance jobs may support later Admin conformance, but this story neither defines Admin interactions nor creates a numerical UX release budget without measured production evidence.

**Dependencies:** Stories 7.10–7.12 for canonical lane ownership, persisted evidence, and honest test-tier classification.

**Current reconciliation:** Story 7.13 remains backlog. `.github/workflows/advisory-tests.yml` runs on pull requests, main pushes, and manual dispatch with pinned browser setup, bounded timeout, result upload, and an explicitly non-release-blocking purpose. Its three projects execute inside one globally `continue-on-error` job, many governance/evidence/browser tests are intentionally skipped red-phase scaffolds, and there is no closed green-or-quarantine ledger or dedicated performance workflow. Benchmark dataset helpers and opt-in DAPR performance prerequisites exist, while the planning authority explicitly defers quantitative UI budgets until a measured production baseline exists.

**Acceptance Criteria:**

**Given** advisory, browser, governance, operational-evidence, benchmark, load, and performance projects, filters, traits, scripts, and workflow jobs are inventoried
**When** the advisory/performance ownership manifest is validated
**Then** every executable scope has one owner, purpose, tier, trigger, command, timeout, tool/browser/infrastructure prerequisites, expected artifacts, gate/advisory semantics, and either a retained green-evidence requirement or tracked quarantine reference
**And** an unowned project, stale path, duplicate lane, empty discovery set, undocumented opt-in environment variable, or job that exists only in comments fails validation.

**Given** an advisory or performance workflow is invoked
**When** its environment is prepared
**Then** it has a documented runnable manual trigger plus appropriate pull-request, main, or scheduled coverage; bounded concurrency and timeout; pinned .NET/browser/action/tool identities; and explicit dependency readiness
**And** it does not depend on ambient developer services, mutable latest tools, protected secrets on untrusted changes, nested submodule initialization, or an undocumented local command.

**Given** multiple advisory suites execute in one workflow
**When** one passes, fails, skips, times out, is cancelled, or is infrastructure-blocked
**Then** each suite's exact command, discovery count, executed/passed/failed/skipped totals, duration, exit status, and classification are retained independently and the workflow summary exposes the aggregate without losing the original failure
**And** job-level `continue-on-error`, shell aggregation, a green check icon, or successful artifact upload cannot relabel a failed, empty, skipped, or unrun suite as passed.

**Given** a test, suite, job, or performance claim is not currently green and runnable
**When** it remains advisory or is quarantined
**Then** a tracked entry records exact scope, owner, reason, created date, review/expiry date, prerequisite or defect, expected evidence, target lane, and objective removal condition
**And** permanent `[Fact(Skip)]`, a broad exclusion filter, issue-free prose, expired quarantine, or global `continue-on-error` cannot satisfy that accountability contract.

**Given** skip and discovery governance runs
**When** source, TRX, and the ownership manifest are compared
**Then** every intentional skip maps to one unexpired quarantine entry, every expected test is discovered in its declared lane, and resolved scaffolds have their skip/quarantine removed in the same change
**And** a suite containing only skipped tests, zero executed cases, or newly added untracked skips reports a non-passing advisory outcome even when the test runner exits successfully.

**Given** a benchmark, load, latency, throughput, or UI-performance job is introduced or retained
**When** it publishes a result
**Then** evidence records revision, workload/dataset and seed, topology/provider, resource shape, tool versions, warm-up, sample/window rules, retry/error treatment, raw measurements, clock method, and statistical calculation sufficient to reproduce the stated claim
**And** it reports path viability or sample-only evidence when those are all that ran; it cannot claim an NFR, regression, or release threshold from a synthetic default, a developer machine, under-powered samples, omitted errors, or an unapproved baseline.

**Given** no measured production baseline supports a quantitative UI-performance budget
**When** advisory policy and Admin browser checks are evaluated
**Then** the absence remains an explicit dated follow-up with an owner and baseline-establishment condition, while qualitative loading, responsiveness, accessibility, and support-safety contracts remain enforceable
**And** no arbitrary millisecond, bundle-size, Lighthouse, or throughput number becomes a release gate or acceptance threshold merely to close this story.

**Given** advisory status is intentionally non-release-blocking
**When** workflow checks and release dependencies are inspected
**Then** names, summaries, documentation, and branch/release wiring consistently label that status and surface failures to maintainers without blocking the release gate
**And** promoting any advisory result to release authority requires an explicit policy change, stable green evidence, approved ownership, and corresponding release-workflow validation rather than an incidental job-name or dependency edit.

**Given** a workflow finishes under any outcome
**When** evidence publication and cleanup execute
**Then** stable per-suite results, coverage where required, summaries, environment manifests, raw performance samples where applicable, and quarantine status upload with `if: always()` behavior and bounded retention, while temporary processes and infrastructure are stopped
**And** required-artifact absence, secret/token/connection-string leakage, unsafe payload capture, orphaned services, or inaccessible evidence is reported as an explicit evidence or infrastructure failure.

**Given** an advisory project or workflow is removed, merged, renamed, or replaced
**When** the ownership manifest changes
**Then** the same change records its successor or justified retirement, migrates or disposes every test/quarantine/evidence obligation, and updates solution, workflow, documentation, and artifact references
**And** removal cannot be used to make unresolved red-phase, accessibility, governance, evidence, or performance work disappear from status reporting.

**Given** Story 7.13 completion is requested
**When** manifest validation, workflow static checks, manual-trigger rehearsal, per-suite pass/fail/skip/empty/cancelled fixtures, quarantine-expiry checks, artifact/redaction validation, and one representative benchmark or explicit no-performance-job disposition run
**Then** every advisory/performance scope is runnable or transparently quarantined, exact outcomes remain visible despite non-blocking semantics, and any quantitative claim is reproducible and baseline-authorized
**And** retained run evidence identifies the exact revision and toolchain with no hidden exclusions, invented UI budget, leaked data, or advisory result masquerading as a release gate.

### Story 7.14: Admin Shell And Canonical Route Migration

As an EventStore operator,
I want the existing Admin UI to become one canonical dashboard shell,
So that operational navigation and legacy deep links remain coherent without creating another host.

**Requirements coverage:** Primary ownership of FR34's Admin shell/navigation slice, NFR14, and UX-DR1–UX-DR5 plus UX-DR23; supporting FR13's typed-client UI boundary.

**Architecture constraints:** AD-11 and AD-21. `Admin.UI` remains the single UI host, FrontComposer dependencies use one catalog-governed family version in both source and package modes, and the UI remains a typed-client consumer with no per-message MVC command/query controllers.

**UX coverage:** Primary ownership of the in-place host, single FrontComposer module entry, ten-tab information architecture, legacy-route migration, FrontComposer/Fluent composition, and optional command-palette boundary in UX-DR1–UX-DR5 and UX-DR23. Story 7.19 owns operational content/evidence interactions, while Story 7.20 owns theme, accessibility, localization, and responsive conformance.

**Dependencies:** Stories 7.4 and 7.5 for honest deferred surfaces and the shared typed Admin client consumed by the consolidated host.

**Current reconciliation:** Story 7.14 remains backlog. `src/Hexalith.EventStore.Admin.UI` already owns resource/container identity `eventstore-admin-ui`, Fluent UI V5 pages, an optional command palette, and 22 route templates, but exposes feature-by-feature left navigation rather than one ten-tab dashboard and references neither FrontComposer Shell nor Contracts.UI. The imported Builds catalog currently sets `HexalithFrontComposerVersion` to `4.1.1`, pins Shell but has no `Hexalith.FrontComposer.Contracts.UI` entry; that dated value is evidence, not a locally frozen version.

**Acceptance Criteria:**

**Given** project, solution, deployment, AppHost, DAPR, container, and route ownership are inspected
**When** the shell migration is applied
**Then** `src/Hexalith.EventStore.Admin.UI` evolves in place and retains its assembly, service, resource, DAPR, and container identity `eventstore-admin-ui`
**And** no second EventStore UI executable, host resource, container, module registration, router, or duplicate feature-page implementation is created.

**Given** the UI composes the host shell
**When** Debug/source and Release/package dependency graphs restore and build
**Then** `Hexalith.FrontComposer.Shell` and `Hexalith.FrontComposer.Contracts.UI` resolve as one compatible family from the Builds catalog's single `HexalithFrontComposerVersion`, with Contracts.UI first added to that catalog under the same variable when absent, and Fluent UI Blazor V5 remains the component system
**And** no consuming project supplies a local FrontComposer version, mixed family version, direct DLL path, branch-floating package, hidden transitive substitute, or source/package boundary mismatch.

**Given** FrontComposer discovers EventStore navigation
**When** the host/module registry is evaluated
**Then** exactly one module with stable id `event-store-admin` and label **Event Store Admin** opens the existing Admin UI dashboard and remains selected for every child tab and detail route
**And** Commands, Streams, DAPR, Storage, Tenants, or any other EventStore feature does not register a competing host-level module entry.

**Given** the canonical information architecture is rendered
**When** an authorized operator opens the module
**Then** one URL-addressable, keyboard-operable `FluentTabs` set owns exactly Overview, Commands, Streams & Events, Projections, Tenants & Access, Topology, Storage & Snapshots, Recovery, Deferred & Backlog, and Settings in the approved order
**And** selecting, reloading, bookmarking, using browser back/forward, or opening a permitted deep link preserves the active tab and relevant bounded route/query state without creating a nested second navigation system.

**Given** route migration begins
**When** the machine-validated route manifest is inspected
**Then** it assigns exactly one canonical tab/detail target and parameter/query/fragment policy to `/`, `/commands`, `/streams`, `/streams/{tenant}/{domain}/{aggregate}`, `/events`, `/projections`, `/tenants`, `/dapr`, `/dapr/actors`, `/dapr/pubsub`, `/dapr/resiliency`, `/dapr/health-history`, `/services`, `/storage`, `/snapshots`, `/health`, `/health/dead-letters`, `/consistency`, `/backups`, `/compaction`, and `/settings`
**And** every existing route is either the one canonical implementation or an explicit compatibility redirect; `/backups` resolves to Deferred & Backlog or the exact unsupported state established by Story 7.4.

**Given** a canonical or compatibility URL is requested
**When** routing resolves it
**Then** the owning dashboard tab or detail surface renders under the selected `event-store-admin` module, authorized tenant/domain/aggregate and safe filters are preserved according to the manifest, and browser history reaches the canonical URL without a redirect loop
**And** malformed, unknown, denied, or cross-tenant parameters fail closed without disclosing resource existence, raw values, or a stale legacy page.

**Given** legacy page components contain reusable feature content
**When** they are migrated behind the dashboard
**Then** shared content is extracted or hosted once under its owning canonical route and legacy entry points delegate or redirect to that owner
**And** the migration does not fork API clients, authorization, polling/SignalR state, mutation behavior, page models, or tests between old and new implementations.

**Given** the dashboard uses the shared Admin transport
**When** shell, tab, detail, or utility code requests data or submits an operation
**Then** it consumes Story 7.5's typed client facets through one composition root and preserves gateway/Admin policy boundaries
**And** the Razor host contains no generated or hand-written per-message MVC controller, direct handler/actor/state-store call, raw DAPR invocation, locally reconstructed endpoint, or duplicate ProblemDetails parser.

**Given** the optional command palette is retained
**When** it opens, searches, and activates a result
**Then** it uses Fluent dialog/input/list primitives, supplements visible tabs and controls, filters entries by current tenant and role, keeps the module/tab selection coherent, and returns focus safely
**And** it does not reveal inaccessible resource names, bypass authorization, replace primary navigation, or expose a runnable entry for any Story 7.4 deferred operation; if omitted, no dormant shortcut or undocumented key binding remains.

**Given** shell loading or routing fails
**When** an operator encounters unavailable FrontComposer composition, an unknown route, denied scope, or a failed tab load
**Then** the single host renders an accessible support-safe state with bounded recovery/navigation choices and retains no false selected feature or stale authorization state
**And** exception bodies, claims, tokens, tenant inventories, raw route values, internal endpoints, and component stack traces are not disclosed.

**Given** Story 7.14 completion is requested
**When** Admin.UI host/component/route tests, every manifest route and redirect case, browser history/deep-link checks, tenant/role and command-palette negatives, AppHost resource/container tests, FrontComposer Debug/source and Release/package-mode checks, and duplicate-host/page/controller scans run
**Then** one `eventstore-admin-ui` host exposes one selected `event-store-admin` module and all approved routes resolve to exactly one ten-tab dashboard implementation
**And** solution restore/build and focused browser tests pass with the catalog-authoritative FrontComposer family, Fluent UI V5, no broken legacy links, duplicate pages, unexpected skips, or warnings.

### Story 7.15: GDPR Aggregate Erasure Backlog

As a product owner,
I want GDPR aggregate erasure and tombstoning tracked independently,
So that legal retention, crypto-shred, replay, backup, and audit questions are not hidden in projection cleanup.

**Requirements coverage:** Primary ownership of FR35's GDPR aggregate-erasure planning slice.

**Architecture constraints:** AD-5 and AD-13. This is a specification/backlog artifact only: EventStore append-only history, projection cleanup, crypto-shredding, and legal erasure remain distinct boundaries, and no runtime mutation is authorized by completing this story.

**UX coverage:** No primary UX-DR ownership. Story 7.4 owns the disabled/unsupported Admin presentation; this artifact may define future operator evidence and denial needs but creates no runnable GDPR control.

**Dependencies:** Story 1.14's generic projection/read-model erasure boundary and Story 7.6's future secret/key-management posture are inputs, not proof that aggregate erasure is implemented. Legal/product retention authority is an explicit unresolved dependency.

**Current reconciliation:** The planned output `_bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md` exists and the sprint ledger marks Story 7.15 done as planning work, but its front matter still says `status: draft`. It already separates aggregate erasure from Story 1.14 and lists broad scope, non-goals, dependencies, risks, and validation expectations; completion must reconcile artifact status and add enough decision/evidence structure for a future implementation story without changing runtime behavior.

**Acceptance Criteria:**

**Given** the GDPR-1 artifact is reviewed
**When** scope completeness is validated
**Then** it separately inventories write-model streams/events, metadata, snapshots, projection detail/index/checkpoints, brokered events and dead letters, backups/restores, caches/search exports, logs/telemetry, audit records, encryption keys, and replicas or derived copies
**And** each category states whether the future question is erase, tombstone, redact, retain, crypto-shred, rebuild, or explicitly unresolved rather than using one generic deletion promise.

**Given** legal and product authority is not encoded in this repository
**When** retention, legal hold, data-subject verification, jurisdiction, audit retention, backup expiry, and proof-of-erasure decisions are recorded
**Then** the artifact names the required decision owner, question, prerequisite, evidence, and approval gate for each unresolved policy
**And** it does not invent a retention period, claim legal compliance, or allow repository-story approval to substitute for legal/product authorization.

**Given** EventStore history is append-only
**When** future tombstone and crypto-shred alternatives are compared
**Then** the artifact defines aggregate identity/scope, tenant isolation, key hierarchy and blast radius, replay/query behavior, snapshot/read-model regeneration, duplicate/idempotent request behavior, failure recovery, and what remains observable after each alternative
**And** it forbids rewriting event history, cross-tenant discovery, or treating deletion of a projection row, encryption key, current snapshot, or broker message as complete aggregate erasure.

**Given** backups, downstream publications, and audit evidence may outlive online state
**When** lifecycle and verification requirements are specified
**Then** the artifact identifies restore-time re-erasure/tombstone rules, retention/expiry interactions, downstream owner boundaries, immutable audit minimization, reconciliation states, and evidence sufficient to prove completed, partial, blocked, failed, or not-applicable disposition
**And** a successful API response, UI message, primary-store absence, or key-deletion request alone cannot prove end-to-end erasure.

**Given** a future implementation is proposed
**When** its readiness gate is evaluated
**Then** it requires an approved dedicated specification and story set covering identity/authorization, legal policy, state matrix, concurrency/idempotency, crash recovery, provider portability, migration/rollback, security/privacy threat analysis, and persisted high-tier tests
**And** this backlog artifact, Story 1.14 completion, Story 7.15 completion, or an Admin placeholder grants no authority to implement or expose runtime erasure.

**Given** Story 7.15 planning completion is asserted
**When** `_bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md`, its metadata, source-story link, and sprint ledger are checked independently
**Then** scope, non-goals, dependencies, risks, decision owners/gates, validation expectations, and the explicit no-runtime-authority statement are present and internally consistent, with `source_story: 7.15`
**And** artifact and ledger statuses no longer contradict one another, all referenced paths/requirements resolve, and no code, UI action, data deletion, secret rotation, deployment, or external approval is performed by this planning story.

### Story 7.16: Admin Interactive OIDC Backlog

As a product owner,
I want Admin interactive OIDC login tracked independently,
So that authorization-code/PKCE, forwarded user identity, claim normalization, and session UX remain separate from immediate auth guards.

**Requirements coverage:** Primary ownership of FR35's Admin interactive OIDC planning slice.

**Architecture constraints:** AD-3, AD-10, and AD-13. This story defines a future identity boundary only; current application-layer authentication, tenant authorization, shared claims normalization, and attributable audit remain mandatory, and no login runtime or identity-provider change is authorized.

**UX coverage:** No primary UX-DR ownership. UX-DR22's unavailable-operation treatment remains owned by Story 7.4. This artifact records future login, denial, expiry, and reauthentication UX requirements without enabling a sign-in button or presenting OIDC as available.

**Dependencies:** Stories 5.3, 7.2, and 7.3 for current production auth guards, shared claims normalization, and attributable Admin mutation audit. Identity-provider ownership, client registration, callback/origin policy, and session requirements remain separately approved external dependencies.

**Current reconciliation:** The planned output `_bmad-output/planning-artifacts/backlog/iam-1-admin-oidc-login.md` exists and the sprint ledger marks Story 7.16 done as planning work, but its front matter remains `status: draft`. It already names authorization-code with PKCE, user-token forwarding, gateway-aligned claims, key risks, and validation themes; completion must reconcile status and turn those themes into a decision-ready future boundary without implementing login.

**Acceptance Criteria:**

**Given** the IAM-1 artifact is reviewed
**When** future authentication flow scope is validated
**Then** it defines authorization-code with PKCE initiation, state/nonce and correlation protection, approved redirect and post-logout URIs, callback validation, code exchange, issuer/audience/signature/time validation, logout, session expiry, reauthentication, cancellation, and provider-unavailable outcomes
**And** it explicitly forbids ROPC, implicit flow, self-minted end-user tokens, tokens in URLs/logs/browser storage not approved by the threat model, and service identity masquerading as a human operator.

**Given** Admin.UI calls Admin.Server through the platform boundary
**When** identity propagation is specified
**Then** the artifact distinguishes browser session, attributable end-user access token, Admin.UI service identity, DAPR API token, and downstream service identity; defines which component may hold/forward each credential; and preserves the authenticated user/tenant/permission context required by Stories 7.2 and 7.3
**And** no forwarded caller header, UI role flag, service credential, or infrastructure scope can synthesize or broaden user authority.

**Given** claims differ across identity providers
**When** normalization and authorization are designed
**Then** the artifact consumes Story 7.2's canonical subject, tenant, permission, and role contract, defines missing/malformed/duplicate/conflicting claim handling, and requires current authorization on every protected route and mutation disposition
**And** login success alone grants no tenant, Admin role, resource visibility, or mutation permission.

**Given** an unauthenticated, expired, revoked, denied, wrong-tenant, consent-failed, callback-error, or provider-unavailable state occurs
**When** future Admin UX behavior is specified
**Then** the artifact defines accessible support-safe messaging, focus destination, bounded retry/reauthentication, preservation or clearing of safe route intent, session data disposal, and non-disclosing denial behavior
**And** it does not reveal tenant/resource existence, claim contents, tokens, authorization URLs containing sensitive data, provider exception bodies, or whether another identity would have access.

**Given** a state-mutating operation crosses authentication or token refresh boundaries
**When** audit and replay behavior is specified
**Then** the future design binds subject/session/tenant/permission, operation identity, token authentication time where policy requires it, intent, terminal disposition, and correlation without persisting raw tokens or unnecessary claims
**And** silent token renewal, browser retry, callback replay, or service restart cannot duplicate a mutation or detach it from the attributable user.

**Given** a future implementation story is proposed
**When** its readiness is assessed
**Then** it requires approved identity-provider/client-registration authority, threat model, credential/cookie storage policy, CSRF/XSS/open-redirect controls, key rotation and clock-skew rules, deployment secret contract, logout/revocation limitations, accessibility/localization design, migration/rollback, and host/browser/integration test plan
**And** this artifact, Story 7.16 completion, a development token provider, or current bearer authentication does not authorize production interactive OIDC.

**Given** Story 7.16 planning completion is asserted
**When** `_bmad-output/planning-artifacts/backlog/iam-1-admin-oidc-login.md`, its metadata, source-story link, and sprint ledger are independently validated
**Then** scope, non-goals, dependencies, risks, identity/credential boundaries, decision owners/gates, UX states, validation expectations, and explicit no-runtime-authority statement are present and internally consistent, with `source_story: 7.16`
**And** artifact and ledger statuses no longer contradict one another, referenced requirements resolve, and no client registration, provider configuration, secret, callback, cookie, login UI, or deployed authentication behavior is created or changed.

### Story 7.17: Aggregate Test-Kit Backlog

As a product owner,
I want the aggregate test kit tracked independently,
So that fixture ergonomics, replay determinism, idempotency, rejection, and package dependencies receive focused design.

**Requirements coverage:** Primary ownership of FR35's aggregate test-kit planning slice.

**Architecture constraints:** AD-2, AD-6, AD-12, and AD-13. A future kit must remain domain-centric and lightweight, preserve EventStore identity/serialization semantics, and state honestly which deterministic behavior it proves versus which production path requires higher-tier evidence.

**UX coverage:** No direct UX requirement applies. This is developer testing ergonomics only and creates no Admin surface or operator workflow.

**Dependencies:** Stable domain-service SDK and aggregate-discovery contracts, testing-package split/compatibility decisions, and separate event-versioning/upcasting specifications if mixed historical versions enter fixture scope.

**Current reconciliation:** The planned output `_bmad-output/planning-artifacts/backlog/kit-1-aggregate-test-kit.md` exists and the sprint ledger marks Story 7.17 done as planning work, but its front matter remains `status: draft`. It already identifies `Given(events).When(command).Then(events)`, replay/idempotency/rejection/metadata goals, lightweight package boundaries, risks, and validation themes; completion must reconcile status and define a future consumable contract without adding the package or runtime helpers.

**Acceptance Criteria:**

**Given** the KIT-1 artifact is reviewed
**When** the future public fixture contract is specified
**Then** it defines typed setup, command execution, expected emitted events, rejection/no-event outcomes, state assertions, metadata/identity assertions, cancellation, deterministic clock/id providers, and readable failure diagnostics for a `Given(events).When(command).Then(events)` workflow
**And** it distinguishes proposed API shape from approved compatibility surface and does not force domain tests to parse platform persistence records or host a Server process.

**Given** historical events initialize an aggregate
**When** replay semantics are designed
**Then** the artifact defines event ordering, version/sequence continuity, handler discovery, unsupported event/handler behavior, replay determinism, Apply-side-effect expectations, duplicate-input handling, and optional snapshot/versioning boundaries
**And** it does not silently ignore unknown events, reorder input, synthesize missing identity, accept non-deterministic replay, or claim upcasting behavior before its separate specification is approved.

**Given** a command is exercised
**When** emitted events or rejection are evaluated
**Then** the artifact defines how tenant/domain/aggregate, message, correlation/causation, sequence, timestamp, command result, rejection type, and corrective action are supplied or asserted under stable EventStore contracts
**And** a fixture-generated default cannot conceal malformed identity, duplicate events, incorrect metadata, an unsupported command shape, or a domain rejection represented as infrastructure failure.

**Given** package placement is selected
**When** dependency and target-framework graphs are validated
**Then** the artifact names the intended package/project owner, public-versus-internal surface, source/package-mode behavior, target frameworks, and allowed dependency direction so a domain module can consume the kit without `Hexalith.EventStore.Server`, AppHost, DAPR runtime, provider, or Admin dependencies
**And** common test-framework adapters remain optional or isolated so the core fixture does not force one runner/assertion library or make production assemblies depend on test packages.

**Given** the deterministic kit is used successfully
**When** its proof claim is reported
**Then** documentation states that it proves domain replay/decision contracts under supplied fixtures and identifies live serialization, gateway authorization, actor concurrency, DAPR persistence, broker delivery, provider behavior, and deployment as excluded higher-tier boundaries
**And** passing kit tests cannot substitute for Story 7.11 persisted read-back, live-sidecar, full-Aspire, browser, provider-conformance, or release evidence.

**Given** a future implementation story is proposed
**When** readiness is assessed
**Then** it requires approved API/package design, compatibility/versioning policy, representative domain consumers, self-test matrix, migration guidance, diagnostics examples, performance bounds for large histories, and removal/disposition of overlapping helpers
**And** this backlog artifact or Story 7.17 completion authorizes no new package, public API, dependency, generator, or domain-test rewrite.

**Given** Story 7.17 planning completion is asserted
**When** `_bmad-output/planning-artifacts/backlog/kit-1-aggregate-test-kit.md`, its metadata, source-story link, and sprint ledger are independently validated
**Then** scope, non-goals, dependencies, risks, proposed fixture semantics, package boundary, proof limitations, decision gates, and validation expectations are present and internally consistent, with `source_story: 7.17`
**And** artifact and ledger statuses no longer contradict one another, all references resolve, and no code, package, dependency, solution membership, or test execution behavior is changed by this planning story.

### Story 7.18: REST Generator Hardening Backlog

As a product owner,
I want remaining REST generator hardening tracked independently,
So that diagnostics, incrementality, authorization, request limits, safe errors, and binding edge cases are not lost after proof stories complete.

**Requirements coverage:** Primary ownership of FR35's REST generator-hardening planning slice.

**Architecture constraints:** AD-4, AD-11–AD-13, and AD-17. Generated controllers remain in dedicated external API hosts, source/package compatibility stays catalog-governed, high-risk claims require production-path evidence, and successful command `Location` is absolute, gateway-authoritative, and fail-closed.

**UX coverage:** No direct UX requirement applies. Interactive UI hosts remain typed-client consumers and must not acquire generated or hand-written per-message MVC controllers.

**Dependencies:** Completed Epic D/Epic 2 generator proof and review evidence, the adopted AD-17 status-location policy, current gateway ProblemDetails/authorization contracts, and the named generator/contract test infrastructure. Future runtime work still requires separately approved implementation stories.

**Current reconciliation:** `_bmad-output/planning-artifacts/backlog/rest-generator-hardening.md` exists and the sprint ledger marks Story 7.18 done as planning work, but its front matter remains `status: draft`. It correctly preserves a resolved first wave and inventories six second-wave items with source/test targets, yet still describes the S2 command-status `Location` policy as open even though AD-17 has since adopted the authoritative rule. Completion must reconcile that drift and status without reopening completed fixes or changing generator/runtime code.

**Acceptance Criteria:**

**Given** the REST-GEN-HARDENING artifact is reviewed
**When** source evidence and current architecture are reconciled
**Then** it retains scope, non-goals, dependencies, risks, validation expectations, source-story/deferred-work links, resolved-first-wave evidence, and a closed inventory of remaining hardening items
**And** every path, story, diagnostic id, architecture reference, and policy statement resolves to current repository authority rather than a superseded review note or open decision.

**Given** first-wave hardening is already implemented
**When** the backlog is updated
**Then** unsupported contract-shape diagnostics, case-insensitive duplicate JSON-name handling, invalid binding diagnostics, route-template constraint parsing, referenced-contract incrementality, API-scope filtering, authorization, and generated error-semantic fixes remain identified as closed with their evidence references
**And** they are not duplicated into the remaining-work inventory, relabeled unimplemented, weakened, or used to claim that later items are also complete.

**Given** second-wave items S1–S6 remain
**When** their planning rows are validated
**Then** each records stable id, exact gap, affected contract, target source artifact/member, target test artifact/tier, dependency, risk, intended policy, completion evidence, owner, lifecycle, and relationship to existing behavior
**And** the inventory explicitly covers the canonical command request-size limit, command-status `Location`, support-safe query errors, allowlisted domain-rejection extensions, invalid tenant-source behavior, and runtime/generator binding-validation parity.

**Given** S2 is reconciled with adopted AD-17
**When** its intended policy is read
**Then** a generated command action may emit `Location` only for successful `202`, as an absolute gateway-authoritative configured command-status URI, and omits it when the target is absent or invalid; a dedicated external API host exposes no competing status authority
**And** the artifact no longer calls that policy undecided or permits the historic hard-coded relative `/api/v1/commands/status/{id}` behavior.

**Given** future diagnostics or generated output change
**When** an implementation story is proposed
**Then** its gate requires stable diagnostic ids/messages/locations, source and referenced-contract cases, deterministic incremental-generator behavior, compile-and-exercise output tests, route/body/tenant negatives, authorization, request ceiling, cursor/ETag behavior, safe ProblemDetails, and source/package compatibility
**And** source-string snapshots or endpoint status alone cannot prove runtime authorization, gateway routing, persisted command outcome, or support-safe failure behavior where a higher-tier boundary applies.

**Given** generated-host boundaries are reviewed
**When** remaining work is decomposed
**Then** each implementation unit preserves dedicated external API host ownership, the gateway policy edge, typed shared contracts, no local persistence/handler bypass, and no generated controller in Admin.UI or another interactive UI host
**And** generator convenience cannot introduce a competing command-status endpoint, decode opaque cursors/ETags, expose raw exception text, trust route/body tenant mismatch, or drop approved safe rejection metadata silently.

**Given** a listed item is implemented, rejected, superseded, or split later
**When** its lifecycle changes
**Then** the artifact records the approving story/decision, exact code/tests/evidence, residual scope, compatibility effect, and disposition of its original row while preserving audit history
**And** completion is not inferred from a related epic, diagnostic with a similar name, green mock test, or removal of the backlog text.

**Given** Story 7.18 planning completion is asserted
**When** `_bmad-output/planning-artifacts/backlog/rest-generator-hardening.md`, metadata, S1–S6 matrix, linked evidence, AD-17 reconciliation, source-story link, and sprint ledger are independently validated
**Then** resolved and open work are unambiguous, all remaining items have named implementation/test destinations and authority, and `source_story: 7.18` is internally consistent
**And** artifact and ledger statuses no longer contradict one another, no completed fix is reopened, and no generator, contract, host, package, route, or runtime behavior is changed by this planning story.

### Story 7.19: Admin Typed-Client And Evidence-State Integration

As an EventStore operator,
I want every dashboard flow to use typed clients and authoritative evidence states,
So that acceptance, freshness signals, and unavailable operations are never presented as completed work.

**Requirements coverage:** Primary ownership of FR34's integrated Admin operator-experience slice, NFR15, and UX-DR10–UX-DR21, UX-DR24–UX-DR31, and UX-DR38–UX-DR41; supporting FR13/FR15 typed-client boundaries, NFR1–NFR2 security, and NFR16 high-risk evidence.

**Architecture constraints:** AD-3, AD-8, AD-10, AD-12, AD-14, AD-15, AD-20, and AD-21. The UI is an external typed-client adapter, notifications are freshness signals only, lifecycle claims require route-bound provenance, rebuilds preserve the last complete model, and mutation success requires authoritative evidence.

**UX coverage:** Primary ownership of the operational shell/header content, summaries, filters, evidence grids, state badges/banners, mutation dialogs, panels, command lifecycle, projection freshness/gating, loading/SignalR/denial/empty/validation/rebuild/security states, and dead-letter, tenant-access, and command-investigation journeys. Story 7.14 owns host/tabs/routes; Story 7.4 owns deferred-operation honesty; Story 7.20 owns theme-token, accessibility, localization, and responsive conformance.

**Dependencies:** Stories 7.1–7.5 for poison handling, claims, audit, deferred behavior, and shared typed clients; Story 7.14 for the canonical dashboard shell/routes; Stories 1.2, 1.14, 2.8, 4.4, and 5.2 for authoritative query/projection, erasure, notification, publisher-recovery, and authorization evidence consumed by the UI.

**Current reconciliation:** Story 7.19 remains backlog. Admin.UI already contains many partial pages and Fluent components for skeletons, issue banners, stat cards, grids, projection badges, detail panels, command pipelines, empty/denied states, polling, and SignalR connectivity. Those surfaces are still distributed across numerous per-feature `Admin*ApiClient` wrappers and legacy routes, with uneven provenance/mutation semantics and fake deferred forms addressed by Stories 7.4, 7.5, and 7.14. This story is the single integration unit that turns the retained components into one authoritative operator model rather than a wholesale second UI rewrite.

**Acceptance Criteria:**

**Given** every Overview, Commands, Streams & Events, Projections, Tenants & Access, Topology, Storage & Snapshots, Recovery, Deferred & Backlog, and Settings surface is inventoried
**When** its data and action boundary is validated
**Then** each query and mutation uses the applicable Story 7.5 typed-client facet and typed outcome from one composition root, with an explicit route, tenant/scope, authorization, provenance, freshness, and error contract
**And** no page or component owns raw URLs/JSON/ProblemDetails mapping, direct handler/actor/store/broker/DAPR calls, per-message controllers, parallel legacy transport, or UI-synthesized authority.

**Given** the dashboard shell and active tab render
**When** environment, tenant, and connection context is available or changes
**Then** the Fluent-themed app bar/content header shows a focusable direct work-surface title, current authorized environment and tenant scope, connection/freshness state, last successful refresh when applicable, and bounded support-safe utility actions
**And** it uses no hero copy, hidden scope, raw endpoint/claim/token detail, false global-health inference, or tenant selection outside the current authorized set.

**Given** a tab displays operational evidence
**When** its summaries, filters, grids, badges, banners, and drill-in controls render
**Then** summaries pair each value with evidence state; Fluent inputs sit above the grid they affect and persist safe useful filters in the URL; dense Fluent/FrontComposer grids support applicable sort/filter/page/detail behavior; badges include readable canonical states; and issue banners name affected visible scope, consequence, and safe next action
**And** multi-section details use one approved drawer/dialog/panel with a primary expanded accordion section, while denied filters, hidden rows, raw payloads, decorative cards, color-only meaning, and a grid concealed as the sole accordion content are forbidden.

**Given** command lifecycle evidence is returned
**When** a command is inspected or a mutation progresses
**Then** the compact lifecycle tracker distinguishes `Received`, `Processing`, `EventsStored`, `EventsPublished`, `Completed`, `Rejected`, `PublishFailed`, and `TimedOut`, correlates only safe stable identifiers, and shows the evidence source and observation time
**And** HTTP `202`, a generated operation id, transport success, one stored event, SignalR, a toast, or elapsed time cannot be relabeled `Completed` without the route's authoritative terminal evidence.

**Given** query/projection metadata is rendered
**When** route provenance is `ProjectionBacked`, `HandlerComputed`, missing, invalid, or unknown
**Then** `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` render only from valid projection-backed lifecycle evidence; every other provenance renders `Unknown`; each state includes its operational consequence and observation/freshness time
**And** ETag, cursor, cache hit, response age, SignalR, or locally computed data never proves projection version/currentness, `LocalOnly` never counts as confirmed success, and opaque ETags/cursors are neither displayed nor parsed.

**Given** a projection is rebuilding or evidence is no longer current
**When** the dashboard refreshes
**Then** the last complete live model remains visible only with an explicit rebuilding/stale/degraded label and timestamp, bounded progress is shown only when authoritative progress exists, and mutations are disabled by default for every state except authoritative `Current`
**And** an exception requires a named consumer owner, exact allowed operation/state, risk rationale, expiry/review point, and automated tests; stale partial rebuild output or locally staged state is never promoted as live.

**Given** a page is cold-loading, refreshing, disconnected, or the Admin API is unavailable
**When** state transitions occur
**Then** layout-matching skeletons represent cold loading, explicit refresh/polling fetches authoritative evidence, SignalR only marks freshness and triggers a bounded refetch, a global issue banner communicates API unavailability, and last-known data appears only with stale scope/time labels
**And** disconnected/reconnected events do not imply service failure/recovery or mutation completion, repeated notifications are coalesced/bounded, and unavailable/timeout/cancelled never becomes empty or success.

**Given** a state-mutating Admin action is available and authorized
**When** an operator validates, submits, confirms, cancels, or revisits it
**Then** a Fluent dialog shows exact target, current tenant/environment, permission context, effect, expected evidence, risk, and cancellation; inline validation blocks malformed/oversized input; and the UI follows validation → submit → accepted → evidence pending → projection-confirmed or terminal non-success
**And** Story 7.3 audit intent/disposition remains attributable, raw payloads are not echoed, EventStore identifiers are described by their actual shape rather than as GUIDs, retries preserve stable operation identity, and optimistic success/toasts cannot bypass confirmation.

**Given** access is unauthenticated, denied, expired, wrong-scope, or revoked during a page load or action
**When** the outcome is displayed
**Then** the UI fails closed with an accessible denied label, safe route/action context, bounded next action, focus return to the initiator, cleared protected/transient state, and no mutation or background retry
**And** status, copy, filters, autocomplete, counts, empty states, cached rows, palette entries, and timing do not confirm whether an inaccessible tenant, user, stream, projection, service, setting, or operation exists.

**Given** an authorized visible scope contains no results
**When** an empty state renders
**Then** it identifies only the current visible tenant/domain/filter scope, distinguishes truly empty from unavailable/denied/stale/loading, offers a safe relevant next action, and retains only bounded support metadata
**And** bearer tokens, decoded JWTs, raw EventStore metadata or payloads, protected payloads, stack traces, cursors, ETags, secrets, connection strings, unbounded SignalR data, or unauthorized identities never render or enter client logs/telemetry.

**Given** the Recovery tab reports dead letters
**When** the incident-recovery journey runs
**Then** it shows count, oldest age, affected authorized tenant/domain, failure category, freshness/evidence source, protected detail, and role-gated retry/archive choices; confirmation creates an audited stable operation, shows accepted/evidence-pending, then verifies the authoritative dead-letter disposition/count and safe audit evidence
**And** no raw payload/stack trace is disclosed, an HTTP response or notification cannot prove recovery, a failed/unknown retry remains non-success, and the UI does not recommend duplicate command resubmission as recovery.

**Given** an authorized administrator changes tenant access
**When** the tenant-access journey runs
**Then** the UI filters only visible scope, requires authoritative-current evidence, confirms exact tenant/user/role and effect, submits through the typed mutation contract, shows accepted/evidence-pending, and confirms only after the authoritative role projection plus attributable audit agree
**And** concurrent/stale evidence, revoked permission, denial, ambiguity, timeout, or mismatched audit leaves a non-success state and does not disclose hidden tenants/users or preserve an optimistic role display.

**Given** an operator investigates a command
**When** a safe message or correlation identifier is searched
**Then** the journey distinguishes received/processing, stored versus published events, command terminal state, protected stream linkage, projection freshness, and committed-but-unpublished recovery posture using typed authoritative evidence
**And** it does not expose payload/metadata internals, infer publication from storage, infer projection success from publication, reveal cross-tenant matches, or tell the operator to resubmit a committed command whose publication needs recovery.

**Given** a deferred/backlog feature or unavailable backend contract is encountered
**When** the dashboard, route, palette, or detail panel evaluates it
**Then** Story 7.4's hidden/disabled/read-only or real-`501` matrix and exact “Unavailable in this release.” copy are preserved with safe tracking context
**And** integrating the dashboard does not revive backup, restore, import, compaction, GDPR erasure, OIDC login, test-kit, or generator-hardening forms, fabricate progress, or synthesize successful typed results.

**Given** Story 7.19 completion is requested
**When** typed-client contract/component tests, route/tab content inventory, provenance/lifecycle fixtures, cold/stale/unavailable/SignalR transitions, mutation state-machine and audit tests, denial/redaction/empty-state negatives, and full dead-letter, tenant-access, and command-investigation browser journeys run against the appropriate real host/persisted-evidence lanes
**Then** every dashboard flow uses one typed boundary, reports only authoritative evidence, and the three critical journeys reach correct confirmed or explicit non-success outcomes with safe attribution
**And** Admin.UI Release build and focused host/browser tests pass with no per-page transport duplication, fake completion, cross-tenant disclosure, raw sensitive data, unexpected skips, or warnings.

### Story 7.20: Admin Accessibility Localization And Responsive Conformance

As an EventStore operator,
I want the canonical dashboard usable across accessibility, locale, and supported viewport conditions,
So that operational evidence remains understandable without a parallel UI implementation.

**Requirements coverage:** Primary ownership of FR34's cross-cutting Admin conformance slice and UX-DR6–UX-DR9 plus UX-DR32–UX-DR37; supporting NFR15 operational honesty and NFR16 testable high-risk evidence.

**Architecture constraints:** AD-12 and AD-21. Conformance applies to the one existing Admin.UI host and closed canonical route/component inventories, uses FrontComposer/Fluent UI V5 rather than a local design system, and preserves authoritative evidence semantics under assistive technology, localization, and layout changes.

**UX coverage:** Primary ownership of Fluent theme roles, contrast, typography, density, keyboard/focus/assistive behavior, reduced motion/live regions, stable selectors, resource-backed complete-string localization, three viewport bands, and narrow-screen mutation disposition. Story 7.19 remains owner of business state and journeys; conformance cannot change their meaning.

**Dependencies:** Story 7.19 and the Story 7.14 closed route/tab manifest provide the complete surfaces and states this story must validate.

**Current reconciliation:** Story 7.20 remains backlog. Admin.UI already has partial skip-link, semantic markup, keyboard activation, focus, live-region, responsive media-query, forced-colors, reduced-motion, viewport, and Fluent-only governance coverage. It has no `.resx` resources or localization registration, still contains hard-coded/custom palettes and component colors, retains skipped browser accessibility scaffolds, and references a nonexistent `perf-lab.yml` from a smoke-test comment. No measured production baseline supports a quantitative UI-performance release budget.

**Acceptance Criteria:**

**Given** Story 7.14's route/tab manifest and Story 7.19's component/state/journey inventory
**When** the conformance matrix is generated
**Then** every canonical tab, deep link, header, summary, filter, grid, badge, banner, lifecycle/freshness state, detail/accordion, dialog, palette state, loading/empty/denied/unavailable/error state, and critical journey maps to accessibility, locale, viewport, selector, and test evidence
**And** a removed legacy duplicate, hidden deferred control, or unsupported route is represented by its canonical redirect/disabled-state obligation rather than silently omitted or implemented as a parallel surface.

**Given** light, dark, system, and forced/high-contrast themes are active
**When** every canonical surface and state renders
**Then** accent, neutral layers, foreground, border, focus, and Success/Warning/Danger/Neutral/lifecycle treatment inherit FrontComposer/Fluent UI V5 roles and each status includes readable text meeting WCAG 2.2 AA contrast
**And** captured hex/rgb colors, gradients, custom brand/status palettes, Fluent-v4/FAST tokens, redefined theme primitives, decorative color bands, custom shadows, and color-only meaning are removed or replaced by a documented upstream-token exception validated in all themes.

**Given** typography and layout density are inspected
**When** pages, panels, dialogs, grids, and utilities render
**Then** Fluent/FrontComposer text roles use Segoe UI with system fallbacks, titles are direct focusable work-surface nouns, metadata uses approved roles, and 4px density yields 8px control gaps, 16px repeated-summary gaps, and 24px region gaps through component parameters/tokens
**And** no local heading ramp, negative letter spacing, marketing hero, oversized/decorative card, unnecessary nested card, floating section, or custom pill substitutes for a Fluent primitive.

**Given** an operator uses keyboard, screen reader, zoom, reflow, high contrast, or text-only status cues
**When** all canonical tabs, grids, filters, accordions, dialogs, detail panels, palette results, and actions are exercised
**Then** every function is operable in logical order; exactly one page title is focusable; the host skip link reaches dashboard content; names, roles, values, validation associations, row context, dialog modality, and state text are exposed; and focus moves/restores predictably after navigation, confirmation, cancellation, denial, and failure
**And** there is no keyboard trap, inaccessible custom SVG/chart/grid control, hover-only information, focus loss, duplicate landmark/title, disabled-control dead end, or inaccessible replacement for visible text.

**Given** motion preferences and asynchronous state changes are evaluated
**When** loading, accepted, evidence-pending, confirmed, freshness, denial, validation, or terminal failure transitions occur
**Then** reduced-motion disables nonessential animation without hiding progress/state, polite live regions announce accepted/evidence-pending/confirmed and freshness changes, assertive regions announce terminal failure/access denial/rejected destructive actions, and validation uses inline association plus a polite summary
**And** repeated polling/SignalR events are deduplicated or throttled so they do not flood assistive technology, while motion or animation never carries the only state meaning.

**Given** automated tests locate canonical controls and evidence
**When** copy, locale, Fluent rendering, or component internals change
**Then** dashboard tabs, filters, badges/states, dialogs/actions, grids/rows, banners, and journey checkpoints expose unique stable `data-testid` and canonical state identifiers appropriate to their scope
**And** tests do not primarily select translated text, CSS presentation, DOM position, generated Fluent internals, colors, timing sleeps, or opaque identifiers such as cursors/ETags.

**Given** the UI runs under every configured supported locale plus a diagnostic pseudo-locale
**When** the closed visible-copy inventory renders
**Then** titles, labels, actions, validation, banners, statuses, dialog copy, dates/numbers, empty/denied/error/deferred states, accessible names, and live-region announcements come from resource-backed complete strings with culture-aware formatting and tested fallback
**And** there is no runtime sentence/clause concatenation, English-only plural grammar, split translatable phrase, hard-coded visible English outside an approved invariant, or translation of tenant/domain/aggregate/message/correlation identifiers and other raw contractual identities.

**Given** the viewport is `>=1280px`
**When** every canonical surface is exercised at representative width, zoom, and content-length extremes
**Then** full host navigation, horizontal dashboard tabs, applicable full grid columns, dense filters, and detail interactions remain visible and usable without content overlap or clipped evidence
**And** responsive code does not infer authorization, evidence state, or feature availability from viewport width.

**Given** the viewport is `960–1279px`
**When** every canonical surface is exercised
**Then** host navigation compacts, tabs may scroll horizontally with keyboard/assistive affordances, secondary metadata moves to the owning detail panel, and all read/triage/action state remains reachable
**And** critical tenant/environment/freshness context, row identity, status text, confirmation evidence, or safe recovery action is not dropped merely to fit the layout.

**Given** the viewport is `<960px`
**When** every canonical surface is exercised at representative narrow widths and zoom
**Then** host navigation collapses accessibly, tabs and dense evidence remain navigable, incident triage/status/simple recovery visibility remain usable, and primary reading order does not require two-dimensional scrolling except within an explicitly labeled data-grid region
**And** no desktop-only duplicate page, hidden-only action, clipped dialog, off-screen focus target, or inaccessible hover detail is introduced.

**Given** a mutation is available on a narrow screen
**When** its complete target, permission, risk, validation, acceptance, pending, and terminal-evidence flow is assessed
**Then** it either remains fully usable in a viewport-sized Fluent dialog, is disabled with a specific resource-backed reason, or renders a support-safe desktop-required state with a usable cancel/back path
**And** the layout cannot shorten confirmation identity/effect, bypass Story 7.19 freshness/authorization/audit gates, or leave an operator in an accepted operation with no visible evidence state.

**Given** automated and manual accessibility evidence is produced
**When** WCAG 2.2 AA conformance is evaluated
**Then** component tests, semantic/governance scans, axe or equivalent browser scans, keyboard-only journeys, screen-reader-oriented name/role/live-region assertions, contrast/forced-color checks, zoom/reflow, and representative manual findings cover the full matrix with defects and exceptions owner-bound
**And** skipped, empty, quarantined, or unavailable browser cases follow Story 7.13 accountability and cannot be reported as passing conformance.

**Given** performance evidence is considered at conformance closure
**When** workflows, tests, and documentation are inspected
**Then** stale references to nonexistent performance jobs are corrected, any actual advisory evidence follows Story 7.13, and the absence of a quantitative UI budget remains a dated owner-bound follow-up until a measured production baseline and approved workload exist
**And** no arbitrary render time, bundle size, Lighthouse score, latency, or throughput threshold becomes a release gate, while accessibility, responsiveness, loading honesty, support safety, and cancellation bounds remain fully enforceable.

**Given** Story 7.20 completion is requested
**When** the full conformance matrix runs across locales, pseudo-locale, themes, forced colors, reduced motion, keyboard/focus/live regions, stable selectors, all three viewport bands, narrow-screen mutation dispositions, and Story 7.19 critical journeys
**Then** the single canonical dashboard remains understandable and operable with authoritative state semantics under every supported condition, and retained evidence identifies exact revision/browser/tool/viewport/locale
**And** Admin.UI Release build, component/governance/browser suites, resource completeness scans, and CSS/token scans pass with no unexpected skips, untranslated or concatenated copy, hard-coded design palette, accessibility violation, parallel UI, or invented performance gate.

<!-- Epic 7 story set confirmed complete for planning. -->

## Epic 8: Domains Can Opt Into Portable Payload Protection - Post-MVP

Domain modules can opt into an EventStore-owned, provider-neutral payload-protection engine with stable formats, production backend proof, compatibility, release provenance, and rollback evidence.

**Independent delivery tracks:**

- **8A — Authority and frozen contracts:** Stories 8.1–8.2 establish the content-bound security authority, additive public contracts, and independently reproducible golden vectors. Story 8.1 authorizes only Story 8.2 for its exact approved digest.
- **8B — Provider-neutral implementation:** Stories 8.3–8.5 implement the non-packable core, historical/mixed readers, and policy/key-lifecycle mechanics. Stories 8.4 and 8.5 may proceed in parallel only after Story 8.3 closes.
- **8C — Production conformance and Server path:** Stories 8.6–8.7 require both 8.4 and 8.5, prove a real Azure Key Vault boundary, and then integrate the engine into EventStore persistence/snapshot/read paths.
- **8D — Release, consumer parity, and rollback:** Stories 8.8–8.10 atomically release both packages, obtain separate Parties authority for dual-provider proof, and rehearse rollback after real `pdenc-v2` writes.
- **8E — G5 closure:** Story 8.11 alone assembles the immutable evidence packet and may close G5 after every preceding technical, release, consumer, rollback, and approval gate passes.

The epic remains additive, opt-in, post-MVP, and disabled by default. No story may start from epic approval alone: it must validate the exact approved normative digest and every dependency shown above. Production credentials/resources, external publication, Parties mutations, and G5 approval each require their separately bound authority.

### Story 8.1: Shared Payload-Protection Security Spec And ADR

As a platform security owner,
I want the shared payload-protection ownership and durable security contract approved before implementation,
So that the engine cannot make story-local choices that strand persisted history or weaken key custody.

**Requirements coverage:** Primary ownership of FR37's security-specification and authorization slice; supporting NFR1–NFR4, NFR7, NFR9–NFR12, NFR16–NFR17, and NFR19 by freezing their payload-protection boundaries before implementation.

**Architecture constraints:** AD-13 and AD-23. EventStore owns reusable engine/formats/mechanics, providers/operators retain production key custody, Parties retains domain legal policy and UX, and the content-bound approval gate—not story or ledger status—authorizes the next slice.

**UX coverage:** No direct UX implementation applies. The specification preserves Parties ownership of legal-policy UX/copy and defines support-safe/no-leak external and Admin boundaries without adding an EventStore UI.

**Dependencies:** No Epic 8 implementation dependency. The existing `IEventPayloadProtectionService`, provider-neutral metadata/outcome/workflow/redaction contracts, Story 22.7 preservation inventory, current 14-package manifest, and exact inspected EventStore/Parties source identities are specification inputs only.

**Current reconciliation:** Story 8.1 is complete as an authorization artifact. `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md` is tracked with `status: approved-authorized`; its normative SHA-256 recomputes exactly to `0f841d5a72a0d0b10fa42a7e765b7282a810f3a5a2aa2b41da2001d17a054ae7`; detached packet `AR-20260801-01` records named approval in every mandatory role and explicitly authorizes Story 8.2. The repository still has no engine/adapter project, consistent with the artifact's no-runtime boundary. Stories 8.3–8.11 remain dependency-gated.

**Acceptance Criteria:**

**Given** the EventStore-owned optional engine is proposed
**When** ownership and package boundaries are reviewed
**Then** the normative specification fixes the provider-neutral engine and companion production-adapter packages, allowed dependency direction, opt-in/disabled behavior, EventStore responsibilities, provider/operator custody, Parties-retained legal/erasure/certificate/report/UX responsibilities, and the no-direct-domain-dependency boundary
**And** an interface, no-op, LocalDevelopment, in-memory, mock, DAPR secret store, or consumer-local implementation cannot satisfy production payload-protection proof or become a second durable-contract authority.

**Given** `pdenc-v2` will become durable data
**When** the wire and authenticated-data sections are validated
**Then** the specification freezes envelope/version/algorithm identifiers, AES-256-GCM parameters, nonce/tag representation, byte-stable 11-field AAD encoding, canonical property-path grammar/manifest, identity and key-version binding, bounds, parsing rules, and exact golden/negative/mutation vector registry
**And** ambiguity, alternate serialization, culture-dependent encoding, nonce reuse, cross-tenant/domain/aggregate/property substitution, metadata tampering, malformed/oversized data, or convenient implementation defaults fail closed rather than creating another format.

**Given** existing durable history must remain readable or explicitly bounded-unreadable
**When** compatibility and rollout are reviewed
**Then** `json+pdenc-v1`, `json-redacted`, legacy-unprotected, Story 22.7 metadata, protected snapshots, `pdenc-v2`, mixed history, unknown/opaque formats, rollout, downgrade, and rollback after v2 writes have exact routing and typed outcomes
**And** unreadable protected content never becomes plaintext, redacted success, absent data, skipped projection input, advanced checkpoint, or silently downgraded output.

**Given** policy and key lifecycle are shared while legal decisions remain domain-owned
**When** public seams and operational mechanics are frozen
**Then** `PersonalDataAttribute`, `IPersonalDataPolicy`, `IErasureStateProvider`, context/result types, discovery rules, key/state paths, record/index/fence/lease/operation shapes, actor/reminder/metric names, wrapping/rotation/erasure/cache/resilience/audit/reconciliation behavior, versioning, and buffer ownership/zeroing rules are exact
**And** EventStore does not decide Parties legal basis, retention, erasure orchestration, certificate/report meaning, Art.20/Art.30 behavior, or user-facing copy.

**Given** a real production backend is required
**When** backend selection and custody restrictions are approved
**Then** Azure Key Vault is selected for the companion adapter, workload identity and exact-version cryptographic operations are required, runtime identities cannot administer/export KEKs, operator/IaC provisioning remains separately authorized, and real isolated service conformance is assigned to Story 8.6
**And** the specification creates no Azure resource, credential, secret, client registration, network rule, production key, or claim that application/provider configuration shape alone is conformance evidence.

**Given** the security and operational threat model is reviewed
**When** misuse, failure, and data-flow boundaries are inspected
**Then** cross-scope substitution, nonce/path ambiguity, metadata/envelope mutation, provider denial/missing/deleted/unavailable/throttled state, lifecycle races, cache staleness, crash/restart, rollback/downgrade, excessive identity, evidence overclaim, and every external/Admin/telemetry/export surface map to preventive controls, bounded outcomes, validation ids, and residual risks
**And** plaintext, key material, credentials, provider-private errors/identities, payloads, protected bytes, unsafe metadata, or sentinel encodings cannot leak through logs, traces, metrics, exceptions, ProblemDetails, Admin, evidence, exports, certificates, reports, or support bundles.

**Given** approval is requested for the frozen normative content
**When** the authorization algorithm runs
**Then** unique LF/no-BOM markers produce the recorded SHA-256, incorporated fixture/vector hashes match, OD-01–OD-06 have no open material finding, every mandatory role is named/dated `Approved` for that same digest, and residual risks are explicitly accepted
**And** author self-review, group aliases, story completion, issue status, planning approval, boolean flags, implied silence, a different digest, or a changed normative byte cannot grant authorization.

**Given** detached approval packet `AR-20260801-01` is verified
**When** its reviewer identity, timestamp, digest, source identities, vector environments/results, findings, accepted risks, roles, and final disposition are compared with the normative rules
**Then** Story 8.2 is `AUTHORIZED` only for exact-digest/source preflight and its bounded contracts/goldens implementation
**And** the packet does not authorize Stories 8.3–8.11, production provisioning, package/release mutation, Server enablement, Parties edits, provider credentials, deletion of the local rollback path, or G5.

**Given** current repository sources have advanced beyond the inspected Story 8.1 baselines
**When** Story 8.2 activation is considered
**Then** Story 8.2 must reverify the exact approved digest, current public/source compatibility, fixture identities, manifest/package baseline, and every relevant preservation seam before editing code and retain evidence of any compatible drift
**And** current drift is not silently absorbed into Story 8.1 approval; a normative gap or incompatible source change returns to the ADR, invalidates dependent authorization as required, and blocks implementation.

**Given** Story 8.1 completion is validated
**When** artifact structure/traceability, marker/digest, vector registry, two-toolchain plus independent reproduction, source register, ownership/package/custody, compatibility, threat/no-leak, sequence, and detached approval checks run
**Then** the exact approved artifact remains `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`, its current digest and authorization are reproducible, and only Story 8.2 is enabled under the stated preflight
**And** the package manifest remains 14, no engine/adapter/runtime/provider/Parties mutation is present, and no completion or G5 claim exceeds the content-bound evidence.

### Story 8.2: Payload-Protection Contracts And Golden Vectors

As a platform security owner,
I want stable payload-protection contracts and independent golden vectors,
So that later engine slices implement one byte-exact durable protocol.

**Requirements coverage:** Primary ownership of FR37's additive public-contract and golden-vector slice; supporting NFR7 and NFR12 compatibility plus NFR19 byte-stable/fail-closed semantics.

**Architecture constraints:** AD-6, AD-12, and AD-23. Existing Contracts remains the single provider-neutral authority, changes are additive for current providers/consumers, and test vectors define durable bytes without introducing a runtime engine or backend.

**UX coverage:** No direct UX requirement applies. Contract diagnostics and fixtures use bounded reason codes and synthetic data only; they expose no payload, credential, provider-private detail, or user-facing legal-policy copy.

**Dependencies:** Story 8.1's exact normative digest `0f841d5a72a0d0b10fa42a7e765b7282a810f3a5a2aa2b41da2001d17a054ae7`, detached approval `AR-20260801-01`, and explicit Story 8.2 authorization. Current-source compatibility must be reverified before editing because the approved source baseline is historical.

**Current reconciliation:** Story 8.2 is backlog but explicitly authorized to start after its exact-digest/source preflight. `Hexalith.EventStore.Contracts/Security` already contains `IEventPayloadProtectionService`, metadata/carrier, readable/unreadable outcomes, stable reason codes, crypto-shredding workflow, backup admission, and readability decisions. The Story 8.1-selected personal-data policy, erasure-state, payload-kind, v2 context/snapshot carrier/completion types and repository-owned frozen vector fixtures are absent; no payload-protection engine project exists.

**Acceptance Criteria:**

**Given** Story 8.2 activation is requested
**When** its preflight runs against the working baseline
**Then** the Story 8.1 normative markers, LF/no-BOM rule, SHA-256, fixture references, `AR-20260801-01` identities/roles/disposition, and `story_8_2_authorized` state match exactly, while current EventStore public/source/package identities and preservation seams are recorded and compared with the approved baseline
**And** a digest mismatch, altered normative byte, missing approval, incompatible drift, unresolved ambiguity, or working-tree uncertainty blocks code and returns the affected decision to the ADR rather than selecting a default.

**Given** the existing Contracts security surface is inventoried
**When** additive API design is reviewed
**Then** every existing type/member, default-interface behavior, nullability, serialization shape, reason code, metadata bound, source/package consumer, fake/no-op provider, and compatibility test is mapped to retain, extend, or explicitly leave unchanged
**And** no existing provider/consumer is forced to implement new members, reference an engine/backend package, change current no-op behavior, reinterpret v1 metadata, or adopt a breaking binary/source/serialization contract.

**Given** the frozen Story 8.1 policy and erasure seams are implemented
**When** `src/Hexalith.EventStore.Contracts/Security` is inspected
**Then** one public type per file provides the exact approved `PersonalDataAttribute`, `PayloadProtectionPayloadKind`, `PersonalDataPolicyDecision`, `PersonalDataPolicyContext`, `IPersonalDataPolicy`, `PayloadErasureState` types, `IErasureStateProvider`, and selected stable v2 event/snapshot context, carrier, and completion contracts with documented ownership, bounds, nullability, cancellation, and typed outcomes
**And** the types contain no Azure/DAPR/Server/domain/UI dependency, legal-policy default, provider implementation detail, plaintext/key field, parallel lifecycle taxonomy, or unapproved reflection/discovery behavior.

**Given** `IEventPayloadProtectionService` lacks context required by the approved v2 contract
**When** it is extended
**Then** backward-compatible default overloads/adapters carry exact identity, payload kind, property path, key/version, and completion context while delegating safely for existing providers according to the frozen compatibility rule
**And** old implementations compile and run unchanged, unsupported v2 operations remain explicit typed outcomes, cancellation is preserved, and default methods never claim v2 protection, fabricate key identity, or silently drop authenticated context.

**Given** durable `pdenc-v2` fixture inputs are materialized
**When** the repository-owned vector manifest and files are generated/reviewed
**Then** every atomic field, UTF-8 byte sequence, path-manifest entry, 11-field AAD, key/nonce/plaintext/ciphertext/tag, envelope/wrapper, expected outcome, vector id, Story 8.1 section, and SHA-256 is explicit, bounded, synthetic, ordered, and immutable under a documented regeneration command
**And** fixtures contain no real tenant/payload/key/credential/provider identity, culture-dependent text, ambient randomness/time, hidden binary, or hand-edited derived value without a hash failure.

**Given** golden correctness is claimed
**When** two independent toolchains implement the frozen atomic inputs and encoding rules
**Then** both reproduce G-001, the NIST AES-256-GCM control, and every Story 8.2-assigned owner golden byte-for-byte—including path manifest, AAD, ciphertext/tag, envelope, wrapper, and hashes—and retained evidence records exact tool/runtime versions and commands
**And** shared generated code, one library called from two wrappers, copied expected output, author assertion, or comparison only after normalization does not count as independent reproduction.

**Given** malformed or hostile vectors are evaluated
**When** the Story 8.1 V001–V138 registry is traced
**Then** every vector maps to an owning story/test tier and Story 8.2 executes all contract/parser/encoding/API-assigned positive, negative, boundary, cross-scope substitution, path ambiguity, metadata mutation, unknown-version, oversized, cancellation, and no-leak cases with exact typed results
**And** unimplemented future engine/provider/persistence vectors remain visibly predecessor-gated rather than skipped, marked passing, weakened, or simulated as production proof.

**Given** contract serialization and diagnostic surfaces are tested
**When** types, fixtures, exceptions, test output, logs, and evidence render success or failure
**Then** durable names/values and reason codes match the frozen vocabulary, unknown fields/versions fail according to policy, equality/round-trip/bounds are deterministic, and output is constructively allowlisted
**And** no plaintext, raw protected bytes, DEK/KEK material, nonce/tag outside approved synthetic fixtures, provider URI/error, credential, unbounded metadata, or unsafe fixture value leaks into runtime-facing contracts or diagnostics.

**Given** source and package compatibility is validated
**When** Contracts restore/build/test and representative existing no-op/fake/custom providers compile against source and staged package outputs
**Then** current behavior remains unchanged unless the new overload is explicitly invoked, public API and serialization baselines show only approved additive differences, and dependency graphs remain provider-neutral
**And** a source-only green build, suppression, baseline overwrite without review, test fake updated to hide a breaking change, or package graph containing the future engine/adapter cannot satisfy compatibility.

**Given** Story 8.2 completion is requested
**When** digest/source preflight, public API inventory/diff, contract unit/property tests, fixture schema/hash validation, two-toolchain goldens, mutation/negative/no-leak corpus, existing-provider compatibility, and package-mode consumer checks pass
**Then** an immutable evidence packet binds exact source SHA, normative digest, contract/fixture inventory and hashes, commands/results, limitations, and named EventStore owner, Security Reviewer, and independent Test/Vector reviewer approvals
**And** Story 8.3 remains blocked until that exact evidence explicitly authorizes it; no core engine, production adapter, Server hook, packability/manifest change, Azure resource, Parties mutation, release, or G5 claim is created by Story 8.2.

### Story 8.3: pdenc-v2 Core Cryptographic Engine

As a platform security owner,
I want the provider-neutral `pdenc-v2` engine to implement the frozen vectors,
So that cryptographic behavior is shared without coupling to one key provider.

**Requirements coverage:** Primary ownership of FR37's provider-neutral cryptographic-core slice; supporting NFR1/NFR3 confidentiality and safe diagnostics plus NFR19 byte-stable, bounded, zeroing, and fail-closed implementation.

**Architecture constraints:** AD-12 and AD-23. The core implements only approved Story 8.1/8.2 contracts and bytes, owns mutable sensitive buffers it can actually zero, depends inward on Contracts, remains provider-neutral/non-packable, and cannot claim production conformance.

**UX coverage:** No direct UX requirement applies. All externally observable outcomes are typed, bounded, and support-safe; the core exposes no UI, payload preview, key detail, or provider-specific recovery copy.

**Dependencies:** Story 8.2 must be complete with an immutable evidence packet that matches the approved Story 8.1 digest and explicitly authorizes Story 8.3. No current ledger status or partial contract implementation substitutes for that gate.

**Current reconciliation:** Story 8.3 is backlog and dependency-blocked pending Story 8.2 evidence. No `Hexalith.EventStore.PayloadProtection` project currently exists. Existing protection interfaces, metadata, no-op behavior, and Server hooks remain the preservation baseline; this story creates a non-packable provider-neutral project but does not integrate it into Server or release it.

**Acceptance Criteria:**

**Given** Story 8.3 activation is requested
**When** authorization preflight runs
**Then** it verifies the unchanged Story 8.1 normative digest, exact Story 8.2 source/contract/fixture hashes, independent golden results, compatibility evidence, approvals, and explicit Story 8.3 authorization
**And** stale/missing evidence, a changed fixture or contract, open security/vector finding, incompatible current-source drift, or authorization for another story blocks implementation and records the exact mismatch.

**Given** the core project is created
**When** solution and dependency graphs are inspected
**Then** `src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj` targets the approved framework, sets `IsPackable=false`, remains opt-in, references `Hexalith.EventStore.Contracts`, and uses only centrally governed provider-neutral/BCL/Microsoft.Extensions/DAPR dependencies allowed by the specification
**And** it contains no Azure SDK, domain assembly, Server implementation, UI/Admin, consumer, provider credential, production configuration, package-manifest entry, local package version, or automatic default registration.

**Given** a canonical property path and protection context are supplied
**When** path traversal and AAD encoding execute
**Then** eligible values, ordering, escaping, duplicate/ambiguous paths, depth/count/length bounds, payload kind, tenant/domain/aggregate/type/path/key-version/format fields, integer encoding, separators, and UTF-8 bytes match the Story 8.2 manifest and all assigned vectors exactly
**And** reflection order, dictionary order, culture, runtime type name drift, Unicode normalization assumptions, missing scope, wildcard ambiguity, duplicate selection, or caller-supplied alternate bytes cannot change authenticated identity silently.

**Given** a selected event property or snapshot value is protected
**When** the core engine runs
**Then** it uses the approved AES-256-GCM algorithm, CSPRNG-generated nonce of exact length, per-value key/nonce/AAD semantics, exact envelope encoding, authentication tag, format identifiers, and bounded output matching G-001 and every applicable Story 8.2 golden
**And** nonce reuse/deterministic nonce, unauthenticated metadata, alternate algorithm/tag length, partial output, plaintext fallback, ambient key source, or mutation of caller-owned input is forbidden.

**Given** an approved `pdenc-v2` envelope is unprotected
**When** strict parsing and authentication run
**Then** every field, version, algorithm, length, encoding, canonical order, duplicate/unknown member rule, context/AAD match, and trailing-data condition is validated before any plaintext is returned, with exact readable or typed unreadable outcome
**And** malformed, truncated, oversized, tampered, wrong-scope, wrong-key-version, unknown-version/algorithm, authentication-failed, or provider-opaque input never becomes partial plaintext, legacy input, redacted success, empty data, or a raw cryptographic exception.

**Given** protect/unprotect succeeds, fails, is cancelled, or throws internally
**When** engine-owned plaintext, DEK, AAD staging, decoded envelope, or temporary output buffers leave use
**Then** every mutable sensitive buffer owned by the engine is zeroed in deterministic `finally` paths, pooled buffers are cleared before return, and ownership/aliasing rules are documented and tested
**And** the engine never falsely claims it erased caller-owned CLR objects, immutable strings, JSON DOMs, aliased memory, provider buffers it does not own, or data already copied by a serializer/runtime.

**Given** input, output, path, collection, nesting, metadata, or work limits are reached
**When** validation or cryptography executes
**Then** fixed approved maxima are checked before allocation/work, cancellation is observed at the specified safe points, integer/size arithmetic is overflow-safe, and failures return the frozen bounded taxonomy without unbounded CPU/memory amplification
**And** cancellation after an irreversible boundary is not mislabeled, partially protected results are not returned, and retry/caller behavior cannot bypass the same bounds.

**Given** concurrent calls use the engine
**When** deterministic and stress tests run
**Then** immutable configuration/codecs are thread-safe, per-operation sensitive state is isolated, randomness is never shared incorrectly, failures cannot poison later operations, and deterministic injectable seams exist only under approved test boundaries
**And** global mutable nonce counters, cached plaintext/DEKs, cross-tenant context reuse, unsafe static buffers, or tests that replace production cryptography are prohibited.

**Given** logging, tracing, metrics, exceptions, and test/evidence output observe the core
**When** every success/failure/cancellation/vector path runs with the sentinel corpus
**Then** only allowlisted low-cardinality operation, format/version, payload-kind, result/reason, duration/count, and synthetic vector identifiers are emitted
**And** plaintext/ciphertext, keys, nonce/tag, full AAD/path, tenant/domain/aggregate/message identity, provider details, serialized envelope, exception parameters, or sentinel encodings never leave approved synthetic fixture artifacts.

**Given** core correctness is challenged
**When** unit/property/mutation/fuzz/boundary/concurrency/cancellation/zeroing/no-leak tests run
**Then** G-001, NIST control, every assigned V-vector, round-trip invariants, single-bit mutations of every authenticated region, cross-scope substitutions, parser corpus, maximum/over-limit cases, and deterministic failure taxonomy pass against Story 8.2 bytes
**And** self-round-trip alone, an unreviewed crypto wrapper, disabled mutation, skipped fuzz seed, mock algorithm, or only happy-path ciphertext comparison cannot establish correctness.

**Given** Story 8.3 completion is requested
**When** dependency/API scans, warnings-as-errors/AOT-relevant build checks, golden and mutation corpus, bounds/concurrency/cancellation, owned-buffer zeroing, no-leak scans, and source/package-preservation tests pass
**Then** an immutable evidence packet binds source SHA, Story 8.1 digest, Story 8.2 contract/fixture hashes, project/dependency inventory, commands/results, limitations, and named owner/security/test approvals
**And** only Stories 8.4 and 8.5 may be explicitly authorized from that evidence; the project stays non-packable and no Azure adapter, Server wiring, persisted v2 data, package/release, Parties mutation, or G5 claim exists.

### Story 8.4: Compatibility Readers And Mixed-History Routing

As a platform maintainer,
I want every approved historical format routed explicitly,
So that new protection never strands or silently downgrades durable history.

**Requirements coverage:** Primary ownership of FR37's historical/mixed-read compatibility slice; supporting NFR7 no-silent-loss, NFR12 compatibility, and NFR19 fail-closed typed read routing.

**Architecture constraints:** AD-6, AD-8, AD-12, and AD-23. Each durable record is classified independently from constructive metadata/format/shape evidence, protected content authenticates before use, and unreadable history stops dependent work rather than becoming plaintext or a skipped event.

**UX coverage:** No direct UX implementation applies. Public/Admin consumers receive only existing bounded readability decisions and safe reason codes; authorization does not permit ciphertext, plaintext-on-failure, key/provider detail, or hidden continuation past unreadable history.

**Dependencies:** Story 8.3 must be complete and its exact evidence must explicitly authorize Story 8.4. Story 8.5 may proceed in parallel after the same gate; neither story depends on the other's implementation.

**Current reconciliation:** Story 8.4 is backlog and dependency-blocked. Existing EventStore paths already use protection metadata and typed readability outcomes for aggregate rehydrate, publish, projection, replay, Admin, snapshots, and restore, but no shared `pdenc-v2` engine/router exists. Parties `json+pdenc-v1` evidence belongs to a separately sourced consumer and must remain behind exact legacy-reader registration rather than being reimplemented or assumed available here.

**Acceptance Criteria:**

**Given** Story 8.4 activation is requested
**When** predecessor preflight runs
**Then** the approved Story 8.1 digest, Story 8.2 contract/fixture hashes, Story 8.3 core project/API/vector/no-leak evidence, and explicit Story 8.4 authorization match the current source baseline
**And** missing authorization, core/vector drift, unresolved parser defect, changed historical contract, or failed preservation test blocks compatibility work rather than adding a permissive route.

**Given** an event or snapshot enters a read path
**When** compatibility routing begins
**Then** the router first parses the bounded `eventstore.protection` carrier, then classifies reserved serialization format and bounded wrapper/marker shape, verifies their agreement, and selects exactly one legacy-unprotected, redacted, registered-v1, shared-v2, or opaque/mismatch route per record
**And** route selection does not inherit from the first/latest stream record, file extension, CLR type, tenant configuration, caller hint, provider exception text, or best-effort content sniffing.

**Given** metadata is missing or exactly unprotected
**When** ordinary `json` or a non-reserved historical custom format has valid bounded bytes and no protected marker/wrapper
**Then** bytes pass through unchanged with explicit legacy/unprotected classification, preserving original format and order
**And** this compatibility path never proves the data was non-sensitive, accepts a reserved `json+pdenc-*` prefix as custom plaintext, or overrides a detectable protected shape/mismatch.

**Given** exact `json-redacted` bytes contain no encrypted marker
**When** the redacted route executes
**Then** valid bounded redacted JSON passes through unchanged with redacted state and remains eligible only for operations that accept redacted evidence
**And** it is not decrypted, re-protected automatically, represented as original/recoverable plaintext, or accepted when metadata/shape claims protected content.

**Given** exact `json+pdenc-v1` and bounded `$enc` shape are present with approved v1 or compatible missing legacy metadata
**When** routing occurs
**Then** the exact configured legacy reader id—`parties-pdenc-v1` for the approved consumer evidence—owns readable/typed-unreadable behavior through a bounded registration seam
**And** the shared engine does not reinterpret v1 cryptography, depend on Parties, synthesize that reader, or treat a missing/wrong reader as plaintext; absence returns `ProviderOpaqueUnsupportedOperation`.

**Given** exact approved v2 metadata, `json+pdenc-v2`, and valid `$pdenc` wrapper shape agree
**When** routing occurs
**Then** each wrapper is bounded/parses canonically, key/lifecycle resolution is requested through the approved seams, Story 8.3 authenticates exact context/AAD before JSON replacement, and only fully readable output receives unprotected read metadata
**And** partial plaintext, unauthenticated JSON parse/use, path replacement before all required checks, unknown extra wrappers, or one successful field hiding another unreadable field is forbidden.

**Given** metadata, format, wrapper, version, or shape disagree
**When** classification runs
**Then** exact v1/v2/redacted/legacy mismatch cases map constructively to `BytesMetadataMismatch`, `MalformedMetadata`, `UnknownMetadataVersion`, `ProviderOpaqueUnsupportedOperation`, or the existing semantically exact typed reason without calling a provider unnecessarily
**And** malformed/truncated/oversized/non-canonical envelopes, unknown reserved versions, forbidden carrier fields, or authentication failure do not fall through to another reader, expose parser/provider text, or return empty/default data.

**Given** a stream contains legacy, redacted, v1, and v2 records in any order
**When** aggregate replay, publication, live/retry/rebuild projection, stream/replay API, Admin decoding, or export reads it
**Then** every record follows its own route in sequence and processing stops at the first unreadable sequence with one canonical bounded decision correlated to safe stream/sequence identity
**And** no later event is parsed/applied/published/projected/exported, no checkpoint or read model advances, no partial aggregate/result is returned, and retrying does not change the classification without changed authoritative evidence.

**Given** a stored snapshot is legacy, redacted, v1, v2, mismatched, unknown, malformed, opaque, or unreadable
**When** snapshot load executes
**Then** legacy state passes only under the approved matrix, v1 uses the registered legacy snapshot reader, v2 validates exact registered type/alias/`JsonTypeInfo` and snapshot AAD before deserialization, and every protected unreadable snapshot records the canonical decision, remains stored, and returns no snapshot so event replay may continue or fail closed
**And** only corrupt unprotected legacy snapshots may use the existing deletion exception; protected/opaque snapshots are never deleted, treated as cache misses without evidence, deserialized before authentication, or replaced by plaintext fallback.

**Given** deployment moves through dual-read/read-only/V2-write or rollback modes later
**When** compatibility capability is declared
**Then** the router exposes exact supported reader ids/formats/versions and preserves legacy/v1/redacted/v2 readers independently of current write mode, highest-written watermark, or elapsed time
**And** this story enables no v2 production write, rewrites/re-encrypts no immutable event, removes no legacy reader/fixture, and cannot make a v1-only/no-op binary safe after a v2 watermark.

**Given** diagnostics and evidence observe routing
**When** the complete matrix and sentinel corpus execute
**Then** only route id, format/version, safe payload kind, bounded reason, safe sequence correlation, and outcome counts are emitted with deterministic classification
**And** plaintext/ciphertext, envelope bytes, metadata bodies, paths/AAD, key aliases/versions beyond approved internal correlation, provider URI/request/error, tenant data, or later unread events never appear in logs, metrics, traces, ProblemDetails, Admin, or evidence.

**Given** Story 8.4 completion is requested
**When** exhaustive metadata-format-shape table tests, legacy/custom/redacted/v1 registration/v2 vectors, mixed-history permutations, first-unreadable stop/checkpoint assertions, snapshot retention/fallback, cancellation, fuzz/bounds, and no-leak tests run across each existing EventStore read path seam
**Then** an immutable packet binds source SHA, normative/contract/core evidence, route matrix, reader registry, fixture hashes, commands/results, limitations, and owner/security/test approvals
**And** it satisfies only the compatibility half of Story 8.6's gate; no Azure adapter, lifecycle store, Server persistence integration, packability/release, Parties edit, legacy-path deletion, or G5 claim is authorized.

### Story 8.5: Policy And Key-Lifecycle Mechanics

As a platform security owner,
I want domain-neutral policy and key lifecycle behind stable contracts,
So that domains retain legal policy while reusable mechanics remain consistent.

**Requirements coverage:** Primary ownership of FR37's policy-discovery, wrapped-key state, lifecycle/cache/resilience/audit slice; supporting NFR1–NFR4, NFR7, and NFR19 fail-closed confidentiality, isolation, operability, and no-silent-loss behavior.

**Architecture constraints:** AD-1, AD-10, AD-12, and AD-23. Domain policy selects values only, provider-neutral mechanics use exact tenant-scoped DAPR state contracts and current authorization/lifecycle evidence, and no cache, retry, infrastructure scope, or operator action can bypass irreversible state.

**UX coverage:** No direct UX implementation applies. Lifecycle/audit/telemetry outputs are bounded operational contracts only; Parties retains legal-policy, erasure-orchestration, certificate/report, and user-facing UX/copy ownership.

**Dependencies:** Story 8.3 must be complete and explicitly authorize Story 8.5. Story 8.4 may proceed in parallel, but Story 8.6 requires approved completion evidence from both 8.4 and 8.5.

**Current reconciliation:** Story 8.5 is backlog and dependency-blocked. Existing Contracts include crypto-shredding/readability/restore workflows, while no shared policy discovery, wrapped-DEK store/index, lifecycle/fence/deployment/lease/operation/reconciliation schema, cache, backend SPI, or payload-protection resilience worker exists. The future engine project remains non-packable, and no provider/IaC resource is authorized here.

**Acceptance Criteria:**

**Given** Story 8.5 activation is requested
**When** predecessor preflight runs
**Then** it verifies the approved normative digest, Story 8.2 contract/fixture identities, Story 8.3 core/API/vector evidence, current state-store contracts, and explicit Story 8.5 authorization
**And** any drift in policy contracts, cryptographic context, state capabilities, key schema, or approval blocks implementation rather than being hidden behind an adapter/default.

**Given** attribute and explicit policy registrations exist
**When** personal-data selection runs over the single source-generated JSON serialization
**Then** the built-in attribute policy and registered `IPersonalDataPolicy`/`ICanonicalPersonalDataPathPolicy` instances use the exact identity/version/order rules, deterministic precedence, monotonic `Protect`/`Abstain` semantics, canonical pointer validation, subtree suppression, null handling, and selected-value bounds
**And** duplicate/invalid policy ids, custom-converter ambiguity without a canonical path policy, pointer/value mismatch, policy/getter exception, conflicting ancestor/descendant selection, undefined decision, or cancellation fails before key creation with no plaintext fallback or domain-name heuristic.

**Given** a domain is protected or unprotected
**When** erasure-state provider ownership is validated
**Then** each protected domain has exactly one keyed `IErasureStateProvider`, each unprotected domain has none, state/epoch is checked before reservation and every unprotect, and `Active`, `Pending`, `Invalidating`, `Invalidated`, `Deleted`, `Unknown`, `Unavailable`, and `Denied` follow the frozen read/write matrix
**And** wildcard/default/duplicate/missing provider ownership, regressed/unchanged transition epoch, exception-text parsing, or a cache/config value cannot synthesize an allowed state.

**Given** the provider-neutral key hierarchy is used
**When** a protected occurrence is reserved
**Then** the engine creates a fresh owned 32-byte DEK, canonical uppercase ULID `keyRef`, positive `dekVersion`, stable operation identity, exact scope/tenant-domain digests, and opaque backend wrap request while keeping provider root/KEK custody outside EventStore
**And** KEK version is never confused with DEK version, fingerprints never become lookup/authorization/uniqueness inputs, and raw keys/credentials/provider ids do not enter envelopes, public metadata, metrics, errors, certificates, or logs.

**Given** `KeyStateStoreName` is configured
**When** PF-02 capability preflight and schema validation run
**Then** the exact selected DAPR component proves strong reads, ETags, multi-operation transactions, compare-only/no-op CAS behavior where needed, and TTL for leases before readiness; all approved wrapped-key, scope/KEK index, lifecycle, fence, deployment, capability/completion/read lease, operation, reconciliation, and audit keys/closed schema-v1 values use the frozen grammar/bounds/order/canonical encodings
**And** query-all/prefix scans, unbounded maps/pages, unknown/duplicate/null fields, weak/eventual-only state, unsupported transaction semantics, local key grammar, ETags in values/logs, or capability inference from component type cannot satisfy the contract.

**Given** a v2 protect operation starts
**When** lifecycle, fence, deployment generation, and capability evidence permit writing
**Then** the engine strong-reads/CAS-binds unchanged authority, wraps the DEK, transactionally creates the `Reserved` wrapped record plus both complete index entries and operation marker, advances the irreversible format watermark when required, and only then returns distinct encrypted bytes and acquires the completion lease for the later persistence hook
**And** a payload never precedes its key record, a write cannot use stale/ambiguous authority, returned bytes cannot alias caller input, partial state cannot produce plaintext/no-op output, and this story does not itself persist an event or activate a reservation.

**Given** wrapping or state creation fails before payload persistence
**When** the operation is retried or reconciled
**Then** stable operation id and durable phase distinguish no-record, reserved, payload-returned, ambiguous, failed, and terminal outcomes; orphan candidates are retained/quarantined under the 24-hour and lease rules and wrap/state ambiguity is reconciled before another mutation
**And** reserved records are never automatically deleted without a future approved complete reverse-reference index proving zero event and snapshot references; scans or missing current data are insufficient.

**Given** KEK rotation, wrapped-record invalidation/deletion, restore, or break-glass is requested
**When** lifecycle mechanics execute
**Then** complete bidirectional scope/KEK indexes and ETags prove membership, rewrap uses the recorded old and selected new provider versions through `RewrapPending`, lifecycle becomes `Invalidating` with a greater epoch before read/write denial, leases/caches drain before irreversible completion, restore never lowers epochs/watermarks, and every ambiguous mutation reconciles by stable operation id
**And** immutable events are not re-encrypted/re-written, provider KEK deletion is not inferred from record deletion, old KEK use is not retired early, break-glass cannot reverse terminal state/bypass authentication, and story completion is not deletion proof.

**Given** an unprotect operation may use a DEK cache
**When** lookup, unwrap, hit, expiry, eviction, invalidation, replacement, cancellation, or shutdown occurs
**Then** a strong current lifecycle/erasure read and distributed read lease precede cache access; the exact scoped cache key, 1,024-entry/60-second defaults and hard 4,096-entry/five-minute maxima apply; single-flight unwrap isolates waiter cancellation; and every owned array is copied/zeroed on all disposal paths
**And** there is no sliding, disk, distributed-plaintext, payload, cross-tenant, failure, denied, missing, invalidated, cancelled, or partially built cache entry, and pub/sub acknowledgement is acceleration rather than correctness authority.

**Given** state/backend operations encounter transient or permanent failures
**When** resilience applies
**Then** eligible strong-read/wrap/unwrap transients use at most three total attempts inside 10 seconds with approved delays/jitter/Retry-After bound; caller cancellation and permanent/auth/not-found/malformed/crypto failures do not retry; ambiguous mutations reconcile first; and the backend-scoped circuit breaker follows the frozen threshold/half-open/backoff rules
**And** retry layering cannot exceed the effective bound, choose another backend, repeat a non-idempotent mutation blindly, cache failure, parse provider text, or fall back to plaintext/no-op.

**Given** policy/lifecycle/cache/reconciliation emits observability or audit
**When** sentinel and cardinality checks run
**Then** exact approved activity/meter/instrument names and closed low-cardinality tags are used, and audit records contain only the approved identifiers, scope allowed by audit policy, states/epochs, fingerprints, operation/result/reason, actor, attempt/duration, and UTC time
**And** tenant/resource/keyRef/provider version/URI, state-store key/ETag, operation identity in metric tags, credential, payload/protected bytes, nonce/tag/wrapped key, exception/stack, certificate/report body, or arbitrary extension is emitted.

**Given** Story 8.5 completion is requested
**When** policy ordering/ambiguity, state-schema/canonicalization, real DAPR PF-02 capabilities, transaction/fence/reservation faults, lifecycle/rotation/invalidation/restore, cache/lease/single-flight/zeroing, retry/breaker/cancellation, reconciliation, audit/telemetry, and no-leak suites run
**Then** an immutable packet binds source SHA, normative/core evidence, component identity/capabilities, schema and policy inventories, fault commands/results, limitations, and owner/security/operations/test approvals
**And** it satisfies only the lifecycle half of Story 8.6's gate; no Azure adapter/resource, Server event/snapshot wiring, package/release transition, Parties mutation, provider-key deletion, or G5 claim is authorized.

### Story 8.6: Azure Key Vault Production Adapter Conformance

As an operations and security owner,
I want one real production adapter to conform without transferring key custody,
So that provider-neutral engine claims are proven against an operated service.

**Requirements coverage:** Primary ownership of FR37's production-backend adapter/conformance slice; supporting NFR1/NFR3/NFR4 confidentiality, diagnostics, and operational behavior, NFR17 pinned environment evidence, and NFR19 real-backend failure/custody semantics.

**Architecture constraints:** AD-11–AD-13 and AD-23. Azure is an outward companion adapter, dependencies are centrally versioned and evidence-pinned, provider/operator custody remains external, real-service persisted evidence is mandatory, and no provider mutation occurs without separately bound authority.

**UX coverage:** No direct UX implementation applies. Readiness, metrics, audit, and typed outcomes expose only bounded backend/profile/version fingerprints and safe reason codes—never vault/key URIs, credentials, provider request/error text, key material, or payloads.

**Dependencies:** Both Stories 8.4 and 8.5 must be complete with immutable packets matching Story 8.3/8.2/8.1 evidence and explicit authorization for Story 8.6. Azure subscription/resource, identity, network, vault/key, fault-injection, and cleanup actions additionally require named Security/Operations/IaC authority.

**Current reconciliation:** Story 8.6 is backlog and dependency-blocked. No `Hexalith.EventStore.PayloadProtection.AzureKeyVault` project or centrally selected Azure SDK dependency exists, and no repository evidence proves a conforming Azure environment. Story 8.1 selected the profile but explicitly deferred stable SDK/API selection and PF-01 source/API reverification to this story; no Azure resource is assumed to exist or be authorized.

**Acceptance Criteria:**

**Given** Story 8.6 activation is requested
**When** technical and external-authority preflight runs
**Then** it validates the approved normative digest and all Story 8.2–8.5 contract/core/compatibility/lifecycle packets, confirms both parallel predecessor approvals explicitly authorize 8.6, and records the exact Azure environment owner, mutation scope, credentials/custody boundary, cleanup/retention plan, and authorization receipt
**And** missing/stale approval, inconsistent route/lifecycle semantics, unapproved subscription/resource access, shared production data/key use, or ambiguous cleanup authority blocks provisioning and implementation evidence.

**Given** Azure service/SDK selection is finalized
**When** PF-01 reverifies current official Azure Key Vault and .NET SDK documentation/packages
**Then** stable non-preview `Azure.Identity` and `Azure.Security.KeyVault.Keys` versions supporting the repository target are centrally pinned with package hashes/dependency lock/SBOM, exact assembly/file versions and effective REST API version are recorded, and SDK retry behavior is configured within Story 8.5's total budget
**And** `.csproj` versions, previews, floating ranges, credential-chain fallbacks, unrecorded SDK/API defaults, nested retry multiplication, or an API chosen from memory cannot enter the adapter.

**Given** the companion adapter project is created
**When** dependency and package graphs are inspected
**Then** `src/Hexalith.EventStore.PayloadProtection.AzureKeyVault/Hexalith.EventStore.PayloadProtection.AzureKeyVault.csproj` targets the approved framework, sets `IsPackable=false`, depends inward on the engine plus centrally governed Azure SDKs, and owns only Azure options/validation, credential/client construction, capability probe, wrap/unwrap, strong version selection, and constructive response classification
**And** it duplicates no Contracts/core/state/policy code, references no domain/UI/consumer, supplies no control-plane/IaC implementation, changes no release manifest, and cannot become enabled by mere assembly presence.

**Given** `EventStore:PayloadProtection:AzureKeyVault` configuration is supplied
**When** closed options validation runs
**Then** `VaultUri`, `KeyName`, optional `ManagedIdentityClientId`, fixed `ExpectedKeySize=3072`, fixed `WrapAlgorithm=RSA-OAEP-256`, bounded `KeyVersionRefreshInterval`, and bounded `RequestTimeout` obey every frozen URI/name/environment/time rule and reject unknown fields
**And** options cannot contain tenant/client secret, certificate, token, private key, wrapped DEK, connection string, managed-HSM/secret-store endpoint, non-HTTPS or malformed vault URI, arbitrary key version, algorithm downgrade, or unsupported timeout.

**Given** the adapter runs outside exact Development
**When** credentials are constructed
**Then** it uses exactly system-assigned or explicitly selected user-assigned `ManagedIdentityCredential`, fails when identity selection is ambiguous/missing, and performs no interactive/control-plane/secret/certificate operation
**And** Azure CLI, IDE, environment client secret, default credential chain, workload fallback, production developer credential, or application permission to create/import/rotate/delete/recover/backup/restore/purge/release keys or modify roles/network is forbidden.

**Given** readiness starts
**When** the adapter validates its real data-plane boundary
**Then** it validates TLS/closed options, authenticates, strongly reads `KeyName`, requires one enabled/time-valid provider-generated non-exportable 3072-bit `RSA-HSM` current version with exactly wrap/unwrap operations and matching versioned vault/name id, then wraps/unwraps a fresh 32-byte probe with `RsaOaep256`, compares in fixed time, zeroes all probe buffers, and revalidates state/lifecycle capabilities
**And** mismatch makes readiness false; probe success neither persists a record nor proves production conformance, grants permission, or lets the application create/rotate/delete the KEK.

**Given** runtime wrap/unwrap executes
**When** current or historical exact-version keys are selected
**Then** new wraps use only the latest strongly validated enabled current version, historical unwrap uses the exact recorded versioned provider id, response identity/algorithm/length are verified, and all input/output buffers follow Story 8.3/8.5 ownership and zeroing rules
**And** refresh failure blocks new wrap after the approved interval while preserving exact historical unwrap where safe; current-version fallback, local RSA, RSA1_5, RSA-OAEP/SHA-1, software/exportable/imported key, EC/symmetric Managed-HSM profile, or response-text inference is rejected.

**Given** identity, network, service, key, or response failures occur
**When** constructive classification runs
**Then** configured-identity/401/403/network-policy denials, token/service/network transients, exact-version 404, disabled/expired/not-yet-valid/deleted key, 408/429/5xx, TLS identity failure, cancellation, invalid profile/algorithm/size, malformed response, returned-id mismatch, and ambiguous wrap/state transition map to the exact approved denied/unavailable/missing/deleted/consistency/cancellation behavior with correct retry/breaker/reconciliation policy
**And** messages/stack traces/URIs/request IDs are never classification inputs, permanent failures never retry, deletion is never inferred without lifecycle evidence, and ambiguous mutations are never replayed or declared complete blindly.

**Given** the production-equivalent conformance environment is provisioned under separate authority
**When** its posture is independently inspected
**Then** it uses a dedicated subscription/resource group, ordinary Premium vault, provider-generated RSA-HSM-3072 KEK, 90-day immutable soft-delete/purge protection, private endpoint/DNS, disabled public access/trusted-service bypass, diagnostic sink, dedicated user-assigned runtime identity with key-scoped Key Vault Crypto Service Encryption User or exact custom data actions, and an ephemeral runner inside the VNet
**And** production tenant data/key, shared prod/non-prod vault/identity/KEK, public endpoint, software key, broad vault/control-plane role, application provisioning identity, DAPR secret store, emulator, LocalDevelopment, or mock cannot satisfy custody posture.

**Given** real-service conformance executes
**When** wrap/persist/restart/read, rotation/rewrap, 401, 403, exact-version 404, disabled/expired key, bounded 429/Retry-After, timeout, DNS/network/5xx, cancellation, breaker transitions, ambiguous state mutation, stale-cache invalidation, protected snapshot, and rollback prerequisites are exercised
**Then** persisted wrapped-key/provider-state and exact typed outcomes agree with Stories 8.4–8.5, raw evidence records redacted resource hashes, region/profile/key attributes/version fingerprint, RBAC/network/SDK/API/package identities, UTC interval, source/spec hashes, and cleanup/retention state, and every owned buffer passes no-leak/zeroing checks
**And** a mock handler proves only classifier logic; one happy-path capability probe, status code, interface, configuration, or state-store row cannot establish real backend conformance.

**Given** deletion, backup, or crypto-erasure is discussed
**When** evidence wording is reviewed
**Then** it distinguishes online wrapped-DEK invalidation from expiry/destruction of every approved replica/export/backup/restore/DR copy, retains KEK versions and required key records for history/rollback, and keeps unknown copies pending/operator-required
**And** 90-day recoverable KEK deletion, primary-row deletion, cache invalidation, vault audit, or EventStore completion cannot falsely certify immediate/full subject erasure or authorize shared-KEK removal.

**Given** Story 8.6 completion is requested
**When** central dependency/API verification, adapter unit/classification/options/startup tests, real isolated service conformance/fault injection, persisted provider/state evidence, RBAC/network/custody review, retry/breaker/zeroing/no-leak scans, and authorized cleanup/retention disposition pass
**Then** an immutable packet binds source SHA, normative and predecessor hashes, package/API identities, redacted Azure environment/authority, commands/results, retained resources/limitations, and named Security/Operations/EventStore/Test approvals
**And** only Story 8.7 may be explicitly authorized; both projects remain non-packable and no Server integration, external release, production enablement, Parties mutation, key deletion beyond the authorized test disposition, or G5 claim occurs.

### Story 8.7: Server Persistence And Snapshot Integration

As an EventStore operator,
I want the approved engine integrated into event and snapshot persistence,
So that protection is opt-in, tenant-safe, durable, and reversible without private serialization paths.

**Requirements coverage:** Primary ownership of FR37's EventStore Server persistence/read integration slice; supporting NFR1–NFR2 confidentiality/isolation, NFR7 no-silent-loss, NFR16 persisted production-path evidence, and NFR19 fail-closed startup/read/write behavior.

**Architecture constraints:** AD-5, AD-6, AD-8, AD-10, AD-12, and AD-23. `AggregateActor` remains sole append coordinator, all event protection completes before staging, occurrence identity comes from trusted persistence context, read routes share one typed compatibility boundary, and existing no-op/default behavior remains valid until explicit registration.

**UX coverage:** No primary UX implementation. Streams/Admin endpoints and diagnostics retain authorization plus support-safe bounded readability outcomes; they never expose protected/plaintext-on-failure data, key/provider detail, or a false completion/readiness state.

**Dependencies:** Story 8.6 must be complete with exact predecessor chain, real-backend conformance evidence, and explicit authorization for Story 8.7. Any use of the isolated Azure environment for persisted integration remains under its recorded Security/Operations authority.

**Current reconciliation:** Story 8.7 is backlog and dependency-blocked. Server already registers `NoOpEventPayloadProtectionService` by default and calls typed protection/readability seams from event persistence, snapshots, aggregate rehydrate, publication, projections, Streams, and Admin. It lacks Story 8.2 v2 occurrence context, Story 8.5 reservation/completion/fence mechanics, Story 8.4 shared routing, and Story 8.6 backend registration; no v2 format has been written by the current implementation.

**Acceptance Criteria:**

**Given** Story 8.7 activation is requested
**When** predecessor and environment preflight runs
**Then** Story 8.1 digest, Story 8.2–8.6 source/evidence hashes, real-backend profile, state capabilities, current Server-hook inventory, authorization receipt, and permitted Azure evidence scope all match
**And** stale/missing evidence, an incompatible Server path, unapproved provider access, incomplete 8.4/8.5 behavior, or unresolved persistence atomicity ambiguity blocks integration.

**Given** payload protection registration is omitted or configured `Mode=Disabled`
**When** any supported environment starts
**Then** Server retains exactly one concrete `NoOpEventPayloadProtectionService`, current serialization/persistence/read behavior and package dependencies remain unchanged, no backend/policy/state/provider is contacted, and stale backend configuration produces only the approved safe informational event
**And** package/assembly presence, policy registration, provider options, Key Vault endpoint, dependency injection order, or previous deployment state cannot enable protection or create readiness/protection claims.

**Given** `AddEventStorePayloadProtection` is called
**When** closed top-level options and environment mode are validated
**Then** exact `Mode`, `Backend`, `WriteMode`, `LegacyWriterId`, value/cache/operation bounds and Disabled/Enabled, LocalDevelopment/AzureKeyVault, ReadOnly/CompatibilityLegacy/V2/RollbackLegacy matrix apply; explicit valid Enabled registration replaces the no-op with exactly one engine descriptor only after all startup validators succeed
**And** missing/unknown fields, multiple services, registration-order selection, non-exact Development, LocalDevelopment outside Development, absent adapter/legacy writer, unsupported mode, or failed backend/state/fence probe fails startup before traffic with no downgrade.

**Given** a typed command produces one or more events
**When** `EventPersister` serializes and protects them
**Then** trusted aggregate identity, persisted event type, aggregate-local sequence, payload kind, and current storage position context produce the exact Story 8.2 occurrence contract; every selected event is fully protected and has a durable `Reserved` key record/fence before any event/global-position state is staged, and only after all protection succeeds does the actor-owned atomic event save proceed
**And** caller extensions cannot supply/override AAD identity, a later global position is not guessed, one failed event cannot leave other events staged, input/result aliasing fails, plaintext fallback is forbidden, and domain processors/policies never write EventStore state.

**Given** actor event persistence succeeds, fails, is cancelled, or the process crashes around the boundary
**When** reservation completion executes or recovers
**Then** the exact completion lease/hook conditionally moves each referenced record `Reserved` to `Active` after confirmed durable save, releases leases, preserves stable operation identity, and schedules bounded reconciliation when a committed event references Reserved or completion is ambiguous
**And** a key record never follows its payload, persistence response alone cannot activate the wrong record, repeated completion is idempotent, no committed event is deleted/re-written, and unreferenced uncertainty is retained/quarantined rather than automatically deleted.

**Given** snapshot creation is due
**When** the approved engine protects it
**Then** the exact registered stable snapshot type id/alias and source-generated `JsonTypeInfo`, snapshot sequence, one-root manifest, v2 carrier, reservation/completion lifecycle, and provider/state rules are used; successful protected snapshots persist atomically under existing snapshot semantics
**And** protection failure remains advisory only when no plaintext/partial protected snapshot was staged and event save can complete independently, `throwOnFailure=true` still propagates, no plaintext snapshot fallback occurs, and a snapshot never proves event-history migration.

**Given** a stored protected, opaque, malformed, or unreadable snapshot is loaded or inspected
**When** snapshot routing executes
**Then** Story 8.4 authenticates/routes before deserialization, canonical readability evidence is recorded, unreadable protected state remains stored, and event replay begins only from a safe baseline or fails closed at its first unreadable event
**And** protected snapshots are not deleted as corrupt cache, exposed through Admin/manual inspection, accepted under an unknown CLR type, or used to skip lifecycle/erasure/provider checks.

**Given** aggregate rehydrate, publication/recovery, live/retry/rebuild projection, stream/replay API, Admin timeline/state, export, or restore reads durable events
**When** mixed-history routing runs
**Then** every path delegates to the same Story 8.4 typed reader with trusted occurrence context and current Story 8.5 lifecycle/backend evidence, stops at first unreadable sequence, preserves cancellation, and maps only bounded existing public/Admin outcomes
**And** no path privately parses/decrypts, publishes protected/partial plaintext, advances a projection/checkpoint, continues later events, exposes cross-tenant data, or normalizes provider/consistency failure into not-found/empty.

**Given** enabled mode starts or fleet authorization changes
**When** readiness and per-write admission are evaluated
**Then** exact spec/source/package-set/backend/read-format/write-mode capability leases, deployment generation, approval epoch/id, `ApprovedWriteMode`, `HighestWrittenFormat`/epoch, lifecycle, and provider/state readiness match; lease renewal loss makes readiness false and rejects traffic at the frozen bounds, and each protect rechecks authority before reservation
**And** static deployment inventory, startup-only check, stale lease/cache, operator config without durable fence, older/newer/ambiguous generation, or a v1-only reader in an open v2 generation cannot authorize a write.

**Given** `ReadOnly`, `CompatibilityLegacy`, or `RollbackLegacy` is configured
**When** protect/read operations execute
**Then** ReadOnly rejects every new protected/plaintext write while retaining approved readers; CompatibilityLegacy is allowed only before the v2 watermark with its exact writer; RollbackLegacy requires post-v2 approval, live v2 reader/backend, exact legacy writer, greater epoch, and retained watermark
**And** Enabled failure never falls to no-op/plaintext/LocalDevelopment/different provider/current key, `HighestWrittenFormat=V2` never decreases, and a v1-only/no-op binary refuses startup/traffic after v2 evidence.

**Given** provider/state becomes denied, missing/deleted, unavailable, stale, malformed, or ambiguous at runtime
**When** a write or read crosses the integration boundary
**Then** the approved typed outcome/retry/reconciliation behavior is preserved, readiness/traffic consequences are correct, current authorization and tenant scope still apply, owned buffers are zeroed, and durable state proves whether no write, reserved-only, committed, pending reconciliation, or unreadable occurred
**And** status/exception/log alone cannot claim persistence, protection, erasure, publication, projection, recovery, or rollback success.

**Given** current behavior and compatibility are validated
**When** source/package modes run without explicit engine registration and with each valid/invalid registration matrix
**Then** all existing providers/fakes/metadata/legacy histories compile and behave unchanged in default mode, Enabled modes remain additive, warnings-as-errors/public API guards pass, and no Phase 4 MVP host/consumer requires the future packages or credentials
**And** tests cannot globally replace the no-op, inject fake Azure as production proof, bypass DAPR/state/actor boundaries, or weaken existing protection/readability assertions to accommodate v2.

**Given** Story 8.7 completion is requested
**When** mode/startup matrices, event multi-write/cancellation/crash faults, reservation/completion/restart/reconciliation, snapshot create/load/fallback, every mixed-history Server path, tenant/authorization denials, fleet/fence/watermark changes, provider outages, restore admission, no-op preservation, and real persisted Azure-backed v2 evidence run
**Then** an immutable packet binds source/spec/predecessor identities, topology/configuration, exact stored event/snapshot/key/fence/checkpoint/audit/provider evidence, commands/results, limitations, and named EventStore/Security/Operations/Test approvals
**And** only Story 8.8 may be explicitly authorized; both new projects remain non-packable, the manifest remains 14, and no external release, production enablement, Parties mutation, rollback deletion, or G5 claim occurs.

### Story 8.8: Package And Release Integration

As an EventStore release owner,
I want the approved engine and Azure adapter released as one governed package-set transition,
So that optional security capability is reproducible and cannot partially publish.

**Requirements coverage:** Primary ownership of FR37's package/release-provenance slice; supporting NFR9–NFR11 build/release determinism, isolation, and completeness plus NFR16 package-only evidence.

**Architecture constraints:** AD-11, AD-12, and AD-23. `tools/release-packages.json` is the sole release inventory, both packages transition atomically under one version/provenance chain, dependency versions remain centralized, and external publication requires separate durable authority.

**UX coverage:** No direct UX requirement applies. Package documentation describes configuration and proven operational states without moving Parties UX/copy or representing package installation as protection/readiness/G5.

**Dependencies:** Story 8.7 must be complete with exact persisted Server/Azure evidence and explicit authorization for Story 8.8. Registry publication, release-environment use, signing/attestation, and any externally visible tag/package mutation additionally require the release owner's separately bound authority.

**Current reconciliation:** Story 8.8 is backlog and dependency-blocked. `tools/release-packages.json` currently lists 14 packages, release workflows/scripts/tests/docs treat it as authoritative, and several tracked assertions name that exact count. The two future projects do not exist yet and, through Story 8.7, must remain `IsPackable=false`; no approved engine/adapter package bytes or release evidence exists.

**Acceptance Criteria:**

**Given** Story 8.8 activation is requested
**When** predecessor and release-authority preflight runs
**Then** it verifies the Story 8.1 digest, complete Story 8.2–8.7 evidence chain, exact source/working-tree baseline, current 14-package manifest hash, selected central dependencies, release tooling identity, and explicit Story 8.8 authorization; any planned external mutation additionally binds its destination/version/commit/workflow credentials and owner receipt
**And** missing/stale evidence, uncommitted package inputs, one absent project, a failed real-backend/Server path, version ambiguity, or generic story approval blocks the package transition/publication respectively.

**Given** the two approved projects are ready
**When** the atomic inventory change is applied
**Then** both `Hexalith.EventStore.PayloadProtection` and `Hexalith.EventStore.PayloadProtection.AzureKeyVault` set `IsPackable=true`, join `Hexalith.EventStore.slnx`, and are added exactly once to `tools/release-packages.json` with canonical ids/paths so the manifest changes directly from 14 to exactly 16
**And** a 15-package intermediate state, adapter hidden in another package, engine without adapter, duplicate/renamed id, stale path, extra package, or independently versioned initial pair fails repository validation.

**Given** package and dependency metadata are inspected
**When** the two projects pack
**Then** IDs, common semantic version, descriptions, license/repository/readme/icon/source-link/symbol settings, target framework, nullable/AOT/trimming declarations where applicable, and inward dependency graph match approved contracts; all Azure/Microsoft/Hexalith versions remain in the central catalog and `.csproj` references are versionless
**And** no Server implementation, domain/Parties/UI assembly, development secret/configuration, local source path, project-reference artifact, preview/unapproved dependency, credential, test vault identity, or transitive adapter dependency leaks into the provider-neutral package.

**Given** every manifest package is built and packed from the exact source revision
**When** manifest-driven pack and validation tools run
**Then** expected `.nupkg`/`.snupkg` files exist exactly once at one version, archive paths/metadata/dependencies are canonical, symbols/source link map to the source SHA, package contents match allowlists, and raw-byte SHA-256 values are recorded for all 16 packages
**And** missing/extra/renamed/duplicate/mixed-version archives, local/project metadata, unsafe archive traversal, untracked generated bytes, warning, non-deterministic rebuild, or package hash mismatch fails the entire set.

**Given** release inventory authorities and documentation are scanned
**When** the manifest count and package list change
**Then** release workflow inputs, validators, governance tests, solution/project inventory, CI/package commands, central-version guidance, architecture/brownfield/reference/upgrade documentation, generated inventory/readme, and release notes all derive from or agree with the 16-package manifest
**And** no stale “14 packages,” hard-coded expected count, manual list, consumer-local version authority, or assistant entry point (`AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`) is edited or used as package inventory.

**Given** clean package-only consumers validate the new packages
**When** restore/build/runtime checks use only a staged feed with project/source references disabled and network sources constrained
**Then** consumers prove engine-only and engine-plus-adapter graphs, public API/contract compatibility, registration omitted/Disabled/invalid Enabled matrices, LocalDevelopment restrictions, exact package versions/hashes, G-001 compatibility, and one authorized real Azure wrap/persist/restart/read path using packaged assemblies
**And** a local project edge, build output probe, source fallback, cached unverified package, fake Azure, one-package install pretending to be production capable, or runtime requiring credentials while disabled cannot satisfy package proof.

**Given** supply-chain evidence is generated
**When** licenses, vulnerabilities, locks, SBOMs, provenance/attestations, signatures where repository policy requires them, and build identities are verified
**Then** each package and the 16-package set bind repository/source SHA, normative digest, version, workflow/build authority, dependency graph, hashes, build time, and staged/published destination with no unresolved policy violation
**And** missing/stale/mismatched SBOM/provenance, unapproved vulnerability/license exception, unsigned artifact where required, mutable ref, or evidence copied from source-mode output blocks release authority.

**Given** one package or release phase fails
**When** orchestration evaluates restore/build/test/pack/validate/sign/attest/publish/read-back
**Then** the set fails closed before later irreversible publication where possible, records exact per-package/phase disposition, and never reports a partial set as the EventStore release
**And** retry cannot repack different bytes under the same version, skip the adapter, overwrite an immutable artifact, publish NuGet after a failed prerequisite, or infer registry success without authenticated hash/read-back evidence.

**Given** an external release is not separately authorized or has not occurred
**When** Story 8.8 evidence is described
**Then** local/staged results are explicitly `evidence-ready` only and package IDs/versions are not claimed published, available, catalog-exposed, or consumed externally
**And** branch merge, release workflow presence, semantic-release plan, credentials existing, story completion, or package build does not authorize a tag, NuGet push, registry mutation, release record, or downstream dependency update.

**Given** separately authorized publication executes
**When** immutable provider read-back completes
**Then** all 16 exact package ids/versions/hashes plus symbols/provenance are present at the approved destination, the release/tag/source/workflow identities agree, no partial/foreign output exists, and any failure remains non-authorizing for consumer migration
**And** credentials, provider private responses, or signing material are not retained in evidence; a successful push command without immutable authenticated read-back is insufficient.

**Given** Story 8.8 completion is requested
**When** manifest/solution/inventory guards, Release build/test, deterministic two-pass pack, package validator, API/dependency/security scans, package-only consumers, real packaged Azure path, SBOM/provenance checks, and applicable external-publication read-back run
**Then** an immutable packet binds source SHA, normative/predecessor identities, 16-package manifest hash, package/version/raw hashes, tool/workflow identities, commands/results, destination/authorization state, limitations, and named Release/Security/EventStore/Test approvals
**And** Story 8.9 remains blocked until that exact packet explicitly authorizes consumer parity and the Parties maintainer separately authorizes its repository scope; no Parties edit, provider deletion, rollback-path removal, production enablement, or G5 claim occurs here.

### Story 8.9: Parties Dual-Provider Parity

As a Parties maintainer,
I want the retained local provider and shared engine proven against the same domain behavior,
So that migration does not transfer legal policy or erase the rollback path prematurely.

**Requirements coverage:** Primary ownership of FR37's named-consumer dual-provider parity slice; supporting NFR1–NFR4, NFR7, NFR12, NFR16, and NFR19 tenant-safe compatibility, failure, persisted evidence, and no-leak behavior.

**Architecture constraints:** AD-10, AD-12, AD-22, and AD-23. Parties owns its repository, policy, erasure orchestration, certificates/reports, and migration authorization; it consumes exact EventStore packages, retains its v1 path, and provides persisted parity evidence without EventStore taking a source dependency on Parties.

**UX coverage:** No EventStore UX implementation applies. Parties retains all domain legal-policy, Art.20/Art.30, erasure, certificate/report, and user-facing copy; parity must prove those outcomes stay truthful and accessible through the consumer's own requirements.

**Dependencies:** Story 8.8 must be complete with an exact approved package-set packet and explicit Story 8.9 authorization. Before any Parties read/change, its maintainer must separately authorize the exact repository, baseline SHA, files, test environment, branch/PR/mutation scope, and retained rollback boundary.

**Current reconciliation:** Story 8.9 is backlog and dependency-blocked. Parties is not a root-declared submodule and no checkout is authorized or modified here. Story 8.1 records historical consumer evidence at SHA `4378dede55d92e489caf7aad63d6c2892e6f856d`: a local `json+pdenc-v1` provider/reader, local `PersonalDataAttribute`, graph inspector, erasure/certificate state, and no production Key Vault SDK. That baseline must be reverified under fresh maintainer authority; it is not current mutation permission.

**Acceptance Criteria:**

**Given** Story 8.9 activation is requested
**When** EventStore and consumer authority preflight runs
**Then** it verifies the Story 8.1 digest, Story 8.2–8.8 evidence/package ids/versions/hashes/provenance, exact current Parties source SHA and clean scoped baseline, maintainer identity/authorization receipt, environment/data/provider permissions, intended branch/PR, and rollback retention agreement
**And** a historical SHA, EventStore approval, package availability, planning story, local checkout, contributor access, or inferred ownership cannot authorize Parties changes or test data/provider mutations.

**Given** Parties current protection, policy, erasure, export, and certificate paths are inventoried
**When** the approved historical Appendix B evidence is reconciled
**Then** every local `json+pdenc-v1` event/snapshot reader/writer, redacted path, marker/path discovery rule, key/erasure store, provider registration, test fixture, Art.20/Art.30 flow, certificate/report field, UI/copy owner, and failure behavior maps to retain, adapt, compare, or explicitly exclude with current source evidence
**And** drift is not silently normalized, completed local safeguards are not deleted/reimplemented, and a similarly named EventStore contract is not assumed equivalent without behavior/byte proof.

**Given** Parties adapts its policy to the shared engine
**When** the EventStore package is consumed
**Then** a Parties-owned adapter maps the exact local `PersonalDataAttribute` and natural-person rules to approved EventStore policy/path contracts with stable id `parties-personal-data-v1-compat`, deterministic precedence and selection, while erasure state maps through a Parties-owned `IErasureStateProvider` adapter with explicit absent-record/epoch semantics
**And** EventStore does not reference Parties, infer sensitivity/legal basis from names, replace Parties orchestration/certificates/reports, confuse the two marker types, or accept unknown/ambiguous selection/erasure state.

**Given** dual-provider configuration is introduced
**When** source/package and environment graphs are inspected
**Then** Parties consumes exact Story 8.8 packages/hashes with no EventStore project/source edge in package mode, retains the local v1 provider/reader/writer and fixtures, adds the shared v2 provider behind an explicit tenant/domain deployment fence, and exposes one selected writer plus all required readers per authorized mode
**And** package presence, DI order, broad feature flag, environment default, or rollout percentage cannot select a writer; no plaintext/no-op fallback, duplicate writer, or early local-path removal is allowed.

**Given** the same representative Parties object graphs and policies run through both providers
**When** selection/golden parity executes
**Then** selected canonical fields/subtrees and excluded/null/redacted values match the approved semantic parity matrix, legacy v1 bytes remain byte-readable by the retained reader, v2 bytes match EventStore goldens, and intentional v1/v2 cryptographic/path-format differences are documented rather than forced byte-equal
**And** a difference in selected personal data, tenant/scope identity, erasure state, natural-person rule, custom-converter handling, or unknown field fails parity before rollout.

**Given** retained legacy, v1, redacted, and new v2 events/snapshots coexist
**When** create/read/rehydrate/publish/project/rebuild/query/export/Admin-domain-equivalent and restart paths execute
**Then** every sequence routes through its recorded reader, resulting domain state and authorized observable outcomes agree, first unreadable data stops dependent work, and persisted event/snapshot/key/fence/checkpoint/provider evidence is correlated without content leakage
**And** status/mock/serialization-only evidence, snapshot success without event history, skipped unreadable records, checkpoint advance, cross-tenant match, or test-only provider cannot establish parity.

**Given** Parties erasure/legal workflows run under each provider
**When** Pending, Invalidating, Invalidated, Deleted, Unknown, Unavailable, Denied, restore, backup-copy, and retained-history states are exercised
**Then** domain orchestration, access/read/write gates, reports/certificates, Art.20/Art.30 outputs, audit attribution, and wording remain Parties-owned and truthfully distinguish online invalidation from final approved-copy expiry/destruction
**And** shared key mechanics, a primary-row/cache delete, Azure soft-delete, EventStore typed outcome, or dual-provider parity cannot make a legal decision or overclaim completed erasure.

**Given** provider/service/state failures are injected
**When** local and shared paths encounter missing/deleted/denied/unavailable/malformed/tampered/opaque data, stale cache, lifecycle regression, cancellation, crash/restart, or ambiguous mutation
**Then** each path follows its approved bounded behavior, preserves stable operation/evidence identity, emits compatible domain-safe outcomes where semantics agree, and keeps every persisted/consumer state recoverable or explicitly blocked
**And** one provider's permissive behavior cannot lower the shared contract, raw provider/local exception text cannot become UI/certificate/report copy, and failure never selects the other writer/plaintext silently.

**Given** no-leak and tenant-isolation evidence runs
**When** logs, traces, metrics, errors, Admin/support surfaces, exports, Art.20/Art.30 artifacts, audits, certificates/reports, test artifacts, and browser/UI outputs are scanned with the sentinel corpus
**Then** only explicitly authorized domain plaintext destinations and bounded safe identifiers/reasons contain permitted values, while protected/key/provider/credential/other-tenant data remains absent
**And** an authenticated/administrator role does not waive minimization, provider-private detail, cross-tenant denial, or evidence-redaction rules.

**Given** rollout, switchback, or test cleanup occurs
**When** maintainer-approved mutations are applied
**Then** exact configuration, package, deployment generation/fence, data/provider resources, evidence retention, and revert procedure are recorded; the local provider/readers/writer and rollback path remain available and tested after shared-provider proof
**And** the story does not delete local code/keys/fixtures, lower highest-written/lifecycle state, modify unauthorized Parties files, merge/push/deploy without separate authority, or treat a clean dual-provider run as G5.

**Given** Story 8.9 completion is requested
**When** package-only dependency proof, policy/field parity, all mixed event/snapshot/domain flows, erasure/legal/certificate/report outcomes, failure/rollback-switchback, tenant isolation, persisted state/provider evidence, and recursive no-leak scans run at the exact authorized Parties SHA
**Then** an immutable packet binds EventStore source/spec/package hashes, Parties repository/SHA/diff, provider configurations, commands/results, persisted evidence, limitations, and named Parties maintainer/Security/EventStore/Test approvals
**And** only Story 8.10 may be explicitly authorized; Parties changes remain subject to its own merge/deploy authority, the retained local path is not removed, and no package release, production rollout, erasure certificate, infrastructure deletion, or G5 claim exceeds the packet.

### Story 8.10: Post-v2-Write Rollback Rehearsal

As an operations owner,
I want rollback rehearsed after the newest durable format has been written,
So that rollback evidence proves history safety rather than only a pre-write DI switch.

**Requirements coverage:** Primary ownership of FR37's exercised post-v2 rollback slice; supporting NFR7 no-silent-loss, NFR12 durable compatibility, NFR16 persisted evidence, and NFR19 downgrade/rollback-after-newest-format safety.

**Architecture constraints:** AD-6, AD-11–AD-13, and AD-23. Immutable history is read in place, format/lifecycle watermarks never decrease, rollback retains all required v2 readers/backend/keys, and any consumer/deployment/provider mutation remains separately authorized and evidence-bound.

**UX coverage:** No EventStore UX implementation applies. Operator/Parties status, audit, reports, and support artifacts must distinguish rollback prepared, authorized, executing, verified, blocked, and failed without exposing payload/key/provider details or claiming erasure/migration.

**Dependencies:** Story 8.9 must be complete with exact EventStore package and separately authorized Parties dual-provider evidence that explicitly authorizes Story 8.10. Operations, Parties, Security, provider/IaC, deployment, test-data, and cleanup actions require exact environment/scope authority before execution.

**Current reconciliation:** Story 8.10 is backlog and dependency-blocked. No shared engine/adapter package, real v2 event/snapshot history, dual-provider Parties proof, v2 watermark, or approved rollback deployment exists in the current repository state. The Story 8.1 procedure is normative planning authority only; mock data or switching registration before any v2 write cannot satisfy this story.

**Acceptance Criteria:**

**Given** Story 8.10 activation is requested
**When** predecessor and rehearsal-authority preflight runs
**Then** it verifies the full Story 8.1–8.9 digest/evidence/package/consumer chain, exact EventStore and Parties SHAs, approved isolated environment and tenant/domain scope, provider/key/data/backup permissions, deployment identities, maintenance window, rollback/abort/cleanup plan, and named Operations/Parties/Security authorization receipts
**And** a plan, historical evidence, generic deployment access, unbound environment, production data/key, or story completion cannot authorize the rehearsal.

**Given** the pre-rollback baseline is prepared
**When** real shared-provider writes execute
**Then** at least one approved Parties aggregate persists and publishes a real `pdenc-v2` event and protected v2 snapshot through package-consumed production paths, advances `HighestWrittenFormat=V2`/epoch under the approved generation, projects/queries successfully, and records exact event/snapshot/key/index/lifecycle/fence/lease/audit/provider/checkpoint evidence before restart/re-read
**And** synthetic bytes injected directly into storage, mock provider, source reference, snapshot-only write, status code, or unpersisted golden cannot establish the irreversible baseline.

**Given** rollback is initiated
**When** the scoped tenant/domain is drained
**Then** new commands stop, in-flight writes/publications/lifecycle/provider operations and capability leases settle or reconcile, ambiguous states are resolved or block progress, and a content-bound before snapshot records all relevant durable/provider/deployment identities and counts
**And** rollback does not start while a write can race, an unreadable/unknown operation exists, evidence is incomplete, or draining silently discards queued/accepted work.

**Given** the rollback target is selected
**When** compatibility and custody are verified
**Then** the exact last-known-good v2-capable reader release, approved spec/fixture/package hashes, Azure read backend/identity, all required v2 wrapped records/KEK versions/lifecycle state, and exact retained `parties-pdenc-v1` writer/reader/fixtures are present and healthy
**And** a v1-only/no-op binary, missing key/version, disabled reader, alternate package bytes, unavailable backend, removed local path, or unverified backup dependency makes rollback blocked rather than selecting plaintext/read omission.

**Given** a rollback deployment generation is authorized
**When** fence/configuration transition executes
**Then** a fresh generation containing v2 readers plus the exact legacy writer opens, the previous generation drains for the lease window, and `ApprovedWriteMode=RollbackLegacy`, `LegacyWriterId=parties-pdenc-v1`, a greater approval epoch/id, and named operator authorization are committed atomically while `HighestWrittenFormat=V2`/highest epoch remain unchanged
**And** `Mode=Disabled`, `ReadOnly` presented as restored service, `CompatibilityLegacy` after v2, lowered/cleared watermark, old generation still serving, package removal, or provider/key deletion is rejected.

**Given** rollback mode is live
**When** retained mixed history is exercised
**Then** a stream containing legacy, v1, redacted where applicable, and real v2 events rehydrates in sequence; a new command writes the exact approved v1 format; persistence, publication/recovery, live/rebuild projection, query, authorized Admin/read path, audit, and restart/re-read all produce the expected domain state and exact stored formats/checkpoints
**And** v2 history remains authenticated through the Azure backend, no event is rewritten/re-encrypted/skipped, no projection checkpoint jumps unreadable data, and new plaintext/v2/unknown writer output is forbidden in RollbackLegacy.

**Given** a real protected v2 snapshot exists before rollback
**When** snapshot load and replay behavior are tested after rollback
**Then** the retained v2 reader authenticates/deserializes it under the exact registered type/context, and injected unreadable/opaque protected-snapshot cases remain stored, record bounded evidence, and fall back to canonical event replay only when all events are readable
**And** rollback does not delete/rebuild an unreadable protected snapshot to make the test pass, use plaintext fallback, or treat successful event replay as proof the snapshot was readable.

**Given** failure occurs during drain, generation/fence change, deployment, v2 read, legacy write, publication, projection, provider access, restart, or evidence collection
**When** the procedure responds
**Then** it stops at a named safe checkpoint, keeps irreversible watermarks/lifecycle/key state monotonic, preserves v2 readability prerequisites and retained legacy path, reconciles stable operations, records exact blocked/failed disposition, and follows the approved forward-fix or previous-v2-capable recovery path
**And** no retry blindly replays a mutation, no error downgrades to plaintext/no-op, no evidence gap is relabeled success, and provider outage is stated honestly as preventing v2-history availability rather than solved by rollback.

**Given** tenant isolation and no-leak controls are challenged
**When** wrong-tenant/context, tampered envelope, denied provider, stale cache, expired identity, malformed legacy/v2 data, cancellation, and sentinel-corpus cases run before/during/after rollback
**Then** each fails at the approved boundary with no state/checkpoint mutation, raw data disclosure, or authorization drift, and logs/traces/metrics/audits/evidence/reports expose only constructive safe fields
**And** maintenance/operator/administrator authority never permits cross-tenant decrypt, raw key/provider detail, protected bytes, payloads, credentials, or stack traces.

**Given** the rehearsal completes
**When** after-state is compared with the content-bound before-state
**Then** all expected events/snapshots/key records/indexes/lifecycle/fence/generation/lease/audit/provider versions/checkpoints and formats are accounted for, the new v1 write and old v2 history remain durable/readable across restart, residual resources and cleanup/retention state are explicit, and a fresh preflight is required to return to v2 writes
**And** the procedure does not remove engine/adapter/keys/local provider, claim data migration or erasure, authorize production rollout, or imply a future binary may drop v2 readers.

**Given** Story 8.10 completion is requested
**When** the authorized real post-v2 procedure, mixed-history domain flow, snapshot retention/fallback, fault/abort/recovery cases, tenant/no-leak controls, and independent before/after persisted/provider/deployment verification pass
**Then** an immutable packet binds exact EventStore/Parties SHAs, normative/package/configuration/provider identities, authorization receipts, timestamps, commands/results, raw evidence hashes, limitations, and named Operations/Parties/Security/EventStore/Test approvals
**And** only Story 8.11 may be explicitly authorized; rollback evidence does not remove the retained path, close G5, grant release/deployment/erasure authority, or permit any unapproved external mutation.

### Story 8.11: G5 Evidence And Approval Closure

As a platform security owner,
I want one content- and identity-bound G5 closure packet,
So that Parties migration can proceed only against a proven shared capability.

**Requirements coverage:** Primary closure ownership of FR37 and NFR19 for the complete optional payload-protection capability; supporting closure evidence for NFR1–NFR4, NFR7, NFR9–NFR12, and NFR16–NFR17 without replacing their primary epic owners.

**Architecture constraints:** AD-11–AD-13 and AD-23. Closure is derived from immutable source/package/provider/consumer/persisted evidence and authenticated approvals, never from story status; the decision performs no release, deployment, provider, consumer, or rollback-path mutation.

**UX coverage:** No EventStore UI implementation applies. Documentation and consumer/operator wording must truthfully distinguish disabled, configured, conformant, experimental, available/G5, unreadable, rollback, and erasure states while Parties retains legal-policy and user-facing UX/copy.

**Dependencies:** Story 8.10 and every prior Story 8.1–8.9 evidence packet must be complete, mutually consistent, and explicitly authorize Story 8.11. Named EventStore, Security, Release, Operations, Parties, and independent Test authorities must approve the exact final packet; missing authority is a valid non-closure result.

**Current reconciliation:** Story 8.11 is backlog and dependency-blocked. The current guide correctly describes only provider-neutral hooks/no-op behavior and states that the shared engine, `pdenc-v2`, production backend, and G5 are unavailable. No implementation/package/provider/Parties/rollback evidence exists today, and sprint status cannot change that. The required future output is `_bmad-output/implementation-artifacts/8-11-g5-evidence-and-approval-closure.md`.

**Acceptance Criteria:**

**Given** Story 8.11 activation is requested
**When** predecessor and evidence-root preflight runs
**Then** it verifies the unchanged approved Story 8.1 normative digest/authorization history plus exact immutable Story 8.2–8.10 packets, EventStore and Parties source SHAs, package/version/raw hashes/provenance, Azure/backend/environment identities, test-data scope, rollback transcript, and explicit Story 8.11 authorization
**And** missing, mutable, stale, rejected, superseded, path-escaping, digest-mismatched, untrusted, or incompatible evidence blocks closure before a packet can claim G5.

**Given** normative completeness is crosswalked
**When** Story 8.1 sections 2–16, appendices, accepted decisions/limitations, FR37/NFR19, AD-23, package/consumer/rollback gates, and V001–V138 are mapped
**Then** every obligation has exactly one owning story, exact artifact/hash, command/result/count, test tier/environment, disposition, reviewer, limitation, and retained evidence path; shared evidence links rather than duplicating or laundering ownership
**And** an unowned vector, open material finding, skipped/empty/quarantined required test, “not applicable” without normative basis, screenshot/status/mock/interface-only proof, or evidence from another source/package/provider identity prevents closure.

**Given** cryptographic and contract evidence is independently verified
**When** owner goldens, atomic inputs, path manifest, AAD, envelope, hashes, public APIs, compatibility matrices, bounds, parser/mutation/fuzz corpus, cancellation, owned-buffer zeroing, and recursive no-leak results are reproduced
**Then** approved Node/Python/.NET or other accepted independent toolchains agree byte-for-byte and every durable/public contract matches the frozen digest and released package surface
**And** self-round-trip, shared implementation masquerading as independence, normalized comparison, copied expected bytes, source-only success, or changed vector/baseline cannot close the cryptographic boundary.

**Given** implementation and persisted-path evidence is verified
**When** provider-neutral core, policy/key lifecycle, historical routing, Server event/snapshot integration, fleet fences/watermarks, crash/reconciliation, restore, publication, projections, APIs/Admin, and restart paths are cross-checked
**Then** exact stored event/snapshot/key/index/lifecycle/fence/generation/lease/operation/audit/checkpoint records prove successful and failure outcomes through production code with tenant isolation and no silent loss
**And** an HTTP result, log, metric, mock call, configuration, one state row, or UI display cannot substitute for coordinated persisted/provider end-state evidence.

**Given** Azure production-backend evidence is independently inspected
**When** service/profile/custody, key attributes/version, managed identity/RBAC, private networking, SDK/API/packages, state capabilities, wrap/unwrap/restart/rotation/rewrap, fault classifications, cache invalidation, provider audit, resource retention/cleanup, and no-leak artifacts are validated
**Then** they bind to the same source/spec/package/environment/test interval using redacted immutable resource identities and prove the selected ordinary Premium RSA-HSM-3072 profile
**And** a mock/emulator/LocalDevelopment/software key/public endpoint, broad or unverifiable permission, production data/key, unresolved retained resource, or provider state inferred from configuration/status blocks production-conformant/G5 wording.

**Given** package and release evidence is independently inspected
**When** the 16-package manifest, solution/inventory, common version, `.nupkg`/`.snupkg` hashes, dependency graph/locks, package-only consumers, SBOM, license/vulnerability results, provenance/attestation/signature policy, and applicable immutable provider read-back are compared
**Then** both new packages and the full set bind to the same approved source/workflow/release identity with no project/source fallback or partial publication
**And** staged/evidence-ready artifacts are not described as externally available, a 15-package/one-package/mixed-version release fails, and missing external release authority/read-back remains an explicit availability blocker rather than inferred success.

**Given** Parties consumer evidence is independently inspected
**When** exact repository/SHA/package hashes, maintainer authority, policy/field parity, erasure adapter, retained v1 reader/writer/fixtures, dual-provider selection/fences, mixed event/snapshot/domain flows, Art.20/Art.30 and certificate/report truthfulness, tenant isolation, failure/no-leak, and retained rollback path are validated
**Then** consumer behavior and ownership remain compatible with the shared capability at the exact approved identity and no EventStore-to-Parties dependency or unapproved migration exists
**And** EventStore evidence, a package reference, a clean unit test, or dual-provider configuration cannot substitute for separately authorized persisted Parties proof.

**Given** the post-v2 rollback packet is independently inspected
**When** before/after event/snapshot/key/provider/fence/watermark/checkpoint/audit state, drained generations, exact RollbackLegacy writer, mixed-history read, new v1 write, publish/project/rebuild/query/Admin/restart, failure recovery, and retained v2 reader/backend/key/local path are reproduced
**Then** rollback is executable after real v2 writes without decreasing `HighestWrittenFormat=V2`, rewriting history, losing data, exposing plaintext, or stranding snapshots/events
**And** a pre-write DI switch, ReadOnly mode, mock data/provider, removed package/key/local path, or unverified provider outage behavior cannot satisfy rollback safety.

**Given** documentation and operational guidance are reconciled
**When** `docs/guides/payload-protection-and-crypto-shredding.md`, package/readme/generated inventory/release notes, deployment/configuration, identity/RBAC/network, rotation/rewrap, outage, cache/lifecycle, backup/restore, erasure-evidence, rollout, rollback, troubleshooting, and support-safety content are reviewed
**Then** they describe only behavior proven by the exact packet, retain disabled/no-op and historical compatibility guidance, identify irreversible watermarks/limitations/owner boundaries, and use commands/ids/options matching released artifacts
**And** stale “unavailable” wording is updated only if availability proof passes, while docs never call package presence/configuration, online invalidation, Azure soft-delete, or G5 complete erasure/legal compliance or permission to remove retained readers.

**Given** known limitations and residual risks are reviewed
**When** value/field bounds, whole-snapshot protection, v1 no-AAD history, shared-KEK blast radius, Azure latency/cost/quota/outage, managed-memory zeroing limits, retained backups/copies, no full downgrade after v2, and experimental/unavailable states are dispositioned
**Then** each has an accountable owner, accepted/rejected decision, operating control, monitoring/review trigger, and exact effect on availability/G5
**And** open material risk, unaccepted limitation, unknown protected copy, expired approval, or a qualification omitted from docs/packet keeps the decision non-authorized.

**Given** the final immutable packet is assembled
**When** structure and tamper/path validation run
**Then** `_bmad-output/implementation-artifacts/8-11-g5-evidence-and-approval-closure.md` binds exact schema/version, normative digest, source/package/provider/consumer/deployment identities, indexed evidence paths and raw hashes, UTC commands/results/counts, findings/limitations, approval subjects, and deterministic decision inputs with no symlink/reparse/path traversal or mutable external reference
**And** raw payload/key/credential/provider-private/tenant data is absent, evidence cannot be spliced across identities, and changing any bound artifact invalidates the decision.

**Given** final approval is requested
**When** the EventStore owner, named Security Reviewer, Release owner, Operations owner, Parties maintainer, and independent Test/Vector reviewer each authenticate an `Approved` or `Rejected` decision for the same packet hash with UTC timestamp, evidence reference, scope, and bounded conditions
**Then** unanimous required approval with no open blocker yields `G5 available` and permits Epic 8 closure/Parties G5 status transition under the exact recorded scope; any other state yields a deterministic non-authorized verdict naming blockers
**And** group aliases, author self-approval, implied silence, issue/story status, approval of an earlier hash, majority vote, or an approval conditioned on missing future evidence cannot close G5.

**Given** the G5 decision is recorded
**When** downstream authority is evaluated
**Then** it states precisely which shared capability/package/provider/consumer identities are available and which retained paths/limitations remain, and any later drift requires revalidation under the packet's rules
**And** the decision itself performs or authorizes no package publication, registry mutation, Azure provisioning/key deletion, deployment/production enablement, Parties merge/edit, local-provider/reader removal, backup destruction, erasure certificate, or other external mutation without its separate owner-bound workflow.

**Given** Story 8.11 completion is requested
**When** independent crosswalk, cryptographic, persisted Server, Azure, release/package-only, Parties, post-v2 rollback, documentation, limitation, no-leak, immutable-packet, and approval validation all pass
**Then** the packet records the reproducible final verdict; only a fully approved `G5 available` verdict closes FR37/NFR19 and Epic 8, while a non-authorized verdict leaves exact blockers open without weakening prior evidence
**And** no hidden exclusion, stale identity, skipped required proof, ambiguous approval, overclaim, legacy-path deletion, or unbound external action is accepted.

<!-- Epic 8 story set confirmed complete for planning. -->
