# Sprint Change Proposal — 2026-04-07 — Story 17-6 Backup Completion

## Section 1: Issue Summary

**Trigger:** Story 17-6 (Snapshot and Backup Subcommands) was marked `done` in sprint-status.yaml, but only the Snapshot half was committed and merged (PR #158). The 6 Backup sub-subcommands (7 source files + 5 test files) remained untracked on disk.

**Problem Statement:** The story was split across two functional areas (Snapshot + Backup). Only the Snapshot commands were staged/committed during the original feature branch work. The Backup files were created locally but never included in the commit. The `.gitignore` pattern `Backup*/` also silently excluded the directory until Jérôme fixed it in PR #178.

**Discovery:** During a routine `git status` check, 12 untracked files were found in the Backup CLI directories.

**Evidence:**
- `git status` shows 12 untracked Backup files (7 source + 5 tests)
- Source files compile on current `main` (0 errors)
- Test files had stale `using` import (`Hexalith.EventStore.Admin.Cli.Tests.Client` instead of `Hexalith.EventStore.Testing.Http`) due to MockHttpMessageHandler refactoring in PR #178/#180

---

## Section 2: Impact Analysis

### Epic Impact
- **Epic 17 (Admin CLI):** Story 17-6 was incomplete. No scope change — the work was planned, just not fully delivered.
- All other epics: No impact.

### Story Impact
- **Story 17-6:** Backup sub-subcommands missing from repository. Sprint-status was incorrectly `done`.

### Artifact Conflicts
- **PRD/Architecture/UX:** None.
- **Tests:** 5 test files needed `using` import fix to align with current test infrastructure.

### Technical Impact
- 7 source files added: BackupArguments, BackupList, BackupTrigger, BackupValidate, BackupRestore, BackupExportStream, BackupImportStream commands
- 5 test files added with corrected imports
- No changes to existing committed code

---

## Section 3: Recommended Approach

**Selected:** Direct Adjustment

**Rationale:**
- Source code compiles without modification
- Tests required only a `using` import fix (1 line per file)
- No behavioral impact on existing code
- Completes the originally planned story scope

**Effort:** Low
**Risk:** Low
**Timeline:** No impact — work was already done, just not committed

---

## Section 4: Detailed Change Proposals

### Change 1: Fix test imports (5 files)

**Files:** `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Backup/*.cs`

OLD: `using Hexalith.EventStore.Admin.Cli.Tests.Client;`
NEW: `using Hexalith.EventStore.Testing.Http;`

**Rationale:** `MockHttpMessageHandler` was refactored from a local test helper into the shared `Hexalith.EventStore.Testing` package. Committed Snapshot tests already use the new import.

### Change 2: Commit all 12 Backup files

- 7 source files in `src/Hexalith.EventStore.Admin.Cli/Commands/Backup/`
- 5 test files in `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Backup/`

---

## Section 5: Implementation Handoff

**Change Scope: Minor** — Direct commit and push.

**Status:** Import fix applied. Ready to commit.

**Note:** 13 pre-existing build errors exist in the CLI test project (`TestContext` from xUnit v3 migration). These are unrelated to Backup files and present on `main` before this change.

**Success Criteria:**
- All 12 Backup files committed and pushed
- No new build errors introduced
- Sprint-status.yaml remains correctly at `done` for story 17-6
