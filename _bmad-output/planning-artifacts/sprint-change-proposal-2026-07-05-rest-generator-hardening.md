---
title: Sprint Change Proposal - REST Generator Hardening Story
date: 2026-07-05
project: eventstore
status: approved-for-story-creation
source:
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
---

# Sprint Change Proposal: REST Generator Hardening Story

## 1. Issue Summary

Epic D delivered the generated REST controller capability, but review and retrospective evidence left a focused generator-hardening backlog item. The trigger is the 2026-07-05 `deferred-work.md` entry and Epic D retrospective action item: create a dedicated REST generator hardening story instead of scattering diagnostics and generated-controller error semantics across security, correctness, or UI stories.

Evidence:

- `deferred-work.md` lists unsupported contract-shape diagnostics, duplicate command JSON-name diagnostics, invalid `RestQueryBinding` source diagnostics, empty constant binding diagnostics, route-template constraint behavior, case-insensitive route/JSON-name matching, referenced-contract incrementality, and generated external API error-semantics coverage.
- D5 and D7 story records show these were deliberately deferred as generator hardening, not proof-story blockers.
- `epics.md` Story 7.5 explicitly requires a dedicated story or backlog item for REST generator hardening.

## 2. Impact Analysis

**Epic impact:** No completed Epic D story is reopened. The work belongs to Epic 7 / FR35 backlog tracking and can later be scheduled as a focused generator implementation story.

**Story impact:** Adds `_bmad-output/implementation-artifacts/7-5-rest-generator-hardening.md` as the dedicated ready-for-dev story. Marks the Epic D retrospective action item as done because the requested story artifact now exists.

**Artifact conflicts:** No PRD or architecture change is needed. The current PRD already treats REST generator hardening as a backlog artifact, and architecture AD-3/AD-4 already fixes generated REST placement in external API hosts.

**Technical impact:** Future implementation touches `src/Hexalith.EventStore.RestApi.Generators/`, `src/Hexalith.EventStore.Contracts/Rest/` only if a contract addition is justified, and `tests/Hexalith.EventStore.RestApi.Generators.Tests/`. It must preserve gateway delegation and external API host boundaries.

## 3. Recommended Approach

Recommended path: **Direct Adjustment**.

Create one focused story and update sprint tracking. This is low-to-medium effort and low planning risk because the deferred items are already identified, the current generator code has focused test projects, and PRD/architecture already contain the backlog and boundary decisions.

Rollback is not useful because Epic D is complete and the requested work is additive hardening. MVP review is not required because this remains backlog/planning work unless the team explicitly schedules it for implementation.

## 4. Detailed Change Proposals

### Story Artifact

OLD:

No dedicated implementation story existed for REST generator hardening. The scope existed only as deferred bullets and action items.

NEW:

Create `_bmad-output/implementation-artifacts/7-5-rest-generator-hardening.md` with:

- ready-for-dev status,
- story context from `deferred-work.md`,
- explicit ACs for the generator diagnostics and generated external API error semantics,
- current-code-state notes for parser, emitter, diagnostics, route template parser, contracts, and tests,
- focused verification commands.

Rationale: gives the Developer agent one implementation target with enough context to avoid mixing generator hardening into unrelated stories.

### Sprint Status

OLD:

`sprint-status.yaml` had an open Epic D action item:

```yaml
- epic: D
  action: "Create a dedicated REST generator hardening story from deferred-work.md."
  owner: "John (Product Manager)"
  status: open
```

NEW:

Add a `7-5-rest-generator-hardening: ready-for-dev` story key and mark the action item `done`.

Rationale: closes the retrospective follow-through while making the new story visible to sprint tooling.

### PRD / Architecture / UX

OLD:

PRD and architecture already track REST generator hardening as backlog and external API host architecture as adopted.

NEW:

No change.

Rationale: changing these artifacts would duplicate existing accepted decisions. UX is not affected because this is generator/test hardening, not a module UI or UX change.

## 5. Implementation Handoff

Change scope: **Minor**.

Route to: Developer agent for direct implementation when scheduled.

Developer responsibilities:

- Implement the focused generator diagnostics and generated-controller tests in the story.
- Preserve external API host/gateway boundary.
- Run the verification commands listed in the story.
- Record any blocked validation with exact command and blocker.

Success criteria:

- The new story exists and is marked ready-for-dev.
- The sprint-status action item is closed.
- No completed Epic D story is reopened.
- PRD/architecture remain consistent with the backlog/external-host decisions.

## 6. Checklist Results

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story identified | Done | Epic D retrospective plus D5/D7 deferred review findings. |
| 1.2 Core problem defined | Done | Deferred generator diagnostics/error semantics need one story. |
| 1.3 Evidence gathered | Done | `deferred-work.md`, D5, D7, retrospective, PRD, epics, architecture. |
| 2.1 Current epic impact | Done | Epic D remains done; work moves to Epic 7 / FR35 backlog tracking. |
| 2.2 Epic-level changes | Done | No new epic required; add focused story artifact. |
| 2.3 Future epics reviewed | Done | Avoids mixing generator hardening into SEC/COR/OPS/TEST stories. |
| 2.4 Invalidates future epics | N/A | No planned epic is invalidated. |
| 2.5 Priority/order | Done | Story is ready-for-dev but can remain unscheduled backlog work. |
| 3.1 PRD conflicts | Done | No conflict; PRD already marks REST generator hardening as backlog. |
| 3.2 Architecture conflicts | Done | No conflict; AD-3/AD-4 already bind external generated REST. |
| 3.3 UX conflicts | N/A | No UI work. |
| 3.4 Other artifacts | Done | Sprint status updated. |
| 4.1 Direct adjustment | Viable | Chosen. |
| 4.2 Rollback | Not viable | No completed work needs rollback. |
| 4.3 MVP review | Not viable | Backlog artifact only unless separately scheduled. |
| 4.4 Path selected | Done | Direct adjustment. |
| 5.1 Issue summary | Done | See section 1. |
| 5.2 Impact/artifacts | Done | See section 2. |
| 5.3 Path rationale | Done | See section 3. |
| 5.4 MVP impact/action plan | Done | No MVP scope change. |
| 5.5 Handoff plan | Done | Developer agent when scheduled. |
| 6.1 Checklist completion | Done | All applicable items addressed. |
| 6.2 Proposal accuracy | Done | Proposal matches current PRD/architecture. |
| 6.3 User approval | Done | User explicitly requested creation of the story. |
| 6.4 Sprint status update | Done | New story key added; action item closed. |
| 6.5 Next steps | Done | Run dev-story on the new story when ready. |

