---
title: 'Story 6.5d: Hold Lifecycle, Resume, and Legacy Admission'
type: 'feature'
created: '2026-09-30'
status: 'in-progress'
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
- [ ] [Review][Patch] PD1 (high, from FD1 option a) — Add owning behavioral cases for the bounded list in FD1, and correct the Completion and summary coverage claim. Do not add a per-guard mutation sweep. [`6-5d-simplification/verify.py`]
- [ ] [Review][Patch] PD2 (medium, from FD2 option a) — Make registry entries addressed per-row entries (≤64 KiB, each with its own generation) under a per-shard count header. Define the registry key derivation and a gateway-owned `RegistryCapacityHold` with readiness pre-rejection and deletion or capability-increase exits. Charge each row to its scope's tenant or deployment account. Update the model and add cases. [Candidate D8 :124-126; `verify.py:300-324`]
- [ ] [Review][Patch] PD3 (medium, from FD3 option a) — Keep the old window charge until authenticated deletion and readback of exactly the superseded window claim, attempt set, drain-limit source/resolution, closure and effect records at `deleteAfter`. C5 terminal proof uses the control's `history`/`closedCount` accumulator. Make `reclaim` delete exactly that set. [Candidate D3 :52/:60; `verify.py:534`, `verify.py:557-565`]
- [ ] [Review][Patch] PD4 (medium, from FD4 option b) — Keep the application-owned metadata adapter. Define the `owner_fence` mint/advance/fencing semantics and the epoch lease record (address, schema, bound, duration, renewal, takeover, erasure). Make `publicationRetentionBackend` name the same declared backend. Record the AD/AD-26 amendment (DDL, migration ownership, credentials) as a required Story 6.5/architecture handoff in D9, without editing any AD. [Candidate D1 :20, D6 :94, D8 :128, D9]
- [ ] [Review][Patch] PD5 (medium, from FD5 option b) — Bind the hold cursor generation to the registry generation only, and return each row's owner revision and stale flag. Order stays registry `firstUtc`; a registry change still returns `hold_inventory_generation_changed`. Update `inventory_page` and add continuation cases. [Candidate D8 :130; `verify.py:725-744`]
- [ ] [Review][Patch] FP1 (high) — The legacy resume path is never bound to the capsule. [`verify.py:443-538`, `verify.py:813-826`; candidate :64-66]
  - `begin_resume` and `resume_step` never read or verify the capsule. Reproduced: a legacy execution with no capsule and no drain completes resume (ordinal 1, outcome and audit written).
  - The invoke re-arms roster positions `[2,3]` rather than the capsule's exact MessageIds and range.
  - `claimed→draining` neither calls `capsule_restore` nor checks for the absence of a live drain.
  - `failed→claimed` never checks that the capsule is unchanged.
  - This violates AC3/AC5 and matrix row 2.
- [ ] [Review][Patch] FP2 (high) — `legacy_transition` ordinal, repair and failure authority; the SB-1/SE-4 fix is incomplete. [`verify.py:813-826`]
  - Non-`failed` edges store the caller's ordinal. Reproduced: draining/failed with ordinal 0 lets `failed→claimed` reuse ordinal 1 after one success.
  - `repair` is an unauthenticated boolean.
  - `failure` is always `transport-retryable`, so the evidence-failure branch is unreachable.
  - **Fix:**
    - Require `ordinal==legacy.ordinal` on non-failed edges.
    - Use the closed failure set `transport-retryable|evidence-unavailable|evidence-contradictory`.
    - An evidence reclaim reads back a repaired-range record bound to the capsule.
- [ ] [Review][Patch] FP3 (high) — Resume recovery recomputes the intent payload from the mutable partition and wedges after an irreversible effect. [`verify.py:514`, `verify.py:522`]
  - Reproduced: the crash leaves the persisted `reject` intent; a member is accepted (D3 expects this); then `finish_resume` fails `intent-binding` every time and `cancel_resume` fails `irreversible`. There is no exit, against AC2 and D1.
  - **Fix:** the persisted intent (address, hash, payload) is the recovery authority; never recompute it.
- [ ] [Review][Patch] FP4 (medium) — A legacy recovery that fails after a successful resume cannot re-arm. [`verify.py:529`, `verify.py:509-511`, `verify.py:824-825`; candidate :66]
  - The successor sets `source=ZERO` and idle clears `reason`, so a later `draining→failed` has no eligible source.
  - "A later exhaustion reuses the same capsule" has no transition.
  - **Fix:** `draining→failed` atomically reinstalls `source=capsule hash` and `reason=legacy-publish-failed` in the same CAS.
- [ ] [Review][Patch] FP5 (medium) — `capsule_make` charges and writes chunks with no owner, discovery or intent. [`verify.py:763-792`, `verify.py:1038-1042`]
  - Reproduced: a refusal on a later chunk leaves the earlier chunks charged with zero registry rows.
  - The scenario also deletes the drain before any execution control or discovery exists, against D3 "control/discovery before cleanup" and D1.
  - **Fix:** discover and persist the legacy-recovery owner and its capsule intent before the first charge; delete the drain only after control and discovery read back.
- [ ] [Review][Patch] FP6 (medium) — The control decoder does not enforce D2/D7 exact schemas or closed sets. [`verify.py:148-232`]
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
- [ ] [Review][Patch] FP7 (medium) — Retained key families lost their literal known answers; the self-test is vacuous; `maxBytes` is unpinned. [`known-answers.json` keys; `verify.py:863-882`; `obligations.md`]
  - Only 9 key answers remain. About 20 retained families (capacity-subject … publication-invocation, command-execution-scope) had archive literals and no obsolete row.
  - The model uses improvised addresses (`action:`, `object:`, raw `scope-usage:` concatenation) instead.
  - The `:879-882` self-test never runs `verify_answers`.
  - Raising `maxBytes` still passes.
  - **Fix:** restore independently recomputed literals, use the retained derivations in the model, make the self-test run the real check on a mutated deep copy, and pin `maxBytes` to the obligations caps.
- [ ] [Review][Patch] FP8 (medium) — Closed public and hold literals were compressed out of the active contract. [Candidate :62, :72, :116, :132-149]
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
- [ ] [Review][Patch] FP9 (medium) — `resumeHandle` is no longer discoverable. [Candidate :50, :130]
  - The archive's rule "every eligible hold inventory item exposes resumeHandle" was dropped. The inventory view returns no handle, and handle→execution resolution is undefined.
  - **Fix:** restore exposure, and state resolution by recomputation over the tenant's registered execution owners.
  - The missing domain tag and placeholder answer `hxrsm1-handle` are pre-existing (archive :239/:850).
- [ ] [Review][Patch] FP10 (medium) — Capability bounds admit configurations where kind maxima can never fit. [Candidate :96, :100]
  - `quarantineMax` can be 256 MiB with `unidentifiedCeiling` 195 MiB, so a deployment-scope oversize capture never fits.
  - At minimum capability, the eight ≤100 MiB queue envelopes (account unspecified) leave about 410 MiB of deployment, under the 449 MiB maximum pin.
  - The row→queue-shard assignment is unspecified.
  - **Fix:** capability validation requires every kind maximum plus overhead to fit its account after fixed precharges; name the envelope account and the shard selector.
- [ ] [Review][Patch] FP11 (medium) — Queue turn head-of-line starvation. [`verify.py:405-424`, `verify.py:373`, `verify.py:397-403`]
  - The fit check uses the stale materialized amount, while `batch` re-renders with the current overhead. Reproduced at the ceiling: every turn refuses `capacity` and the younger tenant's ticket 2 never proceeds.
  - Invalid materialized rows (60 pins, duplicate MessageIds, cleanup state) block the queue the same way.
  - **Fix:** re-render under owner CAS and park above-ceiling rows before the fit; validate in materialize; a chosen-row refusal parks or holds that row instead of aborting the turn.
- [ ] [Review][Patch] FP12 (medium) — Transactions decide on a stale read but CAS against a fresh re-read. [`verify.py:471-475`, `verify.py:419-423`, `verify.py:598-599`]
  - Affected: the `begin_resume` request and `priorHash`, the `queue_turn` chosen row, and the `capture` final phase, which can overwrite `cleanup` with `captured`.
  - **Fix:** compare the decision's predecessor inside the transaction (D1).
- [ ] [Review][Patch] FP13 (medium) — Delivered cleanup persists no pending action, and `erase_held` doubles as delivered cleanup. [`verify.py:640-680`, `verify.py:924-929`]
  - Reproduced: terminal reconcile → `cleanup` with intent `None` → a partial `erase_held` leaves no pending action, so delivered and erased cannot be told apart.
  - **Fix:** a distinct delivered-cleanup intent and function (D7).
- [ ] [Review][Patch] FP14 (medium) — The held `observed` phase has no exit, and some phases have no transitions. [Candidate :44, :110-120]
  - A held record created at its first nonterminal response (136 KiB charge plus registry row) has no exit when a later local attempt succeeds before capture.
  - Execution `cleanup`/`incident` and held `quarantined`/`incident`/`closed` appear only in the enumerations.
  - **Fix:** define `observed→cleanup` on authenticated original-route terminal success; define or remove `closed` (AC2).
- [ ] [Review][Patch] FP15 (medium) — Disposition evidence cites verification that does not exist; capture-scope accounting is unmodeled. [`obligations.md` dispositions; `verify.py:339-347`, `verify.py:581-591`]
  - These rows cite functions that do not cover them: VG2-2, E2-8/9, BH2-9, E2-18/19, BH2-19/E2-21, E2-32 and BH2-2/E2-10/11. Also, `admission_evidence_hold` is never emitted.
  - The model has no capture-scope or unidentified account. Reproduced: a deployment-scope held delivery raises `TypeError`.
  - **Fix:** add the capture-scope path, and make every row cite only real verification or the D9 runtime gate (AC1).
- [ ] [Review][Patch] FP16 (medium) — An expired prepared request starts the irreversible producer-disable. [`verify.py:501-506`]
  - `resume_step` ignores expiry in `prepared`, so a crash plus a delayed recovery acts on expired operator authority.
  - **Fix:** in `prepared` with no irreversible readback and `now ≥ expiry`, take the authenticated-absence cancel path. This is the corollary of D8's rule that "expiry never cancels irreversible work".
- [ ] [Review][Patch] FP17 (medium) — Inventory: one unavailable owner fails the whole page, and the cursor envelope is unspecified. [`verify.py:725-744`; candidate :130]
  - **Fix:** return a per-row explicit evidence-incident view.
  - **Also specify:**
    - the cursor authentication envelope and its 16 KiB cap;
    - the HTTP statuses for invalid, expired and cross-scope cursors.
- [ ] [Review][Patch] FP18 (medium) — Authority and absence are caller booleans. [`verify.py:360`, `verify.py:320-324`, `verify.py:547-550`, `verify.py:664-665`, `verify.py:843-859`]
  - Affected: `refund(deleted=True)`, `reconcile_placeholder(absence)`, `cancel_resume(absence)`, `erase_held(authority)` and the scope `closed` flags. This is the same class as SE-2.
  - **Fix:** use readback of the modeled artifacts, and represent external obligation and erasure authority as stored fixture authority records.
- [ ] [Review][Patch] FP19 (medium) — Trackers were closed before this focused review, and the evidence is ephemeral. [`deferred-work.md:5197`, `sprint-status.yaml:236`, this record :5 and :160-164]
  - D-SPLIT reads "resolved … independently verified and reviewed" and Story 6.5 is unblocked, while the `-2` status is `done` and the sprint row is `review`.
  - The Verification section is stale (47,916 B / 995 lines).
  - The proposal, parent acceptance and protected-gate artifacts live only in `/tmp`.
  - **Fix:** reopen D-SPLIT and align the statuses; refresh the Verification section; copy the `/tmp` evidence into `6-5d-simplification/` with hashes; drop "independently" (sole maintainer).
- [ ] [Review][Patch] FP20 (low) — The replay count-dimension claim is false. [Candidate :88]
  - Accounting (count × 8,192 ≤ 256 MiB) binds at 32,768 events, so the 100,000-event bound and its 75% case are unreachable.
  - **Fix:** state that the accounting dimension supersedes count.
- [ ] [Review][Patch] FP21 (low) — `reclaim` can delete the records of a still-pending request's identity. [`verify.py:557-565`]
  - A request stalled for more than 30 days between `successor` and `idle` loses its `invoke` record, which a retry re-creates.
  - **Fix:** skip the pending identity.
- [ ] [Review][Patch] FP22 (low) — `decode` and `typed` raise non-`Refusal` exceptions. [`verify.py:53-59`, `verify.py:174-196`]
  - NaN, Infinity, `1e400`, lone surrogates and non-hex strings escape as `ValueError` or `UnicodeError`.
  - A non-hex corrupt held request never reaches the repair path.
  - **Fix:** wrap these in `Refusal`.
- [ ] [Review][Patch] FP23 (low) — The verifier summary hard-codes its counts. [`verify.py:1131`]
  - "16 status cases; four matrix rows; 67 lifetime resumes; 131 bounded redrives" are fixed text, yet Completion quotes them as results.
  - **Fix:** compute them.
- [ ] [Review][Patch] FP24 (low) — `scope_expire` uses unchecked subtraction. [`verify.py:857`]
  - A corrupt usage row persists a negative value, against D1's checked-arithmetic rule.
  - **Fix:** use checked subtraction.

**Defer:**
- [x] [Review][Defer] FW1 — `verify.py` runs in no automatic CI path. [`6-5d-simplification/verify.py`] — deferred: 6.5d forbids test and CI edits. Story 6.5 integration must wire it in or carry an explicit re-run gate.
- [x] [Review][Defer] FW2 — A drain-limit resume may re-arm members with definitive class-02/03 failures. [Candidate :48, :58, :70] — deferred, maybe-false (medium if true). The precedence (4)-before-(5) is pre-existing (archive D3.1). To settle it, check whether 6.5c C2/C5 lets an active drain-limit record coexist with a definitive class-02/03 member, and whether drain-only resume must exclude such members.

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

## Completion — 2026-10-03

The three fresh focused reviews produced fourteen individually adjudicated findings, grouped into eleven correction roots. All are resolved in the existing controls/queue with owning regression assertions; no intent change, new coordination family or new deferred entry was introduced. Corrections cover earned legacy resume authority, exact audit bytes, atomic held metadata/capture charging, unchanged quota refusal, fresh bound route reconciliation, complete provider-source erasure, authentic capsule sources, field-specific UTF-8 bounds, remaining owner/provider generations and net queue grant/refund accounting. The original terminal-guard mutation now fails its owning assertion.

Final candidate: 172 lines/48,554 bytes (94.9% fewer bytes than the 957,247-byte archive). Verifier: 1,133 lines/80,557 bytes. The verifier exceeds the 70 KiB design-pressure target by 8,877 bytes to retain the eleven demonstrated regression groups; its size remains below the 1,500-line target. History remains byte-identical and outside the active contract.

Final full verifier passes 24 wire/JSON literals, nine framed keys, five control literals, all 54 dispositions, 16 status cases, four matrix rows, 82 persisted resume restarts, 67 lifetime successes, 131 bounded redrives and the focused corrections. Independent Node construction and all 23 previously published digest comparisons pass. Six parent temporary-copy corruptions each fail; an additional terminal-authority guard mutation also fails. Frozen intent and both archive hashes pass.

`python3 /tmp/6-5d-simplification-protected.py` initially passed before concurrent work, then returned exit 1 at its whole-workspace path allowlist: 33 newly modified/untracked runtime paths appeared under Client/Contracts/Server/controllers; additional runtime paths continued to appear during finalization. These are outside this task and were neither edited nor reverted here. The strict original helper and checkpoint remain unchanged. `python3 /tmp/6-5d-simplification-authority-check.py` independently passes the original four protected hashes, UNAPPROVED AD-13, .gitmodules/submodule state and all 156 checkpoint pins; it deliberately reports the workspace-wide allowlist separately. No source/type/runtime approval is inferred from that narrower result. Observed concurrent paths are recorded in `/tmp/6-5d-simplification-concurrent-runtime.json`.

Only the 6.5d sprint row and D-SPLIT completion status are finalized by the parent. AD-13 and its receipt remain UNAPPROVED, integration/human approval remain pending, no Story 6.6 work is authorized, and no Git history mutation is performed.

## Verification

Parent acceptance on 2026-10-03: compact candidate 172 lines/47,916 bytes; verifier 995 lines/68,226 bytes. Direct verifier and independent Node reconstruction passed (24 retained wire/JSON answers, nine keys, five control literals). Independent frozen/archive hashes and exact original 54-ID comparison passed. Six temporary-copy mutations failed their owning checks. Protected gate passed four hashes, AD-13 UNAPPROVED, nine allowed paths and 156 external pins. Acceptance detail: `/tmp/6-5d-simplification-parent-acceptance.md`. Three fresh focused reviews follow; provider behavior remains unproved.

Run `python3 _bmad-output/implementation-artifacts/6-5d-simplification/verify.py` and the narrow independent known-answer reconstruction written by the parent. Audit the exact frozen-intent hash, archived hashes, protected source paths, all 54 dispositions, and `git diff --check`. Runtime provider tests remain an explicit implementation gate, not evidence claimed by the local verifier.
