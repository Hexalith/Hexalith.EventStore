---
title: 'Fix Copilot commitlint instruction and enforcement gaps'
type: 'bugfix'
created: '2026-07-10'
status: 'done'
review_loop_iteration: 0
baseline_commit: '562205c0e218de8f68d85ab137668e14f0dc37ec'
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Copilot generated a commit header that violated `subject-case` and `header-max-length`, while the Copilot entry point used a broken relative link and the repository had no active local hook to reject the message before a direct push reached CI.

**Approach:** Make the critical commit contract self-contained in the Copilot entry point, explicitly connect VS Code commit generation to it, and install the repository-pinned commitlint through the established Husky `commit-msg` pattern. Document and test the complete path so lint rules, AI guidance, and local enforcement cannot silently diverge.

## Boundaries & Constraints

**Always:** Preserve `@commitlint/config-conventional` as the authority; require `<type>[scope][!]: <description>`, a lowercase description, and a header no longer than 100 characters; prefer a concise header near 50 characters; choose the type by release impact; keep hook files LF-only; use repository-pinned npm dependencies; add regression coverage for instruction discovery and hook wiring.

**Ask First:** Any change to the `references/Hexalith.AI.Tools` submodule, GitHub repository rulesets or branch protection, organization/user-level Copilot settings, or published Git history.

**Never:** Rewrite or force-push the already-published failing commit; weaken or duplicate commitlint rules; rely on probabilistic LLM compliance as the only gate; initialize nested submodules; add a second commit-message policy that conflicts with `commitlint.config.mjs`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Valid documentation commit | `docs: complete projection/query parity proof` | Copilot guidance permits it and the hook exits successfully | No error |
| Uppercase description | `fix: Update status` | Hook rejects it with `subject-case` | Commit is aborted before history changes |
| Oversized header | Conventional header longer than 100 characters | Hook rejects it with `header-max-length` | Commit is aborted before history changes |
| Automated release commit | `chore(release): 3.50.4 [skip ci]` | Existing semantic-release flow remains valid | Any regression fails focused policy validation |

</frozen-after-approval>

## Code Map

- `.github/copilot-instructions.md` -- Copilot repository-wide entry point; currently contains the broken `./references/...` link and no direct commit contract.
- `.vscode/settings.json` -- supported workspace location for the dedicated VS Code commit-message generation instruction setting.
- `package.json` / `package-lock.json` -- repository-pinned commitlint tooling and Husky lifecycle installation.
- `.husky/commit-msg` / `.gitattributes` -- pre-commit-history enforcement and cross-platform LF preservation; mirror the established FrontComposer pattern.
- `CONTRIBUTING.md` -- contributor setup and Conventional Commit workflow, currently describing only a “clear, descriptive” message.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` -- focused repository-policy regression coverage.

## Tasks & Acceptance

**Execution:**
- [x] `.github/copilot-instructions.md` -- correct the file-relative link to `../references/...` and inline concise, exact commit-generation requirements so Visual Studio and agent surfaces receive them without transitive lookup.
- [x] `.vscode/settings.json` -- point `github.copilot.chat.commitMessageGeneration.instructions` at the repository-wide Copilot instruction file so the Source Control smart action receives the same policy.
- [x] `package.json`, `package-lock.json`, `.husky/commit-msg`, `.gitattributes` -- add pinned Husky setup and invoke `npx --no -- commitlint --edit "$1"` from an LF-only hook.
- [x] `CONTRIBUTING.md` -- document `npm ci` hook installation, exact Conventional Commit constraints, and local validation commands.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` -- assert the corrected instruction link, direct policy text, VS Code wiring, Husky dependency/setup, executable hook contract, LF rule, and contributor guidance.

**Acceptance Criteria:**
- Given either supported Copilot IDE surface generates a commit message, when repository configuration is loaded, then it receives the same Conventional Commit, lowercase-description, and 100-character header contract enforced by commitlint.
- Given dependencies were installed with `npm ci`, when a developer attempts an invalid commit, then the tracked Husky hook rejects it before Git creates the commit.
- Given semantic-release creates its existing release commit format, when the hook runs, then the release commit remains accepted.
- Given any policy wiring is removed or regresses, when the focused Contracts test project runs, then the repository-policy test fails with an actionable message.

## Spec Change Log

## Design Notes

Husky is preferred over a documented-only `core.hooksPath` command because `npm ci` already installs repository Node tooling and the root-declared FrontComposer reference proves the same cross-platform pattern. Commitlint remains the sole rule engine; Copilot instructions summarize its contract but do not replace validation.

## Verification

**Commands:**
- `npm ci` -- expected: Husky installs without changing commitlint configuration and npm dependency integrity succeeds.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release` -- expected: all focused repository-policy and existing Contracts tests pass.
- Run `.husky/commit-msg` against temporary valid, uppercase-description, oversized-header, and release messages -- expected: valid/release pass; uppercase/oversized fail with their named commitlint rules.
- `git diff --check` -- expected: no whitespace errors and the hook is stored with LF line endings.

## Suggested Review Order

**Instruction delivery**

- Self-contained rules and the corrected shared-instruction link define Copilot's policy.
  [`copilot-instructions.md:9`](../../.github/copilot-instructions.md#L9)

- VS Code's commit smart action explicitly consumes the repository-wide policy.
  [`settings.json:2`](../../.vscode/settings.json#L2)

**Pre-commit enforcement**

- Supported Node versions and Husky installation make hook setup reproducible.
  [`package.json:6`](../../package.json#L6)

- The tracked hook delegates every proposed message to pinned commitlint.
  [`commit-msg:1`](../../.husky/commit-msg#L1)

- Contributor guidance distinguishes machine checks from human release-impact judgment.
  [`CONTRIBUTING.md:29`](../../CONTRIBUTING.md#L29)

**Regression guardrails**

- LF normalization preserves hook execution across Windows and Unix checkouts.
  [`.gitattributes:4`](../../.gitattributes#L4)

- Focused tests lock instruction discovery, tooling setup, hook integrity, and documentation.
  [`CommitMessagePolicyTests.cs:16`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs#L16)
