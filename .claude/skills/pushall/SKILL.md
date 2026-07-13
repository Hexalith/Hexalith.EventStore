---
name: pushall
description: Commit, merge all local branches into the default branch, push, and prune stale branches/refs across the EventStore repository and every root-declared submodule. Only invoke this directly with /pushall — never trigger it automatically.
disable-model-invocation: true
allowed-tools: Bash(git *)
---

# /pushall — sync, merge, and prune everywhere

Runs the same repo-sync procedure in every root-declared submodule (leaf-first), then in the
superproject last, so the superproject commit picks up the updated submodule pointers.

## Hard rules (never deviate)

- Only operate on submodules listed in the root `.gitmodules` (paths under `references/...`).
  Do not run recursive submodule commands and do not initialize nested submodules. If any nested
  submodule is initialized by accident, deinitialize it before continuing.
- Never force-push (`--force` / `--force-with-lease`) and never delete a remote branch
  (`git push origin --delete`).
- Only delete local branches with `git branch -d` (safe delete, refuses non-merged branches).
  Never use `-D`.
- On a merge conflict: run `git merge --abort` immediately, leave that branch un-merged, record it
  as skipped, and move on to the next branch/repo. Never auto-resolve with `-X ours`/`-X theirs`
  and never hand-edit conflict markers to force a resolution.
- If a repo has no remote, push fails, or the local default branch has diverged from
  `origin/<default-branch>` (fast-forward not possible), record it as skipped/failed and continue
  with the rest — never stop the whole run for one repo.
- Always end each repo back on its default branch, never detached HEAD or a feature branch.
- Give a one-line progress update per repo as you go, and a final consolidated report (per repo:
  branches merged, branches deleted, anything skipped/failed, push status).

## Per-repo procedure

Apply this to one repo directory `<dir>` at a time (a submodule path, or `.` for the superproject):

1. `git -C <dir> fetch --all --prune`
2. Determine the default branch: prefer `main`, else `master`, else
   `git -C <dir> remote show origin | sed -n 's/.*HEAD branch: //p'`.
3. If `git -C <dir> status --porcelain` is non-empty, stage and commit everything:
   `git -C <dir> add -A && git -C <dir> commit -m "chore: automated commit via /pushall"`.
4. `git -C <dir> checkout <default-branch>`
5. Try `git -C <dir> merge --ff-only origin/<default-branch>` to catch up with the remote first.
   If this fails because local and remote diverged, record it and skip the rest of this repo (do
   not force anything).
6. For every other local branch (`git -C <dir> branch --format='%(refname:short)'`, excluding the
   default branch):
   - `git -C <dir> merge --no-ff <branch> -m "chore: merge <branch> into <default-branch> via /pushall"`
   - On conflict: `git -C <dir> merge --abort`, record as skipped, continue to the next branch.
7. `git -C <dir> push origin <default-branch>`. Record failure and continue if it's rejected — do
   not force-push.
8. Delete local branches now fully merged into the default branch (excluding the default branch
   itself): `git -C <dir> branch --merged <default-branch>` minus the default branch, then
   `git -C <dir> branch -d <each>`.
9. `git -C <dir> fetch --prune` to drop stale remote-tracking refs.

## Execution order

1. Read the root `.gitmodules` for the declared submodule paths (`references/...`).
2. Run the per-repo procedure for each submodule path, in the order declared.
3. Run the per-repo procedure for the superproject (`.`). Its step 3 auto-commit will pick up the
   updated submodule gitlink pointers from step 2 along with any other superproject changes.
4. Print the consolidated report.
