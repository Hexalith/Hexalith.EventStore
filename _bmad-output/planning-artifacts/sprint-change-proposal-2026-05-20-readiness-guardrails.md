---
workflowType: "bmad-correct-course"
date: "2026-05-20"
project: "Hexalith.EventStore"
trigger: "Implementation readiness assessment identified 9 non-critical issues requiring planning guardrails before new implementation begins."
status: "implemented"
sourceReport: "_bmad-output/planning-artifacts/implementation-readiness-report-2026-05-20.md"
updatedArtifacts:
  - "_bmad-output/planning-artifacts/prd.md"
  - "_bmad-output/planning-artifacts/architecture.md"
  - "_bmad-output/planning-artifacts/epics.md"
  - "_bmad-output/planning-artifacts/ux-design-specification.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
  - "_bmad-output/project-context.md"
---

# Sprint Change Proposal - Readiness Guardrails

## 1. Issue Summary

The 2026-05-20 implementation readiness assessment returned `READY WITH CONDITIONS`. It found no critical blockers, but identified 9 planning-hygiene risks that could cause implementation drift if future work begins without clearer guardrails.

The issues were:

1. Several epic titles remained technology-centric.
2. Parent/container stories 22.1 and 22.5 could be assigned directly by mistake.
3. Completed Admin/UI Epics 14-21 depend on linked detail artifacts for full review.
4. Bootstrap/setup work was mitigated by WS-1 but not first-class enough for reuse.
5. PRD, UX, architecture, and project context had version-baseline drift.
6. Current-release, v1.1, v2, v3, and v4 requirements lived together without a selection rule.
7. UX interaction details were delegated to story acceptance and needed a visible review guard.
8. WS-1 acceptance criteria were checklist-style rather than Given/When/Then.
9. Epic 11 was supplemental projection-builder scope outside direct numbered PRD coverage.

## 2. Impact Analysis

Epic impact: Epics 1, 2, 4, 5, 7, 9, 11, 14, 19, and 21 needed capability aliases so planning uses user-visible outcomes even when stable IDs retain technical names. Epic 22 needed stronger container-story assignment rules.

Story impact: Story WS-1 needed to become the explicit first-class bootstrap/readiness slice, with BDD acceptance criteria. Parent stories 22.1 and 22.5 needed to be marked as summary-only containers.

Artifact conflicts: Version references in PRD, architecture, UX, and project context drifted from the repository. The verified baseline is .NET SDK 10.0.300, DAPR runtime 1.17.7, DAPR .NET packages 1.17.9, Aspire CLI/AppHost SDK 13.3.2, Aspire.Hosting packages 13.3.3, CommunityToolkit.Aspire.Hosting.Dapr 13.0.0, and Fluent UI 5.0.0-rc.2-26098.1.

Technical impact: No runtime code change is required. This is a planning and handoff correction.

## 3. Recommended Approach

Use direct adjustment. The conditions are documentation and planning-control issues, so the safest correction is to update the source planning artifacts and sprint handoff rules without creating new implementation stories.

Risk: Low. The changes preserve all existing epic/story IDs and statuses.

Timeline impact: None for active implementation. Future story creation should be more constrained and easier to review.

## 4. Detailed Change Proposals

### PRD

Add scope horizon governance to distinguish current repository baseline, v1 historical foundation, current query/projection release, v1.1 downstream contract closure, v2 admin tooling, and v3/v4 roadmap. Clarify that requirements are not assignable unless routed through sprint-status and active story files.

Normalize the technology stack and runtime stack to the current repository/tooling baseline.

Clarify that Epic 11 remains supplemental projection-builder scope under the approved change proposal unless the PRD is amended with explicit new FR coverage.

### Architecture

Normalize runtime dependencies, verified current versions, package references, and the architecture summary row to the current repo baseline.

### Epics

Add capability aliases to technology-centric epic titles while preserving existing IDs. Convert WS-1 acceptance criteria to Given/When/Then and make WS-1 the explicit bootstrap/readiness slice for greenfield or re-bootstrap passes.

Strengthen implementation handoff rules: use outcomes and aliases for planning; do not assign container stories directly; load linked `Detail` artifacts before follow-up work on Epics 14-21; keep UX-DR41 through UX-DR59 visible in Admin Web UI, CLI, and MCP review; use PRD scope horizons before selecting work.

### UX

Align the CLI/Aspire platform baseline to Aspire CLI/AppHost SDK 13.3.2 and Aspire.Hosting 13.3.3 while retaining the Fluent UI v5 admin design baseline.

### Sprint Status

Add explicit planning guardrails near the workflow notes and comments beside Epic 22 container rows. Update the timestamp without changing any story status.

### Project Context

Update the agent-facing version facts so future implementation agents see the current repository/tooling baseline.

## 5. Implementation Handoff

Scope classification: Minor planning correction.

Handoff recipients: Developer agent and future story creation/review workflows.

Success criteria:

- Future sprint planning uses epic outcomes and capability aliases rather than technical titles.
- Stories 22.1 and 22.5 are not assigned directly.
- Epics 14-21 follow-ups cite and load linked `Detail` artifacts.
- WS-1 is first-class and BDD-formatted.
- PRD, architecture, UX, and project context agree on the current technology baseline.
- Scope horizon rules prevent v3/v4 roadmap concepts from entering implementation without new routing.
- UX-DR41 through UX-DR59 remain visible in Admin Web UI, CLI, and MCP story validation.
- Epic 11 remains supplemental unless the PRD explicitly adds new FR coverage.

Correct Course workflow complete for this readiness cleanup.
