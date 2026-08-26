# Baseline Commit Capture

- Commit: `0776785f494fcefc8ad933b5b17b9c8d5cbe0513`
- Subject: `feat(server): recover committed events whose publication was never scheduled`
- Author date: `2026-08-07T22:27:17+02:00`
- Commit date: `2026-08-07T22:27:17+02:00`
- Story branch: `feat/story-4-5-append-durability-race-evidence`

The evidence directory is named for this baseline because that is the production revision AC6
asserts unchanged: no Story 4.5 change touches `src/` or `.github/`. The directory name is **not** a
claim that `HEAD` equals the baseline.

## Re-capture and re-seal, 2026-08-26

The original capture (2026-08-08) was produced with the worktree at the baseline. The packet was
re-captured and re-sealed on 2026-08-26 after three review loops, on `main`, which had advanced
through Stories 3.13-3.15 and 4.8-4.15. The re-capture therefore ran against a later `HEAD`, and
`source-state.md` — not the directory name — records the exact worktree inputs the re-captured run
used. Story 4.5's own commits after the baseline are `86308550` (shared with Story 4.4, whose `src/`
implementation it carries), `3e365150` and `3961bd72`; neither of the latter two touches `src/` or
`.github/`.

Re-run from the repository root:

```bash
git show --no-patch --format='commit %H%nAuthorDate: %aI%nCommitDate: %cI%nSubject: %s' 0776785f494fcefc8ad933b5b17b9c8d5cbe0513
git rev-parse HEAD
git branch --show-current
for sha in 3e365150 3961bd72; do git show --name-only --format= "$sha" -- src .github; done
git diff --name-only HEAD -- src .github
```

The last two commands must print nothing.
