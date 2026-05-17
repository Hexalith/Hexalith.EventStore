---
workflow: bmad-correct-course
project: Hexalith.EventStore
date: 2026-05-17
trigger: implementation-readiness-report-2026-05-17.md
status: applied
approvedAt: 2026-05-17
approvedBy: Jerome
appliedAt: 2026-05-17
appliedBy: Codex
scope: moderate
recommendedPath: direct-planning-adjustment
priorProposal: _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-17.md
artifactsUpdated:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad-output/planning-artifacts/admin-evidence-audit-2026-05-17.md
---

# Sprint Change Proposal Addendum - Remaining Implementation Readiness Corrections

## 1. Issue Summary

The implementation readiness report dated 2026-05-17 found strong requirements coverage but kept the overall status at NEEDS WORK. The earlier applied proposal corrected several documentation and split-map issues, but three readiness risks still need explicit closure before using the planning set for a broad new implementation pass:

1. The Walking Skeleton Gate exists, but it is still a prerequisite block rather than a named assignable/readiness-verifiable story.
2. Epic 11 still shows `FRs covered: (new -- from superpowers spec, SCP-Projection Stories 8.9-8.11)`, leaving its authority outside the numbered PRD coverage map.
3. Epics 14-21 declare linked implementation artifacts as required evidence, but the audit itself has not been recorded as a first-class readiness output.

Epic 22 has mostly good split guidance in `epics.md`, but sprint handoff still needs a rule that future work creates/assigns child rows only, especially for 22.7d-1 through 22.7d-4.

## 2. Checklist Results

| Item | Status | Result |
| --- | --- | --- |
| 1.1 Triggering story | [x] Done | Trigger is the 2026-05-17 implementation readiness assessment, not a single failing implementation story. |
| 1.2 Core problem | [x] Done | Planning readiness issue: requirements are covered, but authority, evidence, and assignability need tightening. |
| 1.3 Evidence | [x] Done | Evidence is `_bmad-output/planning-artifacts/implementation-readiness-report-2026-05-17.md`. |
| 2.1 Current epic impact | [x] Done | Epic 22 remains viable only when child stories are treated as binding implementation units. |
| 2.2 Epic-level changes | [!] Action-needed | Add a named walking skeleton readiness story, clarify Epic 11 authority, and record admin evidence audit output. |
| 2.3 Future epic impact | [x] Done | Future work should not assign parent/container rows for Epic 22 splits. |
| 2.4 New/obsolete epics | [x] Done | No new product epic is required. A readiness prerequisite story is enough. |
| 2.5 Epic order/priority | [x] Done | Walking skeleton verification must precede any new foundation implementation pass. |
| 3.1 PRD conflicts | [!] Action-needed | Epic 11 needs a decision: PRD-backed, approved change-proposal-backed, or supplemental/non-gating. |
| 3.2 Architecture conflicts | [x] Done | No new architecture conflict was found for this addendum. |
| 3.3 UX conflicts | [x] Done | UX warnings are quality-gate carry-forward items, not blockers. |
| 3.4 Other artifacts | [!] Action-needed | Add an admin evidence audit artifact or index entry for Epics 14-21. |
| 4.1 Direct adjustment | [x] Viable | Planning and tracking edits address the remaining blockers. |
| 4.2 Potential rollback | [x] Not viable | No implementation rollback is justified. |
| 4.3 PRD MVP review | [x] Not viable | MVP scope does not need reduction. |
| 4.4 Recommended path | [x] Done | Direct planning adjustment with Product Owner / Developer coordination. |

## 3. Recommended Approach

Use direct planning adjustment.

Do not reopen PRD discovery, renumber completed epics, or roll back implementation. Instead:

- Convert the Walking Skeleton Gate into a named readiness story or explicit sprint-status prerequisite.
- Resolve Epic 11 authority by marking it as approved change-proposal scope that supports existing query/projection FRs, or by adding PRD requirements if Jerome wants it in the numbered baseline.
- Create an audit artifact for Epics 14-21 that records which implementation artifacts were reviewed, with emphasis on accessibility, authorization, tenant isolation, protected-data redaction, and operational write safety.
- Keep Epic 22 parent/container stories unassignable. Add child tracking rows before future work begins, especially for 22.7d-1 through 22.7d-4.

Effort: Low to medium.

Risk: Low. These are planning and audit-trail changes, not runtime code changes.

Timeline impact: Minimal, but it prevents vague handoff into broad parent stories.

## 4. Detailed Change Proposals

### Epics - Walking Skeleton

OLD:

```markdown
## Walking Skeleton Gate

This gate is mandatory for any new implementation pass over the foundation sequence...
```

NEW:

```markdown
### Story WS-1: Clone-to-Command Flow Walking Skeleton

As a developer evaluating the foundation sequence,
I want the thinnest EventStore command path proven end to end,
So that foundation work remains anchored to observable user value.

Readiness rule: WS-1 must be verified before any new implementation pass over Epics 1-8 foundation work.

Acceptance Criteria:
- AppHost starts EventStore and one sample domain service.
- A sample command is submitted through `POST /api/v1/commands`.
- One event is persisted for the aggregate.
- Command status is observable through `GET /api/v1/commands/status/{correlationId}`.
- At least one structured log or trace carries the same correlation ID.
```

Rationale: The gate already names the right proof. Making it a named story/prerequisite gives sprint planning a concrete item to assign, verify, and audit.

### Epics - Epic 11 Authority

OLD:

```markdown
**FRs covered:** (new -- from superpowers spec, SCP-Projection Stories 8.9-8.11)
```

NEW option A, recommended:

```markdown
**Authority:** Approved projection change scope from SCP-Projection Stories 8.9-8.11.
**FRs supported:** FR50, FR51, FR52, FR53, FR54, FR57, FR58, FR61, FR62, FR63.
**Coverage note:** Epic 11 is supplemental implementation scope for the query/projection pipeline. It is not counted as additional numbered PRD coverage unless a future PRD update adds explicit server-managed projection-builder FRs.
```

NEW option B:

Add numbered PRD requirements for server-managed projection building, rerun the coverage map, and update Epic 11 to reference those new FRs.

Rationale: Option A is lower risk because it preserves the current PRD baseline and explicitly labels Epic 11 as approved change-proposal scope. Option B is appropriate only if Jerome wants Epic 11 to become formal PRD baseline scope.

### Epics 14-21 - Evidence Audit

OLD:

```markdown
Readiness review input: every implementation artifact linked in the `Detail` entries below is required evidence...
```

NEW:

Create `_bmad-output/planning-artifacts/admin-evidence-audit-2026-05-17.md` with:

- Reviewed artifact list for Epics 14-21.
- Result per artifact: pass, pass-with-debt, action-needed, or skipped.
- Specific checks for admin UI accessibility, authorization, tenant isolation, protected-data redaction, DAPR/backend safety, and operational write approval gates.
- Residual risks and follow-up stories, if any.

Rationale: Declaring evidence inputs is useful, but readiness sign-off needs a recorded audit result.

### Epic 22 - Assignability

OLD:

`sprint-status.yaml` keeps historical rows such as `22-1...` and `22-5...`, and future blocked work currently has a parent row for `22-7d...`.

NEW:

- Leave completed parent rows in place as historical aggregates.
- Add comments that parent/container rows must not be assigned for new implementation.
- Before starting redaction work, replace or supplement `22-7d-protected-data-redaction-across-operational-surfaces` with:
  - `22-7d-1-protected-data-redaction-in-logs-and-problemdetails`
  - `22-7d-2-protected-data-redaction-in-admin-api-and-web-ui`
  - `22-7d-3-protected-data-redaction-in-cli-and-mcp`
  - `22-7d-4-protected-data-redaction-in-replay-rebuild-backup-validation-and-tests`

Rationale: The epics file already contains the right child story split. Sprint tracking should preserve that boundary when work is created.

### Minor Organization And UX Warnings

Apply as non-blocking cleanup:

- Normalize epic summaries to `Outcome`, `FRs covered`, `Dependencies`, `Status`, and `Implementation evidence` where applicable.
- Keep completed historical evidence separate from assignable future work in the planning index or sprint-status comments.
- Preserve UX accessibility gates in implementation stories: axe-core page inventory, keyboard-only navigation, ARIA tree snapshot, high-contrast verification, and state-matrix coverage.
- Preserve detailed interaction requirements for command palette, breadcrumbs, virtualized rendering, keyboard shortcuts, CLI profiles/completions, and MCP investigation session state in story acceptance criteria.

## 5. Implementation Handoff

Scope classification: Moderate.

Recommended route: Product Owner / Developer coordination.

Product Owner decisions:

- Choose Epic 11 authority option A or B.
- Approve whether WS-1 should be added to `epics.md`, `sprint-status.yaml`, or both.
- Approve creation of the admin evidence audit artifact.

Developer responsibilities after approval:

- Patch planning artifacts only; no runtime code change is implied by this proposal.
- If sprint status is updated, preserve historical completed rows and avoid renumbering completed work.
- Do not assign Epic 22 parent/container stories for future implementation.

Success criteria:

- A readiness rerun no longer flags the walking skeleton as non-assignable.
- A readiness rerun can classify Epic 11 authority without ambiguity.
- Epics 14-21 have a recorded evidence audit, not only evidence links.
- Epic 22 future work is tracked at child-story granularity.

## 6. Approval Request

This addendum was approved by Jerome on 2026-05-17 and applied as planning-only changes.

Applied decision: Option A for Epic 11 authority. WS-1 was added to `epics.md` and `sprint-status.yaml`; Epic 22 child redaction rows were added to `sprint-status.yaml`; the admin evidence audit was created.
