---
title: Sprint Change Proposal - Correct-Course Story Rewrite Gate
date: 2026-07-05
project: eventstore
workflow: bmad-correct-course
mode: batch
status: approved
approved_by: Administrator
approved_on: 2026-07-05
scope_classification: minor
source:
  - _bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad-output/implementation-artifacts/D-5-proof-sample-blazorui-queries.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02-rest-api-external-host.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture.md
---

# Sprint Change Proposal: Correct-Course Story Rewrite Gate

## 1. Issue Summary

The Epic D retrospective left this open action item:

```yaml
- epic: D
  action: "Make correct-course story rewrites mandatory after architectural pivots."
  owner: "John (Product Manager)"
  status: open
```

The trigger is an observed process gap. The July 2 correct-course pivot moved generated REST controllers out of interactive UI hosts and into dedicated external API hosts. Some historical story files still carried active acceptance criteria, tasks, and project-structure notes for the abandoned "generate into BlazorUI" design. Later reviews had to repair or annotate those records after implementation had already moved on.

Evidence:

- `_bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md` says stale acceptance criteria after a correct-course decision created review noise and forced later story-record reconciliation.
- `_bmad-output/implementation-artifacts/D-5-proof-sample-blazorui-queries.md` records repeated review findings that the story still described the abandoned UI-host generator design after the external API host pivot.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02-rest-api-external-host.md` was the approved architectural pivot that superseded the earlier story assumptions.

The process failure is not code behavior. It is workflow enforcement: correct-course could approve a pivot without making story rewrites a hard precondition for continued implementation or review.

## 2. Impact Analysis

### Epic Impact

No product epic is reopened or resequenced. This is a planning-workflow hardening change.

Affected process surfaces:

- `bmad-correct-course`: must make story rewrites mandatory when a pivot supersedes active story ACs, tasks, Dev Notes, or design assumptions.
- `bmad-create-story`: must reject ready-for-dev story generation when active story content contradicts an approved pivot.
- `sprint-status.yaml`: the Epic D retrospective action item can close once the workflow rule is present.

### Story Impact

No Phase 4 implementation story is added.

Future story files affected by architectural pivots must contain current active ACs before `dev-story` or `code-review` continues. Stale ACs/tasks must either be removed from active scope or placed under an explicit superseded/correct-course note that cites the proposal path and states the old design is not implementation scope.

Historical Epic D files are not rewritten as part of this correction because they already record the shipped outcome and are useful audit evidence. The new rule applies to current and future affected stories.

### Artifact Conflicts

PRD, architecture, and UX do not need product-scope changes. They already own the current Phase 4 baseline and external API host boundary. The missing control belongs in BMAD workflow instructions and sprint action tracking.

### Technical Impact

No C# code, build configuration, or tests are affected.

The change updates workflow and tracking artifacts only:

- `.agents/skills/bmad-correct-course/SKILL.md`
- `.agents/skills/bmad-correct-course/checklist.md`
- `.agents/skills/bmad-create-story/SKILL.md`
- `.agents/skills/bmad-create-story/checklist.md`
- `_bmad/custom/bmad-correct-course.toml`
- `_bmad/custom/bmad-create-story.toml`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## 3. Recommended Approach

Recommended path: **Direct Adjustment**.

Make the rule explicit at both ends of the planning flow:

- During correct-course, every architectural pivot must name affected story files and define old-to-new rewrites.
- During create-story validation, a story cannot become ready-for-dev if active ACs/tasks contradict an approved pivot.

Rollback is not useful; this does not change shipped code. MVP review is not required because product scope is unchanged.

Effort estimate: **Low**.

Risk level: **Low**. The rule improves handoff quality and only affects planning artifacts.

## 4. Detailed Change Proposals

### 4.1 Correct-Course Story Rewrite Gate

Artifact: `.agents/skills/bmad-correct-course/SKILL.md`

OLD:

```markdown
For Story changes:
- Show old -> new text format
- Include story ID and section being modified
- Provide rationale for each change
```

NEW:

```markdown
For Story changes:
- If the change is an architectural pivot, or if an approved proposal supersedes story acceptance criteria, tasks, Dev Notes, or design assumptions, story rewrites are mandatory.
- Name every affected implementation story file and section.
- Require active story content to contain the current acceptance criteria before implementation or review continues.
- Remove stale active requirements, or mark them clearly as superseded with a pointer to the approved proposal.
- Do not leave a superseded-scope banner as the only correction when active ACs/tasks still instruct the abandoned design.
- Show old -> new text format
- Include story ID and section being modified
- Provide rationale for each change
```

Rationale: the workflow must treat pivot-driven story rewrite as a hard gate, not optional cleanup.

### 4.2 Correct-Course Checklist Enforcement

Artifact: `.agents/skills/bmad-correct-course/checklist.md`

OLD:

The checklist reviewed PRD, architecture, UX, and other artifacts but did not require identifying affected implementation story files after an architectural pivot.

NEW:

Add checklist items to:

- identify affected implementation story files after architectural pivots,
- define mandatory old-to-new story rewrites,
- halt when a pivot supersedes stories but no rewrite gate exists.

Rationale: checklist execution is where correct-course proves that the proposal covered every impacted artifact.

### 4.3 Create-Story Validation Gate

Artifact: `.agents/skills/bmad-create-story/SKILL.md`

OLD:

Create-story loaded epics, PRD, architecture, UX, prior stories, and git history, but did not explicitly reject stale story content after an approved pivot.

NEW:

Add a Correct-Course Rewrite Gate:

- scan approved sprint change proposals, readiness reports, retrospectives, architecture, previous stories, and sprint action items for superseded scope,
- rewrite active ACs/tasks/Dev Notes to the latest approved baseline,
- mark historical stale text only under an explicit superseded/correct-course note,
- prevent ready-for-dev status while active story content contradicts the approved architecture.

Rationale: the strongest enforcement point is the workflow that creates implementation story files.

### 4.4 Story Context Checklist Update

Artifact: `.agents/skills/bmad-create-story/checklist.md`

OLD:

The checklist focused on quality, architecture, previous-story intelligence, and implementation disasters.

NEW:

Add correct-course and architectural pivot reconciliation as a required source-analysis and critical-miss category.

Rationale: validation should catch stale pivot scope before a developer agent implements it.

### 4.5 Project-Level Workflow Overrides

Artifact: `_bmad/custom/bmad-correct-course.toml` and `_bmad/custom/bmad-create-story.toml`

OLD:

No project-level workflow override enforced the story rewrite gate if the installed skill text was refreshed.

NEW:

Add activation append steps that enforce the Correct-Course Story Rewrite Gate for both correct-course and create-story workflow runs.

Rationale: the rule should survive local skill refreshes and remain project-specific.

### 4.6 Sprint Tracking

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

OLD:

```yaml
- epic: D
  action: "Make correct-course story rewrites mandatory after architectural pivots."
  owner: "John (Product Manager)"
  status: open
```

NEW:

```yaml
- epic: D
  action: "Make correct-course story rewrites mandatory after architectural pivots."
  owner: "John (Product Manager)"
  status: done
```

Rationale: the process rule is now present in correct-course and create-story workflows.

## 5. Implementation Handoff

Change scope: **Minor**.

Route to: **Product Manager / Developer workflow owners**.

Responsibilities:

- Product Manager: keep future correct-course proposals from approving architectural pivots without affected-story rewrite gates.
- Developer/story workflow owner: ensure create-story output rewrites or supersedes stale active story content before ready-for-dev.
- Reviewer: treat active AC/task contradiction after an approved pivot as a blocking story-quality defect.

Success criteria:

- Correct-course proposals for architectural pivots name affected story files and required rewrites.
- Create-story validation fails any story whose active ACs/tasks contradict an approved pivot.
- Affected story files contain current active ACs before implementation or review continues.
- Stale story text is removed from active scope or explicitly marked superseded with the proposal path.

## 6. Checklist Results

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story identified | Done | Epic D retrospective action item 5; D5 story file is evidence. |
| 1.2 Core problem defined | Done | Approved pivots could leave active story content stale. |
| 1.3 Evidence gathered | Done | Epic D retro, D5 review notes, and July 2 external API host proposal loaded. |
| 2.1 Current epic impact | Done | No product epic reopened; process correction only. |
| 2.2 Epic-level changes | N/A | No epic scope changes. |
| 2.3 Remaining epics reviewed | Done | Future epics are affected only through story-creation quality gates. |
| 2.4 New/obsolete epics | N/A | No new epic required. |
| 2.5 Priority/order | Done | Gate applies before implementation or review continues. |
| 3.1 PRD conflicts | Done | No PRD change required. |
| 3.2 Architecture conflicts | Done | Architecture remains source of approved pivot truth. |
| 3.3 UX conflicts | N/A | No UX behavior changes. |
| 3.4 Other artifacts | Done | Correct-course, create-story, and sprint-status updated. |
| 3.5 Affected story files after pivot | Done | Future affected files must be named in proposals; historical D5 retained as audit evidence. |
| 4.1 Direct adjustment | Viable | Selected. |
| 4.2 Rollback | Not viable | No code or completed story rollback needed. |
| 4.3 MVP review | Not viable | Product scope unchanged. |
| 4.4 Path selected | Done | Direct workflow adjustment. |
| 5.1 Issue summary | Done | See section 1. |
| 5.2 Impact/artifacts | Done | See section 2. |
| 5.3 Path rationale | Done | See section 3. |
| 5.4 MVP impact/action plan | Done | MVP unchanged; workflow gate added. |
| 5.5 Handoff plan | Done | Product/story workflow owners and reviewers named. |
| 5.6 Story rewrite gate | Done | Mandatory rewrite gate added to correct-course and create-story. |
| 6.1 Checklist completion | Done | All applicable items addressed. |
| 6.2 Proposal accuracy | Done | Matches edited artifacts. |
| 6.3 User approval | Done | User explicitly requested this correct-course change. |
| 6.4 Sprint status update | Done | Retrospective action item marked done. |
| 6.5 Next steps | Done | Apply gate on future architectural pivots and ready-for-dev story creation. |

## 7. Approval

Approved by Administrator on 2026-07-05 through the direct request:

```text
$bmad-correct-course Make correct-course story rewrites mandatory after architectural pivots.
```

## 8. Handoff Log

| Time | Route | Notes |
| --- | --- | --- |
| 2026-07-05T19:41:27+02:00 | Product Manager / Developer workflow owners | Correct-course and create-story now require active story rewrites after architectural pivots before implementation or review continues. |
