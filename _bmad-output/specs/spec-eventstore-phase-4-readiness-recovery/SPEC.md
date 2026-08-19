---
id: SPEC-eventstore-phase-4-readiness-recovery
companions:
  - ../../planning-artifacts/architecture.md
  - ../../planning-artifacts/prd.md
  - ../../planning-artifacts/epics.md
  - ../../planning-artifacts/story-id-migration-2026-07-15.md
  - ../../planning-artifacts/story-id-migration-2026-08-01.md
  - ../../project-context.md
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

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability only.

> **Current handoff verdict: BLOCKED.** Implementation handoff remains blocked until one
> content-addressed planning set proves that `epics.md`, the active Story 3.13 specification, new
> Story 3.14 and 3.15 specifications, and `sprint-status.yaml` encode the approved split together.

# EventStore Phase 4 Implementation Readiness Recovery

## Why

Hexalith.EventStore Phase 4 must turn a working DAPR-native event-sourcing platform into a safer reusable developer platform before full implementation resumes. The need is driven by both product goals and implementation readiness: domain authors need platform-owned seams instead of copied boilerplate, operators need fail-closed trust and persisted evidence, and the planning set must stop using `epics.md` as a proxy for PRD, architecture, UX, and implementation slicing.

For deployed-runtime parity, the final 2026-08-16 PRD and architecture take precedence over stale
epic, story specification, and sprint tracking text. Story 3.13 is the rejected, non-authorizing
`v3.94.1` disposition; Story 3.14 owns a separately authorized corrective release; only Story 3.15
owns independent positive deployed-runtime parity closure.

The Story 4.8 evidence ledger and implementing Stories 4.9-4.15 are governed first by the approved 2026-07-20 OQ8 proposal and the
OQ8 design version 1.0.0 approved by Architecture, Security, and Test (SHA-256
`1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8`),
then by this reconciled SPEC package. Pre-change FR27/NFR7/NFR16 and
architecture wording is historical context only.

## Capabilities

- **CAP-1**
  - **intent:** Domain authors can build EventStore-backed domain modules with domain code only while EventStore libraries supply hosting, query, projection, read-model, cursor, telemetry, health, Aspire, packaging, and consumer-parity seams.
  - **success:** Sample and Tenants adoption preserve domain behavior, production-path evidence proves the generic replacement seams, and consumer infrastructure remains until one unchanged content-addressed packet proves every applicable source, package, and deployed identity and a separate authenticated Consumer-owner receipt authorizes the exact removal subject.

- **CAP-2**
  - **intent:** External API developers can expose typed generated REST endpoints in dedicated API hosts while interactive UI hosts consume client libraries directly.
  - **success:** Generated controllers delegate to `IEventStoreGatewayClient`, Sample/Tenants UI hosts contain no generated or hand-written per-message MVC command/query controllers, and handler-computed or unknown responses never claim projection-confirmed state.

- **CAP-3**
  - **intent:** Maintainers can release reproducibly with references-based submodules, deterministic package mode, dedicated live-sidecar coverage, shared security workflows, and manifest-governed package output.
  - **success:** Release validation cannot publish submodule packages, CI separates deterministic release-gate tests from live-sidecar tests, and every candidate is bound by the AD-11 canonical release identity and provenance contract; any corrective publication remains separately authorized.

- **CAP-4**
  - **intent:** Operators and consumers can trust event identity, trusted tenant/key admission, fencing, exact replay, terminal expired-key precedence, replay/tombstone separation, replay dispatch, append behavior, global-position semantics, and crash recovery under duplicates, concurrency, and failures.
  - **success:** Tests prove CloudEvent id stability, duplicate result fidelity, stale pipeline rejection, trusted semantic equivalence, one current fence, inclusive expiry, minimal tombstone compaction, rotation/migration fail-closed behavior, stored-but-unpublished recovery, and multi-host production-path durable evidence with exactly one eligible execution and zero later duplicate side effects.

- **CAP-5**
  - **intent:** Public, internal, domain-service, projection-notification, admin, and generated REST surfaces fail closed and preserve tenant isolation.
  - **success:** Anonymous and cross-tenant admin access fails, production auth rejects insecure modes unless break-glassed, committed config contains no forgeable or operational secrets, and production evidence proves fail-closed required-secret handling plus matching default-deny DAPR scopes and OpenBao ACLs.

- **CAP-6**
  - **intent:** Long-lived streams can evolve with bounded snapshot/projection cost, sequence-safe projection updates, event versioning/upcasting, validated identity metadata, and cancellation-aware public seams.
  - **success:** The exact bounded `ProjectionDispatchResult` and replay-equivalent paged rebuild baseline are proven before folded snapshot, projection cost/sequence guard, and event versioning/upcasting specs authorize dependent implementation.

- **CAP-7**
  - **intent:** Operators get explicit delivery semantics, poison/dead-letter handling, attributable admin actions, honest unavailable-operation behavior, hardened deployment posture, meaningful higher-tier evidence, and tracked future capability backlog.
  - **success:** `Hexalith.EventStore.Admin.UI` is the single consolidated FrontComposer-based EventStore UI; unavailable operations are hidden, disabled, or return `501`; audit records remain support-safe; integration tests assert persisted evidence; operational secrets satisfy AD-24 readiness, runtime-failure, acknowledged-rotation, and real-OpenBao evidence gates; and four independently governed backlog artifacts exist.

- **CAP-8**
  - **intent:** Phase 4 has a coherent planning baseline before full implementation resumes.
  - **success:** The eight-epic plan keeps Stories 1.20 and 3.12 complete, records Story 3.13 as the rejected `v3.94.1` disposition, assigns separately authorized correction to Story 3.14 and independent positive parity to Story 3.15, keeps Epic 3 open, replaces oversized Stories 4.8 and 8.2 with focused children, and updates epics, story specifications, and sprint tracking atomically before a fresh readiness assessment permits implementation handoff.

- **CAP-9**
  - **intent:** EventStore can provide an optional, reusable, byte-stable payload-protection engine with fail-closed cryptographic and key-lifecycle behavior for Parties and later consumers.
  - **success:** Stories 8.2-8.11 execute only after Story 8.1 authorization and their immediate predecessors, prove FR37/NFR19 contracts through goldens, core crypto, compatibility, policy/key lifecycle, production adapter, server, package, Parties, rollback, and G5 closure, and leave Parties Story 8.7 blocked until Story 8.11 records an approved `available` packet.

## Constraints

### Authority and handoff

- PRD owns FR/NFR truth and readiness traceability; architecture owns component, integration, topology, and decision-record gates; UX owns UI governance and flows; `epics.md` owns story slicing, sequencing, acceptance criteria, and implementation handoff.
- The finalized `architecture.md` companion is the architecture contract; its memlog is the decision authority. This run binds architecture SHA-256 `9a20ba5c6860f124ca52a8801e531132a96dd0a761856fdc4684390d848f4101` and memlog SHA-256 `3b20c450f7c105b1cedb1d9862b5e6a10e3968e57dcb1698a47a52779d3abedb`. Preserve AD-1 through AD-25; later amendments within an ID supersede earlier wording.
- For deployed-runtime parity, the final 2026-08-16 PRD and architecture override stale `epics.md`, Story 3.13-3.15 specifications, and sprint-tracking text. No stale artifact grants positive `v3.94.1` authority.
- Story 3.13 may complete only on the exact content-bound `rejected-non-authorizing` tuple and authenticated rejection receipts defined in `readiness-gates.md`; it never closes positive FR36 parity.
- Story 3.14 requires complete AD-11 conformance and a separate authenticated, durable, one-use authority reserved to one run and attempt. Partial publication remains immutable non-authorizing evidence; retry requires a new semantic version and authority. This SPEC grants no publication authority.
- AD-11 requires one canonical `ReleaseIdentity` and versioned `ReleaseEvidenceCodec` over exact source, workflow, authority, package, raw OCI graph, provenance, and two-platform smoke evidence. Canonical UTF-8 bytes are hashed without reserialization; missing or mixed lineage fails closed.
- Story 3.15 independently derives one exact new AD-11 lineage and may close only on explicit `deployed_runtime_parity: available` plus the unchanged content-addressed subject and authenticated, valid triad receipts defined in `readiness-gates.md`.
- AD-22 parity approval does not authorize deletion. Consumer removal requires trusted nonempty applicable modes and the fully bound, authenticated, valid Consumer-owner `consumer-removal-authorized` receipt defined in `readiness-gates.md`.
- Implementation handoff requires the exact content-addressed planning set, Story 3.13 key rule, and story-status transition rules in `readiness-gates.md`; any missing, duplicate, partial, stale, or mixed-version artifact remains blocked.
- Full Phase 4 implementation must not resume until the atomic planning-set verifier passes, all other PRD, architecture, UX, story-split, and high-risk NFR gates are reconciled, and a fresh readiness verdict clears the block.

### Runtime and correctness

- The platform remains DAPR-backed hexagonal event sourcing: the EventStore gateway is the policy edge, DAPR actors own aggregate write serialization, domain services are pure domain adapters, and external adapters call platform seams.
- Generated REST controllers live only in dedicated external API hosts and delegate to `IEventStoreGatewayClient`; interactive UI hosts consume EventStore Client libraries directly and host no generated or hand-written per-message MVC command/query controllers.
- `AggregateActor` owns durable event mutation; domain code returns `DomainResult` and never writes EventStore state directly.
- AD-25 admission precedes `AggregateActor`: authentication, current authorization, and canonical validation precede a trusted adapter; the tenant/key admission directory selects exactly one canonical actor; and only its current non-zero fence may cross a protected side-effect boundary.
- Public requests carry only the opaque idempotency key. Canonical intent and fixed retention are server-owned; raw keys and protected intent never enter persistent or diagnostic surfaces.
- Mutation replay retention is exactly 86,400 seconds and commit replay retention uses `DateTimeOffset.AddYears(7)`. Inclusive expiry atomically compacts to the approved fence-free minimal tombstone; unresolved states never age into fresh work.
- Unavailable, corrupt/collision, unknown-version, ambiguous/uninventoried legacy, and unsafe promotion state fail closed. Directory-mediated rotation and versioned legacy migration preserve one canonical authority or remain blocked.
- Story 4.4 retains committed-event publication recovery. Story 4.8 is the non-executable evidence ledger; Story 4.14 produces `_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml` against the `oq8-postgresql-v1` multi-host DAPR profile and Story 4.15 owns EventStore platform closure and handoff. Folders owns canonical cross-repository OQ8 evidence and final closure.
- Read models use `IReadModelStore` plus `ReadModelWritePolicy`; cursors use `IQueryCursorCodec` plus `QueryCursorScope` and remain opaque, bounded, scoped, and fail safe.
- Projection and pub/sub messages are delivered at least once and without guaranteed ordering; notifications are freshness signals, not proof of success; consumers deduplicate by EventStore `MessageId`.
- Runtime topology changes must update AppHost, DAPR component/configuration YAML, app IDs, sidecar options, ACLs, topics, component and secret scopes, the canonical secret contract, and topology tests together.

### Security and release

- Security fails closed above infrastructure scoping: application-layer credentials and tenant authorization are required before data disclosure.
- AD-24 binds FR34, NFR4, NFR17, and current Story 7.6: production operational and application secrets use DAPR component `openbao` of type `secretstores.hashicorp.vault` v1; logical names, map shapes, consumers, retrieval lifecycle, access paths, and rotation bounds derive from the value-free `deploy/dapr/openbao-secret-contract.yaml`.
- The AD-24 contract drives singleton component scopes, per-app DAPR `defaultAccess: deny` plus explicit `allowedSecrets`, and least-privilege OpenBao ACLs; mismatches fail validation, while the OpenBao token, DAPR API token, and TLS trust material remain acyclic out-of-band bootstrap inputs.
- AD-24 required secrets gate readiness, runtime lookup failure disables the dependent operation until bounded recovery, and rotation is generation-aware publish-overlap-acknowledge-revoke. Release evidence must use real OpenBao; Azure Container Apps managed DAPR is non-conforming until a separately approved compatible profile exists.
- AD-24 governs operational and application secret retrieval only. It does not approve, replace, or modify AD-23 or the draft payload-protection Azure Key Vault Premium RSA-HSM KEK proposal; DAPR secret stores are not production `pdenc-v2` key custody.
- Release is manifest-governed through `tools/release-packages.json`; Release/package validation uses package-reference mode by default; submodule packages are not produced by EventStore release jobs. Story 8.8 alone owns payload-protection package creation and release-manifest authority, updates the manifest atomically from 14 to 16 packages, and does not modify assistant entry-point files.
- High-risk verification must assert persisted Redis/state-store/read-model/CloudEvent bodies, topology YAML or sidecar arguments, package outputs, and security denials.

### Planning and repository boundaries

- Folded snapshots, projection delivery cost, projection sequence guards, event versioning/upcasting, identity metadata validation, cancellation-token public seams, and global-position sharding require approved specs before implementation stories start. Story 6.2 must prove `snapshot size <= folded-state payload size + MaxSnapshotEnvelopeOverheadBytes` using the numeric bound approved by Story 6.1.
- Preserve the frozen `/project/v2` wire response and emit one server-owned `ProjectionDispatchResult` Version 1 with bounded ordinal route entries, stable status codes, and explicit `Advanced` or `NotAdvanced` checkpoint state; no equivalent shape is allowed without a new architecture decision.
- `src/Hexalith.EventStore.Admin.UI` remains the only EventStore UI host and the `eventstore-admin-ui` resource. It composes matching FrontComposer Shell and Contracts.UI packages through the Builds catalog `HexalithFrontComposerVersion` (dated architecture value `4.1.1`) with Fluent UI V5, owns the `event-store-admin` module, and distributes shell/routes, typed-client/evidence-state integration, and accessibility/localization/responsive conformance across Stories 7.14, 7.19, and 7.20.
- Story 1.20 closes source/package parity; Story 3.13 preserves only rejected `v3.94.1` evidence; Story 3.14 produces a separately authorized corrective candidate; Story 3.15 may close positive deployed-runtime parity without reopening Stories 1.20 or 3.12. Never compare a consumer repository SHA to the EventStore SHA.
- `AddEventStoreGatewayClient(...)` registers the typed client only. DAPR service invocation is opt-in and must be the last/innermost decorator by explicitly chaining `.AddEventStoreDaprServiceInvocation(appId, apiToken)`; omitting the chain fails open to no transport rather than silently selecting DAPR.
- Tenant provisioning rejects the reserved `system` tenant before any state or side effect; Story 5.10 owns the guard and evidence.
- The approved replan preserves eight-epic order and MVP scope, rehomes platform provenance into Story 1.2, leaves generated REST/Tenants production provenance in Story 2.11, and makes Story 2.6 independently testable with deterministic presentation fixtures.
- A split child inherits `done` only through an evidence crosswalk naming implementation, focused tests, review results, and external approval/exact SHA where applicable; otherwise it remains `review`. The 2026-07-15 and 2026-08-01 crosswalks together are authoritative for migrated status and evidence.
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
- Do not use the AD-24 operational secret store as payload-protection KEK custody or treat AD-24 as approval of the draft payload-protection backend.
- Do not claim Azure Container Apps managed DAPR conforms to AD-24 without a separately approved compatible profile.
- Do not authorize or perform a release, deployment, external publication, Git operation, submodule change, consumer mutation, consumer removal, or human acceptance in this planning update.

## Success signal

Implementation readiness can be re-run only after the atomic downstream planning update. It must then find complete FR1-FR37 and NFR1-NFR19 coverage, no stale positive `v3.94.1` authority, no ungoverned or forward prerequisite, no oversized active parent, and deterministic content-addressed owner, evidence, and removal gates. The resulting plan preserves AD-1 through AD-25; keeps Epic 3 open through independent Story 3.15 closure; keeps FR37 and NFR19 as separately gated post-MVP work; assigns OQ8 platform evidence to Stories 4.14-4.15; and requires persisted-evidence validation.

## Assumptions

- No additional audience-facing artifact was requested; this SPEC plus companions are the deliverable.
