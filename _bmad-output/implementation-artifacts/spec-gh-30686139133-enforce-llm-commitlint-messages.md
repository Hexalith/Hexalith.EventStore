---
title: 'Enforce commitlint validation for LLM-authored Git messages'
type: 'bugfix'
created: '2026-08-01'
status: 'done'
review_loop_iteration: 1
baseline_commit: '4843b492dff7c16a4bc74db67509263f969c78c6'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions run 30686139133 rejected squash commit `4843b492dff7c16a4bc74db67509263f969c78c6`: PR #336 supplied branch-derived subject `Fix/story 3 11 validated catalog refresh (#336)`, which failed `type-empty` and `subject-empty`. The direct LLM entry points mention Conventional Commits only when a commit is requested, leaving PR titles and squash subjects implicit.

**Approach:** Add a synchronized, policy-neutral rule that treats every generated Git history subject, including prospective squash PR titles, as commitlint-gated. Add regression coverage preventing removal of this preflight or acceptance of generated defaults.

## Boundaries & Constraints

**Always:** Keep all three LLM entry points identical; keep repository configuration authoritative; validate the exact candidate before committing, creating a squashable PR, or merging; stop on failure; preserve unrelated worktree changes.

**Ask First:** Any submodule edit, commitlint configuration/dependency change, GitHub ruleset change, Git mutation, or published-history operation.

**Never:** Rewrite the failing commit; duplicate or weaken detailed policy; accept generated defaults without validation; bypass validation; modify unrelated BMAD artifacts.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Commit subject | LLM proposes a message | Validate the exact candidate before committing | Do not commit on failure |
| Squashable PR title | LLM proposes or receives a title | Validate it as a prospective commit subject | Replace and revalidate on failure |
| Branch-derived default | `Fix/story 3 11 validated catalog refresh` | Reject it | Produce an explicit compliant candidate |
| Validator unavailable | Commitlint cannot run | Report the exact blocker | Do not infer compliance |

</frozen-after-approval>

## Code Map

- `AGENTS.md:46` / `CLAUDE.md:46` / `.github/copilot-instructions.md:46` -- synchronized universal Git safeguards; extend together without repository-specific limits or type lists.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` -- instruction-delivery tests; assert one canonical operative preflight block inside the Git section rather than disconnected phrases.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/SharedInstructionEntryPointTests.cs:16` -- already proves the entry points remain identical; reuse its inventory unchanged rather than duplicating it.
- `commitlint.config.mjs:1` / `.husky/commit-msg:1` -- canonical rule engine and local commit gate; read-only, already reject the incident title.
- `.vscode/settings.json:2` -- already routes Copilot commit-message generation through the shared Copilot entry point; read-only.
- `references/Hexalith.AI.Tools/hexalith-git-instructions.md:113` -- detailed policy already exists transitively; read-only evidence.

## Tasks & Acceptance

**Execution:**
- [x] `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md` -- cover every history candidate an LLM authors, selects, receives, or uses, including existing live PR titles; draft replacements locally and validate them before any commit or PR create/update/merge mutation; reject generated defaults and fail closed.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` -- require exactly one canonical operative preflight block in the Git section of the normalized baseline; rely on the existing synchronization test to propagate it to all entry points and retain existing guards against detailed policy duplication.

**Acceptance Criteria:**
- Given an LLM authors a commit, merge, or squash subject or a squashable PR title, when it follows a direct entry point, then it must validate the exact candidate before mutation.
- Given a candidate is generated, rejected, or cannot be validated, when Git history is prepared, then instructions require replacement and revalidation or a stop.
- Given all entry points are inspected, when normalized text is compared, then they remain identical and delegate detailed policy to the canonical baseline.

## Spec Change Log

- Review loop 1: adversarial and edge-case review found that the first wording gated only LLM-authored titles, blurred local replacement with mutation of an existing PR, and used disconnected marker assertions that could survive semantically broken prose. Execution now covers every candidate the LLM authors, selects, receives, or uses; replacement is drafted and validated locally before live mutation; and the test requires one canonical operative block while reusing the existing entry-point synchronization guard. This avoids an invalid human/platform title escaping before merge and avoids phrase-soup false positives. KEEP: synchronized policy-neutral entry points, repository configuration authority, no submodule/configuration changes, real commitlint candidate verification, and untouched unrelated work.

## Design Notes

Encode the invariant and timing, not a second copy of allowed types, casing, or length rules. The canonical block must distinguish drafting a local replacement from mutating a live PR: validate first, then use the validated value. Assert that operative block inside the Git section; the existing synchronization test carries it to Claude and Copilot.

## Verification

**Commands:**
- `npx --no -- commitlint --verbose` with piped valid and incident-title candidates -- expected: a Conventional candidate passes and `Fix/story 3 11 validated catalog refresh` fails with `type-empty` and `subject-empty`.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release --no-restore` followed by direct xUnit v3 execution for `CommitMessagePolicyTests` and `SharedInstructionEntryPointTests` -- expected: focused policy tests pass.
- `git diff --check` -- expected: no whitespace errors in task-owned changes.

## Suggested Review Order

**Operative preflight**

- Defines fail-closed validation across commits, squashable PRs, and merges.
  [`AGENTS.md:59`](../../AGENTS.md#L59)

- Mirrors the canonical safeguard for Claude without repository-specific policy.
  [`CLAUDE.md:59`](../../CLAUDE.md#L59)

- Delivers the same safeguard to Copilot commit-message generation.
  [`copilot-instructions.md:59`](../../.github/copilot-instructions.md#L59)

**Regression coverage**

- Pins the complete operative block as one semantic unit.
  [`CommitMessagePolicyTests.cs:12`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs#L12)

- Requires exactly one block inside the normalized Git section.
  [`CommitMessagePolicyTests.cs:106`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs#L106)
