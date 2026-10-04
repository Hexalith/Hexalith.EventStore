---
title: 'Story 6.5: Event Versioning And Upcasting Spec'
type: 'feature'
created: '2026-09-26'
status: 'done'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 'cbbe41501ba722731bf36b2c343efdef4ac714fb'
initial_baseline_commit: 'ccb4faf03256ef8eb627d49d6f6f0a032fcb4830'
story_key: '6-5-event-versioning-and-upcasting-spec'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Stable `IEventContract.EventType` exists, but persisted events and consumers still use CLR names without a payload schema version. Identity checks and cancellation behavior differ across append, replay, projection, and subscription paths, leaving event evolution unsafe to implement without a frozen contract.

**Approach:** Produce `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` as the versioned AD-13 design. Inventory the current paths, fix the future metadata, registry, upcast, identity, failure, cancellation, and migration contracts, run the relevant checks and record the owner's approval. Story 6.6 starts when the owner requests implementation. This story changes no runtime behavior.

## Boundaries & Constraints

**Always:** Specify exact field names, types, defaults, grammar, uniqueness, legacy version, and mapping for canonical kebab-case identity; distinguish metadata-envelope, domain-service, and payload versions. Preserve stored bytes, `MessageId`, sequence, correlation, protection metadata, and actor-owned commits. Require one shared allow-listed read pipeline and pre-append/pre-dispatch identity checks. Define numeric chain/payload limits, typed failures, checkpoint/last-good-state and cancellation/commit behavior, additive legacy adapters, mixed-version rollout/rollback, provider-portable evidence, support-safe diagnostics, and a closed 6.6 slicing decision.

**Never:** Implement 6.6, edit runtime/tests/public contracts, rewrite history on read, guess unknown types, load arbitrary types, silently complete poison deliveries, claim AOT/trimming support, or depend on Epic 8 protection. Record actual owner approval; do not invent it. Synchronize only Story 6.5's completion status through the workflow after its relevant checks pass.

**Decisions (owner, 2026-09-29):**
- D-NFR12: AD-13 carries an NFR12 breaking-change proposal, approved by the same receipt, covering the `EventsStored`+`Retryable=false` publication-exhausted hold on every shipped surface (repository search is the floor), Admin `TimelineLimit` beyond 1,000 events/64 MiB, and long-stream (>~32,768 events) full-replay holds after activation; Story 6.6 ships SemVer-major. Nothing is deferred to 6.6.
- D-CLOSE: the integrator closes every contract-level refinement routed to Story 6.5 integration with a normative rule tagged `[I-nn]` for approver visibility; evidence/tooling items become Story 6.6 verification obligations; non-story entries stay open outside this story.
- D-SCOPE: keep one integration spec and one integrated change, despite exceeding the token guideline.
- D-RESUME (2026-09-30): Story 6.6 ships an authenticated operator publication-resume operation that re-arms publication of the same committed events under the same MessageId, never re-executing the command. It has its own signing purpose, audit record and hold-inventory removal, and legacy status-6 records may use it after reconciliation. `PublicationRetryExhaustedHold` and drain-limit holds therefore have an exit. C5's replay-safety gate (BC-02) stays.

- D-SPLIT (2026-09-30, revises D-SCOPE): review pass 2 showed that the mechanisms the integration had to invent do not converge inside one integration change. Those mechanisms are:
  - the D-RESUME operation and legacy status-6 resume (`[I-45]`/`[I-46]`);
  - hold inventory and redrive (`[I-36]`/`[I-37]`);
  - pin-capacity wait queues (`[I-31]`);
  - legacy scope claims (`[I-12]`);
  - the `[I-06]` activation record;
  - the exits, reason codes and capacity rules these depend on.

  They move to a focused child spec story, Story 6.5d, under the children's routing bar. Story 6.5 integration then imports 6.5d exactly as it imported 6.5a–6.5c. D-RESUME stays in force as a 6.5d requirement. Story 6.5 is blocked until 6.5d is done.
- D-ARCH (owner, 2026-10-04): “do recommended” authorizes closing the PostgreSQL architecture handoff within Story 6.5. Extend this run's permitted paths to `_bmad-output/planning-artifacts/architecture.md` and `6-5-integration/metadata-adapter-contract.md`. Make focused AD-1/AD-26 amendments and specify the already selected adapter's exact schema/caps/indexes, transaction contract, migration/rollback ownership and least-privilege OpenBao credentials. Keep actor event/snapshot mutation authority and separate production-profile/qualification approval. Use the chosen backend and existing owner records; add no generic provider/migration framework or new runtime mechanism.
- D-RELEASE (owner, 2026-10-04): “do recommended” authorizes compatible maintenance/security publication through the existing current-main manual workflow while 6.6 progresses. Reuse API/wire/package-consumer and focused inactive-path evidence. Compatible dormant preparation is not automatically a shipped breaking change; genuine breaking commits remain honestly classified and require SemVer-major. Retain the hold whenever compatibility is unproven or incomplete breaking changes have reached main; expose the complete approved incompatible set only in the major release. Add no maintenance lane, release-version override or CI mutation in 6.5.
- D-VALIDATION (owner, 2026-10-04): “I Jérôme Piquot approve” approves the presented technical candidate. The subsequent request “make validation simple and pragmatic” explicitly replaces the formal approval and whole-repository freeze policy. Owner approval in this conversation is sufficient; no signed external record, authenticated source timestamp, manually repeated hash or exact authorization sentence is required. Keep checks of actual reviewed inputs, examples and relevant regressions; normal owner commits, unrelated work and submodule advancement do not invalidate them. Preserve historical captures and production/runtime requirements. Update the administrative approval record and Story 6.5 trackers after those checks pass. Story 6.6 is ready and starts on owner request. Use one focused independent review of this administrative amendment with the reused investigator; retain the completed three original reviews. No Git or runtime mutation is authorized.

</frozen-after-approval>


## Code Map

All short artifact paths below resolve under `_bmad-output/implementation-artifacts/`.

- `spec-event-versioning-upcasting.md` — sole normative AD-13 target. The historical loop-1 candidate remains reproducible at `288a61908f4661fed52bb791f928292fe7d90180`, SHA-256 `c474df76a687b2798757051efbdb382bb6b9650f7bd7a7637d0c67bfc5e7b715`. Current §12 accepts the owner’s conversational approval and records the content digest automatically; the old exact authorization sentence is superseded. Old scratchpad backups are historical, never prerequisites.
- `spec-6-5a-event-contract-writer-and-migration-evidence.md`, `spec-6-5b-verified-read-replay-and-projection.md`, `spec-6-5c-publication-subscription-and-rollout.md` and completed `-2.md` records — reviewed A/B/C inputs already imported; §11.4's file/model hashes still match. Keep source files immutable. Preserve public bytes, unowned known answers and V17/V20/V22/V23 signed fixtures.
- `spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md` — reviewed simplified D1–D9, last content revision `b1558b06a1a1771a10e1c0fe76b54a417c96558e`, SHA-256 `6d7e9326671572e07426329c6f57fb38879589e04c155290b04a82df184a1e98`. Completed `-2.md`/`-3.md` retain owner decisions, correction evidence and review limits. D9 is the exact import/activation map.
- `6-5d-simplification/obligations.md`, `known-answers.json`, `verify.py`, integer-safe `independent-answers.mjs` and shared-key constructor — normative schemas/literals and bounded local evidence. Copy required schema tables/literals into AD-13 so its digest covers the complete contract; cite exact full committed revisions and file/block hashes. Preserve the entire child directory and execution records; archives are historical.
- Parent replacements: I-06 ← D5; I-10/I-14/I-15/I-16 ← D3/D4; I-12/I-17 ← D5; I-29/I-30/I-31 ← D1/D6; I-36/I-37 ← D2/D7/D8; I-45/I-46 ← D2/D3. Amend I-01/I-26/I-28 to match whole-batch reservation, eight-envelope single queue and bootstrap precharges. Retain all unrelated A/B/C hold predicates when replacing the inventory.
- Amend imported C1/C2/C4/C5 in place: batch reservation before separate pin install; window/member/send authority; captured physical custody distinct from route completion; permanent operation-terminal fence distinct from resume-window fence. Replace obsolete old hold/redrive/repair/reconciliation families, routes, purposes and answers throughout §§7–11; keep no competing rule.
- `story-6-5-review-pass-2-findings.md` — 13 parent findings: VG2-1/5/6/7, BH2-11/12/13/14/15/17, E2-27/33/37. The other 54 have exact D obligations. Historical review/triage/design-note files remain immutable.
- `deferred-work.md` — preserve unrelated entries, earlier resolutions, D-SPLIT child closure and RW1 during this amendment. The 47 accepted specification dispositions remain open implementation/evidence follow-ups. Check those owned records without freezing unrelated ledger progress; O-06/O-07/O-11 retain focused evidence obligations.
- New `6-5-integration/` — input-focused traceability verifier, recorded source inputs, independent surviving-codec constructor and local check results. D-VALIDATION replaces the whole-repository freeze with descriptive change scope. Historical child `acceptance.py` and its old correction baseline remain archived evidence, not a required current check. Keep its sources and pins unchanged.
- Source inventory is read-only: `src/Hexalith.EventStore.Contracts/Events/{IEventContract,EventContractMetadata}.cs`, Server `EventEnvelope`, `AggregateActor`, `EventStreamReader`, Client replay/subscription, gateway controllers and Admin query/filter surfaces. The integrated inventory describes its recorded source snapshot; Story 6.6 checks its affected sources and symbols at its current revision.
- `../planning-artifacts/architecture.md` AD-1/AD-13/AD-26, `scripts/validate-publication-preflight.sh`, `.github/workflows/release.yml`, `.releaserc.json`, `.editorconfig`, `.gitattributes` and build/test configuration — read-only constraints, except architecture scope as authorized by D-ARCH. D-VALIDATION also permits the Story 6.5/6.6 reconciliation paragraphs in `../planning-artifacts/epics.md` and the Story 6.5 status/date in `sprint-status.yaml`. No runtime, tests, scripts, CI, dependencies, submodules or Git mutation.

## Tasks & Acceptance

**Execution:**

- [x] `6-5-integration/source-manifest.json`, `verify.py` — retain the historical source inventory and check the actual reviewed child/source/model input pins. D-VALIDATION removes the whole-repository freeze. Preserve old captures and reproduce child literals using existing integer-safe constructors.
- [x] `../planning-artifacts/architecture.md`, `6-5-integration/metadata-adapter-contract.md` — close AD-1/AD-26's exact adapter documentation contract under D-ARCH. Preserve actor mutation authority and the separate unapproved production/profile gate. Do not choose new record, codec, state or exit policy locally.
- [x] `spec-event-versioning-upcasting.md` — splice the reviewed D1–D9 and supporting schema/literal contracts; replace all owned I-rules and amend imported C1/C2/C4/C5 in place. Reconcile §§8.1/10.1/10.2/10.3/11: BC-15 legacy claim outage, BC-16 tombstone/410/reuse, authoritative exact-status filters, activation order, current public routes, budgets and legacy recovery prerequisites. Apply D-RELEASE's compatible-maintenance/complete-major policy. Cite D9's actual provider/crash obligations without claiming fixture proof.
- [x] `spec-event-versioning-upcasting.md`, `6-5-integration/verify.py`, `independent-answers.mjs` — resolve VG2-1/5/6/7, BH2-17 and E2-33: derive all 16 same-attempt pairs from pinned A `reduce_set`; compare exact labeled B results and inline C offset/destination vectors; enforce verifier identities and counts; independently reconstruct surviving parent codecs. Exercise each demonstrated corrupting edit and require its owning failure. Import reviewed D bounded cases/constructors without promising per-guard coverage.
- [x] `spec-event-versioning-upcasting.md`, `deferred-work.md` — reconcile BH37-1..10, every raw pass-1/pass-2 finding and ledger entry to one disposition. Keep unapproved entries open with proposed disposition/citation; give each O-row Story 6.6 ownership, gate and closure evidence. Restore O-06/O-07/O-11 for the replacement boundary gate, carry FW1 as an explicit rerun gate, retain RW1 unchanged, cite D6's mismatched-attach conflict/refusal, and remove obsolete O-10/O-20 mechanisms.
- [x] `6-5-integration/verification-results.json`, this execution record — retain completed reviews and historical captures, record focused verification and the independent amendment review, and synchronize completion after the owner's existing approval and current relevant checks. D-VALIDATION supersedes the former external receipt and whole-repository freeze requirements; it starts no Story 6.6 runtime work.

**Acceptance Criteria:**

- Given completed reviewed 6.5a–d inputs, when the integrated artifact is traced, then every metadata, writer, reader, publication, migration, identity, cancellation and compatibility seam has one exact schema/algorithm/bound/outcome/vector; no superseded rule survives, no mechanism is invented, and no implementation decision remains open.
- Given BH37-1..10, pass-1 groups, the 54 D-owned rows and 13 parent findings, when the register and ledger are checked, then each has one explicit disposition, each verification obligation has named story/gate/evidence ownership, and accepted specification dispositions never claim runtime resolution or implementation completion.
- Given the approved architecture/release choices, when compatibility and activation are inspected, then the metadata deployment contract is exact and architecture-consistent, every changed public behavior is classified (including exact filters, BC-15/16), and the approved maintenance/major policy is stated without a fictitious release lane.
- Given the revised verification gates, when baseline checks and targeted corruptions run, then every required block runs, labeled values and independent constructors agree, and each demonstrated regression fails its owning check; local-model success remains distinct from unproven provider behavior.
- Given the owner continues ordinary repository work, when Story 6.5 validation runs, then unrelated commits, files and submodule changes do not fail it; the actual reviewed child inputs, model blocks and public fixtures remain checked against their recorded pins. Preserve user changes and historical evidence without silently repinning an input.
- Given the owner's approval and requested validation amendment, when the relevant checks pass and the administrative §12 record names Jérôme Piquot and this conversation, then Story 6.5 is complete. Tooling records its current digest and recording date; neither an external authenticated capture nor a magic authorization sentence is required. Story 6.6 can be requested separately.

## Implementation Notes

The current AD-13 candidate is `spec-event-versioning-upcasting.md` and remains unapproved. Stories 6.5a–6.5c own focused specification work; this story owns integration, disposition of open findings, content-bound approval, and explicit authorization for Story 6.6. Their reviews alone grant no runtime authority.

Historical review material is retained verbatim after a short file header:

- [Review change log](story-6-5-review-change-log.md)
- [Review triage](story-6-5-review-triage.md)
- [Design notes](story-6-5-design-notes.md)

The latest review snapshot is v37 with `BH37-1` through `BH37-10` open. Its prior instruction to stop rederivation remains recorded in the historical log; this backlog split does not revise the normative candidate.

- 2026-09-29 re-plan. The spec had been left `in-review` at loop 36, although no integration had been done. On the owner's choice it was reset to `draft` with baseline `ccb4faf0` and loop 0. The pre-split loop history stays in the historical files above.

- 2026-09-29 integration build. `spec-event-versioning-upcasting.md` is now the integrated AD-13 candidate: 2,361 lines, 1,004,971 bytes, LF only, `baseline_commit` `ccb4faf0`. A1–A10, B1–B10 and C0–C7 are imported under their labels into the section each amends, with the child models cited rather than copied (§11.4). The superseded pre-integration rules (codec-01 intent/receipt/bundle hash, 13-field corrupt disposition, codec-01 outcome and its retry alternative, 8 KiB attestation, seven-part effect key and 4 KiB receipt, hash-only pin tombstones, legacy manifest and acknowledgement point, V2 record-absence sentence, zero-page genesis, per-call latest root) were removed or amended in place. 44 integration rules are tagged `[I-01]`–`[I-44]`; §8.1 gives each of the 24 child-introduced outcomes one consumer outcome; §10.1–§10.3 carry the D-NFR12 proposal (BC-01–BC-12) and the additive classification; §11.5 gives BH37-1..10 and the 54 ledger entries one class each (12 citations, 36 rules, 11 Story 6.6 obligations, 5 non-story). `deferred-work.md` has 47 updated `status:` lines; the 5 non-story entries stay open and the 2 already resolved entries are unchanged.
- Recomputed §12 digest (not approval): `a6de32e35a0ade68e79699af79be5e432e7b16b069d1e1ab2180f4c91fa9fb3d`. The receipt still holds five `UNAPPROVED` values plus the fixed scope. Whole-file SHA-256 `d5a145151de6c028678a28ca067ca420aaa861e8cb0e6ac0037fcfde7b0341d2`.
- Verification: all three bash blocks pass (V17/V20/V22 and V23 verifiers, then the digest); one receipt marker; BH37-1..10 all present; the 6.5a, 6.5b and 6.5c model commands exit 0 against the unchanged candidates; `git diff --check ccb4faf0` is clean. The 6.5c documentation-integrity script (its second Verification block) fails by design, as planned. Drift: `git diff --name-only ccb4faf0` also lists `references/Hexalith.Builds` and `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.cs`, which commits `5f854425` and `27279fe6` changed after the baseline was set; against `HEAD`, only this story spec, the AD-13 artifact and the ledger differ. Inventory drift at `ccb4faf0` is recorded in `[I-02]`.

- 2026-09-30 review loop 1. Review pass 1 routed an intent gap (G-D) and nine bad_spec groups. The AD-13 artifact and ledger were reverted to `HEAD`, and their pre-review bytes kept as the re-derivation base. The owner resolved G-D as D-RESUME, and the tasks were re-planned as a re-derivation. Code Map line numbers refer to the `ccb4faf0` bytes.

- 2026-09-30 review loop 1 re-derivation. `spec-event-versioning-upcasting.md` was rebuilt from the verified pre-review bytes by scripted splicing. It is now 2,825 lines and 1,079,383 bytes, LF only. What changed:
  - All 24 pass-1 `patch` rows are applied, and imported rules are amended in place where a patch touches them (A8 tag `09` and its no-post-commit rule, A9, A10, C1, C5, C6 and the §7 side-record cap).
  - Groups G-A…G-C and G-E…G-J are reworked in [I-06], [I-10]–[I-17], [I-29], [I-31], [I-36] and [I-37]. Evidence-required admission activates only at slice 4 ([I-41]).
  - D-RESUME is [I-45] (new §7.4) and [I-46] (legacy status-6), under the new signing purpose `2d`. The [I-14] drain-limit hold `PublicationDrainLimitHold` and the retry-exhaustion hold both exit through it, and [I-15] signals the operator wait with `Retry-After: 60`.
  - [I-47] retires the historical 6.5c integrity script, so §11.7 withdraws O-06, O-07 and O-11 and adds O-19 and O-20. §10.2 gains BC-01k, BC-13 and BC-14.
  - §11.5 holds 12 citations, 39 rules, 8 Story 6.6 obligations, 5 non-story entries and the D-RESUME row, plus a one-disposition table covering all 59 pass-1 findings.
  - §11.6 adds 22 integration codec known answers and two `bash` verifier blocks. The first recomputes the answers and runs the capacity, same-attempt transition, status-mapping and pin-queue models. The second checks the §11.4 pins and the child-model answers.
  - `deferred-work.md` rewords the same 47 `status:` lines to `dispositioned pending approval`: 36 rules, 8 obligations and 3 entries now closed by [I-47]. The 5 non-story and 2 already-resolved entries are unchanged.
- Recomputed §12 digest (not approval): `7af8041cca7941c22879ac031d2eccc5f9d89dae3ca0ba7870443a767261424d`. The receipt still holds five `UNAPPROVED` values plus the fixed scope. Whole-file SHA-256 `c474df76a687b2798757051efbdb382bb6b9650f7bd7a7637d0c67bfc5e7b715`; ledger SHA-256 `f5d314a5a213dd8ab08f0f18ead75246e8f89a8bd9d8a2f67bd668f2e59b7196`.
- Verification: command 1 runs five `bash` blocks, all passing, and prints the digest last. There is one receipt marker, and BH37-1..10 are all present. The 6.5a, 6.5b and 6.5c model commands exit 0; the 6.5c integrity script still fails by design. `git diff --check ccb4faf0` is clean, and the name-only list is the three task files plus `references/Hexalith.Builds` and `ContractsPackageDependencyTests.cs`. Mutating one A-series answer, one B-series answer, one §11.4 pin or one integration answer makes a verifier block fail. `scripts/check-deferred-work.py` exits 0 with unchanged counts.

- 2026-09-30 review pass 2 and split. Review pass 2 ran on the loop-1 re-derivation (AD-13 whole-file SHA-256 `c474df76…b715`, digest `7af8041c…`). It returned about 66 raw findings, concentrated on integration-invented mechanisms. The orchestrator confirmed E2-34: `[I-45]` contradicts C5's operation-namespace fence. The owner chose D-SPLIT, so pass 2 was not triaged here. The raw findings are kept in [story-6-5-review-pass-2-findings.md](story-6-5-review-pass-2-findings.md) as Story 6.5d input.
  - The working tree keeps the loop-1 re-derivation uncommitted, with the pre-review backups in the session scratchpad (`backup-pre-review/`, `backup-pre-review-2/`).
  - Story 6.5 returns to `draft`. Its next plan imports 6.5d, re-checks the loop-1 artifact against 6.5d, and restores the named-approval completion condition (BH2-12).

- 2026-09-30 correct-course (`sprint-change-proposal-2026-09-30.md`). Story 6.5d is created as `backlog`. It owns `[I-06]`, `[I-10]`, `[I-12]`, `[I-14]`–`[I-17]`, `[I-29]`–`[I-31]`, `[I-36]`, `[I-37]`, `[I-45]`, `[I-46]` and every hold or wait exit; this story keeps the other `[I-nn]` rules and may add no new mechanism. Of the 67 pass-2 findings, 54 go to 6.5d and 13 return to this story's next plan: VG2-1, VG2-5, VG2-6, VG2-7, BH2-11 to BH2-15, BH2-17, E2-27, E2-33 and E2-37. The loop-1 re-derivation noted above as uncommitted is committed as `288a6190`, now on `origin/main`.


- 2026-10-04 parent re-plan after child completion. Planning baseline is `cbbe41501ba722731bf36b2c343efdef4ac714fb`, clean `main`; initial baseline and historical notes remain evidence. Cached epic context is valid; previous completed 6.1 continuity preserves immutable stream/snapshot authority. **Intent gaps:** Q-ARCH and Q-RELEASE only. **Irreversibles:** none in this documentation run; publishing, runtime migration, dependency or Git mutation is outside scope. **Footprint:** parent build record, AD-13, affected ledger status/evidence, new `6-5-integration/` verification artifacts, plus architecture documentation only as authorized by D-ARCH. Three read-only investigators supplied handoff, parent-finding and validation maps. No integration implementation has started.
- Baseline evidence: all five existing parent bash blocks exit 0; normative digest `7af8041cca7941c22879ac031d2eccc5f9d89dae3ca0ba7870443a767261424d`. In-memory corruptions still passed for accepted-row unfreezing, swapped B labels, changed inline C offset digest and a removed verifier fence. Child `verify.py` passes; child `acceptance.py` exits 1 at line 114 with `changes outside correction scope` against its old correction baseline (792 newer committed paths). Its pins remain immutable history; the successor gate belongs to this integration.
- D-SPLIT's child prerequisite is now satisfied by completed 6.5d, including recovery corrections. The preserved frozen statement that integration is blocked until 6.5d completes is historical; no human-owned decision is rewritten. D-SCOPE's existing acceptance of a single integrated change beyond the token guideline persists. This record retains its earlier append-only history, so its total size exceeds the recommended range; implementation follows the current Code Map/Tasks/Verification, with history read for evidence only.

- 2026-10-04 owner answered both questions with “do recommended” after the detailed tradeoff explanation. D-ARCH/D-RELEASE are recorded above; no Open Questions remain. This instruction approves proceeding with the presented build plan and recommended scope, rather than requesting another confirmation. The single-goal scope acceptance persists. Plan approval does not authenticate §12 approval or authorize Story 6.6.

## Spec Change Log

- 2026-09-30 review loop 1. **Trigger:** review pass 1 routed one intent gap (G-D: `PublicationRetryExhaustedHold` has no resolution, and BC-02 removes today's replay recovery) and nine bad_spec groups G-A…G-C and G-E…G-J. In each group an integration-authored `[I-nn]` rule left a state with no exit, prescribed an impossible action, or defined a codec with no known answer. It also found about 24 text-level patches (see the Review Triage Log). **Amendment:** pending the owner's G-D answer. Step 2 re-plans the non-frozen sections so that every `[I-nn]` rule meets the completeness bar: exit, capacity, activation slice, codec plus known answer plus verifier, and inventory. **Known-bad state avoided:** an approvable AD-13 whose integration rules strand commands, deliveries or pins, rely on unencodable legacy records, or bind literals nothing checks. **KEEP:**
  - Start re-derivation from the pre-review integration bytes, whole-file SHA-256 `d5a145151de6c028678a28ca067ca420aaa861e8cb0e6ac0037fcfde7b0341d2` (scratchpad backup), rather than from scratch.
  - Keep the A1–A10/B1–B10/C0–C7 placement and imports.
  - Keep the [I-01] precedence list.
  - Keep the §8.1 outcome matrix structure.
  - Keep the §10.2 BC table and §10.3.
  - Keep the §11.4 citations with the historical marking.
  - Keep the §11.5 register structure.
  - Keep the §11.6 known answers.
  - Keep the §11.7 obligation list.
  - Keep the §12 bytes and the `UNAPPROVED` receipt.
  - Keep the 47 ledger dispositions, reworded per BH-1.
  - Keep `[I-02]` inventory drift.
  - Keep every `[I-nn]` rule that no finding names.
  - Apply every `patch` row of pass 1 during re-derivation.


- 2026-10-04 planning supersession. **Trigger:** completed simplified 6.5d and its correction review; retained 13 parent pass-2 findings; historical source/preservation drift. **Amendment:** replace obsolete map/tasks/checks with the D9 splice plan, reproducible committed base, current-baseline preservation and independent/labeled verification; restore the exact-content human-approval completion criterion and D-CLOSE tooling obligations. Q-ARCH/Q-RELEASE were resolved by D-ARCH/D-RELEASE on 2026-10-04; their previous pending state remains planning history. **Known-bad state avoided:** approving contradictory old/new holds, silently removing gates, treating fixture or plan approval as runtime authority, or relying on vanished scratchpad bytes. **KEEP:** every prior history entry, original frozen intent, A/B/C candidates, child archives/checkpoints/public literals, original §12 receipt/hash rule and unrelated owner changes. The earlier “Amendment: pending” line describes pass 1; D-RESUME was resolved on 2026-09-30 and is now supplied by reviewed D3 rather than integration-authored mechanism.
- 2026-10-04 owner-directed validation amendment. **Trigger:** Jérôme Piquot approved the presented candidate and explicitly requested simple, pragmatic validation for one contributor. **Amendment:** D-VALIDATION replaces the external authenticated receipt, magic authorization sentence and whole-repository freeze; ordinary conversation approval plus relevant checks is sufficient. Update the active administrative policy and trackers while retaining the reviewed technical contracts and follow-ups. **Known-bad state avoided:** requiring repeated approval ceremonies or rejecting unrelated owner work. **KEEP:** reviewed child inputs, public bytes, event-evolution semantics, production security/provider/runtime gates, previous reports and check captures, unrelated user changes. The older KEEP instructions for §12's former wording and UNAPPROVED state are explicitly superseded by the owner's latest instruction.

## Review Triage Log

Review pass 2 (2026-09-30): not triaged. The owner chose D-SPLIT before classification. The raw findings (VG2 10, BH2 19, E2 38) are in [story-6-5-review-pass-2-findings.md](story-6-5-review-pass-2-findings.md), and only E2-34 was verified (true).

Review pass 1 (2026-09-30), diff `ccb4faf0..worktree` limited to the three story files; layers: Blind Hunter (BH), Edge Case Hunter (E), Verification Gap (VG). Groups G-A…G-J share one root cause each.

| ID | Verdict and evidence | Route |
| --- | --- | --- |
| VG-1 | medium — the only retention model (6.5c C01d, candidate :573–620) still asserts the rules [I-26]–[I-30] replaced and exits 0; nothing executes the [I-26] known answer, and O-03 would port the superseded asserts. | patch |
| VG-2 | medium — the §11.6 known answers and §11.4 SHA-256 pins are compared by no command; a one-character edit to `K07-receipt` passes every check. | patch |
| VG-3 | medium — codecs in [I-08], [I-09], [I-12], [I-31], [I-33], [I-37] have no known answer or model; one contradiction (tag `09`) already exists. | bad_spec (G-J) |
| VG-4 | rejected — the fix edits this build's spec (AC3/Verification base); the two other-owner drift paths are recorded in Implementation Notes. | reject |
| VG-O1 | medium — A8 :1154 keeps tag `09` "exactly `required`" while [I-12] :1174 writes `legacy`; a verifier that follows A8 rejects legacy records. | patch |
| VG-O2 | low — `parse_legacy` and `check-deferred-work.py` ignore `status:` lines (counts unchanged before and after); a pre-existing convention with no everyday harm. | reject |
| BH-1 | low — 47 ledger lines say `resolved` while the §12 receipt is `UNAPPROVED`, and no rule reopens them on refusal or amendment. | patch |
| BH-2 | low — O-01..O-11 bind 6.6 through epics :4596 "conforms exactly", but nothing tracks them; the fix would edit `epics.md`, which this spec forbids, and omission is unlikely. | reject |
| BH-3 | low — O-06, O-07 and O-11 harden the 6.5c integrity script that §11.4 declares historical; 6.6 has nothing to apply them to. | patch |
| BH-4 | rejected — the fix edits this build's spec (AC3 wording). | reject |
| BH-5 | rejected — the fix edits this build's spec (Code Map line numbers). | reject |
| BH-6a | medium — same defect as VG-O1. | patch |
| BH-6b | medium — legacy stores lack tags 03–08/`0a` (`CommandStatusRecord.cs` has no domain or aggregate type and a nullable `AggregateId`; `ArchivedCommand.cs` has no aggregate type), so [I-12]'s "same key and codec" cannot be encoded; the tenant sub-claim is false (`SubmitCommandRequestValidator.cs:47` admits only lowercase). | bad_spec (G-A) |
| BH-6c | medium — the archive and status stores are key-only, so [I-12] names no enumeration source, and no fence covers admissions between inventory and activation. | bad_spec (G-A) |
| BH-7 | medium — [I-12] scope records and tombstones are never deleted, with no ceiling, charge, or tenant offboarding/erasure rule. | bad_spec (G-A) |
| BH-8 | high — [I-14] :1178 stops drains at `MaxDrainAttempts` with member rows unchanged; the command stays `CommandOutcomeHold`/`EventsStored` with no resolution, where today it ends terminal plus dead-letter (`AggregateActor.cs:2972`). | bad_spec (G-B) |
| BH-9 | medium — [I-10](3)/BC-03 turn a routine retry wait into a 503 with no matching [I-16] reason code and index it in [I-37]; no cause→reasonCode table exists. | bad_spec (G-C) |
| BH-10 | high — `PublicationRetryExhaustedHold` has no resolution ("Story 6.6 ships none", :1505), BC-02 removes today's replay recovery (`ReplayController.cs:35–39`) for all `PublishFailed`, [I-15] makes clients poll every second for ever, and none of this is registered. | intent_gap (G-D) |
| BH-11 | medium — §10.2 :1517 says Admin surfaces need no amendment, but error-rate (`DaprHealthQueryService.cs:374–375`) and the failed/processing filters (`CommandStatusFilterHelper.cs:21–27`, `DaprStreamQueryService.cs:535–541`) change meaning; the 503 sub-claim is false (no Admin caller). | patch |
| BH-12 | medium — B2a `SandboxCapabilityHold` (today's sandbox, `AdminStreamQueryController.cs:1001`, serves any domain) and export truncation→`ExportLimit` (:118 vs `DaprBackupCommandService.cs:140–141`) are breaking but [I-43] classes them additive; the dead-letter sub-claim is already BC-08. | patch |
| BH-13 | medium — the "every merge" premise is false (`release.yml` is `workflow_dispatch`), but no `BREAKING CHANGE` footer, release hold or dormant-code rule enforces [I-41]'s single major. | patch |
| BH-14 | medium — [I-31] entries record no counter, and no cross-counter head or skip rule exists; a never-fitting candidate (1,000 × 1,114,112 B > 1 GiB) blocks its queue for ever. | bad_spec (G-E) |
| BH-15 | medium — [I-06] inventories only before activation, so later growth holds silently; `LegacyArrayLimit` is not in [I-37]; the "signed activation record" has no codec, purpose or fields. | bad_spec (G-F) |
| BH-16 | medium — [I-17]'s destination-configuration schema and the [I-29]/[I-30] capability fields have no codec or tags, and [I-04] forbids new row kinds without amendment. | bad_spec (G-G) |
| BH-17 | medium — [I-36] redrive (worker and operator) has no API, authority, trigger, ordering or readiness-evidence format. | bad_spec (G-H) |
| BH-18 | medium — [I-37]: a gateway up-down counter drifts across replicas and restarts (the repo idiom is an `ObservableGauge`, `EventStoreOperationsTelemetry.cs:55–60`); no index reconciliation, no Admin route or shape, no tenant for purpose-1b scopes; the closed list omits indefinite holds. | bad_spec (G-I) |
| BH-19 | low — no shared canonicalizer exists (AD-27), but every admission path already enforces lowercase tenants, so [I-13]'s hold never triggers; cite the existing grammar. | patch |
| BH-20 | low — [I-32] never says probe identifiers are probe-chosen (live 1,024-byte identifiers give 13,179 B > 8,192); the missing known answer belongs to VG-3. | patch |
| BH-21 | low — [I-03] states no scan depth, and a recursive reading would reject user keys inside `CurrentState`/`Extensions`. | patch |
| BH-22 | low — [I-40] leaves the POST reply and what counts as "drift" unstated; pairing the rejection with a readiness hold is consistent with A9 :1403. | patch |
| E1 | high — [I-11] :1172 makes every same-attempt state change a conflict, contradicting K09 and V20 (:2090); each normal pending→accepted publication would hold. | patch |
| E2 | medium — [I-10] :1170 maps `published`→`Completed`, contradicting A8 :1112 for published rejections; `not-applicable` is unmapped. | patch |
| E3 | medium — [I-14] :1178 keys legacy writer treatment on when the record is written, not on the admission class, so post-activation status-6 records carry pre-activation-only codes. | patch |
| E4 | high — same defect as BH-8. | bad_spec (G-B) |
| E5 | medium — same defect as BH-9. | bad_spec (G-C) |
| E6 | medium — [I-09] :1168: a crash after both outputs but before the preparation-write record leaves POST and status at 503 for ever; no rule lets recovery write the record. | patch |
| E7 | medium — [I-12] states no inventory scope (tenant or domain) and no outcome when an inventory key already holds a `required` record. | bad_spec (G-A) |
| E8 | medium — same defect as BH-6b. | bad_spec (G-A) |
| E9 | low — the scope check precedes the expired-key branch except through the Idempotency-Key coordinator, where precedence with a tombstone is unstated. | bad_spec (G-A) |
| E10 | medium — a matching legacy-class record takes A8 retry selection and waits for a first pin that legacy executions never have. | bad_spec (G-A) |
| E11 | medium — same defect as BH-15. | bad_spec (G-F) |
| E12 | low — [I-06]/BC-05 inventory by event count, but the §8 bound (:1239) also caps readable and accounting bytes. | patch |
| E13 | medium — [I-36] leaves the dead-letter topic's own subscription unconstrained; `resiliency.yaml` `maxRetries: 10` exhausts it. | patch |
| E14 | medium — [I-23] header-invalid carriers enter the [I-36] continuation with no terminal path, and :1226 retains the headers that C1 :817 forbids retaining. | patch |
| E15 | medium — same defect as BH-14. | bad_spec (G-E) |
| E16 | medium — same defect as BH-18 (closed hold list). | bad_spec (G-I) |
| E17 | medium — same defect as BH-18 (metric drift). | bad_spec (G-I) |
| E18 | high — same defect as BH-10. | intent_gap (G-D) |
| E19 | low — same defect as BH-21. | patch |
| E20 | false — C1 :801 holds on any length violation, and a zero-byte address is one. | reject |
| E21 | low — [I-22] :1196 leaves explicit-null (`01`) tags `20`/`21` unmapped. | patch |
| E22 | false — :1224 fixes N to 0..2,147,483,647, and the u32 row count (:1033) can never equal a negative N. | reject |
| E23 | false — C2 :849 observations are create-once CAS, and differing definitive results are incidents. | reject |
| E24 | medium — no slice says when A8 evidence-required admission activates, so slice 1's producer could trigger BC-09/BC-10. | patch |
| E25 | rejected — same as VG-4 (fix edits this build's spec). | reject |
| E26 | low — A8 :1108 keeps the unqualified no-post-commit-discovery rule; [I-01](5) settles precedence, but AC1 ("no superseded rule survives") is unmet. | patch |
| E27 | medium — §7 :710 keeps the 128 MiB side-record cap beside C4 :961 and [I-26]'s 193 MiB. | patch |
| E28 | low — [I-40] gives `FirstSendMembershipChangedHold` and `ProjectionPriorConflict` two outcomes each (:1341, :1332). | patch |
| E29 | medium — same defect as BH-12. | patch |



### Current integration review (2026-10-04)

The fresh blind and edge reviewers plus the independent investigator reused with the owner's explicit “reuse investigator” authorization reviewed the preserved snapshot in `6-5-integration/reviews/review-input.diff.gz`. The platform refused the third fresh spawn with `agent thread limit reached`; reuse preserves implementation independence and has prior context. Every individual finding is graded before grouping below. The smallest corrections use existing D1/D3/D8 rules, retained model algorithms and B6 bounds; they add no public field, lifecycle phase, record family or release lane.

| Finding | Verdict and verified evidence | Route / group |
| --- | --- | --- |
| BH-R1 | high — read_text normalizes CRLF before approval() receives bytes; §12 exact-byte protection is bypassed. | patch / P1 exact bytes |
| BH-R2 | medium — inputs() loops over matches without asserting the exact three child identities; deleting the rows passes. | patch / P2 complete source pins |
| BH-R3 | medium — inputs() checks manifest files but never parses the displayed D pin table; a false displayed digest passes. | patch / P2 complete source pins |
| BH-R4 | medium — The hold manifest is pinned but never consumed; removing a supplemental hold row is not detected. | patch / P3 retained inventory |
| BH-R5 | medium — D8 closes its reason/owner vocabulary while the supplemental table names retained predicates only in prose; an inventory encoder lacks exact mappings. | patch / P3 retained inventory |
| BH-R6 | medium — cursor_sign uses SHA256(prefix+payload); that historical model fixture cannot validate a correct HS256 implementation. Retain it as historical and add a standards-based positive vector. | patch / P4 cursor evidence |
| BH-R7 | medium — The exact payload has schema but no declared audience placement; identify its existing schema literal as the authenticated inventory audience. | patch / P4 cursor evidence |
| BH-R8 | medium — The model uses canonical [headerGeneration,subject/generation pairs], but D8 does not state the full preimage; separate implementations can diverge. | patch / P4 cursor evidence |
| BH-R9 | medium — The added adapter wording can imply a new native receipt family absent from D1; define its authenticated SQL readback using the already declared row/participant/backend authority. | patch / P5 adapter contract |
| BH-R10 | medium — Eight retries cap the count only; a row lock or statement can wait without bound. Apply the existing 30-second recovery budget and cancellation-to-hold rules. | patch / P5 adapter contract |
| BH-R11 | medium — Materializing predecessor/result images of eight legal 100 MiB envelopes violates B6 live-memory bounds; state bounded sequential streaming and qualification. | patch / P5 adapter contract |
| EC-R1 | medium — Read-only reviewer probes accept missing A/B/C pins and incorrect D pins, confirming BH-R2/R3. | patch / P2 complete source pins |
| EC-R2 | high — D3 deletes superseded evidence at fixed deadlines while amended C5 requests complete historical source bytes; unresolved operations cannot meet both rules. | patch / P6 D3/C5 seam |
| EC-R3 | medium — All three glob patterns also match -2.md execution records, which contain zero model blocks. Lexicographic enumeration reproduces refusal. | patch / P7 deterministic model paths |
| EC-R4 | high — This claim finding confirms the same D3/C5 historical-source conflict as EC-R2; D3 already requires authenticated rolling history and current attempts. | patch / P6 D3/C5 seam |
| VG-R1 | medium — The verification reviewer reproduced all three filesystem-order failures. This Other finding is independently confirmed by the exact glob and preserved -2 inputs. | patch / P7 deterministic model paths |

All seven groups route to direct correction of demonstrated states. No finding requires a new owner policy or modification of the frozen intent. The fixed D3 reclamation policy and immutable child sources stay intact; C5 must consume the existing authenticated rolling history rather than require deleted sources. Provider qualification and §12 human authority remain separate.

All 16 current findings are corrected. P1 validates raw LF/no-BOM receipt bytes and unique full-line framing before decoding; P2 checks the complete three A/B/C and 54 D pin rows; P3 accounts for all 17 baseline predicates with six D8 and 11 supplemental rows, exact existing owner mappings and unchanged D4 aggregate reasons. P4 states the existing schema audience and generation preimage and adds independent standard HS256 answers while preserving historical child fixtures. P5 requires fresh complete authenticated SQL readback under the existing owner, cumulative 30-second recovery deadline and 128 MiB live scratch bound. P6 consumes the retained active-window claim and authenticated rolling history without reclaimed sources. P7 uses exact immutable candidate paths. No review finding is deferred.

Parent full verification additionally exposed a historical-table parser collision: current review rows were counted as pass-1 identities. The original implementation agent restricted extraction to the original contiguous table and its exact 59 identities. Current rows and three added review rows are ignored; removing historical VG-1 is refused. The failed run and focused correction captures remain separate historical evidence. The subsequent full run passed all six required commands; the old child acceptance command still produced its expected correction-scope failure. See `6-5-integration/verification-results.json` for commands, exact capture hashes and review provenance.

## Design Notes

Use exact reviewed splices and fixed-revision citations, not a fresh design. The only normative document is AD-13: include its required schema/codec/bound/literal content within the hashed bytes, while referenced executable models are evidence with pinned sources. Amend superseded imported prose in place. Keep privately retired families historical; do not invent deployed migrations for them.

Parent verification must detect the demonstrated defects rather than mirror its own expected constants. Existing D constructors supply an independent implementation for D-owned literals; a separate constructor covers surviving parent preimages. Preservation applies to current authorized scope, while older correction-scope results retain their original revision attribution. Production cryptography, two-host PostgreSQL/broker crashes and deployment-profile authorization remain explicit provider gates.

## Verification

From repository root after a relevant change:

- `python3 _bmad-output/implementation-artifacts/6-5-integration/verify.py --mutations` — reviewed input pins, exact codecs/literals and independent known answers, approved content digest and corruption controls. Unrelated repository work is accepted.
- `python3 _bmad-output/implementation-artifacts/6-5d-simplification/verify.py` — reviewed bounded behavior cases.
- `python3 _bmad-output/implementation-artifacts/6-5d-simplification/mutations.py` and `python3 _bmad-output/implementation-artifacts/6-5d-simplification/reviews/focused-regressions.py` — existing regression and corruption controls.
- `python3 scripts/check-deferred-work.py --json` — consistent ledger classifications; the integration check verifies the 47 accepted open implementation/evidence follow-ups and all 20 obligations.
- `git diff --check` — whitespace validation.

These local checks plus the owner’s conversational approval finish Story 6.5. Earlier child acceptance and whole-repository preservation failures remain historical evidence. No .NET build or Aspire start is needed for this artifact-only amendment. Production provider qualification and runtime test requirements remain implementation obligations.


### Historical: 2026-10-04 integration execution before D-VALIDATION

The reviewed D1–D9 sections, complete wire schemas/known answers and immutable-input pins are integrated into AD-13. D-ARCH's focused AD-1/AD-26 handoff and exact application-owned metadata adapter contract are documented. D-RELEASE's existing current-main compatible maintenance/security workflow and complete-major/publication-hold policy are retained. No runtime, tests, CI, dependencies, submodules or Git state were mutated.

D-RESUME's old integration-created inventory/reconciliation mechanisms are superseded by the reviewed D2/D3 same execution-control owner, exact signed intent/window sequence and capsule-bound legacy recovery. D-SPLIT's historical integration block is superseded by the completed reviewed child and this exact import. The historical execution/review logs above remain verbatim. Their withdrawn O-06/O-07/O-11 and old capacity/codec/transition claims are superseded by the restored current-baseline boundary gate and D1/D4/D6 contracts; privately retired fixtures imply no deployed migration.

The current register gives 10 BH37 findings, all 59 pass-1 findings, all 54 child-routed plus 13 parent pass-2 findings, and all 20 O-rows one proposed disposition/owner/gate/evidence obligation. Exactly 47 pending integrated ledger status lines remain recognized open proposals. Five unrelated open entries, two earlier resolutions, D-SPLIT child closure, FW1 and RW1 remain unchanged. All receipt fields remain UNAPPROVED except the fixed scope; Story 6.5 remains in progress and Story 6.6 is unauthorized.

Focused check results, exact commands/exit codes, immutable source hashes and complete output captures are in [6-5-integration/verification-results.json](6-5-integration/verification-results.json). The historical child acceptance failure is preserved with its original correction checkpoint/pins, rather than repinned. Local models and index-width/UTF-8 checks supply no PostgreSQL, broker, cryptographic production-profile or runtime proof; D9 and AD-26 retain those gates.

Three formal review lenses are complete, with the independent investigator reused under the owner's explicit authorization. All 16 findings and the parent historical-table parser correction are addressed and independently checked. Local evidence and presentation are complete; the final execution checkbox stays open for the separate authenticated human §12 approval. Build-plan approval does not approve AD-13.

Final reviewed-and-corrected candidate normative-body SHA-256 (not approval): `b9e85ef0c637b5cf88af39a59d2896cbecaa7f45a259504187904b0dced3561a`. The pre-review and first failed post-review captures retain their original digests and are superseded only as current evidence. All six required commands passed on this final candidate; fourteen final output captures were independently byte/hash checked. Any candidate/executable change requires FW1 rerun and a fresh digest/approval.

The workflow's terminal presentation is complete within the authorized artifact-only scope. Its generic done/commit/sprint-update instructions do not override the frozen §12 gate or the approved preservation boundary: no commit or sprint-status mutation is made, Story 6.5 remains in progress, and Story 6.6 is unauthorized. The candidate awaits a named authenticated human, exact digest/source UTC, immutable approval capture, fixed scope and explicit authorization sentence under §12.


### Historical: 2026-10-04 targeted review corrections before D-VALIDATION

The review correction changes raw-byte receipt framing, complete displayed source-pin accounting, exact candidate selection, consumed preserved-predicate/closed inventory mappings, existing-schema inventory audience and independent HS256/generation vectors, bounded authenticated SQL readback, and C5 current-window/rolling-accumulator authority without reclaimed-history requirements. No child source/literal, prior reviewed snapshot, original verification result or prepatch run/review capture is rewritten. The corrected authorizing files and inline adapter/vector bytes have freshly regenerated current-content pins.

Only the edited seams were checked by `verify.py --review-fixes`, the existing Node parent constructor, and whitespace validation restricted to the edited files. Their separate evidence is [6-5-integration/review-fix-checks.json](6-5-integration/review-fix-checks.json). Full verification and final review/approval disposition belong to the parent workflow. Story 6.5 stays in progress, the six-field receipt stays UNAPPROVED except fixed scope, and Story 6.6 remains unauthorized.

Corrected normative-body SHA-256 (not approval): `7d9af4ba95ff8a7ae4b103fdadc8af2b6986bc24ae858e982843a494d36fbe42`.

- Final P6 precision: C5 authenticates the active window’s retained signed claim/carrier; reclaimed first-window claims are not required. The targeted rerun passes at corrected normative-body SHA-256 `245e4c59ad69034e0163a715e746f69a8aa9e142a1b7490f68ec6764f4d08b24` (not approval), captured separately in [review-fix-final-checks.json](6-5-integration/review-fix-final-checks.json). Earlier targeted captures remain unchanged historical evidence.

### 2026-10-04 owner-directed pragmatic validation amendment

D-VALIDATION is the current governing approval and validation policy. It supersedes the older whole-repository preservation and authenticated receipt requirements above; their prior executions and review logs remain historical. The technical event-evolution contracts, original inputs, production authentication/authorization, transaction/fence behavior and provider qualification stay unchanged.

The amendment changes only administrative validation: short owner approval policy and recorded receipt in AD-13; input-focused validation in `6-5-integration/verify.py`; matching generator policy and active register/ledger wording; this execution record; and the Story 6.5 entries in `epics.md` and `sprint-status.yaml`. Existing owner work in `.gitattributes`, packaging tests and `SecretsProtectionTests.cs` is preserved. No runtime, CI, dependencies, submodules or Git changes are made.

- [x] Replace the approval ceremony with the already stated owner's conversational approval and automatically recorded metadata.
- [x] Remove whole-repository freeze failures; retain actual input pins, byte/codec/known-answer checks and focused regressions.
- [x] Run the existing relevant local checks and one independent review with the reused investigator, preserve earlier evidence, and synchronize Story 6.5 completion.

No new owner decision or irreversible action is needed. The requested policy change authorizes these reversible edits without another approval round.

### Final completion under D-VALIDATION

Jérôme Piquot’s conversational approval accepts the reviewed technical design and the requested administrative validation amendment. Story 6.5 is done. Story 6.6 has an approved design, remains backlog and starts when the owner requests implementation.

The focused reused-investigator review found a remaining whole-ledger freeze and stale administrative approval wording. Both are corrected and independently confirmed with no remaining findings. Unrelated ledger additions/edits are accepted; the 47 owned follow-ups remain open and checked. All 20 implementation/provider verification obligations remain open. Technical contracts, signed fixture bytes and production gates are unchanged.

All six relevant commands passed on the final content: integration and corruption controls, D bounded verification, D mutations, D focused regressions, deferred-work consistency and whitespace. Exact results are in `6-5-integration/runs/pragmatic-20261004T141209Z/results.json`; the current summary is `6-5-integration/verification-results.json`, and the focused review is `6-5-integration/reviews/pragmatic-validation.md`. Historical summaries and captures are retained. The receipt’s digest is computed and checked by tooling; the owner has no hash or external evidence ceremony.

Only the Story 6.5 status and date are synchronized in sprint tracking, with comments unchanged. No Story 6.6 runtime work or Git mutation is performed.
