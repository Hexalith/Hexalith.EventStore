# Sprint Change Proposal - Tracking-versus-Planning Status and FR Ownership Reconciliation

- **Date:** 2026-09-08
- **Trigger:** PRD §12 owed refinements OR2, OR3, and OR12, all blocking the OR1 implementation-readiness re-run
- **Scope classification:** Moderate - planning and tracking artifacts only, no source, release, deployment, Git, or submodule mutation
- **Artifacts changed:** `_bmad-output/implementation-artifacts/sprint-status.yaml`, `_bmad-output/planning-artifacts/epics.md`, `_bmad-output/planning-artifacts/prd.md`

## 1. Issue Summary

Three contradictions recorded in PRD §12 blocked the next readiness re-run. Each was a disagreement between the execution tracker and the planning record about what had actually been implemented, plus one rule violation in the requirements ownership map.

The common cause is the control PRD §12 OR13 says is missing: nothing binds a story's `done` transition to a named gate. Two of the four contested rows were flipped inside commits that did not mention a status change at all, and one of those stories explicitly forbids itself from touching the tracker.

## 2. Impact Analysis

| Area | Impact |
| --- | --- |
| Epic 4 | Story 4.5 status text corrected in `epics.md`; Story 4.6 tracker row corrected. No acceptance criteria changed. |
| Epic 5 | Story 5.1 status text corrected in `epics.md`; Story 5.3 tracker row corrected and its reconciliation paragraph rewritten against current tree facts. |
| Epic 2 / Epic 3 | Eleven `Requirements coverage` lines re-stated to give each of eight FRs exactly one primary owner, or explicitly named disjoint slices. |
| PRD §7 | NFR7 class (a) moves from **contested** to **guarded**. |
| PRD §10 | SM11 moves from three confirmed to four confirmed of five. |
| PRD §12 | OR2, OR3, and OR12 retired with a recorded closure rationale; OR1 restated as the sole remaining blocker. |
| Tests | `Contracts.Tests` 1942/1942, `DeferredWorkGovernance.Tests` 19/19 (all pre-existing ATDD skips). No guarded comment block touched. |
| Code / release | None. No source, workflow, deployment, or submodule change. |

## 3. Recommended Approach

**Direct adjustment.** No rollback and no MVP scope change. Each contradiction was resolved per story against the highest-fidelity evidence available for that story, rather than by preferring one artifact wholesale.

## 4. Detailed Change Proposals

### 4.1 OR2 - Story 5.3, resolved against the tracker

Evidence: `spec-5-3` frontmatter is `status: in-progress`, `review_loop_iteration: 3`, with fourteen of twenty implementation items checked, six open, and no `## Auto Run Result` marker. The spec's own **Never** list forbids it from modifying `sprint-status.yaml`. Commit `c83cc4c3` ("feat(tests): add tests for access token retrieval and handle missing credentials") flipped the row `backlog` -> `done` with no mention in subject or body.

`epics.md` was also partly stale: its claim that committed Development configuration still carries fixed signing-key and administrator credential values is no longer true - the tracked `appsettings.Development.json` files carry no credential material.

| Artifact | Old | New |
| --- | --- | --- |
| `sprint-status.yaml` | `5-3-...: done` | `5-3-...: in-progress`, with a comment recording the spurious flip |
| `epics.md:3810` | "Story 5.3 remains backlog ... committed Development configuration still carries fixed signing-key and administrator credential values" | "Story 5.3 is in progress, neither backlog nor done", naming the fourteen landed items, the six open items, and the standing bar on claiming NFR3/NFR4 coverage |

### 4.2 OR3 - Stories 4.5, 4.6, 5.1, resolved per story

**Story 4.5 - tracker correct, `epics.md` stale.** `spec-4-5` is `status: done` at review loop 4 with a sealed evidence packet. The `epics.md` paragraph said "remains in progress". Corrected, and strengthened: the paragraph now states explicitly that completion grants no fencing authority, so NFR7 class (c) cannot be read out of a `done` row.

**Story 4.6 - tracker wrong.** `spec-4-6` records `approval_state: absent`, `implementation_authorized: false`, and three outstanding `operator_actions`. Commit `8d6f7dac` flipped the row `awaiting-operator` -> `done`. Restored to `awaiting-operator`, the value the spec itself uses, because the block is external operator approval rather than unfinished development work. `epics.md`'s "remains backlog" was also wrong - a five-loop-reviewed spec and a frozen successor both exist - and now states the awaiting-approval position with the three outstanding operator actions named.

**Story 5.1 - tracker correct, `epics.md` stale.** `spec-5-1` is `status: done` at review loop 7. Commit `57fa0909` added `AggregateActorInfrastructureFailureTests` plus the drain and recovery lanes, which verify clearing order and persisted end-state independently through the actor path - exactly the independence the `epics.md` paragraph demanded. Corrected.

One defect found and recorded rather than silently fixed: the `## Auto Run Result` section inside `spec-5-1` names story key `5-2-admin-endpoint-authorization-and-tenant-filters`, because the orchestrator's missing-marker repair synthesized it from the sibling spec. The marker is mislabelled; the frontmatter, loop count and test evidence are Story 5.1's own. Noted in the `epics.md` paragraph.

### 4.3 OR12 - FR primary ownership

**The FR1 half of OR12 was itself wrong and is refuted, not fixed.** `epics.md` records primary FR1 ownership at Story **1.11** ("Domain-Module Adoption Guardrails", Epic 1), and the FR Coverage Map places FR1 in Epic 1. There is no Epic 4 primary claim on FR1; the OR12 text appears to have read "1.11" as "4.11". No change was made to `epics.md` for this half.

**The eight duplicate primary claims were real** and are resolved in the idiom already used by Epics 4 through 8 and by the existing FR36 completion rule:

| FR | Resolution |
| --- | --- |
| FR11 | Story 2.1 sole primary; Story 2.4 demoted to supporting |
| FR12 | Partitioned: Story 2.2 owns the discovery/emission/metadata-header slice, Story 2.9 owns the accepted-command `Location` slice; Story 2.11 demoted to supporting. New FR12 completion rule. |
| FR13 | Story 2.3 sole primary; Story 2.5 demoted to supporting (Story 2.6 already read supporting) |
| FR15 | Partitioned into five named disjoint slices across Stories 2.4, 2.5, 2.6, 2.11, 2.12. New FR15 completion rule. |
| FR19 | Story 3.3 primary; Story 3.16 demoted to supporting maintenance |
| FR21 | Story 3.5 primary; Story 3.16 demoted to supporting maintenance |
| FR22 | Story 3.6 primary; Stories 3.12 and 3.14 demoted to supporting corrective releases |
| FR25 | Story 3.7 primary; Stories 3.12 and 3.14 demoted to supporting corrective releases |

The **Primary-ownership rule** (`epics.md:386`) now states the partition condition explicitly - named disjoint slices, one primary owner each, plus a completion rule - instead of leaving it as unwritten practice that Epics 4 through 8 already follow. The rule was clarified to match established practice, not weakened: an unqualified primary claim on a requirement another story also claims unqualified remains forbidden.

### 4.4 PRD consequential updates

- **§7 NFR7 class (a):** contested -> guarded, delivered by Story 5.1, with the resolving evidence named. Class (c) unchanged and still undelivered.
- **§10 SM11:** three of five confirmed -> four of five confirmed, one out of scope pending the owned deferral tracked by OR4.
- **§12:** OR2, OR3 and OR12 rows removed and replaced by a dated closure record explaining what each was resolved against. Identifiers are retired, not reused, and no other row is renumbered, so existing reviews citing an OR number still point at the same item. OR1's row now states it is the sole remaining blocker. OR13's text updated to note it is the missing control behind all three.

## 5. Implementation Handoff

All edits in this proposal are already applied. Remaining work belongs to named owners:

- **Product owner - OR1.** The three blocking prerequisites are cleared. The implementation-readiness re-run can proceed.
- **Epic 5 owner - Story 5.3.** Six implementation items remain open and NFR3/NFR4 coverage stays unclaimable until they close.
- **Architecture owner - Story 4.6 and OR4.** Story 4.6 needs the three operator actions in its spec. DW-326 still needs an owner and a trigger before NFR7 class (c) or SM11 can move.
- **Test owner - OR13.** This proposal fixed four wrong rows but not the reason they were wrong. Nothing yet binds a `done` transition to a named gate, and two of the four flips happened inside commits about something else.
- **bmad-loop maintainer.** The `## Auto Run Result` marker in `spec-5-1` names the wrong story key; the missing-marker repair path can synthesize a marker from a sibling spec.

## 6. Success Criteria

- No story has contradictory statuses across `sprint-status.yaml`, its spec frontmatter, and its `epics.md` reconciliation paragraph.
- Every FR has exactly one primary owner, or explicitly named disjoint slices each with one primary owner and a completion rule.
- `Contracts.Tests` stays at 1942/1942. Verified after the edits.
