---
title: 'Confirm the commitlint CI repair'
type: 'bugfix'
created: '2026-07-19'
status: 'done'
review_loop_iteration: 0
baseline_commit: '67a9e00efaf397a31669f65df7008f671d20e06a'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-gh-29682903822-fix-ci-cd.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions run `29682903822` failed its Tier 1 contract suite because `commitlint.config.mjs` contained policy relaxations that contradicted the repository's exact commit-message contract. A corrective commit, `d046120f`, is already on `main`; creating another implementation change or rerunning the known-bad SHA would duplicate completed work without addressing a new failure.

**Approach:** Verify that the existing corrective commit restores the strict three-line commitlint configuration, passes the focused contract locally, and closes the failure in replacement CI run `29683585653`. Treat a successful replacement run as completion; only plan a further code change if that run exposes a distinct, reproducible failure.

## Boundaries & Constraints

**Always:** Preserve the exact `@commitlint/config-conventional` policy, LF content required by the existing contract, the current Tier 1 topology, and the user's unrelated `references/Hexalith.FrontComposer` worktree change. Bind external evidence to corrective SHA `d046120fb4178252a0e0f300d714bac70018e9f7`.

**Ask First:** Any new source, test, workflow, contributor-policy, submodule, commit, push, or workflow-rerun change; any attempt to address deferred commit-policy inconsistencies outside this failed-run repair.

**Never:** Rerun failed run `29682903822` at its unchanged SHA; relax commitlint rules; update the regression test to accept the copied overrides; rewrite published history; modify, stage, or discard the pre-existing FrontComposer submodule change.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Known-bad run | Run `29682903822` at `dc058f86` | Evidence identifies the exact config mismatch and failing contract | Do not rerun an unchanged deterministic failure |
| Corrective head | `d046120f` with strict three-line config | Focused policy contract and commitlint behavior pass | Stop before edits if local content differs from the pushed SHA |
| Replacement CI | Run `29683585653` for `d046120f` | Tier 1 and overall CI conclude successfully | If failure is distinct, capture its exact job and logs before proposing scope |

</frozen-after-approval>

## Code Map

- `commitlint.config.mjs` -- corrected root policy; `d046120f` removed the ten copied override/comment lines.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` -- regression contract whose line 205 detected the drift.
- `.github/workflows/ci.yml` -- read-only CI caller; no workflow edit is expected for this repair.
- `_bmad-output/implementation-artifacts/spec-gh-29682903822-fix-ci-cd.md` -- completed implementation and local verification record for the corrective commit.

## Tasks & Acceptance

**Execution:**
- [x] `commitlint.config.mjs` -- verify the working file is LF-only, exactly matches the strict contract, and is identical to corrective commit `d046120f`; make no implementation edit when these checks pass.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` -- build the Contracts test project and run the focused policy test to independently confirm the repaired behavior.
- [x] `.github/workflows/ci.yml` -- inspect replacement run `29683585653`, bind it to `d046120f`, and record its final conclusion without rerunning or modifying workflows.

**Acceptance Criteria:**
- Given corrective SHA `d046120f`, when the config and focused contract are verified, then the exact three-line LF policy is present and the focused test passes.
- Given replacement run `29683585653`, when its final jobs are inspected, then `ci / build-and-test`, including `Unit tests (Tier 1, VSTest)`, and the overall run succeed.
- Given the user-approved dirty worktree, when final scope is inspected, then `references/Hexalith.FrontComposer` is unchanged by this task and no new implementation diff exists.

## Spec Change Log

## Design Notes

The failed run is immutable evidence for an older SHA. The meaningful closure signal is the replacement run for the corrective SHA, not a rerun of the same known-bad commit. This keeps the repair idempotent and avoids manufacturing a second code change after the root cause has already been removed.

## Verification

**Commands:**

```bash
task_known_bad_run_json="$(gh run view 29682903822 \
  --repo Hexalith/Hexalith.EventStore \
  --json headSha,status,conclusion,jobs,url)" &&
jq -e --arg expected_sha 'dc058f869818632601bea9995efad74738fbad3d' '
  .headSha == $expected_sha and
  .status == "completed" and
  .conclusion == "failure" and
  any(.jobs[];
    .databaseId == 88182122357 and
    .name == "ci / build-and-test" and
    .conclusion == "failure")
' <<< "$task_known_bad_run_json" >/dev/null &&
task_known_bad_log="$(gh api \
  repos/Hexalith/Hexalith.EventStore/actions/jobs/88182122357/logs)" &&
grep -F 'Hexalith.EventStore.Contracts.Tests.Packaging.CommitMessagePolicyTests.CommitMessageHookExecutesCommitlintAndRemainsUnixCompatible' \
  <<< "$task_known_bad_log" &&
grep -F 'commitlintConfig' <<< "$task_known_bad_log" &&
grep -F 'should be' <<< "$task_known_bad_log" &&
grep -F '// Sprint Change Proposal 2026-07-05 (CI Package Boundary / Fluent Pin):' \
  <<< "$task_known_bad_log" &&
grep -F "'subject-case': [0]," <<< "$task_known_bad_log"
```

Expected: run `29682903822` is the completed failed run for exact known-bad SHA
`dc058f869818632601bea9995efad74738fbad3d`, failed job `88182122357` is `ci / build-and-test`, and
its logs contain the exact failing policy-contract method plus the expected/actual commitlint mismatch.

- `git show d046120fb4178252a0e0f300d714bac70018e9f7:commitlint.config.mjs | cmp -s - commitlint.config.mjs && test "$(wc -l < commitlint.config.mjs)" -eq 3 && ! LC_ALL=C grep -q $'\r' commitlint.config.mjs` -- expected: exact corrective content, three LF-only lines.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -warnaserror -m:1 -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method '*Packaging.CommitMessagePolicyTests.CommitMessageHookExecutesCommitlintAndRemainsUnixCompatible'` -- expected: 1/1 passed.

```bash
task_replacement_run_json="$(gh run view 29683585653 \
  --repo Hexalith/Hexalith.EventStore \
  --json headSha,status,conclusion,jobs,url)" &&
jq -er --arg expected_sha 'd046120fb4178252a0e0f300d714bac70018e9f7' '
  select(
    .headSha == $expected_sha and
    .status == "completed" and
    .conclusion == "success" and
    any(.jobs[];
      .databaseId == 88183958245 and
      .name == "ci / build-and-test" and
      .status == "completed" and
      .conclusion == "success" and
      any(.steps[];
        .name == "Unit tests (Tier 1, VSTest)" and
        .status == "completed" and
        .conclusion == "success"))
  ) |
  .url
' <<< "$task_replacement_run_json"
```

Expected: run `29683585653` is bound to exact corrective SHA
`d046120fb4178252a0e0f300d714bac70018e9f7`, completed successfully, and job `88183958245`
completed successfully with a successful `Unit tests (Tier 1, VSTest)` step; the command prints the
run URL only after all assertions pass.

```bash
test "$(git rev-parse HEAD)" = '67a9e00efaf397a31669f65df7008f671d20e06a' &&
test "$(git -C references/Hexalith.Builds rev-parse HEAD)" = \
  '4bbe7c04eb901050ee84075f0c8ad225fcc5fefe' &&
test "$(git -C references/Hexalith.FrontComposer rev-parse HEAD)" = \
  'a56c6f626c62d906cf7dac4be0d6800cd2f348b8' &&
test "$(git diff --name-only)" = \
  $'_bmad-output/implementation-artifacts/deferred-work.md\nreferences/Hexalith.Builds\nreferences/Hexalith.FrontComposer' &&
test "$(git ls-files --others --exclude-standard)" = \
  '_bmad-output/implementation-artifacts/spec-gh-29682903822-fix-ci-cd-2.md' &&
git status --short --branch &&
git diff --check
```

Expected: local release HEAD and both externally introduced submodule heads match exactly, tracked and
untracked paths equal their explicit allowlists (including the review-owned deferred ledger), and no
tracked whitespace error exists.

**Results (2026-07-19):**

- `commitlint.config.mjs` matched corrective commit `d046120fb4178252a0e0f300d714bac70018e9f7`, contained exactly three LF-only lines, and required no implementation edit.
- The focused Contracts project Release build and 1/1 `CommitMessageHookExecutesCommitlintAndRemainsUnixCompatible` result ran locally at release HEAD `67a9e00efaf397a31669f65df7008f671d20e06a`, with externally introduced submodule dirt present; these results are supporting smoke evidence rather than the authoritative corrective-head proof.
- The authoritative evidence is replacement run [29683585653](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29683585653) at exact corrective SHA `d046120fb4178252a0e0f300d714bac70018e9f7`. Job [`ci / build-and-test` (`88183958245`)](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29683585653/job/88183958245), including `Unit tests (Tier 1, VSTest)`, and `ci / tenants-source-mode` succeeded; Aspire and performance jobs were intentionally skipped.
- Final scope and whitespace checks passed at local release HEAD `67a9e00efaf397a31669f65df7008f671d20e06a`: unrelated `references/Hexalith.Builds` was at `4bbe7c04eb901050ee84075f0c8ad225fcc5fefe`, unrelated `references/Hexalith.FrontComposer` was at `a56c6f626c62d906cf7dac4be0d6800cd2f348b8`, the tracked diff and untracked path sets exactly matched their allowlists (including the review-owned deferred ledger), and no implementation diff or tracked whitespace error was present.
- Matrix audit passed: a successful fail-closed log assertion bound the known-bad config mismatch to `dc058f869818632601bea9995efad74738fbad3d` without rerunning it, the focused contract passed as local smoke evidence, and authoritative replacement CI passed against the exact corrective SHA.

## Suggested Review Order

**CI closure contract**

- Start with the verification-only intent and immutable corrective-SHA boundary.
  [`spec-gh-29682903822-fix-ci-cd-2.md:15`](spec-gh-29682903822-fix-ci-cd-2.md#L15)

- Confirm known-bad evidence is fail-closed without rerunning the failed SHA.
  [`spec-gh-29682903822-fix-ci-cd-2.md:68`](spec-gh-29682903822-fix-ci-cd-2.md#L68)

- Verify replacement CI binds success through the previously failing Tier 1 step.
  [`spec-gh-29682903822-fix-ci-cd-2.md:100`](spec-gh-29682903822-fix-ci-cd-2.md#L100)

**Workspace isolation**

- Check exact external-submodule and workflow-artifact allowlists before accepting scope.
  [`spec-gh-29682903822-fix-ci-cd-2.md:128`](spec-gh-29682903822-fix-ci-cd-2.md#L128)

- Review authoritative CI attribution separately from local supporting smoke evidence.
  [`spec-gh-29682903822-fix-ci-cd-2.md:146`](spec-gh-29682903822-fix-ci-cd-2.md#L146)

**Deferred follow-up**

- Keep unrelated default-version coverage out of this completed CI repair.
  [`deferred-work.md:403`](deferred-work.md#L403)
