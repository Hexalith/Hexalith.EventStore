---
title: 'Fix GitHub Actions restore failure 28528872473'
type: 'bugfix'
created: '2026-07-02'
status: 'done'
baseline_commit: '80a6670b515ac4e477c3959c8ef5f85c7d4be629'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent -- do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions run `28528872473` fails during `dotnet restore` with Central Package Management errors (`NU1009` and `NU1010`) after `Directory.Packages.props` was cleaned up to import shared package versions from `references/Hexalith.Builds`.

**Approach:** Make the root repository pin `references/Hexalith.Builds` to the shared-build commit that contains the required EventStore package versions and removes the invalid `Aspire.Hosting.AppHost` central version.

## Boundaries & Constraints

**Always:** Keep using `Hexalith.EventStore.slnx` for restore/build. Keep the `Directory.Packages.props` import model introduced by the cleanup commit. Limit submodule handling to root-declared submodules only.

**Ask First:** Ask before changing package versions directly in `.csproj` files, editing files inside submodules, or broadening the fix to unrelated submodule pointer changes.

**Never:** Do not use `.sln` files. Do not recurse into nested submodules. Do not restore the deleted package-version list as a workaround if the shared `Hexalith.Builds` props now own those versions.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Failing CI restore | Root repo pins `Hexalith.Builds` at `488bcda2` while `Directory.Packages.props` imports shared package props | Restore reports missing `PackageVersion` entries and invalid `Aspire.Hosting.AppHost` central version | Update only the root `references/Hexalith.Builds` gitlink to the commit that carries the shared package-version fix |
| Local dirty submodules | Other `references/*` pointers are already dirty | Fix leaves unrelated submodule changes untouched | Do not reset, stage, or modify unrelated pointers |

</frozen-after-approval>

## Code Map

- `.github/workflows/ci.yml` -- CI entry point; failing jobs restore `Hexalith.EventStore.slnx`.
- `Directory.Packages.props` -- central package version file; now imports `references/Hexalith.Builds/Props/Directory.Packages.props`.
- `references/Hexalith.Builds` -- root-declared submodule whose pinned commit supplies shared package versions.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Builds` -- update the root gitlink from `488bcda2` to `3dd4b0cd` -- aligns the repository with the shared package props expected by `Directory.Packages.props`.
- [x] `.github/workflows/ci.yml` and `Directory.Packages.props` -- inspect but leave unchanged unless verification proves the submodule pointer is insufficient -- avoids duplicating package ownership locally.

**Acceptance Criteria:**
- Given the root repo pins `references/Hexalith.Builds` at `3dd4b0cd`, when `dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:NuGetAudit=false --force-evaluate` runs, then restore completes without `NU1009` or `NU1010`.
- Given unrelated submodule pointers were dirty before the fix, when the fix is complete, then those unrelated pointers remain untouched.

## Verification

**Commands:**
- `dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:NuGetAudit=false --force-evaluate` -- expected: restore succeeds.
- `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -warnaserror -p:UseHexalithProjectReferences=false -p:NuGetAudit=false` -- expected: build succeeds or exposes a separate post-restore failure.

## Suggested Review Order

**Shared Package Import**

- Root package props now depend on the pinned shared-build submodule.
  [`Directory.Packages.props:11`](../../Directory.Packages.props#L11)

**Restore Fix Evidence**

- AppHost package versions are supplied without centralizing implicit `Aspire.Hosting.AppHost`.
  [`Directory.Packages.props:77`](../../references/Hexalith.Builds/Props/Directory.Packages.props#L77)

- DAPR and MediatR versions return to shared package ownership.
  [`Directory.Packages.props:101`](../../references/Hexalith.Builds/Props/Directory.Packages.props#L101)

- Test package versions missing in CI are now present in shared props.
  [`Directory.Packages.props:186`](../../references/Hexalith.Builds/Props/Directory.Packages.props#L186)
