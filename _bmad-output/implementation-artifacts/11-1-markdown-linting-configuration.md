# Story 11.1: Markdown Linting Configuration

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a documentation contributor,
I want automated markdown formatting validation on every PR,
so that all documentation pages maintain consistent formatting standards.

## Acceptance Criteria

1. **AC1 - Config File Exists**: `.markdownlint-cli2.jsonc` exists at the repository root with rules configured for:
   - Heading hierarchy enforcement (no skipped levels, NFR6)
   - Code block language tag requirement (NFR9)
   - No hard line wrapping (line-length rule disabled)
   - Allowance for inline HTML (`<details>` blocks for Mermaid accessibility)

2. **AC2 - Ignore File Updated**: The existing `.markdownlintignore` is updated to exclude non-documentation files as needed (BMAD output already excluded; add any other generated/non-doc paths).

3. **AC3 - Local CLI Runs**: `markdownlint-cli2` can be run locally with:
   ```bash
   npx markdownlint-cli2 "docs/**/*.md" "README.md" "CONTRIBUTING.md" "CHANGELOG.md" "CODE_OF_CONDUCT.md"
   ```
   and exits cleanly with zero violations. Requires Node.js 18+ (LTS) for `npx`.

4. **AC4 - Rules Align With Architecture**: The linting rules align with the markdown formatting patterns defined in architecture-documentation.md Decision D3 and the Markdown Formatting Patterns table.

5. **AC5 - Execution Time**: Linter completes in under 5 seconds locally, confirming CI budget target (~5s per architecture D3).

## Tasks / Subtasks

- [x] Task 1: Create `.markdownlint-cli2.jsonc` config file (AC: 1, 4)
  - [x] Use markdownlint-**cli2** format (top-level `"config"` key required — NOT plain `.markdownlint.json` format)
  - [x] Disable `MD013` (line-length) — architecture mandates no hard line wrapping
  - [x] Configure `MD033` (no-inline-html) allowed_elements: `details`, `summary`, `br`, `img`, `picture`, `source` only (minimal set — add more only if actual violations require it)
  - [x] Enable `MD025` (single-title/single-h1) — one H1 per page per architecture convention
  - [x] Enable `MD001` (heading-increment) — no skipped heading levels (NFR6)
  - [x] Configure `MD040` (fenced-code-language) — require language tag on all code blocks (NFR9). Note: `text` and `console` are valid language tags for non-code output blocks
  - [x] Configure `MD041` (first-line-heading) — disabled globally (README has badges, all docs pages have nav links before H1; markdownlint-cli2 JSONC format does not support per-glob overrides — `overrides` key is not a valid cli2 property)
  - [x] Configure `MD024` (no-duplicate-heading) with `siblings_only: true` — allow same heading text in different sections
  - [x] Add `MD046: { style: "fenced" }` — enforce fenced code blocks (no indented style)
  - [x] Add `MD048: { style: "backtick" }` — enforce backtick fences (not tildes)
  - [x] Add `MD007: { indent: 4 }` — match `.editorconfig` 4-space indent for unordered lists
  - [x] Add `MD029: { style: "ordered" }` — enforce ordered list numbering (architecture: ordered lists for sequential steps)
  - [x] Enable `MD036` (no-emphasis-as-heading) — prevent bold text used as fake headings; enforce proper `##` heading syntax (supports NFR6 heading hierarchy)
- [x] Task 2: Update `.markdownlintignore` (AC: 2)
  - [x] Replace existing single-line exclusion with comprehensive list
  - [x] Add `_bmad/**` — BMAD framework files are not project documentation
  - [x] Add `_bmad-output/**` — all BMAD output artifacts (planning + implementation)
  - [x] Add `node_modules/**` — if npx caches locally
  - [x] Add `**/CLAUDE.md` — AI agent instructions, not documentation
  - [x] Add `samples/**/*.md` — no markdown files found in samples; exclusion not needed (no entry added)
  - [x] Review if any other generated/non-doc markdown files need exclusion — none found
- [x] Task 3: Run linter, iterate config, fix violations (AC: 3, 5)
  - [x] Run `npx markdownlint-cli2 "docs/**/*.md" "README.md" "CONTRIBUTING.md" "CHANGELOG.md" "CODE_OF_CONDUCT.md"` with the initial config
  - [x] Review violations — if a rule causes excessive violations in well-written content, adjust the rule config rather than rewriting all docs
  - [x] Fix genuine violations in target documentation files
  - [x] Re-run linter until zero violations
  - [x] Time the linter execution — confirm under 5 seconds (AC5, CI budget) — measured 1.4s
  - [x] Document any rules that were adjusted and why (see Completion Notes)
- [x] Task 4: Verify rule alignment with architecture (AC: 4)
  - [x] Cross-check each enabled rule against Markdown Formatting Patterns table in architecture-documentation.md
  - [x] Verify heading hierarchy enforcement matches NFR6
  - [x] Verify code block language requirement matches NFR9
  - [x] Verify inline HTML allowance covers `<details>` blocks for Mermaid accessibility
  - [x] Verify unordered list indent matches `.editorconfig` 4-space standard

## Dev Notes

### Architecture Compliance

- **Architecture Decision D3** governs this story: CI Pipeline Architecture, Phase 1a
- **Tooling**: `markdownlint-cli2` (David Anson's latest generation CLI) — architecture-documentation.md explicitly selects this tool
- **Config format**: `.markdownlint-cli2.jsonc` (JSONC, not JSON or YAML) — architecture-documentation.md specifies this exact filename
- **CI budget**: Linting estimated at ~5s — well within the ~35s budget for the `lint-and-links` CI job
- **Runs on**: ubuntu-latest only (not cross-platform matrix) — architecture explicitly states linting runs once, not on matrix

### Markdown Formatting Patterns to Enforce (from architecture-documentation.md)

These are the authoritative rules the linter config MUST align with:

| Pattern | Linter Rule | Config |
|---------|-------------|--------|
| Heading hierarchy: H1 page title, H2 major, H3 sub. Never skip levels. | `MD001` (heading-increment) | `true` (enabled) |
| One H1 per page (page title) | `MD025` (single-title/single-h1) | `true` (enabled) |
| Code blocks always specify language (`csharp`, `bash`, `yaml`, `json`, `text`) | `MD040` (fenced-code-language) | `true` (enabled) |
| No hard wrap in markdown source | `MD013` (line-length) | `false` (disabled) |
| Inline HTML allowed for `<details>` blocks | `MD033` (no-inline-html) | `allowed_elements` list |
| Fenced code blocks only (no indented) | `MD046` (code-block-style) | `{ style: "fenced" }` |
| Backtick fences only (no tildes) | `MD048` (code-fence-style) | `{ style: "backtick" }` |
| 4-space unordered list indent | `MD007` (ul-indent) | `{ indent: 4 }` |
| Ordered lists use sequential numbers | `MD029` (ol-prefix) | `{ style: "ordered" }` |
| No bold text as fake headings | `MD036` (no-emphasis-as-heading) | `true` (enabled) |
| Tables for comparisons, ordered lists for sequential steps | Not enforceable by linter | Convention only |
| Callouts use `> **Note:**` syntax | Not enforceable by linter | Convention only |

### Existing Repository State

- `.markdownlintignore` **EXISTS** — currently excludes `_bmad-output/implementation-artifacts/**/*.md`
- `.markdownlint-cli2.jsonc` **DOES NOT EXIST** — this story creates it
- `package.json` **DOES NOT EXIST** — use `npx` for invocation (no local Node.js project)
- **Node.js 18+ (LTS) required** for `npx markdownlint-cli2` — no npm install needed, npx fetches on first run
- No `[*.md]` section in `.editorconfig` — global rules apply (4-space indent, CRLF, UTF-8). Config `MD007` indent MUST match this 4-space standard

### Files to Lint (Architecture Scope)

The linting scope covers all hand-authored documentation:
- `docs/**/*.md` — all documentation pages
- `README.md` — root readme (NOTE: starts with badges — needs `MD041` file-level override)
- `CONTRIBUTING.md` — contribution guide
- `CHANGELOG.md` — changelog
- `CODE_OF_CONDUCT.md` — code of conduct (adopted Contributor Covenant — may need rule adjustments if externally-sourced content triggers violations; prefer adjusting rules over modifying the standard)

**Excluded from linting** (via `.markdownlintignore`):
- `_bmad-output/**/*.md` — BMAD artifacts (planning + implementation)
- `_bmad/**/*.md` — BMAD framework files
- `node_modules/**` — npm cache
- `CLAUDE.md` — AI agent instructions

### Current Documentation Files That Will Be Linted

```
README.md                                    [EXISTS]
CONTRIBUTING.md                              [EXISTS]
CHANGELOG.md                                 [EXISTS]
CODE_OF_CONDUCT.md                           [EXISTS]
docs/page-template.md                        [EXISTS]
docs/concepts/architecture-overview.md       [EXISTS]
docs/concepts/choose-the-right-tool.md       [EXISTS]
docs/getting-started/prerequisites.md        [EXISTS]
docs/getting-started/quickstart.md           [EXISTS]
```

### CRITICAL: Do NOT Create These Files

This story only creates the linting configuration. Do NOT create:
- `.github/workflows/docs-validation.yml` — that's Story 11-3
- `.lycheeignore` — that's Story 11-2
- Any new documentation pages
- Any changes to `src/` or `tests/`

### CRITICAL: Do NOT Modify These Files (Unless Fixing Lint Violations)

- `.github/workflows/ci.yml` — existing CI workflow, not part of this story
- `.github/workflows/release.yml` — release workflow
- Any files in `.github/ISSUE_TEMPLATE/`, `.github/DISCUSSION_TEMPLATE/`
- Any files in `src/`, `tests/`, `samples/`

### Recommended .markdownlint-cli2.jsonc Configuration

Starting point — validate against actual lint output and adjust as needed:

```jsonc
{
  // Hexalith.EventStore markdown linting configuration
  // Aligned with architecture-documentation.md Markdown Formatting Patterns
  // Format: markdownlint-cli2 (top-level "config" key required)
  "config": {
    "default": true,
    // MD013: Line length — disabled (architecture: no hard wrap)
    "MD013": false,
    // MD033: Inline HTML — minimal allowlist for Mermaid accessibility
    // Add more elements ONLY if actual violations require it
    "MD033": {
      "allowed_elements": [
        "details",
        "summary",
        "br",
        "img",
        "picture",
        "source"
      ]
    },
    // MD024: Duplicate headings — allow in different sections
    "MD024": {
      "siblings_only": true
    },
    // MD046: Fenced code blocks only (no indented style)
    "MD046": {
      "style": "fenced"
    },
    // MD048: Backtick fences only (no tildes)
    "MD048": {
      "style": "backtick"
    },
    // MD007: Unordered list indent — match .editorconfig 4-space standard
    "MD007": {
      "indent": 4
    },
    // MD029: Ordered list numbering — sequential (architecture: ordered for steps)
    "MD029": {
      "style": "ordered"
    },
    // MD036: No emphasis as heading — prevent **bold** used as fake headings (NFR6)
    "MD036": true
  },
  // File-level overrides: disable MD041 only for README.md (badges before H1)
  // instead of disabling globally
  "overrides": [
    {
      "files": ["README.md"],
      "config": {
        "MD041": false
      }
    }
  ]
}
```

**Rules left at default `true` (architecture-enforced):**
- `MD001` — heading-increment (NFR6: no skipped levels)
- `MD025` — single-title/single-h1 (one H1 per page)
- `MD040` — fenced-code-language (NFR9: language tags required). Note: `text`, `console` are valid tags for non-code output blocks
- `MD041` — first-line-heading (enabled globally, overridden only for README.md)

**Config format note:** This is markdownlint-**cli2** format. The `"config"` and `"overrides"` keys are cli2-specific. Do NOT use plain `.markdownlint.json` format (which lacks `overrides` support).

### Previous Story Intelligence (Story 10-4)

Key learnings from the last Epic 10 story:
- Pure config/documentation stories — no code changes to `src/` or `tests/`
- Branch naming: `feat/story-11-1-markdown-linting-configuration`
- Commit message: `feat: Complete Story 11-1 markdown linting configuration`
- `.github/` directory structure is well-established — issue templates, PR template, discussion templates, workflows all exist
- CI workflow `ci.yml` already has Python-based YAML validation for discussion templates (Story 10-4 added this)
- Pre-existing build warnings (CA2007 in Server.Tests) are unrelated — ignore them

### Git Intelligence

Recent commits show:
- Epic 16 fluent API completion (stories 16-5 through 16-10)
- CI fixes for .NET SDK 10 compatibility (`dotnet test` per-project, Dapr port fixes)
- Pattern: feature branches merged via PRs, conventional commit messages
- No conflicts expected — this story creates NEW files (`.markdownlint-cli2.jsonc`) and modifies only `.markdownlintignore`

### Project Structure Notes

- Config file `.markdownlint-cli2.jsonc` lives at **repository root** (same level as `.editorconfig`, `.markdownlintignore`)
- Aligns with architecture-documentation.md file tree: `.markdownlint-cli2.jsonc [NEW]` and `.markdownlintignore [EXISTS]`
- No conflicts with existing structure

### References

- [Source: architecture-documentation.md#D3] — CI Pipeline Architecture, markdownlint-cli2 selection, CI budget
- [Source: architecture-documentation.md#Markdown-Formatting-Patterns] — Rule alignment table
- [Source: architecture-documentation.md#Tooling-Stack] — markdownlint-cli2 selected, ~5s CI budget
- [Source: architecture-documentation.md#DGAP-6] — ".markdownlint-cli2.jsonc ruleset needs to be defined during first CI story"
- [Source: architecture-documentation.md#Existing-Infrastructure] — ".markdownlintignore exists, no .markdownlint.json ruleset"
- [Source: prd-documentation.md#NFR6] — Heading hierarchy, no skipped levels
- [Source: prd-documentation.md#NFR9] — Code blocks include language syntax highlighting tags
- [Source: prd-documentation.md#NFR20] — Markdown formatting passes linting consistently
- [Source: prd-documentation.md#FR36] — CI pipeline enforces markdown formatting standards
- [Source: prd-documentation.md#FR61] — Contributors can run validation suite locally
- [Source: epics.md#Story-4.1] — Story definition (mapped as Epic 11, Story 1 in sprint status)
- [Source: 10-4-github-discussions-setup.md] — Previous epic learnings, branch/commit conventions

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Initial linter run: 79 violations across 10 files
- MD041: 7 violations — all docs pages start with nav link `[← Back...]` before H1, README starts with badges. markdownlint-cli2 JSONC format does not support `overrides` key for per-file config; disabled MD041 globally
- MD014: 20+ violations — docs use `$` prefix convention for shell commands; disabled globally
- MD060: 20+ violations — compact table separator rows triggered new rule in markdownlint v0.40; not in architecture requirements; disabled globally
- MD007: 5 violations — CONTRIBUTING.md had 2-space sub-list indent; fixed to 4-space
- MD038: 1 violation — quickstart.md had `Bearer ` with trailing space in code span; fixed
- After config adjustments and doc fixes: 0 violations
- Execution time: 1.4s (well under 5s CI budget)

### Completion Notes List

- Created `.markdownlint-cli2.jsonc` with all architecture-mandated rules
- Updated `.markdownlintignore` with comprehensive exclusion list (_bmad/**, _bmad-output/**, node_modules/**, **/CLAUDE.md)
- No `samples/**/*.md` exclusion needed — no markdown files exist in samples/
- Rules adjusted from story spec: MD014 (disabled — $ prefix convention), MD060 (disabled — not in architecture), MD041 (disabled globally — cli2 lacks per-file override support in JSONC)
- Fixed CONTRIBUTING.md sub-list indentation (2-space → 4-space) for MD007 compliance
- Fixed quickstart.md code span trailing space for MD038 compliance
- All 10 architecture-mandated rules verified and aligned
- Linter completes in 1.4s on 10 files — within 5s CI budget
- Review follow-ups resolved: reverted unrelated Directory.Packages.props changes, confirmed CLAUDE.md not in scope, validated quickstart run command is correct

### Change Log

- 2026-03-01: Created `.markdownlint-cli2.jsonc` config at repository root
- 2026-03-01: Updated `.markdownlintignore` with comprehensive exclusion list
- 2026-03-01: Fixed CONTRIBUTING.md sub-list indentation (MD007)
- 2026-03-01: Fixed docs/getting-started/quickstart.md code span spacing (MD038)
- 2026-03-01: Corrected CONTRIBUTING solution filename reference to `Hexalith.EventStore.slnx`
- 2026-03-01: Updated CONTRIBUTING local lint command to include `CODE_OF_CONDUCT.md`
- 2026-03-01: Corrected quickstart note about PowerShell line continuation behavior

### File List

- `.markdownlint-cli2.jsonc` (NEW) — markdownlint-cli2 configuration
- `.markdownlintignore` (MODIFIED) — updated exclusion list
- `CONTRIBUTING.md` (MODIFIED) — fixed sub-list indentation to 4-space
- `docs/getting-started/quickstart.md` (MODIFIED) — fixed code span trailing space

## Senior Developer Review (AI)

### Review Date

2026-03-01

### Reviewer

GitHub Copilot (GPT-5.3-Codex)

### Outcome

Changes Requested

### Follow-up Review Date

2026-03-01

### Follow-up Outcome

Approved

### Scope Reviewed

- Story file and acceptance criteria validation for `11-1-markdown-linting-configuration`
- Claimed files in story `File List`
- Actual workspace diff state vs story claims
- Lint execution and timing evidence for AC3/AC5

### Acceptance Criteria Validation

- **AC1 (Config file exists):** ✅ Pass (`.markdownlint-cli2.jsonc` present)
- **AC2 (Ignore file updated):** ✅ Pass (`.markdownlintignore` expanded appropriately)
- **AC3 (Local CLI runs cleanly):** ✅ Pass (re-ran lint scope successfully)
- **AC4 (Architecture alignment):** ✅ Pass (core architecture-mandated rules present; documented justified relaxations)
- **AC5 (Execution time < 5s):** ✅ Pass (`1.188s` measured in local run)

### Findings

- **[HIGH] Scope/traceability drift with unrelated dependency upgrade in current review workspace**: `Directory.Packages.props` includes Dapr package version changes (`1.16.1` → `1.17.0`), but this documentation story’s `File List` and scope do not include dependency upgrades. This creates hidden risk and weakens story-level auditability. Evidence: `Directory.Packages.props:12-15`, story `File List` at `11-1-markdown-linting-configuration.md:295-300`.
- **[MEDIUM] Undocumented file discrepancy in workspace**: `CLAUDE.md` is present as an untracked change during this review but is not represented in story metadata. If intentional, isolate it in another story/branch; if accidental, clean it before merge. Evidence: git status output from this review session; story `File List` at `11-1-markdown-linting-configuration.md:295-300`.
- **[MEDIUM] Quickstart run command is inconsistent with repository operational guidance**: `docs/getting-started/quickstart.md` currently uses `dotnet run --project src/Hexalith.EventStore.AppHost`, while repository instructions consistently prescribe `aspire run` (and cloud fallback guidance). This can produce onboarding friction. Evidence: `docs/getting-started/quickstart.md:31`, `AGENTS.md:6`, `AGENTS.md:77`.
- **[HIGH] Incorrect solution file reference in contributor guide**: `CONTRIBUTING.md` referenced `Hexalith.EventStore.sln`, but this repository uses `Hexalith.EventStore.slnx` only. This can break contributor setup and local validation commands. Evidence: `CONTRIBUTING.md:72`, `Hexalith.EventStore.slnx` present at repo root.
- **[MEDIUM] Local docs lint command inconsistency**: `CONTRIBUTING.md` omitted `CODE_OF_CONDUCT.md` in the local markdown lint command, which diverges from this story's accepted lint scope and can let violations slip locally. Evidence: `CONTRIBUTING.md:99`, story AC3 command definition.
- **[MEDIUM] PowerShell command note was inaccurate**: quickstart claimed bash-style `\` line continuation works in PowerShell 7+, which is incorrect and can confuse first-run users. Evidence: `docs/getting-started/quickstart.md:54`; validated in PowerShell during review.

### Review Follow-ups (AI)

- [x] AI-Review (HIGH): Reconcile workspace scope before merge: move/revert unrelated `Directory.Packages.props` Dapr version upgrades from this story branch, or explicitly include and justify them in this story’s `File List` and notes (`Directory.Packages.props:12-15`, `_bmad-output/implementation-artifacts/11-1-markdown-linting-configuration.md:295-300`).
- [x] AI-Review (MEDIUM): Resolve untracked `CLAUDE.md` discrepancy: either remove from branch scope or document it explicitly with rationale in story metadata for traceability.
- [x] AI-Review (MEDIUM): Align quickstart startup command with repository guidance (`aspire run` path, including cloud-safe variant where applicable) and re-run lint to confirm no regressions (`docs/getting-started/quickstart.md:31`, `AGENTS.md:6`, `AGENTS.md:77`).
- [x] AI-Review (HIGH): Updated contributor documentation to reference `Hexalith.EventStore.slnx` instead of `.sln`.
- [x] AI-Review (MEDIUM): Updated local docs lint command to include `CODE_OF_CONDUCT.md` for parity with accepted lint scope.
- [x] AI-Review (MEDIUM): Corrected quickstart PowerShell note to avoid claiming bash `\` continuation support in PowerShell.

### Change Log

- 2026-03-01: Senior Developer Review (AI) completed — 1 HIGH and 2 MEDIUM findings; story moved to `in-progress`; review follow-up items added.
- 2026-03-01: Addressed code review findings — 3 items resolved:
    - [HIGH] Reverted `Directory.Packages.props` Dapr version upgrade (1.16.1→1.17.0) — unrelated to this story, restored to match main branch
    - [MEDIUM] `CLAUDE.md` confirmed as untracked workspace file — not staged or committed; no action needed beyond not including it in the branch
    - [MEDIUM] Quickstart `dotnet run --project` command validated as correct — `AGENTS.md` is agent instructions (not user docs), prerequisites don't list Aspire CLI, and `dotnet run` works universally; no change required
- 2026-03-01: Follow-up review passed — corrected contributor solution reference (`.slnx`), aligned local lint command scope, and fixed PowerShell continuation guidance; story status set to `done`.
