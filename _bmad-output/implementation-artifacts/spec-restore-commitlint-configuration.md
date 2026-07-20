---
title: 'Restore the canonical commitlint configuration'
type: 'bugfix'
created: '2026-07-20'
status: 'done'
review_loop_iteration: 2
baseline_commit: '5c123ccbce2515a618134382d6181c2ec1a5cbbf'
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The staged rename of `commitlint.config copy.mjs` to `commitlint.config.js` makes the new `.js` file shadow the repository's canonical `commitlint.config.mjs`. With the installed Node 26.4.0 and commitlint 21.1.0 loader path, the ESM default export in that `.js` file resolves as an empty configuration, so every commit fails with `Please add rules to your commitlint.config.js` even when its message is valid.

**Approach:** Remove the accidental copied configuration instead of activating it, retain `commitlint.config.mjs` as the only root `commitlint.config.*` file, and add a focused guardrail that prevents another shadow configuration from silently bypassing the canonical rules.

## Boundaries & Constraints

**Always:** Preserve the repository-pinned commitlint and Husky setup; keep `@commitlint/config-conventional` plus the canonical `type-enum` that excludes `chore`; preserve the user's other work and stage only files belonging to this fix; keep `commitlint.config.mjs` LF-normalized.

**Ask First:** Any relaxation of subject casing, body or header length, or allowed commit types; any dependency, hook, contributor-guidance, package-module-mode, or shared-instructions change; any commit or branch operation.

**Never:** Add a competing `.js`/`.cjs` configuration, set `package.json` to ESM merely to work around config loading, bypass Husky with `--no-verify`, weaken the no-`chore` policy, or edit the root-declared `references/Hexalith.AI.Tools` submodule.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Valid commit | `fix: restore commitlint configuration with updated rules` | Hook loads the canonical rules and exits successfully | A nonzero result blocks completion and reports the exact lint output |
| Forbidden type | `chore: restore commitlint configuration` | Hook rejects the message through `type-enum` | Test must prove copied relaxations did not replace strict policy |
| Shadow config | Any second root file matching `commitlint.config.*` | Focused policy test fails before the shadow reaches users | Failure identifies the competing configuration |

</frozen-after-approval>

## Code Map

- `commitlint.config copy.mjs` -- accidental tracked duplicate containing obsolete policy relaxations; its staged rename currently causes the failure.
- `commitlint.config.mjs` -- canonical working configuration with `config-conventional` and the repository's no-`chore` `type-enum`.
- `package.json` / root commitlint search places -- higher-precedence direct discovery sources and Cosmiconfig meta-configuration that must not redirect commitlint away from the canonical file.
- `.config/config.*` -- Cosmiconfig meta-files that can prepend arbitrary search locations before commitlint's tool-defined list and therefore must be absent.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` -- focused repository-policy tests; must validate the canonical file and reject every competing root discovery source supported by the pinned loader.
- `.husky/commit-msg` -- invokes repository-pinned commitlint with the proposed Git message file; no change expected.

## Tasks & Acceptance

**Execution:**
- [x] `commitlint.config copy.mjs` / `commitlint.config.js` -- replace the staged rename with deletion of the accidental copy so the canonical `.mjs` configuration is discoverable.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` -- assert that `commitlint.config.mjs` is authoritative: reject direct alternatives (`.commitlintrc*`, noncanonical `commitlint.config.*`, `package.yaml`, `package.json#commitlint`) and Cosmiconfig meta-sources (`package.json#cosmiconfig`, `.config/config.{json,yaml,yml,js,ts,cjs,mjs}`) that can prepend or replace search places.

**Acceptance Criteria:**
- Given dependencies are installed and the canonical configuration is the sole config, when the user's valid message reaches `.husky/commit-msg`, then commitlint exits successfully with non-empty conventional rules.
- Given repository policy forbids `chore`, when that type is linted after the fix, then commitlint rejects it through `type-enum`.
- Given any direct or meta-configuration source capable of taking precedence over the canonical file is introduced, including `.commitlintrc*`, a noncanonical `commitlint.config.*`, `package.yaml`, `package.json#commitlint`, `package.json#cosmiconfig`, or `.config/config.*`, when the focused policy test runs, then it fails and identifies the competing source.

## Spec Change Log

- Review loop 1: all three reviewers found that the first guard covered only `commitlint.config.*`, while commitlint searches higher-precedence `.commitlintrc*` and package metadata sources. The Code Map, test task, acceptance criteria, and verification scope now require coverage of every supported root discovery source, avoiding a green regression test while an alternate configuration silently shadows the canonical policy. KEEP: delete only the accidental copied config; leave `commitlint.config.mjs`, Husky, dependencies, and strict no-`chore` rules unchanged; preserve the valid-message, forbidden-type, clean-build, and focused-test verification.
- Review loop 2: all three reviewers found that direct-source coverage still missed Cosmiconfig 9 meta-configuration, which can prepend arbitrary search places through `package.json#cosmiconfig` or `.config/config.*`. The Code Map, task, acceptance criteria, and verification scope now explicitly reject those pinned meta-sources, avoiding a green guard while a custom search file shadows or removes the canonical policy. KEEP: retain every review-loop-1 direct-source check, actionable sorted conflict reporting, deletion of the accidental copy, the unchanged canonical/Husky/dependency policy, and all prior verification lanes.

## Design Notes

The `.mjs` extension is an existing compatibility boundary, not a cosmetic choice. The installed commitlint loader selects a synchronous `.js` loader on Node 26.4 because its version predicate compares both major and minor numbers; Node then exposes the ESM `.js` export through a module namespace that commitlint 21.1.0 does not unwrap. Keeping one explicit `.mjs` configuration avoids that upstream loader edge case and prevents policy precedence ambiguity.

## Verification

**Commands:**
- `git diff --cached --name-status && git diff --cached --check` -- expected: intended deletion/config-policy test changes only, with no whitespace errors.
- `npx --no -- commitlint --print-config` -- expected: `@commitlint/config-conventional`, non-empty rules, and strict `type-enum` without `chore`.
- Run the tracked hook against temporary valid and forbidden message files; expected: the valid hook exits 0 and the forbidden hook exits nonzero with `type-enum`:

  ```bash
  task_valid_commit_message="$(mktemp)"
  task_forbidden_commit_message="$(mktemp)"
  trap 'rm -f "$task_valid_commit_message" "$task_forbidden_commit_message"' EXIT
  printf '%s\n' 'fix: restore commitlint configuration with updated rules' > "$task_valid_commit_message"
  printf '%s\n' 'chore: restore commitlint configuration' > "$task_forbidden_commit_message"
  set +e
  task_valid_commit_output="$(.husky/commit-msg "$task_valid_commit_message" 2>&1)"
  task_valid_commit_status=$?
  task_forbidden_commit_output="$(.husky/commit-msg "$task_forbidden_commit_message" 2>&1)"
  task_forbidden_commit_status=$?
  set -e
  printf '%s\n' "$task_valid_commit_output"
  printf '%s\n' "$task_forbidden_commit_output"
  test "$task_valid_commit_status" -eq 0
  test "$task_forbidden_commit_status" -ne 0
  printf '%s\n' "$task_forbidden_commit_output" | rg 'type-enum'
  rm -f "$task_valid_commit_message" "$task_forbidden_commit_message"
  trap - EXIT
  ```

- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release --no-restore` followed by direct xUnit v3 execution with `-class Hexalith.EventStore.Contracts.Tests.Packaging.CommitMessagePolicyTests` -- expected: clean build and all focused policy tests pass, including direct and Cosmiconfig meta-source coverage.

## Suggested Review Order

**Intent and configuration authority**

- Start with the failure mechanism and why `.mjs` remains canonical.
  [`spec-restore-commitlint-configuration.md:16`](spec-restore-commitlint-configuration.md#L16)

- Reject every direct and meta discovery route that could shadow policy.
  [`CommitMessagePolicyTests.cs:237`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs#L237)

**Version boundary**

- Lock Cosmiconfig behavior to the discovery routes encoded by the guard.
  [`CommitMessagePolicyTests.cs:202`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs#L202)

**Verification**

- Exercise the real Husky hook for valid and forbidden commit messages.
  [`spec-restore-commitlint-configuration.md:72`](spec-restore-commitlint-configuration.md#L72)
