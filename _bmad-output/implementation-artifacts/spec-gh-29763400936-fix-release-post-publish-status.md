---
title: 'Fix false failure after successful release publication'
type: 'bugfix'
created: '2026-07-20'
status: 'in-review'
review_loop_iteration: 3
baseline_commit: 'f435d968eae603bf377809925f78f25fdac5f4f5'
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
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- owns fail-closed structural governance for every required CI job.
- `.github/workflows/ci.yml` -- owns a blocking Node governance job with an explicit supported runtime and clean lockfile install.
- `docs/ci.md` -- documents the active manual release flow, mutation boundary, and operator expectations.
- `package-lock.json` -- pins the `@semantic-release/github` implementation exercised by the behavioral fixture.

## Tasks & Acceptance

**Execution:**
- [x] `.releaserc.json` -- retain the exact GitHub option set of `assets` plus JSON `successCommentCondition: false` -- prevents the identified numeric-reference failure without changing failure cleanup or GitHub Release publication.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/semantic-release-github-success.mjs` -- verify the installed lockfile version; isolate each history's public plugin module; use production functional options; enforce loopback on the actual Undici global dispatcher; require the exact `/graphql` cleanup document, repository, and failure-label variables with no URL/body run IDs; reject other GraphQL, numeric notification mutations, or egress -- proves the pinned no-stale-issue path skips run-ID resolution.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` -- assert the exact GitHub option-key set and retain blocking publication-command contracts without starting Node from xUnit -- keeps the .NET lane dependency-pure.
- [x] `.github/workflows/ci.yml` and `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- add and structurally govern one unconditional blocking job with SHA-pinned checkout/setup-node, Node 22, cache-backed `npm ci`, and direct fixture execution in order -- makes the clean-hosted proof required and regression-resistant without changing shared Builds.
- [x] `docs/ci.md` -- document all three blocking CI jobs, the notification choice, merge-message trigger, and governance lane -- aligns operator expectations with the reliable status contract.

**Acceptance Criteria:**
- Given release history containing `fix/gh-<run-id>` fragments, when semantic-release completes GitHub publication, then optional issue/PR notification cannot turn the successful release job red.
- Given the updated GitHub plugin configuration, when governance tests inspect it, then assets remain enabled and `successCommentCondition` is exactly JSON `false`.
- Given any required publication phase fails, when the workflow exits, then its primary failure remains blocking and visible.
- Given the existing `v3.78.0` publication, when this change is verified, then no release dispatch or external artifact mutation occurs.

## Spec Change Log

- Review loop 1: parallel review found that a structural JSON assertion did not prove the frozen issue-like and ordinary-history behaviors. The plan now executes the installed, lockfile-pinned GitHub success hook with real `.releaserc.json` options and a fake HTTP boundary, and registers that fixture through xUnit. Avoid the known-bad config-only claim that unconditional JSON alone covers runtime histories. KEEP the exact `successCommentCondition: false` setting, asset mapping, manual/exact-source/protected-production/immutable-publication gates, documentation rationale, and prohibition on release dispatch or external mutation.
- Review loop 2: hosted CI run `29766169684` proved the xUnit wrapper lacked npm dependencies, and review found the fixture disabled production's stale-failure cleanup. The plan now runs the fixture in a dedicated blocking CI job after explicit Node setup and `npm ci`, verifies the installed lockfile version, and permits only the cleanup GraphQL operation while rejecting run-ID resolution. Avoid the known-bad dependency-populated local-only result and `failCommentCondition: false` test override. KEEP the exact notification setting and assets, structural assertions, loopback-only HTTP boundary, both history cases, documentation rationale, all release gates, and no dispatch/external mutation.
- Review loop 3: review proved `globalThis.fetch` does not guard the plugin's imported Undici transport and found no structural contract preventing the new job from being skipped or softened. The plan now guards the actual global Undici dispatcher, validates the exact cleanup endpoint/document/variables and independent module lifecycle, and adds fail-closed CI-job governance tests. Avoid the known-bad bypassable egress claim, permissive `getSRIssues` name check, and ungoverned job. KEEP the clean Node 22 + `npm ci` job shape, installed-version proof, real functional options, empty stale-issue response, both histories, exact release configuration/assets, blocking release gates, docs rationale, and no external mutation.

## Design Notes

The fixture changes only transport options; functional plugin options remain those in `.releaserc.json`. A guarded Undici dispatcher delegates solely to the selected loopback origin and rejects any other destination before I/O. For each independently loaded public plugin instance, the fake GitHub boundary accepts repository metadata plus the pinned `getSRIssues` query with exact owner/repository/failure-label variables and returns no stale issue. URL/body run IDs, extra GraphQL operations, numeric notification mutations, and egress fail closed. The publish phase remains responsible for GitHub Release/assets.

## Verification

**Commands:**
- `node -e "JSON.parse(require('fs').readFileSync('.releaserc.json', 'utf8'))"` -- expected: configuration parses successfully.
- `npm ci` followed by `node tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/semantic-release-github-success.mjs` -- expected: the locked plugin version and both histories pass with only allowed loopback requests.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -warnaserror -m:1` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.ContainerPublishingGovernanceTests` -- expected: all focused governance tests pass.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests` -- expected: CI job shape and its negative skip/tolerance cases pass with the package contracts.
- `actionlint .github/workflows/ci.yml .github/workflows/release.yml` -- expected: CI provisioning and the unchanged release workflow are valid.
- `git diff f435d968eae603bf377809925f78f25fdac5f4f5 --check` -- expected: no whitespace errors across the review delta; the earlier `a21517e3` audit remains separate historical evidence.

**Manual checks:**
- Read back the `v3.78.0` tag, GitHub Release, 14 assets, NuGet publication, OCI manifests/digest, and target SHA without dispatching a workflow; expect the existing publication to remain unchanged.

**Results:**
- Review loop 3 replaced the bypassable `globalThis.fetch` claim with a guard installed on the actual Undici global dispatcher used by the pinned plugin. The fixture first proves it resolves the same Undici module as the installed plugin, then proves the guard rejects an intentional `https://api.github.com/semantic-release-egress-probe` request before delegation while every plugin request uses the selected loopback origin.
- `.releaserc.json` parsed successfully and both JavaScript and C# governance assert its exact GitHub option keys are only `assets` and JSON `successCommentCondition: false`. A clean `npm ci` installed 349 packages and reported the repository's existing one high-severity audit finding without failing installation.
- The fixture confirmed installed `@semantic-release/github` `12.0.8` matches `package-lock.json`. Two independently imported public plugin wrappers passed issue-like and ordinary histories through six selected-origin requests: each wrapper performed two exact repository metadata reads and exactly one byte-exact `getSRIssues` document with owner `Hexalith`, repository `Hexalith.EventStore`, and the plugin's exact duplicate default failure-label variables. The response contained no stale issues. URL/body run IDs, query strings, other GraphQL, numeric notification mutations, unexpected endpoints, and any non-selected origin fail closed.
- The focused Contracts Tests Release build succeeded with zero warnings and zero errors. `ContainerPublishingGovernanceTests` passed 9/9 with exact configuration and blocking publication-command assertions and no Node child process. `ReleasePackageManifestTests` passed 28/28, including the unique unconditional governance-job shape, exact SHA-pinned checkout/setup-node, Node 22/cache/`npm ci`/fixture ordering, and five negative job/step skip, dependency, and tolerance mutations.
- `actionlint .github/workflows/ci.yml .github/workflows/release.yml`, `git diff f435d968eae603bf377809925f78f25fdac5f4f5 --check`, and the final `git diff --check` passed. The dedicated CI job is restored exactly once; all three CI jobs are structurally blocking. The release workflow, package lock, shared Builds, submodules, permissions, and triggers remained unchanged.
- Frozen matrix audit — **Release notes contain issue-like CI run IDs:** the isolated issue-like wrapper completed with no URL/body run ID, reference-resolution query, or numeric notification. **Normal successful release:** the isolated ordinary-history wrapper completed under the same exact request contract. **Publication fails before success notification:** structural tests retain blocking preflight/NuGet/container commands and reject CI skip/tolerance mutations. **Existing release validation:** the approved read-only evidence for run `29763400936` remains authoritative: `v3.78.0` targets `a21517e3b66458e997d1ea2f4df5072c4abde628`, has 14 GitHub assets and 14 published NuGet packages, and its two-platform OCI validation and smoke checks passed. This loop did not dispatch Release, perform remote operations, or mutate external artifacts.
