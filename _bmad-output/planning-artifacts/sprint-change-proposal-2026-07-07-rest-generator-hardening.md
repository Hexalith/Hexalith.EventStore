---
title: Sprint Change Proposal - REST Generator Hardening (Epic 2 Scoping)
date: 2026-07-07
project: eventstore
status: approved-for-implementation
scope: minor
mode: batch
change_trigger: "Epic 2 retrospective action item 2 (open) — scope REST generator hardening from the Epic 2 deferred entries into a backlog item or implementation story."
source:
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/epic-2-retro-2026-07-07.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad-output/implementation-artifacts/spec-2-1-rest-contract-seam-for-command-and-query-messages.md
  - _bmad-output/implementation-artifacts/spec-2-2-rest-api-generator-discovery-and-controller-emission.md
  - _bmad-output/implementation-artifacts/spec-2-3-sample-external-api-host-proof.md
  - _bmad-output/implementation-artifacts/7-5-rest-generator-hardening.md
  - _bmad-output/planning-artifacts/backlog/rest-generator-hardening.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
---

# Sprint Change Proposal: REST Generator Hardening (Epic 2 Scoping)

## 1. Issue Summary

**Problem.** Epic 2 (External Integration Surfaces, stories 2.1–2.5) proved the generated REST controller surface and, in the process, surfaced a fresh batch of generator/generated-controller hardening items. Those items live only as bullets in `deferred-work.md` — they have no named home, and the Epic 2 retrospective raised action item 2 to fix that: *"Scope REST generator hardening from the Epic 2 deferred entries into a backlog item or implementation story."* (owner: Winston; status: open). Its completion gate: *request-size limits, status `Location`, safe query argument problem text, invalid binding diagnostics, invalid tenant-source handling, and command rejection extension policy have **named target artifacts**.*

**When/how discovered.** Requested via `bmad-correct-course` on 2026-07-07 to close the open Epic 2 retrospective follow-through.

**Key discovery — two waves, only the first is done.** REST generator hardening has arrived in two distinct waves:

- **First wave** (Epic D / D5–D7 review findings): unsupported contract shapes, duplicate JSON-names, invalid `RestQueryBinding` diagnostics, route-template constraints, case-insensitive matching, referenced incrementality, generated error-semantics. **Implemented and `done`** in `7-5-rest-generator-hardening.md` (HESREST006/010/012 + error-semantics tests).
- **Second wave** (Epic 2 stories 2.1–2.5, executed 2026-07-07): request-size limits, command-status `Location`, safe `ArgumentException` text, command-rejection extension forwarding, invalid tenant-source, `RestQueryBinding` `None`+value / padded values. **Not tracked with named artifacts anywhere.**

The backlog artifact `backlog/rest-generator-hardening.md` already existed (created 2026-07-05) but predated Epic 2 and captured only the first wave at summary level. The real gap is the missing, per-item second wave.

**Evidence.** `deferred-work.md` (Epic 2 spec-2-1/2-2/2-3 and D7 review sections), `epic-2-retro-2026-07-07.md` action items 2 and 3, and the `done` status of `7-5-rest-generator-hardening.md`. All six second-wave target source and test files were verified to exist in `src/Hexalith.EventStore.RestApi.Generators/`, `src/Hexalith.EventStore.Contracts/Rest/`, and `tests/Hexalith.EventStore.RestApi.Generators.Tests/`.

## 2. Impact Analysis

**Epic impact.** None reopened. Epic 2 stays `done`. The scoped items are additive hardening tracked under FR35 / Epic 7 Story 7.5's anticipated backlog artifact.

**Story impact.**
- No completed story reopened. `7-5-rest-generator-hardening.md` (done, first wave) is referenced, not modified.
- Epic 7 Story 7.5 (Track Future Capability Backlog, still `backlog`) is *strengthened*: its AC requires `backlog/rest-generator-hardening.md` to record deferred-work items and "link the dedicated hardening story or backlog item that pulls from `deferred-work.md`." This proposal fills that artifact with named second-wave targets — satisfying, not contradicting, that AC.

**Artifact conflicts.** None.
- **PRD:** FR35 already tracks REST generator hardening as backlog. No change.
- **Architecture:** AD-3/AD-4 (generated REST in external API hosts, gateway delegation) and AD-15 (query-response provenance) are unchanged and are referenced as constraints in the backlog artifact. No change.
- **UX:** Not affected (generator/test hardening, no module UI). No change.

**Technical impact (future implementation only).** Touches `src/Hexalith.EventStore.RestApi.Generators/` (emitter, attribute-value reader, route lookup), `src/Hexalith.EventStore.Contracts/Rest/RestQueryBindingAttribute.cs`, and `tests/Hexalith.EventStore.RestApi.Generators.Tests/` + `tests/Hexalith.EventStore.Contracts.Tests/Rest/`. Must preserve gateway delegation and the external-API-host boundary. No implementation is authorized by this proposal.

**Cross-item coupling.** The command-status `Location` item (S2) also appears as its own open action item 3 (Winston). This proposal names its target artifact but explicitly defers the *policy decision* to action item 3, so the two do not conflict.

## 3. Recommended Approach

**Direct Adjustment (backlog item).** Chosen by the project lead.

Extend the existing `backlog/rest-generator-hardening.md` with a "Second Wave" section giving each Epic 2 item a named source + test artifact and an intended policy, plus a completion-gate mapping. Close Epic 2 retro action item 2 in `sprint-status.yaml`.

- **Effort:** Low (two planning-doc edits).
- **Risk:** Low. No code, no test, no completed story affected. Fully reversible.
- **Why not an implementation story now:** several items require a prior design decision (command-status `Location` depends on action item 3; command-rejection extension forwarding needs an allowlist decision), and Epic 2 is closed. The retro gate requires *named target artifacts*, not scheduled implementation. Backlog is the artifact FR35 / Story 7.5 already anticipates.
- **Rollback / MVP review:** not applicable — additive backlog scoping, no MVP scope change.

## 4. Detailed Change Proposals

### 4.1 Backlog artifact — `_bmad-output/planning-artifacts/backlog/rest-generator-hardening.md`

**OLD:** Single-scope draft (created 2026-07-05) describing first-wave hardening at summary level; frontmatter cited only `deferred-work.md` and the Epic D retro; no per-item target artifacts; no Epic 2 items.

**NEW (applied):**
- Frontmatter: added `updated: 2026-07-07`, `related_action_items` (Epic 2 retro items 2 and 3), and Epic 2 retro + spec-2-1/2-2/2-3 to `source_evidence`.
- Added **"First Wave — Resolved (Epic D / D5–D7)"** section pointing at `7-5-rest-generator-hardening.md` (HESREST006/010/012 + error-semantics) with an explicit "do not re-scope" note.
- Added **"Second Wave — Epic 2 (Stories 2.1–2.5) Scoped Items"** table with a named target source + test artifact and intended policy for each item (S1–S6), plus a completion-gate mapping.
- Extended Non-Goals (no re-implementing first wave; no deciding `Location` here), Dependencies (action item 3 gates S2; AD-15/Story 4.7 governs freshness), Risks, and Validation Expectations.

**Second-wave named-artifact table (as written):**

| # | Item | Source spec | Target source | Target test | Policy |
| --- | --- | --- | --- | --- | --- |
| S1 | No canonical `[RequestSizeLimit(1_048_576)]` on generated command endpoints | spec-2-2 | `RestApiControllerEmitter.cs` (`AppendCommandAction`) | `RestApiControllerGenerationTests.cs` | Emit the 1 MiB limit. |
| S2 | Command `Location` hard-codes relative `/api/v1/commands/status/{id}` | spec-2-2/2-3 | `RestApiControllerEmitter.cs` (`AppendCommandAction`) | `RestApiControllerGenerationTests.cs`; Sample runtime test | Policy owned by action item 3. |
| S3 | Query action maps raw `ArgumentException.Message` into `ProblemDetails` | spec-2-2 | `RestApiControllerEmitter.cs` (`AppendQueryAction`) | `RestApiGeneratedControllerErrorSemanticsTests.cs` | Fixed safe message / shared sanitizer. |
| S4 | Generated command problem mapping drops `rejectionType`/`correctiveAction` | spec-2-2 | `RestApiControllerEmitter.cs` (`AppendCommandAction`) | `RestApiGeneratedControllerErrorSemanticsTests.cs` | Support-safe extension allowlist. |
| S5 | `{tenantId}` unvalidated under `System`; out-of-range `RestTenantSource` → numeric text | spec-2-1, D7 | `RestApiControllerEmitter.cs` (`IsTenantParameter`, `ResolveTenant`); `RoslynAttributeValueReader.cs` (`GetEnumName`) | `RestApiDiagnosticTests.cs`; `RestApiControllerGenerationTests.cs` | Validate route tenant vs body when source ≠ Route; diagnose out-of-range source. |
| S6 | `RestQueryBinding` `EntitySource=None`+value / padded values the generator rejects | spec-2-1 | `RestQueryBindingAttribute.cs`; `RestApiControllerEmitter.cs` (binding route lookup) | `Contracts.Tests/Rest/RestQueryBindingAttributeTests.cs`; `RestApiDiagnosticTests.cs` | Reconcile runtime validation with generator fail-closed. |

**Rationale:** gives a future implementation story one place with real anchors, preventing generator hardening from scattering into unrelated security/correctness/UI stories.

### 4.2 Sprint status — `_bmad-output/implementation-artifacts/sprint-status.yaml`

**OLD:**
```yaml
- epic: 2
  action: "Scope REST generator hardening from the Epic 2 deferred entries into a backlog item or implementation story."
  owner: "Winston (System Architect)"
  status: open
```

**NEW (applied):** `status: done` with a `note` naming `backlog/rest-generator-hardening.md`, the S1–S6 gate mapping, the action-item-3 deferral for S2, and confirmation that no completed story was reopened.

**Rationale:** closes the retrospective follow-through and makes the scoping visible to sprint tooling.

### 4.3 PRD / Architecture / UX

**OLD / NEW:** No change. Rationale: FR35 already treats REST generator hardening as backlog; AD-3/AD-4/AD-15 already bind generated REST placement, gateway delegation, and query provenance. Editing them would duplicate accepted decisions.

## 5. Story Rewrite Gate (Correct-Course append step)

**Result: not triggered.** This change is additive backlog scoping. It reopens no completed story, and it supersedes no active story's acceptance criteria, tasks, Dev Notes, project-structure notes, or design assumptions. Epic 7 Story 7.5's AC is satisfied (not rewritten) by populating its anticipated backlog artifact. No old-to-new story rewrites are required, so no rewrite section is owed and approval/handoff is not blocked.

## 6. Implementation Handoff

**Change scope: Minor.** The deliverable is the two applied planning-doc edits; both are done.

- **Route to:** Product Owner / Developer — for *scheduling only*, when the team decides to implement the second wave. No implementation is authorized now.
- **Prerequisite before dev of S2:** Epic 2 retro action item 3 (command-status `Location` policy) must resolve first.
- **Success criteria (all met):**
  1. Every completion-gate item (request-size limits, status `Location`, safe query argument text, invalid binding diagnostics, invalid tenant-source, command-rejection extension policy) has a named target artifact — S1–S6 in the backlog table.
  2. `sprint-status.yaml` Epic 2 action item 2 is `done` with a traceable note.
  3. No completed Epic 2 or Epic D/7.5 story reopened.
  4. PRD / architecture / UX unchanged and consistent.

## 7. Checklist Results

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering issue identified | Done | Epic 2 retro action item 2 (open). |
| 1.2 Core problem defined | Done | Second-wave Epic 2 hardening items have no named home. |
| 1.3 Evidence gathered | Done | deferred-work, Epic 2 retro, specs 2-1/2-2/2-3, done 7.5 story; target files verified to exist. |
| 2.1 Current epic impact | Done | Epic 2 stays done; work tracked under FR35 backlog. |
| 2.2 Epic-level changes | Done | No new epic; extend existing backlog artifact. |
| 2.3 Future epics reviewed | Done | Strengthens Epic 7 Story 7.5; avoids scatter into SEC/COR/UI. |
| 2.4 Invalidates future epics | N/A | None invalidated. |
| 2.5 Priority/order | Done | Backlog; S2 gated by action item 3. |
| 3.1 PRD conflicts | N/A | FR35 already backlog-tracks generator hardening. |
| 3.2 Architecture conflicts | N/A | AD-3/AD-4/AD-15 unchanged, referenced as constraints. |
| 3.3 UX conflicts | N/A | No UI work. |
| 3.4 Other artifacts | Done | Backlog artifact + sprint status updated. |
| 4.1 Direct adjustment | Viable | Chosen (backlog item). |
| 4.2 Rollback | N/A | Nothing to roll back. |
| 4.3 MVP review | N/A | No MVP scope change. |
| 4.4 Path selected | Done | Direct adjustment / backlog item. |
| 5.x Proposal sections | Done | Sections 1–6 complete. |
| Story Rewrite Gate | Done | Not triggered — additive scoping (Section 5). |
| 6.4 Sprint status update | Done | Action item 2 → done. |
| 6.5 Next steps | Done | Schedule second-wave implementation story when S2 policy lands. |

## 8. Concurrency Note

A parallel automated dev-loop was closing sibling Epic 2 action items during this session (action item 1 follow-up-review disposition and action item 6 SignalR leave-validation). This proposal's edits target only action item 2's block and the pre-existing backlog artifact; the concurrent action-item-1 note independently references "REST-generator-hardening" as the tracking home, corroborating this scoping. Commit with a fresh `git` check to avoid absorbing the parallel loop's uncommitted edits.
