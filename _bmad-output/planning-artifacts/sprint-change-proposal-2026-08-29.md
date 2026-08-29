# Sprint Change Proposal - Epic 3 Planning Reconciliation

Date: 2026-08-29
Project: eventstore
Planning mode: Incremental
Scope classification: Moderate
Recommended path: Direct Adjustment
Approval: Approved by Administrator on 2026-08-29

## 1. Issue Summary

Sprint tracking generation stopped at a readiness gate because an approved change and the current planning artifacts disagree:

- The approved 2026-08-20 proposal requires Story 3.16 in `epics.md` and `sprint-status.yaml`, but neither artifact contains it.
- The SHA-256 recorded for `architecture.md` in the `epics.md` input-document frontmatter no longer matches the current architecture bytes.
- `architecture.md` still says the Story 3.13-3.15 reconciliation is stale and implementation handoff is blocked, while the epic plan already carries the corrected story ownership and the sprint tracker records all three stories as `done`.

This is an artifact-synchronization failure after an approved change, not a new product requirement or an implementation defect. No code, dependency, release, deployment, or submodule mutation is authorized by this proposal.

Evidence verified on 2026-08-29:

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-20.md` requires Story 3.16 in the epic plan and sprint tracker.
- `_bmad-output/planning-artifacts/epics.md` ends Epic 3 after Story 3.15 and records architecture digest `9a20ba5c6860f124ca52a8801e531132a96dd0a761856fdc4684390d848f4101`.
- The final pre-application `architecture.md` SHA-256 is `3bbd2f9aec29bb5f914438ce05afcfaad975a5cec44c7e9b184348d49c316791`; an earlier incremental-review snapshot was superseded by preserved concurrent SDK and Stack edits.
- The other four recorded epic input-document digests recompute exactly; only architecture requires reconciliation.
- `sprint-status.yaml` records Epic 3 as `in-progress` and Stories 3.13, 3.14, and 3.15 as `done`.
- The working tree contains unrelated user changes, including SDK and Stack-row edits in `architecture.md`; all are preserved by the exact localized substitutions below.

## 2. Impact Analysis

### Epic and story impact

- Epic 3 remains the sole affected epic and remains `in-progress`.
- Add the already-approved Story 3.16 after Story 3.15 without reopening or rewriting Stories 3.11 or 3.13-3.15.
- No story is removed, split, renumbered, or resequenced.
- Epics 1, 2, and 4-8 remain unchanged.

### Artifact impact

- PRD: no change. Existing FR19, FR21, NFR9, and dependency-authority rules remain valid.
- UX: no change. No interaction or design contract is affected.
- Epics: add Story 3.16, replace the stale completion comment, and reconcile the architecture input digest.
- Sprint status: add Story 3.16 as `backlog` and refresh tracker update metadata. Preserve Epic 3 and all existing story statuses.
- Architecture: remove the resolved deployed-parity handoff block, remove its deferred row, record the governing proposals, and adopt the approved Story 3.16 Stack-authority wording. Preserve current Stack versions and all unrelated working-tree edits.
- Implementation artifacts and source code: no change.

### Delivery impact

The correction is low effort and low implementation risk. It repairs planning state only. Story 3.16 remains a separate backlog implementation unit with its existing evidence, authority, and validation gates.

## 3. Recommended Approach

Use Direct Adjustment. Apply the six incrementally accepted edits as one atomic planning reconciliation, verify exact story/status identities and all five input digests, then rerun sprint planning.

Rollback of the product or completed stories is unnecessary. If validation fails, revert only this proposal's localized planning edits while preserving all pre-existing working-tree changes.

## 4. Detailed Change Proposals

### 4.1 Add approved Story 3.16 to Epic 3

Artifact: `_bmad-output/planning-artifacts/epics.md`

OLD:

```markdown
<!-- Epic 3 story set confirmed complete. -->
```

NEW:

Insert verbatim the complete approved Story 3.16 block from Section 4.1 of `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-20.md`, beginning:

```markdown
### Story 3.16: Latest-Compatible Dependency And Root Submodule Refresh
```

and ending:

```markdown
<!-- Epic 3 story set includes the approved Story 3.16 maintenance follow-up. -->
```

Rationale: propagate the already-approved maintenance story without altering any frozen evidence story.

### 4.2 Add Story 3.16 to sprint tracking

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

OLD:

```yaml
3-15-corrected-deployed-runtime-parity-closure: done
epic-3-retrospective: optional
```

NEW:

```yaml
3-15-corrected-deployed-runtime-parity-closure: done
# Approved follow-up to the frozen Story 3.11 audit; current catalog and root gitlinks
# are re-evaluated from authoritative upstream evidence without rewriting prior packets.
3-16-latest-compatible-dependency-and-root-submodule-refresh: backlog
epic-3-retrospective: optional
```

Rationale: add the missing backlog row while preserving Epic 3 as `in-progress` and Stories 3.13-3.15 as `done`.

### 4.3 Reconcile deployed-runtime architecture status

Artifact: `_bmad-output/planning-artifacts/architecture.md`

OLD:

```markdown
For deployed-runtime parity, the approved 2026-08-16 sprint change proposal and the final PRD
updated 2026-08-16 govern over stale epic, story, specification, and sprint-tracking text.
Implementation handoff is blocked until those downstream artifacts atomically encode Story 3.13
as the rejected `v3.94.1` disposition, Story 3.14 as the corrective release, Story 3.15 as positive
parity closure, and Epic 3 as open. No stale artifact can authorize positive `v3.94.1` closure.
```

NEW:

```markdown
For deployed-runtime parity, the approved 2026-08-16 sprint change proposal and the final PRD
updated 2026-08-16 govern. The Epic 3 plan and sprint tracker encode Story 3.13 as the rejected
`v3.94.1` disposition, Story 3.14 as the corrective release, and Story 3.15 as positive parity
closure. Epic 3 remains open for the approved Story 3.16 maintenance follow-up. Planning or story
status never authorizes release, deployment, consumer removal, or positive `v3.94.1` closure.
```

Also remove the resolved `Atomic 2026-08-16 deployed-parity plan alignment` row from the Deferred table.

Rationale: remove a false handoff blocker while retaining the non-authorization boundary.

### 4.4 Record architecture provenance and Stack authority

Artifact: `_bmad-output/planning-artifacts/architecture.md`

Update `updated: 2026-08-16` to `updated: 2026-08-29` and add these sources after the 2026-08-16 proposal:

```yaml
- _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-20.md
- _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-29.md
```

Replace the Stack preamble:

```markdown
The table is a dated rendering of the current planning baseline; the Builds catalog remains live version authority and always wins. Story 3.11 updates version rows only from accepted shared-catalog and compatibility evidence.
```

with:

```markdown
The table is a dated rendering of the current planning baseline; the Builds catalog remains live version authority and always wins. Story 3.11 established the validated refresh contract; approved follow-ups such as Story 3.16 update version rows only from accepted shared-catalog and compatibility evidence.
```

Rationale: bind Story 3.16 and this reconciliation to their proposals without changing any Stack version row.

### 4.5 Reconcile the architecture input digest

Artifact: `_bmad-output/planning-artifacts/epics.md`

OLD:

```yaml
_bmad-output/planning-artifacts/architecture.md: 9a20ba5c6860f124ca52a8801e531132a96dd0a761856fdc4684390d848f4101
```

NEW:

```yaml
_bmad-output/planning-artifacts/architecture.md: 623bc23e453aba5a703c5aa1b208bf9f985f5937f5900f5e665f5cb5abe5ca94
```

The new value is the SHA-256 of the exact future architecture bytes after Sections 4.3-4.4, including all preserved pre-existing user changes. The other four input digests remain unchanged.

### 4.6 Refresh sprint-tracker metadata

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

OLD:

```yaml
# last_updated: 2026-08-26T12:34:20Z
last_updated: '08-26-2026'
```

NEW:

```yaml
# last_updated: 2026-08-29T10:09:18Z
last_updated: '08-29-2026'
```

Rationale: keep tracker metadata consistent with the Story 3.16 row addition.

## 5. Change Analysis Checklist

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering change | [x] | Readiness gate found the approved 2026-08-20 change was not propagated. |
| 1.2 Core problem | [x] | Cross-artifact synchronization failure; no new requirement or technical limitation. |
| 1.3 Evidence | [x] | Approved proposal, current epic plan, tracker states, architecture text, and five SHA-256 checks verified. |
| 2.1 Current epic impact | [x] | Epic 3 only; it remains completable and `in-progress`. |
| 2.2 Story impact | [x] | Add approved Story 3.16; preserve Stories 3.11 and 3.13-3.15. |
| 2.3 Future epic impact | [N/A] | No future epic requirement or sequence changes. |
| 2.4 Epic validity | [x] | No epic is obsolete and no new epic is needed. |
| 2.5 Sequence | [x] | Story 3.16 follows Story 3.15 in the plan and remains backlog. |
| 3.1 PRD conflict | [x] | No PRD conflict or edit. |
| 3.2 Architecture conflict | [x] | Remove stale status/deferred wording; retain technical decisions and authority guards. |
| 3.3 UX conflict | [N/A] | No UX contract change. |
| 3.4 Other artifacts | [x] | Epic plan, tracker, proposal, and architecture digest only. |
| 4.1 Direct adjustment | [x] | Recommended; localized and reversible. |
| 4.2 Rollback | [N/A] | Product/story rollback is unnecessary. |
| 4.3 MVP review | [N/A] | Scope and MVP remain unchanged. |
| 4.4 Recommended path | [x] | Direct Adjustment. |
| 5.1 Issue summary | [x] | Trigger, evidence, and non-authorizing scope recorded. |
| 5.2 Impact summary | [x] | Epic and artifact impacts recorded. |
| 5.3 Approach | [x] | Effort, risk, rollback, and rerun path recorded. |
| 5.4 Detailed proposals | [x] | Six exact edits accepted incrementally by Administrator. |
| 5.5 Handoff | [x] | Owners, order, validation, and success criteria supplied below. |
| 6.1 Proposal coherence | [x] | Repairs all three readiness concerns without changing product behavior. |
| 6.2 Proposal accuracy | [x] | Exact current states and future architecture digest verified. |
| 6.3 Approval | [x] | Six incremental edits and final implementation explicitly approved by Administrator. |
| 6.4 Sprint status | [x] | Story 3.16 added as backlog; existing Epic 3 and story statuses preserved. |
| 6.5 Handoff | [x] | Moderate planning correction routed to Product Owner and Developer. |

## 6. Implementation Handoff

Primary routing:

- Product Owner: confirm the final proposal and Story 3.16 backlog placement.
- Developer: apply the approved localized edits without overwriting unrelated working-tree changes.
- Architect: verify the stale handoff blocker is removed while AD-11 authority remains intact.

Execution order after explicit final approval:

1. Mark this proposal approved.
2. Apply the architecture status, provenance, and Stack-authority edits without changing current version rows.
3. Recompute `architecture.md` SHA-256 and require exact value `623bc23e453aba5a703c5aa1b208bf9f985f5937f5900f5e665f5cb5abe5ca94`.
4. Add Story 3.16 and update only the architecture digest in `epics.md`.
5. Add Story 3.16 as backlog and refresh tracker metadata without changing any other status.
6. Validate story identities, stale-text absence, YAML structure, all five epic input digests, whitespace, and the scoped diff.
7. Rerun sprint planning/readiness generation separately.

Success criteria:

- Story 3.16 exists exactly once in both the epic plan and tracker and is `backlog` in the tracker.
- Stories 3.13-3.15 remain `done`; Epic 3 remains `in-progress`.
- The architecture contains no resolved deployed-parity handoff block or deferred row.
- All five `epics.md` input-document digests match their current files.
- PRD, UX, code, dependencies, releases, deployments, gitlinks, and unrelated working-tree changes remain untouched.

## 7. Workflow Execution Log

- 2026-08-29: Administrator invoked `bmad-correct-course` for the readiness-gate concerns.
- 2026-08-29: Incremental review mode selected.
- 2026-08-29: Six detailed edits accepted individually by Administrator.
- 2026-08-29: Proposal generated and final implementation explicitly approved by Administrator.
- 2026-08-29: Approved edits applied; architecture digest, all five epic input digests, tracker YAML, story identities, stale-text absence, and whitespace validated.
