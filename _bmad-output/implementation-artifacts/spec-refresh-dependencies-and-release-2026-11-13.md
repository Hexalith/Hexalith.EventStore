---
title: Refresh dependencies and restore a publishable EventStore build
type: bugfix
created: 2026-11-13
status: in-review
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

## Tasks & Acceptance

**Execution:**
- [x] Shared catalog and revision-bound audit -- update JsonSchema.Net and validate owner governance; parent handles commits/pushes separately.
- [x] Local tool manifest and npm manifests -- update the three identified tool/plugin versions; restore and audit dependencies.
- [x] Package authority wrapper -- rebind only the changed canonical shared-validator checksum, preserving all refusal checks.
- [x] CI preparation -- complete the already approved Platform integration deferral by removing only its leftover preparation input and aligning documentation and contract assertions; preserve all standalone producer gates and restore prerequisites.
- [ ] Root gitlinks -- record latest remotely available commits, including any newly published catalog changes; verify all direct repositories are clean and remotely available before the parent commit.
- [ ] CI and release -- validate, push, monitor exact-source workflows, fix failures, dispatch release and approve its authorized pending deployment, then verify the resulting fourteen listed NuGet versions and source commit.

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

Edge-case review reported no findings. Verification-gap review reported no verification gaps.

## Verification

- Owner catalog validation, audit generator/validator and consumer authority checks; npm install/restore, audit and signatures.
- Build and run affected Contracts tests individually; build the Release solution with package dependencies and warnings as errors where practical. Record exact commands and failures.
- `git diff --check`; validate exact commit-message files using pinned commitlint before each mutation.
- GitHub CI/Commitlint on final main, existing release workflow, then independent NuGet registration/catalog checks.
