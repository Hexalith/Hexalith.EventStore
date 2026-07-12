---
title: 'Restore CI documentation contracts'
type: 'bugfix'
created: '2026-07-12'
status: 'in-review'
baseline_commit: '7eb975e0ea59bde713f514f609f090b1c52c2cba'
review_loop_iteration: 1
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
- [x] `AGENTS.md` and `CLAUDE.md` -- restore the deleted 14-package inventory block without replacing current submodule guidance.
- [x] `.github/copilot-instructions.md` -- restore the deleted direct commitlint contract with all asserted rules.
- [x] `tests/Hexalith.EventStore.Contracts.Tests` -- run the two focused regressions and the complete project using fresh Release output.

**Acceptance Criteria:**
- Given the current release manifest and instruction files, when both documentation-governance tests run, then they pass without changing their assertions.
- Given a fresh Release build, when the complete Contracts test project runs with the CI-equivalent command, then all 694 tests pass and evidence contains no failures.
- Given the final scoped diff, when compared with `471ca867`, then only the spec and three intended instruction files changed, and unrelated dirty work remains untouched.

## Spec Change Log

- **Review loop 1:** Adversarial review found that the submodule-safeguard verification command contained a literal placeholder, the claimed fresh build did not explicitly clean outputs, and diff-scope evidence conflated failed-run commit `471ca867` with implementation baseline `7eb975e0`. Verification now uses literal fail-closed safeguard checks, an explicit clean-before-build sequence, and separate exact path-set checks for both baselines. This avoids irreproducible evidence and ambiguous scope. **KEEP:** preserve the exact pre-`322e3193` documentation blocks, all current submodule guidance, the unchanged tests and manifest, the 694-test CI-equivalent lane, and the four-file implementation boundary.

## Design Notes

The failing head commit only bumps `Hexalith.Memories`; it retriggered a defect introduced earlier by `322e3193`. Restoring from that commit's parent is safer than inventing new wording because the existing tests were written against those sections and the last pre-deletion CI passed. `CLAUDE.md` must be repaired with `AGENTS.md`: the current test stops at the first missing document, so fixing only `AGENTS.md` would reveal the same failure on the next loop iteration.

Two baselines serve different checks: `7eb975e0` is the implementation baseline captured after the externally committed planning transition, while failed-run commit `471ca867` remains the frozen acceptance reference. Both must resolve to the same four approved paths when their diffs are explicitly restricted and compared as exact path sets.

## Verification

**Commands:**
- `dotnet clean tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 && dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: outputs are cleaned, then rebuilt with 0 warnings and 0 errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method "*Packaging.CommitMessagePolicyTests.CopilotInstructionsExposeTheCommitlintContractDirectly" -method "*Packaging.ReleasePackageManifestTests.Active_package_inventory_docs_match_manifest_package_set"` -- expected: 2 passed.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests --no-build --configuration Release --logger "trx;LogFileName=Hexalith.EventStore.Contracts.Tests.trx" --results-directory TestResults/Hexalith.EventStore.Contracts.Tests --collect:"XPlat Code Coverage"` -- expected: all 694 tests pass.
- ``for file in AGENTS.md CLAUDE.md .github/copilot-instructions.md; do rg -Fq -- '- Initialize root-declared submodules only, using the `references/...` paths declared in the root `.gitmodules` file.' "$file" && rg -Fq -- '- Avoid recursive submodule commands unless they are explicitly scoped so that nested submodules are not initialized.' "$file" && rg -Fq -- '- If nested submodules are initialized accidentally, deinitialize them before continuing.' "$file"; done`` -- expected: all three literal safeguards remain in all three instruction files.
- `expected=$(printf '%s\n' .github/copilot-instructions.md AGENTS.md CLAUDE.md _bmad-output/implementation-artifacts/spec-gh-29195132839-fix-tier-1-doc-contracts.md | sort); test "$(git diff --name-only 7eb975e0ea59bde713f514f609f090b1c52c2cba -- | sort)" = "$expected" && test "$(git diff --name-only 471ca8670a760bca95dde94dc4d90aecae053d86 -- .github/copilot-instructions.md AGENTS.md CLAUDE.md _bmad-output/implementation-artifacts/spec-gh-29195132839-fix-tier-1-doc-contracts.md | sort)" = "$expected"` -- expected: both the complete implementation diff and failed-run-scoped diff contain exactly the four approved paths.
- `git diff --check -- AGENTS.md CLAUDE.md .github/copilot-instructions.md _bmad-output/implementation-artifacts/spec-gh-29195132839-fix-tier-1-doc-contracts.md` -- expected: no whitespace errors.

**Observed Results (2026-07-12):**
- Explicit Release clean and build completed with exit status 0; both clean and build reported 0 warnings and 0 errors.
- Direct xUnit v3 execution completed with exit status 0: 2 passed, 0 failed, 0 skipped, and 0 not run.
- Complete Contracts lane completed with exit status 0: 694 passed, 0 failed, and 0 skipped. The exact retained artifacts are `TestResults/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.trx` and `TestResults/Hexalith.EventStore.Contracts.Tests/ec74b83e-e457-4925-80d9-3c7575a2086f/coverage.cobertura.xml`.
- Literal submodule-safeguard audit completed with exit status 0; all three safeguards were present in all three instruction files.
- Exact four-path comparisons against `7eb975e0ea59bde713f514f609f090b1c52c2cba` and scoped `471ca8670a760bca95dde94dc4d90aecae053d86` completed with exit status 0.
- Scoped `git diff --check` completed with exit status 0 and no output.
