---
baseline_commit: f7b2aa1c4d14c4b7049ce5c6bfb6c82364c55778
created: 2026-07-18
story_key: 3-5-shared-package-catalog-and-source-package-reference-modes
epic: "Epic 3 - Release And Repository Reliability"
requirements: FR21
governing_nfr: NFR9
architecture_decision: AD-11
story_type: cross-repository-build-governance
implementation_decision: >-
  The Administrator approved explicit source opt-in on 2026-07-18. Unset or explicit
  UseHexalithProjectReferences=false selects packages in every configuration, including
  Debug. Explicit true selects available root-declared source and otherwise falls back
  to the centrally pinned package edge.
completion_gate: >-
  AC4 cannot close while Story 1.20 has not authorized Story 2.12's exact
  EventStore runtime/package identity; independent Story 3.5 work may proceed,
  but the story must remain in-progress until the no-mixed-graph criterion is proven
  or approved change control revises the conflicting boundary.
scope_decision: >-
  Story 3.5 catalog-migration acceptance is limited to Hexalith.Builds and EventStore.
  Other repositories with local version declarations require separately owned follow-ups
  and are not edited or claimed as migrated by this story.
sequencing_gate: >-
  Story 3.3 underpins Story 3.5 and must reach done with current verification evidence
  before Story 3.5 implementation begins.
source_files:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18-story-3-5-reconciliation.md
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

> Tracker note: this repository defines `ready-for-dev` as “story file created.” The AC1 authority and AC6 boundary are resolved, but Story 3.3 must reach `done` before implementation starts and AC4 remains a completion gate.

## Story

As a **package maintainer**,
I want **external Hexalith dependencies selected deterministically by build intent and every NuGet version owned by the shared Hexalith.Builds catalog**,
so that **Debug builds can source-debug, Release builds depend on published packages, and consumer repositories cannot silently mask or compete with shared package-version updates**.

## Story Context

Story 3.5 targets FR21 under NFR9 and AD-11. Its dependency-mode authority and catalog-migration boundary were reconciled by the approved 2026-07-18 course correction. It is a coordinated change across the EventStore repository and the root-declared `references/Hexalith.Builds` repository. Work in each owning repository separately; commit Builds changes first only when the maintainer authorizes commits, then update the EventStore gitlink in an isolated dependency commit. Do not initialize or update nested submodules.

The approved 2026-07-18 correction establishes these boundaries:

- `references/Hexalith.Builds/Props/Directory.Packages.props` is the single authority for source-owned NuGet dependency versions.
- Source project references require explicit `UseHexalithProjectReferences=true` plus available root-declared source. Unset or explicit `false` selects packages in Debug, Release, and configuration-less evaluation; requested source with missing source falls back to packages.
- Configuration-less evaluation remains package-safe. This preserves the recovery from the historical stale-assets defect in which a configuration-less source restore was reused by a Release `--no-restore` build.
- Switching dependency mode requires a new restore before build or test.
- Story 3.5 migrates version authority and adopts the current Builds pins. Story 3.11 owns catalog-wide latest-compatible upgrades, lock-file policy, pruning, and broad dependency refreshes.
- Story 3.6 owns the release-package manifest and final packed-artifact scope. Preserve its boundary.
- Host applications remain source-only. Do not invent packages for AppHost, Admin host applications, or other non-library hosts merely to make the two modes look symmetrical.

### Requirements reconciliation decision

The Administrator approved FR21/AD-11 as the governing rule on 2026-07-18: source mode requires explicit `UseHexalithProjectReferences=true`; unset or explicit `false` is package intent in every configuration. The approved course-correction proposal aligns AC1, the governing planning artifacts, this truth table, and the implementation tasks to that decision.

### Catalog-scope decision

The Administrator approved Builds+EventStore as Story 3.5's implementation boundary. AC6 is satisfied by the shared Builds catalog/governance surfaces and EventStore-owned projects/root props. Repositories outside that boundary are not edited or claimed as migrated; each repository that retains local version declarations must receive a separately owned follow-up with authority, scope, rollback, and validation details.

### Story 3.3 sequencing gate

Epic 3 identifies Story 3.3's references layout as the foundation for Story 3.5. Story 3.3 must complete its normal development and review workflows and reach `done` with current verification evidence before Story 3.5 begins; do not silently mark it done inside this story.

### Current baseline at story creation

The story was prepared against EventStore commit `f7b2aa1c4d14c4b7049ce5c6bfb6c82364c55778` and the live planning artifacts on 2026-07-18.

- `Directory.Build.props` currently defaults `UseHexalithProjectReferences` to `false` when no caller value is supplied. Read-only MSBuild evaluation showed package mode for both unset Debug and unset Release, and source mode only with an explicit `true`. That matches the approved AC1/FR21/AD-11 rule and must remain covered by focused evaluation tests.
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

**AC1 - Source references require explicit opt-in and explicit overrides win.**
**Given** `UseHexalithProjectReferences=true` is explicitly supplied
**When** a build evaluates external Hexalith references and the root-declared source exists
**Then** the project/source edge is selected
**And** missing source falls back to the centrally pinned package edge.

**Given** `UseHexalithProjectReferences` is unset or explicitly `false`
**When** Debug, Release, or configuration-less evaluation runs
**Then** package references are selected
**And** no external source edge is activated.

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

**AC6 - Builds is the only NuGet version authority for the Story 3.5 implementation boundary.**
**Given** EventStore-owned projects/root package props and the shared Builds catalog/governance surfaces are scanned
**When** NuGet version declarations are evaluated
**Then** every EventStore-consumed dependency version originates from `references/Hexalith.Builds/Props/Directory.Packages.props`
**And** EventStore consumer props contain no local `PackageVersion`, `VersionOverride`, or fallback dependency-version property.

**Given** another Hexalith repository retains local version declarations
**When** Story 3.5 closes its approved boundary
**Then** a separately owned migration follow-up records that repository, owner/approval requirement, scope, rollback boundary, and prescribed validation
**And** Story 3.5 does not edit that repository or claim it migrated.

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

- [ ] **Task 1 - Apply the approved decisions and protect ownership boundaries (AC1-AC10).**
  - [ ] Re-read `git status --short --branch`, `git log -5 --oneline`, the relevant planning artifacts, and both repositories' tracked guidance before editing; preserve all user changes made after this story's baseline.
  - [ ] Apply the approved explicit-opt-in rule consistently: unset or explicit `false` remains package mode in every configuration, while explicit `true` selects available source and otherwise falls back to packages.
  - [ ] Enforce the approved Builds+EventStore AC6 boundary and register separately owned migration follow-ups for other repositories that retain local versions. Do not mutate or claim compliance for those repositories.
  - [ ] Confirm Story 3.3 has reached `done` with current verification evidence before treating its references-layout guarantee as a completed prerequisite.
  - [ ] Confirm the EventStore root owns consumer-mode logic, tests, scripts, docs, and its wrapper; confirm `references/Hexalith.Builds` owns the shared catalog, catalog validator, samples, shared workflows, and dependency-update automation.
  - [ ] Confirm Story 2.12 is still gated by Story 1.20. If authorization has not changed, treat Tenants/Gateway as the documented source-only exception and make no dependency-identity change.
  - [ ] Record AC4 as a completion gate while the Gateway source edge still mixes with package-mode EventStore dependencies; do not move this story to `done` based on documentation alone.
  - [ ] Do not initialize/update nested submodules, perform broad dependency upgrades, generate lock files, prune packages, change release-manifest scope, or stage/commit/push unless separately authorized.

- [ ] **Task 2 - Implement and prove the approved dependency-mode truth table (AC1-AC5).**
  - [ ] Keep `Directory.Build.props` package-safe for unset or explicit `false` in Debug, Release, and empty/unset Configuration; activate source only for explicit `true` when the root-declared path exists.
  - [ ] Preserve explicit `UseHexalithProjectReferences=true|false` as the highest-precedence override and keep the legacy `UseNuGetDeps` mapping coherent for existing callers.
  - [ ] Add contradictory-input cases where both properties are supplied. Explicit `UseHexalithProjectReferences` is authoritative and `UseNuGetDeps` must not activate the opposite edge.
  - [ ] Preserve `Exists(...)` guards: source intent with a missing root-declared source path must activate the package fallback, not leave both edges inactive.
  - [ ] Add focused evaluation coverage for at least: Debug/unset/source-present (package), Debug/explicit-false, Release/unset, Release/explicit-true/source-present, empty-configuration/unset, and requested-source/source-missing.
  - [ ] Evaluate all conditional external pairs and prove exactly one active `ProjectReference` or `PackageReference` per dependency and mode. Preserve same-repository EventStore project references as project references.
  - [ ] Preserve genuine source-only application-host edges in `Hexalith.EventStore.AppHost`; do not invent package identities for non-package applications.
  - [ ] Reconcile `Hexalith.EventStore.ServiceDefaults` separately: it is a packable library, and `Hexalith.Commons.ServiceDefaults` has a central package identity. Determine whether the current Commons project edge is required; if required, add a mutually exclusive versionless package edge in package mode, otherwise remove the redundant project edge. Validate resulting package metadata.
  - [ ] Ensure shared Builds CI/release workflows restore with explicit Release/package intent before any `--no-restore` build/test so mode switches cannot reuse stale assets.

- [ ] **Task 3 - Complete catalog ownership in Hexalith.Builds (AC6-AC10).**
  - [ ] In the Builds repository, add exactly one central `PackageVersion` row for `NBomber.Http` `6.2.1` and exactly one for `xunit.v3.extensibility.core` `3.2.2` at their already-adopted EventStore versions.
  - [ ] Do not change unrelated catalog versions. In particular, use current Builds values for System.CommandLine, ModelContextProtocol, TimeProvider testing, NBomber, and Playwright without widening this task into Story 3.11.
  - [ ] Strengthen `Tools/validate-central-package-versions.ps1` and its focused fixture tests so duplicate effective IDs, missing/blank versions, unresolved properties, and malformed declarations fail closed.
  - [ ] Update `README.md`, `DEVELOPMENT.md`, and `Samples/Module.Directory.Packages.props` so consumers import the catalog and contribute version changes to Builds; remove examples inviting consumer-local `PackageVersion` entries.
  - [ ] Move centralized NuGet dependency-update ownership to Builds. Preserve consumer npm and GitHub Actions automation.
  - [ ] Update the shared domain CI/release workflows and their workflow-contract tests to force a fresh Release/package-mode restore before build/test/package operations.
  - [ ] Apply that workflow work in `.github/workflows/domain-ci.yml`, `.github/workflows/domain-release.yml`, and `.github/workflows/build-release.yml`, with contract coverage in `Tools/test-domain-workflow-test-platforms.ps1`.
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

- **Do not conflate explicit source intent with source availability.** Each external dependency activates a source edge only for explicit `UseHexalithProjectReferences=true` when its declared source path exists; missing source must select the package edge.
- **Keep empty Configuration package-safe.** The historical sequence `946016ce` -> `d1b6739c` -> `9333405e` shows why configuration-less source restore is unsafe when a later Release build uses `--no-restore`.
- **Explicit caller values win.** Workflows and releases should continue passing `UseHexalithProjectReferences=false`; developers can explicitly opt either way.
- **One dependency, one active edge.** A package/project pair must be mutually exclusive after evaluation. Host projects are deliberate source-only edges, not missing package declarations.
- **Builds owns versions; consumers own references.** Project files keep versionless `PackageReference` items. The EventStore wrapper only configures CPM and imports Builds.
- **No broad upgrades.** Story 3.5 adds two missing catalog IDs and adopts catalog values already present. Story 3.11 owns the catalog-wide compatibility refresh.
- **Honor the identity gate.** No Gateway identity inference or Tenants mutation is allowed while Story 1.20/2.12 remains gated.
- **Honor the approved catalog boundary.** Builds+EventStore is the Story 3.5 implementation boundary. Register separately owned follow-ups for other repositories; do not edit or claim compliance for them.
- **Do not treat ServiceDefaults as an application host.** `Hexalith.EventStore.ServiceDefaults` is packable and Builds centrally identifies both EventStore and Commons ServiceDefaults packages; its external edge must be paired for package mode or proven unnecessary.
- **Preserve current work.** The baseline includes a just-completed partial cleanup. Re-read the live worktree before each overlapping edit and never restore removed local declarations.

### Dependency-mode truth table (approved 2026-07-18)

| Configuration | Explicit `UseHexalithProjectReferences` | Root-declared source exists | Expected external edge |
|---|---:|---:|---|
| Debug | unset | either | package |
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
| `Directory.Build.props` | UPDATE or VERIFY | Explicit source opt-in; package-safe unset/false defaults in every configuration; missing-source package fallback. |
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
| `src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj` | UPDATE or VERIFY | Because this project is packable, pair the current Commons.ServiceDefaults source edge with its central package identity when required, or remove the edge if proven unnecessary; validate packed metadata. |
| EventStore AppHost application edges | PRESERVE | Genuine application hosts remain source-only; no fake package dependencies. |
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
- `src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj` is a packable library but currently references Commons.ServiceDefaults only from source. Because Builds already has a `Hexalith.Commons.ServiceDefaults` identity, implementation must either create a mutually exclusive source/package pair or prove and remove an unnecessary edge.
- `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` and its host wiring use Tenants hosts as source applications. Preserve that topology.
- Same-repository `Hexalith.EventStore.*` references remain project references in both modes.

### Previous Story Intelligence

Story 3.3 must reach `done` with current verification evidence before Story 3.5 relies on its root `references/` layout guarantee. After that prerequisite completes, reuse its path guardrails:

- Resolve source checkouts through root-declared `references/Hexalith.*` paths and existing flexible fallbacks.
- Do not move submodules, re-run the references migration, initialize nested submodules, or replace flexible source-path resolution with fixed assumptions.
- Work in the repository that owns each change and keep a Builds commit/gitlink update isolated when commits are authorized.

Stories 3.7 and 3.8 have already made root workflows thin and hardened shared-reference/cache behavior. Preserve that design while fixing package-mode restore intent in the owning shared Builds workflows.

### Git Intelligence

- `946016ce` introduced conditional source/package pairs, a Debug-source default, and explicit Release/package workflow values. Its mode shape is useful, but current layout/existence guards remain authoritative.
- `d1b6739c` made empty Configuration select source mode and exposed stale project assets.
- `9333405e` restored package-safe behavior for empty/unqualified Configuration. Do not regress it while preserving explicit source opt-in.
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
pwsh ./Tools/test-domain-workflow-test-platforms.ps1

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

# Focused package-governance tests.
dotnet test tests/Hexalith.EventStore.Contracts.Tests/ \
  --configuration Release \
  --no-restore \
  -p:UseHexalithProjectReferences=false

# Explicit consumers proving inherited System.CommandLine and MCP versions.
dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests/ \
  --configuration Release --no-restore -p:UseHexalithProjectReferences=false
dotnet test tests/Hexalith.EventStore.Admin.Mcp.Tests/ \
  --configuration Release --no-restore -p:UseHexalithProjectReferences=false

# TimeProvider consumers and integration package adoption.
dotnet test tests/Hexalith.EventStore.Server.Tests/ \
  --configuration Release --no-restore -p:UseHexalithProjectReferences=false
dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/ \
  --configuration Release --no-restore -p:UseHexalithProjectReferences=false
dotnet test tests/Hexalith.EventStore.Testing.Integration.Tests/ \
  --configuration Release --no-restore -p:UseHexalithProjectReferences=false

# Build-only consumers proving Playwright and NBomber inheritance.
dotnet build tests/Hexalith.EventStore.Admin.UI.E2E/ \
  --configuration Release --no-restore -p:UseHexalithProjectReferences=false
dotnet build perf/Hexalith.EventStore.LoadTests/ \
  --configuration Release --no-restore -p:UseHexalithProjectReferences=false

bash scripts/check-doc-versions.sh
git diff --check
```

Also evaluate explicit `true`, explicit `false`, empty Configuration, missing-source fixtures, and both contradictory legacy-property combinations. The focused test harness must assert that explicit `UseHexalithProjectReferences` is authoritative and prove exactly one active edge. Inspect the targeted ServiceDefaults package metadata when its Commons edge is reconciled. The commands above are necessary evidence, not permission to omit any additional affected project discovered during implementation.

### References

- FR21 and NFR9: `_bmad-output/planning-artifacts/prd.md`
- Story 3.5 canonical ACs and Epic 3 sequencing: `_bmad-output/planning-artifacts/epics.md`
- AD-11 and build/package invariants: `_bmad-output/planning-artifacts/architecture.md`
- Approved scope correction and catalog inventory: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18.md`
- Approved AC1/AC6 reconciliation and sequencing decision: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18-story-3-5-reconciliation.md`
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
- 2026-07-18: Fresh-context checklist review encoded the AC1 authority, AC4 Gateway, AC6 ecosystem-scope, and Story 3.3 sequencing gates; corrected ServiceDefaults package treatment and made workflow/consumer validation executable.
- 2026-07-18: Administrator-approved Correct Course aligned AC1 to explicit source opt-in, narrowed AC6 to Builds+EventStore with separately owned follow-ups, and made Story 3.3 `done` a start gate while retaining AC4 as the completion gate.
