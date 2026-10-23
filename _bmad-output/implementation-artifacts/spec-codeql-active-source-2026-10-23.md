---
title: 'Complete CodeQL analysis of active EventStore source'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 'fcce6e850a7a8bf509288f2c49cf2e0ab4851b67'
approval: 'User authorized CI repairs and release without human validation'
context: []
---

<frozen-after-approval reason="user-authorized CI repair">

## Intent

CodeQL discovers 28,389 tracked C# files, including 24,581 archived copies under `_bmad-output`, and spends at least 97 minutes extracting that archive. Allow the shared scanner workflow to accept an optional CodeQL configuration, then configure EventStore to exclude only `_bmad-output/**`. Preserve the shared workflow, build mode, security queries, permissions, and every active application/test source input. Preserve all archived evidence bytes and the independent secret scanner.

</frozen-after-approval>

## Implementation Notes

- Shared owner: `references/Hexalith.Builds/.github/workflows/codeql.yml`; add optional string `config-file` with empty default and forward it to CodeQL init. Other callers retain their default behavior.
- EventStore caller: `.github/workflows/codeql.yml`; select `.github/codeql/codeql-config.yml`, which excludes only `_bmad-output/**`.
- Update `docs/ci.md` to explain the archive exclusion. No runtime or test code changes are justified by the isolated Client failure: 49 focused cases and 5,000 repetitions passed; fresh exact-source CI will exercise the complete suite.
- GitHub documents C# path filtering for analysis without builds: https://docs.github.com/en/code-security/reference/code-scanning/workflow-configuration-options#specifying-directories-to-scan.
- Validate both workflows with actionlint, parse the configuration, check that all active tracked C# remains selected, run the existing security-workflow contract, and obtain a successful remote CodeQL run. Commit the shared-owner change before the parent gitlink update. Review both diffs before pushing.

- Local verification passed: actionlint on both workflows; parsed optional default and forwarding; configuration retains all 3,808 active tracked C# files and excludes 24,581 archived copies; existing security shared-caller contract passed (1/1). Full Client coverage suite passed (2,084 passed, 2 isolated-endpoint skips). Parent release task owns the fresh remote CI/CodeQL evidence and publication verification.

## Review Triage Log

| Finding | Verdict and evidence | Disposition |
|---|---|---|
| Existing shared-caller test does not lock configuration literals | low: a proposed string-level test would mirror this reversible configuration; actionlint, parsed boundary checks, and the actual CodeQL run verify the behavior without adding implementation-mirroring assertions | Rejected; no deferred work |
| Shared scanner documentation omits new optional input | low: the public workflow now accepts a caller configuration path and other modules need its resolution/default contract | Patched shared CI standards with default, relative path, and C# no-build scope |
