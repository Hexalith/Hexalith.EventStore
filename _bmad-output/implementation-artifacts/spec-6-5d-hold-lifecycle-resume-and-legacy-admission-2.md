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


## Completion — 2026-10-03

The three fresh focused reviews produced fourteen individually adjudicated findings, grouped into eleven correction roots. All are resolved in the existing controls/queue with owning regression assertions; no intent change, new coordination family or new deferred entry was introduced. Corrections cover earned legacy resume authority, exact audit bytes, atomic held metadata/capture charging, unchanged quota refusal, fresh bound route reconciliation, complete provider-source erasure, authentic capsule sources, field-specific UTF-8 bounds, remaining owner/provider generations and net queue grant/refund accounting. The original terminal-guard mutation now fails its owning assertion.

Final candidate: 172 lines/48,554 bytes (94.9% fewer bytes than the 957,247-byte archive). Verifier: 1,133 lines/80,557 bytes. The verifier exceeds the 70 KiB design-pressure target by 8,877 bytes to retain the eleven demonstrated regression groups; its size remains below the 1,500-line target. History remains byte-identical and outside the active contract.

Final full verifier passes 24 wire/JSON literals, nine framed keys, five control literals, all 54 dispositions, 16 status cases, four matrix rows, 82 persisted resume restarts, 67 lifetime successes, 131 bounded redrives and the focused corrections. Independent Node construction and all 23 previously published digest comparisons pass. Six parent temporary-copy corruptions each fail; an additional terminal-authority guard mutation also fails. Frozen intent and both archive hashes pass.

`python3 /tmp/6-5d-simplification-protected.py` initially passed before concurrent work, then returned exit 1 at its whole-workspace path allowlist: 33 newly modified/untracked runtime paths appeared under Client/Contracts/Server/controllers; additional runtime paths continued to appear during finalization. These are outside this task and were neither edited nor reverted here. The strict original helper and checkpoint remain unchanged. `python3 /tmp/6-5d-simplification-authority-check.py` independently passes the original four protected hashes, UNAPPROVED AD-13, .gitmodules/submodule state and all 156 checkpoint pins; it deliberately reports the workspace-wide allowlist separately. No source/type/runtime approval is inferred from that narrower result. Observed concurrent paths are recorded in `/tmp/6-5d-simplification-concurrent-runtime.json`.

Only the 6.5d sprint row and D-SPLIT completion status are finalized by the parent. AD-13 and its receipt remain UNAPPROVED, integration/human approval remain pending, no Story 6.6 work is authorized, and no Git history mutation is performed.

## Verification

Parent acceptance on 2026-10-03: compact candidate 172 lines/47,916 bytes; verifier 995 lines/68,226 bytes. Direct verifier and independent Node reconstruction passed (24 retained wire/JSON answers, nine keys, five control literals). Independent frozen/archive hashes and exact original 54-ID comparison passed. Six temporary-copy mutations failed their owning checks. Protected gate passed four hashes, AD-13 UNAPPROVED, nine allowed paths and 156 external pins. Acceptance detail: `/tmp/6-5d-simplification-parent-acceptance.md`. Three fresh focused reviews follow; provider behavior remains unproved.

Run `python3 _bmad-output/implementation-artifacts/6-5d-simplification/verify.py` and the narrow independent known-answer reconstruction written by the parent. Audit the exact frozen-intent hash, archived hashes, protected source paths, all 54 dispositions, and `git diff --check`. Runtime provider tests remain an explicit implementation gate, not evidence claimed by the local verifier.
