---
title: 'Synchronize and publish the current EventStore release'
type: 'chore'
created: '2026-11-01'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'd83ecb43ef997f947d08b789d0625a8fa90193b6'
context:
  - 'AGENTS.md'
---

<frozen-after-approval reason="The user explicitly authorizes synchronization, upgrades, CI repairs and release publication without further human validation">

## Intent

**Problem:** EventStore must ship from synchronized source with current supported dependencies and successful CI. Local main was behind two upstream commits fixing a reusable workflow contract and updating published CLI tools; Builds and Memories also have newer root-submodule commits.

**Approach:** Consume upstream repairs, verify SDK and package freshness against official registries, address demonstrated CI failures, push the final source, and run the configured release workflow. Verify the resulting fourteen NuGet package identities directly from the registry.

## Boundaries & Constraints

**Always:** Work in the owning repository, preserve unrelated work, keep central version ownership in Builds, use stable supported versions except established preview integrations, validate exact Git messages using pinned commitlint, and require successful CI on the release source. The parent owns all Git and remote operations. Use the authenticated reviewer's supported deployment-review API when production review is required, as expressly authorized by the user; preserve environment policy.

**Never:** Initialize or update nested submodules, load skills from references, rewrite published tags or packages, suppress failing checks, weaken warnings or security gates, introduce local package-version overrides, or claim publication without checking the registry. The scope is release maintenance for EventStore; unrelated ecosystem migrations remain with their owners.

</frozen-after-approval>

## Code Map

- `global.json`: SDK 10.0.403 is already the latest stable SDK in Microsoft's live release index; .NET 11 remains prerelease.
- `.gitmodules`: nine root-declared dependencies. All were clean main checkouts. Only Builds and Memories were behind their fetched main tips.
- `references/Hexalith.Builds/Props/Directory.Packages.props`: authoritative versions. Newly fetched Builds updates published domain package families and a registry audit.
- `.config/dotnet-tools.json`: upstream now pins EventStore Admin CLI 3.130.2 and Builds Module CLI 4.41.1; preserve DefaultDocumentation 1.2.6 if current.
- `package.json`, `package-lock.json`, `scripts/payload-protection/requirements.txt`: verify Node and Python pins against live registries.
- `.github/workflows/ci.yml`: upstream d83ecb43ef997f947d08b789d0625a8fa90193b6 partially corrects the legacy reusable-workflow contract. Complete it by removing the remaining opt-in `module-manifest` input: v2 preparation correctly rejects the unpublished Platform/tag/catalog tuple. Preserve every existing build, consumer, unit and AppHost integration check and retain the manifest for its eventual qualified activation.
- `.github/workflows/release.yml`, `scripts/verify-release-source.sh`: manual dispatch from current main requires successful push CI and Commitlint for exactly that commit. Production uses the existing required reviewer, OIDC NuGet login and immutable Builds publisher.
- `.releaserc.json`, `tools/release-packages.json`: semantic-release selects the version; package manifest declares fourteen outputs. Preserve release branch behavior.
- `tools/tests/test_release_package_contract.py`: existing packaging guard checks; `scripts/check-committed-files.sh --staged` checks final staged files.
- `Directory.Build.targets`, `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DependencyModeEvaluationTests.cs`: scope promoted Tenants overrides to the normalized selected directory and execute real targets with relative selected/foreign references. Assert child versions, preserved edge metadata and mapping exclusion.

## Tasks & Acceptance

**Execution:**

- [x] Parent fetches and fast-forwards main and root-declared submodules without touching nested submodules.
- [x] Audit actual direct dependency identities and update only justified stale applicable versions; document retained compatibility constraints.
- [x] Implementation handoff inspects root tool pins and verifies installed tools plus existing release package/governance checks. Edit root files only if the live audit establishes a required change. Return precise results; no Git or remote actions.
- [ ] Parent examines hosted CI, repairs demonstrated failures, and records successful exact-source checks.
- [ ] Parent commits and pushes reviewed maintenance and gitlinks after exact message validation.
- [ ] Parent dispatches Release, submits the supported production deployment review as authorized, and verifies release and all fourteen NuGet package repository commits.

**Acceptance Criteria:**

- Given live registry metadata, when pins are checked, then the SDK and applicable dependencies are latest supported or have a specific compatibility reason to retain their version.
- Given root dependency synchronization, when Git state is inspected, then each root submodule is clean and its selected commit exists on origin/main, with nested submodules unchanged.
- Given final main source, when CI and Commitlint finish, then successful push runs identify that exact source before Release starts.
- Given completed publication, when manifest package downloads are inspected, then all fourteen IDs, versions and repository commits match the release and source.

## Implementation Notes

User authorization resolves planning and release confirmation checkpoints. This is a new execution record; earlier refresh evidence remains historical. Parent has already fast-forwarded the clean root main and the two outdated root submodules. No new runtime code is currently required.

Hosted build job 124911864183 in CI run 41327983482 failed before restore: opt-in package preparation returned HXW011 for unreleased Platform 4c43913231b54214b40574d02ce5ed5ed30c470d and HXW009 for its Builds pin mismatch. History shows 61cf9d6f191dd3b539d2414aa56de964af602279 introduced three v2 inputs together; d83ecb43ef997f947d08b789d0625a8fa90193b6 removed two unsupported inputs but left the manifest opt-in. The preceding legacy invocation contains all the same blocking test/consumer lanes without that input. Builds preparation README expressly requires a qualified tuple and describes this as opt-in. Complete the caller correction rather than changing the strict producer resolver or inventing a Platform release.

## Spec Change Log

## Review Triage Log

| Finding | Verdict and evidence | Disposition |
| --- | --- | --- |
| Blind: PostgreSQL qualifier still pending | false as a completed-qualification claim: DW-545 explicitly keeps the actual qualifier pending, and this execution has not claimed it passed or closed the owner record. | Preserve the existing pending owner requirement; inspect hosted integration separately. |
| Blind: promoted-reference regression absent | medium: evaluation-only fixtures do not execute the new target; a regression could restore mixed assembly identities without detection. | Patch with executable target/child-version fixture. |
| Blind: filename selector exceeds selected checkout | medium: the target can alter a Tenants-named reference outside the selected checkout, unlike the evaluation rule. | Patch by reusing the selected path restriction and verify a foreign-path fixture remains unchanged. |
| Blind: add container verification to spec | low, spec-only request. The existing publisher verifies index, architectures and provenance; parent can inspect retained release output. | Reject spec-only edit; publication outcome still must be observed. |
| Blind: add registry evidence to spec | low, spec-only request. Official interfaces and exact observed versions are recorded; live indexes and the existing durable Builds audit are available. | Reject spec-only edit. |
| Edge reviewer | No findings. | Complete. |
| Verification gap: promoted-reference test missing | medium: independently confirmed evaluation-only and topology tests cannot detect loss of target-time version propagation. | Same regression-test patch as the individually triaged blind finding. |

## Verification

- Official .NET release index, NuGet flat-container indexes, npm registry and PyPI metadata establish package freshness.
- `dotnet tool restore` and installed CLI version probes validate the updated manifest.
- Python release contract tests and Node semantic-release governance fixture cover existing publication boundaries.
- `git diff --check`, staged-file guard and pinned commitlint validate the final commit.
- Hosted workflow runs, GitHub Release metadata and downloaded NuGet nuspec repository metadata establish publication outcome.

### Implementation handoff verification — 2026-11-01

- No root dependency files changed: the live official registry audit found every applicable root pin current. Microsoft's release index reports .NET SDK `10.0.403` as the latest stable SDK; .NET 11 is still `11.0.100-rc.3.26522.148`.
- NuGet flat-container indexes confirm the latest stable tool versions are DefaultDocumentation.Console `1.2.6`, Hexalith.EventStore.Admin.Cli `3.130.2`, and Hexalith.Builds.Module.Cli `4.41.1`. `dotnet tool restore` succeeded for all three. Admin CLI and Module CLI `--version` probes exited 0 and reported their pinned versions. DefaultDocumentation `--version` and `--help` printed the expected `1.2.6` identity/help but returned 255; this informational-command behavior remains unchanged.
- The npm registry's `latest` tags match all eight direct locked development dependencies: semantic-release `25.0.9`, exec `7.1.0`, github `12.0.10`, commit-analyzer `13.0.1`, release-notes-generator `14.1.1`, commitlint CLI/config-conventional `21.2.4`, and husky `9.1.7`. `npm ls --depth=0` confirmed the installed versions. Node `26.4.0` satisfies the declared supported engine range; production remains on the configured Node 24 lane.
- PyPI reports cryptography `50.0.2` as latest and requires Python `!=3.9.0,!=3.9.1,>=3.9`. The existing host environment has `46.0.5`, so verification installed the pinned `50.0.2` in an automatically removed temporary virtual environment. Both golden-vector verifiers passed on Python `3.14.4` / Node `26.4.0`; their complete emitted derived objects matched, with V001–V003 executed and 135 vectors still explicitly predecessor-gated.
- Passed `python3 -m unittest discover -s tools/tests -p test_release_package_contract.py` (2 tests), `test_release_publication_authority.py` (10), `test_release_source_branch.py` (4), `test_release_candidate.py` (11), and `test_published_runner_exit.py` (2): 29 tests total.
- Passed `node tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/semantic-release-github-success.mjs`: two isolated histories, six selected-origin requests, and two external Undici probes blocked. Passed `bash scripts/tests/generated-api-smoke-preflight.test.sh`: 56 assertions, zero failures.
- `git diff --check` passed. `bash scripts/check-committed-files.sh` passed for 9,900 tracked index entries. The final staged-file guard, exact Git-message validation, hosted exact-source CI and publication checks remain parent-owned.

### Verified legacy CI caller correction — 2026-11-01

- Removed only the legacy caller's remaining `module-manifest` opt-in from `.github/workflows/ci.yml`; comments retain the activation requirement for a qualified Platform/tag/catalog tuple and explicitly preserve blocking AppHost integration. Every job, test suite, consumer-validation setting, assertion and module manifest remains unchanged. No Builds resolver, release tag or package pin changed.
- `actionlint .github/workflows/ci.yml .github/workflows/release.yml` exited 0 with no diagnostics. `git diff --check` passed.
- Re-ran the release package contract (2 tests), publication authority (10), finite source-proof (4), and native candidate qualification (11) regressions: all 27 tests passed. Re-ran the semantic-release GitHub success lifecycle fixture: both isolated histories passed, with six selected-origin requests and two external Undici probes blocked.
- No new test was added solely to mirror workflow YAML. Successful hosted CI on the final pushed source remains required before Release dispatch.

Concurrent upstream integration: 6ea3e51e62e856d98721a5f570319af3acac3ab3 independently contains the same manifest opt-in correction plus source-reference identity fixes. The parent removed its duplicate local workflow edit and fast-forwarded to that published commit. Remaining local changes are this execution record and four root submodule pointers: Builds 17616d439b36957a7c5da078e293e598bb339790, FrontComposer fdb61860e6eea6ce8f8bfb0bc3d6235406f7f222, McpCli 3bc89642ad43305531c4018b75b5e1b9e0681e10, and Memories 76c737bfcb3b5ecf9ce26191eab13749fdade8d6. All root submodules are clean and aligned with their fetched main tips.

Review patch verification: the executable fixture reproduced the defect before the fix: the foreign Tenants child received 3.130.2 instead of its own 4.5.6. Target-time Update patterns are ignored by MSBuild, so the final correction retains a metadata condition and matches the normalized selected directory with a trailing separator. The final fixture uses relative references and a selected-other sibling. All 25 DependencyModeEvaluationTests cases passed after the patch; the Debug build reported zero warnings/errors. No public surface, mapping authority, package graph or production gate changed. Parent inspected the complete patch, and diff/workflow checks pass.

Hosted Contracts follow-up: run 41328376099 completed 1,444 cases with one failure, 1,441 successes and two inventory-conditioned skips. The failure was the unclassified new Platform.IntegrationTests project. docs/ci.md now explicitly defers that unactivated v2 profile until its qualified Platform/tag/catalog tuple exists, preserving the blocking AppHost lane. The existing Test_projects_are_classified_into_release_live_advisory_or_deferred_lanes test passed 1/1 via the built Debug assembly. The package job already proved all fourteen release packages and consumer contracts. Source patch committed as 535a57105e8611779e0a2ba4d21a9554b4209988 after pinned commitlint and the populated staged-file guard.

Final CI follow-up on 0d58457397d13845d3618a601d7a4f7118c7d6fe: all five independent CI jobs passed, and AppHost integration passed 299/299. The unit wrapper step returned success while retaining a failed-project count; final aggregation correctly refused release because Server.Tests had one secret-scanner false positive in PolicyLifecycleTests.cs:28. The value is generated by RandomSecret(), not a literal. A trailing comma matching the same file's other initializers makes the existing scanner recognize the runtime expression; no scanner rule or test assertion changed. Serialized Debug Server.Tests build passed with zero warnings/errors; TrackedReusableContent_DoesNotContainUsableSecrets passed 1/1. The PayloadProtection.Tests Debug build also passed with zero warnings/errors and all 24 PolicyLifecycleTests passed. Logs: /tmp/eventstore-secret-guard-build.log, /tmp/eventstore-secret-guard-test.log, /tmp/eventstore-policy-fixture-build.log and /tmp/eventstore-policy-fixture-test.log.

A final root-only fetch also found newer committed maintenance in Builds, FrontComposer and Tenants. Builds changes only Works-specific MSBuild package ownership, preserving EventStore's numeric package versions; FrontComposer and Tenants changes are CI/docs/gitlink synchronization. Their root checkouts were fast-forwarded without initializing or updating nested submodules. The duplicate Platform integration row created by concurrent documentation commits was consolidated by removing this execution's duplicate while retaining the upstream row. Fresh exact-source CI remains required before dispatch.
