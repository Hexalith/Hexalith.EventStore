# Baseline Commit Capture

- Commit: `0776785f494fcefc8ad933b5b17b9c8d5cbe0513`
- Subject: `feat(server): recover committed events whose publication was never scheduled`
- Author date: `2026-08-07T22:27:17+02:00`
- Commit date: `2026-08-07T22:27:17+02:00`
- Story branch: `feat/story-4-5-append-durability-race-evidence`

The evidence directory is keyed to the exact baseline above. The implementation worktree still had that commit at `HEAD` when the capture was produced; no implementation commit was created as part of the evidence run.

Re-run from the repository root:

```bash
git show --no-patch --format='commit %H%nAuthorDate: %aI%nCommitDate: %cI%nSubject: %s' 0776785f494fcefc8ad933b5b17b9c8d5cbe0513
git rev-parse HEAD
git branch --show-current
```
