# Sprint Change Proposal — Re-tier Live-Sidecar Tests off the Release Gate (Fix CI/CD)

- **Date:** 2026-06-22
- **Author:** Administrator (via BMAD Correct Course)
- **Project:** Hexalith.EventStore
- **Mode:** Incremental
- **Change classification:** **Minor** (CI/CD reliability remediation; direct Developer implementation, no epic/story restructuring)
- **Status:** Implemented locally — awaiting final approval to commit/push
- **Builds on:** `sprint-change-proposal-2026-06-21.md` (Epic D — REST Controller Source Generator, in progress)

---

## 1. Issue Summary

### Problem statement

The **`Release`** GitHub Actions workflow is red on every push to `main`, blocking semantic-release
(versioning, NuGet publish, GitHub Release). This is a **CI/CD reliability defect, not a product
bug**: the restored release pipeline runs live-`daprd` integration tests in the same step as fast
unit tests, and those tests flake on cold/loaded CI runners.

### How it was discovered

Direct stakeholder request ("fix CI/CD") following restoration of the release pipeline
(`2c6a1838` → `fc64395b`). The pipeline had been removed in `76368f95` and re-added; the team
iteratively fixed build/compile errors, leaving one deterministic test failure as the final blocker.

### Evidence

- **4/4 recent `Release` runs failed.** Build/compile errors were resolved across the first runs;
  the last two failed on a single test.
- **Deterministic failure** (runs `27940392568`, `27939959590`):
  `Hexalith.EventStore.Server.Tests.Integration.DaprETagServiceLiveSidecarTests.GetCurrentETagAsync_AfterRegenerate_ReturnsPersistedETag_NotFailOpenNull`
  — `1 Failed, 2195 Passed, 25 Skipped`.
- **Passes locally** against a real `dapr init` environment (verified: 2/2 then 28/28).
- **Production code is correct.** `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs`
  already invokes the strongly-typed remoting interface — this is *not* the NRE regression the test
  guards (`sprint-change-proposal-2026-05-25-etag-actor-proxy-nre.md`).
- **Root cause:** cold-start. On a CI runner immediately after `dapr init`, the first actor
  round-trip (placement dissemination + activation + Redis state read) exceeds the service proxy's
  tight **3 s `RequestTimeout`**; `DaprETagService`'s fail-open `catch` then returns `null`, failing
  the "not fail-open null" assertion.
- **Blast radius:** not one fragile test. **9 test classes** bound to the shared
  `DaprTestContainerFixture` (live `daprd`) gate the release on every push — any of them can flake.
  This also contradicts CLAUDE.md's own tiering (Docker/Aspire-dependent integration tests are not
  part of basic unit CI).

---

## 2. Impact Analysis

- **Epic Impact:** Epic D (active) is **operationally blocked** — no releases can succeed while
  `main` is red — but its **scope is unchanged**. No epics added, removed, or resequenced.
- **Story Impact:** None. No story acceptance criteria change.
- **Artifact Conflicts:**
  - **CI/CD pipeline** (`.github/workflows/release.yml`) — must stop gating on live-sidecar tests.
  - **Testing strategy** — needs a precise discriminator to separate live-`daprd` tests from
    deterministic unit/in-process tests.
  - **CLAUDE.md** (follow-up, non-blocking) — the "Server.Tests does not build (CA2007)" note is
    **stale**: the project now builds and runs 2221 tests. Should be reconciled separately.
- **Technical Impact:** One published-package edit (`Hexalith.EventStore.Server`) making the ETag
  actor request timeout overridable, with the production default (3 s) preserved.

---

## 3. Recommended Approach

**Selected (Option 1 — Direct Adjustment): Re-tier + harden.** Move the live-`daprd` integration
tests off the per-push release path into a dedicated integration workflow (real DAPR + Docker), and
harden the integration suite so it is reliable on its own lane.

- **Effort:** Low–Medium · **Risk:** Low · **Timeline impact:** Immediate (unblocks releases).
- **Rationale:** Restores green/fast releases now, **preserves** the valuable end-state integration
  assertions (R2-A6), and **aligns with CLAUDE.md tiering**. Rejected alternatives: rollback (would
  re-remove the just-restored pipeline); MVP review (no product-scope impact).
- Stakeholder confirmed **DAPR + Docker are available** for the dedicated integration job.

---

## 4. Detailed Change Proposals (all implemented & validated locally)

### CI/CD

**CP-1 — `.github/workflows/release.yml` (release gate runs only deterministic tests)**
- Renamed step → `Run Unit & In-Process Tests (release gate)`.
- `Server.Tests` line now runs with `--filter "Category!=LiveSidecar"`.
- Removed the now-unnecessary `Install Dapr CLI` + `Initialize Dapr` steps and the `DAPR_VERSION`
  env (no remaining gate test needs a live sidecar — the non-sidecar integration tests mock
  `IActorProxyFactory`).

**CP-2 — new `.github/workflows/integration.yml` (dedicated live-sidecar job)**
- Triggers: push to `main`, PRs to `main`, and `workflow_dispatch`; separate concurrency group.
- Provisions DAPR (`dapr init`, retried) and runs `Server.Tests --filter "Category=LiveSidecar"`.
- **Not** a dependency of the `release` job → never gates a release.
- **Tier-3 deferred:** the `IntegrationTests` project starts a full Aspire `DistributedApplication`
  (`Projects.Hexalith_EventStore_AppHost`) which `dapr init` alone does not satisfy — it ran for
  10+ minutes in the first PR CI run. Running it reliably needs a dedicated Aspire-in-CI setup; it
  is **not** included here (the original release pipeline never ran it either) and is tracked as a
  follow-up.

### Tests

**CP-3 — `[Trait("Category", "LiveSidecar")]` on the 9 live-sidecar classes**
- `Events/EventPersistenceIntegrationTests`, `Events/SnapshotIntegrationTests`,
  `Actors/ActorTenantIsolationTests`, `Actors/ActorConcurrencyConflictTests`,
  `Actors/TombstoningLifecycleTests`, `Actors/AggregateActorIntegrationTests`,
  `Commands/CommandRoutingIntegrationTests`, `Integration/DaprETagServiceLiveSidecarTests`,
  `DomainServices/DaprSerializationRoundTripTests`.
- `TombstoningLifecycleSentinelTests` (Tier-1 reflection check) intentionally **not** tagged.

**CP-4 — harden `DaprTestContainerFixture` with a warm-up + readiness retry**
- After sidecar health, performs a throwaway ETag actor round-trip with bounded retry
  (`WarmUpTimeoutSeconds = 45`) so placement dissemination, activation, and the Redis round-trip are
  hot before any live-sidecar test asserts. Hardens the whole suite, not just the ETag test.

### Release packaging (follow-on, discovered after merge)

**CP-6 — `.releaserc.json`: scope the release pack to EventStore's own packages**
- Merging CP-1…CP-5 turned the release **test gate green**, which exposed a **separate, pre-existing**
  failure in semantic-release's `prepareCmd`: it ran a **whole-solution** `dotnet build` + `dotnet
  pack`, dragging in **submodule** projects that fail under EventStore's root rules — `IDE0065`
  (build) in `Hexalith.PolymorphicSerializations` and `NU5118` (duplicate README at pack) in
  `Hexalith.Commons.*` — and would have **published submodule packages to NuGet under EventStore's
  version**.
- Root cause: submodule projects import EventStore's root `Directory.Build.props` *and* the shared
  `Hexalith.Builds` props; building/packing the whole solution applies EventStore's analyzer/pack
  rules to code that isn't written to them. EventStore's own test gate never hit this (it builds
  individual projects and references `PolymorphicSerializations` as a *package*, not source).
- Fix: `prepareCmd` now builds + packs **only the 12 EventStore packable projects** (loop over
  `Contracts, Client, Server, SignalR, Testing, Testing.Integration, Aspire, ServiceDefaults,
  DomainService, Admin.Abstractions, Admin.Cli, Admin.Server`) instead of the whole solution.
  `PolymorphicSerializations` is never built from source; Commons libs are referenced as package
  dependencies, not packed.

**CP-7 — proper packaging-dependency fixes (after CI surfaced more pre-existing defects)**
- The scoped pack (CP-6) then exposed two more **pre-existing** issues in real CI, both fixed
  properly (root-cause, not blanket suppression):
  - **Submodule auto-pack leak:** in CI the submodule projects are `IsPackable=true` *and*
    `GeneratePackageOnBuild=true` (via shared `Hexalith.Builds`), so building EventStore projects
    auto-emitted `Hexalith.Commons.*.nupkg`. Fixed by `-p:GeneratePackageOnBuild=false` on the pack
    loop (explicit `dotnet pack` still produces each EventStore package) + scoping `publishCmd` to
    `Hexalith.EventStore.*.nupkg`.
  - **NU5104 (stable package with prerelease dependency):**
    - `Hexalith.EventStore.Testing` carried an **unused** `NSubstitute` (prerelease `6.0.0-rc.1`)
      package reference → marked `PrivateAssets="all"` (not in the public API; must not flow).
      `IntegrationTests` relied on that transitive ref, so it now declares `NSubstitute` directly.
    - `Hexalith.EventStore.Aspire` depends on `CommunityToolkit.Aspire.Hosting.Dapr`, which is
      **preview-only upstream** (DAPR-on-Aspire) with no stable version to pin → scoped, documented
      `NoWarn NU5104` in `Aspire.csproj` only.
- Validated locally (SDK 10.0.301, faithful with `GeneratePackageOnBuild=false`): **all 12 EventStore
  packages pack clean, 0 submodule packages, all `Testing`-consumer test projects compile.**

### Production (published package)

**CP-5 — `DaprETagService` actor `RequestTimeout` overridable (default 3 s preserved)**
- Added optional `TimeSpan? requestTimeout = null` constructor parameter; `_proxyOptions` now uses
  `requestTimeout ?? TimeSpan.FromSeconds(3)`. The built-in DI container supplies the default for
  the unregistered optional parameter, so the `TryAddScoped<IETagService, DaprETagService>()`
  registration and production behavior are unchanged.
- The live-sidecar test constructs the service with `requestTimeout: TimeSpan.FromSeconds(30)` to
  tolerate cold-start activation latency on CI.

### Validation (local)

| Lane | Filter | Result |
|------|--------|--------|
| Release gate | `Category!=LiveSidecar` | **2168 passed, 0 failed**, 25 skipped (2193 total) |
| Integration  | `Category=LiveSidecar`  | **28 passed, 0 failed** |
| Partition check | — | 2193 + 28 = 2221 = original total ✓ |
| Workflow YAML | — | `release.yml` + `integration.yml` parse OK |
| **CI (PR #271)** | `Integration Tests` → live-sidecar | **✓ passed in real CI** — the 28 live-sidecar tests (incl. the previously-failing one) green in the exact environment that was failing |

---

## 5. Implementation Handoff

- **Scope:** **Minor** → direct Developer implementation. No backlog reorganization; no PM/Architect
  replan.
- **Status:** All five changes implemented and validated locally. Remaining action: commit on a
  `fix/` branch and push so the `Release` workflow runs green and the new `Integration Tests`
  workflow exercises the live-sidecar suite.
- **Suggested commit (Conventional Commits):**
  `fix(ci): re-tier live-daprd integration tests off the release gate and harden the suite`
- **Success criteria:**
  1. `Release` workflow green on `main` (release/publish proceeds).
  2. `Integration Tests` workflow runs the 28 live-sidecar tests + Tier-3 suite green.
  3. No live-`daprd` test gates the per-push release path.
- **Follow-ups (non-blocking, separate change):**
  - Reconcile CLAUDE.md (Server.Tests now builds; document the `LiveSidecar` trait and two-lane CI).
  - Optional: bind `DaprETagService` timeout to `IOptions`/appsettings if runtime tuning is desired.
  - **Aspire-in-CI for Tier-3:** stand up a dedicated workflow that can host the full Aspire
    topology (`DistributedApplicationTestingBuilder`) so `IntegrationTests` can run in CI without
    making the integration lane slow/flaky.
