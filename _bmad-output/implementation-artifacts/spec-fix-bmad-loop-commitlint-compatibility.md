---
title: 'Fix BMad-loop commitlint compatibility'
type: 'bugfix'
created: '2026-08-26'
status: 'done'
review_loop_iteration: 0
baseline_commit: 'f5bdd56f9490cad50c11d8989c7f1d5c66d05b54'
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `bmad-loop` 0.11.1 crashes after a successful deferred-work migration because its deterministic Python orchestrator commits with the hard-coded subject `chore(sweep): migrate legacy deferred-work entries to DW format`, while this repository's authoritative commitlint configuration rejects `chore`. Other orchestrator-owned bookkeeping paths use the same forbidden type and would fail later in the run.

**Approach:** Correct the installed orchestrator's internally generated bookkeeping subjects to use this repository's permitted `build` maintenance type, preserve their existing scopes and descriptions, and verify every resulting candidate with the repository-pinned commitlint CLI. Leave the sweep skill's no-commit boundary intact because the crash originates after the skill session has completed.

## Boundaries & Constraints

**Always:** Cover every deterministic `chore(...)` subject emitted by the installed orchestrator; preserve message scopes and descriptions; use the repository's pinned commitlint configuration as the acceptance authority; leave the existing FrontComposer gitlink change untouched.

**Ask First:** Upstream issue/PR creation, reinstalling or upgrading `bmad-loop`, changing repository commitlint rules, resuming the active sweep, or committing any repository files.

**Never:** Permit `chore`, bypass Git hooks, weaken commitlint, make migration agents commit, rewrite published history, or absorb unrelated working-tree changes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Migration commit | Valid migrated ledger | `build(sweep): migrate legacy deferred-work entries to DW format` passes commitlint | Commit hook remains authoritative |
| Later bookkeeping | Sweep, decision, deferred-work, sprint-status, or operator metadata commit | Existing scope/description is retained with `build` type | Candidate validation failure blocks handoff |
| Agent skill session | `bmad-loop-sweep` migration/triage | Skill continues to avoid Git mutations | Orchestrator retains commit ownership |

</frozen-after-approval>

## Code Map

- `/home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop/sweep.py` -- owns the crashing migration commit and all sweep-ledger bookkeeping subjects.
- `/home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop/engine.py` -- owns deferred-work carry/close and sprint-status bookkeeping subjects.
- `/home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop/decisions.py` -- commits persisted sweep pre-answers.
- `/home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop/cli.py` -- commits operator confirmations.
- `/home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop/verify.py` / `worktree_flow.py` -- adjacent bookkeeping-subject documentation in the currently installed release.
- `.agents/skills/bmad-loop-sweep/migration-mode.md` / `.claude/skills/bmad-loop-sweep/migration-mode.md` -- read-only contract evidence: the orchestrator, not the skill, owns migration commits.
- `commitlint.config.mjs` / `package.json` -- read-only repository authority and pinned validator.
- `.bmad-loop/runs/20260826-164204-469c/crash.txt` -- historical regression evidence; absent on the resumed run, so current verification uses the installed migration publication path instead.
- `_bmad-output/implementation-artifacts/evidence/bmad-loop-commitlint-compatibility/` -- retained hotfix diff, file hashes, exact commitlint candidates/results, and migration-subject execution evidence.

## Tasks & Acceptance

**Execution:**
- [x] Installed `bmad_loop` package -- replace every orchestrator-generated `chore(...)` bookkeeping type with `build(...)` without changing scopes or descriptions.
- [x] Installed `bmad_loop` package -- update adjacent behavior comments/docstrings that promise a `chore(...)` history subject so runtime documentation matches behavior.
- [x] Validation -- compile the patched Python modules, enumerate remaining production `chore(...)` candidates, and validate representative concrete candidates for every affected scope through pinned commitlint.

**Acceptance Criteria:**
- Given the previously crashing migration subject, when the patched orchestrator prepares it, then its exact `build(sweep)` replacement passes this repository's commitlint rules.
- Given any deterministic bookkeeping commit path in installed production code, when its candidate is inspected, then it does not emit the forbidden `chore` type.
- Given the BMad sweep skill contract, when migration runs, then the agent still never commits and the orchestrator remains the sole commit owner.

## Spec Change Log

- 2026-10-08: Resumed review found installed bmad-loop 0.12.0, sourced from `87e5687f05717f5503b696974220cf2a91399664`, had replaced the original hotfix. Restored the same type-only correction across every current production occurrence, including adjacent documentation in `verify.py` and `worktree_flow.py`. Preserved all scopes, descriptions, commit ownership, Git behavior, and unrelated workspace changes. Added current validation evidence without reinstalling the tool or resuming the sweep.

## Design Notes

The original environment hotfix targeted bmad-loop 0.11.1 from `bmad-code-org/bmad-loop` commit `a4ca93f`. This resumed run applies it to the already-installed 0.12.0 release from `87e5687f05717f5503b696974220cf2a91399664`; no dependency update was performed. A future tool reinstall can replace the hotfix; upstream coordination remains outside this request.

The installed Python sources shared hard links with uv's cache. Each corrected file was replaced atomically, preserving its permissions and leaving cached source bytes unchanged. Across six modules, all 32 replacements are confined to bookkeeping types and adjacent comments/docstrings. The review diff is the installed-package before/after diff; the repository's historical baseline does not version these external files.

## Verification

**Commands:**
- `python3 -m compileall -q /home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop` -- passed; the patched package compiles.
- `rg -n '\bchore(?:\(|:)' /home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop -g '*.py'` -- no matches (exit 1, the expected search result).
- `npx --no -- commitlint --edit <candidate-file> --verbose` -- all 14 unique, complete concrete candidates passed pinned CLI 21.1.0, covering all five affected scopes. Exact inputs, source locations, commands, and successful output are retained in `evidence/bmad-loop-commitlint-compatibility/commitlint-validation.json`.
- `/home/administrator/.local/share/uv/tools/bmad-loop/bin/python _bmad-output/implementation-artifacts/evidence/bmad-loop-commitlint-compatibility/check_migration_subject.py` -- passed; executed the installed migration publication path with a temporary ledger, captured its exact subject, and stopped before Git publication. Result retained in `evidence/bmad-loop-commitlint-compatibility/migration-subject-check.json`.
- `/home/administrator/.local/share/uv/tools/bmad-loop/bin/python _bmad-output/implementation-artifacts/evidence/bmad-loop-commitlint-compatibility/check_publication_subjects.py` -- passed; all 14 validated subjects reached the installed `verify.commit_paths` Git boundary unchanged, with every Git operation stubbed. Result retained in `publication-subjects-check.json`; this check does not execute hooks.
- `/home/administrator/.local/share/uv/tools/bmad-loop/bin/python _bmad-output/implementation-artifacts/evidence/bmad-loop-commitlint-compatibility/check_operator_subject.py` -- passed; executed installed `_land_confirmation` through the real `commit_paths` helper with board/record effects and Git operations stubbed. The observed operator subject passed pinned commitlint. Result retained in `operator-subject-check.json`.
- Byte and AST comparisons against the pre-edit snapshots -- passed; scopes, descriptions, and executable behavior beyond the type substitution are preserved. Cached source hashes still equal the before hashes.
- Both migration skill contracts still declare `Never commit — the orchestrator`; neither file was modified.
- `git diff --check -- _bmad-output/implementation-artifacts/spec-fix-bmad-loop-commitlint-compatibility.md _bmad-output/implementation-artifacts/deferred-work.md` -- passed after triage. No Git mutation or sweep resume is part of these checks.

## Review Triage Log

Review resumed with all three configured layers. Blind hunter's floor was five findings (`17.217 kB`, `min(floor(sqrt(17.217) + 1), 10) = 5`); edge-case hunter returned no findings. The verification-gap reviewer returned one regression gap. Each finding is recorded separately below.

| ID | Layer | Verdict | Evidence | Route / Resolution |
| --- | --- | --- | --- | --- |
| BH-1 | Blind hunter | medium | `verify.commit_paths` stages operands and delegates validation to Git hooks without its own commitlint preflight; an invalid dynamic subject can therefore fail after staging. The cached upstream helper already behaves this way, and this hotfix changes only its documentation. This run prevalidated every authored replacement and retained successful evidence before installing it. | defer — DW-539: pre-existing runtime preflight behavior; changing publication policy exceeds the type-only correction. |
| BH-2 | Blind hunter | medium | `sprintstatus.STORY_RE` accepts long slugs; the declared-ID closure subject with a 170-character slug is 217 characters. Replacing `chore` with `build` leaves the header length identical, so this is a pre-existing length failure rather than a regression. | defer — DW-540: header-length policy for dynamic bookkeeping identifiers; existing descriptions remain intact in this hotfix. |
| BH-3 | Blind hunter | medium | `Engine._base_commit_message` and `SweepEngine._commit_message` emit nonconventional story/sweep subjects when the template is empty. Both defaults predate this patch; this workspace currently specifies a nonempty `commit_message_template`, so its configured path avoids them. | defer — DW-541: pre-existing fallback templates; ordinary story/bundle subjects are outside the deterministic bookkeeping replacements. |
| BH-4 | Blind hunter | false | Installed distribution metadata still names upstream 0.12.0 and reinstalling can replace local edits, as the spec already acknowledges. The proposed missing reproducible patch is now retained as `evidence/bmad-loop-commitlint-compatibility/hotfix.diff`, with before/after hashes in `commitlint-validation.json`; no metadata claim says the upstream distribution includes the hotfix. | reject — retained patch and hashes disprove the claimed missing recovery artifact. |
| BH-5 | Blind hunter | medium | `_land_confirmation` catches `GitError` and still reports completion of its on-disk confirmation. The identical handler exists in the original cached source; remaining hook or repository failures can leave those records unpublished without a commit warning. The type replacement removes this request's known forbidden-type cause. | defer — DW-542: pre-existing error-reporting behavior in operator confirmation. |
| VG-1 | Verification gap | medium | Upstream confirmation tests assert only a substring, and the original local execution check covered migration, so neither exercised the new operator prefix. A source reversion in the confirmation generator would escape those specific checks. | patch — added durable `check_operator_subject.py`, which runs the installed generator through `commit_paths`, captures its exact Git subject, validates the observed candidate with pinned commitlint, and asserts the full expected subject. Passed without real Git mutations. |
