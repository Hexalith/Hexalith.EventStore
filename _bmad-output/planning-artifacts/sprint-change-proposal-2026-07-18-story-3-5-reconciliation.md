---
title: Sprint Change Proposal - Reconcile Story 3.5 Activation And Scope
status: final
created: 2026-07-18
project: eventstore
mode: batch
scope_classification: moderate
approval: approved
approved_by: Administrator
approved_on: 2026-07-18
finalized_on: 2026-07-18
trigger: Story 3.5 development preflight exposed unresolved authority, scope, and sequencing gates
target_stories:
  - "3.3"
  - "3.5"
---

# Sprint Change Proposal: Reconcile Story 3.5 Activation And Scope

## 1. Issue Summary

The Story 3.5 development preflight found that implementation cannot start safely from the current planning state:

1. Story 3.5 AC1 says an **unset** Debug build selects available source projects, while PRD FR21 and adopted architecture decision AD-11 require source references to be **explicitly enabled** with `UseHexalithProjectReferences=true`.
2. Story 3.5 AC6 is ecosystem-wide. An earlier implementation handoff limited migration work to Hexalith.Builds and EventStore even though checked-out Commons, Memories, PolymorphicSerializations, FrontComposer, and Tenants repositories can retain local package-version declarations. Administrator's later Story 3.1 code-review decision supersedes that partial completion boundary: Story 3.5 remains responsible for ecosystem-wide FR21 closure while each repository retains its own maintainer authority.
3. Story 3.3 is the references-layout prerequisite for Story 3.5, but it remains `ready-for-dev` with its verification tasks unchecked.
4. Story 3.5 AC4 cannot close while Story 1.20 remains `in-progress` and Story 2.12 remains `backlog`; the Tenants graph still mixes a source Gateway edge with package-mode EventStore dependencies.

The Administrator approved the following disposition in principle on 2026-07-18 and explicitly approved this complete proposal after batch review:

- preserve FR21/AD-11 explicit source opt-in and amend Story 3.5 AC1;
- retain Story 3.5's ecosystem-wide AC6 completion responsibility while preserving per-repository maintainer approval, commit, rollback, and validation boundaries; and
- complete and review Story 3.3 before Story 3.5 implementation resumes.

This proposal makes those decisions durable without weakening the existing AC4 completion gate. It does not authorize Tenants dependency-identity changes, broad package upgrades, submodule updates, commits, or pushes.

## 2. Impact Analysis

### Epic Impact

- Epic 3 remains valid and in progress.
- No epic is added, removed, or redefined.
- Story 3.3 becomes an explicit `done` prerequisite for Story 3.5 rather than an assumed historical condition.
- Story 3.5 retains ecosystem-wide catalog ownership and dependency-mode implementation responsibility, with explicit per-repository authority boundaries.
- Story 3.11 remains backlog and still activates only after Story 3.5 is done. Because AC4 remains open, this proposal does not accelerate Story 3.11.
- Stories 1.20 and 2.12 retain their current ownership and authorization boundaries.

### Story Impact

#### Story 3.3

- No acceptance criterion changes.
- Execute its verification tasks through the normal development and review workflows.
- Story 3.5 must not rely on the references-layout guarantee until Story 3.3 reaches `done` with current evidence.

#### Story 3.5

- Replace the unset-Debug-source rule with explicit source opt-in.
- Define unset and explicit `false` as package intent in every configuration, including Debug.
- Retain `Exists(...)` source-availability fallback: explicit source intent with missing source selects packages.
- Keep AC6 completion evidence ecosystem-wide across source-owned Hexalith consumers and the shared Builds catalog/governance surfaces.
- Obtain each affected repository maintainer's authority before editing; unavailable authority blocks Story 3.5 completion rather than converting noncompliance into an unmapped follow-up.
- Preserve the AC4 completion gate and keep Story 3.5 `in-progress` if the Tenants graph remains mixed after all independent work passes.

### Artifact Conflicts And Required Adjustments

| Artifact | Adjustment |
| --- | --- |
| `prd.md` FR21 and repository guardrail | Clarify that unset `UseHexalithProjectReferences` is package intent even in Debug; source mode requires explicit `true`. |
| `architecture.md` AD-11 | Add the same deterministic default and preserve source-missing package fallback. |
| `epics.md` Story 3.5 | Replace AC1, retain ecosystem-wide AC6, add Story 3.3 `done` sequencing, and retain AC4 blocking language. |
| Story 3.5 implementation artifact | Resolve the authority/scope gates, update the truth table/tasks/guardrails, retain the Story 3.3 and AC4 gates, and record the decision. |
| `epic-3-context.md` | State explicit source opt-in and the Story 3.3 `done` prerequisite. |
| `sprint-status.yaml` | Preserve Story 3.3 at `ready-for-dev`, move Story 3.5 from `backlog` to `ready-for-dev` now that its story file and approved decision record exist, and update comments to describe the reconciled activation and completion gates. |
| `deferred-work.md` | During Story 3.5, record any repository-authorization blocker without treating it as FR21 completion evidence, and retain the Gateway mixed-graph entry. |
| UX artifacts | No change. |
| Runtime code and deployment artifacts | No change from this course correction. |

### Technical Impact

The accepted dependency-mode rule becomes:

| Configuration | `UseHexalithProjectReferences` | Source exists | External edge |
| --- | --- | --- | --- |
| Debug | unset | either | package |
| Debug | `false` | either | package |
| Debug | `true` | yes | project/source |
| Debug | `true` | no | package fallback |
| Release | unset | either | package |
| Release | `false` | either | package |
| Release | `true` | yes | project/source |
| Release | `true` | no | package fallback |
| empty/unset configuration | unset | either | package |

`UseNuGetDeps` remains a compatibility input, but `UseHexalithProjectReferences` is the authoritative property when explicitly supplied. Contradictory inputs must either normalize to that authority or fail closed through one focused diagnostic; they must never activate both edges.

## 3. Recommended Approach

Use a **Direct Adjustment** within Epic 3.

1. Apply the planning and story corrections in this proposal.
2. Run Story 3.3 verification and review until it reaches `done`.
3. Resume Story 3.5 using the explicit-opt-in truth table and ecosystem-wide migration responsibility, working repository-by-repository under maintainer authority.
4. Complete all independent Story 3.5 work, but leave it `in-progress` if AC4 still lacks Story 1.20/2.12 graph-alignment evidence.

- **Planning scope:** Moderate; several authoritative artifacts and one active story specification change.
- **Implementation effort:** Expanded to the full FR21 consumer boundary, with added Story 3.3 verification and per-repository authorization/validation coordination.
- **Technical risk:** Lower than the current state because defaults and repository ownership become deterministic.
- **Timeline impact:** Story 3.5 waits for Story 3.3 review; Story 3.11 remains gated.
- **MVP impact:** None.

Rollback is not useful because no completed runtime behavior caused this contradiction. An MVP review is unnecessary because the change preserves FR21 and AD-11.

## 4. Detailed Change Proposals

### 4.1 PRD FR21

**OLD**

> Cross-repo Hexalith library dependencies must use Debug source project references when explicitly enabled and Release package references by default.

**NEW**

> Cross-repo Hexalith library dependencies use source project references only when `UseHexalithProjectReferences=true` is explicitly supplied and the root-declared source exists. An unset or explicit `false` value selects package references in every configuration, including Debug; Release and configuration-less evaluation therefore remain package-safe.

The existing shared-catalog ownership sentence remains unchanged.

**Rationale:** The existing wording says “explicitly enabled” but does not state the unset Debug outcome, allowing Story 3.5 AC1 to contradict it.

### 4.2 Architecture AD-11

**ADD after the source-reference rule**

> Unset or explicit `UseHexalithProjectReferences=false` is package intent in every configuration, including Debug. Explicit `true` is source intent, but each external edge still requires its root-declared source path to exist; missing source falls back to the centrally pinned package edge. Empty or unset configuration remains package-safe.

**Rationale:** This turns the adopted decision into an evaluable invariant and preserves the stale-assets recovery.

### 4.3 Epic Story 3.5 AC1

**OLD**

> Given `UseHexalithProjectReferences` is not explicitly set, when a Debug build evaluates project references, then external Hexalith project references are enabled when root-declared submodule source exists.

**NEW**

> **AC1 - Source references require explicit opt-in and explicit overrides win.**
>
> Given `UseHexalithProjectReferences=true` is explicitly supplied, when a build evaluates external Hexalith references and the root-declared source exists, then the project/source edge is selected; when source is missing, the centrally pinned package edge is selected.
>
> Given `UseHexalithProjectReferences` is unset or explicitly `false`, when Debug, Release, or configuration-less evaluation runs, then package references are selected and no external source edge is activated.

### 4.4 Epic Story 3.5 AC6

**OLD**

> Given any source-owned Hexalith project or root package props is scanned ... every dependency version originates from Builds.

**NEW**

> **AC6 - Builds is the only NuGet version authority across the Hexalith ecosystem.**
>
> Given source-owned Hexalith projects/root package props and the shared Builds catalog/governance surfaces are scanned, when NuGet version declarations are evaluated, then every source-owned dependency version originates from `references/Hexalith.Builds/Props/Directory.Packages.props`, and consuming props contain no local `PackageVersion`, `VersionOverride`, or fallback dependency-version property.
>
> Given an affected repository has not authorized or completed its migration, when Story 3.5 completion is evaluated, then the repository, owner/approval requirement, scope, rollback boundary, and prescribed validation remain recorded as an open Story 3.5 blocker, and the story remains `in-progress` without editing that repository outside its maintainer's authority.

### 4.5 Story 3.5 Implementation Artifact

Apply these coordinated edits:

- replace `implementation_gate` with an approved explicit-opt-in decision record;
- replace `scope_gate` with ecosystem-wide FR21 completion responsibility and per-repository authority/validation boundaries;
- retain `sequencing_gate`, strengthened to require Story 3.3 `done`;
- retain `completion_gate` for AC4;
- convert the requirements and catalog-scope reconciliation sections from open gates into approved decision records;
- update AC1, AC6, Task 1, Task 2, the truth table, top guardrails, file map, validation expectations, and change log consistently;
- do not mark implementation tasks complete merely because planning was reconciled.

### 4.6 Tracker And Context

- Keep Story 3.3 and Story 3.5 at `ready-for-dev` until their respective workflows change status.
- Update tracker comments to state: Story 3.3 must reach `done`; Story 3.5 may then start; AC4 still prevents Story 3.5 completion while Story 1.20/2.12 remain unresolved.
- Update Epic 3 context to state explicit source opt-in and the Story 3.3 `done` dependency.

### 4.7 Non-Planning Documentation And Follow-Ups

Story 3.5 implementation updates active build/package guidance (`_bmad-output/project-context.md`, brownfield development/project/source-tree docs, NuGet guide, and directly affected CI/operational guidance) so it names Builds as version owner and does not imply unset Debug source mode.

Separate catalog-migration entries are recorded for repositories still containing consumer-local versions. They require their owning maintainer's authority and are not silently assigned to EventStore.

## 5. Implementation Handoff

### Scope Classification

**Moderate** — Product/architecture planning is reconciled first, then the Developer workflow verifies Story 3.3 and resumes Story 3.5 within the corrected boundary.

### Recipients And Responsibilities

- **Product Owner / backlog maintainer:** approve AC1 and ecosystem-wide AC6 completion responsibility.
- **Architecture owner:** approve the deterministic explicit-opt-in invariant.
- **Developer:** apply artifact edits, verify Story 3.3, then implement Story 3.5 in task order.
- **Hexalith.Builds maintainer:** approve and review Builds-owned catalog, validator, documentation, workflow, and automation changes.
- **Other repository maintainers:** authorize, review, validate, and own their consumer-catalog migrations within Story 3.5's FR21 completion boundary.
- **EventStore/release owner:** retain the Story 1.20/2.12 gate and approve eventual AC4 closure evidence.

### Success Criteria

- FR21, AD-11, Epic Story 3.5, the Story 3.5 implementation artifact, Epic 3 context, and tracker comments agree that source mode requires explicit `true`.
- Story 3.5 AC6 is proven across every source-owned Hexalith consumer without overriding repository-maintainer authority.
- Any repository lacking authorization or migration evidence keeps Story 3.5 `in-progress` and visibly blocked.
- Story 3.3 reaches `done` with current verification evidence before Story 3.5 starts.
- Story 3.5 remains `in-progress` if AC4's no-mixed-graph criterion is still unproven.
- No Tenants dependency identity, broad dependency family, nested submodule, commit, or push is changed by this proposal.

## Appendix A - Change Navigation Checklist

### 1. Understand The Trigger And Context

- [x] 1.1 Triggering story: Story 3.5 development preflight.
- [x] 1.2 Core problem: cross-artifact requirement contradiction plus an over-broad acceptance boundary and an incomplete prerequisite.
- [x] 1.3 Evidence: PRD FR21, AD-11, Story 3.5 AC1/AC6, Story 3.3 unchecked verification tasks, sprint statuses, and the active Tenants mixed graph.

### 2. Epic Impact Assessment

- [x] 2.1 Epic 3 remains completable.
- [x] 2.2 Modify Story 3.5 and its sequencing; no new epic.
- [x] 2.3 Future stories reviewed: Story 3.11 remains gated; Story 2.12 retains identity ownership.
- [x] 2.4 No epic is obsolete and no new epic is required.
- [x] 2.5 Story order is clarified as Story 3.3 `done` before Story 3.5.

### 3. Artifact Conflict And Impact Analysis

- [x] 3.1 PRD needs an unset-Debug clarification, not a product-goal change.
- [x] 3.2 AD-11 needs the same evaluable default invariant.
- [N/A] 3.3 UX is unaffected.
- [x] 3.4 Epics, active stories, tracker comments, Epic 3 context, implementation docs, tests, CI workflows, and deferred-work ownership are impacted.

### 4. Path Forward Evaluation

- [x] 4.1 Direct Adjustment is viable: moderate planning scope, existing implementation effort, lower technical risk.
- [x] 4.2 Rollback is not useful; no completed runtime work caused the contradiction.
- [x] 4.3 MVP review is unnecessary; scope and goals remain unchanged.
- [x] 4.4 Direct Adjustment selected.

### 5. Sprint Change Proposal Components

- [x] 5.1 Issue summary completed.
- [x] 5.2 Epic, story, artifact, and technical impacts documented.
- [x] 5.3 Recommended path and alternatives documented.
- [x] 5.4 MVP impact and ordered action plan documented.
- [x] 5.5 Product, architecture, developer, Builds, repository-owner, and release-owner handoffs defined.

### 6. Final Review And Handoff

- [x] 6.1 Applicable checklist sections addressed.
- [x] 6.2 Proposal reviewed for consistency with FR21, AD-11, Story 3.3, and AC4.
- [x] 6.3 Administrator explicitly approved the complete proposal on 2026-07-18.
- [N/A] 6.4 No epic/story identifiers are added, removed, or renumbered.
- [x] 6.5 Moderate-scope handoff is routed to Product/Architecture ownership and the Developer workflows for Stories 3.3 and 3.5.
