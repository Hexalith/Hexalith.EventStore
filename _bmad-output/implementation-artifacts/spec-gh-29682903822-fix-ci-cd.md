---
title: 'Restore strict EventStore commitlint policy'
type: 'bugfix'
created: '2026-07-19'
status: 'done'
review_loop_iteration: 0
baseline_commit: 'dc058f869818632601bea9995efad74738fbad3d'
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-fix-copilot-commitlint-enforcement.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions run `29682903822`, job `88182122357`, fails after a successful Release build because `CommitMessageHookExecutesCommitlintAndRemainsUnixCompatible` detects that commit `3111a4bc` replaced EventStore's strict commitlint policy with FrontComposer-specific relaxations. The copied rules accept uppercase descriptions, allow oversized headers up to 150 characters, and contradict EventStore's frozen policy, contributor guidance, and shared Git instructions.

**Approach:** Restore `commitlint.config.mjs` to EventStore's exact three-line `@commitlint/config-conventional` configuration with LF line endings. Keep the existing contract test as the regression guard and leave workflows, shared build infrastructure, and published history unchanged.

## Boundaries & Constraints

**Always:** Preserve `@commitlint/config-conventional` as the sole rule authority; restore lowercase-description and 100-character-header enforcement; retain the existing Husky hook, semantic-release compatibility, and Tier 1 test topology; make only the root-repository config change required by the failure.

**Ask First:** Any proposal to relax commit-message policy, alter the existing contract test, change `.github/workflows/`, modify `references/Hexalith.Builds`, rewrite published commits, or expand the repair beyond the reported CI regression.

**Never:** Bless the copied `subject-case`, `body-max-line-length`, or 150-character-header overrides by updating the test; weaken or bypass CI/commit hooks; initialize nested submodules; modify pre-existing unrelated work; commit, push, or rerun external workflows without explicit authorization.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Repository policy contract | Three-line conventional config with LF | Focused Contracts test passes | Any content or line-ending drift fails the contract |
| Valid human commit | `fix(ci): restore strict commit policy` | Commitlint exits successfully | Unexpected rejection blocks completion |
| Uppercase description | `fix: Update status` | Commitlint rejects `subject-case` | Acceptance is a policy regression |
| Oversized header | Conventional header longer than 100 characters | Commitlint rejects `header-max-length` | Acceptance is a policy regression |
| Automated release | Existing semantic-release commit format | Existing release analysis remains compatible | Any regression requires human review before policy changes |

</frozen-after-approval>

## Code Map

- `commitlint.config.mjs` -- root policy file containing the copied relaxations; the only intended implementation edit.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` -- existing read-only regression guard whose line 205 exposed the mismatch.
- `CONTRIBUTING.md` -- read-only repository contract requiring lowercase descriptions and headers of at most 100 characters.
- `references/Hexalith.AI.Tools/hexalith-git-instructions.md` -- read-only shared Conventional Commit rules.
- `.github/workflows/ci.yml` -- read-only caller; restore, build, and package validation succeeded before the Tier 1 contract failure.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- workflow-owned review ledger for verified pre-existing policy and verification gaps; not part of the implementation.

## Tasks & Acceptance

**Execution:**
- [x] `commitlint.config.mjs` -- remove the copied repository-specific comments and rule overrides, restoring the exact strict three-line config with LF endings -- repairs the sole CI failure while preserving the approved EventStore policy.

**Acceptance Criteria:**
- Given the failing run's repository state, when the config is restored and the focused contract test executes, then it passes without any test or workflow modification.
- Given valid lowercase and invalid uppercase/oversized sample messages, when repository-pinned commitlint evaluates them, then the valid message passes and both invalid messages fail for the expected rules.
- Given the complete Contracts test project runs from a fresh Release build, when all cases finish, then no failures remain.
- Given the final diff is inspected, when scope is compared with the baseline, then the implementation changes only `commitlint.config.mjs`, while this spec and the required deferred-review ledger entries are the only workflow-owned artifact changes.

## Spec Change Log

## Design Notes

The exact existing contract is intentional rather than stale: EventStore's approved policy forbids weakening the default conventional rules. The copied relaxation came from another repository's local proposal and was added in a mixed commit without updating EventStore's policy artifacts. Restoring the config fixes both CI consistency and actual pre-commit enforcement; changing the test would merely hide the policy regression.

## Verification

**Commands:**

Verify the immutable baseline and repository-pinned commitlint version before interpreting any other result:

```bash
test "$(git rev-parse HEAD)" = 'dc058f869818632601bea9995efad74738fbad3d'
test "$(npx --no -- commitlint --version)" = '@commitlint/cli@21.1.0'
```

Expected: both checks exit zero; any baseline or resolved-tool drift blocks completion.

Build and run the complete Contracts project from a fresh isolated artifacts directory. The read-only
symlink mirror preserves repository-root discovery for repository contract tests while every generated
build artifact remains under the new `/tmp` directory:

```bash
task_isolated_root="$(mktemp -d /tmp/hexalith-eventstore-contracts.XXXXXX)"
while IFS= read -r task_repository_entry; do
  ln -s "$task_repository_entry" "$task_isolated_root/$(basename "$task_repository_entry")"
done < <(find "$PWD" -mindepth 1 -maxdepth 1 -print)
task_artifacts_path="$task_isolated_root/artifacts"
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj \
  --configuration Release -warnaserror -m:1 -p:MinVerVersionOverride=1.0.0 \
  --artifacts-path "$task_artifacts_path"
dotnet "$task_artifacts_path/bin/Hexalith.EventStore.Contracts.Tests/release/Hexalith.EventStore.Contracts.Tests.dll"
```

Expected: the fresh focused build reports zero warnings and errors and the complete Contracts project
reports 731 passed, zero failed, and zero skipped.

Run the focused regression from the ordinary Release output as a fast diagnostic:

```bash
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll \
  -method '*Packaging.CommitMessagePolicyTests.CommitMessageHookExecutesCommitlintAndRemainsUnixCompatible'
```

Expected: 1/1 passes.

Verify valid human and automated release messages remain accepted:

```bash
printf '%s\n' 'fix(ci): restore strict commit policy' | npx --no -- commitlint --verbose
printf '%s\n' 'chore(release): 3.50.4 [skip ci]' | npx --no -- commitlint --verbose
```

Expected: each command exits zero with `found 0 problems, 0 warnings`.

Verify invalid messages fail closed with the expected named rule rather than an arbitrary tool failure:

```bash
set +e
task_commitlint_output="$(printf '%s\n' 'fix: Update status' | npx --no -- commitlint --verbose 2>&1)"
task_commitlint_status=$?
set -e
printf '%s\n' "$task_commitlint_output"
test "$task_commitlint_status" -eq 1
grep -F '[subject-case]' <<< "$task_commitlint_output"

set +e
task_commitlint_output="$(printf '%s\n' 'fix: this lowercase description intentionally exceeds one hundred characters to verify the strict conventional commit header limit remains enforced' | npx --no -- commitlint --verbose 2>&1)"
task_commitlint_status=$?
set -e
printf '%s\n' "$task_commitlint_output"
test "$task_commitlint_status" -eq 1
grep -F '[header-max-length]' <<< "$task_commitlint_output"
```

Expected: the first probe reports `subject must not be sentence-case, start-case, pascal-case,
upper-case [subject-case]`; the 147-character second probe reports `header must not be longer than
100 characters, current length is 147 [header-max-length]`.

Verify exact content, line endings, known-good identity, and implementation scope:

```bash
test "$(wc -l < commitlint.config.mjs)" -eq 3
if LC_ALL=C grep -q $'\r' commitlint.config.mjs; then
  echo 'commitlint.config.mjs contains CR bytes' >&2
  exit 1
fi
git show 6d34a6b2b587db7c8714f20f045bd62682d157d6:commitlint.config.mjs | \
  cmp -s - commitlint.config.mjs
test "$(git diff --name-only)" = $'_bmad-output/implementation-artifacts/deferred-work.md\ncommitlint.config.mjs'
test "$(git ls-files --others --exclude-standard)" = \
  '_bmad-output/implementation-artifacts/spec-gh-29682903822-fix-ci-cd.md'
git diff --check
git diff -- commitlint.config.mjs
```

Expected: all assertions exit zero; the implementation diff only removes the copied comments and
three rule overrides, the deferred ledger contains review-routed pre-existing findings, and the only
untracked path is this workflow-owned spec artifact.

**Results (2026-07-19):**

- Focused Contracts Release build passed from fresh isolated artifacts with 0 warnings and 0 errors;
  the focused regression passed 1/1 and the complete Contracts project passed 731/731.
- Repository-pinned `@commitlint/cli@21.1.0` accepted the valid lowercase and existing
  semantic-release messages, rejected the uppercase description with `subject-case`, and rejected a
  147-character header with `header-max-length` at the 100-character limit.
- Baseline HEAD remained `dc058f869818632601bea9995efad74738fbad3d`.
  `commitlint.config.mjs` is exactly three LF-only lines and byte-identical to the last known-good
  `6d34a6b2b587db7c8714f20f045bd62682d157d6` version; `git diff --check` passed, the config is the
  only implementation change, the deferred ledger is the only additional tracked workflow artifact,
  this spec is the only untracked path, and no test, workflow, submodule, or Git-history change was made.

## Suggested Review Order

**Policy restoration**

- Restore repository-approved defaults instead of legitimizing copied rule relaxations.
  [`commitlint.config.mjs:1`](../../commitlint.config.mjs#L1)

- Approved intent fixes enforcement while keeping tests and workflows unchanged.
  [`spec-gh-29682903822-fix-ci-cd.md:15`](spec-gh-29682903822-fix-ci-cd.md#L15)

**Evidence and follow-up**

- Isolated artifacts and fail-closed probes make the repair reproducible.
  [`spec-gh-29682903822-fix-ci-cd.md:67`](spec-gh-29682903822-fix-ci-cd.md#L67)

- Deferred entries preserve hotfix scope while retaining verified follow-up risks.
  [`deferred-work.md:391`](deferred-work.md#L391)
