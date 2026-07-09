---
baseline_commit: 0f428d0c914f2151aab15bb262f956a9630041dc
created: 2026-07-09
story_key: 3-3-references-based-submodule-layout
epic: "Epic 3 - Release And Repository Reliability"
requirements: FR19
governing_nfr: NFR9
story_type: verification-and-reconciliation
correct_course: >-
  The references/ submodule layout was already implemented as a direct correction from
  sprint-change-proposal-2026-06-26-submodule-references.md and approved on 2026-07-01.
  This story is therefore re-scoped from IMPLEMENT to VERIFY-AND-RECONCILE by the
  Correct-Course Story Rewrite Gate. Active scope is to verify that the shipped layout still
  satisfies FR19, patch only confirmed stale root-level path regressions, and reconcile the
  sprint ledger/done evidence. Do not re-run a submodule migration and do not replace the
  current flexible RepositoryProjectPaths helper with a fixed references-only path.
source_files:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-26-submodule-references.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-09.md
  - _bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md
  - _bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md
  - .gitmodules
  - AGENTS.md
  - CLAUDE.md
  - .github/copilot-instructions.md
  - Directory.Build.props
  - Directory.Packages.props
  - Hexalith.EventStore.slnx
  - docs/brownfield/source-tree-analysis.md
  - src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs
  - src/Hexalith.EventStore.Aspire/EventStorePlatformProjectMetadata.cs
  - tests/Hexalith.EventStore.AppHost.Tests/Configuration/RepositoryProjectPathsTests.cs
---

# Story 3.3: References-Based Submodule Layout

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

<!-- CORRECT-COURSE REWRITE (2026-07-09):
     FR19's direct implementation already shipped via sprint-change-proposal-2026-06-26-submodule-references.md
     and was approved on 2026-07-01. The proposal records developer implementation and validation as
     complete: git diff check, restore, Release build, and AppHost tests. This story is not a greenfield
     migration. Preserve the original epic ACs under traceability, but implement this story as verification
     and reconciliation. Patch only verified stale root-level Hexalith.* path regressions. -->

## Story

As a **repository maintainer**,
I want **to verify that root-declared Hexalith submodules already live under `references/`, that build/docs/Aspire path resolution still use that layout, and that the sprint ledger is reconciled with the approved June 26 correction**,
so that **FR19 is proven at the current baseline without re-running a risky submodule migration or reviving root-level `Hexalith.*` path assumptions**.

## Story Context

**This is a verification-and-reconciliation story, not a greenfield implementation.** FR19 requires root-declared Git submodules to live under `references/`, and solution, project, documentation, Aspire metadata, and LLM instruction paths to resolve through that layout (`prd.md:139`, `prd.md:313`). Story 3.3's original epic ACs are in `epics.md:909-937`.

The implementation already happened before this story file existed:

- `sprint-change-proposal-2026-06-26-submodule-references.md:5-19` records the requested move from root-level `Hexalith.*` directories into `references/` and the technical impact across `.gitmodules`, MSBuild path resolution, Tenants local path resolution, and Aspire metadata.
- The same proposal records the new layout (`:47-57`), build/solution path requirements (`:61-79`), LLM/doc path requirements (`:81-99`), Aspire metadata intent (`:101-126`), success criteria (`:134-140`), completed validation (`:142-147`), and final approval (`:157-163`).
- Therefore the active implementation stance is: **verify current state, patch only confirmed regressions, and record evidence.**

Current baseline facts read during story creation (`0f428d0c`):

- `.gitmodules:1-21` declares all seven root submodules under `references/`: Tenants, AI.Tools, Commons, Builds, FrontComposer, PolymorphicSerializations, and Memories.
- Root instructions use the `references/Hexalith.AI.Tools` path and explicitly forbid nested submodule initialization (`AGENTS.md:3-14`, `CLAUDE.md:3-14`, `.github/copilot-instructions.md:3-14`).
- `Directory.Build.props:4-10` imports Hexalith.Builds package props through `references/`; `:13-30` resolves Tenants source with the local `references/Hexalith.Tenants` path first; `:40-42` resolves Commons source from `references/Hexalith.Commons` first.
- `Directory.Packages.props:5-15` imports Hexalith.Builds package props through `references/` paths.
- `Hexalith.EventStore.slnx:1-67` is the only solution file and contains `references/Hexalith.Builds` solution items; active EventStore projects stay under `src/`, `samples/`, `tests/`, and `perf/`.
- `docs/brownfield/source-tree-analysis.md:29-36` is acceptable: the bare `Hexalith.*` names are shown as children under the `references/` tree node, not root-level directories.
- `RepositoryProjectPaths.GetReferencedModuleProjectPath` is the current architecture, not the older fixed snippet in the June 26 proposal. It supports the current repository, nested dependency checkouts, `references/` fallback, and parent references layouts (`RepositoryProjectPaths.cs:43-95`). `EventStorePlatformProjectMetadata.cs:6-23` uses that helper for the gateway project; `:29-60` uses it for Admin hosts.
- Existing AppHost tests pin current behavior: current-repository project metadata resolves to this repo's `src/Hexalith.EventStore` (`RepositoryProjectPathsTests.cs:20-29`), and a missing module falls back to `references/<module>/...` (`:31-46`).

The approved 2026-07-09 CI/CD proposal affects Story 3.7, not Story 3.3. Do not fold Tenants-style CI/release workflow migration into this story.

## Acceptance Criteria

> **Verification stance:** every AC is satisfied by observing and recording evidence at the current baseline. Make code or documentation changes only when verification finds a real stale root-level `Hexalith.*` path or a broken `references/` resolution path.

**AC1 - Root-declared submodules are all under `references/`.**
**Given** root-declared submodules are configured,
**When** `.gitmodules` is inspected with `git config -f .gitmodules --get-regexp '^submodule\..*\.path$'`,
**Then** every path begins with `references/`,
**And** `find . -maxdepth 1 -type d -name 'Hexalith.*' -print` returns no root-level submodule directories,
**And** no nested submodule is initialized or updated as part of this story.

**AC2 - Restore/build use the `.slnx` and resolve Hexalith source/package paths through `references/`.**
**Given** the solution and MSBuild props are evaluated,
**When** `dotnet restore Hexalith.EventStore.slnx -p:UseHexalithProjectReferences=false` and `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` run,
**Then** restore and Release build succeed without requiring root-level `Hexalith.*` directories,
**And** `Directory.Build.props` and `Directory.Packages.props` keep `references/Hexalith.Builds`, `references/Hexalith.Tenants`, and `references/Hexalith.Commons` as the local/default paths,
**And** no `.sln` file is created or used.

**AC3 - Documentation and LLM instructions point at the `references/` layout.**
**Given** documentation, generated API reference docs, Aspire metadata, and LLM instructions mention Hexalith submodules,
**When** repository-wide path scans run over root-owned files,
**Then** actionable references point to `references/Hexalith.*`,
**And** historical examples inside approved sprint-change proposals, external submodule content under `references/**`, generated namespace/type references such as `Hexalith.EventStore.*`, and tree entries that are explicitly children of a `references/` node are not treated as defects,
**And** any true stale root-level path found in root-owned docs or instructions is patched narrowly.

**AC4 - Aspire metadata uses the shared flexible resolver and its `references/` fallback.**
**Given** consuming AppHosts need EventStore project metadata,
**When** `RepositoryProjectPaths` and `EventStorePlatformProjectMetadata` are inspected and AppHost tests run,
**Then** metadata resolves through `GetReferencedModuleProjectPath`,
**And** the helper keeps the `references/<module>/...` fallback for missing modules,
**And** the current-repository case remains valid when EventStore itself is the repository root,
**And** no implementation replaces the helper with a fixed `GetProjectPath("references", "Hexalith.EventStore", ...)` call unless tests prove the flexible resolver is broken.

**AC5 - Submodule dirt and pointer changes are not hidden inside this story.**
**Given** the current worktree may already contain modified submodule pointers,
**When** this story is implemented,
**Then** unrelated submodule pointer changes are ignored and not reverted,
**And** no submodule file under `references/Hexalith.*` is edited without explicit maintainer approval,
**And** any intentional future submodule pointer bump is isolated as a conventional `chore(deps): ...` change, not bundled into this verification.

**AC6 - FR19 evidence and sprint ledger are reconciled.**
**Given** the June 26 correction is already implemented and approved,
**When** validation commands complete,
**Then** the Dev Agent Record captures evidence for AC1-AC4,
**And** any genuine stale-path fixes are listed,
**And** `sprint-status.yaml` can be moved from `backlog` to `done` by the normal dev/review flow after evidence is recorded.

### Original Epic Acceptance Criteria (preserved for traceability - `epics.md:919-937`)

1. Root-declared submodule paths in `.gitmodules` are under `references/` and no root-level `Hexalith.*` submodule directory remains required. -> verified by AC1.
2. Restore and Release build against `Hexalith.EventStore.slnx` resolve project references/source path properties through `references/` and no stale root-level path is required. -> verified by AC2.
3. Documentation, generated API reference docs, Aspire metadata, and LLM instructions point to `references/Hexalith.*`; nested submodules are not initialized or required. -> verified by AC3 and AC5.
4. Consuming AppHosts resolve EventStore project metadata using the shared `references/Hexalith.EventStore` convention and focused AppHost tests verify the paths. -> verified by AC4, with the current flexible resolver replacing the older fixed-path snippet.

## Tasks / Subtasks

- [ ] **Task 1 - Verify the approved baseline and story boundaries (AC1-AC6).**
  - [ ] Read `sprint-change-proposal-2026-06-26-submodule-references.md`; confirm implementation/validation/approval are recorded.
  - [ ] Confirm `sprint-change-proposal-2026-07-09.md` affects Story 3.7 only and does not alter Story 3.3 scope.
  - [ ] Confirm no implementation task requires editing submodule files under `references/**`.
- [ ] **Task 2 - Verify root submodule layout (AC1).**
  - [ ] Run `git config -f .gitmodules --get-regexp '^submodule\..*\.path$'` and confirm every path starts with `references/`.
  - [ ] Run `find . -maxdepth 1 -type d -name 'Hexalith.*' -print`; record that it returns no root-level submodule directories.
  - [ ] Do not run `git submodule update --init --recursive`, `git submodule update --remote`, or any recursive submodule command.
- [ ] **Task 3 - Verify solution and MSBuild path resolution (AC2).**
  - [ ] Inspect `Hexalith.EventStore.slnx`, `Directory.Build.props`, and `Directory.Packages.props`; confirm the active root-owned paths use `references/`.
  - [ ] Run `dotnet sln Hexalith.EventStore.slnx list` to confirm `.slnx` tooling reads the solution.
  - [ ] Run restore/build validation in package mode.
- [ ] **Task 4 - Verify docs, generated docs, and instruction paths (AC3).**
  - [ ] Inspect `AGENTS.md`, `CLAUDE.md`, and `.github/copilot-instructions.md`; confirm they point to `./references/Hexalith.AI.Tools/hexalith-llm-instructions.md`.
  - [ ] Run a stale-path scan over root-owned files. Treat these as allowed: historical OLD examples in sprint-change proposals, files under `references/**`, generated namespace/type names, and tree entries nested under a visible `references/` node.
  - [ ] Patch any true root-owned stale path such as `./Hexalith.AI.Tools/...` or `Hexalith.Tenants/src/...` outside historical/explicitly contextual text.
- [ ] **Task 5 - Verify Aspire metadata resolver behavior (AC4).**
  - [ ] Read `RepositoryProjectPaths.cs` and `EventStorePlatformProjectMetadata.cs`; confirm the flexible resolver remains intact and includes the `references/<module>/...` fallback.
  - [ ] Run `dotnet test tests/Hexalith.EventStore.AppHost.Tests/ --configuration Release --no-build -p:UseHexalithProjectReferences=false` after the Release build, or run the project without `--no-build` if the build was skipped.
  - [ ] If AppHost path tests fail, patch the narrow resolver/test mismatch rather than reintroducing fixed root-level paths.
- [ ] **Task 6 - Record evidence and enforce scope (AC5-AC6).**
  - [ ] Record commands, pass/fail counts, and any blocked validation in the Dev Agent Record.
  - [ ] Confirm no submodule pointer changes were introduced by this story.
  - [ ] Confirm no `.sln` file was created and no root-level `Hexalith.*` directory is required.

## Dev Notes

### Top Guardrails

- **Do not re-run the migration.** The `references/` move was implemented and approved through the June 26 proposal. This story verifies current state and patches only real regressions.
- **Do not recurse into nested submodules.** Official Git documentation states that `--recursive` recurses into nested submodules during update/sync. This repo explicitly forbids that for this work. Use only root `.gitmodules` paths.
- **Do not modify submodule files or pointers.** The current `git status --short` already shows modified submodule entries for several `references/Hexalith.*` directories. Treat them as pre-existing unless the user explicitly asks for submodule pointer work.
- **Keep `.slnx` only.** .NET 10 defaults new solutions to SLNX and the repo's rule is to use `Hexalith.EventStore.slnx`; do not create or use legacy `.sln`.
- **Preserve the flexible Aspire resolver.** `GetReferencedModuleProjectPath` is intentionally broader than the older fixed `references/Hexalith.EventStore` snippet because EventStore may be the current repo, a child checkout, or a referenced module. The required invariant is that `references/` remains the fallback/convention, not that every runtime path is always `references/`.
- **Release/package mode is the validation default.** Use `-p:UseHexalithProjectReferences=false` for restore/build unless the story explicitly calls out source-debug validation.

### Current Files Read During Story Creation

**Submodule declaration - `.gitmodules`:**
- Lines `1-21` declare all root submodules with `path = references/...`.

**Build and solution files:**
- `Directory.Build.props:4-10` imports Hexalith.Builds props from `references/`.
- `Directory.Build.props:13-30` resolves Tenants source paths, preferring this repo's `references/Hexalith.Tenants`.
- `Directory.Build.props:40-42` resolves Commons source paths, preferring this repo's `references/Hexalith.Commons`.
- `Directory.Packages.props:5-15` imports Hexalith.Builds package props from `references/`.
- `Hexalith.EventStore.slnx:1-67` is the active XML solution; no `.sln` is needed.

**Instructions and docs:**
- `AGENTS.md:3-14`, `CLAUDE.md:3-14`, and `.github/copilot-instructions.md:3-14` point to `references/Hexalith.AI.Tools` and forbid nested submodule recursion.
- `docs/brownfield/source-tree-analysis.md:29-36` shows `Hexalith.*` entries under the `references/` node. Do not "fix" those lines into duplicate `references/references/...` text.

**Aspire metadata helpers:**
- `RepositoryProjectPaths.cs:43-95` implements the flexible module resolver with a `references/<module>/...` standalone fallback.
- `EventStorePlatformProjectMetadata.cs:6-23` uses the helper for the command gateway; `:29-60` uses it for Admin Server/UI.
- `RepositoryProjectPathsTests.cs:20-46` proves current-repo resolution and missing-module `references/` fallback.

### Latest Technical References

- Git `git-submodule` documentation: `--init` initializes from `.gitmodules`; `--recursive` recurses into nested submodules during update/sync. Source: https://git-scm.com/docs/git-submodule
- Microsoft Learn: In .NET 10, `dotnet new sln` defaults to SLNX; `dotnet sln` supports `.slnx` files. Sources: https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default and https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln
- Microsoft Learn MSBuild: `Directory.Build.props` is imported early by `Microsoft.Common.props`, and MSBuild searches upward from each project path. Source: https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory

### Scope Boundaries

- No `.sln` creation or solution migration.
- No CI/CD migration; the 2026-07-09 Tenants-style reusable workflow proposal belongs to Story 3.7.
- No package-mode redesign; Story 3.5 and 3.6 own Debug-source/Release-package and manifest packaging work.
- No changes inside `references/**` unless explicitly approved by the maintainer.
- No broad rewrite of generated API reference docs; only patch concrete stale path requirements if scans prove they are active guidance.

### Validation Commands

Run from the repository root.

```bash
# Formatting sanity
git diff --check

# Root-declared submodule paths only; do not recurse.
git config -f .gitmodules --get-regexp '^submodule\..*\.path$'
find . -maxdepth 1 -type d -name 'Hexalith.*' -print

# Confirm SLNX tooling and package-mode build.
dotnet sln Hexalith.EventStore.slnx list
dotnet restore Hexalith.EventStore.slnx -p:UseHexalithProjectReferences=false
dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false

# Focused AppHost path-resolution tests. If the Release build did not run first, omit --no-build.
dotnet test tests/Hexalith.EventStore.AppHost.Tests/ \
  --configuration Release --no-build -p:UseHexalithProjectReferences=false

# Stale root-level path scan. Review hits; do not treat historical proposal OLD blocks,
# references/** submodule content, generated namespaces, or tree entries under a visible
# references/ node as defects.
rg -n "(\./Hexalith\.AI\.Tools/|(^|[\"'(=[:space:]])Hexalith\.(AI\.Tools|Builds|Commons|FrontComposer|Memories|PolymorphicSerializations|Tenants)/)" \
  AGENTS.md CLAUDE.md .github/copilot-instructions.md docs src tests samples .github deploy scripts tools \
  --glob '!**/bin/**' --glob '!**/obj/**' --glob '!TestResults/**'
```

Expected current-state scan notes:

- `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, build props, workflow cache keys, and docs should use `references/` where they describe paths.
- `docs/brownfield/source-tree-analysis.md:30-36` is valid because those entries are nested under `references/`.
- `Directory.Build.props:28,41` keeps fallback sibling layout probes. Those are compatibility probes, not active root-submodule requirements.

### Git Intelligence

- Latest commit at story creation: `0f428d0c feat: enhance Hexalith build package version handling and update version condition`, modifying `Directory.Build.props` and `CHANGELOG.md`. Re-check `Directory.Build.props` before editing because it changed recently.
- Recent submodule pointer changes exist (`9f5e2f13 chore: update subproject references for Hexalith.Builds and Hexalith.Memories`) and the current worktree has modified submodule entries. Do not include unrelated pointer updates in this story.
- Recent instruction simplification (`1eeb4842 docs: add AI assistant repository instructions`) reduced AGENTS/CLAUDE to the external `references/Hexalith.AI.Tools` instruction path. Do not restore the older bulky instruction body.
- Earlier references-layout commits include `9bafe1af Refactor submodule references layout to references/ directory`, `5228fdbf refactor: update submodule paths to include references/ prefix`, and `a03f364c feat(paths): add GetReferencedModuleProjectPath method for flexible project path resolution`.

### References

- [Source: _bmad-output/planning-artifacts/prd.md:139] FR19.
- [Source: _bmad-output/planning-artifacts/prd.md:207] NFR9.
- [Source: _bmad-output/planning-artifacts/prd.md:222-227] repository/build guardrails.
- [Source: _bmad-output/planning-artifacts/architecture.md:97-119] AD-9, AD-11, AD-12.
- [Source: _bmad-output/planning-artifacts/epics.md:909-937] Story 3.3 original ACs.
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-26-submodule-references.md:5-19,47-57,134-147,157-163] approved shipped correction and validation.
- [Source: .gitmodules:1-21] root-declared submodule paths.
- [Source: Directory.Build.props:4-55] references path resolution and package/source mode defaults.
- [Source: Directory.Packages.props:5-15] Hexalith.Builds package props imports.
- [Source: src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs:43-95] flexible referenced-module project resolver.
- [Source: tests/Hexalith.EventStore.AppHost.Tests/Configuration/RepositoryProjectPathsTests.cs:20-46] current-repo and references-fallback test coverage.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Implementation Plan / Decisions

### Debug Log References

### Completion Notes List

### File List

### Change Log
