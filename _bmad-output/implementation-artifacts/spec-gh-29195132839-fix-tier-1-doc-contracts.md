---
title: 'Restore CI documentation contracts'
type: 'bugfix'
created: '2026-07-12'
status: 'ready-for-dev'
baseline_commit: '471ca8670a760bca95dde94dc4d90aecae053d86'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions run 29195132839 fails the first Tier 1 project because commit `322e3193` removed repository guidance that two documentation-governance tests still require. `AGENTS.md` lacks the manifest-driven 14-package inventory, `CLAUDE.md` has the same latent defect, and `.github/copilot-instructions.md` lacks the directly embedded commitlint contract.

**Approach:** Restore the exact deleted release-inventory and commit-message guidance from the last successful baseline while preserving all newer submodule instructions. Keep the tests and CI gates unchanged.

## Boundaries & Constraints

**Always:** Treat `tools/release-packages.json` and the existing contract tests as authoritative; keep the root and Copilot instruction links correctly relative; name every current manifest package; preserve the current root-declared-only submodule rules; avoid unrelated dirty story, source, and submodule changes.

**Ask First:** Any package-manifest change, test-policy change, submodule edit, or rewrite of shared instructions under `references/Hexalith.AI.Tools`.

**Never:** Weaken or delete the failing assertions, hard-code a different package set, initialize nested submodules, or modify the unrelated dirty files approved as protected boundaries.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Package guidance | Manifest contains 14 packages | `AGENTS.md` and `CLAUDE.md` state `14 packages` and name every manifest ID | Contract test fails on any missing count or package |
| Copilot guidance | Copilot generates a commit message | Instructions directly expose the conventional format, casing, length, release-impact, and `revert` rules | Contract test fails on any missing required phrase |
| Existing guidance | Newer submodule rules coexist with restored sections | Root-only initialization and nested-submodule safeguards remain intact | Diff review rejects accidental rollback |

</frozen-after-approval>

## Code Map

- `AGENTS.md` -- repository agent entry point; must expose the complete manifest-driven release inventory.
- `CLAUDE.md` -- parallel agent entry point covered by the same inventory contract.
- `.github/copilot-instructions.md` -- Copilot entry point that must embed the commitlint contract directly.
- `tools/release-packages.json` -- authoritative 14-package release manifest; read-only for this fix.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- inventory documentation contract.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` -- Copilot commit-message guidance contract.

## Tasks & Acceptance

**Execution:**
- [ ] `AGENTS.md` and `CLAUDE.md` -- restore the deleted 14-package inventory block without replacing current submodule guidance.
- [ ] `.github/copilot-instructions.md` -- restore the deleted direct commitlint contract with all asserted rules.
- [ ] `tests/Hexalith.EventStore.Contracts.Tests` -- run the two focused regressions and the complete project using fresh Release output.

**Acceptance Criteria:**
- Given the current release manifest and instruction files, when both documentation-governance tests run, then they pass without changing their assertions.
- Given a fresh Release build, when the complete Contracts test project runs with the CI-equivalent command, then all 694 tests pass and evidence contains no failures.
- Given the final scoped diff, when compared with `471ca867`, then only the spec and three intended instruction files changed, and unrelated dirty work remains untouched.

## Spec Change Log

## Design Notes

The failing head commit only bumps `Hexalith.Memories`; it retriggered a defect introduced earlier by `322e3193`. Restoring from that commit's parent is safer than inventing new wording because the existing tests were written against those sections and the last pre-deletion CI passed. `CLAUDE.md` must be repaired with `AGENTS.md`: the current test stops at the first missing document, so fixing only `AGENTS.md` would reveal the same failure on the next loop iteration.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: clean build with warnings as errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method "*Packaging.CommitMessagePolicyTests.CopilotInstructionsExposeTheCommitlintContractDirectly" -method "*Packaging.ReleasePackageManifestTests.Active_package_inventory_docs_match_manifest_package_set"` -- expected: 2 passed.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests --no-build --configuration Release --logger "trx;LogFileName=Hexalith.EventStore.Contracts.Tests.trx" --results-directory TestResults/Hexalith.EventStore.Contracts.Tests --collect:"XPlat Code Coverage"` -- expected: all 694 tests pass.
- `git diff --check -- AGENTS.md CLAUDE.md .github/copilot-instructions.md _bmad-output/implementation-artifacts/spec-gh-29195132839-fix-tier-1-doc-contracts.md` -- expected: no whitespace errors.
