# Sprint Change Proposal: Split Story 6.5's integration-invented mechanisms into Story 6.5d

**Date:** 2026-09-30
**Status:** Approved by Administrator on 2026-09-30 for the backlog and story-file reorganization; implemented
**Scope:** Moderate backlog reorganization within Epic 6
**Review mode:** Batch (one complete proposal)

## 1. Issue summary

Story 6.5 integrates the reviewed 6.5a, 6.5b and 6.5c candidates into the single AD-13 artifact, `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md`. The children had deferred some refinements to integration, for example 6.5c's held-delivery redelivery exhaustion (BH15-11) and held-command discoverability (BH15-13). Closing those refinements forced the integration to invent new mechanisms: holds, waits, exits, capacity queues, legacy classification and an operator resume operation. They are written as `[I-nn]` integration rules, and no child story reviewed them.

Those rules have not converged:

- **Review pass 1** found 57 findings. It routed one intent gap (G-D: `PublicationRetryExhaustedHold` has no exit) and nine bad_spec groups (G-A…G-C, G-E…G-J), every one on an integration-authored rule. The owner answered G-D with D-RESUME, and the loop-1 re-derivation reworked the groups.
- **Review pass 2**, run on that re-derivation (whole-file SHA-256 `c474df76…b715`, §12 digest `7af8041c…`), returned 67 raw findings: VG2 10, BH2 19 and E2 38. They concentrate on the same mechanisms. The one finding verified so far, E2-34, shows that the new resume rule `[I-45]` re-arms nothing, because 6.5c C5's closure fences the whole operation namespace and `[I-45]` does not amend it.

On 2026-09-30 the owner recorded decision **D-SPLIT** in Story 6.5's frozen block. The invented mechanisms move to a focused child story, 6.5d, under the children's routing bar. Story 6.5 then imports 6.5d exactly as it imported 6.5a–6.5c, and it stays blocked until 6.5d is done.

- **Issue type:** failed approach. Designing new mechanisms inside a ~1 MB integration change does not converge, while the focused child stories did.
- **Evidence:** the Story 6.5 spec (D-SPLIT, the Review Triage Log, and Implementation Notes for 2026-09-30); `story-6-5-review-pass-2-findings.md`; commit `288a6190` (local when the proposal was drafted; an external process pushed it to `origin/main` with the submodule-bump commit `ede49537` before approval); and the open 6.5d entry at the end of `deferred-work.md`.

## 2. Impact analysis

| Artifact or area | Impact |
| --- | --- |
| Epic 6 | Add one lettered preparation story, 6.5d. The order becomes 6.5a, 6.5b, 6.5c, 6.5d, then the 6.5 integration and approval gate, then 6.6. No story is removed or renumbered, and Epic 6's delivery accounting is unchanged (6.5d is an enabler, not runtime value). |
| Story 6.5 | It stays the sole integration and approval owner, and it is blocked on 6.5d. Its dependencies and first AC widen from 6.5a–6.5c to 6.5a–6.5d. Its reconciliation text is out of date: it still describes the v37 draft with ten open findings. A new constraint says integration splices and cites but adds no new mechanism. |
| Story 6.6 | Its dependencies and preflight AC widen to 6.5a–6.5d. It stays `backlog` and unauthorized. |
| AD-13 candidate | Unchanged. The loop-1 `[I-nn]` text in `288a6190` becomes unapproved draft input to 6.5d. Its receipt stays `UNAPPROVED`. |
| PRD | No change. FR33-C5 keeps its single final owner, Story 6.5 (`prd.md:408`), and 6.5d supports it like 6.5a–6.5c. |
| Architecture | No change. AD-13 still requires one approved specification and vectors before runtime work. |
| UX | No change. Hold visibility is specified inside 6.5d's candidate; there is no new screen or flow. |
| Epic context and tracker | Add 6.5d to `epic-6-context.md` and `sprint-status.yaml` as `backlog`. Story 6.5 stays `in-progress` (the tracker has no `blocked` status; the block is recorded in `epics.md`), and 6.6 stays `backlog`. |
| Deferred-work ledger | No change. The 6.5d entry stays `open` until 6.5d closes. `scripts/check-deferred-work.py` exits 0 before this change. |
| Code, CI, deployment | None. No gate hashes `epics.md`, and the Epic 6 tracker rows are outside the guarded comment blocks. |

No other epic changes. Epic 7 may later present hold evidence, and Epic 8 stays optional.

## 3. Recommended approach

**Direct adjustment:** add Story 6.5d and rewire the 6.5 and 6.6 dependencies. D-SPLIT has already chosen this.

- **Rollback: rejected.** Reverting `288a6190` would discard the record 6.5d starts from and the pass-1 and pass-2 evidence.
- **MVP reduction: rejected.** FR33 still needs the evolution contract, and dropping the holds would bring back silent loss (NFR7).

**Effort:** medium, since 6.5d is a full specification story. **Risk:** medium. The mechanisms interact with 6.5a A8 and 6.5c C1–C6, and 6.5d must amend imported rules in place (E2-34) rather than contradict them. **Timeline:** 6.5d's build and review loops come before 6.5 can re-plan. That is shorter than more 6.5 integration loops, which went from 57 findings to 67.

### Owner decisions taken in this correct-course (2026-09-30)

- **Scope boundary (option "pass-1 groups + D-RESUME").** 6.5d owns:
  - these 14 integration rules: `[I-06]`, `[I-10]`, `[I-12]`, `[I-14]`, `[I-15]`, `[I-16]`, `[I-17]`, `[I-29]`, `[I-30]`, `[I-31]`, `[I-36]`, `[I-37]`, `[I-45]`, `[I-46]`;
  - the codecs and known answers those rules need;
  - the charge-record codec and counters of the `[I-29]` quota ledger, including the `resume-window` charge kind;
  - signing purpose `2d`;
  - every exit of a hold or wait state, wherever the hold's record is defined.

  Story 6.5 keeps every other `[I-nn]` rule (precedence, citation, header, bound, NFR12 and register rules) and all imports.
- **Deferral rule (option "completeness bar is AC").** The Story 6.5 completeness bar becomes 6.5d's acceptance criteria. The bar requires exits, charge and erasure rules, the activation slice, codecs with known answers and a verifier, grounded sources, and closed sets. A gap against it is therefore acceptance-breaking and must be fixed inside 6.5d. 6.5d may defer only what Story 6.5 can close by citation or as a Story 6.6 verification obligation. Story 6.5 integration never adds a new record, codec, state or exit; anything that would need one goes back to the owner.

## 4. Detailed change proposals

### 4.1 `epics.md`, Story 6.5: dependencies

**OLD:**
> **Dependencies:** Stories 6.5a, 6.5b, and 6.5c provide reviewed section candidates and dispositions. Current contracts, storage/replay/projection/subscription paths, and package compatibility remain inputs. Epic 8's optional protection engine is not a prerequisite.

**NEW:**
> **Dependencies:** Stories 6.5a, 6.5b, 6.5c, and 6.5d provide reviewed section candidates and dispositions. Story 6.5 integration splices and cites them and adds no new record, codec, state, or exit; a gap that would need one returns to the owner (decision D-SPLIT, 2026-09-30). Current contracts, storage/replay/projection/subscription paths, and package compatibility remain inputs. Epic 8's optional protection engine is not a prerequisite.

**Rationale:** D-SPLIT, plus the lesson that integration-invented mechanisms do not converge.

### 4.2 `epics.md`, Story 6.5: current reconciliation and first AC

**OLD:**
> **Current reconciliation:** Story 6.5 remains in progress and `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` remains an unapproved draft. `IEventContract.EventType` supplies a stable kebab-case discriminator, but persisted/wire events remain CLR-name-oriented without a payload schema version or shared upcaster, and published cancellation seams remain inconsistent. The v37 review has ten open findings (`BH37-1` through `BH37-10`), routed to the focused child stories. The draft does not grant completion or implementation authority; Story 6.5 may remain in progress while it is reviewed.

**NEW:**
> **Current reconciliation:** Story 6.5 remains in progress and is blocked on Story 6.5d. `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` is the loop-1 integration of 6.5a–6.5c (commit `288a6190`) and remains an unapproved draft with an `UNAPPROVED` receipt. `IEventContract.EventType` supplies a stable kebab-case discriminator, but persisted/wire events remain CLR-name-oriented without a payload schema version or shared upcaster, and published cancellation seams remain inconsistent. Review pass 1 routed nine bad_spec groups and one intent gap to integration-invented rules, and review pass 2 returned 67 raw findings on the same mechanisms, so the owner moved them to Story 6.5d (D-SPLIT, `sprint-change-proposal-2026-09-30.md`). The draft does not grant completion or implementation authority.

In the first acceptance criterion:

**OLD:** `**Given** Stories 6.5a–6.5c have reviewed outputs`
**NEW:** `**Given** Stories 6.5a–6.5d have reviewed outputs`

**Rationale:** the old text describes the v37 state, which two integration passes have superseded.

### 4.3 `epics.md`: new Story 6.5d, inserted after Story 6.5c

**NEW:**

> ### Story 6.5d: Hold Lifecycle, Resume, and Legacy Admission Spec
>
> As a platform maintainer, I want every hold, wait, and legacy-admission mechanism that event evolution introduces to have a designed exit, bounded storage, activation slice, and checked codec so that no command, delivery, or pin is stranded and Story 6.5 integration invents nothing.
>
> **Requirements coverage:** Supporting FR33-C5 delivery/recovery design and NFR7 no-silent-loss and NFR12 compatibility planning; Story 6.5 retains final approval ownership. **Classification:** Specification work; no runtime capability or independent 6.6 authorization.
>
> **Dependencies:** Stories 6.5a–6.5c's reviewed candidates; the loop-1 integration rules `[I-06]`, `[I-10]`, `[I-12]`, `[I-14]`–`[I-17]`, `[I-29]`–`[I-31]`, `[I-36]`, `[I-37]`, `[I-45]`, and `[I-46]` in the unapproved AD-13 candidate at `288a6190`, as draft input only; owner decision D-RESUME. **Deliverable:** Reviewed section candidate, integration handoff, and dispositions for review-pass-1 groups G-A…G-J (6.5d-owned parts) and the 54 review-pass-2 findings routed here, in `spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md`.
>
> **Acceptance Criteria:** Every hold or wait state has a stated exit; every stored record has a charge or ceiling and a tenant offboarding/erasure rule; every rule names its activation slice; every new record or codec has tags and an independently recomputed known answer checked by an embedded verifier; every reason-code or outcome set is closed for every raiser. The D-RESUME operation re-arms the same committed events under the same MessageId without re-executing the command, and amends the imported 6.5c closure fence in place. Refinements are deferred only where Story 6.5 can close them by citation or as a Story 6.6 verification obligation. No runtime edits or self-approval occur.

### 4.4 `epics.md`, Story 6.6: dependencies and preflight AC

**OLD:** `**Dependencies:** Stories 6.5a, 6.5b, and 6.5c must have completed their reviewed specification work, and Story 6.5 must be complete …`
**NEW:** `**Dependencies:** Stories 6.5a, 6.5b, 6.5c, and 6.5d must have completed their reviewed specification work, and Story 6.5 must be complete …` (the rest of the paragraph is unchanged)

**OLD:** `**When** Stories 6.5a–6.5c and the Story 6.5 artifact and approval are inspected` / `**Then** the three focused specification stories have reviewed outputs, …`
**NEW:** `**When** Stories 6.5a–6.5d and the Story 6.5 artifact and approval are inspected` / `**Then** the four focused specification stories have reviewed outputs, …`

### 4.5 Routing of the 67 review-pass-2 findings

Each finding goes to the story that owns the rule it names. The findings are still raw: the owning story's triage must verify each one before acting on it. Only E2-34 is verified.

| Owner | Findings |
| --- | --- |
| **6.5d (54)** | VG2-2, VG2-3, VG2-4, VG2-O1, VG2-O2, VG2-O3; BH2-1 to BH2-10, BH2-16, BH2-18, BH2-19; E2-1 to E2-26, E2-28 to E2-32, E2-34, E2-35, E2-36, E2-38 |
| **6.5 (13)** | VG2-1 (`[I-11]` table verifier), VG2-5, VG2-6, VG2-7, BH2-17 (verifier construction); BH2-11 (ledger status wording, O-list tracking); BH2-12 (named-approval completion condition); BH2-13 (reproducible re-derivation base); BH2-14 (`[I-47]` vs D-CLOSE); BH2-15 (`[I-41]` release hold); E2-27 (`[I-28]` attach prose); E2-33; E2-37 (filter-helper BC row) |

The scope option chosen by the owner quoted a 55 to 12 split. Checking each finding against rule ownership moved one finding back to Story 6.5: E2-27 is a prose fix to `[I-28]`, and `[I-28]` stays with Story 6.5.

Pass-1 group G-J (integration codec known answers) splits the same way. The codecs of `[I-12]`, `[I-31]` and `[I-37]` go to 6.5d; those of `[I-08]`, `[I-09]` and `[I-33]` stay with 6.5.

### 4.6 New story file `_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md`

The story file is about 50 lines, in the same form as the 6.5a–6.5c files created on 2026-09-27: status `backlog`, Intent, Boundaries & Constraints, Inputs, Tasks & Acceptance, and Verification. It carries the scope boundary and deferral rule from §3 and the 54 routed finding IDs. It also records that the loop-1 `[I-nn]` text is unapproved input, not authority. `/bmad-build 6.5d` turns it into the candidate.

### 4.7 `epic-6-context.md`

- Add `- Story 6.5d: Hold Lifecycle, Resume, and Legacy Admission Spec` after the 6.5c entry under Stories.
- Replace the last Cross-Story Dependencies bullet with:
  > Stories 6.5a, 6.5b, 6.5c, and 6.5d provide reviewed candidates for one normative Story 6.5 artifact. The writer/identity contract feeds verified reads; both feed publication and rollout; 6.5d designs the hold, resume, capacity, and legacy-admission mechanisms the first three left to integration. Story 6.5 splices and cites the four candidates without inventing mechanisms, closes the review findings, and obtains exact-content human approval before Story 6.6 can start.

### 4.8 `sprint-status.yaml`

- Insert `6-5d-hold-lifecycle-resume-and-legacy-admission-spec: backlog` after the 6.5c row. The key is derived by the pinned sprint-planning parser's `_slug` rule from the new heading.
- Set `last_updated` to `2026-09-30`, in both the header comment and the field.
- No other row changes. Story 6.5 stays `in-progress`, and the guarded blocks are untouched.

### 4.9 Story 6.5 file (`spec-6-5-event-versioning-and-upcasting-spec.md`), Implementation Notes only

The frozen block, which already records D-SPLIT, is not touched. Append one bullet:

> - 2026-09-30 correct-course (`sprint-change-proposal-2026-09-30.md`). Story 6.5d is created as `backlog`. It owns `[I-06]`, `[I-10]`, `[I-12]`, `[I-14]`–`[I-17]`, `[I-29]`–`[I-31]`, `[I-36]`, `[I-37]`, `[I-45]`, `[I-46]` and every hold or wait exit; this story keeps the other `[I-nn]` rules and may add no new mechanism. Of the 67 pass-2 findings, 54 go to 6.5d and 13 return to this story's next plan: VG2-1, VG2-5, VG2-6, VG2-7, BH2-11 to BH2-15, BH2-17, E2-27, E2-33 and E2-37. The loop-1 re-derivation noted above as uncommitted is committed as `288a6190`, now on `origin/main`.

## 5. Implementation handoff

**Classification: Moderate.** This is a backlog reorganization inside Epic 6 and needs no PM or architect replan.

- **Product Owner / Developer (this correct-course, on approval):** apply §4.1–§4.9, then run the focused checks:
  - the new key is present and existing statuses are preserved;
  - `sprint_plan.py validate` reports only its three known pre-existing issues;
  - `scripts/check-deferred-work.py` exits 0;
  - the Contracts.Tests sprint-status guards are green;
  - `git diff --check` is clean.
- **Developer / architect (`/bmad-build 6.5d`):**
  - triage and verify the 54 routed findings;
  - design the mechanisms to the completeness bar;
  - amend imported rules in place, starting with C5's closure for D-RESUME (E2-34);
  - deliver the candidate and an integration handoff that names which `[I-nn]` text each section replaces.
- **Developer / architect (Story 6.5 re-plan, after 6.5d is done):**
  - import 6.5d;
  - handle the 13 findings routed back;
  - re-check the loop-1 artifact;
  - restore the named-approval completion condition (BH2-12).
- **Owner (sole maintainer):** approves the exact AD-13 bytes through the six-field receipt, which alone authorizes Story 6.6. This proposal's approval is not that approval.

**Success criteria:**

1. Epic 6, its context file and the tracker agree on 6.5a–6.5d → 6.5 → 6.6.
2. 6.5d has a bounded scope, a deferral rule and a list of routed findings.
3. No normative text, receipt, child candidate or history file changes.
4. 6.6 stays unauthorized.

## 6. Change-navigation checklist

| Section | Status and finding |
| --- | --- |
| 1. Trigger and evidence | [x] D-SPLIT; pass-1 groups; 67 pass-2 findings; E2-34 confirmed; commit `288a6190` (now on `origin/main`). |
| 2. Epic impact | [x] Epic 6 remains viable with one more preparation story. No other epic is affected, obsolete or resequenced. |
| 3. Artifact conflicts | [x] PRD, architecture and UX: N/A. Epics, epic context, tracker and the 6.5 Implementation Notes need edits. Ledger and CI: none. |
| 4. Path forward | [x] Direct adjustment. Rollback and MVP reduction were rejected. |
| 5. Proposal components | [x] §1–§5 above. The scope boundary and deferral rule were decided by the owner in this session. |
| 6. Final review and handoff | [x] The Administrator approved the complete proposal in conversation on 2026-09-30. `epics.md`, the epic context, the tracker, the Story 6.5 Implementation Notes and the new 6.5d story file were updated as §4 describes. |
