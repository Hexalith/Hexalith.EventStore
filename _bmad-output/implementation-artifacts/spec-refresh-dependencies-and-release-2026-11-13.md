---
title: Refresh dependencies and restore a publishable EventStore build
type: bugfix
created: 2026-11-13
status: done
route: dispatch
review_loop_iteration: 0
baseline_commit: 83f756886917ea00ff9431973f6949f9467f0ea2
context:
  - /home/administrator/projects/hexalith/eventstore/AGENTS.md
  - /home/administrator/projects/hexalith/eventstore/references/Hexalith.AI.Tools/hexalith-llm-instructions.md
  - /home/administrator/projects/hexalith/eventstore/references/Hexalith.AI.Tools/hexalith-git-instructions.md
---

<frozen-after-approval reason="User explicitly authorizes updates, pushes, release and bypass of human validation">

## Intent

**Problem:** Current main fails CI before its normal build and in six package-authority contract tests. The user requests all changes pushed, synchronized direct submodules, the latest SDK and packages, repaired CI/CD, and a release whose NuGet publication is independently confirmed.

**Approach:** Refresh supported stable dependencies through their owning catalog, correct stale validation inputs, preserve meaningful package and compatibility gates, and use the existing release workflow after exact-source CI succeeds. The user has already authorized publication and bypassing human checkpoints; no further confirmation is needed.

## Boundaries & Constraints

**Always:** Preserve user work. Commit content in its owning repository, validate every authored Git message with pinned commitlint, push submodule commits before recording them in EventStore, and keep nested submodules uninitialized. Retain technical release checks, package mode and Release CI, warning/error policy, all configured test inventories, and exact-source CI verification. Use the production pending-deployment approval API under the authenticated maintainer to satisfy the authorized release gate without permanently changing environment protection.

**Never:** Fabricate release tags or package identities; delete checks to conceal failures; downgrade an intentionally selected preview to an older stable version; claim an unpublished package is published; load skills from any repository references directory.

</frozen-after-approval>

## Code Map

- `global.json`: already SDK 11.0.100, verified as latest stable in Microsoft's release metadata; no artificial version bump.
- `references/Hexalith.Builds/Props/Directory.Packages.props`: shared version authority. Eighty EventStore direct dependencies/tools were checked against NuGet. JsonSchema.Net 9.6.0 is the sole normal stable package update, to 9.7.0. The catalog has conditional historical compatibility selections; preserve those.
- `references/Hexalith.Builds/Tools/README.md`: revision-bound audit instructions; preserve prior audit and refresh only the affected family after the catalog commit exists. The audit must reference the exact catalog revision. Read owning guidance before editing this repository.
- `.config/dotnet-tools.json`: module CLI 5.0.1 should become 5.2.1, Admin CLI 4.2.0 should become 5.0.0; defaultdocumentation is current.
- `package.json`, `package-lock.json`: GitHub release plugin 12.0.10 should become 12.0.11. Other direct npm dependencies are current. Keep exact commitlint pins and provenance checks.
- `scripts/validate-eventstore-package-authority.ps1`: normalized-LF checksum of Builds central validator is stale. Current approved companion hash is `329e02427b80ac34d3d9f17b3603a2084411e34d126b90a354756fb849b71003`; independently recompute before using. Consumer authority hash remains unchanged. Existing Contracts tests exercise refusal behavior.
- `.github/workflows/ci.yml`: `module-manifest` invokes shared prepare-module-build before compilation. This input arrived with runner-owned Platform integration in `f4134a067f`; `585791e4e` intentionally deferred that integration by owner decision on 2026-11-12 but left its preparation input behind. Complete that existing deferral by omitting this input from standalone producer CI, updating its assertion in `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs`, and documenting why in `docs/ci.md` and the existing deferred-work ledger. Keep every package build, unit and consumer-validation gate; record that no Platform tuple qualification is claimed. Do not alter the shared tool's strict identity checks. The published tool upgrade remains useful for its independently qualified consumers.
- `.github/workflows/release.yml`, `.releaserc.json`, `tools/release-packages.json`: manual release, fourteen packages, source proof then production environment, semantic versioning, API compatibility and container publication.
- `/tmp/eventstore-package-audit-20261113.json`: live dependency audit; `/tmp/ci-contracts-131871919353.log` and `/tmp/ci-build-131871919539.log`: exact failed job logs.
- `src/Hexalith.EventStore.ServiceDefaults/Routing/TechnicalHttpActionObservation.cs:136`: full CI exposed a pre-existing cold-reflection race: `ParameterInfo` object reference equality rejects valid MVC parameters when parallel reflection initializes different wrappers. Actual .NET 11 reproduction in `/tmp/eventstore-parameterinfo-race.cs` produced distinct references with identical method/position/token in 24/100 rounds. .NET 11 runtime source `RuntimeMethodInfo.CoreCLR.cs:67` uses unsynchronized lazy `m_parameters ??=` initialization. Replace only this reference comparison with exact metadata provenance: same concrete runtime parameter implementation type, declaring method, metadata token, position, name, parameter type and binding-relevant attributes; preserve `BindingMatches` and every authorization, delegate, image and inventory check. Same concrete implementation type is required to reject custom `ParameterInfo` subclasses that can spoof metadata while changing attributes used by binding checks.
- `tests/Hexalith.EventStore.Server.Tests/Routing/TechnicalHttpInstalledMetadataTests.cs`, `TechnicalHttpCatalogFixture.cs`: existing real installed-endpoint tamper controls and fixture. Add deterministic acceptance of a distinct `MemberwiseClone` of a real runtime parameter before receiver capture, proving the old reference comparison fails and the exact metadata comparison succeeds. Reject foreign member/wrong position/name/type and custom parameter wrappers; retain existing binder, source, model-name, filter and policy refusal controls, including zero state writes/committed catalog on refusal. Keep any added helper type in its own named file.
- `tests/Hexalith.EventStore.Admin.Server.Tests/Routing/DevelopmentActorStateMetadataTimeoutTests.cs`: original CI failure is the `privateCatalog:true` case during AdminBackupsController.GetBackupJobs discovery. It passes 2/2 in isolation, consistent with a cold first-use race. Re-run the built class after repair and the full Admin.Server project (roughly 35 seconds); no test filtering/skipping to hide this failure. Parent owns the full Release build, commit, push and remote workflow monitoring. Implementation agent handles this pending reflection fix and its focused verification only; already completed dependency tasks need no repeated work.

## Tasks & Acceptance

**Execution:**
- [x] Shared catalog and revision-bound audit -- update JsonSchema.Net and validate owner governance; parent handles commits/pushes separately.
- [x] Local tool manifest and npm manifests -- update the three identified tool/plugin versions; restore and audit dependencies.
- [x] Package authority wrapper -- rebind only the changed canonical shared-validator checksum, preserving all refusal checks.
- [x] CI preparation -- complete the already approved Platform integration deferral by removing only its leftover preparation input and aligning documentation and contract assertions; preserve all standalone producer gates and restore prerequisites.
- [x] Technical parameter qualification -- repair concurrent cold-reflection identity checks with exact runtime metadata provenance, deterministic positive/tamper controls, original timeout case and full Admin.Server verification.
- [x] Root gitlinks -- record latest remotely available commits, including any newly published catalog changes; verify all direct repositories are clean and remotely available before the parent commit.
- [x] CI and release -- validate, push, monitor exact-source workflows, fix failures, dispatch release and approve its authorized pending deployment, then verify the resulting fourteen listed NuGet versions and source commit.

**Acceptance Criteria:**
- Given verified registry metadata, when dependencies are selected, then all direct stable dependencies are current and existing intentional previews are retained without downgrade.
- Given an approved shared validator, when authority tests run, then valid catalog checks succeed and tampering/refusal controls remain effective.
- Given a candidate source commit, when release starts, then CI and Commitlint for that exact commit have succeeded and technical publication gates still execute.
- Given successful publication, when NuGet registration/catalog entries are fetched, then fourteen expected package IDs are listed at the released version and identify the released source commit.

## Implementation Notes

The root and all nine declared submodules started clean. Four checkouts have been fast-forwarded to published main: Builds, Platform, Memories and McpCli; no nested checkout was initialized. SDK update is a verified no-op. Implementation agents make content changes and focused verification only; the parent owns commits, push, remote CI and publication. External qualification failures must remain visible.

- Catalog commit `378c62d4249d009b746d0e97c08cd40987554531` and audit commit `cae351e20093634483afad16aca3bfd90845eda8` passed pinned commitlint and were pushed to Builds main. Audit validation passed 317 packages / 157 families; generator controls passed 113 scenarios, central-validator controls 23, and catalog-owned views 131 probes plus 17 frozen/host controls.
- `dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false` and `dotnet build Hexalith.EventStore.slnx --no-restore --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` passed with zero warnings/errors. Built-assembly focused CI checks passed 3 tests and descriptor compatibility passed 62 tests with zero skips. Package-authority and audit-validator mutation suites are pending completion; remote exact-source CI and publication remain pending.
- `npm ci --ignore-scripts`, `npm audit`, `npm audit signatures` and the installed GitHub release-plugin fixture passed. `dotnet tool run hexalith-module -- --version` reported `5.2.1+0e7424fdce6373920fb2019d1ad2a74d8fe5295c`; Admin CLI reported `5.0.0+76f743ba0a62c254a66973aa707a701d95a6ce43`.
- SDK source: https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json; NuGet metadata: https://api.nuget.org/v3-flatcontainer/{lowercase-package-id}/index.json; npm metadata: https://registry.npmjs.org/{package-name}/latest. Queried on 2026-11-13. The authoritative shared validator source changed in Builds commit `22fb5907bc4641de54042ed956852c108ca2b0cf`; SHA-256 recomputation over UTF-8 bytes with CRLF normalized to LF matches the wrapper. No shared authority implementation was changed by this task.
- First candidate `cfce9a623ccc6dcb0de85ce9c9e4dbc7eeab392f` was pushed after preflight. Builds CI 43438518491 passed. EventStore CI 43439342682 passed Contracts (1570 total, zero failures, two existing skips) and every unit project except one Admin.Server cold-reflection case. Server.Tests passed 8084, ProviderVerification 80, AppHost 392. Original local authority controls passed 44/44 and audit-validator controls passed 109/109. Live-sidecar integration passed; published compatibility was still running. Do not publish the failed candidate. Logs are available through the job-log API even while an overall workflow reports running.

- Reflection repair: runtime parameter provenance replaces object identity while all binding and authorization checks remain unchanged. The actual endpoint clone regression failed before repair and passes afterward. Focused endpoint/receiver controls passed 52/52; original timeout cases passed 2/2; unfiltered Admin.Server passed 852 total, zero failures and 13 existing ATDD skips. Focused Release builds report zero warnings/errors.
- Aspire baseline: `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --isolated --format Json --non-interactive` succeeded. `aspire describe` reported all 30 resources Running/Healthy before source edits. Both task-owned baseline instances were stopped through the exact AppHost path before source builds; no unrelated AppHost was stopped.

- Pinned CLR source: https://github.com/dotnet/runtime/blob/v11.0.0/src/coreclr/System.Private.CoreLib/src/System/Reflection/RuntimeMethodInfo.CoreCLR.cs (FetchNonReturnParameters and GetParameters). Full solution Release build after the first reflection fix passed with zero warnings/errors in 45.47 seconds. Focused verification used `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net11.0/Hexalith.EventStore.Server.Tests.dll -class '*TechnicalHttpInstalledMetadataTests' -class '*TechnicalHttpCatalogReceiverTests'` and the Admin.Server built assembly with `-class '*DevelopmentActorStateMetadataTimeoutTests'`, then unfiltered. Logs are `/tmp/eventstore-reflection-server-focused.log`, `/tmp/eventstore-reflection-admin-timeout.log`, `/tmp/eventstore-reflection-admin-full.log`; the deterministic original-source failure is `/tmp/eventstore-reflection-clone-before.log`.

- Review patches completed: exact Attributes equality prevents a cloned Optional flag from relaxing a required body. The new refusal test failed before that guard; all 18 edited endpoint cases now pass. Real AdminBackupsController.GetBackupJobs acceptance forces distinct runtime clones for its query and optional CancellationToken parameters, checks HTTP results and unchanged persisted backup state. Final Release solution build passed with zero warnings/errors; unfiltered Admin.Server passed 852 total / zero failures / 13 existing skips; combined endpoint and receiver controls passed 54/54. Evidence: `/tmp/eventstore-reflection-final-solution-build.log`, `/tmp/eventstore-reflection-final-admin-full.log`, `/tmp/eventstore-reflection-final-server-controls.log`.

- Completed publication: [v5.0.1](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v5.0.1), published 2026-11-13T16:02:46Z, targets exact source `741d5cecbbaee14f035e66cb27e1acf50969fa04` and contains all 14 package assets. [Release run 43456476937](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/43456476937) succeeded after exact-source proof and the explicitly authorized one-time production approval (deployment `7769619255`); environment protections were unchanged.
- All nine push workflows passed on that released source, including [CI 43450076421](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/43450076421), [Integration 43450074742](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/43450074742), Commitlint, CodeQL and the independent payload/advisory/design lanes. All 20 unit projects passed; Admin.Server passed 852 total with 13 existing skips, Server passed 8094 total with 25 existing skips, and AppHost passed 392 with zero skips. The existing Platform package-integration deferral remains explicit; no Platform qualification is claimed.
- Independent NuGet verification completed at `2026-11-13T16:22:09.920449+00:00`: **14/14** expected IDs from `tools/release-packages.json` have listed registration/catalog entries at `5.0.1`, downloadable package content (HTTP 200), and repository commit `741d5cecbbaee14f035e66cb27e1acf50969fa04`. For each lowercase package ID, verification read `https://api.nuget.org/v3/registration5-gz-semver2/{id}/5.0.1.json`, followed `catalogEntry`, checked both listing flags/version/ID/repository commit, and sent HEAD to `packageContent`. NuGet accepted all 14 pushes at 15:59:24–15:59:30Z; registrations became available asynchronously. This final documentation receipt does not change the released code or dependency snapshot.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH-1: old producer section calls historical pins current | medium | patch | Correct historical labeling and document the actual standalone current lane. |
| BH-2: integration restore recipe omits complete tuple and secrets | low | patch | Add the existing qualified Platform gitlink, published tool/catalog identity and pull-only secret requirements. |
| BH-3: upgraded tools have no runtime smoke | false | reject | Local tool restore and both built command version probes succeeded at 5.2.1 and 5.0.0. |
| BH-4: audit metadata alone implies compatibility acceptance | false | reject | Audit explicitly retains historical/current metadata distinctions; 62 descriptor schema tests and the broad Release build passed. Shared Builds CI additionally runs its own consumer suites; no Platform acceptance claim is made. |
| BH-5: temporary registry notes lack durable reproduction context | low | reject | Proposed fix edits this build's spec; source endpoints/date and reproducible queries are now recorded in Implementation Notes. No historical temporary file is needed by a runtime or validator. |
| BH-6: checksum provenance absent from notes | low | reject | Proposed fix edits this build's spec; source revision and normalized-byte hash computation are recorded above. Exact positive and tamper controls exist and are running. |

Edge-case review reported no findings in the initial dependency/CI configuration change. Verification-gap review reported no verification gaps.

Reflection-fix review:

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| RF-1: clone Optional flag can relax required-body binding | high | patch | Same runtime type/member/token/position/name/type does not establish Attributes; BindingMatches consumes IsOptional when admitting EmptyBodyBehavior.Allow. Compare Attributes and add the demonstrated refusal control. |
| RF-2: member/token refusal cases do not isolate every individual comparison | low | reject | Existing real-endpoint cases establish refusal of foreign metadata. Requiring each redundant comparison to fail independently adds implementation-mirroring mutation scaffolding without a demonstrated accepted bad request. |
| RF-3: clone acceptance does not force the original query/optional action path | medium | patch | Add deterministic real AdminBackupsController.GetBackupJobs query and CancellationToken clone acceptance with existing full inventory support. |
| RF-4: temporary CLR reproduction and source reference durability | low | reject | The proposed fix edits this build's spec; deterministic tracked endpoint regression already preserves the relevant behavior. Pinned runtime source is recorded below. |
| RF-5: exact focused invocation evidence absent from narrative | low | reject | The proposed fix edits this build's spec; command/evidence receipts are recorded below without changing program behavior. |

The edge-case review worker reported the same optional-flag concern but could not complete its probe: the review service flagged the worker turn for possible cybersecurity risk. The blind reviewer independently reproduced the metadata flag mismatch; the patch uses ordinary defensive validation and focused endpoint tests. Verification-gap review returned no findings.


## Verification

- Owner catalog validation, audit generator/validator and consumer authority checks; npm install/restore, audit and signatures.
- Build and run affected Contracts tests individually; build the Release solution with package dependencies and warnings as errors where practical. Record exact commands and failures.
- `git diff --check`; validate exact commit-message files using pinned commitlint before each mutation.
- GitHub CI/Commitlint on final main, existing release workflow, then independent NuGet registration/catalog checks.
