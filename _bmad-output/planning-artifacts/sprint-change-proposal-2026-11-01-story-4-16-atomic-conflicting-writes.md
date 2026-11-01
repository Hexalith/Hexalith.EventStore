# Story 4.16: Atomic Conflicting-Write Protection

Date: 2026-11-01

Status: **Approved; planning amendments applied.** The owner said “approve and accept” on 2026-11-01, accepting all three decisions in §5: the bounded scope/architecture amendments, supported-Dapr dependency strategy and proposed erasure-verifier custody extension. The accepted pre-application proposal SHA-256 is `ff231ddce2ad8d3924234a22262f58f11bc6cf1f0ddc3f8bb277824487b73738`. This is a conversational scope/intent approval, not authenticated per-role AD-26 ratification or a production grant. No runtime implementation, key distribution, dependency/pin change, migration, publication or deployment is performed by this planning application. Story lifecycle values stay unchanged and the capability/final-proof gates remain pending.

Predecessor: [Feasibility before append-envelope migration](sprint-change-proposal-2026-11-01-append-envelope.md). Owning [4.16 specification](../implementation-artifacts/spec-4-16-append-write-once-conformance-through-enforced-envelope.md).

## 1. Issue And Investigation Result

**Recommend a bounded change from envelope-only proof to mandatory atomic conflict protection inside Dapr's actor persistence path.** Keep AggregateActor and `IActorStateManager` ownership, the existing actor transaction and the assigned 5.6/5.7 isolation/migration work. Do not move events to a generic state component or introduce another writer service.

No supported exclusion configuration was demonstrated for the inspected profile. This is a capability gap on identified inputs, not a proof that every possible operating envelope is impossible. The recommendation below requires a Dapr SDK/runtime/component capability that is **not supplied by the inspected combination**; it is a proposed engineering scope, not an existing setting or an implementation-ready dependency selection.

### Established inputs

- Repository HEAD: `f704401ff9033919b1e9925564d814d7410bfe3d`, with the owner's existing planning changes preserved. The predecessor's live runs used `2a1d16d76758c687506c9f375354303430262e7c`; they were not rerun here.
- Candidate lock: [runtime-lock.json](../../deploy/dapr/qualification/runtime-lock.json), daprd `1.18.7`, components-contrib `1.18.6`, CLI `1.18.2`. Runtime source commit `b32a815fa2260983e181b95272ea555fe17d3895`; component source commit `eac9e31dd14cd798c9996aae30818b572aa12c59`. Installed/source-owned actor SDK: `Dapr.Actors` `1.19.0`. These remain non-authorizing candidate inputs.
- Known live result: allowing the actor API preserves lawful append/readback but records `same-key-overwrite-raw-durable-write-lost`; removing that API group breaks lawful actor warm-up. The coarse policy is rejected.
- [EventPersister](../../src/Hexalith.EventStore.Server/Events/EventPersister.cs) stages event upserts and metadata. [AggregateActor](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs) saves the events, snapshot, EventsStored checkpoint, publication/recovery state and batch witness together. Its authorized erasure path deletes event state in bounded batches and persists progress.

### Simplest supported mechanisms checked

| Mechanism | Finding and consequence |
| --- | --- |
| Dapr API allow/deny list, including HTTP/gRPC separation | Configuration selects API group, version and protocol. Actor persistence and raw actor-state transactions share the relevant actor endpoint. Transport selection does not distinguish two requests on that endpoint with the same authority. [API-list documentation](https://docs.dapr.io/operations/configuration/api-allowlist/), [pinned HTTP actor endpoints](https://github.com/dapr/dapr/blob/b32a815fa2260983e181b95272ea555fe17d3895/pkg/api/http/actors.go). |
| Component scope, network reachability, sidecar token, invocation ACL | Useful against other principals and workloads. A raw request using the hosting app's valid API token reaches the same persistence API. Invocation authorization does not impose a condition on that write. Physical isolation remains mandatory under 5.6/5.7; it does not settle this case. |
| `AddStateAsync` / `TryAddStateAsync` | The installed manager checks its cache/provider for existence and stages an Add. The state provider serializes both Add and Update as `upsert`, without ETag or concurrency options. An intervening durable write can still be replaced at save. This is not atomic create-only storage. [SDK manager](https://github.com/dapr/dotnet-sdk/blob/v1.19.0/src/Dapr.Actors/Runtime/ActorStateManager.cs), [SDK provider](https://github.com/dapr/dotnet-sdk/blob/v1.19.0/src/Dapr.Actors/Runtime/DaprStateProvider.cs). |
| Actor transaction ETags | The pinned runtime **does accept and forward ETags** in actor upsert/delete requests. The installed manager does not expose them. More importantly, missing ETags remain legal; an unconditional raw transaction bypasses cooperative OCC. Do not describe actor ETags as wholly absent, or infer mandatory protection from their presence. [Pinned transaction conversion](https://github.com/dapr/dapr/blob/b32a815fa2260983e181b95272ea555fe17d3895/pkg/actors/api/transactional.go), [state API semantics](https://docs.dapr.io/reference/api/state_api/). |
| PostgreSQL v1 `first-write` | Generic state requests can request a conditional insert/update. Actor conversion does not carry this option, and the component's default is an upsert. The v1 implementation has a native conditional query and transaction rollback, but no inspected metadata setting makes event writes obligatorily create-only for every caller. [Pinned query builder](https://github.com/dapr/components-contrib/blob/eac9e31dd14cd798c9996aae30818b572aa12c59/state/postgresql/v1/postgresql.go), [component configuration](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-postgresql-v1/). |
| Placement, drain and activation identity | Placement coordination is not a storage compare-and-swap. The pinned actor-state path checks hosting and coordinates with placement, then calls the component transaction. That is not a durable revision condition. The 1.19 activation-ID announcement concerns SDK cache identity, not a commit fence; no runtime upgrade is selected here. [Pinned actor state](https://github.com/dapr/dapr/blob/b32a815fa2260983e181b95272ea555fe17d3895/pkg/actors/state/state.go), [1.19 release](https://github.com/dapr/dapr/releases/tag/v1.19.0). |

Source inspection supports these limits; it supplies no new production or failover proof. No narrower supported configuration was found that denies the raw request while allowing the identical lawful persistence request. A source scan that finds no production raw caller remains useful inventory evidence, but cannot turn an available same-identity write API into mechanically enforced storage protection.

## 2. Approaches And Recommendation

Only two approaches are evaluated. **A does not meet the full acceptance boundary. B is the recommended scope proposal, conditional on delivering and qualifying its missing Dapr capability. There is no demonstrated stock-Dapr solution to approve for deployment.**

| Concern | A — Cooperative conditional actor batch | B — Mandatory Dapr-owned conflict guard (recommended scope) |
| --- | --- | --- |
| Enforcement | Extend SDK transport to send revision/ETag conditions and create-only event intent to one actor transaction. Conditions are caller-selected. | Enforce the protected actor-state contract at the common Dapr persistence boundary; the component atomically compares a durable revision and creates event identities once. Callers cannot opt out. |
| Same-identity raw writer | A raw request without conditions can still replace committed values. A raw request with the current ETag can deliberately update them. **Fails the required threat case.** | A missing/malformed precondition is refused for protected state. Even a raw caller with the current revision cannot replace an existing event. Actor and generic APIs cannot offer an unguarded alias. |
| Stale activation/failover | Conditional, cooperating writers can conflict; an unconditioned path still wins. | Once another batch advances the durable actor revision, a suspended writer's batch fails as a whole. Create-once event identity independently rejects overlapping events. Placement availability is not the safety predicate. |
| Atomicity | Genuine CAS is atomic for requests that use it; `TryAddStateAsync` followed by ordinary Save is not. | Revision condition, event insertions and all staged actor changes commit or roll back in one component transaction. No existence check outside that transaction authorizes the write. |
| Compatibility/migration | Requires SDK/runtime protocol support and cache/retry changes; moving only event writes to generic state would split the existing transaction and is rejected. | Requires versioned SDK/runtime/component support behind `IActorStateManager`, capability detection and coordinated rollout. Preserve event bytes and logical identities. Existing data still needs guarded initialization and 5.6/5.7 migration rehearsal. |
| Operations | Lower initial engineering effort, but cannot close G-APPEND. | More qualification and runtime maintenance. Prefer a released/upstream-supported Dapr capability. A private runtime build would require a separate explicit maintenance/pin decision; this proposal does not select one. |

**Select direct adjustment of Story 4.16 to B.** It changes the persistence mechanism without reducing NFR7, SM11 or the writer denominator. A local C# preflight is smaller code but cannot satisfy the same-identity raw-write requirement. Database triggers, RLS, application SQL, a new state-store service, and proxies/policy engines are not proposed: they either change the no-schema boundary or add the excluded machinery.

### Required commit contract for B

This is a proposed semantic contract, not the name or syntax of an existing Dapr API.

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

## 3. Impact, Compatibility And Sequence

**Scope classification: Moderate backlog change with high correctness risk and material upstream dependency.** No new epic or rollback of completed features is recommended. Engineering effort is medium-to-high across SDK transport, Dapr commit semantics and component qualification; no credible calendar promise exists before capability ownership and erasure feasibility are settled. This is larger than a configuration fix. The PRD MVP gains bounded storage-protection implementation scope, while its safety target stays unchanged.

| Existing owner | Amendment / retained responsibility |
| --- | --- |
| 4.16 / EventStore persistence | Own semantic contract, Dapr capability gap, actor integration, conflict/unknown handling and reused acceptance tests. Keep historical 4.5 observations unchanged. |
| 3.17 / boundary owner | Qualify protected operations and every alias, raw mutation and erasure path on exact versions. Record a capability gap until resolved; accept no provider exception implicitly. |
| 3.18 / compatibility owner | Classify internal protocol, SDK/default/configuration behavior and package-only consumer effects. No new public domain persistence API is needed. |
| 3.21 / runtime qualification; Builds / pins | Supply/qualify an exact capable runtime/component combination; changed candidates need fresh applicable runtime/broker checks. No dependency or gitlink changes in this proposal. |
| 5.6 | Keep local per-app component bindings, reader/data/default migration and Platform composition handoff. Add capability-mode parity; do not move this work to 4.16. |
| 5.7 / Platform | Keep production targets/principals, one-app-ID isolation, migration/rollback, loaded-sidecar parity and fresh `oq8-postgresql-v1` multi-host regression. Add guarded initialization/upgrade rehearsal. |
| 5.8, 5.9, 7.6, 7.8, 7.22 | Existing drift/runbook, custody, resiliency and restore owners consume the changed contract. Restore actor state and its revision/erasure completion state consistently; never restore an older revision beside newer events. |
| 3.19, 9.2, 9.3 | Keep canonical profile, assurance and baseline responsibilities. Update subjects only after approval; no new packet, evidence store, manifest or validator is proposed. |
| Epics 1, 2, 6, 8; UX | Preserve domain/client APIs, projection behavior, event bytes/upcasters/folded snapshots and post-MVP payload/legal erasure scope. Run relevant regression/compatibility checks; no new UI or automatic broad GDPR claim. |

After scope approval: first settle a supported Dapr capability and the existing-erasure transport/trust design. An undocumented runtime patch is not a supported exclusion. If the capability needs upstream development, that is a blocking delivery dependency; do not start 5.6/5.7 migration around an assumed feature. A private-build fallback, if desired, returns for an explicit owner decision.

Then exercise the disposable candidate with reused fixtures. Only a credible result proceeds to the assigned 5.6/5.7 work and AD-26 ratification; final 4.16 conformance consumes all those prerequisites. Ratification does not precede feasibility merely to authorize a disposable test.

Keep serialized event, MessageId, aggregate sequence and GlobalPosition semantics stable. Initializing Dapr-owned revision state for existing actors is a controlled compatibility/data-layout transition, even if no event is rewritten. Mixed old/new writers are prohibited while protected mode is enabled. Use a quiesced, rehearsed initialization/upgrade boundary under 5.7; absence of a revision must not cause existing history to be treated as empty. Rollback may require stopping writes and restoring a consistent pre-change state: do not promise a rolling binary downgrade that silently removes protection. 5.7 owns the exact plan and rollback limits, not this proposal.

## 4. Concrete Amendments For Approval

The before/after amendments below were approved with “approve and accept” on 2026-11-01 and applied together to the owning PRD, architecture, spec, epic and prerequisite handoffs. The owner renegotiated the prior frozen 4.16 intent. AD-26 target ratification and capability qualification remain outstanding; this section retains the reviewed old/new rationale.

### Architecture AD-5 — replace the opening of the append-race paragraph

**OLD:** From `The owner-selected path (2026-10-07, G-APPEND) is envelope first:` through `the class remains unmet until that proof passes.`

**NEW:**

> Story 4.16 may satisfy NFR7 class (c) through a mechanically enforced no-second-writer envelope, or through mandatory atomic conflicting-write protection in the Dapr-owned actor persistence path. The latter requires one qualified actor transaction to condition on a durable actor-state revision, create each event identity only if absent, and atomically commit or reject all staged actor state. Same-identity raw calls cannot opt out, replace committed events, or use deletion or TTL as a bypass; existing authorized erasure remains available through its governed authority and invalidates stale writers. AggregateActor remains the sole event-append coordinator and mutations remain IActorStateManager-owned. A current OQ8 admission fence, optional ETag, placement lock or client existence check does not supply this contract. The exact protection mode, SDK/runtime/component versions, writer inventory, erasure behavior and failover assumptions bind into AD-26; production-path positive controls, adversarial writes and barrier-removal tests must pass. Unsupported or unproven protection leaves G-APPEND unmet. This approves no new activation-epoch system, application provider access or cross-component transaction.

Retain verbatim the remainder beginning `Component scopes and ACLs act per component` and all its per-app physical-target, reader migration and runtime-minor re-proof requirements. AD-1/AD-34 remain unchanged: Dapr owns provider work; no EventStore SQL, schema or private-key adapter is authorized. In the architecture diagram, label the existing AggregateActor → Dapr actor-state edge with the qualified mandatory commit contract; add no service or alternate event store.

### Architecture AD-26 — exact phrase amendments

| Location | OLD | NEW |
| --- | --- | --- |
| Ratification | `NFR7 class (c) envelope path` | `NFR7 class (c) protection path and its qualified actor-transaction semantics` |
| Production proof | `the AD-5 append operating-envelope identity with its writer inventory and placement/failover configuration` | `the AD-5 append/conflict-protection identity, mandatory guard mode, qualified SDK/runtime/component versions, writer inventory, placement/failover configuration and authorized-erasure semantics` |

Keep the no-application-schema/DDL/provider-credential sentence, profile slot, PostgreSQL v1 target assumption, isolation, restore, assurance and production prohibitions. Replace the G-APPEND status-row next action with this approved scope and its capability/prerequisite gates. AD-5 and AD-26 edits change the ratification subject: record that fact in the existing memlog, recompute the subject using the existing rule, and obtain fresh per-role ratification when credible inputs exist. Old approval/attestation bytes do not authorize the amended subject.

### Story 4.16 — title, intent and boundaries

**OLD title:** Append Write-Once Conformance Through An Enforced Operating Envelope.

**NEW title:** Append Write-Once Conformance Through Atomic Conflict Protection.

Keep the existing story ID/key and paths to avoid unnecessary renumbering/migration; a displayed title change does not advance lifecycle status.

**OLD approach:** `Check reusable inputs once, then test the simplest exclusion mechanism on a disposable candidate.`

**NEW approach:**

> Qualify a mandatory Dapr-owned actor commit primitive with durable revision comparison and create-once event identity. Preserve AggregateActor/IActorStateManager ownership and one actor-state transaction. Reuse the existing race, lawful-control and multi-process PostgreSQL fixtures. Candidate proof precedes migration; final conformance consumes 3.17/3.21, 5.6/5.7 and ratified AD-26. Missing Dapr capability or an erasure bypass blocks delivery.

**OLD boundary:** `Never ... add fencing ... Failed simple mechanisms stop for a separate scope decision.`

**NEW boundary:**

> Only the approved atomic conflict-protection contract is added to 4.16 implementation scope. No separate writer/proxy/policy/control service, application provider SDK/SQL/private-key access, cross-component transaction, general activation fencing, data migration or deployment is authorized by this story. 5.6/5.7 retain migration ownership and separate execution authority. Existing admission, authorized erasure, compatibility and assurance rules continue to bind. A private runtime build or provider exception requires a separate exact-scope decision.

Replace the Remaining Design Decision with the capability/erasure delivery dependency, and replace predetermined workflow/document tasks with: qualify the Dapr primitive; integrate behind the actor manager; preserve the atomic batch; implement the following acceptance tests in existing lanes; consume prerequisite/profile/assurance results. Do not recreate deleted evidence machinery. Add these amendments to the story change log only when accepted.

### Story 4.16 — replacement acceptance criteria and reused fixtures

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

Existing fixture locations: [live actor tests](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/), [live fixtures](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/), [server actor tests](../../tests/Hexalith.EventStore.Server.Tests/Actors/). Extend these lanes; do not create another fixture stack, database-inspection acceptance lane or separate evidence framework. Physical diagnostics remain optional supporting observations.

### PRD And Epic Amendments

| Artifact/section | OLD → NEW / rationale |
| --- | --- |
| PRD §9.2 Safety boundary | `Provider-level append fencing and write-once storage enforcement remain outside MVP implementation scope.` → `Story 4.16 includes the approved mandatory Dapr-owned atomic conflict-protection contract for each qualified supported write profile. General activation fencing, application provider persistence and unqualified provider claims remain outside this scope.` Replace the following scope-exclusion/fence-only sentences with: `FR31 race observation alone grants no implementation authority. NFR7 class (c), SM11, readiness and MVP completion remain blocked until the approved mechanism or an enforced exclusion envelope prevents loss and passes production-path proof.` |
| PRD G-APPEND criterion and OR4 | Add the mandatory Dapr-owned atomic conflict contract as an explicitly approved implementation/conformance path beside a proven exclusion envelope; require exact supported-profile proof. Preserve the full raw-writer denominator and no-waiver rule. Change the primary-owner status phrase from `envelope first` to `atomic conflict protection; capability and final proof pending`. Do not call a PostgreSQL-only result provider-portable. |
| PRD NFR7 / SM11 | Keep requirements and five-of-five target unchanged. SM11 remains four-of-five until accepted prevention proof exists; revise its description only after delivery, not on proposal approval. |
| Epic 4 summary / 4.16 | Replace envelope-only mechanism with the approved contract and AC1–AC11; retain OR4/NFR7/SM11 ownership, Story 4.5's observational status, 4.17 and 4.9–4.15 boundaries. No new epic. |
| 5.6 / 5.7 / 5.8 | Retain all assigned isolation, reader/default/data migration, principal/target and rollback work. Add mandatory-mode/version parity, guarded initialization, mixed-writer refusal and restore compatibility to existing acceptance; no migration work is reassigned to 4.16. |
| Architecture gate rows / 3.17 / 3.21 / 7.22 | Carry the missing qualified primitive as a dependency; include protected erase paths, revisions and restore consistency in existing inventories and checks. No new evidence machinery or exception is deemed accepted. |
| Sprint tracking / UX | Keep current lifecycle values and UX flows. After approval, add a scope-reference comment only; do not mark 4.16 ready/done or open a successful UI outcome. |

## 5. Owner Decisions And Handoff

All three decisions below were accepted by the owner with “approve and accept” on 2026-11-01. Their original decision text is retained for scope; no repeated approval is required for these planning amendments:

1. **Product + Architecture + persistence owner:** approve the bounded 4.16 implementation/conformance scope and the concrete PRD/AD-5/story amendments above, preserving the complete safety requirement and 5.6/5.7 prerequisites. Approving proposal text is not an instruction to implement or migrate in this run.
2. **Architecture + Platform owner:** accept the Dapr capability as a blocking upstream/runtime dependency and the amended AD-26 ratification subject. Default to a qualified supported Dapr release. Selecting a private runtime build, a provider exception or a different production target would need a further exact-content decision; none is requested or selected here.
3. **Security / existing erasure authority owner:** approve or reject the concrete erasure transport/HMAC-verifier custody extension above and the retained minimal completion state. A same-identity API token or unsigned progress record cannot stand in for that decision. Rejection blocks the recommendation pending a separately accepted design; it does not widen existing erasure authority.

Planning application complete: PO/Architect responsibilities are reflected in the amended artifacts and written developer handoff. Next, the persistence developer qualifies capability feasibility with the existing tests before implementation handoff; Platform retains 5.6/5.7 composition/migration execution; Test and the existing assurance workflow evaluate final conformance. No messages or external handoffs were sent. If capability or erasure cannot satisfy the contract, report that precise blocker and return to course correction without silently choosing SQL, a private fork or another service.

The bmad-correct-course [approval step](../../.agents/skills/bmad-correct-course/SKILL.md) is satisfied by the owner's explicit “approve and accept.” The original instruction excluding runtime implementation, migration and deployment still applies; accepted planning work is complete without a repeated confirmation step.

## 6. Verification And Change-Navigation Record

**Proposal preparation (before approval):** Repository/source/API investigation created only this proposal. It ran no new live product tests, implemented no guard, changed no dependency/pin, and performed no migration or deployment. The earlier live results are attributed to the predecessor, not counted as new prevention tests. CI run: none; G-APPEND remains FAIL/BLOCKED.

Executed API inspection:

```bash
dnx dotnet-inspect -y -- member IActorStateManager --package Dapr.Actors@1.19.0 -s Methods -v q -n 35
dnx dotnet-inspect -y -- member ActorRuntimeOptions --package Dapr.Actors@1.19.0 -s Properties -n 15
dnx dotnet-inspect -y -- member ActorHost --package Dapr.Actors@1.19.0 -s Properties -n 10
```

All three passed. The initial skill-suggested `--oneline` option was rejected by the available CLI (`Error: Unrecognized option '--oneline'.`); the successful commands above used its inspected current syntax. The API/source findings do not assume a supported custom state-provider injection hook: the required SDK integration must be delivered/qualified, not inferred from the interface name.

Pinned runtime/component source was fetched read-only into `/tmp/story-4-16-scope-source`; the inspected actor transaction/state and PostgreSQL query/transaction sources matched the lock's exact commits. No source checkout, package reference, submodule or tracked lock was modified. The web reader could not retrieve several pinned source pages; direct HTTPS retrieval of the exact upstream source succeeded. Source links above identify those bytes; scratch downloads are not a new retained evidence packet.

Document validation: `git diff --check` passed for the existing tracked changes. A focused Python check passed for this new proposal's local links, exact amendment anchors, unique AC1–AC11 coverage and preservation hashes of all eight existing input/changed artifacts; none of those artifacts changed during this run. The final check also covers the new untracked file's whitespace, LF endings and conflict markers. No product build/test is needed for this documentation-only deliverable; the proposed acceptance tests remain future work.

| Checklist | Status | Disposition |
| --- | --- | --- |
| 1.1–1.3 | [x] | Trigger, known live evidence and bounded source findings distinguished. |
| 2.1–2.5 | [x] | Existing epic/story adjustment and prerequisite sequence mapped; no new or obsolete epic. |
| 3.1–3.2, 3.4 | [x] | PRD scope conflict, architecture subject, compatibility, runtime, tests, custody and operations addressed. |
| 3.3 | [N/A] | No UX change. |
| 4.1, 4.4 | [x] | Direct adjustment recommended; only mandatory protection can meet the stated raw-writer contract. |
| 4.2 | [N/A] | Reverting completed functionality does not remove the persistence race. |
| 4.3 | [x] | Add bounded MVP implementation scope; no safety/SM11 reduction or waiver. |
| 5.1–5.5, 6.1–6.2 | [x] | Concrete before/after amendments, acceptance and ownership supplied. |
| 6.3 | [x] | Owner explicitly accepted all three scope decisions with “approve and accept” on 2026-11-01; no production ratification inferred. |
| 6.4 | [x] | Approved epic/spec/prerequisite amendments and sprint scope-reference comments applied; all lifecycle values preserved. |
| 6.5 | [x] | Written handoff and stop conditions supplied; execution remains gated. |


## 7. Approval Application And Handoff

Owner instruction: “approve and accept” (2026-11-01). Classification: Moderate backlog amendment with the previously identified high correctness risk. Product/Architecture scope, supported-Dapr dependency strategy and the existing-erasure-capability transport/HMAC signing-capable verifier custody extension are accepted as reviewed. This planning receipt does not distribute keys, qualify a runtime or replace authenticated per-role AD-26 records.

Applied to PRD §9/G-APPEND/OR4, architecture AD-5/AD-26/diagram/gate rows, the owning 4.16 spec and epic acceptance, 3.17/3.21 capability handoffs, 5.6/5.7/5.8 mode/migration/rollback parity, 7.6 custody, 7.22 restore, sprint comments and the architecture memlog. Existing 5.6/5.7 assignments, draft/backlog/in-progress lifecycle values, historical source/test observations, package/runtime pins and prior immutable authority inputs are retained.

AD-26 ratification subject changed from `2ed96f02278d2562a6777a4f33e76cd1bb7eae46fac1d7a072094f6eaa298d90` to `b9e899ecd6bb456537358b23cb69a564e09bc2c7b199dd657075900b2d17503b` using its exact append-paragraph plus AD-26 UTF-8/LF rule, including revision/erasure restore consistency. Existing attestations cannot authorize the new subject. Fresh authenticated per-role ratification remains subject to the existing main-push-based 24-hour window; no commit/push, window start or ratification is claimed here. Planning-baseline/input digests are not repinned by this amendment.

Written handoff: persistence developer + 3.17/3.21 first qualify the missing supported-Dapr capability and accepted erasure integration; only credible results proceed to separately owned 5.6/5.7 migration and final ratified-profile proof. No external message was sent. G-APPEND remains FAIL/BLOCKED; no product test, implementation, migration or deployment is claimed by approval application.
