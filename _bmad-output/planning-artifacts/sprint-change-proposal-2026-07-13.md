---
title: Story 1.12 Parallel-Execution Dependency Correction
status: approved
date: 2026-07-13
project: eventstore
scope: minor
approved_by: Administrator
approval_source: User approved the formal correction in the active bmad-dev-story workflow.
---

# Sprint Change Proposal: Story 1.12 Parallel-Execution Dependency Correction

## 1. Issue Summary

Story 1.12's implementation artifact added an absolute rule that it could not complete until all open review findings for Stories 1.9 and 1.10 were resolved. Story 1.9 remains `in-progress` with open review decisions, so that sentence blocked Story 1.12 before implementation even though the required platform seams already exist and the PRD/epic plan uses Story 1.15 as the final parity convergence gate.

The trigger was the `bmad-dev-story` preflight for Story 1.12 on 2026-07-13. The preflight found the absolute dependency in the story file, confirmed Story 1.10 is `done`, and confirmed Story 1.9 still has open review items. Administrator then explicitly approved a formal correction.

## 2. Impact Analysis

### Epic Impact

Epic 1 remains viable without scope or acceptance-criteria reduction. Stories 1.9-1.14 produce distinct platform capabilities and may be implemented or reviewed in parallel once the contracts they directly consume exist. Story 1.15 remains the mandatory convergence point: it cannot close until Stories 1.9-1.14 are complete and reviewed and all parity evidence is accepted.

No other epic is resequenced, invalidated, added, or removed. Stories 1.13 and 1.14 retain their dependencies on Story 1.12 behavior.

### Artifact Conflicts

- PRD section 11.3 used "approved order" without distinguishing evidence-acceptance order from serial execution. It needs clarification.
- The Epic 1 parity gate needs the same parallel-execution clarification.
- Story 1.12 needs its absolute sibling-review completion lock replaced with a direct-contract dependency rule.
- Architecture invariants AD-7, AD-8, AD-12, AD-19, and AD-20 remain unchanged.
- UX has no affected screen, journey, component, accessibility, or localization contract.
- Sprint status needs no story addition, removal, renumbering, or status transition.

### Technical Impact

The correction changes planning and workflow sequencing only. It does not authorize weakening any Story 1.12 acceptance criterion, changing Story 1.9 behavior, modifying a submodule, or accepting contradictory contracts. If Story 1.12 implementation discovers a direct incompatibility with a Story 1.9 seam, the affected work must halt and return through change control.

## 3. Recommended Approach

Use a direct adjustment. Clarify that the ordered capability list governs final parity evidence and Story 1.15 closure, while Stories 1.9-1.14 may execute in parallel against already implemented seams.

- Effort: Low
- Risk: Low
- Timeline impact: Removes an unnecessary serial wait; no scope increase
- Rollback: Not recommended; it would restore an artificial execution lock without resolving any product or architecture requirement
- MVP impact: None

## 4. Detailed Change Proposals

### Story 1.12 Dependency Text

OLD:

> Depends on: Stories 1.4, 1.9, 1.10, and 1.11; Story 1.12 cannot complete until the open review findings for Stories 1.9 and 1.10 are resolved.

NEW:

> Builds on: Implemented platform seams from Stories 1.4, 1.9, 1.10, and 1.11. Unresolved review findings in another story are not a serial completion lock; Story 1.12 may implement and complete independently unless it exposes a direct contract contradiction. Story 1.15 remains blocked until Stories 1.9-1.14 are complete and reviewed.

Rationale: Preserve the real technical dependencies and final parity gate without treating unrelated sibling review disposition as a serial implementation lock.

### PRD Section 11.3

OLD:

> Parties projection/query parity remains blocked until Stories 1.9-1.15 complete in the approved order and Story 1.15 records an owner-approved `available` packet tied to the exact runtime SHA consumed by Parties.

NEW:

> Parties projection/query parity remains blocked until Stories 1.9-1.15 complete and Story 1.15 records an owner-approved `available` packet tied to the exact runtime SHA consumed by Parties. Stories 1.9-1.14 may be implemented and reviewed in parallel once the contracts they directly consume exist; the approved capability sequence governs evidence acceptance and final parity closure, not serial story execution. Story 1.15 must still verify that Stories 1.9-1.14 are complete and reviewed before it may close the gate.

Rationale: Make the product gate explicit while preserving implementation concurrency.

### Epic 1 Parity Gate

Add a sequencing clarification after the numbered capability list and repeat the rule at Story 1.12: sibling review findings block final Story 1.15 closure, not independent implementation, unless they reveal a direct contract contradiction.

Rationale: Keep implementation slicing and the implementation story consistent with PRD intent.

## 5. Implementation Handoff

Classification: Minor.

Route to the Developer agent for direct Story 1.12 implementation. The developer must:

1. Preserve every Story 1.12 acceptance criterion and resolved implementation contract.
2. Treat existing Story 1.9 files as user-owned and out of scope.
3. Halt if implementation exposes a direct contradiction with a consumed Story 1.9 seam.
4. Leave Story 1.15 blocked until Stories 1.9-1.14 are complete and reviewed.

Success criteria:

- PRD, epic plan, and Story 1.12 express the same parallel-execution rule.
- No architecture, UX, sprint inventory, or runtime scope is changed by the correction.
- Story 1.12 can enter `in-progress` under the normal developer workflow.
- Final Parties parity closure remains fail-closed in Story 1.15.

## 6. Checklist Record

- [x] Triggering story and problem identified.
- [x] Concrete story/sprint evidence recorded.
- [x] Epic and future-story impact assessed.
- [x] PRD and architecture conflicts assessed.
- [N/A] UX changes; no UI behavior is affected.
- [x] Direct adjustment selected; rollback and MVP reduction rejected.
- [x] Detailed before/after edits defined.
- [x] Minor Developer-agent handoff and success criteria defined.
- [x] Explicit approval recorded from Administrator on 2026-07-13.
- [N/A] Sprint inventory update; no story or epic is added, removed, or renumbered.

## 7. Approval

Administrator approved the formal correction on 2026-07-13 in response to the Story 1.12 workflow halt. The correction is approved for immediate implementation under the conditions in section 5.
