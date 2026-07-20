---
title: Sprint Change Proposal - OQ8 Durable Idempotency Admission
date: 2026-07-20
status: approved
scope_classification: Major
workflow: bmad-correct-course
trigger: OQ8 expired-key reuse can reach EventStore domain execution
decision_owner: Administrator
implementation_owner: Hexalith.EventStore
consumer_owner: Hexalith.Folders Story 12.6
final_approved_by: Administrator
final_approved_on: 2026-07-20
---

# Sprint Change Proposal - OQ8 Durable Idempotency Admission

## 1. Executive Summary

Hexalith.EventStore currently removes an exact-match idempotency record when its
application-visible replay retention expires, commits that removal, and permits
the command to continue. Because the aggregate actor does not terminate on the
`Expired` outcome, reuse of an expired key can reach aggregate pipeline and
domain-service execution. A later reuse then observes a miss because the record
has already been deleted.

The approved Folders OQ8 design version 1.0.0 prohibits that behavior. It selects
an EventStore-owned, tenant/key-partitioned durable admission actor with trusted
canonical-intent descriptors, reservation and fencing, replay and conflict
handling, recoverable and unknown-outcome states, inclusive expiry, and
metadata-only consumed-key tombstones. The design is cross-aggregate: repairing
only the current aggregate-local expiry branch would leave same-tenant key reuse
against another target undetected.

The recommended path is a **Major-scope direct adjustment**:

1. Add focused EventStore Story 4.8 under the existing Epic 4.
2. Strengthen existing PRD requirements FR27, NFR7, and NFR16 without adding or
   renumbering stable requirement IDs.
3. Add architecture decision AD-25 and update the canonical Phase 4 SPEC.
4. Implement and release the EventStore-owned admission capability with real
   durable-state, restart, multi-host, compaction, and leakage evidence.
5. Let Folders Story 12.6 consume the released or pinned capability and remain
   owner of the canonical OQ8 evidence manifest and generated C13 matrix.
6. Reconcile the two pre-existing Folders NFR traceability failures separately
   in the Folders repository.

This proposal does not authorize implementation, dependency updates, commits,
pushes, submodule mutation, or changes in the Folders repository.

## 2. Change Trigger And Evidence

### 2.1 Reported blocking conditions

- EventStore deletes expired idempotency records and permits processing to
  continue, so expired-key reuse can reach provider/domain execution.
- The new OQ8 design exists, while durable admission implementation is absent.
- The original full Folders contract-suite report was 281/284 passing: the OQ8
  governance failure plus two existing NFR traceability failures.

### 2.2 Confirmed EventStore defect

The current implementation confirms the reported execution path:

| Surface | Current behavior | Consequence |
| --- | --- | --- |
| `src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs` | `ClassifyAsync` stages removal through `TryRemoveStateAsync` when `ExpiresAt <= now` and returns `IdempotencyCheckOutcome.Expired`. | The replay record is deleted at the application expiry boundary. |
| `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` | The actor commits any staged idempotency mutation. It returns for duplicate/recoverable/migration and identity conflict outcomes, but not for `Expired`. | `Expired` falls through to pipeline loading and eventual domain-service invocation. |
| `tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyCheckerTests.cs` | The existing expiry test expects staged removal and `Expired`. | The unsafe behavior is pinned by a unit test rather than rejected. |
| Story 4.2 review ledger | The actor-level commit of a staged expired-record removal was explicitly deferred. | The defect is known residual work, not a newly introduced interpretation. |

The current aggregate-local identity tuple of message id, normalized causation
id, and command type also cannot detect same-tenant reuse of the same opaque key
against another aggregate or target. A local `Expired` return alone is therefore
necessary but insufficient.

### 2.3 Approved OQ8 authority

The adjacent Hexalith.Folders worktree contains:

- `docs/exit-criteria/oq8-idempotency-design.md`
  - status: `design-approved`
  - design version: `1.0.0`
  - approved on: `2026-07-19`
  - approved by: `Administrator`
  - open production questions: none
- `docs/exit-criteria/oq8-idempotency-evidence.yaml`
  - status: `design-approved`
  - release gate: `in-progress`
  - design SHA-256:
    `1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8`
  - all implementation-evidence lanes: `pending`

Both files are currently untracked in the Folders worktree. Their presence
removes the working-tree artifact-presence failure, but does not establish
committed release evidence or close OQ8. Folders retains ownership of making its
canonical design/evidence artifacts durable under its own repository authority.

### 2.4 Current contract-suite baseline

The following live command was run from the Folders repository:

```text
dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore --configuration Debug
```

Current result: **282 passed, 2 failed, 0 skipped, 284 total**.

The earlier OQ8 artifact-presence failure now passes. The remaining failures are
pre-existing Folders planning-governance failures:

1. `NfrTraceabilityConformanceTests.PrdAndEpicsNfrInventoriesAlignOneForOne`
   - expected 70 PRD NFR bullets;
   - found 73.
2. `NfrTraceabilityConformanceTests.TraceabilityTableHasSeventyRowsMatchingPrdHashes`
   - the NFR1 derived PRD hash is `1d924a03811d`;
   - the traceability row still contains `f33274b9da03`.

These failures are not EventStore implementation evidence and are not absorbed
into Story 4.8.

## 3. Impact Analysis

### 3.1 Epic impact

- **Epic 4 remains viable.** Event correctness and recovery is the correct
  parent for durable idempotency admission and expired-key precedence.
- **Story 4.2 remains done.** Reopening it would obscure shipped scope, its
  review record, and the fact that bounded replay retention intentionally
  created this residual safety gap.
- **New Story 4.8 is required.** It owns the EventStore platform prerequisite
  consumed by Folders Story 12.6.
- **Story 4.4 remains publication-recovery owner.** Admission states and fences
  integrate with stored-but-unpublished recovery; Story 4.8 does not replace or
  broaden publication sweeping.
- No existing epic must be cancelled, reordered, or made obsolete.

### 3.2 Planning artifact impact

| Artifact | Impact |
| --- | --- |
| `prd.md` | Amend FR27, NFR7, NFR16, and their traceability rows. Preserve stable IDs. |
| `architecture.md` | Add AD-25; refine AD-5; cross-reference AD-3, AD-10, AD-12; update flow, structural seed, and capability map. |
| `epics.md` | Add Story 4.8 with the approved acceptance contract. Do not change Story 4.2 status/history. |
| Canonical Phase 4 SPEC | Extend CAP-4, constraints, requirements traceability, and readiness gates. |
| `ux.md` | No change. The correction introduces no user-interface workflow. |
| `sprint-status.yaml` | After final proposal approval, add Story 4.8 as `backlog`; keep Epic 4 `in-progress`. |
| Implementation story | Create and validate a dedicated Story 4.8 artifact before moving it to `ready-for-dev`. |

### 3.3 Code and contract impact

Likely owning surfaces include EventStore contracts, client/gateway error
projection, server admission and actor state, actor registration/configuration,
key-provider integration, aggregate execution fencing, migration, and test
projects. Exact type names remain implementation-owned, but the approved
behavior and authority boundaries in this proposal are normative.

The existing optional domain-service pre-commit admission chain is not a durable
idempotency authority. It may participate only if the implementation preserves
the dedicated tenant/key actor, server-trusted descriptor, durable fence, and
fail-closed state machine. A scoped domain-service hook or caller extension
cannot substitute for those requirements.

### 3.4 Cross-repository impact

- EventStore owns durable admission, fencing, replay/tombstone mechanics,
  retention classes, migration, and platform evidence.
- Folders owns Contract Spine equivalence, its trusted descriptor adapter,
  generated SDK/C13 coverage, REST/CLI/MCP projection, canonical OQ8 evidence,
  and final cross-surface approval.
- Folders Story 12.6 must consume an approved EventStore source/package/runtime
  identity. It must not implement a parallel Folders DAPR ledger.
- Folders Story 3.10 remains blocked until the production expired-key and
  provider-no-touch evidence passes.

### 3.5 Scope classification

**Major.** The correction adds a cross-aggregate persistence authority, new
trusted contracts, security key lifecycle, durable actor state, fencing,
retention/tenant-deletion semantics, gateway errors, migration, cross-repository
consumption, and production evidence. It is not a local branch fix.

## 4. Options Considered

| Option | Viability | Effort | Risk | Decision |
| --- | --- | --- | --- | --- |
| Direct adjustment: add Story 4.8 and implement approved OQ8 design | Viable; preserves product contract and existing Epic 4 | High | High but bounded by approved design and evidence gates | **Selected** |
| Roll back bounded retention or keep full replay records forever | Not viable; conflicts with approved replay tiers, protected-intent minimization, and deletion/legal-hold design | Medium | High privacy, storage, and governance risk | Rejected |
| Patch `AggregateActor` to return on `Expired` without durable tenant/key admission | Incomplete; blocks one execution path but leaves cross-target reuse, caller-selected equivalence, fencing, compaction, and recovery unresolved | Low/Medium | Critical false-completion risk | Rejected |
| Reduce MVP or defer affected Folders mutations | Not viable; FR41/FR42/FR44 and Story 12.6 make expired-key safety a release blocker | Medium | High product and contract regression | Rejected |

## 5. Detailed Change Proposals

### 5.1 Add EventStore Story 4.8

**Before**

Story 4.2 is done and explicitly treats application-expired exact records as
removable misses. Epic 4 has no focused durable admission story.

**After**

Add:

#### Story 4.8: Durable Tenant-Scoped Idempotency Admission And Expired-Key Precedence

**Requirements covered:** FR27, NFR7, NFR16

As a platform operator,
I want every admitted mutation key to remain durably consumed after its replay
result expires,
so that retries, conflicts, crashes, concurrent hosts, and old-key reuse cannot
duplicate aggregate, domain, provider, repository, or other external effects.

**Acceptance criteria**

1. **Trusted admission.** A registered server-trusted adapter supplies the
   versioned canonical-intent descriptor and fixed retention class after
   authentication, current authorization, and canonical validation. Public
   extensions cannot select or override the descriptor, digest, partition,
   state, fence, expiry, or retention class.
2. **Tenant/key identity.** Admission is partitioned by managed tenant plus a
   protected digest of the opaque key, independent of aggregate identity.
   Same-tenant reuse against another operation, aggregate, target, delegated
   task scope, or behavior-affecting credential scope cannot bypass conflict
   detection.
3. **Protected key handling.** Versioned HMAC-SHA-256 digests and a
   domain-separated verification tag protect partition identity and detect
   collisions. Raw keys and protected intent never enter actor IDs, state,
   logs, traces, metrics, exceptions, errors, or evidence.
4. **Atomic state and fencing.** Reservation, descriptor comparison, monotonic
   fence issuance, and state transition are actor-serialized and durable.
   Exactly one current fence may cross the side-effect boundary; crash,
   concurrency, restart, or host failover never turns consumed state into fresh
   work.
5. **Replay, conflict, and expiry.** Live equivalent intent replays only after
   current authorization; live different intent conflicts. Expiry is inclusive
   (`now >= expiresAt`). Equivalent and different expired reuse return the same
   `idempotency_key_expired` outcome before aggregate state, domain service,
   provider, repository, path, content, audit, projection, or scheduling work.
6. **Separate retention.** Non-commit mutation replay results remain live for
   24 hours and commit results for seven calendar years. Expiry atomically
   compacts replay payload and live intent digest into the approved metadata-only
   consumed-key tombstone, retained for the managed-tenant lifetime plus the
   governed post-deletion period with legal-hold pause/resume semantics.
7. **Fail-closed recovery.** `reserved`, `pending`, `recoverable`, and
   `unknown_provider_outcome` never permit blind execution. Unavailable,
   corrupt, collision, unknown-version, and unsafe legacy state never becomes
   `Missing`; migration preserves consumed-key state atomically or remains fail
   closed.
8. **Production evidence.** Verification proves persisted end state, exact
   time boundaries, replay compaction, restart, multi-host concurrency,
   fencing, key rotation/collision handling, leakage constraints, and zero
   duplicate/downstream execution through the approved durable DAPR state path.

**Rationale**

A new story preserves Story 4.2's audit history while making the residual
correctness boundary explicit. Its acceptance criteria are indivisible: a green
local expiry branch without tenant/key serialization and production evidence
must not be accepted as OQ8 completion.

### 5.2 Strengthen the EventStore PRD

Stable IDs remain unchanged.

#### FR27 replacement

> Pipeline and idempotency correctness remediation must use exact command
> identity for resume; provide an EventStore-owned, tenant-scoped durable
> admission contract accepting only a trusted, versioned canonical-intent
> descriptor and fixed retention tier; reject live conflicting intent and
> return non-retryable `idempotency_key_expired` for any expired-key reuse
> before aggregate, domain, or external execution; separate replay-result
> retention from metadata-only consumed-key evidence; and never convert
> consumed, unavailable, corrupt, or unsafe legacy state into a fresh miss.
> Command status/archive identity, transient retryability, and
> tenant-before-state validation remain required.

#### NFR7 addition

> Command processing must prevent duplicate side effects across reservation,
> fencing, execution, recovery, expiry, compaction, restart, and concurrent
> hosts. A consumed key cannot become executable fresh work because its replay
> result expired or storage became unreadable.

#### NFR16 addition

> Durable-admission evidence must inspect persisted production-path state and
> prove restart survival, multi-host serialization, expiry boundaries,
> tombstone compaction, leakage constraints, and zero downstream execution for
> replay, conflict, expired, corrupt, and unsafe legacy outcomes.

#### PRD traceability

- FR27: Epic 4, including Story 4.8.
- NFR7: add Story 4.8.
- NFR16: add Story 4.8.
- UX traceability: unchanged.

### 5.3 Add architecture decision AD-25

Add:

#### AD-25 - Durable Idempotency Admission Precedes Mutation Execution [ADOPTED]

- **Binds:** FR27, NFR7, NFR16, Story 4.8.
- **Prevents:** aggregate-local key reuse, delete-on-expiry resurrection,
  caller-selected equivalence or retention, duplicate cross-target effects, and
  unreadable/corrupt state becoming permission to execute.
- **Rule:** EventStore owns a dedicated admission actor partitioned by managed
  tenant, digest-key version, and HMAC-SHA-256 digest of the opaque key. A
  domain-separated verification tag detects collisions. Raw keys and protected
  intent are never persisted or logged.

A registered server-trusted adapter supplies the versioned canonical-intent
descriptor and fixed retention tier after authentication, current authorization,
and canonical validation. Public command extensions cannot select or override
those values.

Admission serializes reservation, monotonically increasing fencing, descriptor
comparison, pending/recoverable/unknown/terminal transitions, replay, expiry,
and compaction. No aggregate, domain-service, provider, repository, or other
side-effect boundary may be crossed without the current fence.

Expiry is inclusive. Replay payloads and live intent digests compact atomically
into metadata-only consumed-key tombstones. Equivalent and different expired
reuse both return `idempotency_key_expired`; unavailable, corrupt,
unknown-version, collision, and unsafe legacy state fail closed and never become
a miss.

Production evidence uses the approved durable DAPR state component and proves
persisted state, multi-host serialization, restart survival, compaction, leakage
constraints, and zero duplicate execution.

Companion architecture edits:

- Refine AD-5: `AggregateActor` remains durable event-mutation coordinator after
  admission; the admission actor owns tenant/key serialization and fences.
- Cross-reference AD-3, AD-10, and AD-12.
- Add admission actor, trusted descriptor seam, digest-key provider, and
  platform-evidence lane to the structural seed and capability map.
- Update the command flow so gateway/trusted adapter and admission precede
  `AggregateActor` and domain invocation.
- Do not create a duplicate EventStore copy of the canonical Folders
  `oq8-idempotency-evidence.yaml`.

### 5.4 Update the canonical Phase 4 SPEC

- Extend CAP-4 intent and success with tenant/key admission, trusted semantic
  intent, fencing, terminal expired-key precedence, replay/tombstone separation,
  and production-path durable evidence.
- Add the Story 4.8 acceptance contract from section 5.1.
- Add ordering, actor ownership, fixed tiers, inclusive expiry, fail-closed
  legacy/corrupt handling, and evidence constraints.
- Update `requirements-traceability.md` for amended FR27, NFR7, and NFR16.
- Add Story 4.8 to `readiness-gates.md`.
- Add an OQ8 platform gate requiring approved EventStore platform evidence
  before Folders can mark `implementation_evidence.eventstore_platform`
  complete.
- Preserve Story 4.4 publication-recovery ownership.
- Update the success signal to preserve AD-1 through AD-25.

### 5.5 Implementation and evidence slicing

Story 4.8 uses these coordinated tasks:

1. **Trusted contract seam**
   - Add domain-neutral descriptor, fixed retention class, admission
     request/result, disposition, fencing, and stable failure contracts.
   - Validate registered adapters and descriptor versions server-side.
   - Do not derive authority from `CommandEnvelope.Extensions`.
2. **Protected admission identity**
   - Add tenant/key admission actor, actor registration/configuration,
     versioned HMAC key provider, verification tag, rotation lookup, and
     collision handling.
3. **Durable state machine**
   - Implement `reserved`, `pending`, `recoverable`,
     `unknown_provider_outcome`, `terminal`, and `expired`.
   - Persist fences, monotonic observed time, fixed retention class, replay
     result, and compacted tombstone state.
   - Implement governed tenant-deletion and legal-hold lifecycle handling.
4. **Execution integration**
   - Route durable admission before `AggregateActor` and domain invocation.
   - Require the current fence before crossing the side-effect boundary or
     finalizing.
   - Remove the current delete-and-fall-through behavior.
   - Preserve Story 4.4 publication recovery and exact-result fidelity.
5. **Gateway and compatibility**
   - Map `idempotency_key_expired` to HTTP 409, non-retryable,
     `refresh_state_then_submit_with_new_key`, and support-safe RFC 9457 detail.
   - Preserve distinct conflict, unavailable, corrupt, and unsafe-legacy
     outcomes.
   - Supply versioned fail-closed migration for existing aggregate-local
     records.
6. **Verification and evidence**
   - Cover state transitions, descriptor equivalence, retention, exact time
     boundaries, migration, redaction, and error mapping in focused tests.
   - Use the approved DAPR state component, shared state across multiple hosts,
     restart, concurrent writers, and persisted-state inspection in higher
     tiers.
   - Prove no aggregate, domain-service, or provider execution on replay,
     conflict, expiry, or unsafe state.
   - Produce a machine-readable EventStore platform evidence packet containing
     the OQ8 design digest, source/artifact identity, commands/counts, durable
     observations, leakage results, and approval metadata.

Unit, fake, source-text, or recreated-service tests are supporting evidence only
and cannot close the production gate.

### 5.6 Documentation, sprint tracking, and handoff

Update these EventStore documents in the implementation slice:

- `docs/concepts/command-lifecycle.md`
- `docs/concepts/architecture-overview.md`
- `docs/reference/command-api.md`
- `docs/guides/configuration-reference.md`

They must show authorization and canonical validation before admission,
admission before execution, terminal expired-key behavior, stable public error
mapping, key-provider/retention configuration, and separate replay-result versus
consumed-key lifetimes. Existing stale prose that places idempotency reads before
tenant validation must be corrected.

After final proposal approval:

- add `4-8-durable-tenant-scoped-idempotency-admission-and-expired-key-precedence: backlog`;
- keep `epic-4: in-progress`;
- keep Story 4.2 `done`;
- create and validate the dedicated Story 4.8 artifact before moving it to
  `ready-for-dev`.

## 6. Risks And Mitigations

| Risk | Mitigation |
| --- | --- |
| A local expiry-return patch is mistaken for OQ8 completion | Bind acceptance to AD-25, tenant/key actor, fencing, tombstones, and production evidence. |
| Existing consumers cannot supply the new trusted descriptor immediately | Use explicit registration/versioning and an approved compatibility/migration plan; never trust public extensions or silently downgrade to local idempotency. |
| Admission actor and aggregate actor create cyclic waits or poison retries | Keep orchestration unidirectional, persist reservation/fence before execution, and use bounded recovery/reconciliation states rather than nested blind execution. |
| HMAC rotation makes old keys unresolvable or leaks raw keys | Retain reader-key versions while governed records reference them, promote atomically when possible, use domain-separated verification tags, and prohibit raw keys in diagnostics. |
| Clock rollback resurrects a key | Persist `lastObservedAt` and evaluate effective time as the maximum of stored and host `TimeProvider` time. |
| Replay compaction deletes consumed-key knowledge | Atomically replace the replay result/live intent with the minimal tombstone; never delete first. |
| Cross-repository source/package drift invalidates Folders evidence | Bind Folders consumption to an approved EventStore SHA/package/runtime identity and record it in the platform evidence packet. |
| Unit/fake evidence overstates restart or concurrency safety | Require real durable DAPR state, multiple hosts, restart, concurrency, and persisted-state assertions. |
| The two Folders NFR failures are hidden inside OQ8 work | Keep them as a separate Folders governance lane with their exact test names and observed values. |

## 7. Validation And Evidence Plan

### 7.1 Focused EventStore validation

The implementation story must specify exact per-project commands after final
type/project placement is known. The minimum lanes are:

- EventStore contract tests for stable admission/error serialization;
- client/gateway tests for public problem projection and compatibility;
- server actor tests for every state transition, identity comparison, fence,
  time boundary, compaction, and migration outcome;
- aggregate tests proving expired/conflict/unsafe outcomes never invoke the
  domain service;
- integration/live-sidecar tests using persisted state;
- multi-host/restart/concurrency evidence against the approved DAPR component;
- redaction/leakage inspection across state, logs, traces, metrics, errors, and
  evidence artifacts.

### 7.2 EventStore platform evidence packet

The packet must record at least:

- OQ8 design version and SHA-256;
- EventStore source SHA and produced package/container identities;
- actor/state schema version and approved state-store component/profile;
- fixed retention classes and exact boundary observations;
- concurrency, restart, failover, compaction, rotation, collision, migration,
  unavailable/corrupt-state, and leakage outcomes;
- persisted before/after state observations;
- proof that negative paths performed no downstream execution;
- exact commands, pass/fail/skip counts, environment, date, and approvers.

Folders' canonical evidence manifest references this packet. EventStore does not
declare OQ8 closed independently.

### 7.3 Separate Folders validation

After consuming the approved EventStore capability, Folders runs:

- its generated mutation/read denominator and OQ8 matrix;
- REST, SDK, CLI, MCP, domain, provider-no-touch, restart, and persisted-state
  evidence;
- the full Contracts.Tests suite;
- the separately owned NFR traceability reconciliation.

The two existing NFR failures may be fixed in the same Folders delivery window,
but not represented as EventStore Story 4.8 acceptance evidence.

## 8. Execution Sequence

1. Approve this Sprint Change Proposal.
2. Apply the EventStore PRD, architecture, epics, SPEC, and sprint-status edits
   in their owning repository while preserving current user changes.
3. Create and validate the context-filled Story 4.8 artifact.
4. Implement Story 4.8 through the approved EventStore contracts and actor
   architecture.
5. Run adversarial code/security/verification review and all focused plus
   production evidence lanes.
6. Release or pin the approved EventStore source/package/runtime identity.
7. Consume it from Folders Story 12.6 without adding a parallel persistence
   authority.
8. Execute the generated Folders OQ8 matrix and production evidence lanes.
9. Update and approve canonical Folders OQ8 evidence.
10. Resume the blocked Folders Story 3.10 expiry/provider-no-touch acceptance
    gate.
11. Reconcile the two separate Folders NFR traceability failures and rerun the
    complete 284-test contract suite.

## 9. Handoff And Ownership

| Owner | Responsibility |
| --- | --- |
| Product/PM | Apply FR27/NFR7/NFR16, Epic 4, story, and sprint-tracking corrections. |
| Architecture | Apply AD-25 and canonical SPEC changes; ensure no aggregate-local substitute weakens the design. |
| EventStore development | Implement Story 4.8 and compatibility/migration behavior in the EventStore repository. |
| Security | Approve digest derivation, verification tag, rotation, collision, redaction, tombstone minimization, tenant deletion, and legal-hold behavior. |
| Test architecture | Approve the production-path concurrency/restart/persistence/leakage evidence and platform evidence packet. |
| Release owner | Bind EventStore source SHA to consumed package/container identities. |
| Folders Story 12.6 | Supply canonical descriptors, consume the released/pinned platform seam, propagate public errors, generate C13 coverage, and own canonical OQ8 evidence. |
| Folders planning governance | Reconcile the 73-vs-70 NFR inventory and stale NFR1 hash separately. |

## 10. Success Criteria

The correction is complete only when all of the following are true:

- EventStore no longer deletes an expired replay record and permits execution
  as fresh work.
- Same-tenant reuse of one opaque key cannot bypass admission by changing
  aggregate, operation, target, task scope, or behavior-affecting credentials.
- Only a registered trusted descriptor and fixed tier drive equivalence and
  retention.
- Exactly one current fence may cross each protected side-effect boundary.
- Live equivalent, live different, expired equivalent, and expired different
  outcomes match the approved design.
- Expired-equivalent and expired-different responses are indistinguishable and
  contain no protected prior-intent hint.
- Replay expiry compacts atomically to durable metadata-only consumed-key
  evidence; unavailable/corrupt/unsafe legacy state never becomes a miss.
- Restart, multiple hosts, concurrency, clock rollback, compaction races, key
  rotation/collision, and migration are proven through persisted production-path
  evidence.
- EventStore platform evidence is approved and referenced by the canonical
  Folders OQ8 manifest.
- The complete generated Folders mutation/read OQ8 matrix passes.
- Folders Story 3.10's expired-key/provider-no-touch gate passes.
- The separate Folders NFR traceability failures are reconciled and the full
  284-test contract suite is green.

## 11. Approval Record

Incremental review mode was used. Administrator approved all six detailed edit
proposals on 2026-07-20:

1. Add EventStore Story 4.8.
2. Strengthen FR27, NFR7, NFR16, and traceability.
3. Add AD-25 and companion architecture changes.
4. Update CAP-4, canonical SPEC companions, readiness gates, and Story 4.8
   acceptance boundaries.
5. Approve implementation/evidence slicing and cross-repository evidence
   ownership.
6. Approve sequencing, documentation, sprint tracking, and separate Folders NFR
   handling.

**Final Sprint Change Proposal approval:** approved by Administrator on
2026-07-20.

This approval authorizes the planning and sprint-tracking handoff recorded in
this proposal. Production implementation, dependency updates, release, Git
publication, and cross-repository mutation remain governed by their dedicated
story and repository workflows.

| Decision | Approver | Date | Notes |
| --- | --- | --- | --- |
| Approved | Administrator | 2026-07-20 | Proceed with Story 4.8 planning/tracking handoff; implementation remains separately governed. |
