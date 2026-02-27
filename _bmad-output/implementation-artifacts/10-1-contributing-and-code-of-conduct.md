# Story 10.1: CONTRIBUTING.md & CODE_OF_CONDUCT.md

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer who wants to contribute to Hexalith,
I want clear contribution guidelines and a code of conduct,
so that I know the expected workflow (fork, branch, PR) and community standards.

## Acceptance Criteria

1. **AC1 - CONTRIBUTING.md Complete Structure**: `CONTRIBUTING.md` exists at the repository root with all six required sections: (1) How to contribute (fork, branch, PR), (2) Development setup (prerequisites, clone, build), (3) Documentation contributions (edit markdown, run lint locally), (4) Code contributions (coding standards, test requirements), (5) Good first issues label explained, (6) Community guidelines (link to CODE_OF_CONDUCT.md)

2. **AC2 - CODE_OF_CONDUCT.md Contributor Covenant v2.1**: `CODE_OF_CONDUCT.md` exists at the repository root using the full Contributor Covenant v2.1 text, with contact/enforcement details customized for the Hexalith project

3. **AC3 - README Links**: The README links to CONTRIBUTING.md and CODE_OF_CONDUCT.md (already satisfied — verify links remain intact after changes)

4. **AC4 - Markdown Formatting Standards (NFR6, NFR9)**: Both files follow markdown formatting standards: heading hierarchy H1-H4 with no skipped levels (NFR6), all code blocks specify language tags (NFR9)

## Tasks / Subtasks

- [x] Task 1: Replace CONTRIBUTING.md placeholder with full content (AC: 1, 4)
  - [x] Replace the current 5-line placeholder with the full CONTRIBUTING.md
  - [x] Section 1: "How to Contribute" — fork, branch naming (`feat/`, `fix/`, `docs/`), PR workflow
  - [x] Section 2: "Development Setup" — prerequisites (link to `docs/getting-started/prerequisites.md`), clone, `dotnet restore`, `dotnet build`, `dotnet test`
  - [x] Section 3: "Documentation Contributions" — edit markdown, file locations (`docs/`, `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md`), markdown formatting conventions
  - [x] Section 4: "Code Contributions" — coding standards (C# conventions), test requirements (unit tests required, 3-tier test structure), PR expectations
  - [x] Section 5: "Good First Issues" — explain the `good first issue` label, how to find beginner-friendly tasks
  - [x] Section 6: "Community Guidelines" — link to CODE_OF_CONDUCT.md, link to GitHub Discussions
  - [x] Ensure heading hierarchy: H1 > H2 > H3, no skipped levels
  - [x] Ensure all code blocks have language tags (`bash`, `csharp`, etc.)

- [x] Task 2: Replace CODE_OF_CONDUCT.md placeholder with Contributor Covenant v2.1 (AC: 2, 4)
  - [x] Replace the current 4-line placeholder with the full Contributor Covenant v2.1 text
  - [x] CRITICAL: Use the official Contributor Covenant v2.1 text from https://www.contributor-covenant.org/version/2/1/code_of_conduct/
  - [x] Customize enforcement/contact: set contact method (e.g., GitHub issue or email) appropriate for the Hexalith project
  - [x] Set community name to "Hexalith.EventStore"
  - [x] Ensure heading hierarchy compliance (NFR6)

- [x] Task 3: Verify README links (AC: 3)
  - [x] Confirm `README.md` line 105 still has: `[Contributing Guide](CONTRIBUTING.md)` and `[Code of Conduct](CODE_OF_CONDUCT.md)`
  - [x] No README changes expected unless links are broken

## Dev Notes

### Architecture Compliance

- **Architecture Decision D4** governs this story: "Standard open-source GitHub community setup, aligned with PRD Journey 4 (Kenji Contributes)"
- CONTRIBUTING.md structure is explicitly specified in D4 with 6 sections in exact order
- Both files are root-level files (`CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`) — NOT inside `docs/`
- This is a **documentation-only** story — no code changes, no new packages, no CI changes

### CONTRIBUTING.md Required Content Details

From Architecture D4, the required sections and their content:

**Section 1 — How to Contribute:**
- Fork the repository
- Create a feature branch from `main`
- Branch naming: `feat/<description>`, `fix/<description>`, `docs/<description>`
- Submit a pull request against `main`
- Reference a related issue if one exists

**Section 2 — Development Setup:**
- Link to prerequisites page: `docs/getting-started/prerequisites.md`
- Required tools: .NET 10 SDK (10.0.102+), Docker Desktop, DAPR CLI (1.16.x+)
- Clone: `git clone https://github.com/Hexalith/Hexalith.EventStore.git`
- Build: `dotnet restore && dotnet build`
- Test: `dotnet test`
- Solution file: `Hexalith.EventStore.sln`

**Section 3 — Documentation Contributions:**
- Documentation lives in `docs/` folder organized by: `getting-started/`, `concepts/`, `guides/`, `reference/`, `community/`
- Root-level docs: `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md`, `CODE_OF_CONDUCT.md`
- Page template: `docs/page-template.md`
- Markdown conventions: heading hierarchy (no skipped levels), code blocks with language tags, relative links between pages
- Note: Markdown linting tooling (`markdownlint-cli2`) will be configured in a future story (Epic 11). For now, follow the formatting conventions manually.

**Section 4 — Code Contributions:**
- Follow existing C# coding conventions in the codebase
- Test tiers: Tier 1 (unit tests, `tests/**/`), Tier 2 (integration with DAPR), Tier 3 (Aspire end-to-end)
- New features should include Tier 1 unit tests at minimum
- CI runs: `dotnet build --configuration Release`, then tests across tiers
- PR should pass CI (`dotnet build` + `dotnet test`)

**Section 5 — Good First Issues:**
- Look for issues labeled `good first issue`
- These are curated beginner-friendly tasks (typically doc fixes, small enhancements)
- FR29 requirement: developers can identify beginner-friendly contribution opportunities

**Section 6 — Community Guidelines:**
- Link to CODE_OF_CONDUCT.md
- Link to GitHub Discussions for questions and ideas
- Link to Issue Tracker for bugs and feature requests
- Tone: professional-casual, developer-to-developer (per PRD communication patterns)

### CODE_OF_CONDUCT.md Required Content

- Use **Contributor Covenant version 2.1** — the industry standard for open-source projects
- The official text must be used verbatim (this is a legal/community standard document)
- Customize only the contact/enforcement fields:
  - Community name: Hexalith.EventStore
  - Contact method: determine appropriate method (typically maintainer email or GitHub issue)
- Do NOT modify the covenant text itself

### Existing Files to Modify

| File | Current State | Action |
|------|--------------|--------|
| `CONTRIBUTING.md` | Placeholder (5 lines) | **Replace entirely** with full content |
| `CODE_OF_CONDUCT.md` | Placeholder (4 lines) | **Replace entirely** with Contributor Covenant v2.1 |
| `README.md` | Already links to both files (line 105) | **Verify only** — no changes expected |

### Files NOT to Touch

- `.github/ISSUE_TEMPLATE/` — Story 10-2
- `.github/PULL_REQUEST_TEMPLATE.md` — Story 10-3
- GitHub Discussions setup — Story 10-4
- `.markdownlint-cli2.jsonc` — Story 11-1
- `.github/workflows/docs-validation.yml` — Story 11-3

### Project Structure Notes

- Both files are at repository root, consistent with GitHub conventions
- `docs/community/` folder exists (`.gitkeep` only) but is for future community pages (awesome-event-sourcing, roadmap — Epic 12+)
- CONTRIBUTING.md should reference `docs/getting-started/prerequisites.md` for dev setup details (avoid duplicating prerequisite content)
- No conflicts with existing structure detected

### Markdown Formatting Requirements (NFR6, NFR9)

- **NFR6**: Heading hierarchy H1-H4 with no skipped levels (H1 > H2 > H3, never H1 > H3)
- **NFR9**: All code blocks must specify a language tag (`bash`, `csharp`, `powershell`, etc.)
- Professional-casual tone, developer-to-developer perspective
- Second person voice ("you", not "the developer")

### PRD Requirements Covered

| Requirement | Description | How Addressed |
|------------|-------------|---------------|
| FR28 | Developer can find and follow a contribution workflow | CONTRIBUTING.md Section 1 (fork/branch/PR) |
| FR29 | Developer can identify beginner-friendly contributions | CONTRIBUTING.md Section 5 (good first issues) |
| FR38 | Documentation reviewer can verify changes through PR process | CONTRIBUTING.md Section 3 (docs contributions) |

### References

- [Source: architecture-documentation.md#D4] — CONTRIBUTING.md structure, CODE_OF_CONDUCT.md requirement
- [Source: epics.md#Story-3.1] — Story definition, acceptance criteria, BDD scenarios
- [Source: prd-documentation.md#FR28-FR33] — Community & Contribution functional requirements
- [Source: prd-documentation.md#NFR6] — Heading hierarchy standard
- [Source: prd-documentation.md#NFR9] — Code block language tag requirement
- [Source: docs/getting-started/prerequisites.md] — Prerequisites content to reference (not duplicate)
- [Source: .github/workflows/ci.yml] — CI workflow for accurate build/test commands
- [Source: README.md#Contributing] — Existing links to CONTRIBUTING.md and CODE_OF_CONDUCT.md

### Git Intelligence

Recent commits show documentation stories are the current focus (Stories 8-1 through 9-1 all completed). Pattern: `feat: Complete Story X-Y <description>`. All documentation files use standard markdown formatting. No code conflicts expected — this is a pure documentation story.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

No debug issues encountered. Documentation-only story with no code changes.

### Completion Notes List

- **Task 1 (CONTRIBUTING.md):** Full CONTRIBUTING.md with all 6 required sections per Architecture D4. Includes fork/branch/PR workflow, development setup with prerequisites link, documentation contribution guidelines, code contribution standards with 3-tier test structure, good first issues guidance, and community guidelines with links to CoC, Discussions, and Issues. Heading hierarchy H1>H2>H3 verified, all code blocks have `bash` language tags.
- **Task 2 (CODE_OF_CONDUCT.md):** Replaced placeholder with full verbatim Contributor Covenant v2.1 text fetched from contributor-covenant.org. Customized enforcement contact to GitHub Issues (https://github.com/Hexalith/Hexalith.EventStore/issues). Heading hierarchy H1>H2>H3 verified. No code blocks in document (NFR9 N/A).
- **Task 3 (README links):** Verified README.md line 105 contains both `[Contributing Guide](CONTRIBUTING.md)` and `[Code of Conduct](CODE_OF_CONDUCT.md)` links. No changes needed.
- **Code Review Auto-Fix:** Added explicit local markdown lint command in CONTRIBUTING documentation contributions section and replaced generic `dotnet test` guidance with project-specific Tier 1 test commands run individually.
- **Code Review Outcome:** High and medium findings addressed; story documentation now matches actual modified files.

### File List

- `CONTRIBUTING.md` — Replaced placeholder with full contribution guide and added explicit local markdown lint command + project-specific Tier 1 test commands
- `CODE_OF_CONDUCT.md` — Replaced placeholder with Contributor Covenant v2.1
- `_bmad-output/implementation-artifacts/10-1-contributing-and-code-of-conduct.md` — Updated story status and Dev Agent Record after code review auto-fix
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — Synced story status from `review` to `done`

## Change Log

- 2026-02-27: Implemented Story 10-1 — replaced CONTRIBUTING.md and CODE_OF_CONDUCT.md placeholders with full content. CONTRIBUTING.md covers fork/branch/PR workflow, dev setup, docs contributions, code contributions, good first issues, and community guidelines. CODE_OF_CONDUCT.md uses official Contributor Covenant v2.1 with GitHub Issues as enforcement contact.
- 2026-02-27: Code review auto-fix applied — updated CONTRIBUTING.md to include explicit local markdown lint command and project-specific Tier 1 test commands; aligned story File List and set story status to done.
