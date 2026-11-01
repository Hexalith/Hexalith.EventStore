---
title: '4.16 Append write-once conformance through atomic conflict protection'
type: 'feature'
created: '2026-11-01'
status: 'draft'
route: 'dispatch'
story_key: '4-16-append-write-once-conformance-through-enforced-envelope'
review_loop_iteration: 0
planning_baseline: 'ba4c5548ad260b049bda5a1c7def2e8af3484733'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-01-story-4-16-atomic-conflicting-writes.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 4.5 observes event overwrite; admission fences and shared scopes do not establish NFR7 class (c).

**Approach:** Qualify a mandatory Dapr-owned actor commit primitive with durable revision comparison and create-once event identity. Preserve AggregateActor/IActorStateManager ownership and one actor-state transaction. Reuse the existing race, lawful-control and multi-process PostgreSQL fixtures. Candidate proof precedes migration; final conformance consumes 3.17/3.21, 5.6/5.7 and ratified AD-26. Missing Dapr capability or an erasure bypass blocks delivery.

## Boundaries & Constraints

**Always:** Final proof consumes 3.17/3.21, isolation/migration delivery from 5.6/5.7 and ratified AD-26. Preserve actor ownership and authorized erasure. Bind provider, topology, runtime, writers, mechanisms and tests; read acceptance state through Dapr. Falsify every mechanism beside a lawful positive control. Candidate observations supply no final conformance or authority.

**Never:** Ratify/select production inputs implicitly, widen access, substitute invocation ACLs or optional ETags for mandatory conflict rejection, introduce application provider SDK/SQL/private-key access, a cross-component transaction, a separate writer/proxy/policy/control service or general activation fencing. Only the approved atomic conflict contract is added to 4.16 scope. 5.6/5.7 retain reader/default/data migration and separate execution authority. A private runtime build or provider exception requires a separate exact-scope decision. No runtime implementation, key distribution, migration, dependency/submodule change or deployment is performed by this planning approval.

**Earlier owner decision (2026-11-01):** “apply recommendations” authorized bounded reuse/feasibility work and prerequisite ownership correction only; the resulting failed coarse policy remains historical.

**Approved scope renegotiation (2026-11-01):** “approve and accept” accepts the [atomic conflict-protection course correction](../planning-artifacts/sprint-change-proposal-2026-11-01-story-4-16-atomic-conflicting-writes.md) and its complete commit contract, supported-Dapr dependency strategy and existing-erasure-capability transport/HMAC-verifier custody extension. The prior frozen envelope-only intent is amended by this owner instruction. Capability feasibility, execution and final proof remain gated; this is not per-role AD-26 ratification or a production grant.

## I/O & Edge-Case Matrix

| Scenario | Input / state | Expected behavior | Failure handling |
| --- | --- | --- | --- |
| Candidate | Identified disposable runtime and lawful append | Record feasibility and logical readback | No production claim |
| Second writer | Same-identity raw transaction or foreign component/namespace/environment | Enforce exclusion or mandatory conflict rejection; original event remains | Missing/current ETag or caller flags cannot permit replacement |
| Placement failover | Old and successor activations hold the same durable revision | One overlapping batch wins; stale revision is rejected atomically | No newest-activation or new epoch-fencing claim |
| Removed barrier | One mechanism disabled | Its adversary exposes the missing exclusion | An unrelated fault proves nothing |
| Coarse restriction | Entire actor API disabled | Reject mechanism if lawful calls also fail | Do not call this protection |
| Final proof | Qualified mandatory primitive, ratified profile and delivered isolation | Every applicable writer has exclusion or mandatory rejection through failover and erasure | Missing prerequisites remain unproven |
| Authorized erasure | Existing signed lifecycle authority and bounded actor delete batches | Preserve governed removal/progress; invalidate stale revisions | Raw delete, TTL and delete/reinsert cannot bypass protection |
| Unknown outcome | Interrupted save acknowledgement or unavailable readback | Reconcile exact original batch through owning Dapr readback | No blind reappend or inferred absence |

</frozen-after-approval>

## Capability Dependency And Execution Gate

The scope decision is approved. The inspected SDK/runtime/component has no demonstrated mandatory primitive, so supported-Dapr capability delivery/qualification remains a blocking dependency. Do not infer support from actor ETags or TryAddStateAsync and do not begin 5.6/5.7 migration around an assumed feature. Carry and validate the existing erasure authority without a new issuer/service; the approved HMAC custody extension is subject to exact trust/version/secret-path qualification before any distribution. A private runtime build or provider exception remains a separate decision. Production selection, authenticated per-role AD-26 ratification and final conformance remain pending.

## Approved Commit Contract

1. **Ownership and scope.** AggregateActor stages and saves through its actor state manager. Any SDK enhancement sits behind that boundary. Dapr owns physical addressing and component calls. Application code receives no provider credentials and constructs no private actor-state keys. The protected namespace includes events and their metadata/recovery state; unrelated actor types retain their qualified semantics.
2. **Durable revision.** Every protected state-changing actor transaction presents the revision on which its state was based. Reads bind that revision to the loaded state; Save must not fetch a fresh revision and attach it to stale cached values. The same component transaction conditionally advances it. A missing first revision uses atomic create-if-absent, not a read-then-create shortcut. The revision is distinct from aggregate sequence and the OQ8 admission fence; metadata-only saves must not bypass it. The transaction also rejects a regressing stream high-water mark or an append inconsistent with its contiguous sequence range; only governed erasure permits the existing removal transition. A per-key ETag on an unrelated row is insufficient.
3. **Create once.** Each event identity may be created only if absent. Updating an existing event is rejected even with a matching current ETag. An expired event row is not permission to replace it; event TTL is refused. A collision anywhere aborts the complete batch, including earlier operations in that batch. Native uniqueness/conditional mutation and affected-row validation provide the serialization point. PostgreSQL's existing transaction boundary is the initial qualification target, with provider work confined to its Dapr component implementation.
4. **No raw bypass.** Mandatory mode applies to all loaded SDK/runtime write paths, HTTP/gRPC actor transactions, generic state/bulk/transaction paths if they can address the protected target, and metadata/delete mutations. Protection is selected by the bound actor/state contract, not a caller's claim that an event is ordinary mutable state; no general policy language or policy service is introduced. Missing guard support, an unguarded alias or a legacy writer makes the profile ineligible. A client-supplied `guarded=true`, actor app ID, API token, current ETag, or mutable erasure flag is not authorization to bypass it.
5. **Whole batch.** Retain events, metadata, snapshots, EventsStored checkpoint, publication index/receipts and existing commit witness in the same actor transaction. Keep the global allocator, admission actor, pub/sub and external effects outside that transaction; make no cross-component atomicity claim.
6. **Conflict versus unknown.** A definite failed precondition means no part of that batch committed. Clear staged/cache state before existing bounded reconciliation/retry. An interrupted acknowledgement is Unknown until fresh owning-actor readback identifies the exact original batch using the existing pipeline, publication index and commit witness. A later witness, absent/unavailable readback or an error class alone never licenses a blind second append. Retrying reevaluates current admission and current persisted state; never renumber and resubmit a stale event batch silently.
7. **Authorized erasure.** Preserve the existing lifecycle authority, actor ownership, tenant/domain/aggregate binding, bounded deletion and resumable progress. Protected delete requires that authority at the enforced boundary; ordinary raw DELETE, delete-then-insert, TTL and forged progress cannot evade create-once semantics. Erasure changes the durable revision and leaves only the already-authorized minimal completion state needed to reject stale writers; do not retain deleted event bodies or add an evidence ledger. Existing `TrustedEffectAggregateErasure` is a transport/authority input to reuse, not proof this is already implemented. Its present HMAC verifier also has signing material: it cannot be relabelled verification-only or copied into Dapr without Security's trust/custody decision. Failure to carry and validate existing authority without a new service blocks B; it does not permit disabling erasure or allowing all raw deletes.

**Proposed erasure integration for Security review:** Carry the original partition-bound lifecycle request/capability with each existing bounded actor deletion batch through the enhanced SDK transport. The enforcing Dapr path validates the existing canonical capability bytes, purpose, partition, inventory, expiry and completion/replay state before admitting that batch; revision/progress changes participate in its transaction. If reuse of the current HMAC design is approved, its existing key-ring generations are supplied to that verifier through the existing OpenBao/Dapr secret boundary, explicitly acknowledging that this enlarges the signing-capable trust set. No new issuer, key system or control service is proposed. Preserve resumability before completion and reject replay after completion. This is a concrete proposed custody change for decision, not an implicit permission to distribute keys; if Security rejects it, the present proposal remains blocked rather than inventing replacement authority.

**Failover limit:** CAS rejects a writer whose persisted basis has become stale. If two activations start from the same revision, one transaction may win and the other must lose. This contract does not claim that the winner is the newest placement activation, or introduce a new activation-epoch/lease system. Existing AD-5 admission remains mandatory. A requirement to reject an old activation before any successor state change would be additional fencing scope, not an implication of this proposal.

Threat boundary: test an adversary with the hosting application's sidecar identity/API token and raw API access. Do not grant that adversary erasure issuance keys. Compromise of the runtime, provisioning principal or erasure issuer is not solved by either approach; existing custody/isolation obligations remain. A backend credential accessible to an application, second writable component or stale unguarded sidecar fails the profile rather than becoming an accepted bypass.

## Code Map

- `src/Hexalith.EventStore.Server/Events/EventPersister.cs:267` stages unconditional writes; sibling `Actors/AggregateActor.cs:1781` commits; line 2487 erases lawfully.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceLiveSidecarTests.cs:63` supplies synchronization; its Redis readback is supporting diagnostics. Sibling `AggregateActorIntegrationTests.cs:284` supplies lawful append plus logical actor readback.
- `deploy/dapr/statestore-postgresql.yaml:34` shares state with Admin; `accesscontrol.yaml` governs service invocation. Neither excludes the raw actor-state transaction.
- `_bmad-output/planning-artifacts/architecture.md:151,341,549` defines the unchanged safety/ratification rules. Story 3.19 owns the canonical profile; AD-12 supersedes deleted 9.1 tooling.

## Tasks & Acceptance

**Preparation:**

- [x] Existing race/control tests and candidate inputs — inspect reusable work once; compare intact actor API access with removing that group using disposable configuration.
- [x] `_bmad-output/planning-artifacts/epics.md` and `architecture.md` — assign isolation/migration to 5.6/5.7 and preserve final profile/assurance gates; record the course correction separately.

**Approved-scope application:**

- [x] Record the owner's “approve and accept”; amend this intent, PRD, architecture, epic acceptance and prerequisite handoffs while retaining lifecycle values and historical observations.

**Delivery after capability qualification:**

- [ ] With 3.17/3.21, qualify exact supported SDK/runtime/component semantics, every reachable raw write/erase path and the approved lifecycle-capability verifier trust/custody boundary. Preserve unresolved capability results; select no private build implicitly.
- [ ] Integrate mandatory revision/create-once/whole-batch semantics behind IActorStateManager. Keep events, metadata, snapshot, EventsStored checkpoint, publication state and existing commit witness in one actor transaction.
- [ ] Preserve cache clearing, current admission and exact original-batch reconciliation on conflict/unknown outcomes; no blind stale-batch replay.
- [ ] Extend existing LiveSidecar.Tests and Server.Tests lanes for AC1–AC11 using AppendDurabilityRaceControl/Session, lawful actor and snapshot tests, infrastructure-failure/erasure tests and Oq8PostgresqlFixture. Add only bounded missing partition/readback controls; no new fixture stack or evidence machinery.
- [ ] Consume 5.6 local bindings/reader/default/data migration, 5.7 production isolation/guarded-initialization/migration/rollback and fresh OQ8 regression, 3.18 compatibility, 7.6 custody and 7.22 restore inputs; do not execute their migrations in 4.16.
- [ ] Evaluate final production-path proof only on the ratified canonical AD-26 profile and existing AD-12/9.2 assurance. Advance lifecycle only on actual applicable delivery and proof.

**Acceptance Criteria:**

All prevention oracles use fresh logical reads through the owning Dapr actor boundary after clearing cache or restarting the reader. Compare exact event identity and content, metadata, snapshot sequence, pipeline, publication/recovery ownership and the existing witness as applicable. A mock, HTTP status or physical Redis/PostgreSQL snapshot alone cannot pass these criteria.

| AC | Given / when | Required result and reuse |
| --- | --- | --- |
| AC1 Lawful operations | Protected mode; lawful append, zero-event command, snapshot, publication/recovery and restart | All retain their established behavior and atomic state. Reuse `AggregateActorIntegrationTests` and `Events/SnapshotIntegrationTests` in LiveSidecar.Tests; existing Server.Tests failure/recovery lanes provide focused regressions. |
| AC2 Existing event | Actor has committed event N; same-identity/token raw transaction attempts replacement, including current/stale/omitted ETags and caller flags | Every replacement fails; original bytes/identity remain. Exercise both exposed actor transports and any generic-state/bulk/transaction alias that can reach the target; if no alias is reachable, prove that denial with a separate lawful generic-state control. Extend the existing append-race test infrastructure. |
| AC3 Raw-first race | Suspend actor after its source-state read; raw contender attempts the overlapping write; release actor | An unguarded contender is refused, or a correctly guarded first commit is retained and the stale actor batch is rejected. No acknowledged durable event disappears. Reuse `AppendDurabilityRaceControl`, `AppendDurabilityRaceSession` and the existing classifier's synchronization checks. Test both commit orders and a new empty stream. Never overwrite to make the lawful actor win. |
| AC4 Atomic rollback | Multi-event batch; a later event identity already exists or revision is stale; earlier operations have been attempted | No new event, metadata advance, snapshot/checkpoint, publication entry, receipt or witness from the losing batch persists. Exercise a collision late in the operation order. Reuse the actor fixture plus `AggregateActorInfrastructureFailureTests` patterns; live PostgreSQL end-state is mandatory. |
| AC5 Failover overlap | Two independent hosts; suspend old writer after its read; placement disconnect/rebalance permits successor commit; resume old writer while its database path remains usable | Old revision cannot commit; successor event and coupled state survive. Repeat reversed ordering: exactly one overlapping batch wins, with no overwrite. Reuse `Oq8PostgresqlFixture` process lifecycle and race gate. Add only the bounded partition/resume control and logical actor read helper it lacks; killing the old process alone does not prove stale-writer rejection. |
| AC6 Unknown outcome | Commit succeeds but acknowledgement is lost; or cancellation/readback is unavailable | Exact original committed batch is recovered once; ambiguous/unavailable results fail closed. No blind reappend, fabricated conflict success or duplicate publication/effect. Reuse the existing commit-witness/pipeline/publication inspection and infrastructure-failure fixtures, adding a live acknowledgement-loss case. |
| AC7 Erasure | Valid existing lifecycle request versus invalid/expired/cross-aggregate/replayed authority, raw DELETE, delete+insert and TTL | Governed erasure remains resumable and reads back absent event bodies. Invalid/raw destructive calls fail. A stale pre-erasure append cannot recreate the erased history. Reuse existing trusted-effect lifecycle/erasure tests and the PostgreSQL fixture; the DataContract serialization test alone supplies no enforcement proof. Do not expand this into the full AD-30/GDPR workflow. |
| AC8 Isolation | Other app principal, alternate component target, same app ID in another namespace/environment | All cannot mutate the actor target beside a lawful control, under delivered 5.6/5.7 bindings. Keep those prerequisites and their OQ8 regression mandatory. |
| AC9 Upgrade/restore | Existing history without revision state, two initializing writers, old runtime/SDK writer, mixed guard configuration, restore and rollback | Existing event bytes remain; initialization is atomic; unsupported/mixed writers fail readiness or writes. Restore revisions and erasure state consistently. Package-only consumer/read/snapshot behavior passes 3.18; migration/rollback observations stay owned by 5.6/5.7/7.22. |
| AC10 Falsification | Remove the mandatory check or create-once rule on a disposable candidate; repeat its exact adversary | The corresponding prevention test fails while the intact lawful control passes. Separately removing revision CAS must expose stale metadata-only commits; removing create-once must expose replacement even with a fresh revision. A startup failure proves neither. Reuse existing mutation/fault-injection conventions; no production bypass switch is shipped. |
| AC11 Final gate | Exact qualified SDK/runtime/component, complete writer inventory, delivered prerequisites and ratified canonical profile | Every applicable prevention/erasure/failover/compatibility test passes on the production path with existing AD-12/9.2 assurance. Unknown capability, unavailable lane or an unguarded writer keeps G-APPEND FAIL/BLOCKED. Runtime-minor or bound-input changes require re-proof. |

## Implementation Notes

2026-11-01 preparation: Reused existing tests. The coarse API policy cannot distinguish these writers; it is rejected without proving all envelopes impossible.

2026-11-01 approved scope: Mandatory atomic conflict protection is now within the bounded story scope. No runtime work has been performed; capability qualification, erasure enforcement, broader migration and final conformance remain pending.

## Spec Change Log

- 2026-11-01: Owner “approve and accept” renegotiated the frozen envelope-only intent, accepted the supported-Dapr dependency and existing erasure transport/HMAC custody extension, and adopted AC1–AC11. The display title changes; story key/path, draft/backlog lifecycle, historical verification and 5.6/5.7 ownership remain unchanged.

## Review Triage Log

## Design Notes

Component scope is not per-key ownership. Optional ETags or existence checks cannot enforce the approved contract against a raw caller. The mandatory primitive is not supplied by the inspected combination; qualification must resolve that gap without application provider access or another control service.

## Verification

- `dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — PASS, zero warnings/errors.
- Built assembly, `-method '*AppendDurabilityRaceLiveSidecarTests.SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome'` — 1/1 observational PASS; classified `same-key-overwrite-raw-durable-write-lost`.
- Explicit actor API allowlist: race plus `-method '*AggregateActorIntegrationTests.ProcessCommandAsync_PlumbsAggregateTypeThroughActorPipeline'` — 2/2 PASS, but overwrite persists. Removing only the actor group: 0/1 PASS; fixture actor warm-up failed with `failed getting app id either from the URL path or the header dapr-app-id`; mechanism rejected.
- Tested source: `2a1d16d76758c687506c9f375354303430262e7c`. Dapr 1.18.7/CLI 1.18.2, Redis fixture; no AD-26 proof or CI run. Commands/configuration/limitations: [course correction](../planning-artifacts/sprint-change-proposal-2026-11-01-append-envelope.md). Output stays in scratch; final acceptance requires production-path CI and runtime-minor re-proof.
