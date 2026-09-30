---
title: 'Story 6.5d: Hold Lifecycle, Resume, and Legacy Admission'
type: 'feature'
created: '2026-09-30'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 2
baseline_commit: '6a2f25e39586692b54b655d3e6e8a5f6fa4d317e'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 6.5 integration invented hold, wait, resume, capacity, and legacy-admission mechanisms that still contain acceptance-breaking lifecycle, evidence, and verification gaps.

**Approach:** Turn the verified pass-1 groups and all 54 routed pass-2 findings into one reviewed 6.5d candidate that Story 6.5 can import without inventing another rule.

## Boundaries & Constraints

**Always:** Replace `[I-06]`, `[I-10]`, `[I-12]`, `[I-14]`–`[I-17]`, `[I-29]`–`[I-31]`, `[I-36]`, `[I-37]`, `[I-45]`, and `[I-46]`; own their codecs, known answers, purpose `2d`, quota charge/counter codecs, and every hold/wait exit. Preserve history and public identity. D-RESUME republishes the same committed events under the same MessageIds and never re-executes a command. Amend imported rules in place, especially C5's operation-namespace fence confirmed by E2-34. Every stored state needs bounded storage, erasure, activation, recovery, and an inventory-visible exit.

**Never:** Edit AD-13 or its `UNAPPROVED` receipt, the 6.5a–6.5c candidates/records, `story-6-5-*` history, runtime code, tests, or product docs. Do not reopen D-NFR12, D-CLOSE, D-RESUME, or D-SPLIT; authorize Story 6.6; self-approve; or claim provider behavior from local models.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Eligible resume | Retry-exhausted or drain-limit hold | Authenticated bounded window re-arms only unresolved publication | Stale evidence conflicts; unavailable evidence holds |
| Legacy resume | Reconciled status-6 execution | Restore the exact stored range and MessageIds without command execution | Missing or contradictory authority follows the approved policy below |
| Capacity wait | Multi-member pin batch exceeds a counter | Durable fair wait with deadlock-free reservation, discovery, and exact refund | Invalid arithmetic/evidence holds without partial admission |
| Held delivery or long stream | Delivery cannot progress, or full replay exceeds a bound | Indexed operator-visible state with a deterministic redrive or capability exit | No silent drop, truncation, or infinite invisible retry |

### Owner Decisions

- `FirstSendMembershipChangedHold` exits only through verified configuration restoration. A configuration or membership revision triggers re-evaluation; the broker supplies a fresh atomic zero-send proof, and the active configuration must satisfy C2's existing byte-identical `ContinueSamePin` conditions. A bounded versioned resolution may then continue the original pin. Manual action may request re-evaluation but cannot override incompatibility. There is no generic operator route mapping or abandonment path.
- A historical legacy status-6 execution without authoritative drain, dead-letter, status, or range evidence is a non-resumable incident. Resume fails closed with a stable evidence-unavailable reason; it never fabricates bytes or automatically re-executes the command. Slice 3 prevents recurrence by durably reading back a bounded resume capsule before legacy drain evidence is removed. The capsule binds tenant, domain, aggregate, tracking identity, exact range, ordered stored-event root and MessageIds, correlation, rejection classification, and cleanup source. Any privileged evidence-import facility is deferred to a separately approved migration story.

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md` -- expand the backlog record into the normative candidate and integration handoff.
- `_bmad-output/implementation-artifacts/story-6-5-review-pass-2-findings.md` -- source list for the 54 dispositions; only E2-34 arrived pre-verified.
- `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` at commit `288a6190` -- unapproved loop-1 source for owned rules, §8.1, §10, and §11 replacements.
- `_bmad-output/implementation-artifacts/spec-6-5c-publication-subscription-and-rollout.md` -- cite-only C1/C2/C5 contracts that 6.5d must amend in its handoff.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`, `src/Hexalith.EventStore.ApiServer/Controllers/ReplayController.cs`, and `src/Hexalith.EventStore.Operations/` -- inspect-only anchors for shipped drains, replay, dead-letter, actor-index, and telemetry behavior.
- `_bmad-output/implementation-artifacts/{sprint-status.yaml,deferred-work.md}` -- update only the 6.5d tracker row and its D-SPLIT ledger entry.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md` -- re-derive the candidate under review-loop-1 requirements; verify and disposition every routed finding; specify the owned mechanisms, exact codecs/keys/charges/exits/slices, independently recomputed known answers, mutation-killing verifier blocks, and complete integration handoff.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- leave D-SPLIT open during implementation and review; the parent closes it only after a review pass has no surviving defect.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- advance only `6-5d-hold-lifecycle-resume-and-legacy-admission-spec` through the workflow states.

**Acceptance Criteria:**
- Given the pass-1 groups and 54 routed pass-2 findings, when the candidate is audited, then every item has exactly one evidence-backed disposition and every owned rule has an explicit replacement/handoff target.
- Given any state the candidate creates, when its lifecycle is traced, then storage, ceiling/charge, erasure, activation, inventory predicate, re-evaluation owner, and deterministic exit are all defined.
- Given an eligible evidence-required or reconciled legacy resume, when it succeeds, then only the same committed events and MessageIds are re-armed and no command, accepted member, pin, or first response is recreated.
- Given a first-send membership hold, when compatible configuration is restored, then fresh zero-send and exact-byte proofs resolve it to `ContinueSamePin`; incompatible configuration cannot be overridden or abandoned as if publication completed.
- Given a legacy status-6 record, when authoritative evidence is absent, then resume fails closed as a non-resumable incident; new slice-3 failures retain a read-back resume capsule before cleanup.
- Given candidate verification, when one known answer, framing rule, bound, transition, queue invariant, or model assertion is mutated, then the owning verifier fails.
- Given Story 6.5 integration, when 6.5d is imported, then §8.1, §10.2, §11.5, §11.6, and amended imported rules require no newly invented mechanism and AD-13 remains `UNAPPROVED`.

## Implementation Notes

- Built the candidate as D1–D13 against `6a2f25e39586692b54b655d3e6e8a5f6fa4d317e`, preserving the loop-1 source at `288a6190` and all three child-candidate hashes.
- Replaced all 14 owned integration rules and amended imported C1/C2/C5/A8 behavior in the handoff. The central correction keeps C5's permanent fence for terminal closure and gives resume a separately authenticated, permanently fenced window namespace.
- Added 30 exact record/codec answers, six framed-key answers, a 30-byte-mutation codec verifier, four named executable matrix scenarios, 14 status branches, 27 explicit defect mutations, and uniqueness checks over all 54 routed pass-2 findings.
- Re-derived exact 943-route and 59-member ceilings; removed both resume hash cycles; added a bounded live-retry cache, exact duplicate-free member partition, checked overflow outcomes, classification-preserving legacy recovery, precharged queue slots, queue-corruption recovery, durable delivery observation, resolvable retained-object locators, and the C4 captured-copy amendment.
- Closed the final acceptance-matrix audit gaps: D1 now reuses D7's admitted-plan capacity subject; both queue destinations are pre-reserved and exercised at a full destination; raw-byte queue corruption, all hard-bound transitions, the exact framed legacy root, and restart/capture/above-maximum delivery state are executable assertions.
- Left D-SPLIT open as required. The sprint row remains `in-progress`; final review-state and ledger transitions remain the parent workflow's responsibility.

## Spec Change Log

- 2026-09-30 implementation: expanded the 68-line backlog candidate into the reviewed section candidate; defined bounded activation, scope, quota, queue, resume, legacy capsule, held-delivery, quarantine, and inventory lifecycles; added the integration handoff and complete pass-1/pass-2 disposition registers. AD-13 and its receipt were not edited.
- 2026-09-30 review-loop-1 implementation: rebuilt the candidate from its backlog base and selectively restored the D1–D13 design with exact sizing, key framing, acyclic hash/write order, bounded retry evidence, non-overflowing charged waits, exhaustive resume/legacy validation, durable delivery capture authority, and one disposition for every loop-1 finding.
- 2026-09-30 acceptance-matrix audit: aligned the D1/D3/D12 wording, modeled both queue-slot reservations and full-destination moves, decoded corrupt queue bytes directly, exercised each hard bound with the incremental override, recomputed the D10 domain-separated row root, and asserted durable held-delivery state across restart, capture acknowledgement, and above-maximum incidents.
- 2026-09-30 review loop 1: VG-1 through VG-5, VG-O1, BH-2 through BH-18, and EC-1 through EC-17 plus EC-19 through EC-23 exposed non-frozen specification gaps in record sizing/framing, key and hash dependency graphs, retention and charge lifecycles, resume state transitions, held-delivery authority, and executable verification; BH-1 and EC-18 were smaller patches but are moot under re-derivation. The requirements below replace those incomplete instructions, keep D-SPLIT open until review succeeds, and avoid unaddressable records, hash cycles, capacity overflow, silently omitted members, classification drift, and false verification claims. KEEP the D1–D13 organization, permanent C5 terminal fence plus window-scoped resume, fail-closed source-less legacy policy, quota-ledger two-phase reservation, immutable accepted members/pins/first response, 30 codec-family coverage, four named matrix scenarios, all 54 historical dispositions, protected paths/hashes, and AD-13 `UNAPPROVED`.
- 2026-09-30 review loop 2: BHR2-01 through BHR2-18, VGR2-01 through VGR2-09 plus VGR2-O1, and ECR2-01 through ECR2-12 exposed a new resume-state/window hash cycle, non-idempotent retry identity and expiry, incomplete durable addressing/accounting, unrepresentable legacy capsules, incomplete held-delivery state, and verifier models disconnected from the normative transitions. The requirements below replace those incomplete instructions and avoid cyclic construction, duplicate resume windows, unauthenticated quota movement, stranded legacy ranges, cross-scope capture collisions, and executable tests that pass without exercising the claimed rule. KEEP the D1–D13 organization; exact 943-route and 59-member ceilings; framed keys already proven; acyclic closure/history and audit/prior-state dependencies; stable admitted-plan capacity subject; closed owner/reason mappings; two-counterpart queue reservations and full-destination moves; exact duplicate-free resume union; legacy row-root and success/rejection preservation; durable capture/readback authority; the disjoint 193 MiB boundary; hard-bound no-partial-apply behavior; 30 record families, 54 historical dispositions, protected paths/hashes, and AD-13 `UNAPPROVED`.

### Review loop 1 implementation requirements

- Keep the prior candidate only as preservation evidence. Rebuild the unsuffixed candidate from the baseline backlog record and restore only content consistent with these requirements; do not modify the frozen execution block, child candidates/history, runtime/tests/product docs, or AD-13.
- Bound every variable-size record by construction. The full-replay activation record and pin-batch reservation must state exact row/member ceilings derived from their byte caps and imported maximum identifier/member widths, and must define deterministic segmentation or a fail-closed readiness/admission outcome for every otherwise-valid larger inventory/batch. Quarantine header manifests must cover the full imported header maximum without exceeding their record cap.
- Use collision-free framed key derivations for every create-once record, including drain-limit, legacy capsule, and recovery records. Define resolvable retained-object identifiers and backend authority, not only their hashes. Invalid candidate evidence must still have a stable hold subject derived from immutable admitted plan evidence, or fail before commit if even that subject is unavailable.
- Make all hash dependencies acyclic and constructible. Window closure cannot contain its own successor accumulator; resume state and audit cannot hash each other. State the write/readback order, predecessor inputs, exact-retry lookup, and retained compact evidence that allows retries of older live requests after rolling bodies are reclaimed. Recompute semantic roots/accumulators in known answers instead of substituting unrelated label hashes.
- Reconcile scope retention into one rule: reject readiness when `H` cannot fit the configured bounded tombstone horizon; define identical expired-claim CAS replacement versus conflict; retain/delete scope records under one non-contradictory obligation/erasure policy. Checked ordinal/window/drain arithmetic must have closed overflow outcomes.
- Define the unchanged A8/[I-09] response-preparation-write authority by its exact imported name and dependency rather than inventing “D3 preparation evidence.” Define closed enums/mappings for held-delivery reasons, hold codes, owners, recovery states, and every transition out of `failed`.
- Charge wait rows and queue-directory capacity before commit, keep `queued + parked <= ceiling`, and never append a row to a full directory. Specify an external bounded parking/index mechanism or pre-reserved slot that remains discoverable, plus safe no-op behavior for empty and parked-only turns and a durable owner/exit for queue corruption.
- Specify resume as an exact, duplicate-free partition of committed members. Accepted plus unresolved must equal the committed set exactly; duplicate MessageIds/positions, omissions, overlaps, or unknown members hold before mutation. Drain-limit-only success must prove unchanged window/member set, checked limit increase, exact resolution linkage, and re-armed invocation. Membership and configuration revisions both exercise hold and success exits.
- Preserve legacy range identity and classification end to end: validate sequence, MessageId, StoredDigest, root, correlation, command type, and success/rejection classification; prove success and rejection resumes separately; define contradiction outcomes and the complete `claimed -> draining -> completed/failed` recovery graph without a stranded failed fence.
- Durably establish first-observed time and attempt count before relying on the 24-hour/attempt capture boundary. Make durable capture an explicit authenticated terminal handoff for the physical transport copy while logical route/effect obligations remain open, thereby amending the cited 6.5c acknowledgement rule without claiming route success. Inputs above the advertised quarantine maximum must create a bounded charged operator-visible incident before any repeated delivery can become invisible.
- Resolve the exact 193 MiB boundary without overlap. Define one maximum-inclusive ordinary path and a strictly greater oversize interval. Provider readiness must pre-reject what it cannot capture.
- Verification must execute, not merely describe: below/at/above every 75% full-replay threshold and hard bound; membership and configuration revisions; exact queue row/count/parked/trailing corruption; empty and parked-only queues; unknown account kinds; all D3 status branches including Completed, Rejected, not-applicable, class-02, class-03 and catch-all; duplicate/omitted resume members; drain-limit-only state transitions; legacy success/rejection/contradiction; ordinal/limit overflow; old exact retry; and each repaired hash/key/charge invariant. Mutation checks must fail when the corresponding rule is broken and must not bless ceiling-plus-one state.
- Leave the D-SPLIT ledger entry open during implementation and review. Preserve sprint `in-progress`; the parent workflow alone resolves the ledger and advances status after a clean review.

### Review loop 2 implementation requirements

- Break the D9 window/state mutual hash dependency. Define an independently constructible window intent/claim identity or predecessor reference, an acyclic write/readback order, and known answers that recompute the exact semantic dependency rather than inserting a label hash.
- Make resume requests retryable from caller-stable bytes: require a bounded caller idempotency key or signed request carrier, bind it into the claim, and index live rows/orphan audits by that stable identity. Retain a bounded expiry tombstone long enough to distinguish an expired exact retry from a new request after compact rows and audit bodies are reclaimed; changed bytes must conflict and an exact retry must create no ordinal, window, charge, audit, or invocation.
- Define the resume charge swap as an explicit staged reservation/ownership protocol. The old active charge remains authoritative until successor readback, the new worst-case reservation cannot exceed counters, and every crash point deterministically completes or rolls back without a double release or transient ceiling bypass.
- Authenticate every counter changed by a D7 pin-batch transaction, including the tenant-pool predecessor. Restore `scopeRetentionCeiling >= 64 MiB` to capability readiness. Give every replacement-owned durable record, head, pointer, counter, wait, policy, quarantine, index, and directory a collision-free framed address and revision/predecessor or create-once rule, with key vectors for representative families.
- Define a durable globally ordered D8 ticket allocator with exact codec/key, monotonic CAS, recovery, and a closed u64-exhaustion admission outcome. State the canonical queue sort tuple and byte/numeric comparison rules. Reject duplicate stable subjects before reserving or materializing a row; validate the closed account kind on refund as well as reservation.
- Make D10 capsules represent every supported legacy V1 range, including 1,000 maximum-width MessageIds, with bounded indexed chunks/manifest or an equally deterministic lossless design. A second publication exhaustion reuses the immutable capsule and advances recovery/resume generations; it never attempts changed bytes at the existing create-once capsule key. Verify every allowed and forbidden recovery-state transition, typed failure exit, owner, repaired-evidence condition, and new ordinal.
- Strengthen D11 identity and accounting: bind held-delivery addresses to scope, tenant/deployment identity, component, topic, physical subscription, and carrier hash; enforce `tenant` iff tenant is present; charge ordinary retained carrier bytes before physical-copy acknowledgement; version/address subscription policy; and define redriving failure back to captured with incremented count, durable error evidence, and scheduled retry.
- Close D3 catch-all failure semantics so an unknown/contradictory failure class can never project retry exhaustion. Verification must exercise pairwise precedence between conflict/unavailable evidence, terminal, published, drain, and failure-class branches.
- Response-preparation verification must require both pre-existing immutable outputs and both generation-bound receipts. Missing output, missing receipt, or mismatch stays `response_preparation_hold`; no helper may synthesize an output.
- Model resume partitions as exact `(position, MessageId, bytes)` tuples and reject swapped or mismatched identities. Drain-limit-only tests must consume the actual transition result and prove unchanged window/roster, exact old-limit and successor linkage, checked new limit, and recorded invocation.
- Exercise checked transition failure for ordinal, window, closed-window count, drain limit, and every charge sum with byte-identical pre/post state. Exercise live-row and orphan-audit exact retries plus changed-bytes conflict through the transition itself.
- Decode the full persisted queue record, including reserved-slot count and capability ceiling, and reject combined ceiling overflow before movement. Add below/at/above ten-year retention readiness and slice activation checks, plus distinct post-activation stream growth transitions for each hard replay bound.
- Replace digest-only byte flips with decoder-level malformed-record checks for missing, duplicate, reordered, overflowing, and trailing fields across the owned codec families. Keep exact length/hash known answers as framing evidence, but do not describe digest inequality as decoder rejection.

## Review Triage Log

### 2026-09-30 — Review pass 1

| Finding | Verdict and verified evidence | Route |
| --- | --- | --- |
| VG-1 | medium — pre-verified: D5 permits membership or configuration revision, but the executable helper accepts only `configuration-revision`; neither membership success nor membership hold is exercised. | bad_spec |
| VG-2 | high — pre-verified: the legacy matrix supplies `is_rejection` but recovery ignores it, so the verifier cannot detect loss or inversion of Completed/Rejected classification. | bad_spec |
| VG-3 | high — pre-verified: drain-limit-only resume checks only re-armed bytes; it does not verify unchanged window/member state, checked limit growth, resolution linkage, or closure behavior. | bad_spec |
| VG-4 | medium — pre-verified: full replay tests only 100001 events; none of the below/at/above 75% count, readable-byte, or accounting-byte activation boundaries are executed. | bad_spec |
| VG-5 | high — pre-verified: the queue known answer has one valid row only; too few/extra rows, parked-count mismatch, truncation, and trailing data never reach a decoder assertion. | bad_spec |
| VG-O1 | high — `Queues.add` appends a fourth row when ceiling is three and merely labels it parked; the explicit mutant then accepts `ceiling + 1`, contradicting D8's maximum entry count. | bad_spec |
| BH-1 | medium — the D-SPLIT ledger entry says Story 6.5 may import the candidate while this execution remains `in-review` and review has found defects. | patch; moot under loopback |
| BH-2 | high — the 1 MiB activation record inventories every route but defines neither a route-count/identifier-derived capacity bound nor segmentation, so a valid large catalog can be unencodable. | bad_spec |
| BH-3 | high — the drain-limit key concatenates ScopeOpHash with unframed decimal window and limit values; pairs such as `(1,23)` and `(12,3)` collide. | bad_spec |
| BH-4 | medium — D3 names “D3 preparation evidence,” while the actual imported authority is A8/[I-09]; the wrong unnamed dependency leaves implementers without a precise recovery record reference. | bad_spec |
| BH-5 | high — D4.2 first authorizes hourly tombstone deletion after expiry/obligation closure, then says every tombstone is deleted only with whole-tenant erasure; both retention outcomes cannot govern. | bad_spec |
| BH-6 | high — a 64 KiB pin-batch reservation embeds every member while imported V1 batches allow 1,000 members; maximum-width valid rows exceed the record cap and have no split/failure rule. | bad_spec |
| BH-7 | high — D8 caps queued plus parked rows yet directs a full queue's next candidate to become parked in that same directory; the model demonstrates the overflow. | bad_spec |
| BH-8 | high — D1 requires charged storage for every durable wait, but D8 does not assign charges for its wait/directory records, including directories up to 64 MiB. | bad_spec |
| BH-9 | high — window-closure tag `0b` stores the successor accumulator while the successor hash includes the exact closure, creating an unconstructible self-reference. | bad_spec |
| BH-10 | high — D10 says capsule and recovery keys are stable/bound but never gives their exact framed derivations, so the actor and Operations owner need not address the same fence. | bad_spec |
| BH-11 | high — held/quarantine records retain only a provider-object key hash and authority hash; no resolvable object identifier/backend locator is defined for restart redrive. | bad_spec |
| BH-12 | high — D11 acknowledges the captured transport copy while route obligations remain open, but the imported 6.5c acknowledgement rule requires terminal route state and D13 does not explicitly amend it. | bad_spec |
| BH-13 | high — the quarantine record is capped at 16 KiB while its manifest can include imported header names totaling 16 KiB plus lengths, hashes, and record overhead. | bad_spec |
| BH-14 | high — the 24-hour/attempt capture boundary has no pre-capture durable first-observation or attempt record, so restart can reset both clocks and postpone capture indefinitely. | bad_spec |
| BH-15 | medium — `stable typed hold reason` and inventory codes are unrestricted `U` fields without a closed vocabulary or mapping, allowing producers and dashboards to diverge. | bad_spec |
| BH-16 | high — resume verifies only that unresolved is a non-overlapping subset; a committed member omitted from both unresolved and accepted disappears silently. | bad_spec |
| BH-17 | high — independently confirms VG-2 and also notes the helper does not recompute correlation, MessageIds, digests, root, and range even though D10 requires them. | bad_spec |
| BH-18 | medium — D12 claims all status branches and contract-defect mutations, but omits Rejected/not-applicable/class-03/catch-all cases and contains weak mutants, including ceiling-plus-one. | bad_spec |
| EC-1 | high — independently confirms BH-9's closure/successor self-hash cycle. | bad_spec |
| EC-2 | high — resume state stores the last audit hash while that audit stores the successor state hash, creating a second mutual hash cycle with no constructible write order. | bad_spec |
| EC-3 | medium — the closure fixture prefixes its member bytes with an inner row count that D9.2 never declares, so the published known answer is not derived from the stated codec. | bad_spec |
| EC-4 | medium — invalid candidate evidence can raise a capacity hold before a trustworthy candidate batch root exists, yet D1 requires that root in the stable subject and defines no fallback admitted-plan subject. | bad_spec |
| EC-5 | high — independently confirms VG-O1/BH-7: a full queue still appends a parked row beyond its declared ceiling. | bad_spec |
| EC-6 | medium — the ledger model treats every account kind other than `tenant` as capture instead of rejecting values outside the codec's closed set, so a mutation can charge the wrong counters. | bad_spec |
| EC-7 | medium — queue turns call `next` without an empty/parked-only path; those reachable re-evaluation states raise instead of preserving a discoverable wait. | bad_spec |
| EC-8 | medium — independently confirms VG-1's missing membership-revision executable path. | bad_spec |
| EC-9 | medium — the status model has only a `class02` flag and cannot demonstrate the separately required class-03 terminal-evidence branch. | bad_spec |
| EC-10 | medium — independently confirms VG-4's absent 75% activation-boundary coverage. | bad_spec |
| EC-11 | high — tombstone retention must be at least `H` and at most ten years, but no readiness outcome exists when configured legacy evidence retention makes `H` exceed ten years. | bad_spec |
| EC-12 | medium — D4 defines identical unexpired claims and changed claims but gives no deterministic CAS outcome for an identical claim observed after expiry. | bad_spec |
| EC-13 | high — independently confirms BH-5's tombstone deletion contradiction. | bad_spec |
| EC-14 | high — incrementing resume ordinal/window/drain limit near u64 maximum has no checked transition or closed error outcome; decoder bounds do not settle arithmetic overflow. | bad_spec |
| EC-15 | high — older closure/audit bodies may be deleted while an exact live retry promises the same response; only the latest audit hash remains, so an older request cannot be answered as promised. | bad_spec |
| EC-16 | high — legacy recovery declares a `failed` state but specifies no retry, supersession, incident, or erasure transition from it, allowing the shared fence to strand the range. | bad_spec |
| EC-17 | high — independently confirms BH-14's restart-reset capture horizon. | bad_spec |
| EC-18 | medium — ordinary retention is inclusive through 193 MiB and oversize quarantine starts at 193 MiB, leaving an exact-boundary invalid carrier with two paths. | patch; moot under loopback |
| EC-19 | high — above-maximum delivery is called an unacknowledged deployment incident but has no bounded charged record/index/owner, so repeated delivery can remain operationally invisible. | bad_spec |
| EC-20 | medium — `dict(committed)` collapses duplicate MessageIds and unresolved duplicates are admitted; even with global uniqueness, corrupted contradictory evidence should hold rather than silently selecting bytes. | bad_spec |
| EC-21 | high — known answers inject unrelated `H(label)` values for roots and predecessor/successor relations instead of recomputing their semantic dependencies, so they do not verify the candidate's hash graph. | bad_spec |
| EC-22 | medium — independently confirms BH-18's overclaim that every rule mutation is killed. | bad_spec |
| EC-23 | high — queue corruption says only “hold the queue”; it defines no durable hold record, owner, charge, re-evaluation trigger, or deterministic exit despite D1 requiring all five. | bad_spec |

Grouped routing: 45 `bad_spec` findings form 32 root-cause entries: membership-trigger verification (VG-1/EC-8); legacy classification verification (VG-2/BH-17); drain-only verification (VG-3); activation boundaries (VG-4/EC-10); queue codec corruption (VG-5); queue ceiling (VG-O1/BH-7/EC-5); activation sizing (BH-2); drain-key framing (BH-3); preparation authority naming (BH-4); tombstone retention/horizon (BH-5/EC-11/EC-13); pin-batch sizing (BH-6); wait charging (BH-8); closure self-reference (BH-9/EC-1); state/audit cycle (EC-2); D10 key addressability (BH-10); retained-object addressability (BH-11); transport acknowledgement (BH-12); quarantine manifest sizing (BH-13); durable capture horizon (BH-14/EC-17); hold vocabularies (BH-15); exact resume partition (BH-16/EC-20); status/mutation coverage (BH-18/EC-9/EC-22); closure/semantic fixtures (EC-3/EC-21); capacity-hold subject (EC-4); account-kind validation (EC-6); empty/parked queue handling (EC-7); expired claims (EC-12); resume arithmetic (EC-14); old exact retries (EC-15); recovery failure transition (EC-16); above-maximum incidents (EC-19); and queue-corruption lifecycle (EC-23). BH-1 and EC-18 are direct `patch` entries, but cascading review makes them moot because `bad_spec` requires full re-derivation. There are no `intent_gap`, `defer`, or rejected findings in this pass.

### 2026-09-30 — Review pass 2 (build loop 1)

| Finding | Verdict and verified evidence | Route |
| --- | --- | --- |
| BHR2-01 | high — D9's window embeds the resume-state hash while the state embeds the active window-claim hash; the fixture substitutes `H('window-2')`, so the specified pair is not constructible. | bad_spec |
| BHR2-02 | high — POST has no caller-stable idempotency key/carrier, while server-generated request and expiry timestamps enter the claim hash; a lost-success retry cannot locate the live row or orphan audit. | bad_spec |
| BHR2-03 | medium — after both compact retry row and audit body are reclaimed, no retained tombstone distinguishes an expired exact retry from a new request, despite the deterministic expired outcome. | bad_spec |
| BHR2-04 | high — the reservation advances tenant, tenant-pool, and deployment counters but authenticates predecessors for tenant and deployment only, leaving tenant-pool reconciliation unauthenticated. | bad_spec |
| BHR2-05 | high — D8 relies on a global first-hold ticket but defines no durable allocator, CAS, recovery, or u64-exhaustion path, so uniqueness and fairness are not implementable. | bad_spec |
| BHR2-06 | medium — queue corruption rejects wrong sort without defining the canonical sort tuple or comparisons, so implementations can disagree on the valid head. | bad_spec |
| BHR2-07 | high — imported `[I-29]` requires `scopeRetentionCeiling >= 64 MiB`, but D7's retained feasibility set omits it and could activate an unusable zero/small scope budget. | bad_spec |
| BHR2-08 | high — multiple replacement-owned durable records and heads have no framed storage address or revision rule; independent owners cannot deterministically locate/reconcile them. | bad_spec |
| BHR2-09 | high — the 128 KiB inline D10 capsule cannot encode a supported 1,000-row V1 range with maximum-width MessageIds, leaving valid legacy exhaustion permanently non-resumable. | bad_spec |
| BHR2-10 | high — held-delivery key material omits scope, tenant/deployment identity, component, and topic, so reused physical subscription IDs and identical carriers can cross-link authorization and lifecycle state. | bad_spec |
| BHR2-11 | high — D11 does not enforce tenant presence exactly for tenant scope and absence for deployment scope, leaving charge, authorization, and erasure ownership ambiguous. | bad_spec |
| BHR2-12 | high — D11 charges the 32 KiB state but does not charge an ordinary retained carrier object up to 193 MiB before acknowledging the transport copy. | bad_spec |
| BHR2-13 | medium — subscription policy has no deterministic key, predecessor/revision, or supersession rule, so readiness evidence can roll back or remain stale. | bad_spec |
| BHR2-14 | high — D3 row 9 and the helper map an available unknown failed set to `outcome_evidence_hold`, a reason defined for unavailable evidence, leaving contradictory failure-class handling incomplete. | bad_spec |
| BHR2-15 | high — `render_recovery` synthesizes a missing immutable output although D3 requires both outputs and receipts to pre-exist. | patch |
| BHR2-16 | medium — the resume model accepts position-only partitions and cannot exercise the promised position/MessageId disagreement rejection. | patch |
| BHR2-17 | medium — the codec verifier's byte flip proves only a different digest; it never decodes missing, duplicate, reordered, overflowing, or trailing fields as the candidate claims. | bad_spec |
| BHR2-18 | high — D9 both “replaces” the prior active charge before mutation and forbids releasing it until successor readback, without a staged overlap state or crash semantics. | bad_spec |
| VGR2-01 | high — pre-verified: the only preparation helper and assertion regenerate a missing output, directly blessing forbidden behavior. | patch |
| VGR2-02 | high — pre-verified: the 14 status cases do not combine competing evidence, so moving terminal/published/drain branches above conflict/unavailable evidence would still pass. | patch |
| VGR2-03 | high — pre-verified: drain-only state is fabricated in a separate dictionary rather than returned by `resume_publication`, so window/member/linkage/invocation defects survive. | bad_spec |
| VGR2-04 | high — pre-verified: overflow checks cover only drain-limit addition, not ordinal, window, closed-window count, charge sums, or unchanged failure state. | bad_spec |
| VGR2-05 | high — pre-verified: exact retry is a standalone dictionary lookup and never traverses the resume transition or proves absence of side effects. | bad_spec |
| VGR2-06 | medium — pre-verified: queue decoding has no reserved-slot count or ceiling input, so a persisted `entries + reservations` overflow is accepted. | patch |
| VGR2-07 | medium — pre-verified: no executable below/at/above-ten-year readiness case verifies `scope_retention_horizon_unsupported` or inactive slice 4. | patch |
| VGR2-08 | high — pre-verified: the legacy helper omits recovery state, owner, generation, typed failures, and repaired-evidence exits, so the closed graph is not exercised. | bad_spec |
| VGR2-09 | high — pre-verified: activation helpers do not model an already-active stream crossing a hard replay bound, so unsafe continued dispatch would pass. | patch |
| VGR2-O1 | high — independently verified: the D9 window/resume-state hashes form the same mutual cycle as BHR2-01 and the label hash masks it. | bad_spec |
| ECR2-01 | high — the pin-batch reservation is a durable cross-backend authority with no exact key, independently confirming part of BHR2-08. | bad_spec |
| ECR2-02 | high — a global ticket at u64 maximum has no closed next-admission outcome, independently confirming BHR2-05. | bad_spec |
| ECR2-03 | high — a lost successful response cannot be correlated by caller-stable bytes, independently confirming BHR2-02. | bad_spec |
| ECR2-04 | high — independently confirms the window/state mutual hash cycle and non-semantic fixture. | bad_spec |
| ECR2-05 | high — independently confirms that a maximum valid legacy range cannot fit the inline capsule and has no deterministic resumable representation. | bad_spec |
| ECR2-06 | high — second legacy publication exhaustion says to create another capsule/source although the create-once capsule key is unchanged, producing a collision instead of a next recovery generation. | bad_spec |
| ECR2-07 | high — D11 defines entry into `redriving` and terminal success but no transition after a failed redrive, allowing a charged entry to strand indefinitely. | bad_spec |
| ECR2-08 | high — the helper treats an unknown failure class with `at_max` as retry exhaustion, contrary to the normative all-definitive-class-01 condition. | patch |
| ECR2-09 | medium — the refund model aliases an unknown account kind to unidentified counters instead of enforcing the closed kind set, so corruption can debit the wrong pool. | patch |
| ECR2-10 | medium — queue insertion does not reject a stable subject already resident/reserved; duplicate scope rows can consume capacity and break single residence. | bad_spec |
| ECR2-11 | high — independently confirms that missing preparation output is regenerated by the verifier. | patch |
| ECR2-12 | medium — independently confirms that the resume verifier has no selected MessageId input and cannot test position/MessageId disagreement. | patch |

Grouped routing: 29 `bad_spec` findings form 21 root causes: window/state construction cycle (BHR2-01/VGR2-O1/ECR2-04); caller-stable retry identity, expiry evidence, and transition verification (BHR2-02/BHR2-03/VGR2-05/ECR2-03); counter authentication (BHR2-04); ticket allocation/exhaustion (BHR2-05/ECR2-02); canonical queue ordering (BHR2-06); retained scope-ceiling feasibility (BHR2-07); durable record addressing (BHR2-08/ECR2-01); legacy capsule capacity (BHR2-09/ECR2-05); held-delivery address identity (BHR2-10); held scope/tenant invariants (BHR2-11); ordinary retained-object charging (BHR2-12); subscription-policy lifecycle (BHR2-13); closed failed-set classification (BHR2-14); decoder-level codec verification (BHR2-17); staged charge ownership (BHR2-18); drain-only transition verification (VGR2-03); checked resume transition arithmetic (VGR2-04); legacy recovery graph verification (VGR2-08); repeat legacy exhaustion (ECR2-06); redrive failure lifecycle (ECR2-07); and duplicate wait admission (ECR2-10). The 11 `patch` findings form eight direct verifier/model corrections: preparation recovery (BHR2-15/VGR2-01/ECR2-11); position/MessageId partition (BHR2-16/ECR2-12); status precedence (VGR2-02); persisted queue reserved-slot ceiling (VGR2-06); retention-horizon readiness (VGR2-07); post-activation hard-bound growth (VGR2-09); unknown failed-class handling (ECR2-08); and refund account-kind validation (ECR2-09). Cascading `bad_spec` review makes every patch moot under full re-derivation. There are no `intent_gap`, `defer`, or rejected findings in this pass.

## Design Notes

Follow the established two-artifact pattern: this `-2` file records execution and review; the unsuffixed file becomes the `candidate`. Keep all provider/crash vectors explicitly future-facing.

The candidate deliberately fails closed for source-less historical status-6 records. A resume capsule is now created before future terminal drain cleanup; no local model is treated as proof that a DAPR state store or broker implements the required atomicity.

The pin-capacity protocol uses a quota-ledger batch reservation followed by reservation-bound pin installation because the pin and quota backends may differ. Waiting batches own no partial reservation, which removes the deadlock in the loop-1 one-pin-at-a-time design.

## Verification

**Commands:**
- Run every fenced `bash` verifier in the candidate verbatim -- expected: exact block/family/mutation counts, all fixed known answers, and every mutation rejected by its owning family.
- `python3 scripts/check-deferred-work.py` -- expected: exit 0 while D-SPLIT remains open; legacy advisory diagnostics are informational.
- `git diff --check` -- expected: no whitespace errors.
- Run the candidate's protected-path/hash integrity block -- expected: only 6.5d/bookkeeping paths changed and AD-13 still contains `ApprovalEvidence: UNAPPROVED`.

**Implementation evidence:**

- Candidate fenced blocks: `D12 codec verifier: 30 answers, 30 byte mutations rejected, 6 framed keys`; `D12 lifecycle verifier: 14 status cases, 4 matrix rows, 27 mutants, 54 dispositions passed`; protected-path verifier: `4 hashes, AD-13 UNAPPROVED, 3 allowed paths`.
- `python3 scripts/check-deferred-work.py` exited 0 and reported 337 pre-existing unclassified legacy advisories; the 6.5d D-SPLIT row remained `open`. `git diff --check` passed.
