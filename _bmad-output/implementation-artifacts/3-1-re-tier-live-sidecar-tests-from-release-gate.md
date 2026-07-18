---
baseline_commit: 4fc14c00386bf8a403a633612d36fd8842be990e
topology_origin_commit: 12baa75cf1ba702e61659e9081472c7b1400090a
created: 2026-07-07
updated: 2026-07-18
story_key: 3-1-re-tier-live-sidecar-tests-from-release-gate
epic: "Epic 3 - Release And Repository Reliability"
requirements: FR17
governing_nfr: NFR10
story_type: verification-and-reconciliation
correct_course: >-
  FR17 shipped in PR #271 and was later consolidated into a filtered single-project design.
  Commit 12baa75c then introduced the dedicated Server.LiveSidecar.Tests project. This story now
  verifies the physical project split, unfiltered deterministic and live lanes, and the fixture at
  its current dedicated-project path. The 2026-07-18 sprint change proposal was approved by
  Administrator and supersedes the earlier single-project assertions in this story.
source_files:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-ci-release-retier.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18-story-3-1-live-sidecar-topology.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md
  - _bmad-output/implementation-artifacts/3-10-generated-api-dapr-aspire-smoke-preflight.md
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/spec-integration-e2e-test-recovery.md
  - docs/ci.md
  - .github/workflows/ci.yml
  - .github/workflows/release.yml
  - .github/workflows/integration.yml
  - Hexalith.EventStore.slnx
  - references/Hexalith.Builds/.github/workflows/domain-ci.yml
  - tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
  - tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
  - tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs
---

# Story 3.1: Re-Tier Live-Sidecar Tests From Release Gate

Status: in-progress

<!-- CORRECT-COURSE REWRITE (2026-07-18):
     The earlier verification contract described filtered subsets of Server.Tests. Commit
     12baa75c physically separated live-sidecar tests into Server.LiveSidecar.Tests. The approved
     correction below treats project ownership as the lane boundary and retains the old model only
     as historical traceability. Do not reintroduce Category filters to implement this story. -->

## Story

As a **release maintainer**,
I want **to verify that deterministic and live-DAPR tests are physically separated into their current projects and executed by independent, unfiltered CI lanes**,
so that **FR17/NFR10 lane separation remains provable without making live infrastructure a semantic-release dependency**.

## Story Context

This is a verification-and-reconciliation story, not a request to redesign the test topology. PR #271 / commit `13320952` first moved live-DAPR coverage off the release gate with trait filters. Commit `84ac5b41` consolidated CI and release automation. Commit `12baa75c` then replaced that filtered single-project layout with the current physical split:

- The blocking `CI` workflow calls `Hexalith.Builds` `domain-ci.yml@main`. Its `unit-test-projects` input includes `tests/Hexalith.EventStore.Server.Tests`, and the shared workflow runs that deterministic project unfiltered.
- The `CI` input does not include `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` and supplies no integration-test project input that would initialize DAPR for this lane.
- The independent `Integration Tests` workflow initializes DAPR and runs `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` unfiltered.
- `Release` listens only to successful `CI` completion for a push to `main`, requires the same tested head SHA, and has no dependency on `Integration Tests`.
- The live fixture is `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs`.
- At the inspected baseline, the dedicated project contains 14 classes carrying `Category=LiveSidecar` and `DaprTestContainer`. This count is evidence, not a permanent acceptance invariant; physical project ownership is the durable lane boundary.

Repository-specific CI authority lives in the workflows, shared reusable workflow, solution/project files, and `docs/ci.md`. The synchronized universal `AGENTS.md`, `CLAUDE.md`, and Copilot entry point must not receive EventStore-specific lane rules.

Story 3.10 owns local DAPR/Aspire environment preflight. Story 7.10 owns adjacent Integration-CI recovery, and Story 7.12 owns broader test classification. This story does not absorb that work.

## Acceptance Criteria

### AC1 - Physical Project Ownership, Traits, And Scoping

**Given** tests that require a live `daprd` sidecar,
**When** project ownership and traits are enumerated,
**Then** every live-sidecar test class resides under `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` and carries `[Trait("Category", "LiveSidecar")]` plus `[Collection("DaprTestContainer")]`,
**And** `tests/Hexalith.EventStore.Server.Tests` contains neither a `LiveSidecar`-tagged test nor the live DAPR fixture,
**And** deterministic reflection or mocked tests such as `TombstoningLifecycleSentinelTests` and `ETagActorIntegrationTests` remain in `Server.Tests` without a `LiveSidecar` trait,
**And** the Dev Agent Record captures the current live-class inventory without treating its count as a permanent acceptance invariant.

### AC2 - Deterministic Release Gate

**Given** the EventStore `CI` caller and shared `domain-ci.yml@main`,
**When** deterministic project inputs and commands are inspected,
**Then** `tests/Hexalith.EventStore.Server.Tests` is present in `unit-test-projects` and runs unfiltered,
**And** `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` is absent from the blocking test-project input,
**And** the caller supplies no DAPR integration-test input and the release path does not initialize DAPR solely for deterministic tests,
**And** `Release` remains gated by successful same-head `CI` completion for a push.

### AC3 - Dedicated Live-Sidecar Lane

**Given** `.github/workflows/integration.yml`,
**When** its `live-sidecar` job is inspected,
**Then** it initializes DAPR and runs `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` unfiltered,
**And** it uploads TRX and coverage evidence even when the test step fails,
**And** it retains push, pull-request, and manual triggers plus an independent concurrency group,
**And** `.github/workflows/release.yml` has no dependency on `Integration Tests`.

### AC4 - Fixture Readiness And Persisted Evidence

**Given** `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs`,
**When** initialization and evidence helpers are inspected,
**Then** Redis, placement, and scheduler prerequisites are checked before sidecar startup,
**And** `WaitForDaprHealthAsync` and `WarmUpActorRuntimeAsync` retain bounded readiness and actor round-trip retries,
**And** live tests use fixture or read-back paths such as persisted Redis actor state where their behavior depends on durable state,
**And** a missing control plane is classified as an environment blocker rather than a product failure.

### AC5 - Story And Authority Reconciliation

**Given** the synchronized universal instruction entry points and repository-specific CI authorities,
**When** Story 3.1 is reconciled,
**Then** it cites the current project, workflow, shared-workflow, solution, and documentation sources and no longer asks for repository-specific edits to `CLAUDE.md`,
**And** its historical single-project assertions remain only as superseded traceability,
**And** Story 3.10 is named as the preflight companion and Story 7.10 as the adjacent Integration-CI owner,
**And** `sprint-status.yaml` remains `in-progress` until verification and review actually complete.

### AC6 - Validation And Recorded Evidence

**Given** the validation commands in Dev Notes,
**When** Story 3.1 verification runs,
**Then** the Release solution build succeeds under warnings-as-errors,
**And** unfiltered `Server.Tests` passes without DAPR,
**And** unfiltered `Server.LiveSidecar.Tests` either passes after DAPR/environment preflight or records a support-safe environment blocker,
**And** the Dev Agent Record captures exact project-level pass/fail/skip counts plus persisted-state evidence for live behaviors,
**And** no trait filter is used to manufacture lane separation.

### AC7 - Scope Boundaries

**Given** the FR17/NFR10 boundary,
**When** this story is completed,
**Then** no existing workflow, project split, trait taxonomy, DAPR readiness threshold, or release dependency is changed unless verification proves a defect,
**And** no new two-writer race test or unrelated runtime fix is added,
**And** Story 7.10 Integration-CI recovery and Story 7.12 broader classification work remain outside this story,
**And** live-class growth is permitted when project ownership and trait semantics remain correct.

## Historical Acceptance-Criteria Mapping

The original epic intent remains intact:

1. Live-`daprd` tests are marked and removed from the deterministic release gate. This is now enforced by dedicated-project ownership and verified by AC1/AC2.
2. The release path excludes live-sidecar execution and does not initialize DAPR solely for deterministic tests. This is verified by AC2.
3. A dedicated DAPR workflow runs live-sidecar coverage without becoming a semantic-release dependency. This is verified by AC3.
4. The shared live fixture performs readiness retry and actor warm-up before assertions. This is verified by AC4.

The earlier `Server.Tests --filter Category!=LiveSidecar` and `Server.Tests --filter Category=LiveSidecar` implementation is superseded and must not be restored.

## Tasks / Subtasks

- [ ] **Task 1 - Verify physical project ownership and trait semantics (AC1).**
  - [ ] Enumerate all `LiveSidecar`-tagged classes in `Server.LiveSidecar.Tests`.
  - [ ] Prove `Server.Tests` contains no `LiveSidecar` trait or live fixture.
  - [ ] Record the current inventory and the intentional deterministic sentinel/mock examples.
- [ ] **Task 2 - Verify the deterministic release gate (AC2).**
  - [ ] Confirm `ci.yml` supplies unfiltered `Server.Tests` through `unit-test-projects`.
  - [ ] Confirm the live project is absent and no DAPR integration-test input is supplied.
  - [ ] Confirm release listens only to successful same-head `CI` completion.
- [ ] **Task 3 - Verify the dedicated live lane (AC3).**
  - [ ] Confirm `integration.yml` initializes DAPR and runs the dedicated live project unfiltered.
  - [ ] Confirm result upload and non-release-dependency semantics.
- [ ] **Task 4 - Verify live fixture readiness and evidence seams (AC4).**
  - [ ] Inspect the fixture at its dedicated-project path.
  - [ ] Confirm prerequisite, health, warm-up, and persisted-state read-back behavior.
- [x] **Task 5 - Reconcile Story 3.1 authorities and references (AC5).**
  - [x] Remove the obsolete `CLAUDE.md` task/source.
  - [x] Update Story 3.10/7.10 ownership references and preserve historical notes only as superseded evidence.
  - [x] Retain Story 3.1 as `in-progress`.
- [ ] **Task 6 - Validate both projects and record evidence (AC6).**
  - [ ] Restore and build the Release solution with warnings as errors.
  - [ ] Run the unfiltered deterministic project without DAPR.
  - [ ] Preflight DAPR, then run the unfiltered live project or record an environment blocker.
  - [ ] Capture counts and persisted-state evidence; reconcile FR17 done evidence only after review.
- [ ] **Task 7 - Enforce scope boundaries (AC7).**
  - [ ] Confirm no workflow, test-project split, DAPR threshold, two-writer scenario, or unrelated integration recovery was changed.

## Dev Notes

### Guardrails

- **The physical split is authoritative.** Do not add `Category!=LiveSidecar` or `Category=LiveSidecar` command filters. If a test is in the wrong lane, correct project ownership through an explicitly reviewed implementation change.
- **Do not re-implement shipped CI.** Workflows, projects, tests, and the fixture are evidence unless verification exposes a genuine defect.
- **Release gate means `CI`.** `release.yml` is a same-head `workflow_run` consumer and delegates publishing to `domain-release.yml@main`; it runs only after successful `CI` for a push.
- **Environment is not product behavior.** Use the Story 3.10 preflight and fixture prerequisite checks to classify missing Redis, placement, scheduler, or DAPR support as `blocked`.
- **Persisted evidence is required.** A live result must rely on durable state/read-back evidence where relevant, not only an accepted HTTP status or mock count.
- **Exact class counts are evidence, not scope.** Record the inventory observed during verification without freezing future live-suite growth.
- **Do not edit synchronized universal entry points.** EventStore-specific CI guidance belongs in `docs/ci.md` or repository configuration.

### Current Code State At The Corrected Baseline

- `Hexalith.EventStore.slnx` includes both `Server.Tests` and `Server.LiveSidecar.Tests`.
- `.github/workflows/ci.yml` is a thin shared caller whose `unit-test-projects` includes deterministic `Server.Tests` and excludes the live project.
- `references/Hexalith.Builds/.github/workflows/domain-ci.yml` restores/builds the solution and runs each supplied unit-test project unfiltered with `--no-build`.
- `.github/workflows/integration.yml` initializes DAPR, runs `Server.LiveSidecar.Tests` unfiltered, and uploads TRX/coverage artifacts with `if: always()`.
- `.github/workflows/release.yml` delegates to `domain-release.yml@main` only after successful same-head `CI` completion for a push and has no `Integration Tests` dependency.
- Commit `12baa75c` created the dedicated live project and moved the original live fixture/classes.
- `docs/ci.md` documents the current lanes and explicitly prohibits reintroducing `Category!=LiveSidecar` into deterministic `Server.Tests`.

At baseline `4fc14c00`, the 14 live-sidecar classes are:

- `Actors/ActorConcurrencyConflictTests`
- `Actors/ActorTenantIsolationTests`
- `Actors/AggregateActorIntegrationTests`
- `Actors/TombstoningLifecycleTests`
- `Benchmarking/BenchmarkDatasetBuilderLiveSidecarTests`
- `Commands/CommandRoutingIntegrationTests`
- `DomainServices/DaprSerializationRoundTripTests`
- `Events/EventPersistenceIntegrationTests`
- `Events/SnapshotIntegrationTests`
- `Integration/DaprETagServiceLiveSidecarTests`
- `Integration/NamedProjectionDispatchLiveSidecarTests`
- `Integration/ProjectionDeliveryCutoverLiveSidecarTests`
- `Integration/ProjectionEraseLiveSidecarTests`
- `Integration/ReadModelBatchLiveSidecarTests`

### Validation Commands

Run from the repository root. The `.slnx` is the restore/build surface; each test lane runs its own project without a trait filter.

```bash
dotnet restore Hexalith.EventStore.slnx \
  -p:Configuration=Release \
  -p:UseHexalithProjectReferences=false

dotnet build Hexalith.EventStore.slnx \
  --no-restore \
  --configuration Release \
  -warnaserror \
  -p:UseHexalithProjectReferences=false

dotnet test \
  tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj \
  --no-build \
  --configuration Release \
  -p:UseHexalithProjectReferences=false

scripts/generated-api-smoke-preflight.sh

dotnet test \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj \
  --no-build \
  --configuration Release \
  -p:UseHexalithProjectReferences=false
```

If the live environment is intentionally unavailable, record the Story 3.10 preflight or dedicated fixture prerequisite result as `blocked`; do not report a product failure. Do not reuse historical PR #271 counts as the current baseline.

### References

- [Source: _bmad-output/planning-artifacts/prd.md] FR17, NFR10, and traceability.
- [Source: _bmad-output/planning-artifacts/epics.md] current Story 3.1 criteria and Story 3.10 companion.
- [Source: _bmad-output/planning-artifacts/architecture.md] AD-9, AD-11, and AD-12.
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18-story-3-1-live-sidecar-topology.md] approved course correction.
- [Source: docs/ci.md] EventStore-specific lane and release-flow authority.
- [Source: .github/workflows/ci.yml] deterministic project input.
- [Source: references/Hexalith.Builds/.github/workflows/domain-ci.yml] shared restore/build/test behavior.
- [Source: .github/workflows/integration.yml] dedicated DAPR lane and result upload.
- [Source: .github/workflows/release.yml] same-head CI dependency and shared release delegation.
- [Source: Hexalith.EventStore.slnx] physical solution membership.
- [Source: tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs] prerequisite, readiness, warm-up, and read-back behavior.
- [Source: _bmad-output/implementation-artifacts/3-10-generated-api-dapr-aspire-smoke-preflight.md] local environment classification.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] adjacent work that remains deferred.

## Dev Agent Record

### Agent Model Used

OpenAI Codex (GPT-5)

### Implementation Plan / Decisions

- Verify the shipped lane separation before making any product or workflow edits.
- Preserve the newer physical test-project split and stop if the story's pre-split topology requirements conflict with current authoritative CI documentation.
- Apply Administrator's approved 2026-07-18 course correction to the story contract, then resume verification from the dedicated-project topology without treating the rewrite as completed runtime validation.

### Debug Log References

- 2026-07-18: Task 1 verification at repository HEAD `4fc14c00` found all nine original live-sidecar classes under `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`, each with `Category=LiveSidecar` and `DaprTestContainer`; `TombstoningLifecycleSentinelTests` and mocked `ETagActorIntegrationTests` remain untagged as required.
- 2026-07-18: The dedicated project now contains 14 live-sidecar classes, not the AC1 baseline set of exactly nine. Current `ci.yml` delegates to the shared `domain-ci.yml` and runs deterministic `Server.Tests` unfiltered; `integration.yml` runs `Server.LiveSidecar.Tests` unfiltered. `docs/ci.md` explicitly says not to reintroduce `Category!=LiveSidecar` into `Server.Tests`.
- 2026-07-18: The two AC5 repository-specific statements no longer exist in `CLAUDE.md`; that file is now the synchronized universal assistant baseline and directs repository-specific CI guidance to authoritative configuration/documentation.
- 2026-07-18: HALT before Task 1 completion. Tasks 2, 3, 5, and 6 target the superseded pre-split topology, so satisfying them literally would reverse intentional later work. Story 3.1 needs a correct-course rewrite against the current physical test-project separation before implementation can continue.
- 2026-07-18: Administrator approved the dedicated live-sidecar topology course correction. The story specification was rewritten and statically reconciled; its runtime validation commands were not rerun as part of this workflow.

### Completion Notes List

- No product, test, workflow, or shared-guidance change was made. Verification initially stopped fail-closed on the post-baseline topology contradiction.
- The Story 3.1 planning contract now matches the dedicated live-sidecar topology. Runtime verification and evidence capture remain in progress.

### File List

- `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md` (modified: approved course correction plus preserved development blocker record)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified: Story 3.1 marked in progress)
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18-story-3-1-live-sidecar-topology.md` (added: approved course-correction proposal)

### Change Log

- 2026-07-18: Began verification and recorded a correct-course blocker caused by the later dedicated live-sidecar test-project split; no task was marked complete.
- 2026-07-18: Applied the approved Story 3.1 specification correction for dedicated project paths, unfiltered lane assertions, and current validation commands; runtime verification remains pending.
