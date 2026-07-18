---
baseline_commit: f7b2aa1c4d14c4b7049ce5c6bfb6c82364c55778
created: 2026-07-18
story_key: 3-5-shared-package-catalog-and-source-package-reference-modes
epic: "Epic 3 - Release And Repository Reliability"
requirements: FR21
governing_nfr: NFR9
architecture_decision: AD-11
story_type: cross-repository-build-governance
completion_gate: >-
  AC4 cannot close while Story 1.20 has not authorized Story 2.12's exact
  EventStore runtime/package identity; independent Story 3.5 work may proceed,
  but the story must remain in-progress until the no-mixed-graph criterion is proven
  or approved change control revises the conflicting boundary.
source_files:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18.md
  - _bmad-output/implementation-artifacts/epic-3-context.md
  - _bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/project-context.md
  - Directory.Build.props
  - Directory.Packages.props
  - references/Hexalith.Builds/Props/Directory.Packages.props
  - references/Hexalith.Builds/README.md
  - references/Hexalith.Builds/Samples/Module.Directory.Packages.props
  - tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.cs
  - scripts/check-doc-versions.sh
  - .github/dependabot.yml
---

# Story 3.5: Shared Package Catalog And Source/Package Reference Modes

Status: ready-for-dev

<!-- Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a **package maintainer**,
I want **external Hexalith dependencies selected deterministically by build intent and every NuGet version owned by the shared Hexalith.Builds catalog**,
so that **Debug builds can source-debug, Release builds depend on published packages, and consumer repositories cannot silently mask or compete with shared package-version updates**.

## Story Context

Story 3.5 implements FR21 under NFR9 and the source/package-mode invariant in AD-11. It is a coordinated change across the EventStore repository and the root-declared `references/Hexalith.Builds` repository. Work in each owning repository separately; commit Builds changes first only when the maintainer authorizes commits, then update the EventStore gitlink in an isolated dependency commit. Do not initialize or update nested submodules.

The approved 2026-07-18 correction establishes these boundaries:

- `references/Hexalith.Builds/Props/Directory.Packages.props` is the single authority for source-owned NuGet dependency versions.
- An unset Debug build selects source references when the root-declared source checkout exists. An unset Release build selects package references. Explicit caller properties win in both configurations.
- Configuration-less evaluation remains package-safe. This preserves the recovery from the historical stale-assets defect in which a configuration-less source restore was reused by a Release `--no-restore` build.
- Switching dependency mode requires a new restore before build or test.
- Story 3.5 migrates version authority and adopts the current Builds pins. Story 3.11 owns catalog-wide latest-compatible upgrades, lock-file policy, pruning, and broad dependency refreshes.
- Story 3.6 owns the release-package manifest and final packed-artifact scope. Preserve its boundary.
- Host applications remain source-only. Do not invent packages for AppHost, Admin host applications, or other non-library hosts merely to make the two modes look symmetrical.

### Current baseline at story creation

The story was prepared against EventStore commit `f7b2aa1c4d14c4b7049ce5c6bfb6c82364c55778` and the live planning artifacts on 2026-07-18.

- `Directory.Build.props` currently defaults `UseHexalithProjectReferences` to `false` when no caller value is supplied. Read-only MSBuild evaluation showed package mode for both unset Debug and unset Release, and source mode only with an explicit `true`. Debug-default source selection is therefore still missing.
- Source flags already combine the selected mode with `Exists(...)` checks for the root-declared Commons and Tenants source paths. Preserve those checks and the current repository-layout resolution.
- `Directory.Packages.props` imports the Builds catalog, but still contains the `HexalithCommonsVersion` fallback plus local declarations for `NBomber.Http`, `Microsoft.Playwright`, and `xunit.v3.extensibility.core`.
- Commit `f7b2aa1c` already removed local masking declarations for `System.CommandLine`, `ModelContextProtocol`, `Microsoft.Extensions.TimeProvider.Testing`, and `NBomber`, and added an initial test guard. Preserve that completed work; do not reintroduce those declarations.
- Builds already owns the current adopted versions of `System.CommandLine` (`2.0.10`), `ModelContextProtocol` (`1.4.1`), `Microsoft.Extensions.TimeProvider.Testing` (`10.8.0`), `NBomber` (`6.5.0`), and `Microsoft.Playwright` (`1.61.0`). At the inspected Builds revision, only `NBomber.Http` (`6.2.1`) and `xunit.v3.extensibility.core` (`3.2.2`) still need catalog rows for this migration.
- `scripts/check-doc-versions.sh` reads the EventStore wrapper even though the Dapr version rows it validates are owned by Builds.
- The Builds README and module sample still invite repository-local `PackageVersion` entries. That guidance conflicts with the single-catalog rule.
- EventStore Dependabot still contains a NuGet ecosystem entry that can open competing consumer-local version updates.
- `ContractsPackageDependencyTests.cs` currently allows Microsoft.Playwright as a ledgered local exception. Remove the exception once the shared catalog is the effective source and strengthen the guard to reject all local version declarations and fallback properties.

### Tenants/Gateway authorization gate

The checked-out Tenants project currently has an unconditional source `ProjectReference` to `Hexalith.EventStore.Gateway` while other EventStore dependencies can switch to packages. That is the mixed graph described by AC4. However, Story 2.12 and the hardened Story 1.20 authorization gate explicitly prohibit changing Tenants, EventStore, or Builds dependency identities before the owner authorizes the exact runtime/package identity.

For Story 3.5, the safe and required outcome is therefore:

- Do not edit `references/Hexalith.Tenants` dependency identities.
- Do not add a speculative `Hexalith.EventStore.Gateway` catalog row or package edge.
- Document the Gateway edge as a deliberate, validated source-only exception.
- Tie removal of that exception to authorized Story 2.12 work after Story 1.20 supplies the owner-approved identity.
- Keep the mixed-graph risk visible in `deferred-work.md`; do not mark it resolved.

The explicit exception satisfies only the alternative in AC4's first `Then`; it does **not** by itself satisfy AC4's separate no-mixed-graph `And`. Independent catalog and mode work in this story may proceed, but Story 3.5 cannot become `done` while the current Tenants graph remains mixed. Closure requires either (a) Story 1.20 authorization followed by Story 2.12's approved graph alignment and evidence, or (b) approved change control that explicitly resolves the conflict. Do not infer an identity merely to unblock this story.

## Acceptance Criteria

**AC1 - Unset Debug selects available source dependencies and explicit overrides win.**
**Given** `UseHexalithProjectReferences` is not explicitly set
**When** a Debug build evaluates project references
**Then** external Hexalith project references are enabled when root-declared submodule source exists
**And** developers can override the mode explicitly.

**AC2 - Unset Release selects packages whose versions come only from Builds.**
**Given** `UseHexalithProjectReferences` is not explicitly set
**When** a Release build evaluates project references
**Then** external Hexalith package references are selected by default
**And** every package version resolves from `references/Hexalith.Builds/Props/Directory.Packages.props`.

**AC3 - Each external dependency has exactly one active source per mode.**
**Given** project files reference external Hexalith libraries
**When** source and package modes are evaluated
**Then** each dependency has exactly one active source per mode
**And** host applications that are not library packages are not disguised as package dependencies.

**AC4 - Gateway package-mode behavior is centrally pinned or explicitly excepted.**
**Given** cross-repo consumers such as Tenants depend on reusable EventStore gateway host components
**When** `UseHexalithProjectReferences=false` or Release package mode is selected
**Then** `Hexalith.EventStore.Gateway` is consumed through a centrally pinned `PackageReference` or explicitly documented as a deliberate source-only exception with validation coverage
**And** the dependency graph does not mix a source `Hexalith.EventStore.Gateway` with package-mode EventStore dependencies such as `Hexalith.EventStore.DomainService`, `Client`, `Server`, or `ServiceDefaults`.

**AC5 - Mode switches cannot reuse stale restore assets.**
**Given** dependency mode changes between restores
**When** validation commands run
**Then** restore is rerun before build or test
**And** stale project-reference assets cannot leak into package-mode validation.

**AC6 - Builds is the only source-owned NuGet version authority.**
**Given** any source-owned Hexalith project or root package props is scanned
**When** NuGet version declarations are evaluated
**Then** every dependency version originates from `references/Hexalith.Builds/Props/Directory.Packages.props`
**And** consumer props contain no local `PackageVersion`, `VersionOverride`, or fallback dependency-version property.

**AC7 - Missing EventStore entries move to Builds and evaluate exactly once.**
**Given** EventStore's existing local package-version entries
**When** the catalog migration is applied
**Then** `NBomber.Http` and `xunit.v3.extensibility.core` exist in Builds and all EventStore-local version declarations are removed
**And** effective evaluation resolves each migrated package ID exactly once from Builds.

**AC8 - Current Builds versions are adopted and behaviorally verified.**
**Given** local overrides are removed
**When** package mode restores and focused validation runs
**Then** adoption of the current Builds versions, including `System.CommandLine`, `ModelContextProtocol`, and `Microsoft.Extensions.TimeProvider.Testing`, is explicit and verified
**And** the migration is not accepted as a formatting-only change.

**AC9 - Documentation, scripts, samples, and automation name Builds as owner.**
**Given** package-version documentation, scripts, samples, and dependency-update automation are reviewed
**When** this story completes
**Then** they identify Builds as the owner, `scripts/check-doc-versions.sh` reads the shared catalog successfully, and no official sample invites repository-local package versions
**And** consumer repositories do not open competing local-version updates.

**AC10 - Non-CPM version categories are classified, not rewritten.**
**Given** tool-manifest, SDK, ephemeral consumer-fixture, or cache versions are encountered
**When** the governance scan reports them
**Then** they are classified explicitly
**And** they are not rewritten as NuGet CPM entries.

## Tasks / Subtasks

- [ ] **Task 1 - Reconcile the live baseline and protect ownership boundaries (AC1-AC10).**
  - [ ] Re-read `git status --short --branch`, `git log -5 --oneline`, the relevant planning artifacts, and both repositories' tracked guidance before editing; preserve all user changes made after this story's baseline.
  - [ ] Confirm the EventStore root owns consumer-mode logic, tests, scripts, docs, and its wrapper; confirm `references/Hexalith.Builds` owns the shared catalog, catalog validator, samples, shared workflows, and dependency-update automation.
  - [ ] Confirm Story 2.12 is still gated by Story 1.20. If authorization has not changed, treat Tenants/Gateway as the documented source-only exception and make no dependency-identity change.
  - [ ] Record AC4 as a completion gate while the Gateway source edge still mixes with package-mode EventStore dependencies; do not move this story to `done` based on documentation alone.
  - [ ] Do not initialize/update nested submodules, perform broad dependency upgrades, generate lock files, prune packages, change release-manifest scope, or stage/commit/push unless separately authorized.

- [ ] **Task 2 - Implement and prove the dependency-mode truth table (AC1-AC5).**
  - [ ] Update `Directory.Build.props` so an unset `UseHexalithProjectReferences` evaluates to source intent only for `Configuration=Debug`; unset Release and unset/empty Configuration remain package intent.
  - [ ] Preserve explicit `UseHexalithProjectReferences=true|false` as the highest-precedence override and keep the legacy `UseNuGetDeps` mapping coherent for existing callers.
  - [ ] Preserve `Exists(...)` guards: source intent with a missing root-declared source path must activate the package fallback, not leave both edges inactive.
  - [ ] Add focused evaluation coverage for at least: Debug/unset/source-present, Debug/explicit-false, Release/unset, Release/explicit-true/source-present, empty-configuration/unset, and requested-source/source-missing.
  - [ ] Evaluate all conditional external pairs and prove exactly one active `ProjectReference` or `PackageReference` per dependency and mode. Preserve same-repository EventStore project references as project references.
  - [ ] Preserve deliberate source-only host edges in `Hexalith.EventStore.AppHost` and `Hexalith.EventStore.ServiceDefaults`; do not invent package identities for non-package hosts.
  - [ ] Ensure shared Builds CI/release workflows restore with explicit Release/package intent before any `--no-restore` build/test so the Debug default cannot reintroduce stale assets.

- [ ] **Task 3 - Complete catalog ownership in Hexalith.Builds (AC6-AC10).**
  - [ ] In the Builds repository, add exactly one central `PackageVersion` row for `NBomber.Http` `6.2.1` and exactly one for `xunit.v3.extensibility.core` `3.2.2` at their already-adopted EventStore versions.
  - [ ] Do not change unrelated catalog versions. In particular, use current Builds values for System.CommandLine, ModelContextProtocol, TimeProvider testing, NBomber, and Playwright without widening this task into Story 3.11.
  - [ ] Strengthen `Tools/validate-central-package-versions.ps1` and its focused fixture tests so duplicate effective IDs, missing/blank versions, unresolved properties, and malformed declarations fail closed.
  - [ ] Update `README.md`, `DEVELOPMENT.md`, and `Samples/Module.Directory.Packages.props` so consumers import the catalog and contribute version changes to Builds; remove examples inviting consumer-local `PackageVersion` entries.
  - [ ] Move centralized NuGet dependency-update ownership to Builds. Preserve consumer npm and GitHub Actions automation.
  - [ ] Update the shared domain CI/release workflows and their workflow-contract tests to force a fresh Release/package-mode restore before build/test/package operations.
  - [ ] Validate and, when commits are authorized, commit Builds first. Record the exact Builds SHA before changing the EventStore gitlink; do not bundle unrelated submodule dirt.

- [ ] **Task 4 - Make EventStore an import-only catalog consumer (AC2, AC6-AC8).**
  - [ ] Remove the `HexalithCommonsVersion` fallback and every local `PackageVersion` Include/Update from `Directory.Packages.props`, including the remaining NBomber.Http, Playwright, and xUnit extensibility entries.
  - [ ] Preserve the CPM configuration and the supported Builds import paths; do not add `VersionOverride` or replacement fallback properties elsewhere.
  - [ ] Extend `ContractsPackageDependencyTests.cs` or add one focused packaging-governance test file to assert zero consumer-local package versions, zero `VersionOverride`, zero dependency-version fallback properties, and exact-once effective ownership from Builds.
  - [ ] Remove the temporary Microsoft.Playwright allowlist and prove the effective version is inherited from Builds.
  - [ ] Add effective MSBuild/restore assertions for `NBomber.Http`, `xunit.v3.extensibility.core`, System.CommandLine, ModelContextProtocol, Microsoft.Extensions.TimeProvider.Testing, NBomber, and Playwright without hard-coding versions that merely duplicate the catalog. Where adoption itself matters, compare evaluated values to the Builds source of truth.
  - [ ] Preserve the completed removals in baseline commit `f7b2aa1c`; do not recreate prior consumer-local pins.

- [ ] **Task 5 - Correct ownership guidance and automation in EventStore (AC9-AC10).**
  - [ ] Change `scripts/check-doc-versions.sh` to read `references/Hexalith.Builds/Props/Directory.Packages.props`; preserve its exact-one Dapr-row, family-consistency, documented-row-count, Bash-version, and LF guards.
  - [ ] Remove the EventStore NuGet entry from `.github/dependabot.yml` so the consumer cannot propose competing local catalog changes; retain npm and GitHub Actions entries.
  - [ ] Correct active owner guidance in `_bmad-output/project-context.md`, `docs/brownfield/development-guide.md`, `docs/brownfield/project-overview.md`, `docs/brownfield/source-tree-analysis.md`, `docs/reference/nuget-packages.md`, and any directly impacted operational guide found by the scan.
  - [ ] Preserve legitimate downstream instructions that tell an independent consuming application to manage its own CPM file. Do not rewrite historical proposals or refresh unrelated version tables.
  - [ ] Record `dotnet-tools.json` versions, `global.json` SDK selection, ephemeral package-consumer fixture props, and generated `.csproj.lscache` metadata as deliberate non-CPM categories. Do not move them into the Builds catalog.
  - [ ] Update `deferred-work.md` narrowly: close the Playwright masking item only after validation passes, and retain the Gateway mixed-graph item with its Story 1.20/2.12 removal trigger.

- [ ] **Task 6 - Validate and fail closed on the Gateway completion gate without bypassing authorization (AC3-AC4).**
  - [ ] Inspect the current Tenants graph read-only and record the unconditional Gateway source edge plus the package-mode EventStore edges it can mix with.
  - [ ] Add root-owned validation or a governance scan that fails if the documented exception silently expands to another dependency or is marked resolved without the authorized Story 2.12 identity.
  - [ ] Document the exception, owner, risk, and removal trigger. Do not edit `references/Hexalith.Tenants`, add a speculative Gateway package row, or change EventStore package identities under this story.
  - [ ] Keep Story 3.5 `in-progress` while the graph remains mixed. The exception alone cannot satisfy AC4's final `And` criterion.
  - [ ] If owner authorization exists, stop and reconcile the now-obsolete exception with Story 1.20/2.12; consume Story 2.12's approved graph-alignment evidence rather than implementing an identity change here.

- [ ] **Task 7 - Run fresh dual-mode and governance validation (AC1-AC10).**
  - [ ] Run the Builds catalog validator and its focused tests before updating the EventStore gitlink.
  - [ ] Run MSBuild evaluation-only checks for every dependency-mode truth-table row and for exact-once effective package versions.
  - [ ] Restore and build Debug/source mode from a fresh restore; then rerun restore before Release/package build and tests. Never reuse the source-mode assets with `--no-restore` in package mode.
  - [ ] Run focused EventStore package-governance, Admin CLI, Admin MCP, TimeProvider, integration-testing, UI E2E build, and load-test build coverage needed to prove adoption of inherited versions.
  - [ ] Run `scripts/check-doc-versions.sh`, documentation/automation ownership scans, `git diff --check`, and repository-specific workflow contract tests.
  - [ ] Record commands, result counts, effective versions, both repository SHAs, intentional exclusions, and any environment blocker in the Dev Agent Record.

## Dev Notes

### Top Guardrails

- **Do not conflate default intent with source availability.** Debug defaults to source intent, but each external dependency activates a source edge only when its declared source path exists. Missing source must select the package edge.
- **Keep empty Configuration package-safe.** The historical sequence `946016ce` -> `d1b6739c` -> `9333405e` shows why configuration-less source restore is unsafe when a later Release build uses `--no-restore`.
- **Explicit caller values win.** Workflows and releases should continue passing `UseHexalithProjectReferences=false`; developers can explicitly opt either way.
- **One dependency, one active edge.** A package/project pair must be mutually exclusive after evaluation. Host projects are deliberate source-only edges, not missing package declarations.
- **Builds owns versions; consumers own references.** Project files keep versionless `PackageReference` items. The EventStore wrapper only configures CPM and imports Builds.
- **No broad upgrades.** Story 3.5 adds two missing catalog IDs and adopts catalog values already present. Story 3.11 owns the catalog-wide compatibility refresh.
- **Honor the identity gate.** No Gateway identity inference or Tenants mutation is allowed while Story 1.20/2.12 remains gated.
- **Preserve current work.** The baseline includes a just-completed partial cleanup. Re-read the live worktree before each overlapping edit and never restore removed local declarations.

### Dependency-mode truth table

| Configuration | Explicit `UseHexalithProjectReferences` | Root-declared source exists | Expected external edge |
|---|---:|---:|---|
| Debug | unset | yes | project/source |
| Debug | unset | no | package |
| Debug | `false` | either | package |
| Debug | `true` | yes | project/source |
| Debug | `true` | no | package fallback |
| Release | unset | either | package |
| Release | `false` | either | package |
| Release | `true` | yes | project/source |
| Release | `true` | no | package fallback |
| empty/unset | unset | either | package |

`UseNuGetDeps` is a compatibility input, not a second independent mode. Preserve its existing mapping, but normalize the final decision into one authoritative boolean so contradictory project/package edges cannot become active together.

### File-level implementation map

| File or area | Action | Required outcome |
|---|---|---|
| `Directory.Build.props` | UPDATE | Deterministic Debug/source, Release/package, explicit-override, empty-configuration-safe evaluation. |
| `Directory.Packages.props` | UPDATE | Import-only consumer wrapper; no fallback property, local `PackageVersion`, or override. |
| `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.cs` | UPDATE | Zero-local-version guard and effective Builds ownership/adoption checks; remove Playwright exception. |
| Focused mode-evaluation test file | UPDATE or NEW | Cover every truth-table boundary and exactly-one active edge. Use one public type per file and PascalCase for new test methods. |
| `scripts/check-doc-versions.sh` | UPDATE | Read the shared Builds catalog while retaining existing fail-closed document checks. |
| `.github/dependabot.yml` | UPDATE | No consumer-local NuGet catalog updates; preserve npm/actions automation. |
| Root-owned package/version docs and `_bmad-output/project-context.md` | UPDATE | Builds named as owner; no competing consumer examples or unrelated catalog refresh. |
| `_bmad-output/implementation-artifacts/deferred-work.md` | UPDATE | Close Playwright masking only when proven; keep Gateway gate visible. |
| `references/Hexalith.Builds/Props/Directory.Packages.props` | UPDATE in Builds | Add only NBomber.Http and xUnit extensibility rows required by this story. |
| Builds validator/tests, README, DEVELOPMENT, and sample | UPDATE in Builds | Enforce and teach single-catalog ownership. |
| Builds shared domain CI/release workflows and contract tests | UPDATE in Builds | Explicit fresh Release/package restore before `--no-restore` operations. |
| `references/Hexalith.Tenants/**` | READ-ONLY unless separately authorized | Record the Gateway exception; make no dependency-identity change under the active gate. |
| EventStore AppHost/ServiceDefaults host edges | PRESERVE | Source-only hosts remain source-only; no fake package dependencies. |
| `tools/release-packages.json`, thin root workflow callers, pack tooling | PRESERVE | Story 3.6/3.7/3.8 boundaries remain intact. |

### Architecture and project conventions

- Use `.NET SDK 10.0.302`, `net10.0`, nullable reference types, implicit usings, and warnings as errors.
- Use `Hexalith.EventStore.slnx`; do not create or use a legacy `.sln`.
- Tests use xUnit v3, Shouldly, and NSubstitute. Run test projects individually, not a solution-level `dotnet test`.
- New C# test method names use PascalCase even though older packaging tests use underscore names.
- Root C# uses four spaces and CRLF; Markdown and shell files use LF. Builds has its own `.editorconfig`; preserve its catalog/sample encoding and line endings.
- Keep root workflow callers thin. Package-mode enforcement belongs in shared Builds workflows and the manifest-driven packaging layer, not duplicated root workflow logic.
- Keep NuGet audit enabled. Do not hide dependency findings with `NuGetAudit=false`; report an environmental feed blocker honestly if one occurs.

### Existing reference patterns to preserve

- `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj` already pairs Commons UniqueIds source/package edges with mutually exclusive conditions.
- `src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj` and the Server/LiveSidecar test projects already pair Tenants Contracts source/package edges.
- `src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj` deliberately references Commons.ServiceDefaults only from source when available; it is a hosting helper without a fake package fallback.
- `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` and its host wiring use Tenants hosts as source applications. Preserve that topology.
- Same-repository `Hexalith.EventStore.*` references remain project references in both modes.

### Previous Story Intelligence

Story 3.3 established and verified the root `references/` layout. Reuse its path guardrails:

- Resolve source checkouts through root-declared `references/Hexalith.*` paths and existing flexible fallbacks.
- Do not move submodules, re-run the references migration, initialize nested submodules, or replace flexible source-path resolution with fixed assumptions.
- Work in the repository that owns each change and keep a Builds commit/gitlink update isolated when commits are authorized.

Stories 3.7 and 3.8 have already made root workflows thin and hardened shared-reference/cache behavior. Preserve that design while fixing package-mode restore intent in the owning shared Builds workflows.

### Git Intelligence

- `946016ce` introduced conditional source/package pairs, a Debug-source default, and explicit Release/package workflow values. Its mode shape is useful, but current layout/existence guards remain authoritative.
- `d1b6739c` made empty Configuration select source mode and exposed stale project assets.
- `9333405e` restored package-safe behavior for empty/unqualified Configuration. Do not regress it while reinstating the explicit Debug default.
- `3a43d5e6` demonstrates the desired atomic catalog migration: add entries to Builds, update the gitlink, remove consumer declarations, and validate ownership/effective resolution without copying version literals into consumer tests.
- `f7b2aa1c` is the current baseline and already removes four local masks plus adds an initial central-version guard. Treat this as partial Story 3.5 groundwork that must be preserved and completed.

### Latest Technical Information

- NuGet Central Package Management keeps versions in central `PackageVersion` items while project `PackageReference` items remain versionless. `VersionOverride` takes precedence over the central value, and transitive pinning can promote pinned dependencies into generated package metadata; both are reasons to ban consumer-local masks and validate packed metadata deliberately. Source: https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management
- MSBuild's `-getProperty` and `-getItem` options can inspect evaluated properties and items without running a build target, making them appropriate for the dependency-mode truth-table tests. Multiple queried values are returned as JSON. Source: https://learn.microsoft.com/en-us/visualstudio/msbuild/evaluate-items-and-properties
- MSBuild conditions compare evaluated configuration/property values; keep the condition ordering explicit and test empty values as their own case. Source: https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-conditional-constructs
- PackageReference restore writes the resolved dependency graph to `obj/project.assets.json`. After a project/package mode switch, rerun restore before any `--no-restore` build or test. Sources: https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files and https://learn.microsoft.com/en-us/nuget/consume-packages/package-restore-troubleshooting
- Git submodule changes are gitlink changes in the superproject. Work in the submodule repository first and do not use recursive update commands for this task. Sources: https://git-scm.com/docs/gitsubmodules and https://git-scm.com/docs/git-submodule

### Validation Commands

Run from the owning repository root. Reconcile exact project paths with the live tree before execution.

```bash
# EventStore: inspect evaluated defaults without building.
dotnet msbuild src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj \
  -p:Configuration=Debug \
  -getProperty:UseHexalithProjectReferences,UseNuGetDeps,HexalithCommonsFromSource \
  -getItem:ProjectReference,PackageReference

dotnet msbuild src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj \
  -p:Configuration=Release \
  -getProperty:UseHexalithProjectReferences,UseNuGetDeps,HexalithCommonsFromSource \
  -getItem:ProjectReference,PackageReference

# Builds: catalog governance. Run the owning scripts from references/Hexalith.Builds.
pwsh ./Tools/validate-central-package-versions.ps1
pwsh ./Tools/test-central-package-version-validator.ps1

# EventStore: fresh source-mode restore/build.
dotnet restore Hexalith.EventStore.slnx \
  -p:Configuration=Debug \
  -p:UseHexalithProjectReferences=true
dotnet build Hexalith.EventStore.slnx \
  --configuration Debug \
  --no-restore \
  -m:1 \
  -p:UseHexalithProjectReferences=true

# EventStore: rerun restore after switching to package mode.
dotnet restore Hexalith.EventStore.slnx \
  -p:Configuration=Release \
  -p:UseHexalithProjectReferences=false
dotnet build Hexalith.EventStore.slnx \
  --configuration Release \
  --no-restore \
  -m:1 \
  -p:UseHexalithProjectReferences=false

# Focused package-governance tests. Add other affected project tests/builds found during implementation.
dotnet test tests/Hexalith.EventStore.Contracts.Tests/ \
  --configuration Release \
  --no-restore \
  -p:UseHexalithProjectReferences=false

bash scripts/check-doc-versions.sh
git diff --check
```

Also evaluate explicit `true`, explicit `false`, empty Configuration, and missing-source fixtures; the two default commands above are necessary but not sufficient. Build focused System.CommandLine, MCP, TimeProvider, load-test, and Playwright consumers to prove inherited versions actually restore and compile.

### References

- FR21 and NFR9: `_bmad-output/planning-artifacts/prd.md`
- Story 3.5 canonical ACs and Epic 3 sequencing: `_bmad-output/planning-artifacts/epics.md`
- AD-11 and build/package invariants: `_bmad-output/planning-artifacts/architecture.md`
- Approved scope correction and catalog inventory: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18.md`
- Epic guardrails and cross-story dependencies: `_bmad-output/implementation-artifacts/epic-3-context.md`
- Repository-wide implementation rules: `_bmad-output/project-context.md`
- References-layout precedent: `_bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md`
- Active exception/removal ledger: `_bmad-output/implementation-artifacts/deferred-work.md`

## Dev Agent Record

### Agent Model Used

<!-- Record implementation agent/model. -->

### Debug Log References

<!-- Record investigation notes, exact commands, effective package values, and blockers. -->

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

<!-- List every added, modified, or deleted file in both owning repositories. Record Builds SHA and EventStore gitlink change separately. -->

## Change Log

- 2026-07-18: Story created from the approved FR21 correction, current repository baseline, prior-story/history analysis, and official MSBuild/NuGet/Git guidance; marked `ready-for-dev`.
