---
title: 'Fix false failure after successful release publication'
type: 'bugfix'
created: '2026-07-20'
status: 'in-progress'
review_loop_iteration: 2
baseline_commit: 'af66f6c46b1356bb569e7192d15105be47bc5a19'
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
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` -- owns structural contracts for the exact GitHub plugin options and blocking publication commands.
- `.github/workflows/ci.yml` -- owns a blocking Node governance job with an explicit supported runtime and clean lockfile install.
- `docs/ci.md` -- documents the active manual release flow, mutation boundary, and operator expectations.
- `package-lock.json` -- pins the `@semantic-release/github` implementation exercised by the behavioral fixture.

## Tasks & Acceptance

**Execution:**
- [ ] `.releaserc.json` -- set `successCommentCondition` to JSON `false` on the existing `@semantic-release/github` options while retaining the `nupkgs/*.nupkg` asset mapping -- prevents the identified numeric-reference failure without removing GitHub Releases.
- [ ] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/semantic-release-github-success.mjs` -- verify the installed version matches `package-lock.json`; invoke the success hook for issue-like and ordinary histories with production-equivalent functional options; route all HTTP to loopback; allow only repository reads and the unrelated `getSRIssues` cleanup query; reject associated-PR/related-issue GraphQL and numeric comment/label mutations -- proves the pinned path skips run-ID resolution.
- [ ] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` -- retain exact structural option/asset assertions and prove the surrounding publish commands stay blocking, without starting Node from xUnit -- keeps the .NET lane dependency-pure.
- [ ] `.github/workflows/ci.yml` -- add a blocking EventStore-owned semantic-release governance job using the SHA-pinned setup-node action, explicit supported Node 22, `npm ci`, and the behavioral fixture -- makes the proof reproducible from a clean hosted checkout without changing shared Builds.
- [ ] `docs/ci.md` -- document the notification choice, merge-message trigger, and dedicated blocking governance lane -- aligns operator expectations with the reliable status contract.

**Acceptance Criteria:**
- Given release history containing `fix/gh-<run-id>` fragments, when semantic-release completes GitHub publication, then optional issue/PR notification cannot turn the successful release job red.
- Given the updated GitHub plugin configuration, when governance tests inspect it, then assets remain enabled and `successCommentCondition` is exactly JSON `false`.
- Given any required publication phase fails, when the workflow exits, then its primary failure remains blocking and visible.
- Given the existing `v3.78.0` publication, when this change is verified, then no release dispatch or external artifact mutation occurs.

## Spec Change Log

- Review loop 1: parallel review found that a structural JSON assertion did not prove the frozen issue-like and ordinary-history behaviors. The plan now executes the installed, lockfile-pinned GitHub success hook with real `.releaserc.json` options and a fake HTTP boundary, and registers that fixture through xUnit. Avoid the known-bad config-only claim that unconditional JSON alone covers runtime histories. KEEP the exact `successCommentCondition: false` setting, asset mapping, manual/exact-source/protected-production/immutable-publication gates, documentation rationale, and prohibition on release dispatch or external mutation.
- Review loop 2: hosted CI run `29766169684` proved the xUnit wrapper lacked npm dependencies, and review found the fixture disabled production's stale-failure cleanup. The plan now runs the fixture in a dedicated blocking CI job after explicit Node setup and `npm ci`, verifies the installed lockfile version, and permits only the cleanup GraphQL operation while rejecting run-ID resolution. Avoid the known-bad dependency-populated local-only result and `failCommentCondition: false` test override. KEEP the exact notification setting and assets, structural assertions, loopback-only HTTP boundary, both history cases, documentation rationale, all release gates, and no dispatch/external mutation.

## Design Notes

The fixture changes only transport options to target loopback; functional plugin options remain those in `.releaserc.json`. The pinned plugin's success lifecycle still performs unrelated stale-failure cleanup, so the fake boundary recognizes only its `getSRIssues` query and returns no stale issues. Associated-PR or related-issue queries, run-ID variables, numeric notification mutations, and non-loopback traffic fail closed. The publish phase remains responsible for the GitHub Release and assets.

## Verification

**Commands:**
- `node -e "JSON.parse(require('fs').readFileSync('.releaserc.json', 'utf8'))"` -- expected: configuration parses successfully.
- `npm ci` followed by `node tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/semantic-release-github-success.mjs` -- expected: the locked plugin version and both histories pass with only allowed loopback requests.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -warnaserror -m:1` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.ContainerPublishingGovernanceTests` -- expected: all focused governance tests pass.
- `actionlint .github/workflows/ci.yml .github/workflows/release.yml` -- expected: CI provisioning and the unchanged release workflow are valid.
- `git diff a21517e3b66458e997d1ea2f4df5072c4abde628 --check` -- expected: no whitespace errors across the baseline diff.

**Manual checks:**
- Read back the `v3.78.0` tag, GitHub Release, 14 assets, NuGet publication, OCI manifests/digest, and target SHA without dispatching a workflow; expect the existing publication to remain unchanged.
