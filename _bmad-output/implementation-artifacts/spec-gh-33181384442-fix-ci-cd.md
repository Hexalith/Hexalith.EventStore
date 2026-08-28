---
title: 'Fix CI and add Release validation bypass'
type: 'bugfix'
created: '2026-08-28'
status: 'done'
baseline_commit: 'c61739206fd89619b7d29dfb0812225a234066bb'
review_loop_iteration: 1
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/docs/ci.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** CI run 33181384789 and Integration run 33181384442 could not fetch the root `Hexalith.Builds` gitlink; current `main` has repaired that pointer but its replacement CI now fails every AppHost build because Aspire 13.5.3 emits `ASPIRE010` under warning-as-error. Release also cannot be dispatched while CI is red because both its caller and pinned reusable publisher require exact-source successful CI.

**Approach:** Keep the approved NuGet-backed Aspire orchestration mode and suppress only its documented `ASPIRE010` reminder. Add an explicit boolean Release dispatch bypass, off by default, that substitutes the exact-source successful Commitlint push gate for the CI-success gate while retaining live-main identity, environment approval, publication validation, and destination-safety checks.

## Boundaries & Constraints

**Always:** Make `bypass-validation` explicit, boolean, optional, and default `false`. In normal mode require successful exact-source `ci.yml`; in bypass mode require successful exact-source `commitlint.yml`. In both modes require `refs/heads/main`, equality with live `main`, the protected `production` environment, the pinned Builds release implementation, package inventory validation, immutable source identity, credential checks, destination absence, and post-publish verification. Preserve NuGet-backed Aspire orchestration by explicitly setting `AspireUseCliBundle=false`.

**Ask First:** Expanding bypass to stale/non-main source, protected-environment approval, publication/package/container validation, destination collision checks, or changing the shared `Hexalith.Builds` repository.

**Never:** Make bypass the default; silently infer bypass from CI state; disable warning-as-error globally; suppress unrelated diagnostics; modify root-declared submodule contents or OQ8 evidence; publish before the workflow and focused regression tests pass.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Normal release | `bypass-validation=false`, exact green CI on live `main` | Release reaches protected environment and uses `ci.yml` at all source checks | Missing/stale/failed CI rejects before publication |
| Authorized bypass | `bypass-validation=true`, exact successful Commitlint push on live `main` | CI-success validation is bypassed consistently in caller and reusable publication boundaries | Missing/stale Commitlint proof or non-main source rejects |
| Aspire build | AppHost uses Aspire 13.5.3 with NuGet orchestration | `-warnaserror` build has no `ASPIRE010` failure | Every other warning remains blocking |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj:3-8` -- declare NuGet orchestration mode and the Aspire-documented targeted diagnostic suppression.
- `.github/workflows/release.yml:6-100` -- manual input, caller source preflight, and conditional `source-ci-workflow` passed to immutable Builds release tooling.
- `scripts/validate-publication-preflight.sh:1-120` -- permit exactly `ci.yml` or `commitlint.yml` at the repository publication boundary and forward the selected proof unchanged.
- `.releaserc.json:1-20` -- read-only consumer evidence: Semantic Release invokes the repository publication wrapper in both verify and publish phases.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/` -- add a source-shape guard for explicit `AspireUseCliBundle=false` and exact `ASPIRE010` suppression.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:1-1220` -- replace the no-input invariant; execute the caller mapping and repository wrapper for both allowed workflows; retain rejection coverage for unknown workflow, wrong ref, and stale head.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:800-850` -- assert the reusable publisher receives the caller-selected source workflow rather than a constant.
- `docs/ci.md:142-175` -- document bypass scope, retained safeguards, and CLI dispatch syntax.
- `references/Hexalith.Builds/.github/workflows/domain-release.yml` and its publication preflight -- read-only evidence: all publication boundaries consume caller-selected `source-ci-workflow`.

## Tasks & Acceptance

**Execution:**
- [x] AppHost project and focused configuration test -- retain NuGet orchestration while making warning-as-error builds clean and regression-protected; use repository-conforming PascalCase test names.
- [x] Release workflow and caller governance tests -- add the false-by-default bypass, emit and consume the selected source workflow, and prove `false -> ci.yml` plus `true -> commitlint.yml` through the actual job-output boundary.
- [x] Repository publication wrapper and executable regression tests -- accept exactly `ci.yml` and `commitlint.yml`, forward either unchanged to the pinned preflight, and continue to fail closed for every other value.
- [x] `docs/ci.md` -- document normal and bypass dispatch behavior, including safeguards that remain mandatory.
- [x] Repository readiness -- validate the complete diff, workflow syntax, exact focused commands, and exact commit candidate so the reviewed change is ready for the separately authorized delivery phase.

**Acceptance Criteria:**
- Given Aspire 13.5.3 in either package or Tenants source mode, when AppHost and solution builds run with warnings as errors, then `ASPIRE010` is absent and no other diagnostic is weakened.
- Given either bypass value, when caller and publication-boundary source checks execute, then the selected workflow crosses the job-output, reusable-workflow, repository-wrapper, and pinned-preflight boundaries unchanged while non-main, stale, failed, or unknown proof is rejected.
- Given the complete local repair, when the configured focused and broad gates run, then the change introduces no new workflow regression and every bypass matrix path has executable evidence; the pre-existing immutable OQ8 blocker remains separately evidenced rather than being represented as repaired.

## Spec Change Log

- 2026-08-28, review loop 1: Review found that the repository publication wrapper rejected `commitlint.yml` and the caller job output was not behaviorally observed. Expanded the Code Map, tasks, and verification to execute both allowed workflows through the wrapper and assert the exact `GITHUB_OUTPUT` mapping. KEEP: false-default typed input; normal `ci.yml`; bypass `commitlint.yml`; live-main, pinned publication, package, environment, credential, destination, and post-publish safeguards; explicit NuGet Aspire mode with only `ASPIRE010` suppressed. Avoid the known-bad caller-only bypass.

## Design Notes

Aspire 13.5 documents `NoWarn=ASPIRE010` for existing AppHosts that intentionally retain NuGet-restored DCP/dashboard dependencies. The bypass reuses the pinned publisher's existing `source-ci-workflow` seam rather than modifying shared Builds: `commitlint.yml` supplies an auditable successful exact-source push proof while package and publication validators remain active.

## Verification

**Commands:**
- Package-mode AppHost restore: `dotnet restore src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -m:1 -p:UseHexalithProjectReferences=false -p:HexalithTenantsFromSource=false`.
- Package-mode AppHost build: `dotnet build src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --configuration Release --no-restore -warnaserror -m:1 -p:UseHexalithProjectReferences=false -p:HexalithTenantsFromSource=false`.
- Tenants source-mode AppHost restore: `dotnet restore src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -m:1 -p:UseHexalithProjectReferences=false -p:HexalithTenantsFromSource=true`.
- Tenants source-mode AppHost build: `dotnet build src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --configuration Release --no-restore -warnaserror -m:1 -p:UseHexalithProjectReferences=false -p:HexalithTenantsFromSource=true`.
- Exact CI Tenants source-mode lane: `dotnet restore tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj -p:Configuration=Debug -p:UseHexalithProjectReferences=true`, then `dotnet build tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj --no-restore --configuration Debug -warnaserror -m:1 -p:UseHexalithProjectReferences=true`, then `dotnet test tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj --no-build --configuration Debug --filter FullyQualifiedName~TenantsApiLaunchSettingsTests -p:UseHexalithProjectReferences=true`.
- Full solution restore: `dotnet restore Hexalith.EventStore.slnx -m:1 -p:UseHexalithProjectReferences=false -p:HexalithTenantsFromSource=false`.
- Full solution build: `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -warnaserror -m:1 -p:UseHexalithProjectReferences=false -p:HexalithTenantsFromSource=false`.
- AppHost configuration lane: `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll`.
- Release workflow and publication-wrapper governance lane: `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.ContainerPublishingGovernanceTests`.
- Focused release-manifest source mapping: `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests.Release_workflow_uses_domain_release_with_approved_eventstore_container_only`.
- Contracts lane excluding immutable OQ8 evidence: `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class- Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests`.
- Full Contracts evidence lane: `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -reporter silent -ctrf <temporary-directory>/contracts.json`; inspect the CTRF summary and failed class set before removing the temporary directory.
- Workflow validation: `actionlint -no-color .github/workflows/release.yml` and `python3 -c "from pathlib import Path; import yaml; yaml.safe_load(Path('.github/workflows/release.yml').read_text(encoding='utf-8'))"`.
- Shell validation: `bash -n scripts/validate-publication-preflight.sh`.
- Diff validation: `git diff --check`, plus `git diff --no-index --check /dev/null tests/Hexalith.EventStore.AppHost.Tests/Configuration/AppHostProjectConfigurationTests.cs` and `git diff --no-index --check /dev/null _bmad-output/implementation-artifacts/spec-gh-33181384442-fix-ci-cd.md` for the untracked files.
- Exact commit-message preflight: `printf '%s\n' 'fix(ci): allow guarded release validation bypass' | npx commitlint --verbose`.

## Results

- Package-mode restore and build passed; the build completed with 0 warnings and 0 errors.
- Tenants source-mode restore and build passed; the build completed with 0 warnings and 0 errors.
- The exact CI `tenants-source-mode` restore/build commands passed with 0 warnings and 0 errors, and its filtered topology guardrail passed 10/10.
- Full solution restore and build passed; the build completed with 0 warnings and 0 errors.
- AppHost tests passed 96/96. The focused AppHost project-configuration test also passed 1/1.
- `ContainerPublishingGovernanceTests` passed 60/60, including both `verify` and `publish` forwarding for `ci.yml` and `commitlint.yml`, unknown-workflow rejection, malformed bypass rejection without output, complete dispatch-key enumeration, and exact source-proof producer/output/input binding.
- The focused release-manifest source-workflow test passed 1/1.
- Contracts excluding `Oq8PlatformClosureTests` passed 1460/1460.
- The full Contracts evidence run executed 1775 tests: 1575 passed and 200 failed. All 200 failures were separately evidenced, immutable `Oq8PlatformClosureTests` failures caused by the unchanged `Review subject binding drift: ciWorkflow`; no other test class failed.
- `actionlint` and PyYAML parsing passed for `release.yml`; `bash -n` passed for the publication preflight wrapper.
- Tracked and untracked diff whitespace checks passed.
- The exact candidate `fix(ci): allow guarded release validation bypass` passed the repository-pinned commitlint 21.1.0 preflight. No commit was created, so no post-commit validation applied.

## Suggested Review Order

**Release proof selection**

- A false-default typed input makes bypass explicit and auditable.
  [`release.yml:9`](../../.github/workflows/release.yml#L9)

- One fail-closed selector maps each mode to its exact push proof.
  [`release.yml:32`](../../.github/workflows/release.yml#L32)

- The selected proof crosses the job boundary into immutable release tooling.
  [`release.yml:115`](../../.github/workflows/release.yml#L115)

**Publication boundary**

- The repository wrapper accepts exactly the two reviewed proof workflows.
  [`validate-publication-preflight.sh:62`](../../scripts/validate-publication-preflight.sh#L62)

**Aspire build repair**

- Explicit NuGet orchestration suppresses only Aspire's documented migration reminder.
  [`Hexalith.EventStore.AppHost.csproj:4`](../../src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj#L4)

**Operator contract**

- Documentation distinguishes exceptional bypass from complete CI evidence.
  [`ci.md:145`](../../docs/ci.md#L145)

**Regression evidence**

- Executable wrapper tests cover both workflows across verify and publish phases.
  [`ContainerPublishingGovernanceTests.cs:168`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs#L168)

- Governance tests bind typed input, producer ID, job output, and reusable input.
  [`ContainerPublishingGovernanceTests.cs:548`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs#L548)

- Project-shape tests preserve unconditional targeted Aspire configuration.
  [`AppHostProjectConfigurationTests.cs:7`](../../tests/Hexalith.EventStore.AppHost.Tests/Configuration/AppHostProjectConfigurationTests.cs#L7)

- Release-manifest coverage preserves the pinned publisher and approved container inventory.
  [`ReleasePackageManifestTests.cs:813`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs#L813)
