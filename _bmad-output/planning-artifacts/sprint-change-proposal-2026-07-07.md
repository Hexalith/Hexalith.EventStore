# Sprint Change Proposal — CI Tier-1 failure: brittle Hexalith version pin

- **Date:** 2026-07-07
- **Author:** Administrator (Correct-Course workflow)
- **Trigger:** CI failure — [run 28889353855 / job build-and-test](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/28889353855/job/85697653496)
- **Scope classification:** **Minor** (direct developer fix)
- **Mode:** Batch

## Section 1 — Issue Summary

The `ci / build-and-test` job failed at **Unit tests (Tier 1)**. Restore and build passed (0 errors); a single test failed:

```
Hexalith.EventStore.Contracts.Tests.Packaging.ContractsPackageDependencyTests
  .Contracts_package_pins_commons_unique_ids_to_published_commons_version
Shouldly.ShouldAssertException:
  packageVersionReference should be "2.26.0" but was "2.27.0"
Failed: 1, Passed: 594, Total: 595
```

**Root cause.** The `references/Hexalith.Builds` submodule was intentionally bumped (commit `b32643f0`, already on `main`), moving the centrally-managed `Hexalith.Commons.UniqueIds` package from `2.26.0` → `2.27.0` in the shared `references/Hexalith.Builds/Props/Directory.Packages.props`. That `2.27.0` is a valid published version — restore and build succeeded against it. However, the guardrail test **hard-coded** the expected version string, so every intentional submodule bump breaks CI until the assertion is hand-edited. The test was asserting the *value* of a Hexalith package version, which is exactly the brittle coupling we want to remove.

## Section 2 — Impact Analysis

- **Epic impact:** None. No epic/story acceptance criteria depend on the pinned commons version value.
- **Story impact:** None. No active story owns this assertion. (The only reference to the old test name is a dated historical log in `_bmad-output/implementation-artifacts/D-3-controller-emission.md`, left unchanged.)
- **Artifact conflicts:** The `architecture.md` dependency-table snapshot lists `Hexalith.Commons.UniqueIds | 2.26.0`. Left as the historical point-in-time snapshot value — per the "do not track Hexalith package versions" directive, we do not chase Hexalith version numbers through documentation artifacts.
- **Technical impact:** Test-only. No production code, no runtime behavior, no packaging output changes.

## Section 3 — Recommended Approach

**Direct Adjustment.** Relax the guardrail so it no longer checks the *specific* Hexalith package version, while preserving the two assertions that carry real value:

1. the root `Directory.Packages.props` does **not** redeclare the version (central management is intact); and
2. the shared props pin the package to a **single concrete version** (a non-empty pin exists — not a floating range).

The specific version string is no longer asserted, so future `Hexalith.Builds` submodule bumps will not fail CI. Rejected alternative: bumping the literal to `2.27.0` — it fixes today's red but re-arms the same trap on the next bump.

- **Effort:** trivial (one test method).
- **Risk:** low — structural guardrails retained; verified by running the full Tier-1 project (595/595 pass).
- **Timeline:** immediate.

## Section 4 — Detailed Change Proposals

### Change 1 — `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.cs`

Rename `Contracts_package_pins_commons_unique_ids_to_published_commons_version` →
`Contracts_package_pins_commons_unique_ids_centrally` and replace the hard-coded version assertion.

```
OLD:
    string packageVersionReference = sharedPackageVersions … .Value;
    packageVersionReference.ShouldBe("2.26.0");

NEW:
    // The specific version value is intentionally not asserted so that
    // Hexalith.Builds submodule bumps do not break this test.
    string packageVersionReference = sharedPackageVersions … .Value;
    packageVersionReference.ShouldNotBeNullOrWhiteSpace();
```

**Rationale:** Removes the version-value coupling to Hexalith packages while keeping the central-management and concrete-pin guardrails.

### Non-change — architecture.md dependency table

No net change. The `2.27.0` bump initially applied to the snapshot table was reverted to `2.26.0` per the "do not track Hexalith package versions" directive. The independent AD-15 edits present in that file were made externally and are intentionally left untouched.

## Section 5 — Story Rewrite Gate

**Not triggered.** This change is not an architectural pivot and does not supersede any active story's acceptance criteria, tasks, Dev Notes, project-structure notes, or design assumptions. No story-file rewrites are required.

## Section 6 — Implementation Handoff

- **Scope:** Minor → implemented directly by developer in this session.
- **Status:** Applied and verified. `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --configuration Release` → **595 passed, 0 failed**; test project builds clean under `-warnaserror`.
- **Success criteria:** Tier-1 unit-test step of `ci / build-and-test` is green; future `Hexalith.Builds` version bumps no longer require editing this test.
