---
title: Story 3.15 bounded closure and Phase 4 gate-control ownership
date: 2026-09-26
type: sprint-change-proposal
status: approved-for-implementation
scope: moderate-backlog-reorganization
readiness_effect: none
sprint_status_effect: proposed
supersedes_without_rewriting:
  - _bmad-output/implementation-artifacts/spec-3-15-tracker-reconciliation.md (Decision 2026-09-24)
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-09-23.md (§4.E, Story 3.15 row only)
---

# Story 3.15 Bounded Closure And Phase 4 Gate-Control Ownership

## 1. Issue Summary

**Trigger.** Story 3.15 (`3-15-corrected-deployed-runtime-parity-closure`) cannot reach a common `done` status. Its tracker row waits on a control that no story owns.

- The 2026-09-24 Decision in `spec-3-15-tracker-reconciliation.md` keeps the tracker at `review` "while the independent G-HIGH-RISK control is absent".
- The 2026-09-23 proposal (§4.E, row 3.15) requires the "G-HIGH-RISK control" before a common status.
- G-HIGH-RISK (PRD §11.4, OR10 and OR13) requires four things: a gate risk matrix, its validator, sealed CI validation, and an authenticated second identity independent from the author.
- No story in `epics.md` names G-HIGH-RISK, OR10, OR13, or OR28.

**Category.** A requirements misunderstanding in the lifecycle binding, plus an ownership gap. A story's lifecycle was tied to a cross-cutting authority gate that has no owner.

### Evidence re-checked 2026-09-26 at `818e28a8` (`main` == `origin/main`)

| Source | Observation |
| --- | --- |
| Retained validator | `python3 tools/validate-corrected-deployed-runtime-parity.py …/f343bb01…/closure.json --packet-root …/f343bb01…` exit 0: `subject=sha256:66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6 selected=sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`. |
| `closure.json` authority flags | `publication_authorized`, `deployment_authorized`, `consumer_removal_authorized`, `grants_mutation_authority` are all `false`. |
| Role registry | `eventstore-owner` and `release-owner` → `github:jpiquot`; `test-architect` → `bmad:murat`, a tool persona rather than an authenticated person. The 2026-08-25 roster ratification (issue #352) says it authorizes "no … Story 3.15 done status". |
| Subject inventory | Binds only `tools/` producer, handler, assembler, and verifier files. Tracker, PRD, epics, specs, wrapper, and Contracts tests are **not** subject inputs, so editing them does not re-mint the subject. |
| PRD §7.1 | Maps exactly one clause to Story 3.15. FR36-C2: "Deployed-runtime parity passes the exact three-receipt validator on one unchanged evidence candidate; candidate publication alone grants nothing." That condition is met. |
| PRD §11.4 | "A story label or local documentation edit cannot change a gate result." G-RUNTIME-PARITY is **TECHNICAL PASS; INDEPENDENT GATE BLOCKED**, and G-HIGH-RISK is **FAIL/BLOCKED**. |
| PRD OR10 deadline | "Before any high-risk evidence is used to authorize `READY`, release, deployment, or migration". The deadline is tied to authority, not to a story's lifecycle label. |
| PRD §0 / OR28 | A corrective story that closes a failed gate needs a content-bound authorization record plus `tools/validate-corrective-work-authorization.py` before handoff. Neither the validator nor an owning story exists. |
| `epics.md` | No story owns G-HIGH-RISK, OR10, OR13, or OR28. |

## 2. Impact Analysis

### Epic impact

- **Epic 3.** Stays `in-progress` because Story 3.16 is `backlog`. Closing 3.15 at its bounded scope does not close Epic 3, FR36, or SM6.
- **New Epic 9** (backlog) is needed. G-HIGH-RISK lists 17 mandatory gates and blocks all of them, so it is not a Story 3.15 concern. Hosting it in Epic 3 would tie Epic 3's closure to cross-cutting governance. Its §0 prerequisite (OR28) has no owner either.
- **Epics 1, 2, and 4–8.** No scope change. Epic 8 stays post-MVP. The MVP boundary rule is unaffected, because Epic 9 does not depend on Epic 8.

### Story impact

- **Story 3.15.** The tracker moves `review` → `done` for FR36-C2 evidence validation only. The spec stays `done`. No packet, acceptance, or `tools/` byte changes.
- **New Story 9.1** (OR28) and **Story 9.2** (G-HIGH-RISK: OR10 and OR13) are added in backlog.

### Artifact conflicts

- **PRD.** The Story 3.15 lifecycle wording in §6.8, §11.3, §11.4 (G-RUNTIME-PARITY), and §12 (OR15) changes. Owner pointers are added to G-HIGH-RISK, OR10, OR13, and OR28. Every gate result is unchanged.
- **Architecture.** No edit (see §4.F).
- **UX.** Not affected.
- **Secondary artifacts.** The sprint-status GUARDED fence, two Contracts tests, the 3.15 wrapper and spec status prose, and a dated note in the tracker-reconciliation spec.

### Technical impact

- No production code, `tools/`, CI workflow, or `docs/ci.md` change.
- Contracts.Tests pins must move in one commit.

### Option A rejected (checklist §4.1, assessed as the direct-adjustment path)

- Handing off the G-HIGH-RISK story requires OR28 first, under PRD §0.
- G-HIGH-RISK is itself one of the 17 gates it classifies, so approving it also needs the independent second person.
- The path is OR28 story → G-HIGH-RISK story → a named second human → sign-off on 3.15. No second identity has been named.
- The added protection is zero, because the gate already fails closed whatever the story label says.
- Effort: High. Risk: Low. Timeline: open-ended.

### Rollback (§4.2) not viable

The evidence is valid. Nothing is simplified by reverting it.

### MVP review (§4.3) not needed

No FR or NFR scope changes. G-HIGH-RISK stays mandatory for `READY`.

## 3. Recommended Approach

**Direct adjustment. The owner renegotiates the 2026-09-24 Decision and registers backlog owners** (the owner selected Option "B + backlog owners" on 2026-09-26).

### Dated decision (2026-09-26), to be recorded verbatim

> The Story 3.15 owner renegotiates the 2026-09-24 Decision. Story 3.15 closes `done` at its bounded scope: PRD §7.1 clause FR36-C2 technical evidence validation only, on subject `66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6` with selected index `sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`.
>
> **What `done` does not close.** It closes no NFR11, NFR12, or NFR16 clause and no G-RUNTIME-PARITY, G-HIGH-RISK, FR36, or SM6 result. It grants no `release-available`, `production-promoted`, READY, deployment, or consumer-removal authority. All four `closure.json` authority flags stay `false`.
>
> **Identity caveat.** Both owner roles map to `github:jpiquot`, and the Test Architect record is self-attested. The three receipts are not three-party review.
>
> **Where G-HIGH-RISK goes.** It stays FAIL/BLOCKED as a Phase 4 gate owned by backlog Story 9.2, whose handoff depends on Story 9.1 (OR28).
>
> **Reopen triggers.** Story 3.15 returns to `in-progress` if any of these happens:
> - the retained validator exits non-zero;
> - the subject is re-minted;
> - an independent G-HIGH-RISK evaluation of G-RUNTIME-PARITY rejects the evidence.
>
> **Historical records.** This decision supersedes, without rewriting, the 2026-09-24 Decision and the Story 3.15 row of the 2026-09-23 proposal §4.E. Both remain as historical records.

### Rationale

- The PRD's own clause ledger (§7.1) and gate contract (§11.4) already separate story labels from gate results.
- The safety the 2026-09-24 hold intended is provided by G-RUNTIME-PARITY and G-HIGH-RISK staying blocked. Both are guarded by `PlanningRuntimeParityAccountMatchesCurrentPacketAndPendingControl`.
- Registering Epic 9 removes the ownership gap that caused the deadlock.

**Effort:** Low for this change: one docs and test commit. Stories 9.1 and 9.2 are separate, unestimated corrective work.

**Risks and mitigations:**

| Risk | Mitigation |
| --- | --- |
| A reader might treat `done` as independent approval. | The verbatim guarded tracker block, the PRD gate rows, and the PRD guard keep `INDEPENDENT GATE BLOCKED` asserted. |
| OR13 ("done against a high-risk NFR"). | 3.15 is primary only for NFR12, which is outside SM3's high-risk set. NFR11 and NFR16 are supporting-only and not closed. |

## 4. Detailed Change Proposals (batch)

All edits below land in **one commit**, because the four 3.15 lifecycle pins must move together. No edits touch `tools/`, the frozen 3.15 packet or acceptance bytes, `docs/ci.md`, frozen spec blocks, or unrelated GUARDED blocks. `sprint-status.yaml` is edited in place, not regenerated.

### A. `_bmad-output/implementation-artifacts/sprint-status.yaml`

**A1. Story 3.15 GUARDED fence, lines 153–157.** BEGIN and END stay exactly once. BEGIN stays above the caveat. The caveat and the pointer lines are unchanged. The pointer's "next four lines" count still holds.

OLD:
```yaml
  # 2026-09-26: three current roster-bound receipts validate the corrected
  # subject; the verifier exits 0 and selects only the pinned OCI index.
  # The spec is done for bounded evidence validation, while this row remains
  # review pending G-HIGH-RISK and later authority gates.
  3-15-corrected-deployed-runtime-parity-closure: review
```
NEW:
```yaml
  # 2026-09-26 owner decision (sprint-change-proposal-2026-09-26) closes this row at FR36-C2
  # evidence validation only and supersedes the 2026-09-24 review hold without rewriting it.
  # G-RUNTIME-PARITY and G-HIGH-RISK stay blocked; backlog Story 9.2 owns G-HIGH-RISK. The
  # row grants no release, promotion, deployment, readiness, or consumer-removal authority.
  3-15-corrected-deployed-runtime-parity-closure: done
```

**A2. Append after `epic-8-retrospective: optional`**, with a blank line first:
```yaml

  epic-9: backlog
  # Story 9.1 (OR28) must pass before Story 9.2 can be handed off under PRD section 0.
  # Story 9.2 cannot close until a named human reviewer independent of github:jpiquot approves.
  9-1-corrective-work-authorization-record-and-validator: backlog
  9-2-phase-4-high-risk-gate-matrix-and-non-authorship-control: backlog
  epic-9-retrospective: optional
```
`epic-3: in-progress` is unchanged.

### B. `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs`, `CorrectedLifecycleRowsRetainTheirCorrectedStatus`

- **B1. Comment.** Replace "The spec is done, while the tracker remains in review until G-HIGH-RISK supplies independent control." with "The spec is done, and a dated 2026-09-26 owner decision closed the tracker at its bounded FR36-C2 scope; G-HIGH-RISK stays blocked in the PRD and is owned by Story 9.2, so this pin keeps the bounded wording glued to the row."
- **B2. Verbatim block.** Replace its last five lines (the four dated lines and the row) with the A1 NEW text exactly. The caveat, pointer, and END lines are unchanged.
- **B3. Row pin.** `SingleLineValue(sprint, "  3-15-corrected-deployed-runtime-parity-closure:").ShouldBe("review")` → `.ShouldBe("done")`.
- **B4. Spec pin.** `FrontmatterValue(story315Spec, "status").ShouldBe("'done'")` is unchanged.

### C. `tests/…/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs`, `PlanningRuntimeParityAccountMatchesCurrentPacketAndPendingControl`

OLD: `parityGate.ShouldContain("tracker remains `review`");`

NEW:
```csharp
parityGate.ShouldContain("the tracker is `done` for FR36-C2 only; this gate stays blocked");
parityGate.ShouldNotContain("tracker remains `review`");
```
The `TECHNICAL PASS; INDEPENDENT GATE BLOCKED` assertion and every G-HIGH-RISK, G-PUBLICATION-AUTH, and G-CONSUMER assertion are retained unchanged.

### D. `_bmad-output/planning-artifacts/prd.md`

- **D0. Frontmatter `source_artifacts`.** Append `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-26.md`. Do **not** repin the epics `inputDocumentDigests`: the 2026-09-23 §4.D.1 rule is "never refresh hashes before the body and ownership review".
- **D1. §6.8, line 331.**
  - OLD: "The spec is `done` for this bounded `evidence-validated` result; the tracker remains `review` pending G-HIGH-RISK's independent identity and sealed CI controls."
  - NEW: "The spec is `done` for this bounded `evidence-validated` result, and a dated 2026-09-26 owner decision records the tracker as `done` for FR36-C2 only, superseding the 2026-09-24 `review` hold without rewriting it; G-HIGH-RISK's independent identity and sealed CI controls remain absent, are owned by backlog Story 9.2, and still block G-RUNTIME-PARITY."
- **D2. §11.3, line 616.**
  - OLD: "On 2026-09-26 the tracker is `review` and the spec is `done` for bounded evidence validation on current subject `66be1b4a…ea9b6` with 3 of 3 receipts. G-HIGH-RISK remains open."
  - NEW: "On 2026-09-26 the spec is `done` for bounded evidence validation on current subject `66be1b4a…ea9b6` with 3 of 3 receipts, and a dated owner decision moved the tracker from `review` to `done` for FR36-C2 only. G-HIGH-RISK remains open."
  - The full digest is kept.
  - Last cell, OLD: "Story 3.15 remains in tracker `review` pending independent control;"
  - Last cell, NEW: "Story 3.15's tracker `done` is bounded to FR36-C2 and supplies no gate, release, or readiness evidence;"
- **D3. §11.4 G-RUNTIME-PARITY status cell, line 651.**
  - OLD: "The spec is `done`, while the tracker remains `review` pending G-HIGH-RISK's independent second-identity and sealed CI controls."
  - NEW: "The spec is `done` and, by the dated 2026-09-26 owner decision, the tracker is `done` for FR36-C2 only; this gate stays blocked pending G-HIGH-RISK's independent second-identity and sealed CI controls."
  - `TECHNICAL PASS; INDEPENDENT GATE BLOCKED` stays.
- **D4. §11.4 G-HIGH-RISK status cell, line 654.** Additive. After "…deployment, or migration." insert " Primary owner: backlog Story 9.2; its corrective handoff requires Story 9.1 (OR28)." The guarded substrings are unchanged.
- **D5. §12 owner cells.**
  - OR10: append "; primary story 9.2 (backlog)".
  - OR13: "Test owner" → "Test owner; primary story 9.2 (backlog)".
  - OR28: append "; primary story 9.1 (backlog)".
- **D6. §12 OR15, line 689.**
  - OLD: "the spec is `done` for bounded evidence validation and the tracker is `review` pending G-HIGH-RISK independent control."
  - NEW: "the spec is `done` for bounded evidence validation, and the dated 2026-09-26 owner decision records the tracker `done` for FR36-C2 only while G-HIGH-RISK stays blocked."

### E. `_bmad-output/planning-artifacts/epics.md`

**E1. Epic List.** Insert after the Epic 8 entry and before **Sequencing rule**:

> ### Epic 9: Phase 4 Gate Decisions Are Machine-Enforced And Independently Approved
> Gate evaluators can prove which failed gate a corrective change is authorized to fix, and can prove that every high-risk gate result was validated in sealed CI and approved by an authenticated identity independent of its author.
> **Primary users:** Product owner, Test Architect, gate evaluators, release and deployment owners
> **FRs covered:** none (governance). **Refinements owned:** OR10, OR13, OR28; gate G-HIGH-RISK; the corrective-work authorization input to G-BASELINE
> **Cross-cutting coverage:** supporting NFR7, NFR12, NFR16 (as listed by G-HIGH-RISK); closes none of them
> **Implementation notes:** MVP epic, independent of Epic 8. Story 9.1 bootstraps PRD §0. Story 9.2 cannot reach `done` without a named human reviewer independent of the author. Neither story grants readiness, release, deployment, or migration authority.

**E2. Append an Epic 9 section after Story 8.11:**

> ## Epic 9: Phase 4 Gate Decisions Are Machine-Enforced And Independently Approved
>
> ### Story 9.1: Corrective-Work Authorization Record And Validator
> As a Product owner, I want every gate-closing corrective change bound to a content-addressed authorization record, so that no failed gate is "fixed" by an unauthorized, overbroad, or out-of-path change.
>
> **Requirements coverage:** Primary OR28; supporting G-BASELINE.
>
> **Dependencies:** None. **Bootstrap rule:** PRD §0 cannot validate this story's own handoff, so a dated owner authorization naming its allowed paths (`tools/validate-corrective-work-authorization.py`, its tests and fixtures, the record schema, a new CI workflow file) is recorded in the story before dev starts.
>
> **Acceptance Criteria:**
> - **Given** a record at `_bmad-output/implementation-artifacts/evidence/corrective-work-authorizations/<gate>/<authorization-id>.json`
>   **When** `python3 tools/validate-corrective-work-authorization.py <record> --changed-paths-from <baseline-sha>` runs
>   **Then** it validates every field PRD §0 enumerates, including `corrective-work-only: true` with every authority flag `false`
>   **And** it rejects missing, expired, revoked, mismatched, overbroad, or out-of-path authority.
> - **Given** a completed corrective diff
>   **When** postflight runs
>   **Then** it binds the derived output-subject digest
>   **And** any later output change invalidates the record.
> - Each rejection path is proven by a checked-in negative fixture that is **observed** failing, together with a positive control. No guard may be green by construction.
> - The validator runs as a blocking CI check in a **new** workflow file. `docs/ci.md` is not edited unless a Story 4.15 reseal is planned.
>
> ### Story 9.2: Phase 4 High-Risk Gate Matrix And Non-Authorship Control
> As a Test Architect, I want each mandatory gate classified and every high-risk gate result bound to sealed CI validation plus an authenticated independent approver, so that no author can approve their own high-risk evidence.
>
> **Requirements coverage:** Primary OR10 and OR13; gate G-HIGH-RISK. Absorbs the 2026-09-23 §4.E transition guard **for high-risk gate rows only**. The all-story tracker/wrapper/epics lifecycle comparison stays with OR15 and G-BASELINE.
>
> **Dependencies:** Story 9.1 passed, plus a Story 9.1 authorization record for gate G-HIGH-RISK. **Named independent reviewer:** *unassigned*. Under the External-authority rule this is an explicit blocker. The story may build the matrix and validator, but it cannot reach `done` or mark its own row PASS until a human reviewer is named, with an authenticated account distinct from `github:jpiquot`, and that reviewer approves.
>
> **Acceptance Criteria:**
> - `_bmad-output/implementation-artifacts/evidence/phase-4-gate-risk-matrix.json` enumerates exactly the 17 gates PRD G-HIGH-RISK lists.
>   - `python3 tools/validate-phase-4-gate-risk-matrix.py <matrix>` rejects omissions, unknown gates, and new unclassified gates.
>   - Each entry is `high-risk` or a reason-coded `standard-control`.
> - Each high-risk entry binds its exact command, trigger, subject and evidence identities, pass condition, author and evaluator roles, non-authorship result, and guarded transition.
> - The non-authorship check rejects all of the following, each proven by an observed failing fixture:
>   - self-approval;
>   - identity aliasing, meaning one account in two roles (such as the Story 3.15 registry's `github:jpiquot` ×2);
>   - unauthenticated or tool-persona identities (such as `bmad:*`) offered as the independent identity;
>   - a missing seal.
> - Sealed CI: the validator runs in a blocking, required CI check, and the matrix inputs are content-hashed so that edits fail the check.
> - OR13 transition guard: a high-risk gate cannot move to PASS, and a story cannot record `done` against a high-risk NFR, without the passing validator result and the independent approval.
> - Evaluating G-RUNTIME-PARITY under this control is follow-on work. It does not reopen Story 3.15 unless the independent evaluation rejects the evidence. Any new receipt set follows the G-RUNTIME-PARITY re-mint rule.

**E3. Story 3.15.** Append after its last acceptance criterion:

> **Current reconciliation (2026-09-26):** By dated owner decision (`sprint-change-proposal-2026-09-26.md`), Story 3.15 is `done` for FR36-C2 evidence validation only: subject `66be1b4a…ea9b6`, 3/3 receipts, validator exit 0, selected index `sha256:4b141085…`. Both owner roles map to one account and the Test Architect record is self-attested, so this is not three-party review. G-RUNTIME-PARITY and G-HIGH-RISK stay blocked; Story 9.2 owns G-HIGH-RISK. Reopen triggers: validator failure, subject re-mint, or independent rejection. Epic 3 stays `in-progress` for Story 3.16.

This resolves the second half of the 2026-09-24 un-IDed deferred entry ("Story 3.15 acceptance … without the tracker review handoff"). The FR36-completion-rule half stays open.

### F. Architecture: no edit

G-HIGH-RISK is an evidence and approval governance gate, not a design decision. It changes no component, AD, or contract. `architecture.md` is `draft`, pending the owner review sequenced by the 2026-09-23 proposal (§5 order 2), and this change does not add to that review.

### G. Story 3.15 records (outside frozen blocks)

- **G1. `3-15-corrected-deployed-runtime-parity-closure.md:53`.**
  - OLD: "…while the sprint row remains `review` pending G-HIGH-RISK independent control."
  - NEW: "…and a dated 2026-09-26 owner decision closed the sprint row `done` for FR36-C2 only; G-HIGH-RISK stays blocked and is owned by Story 9.2."
  - The subject and index substrings required by the operator-record test stay.
- **G2. `spec-3-15-corrected-deployed-runtime-parity-closure.md:54–55`.** Make the same substitution. This sits outside `<frozen-after-approval>` (lines 13–38). Historical lines 560 and 610 are unchanged.
- **G3. `spec-3-15-tracker-reconciliation.md`.** Append under Implementation Notes: "2026-09-26: the owner renegotiated the Decision (2026-09-24) via `sprint-change-proposal-2026-09-26.md`; the tracker is `done` for FR36-C2 only. The frozen Decision text above is preserved as history." The frozen block is untouched.

## 5. Implementation Handoff

**Scope: Moderate.** Backlog reorganization with one new epic, plus a docs and test-only lifecycle change.

| Order | Role | Deliverable | Exit check |
| --- | --- | --- | --- |
| 1 | Owner (jpiquot) | Explicit approval of this proposal and the verbatim §3 decision | Approval recorded in §7 |
| 2 | Developer | Apply §4.A–E and G in **one** commit | Release build of Contracts.Tests; focused runs via the assembly (`-class` and `-method` in separate runs, never together); full suite **2130/2133**, where the only failures are the 3 known OQ8 v5 / Story 4.15 tests; validator exit 0 on the same subject and index; `git diff --check` clean |
| 3 | Developer | Commit message `docs(planning): close story 3.15 at bounded scope and register epic 9 gate owners` | Validated with the repo's pinned commitlint before use |
| 4 | Product owner | Name the Story 9.2 independent reviewer; record the Story 9.1 bootstrap authorization | Precondition for 9.1 and 9.2 dev handoff, not for this commit |

**Success criteria:**
- The tracker, spec, PRD, and epics agree that 3.15 is `done`, bounded to FR36-C2.
- G-RUNTIME-PARITY and G-HIGH-RISK are still FAIL/BLOCKED and still guarded.
- All four authority flags are still `false`.
- Epic 3 is `in-progress`.
- G-HIGH-RISK, OR10, OR13, and OR28 each have exactly one primary backlog owner.

## 6. Checklist Disposition

| Area | Status | Record |
| --- | --- | --- |
| 1 Trigger/context | [x] | §1 table, re-run 2026-09-26 at `818e28a8` |
| 2 Epic impact | [x] | Epic 3 unchanged `in-progress`; new backlog Epic 9; no other epic affected |
| 3 Artifact conflict | [x] | PRD, epics, tracker, 2 tests, 3 story records; architecture/UX N/A with reason |
| 4 Path | [x] | A not viable now (double-blocked, open-ended); rollback and MVP review not needed; B + backlog owners selected by the owner |
| 5 Proposal/handoff | [x] | §3–5 |
| 6 Approval/implementation | [x] | Owner approved 2026-09-26; §4 applied in one commit (see §7) |

## 7. Approval And Workflow Execution Log

- 2026-09-26: the owner selected "B + backlog owners" and batch review in the correct-course session.
- 2026-09-26: owner approved this proposal ("continue and continue") after reviewing the batch; scope: apply §4.A–E and G as one docs/test commit. Route: Developer for §4; Product owner for §5 order 4.
- Conversational approval does not supply G-HIGH-RISK approval, a second identity, or any release, deployment, readiness, or consumer-removal authority.
