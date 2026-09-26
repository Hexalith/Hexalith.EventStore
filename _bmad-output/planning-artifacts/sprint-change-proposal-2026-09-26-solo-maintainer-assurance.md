---
title: Solo-maintainer assurance control replaces the unconditional second-identity requirement
date: 2026-09-26
type: sprint-change-proposal
status: approved-for-implementation
scope: major-requirement-amendment
readiness_effect: none
sprint_status_effect: proposed
amends:
  - _bmad-output/planning-artifacts/prd.md (Owner Roles glossary, §11.4 gate rows, OR10, OR13)
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-09-26.md (§5 order 4 only; recorded as a dated supersession)
---

# Solo-Maintainer Assurance Control

## 1. Issue Summary

**Trigger.** On 2026-09-26 the Owner (`github:jpiquot`) stated that they are the only person on the project.

**Why this matters.** Several PRD controls require a second human who is not the author:
- **OR10 and G-HIGH-RISK** require "an authenticated second identity independent from the author".
- **Six gates** require "independent approval": G-TENANT, G-STATUS-ID, G-APPEND, G-COMPAT, G-AUTH-HOSTS, and G-MVP-COVERAGE.
- **G-OQ8, G-RUNTIME-PARITY, G-PUBLICATION-AUTH, and G-CONSUMER** inherit "non-authorship control from G-HIGH-RISK".
- **The Owner Roles glossary entry** (PRD line 191) adds that "OR10 owns that closure rather than weakening the requirement".

With one person, none of these can pass. G-READINESS can therefore never compute `READY`, and release, promotion, and migration authority stay blocked permanently. Story 9.2, added earlier today, is blocked for the same reason: its independent reviewer is "unassigned".

**Category.** A change in stakeholder and resource constraints. It is not a defect in delivered work.

## 2. Impact Analysis

| Area | Impact |
| --- | --- |
| **PRD** | **Glossary:** a new Assurance Control entry, and a reworded Owner Roles entry. **§11.4:** the intro, G-HIGH-RISK, the six "independent approval" gates, the four gates that inherit non-authorship, and G-READINESS. **§12:** OR10 and OR13. **Story 3.15 wording:** lines 332, 509, 619, 652, and 654 say "independent identity/control". **What does not change:** MVP scope, FR and NFR text, and every gate's current result (all still FAIL/BLOCKED). |
| **Epics** | Epic 9 is retitled. Story 9.2 is rewritten: the reviewer blocker is removed, and ACs are added for the assurance level. **No change to Epic 8 / Story 8.11 (post-MVP G5).** It keeps its "independent Test authorities" requirement, so G5 stays blocked while the project is solo. This is recorded as an open owner decision (§6). |
| **Architecture** | None. No AD mentions independence or non-authorship. |
| **UX** | None. |
| **Tracker** | The 9.2 key is renamed while it is still an empty backlog row, and its comment is updated. **The Story 3.15 caveat line in the GUARDED block is reworded.** It currently says G-HIGH-RISK lacks an "independent second-identity check", which would describe a requirement that no longer exists. |
| **Tests** | **PRD guard:** the G-HIGH-RISK status substring changes, and a check is added that the amended OR10 still requires sealed CI. **Lifecycle guard:** the verbatim caveat line changes. **Story 3.15 lifecycle:** unchanged. The row stays `done` and the spec stays `'done'`. |
| **Code, `tools/`, CI, `docs/ci.md`** | None. The 3.15 subject and receipts are unaffected. |

**Options considered**
- **Keep the requirement.** Honest, but the Phase 4 MVP could never complete. Rejected by the Owner.
- **Hybrid with an external audit.** Rejected by the Owner.
- **The amendment** was selected.

**The trade-off, stated plainly.** The amendment accepts that no second human judges high-risk work. It does not pretend otherwise:
- Mechanical checks, a time gap before approval, and mandatory labelling replace independent judgment. They do not equal it.
- The `independent` requirement comes back automatically as soon as the registry names a second human.

## 3. Recommended Approach

**Direct requirement amendment, by dated Owner decision.**

### Decision (2026-09-26), to be recorded verbatim

> The Owner, the project's only person, replaces the unconditional "authenticated second identity independent from the author" requirement with the roster-dependent **Assurance Control** defined below, and accepts the residual risk that no second human reviews high-risk work.
>
> - Every gate result, and every record that consumes one, carries its achieved assurance level.
> - No record may call single-maintainer work `independent`.
> - When the owner-role registry names two or more distinct human identities, the `independent` level becomes the required level again, without any further PRD change.
> - This decision changes no current gate result and grants no `READY`, release, promotion, deployment, migration, or consumer-removal authority.

### The control (new glossary entry)

- **Assurance Control.** The approval control that G-HIGH-RISK applies to every row classified high risk.
- **Required level.** Computed from the owner-role registry:
  - `single-maintainer-attested` while the registry names exactly one distinct human identity;
  - `independent` when it names two or more.
  - Tool personas (such as `bmad:*`) and CI identities never count as human identities.
- **`single-maintainer-attested` requires all of the following:**
  1. **Sealed CI validation.** The gate validator's result is fetched from the CI platform, not supplied by the author as a file. It must come from a required, blocking run on the exact head SHA and workflow-file digest, under the platform's authenticated CI identity.
  2. **Time-separated owner attestation.** An authenticated owner approval, bound to the subject digest, created at least **24 hours** after the last authored change to that subject.
  3. **Label propagation.** The gate result is labelled `single-maintainer-attested`. So is every downstream record that consumes it: `READY`, `release-available`, `production-promoted`, and consumer removal. A downstream record carries the lowest assurance level of its inputs.
- **Optional adversarial review.** It may be bound as `tool-persona` evidence. It never counts as an approval identity.
- **`independent`** additionally requires an authenticated human approver who is not the author.
- **Validators reject:**
  - a label that overstates the evidence, such as `independent` with one human identity, self-approval, or an alias of the author;
  - a level below the one required;
  - an attestation made inside the 24-hour window;
  - a missing seal.

**Effort:** Low for the documentation and test commit. Story 9.2's implementation effort is unchanged.

**Risk:** The main risk is residual author blind spots, which the Owner accepts explicitly. The 24-hour window is a proposed default; edit it before approving if you prefer another value.

## 4. Detailed Change Proposals (batch, one commit)

### A. PRD glossary

**A1. Owner Roles (line 191).**
- OLD: "…and an approval issued by the author of the work it approves satisfies the record but not the control. G-HIGH-RISK makes machine-verifiable non-authorship binding for every row classified high risk; today only Story 8.11 implements such a control, so the mandatory matrix and general control remain unimplemented and G-HIGH-RISK fails. OR10 owns that closure rather than weakening the requirement."
- NEW: "…and an approval issued by the author of the work it approves is never labelled `independent`. G-HIGH-RISK binds every row classified high risk to the Assurance Control, whose required level depends on how many humans the owner-role registry names; the matrix and control remain unimplemented and G-HIGH-RISK fails. On 2026-09-26 the owner, then the project's only person, replaced the unconditional second-identity requirement with that roster-dependent control and accepted its residual risk; OR10 owns the closure. Post-MVP Story 8.11 retains its own independent-authority requirement."

**A2. Insert after Owner Roles.** Add a new `- **Assurance Control** - …` entry containing the §3 control text verbatim, ending with: "Adopted by owner decision 2026-09-26 (`sprint-change-proposal-2026-09-26-solo-maintainer-assurance.md`)."

### B. PRD §11.4 gate contract

- **B1. Intro (line 637).**
  - OLD: "…evaluator result, and independent approval."
  - NEW: "…evaluator result, and approval at its required assurance level (glossary: Assurance Control); `READY` carries the lowest assurance level of its inputs."
- **B2. The six gates' approval cells.**
  - For G-TENANT, G-STATUS-ID, G-APPEND, G-COMPAT, and G-AUTH-HOSTS: "with independent approval." → "with approval at the G-HIGH-RISK Assurance Control level."
  - For G-MVP-COVERAGE: "with independent approval of the same manifest digest." → "with approval of the same manifest digest at the G-HIGH-RISK Assurance Control level."
- **B3. The four inheriting gates.**
  - In G-OQ8, G-PUBLICATION-AUTH, and G-CONSUMER: "non-authorship control from G-HIGH-RISK" → "Assurance Control from G-HIGH-RISK".
  - In G-RUNTIME-PARITY: "subject to G-HIGH-RISK non-authorship control" → "subject to the G-HIGH-RISK Assurance Control".
- **B4. G-HIGH-RISK requirement cell.**
  - OLD: "Every high-risk entry requires both an authenticated second identity independent from the author and sealed CI validation, and binds its exact command, trigger, subject/evidence identities, pass condition, author/evaluator roles, non-authorship result, and guarded transition."
  - NEW: "Every high-risk entry requires the Assurance Control at the level computed from the owner-role registry, always including sealed CI validation, and binds its exact command, trigger, subject/evidence identities, pass condition, author/evaluator roles, required and achieved assurance level, and guarded transition."
  - Approval cell: "both controls must be machine-verifiable for each high-risk row." → "the Assurance Control must be machine-verifiable for each high-risk row."
  - Status cell, which the guard asserts: "second-identity controls" → "assurance-level controls".
- **B5. G-READINESS requirement cell.** Append: " The report must state its computed assurance level (`single-maintainer-attested` or `independent`)."

### C. PRD §12

- **C1. OR10 requirement cell.** Replace it with:
  "**Blocking.** Implement the binding G-HIGH-RISK Assurance Control (glossary): every high-risk row requires sealed CI validation plus approval at the level computed from the owner-role registry — `single-maintainer-attested` while it names one human, `independent` when it names two or more. The versioned matrix and validator must prove the required level and reject overstated labels (including self-approval or identity aliasing labelled `independent`), tool personas counted as human approvers, attestations inside the separation window, missing seals, omitted gates, and reasonless classifications. Amended 2026-09-26 by owner decision; the prior unconditional second-identity wording is superseded."
- **C2. OR13.** "independent approval rule" → "assurance-level approval rule".

### D. PRD Story 3.15 wording (wording only; no result changes)

| Line | Change |
| --- | --- |
| 332 | "G-HIGH-RISK's independent identity and sealed CI controls remain absent" → "G-HIGH-RISK's assurance and sealed CI controls remain absent" |
| 509 | "G-HIGH-RISK's independent control" → "G-HIGH-RISK's assurance control" |
| 619 | "G-HIGH-RISK's independent identity and sealed CI controls are absent" → "G-HIGH-RISK's assurance and sealed CI controls are absent" |
| 652 | "pending G-HIGH-RISK's independent second-identity and sealed CI controls" → "pending G-HIGH-RISK's assurance and sealed CI controls" |
| 654 | "has no G-HIGH-RISK independent control" → "has no G-HIGH-RISK assurance control" |

Every identity caveat stays, for example "Both owner roles map to one authenticated account and the Test Architect receipt is self-attested".

**D0. Frontmatter.** Append this proposal to `source_artifacts`.

### E. Epics

- **E1. Epic List entry and section heading.**
  - "Epic 9: Phase 4 Gate Decisions Are Machine-Enforced And Independently Approved" → "Epic 9: Phase 4 Gate Decisions Are Machine-Enforced And Approved At A Declared Assurance Level".
  - Goal sentence: "…approved by an authenticated identity independent of its author." → "…approved at the Assurance Control level the owner-role registry requires, with that level labelled on every result."
  - Implementation note: "Story 9.2 cannot reach `done` without a named human reviewer independent of the author." → "While the registry names one human, Story 9.2 closes on sealed CI plus a time-separated owner attestation and is labelled `single-maintainer-attested`."
- **E2. Story 9.2.**
  - Retitle it "Phase 4 High-Risk Gate Matrix And Assurance Control".
  - "So that": "…no author can approve their own high-risk evidence." → "…no high-risk result overstates the assurance behind it."
  - Replace the "Named independent reviewer: unassigned" blocker with: "**Approval:** Assurance Control at the registry-computed level; while the registry names one human, the owner's time-separated attestation, labelled `single-maintainer-attested`."
  - Replace the non-authorship AC with one that rejects:
    - `independent` labels backed by fewer than two distinct human identities;
    - self-approval or identity aliasing labelled `independent`;
    - tool-persona or CI identities counted as human approvers;
    - attestations less than 24 hours after the last authored change to the subject;
    - a level below the required one;
    - missing seals.
    Each rejection must be proven by an observed failing fixture.
  - Add an AC: the required level switches to `independent` when a fixture registry names two humans, and fixtures prove that switch.
  - Add an AC: a downstream record carries the lowest input level.
  - The sealed CI AC is extended: the validator result is fetched from the CI platform for the exact head SHA and workflow-file digest, never read from an author-supplied file.

### F. `sprint-status.yaml`

- **F1. 3.15 GUARDED caveat line.** Verbatim; this is the only line in the block that changes. "…matrix, validator, independent second-identity check, and sealed CI control." → "…matrix, validator, assurance-level check, and sealed CI control."
- **F2. Epic 9 comment and key.**
  - "# Story 9.2 cannot close until a named human reviewer independent of github:jpiquot approves." → "# Story 9.2 closes on sealed CI plus a time-separated owner attestation (single-maintainer-attested)."
  - Key `9-2-phase-4-high-risk-gate-matrix-and-non-authorship-control` → `9-2-phase-4-high-risk-gate-matrix-and-assurance-control` (`backlog`; no story file exists yet).

### G. Tests

- **G1. `DeployedRuntimeParityClosureTests.CorrectedLifecycleRowsRetainTheirCorrectedStatus`.** Mirror F1 in the verbatim block. The row pin (`done`) and the spec pin (`'done'`) are unchanged.
- **G2. `CorrectedDeployedRuntimeParityClosureTests.PlanningRuntimeParityAccountMatchesCurrentPacketAndPendingControl`.**
  - G-HIGH-RISK substring "second-identity controls" → "assurance-level controls".
  - Add `highRiskGate.ShouldContain("always including sealed CI validation")`, so that a later edit cannot drop sealed CI from the amended control.

### H. Prior proposal

Append this line to the §7 log of `sprint-change-proposal-2026-09-26.md`: "2026-09-26 (later): §5 order 4 ('name the Story 9.2 independent reviewer') is superseded by `sprint-change-proposal-2026-09-26-solo-maintainer-assurance.md`; the owner is the project's only person." The rest of that proposal is unchanged.

## 5. Implementation Handoff

**Scope: Major** (a requirement amendment), but the execution is a single docs and test commit by the Developer. The Owner is the sole approver.

| Order | Role | Deliverable | Exit check |
| --- | --- | --- | --- |
| 1 | Owner | Approve this proposal, including the 24-hour window | §7 log |
| 2 | Developer | Apply §4 A–H in one commit | Release build of Contracts.Tests; focused guard runs; mutations of the new sealed-CI assertion and of the caveat both go red; full suite 2130/2133 (only OQ8 v5 / 4.15 fail); 3.15 validator exit 0 on the same subject; `git diff --check` |
| 3 | Developer | Message `docs(planning): adopt solo-maintainer assurance control for phase 4 gates` | Pinned commitlint 21.1.0 exit 0 |

**Success criteria:**
- No PRD, epics, or tracker text requires or implies a second human while the registry names one.
- Every gate is still FAIL/BLOCKED, and all four 3.15 authority flags are `false`.
- Story 9.2 has no permanent blocker.
- The switch back to `independent` is written into the definition.

## 6. Open Decisions Not Taken Here

- **Post-MVP G5 (Story 8.11)** still names "independent Test authorities" and a Security approver, so it stays blocked while the project is solo. It needs a separate Owner decision before Epic 8 reaches 8.11.
- **Re-evaluating G-RUNTIME-PARITY** under the Assurance Control is Story 9.2 follow-on work. A new time-separated attestation may be required, under the G-RUNTIME-PARITY re-mint rule.

## 7. Checklist Disposition And Log

| Area | Status |
| --- | --- |
| 1 Trigger | [x] Owner statement 2026-09-26 |
| 2 Epic impact | [x] Epic 9 only; Epic 8 flagged in §6 |
| 3 Artifacts | [x] PRD, epics, tracker, 2 tests, prior proposal; architecture and UX N/A |
| 4 Path | [x] Amendment selected by the Owner; keep-as-is and hybrid rejected |
| 5 Proposal | [x] §3–5 |
| 6 Approval | [x] Owner approved 2026-09-26; applied in one commit |

- 2026-09-26: the Owner selected "Solo-maintainer amendment" in the correct-course session. Batch mode.
- 2026-09-26: the Owner approved this proposal ("continue"), including the 24-hour separation window.
- Applied with three same-class additions found by the implementation sweep, under §5's success criterion that no PRD text requires or implies a second human: UJ5 "its independent receipts" → "its receipts at the required assurance level"; the §11.3 Story 3.15 row "the independent gate" → "the G-HIGH-RISK assurance gate"; OR29 "Implement and independently approve" → "Implement and approve at the G-HIGH-RISK Assurance Control level". Also, the Story 3.15 reopen trigger in epics.md, "independent G-HIGH-RISK rejection", became "a G-HIGH-RISK Assurance Control evaluation that rejects the evidence".
