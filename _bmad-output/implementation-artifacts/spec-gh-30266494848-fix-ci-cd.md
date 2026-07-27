---
title: 'Fix release package-count preflight forwarding'
type: 'bugfix'
created: '2026-07-27'
status: 'done'
baseline_commit: 'b2d3402552fbadf529c220fcc739da9d06d285fe'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions Release run `30266494848`, job `89978348977`, fails deterministically in semantic-release verification because the shared publication preflight now requires `--expected-package-count`, while the EventStore wrapper drops the workflow-exported `HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT=14`. Restore, Release build, source verification, package-count input validation, and supply-chain checks all pass before this wrapper boundary.

**Approach:** Make EventStore independently declare its 14-package release inventory, require the reusable workflow's exported count to match exactly, and forward that reviewed value to the shared preflight. Add focused governance coverage for matching, missing, empty, and mismatched values so future caller/toolchain upgrades fail locally before release publication.

## Boundaries & Constraints

**Always:** Preserve fail-closed publication ordering; keep the independently reviewed count aligned across `tools/release-packages.json`, `.github/workflows/release.yml`, and the wrapper; use `${VAR-}` semantics so unset and set-but-empty inputs both fail; preserve LF endings for shell files; leave the existing Story 2.12 and Tenants worktree changes untouched.

**Ask First:** Any discovery that the authoritative package inventory is not 14, that the approved Builds revision does not export `HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT`, or that the reusable Hexalith.Builds workflow must change.

**Never:** Infer the count dynamically from the manifest inside the wrapper; default a missing workflow value to 14; bypass or weaken the shared preflight; publish packages or containers during verification; edit root-declared submodules; treat the secondary semantic-release missing-label HTTP 422 as the cause of this release failure.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Matching inventory | Exported count is exactly `14`; wrapper declaration and manifest contain 14 packages | Wrapper invokes the shared preflight with `--expected-package-count 14` | Shared preflight remains authoritative for subsequent checks |
| Missing or empty input | Exported count is unset or empty | Wrapper stops before invoking the shared preflight | Emit a package-count contract error and return non-zero |
| Mismatched input | Exported count differs from `14`, including alternate numeric text | Wrapper stops before invoking the shared preflight | Emit a package-count contract error and return non-zero |
| Inventory drift | Manifest, workflow input, or wrapper declaration no longer agree | Contracts governance test fails | Require an intentional three-way inventory update |

</frozen-after-approval>

## Code Map

- `scripts/validate-publication-preflight.sh` -- EventStore-owned fail-closed adapter from reusable-workflow environment to the shared publication preflight CLI.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` -- release-wrapper contract and behavioral guardrails.
- `.github/workflows/release.yml` -- already declares `expected-package-count: 14`; authoritative caller state, inspection only.
- `tools/release-packages.json` -- authoritative 14-package inventory, inspection only.
- `.releaserc.json` -- invokes the wrapper before prepare/publish mutations, inspection only.

## Tasks & Acceptance

**Execution:**
- [x] `scripts/validate-publication-preflight.sh` -- declare the reviewed EventStore package count, compare the exported reusable-workflow value exactly, and forward the declaration with `--expected-package-count`.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` -- bind manifest, caller, wrapper declaration, environment contract, and forwarded CLI argument; behaviorally cover matching, missing, empty, and mismatched values; update the existing rejection test to reach its intended shared-preflight seam.

**Acceptance Criteria:**
- Given the current 14-package manifest and reusable release caller, when the wrapper receives `HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT=14`, then it forwards exactly `--expected-package-count 14` before any publication mutation.
- Given the count is missing, empty, or not exactly `14`, when the wrapper executes, then it returns non-zero without invoking the shared preflight.
- Given the focused Contracts governance class and full Contracts assembly, when executed in Release/package mode, then they pass with zero failures and no new skips or warnings.

## Spec Change Log

- 2026-07-27: Implemented exact package-count validation and forwarding, added four-row matrix coverage, and verified the focused and full Contracts suites in Release/package mode.

## Design Notes

The wrapper count is intentionally independent rather than manifest-derived. This creates three reviewed authorities—manifest contents, caller input, and adapter declaration—whose agreement is enforced by tests and at runtime, preventing one accidental edit from silently redefining the publication gate.

## Verification

**Commands:**
- `bash -n scripts/validate-publication-preflight.sh` -- expected: shell syntax succeeds.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -warnaserror -m:1` -- expected: zero warnings and zero errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class 'Hexalith.EventStore.Contracts.Tests.Packaging.ContainerPublishingGovernanceTests'` -- expected: all focused governance cases pass.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll` -- expected: the full Contracts assembly passes with zero failures and no new skips.

**Results:**
- `bash -n scripts/validate-publication-preflight.sh` -- passed.
- Release/package-mode Contracts build -- passed with 0 warnings and 0 errors.
- Focused `ContainerPublishingGovernanceTests` class -- passed 19/19 with 0 skipped.
- Full Contracts assembly -- passed 778/778 with 0 skipped.

## Suggested Review Order

**Fail-closed release adapter**

- Declare inventory independently so manifest drift cannot redefine the gate silently.
  [`validate-publication-preflight.sh:20`](../../scripts/validate-publication-preflight.sh#L20)

- Reject absent or mismatched workflow input before entering shared publication code.
  [`validate-publication-preflight.sh:47`](../../scripts/validate-publication-preflight.sh#L47)

- Forward the exact reviewed value into the approved preflight CLI.
  [`validate-publication-preflight.sh:64`](../../scripts/validate-publication-preflight.sh#L64)

**Cross-layer governance**

- Bind manifest, caller input, and wrapper declaration to one reviewed count.
  [`ContainerPublishingGovernanceTests.cs:168`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs#L168)

- Exercise both verification and publication phases through the adapter seam.
  [`ContainerPublishingGovernanceTests.cs:203`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs#L203)

- Prove shared rejection occurs before NuGet and container mutations.
  [`ContainerPublishingGovernanceTests.cs:406`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs#L406)

- Bound process coverage to avoid verification hangs and stream deadlocks.
  [`ContainerPublishingGovernanceTests.cs:602`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs#L602)
