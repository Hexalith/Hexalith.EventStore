---
project: eventstore
date: 2026-08-12
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
status: approved-for-documentation-handoff
trigger: story-3-13-bmad-build-step-3-evidence-gate
existing_proposal_disposition: on-hold-not-deleted-not-overwritten-not-superseded
existing_proposal: _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-12.md
final_approved_by: Administrator
final_approved_on: 2026-08-12
handoff_status: routed
handoff_recipients:
  - Product Owner
  - Developer/documentation owner
  - External evidence owners
sprint_tracking_mutation: additive-comments-only-approved
---

# Follow-Up Sprint Change Proposal — Story 3.13 Step 3 Evidence Gate

**Author:** Amelia (Developer) via `bmad-correct-course`
**Mode:** Batch
**Status:** DRAFT FOR REVIEW — NOT APPROVED
**Immediate scope:** Documentation-only evidence disposition; no implementation or tracking mutation

## 1. Issue Summary And Verified Evidence

Story 3.13 repository-owned hardening is fully implemented and locally verified, but the
post-handoff `bmad-build` run halted correctly at its Step 3 implementation gate. The halt is not a
new runtime or verifier defect. It is the truthful consequence of the current story contract:
Tasks 4–7 and 9 still depend on evidence or acceptance that the repository cannot create.

The Step 3 halt must therefore be recorded as a **post-handoff evidence disposition**, not as a
request for another implementation or hardening pass.

### Verified current state

| Boundary | Verified evidence | Disposition |
| --- | --- | --- |
| Story lifecycle | Story record: `Status: in-progress`; spec frontmatter: `status: 'in-progress'`; sprint row: `3-13-deployed-runtime-parity-closure: in-progress`; `docs/ci.md`: `in-progress` | Preserve all four surfaces as `in-progress` |
| Repository-owned hardening | The story/spec record thirteen review-hardening passes, all in-scope patches applied, a Release test-project build with zero warnings/errors, 172/172 focused `DeployedRuntimeParityClosureTests`, Story 1.20 critical manifest 33/33, Story 3.13 core manifest 17/17, outer manifest 3/3, and clean Markdown validation | Complete; no further hardening is justified |
| AC1 | Frozen predecessor identities and the selected Story 1.20 evidence tree are hash-bound and verified | Pass |
| AC2 | No exact source/package/release/deployed lineage exists in the retained packet | Open; must not be reported as passed |
| AC3 | The packet and verifier fail closed on missing or inconsistent evidence and authorize no external mutation | Pass |
| AC4 | Required content-bound acceptances are absent | Open; 0/3 acceptances |
| Task 4 | Exact 14-package inventory is known, but the approved archives are unrecovered (`recovered_count: 0` of `expected_count: 14`) and cannot be independently rehashed | Incomplete; leave unchecked |
| Task 5 | Raw OCI descriptor/body relationships are consistent, but child-manifest and config response metadata was not retained | Incomplete; leave unchecked |
| Task 6 | Retained smokes are unverified and used `Development`; equivalent structured `Production` evidence for `linux/amd64` and `linux/arm64` is absent | Incomplete; leave unchecked |
| Task 7 | The selected lineage has no same-source semantic-release provenance or deployed authority | Incomplete; leave unchecked |
| Task 9 | EventStore owner, Release owner, and Test Architect receipts for one unchanged content-bound subject are absent | Incomplete; leave unchecked; 0/3 |
| `bmad-build` Step 3 gate | Administrator's 2026-08-12 post-handoff report records that the workflow halted at the implementation gate because the remaining work is external-evidence closure | Correct halt; record it, do not rerun `bmad-build` |

### Core problem classification

This is an **external-evidence availability and acceptance gate**, not an unimplemented
repository-owned technical requirement. Repository changes can make the fail-closed result more
robust, but they cannot recover the exact approved archives, create same-lineage release
provenance, recreate missing registry response metadata, manufacture equivalent Production runtime
evidence, or infer content-bound owner/Test Architect acceptance.

Further hardening against unchanged inputs would add churn without changing AC2 or AC4.

## 2. Impact Analysis

### Epic and story impact

| Artifact or scope | Impact | Immediate disposition |
| --- | --- | --- |
| Epic 1 / Story 1.20 | None. Source/package parity remains completed under its existing approval. | Keep unchanged and `done` |
| Epic 3 / Story 3.12 | None. The corrective release remains completed evidence from a different lineage. | Keep unchanged and `done` |
| Epic 3 / Story 3.13 | Repository-owned hardening is complete, but external closure is not. | Keep `in-progress`; record the Step 3 evidence hold |
| Epic 3 overall | Story 3.13 remains an open Epic 3 item. | Keep `in-progress` |
| Other epics and stories | No dependency or acceptance boundary changes. | No change |

No epic is added, removed, reordered, reopened, or moved into or out of MVP scope.

### Planning and architecture impact

- PRD requirements and traceability remain unchanged.
- Architecture decisions, including AD-22, remain unchanged.
- UX has no impact.
- Story 3.13 acceptance criteria remain unchanged.
- The story and spec remain `in-progress`.
- Tasks 4–7 and 9 remain incomplete; none becomes complete, passed, or not applicable.
- The proof packet, evidence schemas, reviewer subject, test verifier, and checksum manifests remain
  unchanged.

### Existing approved proposal

`_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-12.md` remains an approved
historical governance artifact. This follow-up does not delete, edit, overwrite, or silently
supersede it.

Its implementation/handoff disposition is placed on **operational hold** because its proposed
terminal-`unavailable` closure cannot presently satisfy Task 9: the exact content-bound acceptance
count remains 0/3. The existing proposal may be reconsidered only through an explicit future
governance decision that cites both proposals and re-establishes its approval and acceptance
boundary. Until then, no terminal re-scope edits or handoff steps from that proposal are active.

### Technical and operational impact

There is no runtime, test, package, release, registry, deployment, consumer, submodule,
predecessor-evidence, PRD, architecture, or acceptance-criteria implementation impact. The only
proposed future edits are additive documentation annotations that distinguish:

1. completed repository-owned hardening; and
2. incomplete externally owned evidence and acceptance.

Effort after approval is low. Technical risk is low because status, criteria, task checkboxes, and
evidence bytes remain unchanged. Schedule impact is indeterminate and wholly dependent on external
evidence availability.

## 3. Options With Pros And Cons

### Option A — Documentation-only external-evidence hold (recommended)

Preserve Story 3.13 unchanged as `in-progress`, record the correct `bmad-build` Step 3 halt, freeze
further hardening/build attempts, and wait for the complete external restart gate.

**Pros:**

- Truthfully separates completed repository work from incomplete external closure.
- Preserves AC1/AC3 success without overstating AC2/AC4.
- Keeps Tasks 4–7 and 9 visibly open.
- Prevents repeated verifier/evidence churn against unchanged inputs.
- Requires no runtime or evidence mutation.

**Cons:**

- Story 3.13 and Epic 3 remain open for an indeterminate period.
- Closure depends on evidence and acceptances outside repository implementation control.

### Option B — Execute the existing terminal-`unavailable` proposal now

Re-scope Story 3.13 so a terminal negative decision can complete the story.

**Pros:**

- Could eventually provide a governance path out of permanent positive-lineage unavailability.
- Preserves the conceptual distinction between story completion and capability availability.

**Cons:**

- Not currently closable: Task 9 remains 0/3, so the exact re-scoped content-bound packet lacks
  required owner/Test Architect acceptance.
- Conflicts with the present no-acceptance-criteria-change boundary.
- Its proposed Task 4–7 terminal dispositions would conflict with the instruction to leave those
  tasks incomplete and not mark them passed or not applicable.
- Would require a new explicit governance decision before implementation.

**Disposition:** Possible future governance option; place on hold now.

### Option C — Continue hardening or rerun `bmad-build`

Run another verifier, evidence, or workflow pass against the same external state.

**Pros:**

- None material under unchanged inputs.

**Cons:**

- Cannot recover archives or create external provenance, runtime evidence, authority, or receipts.
- Risks rebinding the review subject and invalidating future acceptance work.
- Adds churn to already verified repository-owned hardening.
- Violates the requested implementation boundary.

**Disposition:** Reject.

### Option D — Roll back repository hardening

Revert the Story 3.13 verifier/evidence hardening to reduce the open surface.

**Pros:**

- None relevant to the evidence gap.

**Cons:**

- Does not create missing evidence.
- Weakens fail-closed guarantees and discards verified work.
- Risks disturbing user-owned work and predecessor integrity.

**Disposition:** Reject.

## 4. Recommended Immediate Disposition

Approve Option A as a **moderate sprint-governance correction with low-effort documentation-only
implementation**:

1. Keep Story 3.13, its spec, sprint status, and CI documentation at `in-progress`.
2. Record the `bmad-build` Step 3 halt as the truthful post-handoff evidence disposition.
3. State that repository-owned hardening is complete and externally owned closure is incomplete.
4. Leave Tasks 4–7 and 9 unchecked and incomplete.
5. Prohibit further Story 3.13 hardening, evidence rebinding, verifier expansion, and `bmad-build`
   attempts until the full restart gate in section 6 is satisfied.
6. Put the existing terminal-closure proposal on operational hold without changing its file or
   approval record.
7. Preserve terminal-`unavailable` re-scoping as a possible future governance choice, while stating
   that it cannot close now because Task 9 evidence remains unavailable.

This disposition changes no acceptance criterion and creates no completion claim.

## 5. Exact Old-To-New Documentation Edits

These edits are **proposed only**. They must not be applied until this complete proposal receives
explicit approval. Sprint tracking must not be modified before approval.

### 5.1 Existing approved terminal-closure proposal

**Artifact:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-12.md`

**OLD:**

```yaml
status: approved-for-implementation
final_approved_by: Administrator
final_approved_on: 2026-08-12
```

The proposal body also states `**Status:** APPROVED FOR IMPLEMENTATION` and defines its handoff
recipients and implementation plan.

**NEW DOCUMENTATION EFFECT:**

> Preserve the existing file byte-for-byte and preserve its approval record. This follow-up records
> its implementation/handoff disposition as **on hold**. It is not deleted, overwritten, or
> superseded. Reactivation requires a later explicit governance decision citing both proposals.

**File edit:** None.

### 5.2 Story 3.13 record

**Artifact:** `_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md`
**Section:** Append immediately after `## Story Completion Status`; do not change `Status`, AC1–AC4,
or any task checkbox.

**OLD:**

> Status remains `in-progress` after the 2026-08-12 thirteenth review pass. Every in-scope review
> patch is applied and locally verified. The fail-closed packet still has external evidence
> blockers; AC2/AC4 remain open with 0/3 acceptances.
>
> AC1 and AC3 pass. Raw OCI descriptor/body relationships pass, but child/config response metadata,
> independently replayable runtime facts, package bytes, release/source authority, valid
> provenance labels, and Production runtime equivalence are incomplete, so AC2 does not pass.
>
> AC4 does not pass: the packet is not a complete passing lineage and has zero of three required
> content-bound acceptances.
> Current acceptance status is exactly 0/3; no receipt, approval, publication, registry,
> deployment, consumer, or submodule state was created or changed by hardening. Two predecessor
> files were written at `3d6dea69` solely to restore approved bytes drifted by the unrelated
> commit `089369bb`; net predecessor state at HEAD is byte-identical to the approved identity.
> Story 3.13 must remain non-`done` until every blocker is resolved and all three reviewers accept
> one unchanged replacement review subject.

**NEW:** Preserve the quoted text exactly and append:

```markdown
### Post-Handoff Evidence Disposition — `bmad-build` Step 3 Gate (2026-08-12)

- The `bmad-build` handoff halted correctly at Step 3. Repository-owned Story 3.13 hardening is
  complete and locally verified; the remaining Tasks 4–7 and 9 require external evidence or
  acceptance that this repository cannot create.
- Story 3.13 remains `in-progress`. AC1 and AC3 pass; AC2 and AC4 remain open; the acceptance count
  remains 0/3. Tasks 4–7 and 9 remain unchecked and are not complete, passed, or not applicable.
- No further Story 3.13 hardening, verifier expansion, evidence rebinding, or `bmad-build` attempt is
  authorized until every restart condition in the approved follow-up Sprint Change Proposal is
  satisfied for one unchanged content-bound lineage.
- The approved terminal-closure proposal dated 2026-08-12 remains preserved but is on operational
  hold. Terminal-`unavailable` re-scoping remains a possible future governance decision; it cannot
  currently close because Task 9 content-bound acceptance evidence is unavailable.
```

### 5.3 Story 3.13 spec

**Artifact:** `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
**Section:** Append to `## Spec Change Log`; do not change frontmatter status, acceptance criteria,
task results, verification code, or evidence bindings.

**OLD:**

> 2026-08-12: Applied the thirteenth review-hardening pass without changing frozen intent. Closed
> all remaining review patches: exact fail-closed evidence/receipt/runtime shapes, immutable OCI
> identity, support-safe raw-config coverage, invariant timestamps, symlink-safe copies, durable
> package-source query records, authority/roster bindings, honest suite attribution, and corrected
> predecessor/status documentation. Focused verification is 172/172. AC2/AC4 and 0/3 acceptances
> remain open; the unrelated OQ8 aggregate conflict is recorded rather than weakening this spec.

**NEW:** Preserve the existing entry exactly and append:

```markdown
- 2026-08-12: Recorded the post-handoff `bmad-build` Step 3 evidence disposition without changing
  frozen intent, acceptance criteria, task completion, proof bytes, or status. Repository-owned
  hardening is complete; Tasks 4–7 and 9 remain externally blocked and unchecked. Story/spec/sprint/
  CI lifecycle remains `in-progress`, AC1/AC3 pass, AC2/AC4 remain open, and acceptances remain 0/3.
  Further hardening or `bmad-build` attempts are prohibited until the approved follow-up proposal's
  full external restart gate is satisfied. The existing terminal-closure proposal remains on hold.
```

### 5.4 Sprint status

**Artifact:** `_bmad-output/implementation-artifacts/sprint-status.yaml`
**Section:** Add comments immediately above the unchanged Story 3.13 row.

**OLD:**

```yaml
  3-12-multi-platform-eventstore-container-publishing-correction: done
  3-13-deployed-runtime-parity-closure: in-progress
```

**NEW:**

```yaml
  3-12-multi-platform-eventstore-container-publishing-correction: done
  # 2026-08-12 post-handoff disposition: bmad-build halted correctly at its Step 3 gate.
  # Repository-owned hardening is complete; Tasks 4-7 and 9 still require external evidence.
  # Story 3.13 remains in-progress: AC1/AC3 pass, AC2/AC4 remain open, acceptances are 0/3.
  # Do not rerun hardening or bmad-build until the approved follow-up proposal's full restart gate
  # is satisfied. The approved terminal-closure proposal is preserved on operational hold.
  3-13-deployed-runtime-parity-closure: in-progress
```

The value remains `in-progress`; this is not a lifecycle transition.

### 5.5 CI documentation

**Artifact:** `docs/ci.md`
**Section:** Append immediately after the current Story 3.13 `in-progress` paragraph.

**OLD:**

> The retained Story 3.13 packet is deliberately `fail-closed`: predecessor integrity and
> fail-closed behavior satisfy AC1 and AC3, while the exact same-lineage deployed proof and all
> three content-bound acceptances required by AC2 and AC4 remain absent. The current acceptance
> count is 0 of 3, so Story 3.13 remains `in-progress` and authorizes no release, registry,
> deployment, consumer, or predecessor mutation.

**NEW:** Preserve the quoted paragraph exactly and append:

```markdown
The 2026-08-12 post-handoff `bmad-build` run halted correctly at its Step 3 implementation gate.
Repository-owned Story 3.13 hardening is complete and locally verified; external evidence closure
is not. Tasks 4–7 and 9 remain incomplete, AC2 and AC4 remain open, and acceptances remain 0/3. Do
not rerun Story 3.13 hardening or `bmad-build` until all restart conditions in the approved follow-up
Sprint Change Proposal are satisfied for one unchanged content-bound lineage. The existing approved
terminal-closure proposal is preserved on operational hold; terminal-`unavailable` re-scoping
remains a possible future governance decision but cannot currently close without Task 9 evidence.
```

### 5.6 This follow-up proposal

**Artifact:**
`_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-12-story-3-13-step-3-gate.md`

**OLD:** File absent.

**NEW:** This complete draft. On explicit approval, change only its proposal lifecycle metadata and
approval record as required by the Correct Course workflow; do not silently rewrite the reviewed
body.

### 5.7 Explicit no-edit set

No old-to-new edit is proposed for:

- PRD, architecture, UX, epics, or acceptance criteria;
- Story 3.13 Tasks 4–7 or 9 checkboxes;
- runtime code or test code;
- proof packet, identity crosswalk, review subject/roster, evidence JSON, logs, raw OCI bytes, or
  checksum manifests;
- package/release workflows, manifests, tags, packages, registry objects, deployment state, or
  authority records;
- consumers, root-declared submodules, or predecessor evidence.

## 6. Exact Restart Conditions

The hold is conjunctive. Partial progress may be recorded by the external evidence owners, but it
does not authorize Story 3.13 hardening or a new `bmad-build` attempt. Restart is permitted only
after all five conditions below are simultaneously satisfied and independently verified for one
unchanged content-bound lineage:

1. **Exact package recovery:** all 14 approved
   `999.1.20-proof.fa2d1c9910f8` package archives are recovered from a content-addressed durable
   source, and each original archive is independently SHA-256 verified against the approved
   14-package manifest. Rebuilt or lookalike packages do not qualify.
2. **One same-source release-provenance chain:** one durable record binds the exact approved source
   SHA, all 14 independently verified package bytes, one release version/tag, workflow run and
   attempt, Builds execution SHA, publisher/validator identity, release-owner authority, and one
   immutable OCI index. Ancestry, compatibility, or cross-lineage splicing does not qualify.
3. **Complete digest-bound OCI response metadata:** for both `linux/amd64` and `linux/arm64`, retain
   each child-manifest and config response content type, `Docker-Content-Digest`, byte length, raw
   body, and raw-body SHA-256, with every descriptor/config/platform relation independently
   replayable from the packet.
4. **Equivalent Production runtime evidence on both platforms:** run the same digest-pinned,
   bounded, support-safe `/alive` contract under `Production` for both required platform children,
   retaining HTTP status, redirect count, observed platform, start/end/per-platform timestamps,
   exit codes, readiness outcome, cleanup outcome, and bounded log hashes. Both platforms must pass;
   environment/emulation inability remains unproven, not passed.
5. **All three content-bound acceptances:** the EventStore owner, Release owner, and Test Architect
   provide durable receipts accepting the same unchanged review-subject SHA-256, exact scope,
   limitations, decision, and lineage. The verified count must be 3/3. Any packet/evidence change
   invalidates the receipts until all three accept the replacement subject.

An external owner must document the satisfied full gate and request restart explicitly. The
repository must verify the gate read-only before any implementation workflow resumes.

## 7. Implementation And Handoff Boundaries

### Before explicit approval of this proposal

- The draft proposal file is the only authorized write.
- Do not modify Story 3.13, its spec, sprint status, `docs/ci.md`, or the existing approved proposal.
- Do not finalize or route an implementation handoff.
- Do not run `bmad-build`.

### After explicit approval

The approved implementation is limited to the exact additive documentation edits in section 5.
It remains a documentation/governance handoff, not a Story 3.13 build handoff.

**Product Owner / sprint governance:**

- Record approval of the evidence-hold disposition.
- Keep Story 3.13 and Epic 3 `in-progress`.
- Keep the existing terminal-closure proposal on operational hold.

**Developer/documentation owner:**

- Apply only the exact story, spec, sprint-comment, and `docs/ci.md` additions in section 5.
- Preserve AC text, task checkboxes, proof/evidence bytes, tests, and all external state.
- Run only narrow documentation/YAML/diff validation; do not run Story 3.13 tests or `bmad-build`.

**External evidence owners:**

- Own archive recovery, release provenance, registry response capture, Production runtime evidence,
  and durable acceptance receipts.
- Do not ask the repository implementation workflow to fabricate or infer any missing evidence.

**Future governance:**

- Terminal-`unavailable` re-scoping remains available only through another explicit Correct Course
  decision.
- The current approved terminal-closure proposal alone does not reactivate that work.
- Even after a future re-scope decision, closure remains impossible until the applicable
  content-bound Task 9 acceptance evidence is available and valid for the exact re-scoped subject.

### Success criteria for this immediate correction

1. The Step 3 halt is durably documented as correct.
2. Story 3.13, its spec, sprint status, and CI documentation remain `in-progress`.
3. AC1/AC3 remain passing; AC2/AC4 remain open; 0/3 acceptance remains explicit.
4. Tasks 4–7 and 9 remain unchecked and are not described as complete, passed, or not applicable.
5. Repository-owned hardening and external-evidence closure are clearly distinguished.
6. Further hardening and `bmad-build` are prohibited until the full restart gate passes.
7. The existing approved terminal proposal remains intact and explicitly on hold.
8. No prohibited artifact or external state changes.

## 8. Change Analysis Checklist Results

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] | Story 3.13 deployed-runtime parity closure. |
| 1.2 Core problem | [x] | External-evidence/acceptance gate after repository-owned implementation completed. |
| 1.3 Evidence | [x] | AC1/AC3 pass; AC2/AC4 open; 0/14 archives; missing same-lineage provenance; incomplete OCI response metadata; non-equivalent runtime evidence; 0/3 acceptances; Step 3 halt reported by Administrator. |
| 2.1 Current epic viability | [x] | Epic 3 remains viable and `in-progress`; no scope rewrite needed now. |
| 2.2 Epic-level change | [N/A] | No epic scope, order, or acceptance change. |
| 2.3 Remaining epics | [x] | No other epic is impacted. |
| 2.4 New epic need | [N/A] | No new epic or story is required for the immediate hold disposition. |
| 2.5 Ordering/priority | [x] | Pause Story 3.13 implementation attempts; external evidence owners act before any restart. |
| 3.1 PRD conflict | [x] | No PRD conflict or change; FR36 remains fail closed. |
| 3.2 Architecture conflict | [x] | No architecture conflict or AD-22 change. |
| 3.3 UX conflict | [N/A] | No UI or journey impact. |
| 3.4 Other artifacts | [x] | After approval, additive story/spec/sprint-comment/CI documentation only. |
| 4.1 Direct adjustment | [x] Viable | Low implementation effort and low technical risk; preserves truthful state. |
| 4.2 Potential rollback | [x] Not viable | Does not create evidence and would weaken verified hardening. |
| 4.3 MVP review | [x] Not required | No requirement or MVP-scope change. |
| 4.4 Selected path | [x] | Documentation-only external-evidence hold. |
| 5.1 Issue summary | [x] | Trigger, evidence, and Step 3 disposition are explicit. |
| 5.2 Impact | [x] | Epic, story, artifact, and no-change boundaries are explicit. |
| 5.3 Recommended path | [x] | Option A selected with trade-offs and future governance path. |
| 5.4 MVP/action plan | [x] | MVP unchanged; exact documentation and restart plan defined. |
| 5.5 Handoff plan | [x] | Proposed roles and strict boundaries defined; no handoff started. |
| 6.1 Checklist review | [x] | All applicable analysis items addressed. |
| 6.2 Proposal accuracy | [x] | Cross-checked against story, spec, sprint status, CI docs, proof packet, and retained evidence JSON. |
| 6.3 Explicit approval | [!] | Pending Administrator review; no approval inferred. |
| 6.4 Sprint status update | [!] | Prohibited until explicit approval; proposed value remains `in-progress`. |
| 6.5 Handoff confirmation | [!] | Pending explicit approval; no implementation handoff finalized. |

## 9. Explicit Approval Gate

This proposal is a draft. Creating this file does not approve implementation, reactivate the
existing terminal proposal, authorize a handoff, change sprint tracking, or authorize
`bmad-build`.

**Approval requested:** Approve the complete documentation-only evidence-hold proposal exactly as
written, including the operational hold on the existing approved proposal, the conjunctive restart
gate, and the prohibition on further hardening/`bmad-build` attempts.

Valid responses are:

- **Approve** — authorize only the exact additive documentation edits in section 5 and the bounded
  handoff in section 7.
- **Revise** — keep the draft and all current artifacts unchanged while requested revisions are
  incorporated and re-presented.
- **Reject** — retain current artifacts and the existing approved proposal unchanged; no handoff or
  tracking mutation occurs.

Until an explicit **Approve** response is recorded, Story 3.13, its spec, sprint status, CI
documentation, evidence, tests, and all external state remain untouched.
