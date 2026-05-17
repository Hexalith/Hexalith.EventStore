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
artifactsUpdated:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux-design-specification.md
---

# Sprint Change Proposal - Implementation Readiness Corrections

## 1. Issue Summary

The implementation readiness assessment completed on 2026-05-17 found that requirements traceability is strong, but execution readiness still needed targeted cleanup before the next implementation pass.

Evidence:

- PRD, architecture, epics, and UX artifacts all exist.
- The epics FR coverage map covers 104/104 functional requirements.
- The blockers are planning and handoff issues, not missing product requirements.

The readiness blockers were:

1. Epic 22 contains oversized parent stories that need binding child story splits before implementation.
2. Epics 14-21 rely on linked implementation artifacts and must declare those artifacts as required readiness-review inputs.
3. UX and architecture still used stale `(Command, CurrentState?) -> List<DomainEvent>` wording instead of `DomainResult`.
4. UX still referenced Fluent UI v4 even though the project baseline is Fluent UI Blazor v5.

One related major issue was also addressed: Story 8.7 referenced Story 8.8, but Story 8.8 was missing from the main epics file even though the implementation artifact exists.

## 2. Checklist Results

| Item | Status | Result |
| --- | --- | --- |
| 1.1 Triggering story | [x] Done | Trigger is the 2026-05-17 implementation readiness assessment, not a single implementation story. |
| 1.2 Core problem | [x] Done | Execution-readiness issue: the artifacts cover the requirements but needed cleaner story sizing, review input rules, and current contract/version wording. |
| 1.3 Evidence | [x] Done | Evidence is `_bmad-output/planning-artifacts/implementation-readiness-report-2026-05-17.md`. |
| 2.1 Current epic impact | [x] Done | Epic 22 remains viable if parent stories are treated as containers and child stories become the implementation units. |
| 2.2 Epic-level changes | [x] Done | Epic 22 FR mappings and story sections now point to child story IDs for the oversized areas. |
| 2.3 Future epic impact | [x] Done | Future work should assign child stories 22.1a-22.1d and 22.5a-22.5d, not the parent containers. |
| 2.4 New/obsolete epics | [x] Done | No new product epic is required. |
| 2.5 Epic order/priority | [x] Done | Epic 21 is explicitly historical Fluent UI v5 baseline work for future UI stories. |
| 3.1 PRD conflicts | [x] Done | No PRD scope change required. |
| 3.2 Architecture conflicts | [x] Done | Architecture now uses `DomainResult` for the domain processor contract wording. |
| 3.3 UX conflicts | [x] Done | UX now uses `DomainResult` and Fluent UI v5 language. |
| 3.4 Other artifacts | [x] Done | Epics 14-21 now declare linked implementation artifacts as required review inputs. |
| 4.1 Direct adjustment | [x] Viable | Documentation and story-splitting edits address the blockers. |
| 4.2 Potential rollback | [x] Not viable | No implementation rollback is justified. |
| 4.3 PRD MVP review | [x] Not viable | MVP scope remains achievable. |
| 4.4 Recommended path | [x] Done | Direct planning adjustment with moderate backlog hygiene. |
| 5.1-5.5 Proposal components | [x] Done | Covered in this document. |
| 6.1-6.2 Final review | [x] Done | Proposal reflects the current readiness report and applied artifact edits. |
| 6.3 Approval | [x] Done | Approved by Jerome on 2026-05-17. |
| 6.4 Sprint status update | [N/A] Skip | No sprint-status update was made because the changed parent stories are planning containers and no new sprint execution rows were requested. |
| 6.5 Handoff | [x] Done | Handoff plan is included below. |

## 3. Impact Analysis

### Epic Impact

Epic 22 remains the active public gateway and downstream integration contract epic. Story 22.1 and Story 22.5 are now explicitly marked as container-only parent stories. Their FR coverage map rows now point to the independently deliverable child stories:

- 22.1a Contracts gateway DTOs and ProblemDetails extension names
- 22.1b Client high-level command/query methods
- 22.1c Testing fakes and builders
- 22.1d Package ownership docs and generated API refresh
- 22.5a Durable publish-after-persist semantics
- 22.5b Backend deployment matrix and ordering/session policy
- 22.5c Drain, retry, and dead-letter behavior
- 22.5d Backend-specific proof tests and evidence

Epics 14-21 remain completed historical work. The correction does not rewrite their acceptance history. It makes review expectations explicit: every linked `Detail` implementation artifact is required evidence during readiness review.

Story 8.8 was added as a historical semantic-release migration story pointing to `_bmad-output/implementation-artifacts/8-8-semantic-release-migration.md`.

### Artifact Conflicts

The architecture document previously mixed current `DomainResult` decisions with older `List<DomainEvent>` wording. The current wording now consistently describes the domain processor contract as `(Command, CurrentState?) -> DomainResult`, with domain rejections modeled through rejection event outputs.

The UX document previously referenced Fluent UI v4 and older pure-function return wording. It now references Fluent UI v5, the current project package baseline, and `DomainResult`.

### Technical Impact

No runtime code, apphost configuration, package references, or tests were changed. The impact is limited to planning artifacts and implementation handoff clarity.

## 4. Recommended Approach

Use direct planning adjustment.

Do not roll back completed implementation. Do not change PRD scope. Do not renumber completed epics. The readiness failure is best handled by making the existing planning set clearer and more enforceable:

- Treat broad parent stories as containers.
- Assign child stories as implementation units.
- Treat implementation artifacts as required review evidence for completed admin epics.
- Keep architecture and UX terminology aligned with the actual public contract and UI stack.

Effort: Low to medium.

Risk: Low, because the changes are documentation-only and preserve existing scope.

Timeline impact: Minimal. The main effect is reducing future review ambiguity before Epic 22 or admin UI work is assigned.

## 5. Detailed Change Proposals

### Epics

OLD:

- Story 22.1 and Story 22.5 were full parent stories with split maps described as follow-up guidance.
- FR83-FR86 mapped to Story 22.1.
- FR96-FR98 mapped to Story 22.5.
- Epics 14-21 linked implementation artifacts but did not explicitly declare them as required review inputs.
- Story 8.7 referenced Story 8.8, but no Story 8.8 heading existed.

NEW:

- Story 22.1 and Story 22.5 are marked as container-only parent stories.
- Child stories 22.1a-22.1d and 22.5a-22.5d include independently reviewable acceptance criteria.
- FR83-FR86 map to 22.1a-22.1d.
- FR96-FR98 map to 22.5a-22.5d.
- Epics 14-21 explicitly require the linked implementation artifacts as readiness-review inputs.
- Story 8.8 exists as a historical semantic-release migration story and links to its implementation artifact.

Rationale: Implementation agents should not pick up oversized parent stories or hunt through historical artifact links without an explicit review rule.

### Architecture

OLD:

- Some architecture sections described the domain processor contract as `(Command, CurrentState?) -> List<DomainEvent>`.
- The file tree comment for `DomainResult.cs` described it as a list of domain events.
- The flow diagram described the domain service returning `List<DomainEvent>`.

NEW:

- The domain processor contract is described as `(Command, CurrentState?) -> DomainResult`.
- `DomainResult.cs` is described as aggregate type plus event outputs.
- The flow diagram says the domain service returns `DomainResult`.

Rationale: `DomainResult` is the current public contract and prevents implementers from using an obsolete event-list return model.

### UX

OLD:

- Developer SDK experience, Act 1, and SDK mechanics used `List<DomainEvent>`.
- Dashboard design-system guidance referenced Fluent UI v4 and version 4.13.2.

NEW:

- Developer SDK experience, Act 1, and SDK mechanics use `DomainResult`.
- Dashboard design-system guidance references Fluent UI v5 and package baseline `5.0.0-rc.2-26098.1`.
- Historical prototype wording notes that future implementation follows the v5 baseline.

Rationale: UX guidance should match the current SDK contract and UI component system so future work does not accidentally follow obsolete APIs.

## 6. Implementation Handoff

Scope classification: Moderate.

Recommended route: Product Owner / Developer coordination.

Developer responsibilities:

- Use child stories 22.1a-22.1d and 22.5a-22.5d for any new Epic 22 implementation or review work.
- Treat Story 22.1 and Story 22.5 as containers only.
- Use `DomainResult` wording and APIs in new implementation artifacts.
- Use Fluent UI v5 patterns from Epic 21 for admin UI work.

Product Owner responsibilities:

- Approve the planning changes.
- If sprint-status tracking needs individual rows for child stories, add those rows deliberately rather than assigning the parent container stories.
- Keep Epics 14-21 implementation artifacts in the review bundle for future readiness assessments.

Success criteria:

- A readiness rerun no longer flags Epic 22 story sizing as a blocker.
- A readiness rerun recognizes Epics 14-21 linked implementation artifacts as required review inputs.
- `architecture.md` and `ux-design-specification.md` no longer contain stale `List<DomainEvent>` contract wording.
- `ux-design-specification.md` no longer contains Fluent UI v4 as current implementation guidance.

## 7. Final Routing

This proposal is approved and applied. The next implementation pass can proceed against the split child stories and required review bundle instead of reopening requirements discovery.

Correct Course workflow complete, Jerome.
