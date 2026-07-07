---
baseline_commit: fc0f1de8bd3ed30a2b949d04eea56bde9b49e645
created: 2026-07-07
story_key: 3-1-re-tier-live-sidecar-tests-from-release-gate
epic: "Epic 3 - Release And Repository Reliability"
requirements: FR17
governing_nfr: NFR10
story_type: verification-and-reconciliation
correct_course: >-
  FR17's implementation shipped in PR #271 (commit 13320952, 2026-06-22) and the workflows were
  later consolidated (commit 84ac5b41). This story is re-scoped from IMPLEMENT to
  VERIFY-AND-RECONCILE per the Correct-Course Story Rewrite Gate. Original epic AC wording that
  pointed at "the release workflow" running Server.Tests is superseded: the deterministic gate now
  lives in ci.yml.
source_files:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-ci-release-retier.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md
  - _bmad-output/implementation-artifacts/3-8-generated-api-dapr-aspire-smoke-preflight.md
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/epic-2-retro-2026-07-07.md
  - _bmad-output/implementation-artifacts/spec-integration-e2e-test-recovery.md
  - docs/ci.md
  - .github/workflows/ci.yml
  - .github/workflows/release.yml
  - .github/workflows/integration.yml
  - tests/Hexalith.EventStore.Server.Tests/Fixtures/DaprTestContainerFixture.cs
  - tests/Directory.Build.props
  - CLAUDE.md
---

# Story 3.1: Re-Tier Live-Sidecar Tests From Release Gate

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

<!-- CORRECT-COURSE REWRITE (2026-07-07):
     FR17's implementation already shipped in PR #271 (commit 13320952, 2026-06-22) under
     sprint-change-proposal-2026-06-22-ci-release-retier.md, and the CI/release workflows were
     later consolidated (commit 84ac5b41 "chore(workflows): consolidate ci and release
     automation"). Both changes landed BEFORE this story file existed, and sprint-status.yaml still
     listed 3-1 as `backlog`. Per the Correct-Course Story Rewrite Gate this story is re-scoped from
     IMPLEMENT to VERIFY-AND-RECONCILE. The original epic Acceptance Criteria (epics.md:782-802) are
     preserved verbatim under "Original Epic Acceptance Criteria" below and each is mapped to its
     current, verified implementation location. The only original AC wording that is now factually
     WRONG — "the release workflow runs Server.Tests … filters out Category=LiveSidecar" — is marked
     SUPERSEDED: the deterministic filter now lives in ci.yml's server-inprocess-tests job;
     release.yml runs no tests. Do NOT re-implement the shipped design. -->

## Story

As a **release maintainer**,
I want **to verify that live DAPR-sidecar tests already run outside the per-push release gate in a dedicated integration lane, and to reconcile the stale project documentation and status ledger with that shipped reality**,
so that **FR17/NFR10 lane separation is provably intact after the CI/release consolidation, and CLAUDE.md, sprint-status, and FR17 done-evidence stop contradicting the codebase**.

## Story Context

**This is a verification-and-reconciliation story, not a greenfield implementation.** FR17 ("Live DAPR sidecar tests must be tagged and removed from the per-push release gate, then run in a dedicated integration workflow with sidecar warm-up and readiness retry" — `prd.md:137`) was delivered end-to-end in **PR #271 / commit `13320952` (`fix(ci): re-tier live-daprd integration tests off the release gate`, 2026-06-22)** via `sprint-change-proposal-2026-06-22-ci-release-retier.md`. The CI/release automation was then **consolidated** in commit `84ac5b41` (`chore(workflows): consolidate ci and release automation`), which moved the deterministic-gate filter out of `release.yml` and into `ci.yml`, and made `release.yml` a `workflow_run` consumer of CI success that runs no tests.

The current two-lane design (verified at baseline `fc0f1de8`) is:

- **Release gate = `ci.yml`.** The blocking `server-inprocess-tests` job runs `Server.Tests --filter "Category!=LiveSidecar"` and installs **no** DAPR (`ci.yml:179-186`). `release.yml` is `workflow_run`-triggered on CI success and only runs `npx semantic-release` — zero `dotnet test`, zero DAPR (`release.yml:3-7,18,64-68`).
- **Dedicated live lane = `integration.yml`.** A separate `live-sidecar` job provisions DAPR (`dapr-init`, `integration.yml:55-58`) and runs `Server.Tests --filter "Category=LiveSidecar"` (`integration.yml:60-62`). It is **not** a dependency of `release.yml`, so live-sidecar failures are visible (red job) without blocking semantic-release.
- **9 live-sidecar classes** carry `[Trait("Category", "LiveSidecar")]` + `[Collection("DaprTestContainer")]`; the Tier-1 reflection check `TombstoningLifecycleSentinelTests` is intentionally **not** tagged.
- **Shared fixture warm-up + readiness retry** live in `DaprTestContainerFixture` (`WaitForDaprHealthAsync`, `WarmUpActorRuntimeAsync`, prerequisite preflight).

Two artifacts still contradict this shipped reality and are the **concrete deliverables** of this story:

1. **CLAUDE.md is stale (two places).** It claims `Server.Tests` "does not build due to CA2007 warnings treated as errors" (`CLAUDE.md:91`) and "is currently excluded from the baseline because of the pre-existing CA2007 build failure" (`CLAUDE.md:231`). Both are **false**: `tests/Directory.Build.props:10` adds `CA2007` to `NoWarn`, the project builds cleanly with `-warnaserror`, and it is a **blocking** CI gate (`ci.yml:147-197`). The proposal itself flagged "Reconcile CLAUDE.md" as an open follow-up (`sprint-change-proposal-2026-06-22-ci-release-retier.md:189`).
2. **`sprint-status.yaml` listed `3-1` as `backlog`** while its code was merged — reconciled by this story's completion.

**Governing constraint:** NFR10 — "CI/CD must separate deterministic release-gate tests from live-sidecar/integration tests while preserving live-sidecar coverage in a dedicated lane" (`prd.md:208`; traceability `NFR10 → 3.1, 7.4`, `prd.md:340`). This story owns the FR17 half; **Story 7.4 owns Tier-3 integration-lane recovery** — do not cross that boundary.

Source of truth:

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-ci-release-retier.md` — the origin proposal; CP-1..CP-5, the 9-class list, and the CLAUDE.md reconciliation follow-up.
- `_bmad-output/planning-artifacts/epics.md:774-805` — Story 3.1 original ACs and the Story 3.8 companion note.
- `_bmad-output/planning-artifacts/prd.md:137,208,340` — FR17, NFR10, traceability.
- `_bmad-output/planning-artifacts/architecture.md` — AD-9 (topology changes together), AD-11 (release manifest-governed), AD-12 (persisted evidence for high-risk tests).
- `docs/ci.md:11-16,37-50,52-74` — the two-lane design already documented accurately (do not contradict it).

## Acceptance Criteria

> **Verification stance:** each AC is satisfied by *observing and recording evidence* that the shipped state matches the requirement at the current baseline. Only make code/doc changes where an AC calls for a reconciliation edit (AC5, AC6) or where verification surfaces a genuine regression (AC7). Do **not** re-implement the shipped design.

**AC1 — Live-sidecar classes are trait-tagged and correctly scoped.**
**Given** the Server.Tests project,
**When** live-sidecar test classes are enumerated,
**Then** exactly these 9 classes carry `[Trait("Category", "LiveSidecar")]` **and** `[Collection("DaprTestContainer")]`: `Actors/ActorConcurrencyConflictTests`, `Actors/ActorTenantIsolationTests`, `Actors/AggregateActorIntegrationTests`, `Actors/TombstoningLifecycleTests`, `Commands/CommandRoutingIntegrationTests`, `DomainServices/DaprSerializationRoundTripTests`, `Events/EventPersistenceIntegrationTests`, `Events/SnapshotIntegrationTests`, `Integration/DaprETagServiceLiveSidecarTests`,
**And** the Tier-1 reflection check `Actors/TombstoningLifecycleSentinelTests` remains **untagged**,
**And** no non-sidecar test (those using a mocked `Substitute.For<IActorProxyFactory>()` inside `WebApplicationFactory`, e.g. `Integration/ETagActorIntegrationTests`) is tagged `LiveSidecar`.

**AC2 — The release gate excludes live-sidecar tests and installs no DAPR. [supersedes original AC2 wording]**
**Given** the release path (the CI workflow whose success gates `release.yml`),
**When** its test steps are inspected,
**Then** the blocking `server-inprocess-tests` job runs `Server.Tests` with `--filter "Category!=LiveSidecar"` (`ci.yml:179-186`),
**And** no job on the release path runs `dapr init`, placement, or scheduler (verified across `ci.yml` and `release.yml`),
**And** `release.yml` runs zero `dotnet test` and is `workflow_run`-gated on CI success with `event == 'push'` (`release.yml:3-7,18,64-68`).
> Original epic AC said "the *release workflow* … executes Server.Tests … filters out `Category=LiveSidecar`". That wording is SUPERSEDED by the `84ac5b41` consolidation: the filter lives in `ci.yml`, and `ci.yml` success is the release trigger. The **intent** (release path excludes live-sidecar tests, installs no DAPR) is preserved and is what AC2 verifies.

**AC3 — Live-sidecar tests run in a dedicated lane that cannot block a release.**
**Given** `.github/workflows/integration.yml`,
**When** the dedicated `live-sidecar` job is inspected,
**Then** it provisions DAPR via the shared `dapr-init` composite action (`integration.yml:55-58`) and runs `Server.Tests --filter "Category=LiveSidecar"` (`integration.yml:60-62`),
**And** it is triggered on push/PR to `main` + `workflow_dispatch` with its own concurrency group,
**And** it is **not** referenced as a dependency by `release.yml` (semantic-release depends only on the CI workflow), so a red live-sidecar job leaves releases unblocked.

**AC4 — The shared DAPR fixture warms up and retries readiness before assertions.**
**Given** `tests/Hexalith.EventStore.Server.Tests/Fixtures/DaprTestContainerFixture.cs`,
**When** its initialization path is inspected,
**Then** it performs a prerequisite reachability preflight (Redis, placement, scheduler) that fails with a "Have you run 'dapr init'?"-style message when the local control plane is missing,
**And** `WaitForDaprHealthAsync` polls the sidecar `healthz/outbound` endpoint with bounded retry (`HealthTimeoutSeconds = 60`),
**And** `WarmUpActorRuntimeAsync` performs a throwaway ETag actor round-trip (`RegenerateAsync` → `GetCurrentETagAsync`) with bounded retry (`WarmUpTimeoutSeconds = 45`) so placement dissemination, actor activation, and the Redis round-trip are hot before any live-sidecar assertion runs.

**AC5 — CLAUDE.md is reconciled to the shipped build/test reality.**
**Given** the two stale `Server.Tests` claims in `CLAUDE.md` (the "does not build due to CA2007" build-comment at `:91` and the "excluded from the baseline … pre-existing CA2007 build failure" line at `:231`),
**When** CLAUDE.md is updated,
**Then** both are corrected to state that `Server.Tests` builds cleanly (CA2007 is in `tests/Directory.Build.props` `NoWarn`) and is a **blocking** CI gate (`ci.yml` `server-inprocess-tests`, `Category!=LiveSidecar`), with live-sidecar tests running in the dedicated `integration.yml` lane,
**And** no other CLAUDE.md guidance is weakened or reworded beyond this reconciliation.

**AC6 — FR17/NFR10 lane separation is validated with recorded evidence.**
**Given** the validation commands in Dev Notes,
**When** they are run at the current baseline,
**Then** `Server.Tests` builds cleanly under `-warnaserror` and the deterministic subset (`--filter "Category!=LiveSidecar"`) passes locally,
**And** the live-sidecar subset (`--filter "Category=LiveSidecar"`) is either run against a live sidecar with results recorded, or its environment is classified as `blocked` using the Story 3.8 preflight / the fixture's own prerequisite preflight (a missing local control plane is an environment blocker, **not** a product failure — AD-12/NFR16),
**And** the Dev Agent Record captures pass/fail counts and, for any live-sidecar assertion, persisted-state evidence rather than status-code-only signal (R2-A6 / NFR16),
**And** the FR17 done-evidence and `sprint-status.yaml` entry are reconciled to reflect that FR17 is satisfied.

**AC7 — No scope creep; adjacent work stays deferred.**
**Given** the FR17/FR34 boundary,
**When** this story is implemented,
**Then** no Tier-3 Aspire-in-CI lane is added (that is Story 7.4 / FR34; `integration.yml:75-78` deferral comment stays),
**And** no new "two-writer race" live-sidecar test is added (that belongs to Epic 4 / FR31 — `epics.md:1145,1159`),
**And** the shared-placement fixed-actor-name isolation gap and `HotReloadTests` DCP cascade remain tracked in `deferred-work.md` (Story 7.4 territory), not fixed here,
**And** the existing warm-up/readiness thresholds and the 9-class tag set are not changed unless AC1/AC4 verification proves a defect.

### Original Epic Acceptance Criteria (preserved for traceability — `epics.md:782-802`)

1. Live-`daprd` test classes marked with a `LiveSidecar` trait; non-live-sidecar tests remain in the deterministic release gate. → **verified by AC1** (satisfied).
2. Release workflow runs Server.Tests, filters out `Category=LiveSidecar`, and does not install/initialize DAPR solely for the release gate. → **superseded wording; intent verified by AC2** (filter is in `ci.yml`, not `release.yml`).
3. Dedicated integration workflow provisions DAPR and executes `Category=LiveSidecar` in its own lane; failures visible without blocking semantic-release. → **verified by AC3** (satisfied).
4. Shared DAPR fixture performs readiness retry and warm-up actor round trips so placement/activation/Redis are hot before assertions. → **verified by AC4** (satisfied).

## Tasks / Subtasks

- [ ] **Task 1 — Verify trait tagging and scoping (AC1).**
  - [ ] Confirm the 9 classes above each carry `[Trait("Category", "LiveSidecar")]` and `[Collection("DaprTestContainer")]`.
  - [ ] Confirm `TombstoningLifecycleSentinelTests` is untagged and `Integration/ETagActorIntegrationTests` (mocked `IActorProxyFactory`) is `Category=Integration`/`Tier=2`, not `LiveSidecar`.
  - [ ] Record the enumeration as evidence.
- [ ] **Task 2 — Verify the release gate (AC2).**
  - [ ] Confirm `ci.yml` `server-inprocess-tests` runs `--filter "Category!=LiveSidecar"` and installs no DAPR.
  - [ ] Confirm `release.yml` runs no `dotnet test`, installs no DAPR, and is `workflow_run`-gated on CI success (`event == 'push'`).
  - [ ] Confirm no DAPR install/placement/scheduler step exists anywhere on the release path.
- [ ] **Task 3 — Verify the dedicated live lane (AC3).**
  - [ ] Confirm `integration.yml` provisions DAPR (`dapr-init`) and filters `Category=LiveSidecar`.
  - [ ] Confirm `release.yml` does not reference the Integration Tests workflow (no dependency edge → non-blocking).
- [ ] **Task 4 — Verify fixture warm-up + readiness retry (AC4).**
  - [ ] Read `DaprTestContainerFixture.cs`; confirm prerequisite preflight, `WaitForDaprHealthAsync` (60s), and `WarmUpActorRuntimeAsync` (45s throwaway ETag round-trip with bounded retry).
- [ ] **Task 5 — Reconcile CLAUDE.md (AC5).**
  - [ ] Update `CLAUDE.md:91` (Build & Test comment) and `CLAUDE.md:231` (project-structure note) to the shipped reality: Server.Tests builds cleanly (CA2007 in `tests/Directory.Build.props` `NoWarn`); it is a blocking CI gate (`Category!=LiveSidecar`); live-sidecar tests run in the dedicated `integration.yml` lane.
  - [ ] Do not weaken any other CLAUDE.md guidance.
- [ ] **Task 6 — Validate and record evidence (AC6).**
  - [ ] Run the Dev Notes validation commands; record build result and deterministic (`Category!=LiveSidecar`) pass counts.
  - [ ] For the live-sidecar subset: run against a live sidecar (VM bootstrap in Dev Notes) and record results **with persisted-state evidence**, or classify the environment as `blocked` via the Story 3.8 preflight / fixture preflight. Never treat a missing control plane as a product failure.
  - [ ] Reconcile FR17 done-evidence; note satisfied-by-#271 + consolidation reconciliation in the Dev Agent Record.
- [ ] **Task 7 — Enforce scope boundaries (AC7).**
  - [ ] Confirm no Tier-3 Aspire-in-CI lane, no new two-writer test, and no fixed-actor-name/HotReload fix were introduced.

## Dev Notes

### Top Guardrails

- **DO NOT re-implement.** FR17 shipped in PR #271 (`13320952`). This story verifies the shipped design against the current consolidated topology and reconciles stale docs/status. Re-tagging classes, rewriting `ci.yml`/`integration.yml`/`release.yml`, or changing warm-up thresholds is out of scope unless verification proves a genuine regression from baseline `fc0f1de8`.
- **The release gate is `ci.yml`, not `release.yml`.** Consolidation commit `84ac5b41` moved the `Category!=LiveSidecar` filter into `ci.yml`'s `server-inprocess-tests` job; `release.yml` is a `workflow_run` consumer that runs `npx semantic-release` only. Read every "release workflow" reference in the original AC as "the CI workflow that gates release".
- **NFR10 lane boundary:** this story owns FR17 (live-sidecar off the release gate + dedicated lane). **Story 7.4 owns Tier-3 integration-lane recovery / Aspire-in-CI.** Do not add a Tier-3 lane here (`integration.yml:75-78` deferral stays).
- **Persisted-evidence rule (AD-12 / R2-A6 / NFR16):** any live-sidecar result you record must rest on persisted Redis/state-store/CloudEvent evidence, not on `202`/`200`/mock counts. The existing live-sidecar tests already assert persisted state — preserve that when recording evidence.
- **Environment ≠ defect:** a missing local DAPR control plane (placement/scheduler/redis) is a `blocked` classification, not a product failure. Use `scripts/generated-api-smoke-preflight.sh` (Story 3.8) and the fixture's own prerequisite preflight to classify before asserting.
- **ULID rule** (project-wide): never `Guid.TryParse` a `messageId`/`correlationId`/`aggregateId`/`causationId` — this repo's ids are ULIDs. (No new id parsing is expected in this story, but do not introduce any.)
- **Keep `TombstoningLifecycleSentinelTests` untagged** — it is a Tier-1 reflection check that must run in the deterministic gate.

### Current Code State Read During Story Creation (baseline `fc0f1de8`, all verified)

**Workflows (`.github/workflows/`):**
- `ci.yml:147-197` — `server-inprocess-tests` (BLOCKING): builds `Hexalith.EventStore.slnx` with `-warnaserror` (`:178`), then `dotnet test tests/Hexalith.EventStore.Server.Tests/ --no-build --configuration Release --filter "Category!=LiveSidecar" …` (`:179-186`). No DAPR anywhere in `ci.yml`.
- `ci.yml:205-270` — `non-blocking-tests` (`continue-on-error: true`): advisory suites only; comment `:199-204` explains Server.Tests and IntegrationTests are excluded here.
- `release.yml:3-7` — `workflow_run` on `[CI]`, `types: [completed]`, `branches: [main]`. `:18` gate `conclusion == 'success' && event == 'push'`. `:64-68` only step is `npx semantic-release`. No `dotnet test`, no DAPR.
- `integration.yml:9-18` — triggers push/PR to main + `workflow_dispatch`, own concurrency group. `:24` `DAPR_VERSION: '1.18.0'`. `:55-58` `Initialize Dapr` via `./references/Hexalith.Builds/Github/dapr-init`. `:60-62` `dotnet test … --filter "Category=LiveSidecar" …`. `:75-78` Tier-3 Aspire-in-CI deferral note. Not referenced by `release.yml`.
- `.releaserc.json` — semantic-release plugins; `@semantic-release/exec` prepareCmd packs+validates via `tools/pack-release-packages.py` / `validate-release-packages.py`; publishCmd pushes `Hexalith.EventStore.*.nupkg`. **No test runs on the publish path.**

**Live-sidecar tests (all `[Trait("Category", "LiveSidecar")]` + `[Collection("DaprTestContainer")]`):**
- `Actors/ActorConcurrencyConflictTests.cs`, `Actors/ActorTenantIsolationTests.cs`, `Actors/AggregateActorIntegrationTests.cs`, `Actors/TombstoningLifecycleTests.cs`, `Commands/CommandRoutingIntegrationTests.cs`, `DomainServices/DaprSerializationRoundTripTests.cs`, `Events/EventPersistenceIntegrationTests.cs`, `Events/SnapshotIntegrationTests.cs`, `Integration/DaprETagServiceLiveSidecarTests.cs`.
- These construct a real `new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = fixture.DaprHttpEndpoint … })` and call `CreateActorProxy<…>` against the fixture's live sidecar. `DaprETagServiceLiveSidecarTests` constructs the service with `requestTimeout: 30s` (the FR18/Story 3.2 override, which also shipped in #271).
- **Untagged on purpose:** `Actors/TombstoningLifecycleSentinelTests` (Tier-1). The ~37 files matching `ActorProxyFactory|CreateActorProxy` include many that use a mocked `Substitute.For<IActorProxyFactory>()` inside `WebApplicationFactory` (in-process, no sidecar) — e.g. `Integration/ETagActorIntegrationTests.cs` (its doc-comment says so). Those are correctly `Category=Integration`/`Tier=2`.

**Shared fixture — `tests/Hexalith.EventStore.Server.Tests/Fixtures/DaprTestContainerFixture.cs`:**
- `DaprTestContainerCollection.cs` — `[CollectionDefinition("DaprTestContainer", DisableParallelization = true)]` → `ICollectionFixture<DaprTestContainerFixture>`.
- `HealthTimeoutSeconds = 60` (`:42`), `WarmUpTimeoutSeconds = 45` (`:43`).
- `InitializeAsync` (`:88-136`): kill orphaned `daprd`; allocate 6 free ports; **prerequisite preflight** (`:280-300`, TCP-checks Redis 6379 / placement 50005 / scheduler 50006, throws "Have you run 'dapr init'?"); write temp `statestore.yaml`+`pubsub.yaml`; start in-process host with an isolated `AggregateActor` type name; start `daprd`; `WaitForDaprHealthAsync` (`:117,563-602`); `WarmUpActorRuntimeAsync` (`:129,144-176`).
- Stale-host handling note `:371` — actor calls can route to dead instances for fixed actor names. (This is the shared-placement isolation risk that stays deferred — see AC7.)

**Build-status reality — `tests/Directory.Build.props:10`:** `<NoWarn>$(NoWarn);CA2007;xUnit1051;CS0618;CS8602;CS8604;CS8600;</NoWarn>` neutralizes CA2007-as-error for all test projects. `Server.Tests` builds cleanly (`dotnet build … -warnaserror` succeeds; a fresh Release build during story creation reported 0 warnings / 0 errors). **Therefore `CLAUDE.md:91` and `CLAUDE.md:231` are factually wrong and are the concrete edit for this story (AC5).**

**Already-accurate docs (do not contradict):** `docs/ci.md:11-16,37-50,52-74` describes the two-lane design, the tier table, and the release flow correctly.

### Scope Boundaries (what NOT to do)

- No Tier-3 Aspire-in-CI lane (Story 7.4 / FR34; `prd.md:340` maps `NFR10 → 3.1, 7.4`).
- No new two-writer race live-sidecar test (Epic 4 / FR31; `epics.md:1145,1159`; `sprint-change-proposal-2026-07-04.md:235`). It does not exist yet and is not this story's job.
- No fix for the fixed-actor-name (`ProjectionActor`/`ETagActor`/`GlobalPositionActor`) shared-placement 60s-hang or the `HotReloadTests` DCP cascade — both stay in `deferred-work.md` (see MEMORY note `tier3-integration-test-constraints`).
- No change to warm-up/readiness thresholds or the 9-class tag set unless verification proves a defect.

### Validation Commands

Run per project (never solution-level `dotnet test`; `.slnx` for restore/build only). Use `-p:UseHexalithProjectReferences=false` to match CI (package/Release mode); rerun `dotnet restore` when switching modes.

```bash
# Build the release gate's test project the way CI does (must succeed, 0 warnings under -warnaserror)
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj \
  --configuration Release -warnaserror -p:UseHexalithProjectReferences=false

# Deterministic release-gate subset (no DAPR needed) — must pass
dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release \
  --filter "Category!=LiveSidecar" -p:UseHexalithProjectReferences=false

# Live-sidecar lane — REQUIRES a live control plane. Bootstrap first (VM/slim mode):
#   sudo dockerd &>/tmp/dockerd.log & ; sudo chmod 666 /var/run/docker.sock
#   $HOME/.dapr/bin/placement --port 50005 &
#   $HOME/.dapr/bin/scheduler --port 50006 --etcd-data-dir /tmp/dapr-scheduler-data &
#   (or `dapr init`). Then classify environment with the Story 3.8 preflight before asserting:
#   scripts/generated-api-smoke-preflight.sh
dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release \
  --filter "Category=LiveSidecar" -p:UseHexalithProjectReferences=false
```

Expected baselines from #271 (for comparison, not a hard gate): release-gate subset ≈ 2168 passed / 0 failed / 25 skipped; live-sidecar subset = 28 passed / 0 failed. If the live subset cannot run locally, record `blocked` with the preflight classification — do not mark AC6 failed for a missing control plane.

### Implementation Hints

- The only expected file edit is **CLAUDE.md** (AC5). Everything else is observe-and-record. If Task 2/3/4 verification uncovers a real regression (e.g. the consolidation silently reintroduced a DAPR step on the release path, or dropped the `--filter`), fix that **narrow** gap and note it — but at baseline `fc0f1de8` no such regression exists.
- When recording live-sidecar evidence, cite persisted state (e.g. the Redis hash for the aggregate actor via the fixture's `GetAggregateActorStateJsonAsync` read-retry path), not just the test's green/red.
- Clean any stale DAPR placement before running the live lane locally (MEMORY `tier3-integration-test-constraints`): a shared/long-lived placement with dead fixed-name actor hosts causes ~60s hangs.
- Concurrency caution (MEMORY `concurrent-bmad-loop-git`): a parallel auto-dev loop may auto-commit/push to `main`; check refs before committing the CLAUDE.md edit, and branch (`fix/...` or `docs/...`) rather than committing to `main`.

### References

- [Source: _bmad-output/planning-artifacts/prd.md#FR17] and `#NFR10` — requirement + governing NFR (`:137,208,340`).
- [Source: _bmad-output/planning-artifacts/epics.md#Story-3.1] (`:774-805`) — original ACs + Story 3.8 companion note.
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-ci-release-retier.md] — CP-1..CP-5, 9-class list, validation baselines, "Reconcile CLAUDE.md" follow-up (`:189`).
- [Source: _bmad-output/planning-artifacts/architecture.md#AD-9] topology-changes-together, `#AD-11` manifest-governed release, `#AD-12` persisted-evidence.
- [Source: docs/ci.md] — two-lane CI/release documentation (`:11-16,37-50,52-74`).
- [Source: .github/workflows/ci.yml:147-197] release gate; [Source: .github/workflows/release.yml:3-7,18,64-68]; [Source: .github/workflows/integration.yml:55-78].
- [Source: tests/Hexalith.EventStore.Server.Tests/Fixtures/DaprTestContainerFixture.cs:42-176,280-300,563-602] warm-up/readiness.
- [Source: tests/Directory.Build.props:10] CA2007 NoWarn (build-status truth).
- [Source: CLAUDE.md:91,231] the stale text to reconcile.
- [Source: _bmad-output/implementation-artifacts/3-8-generated-api-dapr-aspire-smoke-preflight.md] companion preflight (`scripts/generated-api-smoke-preflight.sh`) for environment classification.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] fixed-actor-name isolation + HotReload cascade (stay deferred).

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Implementation Plan / Decisions

### Debug Log References

### Completion Notes List

### File List

### Change Log
