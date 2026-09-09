---
id: SPEC-eventstore-phase-4-readiness-recovery
companions:
  - ../../planning-artifacts/architecture.md
  - ../../planning-artifacts/prd.md
  - ../../planning-artifacts/epics.md
  - ../../planning-artifacts/story-id-migration-2026-07-15.md
  - ../../planning-artifacts/story-id-migration-2026-08-01.md
  - ../../project-context.md
  - architecture-reconciliation.md
  - requirements-traceability.md
  - readiness-gates.md
  - glossary.md
sources:
  - ../../planning-artifacts/implementation-readiness-report-2026-07-15.md
  - ../../planning-artifacts/sprint-change-proposal-2026-07-15.md
  - ../../planning-artifacts/sprint-change-proposal-2026-07-20-oq8-durable-idempotency-admission.md
  - ../../planning-artifacts/implementation-readiness-report-2026-08-01.md
  - ../../planning-artifacts/sprint-change-proposal-2026-08-01.md
  - ../../planning-artifacts/sprint-change-proposal-2026-08-16.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete,
> preservation-validated contract for what to build, test, and validate. Source documents listed
> in frontmatter are for traceability only.

> **Current verdict: BLOCKED for implementation readiness and production authority.** Existing
> spec and story identities remain valid, but `epics.md` has not reconciled AD-26 through AD-33 and
> still contains rules superseded by amended AD-16 and new AD-28. Production promotion, traffic,
> consumer migration, and readiness claims are separately blocked by AD-26 until its content-bound
> profile and every applicable evidence gate pass.

# EventStore Phase 4 Implementation Readiness Recovery

## Why

Hexalith.EventStore Phase 4 must turn a working DAPR-native event-sourcing platform into a safe,
reusable developer platform. Domain authors need platform seams instead of copied infrastructure;
operators need fail-closed identity, delivery, and evidence contracts; and every implementation
spec must follow the same architecture before readiness or production authority can be claimed.

## Capabilities

- **CAP-1**
  - **intent:** Domain authors can build EventStore-backed modules with domain code only while the platform supplies hosting, query, projection, read-model, cursor, telemetry, health, orchestration, packaging, identity, and routing seams.
  - **success:** Sample and Tenants preserve behavior through platform seams; Development-only routing overrides and Redis evidence are never represented as production authority; consumer infrastructure remains until exact parity and Consumer-owner removal evidence pass.

- **CAP-2**
  - **intent:** External API developers can expose typed generated REST endpoints in dedicated API hosts while interactive UI hosts consume client libraries directly.
  - **success:** Generated controllers delegate to `IEventStoreGatewayClient`, query evidence keeps its route provenance, command status uses `MessageId`, and every downstream hop preserves the first-boundary correlation identity and activated catalog route.

- **CAP-3**
  - **intent:** Maintainers can release reproducibly with references-based submodules, deterministic package mode, dedicated live-sidecar coverage, shared security workflows, and manifest-governed output.
  - **success:** Release validation cannot publish submodule packages, every candidate has one validated AD-11 identity and provenance chain, and candidate publication is never treated as AD-26 production authority.

- **CAP-4**
  - **intent:** Operators and consumers can trust event identity, idempotency admission, fencing, replay, dispatch, append behavior, global positions, and crash recovery under duplicates, concurrency, and failures.
  - **success:** Tests prove stable CloudEvent identity, exact duplicate results, fail-closed admission and migration, one current nonzero fence for every production aggregate side effect, replay equivalence, stored-but-unpublished recovery, and a shared routing/idempotency catalog fingerprint.

- **CAP-5**
  - **intent:** Public, internal, domain-service, projection-notification, Admin, and generated REST surfaces fail closed and preserve tenant isolation.
  - **success:** Every boundary uses one explicit canonical tenant; every non-Development DAPR app endpoint authenticates the app channel; Production always rejects symmetric JWT mode; and neither caller app ID, mTLS, ACLs, `system` subjects, nor inferred wildcards create tenant or administrator authority.

- **CAP-6**
  - **intent:** Long-lived streams can evolve with bounded snapshot and projection cost, sequence-safe projection updates, event versioning and upcasting, validated identity metadata, and cancellation-aware public seams.
  - **success:** The shipped projection-dispatch carrier and persisted result matrix, replay-equivalent paged rebuild baseline, and each cost/evolution specification are proven before dependent optimizations or format changes proceed.

- **CAP-7**
  - **intent:** Operators get explicit delivery semantics, recoverable poison handling, attributable Admin actions, honest unavailable-operation behavior, hardened deployment posture, and persisted evidence.
  - **success:** Subscriber acknowledgement follows durable capture; Admin mutations preserve bounded human and service attribution through recovery; `eventstore-operations` remains unavailable until its production gates pass; and the consolidated Admin UI presents only support-safe, evidence-backed states.

- **CAP-8**
  - **intent:** Phase 4 has one coherent planning and implementation baseline.
  - **success:** Stable spec, capability, AD, epic, and story identities remain traceable; every AD-1 through AD-33 consequence has an implementation owner and evidence gate; stale or contradictory artifacts grant no completion or production authority; and a fresh readiness assessment passes the reconciled set.

- **CAP-9**
  - **intent:** EventStore can provide an optional reusable byte-stable payload-protection engine while domains retain legal policy and operators retain key custody.
  - **success:** Stories 8.2-8.11 satisfy their predecessor and evidence gates, and payload-key invalidation remains one separately reported AD-30 erasure facet rather than proof of full erasure.

## Constraints

### Authority and readiness

- The PRD owns FR/NFR truth; `architecture.md` owns component, integration, topology, and AD gates; UX owns interaction rules; `epics.md` owns story slicing and handoff.
- The governing architecture has SHA-256 `2678116099e3d1c1f68ee38ef344b9bef5a58a82062a800e5a89b8b0f5774395`; its decision-authority memlog has SHA-256 `5b6fa6ec60261de4be496a8350b048e8cf5381d6cd0a8681cc475e69cbd4f793`. Exactly AD-1 through AD-33 govern; later wording under an existing ID supersedes earlier wording without changing the ID.
- Prior specifications and evidence retain their identities and historical conclusions only within the environment and claim they actually proved. Architecture changes narrow future authority; they do not rewrite historical bytes or silently promote a completed story.
- AD-26 selects one self-managed Kubernetes production profile: independent DAPR sidecars, `statestore` using stable `state.postgresql` v1 with `actorStateStore: true`, the production resiliency policy, an approved durable broker, AD-24 OpenBao, and OQ8 profile `oq8-postgresql-v1`. Redis is Development/test only; Cosmos and other templates are non-authorizing alternatives.
- The Platform deployment owner must publish one canonical `deploy/dapr/production-profile.yaml` whose retained-byte digest binds runtime, topology, components, identities, ACLs, resiliency, OpenBao, catalogs, restore posture, and required evidence. A separately authorized candidate can produce evidence but cannot authorize production.
- Story 3.13 remains the rejected `v3.94.1` disposition; Story 3.14 owns a separately authorized corrective release; Story 3.15 remains the independent positive deployed-runtime gate. None authorizes deployment or consumer removal by status alone.
- `architecture-reconciliation.md` records the PRD and epic changes still required. Until those owners reconcile and validate their artifacts, the architecture governs any disagreement and readiness remains blocked.

### Runtime, identity, and delivery

- EventStore remains a DAPR-backed hexagonal event-sourcing platform: the gateway is the policy edge, `AggregateActor` is the sole append coordinator, and domain services remain pure domain adapters.
- Every production aggregate mutation or side effect requires a nonempty opaque idempotency key, successful AD-25 admission, and the current nonzero fence before domain, persistence, projection, audit, provider, repository, or scheduling work. Unfenced compatibility seams fail closed or remain unmapped outside Development.
- AD-25 and AD-33 share one deployable `deploy/dapr/eventstore-routing-catalog.json` envelope. Stable route-entry IDs join command/query/projection routing and idempotency facets under one root digest and generation; activation is prepare/ready/commit with rollback to the prior complete generation.
- Commands and queries map by `(Domain, MessageType)` and projections by `(Domain, ProjectionType)` to one app ID, method, and contract version. Exact keys precede bounded cataloged fallbacks; duplicate, missing, ambiguous, partial, untrusted, or mismatched entries fail readiness, and runtime overrides are Development-only.
- Read models use `IReadModelStore` plus `ReadModelWritePolicy`; cursors use `IQueryCursorCodec` plus `QueryCursorScope`; projection lifecycle, opaque route-bound version tokens, and rebuild checkpoints remain persisted authorities.
- `ProjectionDispatchResponse` v2 with `ProjectionDispatchOutcome` is the only cross-service projection carrier. The server owns the persisted normalized per-route outcome and checkpoint matrix; no separate public `ProjectionDispatchResult` family is implied.
- Pub/sub is at-least-once and unordered. A poison or terminally rejected message is acknowledged only after the AD-31 tenant/domain durable sink accepts a record keyed by stable `MessageId`; unknown, failed, conflicting, or unretainable capture remains retryable or enters separately proven durable quarantine.
- `X-Correlation-ID` is 1-128 ASCII alphanumeric or hyphen characters. The first public boundary accepts or mints it; downstream hops propagate it unchanged or reject an invalid replacement. It is not a GUID, `traceparent`, or command-status identity.
- AppHost, DAPR YAML, profile/catalog fingerprints, app IDs, component and secret scopes, ACLs, resiliency, topics, subscriptions, deployment documentation, and topology tests change as one slice.

### Security and operations

- Every HTTP host installs the platform authenticated `FallbackPolicy`. Only `/health`, `/alive`, and `/ready` carry explicit `AllowAnonymous` metadata and support-safe responses.
- The shared JWT contract owns issuer, audience, signature, lifetime, roles, tenant validation, 60-second skew, HTTPS metadata, and the closed algorithm allowlist. Production rejects symmetric mode even when break-glass is enabled; HS256 is limited to Development or an explicitly enabled environment that is neither Development nor Production.
- `Contracts` owns tenant canonicalization. Every boundary requires exactly one explicit request tenant and matching `eventstore:tenant` grant, normalized lowercase to 1-64 characters using `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`; missing, duplicate, conflicting, invalid, public `system`, or inferred wildcard scope fails before routing or state access.
- Every non-Development DAPR app endpoint validates exactly one `dapr-api-token` against startup `APP_API_TOKEN` through shared constant-time middleware. The token authenticates the channel, not claimed caller identity; sidecar-established attribution plus catalog and ACL authorization are still required.
- Admin mutations preserve either the authenticated operator or a validated bounded delegation binding human subject, service principal, tenant, operation, reason, request, correlation and message IDs, issuer, and expiry. Mutation and audit use resumable prepare/effect/commit/recovery states; audit failure cannot silently permit the effect.
- AD-24 production secrets use one value-free OpenBao contract, DAPR Secrets API access, `auth.secretStore: openbao`, `secretKeyRef`, default-deny grants, TLS verification, required-secret readiness, and acknowledged rollout-based rotation. Digest-key retirement also requires the AD-25/AD-33 catalog to prove zero live references.
- `Hexalith.EventStore.Operations` owns poison capture and replay under app ID `eventstore-operations`, but remains non-production until AppHost/deployment wiring, immutable release identity, AD-28 authentication, AD-29 audit, tenant/target validation, common catalog fingerprint, and capture-before-ack evidence pass.
- Sensitive payloads, plaintext, ciphertext, tokens, credentials, key material, unbounded claims, and PII never enter telemetry, support output, or evidence; tenant IDs are not metric labels.

### Erasure, release, and planning boundaries

- AD-7 owns only typed, idempotent, read-back-proven MVP projection read-model/checkpoint removal. It makes no GDPR, event, broker, backup, audit, or cryptographic-erasure claim.
- AD-30 is a distinct post-MVP domain-policy-owned workflow. It freezes the canonical subject scope and records logical, projection, cryptographic, broker, backup, restore-point, cache, export, replica, and legal-hold facets separately; overall completion is impossible while any required facet is pending, unknown, or failed.
- AD-23 payload protection remains optional and separately gated. OpenBao operational secrets are not `pdenc-v2` KEK custody, and key invalidation alone is not complete erasure.
- Release remains manifest-governed; canonical retained bytes, exact OCI lineage, provenance, platform smokes, release authority, parity approval, deployment authority, and Consumer-owner removal authority remain separate fail-closed decisions.
- Cost, sequence, snapshot, and upcaster changes remain spec-first. Existing paged-rebuild correctness remains binding, but Redis proof is Development/test evidence and cannot satisfy AD-26 production readiness.
- `Hexalith.EventStore.Admin.UI` remains the only EventStore UI host. FrontComposer and Fluent UI versions come from the live Builds catalog; a dated literal in a specification is not package authority.
- The dated story-migration crosswalks preserve existing story identities and evidence. A split or amended story inherits no new completion claim without focused evidence and its required owner approvals.
- Use `Hexalith.EventStore.slnx` for restore/build, run tests per project, keep package versions centralized, preserve ULID-safe envelope IDs, and do not recurse or modify submodules without explicit approval.
- AOT and trimming remain unsupported while reflection conventions are load-bearing.

## Non-goals

- Do not implement or authorize production deployment, traffic, consumer migration, release, publication, secret creation, data deletion, submodule changes, or cross-repository mutation in this planning update.
- Do not reduce Phase 4 scope, renumber any capability, AD, epic, story, or existing implementation specification, or rewrite historical evidence to match newer architecture.
- Do not treat Story 1.14 projection cleanup, payload-key invalidation, a dead-letter publish attempt, a candidate image, or a catalog file's presence as its broader completion claim.
- Do not create a second EventStore UI host, move generated controllers into interactive UI hosts, or present accepted/notification/deferred outcomes as projection-confirmed success.
- Do not target AOT/trimming while reflection conventions remain load-bearing.

## Success signal

A fresh readiness assessment can trace every FR1-FR37 and NFR1-NFR19 obligation through one
governing AD-1 through AD-33 baseline to stable implementation-spec identities, unambiguous epic
owners, and persisted evidence. Production authority is granted only when the exact AD-26 profile,
AD-33 catalog generation, workload authentication, tenant, delivery, attribution, recovery, and
release gates all pass without relying on Development-only evidence.

## Assumptions

- Existing implementation-spec files remain historical records; reconciliation adds governing constraints and follow-up ownership rather than rewriting their completed evidence sections.

## Open Questions

- Which durable broker, retention/RTO/RPO and restore posture, OpenBao production topology, and telemetry exporter/cardinality budgets will Platform Operations bind into the AD-26 profile?
