# Story 13.3: Progressive Documentation Structure

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want documentation organized by expertise level,
So that newcomers aren't overwhelmed and experts aren't condescended to.

## Acceptance Criteria

1. **Given** the documentation set, **When** organized, **Then** it follows a progressive structure: quick start (assumes DDD knowledge), concepts (for newcomers), reference (deep dives) (UX-DR33) **And** levels are never mixed within a single page.
2. **Given** projection type naming guidance, **When** documented, **Then** short names are recommended (e.g., `OrderList` rather than fully qualified type names) for compact ETags (FR64).

## Tasks / Subtasks

- [x] Task 0: Prerequisites (AC: #1, #2)
  - [x] 0.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- confirm baseline compiles (0 errors, 0 warnings)
  - [x] 0.2 Inventory all existing docs pages: `find docs/ -name "*.md" | sort`
  - [x] 0.3 Read `docs/page-template.md` to confirm page structure conventions

- [x] Task 1: Create documentation index page (AC: #1)
  - [x] 1.1 Create `docs/index.md` -- a documentation home page that organizes all docs into explicit progressive tiers. Each linked page MUST have a one-line description (matching README style, not a bare link list). Exclude internal/planning files (`fr-traceability.md`, `superpowers/`, `page-template.md`, `assets/`) from the index — these are not user-facing documentation. Tiers:
    - **Getting Started** (quickstart level, assumes DDD knowledge): `getting-started/quickstart.md`, `getting-started/prerequisites.md`, `getting-started/first-domain-service.md`
    - **Concepts** (newcomer-friendly explanations): all 6 files in `concepts/`
    - **Guides** (task-oriented how-to): all 11 files in `guides/`
    - **Reference** (deep dives, API contracts): `reference/command-api.md`, `reference/query-api.md`, `reference/nuget-packages.md`, `reference/problems/index.md`, `reference/api/index.md`
    - **Community** (ecosystem resources): `community/roadmap.md`, `community/awesome-event-sourcing.md`
  - [x] 1.2 Each tier section includes a one-line description of the audience and level (e.g., "For developers who know DDD and want to get running fast", "For newcomers who want to understand how the system works", "For developers who need exact API contracts and specifications")
  - [x] 1.3 Follow `docs/page-template.md` conventions: back-link, one H1, summary paragraph, Next Steps footer

- [x] Task 2: Audit all documentation pages for level mixing (AC: #1)
  - [x] 2.1 Read every page in `docs/getting-started/` and verify they assume DDD knowledge, stay at quickstart level (step-by-step, hands-on), and do not include deep reference material
  - [x] 2.2 Read every page in `docs/concepts/` and verify they explain conceptual "why" and "what", targeted at developers new to the system, without mixing in quickstart-level "do this now" steps or reference-level API contract details
  - [x] 2.3 Read every page in `docs/guides/` and verify they are task-oriented how-to content (deployment, configuration, troubleshooting) without mixing quickstart-level introductory material or reference-level API specs
  - [x] 2.4 Read every page in `docs/reference/` (excluding `api/` auto-generated pages and `problems/` from Story 13-2) and verify they provide precise API contracts and specifications without mixing in conceptual explanations or quickstart steps
  - [x] 2.5 For any page with level mixing found, create a remediation sub-task:
    - Extract misplaced content to the correct tier page (or create a new page if needed)
    - Replace extracted content with a cross-link: "For details, see [Page Name](link)"
    - Ensure the page reads coherently after extraction
    - **Heuristic:** if removing the content wouldn't hurt the page's teaching goal, it belongs in Reference. If the content IS the teaching, it stays. Small illustrative code snippets in Concepts pages are fine; exhaustive API contract tables are not.
  - [x] 2.6 Document the audit results in the Dev Agent Record section: list which pages were clean and which needed remediation

- [x] Task 3: Add projection type naming guidance (AC: #2)
  - [x] 3.1 Add a section to `docs/reference/query-api.md` titled "Projection Type Naming" that:
    - Recommends short projection type names (e.g., `OrderList`, `ProductCatalog`) instead of fully qualified type names (e.g., `MyCompany.Sales.Projections.OrderListProjection`)
    - Explains that projection type names are base64url-encoded in ETags and longer names produce proportionally longer ETag tokens in HTTP headers
    - Provides 2-3 good vs. bad naming examples in a comparison table
    - References FR64
  - [x] 3.2 Cross-link from `docs/reference/nuget-packages.md` or `docs/reference/command-api.md` to the projection type naming section if there's a natural anchor point. Do NOT cross-link from concepts pages — that would itself be level mixing.

- [x] Task 4: Verify cross-linking and navigation (AC: #1)
  - [x] 4.1 Verify every page has a "Next Steps" footer with "Next:" and "Related:" links per `page-template.md`
  - [x] 4.2 Verify every page's back-link at the top points to `../../README.md` (or correct relative path)
  - [x] 4.3 Verify the README Documentation section links match the progressive tier organization in `docs/index.md`
  - [x] 4.4 Add a link to `docs/index.md` from README.md (e.g., "Full documentation index: [Documentation](docs/index.md)")

- [x] Task 5: Build and test (AC: #1, #2)
  - [x] 5.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- 0 errors, 0 warnings
  - [x] 5.2 Run all Tier 1 tests -- 0 regressions
  - [x] 5.3 Verify no broken relative links in modified/new docs (grep for `](` and spot-check paths exist)

## Dev Notes

### CRITICAL: This Is Primarily a Documentation Restructuring Story -- Minimal Source Code Changes

This story creates 1 new documentation page (`docs/index.md`), adds content to `docs/reference/query-api.md` (projection type naming section), and may edit existing docs to fix level mixing. No C# source code changes expected.

### Current Documentation Structure (as of Story 13-2 completion)

The docs folder already follows a roughly progressive structure:

```
docs/
  getting-started/    (3 files) -- quickstart level
    quickstart.md           -- 10-minute clone-to-command guide
    prerequisites.md        -- environment setup
    first-domain-service.md -- building your first domain service
  concepts/           (6 files) -- newcomer concepts level
    architecture-overview.md     -- system topology and design
    choose-the-right-tool.md     -- when Hexalith fits
    command-lifecycle.md         -- command processing pipeline
    event-envelope.md            -- 14-field event metadata
    event-versioning.md          -- schema evolution
    identity-scheme.md           -- tenant:domain:aggregate-id
  guides/             (11 files) -- task-oriented how-to
    configuration-reference.md
    dapr-component-reference.md
    dapr-faq.md
    deployment-azure-container-apps.md
    deployment-docker-compose.md
    deployment-kubernetes.md
    deployment-progression.md
    disaster-recovery.md
    security-model.md
    troubleshooting.md
    upgrade-path.md
  reference/          (deep dives, API contracts)
    api/              (auto-generated type docs)
    command-api.md    -- REST command API spec
    query-api.md      -- REST/SignalR query API spec
    nuget-packages.md -- package guide
    problems/         (14 files from Story 13-2)
  community/          (2 files)
    awesome-event-sourcing.md
    roadmap.md
  page-template.md    -- documentation conventions
  fr-traceability.md  -- FR traceability matrix
  assets/             -- images, diagrams, demos
  superpowers/        -- internal planning specs
```

### Progressive Level Definitions (UX-DR33)

| Level | Folder | Audience | Content Style |
|-------|--------|----------|---------------|
| **Quick Start** | `getting-started/` | Developers who know DDD, want to run the system fast | Step-by-step, hands-on, copy-paste commands, 3 pages max |
| **Concepts** | `concepts/` | Newcomers who want to understand how the system works | Explains "why" and "what", uses diagrams, no CLI commands to run |
| **Guides** | `guides/` | Developers performing specific operational tasks | Task-oriented "how-to", assumes system understanding, provides procedures |
| **Reference** | `reference/` | Developers who need exact API contracts and specifications | Precise, terse, exhaustive, no explanations of why |
| **Community** | `community/` | Anyone interested in the project ecosystem | Roadmap, resources, ecosystem links |

### Level Mixing Anti-Patterns to Watch For

- A **Getting Started** page that includes a deep dive into ProblemDetails response format (should link to reference instead)
- A **Concepts** page that includes exact curl commands to try (should link to getting-started or guides)
- A **Reference** page that starts with "What is event sourcing?" (should link to concepts)
- A **Guides** page that starts with "First, let's understand the architecture..." (should link to concepts)

**Decision heuristic:** If removing the content wouldn't hurt the page's teaching goal, it belongs elsewhere. If the content IS the teaching, it stays. Small illustrative code snippets in Concepts pages are fine; exhaustive API contract tables are not.

### Files Excluded from Progressive Index

These files exist in `docs/` but are NOT user-facing documentation and should NOT appear in `docs/index.md`:
- `docs/fr-traceability.md` — internal FR traceability matrix (planning artifact)
- `docs/superpowers/` — internal planning specs and design documents
- `docs/page-template.md` — documentation conventions (meta-doc for contributors, not readers)
- `docs/assets/` — media files (referenced by other pages, not standalone docs)

### Page Template Conventions (from `docs/page-template.md`)

Every doc page MUST:
- Start with a back-link: `[← Back to Hexalith.EventStore](../../README.md)`
- Have exactly one H1 heading (page title)
- Have a one-paragraph summary after the title
- Have optional Prerequisites callout (max 2 prerequisites)
- End with "Next Steps" footer with "Next:" and "Related:" links
- Use kebab-case filenames, no YAML frontmatter
- Use language-tagged code blocks (`csharp`, `bash`, `yaml`)

### FR64 Projection Type Naming Guidance Content

The section to add in `docs/reference/query-api.md` should cover:

- Projection type names are used in self-routing ETags (format: `{base64url(projectionType)}.{guid}`)
- Longer names = longer ETag tokens in HTTP headers
- Recommended: short, descriptive names (2-3 words max)

| Good | Bad | Reason |
|------|-----|--------|
| `OrderList` | `MyCompany.Sales.Projections.OrderListProjection` | Short name = compact ETag |
| `ProductCatalog` | `Ecommerce.Inventory.ReadModels.ProductCatalogReadModel` | No namespace needed |
| `UserProfile` | `ApplicationLayer.Identity.Projections.UserProfileProjectionV2` | No version suffix needed |

### Editing Scope

**Files you MUST create:**
- `docs/index.md` -- documentation home page with progressive tier organization

**Files you MUST edit:**
- `docs/reference/query-api.md` -- add projection type naming section (FR64)
- `README.md` -- add link to `docs/index.md`

**Files you MAY edit** (if level mixing found during audit):
- Any existing `docs/**/*.md` file (to fix level mixing by extracting content or adding cross-links)

**Files you MUST NOT edit:**
- Any C# source files
- `docs/reference/api/**/*.md` (auto-generated, do not hand-edit)
- `docs/reference/problems/**/*.md` (created by Story 13-2, should not be restructured)
- `docs/page-template.md` (conventions doc, not a content page to restructure)
- `docs/assets/**` (media files)

### What NOT to Do

- Do NOT modify any source code -- this is documentation only
- Do NOT restructure the auto-generated `docs/reference/api/` pages
- Do NOT restructure the error reference pages from Story 13-2
- Do NOT create a separate documentation site (these are Markdown files browsed on GitHub)
- Do NOT add YAML frontmatter to pages (per page-template.md)
- Do NOT rewrite entire pages during level-mixing remediation -- make surgical edits to extract misplaced content and add cross-links
- Do NOT change file names of existing docs pages (would break external links)
- Do NOT add unit tests -- there is no testable code change

### Branch Base Guidance

Branch from `main`. Branch name: `docs/story-13-3-progressive-documentation-structure`

### Previous Story Intelligence

**Story 13-2 (Error Reference Pages at Type URIs) learnings:**
- Documentation-only story, 14 new Markdown files created
- Source of truth principle: codebase is always right, docs must match code
- Page structure template strictly followed: back-link, H1, summary, prerequisites, content sections, Next Steps footer
- Writing style: scannable, not essays. Short sentences, bullet points, code blocks
- Forbidden term compliance verified via grep
- Build: 0 errors, 0 warnings. 724 Tier 1 tests pass (271 Contracts + 297 Client + 62 Sample + 67 Testing + 27 SignalR)
- Branch naming convention: `docs/story-13-2-error-reference-pages-at-type-uris`
- Commit message style: `docs: <description> (Story 13-2)`

**Story 13-1 (Quick Start Guide) learnings:**
- Source of truth principle applied: found and fixed gaps between docs and code
- Two gaps found: missing `messageId` field, missing Aspire CLI prerequisite
- Verified quickstart stays at hands-on level (does not deep-dive into internals)

### Git Intelligence

Recent commits show Stories 13-1 and 13-2 completed:
- `67c9bd2` Merge PR #132: docs/story-13-2-error-reference-pages-at-type-uris
- `6f8d4e6` docs: Add error reference pages at type URIs (Story 13-2)
- `b17d298` Merge PR #131: docs/story-13-1-quick-start-guide
- `3eca00e` docs: Update quick start guide with Aspire CLI prerequisite and messageId field (Story 13-1)

Epic 13 is in-progress. Stories 13-1 and 13-2 done. Codebase stable on main.

### Architecture Compliance

- **File locations:** New index page at `docs/index.md`, projection naming section in existing `docs/reference/query-api.md`
- **Solution file:** `Hexalith.EventStore.slnx` (modern XML format)
- **Build command:** `dotnet build Hexalith.EventStore.slnx --configuration Release`
- **Warnings as errors:** enabled -- build must produce 0 warnings

### Testing Compliance

No new tests needed -- this is a documentation restructuring story. Tier 1 tests run as regression verification only.

### Project Structure Notes

- The `docs/` folder is already roughly progressive: `getting-started/` → `concepts/` → `guides/` → `reference/` → `community/`
- The README Documentation section already groups links by these tiers
- The missing piece is an explicit `docs/index.md` that maps the structure and serves as the landing page
- Level mixing audit may reveal pages that need surgical edits
- FR64 projection type naming guidance fits naturally in `docs/reference/query-api.md` alongside the ETag and projection documentation already there

### References

- [Source: _bmad-output/planning-artifacts/epics.md, Epic 13, Story 13.3 (lines 1525-1541)]
- [Source: _bmad-output/planning-artifacts/epics.md, UX-DR33 (line 313)]
- [Source: _bmad-output/planning-artifacts/prd.md, FR64 (line 849)]
- [Source: docs/page-template.md -- documentation conventions and page structure rules]
- [Source: docs/reference/query-api.md -- existing query/projection API reference where FR64 guidance will be added]
- [Source: README.md, Documentation section (lines 104-136) -- current progressive navigation structure]

### Definition of Done

- AC #1 verified: documentation follows progressive structure (quick start → concepts → reference) with levels never mixed within a single page
- `docs/index.md` created with explicit tier organization and audience descriptions
- Level mixing audit completed with remediation applied (documented in Dev Agent Record)
- AC #2 verified: projection type naming guidance added to `docs/reference/query-api.md` recommending short names for compact ETags
- README updated with link to `docs/index.md`
- Cross-linking verified: all pages have Next Steps footers, back-links, and tier-appropriate cross-references
- Build: `dotnet build Hexalith.EventStore.slnx --configuration Release` -- 0 errors, 0 warnings
- Tier 1 tests pass with 0 regressions

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

### Completion Notes List

- Task 0: Build baseline confirmed (0 errors, 0 warnings), all docs inventoried, page-template.md conventions reviewed
- Task 1: Created `docs/index.md` with progressive tier organization (Getting Started, Concepts, Guides, Reference, Community), audience descriptions per tier, back-link, H1, summary, Next Steps footer
- Task 2 Level Mixing Audit:
  - **getting-started/ (3 files):** All clean — step-by-step hands-on, assumes DDD knowledge
  - **concepts/ (6 files):** All clean — conceptual "why/what", newcomer-friendly, no CLI steps or API tables
  - **guides/ (11 files):** All clean — `security-model.md` has architectural overview but it provides essential context for configuration sections (removing it would hurt the guide's teaching goal)
  - **reference/ (3 files, excl. api/ and problems/):** All clean — `command-api.md` "Complete Flow Example" is a compact quick-reference recipe (not a guide walkthrough), `query-api.md` SignalR pattern is illustrative, `nuget-packages.md` "Which Packages" is package selection reference
  - **community/ (2 files):** All clean — ecosystem resources and project direction
  - **No remediation needed** — all borderline content serves each page's teaching goal per the heuristic
- Task 3: Added "Projection Type Naming" section to `docs/reference/query-api.md` with FR64 guidance (3-row comparison table, guidelines, base64url ETag explanation). Cross-linked from `docs/reference/nuget-packages.md` SignalR section.
- Task 4: All 27 user-facing pages verified — all have back-links to README.md and Next Steps footers. README Documentation section matches index tier organization. Added "Full documentation index" link to README.md.
- Task 5: Build 0 errors / 0 warnings. 724 Tier 1 tests pass (271 Contracts + 297 Client + 62 Sample + 67 Testing + 27 SignalR). All links in new/modified docs verified.

### Change Log

- Created `docs/index.md` — documentation home page with progressive tier organization (Getting Started, Concepts, Guides, Reference, Community)
- Added "Projection Type Naming" section to `docs/reference/query-api.md` (FR64)
- Added cross-link to projection naming from `docs/reference/nuget-packages.md`
- Added "Full documentation index" link to `README.md`
- Date: 2026-03-20

### File List

- docs/index.md (new)
- docs/reference/query-api.md (modified — added Projection Type Naming section)
- docs/reference/nuget-packages.md (modified — added cross-link to projection naming)
- README.md (modified — added link to docs/index.md)
