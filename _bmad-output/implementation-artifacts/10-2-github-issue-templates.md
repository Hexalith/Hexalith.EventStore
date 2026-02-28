# Story 10.2: GitHub Issue Templates

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer who found a bug or wants to request a feature,
I want structured issue templates that guide me through filing a useful report,
so that maintainers have the information they need to act on my feedback.

## Acceptance Criteria

1. **AC1 - Bug Report Template**: `.github/ISSUE_TEMPLATE/01-bug-report.yml` exists as a YAML-based GitHub Issue Form with: a required duplicate-check checkbox, description, steps to reproduce, expected behavior, actual behavior, environment fields (OS, .NET SDK version, DAPR CLI version, Docker version — all as free-text `input` fields), a log/stack trace textarea with `render: text`, and optional regression and additional context fields. Every field has a unique `id` attribute. The `labels` key is set to `["bug"]`.

2. **AC2 - Feature Request Template**: `.github/ISSUE_TEMPLATE/02-feature-request.yml` exists as a YAML-based GitHub Issue Form with: a required duplicate-check checkbox, problem description, proposed solution, intended use case, alternatives considered, and additional context fields. Every field has a unique `id` attribute. The `labels` key is set to `["enhancement"]`.

3. **AC3 - Documentation Improvement Template**: `.github/ISSUE_TEMPLATE/03-docs-improvement.yml` exists as a YAML-based GitHub Issue Form with: page URL or file path (`input`), type of issue (dropdown: Missing documentation, Incorrect/outdated information, Unclear/confusing explanation, Code sample doesn't work, Broken link, Other), description of what's wrong or missing, and suggested fix. Every field has a unique `id` attribute. The `labels` key is set to `["documentation"]`.

4. **AC4 - Auto-Labels**: Issues created from each template automatically receive the correct label via the `labels` top-level key: `bug` for bug reports, `enhancement` for feature requests, `documentation` for docs improvements.

5. **AC5 - Good First Issue Label**: The `good first issue` label exists in the repository so that beginner-friendly issues can be tagged per FR29. This is a repository admin action — document in completion notes if the label needs manual creation.

6. **AC6 - Config File**: `.github/ISSUE_TEMPLATE/config.yml` exists with `blank_issues_enabled: true` and includes contact links to: (1) GitHub Discussions for general questions, (2) a security advisory reporting path.

## Tasks / Subtasks

- [x] Task 1: Create `.github/ISSUE_TEMPLATE/` directory (AC: all)
  - [x] Create the `ISSUE_TEMPLATE` directory under `.github/`

- [x] Task 2: Create `01-bug-report.yml` issue form (AC: 1, 4)
  - [x] Top-level keys: `name: "Bug Report"`, `description: "Report a bug in Hexalith.EventStore"`, `labels: ["bug"]`
  - [x] Body element 1: `type: markdown` — intro text explaining what qualifies as a bug vs. a support question, link to Discussions for questions
  - [x] Body element 2: `type: checkboxes`, id: `existing-issue` — required "I have searched the existing issues" checkbox
  - [x] Body element 3: `type: textarea`, id: `description` — "Describe the bug" (required). Placeholder: "A clear and concise description of what the bug is."
  - [x] Body element 4: `type: textarea`, id: `repro-steps` — "Steps to Reproduce" (required). Placeholder with numbered step template: "1. ...\n2. ...\n3. ..."
  - [x] Body element 5: `type: textarea`, id: `expected-behavior` — "Expected Behavior" (required)
  - [x] Body element 6: `type: textarea`, id: `actual-behavior` — "Actual Behavior" (required)
  - [x] Body element 7: `type: textarea`, id: `regression` — "Is this a regression?" (optional). Description: "Did this work in a previous version?"
  - [x] Body element 8: `type: input`, id: `dotnet-version` — ".NET SDK Version" (optional). Description: "Run `dotnet --version`". Placeholder: "e.g. 10.0.102"
  - [x] Body element 9: `type: input`, id: `dapr-version` — "DAPR CLI Version" (optional). Description: "Run `dapr version`". Placeholder: "e.g. 1.16.0"
  - [x] Body element 10: `type: input`, id: `os` — "Operating System" (optional). Placeholder: "e.g. Windows 11, Ubuntu 24.04, macOS 15"
  - [x] Body element 11: `type: input`, id: `docker-version` — "Docker Version" (optional). Description: "Run `docker --version`". Placeholder: "e.g. 27.0.3"
  - [x] Body element 12: `type: textarea`, id: `logs` — "Relevant Log Output / Stack Trace" (optional). `render: text` for automatic code formatting
  - [x] Body element 13: `type: textarea`, id: `additional-context` — "Additional Context" (optional)
  - [x] Body element 14: `type: checkboxes`, id: `code-of-conduct` — "I agree to follow this project's [Code of Conduct](../../CODE_OF_CONDUCT.md)" (required)

- [x] Task 3: Create `02-feature-request.yml` issue form (AC: 2, 4)
  - [x] Top-level keys: `name: "Feature Request"`, `description: "Suggest a new feature or enhancement"`, `labels: ["enhancement"]`
  - [x] Body element 1: `type: markdown` — intro text encouraging Discussion for early-stage ideas
  - [x] Body element 2: `type: checkboxes`, id: `existing-issue` — required "I have searched the existing issues" checkbox
  - [x] Body element 3: `type: textarea`, id: `problem` — "Is your feature request related to a problem?" (required). Placeholder: "I'm always frustrated when..."
  - [x] Body element 4: `type: textarea`, id: `solution` — "Describe the solution you'd like" (required). Description: "A clear description of what you want to happen. Include API examples if applicable."
  - [x] Body element 5: `type: textarea`, id: `use-case` — "Intended Use Case" (required). Description: "Describe the scenario where you would use this feature." Placeholder: "In my application I need to..."
  - [x] Body element 6: `type: textarea`, id: `alternatives` — "Alternatives Considered" (optional). Description: "Any alternative solutions or workarounds you've considered."
  - [x] Body element 7: `type: textarea`, id: `additional-context` — "Additional Context" (optional)
  - [x] Body element 8: `type: checkboxes`, id: `code-of-conduct` — "I agree to follow this project's [Code of Conduct](../../CODE_OF_CONDUCT.md)" (required)

- [x] Task 4: Create `03-docs-improvement.yml` issue form (AC: 3, 4)
  - [x] Top-level keys: `name: "Documentation Improvement"`, `description: "Report an issue or suggest an improvement to documentation"`, `labels: ["documentation"]`
  - [x] Body element 1: `type: input`, id: `page-url` — "Page URL or File Path" (required). Description: "Link to the documentation page or file path in the repository." Placeholder: "https://github.com/Hexalith/Hexalith.EventStore/blob/main/docs/..."
  - [x] Body element 2: `type: dropdown`, id: `issue-type` — "Type of Documentation Issue" (required). Options: "Missing documentation", "Incorrect or outdated information", "Unclear or confusing explanation", "Code sample doesn't work", "Broken link", "Other"
  - [x] Body element 3: `type: textarea`, id: `description` — "What's Wrong or Missing" (required). Description: "Describe what needs to be fixed or added."
  - [x] Body element 4: `type: textarea`, id: `suggested-fix` — "Suggested Fix" (optional). Description: "If you have a specific suggestion, describe it here."
  - [x] Body element 5: `type: textarea`, id: `additional-context` — "Additional Context" (optional)

- [x] Task 5: Create `config.yml` template chooser (AC: 6)
  - [x] Set `blank_issues_enabled: true`
  - [x] Contact link 1: name "Questions & Discussions", url `https://github.com/Hexalith/Hexalith.EventStore/discussions`, about: "Ask questions, share ideas, and get community support"
  - [x] Contact link 2: name "Security Vulnerability", url `https://github.com/Hexalith/Hexalith.EventStore/security/advisories/new`, about: "Report a security vulnerability privately"

- [x] Task 6: Verify labels exist in GitHub repository (AC: 4, 5)
  - [x] Confirm `bug` and `enhancement` labels exist (GitHub defaults — should already be present)
  - [x] Confirm `documentation` label exists — if missing, note in completion notes for maintainer to create
  - [x] Confirm `good first issue` label exists — if missing, note in completion notes for maintainer to create

## Dev Notes

### Architecture Compliance

- **Architecture Decision D4** governs this story: "Standard open-source GitHub community setup, aligned with PRD Journey 4 (Kenji Contributes)"
- D4 explicitly specifies three issue templates: Bug Report, Feature Request, Documentation Improvement
- D4 specifies exact fields per template (see table below)
- Files go in `.github/ISSUE_TEMPLATE/` — this directory does NOT currently exist and must be created
- This is a **documentation/config-only** story — no source code changes, no new packages, no CI changes

### Architecture D4 Issue Template Specification

| Template | Fields (from D4) | Label |
|----------|-----------------|-------|
| Bug Report | Steps to reproduce, expected/actual behavior, environment (OS, .NET version, DAPR version) | `bug` |
| Feature Request | Problem description, proposed solution, alternatives considered | `enhancement` |
| Documentation Improvement | Page/section, what's wrong or missing, suggested fix | `documentation` |

### GitHub Issue Forms (YAML format) — Technical Requirements

- Use GitHub Issue Forms (`.yml` YAML format), NOT the older markdown-based templates (`.md`)
- YAML issue forms provide structured input fields (dropdowns, textareas, checkboxes) with validation
- Required top-level keys: `name`, `description`, `labels`, `body`
- Body contains an array of field objects, each with `type` (markdown, input, textarea, dropdown, checkboxes), `id`, `attributes` (label, description, placeholder, options), and `validations` (required)
- **Every field element MUST have a unique `id` attribute** — required for GitHub Actions automation and API parsing
- Use `render: text` on log/stack trace textareas to auto-format as code blocks
- Use `input` fields (not dropdowns) for version numbers — dropdowns hardcode values that go stale with each release. This follows the pattern used by dotnet/runtime, dotnet/efcore, and dotnet/aspnetcore
- Use descriptive `placeholder` text showing example good input (e.g., "e.g. 10.0.102") to guide contributors
- Include a required "I have searched existing issues" checkbox as the first interactive element — this is the single most universally adopted pattern across all well-maintained OSS projects (dotnet/aspnetcore, grafana, fluxcd)

### File Structure

```
.github/
├── ISSUE_TEMPLATE/
│   ├── 01-bug-report.yml       [NEW] Bug report issue form
│   ├── 02-feature-request.yml  [NEW] Feature request issue form
│   ├── 03-docs-improvement.yml [NEW] Documentation improvement issue form
│   └── config.yml              [NEW] Template chooser configuration
├── agents/                     [EXISTS] BMAD agents — DO NOT TOUCH
├── prompts/                    [EXISTS] BMAD prompts — DO NOT TOUCH
├── workflows/                  [EXISTS] GitHub workflows — DO NOT TOUCH
└── copilot-instructions.md     [EXISTS] — DO NOT TOUCH
```

Number-prefixed filenames (`01-`, `02-`, `03-`) control display order in the GitHub template chooser UI. YAML files sort before markdown files in the chooser.

### CRITICAL: Files NOT to Touch

- `.github/agents/` — BMAD agent files
- `.github/prompts/` — BMAD prompt files
- `.github/workflows/` — GitHub Actions workflows (Story 11-3)
- `.github/copilot-instructions.md` — Copilot config
- `.github/PULL_REQUEST_TEMPLATE.md` — Story 10-3 (does not exist yet)
- `CONTRIBUTING.md` — Already complete (Story 10-1)
- `CODE_OF_CONDUCT.md` — Already complete (Story 10-1)
- Any `src/` or `tests/` files — this is config-only

### Environment Values for Bug Report Template

From `docs/getting-started/prerequisites.md` and architecture. Use as placeholder examples in `input` fields (NOT dropdowns):
- .NET SDK: e.g. 10.0.102
- DAPR CLI: e.g. 1.16.0
- Docker: e.g. 27.0.3
- OS: e.g. Windows 11, Ubuntu 24.04, macOS 15

### Labels Strategy

GitHub repositories come with default labels `bug` and `enhancement`. The labels `documentation` and `good first issue` may need to be created manually by a repository admin. The issue templates reference labels by name — GitHub will auto-apply them when an issue is created from a template, provided the labels exist in the repository.

**Do NOT attempt to create labels programmatically** — this story only creates the YAML template files. Label creation is an admin action noted for the maintainer.

### config.yml Design Decisions

- **`blank_issues_enabled: true`**: Allows contributors to file free-form issues for edge cases that don't fit templates (security reports, meta-issues, etc.). This follows the pattern of dotnet/runtime, dotnet/aspnetcore, and dotnet/maui. Setting `false` risks blocking legitimate issues.
- **Discussions contact link**: Redirects support questions away from the issue tracker, keeping issues actionable.
- **Security advisory contact link**: Points to GitHub's private vulnerability reporting (`/security/advisories/new`), ensuring security issues are not filed publicly.

### Previous Story (10-1) Intelligence

Story 10-1 created `CONTRIBUTING.md` and `CODE_OF_CONDUCT.md`. Key learnings:
- Pure documentation/config stories — no code changes
- CONTRIBUTING.md already references issue templates: "Use the [Issue Tracker](https://github.com/Hexalith/Hexalith.EventStore/issues) for bug reports and feature requests"
- CONTRIBUTING.md already links to `good first issue` label with a filtered GitHub URL
- CODE_OF_CONDUCT.md exists at repo root — issue templates can link to it via `../../CODE_OF_CONDUCT.md`
- Branch naming convention: `feat/story-10-2-github-issue-templates`
- Commit message pattern: `feat: Complete Story 10-2 GitHub issue templates`

### PRD Requirements Covered

| Requirement | Description | How Addressed |
|------------|-------------|---------------|
| FR29 | Developer can identify beginner-friendly contributions | `good first issue` label exists in repo |
| FR30 | Developer can file structured bug reports, feature requests, and docs improvements | Three issue form templates with structured fields, validation, and auto-labels |

### References

- [Source: architecture-documentation.md#D4] — Issue template specification, fields, labels
- [Source: epics.md#Story-3.2] — Story definition, acceptance criteria, BDD scenarios
- [Source: prd-documentation.md#FR29-FR30] — Structured issue templates and beginner-friendly issues
- [Source: CONTRIBUTING.md#Community-Guidelines] — Existing link to Issue Tracker
- [Source: CONTRIBUTING.md#Good-First-Issues] — Existing `good first issue` label reference
- [Source: GitHub Docs — Issue Form Syntax] — YAML form schema, field types, `id` and `render` attributes
- [Source: dotnet/runtime, dotnet/aspnetcore, dotnet/efcore ISSUE_TEMPLATE] — Industry patterns: input fields for versions, duplicate-check checkbox, `render: text` for logs

### Git Intelligence

Recent commits (last 5): All documentation stories (10-1, 9-1, 8-6, 8-5). Pattern: `feat: Complete Story X-Y <description>`. Merge via PR. No conflicts expected — new directory and files only.

## Change Log

- 2026-02-28: Created GitHub Issue Templates (Bug Report, Feature Request, Documentation Improvement) and config.yml template chooser. Verified all required labels exist in repository.
- 2026-02-28: Senior code review fixes applied: added `id` to markdown intro fields in bug/feature templates, reconciled story File List with actual workspace git changes, and moved story to done.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- All 4 YAML files validated with Python yaml.safe_load — no syntax errors
- All required GitHub labels verified via `gh label list`: bug, enhancement, documentation, good first issue

### Completion Notes List

- Created `.github/ISSUE_TEMPLATE/` directory with 4 files
- Bug report template (01-bug-report.yml): 14 body elements with unique IDs, required duplicate-check checkbox, environment fields as free-text inputs (not dropdowns), log textarea with `render: text`, Code of Conduct acknowledgment
- Feature request template (02-feature-request.yml): 8 body elements with unique IDs, required duplicate-check checkbox, problem/solution/use-case fields, Code of Conduct acknowledgment
- Documentation improvement template (03-docs-improvement.yml): 5 body elements with unique IDs, page URL input, dropdown with 6 issue types, description and suggested fix textareas
- Template chooser config (config.yml): `blank_issues_enabled: true`, contact links to Discussions and Security Advisory
- All 4 required labels (`bug`, `enhancement`, `documentation`, `good first issue`) already exist in the repository — no manual creation needed
- Senior review reconciliation applied: markdown intro items in bug and feature templates now include explicit unique `id` attributes (`intro`) for strict AC interpretation.
- Workspace-level git reconciliation recorded: `.mcp.json` and `_bmad-output/implementation-artifacts/sprint-status.yaml` were detected as modified in working tree during review and are tracked in this file list for transparency.

### Senior Developer Review (AI)

- **Review Date:** 2026-02-28
- **Outcome:** Approved after fixes
- **Issues fixed:** 5 (1 High, 4 Medium)
- **Fix summary:**
  - Added `id: intro` to markdown intro elements in `.github/ISSUE_TEMPLATE/01-bug-report.yml` and `.github/ISSUE_TEMPLATE/02-feature-request.yml`.
  - Reconciled story metadata with git reality (workspace-modified files included in File List; completion notes updated).
  - Re-verified repository labels via `gh label list` (`bug`, `enhancement`, `documentation`, `good first issue`).

### File List

- .github/ISSUE_TEMPLATE/01-bug-report.yml [NEW]
- .github/ISSUE_TEMPLATE/02-feature-request.yml [NEW]
- .github/ISSUE_TEMPLATE/03-docs-improvement.yml [NEW]
- .github/ISSUE_TEMPLATE/config.yml [NEW]
- _bmad-output/implementation-artifacts/10-2-github-issue-templates.md [MODIFIED]
- _bmad-output/implementation-artifacts/sprint-status.yaml [MODIFIED - workspace tracking sync]
- .mcp.json [MODIFIED - workspace file, unrelated to template implementation]
