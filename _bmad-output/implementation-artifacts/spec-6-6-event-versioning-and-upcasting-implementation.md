---
title: 'Story 6.6: Event Versioning And Upcasting Implementation'
type: 'feature'
created: '2026-10-04'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 1
story_key: '6-6-event-versioning-and-upcasting-implementation'
planning_revision: '2242ad55a1b678828df8aa093fd92399c29af5bf'
baseline_commit: '1329b35e52852952ecb2c94aabf100674e9691e3'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/6-6-implementation-map.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-6-6-trusted-code-amendment.md'
---

<frozen-after-approval reason="human-owned intent — renegotiated by owner on 2026-10-05 for Dapr-only storage">

## Intent

**Problem:** Persisted/wire events lack stable versioned identity, and consumers have no shared authenticated evolution pipeline. Processing and cancellation differ across durable boundaries.

**Approach:** Implement versioned event identity and deterministic upcasting across writers, readers, consumers and recovery through Dapr state, actors, pub/sub and service invocation. The [Dapr-only amendment](story-6-6-dapr-only-amendment.md) supersedes conflicting provider-specific parts of the earlier approved AD-13 design.

## Boundaries & Constraints

**Always:** Follow compatible approved schemas, budgets, outcomes and activation order. Preserve application payload bytes, MessageIds, actor commits, last-good state and compatibility adapters. Propagate cancellation to the defined durable boundary. Verify every consumer through one allow-listed evolution service. Keep unavailable proof-dependent operations fenced.

**Scope:** Local implementation, tests and existing CI gates. Remove the direct PostgreSQL adapter, Server Npgsql reference, and Story 6.6 Npgsql pin in Builds. New persistence code must call Dapr actor/state APIs only.

**Never:** Rewrite history, re-execute committed commands for publication resume, claim provider-signed or historical-generation evidence from Dapr logical readback, relax gates or modify unrelated dependencies. Snapshot/projection-cost redesign and Epic 8 protection are excluded. Deployment, publication and offline retained-data migration require their existing separate authority; absent production qualification keeps activation fenced.

## I/O & Edge-Case Matrix

| Input/state | Required behavior |
| --- | --- |
| Current, legacy or mixed history | Canonical effective state; immutable stored evidence |
| Invalid identity/chain, unreadable payload or unavailable evidence | Approved typed outcome; no partial state, checkpoint or handler effect |
| Cancellation before/after commit | No pre-commit mutation; preserve committed truth and bounded recovery |
| Retry, resume, owner takeover or rollback | Same durable identities; fenced authority, idempotent effects and Dapr logical readback |

</frozen-after-approval>

## Code Map

[Implementation map](6-6-implementation-map.md) M1–M8 records the previous plan and the current Dapr-only corrections. AD-13's approved digest `bc1625e3b8147fb0bc2cd9491fad8379a95c1ce0b597eba70f8d29f2fee3b050` and its passing preflight describe the earlier design, not this amendment. A revised preflight and evidence set are required before claiming Story 6.6 completion.

## Tasks & Acceptance

**Execution, in dependency order:**

- [ ] `src/Hexalith.EventStore.Contracts/Events/`, `src/Hexalith.EventStore.Client/Events/` — M1: add compatible metadata, registry, codecs and bounded upcaster interfaces; preserve constructors and legacy wire behavior.
- [ ] `src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs`, `src/Hexalith.EventStore.Server/Events/`, `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` — M2: bounded V1 producer, actor same-save logical evidence/readback and shared Dapr-backed evolution reader; keep slice 1 additive. Update invoker streaming before admission.
- [ ] Dapr actor/state APIs — M3: define and qualify the Dapr control owner, ETag/transaction capability and recovery boundaries needed by actual hold producers; V2 stays dormant. The former PostgreSQL adapter is withdrawn.
- [ ] `src/Hexalith.EventStore.Contracts/Replay/`, `src/Hexalith.EventStore.Client/Handlers/`, `src/Hexalith.EventStore.DomainService/DomainQueryDispatcher.cs` — M4: private paged replay, async processing, verified reconstruction and scoped query intake; propagate original tokens and fence versioned cache access.
- [ ] `src/Hexalith.EventStore.Server/Projections/`, `src/Hexalith.EventStore.Client/Projections/`, `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs` — M5: verified full/incremental dispatch, application-owned named generations with Dapr logical readback, checkpoint and query visibility; preserve legacy key ownership.
- [ ] `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`, `src/Hexalith.EventStore.Client/Subscriptions/` — M6: exact carriers/pins, membership and effect receipts; verify before marker/handler and preserve first-response/status authority.
- [ ] `src/Hexalith.EventStore/Controllers/`, `src/Hexalith.EventStore.Operations/`, `src/Hexalith.EventStore.Admin.UI/` — M7: authorized same-event resume, Dapr logical capture/redrive, hold inventory and safe diagnostics; audit SDK/CLI/Admin filters and activate only qualified slice changes.
- [ ] `tests/`, `scripts/verify-event-evolution.py`, `.github/workflows/ci.yml` — M8: production-path matrices, timed mutation checks, API/wire/package consumers and two-host Dapr evidence; re-evaluate O-01–O-20 against the amendment before final V2/major gate.

**Acceptance Criteria:**

- Given the earlier reviewed inputs and owner's Dapr-only correction, when preflight runs, then historical approval is distinguished from this amendment and implementation traces to the current Dapr-only boundary.
- Given any supported history, when each consumer executes, then effective objects and persisted aggregate/projection end-state equal the canonical baseline with original bytes unchanged.
- Given invalid or cancelled work, when each durable boundary is exercised, then typed outcomes preserve last-good truth and produce zero forbidden mutation or disclosure.
- Given concurrent recovery and mixed fleets, when crash/rollback matrices run through Dapr APIs, then durable intent, logical readback, fences and stable identities prevent duplicate effects or incompatible admission within the demonstrated Dapr capability boundary.
- Given completion, when all affected regressions, compatibility and Dapr live-sidecar lanes run, then required checks pass without unexpected skips; only proven obligations close. Missing production authority or required evidence leaves the story incomplete.

### Review Findings — local JSON replay admission, 2026-10-07

The four independent review layers assessed this run's owned diff as an M4
prerequisite. All layers reported before triage. These findings do not qualify
the complete parent story or change its frozen intent or M1–M8 dispositions.

- [x] [Review][Patch] Preserve exact lowercase inline payload classification while retaining case-insensitive contract-envelope binding [src/Hexalith.EventStore.Client/Handlers/LegacyCommandReplayJsonAdmission.cs:99].
- [x] [Review][Patch] Preserve already parsed JSON containing comments or trailing commas in measurement and private capture [src/Hexalith.EventStore.Client/Handlers/LegacyCommandReplayInput.cs:156].
- [x] [Review][Patch] Admit decoded payload JSON token-table capacity before parsing in both contract-envelope and inline base64 routes [src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs:364].
- [x] [Review][Patch] Decode base64 through bounded fixed scratch rather than unaccounted framework unescape/decode buffers [src/Hexalith.EventStore.Client/Handlers/LegacyCommandReplayInput.cs:172].
- [x] [Review][Patch] Reject contradictory aliases in fixed contract metadata before binding [src/Hexalith.EventStore.Client/Handlers/LegacyCommandReplayJsonAdmission.cs:55].
- [x] [Review][Patch] Prove positive and negative default-Web metadata boundaries using independently serialized images [tests/Hexalith.EventStore.Client.Tests/Handlers/LegacyCommandReplayJsonAdmissionTests.cs:29].
- [x] [Review][Patch] Prove private buffer/document cleanup after later refusal, failure and cancellation [tests/Hexalith.EventStore.Client.Tests/Handlers/LegacyCommandReplayJsonAdmissionTests.cs:139].
- [x] [Review][Patch] Exercise aggregate readable admission for individually supplied enumerable JsonElements [src/Hexalith.EventStore.Client/Handlers/LegacyCommandReplayInput.cs:116].
- [x] [Review][Patch] Bound ignored JSON property-name decoding during fixed alias lookup while preserving case/escape compatibility [src/Hexalith.EventStore.Client/Handlers/LegacyCommandReplayJsonAdmission.cs:146].
- [x] [Review][Patch] Apply the metadata ceiling to the emitted key image so equivalent raw/escaped ignored names remain compatible [src/Hexalith.EventStore.Client/Handlers/LegacyCommandReplayJsonAdmission.cs:154].
- [x] [Review][Patch] Isolate the enumerable readable ceiling from the independent accounted-memory ceiling with a killing mutation control [tests/Hexalith.EventStore.Client.Tests/Handlers/LegacyCommandReplayJsonAdmissionTests.cs:372].

Rejected: blind-hunter nested subtree recharging — low. This is conservative
admission of actual additional private copies, not a demonstrated budget bypass;
the former wrapper binder also copied nested snapshot subtrees. Removing copies
would add ownership-tracking complexity without an established required acceptance
boundary. The conservative live-array ceiling remains explicit.

## Implementation Notes

### Current-amendment obligation audit — 2026-10-07

The [current obligation audit](6-6-obligation-audit.json) re-evaluates O-01
through O-20 under the Dapr-only and trusted-code amendments. It binds the
historical approved normative bytes and each historical obligation row separately
from both current amendments, identifies six rows with withdrawn provider
requirements, and records their current Dapr capability/logical evidence boundary.
Every obligation remains open; this audit supplies traceability and gate accounting,
not implementation qualification, closure, production authority or activation.

The existing CI `scripts/verify-event-evolution.py --mutations` gate now checks
these inputs and dispositions. Missing/changed inputs, incomplete/duplicate rows,
unsupported provider assurance, premature closure and claimed activation refuse.
The [local verification](evidence/story-6-6/verification-2026-10-07-obligation-audit.md)
records timeout-bounded mutations, actual temporary-tree missing/changed-input
and malformed-schema controls, and an unrelated-file/Git-metadata positive control.
No runtime registrations, consumer routes, parent tasks or activation fences change.

### Local JSON replay admission — 2026-10-07

The [JSON replay admission verification](evidence/story-6-6/verification-2026-10-07-json-replay-admission.md)
extends the earlier command-state owner to private JSON capture, fixed wrapper
binding, fixed-scratch base64 decoding and pre-parse decoded token-table admission.
It preserves legacy payload classification and accepted source-document syntax,
checks fixed metadata aliases and exact emitted metadata boundaries, and proves
clearing after later refusal, failure and cancellation. Four independent review
layers and targeted follow-ups resolved all 11 local patch groups; an isolated
readable-guard mutation distinguishes the 64 MiB ceiling from accounted capacity.

Final focused checks passed 101/101, full Client tests 1,284/1,284, DomainService
495/495 and Sample 175/175, with no runner failures/skips. The required Release
package-mode solution build had zero warnings/errors; all 14 local CI packages
and three isolated package-only consumers passed. Original ingress parsing,
application converters/typed graphs and caller-owned typed snapshot isolation
remain unqualified. Catalog, immutable execution, consumer, Dapr/fleet and
production qualifications remain open. Frozen intent, canonical baseline,
in-progress status, M1–M8 dispositions and activation fences are unchanged.

### Local command-state envelope ownership — 2026-10-07

The [local command-state admission verification](evidence/story-6-6/verification-2026-10-07-command-state-envelope-admission.md)
records a bounded disposable owner for built-in legacy contract-envelope replay,
shared nested count/payload/metadata/container charges, bounded empty-wrapper
recursion and deterministic private payload clearing. An early converter mutation
control proves the successor payload is detached before deserialization. Final
focused tests passed 22/22, full Client tests 1,205/1,205, full DomainService tests
495/495, and the required package-mode Release build had zero warnings/errors.

Raw JSON/base64 materialization and arbitrary typed graphs remain unqualified
compatibility adapters. A retained public-processor diagnostic reproduces the
unresolved caller-owned typed snapshot/tail alias: a later mutating Apply throws
and leaves the source snapshot changed. This owner therefore supplies neither
complete command-state admission nor last-good typed snapshot isolation.
Authoritative private state/serializer, catalog and production qualifications
remain required. Frozen intent, baseline, in-progress status, M1–M8 dispositions
and activation fences are unchanged.

### Dormant managed artifact admission and observations — 2026-10-07

The [local managed-loader verification](evidence/story-6-6/verification-2026-10-07-managed-loader-preparation.md)
records bounded private G-row/image ownership, exact direct Assembly-object
binding, scope-bound registry callbacks and composed managed observations.
Original dependency rows remain privately retained and charged; identical bytes
cannot be relabelled as another dependency or loader context. Undeclared observed
loads and loss of retained evidence fence subsequent callbacks. Observations occur
after loads and cannot undo effects. Unlisted contexts and native loads remain
outside this local coverage.

These internal prerequisites are unregistered. Authoritative complete catalogs,
serving-peer pins, transitive/framework/native execution binding, qualified
process-wide observations and production/Dapr/fleet consumer evidence remain
required before readiness. Frozen intent, canonical baseline, in-progress status,
all M1–M8 dispositions and V2/proof-dependent activation fences are unchanged.

### Source-derived Counter writer and Dapr/PostgreSQL recovery — 2026-10-07

The owner directed this resumed run to derive the missing declarations and
qualification inputs from the repository. The root-owned Counter host now
registers its six exact V1/json serializer declarations: five two-byte marker
events and a maximum 309-byte framework termination rejection. The existing
bounded router selects this profile; independent byte/alias compatibility,
refusal, cancellation and count controls pass. Other domains retain their
existing compatibility behavior.

The [Counter writer and PostgreSQL logical recovery evidence](evidence/story-6-6/verification-2026-10-07-counter-v1-postgresql.md)
records the actual Sample/actor/Dapr path with two EventStore hosts, immutable
application bytes and metadata, separate stable command/event identities,
sequence-one end state, exactly one domain execution, duplicate/failover/restart
recovery and exact owned-resource cleanup. The original sealed OQ8 fixture is
preserved; the probe instruments a temporary copy. This Testing profile uses
Dapr 1.18.4 and the tracked PostgreSQL v1 state component, with Redis pub/sub and
fixture authentication; it supplies no AD-26 production authority. Focused
Sample tests passed 17/17, full Sample tests 175/175, the live test 1/1, and the
required package-mode Release solution build had zero warnings/errors.

This completes these local Counter writer and recovery prerequisites. Full
catalog/artifact admission, qualified loader observations, all consumer
integrations and production/fleet qualification remain required. Frozen intent,
canonical baseline, in-progress status, M1–M8 dispositions and activation fences
are unchanged.

### Dapr-only slice — 2026-10-05

The dormant SQL control adapter, its tests and Npgsql footprint were removed.
An unregistered addressed `IActorStateManager` logical reader now resolves
allow-listed event versions through the Client upcaster kernel; new V1 writes
carry an application payload/metadata digest that publication preserves. The
reader verifies the digest after unprotection when present, while historical
V1 values without it remain readable. This is logical application evidence,
not provider attestation. The [transition evidence](evidence/story-6-6/dapr-only-transition-2026-10-05.md)
records focused tests and a warning-free Release build. The reader is not yet
wired into production consumers; actor-head/route checks and live Dapr proof
remain open. V2 writes remain fenced and no M1–M8 task is complete.

The subsequent [V1 read-safety verification](evidence/story-6-6/verification-2026-10-05-v1-read-safety.md)
records actor rehydration digest checks, addressed stream reads, production
cancellation forwarding and the affected Server test results. This remains
partial evidence; the shared reader and M1–M8 acceptance are still open.

The [logical-page and V1 consumer safety evidence](evidence/story-6-6/verification-2026-10-05-logical-page-safety.md)
records publisher/projection digest and addressed-batch checks, a bounded
legacy-array read guard, an internal fixed-head Dapr logical page, and an
explicit pinned-manifest candidate registration with a supplied local closure
check. It names the missing authoritative domain inputs, production registry
readiness and signed route/proof seams. The
page is not wired into production consumers; V2 and proof-dependent activation
remain fenced, and M1–M8 acceptance is still open.

The [V1 intake and compatibility repair evidence](evidence/story-6-6/verification-2026-10-05-v1-intake-and-compatibility.md)
records whole-result bounded admission, composed readable/output ownership,
dormant exact provenance members with refusal on legacy consumer routes, the
restored twelve-member drain-record ABI, strengthened source-fence controls,
the standard local package fixtures and separate Development live profiles.
Authoritative manifests, trusted loader qualification, route/proof binding,
control transactions and production/fleet qualification remain open. No M1–M8
acceptance checkbox or activation fence changes.

### Re-derivation requirements from resumed review

The [private replay ownership verification](evidence/story-6-6/verification-2026-10-05-private-replay-ownership.md)
records an unregistered charged private session, synchronous successor capture,
scoped local handles, cancellation/expiry clearing, bounded contiguous timeline
controls and immutable legacy last-good state on mutating Apply failure. Local
sealing confers no durable progress. Dapr page ledgers/pins, authenticated
serializer/source/continuation binding and protected timeline storage remain
open; all M1–M8 tasks, O-rows and activation fences are unchanged.

The subsequent [composed logical-read verification](evidence/story-6-6/verification-2026-10-05-composed-logical-read-boundary.md)
records manifest route checks, shared page ownership, separate readable-source
accounting, fixed head/floor/ETag checks on the production V1 reader, and a
Development Dapr two-sidecar/restart application-byte test. The current-amendment
source preflight is separate from the historical AD-13 approval verifier and
grants no activation authority. The exact missing per-domain manifest, trusted
loader/catalog closure, route/proof binding and production profile still block
consumer activation. V2 remains fenced; no M1–M8 task or O-row is closed.

Current follow-up implementation and exact verification evidence is recorded in [Story 6.6 follow-up evidence](evidence/story-6-6/verification-2026-10-04-followup.md). Internal registry/hash/options, bounded local E/F execution, registered implementation/type/validator bindings, an optional bounded producer/renderer and default incremental V1 response admission have been added alongside the review repairs. The final-source Release build and standard package consumer checks passed; full Server regression retained two unrelated failures and 25 existing skips. These results do not establish authenticated production reader, startup readiness, complete dependency closure or M1–M8 acceptance. Remaining local omissions and the actual provider/profile authority boundary are recorded separately; status remains `in-progress`.

- `Server/DomainServices/DaprDomainServiceInvoker.cs` and `Server/Events/EventPersister.cs`: reject unsolicited V2 on the current legacy-only production path before state mutation; do not invent activation authority. A later real negotiated V2 path requires the amended Dapr capability and consumer gates.
- `Server/Events/EventPublisher.cs`: preserve both additive metadata fields when constructing publication envelopes, with actual published-envelope assertions.
- `Client/Events/BoundedScratchAllocator.cs`: reserve total live capacity atomically before allocation, release only after clearing; initial scratch bytes must be zero; nested/concurrent requests must refuse over-budget work.
- `Contracts/Events/AuthenticatedRawEventPage.cs`: snapshot caller event references once before validation/copy; establish composed bounded ownership and clearing rather than claiming summed source length proves live memory.
- Both `Contracts/Events/VerifiedEffective*` DTOs: validate fixed-array sizes before copying and enforce approved claim/payload limits without confusing current zero-hop V1 payloads with hopped payloads. Production ingress must enforce the complete encoded property/page budgets before JSON materialization.
- `Server.Tests/Events/EventEnvelopeTests.cs`: add JSON/DataContract non-null V2 round-trip regression coverage.
- Preserve the compatible KEEP constraints recorded in the resumed review; acceptance still requires the amended M1–M8 implementation and compatibility/Dapr evidence. Do not mark partial repairs as story completion.

- This run adds bounded payload/scratch primitives, event-view contracts, an authenticated raw-page contract, metadata tuple validation, and metadata-preserving actor mapping. The M1 registry/codecs, M2 bounded producer/authenticated reader, and M3–M8 runtime integrations and evidence are not complete; no task checkbox is marked complete.
- The workflow baseline is `1329b35e52852952ecb2c94aabf100674e9691e3`. During the run, `HEAD` advanced concurrently to `f3dc36b934336920b3c7e4bcec5cf52c6327df9d`; that commit and its unrelated Story 6.1 changes were preserved as external input.
- `VerifiedEffectiveCommandEvent.cs` was marked assume-unchanged and differed from `HEAD`. The implementation agent added its constructor validation before checking that path's original bytes. No pre-edit copy is available, so its current worktree content is preserved and the possibility of overlapping hidden local edits remains unresolved.
- Aspire was started for baseline inspection and stopped after dependent services remained waiting on Keycloak. No provider or live-sidecar qualification was obtained. The Builds package pin differs from its submodule `HEAD` and was left unchanged.

## Spec Change Log

- 2026-10-07 — Added the [current-amendment obligation audit](6-6-obligation-audit.json)
  and its [scoped verification](evidence/story-6-6/verification-2026-10-07-obligation-audit.md).
  The existing CI gate binds historical approval separately from both current
  amendments and refuses unqualified closure/activation. All O-rows, M1–M8 task
  dispositions and parent status remain open/in progress.

- 2026-10-07 — Added [local JSON replay admission](evidence/story-6-6/verification-2026-10-07-json-replay-admission.md), including reviewed compatibility fixes, emitted metadata boundaries, bounded decoding, pre-parse token capacity, deterministic cleanup and an independently killed readable-guard mutation. Final source suites, Release build and local package consumers pass; all parent tasks and production/authority gates remain open.

- 2026-10-07 — Added [dormant managed artifact and observation preparation](evidence/story-6-6/verification-2026-10-07-managed-loader-preparation.md): privately retained exact G declarations/images, direct loaded-object provenance, original-row and shared-loss checks through actual registry callbacks, and composed managed-load detection. Local supplied graphs grant no catalog, peer or production authority; native/process-wide qualification and broader consumers remain open. Frozen intent, baseline, parent status and M1–M8 tasks remain unchanged.

- 2026-10-07 — On the owner's direction to derive the inputs, implemented and registered the [source-derived Counter V1 serializer declarations](evidence/story-6-6/verification-2026-10-07-counter-v1-serialization.md) and verified the real Sample/actor path with [two-host Dapr/PostgreSQL logical recovery](evidence/story-6-6/verification-2026-10-07-counter-v1-postgresql.md). Exact wire compatibility, persisted bytes/metadata, stable separate identities, sequence-one state, once-only domain execution and restart recovery passed. Full Sample regressions and the required Release build passed. These local prerequisites do not close M1–M8 or grant catalog/production/V2 readiness; frozen intent, baseline and parent status are preserved.

- 2026-10-07 — Closed the local [bounded V1 scratch-admission verification prerequisite](evidence/story-6-6/verification-2026-10-07-bounded-v1-scratch-admission.md): independent 42/43 one-MiB serialized-source controls, a zero-callback refusal sentinel, exact unchanged-source/detached-output checks and an isolated killing mutation of only the scratch guard. The full Debug/source DomainService suite passed 494 tests. Runtime/profile/registration APIs and the compatible router fallback were unchanged; authoritative application serializer declarations and broader catalog/Dapr/fleet qualification remain required. Frozen intent, canonical baseline, in-progress status and all M1–M8 tasks are unchanged.

- 2026-10-06 — Recorded [page admission and observed capability loss](evidence/story-6-6/verification-2026-10-06-page-preflight-and-capability-loss.md): complete logical-page preparation before catalog callbacks, private readable ownership, source-mutation refusal and retained page/range metadata charges, plus shared sticky loss checks around local callback and result boundaries. Production deployment belongs to the root-declared Hexalith.Platform composition owner; its checked-in Redis components are Development inputs. Exact domain serializer declarations/options/bounds/aliases, authoritative catalogs and the ratified production profile remain required. Frozen intent, baseline, in-progress status and all M1–M8 dispositions are unchanged.

- 2026-10-06 — Recorded [local trusted-code artifact qualification](evidence/story-6-6/verification-2026-10-06-trusted-code-artifacts.md): exact-file and supplied-graph refusal controls, manifest registration guards and accurate assurance limits. Consistent partial supplied graphs and replaceable paths remain unqualified for readiness; complete authoritative catalogs, immutable execution binding, observations and production evidence remain open. Frozen intent, baseline and all M1–M8 dispositions are unchanged.

- 2026-10-06 — Owner selected reviewed trusted application code with “do recommended”. The [loader amendment](story-6-6-trusted-code-amendment.md) replaces the conflicting universal before-effect loader assurance, preserves the failed probe and historical approval inputs, and requires immutable artifact/pin admission and detection with subsequent capability loss. No activation gate or M1–M8 disposition changes.

- 2026-10-05 — Recorded local [private replay ownership and last-good state](evidence/story-6-6/verification-2026-10-05-private-replay-ownership.md) implementation and verification. Distinguished unregistered preparation from durable page authority, documented added legacy serialization work and kept all M1–M8 acceptance and activation gates open.

- 2026-10-05 — Recorded local [V1 intake, ownership and compatibility repairs](evidence/story-6-6/verification-2026-10-05-v1-intake-and-compatibility.md), scoped package/compiled-consumer checks and Development live fallback evidence. Kept the canonical baseline, frozen intent, in-progress status and all M1–M8 acceptance requirements unchanged.

- 2026-10-05 — Owner directed implementation to stay within Dapr. The [Dapr-only amendment](story-6-6-dapr-only-amendment.md) supersedes the earlier direct PostgreSQL/provider-extension design for Story 6.6. Removed dormant SQL adapter and dependency footprint; V2 and proof-dependent activation remain fenced. Earlier approval digest and provider tests remain historical evidence, not current acceptance.

- 2026-10-05 — Recorded the earlier [PostgreSQL transaction-capture extension](evidence/story-6-6/postgresql-capture-extension-decision.md), since withdrawn by the Dapr-only direction, and the separate pending [managed/native loader boundary](evidence/story-6-6/dependency-loader-decision.md). M1–M8 and activation remain open.

- 2026-10-04 — Resumed review identified unsolicited V2 admission, publication tuple loss, live scratch accounting, private-copy lifetime, raw-page reference substitution and unbounded transport copies. Added explicit re-derivation requirements above to avoid premature writes/unvalidated inputs. KEEP legacy APIs/wire null omission, metadata mapping/validation, immutable copies, source limits, token forwarding and passing regressions. Existing committed code/external Story 6.1 work is preserved; this run has no runtime changes to revert.

- 2026-10-04 — Started Story 6.6 on owner request; recorded baseline `1329b35e52852952ecb2c94aabf100674e9691e3`, moved status to `in-progress`, and captured bounded implementation progress and remaining M1–M8 scope.
- 2026-10-04 — Review pass corrected metadata validation, event metadata mapping, raw-page gap/budget/ownership handling, and focused coverage; production integration and provider qualification remain outstanding.

## Review Triage Log

- [defer] `IEventUpcaster`/`IV1Downserializer` have no registry or execution pipeline (Blind Hunter 1; Edge Case Hunter 4). The baseline had no shared evolution pipeline, and the added interfaces do not change runtime behavior; M1 remains incomplete and this capability must be implemented before the story can be done.
- [defer] Domain-service invocation does not negotiate writer mode/fingerprint or authenticate raw-source reads (Blind Hunter 2 and 9). These runtime paths were absent before this partial implementation; M2 remains incomplete and activation is still fenced.
- [medium / patch, resolved] `EventPersister` accepted invalid metadata versions and partial identity pairs (Blind Hunter 3; Edge Case Hunter 1). It now validates the V1/V2 tuple before state reads, protection, global-position reservation, or writes; `EventPersisterTests` passed 38/38.
- [false] `VerifiedEffectiveCommandEvent` lacks basic validation (Blind Hunter 4). The current constructor validates positive sequence, exact digest/signature sizes, canonical contract identity, bounded payload version, serialization format, and route key ID.
- [false] `EventContractIdentityValidator` rejects no consecutive-hyphen rule (Blind Hunter 5; Edge Case Hunter 2). The approved schema explicitly allows repeated interior hyphens and accepts any lower-case alphanumeric endpoint.
- [medium / patch, resolved] `AuthenticatedRawEventPage` accepted an empty page while `StartSequence <= ActorHead` (Blind Hunter 6). It now rejects this gap; bounded nonempty pages remain valid. Focused page tests passed 5/5.
- [medium / patch, resolved] `AuthenticatedRawEventPage` did not charge optional evidence payloads against the raw source budget (Blind Hunter 7; Edge Case Hunter 3). It now caps envelopes and sidecars together at 128 MiB and exposes the separate 64 MiB post-unprotection check; production reader integration remains in the deferred M2 work.
- [medium / patch, resolved] `AuthenticatedRawEventPage` retained caller-owned `IReadOnlyPayload` references (Blind Hunter 8). It now takes private copies after checking the aggregate source budget; mutation-isolation tests passed in the focused 5/5 page run.
- [low / rejected] `DomainServiceWireEvent.Payload` remains a mutable array (Blind Hunter 10). This was already the positional DTO contract before version metadata was added, and the reviewed production response path introduces no additional shared owner; a compatibility-preserving ownership wrapper would add complexity for a non-routine misuse.
- [medium / patch, resolved] No focused test proved version metadata survives invoker conversion and persistence (Verification Gap primary finding). A focused invoker-to-persister route test now asserts the persisted triplet; `DaprDomainServiceInvokerTests` passed 40/40.
- [medium / patch, resolved] `AggregateActor.ToContractEventEnvelope` dropped the new event contract type and payload version when building domain-service current state (Verification Gap other finding). The mapping now copies both values; `AggregateActorDomainResultTests` passed 32/32.
- [false] Sidecar evidence exceeding the readable payload ceiling (Edge Case Hunter 3). Raw/source evidence and readable payloads have distinct approved budgets; the page charges raw envelopes plus sidecars against 128 MiB, while `ValidateReadablePayloadBytes` checks post-unprotection bytes against 64 MiB. The missing production caller for that check is tracked in the deferred M2 work.

### Resumed review — 2026-10-04, HEAD `547c938d52e4fd31b47783b9dd032528a5100549`

- [high / bad_spec] Blind Hunter 1: Unsolicited V2 admission is reachable: `DaprDomainServiceInvoker` constructs a request with no mode/fingerprint, converts any response triplet, and `EventPersister` accepts metadata version 2 before state staging. The new successful V2 tests demonstrate this path without activation evidence. AD-13 §2 and §10 require negotiated capabilities and qualified activation; repair must fence V2 until those prerequisites exist.
- [medium / patch, pending loopback] Blind Hunter 2: `EventPublisher` reconstructs the envelope with `MetadataVersion` but omits both additive fields, so an admitted V2 event loses its complete tuple on the actual publication path. Copy both init properties and assert publication equality after the higher-priority loopback is resolved.
- [defer / carried] Blind Hunter 3: Same missing registry/codecs/execution pipeline as the existing Blind Hunter 1/Edge Case Hunter 4 row; interfaces remain declarations, M1 remains incomplete. Preserve the previous route; no duplicate deferral.
- [defer / carried] Blind Hunter 4: Same absent production negotiation/bounded pipeline as the existing Blind Hunter 2/9 row; router still uses `FromDomainResult` and invoker still buffers before response validation. M2 remains incomplete; no duplicate deferral.
- [defer / carried] Blind Hunter 5: Same absent authenticated raw-source/runtime evidence as the existing Blind Hunter 2/9 row; the interface has no production provider implementation and the actor does not stage AD-13 sidecars/receipts. M2 remains incomplete; no duplicate deferral.
- [defer / carried] Blind Hunter 6: Same absent shared evolution reader as the existing Blind Hunter 2/9 row; stream/replay/snapshot consumers still use typed reads. M2–M4 remain incomplete; no duplicate deferral.
- [medium / bad_spec] Blind Hunter 7: The scratch allocator checks each request against `_maximumBytes` but records no live capacity; nested/concurrent callbacks can retain multiple individually legal allocations. AD-13 §3 requires reservation before allocation and a shared live ceiling; add aggregate live accounting and refusal tests.
- [medium / bad_spec] Blind Hunter 8: Raw-page copies have no charged/disposable lifetime or failure clearing; up to 128 MiB remains live alongside caller source buffers, and copied private plaintext cannot be deterministically cleared. AD-13 §§3–4/B6 require composed charges and full-capacity clearing. Reader/page ownership must establish one charged lifetime rather than claiming a summed source-length check proves live memory.
- [medium / bad_spec] Blind Hunter 9: Both effective DTO constructors copy fixed proof arrays before checking their lengths and impose no payload/claim allocation ceiling. `CommandStateProof` has no bounded ingress caller. AD-13 §2/§6 specify transport proof and composed page limits; enforce admission before materialization/copy with exact zero-hop versus hopped budgets.
- [medium / bad_spec] Blind Hunter 10: Existing evidence confirms package/API/compiled-consumer and full affected regressions are absent. Current checks do not prove compatibility of the changed public contracts. Complete the required fixtures/gates before acceptance; new focused checks below do not settle those missing lanes.
- [medium / bad_spec] Edge Case Hunter 1: Same live scratch-accounting root cause as Blind Hunter 7; recursive calls can allocate beyond the configured ceiling. Grouped with that finding after independently verifying the missing live reservation.
- [medium / patch, pending loopback] Edge Case Hunter 2: `GC.AllocateUninitializedArray<byte>` does not promise cleared initial bytes, and the callback can read the span before writing it. End-of-callback zeroing does not protect its first read. Allocate zeroed storage (or clear before invocation) to preserve scratch isolation.
- [medium / bad_spec] Edge Case Hunter 3: The raw page validates `events[i]`, then later rereads the caller-owned collection while calling arbitrary `CopyTo` implementations. An earlier callback can replace a later element with an unvalidated key/sequence/evidence. Capture event references once and use only that validated snapshot; test mutation from `CopyTo`.
- [medium / bad_spec] Edge Case Hunter 4: Same unbounded DTO copying root cause as Blind Hunter 9; wrong-sized fixed proofs allocate a private copy before rejection. Grouped after checking both constructors.
- [high / bad_spec] Edge Case Hunter 5: Same reachable unsolicited V2 write as Blind Hunter 1; no negotiation or fingerprint check exists between legacy response conversion and state staging. Grouped after checking the production caller.
- [medium / patch, pending loopback] Verification Gap: Accepted pre-verified regression gap: `EventEnvelopeTests` serializes only V1 data, while new tests bypass durable JSON/DataContract round trips. Add non-null V2 triplet round trips in both serializers after the loopback is resolved; current attributes appear correct but the regression boundary is unprotected.

Review groups require a `bad_spec` loopback; lower-priority patches/deferrals are pending rather than executed. KEEP: preserve legacy constructors/deconstruction/null omission/default interface members, complete-triplet actor mapping and persistence validation, private payload copies, raw page contiguity/source-byte limits, exact token propagation and all passing regression checks. Preserve external Story 6.1/reminder changes and the committed baseline.

The workflow instruction to revert code before re-derivation cannot safely apply to this resumed run: all reviewed code is already committed at HEAD and the baseline diff includes external Story 6.1/reminder work. This run made no runtime code changes to revert. A blanket rollback would overwrite pre-existing work, contrary to repository/user preservation instructions. Preserve existing commits and re-derive in place under the already authorized implementation scope; there are no runtime changes from this run to revert. No runtime rollback, staging, commit, or branch mutation was performed.

## Design Notes

The user authorized the recommended PostgreSQL v1 feasibility probe on 2026-10-04.
The executable probe and retained observations are recorded in
[the feasibility report](evidence/story-6-6/postgresql-v1-feasibility.md).
The stock provider preserves embedded payload bytes but normalizes envelope JSON
and supplies no retained committed-generation lookup. Current-state recovery
after controlled post-commit acknowledgment loss succeeds; retrieving a complete
earlier committed image after head advancement is unsupported by the audited
stock contract. This is a negative provider feasibility result, not production
authorization or completed M2 qualification. Review a PostgreSQL transaction
capture extension before expanding integration; frozen intent and M1–M8
acceptance requirements remain unchanged.

One cohesive feature uses the existing four-slice policy. Slice 1 activates no breaking behavior; slice 2 establishes shared prerequisites; slice 3 integrates consumers and safety routes; slice 4 requires full migration/fleet/provider/major compatibility evidence before V2. Dormant preparation cannot claim activation.

## Verification

- `python3 _bmad-output/implementation-artifacts/6-5-integration/verify.py --mutations` — passed; approved digest `bc1625e3b8147fb0bc2cd9491fad8379a95c1ce0b597eba70f8d29f2fee3b050`; 20 obligations and 47 implementation follow-ups remain open.
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — passed, 0 warnings/errors. Focused built-DLL classes: EventPersisterTests 38/38, DaprDomainServiceInvokerTests 40/40, AggregateActorDomainResultTests 32/32.
- `dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — passed, 0 warnings/errors; BoundedPayloadPrimitiveTests 4/4.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — passed, 0 warnings/errors; AuthenticatedRawEventPageTests 5/5. A full Contracts assembly run before the final page-test additions passed 2,187 tests with 2 package-inventory skips because `EVENTSTORE_PACKAGE_CONTRACT_DIR` was unset.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false` — passed, 0 warnings/errors.
- `git diff --check 1329b35e52852952ecb2c94aabf100674e9691e3..HEAD` and `git diff --check` — passed.
- The `dotnet test` MTP invocation for Contracts.Tests reported zero tests and exit code 5; focused and full Contracts execution used the built xUnit assembly. Aspire dependents waited on Keycloak; no live-sidecar/provider run, full affected regression set, package/API compatibility gate, or two-host qualification completed. The acceptance matrix therefore remains open and the story is incomplete.
- Exact focused evidence and the environment limitation are recorded in `evidence/story-6-6/verification-2026-10-04.md`.

### Resumed-review verification — 2026-10-04

- Approval preflight with `--mutations` passed at `547c938d52e4fd31b47783b9dd032528a5100549`; 20 obligations/47 follow-ups remain open.
- Contracts, Client, and Server test projects rebuilt in Debug with `-p:UseHexalithProjectReferences=true -m:1`; each passed with zero warnings/errors.
- Built Contracts assembly `-class 'Hexalith.EventStore.Contracts.Tests.Events.*'`: 56 passed, zero failed/skipped.
- Built Client assembly `-class Hexalith.EventStore.Client.Tests.Events.BoundedPayloadPrimitiveTests`: 4 passed, zero failed/skipped.
- Built Server assembly, classes `EventPersisterTests`, `DaprDomainServiceInvokerTests`, and `AggregateActorDomainResultTests`: 110 passed, zero failed/skipped.
- These are focused checks of existing code, not Story 6.6 completion, missing compatibility/provider evidence, or activation authorization.

### Replay admission and cancellation slice — 2026-10-06

The [local verification](evidence/story-6-6/verification-2026-10-06-replay-admission.md)
records bounded private legacy replay admission, additive typed outcomes and
safe Admin mapping, built-in cancellation propagation, and V1 producer input
ownership. Review controls reproduced and fixed converter-cancellation and
contradictory typed-result failures. Release builds, local regressions and
package consumers were exercised; a separate SHA-bound Story 6.1 evidence
snapshot still triggers the repository content guard.

At the time of this slice, the [loader-policy options](evidence/story-6-6/dependency-loader-options-2026-10-06.md)
were a proposal. The subsequent owner decision is recorded below. Missing manifests/catalog closure and
production Dapr qualification still prevent activation. Frozen intent,
`baseline_commit`, parent status and all M1–M8 task dispositions remain unchanged.

### Reviewed trusted-code policy — 2026-10-06

The owner approved the recommended option 3. The
[current amendment](story-6-6-trusted-code-amendment.md) is a context input and
controls the conflicting stronger loader assurance without repinning historical
AD-13 approval. Complete authoritative catalogs and immutable artifact binding
remain required before readiness; local supplied-graph checks confer no authority.

The [local artifact qualification](evidence/story-6-6/verification-2026-10-06-trusted-code-artifacts.md)
records exact-file, pin, graph and manifest refusal controls and aligns the
existing primitives' assurance claims with this amendment. A consistent partial
supplied graph and a successful hash of a replaceable path confer no readiness.
Complete authoritative catalogs, immutable artifact execution binding, loader
observations and production qualification remain required; no registrations or
activation fences change.

### Page admission and observed capability loss — 2026-10-06

The [scoped local verification](evidence/story-6-6/verification-2026-10-06-page-preflight-and-capability-loss.md)
records whole-page preparation before domain catalog callbacks, private readable
ownership and retained metadata charges through page/range disposal, and sticky
observed-loss fencing shared by default manifest candidates in the admitted Client
load context. Qualified runtime observers and immutable execution binding remain
unimplemented; these local controls grant no readiness or universal before-effect
assurance.

The next M2 router fallback replacement requires actual per-domain payload types,
exact legacy aliases/formats, immutable serializer options, measured payload and
internal scratch/token bounds, and compatible once-only serializer declarations.
The existing optional bounded profile has no application registrations in the
inspected source; bounds and serializer behavior cannot be inferred safely. The
root-declared Hexalith.Platform owns production composition. Its architecture
assigns the EventStore-ratified template and Platform environment/provider
inventory, but its checked-in Redis components are explicitly local Development
assets and shared runtime/profile qualification remains owned work. No production
profile or authoritative event-evolution catalog was supplied by those inputs.
All M1–M8 tasks and activation gates remain open.
