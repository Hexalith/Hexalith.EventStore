---
title: 'Story 6.5: Event Versioning And Upcasting Spec'
type: 'feature'
created: '2026-09-26'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 'ccb4faf03256ef8eb627d49d6f6f0a032fcb4830'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Stable `IEventContract.EventType` exists, but persisted events and consumers still use CLR names without a payload schema version. Identity checks and cancellation behavior differ across append, replay, projection, and subscription paths, leaving event evolution unsafe to implement without a frozen contract.

**Approach:** Produce `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` as the versioned AD-13 gate. Inventory the current paths, fix the future metadata, registry, upcast, identity, failure, cancellation, and migration contracts, then obtain content-bound human approval that explicitly authorizes Story 6.6. This story changes no runtime behavior.

## Boundaries & Constraints

**Always:** Specify exact field names, types, defaults, grammar, uniqueness, legacy version, and mapping for canonical kebab-case identity; distinguish metadata-envelope, domain-service, and payload versions. Preserve stored bytes, `MessageId`, sequence, correlation, protection metadata, and actor-owned commits. Require one shared allow-listed read pipeline and pre-append/pre-dispatch identity checks. Define numeric chain/payload limits, typed failures, checkpoint/last-good-state and cancellation/commit behavior, additive legacy adapters, mixed-version rollout/rollback, provider-portable evidence, support-safe diagnostics, and a closed 6.6 slicing decision.

**Never:** Implement 6.6, edit runtime/tests/public contracts, change `sprint-status.yaml` manually, rewrite history on read, guess unknown types, load arbitrary types, silently complete poison deliveries, claim AOT/trimming support, or depend on Epic 8 protection. Do not self-approve or authorize 6.6 without named human approval, date, exact digest, and explicit authorization.

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
</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` -- the only normative target: the v37 draft, 1,008 lines, LF enforced by `.gitattributes` `*.md eol=lf`. Section starts:

  | § | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 |
  |---|---|---|---|---|---|---|---|---|---|---|---|---|
  | Line | L14 | L34 | L62 | L88 | L122 | L149 | L251 | L372 | L396 | L450 | L465 | L992 |

  - §10's four 6.6 slices are at L456–459.
  - §11 has Node verifiers at L628–797 and L864–984. They read this exact path and need unique `^V17…=`, `^V20…=` and `^V23…=` lines.
  - The §12 receipt marker is at L1002.
  - The L12 claim "no deferred implementation decisions" and the L496 claim "Open decisions: none" must end up true.
  - L301, L480 and L988 pin the `000f` lease codec, the V17/V20/V22/V23 bytes and the six-field `UNAPPROVED` receipt.
- `_bmad-output/implementation-artifacts/spec-6-5a-event-contract-writer-and-migration-evidence.md` -- A1–A10 replace or add to draft §§1–5 and 7–11 (L39):
  - A3 limits (L85) are superseded by 6.5b B6 (BH37-4).
  - A5 (L126): codec-02 intent, pre-save and receipt, plus `ActorBundleReadbackHash`.
  - A6 (L168): corrupt-event record. A7 (L178): no-op witness.
  - A8 (L199): the codec-03 outcome. It replaces the §7 retry rule and the §11 Review-32 vector, and it narrows §7 L323 to cross-tenant.
  - A9 (L267): §9/§10.
  - A10 (L291): V01–V24 and K01–K12.
  - The BH37 table is at L297–301; the model is at L361–902.
- `_bmad-output/implementation-artifacts/spec-6-5b-verified-read-replay-and-projection.md` -- its L39 scope understates the change: §6 is rewritten well beyond the timeline paragraphs.
  - B2 (L64) replaces §3 L84.
  - B4–B4b (L112–155) add the purpose-12 selector, with expiry at 864,000,000,000 ticks.
  - B5 (L169) changes §6 L223.
  - B6 (L181) sets the §8 budgets and scopes §6 L227/231 and L247.
  - B7/B7c (L211–287) add §5 rows `5a` and `5b`. B8 is at L305 and B9 (§9) at L320.
  - B10 (L343) holds the BH37 table at L349–352.
  - It adds 13 typed outcomes the draft lacks. The handoff is at L1607–1620 and the model at L408–1605.
- `_bmad-output/implementation-artifacts/spec-6-5c-publication-subscription-and-rollout.md` -- the handoff at L3491–3497 is the authoritative integration checklist.
  - C1 (L43): `destinationId`, the tenant and deployment retention ceilings, and attestation 1..6,123 B / key ID 1..738 B. These replace the 8 KiB bound at draft §7 L281 (twice) and §8 L392, not in §6.
  - C3 (L119): an 8-field `EventEffectKey` and a 16 KiB receipt, replacing §7 L352 and L356.
  - C4 (L199): the 23-field manifest, replacing §7 L287.
  - C5 (L249): replaces the codec-01 outcome at §7 L358 and L366.
  - C6 (L322): the activation checklist, plus 11 public surfaces and the repository-search floor (L339).
  - C7 (L345): non-normative vectors. BH37-9 is at L69.
  - The integrity script at L4116–4226 pins the old draft, so it fails by design after this change.
- `_bmad-output/implementation-artifacts/story-6-5-review-triage.md` L546–555 -- BH37 rows. Cite them; never edit them.
- `_bmad-output/implementation-artifacts/deferred-work.md` L4927–5192 -- 54 child entries:
  - 2 resolved: L5003 and L5035.
  - 5 non-story, left open: L4931, L4935, L4939, L5043, L5047.
  - Evidence and tooling: L4959, L4975, L5039, L5102–5114, L5122, L5137, L5167, L5189.
  - Everything else is contract-level.
- **Owner decisions not to reopen:**
  - 6.5a: the routing bar; one scope per execution MessageId (tags 01–07); the first POST reply never waits; the `Completed` `resultPayload` gate.
  - 6.5b: entry-free lists need no provider; the 1,000-event timeline cap is disclosed; two-level storage; selector expiry ≤24 h.
  - 6.5c: the pin is charged at the pin CAS; a capacity-held pin returns `CommandOutcomeHold`, at tenant and deployment level only.
- **Do not change:**
  - `src/`, `tests/`, `docs/`, `tools/` and `.github/`;
  - `sprint-status.yaml` and `epics.md`;
  - the child candidates and their `-2` records;
  - `story-6-5-*` history;
  - the §12 hash rule and its `UNAPPROVED` values.

## Tasks & Acceptance

**Execution (review loop 1 re-derivation):**
- [x] `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` and `_bmad-output/implementation-artifacts/deferred-work.md` -- Start from the pre-review integration: `/tmp/claude-1000/-home-administrator-projects-hexalith-eventstore/3517cf94-2fd6-4d7a-865b-4f03f9f21b4e/scratchpad/backup-pre-review/spec-event-versioning-upcasting.md` (SHA-256 `d5a14515…41d2`) and `/tmp/claude-1000/-home-administrator-projects-hexalith-eventstore/3517cf94-2fd6-4d7a-865b-4f03f9f21b4e/scratchpad/backup-pre-review/deferred-work.md` (SHA-256 `80604156…2321`). Verify both hashes before copying. -- KEEP (Spec Change Log, loop 1).
- [x] same artifact -- Apply every `patch` row of review pass 1, each with the smallest text change that removes the defect. The rows are VG-1, VG-2, VG-O1/BH-6a, BH-1, BH-3, BH-11, BH-12/E29, BH-13, BH-19, BH-20, BH-21/E19, BH-22, E1, E2, E3, E6, E12, E13, E14, E21, E24, E26, E27 and E28. -- These are verified defects with direct fixes.
- [x] same artifact -- Rework the rules named by bad_spec groups G-A (legacy scope inventory [I-12]), G-B (drain limit [I-14]), G-C (hold reason codes [I-10]/[I-16]), G-E (pin-capacity queue [I-31]), G-F (long-stream growth [I-06]), G-G (schemas and capability fields [I-17]/[I-29]/[I-30]), G-H (redrive [I-36]), G-I (hold inventory [I-37]) and G-J (integration codec known answers). Every reworked rule must satisfy the Design Notes completeness bar. -- Pass 1 found states with no exit, impossible actions and unchecked codecs.
- [x] same artifact -- Specify the D-RESUME operation as `[I-nn]` rules:
  - operator authority and signing purpose;
  - request and audit record codecs, with known answers;
  - eligibility (the retry-exhausted hold, the drain-limit hold, and reconciled legacy status-6 records);
  - the exact re-armed attempt, with the same committed events and MessageId, and never a command re-execution;
  - hold-inventory removal;
  - its §10 slice and §10.3 class;
  - the client polling signal while a hold waits for an operator.

  Register it in §11.5, and point the G-B drain-limit hold and the G-D hold at it. -- Owner decision D-RESUME.
- [x] same artifact -- Update §11.5, §11.7 and the ledger status lines to match the reworked rules. Keep §12 and the receipt `UNAPPROVED`, and record the recomputed digest in Implementation Notes. -- One consistent register; approval stays human-only.

**Acceptance Criteria:**
- Given the three handoffs, when each obligation is traced, then it lands in exactly one normative location, no superseded rule survives beside its replacement, and every public, wire or storage change carries its NFR12 class.
- Given BH37-1..10, the ledger entries and the pass-1 triage rows, when the register and the artifact are read, then each item has one disposition, every `patch` row is fixed, every G-group rule meets the completeness bar, and no decision is deferred into Story 6.6.
- Given the re-derived file, when the Verification commands run, then all of them pass (including the new verifier blocks), the receipt is still `UNAPPROVED`, and the only files that differ from the baseline are the task files plus the two other-owner paths listed under Verification.

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

## Design Notes

- **Completeness bar for every `[I-nn]` rule (review loop 1).** Each rule must satisfy all of the following:
  - **Exits.** Every state the rule creates has a stated exit, whether a resolution, a terminal outcome or the D-RESUME operation. Every indefinite or operator-gated hold is admitted to the [I-37] inventory by predicate.
  - **Storage.** Every stored record has a charge or ceiling and a tenant offboarding and erasure rule.
  - **Activation.** It names the §10 slice that activates it.
  - **Codecs.** Every new record or codec has tags and a §11.6 known answer produced and checked by an embedded `bash` verifier block, which Verification command 1 then runs.
  - **Sources.** It reads only sources that exist at `ccb4faf0` or records this document defines.
  - **Closed sets.** Every reason-code or outcome set it adds is complete for every raiser.
- **Imported rules.** A rule a child imported is amended in place, never contradicted elsewhere.

- **Form.** Everything stays in one file. Normative rules, codecs and known answers go inline. The child models are cited, not copied: they verify themselves at their own paths, and copying them would add about 2,300 lines and break their extraction. The expected size is about 0.9–1.2 MB.
- **Splicing.** Integrate section by section, largest first (§7). Splice with scripts rather than retyping, so the imported bytes stay exact. Keep LF line endings.
- **Tagged rules.** A tag `[I-nn]` marks each rule first decided at integration. The approver and the review can then focus on exactly the rules no child story reviewed.
- **Wording.** Use sole-owner vocabulary: never "independent". The 6.5c "Independent review should challenge" items become review focus points and 6.6 provider-evidence obligations.
- **After the build.** The owner fills in the receipt from an authenticated immutable capture, for example a GitHub-verified signed commit. The 6.5 tracker row stays `in-progress` until then.

## Verification

**Commands:**
- `python3 -c 'import re,subprocess,pathlib; t=pathlib.Path("_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md").read_text(); b=re.findall(r"^```bash\n(.*?)^```$",t,re.S|re.M); [subprocess.run(["bash","-c",x],check=True) for x in b]; print(len(b),"bash blocks passed")'` -- expected: exit 0, and the last line before the summary is a 64-hex digest.
- `grep -c '^<!-- APPROVAL RECEIPT: mutable fields below -->$' _bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` -- expected: `1`. The last six lines keep five `UNAPPROVED` values plus the fixed scope.
- `for i in $(seq 1 10); do grep -q "BH37-$i\b" _bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md || echo "missing BH37-$i"; done` -- expected: no output.
- The 6.5a, 6.5b and 6.5c model commands from their Verification sections, run against the unchanged candidates -- expected: exit 0.
- `git diff --check ccb4faf03256ef8eb627d49d6f6f0a032fcb4830` and `git diff --name-only ccb4faf03256ef8eb627d49d6f6f0a032fcb4830` -- expected: clean; the name-only list holds the three task files plus `references/Hexalith.Builds` and `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.cs` (other-owner commits `5f854425`, `27279fe6`).
