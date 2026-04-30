# Post-Epic-3 R3-A8: Retro Follow-Through Gate in Story Creation

Status: superseded

<!-- Source: sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md - Proposal 7 (R3-A8) -->
<!-- Source: epic-3-retro-2026-04-26.md - Action item R3-A8 + §10 takeaway #3 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

> **Cancellation note (2026-04-30, project lead).** This story is superseded without implementation. AC #1, #2, and #6 require edits inside `.claude/skills/bmad-create-story/template.md` and `workflow.md`, which are part of the BMAD method and off-limits per project rule. The fallback path the original sprint-change-proposal §7 allowed (`CLAUDE.md` only) was rejected as too weak — a memory-based pointer reproduces the failure mode the gate was meant to fix. The original failure (R2-A8 carry-over drifting through Epic 3 closure) is mitigated portfolio-side by R3-A5 (resolve/reclassify all post-Epic-1/2 backlog cleanup stories), which already exists. R3-A8b sibling row removed from `sprint-status.yaml` since it was conditional on this story landing. The full v3 spec below is preserved as historical record only.

## Story

As a **scrum master / process owner closing the Epic 3 retrospective backlog**,
I want the `bmad-create-story` skill to enforce an explicit "prior retro follow-through" check before each new story's acceptance criteria are finalized,
so that critical retro action items can no longer drift into `backlog` while a dependent epic closes and so the recurring "I closed Epic N but R(N-1)-Ax was still open" pattern that the Epic 3 retro identified (§4 #1, §10 #3) becomes mechanically impossible rather than memory-dependent.

## Story Context

The Epic 3 retrospective (`epic-3-retro-2026-04-26.md`) recorded that several Epic 1 and Epic 2 cleanup actions stayed `backlog` while Epic 3 closed:

- R2-A8 (`SubmitCommandHandler` NullRefs) was nominally critical-before-Epic-3-starts but was not closed until 2026-04-27 — *after* Epic 3 was already done in `sprint-status.yaml`.
- R2-A3, R2-A4, R2-A5 carry-overs each remained `backlog` while Epic 3 progressed.
- R3-A5 ("resolve or reclassify all post-Epic-1 and post-Epic-2 backlog cleanup stories") was filed as a portfolio-management action precisely because there was no per-story gate to catch the drift earlier.

§10 takeaway #3 of the retro is the verbatim mandate: **"Prior retrospectives need a gate, not a memory."** R3-A8 is the implementation of that takeaway.

This is **not** a code change story. The deliverable is a process artifact:

1. A new "Prior Retro Follow-Through" section in the `bmad-create-story` template.
2. A new pre-write step in the `bmad-create-story` workflow that requires the dev/SM running the skill to populate that section before the story's acceptance criteria are finalized.
3. A short CLAUDE.md pointer so humans and LLM agents reading CLAUDE.md know the gate is mandatory and where it lives.
4. Self-evidence: this story file already contains a filled-in `## Prior Retro Follow-Through` section (see below), which is the canonical example the template/workflow point to.

**Scope boundary (what this story does NOT do):**

- It does NOT retroactively backfill prior-retro sections into already-`done` stories.
- It does NOT resolve open carry-over stories (that is R3-A5's job — out of scope).
- It does NOT reconcile pre-existing template drift between `_bmad/bmm/4-implementation/bmad-create-story/` (BMAD module source copy, missing the project-specific "Testing Standards" section) and `.claude/skills/bmad-create-story/` (the live, project-customized skill the runtime executes). The live skill at `.claude/skills/bmad-create-story/` is the **only** authoritative edit target for this story per the proposal §7 ("Edit target: story-creation skill/template or `CLAUDE.md` if no local template is available"); the `_bmad/bmm/` upstream copy already differs for unrelated reasons and is left as-is.

## Prior Retro Follow-Through

*(This section is the canonical worked example referenced by the new template gate, conforming to the row schema pinned in AC #1. It is filled in here for R3-A8 itself to prove the gate is self-applicable — the story-creation gate created by this story is also exercised by this story.)*

| Retro file | Action ID | Status | Reason |
|---|---|---|---|
| [`epic-3-retro-2026-04-26.md`](epic-3-retro-2026-04-26.md) | R3-A5 | Intentionally deferred | R3-A5 is portfolio management (resolve all post-Epic-1/2 backlog); R3-A8 is the per-story gate. R3-A8 reduces the *recurrence rate* of the R3-A5 class without resolving the existing R3-A5 backlog. |
| [`epic-3-retro-2026-04-26.md`](epic-3-retro-2026-04-26.md) | R3-A8 | In scope | This story IS R3-A8's implementation. |
| [`epic-12-retro-2026-04-30.md`](epic-12-retro-2026-04-30.md) | R12-A5 | Not applicable | Projection-builder concern; `post-epic-11-r11a1-checkpoint-tracked-projection-delivery` already `ready-for-dev`; R11-A2/A3/A4 are intentional `backlog`. Does not depend on the story-creation gate. |
| [`epic-4-retro-2026-04-26.md`](epic-4-retro-2026-04-26.md) | R4-A2 | Not applicable | Story 4.3 execution-record concern; R4-A2's story will pass through the gate at its own creation time. |
| [`epic-4-retro-2026-04-26.md`](epic-4-retro-2026-04-26.md) | R4-A5 | Not applicable | Tier 3 pub/sub delivery concern; same routing as R4-A2. |
| [`epic-4-retro-2026-04-26.md`](epic-4-retro-2026-04-26.md) | R4-A6 | Not applicable | Drain integrity guard; same routing as R4-A2. |
| [`epic-4-retro-2026-04-26.md`](epic-4-retro-2026-04-26.md) | R4-A8 | Not applicable | Story-numbering comments cosmetic; same routing as R4-A2. |
| [`epic-11-retro-2026-04-30.md`](epic-11-retro-2026-04-30.md) | R11-A2 | Not applicable | Polling-mode product behavior; sequenced after R11-A1; gate runs at R11-A2 story creation. |
| [`epic-11-retro-2026-04-30.md`](epic-11-retro-2026-04-30.md) | R11-A3 | Not applicable | AppHost projection proof; same routing as R11-A2. |
| [`epic-11-retro-2026-04-30.md`](epic-11-retro-2026-04-30.md) | R11-A4 | Not applicable | Valid projection round-trip; same routing as R11-A2. |

**Risk if the gate is delayed:** The next story created from `backlog` (e.g., `post-epic-4-r4a2-story-4-3-execution-record`) will be authored without an explicit prior-retro check, repeating the exact failure mode the Epic 3 retro flagged.

## Acceptance Criteria

1. **Template gate section exists.** `.claude/skills/bmad-create-story/template.md` gains a new top-level section titled exactly `## Prior Retro Follow-Through` placed between `## Story` and `## Acceptance Criteria`. The section contains:
   - A one-sentence purpose statement explaining the gate's intent.
   - A 4-item checklist adapted from `sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md` Proposal 7. **Item 1 departs from the proposal's verbatim wording** because patch #3 (party-mode review, 2026-04-30) widened the scan rule from latest-retro-only to multi-retro; the verbatim text is preserved in italics for traceability:
     - `[ ] Review every completed epic retrospective with still-open carry-over actions.` *(amended from proposal §7's "Review **the latest** completed epic retrospective" — see AC #2's multi-retro mandate)*
     - `[ ] List any carry-over actions that affect this story.`
     - `[ ] Confirm whether each action is completed, superseded, or intentionally deferred.`
     - `[ ] If the story depends on an open retro action, either include it in scope or document the risk.`
   - A pointer to this story's filled-in `## Prior Retro Follow-Through` section as the canonical example: `_bmad-output/implementation-artifacts/post-epic-3-r3a8-retro-follow-through-gate.md#prior-retro-follow-through`.
   - **Non-empty contract.** The section is considered populated iff it contains either (a) a markdown table with ≥1 data row beyond the header (using the schema below), OR (b) the literal sentinel line `_No open carry-over actions surface for this story; gate ran YYYY-MM-DD._` (with `YYYY-MM-DD` resolved to today's UTC date). Empty body, header-only stub, or freeform prose without the sentinel fails the gate. This contract is what R3-A8b's CI grep asserts against.
   - **Row schema (when a table is used).** Table columns MUST be exactly four, in order: `Retro file` (markdown link to the retro `.md` file under `_bmad-output/implementation-artifacts/`) │ `Action ID` (single ID per row, e.g., `R3-A5`, `R7-A2` — no comma-separated lists) │ `Status` (one of: `In scope` / `Not applicable` / `Intentionally deferred` / `Superseded`) │ `Reason` (one short clause). Pinning the schema is what makes R3-A8b's CI grep fast and unambiguous (≤5 lines of bash regex instead of ≥30 lines of freeform parsing).

2. **Workflow gate step exists.** `.claude/skills/bmad-create-story/workflow.md` gains an explicit retro-check sub-block **folded into the existing Step 5 ("Create comprehensive story file"), placed immediately before the first `<template-output>` call** (currently at workflow line ~302, the `story_header` template-output). No new top-level `<step>` is created and no decimal step numbers (`n="4.5"`, etc.) are introduced — those are not part of the existing dialect. The sub-block MUST:
   - Direct the runtime to scan `_bmad-output/implementation-artifacts/epic-*-retro-*.md` and identify **every retrospective whose §8 (Action Items) or §11 (Commitments) contains action-item rows (`R<N>-A<n>`) that are still open** — closure determined by the filter precedence below. This is a **multi-retro scan**, not a latest-retro-only scan, because the failure mode that triggered R3-A8 (R2-A8 stayed open through Epic 3) is precisely the multi-retro-carry-over class.
   - **Filter precedence (closed-vs-open determination).** An action item `R<N>-A<n>` is **closed** if and only if at least one of the following holds:
     - **(a) Inline retro annotation.** The retro file's §6 (Technical Debt and Risks) or §8 (Action Items) row carries a closure marker — a `✅` glyph, a `Done <SHA>` annotation, an `addressed by <story-key>` reference, or a `Superseded` annotation. Pattern reference: `epic-3-retro-2026-04-26.md` line 130 (`R3-A3 ✅ ... — ✅ Done 1e4ea10 — addressed by post-epic-2-r2a2-...`).
     - **(b) Sprint-status `done` cross-reference.** A story key associated with the action exists in `_bmad-output/implementation-artifacts/sprint-status.yaml` and its status is `done`.
     - Plain `backlog`, `ready-for-dev`, or `review` story keys count as **still open** — `review` does NOT collapse to closed because the gate fires before code-review's flip-to-done.
     - Items with neither (a) nor (b) (no annotation, no associated story) default to **still open** — fail-loud, not fail-silent.
   - Require the new story file to contain a populated (per AC #1's non-empty contract) `## Prior Retro Follow-Through` section before the workflow proceeds to write acceptance criteria.
   - Allow `In scope`, `Not applicable`, `Intentionally deferred`, and `Superseded` as the four legitimate row Status values — the gate enforces explicit acknowledgment, not blanket scope expansion. Status values outside this set are a schema violation (AC #1).

3. **CLAUDE.md pointer exists.** `CLAUDE.md` gains a short note (≤6 lines) under a new `## Story Creation Process` subsection (or as a clearly-marked addition to an adjacent section such as `## Code Review Process`) stating:
   - That `bmad-create-story` enforces a prior-retro follow-through gate.
   - The location of the gate (`.claude/skills/bmad-create-story/template.md` + `workflow.md`).
   - That the gate is mandatory before acceptance criteria are finalized.
   - A reference to this story (`post-epic-3-r3a8-retro-follow-through-gate`) as the source of the rule and to Epic 3 retro §10 #3 as the rationale.

4. **Self-evidence is preserved.** This story file (`post-epic-3-r3a8-retro-follow-through-gate.md`) retains its filled-in `## Prior Retro Follow-Through` section above. The section is not deleted or moved during implementation. The new template (AC #1) cites this section by name as the worked example.

5. **Existing template content is not regressed.** The pre-existing project-specific additions to `.claude/skills/bmad-create-story/template.md` — specifically the `### Testing Standards (project-wide rules — apply to every story)` block (Tier 1/2/3 rules + ID-validation rule citing R2-A6 and R2-A7) — remain present and unchanged. The Allman-brace, file-scoped namespace, and other code-style guidance (if present) is also untouched. **Mechanical check:** running `git diff` on the template after the edit MUST show only additions in the new gate section; lines outside that section are unchanged.

6. **Workflow gate is encoded as runnable instructions, not prose.** The new sub-block inside Step 5 (per AC #2) uses the same `<action>`/`<check>`/`<output>` XML-tag dialect already used elsewhere in `workflow.md` (see e.g. existing Step 1 lines 54-208). The gate is not a free-text paragraph — it is a structured set of directives the runtime can execute. At minimum:
   - One `<action>` to glob `_bmad-output/implementation-artifacts/epic-*-retro-*.md` and load **all** matching retro files (multi-retro scan per AC #2).
   - One `<action>` to extract `R<N>-A<n>` rows from §8 / §11 of each loaded retro and cross-reference `sprint-status.yaml` to filter to still-open actions.
   - One `<check>` that the new story file contains a non-empty `## Prior Retro Follow-Through` section covering the filtered-open set before falling through to story-write.
   - One `<output>` block describing what to do if the section is missing or empty (re-prompt the dev / surface the gate, do not silently skip).

7. **Gate cost is bounded.** The new workflow sub-block does not require new external tools, MCP servers, or scripts. It uses only the file-globbing, file-reading, and yaml-reading capabilities already in scope for the existing `bmad-create-story` skill. The gate's marginal cost per story creation is bounded at "read N completed-retro files (today: ~13 retros, expected to grow ~1 per epic close) + read sprint-status.yaml + paste a small filtered-open-actions table"; if implementation tries to invoke a sub-agent, run an external script, or perform any I/O outside markdown/yaml file reads, the story is over-engineered and must be tightened. **Note:** the higher per-story file-read count (vs. the "1 retro file" bound this AC carried in the v1 draft) is the cost of fixing the multi-retro-carry-over blind spot — a deliberate trade. The R3-A8b sibling story (see Dev Notes) will move the assertion side of the gate to a CI hook so the gate's runtime cost in `bmad-create-story` does not balloon over time.

8. **Gate is not coupled to retrospective-status field semantics.** `sprint-status.yaml` records `epic-N-retrospective: done` or `optional`. The gate MUST work for both — every retro file present on disk under `_bmad-output/implementation-artifacts/epic-*-retro-*.md` is a valid input regardless of whether its `epic-N-retrospective` field reads `done` or `optional`. The gate MUST NOT misfire on epics whose retrospective is `optional` and was skipped (no retro file exists) — those epics simply contribute zero rows to the multi-retro candidate set (per AC #2). No new field or status value is added to `sprint-status.yaml`.

9. **The deferred-template-drift class is acknowledged but unresolved.** The Project Structure Notes (or Dev Notes) explicitly note that `_bmad/bmm/4-implementation/bmad-create-story/template.md` and `_bmad/bmm/4-implementation/bmad-create-story/workflow.md` are NOT updated by this story. The acknowledgment cites the existing pre-R3-A8 drift (the project-specific `### Testing Standards` block exists in `.claude/skills/` but not in `_bmad/bmm/`) as evidence that the two locations have already drifted for reasons unrelated to R3-A8. Cleanup is logged as a follow-up backlog candidate, not silently ignored.

10. **Sprint-status bookkeeping is closed.** `_bmad-output/implementation-artifacts/sprint-status.yaml` shows `post-epic-3-r3a8-retro-follow-through-gate` flipped from `ready-for-dev` to `review` (or `done` if `code-review` already ran). The file's leading-comment `last_updated:` line and the YAML `last_updated:` key both name this story and use today's UTC date. Same rule as `post-epic-3-r3a6` AC #10 / `post-epic-3-r3a7` AC #11 — bookkeeping is non-negotiable.

11. **Test impact is null-by-design.** This story changes only template/workflow/markdown files; no source code or test code is modified. Tier 1/Tier 2/Tier 3 pass counts MUST be unchanged from the pre-story baseline. The Dev Agent Record records "no code change → no test re-run required" with one-line rationale; if the dev runs tests anyway as a sanity check, baseline-equality is captured. A test that was green pre-story going red post-story is a defect even though the story is process-only — investigate before proceeding.

12. **R3-A8b sibling carve-out exists with explicit promotion trigger.** The CI-enforcement layer of this gate (a bash script + a `docs-validation.yml` workflow step that fails non-zero when a story file under `_bmad-output/implementation-artifacts/` is missing or has an empty `## Prior Retro Follow-Through` section per AC #1's non-empty contract) is **deliberately carved out** to a sibling story `post-epic-3-r3a8b-retro-gate-ci-enforcement`. The carve-out exists in `sprint-status.yaml` under the `# Post-Epic-3 Retro Cleanup` section with status `backlog`. R3-A8 is markdown-only by design (AC #11); R3-A8b adds the CI tooth.

    **Promotion trigger (R3-A8b: `backlog` → `ready-for-dev`).** Without an explicit trigger, R3-A8b can sit in `backlog` indefinitely and the gate stays markdown-only — exactly the failure mode advanced-elicitation finding F1 surfaced. R3-A8b MUST be promoted from `backlog` to `ready-for-dev` once **either** of the following first becomes true:
    - **Trigger A — observed coverage:** The R3-A8 gate has been observed in **≥3 created stories** with non-empty `## Prior Retro Follow-Through` sections. This proves the doc convention has survived contact with multiple story creations and is ready to be pinned by CI.
    - **Trigger B — calendar SLA:** The current epic at R3-A8 close (Epic 12) has its successor epic (Epic 13) close AND its retrospective is `done`. Trigger B is the hard ceiling that prevents R3-A8b from sliding into perpetual backlog if Trigger A's count never materializes.

    The promotion is performed at sprint-status update time (Bob, the project lead, or whichever skill triggers the flip) and is recorded in the same `last_updated` line that flips R3-A8b. Promotion does not require a fresh `bmad-create-story` invocation — flipping the YAML status alone is enough.

    **The carve-out is added by the same diff that creates R3-A8** (added at story-creation time as part of party-mode review patches, 2026-04-30); it is not a future-tense "should be filed" — verify its row exists.

## Tasks / Subtasks

- [ ] **Task 1 — Author the template gate section** (AC: #1, #4, #5)
  - [ ] 1.1 Open `.claude/skills/bmad-create-story/template.md`. Insert the new `## Prior Retro Follow-Through` section between `## Story` and `## Acceptance Criteria`.
  - [ ] 1.2 Section content: 1 sentence purpose statement + the verbatim 4-item checklist (matching the proposal Proposal 7 wording exactly) + a pointer line referencing this R3-A8 story file as the worked example.
  - [ ] 1.3 Run `git diff .claude/skills/bmad-create-story/template.md` and confirm only additions appear; the existing `### Testing Standards (project-wide rules — apply to every story)` block is untouched.

- [ ] **Task 2 — Author the workflow gate sub-block inside Step 5** (AC: #2, #6, #7, #8)
  - [ ] 2.1 Open `.claude/skills/bmad-create-story/workflow.md`. **Locate the existing `<step n="5" goal="Create comprehensive story file">` block (currently at line ~293) and the first `<template-output file="{default_output_file}">story_header</template-output>` call inside it (currently at line ~298).** Insert the new gate sub-block immediately BEFORE the first `<template-output>` call, still inside Step 5. Do NOT create a new top-level `<step>` and do NOT introduce decimal step numbers — Step 5 is the single insertion point.
  - [ ] 2.2 The sub-block uses the same `<action>`/`<check>`/`<output>` XML-tag dialect as Step 1 (lines 54–208). Do NOT introduce a new tag vocabulary.
  - [ ] 2.3 Encode the four directives from AC #6: glob `epic-*-retro-*.md` and load **all** matched files → extract `R<N>-A<n>` rows from §8 / §11 of each → cross-reference `sprint-status.yaml` to filter to still-open actions → require populated `## Prior Retro Follow-Through` section in the story file before story-write.
  - [ ] 2.4 Add a fallback for `epic-N-retrospective: optional` epics — the gate skips them if their retro file does not exist (AC #8). The `optional`-skipped case does NOT halt the gate; it just contributes zero rows to the candidate set.
  - [ ] 2.5 The gate's per-story cost stays within the AC #7 bound (read N retro files + sprint-status.yaml + populate a filtered table; no sub-agents, no scripts, no external I/O).

- [ ] **Task 3 — Add CLAUDE.md pointer** (AC: #3)
  - [ ] 3.1 Open `CLAUDE.md`. Add a `## Story Creation Process` subsection (or extend the existing `## Code Review Process` section with a clearly-marked addendum) of ≤6 lines stating: gate exists, location, mandatory before AC finalize, source = R3-A8, rationale = Epic 3 retro §10 #3.
  - [ ] 3.2 Confirm the addition does not exceed 6 lines and does not paraphrase or duplicate the gate's checklist text — point to the template, do not inline it.

- [ ] **Task 4 — Verify the gate self-applies** (AC: #4)
  - [ ] 4.1 Re-read this story file's `## Prior Retro Follow-Through` section (above). Confirm it satisfies the new template's checklist — i.e., the gate that this story creates is satisfied by this story itself.
  - [ ] 4.2 If the section needs tightening to match the new template's exact wording (e.g., a renamed column header), update this story file in the same diff.

- [ ] **Task 5 — Acknowledge the `_bmad/bmm/` drift class and the R3-A8b carve-out** (AC: #9, #12)
  - [ ] 5.1 In the `### Project Structure Notes` block of this story (already drafted below), confirm the line that names `_bmad/bmm/4-implementation/bmad-create-story/` as out-of-scope. Do not edit those upstream files.
  - [ ] 5.2 Confirm that `post-epic-3-r3a8b-retro-gate-ci-enforcement: backlog` exists in `sprint-status.yaml` under the `# Post-Epic-3 Retro Cleanup` section. The carve-out is added by the same diff that creates this story (added at story creation time; see Dev Notes "Sibling carve-out (R3-A8b)"); the dev's job in this task is verification, not creation.

- [ ] **Task 6 — Sprint-status bookkeeping** (AC: #10)
  - [ ] 6.1 Edit `_bmad-output/implementation-artifacts/sprint-status.yaml` so this story flips from `ready-for-dev` to `review` (Task 6 runs at story close, not at story creation; the create-story workflow has already flipped `backlog → ready-for-dev`).
  - [ ] 6.2 Update both the leading-comment `last_updated:` line and the YAML `last_updated:` key with today's UTC date and a one-line note naming this story.
  - [ ] 6.3 Verify the change with `git diff _bmad-output/implementation-artifacts/sprint-status.yaml` before committing.

- [ ] **Task 7 — Test-impact null-check** (AC: #11)
  - [ ] 7.1 Confirm the diff contains only `.claude/skills/bmad-create-story/template.md`, `.claude/skills/bmad-create-story/workflow.md`, `CLAUDE.md`, this story file, and `sprint-status.yaml`. No `.cs`, no `.csproj`, no test file.
  - [ ] 7.2 Record "no code change → no test re-run required" in the Dev Agent Record. If the dev opts to run a sanity check anyway, capture baseline-equality (Tier 1 = N at baseline → N post-story).

## Dev Notes

### Why this story is process-only, not code

R3-A8 is the implementation of Epic 3 retro §10 takeaway #3 — "Prior retrospectives need a gate, not a memory." Memory-based gates (e.g., "remember to check R2 actions before creating R3 stories") demonstrably failed: R2-A8 stayed `backlog` while Epic 3 closed. The fix is to encode the gate in the artifact that runs every story creation, so it cannot be forgotten by a fresh LLM context or a new dev.

There is no production code, no test suite, no Tier 1/2/3 baseline to move. The "test" of this story is whether the next story created after R3-A8 lands has a populated `## Prior Retro Follow-Through` section. That validation is observational, not assertive — caught by the next `bmad-create-story` invocation, not by `dotnet test`.

### Why `.claude/skills/bmad-create-story/` is the only edit target

Per the proposal §7: "Edit target: story-creation skill/template or `CLAUDE.md` if no local template is available." A local template DOES exist (`.claude/skills/bmad-create-story/template.md`), and that is the file the runtime actually loads when `/bmad-create-story` is invoked.

`_bmad/bmm/4-implementation/bmad-create-story/template.md` is the BMAD module library copy (the upstream/installer source). It already differs from the live skill copy for unrelated reasons — the live skill has a `### Testing Standards (project-wide rules — apply to every story)` block that the upstream module copy does not have. That pre-existing drift was deliberately introduced by an earlier project change and is independent of R3-A8. **Editing the upstream copy is out of scope** for this story; doing so would either (a) over-extend scope to also port the Testing Standards block, or (b) leave the two copies inconsistently drifted — neither helps. AC #9 makes the deferral explicit.

### Why the story self-applies the gate

R3-A8's own creation predates the gate it creates. To prevent a chicken-and-egg paradox where the very story that introduces the gate is exempt from it, this story file already contains a populated `## Prior Retro Follow-Through` section above (between `## Story Context` and `## Acceptance Criteria`). The new template (AC #1) points to this section as the canonical worked example. **This is intentional** — the gate is self-applicable from day one, not "starting next story."

### Workflow integration shape

The existing `workflow.md` Step 5 ("Create comprehensive story file") already controls the order of `<template-output>` calls that emit story sections. The Prior Retro Follow-Through gate is folded **inside Step 5**, immediately before the first `<template-output file="{default_output_file}">story_header</template-output>` call (currently at workflow line ~298), because:

- It produces section content that goes INTO the template, so it must run before any `<template-output>` call.
- It depends on `_bmad-output/implementation-artifacts/epic-*-retro-*.md` files which are loaded outside the existing `discover-inputs.md` pattern (those load PRD/architecture/epics/UX, not retros). The gate is the first place retros are read by the workflow.
- It is the only step that reads `sprint-status.yaml` for retro-status fields (`epic-N-retrospective: done | optional`) AND for cross-referencing still-open action items against story-key statuses, per AC #2 and AC #8.

A new top-level `<step n="4.5">` was considered and rejected — decimal step numbers are not part of the existing workflow.md dialect (Step 1 → Step 6 are integers). Folding into Step 5 preserves dialect consistency.

### Known coverage gap — story-creation skills outside `bmad-create-story`

R3-A8's gate fires only inside `.claude/skills/bmad-create-story/workflow.md`. Stories created via `bmad-quick-spec`, `bmad-correct-course`, or any future story-creating skill **do NOT execute the gate** — they bypass it structurally because they don't load `bmad-create-story`'s workflow. This is a known coverage gap surfaced during advanced-elicitation finding F6 (2026-04-30).

This is **not a defect of R3-A8** — porting the gate to N skills would N-tuple the maintenance surface and create drift between skill copies (the same drift class that already exists between `.claude/skills/bmad-create-story/template.md` and `_bmad/bmm/4-implementation/bmad-create-story/template.md`, per AC #9).

**The structural fix lives in R3-A8b's CI tooth.** When R3-A8b lands, the CI script grep-asserts the `## Prior Retro Follow-Through` section on **any** story file matching the story-key naming convention under `_bmad-output/implementation-artifacts/`, regardless of which skill authored it. CI doesn't care which skill ran; it only cares that the file conforms to AC #1's non-empty contract + row schema.

**Until R3-A8b ships,** devs should prefer `bmad-create-story` for any story that touches retro-implicated work. CLAUDE.md's gate pointer (AC #3) names this preference. For routine code fixes that have zero plausible retro carry-over, `bmad-quick-spec` remains a valid fast path — the gate isn't ceremony for the sake of ceremony.

### Sibling carve-out (R3-A8b — CI enforcement)

The CI tooth on this gate is **deliberately deferred to a sibling story** `post-epic-3-r3a8b-retro-gate-ci-enforcement`, surfaced during the party-mode adversarial review of R3-A8 on 2026-04-30 (Murat's risk-vs-value call: marginal CI cost ≤ 30 lines bash + 1 workflow step, prevents the entire R3-A5 recurrence class). Two-stage deployment rationale:

1. **R3-A8 (this story):** lands the gate as a markdown-only doc convention. Marginal cost: 5 file edits, no test impact, no CI surface change. Lets the gate survive ≥3 story creations before pinning it with CI — same pattern as `post-epic-2-r2a5` → `post-epic-2-r2a5b` (symptom fix first, structural cure second).
2. **R3-A8b (sibling):** adds a bash script `scripts/check-story-retro-gate.sh` that fails non-zero when a story file under `_bmad-output/implementation-artifacts/` is missing or has an empty `## Prior Retro Follow-Through` section (per AC #1's non-empty contract). Wires it into `.github/workflows/docs-validation.yml`'s `lint-and-links` job, mirroring the DAPR-version-pin check pattern from `post-epic-2-r2a5b` (PR #222, SHA `564034a`).

R3-A8b is sequenced **after** R3-A8 so the CI script has a real convention to assert against. R3-A8b is added to `sprint-status.yaml` as `backlog` by the same diff that creates R3-A8 (added at story-creation time as part of these party-mode review patches). Promotion from `backlog` to `ready-for-dev` is governed by AC #12's Trigger A / Trigger B.

**Forward-looking R3-A8b feature requirements** (captured here at R3-A8 spec-creation time so they aren't lost to memory before R3-A8b is picked up):

- **R3-A8b-FR1 (vacuous-NA warning, advanced-elicitation finding F7).** The CI script SHOULD additionally emit a non-blocking warning when the candidate set surfaced by the gate was **non-empty** AND **zero rows in the populated section have `Status` ≠ `Not applicable`**. That state is usually vacuous — the dev had real carry-overs to consider but reflexively wrote "Not applicable" for all of them. The warning is informational, not blocking; a story author can still ship a 100% Not-applicable section if every row genuinely doesn't apply (rare but possible). Implementation hint: count distinct `Status` values in the table; if `Not applicable` is the only value AND the candidate set had ≥1 still-open action across all retros, emit the warning.
- **R3-A8b-FR2 (skill-agnostic enforcement, advanced-elicitation finding F6).** The CI grep MUST fire on **any** story file matching the story-key naming convention under `_bmad-output/implementation-artifacts/`, regardless of which skill authored it (`bmad-create-story`, `bmad-quick-spec`, `bmad-correct-course`, etc.). This closes the F6 coverage gap structurally without porting the gate to N skill workflows.
- **R3-A8b-FR3 (schema strictness).** The CI grep MUST validate the AC #1 row schema (4 columns: `Retro file` / `Action ID` / `Status` / `Reason`) and fail when a row deviates (3-column tables, freeform-prose tables, comma-separated `Action ID` values). Strict schema is the basis for FR1's `Status` counting.

### Self-application audit (Winston's hard question, 2026-04-30 party mode)

Q: *"If R3-A8's gate had been alive when this story was authored, would the gate have caught anything we missed?"*

A: **Looking at the populated `## Prior Retro Follow-Through` table above (now schema-conformant per v3 patches F4 + F5), the gate surfaced 10 distinct action items across 4 retros:**
- `R3-A5` — flagged as Intentionally deferred (correct; portfolio-management decision).
- `R3-A8` itself — flagged as In scope (correct; this story IS the implementation).
- `R12-A5` — flagged as Not applicable (correct; projection-builder concern).
- `R4-A2`, `R4-A5`, `R4-A6`, `R4-A8` — all flagged as Not applicable (correct; their stories will pass through the gate at their own creation time).
- `R11-A2`, `R11-A3`, `R11-A4` — all flagged as Not applicable (correct; sequenced after R11-A1).

**What the gate did NOT surface under the v1 spec**, per Winston's multi-retro concern: latest-retro-only would have read Epic 12 retro only, missing **9 of the 10 rows above** including the load-bearing R3-A5 that motivated R3-A8 in the first place. **Patch #3 (multi-retro scan)** corrects this; the table demonstrates the multi-retro shape; AC #2 + worked example are in agreement.

**v3 patches additionally pinned**: AC #1's non-empty contract (no header-only stubs), AC #1's 4-column row schema (Retro file / Action ID / Status / Reason — one ID per row), AC #2's filter precedence (closure markers `✅` / `Done <SHA>` / `addressed by` / sprint-status `done` cross-reference), and AC #12's R3-A8b promotion trigger (Trigger A: ≥3 stories observed; Trigger B: Epic 13 close as hard ceiling).

**Vacuous-NA self-check (forward-looking, R3-A8b-FR1):** The table above contains 8 `Not applicable` rows + 1 `Intentionally deferred` + 1 `In scope`. R3-A8b-FR1 would NOT warn on this section because the `Status` set is non-singleton (`In scope` and `Intentionally deferred` are present). If R3-A8 had a single `In scope` row and 9 `Not applicable` rows, FR1 would not warn either — only an all-`Not applicable` distribution against a non-empty candidate set is suspect.

This audit IS the canonical evidence that the gate is non-trivial: a single-retro scan would have missed 9 of 10 rows including R3-A5.

### Carry-over story routing — what this story does NOT close

| Retro action | Status | Why R3-A8 doesn't close it |
|---|---|---|
| R3-A5 | open (portfolio mgmt) | R3-A5 wants existing backlog cleanup stories (post-epic-1, post-epic-2) reclassified or scheduled. R3-A8 creates a per-story gate that prevents *future* recurrence. The two are complementary, not substitutable. |
| R4-A2/A5/A6/A8 (Epic 4 carry-overs) | `backlog` | Their stories will be created later and will pass through the new gate at that time. |
| R11-A2/A3/A4 (Epic 11 carry-overs) | `backlog` | Same — the gate runs at their story-creation time, not retroactively. |

### Project Structure Notes

- All edits land in:
  - `.claude/skills/bmad-create-story/template.md` (gate section, AC #1)
  - `.claude/skills/bmad-create-story/workflow.md` (gate execution sub-block inside Step 5, AC #2)
  - `CLAUDE.md` (≤6-line pointer, AC #3)
  - `_bmad-output/implementation-artifacts/post-epic-3-r3a8-retro-follow-through-gate.md` (this file — self-evidence section, AC #4)
  - `_bmad-output/implementation-artifacts/sprint-status.yaml` (R3-A8 status flip per AC #10; R3-A8b sibling carve-out row per AC #12 — added at story creation, verified by dev at Task 5.2)
- Out of scope, explicitly:
  - `_bmad/bmm/4-implementation/bmad-create-story/template.md` and `_bmad/bmm/4-implementation/bmad-create-story/workflow.md` — pre-existing drift class (AC #9). A separate optional cleanup story would handle reconciliation if desired; not currently scheduled.
  - Other skills under `.claude/skills/` — `bmad-correct-course`, `bmad-quick-spec`, `bmad-create-prd`, etc. each have their own templates and workflows; the gate's scope is `bmad-create-story` only. If sprint-change-proposals or correct-course outputs need a similar gate, that's a separate proposal.
  - Existing already-`done` story files — no retroactive backfill of the new section.
  - **CI enforcement of the gate** — carved out to `post-epic-3-r3a8b-retro-gate-ci-enforcement` (sibling story, status `backlog`). See "Sibling carve-out" Dev Note above.

### Testing Standards (project-wide rules — apply to every story)

- **Tier 1 (Unit):** xUnit 2.9.3 + Shouldly + NSubstitute. No DAPR runtime, no Docker. **Not applicable to this story** (no code change).
- **Tier 2 / Tier 3 (Integration) — REQUIRED end-state inspection:** If the story creates or modifies Tier 2 (`Server.Tests`) or Tier 3 (`IntegrationTests`) tests, each test MUST inspect state-store end-state. **Not applicable** — this story creates no tests.
- **ID validation:** Any controller / validator handling `messageId`, `correlationId`, `aggregateId`, or `causationId` MUST use `Ulid.TryParse` (or accept any non-whitespace string per `AggregateIdentity` rules). `Guid.TryParse` on these fields is forbidden. **Not applicable** — this story touches no controller/validator.

(The Testing Standards block is reproduced verbatim from the live template so a fresh LLM context running `dev-story` does not have to cross-reference. The "Not applicable" annotations are this story's evidence that the dev considered each rule explicitly.)

### Library / framework versions

Not applicable — no code change. Markdown / XML-tag dialect changes only.

### Sprint-status transition ownership (AC #10)

Same rule as `post-epic-3-r3a6` (closed 2026-04-29) and `post-epic-3-r3a7` (currently `review`):

- `ready-for-dev → review` is the **dev's** responsibility, executed at Task 6.
- `review → done` is **`code-review`'s** responsibility. Do not flip the story directly to `done` from this task list.

### Previous-story intelligence — what makes this story different

- **`post-epic-3-r3a6` (closed 2026-04-29):** template for the sprint-status bookkeeping AC and for the "no `dev-story` flip to done" rule. Pattern reference only — R3-A6 was a code change story; R3-A8 is process-only.
- **`post-epic-3-r3a7` (review 2026-04-29):** authored the most recent example of a verbose post-epic story (~370 lines). R3-A8 deliberately stays compact (~250 lines) because the deliverable surface is small (3 file edits + 1 self-evidence section) and over-specification would amplify implementation cost beyond the AC #7 bound.
- **`post-epic-11-r11a1` (created `ready-for-dev` 2026-04-30):** authored as the first story since Epic 12 retro completed; its creation predates the R3-A8 gate. R11-A1 does NOT have a `## Prior Retro Follow-Through` section — that is acceptable because the gate did not yet exist when R11-A1 was authored. R3-A8 is forward-looking, not retroactive (AC #4 → AC scope clause).

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md#Proposal-7---post-epic-3-r3a8-retro-follow-through-gate`] — verbatim 4-item checklist + edit-target rule
- [Source: `_bmad-output/implementation-artifacts/epic-3-retro-2026-04-26.md#8-Action-Items`] — R3-A8 row
- [Source: `_bmad-output/implementation-artifacts/epic-3-retro-2026-04-26.md#10-Key-Takeaways`] — takeaway #3 "Prior retrospectives need a gate, not a memory" (rationale)
- [Source: `_bmad-output/implementation-artifacts/epic-3-retro-2026-04-26.md#5-Previous-Retro-Follow-Through`] — table evidence of R2 carry-overs that stayed `backlog` while Epic 3 closed
- [Source: `_bmad-output/implementation-artifacts/post-epic-3-r3a6-tier3-error-contract-update.md`] — pattern reference for sprint-status AC and `ready-for-dev → review` ownership
- [Source: `_bmad-output/implementation-artifacts/post-epic-3-r3a7-live-command-surface-verification.md`] — pattern reference for verbose ACs (intentionally NOT followed here — R3-A8 stays compact)
- [Source: `_bmad-output/implementation-artifacts/post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md`] — pattern reference for compact ACs (this story's size target)
- [Source: `.claude/skills/bmad-create-story/template.md`] — current live template; AC #1 + #5 edit target
- [Source: `.claude/skills/bmad-create-story/workflow.md`] — current live workflow (Step 1 lines 54–208 are the dialect reference, Step 5 is the insertion point); AC #2 + #6 edit target
- [Source: `.claude/skills/bmad-create-story/checklist.md`] — validate-create-story checklist; not edited by this story (the gate is enforced at create-time, not at validate-time)
- [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml`] — `post-epic-3-r3a8-retro-follow-through-gate` row + `epic-N-retrospective` field semantics referenced by AC #8
- [Source: `CLAUDE.md`] — current project instructions; AC #3 edit target (≤6-line addition)
- [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml`] — `post-epic-3-r3a8b-retro-gate-ci-enforcement: backlog` row added at story creation (party-mode review, 2026-04-30); see AC #12 + Sibling Carve-Out Dev Note
- [Source: `_bmad-output/implementation-artifacts/post-epic-2-r2a5b-version-prose-source-of-truth-refactor.md`] — pattern reference for the symptom-fix-then-structural-cure two-stage deployment that R3-A8 → R3-A8b mirrors (PR #222, SHA `564034a`)

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

### Completion Notes List

### File List
