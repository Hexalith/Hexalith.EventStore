---
title: 'Manifest-Driven Release Packaging'
type: 'feature'
created: '2026-07-31'
status: 'done'
baseline_revision: 'ef7c81e81a9f9c2beb17ad9b046408302b56250c'
final_revision: '13ccd4fdf6f3b9cc5f85c6747ca62566dd45204f'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/project-context.md'
warnings:
  - oversized
deferred:
  - summary: >-
      Semantic-release uploads GitHub Release assets with the unscoped glob
      `nupkgs/*.nupkg`, so the exact-scope guarantee AC3 pins on the NuGet push
      command has no equivalent on the second publication channel.
    evidence: |-
      `.releaserc.json:12` publishes to NuGet with
      `dotnet nuget push "./nupkgs/Hexalith.EventStore.*.nupkg"`, and the publish-governance
      test now asserts that glob appears exactly once and that the unscoped form is absent.
      `.releaserc.json:18` still declares `"assets": ["nupkgs/*.nupkg"]` for the
      `@semantic-release/github` plugin; that line is untouched by this story's diff and
      uncovered by the new exact-scope assertion. The live risk is mitigated rather than
      eliminated: `tools/validate-release-packages.py` runs in `prepareCmd` before publish
      and fails closed on any archive outside the 14-entry manifest, so on a successful
      release the unscoped glob can only match manifest packages.
    location: >-
      .releaserc.json:18
    severity: medium
  - summary: >-
      The prior pass's deferred-work ledger entry cites a test name that does not
      exist, so a maintainer following its evidence finds nothing.
    evidence: |-
      The 2026-07-31 ledger entry for the unscoped GitHub asset glob attributes the
      exact-scope assertion to `ReleasePackageManifestTests.Semantic_release_publish_command_pushes_scoped_packages`.
      No such test exists; the assertion lives in
      `Semantic_release_delegates_package_inventory_to_manifest_scripts`. The finding the
      entry records is real and unchanged — only its pointer is wrong. Recorded as new
      evidence rather than an edit, because the orchestrator owns existing ledger entries.
    location: >-
      _bmad-output/implementation-artifacts/deferred-work.md
    severity: low
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

### 2026-07-31 — Review pass (follow-up 2)
- intent_gap: 0
- bad_spec: 0
- patch: 10: (high 1, medium 4, low 5)
- defer: 1: (high 0, medium 0, low 1)
- reject: 13: (high 0, medium 4, low 9)
- addressed_findings:
  - `[high]` `[patch]` The dependency contract could be switched off by the archive it was judging: both `_validate_internal_dependencies` and `_validate_external_hexalith_dependencies` waived their proof whenever the nuspec declared `<packageType name="DotnetTool"/>`, and the consumer script routed on the same self-declared value. Proved by construction that a Gateway archive declaring `DotnetTool` with **zero** dependency edges was accepted by the full validator — the exact defect AC4 exists to prevent — and would then be `dotnet tool install`ed instead of restore/build proven. Waivers are now keyed on the manifest-owned `TOOL_PACKAGE_IDS`; a non-tool package declaring `DotnetTool`, or a tool package omitting it, fails closed, and the constant is guarded against naming non-manifest packages. Pinned with `tool-type-forgery`/`tool-type-missing` rows.
  - `[medium]` `[patch]` The source-path leak pattern only matched rooted or traversal paths, so the unrooted build-output shapes a real pack leak carries (`bin/`, `obj/`, `artifacts/`), plus `~/` and UNC roots, were accepted in both attribute and element position. Extended the alternation and re-verified by replaying the new pattern over 2,261 real cached nuspecs — 0 false positives. Added an `output-path-metadata-leak` row and made the diagnostic name the matched text.
  - `[medium]` `[patch]` The leak scan serialized only the `<metadata>` subtree, so a checkout path in a sibling element such as `<files>` was never examined. Scans the whole nuspec document now; pinned with a `sibling-metadata-leak` row.
  - `[medium]` `[patch]` AC1's exclusivity was proven in one direction only (manifest ⇒ packable); a new packable `src/` project omitted from the manifest was invisible to every check. Added `Non_manifest_src_projects_cannot_produce_release_packages`, which asserts each of the five non-manifest `src/` projects explicitly opts out of the `Directory.Build.props` `IsPackable=true` default, with a non-empty-complement control.
  - `[medium]` `[patch]` The matrix's "fail before packing on malformed, duplicate, out-of-scope, missing, or mismatched entries" had zero coverage — `load_release_manifest`'s injectable parameters had no caller. Added ten parameterized rejection rows plus an accepting control, driven through a sandbox repository root.
  - `[low]` `[patch]` `docs/ci.md` described the Gateway guard as an archive guard (it compares the derived project graph) and described the dependency waiver as applying to `DotnetTool` packages rather than to manifest-listed tool packages. Corrected both, and documented the build-output leak shapes.
  - `[low]` `[patch]` Nothing asserted that `prepareCmd` packs before it validates, even though the deferred asset-glob finding's entire stated mitigation rests on that ordering. Added the index assertion, mirroring the existing secrets-before-push one.
  - `[low]` `[patch]` The 14-project MSBuild sweep had a per-project timeout but no whole-inventory budget, so semantic-release `prepare` could stall for fourteen times that budget. Added a shared deadline that also shortens each remaining per-project timeout.
  - `[low]` `[patch]` The tool consumer installed through the shared global packages folder while every library consumer used a private one, so a cached archive at the fixed CI version could satisfy the only tool proof without the archive under test being read. Redirected `NUGET_PACKAGES` for the tool install.
  - `[low]` `[patch]` `_validate_archive_paths` accepted drive-relative entries such as `C:leak.txt`, which are absolute under neither path flavour, and its Windows-rooted and backslash clauses had no mutation row. Added the rejection and an `archive-drive-relative-entry` row.

### 2026-07-31 — Review pass (follow-up)
- intent_gap: 0
- bad_spec: 0
- patch: 11: (high 1, medium 5, low 5)
- defer: 1: (high 0, medium 1, low 0)
- reject: 9: (high 0, medium 3, low 6)
- addressed_findings:
  - `[high]` `[patch]` Source-path leak detection could never match element text: the boundary class omitted `>`, which serialized XML always places before element content, so checkout paths in `<description>`, `<icon>`, and `<projectUrl>` were accepted. Added `>` to the boundary class and pinned it with `element-metadata-leak` mutation rows; re-verified against 873 real published archives with zero false positives.
  - `[medium]` `[patch]` The duplicate-dependency guard was inoperative: `seen_group_ids` was re-initialized inside the per-child loop, so ungrouped `<dependency>` children and repeated same-framework `<group>` elements each got a fresh set. Accumulated per target framework instead and added `duplicate-dependency`/`ungrouped-duplicate-dependency` rows.
  - `[medium]` `[patch]` Every archive fixture built a namespace-less nuspec while all real `dotnet pack` output is namespaced, so the only production parse path was untested. Fixtures now emit the real `nuspec.xsd` namespace, moving all mutation rows onto the production branch.
  - `[medium]` `[patch]` `_validate_archive_paths` had zero coverage — fixtures wrote a single `.nuspec` entry. Added `archive-project-entry` and `archive-traversal-entry` mutations that prove project-file and traversal entries fail closed.
  - `[medium]` `[patch]` External Hexalith dependency loss had no negative fixture; every dependency mutation dropped an internal edge. Added `external-dependency-loss` rows covering the `Hexalith.Commons.UniqueIds` edge.
  - `[medium]` `[patch]` Only the internal dependency contract exempted `DotnetTool` packages, so any future external `Hexalith.*` reference on `Admin.Cli` would demand nuspec metadata a tool package cannot emit. Added the matching exemption and documented it.
  - `[low]` `[patch]` MSBuild project evaluation ran without a subprocess timeout, letting a hung `dotnet msbuild` stall semantic-release `prepare` indefinitely. Added a bounded timeout with a diagnostic.
  - `[low]` `[patch]` The Gateway four-edge guard indexed the dependency map directly, raising a bare `KeyError` instead of its diagnostic if Gateway ever left the manifest.
  - `[low]` `[patch]` The isolated consumer's package-source mapping matched only `Hexalith.EventStore.*`, so the bare `Hexalith.EventStore` id the scope predicate admits would have resolved from nuget.org instead of the local release directory.
  - `[low]` `[patch]` A dead `MANIFEST` constant was the sole reason the manifest-backed governance assertion passed for the consumer script, so removing unused code would have red-lined an unrelated test. Removed the constant and re-anchored the assertion on the shared contract, extending the contract's own hygiene checks.
  - `[low]` `[patch]` The packer dry-run test reused a 60s single-property budget for a 14-project MSBuild sweep, and the dry run's new .NET SDK prerequisite was undocumented. Added a dedicated whole-inventory budget plus argparse and `docs/ci.md` notes.

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

Status: done

Summary: Story 3.6 delivers a shared fail-closed release-package contract that governs both semantic-release and CI validation from embedded NuGet metadata, plus per-package isolated consumers that prove consumability from package metadata alone. This second follow-up review pass closed a fail-open in that contract — an archive could waive its own dependency proof by declaring itself a .NET tool — tightened source-path leak detection to the unrooted build-output shapes, and gave the manifest's own fail-closed branches and AC1's exclusivity claim their first coverage.

Files changed (this pass):
- `tools/release_package_contract.py` — bound the tool-package dependency waiver to the manifest-owned `TOOL_PACKAGE_IDS` instead of the archive's self-declared `<packageTypes>`, added the bidirectional package-type contract and a guard against the constant naming non-manifest packages, extended the leak pattern to `bin/`, `obj/`, `artifacts/`, `~/` and UNC roots, scanned the whole nuspec document rather than only `<metadata>`, named the matched text in the leak diagnostic, rejected drive-relative archive entries, and added a whole-inventory MSBuild evaluation budget.
- `scripts/validate-consumer-package-references.py` — routed library and tool consumers by `TOOL_PACKAGE_IDS` rather than archive metadata, and isolated the tool install's package folder.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` — added `tool-type-forgery`, `tool-type-missing`, `output-path-metadata-leak`, `sibling-metadata-leak` and `archive-drive-relative-entry` mutation rows; added eleven manifest fail-closed cases plus an accepting control; added the non-manifest `src/` packability assertion; pinned pack-before-validate ordering in `prepareCmd`.
- `docs/ci.md` — corrected the Gateway guard and tool-waiver descriptions, and documented the build-output leak shapes.
- `_bmad-output/implementation-artifacts/deferred-work.md` — recorded new evidence against the prior pass's asset-glob entry (wrong test name), as a new entry.

Review findings: 10 patches applied (high 1, medium 4, low 5); 1 item deferred (low); 13 items rejected (medium 4, low 9) as intent-scoped, design-level-and-already-recorded, or latent-without-current-defect.

Follow-up review recommendation: `true`; a high-severity patch was applied. Patched counts were high 1, medium 4, low 5, for a weighted score of 17.

Verification performed:
- `python3 tools/pack-release-packages.py /tmp/eventstore-3-6-dry 999.3.6-ci --dry-run` — 14 pack commands, each carrying `GeneratePackageOnBuild=false` and `UseHexalithProjectReferences=false`; no output directory created.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/... --configuration Release -m:1 -p:UseHexalithProjectReferences=false` — 0 warnings, 0 errors.
- Focused `ReleasePackageManifestTests` — 89/89 passed, 0 skipped (67 before this pass; the 22 added rows all behave as specified).
- Full `Hexalith.EventStore.Contracts.Tests` — 878/878 passed, 0 failed, 0 skipped.
- Real pack of all 14 projects at `999.0.0-ci-test`, then `scripts/validate-nuget-packages.py` and `tools/validate-release-packages.py` — both validated exactly 14 archives; `scripts/validate-consumer-package-references.py` — 13 isolated library consumers plus 1 isolated tool consumer, all green.
- Before/after probe of the fail-open: a Gateway archive declaring `DotnetTool` with zero dependency edges was **accepted** by the pre-patch validator and is now rejected; the same probe confirms rejection of a tool package omitting the type, a `<files>`-sibling leak, and a drive-relative archive entry, while the honest baseline still validates.
- Leak-pattern replay over 2,261 real cached nuspecs using the new whole-document scan — 0 false positives, while `bin/`, `obj/`, `artifacts/`, `~/` and UNC shapes are now rejected.
- Confirmed real `dotnet pack` output for `Hexalith.EventStore.Admin.Cli` does emit `<packageType name="DotnetTool" />`, so the new "tool package must declare it" direction cannot break a real release.
- `git diff --check` — no whitespace errors.

Residual risks (carried forward from the previous pass, still accurate):
- `_manifest_project_dependencies` derives expected edges from raw csproj XML rather than evaluated MSBuild: it silently skips `ProjectReference` includes containing `$`, ignores `Condition` and `PrivateAssets`, and cannot see references injected by imported props/targets. `_manifest_external_hexalith_dependencies` ignores `Condition` on `PackageReference` for the same reason. No manifest project trips this today — verified that no `src/` `ProjectReference` carries `PrivateAssets`/`ExcludeAssets`, and that the packer forcing `UseHexalithProjectReferences=false` makes the conditional Hexalith package references unconditional in practice — but a root-owned edge expressed through a property would be dropped from the expectation set, and a correct archive could then be rejected for declaring an "unexpected" dependency. A faithful fix requires MSBuild item evaluation, which is a design change rather than a patch.
- The internal dependency expectation and the archive under test both derive from the same working tree, so removing a `ProjectReference` shrinks both in lockstep. The Gateway four-edge literal is the only independent anchor; it covers the metadata-critical entry the intent names and nothing else.
- Internal dependency versions are compared as exact strings, so a legitimate NuGet range such as `[1.0.0, )` would read as drift. Current `dotnet pack` output emits plain versions for these edges only.
- Isolated consumers no longer exercise combined consumption, so a downgrade conflict visible only when two manifest packages are referenced together would go undetected. This is the intent's own scoping (`One manifest library at a time`), and it multiplies the shared-CI restore cost by 13.
- The archive-metadata contract is proven in-repo against synthetic archives; real `dotnet pack` output is exercised only by the Builds-owned shared CI step and by the manual command chain re-run in this pass.
- `assert_assets_use_packages`'s project-backed-library branch cannot fire for the consumers this script generates, since each writes exactly one `PackageReference` and no `ProjectReference`. It is retained as a cheap invariant; the live proof in that function is the adjacent resolved-package assertion.
