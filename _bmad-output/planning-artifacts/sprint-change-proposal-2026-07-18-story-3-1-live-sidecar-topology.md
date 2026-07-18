---
title: Sprint Change Proposal - Align Story 3.1 With The Dedicated Live-Sidecar Topology
status: final
created: 2026-07-18
project: eventstore
mode: batch
scope_classification: minor
approval: approved
approved_by: Administrator
approved_on: 2026-07-18
finalized_on: 2026-07-18
trigger: Story 3.1 still specifies the superseded filtered single-project test topology
target_story: 3.1
---

# Sprint Change Proposal: Align Story 3.1 With The Dedicated Live-Sidecar Topology

## 1. Issue Summary

Story 3.1 was written against the post-PR-271 topology in which deterministic and live-sidecar tests still shared `tests/Hexalith.EventStore.Server.Tests` and CI selected them with `Category!=LiveSidecar` / `Category=LiveSidecar` filters.

Commit `12baa75c` (`chore(ci): align eventstore workflows`, 2026-07-09) subsequently introduced the dedicated `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` project and moved the live fixture and live test classes into it. The current authoritative topology is:

- `.github/workflows/ci.yml` passes `tests/Hexalith.EventStore.Server.Tests` as an unfiltered `unit-test-projects` entry to `Hexalith.Builds` `domain-ci.yml@main`.
- `.github/workflows/integration.yml` initializes DAPR and runs `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` unfiltered.
- `.github/workflows/release.yml` listens only to successful `CI` completion for a push to `main`; it has no dependency on `Integration Tests`.
- The live fixture is `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs`.
- The dedicated project currently contains 14 trait-tagged live-sidecar test classes. The inventory may grow; physical project ownership, not a frozen class count, is the durable lane boundary.

Story 3.1 still points to the old fixture path, freezes the original nine-class inventory, requires trait filters in both lanes, names removed local CI jobs, asks for repository-specific edits to the synchronized universal `CLAUDE.md`, cites Story 3.8 instead of the current Story 3.10 preflight, and assigns adjacent integration recovery to the now-unrelated Story 7.4 rather than Story 7.10.

The development attempt correctly halted before reversing the shipped physical split. This proposal updates Story 3.1 to verify the current topology instead.

## 2. Impact Analysis

### Epic Impact

- Epic 3 remains valid and in progress.
- FR17 and NFR10 remain unchanged: deterministic release-gate tests and live-sidecar tests stay in separate lanes.
- The current `epics.md` Story 3.1 acceptance criteria already describe the dedicated-project topology and need no edit.
- No epic is added, removed, redefined, or resequenced.
- Story 3.10 remains the companion environment-classification preflight.
- Story 7.10 remains the owner of full integration-lane recovery; Story 7.12 owns broader test reclassification.

### Story Impact

Story 3.1 remains a verification-and-reconciliation story, but its verification target changes from filtered subsets of one project to two physically separate projects:

| Concern | Superseded Story 3.1 assertion | Current assertion |
| --- | --- | --- |
| Deterministic project | `Server.Tests` minus `Category=LiveSidecar` | `Server.Tests`, unfiltered |
| Live project | `Server.Tests` plus `Category=LiveSidecar` | `Server.LiveSidecar.Tests`, unfiltered |
| Fixture path | `Server.Tests/Fixtures/...` | `Server.LiveSidecar.Tests/Fixtures/...` |
| CI ownership | local `server-inprocess-tests` job | shared `domain-ci.yml@main` through `unit-test-projects` |
| Live inventory | exactly nine classes | all live-sidecar classes in the dedicated project; 14 at the inspected baseline |
| Documentation edit | modify `CLAUDE.md` | do not add repository-specific CI rules to synchronized universal entry points |
| Preflight companion | Story 3.8 | Story 3.10 |
| Adjacent Integration CI owner | Story 7.4 | Story 7.10 |

### Artifact Conflicts And Required Adjustments

| Artifact | Adjustment |
| --- | --- |
| Story 3.1 implementation artifact | Replace stale paths, filtered-lane assertions, exact nine-class invariant, obsolete `CLAUDE.md` task, story references, code-state notes, commands, and references. Preserve the existing Dev Agent Record blocker evidence. |
| `sprint-status.yaml` | Keep Story 3.1 `in-progress` while the correction is reviewed and implemented. No key or epic-status change is required by this proposal. |
| `epics.md` | No change; its Story 3.1 criteria already match the dedicated topology. |
| `prd.md` | No FR17/NFR10 requirement change. |
| `architecture.md` | No change; AD-9, AD-11, and AD-12 remain applicable. |
| `ux.md` | No change. |
| Workflows, projects, tests, and fixture | No runtime or CI change; they are the shipped evidence being verified. |
| `docs/ci.md` | No change; it already documents the current lanes and explicitly rejects reintroducing `Category!=LiveSidecar`. |

The optional brownfield documentation and generated project context contain older `Server.Tests` build-failure claims. They are pre-existing documentation hygiene, not required to correct Story 3.1's project/lane contract, and should be handled by a separately scoped documentation task rather than silently expanding this story.

### Technical Impact

- No test class, project, fixture, workflow, DAPR component, package, or release behavior changes.
- Verification becomes stronger because lane ownership is asserted structurally by project membership and workflow input, not only by runtime trait filtering.
- Traits remain required for discoverability and semantic classification, but they no longer select the CI lane.
- Validation mirrors the shared CI build plus two project-level, unfiltered test runs.
- Existing user changes in planning artifacts and the Story 3.1 Dev Agent Record remain intact.

## 3. Recommended Approach

Use a **Direct Adjustment** within Story 3.1.

1. Approve this proposal.
2. Rewrite the Story 3.1 planning sections described below while preserving its development blocker record.
3. Resume the developer workflow from Task 1 against the dedicated projects.
4. Run the deterministic project without DAPR and the live project only after environment preflight/DAPR initialization.
5. Record project-level counts and persisted-state evidence, then complete the normal review/status handoff.

- **Planning scope:** Minor; one active story specification changes.
- **Implementation effort:** Low; documentation-only rewrite followed by the story's existing verification work.
- **Technical risk:** Low; the proposal aligns the story with already-shipped code and workflows.
- **Timeline impact:** Minimal; Story 3.1 may resume immediately after approval and rewrite.
- **MVP impact:** None.

Rollback is not viable because it would restore the inferior filtered single-project topology. An MVP review is unnecessary because FR17/NFR10 intent and scope do not change.

## 4. Detailed Change Proposals

### 4.1 Frontmatter And Source Paths

**OLD**

```yaml
baseline_commit: fc0f1de8bd3ed30a2b949d04eea56bde9b49e645
correct_course: >-
  ... the deterministic gate now lives in ci.yml.
source_files:
  - _bmad-output/implementation-artifacts/3-8-generated-api-dapr-aspire-smoke-preflight.md
  - tests/Hexalith.EventStore.Server.Tests/Fixtures/DaprTestContainerFixture.cs
  - tests/Directory.Build.props
  - CLAUDE.md
```

**NEW**

```yaml
baseline_commit: 4fc14c00386bf8a403a633612d36fd8842be990e
topology_origin_commit: 12baa75cf1ba702e61659e9081472c7b1400090a
correct_course: >-
  FR17 shipped in PR #271 and was later consolidated into a filtered single-project design.
  Commit 12baa75c then introduced the dedicated Server.LiveSidecar.Tests project. This story
  verifies the physical project split, unfiltered deterministic/live lanes, and current fixture.
source_files:
  - _bmad-output/implementation-artifacts/3-10-generated-api-dapr-aspire-smoke-preflight.md
  - Hexalith.EventStore.slnx
  - references/Hexalith.Builds/.github/workflows/domain-ci.yml
  - tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
  - tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
  - tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs
```

Keep the current PRD, architecture, epic, origin proposal, status, workflow, `docs/ci.md`, and deferred-work sources. Remove `CLAUDE.md` and the old fixture path. The exact implementation baseline is evidence, not a requirement that future verification run only at that commit.

**Rationale:** The story must name the topology it actually verifies and must not direct repository-specific changes into synchronized universal instruction entry points.

### 4.2 Story Context And Lane Model

**OLD**

> The blocking `server-inprocess-tests` job runs `Server.Tests --filter "Category!=LiveSidecar"`.
>
> A separate `live-sidecar` job runs `Server.Tests --filter "Category=LiveSidecar"`.
>
> Exactly nine live-sidecar classes define the live lane.

**NEW**

> The deterministic release gate is the `CI` thin caller. Its `unit-test-projects` input includes `tests/Hexalith.EventStore.Server.Tests`; the shared `domain-ci.yml@main` runs that project unfiltered after the Release solution build. The caller supplies no `integration-test-projects` input, so the blocking lane does not initialize DAPR.
>
> The dedicated `Integration Tests` workflow initializes DAPR and runs `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` unfiltered. Release listens only to successful `CI` completion for a push to `main`, so live-lane failure remains visible without becoming a semantic-release dependency.
>
> At the inspected baseline the live project contains 14 trait-tagged live-sidecar classes, including the original nine moved by commit `12baa75c` and five later additions. The durable invariant is that every live-`daprd` test belongs to the dedicated project and no such test remains in deterministic `Server.Tests`; the story records the current inventory as evidence without freezing the allowed class count.

Remove the obsolete `CLAUDE.md` deliverable. State that `docs/ci.md` and current workflows are the repository-specific CI authorities, while `AGENTS.md`, `CLAUDE.md`, and Copilot instructions remain synchronized universal baselines.

**Rationale:** Both lanes are selected by project now. Reintroducing filters would contradict the current CI documentation and weaken the physical boundary.

### 4.3 Acceptance Criteria

#### AC1 - Project Ownership, Traits, And Scoping

**OLD**

> Given the Server.Tests project, exactly these nine classes carry `LiveSidecar` and `DaprTestContainer` traits.

**NEW**

> **Given** tests that require a live `daprd` sidecar,
> **When** project ownership and traits are enumerated,
> **Then** every live-sidecar test class resides under `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` and carries `[Trait("Category", "LiveSidecar")]` plus `[Collection("DaprTestContainer")]`,
> **And** `tests/Hexalith.EventStore.Server.Tests` contains neither a `LiveSidecar`-tagged test nor the live DAPR fixture,
> **And** deterministic reflection/mocked tests such as `TombstoningLifecycleSentinelTests` and `ETagActorIntegrationTests` remain in `Server.Tests` without a `LiveSidecar` trait,
> **And** the Dev Agent Record captures the current live-class inventory without treating its count as a permanent acceptance invariant.

#### AC2 - Deterministic Release Gate

**OLD**

> The local `server-inprocess-tests` job runs `Server.Tests` with `--filter "Category!=LiveSidecar"` and installs no DAPR.

**NEW**

> **Given** the EventStore `CI` caller and shared `domain-ci.yml@main`,
> **When** deterministic project inputs and commands are inspected,
> **Then** `tests/Hexalith.EventStore.Server.Tests` is present in `unit-test-projects` and runs unfiltered,
> **And** `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` is absent from the blocking test-project input,
> **And** the caller supplies no DAPR integration-test input and the release path does not initialize DAPR solely for deterministic tests,
> **And** release remains gated by successful `CI` completion for the same pushed head.

#### AC3 - Dedicated Live-Sidecar Lane

**OLD**

> `integration.yml` runs `Server.Tests --filter "Category=LiveSidecar"`.

**NEW**

> **Given** `.github/workflows/integration.yml`,
> **When** its `live-sidecar` job is inspected,
> **Then** it initializes DAPR and runs `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` unfiltered,
> **And** it uploads test and coverage evidence even when the test step fails,
> **And** it retains its push, pull-request, and manual triggers plus independent concurrency group,
> **And** `.github/workflows/release.yml` has no dependency on `Integration Tests`.

#### AC4 - Fixture Readiness And Persisted Evidence

**OLD**

> Given `tests/Hexalith.EventStore.Server.Tests/Fixtures/DaprTestContainerFixture.cs` ...

**NEW**

> **Given** `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs`,
> **When** initialization and evidence helpers are inspected,
> **Then** Redis, placement, and scheduler prerequisites are checked before sidecar startup,
> **And** `WaitForDaprHealthAsync` and `WarmUpActorRuntimeAsync` retain bounded readiness and actor round-trip retries,
> **And** live tests use fixture/read-back paths such as persisted Redis actor state where their behavior depends on durable state,
> **And** a missing control plane is classified as an environment blocker rather than a product failure.

#### AC5 - Story And Authority Reconciliation

**OLD**

> Reconcile two stale repository-specific claims in `CLAUDE.md`.

**NEW**

> **Given** the synchronized universal instruction entry points and repository-specific CI authorities,
> **When** Story 3.1 is reconciled,
> **Then** it cites the current project/workflow/documentation sources and no longer asks for repository-specific edits to `CLAUDE.md`,
> **And** its historical single-project assertions remain only as superseded traceability,
> **And** Story 3.10 is named as the preflight companion and Story 7.10 as the adjacent Integration-CI owner,
> **And** `sprint-status.yaml` remains `in-progress` until verification and review actually complete.

#### AC6 - Validation And Recorded Evidence

**OLD**

> Run filtered deterministic and live subsets of `Server.Tests`.

**NEW**

> **Given** the validation commands below,
> **When** Story 3.1 verification runs,
> **Then** the Release solution build succeeds under warnings-as-errors,
> **And** unfiltered `Server.Tests` passes without DAPR,
> **And** unfiltered `Server.LiveSidecar.Tests` either passes after DAPR/environment preflight or records a support-safe environment blocker,
> **And** the Dev Agent Record captures exact project-level pass/fail/skip counts plus persisted-state evidence for live behaviors,
> **And** no trait filter is used to manufacture lane separation.

#### AC7 - Scope Boundaries

Keep the no-reimplementation, no-new-two-writer-test, and no-unrelated-runtime-fix boundaries, but replace Story 7.4 references with Story 7.10 and remove the frozen nine-class/threshold language. Existing readiness and warm-up behavior changes only when verification proves a defect.

### 4.4 Tasks And Subtasks

Replace the current task list with:

1. **Verify physical project ownership and trait semantics (AC1).**
   - Enumerate all `LiveSidecar`-tagged classes in `Server.LiveSidecar.Tests`.
   - Prove `Server.Tests` contains no `LiveSidecar` trait or live fixture.
   - Record the current inventory and the intentional deterministic sentinel/mock examples.
2. **Verify the deterministic release gate (AC2).**
   - Confirm `ci.yml` supplies unfiltered `Server.Tests` through `unit-test-projects`.
   - Confirm the live project is absent and no DAPR integration input is supplied.
   - Confirm release listens only to successful same-head `CI` completion.
3. **Verify the dedicated live lane (AC3).**
   - Confirm `integration.yml` initializes DAPR and runs the dedicated live project unfiltered.
   - Confirm artifact upload and non-release dependency semantics.
4. **Verify live fixture readiness and evidence seams (AC4).**
   - Inspect the fixture at its dedicated-project path.
   - Confirm prerequisite, health, warm-up, and persisted-state read-back behavior.
5. **Reconcile Story 3.1 authorities and references (AC5).**
   - Remove the obsolete `CLAUDE.md` task/source.
   - Update Story 3.10/7.10 ownership references and preserve historical notes as superseded evidence only.
6. **Validate both projects and record evidence (AC6).**
   - Run the Release solution build and unfiltered deterministic project.
   - Preflight DAPR, then run the unfiltered live project or record an environment blocker.
   - Capture counts and persisted-state evidence; reconcile FR17 done evidence only after review.
7. **Enforce scope boundaries (AC7).**
   - Confirm no workflow, test-project split, DAPR threshold, two-writer scenario, or unrelated integration recovery was changed.

Remove the old task to edit `CLAUDE.md` and every task that asks for `Category!=LiveSidecar` or `Category=LiveSidecar` command filters.

### 4.5 Dev Notes And Current Code State

Replace the pre-split code-state block with current structural evidence:

- `Hexalith.EventStore.slnx` includes both test projects.
- `.github/workflows/ci.yml` is a thin shared caller; `unit-test-projects` contains deterministic `Server.Tests` only.
- `references/Hexalith.Builds/.github/workflows/domain-ci.yml` restores/builds the solution and runs every unit-test project unfiltered with `--no-build`.
- `.github/workflows/integration.yml` initializes DAPR and runs `Server.LiveSidecar.Tests` unfiltered with a live-sidecar TRX and coverage artifact.
- `.github/workflows/release.yml` delegates to `domain-release.yml@main` only after successful same-head `CI` completion for a push.
- Commit `12baa75c` created the dedicated project and moved the original fixture/live classes.
- The current live project has 14 `LiveSidecar`-tagged test classes; the five post-split additions are `BenchmarkDatasetBuilderLiveSidecarTests`, `NamedProjectionDispatchLiveSidecarTests`, `ProjectionDeliveryCutoverLiveSidecarTests`, `ProjectionEraseLiveSidecarTests`, and `ReadModelBatchLiveSidecarTests`.
- `docs/ci.md` is already accurate and forbids reintroducing the deterministic exclusion filter.

Update guardrails to say the physical split is authoritative, Story 3.10 owns local preflight, Story 7.10 owns Integration CI recovery, and exact class counts are evidence rather than frozen scope.

### 4.6 Validation Commands

**OLD**

```bash
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj \
  --configuration Release -warnaserror -p:UseHexalithProjectReferences=false

dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release \
  --filter "Category!=LiveSidecar" -p:UseHexalithProjectReferences=false

dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release \
  --filter "Category=LiveSidecar" -p:UseHexalithProjectReferences=false
```

**NEW**

```bash
# Restore and build the same Release solution surface used by shared CI.
dotnet restore Hexalith.EventStore.slnx \
  -p:Configuration=Release \
  -p:UseHexalithProjectReferences=false
dotnet build Hexalith.EventStore.slnx \
  --no-restore \
  --configuration Release \
  -warnaserror \
  -p:UseHexalithProjectReferences=false

# Deterministic blocking project: no DAPR and no trait filter.
dotnet test \
  tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj \
  --no-build \
  --configuration Release \
  -p:UseHexalithProjectReferences=false

# Classify local DAPR/Aspire prerequisites before accepting a live-lane result.
scripts/generated-api-smoke-preflight.sh

# Dedicated live-sidecar project: DAPR required and no trait filter.
dotnet test \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj \
  --no-build \
  --configuration Release \
  -p:UseHexalithProjectReferences=false
```

When the live environment is intentionally unavailable, run the fixture preflight through the dedicated project or record the Story 3.10 preflight result as `blocked`; do not report product failure. Do not preserve the old #271 test counts as a current baseline because coverage has expanded since the physical split.

### 4.7 References And Dev Agent Record

- Replace stale line-number references with current paths/symbols where practical.
- Point fixture references to `Server.LiveSidecar.Tests`.
- Add the dedicated project file, solution entry, shared `domain-ci.yml`, commit `12baa75c`, and `docs/ci.md` lane table.
- Remove `CLAUDE.md`, the old fixture path, local `server-inprocess-tests` job ranges, and filtered command references.
- Preserve the existing 2026-07-18 Dev Agent Record entries that identified the contradiction.
- Append a change-log entry only when the approved rewrite is applied; do not rewrite the historical halt as if implementation had already completed.

## 5. Implementation Handoff

### Approval And Routing Record

- Administrator approved this proposal on 2026-07-18.
- The approved Story 3.1 specification rewrite was applied without changing runtime code, tests, workflows, or synchronized universal instruction files.
- The corrected story remains `in-progress` and is routed to the Developer agent for resumed verification, the Test Architect/reviewer for evidence review, and the backlog maintainer for the eventual normal status transition.
- Approval finalizes this course-correction artifact; it does not claim that the corrected validation commands have run or that Story 3.1 is complete.

### Scope And Recipient

This is a **Minor** course correction routed to the Developer agent for direct Story 3.1 specification reconciliation and resumed verification.

### Responsibilities

- **Developer agent:** apply the approved story-only rewrite, preserve user-authored blocker evidence, execute the corrected tasks/commands, and record exact evidence.
- **Test Architect / reviewer:** verify physical lane ownership, no-filter commands, DAPR blocker classification, and persisted-state evidence quality.
- **Backlog maintainer:** keep Story 3.1 `in-progress` until verification/review succeeds, then apply the normal status transition; do not rename or duplicate the tracker key.

### Success Criteria

- Every Story 3.1 live fixture/test path points to `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`.
- Deterministic and live lanes are both asserted and validated unfiltered by project.
- `ci.yml`, shared `domain-ci.yml`, `integration.yml`, and `release.yml` ownership/dependency statements match current files.
- The story does not freeze the live test inventory at nine or reintroduce trait filters.
- The obsolete `CLAUDE.md` task/source is removed without changing the synchronized universal instruction files.
- Story 3.10 and Story 7.10 references replace obsolete Story 3.8 and Story 7.4 ownership claims.
- Corrected commands use the `.slnx` only for restore/build and run each test project individually.
- The existing halt/debug record remains intact, and Story 3.1 is not marked done before current verification and review evidence exists.

## Appendix A - Change Navigation Checklist

### 1. Understand The Trigger And Context

- [x] 1.1 Triggering story identified: Story 3.1 halted during Task 1 verification.
- [x] 1.2 Core problem defined: the story specifies a superseded filtered single-project topology after commit `12baa75c` introduced physical project separation.
- [x] 1.3 Evidence recorded from the solution, both project directories, workflows, shared reusable workflow, current documentation, Git history, and the Dev Agent Record.

### 2. Epic Impact Assessment

- [x] 2.1 Epic 3 remains completable as planned.
- [N/A] 2.2 No epic-scope or epic-acceptance change is required; current `epics.md` is already aligned.
- [x] 2.3 Remaining epics/stories reviewed; Story 3.10 and Stories 7.10/7.12 retain adjacent ownership.
- [N/A] 2.4 No epic becomes obsolete and no new epic is needed.
- [N/A] 2.5 No epic order or priority change is needed.

### 3. Artifact Conflict And Impact Analysis

- [x] 3.1 PRD FR17/NFR10 remain valid; no product-scope change.
- [x] 3.2 Architecture AD-9/AD-11/AD-12 remain valid; no decision change.
- [N/A] 3.3 UX is unaffected.
- [x] 3.4 Story 3.1 is the required edit; workflows/projects/tests/docs are read-only evidence. Optional brownfield/project-context hygiene is separately scoped.

### 4. Path Forward Evaluation

- [x] 4.1 Direct Adjustment is viable with low effort and low technical risk.
- [N/A] 4.2 Rollback is not viable; it would restore the superseded filtered topology.
- [N/A] 4.3 MVP review is unnecessary; FR17/NFR10 scope remains intact.
- [x] 4.4 Direct Adjustment selected because it preserves shipped behavior and repairs only the stale story contract.

### 5. Sprint Change Proposal Components

- [x] 5.1 Issue summary completed.
- [x] 5.2 Epic, story, artifact, and technical impacts documented.
- [x] 5.3 Recommended path, effort, risk, timeline, and alternatives documented.
- [x] 5.4 MVP impact and implementation order documented.
- [x] 5.5 Developer, reviewer, and backlog handoff defined.

### 6. Final Review And Handoff

- [x] 6.1 Applicable checklist sections reviewed; no unresolved critical analysis gap.
- [x] 6.2 Proposal checked against current repository evidence and user-owned changes.
- [x] 6.3 Administrator explicitly approved the proposal on 2026-07-18.
- [N/A] 6.4 No epic/story key is added, removed, or renumbered; no tracker structure update is required.
- [x] 6.5 Approved handoff activated; Story 3.1 remains `in-progress` pending verification and review.
