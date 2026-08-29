---
title: 'Bump .NET SDK, Builds, and effective packages'
type: 'chore'
created: '2026-08-29'
status: 'in-progress'
review_loop_iteration: 1
baseline_commit: '62d28510f3c11904b6b2ce22edc075d55878924b'
tenants_baseline_commit: 'eb965727329c7d7335be4cd341db4e2f9bf57b56'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EventStore pins SDK `10.0.302`, which cannot resolve when only current SDK `10.0.400` is installed. The parent already points at latest Builds `main`, but its Roslyn 5.9, NBomber 6.6, and xUnit 4 families remain unreconciled with EventStore consumers and live documentation. The root-declared `Hexalith.Tenants` submodule also pins SDK `10.0.302`, preventing the EventStore AppHost's Tenants resources from remaining healthy under the aligned workspace SDK.

**Approach:** Pin SDK `10.0.400` in EventStore and the already-initialized root-declared `Hexalith.Tenants` repository, retain the exact latest reachable Builds `main` and its audited catalog, migrate affected EventStore consumers, and synchronize only live snapshots in both owning repositories. Prove Release package mode with focused package, test-runner, generator, load-test, AppHost, Tenants documentation, and container checks.

## Boundaries & Constraints

**Always:** Resolve Builds `main` immediately before implementation and preserve its full SHA. Keep Builds as sole package authority, `rollForward: latestPatch`, `net10.0`, package mode, coupled adaptations, unrelated work, and historical evidence. Restrict the approved cross-repository change in `Hexalith.Tenants` to its SDK pin, live SDK snapshots, and directly coupled documentation test; preserve its baseline commit and all nested submodules.

**Ask First:** A newer Builds head introduces an uninvestigated package family; compatibility requires editing Builds rather than EventStore; a latest family fails after reasonable consumer migration; or a real container check requires publication/registry credentials rather than a local archive.

**Never:** Add local versions/overrides, downgrade or suppress a catalog family, initialize or update nested submodules, use recursive/remote updates, change TFMs, rewrite frozen evidence/history, publish, commit, push, or weaken warnings/auditing.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Current upstream | Gitlink, checkout, and remote `main` all equal `18742168...` | Leave gitlink/catalog unchanged; update SDK and consumers | Record the no-op identity proof |
| Upstream advanced | Remote `main` resolves to a newer reachable commit | Advance only the root Builds checkout/gitlink and reassess its catalog delta | Stop on unrelated/local submodule changes |
| Latest family breaks a consumer | Warning-as-error or test failure is caused by Roslyn, NBomber, or xUnit | Apply the supported EventStore API/config migration and rerun the family lane | Ask before shared-catalog rollback or expansion |
| Tenants SDK mismatch | AppHost Tenants resources resolve the submodule's `10.0.302` pin while only SDK `10.0.400` is installed | Pin the already-initialized Tenants repository and its live SDK snapshots to `10.0.400`; rerun its documentation test and live topology | Preserve historical evidence and all other Tenants/submodule content |

</frozen-after-approval>

## Code Map

- `global.json:3-4` -- sole EventStore SDK selector; change `10.0.302` to `10.0.400`, preserving `latestPatch`.
- `references/Hexalith.Tenants/global.json`, `docs/quickstart.md`, `_bmad-output/project-context.md`, `_bmad-output/planning-artifacts/architecture.md`, and `tests/Hexalith.Tenants.Server.Tests/Documentation/QuickstartDocumentationTests.cs` -- approved owning-repository SDK alignment and live snapshot/test update; preserve historical implementation, proposal, archive, investigation, and changelog evidence.
- `references/Hexalith.Builds` -- root gitlink/check-out authority; currently remote-equal at `18742168b0bcdc40e5223f7573b1dcca441d781f`.
- `references/Hexalith.Builds/Props/Directory.Packages.props:196-201,242,252,317-320` and `Tools/package-version-audit.json` -- latest families and matching 285-row audit; consumer-read-only.
- `perf/Hexalith.EventStore.LoadTests/Program.cs:34-36` -- replace obsolete aggregate `NodeStats.AllFailCount` with scenario/step failure statistics.
- `perf/Hexalith.EventStore.LoadTests/LoadTestExitCode.cs`, `tests/Hexalith.EventStore.LoadTests.Tests/`, and `Hexalith.EventStore.slnx` -- isolate and test success, scenario-failure, and step-failure exit classification.
- `tests/Hexalith.EventStore.{Server.LiveSidecar.Tests,IntegrationTests,Server.Tests}/AssemblyInfo.cs` -- replace xUnit 4-obsolete `CollectionBehavior(DisableTestParallelization=true)` with assembly `Parallelization(Mode=ParallelMode.None)` while preserving serialized execution.
- `tests/Hexalith.EventStore.{Server.LiveSidecar.Tests,IntegrationTests,Server.Tests}/AssemblyParallelizationTests.cs` -- reflect each built assembly and prove its xUnit 4 mode is `None`.
- `src/Hexalith.EventStore.RestApi.Generators/` and its test/DomainService consumers -- Roslyn 5.9 compatibility surface; change only compiler-proven incompatibilities.
- `CONTRIBUTING.md`, `docs/getting-started/prerequisites.md`, `docs/guides/{deployment-azure-container-apps,deployment-docker-compose,deployment-kubernetes,troubleshooting}.md`, `docs/brownfield/*.md`, `_bmad-output/project-context.md`, and `_bmad-output/planning-artifacts/architecture.md` -- live SDK/package snapshots only.
- `Directory.Build.targets:21-102` and `CorrectiveOciProvenanceReleaseTests` -- SDK-internal container-label workaround requiring real 10.0.400 archive/provenance regression proof; comments are historical observations.

## Tasks & Acceptance

**Execution:**

- [x] `global.json` and Builds gitlink -- pin SDK `10.0.400`; re-resolve latest Builds and change the gitlink only if upstream advanced.
- [x] Package consumers above -- migrate NBomber/xUnit APIs and address only demonstrated Roslyn 5.9 compatibility failures.
- [x] Regression tests above -- prove both load-harness exit outcomes and the three assemblies' effective xUnit 4 serialization metadata.
- [x] Live documentation surfaces -- synchronize current SDK, Roslyn, NBomber, and xUnit values without touching historical/frozen evidence.
- [x] `Hexalith.Tenants` SDK/live surfaces -- pin SDK `10.0.400`, update only live SDK snapshots and the directly coupled quickstart documentation test, and preserve its baseline/history/nested dependencies.
- [x] Validation lanes -- run exact Builds governance, package-mode restore/build, focused consumers/tests, live AppHost state, and SDK container provenance/archive commands below.

**Acceptance Criteria:**

- Given the approved dependency refresh, when identity preflight runs, then EventStore selects SDK `10.0.400`, Builds checkout/gitlink equals the latest reachable remote `main`, and its catalog exactly matches the deterministic audit.
- Given the latest catalog, when Release package-mode consumers build and focused tests run, then Roslyn 5.9 generators compile, NBomber uses supported statistics, xUnit 4 preserves required serialization, and no warnings/errors or stale dependency assets remain.
- Given SDK `10.0.400`, when container regression validation runs, then both local target architectures retain exact provenance labels and no internal-target/type-load regression occurs.
- Given current documentation, when stale-snapshot checks run, then live SDK/package claims match effective pins while historical and checksum-bound evidence remains byte-unchanged.
- Given the human-approved cross-repository SDK alignment, when Tenants documentation verification and the EventStore AppHost run, then Tenants selects SDK `10.0.400`, its live snapshots agree, and the Tenants resources remain runnable/healthy without SDK-resolution errors.

## Spec Change Log

- 2026-08-29: Implemented SDK and consumer migrations, synchronized live version snapshots, and completed the focused validation matrix.
- 2026-08-29 review loop 1 (`bad_spec`): verification review found compilation-only coverage for the new NBomber exit branch and xUnit serialization plus non-reproducible placeholder commands. Added durable behavior/metadata tests, exact commands, live AppHost state verification, and final HEAD/diff/submodule binding; this avoids a false green where migrated code compiles but exit/serialization/runtime behavior is unproved. **KEEP:** SDK `10.0.400`, latest Builds no-op/package authority, the working scenario/step and `Parallelization` migrations, scoped live-doc updates, historical-evidence preservation, and the successful package/container validation lanes.
- 2026-08-29 human scope renegotiation: live topology verification found the root-declared `Hexalith.Tenants` repository still pinned to SDK `10.0.302`; the user explicitly approved extending the change to align that repository to SDK `10.0.400`. Added its owning-repository pin, live snapshots, coupled documentation test, baseline binding, and topology health proof while retaining the no-commit/no-push and no-nested-submodule constraints.

## Verification

**Commands:**

- `dotnet --version && dotnet --info` -- expected: SDK `10.0.400` resolves from the repository.
- `pwsh -NoProfile -File references/Hexalith.Builds/Tools/validate-central-package-versions.ps1; pwsh -NoProfile -File references/Hexalith.Builds/Tools/test-authoritative-package-catalog.ps1; pwsh -NoProfile -File references/Hexalith.Builds/Tools/validate-package-version-audit.ps1; pwsh -NoProfile -File references/Hexalith.Builds/Tools/validate-package-version-exceptions.ps1 -InventoryPath references/Hexalith.Builds/Tools/package-version-exceptions.json -CatalogPath references/Hexalith.Builds/Props/Directory.Packages.props; pwsh -NoProfile -File references/Hexalith.Builds/Tools/validate-dapr-package-versions.ps1; pwsh -NoProfile -File references/Hexalith.Builds/Tools/validate-consumer-package-authority.ps1 -RepositoryRoot . -CatalogPath references/Hexalith.Builds/Props/Directory.Packages.props` -- expected: all pass.
- `dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false && dotnet build Hexalith.EventStore.slnx --no-restore --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` -- expected: 0 warnings/errors.
- `dotnet tests/Hexalith.EventStore.LoadTests.Tests/bin/Release/net10.0/Hexalith.EventStore.LoadTests.Tests.dll; dotnet tests/Hexalith.EventStore.RestApi.Generators.Tests/bin/Release/net10.0/Hexalith.EventStore.RestApi.Generators.Tests.dll; dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll; dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll` -- expected: all pass, no skips.
- `dotnet tests/Hexalith.EventStore.IntegrationTests/bin/Release/net10.0/Hexalith.EventStore.IntegrationTests.dll -class Hexalith.EventStore.IntegrationTests.AssemblyParallelizationTests; dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class Hexalith.EventStore.Server.LiveSidecar.Tests.AssemblyParallelizationTests; dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.AssemblyParallelizationTests` -- expected: 1/1 each.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.CorrectiveOciProvenanceReleaseTests` -- expected: all pass, including real local dual-architecture archive labels.
- `dotnet build references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Hexalith.Tenants.Server.Tests.csproj --configuration Release -warnaserror -m:1; dotnet references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/bin/Release/net10.0/Hexalith.Tenants.Server.Tests.dll -class Hexalith.Tenants.Server.Tests.Documentation.QuickstartDocumentationTests` -- expected: the owning repository resolves SDK `10.0.400`, builds without warnings/errors, and its quickstart SDK assertions pass.
- `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive --format Json; aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive --format Json; aspire stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` -- expected: start succeeds and described resources reach runnable/healthy states without error telemetry.
- `bash scripts/check-doc-versions.sh; git diff --check; git status --porcelain=v1 --untracked-files=all --ignore-submodules=none` plus scoped stale-version and evidence-path scans -- expected: only intended paths, no live stale pins, whitespace errors, submodule drift, or evidence changes.
- Record final `git rev-parse HEAD`, `git diff HEAD | sha256sum`, `git submodule status`, Builds remote/check-out/gitlink SHA, and exact commands/counts in Results -- expected: verification binds the reviewed worktree despite concurrent HEAD movement.

## Results

- **Identity and dependency authority:** `dotnet --version && dotnet --info` resolved SDK `10.0.400` from the root `global.json`. The approved root baseline remains `62d28510f3c11904b6b2ce22edc075d55878924b`; validation ran at root HEAD `2aa94e805dc8c46d8a50b524f58da2070cd22307` after the pre-existing `2aa94e80` FrontComposer gitlink commit. `git ls-tree HEAD references/Hexalith.Builds` plus `git -C references/Hexalith.Builds rev-parse HEAD` and `git -C references/Hexalith.Builds rev-parse refs/remotes/origin/main` all returned `18742168b0bcdc40e5223f7573b1dcca441d781f`; Builds remained clean and its root gitlink/catalog were a no-op.
- **Catalog governance:** the exact six PowerShell commands above passed: central-package validation covered 285 entries; authoritative-catalog tests covered 49 approved identities and 3 shared versions; the deterministic audit covered 285 packages, 140 families, and 1 source; exception validation covered 15 entries; DAPR validation covered 8 unique IDs at `1.18.5`; consumer-authority validation covered 52 projects. Effective catalog evidence was Roslyn `5.9.0`, NBomber `6.6.0`, and xUnit `4.0.0`; no Builds edit or local override was made.
- **Package-mode build and focused tests:** the exact Release restore/build command above completed with 0 warnings and 0 errors. The four executable suites passed with no skips: LoadTests `3/3`, RestApi.Generators `124/124`, DomainService `155/155`, and AppHost `96/96` (378 total). The three assembly metadata commands each passed `1/1`, proving `ParallelMode.None` for IntegrationTests, Server.LiveSidecar.Tests, and Server.Tests. `CorrectiveOciProvenanceReleaseTests` passed `46/46` with no skips, exercising the real local dual-architecture SDK container archive/provenance path under SDK `10.0.400`.
- **Tenants owning-repository lane:** the recorded Tenants baseline remains `eb965727329c7d7335be4cd341db4e2f9bf57b56`. The checkout was already concurrently fast-forwarded to `435771779e32f357ded47988b294dc1da4029a4a` before the SDK edits. During final auditing it advanced concurrently again to `c085d43291c98a9aab685d00cdf52f68595640f1` (`build(deps): bump .NET SDK version to 10.0.400`), which absorbed four approved SDK/live-documentation files; no validation agent commit, reset, or revert occurred. The exact Tenants Release build passed with 0 warnings/errors and `QuickstartDocumentationTests` passed `6/6` with no skips. The remaining owned dirty diff is `_bmad-output/planning-artifacts/architecture.md` only (`2` insertions, `2` deletions; `git -C references/Hexalith.Tenants diff HEAD | sha256sum` = `86f41eb49ba43cec6b0a6112dde301f2e10b9a82b86c46ed76ba79c6d592f9fe`). All seven Tenants nested submodules remain uninitialized at their recorded identities; none was initialized or changed. The 25 files still mentioning `10.0.302` are changelog/archive/proposal/investigation/completed-story evidence and remained outside the dirty diff.
- **Live AppHost proof:** Aspire CLI `13.5.3` started the specified AppHost successfully. The first cold run reached all `17/17` resources `Running` and `Healthy`; one handled startup-time Polly retry logged Error when the Tenants operational-index metadata endpoint returned `500`, followed by repeated `200` responses. After a clean `aspire stop`, the same `aspire start`, resource waits, `aspire describe`, and `aspire otel logs --severity Error --limit 1000` sequence was rerun warm: all `17/17` resources were `Running` and `Healthy`, including `tenants`, `tenants-api`, and their DAPR executables; the Error query returned `[]`. Final `aspire stop` succeeded and `aspire ps --non-interactive --format Json` returned `[]`, leaving no AppHost running.
- **Documentation, evidence, and worktree binding:** `bash scripts/check-doc-versions.sh` passed the 4-row DAPR consistency check; both root and Tenants `git diff --check` passed. A final concurrent-status rerun found only one trailing space after `story_location:` in `sprint-status.yaml`; that space was removed without changing the concurrent value or structure, and the gate then passed again. Scoped live scans returned zero stale `10.0.302`, Roslyn `5.6.0`, NBomber `6.5.0`, or xUnit `3.2.2` claims, while historical/evidence-path dirty scans returned no paths. Immediately before this Results-only append, `git diff HEAD | sha256sum` was `925a3b7219bd05c17b451773081c7836ef6d33c619f6ac991d0f3ae6ac37fab2`; after the concurrent sprint-status update and whitespace normalization, the stable tracked root diff excluding this spec was 22 paths with SHA-256 `13110e1f74da18c34eae14e5273bcf5c4724428f2cd020519a043a41cf157a19`. Root status contained 23 tracked/submodule paths and 7 untracked implementation paths. `git submodule status` bound AI.Tools `de38f78e`, Builds `18742168`, Commons `feab4efc`, FrontComposer `85216682`, Memories `e0ecafe6`, PolymorphicSerializations `65fc3361`, and the concurrently advanced Tenants checkout `c085d432`; the root Tenants gitlink remains the preserved `eb965727` baseline and is intentionally reported as changed rather than silently staged or committed.
- **Risks:** the no-commit/no-push validation agent made no Git mutations, but the concurrent Tenants `c085d432` commit appeared during validation and the root gitlink has not been committed to it. That concurrent commit and the earlier `43577177`/`d50655ea` commits must be reviewed separately from the five approved SDK/live-snapshot edits before any future root gitlink update. The cold-start retry is a startup-order observation, not an SDK-resolution failure; the required clean warm proof had zero Error telemetry and every resource healthy. A separate FrontComposer AppHost appeared concurrently after the EventStore cleanup; it was preserved as unrelated state, while `aspire ps ... | jq '[.[] | select(.appHostPath | contains("/eventstore/"))]'` returned `[]`, proving no EventStore AppHost remained.
