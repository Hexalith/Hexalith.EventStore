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
**And** `sprint-status.yaml` remains `in-progress` while verification is active, moves to `review` when evidence is ready for code review, and does not move to `done` until review findings are resolved.

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

- [x] **Task 1 - Verify physical project ownership and trait semantics (AC1).**
  - [x] Enumerate all `LiveSidecar`-tagged classes in `Server.LiveSidecar.Tests`.
  - [x] Prove `Server.Tests` contains no `LiveSidecar` trait or live fixture.
  - [x] Record the current inventory and the intentional deterministic sentinel/mock examples.
- [x] **Task 2 - Verify the deterministic release gate (AC2).**
  - [x] Confirm `ci.yml` supplies unfiltered `Server.Tests` through `unit-test-projects`.
  - [x] Confirm the live project is absent and no DAPR integration-test input is supplied.
  - [x] Confirm release listens only to successful same-head `CI` completion.
- [x] **Task 3 - Verify the dedicated live lane (AC3).**
  - [x] Confirm `integration.yml` initializes DAPR and runs the dedicated live project unfiltered.
  - [x] Confirm result upload and non-release-dependency semantics.
- [x] **Task 4 - Verify live fixture readiness and evidence seams (AC4).**
  - [x] Inspect the fixture at its dedicated-project path.
  - [x] Confirm prerequisite, health, warm-up, and persisted-state read-back behavior.
- [x] **Task 5 - Reconcile Story 3.1 authorities and references (AC5).**
  - [x] Remove the obsolete `CLAUDE.md` task/source.
  - [x] Update Story 3.10/7.10 ownership references and preserve historical notes only as superseded evidence.
  - [x] Retain Story 3.1 as `in-progress` during verification, then move it to `review` when the evidence packet is ready.
- [x] **Task 6 - Validate both projects and record evidence (AC6).**
  - [x] Restore and build the Release solution with warnings as errors.
  - [x] Run the unfiltered deterministic project without DAPR.
  - [x] Preflight DAPR, then run the unfiltered live project or record an environment blocker.
  - [x] Capture counts and persisted-state evidence; reconcile FR17 done evidence only after review.
- [x] **Task 7 - Enforce scope boundaries (AC7).**
  - [x] Confirm no workflow, test-project split, DAPR threshold, two-writer scenario, or unrelated integration recovery was changed.

### Review Findings

- [ ] [Review][Patch] Ratify the accepted submodule advances and add focused source-mode behavioral validation — Administrator accepted all present bumps. The final observed targets are `references/Hexalith.Builds` `e64ae34e` -> `14ef97cf` (behavioral source validation ran at its parent `ff721456`; `14ef97cf` changes only `CHANGELOG.md`), `references/Hexalith.FrontComposer` `06b39738` -> `5c284c89` (including the initially reviewed `564b1bad` target), and `references/Hexalith.Tenants` `fbff4649` -> `2d85e35a` (including the initially reviewed `0733a4e0` target). Current source-mode validation is complete for the EventStore/Tenants graph (build: 0 warnings/errors; `TenantsApiLaunchSettingsTests`: 4/4 passed, including the adopted launch profile). FrontComposer validation remains blocked at `5c284c89`: focused restore and `--no-restore` build both fail under warnings-as-errors with `NU1902` for vulnerable `AngleSharp 1.4.0` (`GHSA-pgww-w46g-26qg`). NuGet audit was not suppressed. [references/Hexalith.Tenants:1]
- [x] [Review][Patch] Restore ecosystem-wide FR21 completion responsibility to Story 3.5 — Administrator chose to keep Story 3.5 as FR21's ecosystem-wide implementation owner. Reconciled the Story 3.5 artifact, epic acceptance criteria, sprint-change proposal, context, tracker comments, and PRD traceability so partial Builds+EventStore work cannot close FR21. [_bmad-output/implementation-artifacts/3-5-shared-package-catalog-and-source-package-reference-modes.md:20]
- [x] [Review][Patch] Execute and bind both proof-packet validators against valid and adversarial inputs — The focused Contracts test now extracts both operative executable validators, binds their intended inputs, accepts the real allowlist, and rejects empty, malformed identity, extra-role/member, and multi-document inputs without another approved-membership copy. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ProofPacketValidatorIntegrityTests.cs:21]
- [x] [Review][Patch] Reconcile the Story 3.1 status-transition contract with the verification-to-review lifecycle and the unresolved-action return to `in-progress` [_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md:40]
- [x] [Review][Patch] Correct the recorded preflight evidence because the generated-API script exits when Aspire is not running [_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md:234]
- [x] [Review][Patch] Replace `git status` scope proof with baseline-to-candidate evidence and acknowledge the mixed range [_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md:291]
- [x] [Review][Patch] Align the FR21 traceability summary with explicit source opt-in and package-safe defaults [_bmad-output/planning-artifacts/prd.md:380]
- [x] [Review][Patch] Make the anchored-reference link guard cover normalized CommonMark destinations instead of seven literal spellings [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs:34]

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

# Story 3.10 diagnoses the full Aspire topology. Exit 3 is an accepted, recorded
# topology-not-running classification when the dedicated fixture prerequisites are available.
scripts/generated-api-smoke-preflight.sh || test "$?" -eq 3

dotnet test \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj \
  --no-build \
  --configuration Release \
  -p:UseHexalithProjectReferences=false
```

The Story 3.10 script diagnoses the full generated-API Aspire topology, while `Server.LiveSidecar.Tests` starts its own `daprd` and performs its own Redis/placement/scheduler prerequisite checks. Record script exit `3` as `topology-not-running` rather than claiming a successful full-topology preflight; the dedicated project may continue only when its fixture-specific prerequisite and warm-up checks pass. If the live environment is unavailable, record the script or fixture result as `blocked`; do not report a product failure. Do not reuse historical PR #271 counts as the current baseline.

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
- 2026-07-18: Task 1 (AC1) re-verified at repository HEAD. Confirmed by `grep` that exactly 14 classes under `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` carry both `[Collection("DaprTestContainer")]` and `[Trait("Category", "LiveSidecar")]`: `Actors/ActorConcurrencyConflictTests`, `Actors/ActorTenantIsolationTests`, `Actors/AggregateActorIntegrationTests`, `Actors/TombstoningLifecycleTests`, `Benchmarking/BenchmarkDatasetBuilderLiveSidecarTests`, `Commands/CommandRoutingIntegrationTests`, `DomainServices/DaprSerializationRoundTripTests`, `Events/EventPersistenceIntegrationTests`, `Events/SnapshotIntegrationTests`, `Integration/DaprETagServiceLiveSidecarTests`, `Integration/NamedProjectionDispatchLiveSidecarTests`, `Integration/ProjectionDeliveryCutoverLiveSidecarTests`, `Integration/ProjectionEraseLiveSidecarTests`, `Integration/ReadModelBatchLiveSidecarTests`. This matches the Dev Notes inventory exactly. `grep -rn 'LiveSidecar'` and a filename search for `DaprTestContainerFixture`/`DaprTestContainerCollection` over `tests/Hexalith.EventStore.Server.Tests` returned zero matches. `TombstoningLifecycleSentinelTests` and `ETagActorIntegrationTests` remain in `Server.Tests` untagged with `LiveSidecar` (the latter carries `Category=Integration`/`Tier=2` only), confirming the intentional deterministic sentinel/mock examples.
- 2026-07-18: Task 2 (AC2) re-verified. `.github/workflows/ci.yml` calls `Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main` with `unit-test-projects` including `tests/Hexalith.EventStore.Server.Tests` and no `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` entry; it supplies no `integration-test-projects` input. In the shared `domain-ci.yml`, the "Unit tests (Tier 1)" step runs `dotnet test "$proj" --no-build --configuration Release` with no `--filter`, and the "Install and initialize Dapr" step is gated `if: inputs.integration-test-projects != ''`, so DAPR is never initialized for the deterministic job. `.github/workflows/release.yml` triggers only on `workflow_run: workflows: [CI]` with `conclusion == 'success' && event == 'push' && github.sha == github.event.workflow_run.head_sha`, and has no reference to `Integration Tests`.
- 2026-07-18: Task 3 (AC3) re-verified. `.github/workflows/integration.yml`'s `live-sidecar` job runs `Github/dapr-init` then `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/` with no `--filter`, uploads TRX + Cobertura coverage via `actions/upload-artifact` with `if: always()` and `if-no-files-found: error`, declares `on: push/pull_request/workflow_dispatch`, and uses its own `concurrency: group: integration-${{ github.ref }}`. `release.yml` has no dependency on this workflow (confirmed under Task 2).
- 2026-07-18: Task 4 (AC4) re-verified. `DaprTestContainerFixture.VerifyPrerequisitesAsync` checks Redis (`6379`), placement (`50005`/`6050`), and scheduler (`50006`/`6060`) TCP reachability before `StartDaprSidecar`, raising `InvalidOperationException` with an explicit "Have you run 'dapr init'?" message on failure (environment-blocker classification, not a product failure). `WaitForDaprHealthAsync` retries the outbound health endpoint for up to `HealthTimeoutSeconds=60`; `WarmUpActorRuntimeAsync` retries an `IETagActor` regenerate/read-back round-trip for up to `WarmUpTimeoutSeconds=45`. `GetAggregateActorStateJsonAsync` reads persisted Redis actor-state hashes directly (with bounded retry) and is consumed by `EventPersistenceIntegrationTests`, `ReadModelBatchLiveSidecarTests`, `ProjectionEraseLiveSidecarTests`, and `NamedProjectionDispatchLiveSidecarTests` for durable read-back assertions.
- 2026-07-18: Task 6 (AC6) runtime validation executed on this VM. `scripts/generated-api-smoke-preflight.sh` confirmed the local Docker/DAPR environment checks, then returned its documented exit `3` (`topology-not-running`) because the full Aspire AppHost was not running; this is a recorded Story 3.10 topology classification, not a successful full-topology preflight. The dedicated `LiveSidecar.Tests` fixture does not require that AppHost: it separately proved Redis on `6379`, placement on `50005`, scheduler on `50006`, and `daprd` 1.18.1 readiness before starting its own sidecar. Exact commands and results:
  - `dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false` — succeeded (up-to-date).
  - `dotnet build Hexalith.EventStore.slnx --no-restore --configuration Release -warnaserror -p:UseHexalithProjectReferences=false` — **Build succeeded, 0 Warning(s), 0 Error(s)**.
  - `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --no-build --configuration Release -p:UseHexalithProjectReferences=false` (no DAPR running for this step) — **Passed! Failed: 0, Passed: 2626, Skipped: 25, Total: 2651**, duration 4m14s. No trait filter was passed.
  - `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --no-build --configuration Release -p:UseHexalithProjectReferences=false` (live daprd/Redis/placement/scheduler) — **Passed! Failed: 0, Passed: 44, Skipped: 0, Total: 44**, duration 4s. No trait filter was passed.
  - Persisted-state evidence: `EventPersistenceIntegrationTests` asserts durable Redis sequence/read-back state via `GetCurrentSequenceAsync` (backed by the fixture's `GetAggregateActorStateJsonAsync` Redis hash read), not only HTTP/mock outcomes, satisfying the AC6/AC4 durable-evidence requirement.
  - No `Category!=LiveSidecar` / `Category=LiveSidecar` filter was used for either project; lane separation was exercised purely through physical project selection, matching the guardrail.
- 2026-07-18: Task 7 (AC7) confirmed with `git diff --name-status 4fc14c00386bf8a403a633612d36fd8842be990e..f180c5fdda59bf1914429bb369234fabf7ce33de`, not `git status`. The complete review baseline spans 17 paths across three commits and is a mixed range: Story 3.1 owns this story file, its sprint-status transition, and the approved course-correction proposal; companion changes cover Story 3.5 planning, Story 1.20 guardrail tests, synchronized Copilot guidance, and two submodule advances. The complete diff contains no workflow, test-project split, DAPR-threshold, two-writer-race, or Story 7.10/7.12 runtime change. Code review separately ratified the FrontComposer and Tenants gitlinks and required focused source-mode evidence.
- 2026-07-18: Administrator ratified all present bumps during code review. After later workspace advances, the final observed targets are `references/Hexalith.Builds` `e64ae34e` -> `14ef97cf`, `references/Hexalith.FrontComposer` `06b39738` -> `5c284c89`, and `references/Hexalith.Tenants` `fbff4649` -> `2d85e35a`. The accepted boundary preserves those pointers and requires source-mode validation, including direct observation of the adopted Tenants API launch profile.
- 2026-07-18: Code-review validation results: package-mode `Contracts.Tests` build succeeded with 0 warnings/errors; direct xUnit runs passed `ProofPacketValidatorIntegrityTests` 1/1 and `CommitMessagePolicyTests` 11/11. Fresh Release/source-mode restore and AppHost.Tests build at Builds `ff721456` and Tenants `2d85e35a` succeeded with 0 warnings/errors; direct `TenantsApiLaunchSettingsTests` run passed 4/4, including the ratified profile. The subsequent accepted Builds `14ef97cf` release commit changes only `CHANGELOG.md`, leaving those build inputs unchanged. FrontComposer's focused Shell.Tests restore and `--no-restore` build at `5c284c89` both stopped before compilation with `NU1902` promoted to error for `AngleSharp` 1.4.0 (`GHSA-pgww-w46g-26qg`). NuGet audit and warnings-as-errors remained enabled, so the FrontComposer evidence item stays open.
- 2026-07-18: Final package-mode safety restore and full Release solution build succeeded with **0 Warning(s), 0 Error(s)** using `UseHexalithProjectReferences=false`; the workspace was not left in source-reference mode.

### Completion Notes List

- Story 3.1 itself made no product, test, workflow, or shared-guidance change. Its full baseline range is mixed and includes separately reviewed Story 3.5/1.20 planning and guardrail work, synchronized Copilot guidance, and ratified submodule advances.
- The Story 3.1 planning contract now matches the dedicated live-sidecar topology.
- All seven tasks (AC1-AC7) are verified against the current shipped topology: 14 `LiveSidecar`-tagged classes live only under `Server.LiveSidecar.Tests` with `[Collection("DaprTestContainer")]`; `ci.yml`/`domain-ci.yml` run `Server.Tests` unfiltered with no DAPR init; `integration.yml` runs `Server.LiveSidecar.Tests` unfiltered with DAPR init and always-on TRX/coverage upload; `release.yml` gates only on same-head successful `CI`; the fixture checks Redis/placement/scheduler prerequisites, retries health/warm-up with bounded timeouts, and live tests assert persisted Redis read-back state.
- Runtime evidence captured on this VM's live local DAPR environment: Release build 0 warnings/0 errors; `Server.Tests` 2626 passed/0 failed/25 skipped/2651 total (no DAPR); `Server.LiveSidecar.Tests` 44 passed/0 failed/0 skipped/44 total (live daprd/Redis/placement/scheduler). No trait filter was used for either run.
- The baseline-to-candidate diff confirms no workflow, project-split, DAPR-threshold, or unrelated runtime change was introduced for Story 3.1; companion changes in the mixed range are recorded separately rather than hidden behind a clean working tree.
- FR17/NFR10 done-evidence reconciliation is left to the review gate per AC6/AC5. Verification moved the story to `review`; the unresolved FrontComposer review action now returns it to `in-progress`, and the story does not claim `done`.
- Code review resolved seven of eight patch findings. FrontComposer source validation remains blocked by its warnings-as-errors NuGet vulnerability gate, so Story 3.1 returns to `in-progress` with that action item open.

### File List

Story 3.1-owned files:

- `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md` (modified: approved course correction, verification evidence for AC1-AC7, and completion notes)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified: Story 3.1 status returned to in-progress while one review action remains open)
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18-story-3-1-live-sidecar-topology.md` (added: approved course-correction proposal)

The complete review range and later accepted workspace advances also contain companion Story 3.5 planning, Story 1.20 guardrail tests, the synchronized Copilot-entry-point correction, and the ratified Builds/FrontComposer/Tenants gitlink changes. Those paths are not represented as Story 3.1 implementation deliverables.

Code-review action files:

- `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md`
- `_bmad-output/implementation-artifacts/3-5-shared-package-catalog-and-source-package-reference-modes.md`
- `_bmad-output/implementation-artifacts/epic-3-context.md`
- `_bmad-output/implementation-artifacts/spec-1-20-add-github-approval-login.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/prd.md`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18-story-3-5-reconciliation.md`
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/TenantsApiLaunchSettingsTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ProofPacketValidatorIntegrityTests.cs`

### Change Log

- 2026-07-18: Began verification and recorded a correct-course blocker caused by the later dedicated live-sidecar test-project split; no task was marked complete.
- 2026-07-18: Applied the approved Story 3.1 specification correction for dedicated project paths, unfiltered lane assertions, and current validation commands; runtime verification remains pending.
- 2026-07-18: Completed runtime verification for Tasks 1-4, 6, and 7 against the dedicated live-sidecar topology; captured exact pass/fail/skip counts and persisted-state evidence; all tasks now checked and Status moved to `review`.
- 2026-07-18: Code review applied seven patches and retained one open FrontComposer validation blocker (`NU1902`/AngleSharp 1.4.0); Story 3.1 returned to `in-progress` rather than suppressing NuGet audit or weakening warnings-as-errors.
