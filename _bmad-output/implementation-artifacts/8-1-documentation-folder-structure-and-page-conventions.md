# Story 8.1: Documentation Folder Structure & Page Conventions

Status: ready-for-dev

## Story

As a documentation author (Jerome or AI agent),
I want a standardized folder structure and page conventions established,
So that all future documentation follows consistent patterns and renders correctly on GitHub.

## Acceptance Criteria

1. **AC1 - Folder Structure Created**: The following folder structure exists:
   ```
   docs/
   ├── getting-started/
   ├── concepts/
   ├── guides/
   ├── reference/
   ├── community/
   └── assets/
       ├── diagrams/
       └── images/
   ```

2. **AC2 - Root Level Files**: The following root-level placeholder files exist:
   - `CONTRIBUTING.md` (placeholder with "Coming soon" content)
   - `CODE_OF_CONDUCT.md` (placeholder with "Coming soon" content)
   - `CHANGELOG.md` (placeholder with initial version entry)

3. **AC3 - Page Template Convention**: A `docs/page-template.md` file documents the standard page structure:
   - Back-link to README: `[← Back to Hexalith.EventStore](../../README.md)`
   - H1 title (one per page)
   - One-paragraph summary
   - Optional prerequisites callout (max 2 per NFR10)
   - Content sections
   - Next Steps footer with "Next:" and "Related:" links

4. **AC4 - File Naming Convention**: All documentation files follow:
   - Lowercase letters only
   - Hyphen-separated words (kebab-case)
   - Descriptive, unabbreviated names (e.g., `configuration-reference.md` not `config-ref.md`)

5. **AC5 - Cross-Linking Convention**: Documentation uses relative links only:
   - Same folder: `[link](file.md)`
   - Parent folder: `[link](../folder/file.md)`
   - Never absolute URLs for internal links

6. **AC6 - Assets Convention**: Media files are centralized:
   - All images in `docs/assets/images/`
   - All diagrams in `docs/assets/diagrams/`
   - GIF demo at `docs/assets/quickstart-demo.gif` (placeholder or empty)

7. **AC7 - No YAML Frontmatter**: Pages do not use YAML frontmatter (GitHub renders it as visible text)

8. **AC8 - Index Files**: Each subfolder has a `.gitkeep` or placeholder `index.md` file to ensure folders are tracked

## Tasks / Subtasks

- [ ] Task 1: Create docs folder structure (AC: 1, 8)
  - [ ] Create `docs/getting-started/` with `.gitkeep`
  - [ ] Create `docs/concepts/` with `.gitkeep`
  - [ ] Create `docs/guides/` with `.gitkeep`
  - [ ] Create `docs/reference/` with `.gitkeep`
  - [ ] Create `docs/community/` with `.gitkeep`
  - [ ] Create `docs/assets/` folder
  - [ ] Create `docs/assets/diagrams/` with `.gitkeep`
  - [ ] Create `docs/assets/images/` with `.gitkeep`

- [ ] Task 2: Create root-level documentation files (AC: 2)
  - [ ] Create `CONTRIBUTING.md` with placeholder content
  - [ ] Create `CODE_OF_CONDUCT.md` with placeholder content
  - [ ] Create `CHANGELOG.md` with initial entry

- [ ] Task 3: Create page template documentation (AC: 3, 4, 5, 7)
  - [ ] Create `docs/page-template.md` documenting conventions
  - [ ] Include back-link pattern
  - [ ] Include H1 + summary pattern
  - [ ] Include prerequisites callout format
  - [ ] Include Next Steps footer format
  - [ ] Document file naming conventions
  - [ ] Document cross-linking conventions
  - [ ] Note no YAML frontmatter rule

- [ ] Task 4: Create assets placeholder (AC: 6)
  - [ ] Add placeholder or `.gitkeep` at `docs/assets/quickstart-demo.gif` location
  - [ ] Document expected asset locations in page template

## Dev Notes

### Architecture Source

This story implements **Decision D1: Content Folder Structure & Page Conventions** from `_bmad-output/planning-artifacts/architecture-documentation.md`.

### Key Technical Decisions

**D1 Edge Case Defaults:**
| Concern | Default | Rationale |
|---------|---------|-----------|
| `docs/reference/api/` | Committed to repo, regenerated only on release tags (Phase 2) | GitHub browsing must work |
| Page template | Informal convention: H1 title, one-paragraph summary, content. No frontmatter. | GitHub doesn't render YAML frontmatter |
| Cross-linking | Relative links (e.g., `../concepts/architecture-overview.md`) | Works in GitHub and future docs site |
| Assets | Centralized `docs/assets/` with `diagrams/` and `images/` subdirectories | Single location for all media |
| File naming | Lowercase, hyphen-separated, descriptive | Matches PRD convention (NFR26) |

### NFRs This Story Supports

- **NFR6**: Heading hierarchy H1-H4 with no skipped levels (page template defines this)
- **NFR25**: H1 title + one-paragraph summary per page (page template defines this)
- **NFR26**: Descriptive filenames with hyphens, no abbreviations (naming convention)
- **NFR27**: No page more than 2 clicks from README (cross-linking strategy)
- **NFR10**: Max 2 prerequisite pages (prerequisites callout limit)

### Page Template Structure

Every documentation page MUST follow:

```markdown
[← Back to Hexalith.EventStore](../../README.md)

# Page Title

One-paragraph summary of what this page covers and who it's for.

> **Prerequisites:** [Prerequisite 1](link), [Prerequisite 2](link)
>
> (Maximum 2 prerequisites per NFR10. Omit if none required.)

## Main Content Sections

(Page-specific content)

## Next Steps

- **Next:** [Logical next page](link) — one-sentence description
- **Related:** [Related page 1](link), [Related page 2](link)
```

### Markdown Formatting Rules

| Pattern | Rule |
|---------|------|
| Heading hierarchy | H1 = page title (one per page), H2 = major sections, H3 = subsections. Never skip levels. |
| Code blocks | Always specify language: ` ```csharp `, ` ```bash `, ` ```yaml `. Never bare fences. |
| Terminal commands | Use `bash` language tag. Prefix commands with `$`. |
| Callouts | Use GitHub blockquote syntax: `> **Note:**`, `> **Warning:**`, `> **Tip:**` |
| Tables | Use for structured comparisons. Keep cells concise. Always include header row. |
| Lists | Ordered for sequential steps. Unordered for non-sequential items. |
| Line length | No hard wrap in markdown source |

### Folder Purpose Reference

| Folder | Content Type | Phase |
|--------|-------------|-------|
| `getting-started/` | Prerequisites, quickstart, first domain service | 1a, 1b |
| `concepts/` | Architecture overview, event envelope, identity, lifecycle, decision aid | 1b |
| `guides/` | Deployment guides, configuration reference, security, troubleshooting, DAPR FAQ | 2 |
| `reference/` | Command API, NuGet packages, auto-generated API docs | 1b, 2 |
| `community/` | Awesome event sourcing, roadmap | 1b, 2 |
| `assets/` | GIF demo, diagrams, images | 1a |

### CONTRIBUTING.md Placeholder Content

```markdown
# Contributing to Hexalith.EventStore

Thank you for your interest in contributing! This guide is coming soon.

In the meantime, please:
- Open an issue to discuss your idea before submitting a PR
- Follow the existing code style and conventions
- Ensure all tests pass before submitting

Full contribution guidelines will be available shortly.
```

### CODE_OF_CONDUCT.md Placeholder Content

```markdown
# Code of Conduct

Hexalith.EventStore is committed to providing a welcoming and inclusive environment for all contributors.

A comprehensive Code of Conduct is being prepared and will be published soon.

In the meantime, please treat all community members with respect and professionalism.
```

### CHANGELOG.md Initial Content

```markdown
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Documentation folder structure and page conventions (Story 8.1)
```

### Project Structure Notes

**Alignment with unified project structure:**
- `docs/` folder is at repository root level, parallel to `src/`, `tests/`, `samples/`
- Assets are centralized under `docs/assets/` rather than scattered
- Root-level markdown files (`CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md`) follow GitHub conventions

**No conflicts detected** - the `docs/` folder does not currently exist in the repository.

### Testing Standards

This story creates infrastructure (folders, templates, conventions) rather than code. Validation:

1. **Manual verification**: All folders exist with correct structure
2. **File naming check**: All files follow kebab-case lowercase convention
3. **Template completeness**: Page template includes all required sections
4. **Placeholder content**: Root files have meaningful placeholder text

### What NOT to Do

- Do NOT create actual documentation content (that's future stories)
- Do NOT add YAML frontmatter to any markdown files
- Do NOT use abbreviations in file names
- Do NOT create nested subfolders beyond the defined structure
- Do NOT add CI validation for documentation yet (that's Story 11.3)

### References

- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D1] - Folder structure decision
- [Source: _bmad-output/planning-artifacts/prd-documentation.md] - NFR definitions
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D7] - Cross-linking strategy

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
