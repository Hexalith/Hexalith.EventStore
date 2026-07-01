---
title: 'Fix GH 28522568781 AppHost project path resolution'
type: 'bugfix'
created: '2026-07-01'
status: 'done'
baseline_commit: '294aab403e0ae73965bc402db2b85340088308dc'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-26-submodule-references.md'
---

<frozen-after-approval reason="human-owned intent - do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions run `28522568781` failed in Tier 1 unit tests because `RepositoryProjectPathsTests.EventStoreProjectMetadata_ProjectPath_UsesReferencesSubmoduleLayout` expected `references/Hexalith.EventStore/src/...`, while the AppHost metadata resolved the current repository's root `src/Hexalith.EventStore/...` project path.

**Approach:** Treat the current repository layout as a valid first-class resolution mode for `GetReferencedModuleProjectPath`, then update the focused test to assert the root EventStore project path when the module being resolved is the current repository.

## Boundaries & Constraints

**Always:** Preserve `.slnx`-only build/test usage, keep path resolution compatible with consuming repositories that carry EventStore under `references/`, and keep changes scoped to Aspire path resolution plus focused tests.

**Ask First:** Any change that modifies root-declared submodule entries, initializes nested submodules, or changes AppHost resource topology beyond project path metadata.

**Never:** Do not modify files inside `references/`, do not weaken path traversal validation, and do not replace the flexible layout support with a single hard-coded path.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Current repository module | EventStore root repo resolving `Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj` | Returns `<repo-root>/src/Hexalith.EventStore/Hexalith.EventStore.csproj` when that file exists | N/A |
| Consuming repository fallback | No supported checkout candidate exists for the requested module | Returns `<repo-root>/references/<module>/...` so failures identify the diagnosable standalone submodule path | N/A |
| Invalid path input | Empty, rooted, or parent-traversal path segments | Throws `ArgumentException` as existing tests require | Existing validation remains unchanged |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs` -- shared path helper used by cross-repo Aspire project metadata.
- `src/Hexalith.EventStore.Aspire/EventStorePlatformProjectMetadata.cs` -- metadata types that resolve EventStore platform projects via `GetReferencedModuleProjectPath`.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/RepositoryProjectPathsTests.cs` -- focused unit tests for repository path behavior.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs` -- add the current-repository candidate before external checkout candidates and update comments/docs -- supports EventStore's own root repo and GitHub Actions checkout layout.
- [x] `tests/Hexalith.EventStore.AppHost.Tests/Configuration/RepositoryProjectPathsTests.cs` -- rename/update the EventStore metadata test and add/keep fallback coverage -- prevents future regression to an invalid root-repo fallback assertion.

**Acceptance Criteria:**
- Given the EventStore root repository contains `src/Hexalith.EventStore/Hexalith.EventStore.csproj`, when `EventStoreProjectMetadata.ProjectPath` is evaluated, then it resolves to that root project path.
- Given an unknown module cannot be found in any supported checkout layout, when `GetReferencedModuleProjectPath` is called, then it returns the standalone `references/<module>/...` diagnostic fallback path.
- Given the focused AppHost test project runs in Release, when tests execute, then all AppHost path tests pass.

## Spec Change Log

## Design Notes

The helper already supports multiple external checkout layouts because consuming repositories can reference EventStore as a submodule under `references/`, as a sibling module, or from nested layouts. EventStore's own repository is another valid layout: the requested module is present directly under the current root `src/` tree. That candidate should be checked before external module candidates because it is the most precise match for this repo.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.AppHost.Tests/ --configuration Release --no-restore` -- expected: all tests pass.
- `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore` -- expected: build succeeds with warnings as errors.

## Suggested Review Order

**Path Resolution**

- Current repository source is now the first resolution candidate.
  [`RepositoryProjectPaths.cs:66`](../../src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs#L66)

- Metadata comments now describe the full flexible layout contract.
  [`EventStorePlatformProjectMetadata.cs:7`](../../src/Hexalith.EventStore.Aspire/EventStorePlatformProjectMetadata.cs#L7)

**Regression Tests**

- The failing assertion now expects the root EventStore project path.
  [`RepositoryProjectPathsTests.cs:20`](../../tests/Hexalith.EventStore.AppHost.Tests/Configuration/RepositoryProjectPathsTests.cs#L20)

- Missing modules still fall back to `references/<module>` diagnostics.
  [`RepositoryProjectPathsTests.cs:31`](../../tests/Hexalith.EventStore.AppHost.Tests/Configuration/RepositoryProjectPathsTests.cs#L31)
