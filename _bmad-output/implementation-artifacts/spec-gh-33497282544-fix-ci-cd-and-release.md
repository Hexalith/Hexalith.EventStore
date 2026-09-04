---
title: 'Fix CI timestamp decay and publish the verified release'
type: 'bugfix'
created: '2026-09-04'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'fcafc59464efd2f97347a97f19a1d48ad340f10c'
context:
  - 'docs/ci.md'
  - '_bmad-output/implementation-artifacts/spec-postgres-image-governance.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** CI runs `33497282544` and `33864785022` fail only in Contracts because commit `f152995794337a929c0a1ec2242eff9a5a3a1c44` changed Story 4.15 validator/test dates without renewing content-bound evidence. Its fixed “future” timestamps also decay as the wall clock advances.

**Approach:** Restore approved chronology, make future-time tests deterministic without weakening UTC/order/hash/authority checks, and preserve completed evidence through an additive successor. Then push the repair, require green exact-source CI, run the protected release, and verify GitHub and NuGet outputs.

## Boundaries & Constraints

**Always:** Keep Story 4.15 v1/v2 historical artifacts verifiable; use an additive successor for evolved validator/test bytes; retain the reviewed PostgreSQL index, source-only authority, full Contracts lane, warnings-as-errors, 14-package inventory, exact-source proof, protected environment, immutable publisher pin, and collision/post-publish gates. Validate the exact commit message. Bind the release tag to the pushed SHA and validate both publication channels by ID, version, count, and package contract.

**Never:** Skip or soften OQ8 tests; rewrite historical evidence or published objects; change inventory, PostgreSQL identity, destinations, credentials, protection, or publisher pin; use release bypass when ordinary CI succeeds; reuse a partially published version.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Approved chronology | Historical closures plus the active successor | Contracts passes on later calendar dates | Fail on hash, schema, order, or authority drift |
| Future mutation | Timestamp after captured current UTC | Reject as later than current UTC | Assert the specific diagnostic |
| Publication | Green exact-source `main` | Stable release with 14 GitHub assets and 14 NuGet packages | Stop on denial, collision, missing output, or partial failure; never reuse the version |

</frozen-after-approval>

## Code Map

- `tools/validate-oq8-platform-evidence.py` -- date mismatch, UTC/future validation, binding, and successor selection.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- four expiring future mutations and successor coverage.
- `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v2/**` -- completed historical closure; verify but do not rewrite.
- `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v3/**` -- additive successor: identity, pre-review results, subject, receipts, handoff, manifest.
- `.github/workflows/ci.yml` -- exact failing Contracts restore/build/test commands; behavior is preserved.
- `.github/workflows/release.yml`, `.releaserc.json`, `tools/release-packages.json` -- protected release and 14-package authority; verify, do not broaden.

## Tasks & Acceptance

**Execution:**
- [x] `tools/validate-oq8-platform-evidence.py` -- parse exact UTC seconds generically, reject real future time, validate v2 historically, and require v3 for evolved bytes.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- use runtime-relative future values and cover v2 preservation plus v3 success/drift/future cases.
- [x] `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v3/**` -- seal candidate → subject → reviews → handoff → sorted manifest without changing v1/v2.
- [x] `docs/ci.md` -- document the durable timestamp rule and active successor lineage if operator guidance changes.
- [ ] Git/GitHub/NuGet -- validate a `fix(ci): ...` message, commit/push `main`, require exact-source CI, dispatch ordinary Release, then verify tag/source, release assets, NuGet availability, and contents.

**Acceptance Criteria:**
- Given any later current date, when OQ8 closure and full Contracts run, then approved evidence passes and future/order/drift mutations fail for their intended reasons.
- Given the repair commit on live `main`, when blocking push workflows complete, then CI and Commitlint succeed for that exact SHA without bypass or excluded tests.
- Given successful ordinary publication, when GitHub and NuGet are queried, then a stable release targets the repair SHA and all 14 package IDs exist at one valid version.

## Implementation Notes

- Preserved the v1/v2 evidence bytes and bound historical v2 validation to completed closure commit `83b32fcfad7bb608098aebccdc15002636ffb431`; v3 is the only active current-source successor.
- Aligned `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` with the authoritative shared-policy wording introduced when root commit `fcafc59464efd2f97347a97f19a1d48ad340f10c` updated `references/Hexalith.AI.Tools` to `5f93d2ec8239494852c97032c819cb1689939e36`. The shared instruction changes themselves were preserved.
- Local implementation and verification are complete. The Git/GitHub/NuGet task remains open because push, workflow dispatch, and publication require the post-review remote phase.

## Spec Change Log

## Review Triage Log

## Design Notes

The validator and closure test are content-bound, so their evolution belongs in v3 rather than hidden inside v2. The manifest binds exact approved timestamps; generic parsing plus comparison with current UTC avoids encoding “today” in source.

## Verification

Local results: active and historical-v2 validators passed; the focused OQ8 suite passed 375/375; full Contracts passed 1,896/1,896; tier 1 passed; the Release build completed with zero warnings/errors; diff hygiene and the exact candidate commit message passed.

**Commands:**
- `python3 tools/validate-oq8-platform-evidence.py` -- expected: historical v1/v2 and active v3 current-source closure pass.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` -- expected: zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests -noColor` -- expected: all closure cases pass.
- Exact `.github/workflows/ci.yml` Contracts command, then `./scripts/ci-local.sh --tier 1` -- expected: local gates pass.
- `npx commitlint --edit <candidate-message-file> --verbose` and `git diff --check` -- expected: exact message and diff pass.
- `gh run watch <exact-source-ci-run> --repo Hexalith/Hexalith.EventStore --exit-status` -- expected: successful push CI for live `main`.
- `gh workflow run release.yml --repo Hexalith/Hexalith.EventStore --ref main -f bypass-validation=false` and `gh run watch <release-run> --exit-status` -- expected: protected ordinary release succeeds.
- GitHub tag/release inspection and `python3 tools/validate-release-packages.py <assets> <version>` -- expected: repair SHA and 14 valid assets.
- NuGet flat-container download plus the same validator -- expected: all 14 public packages exist and validate.
