---
title: 'Refresh SDK and dependencies, synchronize sources, and prepare EventStore release'
type: 'chore'
created: '2026-11-11'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '06ab5c16d1dc38e5e3f29ff06824b2c6724e6ea3'
context:
  - 'AGENTS.md'
  - 'docs/ci.md'
---

<frozen-after-approval reason="User explicitly authorizes updates, fixes, pushes and release without intermediate human validation">

## Intent

**Problem:** EventStore and its root dependencies have stale SDK and package pins. The user requests synchronized repositories, repaired CI, a release, and verified NuGet publication.

**Approach:** Audit official registries, update stable compatible dependencies and SDK pins, preserve existing work, validate and push each owning repository before recording parent gitlinks. Run exact-source CI and the configured release workflow; verify registry publication if the protected workflow proceeds.

## Boundaries & Constraints

**Always:** Use latest stable SDK 11.0.100 confirmed by Microsoft release metadata. Preserve .NET 10 target frameworks and retain documented compatibility constraints where newest packages require .NET 11 or break supported APIs. Preserve all existing verification, release gates and immutable publisher pins. Load owning repository guidance before edits. Use only root-declared submodules and keep central package ownership in Builds. Parent owns every Git commit/push, synchronization, workflow dispatch and registry verification. Automated review is authorized. User authorization satisfies interactive skill checkpoints.

**Never:** Initialize nested submodules, load skills from references, weaken checks, change production protection, impersonate reviewers, overwrite concurrent work, rewrite release tags, or claim a new publication without NuGet evidence. Do not expand into framework migration, runtime redesign, dependency updates in nested submodules, or unrelated UI changes.

</frozen-after-approval>

## Code Map

- `global.json` and root-declared `references/*/global.json`: SDK selection only; retain test runner and roll-forward policy. Builds and Tenants already use 11.0.100; others may need an SDK bump. Do not edit nested checkouts.
- `Directory.Packages.props` and `Directory.Build.props`: import the central Builds catalog; do not add local package overrides.
- `references/Hexalith.Builds/Props/Directory.Packages.props`: authoritative package versions. Audited compatible deltas: Microsoft.NET.Test.Sdk 18.11.1 to 18.12.0; OTel instrumentation AspNetCore/Http 1.20.2 to 1.21.0 and Runtime 1.20.1 to 1.21.0; HexalithTenantsVersion 7.0.6 to 7.0.7. Also update compatible Microsoft.Extensions.* and System.* packages to stable 11.0.0 where net10 assets are available; move shared Configuration/DI/Logging/Diagnostics/Options pins as one coherent closure. Retain ASP.NET JwtBearer, Components, Mvc.Testing, OpenApi, SignalR.StackExchangeRedis and TestHost on 10.0.15 because their newest versions require net11. Apply compatible Authorization, DataProtection and SignalR.Client/Common 11 assets; retain Microsoft.OpenApi on 2.13.0. Verify framework compatibility and repair any resulting legitimate compilation errors.
- `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`: Aspire SDK 13.6.4 mismatches catalog/latest 13.6.5; align it. Preserve orchestration model. Parent starts/inspects/stops baseline before edits.
- `.config/dotnet-tools.json`: update Admin CLI 4.1.0 to 4.2.0 and Builds Module CLI 4.47.0 to 4.48.0. Check Builds exact tool-manifest allowance if needed and update only the matching EventStore allowance for the new manifest bytes.
- `package.json`/`package-lock.json`, `scripts/payload-protection/requirements.txt`: audited npm declarations already current; verify Python and remaining tool registry facts before changing.
- `docs/getting-started/prerequisites.md`, active root `README.md` prerequisites: align SDK instructions with global.json, keep .NET 10 runtime requirement explicit. Historical evidence stays immutable.
- `.github/workflows/ci.yml`: current reusable workflow fb027a87afcdf7661c8f80ade89231b7730b60d1 fixed previous runner integration failure; no speculative CI edits.
- `.github/workflows/release.yml`, `.releaserc.json`, `tools/release-packages.json`: explicit dispatch, exact CI/commitlint proof, protected production environment and fourteen-package inventory. Parent handles publication after implementation.

## Tasks & Acceptance

**Execution:**

- [ ] Audit and update root-owned SDK, AppHost and tool pins plus root-declared submodule SDK pins; keep corresponding active prerequisites accurate.
- [ ] Update central compatible package versions in Builds and any exact matching tool-manifest allowance required by its existing policy.
- [ ] Run SDK selection in changed repositories; restore/build EventStore and individual relevant tests, package/governance Python checks and tool restore. Diagnose actual failures without weakening gates; record exact commands/results and blockers in implementation notes.
- [ ] Parent reviews the complete diff, validates exact commit messages with each owning pinned CLI, pushes submodule commits and verifies remote presence before committing root pointers.
- [ ] Parent observes final exact-source required CI/commitlint and other failures, dispatches configured Release when green, and checks every manifest NuGet package if publication completes. Report protected approval as an external blocker.

**Acceptance Criteria:**

- Given Microsoft registry metadata, when updated SDK pins resolve, then each selected SDK is 11.0.100 and existing net10 targets still compile.
- Given official dependency metadata, when the audit finishes, then supported direct versions are latest stable compatible or the retained compatibility exception is recorded.
- Given final Git state, when checked, then intended source is preserved, owning changes are pushed, recorded root gitlinks exist remotely, and no nested submodule was initialized.
- Given exact-source CI, when a release is dispatched, then required checks have passed and protected approval remains enforced.
- Given completed publication, when all fourteen manifest IDs are downloaded, then package versions and repository commit match the release source; pending approval is reported as unpublished new work.

## Implementation Notes

Current handoff (2026-11-11): local source edits complete; final integrated verification and publication remain pending. Another Cursor session committed intermediate shared edits and subsequently overwrote the tested Immutable 11 pin back to 10.0.15 while a build was running. Parent requested writer coordination and paused shared writes to preserve user changes. Earlier source build passed with zero warnings/errors. Client 2394 cases (2 existing skips), ServiceDefaults 157, generators 191, startup options 30, updated exact tool assertion 1 passed. AppHost must rerun after compilation stops because overlapping builds caused loaded-module digest mismatches; final source and other long suites still require collection. Root remains unpushed by this session. GitHub has not allocated jobs for queued CI. New release and NuGet publication are not claimed.

Parent verification before implementation completion (2026-11-11):

- Started the explicit AppHost with Aspire CLI, inspected resource state, and stopped it successfully before edits.
- Python release package contract 2/2, source branch 4/4, publication authority 10/10, release candidate 19/19 passed; semantic-release GitHub lifecycle fixture passed both isolated histories.
- Downloaded all 14 existing v4.2.0 NuGet archives and verified IDs, versions and repository commit `aa16227bd07f0564a3026d619a5abd973e02656f`; this verifies the prior release, not publication of this refresh.
- Official Ubuntu 24.04 and 26.04 runner image inventories both include SDK 10.0.403, supplying the retained net10 runtime for immutable shared CI. Root direct setup-dotnet steps also explicitly install 10.0.x alongside global.json.
- Exact draft commit messages passed each owning pinned commitlint CLI 21.2.4; preflight logs and message files are in temporary storage.

Parent implementation dispatch owns local dependency/configuration edits and verification only. No staging, commits, pushes, remote mutation or workflow actions in implementation subagent. Parent will complete remote tasks after review. Production requires reviewer jpiquot and disables administrator bypass.

## Spec Change Log

## Review Triage Log

## Verification

- `dotnet --version` in every changed SDK-owning repository; expected 11.0.100.
- Root restore/build in explicit appropriate dependency mode, tests individually, and existing Python release/governance checks.
- `dotnet tool restore --tool-manifest .config/dotnet-tools.json` and supported CLI version smoke.
- `git diff --check` plus owning staged-file check before commits.
- Exact-source GitHub Actions CI/Commitlint proof followed by protected Release and registry package identity verification.
