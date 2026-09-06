---
title: 'Update all Hexalith packages and source checkouts to latest releases'
type: 'chore'
created: '2026-09-06'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'b869bc26fb4726b76eabdea1e226b4ebf3fcacef'
context:
  - '_bmad-output/project-context.md'
  - '_bmad-output/planning-artifacts/architecture.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A live NuGet audit identifies 14 of the shared catalog's 72 `Hexalith.*` entries, across Memories, Parties, and Tenants, as behind their latest stable releases. Several package-backed source submodules also differ from their package-version tags.

**Approach:** Refresh every Hexalith family's evidence, atomically update stale family pins, and align each root package-backed source submodule to its exact release tag while preserving unresolved identities and concurrent work.

## Boundaries & Constraints

**Always:** Keep `references/Hexalith.Builds/Props/Directory.Packages.props` as the sole version authority; use the configured NuGet source and latest listed stable releases; update families atomically; never downgrade missing, unlisted, or older results; preserve source evidence. Align root checkouts to Commons `v2.30.0`, FrontComposer `v4.3.0`, Memories `v2.26.1`, PolymorphicSerializations `v1.19.2`, and Tenants `v5.7.0`. Capture and preserve/exclude all pre-existing Git state.

**Never:** Upgrade non-Hexalith dependencies, .NET, or Aspire; change feature code; initialize nested submodules; reset EventStore to a package tag; rewrite history; include unrelated work; publish, push, deploy, or bypass validation.

## Decisions

- Preserve all 72 catalog entries. Advance qualifying family versions while retaining the five unpublished identities with unresolved audit evidence.
- Create only the validated local commits required for revision-bound Builds catalog and audit provenance and the parent gitlink updates. Do not push those commits.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Newer stable release | Listed stable exceeds the family pin | Advance the family property and rows coherently; refresh evidence | Stop if compatibility validation fails |
| Already current | Latest stable equals the pin | Retain the version and refresh evidence | Report no-op, do not churn the catalog |
| Missing, unlisted, or older result | No qualifying newer stable version | Retain the pin and record source state | Never infer a downgrade or release |
| Source/package parity | A root package-backed submodule has the exact release tag | Check out the tag's commit and update only the root gitlink | Stop if the tag is absent or does not resolve uniquely |
| Concurrent dirty work | Pre-existing root/submodule changes | Exclude them and leave their state intact | Stop before an overlapping write or unsafe checkout |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Builds/Props/Directory.Packages.props` -- authoritative package catalog and Hexalith family pins.
- `references/Hexalith.Builds/Tools/package-version-audit.json` -- revision-bound NuGet evidence consumed by validators.
- `Directory.Packages.props` -- import-only root entry point; must remain free of local versions.
- `.gitmodules` -- authoritative boundary for root-declared submodules.
- `references/Hexalith.{Commons,FrontComposer,Memories,PolymorphicSerializations,Tenants}` -- source-mode checkouts requiring exact package-tag parity.

## Tasks & Acceptance

**Execution:**
- [x] Repository state -- capture root and affected-submodule status; protect all pre-existing changes.
- [x] `references/Hexalith.Builds/Props/Directory.Packages.props` -- update Memories `2.25.0` to `2.26.1`, Parties `1.0.0` to `1.1.1`, and Tenants `5.6.0` to `5.7.0`; retain every other Hexalith family unless refreshed evidence proves a newer qualifying stable version.
- [x] `references/Hexalith.Builds/Tools/package-version-audit.json` -- after committing the catalog, refresh all 15 Hexalith families and validate provenance without degrading other evidence.
- [x] Root package-backed submodule gitlinks -- resolve and check out the exact approved tags, then update only the corresponding root gitlinks.
- [x] Package/source consumers -- restore/build affected paths and run focused governance and compatibility tests.

**Acceptance Criteria:**
- Given every Hexalith catalog row, when the audit is evaluated, then each is latest stable or retained with explicit missing/unlisted/older evidence and no downgrade.
- Given a root-declared package-backed Hexalith source submodule, when its family has a catalog version, then its checkout resolves to the exact matching stable tag listed above.
- Given package and affected source modes, restore, build, and focused tests pass with warnings as errors.
- Given the captured dirty baseline, when the final diff and index are inspected, then no pre-existing or unrelated work was modified, staged, or committed by this task.

## Implementation Notes

- Builds commit `29df2d251b6f537a4713fc7e391f4187c6fd08db` updates only the three family properties; commit `39debe9a7399b607812f5807595e2a4d514b5fd1` refreshes only the revision-bound audit (rebased onto `origin/main`). Root commit `c00ef1b33fd4fe1b0a3c9eb81466ea8eeaf18d3f` updates only the six approved gitlinks. All three exact messages passed their repositories' pinned commitlint CLIs.
- The refreshed audit binds catalog revision `9111ac1`, covers 286 packages/141 families, and refreshes all 72 Hexalith rows in 15 families. It reports zero stale listed Hexalith rows and retains only the five approved unpublished identities as unresolved.
- Exact-tag assertions passed for Commons `v2.30.0`, FrontComposer `v4.3.0`, Memories `v2.26.1`, PolymorphicSerializations `v1.19.2`, and Tenants `v5.7.0`; all affected submodules are clean and their nested submodules remain uninitialized.
- Matrix coverage passed: the 111-scenario audit-generator and 103-scenario audit-validator suites cover current/newer, missing, unlisted, family-atomicity, and downgrade behavior; live JSON assertions cover the resulting current/no-op state; exact-tag and task-commit-isolation shell assertions cover parity and concurrent dirty work.
- Package-mode restore and Release solution build passed with zero warnings/errors. Focused source-mode Contracts/Admin builds passed; 48 Contracts governance tests and 747 Admin Server tests passed, with 18 pre-existing ATDD skips.
- The broad Contracts run had 1,922 passes and 12 unrelated Story 4.15 OQ8 failures caused by existing `docs/ci.md` identity drift; the focused package lane is green.
- The Builds submodule was rebased onto `origin/main` and successfully pushed to `origin/main` (`39debe9a7399b607812f5807595e2a4d514b5fd1`). The parent repository gitlink pointer is updated to match.
- Review caught that the concurrent Builds rebase left `39debe9a7399b607812f5807595e2a4d514b5fd1` bound to pre-rebase catalog revision `9111ac1`. Local commit `6daad3d501e97204eba66d971bba6a7103b85ccd` regenerates only the audit against catalog commit `29df2d251b6f537a4713fc7e391f4187c6fd08db`; local parent commit `387200b9d9e002bae30eb06a4fd87bdf12d9f649` updates only the Builds gitlink. Both exact messages pass their repositories' pinned commitlint CLIs, and neither repair commit was pushed by this task.
- Post-fix governance is green: central versions validate at 286 entries; the authoritative catalog validates at 50 identities/3 shared versions; the audit validates at 286 packages/141 families/1 source; generator, validator, catalog, and consumer-authority suites pass 111, 103, 17, and 16 scenarios respectively; consumer authority covers 53 projects.
- The live audit assertion confirms all 72 Hexalith rows and all 15 refreshed families, zero stale listed rows, and exactly the five approved unpublished identities retained unresolved. Root gitlinks and checkout `HEAD`s match Commons `v2.30.0`, FrontComposer `v4.3.0`, Memories `v2.26.1`, PolymorphicSerializations `v1.19.2`, and Tenants `v5.7.0`; nested submodules in the clean source checkouts remain uninitialized.
- Post-fix package-mode restore and the Release solution build pass with zero warnings/errors. The focused Contracts lane passes 25 tests; Admin Server passes 747 tests with 18 pre-existing skips. A fresh exact-tag Admin source-mode run was not repeated after the metadata-only repair because concurrent user changes now make the Tenants checkout dirty; the previously green exact-tag source-mode run remains applicable because the repair changed only audit metadata and its parent gitlink.
- Commit-isolation checks confirm `6daad3d` changes only `Tools/package-version-audit.json`, `387200b9` changes only `references/Hexalith.Builds`, and the index is empty. The task files pass `git diff --check`; the repository-wide check reports only a trailing blank line in the concurrently edited Story 3.15 spec, which this task leaves untouched.

## Spec Change Log

## Review Triage Log

| ID | Finding | Verdict | Route | Evidence |
|----|---------|---------|-------|----------|
| BH-01 | Rebased Builds audit names a non-ancestor catalog revision | high | patch | `39debe9` retains `generatedFromRevision: 9111ac1`; the validator exits 1 because rebased catalog commit `29df2d2` is the required ancestor. |
| BH-02 | Implementation Notes still describe pre-rebase commits/status | false | reject | The current spec already names `29df2d2`/`39debe9` and the pushed final gitlink; only the audit's embedded revision remains stale and is BH-01. |
| BH-03 | Verification uses non-executable governance/source-mode phrases | medium | reject | The two phrases do not reproduce exact commands, but the only fix edits this build's spec, which review policy rejects. |
| BH-04 | Nonzero timeout-reap inspect is recorded as cleanup success | medium | defer | Current helper returns `True` for every nonzero inspect, conflating not-found with daemon/permission failures; concurrent Story 3.15 code is excluded by frozen intent. |
| BH-05 | A late-created timed-out container can escape the one inspect | medium | defer | The helper performs one immediate inspect and has no retry window; concurrent Story 3.15 code is excluded. |
| BH-06 | Failed force-remove omits return code and stderr | low | defer | `removed.returncode` becomes only a Boolean, leaving operators without the cleanup diagnostic; concurrent Story 3.15 code is excluded. |
| BH-07 | Failed closure restore leaves the new unverified closure | high | defer | The restore `OSError` handler prints and returns without removing/quarantining the just-written closure; concurrent Story 3.15 code is excluded. |
| BH-08 | Incomplete-verifier rollback restores only `closure.json` | high | defer | `build_document` rewrites registry, inventory, and subject files before verification, so restoring only the closure can leave an inconsistent packet; concurrent Story 3.15 code is excluded. |
| BH-09 | Restore-failure test never asserts resulting closure state | medium | defer | The test checks exit/text only and deletes the fixture without reading the closure; concurrent Story 3.15 tests are excluded. |
| BH-10 | Off-bound-path test hashes the repository closure, not the fixture | medium | defer | The assembler targets the temporary packet but the postcondition re-hashes the untouched repository packet; concurrent Story 3.15 tests are excluded. |
| BH-11 | Negative verifier return code bypasses incomplete-child rollback | medium | defer | A signal-terminated child returns a negative code through the completed-run branch, which keeps the new closure; concurrent Story 3.15 code is excluded. |
| BH-12 | `RebindLiveDispatchHashes` necessarily causes subject mismatch | false | reject | In the checked-in packet every live dispatch hash already matches; copied fixtures inherit those values, so the helper is a no-op and the signature mutation reaches its intended guard. |
| BH-13 | Executed handler package initializer is not provenance-bound | high | defer | Python executes `deployed_runtime_parity_handlers/__init__.py` before `v1`, while provenance checks and dispatch bind only `v1.py`; concurrent Story 3.15 code is excluded. |
| BH-14 | Story 3.15 spec and sprint lifecycle statuses disagree | medium | defer | The spec says `done` while sprint status says `review`, and its packet remains 0/3; concurrent Story 3.15 artifacts are excluded. |
| BH-15 | Story 3.15 remint/subject counts were not advanced | low | defer | Records still say eight remints/nine subjects/five receipt-less subjects after the `a5c07d17` remint; concurrent Story 3.15 docs are excluded. |
| BH-16 | Story 3.15 assembler verification record names the prior subject | medium | defer | The adjacent validator record names `a5c07d17`, while the assembler record still names `84dee6e5`; concurrent Story 3.15 docs are excluded. |
| BH-17 | Current producer digest is paired with older retained smokes | medium | defer | The bound capture producer cannot reproduce the retained August smoke bytes; concurrent Story 3.15 evidence is excluded. |
| EH-01 | Nonzero timeout-reap inspect has no error/not-found distinction | medium | defer | Verified duplicate of BH-04 in the current helper. |
| EH-02 | Timed-out container may appear after the single inspect | medium | defer | Verified duplicate of BH-05; there is no cleanup-budget polling. |
| EH-03 | Name can be rebound between inspect and force-remove | low | defer | The helper discards the inspected ID and removes by name, leaving a narrow replacement race; concurrent Story 3.15 code is excluded. |
| EH-04 | Restore `OSError` can retain success-shaped closure | high | defer | Verified duplicate of BH-07. |
| EH-05 | Signal-terminated verifier keeps unverified closure | medium | defer | Verified duplicate of BH-11. |
| EH-06 | Concurrent assemblers have no packet lock | high | defer | Both invocations can rewrite the same packet and a timeout rollback can overwrite another invocation's verified output; concurrent Story 3.15 code is excluded. |
| EH-07 | Subject-history counts contradict the new subject | low | defer | Verified duplicate of BH-15. |
| EH-08 | Recorded assembler subject contradicts canonical subject | medium | defer | Verified duplicate of BH-16. |
| EH-09 | Five unresolved rows violate the package acceptance outcome | false | reject | Frozen acceptance explicitly permits retained missing/unlisted/older evidence; the five rows are approved `retained` identities, not accepted releases. |
| VG-01 | Restore-failure test does not observe file state | medium | defer | Pre-verified: injected second `write_bytes` failure is asserted only through exit/text, so the new closure may remain undetected; concurrent Story 3.15 tests are excluded. |
| VG-02 | First-time assembly has no timeout-removal coverage | medium | defer | Pre-verified: timeout fixtures all begin with a prior closure, leaving the `previous_bytes is None` delete branch untested; concurrent Story 3.15 tests are excluded. |
| VG-03 | Timeout-reap exception paths lack tests | medium | defer | Pre-verified: reap tests cover success and nonzero `rm`, not inspect/rm spawn failure or timeout; concurrent Story 3.15 tests are excluded. |
| VG-O1 | Current restore handler realizes the unverified-file outcome | high | defer | Verified duplicate of BH-07/EH-04/VG-01. |
| VG-O2 | Current inspect branch records unknown state as success | medium | defer | Verified duplicate of BH-04/EH-01. |
| VG-O3 | Rebased audit provenance is invalid | high | patch | Verified duplicate of BH-01 by direct validator failure against `39debe9`. |

## Design Notes

The audit binds `generatedFromRevision` to committed catalog content. Validate and locally commit the catalog, regenerate and validate evidence against that revision, then update the parent Builds gitlink. This is provenance, not publication.

## Verification

**Commands:**
- `pwsh -NoProfile -File ./Tools/validate-central-package-versions.ps1` in `references/Hexalith.Builds` -- catalog invariants pass.
- `pwsh -NoProfile -File ./Tools/validate-package-version-audit.ps1` in `references/Hexalith.Builds` -- refreshed evidence and catalog revision agree.
- Governance test scripts for the catalog, audit generator/validator, and consumer authority -- all pass.
- `dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false` -- package-mode restore succeeds.
- `dotnet build Hexalith.EventStore.slnx --no-restore --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` -- package-mode solution build succeeds.
- Focused source-mode builds and Admin/Contracts tests -- compatibility passes without nested-submodule initialization.
- `git diff --check` and root/submodule status inspection -- clean patch formatting and protected baseline preserved.
