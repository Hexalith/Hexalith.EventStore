---
title: 'Story 6.5c: Publication, Subscription, and Rollout Candidate'
type: 'feature'
created: '2026-09-27'
status: 'in-review'
baseline_commit: 'e29b44a2d185b01ddafe534672153308285b4444'
route: 'dispatch'
review_loop_iteration: 6
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 6.5 needs a reconciled publication, subscriber-effect and rollout contract; historical timestamp wording can produce divergent delivery digests.

**Approach:** Extend the 6.5c candidate with source-grounded contracts, vectors and `BH37-9` disposition for AD-13 integration.

## Boundaries & Constraints

**Always:** Preserve history, MessageId, offsets and public compatibility. Pin retries; require durable results for every addressed route before physical acknowledgement. Separate implemented behavior, proposals and unexecuted evidence.

**Never:** Edit runtime/tests, 6.5a/6.5b, historical notes, signed fixtures or the normative artifact/`UNAPPROVED` receipt; authorize 6.6; silently acknowledge poison or unavailable evidence. Epic 8 remains optional.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Retry | Same MessageId; restart or key rotation | Same pinned authenticated publication | Changed bytes conflict; missing trust holds |
| Multiple routes | One route succeeds, another fails | One effect per route; acknowledge complete set | Retry unfinished route without rerunning completed effect |
| Legacy handoff | Queued JSON or old marker | Source-verified binary pin and durable handoff | No inferred route completion |
| Poison | Malformed/oversized carrier or failed capture | Bounded capture and proven retention | Retry or proven quarantine; no false acknowledgement |
| Rollout/time | Mixed fleet; equal instants with different offsets | Capability-fenced admission; distinct timestamp bytes | Unsupported rollback holds; no UTC normalization |

</frozen-after-approval>

## Code Map

Artifact paths below are under `_bmad-output/implementation-artifacts/`:

- `spec-event-versioning-upcasting.md` §§4, 7–11: reuse codecs, pins, numeric bounds, membership/effect and rollout rules; §12 stays unapproved.
- `story-6-5-review-triage.md` BH37-9 and `story-6-5-design-notes.md` Loop-7/Loop-9: record supersession in the candidate, leaving historical text intact.
- `spec-6-5a-event-contract-writer-and-migration-evidence.md` A3–A8: save evidence, budgets, first-response pins and complete publication observations; resolve terminal public `PublishFailed` proof.
- `spec-6-5b-verified-read-replay-and-projection.md` B2/B6/B8 and its ten-item integration handoff: source/effective separation, capabilities, quotas and compatibility.

Runtime anchors, to inspect without editing:

- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` and `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`: range recovery preserves identity but rebuilds bytes.
- `src/Hexalith.EventStore.Client/Subscriptions/`: processor and DAPR marker store share completion across CLR handlers; acquisition does not persist a lease.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs`: DTO ingress acknowledges skipped/invalid outcomes.
- `src/Hexalith.EventStore.Operations/Endpoints/DeadLetterOperationsEndpointExtensions.cs` and `src/Hexalith.EventStore.Operations/Actors/DeadLetterDrainActor.cs`: bounded capture, retention and unsafe acknowledgement inventory; AD-31 readiness remains unproven.

## Tasks & Acceptance

All content tasks edit `_bmad-output/implementation-artifacts/spec-6-5c-publication-subscription-and-rollout.md`:

- [x] Inventory outbox, transport, subscribers, markers, effects, dead letters, deployment and operator evidence with source/test links.
- [x] Specify exact codecs/pins, membership/leases, route receipts, duplicate recovery, complete-route acknowledgement and legacy handoff. Resolve A8's public terminal `PublishFailed` mapping without treating retryable attempt failure as terminal or changing first-response pins.
- [x] Specify bounded capture/readback, typed holds, rollout/provider gates, key/pin retention, rollback, package compatibility and safe diagnostics. Cover all ten 6.5b handoff items and cancellation boundaries.
- [x] Accept `BH37-9` as a historical wording conflict: retain §4 `T` and §7 CloudEvent `O(T)`; explicitly supersede UTC-only wording. Add byte-level equal-instant/different-offset delivery-digest vectors without altering existing literals.
- [x] Add executable codec/state-model checks for each matrix row and future provider/crash vectors; map claims to evidence and independently review.

**Acceptance Criteria:**

- Given sources and BH37-9, when reviewed, then contracts have exact fields, bounds, outcomes and evidence anchors.
- Given retries, partial effects, poison or incompatible peers, when vectors run, then identity, route effects and acknowledgements have deterministic outcomes.
- Given 6.5a/6.5b integration, when handed off, then replacements/disposition are explicit while AD-13 remains unapproved and 6.6 unauthorized.

## Implementation Notes

2026-09-27: Wrote C1–C7 in the existing 6.5c candidate. Source inventory covers the current outbox, subscriber marker, raw ingress, capture and public status seams. The proposed contracts consume 6.5a A3–A8 and all ten 6.5b integration gates. `BH37-9` is accepted as a historical wording conflict; the candidate retains the original-offset `T`/`O(T)` codec and four byte-level digest vectors. Runtime and historical files were not edited.

Independent review found that the first C4 draft acknowledged legacy JSON after handoff storage, before every addressed route had completed. C3/C4 and local checks C04/C05 now require broker acceptance and all durable route results before physical acknowledgement; poison requires capture-backed route quarantine or proved physical nonadmissibility with full-byte retention. This correction leaves the approved frozen block intact.

Local verification: C01–C10 passed when extracted from the candidate and run with `python3`; six digest/acknowledgement guard mutations failed as intended. All 24 relative links resolve. The normative draft, historical triage/design notes, and 6.5a/6.5b candidate files match their baseline bytes. `git diff --cached --check` passed. Future production/provider and injected-crash vectors remain unexecuted; local models do not prove provider readiness. Two concurrently modified submodules are outside this story and were excluded from staging and checks.

Review-loop-1 implementation: restored the sound candidate and added C1 separate encoded/decoded/header limits, C2 accepted-route drain authority, C3 canonical `Filtered` receipt, C4 bounded exact-attempt records/side-record accounting/stable physical quarantine, C5 authenticated terminal source records, and the corresponding C01b/C03/C04/C04b/C05/C06/C08 model cases. The parent independently ran all embedded checks and killed eight guard mutations. All 24 relative links resolve, five protected artifacts match baseline bytes, `src/` and `tests/` have no story diff, and `git diff --cached --check` passed. Retry, multi-route, legacy, poison and rollout matrix rows each have a passing local check; C7 keeps production/provider/crash vectors unexecuted. The unrelated submodule changes remain excluded.

Review-loop-2 implementation: corrected C1 transport relation, C2 per-attempt receipt renewal and historical grant/provider mapping, C3 exclusive route decisions and accepted-revision filter input, C4 feasible streaming retention and addressed broker full-byte reference, and C5 ordered terminal fence, segmented evidence, full public projection and committed `PublishFailed` replay gate. C01–C10 plus C01b/C04b/C08b pass when extracted from the candidate; the parent killed nine independent guard mutations before the final C08b addition and independently reran all checks afterward. The 24 relative links resolve, the five protected artifacts and `src/`/`tests/` remain unchanged, and `git diff --cached --check` passes. Each frozen matrix row has a passing local check. C7 explicitly leaves production provider, broker and crash-injection evidence unexecuted; this documentation candidate does not establish activation readiness.

Review-loop-3 implementation: added the canonical purpose-14 catalog filter claim/input, fenced `EffectReserved` takeover with uncertain-effect reconciliation, discriminated AD-31/broker quarantine references, zero-attempt terminal member proof, exact segment framing and corrected C02/C04/C10 local models. C01–C10 plus C01b/C03b/C03c/C04b/C05b/C08b/C08c pass independently; six targeted guard mutations fail after strengthening two negative assertions. All 24 relative links resolve, five protected artifact hashes and `src/`/`tests/` remain at baseline, and the story diff passes `git diff --cached --check`. Each frozen matrix row has at least one passing local check; C7 keeps production/provider/crash evidence unexecuted.

Review-loop-4 implementation: restored the fourth candidate snapshot and added a unique broker operation key with authenticated parent send binding and cross-destination terminal fence; an acyclic, signed pre-Prepared retry policy and member plan; compact terminal rows that verify full 1,024-byte identifiers; a signed revision clock; signed physical quarantine source records; and append-only takeover history. C04/C03b/C03c/C06/C10 local checks now cover the recorded edge cases. C01–C10 plus C01b/C03b/C03c/C04b/C05b/C08b/C08c/C08d pass from the extracted Python block. Seven targeted digest, acknowledgement, owner, route-scope and identifier-bound mutations fail. All 24 relative links resolve, five protected artifacts match HEAD, and both `git diff --check` and `git diff --cached --check` pass. Runtime/tests were not edited; provider and crash-injection vectors in C7 remain unexecuted, so this candidate does not establish activation readiness.

Review-loop-5 implementation: restored the fifth candidate snapshot and resolved every routed pass-5 finding in C2–C5. Broker send identity and the parent fence now include authenticated tenant; C3 receipt sizing and preflight cover admitted identifiers; accepted-time filter proof survives ordinary expiry; unidentified poison uses a signed isolated capture scope and active retention obligation fence; zero-attempt terminality binds reason-specific pre-send and complete post-fence ledger evidence; the A8 no-op branch precedes policy creation; and takeover tag 02 has one exact framed hash. C02/C04/C06/C08c/C08d and focused C03d/C05/C08a local checks cover new send IDs, exact headers, two destinations, one-based positions, identifier bounds, receipt capacity, scope/retention and no-op. Embedded Python checks passed; six digest/acknowledgement/scope/fence/position/identifier mutations failed as intended. All 24 relative links resolve, and the candidate passes git diff --check. Provider, broker and injected-crash vectors remain unexecuted; this documentation candidate does not prove activation readiness.

## Spec Change Log

2026-09-27, review loop 1: Three parallel layers reviewed the 65,513-byte diff. Verification Gap found no gap; Blind Hunter and Edge Case Hunter raised the rows below. The parent checked each against C1–C8, the embedded models and draft §7 before routing. The first candidate is preserved at `/tmp/bmad-6-5c-before-review-1.md`; restore its sound content and rederive only the missing contracts. The approved frozen block stays byte-identical. The following are specification-level requirements; direct model corrections follow them.

- Define distinct encoded Binary/structured, decoded body/attestation, and total header byte/count limits before parsing/allocation. Account Base64 and JSON expansion, exact-byte poison capture, and provider bounds; over-limit results hold without false acknowledgement.
- Define one canonical, durable, keyed `Filtered` receipt with signed catalog predicate/route revision, exact source/pin/route identity, create-once CAS and readback. It authorizes no effect. Apply its proof to normal and legacy physical acknowledgements and both local models.
- Fence membership changes while accepted deliveries still owe old routes, or retain proven historical execution authority until their receipts complete. A leave, rename, provider change or lease rotation cannot strand an addressed route; migration needs an authenticated old-to-new route mapping and no second effect.
- Reconcile whitespace-varied legacy JSON retries with an immutable first handoff: bind every admitted exact attempt's bytes and core/header evidence to the same canonical source, route set and binary pin under bounded create-once attempt records. Conflicting variants hold; no route effect runs twice. Charge original side-record bytes **plus** headers/framing/protection output under an exact preallocation bound, not a 128 MiB body-only cap.
- Give physical quarantine a stable create-once key independent of broker attempt ID, exact full-byte/header and subscription proof, duplicate lookup and conflict rule. Define canonical authenticated source bytes and verification for C5 terminal queue/drain, no-future-acceptance and policy/exhaustion proofs, including coordinator/broker fence and immutable public status transition; hashes alone confer no authority.
- Make local checks reject unauthenticated empty route sets and empty eventful publication sets; exercise matching, conflicting and ambiguous prior binary pins, filtered proof absence, changed route configuration/nonce and acceptance race. Keep models small and state their provider limits.

KEEP: C1's 37-field delivery codec, original-offset `T`/`O(T)` and four fixed digest-only vectors (they do not claim an admissible signed event); C2's production configuration/V3 probe and acceptance-time broker fence; C3 exact per-route effect transaction, one global pin and corrected all-route physical acknowledgement for Binary **and** legacy JSON; capture-backed route quarantine and proven unidentified physical quarantine; C5 original first-response pin and conservative terminal hold; all ten B handoff gates, source/test inventory, BH37-9 accepted disposition, C01–C10 sound checks, 24 relative links and protected-file hashes. Preserve signed V17/V20/V22/V23 literal bytes, the `UNAPPROVED` AD-13 receipt, and the documentation-only footprint. Do not touch the concurrently modified submodules.

2026-09-27, review loop 2: Three parallel layers reviewed the 92,714-byte revision. Verification Gap found no gap; the 20 Blind Hunter/Edge Case claims are verified individually below. Preserve the revised candidate at `/tmp/bmad-6-5c-before-review-2.md`; rederive from the original backlog document using this work order. The approved frozen block stays byte-identical. Correct these specification-level defects before polishing the local models:

- Make raw-body, encrypted-output and side-record limits simultaneously feasible under worst-case incompressible protection. Define exact retained-byte accounting without double charging a buffer that is streamed and not retained, or lower the admissible body maximum, and make the positive/negative budget vectors physically possible. Address structured carriers larger than local AD-31 capture: an addressed route quarantine must bind an authenticated broker-owned exact-full-byte reference and proof, or admission must prevent that size from becoming an addressed poison delivery.
- Give each addressed route **one** terminal-decision key/CAS across `Completed`, `Filtered` and `Quarantined`. Define an empty-route physical-filter codec, scoped stable key, exact source/pin/revision/predicate fields and readback. Pin the filtered predicate input as well as its accepted-revision predicate so current evolution cannot change an accepted decision; a historical reconstruction may be used only with authenticated proof.
- Define the historical execution grant's canonical bytes, signing purpose/issuer, exact old route/pin/effect/configuration binding, validity and revocation. On provider/backend replacement, query or migrate uncertain old effects/receipts before any handler rerun; without provable reconciliation, hold the mapping and route.
- Specify the order/atomicity of terminal broker reject fence, producer disable, queue drain and final empty observation so no accept can race the proof. Frame the member/receipt list deterministically and give a size-safe overflow/segmentation rule for the maximum complete set. Pin every existing public status field, including timestamp and nullable metadata. Reconcile terminal `PublishFailed` with `ReplayController`'s currently replayable status: partial accepted sets must not be resubmitted with new MessageIds unless separately proved safe; define the safe gate/hold and preserve public compatibility.
- Make embedded checks faithful to these contracts: C01b canonical mode, encoded/decoded relation and per-header-pair bound; C02 immutable receipt conflict; C03 unique routes; C04 authenticated `Completed` receipt; C05 full physical scope; C08 closed outcome states and terminal/published conflict. Include focused positive and negative assertions. These direct model corrections are retained across the spec loopback.

Known-bad state avoided: impossible budget assertions, stranded addressed poison, two terminal outcomes for one route, unprovable historical execution/replacement, evolving filter decisions, a terminal status ahead of an accepting send, oversized/unframed terminal evidence, incomplete public projection, and replaying a partially published command as a fresh execution.

KEEP: All previously logged KEEP constraints still apply. In particular retain original-offset delivery bytes and digest-only vectors, one global MessageId pin, the accepted-revision broker fence, all-route Binary/legacy acknowledgements, exact per-route effect receipts, authenticated filter/quarantine/readback, first POST pin, signed terminal evidence, ten B handoff gates, source/test inventory, 24 working relative links, documentation-only footprint and `UNAPPROVED` AD-13 receipt. Preserve the two prior candidate snapshots in `/tmp`; do not touch unrelated submodules or protected artifacts.

2026-09-27, review loop 3: Three parallel layers reviewed the 126,503-byte revision. Verification Gap found no gap; the twelve findings below were verified individually. Preserve the third candidate at `/tmp/bmad-6-5c-before-review-3.md`; restore its sound content and correct these non-frozen specification defects:

- Terminal source records must encode a **zero-attempt** member canonically: make last-attempt fields absent with explicit `O` typing or a discriminated state, bind that member's exact signed nonadmissibility proof in the roster/policy record and final root, and verify it before public terminality. State attempt ordinals contiguous only through the actual final attempt, bounded by the signed maximum; a member may stop earlier after Accepted or proven terminal exhaustion.
- Give all three terminal segment kinds exact complete byte formats: domain separator, version/count, ordered scoped header fields, row framing, ordinal/length bounds and hash input. Keep deterministic segment partition and previously specified 64 KiB/descriptor/operation reservations; no writer-specific serialization choice should alter a proof hash.
- Define the signed catalog filter predicate claim as canonical bytes with distinct signing purpose/issuer/interval/revocation, accepted-revision scope, filter expression/language, exact input schema and deterministic evaluation rule. Bind the claim and input through `Filtered` and physical-filter receipts; unknown predicates or historical transform bytes hold rather than filter.
- Discriminate C3 route-quarantine tag `08` as an authenticated AD-31 capture key **or** broker-owned full-byte reference with exact tagged encoding and authority-specific readback/retention verifier. A hash or untagged string never grants ack.
- Add owner/fence/lease, bounded expiry and takeover/recovery rules for `EffectReserved`. A crash after reservation must be resumable only after old-owner exclusion and exact uncertain-effect receipt query; no concurrent handler invocation or filter/quarantine transition may bypass the reservation.
- Correct local models: C04 must require independent no-prior-pin/send evidence before a new binary pin and permanent-poison proof before `Quarantined` ack; C10 must distinguish durable preparation before actor save and require readback/recovery; C02 must bind scope/destination/routing/header identity in the same-ID pin. Add positive and negative assertions for each, including an interrupted reservation and zero-attempt terminal member where practical. Keep model limitations explicit.

Known-bad state avoided: an unencodable terminal member, unbound nonadmissibility proof, ambiguous terminal segment hashes, diverging filter decisions, interchangeable poison-reference authorities, stranded or concurrent reserved effects, and local checks that pass unsafe pin/quarantine/cancellation paths.

KEEP: All prior KEEP instructions remain binding. Retain the feasible streaming body/capture budgets, accepted-revision historical grant and provider reconciliation, one exclusive route-decision CAS, signed accepted-revision filter input, physical filter/quarantine evidence, ordered broker reject fence, segmented member/attempt/policy evidence, complete public status projection, committed-`PublishFailed` replay gate, per-attempt broker receipt renewal, original-offset digest vectors, ten B handoff gates, all 24 working relative links, protected-file hashes and the `UNAPPROVED` AD-13 receipt. Keep this a documentation-only candidate; preserve all three `/tmp` snapshots and unrelated submodule changes.

2026-09-27, review loop 4: Three parallel layers reviewed the 158,263-byte revision. Verification Gap found no gap; the fifteen findings below received individual verdicts. The fourth candidate is preserved at `/tmp/bmad-6-5c-before-review-4.md`. Restore its sound content and amend these non-frozen contracts:

- Make the broker's `(domain,component,topic,member send OperationId)` a **unique** idempotency key and store canonical pin digest/mode bytes as immutable values under it; a different digest conflicts, not a second key. Bind each member send ID, including future retry nonce IDs, to the admitted parent command OperationId/ScopeOpHash through authenticated outbox/send-intent/broker records. The terminal reject fence must atomically reject the parent namespace on every accept path, even for a delayed or newly presented member ID; unbound IDs hold.
- Define the immutable signed retry policy in exact canonical bytes: issuer/purpose/key interval, command/destination scope, member count, per-member maximum 1..64, retryable/terminal reason rules, exhaustion and zero-attempt nonadmissibility. Bind it before A4 Prepared and authenticate it in terminal policy/status verification. A policy ID or hash alone grants no terminal failure.
- Keep A8's 1,024-byte identifier contract. Resolve C5's 512-byte terminal-row limit without dropping a valid committed member: compact rows may bind B32 hashes to exact authenticated A8 set/broker receipt IDs, or use a bounded hierarchical segment manifest with pre-A4 reservation. Independently verify full referenced identifier bytes at terminal readback; do not silently shrink the public/input cap.
- Give nonterminal status `Timestamp` an immutable authenticated revision-specific UTC source, key/codec and readback tied to the outcome/head without altering A8's imported twelve-tag outcome or four-tag head. Define retry-stable projection and missing-evidence hold.
- Define exact signed source records and authority checks for unidentified physical quarantine: stable physical delivery ID, complete byte/header hash, historical nonadmissibility/fence, immutable broker full-byte retention/reference and horizon. Bind them into C4's receipt and require byte-for-byte readback before 2xx; hashes alone are insufficient.
- Retain every `EffectReserved` takeover's prior owner/fence/ETag/reconciliation evidence in an append-only authenticated chain under the same route key, with exact predecessor and bounded capacity/recovery rules. A mutable latest proof hash cannot erase earlier exclusions; unavailable history holds.
- Bring local checks into line: C04 needs the signed empty-historical-route physical-filter branch and missing-proof hold. C03b rejects empty owner; C03c compares the claim to the exact accepted route scope; C06 duplicate Accepted must not recreate a closed obligation; C10 must reject commit/receipt proof when preparation/save flags claim no mutation. Keep C04/C05 proof Booleans explicitly as abstractions, as already recorded in the false-finding verdicts.

Known-bad state avoided: changed-pin double acceptance, a terminal fence bypass through unbound send IDs, unverifiable exhaustion, terminal proof overflow after a valid append, unstable status time, untrusted physical quarantine, discarded takeover history and local models that reopen or misclassify settled work.

KEEP: Every prior KEEP applies. Preserve exact original-offset delivery vectors, one global MessageId pin, per-attempt Rejected renewal, historical route grants/provider reconciliation, exclusive route decisions with fenced takeover, signed catalog filter input, tagged AD-31/broker quarantine authority, zero-attempt terminal proof, exact bounded segment framing, ordered terminal closure, committed-failure replay gate, all ten B handoff gates, 24 working links, protected-file hashes and AD-13 `UNAPPROVED`. Preserve all four `/tmp` candidate snapshots, documentation-only scope and unrelated submodule changes.

2026-09-27, review loop 5: Three parallel layers reviewed the 190,724-byte revision. Verification Gap found no gap; all fifteen new content/edge findings have individual verdicts below. Preserve the fifth candidate at `/tmp/bmad-6-5c-before-review-5.md` and restore its sound content. This is the last permitted spec loop; make the non-frozen contracts and local checks internally consistent before the next review:

- Include authenticated **tenant** in the broker's unique send-operation key, parent binding and terminal reject-fence scope, or prove global uniqueness across tenants with a canonical derivation. Equal execution/send IDs in different tenants must never collide or fence each other. Keep the digest as the key's immutable value, not another key component.
- Make C3 effect and route-decision receipt caps feasible for all admitted identifier lengths and provider descriptors. Define complete encoded per-field/record preflight before effect invocation (and capacity reservation before A4 Prepared for new work); choose compatible bounded records or compact references with authenticated full-byte readback. A valid admitted event must not commit an effect that can never be receipted.
- Distinguish signed catalog filter validity **at accepted membership revision** from later delivery after ordinary expiry. Retain the authenticated acceptance-time trust/interval proof; delayed completion uses that historical proof and exact retained predicate/input, while later key revocation or missing historical evidence holds. Do not reclassify an owed route from current settings.
- For unidentified poison, derive an authenticated isolated AD-31 storage/access scope from the broker's signed physical subscription/configuration record rather than an untrusted carrier tenant; bind it in the physical identity/capture proofs and prevent cross-tenant readback. Resolve finite retention against open obligations: no 2xx unless the exact full bytes are retained under a nonexpiring/fenced-until-closure obligation or a proven finite horizon extending beyond all retry/rollback/incident obligations; do not rely on an unguaranteed future renewal.
- Strengthen zero-attempt nonadmissibility: bind a pre-send broker/policy decision record with reason-specific immutable source/configuration evidence and proof that no attempt was accepted or queued before terminal fencing. The later reject fence alone cannot make a member historically ineligible; verify every referenced source at status readback.
- Define takeover tag `02` as one domain-separated hash of exact framed handoff and global pin bytes/identities with stable length and source rules, so providers reproduce the same chain record.
- Correct focused local models: C02 permits a newly parent-bound send ID only after an authenticated prior Rejected attempt while rejecting unrelated parents; C04 binds exact core/header bytes on every attempt and rejects changed-header/same-body retry; C06 tests one parent fence across **two** destinations; C08d requires one-based member positions; C08c rejects empty or >1,024-byte OperationId. Leave C08b as the documented segment arithmetic helper and C08c's opaque framing row as such, without claiming typed-row/provider coverage.
- Make the A8 zero-event no-op an explicit separate branch before retry-policy creation: its authenticated no-op witness has no publication members, policy, send IDs or terminal publication decision, and it preserves A8's `not-applicable` result. Do not force the 1..1,000 eventful policy on no-op.

Known-bad state avoided: cross-tenant broker collisions, effect commits without representable receipts, delayed routes held by ordinary claim expiry, untrusted poison storage scope or expiring retained bytes, a terminal zero-attempt assertion invented by the closure fence, divergent takeover hashes, model checks that miss authorized/forbidden retry identities and a no-op blocked by an eventful publication policy.

KEEP: All earlier KEEP constraints remain binding. Retain one global MessageId pin, tenant-safe broker uniqueness and parent-fenced sends, the acyclic signed retry plan/policy for eventful commands, full 1,024-byte identifier support with compact terminal references, immutable revision clocks, physical quarantine source proofs, append-only fenced takeover history, signed catalog input, zero-attempt terminal proof, exact bounded segment framing, original-offset vectors, ten B handoff gates, 24 relative links, protected-file hashes and the AD-13 `UNAPPROVED` receipt. Preserve all five `/tmp` snapshots and the documentation-only footprint; do not touch unrelated submodule changes.

## Review Triage Log

Review pass 1, 2026-09-27. Verdicts below precede grouping; `bad_spec` rows share this loopback, while direct `patch` rows are retained as local-check requirements for rederivation.

| Finding | Verdict, evidence, route |
| --- | --- |
| BH1 timestamp vector | **false / reject** — C1 explicitly uses the V17 body as a digest-only byte vector; it never claims that the modified body has a valid StoredDigest or purpose-02 signature. C7 leaves the admitted provider vector unexecuted. |
| BH2 transport size | **medium / bad_spec** — C1's one 128 MiB received-body limit cannot hold a 128 MiB decoded body in structured Base64 mode; absent separate encoded/decoded limits changes which mode can admit the pin without a specified outcome. |
| BH3 Filtered receipt | **medium / bad_spec** — C3 permits `Filtered` to satisfy physical ack but has no canonical durable proof, so subscriber and broker may disagree on a route's terminal disposition. |
| BH4 membership leave | **medium / bad_spec** — C2 invalidates old leases on revision change, while C3 still requires each old addressed route to complete; a leave before effect can strand the handoff indefinitely without a drain or historical authority rule. |
| BH5 JSON whitespace | **medium / bad_spec** — C4 allows equivalent JSON attempts yet keys one immutable manifest to the first exact JSON hash; a reserialized retry conflicts despite equal authenticated decoded content. |
| BH6 side-record bound | **medium / bad_spec** — C4's 128 MiB side-record cap must also retain core/headers and framing for a 128 MiB body, so the stated maximum has no feasible exact-byte representation. |
| BH7 quarantine key | **medium / bad_spec** — C4 says create-once/readback for unidentified poison without a stable key; a new broker attempt ID could create a second physical disposition. |
| BH8 terminal proof | **high / bad_spec** — C5 stores hashes for drain/future-acceptance/policy proof but no canonical authenticated source records or verifier; `PublishFailed` could become terminal without evidence that the broker cannot accept later. |
| BH9 prior-pin model | **medium / patch** — C4 allows a matching proved prior binary pin, but C04 always holds for `prior_binary=True`; the model misses a permitted retry branch. |
| BH10 broker model | **medium / patch** — C06 checks revision/expiry only, yet its acceptance claim includes configuration, probe, nonce and bytes; a same-revision config mutation passes this model. |
| EC1 header budget | **medium / bad_spec** — C1 bounds body and attestation but not arbitrary broker headers before exact-byte parsing/capture; a bounded body can still exhaust ingress memory. |
| EC2 empty routes | **medium / patch** — C03 and C04 accept an empty route set without the signed physical filter or quarantine proof that C3 requires. |
| EC3 prior-pin reuse | **medium / patch** — same C04 branch as BH9; the verified matching prior pin is rejected by the model despite C4's explicit reuse path. |
| EC4 Filtered model | **medium / bad_spec** — C04 accepts `Filtered` by name alone because C3 lacks the durable catalog-filter receipt in BH3; a physical JSON ack could precede authorized filter proof. |
| EC5 empty outcome | **medium / patch** — C08's `all([])` returns `published` although an eventful command with no expected publication members must hold under A8; no-op is separate. |

Review pass 2, 2026-09-27. Blind Hunter and Edge Case Hunter reviewed the 92,714-byte revised diff; Verification Gap reported none. Each new claim was checked independently against the candidate, local models and, for replay/public status, the current controller/response type before grouping.

| Finding | Verdict, evidence, route |
| --- | --- |
| BH2-1 side-record budget | **medium / bad_spec** — C4 charges raw plus full encrypted output under 193 MiB, but its C04b positive case assumes only 64 MiB encrypted output for a 128 MiB raw body; incompressible encryption cannot meet that assumption, so the advertised maximum may be impossible. |
| BH2-2 structured capture | **high / bad_spec** — C1 admits structured bodies up to 192 MiB, while C4 local capture stops at 128 MiB; route quarantine's tag `08` requires an AD-31 key and does not bind the broker-owned full-byte reference that C4 allows for physical quarantine, so an addressed over-cap poison carrier cannot close. |
| BH2-3 route terminal race | **high / bad_spec** — C3 gives `Completed`, `Filtered` and `Quarantined` separate receipt locations without one mutually exclusive route-decision CAS; concurrent valid effect and filter/quarantine decisions can both become durable for one route. |
| BH2-4 physical filter receipt | **medium / bad_spec** — C3 names a separately scoped physical filter receipt for an empty route set but specifies no codec, stable key or exact verification fields; independently implemented acks could accept different evidence. |
| BH2-5 historical grant | **medium / bad_spec** — C2's historical execution grant survives an expired/revoked current lease, yet no exact claim, issuer, interval, revocation or byte verifier is given; subscriber and broker can disagree on execution authority. |
| BH2-6 replacement provider | **high / bad_spec** — C2 preserves the old effect key across provider/backend mapping but does not require an old-provider uncertain-effect query or receipt migration before the replacement invokes the handler; one effect can execute twice. |
| BH2-7 filter input drift | **medium / bad_spec** — C3 fixes accepted-revision predicate bytes but its transcript uses the current effective view; a later registry evolution can change the input and therefore the filtered decision for one accepted delivery. |
| BH2-8 terminal fence order | **high / bad_spec** — C5 requires drain and reject-fence proofs but does not order their linearization; a broker accept between final empty observation and reject-fence install can escape the terminal proof. |
| BH2-9 terminal member framing | **medium / bad_spec** — C5's terminal drain record allows 1,000 members in an unframed receipt list within 64 KiB; valid receipt sets can exceed the cap with no defined overflow or deterministic encoding. |
| BH2-10 public status fields | **medium / bad_spec** — C5 pins selected `CommandStatusResponse` properties but leaves its required `Timestamp` and optional `TenantId`, `RejectionEventType`, `TimeoutDuration` unresolved; status projections can diverge despite the existing model's fields. |
| BH2-11 replay after partial publish | **high / bad_spec** — the live `ReplayController` includes `PublishFailed` in `_replayableStatuses` and resubmits the archived command with a new identity; C5 permits accepted members alongside terminal failure but gives no safe replay gate, so a replay can duplicate those members/effects. |
| BH2-12 carrier model relation | **medium / patch** — C01b checks independent numeric caps but accepts impossible decoded/encoded pairs and unknown mode names; its passing assertion overstates canonical transport admission. |
| EC2-1 header-pair model | **medium / patch** — C01b has total header bytes/count but no per-pair 8 KiB check; a single oversized pair passes its model despite C1's bound. |
| EC2-2 unknown-mode model | **medium / patch** — C01b treats every non-`Binary` mode as structured, so an unrecognized mode passes; same branch as BH2-12, retained as a separate finding. |
| EC2-3 stale Accepted model | **medium / patch** — C02 returns the earlier Accepted pin after `send(..., 'Rejected')` or another unrecognized receipt; C2's immutable authenticated receipt rule would require a conflict/hold. |
| EC2-4 duplicate routes model | **medium / patch** — C03 converts route names to `frozenset`, collapsing duplicate HandlerRouteIds; the required unique sorted route manifest would reject the malformed list before an ack. |
| EC2-5 unauthenticated Completed model | **medium / patch** — C04 `route_result(..., 'Completed')` accepts the name with no authenticated effect receipt; it can acknowledge despite C3's exact receipt/readback rule. |
| EC2-6 quarantine scope model | **medium / patch** — C05's physical key omits component/topic, unlike C4's specified four-part scope; two topics with one stable ID and subscription can collide in the model. |
| EC2-7 unexpected outcome state | **medium / patch** — C08 maps any state other than accepted/unknown/pending to private failed; an unrecognized state can become `PublishFailed` with proofs, unlike C5's closed five-state codec. |
| EC2-8 terminal/published conflict | **medium / patch** — C08 returns published for all-accepted states before checking a supplied terminal decision; C5 requires contradictory terminal evidence to hold as an incident. |

Review pass 3, 2026-09-27. The three layers reviewed the 126,503-byte diff; Verification Gap reported none. The eleven Blind Hunter findings and one Edge Case Hunter finding were checked separately against C1–C6 and C02/C04/C10 before grouping.

| Finding | Verdict, evidence, route |
| --- | --- |
| BH3-1 zero-attempt roster | **medium / bad_spec** — C5 permits a signed nonadmissibility member with zero attempts, but the mandatory terminal member row requires `U lastAttemptOperationId` and `B32 receiptHash`; no defined absent encoding can represent that legal member. |
| BH3-2 zero-attempt policy binding | **medium / bad_spec** — C5's policy row permits absent final receipt for zero-attempt nonadmissibility but contains no proof hash/reference; the signed reason cannot be tied to this member in the terminal root. |
| BH3-3 attempt ordinal wording | **medium / patch** — C5 says ordinals are contiguous `1..the signed member maximum`, which implies all 64 slots even after Accepted; C08b checks contiguity only through the actual last attempt. Clarify the contract wording to match that check. |
| BH3-4 exact segment bytes | **medium / bad_spec** — C5 gives fields and segment hashes but no exact ordered encoding for scoped headers and complete segment records, so two writers can hash different bytes for the same roster/attempt/policy rows. |
| BH3-5 filter claim | **high / bad_spec** — C3 relies on an exact signed catalog predicate claim, yet neither this candidate nor the referenced draft defines its codec/purpose/input schema or deterministic evaluation; a route can be filtered differently across consumers before ack. |
| BH3-6 quarantine reference | **high / bad_spec** — C3 tag `08` is one untagged `U` for AD-31 key or broker reference; a verifier cannot derive which authority/readback path applies from exact receipt bytes, risking unproved poison ack. |
| BH3-7 reservation recovery | **high / bad_spec** — C3's `EffectReserved` is durable before handler invocation, but has no owner/fence/takeover rule; a crash at that boundary can permanently strand a route or permit concurrent recovery attempts. |
| BH3-8 C04 no-prior-send proof | **medium / bad_spec** — C4 requires independent no-prior-binary-send proof, while C04 stages with `prior_binary=None` without it; this local model would pass a second-pin case that the acceptance contract forbids. |
| BH3-9 C04 poison guard | **medium / bad_spec** — C04 accepts a matching `Quarantined` tuple without proving permanent poison; a valid addressed event can be acknowledged as quarantined in the model despite C3's predicate. |
| BH3-10 C10 preparation | **medium / bad_spec** — C10 returns `cancelled-no-mutation` whenever save has not started, but C6 permits durable preparation before save and requires recovery; the model makes a false zero-mutation claim at that boundary. |
| BH3-11 C02 pin scope | **medium / bad_spec** — C02 compares body, attestation and mode bytes only, omitting C1's scope, destination configuration, routing intent and signed header identity; a same-MessageId altered send remains equal in this model. |
| EC3-1 zero-attempt roster | **medium / bad_spec** — independently, Edge Case confirms C5's mandatory last-attempt fields cannot encode a signed zero-attempt terminal member; same root cause as BH3-1, retained as its own verdict. |

Review pass 4, 2026-09-27. Three layers reviewed the 158,263-byte diff; Verification Gap reported none. The eleven Blind Hunter and four Edge Case findings were checked individually against C2–C5, the A8 identifier bound, C7's stated local-model limits and the cited Python checks before grouping.

| Finding | Verdict, evidence, route |
| --- | --- |
| BH4-1 broker idempotency key | **high / bad_spec** — C2 makes the digest part of the idempotency key while promising changed-pin conflict; the same operation with a new digest would occupy a different key unless the broker first enforces unique operation identity. |
| BH4-2 command/send fence binding | **high / bad_spec** — C5 fences the command OperationId, but C2 accepts member send OperationIds without an authenticated parent binding; delayed or invented member IDs can evade the terminal command fence. |
| BH4-3 retry policy bytes | **high / bad_spec** — C5 relies on an immutable signed retry policy and exhausted counts, but defines only its ID/version and no canonical bytes, signer or terminal decision algorithm; independent status readers cannot prove exhaustion. |
| BH4-4 terminal row capacity | **medium / bad_spec** — A8 permits 1,024-byte U identifiers; C5's 512-byte row cap may reject a valid committed publication member after append, with no pre-append compatible member-ID cap or alternate row representation. |
| BH4-5 status timestamp | **medium / bad_spec** — C5 projects nonterminal `Timestamp` from observation UTC, but the imported outcome/set/head codecs carry no such authenticated clock value; two readers can report different times for the same revision. |
| BH4-6 physical poison proofs | **high / bad_spec** — C4's physical quarantine receipt binds hashes for nonadmissibility, stable ID and retention but no exact signed source proof codec/issuer/readback; an untrusted proof hash could authorize 2xx. |
| BH4-7 takeover history | **medium / bad_spec** — C3 decision tag `0d` contains only the latest reconciliation hash; a second takeover can lose the first owner/fence evidence needed to exclude a late old commit. |
| BH4-8 C04 empty route | **medium / bad_spec** — C04 rejects every empty route set although C3 permits an authenticated physical-filter result for an empty legacy delivery; the model omits a valid branch and its missing-proof hold. |
| BH4-9 C04 boolean evidence | **false / reject** — C04's `complete` pointer and `broker_accepted` Boolean are explicit model stand-ins for C4's durable readback and Accepted receipt; C7 states these injected Booleans are not production evidence, and C4 itself requires side-record/route-record/receipt readback before ack. |
| BH4-10 C05 empty filter | **false / reject** — `capture_ack(...physical_filter=...)` models an authenticated identified physical out-of-scope filter, which C3 explicitly permits; C05's separate `PhysicalQuarantine` model handles unidentified poison and requires broker nonadmissibility. |
| BH4-11 C03 poison Boolean | **false / reject** — C03's `poison=True` and capture tuple are stated local proof stand-ins, not a source-validity oracle; C3 requires independently authenticated permanent source poison, and C04 separately rejects a quarantine result without that proof. |
| EC4-1 absent reservation owner | **medium / patch** — C03b `reserve(None)` leaves `owner` as `None`, allowing another reservation without advancing the fence despite C3's exclusive owner requirement. |
| EC4-2 wrong filter route | **medium / patch** — C03c checks only a truthy `scope`, so a signed claim for another accepted HandlerRouteId can filter this route in the model; C3's claim scope must match exactly. |
| EC4-3 accepted route re-owed | **medium / patch** — C06 `Broker.accept` adds an owed route even when returning a duplicate Accepted result after completion; membership change can be held again although C2's accepted-route obligation was closed. |
| EC4-4 false cancellation zero | **medium / patch** — C10 returns `cancelled-no-mutation` with `preparation_started=False`/`save_started=False` even if `commit_proven=True`; that inconsistent input needs a hold before the zero-mutation result. |

Review pass 5, 2026-09-27. Three layers reviewed the 190,724-byte diff; Verification Gap found none. The eleven Blind Hunter and four Edge Case claims were checked independently against C2–C5, A8's no-op/tenant/identifier rules and the local checks before grouping.

| Finding | Verdict, evidence, route |
| --- | --- |
| BH5-1 broker tenant scope | **high / bad_spec** — C2's unique broker key omits tenant, while A5 command identity is tenant-scoped and no globally unique send-ID derivation is specified; equal send IDs in different tenants can collide. |
| BH5-2 effect receipt size | **medium / bad_spec** — C3's 4 KiB effect/decision receipt caps have no compatible per-field preflight; multiple admitted 1,024-byte U identifiers can exceed 4 KiB after effect invocation. |
| BH5-3 delayed filter expiry | **medium / bad_spec** — C3 verifies filter validity at acceptance but later says authenticate its interval on receipt creation without distinguishing historical acceptance-time validity from current expiry; a delayed owed route could be stranded. |
| BH5-4 unidentified tenant scope | **medium / bad_spec** — C4 requires tenant-scoped AD-31 storage while unidentified poison has no authenticated tenant; the broker source records give physical subscription but no trusted capture/access scope mapping. |
| BH5-5 finite retention | **medium / bad_spec** — C4 signs finite retention-through UTC while retry/rollback/incident obligations can remain open; no renewal/readback fence prevents a previously acknowledged poison blob expiring too early. |
| BH5-6 zero-attempt evidence | **high / bad_spec** — C5's nonadmissibility record carries a reason and terminal reject-fence hash but no reason-specific pre-send evidence; a fence installed at terminal closure cannot prove the member was already ineligible before any attempt. |
| BH5-7 C02 new send ID | **medium / bad_spec** — C02 binds one member MessageId to one send OperationId forever, but C2's authenticated parent binding permits a later retry-nonce send ID; the model misses that permitted proved-rejection path. |
| BH5-8 C04 header variant | **medium / bad_spec** — C04's attempt model has no exact core/header argument, so it cannot conflict a changed-header, same-body redelivery as C4 requires. |
| BH5-9 C08b completeness | **false / reject** — C08b is explicitly an arithmetic/segmentation helper and C7 says local models do not prove complete provider records; C08 checks eventful nonempty outcome and C5 normatively verifies every member against the complete A8 set. The helper does not claim member-set completeness. |
| BH5-10 C08c typed row | **false / reject** — C08c tests exact segment framing/hash input with an opaque row; C7 expressly says local models do not parse typed terminal rows. The positive `b'row'` is not presented as a typed member-row acceptance test. |
| BH5-11 C06 destinations | **medium / patch** — C06 has only one destination-local parent-fence state; it does not exercise C2/C5's cross-destination rejection of a delayed send under the same parent. |
| EC5-1 no-op retry policy | **high / bad_spec** — C5 requires a pre-Prepared retry policy with 1..1,000 members, but A8 permits a zero-event no-op with no publication; the mandatory policy cannot encode that valid path. |
| EC5-2 compact row zero position | **medium / patch** — C08d compares row and policy positions but accepts both as zero; the signed member plan requires one-based positions. |
| EC5-3 segment OperationId bound | **medium / patch** — C08c hashes an empty or >1,024-byte OperationId without checking the imported U bound; the framing model accepts an inadmissible identity. |
| EC5-4 takeover tag02 | **medium / bad_spec** — C3 calls takeover tag `02` a handoff/pin hash but gives no exact input framing or derivation; independent providers can produce different record bytes and history hashes for one route. |

Review pass 6, 2026-09-28. The three layers reviewed the 220,962-byte diff. Verification Gap reported none. Blind Hunter's ten findings and Edge Case Hunter's three findings were checked separately against C1–C5 and the local models. Five specification-level roots remain after the fifth loop; the required loop increment reaches six, so the workflow stops here without changing the candidate.

| Finding | Verdict, evidence, route |
| --- | --- |
| BH6-1 send-parent signing purpose | **medium / bad_spec** — C2 requires a signed `HX-EV-SEND-PARENT-1` authorization but gives no distinct purpose number or pinned issuer for that claim. Draft §6's generic carrier cannot distinguish it from other broker authorizations. |
| BH6-2 broker header identity | **medium / bad_spec** — C1 pins and compares the six signed routing headers, while C2's broker immutable send value names mode/body/attestation rendering hashes without unambiguously framing those separately transported structured-mode headers. Two implementations may disagree on same-key header conflict. |
| BH6-3 per-attempt receipt lookup | **medium / bad_spec** — C2 allows repeated nonce attempts under one send OperationId, but `GetPublicationReceipt` is keyed only by operation and digest and does not identify the attempted nonce or define complete attempt-chain reconciliation. A later receipt can obscure an earlier uncertain attempt. |
| BH6-4 C02 old attempt result | **medium / patch** — C02's duplicate branch returns the operation's later Accepted pin when queried for the immutable first Rejected attempt; its assertion endorses that conflation. Return that attempt's Rejected result while exposing operation acceptance separately. Moot while bad-spec loopback is stopped. |
| BH6-5 AD-31-only physical source | **medium / bad_spec** — C4 permits tag `05` to name AD-31 capture, yet its mandatory purpose-16 identity and purpose-18 retention records require a broker full-byte reference ID. The candidate never states whether that ID is mandatory even when AD-31 holds the bytes or gives an AD-31-only encoding. |
| BH6-6 class-02 reason source | **false / reject** — C5 deliberately makes a signed broker `Rejected` receipt with a closed class-02 reason the definitive attempted-send outcome under the frozen policy; class-03 alone needs independent pre-send proof because no broker attempt exists. The proposed extra source is not required to establish class-02 terminality. |
| BH6-7 orphan revision clock | **medium / bad_spec** — C5 CAS-creates the immutable clock at the revision key before successor-head CAS. A competing or failed head CAS can leave a signed orphan clock that prevents a different valid observation from publishing that revision; only lost-acknowledgement same-clock retry is defined. |
| BH6-8 C04 current attempt ack | **medium / patch** — C04 `Handoff.ack()` checks the completed pointer/routes but takes no current attempt identity; a fresh redelivery can call it without its exact authenticated attempt record, unlike C4's per-attempt rule. Moot while bad-spec loopback is stopped. |
| BH6-9 C05 scope consistency | **medium / patch** — C05 keys quarantine by `(scope, component, topic, subscription, stable ID)` and explicitly accepts another signed scope for the same stable delivery. C4 requires one signed historical physical scope bound to the broker delivery log; the model misses that consistency check. Moot while bad-spec loopback is stopped. |
| BH6-10 C08d Accepted reason | **medium / patch** — C08d accepts an `Accepted` receipt with an arbitrary terminal reason, although C5 requires exact `Accepted` for that class. A direct model guard and negative vector suffice; moot while bad-spec loopback is stopped. |
| EC6-1 alternate physical scope | **medium / patch** — independently, C05 accepts a second signed isolation scope for the same physical stable ID. The C4 source claim derives one historical scope from immutable broker configuration; enforce that binding in the model. Same root as BH6-9, retained as its own verdict and moot under bad-spec stop. |
| EC6-2 oversized quarantine reference | **medium / patch** — C05b validates authority and proof hash but accepts a key over imported `U`'s 1,024-byte bound and can build a tag beyond the 16 KiB receipt cap. Add those two direct bounds and negative vectors; moot under bad-spec stop. |
| EC6-3 no-op contradiction | **medium / patch** — C08's no-op branch rejects states and terminal flag but ignores supplied proofs, active fence and sequence, returning clean `not-applicable` for contradictory publication evidence. C5 explicitly holds such evidence; add the direct guard. Moot under bad-spec stop. |

## Design Notes

No intent gaps or irreversible actions. Footprint: this record, the candidate and its `sprint-status.yaml` entry during implementation. Preserve backlog content until approval.

## Verification

- Run `git diff --check`; validate links and disposition/vector mappings.
- Run embedded checks with `python3`; mutation-check digest/acknowledgement assertions. Models do not prove provider behavior.
- Compare protected-file hashes and changed paths; inspect publisher, subscriber/marker, endpoint and capture tests. Future provider vectors remain unexecuted; runtime builds do not validate documentation.
