---
title: 'Remove disposable job workspaces on exit'
type: 'chore'
created: '2026-11-14'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Ad hoc build and test jobs leave temporary repository copies and build outputs in `/tmp`. The user approved immediate job cleanup plus hourly system cleanup with 24-hour retention.

**Approach:** Provide `scripts/run-with-temp.sh` and install it as the local `run-with-temp` command. Give each invocation a private scratch directory, set `TMPDIR`, `TMP`, and `TEMP` for its children, preserve command arguments and exit status, and remove that directory when the invocation exits. Document that disposable checkouts belong inside the supplied directory, useful logs belong outside it, and child processes must finish before the command exits. Add the usage rule to local Codex instructions so future ad hoc jobs use the wrapper. Existing repository runners already own their primary scratch cleanup and retained evidence must remain intact.

</frozen-after-approval>

## Implementation Notes

- Host configuration installed separately: `/etc/tmpfiles.d/tmp.conf` changes `/tmp` retention from `10d` to `1d` and retains `/var/tmp` at `30d`; the existing cleanup timer now runs every hour. Unit verification and effective-configuration checks passed.
- Keep the wrapper small; validate real success, failure, arguments, environment inheritance, cancellation, and scratch cleanup without running application builds or changing dependencies.
- Added `scripts/run-with-temp.sh`, installed as `/home/administrator/.local/bin/run-with-temp`, and added the usage rule to `/home/administrator/.codex/AGENTS.md` for future ad hoc jobs. The wrapper holds a directory lock so the system's parent-directory aging policy preserves active jobs.
- The eight focused tests cover isolation, exact arguments, success/failure statuses, read-only directory cleanup, process-group and wrapper-PID cancellation, lock exclusion, and real systemd cleanup of an abandoned sibling. The contracts job in `.github/workflows/ci.yml` now runs the suite through the wrapper.
- Validation: `bash -n scripts/run-with-temp.sh` and `bash scripts/run-with-temp.sh python3 -B -m unittest discover -s tools/tests -p 'test_run_with_temp.py' -v` passed (8 tests). The new cancellation and permission cases failed before the fixes and passed afterward. The host cleanup service completed successfully after the hourly timer restart.

## Review Triage Log

- **medium, patched:** PID-only cancellation left the foreground child running. Reproduced with both SIGTERM and SIGINT. The wrapper now starts a separate process group, resets inherited signal handling, forwards cancellation, waits for the command, and returns 143 or 130; regression tests assert the child exits and scratch is removed.
- **medium, patched:** Owned read-only directories defeated removal and changed exit 23 to 1. Reproduced for successful and failed commands. Cleanup now retries after restoring directory permissions only, preserving hard-linked file permissions and the original failed-command status.
- **medium, patched:** The new suite had no automatic CI invocation. Existing workflows enumerate specific Python test files. Added a focused invocation to the existing Linux contracts job.
