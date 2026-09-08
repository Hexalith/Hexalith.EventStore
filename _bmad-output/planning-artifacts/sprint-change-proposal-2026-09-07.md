# Sprint Change Proposal — 2026-09-07

**Trigger:** Epic 3 retrospective verdict `rejected`
(`_bmad-output/implementation-artifacts/epic-3-retro-2026-09-07.md`)
**Prepared for:** Administrator
**Mode:** Batch
**Scope classification:** **Moderate** — sprint-tracking reorganization plus a red-build repair.
No PRD, architecture, epic, or UX content changes.

---

## Section 1 — Issue Summary

The Epic 3 retrospective rejected the epic. The rejection is not a judgement that delivery was
weak; it is that **two of the epic's own declared closure conditions are open and the governing
test suite is red**. The tracking file asserts a state the repository disproves.

`epic-3-context.md:54` is the authority:

> "Epic 3 stays open until positive Story 3.15 parity and all other Epic 3 stories, **including
> 3.16**, complete."

Neither half holds.

### Evidence — re-verified in this session against primary sources

Each claim below was checked directly, not carried over from the retrospective narrative.

**1. Story 3.16 is in the epic but untracked.**
`epics.md:2809-2880` declares Story 3.16 ("Latest-Compatible Dependency And Root Submodule
Refresh") with full Given/When/Then acceptance criteria, and `epics.md:2880` marks it part of the
Epic 3 story set. `sprint-status.yaml:91-107` tracks `3-1` through `3-15` and has **no `3-16-*`
key**. Consequence: `detect-epic --epic 3` reported `pending_stories: []` on a sixteen-story epic —
an untracked story is indistinguishable from a finished one.

The 2026-09-06 chore spec `spec-update-all-hexalith-packages-to-latest.md` does **not** close it.
Verified: its Boundaries state *"**Never:** Upgrade non-Hexalith dependencies, .NET, or Aspire"*,
while Story 3.16's AC at `epics.md:2849` explicitly requires Microsoft.OpenApi 3.x, xUnit 4.x,
Roslyn 5.9 and Aspire 13.5 compatibility validation. The chore advanced three Hexalith families
(Memories, Parties, Tenants) against Story 3.16's declared scope of 284 audited rows / 43
stable-pin candidates / 4 prerelease candidates across 15 audit families.

**2. Story 3.15 is `done` but its packet fails closed.**
Re-run in this session:

```
$ python3 tools/validate-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb01…/closure.json \
    --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb01…
[corrected-deployed-runtime-parity] fail: exactly three packet-bound receipts are required; …
EXIT=1
```

`sprint-status.yaml:106` says `done`. The story's own artifact and its verifier both say the
packet grants nothing at 0 of 3 receipts. `architecture.md:354` states nothing closes positive
FR36 parity without Story 3.15, so **FR36 remains open**.

**3. `Contracts.Tests` is red at HEAD.**
Executed at HEAD `d45206f7`: `Total: 1942, Errors: 0, Failed: 2, Skipped: 0` (125s).

- `CorrectedDeployedRuntimeParityClosureTests.SubjectRestatingSurfacesNameTheCurrentSubject`
  (`:2815`) — `sprint-status.yaml` must contain the current Story 3.15 subject digest
  `a5c07d17…c3448`; it contains no 64-hex digest at all.
- `ProofPacketValidatorIntegrityTests.PacketAuthorizingSprintTransformMatchesNestedBlockerComments`
  (`:296`) — the Story 1.20 closure line must appear exactly once; found zero.

Cause confirmed in the diff: HEAD commit `d45206f7` *"remove outdated comments and streamline
development status"* deleted comment blocks that two guards bind to as assertions.

### Correction to the retrospective's finding A3

The retrospective states A3 as *"restore the two sprint-status comment lines."* That undercounts
the repair. `PacketAuthorizingSprintTransformMatchesNestedBlockerComments` asserts a **five-line
exact, ordered, contiguous block** (`ProofPacketValidatorIntegrityTests.cs:285-305`) and fails on
the first missing member. Presence check against the current worktree:

| Guard-bound line | Present |
|---|---|
| `  1-19-correct-paged-rebuild-and-replay-equivalence: done` | yes |
| `  # Story 1.20 owner-approved parity closure is complete; authorizing commit C` | **no** |
| `  # verified every pinned artifact, approval, prerequisite, and migration decision.` | **no** |
| `  # Explicitly excludes payload-protection G5 and Parties Story 8.7; those are Epic 8.` | **no** |
| `  1-20-owner-approved-parity-closure-and-runtime-pin: done` | yes |

**Three** comment lines are missing for that guard, plus the Story 3.15 subject block for the
other — restoring only the line named in the failure message would move the failure to the next
line, not clear it.

---

## Section 2 — Impact Analysis

### Epic impact

| Epic | Impact |
|---|---|
| **Epic 3** | Correctly `in-progress`. Gains one tracked story (3.16, unstarted) and loses one false `done` (3.15). Cannot close until 3.15 parity is positive and 3.16 completes. **No scope change.** |
| Epics 1, 2 | None. Story 1.20 / Epic 1 rows are unchanged; only the comment prose around them is restored. |
| Epic 4 | None. Independent lineage. |

### Story impact

| Story | Now | Proposed | Why |
|---|---|---|---|
| 3.15 | `done` | `in-progress` | Verifier exits 1 at 0/3 receipts. `done` asserts what the repo disproves. |
| 3.16 | *(untracked)* | `backlog` | Declared in `epics.md`; `backlog` is defined in-file as *"Story only exists in epic file"* — an exact match for its state. |
| 1.20 / 1.19 | `done` | unchanged | Status rows are correct; only guard-bound comments were deleted. |

### Artifact conflicts

**No conflicts requiring content change.** The planning artifacts are already correct and are what
proves the tracking file wrong:

- **PRD** — no change. FR36's definition is unaffected; it simply remains open. MVP scope unchanged.
- **Architecture** (`architecture.md:354`, `:591`) — no change. Already states 3.15 gates positive
  FR36 parity. This correction makes tracking agree with it.
- **Epics** (`epics.md:2809-2880`) — no change. Story 3.16 stays in the epic set per your decision.
- **epic-3-context.md:54** — no change. Its closure condition is the authority being honoured.
- **UX** — N/A. No user-facing surface involved.

### Technical impact

- Repairs a live red build (2 of 1,942). No production source touched.
- **Guard-safety checks performed before proposing these edits:**
  - No test binds `3-15-…: done` as a status value (`grep` across `tests/**/*.cs`).
  - No test asserts the epic-3 key set or story count.
  - `DeployedRuntimeParityClosureTests:4214` binds `3-13` only — unaffected.
  - `Dw6BookkeepingAtddTests:43` constrains only its own DW story key on `review` — unaffected.
  - `Oq8PlatformClosureTests` operates on temp fixtures, not the live file.
  - `backlog` and `in-progress` are both tokens already defined and in use in this file
    (41 and 6 occurrences).
- The working tree already carries the retrospective's 19 appended action items (uncommitted).
  **All edits below preserve that work**; none reverts or restages it.

---

## Section 3 — Recommended Approach

**Selected path: Option 1 — Direct Adjustment.** No rollback, no MVP review.

| Option | Verdict | Reasoning |
|---|---|---|
| 1. Direct Adjustment | **Selected** | The plan is sound; only the tracking file misreports it. Effort **Low**, risk **Low**. |
| 2. Rollback | Not viable | Nothing to roll back. Story 3.14 genuinely closed; 3.15 correctly refused to declare unproven parity. Reverting `d45206f7` wholesale would clobber the uncommitted action items. |
| 3. MVP review | Not needed | FR36 is unchanged and still achievable — it is blocked on three owner receipts, not on scope. |

**Rationale.** The retrospective's own conclusion applies: *the tooling failing closed at 0 of 3
receipts is the system working.* The defect is that `sprint-status.yaml` says `done` over the top
of it. The fix is to make the tracking file tell the truth and to repair the guards the HEAD commit
broke — not to change what the project is building.

**Timeline impact:** none from these edits. Epic 3 closure remains gated on two owner-owned
actions that this proposal does not itself perform: collecting three receipts on issue #352, and
executing Story 3.16.

---

## Section 4 — Detailed Change Proposals

All five edits are in `_bmad-output/implementation-artifacts/sprint-status.yaml` except EDIT 5.

### EDIT 1 — Restore the Story 1.20 closure block *(unblocks the red build)*

**Location:** between lines 71 and 72. **Rationale:** restores three of five lines
`ProofPacketValidatorIntegrityTests.cs:296` requires as one exact ordered block. Text restored
verbatim from `46bdc176`.

```diff
   1-19-correct-paged-rebuild-and-replay-equivalence: done
+  # Story 1.20 owner-approved parity closure is complete; authorizing commit C
+  # verified every pinned artifact, approval, prerequisite, and migration decision.
+  # Explicitly excludes payload-protection G5 and Parties Story 8.7; those are Epic 8.
   1-20-owner-approved-parity-closure-and-runtime-pin: done
```

### EDIT 2 — Restore the Story 3.15 subject block *(unblocks the red build)*

**Location:** above line 106. **Rationale:** `CorrectedDeployedRuntimeParityClosureTests.cs:2815`
requires the current subject digest present and to be the first known subject digest in the file.
The file currently holds no 64-hex digest, so ordering is satisfied on restore. Verbatim from
`46bdc176`; its content also independently documents the fail-closed state that EDIT 3 records.

```diff
   3-14-corrective-oci-provenance-release: done
+  # 2026-09-06 (Group A tools patch): the packet fails closed at 0 of 3 receipts and grants nothing.
+  # The Group A re-mint rejected the three 86c59c79... receipts collected on 2026-09-05; those
+  # bytes remain under evidence/story-3-15/superseded-acceptances/. Collecting three fresh receipts
+  # on issue #352 is an Ask First owner action and was not performed.
+  # Acceptance lives on dedicated issue #352 -- never #324/#346/#351, which are other
+  # lineages. The current canonical subject is
+  # a5c07d178412d8fbac72ec660a3c0a94826a823f7376c61e0e7b98ea554c3448; the packet
+  # subject has now been re-minted eight times across nine subjects; receipts existed for only four
+  # of them, and all four sets are retained byte-for-byte under
+  # evidence/story-3-15/superseded-acceptances/.
+  # closure.json's deployed_runtime_parity/selected_deployed_identity are the claim the three roles
+  # are asked to accept, granted only at 3/3; at 0/3 the verifier exits 1 and grants nothing.
+  # Caveat: both owner roles resolve to one authenticated account, the Test Architect record is
+  # self-attested, and every receipt is tooling-composed -- so 3/3 is not three-party review.
   3-15-corrected-deployed-runtime-parity-closure: done
```

### EDIT 3 — Move Story 3.15 off `done` *(retro A2)*

**Rationale:** the verifier exits 1. `in-progress` is the honest token; the story is real work
awaiting an owner action, not abandoned. No guard binds this value.

```diff
-  3-15-corrected-deployed-runtime-parity-closure: done
+  3-15-corrected-deployed-runtime-parity-closure: in-progress
```

### EDIT 4 — Track Story 3.16 *(retro A1)*

**Rationale:** makes the completeness gate able to see the sixteenth story. `backlog` is defined
in this file as *"Story only exists in epic file"* — precisely 3.16's state. Key follows the
existing `<epic>-<story>-<slug>` convention.

```diff
   3-15-corrected-deployed-runtime-parity-closure: in-progress
+  # Declared at epics.md:2809-2880 and required by epic-3-context.md:54 for Epic 3 closure.
+  # The 2026-09-06 chore spec-update-all-hexalith-packages-to-latest does NOT close this story:
+  # it excludes non-Hexalith, .NET and Aspire upgrades, which epics.md:2849 requires.
+  3-16-latest-compatible-dependency-and-root-submodule-refresh: backlog
   epic-3-retrospective: done
```

### EDIT 5 — Story 3.15 spec frontmatter coherence *(flagged for your decision)*

**File:** `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`

This is the same claim on a different surface: the spec frontmatter reads `status: 'done'` while
the packet fails closed. Precedent for treating spec frontmatter as a lifecycle surface is in the
project's own guard design — `DeployedRuntimeParityClosureTests.cs:4230-4237` pins Story 3.13's
frontmatter for exactly this reason.

```diff
-status: 'done'
+status: 'in-progress'
```

**Flagged because:** no guard currently requires it, and it edits a story spec rather than the
tracking file. Verified safe — the subject-digest guard on this file checks digest content and
ordering, not frontmatter. **Recommend applying** for coherence; skip if you prefer spec records
frozen post-approval.

### Not proposed here — epic-3 status

`epic-3: in-progress` is **correct and stays as-is**. Per `epic-3-context.md:54` it cannot move to
`done` until 3.15 parity is positive and 3.16 completes.

---

## Section 5 — Implementation Handoff

**Scope: Moderate** — backlog reorganization plus a build repair. Routes to Product Owner /
Developer. No PM or Architect escalation: nothing in the PRD, architecture, or epic definitions
changes.

| # | Action | Owner | Gate |
|---|---|---|---|
| 1 | Apply EDITs 1-2 | Developer | `Contracts.Tests` returns 1942/1942 |
| 2 | Apply EDITs 3-4 (+5 if approved) | Product Owner / Developer | YAML parses; suite still green |
| 3 | Collect three receipts on issue **#352**, re-run the assembler | EventStore owner + Release owner + Test Architect | **Ask First / owner-gated — not performed by this proposal** |
| 4 | Schedule Story 3.16 | Product Manager (John) | Story moves `backlog` → `ready-for-dev` |

### Success criteria

1. `Contracts.Tests` green at 1942/1942.
2. `sprint-status.yaml` parses and makes no claim the repository disproves.
3. `detect-epic --epic 3` reports `story_count: 16` with 3.15 and 3.16 pending.
4. Epic 3 remains `in-progress` with both open closure conditions visible.

### Deferred — recorded, not actioned here

Retrospective items **A4-A19** (guards green by construction; the 3.13/3.14/3.15 codec boundary;
`canonical_bytes` in four versions) are already appended to `sprint-status.yaml` as action items
and are out of scope for this correction. Retrospective open questions **3** (`bypass-validation`
break-glass) and **5** (codec consolidation) remain open owner decisions; Q5 should wait on Story
3.15 closing, since consolidating now would re-mint its subject again.

### Residual risk

Epic 3 cannot close on these edits alone. Two owner-owned actions remain — the three #352 receipts
and Story 3.16 execution. This proposal makes both **visible in tracking** rather than hidden
behind a `done` row and an absent key.

---

## Section 6 — Execution Record (applied 2026-09-08)

**Approved by:** Administrator — "Approve, apply edits now" + EDIT 5 approved.
**Applied:** EDITs 1-5, all in the working tree. **Not committed, not pushed.**

| Check | Result |
|---|---|
| `sprint-status.yaml` diff vs pre-edit backup | exactly the 5 proposed edits, nothing else |
| Retrospective action items preserved | 19 epic-3 items, 39 total — intact |
| YAML parses | OK, 126 `development_status` keys |
| `Contracts.Tests` **before** | Total 1942, **Failed 2**, exit **1** |
| `Contracts.Tests` **after** | Total 1942, **Failed 0**, exit **0** ✅ |
| Story 3.15 verifier after edits | still exits **1** at 0/3 receipts — correctly unchanged |

The last row is the point of the exercise: the tracking file now reports the fail-closed state
instead of contradicting it. Nothing in this correction manufactured closure.

### Epic 3 state after the correction

`epic-3: in-progress`, with both closure conditions now **visible** rather than hidden:

- `3-15-corrected-deployed-runtime-parity-closure: in-progress` — blocked on three owner receipts
  on issue #352 (Ask First; not performed).
- `3-16-latest-compatible-dependency-and-root-submodule-refresh: backlog` — unstarted; the
  2026-09-06 chore spec does not close it.

`detect-epic --epic 3` will now see 16 stories with 2 pending, instead of 15 with 0.

### Remaining owner actions (not performed by this proposal)

1. Collect three receipts on issue **#352** and re-run the assembler — EventStore owner, Release
   owner, Test Architect.
2. Schedule Story 3.16 (`backlog` → `ready-for-dev`) — Product Manager.
3. Retrospective items A4-A19 remain open in `action_items`.
4. Open questions 3 (`bypass-validation` break-glass) and 5 (codec consolidation — should wait on
   3.15 closing) still need owner decisions.

### Follow-up recommended

Superseded by Section 7 below — the recommendation was investigated and found to be wrong.

---

## Section 7 — Follow-up executed, and a correction to Section 6's recommendation

Section 6 recommended *"rebinding those two guards to structured data."* On investigation
**that recommendation was wrong and was not carried out.** What follows is why, and what was
done instead.

### Why the guards must not be rebound

The comment lines are **not incidental prose**. They are the declared output of a frozen,
byte-verified transform:

- `1-20-owner-approved-parity-closure-proof-packet.md:5030-5041` contains an `awk` script that
  rewrites the Story 1.19 blocker comments into the three Story 1.20 closure comments, rebuilds
  the expected `sprint-status.yaml` from evidence commit **B**, and `cmp --silent`s it against
  commit **C**. Both sides are read with `git show <commit>:<path>` — historical objects, not the
  working file, so live edits to this file cannot disturb the packet's own verification.
- `ProofPacketValidatorIntegrityTests.cs:308-316` then asserts the **test constants match the
  packet's awk literals** (`print "{completedStart}"`, `$0 == "{blockerStart}" {` …). Changing the
  constants would break the test↔packet binding, and repairing the packet is explicitly out of
  bounds — `sprint-status.yaml`'s own Story 1.21 note records that it *"grants no
  predecessor-byte repair authority by itself."*
- `SubjectRestatingSurfacesNameTheCurrentSubject` already binds **to** structured data
  (`closure.json`). Its purpose is to catch a narrative surface drifting away from that structured
  truth. "Rebinding it to structured data" would delete the only thing it does.

**Both guards are working exactly as designed.** The defect was never the binding — it was that
the contract is invisible to anyone editing the file. `d45206f7` was a readability cleanup that
had no way to know.

### What was done instead — make the contract self-announcing

Three comment insertions in `sprint-status.yaml`. No guard, test, packet or evidence file was
touched.

1. **Header notice** (after `WORKFLOW NOTES`) — "GUARDED COMMENT BLOCKS — READ THIS BEFORE EDITING
   ANY COMMENT IN THIS FILE", stating that some comments are executable contract, that they are
   fenced with `>>> GUARDED … <<<` markers, that deleting them deletes evidence rather than
   tidying the file, and giving the exact command to verify a comment edit.
2. **Fence around the five-line closure block** — placed strictly *outside* it, since the guard
   requires contiguity. Names the frozen packet as the source, warns against reflow/reorder/insert,
   cites `d45206f7`, and warns that the failure message names only the first missing line.
3. **Fence above the Story 3.15 digest block** — explains it is a drift guard and that the current
   subject must be named before any superseded one.

**Placement constraints respected** (each verified): nothing was inserted *inside* the contiguous
five-line block; no marker text reproduces the two blocker lines the guard asserts are absent; no
marker introduces a 64-hex string that could displace the current subject digest as the first one
in the file.

| Check | Result |
|---|---|
| Guarded block still contiguous (lines 92-96) | intact |
| YAML parses | OK — 126 status keys, 39 action items |
| `Contracts.Tests` after markers | **1942/1942, exit 0** ✅ |

The guard was observed failing at HEAD and passing after repair, so its liveness is established
by observation rather than by construction — the standard this epic's own retrospective set.

### What remains genuinely open

Making the contract discoverable reduces the chance of recurrence but does not make it
impossible; a determined cleanup can still delete a fenced comment. A structural fix (for example,
generating the guarded region from the packet rather than hand-maintaining it) would need
authority over Story 1.20's frozen evidence, which no current story carries. Recorded, not
actioned.
