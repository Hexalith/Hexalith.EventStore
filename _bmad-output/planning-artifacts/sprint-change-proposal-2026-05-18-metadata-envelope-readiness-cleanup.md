---
date: 2026-05-18
project: Hexalith.EventStore
sourceReport: _bmad-output/planning-artifacts/implementation-readiness-report-2026-05-18.md
scope: Minor
status: Approved and Applied
approvedBy: Jerome
approvedOn: 2026-05-18
---

# Sprint Change Proposal: Metadata Envelope Readiness Cleanup

## 1. Issue Summary

The implementation readiness assessment completed on 2026-05-18 found that the planning set has complete functional requirement traceability, with 104/104 PRD functional requirements covered by epics. The readiness status remained NEEDS WORK because secondary UX and architecture wording still referenced an obsolete 11-field event metadata envelope while PRD FR11 and architecture SEC-1 define the current 14-field metadata envelope with two-document storage.

This drift affects a security, compatibility, and persistence boundary. Implementers reading secondary tables could incorrectly assume the old metadata shape and miss `metadataVersion`, global position, aggregate type, or the two-document metadata/payload storage contract.

Evidence:

- `_bmad-output/planning-artifacts/prd.md` FR11 defines a 14-field event metadata envelope with separate metadata JSON and payload JSON.
- `_bmad-output/planning-artifacts/architecture.md` SEC-1 already states that EventStore owns all 14 metadata fields.
- `_bmad-output/planning-artifacts/architecture.md` still had two stale 11-field references.
- `_bmad-output/planning-artifacts/ux-design-specification.md` still had one stale 11-field innovation-pattern reference.

## 2. Impact Analysis

Epic impact:

- No new epic is required.
- No epic scope, ordering, or FR coverage changes are required.
- The existing Epic 1 envelope ownership, Epic 2 persistence, and Epic 22 gateway contract boundaries remain valid.

Story impact:

- No new story is required for this cleanup.
- Future implementation must continue to use child-story assignment boundaries for broad or container work.
- Story 22.1 and Story 22.5 remain container-only and must not be assigned directly.

Artifact conflicts:

- Architecture needed two wording corrections to align cross-cutting concerns and the Contracts tree comment with FR11.
- UX needed one wording correction to align the innovation pattern table with FR11.
- Epics needed readiness guardrail text to make broad Epic 7/Epic 8 assignment boundaries explicit.
- Epics 3, 8, 9, 10, 12, 13, and 22 needed explicit `Outcome:` markers for easier automated readiness review.

Technical impact:

- No runtime code, schema, package, AppHost, or DAPR configuration changes are required.
- No sprint status rows are added or removed because the correction does not add, remove, renumber, or reassign stories.

## 3. Recommended Approach

Recommended path: Direct Adjustment.

Rationale:

- The issue is a planning-artifact consistency defect, not a product scope change.
- The PRD and primary architecture security decision already agree on the 14-field envelope.
- Low-risk document edits remove the contradiction without changing MVP scope or implementation sequencing.
- Rollback and PRD MVP review are not viable because the current 14-field envelope is already the accepted source of truth.

Effort estimate: Low.

Risk level: Low.

Timeline impact: None expected.

## 4. Detailed Change Proposals

Architecture: Cross-Cutting Concerns

Old:

```text
11-field metadata schema is irreversible; EventStore owns all metadata population; extension bag for unforeseen needs
```

New:

```text
14-field metadata schema with `metadataVersion` and separate metadata/payload JSON documents is irreversible; EventStore owns all metadata population; extension bag for unforeseen needs
```

Architecture: Contracts Tree Comment

Old:

```text
EventEnvelope.cs                # 11-field metadata + payload + extensions
```

New:

```text
EventEnvelope.cs                # 14-field metadata + separate payload + extensions
```

UX Design Specification: Innovation Pattern Table

Old:

```text
Event envelope with 11 metadata fields
```

New:

```text
Event envelope with 14 metadata fields
```

Epics: Implementation Handoff Rules

Add a binding readiness guardrail that broad Epic 7 and Epic 8 work must be assigned and validated through their existing story boundaries rather than as single umbrella implementation units.

Epics: Outcome Normalization

Add explicit `Outcome:` markers to Epics 3, 8, 9, 10, 12, 13, and 22 so automated and human reviews can identify the user-value statement consistently.

## 5. Implementation Handoff

Scope classification: Minor.

Handoff recipient: Developer agent for direct artifact cleanup.

Responsibilities:

- Update architecture and UX stale envelope references.
- Strengthen epics handoff rules for broad historical work.
- Normalize missing outcome markers.
- Verify no stale 11-field envelope references remain in PRD, architecture, UX, or epics.
- Leave sprint status unchanged because no epic/story inventory changed.

Success criteria:

- `rg "11-field|11 metadata" _bmad-output/planning-artifacts/prd.md _bmad-output/planning-artifacts/architecture.md _bmad-output/planning-artifacts/ux-design-specification.md _bmad-output/planning-artifacts/epics.md` returns no active stale references.
- Architecture and UX references match PRD FR11's 14-field metadata envelope and two-document storage model.
- Epics 3, 8, 9, 10, 12, 13, and 22 contain explicit `Outcome:` markers.
- Container-only Story 22.1 and Story 22.5 remain documented as non-assignable parent stories.

## 6. Checklist Summary

- [x] 1.1 Trigger identified: 2026-05-18 implementation readiness assessment.
- [x] 1.2 Core problem defined: stale secondary planning references to an obsolete 11-field metadata envelope.
- [x] 1.3 Supporting evidence collected from PRD, architecture, UX, epics, and readiness report.
- [x] 2.1-2.5 Epic impact assessed: no epic restructuring required.
- [x] 3.1 PRD conflict checked: PRD is source of truth and needs no change.
- [x] 3.2 Architecture conflict checked: two stale references corrected.
- [x] 3.3 UX conflict checked: one stale reference corrected.
- [x] 3.4 Other artifacts checked: epics handoff rules and outcome markers updated; sprint status unchanged.
- [x] 4.1 Direct Adjustment selected.
- [N/A] 4.2 Rollback not applicable.
- [N/A] 4.3 PRD MVP review not applicable.
- [x] 5.1-5.5 Proposal, action plan, and handoff documented.
- [x] 6.1-6.2 Proposal reviewed for consistency.
- [x] 6.3 Explicit approval received from Jerome on 2026-05-18.
- [N/A] 6.4 Sprint status unchanged because no epic/story inventory changed.
- [x] 6.5 Next-step handoff documented.
