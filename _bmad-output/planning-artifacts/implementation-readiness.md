# Implementation Readiness Assessment

- Assessment date: 2026-10-06
- Project: eventstore
- Intent: sprint-planning
- Readiness verdict: FAIL

The planning does not currently support a general implementation handoff. Mandatory controls and implementation decisions are missing, and the recorded planning inputs and story lifecycle accounts disagree. Sprint tracking was not regenerated.

## Assessment Scope

The assessment examined the canonical [PRD](prd.md), [architecture](architecture.md), [UX handoff](ux.md), detailed [DESIGN](ux-designs/ux-eventstore-2026-07-05/DESIGN.md) and [EXPERIENCE](ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md), [epics](epics.md), existing [sprint tracking](../implementation-artifacts/sprint-status.yaml), relevant implementation artifacts, and repository documentation.

The assessment ran against commit `a6fc951e2c01c1c816aed1d03c851c0639f12cdd` with local modifications. This report was saved after the checkout advanced to `12aa5d3a0b7b6b760e339cd9319e49123d4222a7`. The six canonical planning-file digests below were rechecked when saving and remained unchanged. Saving this report does not constitute a new runtime validation, owner approval, or approved atomic baseline.

## Blocking Findings

### 1. Required Gate Controls Are Missing

PRD §0 and §11.4 require executable corrective-work authorization, high-risk assurance, and MVP coverage controls. The following artifacts were absent when assessed:

- `tools/validate-corrective-work-authorization.py`
- `_bmad-output/implementation-artifacts/evidence/corrective-work-authorizations/`
- `tools/validate-phase-4-gate-risk-matrix.py`
- `_bmad-output/implementation-artifacts/evidence/phase-4-gate-risk-matrix.json`
- `tools/validate-phase-4-mvp-coverage.py`
- `_bmad-output/implementation-artifacts/evidence/phase-4-mvp-coverage.json`

Stories 9.1 and 9.2 exist in the epics and tracker but remain `backlog`. Story 9.2 explicitly depends on completed Story 9.1 and a valid authorization record. These missing controls prevent their governed evidence from supporting a passing readiness result.

Repair: use `bmad-build` for scoped Story 9.1 and then Story 9.2. Story 9.1's recorded bootstrap rule requires a dated owner authorization in its story before development. Use `bmad-correct-course` and `bmad-create-epics-and-stories` to assign the remaining MVP coverage work to an explicit slice; it must not be silently added to either existing story.

### 2. The Planning Baseline Is Not Reconciled

All five SHA-256 values in `epics.md` under `inputDocumentDigests` differ from the corresponding current files. Architecture and both detailed UX documents still declare `status: draft`. EXPERIENCE also retains unconfirmed assumptions about restore/import visibility and tenant provisioning.

| Input | Recorded SHA-256 in epics | Observed SHA-256 |
| --- | --- | --- |
| `prd.md` | `b99effdb414209da373433d9b4d2075072ca950e89534b22b81155236f650662` | `f6a2a0d674de8bb0d011fb8ac2177baa8cfe4a8098122e9cf7b026272c2f9e09` |
| `architecture.md` | `7e3dbc7bd335034bd9b98cadfed8b14650b7d811321b326b8f32e1b280960d51` | `ca7898c7d0bd22d9f5b0260f6986b49bb4a0153bc1fae6d6fe0149f81d900cab` |
| Detailed `DESIGN.md` | `76be269737ddacb5d5680862590893cd4e69d469222fd1eba554aa4084339a4f` | `3f4f0181ea24b5ed7544b6cb8482cd73b1aadba7ddbef47fd135e080a2d8365d` |
| Detailed `EXPERIENCE.md` | `06de15b349224772146fa279f9a61ca6c0fffe9d17774588cedd330690042eb6` | `11f754031cb5f7f8c376787573c32ba9ac0c68c4718e6b4b86281f579e8c9cab` |
| `ux.md` | `2927f97d4fe7262ec5886084a974d9d5c21240560e6563ab84f84b90e4dd670a` | `4e8eb907b15d5b5badc72b5ec70f93c3671deeb624e80fa33aec868847ef766c` |

The observed `epics.md` digest is `8a4493d19d44fa3eb5ad5c4ac6863038affaaa7b18cb957643e8678ca8601bb5`.

PRD §11.3 explicitly prohibits treating a hash-only refresh as substantive reconciliation or approval. Matching digests alone would therefore not resolve this finding.

Repair: use `bmad-correct-course` to reconcile requirements, story slices, lifecycle accounts, and evidence. Use `bmad-architecture` and `bmad-ux` to resolve their outstanding decisions and approval state before binding the reconciled baseline.

### 3. Mandatory Decisions And Requirement Ownership Are Incomplete

The projection-cost specification `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md` is absent. G-NFR8 requires an approved numeric budget, workload/page-size model, validation command, evidence identity, and Story 6.4 authorization before that implementation starts.

The required `docs/reference/aot-and-trimming-posture.md` is also absent. G-NFR18 requires the document, an owning story, and reviewed validation evidence. PRD §11.4 further records missing primary ownership for FR36-C3, FR36-C4, FR36-C5, NFR17-C5, and other incomplete NFR slices; the current epics do not provide the complete required mapping.

Additional mandatory authority artifacts are absent: `deploy/dapr/production-profile.yaml`, `_bmad-output/implementation-artifacts/evidence/consumer-removal-manifest.json`, and `tools/validate-consumer-removal-authority.py`. The PRD also retains unresolved append prevention, OQ8 design authority, tenant normalization, status identity, compatibility, and all-host authentication gates. Those recorded obligations cannot be closed by regenerating tracking.

Repair: use `bmad-spec` for the projection-cost specification and `bmad-create-epics-and-stories` for missing ownership. Use `bmad-correct-course` for changes spanning requirements, architecture, evidence, and story scope.

### 4. Story Lifecycle Accounts Conflict

The epics still describe Story 6.1 as `backlog` and say `spec-folded-snapshot.md` is absent. That specification exists, and the sprint tracker records Story 6.1 as `in-progress`. Stories 5.2, 5.3, and 5.4 also retain narrative lifecycle accounts that disagree with the tracker and later PRD reconciliation.

These disagreements prevent a mechanical status merge from establishing which evidence and completion claims are authoritative. The existing lifecycle values were preserved during this assessment.

Repair: use `bmad-correct-course` to reconcile each affected story against its canonical specification and bound acceptance evidence. G-BASELINE and OR15 require guarded lifecycle consistency rather than selecting whichever label appears most advanced.

## Verification And Limits

- Computed SHA-256 digests for the five recorded epic inputs and confirmed five mismatches.
- Checked the required artifact paths and confirmed the absences listed above.
- Read canonical document statuses, required gate contracts, Story 9.1/9.2 dependencies, and the affected lifecycle accounts.
- Did not execute missing validators or infer their results from story labels. The PRD's runtime and evidence gate accounts remain recorded obligations, not newly executed test results.
- Did not run `sprint_plan.py generate`: the readiness gate failed before the generation phase.
- Preserved the sprint tracking file and existing user changes.

## Next Action

Run `bmad-correct-course` to produce the reconciled planning baseline and explicit corrective slices. Then complete the scoped bootstrap and dependent gate-control work, resolve missing decisions and ownership, and rerun readiness after the mandatory inputs and approvals are available.

The [bmad-sprint-planning skill](../../.agents/skills/bmad-sprint-planning/SKILL.md) permits generation only after PASS. Its [readiness gate](../../.agents/skills/bmad-sprint-planning/references/readiness-gate.md) requires a FAIL verdict to stop the planning run. This report grants no implementation, release, deployment, migration, or consumer-removal authority.
