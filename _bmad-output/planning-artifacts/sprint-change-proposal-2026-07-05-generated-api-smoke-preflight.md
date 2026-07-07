---
title: Sprint Change Proposal - Generated API Smoke Preflight
date: 2026-07-05
project: eventstore
status: superseded
approved_by: Administrator
approved_on: 2026-07-05
superseded_by: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-generated-api-smoke-preflight-rehome.md
superseded_on: 2026-07-07
superseded_reason: >
  The TEST-1 "Test And CI Recovery" epic was absorbed into the Epic 1-7 structure.
  This proposal's story (TEST-1.1) is re-homed as Epic 3 Story 3.8 (companion to
  Story 3.1) and reconciled to the delivered Epic 2 hosts
  (Hexalith.EventStore.Sample.Api, Hexalith.Tenants.Api).
source:
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture.md
  - docs/brownfield/development-guide.md
  - docs/brownfield/integration-architecture.md
  - docs/guides/troubleshooting-dapr-actor-placement.md
  - tests/Hexalith.EventStore.Testing.Integration.Tests/DaprTestPrerequisiteDiagnosticsTests.cs
---

# Sprint Change Proposal: Generated API Smoke Preflight

> **SUPERSEDED 2026-07-07** — see `sprint-change-proposal-2026-07-07-generated-api-smoke-preflight-rehome.md`.
> Story re-homed from TEST-1.1 to Epic 3 Story 3.8 (companion to Story 3.1) and reconciled to
> the delivered Epic 2 hosts (`Hexalith.EventStore.Sample.Api`, `Hexalith.Tenants.Api`). The
> analysis below remains valid; only the epic/story home and endpoint names changed.

## 1. Issue Summary

The Epic D retrospective left an open Developer action item:

> Add a local DAPR/Aspire smoke preflight for generated API proofs.

The trigger is not a new generated REST feature. It is an evidence-quality gap found while closing Epic D. Generated API proof stories need to distinguish local infrastructure readiness failures from product defects before accepting a live-smoke blocker.

Evidence:

- `deferred-work.md` records that generated API proof stories need a reusable DAPR/Aspire smoke preflight that reports placement/scheduler availability, generated API endpoint URLs, DAPR sidecar state, and support-safe failure details.
- The Epic D retrospective records that missing local DAPR services, source/package mode differences, and intentionally uninitialized nested submodules can look like product defects unless tests explain the environment state.
- D5 eventually produced strong Sample API runtime evidence: generated `sample-api` endpoint, `200` plus ETag, `304`, and Redis persisted end-state after a Counter increment.
- D6 recorded a blocked Aspire smoke because local DAPR placement and scheduler binaries were absent; the story had to list manual commands instead of running a reusable preflight.
- D7 recorded successful generated API integration tests, but also an intermediate `tenants-api` endpoint wait-list issue and DAPR actor-placement collision diagnosis.

## 2. Impact Analysis

### Epic Impact

**Epic 2 - External Integration Surfaces:** no completed Epic D proof is reopened. Future generated API proof work should use the preflight before recording a local runtime blocker.

**TEST-1 - Test And CI Recovery:** impacted. This is the best home for the new story because the work is about trustworthy local runtime evidence, support-safe diagnostics, and separating smoke prerequisites from product behavior.

**Epic 7 - Operator Trust, Admin Honesty, And Future Capabilities:** lightly impacted through FR34/NFR16 evidence rules. No additional Epic 7 backlog story is needed.

### Story Impact

Add one implementation story:

- `TEST-1.1: Generated API DAPR/Aspire Smoke Preflight`

Related existing stories are not reopened:

- D5, D6, and D7 remain historical proof records.
- `7-5-rest-generator-hardening` explicitly excludes this preflight and remains focused on generator diagnostics and generated external API error semantics.
- Future generated API, integration, or topology stories can reference `TEST-1.1` as the standard local preflight.

### Artifact Conflicts

**PRD:** no update required. FR34 and NFR16 already require meaningful integration evidence and persisted state/read-model evidence rather than status-only smoke.

**Architecture:** no update required. AD-12 already says high-risk verification requires persisted evidence, and AD-9 already binds AppHost and DAPR YAML topology.

**UX:** not applicable. This is a local developer/test preflight, not UI behavior.

**Docs:** implementation should update local development or troubleshooting guidance only after the tool exists.

**Sprint tracking:** add a ready-for-dev story under `TEST-1`. Keep the Epic D retrospective action item open until the preflight is implemented and verified.

### Technical Impact

Likely implementation touches:

- `scripts/` for a local preflight entry point.
- `src/Hexalith.EventStore.Testing.Integration/` for reusable DAPR/Aspire diagnostic helpers if shell-only logic would duplicate existing support-safe diagnostics.
- `tests/Hexalith.EventStore.Testing.Integration.Tests/` for support-safe output, prerequisite classification, and redaction tests.
- `tests/Hexalith.EventStore.AppHost.Tests/` if endpoint/resource name assumptions are encoded.
- Documentation in `docs/brownfield/development-guide.md` or a focused troubleshooting guide after implementation.

No submodule edits are required. Tenants generated API checks should be optional and only run when the `references/Hexalith.Tenants` submodule and its API host are present.

## 3. Recommended Approach

Recommended path: **Direct Adjustment**.

Create one ready-for-dev story under `TEST-1`. The implementation can be a small local helper plus focused tests. This is minor planning scope because the desired behavior is already identified by the retrospective and deferred-work entry.

Rollback is not useful because no completed generated REST proof needs to be reverted. MVP review is not required because the preflight improves validation quality without changing product scope.

Effort estimate: **Low to Medium**.

Risk level: **Low** if the preflight stays diagnostic and avoids changing AppHost topology. Risk becomes Medium only if implementation starts/stops Aspire resources automatically, so the story should require explicit flags for any action beyond read-only checks.

## 4. Detailed Change Proposals

### 4.1 New Story

Story: `TEST-1.1: Generated API DAPR/Aspire Smoke Preflight`
Section: New story under `TEST-1 - Test And CI Recovery`

OLD:

No dedicated implementation story exists. The work exists only as:

```yaml
- epic: D
  action: "Add a local DAPR/Aspire smoke preflight for generated API proofs."
  owner: "Amelia (Developer)"
  status: open
```

NEW:

Create `_bmad-output/implementation-artifacts/TEST-1-1-generated-api-smoke-preflight.md` with:

- ready-for-dev status,
- Epic D retrospective context,
- acceptance criteria for environment checks, Aspire resource discovery, DAPR sidecar metadata, generated API endpoint reporting, optional Sample API smoke, support-safe output, and tests,
- verification commands for focused script/helper tests and Release build.

Rationale: gives the Developer agent one concrete implementation target and prevents future generated API proof stories from inventing one-off blocker notes.

### 4.2 Sprint Status

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

OLD:

```yaml
epic-TEST-1-test-ci-recovery: backlog
```

NEW:

```yaml
epic-TEST-1-test-ci-recovery: in-progress

# Added 2026-07-05 from Epic D retrospective follow-through.
TEST-1-1-generated-api-smoke-preflight: ready-for-dev
```

Rationale: this is the first concrete `TEST-1` story file, so the epic moves from backlog to in-progress according to the sprint-status transition rules.

### 4.3 Action Item Status

OLD:

```yaml
- epic: D
  action: "Add a local DAPR/Aspire smoke preflight for generated API proofs."
  owner: "Amelia (Developer)"
  status: open
```

NEW:

No status change yet.

Rationale: creating the story routes the work, but the action item's success criteria are not met until the preflight tool reports placement/scheduler availability, DAPR sidecar endpoints, generated API endpoint URLs, and support-safe failure details.

### 4.4 PRD / Architecture / UX

OLD:

PRD and architecture already require persisted evidence and topology-aligned verification. UX is not affected.

NEW:

No immediate artifact update.

Rationale: changing these artifacts would duplicate accepted AD-9, AD-12, FR34, NFR16, and NFR17 language. Implementation docs should be updated after the preflight exists.

## 5. Implementation Handoff

Change scope: **Minor**.

Route to: **Developer agent**.

Developer responsibilities:

- Implement the local generated API smoke preflight as a support-safe diagnostic helper, preferably under `scripts/` with reusable logic in `Hexalith.EventStore.Testing.Integration` only if needed.
- Keep the tool read-only by default. Any attempt to start placement, scheduler, or Aspire must require an explicit flag.
- Report environment status separately from product smoke status.
- Prefer `EnableKeycloak=false` for local proof mode and HTTP endpoints in cloud/VM environments.
- Ensure generated API endpoint checks use `sample-api`; make `tenants-api` optional when the Tenants submodule and host are present.
- Run focused tests and record exact blockers if Docker, DAPR, Aspire, or local ports are unavailable.

Success criteria:

- A ready-for-dev story exists under `TEST-1`.
- Sprint status tracks the story and keeps the retrospective action item open until implementation.
- The future Developer story defines clear checks for placement/scheduler, DAPR sidecars, Aspire endpoints, generated API URLs, optional smoke behavior, persisted/read-model evidence, and support-safe diagnostics.

## 6. Checklist Results

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story identified | Done | Epic D retrospective action item 4, with D5/D6/D7 evidence. |
| 1.2 Core problem defined | Done | Local DAPR/Aspire blockers can be mistaken for product defects without a standard preflight. |
| 1.3 Evidence gathered | Done | `deferred-work.md`, Epic D retrospective, D5/D6/D7 story records, docs, and existing prerequisite diagnostics tests. |
| 2.1 Current epic impact | Done | Epic D remains done; work moves to TEST-1. |
| 2.2 Epic-level changes | Done | TEST-1 transitions to in-progress with one ready-for-dev story. |
| 2.3 Remaining epics reviewed | Done | Epic 2 and Epic 7 are referenced but not reopened. |
| 2.4 Invalidates future epics | N/A | No planned epic is invalidated. |
| 2.5 Priority/order | Done | Implement before accepting future generated API live-smoke blockers. |
| 3.1 PRD conflicts | Done | No conflict; existing FR34/NFR16 cover evidence quality. |
| 3.2 Architecture conflicts | Done | No conflict; AD-9/AD-12 already bind topology and evidence rules. |
| 3.3 UX conflicts | N/A | No UI or UX work. |
| 3.4 Other artifacts | Done | Sprint status updated; deferred-work remains source evidence. |
| 4.1 Direct adjustment | Viable | Chosen. |
| 4.2 Rollback | Not viable | No completed proof needs rollback. |
| 4.3 MVP review | Not viable | Scope is validation hygiene inside existing requirements. |
| 4.4 Path selected | Done | Direct adjustment via new TEST-1 story. |
| 5.1 Issue summary | Done | See section 1. |
| 5.2 Impact/artifacts | Done | See section 2. |
| 5.3 Path rationale | Done | See section 3. |
| 5.4 MVP impact/action plan | Done | No MVP scope change; implementation routed to Developer. |
| 5.5 Handoff plan | Done | Developer story with clear success criteria. |
| 6.1 Checklist completion | Done | All applicable items addressed. |
| 6.2 Proposal accuracy | Done | Proposal matches current PRD, architecture, and retrospective evidence. |
| 6.3 User approval | Done | User explicitly requested the correct-course addition. |
| 6.4 Sprint status update | Done | New story key added; action item remains open until implementation. |
| 6.5 Next steps | Done | Run Developer story `TEST-1-1-generated-api-smoke-preflight`. |
