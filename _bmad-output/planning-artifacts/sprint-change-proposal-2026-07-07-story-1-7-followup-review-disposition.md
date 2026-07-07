# Sprint Change Proposal — Story 1.7 follow-up-review disposition

- **Date:** 2026-07-07
- **Author:** Administrator (Correct-Course workflow)
- **Trigger:** Epic 1 retro action item #2 — "Resolve the open follow-up review recommendation for Story 1.7 guardrails or record a deliberate acceptance decision." Tracked as `deferred-work.md` entry **DW-1** (`status: open`) and `spec-1-7` frontmatter `followup_review_recommended: true`.
- **Scope classification:** **Minor** (governance disposition; tracking-artifact edits only, no code)
- **Mode:** Batch
- **User decision:** Run one more review pass (then dispose).

## Section 1 — Issue Summary

Story 1.7 (*DomainService Packaging and Guardrails*) is `status: done` — focused suites green, Release build clean, 14-package manifest packed and validated. It nonetheless carries an **open follow-up-review recommendation** that never converged:

- **Four** follow-up review passes ran. Each found **8–9 medium** findings, patched them all, and — because each pass again changed shared regex guardrail logic — **recommended yet another follow-up**.
- `deferred-work.md` **DW-1** records this explicitly: *"Review budget (3 cycles) was exhausted with the story finalized (status: done, verify green) while the review pass kept recommending an independent follow-up."*
- Epic 1 retro root-cause (Winston): *"Regex scans and package manifest tests are code paths with edge cases … plan guardrail work as product work."* A regex-based scanner over arbitrary C# has an unbounded edge-case tail; iterating it pass-by-pass does not terminate.

Per the user's instruction, **one more review pass was executed** as part of this correct-course before disposing of the item.

## Section 1a — Review pass executed (2026-07-07, correct-course)

**Verification (green baseline):**
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/ --filter FullyQualifiedName~DomainModuleAuthoringGuardrailTests` → **25/25 passed**.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter FullyQualifiedName~ReleasePackageManifestTests` → **8/8 passed**.

**Findings — all one class (regex-scan completeness/soundness on arbitrary C#):**

| # | Observation | Class | Disposition |
|---|-------------|-------|-------------|
| 1 | Cross-file / computed canonical route indirection (a canonical route value imported from another type/assembly or a computed variable) evades `StringAssignments`/`ResolveStringExpression`, which resolve only same-file literals and simple concatenations. | false-negative | **Already** covered by deferred entry 2 (Roslyn/convention guardrail). |
| 2 | `ContainsInvocationOnCallResult` (`DomainModuleAuthoringGuardrailTests.cs:806-820`) matches `).<marker>(` on *any* call result with generic markers (`GetStateAsync`, `SaveStateAsync`, `SetStateAsync`, `ClearCacheAsync`). A domain method chain that merely shares one of these names would be flagged — a **false-positive** that would break a legitimate build. | soundness | Same class; consolidated into deferred entry 2 evidence. No domain currently trips it (suites green). |
| 3 | `StateStoreWrapperDeclaration` name-match (`*Repository`/`*Gateway`/`*Persistence`) combined with any same-file state access is a deliberately broad net. | soundness | Same class; accepted broad-net behavior. |

**Determination:** `intent_gap: 0`, `bad_spec: 0`, `patch: 0`, `defer: 0 new` (findings 1–3 are the *same* class already captured by the two existing Story 1.7 deferred entries), `follow-up review: not recommended`.

**Why no patch this pass** (this is the crux): applying another regex refinement would (a) re-arm the identical non-converging loop the retro action item exists to close, and (b) **fail** the retro's own completion criterion — *"deferred-work.md no longer has an open follow-up-review-only item for Story 1.7"* — because every prior patch pass regenerated the follow-up flag. The correct closure is to cap this finding class under the existing Roslyn/convention deferral and record a deliberate acceptance. The review pass *confirmed* the acceptance is warranted: green deliverable + a uniform, already-deferred finding class.

## Section 2 — Impact Analysis

- **Epic impact:** None. Story 1.7 acceptance criteria and guardrail behavior are unchanged. Epic 1 remains closed on substance.
- **Story impact:** No AC, task, Dev Note, or design assumption changes. Only tracking metadata (`followup_review_recommended` flag) and an appended triage-log entry.
- **Artifact conflicts:** `deferred-work.md` DW-1 (`open` → `accepted`); `sprint-status.yaml` action item (`open` → `done`); `epic-1-retro-2026-07-07.md` action #2 gets a resolution note. No production code, no packaging output, no runtime behavior changes.
- **Technical impact:** None. Tests remain green; the two genuine substantive residual risks stay tracked with their concrete future triggers.

## Section 3 — Recommended Approach

**Direct Adjustment — deliberate acceptance, reached through the requested review pass.**

1. The two *substantive* residual gaps remain as their own deferred entries with concrete triggers, untouched as active engineering risk:
   - **Broad DAPR/host-wiring ban** — enforce after Tenants transitional host composition moves behind platform seams or gets a permanent documented exception.
   - **Cross-file/computed canonical route resolution** — needs a Roslyn-level syntax/semantic guardrail or an explicit convention; finding #2 (receiver-agnostic state-access soundness) is folded into this entry's evidence so nothing is lost.
2. The **follow-up-review-only** item (DW-1) is closed as accepted: the deliverable is green and every remaining finding is the already-deferred regex-completeness class; a fifth regex pass has negative expected value and would violate the completion criterion.
3. Any future closure of the regex-completeness class happens as a **scoped Roslyn/convention guardrail story**, not as another ad-hoc follow-up review of Story 1.7.

**Rationale:** Honors the user's "run one more review pass" (a genuine review with green verification was performed and its findings triaged) while terminating the loop the action item targets. Effort: minutes (metadata edits). Risk: low — no behavior change, substantive risks stay tracked. Timeline: unblocks Epic 1 retro closure.

## Section 3a — Correct-Course Story Rewrite Gate

**Evaluated: does NOT apply.** This change is not an architectural pivot and does not supersede any active Story 1.7 acceptance criteria, tasks, Dev Notes, project-structure notes, or design assumptions. Story 1.7 is `done`; its behavior and ACs are unchanged. The only story-file edits are (a) flipping a review-recommendation *flag* in frontmatter and (b) appending a triage-log entry — metadata/audit, not an AC/task/Dev-Note rewrite. No old→new story rewrites are required; none are omitted. Gate satisfied.

## Section 4 — Detailed Change Proposals

### 4.1 `_bmad-output/implementation-artifacts/spec-1-7-domainservice-packaging-and-guardrails.md` — frontmatter

```
OLD:  followup_review_recommended: true
NEW:  followup_review_recommended: false
```

Rationale: this terminating correct-course review pass does not recommend a further follow-up (green deliverable; uniform already-deferred finding class).

### 4.2 `spec-1-7…` — Review Triage Log (append)

Append a "2026-07-07 — Terminating follow-up review pass (correct-course)" entry recording: green suites (25/25, 8/8); `patch: 0`, `defer: 0 new`, follow-up **not** recommended; findings 1–3 consolidated under the existing deferred entries; DW-1 closed as accepted with the completion-criterion rationale.

### 4.3 `_bmad-output/implementation-artifacts/deferred-work.md` — DW-1

```
OLD:  status: open
NEW:  status: accepted 2026-07-07 (correct-course, sprint-change-proposal-2026-07-07-story-1-7-followup-review-disposition)
      resolution: Terminating review pass ran — deliverable green (25/25 guardrail, 8/8 manifest);
      all remaining findings are the regex-scan-completeness class already tracked by the two
      substantive Story 1.7 deferred entries above. A fifth regex patch would re-arm the loop and
      fail the retro completion criterion. Future closure = scoped Roslyn/convention guardrail story,
      not another follow-up review.
```

### 4.4 `deferred-work.md` — extend the cross-file/computed route entry evidence

Append to that entry's `evidence`: the receiver-agnostic state-access soundness note (finding #2) belongs to the same "lightweight scan cannot be complete/sound on arbitrary C#" class and is likewise closed only by a Roslyn/convention-level guardrail.

### 4.5 `_bmad-output/implementation-artifacts/sprint-status.yaml` — action item

```
OLD:  action: "Resolve the open follow-up review recommendation for Story 1.7 guardrails or record a deliberate acceptance decision."
        owner: "Amelia (Developer)"
        status: open
NEW:  status: done
        note: "Terminating correct-course review pass 2026-07-07 (green 25/25 + 8/8); DW-1 accepted.
        Residual regex-completeness risk consolidated under the two substantive 1.7 deferred entries;
        future closure = scoped Roslyn/convention guardrail story."
```

### 4.6 `_bmad-output/implementation-artifacts/epic-1-retro-2026-07-07.md` — action #2 resolution note

Append under action item #2 a one-line resolution note (2026-07-07) pointing to this proposal and confirming the completion criterion is met (no open follow-up-review-only item for Story 1.7).

## Section 5 — Implementation Handoff

- **Scope:** Minor → direct developer implementation (Amelia).
- **Deliverables:** the six edits in Section 4.
- **Success criteria:**
  - `deferred-work.md` has **no open follow-up-review-only item for Story 1.7** (retro completion criterion met); DW-1 marked accepted.
  - `spec-1-7` `followup_review_recommended: false` with a terminating triage-log entry.
  - `sprint-status.yaml` action item `done`.
  - The two substantive residual risks remain **open** and tracked with their future triggers.
  - Guardrail + manifest suites remain green (unchanged; no code touched).
