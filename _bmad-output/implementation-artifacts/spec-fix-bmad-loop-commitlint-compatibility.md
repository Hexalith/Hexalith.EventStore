---
title: 'Fix BMad-loop commitlint compatibility'
type: 'bugfix'
created: '2026-08-26'
status: 'in-review'
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
- `.agents/skills/bmad-loop-sweep/migration-mode.md` / `.claude/skills/bmad-loop-sweep/migration-mode.md` -- read-only contract evidence: the orchestrator, not the skill, owns migration commits.
- `commitlint.config.mjs` / `package.json` -- read-only repository authority and pinned validator.
- `.bmad-loop/runs/20260826-164204-469c/crash.txt` -- read-only regression evidence containing the exact rejected candidate and hook output.

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

## Design Notes

This is an environment hotfix against the installed wheel sourced from `bmad-code-org/bmad-loop` commit `a4ca93f`. Upstream `main` still contains the same hard-coded subjects, so a future tool reinstall can replace the hotfix; upstream coordination is intentionally outside this request.

## Verification

**Commands:**
- `python3 -m compileall -q /home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop` -- expected: patched modules compile. (`python` was unavailable on PATH; `python3` completed the equivalent check.)
- `rg -n '\bchore(?:\(|:)' /home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop -g '*.py'` -- expected: no production-generated bookkeeping candidate remains; fixtures/examples may be reviewed separately.
- Pipe representative `build(sweep)`, `build(deferred-work)`, `build(sprint-status)`, `build(decisions)`, and `build(operator)` subjects to `npx --no -- commitlint --verbose` -- expected: every exact candidate passes.
- `git diff --check` -- expected: no whitespace errors in the task-owned spec; the pre-existing FrontComposer gitlink remains untouched.
