# Story 10.3: PR Template & Review Process

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a contributor submitting a pull request,
I want a PR template with a checklist so I know what's expected,
so that my PR meets quality standards and gets reviewed efficiently.

## Acceptance Criteria

1. **AC1 - PR Template File**: `.github/PULL_REQUEST_TEMPLATE.md` exists at the standard GitHub location with a structured markdown template that is automatically populated into the PR description body when a contributor opens a new pull request.

2. **AC2 - Description Section**: The template includes a `## Description` section prompting the contributor to describe what changes were made and why, with a placeholder for linking related issues (e.g., "Closes #issue-number").

3. **AC3 - Type of Change Section**: The template includes a `## Type of Change` section with checkboxes for: Bug fix, New feature/enhancement, Documentation update, Refactoring, CI/build configuration, Other.

4. **AC4 - Checklist Section**: The template includes a `## Checklist` section with the following checkbox items (from Architecture Decision D4):
   - Description of changes included above
   - Related issue linked (if applicable)
   - Markdown lint passes locally (`npx markdownlint-cli2 "docs/**/*.md" "README.md" "CONTRIBUTING.md" "CHANGELOG.md"`)
   - Links are not broken
   - `dotnet build` passes
   - `dotnet test` passes

5. **AC5 - Unified Review Process**: Documentation changes follow the same PR review process as code changes — no separate publishing workflow (NFR13, FR38). The template applies equally to code PRs and documentation PRs.

## Tasks / Subtasks

- [x] Task 1: Create `.github/PULL_REQUEST_TEMPLATE.md` (AC: 1, 2, 3, 4, 5)
  - [x] Create the file at `.github/PULL_REQUEST_TEMPLATE.md`
  - [x] Add `## Description` section with placeholder text prompting for: what changed, why, and related issue reference (`Closes #issue-number`)
  - [x] Add `## Type of Change` section with checkbox list: Bug fix, New feature/enhancement, Documentation update, Refactoring, CI/build configuration, Other
  - [x] Add `## Checklist` section with D4-specified items as checkboxes (see AC4)
  - [x] Add `## Additional Context` section (optional, for screenshots, notes, or migration instructions)
  - [x] Static validation complete; live GitHub PR preview verification pending after merge to default branch

- [x] Task 2: Verify template auto-population (AC: 1)
  - [x] Confirmed standard GitHub file location is correct; automatic population is expected once merged to default branch
  - [x] Note: Full auto-population testing happens after merge to `main`

## Dev Notes

### Architecture Compliance

- **Architecture Decision D4** governs this story: "Standard open-source GitHub community setup, aligned with PRD Journey 4 (Kenji Contributes)"
- D4 explicitly lists `.github/PULL_REQUEST_TEMPLATE.md` as a `[NEW]` deliverable
- D4 specifies the exact checklist items (description, related issue, markdown lint, links, dotnet build, dotnet test)
- This is a **documentation/config-only** story — no source code changes, no new packages, no CI changes

### PR Template Checklist Content (from D4 specification)

The architecture document prescribes these exact checklist items:

| Checklist Item | Category |
|---------------|----------|
| Description of changes | General |
| Related issue (if any) | General |
| Markdown lint passes locally | Docs |
| Links not broken | Docs |
| `dotnet build` passes | Code |
| `dotnet test` passes | Code |

### File Structure

```
.github/
├── ISSUE_TEMPLATE/
│   ├── 01-bug-report.yml       [EXISTS] Story 10-2
│   ├── 02-feature-request.yml  [EXISTS] Story 10-2
│   ├── 03-docs-improvement.yml [EXISTS] Story 10-2
│   └── config.yml              [EXISTS] Story 10-2
├── PULL_REQUEST_TEMPLATE.md    [NEW]    This story (10-3)
├── agents/                     [EXISTS] BMAD agents — DO NOT TOUCH
├── prompts/                    [EXISTS] BMAD prompts — DO NOT TOUCH
├── workflows/                  [EXISTS] GitHub workflows — DO NOT TOUCH
└── copilot-instructions.md     [EXISTS] — DO NOT TOUCH
```

### CRITICAL: Files NOT to Touch

- `.github/agents/` — BMAD agent files
- `.github/prompts/` — BMAD prompt files
- `.github/workflows/` — GitHub Actions workflows (Story 11-3)
- `.github/copilot-instructions.md` — Copilot config
- `.github/ISSUE_TEMPLATE/` — Already complete (Story 10-2)
- `CONTRIBUTING.md` — Already complete (Story 10-1)
- `CODE_OF_CONDUCT.md` — Already complete (Story 10-1)
- Any `src/` or `tests/` files — this is config-only

### Template Design Decisions

- **Single template** (not multiple): The project has one PR workflow for all contribution types. A single template with a "Type of Change" checkbox section handles code, docs, and config PRs without complexity.
- **Checklist uses unchecked boxes**: GitHub renders `- [ ]` as interactive checkboxes in the PR description. Contributors check items as they complete them. This is the universal pattern used by major .NET OSS projects.
- **Docs and code checks combined**: Per NFR13 ("documentation changes follow the same PR review process as code changes"), the template includes both docs checks (lint, links) and code checks (build, test) in a single checklist. Contributors check the items relevant to their change type.
- **No YAML frontmatter**: PR templates are plain markdown. GitHub does not support YAML-based PR forms (unlike issue forms).
- **No @mentions in template**: Per GitHub best practices, avoid hardcoding reviewer @mentions in templates — use CODEOWNERS or manual assignment instead.

### Alignment with CONTRIBUTING.md

The existing `CONTRIBUTING.md` (Story 10-1) already describes the PR submission workflow at lines 29-40:
- "Submit a Pull Request" section covers: commit, push, open PR against `main`, reference related issue, wait for CI checks
- "Pull Request Expectations" section (lines 122-126) covers: CI checks must pass, clear description, keep PRs small
- "Run Docs Validation Locally" section (lines 96-100) shows the exact `markdownlint-cli2` command

The PR template reinforces these guidelines with actionable checkboxes. No changes to CONTRIBUTING.md are needed — the template complements it.

### Markdown Lint Command Reference

From `CONTRIBUTING.md` line 99, the exact local validation command is:
```bash
npx markdownlint-cli2 "docs/**/*.md" "README.md" "CONTRIBUTING.md" "CHANGELOG.md"
```
Include this command in the template checklist item for markdown lint so contributors can copy-paste it.

### Previous Story (10-2) Intelligence

Story 10-2 created GitHub issue templates in `.github/ISSUE_TEMPLATE/`. Key learnings:
- Pure documentation/config story — no code changes, similar to this story
- YAML issue forms use the `.yml` extension but PR templates use `.md` (plain markdown)
- Story 10-2 Dev Notes explicitly call out `.github/PULL_REQUEST_TEMPLATE.md` as "Story 10-3 (does not exist yet)" at line 131
- Branch naming convention: `feat/story-10-3-pr-template-and-review-process`
- Commit message pattern: `feat: Complete Story 10-3 PR template and review process`
- The `.github/` directory already exists with `ISSUE_TEMPLATE/`, `agents/`, `prompts/`, `workflows/`, `copilot-instructions.md`

### PRD Requirements Covered

| Requirement | Description | How Addressed |
|------------|-------------|---------------|
| FR31 | Developer can submit PRs following a documented template and checklist | `.github/PULL_REQUEST_TEMPLATE.md` with structured checklist |
| FR38 | Documentation reviewer can verify changes through the same PR process as code | Single unified template for all PR types (NFR13) |

### Non-Functional Requirements

| NFR | Description | How Addressed |
|-----|-------------|---------------|
| NFR13 | Documentation changes follow the same PR review process as code changes | Single template with combined docs+code checklist |

### References

- [Source: architecture-documentation.md#D4] — PR template specification, checklist items, file location
- [Source: epics.md#Story-3.3] — Story definition, acceptance criteria, BDD scenarios
- [Source: prd-documentation.md#FR31-FR38] — PR template and review process requirements
- [Source: prd-documentation.md#NFR13] — Unified review process requirement
- [Source: CONTRIBUTING.md#Submit-a-Pull-Request] — Existing PR submission workflow (lines 29-40)
- [Source: CONTRIBUTING.md#Pull-Request-Expectations] — PR expectations (lines 122-126)
- [Source: CONTRIBUTING.md#Run-Docs-Validation-Locally] — Markdown lint command (lines 96-100)
- [Source: 10-2-github-issue-templates.md] — Previous story intelligence, file structure patterns
- [Source: GitHub Docs — Creating a PR template] — Template file location and auto-population behavior

### Git Intelligence

Recent commits (last 5): All documentation stories (10-1, 9-1, 8-6). Pattern: `feat: Complete Story X-Y <description>`. Merge via PR. No conflicts expected — new single file only.

- Branch convention: `feat/story-10-3-pr-template-and-review-process`
- Commit convention: `feat: Complete Story 10-3 PR template and review process`

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

No issues encountered. Straightforward config-only story.

### Completion Notes List

- Created `.github/PULL_REQUEST_TEMPLATE.md` with all four sections: Description, Type of Change, Checklist, Additional Context
- Template includes all six D4-specified checklist items with the exact `markdownlint-cli2` command from CONTRIBUTING.md
- Single unified template serves both code and documentation PRs per NFR13
- File placed at standard GitHub location for automatic PR description population
- Existing workspace modifications were reconciled during review for transparency (`_bmad-output/implementation-artifacts/10-3-pr-template-and-review-process.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml`)
- Pre-existing build errors in `_bmad-output/planning-artifacts/` prototype and `Server.Tests` CA2007 warnings are unrelated to this story

### Senior Developer Review (AI)

- **Review Date:** 2026-02-28
- **Outcome:** Approved after fixes
- **Issues fixed:** 4 (2 High, 2 Medium)
- **Fix summary:**
  - Corrected verification claims to distinguish static validation from post-merge GitHub behavior.
  - Reconciled story completion notes and file tracking with observed workspace reality.
  - Improved PR template heading structure by adding top-level title for markdown consistency.
  - Replaced ambiguous issue placeholder with concrete example (`Closes #123`) to avoid malformed placeholder syntax.

### File List

- `.github/PULL_REQUEST_TEMPLATE.md` (NEW)
- `_bmad-output/implementation-artifacts/10-3-pr-template-and-review-process.md` (MODIFIED)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (MODIFIED - review status sync)

## Change Log

- 2026-02-28: Created PR template with structured Description, Type of Change, Checklist (D4 items), and Additional Context sections. Satisfies FR31, FR38, NFR13.
- 2026-02-28: Senior review completed; corrected verification wording, improved template heading/issue placeholder, and moved story status to done.
