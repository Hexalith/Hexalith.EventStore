---
title: Sprint Change Proposal - Generated API Smoke Preflight Re-Home
date: 2026-07-07
project: eventstore
status: approved
approved_by: Administrator
approved_on: 2026-07-07
change_scope: Minor
supersedes: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-generated-api-smoke-preflight.md
source:
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad-output/implementation-artifacts/epic-2-retro-2026-07-07.md
  - _bmad-output/implementation-artifacts/epic-1-retro-2026-07-07.md
  - _bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-generated-api-smoke-preflight.md
---

# Sprint Change Proposal: Generated API Smoke Preflight Re-Home

## 1. Issue Summary

**Trigger:** "Add a local DAPR/Aspire smoke preflight for generated API proofs."

This is **not new scope**. It is a planning-drift correction. The same action item has
been committed and left `open` across three retrospectives:

- Epic D retrospective (2026-07-05), action item 4.
- Epic 1 retrospective (2026-07-07): "Close the existing generated API DAPR/Aspire smoke
  preflight action before accepting Sample or Tenants generated API live proof blockers."
- Epic 2 retrospective (2026-07-07), action item 4: "Close the reusable DAPR/Aspire
  generated API smoke preflight action," with an explicit completion gate.

An **approved** proposal (`sprint-change-proposal-2026-07-05-generated-api-smoke-preflight.md`)
and a `ready-for-dev` story already existed for this work. However, both were anchored to
the **`TEST-1 "Test And CI Recovery"` epic**, which no longer exists after the sprint plan
was regenerated into the Epic 1-7 structure. Consequences discovered:

1. Neither `epic-TEST-1-...` nor `TEST-1-1-...` appears in the current
   `sprint-status.yaml` `development_status`. **The story file was a dangling artifact,
   not tracked in any active sprint** — which is why the action item kept slipping.
2. The story used **Epic D-era endpoint names** (`sample-api`, `tenants-api`) rather than
   the hosts actually delivered by Epic 2: `Hexalith.EventStore.Sample.Api` (Story 2.3)
   and `Hexalith.Tenants.Api` (Story 2.4).
3. The Epic 2 retrospective explicitly directs where this belongs:
   *"Carry the generated API smoke preflight into the live-sidecar re-tiering work"* —
   i.e., alongside **Epic 3 Story 3.1**.

**Evidence that the underlying work is valid and unchanged:** every code target the
existing story names still exists on disk — `src/Hexalith.EventStore.AppHost/PrerequisiteValidator.cs`,
`src/Hexalith.EventStore.Testing.Integration/DaprDiagnostics.cs`,
`tests/Hexalith.EventStore.Testing.Integration.Tests/DaprTestPrerequisiteDiagnosticsTests.cs`,
`tests/Hexalith.EventStore.AppHost.Tests`, and `samples/Hexalith.EventStore.Sample.Api`.
So the fix is a **re-home + reconcile**, not a re-write of the technical plan.

## 2. Impact Analysis

### Epic Impact

- **Epic 3 (Release And Repository Reliability):** gains one story — **Story 3.8**,
  companion to Story 3.1 — pulled forward out of Epic 3 sequence because it is an
  evidence gate for accepting generated-API live proofs. Epic 3 status stays `backlog`
  by design (only Story 3.8 is `ready-for-dev`); this is a documented, deliberate
  exception to the "first story created -> epic in-progress" auto-transition rule.
- **Epic 2 (External Integration Surfaces):** no completed proof is reopened. Stories 2.3
  and 2.4 remain done; Story 3.8 validates them at live topology level.
- **Epic 7 (Operator Trust / Story 7.4 Integration Test Recovery):** touches evidence
  quality but is not the right home — 7.4 is about CI integration-test refactoring, not
  a local live-topology developer diagnostic. No change to 7.4.
- **Defunct `TEST-1` epic:** its only story is re-homed; nothing else to migrate.

### Story Impact

- **New:** Story 3.8 - Generated API DAPR/Aspire Smoke Preflight (`ready-for-dev`).
- **Rewritten/re-keyed:** `TEST-1-1-generated-api-smoke-preflight.md` ->
  `3-8-generated-api-dapr-aspire-smoke-preflight.md` (content preserved, references and
  host/endpoint names reconciled, completion gate added).
- **Not reopened:** Stories 2.1-2.5, D5-D7 historical records, Story 3.1 (only a
  one-line companion cross-reference added).

### Artifact Conflicts

- **PRD:** no change. FR34 and NFR16 already require meaningful integration evidence and
  persisted state, not status-only smoke.
- **Architecture:** no change. AD-9 binds AppHost/DAPR topology; AD-12 requires persisted
  evidence for high-risk verification. Editing them would duplicate accepted language.
- **UX:** not applicable — local developer/test preflight, no UI.
- **Sprint status:** Story 3.8 added; three duplicate action items cross-referenced to
  the single tracked story.
- **Docs:** `docs/brownfield/development-guide.md` and the actor-placement troubleshooting
  guide are updated by the implementer *after* the preflight exists (Story 3.8 Task 6).

### Technical Impact

Implementation touches (per Story 3.8): `scripts/` for the local entry point; optional
reusable C# diagnostics in `Hexalith.EventStore.Testing.Integration`; focused tests in
`Hexalith.EventStore.Testing.Integration.Tests`; `AppHost.Tests` only if resource-name
assumptions are encoded. No submodule edits required (Tenants checks are optional and
conditional on the submodule/API host being present).

## 3. Recommended Approach

**Path: Direct Adjustment** (re-home + reconcile within the existing epic structure).

- **Rollback** — not viable; no completed proof needs reverting.
- **MVP review** — not viable; this is validation hygiene inside existing FR34/NFR16, no
  scope change.

Effort: **Low-Medium** (the technical story is already written; this proposal is the
planning reconciliation). Risk: **Low** while the preflight stays read-only by default;
Medium only if it auto-starts placement/scheduler/Aspire, which Story 3.8 gates behind an
explicit flag.

## 4. Detailed Change Proposals (applied)

All edits below were approved incrementally and have been applied to the working tree.

1. **Story re-home & re-key.** `TEST-1-1-...md` removed; new
   `3-8-generated-api-dapr-aspire-smoke-preflight.md` created with: re-keyed frontmatter
   (`source_epic` = Epic 3 companion to 3.1; `source_action` = Epic 2 retro item 4);
   title `# Story 3.8`; Story Context re-anchored Epic D -> Epic 2 (Stories 2.3/2.4);
   endpoint/host names reconciled to `Hexalith.EventStore.Sample.Api` /
   `Hexalith.Tenants.Api` with `aspire describe`-driven resource discovery; new **AC10**
   completion gate (Epic 2 retro item 4). ACs 1-9, Tasks, Dev Notes preserved.

2. **`sprint-status.yaml` tracking.** Added
   `3-8-generated-api-dapr-aspire-smoke-preflight: ready-for-dev` under Epic 3 with a
   "pulled forward" comment; `epic-3` kept `backlog` (deliberate exception).

3. **Action-item consolidation.** The three duplicate `open` preflight action items
   (Epic D item 4, Epic 1 gate, Epic 2 item 4) now each cross-reference "-> Story 3.8";
   the Epic 2 item retains the canonical completion gate. All stay `open` until AC10 is
   met, then flip to `done` together.

4. **Prior proposal superseded.** `sprint-change-proposal-2026-07-05-...` marked
   `status: superseded` with `superseded_by`/`superseded_reason` and a body banner.

5. **`epics.md`.** Story 3.8 added to Epic 3 (Given/When/Then ACs, house style); a
   one-line companion cross-reference added under Story 3.1.

6. **`deferred-work.md`.** The 2026-07-05 preflight entry annotated "-> tracked as
   Story 3.8".

**PRD / Architecture / UX:** no change (see Section 2).

## 5. Implementation Handoff

**Change scope: Minor.** Route to: **Developer agent (Amelia)**.

- Implement Story 3.8 (`_bmad-output/implementation-artifacts/3-8-generated-api-dapr-aspire-smoke-preflight.md`).
- Keep the preflight read-only by default; gate any control-plane/Aspire start behind an
  explicit flag.
- Separate environment-blocker status from generated-API product status.
- Prefer `EnableKeycloak=false` + HTTP endpoints in local/VM mode; discover resource names
  from `aspire describe`.
- Tenants checks optional and conditional on the submodule/API host.
- **Completion gate (AC10):** one clean live-topology run reporting generated API
  endpoints, DAPR sidecar readiness, placement/scheduler readiness, and support-safe
  failure details. On success, flip the three carried action items to `done`.

**Success criteria:** Story 3.8 tracked and implementable; the preflight distinguishes
infrastructure blockers from product defects with support-safe, persisted-evidence-aware
output; the long-open action item finally closes against a concrete gate.

## 6. Checklist Results

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering context | Done | Epic 2 retro item 4 (today); open since Epic D retro 2026-07-05. |
| 1.2 Core problem | Done | Planning drift: approved plan/story orphaned under defunct TEST-1 epic. |
| 1.3 Evidence | Done | sprint-status has no TEST-1 keys; 3 duplicate open items; Epic 2 retro "Preparation For Epic 3"; all code targets verified present. |
| 2.1 Current epic | Done | Epic 3 gains Story 3.8 (companion to 3.1); nothing invalidated. |
| 2.2 Epic-level change | Done | One story added; epic-3 stays backlog (documented exception). |
| 2.3 Remaining epics | Done | Epic 2 not reopened; Epic 7 §7.4 unchanged. |
| 2.4 Invalidates future epics | N/A | None. |
| 2.5 Priority/order | Done | Pulled forward as an evidence gate before more generated-API live proofs. |
| 3.1 PRD conflicts | Done | None; FR34/NFR16 already cover evidence quality. |
| 3.2 Architecture conflicts | Done | None; AD-9/AD-12 already bind topology and evidence. |
| 3.3 UX conflicts | N/A | No UI. |
| 3.4 Other artifacts | Done | sprint-status, epics.md, deferred-work, superseded prior proposal. |
| 4.1 Direct adjustment | Viable | Chosen. |
| 4.2 Rollback | Not viable | Nothing to revert. |
| 4.3 MVP review | Not viable | No scope change. |
| 4.4 Path selected | Done | Direct adjustment via re-home + reconcile. |
| 5.1-5.5 Proposal components | Done | Sections 1-5. |
| 6.1 Checklist completion | Done | All applicable items addressed. |
| 6.2 Proposal accuracy | Done | Matches current sprint-status, epics, retros, and verified code state. |
| 6.3 User approval | Done | Options and each edit approved incrementally by Administrator. |
| 6.4 Sprint status update | Done | Story 3.8 added; action items consolidated. |
| 6.5 Next steps | Done | Developer implements Story 3.8 to AC10 gate. |

### Story Rewrite Gate

Satisfied. The current structure supersedes an approved proposal's story, so explicit
old->new rewrites are included for the story file (re-key + reconcile), `sprint-status.yaml`
(story key + action items), the prior proposal (superseded), and `epics.md` (Story 3.8 +
Story 3.1 cross-reference). No affected story was left unaddressed.
