---
title: 'Fix false failure after successful release publication'
type: 'bugfix'
created: '2026-07-20'
status: 'in-progress'
review_loop_iteration: 1
baseline_commit: 'a21517e3b66458e997d1ea2f4df5072c4abde628'
context:
  - '{project-root}/docs/ci.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-simplify-release-architecture.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Manual Release run `29763400936` published `v3.78.0` completely, but the job reported failure afterward. The optional `@semantic-release/github` success hook interpreted historical `/pushall` branch fragments `fix/gh-29738838856-...` and `fix/gh-29720431798-...` as issue-closing references, then failed GraphQL resolution for those workflow-run IDs.

**Approach:** Configure the GitHub semantic-release plugin to skip issue and pull-request success comments through its supported `successCommentCondition: false` option. Preserve GitHub Release and asset publication, add a focused governance contract, and document why release completion does not perform issue-reference notification.

## Boundaries & Constraints

**Always:** Treat `v3.78.0` as successfully and immutably published at source SHA `a21517e3b66458e997d1ea2f4df5072c4abde628`. Keep manual dispatch, exact-green-`main` verification, protected `production` approval, the immutable Builds execution pin, secret and collision preflights, the exact 14-package manifest, GitHub assets, multi-platform OCI validation, and both platform smoke tests unchanged. Use the installed plugin's non-deprecated condition option and cover its exact JSON value with a regression test.

**Ask First:** Any real release dispatch; any mutation or deletion of the existing tag, GitHub Release, NuGet packages, assets, or container; disabling the GitHub publication plugin; changing shared Builds code, workflow permissions, release triggers, merge conventions, or historical commits; or broadening the change beyond post-publication success notification.

**Never:** Retry or recreate `v3.78.0`; bypass collision protection; hide build, validation, package, container, or GitHub publication failures with `continue-on-error`; rewrite release history to remove run IDs; use deprecated `successComment: false`; or weaken publication gates to make the workflow green.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Release notes contain issue-like CI run IDs | A published release includes commits with `fix/gh-<run-id>` fragments | GitHub Release/assets remain published; the success hook skips issue/PR parsing and returns successfully | No GraphQL lookup or comment is attempted for the numeric run IDs |
| Normal successful release | A release contains only ordinary Conventional Commit subjects | Publication completes and the job remains green without issue/PR success comments | Optional comments are consistently skipped rather than changing by commit text |
| Publication fails before success notification | A required build, preflight, package, container, smoke, or GitHub publish operation fails | The release job remains failed with the primary error visible | The new setting must not suppress or downgrade required publication failures |
| Existing release validation | `v3.78.0` already exists at the intended source | Verification observes it read-only and does not dispatch semantic-release | Existing collision gates remain authoritative |

</frozen-after-approval>

## Code Map

- `.releaserc.json` -- configures GitHub Release/assets publication and the currently default-enabled success notification hook.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/semantic-release-github-success.mjs` -- invokes the installed GitHub plugin's success lifecycle behind a fake GitHub HTTP boundary.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` -- owns structural contracts and runs the behavioral fixture in the normal governance test lane.
- `docs/ci.md` -- documents the active manual release flow, mutation boundary, and operator expectations.
- `package-lock.json` -- pins the `@semantic-release/github` implementation exercised by the behavioral fixture.

## Tasks & Acceptance

**Execution:**
- [ ] `.releaserc.json` -- set `successCommentCondition` to JSON `false` on the existing `@semantic-release/github` options while retaining the `nupkgs/*.nupkg` asset mapping -- prevents false post-publication failures without removing GitHub Releases.
- [ ] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/semantic-release-github-success.mjs` -- load the real plugin options, invoke the installed plugin's success hook for issue-like and ordinary commit histories, fake every GitHub HTTP response, and fail on GraphQL or numeric issue/PR comment/label calls -- proves the pinned implementation skips the failure path without external mutation.
- [ ] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` -- assert the exact asset and non-deprecated condition configuration, then execute the Node fixture in the normal xUnit governance lane -- prevents a config-only test from overstating runtime behavior.
- [ ] `docs/ci.md` -- state that release completion intentionally omits referenced issue/PR comments and labels because branch-name fragments embedded in merge commit messages can resemble issue references -- aligns operator expectations with the reliable status contract.

**Acceptance Criteria:**
- Given release history containing `fix/gh-<run-id>` fragments, when semantic-release completes GitHub publication, then optional issue/PR notification cannot turn the successful release job red.
- Given the updated GitHub plugin configuration, when governance tests inspect it, then assets remain enabled and `successCommentCondition` is exactly JSON `false`.
- Given any required publication phase fails, when the workflow exits, then its primary failure remains blocking and visible.
- Given the existing `v3.78.0` publication, when this change is verified, then no release dispatch or external artifact mutation occurs.

## Spec Change Log

- Review loop 1: parallel review found that a structural JSON assertion did not prove the frozen issue-like and ordinary-history behaviors. The plan now executes the installed, lockfile-pinned GitHub success hook with real `.releaserc.json` options and a fake HTTP boundary, and registers that fixture through xUnit. Avoid the known-bad config-only claim that unconditional JSON alone covers runtime histories. KEEP the exact `successCommentCondition: false` setting, asset mapping, manual/exact-source/protected-production/immutable-publication gates, documentation rationale, and prohibition on release dispatch or external mutation.

## Design Notes

The fixture verifies the installed, lockfile-pinned `@semantic-release/github` success lifecycle rather than inferring behavior from configuration alone. It supplies the real plugin options and deterministic fake GitHub responses, permits only unrelated repository and stale-failure cleanup reads, and rejects GraphQL plus numeric issue/PR comment or label mutations. The plugin's publish phase remains responsible for the GitHub Release and package assets; the change is limited to its optional success notification path.

## Verification

**Commands:**
- `node -e "JSON.parse(require('fs').readFileSync('.releaserc.json', 'utf8'))"` -- expected: configuration parses successfully.
- `node tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/semantic-release-github-success.mjs` -- expected: both histories complete with no GraphQL or numeric issue/PR notification calls and no external network.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -warnaserror -m:1` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.ContainerPublishingGovernanceTests` -- expected: all focused governance tests pass.
- `actionlint .github/workflows/release.yml` -- expected: surrounding release workflow remains valid and unchanged in behavior.
- `git diff --check` -- expected: no whitespace errors.
- `git diff --no-index --check -- /dev/null _bmad-output/implementation-artifacts/spec-gh-29763400936-fix-release-post-publish-status.md` -- expected: exit 1 because the spec is new, with no whitespace diagnostics.

**Manual checks:**
- Read back the `v3.78.0` tag, GitHub Release, 14 assets, NuGet publication, OCI manifests/digest, and target SHA without dispatching a workflow; expect the existing publication to remain unchanged.
