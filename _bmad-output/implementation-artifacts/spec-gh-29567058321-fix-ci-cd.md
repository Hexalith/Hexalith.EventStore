---
title: 'Fix mixed-job CI workflow guardrail'
type: 'bugfix'
created: '2026-07-17'
status: 'in-progress'
review_loop_iteration: 0
baseline_commit: '20be7872ff877f512d5663978185b5f425002185'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions run 29567058321 fails the first Tier 1 project because `Shared_ci_workflow_uses_domain_ci_with_deterministic_server_tests` forbids `runs-on:` and `steps:` across the whole CI workflow. Those keys now belong to the intentional, successful `tenants-source-mode` sibling job; the false failure stops the remaining 16 Tier 1 projects and prevents the release gate from succeeding.

**Approach:** Scope reusable-caller assertions to the exact `jobs.ci` block while preserving their anti-drift intent, and explicitly guard the blocking source-mode sibling job. Leave the working CI workflow and release topology unchanged.

## Boundaries & Constraints

**Always:** Keep `jobs.ci` as the shared `domain-ci.yml@main` caller; keep `tenants-source-mode` inside the blocking `CI` workflow with source references, serialized Debug build, and targeted topology tests; keep warnings as errors and run tests per project.

**Ask First:** Any change to `.github/workflows/`, shared Hexalith.Builds workflows, release gating, package dependencies, analyzer policy, or submodule contents/pointers.

**Never:** Delete the no-`runs-on`/no-`steps` protections, revert or move the successful source-mode job, make it advisory with `continue-on-error`, weaken test/build gates, initialize nested submodules, or use a legacy `.sln` or solution-level `dotnet test`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Mixed CI jobs | Shared `ci` caller plus inline `tenants-source-mode` sibling | Contract passes and validates each job against its own role | A whole-file false positive is a regression |
| Shared-caller drift | `jobs.ci` gains inline runner/steps or loses required inputs/tests | Contract fails on the exact shared job block | Failure identifies the shared release gate |
| Source-mode drift | Sibling job is missing, loses source mode/targeted test, or becomes advisory | Contract fails before release gating is weakened | Failure identifies the source-mode guardrail |
| Formatting/order | LF or CRLF workflow with either job last or reordered | Exact top-level job extraction remains deterministic | Missing/duplicate target job fails closed |

</frozen-after-approval>

## Code Map

- `.github/workflows/ci.yml` -- read-only mixed workflow: shared release-gate caller plus the successful inline Tenants source-mode job.
- `.github/workflows/release.yml` -- read-only proof that a successful push-event `CI` conclusion gates release.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- stale whole-file assertions and the intended workflow-governance contract.
- `_bmad-output/implementation-artifacts/2-4-tenants-rest-contract-metadata-and-routes.md` -- records the accepted blocking source-mode topology proof.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- extract an exact top-level workflow job block independent of line endings/order, scope shared-caller requirements and prohibitions to `jobs.ci`, and assert `tenants-source-mode` remains blocking, source-mode, serialized, and targeted -- fixes the false positive without weakening either gate.
- [ ] `tests/Hexalith.EventStore.Contracts.Tests` and all `.github/workflows/ci.yml` Tier 1 projects -- build with Release warnings-as-errors, run the focused workflow contract, the complete Contracts project, and the remaining configured projects individually -- proves the reported failure is fixed and exposes any cascade-hidden failure.

**Acceptance Criteria:**
- Given the current mixed-job workflow, when the focused workflow contract runs, then it passes while retaining no-`runs-on` and no-`steps` enforcement specifically for `jobs.ci`.
- Given either governed job drifts from its required role, when the contract evaluates its exact block, then the relevant assertion fails rather than accepting the drift or rejecting a valid sibling.
- Given fresh Release output, when all Tier 1 projects run individually in workflow order, then every configured project passes and no workflow, release, dependency, or submodule change is present.

## Spec Change Log

- 2026-07-17: Implemented the approved job-scoped guardrail and edge-case coverage. Focused evidence is green; the broad Tier 1 task remains open because unrelated existing failures and one bounded timeout prevent claiming the full gate passes.

## Design Notes

Use an exact two-space top-level job boundary over normalized line endings, with a fail-closed single-match requirement. This keeps the patch dependency-free while tolerating sibling reordering and a target job at end of file. Apply shared reusable-workflow assertions only to the `ci` block; keep explicit source-mode assertions on its sibling block.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -warnaserror -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method '*Packaging.ReleasePackageManifestTests.Shared_ci_workflow_uses_domain_ci_with_deterministic_server_tests'` -- expected: focused regression passes.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --no-build --configuration Release` -- expected: all Contracts tests pass.
- `sed -n '/      unit-test-projects: |/,/^$/p' .github/workflows/ci.yml | tail -n +2 | sed 's/^        //' | while IFS= read -r project; do [ -z "$project" ] || dotnet test "$project" --no-build --configuration Release; done` -- expected: every CI Tier 1 project passes individually.
- `git diff --check && git diff --exit-code -- .github/workflows/ci.yml .github/workflows/release.yml` -- expected: no whitespace errors and no workflow changes.

**Results (2026-07-17):**

- Focused Contracts Release build: passed with 0 warnings and 0 errors.
- Focused mixed-job CI contract: passed, 1/1.
- Job extraction edge cases (LF, CRLF, reordered/final target, missing/duplicate fail-closed): passed, 4/4.
- Complete `ReleasePackageManifestTests` class after the fresh solution build: passed, 20/20.
- Complete Contracts project: 705 passed, 1 failed, 0 skipped. The unrelated `CommitMessagePolicyTests.CopilotInstructionsExposeTheCommitlintContractDirectly` failure reports that unchanged `.github/copilot-instructions.md` contains `./references/...` instead of the expected `../references/...`.
- Fresh serialized package-mode `Hexalith.EventStore.slnx` Release build: passed with 0 warnings and 0 errors in 24.97 seconds.
- Remaining Tier 1 projects: 14 passed; `Hexalith.EventStore.Admin.Server.Tests` had 696 passed, 21 failed, and 18 skipped because `Hexalith.EventStore.Contracts, Version=3.67.3.0` could not be loaded. The same failure remained after the fresh solution build. `Hexalith.EventStore.Server.Tests` built successfully but did not complete within a bounded 90-second `--no-build` rerun (exit 124).
- `git diff --check` passed and both workflow files have no diff. Concurrent pre-existing submodule-pointer and unrelated BMAD artifact changes remain untouched, so the clean-worktree portion of the broad acceptance criterion is not satisfied.
