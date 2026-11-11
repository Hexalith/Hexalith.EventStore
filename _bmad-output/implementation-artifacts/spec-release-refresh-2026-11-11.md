---
title: 'Refresh SDK and dependencies, synchronize sources, and prepare EventStore release'
type: 'chore'
created: '2026-11-11'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 1
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

- Isolated follow-up `buildTransitive/Hexalith.EventStore.Logging.targets` and `Directory.Build.targets`: pack conditional provider selection under each package ID, so public consumers receive the same correction as source-catalog builds. All fourteen archive IDs equal their project names, verified against the manifest and built archives.
- Isolated follow-up `scripts/validate-consumer-package-references.py`: Server and DomainService probes compile a separate LoggerMessage partial class through actual restored package targets; existing package isolation and runtime checks remain unchanged.
- Isolated FrontComposer follow-up: four active workflow files select global.json; source-resource lane keeps its legacy 10.0.302 SDK and checks the root 11.0.1xx feature band. Contributor/deployment/troubleshooting prerequisites match root SDK 11 plus runtime 10.

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

- [ ] Integrate the isolated consumer logging compatibility fix and its actual-package regression; apply independent FrontComposer workflow and active-document corrections once one writer owns the shared checkout.

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

2026-11-11 review loop 1: public package consumers must receive the same conditional logging-generator selection as source-catalog builds, and real package consumers must compile a LoggerMessage method. Preserve all original SDK, tooling, central ownership, source-mode and startup-validation fixes. An isolated candidate adds package-specific buildTransitive assets to all 14 archives and extends existing Server/DomainService package consumers. The existing shared checkout is not reverted: concurrent user commits are protected by AGENTS.md, which takes precedence over the workflow's re-derivation rollback instruction.

## Review Triage Log

| Finding | Verdict and evidence | Route |
| --- | --- | --- |
| Blind 1: public consumers miss logging selection | high: a plain net10 ASP.NET consumer with Resilience 11 and Hosting.Abstractions 11 fails CS0757. Independent reproduction confirmed; repository-only import does not reach package consumers. | bad_spec; isolated transitive-assets candidate prepared and verified |
| Blind 2: FrontComposer jobs still install SDK 10 | medium: four active workflows explicitly install only 10.0.403 while global.json requires 11.0.100; quality source-resource guard also rejects the new band. | patch; isolated YAML corrections prepared |
| Blind 3: main CI lacks explicit net10 runtime input | false: both current Ubuntu hosted images include net10. Actual latest CI successfully executes the net10 module CLI, then fails HXW011/HXW009 producer prerequisites, disproving runtime absence. | reject |
| Blind 4: shared domain-release lacks additional runtime input | false: selected hosted runners already provide net10; no supported current invocation without that runtime was demonstrated. Added input forwarding is an optional improvement, not the observed failure. | reject |
| Blind 5: old immutable release preparation pins miss runtime | false: those actions read global.json for SDK 11 and hosted runners supply net10. Changing immutable publication authority is not required by a demonstrated failure. | reject |
| Blind 6: supplementary SDK changes an SDK 8 caller | maybe-false: no such supported caller was identified; SDK 10+ is the repository baseline, and installing a requested SDK never guaranteed selection without global.json. Need a supported real caller and selection contract to establish harm. | reject low unverified hypothetical |
| Blind 7: handwritten analyzer test misses real package imports | medium: the actual public-consumer failure passes the synthetic selector tests. A real package consumer must exercise LoggerMessage with the published dependency closure. | bad_spec; same consumer-compatibility group as Blind 1 |
| Blind 8: no forwarding-only YAML tests | false: no current runtime regression was established for these mappings; mirror tests would add no demonstrated behavioral protection. Real package-consumer coverage addresses the proven missing check. | reject |
| Blind 9: contributor/deployment SDK documentation stale | low: active instructions name 10.0.403 and contradict global.json; users following them cannot select the required SDK. | patch; isolated active-doc corrections prepared |
| Blind 10: registry audit must assert compatibility acceptance | false: registry freshness explicitly supplies no acceptance authority. Consumer regression and failed producer prerequisites mean acceptance is not established; changing the audit claim would be inaccurate. | reject |
| Edge 1: clean hosted runner lacks net10 runtime | false: the selected Ubuntu images contain net10, and latest CI executed the net10 CLI successfully. The exact observed failure is an unqualified Platform/catalog pair. | reject |

The verification-gap reviewer reported no findings. The independent consumer reproduction exposed a gap beyond its initial test tracing.

## Verification

- `dotnet --version` in every changed SDK-owning repository; expected 11.0.100.
- Root restore/build in explicit appropriate dependency mode, tests individually, and existing Python release/governance checks.
- `dotnet tool restore --tool-manifest .config/dotnet-tools.json` and supported CLI version smoke.
- `git diff --check` plus owning staged-file check before commits.
- Exact-source GitHub Actions CI/Commitlint proof followed by protected Release and registry package identity verification.


Final independent validation and current blockers:

- Isolated candidate workspace: `/tmp/eventstore-isolated-validation-ox7ztzb2/eventstore`, source snapshot `15c8ccd525d743f68eed8ff19ba95c6c6bd2e563`; no original shared source files changed by candidate development.
- `scripts/pack-release-packages.py` produced all 14 archives at synthetic version `999.0.0-ci-test`. Both package validators passed. Every archive includes its package-specific buildTransitive target.
- Plain net10 ASP.NET package consumer compiled with zero warnings/errors and executed successfully. Removing only the packaged targets reproduced CS0757; restoring them returned the build to success.
- Existing package-only Server and DomainService consumer probes, extended with LoggerMessage generation, both passed with zero warnings/errors.
- Hosted CI run https://github.com/Hexalith/Hexalith.EventStore/actions/runs/43100233290 at `15c8ccd525d743f68eed8ff19ba95c6c6bd2e563`: Contracts, folded-snapshot, checkpointed packages, source-mode and semantic-release governance passed. Main prepare step failed HXW011: Platform `fc047fe7ede228d7d356772212c478ccbf36bcdc` has no local release tag; HXW009: Platform pins Builds `ff6b295abcca5159896602bd8a5e87e913950b83` while EventStore selects `bc3efac849d4a17c3d00cba2eee0b54771ec524e`; HXW007 also reports missing shallow history for the pinned catalog. Do not fabricate a tag or weaken qualification to hide this dependency blocker.
- Release was not dispatched because exact-source CI is failing. Production also requires reviewer jpiquot and forbids administrator bypass. No new publication is claimed; all 14 prior v4.2.0 archives were independently verified on NuGet.
- Pending user coordination: another Cursor session is committing and overwriting the same shared files. Root/source mutations remain paused while the owner is selected. Independently prepared fixes are reviewable in the isolated workspace and patch artifacts.

Prepared follow-up artifacts: `/tmp/eventstore-isolated-validation-ox7ztzb2/patches/eventstore-compatibility.patch`, `frontcomposer-sdk.patch`, and `builds-latest-catalog.patch`. Root and FrontComposer diff checks, consumer-script syntax check, and all four changed FrontComposer workflow actionlint checks passed. These patches remain unapplied to the original shared sources while writer coordination is pending.


Isolation removes the need to wait for checkout ownership: follow-up code can be committed and pushed from the independent clone, with original user edits preserved. The tested retained Immutable 10.0.15 exception is also necessary for public consumers: building the generator package against Immutable 11 causes CS8784/FileNotFoundException for assembly version 11 in SDK 10.0.404. The final candidate restores the committed catalog's existing 10.0.15 pin and removes the now-unneeded test reference; no Builds source/catalog change is required. Actual ASP.NET framework references are included in Server/DomainService probes; removing packaged logging targets makes the real consumer lane fail CS0757, and restored targets make both probes pass. A strict SDK 10.0.403 latestPatch generator-consumer build is added to preserve that compiler compatibility.


Follow-up review triage (isolated consumer fix):

| Finding | Verdict and evidence | Disposition |
| --- | --- | --- |
| Final blind 1: producer Immutable correction absent | false: the selected committed Builds tree `bc3efac849d4a17c3d00cba2eee0b54771ec524e` already pins 10.0.15. The isolated catalog is byte-identical to that tree. The original checkout's separate uncommitted 11 pin is not selected by the candidate commit. | Preserve committed compatible pin; retain the external dirty edit separately. |
| Final blind 2: compatibility SDK undocumented | low: full consumer validation now requires a 10.0.4xx SDK as well as repository SDK 11; contributor and CI validation instructions did not state that distinction. | Add the compatibility SDK prerequisite to CONTRIBUTING.md and docs/ci.md. Hosted CI already provides/provisions it. |
| Final blind 3: empty generator probe can pass missing analyzer | medium, pre-existing weakness exposed by the new check: an empty consumer cannot prove generation. | Add an actual RestApi assembly attribute from the Contracts package and reference/run the generated manifest under both compiler hosts. |
| Final blind 4: packaged logger guard coverage | low: shared selector tests already cover both guards, but actual package probes covered only replacement-present cases. | Add an ASP.NET Client consumer LoggerMessage probe without the telemetry replacement, complementing Server/DomainService and the existing guard tests. |
| Final blind 5: troubleshooting omits runtime installation | low: the diagnosis names runtime absence while the numbered steps installed only SDK 11. | Add ASP.NET runtime 10 installation and both-runtime verification. |
| Final verification 1: FrontComposer governance asserts old SDK | medium: old governance assertions would reject the new SDK 11 guard. | Upstream `f74bf648c0fca1da0cc5ddc0440cdb5993e81c4d` already repairs every affected workflow, runtime provisioning and assertions; consume that preserved remote commit instead of duplicating/reverting its policy. |

The final edge-case reviewer reported no findings. All follow-up changes remain within package-consumer compatibility, SDK validation and active prerequisites.


Integrated follow-up status: the isolated clone consumes upstream runtime fixes through EventStore `4c3de433fbfa8a5634d100b19f9a0140a70d0fee` and FrontComposer `f74bf648c0fca1da0cc5ddc0440cdb5993e81c4d`. The latter already fixes the reviewed SDK workflows and blocking governance assertions, so no duplicate FrontComposer commit is needed. The generated consumer now imports the real RestApi attribute from Contracts and references/runs its emitted manifest. Client probes additionally cover the no-telemetry-replacement logging case. Advanced compatibility SDK prerequisites and runtime installation troubleshooting are documented. All fourteen synthetic archives validate; actual ASP.NET Server/DomainService/Client consumers and generated-manifest consumers pass on the appropriate compiler hosts. Full generator test-project Release build passes with zero warnings/errors using the committed Immutable 10.0.15 pin.
