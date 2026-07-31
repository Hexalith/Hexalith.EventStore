---
title: 'Manifest-Driven Release Packaging'
type: 'feature'
created: '2026-07-31'
status: 'in-review'
baseline_revision: 'ef7c81e81a9f9c2beb17ad9b046408302b56250c'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/project-context.md'
warnings:
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** EventStore already packs the 14 manifest entries, but the semantic-release validator trusts filenames and the package-only consumer references every package together. A renamed foreign archive or a missing Gateway dependency edge can therefore escape the direct metadata proof required by FR22.

**Approach:** Make one fail-closed manifest/package contract govern both release and CI validation, inspect embedded NuGet identities and dependencies, and restore each library package independently so package metadata—not sibling direct references or source checkout state—proves consumability.

## Boundaries & Constraints

**Always:** Keep `tools/release-packages.json` as the sole 14-package inventory; pack only root-owned `src/` EventStore projects in Release with `GeneratePackageOnBuild=false` and `UseHexalithProjectReferences=false`; validate actual archive metadata and exact version; run tests per project; preserve package-mode defaults and the shared Builds catalog.

**Block If:** Implementation requires changing the manifest count/IDs, published package identities, or external dependency versions. If an otherwise complete acceptance criterion unexpectedly requires a human-only external action, commit all agent-completable work and finish as `awaiting-operator` with a non-empty `operator_actions` YAML list instead of using `blocked`.

**Never:** Discover packages by solution or filesystem scan; pack or modify a `references/` project; publish to NuGet/GitHub/registry; redesign Story 3.7 shared workflow ownership or Story 3.12 container publication; suppress restore, downgrade, or audit failures.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Valid inventory | 14 unique EventStore IDs mapped to root `src/` projects | Exactly 14 Release/package-mode pack commands | Fail before packing on malformed, duplicate, out-of-scope, missing, or mismatched entries |
| Valid output | 14 archives whose embedded IDs/version match the manifest/release | Validation succeeds and reports the exact inventory/version | Reject missing, extra, duplicate, renamed, foreign, or mixed-version archives |
| Dependency metadata | Packed libraries, including Gateway | Dependencies are package IDs; Gateway declares its four internal EventStore package edges; no checkout path is present | Reject missing required edges or path-like/local source metadata |
| Package-only consumption | One manifest library at a time with only local packages plus NuGet | Restore/build succeeds without project libraries or source checkout state | Fail the specific package consumer and name the unresolved/project-backed edge |

</intent-contract>

## Code Map

- `tools/release-packages.json` -- authoritative 14-entry ID-to-project inventory; Gateway is the metadata-critical entry.
- `tools/pack-release-packages.py:18` -- manifest loader and pack loop; add EventStore ID and resolved root `src/` containment/evaluated identity guards without adding discovery.
- `tools/validate-release-packages.py:13` -- semantic-release prepare validator; replace filename-only trust with embedded `.nuspec` identity, version, dependency, and source-path checks.
- `scripts/validate-nuget-packages.py:18` -- stronger CI archive parser whose identity/version behavior should be reused rather than independently reimplemented.
- `scripts/validate-consumer-package-references.py:121` -- temporary consumer currently references all libraries together; isolate consumers per package and retain project-library rejection.
- `src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj:39` -- four same-repository project edges that must become package dependencies: Admin.Abstractions, Server, Contracts, and ServiceDefaults.
- `.releaserc.json:10` -- prepare delegates to tools scripts and publish uses the EventStore glob; preserve ordering and assert the exact scope.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:73` -- existing inventory/workflow governance suite and `EvaluatedProjectProperty` reuse point.
- `docs/ci.md:64` -- active package-validation contract and reproducible commands.
- `_bmad-output/planning-artifacts/epics.md:1736` -- authoritative FR22 story and four Given/When/Then acceptance clauses; read-only.

## Tasks & Acceptance

**Execution:**
- [x] `tools/release_package_contract.py` -- add the single reusable manifest normalizer and `.nuspec` parser for EventStore scope, root `src/` containment, archive identity/version, dependencies, and source-path rejection.
- [x] `tools/pack-release-packages.py` -- consume the shared contract and fail before packing on duplicate/missing projects, non-packable projects, or evaluated `PackageId` mismatch; keep manifest order and both package-mode flags.
- [x] `tools/validate-release-packages.py` and `scripts/validate-nuget-packages.py` -- consume the shared archive contract for exact files, embedded identities/version, dependencies, Gateway edges, and absence of source paths; retain each CLI contract used by semantic-release/shared CI.
- [x] `scripts/validate-consumer-package-references.py` -- restore/build each library package independently and keep tool-package installation separate so direct sibling references cannot mask metadata defects.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- cover all manifest mappings plus behavioral failure cases for foreign/renamed/mixed output, metadata leakage, Gateway dependency loss, exact package flags, and exact publish glob.
- [x] `docs/ci.md` -- document archive-aware validation and isolated package-only consumption with the supported commands.

**Acceptance Criteria:**
- Given the reviewed manifest, when packing runs, then only its 14 root-owned EventStore projects are packed in Release/package mode and dependent builds cannot emit packages.
- Given package output, when release and CI validators inspect it, then filenames and embedded IDs share the requested version and every missing, extra, renamed, foreign, duplicate, or submodule package fails closed.
- Given semantic-release prepare/publish, when command governance is checked, then packing explicitly selects package mode and publication is exactly scoped to `./nupkgs/Hexalith.EventStore.*.nupkg`.
- Given generated metadata, when each package is inspected and consumed alone, then external Hexalith edges are NuGet dependencies, no local path/project resolution leaks, and Gateway restores with its four required EventStore package dependencies without source checkout state.

## Spec Change Log

## Review Triage Log

### 2026-07-31 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 10: (high 2, medium 5, low 3)
- defer: 0
- reject: 9: (high 0, medium 5, low 4)
- addressed_findings:
  - `[high]` `[patch]` Extended archive validation from Gateway-only IDs to every root-owned manifest project edge and each direct external Hexalith package-mode edge.
  - `[high]` `[patch]` Required every internal manifest dependency to use the exact release version and added Gateway version-drift mutations.
  - `[medium]` `[patch]` Preserved dependency groups and required the complete internal/external contract in every applicable target-framework group.
  - `[medium]` `[patch]` Rejected duplicate and non-canonically-cased dependency/package identities instead of allowing case-folded or set-based matches to hide defects.
  - `[medium]` `[patch]` Scanned parsed XML metadata for project and absolute source paths so XML encoding or entity escaping cannot bypass leak detection.
  - `[medium]` `[patch]` Rejected Windows-rooted archive entries as well as POSIX traversal and project-file entries.
  - `[medium]` `[patch]` Added package-source mapping and safe XML attribute quoting so EventStore consumers resolve manifest packages only from the local release directory.
  - `[low]` `[patch]` Discovered package extensions case-insensitively while continuing to require canonical lowercase archive names.
  - `[low]` `[patch]` Replaced the fixed dry-run test directory with a unique temporary path.
  - `[low]` `[patch]` Removed an unrelated rendered-workflow whitespace change introduced during delegated implementation.

## Design Notes

Use one shared Python contract for manifest normalization and `.nuspec` parsing, while keeping the existing tools/scripts entry points stable for semantic-release and Hexalith.Builds callers. Isolated consumers are the outer proof: each package must restore without another manifest package being directly injected by the fixture.

## Verification

**Commands:**
- `python3 tools/pack-release-packages.py /tmp/eventstore-3-6-dry 999.3.6-ci --dry-run` -- expected: exactly 14 commands, each with both package-safety properties.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false` -- expected: zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests` -- expected: focused class passes.
- `python3 scripts/pack-release-packages.py /tmp/eventstore-3-6-packages 0.0.0-ci-test && python3 scripts/validate-nuget-packages.py /tmp/eventstore-3-6-packages && python3 scripts/validate-consumer-package-references.py /tmp/eventstore-3-6-packages` -- expected: exactly 14 archives validate and every package-only consumer succeeds.
- `git diff --check` -- expected: no whitespace errors.

## Auto Run Result

Status: in-review

Summary: Added a shared fail-closed release-package contract, made both release and CI validators inspect real embedded identities and dependency metadata, and changed consumer validation to restore every manifest library independently with local EventStore source mapping.

Files changed:
- `tools/release_package_contract.py` — normalizes the manifest, evaluates project identities, inspects archives, and enforces exact internal/external dependency contracts.
- `tools/pack-release-packages.py` — validates all manifest projects before issuing package-mode pack commands.
- `tools/validate-release-packages.py` and `scripts/validate-nuget-packages.py` — share the archive-aware contract for semantic-release and CI.
- `scripts/validate-consumer-package-references.py` — restores/builds 13 isolated library consumers and installs the CLI tool independently.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` — covers exact commands, archive identity mutations, metadata leaks, missing dependency edges, and dependency-version drift.
- `docs/ci.md` — documents the manifest, metadata, and isolated-consumer guarantees.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — records Story 3.6 as done.

Review findings: 10 patches applied (high 2, medium 5, low 3); 0 items deferred; 9 defensive-hardening or transient-state findings rejected as outside the story's release contract.

Follow-up review recommendation: `true`; patched counts were high 2, medium 5, low 3, for a weighted score of 18, and high-severity patches were applied.

Verification performed:
- Manifest pack dry-run emitted exactly 14 ordered commands, each with `GeneratePackageOnBuild=false` and `UseHexalithProjectReferences=false`.
- Contracts test project built in Release/package mode with 0 warnings and 0 errors.
- Focused packaging suite passed 55/55 tests with 0 skipped.
- Real package-mode packing produced and validated exactly 14 archives at `999.0.0-ci-test`; all 13 isolated library consumers built with 0 warnings/errors and the CLI tool installed successfully.
- `git diff --check` passed.

Residual risks: Real external publication and its credentialed operator authorization remain intentionally owned by Story 3.12; Story 3.6 has no human-only acceptance action and requires no operator handoff.
