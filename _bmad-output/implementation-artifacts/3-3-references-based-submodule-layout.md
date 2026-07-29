---
baseline_commit: 0f428d0c914f2151aab15bb262f956a9630041dc
verified_at_commit: 1d42528b
verified_at_note: >-
  `baseline_commit` is the story-creation reading baseline and is 399 commits behind the commit this story
  was actually implemented and verified against. Six of the sixteen `source_files` changed in between, so
  some cited line ranges are stale (notably AGENTS.md / CLAUDE.md / copilot-instructions.md, where the
  references/Hexalith.AI.Tools path is now at line 18, and Hexalith.EventStore.slnx, now 68 lines). Every
  substantive AC1-AC4 fact was re-verified at 1d42528b by the 2026-07-29 code review and still holds.
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

Status: done

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

- [x] **Task 1 - Verify the approved baseline and story boundaries (AC1-AC6).**
  - [x] Read `sprint-change-proposal-2026-06-26-submodule-references.md`; confirm implementation/validation/approval are recorded.
  - [x] Confirm `sprint-change-proposal-2026-07-09.md` affects Story 3.7 only and does not alter Story 3.3 scope.
  - [x] Confirm no implementation task requires editing submodule files under `references/**`.
- [x] **Task 2 - Verify root submodule layout (AC1).**
  - [x] Run `git config -f .gitmodules --get-regexp '^submodule\..*\.path$'` and confirm every path starts with `references/`.
  - [x] Run `find . -maxdepth 1 -type d -name 'Hexalith.*' -print`; record that it returns no root-level submodule directories.
  - [x] Do not run `git submodule update --init --recursive`, `git submodule update --remote`, or any recursive submodule command.
- [x] **Task 3 - Verify solution and MSBuild path resolution (AC2).**
  - [x] Inspect `Hexalith.EventStore.slnx`, `Directory.Build.props`, and `Directory.Packages.props`; confirm the active root-owned paths use `references/`.
  - [x] Run `dotnet sln Hexalith.EventStore.slnx list` to confirm `.slnx` tooling reads the solution.
  - [x] Run restore/build validation in package mode.
- [x] **Task 4 - Verify docs, generated docs, and instruction paths (AC3).**
  - [x] Inspect `AGENTS.md`, `CLAUDE.md`, and `.github/copilot-instructions.md`; confirm they point to `./references/Hexalith.AI.Tools/hexalith-llm-instructions.md`.
  - [x] Run a stale-path scan over root-owned files. Treat these as allowed: historical OLD examples in sprint-change proposals, files under `references/**`, generated namespace/type names, and tree entries nested under a visible `references/` node.
  - [x] Patch any true root-owned stale path such as `./Hexalith.AI.Tools/...` or `Hexalith.Tenants/src/...` outside historical/explicitly contextual text.
- [x] **Task 5 - Verify Aspire metadata resolver behavior (AC4).**
  - [x] Read `RepositoryProjectPaths.cs` and `EventStorePlatformProjectMetadata.cs`; confirm the flexible resolver remains intact and includes the `references/<module>/...` fallback.
  - [x] Run `dotnet test tests/Hexalith.EventStore.AppHost.Tests/ --configuration Release --no-build -p:UseHexalithProjectReferences=false` after the Release build, or run the project without `--no-build` if the build was skipped.
  - [x] If AppHost path tests fail, patch the narrow resolver/test mismatch rather than reintroducing fixed root-level paths.
- [x] **Task 6 - Record evidence and enforce scope (AC5-AC6).**
  - [x] Record commands, pass/fail counts, and any blocked validation in the Dev Agent Record.
  - [x] Confirm no submodule pointer changes were introduced by this story.
  - [x] Confirm no `.sln` file was created and no root-level `Hexalith.*` directory is required.

### Review Findings

> **AC5 exception raised during review — needs an owner call.** While this review was running, a concurrent
> automated loop committed `afb90369 fix: update story 3.3 status to review and enhance line-ending handling in
> tests` directly to `main`. That commit bundles **two submodule pointer bumps** — `references/Hexalith.Memories`
> `7d298205` → `ccd8efa3` and `references/Hexalith.Tenants` `96bdfd8a` → `09947a24` — into a commit titled for
> this story. AC5 requires that "any intentional future submodule pointer bump is isolated as a conventional
> `chore(deps): ...` change, not bundled into this verification", so the commit as it stands contradicts AC5 and
> the Task 6 attestation "Confirm no submodule pointer changes were introduced by this story". Both gitlinks
> **are reachable on their submodules' `origin/main`**, so there is no dangling-pointer hazard. Note also that
> AC5's prescribed `chore(deps):` type conflicts with the repo rule "Never use `chore`" — the repo's actual
> convention is `build(deps):` (cf. `1d42528b`).
>
> **RATIFIED 2026-07-29 by Administrator.** The AC5 isolation requirement was violated by an out-of-band
> automated commit, not by this story's work, and both gitlinks are reachable on their remotes. The bundling is
> accepted retroactively; no commit split is required. AC5's `chore(deps):` wording remains inconsistent with
> the repo's `build(deps):` convention and should be corrected the next time this AC is edited.

<!-- Added 2026-07-29 by bmad-code-review (four layers: blind-hunter, edge-case-hunter,
     verification-gap, acceptance-auditor). Every AC1-AC4 cheap gate was independently re-run
     and matched the Dev Agent Record; AppHost re-ran 51/51 at HEAD 1d42528b. -->

- [x] [Review][Decision] **RESOLVED — add `*.cs text eol=lf` to `.gitattributes`.** Repo-wide line-ending policy conflict is the real root cause — `.editorconfig:8` sets `end_of_line = crlf` for `[*]` while `.gitattributes:1` sets `* text=auto`, so editors write CRLF and git normalizes to LF in the index. 117 tracked files are currently `i/lf w/crlf` with a clean `git status`. The story patched one symptom. Fix direction is a shared-convention decision (set `.editorconfig` to `lf`, add `*.cs text eol=lf`, or accept the drift) and affects all Hexalith repos.
- [x] [Review][Decision] **RESOLVED — ratified as an accepted, recorded exception (see the Task 5 scope-ratification entry in the Debug Log).** The only code change sits outside the story's declared change permission, on an unverifiable authorization — the Verification stance (`:82`) and Task 5 (`:155`) permit patching "the narrow resolver/**test** mismatch" when "AppHost **path** tests fail". `RepositoryProjectPathsTests` (the path tests) passed 9/9 and were untouched; the patched test asserts DAPR service-invocation registration. The story concedes this at `:281` ("unrelated to the Story 3.3 resolver/path acceptance criteria"), and `:282` rests the change on "Administrator authorized", which exists only as a self-attestation with no commit trailer or external artifact.
- [x] [Review][Patch] Move line-ending normalization to the file-read site so the caller's text is normalized too — `program` stays raw, and a future multi-line `ShouldNotContain` would pass vacuously (fail-open) [tests/Hexalith.EventStore.AppHost.Tests/Configuration/SampleApiLaunchSettingsTests.cs:41-45,60-61,90]
- [x] [Review][Patch] Use the repo's established idiom `.Replace("\r\n", "\n", StringComparison.Ordinal)` instead of `ReplaceLineEndings("\n")` — the latter is the only such call in the repo and also collapses FF/NEL/LS/PS, which Microsoft's docs explicitly caution against for parsers [tests/Hexalith.EventStore.AppHost.Tests/Configuration/SampleApiLaunchSettingsTests.cs:90]
- [x] [Review][Patch] Converge the two un-hardened sibling copies of the same block extractor, which read the same `Program.cs` and now carry divergent contracts [tests/Hexalith.EventStore.AppHost.Tests/Configuration/TenantsApiLaunchSettingsTests.cs:153; tests/Hexalith.EventStore.Server.Tests/DaprComponents/DaprComponentValidationTests.cs:379]
- [x] [Review][Patch] Add a CRLF/LF equivalence test pinning the fix — nothing currently reddens if the normalization line is deleted, because every CI lane is `ubuntu-latest` and the index blob is LF [tests/Hexalith.EventStore.AppHost.Tests/Configuration/SampleApiLaunchSettingsTests.cs:88-97]
- [x] [Review][Patch] Add a whole-file assertion that `sampleApi` never receives an infrastructure reference — the "zero direct infrastructure access" guard is enforced only inside the extracted 10-line slice, so a later `sampleApi.WithReference(eventStoreResources.StateStore)` ships undetected [tests/Hexalith.EventStore.AppHost.Tests/Configuration/SampleApiLaunchSettingsTests.cs:56-58]
- [x] [Review][Patch] Correct the false resolver docstring — it claims the candidates probe "in the same order as the `$(Hexalith*Root)` auto-detection in `Directory.Build.props`", but `Directory.Build.props` checks `references/` **first** for both Tenants and Commons and contains no root-level probe at all [src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs:44-51,80-81]
- [x] [Review][Patch] Correct AC2 evidence: `dotnet sln list` reads **46** projects, not 48 (48 is the raw line count including the `Project(s)` header and separator) [_bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md:279,290]
- [x] [Review][Patch] Record the actual verification baseline in the Dev Agent Record — frontmatter `baseline_commit: 0f428d0c` is 399 commits behind the verified HEAD `1d42528b`; 6 of 16 `source_files` changed since, making several cited line ranges stale (the substantive facts still hold, re-verified) [_bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md:2]
- [x] [Review][Patch] Add a ledger rationale comment for the `review` transition — every neighbouring row carries an evidence/decision comment; line 168 flips with none [_bmad-output/implementation-artifacts/sprint-status.yaml:168]
- [x] [Review][Defer] Resolver probes a root-level `<root>/Hexalith.<Module>/` layout at higher precedence than `references/` [src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs:80-81] — deferred, pre-existing; reordering is a design change the story explicitly scoped out (Dev Notes `:169`) and no such directory exists today (AC1 verified)
- [x] [Review][Defer] AC4 has no test that a **present** module under `references/` resolves; resolver candidates 2, 3, 4, 6 and 7 are entirely uncovered [tests/Hexalith.EventStore.AppHost.Tests/Configuration/RepositoryProjectPathsTests.cs:31-46] — deferred, pre-existing; needs filesystem fixtures since `GetRepositoryRoot()` derives from `AppContext.BaseDirectory`
- [x] [Review][Defer] `GetReferencedModuleProjectPath` skips the `ValidateRelativePathSegments` guard that `GetProjectPath` enforces, so a rooted segment would escape the repository root [src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs:55-64 vs :30,38-40] — deferred, pre-existing; all current callers pass literals
- [x] [Review][Defer] YAML helpers survive CRLF only accidentally via `.Trim()`, while the sibling test parses the same file with YamlDotNet [tests/Hexalith.EventStore.AppHost.Tests/Configuration/SampleApiLaunchSettingsTests.cs:99-167] — deferred, pre-existing; `.editorconfig` has no `[*.yaml]` override, so the file is LF today only by luck
- [x] [Review][Defer] Three tracked scripts lack the executable bit (`ci-local.sh`, `check-deferred-work.sh`, `validate-release-secrets.sh` are `100644`; five siblings are `100755`) — deferred, pre-existing; discovered during Task 6 (exit 126), worked around via `bash <script>`, never filed. Fix is `git update-index --chmod=+x`

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

OpenAI Codex (GPT-5)

### Implementation Plan / Decisions

- Execute the verification-only tasks in story order; patch root-owned files only when a current-state command proves a stale path or broken resolver.
- Preserve the flexible Aspire path resolver, avoid all recursive submodule commands, and treat `references/**` content and gitlinks as read-only for this story.
- Validate in Release/package mode with the `.slnx`, then run the focused AppHost test project and record exact evidence.

### Debug Log References

- 2026-07-29 Task 1: June 26 proposal records the completed migration, restore/build/AppHost validation, and Administrator approval on 2026-07-01. July 9 proposal is scoped to Story 3.7 CI/CD alignment. No Story 3.3 task requires a `references/**` edit. `git diff --check` passed.
- 2026-07-29 Task 2: `.gitmodules` reports 7 root-declared submodule paths and all 7 begin with `references/`; the root-level `Hexalith.*` directory scan returned no output. No submodule update command was run. `git diff --check` passed.
- 2026-07-29 Task 3: inspected the `.slnx` and both root MSBuild props; active local/default Hexalith paths use `references/`. `dotnet sln ... list` read 46 projects. Package-mode restore succeeded and the Release solution build succeeded with 0 warnings and 0 errors. `git diff --check` passed. (Corrected by the 2026-07-29 code review: the original entry recorded "48 projects" and "48/48", which counted the `Project(s)` header and the `----------` separator as projects. `grep -c '<Project Path=' Hexalith.EventStore.slnx` = 46. The Release build additionally compiles `Hexalith.EventStore.Gateway` and `Hexalith.EventStore.TestSubscriber`, which are pulled in transitively but are absent from the `.slnx`.)
- 2026-07-29 Task 4: all three shared instruction entry points are byte-identical and use `<workspace>/references/Hexalith.AI.Tools/hexalith-llm-instructions.md`. The first stale-scan invocation exited 2 because of shell quote construction; the corrected equivalent scan exited 0 and returned only the seven expected tree children at `docs/brownfield/source-tree-analysis.md:30-36`, visibly nested below `references/`. No stale root-owned path required a patch. `git diff --check` passed.
- 2026-07-29 Task 5 validation blocker: resolver/metadata inspection passed, including the flexible current-repository probes and `references/<module>/...` fallback. The focused `RepositoryProjectPathsTests` class passed 9/9. The required full AppHost project command failed 1/51 (`SampleApiLaunchSettingsTests.AppHost_RegistersSampleApiAsExternalServiceInvocationOnlyHost`; 50 passed) because `ExtractBlock` searches for `;\n\nif (security is not null)` while the checked-out `Program.cs` has CRLF working-tree endings (`git ls-files --eol`: `i/lf w/crlf`) and the test file has LF endings (`i/lf w/lf`). This is unrelated to the Story 3.3 resolver/path acceptance criteria, and the verification-only scope permits patches only for confirmed stale root-level paths or a resolver/test mismatch; Task 5 remains unchecked pending maintainer authorization or external resolution.
- 2026-07-29 Task 5 scope ratification (added by the 2026-07-29 code review): the patched test,
  `SampleApiLaunchSettingsTests.AppHost_RegistersSampleApiAsExternalServiceInvocationOnlyHost`, asserts DAPR
  service-invocation registration, not path resolution. It therefore falls outside the literal wording of the
  Task 5 permission ("If AppHost **path** tests fail, patch the narrow resolver/test mismatch") — the actual
  path tests, `RepositoryProjectPathsTests`, passed 9/9 and were untouched. Administrator confirmed the
  authorization at review time and **explicitly ratified the change as an accepted, recorded exception** rather
  than widening the AC. This paragraph is the audit record; the attestation below is retained as written.
- 2026-07-29 Task 5 resolution: Administrator authorized the narrow test-only correction. `ExtractBlock` now normalizes source text to LF before locating its block boundary. Red evidence was the deterministic 1/51 failure above; the formerly failing method then passed 1/1, `RepositoryProjectPathsTests` remained 9/9, and the complete AppHost project passed 51/51 after its Release/package-mode build succeeded with 0 warnings and 0 errors. `git diff --check` passed.
- 2026-07-29 Task 6: `git status --short` and `git diff --name-status` show exactly the story, sprint ledger, and authorized AppHost test file. `git diff --submodule=short -- .gitmodules references` returned no output. Root-owned `*.sln` scan and root-level `Hexalith.*` directory scan both returned no matches. Direct execution of `./scripts/ci-local.sh --tier 1 --skip-build` was environment-blocked with exit 126 because the tracked script lacks its executable bit; invoking the same script through Bash passed all 17 deterministic Tier 1 projects: 7,678 passed, 51 skipped, 0 failed.
- 2026-07-29 Completion gate: re-scanned the story with zero unchecked tasks/subtasks, confirmed the File List exactly matches `git diff --name-only`, and reran the full deterministic Tier 1 regression matrix after all implementation edits. Final result: 17/17 projects passed, 7,678 passed tests, 51 intentional skips, 0 failures. `git diff --check` passed; Release/package-mode build evidence remains 0 warnings and 0 errors.

### Completion Notes List

- Task 1 complete: confirmed the shipped correction and approval, excluded Story 3.7 CI/CD work, and fixed the story boundary at root-owned verification only.
- Task 2 complete: verified all seven root submodules use `references/` and no root-level `Hexalith.*` directory exists.
- Task 3 complete: verified `.slnx` tooling and all package-mode restore/build paths; the 46-project Release build is clean.
- Task 4 complete: verified synchronized `references/` instructions and reviewed all seven scan hits as valid nested tree entries; no stale-path fix was needed.
- Task 5 complete: preserved the flexible resolver and `references/` fallback, verified all resolver facts, and made the source-inspection test line-ending independent with maintainer authorization.
- Task 6 complete: recorded all AC1-AC4 evidence and validation recovery, confirmed zero submodule/gitlink changes, and confirmed the repository remains `.slnx`-only with no root-level Hexalith module directory.
- Story 3.3 complete and ready for review: FR19 is verified at the current baseline, all AC1-AC6 evidence is recorded, no submodule migration or pointer change occurred, and the only implementation patch makes an existing AppHost source-inspection test deterministic across LF/CRLF checkouts.

### File List

- `_bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md` (modified)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified)
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/SampleApiLaunchSettingsTests.cs` (modified)

Added by the 2026-07-29 code review (11 applied patches):

- `.gitattributes` (modified) — `*.cs text eol=lf`, closing the root cause
- `src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs` (modified) — corrected a false precedence docstring
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/TenantsApiLaunchSettingsTests.cs` (modified) — converged sibling extractor
- `tests/Hexalith.EventStore.Server.Tests/DaprComponents/DaprComponentValidationTests.cs` (modified) — converged third extractor
- `_bmad-output/implementation-artifacts/deferred-work.md` (modified) — 5 deferred entries

### Change Log

- 2026-07-29: Started Story 3.3 verification and confirmed the approved scope boundaries.
- 2026-07-29: Verified the root-declared submodule layout satisfies AC1 without initializing or updating submodules.
- 2026-07-29: Verified AC2 with `.slnx` enumeration plus successful package-mode restore and Release build (0 warnings, 0 errors).
- 2026-07-29: Verified AC3 across root-owned instructions, documentation, source, tests, samples, workflows, deploy assets, scripts, and tools; no actionable stale path was found.
- 2026-07-29: Verified AC4 and hardened the AppHost source-inspection test against LF/CRLF checkout differences; AppHost tests pass 51/51.
- 2026-07-29: Reconciled FR19 evidence and scope for AC5-AC6; deterministic Tier 1 regression passed across 17 projects (7,678 passed, 51 skipped, 0 failed).
- 2026-07-29: Completed Story 3.3 and moved it to review after all ACs, tasks, scope checks, and final regression gates passed.
