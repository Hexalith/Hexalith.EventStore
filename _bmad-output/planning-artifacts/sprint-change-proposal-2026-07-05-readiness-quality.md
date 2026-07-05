---
project: eventstore
date: 2026-07-05
workflow: bmad-correct-course
mode: batch
status: approved
trigger:
  report: _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md
  readiness: needs_work
scope_classification: moderate
artifacts_reviewed:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md
  - _bmad-output/specs/spec-eventstore-phase-4-readiness-recovery/readiness-gates.md
artifacts_updated:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/specs/spec-eventstore-phase-4-readiness-recovery/readiness-gates.md
  - _bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md
  - _bmad-output/planning-artifacts/backlog/iam-1-admin-oidc-login.md
  - _bmad-output/planning-artifacts/backlog/kit-1-aggregate-test-kit.md
  - _bmad-output/planning-artifacts/backlog/rest-generator-hardening.md
approval: approved
approved_by: Administrator
approved_at: 2026-07-05T17:08:56+02:00
---

# Sprint Change Proposal - Readiness Quality Corrections

## 1. Issue Summary

The 2026-07-05 implementation readiness assessment improved the Phase 4 planning baseline but still returned **needs_work**. FR traceability is complete: all 35 PRD functional requirements map to epic/story coverage. The remaining issue is planning execution quality.

Evidence from `_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md`:

- Stories 1.3, 1.6, 2.4, 3.7, 5.6, 7.2, 7.3, and 7.4 remain oversized or multi-concern.
- Stories 6.1, 6.3, and 6.5 require approved specs but did not name exact output paths or approval evidence.
- Story 5.2 used weak request-size wording: "tested or documented."
- Story 7.5 read like an implementation story even though it is backlog/planning work.

## 2. Impact Analysis

### Epic Impact

No epic is removed or resequenced. The corrective action preserves the seven-epic Phase 4 scope and changes how stories are made ready for implementation.

| Epic | Impact |
| --- | --- |
| Epic 1 | Adds coordinated-slice gates for read-model/cursor work and Sample/Tenants adoption. |
| Epic 2 | Adds coordinated-slice gates for Tenants generated API host, UI alignment, and validation. |
| Epic 3 | Adds coordinated-slice gates for CI/shared workflow work versus supply-chain backlog. |
| Epic 5 | Tightens Story 5.2 request-size criteria and gates topology hardening as a coordinated slice. |
| Epic 6 | Adds exact spec paths and approval evidence before implementation stories start. |
| Epic 7 | Gates admin/deployment/test recovery stories and reclassifies Story 7.5 as backlog artifact work. |

### Artifact Impact

- `epics.md` now owns explicit readiness execution gates.
- `prd.md` now points to the corrected gates instead of listing them as unresolved follow-on work.
- `readiness-gates.md` now mirrors the corrected gates for future readiness checks.
- Four backlog artifacts now exist under `_bmad-output/planning-artifacts/backlog/`.

### Technical Impact

No code rollback is recommended. No implementation code changes are required by this course correction. The implementation impact is that future story files must either split the oversized scope or carry the coordinated-slice owner, review boundary, and validation commands forward.

## 3. Recommended Approach

Recommended path: **Direct Adjustment**.

The MVP scope is still achievable. The right correction is to repair the implementation handoff artifacts rather than reduce scope or rollback work.

Effort estimate: **Medium** planning refinement.

Risk level: **Low to Medium** after this correction. The remaining risk is enforcement: implementation story files must copy the gates forward instead of reverting to broad acceptance wording.

## 4. Detailed Change Proposals

### Story Sizing Gate

Artifact: `_bmad-output/planning-artifacts/epics.md`

OLD:

```markdown
Oversized stories are present only as broad implementation stories.
```

NEW:

```markdown
Implementation Readiness Execution Gates defines coordinated-slice gates for Stories 1.3, 1.6, 2.4, 3.7, 5.6, 7.2, 7.3, and 7.4.

Each story must either split into the named implementation slices or carry owner, review boundary, and exact validation commands into the implementation story file.
```

Rationale: This keeps the current epic numbering stable while making implementation review boundaries explicit.

### Story 5.2 Request-Size Criteria

Artifact: `_bmad-output/planning-artifacts/epics.md`

OLD:

```markdown
And limits are tested or documented.
```

NEW:

```markdown
The default maximum request body size is 1_048_576 bytes for representative admin JSON write/sandbox bodies.
AdminBackupsController.ImportStream uses 10 * 1024 * 1024 bytes.
Tests cover exact-limit accepted behavior, excessive-request rejection, bounded support-safe output, and no upstream service invocation on excessive requests.
```

Rationale: Request-size handling is a security boundary and needs objective acceptance evidence.

### Epic 6 Spec Gates

Artifact: `_bmad-output/planning-artifacts/epics.md`

OLD:

```markdown
Spec stories require approval before implementation but do not name exact spec outputs.
```

NEW:

```markdown
Story 6.1 output: _bmad-output/implementation-artifacts/spec-folded-snapshot.md
Story 6.3 output: _bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md
Story 6.5 output: _bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md
```

Rationale: Implementation stories 6.2, 6.4, and 6.6 now have concrete preflight gates.

### Story 7.5 Backlog Reclassification

Artifact: `_bmad-output/planning-artifacts/epics.md`

OLD:

```markdown
Story 7.5: Track Future Capability Backlog
```

NEW:

```markdown
Classification: Planning/backlog artifact.

Required outputs:
- _bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md
- _bmad-output/planning-artifacts/backlog/iam-1-admin-oidc-login.md
- _bmad-output/planning-artifacts/backlog/kit-1-aggregate-test-kit.md
- _bmad-output/planning-artifacts/backlog/rest-generator-hardening.md
```

Rationale: FR35 requires backlog capability tracking, not hidden implementation.

## 5. Implementation Handoff

Change scope classification: **Moderate**.

Handoff recipients:

- Product Owner / Product Manager: approve the backlog-artifact classification and ensure future implementation story creation copies the coordinated-slice gates forward.
- Developer agent: when creating implementation story files, split the listed stories or preserve the owner/review/validation gate.
- Architect: approve Epic 6 specs before implementation stories 6.2, 6.4, and 6.6 start.
- Test Architect: verify higher-tier stories keep persisted state/read-model/CloudEvent evidence expectations.

Success criteria:

- Readiness rerun no longer reports oversized stories, missing spec paths, weak request-size criteria, or Story 7.5 classification as blockers.
- No MVP scope is removed.
- No implementation story starts without its readiness gate when the corresponding gate applies.

## 6. Checklist Results

| Checklist item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story | N/A | Trigger is implementation readiness assessment, not one implementation story. |
| 1.2 Core problem | Done | Planning quality blockers after FR traceability reached 100%. |
| 1.3 Evidence | Done | Readiness report and current planning artifacts loaded. |
| 2.1 Current epic impact | Done | No epic invalidated. |
| 2.2 Epic-level changes | Done | Direct story-quality gates, no new epic required. |
| 2.3 Remaining epics | Done | Epics 1, 2, 3, 5, 6, and 7 affected. |
| 2.4 New/obsolete epics | Done | None. |
| 2.5 Priority/order | Done | Epic 6 spec stories must precede dependent implementation stories. |
| 3.1 PRD conflicts | Done | PRD updated to point to corrected gates. |
| 3.2 Architecture conflicts | Done | Architecture already supports spec-first and coordinated gates. |
| 3.3 UX conflicts | Done | UX remains aligned; Story 7.2 keeps admin honesty UI evidence. |
| 3.4 Other artifacts | Done | Readiness gates and backlog artifacts updated. |
| 4.1 Direct adjustment | Viable | Selected. |
| 4.2 Rollback | Not viable | No implementation rollback needed. |
| 4.3 MVP review | Not viable | No scope reduction needed. |
| 4.4 Path forward | Done | Direct adjustment to planning artifacts. |
| 5.1 Issue summary | Done | Included above. |
| 5.2 Impact summary | Done | Included above. |
| 5.3 Recommendation | Done | Direct adjustment. |
| 5.4 MVP impact | Done | MVP unchanged. |
| 5.5 Handoff | Done | Moderate backlog coordination. |
| 6.1 Checklist completion | Done | Approval recorded 2026-07-05T17:08:56+02:00. |
| 6.2 Proposal accuracy | Done | Proposal matches edited artifacts. |
| 6.3 User approval | Done | Approved by Administrator. |
| 6.4 Sprint status update | N/A | No epic/story IDs were added or removed in sprint status. |
| 6.5 Next steps | Done | Rerun readiness and carry gates into implementation story files. |

## 7. Approval

Approved by Administrator at 2026-07-05T17:08:56+02:00.

## 8. Handoff Log

| Time | Route | Notes |
| --- | --- | --- |
| 2026-07-05T17:08:56+02:00 | Product Owner / Developer agents | Moderate backlog coordination approved. Future implementation story files must split the gated stories or carry the coordinated-slice owner, review boundary, and validation commands forward. |
| 2026-07-05T17:08:56+02:00 | Architect | Epic 6 implementation remains blocked until approved specs exist at the named paths. |
| 2026-07-05T17:08:56+02:00 | Test Architect | Higher-tier stories must preserve persisted state/read-model/CloudEvent evidence expectations. |
