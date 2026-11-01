---
title: 'Refresh dependencies and verify EventStore release readiness'
type: 'chore'
created: '2026-11-01'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '4b3e4a087232d6806b1e4e4610b8344c2b64d767'
context:
  - 'AGENTS.md'
  - 'docs/ci.md'
---

<frozen-after-approval reason="User authorizes dependency maintenance, Git synchronization and publication without intermediate approval">

## Intent

**Problem:** The EventStore checkout has stale root-submodule references and published dependency pins. The requested new release must contain synchronized source, use the latest stable supported SDK and packages, and have successful source-specific CI evidence.

**Approach:** Advance clean root-declared submodules to their fetched main tips, audit official package registries, apply justified dependency updates and compatibility repairs, then verify and push. Use the configured Release workflow and verify every declared NuGet package if publication proceeds.

## Boundaries & Constraints

**Always:** Preserve existing work, repository boundaries, central package ownership, all security and publication checks, immutable evidence and published versions. Use stable versions where the current dependency is stable; retain explicit prerelease integrations and documented compatibility pins. Validate exact commit messages with the owning pinned commitlint. Keep verification logs in temporary storage. Report SDK already current when official metadata confirms it. Git and remote publication are parent-owned.

**Never:** Initialize or update nested submodules, load skills from references, weaken tests or warnings to conceal failures, modify production approval protection or impersonate the required reviewer, rewrite released tags, publish an incomplete package set, or claim publication without registry evidence. Unrelated runtime redesign and ecosystem-wide migrations are outside this release refresh.

</frozen-after-approval>

## Code Map

- `global.json`: pins SDK 10.0.403, confirmed current by official .NET 10 release metadata; Microsoft.Testing.Platform runner stays intact.
- `.gitmodules`: nine root submodules; clean main checkouts can fast-forward to origin/main without nested initialization. Parent records updated gitlinks after checks.
- `Directory.Packages.props` and `Directory.Build.props`: import `references/Hexalith.Builds/Props/Directory.Packages.props`; keep the source-owned catalog authoritative instead of introducing local overrides.
- Latest Builds catalog: EventStore 3.130.1, Tenants 6.0.0, FrontComposer 4.7.4, Memories 3.0.13, Projects 1.2.1. Central source updates belong to the Builds repository if further registry audit requires them.
- `package.json`, `package-lock.json`, `.config/dotnet-tools.json`, `scripts/payload-protection/requirements.txt`: root-owned Node, CLI and Python pins; inspect official latest versions and update stale pins coherently.
- `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`: Aspire SDK 13.6.4; source-mode Tenants compatibility is checked by the dedicated AppHost test lane.
- `.github/workflows/ci.yml`, `.github/workflows/integration.yml`: authoritative build, deterministic tests, source-reference and live lanes. Diagnose current failures against exact source before changing code.
- `.github/workflows/release.yml`, `.releaserc.json`, `tools/release-packages.json`: manual current-main dispatch; exact successful CI and Commitlint push proofs; production reviewer protection; fourteen package inventory and pinned publisher.
- Existing packaging fixtures and `tools/tests/test_release_package_contract.py`: verify package-mode restore and release manifest dependencies.

## Tasks & Acceptance

**Execution:**

- [x] Parent synchronizes all clean root-declared submodule main branches to fetched remote tips, preserving nested checkouts and recording exact revisions.
- [x] Audit SDK and effective direct NuGet/npm/Python/tool dependencies against official registries; update only stale applicable pins and corresponding lockfiles.
- [x] Repair demonstrated build/test failures caused by the refreshed dependencies without reducing test coverage or changing security policy.
- [x] Run relevant package, tooling, source-mode and build checks; inspect complete diff and automatically review changes.
- [ ] Parent validates commit messages, commits and pushes approved changes, and observes exact-source GitHub CI workflows.
- [ ] Parent dispatches Release after successful required checks. If production approval blocks it, retain the pending run and report the exact reviewer requirement.
- [ ] Parent verifies release/tag source identity and all fourteen NuGet packages when the configured workflow publishes; otherwise explicitly reports publication pending.

**Acceptance Criteria:**

- Given the official latest stable SDK, when compared with the root pin, then it matches or the repository and relevant documented prerequisites are updated together.
- Given effective direct dependencies, when the registry audit completes, then each is current or has a specific retained compatibility constraint supported by repository evidence.
- Given synchronized root submodules, when Git state is inspected, then each is clean and matches its selected fetched main tip, without newly initialized nested submodules.
- Given the final source, when configured blocking CI and Commitlint run, then they succeed before release dispatch.
- Given an approved configured release, when all manifest IDs are downloaded from NuGet, then package ID, version and repository commit match the release source. A pending approval is reported as incomplete publication.

## Implementation Notes

User authorization satisfies interactive planning checkpoints. Root handles Git synchronization and remote actions. The implementation handoff handles root-owned dependency edits and demonstrated compatibility repairs only; no commits, pushes, workflow dispatch or approval actions. Exact SDK registry metadata is https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json. Production currently requires reviewer jpiquot and disables admin bypass.

## Spec Change Log

## Review Triage Log

| Finding | Verdict and evidence | Disposition |
| --- | --- | --- |
| Builds exception exists only in dirty checkout | medium: a clean checkout needs the new committed Builds revision. Parent will commit/push the exception before recording the root gitlink. | patch, parent Git action |
| Audit prose lacks reproducible output | low: this requests editing this build spec; registry checks were executed directly by the audit agent. Relevant exact registry interfaces and versions are recorded. | reject spec-only request |
| Package consumer probes skipped without inventory | medium: the local Packaging run lacks package inputs. The existing mandatory process-packages CI job packs all fourteen manifest projects and runs the three representative package-consumer checks. The two inventory-conditioned probes require a separate explicit inventory run; parent will supply it before readiness is claimed. | existing required CI verification |
| Staged-file guard checked an empty index | medium: implementation intentionally had no staged content. Parent must run the guard after staging its exact final changes. | patch, parent Git action |
| Stale Dapr SDK documentation and executable-pin rationale | low: docs/ci.md central NuGet version is stale and will be corrected. Runtime/CLI tuple pins are separately qualified integration infrastructure, not direct package versions, and remain unchanged. | patch documentation |
| Manifest-installed CLI lacks permanent CI smoke | low, pre-existing: source CLI tests do not restore local tool manifests. This exact immutable package was restored and its installed executable successfully reported 3.130.1. | defer ongoing installation coverage |
| Edge case review | no findings | complete |

| Final review: Tenants still dirty at old gitlink | medium: carried Git reproducibility requirement; parent commits and pushes Tenants before staging its root pointer. | parent Git action |
| Final review: legacy/source compilation pending | medium: compile proof is required; legacy 3.128.4 Release build has now passed with zero warnings/errors and source build is in progress. | parent verification |
| Final review: catalog wrong-root/cancellation gap | medium, pre-existing: readiness block existed at incoming Tenants HEAD; the repair preserves it under the existing qualified-SDK capability symbol. Existing happy-cohort integration covers matching readiness but direct negative-path coverage is absent. | defer in owning Tenants ledger |
| Final review: temporary-only registry report | low: preserve a concise sanitized durable report with identities, sources and hashes. | patch documentation |
| Final review: inventory probes still pending in notes | low: both probes have now passed with the actual fourteen-package inventory, zero skips; update observed result before completion. | resolved by verification |
| Final review: deferred CLI follow-up classification | low: the newly added entry needs its own grouping and canonical ownership/review metadata. | patch documentation |
| Final verification review: wrong-root catalog readiness | medium, pre-existing: same gap filed with explicit defer disposition; preserve matching-root guard unchanged and record follow-up in Tenants. | defer, same owning follow-up |
| Final edge case review | no findings | complete |

## Verification

- Official registry audit and effective restore metadata, retaining a concise report of current versions and exceptions.
- `git diff --check` and `bash scripts/check-committed-files.sh --staged`.
- Relevant focused .NET builds/tests individually, with package/source mode selected explicitly; Python release package contract and Node semantic-release governance regression.
- Parent checks exact-source GitHub CI, Commitlint and other configured lanes, followed by Release workflow and NuGet flat-container package identity if authorized workflow completes.

Registry audit: 77 direct NuGet references, SDK and tool identities were checked against the latest central catalog. All are current except the root Admin CLI tool manifest, which must advance from 3.128.4 to 3.130.1. All eight direct npm declarations and resolved versions, cryptography 50.0.2, Aspire SDK 13.6.4 and defaultdocumentation.console 1.2.6 are already current. Implementation handoff: edit the root tool manifest to 3.130.1, restore that tool and run existing relevant packaging/governance checks, preserving all other pins. The parent updates the matching Builds exact-manifest exception, manages root-submodule gitlinks, and handles any broader solution/CI failure. Do not edit Builds or other submodules in the handoff. The AppHost baseline was started, inspected and stopped successfully before dependency edits.

Implementation handoff verification (2026-11-01):

- Updated only the root Admin CLI tool pin from `3.128.4` to `3.130.1`; the manifest now also has the final newline required by `.editorconfig`.
- `dotnet tool restore --tool-manifest .config/dotnet-tools.json` passed for both declared tools. `dotnet tool run eventstore-admin -- --version` reported `3.130.1+1de42b706f52af0da6513f0b7663002606d17156`.
- `python3 -m unittest discover -s tools/tests -p 'test_release_package_contract.py'` passed: 2 tests, zero failures.
- `node tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/semantic-release-github-success.mjs` passed on Node `v26.4.0`: 2 isolated histories using locked `@semantic-release/github 12.0.10`, 6 selected-origin requests, and 2 blocked external probes.
- `git diff --check` and `bash scripts/check-committed-files.sh --staged` passed; the latter checked zero staged files because the handoff does not stage changes.
- `dotnet restore tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -p:UseNuGetDeps=false -m:1 -v:q` passed. The subsequent `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Debug -p:UseNuGetDeps=false -m:1 --no-restore -v:q` exited 1 with `MSB3883` and an `IOException` copying `src/Hexalith.EventStore.Server/obj/Debug/net10.0/ref/Hexalith.EventStore.Server.dll` while the parent AppHost build was writing shared outputs. Logs remain in `/tmp/eventstore-refresh-contracts-restore.log` and `/tmp/eventstore-refresh-contracts-build.log`. The parent owns a sequential retry and the remaining Packaging tests; no build or test policy was changed.

Parent source-mode AppHost.Tests build completed with zero warnings and errors. An overlapping Contracts build encountered an output-copy conflict; the parent reruns affected builds sequentially. The shared Builds exception inventory validates 16 allowlisted entries after aligning the CLI to 3.130.1.

Verification update: source-mode AppHost.Tests build passed with zero warnings/errors and all 10 Tenants topology guardrail tests passed. Sequential Contracts rebuild also passed with zero warnings/errors. Local Packaging tests remain running; two inventory-required tests skip by design without EVENTSTORE_PACKAGE_CONTRACT_DIR. Exact-source mandatory CI packs and validates representative consumers; parent also supplies the inventory explicitly for the two package-required probes.

Expanded CI repair: the incoming Tenants head had concrete frozen-host compile errors (catalog API absent in 3.128.4), unconfigured async-disposal analyzer failure, obsolete qualification-branch triggers, two stale mutable-workflow assertions, and a missing story gitlink declaration. The owning repair record is references/Hexalith.Tenants/_bmad-output/implementation-artifacts/spec-ci-repair-2026-11-01.md. Its implementation preserves catalog checks for all supported source/package cohorts and the exact immutable reusable workflow, and changes no submodule pointers. Python bootstrap/story regressions (8/77/101), both actual story-pointer audits, five workflow actionlint checks and diff checks passed. Remaining .NET compilation and hosted CI are parent-owned.

Dependency audit is reproducible with python3 /tmp/eventstore-package-registry-audit-2026-11-01.py > /tmp/eventstore-package-registry-audit-2026-11-01.json. It records catalog/source hashes and 77 NuGet identities: 74 latest stable and 3 retained latest prereleases. Aspire Keycloak/Kubernetes use 13.6.4-preview.1.26523.11; CommunityToolkit Dapr uses 13.6.2-beta.959 (the 13.0.0 stable conditional is Folders-only). All eight npm resolutions and cryptography 50.0.2 are current. SDK 10.0.403 is current stable. Dapr executable runtime/CLI pins remain exact qualified test infrastructure, separate from NuGet package freshness.

Local root Packaging suite: 355 discovered, 353 passed, 2 inventory-conditioned skips, zero failures. Parent is packing all fourteen projects and supplying that inventory for the two skipped probes. Builds exact-manifest exception validator passed 30 scenarios; inventory schema passed 16 entries. Builds correction was committed and pushed as cf79d3c5ca5fc42424ec9e827e38ff63da70da59 after rebasing onto concurrent upstream transient-upload retry fix 28f9c88d9b0b5eed06ff88a3e44d6162a8f3fb02.

Independent FrontComposer CI remains blocked by its sealed Platform source projection owner mismatch (75bceb22abc60d22d0c4e6f3d7c86795f3e51cc1 vs committed 03a099bba258c37a8f16c8b2e842ae842b1ad6d6). The owning Story 15.5 record explicitly requires owner-reviewed precursor activation and preserves expected old-base refusal. No projection seal, trust, workflow or authority is changed here. The main source/package release checks remain authoritative for EventStore. Production release review is also preserved.

Parent package proof completed: all fourteen Release packages packed and passed tools/validate-release-packages.py at 999.0.0-refresh-test. Both previously skipped inventory-conditioned Contracts probes passed with EVENTSTORE_PACKAGE_CONTRACT_DIR supplied: total 2, failures 0, skips 0. Legacy Tenants 3.128.4 package profile compiled Release with warnings as errors, zero warnings/errors. Source integration and targeted Contracts builds remain in progress.

Final review patches retained a concise registry report at docs/implementation/dependency-refresh-2026-11-01.json and classified the new CLI follow-up under its own dated ledger grouping; JSON, deferred-work diagnostics for that entry and diff checks pass. Tenants source IntegrationTests and Contracts builds passed with zero warnings/errors. Targeted Contracts first run: 394 cases, 393 passed, one additional stale @main predicate at PackageGovernanceTests.cs:1324; it is being corrected to require the exact approved domain-ci workflow path and SHA, preserving all third-party action constraints. No live or production readiness claim is inferred from these local compile and contract results.

Tenants final local verification is complete: legacy Release and current source IntegrationTests/Contracts Debug builds passed with zero warnings/errors. Of 394 focused Contracts cases, 393 passed on the first run; the remaining immutable-reference assertion was repaired, rebuilt and passed in its focused 1/1 rerun. Both package inventory probes passed with no skips. The reviewed Tenants repair is committed for the parent gitlink; fresh hosted CI remains required. FrontComposer's independent sealed-projection activation and production release reviewer controls remain outstanding external approval boundaries, so neither all ecosystem CI nor publication is claimed complete.

Hosted follow-up on Tenants 00e7f380aadcef4dac7a04d139c6edaca2f1ff5b: the main build/unit job, published catalog HTTP, blocking HTTP contracts, Story Guards, Commitlint and CodeQL passed; source qualification exposed two later-executed fixture/build identity failures. The isolated source TestServer now registers the actual SDK GeneratedRouteCatalogFilter alongside its committed test authority, and the actual owner-profile workflow exports its measured EventStore producer version just as the projection job does. No runtime policy, dependency pin or refusal assertion was changed. Regression: original four provenance cases pass4/4; all in-process denial/authorization cases pass. Optional whole-source class60/66 retains six independent process-profile cases that the configured full-class workflow runs in package mode, whose hosted check passed; this limitation is recorded in Tenants. Producer3.128.4 source build/deps/ref identity checks, workflow governance68/68 plus1/1, provisioning2/2, actionlint, story-pointer and diff checks pass. Parent consumes the committed owning repair before rerunning exact final-source CI.
