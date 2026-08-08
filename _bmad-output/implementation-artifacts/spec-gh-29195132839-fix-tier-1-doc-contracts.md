---
title: 'Close obsolete CI documentation-contracts restore'
type: 'bugfix'
created: '2026-07-12'
status: 'done'
baseline_commit: '9034b8988f64139748ed4ed195189f7397edff3d'
review_loop_iteration: 2
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story GH-29195132839 originally restored a 14-package inventory and an embedded Copilot commitlint contract into the three agent entry points. Later main work moved those contracts to repo docs and shared Hexalith.AI.Tools instructions, so the frozen restore intent now conflicts with live packaging tests and the shared-baseline rule that keeps repository-specific guidance out of those entry points.

**Approach:** Close this story as obsolete without restoring inventory or commitlint text into `AGENTS.md`, `CLAUDE.md`, or `.github/copilot-instructions.md`. Record that the current homes already satisfy the original CI need, and leave code, tests, and shared instructions untouched.

## Boundaries & Constraints

**Always:** Treat current `main` tip as authoritative; keep the three entry points as identical shared baselines; leave `tools/release-packages.json`, inventory docs, packaging tests, and `references/Hexalith.AI.Tools` unchanged; touch only this spec file for closure.

**Ask First:** Any request to re-embed package inventory or commitlint markers into the three entry points, change packaging tests, or edit shared Hexalith.AI.Tools instructions.

**Never:** Re-run the July restore; invent a parallel inventory in the entry points; weaken or delete live packaging assertions; initialize nested submodules; mutate source, tests, or submodule pins for this closure.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Obsolete close | Approved supersession intent on clean `main` | Spec becomes `done`; entry points and tests unchanged | Stop if unrelated dirty edits appear outside this spec |
| Inventory proof | Live inventory docs + manifest | Docs still name 14 packages and every manifest ID | Do not move inventory back into entry points |
| Commitlint proof | Copilot entry point + shared git instructions | Entry points delegate; shared instructions hold policy | Do not re-embed forbidden commitlint markers |

</frozen-after-approval>

## Code Map

- `tools/release-packages.json` -- authoritative 14-package release manifest (read-only).
- `docs/reference/nuget-packages.md`, `docs/brownfield/project-overview.md`, `docs/brownfield/architecture.md`, `_bmad-output/project-context.md` -- current inventory documentation homes asserted by packaging tests.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- `Active_package_inventory_docs_match_manifest_package_set` asserts the four doc paths above, not `AGENTS.md`/`CLAUDE.md`.
- `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md` -- identical shared baselines; Shared Entry Points forbids repo-specific inventory here (`SharedInstructionEntryPointTests`).
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` -- live gate is `CopilotInstructionsDelegateCommitlintContractToSharedInstructions` (forbids duplicated commitlint markers); also `SharedGitInstructionsContainOneOperativeCommitlintPreflightBlock`.
- `references/Hexalith.AI.Tools/hexalith-llm-instructions.md` → `hexalith-git-instructions.md` -- shared commit-message policy home (read-only for this closure).
- `_bmad-output/implementation-artifacts/spec-resolve-main-rebase-conflicts.md` -- pattern for no-code obsolete closure via renegotiated frozen intent + `status: done`.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/spec-gh-29195132839-fix-tier-1-doc-contracts.md` -- after approval, set `status: done` and leave frozen intent locked -- closes the obsolete restore story without code mutation.
- [x] Working tree -- verify entry points lack Release Package Inventory and lack embedded commitlint markers, while inventory docs and shared git instructions remain the live homes -- proves supersession premises hold at execution time.
- [x] `tests/Hexalith.EventStore.Contracts.Tests` packaging methods -- confirm `CopilotInstructionsDelegateCommitlintContractToSharedInstructions` and `Active_package_inventory_docs_match_manifest_package_set` still exist and the deleted embed method name is absent from test sources -- proves the July verification surface is obsolete.

**Acceptance Criteria:**
- Given the approved obsolete-closure intent, when implementation finishes, then this spec is `done` and no entry-point inventory or embedded commitlint restore is applied.
- Given tip inspection, when the three agent entry points are compared, then they remain identical shared baselines with no repository-specific package inventory block.
- Given packaging contract homes, when inventory docs and shared git instructions are checked, then they remain the authoritative surfaces named in Code Map without edits from this work.

## Spec Change Log

- **Review loop 1:** Adversarial review found that the submodule-safeguard verification command contained a literal placeholder, the claimed fresh build did not explicitly clean outputs, and diff-scope evidence conflated failed-run commit `471ca867` with implementation baseline `7eb975e0`. Verification now uses literal fail-closed safeguard checks, an explicit clean-before-build sequence, and separate exact path-set checks for both baselines. This avoids irreproducible evidence and ambiguous scope. **KEEP:** preserve the exact pre-`322e3193` documentation blocks, all current submodule guidance, the unchanged tests and manifest, the 694-test CI-equivalent lane, and the four-file implementation boundary.
- **Review loop 2 / human [A]:** Adversarial review found frozen Intent required entry-point inventory + embedded commitlint while HEAD shared baselines and live packaging tests forbid that placement. Human chose supersede/close as obsolete. Replaced frozen Intent with no-code closure; current homes are docs + shared git instructions. Avoids fighting Shared Entry Points and live delegation tests. **KEEP:** do not restore July entry-point blocks; do not weaken packaging tests; leave Hexalith.AI.Tools and inventory docs untouched.
- **Review loop 3 patches:** Closure review found Verification checked inventory count only (not every package ID), omitted embed-marker and entry-point identity checks, and left Review loop 1 KEEP readable as still operative. Verification now asserts package IDs, forbidden embed markers, identical entry points, shared git policy presence, and `main`/diff-scope gates. Design Notes mark Review loop 1 KEEP as superseded by Review loop 2. Avoids false-green closure and accidental July restore. **KEEP:** obsolete-closure frozen Intent; docs + Hexalith.AI.Tools as live homes; no entry-point restore.

## Design Notes

The original July restore briefly put inventory and commitlint text back into the entry points, then later commits synchronized those files to the location-independent shared baseline and moved contracts to docs / Hexalith.AI.Tools. Replaying the July restore would fail `CopilotInstructionsDelegateCommitlintContractToSharedInstructions` and violate Shared Entry Points. Closure is documentation-only against this spec.

**KEEP supersession:** Review loop 1 KEEP (preserve pre-`322e3193` entry-point blocks / four-file restore boundary) is historical and **superseded** by Review loop 2 / human `[A]` obsolete-closure KEEP. Do not re-apply the July entry-point restore.

## Verification

**Commands:**
- `git branch --show-current` -- expected: `main`.
- `rg -n "Release Package Inventory|14 packages" AGENTS.md CLAUDE.md .github/copilot-instructions.md` -- expected: no matches in the three entry points.
- `rg -n "@commitlint/config-conventional|Start the description with a lowercase letter|near 50 characters|Choose the type by release impact" AGENTS.md CLAUDE.md .github/copilot-instructions.md` -- expected: no matches (no embedded commitlint markers).
- `cmp -s AGENTS.md CLAUDE.md && cmp -s AGENTS.md .github/copilot-instructions.md` -- expected: exit 0 (identical shared baselines).
- `rg -n "CopilotInstructionsExposeTheCommitlintContractDirectly|CopilotInstructionsDelegateCommitlintContractToSharedInstructions|Active_package_inventory_docs_match_manifest_package_set|SharedGitInstructionsContainOneOperativeCommitlintPreflightBlock" tests/Hexalith.EventStore.Contracts.Tests/Packaging` -- expected: embed method absent; delegation, inventory-doc, and shared-preflight methods present.
- `python3 - <<'PY'
import json, pathlib
root = pathlib.Path('.')
pkgs = [p['id'] for p in json.loads((root/'tools/release-packages.json').read_text())['packages']]
docs = [
    root/'docs/reference/nuget-packages.md',
    root/'docs/brownfield/project-overview.md',
    root/'docs/brownfield/architecture.md',
    root/'_bmad-output/project-context.md',
]
assert len(pkgs) == 14
for doc in docs:
    text = doc.read_text()
    assert '14 packages' in text, doc
    for pkg in pkgs:
        assert pkg in text, (doc, pkg)
print('inventory-docs-ok', len(pkgs))
PY` -- expected: prints `inventory-docs-ok 14`.
- `rg -n "Conventional Commits|Never use the \`chore\` type|<type>\\[optional scope\\]\\[!\\]: <description>" references/Hexalith.AI.Tools/hexalith-git-instructions.md` -- expected: shared git policy markers present.
- `git diff --name-only` -- expected: only this spec path changed by the closure work.

**Manual checks (if no CLI):**
- After final present/accept, frontmatter `status: done` and frozen Intent still describe obsolete closure only.

## Suggested Review Order

- Frozen obsolete-closure Intent: no entry-point restore; docs + shared git homes win.
  [`spec-gh-29195132839-fix-tier-1-doc-contracts.md:14`](spec-gh-29195132839-fix-tier-1-doc-contracts.md#L14)

- Code Map names live inventory docs and packaging delegation methods.
  [`spec-gh-29195132839-fix-tier-1-doc-contracts.md:38`](spec-gh-29195132839-fix-tier-1-doc-contracts.md#L38)

- Spec Change Log and Design Notes supersede Review loop 1 KEEP.
  [`spec-gh-29195132839-fix-tier-1-doc-contracts.md:60`](spec-gh-29195132839-fix-tier-1-doc-contracts.md#L60)

- Strengthened Verification: IDs, embed markers, identity, main scope.
  [`spec-gh-29195132839-fix-tier-1-doc-contracts.md:72`](spec-gh-29195132839-fix-tier-1-doc-contracts.md#L72)
