---
title: 'Story 6.5d: Hold Lifecycle, Resume, and Legacy Admission'
type: 'feature'
created: '2026-09-30'
status: 'done'
route: 'dispatch'
review_loop_iteration: 12
baseline_commit: '01498ac721db7c44f18fcf9591ffbbf30ba245e2'
initial_baseline_commit: '6a2f25e39586692b54b655d3e6e8a5f6fa4d317e'
external_workspace_checkpoint: '2c58ffda41759e895ace4b9625c9bd931a217672'
external_gitmodules_blob: 'c62b48798894bb3576f02fdf4ebb8c552756e35b'
external_submodule_pins:
  references/Hexalith.Builds: '21ce044ab465ccb2adab58b3d66e394ffbecf3c2'
  references/Hexalith.Commons: 'c13dc6679aa91144b6d541078f3f20019d79c2eb'
  references/Hexalith.FrontComposer: 'b6a4536fc12b64927ad6dfbc46a5f45c8b7f229e'
  references/Hexalith.McpCli: '7e3226ba612a3e7fb3a8969c4197a8f1e4c0c2ed'
  references/Hexalith.Platform: '7c2f0f89f29c79f5d7ab4b731155e2cb07fc690f'
  references/Hexalith.Tenants: '3d7c07363d7a06a24cbba0d777899b2071b02f06'
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

## Active simplification authorization

On 2026-10-03 the user asked why eleven iterations were needed and whether the design could be simplified. After reading `/tmp/bmad-6-5d-simplification-proposal.md`, the user instructed `do`. This authorizes the proposal's contract consolidation, supporting verification files, behavior-preserving replacement of obsolete internal structures/probes, focused review with zero findings permitted, and completion of this simplification. It replaces the additive pass-14/iteration-12 proposal and the historical non-frozen KEEP requirements that mandate every internal record or source-text mutation. Safety behavior, externally referenced wire identity and human intent remain authoritative. The counter remains 12 for history; use focused evidence to fix demonstrated defects without repeatedly asking for a bounded-cycle extension. Do not render the skill again.

The archived files `6-5d-simplification/previous-candidate.md` and `6-5d-simplification/previous-execution.md` preserve the exact prior candidate and complete review/change logs. They are immutable history, not competing normative contracts or requirements to retain removed internal mechanisms. Their SHA-256 values are respectively `fb0c7bec739df1752bc4bc47bd8aa223f74f709fbb6e4926d4be1992c8bc6954` and `a2556eea423ac546aca15e7405f2cf64012a544966fec80464de27e905f87386`.

## Code Map

- `spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md`: replace the active candidate with a compact normative contract under the design below.
- `6-5d-simplification/verify.py`, `known-answers.json`, `obligations.md`: allowed supporting specification verification, literal byte answers and behavior/disposition mappings. These are documentation artifacts; do not change runtime code or the repository test projects.
- `6-5d-simplification/previous-candidate.md`: prior D1–D11 (first 491 lines), D13 and original 54 pass-2 dispositions describe the behavior and external codecs to preserve. Read those normative sections and dispositions; do not carry the 8,810-line embedded model into the new design.
- `story-6-5-review-pass-2-findings.md`: source of the 54 routed findings and the verified E2-34 C5 contradiction. Preserve one mapped disposition per routed ID, including rejected findings with their evidence.
- `spec-6-5c-publication-subscription-and-rollout.md`: inspect C2/C5 to preserve terminal whole-operation fences, zero-send compatibility, stable events/MessageIds and signed publication evidence. Do not edit it.
- `../../.editorconfig`, `../../.gitattributes`, `../planning-artifacts/architecture.md`: repository formatting and AD-26 Dapr/PostgreSQL provider gate. The actual profile remains unapproved; local tests cannot supply production proof.
- `sprint-status.yaml`, `deferred-work.md`: the parent owns the 6.5d status row and D-SPLIT final state. Keep them in-progress/open until a focused review has no demonstrated blocker.

## Simplification design

### Ownership and transactions

1. Use one bounded mutable execution-owned control record for publication resume/legacy recovery, and one bounded mutable held-delivery control record for capture/redrive. Each contains the exact current request, phase, original UTC/expiry, next-action intent, relevant immutable source references and bounded retry/repair state. Revision/count updates share that owner's CAS. Define exact canonical JSON schemas for internal control bytes; existing externally signed binary claims continue to use their existing bytes. Do not create new origin, reconstruction, preparation-head, cleanup, repair-prerequisite or native-receipt codec families for local coordination. Immutable committed event bodies, signed claims/audits/window closures and provider-required evidence remain separate referenced authority where owner, signature or retention differs.
2. Target the repository's declared Dapr/PostgreSQL provider contract. Place execution/delivery controls, quota and registry in an application-owned platform metadata namespace with an explicit conditional same-backend transaction and fixed fenced participants. Keep aggregate/event state under IActorStateManager; never depend on direct SQL mutation of Dapr private actor-state keys or caches. Actor serialization alone never proves cross-actor or broker atomicity. Unsupported transaction/readback capability fails readiness before admission. Actual provider crash/integration proof is a clearly specified runtime gate, not a claim from this specification model.
3. Persist an exact bounded intended action before each external side effect. Recovery reads that external action by its existing deterministic address/identity and advances the same owner record. Broker producer-disable and reject are distinct irreversible effects, each with its own phase/reference. Once an irreversible fence or successful audit exists, finish the original request. Before any irreversible effect, a declined/cancelled preparation may release only after authenticated absence/deletion readback. No speculative rollback from hashes or process caches. Missing evidence is an indexed incident. Retain only current intent, not copies of all old/successor process graphs.
4. An exact retry returns its original result before current eligibility checks. Retain a bounded outcome set through the existing retry horizon; expire/delete deterministically and permit more than 64 lifetime successes. Failed or refused requests must not accumulate durable state or charges. Preserve drain-only resume's existing window and claim while increasing its checked drain limit. A new window admits only unresolved members of the immutable committed roster; never re-execute a command or resend accepted members.

### Held delivery, inventory and telemetry

5. One held record includes carrier locator/hash/length, original observation UTC/count, signed current request, attempt/count, retry deadline, repair state and bounded error evidence. Persist attempt and checked count together before send; lost acknowledgements read the committed record. Repair is an in-record state of this same delivery with the original carrier reason retained. Absent versus corrupt request/attempt is represented explicitly; no fabricated receipt or second independently charged repair hold is required. Unavailable evidence cannot authorize send or acknowledgement. Genuine nonterminal failures leave redriving for captured retry with bounded backoff (60 seconds to 15 minutes); terminal invalid/quarantined deliveries never redrive.
6. Retained carrier bytes have their own charged storage and authenticated readback; local state cannot make external object capture atomic. Capture uses explicit observed/capturing/captured phases in the held record and completes partial charge-only/object-written work on restart. Acknowledgement requires retained bytes, exact original identity, durable control and discovery. Authenticated deletion/readback precedes refund; scope erasure cannot imply successful delivery.
7. Reuse durable owner discovery where its concrete contract permits. A bounded registry entry must be installed/reserved before work could be committed or acknowledged; a crash between registry and owner creation leaves a discoverable placeholder that recovery either completes or safely releases. Define this order concretely without inventing cross-owner atomicity. The original stable registry subject discovers every phase, including repair/cleanup; Admin hold entries and metrics derive the current reason from the authoritative owner record. There is no separately authoritative entry hash that must be synchronized with a second hold index. Gateway, projection and capacity holds must use the same explicit discovery/re-evaluation rule.
8. Use one active Operations reconciler/aggregator epoch and bounded storage shards if required; do not distribute metric ownership across shards. Re-evaluate on cause change and at least hourly. Publication-capacity waits additionally retry every 60 seconds/refund. Admin paging uses a scope-authorized generation-bound cursor with an explicit stale-generation conflict/restart result; page size 1..200/default 50, oldest-first stable order. Observation count/time monotonicity is checked by the owner transition. Metrics are operational views and confer no lifecycle authority.

### Quota, admission and bounded delivery slices

9. Keep shared quota counters/reservations in a dedicated ledger with kind-qualified tenant/capture accounts, checked amounts, exact recorded overhead, preflight generation bounds and one demonstrable backend transaction. Pin installation on another backend remains a reservation-bound external phase; never claim cross-backend atomicity. Simplify capacity waits to one bounded deployment queue owned by that ledger, sorted by an immutable global ticket. Tenant-blocked or capability-parked waits remain in this same discoverable queue; select the oldest currently tenant-eligible wait and preserve deployment fairness. Waiting work owns no partial pin capacity. Eliminate moves between tenant/deployment queues, paired queue interests, duplicated owner indexes and 74 MiB predecessor images. Define exact maximum row count/bytes, precommit slot reservation, ticket exhaustion, checked rerender, corruption fail-closed behavior, grant/deletion/refund and erasure. Rebuilding corruption must use original immutable operation/ledger authority or stay incident; do not promise repair from a digest.
10. Retain all substantive replay/status/admission/membership/destination/legacy policies. Define below/at/above replay activation and hard limits, zero-event capability exit, status precedence for all 14 historical cases, every drain reason, scope tombstone HTTP 410 and deterministic retention, unavailable admission failure, explicit compatible zero-send restoration, bounded legacy capsule-before-cleanup with rejection classification and original range/MessageIds. These need small behavioral checks, not new coordination records.
11. Organize the normative handoff into implementation slices (resume/legacy, capture/redrive, quota/waits, inventory integration). The common discovery/charge prerequisites activate before any producer can strand work. All design decisions needed by Story 6.5 must be settled here; a later runtime slice proves the chosen provider behavior and does not invent policy. Preserve I-06, I-10, I-12, I-14–I-17, I-29–I-31, I-36/I-37, I-45/I-46 replacements, C5 in-place amendment and §8.1/§10.2/§11.5/§11.6 integration targets. No Story 6.6 implementation authorization.

## Verification and review discipline

- Aim for an active candidate under 600 lines/50 KiB and supporting executable verifier under 1,500 lines/70 KiB. These are design-pressure targets, not excuses to omit necessary policy. Keep history outside the active contract and explain any materially larger result.
- Extract verification into the allowed supporting artifact files. Keep literal independently reconstructed known answers for retained external wire families plus new internal-control framing/keys. Preserve signed purpose `2d`, public command/event identity and exact imported bytes. Retired private model-only families get an explicit obsolete/mapped row in obligations.md; do not pretend they were deployed formats needing migration. A wire contract actually consumed by 6.5c needs an exact compatible reference/definition, not an opaque promise.
- Test the four frozen matrix rows through actual simplified functions, serializable persisted-byte restart, pre-effect refusal with unchanged state, lost acknowledgement, each irreversible effect phase, monotone counts, bounded retries/lifetime reclamation, scope/cursor isolation, no-new-event replay exit and ledger/wait fairness/refund. Include deterministic fault injection only where it proves an outcome. No forced count of findings or source-text mutation tests mirroring implementation. Still demonstrate an owning failure for deliberately corrupted byte answer, framing, bound, transition, queue invariant and meaningful model assertion.
- Inspect canonical persisted bytes/state in tests, not only return values. Compare all refusal state/counters and use fresh deserialization for crash recovery. Use a small actual functions' scenario suite and independent byte/hash construction. Explicitly distinguish fixture authorization from production cryptography.
- The parent will independently verify evidence against all seven acceptance criteria, three tasks and four matrix rows, audit the archived file hashes and protected external state, then launch three fresh focused review layers together. Review input is the active candidate/supporting delta and relevant exact dependency context; archives and unrelated baseline changes are history/context, not new feature work. Permit zero findings. Fix demonstrated safety or acceptance failures; optional API/telemetry refinements do not restart full re-derivation.
- Do not invoke/render skills or launch another agent. Do not alter frozen intent, historical archives, 6.5a–c, runtime/tests/product docs, AD-13, dependencies/submodules, or Git history. Do not stage/commit/branch/push. Do not edit the parent's execution record or trackers.

## Tasks & Acceptance

- [x] Re-derive the compact normative candidate and complete integration handoff, with explicit ownership, bounds, erasure, activation, recovery and every hold exit.
- [x] Provide focused supporting verification, independent literal known answers and a complete mapping of all 54 routed findings and retained/retired internal test obligations.
- [x] Parent acceptance, three focused review layers, then only 6.5d tracker/D-SPLIT finalization after no demonstrated acceptance blocker remains.

**Acceptance Criteria:**
- Given the pass-1 groups and 54 routed pass-2 findings, when the candidate is audited, then every item has exactly one evidence-backed disposition and every owned rule has an explicit replacement/handoff target.
- Given any state the candidate creates, when its lifecycle is traced, then storage, ceiling/charge, erasure, activation, inventory predicate, re-evaluation owner, and deterministic exit are all defined.
- Given an eligible evidence-required or reconciled legacy resume, when it succeeds, then only the same committed events and MessageIds are re-armed and no command, accepted member, pin, or first response is recreated.
- Given a first-send membership hold, when compatible configuration is restored, then fresh zero-send and exact-byte proofs resolve it to `ContinueSamePin`; incompatible configuration cannot be overridden or abandoned as if publication completed.
- Given a legacy status-6 record, when authoritative evidence is absent, then resume fails closed as a non-resumable incident; new slice-3 failures retain a read-back resume capsule before cleanup.
- Given candidate verification, when one known answer, framing rule, bound, transition, queue invariant, or model assertion is mutated, then the owning verifier fails.
- Given Story 6.5 integration, when 6.5d is imported, then §8.1, §10.2, §11.5, §11.6, and amended imported rules require no newly invented mechanism and AD-13 remains `UNAPPROVED`.

## Implementation Notes

- 2026-10-03: approved simplification replaces the additive iteration-12 repair scope. Exact history preserved before implementation; frozen intent remains 3,569 bytes/SHA-256 `9234f13ebb6aef6634fff07a239be0b32c948d4fe9ddca2e49a6a77d8eec10d9`. Baseline/checkpoint remain pinned. Initial status in-progress, main tasks unchecked, sprint in-progress and D-SPLIT open.

## Spec Change Log

- 2026-10-03 user-authorized simplification: replace independently synchronized origin/reconstruction/progress/repair/index private coordination families with one owner state machine per execution/delivery and one ledger queue; move verification/history to supporting artifacts. KEEP externally required signed bytes, immutable evidence, same committed events/MessageIds, authorization, quota accounting, bounded crash recovery and four approved scenarios. Preserve old review/change logs in previous-execution.md rather than making obsolete mechanisms binding. Known bad state avoided: another additive loop whose new records create new recovery boundaries. User-approved review permits zero findings and focuses on owned changes.

- 2026-10-03 focused recovery correction: RB1/RV3 and RB2/RE1 exposed pending cleanup before drain deletion and pending restore after external write. Clarified D3 to consume the existing authenticated intent from either side of the effect and advance the same owner/ordinal once; added bounded wrong-owner/foreign-identity unchanged refusals for RV1/RV2. KEEP exact original capsule/range/MessageIds/classification, externally signed codecs, quota/discovery and fail-closed contradictions. Known bad states avoided: permanently stranded authentic cleanup/restore and a suite accepting removal of required owner fences. Parent checks and original-reviewer crash reproductions passed; no frozen intent or review-loop counter change.

## Review Triage Log

Prior fourteen review passes and their evidence are preserved in [previous-execution.md](6-5d-simplification/previous-execution.md). New focused findings will be verified against the simplified contract and logged individually here.


| Finding | Verdict / evidence | Route |
| --- | --- | --- |
| SB-1 | high: legacy_transition accepted caller ordinal 2 without any successful owner resume at ordinal 2; original scenario explicitly allowed this. D3 requires successful same-owner authority. | patch — resolved |
| SB-2 | medium: capture persisted capturing/intent before reserve_charge refused full tenant capacity, changing complete owner bytes before an effect. | patch — resolved |
| SB-3 | medium: queue_turn tested gross usage and reserved pins before the same-transaction 16,385-byte row refund; an exact-ceiling net grant was stranded. | patch — resolved |
| SE-1 | high: held_init installed owner before metadata reservation, and later capture/send checked only the object charge, permitting acknowledgment with missing metadata charge after refusal. | patch — resolved |
| SE-2 | high: redrive_reconcile trusted its default terminal=False without reading the original route source; unavailable terminal evidence permitted a new attempt. | patch — resolved |
| SE-3 | high: erase_held removed only delivery sources; authenticated route-terminal source survived owner/discovery deletion and all charge refunds. | patch — resolved |
| SE-4 | high: separately verified duplicate of SB-1, same legacy_transition predicate allowed greater unauthenticated resume ordinal. | patch — resolved; same root as SB-1 |
| SE-5 | high: capsule_make never read original drain authority and bound a constant source hash; caller events/classification fabricated a capsule on an empty backend. | patch — resolved |
| SE-6 | medium: generic U encoding/decoding enforced 1,024 bytes even for declared 4,096-byte quarantine objectKey and 1,031-byte tenant counter ID. | patch — resolved |
| SE-7 | high: result auditRecordHash hashed a short unrelated list, while the persisted audit action stored a different payload. Exact returned audit authority could not be resolved. | patch — resolved |
| SE-8 | medium: duplicate of SB-2; object-capacity refusal occurred after the capture owner CAS. | patch — resolved; same root as SB-2 |
| SE-9 | high: a legal execution revision MAX-2 admitted resume, committed disable, then exhausted revision before readback/advance; remaining owner/provider write headroom was not preflighted. | patch — resolved |
| SV-1 | medium: pre-verified gap; no terminal=True scenario exercised route-terminal guard. Removing that guard left main passing and permitted unsupported cleanup. | patch — resolved |
| SV-O1 | high: separately verified duplicate of SE-3; terminal source remained without owner, charges or discovery. | patch — resolved; same root as SE-3 |

All fourteen findings were adjudicated individually before grouping. Eleven correction groups remain (SB-1/SE-4, SB-2/SE-8 and SE-3/SV-O1 share their respective roots). No intent change or new coordination family is required; every correction enforces existing D1/D3/D6/D7 or exact wire limits. User-authorized simplification permits this single focused correction instead of full re-derivation. No new work is deferred.


### Recovery focused review — 2026-10-03

Three context-free layers reviewed the same [focused diff](6-5d-simplification/review-input.json), launched together and collected before triage. [Full findings and parent reproductions](6-5d-simplification/reviews/focused-review.md) retain individual evidence. These six claims differ from previously resolved findings; none is carried without verification.

| Finding | Verdict / evidence | Route |
| --- | --- | --- |
| RB1 | high: byte-only restart after cleanup intent and before original drain deletion retains authentic capsule/control/source; capsule_make repeatedly refuses drain-deletion-readback and remains cleanup. | bad_spec — focused owner repair authorized — resolved |
| RB2 | high: restart after exact live-drain restore and before owner advancement leaves cleanup/invoke/claimed; same-ordinal retries refuse legacy-live-drain, fresh resume refuses resume_capacity_hold. | bad_spec — focused owner repair authorized — resolved |
| RE1 | high: separately verified same reachable lost-ack restore boundary as RB2; current claimed owner cannot advance or complete. | bad_spec — same root as RB2 — resolved |
| RV1 | medium: pre-verified request-binding gap confirmed by parent; correctly signed foreign held identity refuses unchanged in original, sends with identity guard removed, and mutant full suite passes. | patch — bounded owner refusal added, resolved |
| RV2 | medium: pre-verified owner-fence gap confirmed by parent; wrong legacy owner refuses unchanged in original, claims with owner equality removed, and mutant full suite passes. | patch — bounded owner refusal added, resolved |
| RV3 | high: separately verified before-delete cleanup boundary as RB1; existing hook covers only after-delete and does not settle this reachable state. | bad_spec — same root as RB1 — resolved |

Four correction groups remained after individual verdicts; all are now resolved. The existing owners must finish authenticated pending actions before/after external writes; the two approved PD1 bounded guard cases must observe unchanged refusals. Existing simplification authorization permits focused correction instead of additive re-derivation; frozen intent, archived files and review_loop_iteration=12 remain unchanged. No new intent decision or deferral is required. Post-fix results and original-reviewer closure are retained in the linked review evidence.

### Review Findings — 2026-10-03 focused post-simplification pass (bmad-code-review)

**Scope and method:**
- **Input:** the story-owned 6.5d paths committed in `b51978dd`, as new-file diffs for the candidate, this record, `obligations.md`, `known-answers.json` and `verify.py`, plus the real `2c58ffda..b51978dd` diffs of `sprint-status.yaml` and `deferred-work.md` (3,228 lines, 253,485 bytes).
- **Excluded:**
  - The two immutable archives. Their SHA-256s match the hashes recorded above.
  - The ~40 identity runtime and test paths that the concurrent commit `b51978dd` bundled. They are not 6.5d work.
- **Reviewed SHA-256s:**
  - candidate `6f28b7263ef1f76d7a6d5fb65c9da5670ef32f0c3a3efb1f9ead62a381c82036`
  - `verify.py` `5d0b8341ef91e8999774fe0e9d5d81ec5c54c05e3f19a55b83d506314458f5bf`
  - `known-answers.json` `f4b058c58fb1988c240f57945ff597927a873a01a89169c2cf92448398df5dcb`
  - `obligations.md` `7fbd74bcba68365f354a4ddf6cb289529dddfbb815249db3ed7f27cf2b43188b`
- **Layers:** Blind Hunter, Edge Case Hunter, Verification Gap and Acceptance Auditor all ran and reported. No layer failed.
- **Baseline:** `verify.py` exits 0, and `git diff --check` is clean.
- **Verification:**
  - The layers raised 76 raw findings.
  - The three high-severity patch claims were reproduced against `verify.py` with the verifier's own functions: legacy resume with no capsule, legacy reclaim reusing ordinal 1, and the intent-binding wedge after producer-disable.
  - Four medium claims were also reproduced: queue head-of-line starvation after an overhead change, capsule chunks charged with no discovery, cleanup with no persisted intent, and a deployment-scope held `TypeError`.
  - Verification Gap findings arrive pre-verified.
- **Result:** 5 decision-needed (all resolved to the recommended option, tracked as PD1–PD5), 24 patch, 2 defer, 9 rejected.

**Decision-needed:**
- [x] [Review][Decision] FD1 — **Resolved 2026-10-03 by the owner: option (a) bounded behavior set; tracked as PD1.** AC6 coverage bar under the simplification (high).
  - **Evidence:**
    - The Verification Gap layer removed 50 guards one at a time; 47 removals still left `verify.py` exiting 0.
    - The Acceptance Auditor independently confirms the bound, transition and queue mutants, and the Edge Case Hunter lists 22 guards that can be deleted unnoticed.
    - The surviving behaviors include several the spec enumerates:
      - resume outputs: the new limit and successor window, the 64-outcome capacity refusal and the reclaim timing;
      - cancel after a crash that followed producer-disable;
      - queue fairness: no bypass of a deployment-blocked oldest row, and skipping of parked rows;
      - scope admission: changed-input conflict, capacity and refund;
      - adjacent status-precedence pairs;
      - redrive: the back-off gate, request binding and the 193 MiB boundary;
      - the cursor: continuation, expiry and signature;
      - legacy: the owner fence, `completed` being terminal, and the 1,000-event bound.
    - The Completion claim "framing/bound/transition/queue/model corruptions refused" overstates this coverage.
  - **Options:**
    - (a) Add owning behavioral cases for that bounded list and correct the claim; no per-guard mutation sweep.
    - (b) Literal AC6 for every guard, which risks re-entering the additive loop.
    - (c) Correct the claim only.
  - **Recommended:** (a).
- [x] [Review][Decision] FD2 — **Resolved 2026-10-03 by the owner: option (a) per-row registry entries; tracked as PD2.** Registry bounds contradict each other, and a full shard has no hold (medium). Candidate :124.
  - **The contradiction:**
    - 50,000 scopes × a 64 KiB reservation per row is at least 3 GiB, but a shard is capped at 1 GiB.
    - A 1 GiB single canonical value exceeds PostgreSQL's 1 GB field limit, and jsonb's roughly 255 MB limit if `payload` is jsonb.
    - Every registration rewrites the whole shard.
  - **What is missing:**
    - a registry key derivation;
    - a capacity hold or exit;
    - an account for the "scope operational-evidence quota", which is not one of D6's account kinds.
  - **Options:**
    - (a) Store addressed per-row registry entries (≤64 KiB, each with its own generation) under a per-shard count header, and define the registry key. Add a gateway-owned `RegistryCapacityHold` with readiness pre-rejection and deletion or capability-increase exits, and charge each row to its scope's tenant or deployment account.
    - (b) Keep single-value shards, but shrink them below the provider value limits and reduce the scope claim.
  - **Recommended:** (a).
- [x] [Review][Decision] FD3 — **Resolved 2026-10-03 by the owner: option (a) keep old charge until reclaim; tracked as PD3.** Superseded-window evidence lifecycle and old-charge release (medium). Candidate :52/:60, `verify.py:557-565`.
  - **The problem:**
    - Finalize releases the old window charge.
    - The old window claim, the sealed attempt set (up to 64 MiB), the drain-limit source and resolution, and the closure keep no exit except scope erasure.
    - Lifetime resumes are unbounded (more than 64 are allowed).
    - The model's `reclaim` deletes every action address, including window/disable/reject/successor. "67 lifetime resumes, one charge" therefore passes only because the model deletes what the contract retains.
  - **Options:**
    - (a) Keep the old charge until authenticated deletion and readback of exactly those artifacts at outcome reclamation (`deleteAfter`). C5 terminal proof uses the control's `history`/`closedCount` accumulator, and the model deletes exactly that set.
    - (b) Release the old charge at finalize, but move the old artifacts to a bounded closed-window retention charge.
    - (c) Retain them until scope erasure and cap lifetime resumes.
  - **Recommended:** (a).
- [x] [Review][Decision] FD4 — **Resolved 2026-10-03 by the owner: option (b) keep adapter, route AD amendment; tracked as PD4.** The metadata persistence path versus the architecture rule "over DAPR state" (medium). Candidate :20/:94/:128.
  - **The conflict:**
    - D1 introduces the application-owned PostgreSQL schema `hexalith_eventstore_control.rows` with SERIALIZABLE transactions.
    - It gives no DDL, migration ownership or credentials, and does not route the change to an AD amendment.
    - `owner_fence` is never defined: no minting or advance rule.
    - The epoch lease has no schema, address, duration, renewal, takeover, ceiling or erasure.
    - D6's "dedicated publicationRetentionBackend" contradicts D1's rule that a separately configured ledger backend fails readiness.
  - **Options:**
    - (a) The Dapr transactional state API is the only normative path: ETag-conditional multi-key transactions on the AD-26 statestore. Drop the direct table; `owner_fence` is the ETag; define the lease on the same store; `publicationRetentionBackend` names that store.
    - (b) Keep the direct PostgreSQL adapter, which is the approved simplification design item 2 ("application-owned platform metadata namespace"). Define the `owner_fence` and lease semantics here, and record the AD/AD-26 amendment (DDL, migrations, credentials) as a required Story 6.5/architecture handoff, with no AD edit in 6.5d.
  - **Recommended:** (b).
- [x] [Review][Decision] FD5 — **Resolved 2026-10-03 by the owner: option (b) bind cursor to registry generation; tracked as PD5.** Hold-inventory paging may never finish under churn (medium). Candidate :130.
  - **The problem:**
    - The cursor generation hashes every owner revision in the scope.
    - Redrive back-off (60 s–15 min), capacity turns and hourly reconciliation update owners continuously.
    - A multi-page scan of a large incident (10,000 subjects is 50 pages) can therefore keep returning `hold_inventory_generation_changed`, exactly when the inventory matters most.
  - **Options:**
    - (a) Keep the approved scope-wide generation and document the restart behavior.
    - (b) Bind the cursor to the registry generation only. Return the per-row owner revision and a stale flag, so order stays registry `firstUtc` and progress is guaranteed.
    - (c) A server-side snapshot read, which adds state.
  - **Recommended:** (b).

**Patch:**
- [x] [Review][Patch] PD1 (high, from FD1 option a) — Add owning behavioral cases for the bounded list in FD1, and correct the Completion and summary coverage claim. Do not add a per-guard mutation sweep. [`6-5d-simplification/verify.py`]
- [x] [Review][Patch] PD2 (medium, from FD2 option a) — Make registry entries addressed per-row entries (≤64 KiB, each with its own generation) under a per-shard count header. Define the registry key derivation and a gateway-owned `RegistryCapacityHold` with readiness pre-rejection and deletion or capability-increase exits. Charge each row to its scope's tenant or deployment account. Update the model and add cases. [Candidate D8 :124-126; `verify.py:300-324`]
- [x] [Review][Patch] PD3 (medium, from FD3 option a) — Keep the old window charge until authenticated deletion and readback of exactly the superseded window claim, attempt set, drain-limit source/resolution, closure and effect records at `deleteAfter`. C5 terminal proof uses the control's `history`/`closedCount` accumulator. Make `reclaim` delete exactly that set. [Candidate D3 :52/:60; `verify.py:534`, `verify.py:557-565`]
- [x] [Review][Patch] PD4 (medium, from FD4 option b) — Keep the application-owned metadata adapter. Define the `owner_fence` mint/advance/fencing semantics and the epoch lease record (address, schema, bound, duration, renewal, takeover, erasure). Make `publicationRetentionBackend` name the same declared backend. Record the AD/AD-26 amendment (DDL, migration ownership, credentials) as a required Story 6.5/architecture handoff in D9, without editing any AD. [Candidate D1 :20, D6 :94, D8 :128, D9]
- [x] [Review][Patch] PD5 (medium, from FD5 option b) — Bind the hold cursor generation to the registry generation only, and return each row's owner revision and stale flag. Order stays registry `firstUtc`; a registry change still returns `hold_inventory_generation_changed`. Update `inventory_page` and add continuation cases. [Candidate D8 :130; `verify.py:725-744`]
- [x] [Review][Patch] FP1 (high) — The legacy resume path is never bound to the capsule. [`verify.py:443-538`, `verify.py:813-826`; candidate :64-66]
  - `begin_resume` and `resume_step` never read or verify the capsule. Reproduced: a legacy execution with no capsule and no drain completes resume (ordinal 1, outcome and audit written).
  - The invoke re-arms roster positions `[2,3]` rather than the capsule's exact MessageIds and range.
  - `claimed→draining` neither calls `capsule_restore` nor checks for the absence of a live drain.
  - `failed→claimed` never checks that the capsule is unchanged.
  - This violates AC3/AC5 and matrix row 2.
- [x] [Review][Patch] FP2 (high) — `legacy_transition` ordinal, repair and failure authority; the SB-1/SE-4 fix is incomplete. [`verify.py:813-826`]
  - Non-`failed` edges store the caller's ordinal. Reproduced: draining/failed with ordinal 0 lets `failed→claimed` reuse ordinal 1 after one success.
  - `repair` is an unauthenticated boolean.
  - `failure` is always `transport-retryable`, so the evidence-failure branch is unreachable.
  - **Fix:**
    - Require `ordinal==legacy.ordinal` on non-failed edges.
    - Use the closed failure set `transport-retryable|evidence-unavailable|evidence-contradictory`.
    - An evidence reclaim reads back a repaired-range record bound to the capsule.
- [x] [Review][Patch] FP3 (high) — Resume recovery recomputes the intent payload from the mutable partition and wedges after an irreversible effect. [`verify.py:514`, `verify.py:522`]
  - Reproduced: the crash leaves the persisted `reject` intent; a member is accepted (D3 expects this); then `finish_resume` fails `intent-binding` every time and `cancel_resume` fails `irreversible`. There is no exit, against AC2 and D1.
  - **Fix:** the persisted intent (address, hash, payload) is the recovery authority; never recompute it.
- [x] [Review][Patch] FP4 (medium) — A legacy recovery that fails after a successful resume cannot re-arm. [`verify.py:529`, `verify.py:509-511`, `verify.py:824-825`; candidate :66]
  - The successor sets `source=ZERO` and idle clears `reason`, so a later `draining→failed` has no eligible source.
  - "A later exhaustion reuses the same capsule" has no transition.
  - **Fix:** `draining→failed` atomically reinstalls `source=capsule hash` and `reason=legacy-publish-failed` in the same CAS.
- [x] [Review][Patch] FP5 (medium) — `capsule_make` charges and writes chunks with no owner, discovery or intent. [`verify.py:763-792`, `verify.py:1038-1042`]
  - Reproduced: a refusal on a later chunk leaves the earlier chunks charged with zero registry rows.
  - The scenario also deletes the drain before any execution control or discovery exists, against D3 "control/discovery before cleanup" and D1.
  - **Fix:** discover and persist the legacy-recovery owner and its capsule intent before the first charge; delete the drain only after control and discovery read back.
- [x] [Review][Patch] FP6 (medium) — The control decoder does not enforce D2/D7 exact schemas or closed sets. [`verify.py:148-232`]
  - Accepted by probes:
    - an arbitrary `legacy` object;
    - a non-closed held `reason`;
    - a malformed or oversize `error`;
    - attempt `utc` of the wrong type;
    - an arbitrary intent `kind`;
    - a 100 KB execution `reason`;
    - a boolean registry `shard`, and a registry `owner` outside D8's owner kinds.
  - The rule "fields not used by a phase are null" is unchecked.
  - Add the validation, plus one refusal case per rule: roster/queue/registry order, request horizon, `deleteAfter`, held scope/tenant, retained locator, count/UTC/identity regressions, intent binding, and destination metadata count/total.
- [x] [Review][Patch] FP7 (medium) — Retained key families lost their literal known answers; the self-test is vacuous; `maxBytes` is unpinned. [`known-answers.json` keys; `verify.py:863-882`; `obligations.md`]
  - Only 9 key answers remain. About 20 retained families (capacity-subject … publication-invocation, command-execution-scope) had archive literals and no obsolete row.
  - The model uses improvised addresses (`action:`, `object:`, raw `scope-usage:` concatenation) instead.
  - The `:879-882` self-test never runs `verify_answers`.
  - Raising `maxBytes` still passes.
  - **Fix:** restore independently recomputed literals, use the retained derivations in the model, make the self-test run the real check on a mutated deep copy, and pin `maxBytes` to the obligations caps.
- [x] [Review][Patch] FP8 (medium) — Closed public and hold literals were compressed out of the active contract. [Candidate :62, :72, :116, :132-149]
  - The archive's closed hold→reason mapping is absent from the candidate. Missing literals: `legacy_array_limit`, `full_replay_inventory_capacity`, `scope_retention_capacity_hold`, `pin_capacity_queue_corruption_hold`, and `ResumeAttemptCollectionHold→resume_evidence_hold`.
  - The D8 table owner literals (`quota`, `aggregate`) differ from the closed set (`quota-coordinator`, `actor`).
  - These are named nowhere:
    - eligibility values;
    - the pending-identity fence reason;
    - `expiresAt`;
    - the redrive body, response and `entryKey` format;
    - D1 CAS-exhaustion, ceiling and u64-exhaustion holds;
    - D5's per-stream hold.
  - **Fix:** restore these into the candidate, with `RedriveEvidenceRepairHold` mapped as retired to the HeldDelivery repair view (AC2/AC7).
- [x] [Review][Patch] FP9 (medium) — `resumeHandle` is no longer discoverable. [Candidate :50, :130]
  - The archive's rule "every eligible hold inventory item exposes resumeHandle" was dropped. The inventory view returns no handle, and handle→execution resolution is undefined.
  - **Fix:** restore exposure, and state resolution by recomputation over the tenant's registered execution owners.
  - The missing domain tag and placeholder answer `hxrsm1-handle` are pre-existing (archive :239/:850).
- [x] [Review][Patch] FP10 (medium) — Capability bounds admit configurations where kind maxima can never fit. [Candidate :96, :100]
  - `quarantineMax` can be 256 MiB with `unidentifiedCeiling` 195 MiB, so a deployment-scope oversize capture never fits.
  - At minimum capability, the eight ≤100 MiB queue envelopes (account unspecified) leave about 410 MiB of deployment, under the 449 MiB maximum pin.
  - The row→queue-shard assignment is unspecified.
  - **Fix:** capability validation requires every kind maximum plus overhead to fit its account after fixed precharges; name the envelope account and the shard selector.
- [x] [Review][Patch] FP11 (medium) — Queue turn head-of-line starvation. [`verify.py:405-424`, `verify.py:373`, `verify.py:397-403`]
  - The fit check uses the stale materialized amount, while `batch` re-renders with the current overhead. Reproduced at the ceiling: every turn refuses `capacity` and the younger tenant's ticket 2 never proceeds.
  - Invalid materialized rows (60 pins, duplicate MessageIds, cleanup state) block the queue the same way.
  - **Fix:** re-render under owner CAS and park above-ceiling rows before the fit; validate in materialize; a chosen-row refusal parks or holds that row instead of aborting the turn.
- [x] [Review][Patch] FP12 (medium) — Transactions decide on a stale read but CAS against a fresh re-read. [`verify.py:471-475`, `verify.py:419-423`, `verify.py:598-599`]
  - Affected: the `begin_resume` request and `priorHash`, the `queue_turn` chosen row, and the `capture` final phase, which can overwrite `cleanup` with `captured`.
  - **Fix:** compare the decision's predecessor inside the transaction (D1).
- [x] [Review][Patch] FP13 (medium) — Delivered cleanup persists no pending action, and `erase_held` doubles as delivered cleanup. [`verify.py:640-680`, `verify.py:924-929`]
  - Reproduced: terminal reconcile → `cleanup` with intent `None` → a partial `erase_held` leaves no pending action, so delivered and erased cannot be told apart.
  - **Fix:** a distinct delivered-cleanup intent and function (D7).
- [x] [Review][Patch] FP14 (medium) — The held `observed` phase has no exit, and some phases have no transitions. [Candidate :44, :110-120]
  - A held record created at its first nonterminal response (136 KiB charge plus registry row) has no exit when a later local attempt succeeds before capture.
  - Execution `cleanup`/`incident` and held `quarantined`/`incident`/`closed` appear only in the enumerations.
  - **Fix:** define `observed→cleanup` on authenticated original-route terminal success; define or remove `closed` (AC2).
- [x] [Review][Patch] FP15 (medium) — Disposition evidence cites verification that does not exist; capture-scope accounting is unmodeled. [`obligations.md` dispositions; `verify.py:339-347`, `verify.py:581-591`]
  - These rows cite functions that do not cover them: VG2-2, E2-8/9, BH2-9, E2-18/19, BH2-19/E2-21, E2-32 and BH2-2/E2-10/11. Also, `admission_evidence_hold` is never emitted.
  - The model has no capture-scope or unidentified account. Reproduced: a deployment-scope held delivery raises `TypeError`.
  - **Fix:** add the capture-scope path, and make every row cite only real verification or the D9 runtime gate (AC1).
- [x] [Review][Patch] FP16 (medium) — An expired prepared request starts the irreversible producer-disable. [`verify.py:501-506`]
  - `resume_step` ignores expiry in `prepared`, so a crash plus a delayed recovery acts on expired operator authority.
  - **Fix:** in `prepared` with no irreversible readback and `now ≥ expiry`, take the authenticated-absence cancel path. This is the corollary of D8's rule that "expiry never cancels irreversible work".
- [x] [Review][Patch] FP17 (medium) — Inventory: one unavailable owner fails the whole page, and the cursor envelope is unspecified. [`verify.py:725-744`; candidate :130]
  - **Fix:** return a per-row explicit evidence-incident view.
  - **Also specify:**
    - the cursor authentication envelope and its 16 KiB cap;
    - the HTTP statuses for invalid, expired and cross-scope cursors.
- [x] [Review][Patch] FP18 (medium) — Authority and absence are caller booleans. [`verify.py:360`, `verify.py:320-324`, `verify.py:547-550`, `verify.py:664-665`, `verify.py:843-859`]
  - Affected: `refund(deleted=True)`, `reconcile_placeholder(absence)`, `cancel_resume(absence)`, `erase_held(authority)` and the scope `closed` flags. This is the same class as SE-2.
  - **Fix:** use readback of the modeled artifacts, and represent external obligation and erasure authority as stored fixture authority records.
- [x] [Review][Patch] FP19 (medium) — Trackers were closed before this focused review, and the evidence is ephemeral. [`deferred-work.md:5197`, `sprint-status.yaml:236`, this record :5 and :160-164]
  - D-SPLIT reads "resolved … independently verified and reviewed" and Story 6.5 is unblocked, while the `-2` status is `done` and the sprint row is `review`.
  - The Verification section is stale (47,916 B / 995 lines).
  - The proposal, parent acceptance and protected-gate artifacts live only in `/tmp`.
  - **Fix:** reopen D-SPLIT and align the statuses; refresh the Verification section; copy the `/tmp` evidence into `6-5d-simplification/` with hashes; drop "independently" (sole maintainer).
- [x] [Review][Patch] FP20 (low) — The replay count-dimension claim is false. [Candidate :88]
  - Accounting (count × 8,192 ≤ 256 MiB) binds at 32,768 events, so the 100,000-event bound and its 75% case are unreachable.
  - **Fix:** state that the accounting dimension supersedes count.
- [x] [Review][Patch] FP21 (low) — `reclaim` can delete the records of a still-pending request's identity. [`verify.py:557-565`]
  - A request stalled for more than 30 days between `successor` and `idle` loses its `invoke` record, which a retry re-creates.
  - **Fix:** skip the pending identity.
- [x] [Review][Patch] FP22 (low) — `decode` and `typed` raise non-`Refusal` exceptions. [`verify.py:53-59`, `verify.py:174-196`]
  - NaN, Infinity, `1e400`, lone surrogates and non-hex strings escape as `ValueError` or `UnicodeError`.
  - A non-hex corrupt held request never reaches the repair path.
  - **Fix:** wrap these in `Refusal`.
- [x] [Review][Patch] FP23 (low) — The verifier summary hard-codes its counts. [`verify.py:1131`]
  - "16 status cases; four matrix rows; 67 lifetime resumes; 131 bounded redrives" are fixed text, yet Completion quotes them as results.
  - **Fix:** compute them.
- [x] [Review][Patch] FP24 (low) — `scope_expire` uses unchecked subtraction. [`verify.py:857`]
  - A corrupt usage row persists a negative value, against D1's checked-arithmetic rule.
  - **Fix:** use checked subtraction.

**Defer:**
- [x] [Review][Defer] FW1 — `verify.py` runs in no automatic CI path. [`6-5d-simplification/verify.py`] — deferred: 6.5d forbids test and CI edits. Story 6.5 integration must wire it in or carry an explicit re-run gate.
- [x] [Review][Defer] FW2 — A drain-limit resume may re-arm members with definitive class-02/03 failures. [Candidate :48, :58, :70] — deferred, maybe-false (medium if true). The precedence (4)-before-(5) is pre-existing (archive D3.1). To settle it, check whether 6.5c C2/C5 lets an active drain-limit record coexist with a definitive class-02/03 member, and whether drain-only resume must exclude such members. Current resolution (2026-10-03): D9 explicitly retains C2 class-02/03 and accepted-member permanent fences; C2 successor checks admit only definitive class-01 retryable rejection. Drain-limit status cannot authorize a forbidden send. The original deferral is retained as history; ledger FW2 is resolved with production qualification still pending.

**Rejected (9):**
- ECH-25: false. D4 row (9), "other … failed classes", covers class-01 below maximum without an automatic attempt, and archive D3.1 row 9 states it explicitly.
- BH-17 (C5 part): false. C5 terminal verification consumes the authenticated prior closure accumulator/count held in the control (`history`/`closedCount`), not the deleted closure bytes. The lifecycle part is FD3.
- ECH-4: low. A `closedCount` overflow needs 2^64 closed windows, so it is unreachable; the fix would add a guard.
- ECH-10: low. A precommit reserve retried after a grant needs a duplicate admission after commit, which idempotent admission prevents. The extra row self-resolves through idempotent `batch`.
- ECH-11: low. `KeyError` is a loud failure for missing immutable operation authority, which no scenario produces.
- ECH-12: low. `StopIteration` for an absent row is a loud failure in an unreachable situation.
- ECH-20: low. A status read on a deleted scope is outside the model and fails loudly; the fix would add a guard.
- ECH-22: low. The model never produces an `incident` execution with an eligible reason; the fix would add a guard.
- ECH-26: low. The byte-compare CAS is fixture-only, controls carry revisions, and the fix would change the Store API.

### Review Findings — 2026-10-04 recovery-build review (bmad-code-review)

**Scope and method:**
- **Input:** `ab11c86a..bf11765c`, restricted to story-owned `_bmad-output/implementation-artifacts/` paths (23 files, +2,331/−582; 3,796 diff lines, 387,652 bytes). `bf11765c` was committed by a concurrent process under the subject "feat: add focused review artifacts…", which does not mention the bundled candidate, verifier and tracker changes.
- **Excluded:** `prior-evidence/` (byte-identical `/tmp` copies bound by `manifest.json`), the `.gz` blobs, and the immutable archives.
- **Reviewed SHA-256s:**
  - candidate `d779479795c5ebb8e90961e5c9b3987c5815c85a3c1c5a1e19a1d1f306fd86d4` (171 lines, 63,623 B)
  - `verify.py` `b5bab7e6acff9b102b483a7c97ffe79f241172bde84a76bbd0ed2cdd9e463d77` (1,672 lines, 124,312 B)
  - `known-answers.json` `b0a4bd1e608b5f3c345d7ffcd5236250292bbc0e4808cb95a867033b79e26804`
  - `obligations.md` `3ad39e6c83896bb03c8e6e04655189e89698e59456d93c0e040cfa5308d9ac89`
- **Layers:** Blind Hunter, Edge Case Hunter, Verification Gap and Acceptance Auditor all ran and reported. No layer failed.
- **Baseline at `bf11765c`:**
  - `verify.py`, `mutations.py` and `reviews/focused-regressions.py` exit 0; `git diff --check` is clean.
  - `acceptance.py` **exits 1** (`AssertionError: Git history changed`). With only its HEAD-equality assertion relaxed in memory, it passes.
- **Verification:**
  - 84 raw findings: Blind Hunter 18, Acceptance Auditor 17, Verification Gap 19 (pre-verified, including 2 "Other"), Edge Case Hunter 30.
  - Reproduced with `verify.py`'s own functions:
    - cross-tenant cursor invalidation;
    - the second resume refused with the private label `capacity`;
    - a corrupt owner rendered `stale=False`, and a non-dict owner raising `AttributeError`;
    - recovery without `now` performing producer-disable;
    - `batch-conflict` aborting every queue turn;
    - capsule re-entry resetting legacy `claimed/1/gen 2 → failed/0/gen 1`, and stranding a pending resume (`resume-transition`);
    - 200 maximum-width MessageIds refused `intent-bound` at `claimed→draining`;
    - a stale repaired-range record re-authorizing reclaim, and a `transport-retryable` label skipping repair;
    - a re-signed repair claim wedging both redrive (`route-authority`) and reconcile (`reconcile-phase`);
    - an erase racing capture, leaving the carrier object with no control, charge or registry row;
    - a 1,024-quote scope id refused `control-byte-bound`.
  - Six guard-removal mutants still pass the full suite: `route-terminal-readback`, `legacy-completion-readback`, send `terminal-no-send`, the `:777` accepted exclusion, the `:510` refund readback, and the `legacy-failure` closed set.
- **Size:** the candidate and verifier exceed the design-pressure targets (50 KiB; 70 KiB/1,500 lines). The record's "bounds and necessary cases take precedence" note is the stated explanation.
- **Result:** 4 decision-needed, 26 patch, 1 defer, 11 rejected.

**Decision-needed:**
- [x] [Review][Decision] RD1 (medium) — **Owner chose recommended option (a), 2026-10-04.** Resume stage capacity: refusal reason and sizing.
  - **What happens:** `begin_resume` (`verify.py:680`) refuses with the private label `capacity`, which is outside D3's closed 409/503 sets.
  - **Why it is reached:** a resume holds the active window charge and the new staged charge at the same time. FD3(a) then keeps each superseded charge until `deleteAfter` (expiry + 30 days). D6 readiness guarantees only one 1 GiB window plus fixed charges.
  - **Reproduced:** with a 6 MiB tenant ceiling, the first resume succeeds and the second is refused with `capacity`.
  - **Options:**
    - (a) Map stage and charge refusal to `resume_capacity_hold` (503, Retry-After 30). State in D3/D6 that a resume needs headroom for the staged window plus retained superseded charges; readiness is unchanged. **Recommended.**
    - (b) Raise tenant readiness so that at least one resume always fits (old + staged + fixed).
    - (c) Add a new closed reason.
- [x] [Review][Decision] RD2 (medium) — **Owner chose recommended option (a), 2026-10-04.** Repair authority: per failure, or per capsule?
  - **What happens:** `provider_address('legacy-repaired-range', key, capsule)` (`verify.py:1196`) is keyed by capsule only and is never consumed.
  - **Reproduced:** after one repaired evidence failure, a second evidence failure is reclaimed with the stale record.
  - Candidate D3 :66 binds the record to "the unchanged capsule and every original range/classification field", not to a failure instance.
  - **Options:**
    - (a) Bind the record to the failed legacy generation, and delete/read it back when reclaim consumes it. **Recommended.**
    - (b) Keep it per-capsule (the repair attests capsule content, so reuse is allowed) and document that.
- [x] [Review][Decision] RD3 (medium) — **Owner chose recommended option (a), 2026-10-04.** `owner_fence` "holder lease" is undefined for non-Operations owners.
  - **What happens:** candidate D1 :20 requires "the backend-verified current holder lease" on every mutation, and a transfer that "authenticates the old holder/lease". Only the `operations-epoch` lease is defined; coordinator, gateway, actor and subscriber rows have no lease schema, address, duration, renewal or takeover. This violates PD4 and AC7.
  - **Options:**
    - (a) Non-Operations owners are fenced by `owner_fence` + generation CAS only; a takeover mints a fresh token and stale holders fail. "Holder lease" applies only to the Operations epoch. **Recommended**, as the simplest option.
    - (b) Define a per-owner lease record.
- [x] [Review][Decision] RD4 (medium) — **Owner chose recommended option (a), 2026-10-04.** Queue storage shards (FP10) are prose-only.
  - **What happens:**
    - Candidate D6 :100 says eight addressed shards of at most 100 MiB, with ticket→shard `(ticket−1) mod 8` and a per-shard admission check.
    - `obligations.md:142` calls the shards "optional". No shard or header address is defined.
    - The model stores one queue value with `CAPS['queue']` = 800 MiB + 16 KiB, and `known-answers.json` pins `maxBytes` 838,877,184 for an object larger than the 193 MiB per-object cap.
    - The spec does not say whether a ticket whose shard is full is consumed.
  - **Options:**
    - (a) Define the per-shard key derivation and the header address. A full shard refuses `AppendPreparationLimit` without consuming the ticket. Model the eight shard rows and literals. **Recommended.**
    - (b) Drop sharding: one queue value, with the ceiling lowered to fit 193 MiB (about 12,000 rows), and amend the capability bounds.
    - (c) Map a ticket to the first non-full shard, dropping the fixed `(ticket−1) mod 8` rule.

**Patch:**
- [x] [Review][Patch] RP1 (high) — The legacy restore intent embeds the whole MessageId list, so a legal capsule with more than ~127 maximum-width IDs is stuck in `claimed`, which has no other exit. Bind the intent to capsule hash, `eventRoot`, range and classification, and add a maximum-width `draining` case. The 1,000-row case never reaches `draining`. [`6-5d-simplification/verify.py:1203`; candidate D3 :66]
- [x] [Review][Patch] RP2 (medium) — The build closed its own trackers before this review (FP19 unmet).
  - The `-2` status is `done` while the sprint row is `review`.
  - D-SPLIT is `resolved` with "Story 6.5 may import it", while its evidence line still says 6.5 is blocked until 6.5d is done.
  - Reopen D-SPLIT until a review passes, and align the statuses.
  - [`deferred-work.md:5197`; this record :5; `sprint-status.yaml:236`]
- [x] [Review][Patch] RP3 (medium) — `acceptance.py` hard-pins `HEAD == ab11c86a`, so it exits 1 at the commit that contains it.
  - Everything after line 97 is unreachable: the scope audit, the submodule check, the prior-evidence manifest, and both independent Node constructions.
  - The claims "Each command exited 0" (this record) and "Rerun the commands" (`RECOVERY-EVIDENCE.md`/`parent-acceptance.md`) do not reproduce.
  - The checkpoint stores digests, not "exact initial bytes".
  - Fix: use an ancestry check plus a `RUN_HEAD..HEAD` scope audit over the allowed paths, refresh `current-acceptance.json`, and correct the claims.
  - [`6-5d-simplification/acceptance.py:97`]
- [x] [Review][Patch] RP4 (medium) — The registry is modeled as one deployment-global header plus a full scan, so PD2 and PD5 are incomplete:
  - the cursor generation hashes the global header generation, so one tenant's discover returns `hold_inventory_generation_changed` to another tenant (reproduced);
  - `shard_limit` is compared with the global `scopeCount`;
  - the `registry-entry` known answer pins `shard: 0`, while D8 derives 132 for `tenant`/`t`;
  - one unreadable entry fails every owner's discovery and every inventory page;
  - no shard-header address is defined.

  Fix: bind the generation to the scope header plus the scope's entries; add 256 addressed shard headers with shard-local limits; correct the literal. [`6-5d-simplification/verify.py:391-423`, `:1023`; candidate D8 :124/:128; `known-answers.json` `registry-entry`]
- [x] [Review][Patch] RP5 (medium) — `capsule_make` re-entry overwrites an advanced owner.
  - Re-running it while the original drain remains resets legacy from `claimed`/ordinal 1/generation 2 to `failed`/0/1, a fence-generation regression.
  - Running it during a pending resume leaves `cleanup` with a request, so the resume is wedged with `resume-transition`.
  - Fix: return idempotently when the owner already binds this capsule; refuse while a request is pending.
  - [`6-5d-simplification/verify.py:1079-1100`, `:1135`]
- [x] [Review][Patch] RP6 (medium) — The legacy failure class is asserted by the caller.
  - Labelling an evidence failure `transport-retryable` lets reclaim skip the repaired-range requirement (reproduced).
  - Removing the closed-set check survives the full suite.
  - The live-drain delete runs before the CAS with no intent.
  - Fix: derive the class from authenticated drain-failure evidence (FP18 pattern).
  - [`6-5d-simplification/verify.py:1227-1230`; candidate D3 :66]
- [x] [Review][Patch] RP7 (medium) — The FP16 expiry gate runs only when the caller passes `now`.
  - `finish_resume` never does, so recovery of an expired prepared request performs the irreversible producer-disable (reproduced).
  - Make the clock mandatory.
  - [`6-5d-simplification/verify.py:709-717`, `:762-767`]
- [x] [Review][Patch] RP8 (medium) — Inventory reads owners with `decode`, not `typed`.
  - A corrupt `reason='fabricated-reason'` row renders as authoritative with `stale=False`, and a non-dict owner raises `AttributeError` and aborts the page.
  - Both contradict D8's per-row evidence incident.
  - [`6-5d-simplification/verify.py:1033-1040`]
- [x] [Review][Patch] RP9 (medium) — A refusal on the selected queue row is re-raised, so every turn aborts on the same oldest row and younger tenants are never granted (reproduced with `batch-conflict`).
  - The FP11/D6 rule that such a row "parks/holds" is not implemented.
  - [`6-5d-simplification/verify.py:592-593`]
- [x] [Review][Patch] RP10 (medium) — `queue_materialize` creates the operation-plan authority from the caller's pins (`db.external`), although its comment says the authority is read back.
  - Rerender authority is therefore self-minted.
  - Fix: read the authority created at admission, and refuse if it is absent or different.
  - [`6-5d-simplification/verify.py:558-559`]
- [x] [Review][Patch] RP11 (medium) — `repair_held` accepts a re-signed, non-original claim.
  - With a stale route record present, redrive then refuses `route-authority` and reconcile refuses `reconcile-phase`; only erasure exits (reproduced).
  - D7 requires the exact original request: bind the repair to the retained route/attempt `requestHash`.
  - [`6-5d-simplification/verify.py:922-927`; candidate D7 :120]
- [x] [Review][Patch] RP12 (medium) — `erase_held` racing an in-flight capture leaves the carrier object with no control, charge or registry row (reproduced).
  - This breaks "no uncharged retained object".
  - Fix: after the write, re-read the control and delete/read back the object if the control is absent or not `capturing`; state in D7 that erasure fences in-flight capture.
  - [`6-5d-simplification/verify.py:834-836`; candidate D7 :112/:120]
- [x] [Review][Patch] RP13 (medium) — The `registry-scope` cap of 2 KiB contradicts D8's 1..1,024-byte identifiers.
  - Two maximum ASCII identifiers or escaped characters exceed it, and the refusal is `control-byte-bound`, not `registry_capacity_hold` (reproduced).
  - Size the cap to the escaped maxima and add a maximum-width case.
  - [`6-5d-simplification/verify.py:17`; candidate D8 :124]
- [x] [Review][Patch] RP14 (medium) — The readiness constants exist only in the verifier.
  - The candidate states neither the unidentified/reserve precharge (800 MiB + 16 KiB + 9×overhead) nor the tenant fixed charge (216 KiB + 3×overhead).
  - The encoded floor `reserve ≥ 195 MiB` is unreachable; the effective minimum is ≈ 993 MiB.
  - The tenant fixed charge omits the 2 KiB `registry-scope` header.
  - Fix: state the formula in D6, include the header, and record that the codec floor is superseded (as FP20 did).
  - [`6-5d-simplification/verify.py:481-488`; candidate D6 :96]
- [x] [Review][Patch] RP15 (medium) — The closed eligibility set (`retry-exhausted`, `drain-limit`, `drain-limit-and-retry-exhausted`, `legacy-publish-failed`) was dropped from the contract.
  - It now appears only in `verify.py:31`, although it is a signed claim field and precondition output (an FP8-class regression).
  - [candidate D3 :48-50; `obligations.md:109`]
- [x] [Review][Patch] RP16 (medium) — Restore the owning cases this diff deleted. Each guard below now survives removal:
  - the signed foreign-scope resume claim (`resume-claim-owner`, plus a `resume-claim-request` variant);
  - legacy reclaim with a stale ordinal or before any success (`legacy-reclaim`, `legacy-success-owner`);
  - capsule events, classification, missing or unavailable drain (`legacy-drain-authority`, `legacy-original-range`);
  - forged or unavailable route-terminal records (`route-authority`, plus `terminal-no-send` at send);
  - held absent repair, the `metadata-charge` refund, and signed claims with tag 2 (tenant) or tag 4 (count) substituted;
  - stage refund after both cancel paths;
  - `resume_not_eligible` with nothing unresolved;
  - maximum-width fit for the `registry-scope`/`registry-entry`/`epoch` caps.

  Then correct the `obligations.md:195` coverage claims these contradict. [`6-5d-simplification/verify.py:1393-1653`]
- [x] [Review][Patch] RP17 (medium) — Add owning cases for guards behind named fixes or the FD1 list that never had one:
  - observed `route-terminal-readback`, `legacy-completion-readback`, send `terminal-no-send`;
  - accepted-member exclusion at dispatch (`:777`, AC3);
  - drain-absent before restore (`:1201`), `legacy-success-audit` (`:1192`), refund delete-readback (`:510`), `placeholder-effect`/`owner-deletion-readback` (`:455`/`:437`), and resume claim time (`:694`);
  - the PD3 reclaim artifact set: all retained artifacts deleted, the active window kept;
  - `queue_turn` parking on a missing or mismatched plan;
  - the PD2 deployment-scope registry account and a `shard_limit` hold;
  - FD1's 193 MiB + 1 capture and 1,001-event bound, on the owning paths;
  - FP12 predecessor CAS for `queue_turn`, `capture`, `reclaim` and `cancel`;
  - FP1 capsule hash/source tamper;
  - FP9 retained-handle resolution and the PD5 page revision.

  [`6-5d-simplification/verify.py`]
- [x] [Review][Patch] RP18 (low) — The model's refusal labels differ from the candidate's closed public reasons:
  - `redrive-count` vs `held_redrive_count_changed`;
  - `cursor-invalid/expired/scope` vs `hold_inventory_cursor_*`;
  - scope admission surfacing `owner`/`invalid-json`.

  There is also no known answer for the cursor envelope or the held-redrive request/202. [`6-5d-simplification/verify.py:855`, `:1024-1028`, `:1244-1256`]
- [x] [Review][Patch] RP19 (low) — A falsy cursor (`{}`) is treated as "no cursor" and restarts paging; use `is not None`. [`6-5d-simplification/verify.py:1024`]
- [x] [Review][Patch] RP20 (low) — `RegistryCapacityHold` has prose (candidate :124) but no row in the D8 predicate/exit table. [candidate D8 :129-144]
- [x] [Review][Patch] RP21 (low) — FP7 is only partly done.
  - The model's charges use improvised keys (`stage:`, `wait:`, `object:`, `metadata:`, `old-window:`) instead of the retained `publication-charge:` derivation.
  - The format of the control `charge` field is unspecified.
  - [`6-5d-simplification/verify.py:542`, `:601`, `:680`; `obligations.md:138`]
- [x] [Review][Patch] RP22 (low) — `mutations.py` and `focused-regressions.py` accept any `AssertionError`/`Refusal` and do not pin the expected owning label, so the claim "failed their owning checks" is not demonstrated. [`6-5d-simplification/mutations.py:25-30`]
- [x] [Review][Patch] RP23 (low) — FP23 is only partly done.
  - `matrixRows = len((…4 functions…))` is a constant and omits the idle-replay row.
  - `lifetimeResumes` and `boundedRedrives` are loop sizes.
  - The same Acceptance Auditor finding's public-literal part is RP18. Its claimed D1/D3 `resume_arithmetic_exhausted` contradiction is refuted: D1 :24 itself says the preflight refuses unchanged with that reason.
  - [`6-5d-simplification/verify.py:1657`]
- [x] [Review][Patch] RP24 (low) — The claim that "fixed maxBytes values are checked against the normative schema caps" is overstated.
  - `RECORD_CAPS`/`CAPS` are a second hard-coded copy that is never parsed from `obligations.md`.
  - `answer_mutations` mutates only `D45-window`.
  - [`6-5d-simplification/verify.py:1285`, `:1312-1317`; `obligations.md:188`]
- [x] [Review][Patch] RP25 (low) — The retained probe evidence is not reproducible as claimed.
  - The scripts use hard-coded absolute paths and run against the post-fix `verify.py`.
  - `pre-fix-probe-output.txt` names other `/tmp` scripts, and no hash binds it to the pre-fix verifier.
  - [`6-5d-simplification/reviews/focused-review.md:35`; `reviews/owner-and-cleanup-probes.py:8`]
- [x] [Review][Patch] RP26 (low) — `review-input.json` and `final-review-input.json` name the same overwritten `/tmp` diff with different digests. Drop the `/tmp` fields or mark them non-durable. [`6-5d-simplification/review-input.json`, `final-review-input.json`]

**Defer:**
- [x] [Review][Defer] RW1 — New invariants have no refusal case:
  - `registry-count-readback`/`registry-scope-readback`;
  - `cursor-envelope-bound`/`cursor-position`;
  - `capsule-prior-chunk`;
  - `execution-identity`/`execution-progress`/`predecessor-generation`;
  - `intent-binding`/`intent-phase`.

  [`6-5d-simplification/verify.py:346-357`, `:396-398`, `:1025-1031`, `:1111`] — deferred: owner decision FD1(a) declined per-guard coverage. The behavior is exercised end to end.

**Rejected (11):**
- BH "independently" (FP19 wording): false.
  - The FP19 target was D-SPLIT's assurance claim ("independently verified and reviewed"), which is gone.
  - The remaining uses describe separate implementations or agents (an independent Node construction), not an assurance control.
- BH FW2 (status drain + class-02): false.
  - Protected 6.5c C2 says "class-02 terminal rejection cannot mint a new ID" and admits successors only after class-01. D9 :167 retains that fence.
  - Precedence (4) before (5) is the specified D4 order.
- BH owner-registry/1 reshaped without a version bump: false. The schema never left the unapproved candidate and has no deployed reader or writer, and the prior literal is archived.
- AA D3 receipts contradiction: false.
  - The permanent closed-window fence is the broker's window-aware index (D3 :56 "Closed-window delayed IDs reject").
  - C5 terminal proof uses the history accumulator.
  - Reclaimed receipts are EventStore readback copies.
- ECH `resolve_handle` refuses when a sibling is unreadable: false. Failing closed is correct, because a unique match cannot be authenticated without reading every candidate owner.
- ECH same address under two scopes: low. Production addresses are derived from the scope (tenant + execution, held identity); only the model's literal keys collide. The fix would add a guard.
- ECH `queue_materialize` StopIteration on an absent row: low. The case is unreachable, and the 2026-10-03 ECH-12 precedent applies.
- ECH `redrive_reconcile` AttributeError on an absent control: low. This is a misuse-only path, and the fix would add a guard.
- ECH `capsule_read` UnicodeDecodeError: low. It needs corrupt bytes that were authenticated anyway, and the fix would add a guard.
- ECH `scope_status` after `scope_expire`: low. The 2026-10-03 ECH-20 precedent applies.
- ECH commit subject misdescribes `bf11765c`: low. This is not an artifact defect. The commit is local (2 commits ahead of `origin/main`), so the owner may reword it before pushing.

## Historical completion claim — superseded by focused review

The three fresh focused reviews produced fourteen individually adjudicated findings, grouped into eleven correction roots. All are resolved in the existing controls/queue with owning regression assertions; no intent change, new coordination family or new deferred entry was introduced. Corrections cover earned legacy resume authority, exact audit bytes, atomic held metadata/capture charging, unchanged quota refusal, fresh bound route reconciliation, complete provider-source erasure, authentic capsule sources, field-specific UTF-8 bounds, remaining owner/provider generations and net queue grant/refund accounting. The original terminal-guard mutation now fails its owning assertion.

Final candidate: 172 lines/48,554 bytes (94.9% fewer bytes than the 957,247-byte archive). Verifier: 1,133 lines/80,557 bytes. The verifier exceeds the 70 KiB design-pressure target by 8,877 bytes to retain the eleven demonstrated regression groups; its size remains below the 1,500-line target. History remains byte-identical and outside the active contract.

Final full verifier passes 24 wire/JSON literals, nine framed keys, five control literals, all 54 dispositions, 16 status cases, four matrix rows, 82 persisted resume restarts, 67 lifetime successes, 131 bounded redrives and the focused corrections. Independent Node construction and all 23 previously published digest comparisons pass. Six parent temporary-copy corruptions each fail; an additional terminal-authority guard mutation also fails. Frozen intent and both archive hashes pass.

`python3 /tmp/6-5d-simplification-protected.py` initially passed before concurrent work, then returned exit 1 at its whole-workspace path allowlist: 33 newly modified/untracked runtime paths appeared under Client/Contracts/Server/controllers; additional runtime paths continued to appear during finalization. These are outside this task and were neither edited nor reverted here. The strict original helper and checkpoint remain unchanged. `python3 /tmp/6-5d-simplification-authority-check.py` independently passes the original four protected hashes, UNAPPROVED AD-13, .gitmodules/submodule state and all 156 checkpoint pins; it deliberately reports the workspace-wide allowlist separately. No source/type/runtime approval is inferred from that narrower result. Observed concurrent paths are recorded in `/tmp/6-5d-simplification-concurrent-runtime.json`.

Only the 6.5d sprint row and D-SPLIT completion status are finalized by the parent. AD-13 and its receipt remain UNAPPROVED, integration/human approval remain pending, no Story 6.6 work is authorized, and no Git history mutation is performed.

## Historical verification — superseded by focused review

Parent acceptance on 2026-10-03: compact candidate 172 lines/47,916 bytes; verifier 995 lines/68,226 bytes. Direct verifier and independent Node reconstruction passed (24 retained wire/JSON answers, nine keys, five control literals). Independent frozen/archive hashes and exact original 54-ID comparison passed. Six temporary-copy mutations failed their owning checks. Protected gate passed four hashes, AD-13 UNAPPROVED, nine allowed paths and 156 external pins. Acceptance detail: `/tmp/6-5d-simplification-parent-acceptance.md`. Three fresh focused reviews follow; provider behavior remains unproved.

Run `python3 _bmad-output/implementation-artifacts/6-5d-simplification/verify.py` and the narrow independent known-answer reconstruction written by the parent. Audit the exact frozen-intent hash, archived hashes, protected source paths, all 54 dispositions, and `git diff --check`. Runtime provider tests remain an explicit implementation gate, not evidence claimed by the local verifier.

## Historical recovery run — 2026-10-03, superseded by correction

Resumed the owner-approved PD1–PD5 and FP1–FP24 corrections from clean HEAD `ab11c86a991a6aa8349041e73312a9655a0aa1bf`. The original source baseline and frozen intent remain unchanged. At recovery start, tasks and D-SPLIT were reopened pending corrected acceptance and focused review, with the sprint row in-progress. Current completion below records the final state. Earlier completion/verification sections above are historical claims, not current evidence.

FP19 historical evidence is retained byte-for-byte in [prior-evidence](6-5d-simplification/prior-evidence/README.md), with exact original paths, lengths and SHA-256 values in [manifest.json](6-5d-simplification/prior-evidence/manifest.json). The strict historical protected gate remains unchanged; current scope preservation compares against the full recovery-run HEAD separately. Current acceptance and review evidence are retained durably below. No runtime, test-project, architecture, dependency, submodule or Git history changes are authorized by this story.

## Historical recovery verification — superseded by correction

Parent acceptance passed for the corrected candidate/supporting delta against recovery HEAD `ab11c86a991a6aa8349041e73312a9655a0aa1bf`. [Current verifier output](6-5d-simplification/current-verification.json), [preservation and independent byte construction](6-5d-simplification/current-acceptance.json), and [six bounded corruptions](6-5d-simplification/current-mutations.json) retain actual results. [Recovery instructions](6-5d-simplification/RECOVERY-EVIDENCE.md) document the scope checkpoint and byte-identical earlier evidence. All PD1–PD5 and FP1–FP24 corrections and four focused recovery groups are implemented and verified. [Three-layer review and targeted closure](6-5d-simplification/reviews/focused-review.md) records six individual verdicts before grouping, both crash reproductions now passing, and [two owning guard-regression failures](6-5d-simplification/reviews/current-focused-regressions.json). No demonstrated blocker remains and no new work is deferred; all three main tasks are complete. Historical review_loop_iteration=12 remains unchanged under the active simplification authorization.

Run `python3 _bmad-output/implementation-artifacts/6-5d-simplification/verify.py`, `python3 _bmad-output/implementation-artifacts/6-5d-simplification/acceptance.py`, `python3 _bmad-output/implementation-artifacts/6-5d-simplification/mutations.py`, `python3 _bmad-output/implementation-artifacts/6-5d-simplification/reviews/focused-regressions.py`, and `git diff --check`. Each command exited 0; the six corruptions and two targeted guard removals each failed their owning checks. These checks establish specification evidence only; no provider, architecture or AD-13 approval is inferred.

## Historical recovery completion — 2026-10-03, superseded by correction

The corrected 6.5d candidate is complete and reviewed, with execution status done and only its sprint row set to review. D-SPLIT is resolved for this child-specification prerequisite; Story 6.5 must still import the candidate and obtain exact integrated AD-13 human approval. AD-13 and its receipt remain UNAPPROVED; Story 6.6 remains backlog and is not authorized. Existing FW1 automatic CI wiring is explicitly assigned to Story 6.5 integration; FW2 is settled by the retained C2 permanent member fences, with provider qualification still pending.

[Parent acceptance](6-5d-simplification/parent-acceptance.md) accounts for all seven criteria, three tasks and four executed matrix groups, actual artifact sizes and the bounded verification evidence. The preservation gate confirms all 8,251 outside-scope recovery inputs, original frozen/archive/protected hashes and nine root submodule revisions remain unchanged. Source baseline 01498ac721db7c44f18fcf9591ffbbf30ba245e2 and review loop iteration 12 are preserved. No runtime, test-project, architecture, dependency, submodule or Git history mutation was made, and no provider proof or self-approval is claimed.

## Approved recovery corrections — 2026-10-04

Owner selected recommended RD1–RD4 decisions and authorized implementation in the new [correction execution record](spec-6-5d-hold-lifecycle-resume-and-legacy-admission-3.md). The latest recovery review supersedes prior completion claims; tasks, sprint state and D-SPLIT were reopened pending corrected acceptance and focused review; the current completion below records their final resolution. Original frozen intent, source baseline and historical review counter remain unchanged.

## Current correction completion — 2026-10-04

All RD1–RD4 recommendations and RP1–RP26 corrections are implemented and individually accounted for in the [correction execution record](spec-6-5d-hold-lifecycle-resume-and-legacy-admission-3.md). Full parent gates and targeted three-layer review closure pass. Both execution records and the child sprint entry are done; D-SPLIT evidence/status agree. Original frozen intent, source baseline and historical review counter 12 remain unchanged. No new deferral; existing RW1 remains. The verification layer reused a read-only investigator after the fresh-launch tool limit; the review record discloses its prior context. Runtime implementation, provider qualification, Story 6.5 integration and AD-13 approval remain pending.
