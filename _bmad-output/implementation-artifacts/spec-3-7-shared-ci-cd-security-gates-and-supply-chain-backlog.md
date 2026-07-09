---
title: '3.7 Shared CI/CD Security Gates And Supply-Chain Backlog'
type: 'chore'
created: '2026-07-09T12:53:52+02:00'
status: 'done'
review_loop_iteration: 0
story_key: '3-7-shared-ci-cd-security-gates-and-supply-chain-backlog'
baseline_commit: '0f428d0c914f2151aab15bb262f956a9630041dc'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-09.md'
---

<frozen-after-approval reason="human-owned intent - do not modify unless human renegotiates">

## Intent

**Problem:** EventStore CI and release workflows are custom while Tenants uses shared Hexalith.Builds reusable workflows. A direct workflow copy would violate NFR10 unless live-sidecar tests remain outside the deterministic release gate.

**Approach:** Migrate EventStore CI and release to thin shared workflow callers, add the scripts expected by shared `domain-ci`, and split live-sidecar tests into their own project so `Server.Tests` can run unfiltered in the deterministic gate while the live lane remains visible but non-release-blocking.

## Boundaries & Constraints

**Always:** Keep `Category=LiveSidecar` execution outside the semantic-release gate. Keep `tools/release-packages.json` as the package inventory and package in Release with `UseHexalithProjectReferences=false`. Use only `Hexalith.EventStore.slnx`; do not create `.sln`. Use root-declared submodules only and do not edit submodule contents. Preserve support-safe CI/docs language and current manifest scope of 14 EventStore NuGet packages.

**Ask First:** Changing `references/Hexalith.Builds` reusable workflows, deleting `integration.yml`, making live-sidecar failures block release, enabling additional EventStore container publications beyond `src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore`, or adding a coverage gate that requires new shared-script contracts.

**Never:** Do not run solution-level `dotnet test` as the validation default. Do not put package versions in project files. Do not publish sample/admin containers accidentally. Do not hide live-sidecar coverage by simply omitting it from CI. Do not initialize nested submodules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| reusable CI | Push or PR to `main` | `.github/workflows/ci.yml` calls `Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main` with EventStore inputs, package validation enabled, and deterministic projects listed | Workflow syntax fails fast if required inputs or scripts are missing |
| live lane | `Category=LiveSidecar` tests moved to live project | Deterministic `Server.Tests` runs unfiltered in shared CI; live project runs in a dedicated DAPR lane outside release blocking | Live lane failure is visible but does not trigger semantic-release failure |
| package validation | `domain-ci` calls `scripts/pack-release-packages.py`, `scripts/validate-nuget-packages.py`, and `scripts/validate-consumer-package-references.py` | Scripts preserve manifest-only packing and reject missing, extra, or non-EventStore packages | Validation exits nonzero with manifest/package evidence |
| release | CI succeeds from a push to `main` | `.github/workflows/release.yml` calls shared `domain-release.yml@main`, publishes NuGet through semantic-release, and prepares only approved container mapping | Missing secrets or invalid package output blocks release before publish |

</frozen-after-approval>

## Code Map

- `.github/workflows/ci.yml` -- replace custom restore/build/test workflow with shared `domain-ci` caller and EventStore inputs.
- `.github/workflows/release.yml` -- replace custom semantic-release job with shared `domain-release` caller and approved container mapping.
- `.github/workflows/integration.yml` -- keep as the non-release-blocking live-sidecar lane until shared CI supports advisory filtered/project lanes.
- `.github/workflows/commitlint.yml` -- add `push` on `main` trigger parity with Tenants.
- `scripts/pack-release-packages.py`, `scripts/validate-nuget-packages.py`, `scripts/validate-consumer-package-references.py` -- shared-workflow-compatible package validation entry points.
- `tools/pack-release-packages.py`, `tools/validate-release-packages.py`, `tools/release-packages.json` -- existing manifest-governed release scripts and inventory to preserve.
- `tests/Hexalith.EventStore.Server.Tests/` -- deterministic server test project after live-sidecar files are moved or split.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/` -- new DAPR-backed live-sidecar test project.
- `Hexalith.EventStore.slnx` -- add the new live-sidecar test project.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- package/workflow governance coverage for shared-script compatibility.
- `docs/ci.md` -- document reusable workflow shape, package validation, and NFR10 lane split.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/` -- create a test project and move the live-sidecar fixture plus live-only test classes from `Server.Tests`; split mixed files so deterministic sentinel/serialization coverage stays in `Server.Tests` -- lets shared CI run `Server.Tests` without trait filters.
- [x] `Hexalith.EventStore.slnx` -- add the new live-sidecar test project -- keeps restore/build on the `.slnx` solution only.
- [x] `.github/workflows/ci.yml` -- replace the custom workflow with a shared `domain-ci.yml@main` caller using EventStore deterministic test inputs and `run-consumer-validation: true`; leave coverage gate disabled unless its script contract is implemented -- matches Tenants pattern without inventing local workflow logic.
- [x] `.github/workflows/integration.yml` -- retarget the live-sidecar DAPR lane to the new live project and keep it independent from `release.yml` -- preserves NFR10 while shared workflow lacks advisory live-lane inputs.
- [x] `.github/workflows/release.yml` -- replace the custom release job with a shared `domain-release.yml@main` caller, pass NuGet/container secrets, and publish only `src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore` -- mirrors Tenants release structure.
- [x] `.github/workflows/commitlint.yml` -- add `push: main` trigger -- prevents direct-push commit messages from bypassing release semantics.
- [x] `scripts/pack-release-packages.py`, `scripts/validate-nuget-packages.py`, and `scripts/validate-consumer-package-references.py` -- add compatibility wrappers/checks that keep `tools/release-packages.json` authoritative -- satisfies shared `domain-ci` consumer validation.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- assert shared-script entry points exist, remain manifest-backed, and release config still delegates manifest packing/validation -- locks the workflow migration contract.
- [x] `docs/ci.md` -- update CI/release documentation for shared callers, separate live-sidecar lane, package validation scripts, and remaining supply-chain backlog -- prevents stale operational guidance.

**Acceptance Criteria:**
- Given EventStore workflow files are inspected, when CI/release/security workflows are reviewed, then CI and release are thin shared workflow callers and CodeQL, dependency-review, and commitlint remain shared callers using `@main`.
- Given `Server.Tests` and live-sidecar tests are both present, when shared CI inputs are read, then deterministic `Server.Tests` run without `Category!=LiveSidecar` and live-sidecar tests run only in the dedicated non-release-blocking DAPR lane.
- Given shared `domain-ci` package validation runs, when it calls the expected `scripts/` entry points, then only manifest-listed EventStore packages are packed/validated and consumer restore/build uses the local packages, not source submodule projects.
- Given semantic-release runs through shared `domain-release`, when artifacts are prepared, then NuGet publish remains scoped to the 14 manifest packages and container publish is limited to the approved `eventstore` mapping.
- Given docs and governance tests are run, when workflow/package references are scanned, then they describe the reusable workflow pattern and fail on stale custom workflow or manifest-bypass assumptions.

### Review Findings

- [x] [Review][Decision] Broad submodule reference bumps are bundled into Story 3.7 — resolved: Administrator chose to keep the broad submodule reference bumps in this story.
- [ ] [Review][Patch] Hexalith.Builds submodule pin breaks package restore [references/Hexalith.Builds/Props/Directory.Packages.props:7]
- [ ] [Review][Patch] Release can partially publish NuGet before container credential failure [.releaserc.json:12]
- [ ] [Review][Patch] Planning epics still contain stale Story 3.7 scope [_bmad-output/planning-artifacts/epics.md:1030]
- [ ] [Review][Patch] Advisory workflow runs Playwright E2E tests without browser installation [.github/workflows/advisory-tests.yml:22]
- [ ] [Review][Patch] Shared CI uses default 15-minute build timeout for a larger restore/build/package/test lane [.github/workflows/ci.yml:18]
- [ ] [Review][Patch] No durable guard keeps LiveSidecar tests out of Server.Tests [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:163]
- [ ] [Review][Patch] Test-lane classification passes on any docs mention instead of explicit workflow/deferred ownership [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:241]
- [ ] [Review][Patch] Package validators accept prefix-collision package IDs [scripts/validate-nuget-packages.py:35]
- [ ] [Review][Patch] `git diff --check` fails on sprint change proposal EOF whitespace [_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-09.md:430]
- [ ] [Review][Patch] Sprint change proposal checklist still says approval is pending [_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-09.md:412]
- [ ] [Review][Patch] Moved live-sidecar tests retain same-line braces instead of Allman style [tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/ActorTenantIsolationTests.cs:20]
- [ ] [Review][Patch] Dapr serialization live-sidecar test keeps extra support types in one file [tests/Hexalith.EventStore.Server.LiveSidecar.Tests/DomainServices/DaprSerializationRoundTripTests.cs:39]

## Spec Change Log

## Review Triage Log

### 2026-07-09 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 5
- defer: 0
- reject: 0
- addressed_findings:
  - `[patch]` Added the release head-SHA guard so `workflow_run` releases only run when `github.sha` matches the completed CI run's `head_sha`.
  - `[patch]` Updated `.releaserc.json` so semantic-release calls the container helper installed by shared `publish-containers`.
  - `[patch]` Added a separate advisory workflow for the non-release-blocking browser/governance/evidence suites and governance coverage that classifies every test project into a workflow or documented deferred lane.
  - `[patch]` Removed package-consumer downgrade suppression by normalizing the shared CI synthetic package version to `999.0.0-ci-test` in the `scripts/pack-release-packages.py` wrapper.
  - `[patch]` Added governance assertions for the release helper, head-SHA guard, advisory workflow, lane classification, and no `NU1605` suppression.

## Design Notes

The shared `domain-ci.yml` currently has no per-project trait filter or advisory generic-test input. This spec therefore uses the approved alternative from the change proposal: split live-sidecar tests into a separate project and keep `integration.yml` as the advisory DAPR lane. Delete or absorb `integration.yml` only after Hexalith.Builds exposes an advisory lane that can preserve the same NFR10 behavior.

## Verification

**Commands:**
- `python3 scripts/pack-release-packages.py /tmp/hexalith-eventstore-ci-packages 0.0.0-ci-test --dry-run` -- expected: lists only manifest projects.
- `python3 scripts/pack-release-packages.py /tmp/hexalith-eventstore-ci-packages 0.0.0-ci-test` -- expected: creates the 14 manifest `.nupkg` files.
- `python3 scripts/validate-nuget-packages.py /tmp/hexalith-eventstore-ci-packages` -- expected: validates exactly the manifest package set.
- `python3 scripts/validate-consumer-package-references.py /tmp/hexalith-eventstore-ci-packages` -- expected: temporary consumer restores/builds from local packages in package mode.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter FullyQualifiedName~ReleasePackageManifestTests` -- expected: package/workflow governance tests pass.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release --filter "Category=LiveSidecar"` -- expected: zero live-sidecar tests remain in deterministic project.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release` -- expected: deterministic server tests pass.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -warnaserror -p:UseHexalithProjectReferences=false` -- expected: package-mode Release build passes.
- `git diff --check` -- expected: no whitespace errors.

**Results:**
- `python3 scripts/pack-release-packages.py /tmp/hexalith-eventstore-ci-packages 0.0.0-ci-test --dry-run` -- passed; listed the 14 manifest projects.
- `python3 scripts/pack-release-packages.py /tmp/hexalith-eventstore-ci-packages 0.0.0-ci-test` -- passed; created the 14 manifest `.nupkg` files.
- `python3 scripts/validate-nuget-packages.py /tmp/hexalith-eventstore-ci-packages` -- passed; validated 14 EventStore packages.
- `python3 scripts/validate-consumer-package-references.py /tmp/hexalith-eventstore-ci-packages` -- passed; validated 13 library packages plus the CLI tool package from local packages.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter FullyQualifiedName~ReleasePackageManifestTests` -- passed; 13/13.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release --filter "Category=LiveSidecar"` -- passed; zero matching live-sidecar tests.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release` -- passed; 2268 passed, 25 skipped.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -warnaserror -p:UseHexalithProjectReferences=false` -- passed.
- `git diff --check` -- passed.
- `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/ --configuration Release --filter "Category=LiveSidecar" --list-tests` -- passed; listed 26 live-sidecar tests without starting DAPR.

**Spot-check Results (2026-07-09):**
- `git diff --check` -- passed.
- `python3 -m py_compile scripts/pack-release-packages.py scripts/validate-nuget-packages.py scripts/validate-consumer-package-references.py` -- passed.
- `rg -n 'Category=LiveSidecar|DaprTestContainer' tests/Hexalith.EventStore.Server.Tests -g '*.cs' -g '*.csproj'` -- passed; no matches.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter FullyQualifiedName~ReleasePackageManifestTests` -- passed; 13/13.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release --filter "Category=LiveSidecar"` -- passed; zero matching live-sidecar tests.
- `python3 scripts/pack-release-packages.py /tmp/hexalith-eventstore-ci-packages-spot 0.0.0-ci-test --dry-run` -- passed; listed 14 manifest projects.
- `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/ --configuration Release --filter "Category=LiveSidecar" --list-tests` -- passed; listed 26 live-sidecar tests.
- `python3 scripts/pack-release-packages.py /tmp/hexalith-eventstore-ci-packages-review 0.0.0-ci-test` -- passed; normalized to `999.0.0-ci-test` and created 14 manifest packages.
- `python3 scripts/validate-nuget-packages.py /tmp/hexalith-eventstore-ci-packages-review` -- passed; validated 14 EventStore packages at `999.0.0-ci-test`.
- `python3 scripts/validate-consumer-package-references.py /tmp/hexalith-eventstore-ci-packages-review` -- passed; validated 13 library packages and 1 tool package with no downgrade suppression.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter FullyQualifiedName~ReleasePackageManifestTests` -- passed after review patches; 15/15.

## Suggested Review Order

1. **Workflow shape**
   - Start with the shared CI caller and deterministic test list in [ci.yml](../../.github/workflows/ci.yml#L18).
   - Check the release caller, head-SHA guard, and approved container mapping in [release.yml](../../.github/workflows/release.yml#L23).
   - Confirm the live-sidecar DAPR lane targets only the live project in [integration.yml](../../.github/workflows/integration.yml#L60).
   - Review the non-release-blocking advisory lane in [advisory-tests.yml](../../.github/workflows/advisory-tests.yml#L21).
   - Confirm direct pushes run the shared commitlint gate in [commitlint.yml](../../.github/workflows/commitlint.yml#L3).

2. **Release package validation**
   - Check shared CI version normalization in [pack-release-packages.py](../../scripts/pack-release-packages.py#L22).
   - Check the package-only consumer restore/build in [validate-consumer-package-references.py](../../scripts/validate-consumer-package-references.py#L223).
   - Confirm semantic-release invokes the shared container publish helper in [.releaserc.json](../../.releaserc.json#L11).

3. **Live-sidecar split**
   - Review the new live-sidecar test project in [Hexalith.EventStore.Server.LiveSidecar.Tests.csproj](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj#L1).
   - Review the moved live DAPR fixture in [DaprTestContainerFixture.cs](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs#L37).
   - Confirm deterministic sentinel coverage stayed in [TombstoningLifecycleSentinelTests.cs](../../tests/Hexalith.EventStore.Server.Tests/Actors/TombstoningLifecycleSentinelTests.cs#L11).
   - Confirm serialization round-trip coverage stayed in [DaprSerializationRoundTripTests.cs](../../tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprSerializationRoundTripTests.cs#L9).

4. **Governance and docs**
   - Review workflow/package governance coverage in [ReleasePackageManifestTests.cs](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs#L127).
   - Review lane classification coverage in [ReleasePackageManifestTests.cs](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs#L241).
   - Check the updated CI lane documentation in [docs/ci.md](../../docs/ci.md#L45).
   - Confirm the solution includes the new live-sidecar test project in [Hexalith.EventStore.slnx](../../Hexalith.EventStore.slnx#L62).
