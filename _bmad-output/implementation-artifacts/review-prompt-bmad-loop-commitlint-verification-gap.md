Read `/home/administrator/projects/hexalith/eventstore/_bmad/render/bmad-build/eventstore-5ec6a32020fe/949c1652f308ba6a0e7e/review-prompts/verification-gap.md` completely and follow it as your review instructions.

Review content:

--- a/src/bmad_loop/sweep.py
+++ b/src/bmad_loop/sweep.py
@@ -590,7 +590,7 @@
             # a recovered bundle's ledger restore can leave the tree dirty, and
             # triage plus the first bundle baseline need a clean one. Guarded on
             # a non-empty pass so a fresh sweep never commits the user's dirt.
-            self._commit_ledger("chore(sweep): commit ledger after recovering in-flight bundles")
+            self._commit_ledger("build(sweep): commit ledger after recovering in-flight bundles")
         while True:
             # First statement of the loop body: covers the boundary right after
             # _finish_inflight_bundles on resume and between repeat cycles. A
@@ -637,7 +637,7 @@
                 return
             # a deferred bundle's ledger restore can leave the tree dirty; the
             # next cycle's triage and bundle baselines need a clean tree
-            self._commit_ledger("chore(sweep): commit ledger before next sweep cycle")
+            self._commit_ledger("build(sweep): commit ledger before next sweep cycle")
             cycle += 1
 
     def _finish_inflight_bundles(self) -> int:
@@ -753,7 +753,7 @@
         )
         if dropped:
             self.journal.append("decision-preanswers-pruned", dw_ids=dropped)
-            self._commit_ledger("chore(sweep): drop consumed deferred-work pre-answers")
+            self._commit_ledger("build(sweep): drop consumed deferred-work pre-answers")
 
     def _drive_story(self, task: StoryTask) -> None:
         # no spec-approval gate for bundles: the bundle intent came from the
@@ -1013,7 +1013,7 @@
                     json.dumps(result.result_json, indent=2), encoding="utf-8"
                 )
                 self._commit_ledger(
-                    "chore(sweep): migrate legacy deferred-work entries to DW format"
+                    "build(sweep): migrate legacy deferred-work entries to DW format"
                 )
                 post = deferredwork.parse_ledger(new_text)
                 self.journal.append(
@@ -1166,7 +1166,7 @@
                 closed.append(entry.id)
         if closed:
             self.journal.append("sweep-resolved-closed", dw_ids=closed)
-        self._commit_ledger("chore(sweep): close resolved deferred-work entries")
+        self._commit_ledger("build(sweep): close resolved deferred-work entries")
         self._emit("post_close_resolved")
         return len(closed)
 
@@ -1274,7 +1274,7 @@
                 answered_interactively = True
                 if option.effect == "close":
                     closed += 1
-        self._commit_ledger("chore(sweep): record deferred-work decisions")
+        self._commit_ledger("build(sweep): record deferred-work decisions")
         if answered_interactively:
             self._return_after_decisions()
         return answers, closed
@@ -1729,7 +1729,7 @@
             try:
                 verify.commit_paths(
                     self.paths.repo_root,
-                    f"chore(deferred-work): close {task.story_key}'s bundle ids",
+                    f"build(deferred-work): close {task.story_key}'s bundle ids",
                     [ledger],
                 )
             except verify.GitError as e:
--- a/src/bmad_loop/engine.py
+++ b/src/bmad_loop/engine.py
@@ -6119,7 +6119,7 @@
             try:
                 verify.commit_paths(
                     self.paths.repo_root,
-                    f"chore(deferred-work): carry harvested findings from {task.story_key}",
+                    f"build(deferred-work): carry harvested findings from {task.story_key}",
                     [ledger],
                 )
             except verify.GitError as e:
@@ -6197,7 +6197,7 @@
             try:
                 verify.commit_paths(
                     self.paths.repo_root,
-                    f"chore(deferred-work): carry {task.story_key}'s review follow-up",
+                    f"build(deferred-work): carry {task.story_key}'s review follow-up",
                     [ledger],
                 )
             except verify.GitError as e:
@@ -6268,7 +6268,7 @@
             try:
                 verify.commit_paths(
                     self.paths.repo_root,
-                    f"chore(deferred-work): close {task.story_key}'s declared ids",
+                    f"build(deferred-work): close {task.story_key}'s declared ids",
                     [ledger],
                 )
             except verify.GitError as e:
@@ -6592,7 +6592,7 @@
             try:
                 verify.commit_paths(
                     self.paths.repo_root,
-                    f"chore(sprint-status): carry {task.story_key} to {target}",
+                    f"build(sprint-status): carry {task.story_key} to {target}",
                     [board],
                 )
             except verify.GitError as e:
--- a/src/bmad_loop/decisions.py
+++ b/src/bmad_loop/decisions.py
@@ -185,7 +185,7 @@
         try:
             verify.commit_paths(
                 project,
-                f"chore(decisions): pre-answer {decision.id}",
+                f"build(decisions): pre-answer {decision.id}",
                 [ledger, store_path(project)],
             )
         except verify.GitError:
--- a/src/bmad_loop/cli.py
+++ b/src/bmad_loop/cli.py
@@ -3180,7 +3180,7 @@
     try:
         verify.commit_paths(
             paths.repo_root,
-            f"chore(operator): confirm {story.story_key}",
+            f"build(operator): confirm {story.story_key}",
             [spec, record] if board_ignored else [spec, paths.sprint_status, record],
         )
     except verify.GitError:
--- a/src/bmad_loop/verify.py
+++ b/src/bmad_loop/verify.py
@@ -2243,7 +2243,7 @@
     matter who wrote it. The run's carry bookkeeping passes the sprint board and the
     deferred-work ledger through that call — ``_carry_board_advance``
     unconditionally — so an operator's private unstaged edit to one of those files
-    lands in git history under a `chore(sprint-status): carry ...` message, leaving
+    lands in git history under a `build(sprint-status): carry ...` message, leaving
     the tree clean and no trace of the substitution. The blast radius is strictly
     SAME-PATH: `git commit -- <pathspec>` is implicitly `--only`, so dirt on any
     other path is never swept in, which is why this list is a set of exact paths and
--- a/src/bmad_loop/worktree_flow.py
+++ b/src/bmad_loop/worktree_flow.py
@@ -1790,7 +1790,7 @@
         ``repo``. That call stages by PATHSPEC — `git add -- :(literal)<path>` — so
         whatever the working tree holds at that path is committed no matter who wrote
         it, and a merge that walked past an operator's edit there hands the run its
-        own bytes to commit under a `chore(...)` message. The blast radius is strictly
+        own bytes to commit under a `build(...)` message. The blast radius is strictly
         same-path (`git commit -- <pathspec>` is implicitly `--only`), which is why
         this is an exact path set and not a policy.
 
@@ -1815,7 +1815,7 @@
         TRACKED ONLY, and that is the whole boundary of the hazard rather than a
         precaution. What makes the carry dangerous is committing a DIVERGENCE from a
         baseline somebody else authored: on a tracked board, an operator's local
-        reopen of a story row rides out under `chore(sprint-status): carry ...` with
+        reopen of a story row rides out under `build(sprint-status): carry ...` with
         the tree left clean and nothing to read it back from. An UNTRACKED artifact
         has no such baseline — git reports the whole file as dirt because git has
         never seen it, the orchestrator has been reading that exact file as its own
--- /dev/null
+++ b/_bmad-output/implementation-artifacts/spec-fix-bmad-loop-commitlint-compatibility.md
@@ -0,0 +1,72 @@
+---
+title: 'Fix BMad-loop commitlint compatibility'
+type: 'bugfix'
+created: '2026-08-26'
+status: 'in-review'
+review_loop_iteration: 0
+baseline_commit: 'f5bdd56f9490cad50c11d8989c7f1d5c66d05b54'
+context:
+  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
+---
+
+<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">
+
+## Intent
+
+**Problem:** `bmad-loop` 0.11.1 crashes after a successful deferred-work migration because its deterministic Python orchestrator commits with the hard-coded subject `chore(sweep): migrate legacy deferred-work entries to DW format`, while this repository's authoritative commitlint configuration rejects `chore`. Other orchestrator-owned bookkeeping paths use the same forbidden type and would fail later in the run.
+
+**Approach:** Correct the installed orchestrator's internally generated bookkeeping subjects to use this repository's permitted `build` maintenance type, preserve their existing scopes and descriptions, and verify every resulting candidate with the repository-pinned commitlint CLI. Leave the sweep skill's no-commit boundary intact because the crash originates after the skill session has completed.
+
+## Boundaries & Constraints
+
+**Always:** Cover every deterministic `chore(...)` subject emitted by the installed orchestrator; preserve message scopes and descriptions; use the repository's pinned commitlint configuration as the acceptance authority; leave the existing FrontComposer gitlink change untouched.
+
+**Ask First:** Upstream issue/PR creation, reinstalling or upgrading `bmad-loop`, changing repository commitlint rules, resuming the active sweep, or committing any repository files.
+
+**Never:** Permit `chore`, bypass Git hooks, weaken commitlint, make migration agents commit, rewrite published history, or absorb unrelated working-tree changes.
+
+## I/O & Edge-Case Matrix
+
+| Scenario | Input / State | Expected Output / Behavior | Error Handling |
+|----------|---------------|---------------------------|----------------|
+| Migration commit | Valid migrated ledger | `build(sweep): migrate legacy deferred-work entries to DW format` passes commitlint | Commit hook remains authoritative |
+| Later bookkeeping | Sweep, decision, deferred-work, sprint-status, or operator metadata commit | Existing scope/description is retained with `build` type | Candidate validation failure blocks handoff |
+| Agent skill session | `bmad-loop-sweep` migration/triage | Skill continues to avoid Git mutations | Orchestrator retains commit ownership |
+
+</frozen-after-approval>
+
+## Code Map
+
+- `/home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop/sweep.py` -- owns the crashing migration commit and all sweep-ledger bookkeeping subjects.
+- `/home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop/engine.py` -- owns deferred-work carry/close and sprint-status bookkeeping subjects.
+- `/home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop/decisions.py` -- commits persisted sweep pre-answers.
+- `/home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop/cli.py` -- commits operator confirmations.
+- `.agents/skills/bmad-loop-sweep/migration-mode.md` / `.claude/skills/bmad-loop-sweep/migration-mode.md` -- read-only contract evidence: the orchestrator, not the skill, owns migration commits.
+- `commitlint.config.mjs` / `package.json` -- read-only repository authority and pinned validator.
+- `.bmad-loop/runs/20260826-164204-469c/crash.txt` -- read-only regression evidence containing the exact rejected candidate and hook output.
+
+## Tasks & Acceptance
+
+**Execution:**
+- [x] Installed `bmad_loop` package -- replace every orchestrator-generated `chore(...)` bookkeeping type with `build(...)` without changing scopes or descriptions.
+- [x] Installed `bmad_loop` package -- update adjacent behavior comments/docstrings that promise a `chore(...)` history subject so runtime documentation matches behavior.
+- [x] Validation -- compile the patched Python modules, enumerate remaining production `chore(...)` candidates, and validate representative concrete candidates for every affected scope through pinned commitlint.
+
+**Acceptance Criteria:**
+- Given the previously crashing migration subject, when the patched orchestrator prepares it, then its exact `build(sweep)` replacement passes this repository's commitlint rules.
+- Given any deterministic bookkeeping commit path in installed production code, when its candidate is inspected, then it does not emit the forbidden `chore` type.
+- Given the BMad sweep skill contract, when migration runs, then the agent still never commits and the orchestrator remains the sole commit owner.
+
+## Spec Change Log
+
+## Design Notes
+
+This is an environment hotfix against the installed wheel sourced from `bmad-code-org/bmad-loop` commit `a4ca93f`. Upstream `main` still contains the same hard-coded subjects, so a future tool reinstall can replace the hotfix; upstream coordination is intentionally outside this request.
+
+## Verification
+
+**Commands:**
+- `python3 -m compileall -q /home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop` -- expected: patched modules compile. (`python` was unavailable on PATH; `python3` completed the equivalent check.)
+- `rg -n '\bchore(?:\(|:)' /home/administrator/.local/share/uv/tools/bmad-loop/lib/python3.11/site-packages/bmad_loop -g '*.py'` -- expected: no production-generated bookkeeping candidate remains; fixtures/examples may be reviewed separately.
+- Pipe representative `build(sweep)`, `build(deferred-work)`, `build(sprint-status)`, `build(decisions)`, and `build(operator)` subjects to `npx --no -- commitlint --verbose` -- expected: every exact candidate passes.
+- `git diff --check` -- expected: no whitespace errors in the task-owned spec; the pre-existing FrontComposer gitlink remains untouched.

Do not invoke any skill. If the instruction file is unreadable, report that exact failure and stop. Return only the review result.
